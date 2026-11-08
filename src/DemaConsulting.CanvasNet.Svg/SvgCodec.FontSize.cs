using System.Globalization;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Fonts;

namespace DemaConsulting.CanvasNet.Svg;

public static partial class SvgCodec
{
    // ================================================================================================
    // font-size, line-height and the font shorthand
    // ================================================================================================
    //
    // Specification references:
    //   - CSS Fonts Level 4, "font-size" (https://www.w3.org/TR/css-fonts-4/#font-size-prop): the
    //     <absolute-size> and <relative-size> keywords, <length-percentage>, and the rule that
    //     em/ex/ch and % in font-size itself resolve against the parent's computed font size.
    //   - CSS Fonts Level 4, "font" shorthand (https://www.w3.org/TR/css-fonts-4/#font-prop):
    //     [ <font-style> || <font-variant-css2> || <font-weight> || <font-stretch-css2> ]?
    //     <font-size> [ / <line-height> ]? <font-family>, resetting the omitted longhands.
    //   - CSS Values 4 (https://www.w3.org/TR/css-values-4/#common-keywords): the CSS-wide keywords
    //     inherit, initial and unset.
    //   - SVG 2, "Presentation attributes" (https://www.w3.org/TR/SVG2/styling.html#PresentationAttributes):
    //     font, font-size and font-family are presentation attributes that rank below CSS.

    /// <summary>The initial <c>font-size</c> (<c>medium</c>): the CSS/UA default of 16 user units.</summary>
    private const float MediumFontSize = 16f;

    /// <summary>
    ///     The ratio <c>larger</c> multiplies, and <c>smaller</c> divides, the parent's font size by.
    ///     CSS Fonts 4 leaves the ratio user-agent defined; <c>1.2</c> is the CSS 2.1 convention.
    /// </summary>
    private const float RelativeFontSizeRatio = 1.2f;

    /// <summary>
    ///     The pixel size of each <c>&lt;absolute-size&gt;</c> keyword at the default <c>medium</c>
    ///     of 16 - the table used by the major browsers (<c>9/10/13/16/18/24/32/48</c>).
    /// </summary>
    /// <remarks>
    ///     CSS Fonts 4 (<c>font-size</c>, <c>&lt;absolute-size&gt;</c>) defines scale factors of
    ///     <c>3/5, 3/4, 8/9, 1, 6/5, 3/2, 2, 3</c> (9.6, 12, 14.22, 16, 19.2, 24, 32, 48 px at
    ///     medium = 16) but explicitly no longer mandates a fixed ratio. The browser table is used
    ///     so that a document renders at the same text size here as in the browser it was authored
    ///     in; swapping to the specification factors is a change to this one table.
    /// </remarks>
    private static readonly Dictionary<string, float> AbsoluteFontSizes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["xx-small"] = 9f,
        ["x-small"] = 10f,
        ["small"] = 13f,
        ["medium"] = MediumFontSize,
        ["large"] = 18f,
        ["x-large"] = 24f,
        ["xx-large"] = 32f,
        ["xxx-large"] = 48f
    };

    /// <summary>The system-font keywords of the <c>font</c> shorthand, which this codec does not support (no host font settings).</summary>
    private static readonly HashSet<string> SystemFontKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "caption", "icon", "menu", "message-box", "small-caption", "status-bar"
    };

    /// <summary>The <c>font-stretch</c> keywords permitted in the <c>font</c> shorthand; parsed and ignored (the codec does not model stretch).</summary>
    private static readonly HashSet<string> FontStretchKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "ultra-condensed", "extra-condensed", "condensed", "semi-condensed",
        "semi-expanded", "expanded", "extra-expanded", "ultra-expanded"
    };

    /// <summary>The maximum number of tokens the <c>font</c> shorthand parser examines before the family list begins.</summary>
    private const int MaxFontShorthandPrefixTokens = 8;

    /// <summary>The largest magnitude, in degrees, of an <c>oblique</c> angle (CSS Fonts 4: <c>[-90deg, 90deg]</c>).</summary>
    private const double MaxObliqueDegrees = 90.0;

    /// <summary>Tolerance, in degrees, so equivalent boundary angles (<c>100grad</c>, <c>0.25turn</c>) compare equal to <c>90deg</c>.</summary>
    private const double ObliqueAngleEpsilon = 1e-9;

    /// <summary>
    ///     Resolves a raw <c>font-size</c> value to user units against its parent's state.
    /// </summary>
    /// <param name="raw">The raw value (attribute, inline style, stylesheet rule or <c>font</c> expansion).</param>
    /// <param name="parent">
    ///     The parent element's cascaded state: <c>em</c>/<c>ex</c>/<c>ch</c>/<c>lh</c>, <c>%</c>,
    ///     <c>larger</c> and <c>smaller</c> are all relative to the PARENT for the <c>font-size</c>
    ///     property itself (CSS Fonts 4), unlike every other property.
    /// </param>
    /// <param name="numberResultAllowed">
    ///     <see langword="true"/> only when <paramref name="raw"/> is the element's <c>font-size</c>
    ///     presentation <em>attribute</em>, to which SVG 2 "Attribute syntax" (Types 4.2) adds
    ///     <c>&lt;number&gt;</c>, so a number-typed math result (<c>calc(5 + 5)</c>) is user units
    ///     like the literal <c>10</c>; <see langword="false"/> for a CSS declaration (inline style,
    ///     stylesheet rule, <c>font</c> shorthand or the <c>font</c> attribute's expansion), whose
    ///     grammar is <c>&lt;length-percentage [0,inf]&gt;</c> with no <c>&lt;number&gt;</c>.
    /// </param>
    /// <returns>
    ///     The resolved size; <see langword="null"/> when the value is invalid (an unknown unit,
    ///     garbage, a malformed expression, an over-magnitude result, or a number-typed math result
    ///     where <paramref name="numberResultAllowed"/> is <see langword="false"/>) or is a keyword with no
    ///     effect (<c>revert</c>, <c>revert-layer</c>), meaning "drop the declaration": the cascade
    ///     then tries the next lower source and inherits only when none is valid
    ///     (see <see cref="ResolveFontSize"/>; class F in this class's remarks). Never throws.
    /// </returns>
    /// <remarks>
    ///     <c>inherit</c> and <c>unset</c> (font-size is inherited) yield the parent's size;
    ///     <c>initial</c> and <c>medium</c> yield 16 regardless of the parent. A zero or negative
    ///     size is returned as-is: the text renderer draws nothing for it, as before.
    /// </remarks>
    private static float? ParseFontSizeValue(string raw, RenderState parent, bool numberResultAllowed)
    {
        var text = raw.Trim();
        if (text.Length == 0)
        {
            return null;
        }

        if (AbsoluteFontSizes.TryGetValue(text, out var absolute))
        {
            return absolute;
        }

        float keywordValue;
        if (text.Equals("larger", StringComparison.OrdinalIgnoreCase))
        {
            keywordValue = parent.FontSize * RelativeFontSizeRatio;
        }
        else if (text.Equals("smaller", StringComparison.OrdinalIgnoreCase))
        {
            keywordValue = parent.FontSize / RelativeFontSizeRatio;
        }
        else if (text.Equals("inherit", StringComparison.OrdinalIgnoreCase) ||
                 text.Equals("unset", StringComparison.OrdinalIgnoreCase))
        {
            keywordValue = parent.FontSize;
        }
        else if (text.Equals("initial", StringComparison.OrdinalIgnoreCase))
        {
            keywordValue = MediumFontSize;
        }
        else if (text.Equals("revert", StringComparison.OrdinalIgnoreCase) ||
                 text.Equals("revert-layer", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        else
        {
            return TryResolveLength(text, parent, PercentageAxis.FontSize, out var length, out _, allowNumberResult: numberResultAllowed) ? length : null;
        }

        // Repeated 'larger' down a deep tree compounds 1.2^depth: keep the result inside the same
        // bound every other length obeys, otherwise ignore the declaration
        return float.IsFinite(keywordValue) && MathF.Abs(keywordValue) <= MaxCoordinateMagnitude ? keywordValue : null;
    }

    /// <summary>
    ///     Resolves a raw <c>line-height</c> value (only reachable through CSS, the <c>font</c>
    ///     shorthand or an inline style - it is not an SVG presentation attribute) for the
    ///     <c>lh</c>/<c>rlh</c> units.
    /// </summary>
    /// <param name="raw">The raw value.</param>
    /// <param name="state">The declaring element's state (its own computed font size is the basis for <c>em</c> and <c>%</c>).</param>
    /// <param name="computed">
    ///     On success the computed value: <see langword="null"/> for <c>normal</c> (and
    ///     <c>initial</c>), a multiplier for a bare number or a math expression that resolves to a
    ///     number (<c>calc(1 + 1)</c>), or an absolute length for a length/percentage (resolved at
    ///     the declaring element, as CSS specifies).
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when the declaration sets a value; <see langword="false"/> for an
    ///     invalid value, a negative value, <c>inherit</c>/<c>unset</c> (both inherit) or
    ///     <c>revert</c>, meaning "keep the parent's line height". Never throws.
    /// </returns>
    private static bool TryParseLineHeight(string raw, RenderState state, out LineHeightSpec? computed)
    {
        computed = null;
        var text = raw.Trim();
        if (text.Equals("normal", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("initial", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (text.Length == 0 || (char.IsAsciiLetter(text[0]) && !StartsWithMathFunction(text)))
        {
            return false;
        }

        // A bare number is a multiplier of each element's own font size (CSS Fonts 4, line-height)
        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var multiplier))
        {
            if (!float.IsFinite(multiplier) || multiplier < 0f || multiplier > MaxCoordinateMagnitude)
            {
                return false;
            }

            computed = new LineHeightSpec(multiplier, IsMultiplier: true);
            return true;
        }

        // A number-typed math result (calc(1 + 1)) is a multiplier like a bare number; a length or
        // percentage result is an absolute length (CSS Values 4, "Type Checking", which singles out
        // line-height for this); the two are told apart by the type the evaluator preserved
        if (!TryResolveLengthOrNumber(text, state, PercentageAxis.FontSize, out var length, out var isNumber, out _) || length < 0f)
        {
            return false;
        }

        computed = new LineHeightSpec(length, IsMultiplier: isNumber);
        return true;
    }

    /// <summary>
    ///     Expands a <c>font</c> shorthand value into its longhand declarations, so every consumer
    ///     (the inline-style parser, the stylesheet parser and the presentation-attribute tier) sees
    ///     only the longhands the cascade already understands - and so omitted longhands are reset to
    ///     <c>normal</c> as the shorthand requires.
    /// </summary>
    /// <param name="value">The raw shorthand value.</param>
    /// <returns>
    ///     The longhand <c>(property, value)</c> pairs (<c>font-style</c>, <c>font-weight</c>,
    ///     <c>font-size</c>, <c>line-height</c> and, for a full shorthand, <c>font-family</c>), or
    ///     <see langword="null"/> when the shorthand is invalid or uses a system-font keyword: the
    ///     whole declaration is then dropped, as CSS requires for an invalid shorthand. The
    ///     <c>font-variant</c> and <c>font-stretch</c> components are parsed and ignored. Never throws.
    /// </returns>
    /// <remarks>
    ///     Grammar (CSS Fonts 4): optional style/variant/weight/stretch tokens in any order, each at
    ///     most once (<c>normal</c> may stand in for any unfilled slot, so it may repeat up to the
    ///     number of free slots), then the size, an optional <c>/ line-height</c>, then a mandatory
    ///     family list. A repeated component (<c>bold bold</c>, <c>italic oblique</c>) makes the
    ///     whole declaration invalid. A bare number before the size is a weight; the size itself must
    ///     carry a unit or be a keyword, a percentage or a math function (only <c>0</c> may be bare),
    ///     which is how the two are told apart. <c>oblique</c> may be
    ///     followed by an angle (<c>deg</c>, <c>grad</c>, <c>rad</c> or <c>turn</c>, within
    ///     <c>[-90deg, 90deg]</c>), which is validated and ignored; an invalid angle drops the whole
    ///     declaration. A CSS-wide keyword (<c>inherit</c>, <c>initial</c>, <c>unset</c>) expands to
    ///     that keyword for every longhand, <c>font-family</c> included.
    /// </remarks>
    private static List<(string Property, string Value)>? ExpandFontShorthand(string value)
    {
        var text = value.Trim();
        if (text.Length == 0 || text.Length > MaxLengthExpressionChars * 4)
        {
            return null;
        }

        // CSS-wide keywords and system fonts are whole-value forms. A CSS-wide keyword applies to
        // every longhand of the shorthand (CSS Values 4, "CSS-wide keywords"; CSS Fonts 4, "font"),
        // font-family included, and reaches the cascade as the keyword itself
        var lower = text.ToLowerInvariant();
        switch (lower)
        {
            case "inherit":
            case "unset":
            case "initial":
                return
                [
                    ("font-style", lower), ("font-weight", lower), ("font-size", lower),
                    ("line-height", lower), ("font-family", lower)
                ];
            case "revert":
            case "revert-layer":
                return null;
        }

        if (SystemFontKeywords.Contains(lower))
        {
            return null;
        }

        // Step 1: consume the optional style/variant/weight/stretch tokens until the size appears.
        // Each of the four components may appear at most once, in any order; 'normal' is a member of
        // all four and may stand in for any still-unfilled slot
        var prefix = new FontShorthandPrefix { Style = "normal", Weight = "normal" };
        var position = 0;
        string? size = null;
        var previousWasOblique = false;
        for (var examined = 0; examined < MaxFontShorthandPrefixTokens; examined++)
        {
            if (!TryReadFontShorthandToken(text, ref position, out var token))
            {
                return null;
            }

            // 'oblique' may be followed by an angle, which belongs to the style and is ignored; a
            // missing, malformed or out-of-range angle falls through to the size slot, fails its
            // syntax check and drops the whole shorthand
            if (previousWasOblique && TryParseObliqueAngle(token, out _))
            {
                previousWasOblique = false;
                continue;
            }

            var classification = ClassifyFontShorthandPrefixToken(token, ref prefix);
            if (classification == FontShorthandToken.Duplicate)
            {
                return null;
            }

            if (classification == FontShorthandToken.Accepted)
            {
                previousWasOblique = token.Equals("oblique", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            size = token;
            break;
        }

        if (size == null || !IsValidFontSizeSyntax(size) || prefix.ComponentCount > FontShorthandPrefixSlots)
        {
            return null;
        }

        var style = prefix.Style;
        var weight = prefix.Weight;

        // Step 2: optional "/ line-height"
        var lineHeight = "normal";
        var afterSize = position;
        if (TryReadFontShorthandToken(text, ref afterSize, out var slash) && slash == "/")
        {
            if (!TryReadFontShorthandToken(text, ref afterSize, out var lineHeightToken) ||
                !IsValidLineHeightSyntax(lineHeightToken))
            {
                return null;
            }

            lineHeight = lineHeightToken;
            position = afterSize;
        }

        // Step 3: everything left is the (mandatory) family list
        var family = text[position..].Trim();
        if (family.Length == 0 || GenericFontFamilies.SplitFamilyList(family).Count == 0)
        {
            return null;
        }

        return
        [
            ("font-style", style), ("font-weight", weight), ("font-size", size),
            ("line-height", lineHeight), ("font-family", family)
        ];
    }

    /// <summary>
    ///     Reads the next token of a <c>font</c> shorthand: a whitespace-delimited word that keeps
    ///     quoted strings and parenthesized groups (a <c>calc()</c> size) whole, with a top-level
    ///     <c>/</c> returned as its own token.
    /// </summary>
    /// <param name="text">The shorthand text.</param>
    /// <param name="position">The position to read from; advanced past the token on success.</param>
    /// <param name="token">The token text.</param>
    /// <returns><see langword="false"/> at the end of the text.</returns>
    private static bool TryReadFontShorthandToken(string text, ref int position, out string token)
    {
        token = string.Empty;
        while (position < text.Length && char.IsWhiteSpace(text[position]))
        {
            position++;
        }

        if (position >= text.Length)
        {
            return false;
        }

        var start = position;
        if (text[position] == '/')
        {
            position++;
            token = "/";
            return true;
        }

        var depth = 0;
        char quote = '\0';
        while (position < text.Length)
        {
            var c = text[position];
            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }
            }
            else if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth = Math.Max(0, depth - 1);
            }
            else if (depth == 0 && (char.IsWhiteSpace(c) || c == '/'))
            {
                break;
            }

            position++;
        }

        token = text[start..position];
        return true;
    }

    /// <summary>
    ///     Parses the <c>&lt;angle&gt;</c> of <c>font-style: oblique &lt;angle&gt;</c> (CSS Fonts 4):
    ///     a number immediately followed by <c>deg</c>, <c>grad</c> (400 per turn), <c>rad</c> or
    ///     <c>turn</c>, the unit matched ASCII case-insensitively (CSS Values 4, "Angle Units").
    /// </summary>
    /// <param name="token">The candidate token; no whitespace is allowed between the number and its unit.</param>
    /// <param name="degrees">The angle converted to degrees when this method returns <see langword="true"/>.</param>
    /// <returns>
    ///     <see langword="true"/> for a finite angle within <c>[-90deg, 90deg]</c> (the CSS range for
    ///     an oblique angle; a tiny epsilon keeps <c>100grad</c> and <c>0.25turn</c> exactly valid);
    ///     <see langword="false"/> for a unitless number (an angle has no legacy zero here), an
    ///     unknown unit, or an out-of-range or non-finite value.
    /// </returns>
    private static bool TryParseObliqueAngle(string token, out double degrees)
    {
        degrees = 0;
        var scan = 0;
        if (scan < token.Length && (token[scan] == '+' || token[scan] == '-'))
        {
            scan++;
        }

        var sawDigit = false;
        var sawDot = false;
        while (scan < token.Length && (char.IsAsciiDigit(token[scan]) || (token[scan] == '.' && !sawDot)))
        {
            if (token[scan] == '.')
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

        scan = TryConsumeExponent(token, scan);
        if (!double.TryParse(token.AsSpan(0, scan), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ||
            !double.IsFinite(number))
        {
            return false;
        }

        var unit = token.AsSpan(scan);
        double converted;
        if (unit.Equals("deg", StringComparison.OrdinalIgnoreCase))
        {
            converted = number;
        }
        else if (unit.Equals("grad", StringComparison.OrdinalIgnoreCase))
        {
            converted = number * 0.9;
        }
        else if (unit.Equals("rad", StringComparison.OrdinalIgnoreCase))
        {
            converted = number * 180.0 / Math.PI;
        }
        else if (unit.Equals("turn", StringComparison.OrdinalIgnoreCase))
        {
            converted = number * 360.0;
        }
        else
        {
            return false;
        }

        if (!double.IsFinite(converted) || Math.Abs(converted) > MaxObliqueDegrees + ObliqueAngleEpsilon)
        {
            return false;
        }

        degrees = converted;
        return true;
    }

    /// <summary>The number of optional components (style, variant, weight, stretch) a <c>font</c> shorthand may carry before the size.</summary>
    private const int FontShorthandPrefixSlots = 4;

    /// <summary>The outcome of classifying one pre-size <c>font</c> shorthand token.</summary>
    private enum FontShorthandToken
    {
        /// <summary>The token is not one of the optional components (it is the size).</summary>
        NotAComponent,

        /// <summary>The token filled a free component slot.</summary>
        Accepted,

        /// <summary>The token repeats a component already given, which makes the shorthand invalid.</summary>
        Duplicate
    }

    /// <summary>The optional pre-size components of a <c>font</c> shorthand collected so far.</summary>
    private struct FontShorthandPrefix
    {
        /// <summary>The style keyword, <c>normal</c> until one is given.</summary>
        public string Style;

        /// <summary>The weight keyword or number, <c>normal</c> until one is given.</summary>
        public string Weight;

        /// <summary>Whether a style (<c>italic</c>/<c>oblique</c>) has been given.</summary>
        public bool StyleSet;

        /// <summary>Whether a <c>small-caps</c> variant has been given.</summary>
        public bool VariantSet;

        /// <summary>Whether a weight has been given.</summary>
        public bool WeightSet;

        /// <summary>Whether a stretch keyword has been given.</summary>
        public bool StretchSet;

        /// <summary>The number of <c>normal</c> tokens, each filling one still-unfilled slot.</summary>
        public int NormalCount;

        /// <summary>Gets the number of slots consumed by specific keywords and <c>normal</c> tokens together.</summary>
        public readonly int ComponentCount =>
            (StyleSet ? 1 : 0) + (VariantSet ? 1 : 0) + (WeightSet ? 1 : 0) + (StretchSet ? 1 : 0) + NormalCount;
    }

    /// <summary>
    ///     Classifies one pre-size <c>font</c> shorthand token, recording a style or weight.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="prefix">The components collected so far; updated when the token is accepted.</param>
    /// <returns>
    ///     <see cref="FontShorthandToken.Accepted"/> when the token fills a free component slot,
    ///     <see cref="FontShorthandToken.Duplicate"/> when it repeats a component (CSS Fonts 4:
    ///     <c>||</c> allows each at most once), and <see cref="FontShorthandToken.NotAComponent"/>
    ///     when it is not one of the optional components (so it is the size). <c>normal</c> is
    ///     counted rather than slotted, because it is a member of all four components.
    /// </returns>
    private static FontShorthandToken ClassifyFontShorthandPrefixToken(string token, ref FontShorthandPrefix prefix)
    {
        if (token.Equals("normal", StringComparison.OrdinalIgnoreCase))
        {
            prefix.NormalCount++;
            return FontShorthandToken.Accepted;
        }

        if (token.Equals("small-caps", StringComparison.OrdinalIgnoreCase))
        {
            return Claim(ref prefix.VariantSet);
        }

        if (FontStretchKeywords.Contains(token))
        {
            return Claim(ref prefix.StretchSet);
        }

        if (token.Equals("italic", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("oblique", StringComparison.OrdinalIgnoreCase))
        {
            var result = Claim(ref prefix.StyleSet);
            if (result == FontShorthandToken.Accepted)
            {
                prefix.Style = token.ToLowerInvariant();
            }

            return result;
        }

        // A bare number in 1..1000 is a weight; a size always has a unit, so there is no ambiguity
        if (token.Equals("bold", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("bolder", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("lighter", StringComparison.OrdinalIgnoreCase) ||
            (float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) &&
             number is >= 1f and <= 1000f))
        {
            var result = Claim(ref prefix.WeightSet);
            if (result == FontShorthandToken.Accepted)
            {
                prefix.Weight = char.IsAsciiLetter(token[0]) ? token.ToLowerInvariant() : token;
            }

            return result;
        }

        return FontShorthandToken.NotAComponent;

        static FontShorthandToken Claim(ref bool slot)
        {
            if (slot)
            {
                return FontShorthandToken.Duplicate;
            }

            slot = true;
            return FontShorthandToken.Accepted;
        }
    }

    /// <summary>Validates that a <c>font</c> shorthand size token is a keyword or a well-formed length (a bad size drops the whole shorthand).</summary>
    /// <param name="token">The size token.</param>
    /// <returns>
    ///     <see langword="true"/> when the token is acceptable as a <c>font-size</c>: a keyword, or a
    ///     length, percentage or math result. A unitless number is a length only when it is zero (CSS
    ///     Values 4, <c>&lt;length&gt;</c>), so <c>0.5</c> is rejected while <c>0</c> is accepted.
    /// </returns>
    private static bool IsValidFontSizeSyntax(string token)
    {
        if (AbsoluteFontSizes.ContainsKey(token) ||
            token.Equals("larger", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("smaller", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!TryResolveLength(token, RenderState.Initial, PercentageAxis.FontSize, out _, out _, allowNumberResult: false))
        {
            return false;
        }

        var position = 0;
        return !(TryReadLengthToken(token, ref position, out var number, out var unit) &&
                 position == token.Length && unit == LengthUnit.None && number != 0);
    }

    /// <summary>Validates that a <c>font</c> shorthand line-height token is <c>normal</c>, a number or a well-formed length.</summary>
    /// <param name="token">The line-height token.</param>
    /// <returns><see langword="true"/> when the token is acceptable as a <c>line-height</c>.</returns>
    private static bool IsValidLineHeightSyntax(string token) =>
        token.Equals("normal", StringComparison.OrdinalIgnoreCase) ||
        (float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && float.IsFinite(number) && number >= 0f) ||
        TryResolveLengthOrNumber(token, RenderState.Initial, PercentageAxis.FontSize, out _, out _, out _);

    /// <summary>
    ///     Expands an element's <c>font</c> presentation attribute into longhand values, the lowest
    ///     tier of the font cascade: an explicit longhand attribute, an inline <c>style</c> or a
    ///     stylesheet rule all outrank it.
    /// </summary>
    /// <param name="element">The element to inspect.</param>
    /// <returns>The longhand values by property name, or <see langword="null"/> when the element has no valid <c>font</c> attribute.</returns>
    private static Dictionary<string, string>? ExpandFontAttribute(XElement element)
    {
        var raw = (string?)element.Attribute("font");
        var expanded = raw == null ? null : ExpandFontShorthand(raw);
        if (expanded == null)
        {
            return null;
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (property, value) in expanded)
        {
            result[property] = value;
        }

        return result;
    }

    /// <summary>
    ///     Reads the root <c>svg</c> element's own <c>font-size</c> for resolving a font-relative
    ///     root <c>width</c>/<c>height</c>, which must be known before any render state exists.
    /// </summary>
    /// <param name="root">The root element.</param>
    /// <returns>The root's font size: the first valid of its inline <c>style</c> (including the <c>font</c> shorthand), its <c>font-size</c> attribute, then its <c>font</c> attribute (an invalid one is dropped, not masking the next), else 16.</returns>
    /// <remarks>
    ///     Stylesheet rules are not consulted: the stylesheet is built after the document size is
    ///     known. A root whose size depends on a <c>&lt;style&gt;</c>-declared font size therefore
    ///     sizes against 16, a documented simplification that keeps <c>GetInfo</c> and <c>Load</c>
    ///     in agreement. Only the <c>font-size</c> attribute accepts a number-typed math result
    ///     (see <see cref="ParseFontSizeValue"/>), mirroring the element cascade so both entry
    ///     points agree.
    /// </remarks>
    private static float ReadRootFontSize(XElement root)
    {
        string? inlineSize = null;
        var inline = (string?)root.Attribute("style");
        if (inline != null)
        {
            // Invalid font-size declarations were dropped by the parser, so the last survivor wins
            foreach (var (property, value) in ParseDeclarationBlock(inline))
            {
                if (property == "font-size")
                {
                    inlineSize = value;
                }
            }
        }

        string? fontAttributeSize = null;
        ExpandFontAttribute(root)?.TryGetValue("font-size", out fontAttributeSize);
        return ResolveFontSize(inlineSize, (string?)root.Attribute("font-size"), fontAttributeSize, RenderState.Initial);
    }

    /// <summary>
    ///     Resolves an element's <c>font-size</c> from its sources in cascade order, skipping any
    ///     source whose value is invalid so an invalid declaration never masks a lower one.
    /// </summary>
    /// <param name="css">The CSS value (inline style, then stylesheet), or <see langword="null"/>.</param>
    /// <param name="attribute">The <c>font-size</c> presentation attribute, which alone accepts a number-typed math result, or <see langword="null"/>.</param>
    /// <param name="fontAttribute">The <c>font</c> attribute's expanded size, or <see langword="null"/>.</param>
    /// <param name="parent">The parent's state (the basis of <c>em</c>, <c>%</c>, <c>larger</c> and <c>smaller</c>).</param>
    /// <returns>
    ///     The size from the first valid source, else the parent's size. A CSS-wide keyword that has no
    ///     effect of its own (<c>revert</c>, <c>revert-layer</c>) is a valid declaration that inherits,
    ///     so it still masks the lower sources. Shared by the element cascade and
    ///     <see cref="ReadRootFontSize"/> so <c>GetInfo</c> and <c>Load</c> agree.
    /// </returns>
    private static float ResolveFontSize(string? css, string? attribute, string? fontAttribute, RenderState parent)
    {
        if (TryResolveFontSizeSource(css, parent, false, out var size) ||
            TryResolveFontSizeSource(attribute, parent, true, out size) ||
            TryResolveFontSizeSource(fontAttribute, parent, false, out size))
        {
            return size;
        }

        return parent.FontSize;
    }

    /// <summary>Resolves one <c>font-size</c> source; see <see cref="ResolveFontSize"/>.</summary>
    /// <param name="raw">The raw value, or <see langword="null"/> when the source is absent.</param>
    /// <param name="parent">The parent's state.</param>
    /// <param name="numberResultAllowed">See <see cref="ParseFontSizeValue"/>.</param>
    /// <param name="size">The resolved size when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the source is present and valid (including a no-effect CSS-wide keyword, which inherits).</returns>
    private static bool TryResolveFontSizeSource(string? raw, RenderState parent, bool numberResultAllowed, out float size)
    {
        size = parent.FontSize;
        if (raw == null)
        {
            return false;
        }

        if (ParseFontSizeValue(raw, parent, numberResultAllowed) is { } parsed)
        {
            size = parsed;
            return true;
        }

        return ParseCssWideKeyword(raw) != CssWideKeyword.None;
    }

    /// <summary>
    ///     Determines whether a font declaration's value is valid for its property, so the declaration
    ///     parser can drop an invalid one before it reaches the cascade (CSS Syntax 3: a declaration
    ///     whose value is invalid is dropped when parsed).
    /// </summary>
    /// <param name="property">The lower-cased property name.</param>
    /// <param name="value">The raw value.</param>
    /// <param name="state">The state a <c>line-height</c> value resolves against.</param>
    /// <returns>
    ///     <see langword="true"/> for any property other than <c>font-size</c>, <c>font-family</c>,
    ///     <c>font-weight</c>, <c>font-style</c> and <c>line-height</c>, and for a CSS-wide keyword.
    ///     Otherwise whether the value parses: <c>font-weight</c> also accepts <c>bolder</c> and
    ///     <c>lighter</c> (valid, but with no modeled effect), <c>font-family</c> needs at least one
    ///     family name, and <c>font-size</c> is checked as a CSS declaration (no number-typed math
    ///     result) against the initial state, since only a parent-dependent over-magnitude result can
    ///     differ, which the cascade handles by falling to the next source.
    /// </returns>
    private static bool IsValidFontLonghand(string property, string value, RenderState state)
    {
        if (property is not ("font-size" or "font-family" or "font-weight" or "font-style" or "line-height") ||
            ParseCssWideKeyword(value) != CssWideKeyword.None)
        {
            return true;
        }

        return property switch
        {
            "font-size" => ParseFontSizeValue(value, state, false) != null,
            "font-family" => GenericFontFamilies.SplitFamilyList(value).Count > 0,
            "font-weight" => ParseFontWeight(value) != null ||
                             value.Trim().Equals("bolder", StringComparison.OrdinalIgnoreCase) ||
                             value.Trim().Equals("lighter", StringComparison.OrdinalIgnoreCase),
            "font-style" => ParseFontStyle(value) != null,
            _ => TryParseLineHeight(value, state, out _)
        };
    }

    /// <summary>
    ///     Picks the first valid value of a font property from its sources, in cascade order.
    /// </summary>
    /// <param name="property">The property name.</param>
    /// <param name="state">The state used to validate a <c>line-height</c>.</param>
    /// <param name="css">The CSS value (inline style, then stylesheet), or <see langword="null"/>.</param>
    /// <param name="attribute">The presentation attribute value, or <see langword="null"/>.</param>
    /// <param name="fontAttribute">The <c>font</c> attribute's expanded value, or <see langword="null"/>.</param>
    /// <returns>The first present, valid value, or <see langword="null"/> when none is (the value is then inherited).</returns>
    private static string? FirstValidFontValue(string property, RenderState state, string? css, string? attribute, string? fontAttribute)
    {
        if (css != null && IsValidFontLonghand(property, css, state))
        {
            return css;
        }

        if (attribute != null && IsValidFontLonghand(property, attribute, state))
        {
            return attribute;
        }

        return fontAttribute != null && IsValidFontLonghand(property, fontAttribute, state) ? fontAttribute : null;
    }

    /// <summary>
    ///     Builds the throwaway state used to resolve a root <c>width</c>/<c>height</c>: only the
    ///     root font size is known, so a percentage or a viewport unit has no basis (it resolves to
    ///     NaN and the attribute is treated as absent) and <c>ex</c>/<c>ch</c> use their fallbacks.
    /// </summary>
    /// <param name="root">The root element.</param>
    /// <returns>The root-size resolution state.</returns>
    private static RenderState CreateRootSizeState(XElement root)
    {
        var fontSize = ReadRootFontSize(root);
        return RenderState.Initial with
        {
            FontSize = fontSize,
            RootFontSize = fontSize,
            ViewportWidth = float.NaN,
            ViewportHeight = float.NaN,
            RootViewportWidth = float.NaN,
            RootViewportHeight = float.NaN
        };
    }
}
