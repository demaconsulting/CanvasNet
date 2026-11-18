using DemaConsulting.CanvasNet.Files;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.PlantUml.Tests;

/// <summary>
///     System integration tests for the include directives: local files read from the folder of a diagram under the
///     file-access policy, located errors, and nothing read from the network.
/// </summary>
public class PlantUmlIncludeTests
{
    /// <summary>
    ///     Gets the path of the include fixture.
    /// </summary>
    /// <returns>The full path of <c>Fixtures/include/main.puml</c>.</returns>
    private static string MainPath() => Path.Combine(AppContext.BaseDirectory, "Fixtures", "include", "main.puml");

    /// <summary>
    ///     Validates that a diagram opened from its file includes local files (a fragment and the block of another
    ///     diagram, a variable and a skin parameter shared, a repeated include skipped) and renders without reports, and
    ///     that a text parsed with the same base directory gives the same diagram.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Includes_LocalFilesAreRead()
    {
        // Arrange
        var (options, warnings, unsupported) = PlantUmlTestHelpers.Collecting(strict: true);

        // Act
        var opened = PlantUmlDocument.Open(MainPath());
        var parsed = PlantUmlDocument.Parse(File.ReadAllText(MainPath()), new PlantUmlOpenOptions { BaseDirectory = Path.GetDirectoryName(MainPath()) });
        using var surface = opened.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(("activity", "Included"), (opened.DiagramType, opened.Title));
        Assert.Equal(6, opened.NodeCount);
        Assert.Equal((opened.NodeCount, opened.EdgeCount), (parsed.NodeCount, parsed.EdgeCount));
        Assert.Empty(warnings);
        Assert.Empty(unsupported);
        Assert.True(surface.Height > 200);
    }

    /// <summary>
    ///     Validates that the errors of an include are located at the directive with a hint, name the included file when
    ///     they are inside it, and carry the refusal of the file-access policy as the inner exception.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Includes_FailuresAreLocated()
    {
        // Arrange
        var folder = Path.GetDirectoryName(MainPath())!;
        var only = FileAccessPolicy.UnderRoots(Path.Combine(folder, "parts"));

        // Act
        var missing = Assert.Throws<PlantUmlSyntaxException>(() => PlantUmlDocument.Parse("@startuml\nstart\n!include missing.iuml\nstop\n@enduml\n", new PlantUmlOpenOptions { BaseDirectory = folder, SourceName = "a.puml" }));
        var noFolder = Assert.Throws<PlantUmlSyntaxException>(() => PlantUmlDocument.Parse(File.ReadAllText(MainPath())));
        var denied = Assert.Throws<PlantUmlSyntaxException>(() => PlantUmlDocument.Open(MainPath(), new PlantUmlOpenOptions { FileAccess = only, SourceName = "main.puml" }));

        // Assert
        Assert.All([missing, noFolder, denied], e => Assert.Equal(PlantUmlDiagnosticCodes.IncludeFailed, e.Code));
        Assert.All([missing, noFolder, denied], e => Assert.False(string.IsNullOrEmpty(e.Hint)));
        Assert.Equal(new PlantUmlLocation("a.puml", 3, 1), missing.Location);
        Assert.Equal(new PlantUmlLocation("main.puml", 3, 1), denied.Location);
        Assert.Equal(FileReferenceFailureKind.AccessDenied, Assert.IsType<FileReferenceException>(denied.InnerException).Kind);
    }

    /// <summary>
    ///     Validates that the include forms that would read the network or the standard library are reported, never
    ///     followed, and drawn without what they would include.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Includes_NetworkIsNeverRead()
    {
        // Arrange
        var document = PlantUmlDocument.Parse("@startuml\n!include https://example.invalid/lib.puml\n!includeurl http://example.invalid/x\n!include <C4/C4_Context>\nA -> B\n@enduml\n");
        var (options, _, unsupported) = PlantUmlTestHelpers.Collecting();

        // Act
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([2, 3, 4], unsupported.Select(u => u.Location.Line));
        Assert.All(unsupported, u => Assert.Equal(PlantUmlDiagnosticCodes.IncludeUnsupported, u.Feature));
        Assert.Equal("sequence", document.DiagramType);
    }

    /// <summary>
    ///     Validates the composition hook: a folder resolver with the PlantUML image source draws a linked diagram (whose
    ///     includes resolve in its own folder) as a picture of the requested size, as a document that embeds it asks.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_ImageSource_DrawsALinkedDiagram()
    {
        // Arrange
        var resolver = new FolderImageResolver(Path.Combine(AppContext.BaseDirectory, "Fixtures"), [new PlantUmlImageSource()]);

        // Act
        var result = resolver.Render("include/main.puml");
        using var natural = result.Surface;
        using var fitted = resolver.Resolve("activity/01-basic.puml", 100, 100);
        var expected = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "include", "main.puml")).Measure(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal((expected.Width, expected.Height), (natural.Width, natural.Height));
        Assert.Empty(result.Diagnostics);
        Assert.NotNull(fitted);
        Assert.True(fitted.Width <= 100 && fitted.Height <= 100);
        Assert.Empty(resolver.Diagnostics);
        Assert.Contains(".puml", resolver.Extensions);
    }
}
