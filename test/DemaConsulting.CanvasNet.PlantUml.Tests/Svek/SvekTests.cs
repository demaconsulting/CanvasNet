using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;
using DemaConsulting.CanvasNet.PlantUml.Svek;
using DemaConsulting.CanvasNet.PlantUml.Syntax;
using static DemaConsulting.CanvasNet.PlantUml.Tests.Svek.SvekTestHelpers;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Svek;

/// <summary>
///     Subsystem tests of the Svek subsystem: statements read by <c>SvekSyntax</c>, lowered to a <c>SvekDiagram</c>,
///     laid out by <c>SvekLayout</c> with the dagre layout of the Layout package, and drawn by <c>SvekRenderer</c> with
///     the shapes of <c>SvekGeometry</c>.
/// </summary>
public class SvekTests
{
    /// <summary>
    ///     Validates the whole engine on a small diagram text: the links with their decorations, styles, and labels,
    ///     the notes, and the page texts are read, laid out, and drawn, and drawing twice gives the same scene.
    /// </summary>
    [Fact]
    public void PlantUmlSvek_ReadLayoutRender_LinksNotesAndLabels()
    {
        // Arrange
        const string Body = "title Shapes\nA <|-- B\nB *-- \"1..*\" C : owns >\nA ..> D\nnote left of B : a note\nnote \"floating\" as N1\nD .. N1";

        // Act
        var (diagram, page) = Lower(Body);
        var first = SvekRenderer.Render(diagram, SvekLayout.Run(diagram, Context()));
        var second = SvekRenderer.Render(diagram, SvekLayout.Run(diagram, Context()));

        // Assert: the page text, the entities, the decorations, the dashed link, the labels, and the notes
        Assert.Equal("Shapes", page.Title!.Text);
        Assert.Equal(4, first.Items.Count(i => i.Role == "entity"));
        Assert.Contains(first.Items, i => i.Role == "link-tail" && i.Id == "l0");
        Assert.Contains(first.Items, i => i is ScenePath { Role: "link-tail", Fill: not null } && i.Id == "l1");
        Assert.Contains(first.Items, i => i is ScenePath { Role: "link", Stroke.Dash: not null } && i.Id == "l2");
        Assert.Contains(first.Items, i => i is SceneText { Role: "link-label", Text: "owns" });
        Assert.Contains(first.Items, i => i.Role == "link-label-arrow");
        Assert.Contains(first.Items, i => i is SceneText { Role: "link-head-label", Text: "1..*" });
        Assert.Equal(2, first.Items.OfType<SceneText>().Count(t => t.Role == "note-text"));
        Assert.DoesNotContain(first.Items, i => i.Role == "link" && i.Id == "l3");
        Assert.Equal(first.ViewBox, second.ViewBox);
        Assert.Equal(first.Items.Count, second.Items.Count);
    }

    /// <summary>
    ///     Validates clusters on their own and crossed by links, a cluster with concurrent regions, and a left to right
    ///     diagram: every entity is inside its clusters, no two entities overlap, and the clusters are drawn with their
    ///     titles.
    /// </summary>
    [Fact]
    public void PlantUmlSvek_ClustersAndRegions_DrawnWithoutOverlaps()
    {
        // Arrange
        var text = new TextLayout(TextLayout.CreateFontResolver());
        ISvekClusterImage Image(ClusterSymbol symbol, string title) => SvekRenderer.ClusterImage(symbol, title, title, [], SkinTheme.Default, text, null, default);
        var geo = new SvekCluster("geo") { Image = Image(ClusterSymbol.Folder, "geo") };
        var node = new SvekCluster("srv") { Image = Image(ClusterSymbol.Node, "srv") };
        var composite = new SvekCluster("S") { Image = Image(ClusterSymbol.Rectangle, "S"), Concurrency = ClusterConcurrency.SideBySide };
        var (r1, r2) = (new SvekCluster("S.1") { Parent = composite }, new SvekCluster("S.2") { Parent = composite });
        var entities = new[]
        {
            Entity("A", parent: geo), Entity("B", parent: geo), Entity("C", parent: node), Entity("D"),
            Entity("E", parent: r1), Entity("F", parent: r2), Entity("G", parent: r2),
        };
        SvekEntity E(string id) => entities.Single(e => e.Id == id);
        var diagram = new SvekDiagram
        {
            Entities = entities,
            Clusters = [geo, node, composite, r1, r2],
            Links =
            [
                new SvekLink("ab", E("A"), E("B")), new SvekLink("bd", E("B"), E("D")), new SvekLink("dc", E("D"), E("C")),
                new SvekLink("fg", E("F"), E("G")), new SvekLink("ds", E("D"), composite), new SvekLink("gn", E("G"), node),
            ],
            RankDir = SvekRankDir.LeftToRight,
        };

        // Act
        var placement = SvekLayout.Run(diagram, Context());
        var scene = SvekRenderer.Render(diagram, placement);

        // Assert: membership, no overlaps, and the drawing
        var clusters = placement.Clusters.ToDictionary(c => c.Cluster.Id, c => c.Box);
        foreach (var entity in placement.Entities)
        {
            for (var parent = entity.Entity.Parent; parent is not null; parent = parent.Parent)
            {
                Assert.True(Contains(clusters[parent.Id], entity.Box));
            }

            Assert.All(placement.Entities.Where(o => o != entity), o => Assert.False(Overlap(o.Box, entity.Box)));
        }

        Assert.False(Overlap(clusters["geo"], clusters["srv"]));
        Assert.Single(placement.Clusters.Single(c => c.Cluster.Id == "S").Separators);
        Assert.Equal(["geo", "srv", "S"], scene.Items.OfType<SceneText>().Where(t => t.Role == "cluster-title").Select(t => t.Text));
        Assert.True(double.IsFinite(scene.ViewBox.Width) && double.IsFinite(scene.ViewBox.Height));
    }

    /// <summary>
    ///     Validates that the layout of a lowered diagram is bounded: past a small work budget it fails with a located
    ///     limit error naming the limit, and a canceled call stops it.
    /// </summary>
    [Fact]
    public void PlantUmlSvek_LayoutLimits_FailClosedAndCancel()
    {
        // Arrange
        var (diagram, _) = Lower("A --> B\nB --> C\nC --> D\nD --> A\nA --> C : x");
        var limited = Context(new PlantUmlRenderOptions { Limits = new PlantUmlLimits { MaxLayoutWork = 100 } });
        using var source = new CancellationTokenSource();
        source.Cancel();
        var canceled = Context() with { CancellationToken = source.Token };

        // Act
        var ex = Assert.Throws<PlantUmlRenderException>(() => SvekLayout.Run(diagram, limited));

        // Assert
        Assert.Equal((PlantUmlDiagnosticCodes.LimitExceeded, 2), (ex.Code, ex.Location.Line));
        Assert.Contains("PlantUmlLimits.MaxLayoutWork", ex.Message, StringComparison.Ordinal);
        Assert.ThrowsAny<OperationCanceledException>(() => SvekLayout.Run(diagram, canceled));
    }

    /// <summary>
    ///     Lowers a diagram text to a Svek diagram as a diagram subsystem does: every link end is an entity with a fake
    ///     image, notes attach to their targets or float, and the page texts are kept.
    /// </summary>
    /// <param name="body">The statements.</param>
    /// <returns>The diagram and its page texts.</returns>
    private static (SvekDiagram Diagram, PageTexts Page) Lower(string body)
    {
        var statements = Statements(body);
        var entities = new Dictionary<string, SvekEntity>(StringComparer.Ordinal);
        var notes = new List<SvekNote>();
        var links = new List<SvekLink>();
        var page = PageTexts.None;
        SvekNode Node(string name)
        {
            if (notes.FirstOrDefault(n => n.Id == name) is { } found)
            {
                return found;
            }

            if (!entities.TryGetValue(name, out var entity))
            {
                entity = Entity(name);
                entities[name] = entity;
            }

            return entity;
        }

        for (var i = 0; i < statements.Count; i++)
        {
            if (SvekSyntax.TryReadPageText(statements, ref i, ref page, PlantUmlLimits.Default))
            {
                continue;
            }

            if (SvekSyntax.TryReadNote(statements, ref i, out var note))
            {
                var target = note.Target is { } t ? (SvekEntity)Node(t.Text) : null;
                notes.Add(new SvekNote(note.Alias ?? "note" + notes.Count, note.Text) { Target = target, Position = note.Position ?? NotePosition.Right });
                continue;
            }

            Assert.True(SvekSyntax.TryReadLink(statements[i], out var link));
            var (from, to) = (Node(link.From.Text), Node(link.To.Text));
            links.Add(new SvekLink("l" + links.Count, from, to)
            {
                TailDecor = link.Arrow.TailDecor,
                HeadDecor = link.Arrow.HeadDecor,
                Length = link.Arrow.Length,
                Direction = link.Arrow.Direction,
                Style = link.Arrow.ResolveStyle(doubledIsBold: false),
                Label = link.Label,
                LabelArrow = link.LabelArrow,
                TailLabel = link.TailLabel,
                HeadLabel = link.HeadLabel,
                Location = link.Location,
            });
        }

        var diagram = new SvekDiagram { Entities = [.. entities.Values], Notes = notes, Links = links, Location = statements[0].Location };
        return (diagram, page);
    }
}
