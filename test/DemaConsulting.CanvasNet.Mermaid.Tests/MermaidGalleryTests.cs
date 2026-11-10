using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Mermaid.Tests;

/// <summary>
///     System-level tests of the documentation gallery: every diagram <c>docs/user_guide/examples/mermaid-*.mmd</c> (copied
///     next to the test assembly as <c>Gallery</c>) is opened and rendered with the strict policy, so a diagram of the
///     gallery can never use a feature that is not drawn, never raise a warning, and never come out blank between two
///     builds of the user guide.
/// </summary>
public class MermaidGalleryTests
{
    /// <summary>The fewest diagrams the gallery must hold.</summary>
    private const int MinimumDiagrams = 16;

    /// <summary>The diagram types this version draws, which the gallery shows.</summary>
    private static readonly string[] DrawnTypes = ["flowchart", "sequence", "class", "state", "er"];

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     A resolver that hands out a solid picture of the requested size for every link and records the links it was asked for.
    /// </summary>
    private sealed class SolidResolver : IImageResolver
    {
        /// <summary>Gets the links that were asked for.</summary>
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
    ///     Gets the folder of the gallery diagrams.
    /// </summary>
    /// <returns>The folder.</returns>
    private static string GalleryFolder() => Path.Combine(AppContext.BaseDirectory, "Gallery");

    /// <summary>
    ///     Gets the names of the diagrams of the gallery, for the theory.
    /// </summary>
    /// <returns>The file names, in order.</returns>
    public static TheoryData<string> Diagrams()
    {
        var data = new TheoryData<string>();
        foreach (var path in Directory.GetFiles(GalleryFolder(), "mermaid-*.mmd").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileName(path));
        }

        return data;
    }

    /// <summary>
    ///     Validates that the gallery holds at least sixteen diagrams, that every one is a supported diagram of a type this
    ///     version draws (a flowchart, a sequence diagram, a class diagram, a state diagram, or an entity relationship
    ///     diagram), and that every drawn type is shown.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_SystemIntegration_Gallery_HoldsEveryDrawnDiagramType()
    {
        // Arrange
        var files = Directory.GetFiles(GalleryFolder(), "mermaid-*.mmd");

        // Act
        var documents = files.Select(path => MermaidDocument.Open(path)).ToList();

        // Assert: enough diagrams, each of a type this version draws, and every drawn type present
        Assert.True(files.Length >= MinimumDiagrams, $"the gallery holds {files.Length} diagrams");
        Assert.All(documents, d => Assert.Contains(d.DiagramType, DrawnTypes));
        Assert.All(documents, d => Assert.True(d.IsSupported));
        Assert.All(DrawnTypes, type => Assert.Contains(documents, d => d.DiagramType == type));
    }

    /// <summary>
    ///     Validates that a diagram of the gallery renders with the strict policy: no feature is reported, no warning is
    ///     raised, and the picture is not blank.
    /// </summary>
    /// <param name="name">The file name of the diagram.</param>
    [Theory]
    [MemberData(nameof(Diagrams))]
    public void CanvasNetMermaid_SystemIntegration_Gallery_RendersStrictWithoutWarnings(string name)
    {
        // Arrange: the pictures that a node names are given by a resolver that records them
        var diagram = MermaidDocument.Open(Path.Combine(GalleryFolder(), name));
        var warnings = new List<string>();
        var resolver = new SolidResolver();
        var options = new MermaidRenderOptions
        {
            ThrowOnUnsupportedFeature = true,
            ImageResolver = resolver,
            Background = new Rgba32(255, 255, 255, 255),
            WarningHandler = warning => warnings.Add(warning.ToString()),
        };

        // Act
        var size = diagram.Measure(options, Ct);
        using var surface = diagram.Render(options, Ct);

        // Assert: nothing reported, the picture has the measured size, and more than a few pixels differ from the corner
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
        if (name.Contains("pictures", StringComparison.Ordinal))
        {
            Assert.Contains("bracket.scad", resolver.Links);
            Assert.Contains("swatch.svg", resolver.Links);
            Assert.Contains("stall-sales.vl.json", resolver.Links);
        }
    }
}
