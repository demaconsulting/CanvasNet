using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Tool.Imaging;

/// <summary>
///     The picture creators of <see cref="ImageComparison"/>: difference, overlay, and side-by-side images.
/// </summary>
/// <remarks>
///     All pictures are opaque and are computed with integer arithmetic (after the amplification and the overlay weight
///     are rounded once), so the same inputs give the same pixels on every run.
/// </remarks>
internal static partial class ImageComparison
{
    /// <summary>The color of the gaps of a side-by-side image.</summary>
    private static readonly Rgba32 GapColor = new(128, 128, 128, 255);

    /// <summary>
    ///     Creates a difference image.
    /// </summary>
    /// <param name="a">The first image; it is not modified or disposed.</param>
    /// <param name="b">The second image; it is not modified or disposed.</param>
    /// <param name="style">How a difference is drawn.</param>
    /// <param name="gain">The amplification of a difference, 0.1 to 255.</param>
    /// <returns>A new opaque surface of the size of the images.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an image is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the images have different sizes.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the gain is not 0.1 to 255 or the style is not defined.</exception>
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
    public static Surface CreateDiff(Surface a, Surface b, DiffStyle style, double gain)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        RequireSameSize(a, b);
        if (!Enum.IsDefined(style))
        {
            throw new ArgumentOutOfRangeException(nameof(style), style, "The diff style is not defined.");
        }

        if (!(gain >= 0.1 && gain <= 255))
        {
            throw new ArgumentOutOfRangeException(nameof(gain), gain, "The gain must be 0.1 to 255.");
        }

        var result = new Surface(a.Width, a.Height);
        try
        {
            // A table makes the amplification one lookup per channel difference
            var amplified = new byte[256];
            for (var d = 0; d < amplified.Length; d++)
            {
                amplified[d] = (byte)Math.Min(255, (int)Math.Round(d * gain, MidpointRounding.AwayFromZero));
            }

            for (var y = 0; y < a.Height; y++)
            {
                var rowA = a.GetRowSpan(y);
                var rowB = b.GetRowSpan(y);
                var target = result.GetRowSpan(y);
                for (var x = 0; x < a.Width; x++)
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
    ///     Creates an onion-skin overlay: a blend of two images.
    /// </summary>
    /// <param name="a">The first image; it is not modified or disposed.</param>
    /// <param name="b">The second image; it is not modified or disposed.</param>
    /// <param name="weightOfB">The weight of the second image, 0 to 1.</param>
    /// <returns>A new opaque surface of the size of the images.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an image is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the images have different sizes.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the weight is not 0 to 1.</exception>
    /// <remarks>
    ///     With <c>bw = round(weight * 255)</c> (half away from zero) each channel is
    ///     <c>(a * (255 - bw) + b * bw + 127) / 255</c> in integer arithmetic, so weight 0 is the first image and
    ///     weight 1 is the second image exactly.
    /// </remarks>
    public static Surface CreateOverlay(Surface a, Surface b, double weightOfB)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        RequireSameSize(a, b);
        if (!(weightOfB >= 0 && weightOfB <= 1))
        {
            throw new ArgumentOutOfRangeException(nameof(weightOfB), weightOfB, "The weight must be 0 to 1.");
        }

        var weight = (int)Math.Round(weightOfB * 255, MidpointRounding.AwayFromZero);
        var result = new Surface(a.Width, a.Height);
        try
        {
            for (var y = 0; y < a.Height; y++)
            {
                var rowA = a.GetRowSpan(y);
                var rowB = b.GetRowSpan(y);
                var target = result.GetRowSpan(y);
                for (var x = 0; x < a.Width; x++)
                {
                    var pa = rowA[x];
                    var pb = rowB[x];
                    target[x] = new Rgba32(
                        Blend(pa.R, pb.R, weight),
                        Blend(pa.G, pb.G, weight),
                        Blend(pa.B, pb.B, weight),
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
    ///     Creates a side-by-side image: the first image, the second image, and the difference, left to right.
    /// </summary>
    /// <param name="a">The first image; it is not modified or disposed.</param>
    /// <param name="b">The second image; it is not modified or disposed.</param>
    /// <param name="diff">The difference image, of the same size; it is not modified or disposed.</param>
    /// <returns>
    ///     A new surface: the three panels separated by gaps of <see cref="SideBySideGap"/> pixels of gray
    ///     (<c>#808080</c>), without labels.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when an image is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the images have different sizes.</exception>
    /// <remarks>
    ///     The sheet is <c>3 W + 2 gap</c> wide and <c>H</c> high. When that is wider or higher than
    ///     <see cref="Surface.MaxDimension"/>, the three panels are first scaled down uniformly with the area filter to
    ///     <c>floor(W s)</c> by <c>floor(H s)</c> pixels, where <c>s</c> is the largest factor that makes the sheet fit.
    /// </remarks>
    public static Surface CreateSideBySide(Surface a, Surface b, Surface diff)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        ArgumentNullException.ThrowIfNull(diff);
        RequireSameSize(a, b);
        RequireSameSize(a, diff);

        var width = a.Width;
        var height = a.Height;
        var gaps = 2 * SideBySideGap;
        if ((3L * width) + gaps > Surface.MaxDimension || height > Surface.MaxDimension)
        {
            var scale = Math.Min(
                (double)(Surface.MaxDimension - gaps) / (3.0 * width),
                (double)Surface.MaxDimension / height);
            width = Math.Max(1, (int)Math.Floor(width * scale));
            height = Math.Max(1, (int)Math.Floor(height * scale));
        }

        var sheet = new Surface((3 * width) + gaps, height);
        try
        {
            sheet.Clear(GapColor);
            Surface[] panels = [a, b, diff];
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
    private static byte Blend(byte first, byte second, int weight)
    {
        return (byte)(((first * (255 - weight)) + (second * weight) + 127) / 255);
    }

    /// <summary>
    ///     Requires that two surfaces have the same size.
    /// </summary>
    /// <param name="first">The first surface.</param>
    /// <param name="second">The second surface.</param>
    /// <exception cref="ArgumentException">Thrown when the sizes differ.</exception>
    private static void RequireSameSize(Surface first, Surface second)
    {
        if (first.Width != second.Width || first.Height != second.Height)
        {
            throw new ArgumentException(
                $"The images have different sizes: {first.Width}x{first.Height} and {second.Width}x{second.Height}.");
        }
    }
}
