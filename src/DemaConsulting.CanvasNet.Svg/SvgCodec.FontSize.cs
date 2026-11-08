using System.Globalization;
using System.Xml.Linq;

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

    /// <summary>
    ///     Resolves a raw <c>font-size</c> value to user units against its parent's state.
    /// </summary>
    /// <param name="raw">The raw value (attribute, inline style, stylesheet rule or <c>font</c> expansion).</param>
    /// <param name="parent">
    ///     The parent element's cascaded state: <c>em</c>/<c>ex</c>/<c>ch</c>/<c>lh</c>, <c>%</c>,
    ///     <c>larger</c> and <c>smaller</c> are all relative to the PARENT for the <c>font-size</c>
    ///     property itself (CSS Fonts 4), unlike every other property.
    /// </param>
    /// <returns>
    ///     The resolved size; <see langword="null"/> when the value is invalid (an unknown unit,
    ///     garbage, a malformed expression, an over-magnitude result) or is a keyword with no
    ///     effect (<c>revert</c>, <c>revert-layer</c>), meaning "ignore the declaration and inherit"
    ///     per the CSS invalid-declaration rule (class F in this class's remarks). Never throws.
    /// </returns>
    /// <remarks>
    ///     <c>inherit</c> and <c>unset</c> (font-size is inherited) yield the parent's size;
    ///     <c>initial</c> and <c>medium</c> yield 16 regardless of the parent. A zero or negative
    ///     size is returned as-is: the text renderer draws nothing for it, as before.
    /// </remarks>
    private static float? ParseFontSizeValue(string raw, RenderState parent)
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
            return TryResolveLength(text, parent, PercentageAxis.FontSize, out var length, out _) ? length : null;
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
    ///     <c>initial</c>), a multiplier for a bare number, or an absolute length for a
    ///     length/percentage (resolved at the declaring element, as CSS specifies).
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

        if (!TryResolveLength(text, state, PercentageAxis.FontSize, out var length, out _) || length < 0f)
        {
            return false;
        }

        computed = new LineHeightSpec(length, IsMultiplier: false);
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
    ///     Grammar (CSS Fonts 4): optional style/weight/variant/stretch tokens in any order, then
    ///     the size, an optional <c>/ line-height</c>, then a mandatory family list. A bare number
    ///     before the size is a weight; the size itself must carry a unit or be a keyword, a
    ///     percentage or a math function, which is how the two are told apart. The CSS-wide
    ///     keywords expand to the longhands they affect here (<c>font-family</c> is left alone
    ///     because its value cannot be a keyword).
    /// </remarks>
    private static List<(string Property, string Value)>? ExpandFontShorthand(string value)
    {
        var text = value.Trim();
        if (text.Length == 0 || text.Length > MaxLengthExpressionChars * 4)
        {
            return null;
        }

        // CSS-wide keywords and system fonts are whole-value forms
        var lower = text.ToLowerInvariant();
        switch (lower)
        {
            case "inherit":
            case "unset":
                return
                [
                    ("font-style", lower), ("font-weight", lower), ("font-size", lower), ("line-height", lower)
                ];
            case "initial":
                return
                [
                    ("font-style", "normal"), ("font-weight", "normal"), ("font-size", "initial"), ("line-height", "normal")
                ];
            case "revert":
            case "revert-layer":
                return null;
        }

        if (SystemFontKeywords.Contains(lower))
        {
            return null;
        }

        // Step 1: consume the optional style/weight/variant/stretch tokens until the size appears
        var style = "normal";
        var weight = "normal";
        var position = 0;
        string? size = null;
        var previousWasOblique = false;
        for (var examined = 0; examined < MaxFontShorthandPrefixTokens; examined++)
        {
            if (!TryReadFontShorthandToken(text, ref position, out var token))
            {
                return null;
            }

            // 'oblique' may be followed by an angle, which belongs to the style and is ignored
            if (previousWasOblique && token.EndsWith("deg", StringComparison.OrdinalIgnoreCase))
            {
                previousWasOblique = false;
                continue;
            }

            if (IsFontShorthandPrefixToken(token, ref style, ref weight))
            {
                previousWasOblique = token.Equals("oblique", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            size = token;
            break;
        }

        if (size == null || !IsValidFontSizeSyntax(size))
        {
            return null;
        }

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
        if (family.Length == 0)
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
    ///     Classifies one pre-size <c>font</c> shorthand token, recording a style or weight.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="style">Updated when the token is a style keyword.</param>
    /// <param name="weight">Updated when the token is a weight keyword or number.</param>
    /// <returns><see langword="true"/> when the token is one of the optional components (so it is not the size).</returns>
    private static bool IsFontShorthandPrefixToken(string token, ref string style, ref string weight)
    {
        if (token.Equals("normal", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("small-caps", StringComparison.OrdinalIgnoreCase) ||
            FontStretchKeywords.Contains(token))
        {
            return true;
        }

        if (token.Equals("italic", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("oblique", StringComparison.OrdinalIgnoreCase))
        {
            style = token.ToLowerInvariant();
            return true;
        }

        if (token.Equals("bold", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("bolder", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("lighter", StringComparison.OrdinalIgnoreCase))
        {
            weight = token.ToLowerInvariant();
            return true;
        }

        // A bare number in 1..1000 is a weight; a size always has a unit, so there is no ambiguity
        if (float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) &&
            number is >= 1f and <= 1000f)
        {
            weight = token;
            return true;
        }

        return false;
    }

    /// <summary>Validates that a <c>font</c> shorthand size token is a keyword or a well-formed length (a bad size drops the whole shorthand).</summary>
    /// <param name="token">The size token.</param>
    /// <returns><see langword="true"/> when the token is acceptable as a <c>font-size</c>.</returns>
    private static bool IsValidFontSizeSyntax(string token) =>
        AbsoluteFontSizes.ContainsKey(token) ||
        token.Equals("larger", StringComparison.OrdinalIgnoreCase) ||
        token.Equals("smaller", StringComparison.OrdinalIgnoreCase) ||
        TryResolveLength(token, RenderState.Initial, PercentageAxis.FontSize, out _, out _);

    /// <summary>Validates that a <c>font</c> shorthand line-height token is <c>normal</c>, a number or a well-formed length.</summary>
    /// <param name="token">The line-height token.</param>
    /// <returns><see langword="true"/> when the token is acceptable as a <c>line-height</c>.</returns>
    private static bool IsValidLineHeightSyntax(string token) =>
        token.Equals("normal", StringComparison.OrdinalIgnoreCase) ||
        (float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && float.IsFinite(number) && number >= 0f) ||
        TryResolveLength(token, RenderState.Initial, PercentageAxis.FontSize, out _, out _);

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
    /// <returns>The root's font size: its inline <c>style</c> (including the <c>font</c> shorthand), else its <c>font-size</c> attribute, else its <c>font</c> attribute, else 16.</returns>
    /// <remarks>
    ///     Stylesheet rules are not consulted: the stylesheet is built after the document size is
    ///     known. A root whose size depends on a <c>&lt;style&gt;</c>-declared font size therefore
    ///     sizes against 16, a documented simplification that keeps <c>GetInfo</c> and <c>Load</c>
    ///     in agreement.
    /// </remarks>
    private static float ReadRootFontSize(XElement root)
    {
        string? raw = null;
        var inline = (string?)root.Attribute("style");
        if (inline != null)
        {
            foreach (var (property, value) in ParseDeclarationBlock(inline))
            {
                if (property == "font-size")
                {
                    raw = value;
                }
            }
        }

        raw ??= (string?)root.Attribute("font-size");
        if (raw == null && ExpandFontAttribute(root) is { } fontAttribute)
        {
            fontAttribute.TryGetValue("font-size", out raw);
        }

        return (raw == null ? null : ParseFontSizeValue(raw, RenderState.Initial)) ?? MediumFontSize;
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
