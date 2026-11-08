using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     System-level tests of the documentation gallery: every specification under <c>docs/user_guide/examples/gallery</c> (copied next to the test assembly as <c>Gallery</c>) is opened and rendered with the
///     strict policy, so a chart of the gallery can never use a feature that is not drawn, never raise a warning, and never come out blank between two builds of the user guide.
/// </summary>
public class VegaLiteGalleryTests
{
    /// <summary>The fewest specifications the gallery must hold.</summary>
    private const int MinimumSpecifications = 16;

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     A resolver that hands out a solid picture of the requested size for every link and records the links it was asked for.
    /// </summary>
    private sealed class SolidResolver : IImageResolver
    {
        /// <summary>The links that were asked for.</summary>
        public List<string> Links { get; } = [];

        /// <inheritdoc />
        public Surface? Resolve(string href, int pixelWidth, int pixelHeight)
        {
            Links.Add(href);
            var surface = new Surface(pixelWidth, pixelHeight);
            surface.Clear(new Rgba32(0x2a, 0x9d, 0x8f, 255));
            return surface;
        }
    }

    /// <summary>
    ///     Gets the folder of the gallery specifications.
    /// </summary>
    /// <returns>The folder.</returns>
    private static string GalleryFolder() => Path.Combine(AppContext.BaseDirectory, "Gallery");

    /// <summary>
    ///     Gets the names of the specifications of the gallery, for the theory.
    /// </summary>
    /// <returns>The file names, in order.</returns>
    public static TheoryData<string> Specifications()
    {
        var data = new TheoryData<string>();
        foreach (var path in Directory.GetFiles(GalleryFolder(), "*.vl.json").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileName(path));
        }

        return data;
    }

    /// <summary>
    ///     Validates that the gallery holds at least sixteen specifications, each with a description and the Vega-Lite 6 schema.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Gallery_HoldsDescribedSpecifications()
    {
        // Arrange
        var files = Directory.GetFiles(GalleryFolder(), "*.vl.json");

        // Act
        var texts = files.Select(File.ReadAllText).ToList();

        // Assert: enough charts, each says what it shows and which schema it follows
        Assert.True(files.Length >= MinimumSpecifications, $"the gallery holds {files.Length} specifications");
        Assert.All(texts, t => Assert.Contains("\"description\"", t, StringComparison.Ordinal));
        Assert.All(texts, t => Assert.Contains("https://vega.github.io/schema/vega-lite/v6.json", t, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that a specification of the gallery renders with the strict policy: no feature is reported, no warning is raised, and the picture is not blank.
    /// </summary>
    /// <param name="name">The file name of the specification.</param>
    [Theory]
    [MemberData(nameof(Specifications))]
    public void CanvasNetVegaLite_SystemIntegration_Gallery_RendersStrictWithoutWarnings(string name)
    {
        // Arrange: the pictures that an image mark names are given by a resolver that records them
        var chart = VegaLiteDocument.Open(Path.Combine(GalleryFolder(), name));
        var warnings = new List<string>();
        var resolver = new SolidResolver();
        var options = new VegaLiteRenderOptions
        {
            ThrowOnUnsupportedFeature = true,
            ImageResolver = resolver,
            WarningHandler = warning => warnings.Add(warning.Message),
        };

        // Act
        var size = chart.Measure(options, Ct);
        using var surface = chart.Render(options, Ct);

        // Assert: nothing reported, the picture has the measured size, and more than a few pixels differ from the background in the corner
        Assert.Empty(warnings);
        Assert.Equal((size.Width, size.Height), (surface.Width, surface.Height));
        var background = surface[0, 0];
        var differing = 0;
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                if (surface[x, y] != background)
                {
                    differing++;
                }
            }
        }

        Assert.True(differing > surface.Width * surface.Height / 50, $"{name} is blank");
        if (name.Contains("image", StringComparison.Ordinal))
        {
            Assert.Contains("gallery-badge.svg", resolver.Links);
            Assert.Contains("gallery-flag.svg", resolver.Links);
        }
    }
}
