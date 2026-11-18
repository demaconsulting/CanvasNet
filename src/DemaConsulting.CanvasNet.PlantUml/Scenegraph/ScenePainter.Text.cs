using System.Numerics;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Geometry;
using DemaConsulting.CanvasNet.Rendering;

namespace DemaConsulting.CanvasNet.PlantUml.Scenegraph;

/// <content>
///     Text: the runs of a line are drawn one after the other with their resolved faces, an underlined or struck run with
///     a bar.
/// </content>
internal sealed partial class ScenePainter
{
    /// <summary>The position of the underline bar below the baseline, as a fraction of the font size.</summary>
    private const double UnderlineOffset = 0.1;

    /// <summary>The position of the strike bar above the baseline, as a fraction of the font size.</summary>
    private const double StrikeOffset = 0.3;

    /// <summary>The thickness of an underline or strike bar, as a fraction of the font size (at least one pixel).</summary>
    private const double BarThickness = 0.07;

    /// <summary>
    ///     Draws a line of text.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="item">The text item.</param>
    /// <param name="placement">The placement.</param>
    private void PaintText(Canvas.Surface surface, SceneText item, ScenePlacement placement)
    {
        var x = (item.X * placement.Scale) + placement.OffsetX;
        var baseline = (item.Baseline * placement.Scale) + placement.OffsetY;
        var box = item.Box;
        var (left, top) = ((box.X * placement.Scale) + placement.OffsetX, (box.Y * placement.Scale) + placement.OffsetY);
        var (right, bottom) = ((box.Right * placement.Scale) + placement.OffsetX, (box.Bottom * placement.Scale) + placement.OffsetY);
        if (item.Runs.Count == 0 || !IsBounded(x, baseline, left, top, right, bottom))
        {
            return;
        }

        // The line box is charged once, then each run is drawn after the previous one at its measured advance
        Charge(surface, left, top, right, bottom, item);
        foreach (var run in item.Runs)
        {
            var size = run.Font.Size * placement.Scale;
            var advance = run.Width * placement.Scale;
            if (run.Text.Length > 0 && (float)size > 0 && IsBounded(size, advance) && run.Color.A > 0)
            {
                var face = _text.Resolve(run.Font);
                var path = TextRenderer.CreateTextPath(run.Text, face.Font, (float)size, (float)x, (float)baseline);
                PathFiller.Fill(surface, path, run.Color, FillRule.NonZero, FlattenTolerance);
                if (run.Underline)
                {
                    PathFiller.Fill(surface, Bar(x, baseline + (UnderlineOffset * size), advance, size), run.Color, FillRule.NonZero, FlattenTolerance);
                }

                if (run.Strike)
                {
                    PathFiller.Fill(surface, Bar(x, baseline - (StrikeOffset * size), advance, size), run.Color, FillRule.NonZero, FlattenTolerance);
                }
            }

            x += advance;
        }
    }

    /// <summary>
    ///     Builds the bar of an underlined or struck run.
    /// </summary>
    /// <param name="x">The device x of the run's start.</param>
    /// <param name="y">The device y of the bar's center.</param>
    /// <param name="width">The advance of the run in device pixels.</param>
    /// <param name="size">The font size in device pixels.</param>
    /// <returns>The bar's path in device space.</returns>
    private static Geometry.Path Bar(double x, double y, double width, double size)
    {
        var half = Math.Max(0.5, BarThickness * size) / 2;
        return new PathBuilder()
            .MoveTo(new Vector2((float)x, (float)(y - half)))
            .LineTo(new Vector2((float)(x + width), (float)(y - half)))
            .LineTo(new Vector2((float)(x + width), (float)(y + half)))
            .LineTo(new Vector2((float)x, (float)(y + half)))
            .Close()
            .Build();
    }
}
