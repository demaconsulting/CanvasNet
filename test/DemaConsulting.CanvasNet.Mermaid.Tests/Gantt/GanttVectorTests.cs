using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Mermaid.Dagre;
using DemaConsulting.CanvasNet.Mermaid.Gantt;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Gantt;

/// <summary>
///     The upstream vectors of the gantt parser and model: every case of <c>gantt-parser.json</c>, recorded from the pinned
///     Mermaid (its jison parser and gantt database, in the UTC time zone with the clock at 2024-06-12T10:30:00Z) by
///     <c>gen-diagram-vectors.mjs --type gantt</c>, must give exactly Mermaid's database facts and task dates, or fail
///     where Mermaid fails.
/// </summary>
public sealed class GanttVectorTests
{
    /// <summary>The clock of the vector pages.</summary>
    internal static readonly DateTimeOffset VectorNow = new(2024, 6, 12, 10, 30, 0, TimeSpan.Zero);

    /// <summary>
    ///     The cases whose start date Mermaid reads with the browser's own date parser in a form outside the ECMAScript
    ///     format (V8's legacy forms, such as <c>2024-1-5</c> or <c>Jan 5 2024</c>), which the port does not read: they
    ///     fail with Mermaid's <c>Invalid date</c> message (a documented difference).
    /// </summary>
    private static readonly HashSet<string> LegacyDateCases = new(StringComparer.Ordinal)
    {
        "db-seconds-only", "db-year-typo", "iso-17", "iso-18", "iso-20", "iso-21", "iso-26", "format-23", "format-28",
        "format-29", "format-34",
    };

    /// <summary>
    ///     Gets the indexes of the vector cases.
    /// </summary>
    public static TheoryData<int> Cases => UpstreamVectors.Indexes(UpstreamVectors.Gantts);

    /// <summary>
    ///     Checks that the file holds enough cases: at least 200, with the upstream specs, valid texts, parse errors and
    ///     compile errors.
    /// </summary>
    [Fact]
    public void GanttParser_Vectors_FileHasEnoughCases_AtLeast200()
    {
        // Arrange
        var cases = UpstreamVectors.Gantts;

        // Act
        var errors = cases.Count(c => c!["expected"]!["error"] is not null);
        var compileErrors = cases.Count(c => c!["expected"]!["compileError"] is not null);
        var upstream = cases.Count(c => ((string)c!["spec"]!).Contains("diagrams/gantt/", StringComparison.Ordinal));

        // Assert
        Assert.True(cases.Count >= 200, $"only {cases.Count} cases");
        Assert.True(upstream >= 50, $"only {upstream} upstream cases");
        Assert.InRange(errors, 15, cases.Count - 150);
        Assert.InRange(compileErrors, 10, cases.Count - 150);
    }

    /// <summary>
    ///     Parses one vector case, compiles its task dates with the vectors' clock and compares the result with what
    ///     Mermaid recorded.
    /// </summary>
    /// <param name="index">The case index.</param>
    [Theory]
    [MemberData(nameof(Cases))]
    public void GanttParser_Parse_UpstreamVector_MatchesMermaid(int index)
    {
        // Arrange
        var vector = UpstreamVectors.Gantts[index]!.AsObject();
        var name = (string)vector["name"]!;
        var text = (string)vector["text"]!;
        var expected = vector["expected"]!.AsObject();
        var source = Preprocessor.Run(text, null, MermaidLimits.Default);

        // Act
        Exception? failure = null;
        GanttModel? model = null;
        try
        {
            model = GanttParser.Parse(source, DiagramDetector.Detect(source.Code, DetectorSettings.Default), MermaidLimits.Default);
        }
        catch (MermaidSyntaxException exception)
        {
            failure = exception;
        }

        // Assert: a text Mermaid does not detect, or the parse error
        if (expected["error"] is JsonObject error)
        {
            if (((string)error["message"]!).StartsWith("No diagram type detected", StringComparison.Ordinal))
            {
                Assert.Equal(MermaidDiagnosticCodes.UnknownDiagram, Assert.IsType<MermaidSyntaxException>(failure).Code);
                return;
            }

            AssertSameParseFailure(name, error, Assert.IsType<MermaidSyntaxException>(failure));
            return;
        }

        Assert.True(failure is null, $"{name}: {failure?.Message}");
        var statements = StatementFacts(model!);
        var difference = UpstreamVectors.Difference(WithoutTasks(expected), statements);
        Assert.True(difference is null, $"{name}: {difference}");

        // Assert: the task dates, or the failure
        AssertSameTasks(name, expected, model!);
    }

    /// <summary>
    ///     Compiles the task dates of a model with the vectors' clock.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns>The tasks.</returns>
    internal static List<GanttTask> Compile(GanttModel model) =>
        model.Compile(new GanttClock(VectorNow, null), new LayoutWorkMeter(MermaidLimits.Default.MaxLayoutWork, model.KeywordLocation, CancellationToken.None));

    /// <summary>
    ///     Asserts that the task dates are Mermaid's, or that the compilation fails where Mermaid fails or would draw
    ///     unresolved or invalid dates.
    /// </summary>
    /// <param name="name">The case name.</param>
    /// <param name="expected">Mermaid's facts.</param>
    /// <param name="model">The model.</param>
    private static void AssertSameTasks(string name, JsonObject expected, GanttModel model)
    {
        Exception? failure = null;
        List<GanttTask>? tasks = null;
        try
        {
            tasks = Compile(model);
        }
        catch (MermaidRenderException exception)
        {
            failure = exception;
        }

        if (expected["compileError"] is JsonValue compileError)
        {
            var exception = Assert.IsType<MermaidRenderException>(failure);
            Assert.Equal(MermaidDiagnosticCodes.RenderFailed, exception.Code);
            Assert.True(exception.Location.IsKnown, $"{name}: {exception.Message}");
            Assert.Contains(compileError.GetValue<string>(), exception.Message, StringComparison.Ordinal);
            return;
        }

        var expectedTasks = expected["tasks"]!.AsArray();
        if (LegacyDateCases.Contains(name))
        {
            // A documented difference: the port does not read V8's legacy date forms
            var exception = Assert.IsType<MermaidRenderException>(failure);
            Assert.Contains(": Invalid date:", exception.Message, StringComparison.Ordinal);
            return;
        }

        if (expectedTasks.Any(t => !t!["processed"]!.GetValue<bool>() || t["startTime"] is not JsonValue || t["endTime"] is not JsonValue))
        {
            // Mermaid would draw NaN geometry; the port fails at the task
            var exception = Assert.IsType<MermaidRenderException>(failure);
            Assert.Equal(MermaidDiagnosticCodes.RenderFailed, exception.Code);
            return;
        }

        Assert.True(failure is null, $"{name}: {failure?.Message}");
        var difference = UpstreamVectors.Difference(expectedTasks, TaskFacts(tasks!));
        Assert.True(difference is null, $"{name}: {difference}");
    }

    /// <summary>
    ///     Writes the statement facts of a model as the generator records them from Mermaid's gantt database (the display
    ///     mode and the top axis are never set by the text: the parser fails on <c>topAxis</c> and the frontmatter's
    ///     display mode goes to the configuration).
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns>The facts.</returns>
    private static JsonObject StatementFacts(GanttModel model) => new()
    {
        ["type"] = "gantt",
        ["title"] = model.DiagramTitle,
        ["accTitle"] = model.AccessibleTitle,
        ["accDescr"] = model.AccessibleDescription,
        ["dateFormat"] = model.DateFormat,
        ["axisFormat"] = model.AxisFormat,
        ["tickInterval"] = model.TickInterval,
        ["todayMarker"] = model.TodayMarker,
        ["includes"] = new JsonArray([.. model.Includes.Select(i => (JsonNode)i)]),
        ["excludes"] = new JsonArray([.. model.Excludes.Select(e => (JsonNode)e)]),
        ["weekday"] = model.Weekday,
        ["inclusiveEndDates"] = model.InclusiveEndDates,
        ["topAxis"] = false,
        ["displayMode"] = string.Empty,
        ["sections"] = new JsonArray([.. model.Sections.Select(s => (JsonNode)s)]),
        ["links"] = new JsonArray([.. model.Links.Select(l => (JsonNode)new JsonObject { ["id"] = l.Key, ["url"] = l.Value })]),
    };

    /// <summary>
    ///     Writes the facts of compiled tasks as the generator records them (an undefined value has no key, false tags
    ///     are undefined in Mermaid, and dates are times in milliseconds).
    /// </summary>
    /// <param name="tasks">The tasks.</param>
    /// <returns>The facts.</returns>
    private static JsonArray TaskFacts(List<GanttTask> tasks)
    {
        var result = new JsonArray();
        foreach (var task in tasks)
        {
            var raw = task.Raw;
            var facts = new JsonObject();
            if (raw.Id is not null)
            {
                facts["id"] = raw.Id;
            }

            facts["task"] = raw.Task;
            facts["section"] = raw.Section;
            facts["type"] = raw.Section;
            facts["processed"] = task.Processed;
            facts["manualEndTime"] = task.ManualEndTime;
            facts["classes"] = new JsonArray([.. raw.Classes.Select(c => (JsonNode)c)]);
            foreach (var (tag, set) in new[] { ("active", raw.Active), ("done", raw.Done), ("crit", raw.Crit), ("milestone", raw.Milestone), ("vert", raw.Vert) })
            {
                if (set)
                {
                    facts[tag] = true;
                }
            }

            facts["order"] = task.Order;
            facts["startTime"] = task.StartTime!.Value.Time;
            facts["endTime"] = task.EndTime!.Value.Time;
            facts["renderEndTime"] = task.RenderEndTime?.Time;
            result.Add(facts);
        }

        return result;
    }

    /// <summary>
    ///     Gets the facts without the tasks and the compile error.
    /// </summary>
    /// <param name="expected">The facts.</param>
    /// <returns>The statement facts.</returns>
    private static JsonObject WithoutTasks(JsonObject expected)
    {
        var copy = expected.DeepClone().AsObject();
        copy.Remove("tasks");
        copy.Remove("compileError");
        return copy;
    }

    /// <summary>
    ///     Asserts that a parse error is the error Mermaid reports: for a jison error, the same unexpected token and the
    ///     same expected tokens; for a JavaScript error of Mermaid's parser, a located syntax error.
    /// </summary>
    /// <param name="name">The case name.</param>
    /// <param name="error">Mermaid's error.</param>
    /// <param name="exception">The exception.</param>
    private static void AssertSameParseFailure(string name, JsonObject error, MermaidSyntaxException exception)
    {
        Assert.True(exception.Location.IsKnown, $"{name}: {exception.Message}");
        Assert.Equal(MermaidDiagnosticCodes.SyntaxError, exception.Code);
        if (error["token"] is null)
        {
            // A JavaScript error in Mermaid's parser: topAxis, or a task of tags only
            Assert.Empty(exception.Expected);
            Assert.Contains("Mermaid fails", exception.Message, StringComparison.Ordinal);
            return;
        }

        // The same unexpected token: jison names the end of the tokens '1'
        var token = (string)error["token"]!;
        var found = token == "1" ? "the end of the text" : $"'{token}'";
        Assert.True(exception.Message.Contains("found " + found, StringComparison.Ordinal), $"{name}: expected the token {token}, got: {exception.Message}");

        // And the same expected tokens, in jison's order
        var jison = error["expected"]!.AsArray().Select(e => (string)e!).ToList();
        Assert.Equal(jison, exception.Expected);
    }
}
