using System.Text;
using DemaConsulting.CanvasNet.Fonts;

namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio Rgba

/// <summary>
///     Implements the <see cref="VsdxDocument"/> text layout engine: a deliberately scaled-down
///     mirror of <c>PptxDocument.TextLayout.cs</c>'s own word-wrap/line-breaking/alignment
///     algorithm shape (tokenize -&gt; pack tokens into width-bounded lines -&gt; position by
///     horizontal/vertical alignment), trimmed of every concern explicitly out of this milestone's
///     scope (autofit shrink loop, bullets/numbering, tab-stops, underline spans - see the
///     originating plan report's Scope section). A literal <c>'\n'</c> character inside a run's
///     own text (format reference §8.1 - <c>xml:space="preserve"</c>, confirmed against
///     <c>test4_connectors_extracted</c>'s <c>&lt;Text&gt;Shape A\n&lt;/Text&gt;</c>) is VisioML's
///     own paragraph-break convention - there is no separate per-paragraph XML element the way
///     DrawingML's <c>&lt;a:p&gt;</c> is - so <see cref="SplitIntoParagraphs"/> treats every
///     <c>'\n'</c> as ending one paragraph and starting the next.
/// </summary>
public sealed partial class VsdxDocument
{
    /// <summary>The empty-paragraph fallback properties used for a paragraph group with no runs at all (for example a lone trailing <c>'\n'</c>'s own blank paragraph).</summary>
    private static readonly VsdxEffectiveParagraphProperties DefaultParagraphProperties = new(VsdxHorizontalAlign.Left, 0d, 0d, 0d, 0d, 0d, 0d);

    /// <summary>
    ///     Resolves a shape's full text layout: every run split into paragraphs at literal
    ///     <c>'\n'</c> boundaries, each paragraph word-wrapped into lines bounded by the resolved
    ///     text box's own width (less margins/indents), then positioned by the resolved vertical
    ///     anchor and each line's own paragraph horizontal alignment/indent.
    /// </summary>
    /// <param name="runs">The shape's fully resolved, effective text runs, in document order.</param>
    /// <param name="textBox">The shape's resolved text-box transform.</param>
    /// <param name="textBoxStyle">The shape's resolved flat <c>VerticalAlign</c>/margin cells.</param>
    /// <param name="fontResolver">
    ///     Resolves a <c>(familyName, bold, italic)</c> triple to a <see cref="TrueTypeFont"/> -
    ///     production callers supply <see cref="ResolveTextFont"/>; tests may inject a delegate
    ///     that bypasses installed-font discovery entirely for deterministic, machine-independent
    ///     assertions, mirroring <c>PptxDocument.ResolveTextLayout</c>'s own test seam.
    /// </param>
    /// <returns>The resolved <see cref="VsdxTextLayout"/>. Empty (no glyphs) when <paramref name="runs"/> is empty.</returns>
    internal static VsdxTextLayout ResolveTextLayout(
        IReadOnlyList<VsdxEffectiveTextRun> runs,
        VsdxTextBoxTransform textBox,
        VsdxEffectiveTextBoxStyle textBoxStyle,
        Func<string, bool, bool, TrueTypeFont> fontResolver)
    {
        if (runs.Count == 0)
        {
            return new VsdxTextLayout([]);
        }

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

        // Shape-local space is y-up, origin bottom-left (see VsdxShapeTransform's own remarks),
        // so the text box's own rectangle - and "top"/"bottom" vertical anchoring within it -
        // must be derived from TxtPinX/TxtPinY/TxtLocPinX/TxtLocPinY/TxtWidth/TxtHeight rather
        // than assumed to start at (0,0) the way DrawingML's own top-left-origin, y-down shape
        // space lets PptxDocument.TextLayout.cs do. TxtAngle is not applied (Milestone 7 - see
        // VsdxTextBoxTransform's own remarks).
        var boxLeft = textBox.TxtPinX - textBox.TxtLocPinX;
        var boxBottom = textBox.TxtPinY - textBox.TxtLocPinY;
        var boxTop = boxBottom + textBox.TxtHeight;

        var contentLeft = boxLeft + textBoxStyle.LeftMargin;
        var contentTop = boxTop - textBoxStyle.TopMargin;
        var contentBottom = boxBottom + textBoxStyle.BottomMargin;
        var availableWidth = Math.Max(0d, textBox.TxtWidth - textBoxStyle.LeftMargin - textBoxStyle.RightMargin);
        var availableHeight = Math.Max(0d, contentTop - contentBottom);

        var lines = new List<LineBox>();
        foreach (var paragraph in SplitIntoParagraphs(runs))
        {
            lines.AddRange(BuildParagraphLines(paragraph, availableWidth, ResolveFont));
        }

        return PositionLines(lines, textBoxStyle.VerticalAlign, contentLeft, contentTop, availableWidth, availableHeight);
    }

    /// <summary>
    ///     Splits the shape's runs into paragraph groups at every literal <c>'\n'</c> boundary
    ///     (VisioML's own paragraph-break convention - see this class's own remarks). A run whose
    ///     text contains no <c>'\n'</c> at all contributes its entire text to the current, still-
    ///     open paragraph group; a trailing <c>'\n'</c> (the common single-line-of-text case, per
    ///     <c>&lt;Text&gt;Shape A\n&lt;/Text&gt;</c>) yields a final, empty paragraph group - kept
    ///     deliberately (not discarded), mirroring <c>PptxDocument.PackTokensIntoLines</c>'s own
    ///     "trailing blank line is kept" convention for an explicit trailing break.
    /// </summary>
    private static List<List<(string Text, VsdxEffectiveTextRun Run)>> SplitIntoParagraphs(IReadOnlyList<VsdxEffectiveTextRun> runs)
    {
        var paragraphs = new List<List<(string Text, VsdxEffectiveTextRun Run)>>();
        var current = new List<(string Text, VsdxEffectiveTextRun Run)>();

        foreach (var run in runs)
        {
            var pieces = run.Text.Split('\n');
            for (var i = 0; i < pieces.Length; i++)
            {
                if (pieces[i].Length > 0)
                {
                    current.Add((pieces[i], run));
                }

                if (i < pieces.Length - 1)
                {
                    paragraphs.Add(current);
                    current = [];
                }
            }
        }

        paragraphs.Add(current);
        return paragraphs;
    }

    /// <summary>A single word-wrap token, resolved to its owning run's effective properties/font for width measurement and glyph emission.</summary>
    private readonly record struct ResolvedToken(string Text, bool IsWhitespace, VsdxEffectiveTextRun Run, TrueTypeFont Font, double WidthInches);

    /// <summary>A single laid-out glyph, positioned relative to its own line's start (alignment/indent not yet applied).</summary>
    private readonly record struct LineGlyph(TrueTypeFont Font, int GlyphIndex, double XInLineInches, double SizeInches, Canvas.Rgba32 Color);

    /// <summary>A single word-wrapped line, ready for alignment/vertical-anchor positioning.</summary>
    private sealed record LineBox(
        IReadOnlyList<LineGlyph> Glyphs,
        double LineWidthInches,
        double LineHeightInches,
        double AscentInches,
        VsdxEffectiveParagraphProperties ParagraphProperties,
        bool IsFirstLineOfParagraph,
        double LeadingGapInches);

    /// <summary>
    ///     Tokenizes a paragraph piece's text into whitespace/non-whitespace runs, each carrying
    ///     its own measured advance width - mirrors <c>PptxDocument.Tokenize</c>'s own shape,
    ///     trimmed of the tab-stop special case (no tab-stop concept exists for VisioML text,
    ///     out of scope for this milestone).
    /// </summary>
    private static List<ResolvedToken> Tokenize(string text, VsdxEffectiveTextRun run, TrueTypeFont font)
    {
        var tokens = new List<ResolvedToken>();
        var start = 0;
        while (start < text.Length)
        {
            var isWhitespace = char.IsWhiteSpace(text[start]);
            var end = start + 1;
            while (end < text.Length && char.IsWhiteSpace(text[end]) == isWhitespace)
            {
                end++;
            }

            var token = text[start..end];
            tokens.Add(new ResolvedToken(token, isWhitespace, run, font, MeasureTokenWidthInches(token, font, run.SizeInches)));
            start = end;
        }

        return tokens;
    }

    /// <summary>Measures a token's natural advance width, in inches, summing each Unicode scalar value's font-metric advance.</summary>
    private static double MeasureTokenWidthInches(string text, TrueTypeFont font, double sizeInches)
    {
        var total = 0d;
        foreach (var rune in text.EnumerateRunes())
        {
            var glyphIndex = font.GetGlyphIndex(rune.Value);
            total += font.GetAdvanceWidth(glyphIndex) / (double)font.UnitsPerEm * sizeInches;
        }

        return total;
    }

    /// <summary>
    ///     Word-wraps a single paragraph's tokens into width-bounded lines - mirrors
    ///     <c>PptxDocument.PackTokensIntoLines</c>'s own word-group atomicity rule (consecutive
    ///     non-whitespace tokens never wrap mid-word, even across a run boundary with no
    ///     intervening whitespace) and overflow policy (a token/word-group alone exceeding
    ///     <paramref name="availableWidthInches"/> is still placed whole on its own, overflowing
    ///     line, rather than looping indefinitely).
    /// </summary>
    private static List<List<ResolvedToken>> PackTokensIntoLines(IReadOnlyList<ResolvedToken> tokens, double availableWidthInches)
    {
        var lines = new List<List<ResolvedToken>>();
        var currentLine = new List<ResolvedToken>();
        var currentWidth = 0d;
        var sawAnyToken = false;

        var pendingGroup = new List<ResolvedToken>();
        var pendingGroupWidth = 0d;

        void FlushPendingGroup()
        {
            if (pendingGroup.Count == 0)
            {
                return;
            }

            Pack(pendingGroup, pendingGroupWidth);
            pendingGroup.Clear();
            pendingGroupWidth = 0d;
        }

        void Pack(IReadOnlyList<ResolvedToken> unit, double unitWidth)
        {
            var currentLineHasVisibleContent = currentLine.Any(static t => !t.IsWhitespace);
            if (currentLineHasVisibleContent && currentWidth + unitWidth > availableWidthInches)
            {
                lines.Add(currentLine);
                currentLine = [];
                currentWidth = 0d;
            }

            currentLine.AddRange(unit);
            currentWidth += unitWidth;
        }

        foreach (var token in tokens)
        {
            sawAnyToken = true;

            if (token.IsWhitespace)
            {
                FlushPendingGroup();
                Pack([token], token.WidthInches);
                continue;
            }

            pendingGroup.Add(token);
            pendingGroupWidth += token.WidthInches;
        }

        FlushPendingGroup();
        if (currentLine.Count > 0 || sawAnyToken)
        {
            lines.Add(currentLine);
        }

        return lines;
    }

    /// <summary>
    ///     Tokenizes and word-wraps a single paragraph group into its own <see cref="LineBox"/>
    ///     lines. An empty paragraph group (no runs at all - the common trailing blank paragraph
    ///     after a shape's text's own trailing <c>'\n'</c>) still yields exactly one empty line,
    ///     so it occupies vertical space the same way a blank line in any ordinary text editor
    ///     does, using the paragraph's own representative font metrics (its first run's, when
    ///     any; otherwise the neutral default font/size) for that line's height.
    /// </summary>
    private static List<LineBox> BuildParagraphLines(
        List<(string Text, VsdxEffectiveTextRun Run)> paragraphPieces,
        double availableWidthInches,
        Func<string, bool, bool, TrueTypeFont> resolveFont)
    {
        var paragraphProperties = paragraphPieces.Count > 0 ? paragraphPieces[0].Run.Paragraph : DefaultParagraphProperties;
        var effectiveWidth = Math.Max(0d, availableWidthInches - paragraphProperties.IndLeft - paragraphProperties.IndRight);

        var tokens = new List<ResolvedToken>();
        foreach (var (text, run) in paragraphPieces)
        {
            tokens.AddRange(Tokenize(text, run, resolveFont(run.FontFamily, run.Bold, run.Italic)));
        }

        var wrappedLines = PackTokensIntoLines(tokens, effectiveWidth);
        if (wrappedLines.Count == 0)
        {
            wrappedLines.Add([]);
        }

        var representativeSize = paragraphPieces.Count > 0 ? paragraphPieces[0].Run.SizeInches : DefaultFontSizeInches;
        var representativeFont = resolveFont(
            paragraphPieces.Count > 0 ? paragraphPieces[0].Run.FontFamily : DefaultFontFamily,
            paragraphPieces.Count > 0 && paragraphPieces[0].Run.Bold,
            paragraphPieces.Count > 0 && paragraphPieces[0].Run.Italic);

        var lines = new List<LineBox>();
        for (var lineIndex = 0; lineIndex < wrappedLines.Count; lineIndex++)
        {
            var lineTokens = wrappedLines[lineIndex];
            var isFirstLine = lineIndex == 0;
            var isLastLine = lineIndex == wrappedLines.Count - 1;

            var lineGlyphs = new List<LineGlyph>();
            var x = isFirstLine ? paragraphProperties.IndFirst : 0d;
            var maxAscent = 0d;
            var maxDescent = 0d;

            foreach (var token in lineTokens)
            {
                var ascent = token.Font.Ascender / (double)token.Font.UnitsPerEm * token.Run.SizeInches;
                var descent = -token.Font.Descender / (double)token.Font.UnitsPerEm * token.Run.SizeInches;
                maxAscent = Math.Max(maxAscent, ascent);
                maxDescent = Math.Max(maxDescent, descent);

                if (!token.IsWhitespace)
                {
                    foreach (var rune in token.Text.EnumerateRunes())
                    {
                        var glyphIndex = token.Font.GetGlyphIndex(rune.Value);
                        lineGlyphs.Add(new LineGlyph(token.Font, glyphIndex, x, token.Run.SizeInches, token.Run.Color));
                        x += token.Font.GetAdvanceWidth(glyphIndex) / (double)token.Font.UnitsPerEm * token.Run.SizeInches;
                    }
                }
                else
                {
                    x += token.WidthInches;
                }
            }

            if (maxAscent == 0d && maxDescent == 0d)
            {
                // A blank line (no tokens at all): fall back to the paragraph's own representative
                // font metrics so it still occupies the natural single-spacing vertical space a
                // blank line in any ordinary text editor would.
                maxAscent = representativeFont.Ascender / (double)representativeFont.UnitsPerEm * representativeSize;
                maxDescent = -representativeFont.Descender / (double)representativeFont.UnitsPerEm * representativeSize;
            }

            var naturalLineHeight = maxAscent + maxDescent;
            var scaledLineHeight = paragraphProperties.SpLine switch
            {
                > 0d => Math.Max(naturalLineHeight, paragraphProperties.SpLine),
                < 0d => naturalLineHeight * -paragraphProperties.SpLine,
                _ => naturalLineHeight,
            };

            var leadingGap = isFirstLine ? paragraphProperties.SpBefore : 0d;
            var trailingGap = isLastLine ? paragraphProperties.SpAfter : 0d;

            lines.Add(new LineBox(lineGlyphs, x, scaledLineHeight + trailingGap, maxAscent, paragraphProperties, isFirstLine, leadingGap));
        }

        return lines;
    }

    /// <summary>
    ///     Positions every <see cref="LineBox"/> vertically (per <paramref name="verticalAlign"/>)
    ///     and horizontally (per each line's own paragraph alignment/indent), emitting the final,
    ///     shape-local-space <see cref="VsdxGlyphPlacement"/> stream. No clipping is applied -
    ///     overflowing text is positioned exactly as computed, even past the text box's own
    ///     bounds, the same documented limitation <c>PptxDocument.PositionLines</c> carries.
    /// </summary>
    private static VsdxTextLayout PositionLines(
        IReadOnlyList<LineBox> lines,
        VsdxVerticalAlign verticalAlign,
        double contentLeftInches,
        double contentTopInches,
        double availableWidthInches,
        double availableHeightInches)
    {
        var totalHeight = lines.Sum(line => line.LineHeightInches + line.LeadingGapInches);

        // Shape-local space is y-up: moving "down" through the lines means subtracting from the
        // running Y, the mirror image of PptxDocument.PositionLines's own y-down "add to runningY"
        // convention - see ResolveTextLayout's own remarks for why this box's top/bottom are
        // derived explicitly rather than assumed.
        var startTopY = verticalAlign switch
        {
            VsdxVerticalAlign.Middle => contentTopInches - Math.Max(0d, (availableHeightInches - totalHeight) / 2d),
            VsdxVerticalAlign.Bottom => contentTopInches - Math.Max(0d, availableHeightInches - totalHeight),
            _ => contentTopInches,
        };

        var glyphs = new List<VsdxGlyphPlacement>();
        var runningTopY = startTopY;

        foreach (var line in lines)
        {
            runningTopY -= line.LeadingGapInches;
            var baselineY = runningTopY - line.AscentInches;

            var startX = line.ParagraphProperties.Align switch
            {
                VsdxHorizontalAlign.Center => contentLeftInches + line.ParagraphProperties.IndLeft +
                    Math.Max(0d, (availableWidthInches - line.ParagraphProperties.IndLeft - line.ParagraphProperties.IndRight - line.LineWidthInches) / 2d),
                _ => contentLeftInches + line.ParagraphProperties.IndLeft,
            };

            foreach (var glyph in line.Glyphs)
            {
                glyphs.Add(new VsdxGlyphPlacement(glyph.Font, glyph.GlyphIndex, startX + glyph.XInLineInches, baselineY, glyph.SizeInches, glyph.Color));
            }

            runningTopY -= line.LineHeightInches;
        }

        return new VsdxTextLayout(glyphs);
    }
}
