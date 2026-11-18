using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using static DemaConsulting.CanvasNet.Svg.Tests.SvgLengthTestSupport;

namespace DemaConsulting.CanvasNet.Svg.Tests;

/// <summary>
///     Tests of the <c>text</c> layout of <see cref="SvgCodec"/>: <c>tspan</c> and <c>a</c> runs with their own styles
///     and positions, text chunks and their anchors, position lists, white space, <c>baseline-shift</c>, and the
///     <c>a</c> and <c>switch</c> containers.
/// </summary>
/// <remarks>
///     The synthetic square font of <see cref="SvgLengthTestSupport"/> makes positions exact: at font size <c>S</c> a
///     glyph <c>A</c> is an <c>S/2</c> square that sits on the baseline at the pen position and advances the pen by
///     <c>S</c>. Its ascent is the whole em and its descent 0. The layouts the tests expect were cross-checked against
///     Microsoft Edge with a real font.
/// </remarks>
public class SvgTextLayoutTests
{
    /// <summary>
    ///     Wraps markup in a 200 by 100 document whose text defaults to the 20 px synthetic font.
    /// </summary>
    /// <param name="inner">The markup inside the root element.</param>
    /// <returns>The document.</returns>
    private static string Doc(string inner) =>
        "<svg xmlns='http://www.w3.org/2000/svg' xmlns:xlink='http://www.w3.org/1999/xlink' width='200' height='100' " +
        $"font-family='TestFont' font-size='20'>{inner}</svg>";

    /// <summary>
    ///     Determines whether a pixel is covered (more than half opaque).
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns><see langword="true"/> when the pixel is inked.</returns>
    private static bool Inked(Surface surface, int x, int y) => surface[x, y].A > 128;

    /// <summary>
    ///     Asserts that a pixel matches an expected color within 2 per channel, allowing for rounding.
    /// </summary>
    /// <param name="expected">The expected color.</param>
    /// <param name="actual">The actual pixel.</param>
    private static void AssertNear(Rgba32 expected, Rgba32 actual) =>
        Assert.True(
            Math.Abs(expected.R - actual.R) <= 2 && Math.Abs(expected.G - actual.G) <= 2 &&
            Math.Abs(expected.B - actual.B) <= 2 && Math.Abs(expected.A - actual.A) <= 2,
            $"expected about {expected}, got {actual}");

    /// <summary>
    ///     Finds the first and last inked columns of a row.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="y">The row.</param>
    /// <returns>The inked column ranges, left to right.</returns>
    private static List<(int Start, int End)> InkRuns(Surface surface, int y)
    {
        var runs = new List<(int Start, int End)>();
        var start = -1;
        for (var x = 0; x <= surface.Width; x++)
        {
            var inked = x < surface.Width && Inked(surface, x, y);
            if (inked && start < 0)
            {
                start = x;
            }
            else if (!inked && start >= 0)
            {
                runs.Add((start, x - 1));
                start = -1;
            }
        }

        return runs;
    }

    /// <summary>
    ///     Renders two documents and determines whether they draw the same pixels.
    /// </summary>
    /// <param name="left">The first document.</param>
    /// <param name="right">The second document.</param>
    /// <returns><see langword="true"/> when both are pixel-identical.</returns>
    private static bool DrawSame(string left, string right)
    {
        using var a = Render(left, 200, 100);
        using var b = Render(right, 200, 100);
        return AreIdentical(a, b);
    }

    /// <summary>
    ///     Renders a markup and finds the first inked column of its last glyph on row 45.
    /// </summary>
    /// <param name="markup">The markup inside the root element.</param>
    /// <returns>The column where the last ink run of row 45 starts.</returns>
    private static int LastGlyphStart(string markup)
    {
        using var surface = Render(Doc(markup), 200, 100);
        return InkRuns(surface, 45)[^1].Start;
    }

    /// <summary>
    ///     Test that a <c>tspan</c> with its own <c>fill</c> paints its characters in that color and the text after it
    ///     in the text's color.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TspanFill_PaintsItsRunInItsOwnColor()
    {
        // Act
        using var surface = Render(Doc("<text x='10' y='50' fill='#000000'>A<tspan fill='#ff0000'>A</tspan>A</text>"), 200, 100);

        // Assert: glyphs at 10, 30 and 50, ten pixels wide, the middle one red
        Assert.Equal([(10, 19), (30, 39), (50, 59)], InkRuns(surface, 45));
        Assert.Equal(new Rgba32(0, 0, 0, 255), surface[15, 45]);
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[35, 45]);
        Assert.Equal(new Rgba32(0, 0, 0, 255), surface[55, 45]);
    }

    /// <summary>
    ///     Test that a <c>tspan</c> with its own <c>font-size</c> draws its glyphs at that size and advances the pen by
    ///     its own advance.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TspanFontSize_ScalesItsRunAndAdvancesThePen()
    {
        // Act
        using var surface = Render(Doc("<text x='10' y='50'>A<tspan font-size='40'>A</tspan>A</text>"), 200, 100);

        // Assert: the 40 px glyph is a 20 px square at 30, so the third glyph starts at 30 + 40
        Assert.Equal([(10, 19), (30, 49), (70, 79)], InkRuns(surface, 45));
        Assert.Equal([(30, 49)], InkRuns(surface, 32));
    }

    /// <summary>
    ///     Test that a bold <c>tspan</c> uses the bold face of the family.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TspanFontWeightBold_UsesTheBoldFace()
    {
        // Act: the bold face's glyph is 80 font units wide, 16 px at 20 px
        using var surface = RenderWithFaces(Doc("<text x='10' y='50'>A<tspan font-weight='bold'>A</tspan>A</text>"), 200, 100);

        // Assert
        Assert.Equal([(10, 19), (30, 45), (50, 59)], InkRuns(surface, 45));
    }

    /// <summary>
    ///     Test that an italic <c>tspan</c> uses the italic face of the family.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TspanFontStyleItalic_UsesTheItalicFace()
    {
        // Act: the italic face's glyph is 65 font units wide, 13 px at 20 px
        using var surface = RenderWithStyledFaces(Doc("<text x='10' y='50'>A<tspan font-style='italic'>A</tspan></text>"), 200, 100);

        // Assert
        Assert.Equal([(10, 19), (30, 42)], InkRuns(surface, 45));
    }

    /// <summary>
    ///     Test that a stylesheet class, a descendant selector and an inline <c>style</c> style a <c>tspan</c> through
    ///     the cascade.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TspanClassAndStyle_AreStyledByTheCascade()
    {
        // Arrange
        var svg = Doc(
            "<style>.hl { fill: #ff0000 } text tspan.b { fill: #0000ff }</style>" +
            "<text x='10' y='50'>A<tspan class='hl'>A</tspan><tspan class='b'>A</tspan><tspan style='fill:#00ff00'>A</tspan></text>");

        // Act
        using var surface = Render(svg, 200, 100);

        // Assert
        Assert.Equal(new Rgba32(0, 0, 0, 255), surface[15, 45]);
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[35, 45]);
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[55, 45]);
        Assert.Equal(new Rgba32(0, 255, 0, 255), surface[75, 45]);
    }

    /// <summary>
    ///     Test that <c>dx</c>/<c>dy</c> on a <c>tspan</c> shift its run, and that the <c>dy</c> shift persists for the
    ///     text after it.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TspanDxDy_ShiftsTheRunAndTheFollowingText()
    {
        // Act
        using var surface = Render(Doc("<text x='10' y='50'>A<tspan dx='10' dy='20'>A</tspan>A</text>"), 200, 100);

        // Assert: the second glyph moves right 10 and down 20, the third follows it on the new baseline
        Assert.Equal([(10, 19)], InkRuns(surface, 45));
        Assert.Equal([(40, 49), (60, 69)], InkRuns(surface, 65));
    }

    /// <summary>
    ///     Test that an absolute <c>x</c>/<c>y</c> on a <c>tspan</c> starts a new text chunk at that position.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TspanAbsoluteXY_StartsANewChunk()
    {
        // Act
        using var surface = Render(Doc("<text x='10' y='30'>A<tspan x='100' y='70'>AA</tspan></text>"), 200, 100);

        // Assert
        Assert.Equal([(10, 19)], InkRuns(surface, 25));
        Assert.Equal([(100, 109), (120, 129)], InkRuns(surface, 65));
    }

    /// <summary>
    ///     Test that a nested <c>tspan</c> inherits the size of its parent <c>tspan</c> and overrides its color.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_NestedTspans_InheritAndOverride()
    {
        // Act
        using var surface = Render(
            Doc("<text x='10' y='50'><tspan fill='#ff0000' font-size='40'>A<tspan fill='#0000ff'>A</tspan></tspan></text>"), 200, 100);

        // Assert: both glyphs are 40 px (20 px squares), the inner one blue
        Assert.Equal([(10, 29), (50, 69)], InkRuns(surface, 35));
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[20, 40]);
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[60, 40]);
    }

    /// <summary>
    ///     Test that <c>text-anchor</c> middle and end anchor a chunk of several runs as a whole.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TextAnchorMiddleAndEnd_AnchorEachChunk()
    {
        // Act: two runs of one 40 px chunk, anchored at x = 100
        using var middle = Render(Doc("<text x='100' y='50' text-anchor='middle'>A<tspan fill='#ff0000'>A</tspan></text>"), 200, 100);
        using var end = Render(Doc("<text x='100' y='50' text-anchor='end'>A<tspan fill='#ff0000'>A</tspan></text>"), 200, 100);

        // Assert
        Assert.Equal([(80, 89), (100, 109)], InkRuns(middle, 45));
        Assert.Equal([(60, 69), (80, 89)], InkRuns(end, 45));
        Assert.Equal(new Rgba32(255, 0, 0, 255), end[85, 45]);
    }

    /// <summary>
    ///     Test that a chunk takes the <c>text-anchor</c> of its first character, here a <c>tspan</c> that starts the
    ///     chunk.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TextAnchorOfAChunk_IsThatOfItsFirstCharacter()
    {
        // Act
        using var surface = Render(Doc("<text x='10' y='30'>A<tspan x='100' y='70' text-anchor='end'>AA</tspan></text>"), 200, 100);

        // Assert: the text's own chunk starts at 10, the tspan's chunk ends at 100
        Assert.Equal([(10, 19)], InkRuns(surface, 25));
        Assert.Equal([(60, 69), (80, 89)], InkRuns(surface, 65));
    }

    /// <summary>
    ///     Test that <c>x</c> and <c>dx</c> lists place each character, and that the characters beyond a list follow
    ///     the last one.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TextPositionLists_PlaceEachCharacter()
    {
        // Act
        using var absolute = Render(Doc("<text x='10 50 100' y='50'>AAAA</text>"), 200, 100);
        using var relative = Render(Doc("<text x='10' dx='0 5 5' y='50'>AAAA</text>"), 200, 100);

        // Assert
        Assert.Equal([(10, 19), (50, 59), (100, 109), (120, 129)], InkRuns(absolute, 45));
        Assert.Equal([(10, 19), (35, 44), (60, 69), (80, 89)], InkRuns(relative, 45));
    }

    /// <summary>
    ///     Test that an <c>x</c> list with an entry that is not a length is invalid data, as a single <c>x</c> always
    ///     was, and that a malformed <c>dx</c> entry ends that list.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TextPositionListMalformed_XIsInvalidAndDxEnds()
    {
        // Act / Assert
        Assert.Throws<InvalidDataException>(() => Render(Doc("<text x='10 oops' y='50'>AA</text>"), 200, 100).Dispose());
        Assert.True(DrawSame(
            Doc("<text x='10' dx='5 oops 5' y='50'>AAA</text>"),
            Doc("<text x='15' y='50'>AAA</text>")));
    }

    /// <summary>
    ///     Test that white space collapses across element boundaries and is trimmed at both ends, while
    ///     <c>xml:space="preserve"</c> keeps it.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TextWhiteSpace_CollapsesAndTrimsUnlessPreserved()
    {
        // Arrange: the bundled sans-serif font, because the synthetic font's space has no advance
        static string Sans(string inner) => Doc($"<g font-family='sans-serif'>{inner}</g>");

        // Act / Assert: tab and line feed are spaces, runs of spaces collapse, the ends are trimmed
        Assert.True(DrawSame(
            Sans("<text x='10' y='50'>  \tA \n  <tspan> A</tspan>  </text>"),
            Sans("<text x='10' y='50'>A <tspan>A</tspan></text>")));
        Assert.False(DrawSame(
            Sans("<text x='10' y='50'>A <tspan>A</tspan></text>"),
            Sans("<text x='10' y='50'>A<tspan>A</tspan></text>")));
        Assert.True(DrawSame(
            Sans("<text x='100' y='50' text-anchor='end'>A   </text>"),
            Sans("<text x='100' y='50' text-anchor='end'>A</text>")));
        Assert.False(DrawSame(
            Sans("<text x='10' y='50' xml:space='preserve'>  A</text>"),
            Sans("<text x='10' y='50'>A</text>")));
        Assert.True(DrawSame(
            Sans("<text x='10' y='50' xml:space='preserve'>A\tA</text>"),
            Sans("<text x='10' y='50'>A A</text>")));
    }

    /// <summary>
    ///     Test that <c>baseline-shift</c> moves only its own run: a length up, a percentage of the font size, and
    ///     <c>super</c>/<c>sub</c> by half the font's ascent plus descent.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TspanBaselineShift_ShiftsOnlyItsRun()
    {
        // Arrange: the synthetic font's ascent is the whole em and its descent 0, so super is 10 px at 20 px
        static string Shifted(string value) => Doc($"<text x='10' y='50'>A<tspan baseline-shift='{value}'>A</tspan>A</text>");

        // Act
        using var length = Render(Shifted("10"), 200, 100);

        // Assert: the middle glyph sits 10 px higher, the third is back on the baseline
        Assert.Equal([(10, 19), (50, 59)], InkRuns(length, 45));
        Assert.Equal([(30, 39)], InkRuns(length, 35));
        Assert.True(DrawSame(Shifted("50%"), Shifted("10")));
        Assert.True(DrawSame(Shifted("super"), Shifted("10")));
        Assert.True(DrawSame(Shifted("sub"), Shifted("-10")));
        Assert.True(DrawSame(Shifted("baseline"), Doc("<text x='10' y='50'>AAA</text>")));
    }

    /// <summary>
    ///     Test that <c>title</c> and <c>desc</c> inside a <c>text</c> are not drawn.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TextTitleAndDesc_AreNotDrawn()
    {
        // Act / Assert
        Assert.True(DrawSame(
            Doc("<text x='10' y='50'><title>AAAA</title>A<desc>AA</desc></text>"),
            Doc("<text x='10' y='50'>A</text>")));
    }

    /// <summary>
    ///     Test that an <c>a</c> element draws its content like a group, and inside a <c>text</c> like a <c>tspan</c>.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_AnchorElement_DrawsItsContent()
    {
        // Act
        using var surface = Render(
            Doc("<a href='#x' fill='#0000ff'><rect x='150' y='10' width='20' height='20'/></a>" +
                "<text x='10' y='50'>A<a href='#x' fill='#ff0000'>A</a></text>"),
            200,
            100);

        // Assert
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[160, 20]);
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[35, 45]);
    }

    /// <summary>
    ///     Test that a <c>switch</c> draws only its first child that does not need <c>foreignObject</c>: the fallback
    ///     of a draw.io label, and the first of two drawable children.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_Switch_DrawsTheFirstSupportedChild()
    {
        // Arrange
        var svg = Doc(
            "<switch><foreignObject width='50' height='50'><p xmlns='http://www.w3.org/1999/xhtml'>html</p></foreignObject>" +
            "<text x='10' y='50'>A</text></switch>" +
            "<switch><g requiredFeatures='http://www.w3.org/TR/SVG11/feature#Extensibility'>" +
            "<rect x='100' y='10' width='20' height='20' fill='#00ff00'/></g>" +
            "<rect x='100' y='10' width='20' height='20' fill='#ff0000'/>" +
            "<rect x='150' y='10' width='20' height='20' fill='#0000ff'/></switch>");

        // Act
        using var surface = Render(svg, 200, 100);

        // Assert
        Assert.True(Inked(surface, 15, 45));
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[110, 20]);
        Assert.False(Inked(surface, 160, 20));
    }

    /// <summary>
    ///     Test that a gradient on a <c>tspan</c> spans the bounding box of the whole text, as in Chrome and Edge.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TspanGradientFill_UsesTheTextBoundingBox()
    {
        // Arrange: the text spans x 10..80; the tspan's glyphs are at 50..60 and 70..80, in the blue half
        var svg = Doc(
            "<defs><linearGradient id='g'><stop offset='0' stop-color='#ff0000'/><stop offset='1' stop-color='#0000ff'/></linearGradient></defs>" +
            "<text x='10' y='50'>AA<tspan fill='url(#g)'>AA</tspan></text>");

        // Act
        using var surface = Render(svg, 200, 100);

        // Assert
        var left = surface[51, 45];
        Assert.True(left.B > left.R, $"expected the blue half of the gradient at the tspan's first glyph, got {left}");
    }

    /// <summary>
    ///     Test that a <c>clipPath</c> whose text has <c>tspan</c> runs clips to every run where it is drawn.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_ClipPathTextWithTspans_ClipsToEveryRun()
    {
        // Arrange: the second run is 40 px and 20 px lower, at x 30..50 and y 50..70
        var svg = Doc(
            "<clipPath id='c'><text x='10' y='50' font-family='TestFont' font-size='20'>A<tspan dy='20' font-size='40'>A</tspan></text></clipPath>" +
            "<rect width='200' height='100' fill='#00ff00' clip-path='url(#c)'/>");

        // Act
        using var surface = Render(svg, 200, 100);

        // Assert
        Assert.True(Inked(surface, 15, 45));
        Assert.True(Inked(surface, 40, 65));
        Assert.False(Inked(surface, 40, 45));
        Assert.False(Inked(surface, 60, 65));
    }

    /// <summary>
    ///     Test that a filtered text with <c>tspan</c> runs keeps each run's paint and position, and that the text's
    ///     opacity applies once to the result.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_FilteredTextWithTspans_KeepsEachRunsPaint()
    {
        // Arrange: a pass-through filter forces the effects pipeline
        var svg = Doc(
            "<filter id='f' x='-1' y='-1' width='3' height='3'><feOffset dx='0' dy='0'/></filter>" +
            "<text x='10' y='50' filter='url(#f)'>A<tspan fill='#ff0000' dy='20'>A</tspan></text>");

        // Act
        using var surface = Render(svg, 200, 100);

        // Assert
        Assert.Equal(new Rgba32(0, 0, 0, 255), surface[15, 45]);
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[35, 65]);
        Assert.False(Inked(surface, 35, 45));
    }

    /// <summary>
    ///     Test that the opacity of a text applies once to all its runs, so a run that overlaps another covers it, as in
    ///     browsers, instead of both showing through.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TextOpacityWithOverlappingTspans_AppliesOnceToTheWholeText()
    {
        // Arrange: blue A at 10..19, a red tspan A moved back 15 px to 15..24, the text at half opacity, on white
        var svg = Doc(
            "<rect width='200' height='100' fill='#ffffff'/>" +
            "<text x='10' y='50' opacity='0.5' fill='#0000ff'>A<tspan dx='-15' fill='#ff0000'>A</tspan></text>");

        // Act
        using var surface = Render(svg, 200, 100);

        // Assert: red covers blue where they overlap (group opacity: ~(255,128,128), not ~(191,64,128));
        // each run alone is half opaque
        AssertNear(new Rgba32(255, 128, 128, 255), surface[17, 45]);
        AssertNear(new Rgba32(128, 128, 255, 255), surface[12, 45]);
        AssertNear(new Rgba32(255, 128, 128, 255), surface[22, 45]);
    }

    /// <summary>
    ///     Test that plain text keeps its layout: one run anchored by the text, offset by its <c>dx</c>.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_PlainText_IsUnchangedByTheRunLayout()
    {
        // Act
        using var surface = Render(Doc("<text x='100' y='50' text-anchor='middle' dx='5'>AA</text>"), 200, 100);

        // Assert: 40 px of advance centered on 105
        Assert.Equal([(85, 94), (105, 114)], InkRuns(surface, 45));
    }

    /// <summary>
    ///     Test that a long text of many positioned characters lays out linearly and within the work budget.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_LongPositionedText_Renders()
    {
        // Arrange: 5000 characters, each with its own x
        var xs = new StringBuilder();
        for (var i = 0; i < 5000; i++)
        {
            xs.Append(i % 190).Append(' ');
        }

        var svg = Doc($"<text x='{xs}' y='50'>{new string('A', 5000)}</text>");

        // Act
        using var surface = Render(svg, 200, 100);

        // Assert
        Assert.True(Inked(surface, 5, 45));
    }

    /// <summary>
    ///     Test that <c>letter-spacing</c> adds its length after every character.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_LetterSpacing_AddsSpaceAfterEveryCharacter()
    {
        // Act
        using var surface = Render(Doc("<text x='10' y='50' letter-spacing='10'>AAA</text>"), 200, 100);

        // Assert: each 20 px advance is followed by 10 px
        Assert.Equal([(10, 19), (40, 49), (70, 79)], InkRuns(surface, 45));
    }

    /// <summary>
    ///     Test that <c>letter-spacing</c> in <c>em</c>, in percent of the font size, in CSS, and inherited all resolve
    ///     as in Edge: <c>em</c> at the element that declares it, a percentage with each character's own font size; that
    ///     <c>normal</c>, 0 and <c>initial</c> add nothing; and that an invalid value is ignored.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_LetterSpacingUnits_ResolveEmPercentAndInheritance()
    {
        // Arrange: every case lays out three 20 px glyphs
        List<(int Start, int End)> spaced = [(10, 19), (40, 49), (70, 79)];
        List<(int Start, int End)> plain = [(10, 19), (30, 39), (50, 59)];

        // Act / Assert: spaced by 10
        foreach (var markup in new[]
                 {
                     "<text x='10' y='50' letter-spacing='0.5em'>AAA</text>",
                     "<text x='10' y='50' letter-spacing='50%'>AAA</text>",
                     "<text x='10' y='50' style='letter-spacing:10px'>AAA</text>",
                     "<g letter-spacing='1em' font-size='10'><text x='10' y='50' font-size='20'>AAA</text></g>",
                     "<g letter-spacing='50%' font-size='10'><text x='10' y='50' font-size='20'>AAA</text></g>",
                     "<text x='10' y='50' letter-spacing='10'>A<tspan letter-spacing='bogus'>AA</tspan></text>",
                     "<text x='10' y='50' letter-spacing='10' style='letter-spacing:bogus'>AAA</text>"
                 })
        {
            using var surface = Render(Doc(markup), 200, 100);
            Assert.Equal(spaced, InkRuns(surface, 45));
        }

        // Act / Assert: not spaced
        foreach (var markup in new[]
                 {
                     "<text x='10' y='50' letter-spacing='normal'>AAA</text>",
                     "<text x='10' y='50' letter-spacing='0'>AAA</text>",
                     "<g letter-spacing='10'><text x='10' y='50' letter-spacing='initial'>AAA</text></g>"
                 })
        {
            using var surface = Render(Doc(markup), 200, 100);
            Assert.Equal(plain, InkRuns(surface, 45));
        }
    }

    /// <summary>
    ///     Test that a middle or end anchor centers or ends a spaced text by a width that includes the spacing after its
    ///     last character, as Edge does.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_LetterSpacingAnchorMiddleAndEnd_IncludesTheTrailingSpacing()
    {
        // Act: two glyphs spaced by 10, a width of 60, anchored at x = 100
        using var middle = Render(Doc("<text x='100' y='50' letter-spacing='10' text-anchor='middle'>AA</text>"), 200, 100);
        using var end = Render(Doc("<text x='100' y='50' letter-spacing='10' text-anchor='end'>AA</text>"), 200, 100);

        // Assert
        Assert.Equal([(70, 79), (100, 109)], InkRuns(middle, 45));
        Assert.Equal([(40, 49), (70, 79)], InkRuns(end, 45));
    }

    /// <summary>
    ///     Test that the <c>letter-spacing</c> of a <c>tspan</c> spaces only its own characters, and that each text chunk
    ///     is anchored by its own spaced width.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TspanLetterSpacing_SpacesOnlyItsCharacters()
    {
        // Act
        using var surface = Render(Doc("<text x='10' y='50'>A<tspan letter-spacing='10'>AA</tspan>A</text>"), 200, 100);
        using var chunks = Render(
            Doc("<text x='50' y='30' text-anchor='middle' letter-spacing='10'>A<tspan x='100' y='70'>AA</tspan></text>"), 200, 100);

        // Assert: the tspan's two glyphs are each followed by 10 px; chunk widths 30 and 60
        Assert.Equal([(10, 19), (30, 39), (60, 69), (90, 99)], InkRuns(surface, 45));
        Assert.Equal([(35, 44)], InkRuns(chunks, 25));
        Assert.Equal([(70, 79), (100, 109)], InkRuns(chunks, 65));
    }

    /// <summary>
    ///     Test that <c>word-spacing</c> widens spaces and no-break spaces (adding to the letter spacing), and leaves other
    ///     spaces such as the en space alone, as Edge does.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_WordSpacing_WidensSpacesAndNoBreakSpaces()
    {
        // Arrange: the start of the second glyph of a text, which the spacing moves
        var space = LastGlyphStart("<text x='10' y='50'>A A</text>");
        var noBreakSpace = LastGlyphStart("<text x='10' y='50'>A&#xA0;A</text>");

        // Act / Assert
        Assert.Equal(space + 10, LastGlyphStart("<text x='10' y='50' word-spacing='10'>A A</text>"));
        Assert.Equal(space + 10, LastGlyphStart("<text x='10' y='50' word-spacing='50%'>A A</text>"));
        Assert.Equal(noBreakSpace + 10, LastGlyphStart("<text x='10' y='50' word-spacing='10'>A&#xA0;A</text>"));
        Assert.Equal(space + 20, LastGlyphStart("<text x='10' y='50' letter-spacing='5' word-spacing='10'>A A</text>"));
        Assert.True(DrawSame(
            Doc("<text x='10' y='50'>A&#x2002;A</text>"),
            Doc("<text x='10' y='50' word-spacing='10'>A&#x2002;A</text>")));
    }

    /// <summary>
    ///     Test that a base character and its combining mark take the letter spacing once, as one grapheme cluster.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_LetterSpacingWithCombiningMark_SpacesTheClusterOnce()
    {
        // Arrange
        var plain = LastGlyphStart("<text x='10' y='50'>A&#x301;A</text>");

        // Act
        var spaced = LastGlyphStart("<text x='10' y='50' letter-spacing='10'>A&#x301;A</text>");

        // Assert
        Assert.Equal(plain + 10, spaced);
    }

    /// <summary>
    ///     Test that a <c>dx</c> shift adds to the letter spacing.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_LetterSpacingWithDx_AddsToTheShift()
    {
        // Act
        using var surface = Render(Doc("<text x='10' y='50' dx='0 5' letter-spacing='10'>AA</text>"), 200, 100);

        // Assert: 10 + 20 + 10 + 5
        Assert.Equal([(10, 19), (45, 54)], InkRuns(surface, 45));
    }

    /// <summary>
    ///     Test that a clip path's text uses the spaced layout, so it clips to the spaced glyphs and not the gaps.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_LetterSpacingInClipPathText_ClipsToTheSpacedGlyphs()
    {
        // Arrange
        var svg = Doc(
            "<clipPath id='c'><text x='10' y='50' font-family='TestFont' font-size='20' letter-spacing='10'>AA</text></clipPath>" +
            "<rect width='200' height='100' fill='#00ff00' clip-path='url(#c)'/>");

        // Act
        using var surface = Render(svg, 200, 100);

        // Assert
        Assert.Equal([(10, 19), (40, 49)], InkRuns(surface, 45));
    }

    /// <summary>
    ///     Test that a zero spacing draws exactly as no spacing, for a single run and for several anchored runs.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_ZeroSpacing_DrawsExactlyAsPlainText()
    {
        // Act / Assert
        Assert.True(DrawSame(
            Doc("<text x='100' y='50' text-anchor='middle' dx='5'>A AA</text>"),
            Doc("<text x='100' y='50' text-anchor='middle' dx='5' letter-spacing='0' word-spacing='normal'>A AA</text>")));
        Assert.True(DrawSame(
            Doc("<text x='100' y='50' text-anchor='end'>A<tspan fill='#ff0000'>A A</tspan></text>"),
            Doc("<text x='100' y='50' text-anchor='end' letter-spacing='0em' word-spacing='0%'>A<tspan fill='#ff0000'>A A</tspan></text>")));
    }

    /// <summary>
    ///     Test that a long spaced text lays out cluster by cluster linearly and within the work budget.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_LongSpacedText_Renders()
    {
        // Arrange: 5000 characters with letter and word spacing
        var svg = Doc($"<text x='10' y='50' letter-spacing='1' word-spacing='2'>{string.Join(' ', Enumerable.Repeat("AAAA", 1000))}</text>");

        // Act
        using var surface = Render(svg, 200, 100);

        // Assert: the first glyph is drawn, the second follows after its spacing
        Assert.Equal((10, 19), InkRuns(surface, 45)[0]);
        Assert.Equal((31, 40), InkRuns(surface, 45)[1]);
    }
}
