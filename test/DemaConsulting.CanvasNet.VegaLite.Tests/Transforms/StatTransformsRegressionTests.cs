using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Unit tests for the <c>regression</c> and <c>loess</c> transforms of the statistical transform unit: the output shape and the defaults of Vega-Lite (<c>as</c>), the line
///     and curve sampling, <c>params</c>, the groups of Vega's <c>partition</c> (the text of the group values), the skipped groups and the warnings, the log extent rule, the literal
///     names of the output fields, the row budget and the determinism. The expected numbers are the closed forms of the fits (a line, a cubic) with the reference numbers of Vega as
///     a cross-check where the doc comment says so.
/// </summary>
public class StatTransformsRegressionTests
{
    /// <summary>The rows <c>y = 2x + 1</c> for x = 0 to 5.</summary>
    private const string LineRows = "[{\"x\":0,\"y\":1},{\"x\":1,\"y\":3},{\"x\":2,\"y\":5},{\"x\":3,\"y\":7},{\"x\":4,\"y\":9},{\"x\":5,\"y\":11}]";

    /// <summary>The rows of three groups: a on y = 1 + 2x, b on y = 5 - 2x and c with only two rows.</summary>
    private const string GroupRows =
        "[{\"g\":\"a\",\"x\":0,\"y\":1},{\"g\":\"a\",\"x\":1,\"y\":3},{\"g\":\"b\",\"x\":0,\"y\":5},{\"g\":\"b\",\"x\":2,\"y\":1},{\"g\":\"a\",\"x\":2,\"y\":5},{\"g\":\"b\",\"x\":1,\"y\":3},{\"g\":\"c\",\"x\":1,\"y\":1},{\"g\":\"c\",\"x\":2,\"y\":2}]";

    /// <summary>
    ///     Runs a transform array over inline rows the way a render does.
    /// </summary>
    /// <param name="transforms">The JSON array of transforms.</param>
    /// <param name="rows">The JSON array of rows.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <returns>The table and the warnings of the transforms.</returns>
    private static (DataTable Table, List<FeatureReport> Warnings) Run(string transforms, string rows, VegaLiteLimits? limits = null)
    {
        var (table, context, _, tree) = AggregateTestSupport.Prepare(AggregateTestSupport.Spec(rows, transforms), limits);
        var result = RowTransforms.Apply(table, tree.Root["transform"], context);
        return (result, context.Warnings);
    }

    /// <summary>
    ///     Asserts that a number agrees with an expected one to a relative tolerance.
    /// </summary>
    /// <param name="expected">The expected number.</param>
    /// <param name="actual">The actual number.</param>
    private static void AssertNear(double expected, double actual) =>
        Assert.True(Math.Abs(expected - actual) <= 1e-12 * Math.Max(1, Math.Abs(expected)), $"expected {expected:R}, actual {actual:R}");

    /// <summary>
    ///     Validates the default linear regression: the table is replaced by the two end points of the line at the extent of x, in the fields of <c>on</c> and the predicted field
    ///     (Vega-Lite's default <c>as</c>), and the rows of the input are gone.
    /// </summary>
    [Fact]
    public void StatTransforms_Regression_LinearGivesTheEndPointsOfTheLine()
    {
        // Arrange / Act
        var (table, warnings) = Run("[{\"regression\":\"y\",\"on\":\"x\"}]", LineRows);

        // Assert: y = 2x + 1 at x = 0 and 5
        Assert.Equal(["x", "y"], table.Fields);
        Assert.Equal(["0|1", "5|11"], AggregateTestSupport.Rows(table, "x", "y"));
        Assert.Empty(warnings);
    }

    /// <summary>
    ///     Validates the <c>as</c> names: both given, only the first given (the second is the predicted field), and that a name with a dot is a literal field name.
    /// </summary>
    [Fact]
    public void StatTransforms_Regression_AsRenamesTheOutputFieldsAndNamesAreLiteral()
    {
        // Arrange / Act
        var (both, _) = Run("[{\"regression\":\"y\",\"on\":\"x\",\"as\":[\"px\",\"py\"]}]", LineRows);
        var (first, _) = Run("[{\"regression\":\"y\",\"on\":\"x\",\"as\":[\"a.b\"]}]", LineRows);

        // Assert
        Assert.Equal(["px", "py"], both.Fields);
        Assert.Equal(["a\\.b", "y"], first.Fields);
        Assert.Equal(["0|1", "5|11"], AggregateTestSupport.Rows(first, "a\\.b", "y"));
    }

    /// <summary>
    ///     Validates that a field with a dot in its path (a nested value) is read as a path and that the default output name is the literal text of the path, as in Vega-Lite.
    /// </summary>
    [Fact]
    public void StatTransforms_Regression_ReadsNestedFieldsAndNamesTheOutputLiterally()
    {
        // Arrange
        const string rows = "[{\"p\":{\"x\":0},\"q\":1},{\"p\":{\"x\":1},\"q\":3},{\"p\":{\"x\":2},\"q\":5}]";

        // Act
        var (table, _) = Run("[{\"regression\":\"q\",\"on\":\"p.x\"}]", rows);

        // Assert: the output field is named p.x (a dot in a literal name is escaped in the canonical path); the line through (0, 1) and (2, 5)
        Assert.Equal(["p\\.x", "q"], table.Fields);
        Assert.Equal([0.0, 2.0], AggregateTestSupport.Numbers(table, "p\\.x"));
        AssertNear(1, AggregateTestSupport.Numbers(table, "q")[0]);
        AssertNear(5, AggregateTestSupport.Numbers(table, "q")[1]);
    }

    /// <summary>
    ///     Validates the <c>extent</c> option: the line is drawn over the given domain, not over the extent of the data.
    /// </summary>
    [Fact]
    public void StatTransforms_Regression_ExtentSetsTheDomainOfTheLine()
    {
        // Arrange / Act
        var (table, _) = Run("[{\"regression\":\"y\",\"on\":\"x\",\"extent\":[0,10]}]", LineRows);

        // Assert: y = 2x + 1 at 0 and 10
        Assert.Equal(["0|1", "10|21"], AggregateTestSupport.Rows(table, "x", "y"));
    }

    /// <summary>
    ///     Validates that an <c>extent</c> next to a <c>regression</c> is an option of the regression and not an extent transform (the Vega-Lite rule for an entry that has both properties),
    ///     whatever the order of the properties.
    /// </summary>
    [Fact]
    public void StatTransforms_Regression_ExtentBeforeTheRegressionIsStillTheRegressionsOption()
    {
        // Arrange / Act
        var (table, _) = Run("[{\"extent\":[0,10],\"regression\":\"y\",\"on\":\"x\"}]", LineRows);

        // Assert
        Assert.Equal(["0|1", "10|21"], AggregateTestSupport.Rows(table, "x", "y"));
    }

    /// <summary>
    ///     Validates the parameters of the fit: <c>params: true</c> gives one row with the coefficients (as an array), the coefficient of determination and, without <c>groupby</c>, no keys.
    /// </summary>
    [Fact]
    public void StatTransforms_Regression_ParamsGiveTheCoefficientsAndRSquared()
    {
        // Arrange / Act
        var (table, _) = Run("[{\"regression\":\"y\",\"on\":\"x\",\"params\":true}]", LineRows);

        // Assert
        Assert.Equal(["coef", "keys", "rSquared"], table.Fields);
        Assert.Equal(1, table.RowCount);
        var coefficients = table.GetValue(0, "coef").Elements.ToArray();
        Assert.Equal(2, coefficients.Length);
        AssertNear(1, coefficients[0].Number);
        AssertNear(2, coefficients[1].Number);
        AssertNear(1, table.GetValue(0, "rSquared").Number);
        Assert.True(table.GetValue(0, "keys").IsNull);
    }

    /// <summary>
    ///     Validates the groups: one trend line per group in the order of first appearance, with the group field first of the row, from the closed forms of the three fits
    ///     (a: 1 + 2x, b: 5 - 2x, c: x through two points); the reference gives 0.9999999999999996 for the line of a at 0.
    /// </summary>
    [Fact]
    public void StatTransforms_Regression_GroupbyFitsEveryGroupInOrderOfFirstAppearance()
    {
        // Arrange / Act
        var (table, warnings) = Run("[{\"regression\":\"y\",\"on\":\"x\",\"groupby\":[\"g\"]}]", GroupRows);

        // Assert
        Assert.Equal(["g", "x", "y"], table.Fields);
        Assert.Equal(["a", "a", "b", "b", "c", "c"], Enumerable.Range(0, table.RowCount).Select(r => table.GetValue(r, "g").Text));
        Assert.Equal([0.0, 2, 0, 2, 1, 2], AggregateTestSupport.Numbers(table, "x"));
        var y = AggregateTestSupport.Numbers(table, "y");
        double[] expected = [1, 5, 5, 1, 1, 2];
        for (var i = 0; i < expected.Length; i++)
        {
            AssertNear(expected[i], y[i]);
        }

        Assert.Empty(warnings);
    }

    /// <summary>
    ///     Validates the parameters of grouped fits: the keys are the group values of the first row of each group and the coefficients are the closed forms (a: [1, 2], b: [5, -2], c: [0, 1]).
    /// </summary>
    [Fact]
    public void StatTransforms_Regression_GroupedParamsCarryTheKeys()
    {
        // Arrange / Act
        var (table, _) = Run("[{\"regression\":\"y\",\"on\":\"x\",\"groupby\":[\"g\"],\"params\":true}]", GroupRows);

        // Assert
        Assert.Equal(3, table.RowCount);
        Assert.Equal(["a", "b", "c"], Enumerable.Range(0, 3).Select(r => table.GetValue(r, "keys").Elements[0].Text));
        double[][] expected = [[1, 2], [5, -2], [0, 1]];
        for (var r = 0; r < 3; r++)
        {
            var coefficients = table.GetValue(r, "coef").Elements.ToArray();
            AssertNear(expected[r][0], coefficients[0].Number);
            AssertNear(expected[r][1], coefficients[1].Number);
        }
    }

    /// <summary>
    ///     Validates Vega's partition: the key of a group is the JavaScript text of the group values, so the number 1 and the text "1" are one group (whose keys are those of its first row), and
    ///     null, a missing field and an undefined value are one group; the reference gives the coefficients [0, 1] and [2, 1].
    /// </summary>
    [Fact]
    public void StatTransforms_Regression_GroupKeysAreTheTextOfTheValues()
    {
        // Arrange: g is 1, "1", 1 (x = 0, 1, 2 on y = x), then null, missing and null (x = 1, 2, 3, 5 on y = x + 2)
        const string rows =
            "[{\"g\":1,\"x\":0,\"y\":0},{\"g\":\"1\",\"x\":1,\"y\":1},{\"g\":1,\"x\":2,\"y\":2},{\"g\":null,\"x\":1,\"y\":3},{\"x\":2,\"y\":4},{\"x\":3,\"y\":5},{\"g\":null,\"x\":5,\"y\":7}]";

        // Act
        var (table, _) = Run("[{\"regression\":\"y\",\"on\":\"x\",\"groupby\":[\"g\"],\"params\":true}]", rows);

        // Assert: two groups, the first keyed by the number 1 of its first row, the second by null
        Assert.Equal(2, table.RowCount);
        var first = table.GetValue(0, "keys").Elements[0];
        Assert.True(first.IsNumber);
        Assert.Equal(1, first.Number);
        Assert.True(table.GetValue(1, "keys").Elements[0].IsNull);
        AssertNear(0, table.GetValue(0, "coef").Elements[0].Number);
        AssertNear(1, table.GetValue(0, "coef").Elements[1].Number);
        AssertNear(2, table.GetValue(1, "coef").Elements[0].Number);
        AssertNear(1, table.GetValue(1, "coef").Elements[1].Number);
    }

    /// <summary>
    ///     Validates that a group with no more rows than the model has parameters is skipped with a warning (one row is not enough for a line), that the other groups are kept, and that a
    ///     quadratic needs three rows.
    /// </summary>
    [Fact]
    public void StatTransforms_Regression_GroupsWithTooFewRowsAreSkippedWithAWarning()
    {
        // Arrange: group a has one row, group b has two
        const string rows = "[{\"g\":\"a\",\"x\":1,\"y\":1},{\"g\":\"b\",\"x\":1,\"y\":2},{\"g\":\"b\",\"x\":2,\"y\":3}]";

        // Act
        var (line, lineWarnings) = Run("[{\"regression\":\"y\",\"on\":\"x\",\"groupby\":[\"g\"]}]", rows);
        var (quad, quadWarnings) = Run("[{\"regression\":\"y\",\"on\":\"x\",\"method\":\"quad\",\"groupby\":[\"g\"]}]", rows);

        // Assert
        Assert.Equal(["b", "b"], Enumerable.Range(0, line.RowCount).Select(r => line.GetValue(r, "g").Text));
        var warning = Assert.Single(lineWarnings);
        Assert.Equal(("vl-transform-runtime", "/transform/0", FeatureReportKind.Warning), (warning.Id, warning.Location.Pointer, warning.Kind));
        Assert.Contains("skipped 1 group", warning.Message, StringComparison.Ordinal);
        Assert.Equal(0, quad.RowCount);
        Assert.Contains("skipped 2 groups", Assert.Single(quadWarnings).Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the rows of a group count whether or not they are valid points: three rows, two of them without a number, are enough for a line (the reference does not skip it),
    ///     and the fit uses the one valid pair only.
    /// </summary>
    [Fact]
    public void StatTransforms_Regression_RowsCountEvenWhenTheyAreNotValidPoints()
    {
        // Arrange
        const string rows = "[{\"x\":1,\"y\":2},{\"x\":null,\"y\":3},{\"x\":\"abc\",\"y\":4}]";

        // Act
        var (table, warnings) = Run("[{\"regression\":\"y\",\"on\":\"x\"}]", rows);

        // Assert: not skipped; with one valid point the slope is zero and the line is the mean at the extent of x (1 and "abc" compare as texts)
        Assert.Empty(warnings);
        Assert.Equal(2, table.RowCount);
    }

    /// <summary>
    ///     Validates that a table without rows has one group of no rows, which is skipped with the warning (as in Vega), and that the output is an empty table.
    /// </summary>
    [Fact]
    public void StatTransforms_Regression_EmptyInputGivesAnEmptyTable()
    {
        // Arrange / Act
        var (table, warnings) = Run("[{\"regression\":\"y\",\"on\":\"x\"}]", "[]");

        // Assert
        Assert.Equal(0, table.RowCount);
        Assert.Single(warnings);
    }

    /// <summary>
    ///     Validates that a group whose x has no value at all has no extent: the two end points have no x and a NaN y (the reference throws an error here; the renderer keeps going and the
    ///     points are not drawn).
    /// </summary>
    [Fact]
    public void StatTransforms_Regression_AGroupWithoutXValuesGivesPointsWithoutPosition()
    {
        // Arrange
        const string rows = "[{\"x\":null,\"y\":1},{\"x\":null,\"y\":2},{\"x\":null,\"y\":3}]";

        // Act
        var (table, _) = Run("[{\"regression\":\"y\",\"on\":\"x\"}]", rows);

        // Assert
        Assert.Equal(2, table.RowCount);
        Assert.True(table.GetValue(0, "x").IsNull);
        Assert.True(double.IsNaN(table.GetValue(0, "y").Number));
    }

    /// <summary>
    ///     Validates the curve of a polynomial: the cubic <c>1 - x + 0.5x² + 0.25x³</c> is sampled adaptively over the extent of x with the 124 points of the reference, and every sampled
    ///     point is on the cubic.
    /// </summary>
    [Fact]
    public void StatTransforms_Regression_PolySamplesTheCurveAdaptively()
    {
        // Arrange: nine points of the cubic over x = -4..4
        var rows = StatTestSupport.XyRows(Enumerable.Range(-4, 9), x => 1 - x + (0.5 * x * x) + (0.25 * x * x * x));

        // Act
        var (table, _) = Run("[{\"regression\":\"y\",\"on\":\"x\",\"method\":\"poly\",\"order\":3}]", rows);

        // Assert
        Assert.Equal(124, table.RowCount);
        var x = AggregateTestSupport.Numbers(table, "x");
        var y = AggregateTestSupport.Numbers(table, "y");
        Assert.Equal(-4, x[0]);
        Assert.Equal(4, x[^1]);
        for (var i = 0; i < x.Length; i++)
        {
            Assert.True(Math.Abs(1 - x[i] + (0.5 * x[i] * x[i]) + (0.25 * x[i] * x[i] * x[i]) - y[i]) < 1e-9);
        }
    }

    /// <summary>
    ///     Validates the exponential, power and logarithmic methods through the transform: the sampled curve passes through the data, which the models reproduce exactly (an exponential, a power
    ///     and a logarithm), at the first and last sample.
    /// </summary>
    [Fact]
    public void StatTransforms_Regression_CurveMethodsPassThroughTheirData()
    {
        // Arrange
        var exp = StatTestSupport.XyRows(Enumerable.Range(0, 7), x => 3 * Math.Exp(0.5 * x));
        var pow = StatTestSupport.XyRows(Enumerable.Range(1, 7), x => 2.0 * x * x * x);
        var log = StatTestSupport.XyRows(Enumerable.Range(1, 7), x => 2 + (3 * Math.Log(x)));

        // Act
        var (expTable, _) = Run("[{\"regression\":\"y\",\"on\":\"x\",\"method\":\"exp\"}]", exp);
        var (powTable, _) = Run("[{\"regression\":\"y\",\"on\":\"x\",\"method\":\"pow\"}]", pow);
        var (logTable, _) = Run("[{\"regression\":\"y\",\"on\":\"x\",\"method\":\"log\"}]", log);

        // Assert: the reference sample of the exponential has 45 points
        Assert.Equal(45, expTable.RowCount);
        AssertNear(3, AggregateTestSupport.Numbers(expTable, "y")[0]);
        AssertNear(3 * Math.Exp(3), AggregateTestSupport.Numbers(expTable, "y")[^1]);
        AssertNear(2, AggregateTestSupport.Numbers(powTable, "y")[0]);
        AssertNear(2 * 343, AggregateTestSupport.Numbers(powTable, "y")[^1]);
        AssertNear(2, AggregateTestSupport.Numbers(logTable, "y")[0]);
        AssertNear(2 + (3 * Math.Log(7)), AggregateTestSupport.Numbers(logTable, "y")[^1]);
    }

    /// <summary>
    ///     Validates that the extent of a log regression must be positive: an extent that starts at zero is ignored with a warning and the extent of the data is used, as in Vega.
    /// </summary>
    [Fact]
    public void StatTransforms_Regression_LogIgnoresANonPositiveExtent()
    {
        // Arrange
        var rows = StatTestSupport.XyRows(Enumerable.Range(1, 7), x => 2 + (3 * Math.Log(x)));

        // Act
        var (table, warnings) = Run("[{\"regression\":\"y\",\"on\":\"x\",\"method\":\"log\",\"extent\":[0,9]}]", rows);

        // Assert: the curve runs over [1, 7], the data's extent
        Assert.Equal(1, AggregateTestSupport.Numbers(table, "x")[0]);
        Assert.Equal(7, AggregateTestSupport.Numbers(table, "x")[^1]);
        Assert.Contains("log regression", Assert.Single(warnings).Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the loess transform: of exactly linear data it is the line at the original x values, in the fields of <c>on</c> and the smoothed field, with the bandwidth given.
    /// </summary>
    [Fact]
    public void StatTransforms_Loess_OfALineIsTheLine()
    {
        // Arrange
        var rows = StatTestSupport.XyRows(Enumerable.Range(1, 10), x => 2 * x);

        // Act
        var (table, _) = Run("[{\"loess\":\"y\",\"on\":\"x\",\"bandwidth\":0.5}]", rows);

        // Assert
        Assert.Equal(["x", "y"], table.Fields);
        Assert.Equal(10, table.RowCount);
        var y = AggregateTestSupport.Numbers(table, "y");
        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(2 * (i + 1), y[i], 9);
        }
    }

    /// <summary>
    ///     Validates the loess groups and names: the output is sorted by x within each group in the order of first appearance, the group field comes with it, and <c>as</c> renames the outputs.
    /// </summary>
    [Fact]
    public void StatTransforms_Loess_GroupsAreSortedByXAndRenamed()
    {
        // Arrange: the rows of group a are not in order of x
        const string rows = "[{\"g\":\"a\",\"x\":3,\"y\":4},{\"g\":\"b\",\"x\":1,\"y\":1},{\"g\":\"a\",\"x\":1,\"y\":2},{\"g\":\"a\",\"x\":2,\"y\":3.5},{\"g\":\"b\",\"x\":2,\"y\":2},{\"g\":\"b\",\"x\":3,\"y\":5}]";

        // Act
        var (table, _) = Run("[{\"loess\":\"y\",\"on\":\"x\",\"groupby\":[\"g\"],\"bandwidth\":0.9,\"as\":[\"px\",\"py\"]}]", rows);

        // Assert: the reference reproduces the three points of each group
        Assert.Equal(["g", "px", "py"], table.Fields);
        Assert.Equal(["a|1|2", "a|2|3.5", "a|3|4", "b|1|1", "b|2|2", "b|3|5"], AggregateTestSupport.Rows(table, "g", "px", "py"));
    }

    /// <summary>
    ///     Validates that a missing or zero bandwidth is Vega's default 0.3 and that a group of one point has a NaN value without an error.
    /// </summary>
    [Fact]
    public void StatTransforms_Loess_DefaultBandwidthAndASinglePoint()
    {
        // Arrange
        var rows = StatTestSupport.XyRows(Enumerable.Range(1, 10), x => x * x);

        // Act
        var (omitted, _) = Run("[{\"loess\":\"y\",\"on\":\"x\"}]", rows);
        var (zero, _) = Run("[{\"loess\":\"y\",\"on\":\"x\",\"bandwidth\":0}]", rows);
        var (explicitDefault, _) = Run("[{\"loess\":\"y\",\"on\":\"x\",\"bandwidth\":0.3}]", rows);
        var (single, _) = Run("[{\"loess\":\"y\",\"on\":\"x\"}]", "[{\"x\":1,\"y\":2}]");

        // Assert
        Assert.Equal(AggregateTestSupport.Numbers(explicitDefault, "y"), AggregateTestSupport.Numbers(omitted, "y"));
        Assert.Equal(AggregateTestSupport.Numbers(explicitDefault, "y"), AggregateTestSupport.Numbers(zero, "y"));
        Assert.Equal(1, single.RowCount);
        Assert.True(double.IsNaN(single.GetValue(0, "y").Number));
    }

    /// <summary>
    ///     Validates the determinism: the same transform over the same rows gives identical tables on every run, for the adaptive curve and the loess.
    /// </summary>
    [Fact]
    public void StatTransforms_Regression_IsDeterministic()
    {
        // Arrange
        var rows = StatTestSupport.XyRows(Enumerable.Range(1, 12), x => Math.Sin(x) + x);
        const string transforms = "[{\"regression\":\"y\",\"on\":\"x\",\"method\":\"poly\",\"order\":4}]";

        // Act
        var (first, _) = Run(transforms, rows);
        var (second, _) = Run(transforms, rows);
        var (loessFirst, _) = Run("[{\"loess\":\"y\",\"on\":\"x\"}]", rows);
        var (loessSecond, _) = Run("[{\"loess\":\"y\",\"on\":\"x\"}]", rows);

        // Assert
        Assert.Equal(AggregateTestSupport.Rows(first, "x", "y"), AggregateTestSupport.Rows(second, "x", "y"));
        Assert.Equal(AggregateTestSupport.Rows(loessFirst, "x", "y"), AggregateTestSupport.Rows(loessSecond, "x", "y"));
    }

    /// <summary>
    ///     Validates the row budget: a sampled curve that would have more rows than <see cref="VegaLiteLimits.MaxDataRows"/> fails with an error located at the transform, before the rows exist.
    /// </summary>
    [Fact]
    public void StatTransforms_Regression_CurveBeyondTheRowBudgetFailsAtTheTransform()
    {
        // Arrange: six input rows and a budget of twenty; the quadratic curve needs more than twenty points
        var limits = new VegaLiteLimits { MaxDataRows = 20 };

        // Act
        var error = Assert.Throws<VegaLiteRenderException>(() => Run("[{\"regression\":\"y\",\"on\":\"x\",\"method\":\"quad\"}]", LineRows, limits));

        // Assert
        Assert.Equal("/transform/0", error.Location.Pointer);
        Assert.Contains("MaxDataRows", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the field budget: an output with more fields than <see cref="VegaLiteLimits.MaxFieldCount"/> (two group fields and two outputs against a limit of three) fails at the transform.
    /// </summary>
    [Fact]
    public void StatTransforms_Regression_OutputBeyondTheFieldLimitFailsAtTheTransform()
    {
        // Arrange
        var limits = new VegaLiteLimits { MaxFieldCount = 3 };

        // Act
        var error = Assert.Throws<VegaLiteRenderException>(() => Run("[{\"regression\":\"y\",\"on\":\"x\",\"groupby\":[\"g\",\"h\"]}]", GroupRows, limits));

        // Assert
        Assert.Equal("/transform/0", error.Location.Pointer);
        Assert.Contains("MaxFieldCount", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the step budget: a loess whose window work exceeds <see cref="VegaLiteLimits.MaxEvaluationSteps"/> fails at the transform before the fit starts.
    /// </summary>
    [Fact]
    public void StatTransforms_Loess_WorkBeyondTheStepBudgetFailsAtTheTransform()
    {
        // Arrange: a hundred rows and a bandwidth of 1 (the window is the whole group, three times); the budget is a handful of steps
        var rows = StatTestSupport.XyRows(Enumerable.Range(1, 100), x => x % 7);
        var limits = new VegaLiteLimits { MaxEvaluationSteps = 100 };

        // Act
        var error = Assert.Throws<VegaLiteRenderException>(() => Run("[{\"loess\":\"y\",\"on\":\"x\",\"bandwidth\":1}]", rows, limits));

        // Assert
        Assert.Equal("/transform/0", error.Location.Pointer);
        Assert.Contains("MaxEvaluationSteps", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the cancellation: a token that is already canceled stops the regression and the loess with an <see cref="OperationCanceledException"/>.
    /// </summary>
    /// <param name="transform">The transform entry.</param>
    [Theory]
    [InlineData("{\"regression\":\"y\",\"on\":\"x\"}")]
    [InlineData("{\"loess\":\"y\",\"on\":\"x\"}")]
    public void StatTransforms_RegressionAndLoess_StopWhenCanceled(string transform)
    {
        // Arrange
        using var source = new CancellationTokenSource();
        var (table, context, _, tree) = AggregateTestSupport.Prepare(AggregateTestSupport.Spec(LineRows, "[" + transform + "]"), null, source.Token);
        source.Cancel();
        var entry = tree.Root["transform"]!.Items[0];

        // Act / Assert
        Assert.ThrowsAny<OperationCanceledException>(() => StatTransforms.Apply(table, entry, SpecParser.FindTransformOperation(entry)!, context));
    }
}
