// cspell:ignore linecap linejoin dasharray dashoffset miterlimit anchor xlink href
// cspell:ignore Glyf Loca
// cspell:ignore evenodd nonzero viewbox gradientunits gradienttransform spreadmethod
// cspell:ignore userspaceonuse objectboundingbox skewx skewy tspan
// cspell:ignore rasterizing unparseable rrggbb sizeless bbox moveto multiplicatively pillarbox SMIL uncatchable formedness
// cspell:ignore unblurred premult mediatype letterboxed pillarboxed
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
using DemaConsulting.CanvasNet.Geometry;

namespace DemaConsulting.CanvasNet.Codecs;

public static partial class SvgCodec
{
    // ================================================================================================
    // <image> element rendering (Phase 4 of the SVG roadmap)
    // ================================================================================================
    //
    // An <image> element embeds a raster image, placed at x/y/width/height and fitted per
    // preserveAspectRatio, exactly like an <img>/replaced-element box in CSS. This codec supports
    // exactly one href form: a base64-encoded "data:" URI whose MIME type matches one of this
    // codec's own sibling raster codecs (PngCodec/JpegCodec/BmpCodec/TiffCodec/GifCodec) - no new
    // decode logic is written anywhere in this file, only base64/data-URI parsing and dispatch to
    // each codec's own existing Load(Stream) entry point.
    //
    // Explicitly out of scope, documented here as a single reference point (mirroring
    // SvgCodec.Patterns.cs's/SvgCodec.ClippingAndMasking.cs's identical convention):
    //
    //   - A non-"data:" href (a file path, or an absolute/relative URL) is a documented, safe
    //     no-op: the element renders nothing, no exception is thrown, and - critically - no
    //     filesystem or network I/O of any kind is ever performed for it. This is a deliberate
    //     security decision, not a missing feature: none of this codec's four Load/
    //     LoadWithFontFaces overloads (see SvgCodec.cs) accept a base path, resource-resolver
    //     delegate, or HttpClient, and there is no existing precedent anywhere in this codebase for
    //     resolving an external resource reference during decode. Inventing one here, solely for
    //     <image>, would introduce either an arbitrary-file-read vector (a base path inferred from
    //     an unrelated Load(string path, ...) call, followed by a crafted "../../secret" href) or
    //     an SSRF vector (fetching an attacker-controlled internal-network URL) - both avoided
    //     entirely by never constructing a File.Exists/WebRequest/Stream for anything but a "data:"
    //     URI's own in-memory payload.
    //   - A nested SVG document - either "data:image/svg+xml" or any external ".svg" reference (the
    //     latter already covered by the external-href-as-no-op decision above) - is also a
    //     documented, safe no-op, for the same reason feImage (see SvgCodec.Filters.cs's own
    //     remarks) is out of scope: this codec's entire rendering pipeline (RenderElement,
    //     RenderContext, the whole-document IdIndex, and every GeometryWorkBudget/
    //     FilterWorkBudget/BoundsPrePassWorkBudget) is built around exactly one root document
    //     parsed once by Load. Supporting a second, nested document embedded in an <image> href
    //     would require re-parsing a second XDocument, re-running BuildIdIndex, establishing fresh
    //     (or shared - itself a non-trivial design question) work budgets, and a new
    //     recursion-depth guard distinct from MaxElementDepth/MaxUseDepth - a disproportionate cost
    //     for a rarely-used feature, deferred to a future phase.
    //   - Only nearest-neighbor sampling is performed (see SampleImageIntoRegion below), matching
    //     this codec's one existing raster-resampling precedent (SampleTileIntoRegion in
    //     SvgCodec.Patterns.cs) rather than introducing a second, inconsistent bilinear/bicubic
    //     quality tier solely for <image>.
    //   - An <image> with no explicit width/height (or an explicit "0") renders nothing: this codec
    //     does not implement SVG2's "auto" intrinsic-size resolution from the decoded raster (i.e.
    //     inferring width/height from the decoded image when the attribute is absent) - a
    //     documented simplification, mirroring this class's other "sizeless fallback" decisions
    //     elsewhere (see GetFloatAttribute's own default-value parameter).

    /// <summary>
    ///     Renders an <c>image</c> element: resolves its placement rect and <c>data:</c> URI raster
    ///     payload, fits the decoded raster into the placement rect per <c>preserveAspectRatio</c>,
    ///     and composites the result through the same <c>filter</c>/<c>clip-path</c>/<c>mask</c>/
    ///     <c>opacity</c> effects pipeline every other shape/text element uses.
    /// </summary>
    /// <param name="element">The <c>image</c> element.</param>
    /// <param name="state">The cascaded render state, supplying the current viewport size (for percentage resolution) and <see cref="RenderState.Opacity"/>.</param>
    /// <param name="transform">The accumulated transform from this element's own local space into pixel space.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="useDepth">The current <c>use</c>-reference nesting depth, forwarded to <see cref="ApplyMask"/>'s own content rendering.</param>
    /// <param name="elementDepth">The current recursion depth, forwarded to <see cref="ApplyMask"/>'s own content rendering.</param>
    /// <param name="markerDepth">The current <c>marker</c>-reference nesting depth, forwarded unchanged.</param>
    /// <param name="totalElements">The running total-rendered-elements count, forwarded to <see cref="ApplyMask"/>'s own content rendering.</param>
    /// <param name="workBudget">The shared geometry-parsing work budget, forwarded to <c>clip-path</c> child geometry building and to <see cref="ApplyMask"/>'s own content.</param>
    /// <param name="filterWorkBudget">
    ///     The shared cumulative filter-evaluation work budget (see <see cref="FilterWorkBudget"/>),
    ///     charged the combined effects-pipeline region-area cost (identical to
    ///     <see cref="ComputeEffectsPipelineWorkUnits"/>'s own formula, reused verbatim) plus the
    ///     raw base64 payload's own character length, charged <em>before</em> the (comparatively
    ///     expensive) base64 decode and raster codec <c>Load</c> call are attempted - mirroring
    ///     <see cref="RenderPatternFill"/>'s identical "charge before allocate" convention. This
    ///     additional length term is what bounds a huge base64 blob (or the same moderately-sized
    ///     blob decoded repeatedly via <c>&lt;use&gt;</c> fan-out) from amplifying into unbounded
    ///     aggregate decode CPU cost, a risk the plain region-area charge alone does not cover
    ///     (a small placement rect can still reference an enormous base64 payload). A decline is a
    ///     tolerant no-op, exactly like a declined pattern-fill charge - never a thrown exception.
    /// </param>
    /// <param name="boundsPrePassBudget">The shared cumulative bounds-pre-pass work budget, forwarded to <see cref="ApplyMask"/>'s own content in case it itself contains a filtered group.</param>
    /// <param name="suppressEffects">
    ///     <see langword="true"/> when <paramref name="element"/> is being rendered as part of a
    ///     <c>marker</c> element's own content - <paramref name="element"/>'s own <c>filter</c>/
    ///     <c>clip-path</c>/<c>mask</c> attributes are then never resolved, mirroring
    ///     <see cref="RenderShapeWithEffects"/>'s identical parameter.
    /// </param>
    /// <remarks>
    ///     Unlike <see cref="RenderShapeWithEffects"/>, there is no separate "no effects" fast path:
    ///     an <c>image</c> always needs a region-sized sampling pass to place its decoded raster
    ///     regardless of whether <c>clip-path</c>/<c>mask</c>/<c>filter</c> apply, since there is no
    ///     cheaper "direct paint" primitive to fall back to (unlike <c>RenderShape</c>'s own direct-
    ///     composite fast path, which exists purely to preserve pre-Phase-2 shape-rendering
    ///     byte-identity). This method tolerantly renders nothing (leaving whatever was already
    ///     painted underneath fully visible) for every one of the following conditions, none of
    ///     which abort the whole document: a non-finite/non-positive <c>width</c>/<c>height</c>
    ///     (including the default of <c>0</c> when either is absent - see this class's remarks); a
    ///     <c>href</c> that is not a well-formed <c>data:...;base64,...</c> URI (covers a non-
    ///     <c>data:</c> href, i.e. a file path/URL, and a data URI missing the required
    ///     <c>;base64</c> token); an unrecognized/unsupported MIME type (including
    ///     <c>image/svg+xml</c>); a non-finite/non-invertible composed placement transform; a
    ///     degenerate/oversized effects region (mirroring every other effects-capable element's
    ///     identical <see cref="ConvertLocalRegionToPixelBounds"/> reuse); a declined
    ///     <paramref name="filterWorkBudget"/> charge; or a malformed base64 payload/malformed or
    ///     oversized raster payload (each already-sibling raster codec's own <c>Load</c> throws
    ///     <see cref="InvalidDataException"/> for the latter, already enforcing
    ///     <see cref="Surface.MaxDimension"/> internally - see this class's remarks).
    /// </remarks>
    private static void RenderImageWithEffects(
        XElement element,
        RenderState state,
        Matrix3x2 transform,
        RenderContext context,
        int useDepth,
        int elementDepth,
        int markerDepth,
        ref int totalElements,
        GeometryWorkBudget workBudget,
        FilterWorkBudget filterWorkBudget,
        BoundsPrePassWorkBudget boundsPrePassBudget,
        bool suppressEffects)
    {
        // Resolve the placement rect first - a degenerate size means "render nothing" before any
        // href parsing/decoding is even attempted, exactly mirroring RenderPatternFill's own
        // "validate cheap geometry before expensive work" ordering
        var x = GetFloatAttribute(element, "x", state, PercentageAxis.Horizontal);
        var y = GetFloatAttribute(element, "y", state, PercentageAxis.Vertical);
        var width = GetFloatAttribute(element, "width", state, PercentageAxis.Horizontal);
        var height = GetFloatAttribute(element, "height", state, PercentageAxis.Vertical);
        if (!float.IsFinite(width) || !float.IsFinite(height) || width <= 0f || height <= 0f)
        {
            return;
        }

        // Resolve the href - only a well-formed base64 "data:" URI with a recognized raster MIME
        // type is ever decoded; every other form (a non-"data:" href, a data URI missing
        // ";base64", or an unsupported/nested-SVG MIME type) tolerantly renders nothing without
        // ever touching the filesystem or network - see this class's remarks
        var href = GetHrefAttribute(element);
        if (href == null || !TryParseDataUri(href, out var mimeType, out var base64Payload))
        {
            return;
        }

        var decodeRaster = ResolveRasterDecoder(mimeType);
        if (decodeRaster == null)
        {
            return;
        }

        // Resolve the same filter/clip-path/mask attributes, and the same effects-region
        // precedence, every other effects-capable element resolves - the placement rect itself
        // stands in for a shape's own stroke-expanded painted bounds (an <image> has no stroke
        // concept, so no ExpandBoundsForStroke call is needed)
        var filterElement = suppressEffects ? null : ResolveFilterElement(element, context);
        var clipPathElement = suppressEffects ? null : ResolveClipPathElement(element, context);
        var maskElement = suppressEffects ? null : ResolveMaskElement(element, context);

        var placementRect = new Rect(x, y, width, height);
        var region = ResolveEffectsRegionPixelBounds(filterElement, clipPathElement, maskElement, placementRect, transform);
        if (region == null)
        {
            return;
        }

        var (pixelX, pixelY, pixelWidth, pixelHeight) = region.Value;

        // Mirrors RenderShapeEffectsPipeline's own per-filter budget guard: a filter that does not
        // survive its own primitive-count/region-area ceiling simply never applies, but a
        // clip-path/mask that is also present is unaffected and still applies
        var primitiveCount = filterElement == null ? 0 : CountFilterPrimitiveWorkUnits(filterElement);
        var filterApplies = filterElement != null && primitiveCount != 0 &&
            IsFilterPrimitiveWorkWithinBudget(primitiveCount, pixelWidth, pixelHeight);

        // Charge the shared cumulative FilterWorkBudget BEFORE the base64 decode/raster Load is
        // attempted - see this method's own remarks on the additional base64-length term
        var workUnits = ComputeEffectsPipelineWorkUnits(
            filterApplies, primitiveCount, clipPathElement != null, maskElement != null, pixelWidth, pixelHeight)
            + base64Payload.Length;
        if (!filterWorkBudget.TryCharge(workUnits))
        {
            return;
        }

        // Decode the base64 payload and dispatch to the matching sibling raster codec's own
        // Load(Stream) - any failure (malformed base64, malformed/truncated/oversized raster
        // bytes, or a well-formed-but-unsupported raster feature such as PNG Adam7 interlacing -
        // see UnsupportedImageFeatureException's own remarks on why that type is not an
        // InvalidDataException and would otherwise slip past this filter) is a tolerant
        // per-element no-op, matching this codec's existing dangling-reference tolerance policy,
        // never propagating past this one element. IOException is caught alongside the sealed
        // UnsupportedImageFeatureException so any other sibling raster codec's own recognized-
        // but-unsupported-feature exception (should one ever be introduced) is skipped just as
        // tolerantly, without requiring this filter to be revisited.
        Surface decodedSurface;
        try
        {
            var bytes = Convert.FromBase64String(base64Payload);
            using var payloadStream = new MemoryStream(bytes);
            decodedSurface = decodeRaster(payloadStream);
        }
        catch (Exception ex) when (ex is FormatException or InvalidDataException or ArgumentOutOfRangeException
            or IOException)
        {
            return;
        }

        // Compute the transform mapping the decoded raster's own intrinsic pixel space into pixel
        // space: preserveAspectRatio fits the intrinsic size into the placement rect (origin at
        // the local space's own zero, sized width x height), then the placement rect's own x/y
        // origin and this element's own accumulated transform are composed on top - see this
        // class's remarks and SvgCodec.PreserveAspectRatio.cs's own identical reuse for <pattern>
        var preserveAspectRatio = GetPreserveAspectRatio(element);
        var fitTransform = ComputePreserveAspectRatioFit(
            Vector2.Zero, new Vector2(decodedSurface.Width, decodedSurface.Height), width, height, preserveAspectRatio);
        var imageToPixel = fitTransform * Matrix3x2.CreateTranslation(x, y) * transform;
        if (!IsFiniteTransform(imageToPixel) || !Matrix3x2.Invert(imageToPixel, out var pixelToImage))
        {
            return;
        }

        // Sample the decoded raster into a fresh region-sized buffer via non-tiling
        // inverse-transform sampling (see SampleImageIntoRegion), then apply clip-path -> mask ->
        // filter in SVG's defined order, exactly like RenderShapeEffectsPipeline
        var content = SampleImageIntoRegion(decodedSurface, pixelX, pixelY, pixelWidth, pixelHeight, pixelToImage);

        if (clipPathElement != null)
        {
            ApplyClipPath(clipPathElement, content, placementRect, pixelX, pixelY, transform, state, context, workBudget);
        }

        if (maskElement != null)
        {
            ApplyMask(
                maskElement, content, placementRect, pixelX, pixelY, transform, state, context,
                useDepth, elementDepth, markerDepth, ref totalElements, workBudget, filterWorkBudget, boundsPrePassBudget);
        }

        var finalSurface = filterApplies
            ? EvaluateFilterChain(
                filterElement!, content, transform, pixelX, pixelY, state, context, useDepth, elementDepth, markerDepth,
                ref totalElements, workBudget, filterWorkBudget, boundsPrePassBudget)
            : content;

        CompositeFilterResultOntoCanvas(finalSurface, pixelX, pixelY, context.Surface, state.Opacity);
    }

    /// <summary>
    ///     Parses <paramref name="href"/> as a base64-encoded <c>data:</c> URI
    ///     (<c>data:[mediatype];base64,&lt;data&gt;</c>) - the only <c>href</c> form
    ///     <see cref="RenderImageWithEffects"/> ever decodes. A hand-rolled parser, matching this
    ///     codec's own "no third-party XML/SVG package" policy (see <c>SvgCodec.cs</c>'s class
    ///     doc), since a data URI has no existing BCL parsing facility this codec already depends
    ///     on.
    /// </summary>
    /// <param name="href">The raw <c>href</c>/<c>xlink:href</c> attribute value.</param>
    /// <param name="mimeType">
    ///     The parsed MIME type token, trimmed and lower-invariant (for example <c>image/png</c>),
    ///     or <see cref="string.Empty"/> if this method returns <see langword="false"/>.
    /// </param>
    /// <param name="base64Payload">
    ///     The raw, not-yet-decoded/validated base64 payload substring, or
    ///     <see cref="string.Empty"/> if this method returns <see langword="false"/>.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> if <paramref name="href"/> begins with the <c>data:</c> scheme
    ///     (matched case-insensitively, for tolerance - RFC 2397's scheme is technically
    ///     case-sensitive, but URI schemes are conventionally parsed case-insensitively in
    ///     practice), contains a comma separating the header from the payload, and its header
    ///     contains a <c>base64</c> token; otherwise <see langword="false"/> - covering every
    ///     non-<c>data:</c> href (a file path/URL, tolerantly rendering nothing without any
    ///     filesystem/network access - see this class's remarks) and every percent-encoded/
    ///     plain-text data URI (out of scope, see this class's remarks). This method does not
    ///     itself validate whether <paramref name="mimeType"/> is a supported raster format - that
    ///     is <see cref="ResolveRasterDecoder"/>'s own, separate responsibility.
    /// </returns>
    private static bool TryParseDataUri(string href, out string mimeType, out string base64Payload)
    {
        mimeType = string.Empty;
        base64Payload = string.Empty;

        // Only the "data:" scheme is ever recognized - every other scheme (or no scheme at all,
        // i.e. a bare relative file path) falls through to false here, and this method never
        // constructs a File.Exists/WebRequest/Stream for any of them, which is what makes the
        // external-href-as-no-op security decision (see this class's remarks) hold in the
        // implementation, not just in documentation
        if (!href.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var afterScheme = href[5..];
        var commaIndex = afterScheme.IndexOf(',');
        if (commaIndex < 0)
        {
            return false;
        }

        var header = afterScheme[..commaIndex];
        var payload = afterScheme[(commaIndex + 1)..];

        // Only the ";base64" encoding is supported - a percent-encoded/plain-text data URI is out
        // of scope (rare for raster images, and its percent-decoding semantics are not otherwise
        // needed anywhere else in this codec)
        var headerParts = header.Split(';');
        if (!headerParts.Any(part => string.Equals(part, "base64", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        mimeType = headerParts[0].Trim().ToLowerInvariant();
        base64Payload = payload;
        return true;
    }

    /// <summary>
    ///     Maps a <c>data:</c> URI's parsed MIME type token to the matching sibling raster codec's
    ///     own <c>Load(Stream)</c> entry point - pure dispatch, no new decode logic is written
    ///     anywhere in this file (see this class's remarks).
    /// </summary>
    /// <param name="mimeType">The lower-invariant MIME type token parsed by <see cref="TryParseDataUri"/>.</param>
    /// <returns>
    ///     <see cref="PngCodec.Load(Stream)"/>, <see cref="JpegCodec.Load(Stream)"/>,
    ///     <see cref="BmpCodec.Load(Stream)"/>, <see cref="TiffCodec.Load(Stream)"/>, or
    ///     <see cref="GifCodec.Load(Stream)"/> for the matching recognized MIME type; otherwise
    ///     <see langword="null"/> for any unrecognized MIME type - including <c>image/svg+xml</c>
    ///     (a nested SVG document), listed explicitly below (rather than left to fall through to
    ///     the default case implicitly) to document that its exclusion is this phase's own
    ///     deliberate scope decision, not an oversight (see this class's remarks on nested-SVG-in-
    ///     image scoping).
    /// </returns>
    private static Func<Stream, Surface>? ResolveRasterDecoder(string mimeType) => mimeType switch
    {
        "image/png" => PngCodec.Load,
        "image/jpeg" or "image/jpg" => JpegCodec.Load,
        "image/bmp" or "image/x-bmp" or "image/x-ms-bmp" => BmpCodec.Load,
        "image/tiff" or "image/x-tiff" => TiffCodec.Load,
        "image/gif" => GifCodec.Load,
        "image/svg+xml" => null,
        _ => null
    };

    /// <summary>
    ///     Samples <paramref name="decodedSurface"/> into a fresh region-sized buffer, one pixel at
    ///     a time: each destination pixel is inverse-mapped through
    ///     <paramref name="pixelToImage"/> back into the decoded raster's own intrinsic pixel
    ///     space and sampled nearest-neighbor, or left fully transparent when the inverse-mapped
    ///     point falls outside <c>[0, decodedSurface.Width) x [0, decodedSurface.Height)</c> - the
    ///     letterboxed/pillarboxed (or sliced-away) area a non-<c>none</c>/non-exactly-matching
    ///     <c>preserveAspectRatio</c> fit can leave within the placement rect.
    /// </summary>
    /// <param name="decodedSurface">The already fully decoded raster image.</param>
    /// <param name="pixelX">The painted region's own pixel-space X origin.</param>
    /// <param name="pixelY">The painted region's own pixel-space Y origin.</param>
    /// <param name="pixelWidth">The painted region's own pixel width.</param>
    /// <param name="pixelHeight">The painted region's own pixel height.</param>
    /// <param name="pixelToImage">The inverse of the composed image-content-space-to-pixel-space transform.</param>
    /// <returns>A new region-sized buffer, ready for <see cref="CompositeFilterResultOntoCanvas"/> (after any clip/mask/filter is applied).</returns>
    /// <remarks>
    ///     Deliberately distinct from <c>SampleTileIntoRegion</c> (<c>SvgCodec.Patterns.cs</c>): a
    ///     single <c>&lt;image&gt;</c> is drawn once, never tiled, so there is no
    ///     <c>WrapCoordinate</c> modulo step here - an inverse-mapped point outside the decoded
    ///     raster's own bounds is simply left transparent rather than wrapped. There is also no
    ///     separate <c>coverage</c> buffer to multiply against: unlike a pattern-filled shape's own
    ///     arbitrary outline, the region here already <em>is</em> the placement rect converted to
    ///     pixel space, so every destination pixel is either "inside the fitted image" (sampled) or
    ///     "outside it" (left transparent) - there is no third, partially-covered case to blend.
    /// </remarks>
    private static Surface SampleImageIntoRegion(
        Surface decodedSurface,
        int pixelX,
        int pixelY,
        int pixelWidth,
        int pixelHeight,
        Matrix3x2 pixelToImage)
    {
        var result = new Surface(pixelWidth, pixelHeight);
        var decodedWidth = decodedSurface.Width;
        var decodedHeight = decodedSurface.Height;

        for (var row = 0; row < pixelHeight; row++)
        {
            var resultRow = result.GetRowSpan(row);

            for (var col = 0; col < pixelWidth; col++)
            {
                // Sample at the destination pixel's own center, matching this codec's existing
                // pixel-center sampling convention elsewhere (for example SampleTileIntoRegion)
                var canvasPoint = new Vector2(pixelX + col + 0.5f, pixelY + row + 0.5f);
                var imagePoint = Vector2.Transform(canvasPoint, pixelToImage);
                if (!float.IsFinite(imagePoint.X) || !float.IsFinite(imagePoint.Y))
                {
                    continue;
                }

                if (imagePoint.X < 0f || imagePoint.X >= decodedWidth || imagePoint.Y < 0f || imagePoint.Y >= decodedHeight)
                {
                    // Outside the decoded raster's own intrinsic bounds entirely - left fully
                    // transparent instead of wrapped, unlike SampleTileIntoRegion's tiling wrap
                    continue;
                }

                var sourceX = Math.Clamp((int)imagePoint.X, 0, decodedWidth - 1);
                var sourceY = Math.Clamp((int)imagePoint.Y, 0, decodedHeight - 1);
                resultRow[col] = decodedSurface.GetRowSpan(sourceY)[sourceX];
            }
        }

        return result;
    }
}
