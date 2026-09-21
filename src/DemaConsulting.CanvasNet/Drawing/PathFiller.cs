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
    ///     Thrown when <paramref name="flattenTolerance"/> is less than or equal to zero.
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

        if (flattenTolerance <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(flattenTolerance), flattenTolerance, "Tolerance must be greater than zero.");
        }

        var pathBounds = path.GetBounds(flattenTolerance);
        var surfaceBounds = new Rect(0, 0, surface.Width, surface.Height);
        var clipBounds = Rect.Intersect(pathBounds, surfaceBounds);
        if (clipBounds.IsEmpty)
        {
            return;
        }

        var polygons = EdgeFlattener.Flatten(path, flattenTolerance);
        ScanlineRasterizer.Fill(surface, polygons, color, fillRule, clipBounds);
    }
}
