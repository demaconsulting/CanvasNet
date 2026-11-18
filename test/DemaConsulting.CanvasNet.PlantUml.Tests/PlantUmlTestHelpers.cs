namespace DemaConsulting.CanvasNet.PlantUml.Tests;

/// <summary>
///     Shared helpers of the PlantUML tests: report collection, pixel checks and temporary files.
/// </summary>
internal static class PlantUmlTestHelpers
{
    /// <summary>A small class diagram, a known type that this version does not draw.</summary>
    public const string UnsupportedText = "@startuml\nclass Car\nclass Wheel\nCar *-- Wheel\n@enduml\n";

    /// <summary>
    ///     Creates render options that collect every report.
    /// </summary>
    /// <param name="strict">Whether the first unsupported feature throws.</param>
    /// <returns>The options and the lists the reports go to.</returns>
    public static (PlantUmlRenderOptions Options, List<PlantUmlDiagnostic> Warnings, List<PlantUmlUnsupportedFeature> Unsupported) Collecting(bool strict = false)
    {
        var warnings = new List<PlantUmlDiagnostic>();
        var unsupported = new List<PlantUmlUnsupportedFeature>();
        var options = new PlantUmlRenderOptions
        {
            WarningHandler = warnings.Add,
            UnsupportedFeatureHandler = unsupported.Add,
            ThrowOnUnsupportedFeature = strict,
        };
        return (options, warnings, unsupported);
    }

    /// <summary>
    ///     Writes a temporary file and returns its path.
    /// </summary>
    /// <param name="content">The bytes.</param>
    /// <param name="extension">The extension, with the dot.</param>
    /// <returns>The path; the caller deletes it.</returns>
    public static string WriteTempFile(byte[] content, string extension = ".puml")
    {
        var path = Path.Combine(Path.GetTempPath(), $"canvasnet-plantuml-{Guid.NewGuid():N}{extension}");
        File.WriteAllBytes(path, content);
        return path;
    }
}
