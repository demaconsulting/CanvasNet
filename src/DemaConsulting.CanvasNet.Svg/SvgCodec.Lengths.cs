using System.Globalization;
using DemaConsulting.CanvasNet.Fonts;

namespace DemaConsulting.CanvasNet.Svg;

public static partial class SvgCodec
{
    // ================================================================================================
    // CSS length units and math functions
    // ================================================================================================
    //
    // Specification references (anchors are stable section ids of the editor's drafts):
    //   - SVG 2, "Units" (https://www.w3.org/TR/SVG2/coords.html#Units): a <length> with no unit is
    //     in user units, and a unit-bearing length is converted to user units at the CSS reference
    //     pixel ratio (1in = 96px).
    //   - CSS Values and Units Level 4, "Distance Units: the <length> type"
    //     (https://www.w3.org/TR/css-values-4/#lengths): absolute (#absolute-lengths, the unit
    //     ratios used below), font-relative (#font-relative-lengths: em, ex, ch, rem, lh, rlh) and
    //     viewport-percentage (#viewport-relative-lengths: vw, vh, vmin, vmax) units.
    //   - CSS Values and Units Level 4, "Mathematical Expressions" (https://www.w3.org/TR/css-values-4/#calc-func):
    //     calc(), min(), max() and clamp().

    /// <summary>
    ///     The CSS length units (plus the bare-number and percentage forms) this codec recognizes in
    ///     a <c>&lt;length&gt;</c> value.
    /// </summary>
    private enum LengthUnit
    {
        /// <summary>No suffix: a bare number, which SVG defines as user units.</summary>
        None,

        /// <summary>A trailing <c>%</c>, resolved against the call site's <see cref="PercentageAxis"/>.</summary>
        Percent,

        /// <summary>CSS pixel (<c>px</c>): exactly one user unit.</summary>
        Px,

        /// <summary>Point (<c>pt</c>): 1/72 inch.</summary>
        Pt,

        /// <summary>Pica (<c>pc</c>): 1/6 inch.</summary>
        Pc,

        /// <summary>Inch (<c>in</c>): 96 user units.</summary>
        In,

        /// <summary>Centimeter (<c>cm</c>): 1/2.54 inch.</summary>
        Cm,

        /// <summary>Millimeter (<c>mm</c>): 1/25.4 inch.</summary>
        Mm,

        /// <summary>Quarter-millimeter (<c>Q</c>): 1/101.6 inch.</summary>
        Q,

        /// <summary>The element's own computed <c>font-size</c> (the parent's for the <c>font-size</c> property itself).</summary>
        Em,

        /// <summary>The x-height of the element's font.</summary>
        Ex,

        /// <summary>The advance width of the glyph <c>0</c> in the element's font.</summary>
        Ch,

        /// <summary>The computed <c>font-size</c> of the root <c>svg</c> element.</summary>
        Rem,

        /// <summary>The element's computed <c>line-height</c>.</summary>
        Lh,

        /// <summary>The root <c>svg</c> element's computed <c>line-height</c>.</summary>
        Rlh,

        /// <summary>One percent of the root viewport's width.</summary>
        Vw,

        /// <summary>One percent of the root viewport's height.</summary>
        Vh,

        /// <summary>One percent of the smaller of the root viewport's width and height.</summary>
        Vmin,

        /// <summary>One percent of the larger of the root viewport's width and height.</summary>
        Vmax
    }

    /// <summary>
    ///     Maps each unit identifier (matched ASCII case-insensitively, per CSS) to its
    ///     <see cref="LengthUnit"/>.
    /// </summary>
    private static readonly Dictionary<string, LengthUnit> LengthUnitsByName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["px"] = LengthUnit.Px,
        ["pt"] = LengthUnit.Pt,
        ["pc"] = LengthUnit.Pc,
        ["in"] = LengthUnit.In,
        ["cm"] = LengthUnit.Cm,
        ["mm"] = LengthUnit.Mm,
        ["q"] = LengthUnit.Q,
        ["em"] = LengthUnit.Em,
        ["ex"] = LengthUnit.Ex,
        ["ch"] = LengthUnit.Ch,
        ["rem"] = LengthUnit.Rem,
        ["lh"] = LengthUnit.Lh,
        ["rlh"] = LengthUnit.Rlh,
        ["vw"] = LengthUnit.Vw,
        ["vh"] = LengthUnit.Vh,
        ["vmin"] = LengthUnit.Vmin,
        ["vmax"] = LengthUnit.Vmax
    };

    /// <summary>The number of user units (CSS reference pixels) in one inch (CSS Values 4, "Absolute lengths").</summary>
    private const double UserUnitsPerInch = 96.0;

    /// <summary>The number of centimeters in one inch (CSS Values 4: <c>1in = 2.54cm</c>).</summary>
    private const double CentimetersPerInch = 2.54;

    /// <summary>The number of millimeters in one inch (<c>1in = 25.4mm</c>).</summary>
    private const double MillimetersPerInch = 25.4;

    /// <summary>The number of quarter-millimeters in one inch (<c>1in = 101.6Q</c>).</summary>
    private const double QuarterMillimetersPerInch = 101.6;

    /// <summary>The number of points in one inch (<c>1in = 72pt</c>).</summary>
    private const double PointsPerInch = 72.0;

    /// <summary>The number of picas in one inch (<c>1in = 6pc</c>).</summary>
    private const double PicasPerInch = 6.0;

    /// <summary>
    ///     The fallback for <c>ex</c> (and, below, <c>ch</c>) when the font's own metric cannot be
    ///     measured: CSS Values 4 permits using <c>0.5em</c> when the real value is impractical to
    ///     determine.
    /// </summary>
    private const double FallbackFontMetricRatio = 0.5;

    /// <summary>
    ///     The line-height multiplier <c>normal</c> resolves to for <c>lh</c>/<c>rlh</c>: CSS Fonts 4
    ///     leaves <c>normal</c> font-dependent, and <c>1.2</c> is the conventional approximation.
    /// </summary>
    private const double NormalLineHeightMultiplier = 1.2;

    /// <summary>
    ///     The maximum length, in characters, of one length value or math expression. Real values
    ///     are a few dozen characters; the bound keeps the recursive-descent evaluator's work linear
    ///     and small for a hostile attribute.
    /// </summary>
    private const int MaxLengthExpressionChars = 1024;

    /// <summary>
    ///     The maximum nesting depth of parentheses and math functions in one expression, bounding
    ///     the evaluator's recursion so a deeply nested value can never exhaust the call stack.
    /// </summary>
    private const int MaxLengthExpressionDepth = 16;

    /// <summary>The longest unit identifier this codec recognizes (<c>vmin</c>/<c>vmax</c>).</summary>
    private const int MaxLengthUnitLetters = 4;

    /// <summary>
    ///     A computed <c>line-height</c> value: either a unitless multiplier of the element's own
    ///     font size (inherited as a number, so each descendant re-multiplies by its own size) or an
    ///     absolute length already resolved at the declaring element (CSS Fonts 4, <c>line-height</c>).
    /// </summary>
    /// <param name="Value">The multiplier, or the absolute length in user units.</param>
    /// <param name="IsMultiplier"><see langword="true"/> when <paramref name="Value"/> is a multiplier.</param>
    private readonly record struct LineHeightSpec(float Value, bool IsMultiplier);

    /// <summary>
    ///     Lazily measures the font-dependent ratios behind the <c>ex</c> and <c>ch</c> units for
    ///     one font, shared by every element in the cascade that keeps the same family, weight and
    ///     style so each font is measured at most once per cascade chain.
    /// </summary>
    /// <remarks>
    ///     Laziness matters because almost no document uses <c>ex</c>/<c>ch</c>: the font is
    ///     resolved (and its glyphs decoded) only the first time such a unit is evaluated. The
    ///     measurements are CSS Values 4 permitted approximations: <c>ex</c> is the height of the
    ///     <c>x</c> glyph's outline bounds (the core font reader does not expose the OS/2
    ///     <c>sxHeight</c> field) and <c>ch</c> is the advance width of the glyph <c>0</c>; either
    ///     falls back to <c>0.5em</c> when the font lacks the glyph or the glyph is unusable.
    ///     Not thread-safe; one render cascade is single-threaded.
    /// </remarks>
    private sealed class FontRelativeMetrics
    {
        /// <summary>Supplies the resolved font on first use.</summary>
        private readonly Func<TrueTypeFont> _fontProvider;

        /// <summary>The measured ratios, or <see langword="null"/> until first needed.</summary>
        private (double Ex, double Ch)? _ratios;

        /// <summary>Creates metrics that resolve the font through <paramref name="fontProvider"/> on first use.</summary>
        /// <param name="fontProvider">A delegate returning the font; invoked at most once.</param>
        public FontRelativeMetrics(Func<TrueTypeFont> fontProvider) => _fontProvider = fontProvider;

        /// <summary>Gets the <c>ex</c> unit as a fraction of the font size.</summary>
        public double ExRatio => Measure().Ex;

        /// <summary>Gets the <c>ch</c> unit as a fraction of the font size.</summary>
        public double ChRatio => Measure().Ch;

        /// <summary>Measures (once) and returns both ratios.</summary>
        /// <returns>The <c>ex</c> and <c>ch</c> ratios, each falling back to <c>0.5</c>.</returns>
        private (double Ex, double Ch) Measure()
        {
            if (_ratios.HasValue)
            {
                return _ratios.Value;
            }

            var ex = FallbackFontMetricRatio;
            var ch = FallbackFontMetricRatio;
            try
            {
                var font = _fontProvider();
                var unitsPerEm = (double)font.UnitsPerEm;
                if (unitsPerEm > 0)
                {
                    var xGlyph = font.GetGlyphIndex('x');
                    if (xGlyph != 0)
                    {
                        var bounds = font.GetGlyphOutline(xGlyph).GetBounds(1f);
                        if (!bounds.IsEmpty && bounds.Height > 0f)
                        {
                            ex = bounds.Height / unitsPerEm;
                        }
                    }

                    var zeroGlyph = font.GetGlyphIndex('0');
                    if (zeroGlyph != 0)
                    {
                        var advance = font.GetAdvanceWidth(zeroGlyph);
                        if (advance > 0)
                        {
                            ch = advance / unitsPerEm;
                        }
                    }
                }
            }
            catch (InvalidDataException)
            {
                // A malformed glyph must never fail the document over a unit approximation:
                // whichever ratio was not yet measured keeps its 0.5em fallback
            }

            _ratios = (ex, ch);
            return _ratios.Value;
        }
    }

    /// <summary>
    ///     Resolves one <c>&lt;length&gt;</c>/<c>&lt;percentage&gt;</c>/math-function value to user
    ///     units without ever throwing - the single place every length call site funnels through, so
    ///     the unit grammar, the unit conversions and the safety bounds exist exactly once.
    /// </summary>
    /// <param name="raw">The raw value text; surrounding whitespace is ignored.</param>
    /// <param name="state">
    ///     The cascaded state supplying the font size (<c>em</c>/<c>ex</c>/<c>ch</c>/<c>lh</c>), the
    ///     root font size and root viewport (<c>rem</c>/<c>rlh</c>/<c>vw</c>...), and the viewport
    ///     used for a percentage.
    /// </param>
    /// <param name="axis">Which basis a <c>%</c> resolves against.</param>
    /// <param name="value">The resolved user-unit value when this method returns <see langword="true"/>.</param>
    /// <param name="error">A short description of the problem when this method returns <see langword="false"/>.</param>
    /// <param name="enforceMagnitude">
    ///     <see langword="true"/> (the default) to reject a result beyond
    ///     <see cref="MaxCoordinateMagnitude"/>; <see langword="false"/> only for the root
    ///     <c>width</c>/<c>height</c>, whose oversized values are clamped later by the size
    ///     pipeline rather than rejected here.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when <paramref name="raw"/> is a valid length that resolves to a
    ///     finite value within <see cref="MaxCoordinateMagnitude"/>; <see langword="false"/> for
    ///     anything else (garbage, an unknown unit, whitespace between number and unit, a malformed
    ///     or over-complex math expression, a non-finite result, or an over-magnitude result). The
    ///     caller owns the malformed-value policy (see this class's remarks).
    /// </returns>
    /// <remarks>
    ///     A bare number and a bare percentage are evaluated in single-precision exactly as the
    ///     pre-unit parser did, so every document that used only those forms renders bit-identically.
    ///     Unit conversions are evaluated in double precision and rounded to single precision once,
    ///     so equivalent absolute lengths (for example <c>1in</c>, <c>96px</c> and <c>2.54cm</c>)
    ///     resolve to the same float.
    /// </remarks>
    private static bool TryResolveLength(
        string raw, RenderState state, PercentageAxis axis, out float value, out string? error, bool enforceMagnitude = true)
    {
        value = 0f;
        error = null;
        var text = raw.Trim();
        if (text.Length == 0)
        {
            error = "the value is empty";
            return false;
        }

        if (text.Length > MaxLengthExpressionChars)
        {
            error = "the value is too long";
            return false;
        }

        double result;
        if (StartsWithMathFunction(text))
        {
            var evaluator = new LengthMathEvaluator(text, state, axis);
            if (!evaluator.TryEvaluate(out result))
            {
                error = evaluator.Error ?? "the math expression is invalid";
                return false;
            }
        }
        else
        {
            var position = 0;
            if (!TryReadLengthToken(text, ref position, out var number, out var unit) || position != text.Length)
            {
                error = "not a number, percentage, or recognized length";
                return false;
            }

            result = unit switch
            {
                // Single-precision, exactly as the legacy number/percentage parser: see remarks
                LengthUnit.None => float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture),
                LengthUnit.Percent => float.Parse(text[..^1], NumberStyles.Float, CultureInfo.InvariantCulture)
                    / 100f * ResolvePercentageBasis(state, axis),
                _ => ConvertToUserUnits(number, unit, state)
            };
        }

        if (!double.IsFinite(result))
        {
            error = "the value is not a finite number";
            return false;
        }

        if (enforceMagnitude && Math.Abs(result) > MaxCoordinateMagnitude)
        {
            error = "the value exceeds the maximum supported magnitude";
            return false;
        }

        value = (float)result;
        if (!float.IsFinite(value))
        {
            error = "the value is not a finite number";
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Resolves a length with the strict malformed-value policy (class S in this class's
    ///     remarks): a value that is not a valid length is a corrupt document.
    /// </summary>
    /// <param name="raw">The raw value text.</param>
    /// <param name="attributeName">The attribute or property name, used in the error message.</param>
    /// <param name="state">The cascaded state supplying unit and percentage bases.</param>
    /// <param name="axis">Which basis a <c>%</c> resolves against.</param>
    /// <returns>The resolved user-unit value.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="raw"/> is not a valid length - never a raw
    ///     <see cref="FormatException"/>, so a malformed value always surfaces as one documented
    ///     corrupt-document failure.
    /// </exception>
    private static float ResolveLengthStrict(string raw, string attributeName, RenderState state, PercentageAxis axis)
    {
        if (TryResolveLength(raw, state, axis, out var value, out var error))
        {
            return value;
        }

        var shown = raw.Length > 100 ? raw[..100] + "..." : raw;
        throw new InvalidDataException($"The '{attributeName}' value '{shown}' is not a valid length: {error}.");
    }

    /// <summary>Determines whether <paramref name="text"/> begins a supported math function (<c>calc</c>, <c>min</c>, <c>max</c>, <c>clamp</c>).</summary>
    /// <param name="text">The trimmed value text.</param>
    /// <returns><see langword="true"/> when the text starts with one of those function names followed by <c>(</c>.</returns>
    private static bool StartsWithMathFunction(string text)
    {
        var scan = 0;
        while (scan < text.Length && char.IsAsciiLetter(text[scan]))
        {
            scan++;
        }

        if (scan == 0 || scan >= text.Length || text[scan] != '(')
        {
            return false;
        }

        return IsMathFunctionName(text.AsSpan(0, scan));
    }

    /// <summary>Determines whether <paramref name="name"/> names a supported math function.</summary>
    /// <param name="name">The identifier.</param>
    /// <returns><see langword="true"/> for <c>calc</c>, <c>min</c>, <c>max</c> or <c>clamp</c> (ASCII case-insensitive).</returns>
    private static bool IsMathFunctionName(ReadOnlySpan<char> name) =>
        name.Equals("calc", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("min", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("max", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("clamp", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Reads one number with an optional unit or <c>%</c> suffix starting at
    ///     <paramref name="position"/> (no whitespace allowed between the number and its unit).
    /// </summary>
    /// <param name="text">The text to read from.</param>
    /// <param name="position">The start position; advanced past the token on success, unchanged on failure.</param>
    /// <param name="number">The token's numeric part.</param>
    /// <param name="unit">The token's unit (<see cref="LengthUnit.None"/> for a bare number).</param>
    /// <returns>
    ///     <see langword="true"/> when a well-formed token was read; <see langword="false"/> for a
    ///     missing number, a non-finite number, or an unrecognized unit identifier.
    /// </returns>
    /// <remarks>
    ///     The number scan shares <see cref="TryConsumeExponent"/> with the plain number reader, so
    ///     <c>1e3</c> is a number while <c>1em</c> and <c>1ex</c> are a number plus a unit (an
    ///     <c>e</c> is an exponent only when digits follow).
    /// </remarks>
    private static bool TryReadLengthToken(string text, ref int position, out double number, out LengthUnit unit)
    {
        number = 0;
        unit = LengthUnit.None;
        var scan = position;
        if (scan < text.Length && (text[scan] == '+' || text[scan] == '-'))
        {
            scan++;
        }

        var sawDigit = false;
        var sawDot = false;
        while (scan < text.Length && (char.IsAsciiDigit(text[scan]) || (text[scan] == '.' && !sawDot)))
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
            return false;
        }

        scan = TryConsumeExponent(text, scan);
        if (!double.TryParse(text.AsSpan(position, scan - position), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ||
            !double.IsFinite(parsed))
        {
            return false;
        }

        var parsedUnit = LengthUnit.None;
        if (scan < text.Length && text[scan] == '%')
        {
            parsedUnit = LengthUnit.Percent;
            scan++;
        }
        else
        {
            var letterStart = scan;
            while (scan < text.Length && char.IsAsciiLetter(text[scan]))
            {
                scan++;
                if (scan - letterStart > MaxLengthUnitLetters)
                {
                    return false;
                }
            }

            if (scan > letterStart && !LengthUnitsByName.TryGetValue(text[letterStart..scan], out parsedUnit))
            {
                return false;
            }
        }

        number = parsed;
        unit = parsedUnit;
        position = scan;
        return true;
    }

    /// <summary>
    ///     Converts a unit-bearing number to user units. Percent is handled by the caller (it needs
    ///     an axis); bare numbers and <c>px</c> are the identity.
    /// </summary>
    /// <param name="number">The numeric part.</param>
    /// <param name="unit">The unit.</param>
    /// <param name="state">The cascaded state supplying font and viewport bases.</param>
    /// <returns>The value in user units, in double precision.</returns>
    private static double ConvertToUserUnits(double number, LengthUnit unit, RenderState state) => unit switch
    {
        LengthUnit.None or LengthUnit.Px => number,
        LengthUnit.Pt => number * UserUnitsPerInch / PointsPerInch,
        LengthUnit.Pc => number * UserUnitsPerInch / PicasPerInch,
        LengthUnit.In => number * UserUnitsPerInch,
        LengthUnit.Cm => number * UserUnitsPerInch / CentimetersPerInch,
        LengthUnit.Mm => number * UserUnitsPerInch / MillimetersPerInch,
        LengthUnit.Q => number * UserUnitsPerInch / QuarterMillimetersPerInch,
        LengthUnit.Em => number * state.FontSize,
        LengthUnit.Ex => number * (state.FontMetrics?.ExRatio ?? FallbackFontMetricRatio) * state.FontSize,
        LengthUnit.Ch => number * (state.FontMetrics?.ChRatio ?? FallbackFontMetricRatio) * state.FontSize,
        LengthUnit.Rem => number * state.RootFontSize,
        LengthUnit.Lh => number * ResolveLineHeight(state.LineHeight, state.FontSize),
        LengthUnit.Rlh => number * ResolveLineHeight(state.RootLineHeight, state.RootFontSize),
        LengthUnit.Vw => number / 100.0 * state.RootViewportWidth,
        LengthUnit.Vh => number / 100.0 * state.RootViewportHeight,
        LengthUnit.Vmin => number / 100.0 * Math.Min(state.RootViewportWidth, state.RootViewportHeight),
        LengthUnit.Vmax => number / 100.0 * Math.Max(state.RootViewportWidth, state.RootViewportHeight),
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unrecognized length unit.")
    };

    /// <summary>Resolves a computed line height to user units for the <c>lh</c>/<c>rlh</c> units.</summary>
    /// <param name="lineHeight">The computed line height, or <see langword="null"/> for <c>normal</c>.</param>
    /// <param name="fontSize">The font size a multiplier applies to.</param>
    /// <returns>The line height in user units; <c>normal</c> is <see cref="NormalLineHeightMultiplier"/> times the font size.</returns>
    private static double ResolveLineHeight(LineHeightSpec? lineHeight, float fontSize)
    {
        if (lineHeight is not { } spec)
        {
            return NormalLineHeightMultiplier * fontSize;
        }

        return spec.IsMultiplier ? spec.Value * (double)fontSize : spec.Value;
    }

    /// <summary>
    ///     Scans the next whitespace/comma-separated entry of a length list (a
    ///     <c>stroke-dasharray</c>, or a text <c>dx</c>/<c>dy</c> list), treating a math function
    ///     call such as <c>calc(1px + 2px)</c> as one entry even though it contains spaces and commas.
    /// </summary>
    /// <param name="text">The list text.</param>
    /// <param name="position">The position to start at; advanced past the entry on success.</param>
    /// <param name="entry">The entry's text when this method returns <see langword="true"/>.</param>
    /// <returns>
    ///     <see langword="true"/> when an entry was read; <see langword="false"/> at the end of the
    ///     list or when the next entry is malformed (distinguish with <paramref name="position"/>
    ///     against the text length after skipping separators).
    /// </returns>
    private static bool TryReadLengthListEntry(string text, ref int position, out string entry)
    {
        entry = string.Empty;
        SkipSeparators(text, ref position);
        var start = position;
        var scan = position;
        if (scan >= text.Length)
        {
            return false;
        }

        if (char.IsAsciiLetter(text[scan]))
        {
            while (scan < text.Length && char.IsAsciiLetter(text[scan]))
            {
                scan++;
            }

            if (scan >= text.Length || text[scan] != '(' || !IsMathFunctionName(text.AsSpan(start, scan - start)))
            {
                return false;
            }

            var depth = 0;
            while (scan < text.Length)
            {
                if (text[scan] == '(')
                {
                    depth++;
                }
                else if (text[scan] == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        scan++;
                        break;
                    }
                }

                scan++;
            }

            if (depth != 0)
            {
                return false;
            }
        }
        else if (!TryReadLengthToken(text, ref scan, out _, out _))
        {
            return false;
        }

        entry = text[start..scan];
        position = scan;
        return true;
    }

    /// <summary>
    ///     Resolves the first entry of a length list as a class F value (used for the text
    ///     <c>dx</c>/<c>dy</c> attributes, of which only the first value is honored; see
    ///     <see cref="SvgCodec"/>'s remarks on the malformed-length policy).
    /// </summary>
    /// <param name="raw">The raw list text, or <see langword="null"/> when the attribute is absent.</param>
    /// <param name="state">The cascaded state supplying unit and percentage bases.</param>
    /// <param name="axis">Which basis a <c>%</c> resolves against.</param>
    /// <returns>The first entry's value, or <c>0</c> when absent, empty or malformed.</returns>
    private static float ResolveFirstLengthOrZero(string? raw, RenderState state, PercentageAxis axis)
    {
        if (raw == null)
        {
            return 0f;
        }

        var position = 0;
        return TryReadLengthListEntry(raw, ref position, out var entry) &&
               TryResolveLength(entry, state, axis, out var value, out _)
            ? value
            : 0f;
    }

    /// <summary>
    ///     A bounded recursive-descent evaluator for CSS <c>calc()</c>, <c>min()</c>, <c>max()</c>
    ///     and <c>clamp()</c> (CSS Values 4, "Mathematical Expressions"), evaluating eagerly to user
    ///     units so a mixed expression such as <c>calc(100% - 2em)</c> needs no deferred resolution.
    /// </summary>
    /// <remarks>
    ///     Type checking follows the specification's simplification to two kinds: a number and a
    ///     length (a percentage is a length here because it resolves against the call site's axis).
    ///     <c>+</c>/<c>-</c> require matching kinds and, per the CSS grammar, whitespace on both
    ///     sides of the operator; <c>*</c> needs at least one number operand; <c>/</c> needs a
    ///     non-zero number divisor. Nesting is capped by <see cref="MaxLengthExpressionDepth"/> and
    ///     the input by <see cref="MaxLengthExpressionChars"/>, so evaluation is linear and cannot
    ///     overflow the stack. Instances are single-use and not thread-safe.
    /// </remarks>
    private sealed class LengthMathEvaluator
    {
        /// <summary>The expression text.</summary>
        private readonly string _text;

        /// <summary>The cascaded state supplying unit and percentage bases.</summary>
        private readonly RenderState _state;

        /// <summary>The percentage axis.</summary>
        private readonly PercentageAxis _axis;

        /// <summary>The current read position.</summary>
        private int _position;

        /// <summary>The current nesting depth.</summary>
        private int _depth;

        /// <summary>Creates an evaluator for one expression.</summary>
        /// <param name="text">The trimmed expression, starting with a math function name.</param>
        /// <param name="state">The cascaded state supplying unit and percentage bases.</param>
        /// <param name="axis">Which basis a <c>%</c> resolves against.</param>
        public LengthMathEvaluator(string text, RenderState state, PercentageAxis axis)
        {
            _text = text;
            _state = state;
            _axis = axis;
        }

        /// <summary>Gets a short description of why evaluation failed, or <see langword="null"/>.</summary>
        public string? Error { get; private set; }

        /// <summary>Evaluates the whole expression.</summary>
        /// <param name="result">The result (as a number or user-unit length) on success.</param>
        /// <returns><see langword="true"/> on success; <see langword="false"/> with <see cref="Error"/> set otherwise.</returns>
        public bool TryEvaluate(out double result)
        {
            result = 0;
            if (!TryParseFactor(out var value))
            {
                return false;
            }

            SkipSpaces();
            if (_position != _text.Length)
            {
                return Fail("unexpected trailing content");
            }

            result = value.Number;
            return true;
        }

        /// <summary>Records a failure.</summary>
        /// <param name="message">The failure description.</param>
        /// <returns>Always <see langword="false"/>, for fluent early returns.</returns>
        private bool Fail(string message)
        {
            Error ??= message;
            return false;
        }

        /// <summary>Skips whitespace.</summary>
        /// <returns><see langword="true"/> if any whitespace was skipped.</returns>
        private bool SkipSpaces()
        {
            var start = _position;
            while (_position < _text.Length && char.IsWhiteSpace(_text[_position]))
            {
                _position++;
            }

            return _position > start;
        }

        /// <summary>Parses a sum: <c>product (('+' | '-') product)*</c>.</summary>
        /// <param name="value">The parsed value.</param>
        /// <returns><see langword="true"/> on success.</returns>
        private bool TryParseSum(out MathValue value)
        {
            if (!TryParseProduct(out value))
            {
                return false;
            }

            while (true)
            {
                var hadLeadingSpace = SkipSpaces();
                if (_position >= _text.Length || (_text[_position] != '+' && _text[_position] != '-'))
                {
                    return true;
                }

                var op = _text[_position++];
                if (!hadLeadingSpace || !SkipSpaces())
                {
                    return Fail("'+' and '-' must be surrounded by whitespace");
                }

                if (!TryParseProduct(out var right))
                {
                    return false;
                }

                if (value.IsLength != right.IsLength)
                {
                    return Fail("cannot add or subtract a number and a length");
                }

                value = new MathValue(op == '+' ? value.Number + right.Number : value.Number - right.Number, value.IsLength);
                if (!double.IsFinite(value.Number))
                {
                    return Fail("the value is not a finite number");
                }
            }
        }

        /// <summary>Parses a product: <c>factor (('*' | '/') factor)*</c>.</summary>
        /// <param name="value">The parsed value.</param>
        /// <returns><see langword="true"/> on success.</returns>
        private bool TryParseProduct(out MathValue value)
        {
            if (!TryParseFactor(out value))
            {
                return false;
            }

            while (true)
            {
                var saved = _position;
                SkipSpaces();
                if (_position >= _text.Length || (_text[_position] != '*' && _text[_position] != '/'))
                {
                    // Not a product operator: restore so the sum loop can see its own leading space
                    _position = saved;
                    return true;
                }

                var op = _text[_position++];
                SkipSpaces();
                if (!TryParseFactor(out var right))
                {
                    return false;
                }

                if (op == '*')
                {
                    if (value.IsLength && right.IsLength)
                    {
                        return Fail("cannot multiply two lengths");
                    }

                    value = new MathValue(value.Number * right.Number, value.IsLength || right.IsLength);
                }
                else
                {
                    if (right.IsLength)
                    {
                        return Fail("cannot divide by a length");
                    }

                    if (right.Number == 0)
                    {
                        return Fail("division by zero");
                    }

                    value = new MathValue(value.Number / right.Number, value.IsLength);
                }

                if (!double.IsFinite(value.Number))
                {
                    return Fail("the value is not a finite number");
                }
            }
        }

        /// <summary>
        ///     Parses a factor: a number/length/percentage token, a parenthesized sum, or a math
        ///     function call.
        /// </summary>
        /// <param name="value">The parsed value.</param>
        /// <returns><see langword="true"/> on success.</returns>
        private bool TryParseFactor(out MathValue value)
        {
            value = default;
            if (++_depth > MaxLengthExpressionDepth)
            {
                return Fail("the expression is nested too deeply");
            }

            try
            {
                if (_position >= _text.Length)
                {
                    return Fail("the expression ended unexpectedly");
                }

                var c = _text[_position];
                if (c == '(')
                {
                    _position++;
                    return TryParseGroupBody(out value);
                }

                if (char.IsAsciiLetter(c))
                {
                    return TryParseFunction(out value);
                }

                if (!TryReadLengthToken(_text, ref _position, out var number, out var unit))
                {
                    return Fail("unrecognized token");
                }

                value = unit switch
                {
                    LengthUnit.None => new MathValue(number, false),
                    LengthUnit.Percent => new MathValue(number / 100.0 * ResolvePercentageBasis(_state, _axis), true),
                    _ => new MathValue(ConvertToUserUnits(number, unit, _state), true)
                };
                return true;
            }
            finally
            {
                _depth--;
            }
        }

        /// <summary>Parses <c>sum ')'</c> after an opening parenthesis.</summary>
        /// <param name="value">The parsed value.</param>
        /// <returns><see langword="true"/> on success.</returns>
        private bool TryParseGroupBody(out MathValue value)
        {
            SkipSpaces();
            if (!TryParseSum(out value))
            {
                return false;
            }

            SkipSpaces();
            if (_position >= _text.Length || _text[_position] != ')')
            {
                return Fail("missing closing parenthesis");
            }

            _position++;
            return true;
        }

        /// <summary>Parses a <c>calc()</c>, <c>min()</c>, <c>max()</c> or <c>clamp()</c> call.</summary>
        /// <param name="value">The parsed value.</param>
        /// <returns><see langword="true"/> on success.</returns>
        private bool TryParseFunction(out MathValue value)
        {
            value = default;
            var start = _position;
            while (_position < _text.Length && char.IsAsciiLetter(_text[_position]))
            {
                _position++;
            }

            var name = _text.AsSpan(start, _position - start);
            if (_position >= _text.Length || _text[_position] != '(' || !IsMathFunctionName(name))
            {
                return Fail("unsupported function or identifier");
            }

            _position++;
            if (name.Equals("calc", StringComparison.OrdinalIgnoreCase))
            {
                return TryParseGroupBody(out value);
            }

            var isMin = name.Equals("min", StringComparison.OrdinalIgnoreCase);
            var isClamp = name.Equals("clamp", StringComparison.OrdinalIgnoreCase);
            var arguments = new List<MathValue>(3);
            while (true)
            {
                SkipSpaces();
                if (!TryParseSum(out var argument))
                {
                    return false;
                }

                arguments.Add(argument);
                SkipSpaces();
                if (_position < _text.Length && _text[_position] == ',')
                {
                    _position++;
                    continue;
                }

                break;
            }

            if (_position >= _text.Length || _text[_position] != ')')
            {
                return Fail("missing closing parenthesis");
            }

            _position++;
            if (isClamp ? arguments.Count != 3 : arguments.Count < 1)
            {
                return Fail("wrong number of arguments");
            }

            var isLength = arguments[0].IsLength;
            if (arguments.Exists(a => a.IsLength != isLength))
            {
                return Fail("arguments mix numbers and lengths");
            }

            double result;
            if (isClamp)
            {
                result = Math.Max(arguments[0].Number, Math.Min(arguments[1].Number, arguments[2].Number));
            }
            else
            {
                result = arguments[0].Number;
                for (var i = 1; i < arguments.Count; i++)
                {
                    result = isMin ? Math.Min(result, arguments[i].Number) : Math.Max(result, arguments[i].Number);
                }
            }

            value = new MathValue(result, isLength);
            return true;
        }
    }

    /// <summary>One evaluated math term: a value and whether it is a length (rather than a plain number).</summary>
    /// <param name="Number">The value; a length is already in user units.</param>
    /// <param name="IsLength"><see langword="true"/> for a length or percentage, <see langword="false"/> for a number.</param>
    private readonly record struct MathValue(double Number, bool IsLength);
}
