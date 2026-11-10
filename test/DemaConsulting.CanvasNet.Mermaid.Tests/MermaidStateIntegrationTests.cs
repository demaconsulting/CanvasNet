using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Tests;

/// <summary>
///     System tests of state diagrams through the public API: every fixture renders strictly, the pixels carry the
///     theme's colors for the states, the start and end states and the composite states, the metadata is reported, the
///     legacy renderer, the look and the layout are reported, the scale behaves, failures are located, and rendering is
///     deterministic and can be canceled.
/// </summary>
public class MermaidStateIntegrationTests
{
    /// <summary>A small diagram: a start, a state with a transition label, a composite state nested two deep and an end.</summary>
    private const string Simple =
        "stateDiagram-v2\n    [*] --> Idle\n    Idle --> Busy : start\n    state Busy {\n        [*] --> Working\n" +
        "        state Working {\n            a --> b\n        }\n    }\n    Busy --> [*]\n";

    /// <summary>A diagram with concurrent regions.</summary>
    private const string Concurrent =
        "stateDiagram-v2\n    [*] --> Active\n    state Active {\n        x --> y\n        --\n        p --> q\n    }\n";

    /// <summary>
    ///     Gets the names of the state fixtures.
    /// </summary>
    /// <returns>The names.</returns>
    public static TheoryData<string> Fixtures()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "state"), "*.mmd").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileNameWithoutExtension(file));
        }

        return data;
    }

    /// <summary>
    ///     Validates that every state fixture renders in strict mode, draws something, gives no report, and measures as it
    ///     renders.
    /// </summary>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void CanvasNetMermaid_StateDiagram_Fixture_RendersStrictly(string name)
    {
        // Arrange
        var document = MermaidDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "state", name + ".mmd"));
        var (options, warnings, unsupported) = MermaidTestHelpers.Collecting(strict: true);

        // Act
        var size = document.Measure(options, TestContext.Current.CancellationToken);
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("state", document.DiagramType);
        Assert.Equal((size.Width, size.Height), (surface.Width, surface.Height));
        Assert.False(MermaidTestHelpers.IsFullyTransparent(surface));
        Assert.Empty(unsupported);
        Assert.Empty(warnings);
    }

    /// <summary>
    ///     Validates the metadata of a state diagram: the states (with the start, end, fork, join and choice states) are
    ///     the nodes, the transitions the edges (the links of notes are not counted) and the composite states the
    ///     subgraphs (their concurrent regions and the groups of notes are not counted); the direction is the one the
    ///     diagram is laid out in, the last top-level direction statement.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_StateDiagram_Metadata_CountsStatesTransitionsAndComposites()
    {
        // Arrange
        const string Text = "stateDiagram-v2\n    accTitle: Lamp\n    direction BT\n    [*] --> Off\n    state F <<fork>>\n    Off --> F\n" +
            "    state Comp {\n        a\n        --\n        b\n    }\n    F --> Comp\n    note left of Off : a note\n    Comp --> [*]\n" +
            "    direction LR\n";

        // Act
        var document = MermaidDocument.Parse(Text);

        // Assert: root_start, Off, F, a, b, root_end; four transitions; one composite state
        Assert.Equal(("state", true), (document.DiagramType, document.IsSupported));
        Assert.Equal((6, 4, 1), (document.NodeCount, document.EdgeCount, document.SubgraphCount));
        Assert.Equal("LR", document.Direction);
        Assert.Equal("Lamp", document.AccTitle);
        Assert.Null(document.Title);
    }

    /// <summary>
    ///     Validates the default theme's colors in the pixels: the state fill <c>#ECECFF</c>, the start state in the
    ///     special state color <c>#333333</c>, the end state's ring in the line color <c>#333333</c> around its inner
    ///     circle in the node border <c>#9370DB</c>, the composite state's title band in <c>#ECECFF</c> over its white
    ///     inner area, and the nested composite state's inner area in the alternate background <c>#F0F0F0</c>.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_StateDiagram_DefaultTheme_PaintsMermaidsColors()
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);
        var scene = SceneQuery.Layout(Simple);
        var nodes = SceneQuery.Nodes(scene);
        var idle = Find(nodes, "state-Idle-").Box;
        var start = Find(nodes, "state-root_start-");
        var end = Find(nodes, "state-root_end-");
        var busy = Find(SceneQuery.Clusters(scene), "state-Busy-");
        var busyInner = Find(SceneQuery.ClusterInners(scene), "state-Busy-");
        var workingInner = Find(SceneQuery.ClusterInners(scene), "state-Working-");

        // Act
        using var surface = document.Render(new MermaidRenderOptions { Scale = 4 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new Rgba32(0xEC, 0xEC, 0xFF, 255), Pixel(surface, scene, idle.X + 3, idle.Y + 3, 4));
        Assert.Equal(new Rgba32(0x33, 0x33, 0x33, 255), Pixel(surface, scene, start.X, start.Y, 4));
        Assert.Equal(new Rgba32(0x93, 0x70, 0xDB, 255), Pixel(surface, scene, end.X, end.Y, 4));
        Assert.Equal(new Rgba32(0x33, 0x33, 0x33, 255), Pixel(surface, scene, end.X + 7, end.Y, 4));
        Assert.Equal(new Rgba32(0xEC, 0xEC, 0xFF, 255), Pixel(surface, scene, busy.X + 3, busy.Y + 6, 4));
        Assert.Equal(new Rgba32(0xFF, 0xFF, 0xFF, 255), Pixel(surface, scene, busyInner.X + 3, busyInner.Y + 3, 4));
        Assert.Equal(new Rgba32(0xF0, 0xF0, 0xF0, 255), Pixel(surface, scene, workingInner.X + 3, workingInner.Y + 3, 4));
    }

    /// <summary>
    ///     Validates that a concurrent region is filled with the alternate background <c>#F0F0F0</c>.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_StateDiagram_ConcurrentRegion_IsFilledWithTheAlternateBackground()
    {
        // Arrange
        var document = MermaidDocument.Parse(Concurrent);
        var scene = SceneQuery.Layout(Concurrent);
        var region = Find(SceneQuery.Clusters(scene), "state-divider-id-1-");

        // Act
        using var surface = document.Render(new MermaidRenderOptions { Scale = 4 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new Rgba32(0xF0, 0xF0, 0xF0, 255), Pixel(surface, scene, region.X + 3, region.Y + 3, 4));
        Assert.Equal(2, SceneQuery.Clusters(scene).Keys.Count(id => id.StartsWith("state-divider-id-", StringComparison.Ordinal) || id.StartsWith("state-id-random-", StringComparison.Ordinal)));
    }

    /// <summary>
    ///     Validates that the theme changes the state fill (the theme's <c>stateBkg</c>, which is its <c>mainBkg</c>).
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
    public void CanvasNetMermaid_StateDiagram_Theme_ChangesTheStateFill(string theme, byte r, byte g, byte b)
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);
        var options = new MermaidRenderOptions { Scale = 2, Theme = theme };
        var scene = SceneQuery.Layout(Simple, options);
        var idle = Find(SceneQuery.Nodes(scene), "state-Idle-").Box;

        // Act
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new Rgba32(r, g, b, 255), Pixel(surface, scene, idle.X + 3, idle.Y + 3, 2));
    }

    /// <summary>
    ///     Validates that a state's <c>style</c> and style class fill it.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_StateDiagram_StylesAndClasses_FillTheState()
    {
        // Arrange
        const string Text = "stateDiagram-v2\n    classDef hot fill:#ff0000\n    A:::hot --> B\n    style B fill:#00ff00\n";
        var document = MermaidDocument.Parse(Text);
        var scene = SceneQuery.Layout(Text);
        var nodes = SceneQuery.Nodes(scene);
        var a = Find(nodes, "state-A-").Box;
        var b = Find(nodes, "state-B-").Box;

        // Act
        using var surface = document.Render(new MermaidRenderOptions { Scale = 2 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new Rgba32(0xFF, 0, 0, 255), Pixel(surface, scene, a.X + 3, a.Y + 3, 2));
        Assert.Equal(new Rgba32(0, 0xFF, 0, 255), Pixel(surface, scene, b.X + 3, b.Y + 3, 2));
    }

    /// <summary>
    ///     Validates that the legacy renderer (a <c>stateDiagram</c> with another <c>state.defaultRenderer</c>) is reported
    ///     once with the hint, drawn as the same diagram by the unified renderer, and refused in strict mode.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_StateDiagram_LegacyRenderer_IsReportedAndDrawnTheSame()
    {
        // Arrange
        var legacy = MermaidDocument.Parse("---\nconfig:\n  state:\n    defaultRenderer: dagre-d3\n---\n" + Simple.Replace("stateDiagram-v2", "stateDiagram", StringComparison.Ordinal));
        var current = MermaidDocument.Parse(Simple);
        var (options, _, unsupported) = MermaidTestHelpers.Collecting();
        var (strict, _, _) = MermaidTestHelpers.Collecting(strict: true);

        // Act
        using var surface = legacy.Render(options, TestContext.Current.CancellationToken);
        using var expected = current.Render(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(("state", true), (legacy.DiagramType, legacy.IsSupported));
        var report = Assert.Single(unsupported);
        Assert.Equal(MermaidDiagnosticCodes.LayoutUnsupported, report.Feature);
        Assert.Equal("state.defaultRenderer selects Mermaid's legacy state renderer; drawn with dagre-wrapper", report.Hint);
        Assert.Equal(3, report.Location.Line);
        Assert.True(MermaidTestHelpers.PixelsEqual(expected, surface));
        Assert.Throws<MermaidUnsupportedFeatureException>(() => legacy.Render(strict, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Validates that a look other than classic is reported once and drawn classic.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_StateDiagram_HandDrawnLook_IsReportedAndDrawnClassic()
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
    public void CanvasNetMermaid_StateDiagram_ElkLayout_IsReportedAndLaidOutWithDagre()
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
    ///     Validates that a nested note is sanitized when it is drawn, as Mermaid's shapes do: its markup is kept as
    ///     formatting and its script is dropped.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_StateDiagram_NestedNoteMarkup_IsSanitized()
    {
        // Arrange
        const string Text = "stateDiagram-v2\n    state Comp {\n        a --> b\n        note right of a : <b>kept</b><script>dropped</script>\n    }\n";

        // Act
        var runs = SceneQuery.Layout(Text).Walk().Select(entry => entry.Item).OfType<SceneText>().SelectMany(text => text.Runs).ToList();

        // Assert
        Assert.Contains(runs, run => run.Text.Contains("kept", StringComparison.Ordinal) && run.Font.Bold);
        Assert.DoesNotContain(runs, run => run.Text.Contains("dropped", StringComparison.Ordinal) || run.Text.Contains("script", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates the located render failures where Mermaid's render fails too: a note on a state that appears nowhere
    ///     before it (the state has no shape), and composite states inside each other.
    /// </summary>
    /// <param name="text">The diagram.</param>
    /// <param name="line">The expected line.</param>
    /// <param name="message">A part of the expected message.</param>
    [Theory]
    [InlineData("stateDiagram-v2\n    A\n    note left of Ghost : boo\n", 3, "No such shape")]
    [InlineData("stateDiagram-v2\n    state A {\n        B\n    }\n    state B {\n        A\n    }\n", 5, "cycle")]
    public void CanvasNetMermaid_StateDiagram_MermaidRenderFailures_AreLocated(string text, int line, string message)
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
    public void CanvasNetMermaid_StateDiagram_Scale_MultipliesTheNaturalSize()
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
    public void CanvasNetMermaid_StateDiagram_RepeatedAndConcurrentRenders_AreIdentical()
    {
        // Arrange
        var document = MermaidDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "state", "07-concurrency.mmd"));
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
    ///     Validates that a canceled token stops a state diagram render.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_StateDiagram_CanceledToken_Throws()
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
    public void CanvasNetMermaid_StateDiagram_WorkLimit_FailsWithLimitExceeded()
    {
        // Arrange: many transitions and a small work limit
        var text = "stateDiagram-v2\n" + string.Concat(Enumerable.Range(0, 60).Select(i => $"    S{i} --> S{i + 1}\n"));
        var document = MermaidDocument.Parse(text);
        var small = new MermaidRenderOptions { Limits = new MermaidLimits { MaxLayoutWork = 2_000 } };

        // Act
        var exception = Assert.Throws<MermaidRenderException>(() => document.Measure(small, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Contains("MaxLayoutWork", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Finds the entry whose DOM id starts with a prefix (the DOM ids end with Mermaid's counter).
    /// </summary>
    /// <typeparam name="T">The type of the entries.</typeparam>
    /// <param name="entries">The entries by DOM id.</param>
    /// <param name="prefix">The prefix.</param>
    /// <returns>The entry.</returns>
    private static T Find<T>(Dictionary<string, T> entries, string prefix) =>
        Assert.Single(entries, entry => entry.Key.StartsWith(prefix, StringComparison.Ordinal)).Value;

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
