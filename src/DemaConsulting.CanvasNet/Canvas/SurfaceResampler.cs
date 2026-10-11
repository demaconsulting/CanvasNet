using System.Numerics;

namespace DemaConsulting.CanvasNet.Canvas;

/// <summary>
///     Internal image-resampling helpers owned by the <see cref="Surface"/> unit: an area-average
///     (box) prefilter used when an image is shrunk on screen, and a bilinear sampler used to
///     read the prefiltered result.
/// </summary>
/// <remarks>
///     Point sampling a source image while shrinking it drops thin features (a one-pixel line in
///     a 2.6:1 reduction is hit by roughly one destination sample in 2.6). The prefilter averages
///     integer-sized blocks of source pixels (the rounded reduction ratio) once per draw (a single
///     O(W*H) pass and one output allocation no larger than the source), and the caller then
///     samples the reduced surface
///     bilinearly, which covers the remaining fractional part of the reduction. Averaging is done
///     in premultiplied alpha with checked <see cref="long"/> accumulators. When the image is not
///     shrunk (or the shrink is too mild for any block reduction), the source surface is returned
///     unchanged so the upscale path is untouched.
/// </remarks>
internal static partial class SurfaceResampler
{
    /// <summary>
    ///     Computes the integer box-filter factors for a pixel-space to device-space transform.
    /// </summary>
    /// <param name="width">Source image width in pixels.</param>
    /// <param name="height">Source image height in pixels.</param>
    /// <param name="pixelToDevice">Affine map from source pixel coordinates to device coordinates.</param>
    /// <returns>
    ///     The horizontal and vertical block sizes (each at least 1). <c>(1, 1)</c> means no
    ///     filtering is needed, including when the transform is degenerate or non-finite.
    /// </returns>
    internal static (int Fx, int Fy) ComputeFactors(int width, int height, Matrix3x2 pixelToDevice)
    {
        var sx = Math.Sqrt(((double)pixelToDevice.M11 * pixelToDevice.M11) + ((double)pixelToDevice.M12 * pixelToDevice.M12));
        var sy = Math.Sqrt(((double)pixelToDevice.M21 * pixelToDevice.M21) + ((double)pixelToDevice.M22 * pixelToDevice.M22));
        return (FactorFor(sx, width), FactorFor(sy, height));
    }

    /// <summary>
    ///     Returns a surface suitable for bilinear sampling at the given scale: a box-filtered,
    ///     reduced copy when the image is being shrunk, otherwise <paramref name="source"/> itself.
    /// </summary>
    /// <param name="source">The decoded source image (not modified).</param>
    /// <param name="pixelToDevice">Affine map from source pixel coordinates to device coordinates.</param>
    /// <param name="useBilinear">
    ///     <see langword="true"/> when the image is shrunk on at least one axis, so the caller
    ///     must sample the result with <see cref="SampleBilinear"/>; <see langword="false"/> for
    ///     enlargement or 1:1 (nearest-neighbor sampling of the unchanged source).
    /// </param>
    /// <returns>
    ///     <paramref name="source"/> when no block reduction is needed - enlargement, 1:1, a
    ///     degenerate or non-finite mapping, or a shrink too mild to round to a block factor above 1
    ///     (in which case <paramref name="useBilinear"/> is still <see langword="true"/>); the
    ///     caller must not dispose it as a temporary. Otherwise a new surface the caller owns and
    ///     must dispose.
    /// </returns>
    internal static Surface Prefilter(Surface source, Matrix3x2 pixelToDevice, out bool useBilinear)
    {
        var sx = Math.Sqrt(((double)pixelToDevice.M11 * pixelToDevice.M11) + ((double)pixelToDevice.M12 * pixelToDevice.M12));
        var sy = Math.Sqrt(((double)pixelToDevice.M21 * pixelToDevice.M21) + ((double)pixelToDevice.M22 * pixelToDevice.M22));
        useBilinear = IsShrink(sx) || IsShrink(sy);
        if (!useBilinear)
        {
            return source;
        }

        var (fx, fy) = ComputeFactors(source.Width, source.Height, pixelToDevice);
        return fx == 1 && fy == 1 ? source : BoxReduce(source, fx, fy);
    }

    /// <summary>
    ///     Reduces <paramref name="source"/> by averaging <paramref name="fx"/> by
    ///     <paramref name="fy"/> blocks in premultiplied alpha; edge blocks average only the
    ///     pixels they cover.
    /// </summary>
    internal static Surface BoxReduce(Surface source, int fx, int fy)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(fx, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(fy, 1);

        // Each output pixel covers an fx-by-fy block; blocks that overhang the image edge average
        // only the pixels they cover (the Area filter drops out-of-image coverage and renormalizes)
        var outWidth = checked((source.Width + fx - 1) / fx);
        var outHeight = checked((source.Height + fy - 1) / fy);
        return Resample(source, 0, (double)outWidth * fx, 0, (double)outHeight * fy, outWidth, outHeight, ResamplingMode.Area);
    }

    /// <summary>
    ///     Samples <paramref name="image"/> bilinearly (pixel-center aligned, edge clamped) at the
    ///     normalized image-space position (<paramref name="u"/>, <paramref name="v"/>), where
    ///     <c>(0, 0)</c> is the top-left corner and <c>(1, 1)</c> the bottom-right corner.
    /// </summary>
    internal static Rgba32 SampleBilinear(Surface image, float u, float v)
    {
        var fx = (u * image.Width) - 0.5f;
        var fy = (v * image.Height) - 0.5f;
        var x0f = MathF.Floor(fx);
        var y0f = MathF.Floor(fy);
        var tx = fx - x0f;
        var ty = fy - y0f;
        var x0 = Math.Clamp((int)x0f, 0, image.Width - 1);
        var x1 = Math.Clamp((int)x0f + 1, 0, image.Width - 1);
        var y0 = Math.Clamp((int)y0f, 0, image.Height - 1);
        var y1 = Math.Clamp((int)y0f + 1, 0, image.Height - 1);

        return Blend(image[x0, y0], image[x1, y0], image[x0, y1], image[x1, y1], tx, ty);
    }

    /// <summary>
    ///     Blends four pixels bilinearly in premultiplied alpha with fractional offsets
    ///     (<paramref name="tx"/>, <paramref name="ty"/>) from <paramref name="p00"/>.
    /// </summary>
    private static Rgba32 Blend(Rgba32 p00, Rgba32 p10, Rgba32 p01, Rgba32 p11, float tx, float ty)
    {
        var w00 = (1 - tx) * (1 - ty);
        var w10 = tx * (1 - ty);
        var w01 = (1 - tx) * ty;
        var w11 = tx * ty;

        var a00 = w00 * p00.A;
        var a10 = w10 * p10.A;
        var a01 = w01 * p01.A;
        var a11 = w11 * p11.A;
        var alpha = a00 + a10 + a01 + a11;
        if (alpha <= 0f)
        {
            return new Rgba32(0, 0, 0, 0);
        }

        return new Rgba32(
            ToByte(((a00 * p00.R) + (a10 * p10.R) + (a01 * p01.R) + (a11 * p11.R)) / alpha),
            ToByte(((a00 * p00.G) + (a10 * p10.G) + (a01 * p01.G) + (a11 * p11.G)) / alpha),
            ToByte(((a00 * p00.B) + (a10 * p10.B) + (a01 * p01.B) + (a11 * p11.B)) / alpha),
            ToByte(alpha));
    }

    /// <summary>True when the local scale is finite and shrinks the image (device pixels per source pixel below one).</summary>
    private static bool IsShrink(double scale) => double.IsFinite(scale) && scale > 0 && scale < 1;

    /// <summary>
    ///     Converts a local scale into an integer block size, the nearest integer to the
    ///     reduction ratio (1 when not shrinking or invalid). Rounding to nearest keeps the
    ///     reduced image at or above roughly one device pixel per sample, so the bilinear pass
    ///     that follows never aliases.
    /// </summary>
    private static int FactorFor(double scale, int size)
    {
        if (!IsShrink(scale))
        {
            return 1;
        }

        var factor = Math.Round(1.0 / scale, MidpointRounding.AwayFromZero);
        return factor >= size ? Math.Max(1, size) : Math.Max(1, (int)factor);
    }

    /// <summary>Rounds a float channel to a byte (half away from zero, clamped).</summary>
    private static byte ToByte(float value) =>
        (byte)Math.Clamp((int)MathF.Round(value, MidpointRounding.AwayFromZero), 0, 255);
}
