using DemaConsulting.CanvasNet.PlantUml.Activity;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Activity;

/// <summary>
///     Unit tests for the <c>ActivityModel</c> unit: the facts of an activity diagram and its layout entry point.
/// </summary>
public class ActivityModelTests
{
    /// <summary>
    ///     Validates the facts: actions and nodes are nodes, arrows are edges, framed groups and swimlanes are
    ///     subgraphs, and the title.
    /// </summary>
    [Fact]
    public void PlantUmlActivityModel_Facts_CountNodesEdgesAndSubgraphs()
    {
        // Act
        var fork = ActivityTestHelpers.Parse("start\n:Receive order;\nfork\n:Bill;\nfork again\n:Pick;\n:Pack;\nfork again\n:Notify;\nend fork\n:Ship;\nstop");
        var groups = ActivityTestHelpers.Parse("title Work\n|A|\nstart\npartition P {\n:x;\n:y;\n}\n|B|\n:z;\nstop");

        // Assert
        Assert.Equal((8, 11, 0), (fork.NodeCount, fork.EdgeCount, fork.SubgraphCount));
        Assert.Equal((5, 4, 3), (groups.NodeCount, groups.EdgeCount, groups.SubgraphCount));
        Assert.Equal("Work", groups.Title);
        Assert.Null(fork.Title);
    }

    /// <summary>
    ///     Validates that a decision counts once per test and a loop once, with its arrows.
    /// </summary>
    [Fact]
    public void PlantUmlActivityModel_Facts_CountDecisionsAndLoops()
    {
        // Act
        var model = ActivityTestHelpers.Parse("if (a) then\n:x;\nelseif (b) then\n:y;\nelse\n:z;\nendif\nwhile (c)\n:w;\nendwhile");

        // Assert
        Assert.Equal(2 + 3 + 1 + 1, model.NodeCount);
        Assert.Equal(1 + (3 * 2) + 3, model.EdgeCount);
    }

    /// <summary>
    ///     Validates that the layout of the model draws the page texts around the body, the theme's background, and
    ///     the diagram's scale.
    /// </summary>
    [Fact]
    public void PlantUmlActivityModel_Layout_DecoratesThePageAndScales()
    {
        // Arrange
        var model = ActivityTestHelpers.Parse("scale 2\ntitle Order\nlegend\nNote\nendlegend\nstart\n:a;\nstop");

        // Act
        var scene = model.Layout(ActivityTestHelpers.Context());

        // Assert
        Assert.Equal(2, scene.Scale);
        Assert.Equal(model.Theme.Background, scene.Background);
        var title = Assert.Single(ActivityTestHelpers.Texts(scene, "title"));
        var action = ActivityTestHelpers.Bounds(scene, "activity")[0];
        Assert.True(title.Baseline < action.Y);
        Assert.True(ActivityTestHelpers.Texts(scene, "legend")[0].Baseline > action.Bottom);
    }

    /// <summary>
    ///     Validates that the reports of the parser are kept in text order on the model.
    /// </summary>
    [Fact]
    public void PlantUmlActivityModel_Reports_AreInTextOrder()
    {
        // Act
        var model = ActivityTestHelpers.Parse("skinparam unknownThing 3\ngoto x\n:a;\nlabel y");

        // Assert
        Assert.Equal(model.Reports.OrderBy(r => r.Location.Line).Select(r => r.Location.Line), model.Reports.Select(r => r.Location.Line));
        Assert.True(model.Reports.Count >= 3);
    }

    /// <summary>
    ///     Validates that the model is a diagram of the pipeline: the registry hands an activity diagram to it.
    /// </summary>
    [Fact]
    public void PlantUmlActivityModel_Registry_ParsesActivityDiagrams()
    {
        // Act
        var document = PlantUmlDocument.Parse("@startuml\nstart\n:a;\nstop\n@enduml\n");

        // Assert
        Assert.True(document.IsSupported);
        Assert.Equal("activity", document.DiagramType);
        Assert.IsType<ActivityModel>(document.Diagram);
        Assert.IsType<Scene>(document.LayoutScene(cancellationToken: TestContext.Current.CancellationToken));
    }
}
