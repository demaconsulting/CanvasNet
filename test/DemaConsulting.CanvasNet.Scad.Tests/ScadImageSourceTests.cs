// cspell:ignore scad openscad
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Scad.Tests;

/// <summary>
///     Tests of <see cref="ScadImageSource"/>: an OpenSCAD model as an embedded picture - the size of its placement, a
///     transparent background, includes confined to the folder of the file, and notes and echo lines as diagnostics.
/// </summary>
public class ScadImageSourceTests
{
    /// <summary>A model of one cube.</summary>
    private const string Cube = "cube(10, center = true);";

    /// <summary>
    ///     Creates a request for a model.
    /// </summary>
    /// <param name="source">The text of the model.</param>
    /// <param name="width">The requested width, or <see langword="null"/>.</param>
    /// <param name="height">The requested height, or <see langword="null"/>.</param>
    /// <param name="directory">The folder of the file, or <see langword="null"/>.</param>
    /// <param name="strict">Whether an unsupported feature must throw.</param>
    /// <param name="remaining">The pixels the source may allocate.</param>
    /// <returns>The request.</returns>
    private static ImageSourceRequest Request(
        string source,
        int? width = null,
        int? height = null,
        string? directory = null,
        bool strict = false,
        long remaining = long.MaxValue)
    {
        return new ImageSourceRequest
        {
            Href = "part.scad",
            FileName = "part.scad",
            Content = System.Text.Encoding.UTF8.GetBytes(source),
            Directory = directory,
            PixelWidth = width,
            PixelHeight = height,
            ThrowOnUnsupportedFeature = strict,
            RemainingPixels = remaining
        };
    }

    /// <summary>
    ///     Determines whether a surface has a pixel that is drawn and one that is transparent.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <returns><see langword="true"/> when the surface has both.</returns>
    private static bool HasModelOnTransparentBackground(Surface surface)
    {
        var drawn = false;
        var transparent = false;
        for (var y = 0; y < surface.Height; y += 4)
        {
            for (var x = 0; x < surface.Width; x += 4)
            {
                if (surface[x, y].A == 0)
                {
                    transparent = true;
                }
                else
                {
                    drawn = true;
                }
            }
        }

        return drawn && transparent;
    }

    /// <summary>
    ///     Test that a model without a requested size is drawn at 800 by 800 pixels on a transparent background.
    /// </summary>
    [Fact]
    public void ScadImageSource_Render_NoRequestedSize_DrawsNaturalSizeOnTransparentBackground()
    {
        // Arrange
        var source = new ScadImageSource();

        // Act
        var result = source.Render(Request(Cube));
        using var surface = result.Surface;

        // Assert
        Assert.Equal((ScadImageSource.NaturalSize, ScadImageSource.NaturalSize), (surface.Width, surface.Height));
        Assert.Equal(800, ScadImageSource.NaturalSize);
        Assert.Equal(0, surface[0, 0].A);
        Assert.True(HasModelOnTransparentBackground(surface));
        Assert.Empty(result.Diagnostics);
        Assert.Empty(result.Echo);
    }

    /// <summary>
    ///     Test that the model is drawn at exactly the requested box, and that one requested dimension keeps the aspect
    ///     ratio of the natural (square) size.
    /// </summary>
    [Fact]
    public void ScadImageSource_Render_RequestedSize_DrawsAtThatSize()
    {
        // Arrange
        var source = new ScadImageSource();

        // Act
        using var boxed = source.Render(Request(Cube, 60, 40)).Surface;
        using var widthOnly = source.Render(Request(Cube, 50)).Surface;

        // Assert
        Assert.Equal((60, 40), (boxed.Width, boxed.Height));
        Assert.Equal((50, 50), (widthOnly.Width, widthOnly.Height));
        Assert.True(HasModelOnTransparentBackground(boxed));
    }

    /// <summary>
    ///     Test that a size beyond <see cref="ImageSourceRequest.RemainingPixels"/> is refused with
    ///     <see cref="ImageSourceBudgetException"/> before the model is drawn (the allocation stays far below the 64 MB
    ///     of the picture), and that a size that exactly fits is drawn.
    /// </summary>
    [Fact]
    public void ScadImageSource_Render_SizeBeyondRemainingPixels_IsRefusedBeforeDrawing()
    {
        // Arrange
        var source = new ScadImageSource();

        // Act: a 4000 by 4000 picture against a budget of 1000 pixels
        var before = GC.GetAllocatedBytesForCurrentThread();
        var ex = Assert.Throws<ImageSourceBudgetException>(() => source.Render(Request(Cube, 4000, 4000, remaining: 1000)));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        using var fits = source.Render(Request(Cube, 60, 40, remaining: 2400)).Surface;

        // Assert
        Assert.Equal((16_000_000L, 1000L), (ex.RequestedPixels, ex.RemainingPixels));
        Assert.True(allocated < 32 * 1024 * 1024, $"{allocated} bytes were allocated before the refusal.");
        Assert.Equal((60, 40), (fits.Width, fits.Height));
    }

    /// <summary>
    ///     Test that the echo output and an unsupported feature of the model are returned, with the source name
    ///     <c>scad</c>, and that the model is still drawn.
    /// </summary>
    [Fact]
    public void ScadImageSource_Render_ModelWithEchoAndUnsupportedFeature_ReturnsEchoAndDiagnostics()
    {
        // Arrange
        const string model = "echo(\"from-the-model\");\noffset(r = 1) square(2);\ncube(5);\n";

        // Act
        var result = new ScadImageSource().Render(Request(model, 40, 40));
        using var surface = result.Surface;

        // Assert
        Assert.Contains(result.Echo, line => line.Contains("from-the-model", StringComparison.Ordinal));
        var note = Assert.Single(result.Diagnostics, d => d.Severity == ImageDiagnosticSeverity.Unsupported);
        Assert.Equal("scad", note.Source);
        Assert.False(note.Message.Contains('\n', StringComparison.Ordinal));
        Assert.NotEmpty(note.Code);
    }

    /// <summary>
    ///     Test that an unsupported feature throws when the request asks for it.
    /// </summary>
    [Fact]
    public void ScadImageSource_Render_UnsupportedFeatureWhenStrict_ThrowsUnsupportedFeatureException()
    {
        // Arrange
        const string model = "offset(r = 1) square(2);\n";

        // Act / Assert
        Assert.Throws<ScadUnsupportedFeatureException>(() => new ScadImageSource().Render(Request(model, strict: true)));
    }

    /// <summary>
    ///     Test that an <c>include</c> is looked up in the folder of the file: found there, reported as a warning when the
    ///     folder has no such file, and a failure when the request has no folder at all.
    /// </summary>
    [Fact]
    public void ScadImageSource_Render_Include_ResolvesFromDirectoryOfRequest()
    {
        // Arrange: a library next to the model
        var folder = Path.Combine(Path.GetTempPath(), "scad-image-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(Path.Combine(folder, "lib.scad"), "module part() { cube(8, center = true); }");
            const string model = "include <lib.scad>\npart();\n";
            var source = new ScadImageSource();

            // Act
            var found = source.Render(Request(model, 40, 40, folder));
            var missing = source.Render(Request(model, 40, 40, Path.GetTempPath()));
            using var foundSurface = found.Surface;
            using var missingSurface = missing.Surface;

            // Assert: the include was found beside the file, and was reported as a warning when the folder lacks it
            Assert.Empty(found.Diagnostics);
            Assert.True(HasModelOnTransparentBackground(foundSurface));
            var warning = Assert.Single(missing.Diagnostics, d => d.Message.Contains("lib.scad", StringComparison.Ordinal));
            Assert.Equal(("scad", ImageDiagnosticSeverity.Warning, "scad-warning"), (warning.Source, warning.Severity, warning.Code));
            Assert.Contains("lib.scad", warning.Message, StringComparison.Ordinal);
            Assert.Throws<ScadEvaluationException>(() => source.Render(Request(model, 40, 40)));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>
    ///     Test that a model with a syntax error and a request without file content fail.
    /// </summary>
    [Fact]
    public void ScadImageSource_Render_InvalidInput_Throws()
    {
        // Arrange
        var source = new ScadImageSource();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act / Assert
        Assert.Throws<ScadSyntaxException>(() => source.Render(Request("cube(;")));
        Assert.Throws<InvalidOperationException>(() => source.Render(new ImageSourceRequest { Href = "camera:front" }));
        Assert.Throws<ArgumentNullException>(() => source.Render(null!));
        Assert.Throws<OperationCanceledException>(() => source.Render(new ImageSourceRequest { Href = "a", FileName = "a.scad", CancellationToken = cts.Token }));
    }

    /// <summary>
    ///     Test the identity of the source: its name, its extension, and that it claims no scheme.
    /// </summary>
    [Fact]
    public void ScadImageSource_Properties_NameExtensionAndNoSchemes()
    {
        // Arrange
        var source = new ScadImageSource();

        // Act / Assert
        Assert.Equal("scad", source.Name);
        Assert.Equal([".scad"], source.Extensions);
        Assert.Empty(source.Schemes);
    }
}
