using System.Text;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Rendering;

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
    /// <param name="glyphFallbackResolver">
    ///     Optional per-glyph fallback: given a code point the run's font lacks (plus the run's
    ///     bold/italic), returns a font that has it, or <see langword="null"/> when none does.
    /// </param>
    /// <returns>The resolved <see cref="VsdxTextLayout"/>. Empty (no glyphs) when <paramref name="runs"/> is empty.</returns>
    internal static VsdxTextLayout ResolveTextLayout(
        IReadOnlyList<VsdxEffectiveTextRun> runs,
        VsdxTextBoxTransform textBox,
        VsdxEffectiveTextBoxStyle textBoxStyle,
        Func<string, bool, bool, TrueTypeFont> fontResolver,
        Func<int, bool, bool, TrueTypeFont?>? glyphFallbackResolver = null)
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
        // space lets PptxDocument.TextLayout.cs do. TxtAngle is deliberately not applied here:
        // every glyph position below is computed within the text box's own unrotated rectangle,
        // and the box's TxtAngle rotation (about its own TxtPinX/TxtPinY pin) is instead composed
        // into the transform used when painting - see VsdxDocument.Render.cs's
        // ResolveTextToPixelTransform - so this resolver's own output never needs revisiting
        // for a non-zero TxtAngle.
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
            lines.AddRange(BuildParagraphLines(paragraph, availableWidth, ResolveFont, glyphFallbackResolver));
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

    /// <summary>A decoration bar in line-relative coordinates: X range, the decoration kind, and the em size/color/ascent of the run it belongs to.</summary>
    private readonly record struct LineBar(double X0, double X1, VsdxTextDecoration Kind, double SizeInches, Canvas.Rgba32 Color, double AscentEm);

    /// <summary>A single word-wrapped line, ready for alignment/vertical-anchor positioning.</summary>
    private sealed record LineBox(
        IReadOnlyList<LineGlyph> Glyphs,
        double LineWidthInches,
        double LineHeightInches,
        double AscentInches,
        VsdxEffectiveParagraphProperties ParagraphProperties,
        bool IsFirstLineOfParagraph,
        double LeadingGapInches,
        IReadOnlyList<LineBar> Bars);

    /// <summary>
    ///     Tokenizes a paragraph piece's text into whitespace/non-whitespace runs, each carrying
    ///     its own measured advance width - mirrors <c>PptxDocument.Tokenize</c>'s own shape,
    ///     trimmed of the tab-stop special case (no tab-stop concept exists for VisioML text,
    ///     out of scope for this milestone).
    /// </summary>
    private static List<ResolvedToken> Tokenize(
        string text,
        VsdxEffectiveTextRun run,
        TrueTypeFont font,
        Func<int, bool, bool, TrueTypeFont?>? fallback)
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
            tokens.Add(new ResolvedToken(token, isWhitespace, run, font, MeasureTokenWidthInches(token, font, run, isWhitespace ? null : fallback)));
            start = end;
        }

        return tokens;
    }

    /// <summary>Measures a token's natural advance width, in inches, summing each Unicode scalar value's font-metric advance.</summary>
    private static double MeasureTokenWidthInches(
        string text,
        TrueTypeFont font,
        VsdxEffectiveTextRun run,
        Func<int, bool, bool, TrueTypeFont?>? fallback)
    {
        var total = 0d;
        foreach (var rune in text.EnumerateRunes())
        {
            var glyphFont = SelectGlyphFont(font, rune, run, fallback, out var glyphIndex);
            total += glyphFont.GetAdvanceWidth(glyphIndex) / (double)glyphFont.UnitsPerEm * run.SizeInches;
        }

        return total;
    }

    /// <summary>
    ///     Picks the font used to draw one code point: the run's own font when it has a glyph for
    ///     it, otherwise the per-glyph fallback font (when the fallback supplies one), otherwise
    ///     the run's font again (drawing its missing-glyph outline). Control and format characters
    ///     never trigger fallback.
    /// </summary>
    private static TrueTypeFont SelectGlyphFont(
        TrueTypeFont primary,
        System.Text.Rune rune,
        VsdxEffectiveTextRun run,
        Func<int, bool, bool, TrueTypeFont?>? fallback,
        out int glyphIndex)
    {
        glyphIndex = primary.GetGlyphIndex(rune.Value);
        if (glyphIndex != 0 || fallback is null || Rune.IsControl(rune) || Rune.IsWhiteSpace(rune) ||
            Rune.GetUnicodeCategory(rune) == System.Globalization.UnicodeCategory.Format)
        {
            return primary;
        }

        var alternate = fallback(rune.Value, run.Bold, run.Italic);
        if (alternate is null)
        {
            ReportUnsupported(
                "vsdx-text-missing-glyph",
                $"No installed font has a glyph for U+{rune.Value:X4}; the font's missing-glyph box is drawn instead.");
            return primary;
        }

        glyphIndex = alternate.GetGlyphIndex(rune.Value);
        return alternate;
    }

    /// <summary>
    ///     Word-wraps a single paragraph's tokens into width-bounded lines with the core
    ///     <see cref="WordWrap"/> packer - the same word-group atomicity rule as
    ///     <c>PptxDocument.PackTokensIntoLines</c> (consecutive non-whitespace tokens never wrap
    ///     mid-word, even across a run boundary with no intervening whitespace) and overflow policy
    ///     (a token/word-group alone exceeding <paramref name="availableWidthInches"/> is still
    ///     placed whole on its own, overflowing line, rather than looping indefinitely). VisioML
    ///     has no explicit line-break or tab tokens here, so every token is a word or whitespace
    ///     of fixed width.
    /// </summary>
    private static List<List<ResolvedToken>> PackTokensIntoLines(IReadOnlyList<ResolvedToken> tokens, double availableWidthInches) =>
        WordWrap.Pack(
            tokens,
            availableWidthInches,
            static token => token.IsWhitespace ? WordWrapTokenKind.Whitespace : WordWrapTokenKind.Word,
            static (token, _) => token.WidthInches);

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
        Func<string, bool, bool, TrueTypeFont> resolveFont,
        Func<int, bool, bool, TrueTypeFont?>? glyphFallback)
    {
        var paragraphProperties = paragraphPieces.Count > 0 ? paragraphPieces[0].Run.Paragraph : DefaultParagraphProperties;
        var effectiveWidth = Math.Max(0d, availableWidthInches - paragraphProperties.IndLeft - paragraphProperties.IndRight);

        var tokens = new List<ResolvedToken>();
        foreach (var (text, run) in paragraphPieces)
        {
            tokens.AddRange(Tokenize(text, run, resolveFont(run.FontFamily, run.Bold, run.Italic), glyphFallback));
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
            lines.Add(BuildLineBox(
                wrappedLines[lineIndex],
                lineIndex == 0,
                lineIndex == wrappedLines.Count - 1,
                paragraphProperties,
                (representativeFont, representativeSize),
                glyphFallback));
        }

        return lines;
    }

    /// <summary>Lays out one wrapped line's tokens into a <see cref="LineBox"/>.</summary>
    /// <param name="lineTokens">The tokens on this line.</param>
    /// <param name="isFirstLine">Whether this is the paragraph's first line.</param>
    /// <param name="isLastLine">Whether this is the paragraph's last line.</param>
    /// <param name="paragraphProperties">The paragraph's effective properties.</param>
    /// <param name="representative">The paragraph's representative font and size, used for blank lines.</param>
    /// <param name="glyphFallback">The optional per-glyph fallback font resolver.</param>
    /// <returns>The laid-out line.</returns>
    private static LineBox BuildLineBox(
        List<ResolvedToken> lineTokens,
        bool isFirstLine,
        bool isLastLine,
        VsdxEffectiveParagraphProperties paragraphProperties,
        (TrueTypeFont Font, double SizeInches) representative,
        Func<int, bool, bool, TrueTypeFont?>? glyphFallback)
    {
        var lineGlyphs = new List<LineGlyph>();
        var lineBars = new List<LineBar>();
        var x = isFirstLine ? paragraphProperties.IndFirst : 0d;
        var maxAscent = 0d;
        var maxDescent = 0d;

        foreach (var token in lineTokens)
        {
            var tokenStartX = x;
            var ascent = token.Font.Ascender / (double)token.Font.UnitsPerEm * token.Run.SizeInches;
            var descent = -token.Font.Descender / (double)token.Font.UnitsPerEm * token.Run.SizeInches;
            maxAscent = Math.Max(maxAscent, ascent);
            maxDescent = Math.Max(maxDescent, descent);

            x = AppendTokenGlyphs(token, x, lineGlyphs, glyphFallback);

            if (token.Run.Decoration != VsdxTextDecoration.None)
            {
                AddDecorationBars(lineBars, token, tokenStartX, x);
            }
        }

        if (maxAscent.Equals(0d) && maxDescent.Equals(0d))
        {
            // A blank line (no tokens at all): fall back to the paragraph's own representative
            // font metrics so it still occupies the natural single-spacing vertical space a
            // blank line in any ordinary text editor would.
            maxAscent = representative.Font.Ascender / (double)representative.Font.UnitsPerEm * representative.SizeInches;
            maxDescent = -representative.Font.Descender / (double)representative.Font.UnitsPerEm * representative.SizeInches;
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

        return new LineBox(lineGlyphs, x, scaledLineHeight + trailingGap, maxAscent, paragraphProperties, isFirstLine, leadingGap, lineBars);
    }

    /// <summary>Appends a token's glyphs to the line (or just advances over whitespace).</summary>
    /// <param name="token">The token to place.</param>
    /// <param name="x">The pen position before the token, in inches.</param>
    /// <param name="lineGlyphs">The line's glyph list to append to.</param>
    /// <param name="glyphFallback">The optional per-glyph fallback font resolver.</param>
    /// <returns>The pen position after the token, in inches.</returns>
    private static double AppendTokenGlyphs(
        ResolvedToken token,
        double x,
        List<LineGlyph> lineGlyphs,
        Func<int, bool, bool, TrueTypeFont?>? glyphFallback)
    {
        if (token.IsWhitespace)
        {
            return x + token.WidthInches;
        }

        foreach (var rune in token.Text.EnumerateRunes())
        {
            var glyphFont = SelectGlyphFont(token.Font, rune, token.Run, glyphFallback, out var glyphIndex);
            lineGlyphs.Add(new LineGlyph(glyphFont, glyphIndex, x, token.Run.SizeInches, token.Run.Color));
            x += glyphFont.GetAdvanceWidth(glyphIndex) / (double)glyphFont.UnitsPerEm * token.Run.SizeInches;
        }

        return x;
    }

    private static readonly VsdxTextDecoration[] DecorationKinds =
    [
        VsdxTextDecoration.Underline,
        VsdxTextDecoration.Strikethrough,
        VsdxTextDecoration.DoubleStrikethrough,
        VsdxTextDecoration.Overline,
    ];

    /// <summary>Adds (or extends) the decoration bars a token's run requests.</summary>
    /// <param name="lineBars">The line's bar list.</param>
    /// <param name="token">The token being decorated.</param>
    /// <param name="tokenStartX">The token's start position, in inches.</param>
    /// <param name="tokenEndX">The token's end position, in inches.</param>
    private static void AddDecorationBars(List<LineBar> lineBars, ResolvedToken token, double tokenStartX, double tokenEndX)
    {
        var ascentEm = token.Font.Ascender / (double)token.Font.UnitsPerEm;
        foreach (var kind in DecorationKinds.Where(k => (token.Run.Decoration & k) != 0))
        {
            // Extend this kind's open bar when it has the same color and abuts this
            // token, so a decoration spanning several tokens is one unbroken bar. Each
            // decoration kind tracks its own open bar (searching backward), so several
            // simultaneous decorations all merge; differing sizes (small caps) merge
            // too, keeping the larger size so the bar sits at the nominal position.
            var open = lineBars.FindLastIndex(bar => bar.Kind == kind);

            if (open >= 0 && lineBars[open].Color == token.Run.Color && Math.Abs(lineBars[open].X1 - tokenStartX) < 1e-9)
            {
                lineBars[open] = lineBars[open] with
                {
                    X1 = tokenEndX,
                    SizeInches = Math.Max(lineBars[open].SizeInches, token.Run.SizeInches),
                };
            }
            else
            {
                lineBars.Add(new LineBar(tokenStartX, tokenEndX, kind, token.Run.SizeInches, token.Run.Color, ascentEm));
            }
        }
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
        var bars = new List<VsdxDecorationBar>();
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

            foreach (var bar in line.Bars)
            {
                var thickness = Math.Max(bar.SizeInches * 0.05d, 0.004d);
                var centerY = bar.Kind switch
                {
                    VsdxTextDecoration.Underline => baselineY - bar.SizeInches * 0.12d,
                    VsdxTextDecoration.Strikethrough => baselineY + bar.SizeInches * 0.3d,
                    VsdxTextDecoration.DoubleStrikethrough => baselineY + bar.SizeInches * 0.3d,
                    _ => baselineY + bar.SizeInches * bar.AscentEm + thickness,
                };

                if (bar.Kind == VsdxTextDecoration.DoubleStrikethrough)
                {
                    var offset = bar.SizeInches * 0.06d;
                    bars.Add(new VsdxDecorationBar(startX + bar.X0, startX + bar.X1, centerY + offset, thickness, bar.Color));
                    bars.Add(new VsdxDecorationBar(startX + bar.X0, startX + bar.X1, centerY - offset, thickness, bar.Color));
                }
                else
                {
                    bars.Add(new VsdxDecorationBar(startX + bar.X0, startX + bar.X1, centerY, thickness, bar.Color));
                }
            }

            runningTopY -= line.LineHeightInches;
        }

        return new VsdxTextLayout(glyphs, bars);
    }
}
