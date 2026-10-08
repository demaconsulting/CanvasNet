using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio

/// <summary>
///     Implements the <see cref="VsdxDocument"/> public, page-level rendering API (Milestone 7):
///     resolves the requested page's full shape tree (first call for that page; cached
///     thereafter - see <c>VsdxDocument.Shapes.cs</c>'s <c>GetPageShapes</c>), walks it in
///     document order (document order is z-order; the first declared shape paints first/bottom),
///     and wires the already-resolved geometry/fill/stroke (Milestone 3), text layout
///     (Milestone 4), and connector/arrowhead (Milestone 5/6) data into one cohesive pipeline -
///     see <c>vsdx-document.md</c>'s "Render" Key Methods entries for the authoritative contract
///     this type implements.
/// </summary>
public sealed partial class VsdxDocument
{
    /// <summary>
    ///     The minimum stroke width, in pixels, <see cref="PaintShapeGeometry"/> will ever pass to
    ///     <see cref="PaintStroke"/> - a hairline/"cosmetic pen" floor matching Visio's own
    ///     convention of always rendering a visually perceptible line regardless of a resolved
    ///     sub-pixel <c>LineWeight</c>. Confirmed necessary against <c>60973.vsdx</c>'s own
    ///     connector shapes: their Master's literal <c>LineWeight</c> resolves to
    ///     <c>~0.0033</c> inches, which at this milestone's 150 DPI smoke-test render is
    ///     <c>~0.5</c> physical pixels - sub-pixel enough that, with no floor, every connector
    ///     line vanished entirely (visually confirmed absent against the Visio-reference PNG)
    ///     despite its geometry, color, and <c>HasLine</c> all already resolving correctly.
    ///     Expressed in pixels (not inches) so it only ever engages when a line would otherwise
    ///     render below one physical pixel, never thickening an already-&gt;1px line - see this
    ///     milestone's own plan report, Risk #2.
    /// </summary>
    private const float MinStrokeWidthPixels = 1f;

    /// <summary>
    ///     Renders the specified page into a new <see cref="Surface"/> of the given pixel
    ///     dimensions, walking the page's full, lazily-resolved shape tree
    ///     (<c>GetPageShapes</c>) in document order and painting each shape's resolved fill,
    ///     stroke, text, and (for a 1-D connector) line and arrowheads.
    /// </summary>
    /// <param name="pageIndex">The zero-based index of the page to render, in <c>[0, PageCount)</c>.</param>
    /// <param name="width">The width of the rendered surface, in pixels.</param>
    /// <param name="height">The height of the rendered surface, in pixels.</param>
    /// <param name="options">
    ///     Optional page-rendering configuration. When <see langword="null"/> (the default),
    ///     <see cref="VsdxRenderOptions.Default"/> is used, which clears the surface to opaque
    ///     white before the page's shape tree is painted.
    /// </param>
    /// <returns>
    ///     A new <see cref="Surface"/> of the requested <paramref name="width"/> x
    ///     <paramref name="height"/>, cleared to <paramref name="options"/>'s
    ///     <see cref="VsdxRenderOptions.BackgroundColor"/> and painted with the page's resolved
    ///     shape tree.
    /// </returns>
    /// <exception cref="ObjectDisposedException">Thrown when this document has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="pageIndex"/> is outside <c>[0, PageCount)</c> (propagated,
    ///     unwrapped, from <see cref="GetPageSize(int)"/>), or when <paramref name="width"/>/
    ///     <paramref name="height"/> is outside <see cref="Surface"/>'s own valid dimension range
    ///     (propagated, unwrapped, from the <see cref="Surface(int, int)"/> constructor).
    /// </exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the page's shape tree cannot be resolved - propagated unchanged from
    ///     <see cref="GetPageShapes"/> (a malformed content part, or a group-nesting depth/
    ///     resolved-shape-count budget exceeded).
    /// </exception>
    /// <remarks>
    ///     A recognized-but-unsupported construct (an unrecognized arrowhead index, a dangling
    ///     glue target, a non-solid fill pattern) never throws - it degrades to a tolerant,
    ///     visually-reasonable default instead, per <c>canvas-net-vsdx.md</c>'s Design
    ///     Constraints; every such degradation already happens during shape-tree resolution (see
    ///     <c>VsdxDocument.Arrowheads.cs</c>/<c>VsdxDocument.Paint.cs</c>), so this method itself
    ///     never needs to special-case one.
    /// </remarks>
    public Surface Render(int pageIndex, int width, int height, VsdxRenderOptions? options = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // GetPageSize validates pageIndex (ArgumentOutOfRangeException) before any Surface is
        // allocated; GetPageShapes lazily resolves (and caches) the page's full shape tree -
        // together these satisfy CanvasNetVsdx-VsdxDocument-LazyPageResolution: a page's shape
        // tree is not resolved until Render is first called for that page.
        var pageSize = GetPageSize(pageIndex);
        var shapes = GetPageShapes(pageIndex);

        var surface = new Surface(width, height);
        try
        {
            var resolvedOptions = options ?? VsdxRenderOptions.Default;
            surface.Clear(resolvedOptions.BackgroundColor);

            var widthInches = pageSize.WidthEmu / EmuPerInch;
            var heightInches = pageSize.HeightEmu / EmuPerInch;
            var scaleX = (float)(width / widthInches);
            var scaleY = (float)(height / heightInches);

            // Page-space (origin bottom-left, Y-up, inches) -> pixel-space (origin top-left,
            // Y-down, pixels).
            var pageToPixel =
                Matrix3x2.CreateScale(scaleX, -scaleY) *
                Matrix3x2.CreateTranslation(0f, height);

            // Document order is z-order: the first declared top-level shape paints first/bottom.
            foreach (var shape in shapes)
            {
                RenderShapeRecursive(surface, shape, pageToPixel, pageToPixel);
            }

            return surface;
        }
        catch
        {
            // Never leak a partially-painted Surface on failure.
            surface.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Resolves the requested page's declared size, converts it to pixel dimensions at the
    ///     given <paramref name="dpi"/> (dots per inch), and delegates to the pixel-dimension
    ///     overload (<see cref="Render(int, int, int, VsdxRenderOptions?)"/>).
    /// </summary>
    /// <param name="pageIndex">The zero-based index of the page to render, in <c>[0, PageCount)</c>.</param>
    /// <param name="dpi">The rendering resolution, in dots (pixels) per inch. Must be greater than zero.</param>
    /// <param name="options">Optional page-rendering configuration - see <see cref="Render(int, int, int, VsdxRenderOptions?)"/>'s own remarks.</param>
    /// <returns>
    ///     A new <see cref="Surface"/> sized to the page's own declared width/height at
    ///     <paramref name="dpi"/>, painted exactly as <see cref="Render(int, int, int, VsdxRenderOptions?)"/>
    ///     describes.
    /// </returns>
    /// <exception cref="ObjectDisposedException">Thrown when this document has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="dpi"/> is not greater than zero, when <paramref name="pageIndex"/>
    ///     is outside <c>[0, PageCount)</c> (propagated, unwrapped, from <see cref="GetPageSize(int)"/>),
    ///     or when the computed pixel width/height falls outside <see cref="Surface"/>'s own valid
    ///     dimension range (cascaded from the pixel-dimension overload above).
    /// </exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the page's shape tree cannot be resolved - see
    ///     <see cref="Render(int, int, int, VsdxRenderOptions?)"/>'s own exception remarks.
    /// </exception>
    /// <remarks>
    ///     This overload deliberately accepts <paramref name="dpi"/> as an <see langword="int"/>,
    ///     not a <see langword="float"/> - a documented deviation from
    ///     <c>DemaConsulting.CanvasNet.Pptx.PptxDocument</c>'s own <c>float dpi</c> overload,
    ///     per <c>vsdx-document.md</c>'s own authoritative signature.
    /// </remarks>
    public Surface Render(int pageIndex, int dpi, VsdxRenderOptions? options = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (dpi <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dpi), dpi, "dpi must be greater than zero.");
        }

        var pageSize = GetPageSize(pageIndex);
        var widthInches = pageSize.WidthEmu / EmuPerInch;
        var heightInches = pageSize.HeightEmu / EmuPerInch;
        var width = (int)Math.Round(widthInches * dpi, MidpointRounding.AwayFromZero);
        var height = (int)Math.Round(heightInches * dpi, MidpointRounding.AwayFromZero);

        return Render(pageIndex, width, height, options);
    }

    /// <summary>
    ///     Recursively paints <paramref name="shape"/> - its resolved geometry fill/stroke, text,
    ///     and (for a 1-D connector) line/arrowheads - then recurses into each of its own
    ///     <see cref="VsdxShapeNode.Children"/>, composing each level's own
    ///     <see cref="VsdxShapeTransform.ToPageMatrix"/> with <paramref name="parentToPixel"/> -
    ///     uniform recursion regardless of whether <paramref name="shape"/> is a pure-container
    ///     group (whose own <see cref="VsdxShapeNode.Geometries"/>/<see cref="VsdxShapeNode.TextLayout"/>
    ///     are simply empty) or a leaf shape. A shape whose resolved <c>NonPrinting</c> cell is
    ///     <see langword="true"/> skips its own self-paint (fill/stroke/text/arrowheads) entirely
    ///     but still recurses into its own children, since <c>NonPrinting</c> is a per-shape,
    ///     non-inherited cell (see this method's own body remarks).
    /// </summary>
    /// <param name="surface">The surface to paint onto.</param>
    /// <param name="shape">The already-resolved shape to paint.</param>
    /// <param name="parentToPixel">The transform mapping <paramref name="shape"/>'s own local-box coordinates (inches) into pixel space.</param>
    /// <param name="pageToPixel">
    ///     The page-space-to-pixel-space transform (constant for the whole page) - used only for
    ///     a 1-D connector's own arrowhead tip/direction, which <see cref="VsdxConnectorEndpoints"/>
    ///     already expresses in absolute page-space inches (see that type's own remarks), bypassing
    ///     any local-space round-trip through the group-nesting chain.
    /// </param>
    private void RenderShapeRecursive(Surface surface, VsdxShapeNode shape, Matrix3x2 parentToPixel, Matrix3x2 pageToPixel)
    {
        var localToPixel = shape.Transform!.ToPageMatrix() * parentToPixel;

        // A NonPrinting shape (Visio's own per-shape, non-inherited "exclude from print/export"
        // cell - confirmed in 44501b.vsdx's Watermark Title master shape, carrying
        // <Cell N="NonPrinting" V="1"/>) must not paint its own fill/stroke/text/arrowheads, but
        // its children are still painted (NonPrinting is not inherited - see this milestone's own
        // plan report). Skipping self-paint only (not recursion) matches Visio's own print/export
        // behavior, which produced the Visio-reference PNGs this fix is verified against.
        if (!shape.EffectiveCells!.GetBool("NonPrinting"))
        {
            PaintShapeGeometry(surface, shape, localToPixel);

            // Shape-local text (glyph outlines positioned in the owning shape's own local,
            // unrotated/unflipped coordinate space - see VsdxTextLayout's own remarks) uses the
            // same localToPixel transform as the shape's own geometry.
            PaintTextLayout(surface, shape.TextLayout!, localToPixel);

            if (shape.ConnectorEndpoints is { } endpoints)
            {
                PaintConnectorArrowheads(surface, endpoints, shape.Paint!, pageToPixel);
            }
        }

        foreach (var child in shape.Children)
        {
            RenderShapeRecursive(surface, child, localToPixel, pageToPixel);
        }
    }

    /// <summary>
    ///     Paints <paramref name="shape"/>'s own resolved geometry sections: each section's
    ///     shape-local-space <see cref="Path"/>, transformed into pixel space, is filled (when the
    ///     shape's resolved paint has a fill and the section does not declare <c>NoFill</c>) and
    ///     stroked (when the shape's resolved paint has a line and the section does not declare
    ///     <c>NoLine</c>), honoring each section's own <c>NoShow</c> flag by skipping it entirely.
    /// </summary>
    /// <param name="surface">The surface to paint onto.</param>
    /// <param name="shape">The already-resolved shape whose geometry is painted.</param>
    /// <param name="localToPixel">The transform mapping <paramref name="shape"/>'s own local-box coordinates (inches) into pixel space.</param>
    private static void PaintShapeGeometry(Surface surface, VsdxShapeNode shape, Matrix3x2 localToPixel)
    {
        var paint = shape.Paint!;
        foreach (var geometry in shape.Geometries!)
        {
            if (geometry.NoShow)
            {
                continue;
            }

            var transformedPath = geometry.Path.Transform(localToPixel);

            if (paint.HasFill && !geometry.NoFill)
            {
                PathFiller.Fill(surface, transformedPath, paint.FillColor);
            }

            if (paint.HasLine && !geometry.NoLine)
            {
                var strokeWidthPixels = Math.Max((float)(paint.StrokeWidthInches * AverageScale(localToPixel)), MinStrokeWidthPixels);
                PaintStroke(surface, transformedPath, paint.StrokeColor, strokeWidthPixels);
            }
        }
    }

    /// <summary>
    ///     Paints a 1-D (connector) shape's resolved <c>BeginArrow</c>/<c>EndArrow</c> arrowheads,
    ///     oriented and positioned directly from its already-resolved, absolute page-space
    ///     <see cref="VsdxConnectorEndpoints"/> - see <see cref="RenderShapeRecursive"/>'s own
    ///     <paramref name="pageToPixel"/> remarks for why no local-space round-trip is used here.
    ///     The connector's own stroked <em>line</em> is painted separately, through the identical
    ///     uniform geometry dispatch every other shape uses (<see cref="PaintShapeGeometry"/>) -
    ///     this method paints only the two end ornaments.
    /// </summary>
    /// <param name="surface">The surface to paint onto.</param>
    /// <param name="endpoints">The connector's resolved, absolute page-space begin/end endpoints.</param>
    /// <param name="paint">The connector's resolved paint (supplies <see cref="VsdxResolvedPaint.BeginArrowhead"/>/<see cref="VsdxResolvedPaint.EndArrowhead"/> and the stroke color/width to render them with).</param>
    /// <param name="pageToPixel">The page-space-to-pixel-space transform.</param>
    private static void PaintConnectorArrowheads(
        Surface surface,
        VsdxConnectorEndpoints endpoints,
        VsdxResolvedPaint paint,
        Matrix3x2 pageToPixel)
    {
        var beginPixel = Vector2.Transform(new Vector2((float)endpoints.BeginX, (float)endpoints.BeginY), pageToPixel);
        var endPixel = Vector2.Transform(new Vector2((float)endpoints.EndX, (float)endpoints.EndY), pageToPixel);

        var delta = endPixel - beginPixel;
        if (delta.LengthSquared() <= float.Epsilon)
        {
            // A degenerate (coincident-endpoint) connector has no well-defined direction to
            // orient an arrowhead along - skip both ends rather than dividing by a near-zero
            // length below.
            return;
        }

        var direction = Vector2.Normalize(delta);
        var pixelsPerInch = AverageScale(pageToPixel);

        PaintArrowhead(surface, paint.BeginArrowhead, paint.StrokeWidthInches, paint.StrokeColor, beginPixel, -direction, pixelsPerInch);
        PaintArrowhead(surface, paint.EndArrowhead, paint.StrokeWidthInches, paint.StrokeColor, endPixel, direction, pixelsPerInch);
    }

    /// <summary>
    ///     Builds and paints a single resolved <see cref="VsdxArrowhead"/> (see
    ///     <see cref="VsdxArrowheadGeometry.Build"/>), composing its local arrow-space geometry
    ///     (built in inches) with a scale-then-rotate-then-translate placement matrix: scaled by
    ///     <paramref name="pixelsPerInch"/> into pixel magnitude, rotated so local <c>+x</c> aligns
    ///     with <paramref name="direction"/>, then translated to <paramref name="tip"/>. A no-op
    ///     (degrades silently) when <see cref="VsdxArrowheadGeometry.Build"/> resolves to an empty
    ///     path - an unrecognized arrowhead index already resolves to <see cref="VsdxArrowheadStyle.None"/>
    ///     upstream (Milestone 5), so "degrade to no arrowhead" falls out naturally here with no
    ///     extra handling needed.
    /// </summary>
    /// <param name="surface">The surface to paint onto.</param>
    /// <param name="arrowhead">The resolved arrowhead to paint.</param>
    /// <param name="strokeWidthInches">The connector line's own resolved stroke width, in inches - the arrowhead's own sizing basis (see <see cref="VsdxArrowheadGeometry"/>'s own remarks).</param>
    /// <param name="color">The color to paint the arrowhead with (the connector line's own resolved stroke color).</param>
    /// <param name="tip">The arrowhead's tip position, in pixel space.</param>
    /// <param name="direction">The unit vector, in pixel space, the arrowhead's tip points along (away from the connector's own body).</param>
    /// <param name="pixelsPerInch">The page-to-pixel scale magnitude (see <see cref="AverageScale"/>), used to size the arrowhead's local, inch-based geometry into pixel space.</param>
    private static void PaintArrowhead(
        Surface surface,
        VsdxArrowhead arrowhead,
        double strokeWidthInches,
        Rgba32 color,
        Vector2 tip,
        Vector2 direction,
        float pixelsPerInch)
    {
        var geometry = VsdxArrowheadGeometry.Build(arrowhead.Style, arrowhead.SizeIndex, strokeWidthInches);

        var angle = MathF.Atan2(direction.Y, direction.X);
        var arrowToPixel =
            Matrix3x2.CreateScale(pixelsPerInch) *
            Matrix3x2.CreateRotation(angle) *
            Matrix3x2.CreateTranslation(tip.X, tip.Y);

        var transformedPath = geometry.Path.Transform(arrowToPixel);

        if (geometry.IsFilled)
        {
            PathFiller.Fill(surface, transformedPath, color);
        }
        else
        {
            PaintStroke(surface, transformedPath, color, (float)(strokeWidthInches * pixelsPerInch));
        }
    }

    /// <summary>
    ///     Strokes <paramref name="pixelSpacePath"/> (already transformed into pixel space) with
    ///     <paramref name="strokeWidthPixels"/>, filling the resulting outline with
    ///     <paramref name="color"/> - a no-op when <paramref name="strokeWidthPixels"/> is not a
    ///     finite positive value (a resolved zero/negative stroke width degrades to "no visible
    ///     line" rather than throwing from <see cref="StrokeStyle"/>'s own constructor guard).
    /// </summary>
    /// <param name="surface">The surface to paint onto.</param>
    /// <param name="pixelSpacePath">The already pixel-space-transformed path to stroke.</param>
    /// <param name="color">The stroke color.</param>
    /// <param name="strokeWidthPixels">The stroke width, in pixels.</param>
    private static void PaintStroke(Surface surface, Path pixelSpacePath, Rgba32 color, float strokeWidthPixels)
    {
        if (!float.IsFinite(strokeWidthPixels) || strokeWidthPixels <= 0f)
        {
            return;
        }

        var outline = PathStroker.Stroke(pixelSpacePath, new StrokeStyle(strokeWidthPixels));
        PathFiller.Fill(surface, outline, color);
    }

    /// <summary>
    ///     Derives a uniform scale-magnitude approximation from <paramref name="matrix"/>'s own
    ///     diagonal (<c>(|M11| + |M22|) / 2</c>) - an accepted approximation for stroke-width/
    ///     arrowhead sizing under a non-uniform transform (<c>scaleX != scaleY</c>, only possible
    ///     via the pixel-dimension <see cref="Render(int, int, int, VsdxRenderOptions?)"/> overload
    ///     when called with an aspect ratio that does not match the page's own - the common
    ///     <see cref="Render(int, int, VsdxRenderOptions?)"/> DPI overload always produces square
    ///     pixels), consistent with <c>DemaConsulting.CanvasNet.Pptx.PptxDocument</c>'s own
    ///     equivalent handling.
    /// </summary>
    /// <param name="matrix">The transform to approximate a uniform scale magnitude from.</param>
    /// <returns>The approximated uniform scale magnitude.</returns>
    private static float AverageScale(Matrix3x2 matrix) => (MathF.Abs(matrix.M11) + MathF.Abs(matrix.M22)) / 2f;
}
