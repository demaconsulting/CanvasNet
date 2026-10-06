using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Geometry;
using DemaConsulting.CanvasNet.Rendering;
using RenderCanvas = DemaConsulting.CanvasNet.Rendering.Canvas;

namespace DemaConsulting.CanvasNet.Charts;

public static partial class ChartRenderer
{
    /// <summary>
    ///     Paints <c>chart.Title</c>'s text, horizontally and vertically centered within
    ///     <paramref name="titleRect"/>.
    /// </summary>
    /// <param name="canvas">The target canvas.</param>
    /// <param name="chart">
    ///     The chart being rendered. <see cref="Chart.Title"/> must not be <see langword="null"/>
    ///     (callers only invoke this once <see cref="ChartLayout.HasTitle"/> is <see langword="true"/>).
    /// </param>
    /// <param name="titleRect">The title band's rectangle, as computed by <see cref="ComputeLayout"/>.</param>
    /// <param name="options">The effective render options.</param>
    /// <param name="font">The resolved text font.</param>
    private static void PaintTitle(RenderCanvas canvas, Chart chart, Rect titleRect, ChartRenderOptions options, TrueTypeFont font)
    {
        if (titleRect.Width <= 0f || titleRect.Height <= 0f)
        {
            return;
        }

        var title = chart.Title!;
        var fontSize = title.FontSize ?? options.TitleFontSize;
        var x = titleRect.X + titleRect.Width / 2f;
        var y = titleRect.Y + titleRect.Height / 2f + fontSize * 0.35f;
        canvas.DrawText(title.Text, x, y, TextAlign.Center, font, fontSize, TitleTextColor);
    }

    /// <summary>The color used to paint a chart's title text.</summary>
    private static readonly Rgba32 TitleTextColor = new(32, 32, 32, 255);
}
