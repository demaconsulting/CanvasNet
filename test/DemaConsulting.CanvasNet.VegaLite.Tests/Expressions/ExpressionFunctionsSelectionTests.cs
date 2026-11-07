using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using static DemaConsulting.CanvasNet.VegaLite.Tests.Expressions.ExpressionTestSupport;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Expressions;

/// <summary>
///     Tests of the selection functions of the expression table (<c>data</c>, <c>vlSelectionTest</c>, <c>vlSelectionIdTest</c> and <c>vlSelectionResolve</c>) through the real parser and evaluator, with the text of
///     the predicate that Vega-Lite compiles for a selection: <c>!length(data("brush_store")) || vlSelectionTest("brush_store", datum)</c>.
/// </summary>
public class ExpressionFunctionsSelectionTests
{
    /// <summary>The predicate that Vega-Lite compiles for a selection named <c>brush</c> with the default <c>empty</c>.</summary>
    private const string Predicate = """!length(data("brush_store")) || vlSelectionTest("brush_store", datum)""";

    /// <summary>
    ///     Builds the stores of a call: <c>brush_store</c> holds the range 4 to 7 over the field <c>a</c>, and <c>pick_store</c> the rows 1 and 3 by identity.
    /// </summary>
    /// <returns>The stores by name.</returns>
    private static Dictionary<string, SelectionStore> Stores() => new(StringComparer.Ordinal)
    {
        ["brush_store"] = new SelectionStore(
            "brush_store",
            [new SelectionEntry("u", [new SelectionTupleField("a", "x", "R")], [DataValue.FromOwnedArray([DataValue.FromNumber(4), DataValue.FromNumber(7)])])]),
        ["pick_store"] = new SelectionStore(
            "pick_store",
            [new SelectionEntry("u", DataValue.FromNumber(1)), new SelectionEntry("u", DataValue.FromNumber(3))]),
        ["none_store"] = new SelectionStore("none_store", []),
    };

    /// <summary>
    ///     Evaluates an expression against a datum with the stores of the call.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <param name="datum">The datum.</param>
    /// <param name="limits">The limits; the defaults when null.</param>
    /// <param name="stores">The stores; <see cref="Stores"/> when null.</param>
    /// <returns>The value.</returns>
    private static DataValue Run(string text, DataValue datum, VegaLiteLimits? limits = null, Dictionary<string, SelectionStore>? stores = null)
    {
        var evaluator = new ExpressionEvaluator(limits, CancellationToken.None);
        evaluator.Context.SelectionStores = stores ?? Stores();
        return evaluator.Evaluate(Compile(text, limits), datum);
    }

    /// <summary>
    ///     Validates that <c>data</c> gives the tuples of a store as objects, and an empty array for any other name.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_Data_GivesTheTuplesOfAStore()
    {
        // Arrange
        var datum = Obj(("a", DataValue.FromNumber(1)));

        // Act
        var length = Run("""length(data("pick_store"))""", datum);
        var unit = Run("""data("brush_store")[0].unit""", datum);
        var type = Run("""data("brush_store")[0].fields[0].type""", datum);
        var other = Run("""length(data("unknown"))""", datum);
        var withoutStores = new ExpressionEvaluator(null, CancellationToken.None).Evaluate(Compile("""length(data("brush_store"))"""), datum);

        // Assert
        Assert.Equal(2, length.Number);
        Assert.Equal("u", unit.Text);
        Assert.Equal("R", type.Text);
        Assert.Equal(0, other.Number);
        Assert.Equal(0, withoutStores.Number);
    }

    /// <summary>
    ///     Validates the predicate of Vega-Lite on a store of the range 4 to 7: a datum inside (including the ends) is selected, outside is not, and with no store (or a store without tuples) everything is selected.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_VlSelectionTest_PredicateSelectsTheRangeOrEverythingWhenEmpty()
    {
        // Act / Assert: 4 and 7 are the ends, 5 is inside, 3 and 8 are outside
        Assert.Equal([true, true, true, false, false], new[] { 4.0, 5, 7, 3, 8 }.Select(a => ExpressionEvaluator.IsTruthy(Run(Predicate, Obj(("a", DataValue.FromNumber(a)))))));

        // Assert: a store without tuples, and a name that is no store, select everything
        var empty = """!length(data("none_store")) || vlSelectionTest("none_store", datum)""";
        var unknown = """!length(data("nothing_store")) || vlSelectionTest("nothing_store", datum)""";
        Assert.True(ExpressionEvaluator.IsTruthy(Run(empty, Obj(("a", DataValue.FromNumber(100))))));
        Assert.True(ExpressionEvaluator.IsTruthy(Run(unknown, Obj(("a", DataValue.FromNumber(100))))));

        // Assert: with empty: false the same stores select nothing
        Assert.False(ExpressionEvaluator.IsTruthy(Run("""length(data("none_store")) && vlSelectionTest("none_store", datum)""", Obj(("a", DataValue.FromNumber(5))))));
    }

    /// <summary>
    ///     Validates the operation argument and the result of the test on a store that has no tuple: <c>intersect</c> is accepted and the test is falsy (the number zero).
    /// </summary>
    [Fact]
    public void ExpressionFunctions_VlSelectionTest_OperationAndEmptyResult()
    {
        // Arrange
        var inside = Obj(("a", DataValue.FromNumber(5)));

        // Act
        var intersect = Run("""vlSelectionTest("brush_store", datum, "intersect")""", inside);
        var empty = Run("""vlSelectionTest("none_store", datum)""", inside);

        // Assert
        Assert.True(intersect.IsBoolean && intersect.Boolean);
        Assert.True(empty.IsNumber && empty.Number == 0);
    }

    /// <summary>
    ///     Validates the identity test: the rows 1 and 3 are in the store, the row 2 is not.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_VlSelectionIdTest_SelectsTheRowsOfTheStore()
    {
        // Act
        var results = new[] { 1.0, 2, 3 }.Select(id => ExpressionEvaluator.IsTruthy(Run("""vlSelectionIdTest("pick_store", datum)""", Obj(("_vgsid_", DataValue.FromNumber(id))))));

        // Assert
        Assert.Equal([true, false, true], results);
    }

    /// <summary>
    ///     Validates that the identity test of a store that is projected over fields is the field test: the specification chose the identity test for a projection that the configuration or the
    ///     values of the caller then replaced, and Vega-Lite never compiles the identity test for a store of fields.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_VlSelectionIdTest_StoreOfFieldsIsTestedByItsFields()
    {
        // Act: the brush store holds the range 4 to 7 of the field a
        var inside = ExpressionEvaluator.IsTruthy(Run("""vlSelectionIdTest("brush_store", datum)""", Obj(("a", DataValue.FromNumber(5)))));
        var outside = ExpressionEvaluator.IsTruthy(Run("""vlSelectionIdTest("brush_store", datum)""", Obj(("a", DataValue.FromNumber(8)))));

        // Assert
        Assert.True(inside);
        Assert.False(outside);
    }

    /// <summary>
    ///     Validates the resolution of the selection as a value: the range is the value of the field, and a point query is added when asked.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_VlSelectionResolve_GivesTheSelectedValuesOfEachField()
    {
        // Arrange
        var datum = default(DataValue);

        // Act
        var low = Run("""vlSelectionResolve("brush_store", "union").a[0]""", datum);
        var high = Run("""vlSelectionResolve("brush_store", "union").a[1]""", datum);
        var query = Run("""vlSelectionResolve("brush_store", "union", true, true).vlPoint.or[0].a[0]""", datum);
        var members = Run("""vlSelectionResolve("none_store", "union")""", datum);

        // Assert
        Assert.Equal(4, low.Number);
        Assert.Equal(7, high.Number);
        Assert.Equal(4, query.Number);
        Assert.Equal(0, members.ObjectValue!.Count);
    }

    /// <summary>
    ///     Validates that the functions are charged to the step budget: a store of many tuples tested for a row exceeds a small budget with a located render error.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_VlSelectionTest_IsChargedToTheStepBudget()
    {
        // Arrange: 2000 tuples that the datum misses against a budget of 500 steps
        var tuples = Enumerable.Range(0, 2000)
            .Select(i => new SelectionEntry("u", [new SelectionTupleField("a", null, "E")], [DataValue.FromNumber(i + 10)]))
            .ToArray();
        var stores = new Dictionary<string, SelectionStore>(StringComparer.Ordinal) { ["big_store"] = new SelectionStore("big_store", tuples) };

        // Act / Assert
        var ex = Assert.Throws<VegaLiteRenderException>(() => Run("""vlSelectionTest("big_store", datum)""", Obj(("a", DataValue.FromNumber(1))), Steps(500), stores));
        Assert.Contains("steps", ex.Message, StringComparison.Ordinal);
    }
}
