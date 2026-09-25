using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;
using Rgba32 = DemaConsulting.CanvasNet.Canvas.Rgba32;

namespace DemaConsulting.CanvasNet.Rendering;

/// <summary>
///     Convenience shape-drawing extension methods on <see cref="Canvas"/>, each building a
///     <see cref="Path"/> via the corresponding <see cref="Path.Rectangle"/>/
///     <see cref="Path.RoundRectangle"/>/<see cref="Path.Circle"/> factory and forwarding to
///     <see cref="Canvas.FillPath(Path, Rgba32, FillRule)"/> or
///     <see cref="Canvas.StrokePath(Path, StrokeStyle, Rgba32)"/>.
/// </summary>
public static class Shapes
{
    /// <summary>Fills an axis-aligned rectangle on <paramref name="canvas"/>.</summary>
    public static void FillRect(this Canvas canvas, float x, float y, float width, float height, Rgba32 color)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        canvas.FillPath(Path.Rectangle(x, y, width, height), color);
    }

    /// <summary>Strokes an axis-aligned rectangle on <paramref name="canvas"/>.</summary>
    public static void StrokeRect(this Canvas canvas, float x, float y, float width, float height, StrokeStyle style, Rgba32 color)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        canvas.StrokePath(Path.Rectangle(x, y, width, height), style, color);
    }

    /// <summary>Fills a rounded rectangle on <paramref name="canvas"/>.</summary>
    public static void FillRoundRect(this Canvas canvas, float x, float y, float width, float height, float radius, Rgba32 color)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        canvas.FillPath(Path.RoundRectangle(x, y, width, height, radius), color);
    }

    /// <summary>Strokes a rounded rectangle on <paramref name="canvas"/>.</summary>
    public static void StrokeRoundRect(this Canvas canvas, float x, float y, float width, float height, float radius, StrokeStyle style, Rgba32 color)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        canvas.StrokePath(Path.RoundRectangle(x, y, width, height, radius), style, color);
    }

    /// <summary>Fills a circle on <paramref name="canvas"/>.</summary>
    public static void FillCircle(this Canvas canvas, float centerX, float centerY, float radius, Rgba32 color)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        canvas.FillPath(Path.Circle(centerX, centerY, radius), color);
    }

    /// <summary>Strokes a circle on <paramref name="canvas"/>.</summary>
    public static void StrokeCircle(this Canvas canvas, float centerX, float centerY, float radius, StrokeStyle style, Rgba32 color)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        canvas.StrokePath(Path.Circle(centerX, centerY, radius), style, color);
    }
}
