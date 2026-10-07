using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Fonts;

namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore fontscale lnspcreduction pptx

/// <summary>
///     Implements the <see cref="PptxDocument"/> text layout engine (Phase 1d): word-wrap,
///     horizontal alignment, vertical anchor, and the three-tier autofit policy - see the design
///     document's "Text Layout and Rendering (Phase 1d)" section for the full algorithm narrative
///     and its documented simplifications/deferrals (full justification, bullets/numbering,
///     kerning, text clipping on overflow, growing a shape for <c>spAutoFit</c>).
/// </summary>
public sealed partial class PptxDocument
{
    /// <summary>The number of shrink-loop iterations attempted before giving up at the floor scale (Design Decision 4).</summary>
    private const int MaxShrinkIterations = 9;

    /// <summary>The per-iteration font-scale reduction step used by the shrink loop (10%).</summary>
    private const float ShrinkStep = 0.1f;

    /// <summary>The minimum font scale the shrink loop will reach before stopping regardless of fit.</summary>
    private const float MinShrinkScale = 0.1f;

    /// <summary>
    ///     The default OOXML tab-stop interval, in EMU (914400 EMU = 1 inch), consulted when a
    ///     run's literal text contains a U+0009 TAB character (Phase 2 Follow-Up: Default
    ///     Tab-Stop Expansion). Confirmed against a real-world corpus file
    ///     ("ERF IWF Breadboard Peer Review.pptx", slide 9): both its slide master's
    ///     <c>&lt;p:txStyles&gt;</c> and the presentation's own <c>&lt;p:defaultTextStyle&gt;</c>
    ///     declare <c>defTabSz="914400"</c> on every list level, and independent ground-truth
    ///     pixel measurement of the rendered slide (three different bullet-number widths) matched
    ///     this value's predicted tab-stop column to within 1px. Explicit <c>&lt;a:tabLst&gt;</c>
    ///     tab stops are deliberately out of scope - see the design doc's matching follow-up note.
    /// </summary>
    private const float DefaultTabStopEmu = 914400f;

    /// <summary>
    ///     Returns the next default tab stop strictly greater than <paramref name="currentXEmu"/>,
    ///     i.e. the smallest multiple of <paramref name="tabStopEmu"/> that exceeds
    ///     <paramref name="currentXEmu"/> - standard tab semantics, guaranteeing a tab always
    ///     advances the cursor by at least a minimal, non-zero amount even when
    ///     <paramref name="currentXEmu"/> already sits exactly on a stop boundary (a tab never
    ///     produces a zero-width advance).
    /// </summary>
    private static float GetNextTabStopEmu(float currentXEmu, float tabStopEmu) =>
        (MathF.Floor(currentXEmu / tabStopEmu) + 1f) * tabStopEmu;

    /// <summary>
    ///     Resolves a text body's full layout: every run/paragraph's effective properties, word-
    ///     wrapped into lines bounded by <paramref name="widthEmu"/>, positioned per the body's
    ///     horizontal alignment/vertical anchor, and scaled per its autofit policy.
    /// </summary>
    /// <param name="textBody">The parsed text body to lay out.</param>
    /// <param name="placeholderProperties">The owning shape's resolved placeholder property chain.</param>
    /// <param name="theme">The resolved theme.</param>
    /// <param name="placeholderType">The owning shape's placeholder type, or <see cref="string.Empty"/> for a non-placeholder shape.</param>
    /// <param name="widthEmu">The owning shape's own declared width, in EMU (see <see cref="PptxShapeFrame.WidthEmu"/>).</param>
    /// <param name="heightEmu">The owning shape's own declared height, in EMU (see <see cref="PptxShapeFrame.HeightEmu"/>).</param>
    /// <param name="fontResolver">
    ///     Resolves a <c>(familyName, bold, italic)</c> triple to a <see cref="TrueTypeFont"/> -
    ///     production callers supply <see cref="ResolveTextFont"/>; tests may inject a delegate
    ///     that bypasses installed-font discovery entirely (for example calling
    ///     <see cref="SystemFontCatalog.LoadBundledFallback"/> directly) for deterministic,
    ///     machine-independent pixel assertions.
    /// </param>
    /// <param name="colorMap">
    ///     The effective color map consulted when a run/bullet color resolves an
    ///     <c>&lt;a:schemeClr val="bg1"/&gt;</c>-shaped token, or <see langword="null"/> (the
    ///     default) - see <see cref="ResolveFill"/>'s matching parameter.
    /// </param>
    /// <param name="fallbackFontResolver">
    ///     Resolves a <c>(bold, italic)</c> pair to a bundled fallback <see cref="TrueTypeFont"/>,
    ///     consulted only when a specific character is missing from its run's own primary font
    ///     (per-character glyph-coverage fallback - see the private <c>ResolveGlyph</c> helper
    ///     this phase builds from it). Production callers leave this at its default
    ///     (<see cref="SystemFontCatalog.LoadBundledFallback"/> with the same
    ///     <c>serif: false, fixedPitch: false</c> convention <see cref="ResolveTextFont"/> already
    ///     uses); tests may inject a deterministic synthetic fallback font instead, mirroring
    ///     <paramref name="fontResolver"/>'s own test seam.
    /// </param>
    /// <returns>The resolved <see cref="PptxTextLayout"/>.</returns>
    internal static PptxTextLayout ResolveTextLayout(
        PptxTextBody textBody,
        PptxPlaceholderProperties placeholderProperties,
        PptxTheme theme,
        string placeholderType,
        float widthEmu,
        float heightEmu,
        Func<string, bool, bool, TrueTypeFont> fontResolver,
        PptxColorMap? colorMap = null,
        Func<bool, bool, TrueTypeFont>? fallbackFontResolver = null)
    {
        fallbackFontResolver ??= static (bold, italic) => SystemFontCatalog.LoadBundledFallback(serif: false, fixedPitch: false, bold, italic);
        var bodyProperties = textBody.Properties;
        var insetLeft = bodyProperties.InsetLeftEmu;
        var insetRight = bodyProperties.InsetRightEmu;
        var insetTop = bodyProperties.InsetTopEmu;
        var insetBottom = bodyProperties.InsetBottomEmu;

        var availableWidth = bodyProperties.Wrap == PptxTextWrap.None
            ? float.MaxValue / 4f
            : MathF.Max(0f, widthEmu - insetLeft - insetRight);
        var availableHeight = MathF.Max(0f, heightEmu - insetTop - insetBottom);

        // Horizontal alignment (center/right) must always be computed against the shape's own
        // declared box width, never against the effectively-infinite width used above to suppress
        // word-wrapping for a wrap="none" (typically spAutoFit) shape: PositionLines centers (or
        // right-aligns) each line within "availableWidthEmu" - passing the infinite wrap width
        // through unchanged would place a centered/right-aligned line's glyphs at an astronomical
        // X offset, far outside the shape (and surface) entirely, painting nothing. A wrap="none"
        // shape still has a real, finite declared width (<c>widthEmu</c>/<c>cx</c>) that
        // alignment must honor even though wrapping itself is suppressed.
        var alignmentWidth = MathF.Max(0f, widthEmu - insetLeft - insetRight);

        var fontCache = new Dictionary<(string Family, bool Bold, bool Italic), TrueTypeFont>();
        TrueTypeFont ResolveFont(string family, bool bold, bool italic)
        {
            var key = (family, bold, italic);
            if (!fontCache.TryGetValue(key, out var font))
            {
                font = fontResolver(family, bold, italic);
                fontCache[key] = font;
            }

            return font;
        }

        // Per-character glyph-coverage fallback - the tofu-box feature gap. A run is resolved
        // to exactly one primary font above, but that font may not cover every character the
        // run's text actually contains. ResolveGlyph below tries the primary font first and,
        // only on an actual notdef miss for a non-whitespace character, consults a bundled
        // fallback font, memoized per bold/italic pair the same way as fontCache above. When
        // the fallback font covers the character, its own font and glyph index win for that one
        // character only; otherwise the primary font's own notdef glyph is kept, the same "no
        // candidate matched" convention PDF's simple-font resolver already uses. Whitespace is
        // deliberately excluded, since a primary font's own missing space glyph is not a visible
        // tofu box and does not warrant a fallback-font substitution.
        var fallbackFontCache = new Dictionary<(bool Bold, bool Italic), TrueTypeFont>();
        TrueTypeFont ResolveFallbackFont(bool bold, bool italic)
        {
            var key = (bold, italic);
            if (!fallbackFontCache.TryGetValue(key, out var font))
            {
                font = fallbackFontResolver(bold, italic);
                fallbackFontCache[key] = font;
            }

            return font;
        }

        (TrueTypeFont Font, int GlyphIndex) ResolveGlyph(TrueTypeFont primaryFont, int codepoint, bool bold, bool italic)
        {
            var glyphIndex = primaryFont.GetGlyphIndex(codepoint);
            if (glyphIndex != 0 || Rune.IsWhiteSpace(new Rune(codepoint)))
            {
                return (primaryFont, glyphIndex);
            }

            var fallbackFont = ResolveFallbackFont(bold, italic);
            var fallbackGlyphIndex = fallbackFont.GetGlyphIndex(codepoint);
            return fallbackGlyphIndex != 0 ? (fallbackFont, fallbackGlyphIndex) : (primaryFont, glyphIndex);
        }

        var paragraphs = textBody.Paragraphs
            .Select(paragraph =>
            {
                // Run properties are resolved before paragraph properties (reversing the two
                // Selects' former independence) because bullet resolution needs the paragraph's
                // own first run's effective properties as its "follow text"
                // (buClrTx/buFontTx/buSzTx) fallback - see ResolveEffectiveBulletProperties.
                var runProperties = paragraph.Runs
                    .Select(run => ResolveEffectiveRunProperties(run, paragraph, placeholderProperties, theme, placeholderType, colorMap))
                    .ToList();
                var firstRunProperties = runProperties.Count > 0 ? runProperties[0] : null;
                var paragraphProperties = ResolveEffectiveParagraphProperties(paragraph, placeholderProperties, placeholderType, firstRunProperties, colorMap);
                return new ResolvedParagraph(paragraph, paragraphProperties, runProperties);
            })
            .ToList();

        var (fontScale, lineSpacingFactor) = ResolveAutofitScale(
            bodyProperties.AutofitElement,
            paragraphs,
            availableWidth,
            availableHeight,
            ResolveFont,
            ResolveGlyph);

        var lines = BuildLines(paragraphs, availableWidth, fontScale, lineSpacingFactor, ResolveFont, ResolveGlyph);
        var (glyphs, underlines) = PositionLines(lines, bodyProperties.Anchor, insetLeft, insetTop, alignmentWidth, availableHeight);

        return new PptxTextLayout(glyphs, fontScale, underlines);
    }

    /// <summary>
    ///     Resolves the autofit font-scale/line-spacing-reduction factors to apply, per Design
    ///     Decision 4's three-tier policy: <c>noAutofit</c>/absent/<c>spAutoFit</c> apply no
    ///     scaling; an explicit <c>normAutofit</c> applies each of its <c>fontScale</c>/
    ///     <c>lnSpcReduction</c> attributes independently (per OOXML, each is independently
    ///     optional - an element declaring only one still carries a meaningful, persisted factor
    ///     for that one, falling back to that one attribute's own spec-defined neutral default
    ///     (<c>100000</c>/no reduction) only when it is itself absent); a <c>normAutofit</c>
    ///     declaring neither attribute runs a bounded 10%-step shrink loop instead, stopping at
    ///     the first scale whose natural layout height fits <paramref name="availableHeight"/> or
    ///     at the 10% floor, whichever comes first.
    /// </summary>
    /// <returns>A <c>(FontScale, LineSpacingFactor)</c> pair, each <c>1.0</c> meaning "unscaled".</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="autofitElement"/>'s <c>fontScale</c>/<c>lnSpcReduction</c>
    ///     attribute is present but not a well-formed, finite <see cref="float"/> - see
    ///     <c>PptxDocument.TextInheritance.cs</c>'s <c>ParseNullableFloatAttribute</c>.
    /// </exception>
    private static (float FontScale, float LineSpacingFactor) ResolveAutofitScale(
        System.Xml.Linq.XElement? autofitElement,
        IReadOnlyList<ResolvedParagraph> paragraphs,
        float availableWidth,
        float availableHeight,
        Func<string, bool, bool, TrueTypeFont> resolveFont,
        Func<TrueTypeFont, int, bool, bool, (TrueTypeFont Font, int GlyphIndex)> resolveGlyph)
    {
        if (autofitElement is null || autofitElement.Name.LocalName is "noAutofit" or "spAutoFit")
        {
            return (1f, 1f);
        }

        var fontScaleAttribute = ParseNullableFloatAttribute(autofitElement.Attribute("fontScale"));
        var lnSpcReductionAttribute = ParseNullableFloatAttribute(autofitElement.Attribute("lnSpcReduction"));
        if (fontScaleAttribute is { } || lnSpcReductionAttribute is { })
        {
            var fontScale = fontScaleAttribute is { } storedFontScale ? storedFontScale / 100000f : 1f;
            var lineSpacingFactor = lnSpcReductionAttribute is { } storedReduction ? 1f - (storedReduction / 100000f) : 1f;
            return (fontScale, lineSpacingFactor);
        }

        // Attribute-less <a:normAutofit/>: bounded deterministic shrink loop (Design Decision 4).
        var scale = 1f;
        for (var iteration = 0; iteration < MaxShrinkIterations; iteration++)
        {
            var lines = BuildLines(paragraphs, availableWidth, scale, 1f, resolveFont, resolveGlyph);
            var totalHeight = lines.Sum(line => line.LineHeightEmu + line.LeadingGapEmu);
            if (totalHeight <= availableHeight || scale <= MinShrinkScale)
            {
                return (scale, 1f);
            }

            scale = MathF.Max(MinShrinkScale, scale - ShrinkStep);
        }

        return (scale, 1f);
    }

    /// <summary>
    ///     Tokenizes run text into whitespace/non-whitespace runs, preserving every consecutive-
    ///     whitespace token's own advance width. A literal U+0009 TAB character (Phase 2
    ///     Follow-Up: Default Tab-Stop Expansion) is always isolated as its own single-character
    ///     token - never merged with adjacent whitespace of another kind, nor with another
    ///     adjacent tab - since its effective wrap/render width depends on its own position in the
    ///     line (see <see cref="GetNextTabStopEmu"/>), not a fixed, pre-computable glyph advance.
    /// </summary>
    private static List<(string Text, bool IsWhitespace, bool IsTab)> Tokenize(string text)
    {
        var tokens = new List<(string Text, bool IsWhitespace, bool IsTab)>();
        var start = 0;
        while (start < text.Length)
        {
            if (text[start] == '\t')
            {
                tokens.Add((text[start..(start + 1)], true, true));
                start++;
                continue;
            }

            var isWhitespace = char.IsWhiteSpace(text[start]);
            var end = start + 1;
            while (end < text.Length && text[end] != '\t' && char.IsWhiteSpace(text[end]) == isWhitespace)
            {
                end++;
            }

            tokens.Add((text[start..end], isWhitespace, false));
            start = end;
        }

        return tokens;
    }

    /// <summary>A single word-wrap token, resolved to a specific run's effective properties/font for width measurement and emission.</summary>
    /// <param name="Text">The token's own text (empty for an <see cref="IsLineBreak"/> token).</param>
    /// <param name="IsWhitespace">Whether the token is a collapsible run of whitespace rather than a visible word.</param>
    /// <param name="RunProperties">The owning run's effective, already font-scaled properties (unused for an <see cref="IsLineBreak"/> token).</param>
    /// <param name="Font">The owning run's resolved font (unused for an <see cref="IsLineBreak"/> token).</param>
    /// <param name="WidthEmu">The token's measured advance width, in EMU (always <c>0</c> for an <see cref="IsLineBreak"/> token).</param>
    /// <param name="IsLineBreak">
    ///     When <see langword="true"/>, this token represents an explicit <c>&lt;a:br&gt;</c> line
    ///     break rather than run text: it carries no text/font/width of its own and is never
    ///     added to a line's glyph content - <see cref="PackTokensIntoLines"/> instead consumes
    ///     it to force a new line boundary at this point in the paragraph's token stream.
    /// </param>
    /// <param name="IsTab">
    ///     When <see langword="true"/>, this token represents a single literal U+0009 TAB run
    ///     character (Phase 2 Follow-Up: Default Tab-Stop Expansion) rather than an ordinary
    ///     glyph-measured word/whitespace token: its <see cref="WidthEmu"/> is unused (always
    ///     <c>0</c>, since a tab's true width depends on its own position in the line, not a
    ///     fixed advance) - both <see cref="PackTokensIntoLines"/> and <see cref="BuildLines"/>'s
    ///     own per-line loop instead compute its effective width dynamically via
    ///     <see cref="GetNextTabStopEmu"/> from the current running X position at that point.
    /// </param>
    private readonly record struct ResolvedToken(
        string Text,
        bool IsWhitespace,
        PptxEffectiveRunProperties RunProperties,
        TrueTypeFont Font,
        float WidthEmu,
        bool IsLineBreak = false,
        bool IsTab = false);

    /// <summary>A single laid-out glyph, positioned relative to its own line's start (alignment/margin not yet applied).</summary>
    private readonly record struct LineGlyph(TrueTypeFont Font, int GlyphIndex, float XInLineEmu, float SizeEmu, Rgba32 Color);

    /// <summary>
    ///     A single contiguous underlined span on one line (Phase 2 Follow-Up: Underline
    ///     Rendering), positioned relative to the line's own start (alignment/margin not yet
    ///     applied) - mirrors <see cref="LineGlyph"/>'s own convention.
    /// </summary>
    /// <param name="StartXEmu">The span's start X, relative to the line's own start, in EMU.</param>
    /// <param name="EndXEmu">The span's end X, relative to the line's own start, in EMU.</param>
    /// <param name="RunProperties">
    ///     The underlined run's own resolved effective properties, supplying the style/color/size
    ///     <see cref="PositionLines"/> carries into the final <see cref="PptxUnderlineSegment"/>.
    /// </param>
    private readonly record struct LineUnderlineSpan(float StartXEmu, float EndXEmu, PptxEffectiveRunProperties RunProperties);

    /// <summary>A single word-wrapped line, ready for alignment/vertical-anchor positioning.</summary>
    /// <param name="Glyphs">The line's own text glyphs, positioned relative to the line's own start.</param>
    /// <param name="LineWidthEmu">The line's natural (unaligned) text width, in EMU.</param>
    /// <param name="LineHeightEmu">The line's resolved height, in EMU.</param>
    /// <param name="AscentEmu">The line's resolved ascent (baseline offset from the line's top), in EMU.</param>
    /// <param name="ParagraphProperties">The owning paragraph's resolved effective properties.</param>
    /// <param name="IsFirstLineOfParagraph">Whether this is the owning paragraph's first word-wrapped line.</param>
    /// <param name="BulletGlyphs">
    ///     The paragraph's rendered bullet glyph(s) (Phase 2 Follow-Up: Bullets and Numbering),
    ///     positioned relative to X=0 (the hanging-indent gutter, not the line's own text start -
    ///     see <see cref="PositionLines"/>). Always empty except on a bulleted paragraph's own
    ///     first line (<see cref="IsFirstLineOfParagraph"/>).
    /// </param>
    /// <param name="BulletWidthEmu">
    ///     The bullet string's own total measured advance width, in EMU (<c>0</c> when
    ///     <see cref="BulletGlyphs"/> is empty) - consulted by <see cref="PositionLines"/> to
    ///     guarantee the paragraph's own text never shares an X range with the bullet glyph(s) it
    ///     is painted beside (the gutter-clearance fix).
    /// </param>
    /// <param name="BulletTrailingGapEmu">
    ///     The minimum additional gap, in EMU, <see cref="PositionLines"/> reserves beyond
    ///     <see cref="BulletWidthEmu"/> before the paragraph's own text may start (<c>0</c> for
    ///     every bullet kind except <see cref="PptxBulletKind.AutoNum"/> - see
    ///     <see cref="BuildBulletGlyphs"/>'s own remarks for why auto-numbered markers alone need
    ///     this extra, PowerPoint-like, tab-stop-style separation).
    /// </param>
    /// <param name="UnderlineSpans">
    ///     The line's resolved underlined spans (Phase 2 Follow-Up: Underline Rendering),
    ///     positioned relative to the line's own start, same convention as <see cref="Glyphs"/>.
    /// </param>
    /// <param name="LeadingGapEmu">
    ///     The extra vertical gap, in EMU, inserted immediately before this line's own box (Phase
    ///     2 Follow-Up: Paragraph Spacing) - always <c>0</c> except a paragraph's first
    ///     word-wrapped line when a preceding paragraph exists, where it carries the additive
    ///     <c>spcAft(previous paragraph) + spcBef(this paragraph)</c> gap. Never applied before a
    ///     text body's first paragraph nor after its last (see <see cref="BuildLines"/>'s own
    ///     remarks for the exact additive semantics this field implements).
    /// </param>
    private sealed record LineBox(
        IReadOnlyList<LineGlyph> Glyphs,
        float LineWidthEmu,
        float LineHeightEmu,
        float AscentEmu,
        PptxEffectiveParagraphProperties ParagraphProperties,
        bool IsFirstLineOfParagraph,
        IReadOnlyList<LineGlyph> BulletGlyphs,
        float BulletWidthEmu,
        float BulletTrailingGapEmu,
        IReadOnlyList<LineUnderlineSpan> UnderlineSpans,
        float LeadingGapEmu = 0f);

    /// <summary>A paragraph with its effective paragraph properties and every run's effective properties resolved up front.</summary>
    private sealed record ResolvedParagraph(
        PptxParagraph Paragraph,
        PptxEffectiveParagraphProperties ParagraphProperties,
        IReadOnlyList<PptxEffectiveRunProperties> RunProperties);

    /// <summary>
    ///     Computes a resolved token's natural advance width, in EMU, summing each Unicode scalar
    ///     value (code point)'s font-metric advance - via <paramref name="resolveGlyph"/>, so a
    ///     code point the token's own primary <paramref name="font"/> lacks (and that a bundled
    ///     fallback font covers) measures using the fallback font's own advance width, not the
    ///     primary font's glyph-0 advance, keeping word-wrap width measurement consistent with
    ///     what <see cref="BuildLines"/>'s own per-code-point loop actually paints. Enumerating
    ///     via <c>text.EnumerateRunes()</c> (rather than per-UTF-16 <c>char</c>) ensures a
    ///     supplementary-plane character (for example an emoji, codepoint above <c>U+FFFF</c>) is
    ///     measured as a single logical unit, not as two mis-measured surrogate halves.
    /// </summary>
    private static float MeasureTokenWidthEmu(
        string text,
        TrueTypeFont font,
        float sizeEmu,
        bool bold,
        bool italic,
        Func<TrueTypeFont, int, bool, bool, (TrueTypeFont Font, int GlyphIndex)> resolveGlyph)
    {
        var total = 0f;
        foreach (var rune in text.EnumerateRunes())
        {
            var (resolvedFont, glyphIndex) = resolveGlyph(font, rune.Value, bold, italic);
            total += resolvedFont.GetAdvanceWidth(glyphIndex) / (float)resolvedFont.UnitsPerEm * sizeEmu;
        }

        return total;
    }

    /// <summary>
    ///     Word-wraps every paragraph's tokenized runs into <see cref="LineBox"/> lines bounded by
    ///     <paramref name="availableWidthEmu"/>, applying <paramref name="fontScale"/> to every
    ///     run's resolved size and <paramref name="lineSpacingFactor"/> to every paragraph's
    ///     resolved line spacing. A token that alone exceeds <paramref name="availableWidthEmu"/>
    ///     is placed alone on its own (overflowing) line rather than looping indefinitely - a
    ///     documented overflow policy, not a crash.
    /// </summary>
    /// <remarks>
    ///     Phase 2 Follow-Up: Paragraph Spacing - the gap between two adjacent paragraphs is
    ///     additive (<c>spcAft(paragraph N) + spcBef(paragraph N+1)</c>), applied only between
    ///     paragraphs - never before a text body's first paragraph nor after its last - via each
    ///     paragraph's own first line's <see cref="LineBox.LeadingGapEmu"/>. A percentage
    ///     (<c>spcPct</c>) spacing value resolves against the boundary line's own already-resolved
    ///     <c>lineHeight</c> (the first line for <c>spcBef</c>, the last line for <c>spcAft</c>) -
    ///     a documented scoping decision avoiding a second, separately-tracked "natural height".
    /// </remarks>
    private static List<LineBox> BuildLines(
        IReadOnlyList<ResolvedParagraph> paragraphs,
        float availableWidthEmu,
        float fontScale,
        float lineSpacingFactor,
        Func<string, bool, bool, TrueTypeFont> resolveFont,
        Func<TrueTypeFont, int, bool, bool, (TrueTypeFont Font, int GlyphIndex)> resolveGlyph)
    {
        var lines = new List<LineBox>();

        // Auto-number counter/last-type state (Phase 2 Follow-Up: Bullets and Numbering),
        // scoped per-call (one text body/shape) - see AdvanceBulletCounters' own remarks for the
        // full per-level sequencing/reset state machine this threads across the paragraph loop.
        var counters = new int[MaxParagraphLevel + 1];
        var lastTypes = new string?[MaxParagraphLevel + 1];

        // Phase 2 Follow-Up: Paragraph Spacing - tracks the previous paragraph's own resolved
        // SpaceAfter and its last line's resolved lineHeight (needed to resolve that SpaceAfter's
        // own spcPct, if any), so the next paragraph's first line can compute its additive
        // leading gap. isFirstParagraph suppresses the gap before the text body's very first
        // paragraph, regardless of any spcBef it declares.
        var isFirstParagraph = true;
        var previousSpaceAfter = PptxLineSpacing.None;
        var previousLastLineHeightEmu = 0f;

        foreach (var resolvedParagraph in paragraphs)
        {
            var paragraph = resolvedParagraph.Paragraph;
            var paraProps = resolvedParagraph.ParagraphProperties;
            var level = Math.Clamp(paragraph.RawProperties.Level, 0, MaxParagraphLevel);
            var bulletText = AdvanceBulletCounters(level, paraProps.Bullet, counters, lastTypes);

            // Phase 2 Follow-Up: Bullets and Numbering - a paragraph with no <a:r> run children
            // (a blank/spacer paragraph, for example one containing only <a:endParaRPr>) must not
            // paint a bullet glyph, even though its bullet properties still resolve and its
            // auto-number counter state must still advance above (PowerPoint itself never shows a
            // bullet next to an empty line). A run whose text is whitespace-only still counts as
            // "has a run" here - a documented, accepted minor limitation (see the design doc).
            var hasRuns = paragraph.Items.Any(item => item is PptxRunItem);

            // Build the paragraph's scaled token stream, resolving each run's font once. An
            // <c>&lt;a:br&gt;</c> item becomes a break-marker token that forces a new line in
            // PackTokensIntoLines, preserving its position relative to surrounding runs.
            var tokens = new List<ResolvedToken>();
            var runIndex = 0;
            foreach (var item in paragraph.Items)
            {
                if (item is PptxLineBreakItem)
                {
                    tokens.Add(new ResolvedToken(string.Empty, false, default!, null!, 0f, IsLineBreak: true));
                    continue;
                }

                var run = ((PptxRunItem)item).Run;
                var runProps = resolvedParagraph.RunProperties[runIndex];
                runIndex++;
                var scaledSizeEmu = runProps.SizeEmu * fontScale;
                var font = resolveFont(runProps.FontFamily, runProps.Bold, runProps.Italic);
                var scaledRunProps = runProps with { SizeEmu = scaledSizeEmu };

                foreach (var (text, isWhitespace, isTab) in Tokenize(run.Text))
                {
                    if (text.Length == 0)
                    {
                        continue;
                    }

                    // A tab token's true width depends on its own position in the line - skip the
                    // (otherwise-misleading, font-glyph-based) measurement entirely rather than
                    // looking up a near-zero/.notdef glyph advance for it (see GetNextTabStopEmu).
                    var width = isTab
                        ? 0f
                        : MeasureTokenWidthEmu(text, font, scaledSizeEmu, runProps.Bold, runProps.Italic, resolveGlyph);
                    tokens.Add(new ResolvedToken(text, isWhitespace, scaledRunProps, font, width, IsTab: isTab));
                }
            }

            var paragraphLines = PackTokensIntoLines(tokens, availableWidthEmu);
            if (paragraphLines.Count == 0)
            {
                // A blank paragraph (no runs/tokens) still occupies a visible, empty line - its
                // height falls back to the nominal default font size (see the blank-line branch
                // below), since there is no run to resolve font metrics from.
                paragraphLines.Add([]);
            }

            for (var i = 0; i < paragraphLines.Count; i++)
            {
                var lineTokens = paragraphLines[i];
                var glyphs = new List<LineGlyph>();
                var underlineSpans = new List<LineUnderlineSpan>();
                LineUnderlineSpan? pendingUnderlineSpan = null;
                var cursorX = 0f;
                TrueTypeFont? tallestFont = null;
                var tallestSize = 0f;
                var tallestNaturalHeight = -1f;

                foreach (var token in lineTokens)
                {
                    var tokenStartX = cursorX;

                    // Compare each candidate's actual rendered natural height (its raw font-design-
                    // unit metrics scaled by its own UnitsPerEm and SizeEmu), not raw font-design
                    // units directly - otherwise a small-font run using a font with a tall em-box
                    // can out-rank a much larger run that happens to use a more compact em-box,
                    // producing the wrong line height/baseline.
                    var naturalHeight = (token.Font.Ascender - token.Font.Descender + token.Font.LineGap) /
                        (float)token.Font.UnitsPerEm * token.RunProperties.SizeEmu;
                    if (naturalHeight > tallestNaturalHeight)
                    {
                        tallestNaturalHeight = naturalHeight;
                        tallestFont = token.Font;
                        tallestSize = token.RunProperties.SizeEmu;
                    }

                    if (token.IsTab)
                    {
                        // Phase 2 Follow-Up: Default Tab-Stop Expansion - expand the cursor to
                        // the next default tab stop instead of resolving a (near-zero,
                        // .notdef-glyph) font advance for the literal U+0009 TAB character; no
                        // glyph is emitted (mirroring the pre-existing IsWhitespace glyph-
                        // emission guard below for every other whitespace token kind). Explicit
                        // <a:tabLst> tab stops are out of scope - see the design doc note.
                        cursorX = GetNextTabStopEmu(cursorX, DefaultTabStopEmu);
                    }
                    else
                    {
                        // Enumerate by Unicode scalar value (Rune), not UTF-16 char, so a
                        // supplementary-plane character (codepoint above U+FFFF, e.g. an emoji)
                        // is measured/placed as one logical glyph unit instead of being split
                        // into two mis-measured surrogate halves.
                        foreach (var rune in token.Text.EnumerateRunes())
                        {
                            var (resolvedFont, glyphIndex) = resolveGlyph(token.Font, rune.Value, token.RunProperties.Bold, token.RunProperties.Italic);
                            var advance = resolvedFont.GetAdvanceWidth(glyphIndex) / (float)resolvedFont.UnitsPerEm * token.RunProperties.SizeEmu;
                            if (!token.IsWhitespace)
                            {
                                glyphs.Add(new LineGlyph(resolvedFont, glyphIndex, cursorX, token.RunProperties.SizeEmu, token.RunProperties.Color));
                            }

                            cursorX += advance;
                        }
                    }

                    var tokenEndX = cursorX;

                    // Phase 2 Follow-Up: Underline Rendering - accumulate contiguous underlined
                    // tokens belonging to the same run instance into a single span, so the span
                    // covers interior whitespace within one run (not just individual words) and
                    // flushes exactly at a run boundary (including a transition to a
                    // non-underlined run). RunProperties is reused, by reference, across every
                    // token of a single run (see BuildLines' own per-run loop above) and survives
                    // PackTokensIntoLines unchanged, so ReferenceEquals reliably detects "same
                    // run" here.
                    if (token.RunProperties.UnderlineStyle != PptxUnderlineStyle.None)
                    {
                        if (pendingUnderlineSpan is { } pending && ReferenceEquals(pending.RunProperties, token.RunProperties))
                        {
                            pendingUnderlineSpan = pending with { EndXEmu = tokenEndX };
                        }
                        else
                        {
                            if (pendingUnderlineSpan is { } toFlush)
                            {
                                underlineSpans.Add(toFlush);
                            }

                            pendingUnderlineSpan = new LineUnderlineSpan(tokenStartX, tokenEndX, token.RunProperties);
                        }
                    }
                    else
                    {
                        if (pendingUnderlineSpan is { } toFlush)
                        {
                            underlineSpans.Add(toFlush);
                        }

                        pendingUnderlineSpan = null;
                    }
                }

                // The line's natural width is the cursor's own final accumulated position -
                // authoritative for every token kind (including a tab, whose WidthEmu is unused -
                // see ResolvedToken.IsTab), so a single post-loop read replaces a separate,
                // now-inconsistent-for-tabs running accumulator.
                var lineWidth = cursorX;

                if (pendingUnderlineSpan is { } finalSpan)
                {
                    underlineSpans.Add(finalSpan);
                }

                float lineHeight;
                float ascent;
                if (tallestFont is null)
                {
                    // Blank line: fall back to the default font size's nominal proportions.
                    ascent = DefaultFontSizeEmu * 0.8f;
                    lineHeight = DefaultFontSizeEmu * 1.2f * lineSpacingFactor;
                }
                else
                {
                    var naturalHeight = (tallestFont.Ascender - tallestFont.Descender + tallestFont.LineGap) /
                        (float)tallestFont.UnitsPerEm * tallestSize;
                    ascent = tallestFont.Ascender / (float)tallestFont.UnitsPerEm * tallestSize;
                    var spacingFactor = paraProps.LineSpacing.Percent is { } percent ? percent : 1f;
                    lineHeight = paraProps.LineSpacing.FixedEmu is { } fixedEmu
                        ? fixedEmu * lineSpacingFactor
                        : naturalHeight * spacingFactor * lineSpacingFactor;
                }

                // Phase 2 Follow-Up: Paragraph Spacing - only the paragraph's own first
                // word-wrapped line carries a leading gap, and only when a preceding paragraph
                // exists (never before the text body's very first paragraph). The gap is additive:
                // the preceding paragraph's own SpaceAfter (resolved against its own last line's
                // lineHeight) plus this paragraph's own SpaceBefore (resolved against this, its
                // first, line's lineHeight).
                var leadingGapEmu = 0f;
                if (i == 0 && !isFirstParagraph)
                {
                    leadingGapEmu = ResolveSpacingEmu(previousSpaceAfter, previousLastLineHeightEmu) +
                        ResolveSpacingEmu(paraProps.EffectiveSpaceBefore, lineHeight);
                }

                var isFirstLine = i == 0;
                var bulletGlyphsResult = isFirstLine && hasRuns
                    ? BuildBulletGlyphs(bulletText, paraProps.Bullet, fontScale, resolveFont, resolveGlyph)
                    : new BulletGlyphsResult([], 0f, 0f);

                lines.Add(new LineBox(glyphs, lineWidth, lineHeight, ascent, paraProps, isFirstLine, bulletGlyphsResult.Glyphs, bulletGlyphsResult.WidthEmu, bulletGlyphsResult.TrailingGapEmu, underlineSpans, leadingGapEmu));

                // The paragraph's own last line records its resolved SpaceAfter/lineHeight for
                // the next paragraph's own leading-gap computation above.
                if (i == paragraphLines.Count - 1)
                {
                    previousSpaceAfter = paraProps.EffectiveSpaceAfter;
                    previousLastLineHeightEmu = lineHeight;
                }
            }

            isFirstParagraph = false;
        }

        return lines;
    }

    /// <summary>
    ///     Resolves a paragraph's spacing-before/after (Phase 2 Follow-Up: Paragraph Spacing) into
    ///     a concrete EMU gap: a fixed value (<c>spcPts</c>) is used verbatim; a percentage
    ///     (<c>spcPct</c>) resolves against <paramref name="referenceLineHeightEmu"/> - the
    ///     boundary line's own already-resolved <c>lineHeight</c> (already scaled by any
    ///     applicable autofit <c>lnSpcReduction</c>), deliberately not a second, unscaled "natural
    ///     height" (see <see cref="BuildLines"/>'s own remarks for this scoping decision).
    /// </summary>
    private static float ResolveSpacingEmu(PptxLineSpacing spacing, float referenceLineHeightEmu) =>
        spacing.FixedEmu is { } fixedEmu ? fixedEmu : referenceLineHeightEmu * (spacing.Percent ?? 0f);

    /// <summary>
    ///     Advances this call's (one shape/text-body's) per-level auto-number counter/last-type
    ///     state machine for one paragraph in document order, and returns that paragraph's
    ///     rendered bullet string (Phase 2 Follow-Up: Bullets and Numbering) - or
    ///     <see langword="null"/> when the paragraph has no bullet at all, is explicitly
    ///     <see cref="PptxBulletKind.None"/>, or is an auto-number whose <c>type</c>
    ///     <see cref="FormatAutoNumber"/> does not recognize (that one bullet is then simply
    ///     skipped, per this phase's documented graceful-degradation policy - the paragraph's own
    ///     text still renders normally and the counter state still advances for later
    ///     paragraphs).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         For each paragraph, every counter/last-type slot strictly <em>deeper</em> than the
    ///         paragraph's own level is first zeroed/cleared - this closes out any nested list the
    ///         document has since left, so a later paragraph that returns to that deeper level
    ///         starts a fresh numbered run rather than resuming a stale one. The paragraph's own
    ///         level's slot is then either:
    ///     </para>
    ///     <list type="bullet">
    ///         <item><description>Reset to <c>startAt</c> (and the slot's last-type recorded) when this is the level's first auto-number run, or its <c>type</c> changed from the immediately-preceding paragraph at that same level (a new numbered run starting over).</description></item>
    ///         <item><description>Incremented by one, continuing the same numbered run.</description></item>
    ///         <item><description>Zeroed/cleared entirely for a non-auto-number (<see cref="PptxBulletKind.Char"/>/<see cref="PptxBulletKind.None"/>) paragraph, so any later resumption of auto-numbering at that level starts over rather than continuing silently across an intervening non-numbered paragraph.</description></item>
    ///     </list>
    /// </remarks>
    private static string? AdvanceBulletCounters(int level, PptxEffectiveBulletProperties? bullet, int[] counters, string?[] lastTypes)
    {
        level = Math.Clamp(level, 0, MaxParagraphLevel);
        for (var deeper = level + 1; deeper <= MaxParagraphLevel; deeper++)
        {
            counters[deeper] = 0;
            lastTypes[deeper] = null;
        }

        if (bullet is null || bullet.Kind == PptxBulletKind.None)
        {
            counters[level] = 0;
            lastTypes[level] = null;
            return null;
        }

        if (bullet.Kind == PptxBulletKind.Char)
        {
            counters[level] = 0;
            lastTypes[level] = null;
            return bullet.Character is { Length: > 0 } ? bullet.Character : null;
        }

        // PptxBulletKind.AutoNum.
        var autoNumType = bullet.AutoNumType ?? "arabicPeriod";
        if (lastTypes[level] != autoNumType)
        {
            counters[level] = bullet.AutoNumStartAt;
            lastTypes[level] = autoNumType;
        }
        else
        {
            counters[level]++;
        }

        return FormatAutoNumber(counters[level], autoNumType);
    }

    /// <summary>
    ///     The result of measuring a paragraph's rendered bullet string (see
    ///     <see cref="BuildBulletGlyphs"/>): the bullet's own positioned glyphs plus the bullet
    ///     string's total measured advance width - the latter is what <see cref="PositionLines"/>
    ///     needs to guarantee the paragraph's own text never shares an X range with the bullet
    ///     glyph(s) it is painted beside (the gutter-clearance fix; see the design document's
    ///     "Bullet/text gutter clearance" note).
    /// </summary>
    /// <param name="Glyphs">The bullet's own positioned glyphs (see <see cref="BuildBulletGlyphs"/>).</param>
    /// <param name="WidthEmu">The bullet string's total measured advance width, in EMU; <c>0</c> when <see cref="Glyphs"/> is empty.</param>
    /// <param name="TrailingGapEmu">
    ///     A minimum additional gap, in EMU, <see cref="PositionLines"/> reserves beyond
    ///     <see cref="WidthEmu"/> before the paragraph's own text may start - <c>0</c> for every
    ///     bullet kind except <see cref="PptxBulletKind.AutoNum"/> (see
    ///     <see cref="BuildBulletGlyphs"/>'s own remarks).
    /// </param>
    private readonly record struct BulletGlyphsResult(IReadOnlyList<LineGlyph> Glyphs, float WidthEmu, float TrailingGapEmu = 0f);

    /// <summary>
    ///     Tokenizes and measures a paragraph's rendered bullet string (if any) into
    ///     <see cref="LineGlyph"/>s positioned relative to X=0 - the hanging-indent gutter
    ///     (<c>marL+indent</c>), not the line's own text start (see
    ///     <see cref="PositionLines"/>'s separate bullet-gutter-X computation). Bullets are never
    ///     bold/italic regardless of any adjacent run's own style - a documented simplification
    ///     (see the design document).
    /// </summary>
    /// <remarks>
    ///     For <see cref="PptxBulletKind.AutoNum"/> bullets, the returned
    ///     <see cref="BulletGlyphsResult.TrailingGapEmu"/> additionally reserves one space
    ///     character's own advance width, measured in the bullet's already-resolved font/size, as
    ///     a minimum gap beyond the marker's own measured width - approximating PowerPoint's own
    ///     visibly wider, tab-stop-like separation after a number marker (e.g. <c>"1."</c>), which
    ///     the plain gutter-clearance clamp alone (a "just touching" minimum) does not reproduce.
    ///     This is gated to <see cref="PptxBulletKind.AutoNum"/> only - a <see cref="PptxBulletKind.Char"/>
    ///     bullet's own already-correct, already-verified "touching, zero extra gap" behavior is
    ///     unaffected (see the design document's "Closed risk (bullet/text gutter clearance)"
    ///     note).
    /// </remarks>
    private static BulletGlyphsResult BuildBulletGlyphs(
        string? bulletText,
        PptxEffectiveBulletProperties? bullet,
        float fontScale,
        Func<string, bool, bool, TrueTypeFont> resolveFont,
        Func<TrueTypeFont, int, bool, bool, (TrueTypeFont Font, int GlyphIndex)> resolveGlyph)
    {
        if (string.IsNullOrEmpty(bulletText) || bullet is null || bullet.Kind == PptxBulletKind.None)
        {
            return new BulletGlyphsResult([], 0f);
        }

        var font = resolveFont(bullet.FontFamily, false, false);
        var sizeEmu = bullet.SizeEmu * fontScale;
        var glyphs = new List<LineGlyph>();
        var cursorX = 0f;
        // Enumerate by Unicode scalar value (Rune), not UTF-16 char, so a supplementary-plane
        // bullet character (e.g. an <a:buChar char="..."/> emoji) is measured/placed as one
        // logical glyph unit instead of two mis-measured surrogate halves.
        foreach (var rune in bulletText.EnumerateRunes())
        {
            // Bullets are never bold/italic (see this method's own remarks), so the fallback
            // lookup is always keyed (false, false), matching resolveFont's own call above.
            var (resolvedFont, glyphIndex) = resolveGlyph(font, rune.Value, false, false);
            var advance = resolvedFont.GetAdvanceWidth(glyphIndex) / (float)resolvedFont.UnitsPerEm * sizeEmu;
            glyphs.Add(new LineGlyph(resolvedFont, glyphIndex, cursorX, sizeEmu, bullet.Color));
            cursorX += advance;
        }

        var trailingGapEmu = 0f;
        if (bullet.Kind == PptxBulletKind.AutoNum)
        {
            var spaceGlyphIndex = font.GetGlyphIndex(' ');
            trailingGapEmu = font.GetAdvanceWidth(spaceGlyphIndex) / (float)font.UnitsPerEm * sizeEmu;
        }

        return new BulletGlyphsResult(glyphs, cursorX, trailingGapEmu);
    }

    /// <summary>
    ///     Greedily packs a paragraph's token stream into width-bounded lines, returning each
    ///     line's own token list. An <see cref="ResolvedToken.IsLineBreak"/> token (from an
    ///     explicit <c>&lt;a:br&gt;</c>) is never itself added to a line - it instead force-flushes
    ///     the current line (even if empty) and starts a new one, independent of word-wrap width.
    ///     Only whitespace tokens (and line breaks) are legal wrap points: consecutive
    ///     non-whitespace tokens are accumulated into a single wrap-atomic "word group" and
    ///     packed as one unit, so a word <see cref="Tokenize"/> happened to split across two
    ///     adjacent formatting runs (for example <c>"Hel"</c> in a bold run immediately followed
    ///     by <c>"lo"</c> in a plain run, with no whitespace between them) can never wrap at that
    ///     run boundary - it wraps only where whitespace actually separates the runs' combined
    ///     text. A word group that alone exceeds <paramref name="availableWidthEmu"/> is still
    ///     placed whole on its own (overflowing) line, per the same documented overflow policy as
    ///     any other oversized token.
    /// </summary>
    private static List<List<ResolvedToken>> PackTokensIntoLines(IReadOnlyList<ResolvedToken> tokens, float availableWidthEmu)
    {
        var lines = new List<List<ResolvedToken>>();
        var currentLine = new List<ResolvedToken>();
        var currentWidth = 0f;
        var sawAnyToken = false;

        var pendingGroup = new List<ResolvedToken>();
        var pendingGroupWidth = 0f;

        void FlushPendingGroup()
        {
            if (pendingGroup.Count == 0)
            {
                return;
            }

            Pack(pendingGroup, pendingGroupWidth);
            pendingGroup.Clear();
            pendingGroupWidth = 0f;
        }

        void Pack(IReadOnlyList<ResolvedToken> unit, float unitWidth)
        {
            // Only a currentLine already carrying at least one visible (non-whitespace) token
            // counts as "has content" for the wrap decision - a currentLine consisting solely of
            // a stranded whitespace token (for example the space between two words, where the
            // following word itself would overflow) must not be flushed as its own, visually
            // blank, line. Instead the whitespace is carried forward and merged onto the same
            // line as whatever comes next, matching standard word-processor wrap behavior (the
            // trailing space before a wrapped word is swallowed at the wrap point).
            var currentLineHasVisibleContent = currentLine.Any(static t => !t.IsWhitespace);
            if (currentLineHasVisibleContent && currentWidth + unitWidth > availableWidthEmu)
            {
                lines.Add(currentLine);
                currentLine = [];
                currentWidth = 0f;
            }

            currentLine.AddRange(unit);
            currentWidth += unitWidth;
        }

        foreach (var token in tokens)
        {
            sawAnyToken = true;

            if (token.IsLineBreak)
            {
                FlushPendingGroup();
                lines.Add(currentLine);
                currentLine = [];
                currentWidth = 0f;
                continue;
            }

            if (token.IsWhitespace)
            {
                FlushPendingGroup();

                // A tab token's effective width for the wrap/fit decision depends on its own
                // position in the line (Phase 2 Follow-Up: Default Tab-Stop Expansion) - compute
                // it dynamically from the running currentWidth at this point, rather than using
                // the token's static (unused, 0) WidthEmu, so an early tab's expanded width is
                // correctly accounted for when deciding where word-wrap breaks occur.
                var width = token.IsTab
                    ? GetNextTabStopEmu(currentWidth, DefaultTabStopEmu) - currentWidth
                    : token.WidthEmu;
                Pack([token], width);
                continue;
            }

            // Non-whitespace: accumulate into the pending word group rather than packing
            // immediately, so a word split across run boundaries is never wrapped mid-word.
            pendingGroup.Add(token);
            pendingGroupWidth += token.WidthEmu;
        }

        FlushPendingGroup();

        // The final (or only) line is always kept, even if empty - a trailing <a:br/> or a
        // paragraph consisting solely of break(s) still produces the trailing blank line(s) the
        // explicit break boundary implies. A paragraph with no tokens at all (no runs, no
        // breaks) intentionally yields no lines here - BuildLines supplies its own single blank
        // line for that case.
        if (currentLine.Count > 0 || sawAnyToken)
        {
            lines.Add(currentLine);
        }

        return lines;
    }

    /// <summary>
    ///     Positions every <see cref="LineBox"/> vertically (per <paramref name="anchor"/>) and
    ///     horizontally (per each line's own paragraph alignment/margin/indent), emitting the
    ///     final, shape-local-space <see cref="PptxGlyphPlacement"/> and
    ///     <see cref="PptxUnderlineSegment"/> streams. No clipping is applied - overflowing text
    ///     (under <c>noAutofit</c>/<c>spAutoFit</c>) is positioned exactly as computed, even past
    ///     the shape's own box, a documented limitation.
    /// </summary>
    private static (List<PptxGlyphPlacement> Glyphs, List<PptxUnderlineSegment> Underlines) PositionLines(
        IReadOnlyList<LineBox> lines,
        PptxTextAnchor anchor,
        float insetLeftEmu,
        float insetTopEmu,
        float availableWidthEmu,
        float availableHeightEmu)
    {
        var totalHeight = lines.Sum(line => line.LineHeightEmu + line.LeadingGapEmu);
        var startY = anchor switch
        {
            PptxTextAnchor.Middle => insetTopEmu + MathF.Max(0f, (availableHeightEmu - totalHeight) / 2f),
            PptxTextAnchor.Bottom => insetTopEmu + MathF.Max(0f, availableHeightEmu - totalHeight),
            _ => insetTopEmu,
        };

        var glyphs = new List<PptxGlyphPlacement>();
        var underlines = new List<PptxUnderlineSegment>();
        var runningY = startY;

        foreach (var line in lines)
        {
            var marginLeft = line.ParagraphProperties.MarginLeftEmu;

            // Hanging-indent fix (Phase 2 Follow-Up: Bullets and Numbering): once a bullet glyph
            // is painted, it alone occupies the indent gutter (marL+indent) - the paragraph's own
            // text, even on its first line, starts flush at marL. Without a bullet, "indent" is
            // applied to the first line's own text-X exactly as before (unaffected, pre-existing
            // first-line-indent behavior for non-bulleted paragraphs).
            var hasBullet = line.IsFirstLineOfParagraph && line.BulletGlyphs.Count > 0;
            var indent = line.IsFirstLineOfParagraph && !hasBullet ? line.ParagraphProperties.IndentEmu : 0f;
            var lineStartX = insetLeftEmu + marginLeft + indent;

            var startX = line.ParagraphProperties.Alignment switch
            {
                "ctr" => insetLeftEmu + marginLeft + MathF.Max(0f, (availableWidthEmu - marginLeft - line.LineWidthEmu) / 2f),
                "r" => insetLeftEmu + MathF.Max(marginLeft, availableWidthEmu - line.LineWidthEmu),
                _ => lineStartX,
            };

            if (hasBullet)
            {
                // Gutter-clearance fix: when the paragraph's resolved IndentEmu is zero or not
                // negative enough to clear the bullet glyph's own rendered width, the unclamped
                // text-start-X above would sit at or past the bullet's own gutter, overlapping it
                // (confirmed on a real-world buAutoNum paragraph with marL="320040", lvl="1", no
                // indent attribute - see the design document). Clamp the text's own start X so it
                // never shares an X range with the bullet - a no-op whenever the existing gutter
                // already clears the bullet's width (the already-correct, sufficiently-negative-
                // indent case). BulletTrailingGapEmu additionally reserves a minimum
                // PowerPoint-like, tab-stop-style gap beyond the bullet's own width for
                // PptxBulletKind.AutoNum markers only (0 for every other bullet kind - a no-op
                // there, see the design document's "buAutoNum gutter-clearance minimum gap" note).
                // The clamp is applied unconditionally, after startX is resolved for whichever
                // horizontal alignment the paragraph declares (left/center/right) - a bulleted
                // paragraph's own text must never overlap its bullet regardless of alignment.
                var bulletGutterXForClamp = insetLeftEmu + marginLeft + line.ParagraphProperties.IndentEmu;
                var minTextStartX = bulletGutterXForClamp + line.BulletWidthEmu + line.BulletTrailingGapEmu;
                startX = MathF.Max(startX, minTextStartX);
            }

            // Phase 2 Follow-Up: Paragraph Spacing - the leading gap is "space before the line's
            // own box": advance runningY past it before computing this line's baseline, so the
            // gap sits strictly between the previous line's own box and this one.
            runningY += line.LeadingGapEmu;
            var baselineY = runningY + line.AscentEmu;

            foreach (var glyph in line.Glyphs)
            {
                glyphs.Add(new PptxGlyphPlacement(glyph.Font, glyph.GlyphIndex, startX + glyph.XInLineEmu, baselineY, glyph.SizeEmu, glyph.Color));
            }

            foreach (var span in line.UnderlineSpans)
            {
                underlines.Add(new PptxUnderlineSegment(
                    startX + span.StartXEmu,
                    startX + span.EndXEmu,
                    baselineY,
                    span.RunProperties.SizeEmu,
                    span.RunProperties.UnderlineStyle,
                    span.RunProperties.UnderlineColor));
            }

            if (hasBullet)
            {
                // The bullet itself is always anchored at the gutter (marL+indent), independent
                // of the paragraph's own horizontal alignment - a documented, scoped limitation
                // (see the design document): bullets are not re-justified for ctr/r paragraphs.
                var bulletGutterX = insetLeftEmu + marginLeft + line.ParagraphProperties.IndentEmu;
                foreach (var glyph in line.BulletGlyphs)
                {
                    glyphs.Add(new PptxGlyphPlacement(glyph.Font, glyph.GlyphIndex, bulletGutterX + glyph.XInLineEmu, baselineY, glyph.SizeEmu, glyph.Color));
                }
            }

            runningY += line.LineHeightEmu;
        }

        return (glyphs, underlines);
    }
}

