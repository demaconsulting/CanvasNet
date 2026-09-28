// cspell:ignore morphology
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Codecs;

public static partial class SvgCodec
{
    /// <summary>
    ///     Evaluates one <c>feMorphology</c> primitive against <paramref name="input"/>.
    /// </summary>
    /// <param name="element">The <c>feMorphology</c> element.</param>
    /// <param name="input">The already-resolved input buffer.</param>
    /// <param name="scale">The local-space-to-pixel-space scale factor.</param>
    /// <returns>A new, independent output buffer.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when either resolved radius exceeds <see cref="MaxMorphologyRadiusPixels"/>.
    /// </exception>
    private static Surface ApplyFeMorphology(XElement element, Surface input, float scale)
    {
        var (radiusX, radiusY) = ResolveMorphologyRadii(element, scale);
        var working = input.Crop(0, 0, input.Width, input.Height);
        if (radiusX <= 0 && radiusY <= 0)
        {
            return working;
        }

        var dilate = string.Equals(((string?)element.Attribute("operator"))?.Trim(), "dilate", StringComparison.OrdinalIgnoreCase);
        working.PremultiplyAlpha();
        if (radiusX > 0)
        {
            ApplyMorphologyHorizontal(working, radiusX, dilate);
        }

        if (radiusY > 0)
        {
            ApplyMorphologyVertical(working, radiusY, dilate);
        }

        working.UnpremultiplyAlpha();
        return working;
    }

    /// <summary>
    ///     Resolves <c>feMorphology</c>'s one- or two-component radius into integer pixel radii.
    /// </summary>
    /// <param name="element">The <c>feMorphology</c> element.</param>
    /// <param name="scale">The local-space-to-pixel-space scale factor.</param>
    /// <returns>The resolved horizontal and vertical radii.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when either resolved radius exceeds <see cref="MaxMorphologyRadiusPixels"/>.
    /// </exception>
    private static (int X, int Y) ResolveMorphologyRadii(XElement element, float scale)
    {
        var rawRadius = (string?)element.Attribute("radius");
        if (string.IsNullOrWhiteSpace(rawRadius))
        {
            return (0, 0);
        }

        var numbers = ParseNumberList(rawRadius);
        if (numbers.Count == 0)
        {
            return (0, 0);
        }

        var radiusX = numbers[0] * scale;
        var radiusY = (numbers.Count > 1 ? numbers[1] : numbers[0]) * scale;
        if (!float.IsFinite(radiusX))
        {
            radiusX = 0f;
        }

        if (!float.IsFinite(radiusY))
        {
            radiusY = 0f;
        }

        if (radiusX > MaxMorphologyRadiusPixels || radiusY > MaxMorphologyRadiusPixels)
        {
            throw new InvalidDataException($"The feMorphology radius exceeds the maximum of {MaxMorphologyRadiusPixels} pixels.");
        }

        return
        (
            radiusX <= 0f ? 0 : (int)MathF.Round(radiusX, MidpointRounding.AwayFromZero),
            radiusY <= 0f ? 0 : (int)MathF.Round(radiusY, MidpointRounding.AwayFromZero)
        );
    }

    /// <summary>
    ///     Applies one horizontal morphology pass in place.
    /// </summary>
    /// <param name="surface">The already-premultiplied buffer to mutate.</param>
    /// <param name="radius">The horizontal radius.</param>
    /// <param name="dilate"><see langword="true"/> for max/dilate; <see langword="false"/> for min/erode.</param>
    private static void ApplyMorphologyHorizontal(Surface surface, int radius, bool dilate)
    {
        var width = surface.Width;
        var sourceRed = new byte[width];
        var sourceGreen = new byte[width];
        var sourceBlue = new byte[width];
        var sourceAlpha = new byte[width];
        var outputRed = new byte[width];
        var outputGreen = new byte[width];
        var outputBlue = new byte[width];
        var outputAlpha = new byte[width];

        for (var y = 0; y < surface.Height; y++)
        {
            var row = surface.GetRowSpan(y);
            for (var x = 0; x < width; x++)
            {
                var pixel = row[x];
                sourceRed[x] = pixel.R;
                sourceGreen[x] = pixel.G;
                sourceBlue[x] = pixel.B;
                sourceAlpha[x] = pixel.A;
            }

            ComputeSlidingExtrema(sourceRed, outputRed, radius, dilate);
            ComputeSlidingExtrema(sourceGreen, outputGreen, radius, dilate);
            ComputeSlidingExtrema(sourceBlue, outputBlue, radius, dilate);
            ComputeSlidingExtrema(sourceAlpha, outputAlpha, radius, dilate);

            for (var x = 0; x < width; x++)
            {
                row[x] = new Rgba32(outputRed[x], outputGreen[x], outputBlue[x], outputAlpha[x]);
            }
        }
    }

    /// <summary>
    ///     Applies one vertical morphology pass in place.
    /// </summary>
    /// <param name="surface">The already-premultiplied buffer to mutate.</param>
    /// <param name="radius">The vertical radius.</param>
    /// <param name="dilate"><see langword="true"/> for max/dilate; <see langword="false"/> for min/erode.</param>
    private static void ApplyMorphologyVertical(Surface surface, int radius, bool dilate)
    {
        var height = surface.Height;
        var sourceRed = new byte[height];
        var sourceGreen = new byte[height];
        var sourceBlue = new byte[height];
        var sourceAlpha = new byte[height];
        var outputRed = new byte[height];
        var outputGreen = new byte[height];
        var outputBlue = new byte[height];
        var outputAlpha = new byte[height];

        for (var x = 0; x < surface.Width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                var pixel = surface[x, y];
                sourceRed[y] = pixel.R;
                sourceGreen[y] = pixel.G;
                sourceBlue[y] = pixel.B;
                sourceAlpha[y] = pixel.A;
            }

            ComputeSlidingExtrema(sourceRed, outputRed, radius, dilate);
            ComputeSlidingExtrema(sourceGreen, outputGreen, radius, dilate);
            ComputeSlidingExtrema(sourceBlue, outputBlue, radius, dilate);
            ComputeSlidingExtrema(sourceAlpha, outputAlpha, radius, dilate);

            for (var y = 0; y < height; y++)
            {
                surface[x, y] = new Rgba32(outputRed[y], outputGreen[y], outputBlue[y], outputAlpha[y]);
            }
        }
    }

    /// <summary>
    ///     Computes one zero-padded sliding-window minimum or maximum in O(n) time.
    /// </summary>
    /// <param name="source">The source channel values.</param>
    /// <param name="destination">The destination span receiving the window extrema.</param>
    /// <param name="radius">The window radius.</param>
    /// <param name="pickMaximum"><see langword="true"/> for max; <see langword="false"/> for min.</param>
    private static void ComputeSlidingExtrema(ReadOnlySpan<byte> source, Span<byte> destination, int radius, bool pickMaximum)
    {
        if (radius <= 0)
        {
            source.CopyTo(destination);
            return;
        }

        var paddedLength = source.Length + (2 * radius);
        var padded = new byte[paddedLength];
        source.CopyTo(padded.AsSpan(radius));
        var windowIndices = new int[paddedLength];
        var head = 0;
        var tail = 0;
        var windowSize = (2 * radius) + 1;

        for (var index = 0; index < paddedLength; index++)
        {
            while (head < tail && (pickMaximum ? padded[index] >= padded[windowIndices[tail - 1]] : padded[index] <= padded[windowIndices[tail - 1]]))
            {
                tail--;
            }

            windowIndices[tail++] = index;
            var windowStart = index - windowSize + 1;
            if (windowStart < 0)
            {
                continue;
            }

            while (head < tail && windowIndices[head] < windowStart)
            {
                head++;
            }

            destination[windowStart] = padded[windowIndices[head]];
        }
    }
}
