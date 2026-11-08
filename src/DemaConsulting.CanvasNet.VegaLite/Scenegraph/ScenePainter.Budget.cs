using System.Numerics;
using DemaConsulting.CanvasNet.Geometry;
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Guides;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.VegaLite.Scenegraph;

/// <summary>
///     The paint budget of a painting (a part of <see cref="ScenePainter"/>): the count of device pixels that the painter fills, strokes and draws, against
///     <see cref="VegaLiteLimits.MaxPaintedPixels"/>.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why.</b> <see cref="VegaLiteLimits.MaxSceneItems"/> counts items, not the area that they cover: a specification of a few thousand rectangles that each cover the whole
///         surface, symbols of a million square pixels, strokes thousands of pixels wide or pictures without a size are cheap to build and expensive to paint. The cost of painting an item is
///         proportional to the pixels of its box, so the painter charges the box of each item to one budget <em>before</em> it does the work of the item, and a painting that would
///         exceed the budget fails with a <see cref="VegaLiteRenderException"/> that names <c>VegaLiteLimits.MaxPaintedPixels</c> instead of running for minutes.
///     </para>
///     <para>
///         <b>What is charged.</b> For a fill, the bounding box of the path in surface pixels cut to the surface (or, inside a clipped group, to the clip layer, which is the surface
///         that the children are drawn on); for a stroke, the same box grown by half the stroke width on each side; for text, the estimated box of its lines; for a picture, its box in the
///         surface; for the layer of a clipped group and the layers of a translucent trail, the pixels of the layer. A fill and a stroke of one item are two charges (two passes over its
///         pixels). The box of a path is taken from its control points (a curve lies inside the hull of its control points, and an arc is converted to the curves of its ellipse first, so a circle near the edge is charged for the part of it that is on the surface), so the charge is an upper bound of the pixels that the filler
///         visits, and an item that lies outside the surface is charged nothing. A box that cannot be computed (a coordinate that is NaN or overflows) is charged as the whole target.
///     </para>
///     <para>
///         <b>Location.</b> The failure is located at the <see cref="SceneItem.Source"/> of the item that did not fit, or at that of the nearest enclosing group that has one (the marks group
///         of a mark has the location of the mark), so the author sees which mark is too expensive.
///     </para>
/// </remarks>
internal sealed partial class ScenePainter
{
    /// <summary>The estimated width of a character of text as a fraction of the font size, for the charge of a text item.</summary>
    private const double TextCharacterWidthFactor = 0.6;

    /// <summary>
    ///     Creates the meter of one painting.
    /// </summary>
    /// <returns>The meter, or null when the painter has no limit (nothing is then counted).</returns>
    private PaintMeter? CreateMeter() => _maxPaintedPixels == long.MaxValue ? null : new PaintMeter(_maxPaintedPixels);

    /// <summary>
    ///     Charges the pixels of a path box to the paint budget.
    /// </summary>
    /// <param name="context">The context of the painting.</param>
    /// <param name="path">The path in the item's own units.</param>
    /// <param name="transform">The matrix from the path's units to surface pixels.</param>
    /// <param name="grow">The distance in surface pixels that the box grows on each side (half the width of a stroke).</param>
    /// <param name="item">The item that is painted, which names the item in the failure and gives its location.</param>
    /// <exception cref="VegaLiteRenderException">Thrown when the charge exceeds the limit.</exception>
    private static void ChargePath(Context context, Path path, Matrix3x2 transform, double grow, SceneItem item)
    {
        if (context.Meter is null)
        {
            return;
        }

        // The control box of the path in the units of the item. A curve lies inside the hull of its control points, and an arc (a circle is emitted as arcs) is converted to the
        // cubic curves of its ellipse first (the radii corrected as SVG does), so the box holds the whole arc and not only its end point: Path.GetBounds does both
        var bounds = path.GetBounds();
        if (bounds.IsEmpty)
        {
            return;
        }

        double minX = bounds.Left, minY = bounds.Top, maxX = bounds.Right, maxY = bounds.Bottom;

        // The four corners of the box are mapped to surface pixels (the matrix can rotate a symbol); the pixel box is the box of the corners
        var (x1, y1) = Map(transform, minX, minY);
        var (x2, y2) = Map(transform, maxX, minY);
        var (x3, y3) = Map(transform, maxX, maxY);
        var (x4, y4) = Map(transform, minX, maxY);
        var left = Math.Min(Math.Min(x1, x2), Math.Min(x3, x4)) - grow;
        var top = Math.Min(Math.Min(y1, y2), Math.Min(y3, y4)) - grow;
        var right = Math.Max(Math.Max(x1, x2), Math.Max(x3, x4)) + grow;
        var bottom = Math.Max(Math.Max(y1, y2), Math.Max(y3, y4)) + grow;
        ChargeBox(context, left, top, right, bottom, item.Role, item.Source);
    }

    /// <summary>
    ///     Maps a point through a matrix in double precision.
    /// </summary>
    /// <param name="matrix">The matrix.</param>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <returns>The mapped point.</returns>
    private static (double X, double Y) Map(Matrix3x2 matrix, double x, double y) =>
        ((matrix.M11 * x) + (matrix.M21 * y) + matrix.M31, (matrix.M12 * x) + (matrix.M22 * y) + matrix.M32);

    /// <summary>
    ///     Charges the estimated box of a text item to the paint budget.
    /// </summary>
    /// <remarks>
    ///     The box is estimated from the text, not measured (measuring is the work of painting): the longest line is its characters times 0.6 of the font size (at most the limit of the
    ///     item when it has one) wide, and each line is the line height tall (the absolute value of it, and at least the font size: a line paints a row of glyphs even when the line height is zero or negative). The estimate is cut to the size of the target, which is also what the glyphs are cut to.
    /// </remarks>
    /// <param name="context">The context of the painting.</param>
    /// <param name="text">The text item.</param>
    /// <exception cref="VegaLiteRenderException">Thrown when the charge exceeds the limit.</exception>
    private static void ChargeText(Context context, SceneText text)
    {
        if (context.Meter is null)
        {
            return;
        }

        var size = text.FontSize * context.Scale;
        var longest = 0;
        foreach (var line in text.Lines)
        {
            longest = Math.Max(longest, line.Length);
        }

        var width = longest * size * TextCharacterWidthFactor;
        if (text.Limit > 0)
        {
            width = Math.Min(width, text.Limit * context.Scale);
        }

        // Every line paints its own row of glyphs, so a line is at least a font size tall, whatever the line height: Vega steps the baselines by a negative line height upward and by a
        // zero one not at all (the lines then overlap), and a negative or small height must not make the charge negative or smaller than the painting
        var step = Math.Max(Math.Abs(TextMeasurer.LineHeight(text.FontSize, text.LineHeight)) * context.Scale, size);
        var height = text.Lines.Count * step;
        var area = Math.Min(width, context.Target.Width) * Math.Min(height, context.Target.Height);
        ChargePixels(context, area, text.Role, text.Source);
    }

    /// <summary>
    ///     Charges the part of a box that lies on the target to the paint budget.
    /// </summary>
    /// <param name="context">The context of the painting.</param>
    /// <param name="left">The left edge in surface pixels.</param>
    /// <param name="top">The top edge in surface pixels.</param>
    /// <param name="right">The right edge in surface pixels.</param>
    /// <param name="bottom">The bottom edge in surface pixels.</param>
    /// <param name="role">The role of the item, for the failure.</param>
    /// <param name="source">The location of the item, or unknown.</param>
    /// <exception cref="VegaLiteRenderException">Thrown when the charge exceeds the limit.</exception>
    private static void ChargeBox(Context context, double left, double top, double right, double bottom, string role, VegaLiteLocation source)
    {
        if (context.Meter is null)
        {
            return;
        }

        // A box that is not a number (NaN, or an infinity minus itself) cannot be cut: the whole target is charged, the most it can cost
        double area;
        if (double.IsNaN(left) || double.IsNaN(top) || double.IsNaN(right) || double.IsNaN(bottom))
        {
            area = (double)context.Target.Width * context.Target.Height;
        }
        else
        {
            // The pixels that a shape touches are the whole pixels of its box: the box is widened to whole pixels (a sliver of a pixel still paints that pixel)
            var width = Math.Min(Math.Ceiling(right), context.Target.Width) - Math.Max(Math.Floor(left), 0);
            var height = Math.Min(Math.Ceiling(bottom), context.Target.Height) - Math.Max(Math.Floor(top), 0);
            area = width > 0 && height > 0 ? width * height : 0;
        }

        ChargePixels(context, area, role, source);
    }

    /// <summary>
    ///     Charges a number of device pixels to the paint budget before the work that they stand for is done.
    /// </summary>
    /// <param name="context">The context of the painting; nothing is charged when it has no meter.</param>
    /// <param name="pixels">The pixels; a fraction is rounded up and a value that is not a number counts as the most there can be.</param>
    /// <param name="role">The role of the item, for the failure.</param>
    /// <param name="source">The location of the item; unknown to use the location of the enclosing group.</param>
    /// <exception cref="VegaLiteRenderException">Thrown when the charge exceeds the limit.</exception>
    private static void ChargePixels(Context context, double pixels, string role, VegaLiteLocation source = default) =>
        context.Meter?.Charge(pixels, role, source.IsKnown ? source : context.Location);

    /// <summary>
    ///     Counts the painted pixels of one painting against the limit.
    /// </summary>
    private sealed class PaintMeter
    {
        /// <summary>The limit.</summary>
        private readonly long _limit;

        /// <summary>The pixels charged so far; never more than the limit.</summary>
        private long _charged;

        /// <summary>
        ///     Initializes a meter.
        /// </summary>
        /// <param name="limit">The most pixels that may be charged.</param>
        public PaintMeter(long limit)
        {
            _limit = limit;
        }

        /// <summary>
        ///     Charges pixels, failing before the total would pass the limit.
        /// </summary>
        /// <param name="pixels">The pixels; rounded up, and NaN counts as the largest count.</param>
        /// <param name="role">The role of the item that is about to be painted.</param>
        /// <param name="location">The location that the failure is reported at.</param>
        /// <exception cref="VegaLiteRenderException">Thrown when the charge would exceed the limit.</exception>
        public void Charge(double pixels, string role, VegaLiteLocation location)
        {
            var cost = double.IsNaN(pixels) ? long.MaxValue : NumericConversions.ToInt64Saturating(Math.Ceiling(pixels));
            if (cost <= 0)
            {
                return;
            }

            if (cost > _limit - _charged)
            {
                throw new VegaLiteRenderException(
                    string.Create(
                        System.Globalization.CultureInfo.InvariantCulture,
                        $"The chart paints more than the limit of {_limit} pixels (VegaLiteLimits.MaxPaintedPixels): the '{role}' item that would paint {(cost == long.MaxValue ? "an enormous number of" : cost.ToString(System.Globalization.CultureInfo.InvariantCulture))} more pixels after {_charged} was not drawn; reduce the sizes, the stroke widths or the number of the marks, or the picture sizes."),
                    location);
            }

            _charged += cost;
        }
    }
}
