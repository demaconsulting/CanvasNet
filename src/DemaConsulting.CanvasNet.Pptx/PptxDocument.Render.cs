using System.Numerics;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore xfrm grpsppr sppr pptx prst cust unrenderable patt

/// <summary>
///     Implements the <see cref="PptxDocument"/> public, slide-level rendering API (Phase 1f):
///     walks a slide's full shape tree (<see cref="PptxSlide.ShapeTree"/>, produced by
///     <see cref="ParseShapeTree"/>) in document order, threading an accumulating
///     <see cref="Matrix3x2"/> transform through nested <c>&lt;p:grpSp&gt;</c> groups, and
///     dispatches each leaf node kind to the already-verified Phase 1c/1d/1e resolvers/painters -
///     see <c>pptx-document.md</c>'s "Full Slide Rendering (Phase 1f)" design section for the
///     full per-node-kind dispatch and the deferred-items list this phase leaves unimplemented.
///     As of the Phase 2 Follow-Up background-fill hardening pass, also paints the slide's own
///     (or, failing that, its layout's/master's) <c>&lt;p:bg&gt;</c> background fill before the
///     shape-tree walk - see <see cref="ResolveSlideBackgroundFill"/>.
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
    ///     <see cref="PptxRenderOptions.BackgroundColor"/> (opaque white by default); the slide's
    ///     own <c>&lt;p:cSld&gt;/&lt;p:bg&gt;</c> background fill (falling back to its layout's,
    ///     then its master's, own <c>&lt;p:bg&gt;</c> - see
    ///     <see cref="ResolveSlideBackgroundFill"/>) is then painted across the full slide, before
    ///     any shape is walked, so slide content continues to draw on top of it; when none of
    ///     slide/layout/master declare a <c>&lt;p:bg&gt;</c> at all, <paramref name="options"/>'s
    ///     <see cref="PptxRenderOptions.BackgroundColor"/> remains the only background a slide
    ///     with an empty shape tree renders as. Pass
    ///     <c>new PptxRenderOptions { BackgroundColor = new Rgba32(0, 0, 0, 0) }</c> to reproduce a
    ///     fully transparent fallback background.
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
    ///     <see cref="ParseTextBody"/>) - including a <c>&lt;p:pic&gt;</c>'s own
    ///     <c>&lt;a:prstGeom&gt;</c>/<c>&lt;a:custGeom&gt;</c> clip geometry, propagated unchanged
    ///     from <see cref="ResolvePictureClipPath"/> exactly as it already propagates for an
    ///     auto-shape's own geometry.
    /// </exception>
    /// <exception cref="PptxUnsupportedFeatureException">
    ///     Thrown when a shape declares a well-formed-but-unsupported DrawingML construct -
    ///     propagated unchanged from <see cref="ResolveShapeGeometry"/> (an unsupported
    ///     <c>&lt;a:prstGeom&gt;</c> preset), <see cref="ResolveFill"/> (a pattern/picture fill or
    ///     a radial/path gradient), <see cref="ResolvePictureSurface"/> (a linked, non-embedded
    ///     image, or an unsupported raster image format), or <see cref="ResolvePictureClipPath"/>
    ///     (a <c>&lt;p:pic&gt;</c>'s own unsupported <c>&lt;a:prstGeom&gt;</c> clip preset).
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
    ///         A <c>&lt;p:cxnSp&gt;</c> connector shape, a nested table, table auto-sizing/
    ///         banding, group-level style cascading, and picture effects/shadows are not rendered
    ///         this phase - see <c>pptx-document.md</c>'s "Full Slide Rendering (Phase 1f)" design
    ///         section for the complete deferred-items list, and its "Phase 2 Follow-Up: Slide/
    ///         Layout/Master Background Fill (&lt;p:bg&gt;)" section for the background-fill
    ///         fidelity achieved (solid and theme-indexed <c>&lt;p:bgRef&gt;</c> fills: full;
    ///         linear gradient: best-effort; pattern/picture background fill: still deferred).
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

        var colorMap = ResolveEffectiveColorMap(slide.ClrMapOvr, layout.ClrMapOvr, master.ColorMap);

        var backgroundFill = ResolveSlideBackgroundFill(
            slide.Background, layout.Background, master.Background, theme, SlideSize.WidthEmu, SlideSize.HeightEmu, colorMap);
        if (backgroundFill is not null)
        {
            var backgroundPath = Path.Rectangle(0, 0, SlideSize.WidthEmu, SlideSize.HeightEmu).Transform(baseTransform);
            FillPaint(surface, backgroundPath, backgroundFill);
        }

        // Phase 2 Follow-Up: paint the master's, then the layout's, own non-placeholder
        // decorative shapes (pictures, autoshapes, groups, freeform shapes) before the slide's
        // own shape tree, so paint order becomes background -> master decoration -> layout
        // decoration -> slide content (each later tier painting on top of the earlier ones). Each
        // walk's own owner part path (not the slide's) is threaded through so a master/layout-
        // owned <p:pic>'s embedded-image relationship resolves against its own .rels file, not
        // the slide's. skipPlaceholderShapes: true means a master/layout's own placeholder shapes
        // (its "Click to edit..." prompt content) are never painted directly - only their
        // non-placeholder siblings are.
        foreach (var node in master.ShapeTree)
        {
            RenderNode(surface, node, master.PartPath, layout, master, theme, baseTransform, colorMap, skipPlaceholderShapes: true);
        }

        foreach (var node in layout.ShapeTree)
        {
            RenderNode(surface, node, layout.PartPath, layout, master, theme, baseTransform, colorMap, skipPlaceholderShapes: true);
        }

        foreach (var node in slide.ShapeTree)
        {
            RenderNode(surface, node, slide.PartPath, layout, master, theme, baseTransform, colorMap);
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
    ///     or when the computed pixel width/height is non-finite, non-positive, or exceeds
    ///     <see cref="Surface.MaxDimension"/> (validated directly in this method, against
    ///     <paramref name="dpi"/>, before the narrowing cast to <see cref="int"/>).
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
        var widthPixels = slideSize.WidthEmu * scale;
        var heightPixels = slideSize.HeightEmu * scale;
        if (!double.IsFinite(widthPixels) || !double.IsFinite(heightPixels) ||
            widthPixels <= 0 || heightPixels <= 0 ||
            widthPixels > Surface.MaxDimension || heightPixels > Surface.MaxDimension)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dpi), dpi,
                $"The computed pixel dimensions must be positive and not exceed {Surface.MaxDimension}x{Surface.MaxDimension}.");
        }

        var width = (int)Math.Round(widthPixels, MidpointRounding.AwayFromZero);
        var height = (int)Math.Round(heightPixels, MidpointRounding.AwayFromZero);
        return Render(slideIndex, width, height, options);
    }

    /// <summary>
    ///     Dispatches a single shape-tree node to its own per-kind rendering helper, recursing
    ///     into a group's own children with its composed child transform.
    /// </summary>
    /// <param name="surface">The destination surface to paint onto.</param>
    /// <param name="node">The shape-tree node to dispatch.</param>
    /// <param name="ownerPartPath">
    ///     The part path that owns <paramref name="node"/> (the slide's, layout's, or master's own
    ///     part path) - needed so a <see cref="PptxPictureShapeNode"/>'s embedded-image
    ///     relationship (see <see cref="RenderPicture"/>) resolves against the <em>owning</em>
    ///     part's own <c>.rels</c> file, not always the slide's (OPC relationships are part-scoped
    ///     - see the companion planning report's bug-fix rationale).
    /// </param>
    /// <param name="layout">The slide's own resolved layout, consulted for placeholder-property inheritance.</param>
    /// <param name="master">The slide's own resolved master, consulted for placeholder-property inheritance.</param>
    /// <param name="theme">The slide's own resolved theme, consulted for color/font resolution.</param>
    /// <param name="parentToSurface">The accumulated transform from this node's own parent space into surface pixel space.</param>
    /// <param name="colorMap">
    ///     The slide's own effective color map (see <see cref="ResolveEffectiveColorMap"/>),
    ///     computed once in <see cref="Render(int, int, int, PptxRenderOptions?)"/> and threaded
    ///     unchanged through every recursive call - consulted whenever a resolved fill/line/text
    ///     color declares an <c>&lt;a:schemeClr val="bg1"/&gt;</c>-shaped token.
    /// </param>
    /// <param name="skipPlaceholderShapes">
    ///     When <see langword="true"/> (the master/layout decorative-shape walks in
    ///     <see cref="Render(int, int, int, PptxRenderOptions?)"/>), a <see cref="PptxSpShapeNode"/>
    ///     with a non-null <see cref="PptxSpShapeNode.Placeholder"/> is skipped without recursing
    ///     into <see cref="RenderShape"/> - a master/layout's own placeholder shapes are
    ///     edit-mode-only "Click to edit..." prompt content in real PowerPoint and must stay
    ///     invisible when rendering an actual slide; only their non-placeholder siblings (and a
    ///     group's own non-placeholder descendants) are painted. Propagated unchanged into
    ///     recursive calls for a group's own children. Always <see langword="false"/> for the
    ///     slide's own shape-tree walk (a slide's placeholder shapes, unlike a master's/layout's
    ///     own, are real content and must render normally). This same flag also gates
    ///     exception-containment: when <see langword="true"/>, a <see cref="PptxUnsupportedFeatureException"/>
    ///     thrown while painting a single master/layout shape (including one nested inside a
    ///     group) is caught and only that one shape is skipped, so one already-deferred, well-
    ///     formed-but-unsupported decorative shape (for example an EMF picture) cannot abort the
    ///     rest of the slide's rendering - see <c>pptx-document.md</c>'s "Phase 2 Follow-Up:
    ///     Master/Layout Decorative Shape Rendering" section. A slide's own shape
    ///     (<see langword="false"/>) is never caught here and continues to hard-fail
    ///     <see cref="Render(int, int, int, PptxRenderOptions?)"/> exactly as before this
    ///     containment was added.
    /// </param>
    private void RenderNode(
        Surface surface,
        PptxShapeTreeNode node,
        string ownerPartPath,
        PptxLayout layout,
        PptxMaster master,
        PptxTheme theme,
        Matrix3x2 parentToSurface,
        PptxColorMap colorMap,
        bool skipPlaceholderShapes = false)
    {
        switch (node)
        {
            case PptxGroupShapeNode group:
                var childToSurface = group.ChildTransform * parentToSurface;
                foreach (var child in group.Children)
                {
                    RenderNode(surface, child, ownerPartPath, layout, master, theme, childToSurface, colorMap, skipPlaceholderShapes);
                }

                break;

            case PptxSpShapeNode sp:
                if (skipPlaceholderShapes && sp.Placeholder is not null)
                {
                    break;
                }

                try
                {
                    RenderShape(surface, sp, layout, master, theme, parentToSurface, colorMap);
                }
                catch (PptxUnsupportedFeatureException) when (skipPlaceholderShapes)
                {
                    // A master/layout's own decorative shape using an already-deferred, well-
                    // formed-but-unsupported feature must not abort the rest of the slide - see
                    // pptx-document.md's "Phase 2 Follow-Up: Master/Layout Decorative Shape
                    // Rendering" (graceful-skip containment). The `when (skipPlaceholderShapes)`
                    // filter means a slide's own shape (skipPlaceholderShapes == false) is never
                    // caught here and continues to hard-fail Render exactly as before this fix.
                }

                break;

            case PptxPictureShapeNode pic:
                try
                {
                    RenderPicture(surface, pic, ownerPartPath, theme, parentToSurface, colorMap);
                }
                catch (PptxUnsupportedFeatureException) when (skipPlaceholderShapes)
                {
                    // See the PptxSpShapeNode case's own remarks above.
                }

                break;

            case PptxGraphicFrameShapeNode graphicFrame:
                try
                {
                    RenderGraphicFrame(surface, graphicFrame, theme, parentToSurface, colorMap);
                }
                catch (PptxUnsupportedFeatureException) when (skipPlaceholderShapes)
                {
                    // See the PptxSpShapeNode case's own remarks above.
                }

                break;

            case PptxConnectorShapeNode connector:
                try
                {
                    RenderConnector(surface, connector, theme, parentToSurface, colorMap);
                }
                catch (PptxUnsupportedFeatureException) when (skipPlaceholderShapes)
                {
                    // See the PptxSpShapeNode case's own remarks above. RenderConnector itself
                    // additionally, and unconditionally (regardless of skipPlaceholderShapes),
                    // already narrowly contains the one sub-step realistically able to throw this
                    // same exception type (an exotic/unimplemented connector preset name) to just
                    // that one connector - see RenderConnector's own remarks - so this outer catch
                    // exists only for parity with every other node kind's own containment and is
                    // not expected to ever actually trigger for a connector in practice.
                }

                break;
        }

        // No default arm: PptxShapeTreeNode is a closed hierarchy over exactly these five
        // subtypes (see PptxShapeTree.cs's own remarks) - ParseShapeTree already excludes every
        // other unrecognized element kind before a PptxShapeTreeNode is ever constructed, so
        // there is no sixth case to handle here.
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
        Matrix3x2 parentToSurface,
        PptxColorMap colorMap)
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

        // A shape's own "Shape Styles" gallery reference (p:style's fillRef/lnRef) is always read
        // from the slide shape's own element, never inherited from its layout/master placeholder,
        // unlike the fill-position/xfrm/geometry resolution above. This is a documented,
        // narrower-scope limitation - see pptx-document.md's "Phase 2 Follow-Up: Shape Style
        // References" design section.
        var styleElement = node.ShapeElement.Element(PresentationNamespace + "style");

        // An explicit fill-definition child on spPr always wins over a style fillRef
        // (ResolveFill's own "nothing at all" and "explicit noFill" cases are otherwise
        // indistinguishable from its return value alone).
        var fill = HasExplicitFillChild(spPrElement)
            ? ResolveFill(spPrElement, theme, frame.WidthEmu, frame.HeightEmu, colorMap: colorMap)
            : ResolveShapeStyleFill(styleElement, theme, frame.WidthEmu, frame.HeightEmu, colorMap);
        FillPaint(surface, transformedPath, fill);

        // An explicit fill-definition child (including <a:noFill/>) on the shape's own <a:ln>
        // wins outright over a style lnRef. A present <a:ln> with no recognized fill-definition
        // child of its own keeps its own width/dash but defers only its color to the style
        // lnRef (falling back to "no stroke" when no style color is available). A fully absent
        // <a:ln> defers entirely to the style lnRef. See ResolveShapeLineStyle's own remarks.
        var lnElement = spPrElement.Element(DrawingNamespace + "ln");
        var lineStyle = ResolveShapeLineStyle(lnElement, styleElement, theme, colorMap);
        if (lineStyle is not null)
        {
            var strokedOutline = ResolveStrokeOutline(geometryPath, lineStyle, localToSurface).Transform(localToSurface);
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
                textBody, placeholderProperties, theme, placeholderType, frame.WidthEmu, frame.HeightEmu, ResolveTextFont, colorMap);
            PaintTextLayout(surface, layoutResult, localToSurface);
        }
    }

    /// <summary>
    ///     Determines whether <paramref name="fillParentElement"/> declares an explicit
    ///     fill-definition child (<c>&lt;a:noFill&gt;</c>/<c>&lt;a:solidFill&gt;</c>/
    ///     <c>&lt;a:gradFill&gt;</c>/<c>&lt;a:pattFill&gt;</c>/<c>&lt;a:blipFill&gt;</c>) at all -
    ///     needed because <see cref="ResolveFill"/> itself collapses "explicit <c>&lt;a:noFill/&gt;</c>"
    ///     and "no recognized fill-definition child at all" to the same <see cref="PptxNoFill.Instance"/>
    ///     return value, so that return value alone cannot distinguish "this shape explicitly wins
    ///     over its own <c>&lt;p:style&gt;/&lt;a:fillRef&gt;</c>" from "this shape falls back to
    ///     it" (see <see cref="RenderShape"/>).
    /// </summary>
    private static bool HasExplicitFillChild(XElement fillParentElement) =>
        fillParentElement.Element(DrawingNamespace + "noFill") is not null ||
        fillParentElement.Element(DrawingNamespace + "solidFill") is not null ||
        fillParentElement.Element(DrawingNamespace + "gradFill") is not null ||
        fillParentElement.Element(DrawingNamespace + "pattFill") is not null ||
        fillParentElement.Element(DrawingNamespace + "blipFill") is not null;

    /// <summary>
    ///     Renders a <see cref="PptxPictureShapeNode"/>: decodes and composites its embedded
    ///     image via the Phase 1e picture pipeline, clipped to its own resolved
    ///     <c>&lt;p:spPr&gt;</c>/<c>&lt;a:prstGeom&gt;</c>/<c>&lt;a:custGeom&gt;</c> geometry (see
    ///     <see cref="ResolvePictureClipPath"/>) when it declares a non-<c>rect</c> preset or a
    ///     custom geometry - Phase 2 Follow-Up: Picture Preset-Geometry Clipping - and then, on
    ///     top of the composited image, strokes the picture's own <c>&lt;a:ln&gt;</c> (when
    ///     present) around its resolved geometry (see <see cref="ResolvePictureGeometryPath"/>) -
    ///     Phase 2 Follow-Up: Picture Own-Stroke Outline Rendering. Mirrors <see cref="RenderShape"/>'s
    ///     own fill-then-stroke painting order: the image content paints first, and the stroke
    ///     frames it on top, matching real-world PowerPoint's own visual stacking.
    /// </summary>
    /// <param name="surface">The destination surface to paint onto.</param>
    /// <param name="node">The picture shape-tree node to render.</param>
    /// <param name="ownerPartPath">
    ///     The part path that owns <paramref name="node"/> (the slide's, layout's, or master's own
    ///     part path) - the <c>&lt;a:blip r:embed="..."/&gt;</c> relationship is scoped to this
    ///     part's own <c>.rels</c> file (see <see cref="ResolvePictureSurface"/>).
    /// </param>
    /// <param name="theme">
    ///     The resolved theme, used to resolve the picture's own stroke line style (see
    ///     <see cref="ResolveShapeLineStyle"/>'s <c>theme</c> parameter) - needed because a
    ///     <c>&lt;p:pic&gt;</c>'s own <c>&lt;a:ln&gt;</c> can defer its color to a sibling
    ///     <c>&lt;p:style&gt;/&lt;a:lnRef&gt;</c>, exactly like an ordinary <c>&lt;p:sp&gt;</c>.
    /// </param>
    /// <param name="parentToSurface">The accumulated transform from this node's own parent space into surface pixel space.</param>
    /// <param name="colorMap">
    ///     The effective color map consulted when the resolved stroke paint declares an
    ///     <c>&lt;a:schemeClr val="bg1"/&gt;</c>-shaped token - see <see cref="ResolveShapeLineStyle"/>'s
    ///     matching parameter.
    /// </param>
    private void RenderPicture(
        Surface surface,
        PptxPictureShapeNode node,
        string ownerPartPath,
        PptxTheme theme,
        Matrix3x2 parentToSurface,
        PptxColorMap colorMap)
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

        var image = ResolvePictureSurface(ownerPartPath, blipFillElement);
        var srcRect = ResolveSrcRect(blipFillElement);

        // spPrElement is guaranteed non-null here: xfrmElement (checked above) is resolved via
        // spPrElement?.Element(...), so a null xfrmElement would already have returned.
        var clipPath = ResolvePictureClipPath(spPrElement!, frame.WidthEmu, frame.HeightEmu);
        PaintPicture(surface, image, srcRect, localToSurface, frame.WidthEmu, frame.HeightEmu, clipPath);

        // The picture's own "Shape Styles" gallery reference (p:style's lnRef), mirroring
        // RenderShape's identical sibling-element lookup - a <p:pic> can carry a sibling
        // <p:style> under ECMA-376's CT_Picture schema exactly like a <p:sp>'s CT_Shape.
        var styleElement = node.PicElement.Element(PresentationNamespace + "style");

        // The picture's own <a:ln>: absent entirely until this fix (the root cause this phase
        // resolves - see this method's own remarks and the companion planning report). Resolved
        // via the exact same ResolveShapeLineStyle an ordinary auto-shape already uses - no new
        // line-style-resolution logic needed.
        var lnElement = spPrElement!.Element(DrawingNamespace + "ln");
        var lineStyle = ResolveShapeLineStyle(lnElement, styleElement, theme, colorMap);
        if (lineStyle is not null)
        {
            // Unlike the clip path above (which collapses "no geometry"/"rect" to null - a pure
            // image-content-clip optimization), the stroke outline always needs a concrete
            // geometry path - see ResolvePictureGeometryPath's own remarks for why this is a
            // deliberately separate resolution from clipPath above.
            var geometryPath = ResolvePictureGeometryPath(spPrElement, frame.WidthEmu, frame.HeightEmu);
            var strokedOutline = ResolveStrokeOutline(geometryPath, lineStyle, localToSurface).Transform(localToSurface);
            FillPaint(surface, strokedOutline, lineStyle.Paint);
        }
    }

    /// <summary>
    ///     Renders a <see cref="PptxGraphicFrameShapeNode"/> (a table): paints its already-parsed
    ///     <see cref="PptxGraphicFrameShapeNode.Table"/> via the Phase 1e table pipeline.
    /// </summary>
    private static void RenderGraphicFrame(
        Surface surface, PptxGraphicFrameShapeNode node, PptxTheme theme, Matrix3x2 parentToSurface, PptxColorMap colorMap)
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

        PaintTable(surface, node.Table, theme, localToSurface, ResolveTextFont, colorMap);
    }

    /// <summary>
    ///     Renders a <see cref="PptxConnectorShapeNode"/> (Phase 2 Follow-Up: Connector Shape
    ///     Rendering): a <c>&lt;p:cxnSp&gt;</c> straight/elbow/curved connector line, typically
    ///     drawn between two other shapes in a flowchart or diagram, with no text body and
    ///     (unless it explicitly declares one) no fill of its own.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     Painting order mirrors <see cref="RenderShape"/>'s own fill-then-stroke order: an
    ///     explicit fill (rare for a connector, but valid - see hard requirement #4 of the
    ///     companion planning report) is painted first, then the stroked line outline, then
    ///     either endpoint's own resolved arrowhead.
    ///     </para>
    ///     <para>
    ///     <b>Exception containment.</b> Only <see cref="ResolveShapeGeometry"/> - the one
    ///     realistically able to throw a <see cref="PptxUnsupportedFeatureException"/> for a
    ///     connector (an exotic/unimplemented preset name; see
    ///     <see cref="PptxPresetGeometry.Build"/>) - is wrapped in its own, narrow,
    ///     <em>unconditional</em> try/catch here, deliberately returning (skipping just this one
    ///     connector) regardless of the caller's own <c>skipPlaceholderShapes</c> containment
    ///     policy. This is a deliberate, connector-specific deviation from every other node kind's
    ///     policy (which only ever skips gracefully for a master/layout's own decorative shapes,
    ///     per <see cref="RenderNode"/>'s own remarks): an unsupported connector preset is common
    ///     enough in real-world decks (this phase supports only the straight/bent/curved
    ///     connector families - see <see cref="PptxPresetGeometry"/>) that it should degrade to
    ///     "this one connector is invisible" rather than aborting an entire slide's own rendering,
    ///     even for a slide's own (non-placeholder) shape tree.
    ///     </para>
    /// </remarks>
    private static void RenderConnector(
        Surface surface, PptxConnectorShapeNode node, PptxTheme theme, Matrix3x2 parentToSurface, PptxColorMap colorMap)
    {
        var spPrElement = node.CxnSpElement.Element(PresentationNamespace + "spPr");
        var xfrmElement = spPrElement?.Element(DrawingNamespace + "xfrm");
        if (spPrElement is null || xfrmElement is null)
        {
            // No resolvable <a:xfrm> - skip silently rather than throwing, matching RenderShape's
            // own policy for a shape with no resolvable frame.
            return;
        }

        var frame = ResolveShapeFrame(xfrmElement);
        var localToSurface = frame.Transform * parentToSurface;

        Path geometryPath;
        try
        {
            geometryPath = ResolveShapeGeometry(spPrElement, frame.WidthEmu, frame.HeightEmu);
        }
        catch (PptxUnsupportedFeatureException)
        {
            // See this method's own remarks: an unsupported connector preset degrades to "this
            // one connector is invisible", unconditionally, rather than aborting the slide.
            return;
        }

        if (geometryPath.Subpaths.Count == 0)
        {
            return;
        }

        // A connector has no fill by default (hard requirement #4) - only paint one when the
        // shape's own spPr explicitly declares a recognized fill-definition child, exactly like
        // RenderShape's own "explicit fill-definition child always wins" HasExplicitFillChild
        // check (a connector has no <p:style>/<a:fillRef> style fallback to consult at all).
        if (HasExplicitFillChild(spPrElement))
        {
            var fill = ResolveFill(spPrElement, theme, frame.WidthEmu, frame.HeightEmu, colorMap: colorMap);
            var transformedPath = geometryPath.Transform(localToSurface);
            FillPaint(surface, transformedPath, fill);
        }

        var styleElement = node.CxnSpElement.Element(PresentationNamespace + "style");
        var lineStyle = ResolveConnectorLineStyle(spPrElement, styleElement, theme, colorMap);
        if (lineStyle is null)
        {
            return;
        }

        var strokedOutline = ResolveStrokeOutline(geometryPath, lineStyle, localToSurface).Transform(localToSurface);
        FillPaint(surface, strokedOutline, lineStyle.Paint);

        var lnElement = spPrElement.Element(DrawingNamespace + "ln");
        var (startPoint, startTangent, endPoint, endTangent) = ComputeEndpointsAndTangents(geometryPath);

        // headEnd is this connector's own start vertex, oriented pointing backward (away from the
        // line, continuing past the start in the reverse direction of travel) - see
        // PptxDocument.Connectors.cs's own remarks for why this mapping (rather than the other,
        // superficially equally plausible one) matches real PowerPoint-authored connectors.
        var headEnd = ResolveArrowhead(lnElement, "headEnd");
        if (headEnd is not null)
        {
            PaintArrowhead(surface, headEnd, lineStyle, startPoint, -startTangent, localToSurface);
        }

        // tailEnd is this connector's own end vertex, oriented pointing forward (continuing past
        // the end in the same direction of travel).
        var tailEnd = ResolveArrowhead(lnElement, "tailEnd");
        if (tailEnd is not null)
        {
            PaintArrowhead(surface, tailEnd, lineStyle, endPoint, endTangent, localToSurface);
        }
    }

    /// <summary>
    ///     Builds, orients, positions, and paints a single resolved connector arrowhead.
    /// </summary>
    /// <param name="surface">The destination surface to paint onto.</param>
    /// <param name="style">The resolved arrowhead style to paint.</param>
    /// <param name="lineStyle">The connector's own resolved line style (supplies the arrowhead's own size-scale basis and, for an open/stroked arrowhead, its paint/width).</param>
    /// <param name="point">The arrowhead's own tip position, in the connector's local (pre-<paramref name="localToSurface"/>) coordinate space.</param>
    /// <param name="direction">The direction the arrowhead's own tip points toward, in that same local coordinate space - need not be unit length.</param>
    /// <param name="localToSurface">The connector's own resolved local-to-surface transform.</param>
    private static void PaintArrowhead(
        Surface surface, PptxArrowheadStyle style, PptxLineStyle lineStyle, Vector2 point, Vector2 direction, Matrix3x2 localToSurface)
    {
        if (direction == Vector2.Zero)
        {
            direction = Vector2.UnitX;
        }

        var arrowheadPath = PptxArrowheadGeometry.Build(style, lineStyle.WidthEmu);
        if (arrowheadPath.Subpaths.Count == 0)
        {
            return;
        }

        // Rotating/translating in the connector's own local space (before localToSurface is
        // applied) - rather than transforming direction/point into surface space first and
        // rotating there - keeps an arrowhead consistent with any non-uniform scale or flip
        // localToSurface itself carries, exactly like the connector's own stroked line outline
        // (also built and transformed in local space; see RenderConnector).
        var angle = MathF.Atan2(direction.Y, direction.X);
        var orientToSurface = Matrix3x2.CreateRotation(angle) * Matrix3x2.CreateTranslation(point) * localToSurface;
        var transformedArrowhead = arrowheadPath.Transform(orientToSurface);

        if (style.Kind == PptxArrowheadKind.Arrow)
        {
            // The "arrow" open-chevron kind is stroked with the connector's own line paint/width,
            // never dashed (an arrowhead should always read as a solid mark, even on a dashed
            // connector) - built directly from PptxArrowheadGeometry.Build's own open path rather
            // than ResolveStrokeOutline's usual "closed shape outline" path.
            var arrowheadLineStyle = lineStyle with { DashArray = null };
            var strokedArrowhead = ResolveStrokeOutline(arrowheadPath, arrowheadLineStyle, orientToSurface).Transform(orientToSurface);
            FillPaint(surface, strokedArrowhead, lineStyle.Paint);
        }
        else
        {
            FillPaint(surface, transformedArrowhead, lineStyle.Paint);
        }
    }
}
