using System.Globalization;
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using static DemaConsulting.CanvasNet.VegaLite.Tests.Expressions.ExpressionTestSupport;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Expressions;

/// <summary>
///     Tests of <see cref="SelectionTestSemantics"/>, the port of the selection functions of Vega (<c>vlSelectionTest</c>, <c>vlSelectionIdTest</c> and <c>vlSelectionResolve</c>): the stores are written out tuple
///     by tuple and every expected result follows from the rule of the tuple type (strict equality, inclusive range, range that excludes its end) and of the combination of the units, by hand.
/// </summary>
public class SelectionTestSemanticsTests
{
    /// <summary>Gets the evaluation state that the tests charge their work to.</summary>
    private static ExpressionFunctionContext Context => new(null, CancellationToken.None);

    /// <summary>
    ///     Builds a number value.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The value.</returns>
    private static DataValue N(double value) => DataValue.FromNumber(value);

    /// <summary>
    ///     Builds an array value.
    /// </summary>
    /// <param name="items">The elements.</param>
    /// <returns>The value.</returns>
    private static DataValue A(params DataValue[] items) => DataValue.FromOwnedArray(items);

    /// <summary>
    ///     Builds a datum with one field.
    /// </summary>
    /// <param name="field">The field name.</param>
    /// <param name="value">The value.</param>
    /// <returns>The datum.</returns>
    private static DataValue Datum(string field, DataValue value) => Obj((field, value));

    /// <summary>
    ///     Builds a store of one tuple over one field.
    /// </summary>
    /// <param name="type">The tuple store type.</param>
    /// <param name="value">The value of the field in the tuple.</param>
    /// <returns>The store.</returns>
    private static SelectionStore OneField(string type, DataValue value) =>
        new("s_store", [new SelectionEntry("u", [new SelectionTupleField("a", "x", type)], [value])]);

    /// <summary>
    ///     Builds a store of tuples over the field <c>a</c>, each in a unit.
    /// </summary>
    /// <param name="tuples">The unit, the type and the value of each tuple.</param>
    /// <returns>The store.</returns>
    private static SelectionStore Units(params (string Unit, string Type, DataValue Value)[] tuples) =>
        new("s_store", [.. tuples.Select(t => new SelectionEntry(t.Unit, [new SelectionTupleField("a", null, t.Type)], [t.Value]))]);

    /// <summary>
    ///     Tests a datum of one field against a store.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="value">The value of <c>a</c> in the datum.</param>
    /// <param name="operation">The operation, or null.</param>
    /// <returns>Whether the datum is in the selection.</returns>
    private static bool In(SelectionStore store, DataValue value, string? operation = null) =>
        ExpressionEvaluator.IsTruthy(SelectionTestSemantics.Test(store, Datum("a", value), operation, Context));

    /// <summary>
    ///     Writes a value as text, the way a JSON document would (numbers in the shortest form).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The text.</returns>
    private static string Show(DataValue value)
    {
        if (value.IsArray)
        {
            return "[" + string.Join(",", value.Elements.ToArray().Select(Show)) + "]";
        }

        if (value.IsObject)
        {
            var members = value.ObjectValue!;
            return "{" + string.Join(",", members.Keys.Select(k => k + ":" + Show(members.TryGetValue(k, out var v) ? v : default))) + "}";
        }

        if (value.IsString)
        {
            return "'" + value.Text + "'";
        }

        return value.IsNumber ? value.Number.ToString("R", CultureInfo.InvariantCulture) : value.ToText();
    }

    /// <summary>
    ///     Validates that an enumerated scalar matches by strict equality: the number 1 is not the text "1", and a datum without the field matches nothing.
    /// </summary>
    [Fact]
    public void SelectionTestSemantics_Test_EnumeratedScalarUsesStrictEquality()
    {
        // Arrange: the tuple says a === 1
        var store = OneField("E", N(1));

        // Act / Assert
        Assert.True(In(store, N(1)));
        Assert.False(In(store, DataValue.FromString("1")));
        Assert.False(In(store, N(2)));
        Assert.False(In(store, DataValue.FromBoolean(true)));
        Assert.False(ExpressionEvaluator.IsTruthy(SelectionTestSemantics.Test(store, Datum("b", N(1)), null, Context)));
    }

    /// <summary>
    ///     Validates that an enumerated list matches by membership: the interval over a discrete axis stores the values at its two ends and only those match.
    /// </summary>
    [Fact]
    public void SelectionTestSemantics_Test_EnumeratedListMatchesByMembership()
    {
        // Arrange: the tuple says a is one of 'x' and 'y'
        var store = OneField("E", A(DataValue.FromString("x"), DataValue.FromString("y")));

        // Act / Assert
        Assert.True(In(store, DataValue.FromString("x")));
        Assert.True(In(store, DataValue.FromString("y")));
        Assert.False(In(store, DataValue.FromString("z")));
    }

    /// <summary>
    ///     Validates that a range includes both ends, that a reversed range is swapped, and that NaN is in no range.
    /// </summary>
    [Fact]
    public void SelectionTestSemantics_Test_RangeIncludesBothEndsAndIsSwapped()
    {
        // Arrange: the tuple says 2 <= a <= 5, written the wrong way round
        var store = OneField("R", A(N(5), N(2)));

        // Act / Assert
        Assert.True(In(store, N(2)));
        Assert.True(In(store, N(5)));
        Assert.True(In(store, N(3.5)));
        Assert.False(In(store, N(1.999)));
        Assert.False(In(store, N(5.001)));
        Assert.False(In(store, N(double.NaN)));
    }

    /// <summary>
    ///     Validates the other range types: <c>R-RE</c> (a bin) includes its start and not its end, <c>R-E</c> neither end, <c>R-LE</c> its end only.
    /// </summary>
    [Fact]
    public void SelectionTestSemantics_Test_BinAndExclusiveRangeTypes()
    {
        // Arrange: the range [2, 5] under each type
        var bin = OneField("R-RE", A(N(2), N(5)));
        var open = OneField("R-E", A(N(2), N(5)));
        var rightClosed = OneField("R-LE", A(N(2), N(5)));

        // Act / Assert: start 2, inside 3, end 5
        Assert.Equal([true, true, false], new[] { 2.0, 3, 5 }.Select(v => In(bin, N(v))));
        Assert.Equal([false, true, false], new[] { 2.0, 3, 5 }.Select(v => In(open, N(v))));
        Assert.Equal([false, true, true], new[] { 2.0, 3, 5 }.Select(v => In(rightClosed, N(v))));
    }

    /// <summary>
    ///     Validates that a date in the data is compared as its number of milliseconds with the numbers of the range.
    /// </summary>
    [Fact]
    public void SelectionTestSemantics_Test_DateOfTheDatumIsComparedAsANumber()
    {
        // Arrange: the instants 1000 and 3000 as numbers
        var store = OneField("R", A(N(1000), N(3000)));

        // Act / Assert
        Assert.True(In(store, DataValue.FromDate(2000)));
        Assert.True(In(store, DataValue.FromDate(3000)));
        Assert.False(In(store, DataValue.FromDate(3001)));
    }

    /// <summary>
    ///     Validates that a date in the values of a tuple is stored as its number of milliseconds.
    /// </summary>
    [Fact]
    public void SelectionEntry_Create_DatesOfTheValuesBecomeNumbers()
    {
        // Arrange / Act
        var entry = new SelectionEntry("u", [new SelectionTupleField("a", "x", "R")], [A(DataValue.FromDate(1000), DataValue.FromDate(3000))]);

        // Assert
        Assert.Equal("[1000,3000]", Show(entry.Values[0]));
    }

    /// <summary>
    ///     Validates the predicate types that no static chart produces: they compare the datum with one value, or test validity and membership.
    /// </summary>
    [Fact]
    public void SelectionTestSemantics_Test_ComparisonTypes()
    {
        // Arrange / Act / Assert: E-LT misses at a >= 3, E-GTE at a < 3, E-GT at a <= 3, E-LTE at a > 3
        Assert.Equal([true, false, false], new[] { 2.0, 3, 4 }.Select(v => In(OneField("E-LT", N(3)), N(v))));
        Assert.Equal([false, true, true], new[] { 2.0, 3, 4 }.Select(v => In(OneField("E-GTE", N(3)), N(v))));
        Assert.Equal([false, false, true], new[] { 2.0, 3, 4 }.Select(v => In(OneField("E-GT", N(3)), N(v))));
        Assert.Equal([true, true, false], new[] { 2.0, 3, 4 }.Select(v => In(OneField("E-LTE", N(3)), N(v))));

        // Assert: E-VALID misses null and NaN, E-ONE needs membership
        var valid = OneField("E-VALID", default);
        Assert.True(In(valid, N(0)));
        Assert.False(In(valid, default));
        Assert.False(In(valid, N(double.NaN)));
        var oneOf = OneField("E-ONE", A(N(1), N(2)));
        Assert.True(In(oneOf, N(2)));
        Assert.False(In(oneOf, N(3)));
    }

    /// <summary>
    ///     Validates the result for a store without tuples: the number zero, which is falsy, for a store with no tuple and for a name that is not a store.
    /// </summary>
    [Fact]
    public void SelectionTestSemantics_Test_EmptyStoreIsFalsyZero()
    {
        // Arrange
        var empty = new SelectionStore("s_store", []);

        // Act
        var viaStore = SelectionTestSemantics.Test(empty, Datum("a", N(1)), null, Context);
        var viaNull = SelectionTestSemantics.Test(null, Datum("a", N(1)), "intersect", Context);

        // Assert
        Assert.True(viaStore.IsNumber && viaStore.Number == 0);
        Assert.True(viaNull.IsNumber && viaNull.Number == 0);
    }

    /// <summary>
    ///     Validates how the units are combined: the union selects a datum that any tuple holds; the intersection unites the tuples of one unit and intersects the units.
    /// </summary>
    [Fact]
    public void SelectionTestSemantics_Test_UnionAndIntersectCombineUnits()
    {
        // Arrange: unit A holds [1, 3] and [6, 8], unit B holds [2, 7]
        var store = Units(("A", "R", A(N(1), N(3))), ("A", "R", A(N(6), N(8))), ("B", "R", A(N(2), N(7))));

        // Act / Assert: union: 1 is in A, 4 is in B, 9 is in none
        Assert.Equal([true, true, false], new[] { 1.0, 4, 9 }.Select(v => In(store, N(v))));

        // Assert: intersect: 1 misses B, 4 misses both tuples of A, 7 is in the second tuple of A and in B (the end of both ranges), 2.5 is in the first tuple of A and in B
        Assert.Equal([false, false, true, true], new[] { 1.0, 4, 7, 2.5 }.Select(v => In(store, N(v), "intersect")));
    }

    /// <summary>
    ///     Validates that the intersection of units counts the units a datum matches in <c>vlSelectionTest</c>, not the tuples: a datum that two tuples of one unit hold does not satisfy a unit it misses.
    ///     <c>vlSelectionIdTest</c> compares the number of tuples that hold the row with the number of units (<c>bisectRight(entries, value) - index &lt; unitIdx.size</c> in vega-selections 6.1.5), so a
    ///     row that one unit holds twice passes it, as in Vega.
    /// </summary>
    [Fact]
    public void SelectionTestSemantics_Intersect_DuplicateTuplesOfOneUnit()
    {
        // Arrange: unit A holds [1, 3] twice, unit B holds [5, 6]
        var store = Units(("A", "R", A(N(1), N(3))), ("A", "R", A(N(1), N(3))), ("B", "R", A(N(5), N(6))));
        var ids = new SelectionStore("s_store", [new SelectionEntry("A", N(1)), new SelectionEntry("A", N(1)), new SelectionEntry("B", N(2))]);
        bool Hit(double id) => ExpressionEvaluator.IsTruthy(SelectionTestSemantics.IdTest(ids, Datum("_vgsid_", N(id)), "intersect", Context));

        // Act / Assert: the field test needs a match in every unit; neither unit A's two tuples nor its tuple order change that
        Assert.Equal([false, false, false], new[] { 2.0, 5.5, 4 }.Select(v => In(store, N(v), "intersect")));
        Assert.False(In(store, N(1), "intersect"));
        Assert.Equal([false, false], new[] { 3.0, 2 }.Select(v => In(store, N(v), "intersect")));

        // Assert: the identity test is Vega's count of tuples against the number of units
        Assert.Equal([true, false], new[] { 1.0, 2 }.Select(Hit));
    }

    /// <summary>
    ///     Validates the test of a row-identity selection: the identity of the row must be in the store (in every unit when intersecting).
    /// </summary>
    [Fact]
    public void SelectionTestSemantics_IdTest_IdentityMustBeInTheStore()
    {
        // Arrange: unit A holds the rows 1 and 3, unit B the row 3
        var store = new SelectionStore("s_store", [new SelectionEntry("A", N(1)), new SelectionEntry("A", N(3)), new SelectionEntry("B", N(3))]);
        bool Hit(double id, string? operation) =>
            ExpressionEvaluator.IsTruthy(SelectionTestSemantics.IdTest(store, Datum("_vgsid_", N(id)), operation, Context));

        // Act / Assert: union: 1 and 3 are in, 2 is not; intersect: only 3 is in both units
        Assert.Equal([true, false, true], new[] { 1.0, 2, 3 }.Select(v => Hit(v, null)));
        Assert.Equal([false, false, true], new[] { 1.0, 2, 3 }.Select(v => Hit(v, "intersect")));
        Assert.False(ExpressionEvaluator.IsTruthy(SelectionTestSemantics.IdTest(null, Datum("_vgsid_", N(1)), null, Context)));
    }

    /// <summary>
    ///     Validates the resolution of a point selection: the values of a field in order without repeats, and the query <c>vlPoint</c> that lists the tuples.
    /// </summary>
    [Fact]
    public void SelectionTestSemantics_Resolve_PointValuesAndTheirQuery()
    {
        // Arrange: three tuples over a, the value 1 twice
        var store = Units(("u", "E", N(1)), ("u", "E", N(7)), ("u", "E", N(1)));

        // Act
        var union = SelectionTestSemantics.Resolve(store, "union", true, true, Context);
        var oldName = SelectionTestSemantics.Resolve(store, null, true, false, Context);

        // Assert: a is [1, 7]; the query is the or of the three tuples
        Assert.Equal("{a:[1,7],vlPoint:{or:[{a:1},{a:7},{a:1}]}}", Show(union));
        Assert.Equal("{a:[1,7],vlMulti:{or:[{a:1},{a:7},{a:1}]}}", Show(oldName));
    }

    /// <summary>
    ///     Validates the resolution of an interval selection: the range, with the ends in order, is the value of the field and there is no query.
    /// </summary>
    [Fact]
    public void SelectionTestSemantics_Resolve_IntervalRangeIsSorted()
    {
        // Arrange
        var store = OneField("R", A(N(6), N(2)));

        // Act
        var resolved = SelectionTestSemantics.Resolve(store, "union", false, true, Context);

        // Assert
        Assert.Equal("{a:[2,6]}", Show(resolved));
    }

    /// <summary>
    ///     Validates the resolution across units: ranges unite to the smallest range that holds both and intersect to the overlap (empty when they do not overlap); values unite without repeats and intersect to the common values.
    /// </summary>
    [Fact]
    public void SelectionTestSemantics_Resolve_UnitsAreUnitedOrIntersected()
    {
        // Arrange
        var ranges = Units(("A", "R", A(N(1), N(3))), ("B", "R", A(N(2), N(5))));
        var apart = Units(("A", "R", A(N(1), N(2))), ("B", "R", A(N(4), N(5))));
        var values = Units(("A", "E", A(N(1), N(2))), ("B", "E", A(N(2), N(3))));

        // Act / Assert
        Assert.Equal("{a:[1,5]}", Show(SelectionTestSemantics.Resolve(ranges, "union", false, true, Context)));
        Assert.Equal("{a:[2,3]}", Show(SelectionTestSemantics.Resolve(ranges, "intersect", false, true, Context)));
        Assert.Equal("{a:[]}", Show(SelectionTestSemantics.Resolve(apart, "intersect", false, true, Context)));
        Assert.Equal("{a:[1,2,3]}", Show(SelectionTestSemantics.Resolve(values, "union", false, true, Context)));
        Assert.Equal("{a:[2]}", Show(SelectionTestSemantics.Resolve(values, "intersect", false, true, Context)));
    }

    /// <summary>
    ///     Validates the resolution of a row-identity store and of an empty store: the identities of the units, and an empty object.
    /// </summary>
    [Fact]
    public void SelectionTestSemantics_Resolve_IdentitiesAndEmptyStore()
    {
        // Arrange: unit A holds the rows 1 and 3, unit B the row 3
        var ids = new SelectionStore("s_store", [new SelectionEntry("A", N(1)), new SelectionEntry("A", N(3)), new SelectionEntry("B", N(3))]);

        // Act / Assert
        Assert.Equal("{_vgsid_:[1,3]}", Show(SelectionTestSemantics.Resolve(ids, "union", false, true, Context)));
        Assert.Equal("{_vgsid_:[3]}", Show(SelectionTestSemantics.Resolve(ids, "intersect", false, true, Context)));
        Assert.Equal("{}", Show(SelectionTestSemantics.Resolve(null, "union", true, true, Context)));
        Assert.Equal("{}", Show(SelectionTestSemantics.Resolve(new SelectionStore("s_store", []), "union", false, true, Context)));
    }

    /// <summary>
    ///     Validates that the store shows its tuples as Vega-Lite's data set does: <c>unit</c>, the <c>fields</c> with their channel and type, and the <c>values</c>.
    /// </summary>
    [Fact]
    public void SelectionStore_ToValue_ShowsTheTuples()
    {
        // Arrange
        var store = new SelectionStore(
            "s_store",
            [
                new SelectionEntry("u", [new SelectionTupleField("a", "x", "R"), new SelectionTupleField("b", null, "E")], [A(N(1), N(2)), DataValue.FromString("k")]),
                new SelectionEntry("u", N(4)),
            ]);

        // Act
        var value = store.ToValue();

        // Assert
        Assert.Equal("[{unit:'u',fields:[{field:'a',channel:'x',type:'R'},{field:'b',type:'E'}],values:[[1,2],'k']},{unit:'u',_vgsid_:4}]", Show(value));
        Assert.Same(store.ToValue().Elements[0].ObjectValue, value.Elements[0].ObjectValue);
    }

    /// <summary>
    ///     Validates that the work is charged to the step budget: a tuple with many fields cannot run past a small budget.
    /// </summary>
    [Fact]
    public void SelectionTestSemantics_Test_WorkIsChargedToTheBudget()
    {
        // Arrange: 1000 tuples over one field against a budget of 100 steps
        var tuples = Enumerable.Range(0, 1000).Select(i => ("u", "E", N(i))).ToArray();
        var store = Units(tuples);
        var context = new ExpressionFunctionContext(Steps(100), CancellationToken.None);

        // Act / Assert
        Assert.Throws<ExpressionRuntimeException>(() => SelectionTestSemantics.Test(store, Datum("a", N(-1)), null, context));
    }
}
