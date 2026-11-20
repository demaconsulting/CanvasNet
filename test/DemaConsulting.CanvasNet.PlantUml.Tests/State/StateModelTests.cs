using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.State;
using DemaConsulting.CanvasNet.PlantUml.Svek;
using static DemaConsulting.CanvasNet.PlantUml.Tests.State.StateTestHelpers;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.State;

/// <summary>
///     Unit tests for the <c>StateModel</c> unit: the facts, the lowering for the Svek engine, and the layout.
/// </summary>
public class StateModelTests
{
    /// <summary>
    ///     Validates the facts: simple states and pseudo-states are nodes, composite states subgraphs, and transitions
    ///     that do not touch a note edges.
    /// </summary>
    [Fact]
    public void PlantUmlStateModel_Facts_CountStatesTransitionsAndComposites()
    {
        // Act
        var model = Parse("[*] --> A\nstate A {\n[*] --> B\n}\nA --> C : go\nnote \"n\" as N\nN --> C\ntitle Machine");

        // Assert
        Assert.Equal((4, 3, 1), (model.NodeCount, model.EdgeCount, model.SubgraphCount));
        Assert.Equal("Machine", model.Title);
    }

    /// <summary>
    ///     Validates the lowering: simple elements become entities in the cluster of their composite state, composite
    ///     states clusters with their images, and the links and notes keep the text order.
    /// </summary>
    [Fact]
    public void PlantUmlStateModel_Lower_CompositeStates_BecomeClusters()
    {
        // Arrange
        var model = Parse("left to right direction\n[*] --> A\nstate A {\nstate Inner {\nX --> Y\n}\n}\nnote right of X : x\nA --> B : go\nnote on link : on go");

        // Act
        var diagram = model.Lower(Context());

        // Assert
        Assert.Equal(["A", "A.Inner"], diagram.Clusters.Select(c => c.Id));
        Assert.Same(diagram.Clusters[0], diagram.Clusters[1].Parent);
        Assert.All(diagram.Clusters, c => Assert.IsType<StateCompositeImage>(c.Image));
        Assert.Equal([".start.", "A.Inner.X", "A.Inner.Y", "B"], diagram.Entities.Select(e => e.Id));
        Assert.Same(diagram.Clusters[1], diagram.Entities[1].Parent);
        Assert.Equal(["link#1", "link#2", "link#3"], diagram.Links.Select(l => l.Id));
        Assert.Equal("go", diagram.Links[2].Label);
        Assert.Equal(["x", "on go"], diagram.Notes.Select(n => n.Text));
        Assert.Same(diagram.Links[2], diagram.Notes[1].Link);
        Assert.Equal(SvekRankDir.LeftToRight, diagram.RankDir);
        diagram.Validate();
    }

    /// <summary>
    ///     Validates the lowering of concurrent regions: the composite state is a cluster with the arrangement of its
    ///     first separator and one cluster without an image per region, holding the region's elements.
    /// </summary>
    [Fact]
    public void PlantUmlStateModel_Lower_ConcurrentRegions_BecomeRegionClusters()
    {
        // Arrange
        var model = Parse("state P {\n[*] --> L\n||\n[*] --> R\n}");

        // Act
        var diagram = model.Lower(Context());

        // Assert
        var composite = diagram.Clusters[0];
        Assert.Equal(ClusterConcurrency.SideBySide, composite.Concurrency);
        Assert.Equal(["P", "P\"region0", "P\"region1"], diagram.Clusters.Select(c => c.Id));
        Assert.All(diagram.Clusters.Skip(1), c => Assert.Same(composite, c.Parent));
        Assert.All(diagram.Clusters.Skip(1), c => Assert.Null(c.Image));
        Assert.Same(diagram.Clusters[2], diagram.Entities.Single(e => e.Id == "P.CONC1.R").Parent);
        Assert.Equal("P\"region1", StateModel.RegionClusterId(ElementOf(model, "P").Regions[1]));
        diagram.Validate();
    }

    /// <summary>
    ///     Validates the layout: the scene draws every element with its role, the separators of the concurrent regions,
    ///     the page texts, the diagram's scale, and the same scene twice.
    /// </summary>
    [Fact]
    public void PlantUmlStateModel_Layout_DrawsTheScene()
    {
        // Arrange
        var model = Parse("scale 2\ntitle T\n[*] --> A\nstate A {\n[*] --> B\n--\n[*] --> C\n}\nA --> [*] : done");

        // Act
        var first = model.Layout(Context());
        var second = model.Layout(Context());

        // Assert
        Assert.Equal(2, first.Scale);
        Assert.Equal(3, first.Items.Count(i => i.Role == "state-start"));
        Assert.Equal(2, first.Items.Count(i => i.Role == "state-end"));
        Assert.Contains(first.Items, i => i.Role == "cluster-separator" && i.Id == "A");
        Assert.Contains(first.Items, i => i is SceneText { Role: "cluster-title", Text: "A" });
        Assert.Contains(first.Items, i => i is SceneText { Role: "link-label", Text: "done" });
        Assert.Contains(first.Items, i => i is SceneText { Role: "title", Text: "T" });
        Assert.Equal(first.ViewBox, second.ViewBox);
        Assert.Equal(first.Items.Count, second.Items.Count);
    }

    /// <summary>
    ///     Validates that a canceled layout stops with an <see cref="OperationCanceledException"/>.
    /// </summary>
    [Fact]
    public void PlantUmlStateModel_Lower_ObservesCancellation()
    {
        // Arrange
        var model = Parse("state A {\nstate B\n}");
        using var source = new CancellationTokenSource();
        source.Cancel();
        var context = new DiagramRenderContext(new PlantUmlRenderOptions(), new PlantUmlReportSink(10, false), new TextLayout(TextLayout.CreateFontResolver()), source.Token);

        // Act / Assert
        Assert.ThrowsAny<OperationCanceledException>(() => model.Lower(context));
    }

    /// <summary>
    ///     Validates that the ids the model makes up (of the notes without an alias and of the regions) never take the
    ///     id of a state or of a floating note, whatever names the diagram uses.
    /// </summary>
    [Fact]
    public void PlantUmlStateModel_Lower_GeneratedIds_NeverCollide()
    {
        // Arrange
        var model = Parse("state \"note#1\"\nstate \"A#region1\"\nnote \"x\" as \"note#2\"\nstate A {\nstate X\n--\nstate Y\n}\nnote right of A.X : y\nnote left of A.Y : z");

        // Act
        var diagram = model.Lower(Context());
        var scene = model.Layout(Context());

        // Assert
        var ids = diagram.Entities.Select(e => e.Id).Concat(diagram.Clusters.Select(c => c.Id)).Concat(diagram.Notes.Select(n => n.Id)).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(scene.Items, i => i.Role == "state" && i.Id == "A#region1");
    }
}
