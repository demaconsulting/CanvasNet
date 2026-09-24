// cspell:ignore linecap linejoin dasharray dashoffset miterlimit anchor xlink href
// cspell:ignore Glyf Loca
// cspell:ignore evenodd nonzero viewbox gradientunits gradienttransform spreadmethod
// cspell:ignore userspaceonuse objectboundingbox skewx skewy tspan
// cspell:ignore rasterizing unparseable rrggbb sizeless bbox moveto multiplicatively pillarbox SMIL uncatchable formedness
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

/// <summary>
///     Provides hand-rolled, dependency-free decoding (rasterization) of a useful subset of SVG
///     (Scalable Vector Graphics) documents onto a <see cref="Surface"/> pixel buffer.
/// </summary>
/// <remarks>
///     <para>
///     <c>SvgCodec</c> is decode-only: unlike <see cref="BmpCodec"/>, <see cref="PngCodec"/>,
///     <see cref="TiffCodec"/>, and <see cref="JpegCodec"/>, it has no <c>Save</c> method. SVG is a
///     textual, vector document format rather than a fixed-size raster format, so "saving" a
///     <see cref="Surface"/> back to SVG would require tracing raster pixels into vector shapes -
///     a fundamentally different (and out of scope) problem from rasterizing an existing document.
///     </para>
///     <para>
///     Parsing uses <see cref="System.Xml.Linq"/> (<see cref="XDocument"/>/<see cref="XElement"/>),
///     an approved dependency-free BCL facility - no third-party XML or SVG package is referenced.
///     </para>
///     <para>
///     <b>Supported subset.</b> Shapes <c>rect</c> (including rounded corners), <c>circle</c>,
///     <c>ellipse</c>, <c>line</c>, <c>polyline</c>, <c>polygon</c>, and <c>path</c> (the full
///     <c>d</c> mini-language: <c>M/m L/l H/h V/v C/c S/s Q/q T/t A/a Z/z</c>); <c>g</c> grouping
///     with attribute inheritance and nested <c>transform</c>; the <c>transform</c> attribute's
///     <c>translate</c>/<c>scale</c>/<c>rotate</c>/<c>skewX</c>/<c>skewY</c>/<c>matrix</c>
///     functions; presentation attributes <c>fill</c>, <c>fill-opacity</c>, <c>fill-rule</c>,
///     <c>stroke</c>, <c>stroke-width</c>, <c>stroke-opacity</c>, <c>stroke-linecap</c>,
///     <c>stroke-linejoin</c>, <c>stroke-miterlimit</c>, <c>stroke-dasharray</c>,
///     <c>stroke-dashoffset</c>, and <c>opacity</c>; <c>defs</c> plus <c>linearGradient</c>/
///     <c>radialGradient</c> (with <c>stop</c> children, <c>gradientUnits</c>,
///     <c>gradientTransform</c>, <c>spreadMethod</c>, and a single linear <c>href</c>/
///     <c>xlink:href</c> template-inheritance chain, cycle-checked); <c>use</c> (with
///     <c>x</c>/<c>y</c> translation); and <c>text</c> (with <c>font-family</c> best-effort
///     matching against a caller-supplied font dictionary, <c>font-size</c>, <c>fill</c>, and
///     <c>text-anchor</c>).
///     </para>
///     <para>
///     <b>Out of scope (silently ignored, per element).</b> <c>style</c> blocks and CSS
///     class/id selectors, <c>filter</c>, <c>mask</c>, <c>clipPath</c>, <c>pattern</c>,
///     <c>marker</c>, SMIL animation (<c>animate</c>/<c>animateTransform</c>/<c>animateMotion</c>/
///     <c>animateColor</c>/<c>set</c>), <c>image</c>, <c>foreignObject</c>, nested <c>svg</c>, and
///     an inline <c>style="..."</c> presentation attribute are all well-formed-but-unsupported
///     constructs: encountering one never aborts the document, it is simply skipped, and every
///     other element continues to render normally. The <c>preserveAspectRatio</c> attribute is
///     never read - see this class's viewBox-fitting remarks below for the one fitting policy this
///     codec always applies instead.
///     </para>
///     <para>
///     <b>ViewBox fitting.</b> <see cref="Load(Stream, int, int, IReadOnlyDictionary{string, TrueTypeFont}?)"/>
///     always fits the document's intrinsic user-space size into the caller-requested raster size
///     using a "meet, centered" policy equivalent to CSS <c>object-fit: contain</c> (SVG's own
///     <c>preserveAspectRatio="xMidYMid meet"</c>): the content is uniformly scaled as large as
///     possible while remaining fully visible, then centered, leaving transparent letterbox/
///     pillarbox bars on the raster's shorter axis. This is the only fitting behavior implemented.
///     </para>
///     <para>
///     <b>GetInfo fallback policy.</b> <see cref="GetInfo(Stream)"/> resolves an intrinsic size in
///     three tiers: a valid <c>viewBox</c> attribute, if present; otherwise valid <c>width</c>/
///     <c>height</c> attributes, if both are present and parse successfully; otherwise the CSS/UA
///     default replaced-element intrinsic size of 300x150. <see cref="ImageInfo.Channels"/> is
///     always <c>4</c> and <see cref="ImageInfo.HasAlpha"/> is always <see langword="true"/>,
///     since every rasterized pixel carries an alpha channel regardless of document content.
///     </para>
///     <para>
///     <b>Error-handling policy.</b> Malformed or unparseable input - <see cref="XDocument.Load(Stream)"/>
///     (used by <see cref="Load(Stream, int, int, IReadOnlyDictionary{string, TrueTypeFont}?)"/>,
///     which parses the whole document) or the bounded, root-start-tag-only <see cref="XmlReader"/>
///     read used by <see cref="GetInfo(Stream)"/> throwing <see cref="XmlException"/>, a
///     non-<c>svg</c> root element, missing required path/shape data, invalid numeric syntax
///     (including a non-finite <c>NaN</c>/<c>Infinity</c> value), a percentage value on a
///     shape/text geometry attribute (<c>x</c>, <c>y</c>, <c>width</c>, <c>height</c>, <c>rx</c>,
///     <c>ry</c>, <c>cx</c>, <c>cy</c>, <c>r</c>, <c>x1</c>/<c>y1</c>/<c>x2</c>/<c>y2</c>,
///     <c>font-size</c>, <c>stroke-width</c>, <c>stroke-miterlimit</c>, <c>stroke-dashoffset</c>,
///     <c>use</c>'s <c>x</c>/<c>y</c>, and <c>text</c>'s <c>x</c>/<c>y</c> - this codec has no
///     defined viewport-relative basis to resolve one against, unlike opacity-family attributes
///     and gradient coordinates/<c>stop</c> <c>offset</c>, which correctly treat a percentage as a
///     <c>[0, 1]</c> fraction and are unaffected), a non-positive <c>viewBox</c>
///     size, a malformed <c>transform</c> attribute, a gradient <c>href</c> cycle, or a combined
///     total of path-data commands/points-list coordinates/text characters exceeding a fixed
///     geometry-parsing work budget (independent of the total-rendered-element budget, bounding a
///     single pathological element's own content) - is caught
///     and re-thrown as <see cref="InvalidDataException"/> with a descriptive message. A dangling <c>url(#id)</c>
///     paint reference or an unrecognized color keyword is instead treated as tolerant "no paint"
///     (nothing is drawn for that fill/stroke), and a <c>text</c> element with no caller-supplied
///     font dictionary, or no entry matching its <c>font-family</c>, is silently skipped rather
///     than throwing - both documented simplifications of an otherwise strict parser. An invalid
///     <c>stroke-miterlimit</c> value (non-finite, or less than <c>1</c> - see
///     <see cref="Drawing.StrokeStyle"/>'s documented contract) is a further, separate tolerant
///     case: rather than throwing, it falls back to the inherited value, matching this class's
///     existing tolerant handling of a malformed <c>stroke-dasharray</c>. A negative
///     <c>radialGradient</c> <c>r</c>/<c>fr</c> (below <see cref="Drawing.RadialGradient"/>'s
///     documented contract of "finite and greater than or equal to zero") is likewise tolerant:
///     it falls back to the same default used for an absent attribute, matching this class's
///     existing tolerant handling of every other gradient coordinate. A composed transform that
///     overflows to a non-finite value across nested <c>transform="scale(...)"</c> groups (each
///     individual literal finite, but their cross-element product not) is likewise tolerant: the
///     affected element (and, independently, an affected gradient's own
///     <c>gradientTransform</c>/bounding-box composition) is skipped/treated as "no paint" rather
///     than reaching a <see cref="Drawing"/>-namespace constructor's own finiteness check. A
///     <c>path</c> <c>d</c> attribute whose relative-coordinate accumulation, or whose <c>S</c>/
///     <c>T</c> smooth-curve reflection, overflows an individually-finite pair of literals to a
///     non-finite value is likewise tolerant: the whole <c>path</c> element is skipped (rendered
///     as an empty path) rather than reaching <see cref="Drawing.DashSplitter"/>'s dash-interval
///     walk, which would otherwise stall indefinitely on a non-finite path length.
///     </para>
///     <para>
///     <b>Caller-supplied raster dimensions.</b> The <c>width</c>/<c>height</c>
///     parameters of both <c>Load</c> overloads are ordinary API parameters, not untrusted file
///     data: they are passed directly to <see cref="Surface"/>'s constructor and its own
///     <see cref="ArgumentOutOfRangeException"/> is allowed to propagate uncaught, rather than
///     being pre-validated or wrapped as <see cref="InvalidDataException"/>.
///     </para>
/// </remarks>
public static class SvgCodec
{
    /// <summary>
    ///     The XML namespace SVG documents use to qualify <c>href</c> attributes under their
    ///     legacy SVG 1.1 name (<c>xlink:href</c>), still common in real-world documents alongside
    ///     the unprefixed SVG 2 <c>href</c> attribute this codec also recognizes.
    /// </summary>
    private static readonly XNamespace XlinkNamespace = "http://www.w3.org/1999/xlink";

    /// <summary>
    ///     The maximum number of nested <c>use</c> references this codec follows before giving up,
    ///     guarding against a reference cycle (direct or indirect self-reference) that would
    ///     otherwise recurse indefinitely.
    /// </summary>
    private const int MaxUseDepth = 32;

    /// <summary>
    ///     The maximum <see cref="RenderElement"/> recursion depth this codec descends through
    ///     while walking the element tree - covering plain <c>g</c>/<c>symbol</c> nesting as well
    ///     as <c>use</c>-reference recursion - before giving up, guarding against a
    ///     <see cref="StackOverflowException"/> (which cannot be caught and would otherwise
    ///     terminate the process outright, bypassing this class's documented
    ///     <see cref="InvalidDataException"/>-wrapping error-handling policy) from a document with
    ///     many levels of nested container elements. Real-world documents, including deeply
    ///     grouped output from illustration tools, essentially never approach this depth.
    /// </summary>
    private const int MaxElementDepth = 100;

    /// <summary>
    ///     The maximum total number of elements this codec will render across a single
    ///     <c>Load</c> call, bounding non-cyclic exponential <c>use</c> fan-out. Neither
    ///     <see cref="MaxUseDepth"/> nor <see cref="MaxElementDepth"/> bounds total work: a group
    ///     legitimately (non-cyclically) referenced by several sibling <c>use</c> elements, itself
    ///     containing further such fan-out, re-renders its entire subtree once per reference, so
    ///     the total number of elements rendered grows exponentially with nesting depth even while
    ///     every individual reference chain stays well within both depth caps. 100,000 is far
    ///     beyond the element count of any real-world SVG this codec has been exercised against
    ///     (the most complex fixture in this repository's test suite has roughly 700 elements),
    ///     but small enough to keep worst-case rendering CPU/memory bounded to a small, practical
    ///     amount regardless of how a malicious/pathological document is structured.
    /// </summary>
    private const int MaxTotalRenderedElements = 100_000;

    /// <summary>
    ///     The maximum total number of characters <see cref="LoadRootElement"/>'s
    ///     <see cref="XDocument.Load(XmlReader, LoadOptions)"/> call will read before giving up,
    ///     bounding how large a single in-memory <see cref="XDocument"/> this codec will ever
    ///     materialize for a single <c>Load</c> call. Without this bound, an attacker-supplied
    ///     stream of unbounded size would be fully parsed into an unbounded DOM tree before any of
    ///     this class's other guards (<see cref="MaxTotalRenderedElements"/>,
    ///     <see cref="GeometryWorkBudget"/>) ever get a chance to run, since those guards only
    ///     execute during the rendering walk that follows a successful parse. 5,000,000 characters
    ///     is roughly 100 times the size of the largest real-world fixture in this repository's
    ///     test suite (<c>InkscapeFilters.svg</c>, 50,381 bytes) - far beyond any real document,
    ///     but small enough to keep worst-case parse-time CPU/memory bounded to a small, practical
    ///     amount, matching this class's other budgets' "generous but bounded" order-of-magnitude
    ///     spirit.
    /// </summary>
    /// <remarks>
    ///     This is an accepted, bounded limitation, not a full incremental/streaming parse: a
    ///     well-formed document sized just under this character cap can still fully materialize
    ///     into an in-memory DOM before <see cref="MaxTotalRenderedElements"/> or
    ///     <see cref="GeometryWorkBudget"/> ever get a chance to reject a single pathological
    ///     element's content. A full streaming-parser rewrite of <c>Load</c> (replacing
    ///     <see cref="XDocument"/>/<see cref="XElement"/> entirely) would close this remaining gap
    ///     but is out of scope for this bound, which targets the specific, previously-completely-
    ///     unbounded "raw document size" dimension.
    /// </remarks>
    private const int MaxDocumentCharacters = 5_000_000;

    /// <summary>
    ///     Tracks the cumulative "geometry parsing work" - path <c>d</c> data commands,
    ///     points-list coordinate pairs, and text characters - charged across a single
    ///     <c>Load</c> call, throwing once a fixed combined budget is exceeded. This bounds the
    ///     content of a single element, a dimension <see cref="MaxTotalRenderedElements"/> does
    ///     not cover: that budget only counts how many elements are visited, so one
    ///     <c>path</c>/<c>polyline</c>/<c>polygon</c>/<c>text</c> element with an extremely large
    ///     <c>d</c>/<c>points</c>/text value would otherwise count as only a single element while
    ///     allocating or processing an unbounded amount of geometry or text.
    /// </summary>
    /// <remarks>
    ///     A mutable reference type, rather than a <c>ref int</c> counter (the convention used
    ///     for <c>totalElements</c> below and for <see cref="Fonts.GlyfLocaReader"/>'s
    ///     total-point/component counters), because <see cref="PathDataParser"/> is a long-lived
    ///     stateful instance that cannot store a <c>ref</c> parameter as a field; sharing one
    ///     instance by ordinary object reference achieves the same "one counter, many call sites"
    ///     effect without that constraint.
    /// </remarks>
    private sealed class GeometryWorkBudget
    {
        /// <summary>
        ///     The maximum combined total of path-data commands, points-list coordinate pairs,
        ///     and text characters this codec will parse across a single <c>Load</c> call. Mirrors
        ///     <see cref="Fonts.GlyfLocaReader"/>'s own <c>MaxTotalPoints</c> budget (also
        ///     <c>200_000</c>) - the same order of magnitude precedent for bounding a single
        ///     pathological element's parsing cost - and is far beyond the combined
        ///     command/coordinate/character count of any real-world document this codec has been
        ///     exercised against, while keeping worst-case CPU/memory bounded to a small,
        ///     practical amount.
        /// </summary>
        private const int MaxTotalGeometryWork = 200_000;

        /// <summary>The running total of geometry-parsing work charged so far.</summary>
        private int _total;

        /// <summary>
        ///     Charges <paramref name="amount"/> units of work against the running total,
        ///     throwing once the combined budget is exceeded - called incrementally, before or as
        ///     each unit of work is actually spent, so a single pathological element throws
        ///     partway through parsing rather than only after its entire (unbounded) content has
        ///     already been scanned.
        /// </summary>
        /// <param name="amount">The number of commands/coordinates/characters just accounted for.</param>
        /// <exception cref="InvalidDataException">
        ///     Thrown once the cumulative total exceeds <see cref="MaxTotalGeometryWork"/>.
        /// </exception>
        public void Charge(int amount)
        {
            // Check before adding (rather than adding then checking) so that a single amount
            // large enough to make the addition itself overflow int cannot bypass the budget -
            // MaxTotalGeometryWork - _total is always non-negative and small here, since the
            // invariant _total <= MaxTotalGeometryWork holds after every successful call, so the
            // subtraction itself cannot overflow. This is defense-in-depth: given today's fixed
            // constants (amount is at most MaxDocumentCharacters, far below int.MaxValue / 2),
            // _total could never legitimately climb anywhere near int.MaxValue via repeated small
            // additions before the very next charge past MaxTotalGeometryWork already throws -
            // but the check-before-add ordering is strictly more correct regardless, and remains
            // safe if either constant is ever raised without re-auditing this method.
            if (amount > MaxTotalGeometryWork - _total)
            {
                throw new InvalidDataException(
                    "SVG document resolves to too much total path/point-list/text geometry-parsing work.");
            }

            _total += amount;
        }
    }

    // ================================================================================================
    // Public API
    // ================================================================================================

    /// <summary>
    ///     Rasterizes an SVG document read from an open, readable stream onto a new
    ///     <see cref="Surface"/> of the requested size.
    /// </summary>
    /// <param name="stream">
    ///     The stream to read the SVG document from. Reading begins at the stream's current
    ///     position and consumes the remainder of the stream.
    /// </param>
    /// <param name="width">The width, in pixels, of the returned surface.</param>
    /// <param name="height">The height, in pixels, of the returned surface.</param>
    /// <param name="fonts">
    ///     An optional dictionary mapping font-family names to loaded <see cref="TrueTypeFont"/>
    ///     instances, used to render <c>text</c> elements. A <see langword="null"/> value (the
    ///     default) or a dictionary with no entry matching a given <c>text</c> element's
    ///     <c>font-family</c> causes that element to be silently skipped rather than throwing -
    ///     see this class's remarks.
    /// </param>
    /// <returns>
    ///     A new <see cref="Surface"/> of the requested size containing the rasterized document,
    ///     fitted per this class's viewBox-fitting policy.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="width"/> or <paramref name="height"/> is not a valid
    ///     <see cref="Surface"/> dimension - see this class's remarks on caller-supplied raster
    ///     dimensions.
    /// </exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the stream does not contain a well-formed, supported SVG document - see
    ///     this class's error-handling policy remarks.
    /// </exception>
    /// <example>
    ///     <code>
    ///     const string svg = "&lt;svg viewBox='0 0 100 100'&gt;&lt;circle cx='50' cy='50' r='40' fill='red'/&gt;&lt;/svg&gt;";
    ///     using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(svg));
    ///     var surface = SvgCodec.Load(stream, 200, 200);
    ///     </code>
    /// </example>
    public static Surface Load(
        Stream stream,
        int width,
        int height,
        IReadOnlyDictionary<string, TrueTypeFont>? fonts = null)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var surface = new Surface(width, height);
        var root = LoadRootElement(stream);

        try
        {
            var (origin, size) = ResolveViewBoxOrSize(root);
            var fitTransform = ComputeFitTransform(origin, size, surface.Width, surface.Height);

            // A tiny-but-positive, finite resolved viewBox/width/height (for example a subnormal
            // float) passes ResolveViewBoxOrSize's own "must be positive" check but can still
            // overflow ComputeFitTransform's own division to a non-finite scale. Left without a
            // check here, the resulting non-finite fitTransform would silently fail IsFiniteTransform's
            // per-element check for every single element in the document, rendering a blank,
            // fully-transparent surface with no exception at all - a worse "quiet" failure than
            // the sibling non-positive-viewBox case already throws for. Treat a degenerate fit
            // scale as the same class of malformed-sizing-data error instead.
            if (!IsFiniteTransform(fitTransform))
            {
                throw new InvalidDataException(
                    "The SVG document's resolved viewBox/width/height produces a non-finite fit transform.");
            }

            var idIndex = BuildIdIndex(root);
            var context = new RenderContext(surface, idIndex, fonts);
            RenderDocument(root, fitTransform, context);
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException("The SVG document contains invalid numeric data.", ex);
        }

        return surface;
    }

    /// <summary>
    ///     Rasterizes an SVG document loaded from a file path onto a new <see cref="Surface"/> of
    ///     the requested size. See <see cref="Load(Stream, int, int, IReadOnlyDictionary{string, TrueTypeFont}?)"/>
    ///     for the full contract.
    /// </summary>
    /// <param name="path">The path of the SVG file to load. Must not be null, empty, or whitespace.</param>
    /// <param name="width">The width, in pixels, of the returned surface.</param>
    /// <param name="height">The height, in pixels, of the returned surface.</param>
    /// <param name="fonts">
    ///     An optional dictionary mapping font-family names to loaded <see cref="TrueTypeFont"/>
    ///     instances. See the stream overload's remarks.
    /// </param>
    /// <returns>A new <see cref="Surface"/> containing the rasterized document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="path"/> is empty or consists only of whitespace.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="width"/> or <paramref name="height"/> is not a valid
    ///     <see cref="Surface"/> dimension.
    /// </exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the file does not contain a well-formed, supported SVG document.
    /// </exception>
    public static Surface Load(
        string path,
        int width,
        int height,
        IReadOnlyDictionary<string, TrueTypeFont>? fonts = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path must not be empty or whitespace.", nameof(path));
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        return Load(stream, width, height, fonts);
    }

    /// <summary>
    ///     Reads only an SVG document's root <c>svg</c> element (<c>viewBox</c>/<c>width</c>/
    ///     <c>height</c> attributes) and reports its resolved intrinsic size, without rendering
    ///     any shape content.
    /// </summary>
    /// <param name="stream">
    ///     The stream to read the SVG document from. Reading begins at the stream's current
    ///     position.
    /// </param>
    /// <returns>
    ///     The document's resolved intrinsic size (see this class's GetInfo fallback policy
    ///     remarks), rounded to the nearest integer pixel (minimum <c>1</c> on each axis).
    ///     <see cref="ImageInfo.Channels"/> is always <c>4</c> and <see cref="ImageInfo.HasAlpha"/>
    ///     is always <see langword="true"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the stream does not contain a well-formed SVG document, or its sizing
    ///     attributes are malformed - see this class's error-handling policy remarks.
    /// </exception>
    public static ImageInfo GetInfo(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var root = LoadRootElementAttributesOnly(stream);

        try
        {
            var (_, size) = ResolveViewBoxOrSize(root);

            // Clamp each resolved dimension to a valid Int32 range in double precision *before*
            // casting to int: int.MaxValue (2147483647) is not exactly representable as a float
            // (it rounds up to 2147483648f), so clamping in float first would leave an
            // out-of-range value that is undefined behavior to cast to int. int.MaxValue *is*
            // exactly representable as a double, so clamp-then-cast here is well-defined for
            // every possible resolved size, including a viewBox/width/height large enough to
            // otherwise overflow Int32 on cast.
            var width = (int)Math.Clamp((double)MathF.Round(size.X), 1d, int.MaxValue);
            var height = (int)Math.Clamp((double)MathF.Round(size.Y), 1d, int.MaxValue);
            return new ImageInfo(width, height, 4, true);
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException("The SVG document contains invalid numeric data.", ex);
        }
    }

    /// <summary>
    ///     Reads only an SVG file's root <c>svg</c> element and reports its resolved intrinsic
    ///     size. See <see cref="GetInfo(Stream)"/> for the full contract.
    /// </summary>
    /// <param name="path">The path of the SVG file to inspect. Must not be null, empty, or whitespace.</param>
    /// <returns>The document's resolved intrinsic size.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="path"/> is empty or consists only of whitespace.
    /// </exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the file does not contain a well-formed SVG document, or its sizing
    ///     attributes are malformed.
    /// </exception>
    public static ImageInfo GetInfo(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path must not be empty or whitespace.", nameof(path));
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        return GetInfo(stream);
    }

    // ================================================================================================
    // XML document parsing and id-index construction
    // ================================================================================================

    /// <summary>
    ///     Parses <paramref name="stream"/> as XML and returns its validated <c>svg</c> root
    ///     element, including its full descendant tree. Used by <c>Load</c> only, which needs the
    ///     complete document to render shape content - see <see cref="LoadRootElementAttributesOnly"/>
    ///     for the bounded, header-only parse <c>GetInfo</c> uses instead.
    /// </summary>
    /// <param name="stream">The stream to parse.</param>
    /// <returns>The document's root <c>svg</c> element.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="stream"/> is not well-formed XML, or its root element is
    ///     not named <c>svg</c>.
    /// </exception>
    private static XElement LoadRootElement(Stream stream)
    {
        XDocument document;
        try
        {
            // Bound the reader's total character count via MaxCharactersInDocument before
            // XDocument.Load ever begins materializing the DOM tree - see MaxDocumentCharacters'
            // own remarks for why this is a raw-size bound, not a full streaming parse
            var settings = new XmlReaderSettings { MaxCharactersInDocument = MaxDocumentCharacters };
            using var reader = XmlReader.Create(stream, settings);
            document = XDocument.Load(reader, LoadOptions.None);
        }
        catch (XmlException ex)
        {
            throw new InvalidDataException("The stream does not contain well-formed XML.", ex);
        }

        var root = document.Root;
        if (root == null || root.Name.LocalName != "svg")
        {
            throw new InvalidDataException("The document's root element is not an <svg> element.");
        }

        return root;
    }

    /// <summary>
    ///     Parses only <paramref name="stream"/>'s root <c>svg</c> start-tag and its own
    ///     attributes using a forward-only <see cref="XmlReader"/>, never reading past the root
    ///     element's attributes into the document body. Used by <c>GetInfo</c> only, so that
    ///     resolving intrinsic size costs time and memory proportional to the root start-tag alone
    ///     - never the full document body, regardless of how large or deeply nested it is. The
    ///     reader is still bounded by the same <see cref="MaxDocumentCharacters"/> character cap
    ///     <c>LoadRootElement</c> applies, since even a parse that never reads past the root
    ///     start-tag can still be forced to materialize an unbounded amount of data via a single
    ///     oversized root-tag attribute value.
    /// </summary>
    /// <param name="stream">The stream to parse.</param>
    /// <returns>
    ///     A new, detached, childless <see cref="XElement"/> named <c>svg</c> carrying only the
    ///     root element's own attributes. Namespace-declaration attributes (<c>xmlns</c> and
    ///     <c>xmlns:*</c>) are omitted, since none of this class's attribute lookups need them.
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="stream"/> is not well-formed XML up to and including the
    ///     root start-tag, its root element is not named <c>svg</c>, or the root start-tag alone
    ///     (including an oversized attribute value) exceeds <see cref="MaxDocumentCharacters"/>.
    /// </exception>
    private static XElement LoadRootElementAttributesOnly(Stream stream)
    {
        try
        {
            // A plain XmlReader, bounded only by MaxCharactersInDocument, is forward-only and
            // never buffers more than the current node, so advancing only as far as the root
            // start-tag's attributes - and never calling Read() again - guarantees the rest of
            // the document body is never parsed or walked, regardless of its size or
            // well-formedness. The character cap is still required despite that: the reader
            // still advances character-by-character through a single root-tag attribute value
            // even though it never reads any further afterward, so an oversized attribute value
            // alone (never mind the document body) could otherwise force this method to
            // materialize an unbounded amount of data.
            var settings = new XmlReaderSettings { MaxCharactersInDocument = MaxDocumentCharacters };
            using var reader = XmlReader.Create(stream, settings);
            if (reader.MoveToContent() != XmlNodeType.Element || reader.LocalName != "svg")
            {
                throw new InvalidDataException("The document's root element is not an <svg> element.");
            }

            var root = new XElement("svg");
            if (reader.MoveToFirstAttribute())
            {
                do
                {
                    if (reader.LocalName == "xmlns" || reader.Prefix == "xmlns")
                    {
                        continue;
                    }

                    root.SetAttributeValue(reader.LocalName, reader.Value);
                } while (reader.MoveToNextAttribute());
            }

            return root;
        }
        catch (XmlException ex)
        {
            throw new InvalidDataException("The stream does not contain well-formed XML.", ex);
        }
    }

    /// <summary>
    ///     Builds a lookup from every <c>id</c> attribute value appearing anywhere in the document
    ///     (including <paramref name="root"/> itself) to its element, so that <c>use</c>/<c>href</c>/
    ///     gradient-template references resolve regardless of where the referenced element appears
    ///     relative to the reference (document order is irrelevant to id resolution in SVG).
    /// </summary>
    /// <param name="root">The document's root element.</param>
    /// <returns>
    ///     The id index. When two elements share the same <c>id</c> (invalid, but tolerated), the
    ///     first one encountered in document order wins.
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown once this walk visits more than <see cref="MaxTotalRenderedElements"/> elements.
    ///     This walk runs before rendering and covers every element in the document - including
    ///     elements <see cref="RenderElement"/> would never itself visit, such as unreferenced
    ///     <c>defs</c> content and a gradient's own <c>stop</c> children - so charging it against
    ///     the same budget also transitively bounds <see cref="ParseStops"/>'s later, otherwise-
    ///     unbounded enumeration of a single gradient's <c>stop</c> children.
    /// </exception>
    private static Dictionary<string, XElement> BuildIdIndex(XElement root)
    {
        var index = new Dictionary<string, XElement>(StringComparer.Ordinal);
        var totalElements = 0;
        foreach (var element in root.DescendantsAndSelf())
        {
            // Check before incrementing (rather than incrementing then checking) - see
            // GeometryWorkBudget.Charge's identical rationale. Each visit increments by exactly
            // 1, so this specific site can never itself overflow int in practice, but the
            // check-before-add ordering is the strictly more correct pattern regardless, applied
            // here for systemic consistency across every accumulation site in this class.
            if (totalElements >= MaxTotalRenderedElements)
            {
                throw new InvalidDataException($"The document contains more than {MaxTotalRenderedElements} elements.");
            }

            totalElements++;

            var id = (string?)element.Attribute("id");
            if (id != null)
            {
                index.TryAdd(id, element);
            }
        }

        return index;
    }

    /// <summary>
    ///     Looks up an element's <c>href</c> attribute, accepting both the unprefixed SVG 2 form
    ///     and the legacy SVG 1.1 <c>xlink:href</c> form (preferring the unprefixed form when both
    ///     are present).
    /// </summary>
    /// <param name="element">The element to inspect.</param>
    /// <returns>The href attribute's raw value, or <see langword="null"/> if neither form is present.</returns>
    private static string? GetHrefAttribute(XElement element) =>
        (string?)element.Attribute("href") ?? (string?)element.Attribute(XlinkNamespace + "href");

    // ================================================================================================
    // Root sizing and viewBox fitting
    // ================================================================================================

    /// <summary>
    ///     Resolves the root <c>svg</c> element's intrinsic origin/size per this class's three-tier
    ///     GetInfo fallback policy (<c>viewBox</c>, then <c>width</c>/<c>height</c>, then the
    ///     300x150 CSS/UA default).
    /// </summary>
    /// <param name="root">The document's root element.</param>
    /// <returns>The resolved origin (viewBox top-left, or the zero vector) and size.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when a <c>viewBox</c> attribute is present but malformed (not exactly four
    ///     numbers, or a non-positive width/height).
    /// </exception>
    private static (Vector2 Origin, Vector2 Size) ResolveViewBoxOrSize(XElement root)
    {
        var viewBox = ParseViewBox((string?)root.Attribute("viewBox"));
        if (viewBox.HasValue)
        {
            return viewBox.Value;
        }

        var width = ParseLength((string?)root.Attribute("width"));
        var height = ParseLength((string?)root.Attribute("height"));
        if (width is > 0f && height is > 0f)
        {
            return (Vector2.Zero, new Vector2(width.Value, height.Value));
        }

        // Neither a viewBox nor a valid width/height pair is present - fall back to the CSS/UA
        // default replaced-element intrinsic size for a fully sizeless SVG document
        return (Vector2.Zero, new Vector2(300f, 150f));
    }

    /// <summary>
    ///     Parses a <c>viewBox</c> attribute's raw value ("<c>min-x min-y width height</c>").
    /// </summary>
    /// <param name="raw">The attribute's raw value, or <see langword="null"/> if absent.</param>
    /// <returns>
    ///     The parsed origin/size, or <see langword="null"/> when <paramref name="raw"/> is
    ///     <see langword="null"/> or blank (meaning the attribute is simply absent, not an error).
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="raw"/> is present but does not contain exactly four
    ///     numbers, or its width/height are not both greater than zero.
    /// </exception>
    private static (Vector2 Origin, Vector2 Size)? ParseViewBox(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var numbers = ParseNumberList(raw);
        if (numbers.Count != 4)
        {
            throw new InvalidDataException("Malformed viewBox attribute: expected exactly four numbers.");
        }

        var width = numbers[2];
        var height = numbers[3];
        if (width <= 0f || height <= 0f)
        {
            throw new InvalidDataException("Malformed viewBox attribute: width and height must be greater than zero.");
        }

        return (new Vector2(numbers[0], numbers[1]), new Vector2(width, height));
    }

    /// <summary>
    ///     Leniently parses a CSS-style length attribute (<c>width</c>/<c>height</c>), stripping a
    ///     recognized unit suffix and treating a percentage or otherwise-unparseable value
    ///     (including a non-finite result such as <c>NaN</c>/<c>Infinity</c>) as "absent" rather
    ///     than an error - see this class's GetInfo fallback policy remarks.
    /// </summary>
    /// <param name="raw">The attribute's raw value, or <see langword="null"/> if absent.</param>
    /// <returns>
    ///     The parsed value, or <see langword="null"/> if <paramref name="raw"/> is absent, blank,
    ///     a percentage, not a recognizable number, or a non-finite number (<c>NaN</c>/<c>Infinity</c>).
    /// </returns>
    private static float? ParseLength(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();
        if (trimmed.EndsWith('%'))
        {
            // A percentage has no intrinsic-size basis to resolve against at this point - treat
            // as though the attribute were absent, falling through to the next fallback tier
            return null;
        }

        trimmed = StripUnitSuffix(trimmed);
        if (!float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
            !float.IsFinite(value))
        {
            // A non-finite result (NaN/Infinity) is syntactically a valid float but never a
            // meaningful length - treat it the same as an unparseable value: "absent", falling
            // through to the next GetInfo fallback tier, matching this method's existing
            // tolerant-parsing contract rather than introducing a new throw site
            return null;
        }

        return value;
    }

    /// <summary>
    ///     The CSS length unit suffixes this codec recognizes and strips before parsing a bare
    ///     number - unit conversion itself is out of scope (every value is treated as a bare
    ///     user-space number once its suffix is removed), a documented simplification.
    /// </summary>
    private static readonly string[] LengthUnitSuffixes = ["px", "pt", "pc", "in", "cm", "mm", "em", "ex"];

    /// <summary>
    ///     Strips a trailing unit suffix from <paramref name="value"/>, if one of
    ///     <see cref="LengthUnitSuffixes"/> matches.
    /// </summary>
    /// <param name="value">The trimmed attribute value to inspect.</param>
    /// <returns><paramref name="value"/> with any recognized trailing unit suffix removed.</returns>
    private static string StripUnitSuffix(string value)
    {
        var suffix = LengthUnitSuffixes.FirstOrDefault(candidate => value.EndsWith(candidate, StringComparison.OrdinalIgnoreCase));
        return suffix == null ? value : value[..^suffix.Length];
    }

    /// <summary>
    ///     Computes the "meet, centered" (CSS <c>object-fit: contain</c> equivalent) transform that
    ///     fits a document's intrinsic viewBox origin/size into a raster of the requested pixel
    ///     dimensions - see this class's viewBox-fitting policy remarks.
    /// </summary>
    /// <param name="origin">The intrinsic viewBox origin.</param>
    /// <param name="size">The intrinsic viewBox size.</param>
    /// <param name="rasterWidth">The requested raster width, in pixels.</param>
    /// <param name="rasterHeight">The requested raster height, in pixels.</param>
    /// <returns>
    ///     A transform mapping viewBox user-space coordinates directly into raster pixel-space
    ///     coordinates. Can be non-finite when <paramref name="size"/> is extremely small but
    ///     still positive (for example a subnormal float) - dividing the requested raster
    ///     dimensions by such a value overflows the resulting scale to <c>Infinity</c>. This
    ///     method does not itself validate its result; <c>Load</c> is responsible for checking
    ///     the returned transform with <see cref="IsFiniteTransform"/> immediately after calling
    ///     this method, before using it to render anything.
    /// </returns>
    private static Matrix3x2 ComputeFitTransform(Vector2 origin, Vector2 size, int rasterWidth, int rasterHeight)
    {
        var scale = MathF.Min(rasterWidth / size.X, rasterHeight / size.Y);
        var scaledWidth = size.X * scale;
        var scaledHeight = size.Y * scale;
        var offsetX = (rasterWidth - scaledWidth) / 2f;
        var offsetY = (rasterHeight - scaledHeight) / 2f;

        return Matrix3x2.CreateTranslation(-origin.X, -origin.Y)
            * Matrix3x2.CreateScale(scale, scale)
            * Matrix3x2.CreateTranslation(offsetX, offsetY);
    }

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
    /// <param name="TextAnchor">The <c>text-anchor</c> value.</param>
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
        TextAnchor TextAnchor)
    {
        /// <summary>
        ///     The default render state every document starts with, matching the SVG/CSS initial
        ///     values for every cascaded presentation property this codec supports.
        /// </summary>
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
            TextAnchor: TextAnchor.Start);
    }

    /// <summary>
    ///     The fixed, per-document context (pixel target, id index, and optional font dictionary)
    ///     threaded through the recursive tree walk, kept as its own type so every walk/render
    ///     method needs only one extra parameter rather than three.
    /// </summary>
    /// <param name="Surface">The pixel target every shape is rendered onto.</param>
    /// <param name="IdIndex">The whole-document id-to-element index built once up front.</param>
    /// <param name="Fonts">The caller-supplied font dictionary, or <see langword="null"/> if none was supplied.</param>
    private sealed record RenderContext(
        Surface Surface,
        Dictionary<string, XElement> IdIndex,
        IReadOnlyDictionary<string, TrueTypeFont>? Fonts)
    {
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
        public Dictionary<XElement, List<GradientStop>> GradientStopCache { get; } = new();
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
        "defs", "clipPath", "mask", "pattern", "marker", "linearGradient", "radialGradient"
    };

    /// <summary>
    ///     Well-formed-but-out-of-scope elements that are silently skipped (not recursed into),
    ///     without aborting rendering of the rest of the document - see this class's remarks.
    /// </summary>
    private static readonly HashSet<string> SkippedElements = new(StringComparer.Ordinal)
    {
        "style", "filter", "animate", "animateTransform", "animateMotion", "animateColor", "set",
        "image", "foreignObject", "svg", "metadata", "title", "desc", "script"
    };

    /// <summary>
    ///     Renders every top-level child of the root <c>svg</c> element, seeded with the initial
    ///     render state and the root viewBox-fit transform.
    /// </summary>
    /// <param name="root">The document's root element.</param>
    /// <param name="fitTransform">The viewBox-fit transform computed for this render.</param>
    /// <param name="context">The fixed per-document render context.</param>
    private static void RenderDocument(XElement root, Matrix3x2 fitTransform, RenderContext context)
    {
        var rootState = ApplyPresentationAttributes(RenderState.Initial, root);
        var totalElements = 0;
        var workBudget = new GeometryWorkBudget();
        foreach (var child in root.Elements())
        {
            RenderElement(child, rootState, fitTransform, context, useDepth: 0, elementDepth: 0, ref totalElements, workBudget);
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
    /// <param name="useDepth">
    ///     The current <c>use</c>-reference nesting depth, propagated so <see cref="RenderUse"/>
    ///     can enforce <see cref="MaxUseDepth"/>.
    /// </param>
    /// <param name="elementDepth">
    ///     The current recursion depth of this call within the element tree walk, propagated so
    ///     this method can enforce <see cref="MaxElementDepth"/> before descending further,
    ///     regardless of whether the recursion arises from plain <c>g</c>/<c>symbol</c> nesting or
    ///     from a <c>use</c> reference.
    /// </param>
    /// <param name="totalElements">
    ///     The running count of elements rendered/visited so far across the whole document walk,
    ///     charged before this element is processed further so this method can enforce
    ///     <see cref="MaxTotalRenderedElements"/> - a bound that catches non-cyclic exponential
    ///     <c>use</c> fan-out neither <see cref="MaxUseDepth"/> nor <see cref="MaxElementDepth"/>
    ///     can, since a legitimately (non-cyclically) shared subtree stays within both depth caps
    ///     no matter how many times sibling <c>use</c> elements reference it.
    /// </param>
    /// <param name="workBudget">
    ///     The shared geometry-parsing work budget (see <see cref="GeometryWorkBudget"/>), threaded
    ///     down to <c>path</c>/<c>polyline</c>/<c>polygon</c>/<c>text</c> handling so a single
    ///     pathologically large element's own content is also bounded, independent of
    ///     <paramref name="totalElements"/>.
    /// </param>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="element"/> or a descendant contains malformed presentation
    ///     data, a <c>use</c> reference cycle/excessive nesting is detected, the element tree
    ///     nests deeper than <see cref="MaxElementDepth"/>, the document resolves to more than
    ///     <see cref="MaxTotalRenderedElements"/> total rendered elements, or the combined total
    ///     of path-data commands, points-list coordinates, and text characters parsed exceeds
    ///     <see cref="GeometryWorkBudget"/>'s fixed budget.
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
        int useDepth,
        int elementDepth,
        ref int totalElements,
        GeometryWorkBudget workBudget)
    {
        // Fail fast before recursing any further - an unbounded element tree walk would otherwise
        // eventually drive the call stack into an uncatchable StackOverflowException
        if (elementDepth >= MaxElementDepth)
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

        var state = ApplyPresentationAttributes(parentState, element);
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
                // "symbol" is spec-defined to render only when referenced via <use>, never when
                // encountered directly - this codec has no notion of a "use-only" render context,
                // so, as a documented simplification, it is instead treated identically to a plain
                // group in both situations (renders in place if encountered directly, and also
                // renders when referenced via <use>)
                foreach (var child in element.Elements())
                {
                    RenderElement(child, state, transform, context, useDepth, elementDepth + 1, ref totalElements, workBudget);
                }

                break;

            case "rect":
                RenderShape(BuildRectPath(element), state, transform, context);
                break;

            case "circle":
                RenderShape(BuildEllipsePath(element, isCircle: true), state, transform, context);
                break;

            case "ellipse":
                RenderShape(BuildEllipsePath(element, isCircle: false), state, transform, context);
                break;

            case "line":
                RenderShape(BuildLinePath(element), state, transform, context);
                break;

            case "polyline":
                RenderShape(BuildPolyPath(element, closed: false, workBudget), state, transform, context);
                break;

            case "polygon":
                RenderShape(BuildPolyPath(element, closed: true, workBudget), state, transform, context);
                break;

            case "path":
                RenderShape(BuildPathDataPath(element, workBudget), state, transform, context);
                break;

            case "use":
                RenderUse(element, state, transform, context, useDepth, elementDepth, ref totalElements, workBudget);
                break;

            case "text":
                RenderText(element, state, transform, context, workBudget);
                break;

            default:
                // Any other element name (including future/unknown elements) is tolerated by
                // silently skipping it, consistent with this codec's out-of-scope-construct policy
                break;
        }
    }

    /// <summary>
    ///     Applies every presentation attribute directly set on <paramref name="element"/> on top
    ///     of <paramref name="parent"/>'s cascaded state, leaving any attribute not present on
    ///     <paramref name="element"/> inherited unchanged from <paramref name="parent"/>.
    /// </summary>
    /// <param name="parent">The inherited render state from the parent element.</param>
    /// <param name="element">The element whose own presentation attributes are applied.</param>
    /// <returns>The new, cascaded render state for <paramref name="element"/>.</returns>
    private static RenderState ApplyPresentationAttributes(RenderState parent, XElement element)
    {
        var dashArrayAttr = (string?)element.Attribute("stroke-dasharray");
        var strokeDashArray = dashArrayAttr switch
        {
            null => parent.StrokeDashArray,
            _ when string.Equals(dashArrayAttr.Trim(), "none", StringComparison.OrdinalIgnoreCase) => null,
            _ => ParseDashArray(dashArrayAttr)
        };

        return parent with
        {
            Fill = (string?)element.Attribute("fill") ?? parent.Fill,
            Stroke = (string?)element.Attribute("stroke") ?? parent.Stroke,
            FillOpacity = ParseOptionalOpacity(element, "fill-opacity") ?? parent.FillOpacity,
            StrokeOpacity = ParseOptionalOpacity(element, "stroke-opacity") ?? parent.StrokeOpacity,
            Opacity = parent.Opacity * (ParseOptionalOpacity(element, "opacity") ?? 1f),
            FillRule = ParseFillRule((string?)element.Attribute("fill-rule")) ?? parent.FillRule,
            StrokeWidth = GetOptionalFloat(element, "stroke-width") ?? parent.StrokeWidth,
            StrokeLineCap = ParseLineCap((string?)element.Attribute("stroke-linecap")) ?? parent.StrokeLineCap,
            StrokeLineJoin = ParseLineJoin((string?)element.Attribute("stroke-linejoin")) ?? parent.StrokeLineJoin,
            StrokeMiterLimit = ParseValidMiterLimit(element) ?? parent.StrokeMiterLimit,
            StrokeDashArray = strokeDashArray,
            StrokeDashOffset = GetOptionalFloat(element, "stroke-dashoffset") ?? parent.StrokeDashOffset,
            FontFamily = (string?)element.Attribute("font-family") ?? parent.FontFamily,
            FontSize = GetOptionalFloat(element, "font-size") ?? parent.FontSize,
            TextAnchor = ParseTextAnchor((string?)element.Attribute("text-anchor")) ?? parent.TextAnchor
        };
    }

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
    ///     Parses an opacity-like attribute (<c>fill-opacity</c>/<c>stroke-opacity</c>/<c>opacity</c>/
    ///     <c>stop-opacity</c>), accepting either a bare <c>[0, 1]</c> number or a percentage, and
    ///     clamping the result to <c>[0, 1]</c>.
    /// </summary>
    /// <param name="element">The element to inspect.</param>
    /// <param name="name">The attribute name to read.</param>
    /// <returns>The clamped opacity value, or <see langword="null"/> if the attribute is absent.</returns>
    /// <exception cref="FormatException">Thrown when the attribute is present but not a valid number.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the attribute is present but parses to a non-finite value - see
    ///     <see cref="ParseCoordinate"/>.
    /// </exception>
    private static float? ParseOptionalOpacity(XElement element, string name)
    {
        var raw = (string?)element.Attribute(name);
        return raw == null ? null : ParseOpacityValue(raw);
    }

    /// <summary>Parses and clamps a raw opacity string to <c>[0, 1]</c>. See <see cref="ParseOptionalOpacity"/>.</summary>
    /// <param name="raw">The raw opacity string.</param>
    /// <returns>The clamped opacity value.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="raw"/> is not a valid number.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="raw"/> parses to a non-finite value - see
    ///     <see cref="ParseCoordinate"/>.
    /// </exception>
    private static float ParseOpacityValue(string raw) => Math.Clamp(ParseCoordinate(raw, 1f), 0f, 1f);

    /// <summary>
    ///     Parses a <c>stroke-dasharray</c> attribute's number list, tolerantly treating a
    ///     negative-containing or all-zero list as "no dashing" (solid stroke) rather than an
    ///     error, matching how an unparseable value is treated elsewhere in this codec.
    /// </summary>
    /// <param name="raw">The attribute's raw, non-<c>"none"</c> value.</param>
    /// <returns>The parsed dash array, or <see langword="null"/> for an effectively-solid stroke.</returns>
    private static IReadOnlyList<float>? ParseDashArray(string raw)
    {
        var numbers = ParseNumberList(raw);
        return numbers.Count == 0 || numbers.Exists(v => v < 0f) || numbers.TrueForAll(v => v == 0f)
            ? null
            : numbers;
    }

    /// <summary>
    ///     Parses and validates the <c>stroke-miterlimit</c> attribute against
    ///     <see cref="Drawing.StrokeStyle"/>'s documented contract (finite and at least <c>1</c>),
    ///     so an invalid value falls back to the inherited value here rather than escaping later
    ///     as an undocumented <see cref="ArgumentOutOfRangeException"/> from
    ///     <see cref="Drawing.StrokeStyle"/>'s constructor - mirroring this class's existing
    ///     tolerant handling of a malformed <c>stroke-dasharray</c> (see
    ///     <see cref="ParseDashArray"/>), rather than aborting the whole document over one
    ///     presentation-attribute value.
    /// </summary>
    /// <remarks>
    ///     This deliberately does not delegate to <see cref="GetOptionalFloat"/>/
    ///     <see cref="ParseGeometryCoordinate"/>: <see cref="ParseCoordinate"/> already throws
    ///     <see cref="InvalidDataException"/> for a non-finite parsed value (added for
    ///     coordinates/lengths generally), which would preempt this method's own finiteness
    ///     check below and make it dead code - a non-finite <c>stroke-miterlimit</c> would then
    ///     abort the whole document instead of falling back to the inherited value, contradicting
    ///     this method's documented contract above. Reading and parsing the raw attribute directly
    ///     keeps the non-finite-falls-back path reachable for this attribute specifically, without
    ///     changing that shared, correct-for-every-other-attribute behavior. The percentage-suffix
    ///     rejection below is intentionally duplicated (not delegated) for the same reason - see
    ///     <see cref="ParseGeometryCoordinate"/> for the identical check applied to other
    ///     shape/text geometry attributes.
    /// </remarks>
    /// <param name="element">The element to inspect.</param>
    /// <returns>The valid parsed value, or <see langword="null"/> if absent or out of contract.</returns>
    /// <exception cref="FormatException">
    ///     Thrown when the attribute is present but not a valid number - propagates uncaught to
    ///     this class's top-level <c>Load</c>/<c>GetInfo</c> boundary, which rewraps it as
    ///     <see cref="InvalidDataException"/>.
    /// </exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the attribute carries a percentage suffix - this codec has no defined
    ///     viewport-relative basis for it, matching <see cref="ParseGeometryCoordinate"/>'s
    ///     rejection of a percentage on every other shape/text geometry attribute. Not thrown for
    ///     a non-finite parsed value (<c>NaN</c>, <c>Infinity</c>, <c>-Infinity</c>) - unlike
    ///     <see cref="ParseCoordinate"/>, such a value falls back to <see langword="null"/> here
    ///     instead, per this method's documented contract above.
    /// </exception>
    private static float? ParseValidMiterLimit(XElement element)
    {
        var raw = (string?)element.Attribute("stroke-miterlimit");
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

    // ================================================================================================
    // Transform attribute parsing
    // ================================================================================================

    /// <summary>
    ///     Parses an element's <c>transform</c> attribute (a space/comma-separated list of
    ///     <c>translate</c>/<c>scale</c>/<c>rotate</c>/<c>skewX</c>/<c>skewY</c>/<c>matrix</c>
    ///     function calls) into a single combined matrix, folding the functions left-to-right in
    ///     the order they appear (each function's effect is applied before those to its right).
    /// </summary>
    /// <param name="element">The element to inspect.</param>
    /// <returns>
    ///     The combined transform, or <see cref="Matrix3x2.Identity"/> if the attribute is absent
    ///     or blank.
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the attribute is present but its syntax cannot be parsed as a function list.
    /// </exception>
    private static Matrix3x2 ParseTransformAttribute(XElement element) =>
        ParseTransformList((string?)element.Attribute("transform"));

    /// <summary>
    ///     Parses a <c>gradientTransform</c> attribute, in exactly the same function-list syntax
    ///     as the ordinary <c>transform</c> attribute.
    /// </summary>
    /// <param name="element">The gradient element to inspect.</param>
    /// <returns>The combined transform, or <see cref="Matrix3x2.Identity"/> if absent or blank.</returns>
    /// <exception cref="InvalidDataException">Thrown when the attribute is present but malformed.</exception>
    private static Matrix3x2 ParseGradientTransform(XElement element) =>
        ParseTransformList((string?)element.Attribute("gradientTransform"));

    /// <summary>
    ///     Parses a transform function-list value (shared by the <c>transform</c> and
    ///     <c>gradientTransform</c> attributes). Per the SVG specification, a function list is
    ///     equivalent to the matrix product of its individual functions in the order listed, and
    ///     - because that product is applied to a point as a column-vector left-multiply - the
    ///     <em>last</em>-listed function is the one actually applied to a point first, with the
    ///     <em>first</em>-listed function applied last (for example <c>"translate(10,20) rotate(30)"</c>
    ///     rotates a point first, then translates the rotated result).
    /// </summary>
    /// <param name="raw">The raw attribute value, or <see langword="null"/> if absent.</param>
    /// <returns>
    ///     The combined transform, or <see cref="Matrix3x2.Identity"/> if <paramref name="raw"/>
    ///     is <see langword="null"/> or blank.
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="raw"/> is present but its syntax cannot be parsed as a
    ///     function list.
    /// </exception>
    private static Matrix3x2 ParseTransformList(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Matrix3x2.Identity;
        }

        var result = Matrix3x2.Identity;
        var position = 0;
        while (TryReadTransformFunction(raw, ref position, out var function))
        {
            // Prepending (rather than appending) each newly-read function reproduces the SVG
            // spec's "rightmost function applied first" composition rule under this class's
            // row-vector Matrix3x2 convention (Vector2.Transform(p, A * B) applies A first, then
            // B) - see this method's remarks.
            result = function * result;
        }

        return result;
    }

    /// <summary>
    ///     Checks whether every component of <paramref name="transform"/> is a finite value.
    /// </summary>
    /// <param name="transform">The composed transform to check.</param>
    /// <returns>
    ///     <see langword="true"/> if all six components (<see cref="Matrix3x2.M11"/>,
    ///     <see cref="Matrix3x2.M12"/>, <see cref="Matrix3x2.M21"/>, <see cref="Matrix3x2.M22"/>,
    ///     <see cref="Matrix3x2.M31"/>, <see cref="Matrix3x2.M32"/>) are finite;
    ///     <see langword="false"/> if any is <c>NaN</c> or an infinity.
    /// </returns>
    /// <remarks>
    ///     Every individual transform-function literal parsed by <see cref="ParseTransformList"/>
    ///     is already validated finite on its own (via <see cref="TryReadNumber"/>), but composing
    ///     several individually-finite transforms across nested elements (each cross-element
    ///     product, not any single function's own arguments) can still overflow to a non-finite
    ///     result - this helper lets each composition call site re-validate its own product before
    ///     letting it reach a <see cref="Drawing"/>-namespace constructor's own finiteness check,
    ///     which throws an uncaught <see cref="ArgumentOutOfRangeException"/> rather than this
    ///     codec's documented <see cref="InvalidDataException"/> contract.
    /// </remarks>
    private static bool IsFiniteTransform(Matrix3x2 transform) =>
        float.IsFinite(transform.M11) && float.IsFinite(transform.M12) &&
        float.IsFinite(transform.M21) && float.IsFinite(transform.M22) &&
        float.IsFinite(transform.M31) && float.IsFinite(transform.M32);

    /// <summary>
    ///     Attempts to read one <c>name(arguments)</c> transform function starting at
    ///     <paramref name="position"/>, advancing past it (and any trailing separators) on success.
    /// </summary>
    /// <param name="raw">The full <c>transform</c> attribute text.</param>
    /// <param name="position">The current scan position, advanced past the parsed function.</param>
    /// <param name="function">The parsed function's matrix, if this method returns <see langword="true"/>.</param>
    /// <returns>
    ///     <see langword="true"/> if a function was read; <see langword="false"/> if only
    ///     whitespace/separators remained (end of input).
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when non-whitespace content remains but does not form a valid, recognized
    ///     transform function call.
    /// </exception>
    private static bool TryReadTransformFunction(string raw, ref int position, out Matrix3x2 function)
    {
        SkipSeparators(raw, ref position);
        if (position >= raw.Length)
        {
            function = Matrix3x2.Identity;
            return false;
        }

        var nameStart = position;
        while (position < raw.Length && char.IsLetter(raw[position]))
        {
            position++;
        }

        var name = raw[nameStart..position];
        SkipSeparators(raw, ref position);
        if (name.Length == 0 || position >= raw.Length || raw[position] != '(')
        {
            throw new InvalidDataException($"Malformed transform attribute near position {nameStart}.");
        }

        var closeIndex = raw.IndexOf(')', position);
        if (closeIndex < 0)
        {
            throw new InvalidDataException("Malformed transform attribute: unterminated function call.");
        }

        var argumentsText = raw[(position + 1)..closeIndex];
        position = closeIndex + 1;

        var arguments = ParseNumberList(argumentsText);
        function = BuildTransformFunction(name, arguments);
        return true;
    }

    /// <summary>
    ///     Builds the matrix for one named transform function given its parsed argument list.
    /// </summary>
    /// <param name="name">The function name (<c>translate</c>/<c>scale</c>/<c>rotate</c>/<c>skewX</c>/<c>skewY</c>/<c>matrix</c>).</param>
    /// <param name="arguments">The function's parsed numeric arguments.</param>
    /// <returns>The function's matrix.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="name"/> is not recognized, or <paramref name="arguments"/>
    ///     does not have one of the argument counts that function accepts.
    /// </exception>
    private static Matrix3x2 BuildTransformFunction(string name, IReadOnlyList<float> arguments) => name switch
    {
        "translate" => BuildTranslate(arguments),
        "scale" => BuildScale(arguments),
        "rotate" => BuildRotate(arguments),
        "skewX" => arguments.Count == 1
            ? new Matrix3x2(1f, 0f, MathF.Tan(DegreesToRadians(arguments[0])), 1f, 0f, 0f)
            : throw new InvalidDataException("Malformed skewX transform function: expected exactly one argument."),
        "skewY" => arguments.Count == 1
            ? new Matrix3x2(1f, MathF.Tan(DegreesToRadians(arguments[0])), 0f, 1f, 0f, 0f)
            : throw new InvalidDataException("Malformed skewY transform function: expected exactly one argument."),
        "matrix" => arguments.Count == 6
            ? new Matrix3x2(arguments[0], arguments[1], arguments[2], arguments[3], arguments[4], arguments[5])
            : throw new InvalidDataException("Malformed matrix transform function: expected exactly six arguments."),
        _ => throw new InvalidDataException($"Unrecognized transform function '{name}'.")
    };

    /// <summary>Builds a <c>translate(tx[, ty])</c> function's matrix.</summary>
    /// <param name="arguments">The function's parsed arguments (one or two numbers).</param>
    /// <returns>The translation matrix.</returns>
    /// <exception cref="InvalidDataException">Thrown when the argument count is not one or two.</exception>
    private static Matrix3x2 BuildTranslate(IReadOnlyList<float> arguments) => arguments.Count switch
    {
        1 => Matrix3x2.CreateTranslation(arguments[0], 0f),
        2 => Matrix3x2.CreateTranslation(arguments[0], arguments[1]),
        _ => throw new InvalidDataException("Malformed translate transform function: expected one or two arguments.")
    };

    /// <summary>Builds a <c>scale(sx[, sy])</c> function's matrix.</summary>
    /// <param name="arguments">The function's parsed arguments (one or two numbers).</param>
    /// <returns>The scale matrix.</returns>
    /// <exception cref="InvalidDataException">Thrown when the argument count is not one or two.</exception>
    private static Matrix3x2 BuildScale(IReadOnlyList<float> arguments) => arguments.Count switch
    {
        1 => Matrix3x2.CreateScale(arguments[0], arguments[0]),
        2 => Matrix3x2.CreateScale(arguments[0], arguments[1]),
        _ => throw new InvalidDataException("Malformed scale transform function: expected one or two arguments.")
    };

    /// <summary>Builds a <c>rotate(angle[, cx, cy])</c> function's matrix.</summary>
    /// <param name="arguments">The function's parsed arguments (one or three numbers, angle in degrees).</param>
    /// <returns>The rotation matrix, about the origin or about <c>(cx, cy)</c>.</returns>
    /// <exception cref="InvalidDataException">Thrown when the argument count is not one or three.</exception>
    private static Matrix3x2 BuildRotate(IReadOnlyList<float> arguments)
    {
        switch (arguments.Count)
        {
            case 1:
                return Matrix3x2.CreateRotation(DegreesToRadians(arguments[0]));
            case 3:
                var cx = arguments[1];
                var cy = arguments[2];
                return Matrix3x2.CreateTranslation(-cx, -cy)
                    * Matrix3x2.CreateRotation(DegreesToRadians(arguments[0]))
                    * Matrix3x2.CreateTranslation(cx, cy);
            default:
                throw new InvalidDataException("Malformed rotate transform function: expected one or three arguments.");
        }
    }

    /// <summary>Converts an angle in degrees to radians.</summary>
    /// <param name="degrees">The angle, in degrees.</param>
    /// <returns>The angle, in radians.</returns>
    private static float DegreesToRadians(float degrees) => degrees * (MathF.PI / 180f);

    // ================================================================================================
    // Shape geometry builders (each returns a Path in local, untransformed user-space coordinates)
    // ================================================================================================

    /// <summary>Builds a <c>rect</c> element's outline, including optional rounded corners.</summary>
    /// <param name="element">The <c>rect</c> element.</param>
    /// <returns>
    ///     The local-space path, empty if the rectangle has no positive area, or if its corner-arc
    ///     construction overflows to a non-finite value (see this method's remarks).
    /// </returns>
    /// <remarks>
    ///     A rounded corner's arc-to-Bezier conversion (via <see cref="AppendArcTo"/>) can overflow
    ///     to a non-finite control point or endpoint for an extreme-but-individually-finite
    ///     combination of the rectangle's position/size and corner radii, even though every raw
    ///     literal parsed from the element's attributes is itself finite. Tolerantly skips (returns
    ///     an empty path) in that case, mirroring <see cref="PathDataParser.Parse"/>'s identical
    ///     "catch <see cref="OverflowException"/>, return an empty path" convention for the same
    ///     class of arithmetic-overflow risk in path <c>d</c> data.
    /// </remarks>
    private static Path BuildRectPath(XElement element)
    {
        var x = GetFloatAttribute(element, "x");
        var y = GetFloatAttribute(element, "y");
        var width = GetFloatAttribute(element, "width");
        var height = GetFloatAttribute(element, "height");
        var builder = new PathBuilder();
        if (width <= 0f || height <= 0f)
        {
            return builder.Build();
        }

        var (rx, ry) = ResolveRectRadii(
            GetFloatAttribute(element, "rx", float.NaN),
            GetFloatAttribute(element, "ry", float.NaN),
            width,
            height);

        if (rx <= 0f || ry <= 0f)
        {
            AppendRectOutline(builder, x, y, width, height);
        }
        else
        {
            try
            {
                AppendRoundedRectOutline(builder, x, y, width, height, rx, ry);
            }
            catch (OverflowException)
            {
                // Tolerant skip: see this method's remarks
                return new PathBuilder().Build();
            }
        }

        return builder.Build();
    }

    /// <summary>Appends a sharp-cornered rectangle's outline to <paramref name="builder"/>.</summary>
    /// <param name="builder">The path builder to append to.</param>
    /// <param name="x">The rectangle's left edge.</param>
    /// <param name="y">The rectangle's top edge.</param>
    /// <param name="width">The rectangle's width.</param>
    /// <param name="height">The rectangle's height.</param>
    private static void AppendRectOutline(PathBuilder builder, float x, float y, float width, float height)
    {
        builder.MoveTo(new Vector2(x, y));
        builder.LineTo(new Vector2(x + width, y));
        builder.LineTo(new Vector2(x + width, y + height));
        builder.LineTo(new Vector2(x, y + height));
        builder.Close();
    }

    /// <summary>Appends a rounded-corner rectangle's outline to <paramref name="builder"/>.</summary>
    /// <param name="builder">The path builder to append to.</param>
    /// <param name="x">The rectangle's left edge.</param>
    /// <param name="y">The rectangle's top edge.</param>
    /// <param name="width">The rectangle's width.</param>
    /// <param name="height">The rectangle's height.</param>
    /// <param name="rx">The corner radius along x, already clamped to at most half the width.</param>
    /// <param name="ry">The corner radius along y, already clamped to at most half the height.</param>
    private static void AppendRoundedRectOutline(
        PathBuilder builder,
        float x,
        float y,
        float width,
        float height,
        float rx,
        float ry)
    {
        var radius = new Vector2(rx, ry);
        var topRightStart = new Vector2(x + width - rx, y);
        var rightBottomStart = new Vector2(x + width, y + height - ry);
        var bottomLeftStart = new Vector2(x + rx, y + height);
        var leftTopStart = new Vector2(x, y + ry);

        builder.MoveTo(new Vector2(x + rx, y));
        builder.LineTo(topRightStart);
        AppendArcTo(builder, topRightStart, radius, new Vector2(x + width, y + ry));
        builder.LineTo(rightBottomStart);
        AppendArcTo(builder, rightBottomStart, radius, bottomLeftStart);
        builder.LineTo(bottomLeftStart);
        AppendArcTo(builder, bottomLeftStart, radius, leftTopStart);
        builder.LineTo(leftTopStart);
        AppendArcTo(builder, leftTopStart, radius, new Vector2(x + rx, y));
        builder.Close();
    }

    /// <summary>
    ///     Resolves a <c>rect</c> element's effective corner radii from its (possibly absent)
    ///     <c>rx</c>/<c>ry</c> attributes, per the SVG rule that either one alone implies an equal
    ///     value for the other, then clamps both to at most half the rectangle's corresponding
    ///     side length.
    /// </summary>
    /// <param name="rx">The parsed <c>rx</c> attribute, or <see cref="float.NaN"/> if absent.</param>
    /// <param name="ry">The parsed <c>ry</c> attribute, or <see cref="float.NaN"/> if absent.</param>
    /// <param name="width">The rectangle's width.</param>
    /// <param name="height">The rectangle's height.</param>
    /// <returns>The resolved, clamped <c>(rx, ry)</c> pair; both zero if neither was specified/valid.</returns>
    private static (float Rx, float Ry) ResolveRectRadii(float rx, float ry, float width, float height)
    {
        if (float.IsNaN(rx) && float.IsNaN(ry))
        {
            return (0f, 0f);
        }

        if (float.IsNaN(rx))
        {
            rx = ry;
        }
        else if (float.IsNaN(ry))
        {
            ry = rx;
        }

        if (rx < 0f || ry < 0f)
        {
            return (0f, 0f);
        }

        return (Math.Min(rx, width / 2f), Math.Min(ry, height / 2f));
    }

    /// <summary>Builds a <c>circle</c> or <c>ellipse</c> element's outline from four quarter-arcs.</summary>
    /// <param name="element">The <c>circle</c> or <c>ellipse</c> element.</param>
    /// <param name="isCircle">
    ///     <see langword="true"/> to read the single <c>r</c> radius attribute (<c>circle</c>);
    ///     <see langword="false"/> to read separate <c>rx</c>/<c>ry</c> attributes (<c>ellipse</c>).
    /// </param>
    /// <returns>
    ///     The local-space path, empty if either radius is not positive, or if its quarter-arc
    ///     construction overflows to a non-finite value - see <see cref="BuildRectPath"/>'s
    ///     remarks for the identical tolerant-skip convention applied here.
    /// </returns>
    private static Path BuildEllipsePath(XElement element, bool isCircle)
    {
        var cx = GetFloatAttribute(element, "cx");
        var cy = GetFloatAttribute(element, "cy");
        var radius = isCircle
            ? new Vector2(GetFloatAttribute(element, "r"), GetFloatAttribute(element, "r"))
            : new Vector2(GetFloatAttribute(element, "rx"), GetFloatAttribute(element, "ry"));

        var builder = new PathBuilder();
        if (radius.X <= 0f || radius.Y <= 0f)
        {
            return builder.Build();
        }

        var right = new Vector2(cx + radius.X, cy);
        var bottom = new Vector2(cx, cy + radius.Y);
        var left = new Vector2(cx - radius.X, cy);
        var top = new Vector2(cx, cy - radius.Y);

        try
        {
            builder.MoveTo(right);
            AppendArcTo(builder, right, radius, bottom);
            AppendArcTo(builder, bottom, radius, left);
            AppendArcTo(builder, left, radius, top);
            AppendArcTo(builder, top, radius, right);
            builder.Close();
        }
        catch (OverflowException)
        {
            // Tolerant skip: see BuildRectPath's remarks
            return new PathBuilder().Build();
        }

        return builder.Build();
    }

    /// <summary>Builds a <c>line</c> element's two-point open path.</summary>
    /// <param name="element">The <c>line</c> element.</param>
    /// <returns>The local-space path.</returns>
    private static Path BuildLinePath(XElement element)
    {
        var start = new Vector2(GetFloatAttribute(element, "x1"), GetFloatAttribute(element, "y1"));
        var end = new Vector2(GetFloatAttribute(element, "x2"), GetFloatAttribute(element, "y2"));

        var builder = new PathBuilder();
        builder.MoveTo(start);
        builder.LineTo(end);
        return builder.Build();
    }

    /// <summary>Builds a <c>polyline</c> or <c>polygon</c> element's path from its <c>points</c> attribute.</summary>
    /// <param name="element">The <c>polyline</c> or <c>polygon</c> element.</param>
    /// <param name="closed"><see langword="true"/> for <c>polygon</c>; <see langword="false"/> for <c>polyline</c>.</param>
    /// <param name="workBudget">
    ///     The shared geometry-parsing work budget, charged incrementally as each coordinate pair
    ///     is parsed (see <see cref="ParsePointList"/>).
    /// </param>
    /// <returns>The local-space path, empty if fewer than two points are present.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the resolved point count pushes the combined geometry-parsing work total
    ///     past <see cref="GeometryWorkBudget"/>'s fixed budget.
    /// </exception>
    private static Path BuildPolyPath(XElement element, bool closed, GeometryWorkBudget workBudget)
    {
        var points = ParsePointList((string?)element.Attribute("points"), workBudget);
        var builder = new PathBuilder();
        if (points.Count < 2)
        {
            return builder.Build();
        }

        builder.MoveTo(points[0]);
        for (var i = 1; i < points.Count; i++)
        {
            builder.LineTo(points[i]);
        }

        if (closed)
        {
            builder.Close();
        }

        return builder.Build();
    }

    /// <summary>Parses a <c>points</c> attribute's flat number list into coordinate pairs.</summary>
    /// <param name="raw">The attribute's raw value, or <see langword="null"/> if absent.</param>
    /// <param name="workBudget">
    ///     The shared geometry-parsing work budget, charged with <c>1</c> unit immediately after
    ///     each coordinate pair is parsed - before the next pair is read - using the same
    ///     <see cref="SkipSeparators"/>/<see cref="TryReadNumber"/> primitives
    ///     <see cref="ParseNumberList"/> itself uses, rather than delegating to
    ///     <see cref="ParseNumberList"/> and charging the whole resolved count in one batch at the
    ///     end. This mirrors <see cref="PathDataParser"/>'s own per-command incremental charging,
    ///     so a single pathologically large <c>points</c> string throws partway through parsing -
    ///     without ever materializing the full coordinate list - rather than only after its entire
    ///     (otherwise unbounded) content has already been scanned and allocated.
    /// </param>
    /// <returns>The parsed points, in document order. A trailing unpaired number is dropped.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when a token is not a valid number (matching <see cref="ParseNumberList"/>'s own
    ///     "malformed token" message convention), or when the resolved point count pushes the
    ///     combined geometry-parsing work total past <see cref="GeometryWorkBudget"/>'s fixed
    ///     budget.
    /// </exception>
    private static List<Vector2> ParsePointList(string? raw, GeometryWorkBudget workBudget)
    {
        var points = new List<Vector2>();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return points;
        }

        var position = 0;
        while (true)
        {
            // Read the pair's first coordinate - reaching end-of-input here (rather than a
            // malformed token) means every preceding pair has already been fully parsed and
            // charged, so this is the normal, successful end of the list
            SkipSeparators(raw, ref position);
            if (position >= raw.Length)
            {
                break;
            }

            if (!TryReadNumber(raw, ref position, out var x))
            {
                throw new InvalidDataException($"Malformed number list: unexpected character at position {position}.");
            }

            // A lone trailing number with nothing left to pair it with is silently dropped, not
            // an error - this is the only case end-of-input is reached between a pair's two
            // coordinates rather than before the pair starts
            SkipSeparators(raw, ref position);
            if (position >= raw.Length)
            {
                break;
            }

            if (!TryReadNumber(raw, ref position, out var y))
            {
                throw new InvalidDataException($"Malformed number list: unexpected character at position {position}.");
            }

            // Charge this pair immediately, before continuing to scan the next one, so a hostile
            // huge points string is rejected as soon as the budget is exceeded rather than after
            // the whole string has already been scanned
            points.Add(new Vector2(x, y));
            workBudget.Charge(1);
        }

        return points;
    }

    /// <summary>
    ///     Appends one arc segment (converted to cubic Beziers via <see cref="Geometry.SvgArcConverter"/>)
    ///     to <paramref name="builder"/>, using the fixed <c>rotation=0, largeArc=false, sweep=true</c>
    ///     parameters every quarter-arc built by this codec's own shape builders (rounded-rect
    ///     corners, circles, ellipses) needs.
    /// </summary>
    /// <param name="builder">The path builder to append to.</param>
    /// <param name="start">The arc's start point (must match the builder's current point).</param>
    /// <param name="radius">The arc's x/y radii.</param>
    /// <param name="end">The arc's end point.</param>
    private static void AppendArcTo(PathBuilder builder, Vector2 start, Vector2 radius, Vector2 end)
    {
        var segments = new List<(Vector2 Control1, Vector2 Control2, Vector2 End)>();
        SvgArcConverter.ToBeziers(start, radius, 0f, false, true, end, segments);
        foreach (var segment in segments)
        {
            builder.CubicBezierTo(
                RequireFiniteArcPoint(segment.Control1),
                RequireFiniteArcPoint(segment.Control2),
                RequireFiniteArcPoint(segment.End));
        }
    }

    /// <summary>
    ///     Validates that <paramref name="value"/>'s components are both finite, throwing
    ///     <see cref="OverflowException"/> otherwise - used by <see cref="AppendArcTo"/> to guard
    ///     <see cref="Geometry.SvgArcConverter"/>'s output against an extreme-but-individually-
    ///     finite radius/start/end combination whose internal rotation/trig arithmetic overflows
    ///     to a non-finite control point or endpoint. Mirrors
    ///     <see cref="PathDataParser.RequireFinite(Vector2)"/>'s identical tolerant-skip
    ///     convention - the same BCL <see cref="OverflowException"/> sentinel type, reused here
    ///     rather than a bespoke exception, so <see cref="BuildRectPath"/>/
    ///     <see cref="BuildEllipsePath"/>'s own narrowly-scoped catch clauses can identify it
    ///     without risking confusion with an actual arithmetic-overflow bug elsewhere.
    /// </summary>
    /// <param name="value">The point to validate.</param>
    /// <returns><paramref name="value"/> unchanged, when both components are finite.</returns>
    /// <exception cref="OverflowException">Thrown when either component of <paramref name="value"/> is not finite.</exception>
    private static Vector2 RequireFiniteArcPoint(Vector2 value)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y))
        {
            throw new OverflowException("Arc-to-Bezier conversion overflowed to a non-finite value.");
        }

        return value;
    }

    // ================================================================================================
    // Path data ("d" attribute) mini-language parsing
    // ================================================================================================

    /// <summary>Builds a <c>path</c> element's outline from its <c>d</c> attribute.</summary>
    /// <param name="element">The <c>path</c> element.</param>
    /// <param name="workBudget">The shared geometry-parsing work budget, charged once per parsed command.</param>
    /// <returns>The local-space path, empty if <c>d</c> is absent or blank.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>d</c> is present but not valid path data, or parsing its commands pushes
    ///     the combined geometry-parsing work total past <see cref="GeometryWorkBudget"/>'s fixed
    ///     budget.
    /// </exception>
    private static Path BuildPathDataPath(XElement element, GeometryWorkBudget workBudget)
    {
        var d = (string?)element.Attribute("d");
        return string.IsNullOrWhiteSpace(d) ? new PathBuilder().Build() : new PathDataParser(d, workBudget).Parse();
    }

    /// <summary>
    ///     A single-use, stateful parser for one SVG <c>path</c> element's <c>d</c> attribute
    ///     mini-language (<c>M/m L/l H/h V/v C/c S/s Q/q T/t A/a Z/z</c>, both absolute and
    ///     relative, including every command's "implicit repeat" shorthand for additional
    ///     argument groups following the same command letter).
    /// </summary>
    /// <remarks>
    ///     Kept as its own nested class, rather than a set of methods threading many <c>ref</c>
    ///     parameters through <see cref="SvgCodec"/> directly, so that each SVG command becomes a
    ///     small, single-responsibility instance method operating on private fields - the same
    ///     rationale documented for <see cref="RenderContext"/> above.
    /// </remarks>
    private sealed class PathDataParser
    {
        /// <summary>The full <c>d</c> attribute text being parsed.</summary>
        private readonly string _text;

        /// <summary>The builder accumulating the parsed path's commands.</summary>
        private readonly PathBuilder _builder = new();

        /// <summary>
        ///     The shared geometry-parsing work budget, charged once per emitted path command so a
        ///     single pathological <c>d</c> string throws partway through parsing rather than
        ///     after its entire (unbounded) content has already been scanned. Stored as an
        ///     ordinary field (rather than a <c>ref int</c>, the convention used elsewhere in this
        ///     class) because a <c>ref</c> parameter cannot be assigned into an instance field of
        ///     an ordinary class - see <see cref="GeometryWorkBudget"/>'s own remarks.
        /// </summary>
        private readonly GeometryWorkBudget _workBudget;

        /// <summary>The current scan position within <see cref="_text"/>.</summary>
        private int _position;

        /// <summary>The current point (the end point of the most recently issued command).</summary>
        private Vector2 _current;

        /// <summary>The point the current subpath began at, restored by a <c>Z</c>/<c>z</c> command.</summary>
        private Vector2 _subpathStart;

        /// <summary>
        ///     The most recent cubic Bezier's second control point, used to compute the implicit
        ///     reflected control point for a following <c>S</c>/<c>s</c> command; <see langword="null"/>
        ///     if the previous command was not a cubic Bezier (in which case <c>S</c>/<c>s</c>
        ///     reflects about the current point itself, per spec).
        /// </summary>
        private Vector2? _lastCubicControl;

        /// <summary>
        ///     The most recent quadratic Bezier's control point, used analogously to
        ///     <see cref="_lastCubicControl"/> for a following <c>T</c>/<c>t</c> command.
        /// </summary>
        private Vector2? _lastQuadControl;

        /// <summary><see langword="true"/> once the first command has been read.</summary>
        private bool _started;

        /// <summary>Initializes a new parser over <paramref name="text"/>.</summary>
        /// <param name="text">The <c>d</c> attribute's full raw text.</param>
        /// <param name="workBudget">The shared geometry-parsing work budget to charge as commands are parsed.</param>
        public PathDataParser(string text, GeometryWorkBudget workBudget)
        {
            _text = text;
            _workBudget = workBudget;
        }

        /// <summary>Parses the whole <c>d</c> attribute and builds its path.</summary>
        /// <returns>
        ///     The resulting local-space path; an empty path (see <see cref="PathBuilder.Build"/>)
        ///     if relative-coordinate accumulation or a smooth-curve reflection (see
        ///     <see cref="RequireFinite(Vector2)"/>/<see cref="RequireFinite(float)"/>) overflows
        ///     an individually-finite pair of literals to a non-finite result partway through
        ///     parsing - a tolerant "skip this element" outcome, matching this class's existing
        ///     tolerant handling of a composed non-finite transform elsewhere in this codec,
        ///     because a non-finite point cannot be rendered meaningfully and, left unchecked,
        ///     would stall <see cref="Drawing.DashSplitter"/>'s dash-interval walk (its existing
        ///     "huge-but-finite" double-widening fix does not cover a genuinely
        ///     <c>Infinity</c>-valued coordinate).
        /// </returns>
        /// <exception cref="InvalidDataException">
        ///     Thrown when the text is not valid path data, or parsing its commands pushes the
        ///     combined geometry-parsing work total past <see cref="GeometryWorkBudget"/>'s fixed
        ///     budget.
        /// </exception>
        public Path Parse()
        {
            try
            {
                while (true)
                {
                    SkipSeparators(_text, ref _position);
                    if (_position >= _text.Length)
                    {
                        break;
                    }

                    var command = _text[_position];
                    if (!IsCommandLetter(command))
                    {
                        throw new InvalidDataException($"Malformed path data: expected a command letter at position {_position}.");
                    }

                    if (!_started && char.ToUpperInvariant(command) != 'M')
                    {
                        throw new InvalidDataException("Malformed path data: the first command must be a moveto (M/m).");
                    }

                    _position++;
                    ExecuteCommand(command);
                    _started = true;
                }

                return _builder.Build();
            }
            catch (OverflowException)
            {
                // Tolerant skip: RequireFinite raises this private-to-this-parser sentinel (the
                // BCL's own OverflowException, reused rather than a bespoke exception type, so no
                // catch clause elsewhere in this codec's Load/GetInfo boundary can mistake it for
                // an actual arithmetic-overflow bug) when a non-finite accumulated/reflected point
                // is produced - see this method's own remarks above for why an empty path is
                // returned rather than the exception propagating further
                return new PathBuilder().Build();
            }
        }

        /// <summary>Determines whether <paramref name="ch"/> is one of the recognized path command letters.</summary>
        /// <param name="ch">The character to test.</param>
        /// <returns><see langword="true"/> if <paramref name="ch"/> is a recognized command letter.</returns>
        private static bool IsCommandLetter(char ch) => "MmLlHhVvCcSsQqTtAaZz".Contains(ch);

        /// <summary>Dispatches one command letter (and its full run of implicitly repeated argument groups).</summary>
        /// <param name="command">The command letter just consumed.</param>
        /// <exception cref="InvalidDataException">Thrown when <paramref name="command"/> is not recognized.</exception>
        private void ExecuteCommand(char command)
        {
            switch (char.ToUpperInvariant(command))
            {
                case 'M':
                    ExecuteMoveTo(command);
                    break;
                case 'L':
                    ExecuteLineTo(command);
                    break;
                case 'H':
                    ExecuteHorizontal(command);
                    break;
                case 'V':
                    ExecuteVertical(command);
                    break;
                case 'C':
                    ExecuteCubic(command);
                    break;
                case 'S':
                    ExecuteSmoothCubic(command);
                    break;
                case 'Q':
                    ExecuteQuadratic(command);
                    break;
                case 'T':
                    ExecuteSmoothQuadratic(command);
                    break;
                case 'A':
                    ExecuteArc(command);
                    break;
                case 'Z':
                    ExecuteClose();
                    break;
                default:
                    throw new InvalidDataException($"Unrecognized path command '{command}'.");
            }
        }

        /// <summary>Executes an <c>M</c>/<c>m</c> command and any implicitly repeated <c>L</c>/<c>l</c>-equivalent groups.</summary>
        /// <param name="command">The literal command letter (<c>M</c> or <c>m</c>).</param>
        private void ExecuteMoveTo(char command)
        {
            var isRelative = char.IsLower(command);
            _current = ReadPoint(isRelative, _current);
            _subpathStart = _current;
            _builder.MoveTo(_current);
            ClearReflectionState();
            _workBudget.Charge(1);

            while (TryPeekNumber())
            {
                _current = ReadPoint(isRelative, _current);
                _builder.LineTo(_current);
                ClearReflectionState();
                _workBudget.Charge(1);
            }
        }

        /// <summary>Executes an <c>L</c>/<c>l</c> command and any implicitly repeated argument groups.</summary>
        /// <param name="command">The literal command letter (<c>L</c> or <c>l</c>).</param>
        private void ExecuteLineTo(char command)
        {
            var isRelative = char.IsLower(command);
            do
            {
                _current = ReadPoint(isRelative, _current);
                _builder.LineTo(_current);
                ClearReflectionState();
                _workBudget.Charge(1);
            } while (TryPeekNumber());
        }

        /// <summary>Executes an <c>H</c>/<c>h</c> (horizontal line) command and any implicitly repeated groups.</summary>
        /// <param name="command">The literal command letter (<c>H</c> or <c>h</c>).</param>
        private void ExecuteHorizontal(char command)
        {
            var isRelative = char.IsLower(command);
            do
            {
                var x = ReadNumber();
                _current = new Vector2(isRelative ? RequireFinite(_current.X + x) : x, _current.Y);
                _builder.LineTo(_current);
                ClearReflectionState();
                _workBudget.Charge(1);
            } while (TryPeekNumber());
        }

        /// <summary>Executes a <c>V</c>/<c>v</c> (vertical line) command and any implicitly repeated groups.</summary>
        /// <param name="command">The literal command letter (<c>V</c> or <c>v</c>).</param>
        private void ExecuteVertical(char command)
        {
            var isRelative = char.IsLower(command);
            do
            {
                var y = ReadNumber();
                _current = new Vector2(_current.X, isRelative ? RequireFinite(_current.Y + y) : y);
                _builder.LineTo(_current);
                ClearReflectionState();
                _workBudget.Charge(1);
            } while (TryPeekNumber());
        }

        /// <summary>Executes a <c>C</c>/<c>c</c> (cubic Bezier) command and any implicitly repeated groups.</summary>
        /// <param name="command">The literal command letter (<c>C</c> or <c>c</c>).</param>
        private void ExecuteCubic(char command)
        {
            var isRelative = char.IsLower(command);
            do
            {
                var control1 = ReadPoint(isRelative, _current);
                var control2 = ReadPoint(isRelative, _current);
                var end = ReadPoint(isRelative, _current);
                _builder.CubicBezierTo(control1, control2, end);
                _current = end;
                _lastCubicControl = control2;
                _lastQuadControl = null;
                _workBudget.Charge(1);
            } while (TryPeekNumber());
        }

        /// <summary>Executes an <c>S</c>/<c>s</c> (smooth cubic Bezier) command and any implicitly repeated groups.</summary>
        /// <param name="command">The literal command letter (<c>S</c> or <c>s</c>).</param>
        private void ExecuteSmoothCubic(char command)
        {
            var isRelative = char.IsLower(command);
            do
            {
                var control2 = ReadPoint(isRelative, _current);
                var end = ReadPoint(isRelative, _current);
                var control1 = _lastCubicControl.HasValue ? Reflect(_lastCubicControl.Value, _current) : _current;
                _builder.CubicBezierTo(control1, control2, end);
                _current = end;
                _lastCubicControl = control2;
                _lastQuadControl = null;
                _workBudget.Charge(1);
            } while (TryPeekNumber());
        }

        /// <summary>Executes a <c>Q</c>/<c>q</c> (quadratic Bezier) command and any implicitly repeated groups.</summary>
        /// <param name="command">The literal command letter (<c>Q</c> or <c>q</c>).</param>
        private void ExecuteQuadratic(char command)
        {
            var isRelative = char.IsLower(command);
            do
            {
                var control = ReadPoint(isRelative, _current);
                var end = ReadPoint(isRelative, _current);
                _builder.QuadraticBezierTo(control, end);
                _current = end;
                _lastQuadControl = control;
                _lastCubicControl = null;
                _workBudget.Charge(1);
            } while (TryPeekNumber());
        }

        /// <summary>Executes a <c>T</c>/<c>t</c> (smooth quadratic Bezier) command and any implicitly repeated groups.</summary>
        /// <param name="command">The literal command letter (<c>T</c> or <c>t</c>).</param>
        private void ExecuteSmoothQuadratic(char command)
        {
            var isRelative = char.IsLower(command);
            do
            {
                var end = ReadPoint(isRelative, _current);
                var control = _lastQuadControl.HasValue ? Reflect(_lastQuadControl.Value, _current) : _current;
                _builder.QuadraticBezierTo(control, end);
                _current = end;
                _lastQuadControl = control;
                _lastCubicControl = null;
                _workBudget.Charge(1);
            } while (TryPeekNumber());
        }

        /// <summary>Executes an <c>A</c>/<c>a</c> (elliptical arc) command and any implicitly repeated groups.</summary>
        /// <param name="command">The literal command letter (<c>A</c> or <c>a</c>).</param>
        private void ExecuteArc(char command)
        {
            var isRelative = char.IsLower(command);
            do
            {
                var radius = new Vector2(MathF.Abs(ReadNumber()), MathF.Abs(ReadNumber()));
                var rotationDegrees = ReadNumber();
                var largeArc = ReadFlag();
                var sweep = ReadFlag();
                var end = ReadPoint(isRelative, _current);
                AppendArc(radius, rotationDegrees, largeArc, sweep, end);
                _workBudget.Charge(1);
            } while (TryPeekNumber());
        }

        /// <summary>Executes a <c>Z</c>/<c>z</c> (close path) command. Takes no arguments and is never repeated.</summary>
        private void ExecuteClose()
        {
            _builder.Close();
            _current = _subpathStart;
            ClearReflectionState();
            _workBudget.Charge(1);
        }

        /// <summary>
        ///     Converts one elliptical arc segment to cubic Beziers (via <see cref="Geometry.SvgArcConverter"/>)
        ///     and appends them to the builder, advancing the current point and clearing the
        ///     smooth-curve reflection state.
        /// </summary>
        /// <param name="radius">The arc's x- and y-radii.</param>
        /// <param name="rotationDegrees">The arc's x-axis rotation, in degrees.</param>
        /// <param name="largeArc">The SVG arc "large-arc-flag".</param>
        /// <param name="sweep">The SVG arc "sweep-flag".</param>
        /// <param name="end">The arc's end point.</param>
        /// <exception cref="OverflowException">
        ///     Thrown when an extreme-but-individually-finite radius/rotation/start/end
        ///     combination causes <see cref="Geometry.SvgArcConverter"/>'s internal rotation/trig
        ///     arithmetic to overflow one of its emitted control points or endpoints to a
        ///     non-finite value. Caught (private to this parser) by <see cref="Parse"/>.
        /// </exception>
        private void AppendArc(Vector2 radius, float rotationDegrees, bool largeArc, bool sweep, Vector2 end)
        {
            var segments = new List<(Vector2 Control1, Vector2 Control2, Vector2 End)>();
            SvgArcConverter.ToBeziers(_current, radius, rotationDegrees, largeArc, sweep, end, segments);
            foreach (var segment in segments)
            {
                _builder.CubicBezierTo(
                    RequireFinite(segment.Control1),
                    RequireFinite(segment.Control2),
                    RequireFinite(segment.End));
            }

            _current = end;
            ClearReflectionState();
        }

        /// <summary>Clears both smooth-curve reflection fields, called after any non-Bezier command.</summary>
        private void ClearReflectionState()
        {
            _lastCubicControl = null;
            _lastQuadControl = null;
        }

        /// <summary>Reflects <paramref name="point"/> through <paramref name="center"/> (<c>2*center - point</c>).</summary>
        /// <param name="point">The point to reflect.</param>
        /// <param name="center">The center of reflection.</param>
        /// <returns>The reflected point.</returns>
        /// <exception cref="OverflowException">
        ///     Thrown when the reflection arithmetic overflows an individually-finite
        ///     <paramref name="point"/>/<paramref name="center"/> pair to a non-finite result - an
        ///     independent overflow path into the same <see cref="Drawing.DashSplitter"/> hang
        ///     risk as relative-coordinate accumulation, reached via the <c>S</c>/<c>s</c> and
        ///     <c>T</c>/<c>t</c> smooth-curve commands. Caught (private to this parser) by
        ///     <see cref="Parse"/>.
        /// </exception>
        private static Vector2 Reflect(Vector2 point, Vector2 center) => RequireFinite((2 * center) - point);

        /// <summary>Reads one <c>x,y</c> coordinate pair, resolving it against <paramref name="reference"/> if relative.</summary>
        /// <param name="isRelative">Whether the pair is relative to <paramref name="reference"/>.</param>
        /// <param name="reference">The reference point for a relative pair (ignored if absolute).</param>
        /// <returns>The resolved, absolute point.</returns>
        /// <exception cref="InvalidDataException">Thrown when a valid number cannot be read.</exception>
        /// <exception cref="OverflowException">
        ///     Thrown when a relative pair's offset accumulation overflows an
        ///     individually-finite <paramref name="reference"/>/offset pair to a non-finite
        ///     result. Caught (private to this parser) by <see cref="Parse"/>.
        /// </exception>
        private Vector2 ReadPoint(bool isRelative, Vector2 reference)
        {
            var x = ReadNumber();
            var y = ReadNumber();
            return isRelative ? RequireFinite(reference + new Vector2(x, y)) : new Vector2(x, y);
        }

        /// <summary>
        ///     Validates that <paramref name="value"/>'s components are both finite, throwing
        ///     <see cref="OverflowException"/> otherwise - called at every point produced by
        ///     arithmetic (relative-offset accumulation or smooth-curve reflection), never at a
        ///     point built directly from two already-finite parsed literals (which cannot itself
        ///     overflow, and so needs no check). <see cref="OverflowException"/> (a BCL type,
        ///     rather than a bespoke exception) is caught only within this parser's own
        ///     <see cref="Parse"/> method - it never escapes to this class's top-level
        ///     <c>Load</c>/<c>GetInfo</c> boundary, and so cannot be confused there with an actual
        ///     arithmetic-overflow bug elsewhere in this codec.
        /// </summary>
        /// <param name="value">The point to validate.</param>
        /// <returns><paramref name="value"/> unchanged, when both components are finite.</returns>
        /// <exception cref="OverflowException">Thrown when either component of <paramref name="value"/> is not finite.</exception>
        private static Vector2 RequireFinite(Vector2 value)
        {
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y))
            {
                throw new OverflowException("Path-data relative-coordinate accumulation overflowed to a non-finite value.");
            }

            return value;
        }

        /// <summary>
        ///     The scalar overload of <see cref="RequireFinite(Vector2)"/>, used by
        ///     <see cref="ExecuteHorizontal"/>/<see cref="ExecuteVertical"/>'s relative branch,
        ///     where only a single axis is accumulated.
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <returns><paramref name="value"/> unchanged, when finite.</returns>
        /// <exception cref="OverflowException">Thrown when <paramref name="value"/> is not finite.</exception>
        private static float RequireFinite(float value)
        {
            if (!float.IsFinite(value))
            {
                throw new OverflowException("Path-data relative-coordinate accumulation overflowed to a non-finite value.");
            }

            return value;
        }

        /// <summary>Reads one number, advancing past it.</summary>
        /// <returns>The parsed number.</returns>
        /// <exception cref="InvalidDataException">Thrown when a valid number cannot be read at the current position.</exception>
        private float ReadNumber()
        {
            if (!TryReadNumber(_text, ref _position, out var value))
            {
                throw new InvalidDataException("Malformed path data: expected a number.");
            }

            return value;
        }

        /// <summary>Reads one SVG arc flag (a bare <c>0</c> or <c>1</c> digit), advancing past it.</summary>
        /// <returns><see langword="true"/> for <c>1</c>; <see langword="false"/> for <c>0</c>.</returns>
        /// <exception cref="InvalidDataException">Thrown when the current position is not a <c>0</c> or <c>1</c> digit.</exception>
        private bool ReadFlag()
        {
            SkipSeparators(_text, ref _position);
            if (_position < _text.Length && (_text[_position] == '0' || _text[_position] == '1'))
            {
                var value = _text[_position] == '1';
                _position++;
                return value;
            }

            throw new InvalidDataException("Malformed path data: expected an arc flag ('0' or '1').");
        }

        /// <summary>Determines whether another number follows at the current position, without consuming it.</summary>
        /// <returns><see langword="true"/> if another argument group should be read (implicit command repeat).</returns>
        private bool TryPeekNumber()
        {
            var probe = _position;
            return TryReadNumber(_text, ref probe, out _);
        }
    }

    // ================================================================================================
    // Path transform mapping and shape rendering
    // ================================================================================================

    /// <summary>
    ///     Re-issues every subpath/command of <paramref name="source"/> through a fresh
    ///     <see cref="PathBuilder"/>, mapping every point through <paramref name="transform"/>.
    /// </summary>
    /// <param name="source">The local, untransformed-space path to remap.</param>
    /// <param name="transform">The transform mapping local-space points to pixel-space points.</param>
    /// <returns>A new path with every point transformed, and the same command structure.</returns>
    /// <remarks>
    ///     Mirrors <c>TrueTypeFontRealFontIntegrationTests</c>' "re-issue through a fresh
    ///     <see cref="PathBuilder"/> with a point-mapping lambda" pattern: this is the one place
    ///     every shape/glyph outline this codec builds gets baked into final pixel-space
    ///     coordinates, since <see cref="Drawing.PathFiller"/>/<see cref="Drawing.PathStroker"/>
    ///     have no transform parameter of their own.
    /// </remarks>
    private static Path TransformPath(Path source, Matrix3x2 transform)
    {
        var builder = new PathBuilder();
        AppendTransformedPathInto(builder, source, transform);
        return builder.Build();
    }

    /// <summary>
    ///     Appends every subpath of <paramref name="source"/> into <paramref name="builder"/>'s
    ///     already-in-progress build, transformed through <paramref name="transform"/> - the
    ///     shared core of <see cref="TransformPath"/> (a fresh builder) and glyph-run assembly (an
    ///     existing builder accumulating multiple glyphs' outlines into one combined path).
    /// </summary>
    /// <param name="builder">The destination builder.</param>
    /// <param name="source">The local, untransformed-space path to append.</param>
    /// <param name="transform">The transform mapping local-space points to the destination space.</param>
    private static void AppendTransformedPathInto(PathBuilder builder, Path source, Matrix3x2 transform)
    {
        foreach (var subpath in source.Subpaths)
        {
            builder.MoveTo(Vector2.Transform(subpath.Start, transform));
            AppendTransformedCommands(builder, subpath.Commands, transform);
        }
    }

    /// <summary>Appends one subpath's already-open commands to <paramref name="builder"/>, transformed.</summary>
    /// <param name="builder">The destination builder, already positioned via a preceding <see cref="PathBuilder.MoveTo"/>.</param>
    /// <param name="commands">The source subpath's commands, in local/untransformed space.</param>
    /// <param name="transform">The transform mapping local-space points to pixel-space points.</param>
    /// <exception cref="NotSupportedException">
    ///     Thrown for a <see cref="PathCommandType.ArcTo"/> command - this codec never issues one
    ///     (arcs are always pre-converted to cubic Beziers before reaching this method), so
    ///     encountering one indicates an internal defect rather than a data-driven condition.
    /// </exception>
    private static void AppendTransformedCommands(PathBuilder builder, IReadOnlyList<PathCommand> commands, Matrix3x2 transform)
    {
        foreach (var command in commands)
        {
            switch (command.Type)
            {
                case PathCommandType.LineTo:
                    builder.LineTo(Vector2.Transform(command.EndPoint, transform));
                    break;
                case PathCommandType.QuadraticBezierTo:
                    builder.QuadraticBezierTo(
                        Vector2.Transform(command.Control1, transform),
                        Vector2.Transform(command.EndPoint, transform));
                    break;
                case PathCommandType.CubicBezierTo:
                    builder.CubicBezierTo(
                        Vector2.Transform(command.Control1, transform),
                        Vector2.Transform(command.Control2, transform),
                        Vector2.Transform(command.EndPoint, transform));
                    break;
                case PathCommandType.Close:
                    builder.Close();
                    break;
                default:
                    throw new NotSupportedException(
                        $"Unsupported path command type '{command.Type}' encountered while transforming a path.");
            }
        }
    }

    /// <summary>
    ///     Renders one shape's local-space outline: transforms it into pixel space once, then
    ///     fills and/or strokes it per <paramref name="state"/>.
    /// </summary>
    /// <param name="localPath">The shape's outline, in local (untransformed) user-space coordinates.</param>
    /// <param name="state">The cascaded render state supplying fill/stroke paint and style.</param>
    /// <param name="transform">The accumulated transform mapping local space to pixel space.</param>
    /// <param name="context">The fixed per-document render context.</param>
    private static void RenderShape(Path localPath, RenderState state, Matrix3x2 transform, RenderContext context)
    {
        if (localPath.Subpaths.Count == 0)
        {
            return;
        }

        var pixelPath = TransformPath(localPath, transform);
        RenderFill(localPath, pixelPath, state, transform, context);
        RenderStroke(localPath, pixelPath, state, transform, context);
    }

    /// <summary>Fills <paramref name="pixelPath"/> per <paramref name="state"/>'s <c>fill</c> paint.</summary>
    /// <param name="localPath">The shape's local-space outline, used as a gradient's object-bounding-box basis.</param>
    /// <param name="pixelPath">The shape's already pixel-space-transformed outline.</param>
    /// <param name="state">The cascaded render state.</param>
    /// <param name="transform">The accumulated transform, used to resolve a gradient's own transform.</param>
    /// <param name="context">The fixed per-document render context.</param>
    private static void RenderFill(Path localPath, Path pixelPath, RenderState state, Matrix3x2 transform, RenderContext context)
    {
        var paint = ResolvePaint(state.Fill, state.FillOpacity * state.Opacity, localPath, transform, context);
        FillWithPaint(context.Surface, pixelPath, paint, state.FillRule);
    }

    /// <summary>
    ///     Strokes <paramref name="pixelPath"/> per <paramref name="state"/>'s <c>stroke</c> paint
    ///     and stroke-style attributes, converting the stroke to fillable outline geometry first
    ///     via <see cref="Drawing.PathStroker"/>.
    /// </summary>
    /// <param name="localPath">The shape's local-space outline, used as a gradient's object-bounding-box basis.</param>
    /// <param name="pixelPath">The shape's already pixel-space-transformed outline.</param>
    /// <param name="state">The cascaded render state.</param>
    /// <param name="transform">The accumulated transform, used to estimate the pixel-space stroke-width scale.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <remarks>
    ///     A no-op stroke (<c>stroke="none"</c>, a dangling gradient reference, or a non-positive
    ///     effective stroke width) never constructs a <see cref="Drawing.StrokeStyle"/> at all,
    ///     avoiding its constructor's own <see cref="ArgumentOutOfRangeException"/> for a
    ///     zero width. The same tolerant skip also covers a non-finite effective stroke width: the
    ///     locally-finite <c>stroke-width</c> is scaled by <see cref="EstimateUniformScale"/>,
    ///     whose composed nested <c>transform="scale(...)"</c> determinant can overflow to a
    ///     non-finite value (<c>Infinity</c>, or <c>NaN</c> if the overflow arithmetic itself
    ///     produces an indeterminate result) even though every individual transform literal was
    ///     finite - such an overflowed width would otherwise pass the <c>&lt;= 0f</c> check (since
    ///     neither <c>Infinity</c> nor <c>NaN</c> compares <c>&lt;= 0f</c>) and reach
    ///     <see cref="Drawing.StrokeStyle"/>'s constructor, which throws an uncaught
    ///     <see cref="ArgumentOutOfRangeException"/> for it.
    /// </remarks>
    private static void RenderStroke(Path localPath, Path pixelPath, RenderState state, Matrix3x2 transform, RenderContext context)
    {
        var scale = EstimateUniformScale(transform);
        var strokeWidth = state.StrokeWidth * scale;
        if (!float.IsFinite(strokeWidth) || strokeWidth <= 0f)
        {
            return;
        }

        var paint = ResolvePaint(state.Stroke, state.StrokeOpacity * state.Opacity, localPath, transform, context);
        if (paint == null)
        {
            return;
        }

        // The scaled dasharray/dashoffset - not just their raw, already-finite parsed values -
        // must be validated: 'scale' (like strokeWidth's own scale above) can itself be extreme
        // from a composed transform, overflowing an individually-finite dash entry/offset to a
        // non-finite value that would otherwise reach StrokeStyle's constructor and throw an
        // uncaught ArgumentOutOfRangeException/ArgumentException. Tolerantly fall back to "no
        // dashing" (a solid stroke) rather than skipping the whole stroke - the stroke geometry
        // itself is still perfectly valid, only its dash pattern overflowed - mirroring
        // ParseDashArray's own existing tolerant "malformed dash array -> no dashing" convention.
        var scaledDashArray = ScaleDashArray(state.StrokeDashArray, scale);
        var scaledDashOffset = state.StrokeDashOffset * scale;
        if (!float.IsFinite(scaledDashOffset) || (scaledDashArray?.Any(v => !float.IsFinite(v)) ?? false))
        {
            scaledDashArray = null;
            scaledDashOffset = 0f;
        }

        var style = new StrokeStyle(
            strokeWidth,
            state.StrokeLineCap,
            state.StrokeLineJoin,
            state.StrokeMiterLimit,
            scaledDashArray,
            scaledDashOffset);

        var outline = PathStroker.Stroke(pixelPath, style);
        FillWithPaint(context.Surface, outline, paint, FillRule.NonZero);
    }

    /// <summary>Fills <paramref name="path"/> with a resolved paint value, tolerating a <see langword="null"/> (no-op) paint.</summary>
    /// <param name="surface">The surface to fill into.</param>
    /// <param name="path">The pixel-space path to fill.</param>
    /// <param name="paint">The resolved paint: a boxed <see cref="Rgba32"/>, a <see cref="Gradient"/>, or <see langword="null"/>.</param>
    /// <param name="fillRule">The fill rule to apply.</param>
    private static void FillWithPaint(Surface surface, Path path, object? paint, FillRule fillRule)
    {
        switch (paint)
        {
            case Rgba32 color:
                PathFiller.Fill(surface, path, color, fillRule);
                break;
            case Gradient gradient:
                PathFiller.Fill(surface, path, gradient, fillRule);
                break;
        }
    }

    /// <summary>
    ///     Estimates a single isotropic scale factor for <paramref name="transform"/>, used to map
    ///     a local-space stroke-width/dash-array into pixel space.
    /// </summary>
    /// <param name="transform">The transform to estimate.</param>
    /// <returns>The square root of the absolute value of the transform's linear determinant.</returns>
    /// <remarks>
    ///     A deliberate simplification: under a non-uniform-scale or skewed transform, a
    ///     mathematically correct stroke outline would need to be generated in local space (where
    ///     the stroke width is defined) and then transformed, rather than transformed first and
    ///     stroked with a single scalar width second. This codec always does the latter, matching
    ///     this class's "bake every transform into final pixel-space points" design - visually
    ///     reasonable for the common case of uniform-scale-only transforms, but not exact for
    ///     skewed or non-uniformly scaled ones.
    /// </remarks>
    private static float EstimateUniformScale(Matrix3x2 transform) =>
        MathF.Sqrt(MathF.Abs((transform.M11 * transform.M22) - (transform.M12 * transform.M21)));

    /// <summary>Scales every entry of a dash array by <paramref name="scale"/>.</summary>
    /// <param name="dashArray">The local-space dash array, or <see langword="null"/> for a solid stroke.</param>
    /// <param name="scale">The local-to-pixel-space scale factor.</param>
    /// <returns>The scaled dash array, or <see langword="null"/> if <paramref name="dashArray"/> is <see langword="null"/>.</returns>
    private static IReadOnlyList<float>? ScaleDashArray(IReadOnlyList<float>? dashArray, float scale)
    {
        if (dashArray == null)
        {
            return null;
        }

        var scaled = new float[dashArray.Count];
        for (var i = 0; i < dashArray.Count; i++)
        {
            scaled[i] = dashArray[i] * scale;
        }

        return scaled;
    }

    // ================================================================================================
    // Paint and color resolution
    // ================================================================================================

    /// <summary>
    ///     Resolves a raw <c>fill</c>/<c>stroke</c> presentation-attribute value into a concrete
    ///     paint: a solid <see cref="Rgba32"/> color, a <see cref="Gradient"/>, or
    ///     <see langword="null"/> for "paint nothing".
    /// </summary>
    /// <param name="spec">The raw paint specification (<c>none</c>/color/<c>url(#id)</c>).</param>
    /// <param name="alphaMultiplier">
    ///     The combined opacity multiplier (the relevant <c>fill-opacity</c>/<c>stroke-opacity</c>
    ///     times the cascaded <c>opacity</c> product) folded into a resolved solid color's alpha.
    /// </param>
    /// <param name="localPath">The shape's local-space outline, used as a gradient's object-bounding-box basis.</param>
    /// <param name="transform">The accumulated transform, composed into a resolved gradient's own transform.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <returns>
    ///     A boxed <see cref="Rgba32"/>, a <see cref="Gradient"/>, or <see langword="null"/> - see
    ///     this class's error-handling policy remarks for why an unrecognized color keyword or a
    ///     dangling <c>url(#id)</c> reference tolerantly resolve to <see langword="null"/> rather
    ///     than throwing.
    /// </returns>
    private static object? ResolvePaint(string spec, float alphaMultiplier, Path localPath, Matrix3x2 transform, RenderContext context)
    {
        var trimmed = spec.Trim();
        if (trimmed.Length == 0 || string.Equals(trimmed, "none", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (trimmed.StartsWith("url(", StringComparison.OrdinalIgnoreCase))
        {
            var id = ExtractUrlId(trimmed);
            if (id == null || !context.IdIndex.TryGetValue(id, out var element))
            {
                return null;
            }

            return BuildGradient(element, alphaMultiplier, localPath, transform, context);
        }

        var color = ParseColor(trimmed);
        return color == null ? null : ApplyAlpha(color.Value, alphaMultiplier);
    }

    /// <summary>Extracts the fragment id referenced by a <c>url(#id)</c> paint specification.</summary>
    /// <param name="spec">The raw, already <c>url(</c>-prefixed specification.</param>
    /// <returns>The referenced id, or <see langword="null"/> if the syntax is not a <c>url(#id)</c> reference.</returns>
    private static string? ExtractUrlId(string spec)
    {
        var openIndex = spec.IndexOf('(');
        var closeIndex = spec.LastIndexOf(')');
        if (openIndex < 0 || closeIndex < 0 || closeIndex <= openIndex)
        {
            return null;
        }

        var inner = spec[(openIndex + 1)..closeIndex].Trim().Trim('\'', '"');
        return inner.StartsWith('#') ? inner[1..] : null;
    }

    /// <summary>Applies an alpha multiplier to a color, rounding and clamping the result to a valid byte.</summary>
    /// <param name="color">The base color.</param>
    /// <param name="multiplier">The multiplier to apply to <paramref name="color"/>'s existing alpha.</param>
    /// <returns>The color with its alpha channel scaled.</returns>
    private static Rgba32 ApplyAlpha(Rgba32 color, float multiplier)
    {
        var alpha = (byte)Math.Clamp(MathF.Round(color.A * multiplier), 0, 255);
        return new Rgba32(color.R, color.G, color.B, alpha);
    }

    /// <summary>
    ///     Parses a CSS/SVG color value in any of this codec's supported forms: a named keyword,
    ///     <c>#rgb</c>/<c>#rrggbb</c> hex, or <c>rgb(...)</c>/<c>rgba(...)</c>.
    /// </summary>
    /// <param name="spec">The trimmed, non-empty, non-<c>"none"</c> color text.</param>
    /// <returns>
    ///     The parsed color, or <see langword="null"/> if <paramref name="spec"/> does not match
    ///     any supported form - tolerated as "no paint" rather than an error, since an
    ///     unrecognized color keyword is a well-formed-but-unsupported value, not corrupt data.
    /// </returns>
    private static Rgba32? ParseColor(string spec)
    {
        if (spec.StartsWith('#'))
        {
            return ParseHexColor(spec);
        }

        if (spec.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase) ||
            spec.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase))
        {
            return ParseRgbFunctionColor(spec);
        }

        return NamedColors.TryGetValue(spec, out var color) ? color : null;
    }

    /// <summary>Parses a <c>#rgb</c> or <c>#rrggbb</c> hex color.</summary>
    /// <param name="spec">The full, <c>#</c>-prefixed hex text.</param>
    /// <returns>The parsed color, or <see langword="null"/> if not a recognized hex form.</returns>
    private static Rgba32? ParseHexColor(string spec)
    {
        var hex = spec[1..];
        return hex.Length switch
        {
            3 => ParseShortHexColor(hex),
            6 => ParseLongHexColor(hex),
            _ => null
        };
    }

    /// <summary>Parses a three-digit <c>#rgb</c> hex color, doubling each digit per the CSS shorthand rule.</summary>
    /// <param name="hex">The three hex digits following the <c>#</c>.</param>
    /// <returns>The parsed color, or <see langword="null"/> if any digit is not a valid hex character.</returns>
    private static Rgba32? ParseShortHexColor(string hex)
    {
        if (!TryHexNibble(hex[0], out var r) || !TryHexNibble(hex[1], out var g) || !TryHexNibble(hex[2], out var b))
        {
            return null;
        }

        return new Rgba32((byte)(r * 17), (byte)(g * 17), (byte)(b * 17), 255);
    }

    /// <summary>Parses a six-digit <c>#rrggbb</c> hex color.</summary>
    /// <param name="hex">The six hex digits following the <c>#</c>.</param>
    /// <returns>The parsed color, or <see langword="null"/> if any pair is not a valid hex byte.</returns>
    private static Rgba32? ParseLongHexColor(string hex)
    {
        if (!byte.TryParse(hex.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) ||
            !byte.TryParse(hex.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) ||
            !byte.TryParse(hex.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            return null;
        }

        return new Rgba32(r, g, b, 255);
    }

    /// <summary>Parses a single hexadecimal digit's value.</summary>
    /// <param name="digit">The character to parse.</param>
    /// <param name="value">The digit's value (0-15), if this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> if <paramref name="digit"/> is a valid hex digit.</returns>
    private static bool TryHexNibble(char digit, out int value)
    {
        value = digit switch
        {
            >= '0' and <= '9' => digit - '0',
            >= 'a' and <= 'f' => digit - 'a' + 10,
            >= 'A' and <= 'F' => digit - 'A' + 10,
            _ => -1
        };

        return value >= 0;
    }

    /// <summary>Parses an <c>rgb(r, g, b)</c> or <c>rgba(r, g, b, a)</c> functional color.</summary>
    /// <param name="spec">The full, already <c>rgb(</c>/<c>rgba(</c>-prefixed text.</param>
    /// <returns>The parsed color, or <see langword="null"/> if the syntax is not recognized.</returns>
    private static Rgba32? ParseRgbFunctionColor(string spec)
    {
        var openIndex = spec.IndexOf('(');
        var closeIndex = spec.LastIndexOf(')');
        if (openIndex < 0 || closeIndex < 0 || closeIndex <= openIndex)
        {
            return null;
        }

        var parts = spec[(openIndex + 1)..closeIndex]
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is not (3 or 4))
        {
            return null;
        }

        var r = ParseColorChannel(parts[0]);
        var g = ParseColorChannel(parts[1]);
        var b = ParseColorChannel(parts[2]);
        if (r == null || g == null || b == null)
        {
            return null;
        }

        var alpha = 255;
        if (parts.Length == 4)
        {
            var a = ParsePercentOrNumber(parts[3], 1f);
            if (a == null)
            {
                return null;
            }

            alpha = (int)Math.Clamp(MathF.Round(a.Value * 255f), 0, 255);
        }

        return new Rgba32(r.Value, g.Value, b.Value, (byte)alpha);
    }

    /// <summary>Parses one <c>rgb()</c>/<c>rgba()</c> color channel (a bare 0-255 number or a percentage).</summary>
    /// <param name="token">The raw, trimmed channel text.</param>
    /// <returns>The channel's byte value, or <see langword="null"/> if not a recognizable number/percentage.</returns>
    private static byte? ParseColorChannel(string token)
    {
        var value = ParsePercentOrNumber(token, 255f);
        return value == null ? null : (byte)Math.Clamp(MathF.Round(value.Value), 0, 255);
    }

    /// <summary>
    ///     Parses either a bare number or a CSS percentage (resolved against <paramref name="basis"/>).
    /// </summary>
    /// <param name="token">The raw, trimmed text.</param>
    /// <param name="basis">The value a <c>100%</c> percentage resolves to.</param>
    /// <returns>
    ///     The resolved value, or <see langword="null"/> if not a recognizable number/percentage,
    ///     or if it resolves to a non-finite value (<c>NaN</c>/<c>Infinity</c>).
    /// </returns>
    private static float? ParsePercentOrNumber(string token, float basis)
    {
        var trimmed = token.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        float value;
        if (trimmed.EndsWith('%'))
        {
            if (!float.TryParse(trimmed[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var pct))
            {
                return null;
            }

            value = pct / 100f * basis;
        }
        else if (!float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return null;
        }

        // Reject a non-finite result (NaN/Infinity) the same way as an unparseable token: it is
        // syntactically a valid float but never a meaningful coordinate/channel/offset value -
        // see ParseCoordinate's identical rule for the throwing (non-nullable) counterpart of
        // this method
        return float.IsFinite(value) ? value : null;
    }

    // ================================================================================================
    // Named CSS/SVG color keyword table
    // ================================================================================================

    /// <summary>
    ///     The full CSS Color Module Level 3 "extended color keywords" table (the X11 color names
    ///     supported by mainstream browsers, identical to the original SVG 1.0 color keyword list),
    ///     plus <c>transparent</c>. Looked up case-insensitively, matching the CSS specification's
    ///     "ASCII case-insensitive" keyword-matching rule.
    /// </summary>
    private static readonly Dictionary<string, Rgba32> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["aliceblue"] = new Rgba32(0xF0, 0xF8, 0xFF, 0xFF),
        ["antiquewhite"] = new Rgba32(0xFA, 0xEB, 0xD7, 0xFF),
        ["aqua"] = new Rgba32(0x00, 0xFF, 0xFF, 0xFF),
        ["aquamarine"] = new Rgba32(0x7F, 0xFF, 0xD4, 0xFF),
        ["azure"] = new Rgba32(0xF0, 0xFF, 0xFF, 0xFF),
        ["beige"] = new Rgba32(0xF5, 0xF5, 0xDC, 0xFF),
        ["bisque"] = new Rgba32(0xFF, 0xE4, 0xC4, 0xFF),
        ["black"] = new Rgba32(0x00, 0x00, 0x00, 0xFF),
        ["blanchedalmond"] = new Rgba32(0xFF, 0xEB, 0xCD, 0xFF),
        ["blue"] = new Rgba32(0x00, 0x00, 0xFF, 0xFF),
        ["blueviolet"] = new Rgba32(0x8A, 0x2B, 0xE2, 0xFF),
        ["brown"] = new Rgba32(0xA5, 0x2A, 0x2A, 0xFF),
        ["burlywood"] = new Rgba32(0xDE, 0xB8, 0x87, 0xFF),
        ["cadetblue"] = new Rgba32(0x5F, 0x9E, 0xA0, 0xFF),
        ["chartreuse"] = new Rgba32(0x7F, 0xFF, 0x00, 0xFF),
        ["chocolate"] = new Rgba32(0xD2, 0x69, 0x1E, 0xFF),
        ["coral"] = new Rgba32(0xFF, 0x7F, 0x50, 0xFF),
        ["cornflowerblue"] = new Rgba32(0x64, 0x95, 0xED, 0xFF),
        ["cornsilk"] = new Rgba32(0xFF, 0xF8, 0xDC, 0xFF),
        ["crimson"] = new Rgba32(0xDC, 0x14, 0x3C, 0xFF),
        ["cyan"] = new Rgba32(0x00, 0xFF, 0xFF, 0xFF),
        ["darkblue"] = new Rgba32(0x00, 0x00, 0x8B, 0xFF),
        ["darkcyan"] = new Rgba32(0x00, 0x8B, 0x8B, 0xFF),
        ["darkgoldenrod"] = new Rgba32(0xB8, 0x86, 0x0B, 0xFF),
        ["darkgray"] = new Rgba32(0xA9, 0xA9, 0xA9, 0xFF),
        ["darkgreen"] = new Rgba32(0x00, 0x64, 0x00, 0xFF),
        ["darkgrey"] = new Rgba32(0xA9, 0xA9, 0xA9, 0xFF),
        ["darkkhaki"] = new Rgba32(0xBD, 0xB7, 0x6B, 0xFF),
        ["darkmagenta"] = new Rgba32(0x8B, 0x00, 0x8B, 0xFF),
        ["darkolivegreen"] = new Rgba32(0x55, 0x6B, 0x2F, 0xFF),
        ["darkorange"] = new Rgba32(0xFF, 0x8C, 0x00, 0xFF),
        ["darkorchid"] = new Rgba32(0x99, 0x32, 0xCC, 0xFF),
        ["darkred"] = new Rgba32(0x8B, 0x00, 0x00, 0xFF),
        ["darksalmon"] = new Rgba32(0xE9, 0x96, 0x7A, 0xFF),
        ["darkseagreen"] = new Rgba32(0x8F, 0xBC, 0x8F, 0xFF),
        ["darkslateblue"] = new Rgba32(0x48, 0x3D, 0x8B, 0xFF),
        ["darkslategray"] = new Rgba32(0x2F, 0x4F, 0x4F, 0xFF),
        ["darkslategrey"] = new Rgba32(0x2F, 0x4F, 0x4F, 0xFF),
        ["darkturquoise"] = new Rgba32(0x00, 0xCE, 0xD1, 0xFF),
        ["darkviolet"] = new Rgba32(0x94, 0x00, 0xD3, 0xFF),
        ["deeppink"] = new Rgba32(0xFF, 0x14, 0x93, 0xFF),
        ["deepskyblue"] = new Rgba32(0x00, 0xBF, 0xFF, 0xFF),
        ["dimgray"] = new Rgba32(0x69, 0x69, 0x69, 0xFF),
        ["dimgrey"] = new Rgba32(0x69, 0x69, 0x69, 0xFF),
        ["dodgerblue"] = new Rgba32(0x1E, 0x90, 0xFF, 0xFF),
        ["firebrick"] = new Rgba32(0xB2, 0x22, 0x22, 0xFF),
        ["floralwhite"] = new Rgba32(0xFF, 0xFA, 0xF0, 0xFF),
        ["forestgreen"] = new Rgba32(0x22, 0x8B, 0x22, 0xFF),
        ["fuchsia"] = new Rgba32(0xFF, 0x00, 0xFF, 0xFF),
        ["gainsboro"] = new Rgba32(0xDC, 0xDC, 0xDC, 0xFF),
        ["ghostwhite"] = new Rgba32(0xF8, 0xF8, 0xFF, 0xFF),
        ["gold"] = new Rgba32(0xFF, 0xD7, 0x00, 0xFF),
        ["goldenrod"] = new Rgba32(0xDA, 0xA5, 0x20, 0xFF),
        ["gray"] = new Rgba32(0x80, 0x80, 0x80, 0xFF),
        ["green"] = new Rgba32(0x00, 0x80, 0x00, 0xFF),
        ["greenyellow"] = new Rgba32(0xAD, 0xFF, 0x2F, 0xFF),
        ["grey"] = new Rgba32(0x80, 0x80, 0x80, 0xFF),
        ["honeydew"] = new Rgba32(0xF0, 0xFF, 0xF0, 0xFF),
        ["hotpink"] = new Rgba32(0xFF, 0x69, 0xB4, 0xFF),
        ["indianred"] = new Rgba32(0xCD, 0x5C, 0x5C, 0xFF),
        ["indigo"] = new Rgba32(0x4B, 0x00, 0x82, 0xFF),
        ["ivory"] = new Rgba32(0xFF, 0xFF, 0xF0, 0xFF),
        ["khaki"] = new Rgba32(0xF0, 0xE6, 0x8C, 0xFF),
        ["lavender"] = new Rgba32(0xE6, 0xE6, 0xFA, 0xFF),
        ["lavenderblush"] = new Rgba32(0xFF, 0xF0, 0xF5, 0xFF),
        ["lawngreen"] = new Rgba32(0x7C, 0xFC, 0x00, 0xFF),
        ["lemonchiffon"] = new Rgba32(0xFF, 0xFA, 0xCD, 0xFF),
        ["lightblue"] = new Rgba32(0xAD, 0xD8, 0xE6, 0xFF),
        ["lightcoral"] = new Rgba32(0xF0, 0x80, 0x80, 0xFF),
        ["lightcyan"] = new Rgba32(0xE0, 0xFF, 0xFF, 0xFF),
        ["lightgoldenrodyellow"] = new Rgba32(0xFA, 0xFA, 0xD2, 0xFF),
        ["lightgray"] = new Rgba32(0xD3, 0xD3, 0xD3, 0xFF),
        ["lightgreen"] = new Rgba32(0x90, 0xEE, 0x90, 0xFF),
        ["lightgrey"] = new Rgba32(0xD3, 0xD3, 0xD3, 0xFF),
        ["lightpink"] = new Rgba32(0xFF, 0xB6, 0xC1, 0xFF),
        ["lightsalmon"] = new Rgba32(0xFF, 0xA0, 0x7A, 0xFF),
        ["lightseagreen"] = new Rgba32(0x20, 0xB2, 0xAA, 0xFF),
        ["lightskyblue"] = new Rgba32(0x87, 0xCE, 0xFA, 0xFF),
        ["lightslategray"] = new Rgba32(0x77, 0x88, 0x99, 0xFF),
        ["lightslategrey"] = new Rgba32(0x77, 0x88, 0x99, 0xFF),
        ["lightsteelblue"] = new Rgba32(0xB0, 0xC4, 0xDE, 0xFF),
        ["lightyellow"] = new Rgba32(0xFF, 0xFF, 0xE0, 0xFF),
        ["lime"] = new Rgba32(0x00, 0xFF, 0x00, 0xFF),
        ["limegreen"] = new Rgba32(0x32, 0xCD, 0x32, 0xFF),
        ["linen"] = new Rgba32(0xFA, 0xF0, 0xE6, 0xFF),
        ["magenta"] = new Rgba32(0xFF, 0x00, 0xFF, 0xFF),
        ["maroon"] = new Rgba32(0x80, 0x00, 0x00, 0xFF),
        ["mediumaquamarine"] = new Rgba32(0x66, 0xCD, 0xAA, 0xFF),
        ["mediumblue"] = new Rgba32(0x00, 0x00, 0xCD, 0xFF),
        ["mediumorchid"] = new Rgba32(0xBA, 0x55, 0xD3, 0xFF),
        ["mediumpurple"] = new Rgba32(0x93, 0x70, 0xDB, 0xFF),
        ["mediumseagreen"] = new Rgba32(0x3C, 0xB3, 0x71, 0xFF),
        ["mediumslateblue"] = new Rgba32(0x7B, 0x68, 0xEE, 0xFF),
        ["mediumspringgreen"] = new Rgba32(0x00, 0xFA, 0x9A, 0xFF),
        ["mediumturquoise"] = new Rgba32(0x48, 0xD1, 0xCC, 0xFF),
        ["mediumvioletred"] = new Rgba32(0xC7, 0x15, 0x85, 0xFF),
        ["midnightblue"] = new Rgba32(0x19, 0x19, 0x70, 0xFF),
        ["mintcream"] = new Rgba32(0xF5, 0xFF, 0xFA, 0xFF),
        ["mistyrose"] = new Rgba32(0xFF, 0xE4, 0xE1, 0xFF),
        ["moccasin"] = new Rgba32(0xFF, 0xE4, 0xB5, 0xFF),
        ["navajowhite"] = new Rgba32(0xFF, 0xDE, 0xAD, 0xFF),
        ["navy"] = new Rgba32(0x00, 0x00, 0x80, 0xFF),
        ["oldlace"] = new Rgba32(0xFD, 0xF5, 0xE6, 0xFF),
        ["olive"] = new Rgba32(0x80, 0x80, 0x00, 0xFF),
        ["olivedrab"] = new Rgba32(0x6B, 0x8E, 0x23, 0xFF),
        ["orange"] = new Rgba32(0xFF, 0xA5, 0x00, 0xFF),
        ["orangered"] = new Rgba32(0xFF, 0x45, 0x00, 0xFF),
        ["orchid"] = new Rgba32(0xDA, 0x70, 0xD6, 0xFF),
        ["palegoldenrod"] = new Rgba32(0xEE, 0xE8, 0xAA, 0xFF),
        ["palegreen"] = new Rgba32(0x98, 0xFB, 0x98, 0xFF),
        ["paleturquoise"] = new Rgba32(0xAF, 0xEE, 0xEE, 0xFF),
        ["palevioletred"] = new Rgba32(0xDB, 0x70, 0x93, 0xFF),
        ["papayawhip"] = new Rgba32(0xFF, 0xEF, 0xD5, 0xFF),
        ["peachpuff"] = new Rgba32(0xFF, 0xDA, 0xB9, 0xFF),
        ["peru"] = new Rgba32(0xCD, 0x85, 0x3F, 0xFF),
        ["pink"] = new Rgba32(0xFF, 0xC0, 0xCB, 0xFF),
        ["plum"] = new Rgba32(0xDD, 0xA0, 0xDD, 0xFF),
        ["powderblue"] = new Rgba32(0xB0, 0xE0, 0xE6, 0xFF),
        ["purple"] = new Rgba32(0x80, 0x00, 0x80, 0xFF),
        ["rebeccapurple"] = new Rgba32(0x66, 0x33, 0x99, 0xFF),
        ["red"] = new Rgba32(0xFF, 0x00, 0x00, 0xFF),
        ["rosybrown"] = new Rgba32(0xBC, 0x8F, 0x8F, 0xFF),
        ["royalblue"] = new Rgba32(0x41, 0x69, 0xE1, 0xFF),
        ["saddlebrown"] = new Rgba32(0x8B, 0x45, 0x13, 0xFF),
        ["salmon"] = new Rgba32(0xFA, 0x80, 0x72, 0xFF),
        ["sandybrown"] = new Rgba32(0xF4, 0xA4, 0x60, 0xFF),
        ["seagreen"] = new Rgba32(0x2E, 0x8B, 0x57, 0xFF),
        ["seashell"] = new Rgba32(0xFF, 0xF5, 0xEE, 0xFF),
        ["sienna"] = new Rgba32(0xA0, 0x52, 0x2D, 0xFF),
        ["silver"] = new Rgba32(0xC0, 0xC0, 0xC0, 0xFF),
        ["skyblue"] = new Rgba32(0x87, 0xCE, 0xEB, 0xFF),
        ["slateblue"] = new Rgba32(0x6A, 0x5A, 0xCD, 0xFF),
        ["slategray"] = new Rgba32(0x70, 0x80, 0x90, 0xFF),
        ["slategrey"] = new Rgba32(0x70, 0x80, 0x90, 0xFF),
        ["snow"] = new Rgba32(0xFF, 0xFA, 0xFA, 0xFF),
        ["springgreen"] = new Rgba32(0x00, 0xFF, 0x7F, 0xFF),
        ["steelblue"] = new Rgba32(0x46, 0x82, 0xB4, 0xFF),
        ["tan"] = new Rgba32(0xD2, 0xB4, 0x8C, 0xFF),
        ["teal"] = new Rgba32(0x00, 0x80, 0x80, 0xFF),
        ["thistle"] = new Rgba32(0xD8, 0xBF, 0xD8, 0xFF),
        ["tomato"] = new Rgba32(0xFF, 0x63, 0x47, 0xFF),
        ["transparent"] = new Rgba32(0x00, 0x00, 0x00, 0x00),
        ["turquoise"] = new Rgba32(0x40, 0xE0, 0xD0, 0xFF),
        ["violet"] = new Rgba32(0xEE, 0x82, 0xEE, 0xFF),
        ["wheat"] = new Rgba32(0xF5, 0xDE, 0xB3, 0xFF),
        ["white"] = new Rgba32(0xFF, 0xFF, 0xFF, 0xFF),
        ["whitesmoke"] = new Rgba32(0xF5, 0xF5, 0xF5, 0xFF),
        ["yellow"] = new Rgba32(0xFF, 0xFF, 0x00, 0xFF),
        ["yellowgreen"] = new Rgba32(0x9A, 0xCD, 0x32, 0xFF)
    };

    // ================================================================================================
    // Gradient resolution
    // ================================================================================================

    /// <summary>
    ///     Resolves a <c>linearGradient</c>/<c>radialGradient</c> element (reached via a
    ///     <c>fill="url(#id)"</c>/<c>stroke="url(#id)"</c> reference) into a concrete
    ///     <see cref="Gradient"/>.
    /// </summary>
    /// <param name="element">The gradient element referenced by id.</param>
    /// <param name="alphaMultiplier">The combined opacity multiplier folded into every stop's alpha.</param>
    /// <param name="localPath">The shape's local-space outline, used as the object-bounding-box basis.</param>
    /// <param name="elementTransform">The accumulated transform from local space into pixel space.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <returns>
    ///     The resolved <see cref="Gradient"/>, or <see langword="null"/> if it resolves to zero
    ///     stops (tolerated as "no paint"), <paramref name="element"/> is not itself a
    ///     <c>linearGradient</c>/<c>radialGradient</c>, or its composed
    ///     <c>gradientTransform</c>/bounding-box/<paramref name="elementTransform"/> product
    ///     overflows to a non-finite value (also tolerated as "no paint").
    /// </returns>
    /// <remarks>
    ///     <paramref name="element"/>'s own <c>gradientUnits</c>/<c>gradientTransform</c>/
    ///     <c>spreadMethod</c>/coordinate attributes are always read directly from
    ///     <paramref name="element"/> itself, never inherited through an <c>href</c> chain -
    ///     only color stops are template-inherited (see <see cref="ResolveGradientStops"/>). This
    ///     is a deliberate, bounded simplification of the full SVG href-inheritance model,
    ///     documented as an out-of-scope limitation. This composition is independent of, and not
    ///     subsumed by, <see cref="RenderElement"/>'s own composed-transform finiteness check: an
    ///     extreme-but-individually-finite shape bounding box combined with a
    ///     <c>gradientTransform</c> can overflow this method's own product even when
    ///     <paramref name="elementTransform"/> alone is finite.
    /// </remarks>
    private static object? BuildGradient(
        XElement element,
        float alphaMultiplier,
        Path localPath,
        Matrix3x2 elementTransform,
        RenderContext context)
    {
        var stops = ApplyAlphaToStops(ResolveGradientStops(element, context), alphaMultiplier);
        if (stops.Count == 0)
        {
            return null;
        }

        var isUserSpace = string.Equals(
            (string?)element.Attribute("gradientUnits"), "userSpaceOnUse", StringComparison.OrdinalIgnoreCase);
        var bboxMap = isUserSpace ? Matrix3x2.Identity : ComputeObjectBoundingBoxMap(localPath);
        var transform = ParseGradientTransform(element) * bboxMap * elementTransform;

        // A non-finite composed transform cannot meaningfully position this gradient's stops -
        // tolerate it as "no paint", matching ResolvePaint's existing dangling-reference/
        // unrecognized-color convention, rather than letting it reach Gradient's constructor and
        // throw an uncaught ArgumentOutOfRangeException
        if (!IsFiniteTransform(transform))
        {
            return null;
        }

        var spread = ParseSpreadMethod((string?)element.Attribute("spreadMethod"));

        return element.Name.LocalName switch
        {
            "linearGradient" => BuildLinearGradient(element, stops, spread, transform),
            "radialGradient" => BuildRadialGradient(element, stops, spread, transform),
            _ => null
        };
    }

    /// <summary>
    ///     Computes the transform mapping the <c>[0, 1]</c> object-bounding-box fraction space
    ///     onto <paramref name="localPath"/>'s own local-space bounds.
    /// </summary>
    /// <param name="localPath">The shape's local-space outline.</param>
    /// <returns>
    ///     The bounding-box mapping transform, or <see cref="Matrix3x2.Identity"/> if the path's
    ///     bounds are empty or have zero extent on either axis (a degenerate case tolerated rather
    ///     than producing a non-invertible/zero-scale gradient mapping).
    /// </returns>
    private static Matrix3x2 ComputeObjectBoundingBoxMap(Path localPath)
    {
        var bounds = localPath.GetBounds();
        if (bounds.IsEmpty || bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return Matrix3x2.Identity;
        }

        return Matrix3x2.CreateScale(bounds.Width, bounds.Height) * Matrix3x2.CreateTranslation(bounds.X, bounds.Y);
    }

    /// <summary>
    ///     Resolves a gradient element's effective color stops, walking its <c>href</c>/
    ///     <c>xlink:href</c> template-inheritance chain (starting at <paramref name="start"/>
    ///     itself) until an element with at least one <c>stop</c> child is found. The result is
    ///     cached per <paramref name="start"/> element (see
    ///     <see cref="RenderContext.GradientStopCache"/>), so a gradient referenced by many shapes
    ///     has this chain walk performed only once per <c>Load</c> call.
    /// </summary>
    /// <param name="start">The gradient element originally referenced.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <returns>
    ///     The first chain element's own stops (not merged across chain levels), or an empty list
    ///     if the chain ends (a dangling/absent/non-gradient <c>href</c> target) without ever
    ///     finding one with stops.
    /// </returns>
    /// <exception cref="InvalidDataException">Thrown when the chain revisits an element (a cycle).</exception>
    private static List<GradientStop> ResolveGradientStops(XElement start, RenderContext context)
    {
        if (context.GradientStopCache.TryGetValue(start, out var cached))
        {
            return cached;
        }

        var visited = new HashSet<XElement>();
        var current = start;
        while (visited.Add(current))
        {
            var stops = ParseStops(current);
            if (stops.Count > 0)
            {
                context.GradientStopCache[start] = stops;
                return stops;
            }

            var hrefId = GetHrefAttribute(current) is { } href ? ExtractFragmentId(href) : null;
            if (hrefId == null || !context.IdIndex.TryGetValue(hrefId, out var next))
            {
                // Cache the empty-list terminal case too, so a dangling/cycle-free chain with no
                // stops is not re-walked on every future reference to the same starting element
                context.GradientStopCache[start] = stops;
                return stops;
            }

            current = next;
        }

        throw new InvalidDataException("The gradient's href chain contains a cycle.");
    }

    /// <summary>Extracts the fragment id from a bare <c>#id</c>-form <c>href</c> value.</summary>
    /// <param name="raw">The raw <c>href</c>/<c>xlink:href</c> attribute value.</param>
    /// <returns>The referenced id, or <see langword="null"/> if not a <c>#id</c>-form reference.</returns>
    private static string? ExtractFragmentId(string raw) => raw.StartsWith('#') ? raw[1..] : null;

    /// <summary>Parses a gradient element's direct <c>stop</c> children into color stops.</summary>
    /// <param name="gradientElement">The gradient element to inspect.</param>
    /// <returns>
    ///     The parsed stops, in document order, with each stop's effective offset clamped to
    ///     <c>[0, 1]</c> and forced non-decreasing relative to the previous stop, per the SVG
    ///     specification's stop-offset normalization rule.
    /// </returns>
    private static List<GradientStop> ParseStops(XElement gradientElement)
    {
        var stops = new List<GradientStop>();
        var previousOffset = 0f;

        foreach (var stopElement in gradientElement.Elements().Where(e => e.Name.LocalName == "stop"))
        {
            var rawOffset = (string?)stopElement.Attribute("offset");
            var offset = rawOffset == null ? 0f : ParsePercentOrNumber(rawOffset, 1f) ?? 0f;
            offset = Math.Max(previousOffset, Math.Clamp(offset, 0f, 1f));
            previousOffset = offset;

            stops.Add(new GradientStop(offset, ParseStopColor(stopElement)));
        }

        return stops;
    }

    /// <summary>Parses a single <c>stop</c> element's <c>stop-color</c>/<c>stop-opacity</c> into a color.</summary>
    /// <param name="stopElement">The <c>stop</c> element.</param>
    /// <returns>
    ///     The stop's color; <c>stop-color</c> defaults to black and <c>stop-opacity</c> defaults
    ///     to fully opaque, per the SVG specification's initial values. An unrecognized
    ///     <c>stop-color</c> keyword tolerantly falls back to black rather than throwing.
    /// </returns>
    private static Rgba32 ParseStopColor(XElement stopElement)
    {
        var rawColor = (string?)stopElement.Attribute("stop-color");
        var color = rawColor == null ? null : ParseColor(rawColor.Trim());
        var baseColor = color ?? new Rgba32(0, 0, 0, 255);

        var rawOpacity = (string?)stopElement.Attribute("stop-opacity");
        var opacity = rawOpacity == null ? 1f : ParseOpacityValue(rawOpacity);

        return ApplyAlpha(baseColor, opacity);
    }

    /// <summary>Applies an alpha multiplier to every stop's color, preserving each stop's offset.</summary>
    /// <param name="stops">The source stops.</param>
    /// <param name="multiplier">The multiplier to apply to each stop's existing alpha.</param>
    /// <returns>A new list of stops with their colors' alpha scaled.</returns>
    private static List<GradientStop> ApplyAlphaToStops(List<GradientStop> stops, float multiplier)
    {
        var result = new List<GradientStop>(stops.Count);
        foreach (var stop in stops)
        {
            result.Add(new GradientStop(stop.Offset, ApplyAlpha(stop.Color, multiplier)));
        }

        return result;
    }

    /// <summary>Parses a <c>spreadMethod</c> keyword.</summary>
    /// <param name="raw">The attribute's raw value, or <see langword="null"/> if absent.</param>
    /// <returns>
    ///     The matching <see cref="GradientSpread"/>, defaulting to <see cref="GradientSpread.Pad"/>
    ///     for an absent or unrecognized value.
    /// </returns>
    private static GradientSpread ParseSpreadMethod(string? raw) => raw switch
    {
        "reflect" => GradientSpread.Reflect,
        "repeat" => GradientSpread.Repeat,
        _ => GradientSpread.Pad
    };

    /// <summary>
    ///     Reads one gradient-geometry coordinate/percentage attribute (for example <c>x1</c>,
    ///     <c>cx</c>, <c>r</c>), tolerantly falling back to <paramref name="fallback"/> for an
    ///     absent or unparseable value.
    /// </summary>
    /// <param name="element">The gradient element to inspect.</param>
    /// <param name="name">The attribute name to read.</param>
    /// <param name="fallback">The value to use if the attribute is absent or unparseable.</param>
    /// <returns>The resolved coordinate value.</returns>
    /// <remarks>
    ///     A <c>%</c> suffix is always resolved as a fraction of <c>1</c> (matching the
    ///     objectBoundingBox unit convention), even in <c>userSpaceOnUse</c> mode, where a
    ///     percentage should properly resolve against the current viewport - a documented,
    ///     bounded simplification.
    /// </remarks>
    private static float GetGradientCoordinateOrDefault(XElement element, string name, float fallback)
    {
        var raw = (string?)element.Attribute(name);
        return raw == null ? fallback : ParsePercentOrNumber(raw, 1f) ?? fallback;
    }

    /// <summary>
    ///     Reads one gradient <b>radius</b> attribute (<c>r</c> or <c>fr</c>), tolerantly falling
    ///     back to <paramref name="fallback"/> for an absent, unparseable, non-finite, <b>or
    ///     negative</b> value.
    /// </summary>
    /// <remarks>
    ///     <see cref="GetGradientCoordinateOrDefault"/> already tolerates an absent/unparseable/
    ///     non-finite value, but a radius is the only gradient-geometry attribute with an
    ///     additional sign constraint - <see cref="Drawing.RadialGradient"/>'s constructor throws
    ///     <see cref="ArgumentOutOfRangeException"/> for a negative <c>startRadius</c>/<c>endRadius</c>.
    ///     A negative <c>r</c>/<c>fr</c> is syntactically valid (finite) but out of that documented
    ///     contract, so - matching this codec's existing tolerant handling of every other gradient
    ///     coordinate (see <see cref="GetGradientCoordinateOrDefault"/>) rather than aborting the
    ///     whole document over one presentation attribute - it falls back to
    ///     <paramref name="fallback"/> here instead of reaching the constructor and throwing an
    ///     undocumented <see cref="ArgumentOutOfRangeException"/>.
    /// </remarks>
    /// <param name="element">The gradient element to inspect.</param>
    /// <param name="name">The attribute name to read (<c>r</c> or <c>fr</c>).</param>
    /// <param name="fallback">The value to use if the attribute is absent, unparseable, or negative.</param>
    /// <returns>The resolved, non-negative radius value.</returns>
    private static float GetGradientRadiusOrDefault(XElement element, string name, float fallback)
    {
        var value = GetGradientCoordinateOrDefault(element, name, fallback);
        return value >= 0f ? value : fallback;
    }

    /// <summary>Builds a <c>linearGradient</c> element's <see cref="LinearGradient"/>.</summary>
    /// <param name="element">The <c>linearGradient</c> element.</param>
    /// <param name="stops">The resolved, alpha-adjusted color stops.</param>
    /// <param name="spread">The resolved spread method.</param>
    /// <param name="transform">The combined gradient-to-pixel-space transform.</param>
    /// <returns>The built gradient.</returns>
    private static LinearGradient BuildLinearGradient(
        XElement element,
        IReadOnlyList<GradientStop> stops,
        GradientSpread spread,
        Matrix3x2 transform)
    {
        var start = new Vector2(
            GetGradientCoordinateOrDefault(element, "x1", 0f),
            GetGradientCoordinateOrDefault(element, "y1", 0f));
        var end = new Vector2(
            GetGradientCoordinateOrDefault(element, "x2", 1f),
            GetGradientCoordinateOrDefault(element, "y2", 0f));

        return new LinearGradient(start, end, stops, spread, transform);
    }

    /// <summary>Builds a <c>radialGradient</c> element's <see cref="RadialGradient"/>.</summary>
    /// <param name="element">The <c>radialGradient</c> element.</param>
    /// <param name="stops">The resolved, alpha-adjusted color stops.</param>
    /// <param name="spread">The resolved spread method.</param>
    /// <param name="transform">The combined gradient-to-pixel-space transform.</param>
    /// <returns>The built gradient.</returns>
    /// <remarks>
    ///     The focal point (<c>fx</c>/<c>fy</c>) defaults to the end circle's own center
    ///     (<c>cx</c>/<c>cy</c>) per the SVG specification, and the focal radius (<c>fr</c>,
    ///     SVG 2) defaults to zero.
    /// </remarks>
    private static RadialGradient BuildRadialGradient(
        XElement element,
        IReadOnlyList<GradientStop> stops,
        GradientSpread spread,
        Matrix3x2 transform)
    {
        var cx = GetGradientCoordinateOrDefault(element, "cx", 0.5f);
        var cy = GetGradientCoordinateOrDefault(element, "cy", 0.5f);
        var r = GetGradientRadiusOrDefault(element, "r", 0.5f);
        var fx = GetGradientCoordinateOrDefault(element, "fx", cx);
        var fy = GetGradientCoordinateOrDefault(element, "fy", cy);
        var fr = GetGradientRadiusOrDefault(element, "fr", 0f);

        return new RadialGradient(new Vector2(fx, fy), fr, new Vector2(cx, cy), r, stops, spread, transform);
    }

    // ================================================================================================
    // "use" element handling
    // ================================================================================================

    /// <summary>
    ///     Renders a <c>use</c> element by re-rendering its referenced element in place, offset by
    ///     the <c>use</c> element's own <c>x</c>/<c>y</c> translation and cascaded state/transform.
    /// </summary>
    /// <param name="element">The <c>use</c> element.</param>
    /// <param name="state">The cascaded render state at the <c>use</c> element itself.</param>
    /// <param name="transform">The accumulated transform at the <c>use</c> element itself.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="useDepth">The current <c>use</c>-reference nesting depth.</param>
    /// <param name="elementDepth">
    ///     The current recursion depth of the enclosing <see cref="RenderElement"/> call,
    ///     propagated to the re-rendered target so it also contributes toward
    ///     <see cref="MaxElementDepth"/>.
    /// </param>
    /// <param name="totalElements">
    ///     The running total-rendered-elements count, propagated to the re-rendered target so it
    ///     also contributes toward <see cref="MaxTotalRenderedElements"/>.
    /// </param>
    /// <param name="workBudget">The shared geometry-parsing work budget, propagated to the re-rendered target.</param>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="useDepth"/> has already reached <see cref="MaxUseDepth"/>,
    ///     guarding against a reference cycle that would otherwise recurse indefinitely.
    /// </exception>
    /// <remarks>
    ///     A dangling, absent, or malformed <c>href</c>/<c>xlink:href"</c> reference is a tolerant
    ///     no-op (nothing is rendered), consistent with this class's general dangling-reference
    ///     handling elsewhere.
    /// </remarks>
    private static void RenderUse(XElement element, RenderState state, Matrix3x2 transform, RenderContext context, int useDepth, int elementDepth, ref int totalElements, GeometryWorkBudget workBudget)
    {
        if (useDepth >= MaxUseDepth)
        {
            throw new InvalidDataException("Exceeded the maximum <use> reference nesting depth.");
        }

        var hrefId = GetHrefAttribute(element) is { } href ? ExtractFragmentId(href) : null;
        if (hrefId == null || !context.IdIndex.TryGetValue(hrefId, out var target))
        {
            return;
        }

        var offset = new Vector2(GetFloatAttribute(element, "x"), GetFloatAttribute(element, "y"));
        var useTransform = Matrix3x2.CreateTranslation(offset) * transform;
        RenderElement(target, state, useTransform, context, useDepth + 1, elementDepth + 1, ref totalElements, workBudget);
    }

    // ================================================================================================
    // Text rendering
    // ================================================================================================

    /// <summary>
    ///     Renders a <c>text</c> element by laying out its glyphs as one combined local-space
    ///     outline, then rendering it through the same fill/stroke pipeline as any other shape.
    /// </summary>
    /// <param name="element">The <c>text</c> element.</param>
    /// <param name="state">The cascaded render state, supplying <c>font-family</c>/<c>font-size</c>/<c>text-anchor</c>.</param>
    /// <param name="transform">The accumulated transform from local space into pixel space.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="workBudget">
    ///     The shared geometry-parsing work budget (see <see cref="GeometryWorkBudget"/>), charged
    ///     with the text's character count before glyph layout begins so a pathologically long
    ///     run's per-rune outline/kerning work never starts once the budget is exceeded.
    /// </param>
    /// <remarks>
    ///     Silently renders nothing - never throws - when <see cref="RenderContext.Fonts"/> is
    ///     <see langword="null"/>, no entry matches <paramref name="state"/>'s <c>font-family</c>,
    ///     or the element has no text content, per this class's documented tolerant font-lookup
    ///     policy. Nested markup (for example <c>tspan</c>) is not given its own positioning: every
    ///     descendant text node's content is concatenated and laid out as one flat run, a
    ///     documented simplification.
    /// </remarks>
    private static void RenderText(XElement element, RenderState state, Matrix3x2 transform, RenderContext context, GeometryWorkBudget workBudget)
    {
        if (context.Fonts == null)
        {
            return;
        }

        var font = MatchFont(state.FontFamily, context.Fonts);
        if (font == null)
        {
            return;
        }

        var text = element.Value;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        // Charge the text's character count before the expensive per-rune glyph-outline/kerning
        // loop begins, so a pathologically long run throws before that work is spent
        workBudget.Charge(text.Length);

        var origin = new Vector2(GetFloatAttribute(element, "x"), GetFloatAttribute(element, "y"));
        var glyphRunPath = BuildGlyphRunPath(text, font, state, origin);
        RenderShape(glyphRunPath, state, transform, context);
    }

    /// <summary>
    ///     Finds the first font-family name in <paramref name="fontFamily"/>'s comma-separated
    ///     list with a case-insensitive match in <paramref name="fonts"/>.
    /// </summary>
    /// <param name="fontFamily">The raw, possibly comma-separated, possibly quoted <c>font-family</c> value.</param>
    /// <param name="fonts">The caller-supplied font dictionary.</param>
    /// <returns>The matching font, or <see langword="null"/> if none match (or <paramref name="fontFamily"/> is absent/blank).</returns>
    private static TrueTypeFont? MatchFont(string? fontFamily, IReadOnlyDictionary<string, TrueTypeFont> fonts)
    {
        if (string.IsNullOrWhiteSpace(fontFamily))
        {
            return null;
        }

        foreach (var candidate in fontFamily.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var name = candidate.Trim('\'', '"');
            foreach (var (familyName, font) in fonts)
            {
                if (string.Equals(familyName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return font;
                }
            }
        }

        return null;
    }

    /// <summary>
    ///     Lays out <paramref name="text"/> as one combined local-space glyph-outline path,
    ///     starting at <paramref name="origin"/>, applying <paramref name="state"/>'s
    ///     <c>font-size</c>/<c>text-anchor"</c> and the font's own kerning.
    /// </summary>
    /// <param name="text">The text to lay out.</param>
    /// <param name="font">The matched font.</param>
    /// <param name="state">The cascaded render state.</param>
    /// <param name="origin">The text element's <c>x</c>/<c>y</c> anchor position, in local space.</param>
    /// <returns>
    ///     The combined local-space path (empty if every glyph in <paramref name="text"/> has no
    ///     contour data, for example an all-whitespace run).
    /// </returns>
    private static Path BuildGlyphRunPath(string text, TrueTypeFont font, RenderState state, Vector2 origin)
    {
        var scale = state.FontSize / font.UnitsPerEm;
        var totalAdvance = MeasureTextAdvance(text, font, scale);
        var anchorOffset = state.TextAnchor switch
        {
            TextAnchor.Middle => totalAdvance / 2f,
            TextAnchor.End => totalAdvance,
            _ => 0f
        };

        var builder = new PathBuilder();
        var pen = origin.X - anchorOffset;
        int? previousGlyph = null;

        foreach (var rune in text.EnumerateRunes())
        {
            var glyphIndex = font.GetGlyphIndex(rune.Value);
            if (previousGlyph.HasValue)
            {
                pen += font.GetKerning(previousGlyph.Value, glyphIndex) * scale;
            }

            var outline = font.GetGlyphOutline(glyphIndex);
            if (outline.Subpaths.Count > 0)
            {
                // The font's own outline is Y-up, in design units - flipping the y-axis scale
                // maps it into this codec's Y-down pixel-space convention in the same step as
                // applying the font-size scale and positioning the glyph's origin at the pen
                var glyphMatrix = Matrix3x2.CreateScale(scale, -scale) * Matrix3x2.CreateTranslation(pen, origin.Y);
                AppendTransformedPathInto(builder, outline, glyphMatrix);
            }

            pen += font.GetAdvanceWidth(glyphIndex) * scale;
            previousGlyph = glyphIndex;
        }

        return builder.Build();
    }

    /// <summary>Measures a text run's total advance width, including inter-glyph kerning.</summary>
    /// <param name="text">The text to measure.</param>
    /// <param name="font">The font to measure with.</param>
    /// <param name="scale">The font-design-units-to-local-space scale factor.</param>
    /// <returns>The total advance width, in local-space units.</returns>
    private static float MeasureTextAdvance(string text, TrueTypeFont font, float scale)
    {
        var total = 0f;
        int? previousGlyph = null;

        foreach (var rune in text.EnumerateRunes())
        {
            var glyphIndex = font.GetGlyphIndex(rune.Value);
            if (previousGlyph.HasValue)
            {
                total += font.GetKerning(previousGlyph.Value, glyphIndex) * scale;
            }

            total += font.GetAdvanceWidth(glyphIndex) * scale;
            previousGlyph = glyphIndex;
        }

        return total;
    }

    // ================================================================================================
    // Numeric and attribute parsing helpers
    // ================================================================================================

    /// <summary>Reads a required numeric attribute, defaulting to <paramref name="defaultValue"/> if absent.</summary>
    /// <param name="element">The element to inspect.</param>
    /// <param name="name">The attribute name to read.</param>
    /// <param name="defaultValue">The value to use if the attribute is absent. Defaults to <c>0</c>.</param>
    /// <returns>The parsed value.</returns>
    /// <exception cref="FormatException">Thrown when the attribute is present but not a valid number/percentage.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the attribute is present but parses to a non-finite value, or carries a
    ///     percentage suffix - see <see cref="ParseGeometryCoordinate"/>.
    /// </exception>
    private static float GetFloatAttribute(XElement element, string name, float defaultValue = 0f)
    {
        var raw = (string?)element.Attribute(name);
        return raw == null ? defaultValue : ParseGeometryCoordinate(raw, name);
    }

    /// <summary>Reads an optional numeric attribute, distinguishing "absent" from any parsed value.</summary>
    /// <param name="element">The element to inspect.</param>
    /// <param name="name">The attribute name to read.</param>
    /// <returns>The parsed value, or <see langword="null"/> if the attribute is absent.</returns>
    /// <exception cref="FormatException">Thrown when the attribute is present but not a valid number/percentage.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the attribute is present but parses to a non-finite value, or carries a
    ///     percentage suffix - see <see cref="ParseGeometryCoordinate"/>.
    /// </exception>
    private static float? GetOptionalFloat(XElement element, string name)
    {
        var raw = (string?)element.Attribute(name);
        return raw == null ? null : ParseGeometryCoordinate(raw, name);
    }

    /// <summary>
    ///     Parses a shape/text geometry attribute's numeric value, rejecting a percentage suffix
    ///     because this codec has no defined viewport-relative basis to resolve it against -
    ///     unlike opacity-family attributes (<see cref="ParseOpacityValue"/>), which are correctly
    ///     basis-1 percentages of a <c>[0, 1]</c> range, and gradient coordinates/<c>stop</c>
    ///     <c>offset</c> (<see cref="ParsePercentOrNumber"/>), which are correctly resolved as
    ///     basis-1 fractions of the gradient's own coordinate space - both of which remain
    ///     unaffected by, and must continue to work exactly as before, this rejection.
    /// </summary>
    /// <param name="raw">The raw attribute text.</param>
    /// <param name="attributeName">The attribute's name, used only for the exception message.</param>
    /// <returns>The parsed value.</returns>
    /// <exception cref="FormatException">Propagates from <see cref="ParseCoordinate"/>.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="raw"/> ends with a percentage suffix - this codec has no
    ///     defined viewport-relative basis for a shape/text geometry attribute - or parses to a
    ///     non-finite value, see <see cref="ParseCoordinate"/>.
    /// </exception>
    private static float ParseGeometryCoordinate(string raw, string attributeName)
    {
        var trimmed = raw.Trim();
        if (trimmed.EndsWith('%'))
        {
            throw new InvalidDataException(
                $"The '{attributeName}' attribute's percentage value '{raw}' is not supported: " +
                "SvgCodec has no defined viewport-relative basis for shape/text geometry attributes.");
        }

        return ParseCoordinate(trimmed, percentageBasis: 1f);
    }

    /// <summary>
    ///     The maximum absolute magnitude a single coordinate/length value parsed by
    ///     <see cref="ParseCoordinate"/> or <see cref="TryReadNumber"/> may have. Both methods
    ///     already reject a non-finite (<c>NaN</c>/<c>Infinity</c>) parsed value, but an
    ///     extreme-but-individually-finite value (for example <c>3e38</c>) can still drive
    ///     downstream arithmetic - <see cref="Geometry.BezierFlattening"/>'s per-curve recursive
    ///     subdivision, and <see cref="Geometry.SvgArcConverter"/>'s ellipse-center calculation -
    ///     far out of proportion to the command-count-based <see cref="GeometryWorkBudget"/> that
    ///     is supposed to bound total parsing/rendering work, since that budget counts parsed
    ///     commands, not the real cost a single extreme coordinate can still cause downstream.
    ///     <c>1,000,000</c> is roughly 100 times the largest real-world coordinate magnitude any
    ///     fixture in this repository's test suite uses, the same "generous but bounded" order-of-
    ///     magnitude spirit as <see cref="MaxDocumentCharacters"/>/<see cref="MaxNumberListLength"/>,
    ///     while keeping every downstream consumer's worst-case cost small in practice.
    /// </summary>
    private const float MaxCoordinateMagnitude = 1_000_000f;

    /// <summary>
    ///     Strictly parses a single coordinate/length/opacity-style numeric attribute value,
    ///     resolving a trailing <c>%</c> against <paramref name="percentageBasis"/>.
    /// </summary>
    /// <param name="raw">The raw attribute text.</param>
    /// <param name="percentageBasis">The value a <c>100%</c> percentage resolves to.</param>
    /// <returns>The resolved value.</returns>
    /// <exception cref="FormatException">
    ///     Thrown when <paramref name="raw"/> (with any <c>%</c> suffix stripped) is not a valid
    ///     number - propagates uncaught to this class's top-level <c>Load</c>/<c>GetInfo</c>
    ///     boundary, which rewraps it as <see cref="InvalidDataException"/>.
    /// </exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="raw"/> parses to a non-finite value (<c>NaN</c>,
    ///     <c>Infinity</c>, or <c>-Infinity</c>) - such a value is syntactically a valid float but
    ///     is never a meaningful coordinate/length/opacity, and would otherwise silently propagate
    ///     into rendering or reported image size - or a finite value whose absolute magnitude
    ///     exceeds <see cref="MaxCoordinateMagnitude"/>.
    /// </exception>
    private static float ParseCoordinate(string raw, float percentageBasis)
    {
        var trimmed = raw.Trim();
        var value = trimmed.EndsWith('%')
            ? float.Parse(trimmed[..^1], NumberStyles.Float, CultureInfo.InvariantCulture) / 100f * percentageBasis
            : float.Parse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture);

        // Reject NaN/Infinity here rather than letting them silently propagate: they are valid
        // float literals but never a meaningful coordinate/length/opacity value
        if (!float.IsFinite(value))
        {
            throw new InvalidDataException($"The numeric value '{raw}' is not a finite number.");
        }

        // Reject an extreme-but-finite magnitude too - see MaxCoordinateMagnitude's own remarks
        // for why a value this large is rejected even though it is not itself non-finite
        if (MathF.Abs(value) > MaxCoordinateMagnitude)
        {
            throw new InvalidDataException($"The numeric value '{raw}' exceeds the maximum supported magnitude.");
        }

        return value;
    }

    /// <summary>
    ///     The maximum number of numbers a single <see cref="ParseNumberList"/> call accepts,
    ///     charged incrementally (per number, not after full materialization) so that a single
    ///     pathological attribute value cannot force an unbounded <see cref="List{T}"/> allocation
    ///     before the excess is detected. Deliberately an order of magnitude below
    ///     <see cref="MaxTotalRenderedElements"/>/<see cref="GeometryWorkBudget.MaxTotalGeometryWork"/>:
    ///     this budget applies to a single attribute value, not a whole document, and a legitimate
    ///     <c>viewBox</c> (exactly 4), <c>matrix(...)</c> (exactly 6), or real-world
    ///     <c>stroke-dasharray</c> (essentially always under a few dozen entries) needs nowhere
    ///     near this many.
    /// </summary>
    private const int MaxNumberListLength = 10_000;

    /// <summary>
    ///     Parses a whitespace/comma-separated list of numbers (used by <c>viewBox</c>,
    ///     transform-function arguments, and <c>stroke-dasharray</c> - <c>points</c> is parsed
    ///     directly by <see cref="ParsePointList"/> instead, so its coordinate pairs can be
    ///     charged against the geometry-parsing work budget incrementally as each is read).
    /// </summary>
    /// <param name="raw">The raw, non-<see langword="null"/> list text (may be blank).</param>
    /// <returns>The parsed numbers, in order; empty if <paramref name="raw"/> is blank.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when non-whitespace/comma content remains that does not form a valid number, or
    ///     when more than <see cref="MaxNumberListLength"/> numbers are present.
    /// </exception>
    private static List<float> ParseNumberList(string raw)
    {
        var numbers = new List<float>();
        var position = 0;
        while (true)
        {
            SkipSeparators(raw, ref position);
            if (position >= raw.Length)
            {
                break;
            }

            if (!TryReadNumber(raw, ref position, out var value))
            {
                throw new InvalidDataException($"Malformed number list: unexpected character at position {position}.");
            }

            numbers.Add(value);

            // Charged incrementally (immediately after each Add, not after the loop completes)
            // so a pathologically long list is rejected before it can force an unbounded
            // allocation, rather than only after fully materializing it.
            if (numbers.Count > MaxNumberListLength)
            {
                throw new InvalidDataException($"Number list exceeds the maximum of {MaxNumberListLength} numbers.");
            }
        }

        return numbers;
    }

    /// <summary>
    ///     Attempts to read a single SVG-syntax number (an optional sign, digits, an optional
    ///     single decimal point, and an optional exponent) starting at <paramref name="position"/>,
    ///     advancing past it on success.
    /// </summary>
    /// <param name="text">The text to read from.</param>
    /// <param name="position">
    ///     The position to start reading at (separators are skipped first), advanced past the
    ///     number on success and left unchanged on failure.
    /// </param>
    /// <param name="value">The parsed number, if this method returns <see langword="true"/>.</param>
    /// <returns>
    ///     <see langword="true"/> if a valid, finite number within <see cref="MaxCoordinateMagnitude"/>
    ///     was read. Returns <see langword="false"/> - without advancing <paramref name="position"/> -
    ///     for a syntactically valid number that overflows to a non-finite <c>float</c> value (for
    ///     example, an exponent large enough to overflow to <c>Infinity</c>), or whose finite
    ///     magnitude exceeds <see cref="MaxCoordinateMagnitude"/>, matching this method's existing
    ///     "malformed token" failure contract.
    /// </returns>
    /// <remarks>
    ///     Stops at a second decimal point rather than treating it as an error, so that a
    ///     concatenated shorthand run such as <c>"0.5.5"</c> (two numbers, <c>0.5</c> and <c>.5</c>,
    ///     with no separator between them - a legal SVG path-data shorthand) is read as two
    ///     separate numbers across two calls, rather than failing on the second decimal point.
    /// </remarks>
    private static bool TryReadNumber(string text, ref int position, out float value)
    {
        SkipSeparators(text, ref position);
        var start = position;
        var scan = position;

        if (scan < text.Length && (text[scan] == '+' || text[scan] == '-'))
        {
            scan++;
        }

        var sawDigit = false;
        var sawDot = false;
        while (scan < text.Length && (char.IsDigit(text[scan]) || (text[scan] == '.' && !sawDot)))
        {
            if (text[scan] == '.')
            {
                sawDot = true;
            }
            else
            {
                sawDigit = true;
            }

            scan++;
        }

        if (!sawDigit)
        {
            value = 0f;
            return false;
        }

        scan = TryConsumeExponent(text, scan);

        // Parse into a local candidate before committing position/value: a syntactically valid
        // token (e.g. an exponent large enough to overflow, such as "1e400") can still parse to a
        // non-finite float, which must be rejected as a failed read without advancing position
        var candidate = float.Parse(text[start..scan], NumberStyles.Float, CultureInfo.InvariantCulture);
        if (!float.IsFinite(candidate))
        {
            value = 0f;
            return false;
        }

        // Reject an extreme-but-finite magnitude too (e.g. "3e38") - see
        // MaxCoordinateMagnitude's own remarks - with the same failed-read contract as the
        // non-finite case above, rather than advancing position and returning a value that would
        // otherwise reach Bezier flattening or arc conversion with a wildly out-of-proportion
        // magnitude relative to any real-world document.
        if (MathF.Abs(candidate) > MaxCoordinateMagnitude)
        {
            value = 0f;
            return false;
        }

        position = scan;
        value = candidate;
        return true;
    }

    /// <summary>
    ///     Consumes a trailing <c>e</c>/<c>E</c> exponent suffix (an optional sign and one or more
    ///     digits) starting at <paramref name="position"/>, if one is present and well-formed.
    /// </summary>
    /// <param name="text">The text to read from.</param>
    /// <param name="position">The position immediately after a number's digits/decimal point.</param>
    /// <returns>
    ///     The position after the exponent suffix, or the original <paramref name="position"/>
    ///     unchanged if no well-formed exponent suffix is present.
    /// </returns>
    private static int TryConsumeExponent(string text, int position)
    {
        if (position >= text.Length || (text[position] != 'e' && text[position] != 'E'))
        {
            return position;
        }

        var scan = position + 1;
        if (scan < text.Length && (text[scan] == '+' || text[scan] == '-'))
        {
            scan++;
        }

        var digitsStart = scan;
        while (scan < text.Length && char.IsDigit(text[scan]))
        {
            scan++;
        }

        return scan > digitsStart ? scan : position;
    }

    /// <summary>
    ///     Advances <paramref name="position"/> past every consecutive whitespace/comma separator
    ///     character (SVG's <c>comma-wsp</c> production).
    /// </summary>
    /// <param name="text">The text to scan.</param>
    /// <param name="position">The position to advance.</param>
    private static void SkipSeparators(string text, ref int position)
    {
        while (position < text.Length && (char.IsWhiteSpace(text[position]) || text[position] == ','))
        {
            position++;
        }
    }
}
