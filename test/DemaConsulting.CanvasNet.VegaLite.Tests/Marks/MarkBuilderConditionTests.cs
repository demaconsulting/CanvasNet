using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Marks;

/// <summary>
///     Unit tests of the conditional encodings of <see cref="DemaConsulting.CanvasNet.VegaLite.Marks.MarkBuilder"/>: the <c>test</c> of a
///     condition is evaluated for every row, the first condition that holds gives the value, and the channel's own value, its scale or the
///     default of the mark gives the rest, as Vega-Lite's conditional encodings do.
/// </summary>
public class MarkBuilderConditionTests
{
    /// <summary>The first color of the tableau10 palette, which the scale gives the first category and a circle uses by default.</summary>
    private const string First = "#4c78a8";

    /// <summary>The second color of the tableau10 palette.</summary>
    private const string Second = "#f58518";

    /// <summary>Three rows: q is 1, 3 and 5, and the category c is a, b and a.</summary>
    private const string Rows = """[ { "q": 1, "c": "a" }, { "q": 3, "c": "b" }, { "q": 5, "c": "a" } ]""";

    /// <summary>
    ///     Builds a specification of a mark in a 100 by 100 view over the three rows.
    /// </summary>
    /// <param name="mark">The JSON text of the mark.</param>
    /// <param name="channels">The JSON text of the channels after the x channel (a comma separated list of properties).</param>
    /// <param name="extra">The JSON text of additional top-level properties, with a trailing comma, or empty.</param>
    /// <returns>The specification text.</returns>
    private static string Spec(string mark, string channels, string extra = "") =>
        "{ " + extra + " \"width\": 100, \"height\": 100, \"data\": { \"values\": " + Rows + " }, \"mark\": " + mark
        + ", \"encoding\": { \"x\": { \"field\": \"q\", \"type\": \"quantitative\" }, " + channels + " } }";

    /// <summary>
    ///     Builds the circles of a specification.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="limits">The limits, or null.</param>
    /// <returns>The symbols in data order.</returns>
    private static List<SceneSymbol> Circles(string json, VegaLiteLimits? limits = null) =>
        ChartTestHelpers.Of<SceneSymbol>(ChartTestHelpers.Marks(json, 100, 100, null, limits), "mark-circle");

    /// <summary>
    ///     Validates that a value condition gives its value to the rows whose test holds and leaves the channel's own value to the others.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_ValueConditionOverridesTheBaseValueWhereTheTestHolds()
    {
        // Arrange: q is 1, 3 and 5; only 5 is greater than 3
        var json = Spec("\"circle\"", """ "color": { "condition": { "test": "datum.q > 3", "value": "red" }, "value": "gray" } """);

        // Act
        var items = Circles(json);

        // Assert
        Assert.Equal(["gray", "gray", "red"], items.Select(i => i.Fill));
    }

    /// <summary>
    ///     Validates that, without a value of its own, a row where no condition holds keeps the default of the mark.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_ConditionWithoutBaseValueLeavesTheMarkDefault()
    {
        // Arrange
        var json = Spec("\"circle\"", """ "color": { "condition": { "test": "datum.q > 3", "value": "red" } } """);

        // Act
        var items = Circles(json);

        // Assert: the default color of a filled mark is the first color of the palette
        Assert.Equal([First, First, "red"], items.Select(i => i.Fill));
    }

    /// <summary>
    ///     Validates that the first condition of an array whose test holds wins, whatever the later ones say.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_FirstConditionThatHoldsWins()
    {
        // Arrange: row q=1 matches none, q=3 matches the first (greater than 1), q=5 matches both and takes the first
        var json = Spec(
            "\"circle\"",
            """ "color": { "condition": [ { "test": "datum.q > 1", "value": "red" }, { "test": "datum.q > 3", "value": "blue" } ], "value": "gray" } """);

        // Act
        var items = Circles(json);

        // Assert
        Assert.Equal(["gray", "red", "red"], items.Select(i => i.Fill));
    }

    /// <summary>
    ///     Validates that a field condition draws its field through its own scale where the test holds and the value elsewhere.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_FieldConditionUsesTheScaleOfItsFieldWhereTheTestHolds()
    {
        // Arrange: c is a, b, a, so the ordinal scale gives a the first and b the second palette color; the test fails for q=1
        var json = Spec(
            "\"circle\"",
            """ "color": { "condition": { "test": "datum.q > 1", "field": "c", "type": "nominal" }, "value": "gray" } """);

        // Act
        var items = Circles(json);

        // Assert
        Assert.Equal(["gray", Second, First], items.Select(i => i.Fill));
    }

    /// <summary>
    ///     Validates that a field definition with value conditions maps its field for the rows no condition holds for.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_FieldWithValueConditionMapsTheFieldElsewhere()
    {
        // Arrange
        var json = Spec(
            "\"circle\"",
            """ "color": { "field": "c", "type": "nominal", "condition": { "test": "datum.q > 3", "value": "red" } } """);

        // Act
        var items = Circles(json);

        // Assert: a and b take their palette colors, the row with q=5 takes the condition
        Assert.Equal([First, Second, "red"], items.Select(i => i.Fill));
    }

    /// <summary>
    ///     Validates the numeric channels: opacity, size, stroke width, fill opacity and angle, with and without a base value.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_NumericChannelConditionsGiveValuesAndKeepDefaults()
    {
        // Arrange: a circle has the opacity 0.7 and the area 30 by default
        var withBase = Circles(Spec(
            "\"circle\"",
            """
            "opacity": { "condition": { "test": "datum.q > 3", "value": 1 }, "value": 0.2 },
            "size": { "condition": { "test": "datum.q > 3", "value": 200 }, "value": 50 },
            "strokeWidth": { "condition": { "test": "datum.q > 3", "value": 4 }, "value": 1 },
            "angle": { "condition": { "test": "datum.q > 3", "value": 45 }, "value": 10 }
            """));
        var withoutBase = Circles(Spec(
            "\"circle\"",
            """
            "opacity": { "condition": { "test": "datum.q > 3", "value": 1 } },
            "size": { "condition": { "test": "datum.q > 3", "value": 200 } },
            "angle": { "condition": { "test": "datum.q > 3", "value": 45 } }
            """));

        // Assert: the condition replaces the base value; without a base the default of the mark remains
        Assert.Equal([0.2, 0.2, 1.0], withBase.Select(i => i.Opacity));
        Assert.Equal([50.0, 50.0, 200.0], withBase.Select(i => i.Size));
        Assert.Equal([1.0, 1.0, 4.0], withBase.Select(i => i.StrokeWidth ?? double.NaN));
        Assert.Equal([10.0, 10.0, 45.0], withBase.Select(i => i.Angle));
        Assert.Equal([0.7, 0.7, 1.0], withoutBase.Select(i => i.Opacity));
        Assert.Equal([30.0, 30.0, 200.0], withoutBase.Select(i => i.Size));
        Assert.Equal([0.0, 0.0, 45.0], withoutBase.Select(i => i.Angle));
    }

    /// <summary>
    ///     Validates the fill opacity and stroke opacity conditions.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_FillAndStrokeOpacityConditionsApplyPerRow()
    {
        // Arrange
        var json = Spec(
            "{ \"type\": \"circle\", \"stroke\": \"black\" }",
            """
            "fillOpacity": { "condition": { "test": "datum.q > 3", "value": 0.9 }, "value": 0.1 },
            "strokeOpacity": { "condition": { "test": "datum.q < 3", "value": 0.3 }, "value": 0.6 }
            """);

        // Act
        var items = Circles(json);

        // Assert
        Assert.Equal([0.1, 0.1, 0.9], items.Select(i => i.FillOpacity));
        Assert.Equal([0.3, 0.6, 0.6], items.Select(i => i.StrokeOpacity));
    }

    /// <summary>
    ///     Validates the shape and stroke dash conditions of a point.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_ShapeAndDashConditionsApplyPerRow()
    {
        // Arrange
        var json = Spec(
            "\"point\"",
            """
            "shape": { "condition": { "test": "datum.q > 3", "value": "square" }, "value": "diamond" },
            "strokeDash": { "condition": { "test": "datum.q > 3", "value": [4, 2] } }
            """);

        // Act
        var items = ChartTestHelpers.Of<SceneSymbol>(ChartTestHelpers.Marks(json, 100, 100), "mark-point");

        // Assert: the dash is solid (none) where no condition holds
        Assert.Equal(["diamond", "diamond", "square"], items.Select(i => i.Shape));
        Assert.Null(items[0].StrokeDash);
        Assert.Null(items[1].StrokeDash);
        Assert.Equal([4.0, 2.0], items[2].StrokeDash!);
    }

    /// <summary>
    ///     Validates that the predicate objects of a <c>test</c> are evaluated: comparisons, a range, a set, a validity check and the compositions.
    /// </summary>
    [Theory]
    [InlineData("""{ "field": "q", "gt": 3 }""", "gray,gray,red")]
    [InlineData("""{ "field": "q", "gte": 3 }""", "gray,red,red")]
    [InlineData("""{ "field": "q", "lt": 3 }""", "red,gray,gray")]
    [InlineData("""{ "field": "q", "lte": 3 }""", "red,red,gray")]
    [InlineData("""{ "field": "q", "equal": 3 }""", "gray,red,gray")]
    [InlineData("""{ "field": "q", "range": [2, 4] }""", "gray,red,gray")]
    [InlineData("""{ "field": "q", "range": [4, 2] }""", "gray,red,gray")]
    [InlineData("""{ "field": "c", "oneOf": ["a"] }""", "red,gray,red")]
    [InlineData("""{ "field": "c", "valid": true }""", "red,red,red")]
    [InlineData("""{ "field": "missing", "valid": true }""", "gray,gray,gray")]
    [InlineData("""{ "not": { "field": "c", "equal": "a" } }""", "gray,red,gray")]
    [InlineData("""{ "and": [ { "field": "q", "gt": 1 }, { "field": "c", "equal": "a" } ] }""", "gray,gray,red")]
    [InlineData("""{ "or": [ { "field": "q", "lt": 2 }, { "field": "q", "gt": 4 } ] }""", "red,gray,red")]
    public void MarkBuilder_Build_PredicateObjectTestsAreEvaluated(string test, string expected)
    {
        // Arrange
        var json = Spec("\"circle\"", "\"color\": { \"condition\": { \"test\": " + test + ", \"value\": \"red\" }, \"value\": \"gray\" }");

        // Act
        var items = Circles(json);

        // Assert
        Assert.Equal(expected, string.Join(',', items.Select(i => i.Fill)));
    }

    /// <summary>
    ///     Validates that a test that reads a variable parameter sees its value, and that a missing field is a test that does not hold.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_TestReadsVariableParametersAndMissingFieldsDoNotHold()
    {
        // Arrange: the threshold is a parameter; the second condition reads a field that no row has
        var json = Spec(
            "\"circle\"",
            """
            "color": { "condition": [ { "test": "datum.q > threshold", "value": "red" }, { "test": "datum.nothing > 0", "value": "blue" } ], "value": "gray" }
            """,
            """ "params": [ { "name": "threshold", "value": 1 } ], """);

        // Act
        var items = Circles(json);

        // Assert
        Assert.Equal(["gray", "red", "red"], items.Select(i => i.Fill));
    }

    /// <summary>
    ///     Validates that a condition on a parameter never holds: the channel keeps its own value.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_ParameterConditionIsNotApplied()
    {
        // Arrange
        var json = Spec(
            "\"circle\"",
            """ "color": { "condition": { "param": "brush", "value": "red" }, "value": "gray" } """,
            """ "params": [ { "name": "brush", "select": "interval" } ], """);

        // Act
        var items = Circles(json);

        // Assert
        Assert.Equal(["gray", "gray", "gray"], items.Select(i => i.Fill));
    }

    /// <summary>
    ///     Validates the conditions of the bar, tick and rule marks (the color and the opacity apply to their rectangles and lines).
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_ConditionsApplyToBarsTicksAndRules()
    {
        // Arrange
        const string channels = """
            "color": { "condition": { "test": "datum.q > 3", "value": "red" }, "value": "gray" },
            "opacity": { "condition": { "test": "datum.q > 3", "value": 1 }, "value": 0.5 }
            """;

        // Act
        var bars = ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(Spec("\"bar\"", channels), 100, 100), "mark-bar");
        var ticks = ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(Spec("\"tick\"", channels), 100, 100), "mark-tick");
        var rules = ChartTestHelpers.Of<SceneRule>(ChartTestHelpers.Marks(Spec("\"rule\"", channels), 100, 100), "mark-rule");

        // Assert
        Assert.Equal(["gray", "gray", "red"], bars.Select(i => i.Fill));
        Assert.Equal([0.5, 0.5, 1.0], bars.Select(i => i.Opacity));
        Assert.Equal(["gray", "gray", "red"], ticks.Select(i => i.Fill));
        Assert.Equal([0.5, 0.5, 1.0], ticks.Select(i => i.Opacity));
        Assert.Equal(["gray", "gray", "red"], rules.Select(i => i.Stroke));
        Assert.Equal([0.5, 0.5, 1.0], rules.Select(i => i.Opacity));
    }

    /// <summary>
    ///     Validates that the size condition of a bar gives the thickness of the bar of the row.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_SizeConditionGivesTheThicknessOfABar()
    {
        // Arrange: a bar on a quantitative x with no y has the thickness that the size channel gives
        var json = Spec("\"bar\"", """ "size": { "condition": { "test": "datum.q > 3", "value": 9 }, "value": 4 } """);

        // Act
        var bars = ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(json, 100, 100), "mark-bar");

        // Assert
        Assert.Equal([4.0, 4.0, 9.0], bars.Select(i => i.Height));
    }

    /// <summary>
    ///     Validates that the evaluation of the tests is charged to the step budget (a located render failure naming the limit) and
    ///     that a roomy budget draws every row.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_TestEvaluationIsChargedToTheStepBudget()
    {
        // Arrange
        var json = Spec("\"circle\"", """ "color": { "condition": { "test": "datum.q > 3", "value": "red" }, "value": "gray" } """);

        // Act
        var roomy = Circles(json, new VegaLiteLimits { MaxEvaluationSteps = 10_000 });
        var tight = Assert.Throws<VegaLiteRenderException>(() => Circles(json, new VegaLiteLimits { MaxEvaluationSteps = 4 }));

        // Assert
        Assert.Equal(3, roomy.Count);
        Assert.Contains("MaxEvaluationSteps", tight.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a canceled token stops the evaluation of the conditions.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_CanceledTokenStopsTheEvaluation()
    {
        // Arrange
        var json = Spec("\"circle\"", """ "color": { "condition": { "test": "datum.q > 3", "value": "red" }, "value": "gray" } """);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        // Act and Assert
        Assert.ThrowsAny<OperationCanceledException>(() => ChartTestHelpers.Marks(json, 100, 100, null, null, canceled.Token));
    }

    /// <summary>
    ///     Validates that building twice gives identical items.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_ConditionalMarksAreDeterministic()
    {
        // Arrange
        var json = Spec(
            "\"circle\"",
            """ "color": { "condition": { "test": { "field": "q", "gt": 1 }, "field": "c", "type": "nominal" }, "value": "gray" }, "size": { "condition": { "test": "datum.q > 3", "value": 80 } } """);

        // Act
        var first = Circles(json);
        var second = Circles(json);

        // Assert
        Assert.Equal(first, second);
    }
}
