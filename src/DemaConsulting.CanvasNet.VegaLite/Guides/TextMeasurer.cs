// Portions derived from Vega (BSD-3-Clause); see third-party/README.md
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Rendering;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Guides;

/// <summary>
///     A font request: a CSS family list, a size and the weight and style that select the face.
/// </summary>
/// <param name="Family">The CSS font family list (<c>"Helvetica Neue", Arial, sans-serif</c>), or null or empty for <c>sans-serif</c>.</param>
/// <param name="Size">The size in pixels.</param>
/// <param name="Weight">The weight: <c>normal</c>, <c>bold</c>, <c>bolder</c> or a number (600 and above is bold); null is normal.</param>
/// <param name="Style">The style: <c>normal</c>, <c>italic</c> or <c>oblique</c>; null is normal.</param>
internal readonly record struct TextFont(string? Family, double Size, string? Weight = null, string? Style = null);

/// <summary>
///     The size of a block of text lines.
/// </summary>
/// <param name="Width">The width of the widest line after truncation.</param>
/// <param name="Height">The height of the block: the font size plus the line height for every further line.</param>
/// <param name="LineHeight">The distance between the baselines of consecutive lines.</param>
/// <param name="Lines">The lines as they are drawn: trimmed and truncated to the limit.</param>
internal readonly record struct TextExtent(double Width, double Height, double LineHeight, IReadOnlyList<string> Lines);

/// <summary>
///     Font ascent and descent of a resolved font at a size.
/// </summary>
/// <param name="Ascent">The distance from the baseline to the top of the font's line box.</param>
/// <param name="Descent">The distance from the baseline to the bottom of the font's line box (positive).</param>
internal readonly record struct FontMetrics(double Ascent, double Descent);

/// <summary>
///     Resolves CSS font requests to fonts and measures text with them.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why.</b> Chart layout depends on the width of labels, so measuring must give the same numbers on every
///         machine. By default every request resolves to one of the Liberation fonts that are bundled with the core
///         library, whatever fonts are installed.
///     </para>
///     <para>
///         <b>Resolution.</b> The core <see cref="FontResolver"/> does it, with the common aliases mapped to the
///         bundled fonts first (<see cref="FontResolverOptions.MapCommonAliasesToBundled"/>): the family list is read
///         from left to right and the first family that resolves wins. <c>sans-serif</c>, <c>system-ui</c> and the
///         common sans families (Helvetica, Arial, Helvetica Neue, Verdana, Tahoma, Segoe UI, ...) resolve to Liberation
///         Sans; <c>serif</c>, Times, Times New Roman, Georgia and similar to Liberation Serif; <c>monospace</c>,
///         Courier, Courier New, Menlo, Consolas and similar to Liberation Mono. Any other family is looked up by name
///         (case-insensitive) in the <see cref="VegaLiteFontOptions.AdditionalDirectories"/> and then, only when
///         <see cref="VegaLiteFontOptions.UseSystemFonts"/> is set, among the installed fonts; when nothing matches the
///         next family is tried, and an empty or exhausted list means Liberation Sans. A weight of 600 or more, or
///         <c>bold</c>/<c>bolder</c>, selects the bold face; <c>italic</c> or <c>oblique</c> the italic face. A family
///         that lacks the requested style is used as it is.
///     </para>
///     <para>
///         <b>Measuring.</b> Widths are the advance widths (with kerning) from the font at the requested size, computed
///         by the core text renderer; they are never rounded. Vega's text layout does not use the font's ascent and
///         descent: a line is <c>fontSize</c> tall, successive lines are <c>lineHeight</c> (default <c>fontSize + 2</c>)
///         apart, and the baseline of a text mark is shifted from its anchor by fixed fractions of the font size
///         (<see cref="BaselineOffset"/>, ported from Vega's <c>offset</c>), so that layout does not depend on the
///         metrics of one font. This unit follows that rule; the ascent and descent that <see cref="GetMetrics"/>
///         returns are for painters that need the font's own line box. Truncation to a limit follows Vega's algorithm:
///         the line is cut with a binary search at the longest prefix whose width plus the ellipsis stays below the
///         limit.
///     </para>
///     <para>
///         Fonts are cached per instance, so a measurer should be shared for one render. The class is safe for
///         concurrent use and every result is deterministic.
///     </para>
/// </remarks>
internal sealed class TextMeasurer
{
    /// <summary>The resolver that picks the face for a request; it caches the faces already resolved.</summary>
    private readonly FontResolver _resolver;

    /// <summary>
    ///     Initializes a measurer.
    /// </summary>
    /// <param name="options">The font options, or null for the defaults (bundled fonts only).</param>
    public TextMeasurer(VegaLiteFontOptions? options = null)
    {
        options ??= new VegaLiteFontOptions();

        // The common aliases map to the bundled fonts first, so those names measure alike on every machine
        _resolver = new FontResolver(new FontResolverOptions
        {
            AdditionalDirectories = options.AdditionalDirectories,
            UseSystemFonts = options.UseSystemFonts,
            MapCommonAliasesToBundled = true,
        });
    }

    /// <summary>
    ///     Determines whether a font weight selects the bold face.
    /// </summary>
    /// <param name="weight">The weight: <c>bold</c>, <c>bolder</c>, <c>normal</c>, <c>lighter</c> or a number.</param>
    /// <returns><see langword="true"/> for <c>bold</c>, <c>bolder</c> and numbers of 600 or more.</returns>
    public static bool IsBold(string? weight)
    {
        if (string.IsNullOrWhiteSpace(weight))
        {
            return false;
        }

        var text = weight.Trim();
        return string.Equals(text, "bold", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "bolder", StringComparison.OrdinalIgnoreCase)
            || (double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number) && number >= 600);
    }

    /// <summary>
    ///     Determines whether a font style selects the italic face.
    /// </summary>
    /// <param name="style">The style.</param>
    /// <returns><see langword="true"/> for <c>italic</c> and <c>oblique</c>.</returns>
    public static bool IsItalic(string? style) =>
        !string.IsNullOrWhiteSpace(style)
        && (string.Equals(style.Trim(), "italic", StringComparison.OrdinalIgnoreCase)
            || string.Equals(style.Trim(), "oblique", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    ///     Splits text into lines at line feeds (a carriage return, alone or before a line feed, also ends a line).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The lines; null or an empty text has none.</returns>
    public static IReadOnlyList<string> SplitLines(string? text) =>
        string.IsNullOrEmpty(text)
            ? []
            : text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

    /// <summary>
    ///     Gets the distance between the baselines of consecutive lines.
    /// </summary>
    /// <param name="fontSize">The font size.</param>
    /// <param name="lineHeight">The explicit line height, or null for the font size plus 2 (Vega's default).</param>
    /// <returns>The line height.</returns>
    public static double LineHeight(double fontSize, double? lineHeight = null) => lineHeight ?? (fontSize + 2);

    /// <summary>
    ///     Rounds half up, like <c>Math.round</c> of JavaScript (and unlike <see cref="Math.Round(double)"/>, which rounds half to even).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The rounded value.</returns>
    public static double RoundHalfUp(double value) => Math.Floor(value + 0.5);

    /// <summary>
    ///     Gets the vertical shift of a text mark's baseline from its anchor: Vega's text offset.
    /// </summary>
    /// <remarks>
    ///     Vega shifts the alphabetic baseline by fixed fractions of the font size so that the same baseline setting
    ///     lays out alike in every renderer: <c>top</c> 0.79, <c>middle</c> 0.30 and <c>bottom</c> -0.21 of the font size;
    ///     <c>line-top</c> and <c>line-bottom</c> 0.29 of it plus or minus half the line height; anything else (<c>alphabetic</c>) 0.
    ///     The result is rounded half up to a whole pixel, and is added to the anchor's y to get the baseline to draw at.
    /// </remarks>
    /// <param name="baseline">The baseline name.</param>
    /// <param name="fontSize">The font size.</param>
    /// <param name="lineHeight">The explicit line height, or null for the default.</param>
    /// <returns>The offset in pixels.</returns>
    public static double BaselineOffset(string? baseline, double fontSize, double? lineHeight = null) =>
        RoundHalfUp(baseline switch
        {
            "top" => 0.79 * fontSize,
            "middle" => 0.30 * fontSize,
            "bottom" => -0.21 * fontSize,
            "line-top" => (0.29 * fontSize) + (0.5 * LineHeight(fontSize, lineHeight)),
            "line-bottom" => (0.29 * fontSize) - (0.5 * LineHeight(fontSize, lineHeight)),
            _ => 0,
        });

    /// <summary>
    ///     Resolves a font request to a face.
    /// </summary>
    /// <param name="font">The request; the size is not used.</param>
    /// <returns>The face; never null, because the bundled Liberation Sans is the last resort.</returns>
    public ResolvedFont Resolve(TextFont font) => Resolve(font.Family, font.Weight, font.Style);

    /// <summary>
    ///     Resolves a family list, weight and style to a face.
    /// </summary>
    /// <param name="family">The CSS family list, or null for <c>sans-serif</c>.</param>
    /// <param name="weight">The weight, or null for normal.</param>
    /// <param name="style">The style, or null for normal.</param>
    /// <returns>The face; never null, because the bundled Liberation Sans is the last resort.</returns>
    public ResolvedFont Resolve(string? family, string? weight, string? style) =>
        _resolver.Resolve(family, IsBold(weight), IsItalic(style), out _);

    /// <summary>
    ///     Measures the advance width of one line of text, without trimming or truncating it.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="font">The font.</param>
    /// <returns>The width in pixels; 0 for an empty text or a font size that is not positive, or that is not a finite number as a single (the renderer cannot size such a font).</returns>
    public double MeasureWidth(string? text, TextFont font)
    {
        if (string.IsNullOrEmpty(text) || !TryGetRenderSize(font, out var size))
        {
            return 0;
        }

        return TextRenderer.MeasureText(text, Resolve(font).Font, size).Width;
    }

    /// <summary>
    ///     Gets the ascent and descent of the font's own line box at the font's size.
    /// </summary>
    /// <param name="font">The font.</param>
    /// <returns>The metrics; zero for a font size that is not positive or not a finite number as a single.</returns>
    public FontMetrics GetMetrics(TextFont font)
    {
        if (!TryGetRenderSize(font, out var size))
        {
            return new FontMetrics(0, 0);
        }

        var metrics = TextRenderer.MeasureText(" ", Resolve(font).Font, size);
        return new FontMetrics(metrics.Ascent, metrics.Descent);
    }

    /// <summary>
    ///     Converts the size of a font to the single that the text renderer takes.
    /// </summary>
    /// <remarks>
    ///     A size that is finite as a double can still overflow to infinity (1e300) or underflow to zero as a single, and the core text renderer throws
    ///     <see cref="ArgumentOutOfRangeException"/> for such a size; a text with such a size is unmeasurable instead (zero width), so that no raw exception escapes.
    /// </remarks>
    /// <param name="font">The font.</param>
    /// <param name="size">Receives the size.</param>
    /// <returns><see langword="true"/> when the size is a finite single greater than zero.</returns>
    private static bool TryGetRenderSize(TextFont font, out float size)
    {
        size = double.IsFinite(font.Size) ? (float)font.Size : 0f;
        return float.IsFinite(size) && size > 0f;
    }

    /// <summary>
    ///     Cuts a line to a width limit, ending it with an ellipsis (Vega's truncation).
    /// </summary>
    /// <param name="text">The line.</param>
    /// <param name="font">The font.</param>
    /// <param name="limit">The width limit; a text narrower than the limit is returned unchanged.</param>
    /// <param name="ellipsis">The text that marks the cut; null or empty for the horizontal ellipsis.</param>
    /// <param name="rightToLeft"><see langword="true"/> to cut at the start of the line instead of the end.</param>
    /// <returns>The text, possibly shortened.</returns>
    public string Truncate(string text, TextFont font, double limit, string? ellipsis = null, bool rightToLeft = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (MeasureWidth(text, font) < limit)
        {
            return text;
        }

        var mark = string.IsNullOrEmpty(ellipsis) ? "\u2026" : ellipsis;
        var room = limit - MeasureWidth(mark, font);
        var low = 0;
        var high = text.Length;
        if (rightToLeft)
        {
            while (low < high)
            {
                var mid = (low + high) >>> 1;
                if (MeasureWidth(text[mid..], font) > room)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid;
                }
            }

            return mark + text[low..];
        }

        while (low < high)
        {
            var mid = 1 + ((low + high) >>> 1);
            if (MeasureWidth(text[..mid], font) < room)
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }

        return text[..low] + mark;
    }

    /// <summary>
    ///     Gets the text that is drawn for a line: trimmed, and truncated when a limit is set (Vega's <c>textValue</c>).
    /// </summary>
    /// <param name="line">The line; null counts as empty.</param>
    /// <param name="font">The font.</param>
    /// <param name="limit">The width limit; 0 or less means none.</param>
    /// <param name="ellipsis">The ellipsis text, or null for the default.</param>
    /// <returns>The text to draw.</returns>
    public string TextValue(string? line, TextFont font, double limit = 0, string? ellipsis = null)
    {
        var text = (line ?? string.Empty).Trim();
        return limit > 0 && text.Length > 0 ? Truncate(text, font, limit, ellipsis) : text;
    }

    /// <summary>
    ///     Measures a block of lines the way Vega lays out a text mark.
    /// </summary>
    /// <param name="lines">The lines.</param>
    /// <param name="font">The font.</param>
    /// <param name="limit">The width limit of each line; 0 or less means none.</param>
    /// <param name="lineHeight">The explicit line height, or null for the font size plus 2.</param>
    /// <param name="ellipsis">The ellipsis text, or null for the default.</param>
    /// <returns>The extent: the width of the widest line, and the height of the first line (the font size) plus the line height of each further line; all zero (but for the lines) for a font size that is not positive or not a finite number as a single.</returns>
    public TextExtent Measure(IEnumerable<string> lines, TextFont font, double limit = 0, double? lineHeight = null, string? ellipsis = null)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var shown = lines.Select(l => TextValue(l, font, limit, ellipsis)).ToList();

        // A size the renderer cannot use makes the text unmeasurable in every output, so that layout never sees a huge or non-finite height
        if (!TryGetRenderSize(font, out _))
        {
            return new TextExtent(0, 0, 0, shown.AsReadOnly());
        }

        var width = shown.Count == 0 ? 0 : shown.Max(l => MeasureWidth(l, font));
        var step = LineHeight(font.Size, lineHeight);
        var height = font.Size + (step * (Math.Max(shown.Count, 1) - 1));
        return new TextExtent(width, height, step, shown.AsReadOnly());
    }

    /// <summary>
    ///     Gets the bounding box of a text run in the coordinate system of its group, the way Vega computes it.
    /// </summary>
    /// <remarks>
    ///     The box is the measured width by the height of the block (the font size plus the line height per further line),
    ///     aligned around the position horizontally and shifted vertically by the baseline offset less 80% of the font
    ///     size (Vega's "4/5 offset", which makes the box start at the top of the line for the <c>top</c> baseline), then
    ///     rotated about the position by the angle.
    /// </remarks>
    /// <param name="text">The text item.</param>
    /// <returns>The bounds; text without lines still has the width 0 and the height of one line; text whose font size is unmeasurable (see <see cref="MeasureWidth"/>) has an empty box at its position.</returns>
    public SceneBounds GetBounds(SceneText text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var font = new TextFont(text.FontFamily, text.FontSize, text.FontWeight, text.FontStyle);
        if (!TryGetRenderSize(font, out _))
        {
            // Unmeasurable text (it is never painted) has an empty box at its position, which the layout and the label-overlap pass skip
            return new SceneBounds(0, 0, 0, 0).Translate(text.X, text.Y);
        }

        var extent = Measure(text.Lines, font, text.Limit, text.LineHeight, text.Ellipsis);
        var dx = text.Dx;
        if (string.Equals(text.Align, "center", StringComparison.Ordinal))
        {
            dx -= extent.Width / 2;
        }
        else if (string.Equals(text.Align, "right", StringComparison.Ordinal))
        {
            dx -= extent.Width;
        }

        var dy = text.Dy + BaselineOffset(text.Baseline, text.FontSize, text.LineHeight) - RoundHalfUp(0.8 * text.FontSize);
        return new SceneBounds(dx, dy, dx + extent.Width, dy + extent.Height).Rotate(text.Angle).Translate(text.X, text.Y);
    }

    /// <summary>
    ///     Determines whether the measurer maps a family name to a bundled font itself (a generic or well-known family).
    /// </summary>
    /// <param name="family">The family name.</param>
    /// <returns>True when the family resolves to a bundled font whatever fonts are installed.</returns>
    internal static bool IsMappedFamily(string family) =>
        GenericFontFamilies.TryClassify(family, includeCommonAliases: true, out _, out _);
}
