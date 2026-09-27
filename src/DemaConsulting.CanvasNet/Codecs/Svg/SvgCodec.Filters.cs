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
    ///     <see cref="ResolveMarkerElement"/>. Filter support applies per-element only: it is never
    ///     invoked for a <c>g</c>/<c>symbol</c> group (group-level filtering is out of scope) and
    ///     never applied to a shape's own marker content (markers always render directly onto
    ///     <paramref name="context"/>'s surface, unaffected by the referencing shape's own
    ///     <c>filter</c>) - see <see cref="RenderFilteredShape"/>'s remarks for the full filter
    ///     evaluation pipeline.
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
    ///     <see cref="ComputeFilterRegionPixelBounds"/> for the region computation itself.
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
        XElement filterElement, Path localPath, RenderState state, Matrix3x2 transform)
    {
        var bounds = ExpandBoundsForStroke(localPath.GetBounds(), state);
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

        if (localRegion.Width <= 0f || localRegion.Height <= 0f)
        {
            return null;
        }

        var pixelRegion = localRegion.Transform(transform);
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
