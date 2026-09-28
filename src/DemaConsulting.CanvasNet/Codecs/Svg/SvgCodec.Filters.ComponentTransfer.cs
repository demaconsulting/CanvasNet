// cspell:ignore componenttransfer tablevalues
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Codecs;

public static partial class SvgCodec
{
    /// <summary>
    ///     Enumerates the supported <c>feComponentTransfer</c> function types.
    /// </summary>
    private enum ComponentTransferType
    {
        /// <summary>Leaves the channel unchanged.</summary>
        Identity,

        /// <summary>Interpolates between the supplied table entries.</summary>
        Table,

        /// <summary>Selects one discrete table entry.</summary>
        Discrete,

        /// <summary>Applies a linear slope/intercept mapping.</summary>
        Linear,

        /// <summary>Applies a gamma curve.</summary>
        Gamma
    }

    /// <summary>
    ///     Captures one channel transfer-function definition from a <c>feFunc*</c> child.
    /// </summary>
    /// <param name="Type">The function type.</param>
    /// <param name="TableValues">The parsed table values for table/discrete functions.</param>
    /// <param name="Slope">The linear slope.</param>
    /// <param name="Intercept">The linear intercept.</param>
    /// <param name="Amplitude">The gamma amplitude.</param>
    /// <param name="Exponent">The gamma exponent.</param>
    /// <param name="Offset">The gamma offset.</param>
    private readonly record struct ComponentTransferFunction(
        ComponentTransferType Type,
        IReadOnlyList<float> TableValues,
        float Slope,
        float Intercept,
        float Amplitude,
        float Exponent,
        float Offset);

    /// <summary>
    ///     Evaluates one <c>feComponentTransfer</c> primitive against <paramref name="input"/>.
    /// </summary>
    /// <param name="element">The <c>feComponentTransfer</c> element.</param>
    /// <param name="input">The already-resolved input buffer.</param>
    /// <returns>A new, independent output buffer.</returns>
    private static Surface ApplyFeComponentTransfer(XElement element, Surface input)
    {
        var identity = new ComponentTransferFunction(ComponentTransferType.Identity, [], 1f, 0f, 1f, 1f, 0f);
        var redFunction = identity;
        var greenFunction = identity;
        var blueFunction = identity;
        var alphaFunction = identity;

        foreach (var child in element.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "feFuncR":
                    redFunction = ParseComponentTransferFunction(child);
                    break;

                case "feFuncG":
                    greenFunction = ParseComponentTransferFunction(child);
                    break;

                case "feFuncB":
                    blueFunction = ParseComponentTransferFunction(child);
                    break;

                case "feFuncA":
                    alphaFunction = ParseComponentTransferFunction(child);
                    break;
            }
        }

        var output = new Surface(input.Width, input.Height);
        for (var y = 0; y < input.Height; y++)
        {
            var inputRow = input.GetRowSpan(y);
            var outputRow = output.GetRowSpan(y);
            for (var x = 0; x < input.Width; x++)
            {
                var pixel = inputRow[x];
                outputRow[x] = new Rgba32(
                    ToByte(ApplyComponentTransferFunction(redFunction, pixel.R / 255f) * 255f),
                    ToByte(ApplyComponentTransferFunction(greenFunction, pixel.G / 255f) * 255f),
                    ToByte(ApplyComponentTransferFunction(blueFunction, pixel.B / 255f) * 255f),
                    ToByte(ApplyComponentTransferFunction(alphaFunction, pixel.A / 255f) * 255f));
            }
        }

        return output;
    }

    /// <summary>
    ///     Parses one <c>feFunc*</c> child element.
    /// </summary>
    /// <param name="element">The child element to parse.</param>
    /// <returns>The parsed transfer function.</returns>
    private static ComponentTransferFunction ParseComponentTransferFunction(XElement element)
    {
        var type = ((string?)element.Attribute("type"))?.Trim();
        var parsedType = type switch
        {
            null or "" => ComponentTransferType.Identity,
            _ when string.Equals(type, "table", StringComparison.OrdinalIgnoreCase) => ComponentTransferType.Table,
            _ when string.Equals(type, "discrete", StringComparison.OrdinalIgnoreCase) => ComponentTransferType.Discrete,
            _ when string.Equals(type, "linear", StringComparison.OrdinalIgnoreCase) => ComponentTransferType.Linear,
            _ when string.Equals(type, "gamma", StringComparison.OrdinalIgnoreCase) => ComponentTransferType.Gamma,
            _ => ComponentTransferType.Identity
        };

        var tableValuesRaw = (string?)element.Attribute("tableValues");
        var tableValues = string.IsNullOrWhiteSpace(tableValuesRaw) ? [] : ParseNumberList(tableValuesRaw);
        var slope = ParseFirstNumberToken((string?)element.Attribute("slope")) ?? 1f;
        var intercept = ParseFirstNumberToken((string?)element.Attribute("intercept")) ?? 0f;
        var amplitude = ParseFirstNumberToken((string?)element.Attribute("amplitude")) ?? 1f;
        var exponent = ParseFirstNumberToken((string?)element.Attribute("exponent")) ?? 1f;
        var offset = ParseFirstNumberToken((string?)element.Attribute("offset")) ?? 0f;

        return new ComponentTransferFunction(parsedType, tableValues, slope, intercept, amplitude, exponent, offset);
    }

    /// <summary>
    ///     Applies one parsed transfer function to a normalized channel value.
    /// </summary>
    /// <param name="function">The parsed transfer function.</param>
    /// <param name="value">The normalized channel value.</param>
    /// <returns>The transformed channel value clamped to <c>[0, 1]</c>.</returns>
    private static float ApplyComponentTransferFunction(ComponentTransferFunction function, float value)
    {
        value = Math.Clamp(value, 0f, 1f);
        var result = function.Type switch
        {
            ComponentTransferType.Table => ApplyTableTransfer(function.TableValues, value),
            ComponentTransferType.Discrete => ApplyDiscreteTransfer(function.TableValues, value),
            ComponentTransferType.Linear => (function.Slope * value) + function.Intercept,
            ComponentTransferType.Gamma => (function.Amplitude * MathF.Pow(value, function.Exponent)) + function.Offset,
            _ => value
        };

        return Math.Clamp(result, 0f, 1f);
    }

    /// <summary>
    ///     Applies a table transfer function by piecewise-linear interpolation.
    /// </summary>
    /// <param name="tableValues">The parsed table values.</param>
    /// <param name="value">The normalized channel value.</param>
    /// <returns>The interpolated channel value.</returns>
    private static float ApplyTableTransfer(IReadOnlyList<float> tableValues, float value)
    {
        if (tableValues.Count < 2)
        {
            return value;
        }

        var scaled = value * (tableValues.Count - 1);
        var index = Math.Min((int)MathF.Floor(scaled), tableValues.Count - 2);
        var fraction = scaled - index;
        var start = tableValues[index];
        var end = tableValues[index + 1];
        return start + ((end - start) * fraction);
    }

    /// <summary>
    ///     Applies a discrete transfer function.
    /// </summary>
    /// <param name="tableValues">The parsed table values.</param>
    /// <param name="value">The normalized channel value.</param>
    /// <returns>The selected channel value.</returns>
    private static float ApplyDiscreteTransfer(IReadOnlyList<float> tableValues, float value)
    {
        if (tableValues.Count == 0)
        {
            return value;
        }

        var index = Math.Min((int)MathF.Floor(value * tableValues.Count), tableValues.Count - 1);
        return tableValues[index];
    }
}
