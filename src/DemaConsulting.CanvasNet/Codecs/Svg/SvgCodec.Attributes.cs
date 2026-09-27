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
