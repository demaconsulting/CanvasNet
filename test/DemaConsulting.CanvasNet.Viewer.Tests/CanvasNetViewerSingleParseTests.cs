// cspell:ignore pptx vsdx
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Viewer.Tests;

/// <summary>
///     Tests that a render through <see cref="CanvasNetViewer"/> or <see cref="LoaderImageSource"/> parses a PDF,
///     presentation or drawing once: the facts about the pages and the pages themselves come from the same parsed document.
/// </summary>
/// <remarks>
///     The proof is the per-thread parse counter of <see cref="SourceLoader"/>, which the loader increments every time it
///     parses one of these documents. A render runs on the calling thread, so the counter is not disturbed by tests that
///     run in parallel.
/// </remarks>
public class CanvasNetViewerSingleParseTests
{
    /// <summary>
    ///     Gets the document kinds that are parsed to read their pages.
    /// </summary>
    /// <returns>The test data: the file extension of each kind.</returns>
    public static TheoryData<string> DocumentKinds()
    {
        return ["pdf", "pptx", "vsdx"];
    }

    /// <summary>
    ///     Test that rendering a document by path parses it exactly once.
    /// </summary>
    /// <param name="extension">The kind of document.</param>
    [Theory]
    [MemberData(nameof(DocumentKinds))]
    public void CanvasNetViewer_Render_Path_ParsesTheDocumentOnce(string extension)
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, extension);
        var before = SourceLoader.DocumentParseCount;

        // Act
        using var image = CanvasNetViewer.Render(path, new ViewerRenderOptions { Page = 2 });

        // Assert
        Assert.Equal(1, SourceLoader.DocumentParseCount - before);
        Assert.Equal((2, 2), (image.Page, image.PageCount));
    }

    /// <summary>
    ///     Test that rendering a document from a stream parses it exactly once.
    /// </summary>
    /// <param name="extension">The kind of document.</param>
    [Theory]
    [MemberData(nameof(DocumentKinds))]
    public void CanvasNetViewer_Render_Stream_ParsesTheDocumentOnce(string extension)
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, extension);
        using var stream = new MemoryStream(File.ReadAllBytes(path));
        var before = SourceLoader.DocumentParseCount;

        // Act
        using var image = CanvasNetViewer.Render(stream, "document." + extension);

        // Assert
        Assert.Equal(1, SourceLoader.DocumentParseCount - before);
        Assert.Equal(1, image.Page);
    }

    /// <summary>
    ///     Test that a page that does not exist is refused after one parse, and that the parsed document is not parsed
    ///     again or leaked.
    /// </summary>
    /// <param name="extension">The kind of document.</param>
    [Theory]
    [MemberData(nameof(DocumentKinds))]
    public void CanvasNetViewer_Render_PageOutOfRange_ParsesTheDocumentOnce(string extension)
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, extension);
        var before = SourceLoader.DocumentParseCount;

        // Act
        var exception = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(path, new ViewerRenderOptions { Page = 9 }));

        // Assert: the file is not locked after the failure either
        Assert.Equal(ErrorCodes.PageOutOfRange, exception.Code);
        Assert.Equal(1, SourceLoader.DocumentParseCount - before);
        File.Delete(path);
    }

    /// <summary>
    ///     Test that a document linked from an SVG file by a page fragment is parsed once for the link.
    /// </summary>
    /// <param name="extension">The kind of document.</param>
    [Theory]
    [MemberData(nameof(DocumentKinds))]
    public void CanvasNetViewer_Render_LinkedDocument_ParsesTheDocumentOnce(string extension)
    {
        // Arrange
        using var directory = new TempDirectory();
        FixtureFactory.WriteSource(directory.Path, extension);
        var svg = Path.Combine(directory.Path, "outer.svg");
        File.WriteAllText(
            svg,
            $"""<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64"><image href="source.{extension}#page=2" width="64" height="64"/></svg>""");
        var before = SourceLoader.DocumentParseCount;

        // Act
        using var image = CanvasNetViewer.Render(svg);

        // Assert
        Assert.Equal(1, SourceLoader.DocumentParseCount - before);
        Assert.Empty(image.Diagnostics);
    }

    /// <summary>
    ///     Test that probing a document for its facts (the <c>info</c> command) parses it once.
    /// </summary>
    /// <param name="extension">The kind of document.</param>
    [Theory]
    [MemberData(nameof(DocumentKinds))]
    public void SourceLoader_Probe_ParsesTheDocumentOnce(string extension)
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, extension);
        var before = SourceLoader.DocumentParseCount;

        // Act
        var info = SourceLoader.Probe(path);

        // Assert
        Assert.Equal(1, SourceLoader.DocumentParseCount - before);
        Assert.Equal(2, info.PageCount);
    }
}
