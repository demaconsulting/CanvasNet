using System.Numerics;
using DemaConsulting.CanvasNet.Geometry;

namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio RelLineTo RelMoveTo NURBSTo davehoward libvisio circumcenter

/// <summary>
///     Implements the <see cref="VsdxDocument"/> geometry-row resolver: converts a shape's merged
///     geometry sections (see <c>VsdxDocument.CellMerge.cs</c>) into shape-local-space
///     <see cref="VsdxGeometrySection"/> instances, recognizing <c>MoveTo</c>/<c>LineTo</c>/
///     <c>RelMoveTo</c>/<c>RelLineTo</c>/<c>EllipticalArcTo</c>/<c>ArcTo</c> rows and gracefully
///     skipping every other row type.
/// </summary>
public sealed partial class VsdxDocument
{
    /// <summary>
    ///     Builds the resolved, shape-local-space <see cref="VsdxGeometrySection"/> list from the
    ///     given merged geometry sections.
    /// </summary>
    /// <param name="mergedSections">The shape's merged (Master/instance) geometry sections, in <c>IX</c> order.</param>
    /// <param name="effectiveCells">The shape's merged flat cell bag, supplying the <c>Width</c>/<c>Height</c> values <c>RelMoveTo</c>/<c>RelLineTo</c> rows are normalized against.</param>
    /// <returns>The resolved geometry sections, one per merged section, in the same order.</returns>
    private static IReadOnlyList<VsdxGeometrySection> BuildGeometrySections(
        IReadOnlyList<VsdxGeometrySectionRaw> mergedSections,
        VsdxCellBag effectiveCells)
    {
        if (mergedSections.Count == 0)
        {
            return [];
        }

        var width = effectiveCells.GetDouble("Width");
        var height = effectiveCells.GetDouble("Height");

        var result = new List<VsdxGeometrySection>(mergedSections.Count);
        foreach (var section in mergedSections)
        {
            result.Add(BuildGeometrySection(section, width, height));
        }

        return result;
    }

    /// <summary>Builds a single resolved <see cref="VsdxGeometrySection"/> from its merged raw section.</summary>
    /// <param name="section">The merged raw section.</param>
    /// <param name="width">The owning shape's resolved <c>Width</c> cell, in inches (normalizes <c>RelMoveTo</c>/<c>RelLineTo</c> rows).</param>
    /// <param name="height">The owning shape's resolved <c>Height</c> cell, in inches (normalizes <c>RelMoveTo</c>/<c>RelLineTo</c> rows).</param>
    /// <returns>The resolved <see cref="VsdxGeometrySection"/>.</returns>
    private static VsdxGeometrySection BuildGeometrySection(VsdxGeometrySectionRaw section, double width, double height)
    {
        var builder = new PathBuilder();
        var hasOpenSubpath = false;
        var currentPoint = Vector2.Zero;

        foreach (var row in section.Rows.OrderBy(r => r.Index))
        {
            switch (row.Type)
            {
                case "MoveTo":
                    currentPoint = ToLocalPoint(row.Cells.GetDouble("X"), row.Cells.GetDouble("Y"));
                    builder.MoveTo(currentPoint);
                    hasOpenSubpath = true;
                    break;

                case "LineTo":
                    if (hasOpenSubpath)
                    {
                        currentPoint = ToLocalPoint(row.Cells.GetDouble("X"), row.Cells.GetDouble("Y"));
                        builder.LineTo(currentPoint);
                    }

                    break;

                case "RelMoveTo":
                    currentPoint = ToLocalPoint(row.Cells.GetDouble("X") * width, row.Cells.GetDouble("Y") * height);
                    builder.MoveTo(currentPoint);
                    hasOpenSubpath = true;
                    break;

                case "RelLineTo":
                    if (hasOpenSubpath)
                    {
                        currentPoint = ToLocalPoint(row.Cells.GetDouble("X") * width, row.Cells.GetDouble("Y") * height);
                        builder.LineTo(currentPoint);
                    }

                    break;

                case "EllipticalArcTo":
                    if (hasOpenSubpath)
                    {
                        currentPoint = AppendEllipticalArcTo(builder, currentPoint, row.Cells);
                    }

                    break;

                case "ArcTo":
                    if (hasOpenSubpath)
                    {
                        currentPoint = AppendArcTo(builder, currentPoint, row.Cells);
                    }

                    break;

                default:
                    // Unrecognized geometry row type (for example NURBSTo, InfiniteLine,
                    // RelCubBezTo, Ellipse, SplineStart/SplineKnot, PolylineTo): per
                    // canvas-net-vsdx.md's Risk Control Measures ("an unrecognized ... construct
                    // is tolerantly skipped rather than failing the whole parse"), this row is
                    // silently skipped, leaving the pen position exactly where the previous
                    // recognized row left it, rather than throwing. Confirmed exercised directly
                    // by davehoward-test5-master.vsdx's master1.xml, Shape ID='6', whose rounded-
                    // corner outline alternates recognized LineTo rows with unrecognized NURBSTo
                    // rows.
                    break;
            }
        }

        return new VsdxGeometrySection(
            NoFill: section.Cells.GetBool("NoFill"),
            NoLine: section.Cells.GetBool("NoLine"),
            NoShow: section.Cells.GetBool("NoShow"),
            Path: builder.Build());
    }

    /// <summary>
    ///     Appends an <c>EllipticalArcTo</c> row (MS-VSDX §2.2.1.7's <c>X</c>/<c>Y</c> end point,
    ///     <c>A</c>/<c>B</c> a third point on the arc, <c>C</c> the ellipse's major-axis angle in
    ///     radians, <c>D</c> the ratio of the ellipse's major to minor axis) to
    ///     <paramref name="builder"/>, converting it into one or more cubic Bezier segments via
    ///     <see cref="TryResolveEllipticalArc"/> and <see cref="PathBuilder.ArcTo"/>'s own
    ///     SVG-style endpoint parameterization; degrades to a straight <see cref="PathBuilder.LineTo"/>
    ///     when the three points are (near-)collinear, matching the same degenerate-case handling
    ///     the reference <c>libvisio</c> implementation uses (the three points cannot determine a
    ///     unique ellipse in that case).
    /// </summary>
    /// <param name="builder">The path builder to append to.</param>
    /// <param name="start">The current pen position (the arc's own start point), in local space.</param>
    /// <param name="cells">The row's own cell bag.</param>
    /// <returns>The arc's end point (<paramref name="builder"/>'s new current pen position).</returns>
    private static Vector2 AppendEllipticalArcTo(PathBuilder builder, Vector2 start, VsdxCellBag cells)
    {
        var end = ToLocalPoint(cells.GetDouble("X"), cells.GetDouble("Y"));
        var control = ToLocalPoint(cells.GetDouble("A"), cells.GetDouble("B"));
        var angle = cells.GetDouble("C");
        var ratio = cells.GetDouble("D", 1d);

        if (TryResolveEllipticalArc(start, control, end, angle, ratio, out var radius, out var rotationDegrees, out var largeArc, out var sweep))
        {
            builder.ArcTo(radius, rotationDegrees, largeArc, sweep, end);
        }
        else
        {
            builder.LineTo(end);
        }

        return end;
    }

    /// <summary>
    ///     Derives the SVG-style arc parameters (radii, x-axis rotation, large-arc-flag,
    ///     sweep-flag) an MS-VSDX <c>EllipticalArcTo</c> row describes, by the same
    ///     "un-rotate, un-eccentricity-scale the three known points into a circle, find that
    ///     circle's center and radius" construction the reference <c>libvisio</c> implementation's
    ///     own <c>collectEllipticalArcTo</c> uses (ratio <paramref name="ratio"/>, MS-VSDX's own
    ///     cell <c>D</c>, is the major-to-minor-axis ratio: scaling the rotated frame's own
    ///     <c>y</c>-coordinate by this ratio turns the ellipse into a circle of radius equal to
    ///     the ellipse's own semi-major axis).
    /// </summary>
    /// <param name="start">The arc's start point, in local space.</param>
    /// <param name="control">The arc's own third point (cells <c>A</c>/<c>B</c>), in local space.</param>
    /// <param name="end">The arc's end point (cells <c>X</c>/<c>Y</c>), in local space.</param>
    /// <param name="angle">The ellipse's own major-axis angle, in radians (cell <c>C</c>).</param>
    /// <param name="ratio">The ellipse's own major-to-minor-axis ratio (cell <c>D</c>).</param>
    /// <param name="radius">The resolved SVG arc x-/y-radii, when this method returns <see langword="true"/>.</param>
    /// <param name="rotationDegrees">The resolved SVG arc x-axis rotation, in degrees, when this method returns <see langword="true"/>.</param>
    /// <param name="largeArc">The resolved SVG arc "large-arc-flag", when this method returns <see langword="true"/>.</param>
    /// <param name="sweep">The resolved SVG arc "sweep-flag", when this method returns <see langword="true"/>.</param>
    /// <returns>
    ///     <see langword="true"/> when <paramref name="start"/>/<paramref name="control"/>/
    ///     <paramref name="end"/> (after the un-rotate/un-scale transform) are not collinear, and
    ///     a unique ellipse/arc was resolved; <see langword="false"/> when they are (near-)
    ///     collinear (an ill-conditioned or degenerate row - every output parameter is left at its
    ///     default), matching the reference implementation's own "fall back to a straight line"
    ///     degenerate case.
    /// </returns>
    private static bool TryResolveEllipticalArc(
        Vector2 start,
        Vector2 control,
        Vector2 end,
        double angle,
        double ratio,
        out Vector2 radius,
        out float rotationDegrees,
        out bool largeArc,
        out bool sweep)
    {
        radius = Vector2.Zero;
        rotationDegrees = 0f;
        largeArc = false;
        sweep = false;

        var eccentricity = double.IsFinite(ratio) && ratio != 0d ? ratio : 1d;
        var cosAngle = Math.Cos(angle);
        var sinAngle = Math.Sin(angle);

        // Un-rotate by -angle, then scale the rotated y-coordinate by eccentricity: this maps the
        // (unknown) ellipse the three points lie on into a circle, so a standard
        // three-point-circumcenter construction can locate its center and radius.
        var x1 = start.X * cosAngle + start.Y * sinAngle;
        var y1 = eccentricity * (start.Y * cosAngle - start.X * sinAngle);
        var x2 = control.X * cosAngle + control.Y * sinAngle;
        var y2 = eccentricity * (control.Y * cosAngle - control.X * sinAngle);
        var x3 = end.X * cosAngle + end.Y * sinAngle;
        var y3 = eccentricity * (end.Y * cosAngle - end.X * sinAngle);

        const double collinearEpsilon = 1e-10;
        var denominatorX = (x1 - x2) * (y2 - y3) - (x2 - x3) * (y1 - y2);
        var denominatorY = (x2 - x3) * (y1 - y2) - (x1 - x2) * (y2 - y3);
        if (Math.Abs(denominatorX) <= collinearEpsilon || Math.Abs(denominatorY) <= collinearEpsilon)
        {
            // The three (un-rotated/un-scaled) points are collinear: no unique circle (and
            // therefore no unique ellipse) passes through all three.
            return false;
        }

        var centerX = ((x1 - x2) * (x1 + x2) * (y2 - y3) - (x2 - x3) * (x2 + x3) * (y1 - y2) +
                       (y1 - y2) * (y2 - y3) * (y1 - y3)) / (2 * denominatorX);
        var centerY = ((x1 - x2) * (x2 - x3) * (x1 - x3) + (x2 - x3) * (y1 - y2) * (y1 + y2) -
                       (x1 - x2) * (y2 - y3) * (y2 + y3)) / (2 * denominatorY);

        var majorRadius = Math.Sqrt(((x1 - centerX) * (x1 - centerX)) + ((y1 - centerY) * (y1 - centerY)));
        var minorRadius = majorRadius / eccentricity;

        // The chord from start to end splits the plane in two; the arc is the "large" one when
        // the ellipse's own center and the known third (control) point fall on the same side of
        // that chord, and sweeps in the direction the control point lies on relative to the
        // chord - the same side/sweep determination the reference implementation uses.
        var centerSide = ((x3 - x1) * (centerY - y1)) - ((y3 - y1) * (centerX - x1));
        var controlSide = ((x3 - x1) * (y2 - y1)) - ((y3 - y1) * (x2 - x1));
        largeArc = (centerSide > 0 && controlSide > 0) || (centerSide < 0 && controlSide < 0);
        sweep = controlSide <= 0;

        radius = new Vector2((float)majorRadius, (float)minorRadius);
        rotationDegrees = (float)(angle * 180.0 / Math.PI);
        return true;
    }

    /// <summary>
    ///     Appends an <c>ArcTo</c> row (a circular arc: endpoint cells <c>X</c>/<c>Y</c> plus a
    ///     single bow-height cell <c>A</c> - the perpendicular distance from the chord's own
    ///     midpoint to the arc, positive bowing to one side and negative to the other) to
    ///     <paramref name="builder"/>, matching the reference <c>libvisio</c> implementation's own
    ///     <c>collectArcTo</c> radius/sweep derivation. A zero bow degrades to a straight
    ///     <see cref="PathBuilder.LineTo"/> (the documented "no bow" case - a straight chord).
    /// </summary>
    /// <param name="builder">The path builder to append to.</param>
    /// <param name="start">The current pen position (the arc's own start point), in local space.</param>
    /// <param name="cells">The row's own cell bag.</param>
    /// <returns>The arc's end point (<paramref name="builder"/>'s new current pen position).</returns>
    private static Vector2 AppendArcTo(PathBuilder builder, Vector2 start, VsdxCellBag cells)
    {
        var end = ToLocalPoint(cells.GetDouble("X"), cells.GetDouble("Y"));
        var bow = cells.GetDouble("A");

        if (bow == 0d)
        {
            builder.LineTo(end);
            return end;
        }

        var chord = Vector2.Distance(start, end);
        var radius = (float)(((4 * bow * bow) + (chord * chord)) / (8 * Math.Abs(bow)));
        var largeArc = Math.Abs(bow) > radius;
        var sweep = bow < 0;

        builder.ArcTo(new Vector2(radius, radius), 0f, largeArc, sweep, end);
        return end;
    }

    /// <summary>Converts a shape-local X/Y pair (inches) into the <see cref="Vector2"/> coordinate <see cref="PathBuilder"/> expects.</summary>
    /// <param name="x">The local X coordinate, in inches.</param>
    /// <param name="y">The local Y coordinate, in inches.</param>
    /// <returns>The corresponding <see cref="Vector2"/>.</returns>
    private static Vector2 ToLocalPoint(double x, double y) => new((float)x, (float)y);
}
