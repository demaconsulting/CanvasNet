// cspell:ignore Bezier Casteljau
using System.Numerics;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Geometry;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using GeometryPath = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <content>
///     The reference height of a font and the flattening of glyph outlines into polygon contours.
/// </content>
internal static partial class ScadTextGeometry
{
    /// <summary>The deepest subdivision of one curve; at most <c>2^MaxSubdivisionDepth</c> segments, a bound on the work of a hostile outline.</summary>
    private const int MaxSubdivisionDepth = 10;

    /// <summary>The fraction of an em used as the flattening tolerance when measuring the reference glyph (about 0.02% of the em).</summary>
    private const double ReferenceToleranceEm = 1.0 / 4096.0;

    /// <summary>The Unicode code point of the glyph whose ink height is the reference height of a font.</summary>
    private const int ReferenceCodePoint = 'H';

    /// <summary>
    ///     Measures the reference height of a font, the height in font design units that <c>size</c> maps to.
    /// </summary>
    /// <remarks>
    ///     This is the ink height of <c>H</c> (a deterministic cap-height measurement that needs no <c>OS/2</c>
    ///     metrics, which fonts fill in inconsistently). A font without a usable <c>H</c> falls back to the larger of
    ///     its absolute ascender and its units per em, so a text always has a positive scale.
    /// </remarks>
    /// <param name="font">The font.</param>
    /// <param name="context">The context polled for cancellation before and during the flattening of the reference glyph.</param>
    /// <returns>The reference height in font design units; always finite and greater than zero.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the build is canceled.</exception>
    internal static double ReferenceHeight(TrueTypeFont font, ScadGeometryContext context)
    {
        // The reference outline is flattened at a fine tolerance, so cancellation gets its chance before the work starts
        context.Poll();
        var glyph = font.GetGlyphIndex(ReferenceCodePoint);
        if (glyph != 0 && TryFlatten(font, glyph, font.UnitsPerEm * ReferenceToleranceEm, context, out var contours, out _) && contours.Length > 0)
        {
            var heights = contours.SelectMany(contour => contour).Select(point => point.Y).ToArray();
            var height = heights.Max() - heights.Min();
            if (double.IsFinite(height) && height > 0.0)
            {
                return height;
            }
        }

        var fallback = Math.Max(Math.Abs((double)font.Ascender), font.UnitsPerEm);
        return fallback > 0.0 ? fallback : 1.0;
    }

    /// <summary>
    ///     Flattens the outline of a glyph into closed polygon contours in font design units.
    /// </summary>
    /// <remarks>
    ///     Lines are copied exactly; quadratic and cubic curves are flattened by iterative de Casteljau subdivision
    ///     until the control points are within <paramref name="tolerance"/> of the chord (or the depth bound is
    ///     reached), always in the same order, so the result is deterministic. Every subpath is implicitly closed.
    ///     A subpath with fewer than three distinct vertices or no area has no ink and is dropped; an empty outline
    ///     (a space) gives no contours and is not degenerate, whereas an outline whose subpaths are all dropped is
    ///     reported through <paramref name="degenerate"/> so the caller can tell it from a space.
    /// </remarks>
    /// <param name="font">The font.</param>
    /// <param name="glyph">The glyph index.</param>
    /// <param name="tolerance">The largest distance between a curve and its flattening, in font units; greater than zero.</param>
    /// <param name="context">The context polled for cancellation after every contour.</param>
    /// <param name="contours">The contours; empty when the glyph has no outline.</param>
    /// <param name="degenerate"><see langword="true"/> when the outline has drawing subpaths but none of them has ink.</param>
    /// <returns><see langword="false"/> when the glyph outline is damaged and cannot be read.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the build is canceled.</exception>
    private static bool TryFlatten(TrueTypeFont font, int glyph, double tolerance, ScadGeometryContext context, out Vector2D[][] contours, out bool degenerate)
    {
        degenerate = false;
        GeometryPath outline;
        try
        {
            outline = font.GetGlyphOutline(glyph);
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentOutOfRangeException)
        {
            contours = [];
            return false;
        }

        var result = new List<Vector2D[]>();
        var dropped = false;
        foreach (var subpath in outline.Subpaths)
        {
            var points = FlattenSubpath(subpath, tolerance);
            if (points.Count >= 3 && HasArea(points))
            {
                result.Add([.. points]);
            }
            else if (subpath.Commands.Count > 0)
            {
                // A subpath that draws something but encloses nothing
                dropped = true;
            }

            context.Poll();
        }

        degenerate = dropped && result.Count == 0;
        contours = [.. result];
        return true;
    }

    /// <summary>
    ///     Flattens one subpath into a polygon.
    /// </summary>
    /// <param name="subpath">The subpath.</param>
    /// <param name="tolerance">The flattening tolerance in font units.</param>
    /// <returns>The vertices without consecutive duplicates and without a closing duplicate.</returns>
    private static List<Vector2D> FlattenSubpath(Subpath subpath, double tolerance)
    {
        var points = new List<Vector2D>();
        var current = ToVector(subpath.Start);
        points.Add(current);
        foreach (var command in subpath.Commands)
        {
            var end = ToVector(command.EndPoint);
            switch (command.Type)
            {
                case PathCommandType.LineTo:
                    points.Add(end);
                    break;
                case PathCommandType.QuadraticBezierTo:
                    // A quadratic is the cubic with the control points two thirds of the way to the single control point
                    var control = ToVector(command.Control1);
                    FlattenCubic(
                        current,
                        current + ((control - current) * (2.0 / 3.0)),
                        end + ((control - end) * (2.0 / 3.0)),
                        end,
                        tolerance,
                        points);
                    break;
                case PathCommandType.CubicBezierTo:
                    FlattenCubic(current, ToVector(command.Control1), ToVector(command.Control2), end, tolerance, points);
                    break;
                case PathCommandType.ArcTo:
                    // Fonts do not draw arcs; a stray one is approximated by its chord
                    points.Add(end);
                    break;
                default:
                    // Close: the polygon is implicitly closed
                    break;
            }

            if (command.Type == PathCommandType.Close)
            {
                break;
            }

            current = end;
        }

        // Drop consecutive duplicates and a closing duplicate of the first vertex
        var cleaned = new List<Vector2D>(points.Count);
        foreach (var point in points)
        {
            if (cleaned.Count == 0 || cleaned[^1] != point)
            {
                cleaned.Add(point);
            }
        }

        while (cleaned.Count > 1 && cleaned[^1] == cleaned[0])
        {
            cleaned.RemoveAt(cleaned.Count - 1);
        }

        return cleaned;
    }

    /// <summary>
    ///     Flattens a cubic Bezier curve by iterative de Casteljau subdivision, appending every vertex after the
    ///     start up to and including the end.
    /// </summary>
    /// <param name="p0">The start.</param>
    /// <param name="p1">The first control point.</param>
    /// <param name="p2">The second control point.</param>
    /// <param name="p3">The end.</param>
    /// <param name="tolerance">The tolerance in font units.</param>
    /// <param name="output">The vertex list to append to.</param>
    private static void FlattenCubic(Vector2D p0, Vector2D p1, Vector2D p2, Vector2D p3, double tolerance, List<Vector2D> output)
    {
        // An explicit stack keeps the order (first half before second half) without recursion
        var stack = new Stack<(Vector2D A, Vector2D B, Vector2D C, Vector2D D, int Depth)>();
        stack.Push((p0, p1, p2, p3, 0));
        while (stack.Count > 0)
        {
            var (a, b, c, d, depth) = stack.Pop();
            if (depth >= MaxSubdivisionDepth || ControlDistance(a, b, c, d) <= tolerance)
            {
                output.Add(d);
                continue;
            }

            // Split at the middle: the left curve is a..abcd, the right one abcd..d
            var ab = Midpoint(a, b);
            var bc = Midpoint(b, c);
            var cd = Midpoint(c, d);
            var abc = Midpoint(ab, bc);
            var bcd = Midpoint(bc, cd);
            var abcd = Midpoint(abc, bcd);
            stack.Push((abcd, bcd, cd, d, depth + 1));
            stack.Push((a, ab, abc, abcd, depth + 1));
        }
    }

    /// <summary>
    ///     Measures how far the control points of a cubic are from its chord, an upper bound of the distance between
    ///     the curve and the chord.
    /// </summary>
    /// <param name="a">The start.</param>
    /// <param name="b">The first control point.</param>
    /// <param name="c">The second control point.</param>
    /// <param name="d">The end.</param>
    /// <returns>The larger distance of the two control points from the chord (from the start when the chord is a point).</returns>
    private static double ControlDistance(Vector2D a, Vector2D b, Vector2D c, Vector2D d)
    {
        var chord = d - a;
        var length = Math.Sqrt((chord.X * chord.X) + (chord.Y * chord.Y));
        if (length <= 0.0)
        {
            return Math.Max(Distance(a, b), Distance(a, c));
        }

        // Distance from the infinite line through a and d
        var db = Math.Abs(((b.X - a.X) * chord.Y) - ((b.Y - a.Y) * chord.X)) / length;
        var dc = Math.Abs(((c.X - a.X) * chord.Y) - ((c.Y - a.Y) * chord.X)) / length;
        return Math.Max(db, dc);
    }

    /// <summary>
    ///     Computes the distance between two points.
    /// </summary>
    /// <param name="a">The first point.</param>
    /// <param name="b">The second point.</param>
    /// <returns>The distance.</returns>
    private static double Distance(Vector2D a, Vector2D b)
    {
        var delta = b - a;
        return Math.Sqrt((delta.X * delta.X) + (delta.Y * delta.Y));
    }

    /// <summary>
    ///     Computes the midpoint of two points.
    /// </summary>
    /// <param name="a">The first point.</param>
    /// <param name="b">The second point.</param>
    /// <returns>The midpoint.</returns>
    private static Vector2D Midpoint(Vector2D a, Vector2D b) => new((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);

    /// <summary>
    ///     Converts a path point (single precision) to a double-precision vector.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <returns>The vector.</returns>
    private static Vector2D ToVector(Vector2 point) => new(point.X, point.Y);

    /// <summary>
    ///     Tests whether a polygon has a non-zero area (the shoelace sum).
    /// </summary>
    /// <param name="points">The vertices.</param>
    /// <returns><see langword="true"/> when the signed area is not zero.</returns>
    private static bool HasArea(List<Vector2D> points)
    {
        var sum = 0.0;
        for (var i = 0; i < points.Count; i++)
        {
            var next = points[(i + 1) % points.Count];
            sum += (points[i].X * next.Y) - (next.X * points[i].Y);
        }

        return sum != 0.0;
    }
}
