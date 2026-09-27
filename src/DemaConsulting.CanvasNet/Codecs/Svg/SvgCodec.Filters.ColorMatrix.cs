// cspell:ignore huerotate luminancetoalpha
using System.Numerics;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Codecs;

public static partial class SvgCodec
{
    /// <summary>
    ///     Evaluates one <c>feColorMatrix</c> primitive against <paramref name="input"/>.
    /// </summary>
    /// <param name="element">The <c>feColorMatrix</c> element.</param>
    /// <param name="input">The already-resolved input buffer.</param>
    /// <returns>A new, independent output buffer.</returns>
    /// <remarks>
    ///     Operates directly on this codec's stored straight-alpha RGBA values. The default
    ///     <c>type</c> is <c>matrix</c>; missing values for <c>matrix</c>, <c>saturate</c>, and
    ///     <c>hueRotate</c> fall back to the SVG defaults of identity, <c>1</c>, and <c>0</c>
    ///     respectively.
    /// </remarks>
    private static Surface ApplyFeColorMatrix(XElement element, Surface input)
    {
        var type = ((string?)element.Attribute("type"))?.Trim();
        return string.IsNullOrEmpty(type) || string.Equals(type, "matrix", StringComparison.OrdinalIgnoreCase)
            ? ApplyColorMatrix(element, input)
            : type switch
            {
                _ when string.Equals(type, "saturate", StringComparison.OrdinalIgnoreCase) => ApplyColorMatrix(input, BuildSaturateColorMatrix(ParseFirstNumberToken((string?)element.Attribute("values")) ?? 1f)),
                _ when string.Equals(type, "hueRotate", StringComparison.OrdinalIgnoreCase) => ApplyColorMatrix(input, BuildHueRotateColorMatrix(ParseFirstNumberToken((string?)element.Attribute("values")) ?? 0f)),
                _ when string.Equals(type, "luminanceToAlpha", StringComparison.OrdinalIgnoreCase) => ApplyLuminanceToAlpha(input),
                _ => input.Crop(0, 0, input.Width, input.Height)
            };
    }

    /// <summary>
    ///     Applies the default <c>matrix</c> variant of <c>feColorMatrix</c>.
    /// </summary>
    /// <param name="element">The <c>feColorMatrix</c> element.</param>
    /// <param name="input">The already-resolved input buffer.</param>
    /// <returns>A new, independent output buffer.</returns>
    private static Surface ApplyColorMatrix(XElement element, Surface input)
    {
        var rawValues = (string?)element.Attribute("values");
        if (string.IsNullOrWhiteSpace(rawValues))
        {
            return ApplyColorMatrix(input, BuildIdentityColorMatrix());
        }

        var values = ParseNumberList(rawValues);
        if (values.Count != 20)
        {
            return input.Crop(0, 0, input.Width, input.Height);
        }

        return ApplyColorMatrix(input, [.. values]);
    }

    /// <summary>
    ///     Applies one 4x5 affine color matrix to every pixel of <paramref name="input"/>.
    /// </summary>
    /// <param name="input">The already-resolved input buffer.</param>
    /// <param name="matrix">The 20 row-major matrix coefficients.</param>
    /// <returns>A new, independent output buffer.</returns>
    private static Surface ApplyColorMatrix(Surface input, float[] matrix)
    {
        var output = new Surface(input.Width, input.Height);
        for (var y = 0; y < input.Height; y++)
        {
            var inputRow = input.GetRowSpan(y);
            var outputRow = output.GetRowSpan(y);
            for (var x = 0; x < input.Width; x++)
            {
                var pixel = inputRow[x];
                var red = pixel.R / 255f;
                var green = pixel.G / 255f;
                var blue = pixel.B / 255f;
                var alpha = pixel.A / 255f;

                var outRed = (matrix[0] * red) + (matrix[1] * green) + (matrix[2] * blue) + (matrix[3] * alpha) + matrix[4];
                var outGreen = (matrix[5] * red) + (matrix[6] * green) + (matrix[7] * blue) + (matrix[8] * alpha) + matrix[9];
                var outBlue = (matrix[10] * red) + (matrix[11] * green) + (matrix[12] * blue) + (matrix[13] * alpha) + matrix[14];
                var outAlpha = (matrix[15] * red) + (matrix[16] * green) + (matrix[17] * blue) + (matrix[18] * alpha) + matrix[19];

                outputRow[x] = new Rgba32(
                    ToByte(Math.Clamp(outRed, 0f, 1f) * 255f),
                    ToByte(Math.Clamp(outGreen, 0f, 1f) * 255f),
                    ToByte(Math.Clamp(outBlue, 0f, 1f) * 255f),
                    ToByte(Math.Clamp(outAlpha, 0f, 1f) * 255f));
            }
        }

        return output;
    }

    /// <summary>
    ///     Converts RGB luminance into alpha while clearing color to black.
    /// </summary>
    /// <param name="input">The already-resolved input buffer.</param>
    /// <returns>A new, independent output buffer.</returns>
    private static Surface ApplyLuminanceToAlpha(Surface input)
    {
        var output = new Surface(input.Width, input.Height);
        for (var y = 0; y < input.Height; y++)
        {
            var inputRow = input.GetRowSpan(y);
            var outputRow = output.GetRowSpan(y);
            for (var x = 0; x < input.Width; x++)
            {
                var pixel = inputRow[x];
                outputRow[x] = new Rgba32(0, 0, 0, ToByte(ComputeLuminance(pixel.R, pixel.G, pixel.B) * 255f));
            }
        }

        return output;
    }

    /// <summary>
    ///     Builds the identity 4x5 color matrix.
    /// </summary>
    /// <returns>The row-major identity matrix.</returns>
    private static float[] BuildIdentityColorMatrix() =>
    [
        1f, 0f, 0f, 0f, 0f,
        0f, 1f, 0f, 0f, 0f,
        0f, 0f, 1f, 0f, 0f,
        0f, 0f, 0f, 1f, 0f
    ];

    /// <summary>
    ///     Builds the saturation-adjustment color matrix.
    /// </summary>
    /// <param name="saturation">The requested saturation factor.</param>
    /// <returns>The row-major 4x5 matrix.</returns>
    private static float[] BuildSaturateColorMatrix(float saturation)
    {
        var lumRed = ComputeLuminance(255, 0, 0);
        var lumGreen = ComputeLuminance(0, 255, 0);
        var lumBlue = ComputeLuminance(0, 0, 255);
        var inverse = 1f - saturation;

        return
        [
            (lumRed * inverse) + saturation, lumGreen * inverse, lumBlue * inverse, 0f, 0f,
            lumRed * inverse, (lumGreen * inverse) + saturation, lumBlue * inverse, 0f, 0f,
            lumRed * inverse, lumGreen * inverse, (lumBlue * inverse) + saturation, 0f, 0f,
            0f, 0f, 0f, 1f, 0f
        ];
    }

    /// <summary>
    ///     Builds a hue-rotation matrix around the shared luminance axis.
    /// </summary>
    /// <param name="degrees">The rotation angle, in degrees.</param>
    /// <returns>The row-major 4x5 matrix.</returns>
    private static float[] BuildHueRotateColorMatrix(float degrees)
    {
        var axis = new Vector3(
            ComputeLuminance(255, 0, 0),
            ComputeLuminance(0, 255, 0),
            ComputeLuminance(0, 0, 255));
        axis = Vector3.Normalize(axis);

        var radians = degrees * (MathF.PI / 180f);
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        var oneMinusCos = 1f - cos;

        var ux = axis.X;
        var uy = axis.Y;
        var uz = axis.Z;

        return
        [
            cos + (ux * ux * oneMinusCos), (ux * uy * oneMinusCos) - (uz * sin), (ux * uz * oneMinusCos) + (uy * sin), 0f, 0f,
            (uy * ux * oneMinusCos) + (uz * sin), cos + (uy * uy * oneMinusCos), (uy * uz * oneMinusCos) - (ux * sin), 0f, 0f,
            (uz * ux * oneMinusCos) - (uy * sin), (uz * uy * oneMinusCos) + (ux * sin), cos + (uz * uz * oneMinusCos), 0f, 0f,
            0f, 0f, 0f, 1f, 0f
        ];
    }
}
