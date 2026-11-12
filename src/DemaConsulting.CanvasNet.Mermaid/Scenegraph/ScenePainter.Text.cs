using System.Numerics;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Geometry;
using DemaConsulting.CanvasNet.Rendering;

namespace DemaConsulting.CanvasNet.Mermaid.Scenegraph;

/// <content>
///     Text: the runs of a line are drawn one after the other with their resolved faces, an underlined run with a bar
///     under it; a rotated line is drawn as without its rotation, then turned about its pivot.
/// </content>
internal sealed partial class ScenePainter
{
    /// <summary>
    ///     The underline position and thickness of the bundled faces in font units, from their <c>post</c> tables (the
    ///     position is the top of the bar, negative below the baseline), by family and weight. The core font reader does
    ///     not expose the <c>post</c> table, so the values of the bundled files are held here.
    /// </summary>
    private static readonly Dictionary<(string Family, bool Bold), (double Position, double Thickness)> UnderlineMetrics = new()
    {
        [("Liberation Sans", false)] = (-67, 150),
        [("Liberation Sans", true)] = (-2, 215),
        [("Liberation Serif", false)] = (-123, 100),
        [("Liberation Serif", true)] = (-28, 195),
        [("Liberation Mono", false)] = (-393, 84),
        [("Liberation Mono", true)] = (-272, 205),
    };

    /// <summary>
    ///     Draws a line of text.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="item">The text item.</param>
    /// <param name="placement">The placement.</param>
    private void PaintText(Canvas.Surface surface, SceneText item, ScenePlacement placement)
    {
        // Resolve and measure every run at its device size; a run that cannot be drawn ends the line
        var runs = new List<(SceneTextRun Run, ResolvedFont Face, float Size, float Width)>();
        var total = 0f;
        float ascent = 0f, descent = 0f;
        foreach (var run in item.Runs)
        {
            var size = run.Font.Size * placement.Scale;
            if (run.Text.Length == 0 || !((float)size > 0) || !IsBounded(size))
            {
                continue;
            }

            var face = _fonts.Resolve(run.Font.Family, run.Font.Bold, run.Font.Italic, out _);
            var metrics = TextRenderer.MeasureText(run.Text, face.Font, (float)size);
            runs.Add((run, face, (float)size, metrics.Width));
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

        // A rotated line turns about its device pivot; a pivot or angle that cannot be drawn ends the line
        Matrix3x2? rotation = null;
        if (item.Rotation is { } turn)
        {
            var pivotX = (turn.CenterX * placement.Scale) + placement.OffsetX;
            var pivotY = (turn.CenterY * placement.Scale) + placement.OffsetY;
            if (!IsBounded(pivotX, pivotY) || !double.IsFinite(turn.Angle))
            {
                return;
            }

            rotation = Matrix3x2.CreateRotation((float)(turn.Angle % 360 * Math.PI / 180), new Vector2((float)pivotX, (float)pivotY));
        }

        // The line box is charged once (the box of its turned corners for a rotated line), then each run is drawn after
        // the previous one, its outline stroked over its fill; a stroke width that rounds to zero in single precision (a
        // tiny fit scale) draws no stroke
        var strokeWidth = DeviceStrokeWidth(item.Stroke, placement.Scale);
        var stroke = strokeWidth > 0 ? item.Stroke : null;
        var half = stroke is null ? 0 : strokeWidth / 2.0;
        var (left, top, right, bottom) = (x - half, baseline - ascent - half, x + total + half, baseline + descent + half);
        if (rotation is { } matrix)
        {
            (left, top, right, bottom) = Turned(matrix, left, top, right, bottom);
            if (!IsBounded(left, top, right, bottom))
            {
                return;
            }
        }

        Charge(surface, left, top, right, bottom, item);
        foreach (var (run, face, size, width) in runs)
        {
            var path = Turn(TextRenderer.CreateTextPath(run.Text, face.Font, size, (float)x, (float)baseline), rotation);
            if (run.Color.A > 0)
            {
                PathFiller.Fill(surface, path, run.Color, FillRule.NonZero, FlattenTolerance);
                if (run.Underline)
                {
                    PathFiller.Fill(surface, Turn(UnderlineBar(face, size, x, baseline, width), rotation), run.Color, FillRule.NonZero, FlattenTolerance);
                }
            }

            if (stroke is not null)
            {
                var style = new StrokeStyle(strokeWidth, stroke.Cap, stroke.Join, 4f, null, 0f);
                PathFiller.Fill(surface, PathStroker.Stroke(path, style, FlattenTolerance), stroke.Color, FillRule.NonZero, FlattenTolerance);
            }

            x += width;
        }
    }

    /// <summary>
    ///     Turns a device path by a rotation, or keeps it when there is none.
    /// </summary>
    /// <param name="path">The path in device space.</param>
    /// <param name="rotation">The rotation about the device pivot, or null.</param>
    /// <returns>The path.</returns>
    private static Geometry.Path Turn(Geometry.Path path, Matrix3x2? rotation) =>
        rotation is { } matrix ? path.Transform(matrix) : path;

    /// <summary>
    ///     Gets the axis-aligned box of a device box turned by a rotation (the box of its four turned corners).
    /// </summary>
    /// <param name="matrix">The rotation.</param>
    /// <param name="left">The left of the box.</param>
    /// <param name="top">The top of the box.</param>
    /// <param name="right">The right of the box.</param>
    /// <param name="bottom">The bottom of the box.</param>
    /// <returns>The turned box.</returns>
    internal static (double Left, double Top, double Right, double Bottom) Turned(Matrix3x2 matrix, double left, double top, double right, double bottom)
    {
        var corners = new[]
        {
            Vector2.Transform(new Vector2((float)left, (float)top), matrix),
            Vector2.Transform(new Vector2((float)right, (float)top), matrix),
            Vector2.Transform(new Vector2((float)right, (float)bottom), matrix),
            Vector2.Transform(new Vector2((float)left, (float)bottom), matrix),
        };
        return (corners.Min(c => (double)c.X), corners.Min(c => (double)c.Y), corners.Max(c => (double)c.X), corners.Max(c => (double)c.Y));
    }

    /// <summary>
    ///     Builds the underline bar of a run (CSS's <c>text-decoration: underline</c>): under the whole advance of the run,
    ///     at the position and thickness of the face's <c>post</c> table (those of Liberation Sans for a face not in
    ///     <see cref="UnderlineMetrics"/>).
    /// </summary>
    /// <param name="face">The resolved face of the run.</param>
    /// <param name="size">The font size in device pixels.</param>
    /// <param name="x">The device x of the run's start.</param>
    /// <param name="baseline">The device y of the baseline.</param>
    /// <param name="width">The advance of the run in device pixels.</param>
    /// <returns>The bar's path in device space.</returns>
    internal static Geometry.Path UnderlineBar(ResolvedFont face, float size, double x, double baseline, float width)
    {
        ArgumentNullException.ThrowIfNull(face);
        var (position, thickness) = UnderlineMetrics.TryGetValue((face.Family, face.Bold), out var metrics)
            ? metrics
            : UnderlineMetrics[("Liberation Sans", false)];
        var scale = size / (double)face.Font.UnitsPerEm;
        var top = baseline - (position * scale);
        var bottom = top + (thickness * scale);
        return new PathBuilder()
            .MoveTo(new Vector2((float)x, (float)top))
            .LineTo(new Vector2((float)(x + width), (float)top))
            .LineTo(new Vector2((float)(x + width), (float)bottom))
            .LineTo(new Vector2((float)x, (float)bottom))
            .Close()
            .Build();
    }
}
