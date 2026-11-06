// cspell:ignore href hrefs pptx vsdx scad
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Viewer.Tests;

/// <summary>
///     End-to-end tests of the page and view fragments of the links of an SVG document, through the built-in sources of
///     <see cref="CanvasNetViewer"/> (which draw with <see cref="SourceLoader"/>): <c>report.pdf#page=2</c>,
///     <c>deck.pptx#slide=2</c>, <c>drawing.vsdx#page=2</c>, <c>part.scad#view=front</c>, the notes and codes for what
///     cannot be drawn, the query, the sources of a URI scheme, and <c>--strict</c>.
/// </summary>
public class CanvasNetViewerFragmentTests
{
    /// <summary>The color of the second page of the PDF fixture (a green fill).</summary>
    private static readonly Rgba32 PdfPageTwoGreen = new(0, 153, 0, 255);

    /// <summary>The color of the backdrop rectangle of the document that embeds the pictures.</summary>
    private static readonly Rgba32 Backdrop = new(128, 128, 128, 255);

    /// <summary>
    ///     Creates a folder with the multi-page fixtures beside the diagram.
    /// </summary>
    /// <returns>The folder; the caller disposes it.</returns>
    private static ViewerTestFolder CreateFolder()
    {
        var folder = new ViewerTestFolder();
        folder.WriteBytes("report.pdf", FixtureFactory.CreatePdf());
        folder.WriteBytes("deck.pptx", FixtureFactory.CreatePptx());
        folder.WriteBytes("drawing.vsdx", FixtureFactory.CreateVsdx());
        folder.WriteText("part.scad", ViewerFixtures.Cube);
        return folder;
    }

    /// <summary>
    ///     Writes a diagram with the given cells and renders it with the viewer.
    /// </summary>
    /// <param name="folder">The folder of the diagram.</param>
    /// <param name="hrefs">The links, one per cell, from the left.</param>
    /// <returns>The picture; the caller disposes it.</returns>
    private static ViewerImage Render(ViewerTestFolder folder, params string[] hrefs)
    {
        // An ampersand in an attribute is written as an entity in XML; the parser gives the link its plain "&" back
        var svg = ViewerFixtures.Svg(string.Concat(hrefs.Select((href, i) => ViewerFixtures.Cell(href.Replace("&", "&amp;", StringComparison.Ordinal), i))));
        var path = folder.WriteText("diagram.svg", svg);
        return CanvasNetViewer.Render(path);
    }

    /// <summary>
    ///     Test that the two pages of a PDF are drawn by the fragment of the link: no fragment and <c>#page=1</c> are
    ///     the first page, <c>#page=2</c> is the second, and nothing is reported.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_PdfPageFragment_DrawsTheChosenPage()
    {
        // Arrange
        using var folder = CreateFolder();

        // Act
        using var image = Render(folder, "report.pdf", "report.pdf#page=1", "report.pdf#page=2");

        // Assert: page 1 has its red rectangle at the top left of the fitted page; page 2 is the green one, upright and narrower
        var surface = image.Surface;
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[25, 33]);
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[125, 33]);
        Assert.Equal(PdfPageTwoGreen, surface[250, 50]);
        Assert.Equal(Backdrop, surface[200 + 5, 50]);
        Assert.Empty(image.Diagnostics);
        Assert.Equal(["report.pdf", "report.pdf#page=1", "report.pdf#page=2"], image.Embedded.Select(e => e.Href));
        Assert.Equal(["pdf", "pdf", "pdf"], image.Embedded.Select(e => e.Format));
        Assert.Equal((100, 67), (image.Embedded[0].Width, image.Embedded[0].Height));
        Assert.Equal((67, 100), (image.Embedded[2].Width, image.Embedded[2].Height));
    }

    /// <summary>
    ///     Test that a slide of a presentation is chosen with <c>#slide=2</c> or <c>#page=2</c>, and a page of a drawing
    ///     with <c>#page=2</c>.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_SlideAndVsdxPageFragments_DrawTheChosenPage()
    {
        // Arrange
        using var folder = CreateFolder();

        // Act
        using var image = Render(folder, "deck.pptx", "deck.pptx#slide=2", "deck.pptx#page=2", "drawing.vsdx", "drawing.vsdx#page=2");

        // Assert: slide 1 is red on its left half, slide 2 blue on its top half; VSDX page 1 red at the top left, page 2 blue at the bottom
        var surface = image.Surface;
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[25, 50]);
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[150, 30]);
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[250, 30]);
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[325, 22]);
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[450, 75]);
        Assert.Empty(image.Diagnostics);
    }

    /// <summary>
    ///     Test that a page the document does not have is an unsupported note with the page-out-of-range code, nothing is
    ///     drawn for it, it is not listed as embedded, and the rest of the document is drawn.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_PageBeyondTheLast_IsUnsupportedNoteAndNothingDrawn()
    {
        // Arrange
        using var folder = CreateFolder();

        // Act
        using var image = Render(folder, "report.pdf#page=9", "report.pdf#page=2", "deck.pptx#slide=3", "drawing.vsdx#page=3");

        // Assert
        var codes = image.Diagnostics.Select(d => (d.Code, d.Severity, d.Source)).ToList();
        Assert.Equal(
            [
                (FolderImageResolver.PageOutOfRangeCode, ImageDiagnosticSeverity.Unsupported, "svg"),
                (FolderImageResolver.PageOutOfRangeCode, ImageDiagnosticSeverity.Unsupported, "svg"),
                (FolderImageResolver.PageOutOfRangeCode, ImageDiagnosticSeverity.Unsupported, "svg")
            ],
            codes);
        Assert.Contains("'report.pdf#page=9'", image.Diagnostics[0].Message, StringComparison.Ordinal);
        Assert.Contains("page 9 does not exist: the document has 2 pages", image.Diagnostics[0].Message, StringComparison.Ordinal);
        Assert.Contains("slide 3 does not exist: the presentation has 2 slides", image.Diagnostics[1].Message, StringComparison.Ordinal);
        Assert.Equal(Backdrop, image.Surface[50, 50]);
        Assert.Equal(PdfPageTwoGreen, image.Surface[150, 50]);
        Assert.Equal(["report.pdf#page=2"], image.Embedded.Select(e => e.Href));
    }

    /// <summary>
    ///     Test that a page value that is not a whole number of 1 or more is an unsupported note with the invalid-fragment
    ///     code, and nothing is drawn.
    /// </summary>
    /// <param name="fragment">The fragment.</param>
    [Theory]
    [InlineData("page=0")]
    [InlineData("page=abc")]
    [InlineData("page=")]
    [InlineData("page=-1")]
    [InlineData("page=1.5")]
    public void CanvasNetViewer_Render_InvalidPageValue_IsInvalidFragmentNote(string fragment)
    {
        // Arrange
        using var folder = CreateFolder();

        // Act
        using var image = Render(folder, "report.pdf#" + fragment);

        // Assert
        var note = Assert.Single(image.Diagnostics);
        Assert.Equal(FolderImageResolver.FragmentInvalidCode, note.Code);
        Assert.Equal(ImageDiagnosticSeverity.Unsupported, note.Severity);
        Assert.Contains("'page'", note.Message, StringComparison.Ordinal);
        Assert.Equal(Backdrop, image.Surface[50, 50]);
        Assert.Empty(image.Embedded);
    }

    /// <summary>
    ///     Test that a parameter the source does not use is a warning and the picture is still drawn, for a document, a
    ///     raster, an SVG file, and a link that is only a made-up parameter.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_UnknownParameter_IsWarningAndStillDrawn()
    {
        // Arrange: a PDF with a made-up parameter, a raster, and an SVG file that is linked with a parameter too
        using var folder = CreateFolder();
        folder.WriteBytes("logo.png", ViewerFixtures.Png(4, 4, ViewerFixtures.Green));
        folder.WriteText("inner.svg", "<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'><rect width='10' height='10' fill='#00ff00'/></svg>");

        // Act
        using var image = Render(folder, "report.pdf#page=2&foo=1", "logo.png#x", "inner.svg#anything");

        // Assert
        Assert.Equal(
            ["'foo'", "'x'", "'anything'"],
            image.Diagnostics.Select(d => d.Message.Split(' ').First(p => p.StartsWith('\'') && !p.Contains('#', StringComparison.Ordinal))));
        Assert.All(image.Diagnostics, d => Assert.Equal(FolderImageResolver.FragmentUnknownParameterCode, d.Code));
        Assert.All(image.Diagnostics, d => Assert.Equal(ImageDiagnosticSeverity.Warning, d.Severity));
        Assert.Equal(PdfPageTwoGreen, image.Surface[50, 50]);
        Assert.Equal(ViewerFixtures.Green, image.Surface[150, 50]);
        Assert.Equal(3, image.Embedded.Count);
    }

    /// <summary>
    ///     Test that the same parameter given twice with different values is a warning and the last one wins, and with the
    ///     same value is silent.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_DuplicatePage_LastWinsAndWarnsOnlyWhenDifferent()
    {
        // Arrange
        using var folder = CreateFolder();

        // Act
        using var image = Render(folder, "report.pdf#page=1&page=2", "report.pdf#page=2&page=2");

        // Assert
        var note = Assert.Single(image.Diagnostics);
        Assert.Equal(FolderImageResolver.FragmentDuplicateParameterCode, note.Code);
        Assert.Equal(ImageDiagnosticSeverity.Warning, note.Severity);
        Assert.Equal(PdfPageTwoGreen, image.Surface[50, 50]);
        Assert.Equal(PdfPageTwoGreen, image.Surface[150, 50]);
    }

    /// <summary>
    ///     Test that a query before the fragment is ignored for a local file, and that percent-encoding in the fragment is
    ///     decoded after it is split.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_QueryAndEncodedFragment_AreHandled()
    {
        // Arrange
        using var folder = CreateFolder();

        // Act: %67 is "g" and %32 is "2"
        using var image = Render(folder, "report.pdf?v=2#page=2", "report.pdf#pa%67e=%32");

        // Assert
        Assert.Empty(image.Diagnostics);
        Assert.Equal(PdfPageTwoGreen, image.Surface[50, 50]);
        Assert.Equal(PdfPageTwoGreen, image.Surface[150, 50]);
        Assert.Equal("report.pdf?v=2#page=2", image.Embedded[0].Href);
    }

    /// <summary>
    ///     Test that <c>#view=front</c> chooses the viewpoint of a model, that the name is not case-sensitive, and that an
    ///     unknown name and an empty one are invalid-fragment notes with nothing drawn.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_ScadViewFragment_ChoosesTheViewOrIsInvalid()
    {
        // Arrange
        using var folder = CreateFolder();

        // Act
        using var image = Render(folder, "part.scad", "part.scad#view=front", "part.scad#view=TOP", "part.scad#view=sideways", "part.scad#view");

        // Assert: the three drawn models are all different from the backdrop; the views differ; two links are invalid
        var surface = image.Surface;
        Assert.NotEqual(Backdrop, surface[50, 50]);
        Assert.NotEqual(Backdrop, surface[150, 50]);
        Assert.NotEqual(Backdrop, surface[250, 50]);
        Assert.Equal(Backdrop, surface[350, 50]);
        Assert.Equal(Backdrop, surface[450, 50]);
        Assert.Equal(
            [FolderImageResolver.FragmentInvalidCode, FolderImageResolver.FragmentInvalidCode],
            image.Diagnostics.Select(d => d.Code));
        Assert.Contains("'sideways'", image.Diagnostics[0].Message, StringComparison.Ordinal);
        Assert.Equal(3, image.Embedded.Count);
    }

    /// <summary>
    ///     Test that a source of a URI scheme gets the link untouched (query included) with the fragment parsed, and
    ///     that a built-in source does not get a scheme link.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_SchemeSource_ReceivesRawLinkAndParsedFragment()
    {
        // Arrange
        using var folder = CreateFolder();
        var source = new LiveFrameSource();
        var svg = ViewerFixtures.Svg(ViewerFixtures.Cell("test-scheme:front?fps=30#page=2&amp;who=me", 0));
        var path = folder.WriteText("diagram.svg", svg);

        // Act
        using var image = CanvasNetViewer.Render(path, new ViewerRenderOptions { ExtraSources = [source] });

        // Assert: the source owns its link, so no warning for who=me
        var request = Assert.Single(source.Requests);
        Assert.Equal("test-scheme:front?fps=30#page=2&who=me", request.Href);
        Assert.Equal("page=2&who=me", request.Fragment.Raw);
        Assert.Equal(["page", "who"], request.Fragment.Parameters.Select(p => p.Name));
        Assert.Empty(image.Diagnostics);
    }

    /// <summary>
    ///     Test that a custom file source that declares a parameter gets the parsed fragment, and that an extra source for
    ///     the same extension wins over the built-in one.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_ExtraSourceForBuiltInExtension_WinsAndGetsFragment()
    {
        // Arrange
        using var folder = CreateFolder();
        var source = ViewerFixtures.ColorText("my-pdf", ".pdf");

        // Act
        var svg = ViewerFixtures.Svg(ViewerFixtures.Cell("report.pdf#page=2", 0));
        var path = folder.WriteText("diagram.svg", svg);
        using var image = CanvasNetViewer.Render(path, new ViewerRenderOptions { ExtraSources = [source] });

        // Assert: the stub drew it (it does not declare page, so the parameter is unknown)
        Assert.Equal("page=2", Assert.Single(source.Requests).Fragment.Raw);
        Assert.Equal(FolderImageResolver.FragmentUnknownParameterCode, Assert.Single(image.Diagnostics).Code);
    }

    /// <summary>
    ///     Test that an SVG file embedded in an SVG file chooses the page of the document it links to with a fragment,
    ///     through the resolver of its own scope, and that the nested picture is listed one level deeper.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_NestedSvgWithFragment_UsesTheScopedResolver()
    {
        // Arrange: outer.svg embeds inner.svg, which embeds the second page of the PDF
        using var folder = CreateFolder();
        folder.WriteText(
            "inner.svg",
            "<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'><image href='report.pdf#page=2' x='0' y='0' width='100' height='100'/></svg>");
        var outer = folder.WriteText("outer.svg", ViewerFixtures.Svg(ViewerFixtures.Cell("inner.svg", 0)));

        // Act
        using var image = CanvasNetViewer.Render(outer);

        // Assert
        Assert.Empty(image.Diagnostics);
        Assert.Equal(PdfPageTwoGreen, image.Surface[50, 50]);
        Assert.Equal([("report.pdf#page=2", 1), ("inner.svg", 0)], image.Embedded.Select(e => (e.Href, e.Depth)));
    }

    /// <summary>
    ///     Test that with <c>Strict</c> a link with a page that does not exist or an invalid fragment is an
    ///     <c>unsupported-feature</c> error naming the link, while an unknown parameter is a warning only.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_StrictWithFragmentProblems_FailsOnlyForRefusedLinks()
    {
        // Arrange
        using var folder = CreateFolder();
        var outOfRange = folder.WriteText("a.svg", ViewerFixtures.Svg(ViewerFixtures.Cell("report.pdf#page=9", 0)));
        var invalid = folder.WriteText("b.svg", ViewerFixtures.Svg(ViewerFixtures.Cell("report.pdf#page=x", 0)));
        var unknown = folder.WriteText("c.svg", ViewerFixtures.Svg(ViewerFixtures.Cell("report.pdf#foo=1", 0)));
        var options = new SourceOptions { Strict = true };

        // Act
        var first = Assert.Throws<ViewerException>(() => SourceLoader.Load(outOfRange, options));
        var second = Assert.Throws<ViewerException>(() => SourceLoader.Load(invalid, options));
        using var third = SourceLoader.Load(unknown, options);

        // Assert
        Assert.Equal(ErrorCodes.UnsupportedFeature, first.Code);
        Assert.Contains("embeds 'report.pdf#page=9', which was not drawn: page 9 does not exist", first.Message, StringComparison.Ordinal);
        Assert.Equal(ErrorCodes.UnsupportedFeature, second.Code);
        Assert.Contains("image-fragment-invalid", second.Hint, StringComparison.Ordinal);
        Assert.Equal(FolderImageResolver.FragmentUnknownParameterCode, Assert.Single(third.Diagnostics).Code);
    }

    /// <summary>
    ///     Test that without <c>Strict</c> the same problems are notes of the loaded source, with the origin empty (they
    ///     are about links of the document itself).
    /// </summary>
    [Fact]
    public void SourceLoader_Load_FragmentProblems_AreNotesOfTheDocument()
    {
        // Arrange
        using var folder = CreateFolder();
        var path = folder.WriteText("a.svg", ViewerFixtures.Svg(ViewerFixtures.Cell("report.pdf#page=9", 0)));

        // Act
        using var loaded = SourceLoader.Load(path, new SourceOptions());

        // Assert
        var note = Assert.Single(loaded.Diagnostics);
        Assert.Equal((FolderImageResolver.PageOutOfRangeCode, "unsupported", "svg"), (note.Code, note.Severity, note.Source));
        Assert.Empty(note.Origin);
    }

    /// <summary>
    ///     Test that a hostile fragment is refused as an invalid-fragment note and costs no file read or image count.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_HostileFragment_IsRefusedAndShortInTheMessage()
    {
        // Arrange
        using var folder = CreateFolder();
        var href = "report.pdf#page=2&" + string.Join('&', Enumerable.Range(0, 40).Select(i => "p" + i));

        // Act
        using var image = Render(folder, href);

        // Assert
        var note = Assert.Single(image.Diagnostics);
        Assert.Equal(FolderImageResolver.FragmentInvalidCode, note.Code);
        Assert.Contains("more than 32 parameters", note.Message, StringComparison.Ordinal);
        Assert.Empty(image.Embedded);
    }
}
