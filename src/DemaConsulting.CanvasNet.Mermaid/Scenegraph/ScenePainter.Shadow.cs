using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Mermaid.Scenegraph;

/// <content>
///     <para>
///         Drop shadows (<see cref="ScenePath.Shadow"/>): CSS's <c>filter: drop-shadow(dx dy blur color)</c>, which Mermaid's
///         themes give the boxes of a sequence diagram. The path's fill and stroke are drawn into a mask moved by the
///         offset, the mask's coverage is blurred with the Gaussian approximation the SVG filter specification
///         describes (three box blurs of size <c>d = floor(s * 3 * sqrt(2 * pi) / 4 + 0.5)</c> in each direction, for an
///         even <c>d</c> two of size <c>d</c> offset by half a pixel either way and one of size <c>d + 1</c>), and the
///         shadow color is painted through it under the path.
///     </para>
///     <para>
///         The Svg package has its own private filter code for the same effect; it is not shared, because the Mermaid
///         package depends on the core only. The blur is clamped to <see cref="MaxShadowBlur"/> device pixels. The
///         scratch region (<see cref="ShadowRegion"/>) is the shadow's own bounds - the path's device bounds grown by
///         the reach of its drawn stroke, moved by the offset, and grown by the blur's reach - kept to the surface and
///         the blur's reach around it, so a small shadow on a large surface gets a small region. A stroke that is not
///         drawn (<see cref="DeviceStrokeWidth"/> is zero, for example a width beyond <see cref="MaxMagnitude"/>)
///         does not grow it. The whole region, its margins beyond the surface included, is charged to
///         <see cref="MermaidLimits.MaxPaintedPixels"/> before any scratch is allocated.
///     </para>
///     <para>
///         Scratch memory: the silhouette is drawn into an RGBA mask (4 bytes a region pixel), whose alpha is copied
///         into a byte buffer (1 byte) before the mask is released; the float coverage (4 bytes) is allocated after
///         that, so the peak is about 5 bytes a region pixel (and one row or column of running sums). The blur keeps the
///         whole region in floats, because computing it in bounded strips would change the rounding of the running
///         sums or recompute the horizontal pass once per strip.
///     </para>
/// </content>
internal sealed partial class ScenePainter
{
    /// <summary>The largest blur standard deviation in device pixels that a shadow is drawn with.</summary>
    internal const double MaxShadowBlur = 64;

    /// <summary>The miter limit of strokes, which bounds how far a stroke reaches past its path in stroke widths.</summary>
    private const double StrokeReach = 2;

    /// <summary>
    ///     Paints the drop shadow of a path item under it.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="item">The path item.</param>
    /// <param name="shadow">The shadow.</param>
    /// <param name="placement">The placement.</param>
    /// <param name="bounds">The device bounds of the item's geometry.</param>
    /// <exception cref="MermaidRenderException">Thrown when the shadow would pass the painted-pixel limit.</exception>
    private void PaintShadow(Surface surface, ScenePath item, SceneShadow shadow, ScenePlacement placement, Geometry.Rect bounds)
    {
        // The offset and blur in device pixels; a transparent or unbounded shadow draws nothing
        var dx = shadow.OffsetX * placement.Scale;
        var dy = shadow.OffsetY * placement.Scale;
        var sigma = shadow.Blur * placement.Scale;
        if (shadow.Color.A == 0 || !IsBounded(dx, dy) || !double.IsFinite(sigma) || sigma < 0)
        {
            return;
        }

        sigma = Math.Min(sigma, MaxShadowBlur);
        var boxSize = sigma > 0 ? (int)Math.Floor((sigma * 3 * Math.Sqrt(2 * Math.PI) / 4) + 0.5) : 0;
        var reach = boxSize > 0 ? 3 * ((boxSize / 2) + 1) : 0;

        // The region of the moved silhouette and the blur's reach; a stroke that is not drawn (see DeviceStrokeWidth)
        // does not grow it, so the shadow is the fill's alone, as drawn
        var stroke = item.Stroke is { } s && DeviceStrokeWidth(s, placement.Scale) > 0 ? s.Width * placement.Scale * StrokeReach : 0;
        if (ShadowRegion(bounds, stroke, dx, dy, reach, surface.Width, surface.Height) is not { } region)
        {
            return;
        }

        // The whole scratch region, its margins beyond the surface included, is charged before it is allocated
        var (left, top, right, bottom) = region;
        var width = right - left;
        var height = bottom - top;
        ChargeArea((long)width * height, item);

        // The silhouette: the fill and stroke drawn into the mask, moved by the offset; only its alpha is kept (one byte
        // a pixel), so the mask is released before the coverage is allocated
        var alpha = new byte[width * height];
        using (var mask = new Surface(width, height))
        {
            var maskPlacement = placement with { OffsetX = placement.OffsetX + dx - left, OffsetY = placement.OffsetY + dy - top };
            var path = Scene.ToPath(item.Segments, maskPlacement.Scale, maskPlacement.OffsetX, maskPlacement.OffsetY);
            FillAndStroke(mask, item, path, maskPlacement, path.GetBounds(), charge: false);
            for (var y = 0; y < height; y++)
            {
                var row = mask.GetRowSpan(y);
                for (var x = 0; x < width; x++)
                {
                    alpha[(y * width) + x] = row[x].A;
                }
            }
        }

        var coverage = new float[width * height];
        for (var i = 0; i < coverage.Length; i++)
        {
            coverage[i] = alpha[i] / 255f;
        }

        // The blur, then the shadow color painted through the coverage on the surface
        if (boxSize > 0)
        {
            Blur(coverage, width, height, boxSize, horizontal: true);
            Blur(coverage, width, height, boxSize, horizontal: false);
        }

        var startX = Math.Max(left, 0);
        var endX = Math.Min(right, surface.Width);
        for (var y = Math.Max(top, 0); y < Math.Min(bottom, surface.Height); y++)
        {
            var span = new ReadOnlySpan<float>(coverage, ((y - top) * width) + (startX - left), endX - startX);
            surface.CompositeOverSpan(y, startX, span, shadow.Color);
        }
    }

    /// <summary>
    ///     Gets the scratch region of a drop shadow in device pixels: the device bounds of the path grown by the stroke's
    ///     reach, moved by the offset, and grown by the blur's reach - the shadow's own bounds, not the surface's - kept to
    ///     the surface and the blur's reach around it, and kept to the surface alone when that would make a side longer
    ///     than <see cref="Surface.MaxDimension"/>. The edges are computed in double precision and clamped before they
    ///     become integers, so no value overflows.
    /// </summary>
    /// <param name="bounds">The device bounds of the path's geometry.</param>
    /// <param name="strokeReach">How far the drawn stroke reaches past the geometry in device pixels; zero without one.</param>
    /// <param name="dx">The device x offset of the shadow.</param>
    /// <param name="dy">The device y offset of the shadow.</param>
    /// <param name="blurReach">How far the blur reaches in device pixels; zero for a sharp shadow.</param>
    /// <param name="surfaceWidth">The width of the surface.</param>
    /// <param name="surfaceHeight">The height of the surface.</param>
    /// <returns>The region, left and top inclusive and right and bottom exclusive, or null when none of it is on the surface.</returns>
    internal static (int Left, int Top, int Right, int Bottom)? ShadowRegion(
        Geometry.Rect bounds,
        double strokeReach,
        double dx,
        double dy,
        int blurReach,
        int surfaceWidth,
        int surfaceHeight)
    {
        var min = -(double)blurReach;
        var maxX = (double)surfaceWidth + blurReach;
        var maxY = (double)surfaceHeight + blurReach;
        var l = Math.Clamp(Math.Floor(bounds.X - strokeReach + dx) - blurReach, min, maxX);
        var t = Math.Clamp(Math.Floor(bounds.Y - strokeReach + dy) - blurReach, min, maxY);
        var r = Math.Clamp(Math.Ceiling(bounds.Right + strokeReach + dx) + blurReach, min, maxX);
        var b = Math.Clamp(Math.Ceiling(bounds.Bottom + strokeReach + dy) + blurReach, min, maxY);
        if (double.IsNaN(l) || double.IsNaN(t) || double.IsNaN(r) || double.IsNaN(b))
        {
            return null;
        }

        var (left, top, right, bottom) = ((int)l, (int)t, (int)r, (int)b);
        if (right - left > Surface.MaxDimension || bottom - top > Surface.MaxDimension)
        {
            (left, right) = (Math.Max(left, 0), Math.Min(right, surfaceWidth));
            (top, bottom) = (Math.Max(top, 0), Math.Min(bottom, surfaceHeight));
        }

        if (right <= left || bottom <= top || right <= 0 || bottom <= 0 || left >= surfaceWidth || top >= surfaceHeight)
        {
            return null;
        }

        return (left, top, right, bottom);
    }

    /// <summary>
    ///     Blurs coverage in one direction with the three box blurs of the SVG specification's Gaussian approximation.
    /// </summary>
    /// <param name="data">The coverage, row by row.</param>
    /// <param name="width">The number of columns.</param>
    /// <param name="height">The number of rows.</param>
    /// <param name="size">The box size <c>d</c>; at least one.</param>
    /// <param name="horizontal">Whether the rows are blurred (otherwise the columns).</param>
    private static void Blur(float[] data, int width, int height, int size, bool horizontal)
    {
        if (size % 2 == 1)
        {
            // Three boxes of size d centered on the output pixel
            var half = (size - 1) / 2;
            for (var pass = 0; pass < 3; pass++)
            {
                BoxPass(data, width, height, half, half, horizontal);
            }

            return;
        }

        // Two boxes of size d centered on the pixel's left and right boundaries, and one of size d + 1 centered on it
        BoxPass(data, width, height, size / 2, (size / 2) - 1, horizontal);
        BoxPass(data, width, height, (size / 2) - 1, size / 2, horizontal);
        BoxPass(data, width, height, size / 2, size / 2, horizontal);
    }

    /// <summary>
    ///     Applies one box blur in one direction: each value becomes the mean of a window around it, with zero beyond the
    ///     edges, computed with running sums so the cost does not depend on the window.
    /// </summary>
    /// <param name="data">The coverage, row by row.</param>
    /// <param name="width">The number of columns.</param>
    /// <param name="height">The number of rows.</param>
    /// <param name="before">The number of values before the output value in the window.</param>
    /// <param name="after">The number of values after the output value in the window.</param>
    /// <param name="horizontal">Whether the rows are blurred (otherwise the columns).</param>
    private static void BoxPass(float[] data, int width, int height, int before, int after, bool horizontal)
    {
        var count = horizontal ? width : height;
        var lines = horizontal ? height : width;
        var stride = horizontal ? 1 : width;
        var size = before + after + 1;
        var sums = new double[count + 1];
        for (var line = 0; line < lines; line++)
        {
            var start = horizontal ? line * width : line;
            for (var i = 0; i < count; i++)
            {
                sums[i + 1] = sums[i] + data[start + (i * stride)];
            }

            for (var i = 0; i < count; i++)
            {
                var sum = sums[Math.Min(i + after + 1, count)] - sums[Math.Max(i - before, 0)];
                data[start + (i * stride)] = (float)(sum / size);
            }
        }
    }
}
