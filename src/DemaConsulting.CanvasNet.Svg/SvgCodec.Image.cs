// cspell:ignore linecap linejoin dasharray dashoffset miterlimit anchor xlink href
// cspell:ignore Glyf Loca
// cspell:ignore evenodd nonzero viewbox gradientunits gradienttransform spreadmethod
// cspell:ignore userspaceonuse objectboundingbox skewx skewy tspan
// cspell:ignore rasterizing unparseable rrggbb sizeless bbox moveto multiplicatively pillarbox SMIL uncatchable formedness
// cspell:ignore unblurred premult mediatype letterboxed pillarboxed
using System.Numerics;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Geometry;

namespace DemaConsulting.CanvasNet.Svg;

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
    //   - The raster is resampled smoothly (see SampleImageIntoRegion below): an axis-aligned
    //     placement is resampled once to its device-pixel rectangle by the shared SurfaceResampler
    //     engine (area averaging when shrunk, Catmull-Rom bicubic when enlarged); a rotated or
    //     skewed placement falls back to inverse-mapped bilinear sampling of a prefiltered source.
    //     The CSS "image-rendering" property values pixelated, crisp-edges and optimizeSpeed select
    //     nearest-neighbor sampling instead.
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
    /// <param name="scope">
    ///     The recursion depths and shared work budgets, forwarded to <see cref="ApplyMask"/>'s own
    ///     content rendering (and, for the geometry budget, to <c>clip-path</c> child geometry
    ///     building). Its <see cref="RenderScope.FilterWorkBudget"/> is the shared cumulative
    ///     filter-evaluation work budget (see <see cref="FilterWorkBudget"/>),
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
    ///     Its <see cref="RenderScope.BoundsPrePassBudget"/> is forwarded to <see cref="ApplyMask"/>'s
    ///     own content in case it itself contains a filtered group. A
    ///     <see cref="RenderScope.MarkerDepth"/> greater than zero means <paramref name="element"/>
    ///     is being rendered as part of a <c>marker</c> element's own content, so its own
    ///     <c>filter</c>/<c>clip-path</c>/<c>mask</c> attributes are never resolved, mirroring
    ///     <see cref="RenderShapeWithEffects"/>'s identical behavior.
    /// </param>
    /// <param name="totalElements">The running total-rendered-elements count, forwarded to <see cref="ApplyMask"/>'s own content rendering.</param>
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
    ///     <see cref="RenderScope.FilterWorkBudget"/> charge; or a malformed base64 payload/malformed or
    ///     oversized raster payload (each already-sibling raster codec's own <c>Load</c> throws
    ///     <see cref="InvalidDataException"/> for the latter, already enforcing
    ///     <see cref="Surface.MaxDimension"/> internally - see this class's remarks).
    /// </remarks>
    private static void RenderImageWithEffects(
        XElement element,
        RenderState state,
        Matrix3x2 transform,
        RenderContext context,
        RenderScope scope,
        ref int totalElements)
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
        if (!TryResolveImageSource(element, out var decodeRaster, out var base64Payload))
        {
            return;
        }

        // Resolve the same filter/clip-path/mask attributes, and the same effects-region
        // precedence, every other effects-capable element resolves - the placement rect itself
        // stands in for a shape's own stroke-expanded painted bounds (an <image> has no stroke
        // concept, so no ExpandBoundsForStroke call is needed)
        var placementRect = new Rect(x, y, width, height);
        var effects = ResolveImageEffects(element, context, scope.MarkerDepth > 0, placementRect, transform);
        if (effects == null)
        {
            return;
        }

        var (pixelX, pixelY, pixelWidth, pixelHeight) = effects.Value.Region;

        // Charge the shared cumulative FilterWorkBudget BEFORE the base64 decode/raster Load is
        // attempted - see this method's own remarks on the additional base64-length term
        var workUnits = ComputeEffectsPipelineWorkUnits(
            effects.Value.FilterApplies,
            effects.Value.PrimitiveCount,
            effects.Value.Filter != null && FilterUsesSourceAlpha(effects.Value.Filter),
            effects.Value.ClipPath != null,
            effects.Value.Mask != null,
            pixelWidth,
            pixelHeight)
            + base64Payload.Length;
        if (!scope.FilterWorkBudget.TryCharge(workUnits))
        {
            return;
        }

        var decodedSurface = TryDecodeImageSurface(decodeRaster, base64Payload);
        if (decodedSurface == null)
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
        scope.FilterWorkBudget.RecordSurfaceAllocated(pixelWidth, pixelHeight);
        var content = SampleImageIntoRegion(
            decodedSurface, new PixelRect(pixelX, pixelY, pixelWidth, pixelHeight), imageToPixel, pixelToImage, state.PixelatedImages);

        ApplyEffectsAndComposite(
            new EffectsSet(effects.Value.Filter, effects.Value.ClipPath, effects.Value.Mask),
            effects.Value.FilterApplies,
            new EffectsTarget(content, effects.Value.PlacementRect, pixelX, pixelY, transform),
            state,
            context,
            scope,
            ref totalElements);
    }

    /// <summary>
    ///     The resolved <c>filter</c>/<c>clip-path</c>/<c>mask</c> effects of an <c>image</c>
    ///     element together with its placement rect and pixel-space effects region.
    /// </summary>
    /// <param name="Filter">The resolved <c>filter</c> element, or <see langword="null"/>.</param>
    /// <param name="ClipPath">The resolved <c>clipPath</c> element, or <see langword="null"/>.</param>
    /// <param name="Mask">The resolved <c>mask</c> element, or <see langword="null"/>.</param>
    /// <param name="FilterApplies">Whether the filter survives its own primitive-count/region-area budget.</param>
    /// <param name="PrimitiveCount">The filter's primitive work-unit count (0 when there is no filter).</param>
    /// <param name="PlacementRect">The element's placement rect in its own local space.</param>
    /// <param name="Region">The pixel-space effects region.</param>
    private readonly record struct ImageEffects(
        XElement? Filter,
        XElement? ClipPath,
        XElement? Mask,
        bool FilterApplies,
        int PrimitiveCount,
        Rect PlacementRect,
        (int X, int Y, int Width, int Height) Region);

    /// <summary>
    ///     Resolves the <c>href</c> of an <c>image</c> element into a raster decoder and its raw
    ///     base64 payload.
    /// </summary>
    /// <remarks>
    ///     Only a well-formed base64 <c>data:</c> URI with a recognized raster MIME type is ever
    ///     decoded; every other form (a non-<c>data:</c> href, a data URI missing
    ///     <c>;base64</c>, or an unsupported/nested-SVG MIME type) tolerantly yields
    ///     <see langword="false"/> without ever touching the filesystem or network - see this
    ///     class's remarks.
    /// </remarks>
    /// <param name="element">The <c>image</c> element.</param>
    /// <param name="decodeRaster">The matching raster decoder, or <see langword="null"/> on failure.</param>
    /// <param name="base64Payload">The raw base64 payload, or <see cref="string.Empty"/> on failure.</param>
    /// <returns><see langword="true"/> if the <c>href</c> is a decodable raster data URI.</returns>
    private static bool TryResolveImageSource(
        XElement element,
        out Func<Stream, Surface> decodeRaster,
        out string base64Payload)
    {
        decodeRaster = null!;
        base64Payload = string.Empty;
        var href = GetHrefAttribute(element);
        if (href == null || !TryParseDataUri(href, out var mimeType, out base64Payload))
        {
            return false;
        }

        var decoder = ResolveRasterDecoder(mimeType);
        if (decoder == null)
        {
            return false;
        }

        decodeRaster = decoder;
        return true;
    }

    /// <summary>
    ///     Resolves an <c>image</c> element's <c>filter</c>/<c>clip-path</c>/<c>mask</c> references
    ///     and the pixel-space effects region they imply.
    /// </summary>
    /// <param name="element">The <c>image</c> element.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="suppressEffects">Whether the element's own effects are never resolved (marker content).</param>
    /// <param name="placementRect">The placement rect, standing in for a shape's painted bounds.</param>
    /// <param name="transform">The accumulated local-to-pixel transform.</param>
    /// <returns>The resolved effects, or <see langword="null"/> for a degenerate/oversized region.</returns>
    private static ImageEffects? ResolveImageEffects(
        XElement element,
        RenderContext context,
        bool suppressEffects,
        Rect placementRect,
        Matrix3x2 transform)
    {
        var filterElement = suppressEffects ? null : ResolveFilterElement(element, context);
        var clipPathElement = suppressEffects ? null : ResolveClipPathElement(element, context);
        var maskElement = suppressEffects ? null : ResolveMaskElement(element, context);

        var region = ResolveEffectsRegionPixelBounds(filterElement, clipPathElement, maskElement, placementRect, transform);
        if (region == null)
        {
            return null;
        }

        // Mirrors RenderShapeEffectsPipeline's own per-filter budget guard: a filter that does not
        // survive its own primitive-count/region-area ceiling simply never applies, but a
        // clip-path/mask that is also present is unaffected and still applies
        var primitiveCount = filterElement == null ? 0 : CountFilterPrimitiveWorkUnits(filterElement);
        var filterApplies = filterElement != null && primitiveCount != 0 &&
            IsFilterPrimitiveWorkWithinBudget(primitiveCount, region.Value.Width, region.Value.Height);

        return new ImageEffects(filterElement, clipPathElement, maskElement, filterApplies, primitiveCount, placementRect, region.Value);
    }

    /// <summary>
    ///     Decodes a base64 raster payload with the supplied sibling raster codec decoder.
    /// </summary>
    /// <remarks>
    ///     Any failure (malformed base64, malformed/truncated/oversized raster bytes, or a
    ///     well-formed-but-unsupported raster feature such as PNG Adam7 interlacing - see
    ///     UnsupportedImageFeatureException's own remarks on why that type is not an
    ///     <see cref="InvalidDataException"/> and would otherwise slip past this filter) is a
    ///     tolerant per-element no-op, matching this codec's existing dangling-reference tolerance
    ///     policy, never propagating past this one element. <see cref="IOException"/> is caught
    ///     alongside the sealed UnsupportedImageFeatureException so any other sibling raster
    ///     codec's own recognized-but-unsupported-feature exception (should one ever be
    ///     introduced) is skipped just as tolerantly, without requiring this filter to be
    ///     revisited.
    /// </remarks>
    /// <param name="decodeRaster">The raster decoder.</param>
    /// <param name="base64Payload">The raw base64 payload.</param>
    /// <returns>The decoded surface, or <see langword="null"/> on any decode failure.</returns>
    private static Surface? TryDecodeImageSurface(Func<Stream, Surface> decodeRaster, string base64Payload)
    {
        try
        {
            var bytes = Convert.FromBase64String(base64Payload);
            using var payloadStream = new MemoryStream(bytes);
            return decodeRaster(payloadStream);
        }
        catch (Exception ex) when (ex is FormatException or InvalidDataException or ArgumentOutOfRangeException
            or IOException)
        {
            return null;
        }
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
    ///     Maps a <c>data:</c> URI's parsed MIME type token to a raster decoder - pure dispatch
    ///     through the core <see cref="ImageFormats.FromMediaType(string?)"/> /
    ///     <see cref="ImageDecoder.Load(Stream, ImageFormat)"/> facade; no decode logic is written
    ///     anywhere in this file (see this class's remarks).
    /// </summary>
    /// <param name="mimeType">The lower-invariant MIME type token parsed by <see cref="TryParseDataUri"/>.</param>
    /// <returns>
    ///     A decoder over <see cref="PngCodec"/>, <see cref="JpegCodec"/>, <see cref="BmpCodec"/>,
    ///     <see cref="TiffCodec"/>, or <see cref="GifCodec"/> for the matching recognized MIME type;
    ///     otherwise <see langword="null"/> for any unrecognized MIME type - including
    ///     <c>image/svg+xml</c> (a nested SVG document) and JPEG 2000 (which the core facade
    ///     recognizes but this codec deliberately does not embed), so their exclusion is this
    ///     codec's own deliberate scope decision, not an oversight (see this class's remarks on
    ///     nested-SVG-in-image scoping).
    /// </returns>
    private static Func<Stream, Surface>? ResolveRasterDecoder(string mimeType)
    {
        var format = ImageFormats.FromMediaType(mimeType);
        return format is null or ImageFormat.Jpeg2000
            ? null
            : stream => ImageDecoder.Load(stream, format.Value);
    }

    /// <summary>
    ///     Samples <paramref name="decodedSurface"/> into a fresh region-sized buffer, one pixel at
    ///     a time: each destination pixel is inverse-mapped through
    ///     <paramref name="pixelToImage"/> back into the decoded raster's own intrinsic pixel
    ///     space and sampled smoothly (nearest-neighbor only for <c>image-rendering: pixelated</c>
    ///     and its synonyms), or left fully transparent when the inverse-mapped
    ///     point falls outside <c>[0, decodedSurface.Width) x [0, decodedSurface.Height)</c> - the
    ///     letterboxed/pillarboxed (or sliced-away) area a non-<c>none</c>/non-exactly-matching
    ///     <c>preserveAspectRatio</c> fit can leave within the placement rect.
    /// </summary>
    /// <param name="decodedSurface">The already fully decoded raster image.</param>
    /// <param name="region">The painted region's own pixel-space origin and size.</param>
    /// <param name="imageToPixel">The composed image-content-space-to-pixel-space transform.</param>
    /// <param name="pixelToImage">The inverse of <paramref name="imageToPixel"/>.</param>
    /// <param name="nearest"><see langword="true"/> to sample nearest-neighbor (<c>image-rendering: pixelated</c>) instead of smoothing.</param>
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
        PixelRect region,
        Matrix3x2 imageToPixel,
        Matrix3x2 pixelToImage,
        bool nearest)
    {
        var result = new Surface(region.Width, region.Height);
        var imageToRegion = imageToPixel * Matrix3x2.CreateTranslation(-region.X, -region.Y);

        // Axis-aligned placement: resample once to the exact device rectangle and copy it in
        if (TryCopyResampledWindow(decodedSurface, imageToRegion, nearest, result))
        {
            return result;
        }

        // Rotated or skewed: inverse-map each pixel; smooth mode samples a prefiltered source bilinearly
        SampleInverseMapped(decodedSurface, region, imageToRegion, pixelToImage, nearest, result);
        return result;
    }

    /// <summary>
    ///     Resamples <paramref name="decodedSurface"/> once to the exact device rectangle of an
    ///     axis-aligned placement and copies the result into <paramref name="result"/>.
    /// </summary>
    /// <param name="decodedSurface">The already fully decoded raster image.</param>
    /// <param name="imageToRegion">The image-content-space-to-region-space transform.</param>
    /// <param name="nearest"><see langword="true"/> to sample nearest-neighbor instead of smoothing.</param>
    /// <param name="result">The region-sized destination buffer.</param>
    /// <returns><see langword="true"/> if the placement was axis-aligned and handled; otherwise <see langword="false"/>.</returns>
    private static bool TryCopyResampledWindow(Surface decodedSurface, Matrix3x2 imageToRegion, bool nearest, Surface result)
    {
        if (!SurfaceResampler.TryResampleToDevice(
                decodedSurface, imageToRegion, result.Width, result.Height,
                nearest ? ResamplingMode.Nearest : ResamplingMode.Auto, out var window, out var origin))
        {
            return false;
        }

        using (window)
        {
            if (window is not null)
            {
                for (var row = 0; row < window.Height; row++)
                {
                    window.GetRowSpan(row).CopyTo(result.GetRowSpan(origin.Y + row).Slice(origin.X, window.Width));
                }
            }
        }

        return true;
    }

    /// <summary>
    ///     Inverse-maps every destination pixel of <paramref name="result"/> into
    ///     <paramref name="decodedSurface"/> (smooth mode samples a prefiltered source bilinearly).
    /// </summary>
    /// <param name="decodedSurface">The already fully decoded raster image.</param>
    /// <param name="region">The painted region's own pixel-space origin and size.</param>
    /// <param name="imageToRegion">The image-content-space-to-region-space transform.</param>
    /// <param name="pixelToImage">The inverse of the image-to-pixel transform.</param>
    /// <param name="nearest"><see langword="true"/> to sample nearest-neighbor instead of smoothing.</param>
    /// <param name="result">The region-sized destination buffer.</param>
    private static void SampleInverseMapped(
        Surface decodedSurface,
        PixelRect region,
        Matrix3x2 imageToRegion,
        Matrix3x2 pixelToImage,
        bool nearest,
        Surface result)
    {
        var smooth = decodedSurface;
        var factorX = 1;
        var factorY = 1;
        if (!nearest)
        {
            smooth = SurfaceResampler.Prefilter(decodedSurface, imageToRegion, out _, out factorX, out factorY);
        }

        using (ReferenceEquals(smooth, decodedSurface) ? null : smooth)
        {
            for (var row = 0; row < region.Height; row++)
            {
                var resultRow = result.GetRowSpan(row);

                for (var col = 0; col < region.Width; col++)
                {
                    // Sample at the destination pixel's own center, matching this codec's existing
                    // pixel-center sampling convention elsewhere (for example SampleTileIntoRegion)
                    var canvasPoint = new Vector2(region.X + col + 0.5f, region.Y + row + 0.5f);
                    var imagePoint = Vector2.Transform(canvasPoint, pixelToImage);
                    if (TrySampleImagePoint(decodedSurface, smooth, factorX, factorY, imagePoint, nearest, out var pixel))
                    {
                        resultRow[col] = pixel;
                    }
                }
            }
        }
    }

    /// <summary>
    ///     Samples one inverse-mapped point of a decoded raster, leaving it unsampled (transparent)
    ///     when the point is non-finite or outside the decoded raster's own intrinsic bounds.
    /// </summary>
    /// <param name="decodedSurface">The already fully decoded raster image.</param>
    /// <param name="smooth">The (possibly prefiltered) source used for smooth sampling.</param>
    /// <param name="factorX">The prefilter's horizontal reduction factor.</param>
    /// <param name="factorY">The prefilter's vertical reduction factor.</param>
    /// <param name="imagePoint">The inverse-mapped point in image-content space.</param>
    /// <param name="nearest"><see langword="true"/> to sample nearest-neighbor instead of smoothing.</param>
    /// <param name="pixel">The sampled pixel, or the default (transparent) value when unsampled.</param>
    /// <returns><see langword="true"/> if the point was sampled; otherwise <see langword="false"/>.</returns>
    private static bool TrySampleImagePoint(
        Surface decodedSurface,
        Surface smooth,
        int factorX,
        int factorY,
        Vector2 imagePoint,
        bool nearest,
        out Rgba32 pixel)
    {
        pixel = default;
        var decodedWidth = decodedSurface.Width;
        var decodedHeight = decodedSurface.Height;
        if (!float.IsFinite(imagePoint.X) || !float.IsFinite(imagePoint.Y))
        {
            return false;
        }

        // Outside the decoded raster's own intrinsic bounds entirely - left fully transparent
        // instead of wrapped, unlike SampleTileIntoRegion's tiling wrap
        if (imagePoint.X < 0f || imagePoint.X >= decodedWidth || imagePoint.Y < 0f || imagePoint.Y >= decodedHeight)
        {
            return false;
        }

        if (nearest)
        {
            var sourceX = Math.Clamp((int)imagePoint.X, 0, decodedWidth - 1);
            var sourceY = Math.Clamp((int)imagePoint.Y, 0, decodedHeight - 1);
            pixel = decodedSurface.GetRowSpan(sourceY)[sourceX];
        }
        else
        {
            pixel = SurfaceResampler.SampleBilinearReduced(smooth, imagePoint.X, imagePoint.Y, factorX, factorY, decodedWidth, decodedHeight);
        }

        return true;
    }
}
