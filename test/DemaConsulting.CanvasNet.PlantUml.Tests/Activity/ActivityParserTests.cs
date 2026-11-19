using DemaConsulting.CanvasNet.PlantUml.Activity;
using DemaConsulting.CanvasNet.PlantUml.Styling;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Activity;

/// <summary>
///     Unit tests for the <c>ActivityParser</c> unit: the statements of an activity diagram of the new syntax, their
///     errors, their reports, and their limits.
/// </summary>
public class ActivityParserTests
{
    /// <summary>
    ///     Validates the actions: one line, several lines, a color, and the start, stop and end nodes.
    /// </summary>
    [Fact]
    public void PlantUmlActivityParser_Actions_ReadTextColorAndNodes()
    {
        // Act
        var model = ActivityTestHelpers.Parse("start\n:one;\n#pink:two\nlines;\nstop\n:three;\nend");

        // Assert
        var items = model.Root.Items;
        Assert.IsType<ActivityStart>(items[0]);
        Assert.Equal("one", Assert.IsType<ActivityAction>(items[1]).Text);
        var two = Assert.IsType<ActivityAction>(items[2]);
        Assert.Equal("two\nlines", two.Text);
        Assert.Equal(new Canvas.Rgba32(0xFF, 0xC0, 0xCB, 255), two.Fill);
        Assert.IsType<ActivityStop>(items[3]);
        Assert.IsType<ActivityEnd>(items[5]);
        Assert.Equal(4, items[2].Location.Line);
    }

    /// <summary>
    ///     Validates the ifs: the test, the labels, the elseif branches and the else branch.
    /// </summary>
    [Fact]
    public void PlantUmlActivityParser_If_ReadsBranchesAndLabels()
    {
        // Act
        var model = ActivityTestHelpers.Parse("if (a?) then (yes)\n:x;\nelseif (b?) then (also)\n:y;\nelse (no)\n:z;\nendif\nif (c) is (on) then\nendif");

        // Assert
        var branches = Assert.IsType<ActivityIf>(model.Root.Items[0]);
        Assert.Equal(["a?", "b?"], branches.Thens.Select(t => t.Test));
        Assert.Equal(["yes", "also"], branches.Thens.Select(t => t.LabelPositive.Label));
        Assert.Equal("no", branches.Else!.LabelPositive.Label);
        Assert.Equal("z", Assert.IsType<ActivityAction>(branches.Else.List.Items.Single()).Text);
        var second = Assert.IsType<ActivityIf>(model.Root.Items[1]);
        Assert.Equal("on", second.Thens[0].LabelPositive.Label);
    }

    /// <summary>
    ///     Validates the loops: a while with its labels and backward action, and a repeat with its test, labels and
    ///     backward action.
    /// </summary>
    [Fact]
    public void PlantUmlActivityParser_Loops_ReadTestsLabelsAndBackward()
    {
        // Act
        var model = ActivityTestHelpers.Parse("while (more?) is (yes)\n:work;\nbackward :rest;\nendwhile (no)\nrepeat :first;\n:again;\nbackward :retry;\nrepeat while (fail?) is (yes) not (no)");

        // Assert
        var loop = Assert.IsType<ActivityWhile>(model.Root.Items[0]);
        Assert.Equal(("more?", "yes", "rest"), (loop.Test, loop.Yes, loop.Backward));
        Assert.Equal("no", loop.OutLink.Label);
        var repeat = Assert.IsType<ActivityRepeat>(model.Root.Items[1]);
        Assert.Equal(("first", "fail?", "yes", "no", "retry"), (repeat.StartLabel, repeat.Test, repeat.Yes, repeat.Out, repeat.Backward));
    }

    /// <summary>
    ///     Validates the forks and splits: their branches, a merge, and the label of the end.
    /// </summary>
    [Fact]
    public void PlantUmlActivityParser_ForksAndSplits_ReadBranchesAndEnds()
    {
        // Act
        var model = ActivityTestHelpers.Parse("fork\n:a;\nfork again\n:b;\nfork again\n:c;\nend fork {all}\nfork\n:d;\nfork again\n:e;\nend merge\nsplit\n:f;\nsplit again\n:g;\nend split");

        // Assert
        var fork = Assert.IsType<ActivityFork>(model.Root.Items[0]);
        Assert.Equal(3, fork.Branches.Count);
        Assert.Equal(("all", ActivityForkStyle.Fork), (fork.Label, fork.Style));
        Assert.Equal(ActivityForkStyle.Merge, Assert.IsType<ActivityFork>(model.Root.Items[1]).Style);
        Assert.Equal(2, Assert.IsType<ActivitySplit>(model.Root.Items[2]).Branches.Count);
    }

    /// <summary>
    ///     Validates the arrows: a label, a color and a style lead into the next instruction.
    /// </summary>
    [Fact]
    public void PlantUmlActivityParser_Arrows_LeadIntoTheNextInstruction()
    {
        // Act
        var model = ActivityTestHelpers.Parse(":a;\n-> sent;\n:b;\n-[#blue,dashed]-> reply;\n:c;");

        // Assert
        var items = model.Root.Items;
        Assert.Equal("sent", items[1].InLink.Label);
        Assert.Equal("reply", items[2].InLink.Label);
        Assert.Equal(new Canvas.Rgba32(0, 0, 0xFF, 255), items[2].InLink.Color);
        Assert.Equal(ActivityLinkStyle.Dashed, items[2].InLink.Style);
    }

    /// <summary>
    ///     Validates the swimlanes: their order, their colors, and the swimlane of each instruction.
    /// </summary>
    [Fact]
    public void PlantUmlActivityParser_Swimlanes_ReadOrderColorsAndPlacement()
    {
        // Act
        var model = ActivityTestHelpers.Parse("|A|\nstart\n:one;\n|#AntiqueWhite|B|\n:two;\n|A|\n:three;");

        // Assert
        Assert.Equal(["A", "B"], model.Lanes.Select(l => l.Name));
        Assert.Equal(new Canvas.Rgba32(0xFA, 0xEB, 0xD7, 255), model.Lanes[1].Fill);
        Assert.Equal(["A", "A", "B", "A"], model.Root.Items.Select(i => i.Lane!.Name));
    }

    /// <summary>
    ///     Validates the notes, the connectors, the groups, and the end of a flow.
    /// </summary>
    [Fact]
    public void PlantUmlActivityParser_NotesConnectorsGroupsAndKill_AreRead()
    {
        // Act
        var model = ActivityTestHelpers.Parse("partition Work {\n:a;\nnote right: hi\nfloating note left\nfree\nend note\n}\n(A)\n:b;\nkill\npackage P {\n:c;\n}");

        // Assert
        var group = Assert.IsType<ActivityGroup>(model.Root.Items[0]);
        Assert.True(group.Framed);
        Assert.Equal("Work", group.Title);
        var notes = group.Body.Items[0].Notes;
        Assert.Equal([ActivityNotePosition.Right, ActivityNotePosition.Left], notes.Select(n => n.Position));
        Assert.Equal([false, true], notes.Select(n => n.Floating));
        Assert.Equal("A", Assert.IsType<ActivitySpot>(model.Root.Items[1]).Text);
        Assert.True(Assert.IsType<ActivityAction>(model.Root.Items[2]).Killed);
        Assert.False(Assert.IsType<ActivityGroup>(model.Root.Items[3]).Framed);
        Assert.Single(model.Reports, r => r.Code == PlantUmlDiagnosticCodes.StatementUnsupported && r.Location.Line == 12);
    }

    /// <summary>
    ///     Validates the settings: the page texts, the skin parameters, and the scale.
    /// </summary>
    [Fact]
    public void PlantUmlActivityParser_Settings_ReadPageTextsSkinParamsAndScale()
    {
        // Act
        var model = ActivityTestHelpers.Parse("title Flow\nheader Draft\nfooter Page\ncaption Fig\nlegend\nKey\nendlegend\nscale 1.5\nskinparam activityBackgroundColor #00FF00\nstart");

        // Assert
        Assert.Equal(("Flow", "Draft", "Page", "Fig", "Key"), (model.Page.Title!.Text, model.Page.Header!.Text, model.Page.Footer!.Text, model.Page.Caption!.Text, model.Page.Legend!.Text));
        Assert.Equal(1.5, model.Scale);
        Assert.Equal(new Canvas.Rgba32(0, 0xFF, 0, 255), model.Theme[SkinElement.Activity].Fill);
    }

    /// <summary>
    ///     Validates that statements that are not drawn are reported at their lines and skipped.
    /// </summary>
    [Fact]
    public void PlantUmlActivityParser_UnsupportedStatements_AreReportedAndSkipped()
    {
        // Act
        var model = ActivityTestHelpers.Parse(":a;\nswitch (x)\ncase (1)\n:b;\nendswitch\n:c|\ngoto here\n:d;");

        // Assert
        Assert.Equal([3, 7, 8], model.Reports.Where(r => r.Code == PlantUmlDiagnosticCodes.StatementUnsupported).Select(r => r.Location.Line));
        Assert.Equal(["a", "c", "d"], model.Root.Items.OfType<ActivityAction>().Select(a => a.Text));
    }

    /// <summary>
    ///     Validates that a malformed or misplaced statement fails with a located syntax error and a hint.
    /// </summary>
    /// <param name="body">The statements.</param>
    /// <param name="line">The line of the error (the diagram starts at line 2).</param>
    [Theory]
    [InlineData(":a;\nelse\n:b;", 3)]
    [InlineData("if (x) then\n:a;", 2)]
    [InlineData(":a;\nendwhile", 3)]
    [InlineData(":never closed\nstop", 2)]
    [InlineData("fork again", 2)]
    [InlineData(":a;\n|Late|\n:b;", 3)]
    public void PlantUmlActivityParser_InvalidStatements_FailLocated(string body, int line)
    {
        // Act
        var ex = Assert.Throws<PlantUmlSyntaxException>(() => ActivityTestHelpers.Parse(body));

        // Assert
        Assert.Equal(PlantUmlDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Equal(line, ex.Location.Line);
        Assert.False(string.IsNullOrEmpty(ex.Hint));
    }

    /// <summary>
    ///     Validates that the limits of nodes, edges, nesting and label length are enforced while reading.
    /// </summary>
    [Fact]
    public void PlantUmlActivityParser_Limits_AreEnforced()
    {
        // Act
        var nodes = Assert.Throws<PlantUmlSyntaxException>(() => ActivityTestHelpers.Parse(":a;\n:b;\n:c;", new PlantUmlLimits { MaxNodes = 2 }));
        var edges = Assert.Throws<PlantUmlSyntaxException>(() => ActivityTestHelpers.Parse(":a;\n:b;\n:c;", new PlantUmlLimits { MaxEdges = 1 }));
        var depth = Assert.Throws<PlantUmlSyntaxException>(() => ActivityTestHelpers.Parse("if (a) then\nif (b) then\nendif\nendif", new PlantUmlLimits { MaxNestingDepth = 1 }));
        var label = Assert.Throws<PlantUmlSyntaxException>(() => ActivityTestHelpers.Parse(":" + new string('x', 40) + ";", new PlantUmlLimits { MaxLabelLength = 10 }));

        // Assert
        Assert.All([nodes, edges, depth, label], e => Assert.Equal(PlantUmlDiagnosticCodes.LimitExceeded, e.Code));
        Assert.Equal(4, nodes.Location.Line);
        Assert.Equal(3, depth.Location.Line);
        Assert.Contains("MaxEdges", edges.Message, StringComparison.Ordinal);
    }
}
