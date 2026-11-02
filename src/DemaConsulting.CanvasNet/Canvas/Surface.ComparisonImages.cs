// cspell:ignore psnr rmse ssim
namespace DemaConsulting.CanvasNet.Canvas;

/// <summary>
///     The picture creators of the comparison of <see cref="Surface"/>: difference, overlay, and side-by-side images.
/// </summary>
/// <remarks>
///     All pictures are opaque (the alpha of the inputs is ignored) and are computed with integer arithmetic (after the
///     amplification and the overlay weight are rounded once), so the same inputs give the same pixels on every run.
///     Neither input is modified or disposed.
/// </remarks>
public sealed partial class Surface
{
    /// <summary>The width in pixels of the gaps between the panels of a side-by-side image.</summary>
    public const int SideBySideGap = 8;

    /// <summary>The color of the gaps of a side-by-side image.</summary>
    private static readonly Rgba32 GapColor = new(128, 128, 128, 255);

    /// <summary>
    ///     Creates a difference image of this surface and another of the same size.
    /// </summary>
    /// <param name="other">The surface to compare with; it is not modified or disposed.</param>
    /// <param name="style">How a difference is drawn.</param>
    /// <param name="gain">The amplification of a difference, 0.1 to 255.</param>
    /// <returns>A new opaque surface of the size of the images.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the surfaces have different sizes.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the gain is not 0.1 to 255 or the style is not defined.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when either surface has been disposed.</exception>
    /// <remarks>
    ///     <para>
    ///         With <see cref="DiffStyle.Heat"/> the pixel's difference is <c>delta = max(|dR|, |dG|, |dB|)</c> and
    ///         <c>v = min(255, round(delta * gain))</c> (half away from zero); the color is
    ///         <c>R = min(255, 3v)</c>, <c>G = min(255, max(0, 3v - 255))</c>, <c>B = min(255, max(0, 3v - 510))</c>, a
    ///         ramp from black through red and yellow to white; identical pixels are pure black.
    ///     </para>
    ///     <para>
    ///         With <see cref="DiffStyle.Abs"/> each channel is <c>min(255, round(|d| * gain))</c> of its own difference.
    ///     </para>
    /// </remarks>
    public Surface CreateDiff(Surface other, DiffStyle style, double gain)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(other);
        ObjectDisposedException.ThrowIf(other._disposed, other);
        RequireSameSize(other);
        if (!Enum.IsDefined(style))
        {
            throw new ArgumentOutOfRangeException(nameof(style), style, "The diff style is not defined.");
        }

        if (!(gain >= 0.1 && gain <= 255))
        {
            throw new ArgumentOutOfRangeException(nameof(gain), gain, "The gain must be 0.1 to 255.");
        }

        var result = new Surface(Width, Height);
        try
        {
            // A table makes the amplification one lookup per channel difference
            var amplified = new byte[256];
            for (var d = 0; d < amplified.Length; d++)
            {
                amplified[d] = (byte)Math.Min(255, (int)Math.Round(d * gain, MidpointRounding.AwayFromZero));
            }

            for (var y = 0; y < Height; y++)
            {
                var rowA = GetRowSpan(y);
                var rowB = other.GetRowSpan(y);
                var target = result.GetRowSpan(y);
                for (var x = 0; x < Width; x++)
                {
                    var pa = rowA[x];
                    var pb = rowB[x];
                    var dr = Math.Abs(pa.R - pb.R);
                    var dg = Math.Abs(pa.G - pb.G);
                    var db = Math.Abs(pa.B - pb.B);
                    if (style == DiffStyle.Abs)
                    {
                        target[x] = new Rgba32(amplified[dr], amplified[dg], amplified[db], 255);
                        continue;
                    }

                    var v = amplified[Math.Max(dr, Math.Max(dg, db))] * 3;
                    target[x] = new Rgba32(
                        (byte)Math.Min(255, v),
                        (byte)Math.Min(255, Math.Max(0, v - 255)),
                        (byte)Math.Min(255, Math.Max(0, v - 510)),
                        255);
                }
            }

            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Creates an onion-skin overlay: a blend of this surface and another of the same size.
    /// </summary>
    /// <param name="other">The second surface; it is not modified or disposed.</param>
    /// <param name="weightOfOther">The weight of the second surface, 0 to 1.</param>
    /// <returns>A new opaque surface of the size of the images.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the surfaces have different sizes.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the weight is not 0 to 1.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when either surface has been disposed.</exception>
    /// <remarks>
    ///     With <c>bw = round(weight * 255)</c> (half away from zero) each channel is
    ///     <c>(a * (255 - bw) + b * bw + 127) / 255</c> in integer arithmetic, so weight 0 is the colors of this surface and
    ///     weight 1 the colors of the other exactly (both with alpha 255).
    /// </remarks>
    public Surface CreateOverlay(Surface other, double weightOfOther)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(other);
        ObjectDisposedException.ThrowIf(other._disposed, other);
        RequireSameSize(other);
        if (!(weightOfOther >= 0 && weightOfOther <= 1))
        {
            throw new ArgumentOutOfRangeException(nameof(weightOfOther), weightOfOther, "The weight must be 0 to 1.");
        }

        var weight = (int)Math.Round(weightOfOther * 255, MidpointRounding.AwayFromZero);
        var result = new Surface(Width, Height);
        try
        {
            for (var y = 0; y < Height; y++)
            {
                var rowA = GetRowSpan(y);
                var rowB = other.GetRowSpan(y);
                var target = result.GetRowSpan(y);
                for (var x = 0; x < Width; x++)
                {
                    var pa = rowA[x];
                    var pb = rowB[x];
                    target[x] = new Rgba32(
                        OverlayBlend(pa.R, pb.R, weight),
                        OverlayBlend(pa.G, pb.G, weight),
                        OverlayBlend(pa.B, pb.B, weight),
                        255);
                }
            }

            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Creates a side-by-side image: this surface, a second surface, and a difference image, left to right.
    /// </summary>
    /// <param name="other">The second surface, of the same size; it is not modified or disposed.</param>
    /// <param name="difference">The difference image (see <see cref="CreateDiff"/>), of the same size; it is not modified or disposed.</param>
    /// <returns>
    ///     A new surface: the three panels separated by gaps of <see cref="SideBySideGap"/> pixels of gray
    ///     (<c>#808080</c>), without labels.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> or <paramref name="difference"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the surfaces have different sizes.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when any of the surfaces has been disposed.</exception>
    /// <remarks>
    ///     The sheet is <c>3 W + 2 gap</c> wide and <c>H</c> high; the panels are copied opaquely. When that is wider or
    ///     higher than <see cref="MaxDimension"/>, the three panels are first scaled down uniformly with the area filter to
    ///     <c>floor(W s)</c> by <c>floor(H s)</c> pixels, where <c>s</c> is the largest factor that makes the sheet fit.
    /// </remarks>
    public Surface CreateSideBySide(Surface other, Surface difference)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(other);
        ArgumentNullException.ThrowIfNull(difference);
        ObjectDisposedException.ThrowIf(other._disposed, other);
        ObjectDisposedException.ThrowIf(difference._disposed, difference);
        RequireSameSize(other);
        if (Width != difference.Width || Height != difference.Height)
        {
            throw new ArgumentException(
                $"The images have different sizes: {Width}x{Height} and {difference.Width}x{difference.Height}.",
                nameof(difference));
        }

        var width = Width;
        var height = Height;
        var gaps = 2 * SideBySideGap;
        if ((3L * width) + gaps > MaxDimension || height > MaxDimension)
        {
            var scale = Math.Min(
                (double)(MaxDimension - gaps) / (3.0 * width),
                (double)MaxDimension / height);
            width = Math.Max(1, (int)Math.Floor(width * scale));
            height = Math.Max(1, (int)Math.Floor(height * scale));
        }

        var sheet = new Surface((3 * width) + gaps, height);
        try
        {
            sheet.Clear(GapColor);
            Surface[] panels = [this, other, difference];
            for (var i = 0; i < panels.Length; i++)
            {
                var left = i * (width + SideBySideGap);
                if (panels[i].Width == width && panels[i].Height == height)
                {
                    sheet.CopyFrom(panels[i], left, 0);
                    continue;
                }

                using var scaled = panels[i].Resize(width, height, ResamplingMode.Area);
                sheet.CopyFrom(scaled, left, 0);
            }

            return sheet;
        }
        catch
        {
            sheet.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Blends one channel of two pixels.
    /// </summary>
    /// <param name="first">The channel of the first image.</param>
    /// <param name="second">The channel of the second image.</param>
    /// <param name="weight">The weight of the second image, 0 to 255.</param>
    /// <returns><c>(first * (255 - weight) + second * weight + 127) / 255</c>.</returns>
    private static byte OverlayBlend(byte first, byte second, int weight)
    {
        return (byte)(((first * (255 - weight)) + (second * weight) + 127) / 255);
    }
}
