using DemaConsulting.CanvasNet.PlantUml.Syntax;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Syntax;

/// <summary>
///     Unit tests for the <c>DiagramDetector</c> unit: the type from the tag, from the statements in PlantUML's factory
///     order, the recognized types that are not drawn, and the errors.
/// </summary>
public class DiagramDetectorTests
{
    /// <summary>
    ///     Detects the type of a text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The detected type.</returns>
    private static DetectedDiagram Detect(string text) =>
        DiagramDetector.Detect(Preprocessor.Run(text, null, PlantUmlLimits.Default));

    /// <summary>
    ///     Validates that a diagram whose statements are all sequence statements is a sequence diagram.
    /// </summary>
    [Fact]
    public void PlantUmlDiagramDetector_Detect_SequenceStatements_IsSequence()
    {
        // Arrange
        const string text = "@startuml\ntitle T\nactor User\nUser -> Web : go\nalt ok\nWeb --> User\nelse\nend\nnote over User\nclass is text here\nend note\n== x ==\n...\n|||\n@enduml\n";

        // Act
        var detected = Detect(text);

        // Assert
        Assert.Equal("sequence", detected.DiagramType);
        Assert.Equal(("actor", 3), (detected.Keyword, detected.Location.Line));
    }

    /// <summary>
    ///     Validates that a diagram of the new activity syntax is an activity diagram.
    /// </summary>
    [Fact]
    public void PlantUmlDiagramDetector_Detect_ActivityStatements_IsActivity()
    {
        // Arrange
        const string text = "@startuml\nstart\n:read;\nif (ok?) then (yes)\n:go;\nelse (no)\nendif\n(A)\nstop\n@enduml\n";

        // Act
        var detected = Detect(text);

        // Assert
        Assert.Equal("activity", detected.DiagramType);
        Assert.False(detected.IsSupported);
    }

    /// <summary>
    ///     Validates that the first statement with a keyword of another type names that type, which is not drawn.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="type">The expected type.</param>
    [Theory]
    [InlineData("class Car", "class")]
    [InlineData("interface Shape", "class")]
    [InlineData("object order", "object")]
    [InlineData("usecase Buy", "usecase")]
    [InlineData("(Buy) --> (Pay)", "usecase")]
    [InlineData("[*] --> Idle", "state")]
    [InlineData("state Idle", "state")]
    [InlineData("(*) --> First", "activity-legacy")]
    [InlineData("[Web] --> [Db]", "component")]
    [InlineData("component Web", "component")]
    [InlineData("node Server", "deployment")]
    [InlineData("robust \"Web\" as W", "timing")]
    [InlineData("[Design] lasts 5 days", "gantt")]
    public void PlantUmlDiagramDetector_Detect_OtherTypeKeyword_IsRecognized(string statement, string type)
    {
        // Arrange / Act
        var detected = Detect($"@startuml\n{statement}\n@enduml\n");

        // Assert
        Assert.Equal(type, detected.DiagramType);
        Assert.False(detected.IsSupported);
    }

    /// <summary>
    ///     Validates that a tag other than <c>uml</c> names its type.
    /// </summary>
    [Fact]
    public void PlantUmlDiagramDetector_Detect_OtherTag_NamesType()
    {
        // Arrange / Act
        var detected = Detect("@startmindmap\n* root\n@endmindmap\n");

        // Assert
        Assert.Equal(("mindmap", false, "@startmindmap"), (detected.DiagramType, detected.IsSupported, detected.Keyword));
    }

    /// <summary>
    ///     Validates that an unknown tag is a located error whose hint names the nearest known tag.
    /// </summary>
    [Fact]
    public void PlantUmlDiagramDetector_Detect_UnknownTag_ThrowsWithNearestTag()
    {
        // Arrange / Act
        var ex = Assert.Throws<PlantUmlSyntaxException>(() => Detect("\n@startMindMaps\n* x\n@endMindMaps\n"));

        // Assert
        Assert.Equal(PlantUmlDiagnosticCodes.UnknownDiagram, ex.Code);
        Assert.Equal(2, ex.Location.Line);
        Assert.Contains("@startmindmap", ex.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a diagram with nothing to draw is a syntax error.
    /// </summary>
    [Fact]
    public void PlantUmlDiagramDetector_Detect_EmptyDiagram_Throws()
    {
        // Arrange / Act
        var empty = Assert.Throws<PlantUmlSyntaxException>(() => Detect("@startuml\n@enduml\n"));
        var titleOnly = Assert.Throws<PlantUmlSyntaxException>(() => Detect("@startuml\ntitle Only\n@enduml\n"));

        // Assert
        Assert.Equal(PlantUmlDiagnosticCodes.SyntaxError, empty.Code);
        Assert.Equal(2, titleOnly.Location.Line);
    }

    /// <summary>
    ///     Validates that a statement no type accepts is a syntax error at that statement.
    /// </summary>
    [Fact]
    public void PlantUmlDiagramDetector_Detect_UnknownStatement_ThrowsAtStatement()
    {
        // Arrange / Act
        var ex = Assert.Throws<PlantUmlSyntaxException>(() => Detect("@startuml\nAlice -> Bob\n   what is this\n@enduml\n"));

        // Assert
        Assert.Equal(PlantUmlDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Equal((3, 4), (ex.Location.Line, ex.Location.Column));
        Assert.Contains("sequence", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the edit distance of the nearest-tag hint.
    /// </summary>
    [Fact]
    public void PlantUmlDiagramDetector_EditDistance_CountsEdits()
    {
        // Arrange / Act / Assert
        Assert.Equal(0, DiagramDetector.EditDistance("uml", "uml"));
        Assert.Equal(1, DiagramDetector.EditDistance("chat", "chart"));
        Assert.Equal(3, DiagramDetector.EditDistance("abc", string.Empty));
    }
}
