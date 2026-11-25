using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.MathMl.Syntax;

namespace DemaConsulting.CanvasNet.MathMl.Layout;

/// <summary>
///     The computed style of an element: what MathML Core expresses with CSS properties and the layout reads.
/// </summary>
/// <remarks>
///     <see cref="FontSize"/>, <see cref="DisplayStyle"/>, <see cref="MathDepth"/>, <see cref="Color"/> and
///     <see cref="Variant"/> are inherited; <see cref="Background"/> and <see cref="DisplayNone"/> belong to the element
///     alone. The record is immutable.
/// </remarks>
/// <param name="FontSize">The font size in CSS pixels.</param>
/// <param name="DisplayStyle">True for <c>math-style: normal</c> (display style), false for <c>compact</c>.</param>
/// <param name="MathDepth">The <c>math-depth</c> (the script level).</param>
/// <param name="Color">The text color.</param>
/// <param name="Variant">The <c>mathvariant</c> in effect, or null for the default (automatic italic of a single-character <c>mi</c>).</param>
internal sealed record MathStyle(double FontSize, bool DisplayStyle, int MathDepth, Rgba32 Color, MathVariant? Variant)
{
    /// <summary>Gets the background color of the element's box, or null for none (not inherited).</summary>
    public Rgba32? Background { get; init; }

    /// <summary>Gets a value indicating whether the element is not displayed (<c>display: none</c>; not inherited).</summary>
    public bool DisplayNone { get; init; }
}

/// <summary>
///     Computes the style of each element from its parent's (MathML Core Appendix A, the user agent stylesheet, and the
///     presentational hints of 2.1): <c>display</c>, <c>displaystyle</c>, <c>scriptlevel</c>, <c>mathsize</c>,
///     <c>mathcolor</c>, <c>mathbackground</c>, <c>mathvariant</c> and the supported subset of the inline
///     <c>style</c> attribute; and resolves the length attributes of the layouts.
/// </summary>
/// <remarks>
///     <para>
///         <b>Cascade.</b> The user agent rules come first, the presentational hints of the attributes override them, and
///         the declarations of the inline <c>style</c> attribute override both, as in CSS. The <c>math</c> element takes
///         its math style from its display mode (the <c>display</c> attribute or the render option), and resets the math
///         depth to zero; <c>merror</c> gets its light yellow background.
///     </para>
///     <para>
///         <b>Font size.</b> Unless <c>mathsize</c> or a <c>font-size</c> declaration sets it, the font size of every
///         element but <c>math</c> is <c>font-size: math</c>: the inherited size scaled by the change of math depth,
///         with the ScriptPercentScaleDown and ScriptScriptPercentScaleDown constants of the MATH table for the first two
///         levels and 0.71 per further level (MathML Core 4.5). The math depth is clamped to plus or minus
///         <see cref="MaxMathDepth"/>, so the font size of an untrusted equation stays finite.
///     </para>
///     <para>
///         <b>Reports.</b> An invalid value is a <see cref="MathMlDiagnosticCodes.AttributeInvalid"/> warning and is
///         ignored; a <c>style</c> declaration outside the subset is a <see cref="MathMlDiagnosticCodes.CssUnsupported"/>
///         report. An instance serves one call on one thread (it records into the sink of the call).
///     </para>
/// </remarks>
internal sealed class StyleResolver
{
    /// <summary>The largest magnitude of the math depth.</summary>
    public const int MaxMathDepth = 64;

    /// <summary>The scale factor of one further script level when no MATH constant applies (MathML Core 4.5).</summary>
    private const double ScaleFactorPerLevel = 0.71;

    /// <summary>The background of <c>merror</c> (CSS <c>lightYellow</c>).</summary>
    private static readonly Rgba32 LightYellow = new(255, 255, 224, 255);

    /// <summary>The math font, whose constants scale the script levels.</summary>
    private readonly MathFont _font;

    /// <summary>The sink of the call.</summary>
    private readonly MathMlReportSink _sink;

    /// <summary>
    ///     Initializes a resolver for one call.
    /// </summary>
    /// <param name="font">The math font.</param>
    /// <param name="sink">The sink of the call.</param>
    public StyleResolver(MathFont font, MathMlReportSink sink)
    {
        _font = font;
        _sink = sink;
    }

    /// <summary>
    ///     Parses the <c>display</c> attribute of a <c>math</c> element.
    /// </summary>
    /// <param name="value">The attribute value, or null when absent.</param>
    /// <param name="display">The display mode: block for <c>block</c>, inline otherwise.</param>
    /// <returns>True when the value is absent, <c>block</c> or <c>inline</c> (ASCII case-insensitive).</returns>
    public static bool TryParseDisplay(string? value, out MathMlDisplay display)
    {
        display = MathMlDisplay.Inline;
        if (value is null || value.Trim().Equals("inline", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (value.Trim().Equals("block", StringComparison.OrdinalIgnoreCase))
        {
            display = MathMlDisplay.Block;
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Computes the style of the <c>math</c> element.
    /// </summary>
    /// <param name="math">The <c>math</c> element.</param>
    /// <param name="fontSize">The font size of the call in CSS pixels.</param>
    /// <param name="color">The text color of the call.</param>
    /// <param name="displayOverride">The display mode of the call, or null for the element's own.</param>
    /// <returns>The style.</returns>
    public MathStyle ResolveRoot(MathElement math, double fontSize, Rgba32 color, MathMlDisplay? displayOverride)
    {
        // The display mode decides the math style; an invalid display attribute is ignored with a warning
        var own = MathMlDisplay.Inline;
        if (math.Attributes.TryGetValue("display", out var display) && !TryParseDisplay(display.Value, out own))
        {
            Invalid("display", display, "block or inline");
        }

        var mode = displayOverride ?? own;
        var inherited = new MathStyle(fontSize, mode == MathMlDisplay.Block, 0, color, null);
        return Resolve(math, inherited, isRoot: true);
    }

    /// <summary>
    ///     Computes the style of an element from its parent's.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="parent">The computed style of the parent.</param>
    /// <returns>The style.</returns>
    public MathStyle Resolve(MathElement element, MathStyle parent) => Resolve(element, parent, isRoot: false);

    /// <summary>
    ///     Resolves a length attribute of a layout (<c>mspace</c>, <c>mpadded</c>, <c>mo</c>) to CSS pixels.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="name">The attribute name.</param>
    /// <param name="style">The computed style of the element (the base of <c>em</c> and <c>ex</c>).</param>
    /// <param name="percentBase">The reference length of a percentage, or null when the attribute ignores percentages (reported).</param>
    /// <returns>The length, or null when the attribute is absent, invalid (reported) or an ignored percentage.</returns>
    public double? ResolveLength(MathElement element, string name, MathStyle style, double? percentBase)
    {
        if (!element.Attributes.TryGetValue(name, out var attribute))
        {
            return null;
        }

        if (!AttributeValues.TryParseLength(attribute.Value, out var length))
        {
            Invalid(name, attribute, "a CSS length such as 0.5em or 2px");
            return null;
        }

        if (length.Unit == MathLengthUnit.Percent && percentBase is null)
        {
            _sink.Unsupported(
                MathMlDiagnosticCodes.AttributeUnsupported,
                $"the percentage '{attribute.Value}' of the attribute '{name}' of <{element.Name}> is ignored, as MathML Core ignores it",
                attribute.Location,
                "use a length such as 1em");
            return null;
        }

        return length.Resolve(style.FontSize, _font.XHeight(style.FontSize), percentBase ?? 0);
    }

    /// <summary>
    ///     Computes the scale factor of the font size between two math depths (MathML Core 4.5, <c>font-size: math</c>).
    /// </summary>
    /// <param name="inheritedDepth">The inherited math depth A.</param>
    /// <param name="depth">The computed math depth B.</param>
    /// <returns>The factor by which the inherited font size is multiplied.</returns>
    public double ScaleFactor(int inheritedDepth, int depth)
    {
        var a = inheritedDepth;
        var b = depth;
        if (a == b)
        {
            return 1;
        }

        // Scaling up is the inverse of scaling down over the same levels
        var invert = b < a;
        if (invert)
        {
            (a, b) = (b, a);
        }

        var exponent = b - a;
        var script = Percent(MathConstant.ScriptPercentScaleDown, ScaleFactorPerLevel);
        var scriptScript = Percent(MathConstant.ScriptScriptPercentScaleDown, ScaleFactorPerLevel * ScaleFactorPerLevel);
        var factor = 1.0;
        if (a <= 0 && b >= 2)
        {
            factor *= scriptScript;
            exponent -= 2;
        }
        else if (a == 1)
        {
            factor *= scriptScript / script;
            exponent -= 1;
        }
        else if (b == 1)
        {
            factor *= script;
            exponent -= 1;
        }

        factor *= Math.Pow(ScaleFactorPerLevel, exponent);
        return invert ? 1 / factor : factor;
    }

    /// <summary>
    ///     Computes a style: the user agent rules, then the presentational hints, then the inline style.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="parent">The inherited style.</param>
    /// <param name="isRoot">Whether the element is the <c>math</c> element (math depth 0, inherited font size).</param>
    /// <returns>The style.</returns>
    private MathStyle Resolve(MathElement element, MathStyle parent, bool isRoot)
    {
        var displayStyle = parent.DisplayStyle;
        var depth = isRoot ? 0 : parent.MathDepth;
        double? fontSize = null;
        var color = parent.Color;
        Rgba32? background = element.Name == "merror" ? LightYellow : null;
        var variant = parent.Variant;

        // Presentational hints of the attributes
        if (element.Attributes.TryGetValue("displaystyle", out var displayStyleAttribute))
        {
            if (AttributeValues.TryParseBoolean(displayStyleAttribute.Value, out var value))
            {
                displayStyle = value;
            }
            else
            {
                Invalid("displaystyle", displayStyleAttribute, "true or false");
            }
        }

        if (element.Attributes.TryGetValue("scriptlevel", out var scriptLevel))
        {
            if (AttributeValues.TryParseScriptLevel(scriptLevel.Value, out var relative, out var level))
            {
                depth = relative ? depth + level : level;
            }
            else
            {
                Invalid("scriptlevel", scriptLevel, "+n, -n or n");
            }
        }

        if (element.Attributes.TryGetValue("mathsize", out var mathSize))
        {
            fontSize = ParseFontSize(mathSize.Value, parent.FontSize);
            if (fontSize is null)
            {
                Invalid("mathsize", mathSize, "a CSS length or percentage such as 120% or 2em");
            }
        }

        color = ParseColorAttribute(element, "mathcolor") ?? color;
        background = ParseColorAttribute(element, "mathbackground") ?? background;
        if (element.Attributes.TryGetValue("mathvariant", out var mathVariant))
        {
            if (MathVariants.TryParse(mathVariant.Value, out var parsed))
            {
                variant = parsed;
            }
            else
            {
                Invalid("mathvariant", mathVariant, "a MathML mathvariant such as normal, bold or double-struck");
            }
        }

        // The inline style overrides the hints
        var displayNone = false;
        if (element.Attributes.TryGetValue("style", out var styleAttribute))
        {
            var declarations = StyleDeclarations.Parse(styleAttribute.Value);
            ReportStyleIssues(declarations, styleAttribute.Location);
            color = declarations.Color ?? color;
            background = declarations.BackgroundColor ?? background;
            displayStyle = declarations.DisplayStyle ?? displayStyle;
            displayNone = declarations.DisplayNone;
            if (declarations.FontSize is { } declared)
            {
                fontSize = declared.Resolve(parent.FontSize, _font.XHeight(parent.FontSize), parent.FontSize);
            }

            if (declarations.MathDepth is { } mathDepth)
            {
                depth = mathDepth.Kind switch
                {
                    MathDepthKind.AutoAdd => parent.DisplayStyle ? parent.MathDepth : parent.MathDepth + 1,
                    MathDepthKind.Add => parent.MathDepth + mathDepth.Value,
                    _ => mathDepth.Value,
                };
            }

            if (declarations.Italic is { } italic)
            {
                variant = italic ? MathVariant.Italic : MathVariant.Normal;
            }
        }

        // The font size follows the math depth unless it was set explicitly; the math element inherits it
        depth = Math.Clamp(depth, -MaxMathDepth, MaxMathDepth);
        var size = fontSize ?? (isRoot ? parent.FontSize : parent.FontSize * ScaleFactor(parent.MathDepth, depth));
        return new MathStyle(Math.Max(0, size), displayStyle, depth, color, variant)
        {
            Background = background,
            DisplayNone = displayNone,
        };
    }

    /// <summary>
    ///     Parses a font size: a CSS length or a percentage of the inherited size; not negative.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="inherited">The inherited font size, the base of <c>em</c> and of a percentage.</param>
    /// <returns>The font size in CSS pixels, or null when the value is invalid.</returns>
    private double? ParseFontSize(string value, double inherited)
    {
        if (!AttributeValues.TryParseLength(value, out var length) || length.Value < 0)
        {
            return null;
        }

        return length.Resolve(inherited, _font.XHeight(inherited), inherited);
    }

    /// <summary>
    ///     Parses a color attribute, warning about an invalid value.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="name">The attribute name.</param>
    /// <returns>The color, or null when absent or invalid.</returns>
    private Rgba32? ParseColorAttribute(MathElement element, string name)
    {
        if (!element.Attributes.TryGetValue(name, out var attribute))
        {
            return null;
        }

        if (AttributeValues.TryParseColor(attribute.Value, out var color))
        {
            return color;
        }

        Invalid(name, attribute, "a CSS color such as red or #ff0000");
        return null;
    }

    /// <summary>
    ///     Reports the issues of an inline style.
    /// </summary>
    /// <param name="declarations">The parsed declarations.</param>
    /// <param name="location">The location of the <c>style</c> attribute.</param>
    private void ReportStyleIssues(StyleDeclarations declarations, MathMlLocation location)
    {
        foreach (var issue in declarations.Issues)
        {
            if (issue.IsUnsupported)
            {
                _sink.Unsupported(MathMlDiagnosticCodes.CssUnsupported, issue.Message, location, "a rendering supports only a subset of CSS; remove the declaration");
            }
            else
            {
                _sink.Warn(MathMlDiagnosticCodes.AttributeInvalid, issue.Message, location);
            }
        }
    }

    /// <summary>
    ///     Reads a percentage constant of the MATH table as a factor.
    /// </summary>
    /// <param name="constant">The constant.</param>
    /// <param name="fallback">The factor when the table holds zero.</param>
    /// <returns>The factor.</returns>
    private double Percent(MathConstant constant, double fallback)
    {
        var value = _font.RawConstant(constant);
        return value > 0 ? value / 100.0 : fallback;
    }

    /// <summary>
    ///     Warns about an invalid attribute value.
    /// </summary>
    /// <param name="name">The attribute name.</param>
    /// <param name="attribute">The attribute.</param>
    /// <param name="expected">What the attribute accepts.</param>
    private void Invalid(string name, MathAttribute attribute, string expected) =>
        _sink.Warn(
            MathMlDiagnosticCodes.AttributeInvalid,
            string.Create(CultureInfo.InvariantCulture, $"'{attribute.Value}' is not a valid value of the attribute '{name}'; it is ignored"),
            attribute.Location,
            $"use {expected}");
}
