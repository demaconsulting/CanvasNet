using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Unit tests for the <c>density</c> transform of the statistical transform unit: the Vega-Lite defaults (<c>value</c> and <c>density</c>, a shared resolution), the shared uniform
///     grid and the independent adaptive sample, <c>steps</c>, <c>extent</c>, <c>counts</c>, <c>cumulative</c>, Scott's bandwidth, the treatment of null and non-numeric rows, the budgets and
///     the cancellation. The densities are checked against the Gaussian closed forms (a mean of normal densities); the cumulative distribution (the approximation of West) and the sample
///     counts of the adaptive scheme against the numbers of Vega.
/// </summary>
public class StatTransformsDensityTests
{
    /// <summary>The support 1, 2, 4 and 7 of the single-group tests.</summary>
    private const string Support = "[{\"v\":1},{\"v\":2},{\"v\":4},{\"v\":7}]";

    /// <summary>Two groups: a is {1, 2, 3} and b is {10, 12}.</summary>
    private const string TwoGroups = "[{\"g\":\"a\",\"v\":1},{\"g\":\"a\",\"v\":2},{\"g\":\"a\",\"v\":3},{\"g\":\"b\",\"v\":10},{\"g\":\"b\",\"v\":12}]";

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
    ///     Gets the density of the Gaussian kernel estimate with a given bandwidth, written out from its definition.
    /// </summary>
    /// <param name="support">The support.</param>
    /// <param name="bandwidth">The bandwidth.</param>
    /// <param name="x">The point.</param>
    /// <returns>The mean of the normal densities centered on the support, divided by the bandwidth.</returns>
    private static double Density(double[] support, double bandwidth, double x) =>
        support.Sum(s => Math.Exp(-0.5 * Math.Pow((x - s) / bandwidth, 2)) / Math.Sqrt(2 * Math.PI)) / bandwidth / support.Length;

    /// <summary>
    ///     Validates the defaults of Vega-Lite and the shared grid: with the bandwidth 1, four steps and the extent 0 to 8 the output has five rows at 0, 2, 4, 6 and 8 in the fields
    ///     <c>value</c> and <c>density</c>, and every density is the mean of four normal densities (the reference gives 0.07402388031680776 at 0).
    /// </summary>
    [Fact]
    public void StatTransforms_Density_SharedGridHasTheClosedFormDensities()
    {
        // Arrange / Act
        var (table, warnings) = Run("[{\"density\":\"v\",\"bandwidth\":1,\"steps\":4,\"extent\":[0,8]}]", Support);

        // Assert
        Assert.Equal(["density", "value"], table.Fields);
        Assert.Equal([0.0, 2, 4, 6, 8], AggregateTestSupport.Numbers(table, "value"));
        var density = AggregateTestSupport.Numbers(table, "density");
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(Density([1, 2, 4, 7], 1, 2 * i), density[i], 12);
        }

        Assert.Equal(0.07402388031680776, density[0], 12);
        Assert.Empty(warnings);
    }

    /// <summary>
    ///     Validates that a density integrates to one (a Riemann sum over a wide extent) and that the shared default uses 200 steps (201 rows) over the extent of the data.
    /// </summary>
    [Fact]
    public void StatTransforms_Density_IntegratesToOneAndSharedDefaultsToTwoHundredSteps()
    {
        // Arrange / Act
        var (table, _) = Run("[{\"density\":\"v\",\"bandwidth\":1,\"extent\":[-10,20]}]", Support);
        var (data, _) = Run("[{\"density\":\"v\",\"bandwidth\":1}]", Support);

        // Assert: a trapezoid sum over [-10, 20] in 200 steps of 0.15
        var x = AggregateTestSupport.Numbers(table, "value");
        var y = AggregateTestSupport.Numbers(table, "density");
        var area = Enumerable.Range(0, x.Length - 1).Sum(i => (x[i + 1] - x[i]) * (y[i] + y[i + 1]) / 2);
        Assert.Equal(201, table.RowCount);
        Assert.Equal(1, area, 6);

        // Assert: without an extent the domain is that of the data, 1 to 7
        Assert.Equal(201, data.RowCount);
        Assert.Equal(1, AggregateTestSupport.Numbers(data, "value")[0]);
        Assert.Equal(7, AggregateTestSupport.Numbers(data, "value")[^1]);
    }

    /// <summary>
    ///     Validates the density of a symmetric support: it is symmetric about its center, so the value at the center of {1, 3} (at 2) is the maximum and the values at 2 ± d agree.
    /// </summary>
    [Fact]
    public void StatTransforms_Density_OfASymmetricSupportIsSymmetric()
    {
        // Arrange / Act
        var (table, _) = Run("[{\"density\":\"v\",\"bandwidth\":0.5,\"steps\":4,\"extent\":[0,4]}]", "[{\"v\":1},{\"v\":3}]");

        // Assert
        var y = AggregateTestSupport.Numbers(table, "density");
        Assert.Equal(y[0], y[4], 15);
        Assert.Equal(y[1], y[3], 15);
        Assert.True(y[1] > y[0] && y[1] > y[2]);
    }

    /// <summary>
    ///     Validates the cumulative distribution: it is monotone, near 0 far below the support and near 1 far above, and equals the reference (the approximation of West is not the error
    ///     function, so the closed form of the Gaussian cumulative distribution is not the expected value).
    /// </summary>
    [Fact]
    public void StatTransforms_Density_CumulativeMatchesTheReference()
    {
        // Arrange / Act
        var (table, _) = Run("[{\"density\":\"v\",\"bandwidth\":1,\"steps\":8,\"extent\":[0,8],\"cumulative\":true}]", Support);

        // Assert: the reference values at 0, 2, 4 and 8
        var y = AggregateTestSupport.Numbers(table, "density");
        Assert.Equal(0.045359264280687296, y[0], 12);
        Assert.Equal(0.34102379116707354, y[2], 12);
        Assert.Equal(0.6193124670129552, y[4], 12);
        Assert.Equal(0.9603282684597105, y[8], 12);
        Assert.Equal(y.Order(), y);
    }

    /// <summary>
    ///     Validates <c>counts</c>: the density is multiplied by the number of rows of the group (4 for the single group).
    /// </summary>
    [Fact]
    public void StatTransforms_Density_CountsMultiplyByTheGroupSize()
    {
        // Arrange / Act
        var (plain, _) = Run("[{\"density\":\"v\",\"bandwidth\":1,\"steps\":4,\"extent\":[0,8]}]", Support);
        var (counted, _) = Run("[{\"density\":\"v\",\"bandwidth\":1,\"steps\":4,\"extent\":[0,8],\"counts\":true}]", Support);

        // Assert: the reference gives 0.29609552126723104 at 0
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(4 * plain.GetValue(i, "density").Number, counted.GetValue(i, "density").Number, 12);
        }

        Assert.Equal(0.29609552126723104, counted.GetValue(0, "density").Number, 12);
    }

    /// <summary>
    ///     Validates the shared resolution of groups: both groups are sampled on the same grid over the extent of the whole column (1 to 12 in four steps), and the densities are the
    ///     closed forms of each group's own support (a at 1 is the mean of the normal densities at distances 0, 1 and 2).
    /// </summary>
    [Fact]
    public void StatTransforms_Density_SharedGroupsUseOneGridAndTheirOwnSupport()
    {
        // Arrange / Act
        var (table, _) = Run("[{\"density\":\"v\",\"groupby\":[\"g\"],\"bandwidth\":1,\"steps\":4}]", TwoGroups);

        // Assert: two groups of five rows, in the order of first appearance
        Assert.Equal(["density", "g", "value"], table.Fields);
        Assert.Equal(["a", "a", "a", "a", "a", "b", "b", "b", "b", "b"], Enumerable.Range(0, 10).Select(r => table.GetValue(r, "g").Text));
        double[] grid = [1, 3.75, 6.5, 9.25, 12];
        Assert.Equal(grid.Concat(grid), AggregateTestSupport.Numbers(table, "value"));
        var density = AggregateTestSupport.Numbers(table, "density");
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(Density([1, 2, 3], 1, grid[i]), density[i], 12);
            Assert.Equal(Density([10, 12], 1, grid[i]), density[5 + i], 12);
        }
    }

    /// <summary>
    ///     Validates the independent resolution: each group has the extent of its own values and its own sample (a: 1 to 3, b: 10 to 12), and the reference sample of a smooth density has
    ///     123 points.
    /// </summary>
    [Fact]
    public void StatTransforms_Density_IndependentGroupsUseTheirOwnExtent()
    {
        // Arrange / Act
        var (grouped, _) = Run("[{\"density\":\"v\",\"groupby\":[\"g\"],\"bandwidth\":1,\"steps\":4,\"resolve\":\"independent\"}]", TwoGroups);
        var (adaptive, _) = Run("[{\"density\":\"v\",\"bandwidth\":1,\"resolve\":\"independent\"}]", Support);

        // Assert: the grids are those of the groups, and the peak of a (at 2) is the closed form
        Assert.Equal([1.0, 1.5, 2, 2.5, 3, 10, 10.5, 11, 11.5, 12], AggregateTestSupport.Numbers(grouped, "value"));
        Assert.Equal(Density([1, 2, 3], 1, 2), grouped.GetValue(2, "density").Number, 12);
        Assert.Equal(Density([10, 12], 1, 11), grouped.GetValue(7, "density").Number, 12);
        Assert.Equal(123, adaptive.RowCount);
        Assert.Equal(1, adaptive.GetValue(0, "value").Number);
        Assert.Equal(7, adaptive.GetValue(122, "value").Number);
    }

    /// <summary>
    ///     Validates the step options: <c>minsteps</c> and <c>maxsteps</c> bound the adaptive sample (a smooth density in the minimum of four steps), and <c>steps</c> overrides both.
    /// </summary>
    [Fact]
    public void StatTransforms_Density_StepOptionsBoundTheSample()
    {
        // Arrange / Act
        var (minimum, _) = Run("[{\"density\":\"v\",\"bandwidth\":1,\"resolve\":\"independent\",\"minsteps\":4,\"maxsteps\":6,\"extent\":[0,8]}]", Support);
        var (overridden, _) = Run("[{\"density\":\"v\",\"bandwidth\":1,\"resolve\":\"independent\",\"minsteps\":4,\"maxsteps\":6,\"steps\":8,\"extent\":[0,8]}]", Support);

        // Assert: the reference has the minimum grid of five points, and nine for the exact steps
        Assert.Equal([0.0, 2, 4, 6, 8], AggregateTestSupport.Numbers(minimum, "value"));
        Assert.Equal(9, overridden.RowCount);
    }

    /// <summary>
    ///     Validates Scott's bandwidth when none is given: the densities are those of the Gaussian estimate with the bandwidth 1.06 · min(√7, (4.75 - 1.75) / 1.34) · 4^-0.2.
    /// </summary>
    [Fact]
    public void StatTransforms_Density_WithoutABandwidthUsesScottsRule()
    {
        // Arrange
        var bandwidth = 1.06 * Math.Min(Math.Sqrt(7), (4.75 - 1.75) / 1.34) * Math.Pow(4, -0.2);

        // Act
        var (table, _) = Run("[{\"density\":\"v\",\"steps\":4}]", Support);

        // Assert: the grid is the extent of the data (1 to 7) in four steps; the reference gives 0.11697547331069819 at 1
        Assert.Equal([1.0, 2.5, 4, 5.5, 7], AggregateTestSupport.Numbers(table, "value"));
        var density = AggregateTestSupport.Numbers(table, "density");
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(Density([1, 2, 4, 7], bandwidth, 1 + (1.5 * i)), density[i], 12);
        }

        Assert.Equal(0.11697547331069819, density[0], 12);
    }

    /// <summary>
    ///     Validates that every row counts: a null reads as zero (a bump at zero, and one more row in the divisor), and a text that is not a number makes the density NaN everywhere.
    /// </summary>
    [Fact]
    public void StatTransforms_Density_CountsNullRowsAsZeroAndTextAsNaN()
    {
        // Arrange / Act
        var (withNull, _) = Run("[{\"density\":\"v\",\"bandwidth\":1,\"steps\":4,\"extent\":[0,4]}]", "[{\"v\":1},{\"v\":null},{\"v\":3}]");
        var (withText, _) = Run("[{\"density\":\"v\",\"bandwidth\":1,\"steps\":4,\"extent\":[0,4]}]", "[{\"v\":1},{\"v\":\"abc\"},{\"v\":3}]");

        // Assert
        Assert.Equal(Density([1, 0, 3], 1, 0), withNull.GetValue(0, "density").Number, 12);
        Assert.Equal(Density([1, 0, 3], 1, 2), withNull.GetValue(2, "density").Number, 12);
        Assert.All(AggregateTestSupport.Numbers(withText, "density"), d => Assert.True(double.IsNaN(d)));
    }

    /// <summary>
    ///     Validates the empty table: the one group of no rows gives the grid of the steps with NaN positions and densities (as in the reference), not an error.
    /// </summary>
    [Fact]
    public void StatTransforms_Density_OfNoRowsGivesNaNRows()
    {
        // Arrange / Act
        var (table, _) = Run("[{\"density\":\"v\",\"steps\":2}]", "[]");

        // Assert: the reference has three rows with no density
        Assert.Equal(3, table.RowCount);
        Assert.All(AggregateTestSupport.Numbers(table, "density"), d => Assert.True(double.IsNaN(d)));
    }

    /// <summary>
    ///     Validates the <c>as</c> names.
    /// </summary>
    [Fact]
    public void StatTransforms_Density_AsRenamesTheOutputFields()
    {
        // Arrange / Act
        var (table, _) = Run("[{\"density\":\"v\",\"bandwidth\":1,\"steps\":2,\"as\":[\"x\",\"p\"]}]", Support);

        // Assert
        Assert.Equal(["p", "x"], table.Fields);
        Assert.Equal(3, table.RowCount);
    }

    /// <summary>
    ///     Validates the determinism: two runs give identical tables.
    /// </summary>
    [Fact]
    public void StatTransforms_Density_IsDeterministic()
    {
        // Arrange
        const string transforms = "[{\"density\":\"v\",\"groupby\":[\"g\"],\"resolve\":\"independent\"}]";

        // Act
        var (first, _) = Run(transforms, TwoGroups);
        var (second, _) = Run(transforms, TwoGroups);

        // Assert
        Assert.Equal(AggregateTestSupport.Rows(first, "g", "value", "density"), AggregateTestSupport.Rows(second, "g", "value", "density"));
    }

    /// <summary>
    ///     Validates the row budget: a number of steps beyond <see cref="VegaLiteLimits.MaxDataRows"/> is refused before any point is computed, with an error located at the transform.
    /// </summary>
    [Fact]
    public void StatTransforms_Density_StepsBeyondTheRowBudgetFailAtTheTransform()
    {
        // Arrange
        var limits = new VegaLiteLimits { MaxDataRows = 100 };

        // Act
        var error = Assert.Throws<VegaLiteRenderException>(() => Run("[{\"density\":\"v\",\"steps\":100000000}]", Support, limits));

        // Assert
        Assert.Equal("/transform/0", error.Location.Pointer);
        Assert.Contains("MaxDataRows", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the step budget: a density of a hundred rows sampled at two hundred points costs more steps than the budget of a few, and fails at the transform.
    /// </summary>
    [Fact]
    public void StatTransforms_Density_WorkBeyondTheStepBudgetFailsAtTheTransform()
    {
        // Arrange
        var rows = StatTestSupport.ValueRows(Enumerable.Range(1, 100));
        var limits = new VegaLiteLimits { MaxEvaluationSteps = 100 };

        // Act
        var error = Assert.Throws<VegaLiteRenderException>(() => Run("[{\"density\":\"v\"}]", rows, limits));

        // Assert
        Assert.Equal("/transform/0", error.Location.Pointer);
        Assert.Contains("MaxEvaluationSteps", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the cancellation: a token that is already canceled stops the density.
    /// </summary>
    [Fact]
    public void StatTransforms_Density_StopsWhenCanceled()
    {
        // Arrange
        using var source = new CancellationTokenSource();
        var (table, context, _, tree) = AggregateTestSupport.Prepare(AggregateTestSupport.Spec(Support, "[{\"density\":\"v\"}]"), null, source.Token);
        source.Cancel();
        var entry = tree.Root["transform"]!.Items[0];

        // Act / Assert
        Assert.ThrowsAny<OperationCanceledException>(() => StatTransforms.Apply(table, entry, SpecParser.FindTransformOperation(entry)!, context));
    }
}
