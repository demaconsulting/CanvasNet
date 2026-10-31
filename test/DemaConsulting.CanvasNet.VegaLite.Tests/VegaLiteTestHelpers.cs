using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     Shared helpers of the VegaLite tests: pixel snapshots and small pixel predicates.
/// </summary>
internal static class VegaLiteTestHelpers
{
    /// <summary>The default background of a chart: opaque white.</summary>
    public static readonly Rgba32 White = new(255, 255, 255, 255);

    /// <summary>
    ///     Copies all pixels of a surface into one byte array (row-major, four bytes per pixel).
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <returns>The pixel bytes without any row padding.</returns>
    public static byte[] Snapshot(Surface surface)
    {
        var bytes = new byte[surface.Width * surface.Height * 4];
        for (var y = 0; y < surface.Height; y++)
        {
            surface.GetRowSpanBytes(y).CopyTo(bytes.AsSpan(y * surface.Width * 4));
        }

        return bytes;
    }

    /// <summary>
    ///     Determines whether every pixel of a surface has the given color.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="color">The expected color.</param>
    /// <returns><see langword="true"/> when every pixel equals <paramref name="color"/>.</returns>
    public static bool AllPixelsAre(Surface surface, Rgba32 color)
    {
        for (var y = 0; y < surface.Height; y++)
        {
            foreach (var pixel in surface.GetRowSpan(y))
            {
                if (pixel != color)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    ///     Determines whether every pixel of a rectangular region of a surface has the given color.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="left">The first column of the region.</param>
    /// <param name="top">The first row of the region.</param>
    /// <param name="right">The column after the region.</param>
    /// <param name="bottom">The row after the region.</param>
    /// <param name="color">The expected color.</param>
    /// <returns><see langword="true"/> when every pixel of the region equals <paramref name="color"/>.</returns>
    public static bool RegionIs(Surface surface, int left, int top, int right, int bottom, Rgba32 color)
    {
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                if (surface[x, y] != color)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    ///     Finds the smallest rectangle that contains every pixel of a surface that differs from a background color.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="background">The color that does not count as ink.</param>
    /// <returns>The inclusive bounds of the ink, or <see langword="null"/> when every pixel has the background color.</returns>
    public static (int Left, int Top, int Right, int Bottom)? InkBounds(Surface surface, Rgba32 background)
    {
        var left = int.MaxValue;
        var top = int.MaxValue;
        var right = -1;
        var bottom = -1;
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                if (surface[x, y] == background)
                {
                    continue;
                }

                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }

        return right < 0 ? null : (left, top, right, bottom);
    }

    /// <summary>
    ///     Determines whether a painted color is within a tolerance of an expected color in every channel.
    /// </summary>
    /// <param name="expected">The expected color.</param>
    /// <param name="actual">The painted color.</param>
    /// <param name="tolerance">The largest accepted difference of a channel.</param>
    /// <returns><see langword="true"/> when the colors are close.</returns>
    public static bool Near(Rgba32 expected, Rgba32 actual, int tolerance = 2) =>
        Math.Abs(actual.R - expected.R) <= tolerance && Math.Abs(actual.G - expected.G) <= tolerance && Math.Abs(actual.B - expected.B) <= tolerance;

    /// <summary>
    ///     Determines whether a pixel near a point has a color: a line that is two pixels wide and not aligned to the pixel grid still covers one
    ///     pixel of the neighborhood completely.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="x">The x position of the point on the surface.</param>
    /// <param name="y">The y position of the point on the surface.</param>
    /// <param name="color">The expected color.</param>
    /// <param name="radius">The number of pixels around the pixel that holds the point that are examined.</param>
    /// <returns><see langword="true"/> when a pixel of the neighborhood has the color.</returns>
    public static bool HasInk(Surface surface, double x, double y, Rgba32 color, int radius = 1)
    {
        var cx = (int)Math.Floor(x);
        var cy = (int)Math.Floor(y);
        for (var py = cy - radius; py <= cy + radius; py++)
        {
            for (var px = cx - radius; px <= cx + radius; px++)
            {
                if (px >= 0 && py >= 0 && px < surface.Width && py < surface.Height && Near(color, surface[px, py]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     Counts the pixels of a rectangular region of a surface that are close to a color.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="color">The color.</param>
    /// <param name="left">The first column of the region.</param>
    /// <param name="top">The first row of the region.</param>
    /// <param name="right">The column after the region.</param>
    /// <param name="bottom">The row after the region.</param>
    /// <returns>The number of pixels.</returns>
    public static int CountNear(Surface surface, Rgba32 color, int left, int top, int right, int bottom)
    {
        var count = 0;
        for (var y = Math.Max(top, 0); y < Math.Min(bottom, surface.Height); y++)
        {
            for (var x = Math.Max(left, 0); x < Math.Min(right, surface.Width); x++)
            {
                if (Near(color, surface[x, y]))
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>
    ///     Reduces a data reference to the parts that do not depend on the source name, so documents opened from
    ///     different sources can be compared.
    /// </summary>
    /// <param name="reference">The reference.</param>
    /// <returns>A comparable tuple.</returns>
    public static (VegaLiteDataKind Kind, string? Url, long? Rows, string Pointer, int Line, int Column) Anonymous(VegaLiteDataReference reference) =>
        (reference.Kind, reference.Url, reference.RowCount, reference.Location.Pointer, reference.Location.Line, reference.Location.Column);
}
