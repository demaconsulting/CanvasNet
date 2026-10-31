using DemaConsulting.CanvasNet.VegaLite.Marks;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using static DemaConsulting.CanvasNet.VegaLite.Tests.Marks.CurveBuilderTests;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Marks;

/// <summary>
///     Tests of the <see cref="CurveBuilder.Area"/> generator: the polygons of vertical and horizontal areas, the baseline pass of
///     curved areas for every defined run, and the bundle curve that cannot draw an area. Every expected path is derived by hand
///     from d3-shape's <c>area.js</c> (top line forward, baseline backward) and the curve definitions.
/// </summary>
public class CurveBuilderAreaTests
{
    /// <summary>A vertex of an area that is not defined.</summary>
    private static readonly AreaPoint Gap = new(0, 0, 0, false);

    /// <summary>
    ///     Defines a vertex of an area.
    /// </summary>
    /// <param name="along">The coordinate along the axis.</param>
    /// <param name="top">The value edge.</param>
    /// <param name="baseline">The baseline.</param>
    /// <returns>The vertex.</returns>
    private static AreaPoint A(double along, double top, double baseline) => new(along, top, baseline, true);

    /// <summary>
    ///     Generates the text of an area.
    /// </summary>
    /// <param name="kind">The curve.</param>
    /// <param name="horizontal">Whether the area is horizontal.</param>
    /// <param name="points">The vertices.</param>
    /// <returns>The text.</returns>
    private static string Area(CurveKind kind, bool horizontal, params AreaPoint[] points)
    {
        var output = new List<ScenePathSegment>();
        CurveBuilder.Area(kind, null, horizontal, points, output, CancellationToken.None);
        return Text(output);
    }

    /// <summary>
    ///     Validates the polygons of the linear area in both orientations and for gaps and a closed curve.
    /// </summary>
    [Fact]
    public void CurveBuilder_Area_VerticalAndHorizontalPolygonsMatchTheAnchors()
    {
        // A vertical area (the line chart of 300 x 200: x 0, 150, 300; y 200, 0, 100; baseline y = 200): the top line forward, then the baseline
        // backward as a continuation of the same path (lineTo, because the second pass has _line = 1), closed
        Assert.Equal(
            "M0,200 L150,0 L300,100 L300,200 L150,200 L0,200 Z",
            Area(CurveKind.Linear, false, A(0, 200, 200), A(150, 0, 200), A(300, 100, 200)));

        // A horizontal area: y is the axis, the top line runs along x1 = 5, 15, 10 and the baseline along x0 = 0
        Assert.Equal(
            "M5,0 L15,10 L10,20 L0,20 L0,10 L0,0 Z",
            Area(CurveKind.Linear, true, A(0, 5, 0), A(10, 15, 0), A(20, 10, 0)));

        // Each run of defined points is its own closed polygon; a run of one point is a line to the baseline and back
        Assert.Equal(
            "M0,5 L10,15 L10,0 L0,0 Z M30,7 L30,0 Z",
            Area(CurveKind.Linear, false, A(0, 5, 0), A(10, 15, 0), Gap, A(30, 7, 0)));

        // The closed curve has no area passes: the top line and the baseline are two closed polygons
        Assert.Equal(
            "M0,200 L150,0 L300,100 Z M300,200 L150,200 L0,200 Z",
            Area(CurveKind.LinearClosed, false, A(0, 200, 200), A(150, 0, 200), A(300, 100, 200)));

        // A vertex with a coordinate that is not finite ends the run
        Assert.Equal(
            "M0,5 L0,0 Z M20,5 L20,0 Z",
            Area(CurveKind.Linear, false, A(0, 5, 0), A(10, double.NaN, 0), A(20, 5, 0)));
        Assert.Equal(string.Empty, Area(CurveKind.Linear, false));
    }

    /// <summary>
    ///     Validates the baseline pass of curved areas (the curve is applied to both passes) for step, monotone and horizontal monotone areas.
    /// </summary>
    [Fact]
    public void CurveBuilder_Area_CurvedTopAndBaselineClosePerDefinedRun()
    {
        // step, run (0,10), (10,20), (20,15) over the baseline 0: the top is the centered step of the line (ending with the flush to (20,15));
        // lineEnd turns t into 1 - t = 0.5 and the baseline pass continues from (20,15) with a line to its first point (20,0), then repeats the centered
        // step backward through (10,0) and (0,0) with its own flush, and closes
        Assert.Equal(
            "M0,10 L5,10 L5,20 L15,20 L15,15 L20,15 L20,0 L15,0 L15,0 L5,0 L5,0 L0,0 Z",
            Area(CurveKind.Step, false, A(0, 10, 0), A(10, 20, 0), A(20, 15, 0)));

        // step-after with the baseline 0, 2, 4: the top changes at the new x; the baseline pass has t = 0 (the flipped t) so that, backward, it changes at
        // the old x: the baseline is the same step-after curve of the data, read from right to left: 4 at x = 20, 2 on [10,20), 0 on [0,10)
        Assert.Equal(
            "M0,10 L10,10 L10,20 L20,20 L20,15 L20,4 L20,2 L10,2 L10,0 L0,0 Z",
            Area(CurveKind.StepAfter, false, A(0, 10, 0), A(10, 20, 2), A(20, 15, 4)));

        // monotone: the top line (0,0), (10,10), (20,0) is the symmetric peak with the controls (3.3333,5), (6.6667,10) | (13.3333,10), (16.6667,5); the baseline
        // y = -10 runs backward with zero tangents (flat data), so its controls are the thirds of the interval; a second run of two points draws lines.
        // The path is the same for both runs' areaStart/areaEnd (the second run starts again with the top pass)
        Assert.Equal(
            "M0,0 C3.3333,5 6.6667,10 10,10 C13.3333,10 16.6667,5 20,0 L20,-10 C16.6667,-10 13.3333,-10 10,-10 C6.6667,-10 3.3333,-10 0,-10 Z "
            + "M40,5 L50,15 L50,-10 L40,-10 Z",
            Area(CurveKind.MonotoneX, false, A(0, 0, -10), A(10, 10, -10), A(20, 0, -10), Gap, A(40, 5, -10), A(50, 15, -10)));

        // monotone of a horizontal area is the curve of y: the same data with x and y exchanged (the top line has x1 = 0, 10, 0 along y = 0, 10, 20 and the
        // baseline x0 = -10), so every pair of the vertical result is swapped
        Assert.Equal(
            "M0,0 C5,3.3333 10,6.6667 10,10 C10,13.3333 5,16.6667 0,20 L-10,20 C-10,16.6667 -10,13.3333 -10,10 C-10,6.6667 -10,3.3333 -10,0 Z",
            Area(CurveKind.MonotoneY, true, A(0, 0, -10), A(10, 10, -10), A(20, 0, -10)));
    }

    /// <summary>
    ///     Validates that the bundle curve is reported as unsupported for areas (d3 gives it no <c>areaStart</c>) and every other curve is supported.
    /// </summary>
    [Fact]
    public void CurveBuilder_Area_BundleIsReportedAsUnsupported()
    {
        // Only the bundle cannot draw an area
        Assert.False(CurveBuilder.SupportsArea(CurveKind.Bundle));
        Assert.All(Enum.GetValues<CurveKind>().Where(k => k != CurveKind.Bundle), kind => Assert.True(CurveBuilder.SupportsArea(kind)));

        // The generator refuses it, writing nothing
        var output = new List<ScenePathSegment>();
        Assert.Throws<NotSupportedException>(() => CurveBuilder.Area(CurveKind.Bundle, null, false, [A(0, 1, 0), A(1, 2, 0)], output, CancellationToken.None));
        Assert.Empty(output);

        // The line generator has no such restriction
        Assert.Equal("M0,0 L10,10", Run(CurveKind.Bundle, null, P(0, 0), P(10, 10)));
    }
}
