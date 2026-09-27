using System.Numerics;

namespace DemaConsulting.CanvasNet.Codecs;

public static partial class JpegCodec
{
    // ================================================================================================
    // Shared 8x8 DCT math and color conversion (used by both Decoder and Encoder)
    // ================================================================================================

    /// <summary>
    ///     Computes the dot product of two 8-element arrays, using <see cref="Vector{T}"/> to
    ///     process <see cref="Vector{T}.Count"/> elements at a time with a scalar remainder loop
    ///     for whatever does not evenly divide 8.
    /// </summary>
    /// <remarks>
    ///     Architectural decision: this is the vectorized hot path shared by every row/column pass
    ///     of both the inverse and forward 8x8 DCT. Because a fixed vector width of 8
    ///     is not guaranteed on every runtime/hardware combination, this falls back to scalar
    ///     multiply-accumulate for the elements that do not fit a whole vector.
    /// </remarks>
    internal static double DotProduct8(double[] a, double[] b)
    {
        var count = Vector<double>.Count;
        var sum = 0.0;
        var i = 0;
        while (count > 0 && i + count <= 8)
        {
            sum += Vector.Dot(new Vector<double>(a, i), new Vector<double>(b, i));
            i += count;
        }

        for (; i < 8; i++)
        {
            sum += a[i] * b[i];
        }

        return sum;
    }

    private static double[][] NewBlock()
    {
        var block = new double[8][];
        for (var i = 0; i < 8; i++)
        {
            block[i] = new double[8];
        }

        return block;
    }

    private static readonly double[][] Basis = BuildBasis();
    private static readonly double[][] BasisT = Transpose(Basis);

    private static double[][] BuildBasis()
    {
        var basis = NewBlock();
        for (var k = 0; k < 8; k++)
        {
            var ck = k == 0 ? 1.0 / Math.Sqrt(2.0) : 1.0;
            for (var n = 0; n < 8; n++)
            {
                basis[k][n] = ck * Math.Cos((2.0 * n + 1) * k * Math.PI / 16.0);
            }
        }

        return basis;
    }

    private static double[][] Transpose(double[][] source)
    {
        var result = NewBlock();
        for (var i = 0; i < 8; i++)
        {
            for (var j = 0; j < 8; j++)
            {
                result[j][i] = source[i][j];
            }
        }

        return result;
    }

    private static byte ClampToByte(double value) => value switch
    {
        <= 0 => 0,
        >= 255 => 255,
        _ => (byte)Math.Round(value, MidpointRounding.AwayFromZero)
    };

    /// <summary>
    ///     Converts one row of Y/Cb/Cr samples to RGB using the ITU-R BT.601 coefficients, with
    ///     proper clamping to [0,255].
    /// </summary>
    /// <remarks>
    ///     Architectural decision: this is the vectorized color-conversion hot path, processing
    ///     <see cref="Vector{T}.Count"/> pixels at a time with a scalar remainder loop. It is
    ///     cross-checked against <see cref="ConvertYCbCrRowToRgbScalar"/> (a plain per-pixel
    ///     reference implementation of the same formula) by
    ///     <c>JpegCodecTests.JpegCodec_ConvertYCbCrRowToRgb_VectorAndScalarRemainder_MatchesScalarReference</c>.
    /// </remarks>
    internal static void ConvertYCbCrRowToRgb(
        byte[] y, byte[] cb, byte[] cr, byte[] r, byte[] g, byte[] b, int length)
    {
        var count = Vector<float>.Count;
        var i = 0;
        var yBuf = new float[count];
        var cbBuf = new float[count];
        var crBuf = new float[count];
        while (count > 0 && i + count <= length)
        {
            for (var k = 0; k < count; k++)
            {
                yBuf[k] = y[i + k];
                cbBuf[k] = cb[i + k] - 128f;
                crBuf[k] = cr[i + k] - 128f;
            }

            var yv = new Vector<float>(yBuf);
            var cbv = new Vector<float>(cbBuf);
            var crv = new Vector<float>(crBuf);

            var rv = yv + crv * new Vector<float>(1.402f);
            var gv = yv - cbv * new Vector<float>(0.344136f) - crv * new Vector<float>(0.714136f);
            var bv = yv + cbv * new Vector<float>(1.772f);

            for (var k = 0; k < count; k++)
            {
                r[i + k] = ClampToByte(rv[k]);
                g[i + k] = ClampToByte(gv[k]);
                b[i + k] = ClampToByte(bv[k]);
            }

            i += count;
        }

        for (; i < length; i++)
        {
            ConvertOnePixel(y[i], cb[i], cr[i], out r[i], out g[i], out b[i]);
        }
    }

    /// <summary>
    ///     A plain, per-pixel scalar reference implementation of the same YCbCr-&gt;RGB formula as
    ///     <see cref="ConvertYCbCrRowToRgb"/>, used only as the "known good" comparison target by
    ///     the SIMD-vs-scalar cross-check test.
    /// </summary>
    internal static void ConvertYCbCrRowToRgbScalar(
        byte[] y, byte[] cb, byte[] cr, byte[] r, byte[] g, byte[] b, int length)
    {
        for (var i = 0; i < length; i++)
        {
            ConvertOnePixel(y[i], cb[i], cr[i], out r[i], out g[i], out b[i]);
        }
    }

    private static void ConvertOnePixel(byte y, byte cb, byte cr, out byte r, out byte g, out byte b)
    {
        var cbf = cb - 128f;
        var crf = cr - 128f;
        r = ClampToByte(y + 1.402f * crf);
        g = ClampToByte(y - 0.344136f * cbf - 0.714136f * crf);
        b = ClampToByte(y + 1.772f * cbf);
    }

}
