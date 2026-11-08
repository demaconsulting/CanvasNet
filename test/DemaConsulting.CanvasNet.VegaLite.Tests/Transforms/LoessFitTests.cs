using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Unit tests for the loess fit: exact reproduction of a line (a local linear fit of linear data is the data), the JavaScript edge cases of the reference (a window that reaches
///     past the data, merged repeated x, unsorted input, invalid pairs), and, with a tolerance, the numbers of vega-statistics' <c>regressionLoess</c> for noisy data (the robustness
///     iterations have no closed form, so those values are an oracle cross-check).
/// </summary>
public class LoessFitTests
{
    /// <summary>The relative tolerance of the cross-check against the reference numbers.</summary>
    private const double Tolerance = 1e-12;

    /// <summary>
    ///     Makes the values of a sequence of numbers.
    /// </summary>
    /// <param name="values">The numbers.</param>
    /// <returns>The values.</returns>
    private static DataValue[] Values(params double[] values) => [.. values.Select(DataValue.FromNumber)];

    /// <summary>
    ///     Asserts that fitted points agree with the expected y values to the tolerance.
    /// </summary>
    /// <param name="expectedY">The expected y of each point.</param>
    /// <param name="points">The fitted points.</param>
    private static void AssertY(double[] expectedY, List<(double X, double Y)> points)
    {
        Assert.Equal(expectedY.Length, points.Count);
        for (var i = 0; i < expectedY.Length; i++)
        {
            Assert.True(Math.Abs(expectedY[i] - points[i].Y) <= Tolerance * Math.Max(1, Math.Abs(expectedY[i])), $"point {i}: expected {expectedY[i]:R}, actual {points[i].Y:R}");
        }
    }

    /// <summary>
    ///     Validates that a loess of exactly linear data is that line: every local fit is exact, so the output is <c>y = 2x</c> at the original x values.
    /// </summary>
    [Fact]
    public void LoessFit_Fit_OfALineIsTheLine()
    {
        // Arrange
        var x = Values(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
        var y = Values(2, 4, 6, 8, 10, 12, 14, 16, 18, 20);

        // Act
        var points = LoessFit.Fit(x, y, 0.5);

        // Assert
        Assert.Equal(10, points.Count);
        for (var i = 0; i < points.Count; i++)
        {
            Assert.Equal(i + 1, points[i].X, 12);
            Assert.Equal(2 * (i + 1), points[i].Y, 9);
        }
    }

    /// <summary>
    ///     Validates the fit of noisy data with the default bandwidth of 0.3 (a window of three points) against the reference, and the smoother fit with the bandwidth 0.6 (a window of six points).
    /// </summary>
    [Fact]
    public void LoessFit_Fit_OfNoisyDataMatchesTheReference()
    {
        // Arrange
        var x = Values(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
        var y = Values(2.5, 3.1, 6.9, 7.2, 11, 11.8, 15.5, 15.9, 19.4, 21);

        // Act
        var narrow = LoessFit.Fit(x, y, 0.3);
        var wide = LoessFit.Fit(x, y, 0.6);

        // Assert: the reference values (a three-point window follows the data closely, a six-point window smooths it)
        AssertY([2.4999999999999982, 3.0999999999999996, 6.9, 7.2, 11, 11.8, 15.5, 15.9, 19.4, 21.000000000000007], narrow);
        AssertY(
            [2.1077094913861316, 4.051509246800625, 5.981457454402157, 8.152397515814757, 10.272416115566498, 12.492652264170893, 14.593028250514738, 16.797443629027363, 18.93594955022332, 21.09690261865225],
            wide);
    }

    /// <summary>
    ///     Validates the robustness iterations: an outlier at x = 5 (y = 60 instead of 11) is down-weighted, so the fit stays near the trend (the reference gives 9.97 at x = 5, not an
    ///     average pulled toward 60).
    /// </summary>
    [Fact]
    public void LoessFit_Fit_DownWeightsAnOutlier()
    {
        // Arrange
        var x = Values(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
        var y = Values(2.5, 3.1, 6.9, 7.2, 60, 11.8, 15.5, 15.9, 19.4, 21);

        // Act
        var points = LoessFit.Fit(x, y, 0.6);

        // Assert: the reference values
        AssertY([2.1917008033776177, 4.040364386849248, 5.833961348108602, 7.808436249316886, 9.970670587031965, 12.15706283256926, 14.496415331643925, 16.79960807070414, 18.9371892668415, 21.096677263032085], points);
        Assert.True(points[4].Y < 15);
    }

    /// <summary>
    ///     Validates the JavaScript semantics of a group of one point: the two-point window reaches past the data (<c>undefined</c> in JavaScript, NaN here), so the value is NaN and there
    ///     is no exception; two points are reproduced and three points are the reference's.
    /// </summary>
    [Fact]
    public void LoessFit_Fit_OfOneTwoAndThreePointsFollowsTheReference()
    {
        // Arrange / Act
        var one = LoessFit.Fit(Values(1), Values(2), 0.3);
        var two = LoessFit.Fit(Values(1, 2), Values(2, 5), 0.3);
        var three = LoessFit.Fit(Values(1, 2, 3), Values(2, 5, 4), 0.3);

        // Assert
        Assert.Single(one);
        Assert.Equal(1, one[0].X);
        Assert.True(double.IsNaN(one[0].Y));
        AssertY([2, 5], two);
        AssertY([2, 5, 4], three);
    }

    /// <summary>
    ///     Validates that the points that share an x are merged into one output point by the online update of the reference (the first repeat replaces the fitted value, later repeats
    ///     average), and that the output has one point for each distinct x.
    /// </summary>
    [Fact]
    public void LoessFit_Fit_MergesRepeatedXAsTheReferenceDoes()
    {
        // Arrange
        var x = Values(1, 1, 1, 2, 3, 3, 4, 5);
        var y = Values(2, 4, 9, 5, 4, 6, 7, 5);

        // Act
        var points = LoessFit.Fit(x, y, 0.8);

        // Assert: five distinct x, with the reference values
        Assert.Equal([1.0, 2, 3, 4, 5], points.Select(p => p.X));
        AssertY([3.872760745121873, 5, 5.428724661816015, 5.57188594958875, 5.667107411069027], points);
    }

    /// <summary>
    ///     Validates that the rows may come in any order (they are sorted stably by x), that rows with a null or a text that is not a number are ignored, and that no valid pair gives no point.
    /// </summary>
    [Fact]
    public void LoessFit_Fit_SortsTheRowsAndIgnoresInvalidPairs()
    {
        // Arrange: the line y = 2x, shuffled, with invalid pairs mixed in
        DataValue[] x = [DataValue.FromNumber(3), default, DataValue.FromNumber(1), DataValue.FromNumber(4), DataValue.FromString("n/a"), DataValue.FromNumber(2)];
        DataValue[] y = [DataValue.FromNumber(6), DataValue.FromNumber(99), DataValue.FromNumber(2), DataValue.FromNumber(8), DataValue.FromNumber(5), DataValue.FromNumber(4)];

        // Act
        var points = LoessFit.Fit(x, y, 0.9);
        var none = LoessFit.Fit([default, default], Values(1, 2), 0.3);

        // Assert
        Assert.Equal([1.0, 2, 3, 4], points.Select(p => Math.Round(p.X)));
        for (var i = 0; i < points.Count; i++)
        {
            Assert.Equal(2 * (i + 1), points[i].Y, 9);
        }

        Assert.Empty(none);
    }

    /// <summary>
    ///     Validates that the work estimate grows with the points and the window, and that the progress callback is called once for every fitted point and every pass.
    /// </summary>
    [Fact]
    public void LoessFit_Fit_ReportsProgressAndEstimatesTheCost()
    {
        // Arrange
        var calls = 0;
        var x = Values(1, 2, 3, 4, 5);
        var y = Values(1, 3, 2, 5, 4);

        // Act
        LoessFit.Fit(x, y, 0.8, _ => calls++);

        // Assert: five points in three passes (a window of four points does not fit the data exactly, so the iteration does not stop early)
        Assert.Equal(15, calls);
        Assert.True(LoessFit.Cost(1000, 0.5) > LoessFit.Cost(1000, 0.1));
        Assert.True(LoessFit.Cost(2000, 0.3) > LoessFit.Cost(1000, 0.3));
    }

    /// <summary>
    ///     Validates the calibration of the work estimate (defect B5): every weight evaluation counts three work elements (so that the default budget stops a loess in about ten seconds), the sort counts sixteen per
    ///     point, and a bandwidth that no integer window can hold is a window of every point on every runtime. A loess of 20,000 points with a bandwidth of 1 costs more than the whole default budget of
    ///     50,000,000 steps of 64 elements, as it ran for eleven seconds before.
    /// </summary>
    [Fact]
    public void LoessFit_Cost_CountsEachWeightEvaluationAsThreeElements()
    {
        // Arrange / Act
        var cost = LoessFit.Cost(1000, 1);
        var huge = LoessFit.Cost(1000, 1e300);
        var big = LoessFit.Cost(20_000, 1);

        // Assert: 1000 points x (window of 1000 + 2) x 3 passes x 3 elements, plus 16 per point for the sort
        Assert.Equal((1000L * 1002 * 3 * 3) + 16_000, cost);
        Assert.Equal(cost, huge);
        Assert.True(big > VegaLiteLimits.Default.MaxEvaluationSteps * 64);
    }

    /// <summary>
    ///     Validates the argument checks.
    /// </summary>
    [Fact]
    public void LoessFit_Fit_RejectsInvalidArguments()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => LoessFit.Fit(null!, Values(1), 0.3));
        Assert.Throws<ArgumentNullException>(() => LoessFit.Fit(Values(1), null!, 0.3));
        Assert.Throws<ArgumentException>(() => LoessFit.Fit(Values(1, 2), Values(1), 0.3));
    }
}
