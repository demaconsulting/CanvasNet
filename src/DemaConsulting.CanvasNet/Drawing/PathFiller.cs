using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Drawing;

/// <summary>
///     Fills closed <see cref="Path"/> geometry onto a <see cref="Surface"/> with a solid color,
///     using an antialiased scanline-coverage rasterizer.
/// </summary>
public static class PathFiller
{
    /// <summary>
    ///     Fills <paramref name="path"/> onto <paramref name="surface"/> with <paramref name="color"/>.
    /// </summary>
    /// <param name="surface">The surface to fill into. Must not be <see langword="null"/>.</param>
    /// <param name="path">The path to fill. Must not be <see langword="null"/>.</param>
    /// <param name="color">The solid color to paint, scaled per pixel by antialiased fill coverage.</param>
    /// <param name="fillRule">
    ///     The rule used to resolve overlapping or self-intersecting geometry. Defaults to
    ///     <see cref="FillRule.NonZero"/>.
    /// </param>
    /// <param name="flattenTolerance">
    ///     The maximum allowed perpendicular deviation between each curve in <paramref name="path"/>
    ///     and the polyline used to approximate it for filling. Must be greater than zero.
    ///     Defaults to <c>0.25f</c>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="surface"/> or <paramref name="path"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="fillRule"/> is not a defined <see cref="FillRule"/> value,
    ///     or when <paramref name="flattenTolerance"/> is not a finite value greater than zero
    ///     (this includes <see cref="float.NaN"/> and either infinity, not only zero or negative
    ///     values).
    /// </exception>
    /// <remarks>
    ///     <para>
    ///     Every subpath in <paramref name="path"/> is treated as implicitly closed for filling
    ///     purposes, regardless of <see cref="Subpath.IsClosed"/> - an open subpath fills exactly
    ///     as if a closing line had been drawn back to its start point. See
    ///     <see cref="EdgeFlattener"/>'s remarks for details.
    ///     </para>
    ///     <para>
    ///     This method has no <c>transform</c> parameter: <paramref name="path"/>'s coordinates are
    ///     always interpreted directly as surface pixel-space coordinates. Transformed filling is
    ///     left to a future phase (see the <c>Drawing</c> namespace's documentation).
    ///     </para>
    ///     <para>
    ///     The fill is a no-op - <paramref name="surface"/> is left completely unmodified, and no
    ///     exception is thrown - when <paramref name="path"/> has an empty bounding box (for
    ///     example <see cref="Path.Empty"/>, or a path containing only zero-length subpaths), or
    ///     when <paramref name="path"/>'s bounding box does not overlap <paramref name="surface"/>'s
    ///     pixel extent at all.
    ///     </para>
    ///     <para>
    ///     <paramref name="path"/> is flattened via <see cref="EdgeFlattener.Flatten"/> exactly
    ///     once: the resulting polygons' own vertices are reused both to compute the clip bounds
    ///     below (rather than calling <see cref="Path.GetBounds"/>, which would independently
    ///     re-flatten every curve a second time) and to rasterize. This avoids paying curve
    ///     subdivision cost twice for the same path.
    ///     </para>
    /// </remarks>
    public static void Fill(
        Surface surface,
        Path path,
        Rgba32 color,
        FillRule fillRule = FillRule.NonZero,
        float flattenTolerance = 0.25f)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(path);
        ValidateFillArgs(fillRule, flattenTolerance);

        if (!TryFlattenForFill(surface, path, flattenTolerance, out var polygons, out var clipBounds))
        {
            return;
        }

        ScanlineRasterizer.Fill(surface, polygons, color, fillRule, clipBounds);
    }

    /// <summary>
    ///     Fills <paramref name="path"/> onto <paramref name="surface"/> with <paramref name="paint"/>,
    ///     a linear or radial gradient evaluated once per pixel and scaled by that pixel's
    ///     antialiased fill coverage.
    /// </summary>
    /// <param name="surface">The surface to fill into. Must not be <see langword="null"/>.</param>
    /// <param name="path">The path to fill. Must not be <see langword="null"/>.</param>
    /// <param name="paint">The gradient to paint. Must not be <see langword="null"/>.</param>
    /// <param name="fillRule">
    ///     The rule used to resolve overlapping or self-intersecting geometry. Defaults to
    ///     <see cref="FillRule.NonZero"/>.
    /// </param>
    /// <param name="flattenTolerance">
    ///     The maximum allowed perpendicular deviation between each curve in <paramref name="path"/>
    ///     and the polyline used to approximate it for filling. Must be greater than zero.
    ///     Defaults to <c>0.25f</c>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="surface"/>, <paramref name="path"/>, or <paramref name="paint"/>
    ///     is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="fillRule"/> is not a defined <see cref="FillRule"/> value,
    ///     or when <paramref name="flattenTolerance"/> is not a finite value greater than zero.
    /// </exception>
    /// <remarks>
    ///     <para>
    ///     Shares every bit of flattening, clip-bounds computation, and argument validation with
    ///     the solid-color <see cref="Fill(Surface, Path, Rgba32, FillRule, float)"/> overload via
    ///     the private <see cref="TryFlattenForFill"/> helper - the only difference between the
    ///     two overloads is which <c>ScanlineRasterizer.Fill</c> entry point (and therefore
    ///     which final per-row compositing behavior) is invoked.
    ///     </para>
    ///     <para>
    ///     See <see cref="Fill(Surface, Path, Rgba32, FillRule, float)"/>'s remarks for the shared
    ///     "every subpath is treated as implicitly closed", "no-op on an empty/out-of-bounds path",
    ///     and "no transform parameter" documentation, all of which apply identically here.
    ///     </para>
    /// </remarks>
    public static void Fill(
        Surface surface,
        Path path,
        Gradient paint,
        FillRule fillRule = FillRule.NonZero,
        float flattenTolerance = 0.25f)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(paint);
        ValidateFillArgs(fillRule, flattenTolerance);

        if (!TryFlattenForFill(surface, path, flattenTolerance, out var polygons, out var clipBounds))
        {
            return;
        }

        ScanlineRasterizer.Fill(surface, polygons, paint, fillRule, clipBounds);
    }

    /// <summary>
    ///     Validates the <paramref name="fillRule"/>/<paramref name="flattenTolerance"/> arguments
    ///     shared by every <c>Fill</c> overload.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="fillRule"/> is not a defined <see cref="FillRule"/> value,
    ///     or when <paramref name="flattenTolerance"/> is not a finite value greater than zero.
    /// </exception>
    private static void ValidateFillArgs(FillRule fillRule, float flattenTolerance)
    {
        // Reject an undefined FillRule value here, at the public API boundary, rather than
        // letting it silently fall through ScanlineRasterizer's internal winding-resolution
        // "else" branch (which has an explicit case only for NonZero) and be treated as EvenOdd
        // without any indication the caller passed a meaningless value - matching the existing
        // Enum.IsDefined validation convention used by BmpCodec.Save/TiffCodec.Save for their own
        // enum parameters.
        if (!Enum.IsDefined(fillRule))
        {
            throw new ArgumentOutOfRangeException(
                nameof(fillRule), fillRule, "Fill rule must be a defined FillRule value.");
        }

        // Reject non-finite values (NaN or +/-Infinity), not only non-positive ones: NaN in
        // particular compares false against every relational operator (including "<= 0"), so a
        // naive non-positive check alone silently lets it through - which, left unchecked, later
        // makes every BezierFlattening flatness comparison fail and forces the recursive curve
        // subdivision to its maximum depth (allocating on the order of a million points) instead
        // of failing fast here with the documented exception.
        if (!float.IsFinite(flattenTolerance) || flattenTolerance <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(flattenTolerance), flattenTolerance, "Tolerance must be a finite value greater than zero.");
        }
    }

    /// <summary>
    ///     Flattens <paramref name="path"/> and computes its clip bounds, shared by every
    ///     <c>Fill</c> overload - see <see cref="Fill(Surface, Path, Rgba32, FillRule, float)"/>'s
    ///     remarks for why flattening happens exactly once here rather than being repeated by
    ///     <see cref="Path.GetBounds"/>.
    /// </summary>
    /// <returns>
    ///     <see langword="false"/> (with both <see langword="out"/> parameters left at their
    ///     default values) when the fill would be a no-op - <paramref name="clipBounds"/> is
    ///     empty, either because <paramref name="path"/> itself has an empty bounding box or
    ///     because it does not overlap <paramref name="surface"/>'s pixel extent at all;
    ///     <see langword="true"/> otherwise.
    /// </returns>
    private static bool TryFlattenForFill(
        Surface surface,
        Path path,
        float flattenTolerance,
        out List<List<Vector2>> polygons,
        out Rect clipBounds)
    {
        polygons = EdgeFlattener.Flatten(path, flattenTolerance);
        var pathBounds = GetPolygonBounds(polygons);
        var surfaceBounds = new Rect(0, 0, surface.Width, surface.Height);
        clipBounds = Rect.Intersect(pathBounds, surfaceBounds);
        return !clipBounds.IsEmpty;
    }


    /// <summary>
    ///     Computes the axis-aligned bounding box enclosing every vertex of every already-flattened
    ///     polygon in <paramref name="polygons"/>.
    /// </summary>
    /// <param name="polygons">The flattened polygons produced by <see cref="EdgeFlattener.Flatten"/>.</param>
    /// <returns>
    ///     The smallest axis-aligned rectangle enclosing every vertex, or <see cref="Rect.Empty"/>
    ///     if <paramref name="polygons"/> contains no vertices at all (for example, an empty path).
    /// </returns>
    private static Rect GetPolygonBounds(IReadOnlyList<List<Vector2>> polygons)
    {
        var bounds = Rect.Empty;

        foreach (var polygon in polygons)
        {
            foreach (var point in polygon)
            {
                bounds = bounds.Union(new Rect(point.X, point.Y, 0, 0));
            }
        }

        return bounds;
    }
}
