using System.Globalization;
using System.Numerics;
using System.Text;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Svg;

// SvgCodec.TextLayout.cs - the layout of a text element as styled, positioned runs.
//
// A text element's content is a tree of tspan and a elements around character data. The layout follows the SVG 2
// "text layout" steps, simplified to horizontal text:
//
//   1. Collect the addressable characters in document order. Each tspan/a gets its own cascaded state from
//      ApplyPresentationAttributes (presentation attributes, style attribute and stylesheet rules), and title, desc and
//      metadata are not content. White space follows CSS 'white-space: normal' as browsers do (tab, CR and LF become a
//      space, spaces collapse across element boundaries, and the whole text is trimmed), or, under xml:space="preserve",
//      only maps tab, CR and LF to a space.
//   2. Resolve the x, y, dx and dy lists of the text and of every tspan per character; a descendant's entry overrides
//      its ancestor's.
//   3. Place runs: an absolute x or y starts a new text chunk, dx/dy move the pen (a dy persists for the following text),
//      and consecutive characters of the same element without their own position form one run. A run with
//      letter-spacing or word-spacing is walked grapheme cluster by grapheme cluster: the letter spacing follows every
//      cluster (the last one too) and the word spacing every space and no-break space, on top of the kerned advance.
//   4. Anchor each chunk by the text-anchor of its first character; the chunk width includes the spacing after its last
//      character, as in browsers. A chunk of a single run without spacing is laid out with exactly the arithmetic a
//      plain text always used, so text without tspan, position lists or spacing draws exactly as before.
//
// Rendering, the filter bounds pre-pass and clip-path text all use this one layout, so they always agree.
public static partial class SvgCodec
{
    /// <summary>
    ///     One laid-out run of a text: the glyph outlines of consecutive characters that share an element and a position.
    /// </summary>
    /// <param name="Path">The run's glyph outlines in the text's local space; never empty.</param>
    /// <param name="State">
    ///     The cascaded state of the run's element. Its opacity is relative to the <c>text</c> element: the layout starts
    ///     from the text's state with an opacity of 1, so only the opacity of nested <c>tspan</c>/<c>a</c> elements is in it.
    /// </param>
    /// <param name="Element">The innermost element (<c>text</c>, <c>tspan</c> or <c>a</c>) the run's characters belong to.</param>
    private readonly record struct TextRun(Path Path, RenderState State, XElement Element);

    /// <summary>
    ///     One text content element of a text: the <c>text</c> itself or a nested <c>tspan</c>/<c>a</c>.
    /// </summary>
    /// <param name="Element">The element.</param>
    /// <param name="State">Its cascaded state.</param>
    /// <param name="BaselineShift">The accumulated <c>baseline-shift</c> of the element and its ancestors, in user units, positive up.</param>
    /// <param name="PreserveSpaces"><see langword="true"/> under <c>xml:space="preserve"</c>.</param>
    private sealed record TextSpan(XElement Element, RenderState State, float BaselineShift, bool PreserveSpaces);

    /// <summary>
    ///     One addressable character of a text.
    /// </summary>
    /// <param name="Value">The UTF-16 code unit, with tab, CR and LF already mapped to a space.</param>
    /// <param name="Span">The index of the character's innermost element in <see cref="TextContent.Spans"/>.</param>
    /// <param name="Collapsible"><see langword="true"/> for a space that white-space processing may remove.</param>
    private readonly record struct TextCharacter(char Value, int Span, bool Collapsible);

    /// <summary>
    ///     A <c>text</c> or <c>tspan</c> element and the range of characters its position lists address.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="state">Its cascaded state, the basis of <c>em</c> and <c>%</c> in its lists.</param>
    /// <param name="start">The index of its first character.</param>
    private sealed class PositionedElement(XElement element, RenderState state, int start)
    {
        /// <summary>Gets the element.</summary>
        public XElement Element { get; } = element;

        /// <summary>Gets the element's cascaded state.</summary>
        public RenderState State { get; } = state;

        /// <summary>Gets the index of the element's first character.</summary>
        public int Start { get; } = start;

        /// <summary>Gets or sets the index after the element's last character.</summary>
        public int End { get; set; } = start;
    }

    /// <summary>
    ///     The collected content of one <c>text</c> element, built by <see cref="CollectTextContent"/>.
    /// </summary>
    /// <param name="text">The <c>text</c> element.</param>
    /// <param name="collector">The unsupported-feature collector, or <see langword="null"/> when nothing is reported.</param>
    private sealed class TextContent(XElement text, UnsupportedFeatureCollector? collector)
    {
        /// <summary>Gets the <c>text</c> element.</summary>
        public XElement Text { get; } = text;

        /// <summary>Gets the unsupported-feature collector, or <see langword="null"/> when nothing is reported.</summary>
        public UnsupportedFeatureCollector? Collector { get; } = collector;

        /// <summary>Gets the addressable characters in document order.</summary>
        public List<TextCharacter> Characters { get; } = [];

        /// <summary>Gets the text content elements, in document order (the <c>text</c> first).</summary>
        public List<TextSpan> Spans { get; } = [];

        /// <summary>Gets the <c>text</c> and <c>tspan</c> elements, in document order, with their character ranges.</summary>
        public List<PositionedElement> Positioned { get; } = [];
    }

    /// <summary>
    ///     The resolved per-character positions of a text: an absolute <c>x</c>/<c>y</c> and a relative <c>dx</c>/<c>dy</c>,
    ///     each <see langword="null"/> where no list addresses the character.
    /// </summary>
    /// <param name="X">The absolute x positions.</param>
    /// <param name="Y">The absolute y positions.</param>
    /// <param name="Dx">The relative x shifts.</param>
    /// <param name="Dy">The relative y shifts.</param>
    private sealed record CharacterPositions(float?[] X, float?[] Y, float?[] Dx, float?[] Dy)
    {
        /// <summary>Determines whether any list addresses a character, which then starts a new run.</summary>
        /// <param name="index">The character index.</param>
        /// <returns><see langword="true"/> when the character has its own position or shift.</returns>
        public bool HasAny(int index) => X[index].HasValue || Y[index].HasValue || Dx[index].HasValue || Dy[index].HasValue;
    }

    /// <summary>
    ///     A run being placed in a chunk, before the chunk is anchored.
    /// </summary>
    /// <param name="Text">The run's characters.</param>
    /// <param name="Span">The run's element.</param>
    /// <param name="Font">The run's resolved font.</param>
    /// <param name="Pen">The pen position of the run's first character, before anchoring and baseline shift.</param>
    /// <param name="LetterSpacing">The run's resolved <c>letter-spacing</c> in user units; 0 for none.</param>
    /// <param name="WordSpacing">The run's resolved <c>word-spacing</c> in user units; 0 for none.</param>
    private readonly record struct PendingRun(
        string Text, TextSpan Span, TrueTypeFont Font, Vector2 Pen, float LetterSpacing, float WordSpacing)
    {
        /// <summary>Gets a value indicating whether the run has letter or word spacing to lay out.</summary>
        public bool IsSpaced => LetterSpacing != 0f || WordSpacing != 0f;
    }

    /// <summary>
    ///     A computed <c>letter-spacing</c> or <c>word-spacing</c> value: a length plus a fraction of the font size.
    /// </summary>
    /// <remarks>
    ///     A plain percentage is kept as a fraction, so it is inherited as a percentage and resolved with the font size of
    ///     each character, as browsers do (<c>50%</c> on a group of 10 px font size spaces its 20 px text by 10). Every
    ///     other value - a number, a length, <c>em</c>, or a <c>calc()</c> expression - is resolved at the element that
    ///     declares it and inherited as a length; a <c>calc()</c> that mixes in a percentage therefore resolves the
    ///     percentage with the declaring element's font size (a documented approximation). The default value is zero
    ///     (<c>normal</c>).
    /// </remarks>
    /// <param name="Length">The length part, in user units.</param>
    /// <param name="FontSizeFraction">The part relative to the font size (0.5 for <c>50%</c>).</param>
    private readonly record struct TextSpacing(float Length, float FontSizeFraction)
    {
        /// <summary>Resolves the spacing for a character of the given font size.</summary>
        /// <param name="fontSize">The character's font size, in user units.</param>
        /// <returns>The spacing in user units; may be negative.</returns>
        public float Resolve(float fontSize) => Length + (FontSizeFraction * fontSize);
    }

    /// <summary>
    ///     Lays out a <c>text</c> element as styled, positioned runs (see this file's header for the steps).
    /// </summary>
    /// <param name="text">The <c>text</c> element.</param>
    /// <param name="textState">The text's cascaded state.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="report">
    ///     <see langword="true"/> to record unsupported features in the context's collector (the paint and the clip path);
    ///     <see langword="false"/> for the bounds pre-pass, which must not report a text twice.
    /// </param>
    /// <returns>The runs that have glyph outlines, in document order; empty when nothing is drawn.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when an <c>x</c> or <c>y</c> list holds a value that is not a valid length (class S, as for any
    ///     geometry), when the nesting of <c>tspan</c>/<c>a</c> exceeds the element depth limit, or when the cascade of a
    ///     nested element fails.
    /// </exception>
    /// <remarks>
    ///     The caller charges the geometry work budget with the text's character count first. The work is linear in the
    ///     number of characters and elements: every character is visited a fixed number of times, a position list is read
    ///     only as far as its element has characters, and each run is measured and outlined once.
    /// </remarks>
    private static List<TextRun> LayoutText(XElement text, RenderState textState, RenderContext context, bool report)
    {
        // Step 1: the characters, white-space processed, each tagged with its innermost element
        var content = new TextContent(text, report ? context.Unsupported : null);
        var rootState = textState with { Opacity = 1f };
        var rootShift = ResolveBaselineShift(text, rootState, context, content.Collector, text);
        CollectTextContent(text, new TextSpan(text, rootState, rootShift, IsXmlSpacePreserved(text)), content, context, 0);
        TrimTrailingSpaces(content);
        if (content.Characters.Count == 0)
        {
            return [];
        }

        // A feature the layout ignores is reported only for a text that draws something
        if (content.Collector is { } collector)
        {
            CollectUnsupportedTextFeatures(content, context, collector);
        }

        // Steps 2 to 4: the positions, the runs and the chunk anchors
        var positions = ResolveCharacterPositions(content);
        return PlaceTextRuns(content, positions, context);
    }

    /// <summary>
    ///     Counts the characters of a text's content, the amount charged against the geometry work budget before layout.
    /// </summary>
    /// <param name="text">The <c>text</c> element.</param>
    /// <returns>The number of UTF-16 code units of every descendant text node (the length of the element's value).</returns>
    private static int CountTextCharacters(XElement text) =>
        text.DescendantNodes().OfType<XText>().Sum(node => node.Value.Length);

    /// <summary>
    ///     Unions the outlines of laid-out runs into one path: the geometry of a clip path, and the object bounding box
    ///     basis of the text's paint servers and effects.
    /// </summary>
    /// <param name="runs">The runs; at least one.</param>
    /// <returns>The single run's own path, or the union of all.</returns>
    private static Path CombineTextRuns(IReadOnlyList<TextRun> runs)
    {
        if (runs.Count == 1)
        {
            return runs[0].Path;
        }

        var builder = new PathBuilder();
        foreach (var run in runs)
        {
            builder.AppendPath(run.Path);
        }

        return builder.Build();
    }

    /// <summary>
    ///     Unions the stroke-expanded local bounds of laid-out runs, each with its own stroke.
    /// </summary>
    /// <param name="runs">The runs.</param>
    /// <returns>The union, or <see cref="Rect.Empty"/> for no runs.</returns>
    private static Rect ComputeTextRunsBounds(IReadOnlyList<TextRun> runs)
    {
        var bounds = Rect.Empty;
        foreach (var run in runs)
        {
            bounds = bounds.Union(ExpandBoundsForStroke(run.Path.GetBounds(), run.State));
        }

        return bounds;
    }

    /// <summary>
    ///     Collects the characters of one text content element and, recursively, of its <c>tspan</c>/<c>a</c> children.
    /// </summary>
    /// <param name="element">The <c>text</c>, <c>tspan</c> or <c>a</c> element.</param>
    /// <param name="span">The element's span (state, baseline shift, white-space mode).</param>
    /// <param name="content">The content being collected.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="depth">The nesting depth below the <c>text</c> element.</param>
    /// <exception cref="InvalidDataException">Thrown when the nesting exceeds <see cref="MaxElementDepth"/>.</exception>
    private static void CollectTextContent(XElement element, TextSpan span, TextContent content, RenderContext context, int depth)
    {
        var spanIndex = content.Spans.Count;
        content.Spans.Add(span);
        var positioned = element.Name.LocalName is "text" or "tspan"
            ? new PositionedElement(element, span.State, content.Characters.Count)
            : null;
        if (positioned != null)
        {
            content.Positioned.Add(positioned);
        }

        foreach (var node in element.Nodes())
        {
            if (node is XText characters)
            {
                AppendTextCharacters(content, characters.Value, spanIndex, span.PreserveSpaces);
                continue;
            }

            if (node is not XElement child)
            {
                continue;
            }

            switch (child.Name.LocalName)
            {
                case "tspan":
                case "a":
                    {
                        // Same fail-fast guard as the element tree walk: recursion must stay bounded
                        if (depth + 1 >= MaxElementDepth)
                        {
                            throw new InvalidDataException("SVG element nesting exceeds the supported depth.");
                        }

                        var childState = ApplyPresentationAttributes(span.State, child, context);
                        var shift = span.BaselineShift + ResolveBaselineShift(child, childState, context, content.Collector, content.Text);
                        var preserve = ReadXmlSpace(child) ?? span.PreserveSpaces;
                        CollectTextContent(child, new TextSpan(child, childState, shift, preserve), content, context, depth + 1);
                        break;
                    }

                case "title":
                case "desc":
                case "metadata":
                    // Descriptions, not content: never drawn, never reported
                    break;

                default:
                    // textPath, tref and any other element: its characters are not drawn
                    if (content.Collector is { } collector && HasVisibleCharacters(child))
                    {
                        var reason = child.Name.LocalName == "textPath" ? "textPath" : $"<{child.Name.LocalName}> inside text";
                        collector.Add(SvgUnsupportedFeature.TextNotDrawnCode, reason, content.Text);
                    }

                    break;
            }
        }

        if (positioned != null)
        {
            positioned.End = content.Characters.Count;
        }
    }

    /// <summary>
    ///     Appends the characters of one text node, applying the white-space rules (see this file's header).
    /// </summary>
    /// <param name="content">The content being collected.</param>
    /// <param name="value">The text node's value.</param>
    /// <param name="spanIndex">The index of the node's element.</param>
    /// <param name="preserve"><see langword="true"/> under <c>xml:space="preserve"</c>.</param>
    private static void AppendTextCharacters(TextContent content, string value, int spanIndex, bool preserve)
    {
        var characters = content.Characters;
        foreach (var raw in value)
        {
            var c = raw is '\t' or '\n' or '\r' ? ' ' : raw;
            var collapsible = c == ' ' && !preserve;

            // A collapsible space is dropped at the start of the text and after another space
            if (collapsible && (characters.Count == 0 || characters[^1].Value == ' '))
            {
                continue;
            }

            characters.Add(new TextCharacter(c, spanIndex, collapsible));
        }
    }

    /// <summary>
    ///     Removes the collapsible spaces at the end of the text and clamps the element ranges to what is left.
    /// </summary>
    /// <param name="content">The collected content.</param>
    private static void TrimTrailingSpaces(TextContent content)
    {
        var characters = content.Characters;
        var count = characters.Count;
        while (count > 0 && characters[count - 1].Collapsible)
        {
            count--;
        }

        if (count == characters.Count)
        {
            return;
        }

        characters.RemoveRange(count, characters.Count - count);
        foreach (var positioned in content.Positioned)
        {
            positioned.End = Math.Min(positioned.End, count);
        }
    }

    /// <summary>
    ///     Reads an element's own <c>xml:space</c>.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <returns><see langword="true"/> for <c>preserve</c>, <see langword="false"/> for <c>default</c>, <see langword="null"/> when absent or another value.</returns>
    private static bool? ReadXmlSpace(XElement element) => ((string?)element.Attribute(XNamespace.Xml + "space"))?.Trim() switch
    {
        "preserve" => true,
        "default" => false,
        _ => null
    };

    /// <summary>
    ///     Determines whether a <c>text</c> element is under <c>xml:space="preserve"</c>, which it inherits from the nearest
    ///     element of the document that sets it.
    /// </summary>
    /// <param name="text">The <c>text</c> element.</param>
    /// <returns><see langword="true"/> when spaces are preserved.</returns>
    private static bool IsXmlSpacePreserved(XElement text) =>
        text.AncestorsAndSelf().Select(ReadXmlSpace).FirstOrDefault(value => value.HasValue) ?? false;

    /// <summary>
    ///     Determines whether an element holds any character that is not white space.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <returns><see langword="true"/> when some descendant text node has a visible character.</returns>
    private static bool HasVisibleCharacters(XElement element) =>
        element.DescendantNodes().OfType<XText>().Any(node => !string.IsNullOrWhiteSpace(node.Value));

    /// <summary>
    ///     Resolves an element's own <c>baseline-shift</c> as Chrome and Edge do: <c>baseline</c> is 0, a length moves the
    ///     element's characters up, a percentage is of the element's font size, and <c>super</c>/<c>sub</c> move them up or
    ///     down by half the sum of the font's ascent and descent.
    /// </summary>
    /// <param name="element">The <c>text</c>, <c>tspan</c> or <c>a</c> element.</param>
    /// <param name="state">The element's cascaded state.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="collector">The unsupported-feature collector, or <see langword="null"/>.</param>
    /// <param name="text">The <c>text</c> element a report is counted against.</param>
    /// <returns>The shift in user units, positive up; 0 when absent or not supported (reported).</returns>
    /// <remarks>
    ///     The property is not inherited; the layout accumulates it through the nesting instead, so a nested element is
    ///     shifted with its parent.
    /// </remarks>
    private static float ResolveBaselineShift(
        XElement element, RenderState state, RenderContext context, UnsupportedFeatureCollector? collector, XElement text)
    {
        var raw = ResolveElementProperty(element, "baseline-shift", context)?.Trim();
        if (string.IsNullOrEmpty(raw) ||
            raw.Equals("baseline", StringComparison.OrdinalIgnoreCase) ||
            ParseCssWideKeyword(raw) != CssWideKeyword.None)
        {
            return 0f;
        }

        var super = raw.Equals("super", StringComparison.OrdinalIgnoreCase);
        if (super || raw.Equals("sub", StringComparison.OrdinalIgnoreCase))
        {
            if (!(state.FontSize > 0f) || !float.IsFinite(state.FontSize))
            {
                return 0f;
            }

            var font = ResolveFont(context, state.FontFamily, state.FontWeight, state.FontStyle);
            var metrics = Rendering.TextRenderer.MeasureText(string.Empty, font, state.FontSize);
            var half = (metrics.Ascent + metrics.Descent) / 2f;
            return super ? half : -half;
        }

        if (TryResolveLength(raw, state, PercentageAxis.FontSize, out var value, out _))
        {
            return value;
        }

        collector?.Add(SvgUnsupportedFeature.TextUnsupportedCode, "baseline-shift", text);
        return 0f;
    }

    /// <summary>
    ///     Resolves the <c>x</c>, <c>y</c>, <c>dx</c> and <c>dy</c> lists of the text and its <c>tspan</c> elements to one
    ///     entry per character.
    /// </summary>
    /// <param name="content">The collected content.</param>
    /// <returns>The per-character positions.</returns>
    /// <exception cref="InvalidDataException">Thrown when an <c>x</c> or <c>y</c> entry is not a valid length.</exception>
    /// <remarks>
    ///     Elements are visited in document order, so a <c>tspan</c>'s entries override those of its ancestors. Entry k of
    ///     an element's list belongs to the k-th character of its content; entries beyond its characters are not read.
    ///     <c>x</c>/<c>y</c> are geometry and strict (class S); a malformed <c>dx</c>/<c>dy</c> entry ends that list
    ///     (class F), as the first value always did.
    /// </remarks>
    private static CharacterPositions ResolveCharacterPositions(TextContent content)
    {
        var count = content.Characters.Count;
        var positions = new CharacterPositions(new float?[count], new float?[count], new float?[count], new float?[count]);
        foreach (var element in content.Positioned)
        {
            var length = element.End - element.Start;
            if (length <= 0)
            {
                continue;
            }

            ReadPositionList(element, "x", PercentageAxis.Horizontal, positions.X, length, strict: true);
            ReadPositionList(element, "y", PercentageAxis.Vertical, positions.Y, length, strict: true);
            ReadPositionList(element, "dx", PercentageAxis.Horizontal, positions.Dx, length, strict: false);
            ReadPositionList(element, "dy", PercentageAxis.Vertical, positions.Dy, length, strict: false);
        }

        return positions;
    }

    /// <summary>
    ///     Reads one position list of an element into the per-character array.
    /// </summary>
    /// <param name="element">The element and its character range.</param>
    /// <param name="name">The attribute (<c>x</c>, <c>y</c>, <c>dx</c> or <c>dy</c>).</param>
    /// <param name="axis">The percentage basis of the attribute.</param>
    /// <param name="values">The per-character array to write.</param>
    /// <param name="length">The number of characters of the element; at most this many entries are read.</param>
    /// <param name="strict"><see langword="true"/> to throw for a malformed entry; <see langword="false"/> to stop at it.</param>
    /// <exception cref="InvalidDataException">Thrown, when <paramref name="strict"/>, for an entry that is not a valid length.</exception>
    private static void ReadPositionList(
        PositionedElement element, string name, PercentageAxis axis, float?[] values, int length, bool strict)
    {
        var raw = (string?)element.Element.Attribute(name);
        if (raw == null)
        {
            return;
        }

        var position = 0;
        for (var k = 0; k < length; k++)
        {
            if (!TryReadLengthListEntry(raw, ref position, out var entry))
            {
                // The end of the list, or an entry the list scanner cannot read
                if (strict && position < raw.Length)
                {
                    ParseGeometryCoordinate(raw[position..], name, element.State, axis);
                    throw new InvalidDataException($"The '{name}' value '{raw}' is not a valid list of lengths.");
                }

                return;
            }

            if (strict)
            {
                values[element.Start + k] = ParseGeometryCoordinate(entry, name, element.State, axis);
            }
            else if (TryResolveLength(entry, element.State, axis, out var value, out _))
            {
                values[element.Start + k] = value;
            }
            else
            {
                return;
            }
        }
    }

    /// <summary>
    ///     Places the runs of a text and anchors each text chunk.
    /// </summary>
    /// <param name="content">The collected content; at least one character.</param>
    /// <param name="positions">The per-character positions.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <returns>The runs that have glyph outlines.</returns>
    private static List<TextRun> PlaceTextRuns(TextContent content, CharacterPositions positions, RenderContext context)
    {
        var characters = content.Characters;
        var buffer = new char[characters.Count];
        for (var i = 0; i < buffer.Length; i++)
        {
            buffer[i] = characters[i].Value;
        }

        var runs = new List<TextRun>();
        var chunk = new List<PendingRun>();
        var pen = Vector2.Zero;
        var chunkStart = 0f;
        var index = 0;
        while (index < buffer.Length)
        {
            // An absolute position starts a new text chunk (the first character always does); the shift follows
            if (index == 0 || positions.X[index].HasValue || positions.Y[index].HasValue)
            {
                AnchorTextChunk(chunk, chunkStart, pen.X, runs);
                pen = new Vector2(positions.X[index] ?? pen.X, positions.Y[index] ?? pen.Y);
                pen = new Vector2(pen.X + (positions.Dx[index] ?? 0f), pen.Y + (positions.Dy[index] ?? 0f));
                chunkStart = pen.X;
            }
            else
            {
                pen = new Vector2(pen.X + (positions.Dx[index] ?? 0f), pen.Y + (positions.Dy[index] ?? 0f));
            }

            // The run extends over the following characters of the same element that have no position of their own;
            // the low half of a surrogate pair always stays with its high half
            var spanIndex = characters[index].Span;
            var end = index + 1;
            while (end < buffer.Length &&
                   ((char.IsLowSurrogate(buffer[end]) && char.IsHighSurrogate(buffer[end - 1])) ||
                    (characters[end].Span == spanIndex && !positions.HasAny(end))))
            {
                end++;
            }

            var span = content.Spans[spanIndex];
            var runText = new string(buffer, index, end - index);
            var font = ResolveFont(context, span.State.FontFamily, span.State.FontWeight, span.State.FontStyle);
            var size = span.State.FontSize;
            if (size > 0f && float.IsFinite(size))
            {
                // Spacing resolves with the run's own font size (a percentage is relative to it); without spacing the
                // run advances by exactly the whole-run measure a plain text always used
                var letterSpacing = span.State.LetterSpacing.Resolve(size);
                var wordSpacing = span.State.WordSpacing.Resolve(size);
                var run = new PendingRun(runText, span, font, pen, letterSpacing, wordSpacing);
                chunk.Add(run);
                var advance = run.IsSpaced
                    ? LayOutSpacedRun(runText, font, size, letterSpacing, wordSpacing, null, Vector2.Zero)
                    : Rendering.TextRenderer.MeasureText(runText, font, size).Width;
                pen = new Vector2(pen.X + advance, pen.Y);
                ReportMissingGlyphs(content, font, runText);
            }
            else
            {
                chunk.Add(new PendingRun(runText, span, font, pen, 0f, 0f));
                if (size != 0f && content.Collector is { } collector && !string.IsNullOrWhiteSpace(runText))
                {
                    collector.Add(SvgUnsupportedFeature.TextNotDrawnCode, "negative font-size", content.Text);
                }
            }

            index = end;
        }

        AnchorTextChunk(chunk, chunkStart, pen.X, runs);
        return runs;
    }

    /// <summary>
    ///     Lays out a run with <c>letter-spacing</c> or <c>word-spacing</c> grapheme cluster by grapheme cluster, and
    ///     optionally outlines it.
    /// </summary>
    /// <param name="text">The run's characters; not empty.</param>
    /// <param name="font">The run's resolved font.</param>
    /// <param name="size">The run's font size; positive and finite.</param>
    /// <param name="letterSpacing">The spacing added after every grapheme cluster, in user units; may be negative.</param>
    /// <param name="wordSpacing">The spacing added after every word separator, in user units; may be negative.</param>
    /// <param name="builder">The builder to append the glyph outlines to, or <see langword="null"/> to only measure.</param>
    /// <param name="origin">The pen position of the run's first character, used when outlining.</param>
    /// <returns>The run's advance, including the spacing after its last cluster.</returns>
    /// <remarks>
    ///     CSS Text 3 adds the letter spacing after every typographic character unit - a grapheme cluster, so a base and
    ///     its combining marks, a surrogate pair and an emoji sequence each take it once - the last one included, and the
    ///     word spacing after every word separator character (space, no-break space and the few script-specific word
    ///     separators); a space takes both. Edge does exactly this. The font's kerning is kept: the advance of a cluster is
    ///     the measure of the cluster and the next one together minus the measure of the next one alone, which is its
    ///     kerned advance as <c>Rendering.TextRenderer</c> places it (glyph fallback included); the last cluster advances
    ///     by its own measure. The renderer has no ligature shaping, so there are no ligatures to disable. The work is
    ///     linear: two measures per cluster, plus one outline per cluster when drawing.
    /// </remarks>
    private static float LayOutSpacedRun(
        string text, TrueTypeFont font, float size, float letterSpacing, float wordSpacing, PathBuilder? builder, Vector2 origin)
    {
        var pen = 0f;
        var start = 0;
        var length = StringInfo.GetNextTextElementLength(text.AsSpan());
        float? measured = null;
        while (length > 0)
        {
            // The kerned advance: the cluster with its successor, less the successor alone (kept for the next cluster);
            // the last cluster advances by its own measure
            var cluster = text.Substring(start, length);
            var nextStart = start + length;
            var nextLength = StringInfo.GetNextTextElementLength(text.AsSpan(nextStart));
            float advance;
            if (nextLength > 0)
            {
                var nextWidth = Rendering.TextRenderer.MeasureText(text.Substring(nextStart, nextLength), font, size).Width;
                advance = Rendering.TextRenderer.MeasureText(text.Substring(start, length + nextLength), font, size).Width - nextWidth;
                measured = nextWidth;
            }
            else
            {
                advance = measured ?? Rendering.TextRenderer.MeasureText(cluster, font, size).Width;
            }

            builder?.AppendPath(Rendering.TextRenderer.CreateTextPath(cluster, font, size, origin.X + pen, origin.Y));

            // The spacing follows every cluster, the last one too; a word separator also takes the word spacing
            pen += advance + letterSpacing;
            if (IsWordSeparator(cluster))
            {
                pen += wordSpacing;
            }

            start = nextStart;
            length = nextLength;
        }

        return pen;
    }

    /// <summary>
    ///     Determines whether a grapheme cluster starts with a CSS word separator character, the characters
    ///     <c>word-spacing</c> applies to.
    /// </summary>
    /// <param name="cluster">The cluster; not empty.</param>
    /// <returns>
    ///     <see langword="true"/> for U+0020 space, U+00A0 no-break space, U+1361 Ethiopic word space, U+10100/U+10101
    ///     Aegean word separators, U+1039F Ugaritic word divider and U+1091F Phoenician word separator (CSS Text 3);
    ///     other spaces such as U+2002 en space are not word separators, as in Edge.
    /// </returns>
    private static bool IsWordSeparator(string cluster)
    {
        Rune.DecodeFromUtf16(cluster, out var rune, out _);
        return rune.Value is 0x20 or 0xA0 or 0x1361 or 0x10100 or 0x10101 or 0x1039F or 0x1091F;
    }

    /// <summary>
    ///     Reports the characters of a run that no font has a glyph for (they draw as the missing-glyph box).
    /// </summary>
    /// <param name="content">The collected content, with its collector.</param>
    /// <param name="font">The run's font.</param>
    /// <param name="runText">The run's characters.</param>
    private static void ReportMissingGlyphs(TextContent content, TrueTypeFont font, string runText)
    {
        if (content.Collector is not { } collector)
        {
            return;
        }

        foreach (var codePoint in GlyphFallback.FindMissing(font, runText))
        {
            collector.Add(
                SvgUnsupportedFeature.TextUnsupportedCode,
                $"no font has a glyph for U+{codePoint.ToString("X4", CultureInfo.InvariantCulture)}",
                content.Text);
        }
    }

    /// <summary>
    ///     Anchors the runs of one text chunk by the <c>text-anchor</c> of its first character and outlines them.
    /// </summary>
    /// <param name="chunk">The chunk's runs; cleared on return.</param>
    /// <param name="chunkStart">The pen x of the chunk's first character.</param>
    /// <param name="chunkEnd">The pen x after the chunk's last character.</param>
    /// <param name="runs">The laid-out runs to append to.</param>
    /// <remarks>
    ///     A chunk of one run without spacing is outlined by <see cref="BuildGlyphRunPath"/> with the anchor as its
    ///     alignment: the very arithmetic a plain text always used, so such text draws exactly as before. Any other chunk
    ///     is shifted by its whole advance (including <c>dx</c> shifts inside it and the spacing after its last character,
    ///     as Edge anchors it) and each run is outlined from its own pen position, a spaced run cluster by cluster
    ///     (<see cref="LayOutSpacedRun"/>).
    /// </remarks>
    private static void AnchorTextChunk(List<PendingRun> chunk, float chunkStart, float chunkEnd, List<TextRun> runs)
    {
        if (chunk.Count == 0)
        {
            return;
        }

        var anchor = chunk[0].Span.State.TextAnchor;
        var width = chunkEnd - chunkStart;
        var plain = chunk.Count == 1 && !chunk[0].IsSpaced;
        var offset = plain
            ? 0f
            : anchor switch
            {
                TextAnchor.Middle => -width / 2f,
                TextAnchor.End => -width,
                _ => 0f
            };
        var align = plain ? anchor : TextAnchor.Start;

        foreach (var run in chunk)
        {
            var origin = new Vector2(run.Pen.X + offset, run.Pen.Y - run.Span.BaselineShift);
            Path path;
            if (run.IsSpaced)
            {
                var builder = new PathBuilder();
                LayOutSpacedRun(run.Text, run.Font, run.Span.State.FontSize, run.LetterSpacing, run.WordSpacing, builder, origin);
                path = builder.Build();
            }
            else
            {
                path = BuildGlyphRunPath(run.Text, run.Font, run.Span.State.FontSize, align, origin);
            }

            if (path.Subpaths.Count > 0)
            {
                runs.Add(new TextRun(path, run.Span.State, run.Span.Element));
            }
        }

        chunk.Clear();
    }

    /// <summary>
    ///     Records the text features a text uses that the layout ignores, as <see cref="SvgUnsupportedFeature.TextUnsupportedCode"/>.
    /// </summary>
    /// <param name="content">The collected content (its elements).</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="collector">The collector.</param>
    /// <remarks>
    ///     Each text content element is checked for every feature; the ancestors of the <c>text</c> are checked for the
    ///     inherited ones (writing mode, direction, dominant baseline, decoration), so a style set on a group is found too.
    ///     Letter and word spacing are laid out, so they are not reported.
    /// </remarks>
    private static void CollectUnsupportedTextFeatures(TextContent content, RenderContext context, UnsupportedFeatureCollector collector)
    {
        foreach (var element in content.Spans.Select(span => span.Element))
        {
            if (((string?)element.Attribute("rotate")) is { } rotate && HasNonZeroNumber(rotate))
            {
                collector.Add(SvgUnsupportedFeature.TextUnsupportedCode, "rotate", content.Text);
            }

            if (element.Attribute("textLength") != null)
            {
                collector.Add(SvgUnsupportedFeature.TextUnsupportedCode, "textLength", content.Text);
            }

            if (IsUnsupportedTextValue(ResolveElementProperty(element, "alignment-baseline", context), "auto", "baseline", "alphabetic"))
            {
                collector.Add(SvgUnsupportedFeature.TextUnsupportedCode, "alignment-baseline", content.Text);
            }

            CollectUnsupportedInheritedTextFeatures(element, content.Text, context, collector);
        }

        foreach (var ancestor in content.Text.Ancestors())
        {
            CollectUnsupportedInheritedTextFeatures(ancestor, content.Text, context, collector);
        }
    }

    /// <summary>
    ///     Records the inherited text features one element sets that the layout ignores.
    /// </summary>
    /// <param name="element">The element (a text content element or an ancestor of the <c>text</c>).</param>
    /// <param name="text">The <c>text</c> element a report is counted against.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="collector">The collector.</param>
    private static void CollectUnsupportedInheritedTextFeatures(
        XElement element, XElement text, RenderContext context, UnsupportedFeatureCollector collector)
    {
        var writingMode = ResolveElementProperty(element, "writing-mode", context)?.Trim();
        if (writingMode != null &&
            (writingMode.StartsWith("tb", StringComparison.OrdinalIgnoreCase) ||
             writingMode.StartsWith("vertical", StringComparison.OrdinalIgnoreCase) ||
             writingMode.StartsWith("sideways", StringComparison.OrdinalIgnoreCase)))
        {
            collector.Add(SvgUnsupportedFeature.TextUnsupportedCode, "vertical writing-mode", text);
        }

        if (string.Equals(ResolveElementProperty(element, "direction", context)?.Trim(), "rtl", StringComparison.OrdinalIgnoreCase))
        {
            collector.Add(SvgUnsupportedFeature.TextUnsupportedCode, "direction rtl", text);
        }

        if (IsUnsupportedTextValue(ResolveElementProperty(element, "dominant-baseline", context), "auto", "alphabetic"))
        {
            collector.Add(SvgUnsupportedFeature.TextUnsupportedCode, "dominant-baseline", text);
        }

        if (IsUnsupportedTextValue(ResolveElementProperty(element, "text-decoration", context), "none"))
        {
            collector.Add(SvgUnsupportedFeature.TextUnsupportedCode, "text-decoration", text);
        }
    }

    /// <summary>
    ///     Determines whether a text property value asks for something other than its default.
    /// </summary>
    /// <param name="value">The resolved value, or <see langword="null"/> when absent.</param>
    /// <param name="defaults">The keywords that mean the default (matched ASCII case-insensitively).</param>
    /// <returns>
    ///     <see langword="false"/> when absent, blank, a CSS-wide keyword, or one of <paramref name="defaults"/>;
    ///     otherwise <see langword="true"/>.
    /// </returns>
    private static bool IsUnsupportedTextValue(string? value, params ReadOnlySpan<string> defaults)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed) || ParseCssWideKeyword(trimmed) != CssWideKeyword.None)
        {
            return false;
        }

        foreach (var keyword in defaults)
        {
            if (trimmed.Equals(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Determines whether a <c>rotate</c> list turns any character: an entry that is not 0 (or not a number).
    /// </summary>
    /// <param name="list">The list text.</param>
    /// <returns><see langword="true"/> when some entry is not 0.</returns>
    private static bool HasNonZeroNumber(string list) =>
        list.Split([' ', ',', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Any(entry => !float.TryParse(entry, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || value != 0f);

    /// <summary>
    ///     Resolves the cascaded <c>letter-spacing</c> or <c>word-spacing</c> of an element.
    /// </summary>
    /// <param name="css">The value from the CSS cascade (inline style or stylesheet), or <see langword="null"/>.</param>
    /// <param name="attribute">The presentation attribute's value, or <see langword="null"/>.</param>
    /// <param name="fontState">The element's state after its own font cascade, the basis of <c>em</c> and other units.</param>
    /// <param name="parent">The parent's computed value, inherited when no source is valid.</param>
    /// <returns>The element's computed spacing (see <see cref="TextSpacing"/>).</returns>
    /// <remarks>
    ///     The first valid source wins, CSS before the attribute; an invalid value is ignored, so the next source or the
    ///     inherited value applies, as in Edge. <c>inherit</c>, <c>unset</c> and <c>revert</c> take the parent's value,
    ///     <c>initial</c> and <c>normal</c> are zero, a plain percentage is kept relative to the font size of each
    ///     character, and any other length (a unitless number too, as browsers accept it) is resolved here. Negative
    ///     values are valid.
    /// </remarks>
    private static TextSpacing ResolveTextSpacing(string? css, string? attribute, RenderState fontState, TextSpacing parent)
    {
        foreach (var raw in (ReadOnlySpan<string?>)[css, attribute])
        {
            if (TryParseTextSpacing(raw, fontState, parent, out var spacing))
            {
                return spacing;
            }
        }

        return parent;
    }

    /// <summary>
    ///     Parses one source's <c>letter-spacing</c> or <c>word-spacing</c> value (see <see cref="ResolveTextSpacing"/>).
    /// </summary>
    /// <param name="raw">The raw value, or <see langword="null"/> when the source does not set it.</param>
    /// <param name="fontState">The element's state after its own font cascade.</param>
    /// <param name="parent">The parent's computed value, for the CSS-wide keywords that inherit.</param>
    /// <param name="spacing">The computed value when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the value is valid.</returns>
    private static bool TryParseTextSpacing(string? raw, RenderState fontState, TextSpacing parent, out TextSpacing spacing)
    {
        spacing = default;
        var text = raw?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        switch (ParseCssWideKeyword(text))
        {
            case CssWideKeyword.Inherit:
                spacing = parent;
                return true;
            case CssWideKeyword.Initial:
                return true;
        }

        if (text.Equals("normal", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // A plain percentage stays relative: resolved against a font size of 1 it is the fraction itself
        if (text.EndsWith('%') && !text.Contains('('))
        {
            if (!TryResolveLength(text, fontState with { FontSize = 1f }, PercentageAxis.FontSize, out var fraction, out _))
            {
                return false;
            }

            spacing = new TextSpacing(0f, fraction);
            return true;
        }

        if (!TryResolveLength(text, fontState, PercentageAxis.FontSize, out var length, out _))
        {
            return false;
        }

        spacing = new TextSpacing(length, 0f);
        return true;
    }
}
