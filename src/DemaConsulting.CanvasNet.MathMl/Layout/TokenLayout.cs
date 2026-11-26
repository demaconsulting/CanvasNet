using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.MathMl.Syntax;

namespace DemaConsulting.CanvasNet.MathMl.Layout;

/// <summary>
///     Lays out the token elements (<c>mi</c>, <c>mn</c>, <c>mo</c>, <c>mtext</c>, <c>ms</c>, <c>mspace</c>) and
///     text written directly inside a layout element: the glyphs of the text, its box, and the spacing of an operator.
/// </summary>
/// <remarks>
///     <para>
///         <b>Characters.</b> Each character is transformed first: by the <c>mathvariant</c> in effect, or, for an
///         <c>mi</c> of a single character with no <c>mathvariant</c>, to its mathematical italic (MathML Core
///         <c>text-transform: math-auto</c>); see <see cref="MathVariants"/>. It is then drawn with the glyph of the math
///         font or of the first bundled fallback font that has one. An invisible character (a format character such as
///         the invisible operators U+2061 to U+2064) takes no space and draws nothing; a character that no bundled font
///         can draw is drawn as the missing-glyph box and reported (<see cref="MathMlDiagnosticCodes.TextMissingGlyph"/>).
///         Glyphs are placed by their advances, without kerning or glyph substitution.
///     </para>
///     <para>
///         <b>Box.</b> As MathML Core 3.2.1.1 lays out a token: the width is the sum of the advances, and the ascent and
///         the descent are those of the ink of the glyphs. MathML Core lays a text with a space (a soft wrap opportunity)
///         out as a line of text instead; Chrome, whose layout the reference tests compare with, uses the ink for every
///         token, and so does CanvasNet. The ascent and the descent are rounded out to whole CSS pixels, as Chrome rounds the
///         bounds of its glyphs at 100% zoom. A single glyph gives the box its italic correction and its top accent attachment
///         from the MATH table. An <c>ms</c> draws its text as written, without quotes, as MathML Core and Chrome do (the
///         legacy <c>lquote</c> and <c>rquote</c> are reported as unsupported attributes).
///     </para>
///     <para>
///         <b>Operators.</b> An <c>mo</c> is an embellished operator (MathML Core 3.2.4). Its properties come from the
///         operator dictionary (<see cref="OperatorDictionary"/>) for its text and its form (the <c>form</c> attribute, else
///         the form that its row inferred from its position), and its attributes override them: <c>form</c>,
///         <c>fence</c>, <c>separator</c>, <c>lspace</c>, <c>rspace</c> (a percentage is of the dictionary value),
///         <c>stretchy</c>, <c>symmetric</c>, <c>largeop</c>, <c>movablelimits</c>, <c>minsize</c> and <c>maxsize</c>
///         (a percentage is of the unstretched height of the glyph). A row adds the <c>lspace</c> before the operator
///         and the <c>rspace</c> after it. An operator of a single character is laid out as MathML Core 3.2.4.3 lays it
///         out:
///     </para>
///     <list type="bullet">
///         <item>
///             a stretchy operator with a block stretch axis that its row stretches gets a target from the constraint:
///             its ascent and descent, symmetric about the math axis when the operator is <c>symmetric</c>, scaled to
///             <c>minsize</c> and <c>maxsize</c>; its glyph is stretched by <see cref="StretchyGlyphs"/>, centered on the
///             target, and centered in a box as wide as the widest of its constructions (its preferred inline size), as
///             Chrome draws it. A character that cannot stretch keeps its glyph, centered on the target as Chrome centers
///             it; when the glyph is shorter than the target it is reported
///             (<see cref="MathMlDiagnosticCodes.StretchUnavailable"/>);
///         </item>
///         <item>
///             a large operator in display style takes the first size variant at least DisplayOperatorMinHeight tall (else
///             the largest), centered on the math axis, as Chrome centers it;
///         </item>
///         <item>
///             any other operator is laid out like an <c>mtext</c>.
///         </item>
///     </list>
///     <para>
///         An instance serves one call on one thread.
///     </para>
/// </remarks>
internal sealed class TokenLayout
{
    /// <summary>The tolerance that keeps an ascent of 20.0000000001 from becoming 21 when it is rounded out.</summary>
    private const double RoundingTolerance = 1e-6;

    /// <summary>The math font.</summary>
    private readonly MathFont _font;

    /// <summary>The style resolver of the call, which resolves the length attributes.</summary>
    private readonly StyleResolver _styles;

    /// <summary>The sink of the call.</summary>
    private readonly MathMlReportSink _sink;

    /// <summary>The limits of the call (the glyphs of a stretched operator).</summary>
    private readonly MathMlLimits _limits;

    /// <summary>
    ///     Initializes the token layout of one call.
    /// </summary>
    /// <param name="font">The math font.</param>
    /// <param name="styles">The style resolver of the call.</param>
    /// <param name="sink">The sink of the call.</param>
    /// <param name="limits">The limits of the call, or null for the defaults.</param>
    public TokenLayout(MathFont font, StyleResolver styles, MathMlReportSink sink, MathMlLimits? limits = null)
    {
        _font = font;
        _styles = styles;
        _sink = sink;
        _limits = limits ?? MathMlLimits.Default;
    }

    /// <summary>
    ///     Lays out a token element.
    /// </summary>
    /// <param name="element">The token element.</param>
    /// <param name="style">Its computed style.</param>
    /// <param name="context">The form and the stretch constraint that the row gives an <c>mo</c>; ignored by the other tokens.</param>
    /// <returns>The box.</returns>
    /// <exception cref="MathMlRenderException">Thrown when a stretched operator needs more glyphs than <see cref="MathMlLimits.MaxAssemblyParts"/>.</exception>
    public MathBox LayoutToken(MathElement element, MathStyle style, OperatorContext context = default)
    {
        if (element.Name == "mspace")
        {
            return LayoutSpace(element, style);
        }

        if (element.Name == "mo")
        {
            return LayoutOperator(element, style, context);
        }

        // The text as written (ms has no quotes in MathML Core)
        var text = element.TextContent;
        var location = TextLocation(element);
        var box = LayoutText(text, style, autoItalic: element.Name == "mi", location);
        return element.Name == "mtext" ? Copy(box, isSpaceLike: true, null) : box;
    }

    /// <summary>
    ///     Lays out an <c>mo</c> (MathML Core 3.2.4): resolves its properties, and stretches it or draws its large variant
    ///     when they ask for it.
    /// </summary>
    /// <param name="element">The <c>mo</c> element.</param>
    /// <param name="style">Its computed style.</param>
    /// <param name="context">The form inferred by the row and the stretch constraint, if any.</param>
    /// <returns>The box, an embellished operator.</returns>
    public MathBox LayoutOperator(MathElement element, MathStyle style, OperatorContext context)
    {
        var text = element.TextContent;
        var location = TextLocation(element);
        var plain = LayoutText(text, style, autoItalic: false, location);
        var op = ResolveOperator(element, style, context.Form, plain.Ascent + plain.Descent);

        // Only an operator of one character stretches or grows (MathML Core 3.2.4.3); an operator drawn with its normal
        // glyph has no italic correction for its scripts, as Chrome takes it only from a size variant or an assembly
        var codePoints = Transform(text, style.Variant, autoItalic: false);
        var box = new MathBox(plain.Width, plain.Ascent, plain.Descent, plain.Children, plain.Items) { TopAccentAttachment = plain.TopAccentAttachment };
        if (codePoints.Count == 1)
        {
            if (op.Stretchy && op.Axis == StretchAxis.Block && context.Stretch is { } constraint)
            {
                box = StretchBlock(codePoints[0], style, op, constraint, element.Location);
            }
            else if (op.Stretchy && op.Axis == StretchAxis.Inline && context.InlineStretch is { } width)
            {
                box = StretchInline(codePoints[0], style, op, width, element, box);
            }
            else if (op.LargeOp && style.DisplayStyle)
            {
                box = LargeOperator(codePoints[0], style, box);
            }
        }

        return Copy(box, isSpaceLike: false, op);
    }

    /// <summary>
    ///     Lays out a run of text as a token of a style: the glyphs and the box.
    /// </summary>
    /// <param name="text">The text, whitespace already collapsed.</param>
    /// <param name="style">The computed style.</param>
    /// <param name="autoItalic">Whether a single character takes its mathematical italic when no <c>mathvariant</c> is in effect (an <c>mi</c>).</param>
    /// <param name="location">The location of the text, for the reports.</param>
    /// <returns>The box.</returns>
    public MathBox LayoutText(string text, MathStyle style, bool autoItalic, MathMlLocation location)
    {
        var codePoints = Transform(text, style.Variant, autoItalic);
        var items = new List<IPaintItem>();
        var pen = 0.0;
        double? inkAscent = null;
        double? inkDescent = null;
        foreach (var codePoint in codePoints)
        {
            // An invisible character takes no space and draws nothing
            if (GlyphFallback.IsIgnorable(codePoint))
            {
                continue;
            }

            var (font, glyph) = _font.Resolve(codePoint);
            if (!GlyphFallback.NeedsGlyph(codePoint))
            {
                // White space advances by the space of the math font (its glyph, when it has one) and draws nothing
                pen += SpaceAdvance(font, glyph, style.FontSize);
                continue;
            }

            if (glyph == 0)
            {
                _sink.Warn(
                    MathMlDiagnosticCodes.TextMissingGlyph,
                    string.Create(System.Globalization.CultureInfo.InvariantCulture, $"the character U+{codePoint:X4} has no glyph in the bundled fonts; it is drawn as a missing-glyph box"),
                    location,
                    "use a character that Noto Sans Math, Noto Sans or Noto Sans Symbols 2 covers");
            }

            // Place the glyph at the pen and keep its ink for the box
            var ink = _font.InkBounds(font, glyph, style.FontSize).Offset(pen, 0);
            var item = new GlyphItem(font, glyph, style.FontSize, pen, 0, style.Color, ink);
            items.Add(item);
            if (ink.Width > 0 || ink.Height > 0)
            {
                inkAscent = Math.Max(inkAscent ?? double.NegativeInfinity, -ink.Y);
                inkDescent = Math.Max(inkDescent ?? double.NegativeInfinity, ink.Bottom);
            }

            pen += MathFont.Advance(font, glyph, style.FontSize);
        }

        // The box is the ink of the glyphs (MathML Core 3.2.1.1, for every token as in Chrome), rounded out to whole CSS
        // pixels as the browser rounds the bounds of its glyphs
        var ascent = inkAscent is { } top ? Math.Ceiling(top - RoundingTolerance) : 0;
        var descent = inkDescent is { } bottom ? Math.Ceiling(bottom - RoundingTolerance) : 0;
        var single = items.Count == 1 ? (GlyphItem)items[0] : null;
        return new MathBox(pen, ascent, descent, null, items)
        {
            ItalicCorrection = single is null ? 0 : _font.ItalicCorrection(single.Font, single.Glyph, style.FontSize),
            TopAccentAttachment = single is null ? null : _font.TopAccentAttachment(single.Font, single.Glyph, style.FontSize),
        };
    }

    /// <summary>
    ///     Transforms the characters of a text by the <c>mathvariant</c> in effect or the automatic italic.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="variant">The <c>mathvariant</c> in effect, or null.</param>
    /// <param name="autoItalic">Whether a single character takes its mathematical italic when no variant is in effect.</param>
    /// <returns>The code points to draw.</returns>
    internal static List<int> Transform(string text, MathVariant? variant, bool autoItalic)
    {
        var codePoints = new List<int>(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            codePoints.Add(rune.Value);
        }

        if (variant is { } value)
        {
            for (var i = 0; i < codePoints.Count; i++)
            {
                codePoints[i] = MathVariants.Map(codePoints[i], value);
            }
        }
        else if (autoItalic && codePoints.Count == 1)
        {
            codePoints[0] = MathVariants.AutoItalic(codePoints[0]);
        }

        return codePoints;
    }

    /// <summary>
    ///     Gets the location of the text of a token, or of the token when it has no text.
    /// </summary>
    /// <param name="element">The token element.</param>
    /// <returns>The location.</returns>
    private static MathMlLocation TextLocation(MathElement element) =>
        element.Children.OfType<MathText>().FirstOrDefault()?.Location ?? element.Location;

    /// <summary>
    ///     Resolves the properties of an operator (MathML Core 3.2.4.2): the dictionary values of its text and form, and
    ///     the attributes that override them.
    /// </summary>
    /// <param name="element">The <c>mo</c> element.</param>
    /// <param name="style">Its computed style (the base of <c>em</c>, <c>ex</c> and the spacing).</param>
    /// <param name="inferred">The form inferred by the row, or null for infix.</param>
    /// <param name="glyphHeight">The unstretched height of the operator, the base of a <c>minsize</c> or <c>maxsize</c> percentage.</param>
    /// <returns>The properties.</returns>
    private EmbellishedOperator ResolveOperator(MathElement element, MathStyle style, OperatorForm? inferred, double glyphHeight)
    {
        // The form: an explicit valid attribute wins over the position in the row
        var text = element.TextContent;
        OperatorForm? explicitForm = null;
        if (element.Attributes.TryGetValue("form", out var formAttribute))
        {
            if (OperatorDictionary.TryParseForm(formAttribute.Value, out var parsed))
            {
                explicitForm = parsed;
            }
            else
            {
                Invalid("form", formAttribute, "prefix, infix or postfix");
            }
        }

        var form = explicitForm ?? inferred ?? OperatorForm.Infix;
        var properties = OperatorDictionary.Lookup(text, form, explicitForm is not null);

        // The attributes override the dictionary; a percentage of lspace or rspace is of the dictionary value
        var leading = properties.LeadingSpace * style.FontSize;
        var trailing = properties.TrailingSpace * style.FontSize;
        return new EmbellishedOperator(
            _styles.ResolveLength(element, "lspace", style, leading) ?? leading,
            _styles.ResolveLength(element, "rspace", style, trailing) ?? trailing)
        {
            CoreText = text,
            Form = form,
            Category = properties.Category,
            Stretchy = Boolean(element, "stretchy", properties.Stretchy),
            Symmetric = Boolean(element, "symmetric", properties.Symmetric),
            LargeOp = Boolean(element, "largeop", properties.LargeOp),
            MovableLimits = Boolean(element, "movablelimits", properties.MovableLimits),
            Fence = Boolean(element, "fence", OperatorDictionary.IsFence(text)),
            Separator = Boolean(element, "separator", OperatorDictionary.IsSeparator(text)),
            Axis = OperatorDictionary.Axis(text),
            MinSize = _styles.ResolveLength(element, "minsize", style, glyphHeight) ?? glyphHeight,
            MaxSize = _styles.ResolveLength(element, "maxsize", style, glyphHeight) ?? double.PositiveInfinity,
        };
    }

    /// <summary>
    ///     Stretches an operator along the block axis to the constraint of its row (MathML Core 3.2.4.3).
    /// </summary>
    /// <param name="codePoint">The character, transformed by the <c>mathvariant</c> in effect.</param>
    /// <param name="style">The computed style of the operator.</param>
    /// <param name="op">The properties of the operator.</param>
    /// <param name="constraint">The block stretch size constraint.</param>
    /// <param name="location">The location of the operator.</param>
    /// <returns>The box of the stretched operator; the box of its glyph, centered on the target, when the character cannot stretch.</returns>
    private MathBox StretchBlock(int codePoint, MathStyle style, EmbellishedOperator op, BlockStretchConstraint constraint, MathMlLocation location)
    {
        // The target: symmetric about the math axis when asked, the smallest such target that holds the constraint
        var axis = _font.Constant(MathConstant.AxisHeight, style.FontSize);
        var (ascent, descent) = (constraint.Ascent, constraint.Descent);
        if (op.Symmetric)
        {
            var half = Math.Max(ascent - axis, descent + axis);
            (ascent, descent) = (half + axis, half - axis);
        }

        // minsize and maxsize scale the target about the math axis
        var minSize = Math.Max(0, op.MinSize);
        var maxSize = Math.Max(minSize, op.MaxSize);
        var target = ascent + descent;
        if (target <= 0)
        {
            ascent = (minSize / 2) + axis;
            descent = minSize - ascent;
        }
        else if (target < minSize)
        {
            ascent = Math.Max(0, ((ascent - axis) * minSize / target) + axis);
            descent = minSize - ascent;
        }
        else if (maxSize < target)
        {
            ascent = Math.Max(0, ((ascent - axis) * maxSize / target) + axis);
            descent = maxSize - ascent;
        }

        target = ascent + descent;
        var result = StretchyGlyphs.Stretch(_font, codePoint, StretchAxis.Block, target, style.FontSize, _limits.MaxAssemblyParts, location);
        if (result is null)
        {
            // Nothing stretches the character: it keeps its glyph, centered on the target as Chrome centers it, which is
            // only reported when it is too short
            result = StretchyGlyphs.Glyph(_font, codePoint, style.FontSize);
            if (target > result.Height + RoundingTolerance)
            {
                _sink.Warn(
                    MathMlDiagnosticCodes.StretchUnavailable,
                    $"the operator '{op.CoreText}' cannot stretch to the size of its row: neither the bundled math font nor the fallbacks of MathML Core can stretch it; it is drawn at its normal size",
                    location,
                    "use a character that Noto Sans Math stretches, such as a parenthesis, a bracket, a brace or a vertical bar, or set stretchy=\"false\"");
            }
        }

        // The glyph is centered on the target vertically, and in the widest of its constructions horizontally (as Chrome)
        var width = Math.Max(result.Width, StretchyGlyphs.PreferredInlineSize(_font, codePoint, style.FontSize));
        var shift = ((result.Ascent - result.Descent) - (ascent - descent)) / 2;
        return Place(result, width, (width - result.Width) / 2, shift, style);
    }

    /// <summary>
    ///     Stretches an operator along the inline axis to the constraint of its under- or overscript element (MathML Core
    ///     3.2.4.3 and 3.4.2.2): the glyph is stretched by <see cref="StretchyGlyphs"/> to the target width and centered,
    ///     on its baseline, in a box as wide as the unstretched glyph, as Chrome lays it out.
    /// </summary>
    /// <param name="codePoint">The character, transformed by the <c>mathvariant</c> in effect.</param>
    /// <param name="style">The computed style of the operator.</param>
    /// <param name="op">The properties of the operator.</param>
    /// <param name="target">The inline stretch size constraint in CSS pixels.</param>
    /// <param name="element">The <c>mo</c> element (its explicit <c>stretchy</c> decides the report).</param>
    /// <param name="plain">The unstretched box of the operator.</param>
    /// <returns>The box of the stretched operator; the unstretched box when the glyph already covers the target or nothing stretches it.</returns>
    private MathBox StretchInline(int codePoint, MathStyle style, EmbellishedOperator op, double target, MathElement element, MathBox plain)
    {
        var result = StretchyGlyphs.Stretch(_font, codePoint, StretchAxis.Inline, target, style.FontSize, _limits.MaxAssemblyParts, element.Location);
        if (result is null)
        {
            // Nothing stretches the character (Noto Sans Math has no horizontal construction for ^ or ~): it keeps its
            // glyph, which is reported only when the author asked for stretching explicitly and the glyph is too narrow
            if (element.Attributes.TryGetValue("stretchy", out var stretchy)
                && AttributeValues.TryParseBoolean(stretchy.Value, out var value)
                && value
                && target > plain.Width + RoundingTolerance)
            {
                _sink.Warn(
                    MathMlDiagnosticCodes.StretchUnavailable,
                    $"the operator '{op.CoreText}' cannot stretch to the width of its under- or overscript element: neither the bundled math font nor the fallbacks of MathML Core can stretch it horizontally; it is drawn at its normal size",
                    element.Location,
                    "use a character that Noto Sans Math stretches horizontally, such as an arrow, a brace, a bracket or a combining accent, or remove stretchy=\"true\"");
            }

            return plain;
        }

        // The glyph keeps its size when it is wide enough; a stretched glyph is centered in the unstretched box (Chrome)
        return result.Kind == StretchKind.Base ? plain : Place(result, plain.Width, (plain.Width - result.Width) / 2, 0, style);
    }

    /// <summary>
    ///     Lays out a large operator in display style (MathML Core 3.2.4.3): its first size variant at least
    ///     DisplayOperatorMinHeight tall, else its largest, centered on the math axis as Chrome centers it.
    /// </summary>
    /// <param name="codePoint">The character, transformed by the <c>mathvariant</c> in effect.</param>
    /// <param name="style">The computed style of the operator.</param>
    /// <param name="plain">The unstretched box of the operator.</param>
    /// <returns>The box of the large operator, or the unstretched box when the character has no larger variant.</returns>
    private MathBox LargeOperator(int codePoint, MathStyle style, MathBox plain)
    {
        var minHeight = _font.Constant(MathConstant.DisplayOperatorMinHeight, style.FontSize);
        if (StretchyGlyphs.LargeVariant(_font, codePoint, minHeight, style.FontSize) is not { } result)
        {
            return plain;
        }

        // Center on the axis: the ascent exceeds the descent by twice the axis height
        var axis = _font.Constant(MathConstant.AxisHeight, style.FontSize);
        var shift = ((result.Ascent - result.Descent) / 2) - axis;
        return Place(result, result.Width, 0, shift, style);
    }

    /// <summary>
    ///     Builds the box of a stretched glyph moved by an offset.
    /// </summary>
    /// <param name="result">The stretched glyph.</param>
    /// <param name="width">The width of the box.</param>
    /// <param name="left">The horizontal offset of the glyph in the box.</param>
    /// <param name="shift">The downward offset of the glyph.</param>
    /// <param name="style">The style, for the size and the color of the glyphs.</param>
    /// <returns>The box.</returns>
    private MathBox Place(StretchResult result, double width, double left, double shift, MathStyle style)
    {
        var items = new List<IPaintItem>(result.Glyphs.Count + 1);
        foreach (var glyph in result.Glyphs)
        {
            var x = left + glyph.X;
            var baseline = glyph.Baseline + shift;
            var ink = _font.InkBounds(glyph.Font, glyph.Glyph, style.FontSize).Offset(x, baseline);
            items.Add(new GlyphItem(glyph.Font, glyph.Glyph, style.FontSize, x, baseline, style.Color, ink));
        }

        if (result.Rule is { } rule)
        {
            items.Add(new RuleItem(rule.Offset(left, shift), style.Color));
        }

        // Only a size variant or an assembly gives its italic correction to the scripts (as Chrome); the base glyph of a
        // stretchy operator that already covers its target, or that nothing stretches, has none, as an unstretched one
        return new MathBox(width, result.Ascent - shift, result.Descent + shift, null, items)
        {
            ItalicCorrection = result.Kind == StretchKind.Base ? 0 : result.ItalicCorrection,
        };
    }

    /// <summary>
    ///     Reads a boolean attribute of an <c>mo</c>, warning about an invalid value.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="name">The attribute name.</param>
    /// <param name="fallback">The value when the attribute is absent or invalid.</param>
    /// <returns>The value.</returns>
    private bool Boolean(MathElement element, string name, bool fallback)
    {
        if (!element.Attributes.TryGetValue(name, out var attribute))
        {
            return fallback;
        }

        if (AttributeValues.TryParseBoolean(attribute.Value, out var value))
        {
            return value;
        }

        Invalid(name, attribute, "true or false");
        return fallback;
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
            $"'{attribute.Value}' is not a valid value of the attribute '{name}'; it is ignored",
            attribute.Location,
            $"use {expected}");

    /// <summary>
    ///     Lays out an <c>mspace</c>: a blank box of the given width, height and depth.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="style">Its computed style.</param>
    /// <returns>The box.</returns>
    private MathBox LayoutSpace(MathElement element, MathStyle style)
    {
        // A negative width is invalid CSS and is ignored; a negative height or depth is clamped (MathML Core 3.2.5)
        var width = _styles.ResolveLength(element, "width", style, null) ?? 0;
        if (width < 0)
        {
            _sink.Warn(MathMlDiagnosticCodes.AttributeInvalid, "a negative width of <mspace> is not valid; it is ignored", element.Attributes["width"].Location, "use a width of zero or more");
            width = 0;
        }

        var height = Math.Max(0, _styles.ResolveLength(element, "height", style, null) ?? 0);
        var depth = Math.Max(0, _styles.ResolveLength(element, "depth", style, null) ?? 0);
        return new MathBox(width, height, depth) { IsSpaceLike = true };
    }

    /// <summary>
    ///     Gets the advance of a white-space character.
    /// </summary>
    /// <param name="font">The font that the fallback resolved.</param>
    /// <param name="glyph">The glyph, zero when no font maps the character.</param>
    /// <param name="fontSize">The font size in CSS pixels.</param>
    /// <returns>The advance in CSS pixels.</returns>
    private double SpaceAdvance(TrueTypeFont font, int glyph, double fontSize)
    {
        if (glyph != 0)
        {
            return MathFont.Advance(font, glyph, fontSize);
        }

        var (spaceFont, space) = _font.Resolve(' ');
        return MathFont.Advance(spaceFont, space, fontSize);
    }

    /// <summary>
    ///     Copies a box with the token properties of its element.
    /// </summary>
    /// <param name="box">The box of the text.</param>
    /// <param name="isSpaceLike">Whether the token is space-like.</param>
    /// <param name="embellished">The operator data, or null.</param>
    /// <returns>The copy.</returns>
    private static MathBox Copy(MathBox box, bool isSpaceLike, EmbellishedOperator? embellished) =>
        new(box.Width, box.Ascent, box.Descent, box.Children, box.Items)
        {
            ItalicCorrection = box.ItalicCorrection,
            TopAccentAttachment = box.TopAccentAttachment,
            IsSpaceLike = isSpaceLike,
            Operator = embellished,
        };
}
