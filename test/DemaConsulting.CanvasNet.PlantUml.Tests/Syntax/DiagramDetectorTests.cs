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
        Assert.True(detected.IsSupported);
    }

    /// <summary>
    ///     Validates that the first statement with a keyword of another type names that type, which is not drawn.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="type">The expected type.</param>
    [Theory]
    [InlineData("(*) --> First", "activity-legacy")]
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
    ///     Validates that the class family and the state diagram are detected and drawn: a class, an interface, or a
    ///     class relationship is a class diagram, an object or a map declared first is an object diagram, and a state, a
    ///     composite state with regions, or a transition from the start is a state diagram.
    /// </summary>
    /// <param name="text">The statements.</param>
    /// <param name="type">The expected type.</param>
    [Theory]
    [InlineData("class Car", "class")]
    [InlineData("interface Shape", "class")]
    [InlineData("Foo <|-- Bar", "class")]
    [InlineData("A ..> B", "class")]
    [InlineData("a::b --> c", "class")]
    [InlineData("A : field", "class")]
    [InlineData("package P {\nclass C\n}", "class")]
    [InlineData("object order", "object")]
    [InlineData("map M {\nk => v\n}", "object")]
    [InlineData("class C {\n+ field : int\n--\n}\nC --> D", "class")]
    [InlineData("[*] --> Idle", "state")]
    [InlineData("state Idle", "state")]
    [InlineData("state A {\n[*] --> B\n--\n[*] --> C\n}", "state")]
    [InlineData("A --> B\nA : entered\n[*] --> A", "state")]
    public void PlantUmlDiagramDetector_Detect_ClassFamily_IsDrawn(string text, string type)
    {
        // Arrange / Act
        var detected = Detect($"@startuml\n{text}\n@enduml\n");

        // Assert
        Assert.Equal(type, detected.DiagramType);
        Assert.True(detected.IsSupported);
    }

    /// <summary>
    ///     Validates that a component diagram is detected and drawn: a component declared with its keyword or in
    ///     brackets, an interface written <c>() name</c>, or a component in a container.
    /// </summary>
    /// <param name="text">The statements.</param>
    [Theory]
    [InlineData("[Web] --> [Db]")]
    [InlineData("component Web")]
    [InlineData("() HTTP - Web")]
    [InlineData("node N {\n[C]\n}\n[C] --> D")]
    public void PlantUmlDiagramDetector_Detect_ComponentDiagram_IsDrawn(string text)
    {
        // Arrange / Act
        var detected = Detect($"@startuml\n{text}\n@enduml\n");

        // Assert
        Assert.Equal("component", detected.DiagramType);
        Assert.True(detected.IsSupported);
    }

    /// <summary>
    ///     Validates that a use case diagram is detected and drawn: a use case declared with its keyword or in
    ///     parentheses (with what follows it), an actor written <c>:name:</c> (which opens no multi-line action, as its
    ///     ending <c>;</c> never comes), or a use case in a boundary.
    /// </summary>
    /// <param name="text">The statements.</param>
    [Theory]
    [InlineData("usecase Buy")]
    [InlineData("(Buy) --> (Pay)")]
    [InlineData(":Guest:\n:Account Owner: as AO\nGuest --> (Pay)")]
    [InlineData("actor A\nrectangle R {\n(Pay) #back:white;line:green\n}")]
    public void PlantUmlDiagramDetector_Detect_UseCaseDiagram_IsDrawn(string text)
    {
        // Arrange / Act
        var detected = Detect($"@startuml\n{text}\n@enduml\n");

        // Assert
        Assert.Equal("usecase", detected.DiagramType);
        Assert.True(detected.IsSupported);
    }

    /// <summary>
    ///     Validates that an activity diagram whose action is never closed stays an activity diagram when no graph
    ///     family reads its text, so the activity parser locates the unclosed action.
    /// </summary>
    [Fact]
    public void PlantUmlDiagramDetector_Detect_UnclosedActionOfActivity_IsActivity()
    {
        // Arrange / Act
        var detected = Detect("@startuml\nstart\n:never closed\nstill text\n@enduml\n");

        // Assert
        Assert.Equal("activity", detected.DiagramType);
    }

    /// <summary>
    ///     Validates PlantUML's factory order for texts that several families could read: a plain message is a sequence
    ///     diagram, a class relationship a class diagram, a link to a use case or a component a description diagram, a
    ///     state declaration a state diagram, and a class mixed with an actor is still a class diagram (whose parser
    ///     refuses the actor).
    /// </summary>
    /// <param name="text">The statements.</param>
    /// <param name="type">The expected type.</param>
    [Theory]
    [InlineData("A --> B\nB --> C : x", "sequence")]
    [InlineData("A o-> B", "sequence")]
    [InlineData("Foo <|-- Bar", "class")]
    [InlineData("actor User\nUser --> (Login)", "usecase")]
    [InlineData("A -> B\nB -> (U)", "usecase")]
    [InlineData("actor Guest as g\npackage P {\nusecase UC1\n}\ng --> UC1", "usecase")]
    [InlineData("DataAccess - [First Component]", "component")]
    [InlineData("[A] --> B", "component")]
    [InlineData("node N\ncloud C", "deployment")]
    [InlineData("state A\nA --> B : go", "state")]
    [InlineData("class A\nactor B", "class")]
    [InlineData("(*) --> A", "activity-legacy")]
    public void PlantUmlDiagramDetector_Detect_GraphFamilies_FollowPlantUmlOrder(string text, string type)
    {
        // Arrange / Act
        var detected = Detect($"@startuml\n{text}\n@enduml\n");

        // Assert
        Assert.Equal(type, detected.DiagramType);
    }

    /// <summary>
    ///     Validates the hint of a type that is not drawn: it lists the drawn types.
    /// </summary>
    [Fact]
    public void PlantUmlDiagramDetector_UnsupportedHint_ListsDrawnTypes()
    {
        // Arrange
        var detected = Detect("@startuml\n(*) --> A\n@enduml\n");

        // Act
        var hint = DiagramDetector.UnsupportedHint(detected);

        // Assert
        Assert.Contains("only activity, class, component, object, sequence, state, and usecase diagrams", hint, StringComparison.Ordinal);
        Assert.Contains("activity-legacy", hint, StringComparison.Ordinal);
        Assert.Equal("a, b, and c", DiagramDetector.JoinTypes(["a", "b", "c"]));
        Assert.Equal("a and b", DiagramDetector.JoinTypes(["a", "b"]));
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

    /// <summary>
    ///     Validates the block closers of text blocks: a legend closed with <c>end legend</c> or <c>endlegend</c>, an
    ///     aligned footer alone on its line, and an aligned header with text on one line.
    /// </summary>
    [Fact]
    public void PlantUmlDiagramDetector_BlockClosers_AreRecognized()
    {
        // Act and Assert
        Assert.Equal("endlegend", DiagramDetector.BlockCloser("legend right"));
        Assert.True(DiagramDetector.IsCloser("end legend", "endlegend"));
        Assert.True(DiagramDetector.IsCloser("endlegend", "endlegend"));
        Assert.False(DiagramDetector.IsCloser("end legends", "endlegend"));
        Assert.Equal("endfooter", DiagramDetector.BlockCloser("left footer"));
        Assert.Null(DiagramDetector.BlockCloser("right header text"));
        Assert.Equal(DiagramDetector.StatementFamily.Common, DiagramDetector.Classify("center header text"));
        Assert.Equal(DiagramDetector.StatementFamily.Sequence, DiagramDetector.Classify("endbox"));
    }
}
