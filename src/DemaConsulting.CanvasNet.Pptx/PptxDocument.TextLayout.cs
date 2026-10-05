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
    /// <returns>The resolved <see cref="PptxTextLayout"/>.</returns>
    internal static PptxTextLayout ResolveTextLayout(
        PptxTextBody textBody,
        PptxPlaceholderProperties placeholderProperties,
        PptxTheme theme,
        string placeholderType,
        float widthEmu,
        float heightEmu,
        Func<string, bool, bool, TrueTypeFont> fontResolver,
        PptxColorMap? colorMap = null)
    {
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
            ResolveFont);

        var lines = BuildLines(paragraphs, availableWidth, fontScale, lineSpacingFactor, ResolveFont);
        var glyphs = PositionLines(lines, bodyProperties.Anchor, insetLeft, insetTop, alignmentWidth, availableHeight);

        return new PptxTextLayout(glyphs, fontScale);
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
    private static (float FontScale, float LineSpacingFactor) ResolveAutofitScale(
        System.Xml.Linq.XElement? autofitElement,
        IReadOnlyList<ResolvedParagraph> paragraphs,
        float availableWidth,
        float availableHeight,
        Func<string, bool, bool, TrueTypeFont> resolveFont)
    {
        if (autofitElement is null || autofitElement.Name.LocalName is "noAutofit" or "spAutoFit")
        {
            return (1f, 1f);
        }

        var fontScaleAttribute = (float?)autofitElement.Attribute("fontScale");
        var lnSpcReductionAttribute = (float?)autofitElement.Attribute("lnSpcReduction");
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
            var lines = BuildLines(paragraphs, availableWidth, scale, 1f, resolveFont);
            var totalHeight = lines.Sum(line => line.LineHeightEmu);
            if (totalHeight <= availableHeight || scale <= MinShrinkScale)
            {
                return (scale, 1f);
            }

            scale = MathF.Max(MinShrinkScale, scale - ShrinkStep);
        }

        return (scale, 1f);
    }

    /// <summary>Tokenizes run text into whitespace/non-whitespace runs, preserving every consecutive-whitespace token's own advance width.</summary>
    private static List<(string Text, bool IsWhitespace)> Tokenize(string text)
    {
        var tokens = new List<(string Text, bool IsWhitespace)>();
        var start = 0;
        while (start < text.Length)
        {
            var isWhitespace = char.IsWhiteSpace(text[start]);
            var end = start + 1;
            while (end < text.Length && char.IsWhiteSpace(text[end]) == isWhitespace)
            {
                end++;
            }

            tokens.Add((text[start..end], isWhitespace));
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
    private readonly record struct ResolvedToken(
        string Text,
        bool IsWhitespace,
        PptxEffectiveRunProperties RunProperties,
        TrueTypeFont Font,
        float WidthEmu,
        bool IsLineBreak = false);

    /// <summary>A single laid-out glyph, positioned relative to its own line's start (alignment/margin not yet applied).</summary>
    private readonly record struct LineGlyph(TrueTypeFont Font, int GlyphIndex, float XInLineEmu, float SizeEmu, Rgba32 Color);

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
    private sealed record LineBox(
        IReadOnlyList<LineGlyph> Glyphs,
        float LineWidthEmu,
        float LineHeightEmu,
        float AscentEmu,
        PptxEffectiveParagraphProperties ParagraphProperties,
        bool IsFirstLineOfParagraph,
        IReadOnlyList<LineGlyph> BulletGlyphs,
        float BulletWidthEmu = 0f,
        float BulletTrailingGapEmu = 0f);

    /// <summary>A paragraph with its effective paragraph properties and every run's effective properties resolved up front.</summary>
    private sealed record ResolvedParagraph(
        PptxParagraph Paragraph,
        PptxEffectiveParagraphProperties ParagraphProperties,
        IReadOnlyList<PptxEffectiveRunProperties> RunProperties);

    /// <summary>Computes a resolved token's natural advance width, in EMU, summing each character's font-metric advance.</summary>
    private static float MeasureTokenWidthEmu(string text, TrueTypeFont font, float sizeEmu)
    {
        var total = 0f;
        foreach (var ch in text)
        {
            var glyphIndex = font.GetGlyphIndex(ch);
            total += font.GetAdvanceWidth(glyphIndex) / (float)font.UnitsPerEm * sizeEmu;
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
    private static List<LineBox> BuildLines(
        IReadOnlyList<ResolvedParagraph> paragraphs,
        float availableWidthEmu,
        float fontScale,
        float lineSpacingFactor,
        Func<string, bool, bool, TrueTypeFont> resolveFont)
    {
        var lines = new List<LineBox>();

        // Auto-number counter/last-type state (Phase 2 Follow-Up: Bullets and Numbering),
        // scoped per-call (one text body/shape) - see AdvanceBulletCounters' own remarks for the
        // full per-level sequencing/reset state machine this threads across the paragraph loop.
        var counters = new int[MaxParagraphLevel + 1];
        var lastTypes = new string?[MaxParagraphLevel + 1];

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

                foreach (var (text, isWhitespace) in Tokenize(run.Text))
                {
                    if (text.Length == 0)
                    {
                        continue;
                    }

                    var width = MeasureTokenWidthEmu(text, font, scaledSizeEmu);
                    tokens.Add(new ResolvedToken(text, isWhitespace, scaledRunProps, font, width));
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
                var cursorX = 0f;
                var lineWidth = 0f;
                TrueTypeFont? tallestFont = null;
                var tallestSize = 0f;
                var tallestNaturalHeight = -1f;

                foreach (var token in lineTokens)
                {
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

                    foreach (var ch in token.Text)
                    {
                        var glyphIndex = token.Font.GetGlyphIndex(ch);
                        var advance = token.Font.GetAdvanceWidth(glyphIndex) / (float)token.Font.UnitsPerEm * token.RunProperties.SizeEmu;
                        if (!token.IsWhitespace)
                        {
                            glyphs.Add(new LineGlyph(token.Font, glyphIndex, cursorX, token.RunProperties.SizeEmu, token.RunProperties.Color));
                        }

                        cursorX += advance;
                    }

                    lineWidth += token.WidthEmu;
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

                var isFirstLine = i == 0;
                var bulletGlyphsResult = isFirstLine && hasRuns
                    ? BuildBulletGlyphs(bulletText, paraProps.Bullet, fontScale, resolveFont)
                    : new BulletGlyphsResult([], 0f, 0f);

                lines.Add(new LineBox(glyphs, lineWidth, lineHeight, ascent, paraProps, isFirstLine, bulletGlyphsResult.Glyphs, bulletGlyphsResult.WidthEmu, bulletGlyphsResult.TrailingGapEmu));
            }
        }

        return lines;
    }

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
        Func<string, bool, bool, TrueTypeFont> resolveFont)
    {
        if (string.IsNullOrEmpty(bulletText) || bullet is null || bullet.Kind == PptxBulletKind.None)
        {
            return new BulletGlyphsResult([], 0f);
        }

        var font = resolveFont(bullet.FontFamily, false, false);
        var sizeEmu = bullet.SizeEmu * fontScale;
        var glyphs = new List<LineGlyph>();
        var cursorX = 0f;
        foreach (var ch in bulletText)
        {
            var glyphIndex = font.GetGlyphIndex(ch);
            var advance = font.GetAdvanceWidth(glyphIndex) / (float)font.UnitsPerEm * sizeEmu;
            glyphs.Add(new LineGlyph(font, glyphIndex, cursorX, sizeEmu, bullet.Color));
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
            if (currentLine.Count > 0 && currentWidth + unitWidth > availableWidthEmu)
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
                Pack([token], token.WidthEmu);
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
    ///     final, shape-local-space <see cref="PptxGlyphPlacement"/> stream. No clipping is
    ///     applied - overflowing text (under <c>noAutofit</c>/<c>spAutoFit</c>) is positioned
    ///     exactly as computed, even past the shape's own box, a documented limitation.
    /// </summary>
    private static List<PptxGlyphPlacement> PositionLines(
        IReadOnlyList<LineBox> lines,
        PptxTextAnchor anchor,
        float insetLeftEmu,
        float insetTopEmu,
        float availableWidthEmu,
        float availableHeightEmu)
    {
        var totalHeight = lines.Sum(line => line.LineHeightEmu);
        var startY = anchor switch
        {
            PptxTextAnchor.Middle => insetTopEmu + MathF.Max(0f, (availableHeightEmu - totalHeight) / 2f),
            PptxTextAnchor.Bottom => insetTopEmu + MathF.Max(0f, availableHeightEmu - totalHeight),
            _ => insetTopEmu,
        };

        var glyphs = new List<PptxGlyphPlacement>();
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
                var bulletGutterXForClamp = insetLeftEmu + marginLeft + line.ParagraphProperties.IndentEmu;
                lineStartX = MathF.Max(insetLeftEmu + marginLeft, bulletGutterXForClamp + line.BulletWidthEmu + line.BulletTrailingGapEmu);
            }

            var startX = line.ParagraphProperties.Alignment switch
            {
                "ctr" => insetLeftEmu + marginLeft + MathF.Max(0f, (availableWidthEmu - marginLeft - line.LineWidthEmu) / 2f),
                "r" => insetLeftEmu + MathF.Max(marginLeft, availableWidthEmu - line.LineWidthEmu),
                _ => lineStartX,
            };

            var baselineY = runningY + line.AscentEmu;

            foreach (var glyph in line.Glyphs)
            {
                glyphs.Add(new PptxGlyphPlacement(glyph.Font, glyph.GlyphIndex, startX + glyph.XInLineEmu, baselineY, glyph.SizeEmu, glyph.Color));
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

        return glyphs;
    }
}

