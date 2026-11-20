using System.Text;
using DemaConsulting.CanvasNet.Files;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.PlantUml.Tests;

/// <summary>
///     Unit tests of <see cref="PlantUmlImageSource"/> (part of the <c>PlantUmlDocument</c> unit): a PlantUML file as an
///     embedded picture, its size, its notes, and its includes.
/// </summary>
public class PlantUmlImageSourceTests
{
    /// <summary>A small activity diagram.</summary>
    private const string Activity = "@startuml\nstart\n:Draw me;\nstop\n@enduml\n";

    /// <summary>
    ///     Creates a request for a diagram.
    /// </summary>
    /// <param name="text">The diagram text.</param>
    /// <param name="width">The requested width, or null.</param>
    /// <param name="height">The requested height, or null.</param>
    /// <param name="strict">Whether an unsupported feature throws.</param>
    /// <param name="directory">The folder of the file, or null.</param>
    /// <param name="fileAccess">The file-access policy, or null for the default.</param>
    /// <returns>The request.</returns>
    private static ImageSourceRequest Request(string text, int? width = null, int? height = null, bool strict = false, string? directory = null, FileAccessPolicy? fileAccess = null) => new()
    {
        Href = "diagram.puml",
        FileName = "diagram.puml",
        Directory = directory,
        Content = Encoding.UTF8.GetBytes(text),
        PixelWidth = width,
        PixelHeight = height,
        ThrowOnUnsupportedFeature = strict,
        FileAccess = fileAccess ?? FileAccessPolicy.AllowAll,
        CancellationToken = TestContext.Current.CancellationToken,
    };

    /// <summary>
    ///     Validates what the source claims: its name, the three extensions, no scheme, and no fragment parameters.
    /// </summary>
    [Fact]
    public void PlantUmlImageSource_Properties_ClaimPlantUmlExtensions()
    {
        // Arrange
        var source = new PlantUmlImageSource();

        // Act / Assert
        Assert.Equal("plantuml", source.Name);
        Assert.Equal([".puml", ".plantuml", ".pu"], source.Extensions);
        Assert.Empty(source.Schemes);
        Assert.IsNotAssignableFrom<IFragmentImageSource>(source);
        Assert.Equal("plantuml-warning", PlantUmlImageSource.WarningCode);
    }

    /// <summary>
    ///     Validates that a diagram is drawn at its natural size and at a requested size over a transparent background.
    /// </summary>
    [Fact]
    public void PlantUmlImageSource_Render_DrawsTheDiagramOnTransparent()
    {
        // Arrange
        var source = new PlantUmlImageSource();
        var natural = PlantUmlDocument.Parse(Activity).Measure(cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var result = source.Render(Request(Activity));
        using var surface = result.Surface;
        var sized = source.Render(Request(Activity, 300, 200));
        using var sizedSurface = sized.Surface;

        // Assert
        Assert.Equal((natural.Width, natural.Height), (surface.Width, surface.Height));
        Assert.Equal(0, surface.GetRowSpan(1)[1].A);
        Assert.False(PlantUmlTestHelpers.IsFullyTransparent(surface));
        Assert.Empty(result.Diagnostics);
        Assert.True(sizedSurface.Width <= 300 && sizedSurface.Height <= 200);
    }

    /// <summary>
    ///     Validates the notes: a warning keeps its specific code in the detail and its location, an unsupported
    ///     statement is a note with its code, a type that is not drawn is a transparent picture with a note, and strict
    ///     mode throws.
    /// </summary>
    [Fact]
    public void PlantUmlImageSource_Render_ReportsNotesAndStrictMode()
    {
        // Arrange
        var source = new PlantUmlImageSource();
        const string noisy = "@startuml\n!pragma layout smetana\nstart\ngoto x\nstop\n@enduml\n";

        // Act
        var result = source.Render(Request(noisy));
        using var surface = result.Surface;
        var unsupported = source.Render(Request("@startuml\n(*) --> First\n@enduml\n", 32, 24));
        using var empty = unsupported.Surface;
        var ex = Assert.Throws<PlantUmlUnsupportedFeatureException>(() => source.Render(Request(noisy, strict: true)));

        // Assert
        Assert.Equal(2, result.Diagnostics.Count);
        Assert.Equal(("plantuml", ImageDiagnosticSeverity.Warning, PlantUmlImageSource.WarningCode, PlantUmlDiagnosticCodes.PragmaIgnored), (result.Diagnostics[0].Source, result.Diagnostics[0].Severity, result.Diagnostics[0].Code, result.Diagnostics[0].Detail));
        Assert.Equal(new ImageDiagnosticLocation("diagram.puml", 2, 1, null), result.Diagnostics[0].Location);
        Assert.Equal((ImageDiagnosticSeverity.Unsupported, PlantUmlDiagnosticCodes.StatementUnsupported), (result.Diagnostics[1].Severity, result.Diagnostics[1].Code));
        Assert.True(PlantUmlTestHelpers.IsFullyTransparent(empty));
        Assert.Equal(PlantUmlDiagnosticCodes.DiagramUnsupported, Assert.Single(unsupported.Diagnostics).Code);
        Assert.Equal(PlantUmlDiagnosticCodes.StatementUnsupported, ex.Feature);
    }

    /// <summary>
    ///     Validates that the includes of an embedded diagram resolve against the folder of the request under its
    ///     file-access policy, and fail without a folder.
    /// </summary>
    [Fact]
    public void PlantUmlImageSource_Render_IncludesFollowTheRequest()
    {
        // Arrange
        var source = new PlantUmlImageSource();
        var folder = Path.Combine(AppContext.BaseDirectory, "Fixtures", "include");
        var text = File.ReadAllText(Path.Combine(folder, "main.puml"));
        var elsewhere = FileAccessPolicy.UnderRoots(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sequence"));

        // Act
        var result = source.Render(Request(text, directory: folder));
        using var surface = result.Surface;
        var noFolder = Assert.Throws<PlantUmlSyntaxException>(() => source.Render(Request(text)));
        var denied = Assert.Throws<PlantUmlSyntaxException>(() => source.Render(Request(text, directory: folder, fileAccess: elsewhere)));

        // Assert
        Assert.Empty(result.Diagnostics);
        Assert.True(surface.Height > surface.Width);
        Assert.All([noFolder, denied], e => Assert.Equal(PlantUmlDiagnosticCodes.IncludeFailed, e.Code));
        Assert.IsType<FileReferenceException>(denied.InnerException);
    }
}
