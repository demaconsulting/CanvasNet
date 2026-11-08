using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Mermaid.Scenegraph;

/// <content>
///     Pictures: loaded at the device size of their box and composited with their opacity.
/// </content>
internal sealed partial class ScenePainter
{
    /// <summary>
    ///     Draws a picture into its box.
    /// </summary>
    /// <remarks>
    ///     The box is snapped to whole device pixels (left and top rounded down, right and bottom rounded up), the loader
    ///     is asked for a picture of exactly that size, and the picture is composited over the surface with the opacity of
    ///     the item. A painter without a loader, a box without area, or a loader that returns nothing draws nothing.
    /// </remarks>
    /// <param name="surface">The surface.</param>
    /// <param name="item">The picture item.</param>
    /// <param name="placement">The placement.</param>
    private void PaintImage(Surface surface, SceneImage item, ScenePlacement placement)
    {
        if (_images is null || !(item.Opacity > 0))
        {
            return;
        }

        // The device box, snapped outward to whole pixels
        var left = (item.Box.X * placement.Scale) + placement.OffsetX;
        var top = (item.Box.Y * placement.Scale) + placement.OffsetY;
        var right = left + (item.Box.Width * placement.Scale);
        var bottom = top + (item.Box.Height * placement.Scale);
        if (!IsBounded(left, top, right, bottom))
        {
            return;
        }

        var x0 = (int)Math.Floor(left);
        var y0 = (int)Math.Floor(top);
        var width = (int)Math.Ceiling(right) - x0;
        var height = (int)Math.Ceiling(bottom) - y0;
        if (width < 1 || height < 1 || width > Surface.MaxDimension || height > Surface.MaxDimension)
        {
            return;
        }

        Charge(surface, x0, y0, x0 + width, y0 + height, item);
        using var picture = _images.Load(item.Href, width, height);
        if (picture is null)
        {
            return;
        }

        Composite(surface, picture, x0, y0, Math.Min(item.Opacity, 1));
    }

    /// <summary>
    ///     Composites a picture over the surface at a pixel position, clipped to the surface.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="picture">The picture.</param>
    /// <param name="x0">The left of the picture on the surface.</param>
    /// <param name="y0">The top of the picture on the surface.</param>
    /// <param name="opacity">The opacity from 0 to 1.</param>
    private static void Composite(Surface surface, Surface picture, int x0, int y0, double opacity)
    {
        var left = Math.Max(0, x0);
        var right = Math.Min(surface.Width, x0 + picture.Width);
        if (right <= left)
        {
            return;
        }

        var coverage = new float[right - left];
        Array.Fill(coverage, (float)opacity);
        for (var y = Math.Max(0, y0); y < Math.Min(surface.Height, y0 + picture.Height); y++)
        {
            var row = picture.GetRowSpan(y - y0).Slice(left - x0, right - left);
            surface.CompositeOverSpan(y, left, coverage, row);
        }
    }
}
