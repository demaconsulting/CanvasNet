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
///         package depends on the core only. The blur is clamped to <see cref="MaxShadowBlur"/> device pixels, the mask covers at most the surface
///         and the blur's reach around it, and the shadow's pixels on the surface are charged to
///         <see cref="MermaidLimits.MaxPaintedPixels"/>.
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

        // The region of the moved silhouette and the blur's reach, kept to the surface and the reach around it
        var stroke = item.Stroke is { Color.A: > 0 } s && s.Width > 0 && double.IsFinite(s.Width) ? s.Width * placement.Scale * StrokeReach : 0;
        var left = Math.Max((int)Math.Floor(bounds.X - stroke + dx) - reach, -reach);
        var top = Math.Max((int)Math.Floor(bounds.Y - stroke + dy) - reach, -reach);
        var right = Math.Min((int)Math.Ceiling(bounds.Right + stroke + dx) + reach, surface.Width + reach);
        var bottom = Math.Min((int)Math.Ceiling(bounds.Bottom + stroke + dy) + reach, surface.Height + reach);
        if (right - left > Surface.MaxDimension || bottom - top > Surface.MaxDimension)
        {
            (left, right) = (Math.Max(left, 0), Math.Min(right, surface.Width));
            (top, bottom) = (Math.Max(top, 0), Math.Min(bottom, surface.Height));
        }

        if (right <= left || bottom <= top || right <= 0 || bottom <= 0 || left >= surface.Width || top >= surface.Height)
        {
            return;
        }

        Charge(surface, left, top, right, bottom, item);

        // The silhouette: the fill and stroke drawn into the mask, moved by the offset
        var width = right - left;
        var height = bottom - top;
        float[] coverage;
        using (var mask = new Surface(width, height))
        {
            var maskPlacement = placement with { OffsetX = placement.OffsetX + dx - left, OffsetY = placement.OffsetY + dy - top };
            var path = Scene.ToPath(item.Segments, maskPlacement.Scale, maskPlacement.OffsetX, maskPlacement.OffsetY);
            FillAndStroke(mask, item, path, maskPlacement, path.GetBounds(), charge: false);
            coverage = new float[width * height];
            for (var y = 0; y < height; y++)
            {
                var row = mask.GetRowSpan(y);
                for (var x = 0; x < width; x++)
                {
                    coverage[(y * width) + x] = row[x].A / 255f;
                }
            }
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
