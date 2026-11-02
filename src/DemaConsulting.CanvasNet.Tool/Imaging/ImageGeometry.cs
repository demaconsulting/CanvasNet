// cspell:ignore bilinear premultiplied deskew
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Tool.Imaging;

/// <summary>
///     The geometry of a surface that the pipeline needs beyond right-angle moves: rotation by any angle with canvas
///     expansion and a fill color.
/// </summary>
/// <remarks>
///     <para>
///         Angles are in degrees and positive means <b>clockwise</b> on screen; <c>(x, y)</c> has its origin at the top
///         left with y down; surfaces hold straight (non-premultiplied) alpha. Rotation never modifies its input; it
///         returns a new surface the caller owns.
///     </para>
///     <para>
///         Rows are processed in parallel with disjoint output and every pixel is computed from its own coordinates (no
///         incremental accumulation), so the result does not depend on the number of cores. Nothing allocates per pixel.
///     </para>
/// </remarks>
internal static partial class ImageGeometry
{
    /// <summary>The tolerance in pixels that absorbs floating-point noise when the size of a rotated image is calculated.</summary>
    private const double SizeEpsilon = 1e-9;

    /// <summary>
    ///     Calculates the size of the canvas that holds an image rotated by an angle.
    /// </summary>
    /// <param name="width">The width of the image; must be at least 1.</param>
    /// <param name="height">The height of the image; must be at least 1.</param>
    /// <param name="degrees">The rotation in degrees, any finite number (clockwise).</param>
    /// <returns>
    ///     The width and height of the bounding box of the rotated rectangle: with <c>c = |cos θ|</c> and
    ///     <c>s = |sin θ|</c> they are <c>max(1, ceil(W c + H s - 1e-9))</c> and <c>max(1, ceil(W s + H c - 1e-9))</c>; the
    ///     small tolerance absorbs floating-point noise so that an exact fit (a right angle) does not gain a pixel. For
    ///     example 100 by 50 gives 50 by 100 at 90 degrees, 107 by 107 at 45, and 112 by 94 at 30. The size is not limited
    ///     here; see <see cref="CheckRotatedSize"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the width or height is less than 1 or the angle is not finite.</exception>
    public static (int Width, int Height) RotatedSize(int width, int height, double degrees)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        if (!double.IsFinite(degrees))
        {
            throw new ArgumentOutOfRangeException(nameof(degrees), degrees, "The angle must be a finite number.");
        }

        var theta = degrees * Math.PI / 180.0;
        var c = Math.Abs(Math.Cos(theta));
        var s = Math.Abs(Math.Sin(theta));
        var newWidth = Math.Max(1.0, Math.Ceiling((width * c) + (height * s) - SizeEpsilon));
        var newHeight = Math.Max(1.0, Math.Ceiling((width * s) + (height * c) - SizeEpsilon));
        return ((int)newWidth, (int)newHeight);
    }

    /// <summary>
    ///     Calculates the size of a rotated image and refuses one larger than the maximum image size.
    /// </summary>
    /// <param name="width">The width of the image; must be at least 1.</param>
    /// <param name="height">The height of the image; must be at least 1.</param>
    /// <param name="degrees">The rotation in degrees, any finite number (clockwise).</param>
    /// <returns>The size from <see cref="RotatedSize"/>.</returns>
    /// <exception cref="ToolException">Thrown with code <c>size-limit</c> when the rotated image would be larger than <see cref="Surface.MaxDimension"/> on a side.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the width or height is less than 1 or the angle is not finite.</exception>
    public static (int Width, int Height) CheckRotatedSize(int width, int height, double degrees)
    {
        var (newWidth, newHeight) = RotatedSize(width, height, degrees);
        if (newWidth > Surface.MaxDimension || newHeight > Surface.MaxDimension)
        {
            throw new ToolException(
                ErrorCodes.SizeLimit,
                $"The rotated image would be {newWidth}x{newHeight} pixels, more than the maximum of {Surface.MaxDimension} pixels on a side.",
                "Shrink the image first, for example --fit 4096x4096 before --rotate or --deskew.");
        }

        return (newWidth, newHeight);
    }

    /// <summary>
    ///     Rotates a surface by any angle onto an expanded canvas.
    /// </summary>
    /// <param name="source">The surface; it is not modified or disposed.</param>
    /// <param name="degrees">The rotation in degrees, any finite number; positive is clockwise.</param>
    /// <param name="fill">The color of everything outside the rotated image.</param>
    /// <param name="nearest"><see langword="true"/> to copy the nearest source pixel; <see langword="false"/> to interpolate bilinearly.</param>
    /// <param name="useFastPaths"><see langword="false"/> to compute every pixel with the generic bounds-checked sampler (used by tests to prove the fast paths give the same pixels).</param>
    /// <returns>
    ///     A new surface of the size given by <see cref="CheckRotatedSize"/>. For the destination pixel <c>(xd, yd)</c> with
    ///     <c>dx = xd + 0.5 - newW / 2</c> and <c>dy = yd + 0.5 - newH / 2</c> the continuous source coordinate is
    ///     <c>u = dx cos θ + dy sin θ + W / 2</c>, <c>v = -dx sin θ + dy cos θ + H / 2</c> (pixel <c>i</c> covers
    ///     <c>[i, i + 1)</c>), computed from <c>xd</c> and <c>yd</c> alone. <b>Nearest</b> takes the source pixel
    ///     <c>(floor u, floor v)</c> or the fill outside the image. <b>Bilinear</b> blends the four source pixels around
    ///     <c>(u - 0.5, v - 0.5)</c> with the usual weights, a tap outside the image being the fill color, in premultiplied
    ///     form: with the tap alphas <c>a</c> and weights <c>w</c>, <c>sumA = Σ w a</c>, the alpha is <c>Round(sumA)</c> and
    ///     each color channel <c>Round(Σ w c a / sumA)</c>. A pixel whose alpha is 0 is stored as (0, 0, 0, 0), so a fully
    ///     transparent fill is always stored that way. Rotating by an angle and back is not lossless.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle is not finite.</exception>
    /// <exception cref="ToolException">Thrown with code <c>size-limit</c>, before anything is allocated, when the result would be larger than the maximum image size.</exception>
    public static Surface RotateArbitrary(Surface source, double degrees, Rgba32 fill, bool nearest, bool useFastPaths = true)
    {
        ArgumentNullException.ThrowIfNull(source);

        var (newWidth, newHeight) = CheckRotatedSize(source.Width, source.Height, degrees);
        var theta = degrees * Math.PI / 180.0;
        var cos = Math.Cos(theta);
        var sin = Math.Sin(theta);
        var halfNewWidth = newWidth / 2.0;
        var halfNewHeight = newHeight / 2.0;
        var halfWidth = source.Width / 2.0;
        var halfHeight = source.Height / 2.0;
        var stored = fill.A == 0 ? default : fill;

        var result = new Surface(newWidth, newHeight);
        try
        {
            Parallel.For(0, newHeight, yd =>
            {
                var row = result.GetRowSpan(yd);
                var dy = yd + 0.5 - halfNewHeight;
                for (var xd = 0; xd < newWidth; xd++)
                {
                    var dx = xd + 0.5 - halfNewWidth;
                    var u = (dx * cos) + (dy * sin) + halfWidth;
                    var v = (-dx * sin) + (dy * cos) + halfHeight;
                    row[xd] = nearest
                        ? SampleNearest(source, u, v, stored)
                        : SampleBilinear(source, u, v, fill, stored, useFastPaths);
                }
            });

            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Samples a surface at a continuous coordinate with the nearest pixel.
    /// </summary>
    /// <param name="source">The surface.</param>
    /// <param name="u">The continuous x coordinate; pixel <c>i</c> covers <c>[i, i + 1)</c>.</param>
    /// <param name="v">The continuous y coordinate.</param>
    /// <param name="outside">The pixel to return outside the surface.</param>
    /// <returns>The pixel containing the coordinate, or <paramref name="outside"/>.</returns>
    private static Rgba32 SampleNearest(Surface source, double u, double v, Rgba32 outside)
    {
        var fx = Math.Floor(u);
        var fy = Math.Floor(v);
        if (fx < 0 || fy < 0 || fx >= source.Width || fy >= source.Height)
        {
            return outside;
        }

        return source.GetRowSpan((int)fy)[(int)fx];
    }

    /// <summary>
    ///     Samples a surface at a continuous coordinate with bilinear interpolation.
    /// </summary>
    /// <param name="source">The surface.</param>
    /// <param name="u">The continuous x coordinate; pixel <c>i</c> covers <c>[i, i + 1)</c>.</param>
    /// <param name="v">The continuous y coordinate.</param>
    /// <param name="fill">The color of a tap outside the surface.</param>
    /// <param name="stored">The pixel returned when every tap is outside: the fill, or (0, 0, 0, 0) for a transparent fill.</param>
    /// <param name="useFastPaths"><see langword="true"/> to skip the bounds tests when all four taps are inside and to return <paramref name="stored"/> without blending when all are outside.</param>
    /// <returns>The blended pixel.</returns>
    private static Rgba32 SampleBilinear(Surface source, double u, double v, Rgba32 fill, Rgba32 stored, bool useFastPaths)
    {
        var p = u - 0.5;
        var q = v - 0.5;
        var floorX = Math.Floor(p);
        var floorY = Math.Floor(q);
        var fx = p - floorX;
        var fy = q - floorY;

        // Far outside the image the integer conversion below could overflow, and every tap is the fill anyway.
        var width = source.Width;
        var height = source.Height;
        if (floorX < -1 || floorY < -1 || floorX >= width || floorY >= height)
        {
            return useFastPaths ? stored : Blend(fill, fill, fill, fill, fx, fy);
        }

        var x0 = (int)floorX;
        var y0 = (int)floorY;
        if (useFastPaths && x0 >= 0 && y0 >= 0 && x0 + 1 < width && y0 + 1 < height)
        {
            var upper = source.GetRowSpan(y0);
            var lower = source.GetRowSpan(y0 + 1);
            return Blend(upper[x0], upper[x0 + 1], lower[x0], lower[x0 + 1], fx, fy);
        }

        return Blend(
            Tap(source, x0, y0, fill),
            Tap(source, x0 + 1, y0, fill),
            Tap(source, x0, y0 + 1, fill),
            Tap(source, x0 + 1, y0 + 1, fill),
            fx,
            fy);
    }

    /// <summary>
    ///     Reads a pixel of a surface, or the fill color outside it.
    /// </summary>
    /// <param name="source">The surface.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <param name="fill">The color outside the surface.</param>
    /// <returns>The pixel, or <paramref name="fill"/>.</returns>
    private static Rgba32 Tap(Surface source, int x, int y, Rgba32 fill)
    {
        return x < 0 || y < 0 || x >= source.Width || y >= source.Height ? fill : source.GetRowSpan(y)[x];
    }

    /// <summary>
    ///     Blends four taps in premultiplied form.
    /// </summary>
    /// <param name="t00">The top-left tap.</param>
    /// <param name="t10">The top-right tap.</param>
    /// <param name="t01">The bottom-left tap.</param>
    /// <param name="t11">The bottom-right tap.</param>
    /// <param name="fx">The horizontal fraction, 0 to just below 1.</param>
    /// <param name="fy">The vertical fraction, 0 to just below 1.</param>
    /// <returns>
    ///     The pixel with alpha <c>Round(Σ w a)</c> and each color <c>Round(Σ w c a / Σ w a)</c>; (0, 0, 0, 0) when the alpha
    ///     is 0.
    /// </returns>
    private static Rgba32 Blend(Rgba32 t00, Rgba32 t10, Rgba32 t01, Rgba32 t11, double fx, double fy)
    {
        var k00 = (1 - fx) * (1 - fy) * t00.A;
        var k10 = fx * (1 - fy) * t10.A;
        var k01 = (1 - fx) * fy * t01.A;
        var k11 = fx * fy * t11.A;
        var sumA = k00 + k10 + k01 + k11;
        var alpha = (byte)Math.Clamp(Math.Round(sumA, MidpointRounding.AwayFromZero), 0, 255);
        if (alpha == 0)
        {
            return default;
        }

        var r = ((k00 * t00.R) + (k10 * t10.R) + (k01 * t01.R) + (k11 * t11.R)) / sumA;
        var g = ((k00 * t00.G) + (k10 * t10.G) + (k01 * t01.G) + (k11 * t11.G)) / sumA;
        var b = ((k00 * t00.B) + (k10 * t10.B) + (k01 * t01.B) + (k11 * t11.B)) / sumA;
        return new Rgba32(ToByte(r), ToByte(g), ToByte(b), alpha);
    }

    /// <summary>
    ///     Rounds a channel value half away from zero and limits it to a byte.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The rounded value from 0 to 255.</returns>
    private static byte ToByte(double value)
    {
        return (byte)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 0, 255);
    }
}
