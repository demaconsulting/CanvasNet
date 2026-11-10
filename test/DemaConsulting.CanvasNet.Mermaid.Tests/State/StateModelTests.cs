using DemaConsulting.CanvasNet.Mermaid.Scenegraph;
using DemaConsulting.CanvasNet.Mermaid.State;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.State;

/// <summary>
///     Unit tests of <see cref="StateModel"/>: the translation of the statement tree, the database, the data fetcher, the
///     mapping to the layout data, the database errors and the limits.
/// </summary>
public class StateModelTests
{
    /// <summary>
    ///     Validates that <c>[*]</c> becomes the start or end state of the document that holds it, and that a state used
    ///     before its body becomes a composite state.
    /// </summary>
    [Fact]
    public void StateModel_SetRootDocument_StartAndEndStates_TakeTheirParentsId()
    {
        // Act
        var model = StateFacts.Parse("stateDiagram-v2\n[*] --> A\nstate A {\n  [*] --> B\n  B --> [*]\n}\nA --> [*]\n");

        // Assert
        Assert.Equal(["root_start", "A", "A_start", "B", "A_end", "root_end"], model.Nodes.Select(n => n.Id));
        Assert.Equal(["stateStart", "roundedWithTitle", "stateStart", "rect", "stateEnd", "stateEnd"], model.Nodes.Select(n => n.Shape));
        Assert.Equal([null, null, "A", "A", "A", null], model.Nodes.Select(n => n.ParentId));
        Assert.Equal(["root_start", "A", "root_end"], model.States.Select(s => s.Id));
    }

    /// <summary>
    ///     Validates that the legacy diagram applies its statements before the translation, as Mermaid's version 1
    ///     database does, so its start and end states are numbered in the database, while the layout is the same.
    /// </summary>
    [Fact]
    public void StateModel_SetRootDocument_LegacyDiagram_AppliesStatementsBeforeTranslating()
    {
        // Arrange
        var settings = new DetectorSettings("dagre-wrapper", "dagre-wrapper", "dagre-d3");
        const string Text = "stateDiagram\n[*] --> A\nA --> [*]\n";

        // Act
        var model = StateFacts.Parse(Text, settings: settings);
        var data = model.GetData(StateFacts.DefaultConfig(Text));

        // Assert
        Assert.Equal("state", model.MermaidId);
        Assert.Equal(["start1:start", "A:default", "start2:start"], model.States.Select(s => $"{s.Id}:{s.Type}"));
        Assert.Equal(["root_start", "A", "root_end"], data.Nodes.Select(n => n.Id));
        Assert.Equal("stateDiagram", data.DiagramType);
    }

    /// <summary>
    ///     Validates that a document whose last statement is a divider keeps its statements, so its dividers are plain
    ///     states without a label, as in Mermaid.
    /// </summary>
    [Fact]
    public void StateModel_SetRootDocument_TrailingDivider_KeepsTheStatements()
    {
        // Act
        var model = StateFacts.Parse("stateDiagram-v2\nstate Busy {\n  a\n  --\n  b\n  --\n}\n");

        // Assert
        Assert.Equal(["a", "divider-id-1", "b", "divider-id-2"], model.RootDocument[0].Doc!.Select(s => s.Id));
        var divider = model.Nodes.Single(n => n.Id == "divider-id-1");
        Assert.Equal(("divider", false, string.Empty, "Busy"), (divider.Shape, divider.IsGroup, divider.Label, divider.ParentId));
    }

    /// <summary>
    ///     Validates the fields of the layout data: state nodes with Mermaid's radius, padding and centered label, the
    ///     cluster classes, the transitions with the barb end, the spacing of the state section and the barb marker.
    /// </summary>
    [Fact]
    public void StateModel_GetData_StatesAndTransitions_HaveMermaidsFields()
    {
        // Arrange
        const string Text = "---\nconfig:\n  look: classic\n  state:\n    nodeSpacing: 70\n---\nstateDiagram-v2\ndirection LR\nstate Comp {\n  A --> B : go\n}\n";

        // Act
        var model = StateFacts.Parse(Text);
        var data = model.GetData(StateFacts.DefaultConfig(Text));

        // Assert
        Assert.Equal(("stateDiagram", "LR", 70.0, 50.0), (data.DiagramType, data.Direction, data.NodeSpacing, data.RankSpacing));
        Assert.Equal(["barb"], data.Markers);
        var comp = data.Nodes[0];
        Assert.Equal(("roundedWithTitle", true, "TB", " statediagram-state statediagram-cluster "), (comp.Shape, comp.IsGroup, comp.Dir, comp.CssClasses));
        var a = data.Nodes[1];
        Assert.Equal((10.0, 10.0, 8.0, true), (a.Rx!.Value, a.Ry!.Value, a.Padding, a.CenterLabel));
        Assert.Equal(("A", "markdown", "state-A-0", "Comp", "classic"), (a.Label, a.LabelType, a.DomId, a.ParentId, a.Look));
        var edge = Assert.Single(data.Edges);
        Assert.Equal(("edge0", "go", "arrow_barb", "transition", 1), (edge.Id, edge.Label, edge.ArrowTypeEnd, edge.Classes, edge.MinLen));
        Assert.Equal(["fill:none"], edge.Style);
    }

    /// <summary>
    ///     Validates that the first description of a state is its label and the others its descriptions, and that a
    ///     description makes a choice state a plain state, as in Mermaid.
    /// </summary>
    [Fact]
    public void StateModel_GetData_Descriptions_FirstIsTheLabelAndTheRestAreDescriptions()
    {
        // Arrange
        const string Text = "stateDiagram-v2\nS : one\nS : two\nS : three\nR : only\nstate c1 <<choice>>\nc1 : a test\n";

        // Act
        var data = StateFacts.Parse(Text).GetData(StateFacts.DefaultConfig(Text));

        // Assert
        Assert.Equal(["S:rectWithTitle:one", "R:rect:only", "c1:rect:a test"], data.Nodes.Select(n => $"{n.Id}:{n.Shape}:{n.Label}"));
        Assert.Equal(["two", "three"], data.Nodes[0].Descriptions!);
        Assert.Empty(data.Nodes[1].Descriptions!);
    }

    /// <summary>
    ///     Validates notes: each in a note group at the top level, linked from the note for <c>left of</c> and to it
    ///     otherwise, with the DOM ids of the counter the transitions share.
    /// </summary>
    [Fact]
    public void StateModel_GetData_Notes_AreGroupedAndLinkedWithTheSharedCounter()
    {
        // Arrange
        const string Text = "stateDiagram-v2\nA --> B\nnote left of A : first\nnote right of B : second\n";

        // Act
        var model = StateFacts.Parse(Text);
        var data = model.GetData(StateFacts.DefaultConfig(Text));

        // Assert
        Assert.Equal(["A", "B", "A----parent", "A----note-1", "B----parent", "B----note-2"], data.Nodes.Select(n => n.Id));
        Assert.Equal(["state-A-1", "state-B-2", "state-A----parent-1", "state-A----note-1"], data.Nodes.Take(4).Select(n => n.DomId));
        Assert.Equal((16.0, true, (string?)null), (data.Nodes[2].Padding, data.Nodes[2].IsGroup, data.Nodes[2].ParentId));
        Assert.Equal((15.0, "note", "A----parent", "left of"), (data.Nodes[3].Padding, data.Nodes[3].Shape, data.Nodes[3].ParentId, model.Nodes[3].Position));
        Assert.Equal(["edge0", "A----note-1-A", "B-B----note-2"], data.Edges.Select(e => e.Id));
        Assert.Equal(("transition note-edge", string.Empty), (data.Edges[1].Classes, data.Edges[1].ArrowTypeEnd));
    }

    /// <summary>
    ///     Validates that a note on a state seen nowhere before has no shape, as Mermaid leaves it undefined.
    /// </summary>
    [Fact]
    public void StateModel_GetData_NoteOnAnUnseenState_HasNoShape()
    {
        // Arrange
        const string Text = "stateDiagram-v2\nnote right of X : hello\n";

        // Act
        var data = StateFacts.Parse(Text).GetData(StateFacts.DefaultConfig(Text));

        // Assert
        var state = data.Nodes.Single(n => n.Id == "X");
        Assert.Equal((string.Empty, "X"), (state.Shape, state.Label));
    }

    /// <summary>
    ///     Validates style classes and styles: the class styles compile into the node, an attribute with <c>color</c> gives
    ///     a text style, and a <c>style</c> statement's styles lose their semicolons.
    /// </summary>
    [Fact]
    public void StateModel_GetData_StyleClassesAndStyles_ApplyLikeMermaid()
    {
        // Arrange
        const string Text = "stateDiagram-v2\nclassDef hot fill:#f96,color:#036\nA:::hot --> B\nstyle B stroke:#333;\n";

        // Act
        var model = StateFacts.Parse(Text);
        var data = model.GetData(StateFacts.DefaultConfig(Text));

        // Assert
        var hot = Assert.Single(model.StyleClasses);
        Assert.Equal(["fill:#f96", "color:#036"], hot.Styles);
        Assert.Equal(["fill:#036"], hot.TextStyles);
        Assert.Equal("hot statediagram-state", data.Nodes[0].CssClasses);
        Assert.Equal(["fill:#f96", "color:#036"], data.Nodes[0].CssCompiledStyles);
        Assert.Equal(["stroke:#333"], data.Nodes[1].CssStyles!);
    }

    /// <summary>
    ///     Validates that a state listed again in a <c>class</c> statement gets the class again, as in Mermaid, and that
    ///     CSS classes that grow longer than <see cref="MermaidLimits.MaxLabelLength"/> that way (joined with spaces, with
    ///     <c> statediagram-state</c>, as the node draws them) are refused, located at the statement.
    /// </summary>
    [Fact]
    public void StateModel_SetCssClass_RepeatedIds_ThrowsLimitExceeded()
    {
        // Arrange
        var limits = new MermaidLimits { MaxLabelLength = 30 };

        // Act: three classes and the state class are 30 characters; four are 34
        var ex = Assert.Throws<MermaidSyntaxException>(() => StateFacts.Parse("stateDiagram-v2\nclass A,A,A,A hot\nA\n", null, limits));
        var accepted = StateFacts.Parse("stateDiagram-v2\nclass A,A,A hot\nA\n", null, limits);
        var data = accepted.GetData(StateFacts.DefaultConfig("stateDiagram-v2\nclass A,A,A hot\nA\n"));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(new MermaidLocation(null, 2, 1), ex.Location);
        Assert.Contains("a label is 34 characters long", ex.Message, StringComparison.Ordinal);
        Assert.Equal("hot hot hot statediagram-state", Assert.Single(data.Nodes).CssClasses);
    }

    /// <summary>
    ///     Validates that a style class defined again gets its styles again, as in Mermaid, and that the styles of a style
    ///     class that grow longer than <see cref="MermaidLimits.MaxLabelLength"/> (joined with semicolons) are refused,
    ///     located at the statement, while its styles and its text styles are each checked apart.
    /// </summary>
    [Fact]
    public void StateModel_AddStyleClass_RepeatedDefinitions_ThrowsLimitExceeded()
    {
        // Arrange
        var limits = new MermaidLimits { MaxLabelLength = 20 };
        const string Definition = "classDef c a:b,c:d\n";

        // Act: three definitions are 23 characters of styles; two are 15
        var ex = Assert.Throws<MermaidSyntaxException>(() => StateFacts.Parse("stateDiagram-v2\n" + Definition + Definition + Definition, null, limits));
        var accepted = StateFacts.Parse("stateDiagram-v2\n" + Definition + Definition, null, limits);
        var colored = StateFacts.Parse("stateDiagram-v2\nclassDef c color:red,x:yyyyyy\n", null, limits);

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(new MermaidLocation(null, 4, 1), ex.Location);
        Assert.Equal(["a:b", "c:d", "a:b", "c:d"], Assert.Single(accepted.StyleClasses).Styles);
        Assert.Equal(["fill:red"], Assert.Single(colored.StyleClasses).TextStyles);
    }

    /// <summary>
    ///     Validates that the compiled styles of a state's classes (joined with semicolons) are checked against
    ///     <see cref="MermaidLimits.MaxLabelLength"/>, located at the last statement that gave the state a class, and that
    ///     they are compiled in class order once for each state and shared by its nodes (the state and its note group).
    /// </summary>
    [Fact]
    public void StateModel_GetData_CompiledStyles_AreCheckedAndShared()
    {
        // Arrange
        var limits = new MermaidLimits { MaxLabelLength = 22 };
        const string Accepted = "stateDiagram-v2\nclassDef c a:b,c:d\nclassDef d e:f,g:h,i:j\nclass A c\nclass A d\nA\nnote left of A : n\nA\n";
        const string Refused = "stateDiagram-v2\nclassDef c a:b,c:d\nclassDef d e:f,g:h,i:j,k:l\nclass A c\nclass A d\nA\n";

        // Act: the classes are 22 characters; the compiled styles are 19, and 23 with one more style
        var data = StateFacts.Parse(Accepted, null, limits).GetData(StateFacts.DefaultConfig(Accepted));
        var ex = Assert.Throws<MermaidSyntaxException>(() => StateFacts.Parse(Refused, null, limits));

        // Assert
        var state = data.Nodes.Single(n => n.Id == "A");
        var group = data.Nodes.Single(n => n.Id == "A----parent");
        Assert.Equal(["a:b", "c:d", "e:f", "g:h", "i:j"], state.CssCompiledStyles);
        Assert.Same(state.CssCompiledStyles, group.CssCompiledStyles);
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(new MermaidLocation(null, 5, 1), ex.Location);
        Assert.Contains("a label is 23 characters long", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a composite state with a description beyond its title fails with Mermaid's message, located at
    ///     the statement that added the description.
    /// </summary>
    [Fact]
    public void StateModel_SetRootDocument_CompositeWithDescription_ThrowsLocatedGroupError()
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() =>
            StateFacts.Parse("stateDiagram-v2\nstate \"Title\" as Comp {\n  A\n}\nComp : another\n", "s.mmd"));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Contains("Group nodes can only have label. Remove the additional description for node [Comp]", ex.Message, StringComparison.Ordinal);
        Assert.Equal(new MermaidLocation("s.mmd", 5, 1), ex.Location);
    }

    /// <summary>
    ///     Validates that states, composite states, concurrency regions, notes and note groups count together against
    ///     <see cref="MermaidLimits.MaxNodes"/>, located at the statement that adds the node too many, and that the nodes
    ///     at the limit are accepted.
    /// </summary>
    /// <param name="text">The diagram.</param>
    /// <param name="nodes">The number of nodes it has.</param>
    /// <param name="line">The line of the last node.</param>
    [Theory]
    [InlineData("stateDiagram-v2\nstate C {\n  a\n  --\n  b\n}\n", 5, 5)]
    [InlineData("stateDiagram-v2\nA\nnote left of A : n\n", 3, 3)]
    public void StateModel_Limits_ClustersNotesAndGroups_CountAgainstMaxNodes(string text, int nodes, int line)
    {
        // Act
        var accepted = StateFacts.Parse(text, null, new MermaidLimits { MaxNodes = nodes });
        var ex = Assert.Throws<MermaidSyntaxException>(() => StateFacts.Parse(text, null, new MermaidLimits { MaxNodes = nodes - 1 }));

        // Assert
        Assert.Equal(nodes, accepted.Nodes.Count);
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(line, ex.Location.Line);
        Assert.Contains("nodes and subgraphs (MermaidLimits.MaxNodes)", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the states of the database count against <see cref="MermaidLimits.MaxNodes"/> too, though a
    ///     state that only a <c>class</c> statement names is not drawn.
    /// </summary>
    [Fact]
    public void StateModel_Limits_TooManyStates_ThrowsLimitExceeded()
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() =>
            StateFacts.Parse("stateDiagram-v2\nclass a,b,c,d hot\n", null, new MermaidLimits { MaxNodes = 3 }));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(2, ex.Location.Line);
    }

    /// <summary>
    ///     Validates that transitions count against <see cref="MermaidLimits.MaxEdges"/>, while the edge of a note does not.
    /// </summary>
    [Fact]
    public void StateModel_Limits_TooManyTransitions_ThrowsLimitExceeded()
    {
        // Arrange
        var limits = new MermaidLimits { MaxEdges = 1 };

        // Act
        var accepted = StateFacts.Parse("stateDiagram-v2\nA --> B\nnote left of A : n\n", null, limits);
        var ex = Assert.Throws<MermaidSyntaxException>(() => StateFacts.Parse("stateDiagram-v2\nA --> B\nB --> C\n", null, limits));

        // Assert
        Assert.Equal(2, accepted.Edges.Count);
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(3, ex.Location.Line);
        Assert.Contains("MermaidLimits.MaxEdges", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the concurrency regions of a composite state count as a nesting level against
    ///     <see cref="MermaidLimits.MaxNestingDepth"/>, located at the region too deep.
    /// </summary>
    [Fact]
    public void StateModel_Limits_ConcurrentRegions_CountAsNestingLevels()
    {
        // Arrange
        const string Text = "stateDiagram-v2\nstate A {\n  state B {\n    x\n    --\n    y\n  }\n}\n";

        // Act
        var accepted = StateFacts.Parse(Text, null, new MermaidLimits { MaxNestingDepth = 3 });
        var ex = Assert.Throws<MermaidSyntaxException>(() => StateFacts.Parse(Text, null, new MermaidLimits { MaxNestingDepth = 2 }));

        // Assert
        Assert.Equal(2, accepted.Nodes.Count(n => n.Shape == "divider"));
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(5, ex.Location.Line);
        Assert.Contains("MermaidLimits.MaxNestingDepth", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that composite states nested through names (composite state <c>Si</c> names <c>S(i+1)</c>, which is
    ///     declared again at the top with its own states and keeps the parent it was first given) count against
    ///     <see cref="MermaidLimits.MaxNestingDepth"/> in the layout data, though the text nests them one level deep: a
    ///     state four composite states deep is refused with a limit of 3, located at the state, while three are accepted.
    /// </summary>
    [Fact]
    public void StateModel_GetData_StatesNestedThroughNames_ThrowsLimitExceeded()
    {
        // Arrange: state Si { S(i+1) } on lines 2 + 3i to 4 + 3i; the last holds X
        static string Chain(int count) =>
            "stateDiagram-v2\n" + string.Concat(Enumerable.Range(0, count).Select(i => $"state S{i} {{\n{(i == count - 1 ? "X" : $"S{i + 1}")}\n}}\n"));
        var limits = new MermaidLimits { MaxNestingDepth = 3 };
        var refused = StateFacts.Parse(Chain(4), null, limits);
        var accepted = StateFacts.Parse(Chain(3), null, limits);

        // Act
        var ex = Assert.Throws<MermaidRenderException>(() => refused.GetData(StateFacts.DefaultConfig(Chain(4))));
        var data = accepted.GetData(StateFacts.DefaultConfig(Chain(3)));

        // Assert: X is inside S3, S2, S1 and S0
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(new MermaidLocation(null, 12, 1), ex.Location);
        Assert.Contains("composite states and their concurrent regions are nested deeper than 3 levels (MermaidLimits.MaxNestingDepth)", ex.Message, StringComparison.Ordinal);
        Assert.Equal(("S2", "S1", "S0"), (data.Nodes.Single(n => n.Id == "X").ParentId, data.Nodes.Single(n => n.Id == "S2").ParentId, data.Nodes.Single(n => n.Id == "S1").ParentId));
    }

    /// <summary>
    ///     Validates that composite states nested in each other through names (a cycle) are left to the layout, which fails
    ///     on them located, rather than being taken for a nesting too deep.
    /// </summary>
    [Fact]
    public void StateModel_GetData_NestingCycle_IsLeftToTheLayout()
    {
        // Arrange
        const string Text = "stateDiagram-v2\nstate A {\nB\n}\nstate B {\nA\n}\n";
        var model = StateFacts.Parse(Text);
        var document = MermaidDocument.Parse(Text);

        // Act
        var data = model.GetData(StateFacts.DefaultConfig(Text));
        var ex = Assert.Throws<MermaidRenderException>(() => document.Measure(null, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(("B", "A"), (data.Nodes.Single(n => n.Id == "A").ParentId, data.Nodes.Single(n => n.Id == "B").ParentId));
        Assert.Equal(MermaidDiagnosticCodes.RenderFailed, ex.Code);
    }

    /// <summary>
    ///     Validates that the texts the model joins or derives are checked against
    ///     <see cref="MermaidLimits.MaxLabelLength"/>: the descriptions drawn under a title (joined with line breaks), the
    ///     id of a start state (its parent's id with <c>_start</c>) and a text style (with <c>fill</c> as <c>bgFill</c>).
    /// </summary>
    /// <param name="text">The diagram, whose tokens each fit a limit of 10.</param>
    /// <param name="line">The line of the statement.</param>
    [Theory]
    [InlineData("stateDiagram-v2\nS : orange\nS : yellow\nS : purple\n", 4)]
    [InlineData("stateDiagram-v2\nstate Abcdefgh {\n  [*] --> B\n}\n", 3)]
    [InlineData("stateDiagram-v2\nclassDef c fill color\n", 2)]
    public void StateModel_Limits_JoinedTexts_ThrowLimitExceeded(string text, int line)
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => StateFacts.Parse(text, null, new MermaidLimits { MaxLabelLength = 10 }));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(line, ex.Location.Line);
        Assert.Contains("MermaidLimits.MaxLabelLength", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that joined texts at the limit are accepted: two descriptions under a title, and a start id of exactly
    ///     the limit.
    /// </summary>
    [Fact]
    public void StateModel_Limits_JoinedTextsAtTheLimit_AreAccepted()
    {
        // Arrange
        var limits = new MermaidLimits { MaxLabelLength = 10 };

        // Act
        var model = StateFacts.Parse("stateDiagram-v2\nS : abc\nS : de\nS : fgh\nstate Abcd {\n  [*] --> B\n}\n", null, limits);

        // Assert
        Assert.Equal(["de", "fgh"], model.Nodes[0].Descriptions!);
        Assert.Contains(model.Nodes, n => n.Id == "Abcd_start");
    }

    /// <summary>
    ///     Gets click URLs as written (with their quotes) and what the model keeps of them: safe links as sanitize-url
    ///     writes them, and <c>about:blank</c> for an empty URL and for every encoded form of an unsafe scheme. A
    ///     reference sanitize-url cannot read as a number (<c>&amp;#x6A;</c>, where JavaScript's <c>Number</c> needs
    ///     <c>0x</c>) becomes U+0000 and is removed, so the scheme is no longer <c>javascript</c>.
    /// </summary>
    public static TheoryData<string, string> ClickUrlCases => new()
    {
        { "\"google.com\"", "google.com" },
        { "\"https://Example.com\"", "https://example.com/" },
        { "\"./docs/page.html\"", "./docs/page.html" },
        { "\"\"", "about:blank" },
        { "\"javascript:alert(1)\"", "about:blank" },
        { "\"&#x6A;avascript:alert(1)\"", "avascript:alert(1)" },
        { "\"&#0x6A;avascript:alert(1)\"", "about:blank" },
        { "\"&#106;avascript:alert(1)\"", "about:blank" },
        { "\"&\ufb02\u00b0\u00b0106\u00b6\u00dfavascript:alert(1)\"", "about:blank" },
        { "\"java&#9;script:alert(1)\"", "about:blank" },
        { "\"java\tscript:alert(1)\"", "about:blank" },
        { "\"%6Aavascript:alert(1)\"", "about:blank" },
        { "\"javascript&colon;alert(1)\"", "about:blank" },
        { "\"JaVa&Tab;ScRiPt&CoLoN;alert(1)\"", "about:blank" },
        { "\"JAVASCRIPT:alert(1)\"", "about:blank" },
        { "\"data:text/html;base64,PHNjcmlwdD4=\"", "about:blank" },
        { "\"  DaTa:text/html,x  \"", "about:blank" },
        { "\"vbscript:msgbox(1)\"", "about:blank" },
        { "\"VbScRiPt&colon;msgbox(1)\"", "about:blank" },
    };

    /// <summary>
    ///     Validates that the URL of a click is kept only as sanitize-url checks it, never as written: the quotes are
    ///     removed as Mermaid's renderer removes them, Mermaid's entity placeholders are decoded, and an encoded unsafe
    ///     scheme is <c>about:blank</c>.
    /// </summary>
    /// <param name="url">The URL as written, with its quotes.</param>
    /// <param name="expected">The URL the model keeps.</param>
    [Theory]
    [MemberData(nameof(ClickUrlCases))]
    public void StateModel_CheckLink_ClickUrl_KeepsOnlyTheCheckedUrl(string url, string expected)
    {
        // Act
        var checkedUrl = StateModel.CheckLink(url);

        // Assert
        Assert.Equal(expected, checkedUrl);
    }

    /// <summary>
    ///     Validates through the source that the links of click statements, and the click statements of the statement
    ///     tree, keep only the checked URL: every encoded form of an unsafe scheme (character references, which reach
    ///     the model as Mermaid's entity placeholders, a tab or a tab reference inside the scheme, a percent-encoded
    ///     letter, <c>&amp;colon;</c>, mixed case, <c>data:</c> and <c>vbscript:</c>) is <c>about:blank</c>, a safe URL is
    ///     written as sanitize-url writes it, and the tooltip is kept as Mermaid keeps it. A reference that Mermaid's entity
    ///     encoding changes (<c>&amp;#x6A;</c> loses its <c>#</c>, <c>&amp;#9;</c> gains a second <c>&amp;</c>) reaches the
    ///     check, as it would reach the picture, as text whose scheme is no longer <c>javascript</c>, and is kept as
    ///     sanitize-url gives it.
    /// </summary>
    [Fact]
    public void StateModel_SetRootDocument_EncodedUnsafeClicks_KeepOnlyTheCheckedUrls()
    {
        // Arrange
        (string State, string Statement, string Url)[] cases =
        [
            ("A", "click A \"&#x6A;avascript:alert(1)\" \"tip\"", "&&x6A;avascript:alert(1)"),
            ("B", "click B href \"&#106;avascript:alert(1)\"", "about:blank"),
            ("C", "click C \"java&#9;script:alert(1)\" \"tip\"", "java&script:alert(1)"),
            ("D", "click D href \"java\tscript:alert(1)\"", "about:blank"),
            ("E", "click E href \"%6Aavascript:alert(1)\"", "about:blank"),
            ("F", "click F \"javascript&colon;alert(1)\" \"tip\"", "about:blank"),
            ("G", "click G href \"JaVa&Tab;ScRiPt&CoLoN;alert(1)\"", "about:blank"),
            ("H", "click H href \"data:text/html;base64,PHNjcmlwdD4=\"", "about:blank"),
            ("I", "click I href \"vbscript:msgbox(1)\"", "about:blank"),
            ("J", "click J \"https://Example.com/a/../b c\" \"tip\"", "https://example.com/b%20c"),
        ];
        var text = "stateDiagram-v2\n" + string.Concat(cases.Select(c => c.State + "\n")) + string.Concat(cases.Select(c => c.Statement + "\n"));

        // Act
        var model = StateFacts.Parse(text);

        // Assert: the links and the click statements keep the checked URL alone
        var clicks = model.RootDocument.Where(s => s.Stmt == "click").ToList();
        Assert.Equal(cases.Select(c => (c.State, c.Url)), model.Links.Select(l => (l.Target.Id, l.Url)));
        Assert.Equal(cases.Select(c => c.Url), clicks.Select(s => s.Url));
        Assert.Equal("\"tip\"", model.Links[0].Tooltip);
    }

    /// <summary>
    ///     Validates the metadata the model reports: the states (the start, end, fork and choice states included) are
    ///     counted as nodes, the transitions as edges and the composite states as subgraphs, while notes, note groups,
    ///     note links and concurrent regions are not; the direction is the last top-level statement's, the accessible
    ///     title is kept, and the grammar declares no title.
    /// </summary>
    [Fact]
    public void StateModel_Metadata_CountsStatesTransitionsAndComposites()
    {
        // Arrange
        const string Text = "stateDiagram-v2\n    accTitle: Keys\n    direction LR\n    [*] --> Active\n    state Active {\n" +
            "        [*] --> NumOff\n        --\n        [*] --> CapsOff\n    }\n    state split <<fork>>\n    Active --> split\n" +
            "    split --> [*]\n    note right of Active : two regions\n    direction RL\n";

        // Act
        var model = StateFacts.Parse(Text);

        // Assert
        Assert.Equal((7, 5, 1), (model.NodeCount, model.EdgeCount, model.SubgraphCount));
        Assert.Equal(("RL", "Keys"), (((IMermaidDiagram)model).Direction, model.AccTitle));
        Assert.Null(model.Title);
    }

    /// <summary>
    ///     Validates that the legacy renderer, a look other than classic and a layout other than dagre are reported in
    ///     that order, located at their keys, with hints, and that the unified diagram reports neither renderer.
    /// </summary>
    [Fact]
    public void StateModel_ReportUnsupported_LegacyLookAndLayout_AreReportedInOrder()
    {
        // Arrange
        const string Text = "---\nconfig:\n  look: handDrawn\n  layout: elk\n  state:\n    defaultRenderer: dagre-d3\n---\nstateDiagram\n    A --> B\n";
        var legacy = StateFacts.Parse(Text, settings: new DetectorSettings(null, null, "dagre-d3"));
        var unified = StateFacts.Parse("stateDiagram-v2\n    A --> B\n");
        var sink = new MermaidReportSink(100, throwOnUnsupported: false);
        var plain = new MermaidReportSink(100, throwOnUnsupported: false);
        var reports = new List<MermaidUnsupportedFeature>();
        var none = new List<MermaidUnsupportedFeature>();

        // Act
        legacy.ReportUnsupported(StateFacts.DefaultConfig(Text), sink);
        unified.ReportUnsupported(StateFacts.DefaultConfig("stateDiagram-v2\n    A --> B\n"), plain);
        sink.Deliver(null, reports.Add);
        plain.Deliver(null, none.Add);

        // Assert
        Assert.Equal(
            [(MermaidDiagnosticCodes.LayoutUnsupported, 5), (MermaidDiagnosticCodes.LookUnsupported, 3), (MermaidDiagnosticCodes.LayoutUnsupported, 4)],
            reports.Select(r => (r.Feature, r.Location.Line)));
        Assert.All(reports, r => Assert.NotNull(r.Hint));
        Assert.Contains("dagre-d3", reports[0].Message, StringComparison.Ordinal);
        Assert.Empty(none);
    }

    /// <summary>
    ///     Validates the state renderer's frame of the drawing: the frontmatter title is drawn centered above the drawing
    ///     at the state section's title margin, and the view box is the drawn bounds grown by the padding of 8.
    /// </summary>
    [Fact]
    public void StateModel_Layout_Title_IsAboveTheDrawingInsideThePaddedViewBox()
    {
        // Arrange
        var document = MermaidDocument.Parse("---\ntitle: Lights\n---\nstateDiagram-v2\n    WaitingForTheFirstSwitch\n");

        // Act
        var scene = document.LayoutScene(null, TestContext.Current.CancellationToken);

        // Assert
        var title = Assert.IsType<SceneText>(Assert.Single(SceneQuery.OfRole(scene, "title")));
        Assert.Equal((SceneTextAnchor.Middle, -25.0), (title.Anchor, title.Baseline));
        Assert.True(scene.ViewBox.Encloses(title.Box));
        var box = Assert.Single(SceneQuery.Nodes(scene)).Value.Box;
        Assert.Equal(box.X - 8, scene.ViewBox.X, 0.001);
        Assert.Equal(box.X + box.Width + 8, scene.ViewBox.X + scene.ViewBox.Width, 0.001);
        Assert.Equal(box.Y + box.Height + 8, scene.ViewBox.Y + scene.ViewBox.Height, 0.001);
    }

    /// <summary>
    ///     Validates that a state without a shape (a note on a state seen nowhere before it) fails the render, located at
    ///     the note, with Mermaid's own message, as Mermaid's render fails.
    /// </summary>
    [Fact]
    public void StateModel_Layout_StateWithoutShape_FailsLocatedAtItsNote()
    {
        // Arrange
        var document = MermaidDocument.Parse("stateDiagram-v2\n    A --> B\n    note right of X : hello\n", new MermaidOpenOptions { SourceName = "s.mmd" });

        // Act
        var exception = Assert.Throws<MermaidRenderException>(() => document.LayoutScene(null, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.RenderFailed, exception.Code);
        Assert.Equal(new MermaidLocation("s.mmd", 3, 5), exception.Location);
        Assert.Contains("No such shape: undefined", exception.Message, StringComparison.Ordinal);
        Assert.NotNull(exception.Hint);
    }
}
