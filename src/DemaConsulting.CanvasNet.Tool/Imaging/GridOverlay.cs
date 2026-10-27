// cspell:ignore luma
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Rendering;
using RenderCanvas = DemaConsulting.CanvasNet.Rendering.Canvas;

namespace DemaConsulting.CanvasNet.Tool.Imaging;

/// <summary>
///     What a grid overlay drew.
/// </summary>
/// <param name="VerticalLines">The number of vertical lines drawn, including the one on the left edge.</param>
/// <param name="HorizontalLines">The number of horizontal lines drawn, including the one on the top edge.</param>
/// <param name="Labels">The number of coordinate labels drawn.</param>
internal readonly record struct GridResult(int VerticalLines, int HorizontalLines, int Labels);

/// <summary>
///     A labeled coordinate grid drawn over the pixels of a surface, so that positions and sizes can be read off the picture.
/// </summary>
/// <remarks>
///     <para>
///         The grid is drawn <b>in place</b> on a surface the caller owns. Lines are 1 pixel wide at multiples of the
///         spacing, starting at the image edge; each pixel is blended at most once, so the intersections are not darker.
///         The pixel coordinate of a line is written next to it with the bundled Liberation Sans font (never a system
///         font), so the same inputs give the same pixels on every machine.
///     </para>
///     <para>
///         Blending is exact integer source-over arithmetic. Rows are processed in parallel with disjoint output, and
///         nothing allocates per pixel.
///     </para>
/// </remarks>
internal static class GridOverlay
{
    /// <summary>The smallest spacing in pixels between two lines.</summary>
    public const int MinSpacing = 8;

    /// <summary>The largest spacing in pixels between two lines.</summary>
    public const int MaxSpacing = 4096;

    /// <summary>The size in pixels of the label text.</summary>
    public const int LabelFontSize = 12;

    /// <summary>The alpha of the box behind a label.</summary>
    public const int BackingAlpha = 192;

    /// <summary>
    ///     Gets the default color of the grid lines, a translucent magenta <c>#FF00FFA0</c>.
    /// </summary>
    public static Rgba32 DefaultColor { get; } = new(255, 0, 255, 160);

    /// <summary>
    ///     Draws a labeled coordinate grid over a surface.
    /// </summary>
    /// <param name="surface">The surface to draw on, in place.</param>
    /// <param name="spacing">The distance in pixels between two lines, <see cref="MinSpacing"/> to <see cref="MaxSpacing"/>.</param>
    /// <param name="color">The color of the lines and the label text; its alpha is the opacity of the lines.</param>
    /// <returns>The number of lines and labels drawn.</returns>
    /// <remarks>
    ///     <para>
    ///         Vertical lines are at <c>x = k * spacing</c> and horizontal lines at <c>y = k * spacing</c> (<c>k</c> from 0, inside
    ///         the surface); the lines at 0 are the first column and row. A pixel on a line is blended with the color by
    ///         source-over. For an opaque pixel the result is <c>(c * a + p * (255 - a) + 127) / 255</c> per channel; for a
    ///         pixel with alpha <c>pa</c> the alpha is <c>(255 a + pa (255 - a) + 127) / 255</c> and the color
    ///         <c>(c a 255 + p pa (255 - a)) / (outA 255)</c> rounded, or transparent black when the result alpha is 0.
    ///     </para>
    ///     <para>
    ///         The label of a line is its coordinate in decimal digits, 12 pixels high, in a box 4 pixels wider and 2
    ///         pixels higher than the text. The box is filled with white (when the luma of the line color is below 128)
    ///         or black, blended with an alpha of 192; the text is drawn in the line color made opaque. Vertical-line
    ///         labels sit at the top edge (box left <c>x + 1</c>, top 1) and are drawn for every <c>Lv</c>-th line, where
    ///         <c>Lv = max(1, ceil((widest label width + 6) / spacing))</c>, always including the line at 0.
    ///         Horizontal-line labels sit at the left edge (box left 1, top <c>y + 1</c>) for every <c>Lh</c>-th line from the
    ///         first, <c>Lh = max(1, ceil((box height + 2) / spacing))</c>, only when the box starts below the row of top
    ///         labels (<c>y + 1 &gt;= box height + 3</c>). A label whose box does not lie fully inside the surface is skipped.
    ///     </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="surface"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="spacing"/> is not <see cref="MinSpacing"/> to <see cref="MaxSpacing"/>.</exception>
    public static GridResult Draw(Surface surface, int spacing, Rgba32 color)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentOutOfRangeException.ThrowIfLessThan(spacing, MinSpacing);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(spacing, MaxSpacing);

        var width = surface.Width;
        var height = surface.Height;
        var verticalLines = ((width - 1) / spacing) + 1;
        var horizontalLines = ((height - 1) / spacing) + 1;

        Parallel.For(0, height, y =>
        {
            var row = surface.GetRowSpan(y);
            if (y % spacing == 0)
            {
                for (var x = 0; x < width; x++)
                {
                    Blend(ref row[x], color, color.A);
                }
            }
            else
            {
                for (var x = 0; x < width; x += spacing)
                {
                    Blend(ref row[x], color, color.A);
                }
            }
        });

        var labels = DrawLabels(surface, spacing, color, verticalLines, horizontalLines);
        return new GridResult(verticalLines, horizontalLines, labels);
    }

    /// <summary>
    ///     Blends a color over a pixel (source-over, straight alpha, exact integer arithmetic).
    /// </summary>
    /// <param name="pixel">The pixel to change.</param>
    /// <param name="color">The color whose red, green, and blue are blended.</param>
    /// <param name="alpha">The opacity of the color, 0 to 255.</param>
    internal static void Blend(ref Rgba32 pixel, Rgba32 color, int alpha)
    {
        if (alpha <= 0)
        {
            return;
        }

        if (alpha >= 255)
        {
            pixel = new Rgba32(color.R, color.G, color.B, 255);
            return;
        }

        var pa = pixel.A;
        if (pa == 255)
        {
            var inverse = 255 - alpha;
            pixel.R = (byte)(((color.R * alpha) + (pixel.R * inverse) + 127) / 255);
            pixel.G = (byte)(((color.G * alpha) + (pixel.G * inverse) + 127) / 255);
            pixel.B = (byte)(((color.B * alpha) + (pixel.B * inverse) + 127) / 255);
            return;
        }

        var outAlpha = ((alpha * 255) + (pa * (255 - alpha)) + 127) / 255;
        if (outAlpha == 0)
        {
            pixel = new Rgba32(0, 0, 0, 0);
            return;
        }

        var denominator = outAlpha * 255;
        var weightSource = alpha * 255;
        var weightBackdrop = pa * (255 - alpha);
        pixel = new Rgba32(
            BlendChannel(color.R, pixel.R, weightSource, weightBackdrop, denominator),
            BlendChannel(color.G, pixel.G, weightSource, weightBackdrop, denominator),
            BlendChannel(color.B, pixel.B, weightSource, weightBackdrop, denominator),
            (byte)outAlpha);
    }

    /// <summary>
    ///     Combines one channel of a source and a backdrop with their weights.
    /// </summary>
    /// <param name="source">The source channel.</param>
    /// <param name="backdrop">The backdrop channel.</param>
    /// <param name="weightSource">The weight of the source, <c>alpha * 255</c>.</param>
    /// <param name="weightBackdrop">The weight of the backdrop, <c>backdropAlpha * (255 - alpha)</c>.</param>
    /// <param name="denominator">The sum of the weights as the result alpha times 255.</param>
    /// <returns>The rounded channel.</returns>
    private static byte BlendChannel(int source, int backdrop, int weightSource, int weightBackdrop, int denominator)
    {
        var value = ((source * weightSource) + (backdrop * weightBackdrop) + (denominator / 2)) / denominator;
        return (byte)Math.Min(255, value);
    }

    /// <summary>
    ///     Draws the coordinate labels of the grid.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="spacing">The spacing of the lines.</param>
    /// <param name="color">The color of the lines.</param>
    /// <param name="verticalLines">The number of vertical lines.</param>
    /// <param name="horizontalLines">The number of horizontal lines.</param>
    /// <returns>The number of labels drawn.</returns>
    private static int DrawLabels(Surface surface, int spacing, Rgba32 color, int verticalLines, int horizontalLines)
    {
        var font = SystemFontCatalog.LoadBundledFallback(serif: false, fixedPitch: false, bold: false, italic: false);
        var canvas = new RenderCanvas(surface);
        var sample = TextRenderer.MeasureText("0", font, LabelFontSize);
        var ascent = sample.Ascent;
        var boxHeight = (int)Math.Ceiling(sample.Ascent + sample.Descent) + 2;
        var backing = ImageAdjustments.Luma(new Rgba32(color.R, color.G, color.B, 255)) < 128
            ? new Rgba32(255, 255, 255, 255)
            : new Rgba32(0, 0, 0, 255);
        var textColor = new Rgba32(color.R, color.G, color.B, 255);

        var widest = 0;
        for (var k = 0; k < verticalLines; k++)
        {
            widest = Math.Max(widest, TextWidth(font, (k * spacing).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        var everyVertical = Math.Max(1, ((widest + 6) + spacing - 1) / spacing);
        var everyHorizontal = Math.Max(1, ((boxHeight + 2) + spacing - 1) / spacing);
        var labels = 0;

        for (var k = 0; k < verticalLines; k += everyVertical)
        {
            var x = k * spacing;
            var text = x.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var boxWidth = TextWidth(font, text) + 4;
            if (x + 1 + boxWidth <= surface.Width && 1 + boxHeight <= surface.Height)
            {
                FillBox(surface, x + 1, 1, boxWidth, boxHeight, backing);
                canvas.DrawText(text, x + 3, 2 + ascent, TextAlign.Left, font, LabelFontSize, textColor);
                labels++;
            }
        }

        for (var k = everyHorizontal; k < horizontalLines; k += everyHorizontal)
        {
            var y = k * spacing;
            var text = y.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var boxWidth = TextWidth(font, text) + 4;
            if (y + 1 >= boxHeight + 3 && 1 + boxWidth <= surface.Width && y + 1 + boxHeight <= surface.Height)
            {
                FillBox(surface, 1, y + 1, boxWidth, boxHeight, backing);
                canvas.DrawText(text, 3, y + 2 + ascent, TextAlign.Left, font, LabelFontSize, textColor);
                labels++;
            }
        }

        return labels;
    }

    /// <summary>
    ///     Measures the width of a label's text, rounded up to whole pixels.
    /// </summary>
    /// <param name="font">The font.</param>
    /// <param name="text">The text.</param>
    /// <returns>The width in pixels.</returns>
    private static int TextWidth(TrueTypeFont font, string text)
    {
        return (int)Math.Ceiling(TextRenderer.MeasureText(text, font, LabelFontSize).Width);
    }

    /// <summary>
    ///     Blends the backing color, at the backing alpha, over a rectangle that lies inside the surface.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="left">The left edge of the box.</param>
    /// <param name="top">The top edge of the box.</param>
    /// <param name="width">The width of the box.</param>
    /// <param name="height">The height of the box.</param>
    /// <param name="backing">The backing color.</param>
    private static void FillBox(Surface surface, int left, int top, int width, int height, Rgba32 backing)
    {
        for (var y = top; y < top + height; y++)
        {
            var row = surface.GetRowSpan(y);
            for (var x = left; x < left + width; x++)
            {
                Blend(ref row[x], backing, BackingAlpha);
            }
        }
    }
}
