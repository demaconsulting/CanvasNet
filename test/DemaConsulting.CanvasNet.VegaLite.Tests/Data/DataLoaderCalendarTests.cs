using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Data;

/// <summary>
///     Tests of how the calendar of a render reaches the data: the zone of <see cref="VegaLiteRenderOptions.TimeZone"/>
///     through <see cref="DataLoader"/> and <see cref="ExpressionEvaluator"/>, the implicit date parse of the main data
///     (and not of a lookup source), and the calendar of the expression context. All zones are built in the tests.
/// </summary>
public class DataLoaderCalendarTests
{
    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Creates a loader over a specification with a calendar.
    /// </summary>
    /// <param name="tree">The specification.</param>
    /// <param name="calendar">The calendar, or null for the default.</param>
    /// <param name="files">The virtual files, or null.</param>
    /// <param name="caller">The caller's datasets, or null.</param>
    /// <returns>The loader.</returns>
    private static DataLoader Loader(
        SpecTree tree,
        TimeCalendar? calendar,
        VirtualDataFiles? files = null,
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>>? caller = null) =>
        new(tree.SourceName, null, files is null ? null : new VegaLiteOpenOptions { FileResolver = files.Resolve }, tree.Root["datasets"], caller, Ct, calendar: calendar);

    /// <summary>
    ///     Parses a view with the given <c>data</c> object text.
    /// </summary>
    /// <param name="dataJson">The text of the <c>data</c> object.</param>
    /// <returns>The tree.</returns>
    private static SpecTree Spec(string dataJson) => SpecParser.Parse("{\"data\":" + dataJson + ",\"mark\":\"point\"}", "spec.vl.json");

    /// <summary>
    ///     Validates that the calendar reaches every kind of source: inline values, a CSV file, a JSON file and the rows
    ///     of the caller, each with an explicit date parse of text without an offset.
    /// </summary>
    [Fact]
    public void DataLoader_Load_PassesTheCalendarToInlineCsvJsonAndCallerRows()
    {
        // Arrange: a zone at plus 05:30, where 10:00 local is 04:30Z
        var calendar = TimeTestSupport.Calendar(TimeTestSupport.Fixed(5, 30));
        var files = new VirtualDataFiles()
            .With("d.csv", "d\n2024-03-05T10:00\n")
            .With("d.json", "[{\"d\":\"2024-03-05T10:00\"}]");
        const string parse = "\"format\":{\"parse\":{\"d\":\"date\"}}";
        var caller = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>>
        {
            ["rows"] = [new Dictionary<string, object?> { ["d"] = "2024-03-05T10:00" }],
        };
        var expected = TimeTestSupport.Ms(2024, 3, 5, 4, 30);

        // Act
        var inline = Loader(Spec("{\"values\":[{\"d\":\"2024-03-05T10:00\"}]," + parse + "}"), calendar).Load(Spec("{\"values\":[{\"d\":\"2024-03-05T10:00\"}]," + parse + "}").Root["data"]);
        var csvTree = Spec("{\"url\":\"d.csv\"," + parse + "}");
        var csv = Loader(csvTree, calendar, files).Load(csvTree.Root["data"]);
        var jsonTree = Spec("{\"url\":\"d.json\"," + parse + "}");
        var json = Loader(jsonTree, calendar, files).Load(jsonTree.Root["data"]);
        var namedTree = Spec("{\"name\":\"rows\"," + parse + "}");
        var named = Loader(namedTree, calendar, null, caller).Load(namedTree.Root["data"]);
        var datasetTree = SpecParser.Parse("{\"datasets\":{\"t\":[{\"d\":\"2024-03-05T10:00\"}]},\"data\":{\"name\":\"t\"," + parse + "},\"mark\":\"point\"}");
        var dataset = Loader(datasetTree, calendar).Load(datasetTree.Root["data"]);

        // Assert
        Assert.All(new[] { inline, csv, json, named, dataset }, table => Assert.Equal(expected, table.GetValue(0, "d").Milliseconds));
        Assert.All(new[] { inline, csv, json, named, dataset }, table => Assert.Equal(DataValueKind.Date, table.GetValue(0, "d").Kind));
    }

    /// <summary>
    ///     Validates that a loader without a calendar reads in UTC, and that the dates a caller supplies are not moved by a zone.
    /// </summary>
    [Fact]
    public void DataLoader_Load_DefaultCalendarIsUtcAndCallerDatesAreNotMoved()
    {
        // Arrange
        var tree = Spec("{\"values\":[{\"d\":\"2024-03-05T10:00\"}],\"format\":{\"parse\":{\"d\":\"date\"}}}");
        var caller = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>>
        {
            ["rows"] = [new Dictionary<string, object?> { ["d"] = new DateTime(2024, 3, 5, 10, 0, 0, DateTimeKind.Utc) }],
        };
        var namedTree = Spec("{\"name\":\"rows\",\"format\":{\"parse\":{\"d\":\"date\"}}}");

        // Act
        var byDefault = Loader(tree, null).Load(tree.Root["data"]);
        var moved = Loader(namedTree, TimeTestSupport.Calendar(TimeTestSupport.Fixed(-8)), null, caller).Load(namedTree.Root["data"]);

        // Assert
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 5, 10), byDefault.GetValue(0, "d").Milliseconds);
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 5, 10), moved.GetValue(0, "d").Milliseconds);
    }

    /// <summary>
    ///     Validates that the implicit date fields apply to the main data of every kind of source, that an explicit parse
    ///     wins, and that the same data loaded as a secondary source (no implicit fields) stays text.
    /// </summary>
    [Fact]
    public void DataLoader_Load_ImplicitDateFieldsApplyOnlyWhenGiven()
    {
        // Arrange
        var calendar = TimeTestSupport.Calendar(TimeTestSupport.Fixed(1));
        var files = new VirtualDataFiles().With("d.csv", "d,e\n2024-03-05T10:00,2024-03-05T10:00\n").With("d.json", "[{\"d\":\"2024-03-05T10:00\"}]");
        var csvTree = Spec("{\"url\":\"d.csv\",\"format\":{\"parse\":{\"e\":\"string\"}}}");
        var jsonTree = Spec("{\"url\":\"d.json\"}");
        var implicitFields = new HashSet<string>(StringComparer.Ordinal) { "d", "e" };

        // Act
        var main = Loader(csvTree, calendar, files).Load(csvTree.Root["data"], implicitFields);
        var secondary = Loader(csvTree, calendar, files).Load(csvTree.Root["data"]);
        var jsonMain = Loader(jsonTree, calendar, files).Load(jsonTree.Root["data"], implicitFields);

        // Assert: 10:00 at +01:00 is 09:00Z; the explicit string wins for e; the secondary source is untouched
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 5, 9), main.GetValue(0, "d").Milliseconds);
        Assert.Equal(DataValueKind.String, main.GetValue(0, "e").Kind);
        Assert.Equal(DataValueKind.String, secondary.GetValue(0, "d").Kind);
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 5, 9), jsonMain.GetValue(0, "d").Milliseconds);
    }

    /// <summary>
    ///     Validates that the data of a lookup is not parsed implicitly: the main data is loaded with the temporal fields of
    ///     the encoding, the secondary source through the same loader without them, so its dates arrive as text.
    /// </summary>
    [Fact]
    public void DataLoader_Load_SecondaryLookupDataIsNotImplicitlyParsed()
    {
        // Arrange: the temporal field d exists only in the lookup source
        const string json = """
            { "data": { "values": [ { "k": 1 } ] },
              "transform": [ { "lookup": "k", "from": { "data": { "values": [ { "k": 1, "d": "2024-03-05" } ] }, "key": "k", "fields": [ "d" ] } } ],
              "mark": "point",
              "encoding": { "x": { "field": "d", "type": "temporal" } } }
            """;
        var tree = SpecParser.Parse(json, "t.vl.json");
        var loader = Loader(tree, null);
        var table = loader.Load(tree.Root["data"], TemporalFields.Collect(tree.Root));
        var values = new Dictionary<string, DataValue>(StringComparer.Ordinal);
        var evaluator = new ExpressionEvaluator(null, Ct, values);

        // Act
        var result = RowTransforms.Apply(table, tree.Root["transform"], new TransformContext(tree, loader, evaluator, values, VegaLiteLimits.Default, Ct, []));

        // Assert: the looked-up date is the text that the source holds
        Assert.Equal(DataValueKind.String, result.GetValue(0, "d").Kind);
        Assert.Equal("2024-03-05", result.GetValue(0, "d").Text);
    }

    /// <summary>
    ///     Validates the end-to-end wiring of the zone: with a zone of plus five hours, a date-time without an offset moves
    ///     five hours against the same date-time with an offset in the scene of a render, and not in UTC.
    /// </summary>
    [Fact]
    public void DataLoader_Render_TimeZoneOptionReachesTheDataStage()
    {
        // Arrange: row a has an explicit UTC offset, row b none; the calculated number is the instant in milliseconds
        const string json = """
            { "width": 200, "height": 100,
              "data": { "values": [ { "k": "a", "d": "2024-03-05T10:00:00Z" }, { "k": "b", "d": "2024-03-05T10:00:00" } ],
                        "format": { "parse": { "d": "date" } } },
              "transform": [ { "calculate": "toNumber(datum.d)", "as": "n" } ],
              "mark": "point",
              "encoding": { "x": { "field": "n", "type": "quantitative" }, "y": { "field": "k", "type": "nominal" } } }
            """;
        var document = VegaLiteDocument.Parse(json);

        // Act
        var utc = document.BuildScene(new VegaLiteRenderOptions(), Ct).FindAll("mark-point").Select(e => ((SceneSymbol)e.Item).X).ToList();
        var zoned = document.BuildScene(new VegaLiteRenderOptions { TimeZone = TimeTestSupport.Fixed(5) }, Ct).FindAll("mark-point").Select(e => ((SceneSymbol)e.Item).X).ToList();

        // Assert: in UTC both rows are the same instant; five hours east, row b is five hours earlier, which is to the left
        Assert.Equal(2, utc.Count);
        Assert.Equal(utc[0], utc[1]);
        Assert.Equal(2, zoned.Count);
        Assert.True(zoned[1] < zoned[0]);
    }

    /// <summary>
    ///     Validates that a custom date format of a specification renders in strict mode (it is no unsupported feature) and
    ///     that the dates it reads are in order on the axis.
    /// </summary>
    [Fact]
    public void DataLoader_Render_CustomDateFormatRendersInStrictMode()
    {
        // Arrange: day-month-year text, where the order of the days differs from the order of the text
        const string json = """
            { "width": 200, "height": 100,
              "data": { "values": [ { "k": "a", "d": "15/03/2024" }, { "k": "b", "d": "05/03/2024" } ],
                        "format": { "parse": { "d": "date:'%d/%m/%Y'" } } },
              "transform": [ { "calculate": "toNumber(datum.d)", "as": "n" } ],
              "mark": "point",
              "encoding": { "x": { "field": "n", "type": "quantitative" }, "y": { "field": "k", "type": "nominal" } } }
            """;
        var document = VegaLiteDocument.Parse(json);

        // Act
        var xs = document.BuildScene(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true }, Ct).FindAll("mark-point").Select(e => ((SceneSymbol)e.Item).X).ToList();

        // Assert: the 5th of March is to the left of the 15th
        Assert.Equal(2, xs.Count);
        Assert.True(xs[1] < xs[0]);
    }

    /// <summary>
    ///     Validates that the expression context carries a calendar that is UTC unless given one.
    /// </summary>
    [Fact]
    public void ExpressionFunctionContext_Calendar_DefaultsToUtc()
    {
        // Arrange
        var zone = TimeTestSupport.Calendar(TimeTestSupport.UsLike());

        // Act
        var byDefault = new ExpressionFunctionContext(null, Ct);
        var given = new ExpressionFunctionContext(null, Ct, zone);

        // Assert
        Assert.Same(TimeCalendar.Utc, byDefault.Calendar);
        Assert.Same(zone, given.Calendar);
        Assert.Equal(VegaLiteLimits.Default.MaxScaleDomainSize, byDefault.MaxArrayLength);
        Assert.Equal(7, new ExpressionFunctionContext(new VegaLiteLimits { MaxScaleDomainSize = 7 }, Ct).MaxArrayLength);
    }

    /// <summary>
    ///     Validates that the evaluator forwards its calendar to the context that its functions use.
    /// </summary>
    [Fact]
    public void ExpressionEvaluator_Constructor_ForwardsTheCalendar()
    {
        // Arrange
        var zone = TimeTestSupport.Calendar(TimeTestSupport.Fixed(2));

        // Act
        var byDefault = new ExpressionEvaluator(null, Ct);
        var given = new ExpressionEvaluator(null, Ct, null, zone);

        // Assert
        Assert.Same(TimeCalendar.Utc, byDefault.Context.Calendar);
        Assert.Same(zone, given.Context.Calendar);
    }
}
