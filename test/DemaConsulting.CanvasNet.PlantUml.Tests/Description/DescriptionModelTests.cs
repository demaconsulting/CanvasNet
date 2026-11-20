using DemaConsulting.CanvasNet.PlantUml.Description;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Svek;
using static DemaConsulting.CanvasNet.PlantUml.Tests.Description.DescriptionTestHelpers;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Description;

/// <summary>
///     Unit tests for the <c>DescriptionModel</c> unit: the facts, the lowering for the Svek engine, and the layout.
/// </summary>
public class DescriptionModelTests
{
    /// <summary>
    ///     Validates the facts: leaf elements are nodes, containers subgraphs, and links that do not touch a note edges
    ///     (a hidden link counted).
    /// </summary>
    [Fact]
    public void PlantUmlDescriptionModel_Facts_CountLeavesLinksAndContainers()
    {
        // Act
        var model = Parse("node N {\n[A]\nfolder F {\n[B]\n}\n}\n[A] --> [B]\n[A] -[hidden]- I\nnote \"n\" as Note\nNote .. A\ntitle Facts");

        // Assert
        Assert.Equal((3, 2, 2), (model.NodeCount, model.EdgeCount, model.SubgraphCount));
        Assert.Equal("Facts", model.Title);
    }

    /// <summary>
    ///     Validates the lowering: containers become clusters under their parents with their images, leaf elements
    ///     entities in the cluster of their container, and the links and notes keep the text order; a link may end at a
    ///     container.
    /// </summary>
    [Fact]
    public void PlantUmlDescriptionModel_Lower_ContainersBecomeClusters()
    {
        // Arrange
        var model = Parse("left to right direction\npackage P {\ncomponent K {\n[A]\n}\n}\n[A] --> I : uses\nnote on link : on uses\n[B] --> K\nnote right of B : b");

        // Act
        var diagram = model.Lower(Context());

        // Assert
        Assert.Equal(["P", "P.K"], diagram.Clusters.Select(c => c.Id));
        Assert.Same(diagram.Clusters[0], diagram.Clusters[1].Parent);
        Assert.IsType<SvekClusterImage>(diagram.Clusters[0].Image);
        Assert.IsType<DescriptionContainerImage>(diagram.Clusters[1].Image);
        Assert.Equal(["P.K.A", "I", "B"], diagram.Entities.Select(e => e.Id));
        Assert.Same(diagram.Clusters[1], diagram.Entities[0].Parent);
        Assert.Same(diagram.Clusters[1], diagram.Links[1].To);
        Assert.Equal("uses", diagram.Links[0].Label);
        Assert.Equal(["on uses", "b"], diagram.Notes.Select(n => n.Text));
        Assert.Same(diagram.Links[0], diagram.Notes[0].Link);
        Assert.Equal(SvekRankDir.LeftToRight, diagram.RankDir);
        diagram.Validate();
    }

    /// <summary>
    ///     Validates that the ids the parser generates (of the notes without an alias and of the containers without a
    ///     name) never take the id of an element, even of one whose name in brackets holds a quote.
    /// </summary>
    [Fact]
    public void PlantUmlDescriptionModel_Lower_GeneratedIds_NeverCollide()
    {
        // Arrange
        var model = Parse("[note\"1]\n[B]\nnote left of B : x\nrectangle {\n[C]\n}\n[group\"1]\n(group 1)");

        // Act
        var diagram = model.Lower(Context());

        // Assert
        var ids = diagram.Entities.Select(e => e.Id).Concat(diagram.Clusters.Select(c => c.Id)).Concat(diagram.Notes.Select(n => n.Id)).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        diagram.Validate();
    }

    /// <summary>
    ///     Validates that the lowering stops at a canceled token.
    /// </summary>
    [Fact]
    public void PlantUmlDescriptionModel_Lower_ObservesCancellation()
    {
        // Arrange
        var model = Parse("node N {\n[A]\n}\n[A] --> [B]");
        using var source = new CancellationTokenSource();
        source.Cancel();
        var context = Context() with { CancellationToken = source.Token };

        // Act / Assert
        Assert.ThrowsAny<OperationCanceledException>(() => model.Lower(context));
    }

    /// <summary>
    ///     Validates the layout: the scene draws every element, container, note, and link with its role, the page texts
    ///     around them, over the theme's background, at the diagram's scale.
    /// </summary>
    [Fact]
    public void PlantUmlDescriptionModel_Layout_DrawsTheScene()
    {
        // Arrange
        var model = Parse("title Shop\nscale 2\nnode Server {\n[Web]\n}\n[Web] --> HTTP : serves\nnote left of Web : front");

        // Act
        var scene = model.Layout(Context());

        // Assert
        Assert.Contains(scene.Items, i => i.Role == "description" && i.Id == "Server.Web");
        Assert.Contains(scene.Items, i => i.Role == "description" && i.Id == "HTTP");
        Assert.Contains(scene.Items, i => i.Role == "cluster" && i.Id == "Server");
        Assert.Contains(scene.Items, i => i is SceneText { Role: "note-text", Text: "front" });
        Assert.Contains(scene.Items, i => i is SceneText { Role: "link-label", Text: "serves" });
        Assert.Contains(scene.Items, i => i is SceneText { Role: "title", Text: "Shop" });
        Assert.Equal(2, scene.Scale);
        Assert.Equal(model.Theme.Background, scene.Background);
    }
}
