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
///         <b>Operators.</b> An <c>mo</c> is an embellished operator: a row adds its <c>lspace</c> before it and its
///         <c>rspace</c> after it. This version gives every operator the spacing of the <c>Default</c> category of the
///         operator dictionary (5/18 em on each side) unless the attributes set it; the dictionary comes in a later
///         version.
///     </para>
///     <para>
///         An instance serves one call on one thread.
///     </para>
/// </remarks>
internal sealed class TokenLayout
{
    /// <summary>The spacing of the <c>Default</c> operator category on each side, in em (MathML Core B.1).</summary>
    public const double DefaultOperatorSpace = 5.0 / 18;

    /// <summary>The tolerance that keeps an ascent of 20.0000000001 from becoming 21 when it is rounded out.</summary>
    private const double RoundingTolerance = 1e-6;

    /// <summary>The math font.</summary>
    private readonly MathFont _font;

    /// <summary>The style resolver of the call, which resolves the length attributes.</summary>
    private readonly StyleResolver _styles;

    /// <summary>The sink of the call.</summary>
    private readonly MathMlReportSink _sink;

    /// <summary>
    ///     Initializes the token layout of one call.
    /// </summary>
    /// <param name="font">The math font.</param>
    /// <param name="styles">The style resolver of the call.</param>
    /// <param name="sink">The sink of the call.</param>
    public TokenLayout(MathFont font, StyleResolver styles, MathMlReportSink sink)
    {
        _font = font;
        _styles = styles;
        _sink = sink;
    }

    /// <summary>
    ///     Lays out a token element.
    /// </summary>
    /// <param name="element">The token element.</param>
    /// <param name="style">Its computed style.</param>
    /// <returns>The box.</returns>
    public MathBox LayoutToken(MathElement element, MathStyle style)
    {
        if (element.Name == "mspace")
        {
            return LayoutSpace(element, style);
        }

        // The text as written (ms has no quotes in MathML Core)
        var text = element.TextContent;
        var location = element.Children.OfType<MathText>().FirstOrDefault()?.Location ?? element.Location;
        var box = LayoutText(text, style, autoItalic: element.Name == "mi", location);
        if (element.Name == "mo")
        {
            // The interim operator spacing: explicit lspace and rspace, else the Default category; a percentage is of it
            var space = DefaultOperatorSpace * style.FontSize;
            var leading = _styles.ResolveLength(element, "lspace", style, space) ?? space;
            var trailing = _styles.ResolveLength(element, "rspace", style, space) ?? space;
            return Copy(box, isSpaceLike: false, new EmbellishedOperator(leading, trailing));
        }

        return element.Name == "mtext" ? Copy(box, isSpaceLike: true, null) : box;
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
