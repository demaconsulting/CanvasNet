// cspell:ignore fetile
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Svg;

public static partial class SvgCodec
{
    /// <summary>
    ///     Evaluates one <c>feTile</c> primitive by repeating its input primitive's own resolved
    ///     subregion across the whole filter buffer.
    /// </summary>
    /// <param name="input">The already-resolved input buffer.</param>
    /// <param name="inputSubregion">The input primitive's own resolved subregion.</param>
    /// <returns>A new, independent tiled buffer.</returns>
    private static Surface ApplyFeTile(Surface input, PixelRect inputSubregion)
    {
        if (inputSubregion.IsEmpty)
        {
            return new Surface(input.Width, input.Height);
        }

        if (inputSubregion.X == 0 && inputSubregion.Y == 0 &&
            inputSubregion.Width == input.Width && inputSubregion.Height == input.Height)
        {
            return input.Crop(0, 0, input.Width, input.Height);
        }

        var output = new Surface(input.Width, input.Height);
        for (var y = 0; y < output.Height; y++)
        {
            var outputRow = output.GetRowSpan(y);
            for (var x = 0; x < output.Width; x++)
            {
                var wrappedX = inputSubregion.X + (int)WrapCoordinate((x + 0.5f) - inputSubregion.X, inputSubregion.Width);
                var wrappedY = inputSubregion.Y + (int)WrapCoordinate((y + 0.5f) - inputSubregion.Y, inputSubregion.Height);
                outputRow[x] = input[wrappedX, wrappedY];
            }
        }

        return output;
    }
}
