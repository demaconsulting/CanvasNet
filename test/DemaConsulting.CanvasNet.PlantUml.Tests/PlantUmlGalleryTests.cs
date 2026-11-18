namespace DemaConsulting.CanvasNet.PlantUml.Tests;

/// <summary>
///     System-level tests of the documentation gallery: every diagram <c>docs/user_guide/examples/plantuml-*.puml</c>
///     (copied next to the test assembly as <c>Gallery</c>) is opened and rendered with the strict policy, so a diagram of
///     the gallery can never use a feature that is not drawn, never raise a warning, and never come out blank.
/// </summary>
public class PlantUmlGalleryTests
{
    /// <summary>The fewest diagrams the gallery must hold.</summary>
    private const int MinimumDiagrams = 8;

    /// <summary>The diagram types this version draws, which the gallery shows.</summary>
    private static readonly string[] DrawnTypes = ["sequence", "activity"];

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
        foreach (var path in Directory.GetFiles(GalleryFolder(), "plantuml-*.puml").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileName(path));
        }

        return data;
    }

    /// <summary>
    ///     Validates that the gallery holds at least eight diagrams, every one a supported diagram of a type this version
    ///     draws, and that every drawn type is shown.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Gallery_HoldsEveryDrawnDiagramType()
    {
        // Arrange
        var files = Directory.GetFiles(GalleryFolder(), "plantuml-*.puml");

        // Act
        var documents = files.Select(path => PlantUmlDocument.Open(path)).ToList();

        // Assert
        Assert.True(files.Length >= MinimumDiagrams, $"the gallery holds {files.Length} diagrams");
        Assert.All(documents, d => Assert.True(d.IsSupported));
        Assert.All(documents, d => Assert.Contains(d.DiagramType, DrawnTypes));
        Assert.All(DrawnTypes, type => Assert.Contains(documents, d => d.DiagramType == type));
    }

    /// <summary>
    ///     Validates that a diagram of the gallery renders with the strict policy: no feature is reported, no warning is
    ///     raised, the picture has its measured size, and it is not blank.
    /// </summary>
    /// <param name="name">The file name of the diagram.</param>
    [Theory]
    [MemberData(nameof(Diagrams))]
    public void CanvasNetPlantUml_Gallery_RendersStrictWithoutWarnings(string name)
    {
        // Arrange
        var diagram = PlantUmlDocument.Open(Path.Combine(GalleryFolder(), name));
        var (options, warnings, unsupported) = PlantUmlTestHelpers.Collecting(strict: true);

        // Act
        var size = diagram.Measure(options, TestContext.Current.CancellationToken);
        using var surface = diagram.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(warnings);
        Assert.Empty(unsupported);
        Assert.Equal((size.Width, size.Height), (surface.Width, surface.Height));
        var background = surface.GetRowSpan(0)[0];
        var differing = 0;
        for (var y = 0; y < surface.Height; y++)
        {
            foreach (var pixel in surface.GetRowSpan(y))
            {
                differing += pixel == background ? 0 : 1;
            }
        }

        Assert.True(differing > surface.Width * surface.Height / 50, $"{name} is blank");
    }
}
