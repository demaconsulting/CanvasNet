using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Tool.Imaging;

/// <summary>
///     How a raster image is fitted into a box of a different aspect ratio.
/// </summary>
internal enum FitMode
{
    /// <summary>The image keeps its aspect ratio, is scaled to the largest size that fits, and is centered on a fill color.</summary>
    Contain,

    /// <summary>The image is resampled to exactly the box, changing its aspect ratio if needed.</summary>
    Stretch
}

/// <summary>
///     Small pixel operations that more than one part of the Imaging subsystem needs.
/// </summary>
internal static class SurfaceOperations
{
    /// <summary>
    ///     Finds the largest size with the aspect ratio of an image that fits inside a box.
    /// </summary>
    /// <param name="width">The width of the image; must be at least 1.</param>
    /// <param name="height">The height of the image; must be at least 1.</param>
    /// <param name="boxWidth">The width of the box; must be at least 1.</param>
    /// <param name="boxHeight">The height of the box; must be at least 1.</param>
    /// <returns>
    ///     The width and height of the fitted image: the image scaled by the smaller of the two box ratios (so a small
    ///     image is enlarged), each side rounded to the nearest pixel (half away from zero) and then limited to
    ///     1 and the box.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an argument is less than 1.</exception>
    public static (int Width, int Height) FitInside(int width, int height, int boxWidth, int boxHeight)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(boxWidth, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(boxHeight, 1);

        var factor = Math.Min((double)boxWidth / width, (double)boxHeight / height);
        var fittedWidth = (int)Math.Clamp(Math.Round(width * factor, MidpointRounding.AwayFromZero), 1, boxWidth);
        var fittedHeight = (int)Math.Clamp(Math.Round(height * factor, MidpointRounding.AwayFromZero), 1, boxHeight);
        return (fittedWidth, fittedHeight);
    }

    /// <summary>
    ///     Fits a raster image into a box of an exact size.
    /// </summary>
    /// <param name="source">The surface; it is not modified or disposed.</param>
    /// <param name="boxWidth">The width of the result, 1 to <see cref="Surface.MaxDimension"/>.</param>
    /// <param name="boxHeight">The height of the result, 1 to <see cref="Surface.MaxDimension"/>.</param>
    /// <param name="mode">How the image is fitted.</param>
    /// <param name="fill">The color of the margin of a <see cref="FitMode.Contain"/> fit; the image's own pixels are not blended with it.</param>
    /// <param name="filter">The resampling filter.</param>
    /// <returns>
    ///     A new surface of exactly the box size. With <see cref="FitMode.Contain"/> the image is scaled by
    ///     <see cref="Surface.ResizeToFit"/> (it is enlarged or shrunk to touch the box on its limiting side) and
    ///     centered, the margin being <paramref name="fill"/>; with <see cref="FitMode.Stretch"/> it is
    ///     <see cref="Surface.Resize"/>d to the box. A source that already has the box size is copied unchanged.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the box is not 1 to the maximum per side, or the mode or filter is not defined.</exception>
    public static Surface FitInto(Surface source, int boxWidth, int boxHeight, FitMode mode, Rgba32 fill, ResamplingMode filter)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "The fit mode is not defined.");
        }

        if (mode == FitMode.Stretch || (source.Width == boxWidth && source.Height == boxHeight))
        {
            return source.Resize(boxWidth, boxHeight, filter);
        }

        var (width, height) = FitInside(source.Width, source.Height, boxWidth, boxHeight);
        var fitted = source.Resize(width, height, filter);
        if (width == boxWidth && height == boxHeight)
        {
            return fitted;
        }

        var result = new Surface(boxWidth, boxHeight);
        try
        {
            result.Clear(fill);
            var left = (boxWidth - width) / 2;
            var top = (boxHeight - height) / 2;
            result.CopyFrom(fitted, left, top);

            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
        finally
        {
            fitted.Dispose();
        }
    }

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
