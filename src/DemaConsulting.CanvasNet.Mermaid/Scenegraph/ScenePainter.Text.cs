using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Rendering;

namespace DemaConsulting.CanvasNet.Mermaid.Scenegraph;

/// <content>
///     Text: the runs of a line are drawn one after the other with their resolved faces.
/// </content>
internal sealed partial class ScenePainter
{
    /// <summary>
    ///     Draws a line of text.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="item">The text item.</param>
    /// <param name="placement">The placement.</param>
    private void PaintText(Canvas.Surface surface, SceneText item, ScenePlacement placement)
    {
        // Resolve and measure every run at its device size; a run that cannot be drawn ends the line
        var runs = new List<(SceneTextRun Run, TrueTypeFont Font, float Size, float Width)>();
        var total = 0f;
        float ascent = 0f, descent = 0f;
        foreach (var run in item.Runs)
        {
            var size = run.Font.Size * placement.Scale;
            if (run.Text.Length == 0 || !((float)size > 0) || !IsBounded(size))
            {
                continue;
            }

            var font = _fonts.Resolve(run.Font.Family, run.Font.Bold, run.Font.Italic, out _).Font;
            var metrics = TextRenderer.MeasureText(run.Text, font, (float)size);
            runs.Add((run, font, (float)size, metrics.Width));
            total += metrics.Width;
            ascent = Math.Max(ascent, metrics.Ascent);
            descent = Math.Max(descent, metrics.Descent);
        }

        // The anchor decides where the line starts
        var anchorX = (item.X * placement.Scale) + placement.OffsetX;
        var baseline = (item.Baseline * placement.Scale) + placement.OffsetY;
        var x = item.Anchor switch
        {
            SceneTextAnchor.Middle => anchorX - (total / 2),
            SceneTextAnchor.End => anchorX - total,
            _ => anchorX,
        };

        if (runs.Count == 0 || !IsBounded(x, baseline, x + total))
        {
            return;
        }

        // The line box is charged once, then each run is drawn after the previous one, its outline stroked over its fill;
        // a stroke width that rounds to zero in single precision (a tiny fit scale) draws no stroke
        var strokeWidth = DeviceStrokeWidth(item.Stroke, placement.Scale);
        var stroke = strokeWidth > 0 ? item.Stroke : null;
        var half = stroke is null ? 0 : strokeWidth / 2.0;
        Charge(surface, x - half, baseline - ascent - half, x + total + half, baseline + descent + half, item);
        foreach (var (run, font, size, width) in runs)
        {
            var path = TextRenderer.CreateTextPath(run.Text, font, size, (float)x, (float)baseline);
            if (run.Color.A > 0)
            {
                PathFiller.Fill(surface, path, run.Color, FillRule.NonZero, FlattenTolerance);
            }

            if (stroke is not null)
            {
                var style = new StrokeStyle(strokeWidth, stroke.Cap, stroke.Join, 4f, null, 0f);
                PathFiller.Fill(surface, PathStroker.Stroke(path, style, FlattenTolerance), stroke.Color, FillRule.NonZero, FlattenTolerance);
            }

            x += width;
        }
    }
}
