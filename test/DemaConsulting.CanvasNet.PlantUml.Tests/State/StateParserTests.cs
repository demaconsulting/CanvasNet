using DemaConsulting.CanvasNet.PlantUml.State;
using DemaConsulting.CanvasNet.PlantUml.Styling;
using DemaConsulting.CanvasNet.PlantUml.Svek;
using static DemaConsulting.CanvasNet.PlantUml.Tests.State.StateTestHelpers;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.State;

/// <summary>
///     Unit tests for the <c>StateParser</c> unit: declarations, descriptions, transitions, pseudo-states, composite
///     states and their regions, notes, the common statements, the reports, and the errors.
/// </summary>
public class StateParserTests
{
    /// <summary>
    ///     Validates the forms of a state declaration: a name, a quoted display with an alias in both orders, and a quoted
    ///     name alone.
    /// </summary>
    [Fact]
    public void PlantUmlStateParser_Parse_Declarations_ReadNamesAndDisplays()
    {
        // Act
        var model = Parse("state Idle\nstate \"Waiting for input\" as Waiting\nstate Done as \"All done\"\nstate \"Quoted\"");

        // Assert
        Assert.Equal(["Idle", "Waiting", "Done", "Quoted"], model.Elements.Select(e => e.Id));
        Assert.Equal(["Idle", "Waiting for input", "All done", "Quoted"], model.Elements.Select(e => e.Display));
        Assert.All(model.Elements, e => Assert.Equal(StateKind.State, e.Kind));
    }

    /// <summary>
    ///     Validates that a transition creates its states, reads its length, direction, style, color, and label as
    ///     written, and that a reverse arrow puts the head at its first end.
    /// </summary>
    [Fact]
    public void PlantUmlStateParser_Parse_Transitions_ReadTheirArrows()
    {
        // Act
        var model = Parse("A -> B : one dash\nB ---> C\nC -up-> A : \"quoted\" >\nA -[dashed]-> C\nB -[#red,thickness=2]-> A\nC <-- D");

        // Assert
        var t = model.Transitions;
        Assert.Equal(["A", "B", "C", "D"], model.Elements.Select(e => e.Id));
        Assert.Equal((1, "one dash"), (t[0].Length, t[0].Label));
        Assert.Equal(3, t[1].Length);
        Assert.Null(t[1].Label);
        Assert.Equal((LinkDirection.Up, "\"quoted\" >"), (t[2].Direction, t[2].Label));
        Assert.Equal(LinkStyle.Dashed, t[3].Style);
        Assert.Equal((new Canvas.Rgba32(0xFF, 0, 0, 255), 2.0), (t[4].Color!.Value, t[4].Thickness!.Value));
        Assert.Equal((LinkDecor.Arrow, LinkDecor.None), (t[5].TailDecor, t[5].HeadDecor));
        Assert.All(t.Take(5), x => Assert.Equal((LinkDecor.None, LinkDecor.Arrow), (x.TailDecor, x.HeadDecor)));
        Assert.Empty(model.Reports);
    }

    /// <summary>
    ///     Validates the start, end, and history pseudo-states: one start and one end per composite state with PlantUML's
    ///     ids, the history of the current composite state, and the history of a named state (made composite).
    /// </summary>
    [Fact]
    public void PlantUmlStateParser_Parse_PseudoStates_AreScoped()
    {
        // Act
        var model = Parse("[*] --> Active\nstate Active {\n[*] --> Idle\nIdle --> [*]\nIdle --> [H]\n}\nActive --> [*]\nPaused --> Editor[H*]\nPaused --> Editor[H]");

        // Assert
        Assert.Equal(StateKind.Start, ElementOf(model, ".start.").Kind);
        Assert.Equal(StateKind.End, ElementOf(model, ".end.").Kind);
        Assert.Equal(StateKind.Start, ElementOf(model, "Active..start.Active").Kind);
        Assert.Equal(StateKind.End, ElementOf(model, "Active..end.Active").Kind);
        Assert.Equal(StateKind.History, ElementOf(model, "Active..historical.Active").Kind);
        Assert.Equal(StateKind.DeepHistory, ElementOf(model, "Editor..deephistory.Editor").Kind);
        Assert.Equal(StateKind.History, ElementOf(model, "Editor..historical.Editor").Kind);
        Assert.True(ElementOf(model, "Editor").IsComposite);
        Assert.Same(ElementOf(model, "Active"), ElementOf(model, "Active.Idle").Region!.Owner);
        Assert.Equal((9, 7, 2), (model.NodeCount, model.EdgeCount, model.SubgraphCount));
    }

    /// <summary>
    ///     Validates nested composite states: the ids join the composite's id, names resolve in the composite states
    ///     around and by a unique name, and <c>end state</c> closes a block opened with <c>begin</c>.
    /// </summary>
    [Fact]
    public void PlantUmlStateParser_Parse_CompositeStates_NestAndResolve()
    {
        // Act
        var model = Parse("state Outside\nstate Active {\nstate Online {\nIdle --> Busy\nBusy --> Outside\n}\nOnline --> Offline\n}\nstate Later begin\nX --> Y\nend state\nIdle --> Later");

        // Assert
        Assert.Equal(
            ["Outside", "Active", "Active.Online", "Active.Online.Idle", "Active.Online.Busy", "Active.Offline", "Later", "Later.X", "Later.Y"],
            model.Elements.Select(e => e.Id));
        Assert.Same(ElementOf(model, "Outside"), model.Transitions[1].To);
        Assert.Same(ElementOf(model, "Active.Online.Idle"), model.Transitions[^1].From);
        Assert.Equal(3, model.SubgraphCount);
        Assert.Equal(1, ElementOf(model, "Active.Online").Depth);
    }

    /// <summary>
    ///     Validates the concurrent regions: each separator starts a region named as PlantUML names it (numbered across
    ///     the diagram), and the first separator of a composite state chooses stacked or side by side.
    /// </summary>
    [Fact]
    public void PlantUmlStateParser_Parse_ConcurrentRegions_AreNumbered()
    {
        // Act
        var model = Parse("state Running {\n[*] --> A\n--\n[*] --> B\n---\nstate C\n}\nstate Panel {\nstate L\n||\nstate R\n}");

        // Assert
        var running = ElementOf(model, "Running");
        var panel = ElementOf(model, "Panel");
        Assert.Equal(["Running", "Running.CONC1", "Running.CONC2"], running.Regions.Select(r => r.Prefix));
        Assert.Equal(["Panel", "Panel.CONC3"], panel.Regions.Select(r => r.Prefix));
        Assert.Equal((ClusterConcurrency.Stacked, ClusterConcurrency.SideBySide), (running.Concurrency, panel.Concurrency));
        Assert.Equal("Running.CONC1..start.CONC1", model.Elements.Single(e => e.Kind == StateKind.Start && e.Region!.Index == 1).Id);
        Assert.Equal(["Running.CONC2.C", "Panel.L", "Panel.CONC3.R"], model.Elements.Where(e => e.Id.EndsWith(".C", StringComparison.Ordinal) || e.Id.EndsWith('L') || e.Id.EndsWith('R')).Select(e => e.Id));
    }

    /// <summary>
    ///     Validates the stereotypes that make a state a pseudo-state, and that the symbols PlantUML draws on a border are
    ///     reported and drawn as states.
    /// </summary>
    /// <param name="stereotype">The stereotype.</param>
    /// <param name="kind">The expected kind (a <see cref="StateKind"/> name).</param>
    /// <param name="reported">Whether it is reported.</param>
    [Theory]
    [InlineData("choice", "Choice", false)]
    [InlineData("fork", "ForkJoin", false)]
    [InlineData("join", "ForkJoin", false)]
    [InlineData("end", "End", false)]
    [InlineData("start", "Start", false)]
    [InlineData("junction", "Junction", false)]
    [InlineData("history", "History", false)]
    [InlineData("history*", "DeepHistory", false)]
    [InlineData("entryPoint", "State", true)]
    [InlineData("exitPoint", "State", true)]
    [InlineData("inputPin", "State", true)]
    [InlineData("expansionOutput", "State", true)]
    [InlineData("sdlreceive", "State", true)]
    [InlineData("Custom", "State", false)]
    public void PlantUmlStateParser_Parse_Stereotypes_ChooseTheKind(string stereotype, string kind, bool reported)
    {
        // Act
        var model = Parse($"state P <<{stereotype}>>\nP --> Q");

        // Assert
        Assert.Equal(Enum.Parse<StateKind>(kind), ElementOf(model, "P").Kind);
        Assert.Equal(reported, model.Reports.Any(r => r.Code == PlantUmlDiagnosticCodes.StatementUnsupported));
    }

    /// <summary>
    ///     Validates the descriptions: after a colon in the declaration, with <c>Name : text</c> (creating the state),
    ///     and in a quoted name; a description of a pseudo-state is reported.
    /// </summary>
    [Fact]
    public void PlantUmlStateParser_Parse_Descriptions_AddLines()
    {
        // Act
        var model = Parse("state Heating : the element is on\nHeating : keeps 21 degrees\nCooling : the fan runs\n\"Cooling\" : ----\nstate J <<join>>\nJ : ignored");

        // Assert
        Assert.Equal(["the element is on", "keeps 21 degrees"], ElementOf(model, "Heating").Descriptions);
        Assert.Equal(["the fan runs", "----"], ElementOf(model, "Cooling").Descriptions);
        Assert.Empty(ElementOf(model, "J").Descriptions);
        Assert.Equal(7, Assert.Single(model.Reports).Location.Line);
    }

    /// <summary>
    ///     Validates the colors of a declaration: a fill, a line color with <c>##</c> or <c>line:</c>, and a line style
    ///     that is reported.
    /// </summary>
    [Fact]
    public void PlantUmlStateParser_Parse_Colors_AreRead()
    {
        // Act
        var model = Parse("state A #FFDDDD\nstate B ##[dashed]blue\nstate C #lightgreen;line:red {\n}\nstate D #pink : text");

        // Assert
        Assert.Equal(new Canvas.Rgba32(0xFF, 0xDD, 0xDD, 255), ElementOf(model, "A").Fill);
        Assert.Equal(new Canvas.Rgba32(0, 0, 0xFF, 255), ElementOf(model, "B").Line);
        Assert.Equal(new Canvas.Rgba32(0xFF, 0, 0, 255), ElementOf(model, "C").Line);
        Assert.True(ElementOf(model, "C").IsComposite);
        Assert.Equal(["text"], ElementOf(model, "D").Descriptions);
        Assert.Equal(3, Assert.Single(model.Reports).Location.Line);
    }

    /// <summary>
    ///     Validates the notes: on each side of a state, on the last transition, floating with an alias (and linked), on
    ///     the last element, and on a composite state (reported and drawn floating in its region).
    /// </summary>
    [Fact]
    public void PlantUmlStateParser_Parse_Notes_AreAttached()
    {
        // Act
        var model = Parse("A --> B : go\nnote on link : buffered\nnote left of A : left\nnote right of B\nline one\nline two\nend note\nnote \"shared\" as N\nN --> A\nstate C {\nstate X\n}\nnote top of C : on composite\nstate D\nnote bottom : last");

        // Assert
        var notes = model.Notes;
        Assert.Same(model.Transitions[0], notes[0].OnLink);
        Assert.Equal((NotePosition.Left, "A"), (notes[1].Position, notes[1].Target!.Id));
        Assert.Equal("line one\nline two", notes[2].Text);
        Assert.True(notes[3].IsFloating);
        Assert.Equal("N", notes[3].Id);
        Assert.True(model.Transitions[1].TouchesNote);
        Assert.True(notes[4].IsFloating);
        Assert.Null(notes[4].Region);
        Assert.Equal((NotePosition.Bottom, "D"), (notes[5].Position, notes[5].Target!.Id));
        Assert.Equal(14, Assert.Single(model.Reports).Location.Line);
        Assert.Equal(1, model.EdgeCount);
    }

    /// <summary>
    ///     Validates the common statements: <c>hide empty description</c> (other <c>hide</c> forms are reported), page
    ///     texts, the direction, <c>scale</c>, the layout skin parameters, and the state skin parameters.
    /// </summary>
    [Fact]
    public void PlantUmlStateParser_Parse_CommonStatements_AreRead()
    {
        // Act
        var model = Parse("hide empty description\nhide stereotype\ntitle Door\nleft to right direction\nscale 2\nskinparam nodesep 50\nskinparam stateBackgroundColor #EEEEFF\nskinparam stateStartColor green\nA --> B");

        // Assert
        Assert.True(model.HideEmptyDescription);
        Assert.Equal("Door", model.Title);
        Assert.Equal((SvekRankDir.LeftToRight, 2.0, 50.0), (model.RankDir, model.Scale, model.NodeSep!.Value));
        Assert.Equal(new Canvas.Rgba32(0xEE, 0xEE, 0xFF, 255), model.Theme[SkinElement.State].Fill);
        Assert.Equal(new Canvas.Rgba32(0, 0x80, 0, 255), model.Theme[SkinElement.StateStart].Fill);
        Assert.Equal(3, Assert.Single(model.Reports).Location.Line);
    }

    /// <summary>
    ///     Validates the parts of statements that are read but not drawn: a cross at the start of a transition, a
    ///     stereotype of a transition, a concurrent separator outside a composite state, a <c>json</c> block, and
    ///     <c>remove</c>.
    /// </summary>
    [Fact]
    public void PlantUmlStateParser_Parse_UnsupportedParts_AreReported()
    {
        // Act
        var model = Parse("A x--> B\nA --> B <<event>>\n--\njson J {\n\"a\": 1\n}\nremove B");

        // Assert
        Assert.Equal([2, 3, 4, 5, 8], model.Reports.Select(r => r.Location.Line));
        Assert.All(model.Reports, r => Assert.Equal(PlantUmlDiagnosticCodes.StatementUnsupported, r.Code));
        Assert.Equal(LinkDecor.None, model.Transitions[0].TailDecor);
    }

    /// <summary>
    ///     Validates the located syntax errors: an arrow that is not a transition, an unclosed composite state, a
    ///     closing brace without a composite state, a transition between concurrent regions, a pseudo-state made
    ///     composite, a statement of another diagram, a bad color, and a state without a name.
    /// </summary>
    /// <param name="body">The diagram body.</param>
    /// <param name="line">The line of the error.</param>
    [Theory]
    [InlineData("A --> B\nA ..> B", 3)]
    [InlineData("A --> B\nA --* B", 3)]
    [InlineData("state A {\nB --> C", 2)]
    [InlineData("A --> B\n}", 3)]
    [InlineData("state A {\nstate B\n--\nstate C\n}\nB --> C", 7)]
    [InlineData("state A {\nstate B\n--\nstate C\n}\nC --> D", 7)]
    [InlineData("state J <<join>>\nstate J {\n}", 3)]
    [InlineData("A --> B\nclass C", 3)]
    [InlineData("state A #notacolor", 2)]
    [InlineData("state \nA --> B", 2)]
    [InlineData("state A {\n}\nstate A <<choice>>", 4)]
    [InlineData("A --> B\nstate A junk", 3)]
    public void PlantUmlStateParser_Parse_Errors_AreLocated(string body, int line)
    {
        // Act
        var ex = Assert.Throws<PlantUmlSyntaxException>(() => Parse(body));

        // Assert
        Assert.Equal(PlantUmlDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Equal(line, ex.Location.Line);
        Assert.False(string.IsNullOrEmpty(ex.Hint));
    }

    /// <summary>
    ///     Validates that a transition from the first region of a composite state with concurrent regions to a state
    ///     outside it is kept (drawn from the composite state's border) and reported.
    /// </summary>
    [Fact]
    public void PlantUmlStateParser_Parse_TransitionLeavingFirstRegion_IsReported()
    {
        // Act
        var model = Parse("state A {\nstate B\n--\nstate C\n}\nB --> D");

        // Assert
        Assert.Equal(7, Assert.Single(model.Reports).Location.Line);
        Assert.Single(model.Transitions);
    }

    /// <summary>
    ///     Validates that the limits fail closed at the statement that crosses them: the states and notes, the
    ///     transitions, a text length, and the nesting of composite states.
    /// </summary>
    /// <param name="limit">The limit.</param>
    /// <param name="body">The diagram body.</param>
    /// <param name="line">The line that crosses the limit.</param>
    [Theory]
    [InlineData("MaxNodes", "state A\nstate B\nnote \"n\" as N", 4)]
    [InlineData("MaxEdges", "A --> B\nB --> A\nA --> A", 4)]
    [InlineData("MaxLabelLength", "state A\nA : a description far too long", 3)]
    [InlineData("MaxNestingDepth", "state A {\nstate B {\nstate C {\n}\n}\n}", 4)]
    public void PlantUmlStateParser_Limits_FailClosedAtTheLine(string limit, string body, int line)
    {
        // Arrange
        var limits = limit switch
        {
            "MaxNodes" => new PlantUmlLimits { MaxNodes = 2 },
            "MaxEdges" => new PlantUmlLimits { MaxEdges = 2 },
            "MaxLabelLength" => new PlantUmlLimits { MaxLabelLength = 10 },
            _ => new PlantUmlLimits { MaxNestingDepth = 2 },
        };

        // Act
        var ex = Assert.Throws<PlantUmlSyntaxException>(() => Parse(body, limits));

        // Assert
        Assert.Equal(PlantUmlDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(line, ex.Location.Line);
        Assert.Contains(limit, ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the concurrent separator test: two or more dashes or bars, nothing else.
    /// </summary>
    [Fact]
    public void PlantUmlStateParser_IsConcurrentSeparator_RecognizesDashesAndBars()
    {
        // Act / Assert
        Assert.True(StateParser.IsConcurrentSeparator("--"));
        Assert.True(StateParser.IsConcurrentSeparator("||||"));
        Assert.False(StateParser.IsConcurrentSeparator("-"));
        Assert.False(StateParser.IsConcurrentSeparator("-|"));
        Assert.False(StateParser.IsConcurrentSeparator("-->"));
    }
}
