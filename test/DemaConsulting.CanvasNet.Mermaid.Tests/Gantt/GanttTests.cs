using DemaConsulting.CanvasNet.Mermaid.Gantt;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Gantt;

/// <summary>
///     Subsystem tests of the Gantt subsystem (<c>GanttParser</c>, <c>GanttModel</c>, <c>GanttRenderer</c> and
///     <c>TimeAxis</c> together): every gantt fixture parses to a model whose tasks the drawing shows, and a whole text
///     gives the model and the scene.
/// </summary>
public sealed class GanttTests
{
    /// <summary>The clock of the fixtures' reference renders.</summary>
    private static readonly MermaidRenderOptions WithNow = new() { Now = new DateTimeOffset(2024, 6, 12, 10, 30, 0, TimeSpan.Zero) };

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
    ///     Validates that a fixture parses to a gantt model and lays out with at most one bar per task (a bar without width
    ///     is not drawn), one text per task, background rows and ticks, the same way twice.
    /// </summary>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void CanvasNetMermaid_Gantt_Fixture_ParsesAndLaysOutConsistently(string name)
    {
        // Arrange
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "gantt", name + ".mmd"));
        var document = MermaidDocument.Parse(text);

        // Act
        var scene = SceneQuery.Layout(text, WithNow);
        var again = SceneQuery.Layout(text, WithNow);

        // Assert
        var model = Assert.IsType<GanttModel>(document.Diagram);
        var items = scene.Walk().Select(entry => entry.Item).ToList();
        Assert.InRange(items.Count(item => item.Role == "gantt-task"), 1, model.Tasks.Count);
        Assert.Equal(model.Tasks.Count, items.Count(item => item.Role == "gantt-task-text"));
        Assert.InRange(items.Count(item => item.Role == "gantt-section"), 1, model.Tasks.Count(task => !task.Vert));
        Assert.Contains(items, item => item.Role == "gantt-tick");
        Assert.Equal(scene.ViewBox, again.ViewBox);
        Assert.Equal(items.Count, again.Walk().Count());
    }

    /// <summary>
    ///     Validates the whole pipeline on a text: the model's metadata, the task dates the parser and model give, and the
    ///     bars, texts and section titles the renderer draws from them.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Gantt_Text_GivesTheModelAndTheScene()
    {
        // Arrange
        const string Text = "gantt\n  title Plan\n  dateFormat YYYY-MM-DD\n  section Build\n  Code :c, 2024-03-04, 5d\n  Test :t, after c, 2d\n  section Ship\n  Release :milestone, r, after t, 0d\n";

        // Act
        var document = MermaidDocument.Parse(Text);
        var scene = SceneQuery.Layout(Text, WithNow);

        // Assert
        Assert.Equal(("gantt", 3, 2, "Plan"), (document.DiagramType, document.NodeCount, document.SubgraphCount, document.Title));
        var texts = scene.Walk().Select(entry => entry.Item).OfType<SceneText>().ToList();
        Assert.Equal(["Code", "Test", "Release"], texts.Where(t => t.Role == "gantt-task-text").Select(t => string.Concat(t.Runs.Select(r => r.Text))));
        Assert.Equal(["Build", "Ship"], texts.Where(t => t.Role == "gantt-section-title").Select(t => string.Concat(t.Runs.Select(r => r.Text))));
        Assert.Equal(["c", "t", "r"], scene.Walk().Select(entry => entry.Item).Where(i => i.Role == "gantt-task").Select(i => i.Id));
    }

    /// <summary>
    ///     Validates that a whole text the grammar does not accept is refused when the document is opened, and that a task
    ///     date Mermaid cannot read fails the render, both located in the original text with a hint.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Gantt_InvalidText_FailsClosedWithLocation()
    {
        // Arrange
        var options = new MermaidOpenOptions { SourceName = "g.mmd" };
        var invalidDate = MermaidDocument.Parse("gantt\n  dateFormat YYYY-MM-DD\n  section A\n  Code :c, 2024-13-45, 2d\n", options);

        // Act
        var grammar = Assert.Throws<MermaidSyntaxException>(() =>
            MermaidDocument.Parse("---\ntitle: T\n---\ngantt\n  section A\n  Code\n", options));
        var render = Assert.Throws<MermaidRenderException>(() =>
            invalidDate.LayoutScene(WithNow, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal((MermaidDiagnosticCodes.SyntaxError, "g.mmd", 6), (grammar.Code, grammar.Location.File, grammar.Location.Line));
        Assert.Equal(["taskData"], grammar.Expected);
        Assert.NotNull(grammar.Hint);
        Assert.Equal((MermaidDiagnosticCodes.RenderFailed, 4), (render.Code, render.Location.Line));
        Assert.NotNull(render.Hint);
    }

    /// <summary>
    ///     Validates the clock: with <see cref="MermaidRenderOptions.Now"/> the today line is drawn at today; without it
    ///     no today line is drawn, and the reference to an unknown task (when the dates are compiled) and the
    ///     <c>todayMarker</c> style (when the line would be drawn) are each reported as
    ///     <c>mermaid-gantt-today-unavailable</c> at their lines, while the chart is still drawn.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Gantt_Clock_DrawsTheTodayLineOrReports()
    {
        // Arrange
        const string Text = "gantt\n  dateFormat YYYY-MM-DD\n  todayMarker stroke-width:3px\n  section A\n  Code :c, 2024-06-10, 5d\n  Late :l, after nobody, 1d\n";
        var (options, warnings, unsupported) = MermaidTestHelpers.Collecting();

        // Act
        var withNow = SceneQuery.Layout(Text, WithNow);
        var withoutNow = SceneQuery.Layout(Text, options);

        // Assert
        Assert.Single(withNow.Walk(), entry => entry.Item.Role == "gantt-today");
        Assert.DoesNotContain(withoutNow.Walk(), entry => entry.Item.Role == "gantt-today");
        Assert.Equal(
            [(MermaidDiagnosticCodes.GanttTodayUnavailable, 6), (MermaidDiagnosticCodes.GanttTodayUnavailable, 3)],
            warnings.Select(w => (w.Code, w.Location.Line)));
        Assert.Empty(unsupported);
        Assert.True(withoutNow.ViewBox.Width > 0 && withoutNow.ViewBox.Height > 0);
    }

    /// <summary>
    ///     Validates the reports of what Mermaid leaves out of a drawing: a tick interval that would make more than 10,000
    ///     ticks (the default ticks are drawn) and excluded days over more than five years (they are not shaded), each
    ///     reported once at its statement while the chart is drawn.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Gantt_SkippedTicksAndExcludes_AreReported()
    {
        // Arrange
        const string Text = "gantt\n  dateFormat YYYY-MM-DD\n  excludes weekends\n  tickInterval 1second\n  A :a, 2024-01-01, 7y\n";
        var warnings = new List<MermaidDiagnostic>();
        var options = new MermaidRenderOptions { Now = WithNow.Now, WarningHandler = warnings.Add };

        // Act
        var scene = SceneQuery.Layout(Text, options);

        // Assert
        Assert.Equal(
            [MermaidDiagnosticCodes.GanttExcludesSkipped, MermaidDiagnosticCodes.GanttTicksSkipped],
            warnings.Select(w => w.Code).Order(StringComparer.Ordinal));
        Assert.Contains(scene.Walk(), entry => entry.Item.Role == "gantt-tick");
        Assert.DoesNotContain(scene.Walk(), entry => entry.Item.Role == "gantt-exclude");
    }
}
