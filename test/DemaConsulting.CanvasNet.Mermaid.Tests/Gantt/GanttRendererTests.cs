using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Gantt;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Gantt;

/// <summary>
///     Unit tests for <c>GanttRenderer</c> (with <c>GanttStyles</c>): the width and height, the rows and the compact rows,
///     the bars with their classes and paint, the milestone square, the vertical markers, the side of the texts, the
///     section titles, the excluded days, the today line, the axes with their ticks and the guards of the tick interval
///     and labels, and the title.
/// </summary>
public sealed class GanttRendererTests
{
    /// <summary>The clock of the tests.</summary>
    private static readonly MermaidRenderOptions WithNow = new() { Now = new DateTimeOffset(2024, 1, 3, 12, 0, 0, TimeSpan.Zero) };

    /// <summary>
    ///     Validates the size: mermaid-cli's page width (784) or <c>gantt.useWidth</c>, and two top paddings plus a row of
    ///     the bar height and gap per task that is not a vertical marker.
    /// </summary>
    [Fact]
    public void GanttRenderer_Draw_Size_IsPageWidthAndRows()
    {
        // Act
        var scene = Layout("gantt\nA :a, 2024-01-01, 2d\nB :b, after a, 2d\nV :vert, 2024-01-02, 0d\n");
        var wide = Layout("---\nconfig:\n  gantt:\n    useWidth: 1000\n    barHeight: 30\n---\ngantt\nA :a, 2024-01-01, 2d\n");

        // Assert
        Assert.Equal(new SceneRect(0, 0, 784, 148), scene.ViewBox);
        Assert.Equal(new SceneRect(0, 0, 1000, 134), wide.ViewBox);
    }

    /// <summary>
    ///     Validates the bars: the time scale from the earliest start to the latest end over the width between the
    ///     paddings, rounded to whole pixels, and the rows in the order of the text.
    /// </summary>
    [Fact]
    public void GanttRenderer_Draw_Bars_FollowTheTimeScale()
    {
        // Act
        var scene = Layout("gantt\nA :a, 2024-01-01, 2d\nB :b, after a, 2d\n");

        // Assert
        var bars = Of(scene, "gantt-task").Cast<ScenePath>().Select(b => Scene.BoundsOf([b])!.Value).ToList();
        Assert.Equal(new SceneRect(75, 50, 317, 20), bars[0]);
        Assert.Equal(new SceneRect(392, 74, 317, 20), bars[1]);
    }

    /// <summary>
    ///     Validates the bar classes and their paint through the cascade: the section's style number, a done bar, a
    ///     critical active bar, and a clickable bar whose text is bold in the clickable color.
    /// </summary>
    [Fact]
    public void GanttRenderer_Draw_TaskStates_PaintFromTheStyleSheet()
    {
        // Act
        var scene = Layout("gantt\nA :a, 2024-01-01, 2d\nB :done, b, after a, 2d\nC :active, crit, c, after b, 2d\nclick a href \"https://example.com\"\n");

        // Assert
        var bars = Of(scene, "gantt-task").Cast<ScenePath>().ToList();
        Assert.Equal((Color("#8a90dd"), Color("#534fbc"), 2.0), (bars[0].Fill, bars[0].Stroke!.Color, bars[0].Stroke!.Width));
        Assert.Equal((Color("lightgrey"), Color("grey")), (bars[1].Fill, bars[1].Stroke!.Color));
        Assert.Equal((Color("#bfc7ff"), Color("#ff8888")), (bars[2].Fill, bars[2].Stroke!.Color));
        var clickable = Of(scene, "gantt-task-text").Cast<SceneText>().First();
        Assert.True(clickable.Runs[0].Font.Bold);
        Assert.Equal(Color("#003163"), clickable.Runs[0].Color);
    }

    /// <summary>
    ///     Validates <c>gantt.numberSectionStyles: 0</c> as Mermaid draws it: the section number is JavaScript's
    ///     <c>i % 0</c>, NaN, so the classes are <c>taskNaN</c>, <c>sectionNaN</c> and the like, which no rule of the style
    ///     sheet names, and the bars and rows take the fill <c>#333</c> that mermaid-cli computes for them (no failure).
    /// </summary>
    [Fact]
    public void GanttRenderer_Draw_NumberSectionStylesZero_PaintsNaNClassesAsMermaid()
    {
        // Act
        var (scene, warnings) = LayoutWithWarnings("---\nconfig:\n  gantt:\n    numberSectionStyles: 0\n---\ngantt\nsection One\nA :a, 2024-01-01, 2d\nsection Two\nB :done, b, after a, 2d\n");

        // Assert
        var bars = Of(scene, "gantt-task").Cast<ScenePath>().ToList();
        Assert.Equal([Color("#333"), Color("#333")], bars.Select(b => b.Fill));
        Assert.Equal(2, Of(scene, "gantt-section").Count);
        Assert.Empty(warnings);
    }

    /// <summary>
    ///     Validates a milestone: a square of the bar height at the middle of its dates, turned 45 degrees and scaled by
    ///     0.8 around its center, with its stroke scaled, and an italic text to its right.
    /// </summary>
    [Fact]
    public void GanttRenderer_Draw_Milestone_IsATurnedSquare()
    {
        // Act
        var scene = Layout("gantt\nA :a, 2024-01-01, 4d\nMilestone reached :milestone, m, 2024-01-03, 0d\n");

        // Assert
        var milestone = Of(scene, "gantt-task").Cast<ScenePath>().Single(p => p.Id == "m");
        var bounds = Scene.BoundsOf([milestone])!.Value;
        Assert.Equal(317 + 75, bounds.X + (bounds.Width / 2), 6);
        Assert.Equal(84, bounds.Y + (bounds.Height / 2), 6);
        Assert.True(bounds.Width < 20 * 0.8 * Math.Sqrt(2) && bounds.Width > 20 * 0.8);
        Assert.Equal(1.6, milestone.Stroke!.Width, 6);
        var text = Of(scene, "gantt-task-text").Cast<SceneText>().Single(t => t.Id == "m-text");
        Assert.True(text.Runs[0].Font.Italic);
        Assert.Equal(SceneTextAnchor.Start, text.Anchor);
    }

    /// <summary>
    ///     Validates a vertical marker: a thin bar from the grid's top over all rows and two bar heights, takes no row, and
    ///     its text below the rows in 15 pixels.
    /// </summary>
    [Fact]
    public void GanttRenderer_Draw_VerticalMarker_SpansTheRows()
    {
        // Act
        var scene = Layout("gantt\nA :a, 2024-01-01, 4d\nB :b, after a, 2d\nRelease :vert, v, 2024-01-03, 0d\n");

        // Assert
        var marker = Scene.BoundsOf([Of(scene, "gantt-task").Single(p => p.Id == "v")])!.Value;
        Assert.Equal(35.0, marker.Y, 3);
        Assert.Equal(1.6, marker.Width, 3);
        Assert.Equal((2 * 24) + 40.0, marker.Height, 3);
        var text = Of(scene, "gantt-task-text").Cast<SceneText>().Single(t => t.Id == "v-text");
        Assert.Equal(35 + 48 + 60, text.Baseline);
        Assert.Equal(15, text.Runs[0].Font.Size);
        Assert.Equal(2, Of(scene, "gantt-section").Count);
    }

    /// <summary>
    ///     Validates the side of the texts: inside a bar that is wide enough, right of a short bar, and left of a short bar
    ///     whose text would leave the chart.
    /// </summary>
    [Fact]
    public void GanttRenderer_Draw_TaskTexts_InsideRightOrLeft()
    {
        // Act
        var scene = Layout("gantt\nWide :w, 2024-01-01, 20d\nShort bar with a long name :s, 2024-01-02, 1d\nLate bar with a long name :z, 2024-01-20, 1d\n");

        // Assert
        var texts = Of(scene, "gantt-task-text").Cast<SceneText>().ToDictionary(t => t.Id!, t => t.Anchor);
        Assert.Equal(SceneTextAnchor.Middle, texts["w-text"]);
        Assert.Equal(SceneTextAnchor.Start, texts["s-text"]);
        Assert.Equal(SceneTextAnchor.End, texts["z-text"]);
    }

    /// <summary>
    ///     Validates the compact mode: overlapping tasks of a section share as few rows as the most overlapping ones need,
    ///     and the height follows the rows.
    /// </summary>
    [Fact]
    public void GanttRenderer_Draw_Compact_SharesRows()
    {
        // Act
        var scene = Layout("---\ndisplayMode: compact\n---\ngantt\nsection S\nA :a, 2024-01-01, 2d\nB :b, 2024-01-02, 2d\nC :c, after a, 1d\n");

        // Assert
        Assert.Equal(100 + (2 * 24), scene.ViewBox.Height);
        var tops = Of(scene, "gantt-task").Select(p => Scene.BoundsOf([p])!.Value.Y).ToList();
        Assert.Equal([50.0, 74.0, 50.0], tops);
    }

    /// <summary>
    ///     Validates the section titles: in the order of a JavaScript object's keys (integer names first), each at the
    ///     middle of its rows, and a title split at its <c>&lt;br&gt;</c> tags.
    /// </summary>
    [Fact]
    public void GanttRenderer_Draw_SectionTitles_KeyOrderAndBreaks()
    {
        // Act
        var scene = Layout("gantt\nsection b<br>c\nA :a, 2024-01-01, 1d\nsection 5\nB :b, 2024-01-01, 1d\n");

        // Assert
        var titles = Of(scene, "gantt-section-title").Cast<SceneText>().Select(t => string.Concat(t.Runs.Select(r => r.Text))).ToList();
        Assert.Equal(["5", "b", "c"], titles);
    }

    /// <summary>
    ///     Validates the excluded days: a shaded run per run of excluded days, and no shading with a warning when the tasks
    ///     span more than five years.
    /// </summary>
    [Fact]
    public void GanttRenderer_Draw_ExcludedDays_ShadedOrSkipped()
    {
        // Act
        var scene = Layout("gantt\ndateFormat YYYY-MM-DD\nexcludes weekends\nA :a, 2024-01-01, 10d\n");
        var (span, warnings) = LayoutWithWarnings("gantt\ndateFormat YYYY-MM-DD\nexcludes weekends\nA :a, 2024-01-01, 7y\n");

        // Assert
        Assert.Equal(["exclude-2024-01-06", "exclude-2024-01-13"], Of(scene, "gantt-exclude").Select(e => e.Id));
        Assert.Empty(Of(span, "gantt-exclude"));
        Assert.Contains(warnings, w => w.Code == MermaidDiagnosticCodes.GanttExcludesSkipped);
    }

    /// <summary>
    ///     Validates the today line: drawn at <see cref="MermaidRenderOptions.Now"/> with its style, not drawn with
    ///     <c>todayMarker off</c>, and not drawn without a clock (a style is then reported).
    /// </summary>
    [Fact]
    public void GanttRenderer_Draw_TodayLine_FollowsTheClock()
    {
        // Act
        var styled = Layout("gantt\ntodayMarker stroke:#00f,stroke-width:4px\nA :a, 2024-01-01, 4d\n");
        var off = Layout("gantt\ntodayMarker off\nA :a, 2024-01-01, 4d\n");
        var (noClock, warnings) = LayoutWithWarnings("gantt\ntodayMarker stroke:#00f\nA :a, 2024-01-01, 4d\n", new MermaidRenderOptions());

        // Assert
        var line = (ScenePath)Of(styled, "gantt-today").Single();
        Assert.Equal((396 + 75.0, 25.0, 100 + 24 - 25.0), (line.Segments[0].X, line.Segments[0].Y, line.Segments[1].Y));
        Assert.Equal((Color("#00f"), 4.0), (line.Stroke!.Color, line.Stroke.Width));
        Assert.Empty(Of(off, "gantt-today"));
        Assert.Empty(Of(noClock, "gantt-today"));
        Assert.Contains(warnings, w => w.Code == MermaidDiagnosticCodes.GanttTodayUnavailable);
    }

    /// <summary>
    ///     Validates the axes: the bottom axis at the bottom with ticks half a pixel past whole pixels, grid lines up to
    ///     the grid's top and labels in the axis format; a top axis with <c>gantt.topAxis</c>; the ticks of a custom
    ///     interval; and the default ticks with a warning when the interval would make more than 10,000.
    /// </summary>
    [Fact]
    public void GanttRenderer_Draw_Axes_TicksAndLabels()
    {
        // Act
        var scene = Layout("---\nconfig:\n  gantt:\n    topAxis: true\n---\ngantt\naxisFormat %d\ntickInterval 2day\nA :a, 2024-01-01, 8d\n");
        var (skipped, warnings) = LayoutWithWarnings("gantt\ntickInterval 1millisecond\nA :a, 2024-01-01, 8d\n");

        // Assert
        var axes = Of(scene, "gantt-axis").Cast<SceneGroup>().ToList();
        Assert.Equal(["bottom", "top"], axes.Select(a => a.Id));
        Assert.Equal((75.0, 124 - 50.0), (axes[0].OffsetX, axes[0].OffsetY));
        var labels = axes[0].Children.OfType<SceneText>().Select(t => string.Concat(t.Runs.Select(r => r.Text))).ToList();
        Assert.Equal(["01", "03", "05", "07", "09"], labels);
        var firstTick = (ScenePath)axes[0].Children.First(c => c.Role == "gantt-tick");
        Assert.Equal((0.5, 0.0, -(124 - 85.0)), (firstTick.Segments[0].X, firstTick.Segments[0].Y, firstTick.Segments[1].Y));
        Assert.Contains(warnings, w => w.Code == MermaidDiagnosticCodes.GanttTicksSkipped);
        Assert.NotEmpty(Of(skipped, "gantt-tick"));
    }

    /// <summary>
    ///     Validates the tick cap at its boundary, with Mermaid's estimate (<c>Math.ceil</c> of the span over the interval,
    ///     <c>getEstimatedTickCount</c>): a span of exactly 10,000 days at <c>1day</c> estimates 10,000 and draws the
    ///     custom ticks, both ends included (10,001, as mermaid-cli draws), while 10,001 days estimates 10,001, warns and
    ///     draws d3's default ticks.
    /// </summary>
    [Fact]
    public void GanttRenderer_Draw_TickCapBoundary_EstimatesAsMermaid()
    {
        // Act
        var (atCap, atCapWarnings) = LayoutWithWarnings("gantt\ndateFormat YYYY-MM-DD\ntickInterval 1day\nA :a, 2024-01-01, 10000d\n");
        var (overCap, overCapWarnings) = LayoutWithWarnings("gantt\ndateFormat YYYY-MM-DD\ntickInterval 1day\nA :a, 2024-01-01, 10001d\n");

        // Assert
        Assert.Equal(10_001, Of(atCap, "gantt-tick").Count);
        Assert.DoesNotContain(atCapWarnings, w => w.Code == MermaidDiagnosticCodes.GanttTicksSkipped);
        Assert.Equal(14, Of(overCap, "gantt-tick").Count);
        Assert.Contains(overCapWarnings, w => w.Code == MermaidDiagnosticCodes.GanttTicksSkipped);
    }

    /// <summary>
    ///     Validates the parse of a custom tick interval: a positive integer and a unit, nothing else.
    /// </summary>
    [Fact]
    public void GanttRenderer_ParseTickInterval_AcceptsMermaidsForm()
    {
        // Act and Assert
        Assert.Equal((3.0, "week", 604_800_000.0), GanttRenderer.ParseTickInterval("3week"));
        Assert.Equal((1.0, "month", 2_628_000_000.0), GanttRenderer.ParseTickInterval("1month"));
        Assert.Null(GanttRenderer.ParseTickInterval("0day"));
        Assert.Null(GanttRenderer.ParseTickInterval("1 day"));
        Assert.Null(GanttRenderer.ParseTickInterval("1year"));
        Assert.Null(GanttRenderer.ParseTickInterval(null));
    }

    /// <summary>
    ///     Validates the title: the title statement, or the frontmatter's title, centered at the title's top margin.
    /// </summary>
    [Fact]
    public void GanttRenderer_Draw_Title_FromTheTextOrTheFrontmatter()
    {
        // Act
        var own = Layout("gantt\ntitle Own title\nA :a, 2024-01-01, 1d\n");
        var front = Layout("---\ntitle: Front title\n---\ngantt\nA :a, 2024-01-01, 1d\n");

        // Assert
        var title = (SceneText)Of(own, "title").Single();
        Assert.Equal(("Own title", 392.0, 25.0, SceneTextAnchor.Middle), (string.Concat(title.Runs.Select(r => r.Text)), title.X, title.Baseline, title.Anchor));
        Assert.Equal("Front title", string.Concat(((SceneText)Of(front, "title").Single()).Runs.Select(r => r.Text)));
    }

    /// <summary>
    ///     Validates that a title statement that sanitizes to empty replaces the frontmatter title, so no title is drawn,
    ///     as in Mermaid.
    /// </summary>
    [Fact]
    public void GanttRenderer_Draw_TitleSanitizedToEmpty_ReplacesTheFrontmatterTitle()
    {
        // Act
        var scene = Layout("---\ntitle: FM\n---\ngantt\ntitle <script>x</script>\nA :a, 2024-01-01, 1d\n");

        // Assert
        Assert.Empty(Of(scene, "title"));
    }

    /// <summary>
    ///     Validates the failures of the renderer: a section named like an inherited JavaScript property in compact mode
    ///     fails as in Mermaid, and a tick label longer than <see cref="MermaidLimits.MaxLabelLength"/> is a limit.
    /// </summary>
    [Fact]
    public void GanttRenderer_Draw_Failures_AreLocated()
    {
        // Act
        var compact = Assert.Throws<MermaidRenderException>(() => Layout("---\ndisplayMode: compact\n---\ngantt\nsection toString\nA :a, 2024-01-01, 1d\n"));
        var label = Assert.Throws<MermaidRenderException>(() => Layout("gantt\naxisFormat %c%c%c%c\nA :a, 2024-01-01, 1d\n", new MermaidRenderOptions { Limits = new MermaidLimits { MaxLabelLength = 40 } }));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.RenderFailed, compact.Code);
        Assert.Equal(6, compact.Location.Line);
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, label.Code);
        Assert.Contains("tick label", label.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Lays a text out with the tests' clock or other options.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="options">The options, or null for the tests' clock.</param>
    /// <returns>The scene.</returns>
    private static Scene Layout(string text, MermaidRenderOptions? options = null) => SceneQuery.Layout(text, options ?? WithNow);

    /// <summary>
    ///     Lays a text out and collects the warnings.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="options">The options, or null for the tests' clock.</param>
    /// <returns>The scene and the warnings.</returns>
    private static (Scene Scene, List<MermaidDiagnostic> Warnings) LayoutWithWarnings(string text, MermaidRenderOptions? options = null)
    {
        var warnings = new List<MermaidDiagnostic>();
        var source = options ?? WithNow;
        var collecting = new MermaidRenderOptions { Now = source.Now, WarningHandler = warnings.Add };
        return (SceneQuery.Layout(text, collecting), warnings);
    }

    /// <summary>
    ///     Gets the items of a role.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="role">The role.</param>
    /// <returns>The items.</returns>
    private static List<SceneItem> Of(Scene scene, string role) => [.. scene.Walk().Select(e => e.Item).Where(i => i.Role == role)];

    /// <summary>
    ///     Parses a CSS color.
    /// </summary>
    /// <param name="text">The color.</param>
    /// <returns>The color.</returns>
    private static Rgba32 Color(string text) => CssColor.Parse(text);
}
