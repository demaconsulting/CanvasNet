// cspell:ignore premultiplied
namespace DemaConsulting.CanvasNet.Canvas;

/// <summary>
///     The whole-surface transformations of <see cref="Surface"/> that complement <see cref="Crop"/> and
///     <see cref="Resize"/>: flip, 180-degree rotation, pad, trim, fit into a box, flatten over a color, and the
///     transparency test.
/// </summary>
/// <remarks>
///     <para>
///         Every transformation is a <b>pure function</b>: it never modifies or disposes the surface it is called on and
///         returns a new surface that the caller owns and disposes. <c>(x, y)</c> has its origin at the top left with y
///         down; surfaces hold straight (non-premultiplied) alpha.
///     </para>
///     <para>
///         The flips, the rotation, pad, and trim are pixel moves or per-row copies with no resampling, so the pixels of
///         their results are exact copies of pixels of the source; <see cref="FitInto"/> resamples and
///         <see cref="FlattenOver"/> blends (see each method). A surface must not be changed by another thread while a
///         transformation reads it.
///     </para>
/// </remarks>
public sealed partial class Surface
{
    /// <summary>The largest tolerance that <see cref="Trim"/> and <see cref="FindTrimBounds"/> accept.</summary>
    public const int MaxTrimTolerance = 255;

    /// <summary>
    ///     Creates a mirror image of this surface about its vertical axis (left and right are swapped).
    /// </summary>
    /// <returns>A new surface of the same size in which the pixel at <c>(x, y)</c> is this surface's pixel at <c>(Width - 1 - x, y)</c>.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when this surface has been disposed.</exception>
    public Surface FlipHorizontal()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var result = new Surface(Width, Height);
        for (var y = 0; y < Height; y++)
        {
            var row = result.GetRowSpan(y);
            GetRowSpan(y).CopyTo(row);
            row.Reverse();
        }

        return result;
    }

    /// <summary>
    ///     Creates a mirror image of this surface about its horizontal axis (top and bottom are swapped).
    /// </summary>
    /// <returns>A new surface of the same size in which the pixel at <c>(x, y)</c> is this surface's pixel at <c>(x, Height - 1 - y)</c>.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when this surface has been disposed.</exception>
    public Surface FlipVertical()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var result = new Surface(Width, Height);
        for (var y = 0; y < Height; y++)
        {
            GetRowSpan(Height - 1 - y).CopyTo(result.GetRowSpan(y));
        }

        return result;
    }

    /// <summary>
    ///     Creates a copy of this surface turned by 180 degrees, which is a mirror about both axes.
    /// </summary>
    /// <returns>A new surface of the same size in which the pixel at <c>(x, y)</c> is this surface's pixel at <c>(Width - 1 - x, Height - 1 - y)</c>.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when this surface has been disposed.</exception>
    public Surface Rotate180()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var result = new Surface(Width, Height);
        for (var y = 0; y < Height; y++)
        {
            var row = result.GetRowSpan(y);
            GetRowSpan(Height - 1 - y).CopyTo(row);
            row.Reverse();
        }

        return result;
    }

    /// <summary>
    ///     Surrounds this surface with a border.
    /// </summary>
    /// <param name="left">The width of the border at the left edge, 0 or more.</param>
    /// <param name="top">The height of the border at the top edge, 0 or more.</param>
    /// <param name="right">The width of the border at the right edge, 0 or more.</param>
    /// <param name="bottom">The height of the border at the bottom edge, 0 or more.</param>
    /// <param name="color">The color of the border; the pixels of this surface are copied opaquely, not blended with it.</param>
    /// <returns>
    ///     A new surface of <c>Width + left + right</c> by <c>Height + top + bottom</c> pixels: the border color
    ///     everywhere, with this surface copied in at <c>(left, top)</c>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a border width is negative.</exception>
    /// <exception cref="ArgumentException">Thrown, before anything is allocated, when the result would be larger than <see cref="MaxDimension"/> on a side.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this surface has been disposed.</exception>
    public Surface Pad(int left, int top, int right, int bottom, Rgba32 color)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(left);
        ArgumentOutOfRangeException.ThrowIfNegative(top);
        ArgumentOutOfRangeException.ThrowIfNegative(right);
        ArgumentOutOfRangeException.ThrowIfNegative(bottom);

        // Refuse an oversize result before allocating anything (long arithmetic cannot overflow here)
        var width = (long)Width + left + right;
        var height = (long)Height + top + bottom;
        if (width > MaxDimension || height > MaxDimension)
        {
            throw new ArgumentException(
                $"The padded surface would be {width}x{height} pixels, more than the maximum of {MaxDimension} pixels on a side.");
        }

        // A new surface is already transparent, so only another color needs a fill
        var result = new Surface((int)width, (int)height);
        if (color != default)
        {
            result.Clear(color);
        }

        result.CopyFrom(this, left, top);
        return result;
    }

    /// <summary>
    ///     Finds the bounding box of the content inside a uniform border.
    /// </summary>
    /// <param name="tolerance">The largest distance from the background color that is still background, 0 to <see cref="MaxTrimTolerance"/>.</param>
    /// <returns>
    ///     The inclusive bounding box of the foreground pixels, or <see langword="null"/> when there are none (the surface
    ///     is all background). The <b>background color</b> is the most frequent color among the four corner pixels (top left,
    ///     top right, bottom left, bottom right, exact RGBA equality), a tie going to the earliest in that order. The
    ///     <b>distance</b> of a pixel from it is 0 when both have alpha 0 (every transparent pixel is the same), else the
    ///     largest of the absolute differences of red, green, blue, and alpha; a pixel is background when its distance is
    ///     at most the tolerance. The top and bottom are the first and last rows with a foreground pixel and the left and
    ///     right the smallest and largest foreground columns over those rows (row scans with early exit).
    /// </returns>
    /// <remarks>The surface is not modified.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the tolerance is not 0 to 255.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this surface has been disposed.</exception>
    public (int X, int Y, int Width, int Height)? FindTrimBounds(int tolerance)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(tolerance);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(tolerance, MaxTrimTolerance);

        var width = Width;
        var height = Height;
        var background = TrimBackground();

        var top = -1;
        for (var y = 0; y < height && top < 0; y++)
        {
            if (FirstForeground(GetRowSpan(y), background, tolerance) >= 0)
            {
                top = y;
            }
        }

        if (top < 0)
        {
            return null;
        }

        var bottom = top;
        for (var y = height - 1; y > top; y--)
        {
            if (FirstForeground(GetRowSpan(y), background, tolerance) >= 0)
            {
                bottom = y;
                break;
            }
        }

        var left = width;
        var right = -1;
        for (var y = top; y <= bottom; y++)
        {
            var row = GetRowSpan(y);

            // Only a pixel left of the current left edge or right of the current right edge can extend the box.
            var first = FirstForeground(row[..Math.Min(left, width)], background, tolerance);
            if (first >= 0)
            {
                left = first;
            }

            var last = LastForeground(row, right + 1, background, tolerance);
            if (last > right)
            {
                right = last;
            }
        }

        return (left, top, right - left + 1, bottom - top + 1);
    }

    /// <summary>
    ///     Crops this surface to the content inside a uniform border.
    /// </summary>
    /// <param name="tolerance">The largest distance from the background color that is still background, 0 to <see cref="MaxTrimTolerance"/>.</param>
    /// <returns>
    ///     A new surface: the pixels of the box <see cref="FindTrimBounds"/> finds, or a copy of the whole surface when
    ///     there is no foreground (the surface is all background) so that nothing is trimmed.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the tolerance is not 0 to 255.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this surface has been disposed.</exception>
    public Surface Trim(int tolerance)
    {
        var bounds = FindTrimBounds(tolerance);
        return bounds is { } box
            ? Crop(box.X, box.Y, box.Width, box.Height)
            : Clone();
    }

    /// <summary>
    ///     Fits this surface into a box of an exact size.
    /// </summary>
    /// <param name="boxWidth">The width of the result, 1 to <see cref="MaxDimension"/>.</param>
    /// <param name="boxHeight">The height of the result, 1 to <see cref="MaxDimension"/>.</param>
    /// <param name="mode">How the image is fitted.</param>
    /// <param name="fill">The color of the margin of a <see cref="FitMode.Contain"/> fit; the image's own pixels are not blended with it.</param>
    /// <param name="filter">The resampling filter.</param>
    /// <returns>
    ///     A new surface of exactly the box size. With <see cref="FitMode.Contain"/> the image is scaled (it is enlarged or
    ///     shrunk to touch the box on its limiting side, by <see cref="FitInside"/>) and centered, the margin being
    ///     <paramref name="fill"/>; with <see cref="FitMode.Stretch"/> it is <see cref="Resize"/>d to the box. A source
    ///     that already has the box size is copied unchanged.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the box is not 1 to the maximum per side, or the mode or filter is not defined.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this surface has been disposed.</exception>
    public Surface FitInto(int boxWidth, int boxHeight, FitMode mode, Rgba32 fill, ResamplingMode filter = ResamplingMode.Auto)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfLessThan(boxWidth, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(boxWidth, MaxDimension);
        ArgumentOutOfRangeException.ThrowIfLessThan(boxHeight, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(boxHeight, MaxDimension);
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "The fit mode is not defined.");
        }

        if (mode == FitMode.Stretch || (Width == boxWidth && Height == boxHeight))
        {
            return Resize(boxWidth, boxHeight, filter);
        }

        var (width, height) = FitInside(Width, Height, boxWidth, boxHeight);
        var fitted = Resize(width, height, filter);
        if (width == boxWidth && height == boxHeight)
        {
            return fitted;
        }

        Surface? result = null;
        try
        {
            result = new Surface(boxWidth, boxHeight);
            result.Clear(fill);
            result.CopyFrom(fitted, (boxWidth - width) / 2, (boxHeight - height) / 2);
            return result;
        }
        catch
        {
            result?.Dispose();
            throw;
        }
        finally
        {
            fitted.Dispose();
        }
    }

    /// <summary>
    ///     Flattens this surface over a solid background color.
    /// </summary>
    /// <param name="background">The color to composite the surface over.</param>
    /// <returns>A new surface of the same size: the background filled, with this surface composited over it.</returns>
    /// <remarks>Use it to remove transparency, for example before saving to a format that has no alpha channel.</remarks>
    /// <exception cref="ObjectDisposedException">Thrown when this surface has been disposed.</exception>
    public Surface FlattenOver(Rgba32 background)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var result = new Surface(Width, Height);
        try
        {
            result.Clear(background);
            result.CompositeOver(this);
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Determines whether any pixel of this surface is not fully opaque.
    /// </summary>
    /// <returns><see langword="true"/> when at least one pixel has an alpha value below 255.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when this surface has been disposed.</exception>
    public bool HasTransparency()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        for (var y = 0; y < Height; y++)
        {
            foreach (var pixel in GetRowSpan(y))
            {
                if (pixel.A != 255)
                {
                    return true;
                }
            }
        }

        return false;
    }

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
    internal static (int Width, int Height) FitInside(int width, int height, int boxWidth, int boxHeight)
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
    ///     Chooses the background color of a trim from the four corner pixels.
    /// </summary>
    /// <returns>The most frequent corner color; a tie goes to the earliest of top left, top right, bottom left, bottom right.</returns>
    private Rgba32 TrimBackground()
    {
        var right = Width - 1;
        var bottom = Height - 1;
        Span<Rgba32> corners =
        [
            this[0, 0],
            this[right, 0],
            this[0, bottom],
            this[right, bottom]
        ];

        var best = corners[0];
        var bestCount = 0;
        for (var i = 0; i < corners.Length; i++)
        {
            var count = 0;
            for (var j = 0; j < corners.Length; j++)
            {
                if (corners[j] == corners[i])
                {
                    count++;
                }
            }

            if (count > bestCount)
            {
                best = corners[i];
                bestCount = count;
            }
        }

        return best;
    }

    /// <summary>
    ///     Determines whether a pixel is foreground of a trim.
    /// </summary>
    /// <param name="pixel">The pixel.</param>
    /// <param name="background">The background color.</param>
    /// <param name="tolerance">The largest distance from the background that is still background.</param>
    /// <returns><see langword="true"/> when the distance of the pixel from the background is greater than the tolerance.</returns>
    private static bool IsForeground(Rgba32 pixel, Rgba32 background, int tolerance)
    {
        if (pixel.A == 0 && background.A == 0)
        {
            return false;
        }

        var distance = Math.Max(
            Math.Max(Math.Abs(pixel.R - background.R), Math.Abs(pixel.G - background.G)),
            Math.Max(Math.Abs(pixel.B - background.B), Math.Abs(pixel.A - background.A)));
        return distance > tolerance;
    }

    /// <summary>
    ///     Finds the first foreground pixel of a row.
    /// </summary>
    /// <param name="row">The pixels of the row, or a beginning of it.</param>
    /// <param name="background">The background color.</param>
    /// <param name="tolerance">The tolerance.</param>
    /// <returns>The index of the first foreground pixel, or -1.</returns>
    private static int FirstForeground(ReadOnlySpan<Rgba32> row, Rgba32 background, int tolerance)
    {
        for (var x = 0; x < row.Length; x++)
        {
            if (IsForeground(row[x], background, tolerance))
            {
                return x;
            }
        }

        return -1;
    }

    /// <summary>
    ///     Finds the last foreground pixel of a row at or after a column.
    /// </summary>
    /// <param name="row">The pixels of the row.</param>
    /// <param name="start">The first column to consider.</param>
    /// <param name="background">The background color.</param>
    /// <param name="tolerance">The tolerance.</param>
    /// <returns>The index of the last foreground pixel at or after <paramref name="start"/>, or -1.</returns>
    private static int LastForeground(ReadOnlySpan<Rgba32> row, int start, Rgba32 background, int tolerance)
    {
        for (var x = row.Length - 1; x >= start; x--)
        {
            if (IsForeground(row[x], background, tolerance))
            {
                return x;
            }
        }

        return -1;
    }
}
