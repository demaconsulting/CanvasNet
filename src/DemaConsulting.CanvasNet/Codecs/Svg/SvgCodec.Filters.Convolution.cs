// cspell:ignore convolvematrix preservealpha edgemode kernelmatrix
using System.Globalization;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Codecs;

public static partial class SvgCodec
{
    /// <summary>
    ///     Enumerates the supported <c>feConvolveMatrix</c> edge modes.
    /// </summary>
    private enum ConvolveEdgeMode
    {
        /// <summary>Clamps kernel taps to the source edge.</summary>
        Duplicate,

        /// <summary>Wraps kernel taps to the opposite edge.</summary>
        Wrap,

        /// <summary>Treats out-of-bounds taps as transparent black.</summary>
        None
    }

    /// <summary>
    ///     Parses one <c>feConvolveMatrix</c> <c>order</c> attribute, applying the documented cap.
    /// </summary>
    /// <param name="element">The <c>feConvolveMatrix</c> element.</param>
    /// <returns>The resolved kernel order.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when either order component is non-positive or exceeds
    ///     <see cref="MaxConvolveMatrixOrder"/>.
    /// </exception>
    private static (int X, int Y) GetConvolveMatrixOrder(XElement element)
    {
        var rawOrder = (string?)element.Attribute("order");
        if (string.IsNullOrWhiteSpace(rawOrder))
        {
            return (3, 3);
        }

        var tokens = rawOrder.Split([' ', '\t', '\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return (3, 3);
        }

        if (!int.TryParse(tokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var orderX))
        {
            return (3, 3);
        }

        var orderY = orderX;
        if (tokens.Length > 1 &&
            !int.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out orderY))
        {
            return (3, 3);
        }

        if (orderX <= 0 || orderY <= 0 || orderX > MaxConvolveMatrixOrder || orderY > MaxConvolveMatrixOrder)
        {
            throw new InvalidDataException($"The feConvolveMatrix order must be between 1 and {MaxConvolveMatrixOrder} on each axis.");
        }

        return (orderX, orderY);
    }

    /// <summary>
    ///     Evaluates one <c>feConvolveMatrix</c> primitive against <paramref name="input"/>.
    /// </summary>
    /// <param name="element">The <c>feConvolveMatrix</c> element.</param>
    /// <param name="input">The already-resolved input buffer.</param>
    /// <returns>A new, independent output buffer.</returns>
    private static Surface ApplyFeConvolveMatrix(XElement element, Surface input)
    {
        var order = GetConvolveMatrixOrder(element);
        var rawKernelMatrix = (string?)element.Attribute("kernelMatrix");
        if (string.IsNullOrWhiteSpace(rawKernelMatrix))
        {
            return input.Crop(0, 0, input.Width, input.Height);
        }

        var kernelMatrix = ParseNumberList(rawKernelMatrix);
        if (kernelMatrix.Count != order.X * order.Y)
        {
            return input.Crop(0, 0, input.Width, input.Height);
        }

        var divisor = ParseFirstNumberToken((string?)element.Attribute("divisor")) ?? kernelMatrix.Sum();
        if (!float.IsFinite(divisor) || Math.Abs(divisor) < float.Epsilon)
        {
            divisor = 1f;
        }

        var bias = ParseFirstNumberToken((string?)element.Attribute("bias")) ?? 0f;
        var targetX = ReadOptionalConvolveInt(element, "targetX") ?? (order.X / 2);
        var targetY = ReadOptionalConvolveInt(element, "targetY") ?? (order.Y / 2);
        targetX = Math.Clamp(targetX, 0, order.X - 1);
        targetY = Math.Clamp(targetY, 0, order.Y - 1);

        var edgeMode = ReadConvolveEdgeMode(element);
        var preserveAlpha = string.Equals(((string?)element.Attribute("preserveAlpha"))?.Trim(), "true", StringComparison.OrdinalIgnoreCase);

        return preserveAlpha
            ? ApplyConvolveMatrixPreservingAlpha(input, order, kernelMatrix, divisor, bias, targetX, targetY, edgeMode)
            : ApplyConvolveMatrixPremultiplied(input, order, kernelMatrix, divisor, bias, targetX, targetY, edgeMode);
    }

    /// <summary>
    ///     Applies <c>feConvolveMatrix</c> in premultiplied-alpha space.
    /// </summary>
    /// <param name="input">The already-resolved input buffer.</param>
    /// <param name="order">The kernel order.</param>
    /// <param name="kernelMatrix">The kernel coefficients.</param>
    /// <param name="divisor">The effective divisor.</param>
    /// <param name="bias">The additive bias.</param>
    /// <param name="targetX">The target X index.</param>
    /// <param name="targetY">The target Y index.</param>
    /// <param name="edgeMode">The edge-sampling behavior.</param>
    /// <returns>A new, independent output buffer.</returns>
    private static Surface ApplyConvolveMatrixPremultiplied(
        Surface input,
        (int X, int Y) order,
        IReadOnlyList<float> kernelMatrix,
        float divisor,
        float bias,
        int targetX,
        int targetY,
        ConvolveEdgeMode edgeMode)
    {
        var working = input.Crop(0, 0, input.Width, input.Height);
        working.PremultiplyAlpha();
        var output = new Surface(working.Width, working.Height);

        for (var y = 0; y < working.Height; y++)
        {
            var outputRow = output.GetRowSpan(y);
            for (var x = 0; x < working.Width; x++)
            {
                float sumRed = 0f, sumGreen = 0f, sumBlue = 0f, sumAlpha = 0f;
                for (var kernelY = 0; kernelY < order.Y; kernelY++)
                {
                    for (var kernelX = 0; kernelX < order.X; kernelX++)
                    {
                        var sampleX = x + kernelX - targetX;
                        var sampleY = y + kernelY - targetY;
                        var sample = ReadConvolveSample(working, sampleX, sampleY, edgeMode);

                        // The kernel index is deliberately mirrored (180-degree flip) per the
                        // SVG Filter Effects spec's convolution formula, which multiplies
                        // SOURCE(X-targetX+J, Y-targetY+I) by kernelMatrix(orderX-J-1, orderY-I-1).
                        // Do not "simplify" this back to a plain (kernelY * order.X) + kernelX
                        // index -- that would silently regress to correlation instead of
                        // true convolution for any non-point-symmetric kernel.
                        var coefficient = kernelMatrix[((order.Y - kernelY - 1) * order.X) + (order.X - kernelX - 1)];
                        sumRed += coefficient * sample.R;
                        sumGreen += coefficient * sample.G;
                        sumBlue += coefficient * sample.B;
                        sumAlpha += coefficient * sample.A;
                    }
                }

                outputRow[x] = new Rgba32(
                    ToByte((sumRed / divisor) + (bias * 255f)),
                    ToByte((sumGreen / divisor) + (bias * 255f)),
                    ToByte((sumBlue / divisor) + (bias * 255f)),
                    ToByte((sumAlpha / divisor) + (bias * 255f)));
            }
        }

        output.UnpremultiplyAlpha();
        return output;
    }

    /// <summary>
    ///     Applies <c>feConvolveMatrix</c> while preserving the input alpha channel.
    /// </summary>
    /// <param name="input">The already-resolved input buffer.</param>
    /// <param name="order">The kernel order.</param>
    /// <param name="kernelMatrix">The kernel coefficients.</param>
    /// <param name="divisor">The effective divisor.</param>
    /// <param name="bias">The additive bias.</param>
    /// <param name="targetX">The target X index.</param>
    /// <param name="targetY">The target Y index.</param>
    /// <param name="edgeMode">The edge-sampling behavior.</param>
    /// <returns>A new, independent output buffer.</returns>
    private static Surface ApplyConvolveMatrixPreservingAlpha(
        Surface input,
        (int X, int Y) order,
        IReadOnlyList<float> kernelMatrix,
        float divisor,
        float bias,
        int targetX,
        int targetY,
        ConvolveEdgeMode edgeMode)
    {
        var output = new Surface(input.Width, input.Height);
        for (var y = 0; y < input.Height; y++)
        {
            var inputRow = input.GetRowSpan(y);
            var outputRow = output.GetRowSpan(y);
            for (var x = 0; x < input.Width; x++)
            {
                float sumRed = 0f, sumGreen = 0f, sumBlue = 0f;
                for (var kernelY = 0; kernelY < order.Y; kernelY++)
                {
                    for (var kernelX = 0; kernelX < order.X; kernelX++)
                    {
                        var sampleX = x + kernelX - targetX;
                        var sampleY = y + kernelY - targetY;
                        var sample = ReadConvolveSample(input, sampleX, sampleY, edgeMode);

                        // The kernel index is deliberately mirrored (180-degree flip) per the
                        // SVG Filter Effects spec's convolution formula, which multiplies
                        // SOURCE(X-targetX+J, Y-targetY+I) by kernelMatrix(orderX-J-1, orderY-I-1).
                        // Do not "simplify" this back to a plain (kernelY * order.X) + kernelX
                        // index -- that would silently regress to correlation instead of
                        // true convolution for any non-point-symmetric kernel.
                        var coefficient = kernelMatrix[((order.Y - kernelY - 1) * order.X) + (order.X - kernelX - 1)];
                        sumRed += coefficient * sample.R;
                        sumGreen += coefficient * sample.G;
                        sumBlue += coefficient * sample.B;
                    }
                }

                outputRow[x] = new Rgba32(
                    ToByte((sumRed / divisor) + (bias * 255f)),
                    ToByte((sumGreen / divisor) + (bias * 255f)),
                    ToByte((sumBlue / divisor) + (bias * 255f)),
                    inputRow[x].A);
            }
        }

        return output;
    }

    /// <summary>
    ///     Reads one kernel-tap sample according to <paramref name="edgeMode"/>.
    /// </summary>
    /// <param name="surface">The source surface.</param>
    /// <param name="x">The requested X coordinate.</param>
    /// <param name="y">The requested Y coordinate.</param>
    /// <param name="edgeMode">The edge-sampling behavior.</param>
    /// <returns>The sampled pixel, or transparent black for <see cref="ConvolveEdgeMode.None"/>.</returns>
    private static Rgba32 ReadConvolveSample(Surface surface, int x, int y, ConvolveEdgeMode edgeMode)
    {
        switch (edgeMode)
        {
            case ConvolveEdgeMode.Wrap:
                return surface[(int)WrapCoordinate(x, surface.Width), (int)WrapCoordinate(y, surface.Height)];

            case ConvolveEdgeMode.None:
                if (x < 0 || x >= surface.Width || y < 0 || y >= surface.Height)
                {
                    return default;
                }

                return surface[x, y];

            default:
                return surface[Math.Clamp(x, 0, surface.Width - 1), Math.Clamp(y, 0, surface.Height - 1)];
        }
    }

    /// <summary>
    ///     Reads one optional integer-valued <c>feConvolveMatrix</c> attribute.
    /// </summary>
    /// <param name="element">The element to inspect.</param>
    /// <param name="attributeName">The attribute name.</param>
    /// <returns>The parsed integer, or <see langword="null"/> if absent or not parseable.</returns>
    private static int? ReadOptionalConvolveInt(XElement element, string attributeName)
    {
        var raw = ((string?)element.Attribute(attributeName))?.Trim();
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    /// <summary>
    ///     Reads one <c>feConvolveMatrix</c> edge mode.
    /// </summary>
    /// <param name="element">The element to inspect.</param>
    /// <returns>The resolved edge mode.</returns>
    private static ConvolveEdgeMode ReadConvolveEdgeMode(XElement element)
    {
        var raw = ((string?)element.Attribute("edgeMode"))?.Trim();
        return raw switch
        {
            _ when string.Equals(raw, "wrap", StringComparison.OrdinalIgnoreCase) => ConvolveEdgeMode.Wrap,
            _ when string.Equals(raw, "none", StringComparison.OrdinalIgnoreCase) => ConvolveEdgeMode.None,
            _ => ConvolveEdgeMode.Duplicate
        };
    }
}
