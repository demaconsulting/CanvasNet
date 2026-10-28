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
    ///     Reduces a data reference to the parts that do not depend on the source name, so documents opened from
    ///     different sources can be compared.
    /// </summary>
    /// <param name="reference">The reference.</param>
    /// <returns>A comparable tuple.</returns>
    public static (VegaLiteDataKind Kind, string? Url, long? Rows, string Pointer, int Line, int Column) Anonymous(VegaLiteDataReference reference) =>
        (reference.Kind, reference.Url, reference.RowCount, reference.Location.Pointer, reference.Location.Line, reference.Location.Column);
}
