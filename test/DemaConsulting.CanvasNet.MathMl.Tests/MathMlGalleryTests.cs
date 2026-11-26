using DemaConsulting.CanvasNet.MathMl.Syntax;

namespace DemaConsulting.CanvasNet.MathMl.Tests;

/// <summary>
///     System-level tests of the documentation galleries: every equation of the user guide gallery
///     <c>docs/user_guide/examples/mathml-*.mml</c> (copied next to the test assembly as <c>Gallery</c>) and of the README
///     gallery <c>docs/images/mathml/*.mml</c> (copied as <c>ReadmeGallery</c>) is opened and rendered with the strict
///     policy, so an equation of the documentation can never use a construct that is not drawn, never raise a warning,
///     and never come out blank.
/// </summary>
public class MathMlGalleryTests
{
    /// <summary>The fewest equations the user guide gallery must hold.</summary>
    private const int MinimumEquations = 12;

    /// <summary>The number of equations of the README gallery (a 2x3 grid).</summary>
    private const int ReadmeEquations = 6;

    /// <summary>The folder of the user guide gallery, below the test assembly.</summary>
    private const string GalleryFolder = "Gallery";

    /// <summary>The folder of the README gallery, below the test assembly.</summary>
    private const string ReadmeGalleryFolder = "ReadmeGallery";

    /// <summary>The major constructs that the user guide gallery shows together.</summary>
    private static readonly string[] MajorElements = ["mfrac", "msqrt", "mroot", "msubsup", "munderover", "mover", "mtable", "mmultiscripts"];

    /// <summary>
    ///     Gets the paths of the equations of a gallery folder, in order.
    /// </summary>
    /// <param name="folder">The gallery folder below the test assembly.</param>
    /// <returns>The paths.</returns>
    private static string[] Equations(string folder)
    {
        var pattern = folder == GalleryFolder ? "mathml-*.mml" : "*.mml";
        return [.. Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, folder), pattern).Order(StringComparer.Ordinal)];
    }

    /// <summary>
    ///     Gets the equations of both galleries, for the theory.
    /// </summary>
    /// <returns>The gallery folder and the file name of each equation.</returns>
    public static TheoryData<string, string> GalleryEquations()
    {
        var data = new TheoryData<string, string>();
        foreach (var folder in new[] { GalleryFolder, ReadmeGalleryFolder })
        {
            foreach (var path in Equations(folder))
            {
                data.Add(folder, Path.GetFileName(path));
            }
        }

        return data;
    }

    /// <summary>
    ///     Collects the names of every element of an equation tree.
    /// </summary>
    /// <param name="element">The root element.</param>
    /// <param name="names">The set the names are added to.</param>
    private static void CollectNames(MathElement element, HashSet<string> names)
    {
        names.Add(element.Name);
        foreach (var child in element.ElementChildren)
        {
            CollectNames(child, names);
        }
    }

    /// <summary>
    ///     Validates that an equation of a gallery renders with the strict policy: no feature is reported, no warning is
    ///     raised, the picture has its measured size, and it is not blank.
    /// </summary>
    /// <param name="folder">The gallery folder.</param>
    /// <param name="name">The file name of the equation.</param>
    [Theory]
    [MemberData(nameof(GalleryEquations))]
    public void CanvasNetMathMl_Gallery_RendersStrictWithoutWarnings(string folder, string name)
    {
        // Arrange
        var equation = MathMlDocument.Open(Path.Combine(AppContext.BaseDirectory, folder, name));
        var (options, warnings, unsupported) = MathMlTestHelpers.Collecting(strict: true);

        // Act
        var size = equation.Measure(options, TestContext.Current.CancellationToken);
        using var surface = equation.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(warnings);
        Assert.Empty(unsupported);
        Assert.Equal((size.Width, size.Height), (surface.Width, surface.Height));
        Assert.True(MathMlTestHelpers.InkedPixels(surface) > surface.Width * surface.Height / 50, $"{folder}/{name} is blank");
    }

    /// <summary>
    ///     Validates that the user guide gallery holds at least 12 equations and the README gallery 6, that every one is a
    ///     block equation with a text alternative, and that the user guide gallery together shows the major constructs:
    ///     fractions, both radicals, scripts, limits, accents, tables, and prescripts.
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_Gallery_ShowsTheMajorConstructs()
    {
        // Arrange
        var guide = Equations(GalleryFolder);
        var readme = Equations(ReadmeGalleryFolder);

        // Act
        var documents = guide.Concat(readme).Select(path => MathMlDocument.Open(path)).ToList();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in guide)
        {
            CollectNames(MathMlTestHelpers.Tree(File.ReadAllText(path)), names);
        }

        // Assert
        Assert.True(guide.Length >= MinimumEquations, $"the user guide gallery holds {guide.Length} equations");
        Assert.Equal(ReadmeEquations, readme.Length);
        Assert.All(documents, d => Assert.Equal(MathMlDisplay.Block, d.Display));
        Assert.All(documents, d => Assert.False(string.IsNullOrWhiteSpace(d.AltText)));
        Assert.All(MajorElements, element => Assert.Contains(element, names));
    }
}
