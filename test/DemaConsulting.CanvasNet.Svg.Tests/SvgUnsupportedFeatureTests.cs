using System.Text;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.ImageSources;
using static DemaConsulting.CanvasNet.Svg.Tests.SvgLengthTestSupport;

namespace DemaConsulting.CanvasNet.Svg.Tests;

/// <summary>
///     Tests of <see cref="SvgLoadOptions.UnsupportedFeatureHandler"/> and <see cref="SvgUnsupportedFeature"/>: every text
///     that is not drawn, and every text feature that is ignored, is reported once per code with a count, without
///     changing the pixels; and <see cref="SvgImageSource"/> turns the reports into notes or a failure.
/// </summary>
public class SvgUnsupportedFeatureTests
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
    ///     Renders a document with the synthetic font and collects its reports.
    /// </summary>
    /// <param name="svg">The document.</param>
    /// <returns>The reports, in the order they were received.</returns>
    private static List<SvgUnsupportedFeature> Reports(string svg)
    {
        var reports = new List<SvgUnsupportedFeature>();
        using var stream = ToStream(svg);
        var faces = new Dictionary<string, IReadOnlyList<SvgFontFace>> { ["TestFont"] = [new SvgFontFace(BuildSquareFont())] };
        using var surface = SvgCodec.LoadWithFontFaces(
            stream, 200, 100, faces, new SvgLoadOptions { UseSystemFonts = false, UnsupportedFeatureHandler = reports.Add });
        return reports;
    }

    /// <summary>
    ///     Test that the text of a <c>textPath</c> is reported as not drawn.
    /// </summary>
    [Fact]
    public void SvgCodec_UnsupportedFeatureHandler_TextPath_ReportsTextNotDrawn()
    {
        // Act
        var reports = Reports(Doc("<path id='p' d='M0 50 L200 50'/><text id='t'><textPath href='#p'>AAA</textPath></text>"));

        // Assert
        var report = Assert.Single(reports);
        Assert.Equal(SvgUnsupportedFeature.TextNotDrawnCode, report.Code);
        Assert.Equal(1, report.Count);
        Assert.Contains("textPath", report.Message, StringComparison.Ordinal);
        Assert.Contains("<text id=\"t\"> 'AAA'", report.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the ignored text features of one text are reported as one unsupported report naming every feature,
    ///     and that the letter and word spacing it also uses are not reported (they are drawn).
    /// </summary>
    [Fact]
    public void SvgCodec_UnsupportedFeatureHandler_RotateTextLengthAndWritingMode_ReportsOneUnsupportedWithEveryFeature()
    {
        // Arrange: the inherited features come from the group, the others from the text and its tspan
        var svg = Doc(
            "<g letter-spacing='2' direction='rtl'><text x='10' y='50' rotate='0 15' textLength='90' word-spacing='3px' " +
            "writing-mode='vertical-rl' dominant-baseline='middle' text-decoration='underline'>A" +
            "<tspan baseline-shift='bogus' alignment-baseline='middle'>A</tspan></text></g>");

        // Act
        var report = Assert.Single(Reports(svg));

        // Assert
        Assert.Equal(SvgUnsupportedFeature.TextUnsupportedCode, report.Code);
        Assert.Equal(1, report.Count);
        foreach (var feature in new[]
                 {
                     "rotate", "textLength", "vertical writing-mode", "direction rtl",
                     "dominant-baseline", "text-decoration", "baseline-shift", "alignment-baseline"
                 })
        {
            Assert.Contains(feature, report.Message, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("spacing", report.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that text inside a <c>foreignObject</c>, a nested <c>svg</c>, an unknown element, and an unknown
    ///     element inside a <c>text</c> are reported as not drawn, one report for all.
    /// </summary>
    [Fact]
    public void SvgCodec_UnsupportedFeatureHandler_TextInForeignObjectNestedSvgAndUnknownElement_ReportsTextNotDrawn()
    {
        // Arrange
        var svg = Doc(
            "<foreignObject width='50' height='50'><p xmlns='http://www.w3.org/1999/xhtml'>html</p></foreignObject>" +
            "<svg><text>A</text></svg>" +
            "<unknown><text>A</text></unknown>" +
            "<text x='10' y='50'>A<tref>B</tref></text>");

        // Act
        var report = Assert.Single(Reports(svg));

        // Assert
        Assert.Equal(SvgUnsupportedFeature.TextNotDrawnCode, report.Code);
        Assert.Equal(4, report.Count);
        Assert.Contains("HTML content of <foreignObject>", report.Message, StringComparison.Ordinal);
        Assert.Contains("nested <svg>", report.Message, StringComparison.Ordinal);
        Assert.Contains("<unknown>", report.Message, StringComparison.Ordinal);
        Assert.Contains("<tref> inside text", report.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that text with a negative font size, under a non-finite transform, or with coordinates beyond the
    ///     supported range is reported as not drawn, and that a zero font size is not.
    /// </summary>
    [Fact]
    public void SvgCodec_UnsupportedFeatureHandler_NegativeFontSizeAndHugeTransform_ReportsTextNotDrawn()
    {
        // Arrange: seven nested scale(1e6) overflow to infinity; two give 1e12, finite but beyond the range
        var nonFinite = string.Concat(Enumerable.Repeat("<g transform='scale(1000000)'>", 7)) + "<text>A</text>" +
                        string.Concat(Enumerable.Repeat("</g>", 7));
        var svg = Doc(
            "<text x='10' y='50' font-size='-10'>A</text>" +
            "<text x='10' y='50' font-size='0'>A</text>" +
            nonFinite +
            "<g transform='scale(1000000)'><g transform='scale(1000000)'><text x='10' y='50'>A</text></g></g>");

        // Act
        var report = Assert.Single(Reports(svg));

        // Assert
        Assert.Equal(SvgUnsupportedFeature.TextNotDrawnCode, report.Code);
        Assert.Equal(3, report.Count);
        Assert.Contains("negative font-size", report.Message, StringComparison.Ordinal);
        Assert.Contains("non-finite transform", report.Message, StringComparison.Ordinal);
        Assert.Contains("coordinates beyond the supported range", report.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a character no font has a glyph for is reported as unsupported with its code point.
    /// </summary>
    [Fact]
    public void SvgCodec_UnsupportedFeatureHandler_MissingGlyph_ReportsUnsupportedWithTheCodePoint()
    {
        // Act: U+E000 is a private-use character that neither the test font nor a bundled fallback font covers
        var report = Assert.Single(Reports(Doc("<text x='10' y='50'>A&#xE000;</text>")));

        // Assert
        Assert.Equal(SvgUnsupportedFeature.TextUnsupportedCode, report.Code);
        Assert.Contains("U+E000", report.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that several texts are reported once per code, with the count of elements and the first one named,
    ///     and that the not-drawn report comes first.
    /// </summary>
    [Fact]
    public void SvgCodec_UnsupportedFeatureHandler_SeveralElements_ReportsOnceWithTheCount()
    {
        // Arrange
        var svg = Doc(
            "<path id='p' d='M0 50 L200 50'/>" +
            "<text x='10' y='50' rotate='5'>A</text>" +
            "<text id='first'><textPath href='#p'>AA</textPath></text>" +
            "<text><textPath href='#p'>AA</textPath></text>" +
            "<defs><text id='reused'><textPath href='#p'>AA</textPath></text></defs><use href='#reused'/><use href='#reused'/>");

        // Act
        var reports = Reports(svg);

        // Assert: the text drawn twice through use counts once
        Assert.Equal([SvgUnsupportedFeature.TextNotDrawnCode, SvgUnsupportedFeature.TextUnsupportedCode], reports.Select(r => r.Code));
        Assert.Equal(3, reports[0].Count);
        Assert.Contains("first: <text id=\"first\">", reports[0].Message, StringComparison.Ordinal);
        Assert.Contains("3 elements", reports[0].Message, StringComparison.Ordinal);
        Assert.Equal(1, reports[1].Count);
    }

    /// <summary>
    ///     Test that plain text, styled and positioned <c>tspan</c> runs, letter and word spacing, a <c>switch</c>
    ///     fallback and descriptions report nothing.
    /// </summary>
    [Fact]
    public void SvgCodec_UnsupportedFeatureHandler_PlainAndStyledText_ReportsNothing()
    {
        // Arrange
        var svg = Doc(
            "<text x='10 20' y='50' dx='1' letter-spacing='normal' rotate='0'>A<tspan fill='red' baseline-shift='super' dy='3'>A</tspan>" +
            "<title>AAA</title><a href='#x'>A</a></text>" +
            "<g letter-spacing='1em'><text x='10' y='70' letter-spacing='2' word-spacing='3px'>A A</text></g>" +
            "<switch><foreignObject width='5' height='5'><p xmlns='http://www.w3.org/1999/xhtml'>html</p></foreignObject>" +
            "<text x='10' y='80'>A</text></switch>" +
            "<defs><text><textPath href='#p'>never drawn</textPath></text></defs>" +
            "<text>   </text>");

        // Act / Assert
        Assert.Empty(Reports(svg));
    }

    /// <summary>
    ///     Test that a handler never changes the pixels.
    /// </summary>
    [Fact]
    public void SvgCodec_UnsupportedFeatureHandler_DoesNotChangeThePixels()
    {
        // Arrange
        var svg = Doc(
            "<path id='p' d='M0 50 L200 50'/><text x='10' y='30' rotate='5' letter-spacing='3'>A&#xE000;<textPath href='#p'>A</textPath></text>" +
            "<foreignObject width='50' height='50'><p xmlns='http://www.w3.org/1999/xhtml'>html</p></foreignObject>");
        var faces = new Dictionary<string, IReadOnlyList<SvgFontFace>> { ["TestFont"] = [new SvgFontFace(BuildSquareFont())] };
        var reports = new List<SvgUnsupportedFeature>();

        // Act
        using var plainStream = ToStream(svg);
        using var plain = SvgCodec.LoadWithFontFaces(plainStream, 200, 100, faces, new SvgLoadOptions { UseSystemFonts = false });
        using var reportedStream = ToStream(svg);
        using var reported = SvgCodec.LoadWithFontFaces(
            reportedStream, 200, 100, faces, new SvgLoadOptions { UseSystemFonts = false, UnsupportedFeatureHandler = reports.Add });

        // Assert
        Assert.Equal(2, reports.Count);
        Assert.True(AreIdentical(plain, reported));
    }

    /// <summary>
    ///     Test that the handler is not set by default.
    /// </summary>
    [Fact]
    public void SvgCodec_UnsupportedFeatureHandler_DefaultIsNull()
    {
        // Act
        var options = new SvgLoadOptions();

        // Assert
        Assert.Null(options.UnsupportedFeatureHandler);
    }

    /// <summary>
    ///     Test that <see cref="SvgImageSource"/> turns a text that is not drawn into an unsupported note of the result.
    /// </summary>
    [Fact]
    public void SvgImageSource_Render_TextPath_IsAnUnsupportedNoteOfTheResult()
    {
        // Arrange
        var source = new SvgImageSource();

        // Act
        var result = source.Render(Request(TextPathDocument, strict: false));
        using var surface = result.Surface;

        // Assert
        var note = Assert.Single(result.Diagnostics);
        Assert.Equal(ImageDiagnosticSeverity.Unsupported, note.Severity);
        Assert.Equal(SvgUnsupportedFeature.TextNotDrawnCode, note.Code);
        Assert.Equal("svg", note.Source);
    }

    /// <summary>
    ///     Test that <see cref="SvgImageSource"/> fails a request that throws on an unsupported feature.
    /// </summary>
    [Fact]
    public void SvgImageSource_Render_TextPathWithThrowOnUnsupportedFeature_ThrowsUnsupportedImageFeatureException()
    {
        // Arrange
        var source = new SvgImageSource();

        // Act
        var ex = Assert.Throws<UnsupportedImageFeatureException>(() => source.Render(Request(TextPathDocument, strict: true)));

        // Assert
        Assert.Equal(SvgUnsupportedFeature.TextNotDrawnCode, ex.Feature);
    }

    /// <summary>
    ///     A document whose only text is on a <c>textPath</c>.
    /// </summary>
    private const string TextPathDocument =
        "<svg xmlns='http://www.w3.org/2000/svg' width='40' height='30'><path id='p' d='M0 20 L40 20'/>" +
        "<text><textPath href='#p'>On a path</textPath></text></svg>";

    /// <summary>
    ///     Creates a request for an SVG document.
    /// </summary>
    /// <param name="svg">The document.</param>
    /// <param name="strict">Whether the request throws on an unsupported feature.</param>
    /// <returns>The request.</returns>
    private static ImageSourceRequest Request(string svg, bool strict) => new()
    {
        Href = "diagram.svg",
        FileName = "diagram.svg",
        Content = Encoding.UTF8.GetBytes(svg),
        Budget = new ImageBudget(long.MaxValue),
        ThrowOnUnsupportedFeature = strict
    };
}
