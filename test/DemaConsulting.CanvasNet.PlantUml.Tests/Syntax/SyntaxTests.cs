using DemaConsulting.CanvasNet.PlantUml.Syntax;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Syntax;

/// <summary>
///     Subsystem tests for the <c>Syntax</c> subsystem: the preprocessor and the detector together, as the document
///     uses them.
/// </summary>
public class SyntaxTests
{
    /// <summary>
    ///     Validates that a full text is framed, preprocessed and detected, and that the detected statement is located in
    ///     the original text.
    /// </summary>
    [Fact]
    public void PlantUmlSyntax_PreprocessAndDetect_FullText_KeepsOriginalLocations()
    {
        // Arrange
        const string text = "notes before\n@startuml\n' comment\n!define WHO Alice\n\n  participant WHO\nWHO -> Bob : hi\n@enduml\n";

        // Act
        var source = Preprocessor.Run(text, "full.puml", PlantUmlLimits.Default);
        var detected = DiagramDetector.Detect(source);

        // Assert
        Assert.Equal("sequence", detected.DiagramType);
        Assert.Equal(new PlantUmlLocation("full.puml", 6, 3), detected.Location);
        Assert.Contains(source.Block.Statements, s => s.Text == "Alice -> Bob : hi");
    }

    /// <summary>
    ///     Validates that the statements a macro produces are what the detector reads.
    /// </summary>
    [Fact]
    public void PlantUmlSyntax_Detect_ReadsSubstitutedStatements()
    {
        // Arrange
        const string text = "@startuml\n!define DECLARE class\nDECLARE Car\n@enduml\n";

        // Act
        var detected = DiagramDetector.Detect(Preprocessor.Run(text, null, PlantUmlLimits.Default));

        // Assert
        Assert.Equal("class", detected.DiagramType);
        Assert.Equal(3, detected.Location.Line);
    }

    /// <summary>
    ///     Validates that the statements of an included file are read before the type is detected, located in that file.
    /// </summary>
    [Fact]
    public void PlantUmlSyntax_Includes_AreReadBeforeDetection()
    {
        // Arrange
        var folder = Path.Combine(AppContext.BaseDirectory, "Fixtures", "include");
        const string text = "@startuml\n!include parts/steps.puml\n@enduml\n";
        var includes = new IncludeResolver(folder, null, Files.FileAccessPolicy.AllowAll, PlantUmlLimits.Default);

        // Act
        var source = Preprocessor.Run(text, "short.puml", PlantUmlLimits.Default, includes);
        var detected = DiagramDetector.Detect(source);

        // Assert
        Assert.Equal("activity", detected.DiagramType);
        Assert.Equal(new PlantUmlLocation("parts/steps.puml", 4, 1), detected.Location);
    }
}
