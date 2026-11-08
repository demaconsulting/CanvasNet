using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Hardening;

/// <summary>
///     The limit audit of the Vega-Lite renderer in executable form: one row for every input-controlled cost (rows, fields, bytes, files, expression length, depth, steps and strings, domains,
///     bins, ticks, views, composition depth, scene items, images, painted pixels, layout size, transform work), each with a hostile specification and the documented limit that bounds it.
///     A row passes when the cost is refused with the documented exception type, located at the construct that asked for it, with a message that names the limit, and without allocating
///     what the specification asked for (the cost is charged before the allocation). Every row also honors an already canceled token, and the transform and layout stages are canceled while they run.
/// </summary>
/// <remarks>
///     Costs that are bounded by an internal constant instead of a <see cref="VegaLiteLimits"/> property are in the same table with the text that the error carries (for example the highest
///     polynomial order of a regression). The inventory is written out in the design document (<c>docs/design/canvas-net-vega-lite.md</c>, Resource limits and hardening).
/// </remarks>
public class VegaLiteLimitAuditTests
{
    /// <summary>The most bytes a refused row may allocate on its thread: far less than what the hostile specification asks for.</summary>
    private const long RefusedAllocationBytes = 96L * 1024 * 1024;

    /// <summary>The stage of the pipeline at which a row is refused.</summary>
    public enum Stage
    {
        /// <summary>When the document is opened (<see cref="VegaLiteDocument.Parse"/>).</summary>
        Open,

        /// <summary>When the chart is laid out and painted at a fixed size (<see cref="VegaLiteDocument.Render(int, int, VegaLiteRenderOptions?, CancellationToken)"/>).</summary>
        Render,

        /// <summary>When the chart is rendered at its natural size (<see cref="VegaLiteDocument.Render(VegaLiteRenderOptions?, CancellationToken)"/>).</summary>
        Natural,
    }

    /// <summary>How a row ends.</summary>
    public enum Outcome
    {
        /// <summary>A <see cref="VegaLiteRenderException"/>.</summary>
        RenderError,

        /// <summary>A <see cref="VegaLiteSpecException"/>.</summary>
        SpecError,

        /// <summary>An <see cref="InvalidDataException"/> (the source is too long; no location exists).</summary>
        InvalidData,

        /// <summary>An <see cref="ArgumentOutOfRangeException"/> for the options (the size is not a size a surface can have).</summary>
        SizeLimit,

        /// <summary>No exception: the cost is bounded without a refusal (clamped or capped).</summary>
        Completes,
    }

    /// <summary>
    ///     One row of the audit.
    /// </summary>
    /// <param name="Name">The name of the cost, <c>area/cost</c>.</param>
    /// <param name="Spec">The text of the hostile specification.</param>
    /// <param name="Stage">The stage at which the cost is refused.</param>
    /// <param name="Outcome">How the row ends.</param>
    /// <param name="Limit">The text that the message must contain: the name of the limit.</param>
    /// <param name="Pointer">The JSON pointer that the location must start with (empty when the location is not checked).</param>
    /// <param name="MakeLimits">Creates the limits of the row; <see langword="null"/> for the defaults.</param>
    /// <param name="Resolver">The resolver of data files, or <see langword="null"/>.</param>
    /// <param name="Images">Whether the render options have an image resolver that makes a picture of every link.</param>
    internal sealed record Row(string Name, string Spec, Stage Stage, Outcome Outcome, string Limit, string Pointer, Func<VegaLiteLimits>? MakeLimits = null, Func<string, Stream?>? Resolver = null, bool Images = false);

    /// <summary>An image resolver that makes a transparent picture of the requested size for every link.</summary>
    private sealed class BlankImages : IImageResolver
    {
        /// <inheritdoc />
        public Surface? Resolve(string href, int pixelWidth, int pixelHeight) => new(pixelWidth, pixelHeight);
    }

    /// <summary>The rows, by name.</summary>
    private static readonly Dictionary<string, Row> Rows = BuildRows().ToDictionary(r => r.Name, StringComparer.Ordinal);

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Gets the names of the rows that are refused with an exception (for the theory).
    /// </summary>
    /// <returns>The names.</returns>
    public static TheoryData<string> RefusedRows() => [.. Rows.Values.Where(r => r.Outcome != Outcome.Completes).Select(r => r.Name)];

    /// <summary>
    ///     Gets the names of the rows that complete without a refusal (for the theory).
    /// </summary>
    /// <returns>The names.</returns>
    public static TheoryData<string> BoundedRows() => [.. Rows.Values.Where(r => r.Outcome == Outcome.Completes).Select(r => r.Name)];

    /// <summary>
    ///     Gets the names of the rows that fail while a chart is rendered (for the cancellation theory).
    /// </summary>
    /// <returns>The names.</returns>
    public static TheoryData<string> RenderedRows() => [.. Rows.Values.Where(r => r.Stage != Stage.Open).Select(r => r.Name)];

    /// <summary>
    ///     Validates that every input-controlled cost of the inventory is refused with the documented exception, located at the construct, naming its limit, and before the memory it asked for is allocated.
    /// </summary>
    /// <param name="name">The name of the row.</param>
    [Theory]
    [MemberData(nameof(RefusedRows))]
    public void CanvasNetVegaLite_SystemIntegration_LimitAudit_CostIsRefusedLocatedAndBeforeAllocation(string name)
    {
        // Arrange
        var row = Rows[name];

        // Act
        var before = GC.GetAllocatedBytesForCurrentThread();
        var error = Capture(row);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert: the documented exception type, naming the limit, located where the construct is
        Assert.NotNull(error);
        Assert.Contains(row.Limit, error.Message, StringComparison.Ordinal);
        var location = error switch
        {
            VegaLiteRenderException render => render.Location,
            VegaLiteSpecException spec => spec.Location,
            _ => default,
        };
        Assert.Equal(row.Outcome, OutcomeOf(error));
        if (row.Outcome is Outcome.RenderError or Outcome.SpecError)
        {
            Assert.True(location.IsKnown, $"{row.Name}: the exception has no location: {error.Message}");
            Assert.StartsWith(row.Pointer, location.Pointer, StringComparison.Ordinal);
        }

        // Assert: refused before the work and the allocation that the specification asked for
        Assert.True(allocated < RefusedAllocationBytes, $"{row.Name}: allocated {allocated:N0} bytes before the refusal");
    }

    /// <summary>
    ///     Validates the costs that are bounded without a refusal (a format width, a dash period, a document that is within its limits): the rendering completes within the bounds.
    /// </summary>
    /// <param name="name">The name of the row.</param>
    [Theory]
    [MemberData(nameof(BoundedRows))]
    public void CanvasNetVegaLite_SystemIntegration_LimitAudit_BoundedCostCompletes(string name)
    {
        // Arrange
        var row = Rows[name];
        var before = GC.GetAllocatedBytesForCurrentThread();

        // Act
        var error = Capture(row);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert
        Assert.True(error is null, $"{row.Name}: unexpected {error?.GetType().Name}: {error?.Message}");
        Assert.True(allocated < 4 * RefusedAllocationBytes, $"{row.Name}: allocated {allocated:N0} bytes");
    }

    /// <summary>
    ///     Validates that every stage of the table honors a token that is canceled before the call: the call throws the cancellation itself, never a wrapped or a located exception, and never a result.
    /// </summary>
    /// <param name="name">The name of the row.</param>
    [Theory]
    [MemberData(nameof(RenderedRows))]
    public void CanvasNetVegaLite_SystemIntegration_LimitAudit_CanceledTokenIsHonored(string name)
    {
        // Arrange
        var row = Rows[name];
        var document = Open(row);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        // Act / Assert
        var ex = Assert.ThrowsAny<OperationCanceledException>(() => Run(document, row, canceled.Token));
        Assert.IsNotType<AggregateException>(ex);
    }

    /// <summary>
    ///     Validates that the transform and layout stages poll the cancellation token while they run: each of the stages that the calibration of the work meter left to the token (the density estimate, a
    ///     sliding window, an aggregate of many groups, an impute of many combinations, a facet of many cells) runs for a long time when its budget is lifted and ends with the cancellation itself once the token is
    ///     canceled. The assertion is the outcome; the wall clock is only a generous hang guard (see <see cref="HangGuard"/>).
    /// </summary>
    /// <param name="stage">The name of the stage.</param>
    [Theory]
    [InlineData("density")]
    [InlineData("window")]
    [InlineData("aggregate")]
    [InlineData("impute")]
    [InlineData("facet-cells")]
    public void CanvasNetVegaLite_SystemIntegration_LimitAudit_CancellationIsHonoredWhileTheStageRuns(string stage)
    {
        // Arrange: budgets that are lifted, so that the stage runs until it is stopped
        var document = VegaLiteDocument.Parse(RunawaySpec(stage));
        var limits = new VegaLiteLimits { MaxEvaluationSteps = long.MaxValue, MaxDataRows = 50_000_000, MaxSceneItems = 50_000_000, MaxViews = 100_000 };
        using var source = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        source.CancelAfter(TimeSpan.FromMilliseconds(150));
        var started = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var ex = Assert.ThrowsAny<OperationCanceledException>(() => document.Render(200, 200, new VegaLiteRenderOptions { Limits = limits }, source.Token));

        // Assert: the cancellation itself (the outcome); the wall clock is only the hang guard, not an assertion about speed
        Assert.IsNotType<AggregateException>(ex);
        HangGuard.AssertNotHung(started, stage + " after the token was canceled");
    }

    /// <summary>
    ///     Validates that the number of reports is capped by <see cref="VegaLiteLimits.MaxDiagnostics"/>: a chart that uses dozens of unsupported features still renders, and the handler is called a bounded number of times.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_LimitAudit_ReportsAreCappedByTheDiagnosticsLimit()
    {
        // Arrange: forty layers that each use an unsupported mark (geoshape), with at most five reports
        var layers = string.Join(",", Enumerable.Repeat("{ \"mark\": \"geoshape\" }", 40));
        var document = VegaLiteDocument.Parse("{ \"data\": { \"values\": [ { \"a\": 1 } ] }, \"layer\": [" + layers + ", { \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": \"a\", \"type\": \"quantitative\" } } } ] }");
        var reports = new List<string>();
        var options = new VegaLiteRenderOptions
        {
            Limits = new VegaLiteLimits { MaxDiagnostics = 5 },
            WarningHandler = d => reports.Add(d.Message),
            UnsupportedFeatureHandler = f => reports.Add(f.Feature),
        };

        // Act
        using var surface = document.Render(100, 100, options, Ct);

        // Assert: five reports and at most one more that says that the others were left out
        Assert.InRange(reports.Count, 1, 6);
    }

    /// <summary>
    ///     Validates the regression of a defect that the audit found: the key values of an impute were counted against the row limit as a number, then converted to an int for the loop, so with a
    ///     row limit above the range of an int a count beyond it wrapped to a negative number on .NET 8 (no keys, silently) and saturated on .NET 9 and later (a run until the step budget). A count that no table could hold
    ///     is refused on every runtime, whatever the row limit is.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_LimitAudit_ImputeKeysBeyondTheIntRangeAreRefusedWhateverTheRowLimit()
    {
        // Arrange: three billion key values and a row limit that would allow them
        var document = VegaLiteDocument.Parse(Sequence("5", "{ \"impute\": \"data\", \"key\": \"data\", \"keyvals\": { \"start\": 0, \"stop\": 3e9 }, \"value\": 0 }"));
        var options = new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxDataRows = long.MaxValue } };

        // Act
        var error = Assert.Throws<VegaLiteRenderException>(() => document.Render(100, 100, options, Ct));

        // Assert
        Assert.Equal("/transform/0", error.Location.Pointer);
        Assert.Contains("(TableLayout.MaxCells)", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the regression of a defect that review found: with the row limit at its maximum, a hundred million key values (in the range of an int) were materialized before the cells of the result were bounded.
    ///     The key domain is bounded by the cells of a derived table before it is allocated, and the failure is located at the transform.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_LimitAudit_ImputeKeysBeyondTheCellLimitAreRefusedWhateverTheRowLimit()
    {
        // Arrange
        var document = VegaLiteDocument.Parse(Sequence("5", "{ \"impute\": \"data\", \"key\": \"data\", \"keyvals\": { \"start\": 0, \"stop\": 1e8 }, \"value\": 0 }"));
        var options = new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxDataRows = long.MaxValue } };

        // Act
        var error = Assert.Throws<VegaLiteRenderException>(() => document.Render(100, 100, options, Ct));

        // Assert
        Assert.Equal("/transform/0", error.Location.Pointer);
        Assert.Contains("(TableLayout.MaxCells)", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the regression of a defect that the fuzz suite found: a padding that no double can hold in a size (1.8e308) made the canvas infinite, and the scene refused it with an
    ///     <see cref="ArgumentOutOfRangeException"/> that has no location. The chart that has nothing to draw is checked like every other.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_LimitAudit_InfinitePaddingOfAChartWithoutDataIsLocated()
    {
        // Arrange: an unsupported mark gives a chart that has no data stage
        var document = VegaLiteDocument.Parse("{ \"mark\": \"geoshape\", \"padding\": 1.7976931348623157E+308, \"data\": { \"values\": [ { \"a\": 1 } ] } }");

        // Act
        var error = Assert.Throws<VegaLiteRenderException>(() => document.Measure(new VegaLiteRenderOptions(), Ct));

        // Assert
        Assert.True(error.Location.IsKnown);
        Assert.Contains("not a finite number", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the audit table covers the limits that the library documents: every <see cref="VegaLiteLimits"/> property is named by at least one row (a new limit without a row fails here).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_LimitAudit_EveryLimitPropertyHasARow()
    {
        // Arrange
        var properties = typeof(VegaLiteLimits).GetProperties().Where(p => p.Name.StartsWith("Max", StringComparison.Ordinal)).Select(p => p.Name).ToList();

        // Act: the rows that are refused name their limit in the message; the cost of the diagnostics is bounded without a refusal (a row that completes and a dedicated test)
        var uncovered = properties.Where(p => !Rows.Values.Any(r => r.Limit.Contains(p, StringComparison.Ordinal))).ToList();

        // Assert
        Assert.NotEmpty(properties);
        Assert.True(uncovered.Count == 0, "No audit row for: " + string.Join(", ", uncovered));
    }

    /// <summary>
    ///     Runs a row and captures what it throws.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns>The exception, or <see langword="null"/> when the row completed.</returns>
    private static Exception? Capture(Row row)
    {
        try
        {
            Run(Open(row), row, Ct);
            return null;
        }
        catch (Exception ex) when (ex is VegaLiteRenderException or VegaLiteSpecException or InvalidDataException or ArgumentOutOfRangeException)
        {
            return ex;
        }
    }

    /// <summary>
    ///     Maps an exception to the outcome that it stands for.
    /// </summary>
    /// <param name="ex">The exception.</param>
    /// <returns>The outcome.</returns>
    private static Outcome OutcomeOf(Exception ex) => ex switch
    {
        VegaLiteRenderException => Outcome.RenderError,
        VegaLiteSpecException => Outcome.SpecError,
        InvalidDataException => Outcome.InvalidData,
        _ => Outcome.SizeLimit,
    };

    /// <summary>
    ///     Opens the document of a row; for a row that is refused when the document is opened, the exception comes from here.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns>The document.</returns>
    private static VegaLiteDocument Open(Row row) =>
        VegaLiteDocument.Parse(row.Spec, new VegaLiteOpenOptions { Limits = row.MakeLimits?.Invoke(), FileResolver = row.Resolver });

    /// <summary>
    ///     Renders the document of a row at the stage of the row.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="row">The row.</param>
    /// <param name="token">The token.</param>
    private static void Run(VegaLiteDocument document, Row row, CancellationToken token)
    {
        var options = new VegaLiteRenderOptions { Limits = row.MakeLimits?.Invoke() ?? VegaLiteLimits.Default, ImageResolver = row.Images ? new BlankImages() : null };
        if (row.Stage == Stage.Natural)
        {
            document.Render(options, token).Dispose();
        }
        else
        {
            document.Render(120, 120, options, token).Dispose();
        }
    }

    /// <summary>
    ///     Builds a chart of a generated sequence with transforms.
    /// </summary>
    /// <param name="stop">The end of the sequence.</param>
    /// <param name="transforms">The transforms, as JSON text without the brackets.</param>
    /// <param name="mark">The mark.</param>
    /// <returns>The specification.</returns>
    private static string Sequence(string stop, string transforms, string mark = "point") =>
        "{ \"data\": { \"sequence\": { \"start\": 0, \"stop\": " + stop + " } }, \"transform\": [" + transforms + "], \"mark\": \"" + mark
        + "\", \"encoding\": { \"x\": { \"field\": \"data\", \"type\": \"quantitative\" }, \"y\": { \"field\": \"data\", \"type\": \"quantitative\" } } }";

    /// <summary>
    ///     Builds a chart of inline rows.
    /// </summary>
    /// <param name="rows">The rows, as JSON text without the brackets.</param>
    /// <param name="rest">More members of the specification, as JSON text, starting with a comma.</param>
    /// <returns>The specification.</returns>
    private static string Values(string rows, string rest = "") =>
        "{ \"data\": { \"values\": [" + rows + "] }, \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": \"a\", \"type\": \"quantitative\" }, \"y\": { \"field\": \"b\", \"type\": \"quantitative\" } }" + rest + " }";

    /// <summary>
    ///     Builds the specification of a stage that runs for many seconds when its budget is lifted.
    /// </summary>
    /// <param name="stage">The name of the stage.</param>
    /// <returns>The specification.</returns>
    private static string RunawaySpec(string stage) => stage switch
    {
        "density" => Sequence("20000", "{ \"density\": \"data\", \"steps\": 20000, \"extent\": [0, 20000] }").Replace("\"field\": \"data\"", "\"field\": \"value\"").Replace("\"field\": \"value\", \"type\": \"quantitative\" } }", "\"field\": \"density\", \"type\": \"quantitative\" } }"),
        "window" => Sequence("200000", "{ \"window\": [ { \"op\": \"median\", \"field\": \"data\", \"as\": \"m\" } ], \"frame\": [-3000, 3000] }"),
        "aggregate" => Sequence("1000000", "{ \"calculate\": \"datum.data % 50000\", \"as\": \"g\" }, { \"aggregate\": [ { \"op\": \"median\", \"field\": \"data\", \"as\": \"m\" }, { \"op\": \"ci0\", \"field\": \"data\", \"as\": \"c\" } ], \"groupby\": [\"g\"] }"),
        "impute" => Sequence("3000", "{ \"calculate\": \"datum.data % 1000\", \"as\": \"g\" }, { \"impute\": \"data\", \"key\": \"g\", \"groupby\": [\"data\"], \"keyvals\": { \"start\": 0, \"stop\": 3000 }, \"value\": 0 }"),
        _ => "{ \"data\": { \"sequence\": { \"start\": 0, \"stop\": 60000 } }, \"transform\": [ { \"calculate\": \"datum.data % 2000\", \"as\": \"g\" } ], \"facet\": { \"field\": \"g\", \"type\": \"nominal\" }, "
            + "\"spec\": { \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": \"data\", \"type\": \"quantitative\" }, \"y\": { \"field\": \"data\", \"type\": \"quantitative\" } } } }",
    };

    /// <summary>
    ///     Builds the rows of the audit.
    /// </summary>
    /// <returns>The rows.</returns>
    private static IEnumerable<Row> BuildRows()
    {
        var csv = (Func<string, Stream?>)(_ => new MemoryStream(Encoding.UTF8.GetBytes("a,b\n1,2\n3,4\n")));
        var nested = new string('[', 70) + new string(']', 70);
        var deepParens = new string('(', 20) + "1" + new string(')', 20);
        var many = string.Join(",", Enumerable.Range(0, 11).Select(i => "{ \"a\": " + i + ", \"b\": " + i + " }"));
        var wide = "{ \"a\": 1, \"b\": 2, \"c\": 3, \"d\": 4, \"e\": 5, \"f\": 6 }";
        var distinct = string.Join(",", Enumerable.Range(0, 20).Select(i => "{ \"a\": " + i + ", \"b\": 1, \"c\": \"k" + i + "\" }"));
        var imageRows = string.Join(",", Enumerable.Range(0, 10).Select(i => "{ \"a\": " + i + ", \"u\": \"p" + i + ".png\" }"));
        var layers = string.Join(",", Enumerable.Repeat("{ \"mark\": \"point\" }", 10));
        var concat = string.Join(",", Enumerable.Repeat("{ \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": \"a\", \"type\": \"quantitative\" } } }", 10));
        var nestedConcat = Enumerable.Range(0, 6).Aggregate("{ \"mark\": \"point\" }", (inner, _) => "{ \"hconcat\": [" + inner + "] }");
        var bigTitle = new string('t', 2000);
        var flatRows = string.Join(",", Enumerable.Range(0, 3).Select(i => "{ \"a\": " + i + ", \"b\": 1, \"l\": [1, 2, 3, 4, 5] }"));
        var foldRows = string.Join(",", Enumerable.Range(0, 5).Select(i => "{ \"a\": " + i + ", \"b\": " + i + ", \"c\": " + i + ", \"d\": " + i + " }"));
        var tightRows = new Func<VegaLiteLimits>(() => new VegaLiteLimits { MaxDataRows = 10 });

        // Open: the specification text
        yield return new Row("open/source-length", "{ \"mark\": \"point\", \"description\": \"" + new string('a', 200) + "\" }", Stage.Open, Outcome.InvalidData, "MaxSourceLength", string.Empty, () => new VegaLiteLimits { MaxSourceLength = 100 });
        yield return new Row("open/json-depth", "{ \"usermeta\": " + nested + ", \"mark\": \"point\" }", Stage.Open, Outcome.SpecError, "MaxJsonDepth", string.Empty);

        // Data: rows, fields, bytes, files, generated sequences
        yield return new Row("data/rows-inline", Values(many), Stage.Render, Outcome.RenderError, "MaxDataRows", "/data", tightRows);
        yield return new Row("data/rows-sequence", Sequence("1e9", string.Empty), Stage.Render, Outcome.RenderError, "MaxDataRows", "/data/sequence");
        yield return new Row("data/rows-sequence-step", "{ \"data\": { \"sequence\": { \"start\": 0, \"stop\": 1, \"step\": 1e-12 } }, \"mark\": \"point\" }", Stage.Render, Outcome.RenderError, "MaxDataRows", "/data/sequence");
        yield return new Row("data/rows-sequence-unrepresentable", "{ \"data\": { \"sequence\": { \"start\": 0, \"stop\": 1e300, \"step\": 1e-300 } }, \"mark\": \"point\" }", Stage.Render, Outcome.RenderError, "MaxDataRows", "/data/sequence");
        yield return new Row("data/rows-sequence-cells", "{ \"data\": { \"sequence\": { \"start\": 0, \"stop\": 1e8 } }, \"mark\": \"point\" }", Stage.Render, Outcome.RenderError, "TableLayout.MaxCells", "/data/sequence", () => new VegaLiteLimits { MaxDataRows = long.MaxValue });
        yield return new Row("data/fields", Values(wide), Stage.Render, Outcome.RenderError, "MaxFieldCount", "/data", () => new VegaLiteLimits { MaxFieldCount = 5 });
        yield return new Row("data/bytes", "{ \"data\": { \"url\": \"big.csv\" }, \"mark\": \"point\" }", Stage.Render, Outcome.RenderError, "MaxDataBytes", "/data", () => new VegaLiteLimits { MaxDataBytes = 8 }, csv);
        yield return new Row("data/files", "{ \"layer\": [ { \"data\": { \"url\": \"a.csv\" }, \"mark\": \"point\" }, { \"data\": { \"url\": \"b.csv\" }, \"mark\": \"point\" } ] }", Stage.Render, Outcome.RenderError, "MaxDataFiles", "/layer", () => new VegaLiteLimits { MaxDataFiles = 1 }, csv);

        // Transforms that multiply or generate rows
        yield return new Row("transform/fold", Values(foldRows, ", \"transform\": [ { \"fold\": [\"a\", \"b\", \"c\", \"d\"] } ]"), Stage.Render, Outcome.RenderError, "MaxDataRows", "/transform/0", tightRows);
        yield return new Row("transform/flatten", Values(flatRows, ", \"transform\": [ { \"flatten\": [\"l\"] } ]"), Stage.Render, Outcome.RenderError, "MaxDataRows", "/transform/0", tightRows);
        yield return new Row("transform/impute", Sequence("3000", "{ \"calculate\": \"datum.data % 1000\", \"as\": \"g\" }, { \"impute\": \"data\", \"key\": \"g\", \"groupby\": [\"data\"], \"keyvals\": { \"start\": 0, \"stop\": 3000 }, \"value\": 0 }"), Stage.Render, Outcome.RenderError, "MaxDataRows", "/transform/1");
        yield return new Row("transform/loess", Sequence("20000", "{ \"calculate\": \"sin(datum.data)\", \"as\": \"y\" }, { \"loess\": \"y\", \"on\": \"data\", \"bandwidth\": 1 }"), Stage.Render, Outcome.RenderError, "MaxEvaluationSteps", "/transform/1");
        yield return new Row("transform/density-steps", Sequence("10", "{ \"density\": \"data\", \"steps\": 1e9 }"), Stage.Render, Outcome.RenderError, "MaxDataRows", "/transform/0");
        yield return new Row("transform/quantile-step", Sequence("10", "{ \"quantile\": \"data\", \"step\": 1e-300 }"), Stage.Render, Outcome.RenderError, "MaxDataRows", "/transform/0");
        // A window frame is parsed as integers within the range of an int; the work of a frame that the budget accepts is stopped by the meter (MaxEvaluationSteps) and by cancellation (the stage test below)
        yield return new Row("transform/window-frame", Sequence("10", "{ \"window\": [ { \"op\": \"median\", \"field\": \"data\", \"as\": \"m\" } ], \"frame\": [-1e18, 1e18] }"), Stage.Open, Outcome.SpecError, "frame", "/transform/0");
        yield return new Row("transform/regression-order", Sequence("10", "{ \"regression\": \"data\", \"on\": \"data\", \"method\": \"poly\", \"order\": 1e9 }"), Stage.Open, Outcome.SpecError, "integer from", "/transform/0");

        // Expressions
        yield return new Row("expression/length", Sequence("3", "{ \"calculate\": \"" + string.Join(" + ", Enumerable.Repeat("datum.data", 12)) + "\", \"as\": \"w\" }"), Stage.Open, Outcome.SpecError, "MaxExpressionLength", "/transform/0", () => new VegaLiteLimits { MaxExpressionLength = 64 });
        yield return new Row("expression/depth", Sequence("3", "{ \"calculate\": \"" + deepParens + "\", \"as\": \"w\" }"), Stage.Open, Outcome.SpecError, "MaxExpressionDepth", "/transform/0", () => new VegaLiteLimits { MaxExpressionDepth = 8 });
        yield return new Row("expression/steps", Sequence("200", "{ \"calculate\": \"datum.data * 2 + datum.data * 3 + datum.data * 4\", \"as\": \"w\" }"), Stage.Render, Outcome.RenderError, "MaxEvaluationSteps", "/transform/0", () => new VegaLiteLimits { MaxEvaluationSteps = 100 });
        yield return new Row("expression/string", Sequence("3", "{ \"calculate\": \"pad('x', 100000000, 'y', 'left')\", \"as\": \"w\" }"), Stage.Render, Outcome.RenderError, "MaxStringLength", "/transform/0", () => new VegaLiteLimits { MaxStringLength = 1000 });
        // An array that an expression asks for is cut at the limit of a scale domain without a refusal (sequence(0, 1e9) holds MaxScaleDomainSize values)
        yield return new Row("expression/array", Sequence("3", "{ \"calculate\": \"length(sequence(0, 1000000000))\", \"as\": \"w\" }"), Stage.Render, Outcome.Completes, string.Empty, string.Empty);

        // Scales and guides
        yield return new Row("scale/domain", Values(distinct).Replace("\"field\": \"b\", \"type\": \"quantitative\"", "\"field\": \"c\", \"type\": \"nominal\""), Stage.Render, Outcome.RenderError, "MaxScaleDomainSize", "/encoding", () => new VegaLiteLimits { MaxScaleDomainSize = 10 });
        yield return new Row("scale/bins", Values("{ \"a\": 0, \"b\": 1 }, { \"a\": 1000000, \"b\": 1 }").Replace("\"field\": \"a\", \"type\": \"quantitative\"", "\"field\": \"a\", \"type\": \"quantitative\", \"bin\": { \"step\": 1e-9 }"), Stage.Render, Outcome.RenderError, "MaxScaleDomainSize", "/encoding");
        yield return new Row("guide/ticks", Values("{ \"a\": 0, \"b\": 1 }, { \"a\": 1000000, \"b\": 1 }").Replace("\"field\": \"a\", \"type\": \"quantitative\"", "\"field\": \"a\", \"type\": \"quantitative\", \"axis\": { \"tickCount\": 100000 }"), Stage.Render, Outcome.RenderError, "MaxTicks", "/encoding/x");

        // Compositions
        yield return new Row("composition/facet-cells", Values(distinct).Replace("\"encoding\": {", "\"encoding\": { \"column\": { \"field\": \"c\", \"type\": \"nominal\" },"), Stage.Render, Outcome.RenderError, "MaxViews", "/", () => new VegaLiteLimits { MaxViews = 5 });
        yield return new Row("composition/repeat-copies", "{ \"data\": { \"values\": [ { \"a\": 1 } ] }, \"repeat\": { \"row\": [\"a\", \"a\", \"a\", \"a\"], \"column\": [\"a\", \"a\", \"a\", \"a\"] }, \"spec\": { \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": { \"repeat\": \"row\" }, \"type\": \"quantitative\" } } } }", Stage.Render, Outcome.RenderError, "MaxViews", "/repeat", () => new VegaLiteLimits { MaxViews = 5 });
        yield return new Row("composition/concat-children", "{ \"data\": { \"values\": [ { \"a\": 1 } ] }, \"hconcat\": [" + concat + "] }", Stage.Render, Outcome.RenderError, "MaxViews", "/hconcat", () => new VegaLiteLimits { MaxViews = 5 });
        yield return new Row("composition/layers", "{ \"data\": { \"values\": [ { \"a\": 1 } ] }, \"layer\": [" + layers + "] }", Stage.Render, Outcome.RenderError, "MaxViews", "/layer", () => new VegaLiteLimits { MaxViews = 5 });
        yield return new Row("composition/depth", "{ \"data\": { \"values\": [ { \"a\": 1 } ] }, \"hconcat\": [" + nestedConcat + "] }", Stage.Render, Outcome.RenderError, "MaxCompositionDepth", "/hconcat", () => new VegaLiteLimits { MaxCompositionDepth = 3 });

        // The scene and the painter
        yield return new Row("scene/items", Sequence("100", string.Empty), Stage.Render, Outcome.RenderError, "MaxSceneItems", "/", () => new VegaLiteLimits { MaxSceneItems = 50 });
        yield return new Row("scene/images", Values(imageRows, string.Empty).Replace("\"mark\": \"point\"", "\"mark\": \"image\"").Replace("\"y\": { \"field\": \"b\", \"type\": \"quantitative\" }", "\"url\": { \"field\": \"u\", \"type\": \"nominal\" }"), Stage.Render, Outcome.RenderError, "MaxImages", "/mark", () => new VegaLiteLimits { MaxImages = 2 }, null, true);
        yield return new Row("paint/pixels", "{ \"width\": 100, \"height\": 100, \"data\": { \"sequence\": { \"start\": 0, \"stop\": 100 } }, \"mark\": { \"type\": \"point\", \"filled\": true, \"size\": 100000000 }, \"encoding\": { \"x\": { \"field\": \"data\", \"type\": \"quantitative\", \"axis\": null }, \"y\": { \"field\": \"data\", \"type\": \"quantitative\", \"axis\": null } } }", Stage.Render, Outcome.RenderError, "MaxPaintedPixels", "/mark", () => new VegaLiteLimits { MaxPaintedPixels = 100_000 });
        yield return new Row("layout/non-finite", "{ \"data\": { \"values\": [ { \"a\": 1, \"b\": 2 } ] }, \"mark\": \"point\", \"padding\": 1.7976931348623157E+308, \"encoding\": { \"x\": { \"field\": \"a\", \"type\": \"quantitative\" } } }", Stage.Render, Outcome.RenderError, "not a finite number", string.Empty);
        yield return new Row("layout/size", "{ \"width\": 9000, \"data\": { \"values\": [ { \"a\": 1, \"b\": 2 } ] }, \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": \"a\", \"type\": \"quantitative\" } } }", Stage.Natural, Outcome.SizeLimit, "each side must be between", string.Empty);

        // Text
        yield return new Row("text/title", "{ \"title\": \"" + bigTitle + "\", \"data\": { \"values\": [ { \"a\": 1, \"b\": 2 } ] }, \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": \"a\", \"type\": \"quantitative\" } } }", Stage.Open, Outcome.SpecError, "MaxStringLength", "/title", () => new VegaLiteLimits { MaxStringLength = 1000 });

        // Bounded without a refusal
        yield return new Row("format/width", Values("{ \"a\": 1, \"b\": 2 }").Replace("\"field\": \"a\", \"type\": \"quantitative\"", "\"field\": \"a\", \"type\": \"quantitative\", \"axis\": { \"format\": \"1000000000d\" }"), Stage.Render, Outcome.Completes, "NumberFormat.MaxWidth", string.Empty);
        yield return new Row("paint/dash", Values("{ \"a\": 1, \"b\": 2 }, { \"a\": 3, \"b\": 4 }", string.Empty).Replace("\"mark\": \"point\"", "\"mark\": { \"type\": \"line\", \"strokeDash\": [1e-30, 1e-30] }"), Stage.Render, Outcome.Completes, "MaxDashSegments", string.Empty);
        yield return new Row("data/diagnostics-flood", "{ \"data\": { \"values\": [ { \"a\": 1 } ] }, \"layer\": [" + string.Join(",", Enumerable.Repeat("{ \"mark\": \"geoshape\" }", 40)) + "] }", Stage.Render, Outcome.Completes, "MaxDiagnostics", string.Empty, () => new VegaLiteLimits { MaxDiagnostics = 5 });
        yield return new Row("composition/within-limits", "{ \"data\": { \"values\": [ { \"a\": 1 } ] }, \"hconcat\": [" + concat + "] }", Stage.Render, Outcome.Completes, string.Empty, string.Empty);
    }
}
