// cspell:ignore canvasnet pptx scad ssim rmse
using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Tool.Imaging;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Unit tests for the CompareCommand class with different kinds of input: sizes and size normalization, documents,
///     models, the background, and the per-side options.
/// </summary>
[Collection("Sequential")]
public class CompareCommandSourceTests
{
    /// <summary>
    ///     The red of the solid test images.
    /// </summary>
    private static readonly Rgba32 Red = new(255, 0, 0, 255);

    /// <summary>
    ///     Gets the <c>metrics</c> object of a JSON result.
    /// </summary>
    /// <param name="document">The parsed result.</param>
    /// <returns>The metrics.</returns>
    private static JsonElement Metrics(JsonDocument document)
    {
        return document.RootElement.GetProperty("metrics");
    }

    /// <summary>
    ///     Writes the two-slide presentation of the fixtures.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <param name="name">The file name.</param>
    /// <param name="withMetafile"><see langword="true"/> for a slide with a picture the renderer skips with a note.</param>
    /// <returns>The path.</returns>
    private static string WritePptx(TempDirectory directory, string name = "deck.pptx", bool withMetafile = false)
    {
        var path = directory.Combine(name);
        File.WriteAllBytes(path, FixtureFactory.CreatePptx(withMetafile: withMetafile));
        return path;
    }

    /// <summary>
    ///     Test that inputs of different sizes are an error unless a size is chosen, and that the hint says how.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_DifferentSizesWithoutResize_ThrowsSizeMismatchWithHint()
    {
        // Arrange: 8x6 and 4x3
        using var directory = new TempDirectory();
        var a = CompareCommandTests.SaveSolid(directory, "a.png", 8, 6, Red);
        var b = CompareCommandTests.SaveSolid(directory, "b.png", 4, 3, Red);

        // Act
        var exception = CompareCommandTests.RunFailing(a, b);

        // Assert: the code, both sizes, and the way out
        Assert.Equal(ErrorCodes.SizeMismatch, exception.Code);
        Assert.Contains("8x6", exception.Message);
        Assert.Contains("4x3", exception.Message);
        Assert.Contains("--resize-to", exception.Hint);
    }

    /// <summary>
    ///     Test that --resize-to a fits the second raster image to the size of the first.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_ResizeToA_FitsRasterBToA()
    {
        // Arrange: a red 8x6 image and a red 4x3 image of the same aspect ratio
        using var directory = new TempDirectory();
        var a = CompareCommandTests.SaveSolid(directory, "a.png", 8, 6, Red);
        var b = CompareCommandTests.SaveSolid(directory, "b.png", 4, 3, Red);

        // Act
        using var document = JsonDocument.Parse(CompareCommandTests.Run(true, a, b, "--resize-to", "a"));

        // Assert: B was enlarged by a fit and both sides report 8x6; the result says what was asked
        var root = document.RootElement;
        Assert.Equal((4, 3, 8, 6), (root.GetProperty("b").GetProperty("loadedWidth").GetInt32(), root.GetProperty("b").GetProperty("loadedHeight").GetInt32(), root.GetProperty("b").GetProperty("width").GetInt32(), root.GetProperty("b").GetProperty("height").GetInt32()));
        Assert.Equal((8, 6, 8, 6), (root.GetProperty("a").GetProperty("loadedWidth").GetInt32(), root.GetProperty("a").GetProperty("loadedHeight").GetInt32(), root.GetProperty("a").GetProperty("width").GetInt32(), root.GetProperty("a").GetProperty("height").GetInt32()));
        Assert.Contains(root.GetProperty("b").GetProperty("operations").EnumerateArray(), o => o.GetProperty("op").GetString() == "fit");
        Assert.DoesNotContain(root.GetProperty("a").GetProperty("operations").EnumerateArray(), o => o.GetProperty("op").GetString() == "fit");
        Assert.Equal("a", root.GetProperty("resize").GetProperty("to").GetString());
        Assert.Equal("contain", root.GetProperty("resize").GetProperty("fit").GetString());
        Assert.InRange(Metrics(document).GetProperty("maxChannelDiff").GetInt32(), 0, 1);
    }

    /// <summary>
    ///     Test that --resize-to b fits the first raster image to the size of the second.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_ResizeToB_FitsAToB()
    {
        // Arrange: a red 16x12 image and a red 8x6 image
        using var directory = new TempDirectory();
        var a = CompareCommandTests.SaveSolid(directory, "a.png", 16, 12, Red);
        var b = CompareCommandTests.SaveSolid(directory, "b.png", 8, 6, Red);

        // Act
        using var document = JsonDocument.Parse(CompareCommandTests.Run(true, a, b, "--resize-to", "b"));

        // Assert: A was shrunk to 8x6
        var root = document.RootElement;
        Assert.Equal((16, 8), (root.GetProperty("a").GetProperty("loadedWidth").GetInt32(), root.GetProperty("a").GetProperty("width").GetInt32()));
        Assert.Equal((8, 6), (root.GetProperty("size").GetProperty("width").GetInt32(), root.GetProperty("size").GetProperty("height").GetInt32()));
        Assert.Equal("b", root.GetProperty("resize").GetProperty("to").GetString());
        Assert.InRange(Metrics(document).GetProperty("maxChannelDiff").GetInt32(), 0, 1);
    }

    /// <summary>
    ///     Test that --resize-to with a size fits both images to it.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_ResizeToExplicitSize_FitsBoth()
    {
        // Arrange: red 8x6 and red 4x3
        using var directory = new TempDirectory();
        var a = CompareCommandTests.SaveSolid(directory, "a.png", 8, 6, Red);
        var b = CompareCommandTests.SaveSolid(directory, "b.png", 4, 3, Red);

        // Act: a square target
        using var document = JsonDocument.Parse(CompareCommandTests.Run(true, a, b, "--resize-to", "10x10", "--fit", "stretch"));

        // Assert: both were resampled to exactly 10x10
        var root = document.RootElement;
        Assert.Equal((10, 10), (root.GetProperty("size").GetProperty("width").GetInt32(), root.GetProperty("size").GetProperty("height").GetInt32()));
        foreach (var name in new[] { "a", "b" })
        {
            var side = root.GetProperty(name);
            Assert.Equal((10, 10), (side.GetProperty("width").GetInt32(), side.GetProperty("height").GetInt32()));
            Assert.Contains(side.GetProperty("operations").EnumerateArray(), o => o.GetProperty("op").GetString() == "resize");
        }

        Assert.Equal("10x10", root.GetProperty("resize").GetProperty("to").GetString());
        Assert.Equal("stretch", root.GetProperty("resize").GetProperty("fit").GetString());
        Assert.InRange(Metrics(document).GetProperty("maxChannelDiff").GetInt32(), 0, 1);
    }

    /// <summary>
    ///     Test that a target size larger than the maximum is refused.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_ResizeToTooLarge_ThrowsSizeLimit()
    {
        // Arrange
        using var directory = new TempDirectory();
        var (a, b) = CompareCommandTests.SavePair(directory);

        // Act
        var exception = CompareCommandTests.RunFailing(a, b, "--resize-to", "9000x10");

        // Assert
        Assert.Equal(ErrorCodes.SizeLimit, exception.Code);
    }

    /// <summary>
    ///     Test that an unknown target or fit mode is a usage error.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_InvalidResizeToOrFit_ThrowsUsageError()
    {
        // Arrange
        using var directory = new TempDirectory();
        var (a, b) = CompareCommandTests.SavePair(directory);

        // Act
        var target = CompareCommandTests.RunFailing(a, b, "--resize-to", "c");
        var fit = CompareCommandTests.RunFailing(a, b, "--fit", "cover");

        // Assert
        Assert.Equal(ErrorCodes.UsageError, target.Code);
        Assert.Equal(ErrorCodes.UsageError, fit.Code);
    }

    /// <summary>
    ///     Test that contain letterboxes a raster image over the background and stretch fills the target.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_FitContainVsStretch_DifferInLetterboxing()
    {
        // Arrange: a red 20x10 image and a red 10x10 image
        using var directory = new TempDirectory();
        var a = CompareCommandTests.SaveSolid(directory, "a.png", 20, 10, Red);
        var b = CompareCommandTests.SaveSolid(directory, "b.png", 10, 10, Red);

        // Act
        using var contain = JsonDocument.Parse(CompareCommandTests.Run(true, a, b, "--resize-to", "a"));
        using var stretch = JsonDocument.Parse(CompareCommandTests.Run(true, a, b, "--resize-to", "a", "--fit", "stretch"));

        // Assert: contain keeps B square with a white margin on both sides (half of the pixels differ); stretch is identical
        Assert.Equal(100, Metrics(contain).GetProperty("diffPixels").GetInt64());
        Assert.Equal(0.5, Metrics(contain).GetProperty("diffPixelRatio").GetDouble());
        Assert.True(Metrics(stretch).GetProperty("identical").GetBoolean());
    }

    /// <summary>
    ///     Test that a vector input is rendered at the size of the reference, so that it equals the raster made from it.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_VectorAgainstItsRaster_RendersVectorNativelyAndIsIdentical()
    {
        // Arrange: the SVG fixture (100x80) and its raster at 200x160
        using var directory = new TempDirectory();
        var svg = FixtureFactory.WriteSource(directory.Path, "svg");
        var raster = directory.Combine("raster.png");
        using (var loaded = SourceLoader.Load(svg, new SourceOptions { Width = 200, Height = 160 }))
        {
            PngCodec.Save(loaded.Surface, raster);
        }

        // Act: the raster is the reference
        using var document = JsonDocument.Parse(CompareCommandTests.Run(true, raster, svg, "--resize-to", "a"));

        // Assert: the SVG was rendered at 200x160 (not resampled), so the images are identical
        var side = document.RootElement.GetProperty("b");
        Assert.Equal((200, 160), (side.GetProperty("loadedWidth").GetInt32(), side.GetProperty("loadedHeight").GetInt32()));
        Assert.DoesNotContain(side.GetProperty("operations").EnumerateArray(), o => o.GetProperty("op").GetString() is "fit" or "resize");
        Assert.True(Metrics(document).GetProperty("identical").GetBoolean());
    }

    /// <summary>
    ///     Test that each side can choose its own page, and that a shared page applies to both.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_PerSidePages_CompareDifferentPagesOfDocuments()
    {
        // Arrange: the same two-slide presentation on both sides
        using var directory = new TempDirectory();
        var deck = WritePptx(directory);

        // Act: slide 1 against slide 2, then slide 1 against slide 1 (a shared page)
        using var different = JsonDocument.Parse(CompareCommandTests.Run(true, deck, deck, "--a-page", "1", "--b-page", "2"));
        using var same = JsonDocument.Parse(CompareCommandTests.Run(true, deck, deck, "--page", "1"));
        using var overridden = JsonDocument.Parse(CompareCommandTests.Run(true, deck, deck, "--page", "1", "--b-page", "2"));

        // Assert: different slides differ; the same slide is identical; a per-side page wins over the shared one
        Assert.False(Metrics(different).GetProperty("identical").GetBoolean());
        Assert.Equal(1, different.RootElement.GetProperty("a").GetProperty("page").GetInt32());
        Assert.Equal(2, different.RootElement.GetProperty("b").GetProperty("page").GetInt32());
        Assert.Equal(2, different.RootElement.GetProperty("b").GetProperty("pageCount").GetInt32());
        Assert.True(Metrics(same).GetProperty("identical").GetBoolean());
        Assert.Equal(1, same.RootElement.GetProperty("b").GetProperty("page").GetInt32());
        Assert.False(Metrics(overridden).GetProperty("identical").GetBoolean());
    }

    /// <summary>
    ///     Test that the document text names the page of each document input.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_DocumentsInText_NamePages()
    {
        // Arrange
        using var directory = new TempDirectory();
        var deck = WritePptx(directory);

        // Act
        var stdOut = CompareCommandTests.Run(false, deck, deck, "--a-page", "1", "--b-page", "2", "--grid", "1x1");

        // Assert
        Assert.StartsWith($"compared {deck} page 1 with {deck} page 2 (480x360): ", stdOut);
    }

    /// <summary>
    ///     Test that a per-side option for the wrong kind of input is a usage error.
    /// </summary>
    /// <param name="option">The option.</param>
    /// <param name="value">Its value.</param>
    [Theory]
    [InlineData("--a-page", "2")]
    [InlineData("--b-dpi", "96")]
    [InlineData("--a-view", "top")]
    [InlineData("--b-define", "x=1")]
    public void CompareCommand_Run_PerSideOptionsOnWrongKind_ThrowUsageError(string option, string value)
    {
        // Arrange: two PNG images
        using var directory = new TempDirectory();
        var (a, b) = CompareCommandTests.SavePair(directory);

        // Act
        var exception = CompareCommandTests.RunFailing(a, b, option, value);

        // Assert: the option and the kind of file are named
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
        Assert.Contains("png", exception.Message);
    }

    /// <summary>
    ///     Test that shared model options apply to the model only, and are skipped for a raster image.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_ScadSharedOptions_ApplyToModelSideOnly()
    {
        // Arrange: a PNG and the model of the fixtures
        using var directory = new TempDirectory();
        var png = CompareCommandTests.SaveSolid(directory, "a.png", 40, 40, new Rgba32(255, 255, 255, 255));
        var scad = FixtureFactory.WriteSource(directory.Path, "scad");

        // Act: the model viewed from the top, rendered at the size of the PNG
        using var top = JsonDocument.Parse(CompareCommandTests.Run(true, png, scad, "--view", "top", "--resize-to", "a"));
        using var front = JsonDocument.Parse(CompareCommandTests.Run(true, png, scad, "--view", "front", "--resize-to", "a"));

        // Assert: the model has the size of the PNG, and the view changes it
        Assert.Equal("scad", top.RootElement.GetProperty("b").GetProperty("format").GetString());
        Assert.Equal((40, 40), (top.RootElement.GetProperty("b").GetProperty("width").GetInt32(), top.RootElement.GetProperty("b").GetProperty("height").GetInt32()));
        Assert.NotEqual(Metrics(top).GetProperty("mae").GetDouble(), Metrics(front).GetProperty("mae").GetDouble());
    }

    /// <summary>
    ///     Test that model options are a usage error when neither input is a model.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_ScadOptionsWithoutModel_ThrowsUsageError()
    {
        // Arrange
        using var directory = new TempDirectory();
        var (a, b) = CompareCommandTests.SavePair(directory);

        // Act
        var exception = CompareCommandTests.RunFailing(a, b, "--view", "top");

        // Assert
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
        Assert.Contains("only apply to an OpenSCAD model", exception.Message);
    }

    /// <summary>
    ///     Test that a variable of one side wins over a shared variable of the same name.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_PerSideDefines_OverrideSharedDefines()
    {
        // Arrange: the same model on both sides
        using var directory = new TempDirectory();
        var scad = FixtureFactory.WriteSource(directory.Path, "scad");

        // Act: a shared width, then a second side with its own width
        using var shared = JsonDocument.Parse(CompareCommandTests.Run(true, scad, scad, "-D", "width=10", "--resize-to", "48x48"));
        using var overridden = JsonDocument.Parse(CompareCommandTests.Run(true, scad, scad, "-D", "width=10", "--b-define", "width=30", "--resize-to", "48x48"));

        // Assert: the shared definition gives two equal images; the override changes the second
        Assert.True(Metrics(shared).GetProperty("identical").GetBoolean());
        Assert.False(Metrics(overridden).GetProperty("identical").GetBoolean());
    }

    /// <summary>
    ///     Test that a page that does not exist names the side it was chosen for.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_PageOutOfRange_NamesTheSide()
    {
        // Arrange
        using var directory = new TempDirectory();
        var deck = WritePptx(directory);

        // Act
        var exception = CompareCommandTests.RunFailing(deck, deck, "--b-page", "5");

        // Assert
        Assert.Equal(ErrorCodes.PageOutOfRange, exception.Code);
        Assert.StartsWith("Input b: ", exception.Message);
        Assert.Contains("Page 5", exception.Message);
    }

    /// <summary>
    ///     Test that transparency is flattened over the background on both sides.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_Background_FlattensTransparencyBothSides()
    {
        // Arrange: a half transparent blue image and the same blue at half brightness
        using var directory = new TempDirectory();
        var translucent = CompareCommandTests.SaveSolid(directory, "a.png", 4, 4, new Rgba32(0, 0, 255, 128));
        var opaque = CompareCommandTests.SaveSolid(directory, "b.png", 4, 4, new Rgba32(0, 0, 128, 255));

        // Act: over black the two agree; over the default white they do not
        using var black = JsonDocument.Parse(CompareCommandTests.Run(true, translucent, opaque, "--background", "black", "--grid", "1x1"));
        using var white = JsonDocument.Parse(CompareCommandTests.Run(true, translucent, opaque, "--grid", "1x1"));

        // Assert
        Assert.InRange(Metrics(black).GetProperty("maxChannelDiff").GetInt32(), 0, 1);
        Assert.Equal("#000000", black.RootElement.GetProperty("options").GetProperty("background").GetString());
        Assert.True(Metrics(white).GetProperty("maxChannelDiff").GetInt32() > 100);
    }

    /// <summary>
    ///     Test that a background that is not opaque is a usage error.
    /// </summary>
    /// <param name="background">The value of <c>--background</c>.</param>
    [Theory]
    [InlineData("transparent")]
    [InlineData("#ff000080")]
    public void CompareCommand_Run_TransparentBackground_ThrowsUsageError(string background)
    {
        // Arrange
        using var directory = new TempDirectory();
        var (a, b) = CompareCommandTests.SavePair(directory);

        // Act
        var exception = CompareCommandTests.RunFailing(a, b, "--background", background);

        // Assert
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
        Assert.Contains("opaque", exception.Message);
    }

    /// <summary>
    ///     Test that a note about an unsupported feature is listed with its side, and that strict mode fails.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_Strict_UnsupportedFeature_Fails()
    {
        // Arrange: a presentation with a picture the renderer cannot draw, and a plain one
        using var directory = new TempDirectory();
        var unsupported = WritePptx(directory, "unsupported.pptx", withMetafile: true);
        var plain = WritePptx(directory, "plain.pptx");

        // Act
        using var document = JsonDocument.Parse(CompareCommandTests.Run(true, plain, unsupported));
        var strict = CompareCommandTests.RunFailing(plain, unsupported, "--strict");

        // Assert: the note names the side that has it; strict mode is an unsupported-feature error
        var note = Assert.Single(document.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal("b", note.GetProperty("side").GetString());
        Assert.Equal("unsupported", note.GetProperty("severity").GetString());
        Assert.Equal(ErrorCodes.UnsupportedFeature, strict.Code);
    }
}
