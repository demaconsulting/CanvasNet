// Portions derived from mermaid (MIT); see third-party/README.md
using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;
using DemaConsulting.CanvasNet.Mermaid.Syntax;
using DemaConsulting.CanvasNet.Rendering;

namespace DemaConsulting.CanvasNet.Mermaid.Rendering;

/// <content>
///     HTML labels: the HTML of <c>createText</c> and <c>addHtmlSpan</c>, read into styled inline items and laid out by
///     the rules of Mermaid's label <c>&lt;div&gt;</c> (<c>display: table-cell</c>, <c>white-space: nowrap</c>,
///     <c>line-height: 1.5</c>, <c>max-width</c> the wrapping width, <c>text-align: center</c>, then the
///     <c>break-spaces</c> retry).
/// </content>
internal sealed partial class LabelBuilder
{
    /// <summary>The number of layout units per pixel of the browser's layout (Chromium's <c>LayoutUnit</c>).</summary>
    private const double LayoutUnits = 64;

    /// <summary>The elements of a label that are understood (the rest is dropped with its text kept).</summary>
    private static readonly HashSet<string> KnownElements = new(StringComparer.Ordinal) { "b", "strong", "i", "em", "br", "img", "span", "p" };

    /// <summary>
    ///     The kind of an inline item of an HTML label.
    /// </summary>
    private enum HtmlItemKind
    {
        /// <summary>Text with one style.</summary>
        Text,

        /// <summary>A line break (<c>&lt;br&gt;</c>).</summary>
        Break,

        /// <summary>The start or end of a paragraph.</summary>
        Block,

        /// <summary>A picture (<c>&lt;img&gt;</c>), which Mermaid lays out as a block.</summary>
        Image,

    }

    /// <summary>
    ///     Lays out an HTML label.
    /// </summary>
    /// <param name="text">The prepared text.</param>
    /// <param name="request">The request.</param>
    /// <returns>The measured label.</returns>
    private LabelLayout BuildHtml(string text, LabelRequest request)
    {
        // createText: markdown or plain HTML, entity placeholders decoded, icons replaced; addHtmlSpan sanitizes again
        var html = request.Markdown ? MarkdownToHtml(text, request.MarkdownAutoWrap) : NonMarkdownToHtml(text);
        html = HtmlSanitizer.Sanitize(ReplaceIcons(DecodeEntities(html)));
        var items = ParseHtml(html, request, out var hasParagraph);
        if (!items.Exists(item => item.Kind is HtmlItemKind.Text or HtmlItemKind.Image))
        {
            return LabelLayout.Of(true, new SceneRect(0, 0, 0, 0));
        }

        var padding = hasParagraph ? request.ParagraphPadding : 0;
        var rows = SplitRows(items);
        var hasText = items.Exists(item => item.Kind == HtmlItemKind.Text && item.Text.Any(c => !char.IsWhiteSpace(c)));

        // First the label as one line per row, white space collapsed; at the wrapping width, again with wrapping
        var lines = rows.Select(row => CollapseRow(row)).ToList();
        var natural = lines.Count == 0 ? 0 : lines.Max(line => line.Image is null ? line.Width : IntrinsicImageWidth(line.Image, hasText, request));
        double contentWidth;
        if (double.IsFinite(request.Width) && natural + (2 * padding) >= request.Width)
        {
            var available = Math.Max(request.Width - (2 * padding), 0);
            var pieces = rows.Select(row => BreakSpacesPieces(row)).ToList();
            var minContent = pieces.SelectMany(row => Groups(row)).DefaultIfEmpty(0).Max();
            contentWidth = Math.Max(available, minContent);
            lines = [.. rows.SelectMany((row, index) => WrapRow(row, pieces[index], contentWidth))];
        }
        else
        {
            contentWidth = natural;
        }

        return Place(lines, contentWidth, padding, hasText, request);
    }

    /// <summary>
    ///     Replaces <c>fa:fa-...</c> icon names by an empty icon element, as Mermaid's <c>replaceIconSubstring</c> does
    ///     for an icon that is not registered (nothing is drawn for it).
    /// </summary>
    /// <param name="html">The HTML.</param>
    /// <returns>The HTML.</returns>
    internal static string ReplaceIcons(string html)
    {
        var builder = new StringBuilder(html.Length);
        var i = 0;
        while (i < html.Length)
        {
            if (MatchIcon(html, i) is > 0 and var length)
            {
                builder.Append("<i class=\"fa\"></i>");
                i += length;
                continue;
            }

            builder.Append(html[i]);
            i++;
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Removes <c>fa:fa-...</c> icon names from a text (for an SVG label, which draws text only).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text.</returns>
    internal static string RemoveIcons(string text) => ReplaceIcons(text).Replace("<i class=\"fa\"></i>", string.Empty, StringComparison.Ordinal);

    /// <summary>
    ///     Matches an icon name <c>(fa|fab|fak|fal|far|fas):fa-[\w-]+</c> at a position.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="i">The position.</param>
    /// <returns>The length, or zero.</returns>
    private static int MatchIcon(string text, int i)
    {
        if (i + 2 >= text.Length || text[i] != 'f' || text[i + 1] != 'a')
        {
            return 0;
        }

        var j = i + 2;
        if (text[j] is 'b' or 'k' or 'l' or 'r' or 's')
        {
            j++;
        }

        if (string.CompareOrdinal(text, j, ":fa-", 0, 4) != 0)
        {
            return 0;
        }

        var end = j + 4;
        while (end < text.Length && (ScanPatterns.IsJsWordChar(text[end]) || text[end] == '-'))
        {
            end++;
        }

        return end > j + 4 ? end - i : 0;
    }

    /// <summary>
    ///     Reads the sanitized HTML of a label into inline items with their styles.
    /// </summary>
    /// <param name="html">The HTML.</param>
    /// <param name="request">The request (base style, location).</param>
    /// <param name="hasParagraph">Receives whether the HTML has a paragraph.</param>
    /// <returns>The items.</returns>
    private List<HtmlItem> ParseHtml(string html, LabelRequest request, out bool hasParagraph)
    {
        hasParagraph = false;
        var items = new List<HtmlItem>();
        var styles = new Stack<(string Name, HtmlStyle Style)>();
        var baseFont = request.Font;
        var current = new HtmlStyle(new SceneFont(baseFont.Family, baseFont.Size, baseFont.Bold, baseFont.Italic), baseFont.Color);
        var ignored = new SortedSet<string>(StringComparer.Ordinal);
        var i = 0;
        while (i < html.Length)
        {
            if (html[i] != '<')
            {
                var next = html.IndexOf('<', i);
                var end = next < 0 ? html.Length : next;
                var content = HtmlSanitizer.DecodeReferences(html[i..end]);
                items.Add(new HtmlItem(HtmlItemKind.Text, content, current));
                i = end;
                continue;
            }

            var close = html.IndexOf('>', i);
            if (close < 0)
            {
                items.Add(new HtmlItem(HtmlItemKind.Text, HtmlSanitizer.DecodeReferences(html[i..]), current));
                break;
            }

            var tag = ParseTag(html[(i + 1)..close]);
            i = close + 1;
            if (tag.Name.Length == 0)
            {
                continue;
            }

            if (!KnownElements.Contains(tag.Name))
            {
                ignored.Add(tag.Name);
            }

            if (tag.IsEnd)
            {
                // Close the innermost element of the name (the sanitizer leaves the tags balanced)
                if (styles.Count > 0 && styles.Peek().Name == tag.Name)
                {
                    current = styles.Pop().Style;
                }

                if (tag.Name == "p")
                {
                    items.Add(new HtmlItem(HtmlItemKind.Block, string.Empty, current));
                }

                continue;
            }

            switch (tag.Name)
            {
                case "br":
                    items.Add(new HtmlItem(HtmlItemKind.Break, string.Empty, current));
                    continue;
                case "img":
                    items.Add(new HtmlItem(HtmlItemKind.Image, string.Empty, current)
                    {
                        Href = tag.Attribute("src") ?? string.Empty,
                        AttributeWidth = ParseHtmlLength(tag.Attribute("width")),
                        AttributeHeight = ParseHtmlLength(tag.Attribute("height")),
                    });
                    continue;
                case "p":
                    hasParagraph = true;
                    items.Add(new HtmlItem(HtmlItemKind.Block, string.Empty, current));
                    break;
            }

            if (tag.IsSelfClosing || IsVoidElement(tag.Name))
            {
                continue;
            }

            styles.Push((tag.Name, current));
            current = tag.Name switch
            {
                "b" or "strong" => current with { Font = current.Font with { Bold = true } },
                "i" or "em" => current with { Font = current.Font with { Italic = true } },
                "span" when tag.Attribute("style") is { } style => ApplyStyle(current, style, request.Location),
                _ => current,
            };
        }

        foreach (var name in ignored)
        {
            _sink?.Warn(
                MermaidDiagnosticCodes.HtmlTagIgnored,
                $"the HTML element <{name}> of a label is not supported; its text is drawn without it.",
                request.Location,
                "use <b>, <strong>, <i>, <em>, <br>, <img> or <span style=\"...\"> in labels");
        }

        return items;
    }

    /// <summary>
    ///     Applies the style attribute of a <c>&lt;span&gt;</c> (the supported declarations) to a style.
    /// </summary>
    /// <param name="style">The style of the parent.</param>
    /// <param name="text">The attribute value.</param>
    /// <param name="location">The location for reports.</param>
    /// <returns>The style of the span.</returns>
    private HtmlStyle ApplyStyle(HtmlStyle style, string text, MermaidLocation location)
    {
        var set = StyleDeclarations.ParseBlock(text, restrict: true, location, _sink);
        var font = style.Font;
        if (set["font-family"] is { } family)
        {
            font = font with { Family = family };
        }

        if (set["font-size"] is { } size && StyleDeclarations.TryParseLength(size, font.Size, out var length) && length > 0)
        {
            font = font with { Size = length };
        }

        if (set["font-weight"] is { } weight && StyleDeclarations.TryParseBold(weight, out var bold))
        {
            font = font with { Bold = bold };
        }

        if (set["font-style"] is { } fontStyle && StyleDeclarations.TryParseItalic(fontStyle, out var italic))
        {
            font = font with { Italic = italic };
        }

        var color = set.Paint("color") ?? style.Color;
        if (set["opacity"] is { } opacity && StyleDeclarations.TryParseOpacity(opacity, out var factor))
        {
            color = color with { A = (byte)Math.Round(color.A * factor, MidpointRounding.AwayFromZero) };
        }

        return new HtmlStyle(font, color);
    }

    /// <summary>
    ///     Splits the items into rows: at line breaks and paragraph boundaries; a picture is a row of its own. A row with
    ///     no text is dropped, except an empty line between two line breaks.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <returns>The rows.</returns>
    private static List<List<HtmlItem>> SplitRows(List<HtmlItem> items)
    {
        var rows = new List<List<HtmlItem>>();
        var row = new List<HtmlItem>();
        var startedByBreak = false;
        void End(bool byBreak)
        {
            var hasContent = row.Exists(item => item.Kind == HtmlItemKind.Text && item.Text.Any(c => !IsCollapsible(c)));
            if (hasContent || (startedByBreak && byBreak))
            {
                rows.Add(row);
            }

            row = [];
            startedByBreak = byBreak;
        }

        foreach (var item in items)
        {
            switch (item.Kind)
            {
                case HtmlItemKind.Break:
                    End(true);
                    break;
                case HtmlItemKind.Block:
                    End(false);
                    break;
                case HtmlItemKind.Image:
                    End(false);
                    rows.Add([item]);
                    break;
                default:
                    row.Add(item);
                    break;
            }
        }

        End(false);
        return rows;
    }

    /// <summary>
    ///     Lays a row out on one line with the white space collapsed (<c>white-space: nowrap</c>).
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns>The line.</returns>
    private HtmlLine CollapseRow(List<HtmlItem> row)
    {
        if (row is [{ Kind: HtmlItemKind.Image } image])
        {
            return new HtmlLine([], 0, image);
        }

        // Spaces collapse across items; a space at the start or end of the line goes
        var runs = new List<(string Text, HtmlStyle Style)>();
        var lastWasSpace = true;
        foreach (var item in row)
        {

            var builder = new StringBuilder();
            foreach (var c in item.Text)
            {
                if (IsCollapsible(c))
                {
                    if (!lastWasSpace)
                    {
                        builder.Append(' ');
                    }

                    lastWasSpace = true;
                }
                else
                {
                    builder.Append(c);
                    lastWasSpace = false;
                }
            }

            if (builder.Length > 0)
            {
                runs.Add((builder.ToString(), item.Style));
            }
        }

        while (runs.Count > 0 && runs[^1].Text.EndsWith(' '))
        {
            var trimmed = runs[^1].Text[..^1];
            if (trimmed.Length == 0)
            {
                runs.RemoveAt(runs.Count - 1);
            }
            else
            {
                runs[^1] = (trimmed, runs[^1].Style);
                break;
            }
        }

        var measured = runs.Select(run => new LabelRun(run.Text, run.Style.Font, run.Style.Color, HtmlAdvance(run.Text, run.Style.Font))).ToList();
        return new HtmlLine(measured, measured.Sum(run => run.Width), null);
    }

    /// <summary>
    ///     Cuts a row into the pieces of the <c>break-spaces</c> layout: text with its spaces kept, with a break after
    ///     every run of spaces and after a hyphen between letters.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns>The pieces; a null style marks a break opportunity.</returns>
    private List<WrapPiece> BreakSpacesPieces(List<HtmlItem> row)
    {
        var pieces = new List<WrapPiece>();
        if (row is [{ Kind: HtmlItemKind.Image }])
        {
            return pieces;
        }

        foreach (var item in row.Where(item => item.Kind == HtmlItemKind.Text))
        {
            var text = item.Text.Replace('\n', ' ').Replace('\t', ' ').Replace('\r', ' ').Replace('\f', ' ');
            var start = 0;
            for (var i = 0; i < text.Length; i++)
            {
                var spaceEnd = text[i] == ' ' && (i + 1 == text.Length || text[i + 1] != ' ');
                var hyphen = text[i] == '-' && i > 0 && char.IsLetterOrDigit(text[i - 1]) && i + 1 < text.Length && char.IsLetter(text[i + 1]);
                if (spaceEnd || hyphen)
                {
                    AddPiece(pieces, text[start..(i + 1)], item.Style);
                    pieces.Add(WrapPiece.Opportunity);
                    start = i + 1;
                }
            }

            if (start < text.Length)
            {
                AddPiece(pieces, text[start..], item.Style);
            }
        }

        return pieces;
    }

    /// <summary>
    ///     Adds a measured piece of text.
    /// </summary>
    /// <param name="pieces">The pieces.</param>
    /// <param name="text">The text.</param>
    /// <param name="style">The style.</param>
    private void AddPiece(List<WrapPiece> pieces, string text, HtmlStyle style) =>
        pieces.Add(new WrapPiece(text, style, HtmlAdvance(text, style.Font)));

    /// <summary>
    ///     Gets the advance of a text of an HTML label: the browser lays text out in units of 1/64 pixel, rounding a
    ///     run's width up.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="font">The font.</param>
    /// <returns>The advance.</returns>
    private double HtmlAdvance(string text, SceneFont font) => Math.Ceiling((Advance(text, font) * LayoutUnits) - 1e-6) / LayoutUnits;

    /// <summary>
    ///     Gets the widths of the unbreakable groups of pieces (the min-content contributions).
    /// </summary>
    /// <param name="pieces">The pieces of a row.</param>
    /// <returns>The widths.</returns>
    private static IEnumerable<double> Groups(List<WrapPiece> pieces)
    {
        var width = 0.0;
        foreach (var piece in pieces)
        {
            if (piece.Style is null)
            {
                yield return width;
                width = 0;
            }
            else
            {
                width += piece.Width;
            }
        }

        yield return width;
    }

    /// <summary>
    ///     Wraps a row at a width with the core word wrap: the pieces between break opportunities never split.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <param name="pieces">The pieces of the row.</param>
    /// <param name="width">The width of the content box.</param>
    /// <returns>The lines.</returns>
    private static IEnumerable<HtmlLine> WrapRow(List<HtmlItem> row, List<WrapPiece> pieces, double width)
    {
        if (row is [{ Kind: HtmlItemKind.Image } image])
        {
            yield return new HtmlLine([], 0, image);
            yield break;
        }

        var packed = WordWrap.Pack(
            pieces,
            width,
            piece => piece.Style is null ? WordWrapTokenKind.Whitespace : WordWrapTokenKind.Word,
            (piece, _) => piece.Width);
        foreach (var line in packed)
        {
            var runs = new List<LabelRun>();
            foreach (var piece in line.Where(piece => piece.Style is not null))
            {
                // Pieces of one style merge into one run
                if (runs.Count > 0 && runs[^1].Font == piece.Style!.Font && runs[^1].Color == piece.Style.Color)
                {
                    runs[^1] = runs[^1] with { Text = runs[^1].Text + piece.Text, Width = runs[^1].Width + piece.Width };
                }
                else
                {
                    runs.Add(new LabelRun(piece.Text, piece.Style!.Font, piece.Style.Color, piece.Width));
                }
            }

            yield return new HtmlLine(runs, runs.Sum(run => run.Width), null);
        }
    }

    /// <summary>
    ///     Gets the width a picture contributes to the natural width of the label.
    /// </summary>
    /// <param name="image">The picture.</param>
    /// <param name="hasText">Whether the label has text besides pictures.</param>
    /// <param name="request">The request.</param>
    /// <returns>The width.</returns>
    private double IntrinsicImageWidth(HtmlItem image, bool hasText, LabelRequest request)
    {
        if (!hasText)
        {
            return LoneImageFactor * request.ConfigFontSize;
        }

        return image.AttributeWidth ?? NaturalSize(image, request)?.Width ?? 0;
    }

    /// <summary>
    ///     Gets the natural size of a picture of a label.
    /// </summary>
    /// <param name="image">The picture.</param>
    /// <param name="request">The request.</param>
    /// <returns>The natural size, or null when it is not known.</returns>
    private (double Width, double Height)? NaturalSize(HtmlItem image, LabelRequest request) =>
        _images is null || image.Href is not { Length: > 0 } href ? null : _images.NaturalSize(href, request.Location);

    /// <summary>
    ///     Places the lines in the box of the label: centered lines of 1.5 em, pictures as blocks at the left.
    /// </summary>
    /// <param name="lines">The lines.</param>
    /// <param name="contentWidth">The width of the content box.</param>
    /// <param name="padding">The padding of the paragraph.</param>
    /// <param name="hasText">Whether the label has text besides pictures.</param>
    /// <param name="request">The request.</param>
    /// <returns>The measured label.</returns>
    private LabelLayout Place(List<HtmlLine> lines, double contentWidth, double padding, bool hasText, LabelRequest request)
    {
        var baseFont = new SceneFont(request.Font.Family, request.Font.Size, request.Font.Bold, request.Font.Italic);
        var placed = new List<LabelLine>();
        var images = new List<LabelImage>();
        var top = padding;
        foreach (var line in lines)
        {
            if (line.Image is { } image)
            {
                var natural = NaturalSize(image, request);
                var width = hasText ? contentWidth : LoneImageFactor * request.ConfigFontSize;
                var ratio = natural is { Width: > 0 } size ? size.Height / size.Width : 0;
                var height = image.AttributeHeight ?? width * ratio;
                images.Add(new LabelImage(new SceneRect(padding, top, width, height), image.Href ?? string.Empty));
                top += height;
                continue;
            }

            // The line box holds the strut of the label's font and the runs of the line
            var tallest = line.Runs.Select(run => run.Font).Append(baseFont).MaxBy(font => font.Size)!;
            var lineHeight = HtmlLineHeight * tallest.Size;
            var (ascent, descent) = VerticalMetrics(tallest);
            var baseline = top + Math.Floor((lineHeight - ascent - descent) / 2) + ascent;
            var x = padding + ((contentWidth - line.Width) / 2);
            placed.Add(new LabelLine(x, baseline, line.Width, new SceneRect(x, top, line.Width, lineHeight), line.Runs));
            top += lineHeight;
        }

        ReportMissingGlyphs(placed, request.Location);
        return new LabelLayout(true, new SceneRect(0, 0, contentWidth + (2 * padding), top + padding), placed, images);
    }

    /// <summary>
    ///     Reads a tag (the text between <c>&lt;</c> and <c>&gt;</c>).
    /// </summary>
    /// <param name="body">The text of the tag.</param>
    /// <returns>The tag.</returns>
    private static HtmlTag ParseTag(string body)
    {
        var isEnd = body.StartsWith('/');
        var i = isEnd ? 1 : 0;
        var nameStart = i;
        while (i < body.Length && !char.IsWhiteSpace(body[i]) && body[i] != '/')
        {
            i++;
        }

        var name = body[nameStart..i].ToLowerInvariant();
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        while (i < body.Length)
        {
            while (i < body.Length && (char.IsWhiteSpace(body[i]) || body[i] == '/'))
            {
                i++;
            }

            var attrStart = i;
            while (i < body.Length && !char.IsWhiteSpace(body[i]) && body[i] is not '=' and not '/')
            {
                i++;
            }

            if (i == attrStart)
            {
                break;
            }

            var attrName = body[attrStart..i].ToLowerInvariant();
            var value = string.Empty;
            if (i < body.Length && body[i] == '=')
            {
                i++;
                if (i < body.Length && body[i] is '"' or '\'')
                {
                    var quote = body[i];
                    var end = body.IndexOf(quote, i + 1);
                    end = end < 0 ? body.Length : end;
                    value = body[(i + 1)..end];
                    i = Math.Min(end + 1, body.Length);
                }
                else
                {
                    var valueStart = i;
                    while (i < body.Length && !char.IsWhiteSpace(body[i]))
                    {
                        i++;
                    }

                    value = body[valueStart..i];
                }
            }

            attributes.TryAdd(attrName, HtmlSanitizer.DecodeReferences(value));
        }

        return new HtmlTag(name, isEnd, body.EndsWith('/'), attributes);
    }

    /// <summary>
    ///     Reads an HTML length attribute (a number of pixels).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The length, or null.</returns>
    private static double? ParseHtmlLength(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var text = value.Trim();
        var pixels = text.EndsWith("px", StringComparison.Ordinal) ? text : text + "px";
        return StyleDeclarations.TryParseLength(pixels, 16, out var length) && length >= 0 ? length : null;
    }

    /// <summary>Gets whether an element has no content and no end tag.</summary>
    /// <param name="name">The element name.</param>
    /// <returns>Whether it is a void element.</returns>
    private static bool IsVoidElement(string name) => name is "area" or "br" or "col" or "embed" or "hr" or "img" or "input" or "source" or "track" or "wbr";

    /// <summary>Gets whether a character is collapsible white space for CSS.</summary>
    /// <param name="c">The character.</param>
    /// <returns>Whether it is.</returns>
    private static bool IsCollapsible(char c) => c is ' ' or '\t' or '\n' or '\r' or '\f';

    /// <summary>
    ///     The style of inline HTML content.
    /// </summary>
    /// <param name="Font">The font.</param>
    /// <param name="Color">The color.</param>
    private sealed record HtmlStyle(SceneFont Font, Rgba32 Color);

    /// <summary>
    ///     An inline item of an HTML label.
    /// </summary>
    /// <param name="Kind">The kind.</param>
    /// <param name="Text">The text of a text item.</param>
    /// <param name="Style">The style in effect.</param>
    private sealed record HtmlItem(HtmlItemKind Kind, string Text, HtmlStyle Style)
    {
        /// <summary>Gets the link of a picture.</summary>
        public string? Href { get; init; }

        /// <summary>Gets the <c>width</c> attribute of a picture, or null.</summary>
        public double? AttributeWidth { get; init; }

        /// <summary>Gets the <c>height</c> attribute of a picture, or null.</summary>
        public double? AttributeHeight { get; init; }
    }

    /// <summary>
    ///     A line of an HTML label before it is placed.
    /// </summary>
    /// <param name="Runs">The runs.</param>
    /// <param name="Width">The advance width.</param>
    /// <param name="Image">The picture of a picture row, or null.</param>
    private sealed record HtmlLine(List<LabelRun> Runs, double Width, HtmlItem? Image);

    /// <summary>
    ///     A piece of text of the <c>break-spaces</c> layout, or a break opportunity (no style).
    /// </summary>
    /// <param name="Text">The text.</param>
    /// <param name="Style">The style, or null for a break opportunity.</param>
    /// <param name="Width">The advance width.</param>
    private sealed record WrapPiece(string Text, HtmlStyle? Style, double Width)
    {
        /// <summary>Gets the break opportunity.</summary>
        public static WrapPiece Opportunity { get; } = new(string.Empty, null, 0);
    }

    /// <summary>
    ///     A tag of the label's HTML.
    /// </summary>
    /// <param name="Name">The element name, lower case.</param>
    /// <param name="IsEnd">Whether it is an end tag.</param>
    /// <param name="IsSelfClosing">Whether it ends with <c>/&gt;</c>.</param>
    /// <param name="Attributes">The attributes.</param>
    private sealed record HtmlTag(string Name, bool IsEnd, bool IsSelfClosing, Dictionary<string, string> Attributes)
    {
        /// <summary>Gets an attribute.</summary>
        /// <param name="name">The name.</param>
        /// <returns>The value, or null.</returns>
        public string? Attribute(string name) => Attributes.TryGetValue(name, out var value) ? value : null;
    }
}
