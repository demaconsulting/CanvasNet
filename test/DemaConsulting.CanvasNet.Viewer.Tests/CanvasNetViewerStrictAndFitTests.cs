// cspell:ignore href hrefs
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Viewer.Tests;

/// <summary>
///     Tests of two details of <see cref="CanvasNetViewer.Render(string, ViewerRenderOptions?)"/>: the strict policy for
///     the links of an SVG document (the same as the tool's <c>--strict</c>), and the fit of a picture into a box (the
///     margin has the chosen background and the anchors follow the picture).
/// </summary>
public class CanvasNetViewerStrictAndFitTests
{
    /// <summary>
    ///     Creates a folder with a two-page PDF, a small PNG and a header-only huge PNG.
    /// </summary>
    /// <returns>The folder; the caller disposes it.</returns>
    private static ViewerTestFolder CreateFolder()
    {
        var folder = new ViewerTestFolder();
        folder.WriteBytes("report.pdf", FixtureFactory.CreatePdf());
        folder.WriteBytes("logo.png", ViewerFixtures.Png(4, 4, ViewerFixtures.Green));
        folder.WriteBytes("huge.png", ViewerFixtures.PngDeclaring(Surface.MaxDimension, Surface.MaxDimension));
        return folder;
    }

    /// <summary>
    ///     Gets the links that are not drawn and the code of the note each one makes.
    /// </summary>
    /// <returns>The test data: the link and the code.</returns>
    public static TheoryData<string, string> UndrawnLinks()
    {
        return new TheoryData<string, string>
        {
            { "report.pdf#page=9", FolderImageResolver.PageOutOfRangeCode },
            { "report.pdf#page=0", FolderImageResolver.FragmentInvalidCode },
            { "missing.png", FolderImageResolver.UnresolvedCode },
            { "huge.png", FolderImageResolver.TooLargeCode }
        };
    }

    /// <summary>
    ///     Test that without the strict switch a link that is not drawn is a note and the render succeeds.
    /// </summary>
    /// <param name="href">The link.</param>
    /// <param name="code">The code of the note.</param>
    [Theory]
    [MemberData(nameof(UndrawnLinks))]
    public void CanvasNetViewer_Render_UndrawnLink_IsNoteWithoutStrict(string href, string code)
    {
        // Arrange
        using var folder = CreateFolder();
        var path = folder.WriteText("diagram.svg", ViewerFixtures.Svg(ViewerFixtures.Cell(href, 0)));

        // Act
        using var image = CanvasNetViewer.Render(path);

        // Assert
        Assert.Equal(code, Assert.Single(image.Diagnostics).Code);
    }

    /// <summary>
    ///     Test that with the strict switch a link that is not drawn (a page that does not exist, an invalid fragment, a
    ///     missing file, a picture too large for the budget) fails the render with the unsupported-feature code, naming the
    ///     link, for a file given by path and for one given as a stream.
    /// </summary>
    /// <param name="href">The link.</param>
    /// <param name="code">The code of the note.</param>
    [Theory]
    [MemberData(nameof(UndrawnLinks))]
    public void CanvasNetViewer_Render_UndrawnLinkWithStrict_ThrowsUnsupportedFeature(string href, string code)
    {
        // Arrange
        using var folder = CreateFolder();
        var path = folder.WriteText("diagram.svg", ViewerFixtures.Svg(ViewerFixtures.Cell(href, 0)));
        var strict = new ViewerRenderOptions { ThrowOnUnsupportedFeature = true };

        // Act
        var byPath = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(path, strict));
        using var stream = File.OpenRead(path);
        var byStream = Assert.Throws<ViewerException>(
            () => CanvasNetViewer.Render(stream, "diagram.svg", strict with { Folder = folder.Path }));

        // Assert
        foreach (var exception in new[] { byPath, byStream })
        {
            Assert.Equal(ErrorCodes.UnsupportedFeature, exception.Code);
            Assert.Contains("'diagram.svg'", exception.Message, StringComparison.Ordinal);
            Assert.Contains(href, exception.Message, StringComparison.Ordinal);
            Assert.Contains(code, exception.Hint, StringComparison.Ordinal);
        }
    }

    /// <summary>
    ///     Test that with the strict switch the warnings (an unknown fragment parameter) do not fail the render, and that a
    ///     link that is drawn is not an error.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_StrictWithOnlyWarnings_Succeeds()
    {
        // Arrange
        using var folder = CreateFolder();
        var path = folder.WriteText(
            "diagram.svg",
            ViewerFixtures.Svg(ViewerFixtures.Cell("report.pdf#page=2", 0) + ViewerFixtures.Cell("logo.png#x=1", 1)));

        // Act
        using var image = CanvasNetViewer.Render(path, new ViewerRenderOptions { ThrowOnUnsupportedFeature = true });

        // Assert
        Assert.Equal(FolderImageResolver.FragmentUnknownParameterCode, Assert.Single(image.Diagnostics).Code);
        Assert.Equal(2, image.Embedded.Count);
    }

    /// <summary>
    ///     Test that a link that is not drawn inside a nested SVG file fails a strict render and the message names the
    ///     nested file that holds the link.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_UndrawnLinkInNestedSvgWithStrict_NamesTheNestedFile()
    {
        // Arrange
        using var folder = CreateFolder();
        folder.WriteText("inner.svg", ViewerFixtures.Svg(ViewerFixtures.Cell("report.pdf#page=9", 0)));
        var path = folder.WriteText("outer.svg", ViewerFixtures.Svg(ViewerFixtures.Cell("inner.svg", 0)));

        // Act
        var exception = Assert.Throws<ViewerException>(
            () => CanvasNetViewer.Render(path, new ViewerRenderOptions { ThrowOnUnsupportedFeature = true }));

        // Assert
        Assert.Equal(ErrorCodes.UnsupportedFeature, exception.Code);
        Assert.Contains("'inner.svg'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("report.pdf#page=9", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the loader of the tool still refuses the same links in strict mode (its behavior does not change).
    /// </summary>
    /// <param name="href">The link.</param>
    /// <param name="code">The code of the note.</param>
    [Theory]
    [MemberData(nameof(UndrawnLinks))]
    public void SourceLoader_Load_UndrawnLinkWithStrict_StillThrowsUnsupportedFeature(string href, string code)
    {
        // Arrange
        using var folder = CreateFolder();
        var path = folder.WriteText("diagram.svg", ViewerFixtures.Svg(ViewerFixtures.Cell(href, 0)));

        // Act
        var exception = Assert.Throws<ViewerException>(() => SourceLoader.Load(path, new SourceOptions { Strict = true }));

        // Assert
        Assert.Equal(ErrorCodes.UnsupportedFeature, exception.Code);
        Assert.Contains("'diagram.svg'", exception.Message, StringComparison.Ordinal);
        Assert.Contains(code, exception.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the margin of a picture fitted into a box has the background that was chosen, and is transparent when
    ///     none was.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_FitIntoBox_MarginHasTheBackground()
    {
        // Arrange: a 4 by 4 picture in a 10 by 6 box has a margin of 2 pixels on the left and on the right
        using var folder = new ViewerTestFolder();
        var path = folder.WriteBytes("logo.png", ViewerFixtures.Png(4, 4, ViewerFixtures.Green));
        var red = new Rgba32(255, 0, 0, 255);

        // Act
        using var plain = CanvasNetViewer.Render(path, new ViewerRenderOptions { Width = 10, Height = 6 });
        using var colored = CanvasNetViewer.Render(path, new ViewerRenderOptions { Width = 10, Height = 6, Background = red });

        // Assert
        Assert.Equal((10, 6), (colored.Surface.Width, colored.Surface.Height));
        Assert.Equal(0, plain.Surface[0, 3].A);
        Assert.Equal(red, colored.Surface[0, 3]);
        Assert.Equal(red, colored.Surface[9, 3]);
        Assert.Equal(ViewerFixtures.Green, colored.Surface[5, 3]);
    }

    /// <summary>
    ///     Test that the margin of a document page fitted into a box has the chosen background too.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_PdfFitIntoBox_MarginHasTheBackground()
    {
        // Arrange: page 2 is portrait (2 to 3), so a wide box leaves a margin at the sides
        using var folder = new ViewerTestFolder();
        var path = folder.WriteBytes("report.pdf", FixtureFactory.CreatePdf());
        var red = new Rgba32(255, 0, 0, 255);

        // Act
        using var image = CanvasNetViewer.Render(
            path,
            new ViewerRenderOptions { Page = 2, Width = 90, Height = 60, Background = red });

        // Assert
        Assert.Equal((90, 60), (image.Surface.Width, image.Surface.Height));
        Assert.Equal(red, image.Surface[1, 30]);
        Assert.Equal(red, image.Surface[88, 30]);
    }

    /// <summary>
    ///     Test that a position in a picture follows the picture into a box: the fit reports a resize and a pad, and the
    ///     mapped position is where the same point of the picture lands.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_FitIntoBox_AnchorsFollowTheScaleAndOffset()
    {
        // Arrange: 20 by 10 with a red 2 by 2 mark centered at (15, 5), fitted into 40 by 40 (scale 2, top margin 10)
        using var source = ViewerFixtures.Solid(20, 10, new Rgba32(255, 255, 255, 255));
        var red = new Rgba32(255, 0, 0, 255);
        for (var y = 4; y < 6; y++)
        {
            for (var x = 14; x < 16; x++)
            {
                source[x, y] = red;
            }
        }

        var anchors = new[] { new SourceAnchor("mark", 0, 0, 0, 15, 5), new SourceAnchor("lost", 0, 0, 0, null, null) };
        var steps = new List<AppliedOperation>();

        // Act
        using var fitted = CanvasNetViewer.FitIntoBox(source, 40, 40, new Rgba32(0, 0, 0, 0), steps);
        var mapped = ImagePipeline.MapAnchors(anchors, source.Width, source.Height, steps);

        // Assert: the mark is at (30, 20) in the box, and so is the mapped anchor; a lost position stays lost
        Assert.Equal(["resize", "pad"], steps.Select(s => s.Op));
        Assert.Equal((30.0, 20.0), (mapped[0].PixelX, mapped[0].PixelY));
        Assert.Equal(red, fitted[30, 20]);
        Assert.Null(mapped[1].PixelX);
    }

    /// <summary>
    ///     Test that a model rendered into a box reports its anchors at pixel positions inside the picture that is
    ///     returned, also with a box of another shape than the model.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_ScadWithBox_AnchorsAreInsideTheReturnedPicture()
    {
        // Arrange
        using var folder = new ViewerTestFolder();
        var path = folder.WriteText("model.scad", "cube([10, 10, 10], center = true);\necho(\"anchor\", \"top\", [0, 0, 5]);\n");

        // Act
        using var image = CanvasNetViewer.Render(path, new ViewerRenderOptions { Width = 120, Height = 40 });

        // Assert
        var anchor = Assert.Single(image.Anchors);
        Assert.Equal((120, 40), (image.Surface.Width, image.Surface.Height));
        Assert.InRange(anchor.PixelX!.Value, 0, 120);
        Assert.InRange(anchor.PixelY!.Value, 0, 40);
    }
}
