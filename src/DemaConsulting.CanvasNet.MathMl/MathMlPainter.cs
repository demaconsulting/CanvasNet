using System.Globalization;
using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.MathMl.Layout;
using DemaConsulting.CanvasNet.MathMl.Syntax;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.MathMl;

/// <summary>
///     Where a laid-out equation is painted on a surface: the scale from CSS pixels to device pixels and the device
///     position of the origin of the <c>math</c> box (its left edge on its baseline).
/// </summary>
/// <param name="Scale">The device pixels per CSS pixel; greater than zero.</param>
/// <param name="OriginX">The device x of the origin.</param>
/// <param name="OriginY">The device y of the origin (the baseline).</param>
internal readonly record struct MathPlacement(double Scale, double OriginX, double OriginY);

/// <summary>
///     Paints a laid-out equation (a <see cref="MathBox"/> tree) onto a surface: backgrounds, glyph outlines, rules and
///     borders, antialiased and deterministic.
/// </summary>
/// <remarks>
///     <para>
///         <b>Order.</b> The items of the whole tree are painted in four passes, each in tree order: the backgrounds
///         (<c>mathbackground</c>, the light yellow of <c>merror</c>), the glyphs, the rules, and the borders (the red
///         border of <c>merror</c>), so a background never hides a glyph of another element. A phantom subtree
///         (<c>mphantom</c>) keeps its space but paints nothing.
///     </para>
///     <para>
///         <b>Glyphs.</b> A glyph is its outline from the font (design units, y up), scaled to the font size and the
///         placement, flipped, moved to its pen position and filled with the nonzero rule. Only the bundled fonts are used,
///         so the pixels are the same on every machine.
///     </para>
///     <para>
///         <b>Bounds.</b> The glyphs are counted before anything is painted, and more than
///         <see cref="MathMlLimits.MaxPaintedGlyphs"/> fails with a located <see cref="MathMlRenderException"/>
///         (<see cref="MathMlDiagnosticCodes.LimitExceeded"/>); the cancellation token is checked every
///         <see cref="CancellationInterval"/> items. The class is stateless and safe for concurrent use with different
///         surfaces.
///     </para>
/// </remarks>
internal static class MathMlPainter
{
    /// <summary>The number of items painted between two checks of the cancellation token.</summary>
    public const int CancellationInterval = 64;

    /// <summary>
    ///     Paints an equation onto a surface.
    /// </summary>
    /// <param name="surface">The surface; its pixels are overwritten where the equation paints.</param>
    /// <param name="root">The box of the <c>math</c> element.</param>
    /// <param name="placement">Where the equation is painted.</param>
    /// <param name="background">The color the whole surface is filled with first, or null to keep its pixels.</param>
    /// <param name="maxPaintedGlyphs">The largest number of glyphs that may be painted.</param>
    /// <param name="cancellationToken">The cancellation token of the call.</param>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled.</exception>
    /// <exception cref="MathMlRenderException">Thrown, before anything is painted, when there are more glyphs than allowed.</exception>
    public static void Paint(Surface surface, MathBox root, MathPlacement placement, Rgba32? background, int maxPaintedGlyphs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(root);
        cancellationToken.ThrowIfCancellationRequested();

        // Collect the items of the visible boxes in tree order, with the position of their box and their element
        var items = Collect(root);
        var glyphs = 0;
        foreach (var entry in items.Where(entry => entry.Item is GlyphItem))
        {
            glyphs++;
            if (glyphs > maxPaintedGlyphs)
            {
                throw new MathMlRenderException(
                    MathMlDiagnosticCodes.LimitExceeded,
                    string.Create(CultureInfo.InvariantCulture, $"the equation paints more than {maxPaintedGlyphs} glyphs (MathMlLimits.MaxPaintedGlyphs)"),
                    entry.Location,
                    "simplify the equation, or raise the limit in MathMlRenderOptions.Limits");
            }
        }

        if (background is { } fill)
        {
            surface.Clear(fill);
        }

        // Paint in passes: backgrounds, glyphs, rules, borders
        var painted = 0;
        foreach (var pass in (Type[])[typeof(BackgroundItem), typeof(GlyphItem), typeof(RuleItem), typeof(BorderItem)])
        {
            foreach (var (item, x, y, _) in items.Where(entry => entry.Item.GetType() == pass))
            {
                if (++painted % CancellationInterval == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                PaintItem(surface, item, x, y, placement);
            }
        }
    }

    /// <summary>
    ///     Collects the paint items of the visible boxes of a tree in tree order (iteratively, so the depth of the tree
    ///     never threatens the stack).
    /// </summary>
    /// <param name="root">The root box.</param>
    /// <returns>The items with the position of their box origin and the location of their nearest element.</returns>
    private static List<(IPaintItem Item, double X, double Y, MathMlLocation Location)> Collect(MathBox root)
    {
        var result = new List<(IPaintItem Item, double X, double Y, MathMlLocation Location)>();
        var stack = new Stack<(MathBox Box, double X, double Y, MathMlLocation Location)>();
        stack.Push((root, 0, 0, root.Element?.Location ?? default));
        while (stack.Count > 0)
        {
            var (box, x, y, inherited) = stack.Pop();
            if (box.IsPhantom)
            {
                continue;
            }

            var location = box.Element?.Location ?? inherited;
            foreach (var item in box.Items)
            {
                result.Add((item, x, y, location));
            }

            // Reverse order on the stack keeps the children in tree order
            for (var i = box.Children.Count - 1; i >= 0; i--)
            {
                var child = box.Children[i];
                stack.Push((child.Box, x + child.X, y + child.Y, location));
            }
        }

        return result;
    }

    /// <summary>
    ///     Paints one item.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="item">The item.</param>
    /// <param name="x">The x of the origin of its box in CSS pixels.</param>
    /// <param name="y">The y of the origin of its box in CSS pixels.</param>
    /// <param name="placement">The placement.</param>
    private static void PaintItem(Surface surface, IPaintItem item, double x, double y, MathPlacement placement)
    {
        switch (item)
        {
            case GlyphItem glyph:
                PaintGlyph(surface, glyph, x, y, placement);
                break;
            case BackgroundItem backgroundItem:
                FillRect(surface, backgroundItem.Rect.Offset(x, y), backgroundItem.Color, placement);
                break;
            case RuleItem rule:
                FillRect(surface, rule.Rect.Offset(x, y), rule.Color, placement);
                break;
            case BorderItem border:
                PaintBorder(surface, border, x, y, placement);
                break;
        }
    }

    /// <summary>
    ///     Paints a glyph outline.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="glyph">The glyph item.</param>
    /// <param name="x">The x of the origin of its box.</param>
    /// <param name="y">The y of the origin of its box.</param>
    /// <param name="placement">The placement.</param>
    private static void PaintGlyph(Surface surface, GlyphItem glyph, double x, double y, MathPlacement placement)
    {
        var outline = glyph.Font.GetGlyphOutline(glyph.Glyph);
        if (outline.Subpaths.Count == 0)
        {
            return;
        }

        // Design units (y up) to device pixels (y down): scale, flip, and move to the pen on the baseline
        var scale = (float)(glyph.Size / glyph.Font.UnitsPerEm * placement.Scale);
        var penX = (float)(placement.OriginX + ((x + glyph.X) * placement.Scale));
        var penY = (float)(placement.OriginY + ((y + glyph.Baseline) * placement.Scale));
        var transform = new Matrix3x2(scale, 0, 0, -scale, penX, penY);
        PathFiller.Fill(surface, outline.Transform(transform), glyph.Color, FillRule.NonZero);
    }

    /// <summary>
    ///     Paints the four sides of a border.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="border">The border item.</param>
    /// <param name="x">The x of the origin of its box.</param>
    /// <param name="y">The y of the origin of its box.</param>
    /// <param name="placement">The placement.</param>
    private static void PaintBorder(Surface surface, BorderItem border, double x, double y, MathPlacement placement)
    {
        var rect = border.Rect.Offset(x, y);
        var width = Math.Min(border.Width, Math.Min(rect.Width, rect.Height) / 2);
        var inner = Math.Max(0, rect.Height - (2 * width));
        FillRect(surface, rect with { Height = width }, border.Color, placement);
        FillRect(surface, rect with { Y = rect.Bottom - width, Height = width }, border.Color, placement);
        FillRect(surface, new MathRect(rect.X, rect.Y + width, width, inner), border.Color, placement);
        FillRect(surface, new MathRect(rect.Right - width, rect.Y + width, width, inner), border.Color, placement);
    }

    /// <summary>
    ///     Fills a rectangle of the layout.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="rect">The rectangle in CSS pixels relative to the math origin.</param>
    /// <param name="color">The color.</param>
    /// <param name="placement">The placement.</param>
    private static void FillRect(Surface surface, MathRect rect, Rgba32 color, MathPlacement placement)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        var path = Path.Rectangle(
            (float)(placement.OriginX + (rect.X * placement.Scale)),
            (float)(placement.OriginY + (rect.Y * placement.Scale)),
            (float)(rect.Width * placement.Scale),
            (float)(rect.Height * placement.Scale));
        PathFiller.Fill(surface, path, color, FillRule.NonZero);
    }
}
