using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Mermaid.Configuration;

/// <summary>
///     One CSS declaration: a property, its value and whether it is <c>!important</c>.
/// </summary>
/// <param name="Property">The property name in lower case.</param>
/// <param name="Value">The value, trimmed, without <c>!important</c>.</param>
/// <param name="Important">Whether the declaration is <c>!important</c>.</param>
internal sealed record StyleDeclaration(string Property, string Value, bool Important);

/// <summary>
///     The declarations that apply to one element class, after the cascade: at most one declaration per property.
/// </summary>
/// <remarks>
///     A later declaration of a property replaces an earlier one, except that an <c>!important</c> declaration is only
///     replaced by another <c>!important</c> one (the CSS cascade for equal specificity). Instances are immutable.
/// </remarks>
internal sealed class StyleDeclarationSet
{
    /// <summary>The declarations by property, in the order the properties first appeared.</summary>
    private readonly List<StyleDeclaration> _declarations;

    /// <summary>
    ///     Initializes a set from declarations in cascade order.
    /// </summary>
    /// <param name="declarations">The declarations; later ones win as the cascade decides.</param>
    public StyleDeclarationSet(IEnumerable<StyleDeclaration> declarations)
    {
        ArgumentNullException.ThrowIfNull(declarations);
        _declarations = [];
        foreach (var declaration in declarations)
        {
            Apply(_declarations, declaration);
        }
    }

    /// <summary>Gets the empty set.</summary>
    public static StyleDeclarationSet Empty { get; } = new([]);

    /// <summary>Gets the declarations.</summary>
    public IReadOnlyList<StyleDeclaration> Declarations => _declarations;

    /// <summary>
    ///     Gets the value of a property, or null when it is not declared.
    /// </summary>
    /// <param name="property">The property name in lower case.</param>
    /// <returns>The value, or null.</returns>
    public string? this[string property] => Find(property)?.Value;

    /// <summary>
    ///     Gets whether a property is declared <c>!important</c>.
    /// </summary>
    /// <param name="property">The property name.</param>
    /// <returns>Whether it is.</returns>
    public bool IsImportant(string property) => Find(property)?.Important ?? false;

    /// <summary>
    ///     Applies another set over this one (the other set comes later in the cascade).
    /// </summary>
    /// <param name="overlay">The later declarations.</param>
    /// <returns>The combined set.</returns>
    public StyleDeclarationSet Cascade(StyleDeclarationSet overlay)
    {
        ArgumentNullException.ThrowIfNull(overlay);
        return new StyleDeclarationSet(_declarations.Concat(overlay._declarations));
    }

    /// <summary>
    ///     Gets a color property: the color, <see langword="null"/> for <c>none</c>, or the fallback when the property is
    ///     not declared.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <param name="fallback">The value when the property is not declared.</param>
    /// <returns>The color, or null for no paint.</returns>
    public Rgba32? Paint(string property, Rgba32? fallback = null)
    {
        var value = this[property];
        if (value is null)
        {
            return fallback;
        }

        return StyleDeclarations.TryParsePaint(value, out var paint) ? paint : fallback;
    }

    /// <summary>
    ///     Gets a length property in pixels, or the fallback when it is not declared.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <param name="fallback">The value when the property is not declared.</param>
    /// <param name="fontSize">The font size that <c>em</c> refers to.</param>
    /// <returns>The length.</returns>
    public double Length(string property, double fallback, double fontSize = 16) =>
        this[property] is { } value && StyleDeclarations.TryParseLength(value, fontSize, out var length) ? length : fallback;

    /// <summary>
    ///     Applies a declaration to a list with the cascade rule.
    /// </summary>
    /// <param name="list">The list.</param>
    /// <param name="declaration">The declaration.</param>
    private static void Apply(List<StyleDeclaration> list, StyleDeclaration declaration)
    {
        var index = list.FindIndex(existing => existing.Property == declaration.Property);
        if (index < 0)
        {
            list.Add(declaration);
        }
        else if (declaration.Important || !list[index].Important)
        {
            list[index] = declaration;
        }
    }

    /// <summary>
    ///     Finds the declaration of a property.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <returns>The declaration, or null.</returns>
    private StyleDeclaration? Find(string property) => _declarations.Find(declaration => declaration.Property == property);
}

/// <summary>
///     A style rule: a selector and its declarations.
/// </summary>
/// <param name="Selector">The normalized selector.</param>
/// <param name="Declarations">The declarations.</param>
internal sealed record StyleRule(string Selector, StyleDeclarationSet Declarations);

/// <summary>
///     The small CSS subset that Mermaid's <c>style</c>, <c>classDef</c>, <c>linkStyle</c> and <c>themeCSS</c> carry
///     here: declarations of a fixed set of properties, <c>!important</c>, and rules whose selector is one of the
///     diagram's own selectors.
/// </summary>
/// <remarks>
///     <para>
///         <b>Supported properties</b> (<see cref="SupportedProperties"/>): <c>fill</c>, <c>stroke</c>,
///         <c>stroke-width</c>, <c>stroke-dasharray</c>, <c>color</c>, <c>font-weight</c>, <c>font-style</c>,
///         <c>font-size</c>, <c>font-family</c> and <c>opacity</c>. Any other property, a declaration without a colon
///         and a value the property cannot have (a browser drops those too) are reported as
///         <see cref="MermaidDiagnosticCodes.StyleIgnored"/> and left out, so the declaration below it in the cascade
///         stays in effect.
///     </para>
///     <para>
///         <b>Style sheets</b> (<c>themeCSS</c>): comments are removed, and a rule whose selector is one of the known
///         selectors of the diagram's style sheet is applied after the diagram's own rules (as Mermaid appends the user
///         styles). Any other selector, an at-rule and a nested rule are reported as the unsupported feature
///         <see cref="MermaidDiagnosticCodes.ThemeCssUnsupported"/> and not applied.
///     </para>
///     <para>
///         The class is stateless and safe for concurrent use. Parsing is linear in the length of the text.
///     </para>
/// </remarks>
internal static class StyleDeclarations
{
    /// <summary>The properties a user declaration may set.</summary>
    public static readonly FrozenSet<string> SupportedProperties = new[]
    {
        "fill", "stroke", "stroke-width", "stroke-dasharray", "color", "font-weight", "font-style", "font-size", "font-family", "opacity",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>The suffix that makes a declaration important.</summary>
    private const string ImportantKeyword = "important";

    /// <summary>
    ///     Parses the items of a <c>style</c>, <c>classDef</c> or <c>linkStyle</c> statement (each <c>property:value</c>,
    ///     as Mermaid's parser splits them at commas).
    /// </summary>
    /// <param name="items">The items.</param>
    /// <param name="location">The location of the statement.</param>
    /// <param name="sink">The report sink, or null.</param>
    /// <returns>The declarations.</returns>
    public static StyleDeclarationSet ParseItems(IEnumerable<string> items, MermaidLocation location, MermaidReportSink? sink)
    {
        ArgumentNullException.ThrowIfNull(items);
        var declarations = new List<StyleDeclaration>();
        foreach (var item in items)
        {
            if (ParseDeclaration(item, restrict: true, location, sink) is { } declaration)
            {
                declarations.Add(declaration);
            }
        }

        return new StyleDeclarationSet(declarations);
    }

    /// <summary>
    ///     Parses a declaration block (<c>property: value; ...</c>).
    /// </summary>
    /// <param name="text">The text between the braces.</param>
    /// <param name="restrict">Whether only <see cref="SupportedProperties"/> are accepted (user text) or every property is (the diagram's own rules).</param>
    /// <param name="location">The location for reports.</param>
    /// <param name="sink">The report sink, or null.</param>
    /// <returns>The declarations.</returns>
    public static StyleDeclarationSet ParseBlock(string text, bool restrict, MermaidLocation location, MermaidReportSink? sink)
    {
        ArgumentNullException.ThrowIfNull(text);
        var declarations = new List<StyleDeclaration>();
        foreach (var part in SplitTopLevel(text, ';'))
        {
            if (part.Trim().Length > 0 && ParseDeclaration(part, restrict, location, sink) is { } declaration)
            {
                declarations.Add(declaration);
            }
        }

        return new StyleDeclarationSet(declarations);
    }

    /// <summary>
    ///     Parses a style sheet (<c>themeCSS</c>) into the rules whose selectors are known; the rest is reported.
    /// </summary>
    /// <param name="css">The style sheet.</param>
    /// <param name="knownSelectors">The normalized selectors of the diagram's style sheet.</param>
    /// <param name="location">The location of the style sheet's configuration.</param>
    /// <param name="sink">The report sink, or null.</param>
    /// <returns>The rules, one per known selector, in order.</returns>
    /// <exception cref="MermaidUnsupportedFeatureException">Thrown in strict mode at the first unsupported rule.</exception>
    public static IReadOnlyList<StyleRule> ParseSheet(string css, IReadOnlySet<string> knownSelectors, MermaidLocation location, MermaidReportSink? sink)
    {
        ArgumentNullException.ThrowIfNull(css);
        ArgumentNullException.ThrowIfNull(knownSelectors);
        var text = RemoveComments(css);
        var rules = new List<StyleRule>();
        var position = 0;
        while (position < text.Length)
        {
            var open = text.IndexOf('{', position);
            if (open < 0)
            {
                break;
            }

            var prelude = text[position..open].Trim();
            var close = MatchingBrace(text, open);
            var body = text[(open + 1)..close];
            position = Math.Min(close + 1, text.Length);

            // The text left by sanitizeCss for unbalanced braces, and empty rules, apply nothing
            if (prelude.Length == 0)
            {
                continue;
            }

            if (prelude.StartsWith('@') || body.Contains('{', StringComparison.Ordinal))
            {
                Unsupported(sink, $"the themeCSS rule '{Shorten(prelude)}' is an at-rule or has nested rules; it is not applied", location);
                continue;
            }

            var declarations = ParseBlock(body, restrict: true, location, sink);
            foreach (var selector in SplitTopLevel(prelude, ',').Select(NormalizeSelector))
            {
                if (knownSelectors.Contains(selector))
                {
                    rules.Add(new StyleRule(selector, declarations));
                }
                else
                {
                    Unsupported(sink, $"the themeCSS selector '{Shorten(selector)}' is not one of the diagram's own selectors; its rule is not applied", location);
                }
            }
        }

        return rules;
    }

    /// <summary>
    ///     Normalizes a selector: white space collapsed to one space and around the child combinator.
    /// </summary>
    /// <param name="selector">The selector.</param>
    /// <returns>The normalized selector.</returns>
    public static string NormalizeSelector(string selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        var builder = new StringBuilder(selector.Length);
        var pendingSpace = false;
        foreach (var c in selector.Trim())
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = true;
                continue;
            }

            if (c == '>')
            {
                builder.Append(" > ");
                pendingSpace = false;
                continue;
            }

            if (pendingSpace && builder.Length > 0 && builder[^1] != ' ')
            {
                builder.Append(' ');
            }

            pendingSpace = false;
            builder.Append(c);
        }

        return builder.ToString().Trim();
    }

    /// <summary>
    ///     Parses a paint: a CSS color, or <c>none</c> (no paint, given as null).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="paint">The color, or null for <c>none</c>.</param>
    /// <returns>Whether the value is a paint.</returns>
    public static bool TryParsePaint(string value, out Rgba32? paint)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Trim().Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            paint = null;
            return true;
        }

        if (CssColor.TryParse(value, out var color))
        {
            paint = color;
            return true;
        }

        paint = null;
        return false;
    }

    /// <summary>
    ///     Parses a length in pixels: a number with the unit <c>px</c>, <c>pt</c>, <c>em</c> or <c>rem</c>, or none.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="fontSize">The font size that <c>em</c> refers to.</param>
    /// <param name="length">The length in pixels.</param>
    /// <returns>Whether the value is a non-negative length.</returns>
    public static bool TryParseLength(string value, double fontSize, out double length)
    {
        ArgumentNullException.ThrowIfNull(value);
        length = 0;
        var text = value.Trim().ToLowerInvariant();
        var (number, scale) = text switch
        {
            _ when text.EndsWith("rem", StringComparison.Ordinal) => (text[..^3], 16.0),
            _ when text.EndsWith("px", StringComparison.Ordinal) => (text[..^2], 1.0),
            _ when text.EndsWith("pt", StringComparison.Ordinal) => (text[..^2], 4.0 / 3),
            _ when text.EndsWith("em", StringComparison.Ordinal) => (text[..^2], fontSize),
            _ => (text, 1.0),
        };
        if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) || !double.IsFinite(parsed) || parsed < 0)
        {
            return false;
        }

        length = parsed * scale;
        return true;
    }

    /// <summary>
    ///     Parses a dash array: <c>none</c> or <c>0</c> (solid, an empty list), or non-negative lengths separated by commas
    ///     or white space (an odd count is repeated, as SVG specifies).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="dashes">The dash lengths, empty for a solid line.</param>
    /// <returns>Whether the value is a dash array.</returns>
    public static bool TryParseDashArray(string value, out IReadOnlyList<double> dashes)
    {
        ArgumentNullException.ThrowIfNull(value);
        dashes = [];
        var text = value.Trim();
        if (text.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var parts = text.Split([',', ' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        var list = new List<double>();
        foreach (var part in parts)
        {
            if (!TryParseLength(part, 16, out var length))
            {
                return false;
            }

            list.Add(length);
        }

        if (list.Count == 0)
        {
            return false;
        }

        // A list whose lengths are all zero draws a solid line
        if (list.TrueForAll(length => length <= 0))
        {
            return true;
        }

        if (list.Count % 2 == 1)
        {
            list.AddRange(list.ToList());
        }

        dashes = list;
        return true;
    }

    /// <summary>
    ///     Parses an opacity: a number or a percentage, clamped to 0 to 1.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="opacity">The opacity.</param>
    /// <returns>Whether the value is an opacity.</returns>
    public static bool TryParseOpacity(string value, out double opacity)
    {
        ArgumentNullException.ThrowIfNull(value);
        var text = value.Trim();
        var percent = text.EndsWith('%');
        if (!double.TryParse(percent ? text[..^1] : text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) || !double.IsFinite(parsed))
        {
            opacity = 1;
            return false;
        }

        opacity = Math.Clamp(percent ? parsed / 100 : parsed, 0, 1);
        return true;
    }

    /// <summary>
    ///     Parses a font weight into whether it is bold (<c>bold</c>, <c>bolder</c> or 600 and more).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="bold">Whether it is bold.</param>
    /// <returns>Whether the value is a font weight.</returns>
    public static bool TryParseBold(string value, out bool bold)
    {
        ArgumentNullException.ThrowIfNull(value);
        var text = value.Trim().ToLowerInvariant();
        switch (text)
        {
            case "normal" or "lighter":
                bold = false;
                return true;
            case "bold" or "bolder":
                bold = true;
                return true;
        }

        var valid = double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var weight) && weight is >= 1 and <= 1000;
        bold = valid && weight >= 600;
        return valid;
    }

    /// <summary>
    ///     Parses a font style into whether it is italic (<c>italic</c> or <c>oblique</c>).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="italic">Whether it is italic.</param>
    /// <returns>Whether the value is a font style.</returns>
    public static bool TryParseItalic(string value, out bool italic)
    {
        ArgumentNullException.ThrowIfNull(value);
        var text = value.Trim().ToLowerInvariant();
        italic = text.StartsWith("italic", StringComparison.Ordinal) || text.StartsWith("oblique", StringComparison.Ordinal);
        return italic || text == "normal";
    }

    /// <summary>
    ///     Parses one declaration.
    /// </summary>
    /// <param name="text">The declaration text.</param>
    /// <param name="restrict">Whether only the supported properties are accepted, and values are checked.</param>
    /// <param name="location">The location for reports.</param>
    /// <param name="sink">The report sink, or null.</param>
    /// <returns>The declaration, or null when it is ignored.</returns>
    private static StyleDeclaration? ParseDeclaration(string text, bool restrict, MermaidLocation location, MermaidReportSink? sink)
    {
        var colon = text.IndexOf(':', StringComparison.Ordinal);
        var property = colon > 0 ? text[..colon].Trim().ToLowerInvariant() : string.Empty;
        if (property.Length == 0)
        {
            Ignored(sink, $"'{Shorten(text.Trim())}' is not a style declaration (property:value); it is ignored", location);
            return null;
        }

        // The value, with !important (white space may follow the '!') taken off
        var value = text[(colon + 1)..].Trim();
        var important = false;
        var bang = value.LastIndexOf('!');
        if (bang >= 0 && value[(bang + 1)..].Trim().Equals(ImportantKeyword, StringComparison.OrdinalIgnoreCase))
        {
            important = true;
            value = value[..bang].Trim();
        }

        if (!restrict)
        {
            return value.Length == 0 ? null : new StyleDeclaration(property, value, important);
        }

        if (!SupportedProperties.Contains(property))
        {
            Ignored(sink, $"the style property '{Shorten(property)}' is not supported; it is ignored", location, "use fill, stroke, stroke-width, stroke-dasharray, color, font-weight, font-style, font-size, font-family or opacity");
            return null;
        }

        if (!IsValidValue(property, value))
        {
            Ignored(sink, $"'{Shorten(value)}' is not a valid value of the style property '{property}'; it is ignored", location);
            return null;
        }

        return new StyleDeclaration(property, value, important);
    }

    /// <summary>
    ///     Gets whether a value is valid for a supported property (a browser drops invalid declarations).
    /// </summary>
    /// <param name="property">The property.</param>
    /// <param name="value">The value.</param>
    /// <returns>Whether it is valid.</returns>
    private static bool IsValidValue(string property, string value) => property switch
    {
        "fill" or "stroke" => TryParsePaint(value, out _),
        "color" => CssColor.TryParse(value, out _),
        "stroke-width" or "font-size" => TryParseLength(value, 16, out _),
        "stroke-dasharray" => TryParseDashArray(value, out _),
        "opacity" => TryParseOpacity(value, out _),
        "font-weight" => TryParseBold(value, out _),
        "font-style" => TryParseItalic(value, out _),
        _ => value.Length > 0,
    };

    /// <summary>
    ///     Splits a text at a separator that is not inside quotes or parentheses.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="separator">The separator.</param>
    /// <returns>The parts.</returns>
    private static List<string> SplitTopLevel(string text, char separator)
    {
        var parts = new List<string>();
        var depth = 0;
        char quote = '\0';
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quote != '\0')
            {
                quote = c == quote ? '\0' : quote;
            }
            else if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c is '(' or '[')
            {
                depth++;
            }
            else if (c is ')' or ']' && depth > 0)
            {
                depth--;
            }
            else if (c == separator && depth == 0)
            {
                parts.Add(text[start..i]);
                start = i + 1;
            }
        }

        parts.Add(text[start..]);
        return parts;
    }

    /// <summary>
    ///     Removes the <c>/* ... */</c> comments of a style sheet (an unterminated comment runs to the end).
    /// </summary>
    /// <param name="css">The style sheet.</param>
    /// <returns>The style sheet without comments.</returns>
    private static string RemoveComments(string css)
    {
        var builder = new StringBuilder(css.Length);
        var position = 0;
        while (position < css.Length)
        {
            var start = css.IndexOf("/*", position, StringComparison.Ordinal);
            if (start < 0)
            {
                builder.Append(css, position, css.Length - position);
                break;
            }

            builder.Append(css, position, start - position);
            var end = css.IndexOf("*/", start + 2, StringComparison.Ordinal);
            position = end < 0 ? css.Length : end + 2;
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Finds the brace that closes the one at a position (or the end of the text).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="open">The position of the opening brace.</param>
    /// <returns>The position of the closing brace, or the length of the text.</returns>
    private static int MatchingBrace(string text, int open)
    {
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '{')
            {
                depth++;
            }
            else if (text[i] == '}' && --depth == 0)
            {
                return i;
            }
        }

        return text.Length;
    }

    /// <summary>
    ///     Reports an ignored declaration.
    /// </summary>
    /// <param name="sink">The sink, or null.</param>
    /// <param name="message">The message.</param>
    /// <param name="location">The location.</param>
    /// <param name="hint">The hint, or null.</param>
    private static void Ignored(MermaidReportSink? sink, string message, MermaidLocation location, string? hint = null) =>
        sink?.Warn(MermaidDiagnosticCodes.StyleIgnored, message, location, hint);

    /// <summary>
    ///     Reports an unsupported style sheet rule (strict mode throws).
    /// </summary>
    /// <param name="sink">The sink, or null.</param>
    /// <param name="message">The message.</param>
    /// <param name="location">The location.</param>
    private static void Unsupported(MermaidReportSink? sink, string message, MermaidLocation location) =>
        sink?.Unsupported(
            MermaidDiagnosticCodes.ThemeCssUnsupported,
            message,
            location,
            "use themeVariables, classDef or style to change colors; themeCSS supports the diagram's own selectors such as '.node rect'");

    /// <summary>
    ///     Shortens a text for a message.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text, cut after 60 characters.</returns>
    private static string Shorten(string text)
    {
        const int MaxLength = 60;
        return text.Length <= MaxLength ? text : text[..MaxLength] + "...";
    }
}
