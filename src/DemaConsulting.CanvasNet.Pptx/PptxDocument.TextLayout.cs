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
        var glyphs = PositionLines(lines, bodyProperties.Anchor, insetLeft, insetTop, availableWidth, availableHeight);

        return new PptxTextLayout(glyphs, fontScale);
    }

    /// <summary>
    ///     Resolves the autofit font-scale/line-spacing-reduction factors to apply, per Design
    ///     Decision 4's three-tier policy: <c>noAutofit</c>/absent/<c>spAutoFit</c> apply no
    ///     scaling; an explicit <c>normAutofit fontScale="..."/lnSpcReduction="..."</c> is applied
    ///     verbatim; an attribute-less <c>normAutofit</c> runs a bounded 10%-step shrink loop,
    ///     stopping at the first scale whose natural layout height fits <paramref name="availableHeight"/>
    ///     or at the 10% floor, whichever comes first.
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
        if (fontScaleAttribute is { } storedFontScale && lnSpcReductionAttribute is { } storedReduction)
        {
            return (storedFontScale / 100000f, 1f - (storedReduction / 100000f));
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
    private readonly record struct ResolvedToken(
        string Text,
        bool IsWhitespace,
        PptxEffectiveRunProperties RunProperties,
        TrueTypeFont Font,
        float WidthEmu);

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

            // Build the paragraph's scaled token stream, resolving each run's font once.
            var tokens = new List<ResolvedToken>();
            for (var runIndex = 0; runIndex < paragraph.Runs.Count; runIndex++)
            {
                var run = paragraph.Runs[runIndex];
                var runProps = resolvedParagraph.RunProperties[runIndex];
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
                    var naturalHeight = token.Font.Ascender - token.Font.Descender + token.Font.LineGap;
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

    /// <summary>Greedily packs a paragraph's token stream into width-bounded lines, returning each line's own token list.</summary>
    private static List<List<ResolvedToken>> PackTokensIntoLines(IReadOnlyList<ResolvedToken> tokens, float availableWidthEmu)
    {
        var lines = new List<List<ResolvedToken>>();
        var currentLine = new List<ResolvedToken>();
        var currentWidth = 0f;

        foreach (var token in tokens)
        {
            if (currentLine.Count > 0 && currentWidth + token.WidthEmu > availableWidthEmu)
            {
                lines.Add(currentLine);
                currentLine = [];
                currentWidth = 0f;
            }

            currentLine.Add(token);
            currentWidth += token.WidthEmu;
        }

        if (currentLine.Count > 0)
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
