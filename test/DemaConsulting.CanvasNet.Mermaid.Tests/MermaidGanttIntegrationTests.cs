using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Tests;

/// <summary>
///     System tests of gantt charts through the public API: every fixture renders strictly, the metadata is reported, the
///     pixels carry the theme's bar colors for plain, critical, done and active tasks, themes change them, the today line
///     follows <see cref="MermaidRenderOptions.Now"/> and is reported without it, the new codes are reported, failures are
///     located, and rendering is deterministic, concurrent and can be canceled.
/// </summary>
public class MermaidGanttIntegrationTests
{
    /// <summary>A small chart: four tasks in two sections and a title.</summary>
    private const string Simple =
        "gantt\n    title Plan\n    dateFormat YYYY-MM-DD\n    section One\n    Plain :p, 2024-01-01, 4d\n    Critical :crit, c, after p, 4d\n" +
        "    section Two\n    Done :done, d, 2024-01-01, 4d\n    Active :active, a, after d, 4d\n";

    /// <summary>The clock of the fixtures' reference renders.</summary>
    private static readonly DateTimeOffset ReferenceNow = new(2024, 6, 12, 10, 30, 0, TimeSpan.Zero);

    /// <summary>
    ///     Gets the names of the gantt fixtures.
    /// </summary>
    /// <returns>The names.</returns>
    public static TheoryData<string> Fixtures()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "gantt"), "*.mmd").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileNameWithoutExtension(file));
        }

        return data;
    }

    /// <summary>
    ///     Validates that every gantt fixture renders in strict mode with the reference clock, draws something, gives no
    ///     report, and measures as it renders.
    /// </summary>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void CanvasNetMermaid_GanttChart_Fixture_RendersStrictly(string name)
    {
        // Arrange
        var document = MermaidDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "gantt", name + ".mmd"));
        var warnings = new List<MermaidDiagnostic>();
        var unsupported = new List<MermaidUnsupportedFeature>();
        var options = new MermaidRenderOptions { Now = ReferenceNow, WarningHandler = warnings.Add, UnsupportedFeatureHandler = unsupported.Add, ThrowOnUnsupportedFeature = true };

        // Act
        var size = document.Measure(options, TestContext.Current.CancellationToken);
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(("gantt", true), (document.DiagramType, document.IsSupported));
        Assert.Equal((size.Width, size.Height), (surface.Width, surface.Height));
        Assert.False(MermaidTestHelpers.IsFullyTransparent(surface));
        Assert.Empty(unsupported);
        Assert.Empty(warnings);
    }

    /// <summary>
    ///     Validates the metadata of a gantt chart: the tasks (vertical markers included) are the nodes, the drawn
    ///     sections the subgraphs, there are no edges or direction, and the title and accessible title are reported.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_GanttChart_Metadata_CountsTasksAndSections()
    {
        // Arrange
        const string Text = "gantt\n  title  Plan \n  accTitle: Schedule\n  section A\n  T1 :1d\n  T2 :1d\n  section B\n  M :vert, 2024-01-01, 0d\n  section C\n  T3 :1d\n";

        // Act
        var document = MermaidDocument.Parse(Text);

        // Assert
        Assert.Equal((4, 0, 2), (document.NodeCount, document.EdgeCount, document.SubgraphCount));
        Assert.Null(document.Direction);
        Assert.Equal(("Plan", "Schedule"), (document.Title, document.AccTitle));
    }

    /// <summary>
    ///     Validates the default theme's colors in the pixels: a plain bar in <c>taskBkgColor</c>, a critical bar in
    ///     <c>critBkgColor</c>, a done bar in <c>doneTaskBkgColor</c> and an active bar in <c>activeTaskBkgColor</c>.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_GanttChart_DefaultTheme_PaintsTheTaskStates()
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);
        var scene = SceneQuery.Layout(Simple);
        var bars = Bars(scene);
        var options = new MermaidRenderOptions { Scale = 2, Background = new Rgba32(255, 255, 255, 255) };

        // Act
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert: the middle of each bar, away from the texts' rows
        Assert.Equal(CssColor.Parse("#8a90dd"), Pixel(surface, scene, bars["p"].X + 6, bars["p"].Y + 4, 2));
        Assert.Equal(CssColor.Parse("red"), Pixel(surface, scene, bars["c"].X + 6, bars["c"].Y + 4, 2));
        Assert.Equal(CssColor.Parse("lightgrey"), Pixel(surface, scene, bars["d"].X + 6, bars["d"].Y + 4, 2));
        Assert.Equal(CssColor.Parse("#bfc7ff"), Pixel(surface, scene, bars["a"].X + 6, bars["a"].Y + 4, 2));
    }

    /// <summary>
    ///     Validates that the theme changes the bar colors.
    /// </summary>
    /// <param name="theme">The theme.</param>
    /// <param name="color">The <c>taskBkgColor</c> of the theme.</param>
    [Theory]
    [InlineData("default", "#8a90dd")]
    [InlineData("forest", "#487e3a")]
    public void CanvasNetMermaid_GanttChart_Theme_ChangesTheBarColors(string theme, string color)
    {
        // Arrange
        var text = $"%%{{init: {{'theme': '{theme}'}}}}%%\n" + Simple;
        var document = MermaidDocument.Parse(text);
        var scene = SceneQuery.Layout(text);
        var bar = Bars(scene)["p"];

        // Act
        using var surface = document.Render(new MermaidRenderOptions { Scale = 2, Background = new Rgba32(255, 255, 255, 255) }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CssColor.Parse(color), Pixel(surface, scene, bar.X + 6, bar.Y + 4, 2));
    }

    /// <summary>
    ///     Validates the clock: with <see cref="MermaidRenderOptions.Now"/> the today line is drawn red at its time;
    ///     without it nothing is drawn there, and a <c>todayMarker</c> style and an unknown <c>after</c> id are reported
    ///     as <see cref="MermaidDiagnosticCodes.GanttTodayUnavailable"/>.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_GanttChart_Now_DrawsTheTodayLineOrReports()
    {
        // Arrange
        const string Text = "gantt\n  dateFormat YYYY-MM-DD\n  todayMarker stroke-width:6px\n  A :a, 2024-01-01, 10d\n  B :b, after nothing, 1d\n";
        var document = MermaidDocument.Parse(Text);
        var now = new MermaidRenderOptions { Now = new DateTimeOffset(2024, 1, 5, 0, 0, 0, TimeSpan.Zero), Scale = 2 };
        var warnings = new List<MermaidDiagnostic>();
        var without = new MermaidRenderOptions { Scale = 2, WarningHandler = warnings.Add };
        var scene = SceneQuery.Layout(Text, now);
        var line = (ScenePath)scene.Walk().Single(e => e.Item.Role == "gantt-today").Item;

        // Act
        using var drawn = document.Render(now, TestContext.Current.CancellationToken);
        using var notDrawn = document.Render(without, TestContext.Current.CancellationToken);

        // Assert
        var x = line.Segments[0].X;
        Assert.Equal(CssColor.Parse("red"), Pixel(drawn, scene, x, 30, 2));
        Assert.NotEqual(CssColor.Parse("red"), Pixel(notDrawn, scene, x, 30, 2));
        Assert.Equal(2, warnings.Count(w => w.Code == MermaidDiagnosticCodes.GanttTodayUnavailable));
        Assert.Equal(5, warnings.First(w => w.Code == MermaidDiagnosticCodes.GanttTodayUnavailable).Location.Line);
    }

    /// <summary>
    ///     Validates the reports of the gantt chart: a tick interval that would make too many ticks and excluded days over
    ///     more than five years.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_GanttChart_SkippedTicksAndExcludes_AreReported()
    {
        // Arrange
        var document = MermaidDocument.Parse("gantt\ndateFormat YYYY-MM-DD\nexcludes weekends\ntickInterval 1second\nA :a, 2024-01-01, 7y\n");
        var warnings = new List<MermaidDiagnostic>();
        var options = new MermaidRenderOptions { Now = ReferenceNow, WarningHandler = warnings.Add };

        // Act
        document.Measure(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(warnings, w => w.Code == MermaidDiagnosticCodes.GanttTicksSkipped);
        Assert.Contains(warnings, w => w.Code == MermaidDiagnosticCodes.GanttExcludesSkipped);
    }

    /// <summary>
    ///     Validates that a date that does not follow the date format fails at render, located at its task, with
    ///     Mermaid's message.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_GanttChart_InvalidDate_FailsLocated()
    {
        // Arrange
        var document = MermaidDocument.Parse("gantt\n  dateFormat YYYY-MM-DD\n  A :a, 2024-01-01, 2d\n  B :b, next week, 2d\n", new MermaidOpenOptions { SourceName = "plan.mmd" });

        // Act
        var exception = Assert.Throws<MermaidRenderException>(() => document.Measure(cancellationToken: TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.RenderFailed, exception.Code);
        Assert.Equal(("plan.mmd", 4, 3), (exception.Location.File, exception.Location.Line, exception.Location.Column));
        Assert.Contains("Invalid date:next week", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that repeated and concurrent renders of a document give identical pixels.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_GanttChart_RepeatedAndConcurrentRenders_AreIdentical()
    {
        // Arrange
        var document = MermaidDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "gantt", "06-excludes-includes.mmd"));
        var options = new MermaidRenderOptions { Now = ReferenceNow };
        using var first = document.Render(options, TestContext.Current.CancellationToken);
        using var second = document.Render(options, TestContext.Current.CancellationToken);

        // Act
        var concurrent = Enumerable.Range(0, 6).AsParallel().Select(_ =>
        {
            using var surface = document.Render(options);
            return MermaidTestHelpers.PixelsEqual(first, surface);
        }).ToList();

        // Assert
        Assert.True(MermaidTestHelpers.PixelsEqual(first, second));
        Assert.All(concurrent, Assert.True);
    }

    /// <summary>
    ///     Validates that a canceled token stops a gantt chart render.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_GanttChart_CanceledToken_Throws()
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);
        using var source = new CancellationTokenSource();
        source.Cancel();

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => document.Render(cancellationToken: source.Token));
    }

    /// <summary>
    ///     Gets the top-left corners of the bars by task id, in scene coordinates.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The corners.</returns>
    private static Dictionary<string, (double X, double Y)> Bars(Scene scene) =>
        scene.Walk()
            .Where(entry => entry.Item.Role == "gantt-task")
            .ToDictionary(entry => entry.Item.Id!, entry =>
            {
                var box = Scene.BoundsOf([entry.Item])!.Value;
                return (box.X + entry.OffsetX, box.Y + entry.OffsetY);
            });

    /// <summary>
    ///     Reads the pixel under a point of the scene.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="scene">The scene.</param>
    /// <param name="x">The x in scene units.</param>
    /// <param name="y">The y in scene units.</param>
    /// <param name="scale">The render scale.</param>
    /// <returns>The pixel.</returns>
    private static Rgba32 Pixel(Surface surface, Scene scene, double x, double y, double scale) =>
        surface.GetRowSpan((int)Math.Floor((y - scene.ViewBox.Y) * scale))[(int)Math.Floor((x - scene.ViewBox.X) * scale)];
}
