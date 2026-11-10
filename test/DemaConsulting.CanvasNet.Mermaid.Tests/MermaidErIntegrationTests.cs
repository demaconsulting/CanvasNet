using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Tests;

/// <summary>
///     System tests of entity relationship diagrams through the public API: every fixture renders strictly, the pixels
///     carry the theme's colors for the entities and their alternating rows, the cardinality markers are drawn (the zero
///     circles filled white, nothing for <c>md_parent</c>), the metadata is reported, the look and the layout are
///     reported, the scale behaves, failures are located, and rendering is deterministic and can be canceled.
/// </summary>
public class MermaidErIntegrationTests
{
    /// <summary>
    ///     A small diagram: a table with three attributes (an odd, an even and an odd row) above an entity without
    ///     attributes, joined by a relationship with a zero-or-one end.
    /// </summary>
    private const string Simple =
        "erDiagram\n    CUSTOMER {\n        string name PK\n        string email\n        int age\n    }\n" +
        "    CUSTOMER ||--o| PROFILE : has\n";

    /// <summary>
    ///     Gets the names of the ER fixtures.
    /// </summary>
    /// <returns>The names.</returns>
    public static TheoryData<string> Fixtures()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "er"), "*.mmd").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileNameWithoutExtension(file));
        }

        return data;
    }

    /// <summary>
    ///     Validates that every ER fixture renders in strict mode, draws something, gives no report, and measures as it
    ///     renders.
    /// </summary>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void CanvasNetMermaid_ErDiagram_Fixture_RendersStrictly(string name)
    {
        // Arrange
        var document = MermaidDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "er", name + ".mmd"));
        var (options, warnings, unsupported) = MermaidTestHelpers.Collecting(strict: true);

        // Act
        var size = document.Measure(options, TestContext.Current.CancellationToken);
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("er", document.DiagramType);
        Assert.Equal((size.Width, size.Height), (surface.Width, surface.Height));
        Assert.False(MermaidTestHelpers.IsFullyTransparent(surface));
        Assert.Empty(unsupported);
        Assert.Empty(warnings);
    }

    /// <summary>
    ///     Validates the metadata of an ER diagram: the drawn entities are the nodes (an entity named after a subgraph,
    ///     which a relationship to the subgraph adds, is not drawn), the relationships the edges and the subgraphs the
    ///     subgraphs; the direction is the last top-level direction statement; the frontmatter title is the title.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_ErDiagram_Metadata_CountsEntitiesRelationshipsAndSubgraphs()
    {
        // Arrange
        const string Text = "---\ntitle: Shop\n---\nerDiagram\n    accTitle: Orders\n    direction RL\n    subgraph S\n        A ||--o{ B : has\n    end\n" +
            "    C ||--|| S : in\n    direction LR\n";

        // Act
        var document = MermaidDocument.Parse(Text);

        // Assert: A, B and C; two relationships; one subgraph
        Assert.Equal(("er", true), (document.DiagramType, document.IsSupported));
        Assert.Equal((3, 2, 1), (document.NodeCount, document.EdgeCount, document.SubgraphCount));
        Assert.Equal("LR", document.Direction);
        Assert.Equal("Orders", document.AccTitle);
        Assert.Equal("Shop", document.Title);
    }

    /// <summary>
    ///     Validates the default theme's colors in the pixels: the table's name row in <c>mainBkg</c> <c>#ECECFF</c>, its
    ///     odd rows in <c>rowOdd</c> <c>#FFFFFF</c> and its even row in <c>rowEven</c> <c>#F1F1FF</c>, and the entity
    ///     without attributes in <c>#ECECFF</c>.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_ErDiagram_DefaultTheme_PaintsTheRowsInTurn()
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);
        var scene = SceneQuery.Layout(Simple);
        var rows = Rows(scene, "entity-CUSTOMER-0");
        var profile = SceneQuery.Nodes(scene)["entity-PROFILE-1"].Box;

        // Act
        using var surface = document.Render(new MermaidRenderOptions { Scale = 2 }, TestContext.Current.CancellationToken);

        // Assert: the name row, the three attribute rows, then the plain entity
        Assert.Equal(4, rows.Count);
        Assert.Equal(new Rgba32(0xEC, 0xEC, 0xFF, 255), Pixel(surface, scene, rows[0].X + 3, rows[0].Y + 3, 2));
        Assert.Equal(new Rgba32(0xFF, 0xFF, 0xFF, 255), Pixel(surface, scene, rows[1].X + 3, rows[1].Y + 3, 2));
        Assert.Equal(new Rgba32(0xF1, 0xF1, 0xFF, 255), Pixel(surface, scene, rows[2].X + 3, rows[2].Y + 3, 2));
        Assert.Equal(new Rgba32(0xFF, 0xFF, 0xFF, 255), Pixel(surface, scene, rows[3].X + 3, rows[3].Y + 3, 2));
        Assert.Equal(new Rgba32(0xEC, 0xEC, 0xFF, 255), Pixel(surface, scene, profile.X + 3, profile.Y + 3, 2));
    }

    /// <summary>
    ///     Validates the outline of a table in the pixels: its left edge is drawn in the node border <c>#9370DB</c>.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_ErDiagram_DefaultTheme_StrokesTheTableInTheNodeBorder()
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);
        var scene = SceneQuery.Layout(Simple);
        var table = SceneQuery.Nodes(scene)["entity-CUSTOMER-0"].Box;

        // Act
        using var surface = document.Render(new MermaidRenderOptions { Scale = 4 }, TestContext.Current.CancellationToken);

        // Assert: the pixel on the left edge, half way down
        var pixel = Pixel(surface, scene, table.X, table.Y + (table.Height / 2), 4);
        Assert.Equal(new Rgba32(0x93, 0x70, 0xDB, 255), pixel);
    }

    /// <summary>
    ///     Validates the zero-or-one end marker: its circle, 21 pixels before the end of the line, is filled white over
    ///     the line, and the marker is not filled around it (the transparent background shows).
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_ErDiagram_ZeroMarker_IsAWhiteCircle()
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);
        var scene = SceneQuery.Layout(Simple);
        var edge = Assert.Single(SceneQuery.Edges(scene)).Value;
        var end = edge.Segments[^1];

        // Act
        using var surface = document.Render(new MermaidRenderOptions { Scale = 4 }, TestContext.Current.CancellationToken);

        // Assert: the line ends straight down at the top of PROFILE
        Assert.Equal(new Rgba32(0xFF, 0xFF, 0xFF, 255), Pixel(surface, scene, end.X, end.Y - 21, 4));
        Assert.Equal(0, Pixel(surface, scene, end.X + 12, end.Y - 21, 4).A);
    }

    /// <summary>
    ///     Validates that the markers are drawn at the ends their cardinalities name, and that <c>md_parent</c> (the
    ///     <c>u</c> cardinality) draws none, as in Mermaid, which has no marker of that name.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_ErDiagram_Markers_FollowTheCardinalities()
    {
        // Arrange
        const string Both = "erDiagram\n    A ||--o{ B : r\n";
        const string Parent = "erDiagram\n    A u--o{ B : r\n";

        // Act
        var both = SceneQuery.OfRole(SceneQuery.Layout(Both), "marker").Count;
        var parent = SceneQuery.OfRole(SceneQuery.Layout(Parent), "marker").Count;

        // Assert: the only-one start draws one path; the zero-or-more end a circle and a path
        Assert.Equal(3, both);
        Assert.Equal(2, parent);
    }

    /// <summary>
    ///     Validates that the theme changes the entity fill (the theme's <c>mainBkg</c>).
    /// </summary>
    /// <param name="theme">The theme.</param>
    /// <param name="r">The expected red.</param>
    /// <param name="g">The expected green.</param>
    /// <param name="b">The expected blue.</param>
    [Theory]
    [InlineData("default", 0xEC, 0xEC, 0xFF)]
    [InlineData("dark", 0x1F, 0x20, 0x20)]
    [InlineData("forest", 0xCD, 0xE4, 0x98)]
    [InlineData("neutral", 0xEE, 0xEE, 0xEE)]
    public void CanvasNetMermaid_ErDiagram_Theme_ChangesTheEntityFill(string theme, byte r, byte g, byte b)
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);
        var options = new MermaidRenderOptions { Scale = 2, Theme = theme };
        var scene = SceneQuery.Layout(Simple, options);
        var profile = SceneQuery.Nodes(scene)["entity-PROFILE-1"].Box;
        var name = Rows(scene, "entity-CUSTOMER-0")[0];

        // Act
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new Rgba32(r, g, b, 255), Pixel(surface, scene, profile.X + 3, profile.Y + 3, 2));
        Assert.Equal(new Rgba32(r, g, b, 255), Pixel(surface, scene, name.X + 3, name.Y + 3, 2));
    }

    /// <summary>
    ///     Validates the styles of an entity as Mermaid applies them: a class's fill fills an entity without attributes,
    ///     and in a table it fills the name row and the even rows while the odd rows keep <c>rowOdd</c>.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_ErDiagram_StylesAndClasses_FillTheNameAndEvenRows()
    {
        // Arrange
        const string Text = "erDiagram\n    classDef hot fill:#ff0000\n    T {\n        int a\n        int b\n    }\n    P\n    T ||--|| P : r\n" +
            "    class T,P hot\n";
        var document = MermaidDocument.Parse(Text);
        var scene = SceneQuery.Layout(Text);
        var rows = Rows(scene, "entity-T-0");
        var plain = SceneQuery.Nodes(scene)["entity-P-1"].Box;

        // Act
        using var surface = document.Render(new MermaidRenderOptions { Scale = 2 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new Rgba32(0xFF, 0, 0, 255), Pixel(surface, scene, rows[0].X + 3, rows[0].Y + 3, 2));
        Assert.Equal(new Rgba32(0xFF, 0xFF, 0xFF, 255), Pixel(surface, scene, rows[1].X + 3, rows[1].Y + 3, 2));
        Assert.Equal(new Rgba32(0xFF, 0, 0, 255), Pixel(surface, scene, rows[2].X + 3, rows[2].Y + 3, 2));
        Assert.Equal(new Rgba32(0xFF, 0, 0, 255), Pixel(surface, scene, plain.X + 3, plain.Y + 3, 2));
    }

    /// <summary>
    ///     Validates that a generic type is drawn in TypeScript syntax and that an empty keys or comment column is dropped.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_ErDiagram_GenericTypes_AreDrawnWithAngleBrackets()
    {
        // Arrange
        const string Text = "erDiagram\n    R {\n        List~int~ ids\n    }\n";

        // Act
        var scene = SceneQuery.Layout(Text);
        var texts = scene.Walk().Select(entry => entry.Item).OfType<SceneText>().Select(text => string.Concat(text.Runs.Select(run => run.Text))).ToList();
        var dividers = SceneQuery.Parts(scene)["entity-R-0"].Count(part => part.Kind == "divider");

        // Assert: the dividers under the name (twice, each a fill and a stroke path) and after the type column only
        Assert.Contains("List<int>", texts);
        Assert.Equal(6, dividers);
    }

    /// <summary>
    ///     Validates that a look other than classic is reported once and drawn classic.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_ErDiagram_HandDrawnLook_IsReportedAndDrawnClassic()
    {
        // Arrange
        var document = MermaidDocument.Parse("---\nconfig:\n  look: handDrawn\n---\n" + Simple);
        var classic = MermaidDocument.Parse(Simple);
        var (options, _, unsupported) = MermaidTestHelpers.Collecting();

        // Act
        using var surface = document.Render(options, TestContext.Current.CancellationToken);
        using var expected = classic.Render(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LookUnsupported, Assert.Single(unsupported).Feature);
        Assert.True(MermaidTestHelpers.PixelsEqual(expected, surface));
    }

    /// <summary>
    ///     Validates that a layout other than dagre is reported once and laid out with dagre.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_ErDiagram_ElkLayout_IsReportedAndLaidOutWithDagre()
    {
        // Arrange
        var document = MermaidDocument.Parse("---\nconfig:\n  layout: elk\n---\n" + Simple);
        var dagre = MermaidDocument.Parse(Simple);
        var (options, _, unsupported) = MermaidTestHelpers.Collecting();

        // Act
        using var surface = document.Render(options, TestContext.Current.CancellationToken);
        using var expected = dagre.Render(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LayoutUnsupported, Assert.Single(unsupported).Feature);
        Assert.True(MermaidTestHelpers.PixelsEqual(expected, surface));
    }

    /// <summary>
    ///     Validates that the top-level <c>nodeSpacing</c> wins over the ER spacing, as in Mermaid's dagre layout.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_ErDiagram_TopLevelNodeSpacing_WinsOverTheErSpacing()
    {
        // Arrange
        const string Body = "erDiagram\n    R ||--o{ A : r\n    R ||--o{ B : r\n";
        static double Gap(string text)
        {
            var nodes = SceneQuery.Nodes(SceneQuery.Layout(text));
            return nodes["entity-B-2"].Box.X - nodes["entity-A-1"].Box.Right;
        }

        // Act
        var er = Gap(Body);
        var topLevel = Gap("---\nconfig:\n  nodeSpacing: 30\n  er:\n    nodeSpacing: 200\n---\n" + Body);

        // Assert
        Assert.Equal(140, er, 3);
        Assert.Equal(30, topLevel, 3);
    }

    /// <summary>
    ///     Validates the located render failures where Mermaid's render fails too: a relationship whose entity is named
    ///     like a subgraph declared after it (the entity is not drawn, so the line has no end), and a relationship from a
    ///     subgraph to itself inside it (the subgraph would be its own parent).
    /// </summary>
    /// <param name="text">The diagram.</param>
    /// <param name="line">The expected line.</param>
    /// <param name="message">A part of the expected message.</param>
    [Theory]
    [InlineData("erDiagram\n    A ||--|| B : r\n    subgraph A\n        C\n    end\n", 2, "is not drawn")]
    [InlineData("erDiagram\n    subgraph S\n        A ||--o{ S : r\n    end\n", 2, "cycle")]
    public void CanvasNetMermaid_ErDiagram_MermaidRenderFailures_AreLocated(string text, int line, string message)
    {
        // Arrange
        var document = MermaidDocument.Parse(text);

        // Act
        var exception = Assert.Throws<MermaidRenderException>(() => document.Measure(cancellationToken: TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.RenderFailed, exception.Code);
        Assert.Equal(line, exception.Location.Line);
        Assert.Contains(message, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the scale multiplies the natural size, which is the view box's.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_ErDiagram_Scale_MultipliesTheNaturalSize()
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);
        var viewBox = SceneQuery.Layout(Simple).ViewBox;

        // Act
        var natural = document.Measure(cancellationToken: TestContext.Current.CancellationToken);
        var doubled = document.Measure(new MermaidRenderOptions { Scale = 2 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(((int)Math.Ceiling(viewBox.Width), (int)Math.Ceiling(viewBox.Height)), (natural.Width, natural.Height));
        Assert.Equal((natural.Width * 2, natural.Height * 2), (doubled.Width, doubled.Height));
    }

    /// <summary>
    ///     Validates that repeated and concurrent renders of a document give identical pixels.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_ErDiagram_RepeatedAndConcurrentRenders_AreIdentical()
    {
        // Arrange
        var document = MermaidDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "er", "11-subgraphs.mmd"));
        using var first = document.Render(cancellationToken: TestContext.Current.CancellationToken);
        using var second = document.Render(cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var concurrent = Enumerable.Range(0, 6).AsParallel().Select(_ =>
        {
            using var surface = document.Render();
            return MermaidTestHelpers.PixelsEqual(first, surface);
        }).ToList();

        // Assert
        Assert.True(MermaidTestHelpers.PixelsEqual(first, second));
        Assert.All(concurrent, Assert.True);
    }

    /// <summary>
    ///     Validates that a canceled token stops an ER diagram render.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_ErDiagram_CanceledToken_Throws()
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);
        using var source = new CancellationTokenSource();
        source.Cancel();

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => document.Render(cancellationToken: source.Token));
    }

    /// <summary>
    ///     Validates that a layout that needs more work than allowed fails with the limit code instead of running on.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_ErDiagram_WorkLimit_FailsWithLimitExceeded()
    {
        // Arrange: many relationships and a small work limit
        var text = "erDiagram\n" + string.Concat(Enumerable.Range(0, 60).Select(i => $"    E{i} ||--o{{ E{i + 1} : r\n"));
        var document = MermaidDocument.Parse(text);
        var small = new MermaidRenderOptions { Limits = new MermaidLimits { MaxLayoutWork = 2_000 } };

        // Act
        var exception = Assert.Throws<MermaidRenderException>(() => document.Measure(small, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Contains("MaxLayoutWork", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Gets the rows of a table, top down: the name row (from the top of the outline to the first row), then the
    ///     attribute rows.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="id">The entity's id.</param>
    /// <returns>The rows.</returns>
    private static List<SceneRect> Rows(Scene scene, string id)
    {
        var paths = SceneQuery.Parts(scene)[id].Where(part => part.Kind == "path").Select(part => part.Box).ToList();
        var outline = paths[0];
        var rows = paths.Skip(1).ToList();
        return [new SceneRect(outline.X, outline.Y, outline.Width, rows[0].Y - outline.Y), .. rows];
    }

    /// <summary>
    ///     Reads the pixel under a point of the scene.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="scene">The scene (for its view box).</param>
    /// <param name="x">The scene x.</param>
    /// <param name="y">The scene y.</param>
    /// <param name="scale">The scale of the render.</param>
    /// <returns>The pixel.</returns>
    private static Rgba32 Pixel(Surface surface, Scene scene, double x, double y, double scale) =>
        surface.GetRowSpan((int)Math.Floor((y - scene.ViewBox.Y) * scale))[(int)Math.Floor((x - scene.ViewBox.X) * scale)];
}
