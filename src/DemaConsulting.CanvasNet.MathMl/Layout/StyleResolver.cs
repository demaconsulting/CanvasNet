using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.MathMl.Syntax;

namespace DemaConsulting.CanvasNet.MathMl.Layout;

/// <summary>
///     The computed style of an element: what MathML Core expresses with CSS properties and the layout reads.
/// </summary>
/// <remarks>
///     <see cref="FontSize"/>, <see cref="DisplayStyle"/>, <see cref="MathDepth"/>, <see cref="Color"/>,
///     <see cref="Variant"/> and <see cref="MathShiftCompact"/> are inherited; <see cref="Background"/> and
///     <see cref="DisplayNone"/> belong to the element alone. The record is immutable.
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

    /// <summary>
    ///     Gets a value indicating whether <c>math-shift</c> is <c>compact</c> (inherited): a superscript of the element
    ///     uses the cramped shift of the MATH table (MathML Core 3.4.1; set by the user agent rules of radicals and of
    ///     the denominator of a fraction).
    /// </summary>
    public bool MathShiftCompact { get; init; }
}

/// <summary>
///     How a user agent rule of MathML Core sets the <c>math-depth</c> of a child.
/// </summary>
internal enum MathDepthRule
{
    /// <summary>The child inherits the math depth (no rule).</summary>
    None,

    /// <summary><c>math-depth: auto-add</c>: one more than the parent's when the parent's math style is compact.</summary>
    AutoAdd,

    /// <summary><c>math-depth: add(n)</c>: the parent's plus <see cref="ChildStyleRule.DepthAdd"/>.</summary>
    Add,
}

/// <summary>
///     The user agent rules of MathML Core (Appendix A) that a layout element applies to one of its children by position,
///     such as <c>mfrac &gt; * { math-depth: auto-add; math-style: compact }</c>.
/// </summary>
/// <param name="Compact">Whether the child gets <c>math-style: compact</c>.</param>
/// <param name="Depth">How the child's math depth is set.</param>
/// <param name="DepthAdd">The levels added by <see cref="MathDepthRule.Add"/>.</param>
/// <param name="MathShiftCompact">The <c>math-shift</c> of the child: true for compact, false for normal, null to inherit.</param>
/// <param name="FontSizeInherit">Whether the child keeps the font size of its parent (<c>font-size: inherit</c>) while its math depth changes.</param>
internal readonly record struct ChildStyleRule(bool Compact, MathDepthRule Depth, int DepthAdd, bool? MathShiftCompact, bool FontSizeInherit)
{
    /// <summary>The rule of the numerator of a fraction: <c>math-depth: auto-add; math-style: compact</c>.</summary>
    public static ChildStyleRule FractionNumerator => new(true, MathDepthRule.AutoAdd, 0, null, false);

    /// <summary>The rule of the denominator of a fraction: the numerator's plus <c>math-shift: compact</c>.</summary>
    public static ChildStyleRule FractionDenominator => new(true, MathDepthRule.AutoAdd, 0, true, false);

    /// <summary>The rule of the index of a root: <c>math-depth: add(2); math-style: compact</c>.</summary>
    public static ChildStyleRule RootIndex => new(true, MathDepthRule.Add, 2, null, false);

    /// <summary>The rule of a script (every child of a scripted element but the first): <c>math-depth: add(1); math-style: compact</c>.</summary>
    public static ChildStyleRule Script => new(true, MathDepthRule.Add, 1, null, false);

    /// <summary>The rule of the base of an accent <c>mover</c> or <c>munderover</c>: <c>math-shift: compact</c>.</summary>
    public static ChildStyleRule AccentBase => new(false, MathDepthRule.None, 0, true, false);
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
///         depth to zero and the math shift to normal; <c>merror</c> gets its light yellow background; <c>msqrt</c> and
///         <c>mroot</c> get <c>math-shift: compact</c>; <c>mtable</c> gets <c>math-style: compact</c>. The rules that a
///         layout element applies to its children by
///         position (<see cref="ChildStyleRule"/>: the numerator and denominator of <c>mfrac</c>, the index of
///         <c>mroot</c>, the scripts of the scripted elements with the <c>font-size: inherit</c> of an accent and the
///         <c>math-shift: compact</c> of a subscript or of the base of an accent) are user agent rules too, so a hint of
///         the child replaces them: <c>scriptlevel="+1"</c> on a numerator is one level more than the fraction's, not
///         two.
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
    /// <returns>True when the value is absent, <c>block</c> or <c>inline</c> (ASCII case-insensitive, without trimming whitespace, as MathML Core matches it).</returns>
    public static bool TryParseDisplay(string? value, out MathMlDisplay display)
    {
        display = MathMlDisplay.Inline;
        if (value is null || value.Equals("inline", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (value.Equals("block", StringComparison.OrdinalIgnoreCase))
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
        return Resolve(math, inherited, isRoot: true, default);
    }

    /// <summary>
    ///     Computes the style of an element from its parent's.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="parent">The computed style of the parent.</param>
    /// <returns>The style.</returns>
    public MathStyle Resolve(MathElement element, MathStyle parent) => Resolve(element, parent, isRoot: false, default);

    /// <summary>
    ///     Computes the style of a child of a layout element from its parent's, with the user agent rule that the parent
    ///     applies to the child's position (MathML Core Appendix A) before the child's own hints and inline style.
    /// </summary>
    /// <param name="child">The child element.</param>
    /// <param name="parent">The computed style of the parent.</param>
    /// <param name="rule">The user agent rule of the child's position.</param>
    /// <returns>The style.</returns>
    public MathStyle ResolveChild(MathElement child, MathStyle parent, ChildStyleRule rule) => Resolve(child, parent, isRoot: false, rule);

    /// <summary>
    ///     Gets the user agent rule of a child of a scripted element (<c>msub</c>, <c>msup</c>, <c>msubsup</c>,
    ///     <c>munder</c>, <c>mover</c>, <c>munderover</c>, <c>mmultiscripts</c>; MathML Core 3.4.4 and Appendix A) by its
    ///     index among the element children: every child but the first gets <c>math-depth: add(1); math-style:
    ///     compact</c>; an accent script keeps the font size of its parent (<c>font-size: inherit</c>); the subscript of
    ///     <c>msub</c> and <c>msubsup</c>, the subscripts of <c>mmultiscripts</c> (its children at even positions
    ///     before <c>mprescripts</c> and at odd positions after it, counting from 1) and the base of an accent
    ///     <c>mover</c> or <c>munderover</c> get <c>math-shift: compact</c>; the superscripts after <c>mprescripts</c>
    ///     inherit it.
    /// </summary>
    /// <param name="element">The scripted element.</param>
    /// <param name="index">The index of the child among the element children (from 0, hidden elements included).</param>
    /// <returns>The rule.</returns>
    public static ChildStyleRule ScriptRule(MathElement element, int index)
    {
        ArgumentNullException.ThrowIfNull(element);
        var accent = IsTrue(element, "accent");
        var accentUnder = IsTrue(element, "accentunder");
        if (index == 0)
        {
            return accent && (element.Name is "mover" or "munderover") ? ChildStyleRule.AccentBase : default;
        }

        var rule = ChildStyleRule.Script;
        switch (element.Name)
        {
            case "msub" or "msubsup" when index == 1:
                return rule with { MathShiftCompact = true };
            case "munder" or "munderover" when index == 1 && accentUnder:
            case "mover" when index == 1 && accent:
            case "munderover" when index == 2 && accent:
                return rule with { FontSizeInherit = true };
            case "mmultiscripts":
                {
                    // Positions counted from 1: even before mprescripts, odd after it (even after it inherits)
                    var position = index + 1;
                    var prescripts = element.ElementChildren.Select((child, i) => (child, i)).FirstOrDefault(entry => entry.child.Name == "mprescripts");
                    var after = prescripts.child is not null && index > prescripts.i;
                    var compact = after ? position % 2 == 1 : position % 2 == 0;
                    return rule with { MathShiftCompact = compact ? true : null };
                }

            default:
                return rule;
        }
    }

    /// <summary>
    ///     Decides whether an attribute is an ASCII case-insensitive match to <c>true</c>, as the attribute selectors of the
    ///     user agent stylesheet of MathML Core match <c>accent</c> and <c>accentunder</c>.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="name">The attribute name.</param>
    /// <returns>True when the attribute is <c>true</c> in any case.</returns>
    public static bool IsTrue(MathElement element, string name)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.Attributes.TryGetValue(name, out var attribute) && attribute.Value.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

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
    /// <param name="isRoot">Whether the element is the <c>math</c> element (math depth 0, math shift normal, inherited font size).</param>
    /// <param name="rule">The user agent rule of the element's position in its parent (default: none).</param>
    /// <returns>The style.</returns>
    private MathStyle Resolve(MathElement element, MathStyle parent, bool isRoot, ChildStyleRule rule)
    {
        // The user agent rules: of the position in the parent, then of the element itself
        var displayStyle = !rule.Compact && parent.DisplayStyle && element.Name != "mtable";
        var inheritedDepth = isRoot ? 0 : parent.MathDepth;
        var depth = rule.Depth switch
        {
            _ when isRoot => 0,
            MathDepthRule.AutoAdd => parent.DisplayStyle ? parent.MathDepth : parent.MathDepth + 1,
            MathDepthRule.Add => parent.MathDepth + rule.DepthAdd,
            _ => parent.MathDepth,
        };
        var mathShiftCompact = !isRoot && ((rule.MathShiftCompact ?? parent.MathShiftCompact) || element.Name is "msqrt" or "mroot");
        double? fontSize = rule.FontSizeInherit ? parent.FontSize : null;
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
                // math-depth: add(n) replaces the user agent rule: it adds to the inherited depth
                depth = relative ? inheritedDepth + level : level;
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
            MathShiftCompact = mathShiftCompact,
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
