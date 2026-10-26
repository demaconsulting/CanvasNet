using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Tool.Imaging;

/// <summary>
///     Small pixel operations that more than one part of the Imaging subsystem needs.
/// </summary>
internal static class SurfaceOperations
{
    /// <summary>
    ///     Flattens a surface over a solid background color.
    /// </summary>
    /// <param name="source">The surface; it is not modified or disposed.</param>
    /// <param name="background">The color to composite the surface over.</param>
    /// <returns>A new surface of the same size: the background filled, with the source composited over it.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    public static Surface FlattenOver(Surface source, Rgba32 background)
    {
        ArgumentNullException.ThrowIfNull(source);

        var result = new Surface(source.Width, source.Height);
        try
        {
            result.Clear(background);
            result.CompositeOver(source);
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Determines whether any pixel of a surface is not fully opaque.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <returns><see langword="true"/> when at least one pixel has an alpha value below 255.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="surface"/> is <see langword="null"/>.</exception>
    public static bool HasTransparency(Surface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        for (var y = 0; y < surface.Height; y++)
        {
            foreach (var pixel in surface.GetRowSpan(y))
            {
                if (pixel.A != 255)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
