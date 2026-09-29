// cspell:ignore dropshadow
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Svg;

public static partial class SvgCodec
{
    /// <summary>
    ///     Evaluates one <c>feDropShadow</c> primitive by composing the already-implemented blur,
    ///     offset, flood, and composite helpers.
    /// </summary>
    /// <param name="element">The <c>feDropShadow</c> element.</param>
    /// <param name="input">The already-resolved input buffer.</param>
    /// <param name="scale">The local-space-to-pixel-space scale factor.</param>
    /// <returns>A new, independent output buffer.</returns>
    private static Surface ApplyFeDropShadow(XElement element, Surface input, float scale)
    {
        var syntheticElement = new XElement(element);
        if (syntheticElement.Attribute("stdDeviation") == null)
        {
            syntheticElement.SetAttributeValue("stdDeviation", "2");
        }

        if (syntheticElement.Attribute("dx") == null)
        {
            syntheticElement.SetAttributeValue("dx", "2");
        }

        if (syntheticElement.Attribute("dy") == null)
        {
            syntheticElement.SetAttributeValue("dy", "2");
        }

        var shadowAlpha = BuildSourceAlpha(input);
        var blurred = ApplyFeGaussianBlur(syntheticElement, shadowAlpha, scale);
        var offset = ApplyFeOffset(syntheticElement, blurred, scale);
        var flood = ApplyFeFlood(syntheticElement, input.Width, input.Height);
        var tintedShadow = CompositeFeOperator(flood, offset, FeCompositeOperator.In);
        var result = tintedShadow.Crop(0, 0, tintedShadow.Width, tintedShadow.Height);
        result.CompositeOver(input);
        return result;
    }
}
