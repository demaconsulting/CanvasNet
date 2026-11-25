using System.Text;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.MathMl.Syntax;

/// <summary>
///     How a <c>math-depth</c> value (or a <c>scriptlevel</c> attribute) changes the inherited depth.
/// </summary>
internal enum MathDepthKind
{
    /// <summary><c>auto-add</c>: one more than the inherited depth when the inherited math style is compact.</summary>
    AutoAdd,

    /// <summary><c>add(n)</c>: the inherited depth plus n.</summary>
    Add,

    /// <summary><c>n</c>: the depth n.</summary>
    Set,
}

/// <summary>
///     A specified <c>math-depth</c> value.
/// </summary>
/// <param name="Kind">How the value changes the inherited depth.</param>
/// <param name="Value">The integer of <see cref="MathDepthKind.Add"/> or <see cref="MathDepthKind.Set"/>; zero for <see cref="MathDepthKind.AutoAdd"/>.</param>
internal readonly record struct MathDepthValue(MathDepthKind Kind, int Value);

/// <summary>
///     A problem of one declaration of an inline <c>style</c> attribute, reported by the caller with the location of the
///     attribute.
/// </summary>
/// <param name="IsUnsupported">True for a declaration outside the supported subset (<see cref="MathMlDiagnosticCodes.CssUnsupported"/>), false for an invalid value of a supported property (<see cref="MathMlDiagnosticCodes.AttributeInvalid"/>).</param>
/// <param name="Message">The description of the problem.</param>
internal readonly record struct StyleIssue(bool IsUnsupported, string Message);

/// <summary>
///     The supported declarations of an inline <c>style</c> attribute.
/// </summary>
/// <remarks>
///     <para>
///         A rendering has no style sheets, so only the inline <c>style</c> attribute can style an equation, and only this
///         subset of it: <c>color</c>, <c>background-color</c>, <c>font-size</c> (a length, a percentage or <c>math</c>),
///         <c>math-style</c> (<c>normal</c> or <c>compact</c>), <c>math-depth</c> (<c>auto-add</c>, <c>add(n)</c> or
///         <c>n</c>), <c>font-style</c> (<c>normal</c> or <c>italic</c>) and <c>display: none</c>. Every other
///         declaration is an issue that the caller reports and ignores, as is an invalid value of a supported property; a
///         later declaration of the same property wins, as in CSS.
///     </para>
///     <para>
///         The parser is a tolerant CSS declaration-list tokenizer: declarations are separated by semicolons outside
///         parentheses and quotes, comments are removed, and <c>!important</c> is ignored. The record is immutable.
///     </para>
/// </remarks>
/// <param name="Color">The <c>color</c>, or null when not declared.</param>
/// <param name="BackgroundColor">The <c>background-color</c>, or null when not declared.</param>
/// <param name="FontSize">The <c>font-size</c> length (relative units resolve against the inherited size), or null when not declared or <c>math</c>.</param>
/// <param name="DisplayStyle">True for <c>math-style: normal</c>, false for <c>compact</c>, null when not declared.</param>
/// <param name="MathDepth">The <c>math-depth</c>, or null when not declared.</param>
/// <param name="Italic">True for <c>font-style: italic</c>, false for <c>normal</c>, null when not declared.</param>
/// <param name="DisplayNone">True for <c>display: none</c>.</param>
/// <param name="Issues">The declarations that are reported and ignored, in order.</param>
internal sealed record StyleDeclarations(
    Rgba32? Color,
    Rgba32? BackgroundColor,
    MathLength? FontSize,
    bool? DisplayStyle,
    MathDepthValue? MathDepth,
    bool? Italic,
    bool DisplayNone,
    IReadOnlyList<StyleIssue> Issues)
{
    /// <summary>
    ///     Gets the empty declaration list.
    /// </summary>
    public static StyleDeclarations Empty { get; } = new(null, null, null, null, null, null, false, []);

    /// <summary>
    ///     Parses the value of a <c>style</c> attribute.
    /// </summary>
    /// <param name="text">The attribute value.</param>
    /// <returns>The supported declarations and the issues of the others.</returns>
    public static StyleDeclarations Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var builder = new Builder();
        foreach (var declaration in SplitDeclarations(StripComments(text)))
        {
            var colon = declaration.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0)
            {
                builder.Issues.Add(new StyleIssue(false, $"the declaration '{declaration}' has no property name and value"));
                continue;
            }

            var property = declaration[..colon].Trim().ToLowerInvariant();
            var value = declaration[(colon + 1)..].Trim();
            if (value.EndsWith("!important", StringComparison.OrdinalIgnoreCase))
            {
                value = value[..^"!important".Length].TrimEnd();
            }

            builder.Apply(property, value);
        }

        return builder.Build();
    }

    /// <summary>
    ///     Removes the CSS comments of a text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text without comments; an unclosed comment runs to the end.</returns>
    private static string StripComments(string text)
    {
        var result = new StringBuilder(text.Length);
        var index = 0;
        while (index < text.Length)
        {
            var start = text.IndexOf("/*", index, StringComparison.Ordinal);
            if (start < 0)
            {
                result.Append(text, index, text.Length - index);
                break;
            }

            result.Append(text, index, start - index);
            var end = text.IndexOf("*/", start + 2, StringComparison.Ordinal);
            index = end < 0 ? text.Length : end + 2;
        }

        return result.ToString();
    }

    /// <summary>
    ///     Splits a declaration list at the semicolons that are outside parentheses and quotes.
    /// </summary>
    /// <param name="text">The declaration list.</param>
    /// <returns>The non-empty declarations, trimmed.</returns>
    private static IEnumerable<string> SplitDeclarations(string text)
    {
        var depth = 0;
        var quote = '\0';
        var start = 0;
        for (var i = 0; i <= text.Length; i++)
        {
            var c = i < text.Length ? text[i] : ';';
            if (quote != 0)
            {
                quote = c == quote ? '\0' : quote;
                continue;
            }

            switch (c)
            {
                case '"' or '\'':
                    quote = c;
                    break;
                case '(':
                    depth++;
                    break;
                case ')':
                    depth = Math.Max(0, depth - 1);
                    break;
                case ';' when depth == 0 || i == text.Length:
                    var declaration = text[start..i].Trim();
                    if (declaration.Length > 0)
                    {
                        yield return declaration;
                    }

                    start = i + 1;
                    break;
            }
        }
    }

    /// <summary>
    ///     The mutable state of one parse.
    /// </summary>
    private sealed class Builder
    {
        /// <summary>Gets the issues in order.</summary>
        public List<StyleIssue> Issues { get; } = [];

        /// <summary>Gets or sets the color.</summary>
        private Rgba32? Color { get; set; }

        /// <summary>Gets or sets the background color.</summary>
        private Rgba32? BackgroundColor { get; set; }

        /// <summary>Gets or sets the font size.</summary>
        private MathLength? FontSize { get; set; }

        /// <summary>Gets or sets the math style.</summary>
        private bool? DisplayStyle { get; set; }

        /// <summary>Gets or sets the math depth.</summary>
        private MathDepthValue? MathDepth { get; set; }

        /// <summary>Gets or sets the font style.</summary>
        private bool? Italic { get; set; }

        /// <summary>Gets or sets a value indicating whether the element is not displayed.</summary>
        private bool DisplayNone { get; set; }

        /// <summary>
        ///     Applies one declaration.
        /// </summary>
        /// <param name="property">The property name, lowercase.</param>
        /// <param name="value">The value, trimmed.</param>
        public void Apply(string property, string value)
        {
            switch (property)
            {
                case "color":
                    Color = ParseColor(property, value) ?? Color;
                    break;
                case "background-color":
                    BackgroundColor = ParseColor(property, value) ?? BackgroundColor;
                    break;
                case "font-size":
                    ApplyFontSize(value);
                    break;
                case "math-style":
                    ApplyMathStyle(value);
                    break;
                case "math-depth":
                    ApplyMathDepth(value);
                    break;
                case "font-style":
                    ApplyFontStyle(value);
                    break;
                case "display":
                    ApplyDisplay(value);
                    break;
                default:
                    Issues.Add(new StyleIssue(true, $"the CSS property '{property}' is not supported; only color, background-color, font-size, math-style, math-depth, font-style and display: none are"));
                    break;
            }
        }

        /// <summary>
        ///     Creates the immutable result.
        /// </summary>
        /// <returns>The declarations.</returns>
        public StyleDeclarations Build() =>
            new(Color, BackgroundColor, FontSize, DisplayStyle, MathDepth, Italic, DisplayNone, [.. Issues]);

        /// <summary>
        ///     Parses a color value, recording an issue when it is invalid.
        /// </summary>
        /// <param name="property">The property name.</param>
        /// <param name="value">The value.</param>
        /// <returns>The color, or null when invalid.</returns>
        private Rgba32? ParseColor(string property, string value)
        {
            if (AttributeValues.TryParseColor(value, out var color))
            {
                return color;
            }

            Invalid(property, value);
            return null;
        }

        /// <summary>
        ///     Applies a <c>font-size</c> value: a length, a percentage, or <c>math</c> (the default).
        /// </summary>
        /// <param name="value">The value.</param>
        private void ApplyFontSize(string value)
        {
            if (value.Equals("math", StringComparison.OrdinalIgnoreCase))
            {
                FontSize = null;
            }
            else if (AttributeValues.TryParseLength(value, out var length) && length.Value >= 0)
            {
                FontSize = length;
            }
            else
            {
                Invalid("font-size", value);
            }
        }

        /// <summary>
        ///     Applies a <c>math-style</c> value.
        /// </summary>
        /// <param name="value">The value.</param>
        private void ApplyMathStyle(string value)
        {
            if (value.Equals("normal", StringComparison.OrdinalIgnoreCase))
            {
                DisplayStyle = true;
            }
            else if (value.Equals("compact", StringComparison.OrdinalIgnoreCase))
            {
                DisplayStyle = false;
            }
            else
            {
                Invalid("math-style", value);
            }
        }

        /// <summary>
        ///     Applies a <c>math-depth</c> value: <c>auto-add</c>, <c>add(n)</c> or <c>n</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        private void ApplyMathDepth(string value)
        {
            if (value.Equals("auto-add", StringComparison.OrdinalIgnoreCase))
            {
                MathDepth = new MathDepthValue(MathDepthKind.AutoAdd, 0);
            }
            else if (value.StartsWith("add(", StringComparison.OrdinalIgnoreCase) && value.EndsWith(')') && TryParseInteger(value[4..^1], out var change))
            {
                MathDepth = new MathDepthValue(MathDepthKind.Add, change);
            }
            else if (TryParseInteger(value, out var depth))
            {
                MathDepth = new MathDepthValue(MathDepthKind.Set, depth);
            }
            else
            {
                Invalid("math-depth", value);
            }
        }

        /// <summary>
        ///     Applies a <c>font-style</c> value.
        /// </summary>
        /// <param name="value">The value.</param>
        private void ApplyFontStyle(string value)
        {
            if (value.Equals("normal", StringComparison.OrdinalIgnoreCase))
            {
                Italic = false;
            }
            else if (value.Equals("italic", StringComparison.OrdinalIgnoreCase))
            {
                Italic = true;
            }
            else
            {
                Issues.Add(new StyleIssue(true, $"the font style '{value}' is not supported; only normal and italic are"));
            }
        }

        /// <summary>
        ///     Applies a <c>display</c> value: only <c>none</c> is supported.
        /// </summary>
        /// <param name="value">The value.</param>
        private void ApplyDisplay(string value)
        {
            if (value.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                DisplayNone = true;
            }
            else
            {
                Issues.Add(new StyleIssue(true, $"the display value '{value}' is not supported; only display: none is"));
            }
        }

        /// <summary>
        ///     Records an invalid value of a supported property.
        /// </summary>
        /// <param name="property">The property.</param>
        /// <param name="value">The value.</param>
        private void Invalid(string property, string value) =>
            Issues.Add(new StyleIssue(false, $"'{value}' is not a valid value of the CSS property '{property}'"));

        /// <summary>
        ///     Parses a signed integer.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="value">The integer.</param>
        /// <returns>True when the text is a valid integer.</returns>
        private static bool TryParseInteger(string text, out int value)
        {
            value = 0;
            var trimmed = text.Trim();
            if (trimmed.Length == 0)
            {
                return false;
            }

            var negative = trimmed[0] == '-';
            var digits = trimmed[0] is '-' or '+' ? trimmed[1..] : trimmed;
            if (!AttributeValues.TryParseUnsignedInteger(digits, out var magnitude))
            {
                return false;
            }

            value = negative ? -magnitude : magnitude;
            return true;
        }
    }
}
