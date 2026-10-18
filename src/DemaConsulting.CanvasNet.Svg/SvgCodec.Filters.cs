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

namespace DemaConsulting.CanvasNet.Svg;

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
    ///     Renders <paramref name="localPath"/> through <paramref name="element"/>'s own
    ///     <c>filter</c>/<c>clip-path</c>/<c>mask</c> presentation attributes, if any, otherwise
    ///     through the ordinary unaffected <see cref="RenderShape"/> pipeline.
    /// </summary>
    /// <param name="element">
    ///     The originating element (a shape, or a <c>text</c> element whose already-laid-out
    ///     glyph-run outline is passed as <paramref name="localPath"/>), whose own <c>filter</c>/
    ///     <c>clip-path</c>/<c>mask</c> attributes are read directly - none of the three cascade
    ///     through <see cref="RenderState"/>, unlike <c>fill</c>/<c>stroke</c>/marker
    ///     specifications, per CSS/SVG semantics.
    /// </param>
    /// <param name="localPath">The shape's (or glyph run's) already-built local-space outline.</param>
    /// <param name="state">The cascaded render state.</param>
    /// <param name="transform">The accumulated transform from local space into pixel space.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="scope">
    ///     The recursion depths and shared work budgets. <see cref="RenderScope.UseDepth"/> and
    ///     <see cref="RenderScope.ElementDepth"/> are forwarded so a <c>mask</c>'s own content
    ///     (rendered through the ordinary <see cref="RenderElement"/> walk) can enforce
    ///     <see cref="MaxUseDepth"/>/<see cref="MaxElementDepth"/> - the latter is also this
    ///     codec's only guard against a <c>mask</c>-reference cycle (see <see cref="ApplyMask"/>'s
    ///     remarks). <see cref="RenderScope.MarkerDepth"/> greater than zero means
    ///     <paramref name="element"/> is being rendered as part of a <c>marker</c> element's own
    ///     content, so <paramref name="element"/>'s own <c>filter</c>/<c>clip-path</c>/<c>mask</c>
    ///     attributes are never resolved/evaluated at all, regardless of what they reference,
    ///     generalizing the pre-existing documented "filters on marker content have no effect"
    ///     scope decision (see this method's remarks) uniformly to all three effects.
    ///     <see cref="RenderScope.WorkBudget"/> is forwarded to <c>clip-path</c> geometry building
    ///     and to a <c>mask</c>'s own content. <see cref="RenderScope.FilterWorkBudget"/> is
    ///     forwarded to <see cref="RenderShapeEffectsPipeline"/> - also charged for the offscreen
    ///     buffer a <c>clip-path</c>-only or <c>mask</c>-only (no <c>filter</c>) application
    ///     allocates, see that method's remarks. <see cref="RenderScope.BoundsPrePassBudget"/> is
    ///     forwarded so a <c>mask</c>'s own content remains subject to it if it itself contains a
    ///     filtered group.
    /// </param>
    /// <param name="totalElements">The running total-rendered-elements count, forwarded so a <c>mask</c>'s own content is charged against <see cref="MaxTotalRenderedElements"/> exactly like any other rendered subtree.</param>
    /// <remarks>
    ///     A <c>filter</c>/<c>clip-path</c>/<c>mask</c> value of <c>none</c>/absent/not
    ///     <c>url(#id)</c> syntax, a dangling id, or an id resolving to an element not literally
    ///     named <c>filter</c>/<c>clipPath</c>/<c>mask</c> respectively are all tolerated by
    ///     rendering <paramref name="localPath"/> normally through <see cref="RenderShape"/> when
    ///     none of the three resolve - the same dangling-reference tolerance convention as
    ///     <see cref="ResolvePaint"/> and <see cref="ResolveMarkerElement"/>. This method itself
    ///     handles only one shape (or glyph run)'s own effects; a <c>g</c>/<c>symbol</c>/<c>use</c>
    ///     element's own <c>filter</c>/<c>clip-path</c>/<c>mask</c> attributes (applying to its
    ///     whole subtree as one combined unit) are instead handled by
    ///     <see cref="RenderGroupWithEffects"/>, dispatched directly from
    ///     <see cref="RenderElement"/>'s <c>"g"</c>/<c>"symbol"</c> case and from
    ///     <see cref="RenderUse"/> - this method is never itself invoked for those container
    ///     elements. None of the three attributes are ever applied to a shape's own marker content
    ///     either way (markers always render directly onto <paramref name="context"/>'s surface,
    ///     unaffected by the referencing shape's own effects) - see
    ///     <see cref="RenderShapeEffectsPipeline"/>'s remarks for the full ordering/evaluation
    ///     pipeline.
    /// </remarks>
    private static void RenderShapeWithEffects(
        XElement element,
        Path localPath,
        RenderState state,
        Matrix3x2 transform,
        RenderContext context,
        RenderScope scope,
        ref int totalElements)
    {
        var effects = ResolveEffectsSet(element, context, suppressEffects: scope.MarkerDepth > 0);
        if (effects.Filter == null && effects.ClipPath == null && effects.Mask == null)
        {
            RenderShape(localPath, state, transform, context, scope, ref totalElements);
            return;
        }

        RenderShapeEffectsPipeline(effects, localPath, state, transform, context, scope, ref totalElements);
    }

    /// <summary>
    ///     Resolves <paramref name="element"/>'s own <c>filter</c>/<c>clip-path</c>/<c>mask</c>
    ///     references, or an all-<see langword="null"/> set when effects are suppressed.
    /// </summary>
    /// <param name="element">The element whose own effect properties are resolved.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="suppressEffects">
    ///     <see langword="true"/> when the element is part of a <c>marker</c>'s own content, in
    ///     which case no effect is ever resolved.
    /// </param>
    /// <returns>The resolved effects.</returns>
    private static EffectsSet ResolveEffectsSet(XElement element, RenderContext context, bool suppressEffects) =>
        suppressEffects
            ? default
            : new EffectsSet(
                ResolveFilterElement(element, context),
                ResolveClipPathElement(element, context),
                ResolveMaskElement(element, context));

    /// <summary>
    ///     Resolves <paramref name="element"/>'s own <c>filter</c> property (<c>url(#id)</c>) to
    ///     its referenced <c>filter</c> element - through this class's exact same 3-tier CSS
    ///     precedence chokepoint <see cref="ApplyPresentationAttributes"/> already uses (see
    ///     <see cref="ResolveElementProperty"/>), so a matching stylesheet rule or inline
    ///     <c>style</c> declaration for <c>filter</c> is honored exactly like the plain presentation
    ///     attribute - then reusing the exact same <c>url(#id)</c>-parsing and dangling-reference
    ///     tolerance as <see cref="ResolvePaint"/>/<see cref="ResolveMarkerElement"/>.
    /// </summary>
    /// <param name="element">The element whose own <c>filter</c> property is resolved.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <returns>
    ///     The referenced <c>filter</c> element, or <see langword="null"/> if no tier has a value,
    ///     the value is not <c>url(#id)</c> syntax, the id is dangling, or the resolved element is
    ///     not literally a <c>filter</c>.
    /// </returns>
    private static XElement? ResolveFilterElement(XElement element, RenderContext context)
    {
        var spec = ResolveElementProperty(element, "filter", context);
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
    ///     Renders <paramref name="localPath"/> through the unified <c>clip-path</c> &#8594;
    ///     <c>mask</c> &#8594; <c>filter</c> effects pipeline, per SVG's defined per-element
    ///     processing order (geometry &#8594; fill/stroke &#8594; clip &#8594; mask &#8594; filter
    ///     &#8594; group opacity): all painted content is rendered once into a single temporary,
    ///     region-sized offscreen <see cref="Surface"/> ("content"), the <c>clipPath</c>
    ///     (if any) and then the <c>mask</c> (if any) attenuate that buffer's own
    ///     alpha channel in place, the <c>filter</c> (if any) then evaluates its own
    ///     primitive chain against the already-clipped/masked buffer, and the result is finally
    ///     composited onto <paramref name="context"/>'s real surface with <paramref name="state"/>'s
    ///     own cascaded opacity applied exactly once, last.
    /// </summary>
    /// <param name="effects">The resolved <c>filter</c>/<c>clipPath</c>/<c>mask</c> elements; each is <see langword="null"/> if it does not apply.</param>
    /// <param name="localPath">The shape's (or glyph run's) already-built local-space outline.</param>
    /// <param name="state">The cascaded render state.</param>
    /// <param name="transform">The accumulated transform from local space into pixel space.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="scope">
    ///     The recursion depths and shared work budgets, forwarded to the <c>mask</c>'s own content
    ///     rendering (see <see cref="ApplyMask"/>; <see cref="RenderScope.ElementDepth"/> is also
    ///     the sole guard against a <c>mask</c>-reference cycle, see its remarks) and to
    ///     <c>clip-path</c> child geometry building. Its <see cref="RenderScope.FilterWorkBudget"/>
    ///     is the shared cumulative filter-evaluation work budget (see <see cref="FilterWorkBudget"/>).
    ///     A real <c>filter</c> application is charged its existing region-weighted work unit
    ///     (<c>primitiveCount * area</c>) exactly as before Phase 2; a <c>clip-path</c>/<c>mask</c>
    ///     application with no filter (or whose filter did not itself survive
    ///     <see cref="IsFilterPrimitiveWorkWithinBudget"/>) is instead charged a nominal
    ///     <c>1 * area</c> - deliberately reusing this same budget class, rather than introducing a
    ///     parallel one, because a clip/mask offscreen buffer is the exact same resource dimension
    ///     (region-area-sized allocation) a filter application already charges against; one shared
    ///     cumulative ceiling bounds the combined cost of all three effects together.
    /// </param>
    /// <param name="totalElements">The running total-rendered-elements count, forwarded to the <c>mask</c>'s own content rendering.</param>
    /// <remarks>
    ///     The region this pipeline allocates its offscreen buffer at is chosen with the same
    ///     "never smaller than what a filter could need" precedence as the group counterpart of
    ///     this method (<see cref="RenderGroupWithEffects"/>): when a <c>filter</c>
    ///     is present, the region is its own <c>x</c>/<c>y</c>/<c>width</c>/<c>height</c>-derived
    ///     box (unchanged from pre-Phase-2 behavior - <c>clip-path</c>/<c>mask</c> never need to
    ///     enlarge a filter's own region, since clipping/masking only ever removes coverage);
    ///     otherwise, when a <c>mask</c> alone is present, its own region box (see
    ///     <see cref="ComputeMaskRegionLocalBounds"/>, honoring <c>maskUnits</c> properly); otherwise
    ///     (a <c>clip-path</c>-only application) the plain painted, stroke-expanded bounds, with no
    ///     <c>-10%/120%</c> expansion - there is no region-box concept for <c>clip-path</c> alone.
    ///     If no region can be established at all, this whole pipeline is skipped and
    ///     <paramref name="localPath"/> renders through the ordinary unaffected
    ///     <see cref="RenderShape"/>, exactly mirroring every other tolerant-fallback case below.
    ///     <para>
    ///     The <c>filter</c>'s own zero-primitive/per-application budget guard is
    ///     unchanged from this method's own pre-Phase-2 form (then named <c>RenderFilteredShape</c>) - if it fails, the filter itself
    ///     simply never applies (as if it were <see langword="null"/>
    ///     from that point on), but a <c>clipPath</c>/<c>mask</c>
    ///     that is also present is unaffected and still applies, since clip/mask are independent
    ///     effects from filter. Only when none of the three effects would end up applying at all
    ///     does this method fall back to the exact same zero-side-effect
    ///     <see cref="RenderShape"/> call pre-Phase-2 code took for a filter that failed its own
    ///     guard - this is what keeps a filter-only element's regression behavior byte-identical.
    ///     </para>
    ///     <para>
    ///     Per SVG semantics, <paramref name="state"/>'s own <c>opacity</c> applies to the final
    ///     (clipped/masked/filtered) result as a whole, not to the pre-effects source paint:
    ///     "content" is rendered with a copy of <paramref name="state"/> whose
    ///     <see cref="RenderState.Opacity"/> is forced to <c>1.0</c>, and <paramref name="state"/>'s
    ///     own (possibly cascaded) opacity is applied exactly once, afterward, via
    ///     <see cref="CompositeFilterResultOntoCanvas"/>'s coverage argument - unchanged from
    ///     pre-Phase-2 behavior.
    ///     </para>
    /// </remarks>
    private static void RenderShapeEffectsPipeline(
        EffectsSet effects,
        Path localPath,
        RenderState state,
        Matrix3x2 transform,
        RenderContext context,
        RenderScope scope,
        ref int totalElements)
    {
        var (filterElement, clipPathElement, maskElement) = effects;
        var rawBounds = ExpandBoundsForStroke(localPath.GetBounds(), state);

        var region = ResolveEffectsRegionPixelBounds(filterElement, clipPathElement, maskElement, rawBounds, transform);
        if (region == null)
        {
            RenderShape(localPath, state, transform, context, scope, ref totalElements);
            return;
        }

        var (pixelX, pixelY, pixelWidth, pixelHeight) = region.Value;

        // Identical zero-primitive/per-application-budget guard as this method's own pre-Phase-2
        // form (then named RenderFilteredShape)
        // - if the filter itself does not survive this check, it never applies, but this no
        // longer unconditionally falls back to plain RenderShape below: a clip-path/mask that IS
        // present must still run
        var primitiveCount = filterElement == null ? 0 : CountFilterPrimitiveWorkUnits(filterElement);
        var filterApplies = filterElement != null && primitiveCount != 0 &&
            IsFilterPrimitiveWorkWithinBudget(primitiveCount, pixelWidth, pixelHeight);

        // Nothing will actually apply (no clip, no mask, and the filter did not survive its own
        // guard) - this is exactly the pre-Phase-2 "filter absent/rejected" fast path, with zero
        // side effects, preserving that regression byte-for-byte
        if (!filterApplies && clipPathElement == null && maskElement == null)
        {
            RenderShape(localPath, state, transform, context, scope, ref totalElements);
            return;
        }

        // Charge this application's own region-weighted work unit against the shared cumulative
        // budget before allocating the offscreen "content" buffer - see this method's remarks on
        // why FilterWorkBudget is deliberately reused for clip/mask-only work too. The charge
        // covers every offscreen buffer this application will actually allocate (content, plus
        // clip-path's own coverage buffer, plus mask's own maskSource buffer) - see
        // ComputeEffectsPipelineWorkUnits's remarks for why a single one-buffer charge previously
        // under-charged a combined clip+mask (or clip/mask+filter) element.
        var workUnits = ComputeEffectsPipelineWorkUnits(
            filterApplies, primitiveCount, filterElement != null && FilterUsesSourceAlpha(filterElement), clipPathElement != null, maskElement != null, pixelWidth, pixelHeight);
        if (!scope.FilterWorkBudget.TryCharge(workUnits))
        {
            RenderShape(localPath, state, transform, context, scope, ref totalElements);
            return;
        }

        scope.FilterWorkBudget.RecordSurfaceAllocated(pixelWidth, pixelHeight);
        var content = new Surface(pixelWidth, pixelHeight);
        var localToTemp = transform * Matrix3x2.CreateTranslation(-pixelX, -pixelY);
        var tempContext = context with { Surface = content };

        // Render "content" fully opaque (Opacity forced to 1.0) rather than with the element's own
        // cascaded opacity - every downstream stage (clip/mask/filter) must evaluate against an
        // unmodified source, and the element's opacity is instead applied exactly once, afterward
        // (see this method's remarks)
        var opaqueState = state with { Opacity = 1f };
        RenderShape(localPath, opaqueState, localToTemp, tempContext, scope, ref totalElements);

        ApplyEffectsAndComposite(
            effects, filterApplies, new EffectsTarget(content, rawBounds, pixelX, pixelY, transform), state, context, scope, ref totalElements);
    }

    /// <summary>
    ///     Applies <c>clip-path</c> -> <c>mask</c> -> <c>filter</c> (in SVG's defined order) to an
    ///     already-rendered offscreen content buffer and composites the result onto the canvas
    ///     with <paramref name="state"/>'s own cascaded opacity applied exactly once, last.
    /// </summary>
    /// <param name="effects">The resolved effects.</param>
    /// <param name="filterApplies">Whether the filter survived its own primitive-count/region-area budget guard.</param>
    /// <param name="target">The offscreen content buffer and its geometry.</param>
    /// <param name="state">The referencing element's own cascaded render state.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="scope">The recursion depths and shared work budgets forwarded to mask/filter content rendering.</param>
    /// <param name="totalElements">The running total-rendered-elements count.</param>
    private static void ApplyEffectsAndComposite(
        EffectsSet effects,
        bool filterApplies,
        EffectsTarget target,
        RenderState state,
        RenderContext context,
        RenderScope scope,
        ref int totalElements)
    {
        // Per SVG's defined ordering, clip-path is applied before mask, and both before filter
        if (effects.ClipPath != null)
        {
            ApplyClipPath(effects.ClipPath, target, state, context, scope.WorkBudget, scope.FilterWorkBudget);
        }

        if (effects.Mask != null)
        {
            ApplyMask(effects.Mask, target, state, context, scope, ref totalElements);
        }

        var finalSurface = filterApplies
            ? EvaluateFilterChain(effects.Filter!, target, state, context, scope, ref totalElements)
            : target.Content;

        CompositeFilterResultOntoCanvas(finalSurface, target.PixelX, target.PixelY, context.Surface, state.Opacity);
    }

    /// <summary>
    ///     Computes the union of <paramref name="element"/>'s own subtree painted-content bounds,
    ///     expressed in the local space that <paramref name="relativeTransform"/> maps <em>from</em>
    ///     (i.e. the caller's chosen reference frame, not necessarily pixel space) - a bounds-only
    ///     mirror of <see cref="RenderElement"/>'s dispatch switch, reusing the same
    ///     <c>Build*Path</c>/<see cref="BuildGlyphRunPath"/> helpers and <see cref="ExpandBoundsForStroke"/>
    ///     instead of actually rendering anything. Used exclusively by
    ///     <see cref="RenderGroupWithEffects"/> to size a filtered group's own <c>SourceGraphic</c>
    ///     buffer before any of its content is rendered.
    /// </summary>
    /// <param name="element">The element whose own subtree bounds are computed.</param>
    /// <param name="parentState">The inherited render state from the parent element.</param>
    /// <param name="relativeTransform">
    ///     The accumulated transform from <paramref name="element"/>'s parent's own reference
    ///     frame into the caller's chosen reference frame. <see cref="RenderGroupWithEffects"/> always
    ///     starts this at <see cref="Matrix3x2.Identity"/> for a filtered group's own direct
    ///     children, so the bounds this method returns stay in the filtered group's own local
    ///     space - deliberately <em>not</em> composed with any ancestor's accumulated pixel-space
    ///     transform - exactly mirroring how a single shape's own <c>localPath.GetBounds()</c>
    ///     excludes every ancestor transform up to and including its own.
    /// </param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="scope">
    ///     The recursion depths and budgets. <see cref="RenderScope.UseDepth"/> enforces the same
    ///     <see cref="MaxUseDepth"/> ceiling <see cref="RenderUse"/> itself enforces, and
    ///     <see cref="RenderScope.ElementDepth"/> the same <see cref="MaxElementDepth"/> ceiling
    ///     <see cref="RenderElement"/> enforces (this pre-pass necessarily visits the same subtree
    ///     depth the real render pass will visit again afterward). <see cref="RenderScope.MarkerDepth"/>
    ///     is forwarded unchanged through ordinary subtree recursion and incremented only when this
    ///     method recurses into a resolved marker's own content, mirroring
    ///     <see cref="RenderOneMarker"/>'s identical <see cref="MaxMarkerDepth"/> guard.
    ///     <see cref="RenderScope.WorkBudget"/> is the geometry-parsing work budget, which
    ///     <see cref="RenderGroupWithEffects"/> always supplies as a local, independently bounded
    ///     scratch instance so this pre-pass cannot charge the same ceiling the real render pass
    ///     that follows it will also charge. <see cref="RenderScope.BoundsPrePassBudget"/> is
    ///     deliberately the shared, per-<c>Load</c>-call cumulative work budget (see
    ///     <see cref="BoundsPrePassWorkBudget"/>), charged both per element visit and per
    ///     approximate geometry-parsing cost so nested filtered groups are bounded in aggregate.
    /// </param>
    /// <param name="totalElements">
    ///     The running total-rendered-elements count. <see cref="RenderGroupWithEffects"/> always
    ///     passes a local, independently bounded scratch counter here (never the real
    ///     per-<c>Load</c>-call counter <see cref="RenderElement"/> itself threads through), so
    ///     this bounds-only pre-pass cannot charge the same ceiling the real render pass that
    ///     follows it will also charge. The same <see cref="MaxTotalRenderedElements"/> ceiling is
    ///     still enforced against whatever counter is supplied.
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
    ///     <c>SourceGraphic</c> buffer <see cref="RenderGroupWithEffects"/> sizes from this method's
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
        RenderScope scope,
        ref int totalElements)
    {
        // Mirror RenderElement's own depth/total-element guards exactly - this pre-pass walks the
        // same subtree the real render pass will walk again immediately afterward, so it must be
        // bounded by the same ceilings, just against the caller-supplied (possibly local-scratch)
        // counter/budget instances - see this method's remarks on totalElements/workBudget scoping
        if (scope.ElementDepth >= MaxElementDepth)
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
        // still accumulates across every nested RenderGroupWithEffects invocation's own pre-pass,
        // bounding the total "depth * subtree-size" work a document with many levels of nested
        // filtered groups could otherwise force (see BoundsPrePassWorkBudget's remarks)
        scope.BoundsPrePassBudget.Charge();

        var name = element.Name.LocalName;
        if (NonRenderingElements.Contains(name) || SkippedElements.Contains(name))
        {
            return null;
        }

        var state = ApplyPresentationAttributes(parentState, element, context);
        var transform = ParseTransformAttribute(element) * relativeTransform;
        if (!IsFiniteTransform(transform))
        {
            return null;
        }

        switch (name)
        {
            case "g":
            case "symbol":
                return ComputeGroupBounds(element, state, transform, context, scope, ref totalElements);

            case "rect":
                return ShapeBoundsRespectingOwnFilter(element, BuildRectPath(element, state), state, transform, context, scope.MarkerDepth);

            case "circle":
                return ShapeBoundsRespectingOwnFilter(element, BuildEllipsePath(element, isCircle: true, state), state, transform, context, scope.MarkerDepth);

            case "ellipse":
                return ShapeBoundsRespectingOwnFilter(element, BuildEllipsePath(element, isCircle: false, state), state, transform, context, scope.MarkerDepth);

            case "line":
                return ShapeAndMarkerBounds(element, BuildLinePath(element, state), state, transform, context, scope, ref totalElements);

            case "polyline":
                {
                    // Charge the shared, cumulative bounds-pre-pass geometry budget for this
                    // element's own re-parse cost too, upfront and in addition to (never instead
                    // of) BuildPolyPath's own per-invocation, local-scratch workBudget charge
                    // below - see BoundsPrePassWorkBudget.ChargeGeometry's remarks for why a
                    // single element's own geometry-parsing cost must also be bounded across
                    // every nested filtered-group level that re-visits it, not just once per
                    // element visit
                    scope.BoundsPrePassBudget.ChargeGeometry(((string?)element.Attribute("points"))?.Length ?? 0);
                    var polylinePath = BuildPolyPath(element, closed: false, scope.WorkBudget);
                    return ShapeAndMarkerBounds(element, polylinePath, state, transform, context, scope, ref totalElements);
                }

            case "polygon":
                {
                    // See the "polyline" case above for why this charge is made here.
                    scope.BoundsPrePassBudget.ChargeGeometry(((string?)element.Attribute("points"))?.Length ?? 0);
                    var polygonPath = BuildPolyPath(element, closed: true, scope.WorkBudget);
                    return ShapeAndMarkerBounds(element, polygonPath, state, transform, context, scope, ref totalElements);
                }

            case "path":
                {
                    // See the "polyline" case above for why this charge is made here (using the
                    // "d" attribute's own character count as the same kind of simple, cheap
                    // geometry-size proxy).
                    scope.BoundsPrePassBudget.ChargeGeometry(((string?)element.Attribute("d"))?.Length ?? 0);
                    var dataPath = BuildPathDataPath(element, scope.WorkBudget);
                    return ShapeAndMarkerBounds(element, dataPath, state, transform, context, scope, ref totalElements);
                }

            case "use":
                return ComputeUseBounds(element, state, transform, context, scope, ref totalElements);

            case "text":
                return ComputeTextBounds(element, state, transform, context, scope);

            default:
                // Any other element name (including future/unknown elements) paints nothing,
                // consistent with RenderElement's own out-of-scope-construct policy
                return null;
        }
    }

    /// <summary>
    ///     Computes a <c>g</c>/<c>symbol</c> element's subtree bounds for
    ///     <see cref="ComputeSubtreeLocalBounds"/>, applying the element's own filter region when it
    ///     carries one.
    /// </summary>
    /// <param name="element">The <c>g</c>/<c>symbol</c> element.</param>
    /// <param name="state">The element's own cascaded render state.</param>
    /// <param name="transform">The element's own composed transform.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="scope">The recursion depths and budgets of the visit to <paramref name="element"/>.</param>
    /// <param name="totalElements">The running total-rendered-elements count.</param>
    /// <returns>The subtree bounds, or <see langword="null"/> if nothing paints.</returns>
    private static Rect? ComputeGroupBounds(
        XElement element,
        RenderState state,
        Matrix3x2 transform,
        RenderContext context,
        RenderScope scope,
        ref int totalElements)
    {
        // Mirrors RenderElement's identical suppressEffects/ResolveFilterElement
        // derivation - a "g"/"symbol" element's own "filter" attribute never applies
        // (to its own content, as one filtered unit) when this call is itself part of
        // a marker's own content subtree
        var ownFilterElement = scope.MarkerDepth > 0 ? null : ResolveFilterElement(element, context);

        // When this element carries its own filter, its children must be visited
        // relative to this element's own pre-transform local space
        // (Matrix3x2.Identity), exactly mirroring RenderGroupWithEffects's own bounds
        // pre-pass (relativeTransform starting at Identity for a filtered group's own
        // direct children) - so the filter's objectBoundingBox-relative region
        // fractions (see ApplyOwnFilterToLocalBounds) are computed against the right
        // frame, before "transform" is applied exactly once, below. Without an own
        // filter, children are visited directly relative to this element's own
        // already-composed "transform" (this method's original behavior), so their
        // returned bounds are already in the caller's own reference frame and no
        // further Transform call is needed
        var childRelativeTransform = ownFilterElement == null ? transform : Matrix3x2.Identity;

        var childScope = scope.Nested();
        var bounds = Rect.Empty;
        foreach (var child in element.Elements())
        {
            var childBounds = ComputeSubtreeLocalBounds(child, state, childRelativeTransform, context, childScope, ref totalElements);
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

    /// <summary>
    ///     Computes a line/polyline/polygon/path element's own bounds, unioned with the bounds of
    ///     any marker content it places.
    /// </summary>
    /// <param name="element">The shape element.</param>
    /// <param name="path">The shape's built path.</param>
    /// <param name="state">The element's own cascaded render state.</param>
    /// <param name="transform">The element's own composed transform.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="scope">The recursion depths and budgets of the visit to <paramref name="element"/>.</param>
    /// <param name="totalElements">The running total-rendered-elements count.</param>
    /// <returns>The union of the shape and marker bounds, or <see langword="null"/> if neither paints.</returns>
    private static Rect? ShapeAndMarkerBounds(
        XElement element,
        Path path,
        RenderState state,
        Matrix3x2 transform,
        RenderContext context,
        RenderScope scope,
        ref int totalElements)
    {
        var shapeBounds = ShapeBoundsRespectingOwnFilter(element, path, state, transform, context, scope.MarkerDepth);
        var markerBounds = ComputeMarkerContentLocalBounds(path, state, transform, context, scope, ref totalElements);
        return UnionNullableBounds(shapeBounds, markerBounds);
    }

    /// <summary>
    ///     Computes a <c>use</c> element's resolved-target bounds for
    ///     <see cref="ComputeSubtreeLocalBounds"/>, applying the element's own filter region when
    ///     it carries one.
    /// </summary>
    /// <param name="element">The <c>use</c> element.</param>
    /// <param name="state">The element's own cascaded render state.</param>
    /// <param name="transform">The element's own composed transform.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="scope">The recursion depths and budgets of the visit to <paramref name="element"/>.</param>
    /// <param name="totalElements">The running total-rendered-elements count.</param>
    /// <returns>The target bounds, or <see langword="null"/> if nothing paints.</returns>
    private static Rect? ComputeUseBounds(
        XElement element,
        RenderState state,
        Matrix3x2 transform,
        RenderContext context,
        RenderScope scope,
        ref int totalElements)
    {
        if (scope.UseDepth >= MaxUseDepth)
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

        // Mirrors RenderUse's identical suppressEffects/ResolveFilterElement
        // derivation - a "use" element's own "filter" attribute (never the referenced
        // target's own attributes, which this same recursive call already handles
        // identically to any other descendant) applies to its resolved target as one
        // filtered unit; see ComputeGroupBounds for why the target must then
        // be visited relative to Matrix3x2.Identity instead of useTransform
        var ownFilterElement = scope.MarkerDepth > 0 ? null : ResolveFilterElement(element, context);
        var targetRelativeTransform = ownFilterElement == null ? useTransform : Matrix3x2.Identity;
        var targetBounds = ComputeSubtreeLocalBounds(target, targetState, targetRelativeTransform, context, scope.NestedUse(), ref totalElements);
        if (targetBounds == null)
        {
            return null;
        }

        return ownFilterElement == null ? targetBounds : ApplyOwnFilterToLocalBounds(ownFilterElement, targetBounds.Value, useTransform);
    }

    /// <summary>
    ///     Computes a <c>text</c> element's glyph-run bounds for
    ///     <see cref="ComputeSubtreeLocalBounds"/>.
    /// </summary>
    /// <param name="element">The <c>text</c> element.</param>
    /// <param name="state">The element's own cascaded render state.</param>
    /// <param name="transform">The element's own composed transform.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="scope">The recursion depths and budgets of the visit to <paramref name="element"/>.</param>
    /// <returns>The glyph-run bounds, or <see langword="null"/> if nothing paints.</returns>
    private static Rect? ComputeTextBounds(
        XElement element,
        RenderState state,
        Matrix3x2 transform,
        RenderContext context,
        RenderScope scope)
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
        scope.WorkBudget.Charge(text.Length);

        // Also charge the shared, cumulative bounds-pre-pass geometry budget (see the
        // "polyline"/"polygon"/"path" cases above, and BoundsPrePassWorkBudget.
        // ChargeGeometry's remarks) - a text element's own glyph-run layout cost below
        // is likewise proportional to its character count, and re-parsed once per
        // nesting level under many nested filtered groups exactly like path/points
        // data would be
        scope.BoundsPrePassBudget.ChargeGeometry(text.Length);

        var origin = new Vector2(
            GetFloatAttribute(element, "x", state, PercentageAxis.Horizontal),
            GetFloatAttribute(element, "y", state, PercentageAxis.Vertical));
        var glyphRunPath = BuildGlyphRunPath(text, font, state, origin);
        return ShapeBoundsRespectingOwnFilter(element, glyphRunPath, state, transform, context, scope.MarkerDepth);
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
    ///     <see cref="RenderShapeWithEffects"/>, sharing its exact <see cref="ResolveFilterElement"/>/
    ///     <paramref name="markerDepth"/>-suppression decision, so a filtered ancestor group's own
    ///     offscreen buffer is sized large enough to contain this shape's own filtered output too
    ///     (see this class's remarks on nested group filtering), not just its raw stroke-expanded
    ///     outline.
    /// </summary>
    /// <param name="element">
    ///     The originating element, whose own <c>filter</c> attribute is read directly - identical
    ///     to <see cref="RenderShapeWithEffects"/>'s own <paramref name="element"/> parameter.
    /// </param>
    /// <param name="localPath">The shape's (or glyph run's) already-built local-space outline.</param>
    /// <param name="state">The cascaded render state.</param>
    /// <param name="transform">The accumulated transform from local space into the caller's reference frame.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="markerDepth">
    ///     The current <c>marker</c>-reference nesting depth - when greater than zero (this call is
    ///     itself part of a marker's own content subtree), <paramref name="element"/>'s own
    ///     <c>filter</c> attribute is never resolved, identical to <see cref="RenderShapeWithEffects"/>'s
    ///     own <c>suppressEffects</c> derivation in <see cref="RenderElement"/>/<see cref="RenderText"/>.
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
    ///     <see cref="RenderShapeEffectsPipeline"/>/<see cref="RenderGroupWithEffects"/> themselves make at
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
    ///     <see cref="RenderShapeEffectsPipeline"/>/<see cref="RenderGroupWithEffects"/> call remains the only
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

        // A zero-primitive filter can never change the rendered output (see RenderShapeEffectsPipeline's
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
    ///     Renders every element in <paramref name="children"/> as one combined unit through the
    ///     same unified clip &#8594; mask &#8594; filter effects pipeline as
    ///     <see cref="RenderShapeEffectsPipeline"/> - the group-level counterpart, sharing its
    ///     exact offscreen-render/clip/mask/filter/composite ordering and every one of its
    ///     resource-safety guards, but sized from the union of <paramref name="children"/>'s own
    ///     subtree bounds (via <see cref="ComputeSubtreeLocalBounds"/>) instead of a single
    ///     shape's own outline.
    /// </summary>
    /// <param name="effects">The resolved <c>filter</c>/<c>clip-path</c>/<c>mask</c> elements; each may be <see langword="null"/> if none applies.</param>
    /// <param name="children">
    ///     The elements to render as one combined unit - a <c>g</c>/<c>symbol</c> element's own
    ///     direct children (see <see cref="RenderElement"/>'s <c>"g"</c>/<c>"symbol"</c> case), or
    ///     a single-element list containing a <c>use</c> element's resolved target (see
    ///     <see cref="RenderUse"/>).
    /// </param>
    /// <param name="state">The cascaded render state at the referencing group/use element itself.</param>
    /// <param name="childrenTransform">
    ///     The accumulated transform from <paramref name="children"/>'s own shared local space
    ///     (the space <see cref="ComputeSubtreeLocalBounds"/> computes bounds in, starting from
    ///     <see cref="Matrix3x2.Identity"/>) into pixel space - for the <c>g</c>/<c>symbol</c>
    ///     case, this is the group's own already-composed transform; for the <c>use</c> case, this
    ///     is <c>translate(x,y) * (the use element's own already-composed transform)</c>.
    /// </param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="scope">
    ///     The recursion depths and shared work budgets at the referencing group/use element
    ///     itself - <paramref name="children"/> are always rendered/bounds-computed one level
    ///     deeper (<see cref="RenderScope.ElementDepth"/> plus one), mirroring the unaffected
    ///     <c>g</c> loop and unaffected <c>use</c> re-entry this method replaces. Its
    ///     <see cref="RenderScope.FilterWorkBudget"/> is charged identically (same region-weighted
    ///     work-unit formula, same <see cref="FilterWorkBudget.TryCharge"/> call) to
    ///     <see cref="RenderShapeEffectsPipeline"/>'s own charge, so group filters and shape
    ///     filters share one running cumulative ceiling per <c>Load</c> call - and also the
    ///     region-weighted charge for a clip-path/mask-only (no filter) application, see
    ///     <see cref="RenderShapeEffectsPipeline"/>'s remarks for why this budget is deliberately
    ///     reused rather than duplicated. Its <see cref="RenderScope.BoundsPrePassBudget"/> is
    ///     the shared, per-<c>Load</c>-call cumulative bounds-pre-pass work budget (see
    ///     <see cref="BoundsPrePassWorkBudget"/>), charged by this method's own bounds pre-pass -
    ///     unlike the fresh local-scratch <c>totalElements</c> counter/
    ///     <see cref="GeometryWorkBudget"/> instance the pre-pass otherwise uses (see this
    ///     method's remarks), this ONE instance is shared by every <see cref="RenderGroupWithEffects"/>
    ///     invocation (nested or sibling) for the whole document, so a document with many levels
    ///     of nested filtered groups cannot force unbounded total pre-pass work merely because
    ///     each nesting level's own pre-pass otherwise "resets" its own local-scratch ceilings.
    /// </param>
    /// <param name="totalElements">The running total-rendered-elements count.</param>
    /// <remarks>
    ///     If <paramref name="children"/>'s combined subtree paints nothing (an empty group, or a
    ///     group/subtree consisting entirely of non-rendering/skipped/dangling content), no region
    ///     can be computed at all, and this whole method falls back to rendering every element in
    ///     <paramref name="children"/> directly (unaffected), exactly as if the referencing
    ///     <c>g</c>/<c>symbol</c>/<c>use</c> element had none of <c>filter</c>/<c>clip-path</c>/
    ///     <c>mask</c> at all. When a region IS available, the filter's own
    ///     zero-primitive/budget guard is evaluated exactly as this method's own pre-Phase-2 form (then named <c>RenderFilteredGroup</c>)
    ///     did - but, per <see cref="RenderShapeEffectsPipeline"/>'s identical generalization, a
    ///     filter that does not survive this guard no longer forces the whole method back to the
    ///     plain unaffected fallback when a clip-path/mask is also present; only when none of the
    ///     three effects would apply at all does the original zero-side-effect fallback occur,
    ///     preserving the filter-only regression byte-for-byte.
    ///     <para>
    ///     <paramref name="state"/>'s own (possibly cascaded) <see cref="RenderState.Opacity"/> is
    ///     applied exactly once, after clip/mask/filter all evaluate, via
    ///     <see cref="CompositeFilterResultOntoCanvas"/> - <paramref name="children"/> are rendered
    ///     into "content" with a copy of <paramref name="state"/> whose
    ///     <see cref="RenderState.Opacity"/> is forced to <c>1.0</c>, identical in shape to
    ///     <see cref="RenderShapeEffectsPipeline"/>'s own opacity handling. Because
    ///     <see cref="ApplyPresentationAttributes"/> already multiplicatively folds an element's
    ///     own <c>opacity</c> attribute into <paramref name="state"/>'s <see cref="RenderState.Opacity"/>
    ///     before <see cref="RenderElement"/> ever dispatches into the <c>"g"</c>/<c>"use"</c> case,
    ///     this reproduces "group opacity applies once, after clip/mask/filter" with no further
    ///     special-casing - each child's own <c>opacity</c> attribute still folds multiplicatively
    ///     underneath, per pixel, exactly as it does today.
    ///     </para>
    ///     <para>
    ///     A clip-path/mask on a descendant does not need to be accounted for by this method's own
    ///     bounds pre-pass below (<see cref="ComputeSubtreeLocalBounds"/> is unmodified for Phase 2): unlike a filter,
    ///     which can paint pixels beyond a shape's own raw geometry (blur, flood, ...), clip-path/
    ///     mask only ever attenuate coverage that some underlying shape's own geometry already
    ///     paints - they never require the ancestor's own offscreen buffer to be sized any larger
    ///     than that geometry's own (possibly filter-expanded) bounds already account for.
    ///     </para>
    /// </remarks>
    private static void RenderGroupWithEffects(
        EffectsSet effects,
        IReadOnlyList<XElement> children,
        RenderState state,
        Matrix3x2 childrenTransform,
        RenderContext context,
        RenderScope scope,
        ref int totalElements)
    {
        var (filterElement, clipPathElement, maskElement) = effects;
        var childScope = scope.Nested();
        var localBounds = ComputeChildrenLocalBounds(children, state, context, childScope);

        // Every tolerant-fallback guard below renders each child directly and unaffected, exactly
        // as if the referencing element had none of "filter"/"clip-path"/"mask"
        if (localBounds.IsEmpty)
        {
            RenderChildrenDirect(children, state, childrenTransform, context, childScope, ref totalElements);
            return;
        }

        // Region precedence identical to RenderShapeEffectsPipeline: a filter's own region wins
        // (it may need to expand beyond the raw painted bounds), otherwise a mask's own region,
        // otherwise (clip-path only) the plain painted bounds with no expansion
        var region = ResolveEffectsRegionPixelBounds(filterElement, clipPathElement, maskElement, localBounds, childrenTransform);
        if (region == null)
        {
            RenderChildrenDirect(children, state, childrenTransform, context, childScope, ref totalElements);
            return;
        }

        var (pixelX, pixelY, pixelWidth, pixelHeight) = region.Value;

        // Identical zero-primitive/per-application-budget guard as RenderShapeEffectsPipeline -
        // see its remarks for the full rationale, including why a filter that fails this guard no
        // longer unconditionally forces the plain fallback when clip/mask is also present
        var primitiveCount = filterElement == null ? 0 : CountFilterPrimitiveWorkUnits(filterElement);
        var filterApplies = filterElement != null && primitiveCount != 0 &&
            IsFilterPrimitiveWorkWithinBudget(primitiveCount, pixelWidth, pixelHeight);

        if (!filterApplies && clipPathElement == null && maskElement == null)
        {
            RenderChildrenDirect(children, state, childrenTransform, context, childScope, ref totalElements);
            return;
        }

        // Identical cumulative-budget guard as RenderShapeEffectsPipeline - every effects
        // application (shape or group; filter, clip-path, or mask) shares one running total per
        // Load call, and the charge covers every offscreen buffer this application will actually
        // allocate - see ComputeEffectsPipelineWorkUnits's remarks
        var workUnits = ComputeEffectsPipelineWorkUnits(
            filterApplies, primitiveCount, filterElement != null && FilterUsesSourceAlpha(filterElement), clipPathElement != null, maskElement != null, pixelWidth, pixelHeight);
        if (!scope.FilterWorkBudget.TryCharge(workUnits))
        {
            RenderChildrenDirect(children, state, childrenTransform, context, childScope, ref totalElements);
            return;
        }

        scope.FilterWorkBudget.RecordSurfaceAllocated(pixelWidth, pixelHeight);
        var content = new Surface(pixelWidth, pixelHeight);
        var localToTemp = childrenTransform * Matrix3x2.CreateTranslation(-pixelX, -pixelY);
        var tempContext = context with { Surface = content };

        // Render every child fully opaque (Opacity forced to 1.0), identical in shape to
        // RenderShapeEffectsPipeline's own "content" rendering - see this method's remarks
        RenderChildrenDirect(children, state with { Opacity = 1f }, localToTemp, tempContext, childScope, ref totalElements);

        ApplyEffectsAndComposite(
            effects, filterApplies, new EffectsTarget(content, localBounds, pixelX, pixelY, childrenTransform), state, context, childScope, ref totalElements);
    }

    /// <summary>
    ///     Renders every element of <paramref name="children"/> directly (unaffected by any group
    ///     effect) through <see cref="RenderElement"/>.
    /// </summary>
    /// <param name="children">The elements to render.</param>
    /// <param name="state">The cascaded render state.</param>
    /// <param name="transform">The accumulated transform into the target surface's pixel space.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="childScope">The recursion depths and budgets each child is rendered with.</param>
    /// <param name="totalElements">The running total-rendered-elements count.</param>
    private static void RenderChildrenDirect(
        IReadOnlyList<XElement> children,
        RenderState state,
        Matrix3x2 transform,
        RenderContext context,
        RenderScope childScope,
        ref int totalElements)
    {
        foreach (var child in children)
        {
            RenderElement(child, state, transform, context, childScope, ref totalElements);
        }
    }

    /// <summary>
    ///     Bounds pre-pass for <see cref="RenderGroupWithEffects"/>: unions every child's own
    ///     subtree bounds, computed purely in the group's own local space (relative transform
    ///     starts at identity) - mirrors a single shape's own <c>localPath.GetBounds()</c> call,
    ///     just folded over an entire subtree instead of one already-built path.
    /// </summary>
    /// <param name="children">The elements whose subtree bounds are unioned.</param>
    /// <param name="state">The cascaded render state at the referencing element.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="childScope">The scope the children are visited with; only its depth counters and shared pre-pass budget are reused.</param>
    /// <returns>The union of the children's bounds, or <see cref="Rect.Empty"/> if nothing paints.</returns>
    /// <remarks>
    ///     Deliberately uses fresh, local scratch counter/budget instances rather than the real
    ///     total-element counter and geometry work budget threaded through the render pass: this
    ///     pre-pass necessarily re-visits the same subtree the real render pass visits again
    ///     immediately afterward, so charging the same counter/budget instance in both passes would
    ///     double (or, combined with the tolerant-fallback re-render, triple) charge every element
    ///     under a filtered group against the fixed ceilings - a document whose unfiltered
    ///     rendering would legitimately stay under those ceilings could then throw
    ///     <see cref="InvalidDataException"/> purely because some of its content sits inside a
    ///     filtered group. The local instances still enforce the exact same ceilings (see
    ///     <see cref="ComputeSubtreeLocalBounds"/>'s remarks), so this pre-pass itself cannot run
    ///     away on a pathologically large subtree. The bounds pre-pass budget is deliberately
    ///     <em>not</em> a fresh local instance - it is the one shared, per-<c>Load</c>-call
    ///     instance, so every nested/sibling invocation contributes toward ONE cumulative ceiling
    ///     (see <see cref="BoundsPrePassWorkBudget"/>'s remarks for the full rationale).
    /// </remarks>
    private static Rect ComputeChildrenLocalBounds(
        IReadOnlyList<XElement> children,
        RenderState state,
        RenderContext context,
        RenderScope childScope)
    {
        var preRenderElementCount = 0;
        var preRenderScope = childScope with { WorkBudget = new GeometryWorkBudget() };
        var localBounds = Rect.Empty;
        foreach (var child in children)
        {
            var childBounds = ComputeSubtreeLocalBounds(child, state, Matrix3x2.Identity, context, preRenderScope, ref preRenderElementCount);
            if (childBounds != null)
            {
                localBounds = localBounds.Union(childBounds.Value);
            }
        }

        return localBounds;
    }

    /// <summary>
    ///     Computes <paramref name="filterElement"/>'s filter region from an already-computed
    ///     local-space painted-content bounding box - the shared core shared by the single-shape
    ///     overload above (which derives <paramref name="bounds"/> from one shape's own
    ///     stroke-expanded outline) and <see cref="RenderGroupWithEffects"/> (which derives
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
        return localRegion == null ? null : ConvertLocalRegionToPixelBounds(localRegion.Value, transform);
    }

    /// <summary>
    ///     Resolves the pixel-space region an effects pipeline (shape or group) should allocate its
    ///     offscreen buffer at, applying the shared filter &gt; mask &gt; plain-bounds precedence
    ///     used identically by both <see cref="RenderShapeEffectsPipeline"/> and
    ///     <see cref="RenderGroupWithEffects"/> - factored out into its own method purely to avoid
    ///     a nested ternary expression at each call site.
    /// </summary>
    /// <param name="filterElement">The resolved <c>filter</c> element, or <see langword="null"/> if none applies.</param>
    /// <param name="clipPathElement">
    ///     The resolved <c>clipPath</c> element - unused by the precedence logic itself (a
    ///     <c>clip-path</c> never has its own region-box concept), but accepted so every call site
    ///     passes the full resolved-element triple for symmetry/future-proofing.
    /// </param>
    /// <param name="maskElement">The resolved <c>mask</c> element, or <see langword="null"/> if none applies.</param>
    /// <param name="bounds">The referencing element's (or group subtree's) already stroke-expanded local-space painted-content bounds.</param>
    /// <param name="transform">The accumulated transform from local space into pixel space.</param>
    /// <returns>
    ///     <paramref name="filterElement"/>'s own filter region if present; otherwise
    ///     <paramref name="maskElement"/>'s own mask region if present; otherwise
    ///     <paramref name="bounds"/> converted directly with no region-box expansion; or
    ///     <see langword="null"/> if the chosen region does not resolve to a valid pixel-space box.
    /// </returns>
    private static (int X, int Y, int Width, int Height)? ResolveEffectsRegionPixelBounds(
        XElement? filterElement, XElement? clipPathElement, XElement? maskElement, Rect bounds, Matrix3x2 transform)
    {
        _ = clipPathElement;

        if (filterElement != null)
        {
            return ComputeFilterRegionPixelBounds(filterElement, bounds, transform);
        }

        return maskElement != null
            ? ComputeMaskRegionPixelBounds(maskElement, bounds, transform)
            : ConvertLocalRegionToPixelBounds(bounds, transform);
    }

    /// <summary>
    ///     Converts an already-computed local-space region into an integer pixel-space bounding
    ///     box, rounded outward - the shared pixel-space conversion/validation core factored out
    ///     of <see cref="ComputeFilterRegionPixelBounds(XElement, Rect, Matrix3x2)"/> so
    ///     <see cref="ComputeMaskRegionPixelBounds"/> and a plain <c>clip-path</c>-only application
    ///     (which has no region-box concept of its own, and so passes its raw painted bounds
    ///     directly) can reuse the exact same finiteness/<see cref="MaxCoordinateMagnitude"/>/
    ///     <see cref="Surface.MaxDimension"/> checks without duplicating them.
    /// </summary>
    /// <param name="localRegion">The already-computed local-space region.</param>
    /// <param name="transform">The accumulated transform from local space into pixel space.</param>
    /// <returns>
    ///     The pixel-space region as <c>(X, Y, Width, Height)</c>, or <see langword="null"/> if
    ///     <paramref name="localRegion"/>'s pixel-space transform is non-finite, non-positive, or
    ///     exceeds <see cref="MaxCoordinateMagnitude"/>, or its rounded pixel size exceeds
    ///     <see cref="Surface.MaxDimension"/> on either axis.
    /// </returns>
    private static (int X, int Y, int Width, int Height)? ConvertLocalRegionToPixelBounds(Rect localRegion, Matrix3x2 transform)
    {
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
    ///     Computes the total <see cref="FilterWorkBudget"/> charge for one effects-pipeline
    ///     application (a single shape in <see cref="RenderShapeEffectsPipeline"/>, or a whole
    ///     group in <see cref="RenderGroupWithEffects"/>), shared by both so the two call sites
    ///     cannot drift apart. A region-area-sized offscreen <see cref="Surface"/> is allocated for
    ///     every one of the (up to three) effects that actually applies to this element - the
    ///     pipeline's own opaque "content"/<c>SourceGraphic</c> buffer always, plus
    ///     <see cref="ApplyClipPath"/>'s own <c>coverage</c> buffer when a <c>clip-path</c>
    ///     applies, plus <see cref="ApplyMask"/>'s own <c>maskSource</c> buffer when a
    ///     <c>mask</c> applies - so charging for only one of these (the pre-Phase-2 formula, which
    ///     predates clip-path/mask ever being combined with each other or with a filter) let a
    ///     combined clip+mask, or clip/mask+filter, element allocate up to 3x the memory actually
    ///     charged against the shared cumulative budget. The filter's own primitive-count
    ///     multiplier applies only to the "content" buffer's own portion of the charge (mirroring
    ///     <see cref="IsFilterPrimitiveWorkWithinBudget"/>'s identical per-filter formula) - the
    ///     additional clip-path/mask buffers are always a flat one region-area unit each,
    ///     regardless of whether a filter is also present, since neither buffer's own cost scales
    ///     with the filter's primitive count.
    /// </summary>
    /// <param name="filterApplies">
    ///     Whether a <c>filter</c> survives its own per-filter <see cref="IsFilterPrimitiveWorkWithinBudget"/>
    ///     guard and will therefore be evaluated against the "content" buffer.
    /// </param>
    /// <param name="primitiveCount">
    ///     The filter's own primitive-equivalent work-unit count from
    ///     <see cref="CountFilterPrimitiveWorkUnits"/>; ignored when <paramref name="filterApplies"/>
    ///     is <see langword="false"/>.
    /// </param>
    /// <param name="sourceAlphaUsed">
    ///     Whether any primitive references <c>SourceAlpha</c>, which allocates one more shared
    ///     region-area-sized buffer; ignored when <paramref name="filterApplies"/> is <see langword="false"/>.
    /// </param>
    /// <param name="clipPathApplies">
    ///     Whether a <c>clip-path</c> element is present and will therefore cause
    ///     <see cref="ApplyClipPath"/> to allocate its own additional region-area-sized <c>coverage</c>
    ///     buffer.
    /// </param>
    /// <param name="maskApplies">
    ///     Whether a <c>mask</c> element is present and will therefore cause <see cref="ApplyMask"/>
    ///     to allocate its own additional region-area-sized <c>maskSource</c> buffer.
    /// </param>
    /// <param name="pixelWidth">The effects region's pixel width.</param>
    /// <param name="pixelHeight">The effects region's pixel height.</param>
    /// <returns>
    ///     The total work-unit charge to pass to <see cref="FilterWorkBudget.TryCharge"/>, covering
    ///     every offscreen buffer this application will actually allocate.
    /// </returns>
    private static long ComputeEffectsPipelineWorkUnits(
        bool filterApplies,
        int primitiveCount,
        bool sourceAlphaUsed,
        bool clipPathApplies,
        bool maskApplies,
        int pixelWidth,
        int pixelHeight)
    {
        var regionArea = (long)pixelWidth * pixelHeight;

        // The "content"/SourceGraphic buffer's own portion - scaled by the filter's own primitive
        // count when a filter applies, exactly as this codec charged before Phase 2 introduced
        // combinable clip-path/mask
        // The content buffer itself is one further region-area unit on top of the primitive units
        // (primitive outputs and subregion clip copies), plus one for the lazily built SourceAlpha
        // buffer when a primitive references it. These fixed units are deliberately NOT part of
        // primitiveCount, so they never count toward MaxFilterPrimitivesPerFilter.
        var contentSurfaces = filterApplies ? (long)primitiveCount + 1 : 1;
        if (filterApplies && sourceAlphaUsed)
        {
            contentSurfaces++;
        }

        var contentWorkUnits = contentSurfaces * regionArea;

        // One additional flat region-area unit for each of clip-path/mask's own separate
        // offscreen buffer, since neither buffer's cost scales with the filter's primitive count
        var additionalBufferCount = (clipPathApplies ? 1 : 0) + (maskApplies ? 1 : 0);

        return contentWorkUnits + (regionArea * additionalBufferCount);
    }

    /// <summary>
    ///     Records a filter-primitive output rectangle in filter-buffer-local pixel coordinates.
    /// </summary>
    /// <param name="X">The local X origin.</param>
    /// <param name="Y">The local Y origin.</param>
    /// <param name="Width">The local width.</param>
    /// <param name="Height">The local height.</param>
    private readonly record struct PixelRect(int X, int Y, int Width, int Height)
    {
        /// <summary>Gets a value indicating whether this rectangle has no positive area.</summary>
        public bool IsEmpty => Width <= 0 || Height <= 0;
    }

    /// <summary>
    ///     Converts a float-valued channel back into a clamped byte using this codec's standard
    ///     round-half-away-from-zero convention.
    /// </summary>
    /// <param name="value">The channel value to convert.</param>
    /// <returns>The clamped byte result.</returns>
    private static byte ToByte(float value) =>
        (byte)Math.Clamp(MathF.Round(value, MidpointRounding.AwayFromZero), 0f, 255f);

    /// <summary>
    ///     Determines whether <paramref name="primitive"/> declares any explicit primitive
    ///     subregion attribute.
    /// </summary>
    /// <param name="primitive">The primitive to inspect.</param>
    /// <returns><see langword="true"/> when any of <c>x</c>/<c>y</c>/<c>width</c>/<c>height</c> is present.</returns>
    private static bool PrimitiveDeclaresSubregion(XElement primitive) =>
        primitive.Attribute("x") != null ||
        primitive.Attribute("y") != null ||
        primitive.Attribute("width") != null ||
        primitive.Attribute("height") != null;

    /// <summary>
    ///     Resolves one primitive's own <c>x</c>/<c>y</c>/<c>width</c>/<c>height</c> rectangle into
    ///     filter-buffer-local pixel coordinates, clamped to the enclosing filter region.
    /// </summary>
    /// <param name="primitive">The primitive whose own subregion is being resolved.</param>
    /// <param name="filterRegionPixelBounds">The enclosing filter region in absolute pixel coordinates.</param>
    /// <param name="regionPixelX">The enclosing filter region's absolute pixel-space X origin.</param>
    /// <param name="regionPixelY">The enclosing filter region's absolute pixel-space Y origin.</param>
    /// <param name="transform">
    ///     The accumulated transform from the referencing element's local space into pixel space.
    ///     Present for parity with the effects-pipeline call sites; primitive subregions reuse the
    ///     filter region's own objectBoundingBox-relative convention and therefore need only the
    ///     already-resolved pixel-space filter region itself.
    /// </param>
    /// <returns>The clamped, filter-buffer-local primitive rectangle.</returns>
    private static PixelRect ResolvePrimitiveSubregionPixelBounds(
        XElement primitive,
        (int X, int Y, int Width, int Height) filterRegionPixelBounds,
        int regionPixelX,
        int regionPixelY,
        Matrix3x2 transform)
    {
        _ = transform;

        var xFraction = ParseFilterRegionFraction(primitive, "x", 0f);
        var yFraction = ParseFilterRegionFraction(primitive, "y", 0f);
        var widthFraction = ParseFilterRegionFraction(primitive, "width", 1f);
        var heightFraction = ParseFilterRegionFraction(primitive, "height", 1f);

        var absoluteX = filterRegionPixelBounds.X + (xFraction * filterRegionPixelBounds.Width);
        var absoluteY = filterRegionPixelBounds.Y + (yFraction * filterRegionPixelBounds.Height);
        var absoluteWidth = widthFraction * filterRegionPixelBounds.Width;
        var absoluteHeight = heightFraction * filterRegionPixelBounds.Height;

        var unclampedMinX = (int)MathF.Floor(absoluteX) - regionPixelX;
        var unclampedMinY = (int)MathF.Floor(absoluteY) - regionPixelY;
        var unclampedMaxX = (int)MathF.Ceiling(absoluteX + absoluteWidth) - regionPixelX;
        var unclampedMaxY = (int)MathF.Ceiling(absoluteY + absoluteHeight) - regionPixelY;

        var minX = Math.Clamp(unclampedMinX, 0, filterRegionPixelBounds.Width);
        var minY = Math.Clamp(unclampedMinY, 0, filterRegionPixelBounds.Height);
        var maxX = Math.Clamp(unclampedMaxX, 0, filterRegionPixelBounds.Width);
        var maxY = Math.Clamp(unclampedMaxY, 0, filterRegionPixelBounds.Height);

        return new PixelRect(minX, minY, Math.Max(0, maxX - minX), Math.Max(0, maxY - minY));
    }

    /// <summary>
    ///     Clips one primitive output to <paramref name="subregion"/>, clearing every pixel
    ///     outside that rectangle.
    /// </summary>
    /// <param name="source">The already-evaluated primitive output.</param>
    /// <param name="subregion">The primitive's resolved, filter-buffer-local subregion.</param>
    /// <returns>A new, clipped surface.</returns>
    private static Surface ClipPrimitiveOutputToSubregion(Surface source, PixelRect subregion)
    {
        var clipped = new Surface(source.Width, source.Height);
        if (subregion.IsEmpty)
        {
            return clipped;
        }

        for (var row = 0; row < subregion.Height; row++)
        {
            var sourceRow = source.GetRowSpan(subregion.Y + row).Slice(subregion.X, subregion.Width);
            var destinationRow = clipped.GetRowSpan(subregion.Y + row).Slice(subregion.X, subregion.Width);
            sourceRow.CopyTo(destinationRow);
        }

        return clipped;
    }

    /// <summary>
    ///     Copies <paramref name="source"/> into <paramref name="destination"/> at the supplied
    ///     destination origin, clipping to the destination bounds.
    /// </summary>
    /// <param name="source">The surface to copy from.</param>
    /// <param name="destination">The surface to copy into.</param>
    /// <param name="destinationX">The destination X origin.</param>
    /// <param name="destinationY">The destination Y origin.</param>
    private static void CopySurfaceInto(Surface source, Surface destination, int destinationX, int destinationY)
    {
        for (var row = 0; row < source.Height; row++)
        {
            var destRow = destinationY + row;
            if (destRow < 0 || destRow >= destination.Height)
            {
                continue;
            }

            var startX = Math.Max(0, -destinationX);
            var endX = Math.Min(source.Width, destination.Width - destinationX);
            if (endX <= startX)
            {
                continue;
            }

            var length = endX - startX;
            var sourceRow = source.GetRowSpan(row).Slice(startX, length);
            var destinationRow = destination.GetRowSpan(destRow).Slice(destinationX + startX, length);
            sourceRow.CopyTo(destinationRow);
        }
    }

    /// <summary>
    ///     Computes the union of two primitive subregions.
    /// </summary>
    /// <param name="first">The first subregion.</param>
    /// <param name="second">The second subregion.</param>
    /// <returns>The bounding union, or the non-empty operand when only one has area.</returns>
    private static PixelRect UnionPrimitiveSubregions(PixelRect first, PixelRect second)
    {
        if (first.IsEmpty)
        {
            return second;
        }

        if (second.IsEmpty)
        {
            return first;
        }

        var minX = Math.Min(first.X, second.X);
        var minY = Math.Min(first.Y, second.Y);
        var maxX = Math.Max(first.X + first.Width, second.X + second.Width);
        var maxY = Math.Max(first.Y + first.Height, second.Y + second.Height);
        return new PixelRect(minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    ///     Counts <paramref name="filterElement"/>'s upfront primitive-equivalent work-unit charge
    ///     for <see cref="IsFilterPrimitiveWorkWithinBudget"/>: most direct children count as 1;
    ///     a <c>feMerge</c> child counts as its own <c>feMergeNode</c> count (at least 1); a
    ///     <c>feConvolveMatrix</c> child counts as <c>orderX * orderY</c> (at least 1), reflecting
    ///     its O(order<sup>2</sup> &#215; region-area) per-primitive cost; and a <c>feTurbulence</c>
    ///     child counts as its own resolved <c>numOctaves</c> (at least 1), reflecting its
    ///     O(numOctaves &#215; region-area) per-primitive cost. Every primitive that declares an
    ///     explicit subregion (<c>x</c>/<c>y</c>/<c>width</c>/<c>height</c>) is charged one further
    ///     unit for the region-sized copy <see cref="ClipPrimitiveOutputToSubregion"/> allocates, so
    ///     the upfront check and the cumulative charge cover every surface the chain allocates.
    /// </summary>
    /// <param name="filterElement">The resolved <c>filter</c> element.</param>
    /// <returns>The total primitive-equivalent work-unit count.</returns>
    private static int CountFilterPrimitiveWorkUnits(XElement filterElement) =>
        filterElement.Elements().Sum(primitive => (primitive.Name.LocalName switch
        {
            "feMerge" => Math.Max(1, primitive.Elements().Count(node => node.Name.LocalName == "feMergeNode")),
            "feConvolveMatrix" => Math.Max(1, GetConvolveMatrixOrder(primitive).X * GetConvolveMatrixOrder(primitive).Y),
            "feTurbulence" => Math.Max(1, ResolveTurbulenceNumOctaves(primitive)),
            _ => 1
        })

            // A primitive with an explicit x/y/width/height subregion additionally allocates one
            // region-sized clip copy (ClipPrimitiveOutputToSubregion), so it is charged one more unit.
            + (PrimitiveDeclaresSubregion(primitive) ? 1 : 0));

    /// <summary>
    ///     Determines whether any input <see cref="EvaluateFilterChain"/> resolves names <c>SourceAlpha</c>
    ///     (which builds one shared region-sized alpha buffer). The scan mirrors the executor exactly:
    ///     every primitive's <c>in</c> is resolved; <c>in2</c> is resolved only by <c>feComposite</c>,
    ///     <c>feBlend</c> and <c>feDisplacementMap</c>; and each <c>feMergeNode</c> child of an
    ///     <c>feMerge</c> resolves its own <c>in</c>.
    /// </summary>
    /// <param name="filterElement">The resolved <c>filter</c> element.</param>
    /// <returns><see langword="true"/> if <c>SourceAlpha</c> is referenced.</returns>
    private static bool FilterUsesSourceAlpha(XElement filterElement) =>
        filterElement.Elements().Any(primitive =>
            IsSourceAlphaName(primitive, "in") ||
            (primitive.Name.LocalName is "feComposite" or "feBlend" or "feDisplacementMap" &&
                IsSourceAlphaName(primitive, "in2")) ||
            (primitive.Name.LocalName == "feMerge" &&
                primitive.Elements().Any(node => node.Name.LocalName == "feMergeNode" && IsSourceAlphaName(node, "in"))));

    /// <summary>Determines whether <paramref name="element"/>'s <paramref name="attributeName"/> is exactly <c>SourceAlpha</c>.</summary>
    /// <param name="element">The element to inspect.</param>
    /// <param name="attributeName">The attribute name.</param>
    /// <returns><see langword="true"/> if the attribute equals <c>SourceAlpha</c>.</returns>
    private static bool IsSourceAlphaName(XElement element, string attributeName) =>
        string.Equals((string?)element.Attribute(attributeName), "SourceAlpha", StringComparison.Ordinal);

    /// <summary>
    ///     Evaluates <paramref name="filterElement"/>'s <c>fe*</c> primitive children, in document
    ///     order, against <paramref name="target"/>'s source graphic.
    /// </summary>
    /// <param name="filterElement">The resolved <c>filter</c> element.</param>
    /// <param name="target">
    ///     The already-rendered <c>SourceGraphic</c> buffer (sized to the filter region), its
    ///     absolute pixel-space region origin, and the referencing element's own accumulated transform.
    /// </param>
    /// <param name="state">The referencing element's own cascaded render state.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="scope">The recursion depths at the referencing element and the shared work budgets.</param>
    /// <param name="totalElements">The running total-rendered-elements count.</param>
    /// <returns>
    ///     The last document-order primitive's own output buffer, or the source graphic
    ///     itself if <paramref name="filterElement"/> has no <c>fe*</c> children at all.
    /// </returns>
    /// <remarks>
    ///     Every buffer produced by every primitive in this pipeline is exactly
    ///     the source graphic's own size. <c>in</c>/<c>in2</c> name resolution
    ///     follows this fixed precedence: <c>"SourceGraphic"</c>; <c>"SourceAlpha"</c>; an
    ///     earlier named <c>result</c>; the immediately preceding primitive; and, for any other
    ///     dangling/unrecognized name, tolerant fallback to the source graphic.
    ///     Any primitive name this codec does not recognize remains a tolerant no-op pass-through
    ///     operation of its own resolved <c>in</c> input.
    /// </remarks>
    private static Surface EvaluateFilterChain(
        XElement filterElement,
        EffectsTarget target,
        RenderState state,
        RenderContext context,
        RenderScope scope,
        ref int totalElements)
    {
        var chain = new FilterChain(target, state, context, scope);
        foreach (var primitive in filterElement.Elements())
        {
            EvaluatePrimitive(primitive, chain, ref totalElements);
        }

        return chain.PreviousResult ?? chain.SourceGraphic;
    }

    /// <summary>
    ///     Evaluates one <c>fe*</c> primitive against <paramref name="chain"/>, applies its optional
    ///     subregion clip, records its named result, and makes it the chain's previous result.
    /// </summary>
    /// <param name="primitive">The primitive element.</param>
    /// <param name="chain">The mutable evaluation state of the enclosing filter.</param>
    /// <param name="totalElements">The running total-rendered-elements count.</param>
    private static void EvaluatePrimitive(XElement primitive, FilterChain chain, ref int totalElements)
    {
        var sourceGraphic = chain.SourceGraphic;
        var primitiveHasSubregion = PrimitiveDeclaresSubregion(primitive);
        var primitiveSubregion = primitiveHasSubregion
            ? ResolvePrimitiveSubregionPixelBounds(
                primitive,
                (X: chain.RegionPixelX, Y: chain.RegionPixelY, Width: sourceGraphic.Width, Height: sourceGraphic.Height),
                chain.RegionPixelX,
                chain.RegionPixelY,
                chain.Transform)
            : chain.FullRegion;

        var input = chain.ResolveInput((string?)primitive.Attribute("in"), out var inputSubregion);
        var output = ApplyPrimitive(primitive, chain, input, inputSubregion, primitiveSubregion, ref totalElements, out var outputSubregion);

        // Every primitive that produces a new buffer allocates one region-sized surface;
        // a pass-through (the unrecognized-primitive default) allocates nothing.
        if (!ReferenceEquals(output, input))
        {
            chain.Scope.FilterWorkBudget.RecordSurfaceAllocated(sourceGraphic.Width, sourceGraphic.Height);
        }

        if (primitiveHasSubregion)
        {
            chain.Scope.FilterWorkBudget.RecordSurfaceAllocated(sourceGraphic.Width, sourceGraphic.Height);
            output = ClipPrimitiveOutputToSubregion(output, primitiveSubregion);
            outputSubregion = primitiveSubregion;
        }

        var resultName = (string?)primitive.Attribute("result");
        if (!string.IsNullOrEmpty(resultName))
        {
            chain.Results[resultName] = output;
            chain.ResultSubregions[resultName] = outputSubregion;
        }

        chain.PreviousResult = output;
        chain.PreviousSubregion = outputSubregion;
    }

    /// <summary>
    ///     Dispatches one <c>fe*</c> primitive to its own implementation.
    /// </summary>
    /// <param name="primitive">The primitive element.</param>
    /// <param name="chain">The mutable evaluation state of the enclosing filter.</param>
    /// <param name="input">The primitive's resolved <c>in</c> buffer.</param>
    /// <param name="inputSubregion">The subregion of <paramref name="input"/>.</param>
    /// <param name="primitiveSubregion">The primitive's own resolved subregion.</param>
    /// <param name="totalElements">The running total-rendered-elements count.</param>
    /// <param name="outputSubregion">The subregion the output occupies before any explicit subregion clip.</param>
    /// <returns>The primitive's output buffer; <paramref name="input"/> itself for an unrecognized primitive.</returns>
    private static Surface ApplyPrimitive(
        XElement primitive,
        FilterChain chain,
        Surface input,
        PixelRect inputSubregion,
        PixelRect primitiveSubregion,
        ref int totalElements,
        out PixelRect outputSubregion)
    {
        var width = chain.SourceGraphic.Width;
        var height = chain.SourceGraphic.Height;
        var scale = chain.Scale;
        outputSubregion = chain.FullRegion;

        switch (primitive.Name.LocalName)
        {
            case "feFlood":
                return ApplyFeFlood(primitive, width, height);

            case "feGaussianBlur":
                return ApplyFeGaussianBlur(primitive, input, scale);

            case "feOffset":
                return ApplyFeOffset(primitive, input, scale);

            case "feComposite":
                {
                    var input2 = chain.ResolveInput((string?)primitive.Attribute("in2"), out var input2Subregion);
                    outputSubregion = UnionPrimitiveSubregions(inputSubregion, input2Subregion);
                    return ApplyFeComposite(primitive, input, input2);
                }

            case "feBlend":
                {
                    var blendInput2 = chain.ResolveInput((string?)primitive.Attribute("in2"), out var blendInput2Subregion);
                    outputSubregion = UnionPrimitiveSubregions(inputSubregion, blendInput2Subregion);
                    return ApplyFeBlend(primitive, input, blendInput2);
                }

            case "feMerge":
                return ApplyFeMerge(primitive, width, height, name => chain.ResolveInput(name, out _));

            case "feColorMatrix":
                outputSubregion = inputSubregion;
                return ApplyFeColorMatrix(primitive, input);

            case "feComponentTransfer":
                outputSubregion = inputSubregion;
                return ApplyFeComponentTransfer(primitive, input);

            case "feMorphology":
                return ApplyFeMorphology(primitive, input, scale);

            case "feConvolveMatrix":
                return ApplyFeConvolveMatrix(primitive, input);

            case "feDisplacementMap":
                return ApplyFeDisplacementMap(primitive, input, chain.ResolveInput((string?)primitive.Attribute("in2"), out _), scale);

            case "feTile":
                return ApplyFeTile(input, inputSubregion);

            case "feDropShadow":
                return ApplyFeDropShadow(primitive, input, scale);

            case "feImage":
                outputSubregion = primitiveSubregion;
                return ApplyFeImage(primitive, chain, primitiveSubregion, ref totalElements);

            case "feDiffuseLighting":
                outputSubregion = inputSubregion;
                return ApplyFeDiffuseLighting(primitive, input, scale, chain.Transform, chain.RegionPixelX, chain.RegionPixelY);

            case "feSpecularLighting":
                outputSubregion = inputSubregion;
                return ApplyFeSpecularLighting(primitive, input, scale, chain.Transform, chain.RegionPixelX, chain.RegionPixelY);

            case "feTurbulence":
                return ApplyFeTurbulence(primitive, width, height, scale, chain.RegionPixelX, chain.RegionPixelY);

            default:
                outputSubregion = inputSubregion;
                return input;
        }
    }

    /// <summary>
    ///     Mutable evaluation state of one filter chain: the source graphic, the referencing
    ///     element's geometry and render environment, the named results produced so far, and the
    ///     lazily built <c>SourceAlpha</c> input.
    /// </summary>
    private sealed class FilterChain
    {
        private Surface? _sourceAlpha;

        /// <summary>
        ///     Initializes a new instance of the <see cref="FilterChain"/> class.
        /// </summary>
        /// <param name="target">The already-rendered <c>SourceGraphic</c> buffer and its geometry.</param>
        /// <param name="state">The referencing element's own cascaded render state.</param>
        /// <param name="context">The fixed per-document render context.</param>
        /// <param name="scope">The recursion depths and shared work budgets.</param>
        public FilterChain(EffectsTarget target, RenderState state, RenderContext context, RenderScope scope)
        {
            SourceGraphic = target.Content;
            Transform = target.Transform;
            RegionPixelX = target.PixelX;
            RegionPixelY = target.PixelY;
            State = state;
            Context = context;
            Scope = scope;
            Scale = EstimateUniformScale(target.Transform);
            FullRegion = new PixelRect(0, 0, target.Content.Width, target.Content.Height);
            PreviousSubregion = FullRegion;
        }

        /// <summary>Gets the already-rendered <c>SourceGraphic</c> buffer, sized to the filter region.</summary>
        public Surface SourceGraphic { get; }

        /// <summary>Gets the referencing element's own accumulated transform.</summary>
        public Matrix3x2 Transform { get; }

        /// <summary>Gets the filter region's absolute pixel-space X origin.</summary>
        public int RegionPixelX { get; }

        /// <summary>Gets the filter region's absolute pixel-space Y origin.</summary>
        public int RegionPixelY { get; }

        /// <summary>Gets the referencing element's own cascaded render state.</summary>
        public RenderState State { get; }

        /// <summary>Gets the fixed per-document render context.</summary>
        public RenderContext Context { get; }

        /// <summary>Gets the recursion depths and shared work budgets.</summary>
        public RenderScope Scope { get; }

        /// <summary>Gets the uniform scale estimate of <see cref="Transform"/>.</summary>
        public float Scale { get; }

        /// <summary>Gets the full-region subregion.</summary>
        public PixelRect FullRegion { get; }

        /// <summary>Gets the named primitive results produced so far.</summary>
        public Dictionary<string, Surface> Results { get; } = new(StringComparer.Ordinal);

        /// <summary>Gets the subregions of the named primitive results.</summary>
        public Dictionary<string, PixelRect> ResultSubregions { get; } = new(StringComparer.Ordinal);

        /// <summary>Gets or sets the immediately preceding primitive's output, if any.</summary>
        public Surface? PreviousResult { get; set; }

        /// <summary>Gets or sets the immediately preceding primitive's output subregion.</summary>
        public PixelRect PreviousSubregion { get; set; }

        /// <summary>
        ///     Resolves an <c>in</c>/<c>in2</c> name using the fixed precedence: the preceding
        ///     primitive (blank name), <c>SourceGraphic</c>, <c>SourceAlpha</c>, an earlier named
        ///     <c>result</c>, and finally tolerant fallback to <c>SourceGraphic</c>.
        /// </summary>
        /// <param name="name">The input name, possibly <see langword="null"/>/empty.</param>
        /// <param name="subregion">The resolved input's subregion.</param>
        /// <returns>The resolved input buffer.</returns>
        public Surface ResolveInput(string? name, out PixelRect subregion)
        {
            if (string.IsNullOrEmpty(name))
            {
                subregion = PreviousResult == null ? FullRegion : PreviousSubregion;
                return PreviousResult ?? SourceGraphic;
            }

            if (string.Equals(name, "SourceGraphic", StringComparison.Ordinal))
            {
                subregion = FullRegion;
                return SourceGraphic;
            }

            if (string.Equals(name, "SourceAlpha", StringComparison.Ordinal))
            {
                subregion = FullRegion;
                return _sourceAlpha ??= BuildTrackedSourceAlpha();
            }

            if (Results.TryGetValue(name, out var namedResult))
            {
                subregion = ResultSubregions.TryGetValue(name, out var namedSubregion) ? namedSubregion : FullRegion;
                return namedResult;
            }

            subregion = FullRegion;
            return SourceGraphic;
        }

        private Surface BuildTrackedSourceAlpha()
        {
            Scope.FilterWorkBudget.RecordSurfaceAllocated(SourceGraphic.Width, SourceGraphic.Height);
            return BuildSourceAlpha(SourceGraphic);
        }
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
    ///     the dedicated <see cref="CompositeFeOperator"/> per-pixel helper; <c>arithmetic</c> uses
    ///     the dedicated <see cref="ApplyFeCompositeArithmetic"/> per-pixel helper; any other
    ///     unrecognized operator value is a tolerant no-op passthrough of <paramref name="input"/>,
    ///     ignoring <paramref name="input2"/>.
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

            case "arithmetic":
                return ApplyFeCompositeArithmetic(element, input, input2);

            default:
                // A genuinely unrecognized operator value is a tolerant no-op passthrough of the
                // "in" input, ignoring in2/k1..k4
                return input.Crop(0, 0, input.Width, input.Height);
        }
    }

    /// <summary>
    ///     Evaluates a <c>feComposite operator="arithmetic"</c> primitive:
    ///     <c>result = k1&#215;i1&#215;i2 + k2&#215;i1 + k3&#215;i2 + k4</c>, applied identically to
    ///     each of R/G/B/A, clamped to <c>[0, 1]</c>.
    /// </summary>
    /// <param name="element">The <c>feComposite</c> primitive element (supplies <c>k1</c>..<c>k4</c>).</param>
    /// <param name="input">The already-resolved <c>in</c> input buffer ("i1" above).</param>
    /// <param name="input2">The already-resolved <c>in2</c> input buffer ("i2" above).</param>
    /// <returns>A new, independent output buffer.</returns>
    /// <remarks>
    ///     Unlike <see cref="CompositeFeOperator"/>'s other four operators, the SVG/Filter Effects
    ///     specification defines <c>arithmetic</c> directly on <b>premultiplied</b> channel values
    ///     (including alpha, using the identical formula with <c>i1</c>/<c>i2</c> replaced by each
    ///     input's own alpha) - so each input is converted to premultiplied form inline (mirroring
    ///     <see cref="CompositeFeOperator"/>'s own inline premultiply pattern, not
    ///     <see cref="Surface.PremultiplyAlpha"/>), the formula is evaluated once per channel plus
    ///     once for alpha, every one of the four results is clamped to <c>[0, 1]</c>, and the
    ///     premultiplied color is divided back down by the clamped output alpha (or set to <c>0</c>
    ///     when that alpha is <c>0</c>) to derive the straight color this codec stores.
    /// </remarks>
    private static Surface ApplyFeCompositeArithmetic(XElement element, Surface input, Surface input2)
    {
        var k1 = ParsePercentOrNumber((string?)element.Attribute("k1") ?? string.Empty, 1f) ?? 0f;
        var k2 = ParsePercentOrNumber((string?)element.Attribute("k2") ?? string.Empty, 1f) ?? 0f;
        var k3 = ParsePercentOrNumber((string?)element.Attribute("k3") ?? string.Empty, 1f) ?? 0f;
        var k4 = ParsePercentOrNumber((string?)element.Attribute("k4") ?? string.Empty, 1f) ?? 0f;

        var width = input.Width;
        var height = input.Height;
        var output = new Surface(width, height);

        for (var y = 0; y < height; y++)
        {
            var row1 = input.GetRowSpan(y);
            var row2 = input2.GetRowSpan(y);
            var outputRow = output.GetRowSpan(y);

            for (var x = 0; x < width; x++)
            {
                var p1 = row1[x];
                var p2 = row2[x];

                var a1 = p1.A / 255f;
                var a2 = p2.A / 255f;

                var outA = Math.Clamp((k1 * a1 * a2) + (k2 * a1) + (k3 * a2) + k4, 0f, 1f);

                byte outR, outG, outB;
                if (outA <= 0f)
                {
                    outR = outG = outB = 0;
                }
                else
                {
                    var pr1 = (p1.R / 255f) * a1;
                    var pg1 = (p1.G / 255f) * a1;
                    var pb1 = (p1.B / 255f) * a1;
                    var pr2 = (p2.R / 255f) * a2;
                    var pg2 = (p2.G / 255f) * a2;
                    var pb2 = (p2.B / 255f) * a2;

                    var outPr = Math.Clamp((k1 * pr1 * pr2) + (k2 * pr1) + (k3 * pr2) + k4, 0f, 1f);
                    var outPg = Math.Clamp((k1 * pg1 * pg2) + (k2 * pg1) + (k3 * pg2) + k4, 0f, 1f);
                    var outPb = Math.Clamp((k1 * pb1 * pb2) + (k2 * pb1) + (k3 * pb2) + k4, 0f, 1f);

                    outR = ToByte((outPr / outA) * 255f);
                    outG = ToByte((outPg / outA) * 255f);
                    outB = ToByte((outPb / outA) * 255f);
                }

                outputRow[x] = new Rgba32(outR, outG, outB, ToByte(outA * 255f));
            }
        }

        return output;
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
    ///     <see cref="RenderShapeEffectsPipeline"/>'s remarks).
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
