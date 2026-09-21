using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

// cspell:ignore Outliner

namespace DemaConsulting.CanvasNet.Drawing;

/// <summary>
///     Converts a vector path stroke into ordinary filled outline geometry consumable by
///     <see cref="PathFiller"/>.
/// </summary>
/// <remarks>
///     <see cref="Stroke(Path, StrokeStyle, float)"/> performs stroke-to-fill conversion only:
///     it returns a new <see cref="Path"/> whose closed subpaths describe the stroked area, ready
///     for a caller to render via <see cref="PathFiller.Fill(Canvas.Surface, Path, Canvas.Rgba32, FillRule, float)"/> using
///     <see cref="FillRule.NonZero"/>. Rasterization stays entirely on the existing fill code
///     path, so stroking introduces no second rasterizer with independent antialiasing behavior.
/// </remarks>
public static class PathStroker
{
    /// <summary>
    ///     Converts <paramref name="path"/> into closed outline geometry representing its stroke.
    /// </summary>
    /// <param name="path">The vector path to stroke. Must not be <see langword="null"/>.</param>
    /// <param name="style">The stroke style to apply. Must not be <see langword="null"/>.</param>
    /// <param name="flattenTolerance">
    ///     The maximum allowed deviation between each curve in <paramref name="path"/> and the
    ///     polyline used to approximate it before dashing and outlining. Must be finite and
    ///     greater than zero. Defaults to <c>0.25f</c>.
    /// </param>
    /// <returns>
    ///     A new <see cref="Path"/> containing one closed subpath per generated outline polygon,
    ///     or <see cref="Path.Empty"/> if the input path contributes no stroked area.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="path"/> or <paramref name="style"/> is
    ///     <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="flattenTolerance"/> is not finite or is less than or equal
    ///     to zero.
    /// </exception>
    public static Path Stroke(Path path, StrokeStyle style, float flattenTolerance = 0.25f)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(style);

        if (!float.IsFinite(flattenTolerance) || flattenTolerance <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(flattenTolerance),
                flattenTolerance,
                "Tolerance must be a finite value greater than zero.");
        }

        var flattened = StrokePathFlattener.Flatten(path, flattenTolerance);
        if (flattened.Count == 0)
        {
            return Path.Empty;
        }

        var builder = new PathBuilder();
        var polygonCount = 0;
        foreach (var subpath in flattened)
        {
            var dashedSegments = DashSplitter.Split(
                subpath.Points,
                subpath.IsClosed,
                style.DashArray,
                style.DashOffset);

            foreach (var dashedSegment in dashedSegments)
            {
                var outlines = StrokeOutliner.Outline(
                    dashedSegment.Points,
                    dashedSegment.IsClosed,
                    style,
                    flattenTolerance);

                foreach (var outline in outlines)
                {
                    if (outline.Count < 3)
                    {
                        continue;
                    }

                    builder.MoveTo(outline[0]);
                    for (var i = 1; i < outline.Count; i++)
                    {
                        builder.LineTo(outline[i]);
                    }

                    builder.Close();
                    polygonCount++;
                }
            }
        }

        return polygonCount == 0 ? Path.Empty : builder.Build();
    }
}
