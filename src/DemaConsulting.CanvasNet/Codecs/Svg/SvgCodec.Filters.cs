// cspell:ignore linecap linejoin dasharray dashoffset miterlimit anchor xlink href
// cspell:ignore Glyf Loca
// cspell:ignore evenodd nonzero viewbox gradientunits gradienttransform spreadmethod
// cspell:ignore userspaceonuse objectboundingbox skewx skewy tspan
// cspell:ignore rasterizing unparseable rrggbb sizeless bbox moveto multiplicatively pillarbox SMIL uncatchable formedness
// cspell:ignore unblurred premult
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
using System.Globalization;
using System.Numerics;
using System.Xml;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Codecs;

public static partial class SvgCodec
{
    // ================================================================================================
    // Filter rendering
    // ================================================================================================

    /// <summary>
    ///     Identifies the four <c>feComposite</c> Porter-Duff operators (other than the default
    ///     <c>over</c>, which reuses <see cref="Surface.CompositeOver(Surface)"/> directly) that
    ///     require the dedicated per-pixel blend helper <see cref="CompositeFeOperator"/>.
    /// </summary>
    private enum FeCompositeOperator
    {
        /// <summary>Keeps the foreground only where the background has coverage.</summary>
        In,

        /// <summary>Keeps the foreground only where the background has no coverage.</summary>
        Out,

        /// <summary>Keeps the foreground over the background, but only where the background has coverage.</summary>
        Atop,

        /// <summary>Keeps each input only where the other does not have coverage.</summary>
        Xor
    }

    /// <summary>
    ///     Renders <paramref name="localPath"/> through <paramref name="element"/>'s own <c>filter</c>
    ///     presentation attribute, if any, otherwise through the ordinary unfiltered
    ///     <see cref="RenderShape"/> pipeline.
    /// </summary>
    /// <param name="element">
    ///     The originating element (a shape, or a <c>text</c> element whose already-laid-out
    ///     glyph-run outline is passed as <paramref name="localPath"/>), whose own <c>filter</c>
    ///     attribute is read directly - <c>filter</c> does not cascade through <see cref="RenderState"/>,
    ///     unlike <c>fill</c>/<c>stroke</c>/marker specifications, per CSS/SVG semantics.
    /// </param>
    /// <param name="localPath">The shape's (or glyph run's) already-built local-space outline.</param>
    /// <param name="state">The cascaded render state.</param>
    /// <param name="transform">The accumulated transform from local space into pixel space.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="filterWorkBudget">
    ///     The shared cumulative filter-evaluation work budget, forwarded to
    ///     <see cref="RenderFilteredShape"/>.
    /// </param>
    /// <param name="suppressFilter">
    ///     <see langword="true"/> when <paramref name="element"/> is being rendered as part of a
    ///     <c>marker</c> element's own content (propagated from <c>RenderElement</c>'s
    ///     <c>markerDepth &gt; 0</c>) - <paramref name="element"/>'s own <c>filter</c> attribute is
    ///     then never resolved/evaluated at all, regardless of what it references, per the
    ///     documented "filters on marker content have no effect" scope decision (see this method's
    ///     remarks).
    /// </param>
    /// <remarks>
    ///     A <c>filter</c> value of <c>none</c>/absent/not <c>url(#id)</c> syntax, a dangling id,
    ///     or an id resolving to an element not literally named <c>filter</c> are all tolerated by
    ///     rendering <paramref name="localPath"/> normally through <see cref="RenderShape"/> - the
    ///     same dangling-reference tolerance convention as <see cref="ResolvePaint"/> and
    ///     <see cref="ResolveMarkerElement"/>. This method itself handles only one shape (or glyph
    ///     run)'s own <c>filter</c> attribute; a <c>g</c>/<c>symbol</c>/<c>use</c> element's own
    ///     <c>filter</c> attribute (applying to its whole subtree as one filtered unit) is instead
    ///     handled by <see cref="RenderFilteredGroup"/>, dispatched directly from
    ///     <see cref="RenderElement"/>'s <c>"g"</c>/<c>"symbol"</c> case and from
    ///     <see cref="RenderUse"/> - this method is never itself invoked for those container
    ///     elements. A <c>filter</c> attribute is never applied to a shape's own marker content
    ///     either way (markers always render directly onto <paramref name="context"/>'s surface,
    ///     unaffected by the referencing shape's own <c>filter</c>) - see
    ///     <see cref="RenderFilteredShape"/>'s remarks for the full filter evaluation pipeline.
    /// </remarks>
    private static void RenderShapeWithFilter(XElement element, Path localPath, RenderState state, Matrix3x2 transform, RenderContext context, FilterWorkBudget filterWorkBudget, bool suppressFilter = false)
    {
        var filterElement = suppressFilter ? null : ResolveFilterElement(element, context);
        if (filterElement == null)
        {
            RenderShape(localPath, state, transform, context);
            return;
        }

        RenderFilteredShape(filterElement, localPath, state, transform, context, filterWorkBudget);
    }

    /// <summary>
    ///     Resolves <paramref name="element"/>'s own <c>filter</c> presentation attribute
    ///     (<c>url(#id)</c>) to its referenced <c>filter</c> element, reusing the exact same
    ///     <c>url(#id)</c>-parsing and dangling-reference tolerance as <see cref="ResolvePaint"/>/
    ///     <see cref="ResolveMarkerElement"/>.
    /// </summary>
    /// <param name="element">The element whose own <c>filter</c> attribute is read.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <returns>
    ///     The referenced <c>filter</c> element, or <see langword="null"/> if the attribute is
    ///     absent/blank, not <c>url(#id)</c> syntax, the id is dangling, or the resolved element is
    ///     not literally a <c>filter</c>.
    /// </returns>
    private static XElement? ResolveFilterElement(XElement element, RenderContext context)
    {
        var spec = (string?)element.Attribute("filter");
        if (string.IsNullOrWhiteSpace(spec))
        {
            return null;
        }

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

        return candidate.Name.LocalName == "filter" ? candidate : null;
    }

    /// <summary>
    ///     Renders <paramref name="localPath"/> into a temporary, filter-region-sized offscreen
    ///     <see cref="Surface"/> (<c>SourceGraphic</c>), evaluates <paramref name="filterElement"/>'s
    ///     own primitive chain against it, then composites the resulting buffer onto
    ///     <paramref name="context"/>'s real surface at the filter region's pixel position.
    /// </summary>
    /// <param name="filterElement">The resolved <c>filter</c> element.</param>
    /// <param name="localPath">The shape's (or glyph run's) already-built local-space outline.</param>
    /// <param name="state">The cascaded render state.</param>
    /// <param name="transform">The accumulated transform from local space into pixel space.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="filterWorkBudget">
    ///     The shared cumulative filter-evaluation work budget (see <see cref="FilterWorkBudget"/>),
    ///     charged with this filter application's own region-weighted work unit immediately before
    ///     <c>SourceGraphic</c> is allocated, so a filter reused across many shapes cannot bypass
    ///     the resource-safety bound <see cref="MaxFilterPrimitiveWorkUnits"/> alone provides for a
    ///     single filter application.
    /// </param>
    /// <remarks>
    ///     If the filter region cannot be computed (an empty/degenerate local bounding box), or its
    ///     pixel-space size is non-finite, non-positive, or exceeds <see cref="Surface.MaxDimension"/>
    ///     on either axis, the whole filter effect is tolerantly skipped - <paramref name="localPath"/>
    ///     renders exactly as if <c>filter</c> were absent - rather than attempting to clamp and
    ///     still render at a smaller, silently mis-positioned region. See
    ///     <see cref="ComputeFilterRegionPixelBounds(XElement, Path, RenderState, Matrix3x2)"/> for the region computation itself.
    ///     <para>
    ///     The <c>SourceGraphic</c> buffer is produced by re-entering <see cref="RenderShape"/>
    ///     against a temporary <see cref="RenderContext"/> (<c>context with { Surface = ... }</c>,
    ///     correctly sharing <paramref name="context"/>'s own <see cref="RenderContext.GradientStopCache"/>)
    ///     and a transform translated so the region's own pixel origin lands at the temporary
    ///     surface's local <c>(0, 0)</c>. The final filtered buffer is composited back using
    ///     <see cref="Surface.CompositeOverSpan(int, int, ReadOnlySpan{float}, ReadOnlySpan{Rgba32})"/>
    ///     - the existing offset-aware compositing primitive - one row at a time, clipped to
    ///     <paramref name="context"/>'s own surface bounds, rather than any new per-pixel blending
    ///     math.
    ///     </para>
    ///     <para>
    ///     A filter chain whose own <c>fe*</c> primitive count or primitive-count-times-region-area
    ///     work exceeds <see cref="MaxFilterPrimitivesPerFilter"/>/<see cref="MaxFilterPrimitiveWorkUnits"/>
    ///     (see <see cref="IsFilterPrimitiveWorkWithinBudget"/>) is tolerantly skipped identically -
    ///     checked before <c>SourceGraphic</c> is even allocated, so no per-primitive work (buffer
    ///     allocation, blur passes, compositing) ever begins for a rejected chain. A <c>feMerge</c>
    ///     primitive is charged as its own <c>feMergeNode</c> child count (at least 1) rather than a
    ///     flat 1, because <see cref="ApplyFeMerge"/> performs one full-surface
    ///     <see cref="Surface.CompositeOver(Surface)"/> per <c>feMergeNode</c> - a flat charge would
    ///     let a pathologically large <c>feMerge</c> bypass this budget while still doing
    ///     O(node-count &#215; region-area) work.
    ///     </para>
    ///     <para>
    ///     A <c>filter</c> element with zero primitive children (a zero work-unit count from
    ///     <see cref="CountFilterPrimitiveWorkUnits"/>) is skipped identically to an out-of-budget
    ///     chain - checked in the same guard, before <c>SourceGraphic</c> is allocated - because a
    ///     filter with no primitives to evaluate can never change the rendered output, regardless of
    ///     how large its filter region is; treating it as "budget OK" would still allocate a
    ///     potentially enormous temporary surface just to hand it back unchanged.
    ///     </para>
    ///     <para>
    ///     Once this filter application's own region-weighted work unit (identical to the value
    ///     checked against <see cref="MaxFilterPrimitiveWorkUnits"/> above) would push
    ///     <paramref name="filterWorkBudget"/>'s running cumulative total past
    ///     <see cref="FilterWorkBudget"/>'s own fixed ceiling, this filter application is
    ///     tolerantly skipped identically to every other case above - the same unfiltered-fallback
    ///     pattern, never a thrown exception, so a document that legitimately reuses one filter
    ///     across more shapes than the cumulative budget allows still finishes rendering, simply
    ///     with the excess shapes rendered unfiltered rather than the whole <c>Load</c> call
    ///     aborting.
    ///     </para>
    ///     <para>
    ///     Per SVG semantics, an element's own <c>opacity</c> applies to the filtered result as a
    ///     whole, not to the pre-filter source paint: <c>SourceGraphic</c> is rendered with a copy
    ///     of <paramref name="state"/> whose <see cref="RenderState.Opacity"/> is forced to
    ///     <c>1.0</c>, so the filter chain (for example a <c>feFlood</c>) always evaluates against
    ///     a fully-opaque source, and <paramref name="state"/>'s own (possibly cascaded) opacity is
    ///     applied exactly once, afterward, via <see cref="CompositeFilterResultOntoCanvas"/>'s
    ///     coverage argument - the same per-pixel coverage-multiplier mechanism
    ///     <see cref="Surface.CompositeOverSpan(int, int, ReadOnlySpan{float}, ReadOnlySpan{Rgba32})"/>
    ///     already uses everywhere else in this codec.
    ///     </para>
    /// </remarks>
    private static void RenderFilteredShape(XElement filterElement, Path localPath, RenderState state, Matrix3x2 transform, RenderContext context, FilterWorkBudget filterWorkBudget)
    {
        var region = ComputeFilterRegionPixelBounds(filterElement, localPath, state, transform);
        if (region == null)
        {
            RenderShape(localPath, state, transform, context);
            return;
        }

        var (pixelX, pixelY, pixelWidth, pixelHeight) = region.Value;

        // A filter with zero primitive children can never change the rendered output - it has
        // nothing to evaluate - so it is tolerantly treated exactly like an already-degenerate/
        // out-of-budget filter (unfiltered fallback) rather than falling through to allocate a
        // potentially enormous SourceGraphic surface just to hand it back unchanged
        var primitiveCount = CountFilterPrimitiveWorkUnits(filterElement);
        if (primitiveCount == 0 || !IsFilterPrimitiveWorkWithinBudget(primitiveCount, pixelWidth, pixelHeight))
        {
            RenderShape(localPath, state, transform, context);
            return;
        }

        // Charge this filter application's own region-weighted work unit against the cumulative,
        // per-Load-call budget (see FilterWorkBudget's remarks) before allocating SourceGraphic -
        // this is the guard that bounds a single filter definition referenced by many shapes,
        // complementing IsFilterPrimitiveWorkWithinBudget's per-application-only ceiling above
        var filterWorkUnits = (long)primitiveCount * pixelWidth * pixelHeight;
        if (!filterWorkBudget.TryCharge(filterWorkUnits))
        {
            RenderShape(localPath, state, transform, context);
            return;
        }

        var sourceGraphic = new Surface(pixelWidth, pixelHeight);
        var localToTemp = transform * Matrix3x2.CreateTranslation(-pixelX, -pixelY);
        var tempContext = context with { Surface = sourceGraphic };

        // Render SourceGraphic fully opaque (Opacity forced to 1.0) rather than with the
        // element's own cascaded opacity - the filter chain must evaluate against an unmodified
        // source, and the element's opacity is instead applied exactly once, afterward, when the
        // filtered result is composited onto the real canvas below (see this method's remarks)
        var opaqueState = state with { Opacity = 1f };
        RenderShape(localPath, opaqueState, localToTemp, tempContext);

        var finalSurface = EvaluateFilterChain(filterElement, sourceGraphic, transform);

        CompositeFilterResultOntoCanvas(finalSurface, pixelX, pixelY, context.Surface, state.Opacity);
    }

    /// <summary>
    ///     Computes the union of <paramref name="element"/>'s own subtree painted-content bounds,
    ///     expressed in the local space that <paramref name="relativeTransform"/> maps <em>from</em>
    ///     (i.e. the caller's chosen reference frame, not necessarily pixel space) - a bounds-only
    ///     mirror of <see cref="RenderElement"/>'s dispatch switch, reusing the same
    ///     <c>Build*Path</c>/<see cref="BuildGlyphRunPath"/> helpers and <see cref="ExpandBoundsForStroke"/>
    ///     instead of actually rendering anything. Used exclusively by
    ///     <see cref="RenderFilteredGroup"/> to size a filtered group's own <c>SourceGraphic</c>
    ///     buffer before any of its content is rendered.
    /// </summary>
    /// <param name="element">The element whose own subtree bounds are computed.</param>
    /// <param name="parentState">The inherited render state from the parent element.</param>
    /// <param name="relativeTransform">
    ///     The accumulated transform from <paramref name="element"/>'s parent's own reference
    ///     frame into the caller's chosen reference frame. <see cref="RenderFilteredGroup"/> always
    ///     starts this at <see cref="Matrix3x2.Identity"/> for a filtered group's own direct
    ///     children, so the bounds this method returns stay in the filtered group's own local
    ///     space - deliberately <em>not</em> composed with any ancestor's accumulated pixel-space
    ///     transform - exactly mirroring how a single shape's own <c>localPath.GetBounds()</c>
    ///     excludes every ancestor transform up to and including its own.
    /// </param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="useDepth">
    ///     The current <c>use</c>-reference nesting depth, enforcing the same
    ///     <see cref="MaxUseDepth"/> ceiling <see cref="RenderUse"/> itself enforces.
    /// </param>
    /// <param name="elementDepth">
    ///     The current recursion depth, sharing the same <see cref="MaxElementDepth"/> ceiling
    ///     <see cref="RenderElement"/> itself enforces - this bounds pre-pass necessarily visits
    ///     the same subtree depth the real render pass will visit again immediately afterward.
    /// </param>
    /// <param name="markerDepth">
    ///     The current <c>marker</c>-reference nesting depth, forwarded unchanged through
    ///     ordinary subtree recursion and incremented only when this method itself recurses into
    ///     a resolved marker's own content (see this method's remarks on marker bounds inclusion),
    ///     mirroring <see cref="RenderOneMarker"/>'s identical <see cref="MaxMarkerDepth"/> guard.
    /// </param>
    /// <param name="totalElements">
    ///     The running total-rendered-elements count. <see cref="RenderFilteredGroup"/> always
    ///     passes a local, independently bounded scratch counter here (never the real
    ///     per-<c>Load</c>-call counter <see cref="RenderElement"/> itself threads through), so
    ///     this bounds-only pre-pass cannot charge the same ceiling the real render pass that
    ///     follows it will also charge - see <see cref="RenderFilteredGroup"/>'s remarks for the
    ///     full rationale. The same <see cref="MaxTotalRenderedElements"/> ceiling is still
    ///     enforced against whatever counter is supplied, purely to keep this pre-pass's own work
    ///     bounded for a pathologically large subtree.
    /// </param>
    /// <param name="workBudget">
    ///     The geometry-parsing work budget, charged here (via the same <c>Build*Path</c>/
    ///     text-length call sites <see cref="RenderElement"/>/<see cref="RenderText"/> already
    ///     use) - subject to the same caller-supplied local-scratch-instance scoping as
    ///     <paramref name="totalElements"/> above.
    /// </param>
    /// <param name="boundsPrePassBudget">
    ///     The shared, per-<c>Load</c>-call cumulative bounds-pre-pass work budget (see
    ///     <see cref="BoundsPrePassWorkBudget"/>) - deliberately <em>not</em> a fresh local-scratch
    ///     instance like <paramref name="totalElements"/>/<paramref name="workBudget"/> above:
    ///     every <see cref="RenderFilteredGroup"/> invocation (nested or sibling) across the whole
    ///     document shares this one instance, so the combined pre-pass work performed by many
    ///     nested filtered groups is bounded in aggregate, independent of how many times each
    ///     individual invocation "resets" its own local-scratch ceilings above. Charged both per
    ///     element visit (<see cref="BoundsPrePassWorkBudget.Charge"/>) and, for the <c>path</c>/
    ///     <c>polyline</c>/<c>polygon</c>/<c>text</c> cases, per approximate geometry-parsing cost
    ///     (<see cref="BoundsPrePassWorkBudget.ChargeGeometry"/>) - the latter bounds a single
    ///     element with enormous geometry being fully re-parsed once per nesting level, a gap the
    ///     former (which only counts the visit, not its cost) cannot catch on its own.
    /// </param>
    /// <returns>
    ///     The union of every descendant shape/text element's stroke-expanded, transformed local
    ///     bounds - including, for a <c>line</c>/<c>polyline</c>/<c>polygon</c>/<c>path</c> with a
    ///     <c>marker-start</c>/<c>marker-mid</c>/<c>marker-end</c> presentation attribute
    ///     referencing a valid <c>marker</c>, the union of every placed marker instance's own
    ///     content bounds too (see <see cref="ComputeMarkerContentLocalBounds"/>) - or
    ///     <see langword="null"/> if <paramref name="element"/> and its subtree paint nothing at
    ///     all: a non-rendering/skipped/unrecognized element (a bare <c>marker</c> element
    ///     encountered directly, rather than referenced via <c>marker-start</c>/<c>marker-mid</c>/
    ///     <c>marker-end</c>, is still a <see cref="NonRenderingElements"/> member and therefore
    ///     never recursed into here on its own), an element with a non-finite composed transform,
    ///     an empty container, a dangling <c>use</c> reference, a <c>text</c> element with no
    ///     matching font/empty content, or a degenerate/zero-extent shape.
    /// </returns>
    /// <remarks>
    ///     A group's own painted-content bounds must include any marker geometry a
    ///     <c>line</c>/<c>polyline</c>/<c>polygon</c>/<c>path</c> descendant places, because the
    ///     real render pass that follows this pre-pass (<see cref="RenderElement"/>'s shape cases,
    ///     via <see cref="RenderMarkers"/>) paints marker pixels (arrowheads, etc.) directly onto
    ///     whatever surface is current - for a filtered group, that is the offscreen
    ///     <c>SourceGraphic</c> buffer <see cref="RenderFilteredGroup"/> sizes from this method's
    ///     own return value. A marker commonly extends beyond its host shape's own stroke-expanded
    ///     outline (for example an arrowhead marker on a thin line); omitting that marker geometry
    ///     here would size the offscreen buffer too small, silently clipping the marker's pixels
    ///     before the filter chain (or the final composite) ever sees them.
    /// </remarks>
    private static Rect? ComputeSubtreeLocalBounds(
        XElement element,
        RenderState parentState,
        Matrix3x2 relativeTransform,
        RenderContext context,
        int useDepth,
        int elementDepth,
        int markerDepth,
        ref int totalElements,
        GeometryWorkBudget workBudget,
        BoundsPrePassWorkBudget boundsPrePassBudget)
    {
        // Mirror RenderElement's own depth/total-element guards exactly - this pre-pass walks the
        // same subtree the real render pass will walk again immediately afterward, so it must be
        // bounded by the same ceilings, just against the caller-supplied (possibly local-scratch)
        // counter/budget instances - see this method's remarks on totalElements/workBudget scoping
        if (elementDepth >= MaxElementDepth)
        {
            throw new InvalidDataException("SVG element nesting exceeds the supported depth.");
        }

        if (totalElements >= MaxTotalRenderedElements)
        {
            throw new InvalidDataException("SVG document resolves to too many total rendered elements.");
        }

        totalElements++;

        // Charge the shared, per-Load-call cumulative pre-pass budget too - unlike
        // totalElements/workBudget above, this is never a fresh local-scratch instance, so it
        // still accumulates across every nested RenderFilteredGroup invocation's own pre-pass,
        // bounding the total "depth * subtree-size" work a document with many levels of nested
        // filtered groups could otherwise force (see BoundsPrePassWorkBudget's remarks)
        boundsPrePassBudget.Charge();

        var name = element.Name.LocalName;
        if (NonRenderingElements.Contains(name) || SkippedElements.Contains(name))
        {
            return null;
        }

        var state = ApplyPresentationAttributes(parentState, element);
        var transform = ParseTransformAttribute(element) * relativeTransform;
        if (!IsFiniteTransform(transform))
        {
            return null;
        }

        switch (name)
        {
            case "g":
            case "symbol":
                {
                    // Mirrors RenderElement's identical suppressFilter/ResolveFilterElement
                    // derivation - a "g"/"symbol" element's own "filter" attribute never applies
                    // (to its own content, as one filtered unit) when this call is itself part of
                    // a marker's own content subtree
                    var ownFilterElement = markerDepth > 0 ? null : ResolveFilterElement(element, context);

                    // When this element carries its own filter, its children must be visited
                    // relative to this element's own pre-transform local space
                    // (Matrix3x2.Identity), exactly mirroring RenderFilteredGroup's own bounds
                    // pre-pass (relativeTransform starting at Identity for a filtered group's own
                    // direct children) - so the filter's objectBoundingBox-relative region
                    // fractions (see ApplyOwnFilterToLocalBounds) are computed against the right
                    // frame, before "transform" is applied exactly once, below. Without an own
                    // filter, children are visited directly relative to this element's own
                    // already-composed "transform" (this method's original behavior), so their
                    // returned bounds are already in the caller's own reference frame and no
                    // further Transform call is needed
                    var childRelativeTransform = ownFilterElement == null ? transform : Matrix3x2.Identity;

                    var bounds = Rect.Empty;
                    foreach (var child in element.Elements())
                    {
                        var childBounds = ComputeSubtreeLocalBounds(child, state, childRelativeTransform, context, useDepth, elementDepth + 1, markerDepth, ref totalElements, workBudget, boundsPrePassBudget);
                        if (childBounds != null)
                        {
                            bounds = bounds.Union(childBounds.Value);
                        }
                    }

                    if (bounds.IsEmpty)
                    {
                        return null;
                    }

                    return ownFilterElement == null ? bounds : ApplyOwnFilterToLocalBounds(ownFilterElement, bounds, transform);
                }

            case "rect":
                return ShapeBoundsRespectingOwnFilter(element, BuildRectPath(element, state), state, transform, context, markerDepth);

            case "circle":
                return ShapeBoundsRespectingOwnFilter(element, BuildEllipsePath(element, isCircle: true, state), state, transform, context, markerDepth);

            case "ellipse":
                return ShapeBoundsRespectingOwnFilter(element, BuildEllipsePath(element, isCircle: false, state), state, transform, context, markerDepth);

            case "line":
                {
                    var linePath = BuildLinePath(element, state);
                    var shapeBounds = ShapeBoundsRespectingOwnFilter(element, linePath, state, transform, context, markerDepth);
                    var markerBounds = ComputeMarkerContentLocalBounds(linePath, state, transform, context, useDepth, elementDepth, markerDepth, ref totalElements, workBudget, boundsPrePassBudget);
                    return UnionNullableBounds(shapeBounds, markerBounds);
                }

            case "polyline":
                {
                    // Charge the shared, cumulative bounds-pre-pass geometry budget for this
                    // element's own re-parse cost too, upfront and in addition to (never instead
                    // of) BuildPolyPath's own per-invocation, local-scratch workBudget charge
                    // below - see BoundsPrePassWorkBudget.ChargeGeometry's remarks for why a
                    // single element's own geometry-parsing cost must also be bounded across
                    // every nested filtered-group level that re-visits it, not just once per
                    // element visit
                    boundsPrePassBudget.ChargeGeometry(((string?)element.Attribute("points"))?.Length ?? 0);
                    var polylinePath = BuildPolyPath(element, closed: false, workBudget);
                    var shapeBounds = ShapeBoundsRespectingOwnFilter(element, polylinePath, state, transform, context, markerDepth);
                    var markerBounds = ComputeMarkerContentLocalBounds(polylinePath, state, transform, context, useDepth, elementDepth, markerDepth, ref totalElements, workBudget, boundsPrePassBudget);
                    return UnionNullableBounds(shapeBounds, markerBounds);
                }

            case "polygon":
                {
                    // See the "polyline" case above for why this charge is made here.
                    boundsPrePassBudget.ChargeGeometry(((string?)element.Attribute("points"))?.Length ?? 0);
                    var polygonPath = BuildPolyPath(element, closed: true, workBudget);
                    var shapeBounds = ShapeBoundsRespectingOwnFilter(element, polygonPath, state, transform, context, markerDepth);
                    var markerBounds = ComputeMarkerContentLocalBounds(polygonPath, state, transform, context, useDepth, elementDepth, markerDepth, ref totalElements, workBudget, boundsPrePassBudget);
                    return UnionNullableBounds(shapeBounds, markerBounds);
                }

            case "path":
                {
                    // See the "polyline" case above for why this charge is made here (using the
                    // "d" attribute's own character count as the same kind of simple, cheap
                    // geometry-size proxy).
                    boundsPrePassBudget.ChargeGeometry(((string?)element.Attribute("d"))?.Length ?? 0);
                    var dataPath = BuildPathDataPath(element, workBudget);
                    var shapeBounds = ShapeBoundsRespectingOwnFilter(element, dataPath, state, transform, context, markerDepth);
                    var markerBounds = ComputeMarkerContentLocalBounds(dataPath, state, transform, context, useDepth, elementDepth, markerDepth, ref totalElements, workBudget, boundsPrePassBudget);
                    return UnionNullableBounds(shapeBounds, markerBounds);
                }

            case "use":
                {
                    if (useDepth >= MaxUseDepth)
                    {
                        throw new InvalidDataException("Exceeded the maximum <use> reference nesting depth.");
                    }

                    var hrefId = GetHrefAttribute(element) is { } href ? ExtractFragmentId(href) : null;
                    if (hrefId == null || !context.IdIndex.TryGetValue(hrefId, out var target))
                    {
                        return null;
                    }

                    var offset = new Vector2(
                        GetFloatAttribute(element, "x", state, PercentageAxis.Horizontal),
                        GetFloatAttribute(element, "y", state, PercentageAxis.Vertical));

                    // Mirrors RenderUse's identical TryResolveUseTarget derivation - a "symbol"
                    // target establishes a new nested viewport fitted via preserveAspectRatio
                    // against its own viewBox (see TryResolveUseTarget's remarks); a non-"symbol"
                    // target (or a degenerate/non-finite fit) reproduces the original
                    // translate-only behavior exactly
                    if (!TryResolveUseTarget(element, target, state, out var viewportFit, out var targetState))
                    {
                        return null;
                    }

                    var useTransform = viewportFit * Matrix3x2.CreateTranslation(offset) * transform;

                    // Mirrors RenderUse's identical suppressFilter/ResolveFilterElement
                    // derivation - a "use" element's own "filter" attribute (never the referenced
                    // target's own attributes, which this same recursive call already handles
                    // identically to any other descendant) applies to its resolved target as one
                    // filtered unit; see the "g"/"symbol" case above for why the target must then
                    // be visited relative to Matrix3x2.Identity instead of useTransform
                    var ownFilterElement = markerDepth > 0 ? null : ResolveFilterElement(element, context);
                    var targetRelativeTransform = ownFilterElement == null ? useTransform : Matrix3x2.Identity;
                    var targetBounds = ComputeSubtreeLocalBounds(target, targetState, targetRelativeTransform, context, useDepth + 1, elementDepth + 1, markerDepth, ref totalElements, workBudget, boundsPrePassBudget);
                    if (targetBounds == null)
                    {
                        return null;
                    }

                    return ownFilterElement == null ? targetBounds : ApplyOwnFilterToLocalBounds(ownFilterElement, targetBounds.Value, useTransform);
                }

            case "text":
                {
                    if (context.Fonts == null)
                    {
                        return null;
                    }

                    var font = MatchFont(state.FontFamily, state.FontWeight, state.FontStyle, context.Fonts);
                    if (font == null)
                    {
                        return null;
                    }

                    var text = element.Value;
                    if (string.IsNullOrEmpty(text))
                    {
                        return null;
                    }

                    // Charge the text's character count here too, mirroring RenderText's own
                    // charge site - subject to the same caller-supplied counter/budget scoping as
                    // this method's own totalElements/workBudget parameters
                    workBudget.Charge(text.Length);

                    // Also charge the shared, cumulative bounds-pre-pass geometry budget (see the
                    // "polyline"/"polygon"/"path" cases above, and BoundsPrePassWorkBudget.
                    // ChargeGeometry's remarks) - a text element's own glyph-run layout cost below
                    // is likewise proportional to its character count, and re-parsed once per
                    // nesting level under many nested filtered groups exactly like path/points
                    // data would be
                    boundsPrePassBudget.ChargeGeometry(text.Length);

                    var origin = new Vector2(
                        GetFloatAttribute(element, "x", state, PercentageAxis.Horizontal),
                        GetFloatAttribute(element, "y", state, PercentageAxis.Vertical));
                    var glyphRunPath = BuildGlyphRunPath(text, font, state, origin);
                    return ShapeBoundsRespectingOwnFilter(element, glyphRunPath, state, transform, context, markerDepth);
                }

            default:
                // Any other element name (including future/unknown elements) paints nothing,
                // consistent with RenderElement's own out-of-scope-construct policy
                return null;
        }
    }

    /// <summary>
    ///     Unions two optional <see cref="Rect"/> bounds, tolerating either (or both) being
    ///     <see langword="null"/> - a small helper so <see cref="ComputeSubtreeLocalBounds"/>'s
    ///     shape cases can fold a shape's own bounds with its marker content's bounds (see
    ///     <see cref="ComputeMarkerContentLocalBounds"/>) without repeating null-checking at
    ///     every call site.
    /// </summary>
    /// <param name="first">The first optional bounds.</param>
    /// <param name="second">The second optional bounds.</param>
    /// <returns>
    ///     <see langword="null"/> if both <paramref name="first"/> and <paramref name="second"/>
    ///     are <see langword="null"/>; otherwise the other one if exactly one is
    ///     <see langword="null"/>; otherwise their union.
    /// </returns>
    private static Rect? UnionNullableBounds(Rect? first, Rect? second)
    {
        if (first == null)
        {
            return second;
        }

        if (second == null)
        {
            return first;
        }

        return first.Value.Union(second.Value);
    }

    /// <summary>
    ///     Computes one shape (or glyph run)'s own painted-content bounds for
    ///     <see cref="ComputeSubtreeLocalBounds"/>, additionally accounting for <paramref name="element"/>'s
    ///     own <c>filter</c> presentation attribute (if any) - the bounds-only counterpart of
    ///     <see cref="RenderShapeWithFilter"/>, sharing its exact <see cref="ResolveFilterElement"/>/
    ///     <paramref name="markerDepth"/>-suppression decision, so a filtered ancestor group's own
    ///     offscreen buffer is sized large enough to contain this shape's own filtered output too
    ///     (see this class's remarks on nested group filtering), not just its raw stroke-expanded
    ///     outline.
    /// </summary>
    /// <param name="element">
    ///     The originating element, whose own <c>filter</c> attribute is read directly - identical
    ///     to <see cref="RenderShapeWithFilter"/>'s own <paramref name="element"/> parameter.
    /// </param>
    /// <param name="localPath">The shape's (or glyph run's) already-built local-space outline.</param>
    /// <param name="state">The cascaded render state.</param>
    /// <param name="transform">The accumulated transform from local space into the caller's reference frame.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="markerDepth">
    ///     The current <c>marker</c>-reference nesting depth - when greater than zero (this call is
    ///     itself part of a marker's own content subtree), <paramref name="element"/>'s own
    ///     <c>filter</c> attribute is never resolved, identical to <see cref="RenderShapeWithFilter"/>'s
    ///     own <c>suppressFilter</c> derivation in <see cref="RenderElement"/>/<see cref="RenderText"/>.
    /// </param>
    /// <returns>
    ///     <paramref name="element"/>'s own filter-expanded bounds (see
    ///     <see cref="ApplyOwnFilterToLocalBounds"/>) when it carries a resolvable <c>filter</c>
    ///     attribute; otherwise <paramref name="localPath"/>'s raw, stroke-expanded, transformed
    ///     bounds, or <see langword="null"/> if either is empty/degenerate.
    /// </returns>
    private static Rect? ShapeBoundsRespectingOwnFilter(
        XElement element,
        Path localPath,
        RenderState state,
        Matrix3x2 transform,
        RenderContext context,
        int markerDepth)
    {
        var rawLocalBounds = ExpandBoundsForStroke(localPath.GetBounds(), state);
        var ownFilterElement = markerDepth > 0 ? null : ResolveFilterElement(element, context);
        if (ownFilterElement == null)
        {
            var bounds = rawLocalBounds.Transform(transform);
            return bounds.IsEmpty ? null : bounds;
        }

        return ApplyOwnFilterToLocalBounds(ownFilterElement, rawLocalBounds, transform);
    }

    /// <summary>
    ///     Applies <paramref name="filterElement"/>'s own filter-region expansion to
    ///     <paramref name="rawLocalBounds"/> - the local-space (pre-<paramref name="transform"/>)
    ///     union of an element's (or a filtered <c>g</c>/<c>symbol</c>/<c>use</c> target subtree's)
    ///     own raw geometry bounds - mirroring the exact same "would this filter actually apply, or
    ///     tolerantly fall back to the raw, unfiltered bounds instead" decision
    ///     <see cref="RenderFilteredShape"/>/<see cref="RenderFilteredGroup"/> themselves make at
    ///     render time (a zero-primitive filter, or a filter region that cannot be computed from
    ///     <paramref name="rawLocalBounds"/>, falls back to <paramref name="rawLocalBounds"/>
    ///     unchanged), so <see cref="ComputeSubtreeLocalBounds"/>'s bounds pre-pass and the real
    ///     render pass that follows it never disagree about whether a given element's filter will
    ///     actually apply. Used by <see cref="ComputeSubtreeLocalBounds"/> (directly for a
    ///     <c>g</c>/<c>symbol</c>/<c>use</c> element carrying its own <c>filter</c>, and via
    ///     <see cref="ShapeBoundsRespectingOwnFilter"/> for a shape/<c>text</c> element carrying its
    ///     own <c>filter</c>) so an ancestor filtered group's own offscreen buffer is sized large
    ///     enough to contain that descendant's own (possibly larger-than-its-raw-geometry) filtered
    ///     output too.
    /// </summary>
    /// <param name="filterElement">The element's own resolved <c>filter</c> element.</param>
    /// <param name="rawLocalBounds">
    ///     The element's (or, for a group/<c>use</c> target, its subtree's) own raw, stroke-expanded
    ///     geometry bounds, in the local space <paramref name="transform"/> maps into the caller's
    ///     reference frame - i.e. excluding the element's own <c>transform</c> attribute and every
    ///     ancestor transform, exactly mirroring <see cref="ComputeFilterRegionPixelBounds(XElement, Rect, Matrix3x2)"/>'s
    ///     own <c>bounds</c> parameter.
    /// </param>
    /// <param name="transform">
    ///     The accumulated transform from <paramref name="rawLocalBounds"/>'s own local space into
    ///     the caller's chosen reference frame - the same <c>transform</c> local variable
    ///     <see cref="ComputeSubtreeLocalBounds"/> already computes for the visited element itself.
    /// </param>
    /// <returns>
    ///     The expanded (or, on fallback, raw) bounds mapped through <paramref name="transform"/>,
    ///     or <see langword="null"/> if <paramref name="rawLocalBounds"/> is itself empty/
    ///     degenerate, or the transformed result is empty.
    /// </returns>
    /// <remarks>
    ///     Deliberately does not reproduce <see cref="ComputeFilterRegionPixelBounds(XElement, Rect, Matrix3x2)"/>'s
    ///     full pixel-space checks (outward pixel rounding, or <see cref="IsFilterPrimitiveWorkWithinBudget"/>'s
    ///     region-area-weighted work check), nor does it charge <see cref="FilterWorkBudget"/>'s
    ///     cumulative ceiling: this bounds pre-pass necessarily runs before the ancestor filtered
    ///     group's own final pixel-space transform is known (it composes only as far up as that
    ///     ancestor's own local space, see <see cref="ComputeSubtreeLocalBounds"/>'s remarks on
    ///     <c>relativeTransform</c>), so a true pixel-area budget check made here cannot be
    ///     guaranteed to agree with the real render pass's own check regardless. Treating the
    ///     descendant's filter as applying whenever it structurally could (a non-empty region, at
    ///     least one primitive) is therefore a deliberately safe approximation in the direction
    ///     that matters: at worst it sizes the ancestor's offscreen buffer somewhat larger than
    ///     strictly necessary for a descendant filter that later falls back to unfiltered rendering
    ///     at real pixel scale (never smaller, so this can never re-introduce the clipping bug this
    ///     method exists to fix), and the descendant's own real render-time
    ///     <see cref="RenderFilteredShape"/>/<see cref="RenderFilteredGroup"/> call remains the only
    ///     site that ever charges <see cref="FilterWorkBudget"/>, so this pre-pass cannot
    ///     double-charge it.
    ///     <para>
    ///     One pixel-space-only check IS deliberately, partially approximated here, though: a
    ///     region already so large in its own pre-transform LOCAL space that it is virtually
    ///     certain to still exceed <see cref="MaxCoordinateMagnitude"/> (and therefore be rejected
    ///     by <see cref="ComputeFilterRegionPixelBounds(XElement, Rect, Matrix3x2)"/>) once the
    ///     real render pass eventually transforms it all the way into actual pixel space. Without
    ///     this check, a descendant filter that is pathologically oversized and therefore
    ///     guaranteed to be rejected at real render time (falling back to the descendant's own
    ///     raw, unexpanded bounds) would still have already inflated this pre-pass's returned
    ///     bounds with its doomed expanded region - and that inflated value can itself then push
    ///     an OUTER ancestor filter's own region past ITS OWN real pixel-space rejection checks,
    ///     incorrectly skipping a perfectly reasonable outer filter purely because of an inner
    ///     filter that was never actually going to apply. Comparing the region's raw local-space
    ///     size directly against <see cref="MaxCoordinateMagnitude"/> - without composing
    ///     <paramref name="transform"/> (or any further, not-yet-known ancestor transform) into
    ///     the comparison at all - is a deliberately conservative, "never worse than before"
    ///     heuristic, not a precise predictor: it catches only the unambiguous, already-oversized-
    ///     before-any-transform case (the common pathological shape this class's regression tests
    ///     cover), erring toward NOT rejecting borderline-reasonable regions whenever there is any
    ///     doubt, since under-sizing an ancestor's own offscreen buffer for a filter that DOES
    ///     survive the real check would clip real content - the one outcome this whole pre-pass
    ///     exists to avoid, and a strictly worse failure mode than merely over-sizing it.
    ///     </para>
    /// </remarks>
    private static Rect? ApplyOwnFilterToLocalBounds(XElement filterElement, Rect rawLocalBounds, Matrix3x2 transform)
    {
        if (rawLocalBounds.IsEmpty)
        {
            return null;
        }

        // A zero-primitive filter can never change the rendered output (see RenderFilteredShape's
        // identical guard) and therefore always falls back to unfiltered rendering at real time -
        // this pre-pass must fall back to the same raw bounds in that case too, rather than
        // diverging from the real render decision
        var filterLocalRegion = CountFilterPrimitiveWorkUnits(filterElement) == 0
            ? null
            : ComputeFilterRegionLocalBounds(filterElement, rawLocalBounds);

        // A filter region that is already, in its own pre-transform local space, grossly larger
        // than MaxCoordinateMagnitude is virtually certain to still exceed MaxCoordinateMagnitude
        // (or Surface.MaxDimension) once ComputeFilterRegionPixelBounds transforms it all the way
        // into real pixel space at real render time - and therefore virtually certain to be
        // rejected there, falling back to unfiltered rendering at this descendant's own raw
        // bounds. Falling back to rawLocalBounds here too (rather than this pathologically large
        // expanded region) prevents that doomed descendant filter from inflating an ancestor's own
        // combined local bounds enough to itself trip the ancestor's OWN real render-time
        // pixel-space rejection checks, purely because of an inner filter that was never going to
        // apply in the first place - see this method's remarks for why this comparison
        // deliberately never composes "transform" (or any further, not-yet-known ancestor
        // transform) into the check, and is therefore only a conservative approximation, not a
        // precise predictor, of the real render-time rejection.
        var isObviouslyDoomed = filterLocalRegion is { } candidateRegion &&
            (MathF.Abs(candidateRegion.X) > MaxCoordinateMagnitude ||
             MathF.Abs(candidateRegion.Y) > MaxCoordinateMagnitude ||
             candidateRegion.Width > MaxCoordinateMagnitude ||
             candidateRegion.Height > MaxCoordinateMagnitude);

        var effectiveLocalBounds = filterLocalRegion == null || isObviouslyDoomed ? rawLocalBounds : filterLocalRegion.Value;
        var transformed = effectiveLocalBounds.Transform(transform);
        return transformed.IsEmpty ? null : transformed;
    }

    /// <summary>
    ///     Renders every element in <paramref name="children"/> as one combined unit through
    ///     <paramref name="filterElement"/>'s own primitive chain - the group-level counterpart of
    ///     <see cref="RenderFilteredShape"/>, sharing its exact offscreen-render/filter/composite
    ///     algorithm and every one of its resource-safety guards, but sized from the union of
    ///     <paramref name="children"/>'s own subtree bounds (via <see cref="ComputeSubtreeLocalBounds"/>)
    ///     instead of a single shape's own outline.
    /// </summary>
    /// <param name="filterElement">The resolved <c>filter</c> element.</param>
    /// <param name="children">
    ///     The elements to render as one filtered unit - a <c>g</c>/<c>symbol</c> element's own
    ///     direct children (see <see cref="RenderElement"/>'s <c>"g"</c>/<c>"symbol"</c> case), or
    ///     a single-element list containing a <c>use</c> element's resolved target (see
    ///     <see cref="RenderUse"/>).
    /// </param>
    /// <param name="state">The cascaded render state at the filtered group/use element itself.</param>
    /// <param name="childrenTransform">
    ///     The accumulated transform from <paramref name="children"/>'s own shared local space
    ///     (the space <see cref="ComputeSubtreeLocalBounds"/> computes bounds in, starting from
    ///     <see cref="Matrix3x2.Identity"/>) into pixel space - for the <c>g</c>/<c>symbol</c>
    ///     case, this is the group's own already-composed transform; for the <c>use</c> case, this
    ///     is <c>translate(x,y) * (the use element's own already-composed transform)</c>.
    /// </param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="useDepth">The current <c>use</c>-reference nesting depth.</param>
    /// <param name="elementDepth">
    ///     The current recursion depth at the filtered group/use element itself - <paramref name="children"/>
    ///     are always rendered/bounds-computed one level deeper (<c>elementDepth + 1</c>), mirroring
    ///     the unfiltered <c>g</c> loop and unfiltered <c>use</c> re-entry this method replaces.
    /// </param>
    /// <param name="markerDepth">
    ///     The current <c>marker</c>-reference nesting depth, forwarded unchanged to every child
    ///     render.
    /// </param>
    /// <param name="totalElements">The running total-rendered-elements count.</param>
    /// <param name="workBudget">The shared geometry-parsing work budget.</param>
    /// <param name="filterWorkBudget">
    ///     The shared cumulative filter-evaluation work budget - charged identically (same
    ///     region-weighted work-unit formula, same <see cref="FilterWorkBudget.TryCharge"/> call)
    ///     to <see cref="RenderFilteredShape"/>'s own charge, so group filters and shape filters
    ///     share one running cumulative ceiling per <c>Load</c> call.
    /// </param>
    /// <param name="boundsPrePassBudget">
    ///     The shared, per-<c>Load</c>-call cumulative bounds-pre-pass work budget (see
    ///     <see cref="BoundsPrePassWorkBudget"/>), charged by this method's own bounds pre-pass
    ///     below - unlike the fresh local-scratch <c>totalElements</c> counter/
    ///     <see cref="GeometryWorkBudget"/> instance the pre-pass otherwise uses (see this
    ///     method's remarks), this ONE instance is shared by every <see cref="RenderFilteredGroup"/>
    ///     invocation (nested or sibling) for the whole document, so a document with many levels
    ///     of nested filtered groups cannot force unbounded total pre-pass work merely because
    ///     each nesting level's own pre-pass otherwise "resets" its own local-scratch ceilings.
    /// </param>
    /// <remarks>
    ///     If <paramref name="children"/>'s combined subtree paints nothing (an empty group, or a
    ///     group/subtree consisting entirely of non-rendering/skipped/dangling content), the filter
    ///     region cannot be computed, the filter chain has zero primitives or exceeds
    ///     <see cref="MaxFilterPrimitivesPerFilter"/>/<see cref="MaxFilterPrimitiveWorkUnits"/>, or
    ///     this application would push <paramref name="filterWorkBudget"/>'s cumulative total past
    ///     its ceiling, the whole filter effect is tolerantly skipped and every element in
    ///     <paramref name="children"/> is instead rendered directly (unfiltered), exactly as if the
    ///     referencing <c>g</c>/<c>symbol</c>/<c>use</c> element had no <c>filter</c> attribute at
    ///     all - mirroring <see cref="RenderFilteredShape"/>'s identical unfiltered-fallback
    ///     convention.
    ///     <para>
    ///     <paramref name="state"/>'s own (possibly cascaded) <see cref="RenderState.Opacity"/> is
    ///     applied exactly once, after the filter chain evaluates, via
    ///     <see cref="CompositeFilterResultOntoCanvas"/> - <paramref name="children"/> are rendered
    ///     into <c>SourceGraphic</c> with a copy of <paramref name="state"/> whose
    ///     <see cref="RenderState.Opacity"/> is forced to <c>1.0</c>, identical in shape to
    ///     <see cref="RenderFilteredShape"/>'s own opacity handling. Because
    ///     <see cref="ApplyPresentationAttributes"/> already multiplicatively folds an element's
    ///     own <c>opacity</c> attribute into <paramref name="state"/>'s <see cref="RenderState.Opacity"/>
    ///     before <see cref="RenderElement"/> ever dispatches into the <c>"g"</c>/<c>"use"</c> case,
    ///     this reproduces "group opacity applies once, after the filter" with no further
    ///     special-casing - each child's own <c>opacity</c> attribute still folds multiplicatively
    ///     underneath, per pixel, exactly as it does today.
    ///     </para>
    ///     <para>
    ///     This codec implements no <c>clipPath</c> support at all (an existing, independently
    ///     documented out-of-scope decision) - there is therefore nothing for this method to
    ///     "respect" for clip ordering; a filtered group's clip-path attribute, like every other
    ///     element's, is simply never read.
    ///     </para>
    /// </remarks>
    private static void RenderFilteredGroup(
        XElement filterElement,
        IReadOnlyList<XElement> children,
        RenderState state,
        Matrix3x2 childrenTransform,
        RenderContext context,
        int useDepth,
        int elementDepth,
        int markerDepth,
        ref int totalElements,
        GeometryWorkBudget workBudget,
        FilterWorkBudget filterWorkBudget,
        BoundsPrePassWorkBudget boundsPrePassBudget)
    {
        // Bounds pre-pass: union every child's own subtree bounds, computed purely in the
        // group's own local space (relativeTransform starts at Identity) - mirrors a single
        // shape's own localPath.GetBounds() call, just folded over an entire subtree instead of
        // one already-built Path.
        //
        // Deliberately uses fresh, local scratch counter/budget instances here rather than the
        // real "ref int totalElements"/"workBudget" parameters threaded through the rest of this
        // method: this pre-pass necessarily re-visits the same subtree the real render pass below
        // visits again immediately afterward, so charging the *same* counter/budget instance in
        // both passes would double (or, combined with the tolerant-fallback re-render further
        // below, triple) charge every element under a filtered group against
        // MaxTotalRenderedElements/GeometryWorkBudget's fixed ceilings - a document whose
        // unfiltered rendering would legitimately stay under those ceilings could then throw
        // InvalidDataException purely because some of its content happens to sit inside a
        // filtered group, breaking this method's own documented tolerant-fallback contract. The
        // local instances below still enforce the exact same MaxTotalRenderedElements/
        // GeometryWorkBudget ceilings (see ComputeSubtreeLocalBounds's remarks), so this pre-pass
        // itself still cannot run away on a pathologically large subtree - it simply never
        // permanently consumes any of the real, per-Load-call budget the render pass below (and
        // every other filtered shape/group in this document) also needs.
        //
        // boundsPrePassBudget is deliberately NOT one of these fresh local-scratch instances -
        // it is the one shared, per-Load-call instance threaded down from RenderDocument, so
        // every nested/sibling RenderFilteredGroup invocation's own pre-pass below still
        // contributes toward ONE cumulative ceiling, bounding the "depth * subtree-size" total
        // pre-pass work a document with many levels of nested filtered groups would otherwise be
        // able to force by having each nesting level "reset" the local-scratch ceilings above
        // (see BoundsPrePassWorkBudget's remarks for the full rationale).
        var preRenderElementCount = 0;
        var preRenderWorkBudget = new GeometryWorkBudget();
        var localBounds = Rect.Empty;
        foreach (var child in children)
        {
            var childBounds = ComputeSubtreeLocalBounds(child, state, Matrix3x2.Identity, context, useDepth, elementDepth + 1, markerDepth, ref preRenderElementCount, preRenderWorkBudget, boundsPrePassBudget);
            if (childBounds != null)
            {
                localBounds = localBounds.Union(childBounds.Value);
            }
        }

        // Every tolerant-fallback guard below renders each child directly and unfiltered,
        // exactly as if the referencing element had no "filter" attribute at all - inlined at
        // each guard (rather than a shared local function) because a local function cannot
        // capture the "ref int totalElements" parameter threaded through this whole call chain
        if (localBounds.IsEmpty)
        {
            foreach (var child in children)
            {
                RenderElement(child, state, childrenTransform, context, useDepth, elementDepth + 1, markerDepth, ref totalElements, workBudget, filterWorkBudget, boundsPrePassBudget);
            }

            return;
        }

        var region = ComputeFilterRegionPixelBounds(filterElement, localBounds, childrenTransform);
        if (region == null)
        {
            foreach (var child in children)
            {
                RenderElement(child, state, childrenTransform, context, useDepth, elementDepth + 1, markerDepth, ref totalElements, workBudget, filterWorkBudget, boundsPrePassBudget);
            }

            return;
        }

        var (pixelX, pixelY, pixelWidth, pixelHeight) = region.Value;

        // Identical zero-primitive/per-application-budget guard as RenderFilteredShape - see its
        // remarks for the full rationale
        var primitiveCount = CountFilterPrimitiveWorkUnits(filterElement);
        if (primitiveCount == 0 || !IsFilterPrimitiveWorkWithinBudget(primitiveCount, pixelWidth, pixelHeight))
        {
            foreach (var child in children)
            {
                RenderElement(child, state, childrenTransform, context, useDepth, elementDepth + 1, markerDepth, ref totalElements, workBudget, filterWorkBudget, boundsPrePassBudget);
            }

            return;
        }

        // Identical cumulative-budget guard as RenderFilteredShape - group filters and shape
        // filters share one running total per Load call
        var filterWorkUnits = (long)primitiveCount * pixelWidth * pixelHeight;
        if (!filterWorkBudget.TryCharge(filterWorkUnits))
        {
            foreach (var child in children)
            {
                RenderElement(child, state, childrenTransform, context, useDepth, elementDepth + 1, markerDepth, ref totalElements, workBudget, filterWorkBudget, boundsPrePassBudget);
            }

            return;
        }

        var sourceGraphic = new Surface(pixelWidth, pixelHeight);
        var localToTemp = childrenTransform * Matrix3x2.CreateTranslation(-pixelX, -pixelY);
        var tempContext = context with { Surface = sourceGraphic };

        // Render every child fully opaque (Opacity forced to 1.0), identical in shape to
        // RenderFilteredShape's own SourceGraphic rendering - see this method's remarks
        var opaqueState = state with { Opacity = 1f };
        foreach (var child in children)
        {
            RenderElement(child, opaqueState, localToTemp, tempContext, useDepth, elementDepth + 1, markerDepth, ref totalElements, workBudget, filterWorkBudget, boundsPrePassBudget);
        }

        var finalSurface = EvaluateFilterChain(filterElement, sourceGraphic, childrenTransform);

        CompositeFilterResultOntoCanvas(finalSurface, pixelX, pixelY, context.Surface, state.Opacity);
    }

    /// <summary>
    ///     Computes <paramref name="filterElement"/>'s filter region - always as if
    ///     <c>filterUnits="objectBoundingBox"</c>, the SVG default, regardless of what an explicit
    ///     <c>filterUnits="userSpaceOnUse"</c> actually says (a documented, deliberate
    ///     simplification, see this class's remarks) - and converts it to an integer pixel-space
    ///     bounding box, rounded outward.
    /// </summary>
    /// <param name="filterElement">The resolved <c>filter</c> element.</param>
    /// <param name="localPath">The referencing shape's local-space outline.</param>
    /// <param name="state">The cascaded render state, used to expand the degeneracy check for a stroke.</param>
    /// <param name="transform">The accumulated transform from local space into pixel space.</param>
    /// <returns>
    ///     The pixel-space region as <c>(X, Y, Width, Height)</c>, or <see langword="null"/> if
    ///     <paramref name="localPath"/>'s stroke-inflated local bounds are empty/degenerate, the
    ///     region resolves to a non-positive size, its pixel-space transform is non-finite or
    ///     exceeds <see cref="MaxCoordinateMagnitude"/>, or its rounded pixel size exceeds
    ///     <see cref="Surface.MaxDimension"/> on either axis.
    /// </returns>
    /// <remarks>
    ///     The degeneracy/zero-extent check below is performed against the actually-painted bounds
    ///     (<paramref name="localPath"/>'s fill/centerline bounds, inflated by half the effective
    ///     stroke width on each side when <paramref name="state"/> has a stroke - see
    ///     <see cref="ExpandBoundsForStroke"/>) rather than the bare centerline bounds: a
    ///     horizontal or vertical <c>line</c> has zero height or width in centerline space, so
    ///     using the bare bounds would incorrectly treat a valid filter on a stroked axis-aligned
    ///     line as degenerate and silently fall back to unfiltered rendering.
    /// </remarks>
    private static (int X, int Y, int Width, int Height)? ComputeFilterRegionPixelBounds(
        XElement filterElement, Path localPath, RenderState state, Matrix3x2 transform) =>
        ComputeFilterRegionPixelBounds(filterElement, ExpandBoundsForStroke(localPath.GetBounds(), state), transform);

    /// <summary>
    ///     Computes <paramref name="filterElement"/>'s filter region from an already-computed
    ///     local-space painted-content bounding box - the shared core shared by the single-shape
    ///     overload above (which derives <paramref name="bounds"/> from one shape's own
    ///     stroke-expanded outline) and <see cref="RenderFilteredGroup"/> (which derives
    ///     <paramref name="bounds"/> from the union of an entire group subtree's own painted
    ///     content via <see cref="ComputeSubtreeLocalBounds"/>) - always as if
    ///     <c>filterUnits="objectBoundingBox"</c>, the SVG default, regardless of what an explicit
    ///     <c>filterUnits="userSpaceOnUse"</c> actually says (a documented, deliberate
    ///     simplification, see this class's remarks), converting the result to an integer
    ///     pixel-space bounding box, rounded outward.
    /// </summary>
    /// <param name="filterElement">The resolved <c>filter</c> element.</param>
    /// <param name="bounds">
    ///     The referencing element's (or group subtree's) already stroke-expanded local-space
    ///     painted-content bounds.
    /// </param>
    /// <param name="transform">The accumulated transform from local space into pixel space.</param>
    /// <returns>
    ///     The pixel-space region as <c>(X, Y, Width, Height)</c>, or <see langword="null"/> if
    ///     <paramref name="bounds"/> is empty/degenerate, the region resolves to a non-positive
    ///     size, its pixel-space transform is non-finite or exceeds
    ///     <see cref="MaxCoordinateMagnitude"/>, or its rounded pixel size exceeds
    ///     <see cref="Surface.MaxDimension"/> on either axis.
    /// </returns>
    private static (int X, int Y, int Width, int Height)? ComputeFilterRegionPixelBounds(
        XElement filterElement, Rect bounds, Matrix3x2 transform)
    {
        var localRegion = ComputeFilterRegionLocalBounds(filterElement, bounds);
        if (localRegion == null)
        {
            return null;
        }

        var pixelRegion = localRegion.Value.Transform(transform);
        if (!float.IsFinite(pixelRegion.X) || !float.IsFinite(pixelRegion.Y) ||
            !float.IsFinite(pixelRegion.Width) || !float.IsFinite(pixelRegion.Height) ||
            pixelRegion.Width <= 0f || pixelRegion.Height <= 0f ||
            MathF.Abs(pixelRegion.X) > MaxCoordinateMagnitude || MathF.Abs(pixelRegion.Y) > MaxCoordinateMagnitude ||
            pixelRegion.Width > MaxCoordinateMagnitude || pixelRegion.Height > MaxCoordinateMagnitude)
        {
            return null;
        }

        var minX = (int)MathF.Floor(pixelRegion.X);
        var minY = (int)MathF.Floor(pixelRegion.Y);
        var maxX = (int)MathF.Ceiling(pixelRegion.X + pixelRegion.Width);
        var maxY = (int)MathF.Ceiling(pixelRegion.Y + pixelRegion.Height);

        var width = maxX - minX;
        var height = maxY - minY;
        if (width <= 0 || height <= 0 || width > Surface.MaxDimension || height > Surface.MaxDimension)
        {
            return null;
        }

        return (minX, minY, width, height);
    }

    /// <summary>
    ///     Computes <paramref name="filterElement"/>'s filter region purely in the local space
    ///     <paramref name="bounds"/> is expressed in - always as if <c>filterUnits="objectBoundingBox"</c>,
    ///     the SVG default (the same documented simplification <see cref="ComputeFilterRegionPixelBounds(XElement, Rect, Matrix3x2)"/>
    ///     itself makes) - the shared local-space core factored out of that method, stopping short
    ///     of its own pixel-space transform/rounding/<see cref="MaxCoordinateMagnitude"/>/
    ///     <see cref="Surface.MaxDimension"/> checks. Used both by
    ///     <see cref="ComputeFilterRegionPixelBounds(XElement, Rect, Matrix3x2)"/> itself and by
    ///     <see cref="ApplyOwnFilterToLocalBounds"/>, so <see cref="ComputeSubtreeLocalBounds"/>'s
    ///     bounds pre-pass can fold a nested filtered descendant's own expanded region into the
    ///     rest of its purely local-space bounds unioning without an awkward pixel-rounding
    ///     round-trip.
    /// </summary>
    /// <param name="filterElement">The resolved <c>filter</c> element.</param>
    /// <param name="bounds">The referencing element's already stroke-expanded local-space painted-content bounds.</param>
    /// <returns>
    ///     The local-space filter region, or <see langword="null"/> if <paramref name="bounds"/> is
    ///     empty/degenerate or the region resolves to a non-positive size.
    /// </returns>
    private static Rect? ComputeFilterRegionLocalBounds(XElement filterElement, Rect bounds)
    {
        if (bounds.IsEmpty || bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return null;
        }

        // SVG default filter region: -10% -10% 120% 120% of the referencing element's own
        // objectBoundingBox, each independently overridable via x/y/width/height
        var xFraction = ParseFilterRegionFraction(filterElement, "x", -0.10f);
        var yFraction = ParseFilterRegionFraction(filterElement, "y", -0.10f);
        var widthFraction = ParseFilterRegionFraction(filterElement, "width", 1.20f);
        var heightFraction = ParseFilterRegionFraction(filterElement, "height", 1.20f);

        var localRegion = new Rect(
            bounds.X + (xFraction * bounds.Width),
            bounds.Y + (yFraction * bounds.Height),
            widthFraction * bounds.Width,
            heightFraction * bounds.Height);

        return localRegion.Width <= 0f || localRegion.Height <= 0f ? null : localRegion;
    }

    /// <summary>
    ///     Inflates <paramref name="bounds"/> by half of <paramref name="state"/>'s effective
    ///     <c>stroke-width</c> on every side when a stroke is actually painted, so bounds-derived
    ///     calculations reflect the shape's actually-painted extent rather than its bare fill/
    ///     centerline geometry - notably, a horizontal or vertical <c>line</c> has zero height or
    ///     width in centerline space, so a check that ignored the stroke would treat a valid
    ///     stroked line as degenerate.
    /// </summary>
    /// <param name="bounds">The local-space fill/centerline bounds to inflate.</param>
    /// <param name="state">The cascaded render state supplying <c>stroke</c>/<c>stroke-width</c>.</param>
    /// <returns>
    ///     <paramref name="bounds"/> unchanged if <paramref name="bounds"/> is empty, <c>stroke</c>
    ///     resolves to <c>none</c>/empty, or <c>stroke-width</c> is not finite/positive; otherwise
    ///     <paramref name="bounds"/> inflated by half the stroke width on each side.
    /// </returns>
    private static Rect ExpandBoundsForStroke(Rect bounds, RenderState state)
    {
        if (bounds.IsEmpty)
        {
            return bounds;
        }

        var trimmedStroke = state.Stroke.Trim();
        if (trimmedStroke.Length == 0 || string.Equals(trimmedStroke, "none", StringComparison.OrdinalIgnoreCase))
        {
            return bounds;
        }

        if (!float.IsFinite(state.StrokeWidth) || state.StrokeWidth <= 0f)
        {
            return bounds;
        }

        var halfStrokeWidth = state.StrokeWidth / 2f;
        return new Rect(
            bounds.X - halfStrokeWidth,
            bounds.Y - halfStrokeWidth,
            bounds.Width + (2f * halfStrokeWidth),
            bounds.Height + (2f * halfStrokeWidth));
    }

    /// <summary>
    ///     Reads one of a <c>filter</c> element's <c>x</c>/<c>y</c>/<c>width</c>/<c>height</c>
    ///     region attributes as an objectBoundingBox fraction, tolerantly falling back to
    ///     <paramref name="defaultValue"/> when the attribute is absent or does not parse as a
    ///     number/percentage - reusing the existing <see cref="ParsePercentOrNumber"/> helper
    ///     already used identically for gradient objectBoundingBox-relative coordinates.
    /// </summary>
    private static float ParseFilterRegionFraction(XElement filterElement, string attributeName, float defaultValue)
    {
        var raw = (string?)filterElement.Attribute(attributeName);
        return raw == null ? defaultValue : ParsePercentOrNumber(raw, 1f) ?? defaultValue;
    }

    /// <summary>
    ///     Determines whether evaluating <paramref name="primitiveCount"/> <c>fe*</c> primitives
    ///     against a <paramref name="width"/>x<paramref name="height"/> filter region stays within
    ///     this class's two filter work bounds (see <see cref="MaxFilterPrimitivesPerFilter"/>/
    ///     <see cref="MaxFilterPrimitiveWorkUnits"/>) - checked once, upfront, before any primitive
    ///     is actually evaluated (a cheap child-element count plus one multiply/compare), so a
    ///     pathological chain is rejected near-instantly rather than after partially evaluating it.
    /// </summary>
    /// <param name="primitiveCount">
    ///     The <c>filter</c> element's own primitive-equivalent work-unit count - see
    ///     <see cref="CountFilterPrimitiveWorkUnits"/>.
    /// </param>
    /// <param name="width">The filter region's pixel width.</param>
    /// <param name="height">The filter region's pixel height.</param>
    /// <returns><see langword="true"/> if the chain may be evaluated; otherwise <see langword="false"/>.</returns>
    private static bool IsFilterPrimitiveWorkWithinBudget(int primitiveCount, int width, int height) =>
        primitiveCount <= MaxFilterPrimitivesPerFilter &&
        (long)primitiveCount * width * height <= MaxFilterPrimitiveWorkUnits;

    /// <summary>
    ///     Counts <paramref name="filterElement"/>'s upfront primitive-equivalent work-unit charge
    ///     for <see cref="IsFilterPrimitiveWorkWithinBudget"/>: each direct child counts as 1, except
    ///     a <c>feMerge</c> child, which counts as its own <c>feMergeNode</c> child count (at least
    ///     1) instead - because <see cref="ApplyFeMerge"/> performs one full-surface
    ///     <see cref="Surface.CompositeOver(Surface)"/> per <c>feMergeNode</c>, so a flat charge of 1
    ///     would let a <c>feMerge</c> with a huge number of merge nodes bypass the budget while still
    ///     doing O(node-count &#215; region-area) work.
    /// </summary>
    /// <param name="filterElement">The resolved <c>filter</c> element.</param>
    /// <returns>The total primitive-equivalent work-unit count.</returns>
    private static int CountFilterPrimitiveWorkUnits(XElement filterElement) =>
        filterElement.Elements().Sum(primitive => primitive.Name.LocalName == "feMerge"
            ? Math.Max(1, primitive.Elements().Count(node => node.Name.LocalName == "feMergeNode"))
            : 1);

    /// <summary>
    ///     Evaluates <paramref name="filterElement"/>'s <c>fe*</c> primitive children, in document
    ///     order, against <paramref name="sourceGraphic"/>.
    /// </summary>
    /// <param name="filterElement">The resolved <c>filter</c> element.</param>
    /// <param name="sourceGraphic">
    ///     The already-rendered <c>SourceGraphic</c> buffer, sized to the filter region.
    /// </param>
    /// <param name="transform">
    ///     The referencing shape's own accumulated transform, used only to estimate the pixel-space
    ///     scale for <c>feGaussianBlur</c>/<c>feOffset</c> via <see cref="EstimateUniformScale"/>.
    /// </param>
    /// <returns>
    ///     The last document-order primitive's own output buffer, or <paramref name="sourceGraphic"/>
    ///     itself if <paramref name="filterElement"/> has no <c>fe*</c> children at all (a
    ///     degenerate spec edge case tolerated as "no filter").
    /// </returns>
    /// <remarks>
    ///     Every buffer produced by every primitive in this pipeline is exactly
    ///     <paramref name="sourceGraphic"/>'s own size - this invariant is what lets
    ///     <c>feComposite</c>/<c>feMerge</c> reuse <see cref="Surface.CompositeOver(Surface)"/>
    ///     directly, since that method requires equal-size surfaces. <c>in</c>/<c>in2</c> name
    ///     resolution follows this fixed precedence: <c>"SourceGraphic"</c> resolves to
    ///     <paramref name="sourceGraphic"/> itself; <c>"SourceAlpha"</c> resolves to a lazily-built,
    ///     alpha-only copy of it; a name matching an earlier primitive's own <c>result</c>
    ///     resolves to that primitive's output; an absent/empty name resolves to the immediately
    ///     preceding primitive's own output (or <paramref name="sourceGraphic"/> for the very first
    ///     primitive); and any other (dangling/unrecognized) name tolerantly falls back to
    ///     <paramref name="sourceGraphic"/> - a documented simplification. Any primitive type other
    ///     than <c>feFlood</c>/<c>feGaussianBlur</c>/<c>feOffset</c>/<c>feComposite</c>/<c>feMerge</c>
    ///     (for example <c>feColorMatrix</c>, <c>feTurbulence</c>, <c>feDisplacementMap</c>,
    ///     <c>feImage</c>, <c>feTile</c>, <c>feDropShadow</c>, <c>feConvolveMatrix</c>,
    ///     <c>feDiffuseLighting</c>, <c>feSpecularLighting</c>, <c>feComponentTransfer</c>, or
    ///     <c>feMorphology</c>) is a tolerant no-op passthrough of its own resolved <c>in</c> input,
    ///     registered under its own <c>result</c> name (if any) so later primitives in the chain
    ///     still resolve correctly by name - <c>feImage</c> in particular is entirely out of scope,
    ///     which also means a filter chain can never reference another filtered element's own
    ///     render output, so no additional recursion-depth guard is needed here. This method itself
    ///     needs no internal primitive-count/work-budget guard: its only caller,
    ///     <see cref="RenderFilteredShape"/>, already guarantees both stay within
    ///     <see cref="MaxFilterPrimitivesPerFilter"/>/<see cref="MaxFilterPrimitiveWorkUnits"/>
    ///     before this method is ever invoked (see <see cref="IsFilterPrimitiveWorkWithinBudget"/>).
    /// </remarks>
    private static Surface EvaluateFilterChain(XElement filterElement, Surface sourceGraphic, Matrix3x2 transform)
    {
        var scale = EstimateUniformScale(transform);
        var results = new Dictionary<string, Surface>(StringComparer.Ordinal);
        Surface? previousResult = null;
        Surface? sourceAlpha = null;

        Surface ResolveInput(string? name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return previousResult ?? sourceGraphic;
            }

            if (string.Equals(name, "SourceGraphic", StringComparison.Ordinal))
            {
                return sourceGraphic;
            }

            if (string.Equals(name, "SourceAlpha", StringComparison.Ordinal))
            {
                return sourceAlpha ??= BuildSourceAlpha(sourceGraphic);
            }

            return results.TryGetValue(name, out var namedResult) ? namedResult : sourceGraphic;
        }

        foreach (var primitive in filterElement.Elements())
        {
            var input = ResolveInput((string?)primitive.Attribute("in"));

            Surface output;
            switch (primitive.Name.LocalName)
            {
                case "feFlood":
                    output = ApplyFeFlood(primitive, sourceGraphic.Width, sourceGraphic.Height);
                    break;

                case "feGaussianBlur":
                    output = ApplyFeGaussianBlur(primitive, input, scale);
                    break;

                case "feOffset":
                    output = ApplyFeOffset(primitive, input, scale);
                    break;

                case "feComposite":
                    output = ApplyFeComposite(primitive, input, ResolveInput((string?)primitive.Attribute("in2")));
                    break;

                case "feMerge":
                    output = ApplyFeMerge(primitive, sourceGraphic.Width, sourceGraphic.Height, ResolveInput);
                    break;

                default:
                    // Tolerant no-op passthrough for every unsupported primitive type - see this
                    // method's remarks for the full enumerated list
                    output = input;
                    break;
            }

            var resultName = (string?)primitive.Attribute("result");
            if (!string.IsNullOrEmpty(resultName))
            {
                results[resultName] = output;
            }

            previousResult = output;
        }

        return previousResult ?? sourceGraphic;
    }

    /// <summary>Builds a same-size copy of <paramref name="source"/> with every pixel's color forced to black, alpha unchanged.</summary>
    /// <param name="source">The buffer to derive the alpha-only copy from.</param>
    /// <returns>The lazily-built <c>SourceAlpha</c> implicit filter input.</returns>
    private static Surface BuildSourceAlpha(Surface source)
    {
        var output = new Surface(source.Width, source.Height);
        for (var y = 0; y < source.Height; y++)
        {
            var sourceRow = source.GetRowSpan(y);
            var outputRow = output.GetRowSpan(y);
            for (var x = 0; x < source.Width; x++)
            {
                outputRow[x] = new Rgba32(0, 0, 0, sourceRow[x].A);
            }
        }

        return output;
    }

    /// <summary>
    ///     Evaluates a <c>feFlood</c> primitive: a new same-size buffer filled with a constant
    ///     <c>flood-color</c>/<c>flood-opacity</c> color, via <see cref="Surface.Clear(Rgba32)"/> -
    ///     no new blending math.
    /// </summary>
    /// <param name="element">The <c>feFlood</c> primitive element.</param>
    /// <param name="width">The filter pipeline's fixed buffer width.</param>
    /// <param name="height">The filter pipeline's fixed buffer height.</param>
    /// <returns>The filled buffer.</returns>
    /// <remarks>
    ///     <c>flood-color</c> defaults to (and tolerantly falls back to, if absent or
    ///     unrecognized) opaque black, matching the SVG initial value; <c>flood-opacity</c>
    ///     defaults to (and tolerantly falls back to) <c>1</c>, clamped to <c>[0, 1]</c>.
    /// </remarks>
    private static Surface ApplyFeFlood(XElement element, int width, int height)
    {
        var rawColor = (string?)element.Attribute("flood-color");
        var color = (rawColor != null ? ParseColor(rawColor.Trim()) : null) ?? new Rgba32(0, 0, 0, 255);

        var rawOpacity = (string?)element.Attribute("flood-opacity");
        var opacity = rawOpacity == null ? 1f : ParsePercentOrNumber(rawOpacity, 1f) ?? 1f;
        opacity = Math.Clamp(opacity, 0f, 1f);

        var surface = new Surface(width, height);
        surface.Clear(ApplyAlpha(color, opacity));
        return surface;
    }

    /// <summary>
    ///     Evaluates a <c>feGaussianBlur</c> primitive using the SVG specification's own documented
    ///     three-pass box-blur approximation of a true Gaussian blur.
    /// </summary>
    /// <param name="element">The <c>feGaussianBlur</c> primitive element.</param>
    /// <param name="input">The already-resolved input buffer.</param>
    /// <param name="scale">
    ///     The pixel-space scale factor (see <see cref="EstimateUniformScale"/>) used to convert
    ///     the local-space <c>stdDeviation</c> into an effective pixel-space value.
    /// </param>
    /// <returns>
    ///     A new, independent, blurred buffer - or an independent unblurred copy of
    ///     <paramref name="input"/> if the effective <c>stdDeviation</c> clamps to zero or below
    ///     (an absent/zero/invalid value, or a non-finite scaled result).
    /// </returns>
    /// <remarks>
    ///     Only the first whitespace/comma-separated token of <c>stdDeviation</c> is read (a
    ///     separate x/y pair, a rarely-used form, is tolerated by treating the value as isotropic -
    ///     a documented simplification). The effective pixel-space <c>stdDeviation</c> is clamped
    ///     to <see cref="MaxFilterBlurStdDeviationPixels"/> (see that constant's remarks). The
    ///     box radius is <c>floor(stdDeviation * 3 * sqrt(2*pi) / 4 + 0.5)</c>, applied as three
    ///     successive horizontal-then-vertical box-blur passes (see <see cref="BoxBlurHorizontal"/>/
    ///     <see cref="BoxBlurVertical"/>) over <paramref name="input"/>'s <see cref="Surface.PremultiplyAlpha"/>-converted
    ///     copy, reusing that already-tested method (and its <see cref="Surface.UnpremultiplyAlpha"/>
    ///     inverse) rather than re-deriving premultiplication.
    /// </remarks>
    private static Surface ApplyFeGaussianBlur(XElement element, Surface input, float scale)
    {
        var rawStdDeviation = ParseFirstNumberToken((string?)element.Attribute("stdDeviation")) ?? 0f;
        var stdDeviation = rawStdDeviation * scale;
        if (!float.IsFinite(stdDeviation))
        {
            stdDeviation = 0f;
        }

        stdDeviation = Math.Clamp(stdDeviation, 0f, MaxFilterBlurStdDeviationPixels);

        var radius = stdDeviation > 0f
            ? (int)MathF.Floor((stdDeviation * 3f * MathF.Sqrt(2f * MathF.PI) / 4f) + 0.5f)
            : 0;

        var working = input.Crop(0, 0, input.Width, input.Height);
        if (radius <= 0)
        {
            return working;
        }

        working.PremultiplyAlpha();
        for (var pass = 0; pass < 3; pass++)
        {
            BoxBlurHorizontal(working, radius);
            BoxBlurVertical(working, radius);
        }

        working.UnpremultiplyAlpha();
        return working;
    }

    /// <summary>
    ///     Applies one horizontal box-blur pass, in place, to every row of <paramref name="surface"/>,
    ///     using a running-sum sliding window (cost proportional to width/height, independent of
    ///     <paramref name="radius"/>) with zero-padding beyond the surface's own edges.
    /// </summary>
    /// <param name="surface">The already-premultiplied-alpha buffer to blur in place.</param>
    /// <param name="radius">The box-blur radius (window size is <c>2 * radius + 1</c>).</param>
    private static void BoxBlurHorizontal(Surface surface, int radius)
    {
        var width = surface.Width;
        var windowSize = (2 * radius) + 1;
        var outR = new byte[width];
        var outG = new byte[width];
        var outB = new byte[width];
        var outA = new byte[width];

        for (var y = 0; y < surface.Height; y++)
        {
            var row = surface.GetRowSpan(y);

            long sumR = 0, sumG = 0, sumB = 0, sumA = 0;
            for (var k = 0; k <= radius && k < width; k++)
            {
                var p = row[k];
                sumR += p.R;
                sumG += p.G;
                sumB += p.B;
                sumA += p.A;
            }

            for (var x = 0; x < width; x++)
            {
                outR[x] = (byte)(sumR / windowSize);
                outG[x] = (byte)(sumG / windowSize);
                outB[x] = (byte)(sumB / windowSize);
                outA[x] = (byte)(sumA / windowSize);

                var addIndex = x + radius + 1;
                if (addIndex < width)
                {
                    var p = row[addIndex];
                    sumR += p.R;
                    sumG += p.G;
                    sumB += p.B;
                    sumA += p.A;
                }

                var removeIndex = x - radius;
                if (removeIndex >= 0)
                {
                    var p = row[removeIndex];
                    sumR -= p.R;
                    sumG -= p.G;
                    sumB -= p.B;
                    sumA -= p.A;
                }
            }

            for (var x = 0; x < width; x++)
            {
                row[x] = new Rgba32(outR[x], outG[x], outB[x], outA[x]);
            }
        }
    }

    /// <summary>
    ///     Applies one vertical box-blur pass, in place, to every column of <paramref name="surface"/> -
    ///     the column-wise counterpart of <see cref="BoxBlurHorizontal"/>, using the identical
    ///     running-sum sliding-window/zero-padding algorithm, via the surface's own pixel indexer
    ///     (no column-span accessor exists on <see cref="Surface"/>).
    /// </summary>
    /// <param name="surface">The already-premultiplied-alpha buffer to blur in place.</param>
    /// <param name="radius">The box-blur radius (window size is <c>2 * radius + 1</c>).</param>
    private static void BoxBlurVertical(Surface surface, int radius)
    {
        var height = surface.Height;
        var windowSize = (2 * radius) + 1;
        var outR = new byte[height];
        var outG = new byte[height];
        var outB = new byte[height];
        var outA = new byte[height];

        for (var x = 0; x < surface.Width; x++)
        {
            long sumR = 0, sumG = 0, sumB = 0, sumA = 0;
            for (var k = 0; k <= radius && k < height; k++)
            {
                var p = surface[x, k];
                sumR += p.R;
                sumG += p.G;
                sumB += p.B;
                sumA += p.A;
            }

            for (var y = 0; y < height; y++)
            {
                outR[y] = (byte)(sumR / windowSize);
                outG[y] = (byte)(sumG / windowSize);
                outB[y] = (byte)(sumB / windowSize);
                outA[y] = (byte)(sumA / windowSize);

                var addIndex = y + radius + 1;
                if (addIndex < height)
                {
                    var p = surface[x, addIndex];
                    sumR += p.R;
                    sumG += p.G;
                    sumB += p.B;
                    sumA += p.A;
                }

                var removeIndex = y - radius;
                if (removeIndex >= 0)
                {
                    var p = surface[x, removeIndex];
                    sumR -= p.R;
                    sumG -= p.G;
                    sumB -= p.B;
                    sumA -= p.A;
                }
            }

            for (var y = 0; y < height; y++)
            {
                surface[x, y] = new Rgba32(outR[y], outG[y], outB[y], outA[y]);
            }
        }
    }

    /// <summary>Parses the first whitespace/comma-separated numeric token of a raw attribute value.</summary>
    /// <param name="raw">The raw attribute text, or <see langword="null"/> if absent.</param>
    /// <returns>The parsed value, or <see langword="null"/> if absent/blank/unparseable.</returns>
    private static float? ParseFirstNumberToken(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var tokens = raw.Split([' ', '\t', '\n', '\r', ','], StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length == 0 ? null : ParsePercentOrNumber(tokens[0], 1f);
    }

    /// <summary>
    ///     Evaluates a <c>feOffset</c> primitive: shifts <paramref name="input"/>'s pixel content
    ///     by <c>dx</c>/<c>dy</c> (scaled to pixel space by <paramref name="scale"/>) via a clipped
    ///     row-copy loop - a pure data copy, not blending math.
    /// </summary>
    /// <param name="element">The <c>feOffset</c> primitive element.</param>
    /// <param name="input">The already-resolved input buffer.</param>
    /// <param name="scale">The pixel-space scale factor (see <see cref="EstimateUniformScale"/>).</param>
    /// <returns>A new, same-size buffer with <paramref name="input"/>'s content shifted; pixels shifted off the edge are lost, and newly exposed pixels are fully transparent.</returns>
    private static Surface ApplyFeOffset(XElement element, Surface input, float scale)
    {
        var dx = ParsePercentOrNumber((string?)element.Attribute("dx") ?? string.Empty, 1f) ?? 0f;
        var dy = ParsePercentOrNumber((string?)element.Attribute("dy") ?? string.Empty, 1f) ?? 0f;

        var pixelDx = (int)MathF.Round(dx * scale, MidpointRounding.AwayFromZero);
        var pixelDy = (int)MathF.Round(dy * scale, MidpointRounding.AwayFromZero);

        var width = input.Width;
        var height = input.Height;
        var output = new Surface(width, height);

        for (var destY = 0; destY < height; destY++)
        {
            var sourceY = destY - pixelDy;
            if (sourceY < 0 || sourceY >= height)
            {
                continue;
            }

            var destStart = Math.Max(0, pixelDx);
            var destEnd = Math.Min(width, width + pixelDx);
            if (destEnd <= destStart)
            {
                continue;
            }

            var sourceRow = input.GetRowSpan(sourceY);
            var destRow = output.GetRowSpan(destY);
            var length = destEnd - destStart;
            sourceRow.Slice(destStart - pixelDx, length).CopyTo(destRow.Slice(destStart, length));
        }

        return output;
    }

    /// <summary>
    ///     Evaluates a <c>feComposite</c> primitive: <c>operator="over"</c> (the default, if
    ///     absent) reuses <see cref="Surface.CompositeOver(Surface)"/> directly (its formula
    ///     already <i>is</i> Porter-Duff "over"); <c>in</c>/<c>out</c>/<c>atop</c>/<c>xor</c> use
    ///     the dedicated <see cref="CompositeFeOperator"/> per-pixel helper; any other/unrecognized
    ///     operator value (for example <c>"arithmetic"</c>, seen in real-world documents) is a
    ///     tolerant no-op passthrough of <paramref name="input"/>, ignoring <paramref name="input2"/>.
    /// </summary>
    /// <param name="element">The <c>feComposite</c> primitive element.</param>
    /// <param name="input">The already-resolved <c>in</c> input buffer.</param>
    /// <param name="input2">The already-resolved <c>in2</c> input buffer.</param>
    /// <returns>A new, independent output buffer.</returns>
    private static Surface ApplyFeComposite(XElement element, Surface input, Surface input2)
    {
        var op = ((string?)element.Attribute("operator"))?.Trim().ToLowerInvariant();
        switch (op)
        {
            case null:
            case "":
            case "over":
                var result = input2.Crop(0, 0, input2.Width, input2.Height);
                result.CompositeOver(input);
                return result;

            case "in":
                return CompositeFeOperator(input, input2, FeCompositeOperator.In);

            case "out":
                return CompositeFeOperator(input, input2, FeCompositeOperator.Out);

            case "atop":
                return CompositeFeOperator(input, input2, FeCompositeOperator.Atop);

            case "xor":
                return CompositeFeOperator(input, input2, FeCompositeOperator.Xor);

            default:
                // An unrecognized operator value (e.g. "arithmetic") is a tolerant no-op
                // passthrough of the "in" input, ignoring in2/k1..k4
                return input.Crop(0, 0, input.Width, input.Height);
        }
    }

    /// <summary>
    ///     Composites two same-size buffers using one of the four Porter-Duff operators with no
    ///     existing <see cref="Surface"/> equivalent (<c>in</c>/<c>out</c>/<c>atop</c>/<c>xor</c> -
    ///     <c>over</c> instead reuses <see cref="Surface.CompositeOver(Surface)"/> directly). This
    ///     is the one place in this class's filter support genuinely new per-pixel blending math is
    ///     unavoidable, kept small and isolated, working on straight (unassociated) alpha read via
    ///     <see cref="Surface.GetRowSpan"/>.
    /// </summary>
    /// <param name="foreground">The <c>in</c> input buffer ("A" in the Porter-Duff formulas below).</param>
    /// <param name="background">The <c>in2</c> input buffer ("B" in the Porter-Duff formulas below).</param>
    /// <param name="op">Which of the four operators to apply.</param>
    /// <returns>A new, independent output buffer, the same size as both inputs.</returns>
    /// <remarks>
    ///     For each pixel, using premultiplied per-channel products
    ///     (<c>Ca * Aa</c>/<c>Cb * Ab</c>) and per-operator weights <c>(Fa, Fb)</c> -
    ///     <c>in</c>: <c>(Ab, 0)</c>; <c>out</c>: <c>(1 - Ab, 0)</c>; <c>atop</c>: <c>(Ab, 1 - Aa)</c>;
    ///     <c>xor</c>: <c>(1 - Ab, 1 - Aa)</c> - the standard Porter-Duff formulas apply:
    ///     <c>outAlpha = Fa * Aa + Fb * Ab</c>, and each output channel is
    ///     <c>(Fa * Ca * Aa + Fb * Cb * Ab) / outAlpha</c> (or <c>0</c> if <c>outAlpha</c> is zero).
    /// </remarks>
    private static Surface CompositeFeOperator(Surface foreground, Surface background, FeCompositeOperator op)
    {
        var width = foreground.Width;
        var height = foreground.Height;
        var output = new Surface(width, height);

        for (var y = 0; y < height; y++)
        {
            var fgRow = foreground.GetRowSpan(y);
            var bgRow = background.GetRowSpan(y);
            var outRow = output.GetRowSpan(y);

            for (var x = 0; x < width; x++)
            {
                var fg = fgRow[x];
                var bg = bgRow[x];

                var fgA = fg.A / 255f;
                var bgA = bg.A / 255f;

                var (weightFg, weightBg) = op switch
                {
                    FeCompositeOperator.In => (bgA, 0f),
                    FeCompositeOperator.Out => (1f - bgA, 0f),
                    FeCompositeOperator.Atop => (bgA, 1f - fgA),
                    _ => (1f - bgA, 1f - fgA) // Xor
                };

                var outA = (weightFg * fgA) + (weightBg * bgA);
                byte outR, outG, outB;
                if (outA <= 0f)
                {
                    outR = outG = outB = 0;
                }
                else
                {
                    var premultR = (weightFg * fg.R * fgA) + (weightBg * bg.R * bgA);
                    var premultG = (weightFg * fg.G * fgA) + (weightBg * bg.G * bgA);
                    var premultB = (weightFg * fg.B * fgA) + (weightBg * bg.B * bgA);
                    outR = (byte)Math.Clamp(MathF.Round(premultR / outA, MidpointRounding.AwayFromZero), 0f, 255f);
                    outG = (byte)Math.Clamp(MathF.Round(premultG / outA, MidpointRounding.AwayFromZero), 0f, 255f);
                    outB = (byte)Math.Clamp(MathF.Round(premultB / outA, MidpointRounding.AwayFromZero), 0f, 255f);
                }

                outRow[x] = new Rgba32(outR, outG, outB, (byte)Math.Clamp(MathF.Round(outA * 255f, MidpointRounding.AwayFromZero), 0f, 255f));
            }
        }

        return output;
    }

    /// <summary>
    ///     Evaluates a <c>feMerge</c> primitive: starts from a fresh, fully transparent buffer and
    ///     composites each <c>feMergeNode</c> child's own resolved <c>in</c> input over it, in
    ///     document order, via <see cref="Surface.CompositeOver(Surface)"/> - whose "foreground
    ///     over background" semantics already are <c>feMerge</c>'s own "later nodes on top of
    ///     earlier ones" semantics, so no new blending math is needed.
    /// </summary>
    /// <param name="feMergeElement">The <c>feMerge</c> primitive element.</param>
    /// <param name="width">The filter pipeline's fixed buffer width.</param>
    /// <param name="height">The filter pipeline's fixed buffer height.</param>
    /// <param name="resolveInput">The enclosing filter chain's own <c>in</c>-name resolution function.</param>
    /// <returns>The merged buffer.</returns>
    /// <remarks>
    ///     A <c>feMergeNode</c>'s own <c>in</c> follows the identical resolution rule as any other
    ///     primitive's <c>in</c> - notably, an absent <c>in</c> resolves to the filter chain's own
    ///     running <c>previousResult</c> (not reset between merge nodes), a documented
    ///     simplification. Any non-<c>feMergeNode</c> child is tolerantly ignored.
    /// </remarks>
    private static Surface ApplyFeMerge(XElement feMergeElement, int width, int height, Func<string?, Surface> resolveInput)
    {
        var accumulator = new Surface(width, height);
        foreach (var node in feMergeElement.Elements())
        {
            if (node.Name.LocalName != "feMergeNode")
            {
                continue;
            }

            var nodeInput = resolveInput((string?)node.Attribute("in"));
            accumulator.CompositeOver(nodeInput);
        }

        return accumulator;
    }

    /// <summary>
    ///     Composites <paramref name="result"/> - the filter chain's final output buffer - onto
    ///     <paramref name="canvas"/> at pixel position <c>(</c><paramref name="pixelX"/><c>,</c>
    ///     <paramref name="pixelY"/><c>)</c>, one row at a time via
    ///     <see cref="Surface.CompositeOverSpan(int, int, ReadOnlySpan{float}, ReadOnlySpan{Rgba32})"/>,
    ///     clipped to <paramref name="canvas"/>'s own bounds.
    /// </summary>
    /// <param name="result">The filter chain's final output buffer.</param>
    /// <param name="pixelX">The filter region's pixel-space X origin, which can be negative or beyond <paramref name="canvas"/>'s own width.</param>
    /// <param name="pixelY">The filter region's pixel-space Y origin, which can be negative or beyond <paramref name="canvas"/>'s own height.</param>
    /// <param name="canvas">The real surface every other element also renders onto.</param>
    /// <param name="opacity">
    ///     The referencing element's own cascaded <see cref="RenderState.Opacity"/>, applied here as
    ///     a uniform per-pixel coverage multiplier - the same mechanism <see cref="ResolvePaint"/>
    ///     already relies on for ordinary fill/stroke opacity - so it affects the filtered result as
    ///     a whole exactly once, rather than the pre-filter <c>SourceGraphic</c> (see
    ///     <see cref="RenderFilteredShape"/>'s remarks).
    /// </param>
    private static void CompositeFilterResultOntoCanvas(Surface result, int pixelX, int pixelY, Surface canvas, float opacity)
    {
        var coverage = new float[result.Width];
        Array.Fill(coverage, opacity);

        for (var row = 0; row < result.Height; row++)
        {
            var canvasY = pixelY + row;
            if (canvasY < 0 || canvasY >= canvas.Height)
            {
                continue;
            }

            var startCol = Math.Max(0, -pixelX);
            var endCol = Math.Min(result.Width, canvas.Width - pixelX);
            if (endCol <= startCol)
            {
                continue;
            }

            var length = endCol - startCol;
            var rowSpan = result.GetRowSpan(row);
            canvas.CompositeOverSpan(canvasY, pixelX + startCol, coverage.AsSpan(0, length), rowSpan.Slice(startCol, length));
        }
    }
}
