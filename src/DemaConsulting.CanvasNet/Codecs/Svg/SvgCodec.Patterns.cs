// cspell:ignore linecap linejoin dasharray dashoffset miterlimit anchor xlink href
// cspell:ignore Glyf Loca
// cspell:ignore evenodd nonzero viewbox gradientunits gradienttransform spreadmethod
// cspell:ignore userspaceonuse objectboundingbox skewx skewy tspan
// cspell:ignore rasterizing unparseable rrggbb sizeless bbox moveto multiplicatively pillarbox SMIL uncatchable formedness
// cspell:ignore unblurred premult patternunits patterncontentunits patterntransform
// cspell:ignore aliceblue antiquewhite blanchedalmond blueviolet burlywood cadetblue cornflowerblue
// cspell:ignore cornsilk darkcyan darkgoldenrod darkgray darkgreen darkgrey darkkhaki darkmagenta
// cspell:ignore darkolivegreen darkorange darkorchid darkred darksalmon darkseagreen darkslateblue
// cspell:ignore darkslategray darkslategrey darkturquoise darkviolet deeppink deepskyblue dimgray
// cspell:ignore dimgrey dodgerblue floralwhite forestgreen gainsboro ghostwhite greenyellow hotpink
// cspell:ignore indianred lavenderblush lawngreen lemonchiffon lightcoral lightcyan
// cspell:ignore lightgoldenrodyellow lightgray lightgreen lightpink lightsalmon lightseagreen
// cspell:ignore lightskyblue lightslategray lightslategrey lightsteelblue lightyellow limegreen
// cspell:ignore mediumaquamarine mediumblue mediumorchid mediumpurple mediumseagreen mediumslateblue
// cspell:ignore mediumspringgreen mediumturquoise mediumvioletred midnightblue mintcream mistyrose
// cspell:ignore navajowhite oldlace olivedrab orangered palegoldenrod palegreen paleturquoise
// cspell:ignore palevioletred papayawhip peachpuff powderblue rebeccapurple rosybrown royalblue
// cspell:ignore saddlebrown sandybrown seagreen skyblue slateblue slategray slategrey springgreen
// cspell:ignore steelblue whitesmoke yellowgreen
using System.Numerics;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Codecs;

public static partial class SvgCodec
{
    // ================================================================================================
    // <pattern> paint-server resolution and application (Phase 3 of the SVG roadmap)
    // ================================================================================================
    //
    // A <pattern> paint is deliberately NOT modeled as a Gradient subtype: Gradient's own
    // constructor is private protected, closing the hierarchy to exactly LinearGradient/
    // RadialGradient (see Drawing/Gradient.cs), because GradientEvaluator/ScanlineRasterizer's own
    // Fill(Surface, Path, Gradient, ...) entry point pattern-matches exhaustively on those two
    // subtypes alone. Extending that closed hierarchy for pattern would also require threading
    // RenderContext/recursion state (id index, fonts, budgets, element depth) into the Drawing
    // namespace, which today has zero dependency on Codecs.Svg concepts. This codebase is also a
    // pure C# rasterizer with no native tiled/bitmap-shader primitive of any kind (confirmed: no
    // SkiaSharp reference anywhere in src/), so there is no existing precedent to reuse for
    // sampling a raster buffer with wrap-around addressing either. A <pattern> paint is therefore
    // implemented as its own, entirely manual, SvgCodec-layer per-pixel tile-sampling loop, confined
    // to this file - analogous to how <clipPath>/<mask> (Phase 2, SvgCodec.ClippingAndMasking.cs)
    // implement their own effects as manual per-pixel Surface operations rather than extending the
    // Drawing namespace.
    //
    // Explicitly out of scope, documented here as a single reference point (each rationale is
    // deliberately narrow so a future contributor can safely lift any one restriction in isolation):
    //
    //   - href-based template inheritance (mirroring the gradient <c>href</c> chain-walk in
    //     SvgCodec.Gradients.cs) resolves only the pattern's own CONTENT (its children to render
    //     into the tile) through the chain - x/y/width/height/patternUnits/patternContentUnits/
    //     patternTransform/viewBox/preserveAspectRatio are always read directly from the
    //     originally-referenced <pattern> element, never inherited through the href chain. This
    //     mirrors BuildGradient's own identically-documented "geometry/unit attributes are never
    //     inherited, only stops/content are" simplification.
    //   - No tile-buffer cache: a rendered tile buffer's own pixel size (and, when
    //     patternContentUnits="objectBoundingBox", its own content mapping) depends on the
    //     REFERENCING shape's own bounding box, so a naive per-pattern-element cache would be
    //     incorrect when the same <pattern> is referenced by differently-sized shapes - every
    //     pattern fill/stroke re-renders its own tile from scratch. Bounded by FilterWorkBudget
    //     regardless (see MaxPatternTileDimension/RenderPatternFill's remarks below), so this
    //     cannot become an unbounded-resource issue, only a documented perf simplification.
    //   - A pattern-reference cycle (a pattern's own content contains a shape whose own fill/stroke
    //     references that same pattern, directly or indirectly) is caught purely by the
    //     pre-existing MaxElementDepth recursion guard in RenderElement, since tile content renders
    //     through the ordinary recursive RenderElement walk with elementDepth + 1 - exactly the
    //     same reliance ApplyMask's own remarks document for a mask-reference cycle. No separate
    //     pattern-specific cycle/depth guard is implemented.
    //   - Tile sampling is nearest-neighbor only (matching this codec's existing "no bilinear
    //     resampling anywhere" convention); a rotated/skewed patternTransform therefore produces a
    //     tiled result with the same per-tile-pixel aliasing any other nearest-neighbor sampler
    //     would.

    /// <summary>
    ///     The maximum pixel-space dimension (either axis) a single rendered pattern tile buffer
    ///     may have, before <see cref="RenderPatternFill"/> tolerantly falls back to painting
    ///     nothing rather than allocating an oversized buffer.
    /// </summary>
    /// <remarks>
    ///     Set identically to <see cref="Surface.MaxDimension"/> - the same ceiling
    ///     <see cref="ConvertLocalRegionToPixelBounds"/> already enforces for every filter/mask
    ///     region pixel-size computation - reused here (rather than invented as an independent
    ///     value) because a pattern tile buffer is the exact same kind of resource (a
    ///     width-by-height pixel <see cref="Surface"/> allocation) those computations already
    ///     bound. This named constant exists purely for this file's own documentation and test
    ///     traceability: the ceiling is actually enforced by <see cref="ComputeTilePixelSize"/>'s
    ///     own reuse of <see cref="ConvertLocalRegionToPixelBounds"/>, which already returns
    ///     <see langword="null"/> once either axis would exceed <see cref="Surface.MaxDimension"/>.
    /// </remarks>
    private const int MaxPatternTileDimension = Surface.MaxDimension;

    /// <summary>
    ///     Resolves a raw <c>fill</c>/<c>stroke</c> presentation-attribute value (as read from
    ///     <see cref="RenderState.Fill"/>/<see cref="RenderState.Stroke"/>) to its referenced
    ///     <c>pattern</c> element, mirroring <see cref="ResolveClipPathElement"/>'s exact
    ///     <c>url(#id)</c>-parsing and dangling-reference/wrong-element-type tolerance.
    /// </summary>
    /// <param name="spec">The raw paint specification (<c>none</c>/color/<c>url(#id)</c>).</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <returns>
    ///     The referenced <c>pattern</c> element, or <see langword="null"/> if
    ///     <paramref name="spec"/> is not <c>url(#id)</c> syntax, the id is dangling, or the
    ///     resolved element is not literally a <c>pattern</c> - every one of these conditions is a
    ///     purely additive check performed <em>before</em> <see cref="ResolvePaint"/> is reached,
    ///     so falling through to <see langword="null"/> here leaves every existing solid-color/
    ///     gradient/<c>none</c>/dangling-reference call path completely unaffected.
    /// </returns>
    private static XElement? ResolvePatternElement(string spec, RenderContext context)
    {
        var trimmed = spec.Trim();
        if (!trimmed.StartsWith("url(", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var id = ExtractUrlId(trimmed);
        if (id == null || !context.IdIndex.TryGetValue(id, out var candidate))
        {
            return null;
        }

        return candidate.Name.LocalName == "pattern" ? candidate : null;
    }

    /// <summary>
    ///     Resolves a <c>pattern</c> element's effective content element - the element whose own
    ///     children are rendered into each tile - walking its <c>href</c>/<c>xlink:href</c>
    ///     template-inheritance chain (starting at <paramref name="start"/> itself) until an
    ///     element with at least one child is found. The result is cached per
    ///     <paramref name="start"/> element (see <see cref="RenderContext.PatternContentCache"/>),
    ///     mirroring <see cref="ResolveGradientStops"/>'s identical cache rationale: a pattern
    ///     referenced by many shapes has this chain walk performed only once per <c>Load</c> call.
    /// </summary>
    /// <param name="start">The <c>pattern</c> element originally referenced.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <returns>
    ///     The first chain element with at least one child (not merged across chain levels), or
    ///     <see langword="null"/> if the chain ends (a dangling/absent <c>href</c> target, or an
    ///     element with no children of its own) without ever finding one.
    /// </returns>
    /// <exception cref="InvalidDataException">Thrown when the chain revisits an element (a cycle).</exception>
    private static XElement? ResolvePatternContentElement(XElement start, RenderContext context)
    {
        if (context.PatternContentCache.TryGetValue(start, out var cached))
        {
            return cached;
        }

        var visited = new HashSet<XElement>();
        var current = start;
        while (visited.Add(current))
        {
            if (current.Elements().Any())
            {
                context.PatternContentCache[start] = current;
                return current;
            }

            var hrefId = GetHrefAttribute(current) is { } href ? ExtractFragmentId(href) : null;
            if (hrefId == null || !context.IdIndex.TryGetValue(hrefId, out var next))
            {
                // Cache the "no content found" terminal case too, so a dangling/cycle-free chain
                // with no content is not re-walked on every future reference to this same
                // starting element - mirrors ResolveGradientStops's identical empty-result caching
                context.PatternContentCache[start] = null;
                return null;
            }

            current = next;
        }

        throw new InvalidDataException("The pattern's href chain contains a cycle.");
    }

    /// <summary>
    ///     Parses a <c>patternTransform</c> attribute, in exactly the same function-list syntax as
    ///     the ordinary <c>transform</c>/<c>gradientTransform</c> attributes.
    /// </summary>
    /// <param name="element">The <c>pattern</c> element to inspect.</param>
    /// <returns>The combined transform, or <see cref="Matrix3x2.Identity"/> if absent or blank.</returns>
    /// <exception cref="InvalidDataException">Thrown when the attribute is present but malformed.</exception>
    private static Matrix3x2 ParsePatternTransform(XElement element) =>
        ParseTransformList((string?)element.Attribute("patternTransform"));

    /// <summary>
    ///     Renders <paramref name="patternElement"/> as a tiled paint, sampled per pixel across
    ///     <paramref name="outline"/>'s own painted-region pixel bounds, and composites the result
    ///     onto <paramref name="context"/>'s real surface - the single entry point
    ///     <see cref="RenderFill"/>/<see cref="RenderStroke"/> dispatch to instead of
    ///     <see cref="ResolvePaint"/>/<see cref="FillWithPaint"/> whenever a fill/stroke resolves to
    ///     a <c>pattern</c> reference (see <see cref="ResolvePatternElement"/>).
    /// </summary>
    /// <param name="patternElement">The resolved <c>pattern</c> element.</param>
    /// <param name="localPath">
    ///     The shape's own local-space outline (never the stroke outline), used as the
    ///     <c>objectBoundingBox</c> reference basis for both <c>patternUnits</c> and
    ///     <c>patternContentUnits</c> - mirroring <see cref="BuildGradient"/>'s identical choice to
    ///     always use the unstroked local path for both a fill and a stroke paint.
    /// </param>
    /// <param name="outline">
    ///     The already pixel-space-transformed outline actually being painted: the shape's own
    ///     transformed fill outline itself for a fill, or the stroke's own generated outline for a
    ///     stroke.
    /// </param>
    /// <param name="fillRule">The fill rule used to rasterize <paramref name="outline"/>'s own antialiased coverage.</param>
    /// <param name="alphaMultiplier">
    ///     The combined opacity multiplier (<c>fill-opacity</c>/<c>stroke-opacity</c> times the
    ///     cascaded <c>opacity</c>) applied uniformly to the whole sampled/composited result, via
    ///     <see cref="CompositeFilterResultOntoCanvas"/>.
    /// </param>
    /// <param name="elementTransform">The accumulated transform from the referencing element's own local space into pixel space.</param>
    /// <param name="state">The cascaded render state, supplying the current viewport size as the pattern content's own percentage-resolution basis.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="useDepth">The current <c>use</c>-reference nesting depth, forwarded to the tile content's own <c>RenderElement</c> walk.</param>
    /// <param name="elementDepth">
    ///     The current recursion depth, forwarded (incremented by one) to the tile content's own
    ///     <c>RenderElement</c> walk - the sole guard against a pattern-reference cycle, exactly
    ///     mirroring <see cref="ApplyMask"/>'s identical reliance (see this class's remarks).
    /// </param>
    /// <param name="markerDepth">The current <c>marker</c>-reference nesting depth, forwarded unchanged.</param>
    /// <param name="totalElements">The running total-rendered-elements count, forwarded to the tile content's own <c>RenderElement</c> walk.</param>
    /// <param name="workBudget">The shared geometry-parsing work budget, forwarded to the tile content's own <c>RenderElement</c> walk.</param>
    /// <param name="filterWorkBudget">
    ///     The shared cumulative filter-evaluation work budget (see <see cref="FilterWorkBudget"/>),
    ///     charged a nominal <c>1 * (tileArea + regionArea)</c> work unit before either offscreen
    ///     buffer is allocated - deliberately reusing this same budget class rather than
    ///     introducing a parallel one, per this codebase's established "one shared cumulative
    ///     ceiling bounds every offscreen-buffer-allocating effect" philosophy (see
    ///     <see cref="FilterWorkBudget"/>'s own remarks). A decline tolerantly falls back to
    ///     painting nothing, matching <see cref="ResolvePaint"/>'s own dangling-reference
    ///     tolerance - never a hard <see cref="InvalidDataException"/>.
    /// </param>
    /// <param name="boundsPrePassBudget">The shared cumulative bounds-pre-pass work budget, forwarded to the tile content's own <c>RenderElement</c> walk.</param>
    /// <remarks>
    ///     Tolerantly paints nothing (leaves <paramref name="outline"/>'s own region entirely
    ///     unpainted) for every one of the following conditions, none of which abort the whole
    ///     document: no resolvable content (<see cref="ResolvePatternContentElement"/> returns
    ///     <see langword="null"/>); a non-finite/non-positive <c>width</c>/<c>height"</c>; a
    ///     non-invertible/non-finite composed grid transform (<c>patternTransform * bboxMap *
    ///     elementTransform</c>, mirroring <see cref="BuildGradient"/>'s identical
    ///     <see cref="IsFiniteTransform"/> check); a degenerate/oversized painted-region or
    ///     tile-buffer pixel size (see <see cref="MaxPatternTileDimension"/>); or a declined
    ///     <paramref name="filterWorkBudget"/> charge.
    /// </remarks>
    private static void RenderPatternFill(
        XElement patternElement,
        Path localPath,
        Path outline,
        FillRule fillRule,
        float alphaMultiplier,
        Matrix3x2 elementTransform,
        RenderState state,
        RenderContext context,
        int useDepth,
        int elementDepth,
        int markerDepth,
        ref int totalElements,
        GeometryWorkBudget workBudget,
        FilterWorkBudget filterWorkBudget,
        BoundsPrePassWorkBudget boundsPrePassBudget)
    {
        var contentElement = ResolvePatternContentElement(patternElement, context);
        if (contentElement == null)
        {
            return;
        }

        // The painted region: the actual outline being filled/stroked, already in pixel space, so
        // no further transform is needed to compute its own integer pixel bounds - reusing
        // ConvertLocalRegionToPixelBounds's shared finiteness/MaxCoordinateMagnitude/
        // Surface.MaxDimension validation with an identity transform
        var region = ConvertLocalRegionToPixelBounds(outline.GetBounds(), Matrix3x2.Identity);
        if (region == null)
        {
            return;
        }

        var (pixelX, pixelY, pixelWidth, pixelHeight) = region.Value;

        var referenceBounds = localPath.GetBounds();
        var patternUnitsIsUserSpace = string.Equals(
            (string?)patternElement.Attribute("patternUnits"), "userSpaceOnUse", StringComparison.Ordinal);
        var placementTransform = patternUnitsIsUserSpace ? Matrix3x2.Identity : ComputeObjectBoundingBoxMap(referenceBounds);

        var tileX = GetGradientCoordinateOrDefault(patternElement, "x", 0f);
        var tileY = GetGradientCoordinateOrDefault(patternElement, "y", 0f);
        var tileWidth = GetGradientCoordinateOrDefault(patternElement, "width", 0f);
        var tileHeight = GetGradientCoordinateOrDefault(patternElement, "height", 0f);
        if (!float.IsFinite(tileWidth) || !float.IsFinite(tileHeight) || tileWidth <= 0f || tileHeight <= 0f)
        {
            return;
        }

        var gridTransform = ParsePatternTransform(patternElement) * placementTransform * elementTransform;
        if (!IsFiniteTransform(gridTransform) || !Matrix3x2.Invert(gridTransform, out var inverseGridTransform))
        {
            return;
        }

        var tilePixelSize = ComputeTilePixelSize(tileX, tileY, tileWidth, tileHeight, gridTransform);
        if (tilePixelSize == null)
        {
            return;
        }

        var (tilePixelWidth, tilePixelHeight) = tilePixelSize.Value;

        // Charge this application's own tile-buffer-plus-region-buffer allocation cost against the
        // shared cumulative budget before allocating either - see this method's remarks
        var workUnits = ((long)tilePixelWidth * tilePixelHeight) + ((long)pixelWidth * pixelHeight);
        if (!filterWorkBudget.TryCharge(workUnits))
        {
            return;
        }

        if (!TryComputeContentRenderTransform(
                patternElement, referenceBounds, placementTransform, tileX, tileY, tileWidth, tileHeight,
                tilePixelWidth, tilePixelHeight, state, out var contentRenderTransform, out var contentViewportWidth, out var contentViewportHeight))
        {
            return;
        }

        // Render the resolved content's children into a fresh tile buffer via the ordinary
        // recursive RenderElement walk - see this class's remarks for why elementDepth + 1 is this
        // codec's sole guard against a pattern-reference cycle
        var tileBuffer = new Surface(tilePixelWidth, tilePixelHeight);
        var tileContext = context with { Surface = tileBuffer };
        var tileBaseState = RenderState.Initial with
        {
            ViewportWidth = contentViewportWidth,
            ViewportHeight = contentViewportHeight
        };
        foreach (var child in contentElement.Elements())
        {
            RenderElement(
                child, tileBaseState, contentRenderTransform, tileContext, useDepth, elementDepth + 1, markerDepth,
                ref totalElements, workBudget, filterWorkBudget, boundsPrePassBudget);
        }

        // Build the antialiased fill/stroke coverage for the actual outline being painted, in a
        // region-sized buffer aligned to the region's own pixel origin - mirrors ApplyCoverageClip's
        // approach, generalized here to a fractional (not merely binary) coverage-multiply since
        // this buffer also carries the sampled tile's own color, not only its alpha
        var coverage = new Surface(pixelWidth, pixelHeight);
        var outlineInRegion = TransformPath(outline, Matrix3x2.CreateTranslation(-pixelX, -pixelY));
        PathFiller.Fill(coverage, outlineInRegion, OpaqueWhite, fillRule);

        var finalSurface = SampleTileIntoRegion(
            tileBuffer, coverage, pixelX, pixelY, pixelWidth, pixelHeight, tileX, tileY, tileWidth, tileHeight,
            inverseGridTransform);

        CompositeFilterResultOntoCanvas(finalSurface, pixelX, pixelY, context.Surface, alphaMultiplier);
    }

    /// <summary>
    ///     Computes one pattern tile's own rendered-buffer pixel size - the AABB, under the
    ///     composed grid transform, of the tile rectangle <c>(x, y, width, height)</c> - reusing
    ///     <see cref="ConvertLocalRegionToPixelBounds"/>'s shared finiteness/
    ///     <see cref="Surface.MaxDimension"/> validation (see <see cref="MaxPatternTileDimension"/>).
    /// </summary>
    /// <param name="tileX">The tile rectangle's own <c>x</c>, in grid space (see <see cref="RenderPatternFill"/>'s remarks).</param>
    /// <param name="tileY">The tile rectangle's own <c>y</c>, in grid space.</param>
    /// <param name="tileWidth">The tile rectangle's own <c>width</c>, in grid space. Must already be validated finite and positive.</param>
    /// <param name="tileHeight">The tile rectangle's own <c>height</c>, in grid space. Must already be validated finite and positive.</param>
    /// <param name="gridTransform">The composed <c>patternTransform * bboxMap * elementTransform</c> grid-space-to-pixel-space transform.</param>
    /// <returns>
    ///     The tile buffer's own <c>(Width, Height)</c> pixel size, or <see langword="null"/> if it
    ///     resolves to a non-finite, non-positive, or <see cref="Surface.MaxDimension"/>-exceeding
    ///     size on either axis.
    /// </returns>
    /// <remarks>
    ///     Under a rotated/skewed <paramref name="gridTransform"/>, this AABB is a conservative
    ///     over-approximation of the tile's own true on-screen parallelogram footprint - a
    ///     deliberate simplification (a slightly larger, but never smaller, tile buffer than
    ///     strictly necessary) rather than inventing new oriented-bounding-box sizing logic solely
    ///     for this one call site.
    /// </remarks>
    private static (int Width, int Height)? ComputeTilePixelSize(
        float tileX, float tileY, float tileWidth, float tileHeight, Matrix3x2 gridTransform)
    {
        var tileRegion = ConvertLocalRegionToPixelBounds(new Rect(tileX, tileY, tileWidth, tileHeight), gridTransform);
        if (tileRegion == null)
        {
            return null;
        }

        // Defense-in-depth restatement of this method's own remarks: ConvertLocalRegionToPixelBounds
        // already enforces this exact ceiling on both axes internally, so these assertions can
        // never actually fail - they exist purely to keep MaxPatternTileDimension a live,
        // enforced invariant rather than a stray unreferenced constant.
        System.Diagnostics.Debug.Assert(
            tileRegion.Value.Width <= MaxPatternTileDimension, "Tile pixel width must not exceed MaxPatternTileDimension.");
        System.Diagnostics.Debug.Assert(
            tileRegion.Value.Height <= MaxPatternTileDimension, "Tile pixel height must not exceed MaxPatternTileDimension.");

        return (tileRegion.Value.Width, tileRegion.Value.Height);
    }

    /// <summary>
    ///     Computes the transform mapping a pattern's own content children's local coordinates into
    ///     the tile buffer's own pixel space <c>[0, tilePixelWidth) x [0, tilePixelHeight)</c>,
    ///     honoring <c>viewBox</c>/<c>preserveAspectRatio</c> precedence over
    ///     <c>patternContentUnits</c> per the SVG specification (see this method's remarks).
    /// </summary>
    /// <param name="patternElement">The resolved <c>pattern</c> element (never a chain target - see <see cref="ResolvePatternContentElement"/>).</param>
    /// <param name="referenceBounds">The referencing shape's own local-space outline bounds.</param>
    /// <param name="placementTransform">
    ///     The <c>patternUnits</c>-derived bounding-box map (or <see cref="Matrix3x2.Identity"/> for
    ///     <c>userSpaceOnUse</c>) already computed by <see cref="RenderPatternFill"/> - inverted
    ///     here to map back from the tile's own grid-space placement into content-mapping space, so
    ///     <c>patternContentUnits</c> can be resolved fully independently of <c>patternUnits</c>
    ///     (see this method's remarks).
    /// </param>
    /// <param name="tileX">The tile rectangle's own <c>x</c>, in grid space.</param>
    /// <param name="tileY">The tile rectangle's own <c>y</c>, in grid space.</param>
    /// <param name="tileWidth">The tile rectangle's own <c>width</c>, in grid space. Already validated positive.</param>
    /// <param name="tileHeight">The tile rectangle's own <c>height</c>, in grid space. Already validated positive.</param>
    /// <param name="tilePixelWidth">The tile buffer's own pixel width (see <see cref="ComputeTilePixelSize"/>).</param>
    /// <param name="tilePixelHeight">The tile buffer's own pixel height.</param>
    /// <param name="state">The referencing shape's own cascaded render state, supplying the fallback (no-<c>viewBox</c>) content viewport basis.</param>
    /// <param name="contentRenderTransform">The resulting transform, or <see cref="Matrix3x2.Identity"/> if this method returns <see langword="false"/>.</param>
    /// <param name="contentViewportWidth">The resolved content viewport width, for the tile content's own fresh render state.</param>
    /// <param name="contentViewportHeight">The resolved content viewport height.</param>
    /// <returns>
    ///     <see langword="false"/> if <paramref name="placementTransform"/> is not invertible (only
    ///     possible for a degenerate <paramref name="referenceBounds"/>, which
    ///     <see cref="ComputeObjectBoundingBoxMap(Rect)"/> already tolerates by returning
    ///     <see cref="Matrix3x2.Identity"/> - always invertible - so this is defense-in-depth
    ///     rather than a reachable condition today) or the resulting composed transform is
    ///     non-finite; otherwise <see langword="true"/>.
    /// </returns>
    /// <remarks>
    ///     Per the SVG specification, a <c>viewBox</c> on <paramref name="patternElement"/> (when
    ///     present) takes precedence over <c>patternContentUnits</c>, which is then ignored
    ///     entirely - <see cref="ComputePreserveAspectRatioFit"/> (already used identically for
    ///     <c>&lt;svg&gt;</c>/<c>&lt;symbol&gt;</c>/<c>&lt;marker&gt;</c> viewBox fitting) maps the
    ///     <c>viewBox</c> content box directly onto the tile buffer's own
    ///     <paramref name="tilePixelWidth"/> x <paramref name="tilePixelHeight"/> pixel viewport.
    ///     <para>
    ///     Without a <c>viewBox</c>, <c>patternContentUnits</c> (default <c>userSpaceOnUse</c>) is
    ///     resolved independently of <c>patternUnits</c>: a content child's own literal coordinate
    ///     is first mapped via its own bounding-box map (<see cref="ComputeObjectBoundingBoxMap(Rect)"/>
    ///     against the same <paramref name="referenceBounds"/>, for <c>objectBoundingBox</c>, or
    ///     <see cref="Matrix3x2.Identity"/> for <c>userSpaceOnUse</c>) into the same "real local
    ///     space" the tile's own placement occupies, then back through
    ///     <paramref name="placementTransform"/>'s own inverse into grid space, then finally into
    ///     the tile buffer's own pixel space via a plain translate-then-scale from the tile
    ///     rectangle's own grid-space extent. This three-step composition is what allows
    ///     <c>patternUnits="userSpaceOnUse"</c> combined with <c>patternContentUnits="objectBoundingBox"</c>
    ///     (or vice versa) to each resolve correctly and independently, exactly as the specification
    ///     requires, rather than conflating the two attributes' own separate coordinate systems.
    ///     </para>
    /// </remarks>
    private static bool TryComputeContentRenderTransform(
        XElement patternElement,
        Rect referenceBounds,
        Matrix3x2 placementTransform,
        float tileX,
        float tileY,
        float tileWidth,
        float tileHeight,
        int tilePixelWidth,
        int tilePixelHeight,
        RenderState state,
        out Matrix3x2 contentRenderTransform,
        out float contentViewportWidth,
        out float contentViewportHeight)
    {
        contentRenderTransform = Matrix3x2.Identity;
        contentViewportWidth = state.ViewportWidth;
        contentViewportHeight = state.ViewportHeight;

        var viewBox = ParseViewBox((string?)patternElement.Attribute("viewBox"));
        if (viewBox.HasValue)
        {
            var preserveAspectRatio = GetPreserveAspectRatio(patternElement);
            contentRenderTransform = ComputePreserveAspectRatioFit(
                viewBox.Value.Origin, viewBox.Value.Size, tilePixelWidth, tilePixelHeight, preserveAspectRatio);
            contentViewportWidth = viewBox.Value.Size.X;
            contentViewportHeight = viewBox.Value.Size.Y;
            return IsFiniteTransform(contentRenderTransform);
        }

        if (!Matrix3x2.Invert(placementTransform, out var inversePlacementTransform))
        {
            return false;
        }

        var contentUnitsIsObjectBoundingBox = string.Equals(
            (string?)patternElement.Attribute("patternContentUnits"), "objectBoundingBox", StringComparison.Ordinal);
        var contentUnitTransform = contentUnitsIsObjectBoundingBox
            ? ComputeObjectBoundingBoxMap(referenceBounds)
            : Matrix3x2.Identity;

        var tileToBuffer = Matrix3x2.CreateTranslation(-tileX, -tileY)
            * Matrix3x2.CreateScale(tilePixelWidth / tileWidth, tilePixelHeight / tileHeight);

        contentRenderTransform = contentUnitTransform * inversePlacementTransform * tileToBuffer;
        return IsFiniteTransform(contentRenderTransform);
    }

    /// <summary>
    ///     Samples <paramref name="tileBuffer"/> into a fresh region-sized buffer, one pixel at a
    ///     time: each destination pixel is inverse-mapped through <paramref name="inverseGridTransform"/>
    ///     back into grid space, wrapped (modulo) by the tile's own <paramref name="tileWidth"/>/
    ///     <paramref name="tileHeight"/>, rescaled into <paramref name="tileBuffer"/>'s own pixel
    ///     coordinates, and sampled nearest-neighbor - then that sample's own alpha is multiplied by
    ///     <paramref name="coverage"/>'s own alpha at the same destination pixel (a fractional,
    ///     antialiased generalization of <see cref="ApplyCoverageClip"/>'s binary coverage-multiply).
    /// </summary>
    /// <param name="tileBuffer">The already fully rendered single-tile content buffer.</param>
    /// <param name="coverage">The region-sized antialiased fill/stroke coverage buffer (see <see cref="RenderPatternFill"/>).</param>
    /// <param name="pixelX">The painted region's own pixel-space X origin.</param>
    /// <param name="pixelY">The painted region's own pixel-space Y origin.</param>
    /// <param name="pixelWidth">The painted region's own pixel width.</param>
    /// <param name="pixelHeight">The painted region's own pixel height.</param>
    /// <param name="tileX">The tile rectangle's own <c>x</c>, in grid space.</param>
    /// <param name="tileY">The tile rectangle's own <c>y</c>, in grid space.</param>
    /// <param name="tileWidth">The tile rectangle's own <c>width</c>, in grid space. Already validated positive.</param>
    /// <param name="tileHeight">The tile rectangle's own <c>height</c>, in grid space. Already validated positive.</param>
    /// <param name="inverseGridTransform">The inverse of the composed grid-space-to-pixel-space transform.</param>
    /// <returns>A new region-sized buffer, ready for <see cref="CompositeFilterResultOntoCanvas"/>.</returns>
    private static Surface SampleTileIntoRegion(
        Surface tileBuffer,
        Surface coverage,
        int pixelX,
        int pixelY,
        int pixelWidth,
        int pixelHeight,
        float tileX,
        float tileY,
        float tileWidth,
        float tileHeight,
        Matrix3x2 inverseGridTransform)
    {
        var result = new Surface(pixelWidth, pixelHeight);
        var tilePixelWidth = tileBuffer.Width;
        var tilePixelHeight = tileBuffer.Height;

        for (var row = 0; row < pixelHeight; row++)
        {
            var coverageRow = coverage.GetRowSpan(row);
            var resultRow = result.GetRowSpan(row);

            for (var col = 0; col < pixelWidth; col++)
            {
                var coverageAlpha = coverageRow[col].A;
                if (coverageAlpha == 0)
                {
                    // No fill/stroke coverage at this pixel at all - leave it fully transparent,
                    // exactly like ApplyCoverageClip's identical zero-coverage skip
                    continue;
                }

                // Sample at the destination pixel's own center, matching this codec's existing
                // pixel-center sampling convention elsewhere (for example gradient evaluation)
                var canvasPoint = new Vector2(pixelX + col + 0.5f, pixelY + row + 0.5f);
                var gridPoint = Vector2.Transform(canvasPoint, inverseGridTransform);
                if (!float.IsFinite(gridPoint.X) || !float.IsFinite(gridPoint.Y))
                {
                    continue;
                }

                var wrappedX = WrapCoordinate(gridPoint.X - tileX, tileWidth);
                var wrappedY = WrapCoordinate(gridPoint.Y - tileY, tileHeight);

                // The fraction of the tile a wrapped grid-space coordinate falls at is identical
                // whether measured in grid space or in the tile buffer's own pixel space - both
                // are related by a single uniform translate-then-scale (see
                // TryComputeContentRenderTransform's remarks) - so no further transform is needed
                // beyond this direct fraction rescale
                var bufferX = Math.Clamp((int)(wrappedX / tileWidth * tilePixelWidth), 0, tilePixelWidth - 1);
                var bufferY = Math.Clamp((int)(wrappedY / tileHeight * tilePixelHeight), 0, tilePixelHeight - 1);

                var sample = tileBuffer.GetRowSpan(bufferY)[bufferX];
                var combinedAlpha = (byte)Math.Clamp(MathF.Round(sample.A * (coverageAlpha / 255f)), 0, 255);
                resultRow[col] = new Rgba32(sample.R, sample.G, sample.B, combinedAlpha);
            }
        }

        return result;
    }

    /// <summary>
    ///     Wraps <paramref name="value"/> into <c>[0, period)</c>, the modulo-wrap core of
    ///     <see cref="SampleTileIntoRegion"/>'s per-pixel tile-repetition sampling.
    /// </summary>
    /// <param name="value">The value to wrap - can be any finite value, including negative.</param>
    /// <param name="period">The wrap period. Must be finite and strictly positive.</param>
    /// <returns>
    ///     The equivalent value in <c>[0, period)</c>. C#'s <c>%</c> operator keeps the sign of its
    ///     left operand (unlike a true mathematical modulo), so a negative <paramref name="value"/>
    ///     is re-added by <paramref name="period"/> once to land back in range.
    /// </returns>
    private static float WrapCoordinate(float value, float period)
    {
        var wrapped = value % period;
        return wrapped < 0f ? wrapped + period : wrapped;
    }
}
