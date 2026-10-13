// cspell:ignore vsdx Visio NURBS NURBSTo PolylineTo SplineStart SplineKnot

using DemaConsulting.CanvasNet.Geometry;

namespace DemaConsulting.CanvasNet.Vsdx.Tests;

/// <summary>
///     Unit-level tests for the curve geometry rows (<c>PolylineTo</c>, <c>NURBSTo</c>,
///     <c>SplineStart</c>/<c>SplineKnot</c>) resolved by <c>VsdxDocument.Splines.cs</c>, their
///     hard caps, and the group-child Master inheritance that lets a grouped stencil shape draw.
///     Every package is built in memory from synthetic XML.
/// </summary>
public class VsdxCurveGeometryTests
{
    /// <summary>The documented control-point cap (mirrors the renderer's hard limit).</summary>
    private const int MaxControlPoints = 1024;

    /// <summary>The documented flattened-segment cap (mirrors the renderer's hard limit).</summary>
    private const int MaxSegments = 4096;

    /// <summary>A single 2x1-inch shape with no rotation/flip.</summary>
    private const string ShapeCellsXml =
        """
        <Cell N="PinX" V="1"/><Cell N="PinY" V="1"/><Cell N="Width" V="2"/><Cell N="Height" V="1"/>
        <Cell N="LocPinX" V="1"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
        """;

    /// <summary>Builds a one-shape package from the given geometry rows and returns its first geometry subpath plus the reported features.</summary>
    /// <param name="rowsXml">The <c>&lt;Row&gt;</c> elements of one geometry section.</param>
    /// <param name="features">The distinct unsupported-feature tokens reported while rendering.</param>
    /// <returns>The shape's first subpath.</returns>
    private static Subpath ResolveSubpath(string rowsXml, out List<string> features)
    {
        var shapeXml =
            $"""
            <Shape ID="1" Type="Shape">
              {ShapeCellsXml}
              <Section N="Geometry" IX="0">
                {rowsXml}
              </Section>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);
        var received = new List<string>();
        using var surface = document.Render(0, 20, 20, new VsdxRenderOptions { UnsupportedFeatureHandler = f => received.Add(f.Feature) });
        features = received;
        return Assert.Single(document.GetPageShapes(0)[0].Geometries![0].Path.Subpaths);
    }

    /// <summary>Returns the first sampled point whose X is within tolerance of <paramref name="x"/>.</summary>
    private static System.Numerics.Vector2 PointNearX(Subpath subpath, float x) =>
        subpath.Commands.Select(c => c.EndPoint).First(p => Math.Abs(p.X - x) < 1e-3f);

    /// <summary>A quadratic SplineStart/SplineKnot group flattens to a curve that passes through the Bezier midpoint and ends at the last knot's point.</summary>
    [Fact]
    public void Geometry_SplineStartKnot_QuadraticFlattensToCurve()
    {
        var subpath = ResolveSubpath(
            """
            <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
            <Row T="SplineStart" IX="2"><Cell N="X" V="1"/><Cell N="Y" V="1"/><Cell N="A" V="0"/><Cell N="B" V="0"/><Cell N="C" V="1"/><Cell N="D" V="2"/></Row>
            <Row T="SplineKnot" IX="3"><Cell N="X" V="2"/><Cell N="Y" V="0"/><Cell N="A" V="0"/></Row>
            """,
            out var features);

        Assert.Empty(features);
        Assert.True(subpath.Commands.Count >= 16);
        Assert.All(subpath.Commands, c => Assert.Equal(PathCommandType.LineTo, c.Type));
        Assert.Equal(2f, subpath.Commands[^1].EndPoint.X, 3);
        Assert.Equal(0f, subpath.Commands[^1].EndPoint.Y, 3);
        Assert.Equal(0.5f, PointNearX(subpath, 1f).Y, 2);
    }

    /// <summary>A spline followed by another row ends at its last control point, and the next row continues from there.</summary>
    [Fact]
    public void Geometry_SplineGroup_FollowingRowContinuesFromLastControlPoint()
    {
        var subpath = ResolveSubpath(
            """
            <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
            <Row T="SplineStart" IX="2"><Cell N="X" V="1"/><Cell N="Y" V="1"/><Cell N="A" V="0"/><Cell N="B" V="0"/><Cell N="C" V="1"/><Cell N="D" V="2"/></Row>
            <Row T="SplineKnot" IX="3"><Cell N="X" V="2"/><Cell N="Y" V="0"/><Cell N="A" V="0"/></Row>
            <Row T="RelLineTo" IX="4"><Cell N="X" V="0"/><Cell N="Y" V="1"/></Row>
            """,
            out _);

        Assert.Equal(0f, subpath.Commands[^1].EndPoint.X, 3);
        Assert.Equal(1f, subpath.Commands[^1].EndPoint.Y, 3);
        Assert.Equal(2f, subpath.Commands[^2].EndPoint.X, 3);
    }

    /// <summary>An orphan SplineKnot (no SplineStart) is skipped and reported, not thrown.</summary>
    [Fact]
    public void Geometry_SplineKnotWithoutStart_ReportedAndSkipped()
    {
        var subpath = ResolveSubpath(
            """
            <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
            <Row T="SplineKnot" IX="2"><Cell N="X" V="2"/><Cell N="Y" V="0"/><Cell N="A" V="0"/></Row>
            """,
            out var features);

        Assert.Empty(subpath.Commands);
        Assert.Contains("vsdx-geometry-row-SplineKnot", features);
    }

    /// <summary>A SplineKnot before the first MoveTo (no open subpath) is reported as an orphan knot, not silently discarded.</summary>
    [Fact]
    public void Geometry_SplineKnotBeforeMoveTo_ReportedAndSkipped()
    {
        var subpath = ResolveSubpath(
            """
            <Row T="SplineKnot" IX="1"><Cell N="X" V="2"/><Cell N="Y" V="0"/><Cell N="A" V="0"/></Row>
            <Row T="MoveTo" IX="2"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
            <Row T="LineTo" IX="3"><Cell N="X" V="1"/><Cell N="Y" V="1"/></Row>
            """,
            out var features);

        Assert.Single(subpath.Commands);
        Assert.Contains("vsdx-geometry-row-SplineKnot", features);
    }

    /// <summary>
    ///     A subpath-continuing row (NURBSTo, PolylineTo, LineTo, arcs, relative curves, SplineStart) before
    ///     any MoveTo has no open subpath to extend: it adds no geometry and is reported, never silently dropped.
    /// </summary>
    [Theory]
    [InlineData("NURBSTo")]
    [InlineData("PolylineTo")]
    [InlineData("LineTo")]
    [InlineData("RelLineTo")]
    [InlineData("ArcTo")]
    [InlineData("EllipticalArcTo")]
    [InlineData("RelCubBezTo")]
    [InlineData("RelQuadBezTo")]
    [InlineData("SplineStart")]
    public void Geometry_ContinuationRowBeforeMoveTo_ReportedAndSkipped(string rowType)
    {
        var subpath = ResolveSubpath(
            $"""
            <Row T="{rowType}" IX="1"><Cell N="X" V="2"/><Cell N="Y" V="2"/><Cell N="A" V="1"/><Cell N="B" V="1"/><Cell N="C" V="1"/><Cell N="D" V="1"/></Row>
            <Row T="MoveTo" IX="2"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
            """,
            out var features);

        Assert.Empty(subpath.Commands);
        Assert.Contains($"vsdx-geometry-row-{rowType}", features);
    }

    /// <summary>A spline with decreasing knots degrades to a polyline through the control points and reports a distinct token.</summary>
    [Fact]
    public void Geometry_SplineWithInvalidKnots_FallsBackToPolylineAndReports()
    {
        var subpath = ResolveSubpath(
            """
            <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
            <Row T="SplineStart" IX="2"><Cell N="X" V="1"/><Cell N="Y" V="1"/><Cell N="A" V="5"/><Cell N="B" V="9"/><Cell N="C" V="1"/><Cell N="D" V="2"/></Row>
            <Row T="SplineKnot" IX="3"><Cell N="X" V="2"/><Cell N="Y" V="0"/><Cell N="A" V="0"/></Row>
            """,
            out var features);

        Assert.Equal(2, subpath.Commands.Count);
        Assert.Equal(1f, subpath.Commands[0].EndPoint.X, 3);
        Assert.Equal(2f, subpath.Commands[1].EndPoint.X, 3);
        Assert.Contains("vsdx-geometry-spline-approximated", features);
    }

    /// <summary>A SplineStart missing a required cell is skipped and reported instead of drawing a zero-filled curve.</summary>
    [Fact]
    public void Geometry_SplineStartMissingCell_SkippedAndReported()
    {
        var subpath = ResolveSubpath(
            """
            <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
            <Row T="SplineStart" IX="2"><Cell N="X" V="1"/><Cell N="Y" V="abc"/><Cell N="A" V="0"/><Cell N="B" V="0"/><Cell N="C" V="1"/><Cell N="D" V="2"/></Row>
            <Row T="LineTo" IX="3"><Cell N="X" V="2"/><Cell N="Y" V="1"/></Row>
            """,
            out var features);

        Assert.Single(subpath.Commands);
        Assert.Contains("vsdx-geometry-row-SplineStart", features);
    }

    /// <summary>A SplineStart whose required degree cell D is missing or not a finite number is skipped and reported rather than defaulting to a cubic.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("<Cell N=\"D\" V=\"abc\"/>")]
    [InlineData("<Cell N=\"D\" V=\"NaN\"/>")]
    [InlineData("<Cell N=\"D\" V=\"Infinity\"/>")]
    public void Geometry_SplineStartInvalidDegree_SkippedAndReported(string degreeCellXml)
    {
        var subpath = ResolveSubpath(
            $"""
            <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
            <Row T="SplineStart" IX="2"><Cell N="X" V="1"/><Cell N="Y" V="1"/><Cell N="A" V="0"/><Cell N="B" V="0"/><Cell N="C" V="1"/>{degreeCellXml}</Row>
            <Row T="SplineKnot" IX="3"><Cell N="X" V="2"/><Cell N="Y" V="0"/><Cell N="A" V="0.5"/></Row>
            """,
            out var features);

        Assert.Empty(subpath.Commands);
        Assert.Contains("vsdx-geometry-row-SplineStart", features);
    }

    /// <summary>A SplineKnot with a malformed required cell is skipped and reported; the remaining spline still resolves.</summary>
    [Fact]
    public void Geometry_SplineKnotMalformedCell_SkippedAndReported()
    {
        var subpath = ResolveSubpath(
            """
            <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
            <Row T="SplineStart" IX="2"><Cell N="X" V="1"/><Cell N="Y" V="1"/><Cell N="A" V="0"/><Cell N="B" V="0"/><Cell N="C" V="1"/><Cell N="D" V="2"/></Row>
            <Row T="SplineKnot" IX="3"><Cell N="X" V="2"/><Cell N="Y" V="0"/><Cell N="A" V="0"/></Row>
            <Row T="SplineKnot" IX="4"><Cell N="X" V="NaN"/><Cell N="Y" V="0"/><Cell N="A" V="0"/></Row>
            """,
            out var features);

        Assert.Contains("vsdx-geometry-row-SplineKnot", features);
        Assert.Equal(2f, subpath.Commands[^1].EndPoint.X, 3);
        Assert.Equal(0.5f, PointNearX(subpath, 1f).Y, 2);
    }

    /// <summary>A NURBSTo with a non-positive weight is skipped atomically and reported instead of drawing partial geometry.</summary>
    [Fact]
    public void Geometry_NurbsTo_InvalidWeight_SkippedAndReported()
    {
        var subpath = ResolveSubpath(
            """
            <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
            <Row T="NURBSTo" IX="2"><Cell N="X" V="2"/><Cell N="Y" V="0"/><Cell N="A" V="0"/><Cell N="B" V="1"/><Cell N="C" V="0"/><Cell N="D" V="1"/><Cell N="E" V="0" F="NURBS(1,2,1,1,1,1,0,-2)"/></Row>
            """,
            out var features);

        Assert.Empty(subpath.Commands);
        Assert.Contains("vsdx-geometry-row-NURBSTo", features);
    }

    /// <summary>A NURBSTo row with a missing or malformed required cell (X, Y, knot, weight) is skipped atomically and reported.</summary>
    [Theory]
    [InlineData("""<Cell N="Y" V="0"/><Cell N="A" V="0"/><Cell N="B" V="1"/><Cell N="C" V="0"/><Cell N="D" V="1"/>""")]
    [InlineData("""<Cell N="X" V="2"/><Cell N="Y" V="NaN"/><Cell N="A" V="0"/><Cell N="B" V="1"/><Cell N="C" V="0"/><Cell N="D" V="1"/>""")]
    [InlineData("""<Cell N="X" V="2"/><Cell N="Y" V="0"/><Cell N="B" V="1"/><Cell N="C" V="0"/><Cell N="D" V="1"/>""")]
    [InlineData("""<Cell N="X" V="2"/><Cell N="Y" V="0"/><Cell N="A" V="0"/><Cell N="C" V="0"/><Cell N="D" V="1"/>""")]
    [InlineData("""<Cell N="X" V="2"/><Cell N="Y" V="0"/><Cell N="A" V="0"/><Cell N="B" V="0"/><Cell N="C" V="0"/><Cell N="D" V="1"/>""")]
    [InlineData("""<Cell N="X" V="2"/><Cell N="Y" V="0"/><Cell N="A" V="0"/><Cell N="B" V="1"/><Cell N="C" V="0"/>""")]
    public void Geometry_NurbsTo_InvalidRequiredCell_SkippedAtomicallyAndReported(string cellsXml)
    {
        var subpath = ResolveSubpath(
            $"""
            <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
            <Row T="NURBSTo" IX="2">{cellsXml}<Cell N="E" V="0" F="NURBS(1,2,1,1,1,1,0,1)"/></Row>
            """,
            out var features);

        Assert.Empty(subpath.Commands);
        Assert.Contains("vsdx-geometry-row-NURBSTo", features);
    }

    /// <summary>A SplineStart with no following SplineKnot rows is reported and adds no geometry.</summary>
    [Fact]
    public void Geometry_SplineStartWithoutKnots_ReportedAndSkipped()
    {
        var subpath = ResolveSubpath(
            """
            <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
            <Row T="SplineStart" IX="2"><Cell N="X" V="1"/><Cell N="Y" V="1"/><Cell N="A" V="0"/><Cell N="B" V="0"/><Cell N="C" V="1"/><Cell N="D" V="2"/></Row>
            """,
            out var features);

        Assert.Empty(subpath.Commands);
        Assert.Contains("vsdx-geometry-row-SplineStart", features);
    }

    /// <summary>A SplineStart followed directly by another row type (no knots) is reported and the following row still draws.</summary>
    [Fact]
    public void Geometry_SplineStartFollowedByLineTo_ReportedAndLineStillDraws()
    {
        var subpath = ResolveSubpath(
            """
            <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
            <Row T="SplineStart" IX="2"><Cell N="X" V="1"/><Cell N="Y" V="1"/><Cell N="A" V="0"/><Cell N="B" V="0"/><Cell N="C" V="1"/><Cell N="D" V="2"/></Row>
            <Row T="LineTo" IX="3"><Cell N="X" V="2"/><Cell N="Y" V="1"/></Row>
            """,
            out var features);

        var command = Assert.Single(subpath.Commands);
        Assert.Equal(2f, command.EndPoint.X, 3);
        Assert.Contains("vsdx-geometry-row-SplineStart", features);
    }

    /// <summary>A PolylineTo row with a missing or non-finite X or Y end point is skipped and reported rather than drawing to (0, 0).</summary>
    [Theory]
    [InlineData("""<Cell N="Y" V="1"/>""")]
    [InlineData("""<Cell N="X" V="2"/>""")]
    [InlineData("""<Cell N="X" V="NaN"/><Cell N="Y" V="1"/>""")]
    [InlineData("""<Cell N="X" V="2"/><Cell N="Y" V="abc"/>""")]
    public void Geometry_PolylineTo_InvalidEndpoint_SkippedAndReported(string endXml)
    {
        var subpath = ResolveSubpath(
            $"""
            <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
            <Row T="PolylineTo" IX="2">{endXml}<Cell N="A" V="0" F="POLYLINE(1,1,1,1,2,0)"/></Row>
            """,
            out var features);

        Assert.Empty(subpath.Commands);
        Assert.Contains("vsdx-geometry-row-PolylineTo", features);
    }

    /// <summary>A degree above the cap is clamped and a degree above the point count is reduced; resolution never throws.</summary>
    [Fact]
    public void Geometry_SplineDegreeOutOfRange_IsClampedWithoutThrowing()
    {
        var subpath = ResolveSubpath(
            """
            <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
            <Row T="SplineStart" IX="2"><Cell N="X" V="1"/><Cell N="Y" V="1"/><Cell N="A" V="0"/><Cell N="B" V="0"/><Cell N="C" V="1"/><Cell N="D" V="99"/></Row>
            <Row T="SplineKnot" IX="3"><Cell N="X" V="2"/><Cell N="Y" V="0"/><Cell N="A" V="0"/></Row>
            """,
            out _);

        Assert.Equal(2f, subpath.Commands[^1].EndPoint.X, 3);
        Assert.Equal(0.5f, PointNearX(subpath, 1f).Y, 2);
    }

    /// <summary>More SplineKnot rows than the control-point cap are ignored and reported; the curve stays bounded.</summary>
    [Fact]
    public void Geometry_SplineKnotsBeyondCap_AreTruncatedAndReported()
    {
        var knots = string.Concat(Enumerable.Range(0, MaxControlPoints + 50)
            .Select(i => $"""<Row T="SplineKnot" IX="{i + 3}"><Cell N="X" V="{(i % 2)}"/><Cell N="Y" V="0.5"/><Cell N="A" V="{i + 1}"/></Row>"""));
        var subpath = ResolveSubpath(
            $"""
            <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
            <Row T="SplineStart" IX="2"><Cell N="X" V="1"/><Cell N="Y" V="1"/><Cell N="A" V="0"/><Cell N="B" V="0"/><Cell N="C" V="2000"/><Cell N="D" V="3"/></Row>
            {knots}
            """,
            out var features);

        Assert.True(subpath.Commands.Count <= MaxSegments);
        Assert.Contains("vsdx-geometry-row-SplineKnot", features);
    }

    /// <summary>A NURBSTo row with unit weights and local coordinates reproduces the quadratic Bezier.</summary>
    [Fact]
    public void Geometry_NurbsTo_LocalCoordinatesQuadratic()
    {
        var subpath = ResolveSubpath(
            """
            <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
            <Row T="NURBSTo" IX="2"><Cell N="X" V="2"/><Cell N="Y" V="0"/><Cell N="A" V="0"/><Cell N="B" V="1"/><Cell N="C" V="0"/><Cell N="D" V="1"/><Cell N="E" V="0" F="NURBS(1,2,1,1,1,1,0,1)"/></Row>
            """,
            out var features);

        Assert.Empty(features);
        Assert.Equal(2f, subpath.Commands[^1].EndPoint.X, 3);
        Assert.Equal(0.5f, PointNearX(subpath, 1f).Y, 2);
    }

    /// <summary>An xType/yType of 0 scales the formula's coordinates by the shape's Width/Height (2 x 1 here).</summary>
    [Fact]
    public void Geometry_NurbsTo_RelativeCoordinatesScaleByWidthAndHeight()
    {
        var subpath = ResolveSubpath(
            """
            <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
            <Row T="NURBSTo" IX="2"><Cell N="X" V="2"/><Cell N="Y" V="0"/><Cell N="A" V="0"/><Cell N="B" V="1"/><Cell N="C" V="0"/><Cell N="D" V="1"/><Cell N="E" V="0" F="NURBS(1,2,0,0,0.5,1,0,1)"/></Row>
            """,
            out _);

        Assert.Equal(0.5f, PointNearX(subpath, 1f).Y, 2);
    }

    /// <summary>A heavier middle weight pulls the curve toward the middle control point (midpoint (1, 2/3) for weights 1, 2, 1).</summary>
    [Fact]
    public void Geometry_NurbsTo_WeightPullsCurveTowardControlPoint()
    {
        var subpath = ResolveSubpath(
            """
            <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
            <Row T="NURBSTo" IX="2"><Cell N="X" V="2"/><Cell N="Y" V="0"/><Cell N="A" V="0"/><Cell N="B" V="1"/><Cell N="C" V="0"/><Cell N="D" V="1"/><Cell N="E" V="0" F="NURBS(1,2,1,1,1,1,0,2)"/></Row>
            """,
            out _);

        Assert.Equal(2f / 3f, PointNearX(subpath, 1f).Y, 2);
    }

    /// <summary>A NURBSTo row with no parseable NURBS formula is skipped and reported under the row-type token.</summary>
    [Fact]
    public void Geometry_NurbsTo_MalformedFormula_SkippedAndReported()
    {
        var subpath = ResolveSubpath(
            """
            <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
            <Row T="NURBSTo" IX="2"><Cell N="X" V="2"/><Cell N="Y" V="0"/><Cell N="E" V="0" F="NURBS(1,2,abc)"/></Row>
            <Row T="LineTo" IX="3"><Cell N="X" V="2"/><Cell N="Y" V="1"/></Row>
            """,
            out var features);

        Assert.Single(subpath.Commands);
        Assert.Contains("vsdx-geometry-row-NURBSTo", features);
    }

    /// <summary>A NURBS formula with more points than the cap is skipped and reported.</summary>
    [Fact]
    public void Geometry_NurbsTo_OverPointCap_SkippedAndReported()
    {
        var points = string.Join(',', Enumerable.Repeat("0.5,0.5,0,1", MaxControlPoints + 1));
        var subpath = ResolveSubpath(
            $"""
            <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
            <Row T="NURBSTo" IX="2"><Cell N="X" V="2"/><Cell N="Y" V="0"/><Cell N="E" V="0" F="NURBS(1,3,1,1,{points})"/></Row>
            """,
            out var features);

        Assert.Empty(subpath.Commands);
        Assert.Contains("vsdx-geometry-row-NURBSTo", features);
    }

    /// <summary>A PolylineTo row draws its absolute formula vertices as line segments.</summary>
    [Fact]
    public void Geometry_PolylineTo_AbsoluteVertices()
    {
        var subpath = ResolveSubpath(
            """
            <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
            <Row T="PolylineTo" IX="2"><Cell N="X" V="2"/><Cell N="Y" V="0"/><Cell N="A" V="0" F="POLYLINE(1,1,1,1,2,0)"/></Row>
            """,
            out var features);

        Assert.Empty(features);
        Assert.Equal(2, subpath.Commands.Count);
        Assert.Equal(1f, subpath.Commands[0].EndPoint.X, 3);
        Assert.Equal(1f, subpath.Commands[0].EndPoint.Y, 3);
        Assert.Equal(2f, subpath.Commands[1].EndPoint.X, 3);
    }

    /// <summary>A PolylineTo row with xType/yType 0 scales vertices by Width/Height and still reaches its own X/Y end point.</summary>
    [Fact]
    public void Geometry_PolylineTo_RelativeVerticesReachEndPoint()
    {
        var subpath = ResolveSubpath(
            """
            <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
            <Row T="PolylineTo" IX="2"><Cell N="X" V="2"/><Cell N="Y" V="1"/><Cell N="A" V="0" F="POLYLINE(0,0,0.5,1)"/></Row>
            """,
            out _);

        Assert.Equal(2, subpath.Commands.Count);
        Assert.Equal(1f, subpath.Commands[0].EndPoint.X, 3);
        Assert.Equal(1f, subpath.Commands[0].EndPoint.Y, 3);
        Assert.Equal(2f, subpath.Commands[1].EndPoint.X, 3);
        Assert.Equal(1f, subpath.Commands[1].EndPoint.Y, 3);
    }

    /// <summary>A PolylineTo row with no POLYLINE formula is skipped and reported.</summary>
    [Fact]
    public void Geometry_PolylineTo_MissingFormula_SkippedAndReported()
    {
        var subpath = ResolveSubpath(
            """
            <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
            <Row T="PolylineTo" IX="2"><Cell N="X" V="2"/><Cell N="Y" V="0"/></Row>
            """,
            out var features);

        Assert.Empty(subpath.Commands);
        Assert.Contains("vsdx-geometry-row-PolylineTo", features);
    }

    /// <summary>A group child that is itself a Master instance inherits that Master's geometry (previously it resolved with none and drew nothing).</summary>
    [Fact]
    public void Group_ChildWithOwnMaster_InheritsMasterGeometry()
    {
        const string master =
            """
            <Shape ID="1" Type="Shape">
              <Cell N="PinX" V="1"/><Cell N="PinY" V="1"/><Cell N="Width" V="2"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="1"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
                <Row T="LineTo" IX="2"><Cell N="X" V="2"/><Cell N="Y" V="1"/></Row>
              </Section>
            </Shape>
            """;
        const string page =
            """
            <Shape ID="10" Type="Group">
              <Cell N="PinX" V="3"/><Cell N="PinY" V="3"/><Cell N="Width" V="4"/><Cell N="Height" V="4"/>
              <Cell N="LocPinX" V="2"/><Cell N="LocPinY" V="2"/><Cell N="Angle" V="0"/>
              <Shapes>
                <Shape ID="11" Type="Shape" Master="1">
                  <Cell N="PinX" V="2"/><Cell N="PinY" V="2"/>
                </Shape>
              </Shapes>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(page, mastersXml: master);
        using var document = VsdxDocument.Open(stream);

        var child = Assert.Single(document.GetPageShapes(0)[0].Children);

        var subpath = Assert.Single(child.Geometries![0].Path.Subpaths);
        Assert.Equal(2f, Assert.Single(subpath.Commands).EndPoint.X, 3);
    }
}
