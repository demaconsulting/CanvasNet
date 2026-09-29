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
    // Numeric and attribute parsing helpers
    // ================================================================================================

    /// <summary>
    ///     Identifies which viewport dimension (or other basis) a geometry attribute's trailing
    ///     <c>%</c> suffix resolves against, per the SVG specification's per-attribute percentage
    ///     rules - the resolution basis a bare number literal never needs, since it already has an
    ///     unambiguous absolute meaning.
    /// </summary>
    private enum PercentageAxis
    {
        /// <summary>Resolves against the current viewport width (for example <c>x</c>/<c>width</c>).</summary>
        Horizontal,

        /// <summary>Resolves against the current viewport height (for example <c>y</c>/<c>height</c>).</summary>
        Vertical,

        /// <summary>
        ///     Resolves against <c>sqrt(viewportWidth^2 + viewportHeight^2) / sqrt(2)</c> - the SVG
        ///     specification's defined basis for an axis-agnostic length (for example
        ///     <c>stroke-width</c>, a circle's <c>r</c>, or <c>stroke-dasharray</c> entries).
        /// </summary>
        Diagonal,

        /// <summary>
        ///     Resolves against the parent element's own already-cascaded <c>font-size</c> - the
        ///     CSS/SVG-defined basis for a <c>font-size</c> percentage, distinct from every
        ///     viewport-relative basis above.
        /// </summary>
        FontSize
    }

    /// <summary>
    ///     Computes the "diagonal" percentage basis - <c>sqrt(w^2 + h^2) / sqrt(2)</c> - the SVG
    ///     specification's defined resolution basis for an axis-agnostic length percentage (see
    ///     <see cref="PercentageAxis.Diagonal"/>), shared by every call site that needs it
    ///     (<see cref="ResolvePercentageBasis"/> and <see cref="ParseDashArray"/>) so the formula
    ///     is defined exactly once.
    /// </summary>
    /// <param name="viewportWidth">The current viewport width.</param>
    /// <param name="viewportHeight">The current viewport height.</param>
    /// <returns>The resolved diagonal basis.</returns>
    private static float ComputeDiagonalBasis(float viewportWidth, float viewportHeight) =>
        MathF.Sqrt(viewportWidth * viewportWidth + viewportHeight * viewportHeight) / MathF.Sqrt(2f);

    /// <summary>
    ///     Resolves <paramref name="axis"/>'s concrete percentage basis against
    ///     <paramref name="state"/>'s currently-cascaded viewport size/font-size.
    /// </summary>
    /// <param name="state">The cascaded render state carrying the current viewport/font-size.</param>
    /// <param name="axis">Which basis to resolve.</param>
    /// <returns>The resolved basis a <c>100%</c> value would equal.</returns>
    private static float ResolvePercentageBasis(RenderState state, PercentageAxis axis) => axis switch
    {
        PercentageAxis.Horizontal => state.ViewportWidth,
        PercentageAxis.Vertical => state.ViewportHeight,
        PercentageAxis.Diagonal => ComputeDiagonalBasis(state.ViewportWidth, state.ViewportHeight),
        PercentageAxis.FontSize => state.FontSize,
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Unrecognized percentage axis.")
    };

    /// <summary>Reads a required numeric attribute, defaulting to <paramref name="defaultValue"/> if absent.</summary>
    /// <param name="element">The element to inspect.</param>
    /// <param name="name">The attribute name to read.</param>
    /// <param name="state">The cascaded render state, supplying <paramref name="axis"/>'s resolution basis.</param>
    /// <param name="axis">Which viewport-relative basis a trailing <c>%</c> resolves against.</param>
    /// <param name="defaultValue">The value to use if the attribute is absent. Defaults to <c>0</c>.</param>
    /// <returns>The parsed value.</returns>
    /// <exception cref="FormatException">Thrown when the attribute is present but not a valid number/percentage.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the attribute is present but parses to a non-finite value - see
    ///     <see cref="ParseGeometryCoordinate"/>.
    /// </exception>
    private static float GetFloatAttribute(XElement element, string name, RenderState state, PercentageAxis axis, float defaultValue = 0f)
    {
        var raw = (string?)element.Attribute(name);
        return raw == null ? defaultValue : ParseGeometryCoordinate(raw, name, state, axis);
    }

    /// <summary>Reads an optional numeric attribute, distinguishing "absent" from any parsed value.</summary>
    /// <param name="element">The element to inspect.</param>
    /// <param name="name">The attribute name to read.</param>
    /// <param name="state">The cascaded render state, supplying <paramref name="axis"/>'s resolution basis.</param>
    /// <param name="axis">Which viewport-relative basis a trailing <c>%</c> resolves against.</param>
    /// <returns>The parsed value, or <see langword="null"/> if the attribute is absent.</returns>
    /// <exception cref="FormatException">Thrown when the attribute is present but not a valid number/percentage.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the attribute is present but parses to a non-finite value - see
    ///     <see cref="ParseGeometryCoordinate"/>.
    /// </exception>
    private static float? GetOptionalFloat(XElement element, string name, RenderState state, PercentageAxis axis)
    {
        var raw = (string?)element.Attribute(name);
        return raw == null ? null : ParseGeometryCoordinate(raw, name, state, axis);
    }

    /// <summary>
    ///     Parses an already-resolved raw value (from either a plain presentation attribute or the
    ///     CSS cascade - see <c>ResolveStyledValue</c>), rather than reading
    ///     <paramref name="name"/> directly off an <see cref="XElement"/> - the CSS-aware
    ///     counterpart to <see cref="GetOptionalFloat(XElement, string, RenderState, PercentageAxis)"/>,
    ///     used wherever a cascaded property's raw string is resolved once by the caller before
    ///     being handed to this identical, unduplicated value parser.
    /// </summary>
    /// <param name="raw">The already-resolved raw value, or <see langword="null"/> if absent from every tier.</param>
    /// <param name="name">The attribute name, forwarded only for call-site clarity/future diagnostics.</param>
    /// <param name="state">The cascaded render state, supplying <paramref name="axis"/>'s resolution basis.</param>
    /// <param name="axis">Which viewport-relative basis a trailing <c>%</c> resolves against.</param>
    /// <returns>The parsed value, or <see langword="null"/> if <paramref name="raw"/> is <see langword="null"/>.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="raw"/> is present but not a valid number/percentage.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="raw"/> is present but parses to a non-finite value - see
    ///     <see cref="ParseGeometryCoordinate"/>.
    /// </exception>
    private static float? GetOptionalFloat(string? raw, string name, RenderState state, PercentageAxis axis) =>
        raw == null ? null : ParseGeometryCoordinate(raw, name, state, axis);

    /// <summary>
    ///     Parses a shape/text geometry attribute's numeric value, resolving a trailing <c>%</c>
    ///     suffix against <paramref name="axis"/>'s current basis (see
    ///     <see cref="ResolvePercentageBasis"/>) - unlike opacity-family attributes (see
    ///     <see cref="ParseOpacityValue"/>), which are always basis-1 percentages of a
    ///     <c>[0, 1]</c> range, and gradient coordinates/<c>stop</c> <c>offset</c> (see
    ///     <see cref="ParsePercentOrNumber"/>), which are always resolved as basis-1 fractions of
    ///     the gradient's own coordinate space - both unaffected by, and unrelated to, this method.
    ///     Percentage resolution happens entirely inside <see cref="ParseCoordinate"/>'s own
    ///     choke point, so its existing finite/magnitude guards still apply to the resolved pixel
    ///     value, not just the pre-resolution percentage literal.
    /// </summary>
    /// <param name="raw">The raw attribute text.</param>
    /// <param name="attributeName">The attribute's name (unused, retained for call-site clarity/future diagnostics).</param>
    /// <param name="state">The cascaded render state, supplying <paramref name="axis"/>'s resolution basis.</param>
    /// <param name="axis">Which viewport-relative basis a trailing <c>%</c> resolves against.</param>
    /// <returns>The parsed value.</returns>
    /// <exception cref="FormatException">Propagates from <see cref="ParseCoordinate"/>.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="raw"/> parses (after any percentage resolution) to a
    ///     non-finite value or a value exceeding <see cref="MaxCoordinateMagnitude"/> - see
    ///     <see cref="ParseCoordinate"/>.
    /// </exception>
    private static float ParseGeometryCoordinate(string raw, string attributeName, RenderState state, PercentageAxis axis)
    {
        _ = attributeName;
        return ParseCoordinate(raw.Trim(), ResolvePercentageBasis(state, axis));
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
    ///     <para>
    ///     This bound is now a <b>three-purpose</b> constant, enforced at three independent points:
    ///     (1) pre-transform, at parse time, on every source-literal coordinate/length value (this
    ///     constant's original purpose, described above); (2) post-transform, on every shape's
    ///     final pixel-space geometry (<see cref="IsWithinCoordinateMagnitudeBudget"/>, called from
    ///     <see cref="RenderShape"/>) and on the post-transform-scaled effective stroke width
    ///     (<see cref="RenderStroke"/>); and (3) post-stroke, on the fillable outline geometry
    ///     <see cref="Drawing.PathStroker.Stroke"/> synthesizes from an already in-bound
    ///     <c>pixelPath</c>/<c>strokeWidth</c> pair (<see cref="IsWithinCoordinateMagnitudeBudget"/>
    ///     again, called from <see cref="RenderStroke"/> a second time, after stroking). The
    ///     second enforcement point exists because a transform argument only needs to stay <i>at
    ///     or under</i> this same bound to pass its own parse-time check (see
    ///     <see cref="TryReadNumber"/>'s strict <c>&gt;</c> boundary), so an in-bound local
    ///     coordinate or stroke width composed with an in-bound-but-large transform (e.g.
    ///     <c>scale(1000000)</c>) can still produce a final value far beyond what the
    ///     flattening/stroking pipeline was ever meant to see, even though every individual literal
    ///     involved was itself compliant. The third enforcement point exists for a distinct
    ///     reason: <c>stroke-miterlimit</c> (see <see cref="ParseValidMiterLimit"/>) is only
    ///     bounded below (finite, <c>&gt;= 1</c>), never above, so an in-bound-but-large
    ///     <c>strokeWidth</c> combined with an in-bound-but-extreme <c>miterlimit</c> and a
    ///     near-straight ("spike") vertex can drive <see cref="Drawing.StrokeOutliner"/>'s miter-
    ///     join synthesis to a point many orders of magnitude beyond this bound, even though
    ///     every individual literal - each pixel-space coordinate, the stroke width, and the
    ///     miterlimit - independently passed its own check. Reusing one constant for all three
    ///     points keeps today's fix minimal; splitting it into distinct constants remains possible
    ///     later, without any structural change, if evidence emerges that the bounds should
    ///     diverge.
    ///     </para>
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
    ///     Parses a <c>stroke-dasharray</c> attribute's whitespace/comma-separated number list,
    ///     resolving a trailing <c>%</c> on any individual entry against
    ///     <paramref name="percentageBasis"/> (the SVG specification's "diagonal" basis - see
    ///     <see cref="PercentageAxis.Diagonal"/>) - a distinct parsing path from
    ///     <see cref="ParseNumberList"/> (used by <c>viewBox</c>/transform-function arguments,
    ///     neither of which is ever percentage-eligible per spec), since only this one caller
    ///     needs per-entry percentage resolution.
    /// </summary>
    /// <param name="raw">The raw, non-<see langword="null"/> list text (may be blank).</param>
    /// <param name="percentageBasis">The value a <c>100%</c> entry resolves to.</param>
    /// <returns>The parsed, percentage-resolved numbers, in order; empty if <paramref name="raw"/> is blank.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when non-whitespace/comma content remains that does not form a valid number
    ///     (matching <see cref="ParseNumberList"/>'s own message convention), when a resolved
    ///     percentage entry is non-finite or exceeds <see cref="MaxCoordinateMagnitude"/> (mirroring
    ///     <see cref="ParseCoordinate"/>'s own post-resolution guards, since this parser's own
    ///     per-entry percentage math sits outside that choke point), or when more than
    ///     <see cref="MaxNumberListLength"/> numbers are present.
    /// </exception>
    private static List<float> ParseDashArrayNumberList(string raw, float percentageBasis)
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

            // A trailing '%' is not part of TryReadNumber's own number grammar (it stops at the
            // first non-digit character), so it is recognized and consumed here instead, then the
            // raw literal is rescaled against the diagonal basis - after which the same
            // finite/magnitude guards ParseCoordinate applies to every other percentage-eligible
            // attribute are re-applied explicitly, since this resolution happens outside that
            // choke point.
            if (position < raw.Length && raw[position] == '%')
            {
                position++;
                value = value / 100f * percentageBasis;
                if (!float.IsFinite(value))
                {
                    throw new InvalidDataException($"The stroke-dasharray percentage value '{raw}' is not a finite number.");
                }

                if (MathF.Abs(value) > MaxCoordinateMagnitude)
                {
                    throw new InvalidDataException($"The stroke-dasharray percentage value '{raw}' exceeds the maximum supported magnitude.");
                }
            }

            numbers.Add(value);

            // Charged incrementally - see ParseNumberList's identical rationale.
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
