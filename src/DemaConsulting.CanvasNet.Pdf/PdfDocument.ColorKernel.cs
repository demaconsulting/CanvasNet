using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Pdf;

public sealed partial class PdfDocument
{
    // Pure, allocation-free color math for the CIE-based color spaces (CalGray, CalRGB, Lab)
    // defined in PDF 32000-1 §8.6.5. Every function here is static, takes only numbers and the
    // already-validated PdfColorSpace payload, and never touches PdfObject. The final step of each
    // converts CIE XYZ (relative to the space's own /WhitePoint) to sRGB by Bradford chromatic
    // adaptation to D65, the linear-sRGB matrix, and the sRGB transfer function. The ICC profile
    // of an /ICCBased space is never applied (see ResolveIccBasedColorSpace).

    /// <summary>
    ///     The largest component count whose per-color scratch span is stack-allocated (the
    ///     DeviceN colorant and function-output cap), so no per-pixel heap array is needed for any
    ///     supported color space; the stack frame cost is a fixed 256 bytes per scratch span and
    ///     the buffers are local, so conversion stays thread-safe.
    /// </summary>
    private const int MaxStackColorComponents = 32;

    /// <summary>The Bradford cone-response matrix (row-major).</summary>
    private static readonly double[] BradfordMatrix =
    [
        0.8951, 0.2664, -0.1614,
        -0.7502, 1.7135, 0.0367,
        0.0389, -0.0685, 1.0296,
    ];

    /// <summary>The inverse of <see cref="BradfordMatrix"/> (row-major).</summary>
    private static readonly double[] BradfordInverseMatrix =
    [
        0.9869929, -0.1470543, 0.1599627,
        0.4323053, 0.5183603, 0.0492912,
        -0.0085287, 0.0400428, 0.9684867,
    ];

    /// <summary>The CIE XYZ (D65 white) to linear sRGB matrix (row-major).</summary>
    private static readonly double[] XyzD65ToLinearSrgbMatrix =
    [
        3.2404542, -1.5371385, -0.4985314,
        -0.9692660, 1.8760108, 0.0415560,
        0.0556434, -0.2040259, 1.0572252,
    ];

    /// <summary>The D65 reference white (<c>X Y Z</c>, <c>Y = 1</c>).</summary>
    private static readonly double[] D65White = [0.95047, 1.0, 1.08883];

    /// <summary>
    ///     Builds the row-major 3x3 matrix mapping XYZ relative to <paramref name="whitePoint"/>
    ///     to linear sRGB: Bradford adaptation from the white point to D65, then XYZ to linear sRGB.
    /// </summary>
    private static double[] BuildXyzToLinearRgbMatrix(double[] whitePoint)
    {
        var source = Multiply3x1(BradfordMatrix, whitePoint[0], whitePoint[1], whitePoint[2]);
        var target = Multiply3x1(BradfordMatrix, D65White[0], D65White[1], D65White[2]);

        // adaptation = BradfordInverse * diag(target / source) * Bradford
        var scaled = new double[9];
        for (var row = 0; row < 3; row++)
        {
            var ratio = target[row] / source[row];
            for (var column = 0; column < 3; column++)
            {
                scaled[(row * 3) + column] = ratio * BradfordMatrix[(row * 3) + column];
            }
        }

        var adaptation = Multiply3x3(BradfordInverseMatrix, scaled);
        return Multiply3x3(XyzD65ToLinearSrgbMatrix, adaptation);
    }

    private static double[] Multiply3x1(double[] m, double x, double y, double z) =>
    [
        (m[0] * x) + (m[1] * y) + (m[2] * z),
        (m[3] * x) + (m[4] * y) + (m[5] * z),
        (m[6] * x) + (m[7] * y) + (m[8] * z),
    ];

    private static double[] Multiply3x3(double[] a, double[] b)
    {
        var result = new double[9];
        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                result[(row * 3) + column] =
                    (a[row * 3] * b[column]) + (a[(row * 3) + 1] * b[3 + column]) + (a[(row * 3) + 2] * b[6 + column]);
            }
        }

        return result;
    }

    /// <summary>
    ///     Converts <c>Separation</c>/<c>DeviceN</c> tint values (each clamped to <c>[0, 1]</c>) to
    ///     sRGB: a space whose colorants are all <c>/None</c> yields a fully transparent color
    ///     (alpha <c>0</c>, so painting is a no-op); otherwise the tint transform maps the tints
    ///     into the alternate space's natural units.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="tints"/> does not have one value per colorant, or when the
    ///     tint transform fails (for example a malformed PostScript calculator program).
    /// </exception>
    private static Rgba32 TintToColor(PdfColorSpace colorSpace, ReadOnlySpan<double> tints)
    {
        if (tints.Length != colorSpace.ColorantNames.Length)
        {
            throw new InvalidDataException(
                $"A {colorSpace.Kind} color needs {colorSpace.ColorantNames.Length} component(s); got {tints.Length}.");
        }

        if (colorSpace.PaintsNothing)
        {
            return new Rgba32(0, 0, 0, 0);
        }

        var function = colorSpace.TintFunction!;
        Span<double> input = tints.Length <= MaxStackColorComponents ? stackalloc double[MaxStackColorComponents] : new double[tints.Length];
        input = input[..tints.Length];
        for (var i = 0; i < tints.Length; i++)
        {
            input[i] = Clamp01(tints[i]);
        }

        var outputCount = function.OutputCount;
        Span<double> output = outputCount <= MaxStackColorComponents ? stackalloc double[MaxStackColorComponents] : new double[outputCount];
        output = output[..outputCount];
        function.Evaluate(input, output);
        return ColorFromComponentSpan(colorSpace.TintAlternate!, output);
    }

    /// <summary>Converts a <c>CalGray</c> value <c>A</c> (clamped to <c>[0, 1]</c>) to sRGB.</summary>
    private static Rgba32 CalGrayToColor(PdfColorSpace colorSpace, double gray)
    {
        var luminance = Math.Pow(Clamp01(gray), colorSpace.CalGamma[0]);
        var white = colorSpace.CalWhitePoint;
        return XyzToColor(colorSpace, white[0] * luminance, white[1] * luminance, white[2] * luminance);
    }

    /// <summary>Converts a <c>CalRGB</c> value <c>A B C</c> (each clamped to <c>[0, 1]</c>) to sRGB.</summary>
    private static Rgba32 CalRgbToColor(PdfColorSpace colorSpace, double a, double b, double c)
    {
        var gamma = colorSpace.CalGamma;
        var m = colorSpace.CalMatrix;
        var ag = Math.Pow(Clamp01(a), gamma[0]);
        var bg = Math.Pow(Clamp01(b), gamma[1]);
        var cg = Math.Pow(Clamp01(c), gamma[2]);

        return XyzToColor(
            colorSpace,
            (m[0] * ag) + (m[3] * bg) + (m[6] * cg),
            (m[1] * ag) + (m[4] * bg) + (m[7] * cg),
            (m[2] * ag) + (m[5] * bg) + (m[8] * cg));
    }

    /// <summary>
    ///     Converts a <c>Lab</c> value in natural units (<c>L*</c> in <c>[0, 100]</c>, <c>a*</c>/<c>b*</c>
    ///     within <see cref="PdfColorSpace.LabRange"/>, each clamped) to sRGB.
    /// </summary>
    private static Rgba32 LabToColor(PdfColorSpace colorSpace, double lStar, double aStar, double bStar)
    {
        var range = colorSpace.LabRange;
        var l = Math.Clamp(lStar, 0.0, 100.0);
        var a = Math.Clamp(aStar, range[0], range[1]);
        var b = Math.Clamp(bStar, range[2], range[3]);

        var m = (l + 16.0) / 116.0;
        var lPrime = m + (a / 500.0);
        var n = m - (b / 200.0);

        var white = colorSpace.CalWhitePoint;
        return XyzToColor(colorSpace, white[0] * LabInverse(lPrime), white[1] * LabInverse(m), white[2] * LabInverse(n));
    }

    private static double LabInverse(double x) =>
        x >= 6.0 / 29.0 ? x * x * x : 108.0 / 841.0 * (x - (4.0 / 29.0));

    /// <summary>Converts CIE XYZ (relative to the space's own white point) to an opaque sRGB color.</summary>
    private static Rgba32 XyzToColor(PdfColorSpace colorSpace, double x, double y, double z)
    {
        var m = colorSpace.XyzToLinearRgb;
        var r = (m[0] * x) + (m[1] * y) + (m[2] * z);
        var g = (m[3] * x) + (m[4] * y) + (m[5] * z);
        var b = (m[6] * x) + (m[7] * y) + (m[8] * z);
        return new Rgba32(SrgbEncodeToByte(r), SrgbEncodeToByte(g), SrgbEncodeToByte(b), 255);
    }

    /// <summary>Applies the sRGB transfer function to a linear component (clamped to <c>[0, 1]</c>) and rounds to a byte.</summary>
    private static byte SrgbEncodeToByte(double linear)
    {
        var c = Clamp01(linear);
        var encoded = c <= 0.0031308 ? 12.92 * c : (1.055 * Math.Pow(c, 1.0 / 2.4)) - 0.055;
        return ToByte(encoded * 255.0);
    }
}
