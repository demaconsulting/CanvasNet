using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Positions;

namespace DemaConsulting.CanvasNet.Mermaid.Tests;

/// <summary>
///     Tests of the <see cref="MermaidRenderOptions.PositionHandler"/> contract of <see cref="MermaidDocument"/>: the
///     kinds, source ids and facts each diagram type reports, the mapping by scale and fit, the paint at filled box
///     centers, and that positions never change the pixels or come from <c>Measure</c>.
/// </summary>
public class MermaidDocumentPositionTests
{
    /// <summary>A flowchart with a subgraph, parallel edges, a self-loop and a filled node.</summary>
    private const string Flowchart =
        "---\ntitle: Order flow\n---\nflowchart LR\n    start([New order]) --> stock{In stock?}\n" +
        "    stock -- yes --> pack[Pack the order]\n    stock --> pack\n    stock --> stock\n" +
        "    subgraph ship [Shipping]\n        pack --> send((Send))\n    end\n" +
        "    classDef done fill:#cfc,stroke:#393\n    class send done\n";

    /// <summary>
    ///     Renders a diagram and collects its positions.
    /// </summary>
    /// <param name="text">The diagram text.</param>
    /// <param name="kindFilter">The kind filter, or null for every kind.</param>
    /// <returns>The surface and the positions.</returns>
    private static (Surface Surface, List<ImagePosition> Positions) Render(string text, Func<string, bool>? kindFilter = null)
    {
        var positions = new List<ImagePosition>();
        var options = new MermaidRenderOptions
        {
            PositionHandler = positions.Add,
            PositionKindFilter = kindFilter,
            Now = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };
        var surface = MermaidDocument.Parse(text).Render(options, TestContext.Current.CancellationToken);
        return (surface, positions);
    }

    /// <summary>
    ///     Validates the flowchart kinds and source ids, the edge ends and parallel index, the merged self-loop, and the
    ///     source lines.
    /// </summary>
    [Fact]
    public void MermaidDocument_Render_FlowchartPositions_ReportSourceIdsAndEdges()
    {
        // Act
        var (surface, positions) = Render(Flowchart);
        using var _ = surface;

        // Assert: the structural kinds first, with source ids rather than DOM ids
        Assert.Equal(
            ["title", "group", "node", "node", "node", "node", "edge", "edge", "edge", "edge", "edge"],
            positions.Select(position => position.Kind));
        Assert.Equal(["start", "stock", "pack", "send"], positions.Where(p => p.Kind == "node").Select(p => p.Id));
        Assert.Equal("ship", positions.Single(p => p.Kind == "group").Id);
        Assert.Equal("Order flow", positions[0].Text);

        // Assert: the two parallel edges are numbered and the self-loop is one position
        var parallel = positions.Where(p => p is { Kind: "edge", From: "stock", To: "pack" }).ToList();
        Assert.Equal([0, 1], parallel.Select(p => p.Index));
        Assert.Equal("yes", parallel[0].Text);
        Assert.Equal(6, parallel[0].Line);
        var loop = Assert.Single(positions, p => p is { Kind: "edge", From: "stock", To: "stock" });
        Assert.Equal(8, loop.Line);
    }

    /// <summary>
    ///     Validates that every position's box lies within the surface, and that a filled node is painted with its fill
    ///     on the vertical through its pixel, above its centered label.
    /// </summary>
    [Fact]
    public void MermaidDocument_Render_FilledNode_CenterHasTheFill()
    {
        // Act
        var (surface, positions) = Render(Flowchart);
        using var _ = surface;
        var send = positions.Single(p => p is { Kind: "node", Id: "send" });

        // Assert
        var pixel = send.Pixel!.Value;
        var box = send.Box!.Value;
        Assert.Equal(box.Center, pixel);
        Assert.Equal(new Rgba32(0xCC, 0xFF, 0xCC, 255), surface[(int)pixel.X, (int)(box.Y + (box.Height * 0.2))]);
        foreach (var position in positions)
        {
            var bounds = position.Box!.Value;
            Assert.InRange(bounds.X, 0, surface.Width);
            Assert.InRange(bounds.Y, 0, surface.Height);
            Assert.InRange(bounds.X + bounds.Width, 0, surface.Width);
            Assert.InRange(bounds.Y + bounds.Height, 0, surface.Height);
        }
    }

    /// <summary>
    ///     Validates that scale 2 doubles every coordinate and that the exact size maps by the fit.
    /// </summary>
    [Fact]
    public void MermaidDocument_Render_ScaleAndFit_MapPositions()
    {
        // Arrange
        var document = MermaidDocument.Parse(Flowchart);
        var natural = new List<ImagePosition>();
        var doubled = new List<ImagePosition>();
        var fitted = new List<ImagePosition>();
        var size = document.Measure(cancellationToken: TestContext.Current.CancellationToken);

        // Act: the natural size, twice the size, and the exact doubled size (which the fit maps the same way)
        using var one = document.Render(new MermaidRenderOptions { PositionHandler = natural.Add }, TestContext.Current.CancellationToken);
        using var two = document.Render(new MermaidRenderOptions { PositionHandler = doubled.Add, Scale = 2 }, TestContext.Current.CancellationToken);
        using var exact = document.Render(size.Width * 2, size.Height * 2, new MermaidRenderOptions { PositionHandler = fitted.Add }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(natural.Count, doubled.Count);
        for (var index = 0; index < natural.Count; index++)
        {
            Assert.Equal(natural[index].Pixel!.Value.X * 2, doubled[index].Pixel!.Value.X, 6);
            Assert.Equal(natural[index].Pixel!.Value.Y * 2, doubled[index].Pixel!.Value.Y, 6);
            Assert.Equal(natural[index].Box!.Value.Width * 2, doubled[index].Box!.Value.Width, 6);
            Assert.InRange(fitted[index].Pixel!.Value.X - doubled[index].Pixel!.Value.X, -0.5, 0.5);
            Assert.InRange(fitted[index].Pixel!.Value.Y - doubled[index].Pixel!.Value.Y, -0.5, 0.5);
        }
    }

    /// <summary>
    ///     Validates that the handler and the kind filter never change the pixels and that <c>Measure</c> never calls the
    ///     handler.
    /// </summary>
    [Fact]
    public void MermaidDocument_Render_WithHandler_PixelsUnchangedAndMeasureSilent()
    {
        // Arrange
        var document = MermaidDocument.Parse(Flowchart);
        var measured = new List<ImagePosition>();

        // Act
        using var plain = document.Render(cancellationToken: TestContext.Current.CancellationToken);
        var (withHandler, positions) = Render(Flowchart);
        using var _ = withHandler;
        var (filtered, nodes) = Render(Flowchart, kind => kind == "node");
        using var __ = filtered;
        document.Measure(new MermaidRenderOptions { PositionHandler = measured.Add }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(MermaidTestHelpers.PixelsEqual(plain, withHandler));
        Assert.True(MermaidTestHelpers.PixelsEqual(plain, filtered));
        Assert.NotEmpty(positions);
        Assert.All(nodes, position => Assert.Equal("node", position.Kind));
        Assert.Equal(4, nodes.Count);
        Assert.Empty(measured);
    }

    /// <summary>
    ///     Validates that an exception thrown by the handler propagates and the warnings are still delivered once.
    /// </summary>
    [Fact]
    public void MermaidDocument_Render_HandlerThrows_PropagatesAndDeliversWarningsOnce()
    {
        // Arrange: a flowchart with an unsupported look and an unsupported style gives one report of each kind
        const string Text = "---\nconfig:\n  look: handDrawn\n---\nflowchart LR\n  A --> B\n  style A text-decoration:underline\n";
        var warnings = new List<MermaidDiagnostic>();
        var unsupported = new List<MermaidUnsupportedFeature>();
        var options = new MermaidRenderOptions
        {
            WarningHandler = warnings.Add,
            UnsupportedFeatureHandler = unsupported.Add,
            PositionHandler = _ => throw new InvalidOperationException("stop"),
        };

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            MermaidDocument.Parse(Text).Render(options, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal("stop", exception.Message);
        Assert.Single(warnings);
        Assert.Single(unsupported);
    }

    /// <summary>
    ///     Validates the kinds and ids of a sequence diagram: participants in source order, messages numbered with their
    ///     ends and parallel index, and notes.
    /// </summary>
    [Fact]
    public void MermaidDocument_Render_SequencePositions_ParticipantsMessagesNotes()
    {
        // Arrange
        const string Text =
            "sequenceDiagram\n    participant A as Alice\n    participant B\n    A->>B: Hi\n    B-->>A: Hello\n" +
            "    A->>B: Again\n    Note over A,B: Done\n";

        // Act
        var (surface, positions) = Render(Text);
        using var _ = surface;

        // Assert
        Assert.Equal(["A", "B"], positions.Where(p => p.Kind == "participant").Select(p => p.Id));
        Assert.Equal("Alice", positions.First(p => p.Kind == "participant").Text);
        var messages = positions.Where(p => p.Kind == "message").ToList();
        Assert.Equal(["1", "2", "3"], messages.Select(p => p.Id));
        Assert.Equal([0, 0, 1], messages.Select(p => p.Index));
        Assert.Equal(("A", "B", "Hi"), (messages[0].From, messages[0].To, messages[0].Text));
        Assert.Equal("note1", Assert.Single(positions, p => p.Kind == "note").Id);

        // Assert: a message's pixel is on its horizontal line, between the participants
        var box = messages[0].Box!.Value;
        Assert.Equal(0, box.Height, 3);
        Assert.Equal(box.Y, messages[0].Pixel!.Value.Y, 3);
    }

    /// <summary>
    ///     Validates the gantt kinds: tasks and milestones by id (synthetic taskN without one) and sections merged over
    ///     their rows.
    /// </summary>
    [Fact]
    public void MermaidDocument_Render_GanttPositions_TasksMilestonesSections()
    {
        // Arrange
        const string Text =
            "gantt\n    dateFormat YYYY-MM-DD\n    section Build\n    Design :des, 2024-01-01, 3d\n    Code :2024-01-04, 2d\n" +
            "    Freeze :milestone, frz, 2024-01-06, 0d\n    section Test\n    Check :tst, 2024-01-06, 2d\n";

        // Act
        var (surface, positions) = Render(Text);
        using var _ = surface;

        // Assert
        Assert.Equal(["des", "task1", "tst"], positions.Where(p => p.Kind == "task").Select(p => p.Id));
        Assert.Equal("frz", Assert.Single(positions, p => p.Kind == "milestone").Id);
        var sections = positions.Where(p => p.Kind == "section").ToList();
        Assert.Equal(["Build", "Test"], sections.Select(p => p.Id));
        Assert.True(sections[0].Box!.Value.Height > sections[1].Box!.Value.Height);
        Assert.Equal("Design", positions.Single(p => p.Id == "des").Text);
    }

    /// <summary>
    ///     Validates the pie slices: the label as id, the value, and the pixel inside the slice's color.
    /// </summary>
    [Fact]
    public void MermaidDocument_Render_PiePositions_SlicesWithValuesAimInside()
    {
        // Arrange
        const string Text = "pie title Pets\n    \"Dogs\" : 386\n    \"Cats\" : 85\n";

        // Act
        var (surface, positions) = Render(Text);
        using var _ = surface;

        // Assert
        var slices = positions.Where(p => p.Kind == "slice").ToList();
        Assert.Equal(["Dogs", "Cats"], slices.Select(p => p.Id));
        Assert.True(slices[0].TryGetValue("value", out var value));
        Assert.Equal(PositionValue.FromNumber(386), value);
        var center = positions.Single(p => p.Kind == "title");
        Assert.Equal("Pets", center.Text);

        // Assert: each slice's pixel is painted, with a different color
        var dogs = surface[(int)slices[0].Pixel!.Value.X, (int)slices[0].Pixel!.Value.Y];
        var cats = surface[(int)slices[1].Pixel!.Value.X, (int)slices[1].Pixel!.Value.Y];
        Assert.True(dogs.A > 0 && cats.A > 0);
        Assert.NotEqual(dogs, cats);
    }

    /// <summary>
    ///     Validates the git graph kinds: commits at their bullet centers, branches and tags.
    /// </summary>
    [Fact]
    public void MermaidDocument_Render_GitGraphPositions_CommitsAtBullets()
    {
        // Arrange
        const string Text = "gitGraph\n    commit id: \"init\"\n    branch dev\n    commit id: \"fix-1\" tag: \"v1\"\n";

        // Act
        var (surface, positions) = Render(Text);
        using var _ = surface;

        // Assert
        var commits = positions.Where(p => p.Kind == "commit").ToList();
        Assert.Equal(["init", "fix-1"], commits.Select(p => p.Id));
        Assert.Equal(["main", "dev"], positions.Where(p => p.Kind == "branch").Select(p => p.Id));
        Assert.Equal("v1", Assert.Single(positions, p => p.Kind == "tag").Id);
        foreach (var commit in commits)
        {
            Assert.Equal(commit.Box!.Value.Center.X, commit.Pixel!.Value.X, 3);
            Assert.Equal(commit.Box.Value.Center.Y, commit.Pixel.Value.Y, 3);
            Assert.Equal(20, commit.Box.Value.Width, 3);
        }
    }

    /// <summary>
    ///     Validates the remaining types: class and state nodes and notes, entity names, mindmap source ids and packet
    ///     bit ranges.
    /// </summary>
    [Fact]
    public void MermaidDocument_Render_OtherTypes_ReportSourceIds()
    {
        // Act
        var (classSurface, classes) = Render("classDiagram\n    class Book\n    Book --> Shelf\n    note for Book \"Paper\"\n");
        var (stateSurface, states) = Render("stateDiagram-v2\n    [*] --> Idle\n    Idle --> Busy\n");
        var (erSurface, entities) = Render("erDiagram\n    MEMBER ||--o{ LOAN : borrows\n");
        var (mindmapSurface, mindmap) = Render("mindmap\n  root((Plan))\n    a[Build]\n");
        var (packetSurface, packet) = Render("packet-beta\n    0-7: \"Start\"\n    8-15: \"Data\"\n");
        using var _ = classSurface;
        using var __ = stateSurface;
        using var ___ = erSurface;
        using var ____ = mindmapSurface;
        using var _____ = packetSurface;

        // Assert
        Assert.Equal(["Book", "Shelf"], classes.Where(p => p.Kind == "node").Select(p => p.Id));
        Assert.Single(classes, p => p.Kind == "note");
        Assert.Contains(states, p => p is { Kind: "node", Id: "Idle" });
        Assert.Contains(states, p => p is { Kind: "edge", From: "Idle", To: "Busy" });
        Assert.Equal(["MEMBER", "LOAN"], entities.Where(p => p.Kind == "node").Select(p => p.Id));
        Assert.Contains(entities, p => p is { Kind: "edge", From: "MEMBER", To: "LOAN", Text: "borrows" });
        Assert.Equal(["root", "a"], mindmap.Where(p => p.Kind == "node").Select(p => p.Id));
        Assert.Equal(["0-7", "8-15"], packet.Where(p => p.Kind == "block").Select(p => p.Id));
        Assert.Equal("Data", packet.Single(p => p.Id == "8-15").Text);
    }
}
