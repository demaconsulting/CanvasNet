using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Unit tests for the curve sampler: the uniform grid, the adaptive subdivision of a bending curve, the minimum grid of a straight line, the JavaScript edge cases (a NaN extent, a
///     fractional step count, a reversed extent) and the progress callback. The point counts of the adaptive cases were confirmed against vega-statistics' <c>sampleCurve</c>.
/// </summary>
public class CurveSamplerTests
{
    /// <summary>
    ///     Validates that equal step counts give the uniform grid of <c>steps + 1</c> points, which for <c>x²</c> on [0, 10] in 5 steps is the closed form <c>(2i)²</c>.
    /// </summary>
    [Fact]
    public void CurveSampler_Sample_EqualStepCountsGiveAUniformGrid()
    {
        // Arrange / Act
        var points = CurveSampler.Sample(x => x * x, 0, 10, 5, 5);

        // Assert
        Assert.Equal([0.0, 2, 4, 6, 8, 10], points.Select(p => p.X));
        Assert.Equal([0.0, 4, 16, 36, 64, 100], points.Select(p => p.Y));
    }

    /// <summary>
    ///     Validates that a fractional step count is legal: 2.5 steps stop at the last grid point below 2.5 and then at the end of the extent, as the JavaScript loop does.
    /// </summary>
    [Fact]
    public void CurveSampler_Sample_AFractionalUniformStepCountFollowsTheJavaScriptLoop()
    {
        // Arrange / Act
        var points = CurveSampler.Sample(x => x, 0, 1, 2.5, 2.5);

        // Assert: the grid is i / 2.5 for i = 1, 2, then the end: 0, 0.4, 0.8, 1
        Assert.Equal(4, points.Count);
        Assert.Equal(0.4, points[1].X, 12);
        Assert.Equal(0.8, points[2].X, 12);
        Assert.Equal(1.0, points[3].X);
    }

    /// <summary>
    ///     Validates that a straight line is drawn with the minimum grid only: 26 points for the default 25 steps, evenly spaced, with the end points of the extent.
    /// </summary>
    [Fact]
    public void CurveSampler_Sample_AStraightLineKeepsTheMinimumGrid()
    {
        // Arrange / Act
        var points = CurveSampler.Sample(x => (2 * x) + 1, 0, 10, 0, 0);

        // Assert: the angle of every midpoint is zero, so no interval is subdivided
        Assert.Equal(26, points.Count);
        Assert.Equal((0.0, 1.0), points[0]);
        Assert.Equal((10.0, 21.0), points[^1]);
        Assert.Equal(0.4, points[1].X, 12);
        Assert.Equal(1.8, points[1].Y, 12);
    }

    /// <summary>
    ///     Validates the adaptive subdivision: a sine wave over one period needs more than the minimum grid and no more than the maximum, keeps the order of x and ends at the extent
    ///     (vega-statistics gives 93 points for this curve).
    /// </summary>
    [Fact]
    public void CurveSampler_Sample_ABendingCurveIsSubdividedWhereItBends()
    {
        // Arrange
        var end = 2 * Math.PI;

        // Act
        var points = CurveSampler.Sample(Math.Sin, 0, end, 25, 200);

        // Assert
        Assert.Equal(93, points.Count);
        Assert.Equal(0.0, points[0].X);
        Assert.Equal(end, points[^1].X);
        Assert.Equal(0.25132741228718347, points[1].X, 12);
        Assert.Equal(Math.Sin(0.25132741228718347), points[1].Y, 12);
        Assert.True(points.Zip(points.Skip(1), (a, b) => b.X > a.X).All(increasing => increasing));
    }

    /// <summary>
    ///     Validates that the number of points of a smooth curve is the one of the reference: 124 for a cubic with its extremum inside, and the points are those of the function.
    /// </summary>
    [Fact]
    public void CurveSampler_Sample_CubicCurveHasTheReferenceNumberOfPoints()
    {
        // Arrange: the cubic of the poly regression test over its extent
        static double F(double v) => 1 - v + (0.5 * v * v) + (0.25 * v * v * v);

        // Act
        var points = CurveSampler.Sample(F, -4, 4, 25, 200);

        // Assert: the reference sample has 124 points, the first interval is the minimum-grid step of 0.32 subdivided to 0.08
        Assert.Equal(124, points.Count);
        Assert.Equal(-4.0, points[0].X);
        Assert.Equal(-3.92, points[1].X, 12);
        Assert.Equal(-3.84, points[2].X, 12);
        Assert.Equal(F(-3.76), points[3].Y, 12);
        Assert.Equal(4.0, points[^1].X);
    }

    /// <summary>
    ///     Validates the step ranges: a maximum below the minimum is raised to it (a uniform grid of 10 steps), and a minimum of zero stands for 25.
    /// </summary>
    [Fact]
    public void CurveSampler_Sample_StepCountsAreNormalizedAsInJavaScript()
    {
        // Arrange / Act
        var raised = CurveSampler.Sample(x => x, 0, 1, 10, 4);
        var defaults = CurveSampler.Sample(x => x, 0, 1, 0, 0);

        // Assert
        Assert.Equal(11, raised.Count);
        Assert.Equal(26, defaults.Count);
    }

    /// <summary>
    ///     Validates the extent of nothing (NaN): the samples are NaN and the number of points is that of the grid (26 adaptive, steps + 1 uniform), without an exception.
    /// </summary>
    [Fact]
    public void CurveSampler_Sample_ANaNExtentGivesNaNSamples()
    {
        // Arrange / Act
        var adaptive = CurveSampler.Sample(x => x, double.NaN, double.NaN, 25, 200);
        var uniform = CurveSampler.Sample(x => x, double.NaN, double.NaN, 2, 2);

        // Assert
        Assert.Equal(26, adaptive.Count);
        Assert.Equal(3, uniform.Count);
        Assert.All(adaptive.Concat(uniform), p => Assert.True(double.IsNaN(p.X) && double.IsNaN(p.Y)));
    }

    /// <summary>
    ///     Validates that a reversed extent is sampled from its start to its end without subdivision (the resolution test is false for a negative span), as the reference does.
    /// </summary>
    [Fact]
    public void CurveSampler_Sample_AReversedExtentKeepsTheMinimumGrid()
    {
        // Arrange / Act
        var points = CurveSampler.Sample(x => x * x, 10, 0, 25, 200);

        // Assert
        Assert.Equal(26, points.Count);
        Assert.Equal((10.0, 100.0), points[0]);
        Assert.Equal(9.6, points[1].X, 12);
        Assert.Equal((0.0, 0.0), points[^1]);
    }

    /// <summary>
    ///     Validates that the progress callback sees the growing number of points during the refinement, and that an exception of the callback stops the sampling.
    /// </summary>
    [Fact]
    public void CurveSampler_Sample_ReportsProgressAndStopsWhenTheCallbackThrows()
    {
        // Arrange
        var seen = new List<int>();

        // Act
        var points = CurveSampler.Sample(Math.Sin, 0, 2 * Math.PI, 25, 200, seen.Add);
        var stopped = Assert.Throws<InvalidOperationException>(() => CurveSampler.Sample(Math.Sin, 0, 2 * Math.PI, 25, 200, n =>
        {
            if (n > 30)
            {
                throw new InvalidOperationException("budget");
            }
        }));

        // Assert: the loop reports before every refinement step, never fewer than the minimum grid
        Assert.NotEmpty(seen);
        Assert.All(seen, n => Assert.True(n >= 26));
        Assert.True(seen.Max() <= points.Count);
        Assert.Equal("budget", stopped.Message);
    }

    /// <summary>
    ///     Validates the bound on the number of points and the argument check.
    /// </summary>
    [Fact]
    public void CurveSampler_MaximumPoints_BoundsTheSample()
    {
        // Arrange / Act / Assert
        Assert.Equal(227, CurveSampler.MaximumPoints(0, 0));
        Assert.Equal(15, CurveSampler.MaximumPoints(5, 8));
        Assert.True(CurveSampler.MaximumPoints(10, 4) >= 12);
        Assert.Throws<ArgumentNullException>(() => CurveSampler.Sample(null!, 0, 1, 1, 1));
    }
}
