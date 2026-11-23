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
    ///     Validates that a deployment diagram (a description diagram without a use case or component construct) is
    ///     detected and drawn: nodes, artifacts, and the other deployment symbols, also as containers, a queue
    ///     before a symbol that no sequence diagram has, and an archimate element (drawn as a reported rectangle).
    /// </summary>
    /// <param name="text">The statements.</param>
    [Theory]
    [InlineData("node Server")]
    [InlineData("artifact App\nnode Host\nApp --> Host")]
    [InlineData("queue Jobs\nstorage Disk {\nfile Data\n}\nJobs --> Data")]
    [InlineData("person P\nhexagon H\nP --> H")]
    [InlineData("archimate #Business \"Biz\" as biz")]
    [InlineData("node Host\narchimate #Technology \"Tech\" as tech\ntech --> Host")]
    public void PlantUmlDiagramDetector_Detect_DeploymentDiagram_IsDrawn(string text)
    {
        // Arrange / Act
        var detected = Detect($"@startuml\n{text}\n@enduml\n");

        // Assert
        Assert.Equal("deployment", detected.DiagramType);
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
    ///     Validates that a skin parameter block (<c>skinparam element {</c>, its lines, nested blocks, and its closing
    ///     brace) is one statement that every type accepts, so it keeps the type the other statements give: a sequence
    ///     diagram with or without a participant declaration, and every graph family.
    /// </summary>
    /// <param name="text">The statements.</param>
    /// <param name="type">The expected type.</param>
    [Theory]
    [InlineData("skinparam participant {\n  BackgroundColor #DBEAFE\n}\nparticipant A\nA -> B : x", "sequence")]
    [InlineData("skinparam participant {\n  BackgroundColor #DBEAFE\n}\nA -> B : x", "sequence")]
    [InlineData("skinparam sequence {\n  ArrowColor red\n  participant {\n    FontSize 18\n  }\n}\nA -> B : x", "sequence")]
    [InlineData("skinparam activity {\n  BackgroundColor #DBEAFE\n}\nstart\n:a;\nstop", "activity")]
    [InlineData("skinparam class {\n  BackgroundColor #DBEAFE\n}\nclass A\nA --> B", "class")]
    [InlineData("skinparam object {\n  BackgroundColor #DBEAFE\n}\nobject O", "object")]
    [InlineData("skinparam state {\n  BackgroundColor #DBEAFE\n}\n[*] --> S", "state")]
    [InlineData("skinparam usecase {\n  BackgroundColor #DBEAFE\n}\nactor A\nA --> (U)", "usecase")]
    [InlineData("skinparam component {\n  BackgroundColor #DBEAFE\n}\n[C] --> [D]", "component")]
    [InlineData("skinparam node {\n  BackgroundColor #DBEAFE\n}\nnode N", "deployment")]
    public void PlantUmlDiagramDetector_Detect_SkinParamBlock_KeepsTheType(string text, string type)
    {
        // Arrange / Act
        var detected = Detect($"@startuml\n{text}\n@enduml\n");

        // Assert
        Assert.Equal(type, detected.DiagramType);
        Assert.True(detected.IsSupported);
        Assert.NotEqual("skinparam", detected.Keyword);
    }

    /// <summary>
    ///     Validates that the statements after actions that are never closed are found in linear steps: 2,000 unclosed
    ///     actions are 2,000 statements, and each statement is visited at most twice (instead of once for every action
    ///     before it).
    /// </summary>
    [Fact]
    public void PlantUmlDiagramDetector_OuterStatements_ManyUnclosedActions_VisitEachAtMostTwice()
    {
        // Arrange
        var source = Preprocessor.Run("@startuml\n" + string.Join('\n', Enumerable.Repeat(":a", 2000)) + "\n@enduml\n", null, PlantUmlLimits.Default);
        var statements = source.Block.Statements.Where(s => s.Text.Length > 0).ToList();

        // Act
        var outer = DiagramDetector.OuterStatements(statements, openActionIsText: false, out var visits);

        // Assert
        Assert.Equal(2000, outer.Count);
        Assert.True(visits <= 2 * statements.Count, $"visits {visits}");
    }

    /// <summary>
    ///     Validates that a diagram with messages to or from the diagram border (found and lost messages, with a circle,
    ///     a cross, a reversed arrow, or a color at the border end) is a sequence diagram, as in PlantUML.
    /// </summary>
    /// <param name="text">The statements.</param>
    [Theory]
    [InlineData("participant A\n[-> A : in\nA ->] : out")]
    [InlineData("A -> B\n[o-> A")]
    [InlineData("A ->] : out")]
    [InlineData("[x-> A : x\nA ->x] : y\nA ->o] : z")]
    [InlineData("?-> A : q\nA ->? : r\nA ->o? : s\n?o-> A")]
    [InlineData("A -> B\nA o->o] : k\n[<- A\nA <-] : u\n[<-> A")]
    [InlineData("A -> B\nA -[#red]>] : c")]
    public void PlantUmlDiagramDetector_Detect_BorderMessages_IsSequence(string text)
    {
        // Arrange / Act
        var detected = Detect($"@startuml\n{text}\n@enduml\n");

        // Assert
        Assert.Equal("sequence", detected.DiagramType);
        Assert.True(detected.IsSupported);
    }

    /// <summary>
    ///     Validates which statements are messages to or from the diagram border: the border end is read from the part
    ///     before the label only, so a label with brackets and arrows, a cross head, a color, a component, and a state
    ///     pseudo-state are not border ends.
    /// </summary>
    /// <param name="text">The statement.</param>
    /// <param name="expected">Whether it is a border message.</param>
    [Theory]
    [InlineData("[-> A : in", true)]
    [InlineData("[o-> A", true)]
    [InlineData("]-> A", true)]
    [InlineData("?o-> A", true)]
    [InlineData("A ->] : out", true)]
    [InlineData("A ->x] : y", true)]
    [InlineData("A ->o? : r", true)]
    [InlineData("A ->? : r", true)]
    [InlineData("A <-] : u", true)]
    [InlineData("A -[#red]>] : c", true)]
    [InlineData("A ->]++", true)]
    [InlineData("A -> B", false)]
    [InlineData("A -> B : see [-x] and x->]", false)]
    [InlineData("A ->x B", false)]
    [InlineData("A -[#red]-> B", false)]
    [InlineData("[A] --> [B]", false)]
    [InlineData("A --> [*]", false)]
    [InlineData("A -[#red]", false)]
    public void PlantUmlDiagramDetector_IsBorderMessage_ReadsTheBorderEnds(string text, bool expected)
    {
        // Act
        var isBorder = DiagramDetector.IsBorderMessage(text);

        // Assert
        Assert.Equal(expected, isBorder);
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
        Assert.Contains("only activity, class, component, deployment, object, sequence, state, and usecase diagrams", hint, StringComparison.Ordinal);
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
    ///     Validates that an unreadable message from a participant named like a declaration keyword of another type
    ///     (deployment, component, class, object, state, use case) is a sequence syntax error at the message, not a
    ///     diagram of that type whose parser refuses the participant declaration.
    /// </summary>
    /// <param name="name">The participant name.</param>
    [Theory]
    [InlineData("Agent")]
    [InlineData("Node")]
    [InlineData("File")]
    [InlineData("Card")]
    [InlineData("Person")]
    [InlineData("Stack")]
    [InlineData("Cloud")]
    [InlineData("Component")]
    [InlineData("Interface")]
    [InlineData("Class")]
    [InlineData("Object")]
    [InlineData("Package")]
    [InlineData("State")]
    [InlineData("Usecase")]
    public void PlantUmlDiagramDetector_Detect_BadMessageFromKeywordNamedParticipant_IsSequenceErrorAtTheMessage(string name)
    {
        // Arrange
        var text = $"@startuml\nactor User\nparticipant {name}\nUser -> {name} : request\n{name} -> : call tool\n{name} --> User : answer\n@enduml\n";

        // Act
        var ex = Assert.Throws<PlantUmlSyntaxException>(() => Detect(text));

        // Assert
        Assert.Equal(PlantUmlDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Equal((5, 1), (ex.Location.Line, ex.Location.Column));
        Assert.Contains($"'{name} -> : call tool' is not a statement of a sequence diagram", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that readable messages from participants named like declaration keywords are a sequence diagram.
    /// </summary>
    /// <param name="text">The statements.</param>
    [Theory]
    [InlineData("actor User\nparticipant Agent\nUser -> Agent : request\nAgent -> User : call tool\nAgent --> User : answer")]
    [InlineData("Agent -> Bob : hi")]
    [InlineData("Node -> Card : x\nCard --> Node")]
    public void PlantUmlDiagramDetector_Detect_MessagesFromKeywordNamedParticipants_AreSequence(string text)
    {
        // Act
        var detected = Detect($"@startuml\n{text}\n@enduml\n");

        // Assert
        Assert.Equal("sequence", detected.DiagramType);
    }

    /// <summary>
    ///     Validates that declarations by keyword followed by links keep naming their type, so the rule that a word an
    ///     arrow starts from is a name leaves real declarations alone.
    /// </summary>
    /// <param name="text">The statements.</param>
    /// <param name="type">The expected type.</param>
    [Theory]
    [InlineData("agent A\nnode N\nA --> N", "deployment")]
    [InlineData("node A\nnode B\nA -> B", "deployment")]
    [InlineData("agent X\nX --> Y", "deployment")]
    [InlineData("component C\nC --> D", "component")]
    [InlineData("class A\nA -> B", "class")]
    [InlineData("state S\nS -> T", "state")]
    [InlineData("object o\no --> p", "object")]
    public void PlantUmlDiagramDetector_Detect_KeywordDeclarationsWithLinks_KeepTheirType(string text, string type)
    {
        // Act
        var detected = Detect($"@startuml\n{text}\n@enduml\n");

        // Assert: the drawn type of the declarations, recognized by the declaration keyword on line 2
        Assert.Equal((type, true, 2), (detected.DiagramType, detected.IsSupported, detected.Location.Line));
    }

    /// <summary>
    ///     Validates which statements start with an arrow from their first word: a message from a keyword-named
    ///     participant does, a declaration followed by more words, a statement without an arrow, and an arrow without
    ///     a left end do not.
    /// </summary>
    /// <param name="text">The statement.</param>
    /// <param name="expected">Whether the first word is the left end of an arrow.</param>
    [Theory]
    [InlineData("Agent -> : call tool", true)]
    [InlineData("node --> db", true)]
    [InlineData("State ..> Next : x", true)]
    [InlineData("agent A --> B", false)]
    [InlineData("node Server", false)]
    [InlineData("-> A : x", false)]
    [InlineData("\"Agent\" -> B", false)]
    public void PlantUmlDiagramDetector_StartsWithArrowFromFirstWord_ReadsTheLeftEnd(string text, bool expected)
    {
        // Act
        var starts = DiagramDetector.StartsWithArrowFromFirstWord(text);

        // Assert
        Assert.Equal(expected, starts);
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
