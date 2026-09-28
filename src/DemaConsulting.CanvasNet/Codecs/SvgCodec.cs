// cspell:ignore linecap linejoin dasharray dashoffset miterlimit anchor xlink href
// cspell:ignore Glyf Loca
// cspell:ignore evenodd nonzero viewbox gradientunits gradienttransform spreadmethod
// cspell:ignore userspaceonuse objectboundingbox skewx skewy tspan
// cspell:ignore rasterizing unparseable rrggbb sizeless bbox moveto multiplicatively pillarbox SMIL uncatchable formedness
// cspell:ignore unblurred premult
// cspell:ignore unitless letterboxing
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
///     <c>x</c>/<c>y</c> translation); <c>marker</c> (referenced from a <c>line</c>/
///     <c>polyline</c>/<c>polygon</c>/<c>path</c>'s <c>marker-start</c>/<c>marker-mid</c>/
///     <c>marker-end</c> presentation attributes via <c>url(#id)</c>; supports
///     <c>markerWidth</c>/<c>markerHeight</c>/<c>refX</c>/<c>refY</c>/<c>markerUnits</c>
///     (<c>strokeWidth</c> or <c>userSpaceOnUse</c>)/<c>orient</c> (<c>auto</c>,
///     <c>auto-start-reverse</c>, or a fixed angle in degrees) and an optional <c>viewBox</c>
///     fitted with the same "meet, centered" policy described below; marker content renders with
///     its own fresh presentation-attribute cascade, never the referencing shape's fill/stroke -
///     see this class's <c>RenderMarkers</c>/<c>RenderOneMarker</c> remarks for the documented
///     vertex-placement, orientation-averaging, and multi-subpath simplifications); a
///     <c>rect</c>/<c>circle</c>/<c>ellipse</c>/<c>line</c>/<c>polyline</c>/<c>polygon</c>/
///     <c>path</c>/<c>text</c> element's own <c>filter="url(#id)"</c> presentation attribute
///     (resolved via the same <c>url(#id)</c> dangling-reference tolerance, per element only -
///     never for a <c>g</c>/<c>symbol</c> group, and never for a shape's own marker content),
///     referencing a <c>filter</c> element whose <c>fe*</c> primitive children
///     (<c>feFlood</c>, <c>feGaussianBlur</c>, <c>feOffset</c>, <c>feComposite</c> with
///     <c>operator</c> <c>over</c>/<c>in</c>/<c>out</c>/<c>atop</c>/<c>xor</c>/<c>arithmetic</c>, and <c>feMerge</c>/
///     <c>feMergeNode</c>) are evaluated in document order against an offscreen buffer sized to
///     the filter region, with the <c>SourceGraphic</c> and <c>SourceAlpha</c> implicit inputs
///     supported; the filter region defaults to <c>objectBoundingBox</c>'s standard
///     <c>-10% -10% 120% 120%</c> (each independently overridable via <c>x</c>/<c>y</c>/
///     <c>width</c>/<c>height</c>) - see this class's <c>RenderFilteredShape</c>/
///     <c>EvaluateFilterChain</c> remarks for the documented simplifications; and
///     <c>text</c> (with <c>font-family</c> best-effort
///     matching against a caller-supplied font dictionary, <c>font-size</c>, <c>fill</c>,
///     <c>text-anchor</c>, and, for a caller that registers more than one <see cref="SvgFontFace"/>
///     per family via the <see cref="LoadWithFontFaces(Stream, int, int, IReadOnlyDictionary{string, IReadOnlyList{SvgFontFace}}?)"/>
///     overload, <c>font-weight</c>/<c>font-style</c>-aware closest-face matching - see this
///     class's <c>SelectClosestFace</c> remarks for the matching algorithm); a shape/text
///     element's own <c>clip-path="url(#id)"</c> and <c>mask="url(#id)"</c> presentation
///     attributes (resolved with the same dangling-reference tolerance as <c>filter</c>, and
///     combinable with <c>filter</c> and with each other on the same element, applied in the
///     SVG-defined <c>clip-path</c> &#8594; <c>mask</c> &#8594; <c>filter</c> order - see
///     <c>SvgCodec.ClippingAndMasking.cs</c>'s remarks for the <c>clipPathUnits</c>/
///     <c>maskUnits</c>/<c>maskContentUnits</c> unit-space handling and documented per-feature
///     simplifications); a <c>fill</c>/<c>stroke</c> <c>url(#id)</c> reference to a <c>pattern</c>
///     paint server (with <c>x</c>/<c>y</c>/<c>width</c>/<c>height</c>, <c>patternUnits</c>/
///     <c>patternContentUnits</c>, <c>patternTransform</c>, an <c>href</c>/<c>xlink:href</c>
///     content-inheritance chain, and an optional <c>viewBox</c>/<c>preserveAspectRatio</c> - see
///     <c>SvgCodec.Patterns.cs</c>'s remarks for the documented per-feature simplifications); and
///     an <c>image</c> element (with <c>x</c>/<c>y</c>/<c>width</c>/<c>height</c> placement,
///     <c>preserveAspectRatio</c> fitting, and a base64-encoded <c>data:</c> URI <c>href</c>/
///     <c>xlink:href</c> decoded via this codec's own sibling raster codecs - <see cref="PngCodec"/>,
///     <see cref="JpegCodec"/>, <see cref="BmpCodec"/>, <see cref="TiffCodec"/>, and
///     <see cref="GifCodec"/> - fully integrated with the same <c>filter</c>/<c>clip-path</c>/
///     <c>mask</c>/<c>opacity</c> pipeline every other shape/text element uses; a non-<c>data:</c>
///     href (a file path or URL) and any nested SVG reference (<c>data:image/svg+xml</c> or an
///     external <c>.svg</c> file) are both a deliberate, documented, security-conscious no-op -
///     see <c>SvgCodec.Image.cs</c>'s remarks for the full rationale).
///     </para>
///     <para>
///     <b>Out of scope (silently ignored, per element).</b>
///     SMIL animation (<c>animate</c>/<c>animateTransform</c>/<c>animateMotion</c>/
///     <c>animateColor</c>/<c>set</c>), <c>foreignObject</c>, and nested <c>svg</c> are all
///     well-formed-but-unsupported
///     constructs: encountering one never aborts the document, it is simply skipped, and every
///     other element continues to render normally. Within the supported <c>marker</c> feature itself,
///     <c>markerContentUnits</c> (a rarely-used SVG 2 attribute) is not read, and a marker's
///     <c>overflow</c>/clipping-to-its-own-viewport behavior is not implemented (marker content is
///     never clipped to <c>markerWidth</c>/<c>markerHeight</c>) - both explicitly out of scope.
///     Within the supported <c>filter</c> feature itself, an explicit
///     <c>filterUnits="userSpaceOnUse"</c> is tolerantly ignored and always falls back to the
///     <c>objectBoundingBox</c> default region computation; group-level (<c>g</c>/<c>symbol</c>)
///     filtering and filtering a shape's own marker content are both not implemented (a
///     <c>filter</c> only ever affects the single element it is set on directly); and any
///     primitive type other than <c>feFlood</c>/<c>feGaussianBlur</c>/<c>feOffset</c>/
///     <c>feComposite</c>/<c>feMerge</c>/<c>feColorMatrix</c>/<c>feComponentTransfer</c>/
///     <c>feMorphology</c>/<c>feConvolveMatrix</c>/<c>feDisplacementMap</c>/<c>feTile</c>/
///     <c>feDropShadow</c>/<c>feImage</c>/<c>feDiffuseLighting</c>/<c>feSpecularLighting</c>/
///     <c>feTurbulence</c>/<c>feBlend</c> is a tolerant no-op passthrough of its own
///     input rather than actually implemented. <c>feImage</c>'s element-reference form can
///     recurse back into the ordinary element walk, so filter evaluation reuses the existing
///     <c>MaxUseDepth</c> guard that already bounds <c>use</c>/marker reference depth, declining
///     the nested render once that limit is reached.
///     </para>
///     <para>
///     Within the supported <c>text</c> feature itself, the <c>font-weight</c> relative
///     keywords <c>bolder</c>/<c>lighter</c> (which resolve to a value relative to the
///     inherited weight rather than an absolute one) are not implemented - encountering either
///     keyword is tolerantly treated the same as an absent/unparseable <c>font-weight</c>,
///     falling back to the inherited value rather than throwing or guessing a relative
///     adjustment - and the <c>font-style</c> keyword <c>oblique</c> is not distinguished from
///     <c>italic</c>: both map onto the same <see cref="SvgFontStyle.Italic"/> value (see
///     <see cref="SvgFontStyle"/>'s remarks for the rationale).
///     </para>
///     <para>
///     <b>CSS <c>style</c> element and selector-based styling.</b> A <c>style</c> element's text
///     content is parsed as CSS (honoring its <c>type</c> attribute - only absent or
///     <c>text/css</c> is parsed, any other value is opaque and skipped), matching type
///     (<c>rect</c>), class (<c>.foo</c>, including a space-separated multi-class <c>class</c>
///     attribute), id (<c>#foo</c>), universal (<c>*</c>), and compound (<c>rect.foo</c>)
///     selectors, comma-separated selector lists, and the descendant (whitespace) and child
///     (<c>&gt;</c>) combinators. Sibling (<c>+</c>/<c>~</c>) combinators and pseudo-classes
///     (for example <c>:hover</c>) are not implemented - SVG rendering has no interactive state
///     for a pseudo-class to target, and this codec does not otherwise track the sibling-position
///     data a sibling combinator would need; only the individual unsupported selector is dropped
///     from its comma-separated list, not the whole rule. <c>@media</c>/other at-rules are not
///     implemented, and a trailing <c>!important</c> is tolerantly stripped, applying that
///     declaration's value at ordinary (non-<c>!important</c>) precedence rather than rejecting
///     it. Multiple matching rules for the same property resolve via standard CSS specificity
///     (id/class/type tuple comparison, universal contributing zero), with document order as the
///     tie-breaker at equal specificity; multiple <c>style</c> elements merge into one cascade
///     sharing a single document-order source-order counter, so a later <c>style</c> element's
///     equal-specificity rule outranks an earlier one. Every cascaded property resolves across
///     exactly 3 precedence tiers - a plain presentation attribute (lowest), any matching
///     stylesheet rule (next), and an inline <c>style="..."</c> attribute (highest,
///     unconditionally overriding a matching stylesheet rule regardless of its own specificity) -
///     with every tier's raw value handed to the exact same value parser a plain presentation
///     attribute already used, so no SVG-value-parsing logic is duplicated by the CSS engine. A
///     malformed individual rule or declaration is skipped (resynchronizing at the next
///     <c>}</c>/<c>;</c>) rather than aborting the whole stylesheet or document, and both the
///     number of retained stylesheet rules/selectors/declarations and the cumulative
///     selector-matching work performed against the document are bounded, mirroring this
///     codec's existing <c>GeometryWorkBudget</c>/<c>FilterWorkBudget</c> resource-safety
///     conventions.
///     </para>
///     <para>
///     <b>ViewBox fitting and <c>preserveAspectRatio</c>.</b> <see cref="Load(Stream, int, int, IReadOnlyDictionary{string, TrueTypeFont}?)"/>
///     fits the document's intrinsic user-space size into the caller-requested raster size per the
///     root <c>svg</c> element's own <c>preserveAspectRatio</c> attribute (parsed as
///     <c>[defer] &lt;align&gt; [meet|slice]</c>, with <c>defer</c> parsed and ignored - it is only
///     meaningful for an <c>&lt;image&gt;</c>-referenced external resource's own load ordering, and
///     has no observable effect either way even for a supported <c>data:</c> URI <c>&lt;image&gt;</c>,
///     since this codec decodes it synchronously in place with no separate load-ordering concept
///     at all); when absent, the SVG-defined default is <c>xMidYMid meet</c>, equivalent to CSS
///     <c>object-fit: contain</c>: the content is uniformly scaled as large as possible while
///     remaining fully visible, then centered, leaving transparent letterbox/pillarbox bars on the
///     raster's shorter axis. All ten <c>align</c> values (<c>none</c>, or a cross product of
///     <c>xMin</c>/<c>xMid</c>/<c>xMax</c> and <c>YMin</c>/<c>YMid</c>/<c>YMax</c>) and both
///     <c>meet</c>/<c>slice</c> <c>meetOrSlice</c> values are implemented via one shared fit-transform
///     helper, <c>ComputePreserveAspectRatioFit</c>, also reused by a <c>symbol</c> referenced via
///     <c>use</c> (which establishes its own nested viewport, fitted against the symbol's own
///     <c>viewBox</c> and <c>preserveAspectRatio</c> - new capability) and, for an explicit
///     <c>marker</c>-own <c>preserveAspectRatio</c> attribute only, a <c>marker</c>'s own content
///     fit against its <c>viewBox</c> (a <c>marker</c> with no explicit <c>preserveAspectRatio</c>
///     attribute keeps its original, simpler uniform-scale-about-the-origin fit unchanged, to avoid
///     double-counting a centering offset against its own <c>refX</c>/<c>refY</c> anchoring).
///     </para>
///     <para>
///     <b>Percentage-based geometry.</b> A trailing <c>%</c> on a shape/text geometry attribute
///     (<c>x</c>, <c>y</c>, <c>width</c>, <c>height</c>, <c>rx</c>, <c>ry</c>, <c>cx</c>, <c>cy</c>,
///     <c>r</c>, <c>x1</c>/<c>y1</c>/<c>x2</c>/<c>y2</c>, <c>font-size</c>, <c>stroke-width</c>,
///     <c>stroke-dashoffset</c>, each <c>stroke-dasharray</c> entry, <c>use</c>'s
///     <c>x</c>/<c>y</c>/<c>width</c>/<c>height</c>, and <c>text</c>'s <c>x</c>/<c>y</c>) resolves
///     against the current viewport, per the SVG specification's per-attribute basis rules: a
///     horizontal attribute resolves against the current viewport width; a vertical attribute
///     against the current viewport height; an axis-agnostic length (<c>stroke-width</c>, a
///     circle's <c>r</c>, and each <c>stroke-dasharray</c> entry) against
///     <c>sqrt(width^2 + height^2) / sqrt(2)</c>; and <c>font-size</c> against the parent element's
///     own already-cascaded <c>font-size</c>. The current viewport is carried by the same cascading
///     per-element render state as every other inherited presentation attribute, and is only ever
///     changed by the document root or a <c>symbol</c> referenced via <c>use</c> (see the ViewBox
///     fitting paragraph above); every other element inherits it unchanged. <c>stroke-miterlimit</c>
///     is the sole documented exception: it remains a unitless ratio per the SVG specification, not
///     a length, so a percentage on it is deliberately still rejected as invalid numeric syntax (see
///     below), unchanged from this codec's original behavior. Gradient coordinates on a
///     <c>userSpaceOnUse</c> gradient are a separate, narrower simplification: they are still
///     always resolved as basis-1 fractions of the gradient's own bounding-box-relative coordinate
///     space rather than the current viewport (see this class's <c>GetGradientCoordinateOrDefault</c>
///     remarks) - a bounded, intentionally out-of-scope simplification for this phase, not a defect.
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
///     (including a non-finite <c>NaN</c>/<c>Infinity</c> value), a non-positive <c>viewBox</c>
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
///     walk, which would otherwise stall indefinitely on a non-finite path length. An <c>image</c>
///     element's own malformed base64 payload, or a malformed/truncated/oversized decoded raster
///     payload (each rejected by the matching sibling raster codec's own <c>Load</c>, already
///     enforcing <see cref="Surface.MaxDimension"/> internally), is likewise tolerant: the whole
///     <c>image</c> element is skipped (rendered as if absent) rather than the exception
///     propagating past it - see <c>SvgCodec.Image.cs</c>'s remarks for the full catalogue of
///     tolerant <c>image</c>-specific no-op conditions, including its own href-scope security
///     decisions.
///     </para>
///     <para>
///     <b>Caller-supplied raster dimensions.</b> The <c>width</c>/<c>height</c>
///     parameters of both <c>Load</c> overloads are ordinary API parameters, not untrusted file
///     data: they are passed directly to <see cref="Surface"/>'s constructor and its own
///     <see cref="ArgumentOutOfRangeException"/> is allowed to propagate uncaught, rather than
///     being pre-validated or wrapped as <see cref="InvalidDataException"/>.
///     </para>
/// </remarks>
public static partial class SvgCodec
{
    // ================================================================================================
    // Public API
    // ================================================================================================

    /// <summary>
    ///     Rasterizes an SVG document read from an open, readable stream onto a new
    ///     <see cref="Surface"/> of the requested size, matching each <c>text</c> element's
    ///     cascaded <c>font-family</c>/<c>font-weight</c>/<c>font-style</c> against a
    ///     caller-supplied dictionary of per-family <see cref="SvgFontFace"/> lists.
    /// </summary>
    /// <remarks>
    ///     Named distinctly from the single-font-per-family <see cref="Load(Stream, int, int, IReadOnlyDictionary{string, TrueTypeFont}?)"/>
    ///     overload (rather than sharing the <c>Load</c> name) because both dictionary value types
    ///     would otherwise have identical arity and neither generic instantiation is more specific
    ///     than the other - an explicit <see langword="null"/> literal passed as the 4th positional
    ///     argument would be ambiguous between the two overloads, breaking existing callers who use
    ///     that pattern. Using a distinct name eliminates the ambiguity entirely.
    /// </remarks>
    /// <param name="stream">
    ///     The stream to read the SVG document from. Reading begins at the stream's current
    ///     position and consumes the remainder of the stream.
    /// </param>
    /// <param name="width">The width, in pixels, of the returned surface.</param>
    /// <param name="height">The height, in pixels, of the returned surface.</param>
    /// <param name="fonts">
    ///     An optional dictionary mapping font-family names to the list of <see cref="SvgFontFace"/>
    ///     instances registered for that family, used to render <c>text</c> elements. A
    ///     <see langword="null"/> value, a dictionary with no entry matching a given <c>text</c>
    ///     element's <c>font-family</c>, or a matching entry whose face list is empty, causes that
    ///     element to be silently skipped rather than throwing - see this class's remarks. When a
    ///     family has more than one registered face, the face whose <see cref="SvgFontFace.Weight"/>/
    ///     <see cref="SvgFontFace.Style"/> most closely matches the element's own cascaded
    ///     <c>font-weight</c>/<c>font-style</c> is selected - see <c>SelectClosestFace</c>'s remarks
    ///     for the matching algorithm.
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
    ///     const string svg = "&lt;svg viewBox='0 0 100 100'&gt;&lt;text font-family='Sans' font-weight='bold'&gt;Hi&lt;/text&gt;&lt;/svg&gt;";
    ///     using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(svg));
    ///     var faces = new Dictionary&lt;string, IReadOnlyList&lt;SvgFontFace&gt;&gt;
    ///     {
    ///         ["Sans"] = [new SvgFontFace(regularFont), new SvgFontFace(boldFont, Weight: 700)]
    ///     };
    ///     var surface = SvgCodec.LoadWithFontFaces(stream, 200, 200, faces);
    ///     </code>
    /// </example>
    public static Surface LoadWithFontFaces(
        Stream stream,
        int width,
        int height,
        IReadOnlyDictionary<string, IReadOnlyList<SvgFontFace>>? fonts)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var surface = new Surface(width, height);
        var root = LoadRootElement(stream);

        try
        {
            var (origin, size) = ResolveViewBoxOrSize(root);
            var fitTransform = ComputeFitTransform(root, origin, size, surface.Width, surface.Height);

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
            var stylesheet = BuildStylesheet(root);
            var context = new RenderContext(surface, idIndex, fonts, stylesheet);
            RenderDocument(root, fitTransform, size, context);
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException("The SVG document contains invalid numeric data.", ex);
        }

        return surface;
    }

    /// <summary>
    ///     Rasterizes an SVG document loaded from a file path onto a new <see cref="Surface"/> of
    ///     the requested size. See <see cref="LoadWithFontFaces(Stream, int, int, IReadOnlyDictionary{string, IReadOnlyList{SvgFontFace}}?)"/>
    ///     for the full contract.
    /// </summary>
    /// <param name="path">The path of the SVG file to load. Must not be null, empty, or whitespace.</param>
    /// <param name="width">The width, in pixels, of the returned surface.</param>
    /// <param name="height">The height, in pixels, of the returned surface.</param>
    /// <param name="fonts">
    ///     An optional dictionary mapping font-family names to the list of <see cref="SvgFontFace"/>
    ///     instances registered for that family. See the stream overload's remarks.
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
    public static Surface LoadWithFontFaces(
        string path,
        int width,
        int height,
        IReadOnlyDictionary<string, IReadOnlyList<SvgFontFace>>? fonts)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path must not be empty or whitespace.", nameof(path));
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        return LoadWithFontFaces(stream, width, height, fonts);
    }

    /// <summary>
    ///     Rasterizes an SVG document read from an open, readable stream onto a new
    ///     <see cref="Surface"/> of the requested size, using at most one <see cref="TrueTypeFont"/>
    ///     per font-family.
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
    /// <remarks>
    ///     A thin wrapper delegating to
    ///     <see cref="LoadWithFontFaces(Stream, int, int, IReadOnlyDictionary{string, IReadOnlyList{SvgFontFace}}?)"/>,
    ///     via <c>ToFontFaces</c> wrapping each entry as a single normal-weight/normal-style
    ///     <see cref="SvgFontFace"/> - so every <c>text</c> element always resolves to that single
    ///     registered font, regardless of its own <c>font-weight</c>/<c>font-style</c>, exactly as
    ///     before this overload existed. A caller registering more than one face per family (bold/
    ///     italic variants) should call <see cref="LoadWithFontFaces(Stream, int, int, IReadOnlyDictionary{string, IReadOnlyList{SvgFontFace}}?)"/>
    ///     directly instead. That richer overload is deliberately named differently (rather than
    ///     overloading <c>Load</c> itself) because both dictionary value types have identical
    ///     arity, so an overload sharing this name would make an explicit untyped
    ///     <see langword="null"/> literal passed as the 4th positional argument ambiguous between
    ///     the two - a source-breaking change for existing callers using that pattern.
    /// </remarks>
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
        => LoadWithFontFaces(stream, width, height, ToFontFaces(fonts));

    /// <summary>
    ///     Rasterizes an SVG document loaded from a file path onto a new <see cref="Surface"/> of
    ///     the requested size, using at most one <see cref="TrueTypeFont"/> per font-family. See
    ///     <see cref="Load(Stream, int, int, IReadOnlyDictionary{string, TrueTypeFont}?)"/>
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
    /// <remarks>
    ///     A thin wrapper delegating to
    ///     <see cref="LoadWithFontFaces(string, int, int, IReadOnlyDictionary{string, IReadOnlyList{SvgFontFace}}?)"/>,
    ///     via <c>ToFontFaces</c> - see the <see cref="Stream"/> overload's remarks.
    /// </remarks>
    public static Surface Load(
        string path,
        int width,
        int height,
        IReadOnlyDictionary<string, TrueTypeFont>? fonts = null)
        => LoadWithFontFaces(path, width, height, ToFontFaces(fonts));

    /// <summary>
    ///     Wraps a legacy single-font-per-family dictionary as the richer per-family
    ///     <see cref="SvgFontFace"/>-list shape, each entry becoming a single normal-weight
    ///     (<c>400</c>)/normal-style (<see cref="SvgFontStyle.Normal"/>) face - see the legacy
    ///     <c>Load</c> overloads' remarks.
    /// </summary>
    /// <param name="fonts">The legacy single-font-per-family dictionary, or <see langword="null"/>.</param>
    /// <returns>
    ///     <see langword="null"/> if <paramref name="fonts"/> is <see langword="null"/>; otherwise
    ///     a new dictionary with the same family-name keys, each mapped to a single-element face list.
    /// </returns>
    private static IReadOnlyDictionary<string, IReadOnlyList<SvgFontFace>>? ToFontFaces(IReadOnlyDictionary<string, TrueTypeFont>? fonts)
    {
        if (fonts == null)
        {
            return null;
        }

        var result = new Dictionary<string, IReadOnlyList<SvgFontFace>>(fonts.Count);
        foreach (var (familyName, font) in fonts)
        {
            result[familyName] = new SvgFontFace[] { new(font) };
        }

        return result;
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
    ///     Thrown when the stream is not well-formed XML up to and including the root start-tag,
    ///     its character count exceeds <see cref="MaxDocumentCharacters"/>, its root element is not
    ///     named <c>svg</c>, or its <c>viewBox</c> attribute is present but malformed (not exactly
    ///     four numbers, or non-positive width/height). A malformed or unparseable <c>width</c>/
    ///     <c>height</c> attribute is <b>not</b> included in this list: per this class's GetInfo
    ///     fallback policy remarks, such a value is treated as absent and falls back to the next
    ///     sizing tier (ultimately the 300x150 default size) rather than throwing.
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
    ///     Thrown when the file's content is not well-formed XML up to and including the root
    ///     start-tag, its character count exceeds <see cref="MaxDocumentCharacters"/>, its root
    ///     element is not named <c>svg</c>, or its <c>viewBox</c> attribute is present but
    ///     malformed (not exactly four numbers, or non-positive width/height). A malformed or
    ///     unparseable <c>width</c>/<c>height</c> attribute is <b>not</b> included in this list:
    ///     per this class's GetInfo fallback policy remarks, such a value is treated as absent and
    ///     falls back to the next sizing tier (ultimately the 300x150 default size) rather than
    ///     throwing.
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

}
