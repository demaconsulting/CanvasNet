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
            // own remarks for why this is a raw-size bound, not a full streaming parse.
            // Disabling DTD processing and setting no XML resolver are already .NET's effective
            // defaults for XmlReaderSettings; they are set explicitly below as defense-in-depth
            // documentation clarity, making it explicit (rather than merely implicit) that this
            // parser never processes a DOCTYPE declaration or resolves an external entity/DTD -
            // hardening against XML External Entity (XXE) injection.
            var settings = new XmlReaderSettings
            {
                MaxCharactersInDocument = MaxDocumentCharacters,
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };
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
            // materialize an unbounded amount of data. Disabling DTD processing and setting no
            // XML resolver are already .NET's effective defaults for XmlReaderSettings; they are
            // set explicitly below as defense-in-depth documentation clarity, making it explicit
            // (rather than merely implicit) that this parser never processes a DOCTYPE
            // declaration or resolves an external entity/DTD - hardening against XML External
            // Entity (XXE) injection.
            var settings = new XmlReaderSettings
            {
                MaxCharactersInDocument = MaxDocumentCharacters,
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };
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
    ///     Computes the root <c>svg</c> element's own viewBox-fit transform - fitting its intrinsic
    ///     viewBox origin/size into a raster of the requested pixel dimensions per the root's own
    ///     <c>preserveAspectRatio</c> attribute (default <c>xMidYMid meet</c>, CSS <c>object-fit:
    ///     contain</c> equivalent) - see this class's viewBox-fitting policy remarks.
    /// </summary>
    /// <param name="root">The document's root <c>svg</c> element, whose own <c>preserveAspectRatio</c> attribute is read.</param>
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
    /// <remarks>
    ///     A thin wrapper around the shared <see cref="ComputePreserveAspectRatioFit"/> helper
    ///     (also reused by an explicit <c>marker</c> <c>preserveAspectRatio</c> and a <c>symbol</c>
    ///     referenced via <c>use</c>): with no <c>preserveAspectRatio</c> attribute present, this
    ///     resolves to <see cref="PreserveAspectRatio.Default"/> (<c>xMidYMid meet</c>), reproducing
    ///     this codec's original "meet, centered" root-fit math bit-for-bit - no default-behavior
    ///     change, and no existing fixture/test churn, for the common undecorated case. Only an
    ///     explicit non-default <c>align</c>/<c>meetOrSlice</c> value is new behavior.
    /// </remarks>
    private static Matrix3x2 ComputeFitTransform(XElement root, Vector2 origin, Vector2 size, int rasterWidth, int rasterHeight) =>
        ComputePreserveAspectRatioFit(origin, size, rasterWidth, rasterHeight, GetPreserveAspectRatio(root));
}
