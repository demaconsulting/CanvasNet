using System.Numerics;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore xfrm grpsppr sppr pptx prst cust unrenderable

/// <summary>
///     Implements the <see cref="PptxDocument"/> public, slide-level rendering API (Phase 1f):
///     walks a slide's full shape tree (<see cref="PptxSlide.ShapeTree"/>, produced by
///     <see cref="ParseShapeTree"/>) in document order, threading an accumulating
///     <see cref="Matrix3x2"/> transform through nested <c>&lt;p:grpSp&gt;</c> groups, and
///     dispatches each leaf node kind to the already-verified Phase 1c/1d/1e resolvers/painters -
///     see <c>pptx-document.md</c>'s "Full Slide Rendering (Phase 1f)" design section for the
///     full per-node-kind dispatch and the deferred-items list this phase leaves unimplemented.
/// </summary>
public sealed partial class PptxDocument
{
    /// <summary>
    ///     Renders the specified slide into a new <see cref="Surface"/> of the given dimensions,
    ///     walking the slide's full shape tree (<see cref="PptxSlide.ShapeTree"/>) in document
    ///     order and painting each recognized shape kind (see the <see cref="PptxDocument"/>
    ///     class remarks for this phase's dispatch summary, and <c>pptx-document.md</c>'s "Full
    ///     Slide Rendering (Phase 1f)" design section for the full algorithm).
    /// </summary>
    /// <param name="slideIndex">The zero-based index of the slide to render.</param>
    /// <param name="width">The width of the rendered surface, in pixels.</param>
    /// <param name="height">The height of the rendered surface, in pixels.</param>
    /// <param name="options">
    ///     Optional slide-rendering configuration. When <see langword="null"/> (the default),
    ///     <see cref="PptxRenderOptions.Default"/> is used, which clears the surface to opaque
    ///     white before the slide's shape tree is painted.
    /// </param>
    /// <returns>
    ///     A new <see cref="Surface"/> of the requested <paramref name="width"/> x
    ///     <paramref name="height"/>, painted with the slide's interpreted shape tree. The surface
    ///     is first cleared to <paramref name="options"/>'s
    ///     <see cref="PptxRenderOptions.BackgroundColor"/> (opaque white by default), so a slide
    ///     with an empty shape tree renders as a surface filled with that color. Pass
    ///     <c>new PptxRenderOptions { BackgroundColor = new Rgba32(0, 0, 0, 0) }</c> to reproduce a
    ///     fully transparent background.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="slideIndex"/> is negative or greater than or equal to
    ///     <see cref="SlideCount"/>, or when <paramref name="width"/>/<paramref name="height"/> is
    ///     outside <see cref="Surface"/>'s own valid dimension range (propagated, unwrapped, from
    ///     the <see cref="Surface(int, int)"/> constructor).
    /// </exception>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when the slide, its layout/master/theme relationship chain, or any shape's own
    ///     DrawingML (<c>&lt;a:xfrm&gt;</c>, <c>&lt;a:prstGeom&gt;</c>/<c>&lt;a:custGeom&gt;</c>,
    ///     <c>&lt;a:solidFill&gt;</c>/<c>&lt;a:gradFill&gt;</c>, <c>&lt;p:txBody&gt;</c>, a
    ///     <c>&lt;p:pic&gt;</c>'s embedded image relationship, or an <c>&lt;a:tbl&gt;</c>) is
    ///     malformed - propagated unchanged from the Phase 1b-1e resolvers this method dispatches
    ///     to (see <see cref="GetSlide"/>/<see cref="ResolveShapeFrame"/>/
    ///     <see cref="ResolveShapeGeometry"/>/<see cref="ResolvePictureSurface"/>/
    ///     <see cref="ParseTextBody"/>).
    /// </exception>
    /// <exception cref="PptxUnsupportedFeatureException">
    ///     Thrown when a shape declares a well-formed-but-unsupported DrawingML construct -
    ///     propagated unchanged from <see cref="ResolveShapeGeometry"/> (an unsupported
    ///     <c>&lt;a:prstGeom&gt;</c> preset), <see cref="ResolveFill"/> (a pattern/picture fill or
    ///     a radial/path gradient), or <see cref="ResolvePictureSurface"/> (a linked, non-embedded
    ///     image, or an unsupported raster image format).
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when this document has been disposed.</exception>
    /// <remarks>
    ///     <para>
    ///         A shape (placeholder or freeform), picture, or graphic-frame whose fully-resolved
    ///         geometry element declares no <c>&lt;a:xfrm&gt;</c> anywhere in its own ancestry is
    ///         <strong>skipped silently</strong>, not treated as an error - a position-less shape
    ///         is a genuinely unrenderable (not malformed) construct this phase tolerates, mirroring
    ///         <see cref="ParseShapeTree"/>'s own established "tolerant tree walk" precedent.
    ///     </para>
    ///     <para>
    ///         A <c>&lt;p:cxnSp&gt;</c> connector shape, a slide's own <c>&lt;p:bg&gt;</c>
    ///         background fill, a nested table, table auto-sizing/banding, group-level style
    ///         cascading, and picture effects/shadows are not rendered this phase - see
    ///         <c>pptx-document.md</c>'s "Full Slide Rendering (Phase 1f)" design section for the
    ///         complete deferred-items list.
    ///     </para>
    /// </remarks>
    public Surface Render(int slideIndex, int width, int height, PptxRenderOptions? options = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (slideIndex < 0 || slideIndex >= _slidePartPaths.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(slideIndex), slideIndex, "Slide index is out of range.");
        }

        var surface = new Surface(width, height);
        surface.Clear((options ?? PptxRenderOptions.Default).BackgroundColor);

        var slide = GetSlide(slideIndex);
        var layout = GetLayout(slide.LayoutPartPath);
        var master = GetMaster(layout.MasterPartPath);
        var theme = GetTheme(master.ThemePartPath);

        var baseTransform = Matrix3x2.CreateScale(width / (float)SlideSize.WidthEmu, height / (float)SlideSize.HeightEmu);

        foreach (var node in slide.ShapeTree)
        {
            RenderNode(surface, node, slide, layout, master, theme, baseTransform);
        }

        return surface;
    }

    /// <summary>
    ///     Renders the specified slide at a given resolution, preserving the slide's own aspect
    ///     ratio.
    /// </summary>
    /// <remarks>
    ///     A convenience wrapper over <see cref="Render(int, int, int, PptxRenderOptions?)"/> for
    ///     the common case of wanting undistorted, uniformly-scaled output: this overload reads
    ///     <see cref="SlideSize"/> (in EMU, 914,400 EMU/inch), scales both dimensions by
    ///     <paramref name="dpi"/> / 914400, and rounds to the nearest pixel before delegating to
    ///     <see cref="Render(int, int, int, PptxRenderOptions?)"/>. Callers needing independent
    ///     X/Y scaling (for example, non-square pixels, or a specific pixel size regardless of
    ///     aspect ratio) should call <see cref="Render(int, int, int, PptxRenderOptions?)"/>
    ///     directly instead.
    /// </remarks>
    /// <param name="slideIndex">The zero-based index of the slide to render.</param>
    /// <param name="dpi">The resolution to render at, in dots (pixels) per inch.</param>
    /// <param name="options">
    ///     Optional slide-rendering configuration, forwarded unchanged to
    ///     <see cref="Render(int, int, int, PptxRenderOptions?)"/>. When <see langword="null"/>
    ///     (the default), <see cref="PptxRenderOptions.Default"/> is used, which clears the
    ///     surface to opaque white before the slide's shape tree is painted.
    /// </param>
    /// <returns>
    ///     A new <see cref="Surface"/> sized to the slide's aspect ratio at <paramref name="dpi"/>,
    ///     painted with the slide's interpreted shape tree. The surface is first cleared to
    ///     <paramref name="options"/>'s <see cref="PptxRenderOptions.BackgroundColor"/> (opaque
    ///     white by default) - see <see cref="Render(int, int, int, PptxRenderOptions?)"/> for
    ///     details, including how to reproduce a fully transparent background.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="slideIndex"/> is negative or greater than or equal to
    ///     <see cref="SlideCount"/>, when <paramref name="dpi"/> is not a positive, finite number,
    ///     or when the computed pixel width/height is outside <see cref="Surface"/>'s own valid
    ///     dimension range (propagated, unwrapped, from the <see cref="Surface(int, int)"/>
    ///     constructor).
    /// </exception>
    /// <exception cref="System.IO.InvalidDataException">
    ///     See <see cref="Render(int, int, int, PptxRenderOptions?)"/>.
    /// </exception>
    /// <exception cref="PptxUnsupportedFeatureException">
    ///     See <see cref="Render(int, int, int, PptxRenderOptions?)"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when this document has been disposed.</exception>
    public Surface Render(int slideIndex, float dpi, PptxRenderOptions? options = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!float.IsFinite(dpi) || dpi <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dpi), dpi, "DPI must be a positive, finite number.");
        }

        // SlideSize is in EMU (914400 EMU/inch); dpi is pixels/inch. SlideSize's own getter
        // re-validates disposal, so no separate ObjectDisposedException check is needed here
        // beyond the one above (kept for a clear, slideIndex-independent failure point before any
        // EMU math runs).
        var slideSize = SlideSize;
        if (slideIndex < 0 || slideIndex >= _slidePartPaths.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(slideIndex), slideIndex, "Slide index is out of range.");
        }

        var scale = dpi / 914400.0;
        var width = (int)Math.Round(slideSize.WidthEmu * scale, MidpointRounding.AwayFromZero);
        var height = (int)Math.Round(slideSize.HeightEmu * scale, MidpointRounding.AwayFromZero);
        return Render(slideIndex, width, height, options);
    }

    /// <summary>
    ///     Dispatches a single shape-tree node to its own per-kind rendering helper, recursing
    ///     into a group's own children with its composed child transform.
    /// </summary>
    private void RenderNode(
        Surface surface,
        PptxShapeTreeNode node,
        PptxSlide slide,
        PptxLayout layout,
        PptxMaster master,
        PptxTheme theme,
        Matrix3x2 parentToSurface)
    {
        switch (node)
        {
            case PptxGroupShapeNode group:
                var childToSurface = group.ChildTransform * parentToSurface;
                foreach (var child in group.Children)
                {
                    RenderNode(surface, child, slide, layout, master, theme, childToSurface);
                }

                break;

            case PptxSpShapeNode sp:
                RenderShape(surface, sp, layout, master, theme, parentToSurface);
                break;

            case PptxPictureShapeNode pic:
                RenderPicture(surface, pic, slide, parentToSurface);
                break;

            case PptxGraphicFrameShapeNode graphicFrame:
                RenderGraphicFrame(surface, graphicFrame, theme, parentToSurface);
                break;
        }

        // No default arm: PptxShapeTreeNode is a closed hierarchy over exactly these four
        // subtypes (see PptxShapeTree.cs's own remarks) - ParseShapeTree already excludes
        // connectors (<p:cxnSp>) and every other unrecognized element kind before a
        // PptxShapeTreeNode is ever constructed, so there is no fifth case to handle here.
    }

    /// <summary>
    ///     Renders a <see cref="PptxSpShapeNode"/> (an ordinary or placeholder shape): resolves
    ///     its geometry/fill/stroke via the Phase 1c pipeline and, when it declares a
    ///     <c>&lt;p:txBody&gt;</c>, its text via the Phase 1d pipeline.
    /// </summary>
    private static void RenderShape(
        Surface surface,
        PptxSpShapeNode node,
        PptxLayout layout,
        PptxMaster master,
        PptxTheme theme,
        Matrix3x2 parentToSurface)
    {
        XElement? spPrElement;
        XElement? xfrmElement;
        XElement? geometrySpPrElement;
        PptxPlaceholderProperties placeholderProperties;
        string placeholderType;

        if (node.Placeholder is { } placeholder)
        {
            placeholderProperties = ResolvePlaceholderProperties(
                placeholder, layout.Placeholders, master.Placeholders, theme, master.TxStyles);
            spPrElement = placeholderProperties.EffectiveSpPr;
            xfrmElement = placeholderProperties.EffectiveXfrmElement;
            geometrySpPrElement = placeholderProperties.EffectiveGeometrySpPr;
            // Use the resolved effective type (own declared type, else the idx-matched layout
            // placeholder's type, else the schema-defaulted type) rather than the slide
            // placeholder's own raw/schema-defaulted type - see
            // PptxPlaceholderProperties.EffectivePlaceholderType's remarks for why using the raw
            // type here under-resolves an omitted-type title/subtitle placeholder's master
            // text-style bucket to body style.
            placeholderType = placeholderProperties.EffectivePlaceholderType ?? placeholder.Type;
        }
        else
        {
            placeholderProperties = new PptxPlaceholderProperties(null, null, theme, master.TxStyles);
            spPrElement = node.ShapeElement.Element(PresentationNamespace + "spPr");
            xfrmElement = spPrElement?.Element(DrawingNamespace + "xfrm");
            geometrySpPrElement = spPrElement;
            placeholderType = string.Empty;
        }

        if (spPrElement is null || xfrmElement is null || geometrySpPrElement is null)
        {
            // No resolvable <a:xfrm> (or no resolvable <a:prstGeom>/<a:custGeom>) anywhere in
            // this shape's ancestry - skip silently rather than throwing (see this file's Render
            // remarks, Assumption 1 of the companion planning report).
            return;
        }

        var frame = ResolveShapeFrame(xfrmElement);
        var localToSurface = frame.Transform * parentToSurface;

        var geometryPath = ResolveShapeGeometry(geometrySpPrElement, frame.WidthEmu, frame.HeightEmu);
        var transformedPath = geometryPath.Transform(localToSurface);

        var fill = ResolveFill(spPrElement, theme, frame.WidthEmu, frame.HeightEmu);
        FillPaint(surface, transformedPath, fill);

        var lineStyle = ResolveLineStyle(spPrElement.Element(DrawingNamespace + "ln"), theme);
        if (lineStyle is not null)
        {
            var strokedOutline = ResolveStrokeOutline(geometryPath, lineStyle).Transform(localToSurface);
            FillPaint(surface, strokedOutline, lineStyle.Paint);
        }

        // A placeholder's own text content is always read from the slide-level shape element
        // itself - only styling inherits from the layout/master, never content (see the
        // companion planning report's Risk 2).
        var txBodyElement = node.ShapeElement.Element(PresentationNamespace + "txBody");
        if (txBodyElement is not null)
        {
            var textBody = ParseTextBody(txBodyElement);
            var layoutResult = ResolveTextLayout(
                textBody, placeholderProperties, theme, placeholderType, frame.WidthEmu, frame.HeightEmu, ResolveTextFont);
            PaintTextLayout(surface, layoutResult, localToSurface);
        }
    }

    /// <summary>
    ///     Renders a <see cref="PptxPictureShapeNode"/>: decodes and composites its embedded
    ///     image via the Phase 1e picture pipeline.
    /// </summary>
    private void RenderPicture(Surface surface, PptxPictureShapeNode node, PptxSlide slide, Matrix3x2 parentToSurface)
    {
        var spPrElement = node.PicElement.Element(PresentationNamespace + "spPr");
        var xfrmElement = spPrElement?.Element(DrawingNamespace + "xfrm");
        if (xfrmElement is null)
        {
            // Schema-malformed (a well-formed <p:pic> always declares this), but tolerated
            // defensively for consistency with the shape/graphic-frame skip policy (see
            // this file's Render remarks).
            return;
        }

        var blipFillElement = node.PicElement.Element(PresentationNamespace + "blipFill");
        if (blipFillElement is null)
        {
            return;
        }

        var frame = ResolveShapeFrame(xfrmElement);
        var localToSurface = frame.Transform * parentToSurface;

        var image = ResolvePictureSurface(slide.PartPath, blipFillElement);
        var srcRect = ResolveSrcRect(blipFillElement);
        PaintPicture(surface, image, srcRect, localToSurface, frame.WidthEmu, frame.HeightEmu);
    }

    /// <summary>
    ///     Renders a <see cref="PptxGraphicFrameShapeNode"/> (a table): paints its already-parsed
    ///     <see cref="PptxGraphicFrameShapeNode.Table"/> via the Phase 1e table pipeline.
    /// </summary>
    private static void RenderGraphicFrame(Surface surface, PptxGraphicFrameShapeNode node, PptxTheme theme, Matrix3x2 parentToSurface)
    {
        // Per ECMA-376's CT_GraphicalObjectFrame, a <p:graphicFrame>'s own position is a direct
        // <p:xfrm> child - not wrapped in a <p:spPr>, unlike an ordinary shape or picture.
        var xfrmElement = node.GraphicFrameElement.Element(PresentationNamespace + "xfrm");
        if (xfrmElement is null)
        {
            return;
        }

        var frame = ResolveShapeFrame(xfrmElement);
        var localToSurface = frame.Transform * parentToSurface;

        PaintTable(surface, node.Table, theme, localToSurface, ResolveTextFont);
    }
}
