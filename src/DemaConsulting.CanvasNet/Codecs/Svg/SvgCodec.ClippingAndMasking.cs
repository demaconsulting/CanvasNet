// cspell:ignore linecap linejoin dasharray dashoffset miterlimit anchor xlink href
// cspell:ignore Glyf Loca
// cspell:ignore evenodd nonzero viewbox gradientunits gradienttransform spreadmethod
// cspell:ignore userspaceonuse objectboundingbox skewx skewy tspan
// cspell:ignore rasterizing unparseable rrggbb sizeless bbox moveto multiplicatively pillarbox SMIL uncatchable formedness
// cspell:ignore unblurred premult clippathunits maskcontentunits maskunits luminance
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
    // <clipPath>/<mask> resolution and application (Phase 2 of the SVG roadmap)
    // ================================================================================================
    //
    // Both effects are implemented as an offscreen-buffer alpha attenuation, applied directly to
    // the same "content" buffer RenderShapeEffectsPipeline/RenderGroupWithEffects already build for
    // filter evaluation (see SvgCodec.Filters.cs) - there is no native hard-clip-region primitive
    // anywhere in the Canvas/Surface API this codec targets, so an offscreen alpha buffer is the
    // only mechanism available for either effect, exactly mirroring how a <filter> is already
    // implemented. clip-path is modeled as a hard, all-or-nothing coverage mask (each covered
    // pixel's alpha is left unchanged; each uncovered pixel's alpha is forced to zero), built by
    // rasterizing the union of the <clipPath> element's own direct child shapes as opaque white
    // fills into a fresh Surface; mask is modeled as a graduated coverage mask (each pixel's alpha
    // is scaled by the mask content's own per-pixel luminance*alpha), built by rendering the <mask>
    // element's own children into a fresh Surface exactly as an ordinary subtree.
    //
    // Explicitly out of scope, documented here as a single reference point (each rationale is
    // deliberately narrow so a future contributor can safely lift any one restriction in isolation):
    //
    //   - A nested clip-path/mask/filter applied to one of a <clipPath> element's own direct
    //     children is never resolved (see BuildClipChildPath below): clip-path children are
    //     rasterized directly via one-shot Build*Path + PathFiller.Fill calls, never through the
    //     ordinary recursive RenderElement walk, so honoring a clip child's own further clip-path
    //     would require re-implementing a second, parallel recursive effects pipeline solely for
    //     this rare combination. A <mask>'s own children, by contrast, DO render through the
    //     ordinary RenderElement walk (see ApplyMask), so a mask child's own nested clip-path/mask/
    //     filter already works with no special-casing at all.
    //   - A clip-path/mask/filter applied to the <clipPath>/<mask> element itself (as opposed to
    //     its children) has no effect: neither element is ever reached by the ordinary top-down
    //     RenderElement walk (both are listed in NonRenderingElements), and ResolveClipPathElement/
    //     ResolveMaskElement/ResolveFilterElement are only ever invoked against a referencing
    //     element, never against the <clipPath>/<mask> element resolved from it - so any such
    //     attribute is simply never read. This mirrors the identical pre-existing behavior for a
    //     <filter> element's own filter/clip-path/mask attributes.
    //   - <use>/<g> children of a <clipPath> element are not resolved by BuildClipChildPath (only
    //     rect/circle/ellipse/polyline/polygon/path/text are) - supporting a nested group or a
    //     further indirection through <use> would require this rasterization step to itself
    //     recurse, which is exactly the added complexity the first bullet above already declines
    //     for the identical reason. A <line> child is also excluded, matching the SVG specification
    //     itself (a zero-area centerline contributes no fillable clip region).
    //   - A <pattern> paint referenced from within clip-path/mask content is unaffected by this
    //     phase - it already renders exactly as it would anywhere else in the document, since
    //     clip-path/mask content rendering reuses the ordinary shape-fill/RenderElement machinery.
    //   - Only the default luminance mask mode is implemented; an explicit "mask-type: alpha" (or
    //     the equivalent CSS <c>mask-mode</c> property) is not recognized and is tolerantly ignored,
    //     always falling back to luminance - no test in this codec's suite exercises an alpha mask,
    //     and the luminance path already exercises every other part of the mask pipeline (region
    //     resolution, maskContentUnits, budget charging) identically.
    //   - A <clipPath>/<mask> element's own href/xlink:href-based template inheritance (mirroring
    //     the gradient <c>href</c> chain-walk in SvgCodec.Gradients.cs) is not implemented: an
    //     empty <clipPath>/<mask> with only an href to another one resolves to "no children",
    //     rather than following the chain.

    /// <summary>
    ///     The maximum number of a <c>clipPath</c> element's own direct children rasterized as
    ///     clip shapes by <see cref="ApplyClipPath"/> - a resource-safety bound distinct from
    ///     <see cref="FilterWorkBudget"/> (which bounds the clip-path <em>application's</em> own
    ///     offscreen-buffer size), guarding instead against a single pathologically large
    ///     <c>clipPath</c> definition whose own per-child rasterization cost (independent of the
    ///     buffer it is rasterized into) could otherwise grow unbounded.
    /// </summary>
    private const int MaxClipPathShapesPerClipPath = 256;

    /// <summary>A pre-built fully opaque white pixel, used to paint every clip-path child shape's own coverage.</summary>
    private static readonly Rgba32 OpaqueWhite = new(255, 255, 255, 255);

    /// <summary>
    ///     Resolves <paramref name="element"/>'s own <c>clip-path</c> presentation attribute
    ///     (<c>url(#id)</c>) to its referenced <c>clipPath</c> element, mirroring
    ///     <see cref="ResolveFilterElement"/>'s exact <c>url(#id)</c>-parsing and dangling-reference
    ///     tolerance.
    /// </summary>
    /// <param name="element">The element whose own <c>clip-path</c> attribute is read.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <returns>
    ///     The referenced <c>clipPath</c> element, or <see langword="null"/> if the attribute is
    ///     absent/blank, not <c>url(#id)</c> syntax, the id is dangling, or the resolved element is
    ///     not literally a <c>clipPath</c>.
    /// </returns>
    private static XElement? ResolveClipPathElement(XElement element, RenderContext context)
    {
        var spec = (string?)element.Attribute("clip-path");
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

        return candidate.Name.LocalName == "clipPath" ? candidate : null;
    }

    /// <summary>
    ///     Resolves <paramref name="element"/>'s own <c>mask</c> presentation attribute
    ///     (<c>url(#id)</c>) to its referenced <c>mask</c> element, mirroring
    ///     <see cref="ResolveFilterElement"/>'s exact <c>url(#id)</c>-parsing and dangling-reference
    ///     tolerance.
    /// </summary>
    /// <param name="element">The element whose own <c>mask</c> attribute is read.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <returns>
    ///     The referenced <c>mask</c> element, or <see langword="null"/> if the attribute is
    ///     absent/blank, not <c>url(#id)</c> syntax, the id is dangling, or the resolved element is
    ///     not literally a <c>mask</c>.
    /// </returns>
    private static XElement? ResolveMaskElement(XElement element, RenderContext context)
    {
        var spec = (string?)element.Attribute("mask");
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

        return candidate.Name.LocalName == "mask" ? candidate : null;
    }

    /// <summary>
    ///     Applies <paramref name="clipPathElement"/> as a hard geometric clip against
    ///     <paramref name="content"/>, in place: every pixel not covered by the union of the
    ///     <c>clipPath</c> element's own direct child shapes has its alpha forced to zero; every
    ///     covered pixel is left completely unchanged.
    /// </summary>
    /// <param name="clipPathElement">The resolved <c>clipPath</c> element.</param>
    /// <param name="content">The referencing shape/group's own pre-clip offscreen content buffer, mutated in place.</param>
    /// <param name="referenceBounds">
    ///     The referencing element's own local-space painted-content bounds - the
    ///     <c>objectBoundingBox</c> reference rectangle when <c>clipPathUnits="objectBoundingBox"</c>.
    /// </param>
    /// <param name="pixelX">The pixel-space X origin <paramref name="content"/> was allocated at.</param>
    /// <param name="pixelY">The pixel-space Y origin <paramref name="content"/> was allocated at.</param>
    /// <param name="transform">The accumulated transform from the referencing element's own local space into pixel space.</param>
    /// <param name="state">
    ///     The referencing element's own cascaded render state - only its
    ///     <see cref="RenderState.ViewportWidth"/>/<see cref="RenderState.ViewportHeight"/> are
    ///     carried forward into the clip content's own fresh state (see this method's remarks).
    /// </param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="workBudget">
    ///     The shared geometry-parsing work budget, charged by each clip child's own
    ///     <c>Build*Path</c> call exactly as it would be for an ordinarily rendered shape.
    /// </param>
    /// <remarks>
    ///     Per <c>clipPathUnits</c> (default <c>userSpaceOnUse</c>), an <c>objectBoundingBox</c>
    ///     value prepends <see cref="ComputeObjectBoundingBoxMap(Rect)"/> (evaluated against
    ///     <paramref name="referenceBounds"/>) to <paramref name="transform"/> before mapping any
    ///     clip child geometry into pixel space - mirroring <c>gradientUnits</c>'s identical
    ///     pattern in <c>SvgCodec.Gradients.cs</c>.
    ///     <para>
    ///     Every recognized direct child (see <see cref="BuildClipChildPath"/> for the supported
    ///     set and this class's remarks for what is deliberately unsupported) is rasterized with
    ///     its own <c>clip-rule</c> (falling back to <c>nonzero</c>, identical to <c>fill-rule</c>'s
    ///     own default - see <see cref="ParseFillRule"/>) as an opaque white fill into a fresh,
    ///     initially fully-transparent coverage <see cref="Surface"/> the same size as
    ///     <paramref name="content"/>; painting each child fully opaque and letting later children
    ///     paint directly over earlier ones is what naturally realizes the spec-defined "union of
    ///     child shapes" semantics with no separate union step. Clip children establish their own
    ///     fresh presentation-attribute cascade starting at <paramref name="clipPathElement"/>
    ///     itself (not inherited from the referencing element), matching <see cref="ApplyMask"/>'s
    ///     identical choice for mask content.
    ///     </para>
    /// </remarks>
    private static void ApplyClipPath(
        XElement clipPathElement,
        Surface content,
        Rect referenceBounds,
        int pixelX,
        int pixelY,
        Matrix3x2 transform,
        RenderState state,
        RenderContext context,
        GeometryWorkBudget workBudget)
    {
        var isObjectBoundingBox = string.Equals(
            (string?)clipPathElement.Attribute("clipPathUnits"), "objectBoundingBox", StringComparison.Ordinal);
        var contentTransform = isObjectBoundingBox
            ? ComputeObjectBoundingBoxMap(referenceBounds) * transform
            : transform;
        var localToTemp = contentTransform * Matrix3x2.CreateTranslation(-pixelX, -pixelY);

        var coverage = new Surface(content.Width, content.Height);

        // clip-path content does not inherit the referencing element's own cascaded presentation
        // state - only the current viewport size is carried forward, so a percentage length
        // within the clip content (e.g. a clip <rect>'s own x="10%") still resolves sensibly
        var clipBaseState = ApplyPresentationAttributes(
            RenderState.Initial with { ViewportWidth = state.ViewportWidth, ViewportHeight = state.ViewportHeight },
            clipPathElement);

        var shapeCount = 0;
        foreach (var child in clipPathElement.Elements())
        {
            if (shapeCount >= MaxClipPathShapesPerClipPath)
            {
                break;
            }

            var childState = ApplyPresentationAttributes(clipBaseState, child);
            var childPath = BuildClipChildPath(child, childState, context, workBudget);
            if (childPath == null)
            {
                continue;
            }

            shapeCount++;

            var clipRule = ParseFillRule((string?)child.Attribute("clip-rule")) ?? FillRule.NonZero;
            var pixelPath = TransformPath(childPath, localToTemp);
            PathFiller.Fill(coverage, pixelPath, OpaqueWhite, clipRule);
        }

        ApplyCoverageClip(content, coverage);
    }

    /// <summary>
    ///     Builds one <c>clipPath</c> direct child's own local-space outline, for
    ///     <see cref="ApplyClipPath"/> - the clip-content counterpart of <c>RenderElement</c>'s own
    ///     shape/text dispatch, deliberately narrower (see this class's remarks for the excluded
    ///     element names and their rationale).
    /// </summary>
    /// <param name="child">The <c>clipPath</c> element's direct child.</param>
    /// <param name="childState">The child's own cascaded render state (see <see cref="ApplyClipPath"/>).</param>
    /// <param name="context">The fixed per-document render context, supplying <see cref="RenderContext.Fonts"/> for a <c>text</c> child.</param>
    /// <param name="workBudget">The shared geometry-parsing work budget.</param>
    /// <returns>
    ///     The child's own local-space outline, or <see langword="null"/> if <paramref name="child"/>
    ///     is not one of the recognized element names, or - for a <c>text</c> child specifically -
    ///     no font could be matched or it has no text content.
    /// </returns>
    private static Path? BuildClipChildPath(XElement child, RenderState childState, RenderContext context, GeometryWorkBudget workBudget) =>
        child.Name.LocalName switch
        {
            "rect" => BuildRectPath(child, childState),
            "circle" => BuildEllipsePath(child, isCircle: true, childState),
            "ellipse" => BuildEllipsePath(child, isCircle: false, childState),
            "polyline" => BuildPolyPath(child, closed: false, workBudget),
            "polygon" => BuildPolyPath(child, closed: true, workBudget),
            "path" => BuildPathDataPath(child, workBudget),
            "text" => BuildClipChildTextPath(child, childState, context),
            _ => null
        };

    /// <summary>Builds a <c>clipPath</c> direct <c>text</c> child's own glyph-run outline.</summary>
    /// <param name="child">The <c>text</c> element.</param>
    /// <param name="childState">The child's own cascaded render state.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <returns>
    ///     The laid-out glyph-run outline, or <see langword="null"/> under the same tolerant
    ///     conditions as <see cref="RenderText"/> itself (no <see cref="RenderContext.Fonts"/>, no
    ///     matching font, or no text content).
    /// </returns>
    private static Path? BuildClipChildTextPath(XElement child, RenderState childState, RenderContext context)
    {
        if (context.Fonts == null)
        {
            return null;
        }

        var font = MatchFont(childState.FontFamily, childState.FontWeight, childState.FontStyle, context.Fonts);
        if (font == null)
        {
            return null;
        }

        var text = child.Value;
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var origin = new Vector2(
            GetFloatAttribute(child, "x", childState, PercentageAxis.Horizontal),
            GetFloatAttribute(child, "y", childState, PercentageAxis.Vertical));
        return BuildGlyphRunPath(text, font, childState, origin);
    }

    /// <summary>
    ///     Multiplies every pixel of <paramref name="content"/>'s own alpha channel by
    ///     <paramref name="coverage"/>'s own alpha channel at the same coordinate, in place - the
    ///     shared per-pixel core of <see cref="ApplyClipPath"/> (whose <paramref name="coverage"/>
    ///     is always either fully opaque white or fully transparent, per pixel, so this reduces to
    ///     a hard keep/discard) and reused verbatim by nothing else - kept as its own method purely
    ///     so <see cref="ApplyClipPath"/>'s own child-rasterization loop above is not interleaved
    ///     with per-pixel arithmetic.
    /// </summary>
    private static void ApplyCoverageClip(Surface content, Surface coverage)
    {
        for (var y = 0; y < content.Height; y++)
        {
            var contentRow = content.GetRowSpan(y);
            var coverageRow = coverage.GetRowSpan(y);
            for (var x = 0; x < content.Width; x++)
            {
                var coverageAlpha = coverageRow[x].A;
                if (coverageAlpha == 255)
                {
                    continue;
                }

                var contentPixel = contentRow[x];
                var newAlpha = coverageAlpha == 0
                    ? (byte)0
                    : (byte)Math.Clamp(MathF.Round(contentPixel.A * (coverageAlpha / 255f)), 0, 255);
                contentRow[x] = new Rgba32(contentPixel.R, contentPixel.G, contentPixel.B, newAlpha);
            }
        }
    }

    /// <summary>
    ///     Applies <paramref name="maskElement"/> as a luminance mask against
    ///     <paramref name="content"/>, in place: every pixel's alpha is scaled by the mask
    ///     content's own per-pixel luminance (standard sRGB coefficients, see
    ///     <see cref="ApplyLuminanceMask"/>) times its own alpha.
    /// </summary>
    /// <param name="maskElement">The resolved <c>mask</c> element.</param>
    /// <param name="content">The referencing shape/group's own pre-mask offscreen content buffer, mutated in place.</param>
    /// <param name="referenceBounds">The referencing element's own local-space painted-content bounds.</param>
    /// <param name="pixelX">The pixel-space X origin <paramref name="content"/> was allocated at.</param>
    /// <param name="pixelY">The pixel-space Y origin <paramref name="content"/> was allocated at.</param>
    /// <param name="transform">The accumulated transform from the referencing element's own local space into pixel space.</param>
    /// <param name="state">
    ///     The referencing element's own cascaded render state - only its viewport size is carried
    ///     forward into the mask content's own fresh state, matching <see cref="ApplyClipPath"/>.
    /// </param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="useDepth">The current <c>use</c>-reference nesting depth, forwarded to the mask content's own <c>RenderElement</c> walk.</param>
    /// <param name="elementDepth">
    ///     The current recursion depth, forwarded (already incremented by one relative to the
    ///     referencing element itself) to the mask content's own <c>RenderElement</c> walk - the
    ///     sole guard against a <c>mask</c>-reference cycle (an element's own mask content
    ///     containing an element whose own mask references back to the original), since that walk
    ///     throws once <see cref="MaxElementDepth"/> is reached exactly as it would for any other
    ///     runaway recursive structure.
    /// </param>
    /// <param name="markerDepth">The current <c>marker</c>-reference nesting depth, forwarded unchanged.</param>
    /// <param name="totalElements">The running total-rendered-elements count, forwarded to the mask content's own <c>RenderElement</c> walk.</param>
    /// <param name="workBudget">The shared geometry-parsing work budget, forwarded to the mask content's own <c>RenderElement</c> walk.</param>
    /// <param name="filterWorkBudget">The shared cumulative filter-evaluation work budget, forwarded to the mask content's own <c>RenderElement</c> walk (in case it itself contains a filtered/clipped/masked element).</param>
    /// <param name="boundsPrePassBudget">The shared cumulative bounds-pre-pass work budget, forwarded to the mask content's own <c>RenderElement</c> walk.</param>
    /// <remarks>
    ///     Per <c>maskContentUnits</c> (default <c>userSpaceOnUse</c>, and deliberately distinct
    ///     from <c>maskUnits</c> - see <see cref="ComputeMaskRegionLocalBounds"/>'s remarks for why
    ///     these two attributes must never be conflated), an <c>objectBoundingBox</c> value
    ///     prepends <see cref="ComputeObjectBoundingBoxMap(Rect)"/> (evaluated against
    ///     <paramref name="referenceBounds"/>) to <paramref name="transform"/> before the mask's own
    ///     children are rendered. The mask content is rendered into a fresh, initially fully
    ///     transparent <see cref="Surface"/> the exact size of <paramref name="content"/>, using the
    ///     ordinary recursive <c>RenderElement</c> walk for every direct child (so nested groups,
    ///     gradients, and even a nested clip-path/mask/filter on mask content all already work with
    ///     no special-casing at all - unlike <see cref="ApplyClipPath"/>'s children, which are
    ///     rasterized directly and so cannot recurse). Mask content establishes its own fresh
    ///     presentation-attribute cascade starting at <see cref="RenderState.Initial"/> (only the
    ///     viewport size is carried forward) rather than inheriting the referencing element's own
    ///     cascaded state, per SVG's defined mask semantics.
    /// </remarks>
    private static void ApplyMask(
        XElement maskElement,
        Surface content,
        Rect referenceBounds,
        int pixelX,
        int pixelY,
        Matrix3x2 transform,
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
        var contentIsObjectBoundingBox = string.Equals(
            (string?)maskElement.Attribute("maskContentUnits"), "objectBoundingBox", StringComparison.Ordinal);
        var contentTransform = contentIsObjectBoundingBox
            ? ComputeObjectBoundingBoxMap(referenceBounds) * transform
            : transform;
        var localToTemp = contentTransform * Matrix3x2.CreateTranslation(-pixelX, -pixelY);

        var maskSource = new Surface(content.Width, content.Height);
        var maskContext = context with { Surface = maskSource };
        var maskState = RenderState.Initial with { ViewportWidth = state.ViewportWidth, ViewportHeight = state.ViewportHeight };

        foreach (var child in maskElement.Elements())
        {
            RenderElement(child, maskState, localToTemp, maskContext, useDepth, elementDepth + 1, markerDepth, ref totalElements, workBudget, filterWorkBudget, boundsPrePassBudget);
        }

        ApplyLuminanceMask(content, maskSource);
    }

    /// <summary>
    ///     Multiplies every pixel of <paramref name="content"/>'s own alpha channel, in place, by
    ///     <paramref name="maskSource"/>'s own per-pixel luminance times alpha at the same
    ///     coordinate - the shared per-pixel core of <see cref="ApplyMask"/>.
    /// </summary>
    /// <param name="content">The buffer being masked, mutated in place.</param>
    /// <param name="maskSource">The already fully rendered mask content buffer.</param>
    /// <remarks>
    ///     Luminance is computed via the shared <see cref="ComputeLuminance(byte, byte, byte)"/>
    ///     helper so this mask path and <c>feColorMatrix type="luminanceToAlpha"</c> use the
    ///     exact same coefficients. An unpainted mask pixel (alpha zero, and so - since a freshly
    ///     allocated <see cref="Surface"/> is all-zero - also black) contributes zero mask value,
    ///     identical to a mask pixel explicitly painted fully transparent, matching the SVG
    ///     specification's own defined mask algorithm (both are "no coverage").
    /// </remarks>
    private static void ApplyLuminanceMask(Surface content, Surface maskSource)
    {
        for (var y = 0; y < content.Height; y++)
        {
            var contentRow = content.GetRowSpan(y);
            var maskRow = maskSource.GetRowSpan(y);
            for (var x = 0; x < content.Width; x++)
            {
                var maskPixel = maskRow[x];
                var luminance = ComputeLuminance(maskPixel.R, maskPixel.G, maskPixel.B);
                var maskValue = luminance * (maskPixel.A / 255f);

                var contentPixel = contentRow[x];
                var newAlpha = (byte)Math.Clamp(MathF.Round(contentPixel.A * maskValue), 0, 255);
                contentRow[x] = new Rgba32(contentPixel.R, contentPixel.G, contentPixel.B, newAlpha);
            }
        }
    }

    /// <summary>
    ///     Computes <paramref name="maskElement"/>'s own mask region purely in the local space
    ///     <paramref name="bounds"/> is expressed in, honoring <c>maskUnits</c> (default
    ///     <c>objectBoundingBox</c>) properly - unlike <see cref="ComputeFilterRegionLocalBounds"/>'s
    ///     documented always-<c>objectBoundingBox</c> simplification for <c>filterUnits</c>, this
    ///     phase's task requirements call for <c>maskUnits="userSpaceOnUse"</c> to be genuinely
    ///     respected.
    /// </summary>
    /// <param name="maskElement">The resolved <c>mask</c> element.</param>
    /// <param name="bounds">The referencing element's already stroke-expanded local-space painted-content bounds.</param>
    /// <returns>
    ///     The local-space mask region, or <see langword="null"/> if <paramref name="bounds"/> is
    ///     empty/degenerate or the region resolves to a non-finite or non-positive size.
    /// </returns>
    /// <remarks>
    ///     <c>maskUnits</c> and <c>maskContentUnits</c> are two distinct, independently defaulted
    ///     attributes that must never be conflated: <c>maskUnits</c> (handled here) governs how
    ///     this method's own <c>x</c>/<c>y</c>/<c>width</c>/<c>height</c> attributes are
    ///     interpreted (the mask's own region box, defaulting to <c>objectBoundingBox</c>), while
    ///     <c>maskContentUnits</c> (handled entirely separately, in <see cref="ApplyMask"/>)
    ///     governs the coordinate system the <c>mask</c> element's own <em>children</em> are
    ///     rendered in (defaulting to <c>userSpaceOnUse</c> - the opposite default). A percentage
    ///     <c>x</c>/<c>y</c>/<c>width</c>/<c>height</c> value always resolves as a <c>[0, 1]</c>
    ///     fraction of <paramref name="bounds"/> regardless of <c>maskUnits</c> (mirroring
    ///     <see cref="ParseFilterRegionFraction"/>'s identical percentage handling); only a bare
    ///     number literal's interpretation actually depends on <c>maskUnits</c> - a bbox-relative
    ///     fraction under the default <c>objectBoundingBox</c>, or a literal absolute local-space
    ///     length under <c>userSpaceOnUse</c> (see <see cref="ReadMaskRegionCoordinate"/>/
    ///     <see cref="ReadMaskRegionExtent"/>). A <c>userSpaceOnUse</c> percentage is documented as
    ///     resolving against <paramref name="bounds"/> rather than the current viewport - a
    ///     deliberate simplification, since the viewport's own dimensions are not threaded into
    ///     this call site.
    /// </remarks>
    private static Rect? ComputeMaskRegionLocalBounds(XElement maskElement, Rect bounds)
    {
        if (bounds.IsEmpty || bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return null;
        }

        var isUserSpaceOnUse = string.Equals(
            (string?)maskElement.Attribute("maskUnits"), "userSpaceOnUse", StringComparison.Ordinal);

        var x = ReadMaskRegionCoordinate((string?)maskElement.Attribute("x"), -0.10f, bounds.X, bounds.Width, isUserSpaceOnUse);
        var y = ReadMaskRegionCoordinate((string?)maskElement.Attribute("y"), -0.10f, bounds.Y, bounds.Height, isUserSpaceOnUse);
        var width = ReadMaskRegionExtent((string?)maskElement.Attribute("width"), 1.20f, bounds.Width, isUserSpaceOnUse);
        var height = ReadMaskRegionExtent((string?)maskElement.Attribute("height"), 1.20f, bounds.Height, isUserSpaceOnUse);

        if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(width) || !float.IsFinite(height) ||
            width <= 0f || height <= 0f)
        {
            return null;
        }

        return new Rect(x, y, width, height);
    }

    /// <summary>
    ///     Reads a mask region's own <c>x</c>/<c>y</c> attribute (see
    ///     <see cref="ComputeMaskRegionLocalBounds"/>'s remarks for the full percentage-vs-literal/
    ///     <c>maskUnits</c> interpretation rules).
    /// </summary>
    /// <param name="raw">The raw attribute text, or <see langword="null"/> if absent.</param>
    /// <param name="defaultFraction">The bbox-relative fraction to fall back to when absent/unparseable.</param>
    /// <param name="origin">The reference bounds' own origin coordinate on this axis.</param>
    /// <param name="extent">The reference bounds' own extent on this axis.</param>
    /// <param name="isUserSpaceOnUse">Whether <c>maskUnits="userSpaceOnUse"</c> was specified.</param>
    /// <returns>The resolved local-space coordinate.</returns>
    private static float ReadMaskRegionCoordinate(string? raw, float defaultFraction, float origin, float extent, bool isUserSpaceOnUse)
    {
        if (raw == null)
        {
            return origin + (defaultFraction * extent);
        }

        var trimmed = raw.Trim();
        if (trimmed.Length == 0)
        {
            return origin + (defaultFraction * extent);
        }

        if (trimmed.EndsWith('%') || !isUserSpaceOnUse)
        {
            var fraction = ParsePercentOrNumber(trimmed, 1f) ?? defaultFraction;
            return origin + (fraction * extent);
        }

        return float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var literal) && float.IsFinite(literal)
            ? literal
            : origin + (defaultFraction * extent);
    }

    /// <summary>
    ///     Reads a mask region's own <c>width</c>/<c>height</c> attribute (see
    ///     <see cref="ComputeMaskRegionLocalBounds"/>'s remarks for the full percentage-vs-literal/
    ///     <c>maskUnits</c> interpretation rules).
    /// </summary>
    /// <param name="raw">The raw attribute text, or <see langword="null"/> if absent.</param>
    /// <param name="defaultFraction">The bbox-relative fraction to fall back to when absent/unparseable.</param>
    /// <param name="extent">The reference bounds' own extent on this axis.</param>
    /// <param name="isUserSpaceOnUse">Whether <c>maskUnits="userSpaceOnUse"</c> was specified.</param>
    /// <returns>The resolved local-space extent.</returns>
    private static float ReadMaskRegionExtent(string? raw, float defaultFraction, float extent, bool isUserSpaceOnUse)
    {
        if (raw == null)
        {
            return defaultFraction * extent;
        }

        var trimmed = raw.Trim();
        if (trimmed.Length == 0)
        {
            return defaultFraction * extent;
        }

        if (trimmed.EndsWith('%') || !isUserSpaceOnUse)
        {
            var fraction = ParsePercentOrNumber(trimmed, 1f) ?? defaultFraction;
            return fraction * extent;
        }

        return float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var literal) && float.IsFinite(literal)
            ? literal
            : defaultFraction * extent;
    }

    /// <summary>
    ///     Computes <paramref name="maskElement"/>'s own mask region and converts it to an integer
    ///     pixel-space bounding box - the mask counterpart of
    ///     <see cref="ComputeFilterRegionPixelBounds(XElement, Rect, Matrix3x2)"/>, sharing its
    ///     exact pixel-space conversion/validation core (see <see cref="ConvertLocalRegionToPixelBounds"/>).
    /// </summary>
    /// <param name="maskElement">The resolved <c>mask</c> element.</param>
    /// <param name="bounds">The referencing shape's own stroke-expanded local-space painted-content bounds.</param>
    /// <param name="transform">The accumulated transform from local space into pixel space.</param>
    /// <returns>
    ///     The pixel-space region as <c>(X, Y, Width, Height)</c>, or <see langword="null"/> under
    ///     the same conditions as <see cref="ComputeMaskRegionLocalBounds"/>/
    ///     <see cref="ConvertLocalRegionToPixelBounds"/>.
    /// </returns>
    private static (int X, int Y, int Width, int Height)? ComputeMaskRegionPixelBounds(
        XElement maskElement, Rect bounds, Matrix3x2 transform)
    {
        var localRegion = ComputeMaskRegionLocalBounds(maskElement, bounds);
        return localRegion == null ? null : ConvertLocalRegionToPixelBounds(localRegion.Value, transform);
    }
}
