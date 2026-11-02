// cspell:ignore linecap linejoin dasharray dashoffset miterlimit anchor xlink href
// cspell:ignore Glyf Loca
// cspell:ignore evenodd nonzero viewbox gradientunits gradienttransform spreadmethod
// cspell:ignore userspaceonuse objectboundingbox skewx skewy tspan
// cspell:ignore rasterizing unparseable rrggbb sizeless bbox moveto multiplicatively pillarbox SMIL uncatchable formedness
// cspell:ignore unblurred premult
using System.Globalization;
using System.Linq;
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
    // Cascading render state
    // ================================================================================================

    /// <summary>
    ///     Identifies the horizontal text-alignment behavior of the <c>text-anchor</c> presentation
    ///     attribute, implemented via an advance-width offset applied before laying out glyphs.
    /// </summary>
    private enum TextAnchor
    {
        /// <summary>The text's first character starts at the given position (the SVG/CSS default).</summary>
        Start,

        /// <summary>The text is centered on the given position.</summary>
        Middle,

        /// <summary>The text's last character ends at the given position.</summary>
        End
    }

    /// <summary>
    ///     A snapshot of every presentation attribute that cascades from a parent element to its
    ///     children while walking the document tree, plus the accumulated opacity product.
    /// </summary>
    /// <remarks>
    ///     <see cref="Opacity"/> is not itself a spec-inherited CSS property (SVG's <c>opacity</c>
    ///     applies only to the element it is set on, compositing that element/group as a single
    ///     unit against its backdrop). This codec has no notion of isolated group compositing, so,
    ///     as a documented simplification, <see cref="Opacity"/> is instead cascaded multiplicatively
    ///     down the tree and folded directly into each descendant shape's fill/stroke alpha -
    ///     visually reasonable for the common case (non-overlapping children) but not equivalent to
    ///     true isolated-group compositing when a group's children overlap each other.
    /// </remarks>
    /// <param name="Fill">The raw <c>fill</c> paint specification (color keyword/hex/rgb()/none/url(#id)).</param>
    /// <param name="Stroke">The raw <c>stroke</c> paint specification, in the same forms as <paramref name="Fill"/>.</param>
    /// <param name="FillOpacity">The <c>fill-opacity</c> value, in <c>[0, 1]</c>.</param>
    /// <param name="StrokeOpacity">The <c>stroke-opacity</c> value, in <c>[0, 1]</c>.</param>
    /// <param name="Opacity">The accumulated <c>opacity</c> product down the tree - see this record's remarks.</param>
    /// <param name="FillRule">The <c>fill-rule</c> value.</param>
    /// <param name="StrokeWidth">The <c>stroke-width</c> value, in local user-space units.</param>
    /// <param name="StrokeLineCap">The <c>stroke-linecap</c> value.</param>
    /// <param name="StrokeLineJoin">The <c>stroke-linejoin</c> value.</param>
    /// <param name="StrokeMiterLimit">The <c>stroke-miterlimit</c> value.</param>
    /// <param name="StrokeDashArray">The <c>stroke-dasharray</c> value, or <see langword="null"/> for a solid stroke.</param>
    /// <param name="StrokeDashOffset">The <c>stroke-dashoffset</c> value, in local user-space units.</param>
    /// <param name="FontFamily">The <c>font-family</c> value, or <see langword="null"/> if never set.</param>
    /// <param name="FontSize">The <c>font-size</c> value, in local user-space units.</param>
    /// <param name="FontWeight">
    ///     The CSS-numeric <c>font-weight</c> value (see <see cref="ParseFontWeight"/>), used to
    ///     pick the closest-matching <see cref="SvgFontFace"/> among those registered for
    ///     <paramref name="FontFamily"/> - see <see cref="SelectClosestFace"/>.
    /// </param>
    /// <param name="FontStyle">
    ///     The <c>font-style</c> value (see <see cref="ParseFontStyle"/>), likewise used by
    ///     <see cref="SelectClosestFace"/>.
    /// </param>
    /// <param name="TextAnchor">The <c>text-anchor</c> value.</param>
    /// <param name="MarkerStart">
    ///     The raw <c>marker-start</c> paint-like specification (<c>none</c> or <c>url(#id)</c>),
    ///     naming the <c>marker</c> element rendered at a <c>line</c>/<c>polyline</c>/
    ///     <c>polygon</c>/<c>path</c>'s first vertex - see this class's marker-rendering remarks.
    /// </param>
    /// <param name="MarkerMid">
    ///     The raw <c>marker-mid</c> specification, in the same form as <paramref name="MarkerStart"/>,
    ///     applied to every vertex strictly between the first and last.
    /// </param>
    /// <param name="MarkerEnd">
    ///     The raw <c>marker-end</c> specification, in the same form as <paramref name="MarkerStart"/>,
    ///     applied to the last vertex.
    /// </param>
    /// <param name="ViewportWidth">
    ///     The current viewport's width, in user-space units - the basis a
    ///     <see cref="PercentageAxis.Horizontal"/> percentage resolves against. Established by the
    ///     root <c>svg</c> element's own resolved viewBox/size (see <c>RenderDocument</c>) and
    ///     re-established whenever a <c>symbol</c> is rendered via a <c>use</c> reference (see
    ///     <c>RenderUse</c>'s remarks) - no other element establishes a new viewport, matching this
    ///     codec's documented, bounded <c>symbol</c>/nested-<c>svg</c> scope.
    /// </param>
    /// <param name="ViewportHeight">
    ///     The current viewport's height, in user-space units - the basis a
    ///     <see cref="PercentageAxis.Vertical"/> percentage resolves against. See
    ///     <paramref name="ViewportWidth"/>'s remarks.
    /// </param>
    /// <param name="PixelatedImages">
    ///     <see langword="true"/> when the inherited <c>image-rendering</c> property selects
    ///     <c>pixelated</c>, <c>crisp-edges</c> or <c>optimizeSpeed</c>, so a raster <c>image</c>
    ///     is drawn with nearest-neighbor sampling instead of smooth resampling.
    /// </param>
    private sealed record RenderState(
        string Fill,
        string Stroke,
        float FillOpacity,
        float StrokeOpacity,
        float Opacity,
        FillRule FillRule,
        float StrokeWidth,
        LineCap StrokeLineCap,
        LineJoin StrokeLineJoin,
        float StrokeMiterLimit,
        IReadOnlyList<float>? StrokeDashArray,
        float StrokeDashOffset,
        string? FontFamily,
        float FontSize,
        int FontWeight,
        SvgFontStyle FontStyle,
        TextAnchor TextAnchor,
        string MarkerStart,
        string MarkerMid,
        string MarkerEnd,
        float ViewportWidth,
        float ViewportHeight,
        bool PixelatedImages = false)
    {
        /// <summary>
        ///     The default render state every document starts with, matching the SVG/CSS initial
        ///     values for every cascaded presentation property this codec supports.
        /// </summary>
        /// <remarks>
        ///     <see cref="ViewportWidth"/>/<see cref="ViewportHeight"/> are seeded here with the
        ///     CSS/UA replaced-element default (300x150, matching <c>ResolveViewBoxOrSize</c>'s own
        ///     sizeless fallback) purely so this record always has a well-defined, finite,
        ///     positive percentage basis even before <c>RenderDocument</c> overrides it with the
        ///     document's own resolved viewBox/size - every real render path immediately overrides
        ///     both fields (see <c>RenderDocument</c>'s remarks), so this default value is never
        ///     itself observed by a percentage resolved against a real document.
        /// </remarks>
        public static readonly RenderState Initial = new(
            Fill: "black",
            Stroke: "none",
            FillOpacity: 1f,
            StrokeOpacity: 1f,
            Opacity: 1f,
            FillRule: FillRule.NonZero,
            StrokeWidth: 1f,
            StrokeLineCap: LineCap.Butt,
            StrokeLineJoin: LineJoin.Miter,
            StrokeMiterLimit: 4f,
            StrokeDashArray: null,
            StrokeDashOffset: 0f,
            FontFamily: null,
            FontSize: 16f,
            FontWeight: 400,
            FontStyle: SvgFontStyle.Normal,
            TextAnchor: TextAnchor.Start,
            MarkerStart: "none",
            MarkerMid: "none",
            MarkerEnd: "none",
            ViewportWidth: 300f,
            ViewportHeight: 150f,
            PixelatedImages: false);
    }

    /// <summary>
    ///     The fixed, per-document context (pixel target, id index, stylesheet, and optional font
    ///     dictionary) threaded through the recursive tree walk, kept as its own type so every
    ///     walk/render method needs only one extra parameter rather than several.
    /// </summary>
    /// <param name="Surface">The pixel target every shape is rendered onto.</param>
    /// <param name="IdIndex">The whole-document id-to-element index built once up front.</param>
    /// <param name="Fonts">
    ///     The caller-supplied font-family-to-face-list dictionary, or <see langword="null"/> if
    ///     none was supplied (text then resolves entirely through the default font resolution). Populated either directly by the
    ///     <see cref="LoadWithFontFaces(Stream, int, int, IReadOnlyDictionary{string, IReadOnlyList{SvgFontFace}}?)"/>
    ///     overload, or via <see cref="ToFontFaces"/> when the legacy single-font-per-family
    ///     overload is used.
    /// </param>
    /// <param name="Stylesheet">
    ///     The whole-document CSS stylesheet built once up front by <c>BuildStylesheet</c> (see
    ///     <c>SvgCodec.Css.Cascade.cs</c>), mirroring <paramref name="IdIndex"/>'s identical
    ///     "built once, read many times" lifetime. <see cref="CssStylesheet.Empty"/> for a document
    ///     with no <c>&lt;style&gt;</c> element (or none containing any retainable rule).
    /// </param>
    private sealed record RenderContext(
        Surface Surface,
        Dictionary<string, XElement> IdIndex,
        IReadOnlyDictionary<string, IReadOnlyList<SvgFontFace>>? Fonts,
        CssStylesheet Stylesheet)
    {
        /// <summary>
        ///     The shared cumulative CSS selector-matching work budget (see
        ///     <see cref="CssMatchWorkBudget"/>) for this <c>Load</c> call, charged once per
        ///     element that has any stylesheet rule to match against.
        /// </summary>
        public CssMatchWorkBudget CssBudget { get; } = new();

        /// <summary>
        ///     Whether <c>text</c> rendering may consult fonts installed on the host (see
        ///     <see cref="SvgLoadOptions.UseSystemFonts"/>). Set once by <c>LoadCore</c>.
        /// </summary>
        public bool UseSystemFonts { get; init; } = true;

        /// <summary>
        ///     Caches each default-resolved font (see <see cref="ResolveFont"/>), keyed by the
        ///     <c>font-family</c> text and the bold/italic axes, for the lifetime of one <c>Load</c>
        ///     call - the same "populated once, read many times" lifetime as <see cref="GradientStopCache"/>.
        /// </summary>
        public Dictionary<(string Family, bool Bold, bool Italic), TrueTypeFont> ResolvedFontCache { get; } = [];

        /// <summary>
        ///     Caches each gradient element's own resolved (pre-alpha) color stops, keyed by the
        ///     gradient <see cref="XElement"/>'s reference identity (matching the existing
        ///     cycle-detection <see cref="HashSet{XElement}"/> idiom used by
        ///     <see cref="ResolveGradientStops"/> itself), so a gradient referenced by many shapes
        ///     - directly, or via <c>use</c> fan-out - has its <c>stop</c> children parsed only
        ///     once per <c>Load</c> call rather than once per reference. Populated once and read
        ///     many times, the same "one mutable dictionary field, populated once, shared for the
        ///     lifetime of one render" lifetime as <see cref="IdIndex"/> above. Safe because a
        ///     gradient's own stops never change within a single <c>Load</c> call - the parsed
        ///     <see cref="XDocument"/> is never mutated after <c>Load</c> builds it once, and this
        ///     codec implements no scripting/animation support that could redefine a gradient's
        ///     stops mid-render.
        /// </summary>
        public Dictionary<XElement, List<GradientStop>> GradientStopCache { get; } = [];

        /// <summary>
        ///     Caches each <c>pattern</c> element's own resolved content element (the element whose
        ///     children are actually rendered into each tile - see
        ///     <see cref="ResolvePatternContentElement"/>), keyed by the referenced <c>pattern</c>
        ///     <see cref="XElement"/>'s own reference identity, mirroring
        ///     <see cref="GradientStopCache"/>'s identical "populated once, read many times, safe
        ///     because the parsed <see cref="XDocument"/> is never mutated mid-<c>Load</c>" lifetime
        ///     and rationale. A pattern referenced by many shapes therefore has its own <c>href</c>
        ///     chain walked only once per <c>Load</c> call, rather than once per reference - unlike a
        ///     rendered tile buffer itself (deliberately never cached; see
        ///     <see cref="RenderPatternFill"/>'s remarks for why), the resolved content
        ///     <em>element</em> is purely structural and does not depend on the referencing shape's
        ///     own bounding box, so it is always safe to share across every reference.
        /// </summary>
        public Dictionary<XElement, XElement?> PatternContentCache { get; } = [];

        /// <summary>
        ///     Caches each element's own pre-built <see cref="CssElementStyleContext"/> (see
        ///     <see cref="CssStylesheet.BuildElementContext"/>), keyed by the element's own
        ///     reference identity, mirroring <see cref="GradientStopCache"/>'s identical "populated
        ///     once, read many times, safe because the parsed <see cref="XDocument"/> is never
        ///     mutated mid-<c>Load</c>" lifetime and rationale. Without this cache, every one of
        ///     <see cref="ApplyPresentationAttributes"/>'s own per-element CSS matching (charged
        ///     once against <see cref="CssBudget"/>) would be silently repeated - and re-charged -
        ///     for every other call site that also needs to resolve one of an element's own
        ///     cascaded properties (for example <see cref="ResolveFilterElement"/>,
        ///     <see cref="ResolveClipPathElement"/>, <see cref="ResolveMaskElement"/>) against the
        ///     exact same element, doubling real per-element matching cost with no benefit. See
        ///     <see cref="GetStyleContext"/>, the sole reader/writer of this cache.
        /// </summary>
        private Dictionary<XElement, CssElementStyleContext> StyleContextCache { get; } = [];

        /// <summary>
        ///     Gets <paramref name="element"/>'s own <see cref="CssElementStyleContext"/>, building
        ///     and charging <see cref="CssBudget"/> for it (via
        ///     <see cref="CssStylesheet.BuildElementContext"/>) only the first time this element is
        ///     asked for - every further call for the same element (see
        ///     <see cref="StyleContextCache"/>'s remarks) reuses the cached result at no further
        ///     matching cost or budget charge.
        /// </summary>
        /// <param name="element">The element to resolve a styling context for.</param>
        /// <returns>The element's own (possibly cached) <see cref="CssElementStyleContext"/>.</returns>
        /// <exception cref="InvalidDataException">
        ///     Thrown via <see cref="CssBudget"/> the first time this element's context is built, if
        ///     doing so exceeds the cumulative CSS selector-matching work budget.
        /// </exception>
        public CssElementStyleContext GetStyleContext(XElement element)
        {
            if (StyleContextCache.TryGetValue(element, out var cached))
            {
                return cached;
            }

            var built = Stylesheet.BuildElementContext(element, CssBudget);
            StyleContextCache[element] = built;
            return built;
        }
    }

    // ================================================================================================
    // Document tree walking and element dispatch
    // ================================================================================================

    /// <summary>
    ///     Elements that never render visible content directly when encountered by the ordinary
    ///     top-down tree walk - they only serve as templates reachable via <c>url(#id)</c>/
    ///     <c>href</c> lookups against the whole-document id index.
    /// </summary>
    private static readonly HashSet<string> NonRenderingElements = new(StringComparer.Ordinal)
    {
        "defs", "clipPath", "mask", "pattern", "marker", "linearGradient", "radialGradient", "filter"
    };

    /// <summary>
    ///     Well-formed-but-out-of-scope elements that are silently skipped (not recursed into),
    ///     without aborting rendering of the rest of the document - see this class's remarks.
    /// </summary>
    private static readonly HashSet<string> SkippedElements = new(StringComparer.Ordinal)
    {
        "style", "animate", "animateTransform", "animateMotion", "animateColor", "set",
        "foreignObject", "svg", "metadata", "title", "desc", "script"
    };

    /// <summary>
    ///     Renders every top-level child of the root <c>svg</c> element, seeded with the initial
    ///     render state and the root viewBox-fit transform.
    /// </summary>
    /// <param name="root">The document's root element.</param>
    /// <param name="fitTransform">The viewBox-fit transform computed for this render.</param>
    /// <param name="viewportSize">
    ///     The document's own resolved viewBox/size (see <c>ResolveViewBoxOrSize</c>), seeded as
    ///     the root render state's <see cref="RenderState.ViewportWidth"/>/
    ///     <see cref="RenderState.ViewportHeight"/> - the initial percentage-resolution basis every
    ///     descendant inherits until a <c>symbol</c> referenced via <c>use</c> establishes a new one.
    /// </param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="diagnostics">
    ///     The optional work-counter sink (see <see cref="RenderDiagnostics"/>), or
    ///     <see langword="null"/> for none.
    /// </param>
    private static void RenderDocument(XElement root, Matrix3x2 fitTransform, Vector2 viewportSize, RenderContext context, RenderDiagnostics? diagnostics = null)
    {
        var initialState = RenderState.Initial with { ViewportWidth = viewportSize.X, ViewportHeight = viewportSize.Y };
        var rootState = ApplyPresentationAttributes(initialState, root, context);
        var totalElements = 0;
        var workBudget = new GeometryWorkBudget();
        var filterWorkBudget = new FilterWorkBudget(diagnostics);
        var boundsPrePassBudget = new BoundsPrePassWorkBudget();
        var rootScope = new RenderScope(0, 0, 0, workBudget, filterWorkBudget, boundsPrePassBudget);
        foreach (var child in root.Elements())
        {
            RenderElement(child, rootState, fitTransform, context, rootScope, ref totalElements);
        }
    }

    /// <summary>
    ///     Renders a single element and, for container elements, recurses into its children,
    ///     applying <paramref name="element"/>'s own presentation-attribute cascade and transform
    ///     on top of <paramref name="parentState"/>/<paramref name="parentTransform"/> first.
    /// </summary>
    /// <param name="element">The element to render.</param>
    /// <param name="parentState">The inherited render state from the parent element.</param>
    /// <param name="parentTransform">The accumulated transform from the parent element.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="scope">
    ///     The recursion depths and shared work budgets. <see cref="RenderScope.UseDepth"/> is
    ///     propagated so <see cref="RenderUse"/> can enforce <see cref="MaxUseDepth"/>;
    ///     <see cref="RenderScope.ElementDepth"/> is the current recursion depth of this call
    ///     within the element tree walk, propagated so this method can enforce
    ///     <see cref="MaxElementDepth"/> before descending further, regardless of whether the
    ///     recursion arises from plain <c>g</c>/<c>symbol</c> nesting or from a <c>use</c>
    ///     reference; <see cref="RenderScope.MarkerDepth"/> is the current <c>marker</c>-reference
    ///     nesting depth, propagated so <see cref="RenderOneMarker"/> can enforce
    ///     <see cref="MaxMarkerDepth"/> (not incremented by plain <c>g</c>/<c>symbol</c> nesting or
    ///     by a <c>use</c> reference - only by rendering one marker's own content);
    ///     <see cref="RenderScope.WorkBudget"/> (see <see cref="GeometryWorkBudget"/>) is threaded
    ///     down to <c>path</c>/<c>polyline</c>/<c>polygon</c>/<c>text</c> handling so a single
    ///     pathologically large element's own content is also bounded, independent of
    ///     <paramref name="totalElements"/>; <see cref="RenderScope.FilterWorkBudget"/> (see
    ///     <see cref="FilterWorkBudget"/>) is threaded down to every shape/text dispatch below so a
    ///     filter reused across many shapes is bounded in aggregate, independent of each individual
    ///     filter's own <see cref="MaxFilterPrimitiveWorkUnits"/> ceiling; and
    ///     <see cref="RenderScope.BoundsPrePassBudget"/> (see <see cref="BoundsPrePassWorkBudget"/>)
    ///     is threaded down to every <c>g</c>/<c>symbol</c>/<c>use</c> dispatch below so
    ///     <see cref="RenderGroupWithEffects"/>'s own bounds-only pre-pass is bounded in aggregate
    ///     across every nested filtered group, independent of each individual pre-pass
    ///     invocation's own <see cref="MaxTotalRenderedElements"/> ceiling.
    /// </param>
    /// <param name="totalElements">
    ///     The running count of elements rendered/visited so far across the whole document walk,
    ///     charged before this element is processed further so this method can enforce
    ///     <see cref="MaxTotalRenderedElements"/> - a bound that catches non-cyclic exponential
    ///     <c>use</c> fan-out neither <see cref="MaxUseDepth"/> nor <see cref="MaxElementDepth"/>
    ///     can, since a legitimately (non-cyclically) shared subtree stays within both depth caps
    ///     no matter how many times sibling <c>use</c> elements reference it.
    /// </param>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="element"/> or a descendant contains malformed presentation
    ///     data, a <c>use</c> reference cycle/excessive nesting is detected, the element tree
    ///     nests deeper than <see cref="MaxElementDepth"/>, the document resolves to more than
    ///     <see cref="MaxTotalRenderedElements"/> total rendered elements, the combined total
    ///     of path-data commands, points-list coordinates, and text characters parsed exceeds
    ///     <see cref="GeometryWorkBudget"/>'s fixed budget, or the combined total of nested
    ///     filtered-group bounds-pre-pass element visits exceeds
    ///     <see cref="BoundsPrePassWorkBudget"/>'s fixed budget.
    /// </exception>
    /// <remarks>
    ///     A composed <paramref name="parentTransform"/> and <paramref name="element"/>'s own
    ///     <c>transform</c> attribute are each individually finite, but their product can still
    ///     overflow to a non-finite value across deeply nested <c>transform="scale(...)"</c>
    ///     groups even though every individual literal was finite. This method tolerantly skips
    ///     rendering <paramref name="element"/> and its entire subtree in that case, mirroring
    ///     <see cref="RenderStroke"/>'s established non-finite effective stroke-width skip. This
    ///     single check also covers every recursive path (plain <c>g</c>/<c>symbol</c> nesting and
    ///     a <c>use</c> reference), since <see cref="RenderUse"/> always re-enters this method,
    ///     which recomputes and re-checks its own composed transform regardless of how it was
    ///     reached.
    ///     This check is deliberate defense-in-depth rather than a closed crash repro: every
    ///     currently-known way a non-finite composed transform could otherwise reach a throwing
    ///     <see cref="Drawing"/>-namespace constructor is already independently guarded closer to
    ///     that constructor - a non-finite effective stroke width is caught by
    ///     <see cref="RenderStroke"/>'s own check, and a non-finite gradient transform is caught by
    ///     <see cref="BuildGradient"/>'s own check - and the rasterizer/stroker otherwise tolerate
    ///     non-finite path coordinates without throwing. As of this writing there is no known
    ///     input for which removing this check alone (leaving the other two guards intact) causes
    ///     an uncaught exception; it exists to fail safe against a future
    ///     <see cref="Drawing"/>-namespace addition that constructs something from the composed
    ///     transform without its own finiteness guard.
    /// </remarks>
    private static void RenderElement(
        XElement element,
        RenderState parentState,
        Matrix3x2 parentTransform,
        RenderContext context,
        RenderScope scope,
        ref int totalElements)
    {
        // Fail fast before recursing any further - an unbounded element tree walk would otherwise
        // eventually drive the call stack into an uncatchable StackOverflowException
        if (scope.ElementDepth >= MaxElementDepth)
        {
            throw new InvalidDataException("SVG element nesting exceeds the supported depth.");
        }

        // Charge the total-visit budget before doing any further work on this element - this is
        // the only bound that catches non-cyclic exponential "use" fan-out, where every
        // individual reference chain stays well within the depth caps above. Checked before
        // incrementing (rather than incrementing then checking) for the same check-before-add
        // reasoning as GeometryWorkBudget.Charge and BuildIdIndex's identical counter above.
        if (totalElements >= MaxTotalRenderedElements)
        {
            throw new InvalidDataException("SVG document resolves to too many total rendered elements.");
        }

        totalElements++;

        var name = element.Name.LocalName;
        if (NonRenderingElements.Contains(name) || SkippedElements.Contains(name))
        {
            return;
        }

        var state = ApplyPresentationAttributes(parentState, element, context);
        var transform = ParseTransformAttribute(element) * parentTransform;

        // A non-finite composed transform (see this method's remarks) cannot meaningfully
        // position this element or any descendant - skip the whole subtree as defense-in-depth,
        // even though every currently-known throwing downstream path is already independently
        // guarded closer to its own constructor (see this method's remarks)
        if (!IsFiniteTransform(transform))
        {
            return;
        }

        switch (name)
        {
            case "g":
            case "symbol":
                RenderGroupElement(element, state, transform, context, scope, ref totalElements);
                break;

            case "rect":
                RenderShapeWithEffects(element, BuildRectPath(element, state), state, transform, context, scope, ref totalElements);
                break;

            case "circle":
                RenderShapeWithEffects(element, BuildEllipsePath(element, isCircle: true, state), state, transform, context, scope, ref totalElements);
                break;

            case "ellipse":
                RenderShapeWithEffects(element, BuildEllipsePath(element, isCircle: false, state), state, transform, context, scope, ref totalElements);
                break;

            case "line":
                RenderShapeWithMarkers(element, BuildLinePath(element, state), state, transform, context, scope, ref totalElements);
                break;

            case "polyline":
                RenderShapeWithMarkers(element, BuildPolyPath(element, closed: false, scope.WorkBudget), state, transform, context, scope, ref totalElements);
                break;

            case "polygon":
                RenderShapeWithMarkers(element, BuildPolyPath(element, closed: true, scope.WorkBudget), state, transform, context, scope, ref totalElements);
                break;

            case "path":
                RenderShapeWithMarkers(element, BuildPathDataPath(element, scope.WorkBudget), state, transform, context, scope, ref totalElements);
                break;

            case "use":
                RenderUse(element, state, transform, context, scope, ref totalElements);
                break;

            case "text":
                RenderText(element, state, transform, context, scope, ref totalElements);
                break;

            case "image":
                RenderImageWithEffects(element, state, transform, context, scope, ref totalElements);
                break;

            default:
                // Any other element name (including future/unknown elements) is tolerated by
                // silently skipping it, consistent with this codec's out-of-scope-construct policy
                break;
        }
    }

    /// <summary>
    ///     Renders a <c>g</c>/<c>symbol</c> element's children - directly, or as one combined unit
    ///     through <see cref="RenderGroupWithEffects"/> when the element carries its own
    ///     <c>filter</c>/<c>clip-path</c>/<c>mask</c>.
    /// </summary>
    /// <remarks>
    ///     <c>symbol</c> is spec-defined to render only when referenced via <c>use</c>, never when
    ///     encountered directly - this codec has no notion of a "use-only" render context, so, as
    ///     a documented simplification, it is instead treated identically to a plain group in both
    ///     situations (renders in place if encountered directly, and also renders when referenced
    ///     via <c>use</c>). The element's own effects are suppressed whenever this call is itself
    ///     part of a marker's own content (<see cref="RenderScope.MarkerDepth"/> &gt; 0), per the
    ///     documented "filters on marker content have no effect" scope decision. See
    ///     <see cref="RenderGroupWithEffects"/>'s remarks for the full group-effects algorithm.
    /// </remarks>
    /// <param name="element">The <c>g</c>/<c>symbol</c> element.</param>
    /// <param name="state">The element's own cascaded render state.</param>
    /// <param name="transform">The element's own composed transform.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="scope">The recursion depths and shared work budgets.</param>
    /// <param name="totalElements">The running total-rendered-elements count.</param>
    private static void RenderGroupElement(
        XElement element,
        RenderState state,
        Matrix3x2 transform,
        RenderContext context,
        RenderScope scope,
        ref int totalElements)
    {
        var effects = ResolveEffectsSet(element, context, suppressEffects: scope.MarkerDepth > 0);
        if (effects.Filter == null && effects.ClipPath == null && effects.Mask == null)
        {
            var childScope = scope.Nested();
            foreach (var child in element.Elements())
            {
                RenderElement(child, state, transform, context, childScope, ref totalElements);
            }

            return;
        }

        RenderGroupWithEffects(effects, element.Elements().ToList(), state, transform, context, scope, ref totalElements);
    }

    /// <summary>
    ///     Renders one shape (through its own effects, if any) and then its markers.
    /// </summary>
    /// <param name="element">The originating shape element.</param>
    /// <param name="path">The shape's already-built local-space outline.</param>
    /// <param name="state">The element's own cascaded render state.</param>
    /// <param name="transform">The element's own composed transform.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="scope">The recursion depths and shared work budgets.</param>
    /// <param name="totalElements">The running total-rendered-elements count.</param>
    private static void RenderShapeWithMarkers(
        XElement element,
        Path path,
        RenderState state,
        Matrix3x2 transform,
        RenderContext context,
        RenderScope scope,
        ref int totalElements)
    {
        RenderShapeWithEffects(element, path, state, transform, context, scope, ref totalElements);
        RenderMarkers(path, state, transform, context, scope, ref totalElements);
    }

    /// <summary>
    ///     Applies every presentation attribute directly set on <paramref name="element"/> on top
    ///     of <paramref name="parent"/>'s cascaded state, leaving any attribute not present on
    ///     <paramref name="element"/> inherited unchanged from <paramref name="parent"/>. Each
    ///     cascaded property is first resolved through this codec's 3-tier CSS precedence chokepoint
    ///     (<c>ResolveStyledValue</c> - inline <c>style="..."</c> beats any matching stylesheet rule
    ///     beats the plain presentation attribute, see <c>SvgCodec.Css.Cascade.cs</c>'s remarks),
    ///     falling back to reading the plain presentation attribute directly whenever neither CSS
    ///     tier has a value for that property - the overwhelmingly common, fully
    ///     backward-compatible case for a document with no <c>&lt;style&gt;</c> element and no
    ///     inline <c>style</c> attributes. Every property's raw value - whichever tier it came from -
    ///     flows through the exact same value parser (for example <see cref="ParseGeometryCoordinate"/>,
    ///     <see cref="ParseOpacityValue"/>) that a plain presentation attribute already used before
    ///     this phase, so the CSS engine never duplicates any SVG-specific value-parsing logic.
    /// </summary>
    /// <param name="parent">The inherited render state from the parent element.</param>
    /// <param name="element">The element whose own presentation attributes are applied.</param>
    /// <param name="context">
    ///     The fixed per-document render context, supplying this element's stylesheet-matching
    ///     inputs (<see cref="RenderContext.Stylesheet"/>/<see cref="RenderContext.CssBudget"/>).
    /// </param>
    /// <returns>The new, cascaded render state for <paramref name="element"/>.</returns>
    private static RenderState ApplyPresentationAttributes(RenderState parent, XElement element, RenderContext context)
    {
        var styleContext = context.GetStyleContext(element);

        string? Styled(string property) => ResolveStyledValue(styleContext, property) ?? (string?)element.Attribute(property);

        var dashArrayAttr = Styled("stroke-dasharray");
        var strokeDashArray = dashArrayAttr switch
        {
            null => parent.StrokeDashArray,
            _ when string.Equals(dashArrayAttr.Trim(), "none", StringComparison.OrdinalIgnoreCase) => null,
            _ => ParseDashArray(dashArrayAttr, parent)
        };

        return parent with
        {
            Fill = Styled("fill") ?? parent.Fill,
            Stroke = Styled("stroke") ?? parent.Stroke,
            FillOpacity = ParseStyledOptionalOpacity(Styled("fill-opacity")) ?? parent.FillOpacity,
            StrokeOpacity = ParseStyledOptionalOpacity(Styled("stroke-opacity")) ?? parent.StrokeOpacity,
            Opacity = parent.Opacity * (ParseStyledOptionalOpacity(Styled("opacity")) ?? 1f),
            FillRule = ParseFillRule(Styled("fill-rule")) ?? parent.FillRule,
            StrokeWidth = GetOptionalFloat(Styled("stroke-width"), "stroke-width", parent, PercentageAxis.Diagonal) ?? parent.StrokeWidth,
            StrokeLineCap = ParseLineCap(Styled("stroke-linecap")) ?? parent.StrokeLineCap,
            StrokeLineJoin = ParseLineJoin(Styled("stroke-linejoin")) ?? parent.StrokeLineJoin,
            StrokeMiterLimit = ParseValidMiterLimit(Styled("stroke-miterlimit")) ?? parent.StrokeMiterLimit,
            StrokeDashArray = strokeDashArray,
            StrokeDashOffset = GetOptionalFloat(Styled("stroke-dashoffset"), "stroke-dashoffset", parent, PercentageAxis.Diagonal) ?? parent.StrokeDashOffset,
            FontFamily = Styled("font-family") ?? parent.FontFamily,
            FontSize = GetOptionalFloat(Styled("font-size"), "font-size", parent, PercentageAxis.FontSize) ?? parent.FontSize,
            FontWeight = ParseFontWeight(Styled("font-weight")) ?? parent.FontWeight,
            FontStyle = ParseFontStyle(Styled("font-style")) ?? parent.FontStyle,
            TextAnchor = ParseTextAnchor(Styled("text-anchor")) ?? parent.TextAnchor,
            MarkerStart = Styled("marker-start") ?? parent.MarkerStart,
            MarkerMid = Styled("marker-mid") ?? parent.MarkerMid,
            MarkerEnd = Styled("marker-end") ?? parent.MarkerEnd,
            PixelatedImages = ParseImageRendering(Styled("image-rendering")) ?? parent.PixelatedImages
        };
    }

    /// <summary>
    ///     Parses an opacity-like value (<c>fill-opacity</c>/<c>stroke-opacity</c>/<c>opacity</c>) -
    ///     already resolved by the caller from either the plain presentation attribute or the CSS
    ///     cascade (see <c>ResolveStyledValue</c>) - accepting either a bare <c>[0, 1]</c> number
    ///     or a percentage, and clamping the result to <c>[0, 1]</c>.
    /// </summary>
    /// <param name="raw">The already-resolved raw value, or <see langword="null"/> if absent from every tier.</param>
    /// <returns>The clamped opacity value, or <see langword="null"/> if <paramref name="raw"/> is <see langword="null"/>.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="raw"/> is present but not a valid number.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="raw"/> is present but parses to a non-finite value - see
    ///     <see cref="ParseCoordinate"/>.
    /// </exception>
    private static float? ParseStyledOptionalOpacity(string? raw) => raw == null ? null : ParseOpacityValue(raw);

    /// <summary>Parses an <c>image-rendering</c> keyword into "draw nearest-neighbor" (true) or "smooth" (false).</summary>
    /// <param name="raw">The raw value, or <see langword="null"/> if absent.</param>
    /// <returns>
    ///     <see langword="true"/> for <c>pixelated</c>/<c>crisp-edges</c>/<c>optimizeSpeed</c>,
    ///     <see langword="false"/> for <c>auto</c>/<c>optimizeQuality</c>/<c>smooth</c>/<c>high-quality</c>,
    ///     <see langword="null"/> if absent or unrecognized (ignored, inherit).
    /// </returns>
    private static bool? ParseImageRendering(string? raw) => raw?.Trim().ToLowerInvariant() switch
    {
        "pixelated" or "crisp-edges" or "optimizespeed" => true,
        "auto" or "optimizequality" or "smooth" or "high-quality" => false,
        _ => null
    };

    /// <summary>Parses a <c>fill-rule</c>/<c>clip-rule</c>-style keyword.</summary>
    /// <param name="raw">The attribute's raw value, or <see langword="null"/> if absent.</param>
    /// <returns>The matching <see cref="FillRule"/>, or <see langword="null"/> if unrecognized/absent.</returns>
    private static FillRule? ParseFillRule(string? raw) => raw switch
    {
        "nonzero" => FillRule.NonZero,
        "evenodd" => FillRule.EvenOdd,
        _ => null
    };

    /// <summary>Parses a <c>stroke-linecap</c> keyword.</summary>
    /// <param name="raw">The attribute's raw value, or <see langword="null"/> if absent.</param>
    /// <returns>The matching <see cref="LineCap"/>, or <see langword="null"/> if unrecognized/absent.</returns>
    private static LineCap? ParseLineCap(string? raw) => raw switch
    {
        "butt" => LineCap.Butt,
        "round" => LineCap.Round,
        "square" => LineCap.Square,
        _ => null
    };

    /// <summary>Parses a <c>stroke-linejoin</c> keyword.</summary>
    /// <param name="raw">The attribute's raw value, or <see langword="null"/> if absent.</param>
    /// <returns>The matching <see cref="LineJoin"/>, or <see langword="null"/> if unrecognized/absent.</returns>
    private static LineJoin? ParseLineJoin(string? raw) => raw switch
    {
        "miter" => LineJoin.Miter,
        "round" => LineJoin.Round,
        "bevel" => LineJoin.Bevel,
        _ => null
    };

    /// <summary>Parses a <c>text-anchor</c> keyword.</summary>
    /// <param name="raw">The attribute's raw value, or <see langword="null"/> if absent.</param>
    /// <returns>The matching <see cref="TextAnchor"/>, or <see langword="null"/> if unrecognized/absent.</returns>
    private static TextAnchor? ParseTextAnchor(string? raw) => raw switch
    {
        "start" => TextAnchor.Start,
        "middle" => TextAnchor.Middle,
        "end" => TextAnchor.End,
        _ => null
    };

    /// <summary>
    ///     Parses a <c>font-weight</c> value: the keywords <c>normal</c> (<c>400</c>) and
    ///     <c>bold</c> (<c>700</c>), or a literal integer.
    /// </summary>
    /// <param name="raw">The attribute's raw value, or <see langword="null"/> if absent.</param>
    /// <returns>
    ///     The resolved numeric weight, or <see langword="null"/> if <paramref name="raw"/> is
    ///     absent, blank, the relative keywords <c>bolder</c>/<c>lighter</c> (not implemented -
    ///     see this class's remarks), or any other unparseable value - each tolerantly falling
    ///     back to the inherited weight, matching this codec's established tolerant-fallback
    ///     policy for a malformed presentation attribute (for example <c>stroke-miterlimit</c>).
    /// </returns>
    private static int? ParseFontWeight(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();
        return trimmed switch
        {
            "normal" => 400,
            "bold" => 700,
            _ => int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null
        };
    }

    /// <summary>
    ///     Parses a <c>font-style</c> value: <c>normal</c>, <c>italic</c>, or <c>oblique</c> (the
    ///     latter two both resolving to <see cref="SvgFontStyle.Italic"/> - see
    ///     <see cref="SvgFontStyle"/>'s remarks). Only the first whitespace-delimited token is
    ///     considered, tolerating a full <c>oblique &lt;angle&gt;</c> value without parsing the
    ///     angle itself.
    /// </summary>
    /// <param name="raw">The attribute's raw value, or <see langword="null"/> if absent.</param>
    /// <returns>
    ///     The matching <see cref="SvgFontStyle"/>, or <see langword="null"/> if absent, blank, or
    ///     unrecognized (falling back to the inherited style).
    /// </returns>
    private static SvgFontStyle? ParseFontStyle(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var firstToken = raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0];
        return firstToken switch
        {
            "normal" => SvgFontStyle.Normal,
            "italic" or "oblique" => SvgFontStyle.Italic,
            _ => null
        };
    }

    /// <summary>Parses and clamps a raw opacity string to <c>[0, 1]</c>. See <see cref="ParseStyledOptionalOpacity"/>.</summary>
    /// <param name="raw">The raw opacity string.</param>
    /// <returns>The clamped opacity value.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="raw"/> is not a valid number.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="raw"/> parses to a non-finite value - see
    ///     <see cref="ParseCoordinate"/>.
    /// </exception>
    private static float ParseOpacityValue(string raw) => Math.Clamp(ParseCoordinate(raw, 1f), 0f, 1f);

    /// <summary>
    ///     Parses a <c>stroke-dasharray</c> attribute's number list, resolving a trailing <c>%</c>
    ///     on any entry against the diagonal percentage basis (see
    ///     <see cref="PercentageAxis.Diagonal"/> and <see cref="ParseDashArrayNumberList"/>) and
    ///     tolerantly treating a negative-containing or all-zero list as "no dashing" (solid
    ///     stroke) rather than an error, matching how an unparseable value is treated elsewhere in
    ///     this codec.
    /// </summary>
    /// <param name="raw">The attribute's raw, non-<c>"none"</c> value.</param>
    /// <param name="state">
    ///     The cascaded render state supplying the current viewport, used to resolve the diagonal
    ///     percentage basis for any percentage-suffixed entry.
    /// </param>
    /// <returns>The parsed dash array, or <see langword="null"/> for an effectively-solid stroke.</returns>
    private static List<float>? ParseDashArray(string raw, RenderState state)
    {
        var numbers = ParseDashArrayNumberList(raw, ResolvePercentageBasis(state, PercentageAxis.Diagonal));
        return numbers.Count == 0 || numbers.Exists(v => v < 0f) || numbers.TrueForAll(v => v.Equals(0f))
            ? null
            : numbers;
    }

    /// <summary>
    ///     Parses and validates the already-resolved raw <c>stroke-miterlimit</c> value (from
    ///     either the plain presentation attribute or the CSS cascade - see
    ///     <c>ResolveStyledValue</c>) against <see cref="Drawing.StrokeStyle"/>'s documented
    ///     contract (finite and at least <c>1</c>), so an invalid value falls back to the
    ///     inherited value here rather than escaping later as an undocumented
    ///     <see cref="ArgumentOutOfRangeException"/> from <see cref="Drawing.StrokeStyle"/>'s
    ///     constructor - mirroring this class's existing tolerant handling of a malformed
    ///     <c>stroke-dasharray</c> (see <see cref="ParseDashArray"/>), rather than aborting the
    ///     whole document over one presentation-attribute value.
    /// </summary>
    /// <remarks>
    ///     This deliberately does not delegate to
    ///     <see cref="GetOptionalFloat(string?, string, RenderState, PercentageAxis)"/>/
    ///     <see cref="ParseGeometryCoordinate"/>: <see cref="ParseCoordinate"/> already throws
    ///     <see cref="InvalidDataException"/> for a non-finite parsed value (added for
    ///     coordinates/lengths generally), which would preempt this method's own finiteness
    ///     check below and make it dead code - a non-finite <c>stroke-miterlimit</c> would then
    ///     abort the whole document instead of falling back to the inherited value, contradicting
    ///     this method's documented contract above. Parsing the raw value directly keeps the
    ///     non-finite-falls-back path reachable for this attribute specifically, without changing
    ///     that shared, correct-for-every-other-attribute behavior. The percentage-suffix
    ///     rejection below is intentionally duplicated (not delegated) for the same reason - see
    ///     <see cref="ParseGeometryCoordinate"/> for the identical check applied to other
    ///     shape/text geometry attributes.
    /// </remarks>
    /// <param name="raw">The already-resolved raw value, or <see langword="null"/> if absent from every tier.</param>
    /// <returns>The valid parsed value, or <see langword="null"/> if absent or out of contract.</returns>
    /// <exception cref="FormatException">
    ///     Thrown when <paramref name="raw"/> is present but not a valid number - propagates
    ///     uncaught to this class's top-level <c>Load</c>/<c>GetInfo</c> boundary, which rewraps it
    ///     as <see cref="InvalidDataException"/>.
    /// </exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="raw"/> carries a percentage suffix - this codec has no
    ///     defined viewport-relative basis for it, matching <see cref="ParseGeometryCoordinate"/>'s
    ///     rejection of a percentage on every other shape/text geometry attribute. Not thrown for
    ///     a non-finite parsed value (<c>NaN</c>, <c>Infinity</c>, <c>-Infinity</c>) - unlike
    ///     <see cref="ParseCoordinate"/>, such a value falls back to <see langword="null"/> here
    ///     instead, per this method's documented contract above.
    /// </exception>
    private static float? ParseValidMiterLimit(string? raw)
    {
        if (raw == null)
        {
            return null;
        }

        var trimmed = raw.Trim();
        if (trimmed.EndsWith('%'))
        {
            throw new InvalidDataException(
                $"The 'stroke-miterlimit' attribute's percentage value '{raw}' is not supported: " +
                "SvgCodec has no defined viewport-relative basis for shape/text geometry attributes.");
        }

        var value = float.Parse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture);
        return float.IsFinite(value) && value >= 1f ? value : null;
    }
}
