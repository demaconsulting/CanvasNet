// cspell:ignore displacementmap channelselector
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Svg;

public static partial class SvgCodec
{
    /// <summary>
    ///     Evaluates one <c>feDisplacementMap</c> primitive against <paramref name="input"/>.
    /// </summary>
    /// <param name="element">The <c>feDisplacementMap</c> element.</param>
    /// <param name="input">The already-resolved <c>in</c> buffer.</param>
    /// <param name="displacementMap">The already-resolved <c>in2</c> displacement buffer.</param>
    /// <param name="scale">The local-space-to-pixel-space scale factor.</param>
    /// <returns>A new, independent output buffer.</returns>
    private static Surface ApplyFeDisplacementMap(XElement element, Surface input, Surface displacementMap, float scale)
    {
        var rawScale = ParsePercentOrNumber((string?)element.Attribute("scale") ?? string.Empty, 1f) ?? 0f;
        var pixelScale = rawScale * scale;
        var xSelector = ReadDisplacementChannelSelector((string?)element.Attribute("xChannelSelector"));
        var ySelector = ReadDisplacementChannelSelector((string?)element.Attribute("yChannelSelector"));

        var output = new Surface(input.Width, input.Height);
        for (var y = 0; y < input.Height; y++)
        {
            var displacementRow = displacementMap.GetRowSpan(y);
            var outputRow = output.GetRowSpan(y);
            for (var x = 0; x < input.Width; x++)
            {
                var mapPixel = displacementRow[x];
                var displacedX = x + (pixelScale * ((ReadDisplacementChannel(mapPixel, xSelector) / 255f) - 0.5f));
                var displacedY = y + (pixelScale * ((ReadDisplacementChannel(mapPixel, ySelector) / 255f) - 0.5f));
                if (displacedX < 0f || displacedX >= input.Width || displacedY < 0f || displacedY >= input.Height)
                {
                    continue;
                }

                outputRow[x] = input[Math.Clamp((int)displacedX, 0, input.Width - 1), Math.Clamp((int)displacedY, 0, input.Height - 1)];
            }
        }

        return output;
    }

    /// <summary>
    ///     Resolves one <c>feDisplacementMap</c> channel selector.
    /// </summary>
    /// <param name="rawSelector">The raw selector text.</param>
    /// <returns>The resolved channel, defaulting to alpha.</returns>
    private static char ReadDisplacementChannelSelector(string? rawSelector)
    {
        var selector = ((rawSelector ?? "A").Trim()).ToUpperInvariant();
        return selector is "R" or "G" or "B" ? selector[0] : 'A';
    }

    /// <summary>
    ///     Reads one selected channel from <paramref name="pixel"/>.
    /// </summary>
    /// <param name="pixel">The pixel to inspect.</param>
    /// <param name="selector">The selector character.</param>
    /// <returns>The selected channel byte.</returns>
    private static byte ReadDisplacementChannel(Rgba32 pixel, char selector) => selector switch
    {
        'R' => pixel.R,
        'G' => pixel.G,
        'B' => pixel.B,
        _ => pixel.A
    };
}
