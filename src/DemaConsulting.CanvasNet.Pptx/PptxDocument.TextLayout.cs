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
    /// <returns>The resolved <see cref="PptxTextLayout"/>.</returns>
    internal static PptxTextLayout ResolveTextLayout(
        PptxTextBody textBody,
        PptxPlaceholderProperties placeholderProperties,
        PptxTheme theme,
        string placeholderType,
        float widthEmu,
        float heightEmu,
        Func<string, bool, bool, TrueTypeFont> fontResolver)
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
            .Select(paragraph => new ResolvedParagraph(
                paragraph,
                ResolveEffectiveParagraphProperties(paragraph, placeholderProperties, placeholderType),
                paragraph.Runs.Select(run => ResolveEffectiveRunProperties(run, paragraph, placeholderProperties, theme, placeholderType)).ToList()))
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
    private sealed record LineBox(
        IReadOnlyList<LineGlyph> Glyphs,
        float LineWidthEmu,
        float LineHeightEmu,
        float AscentEmu,
        PptxEffectiveParagraphProperties ParagraphProperties,
        bool IsFirstLineOfParagraph);

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

        foreach (var resolvedParagraph in paragraphs)
        {
            var paragraph = resolvedParagraph.Paragraph;
            var paraProps = resolvedParagraph.ParagraphProperties;

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

                lines.Add(new LineBox(glyphs, lineWidth, lineHeight, ascent, paraProps, i == 0));
            }
        }

        return lines;
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
            var indent = line.IsFirstLineOfParagraph ? line.ParagraphProperties.IndentEmu : 0f;
            var lineStartX = insetLeftEmu + marginLeft + indent;

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

            runningY += line.LineHeightEmu;
        }

        return glyphs;
    }
}
