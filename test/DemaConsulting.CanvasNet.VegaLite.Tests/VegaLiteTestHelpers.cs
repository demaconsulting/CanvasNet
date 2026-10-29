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
    ///     Reduces a data reference to the parts that do not depend on the source name, so documents opened from
    ///     different sources can be compared.
    /// </summary>
    /// <param name="reference">The reference.</param>
    /// <returns>A comparable tuple.</returns>
    public static (VegaLiteDataKind Kind, string? Url, long? Rows, string Pointer, int Line, int Column) Anonymous(VegaLiteDataReference reference) =>
        (reference.Kind, reference.Url, reference.RowCount, reference.Location.Pointer, reference.Location.Line, reference.Location.Column);
}
