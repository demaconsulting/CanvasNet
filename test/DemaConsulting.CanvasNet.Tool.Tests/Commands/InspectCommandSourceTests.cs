// cspell:ignore canvasnet exif scad pdf svg
using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Unit tests for the InspectCommand class with different kinds of input: a JPEG with an EXIF orientation, vector
///     sources and models, a page of a document, and transparency.
/// </summary>
[Collection("Sequential")]
public class InspectCommandSourceTests
{
    /// <summary>
    ///     Runs <c>inspect colors</c> with the JSON option and parses the result.
    /// </summary>
    /// <param name="args">The arguments after <c>colors</c>.</param>
    /// <returns>The document; the caller disposes it.</returns>
    private static JsonDocument Colors(params string[] args)
    {
        return JsonDocument.Parse(InspectCommandTests.Run(true, ["colors", .. args]));
    }

    /// <summary>
    ///     Reads the size of the analyzed image from the result.
    /// </summary>
    /// <param name="document">The result.</param>
    /// <returns>The width and the height.</returns>
    private static (int Width, int Height) ImageSize(JsonDocument document)
    {
        var image = document.RootElement.GetProperty("image");
        return (image.GetProperty("width").GetInt32(), image.GetProperty("height").GetInt32());
    }

    /// <summary>
    ///     Test that a JPEG with an EXIF orientation is analyzed upright unless the orientation is disabled.
    /// </summary>
    [Fact]
    public void InspectCommand_Colors_JpegWithExif_UsesUprightImage()
    {
        // Arrange: the 12x8 gradient stored with the orientation 6 (turn a quarter turn clockwise)
        using var directory = new TempDirectory();
        var path = directory.Combine("camera.jpg");
        using (var surface = FixtureFactory.CreateGradient())
        using (var stream = new MemoryStream())
        {
            JpegCodec.Save(surface, stream);
            File.WriteAllBytes(path, FixtureFactory.InsertExifOrientation(stream.ToArray(), 6, true));
        }

        // Act
        using var upright = Colors(path);
        using var stored = Colors(path, "--no-auto-orient");

        // Assert: the natural size is as stored; the analyzed image is upright (8x12) or as stored (12x8)
        Assert.Equal((12, 8), (upright.RootElement.GetProperty("input").GetProperty("natural").GetProperty("width").GetInt32(), upright.RootElement.GetProperty("input").GetProperty("natural").GetProperty("height").GetInt32()));
        Assert.Equal("jpeg", upright.RootElement.GetProperty("input").GetProperty("format").GetString());
        Assert.Equal((8, 12), ImageSize(upright));
        Assert.Equal((12, 8), ImageSize(stored));
        Assert.Contains(upright.RootElement.GetProperty("operations").EnumerateArray(), o => o.GetProperty("op").GetString() == "auto-orient");
        Assert.DoesNotContain(stored.RootElement.GetProperty("operations").EnumerateArray(), o => o.GetProperty("op").GetString() == "auto-orient");
    }

    /// <summary>
    ///     Test that an SVG drawing and an OpenSCAD model are rendered before they are analyzed.
    /// </summary>
    [Fact]
    public void InspectCommand_Colors_SvgAndScad_AreRenderedFirst()
    {
        // Arrange
        using var directory = new TempDirectory();
        var svg = FixtureFactory.WriteSource(directory.Path, "svg");
        var scad = FixtureFactory.WriteSource(directory.Path, "scad");

        // Act
        using var drawing = Colors(svg);
        using var sized = Colors(svg, "--width", "50");
        using var model = Colors(scad, "--view", "top");
        using var white = Colors(svg, "--count", "1");

        // Assert: the drawing is rendered at its natural size, or at the width asked for, flattened over white; the model is
        // drawn at 800x800
        Assert.Equal((100, 80), ImageSize(drawing));
        Assert.Equal((50, 40), ImageSize(sized));
        Assert.Equal("render", drawing.RootElement.GetProperty("operations")[0].GetProperty("op").GetString());
        Assert.Equal((800, 800), ImageSize(model));
        Assert.Equal("render", model.RootElement.GetProperty("operations")[0].GetProperty("op").GetString());
        Assert.NotEmpty(model.RootElement.GetProperty("colors").EnumerateArray());
        Assert.Equal("#FFFFFF", drawing.RootElement.GetProperty("background").GetString());
        Assert.Single(white.RootElement.GetProperty("colors").EnumerateArray());
    }

    /// <summary>
    ///     Test that the page chosen with --page of a document is the one analyzed.
    /// </summary>
    [Fact]
    public void InspectCommand_Colors_DocumentPage_UsesChosenPage()
    {
        // Arrange: the PDF of the fixtures has a landscape first page and a portrait second page
        using var directory = new TempDirectory();
        var pdf = FixtureFactory.WriteSource(directory.Path, "pdf");

        // Act
        using var first = Colors(pdf);
        using var second = Colors(pdf, "--page", "2");

        // Assert: the pages have different sizes, and the document facts name the page
        var firstSize = ImageSize(first);
        var secondSize = ImageSize(second);
        Assert.NotEqual(firstSize, secondSize);
        Assert.Equal((firstSize.Height, firstSize.Width), secondSize);
        Assert.Equal(1, first.RootElement.GetProperty("input").GetProperty("page").GetInt32());
        Assert.Equal(2, second.RootElement.GetProperty("input").GetProperty("page").GetInt32());
        Assert.Equal(2, second.RootElement.GetProperty("input").GetProperty("pageCount").GetInt32());
        Assert.Equal(ErrorCodes.PageOutOfRange, InspectCommandTests.RunFailing("colors", pdf, "--page", "3").Code);
    }

    /// <summary>
    ///     Test that transparent pixels are flattened over the background before they are counted.
    /// </summary>
    [Fact]
    public void InspectCommand_Colors_TransparentPng_FlattensOverBackground()
    {
        // Arrange: a fully transparent red image
        using var directory = new TempDirectory();
        var path = directory.Combine("clear.png");
        using (var surface = new Surface(10, 10))
        {
            surface.Clear(new Rgba32(255, 0, 0, 0));
            PngCodec.Save(surface, path);
        }

        // Act
        using var byDefault = Colors(path);
        using var green = Colors(path, "--background", "#00FF00");
        using var transparent = Colors(path, "--background", "transparent");
        using var translucent = Colors(path, "--background", "#0000FF80");

        // Assert: white by default (and for a transparent background), the background color when one is given
        // (a translucent one over white, counted once: the loader's flattening is not repeated by the analysis)
        var translucentHex = Assert.Single(translucent.RootElement.GetProperty("colors").EnumerateArray()).GetProperty("hex").GetString();
        Assert.Equal("#7F7FFF", translucent.RootElement.GetProperty("background").GetString());
        Assert.Equal("#7F7FFF", translucentHex);
        Assert.Equal("#FFFFFF", Assert.Single(byDefault.RootElement.GetProperty("colors").EnumerateArray()).GetProperty("hex").GetString());
        Assert.Equal("#FFFFFF", byDefault.RootElement.GetProperty("background").GetString());
        Assert.Equal("#00FF00", Assert.Single(green.RootElement.GetProperty("colors").EnumerateArray()).GetProperty("hex").GetString());
        Assert.Equal("#00FF00", green.RootElement.GetProperty("background").GetString());
        Assert.Equal("#FFFFFF", Assert.Single(transparent.RootElement.GetProperty("colors").EnumerateArray()).GetProperty("hex").GetString());
        Assert.Equal("#FFFFFF", transparent.RootElement.GetProperty("background").GetString());
        Assert.Equal(1.0, Assert.Single(green.RootElement.GetProperty("colors").EnumerateArray()).GetProperty("fraction").GetDouble());
    }
}
