// Portions derived from Vega (BSD-3-Clause); see third-party/README.md
using System.Collections.Concurrent;
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
///     Where a resolved font came from.
/// </summary>
internal enum FontSource
{
    /// <summary>One of the Liberation fonts bundled with the core library: the same on every machine.</summary>
    Bundled,

    /// <summary>A font file found in one of <see cref="VegaLiteFontOptions.AdditionalDirectories"/>.</summary>
    AdditionalDirectory,

    /// <summary>A font installed on the host, used because <see cref="VegaLiteFontOptions.UseSystemFonts"/> is set.</summary>
    System,
}

/// <summary>
///     A font face that a <see cref="TextFont"/> request resolved to.
/// </summary>
/// <param name="Font">The loaded font; shared and safe to use from several threads.</param>
/// <param name="Family">The family name of the face (for example <c>Liberation Sans</c>).</param>
/// <param name="Source">Where the face came from.</param>
/// <param name="Bold">Whether the face is bold.</param>
/// <param name="Italic">Whether the face is italic.</param>
internal sealed record ResolvedFont(TrueTypeFont Font, string Family, FontSource Source, bool Bold, bool Italic);

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
///         <b>Resolution.</b> The family list is read from left to right and the first family that resolves wins.
///         <c>sans-serif</c>, <c>system-ui</c> and the common sans families (Helvetica, Arial, Helvetica Neue, Verdana,
///         Tahoma, Segoe UI, ...) resolve to Liberation Sans; <c>serif</c>, Times, Times New Roman, Georgia and similar
///         to Liberation Serif; <c>monospace</c>, Courier, Courier New, Menlo, Consolas and similar to Liberation Mono.
///         Any other family is looked up by name (case-insensitive) in the
///         <see cref="VegaLiteFontOptions.AdditionalDirectories"/> and then, only when
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
    /// <summary>Family names that resolve to the bundled sans-serif font.</summary>
    private static readonly HashSet<string> SansFamilies = new(StringComparer.OrdinalIgnoreCase)
    {
        "sans-serif", "system-ui", "ui-sans-serif", "-apple-system", "blinkmacsystemfont", "helvetica", "helvetica neue",
        "arial", "verdana", "tahoma", "trebuchet ms", "segoe ui", "liberation sans", "dejavu sans", "roboto", "open sans",
    };

    /// <summary>Family names that resolve to the bundled serif font.</summary>
    private static readonly HashSet<string> SerifFamilies = new(StringComparer.OrdinalIgnoreCase)
    {
        "serif", "ui-serif", "times", "times new roman", "georgia", "liberation serif", "palatino", "palatino linotype",
        "book antiqua", "cambria", "garamond",
    };

    /// <summary>Family names that resolve to the bundled monospace font.</summary>
    private static readonly HashSet<string> MonospaceFamilies = new(StringComparer.OrdinalIgnoreCase)
    {
        "monospace", "ui-monospace", "courier", "courier new", "menlo", "consolas", "monaco", "lucida console",
        "liberation mono", "dejavu sans mono",
    };

    /// <summary>The most font files that are read from the additional directories.</summary>
    private const int MaxDirectoryFonts = 4096;

    /// <summary>The font options.</summary>
    private readonly VegaLiteFontOptions _options;

    /// <summary>The resolved fonts by family list, bold and italic.</summary>
    private readonly ConcurrentDictionary<(string Family, bool Bold, bool Italic), ResolvedFont> _cache = new();

    /// <summary>The fonts found in the additional directories, read on first use.</summary>
    private readonly Lazy<IReadOnlyList<ResolvedFont>> _directoryFonts;

    /// <summary>
    ///     Initializes a measurer.
    /// </summary>
    /// <param name="options">The font options, or null for the defaults (bundled fonts only).</param>
    public TextMeasurer(VegaLiteFontOptions? options = null)
    {
        _options = options ?? new VegaLiteFontOptions();
        _directoryFonts = new Lazy<IReadOnlyList<ResolvedFont>>(() => LoadDirectoryFonts(_options.AdditionalDirectories), LazyThreadSafetyMode.ExecutionAndPublication);
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
    ///     Splits a CSS font family list into family names.
    /// </summary>
    /// <param name="families">The list, for example <c>"Helvetica Neue", Arial, sans-serif</c>.</param>
    /// <returns>The names without quotes or surrounding white space, in order; empty entries are dropped.</returns>
    public static IReadOnlyList<string> SplitFamilies(string? families)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(families))
        {
            return result;
        }

        var current = new System.Text.StringBuilder();
        var quote = '\0';
        void Flush()
        {
            var name = current.ToString().Trim();
            current.Clear();
            if (name.Length > 0)
            {
                result.Add(name);
            }
        }

        foreach (var c in families)
        {
            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c == ',')
            {
                Flush();
            }
            else
            {
                current.Append(c);
            }
        }

        Flush();
        return result;
    }

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
    public ResolvedFont Resolve(string? family, string? weight, string? style)
    {
        var bold = IsBold(weight);
        var italic = IsItalic(style);
        return _cache.GetOrAdd((family ?? string.Empty, bold, italic), key => ResolveUncached(SplitFamilies(key.Family), key.Bold, key.Italic));
    }

    /// <summary>
    ///     Measures the advance width of one line of text, without trimming or truncating it.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="font">The font.</param>
    /// <returns>The width in pixels; 0 for an empty text or a font size that is not positive.</returns>
    public double MeasureWidth(string? text, TextFont font)
    {
        if (string.IsNullOrEmpty(text) || !(font.Size > 0) || !double.IsFinite(font.Size))
        {
            return 0;
        }

        return TextRenderer.MeasureText(text, Resolve(font).Font, (float)font.Size).Width;
    }

    /// <summary>
    ///     Gets the ascent and descent of the font's own line box at the font's size.
    /// </summary>
    /// <param name="font">The font.</param>
    /// <returns>The metrics; zero for a font size that is not positive.</returns>
    public FontMetrics GetMetrics(TextFont font)
    {
        if (!(font.Size > 0) || !double.IsFinite(font.Size))
        {
            return new FontMetrics(0, 0);
        }

        var metrics = TextRenderer.MeasureText(" ", Resolve(font).Font, (float)font.Size);
        return new FontMetrics(metrics.Ascent, metrics.Descent);
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
    /// <returns>The extent: the width of the widest line, and the height of the first line (the font size) plus the line height of each further line.</returns>
    public TextExtent Measure(IEnumerable<string> lines, TextFont font, double limit = 0, double? lineHeight = null, string? ellipsis = null)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var shown = lines.Select(l => TextValue(l, font, limit, ellipsis)).ToList();
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
    /// <returns>The bounds; text without lines still has the width 0 and the height of one line.</returns>
    public SceneBounds GetBounds(SceneText text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var font = new TextFont(text.FontFamily, text.FontSize, text.FontWeight, text.FontStyle);
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
    ///     Resolves a family list without the cache.
    /// </summary>
    /// <param name="families">The family names.</param>
    /// <param name="bold">Whether bold is requested.</param>
    /// <param name="italic">Whether italic is requested.</param>
    /// <returns>The face.</returns>
    private ResolvedFont ResolveUncached(IReadOnlyList<string> families, bool bold, bool italic)
    {
        foreach (var family in families)
        {
            if (SansFamilies.Contains(family))
            {
                return Bundled(serif: false, fixedPitch: false, bold, italic);
            }

            if (SerifFamilies.Contains(family))
            {
                return Bundled(serif: true, fixedPitch: false, bold, italic);
            }

            if (MonospaceFamilies.Contains(family))
            {
                return Bundled(serif: false, fixedPitch: true, bold, italic);
            }

            var named = FindNamed(family, bold, italic);
            if (named is not null)
            {
                return named;
            }
        }

        return Bundled(serif: false, fixedPitch: false, bold, italic);
    }

    /// <summary>
    ///     Gets a bundled Liberation face.
    /// </summary>
    /// <param name="serif">Whether the serif family is requested.</param>
    /// <param name="fixedPitch">Whether the monospace family is requested.</param>
    /// <param name="bold">Whether bold is requested.</param>
    /// <param name="italic">Whether italic is requested.</param>
    /// <returns>The face.</returns>
    private static ResolvedFont Bundled(bool serif, bool fixedPitch, bool bold, bool italic)
    {
        var font = SystemFontCatalog.LoadBundledFallback(serif, fixedPitch, bold, italic);
        var family = "Liberation Sans";
        if (fixedPitch)
        {
            family = "Liberation Mono";
        }
        else if (serif)
        {
            family = "Liberation Serif";
        }

        return new ResolvedFont(font, family, FontSource.Bundled, bold, italic);
    }

    /// <summary>
    ///     Looks a named family up in the additional directories and, when allowed, the installed fonts.
    /// </summary>
    /// <param name="family">The family name.</param>
    /// <param name="bold">Whether bold is requested.</param>
    /// <param name="italic">Whether italic is requested.</param>
    /// <returns>The face, or null when no allowed source has the family.</returns>
    private ResolvedFont? FindNamed(string family, bool bold, bool italic)
    {
        ResolvedFont? best = null;
        var bestScore = int.MaxValue;
        foreach (var candidate in _directoryFonts.Value)
        {
            if (!string.Equals(candidate.Family, family, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var score = (candidate.Bold != bold ? 1 : 0) + (candidate.Italic != italic ? 1 : 0);
            if (score < bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        if (best is not null || !_options.UseSystemFonts)
        {
            return best;
        }

        // Installed fonts: the catalog entries of the family in catalog order, best style first, until one loads
        var systemMatches = SystemFontCatalog.Fonts
            .Where(f => string.Equals(f.FamilyName, family, StringComparison.OrdinalIgnoreCase))
            .Select((f, index) => (Font: f, Index: index, Score: (f.Bold != bold ? 1 : 0) + (f.Italic != italic ? 1 : 0)))
            .OrderBy(m => m.Score)
            .ThenBy(m => m.Index)
            .Select(m => m.Font);
        foreach (var match in systemMatches)
        {
            var loaded = TryLoad(match.FilePath, match.FaceIndex);
            if (loaded is not null)
            {
                return new ResolvedFont(loaded, match.FamilyName, FontSource.System, match.Bold, match.Italic);
            }
        }

        return null;
    }

    /// <summary>
    ///     Reads the font files of the additional directories.
    /// </summary>
    /// <param name="directories">The directories, in order.</param>
    /// <returns>The faces found: directories in the given order, files in ordinal order within a directory.</returns>
    private static IReadOnlyList<ResolvedFont> LoadDirectoryFonts(IReadOnlyList<string> directories)
    {
        var result = new List<ResolvedFont>();
        var seen = 0;
        foreach (var directory in directories)
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                MaxRecursionDepth = 4,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System,
                MatchCasing = MatchCasing.CaseInsensitive,
            };
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(directory, "*.*", options)
                    .Where(f => f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)
                        || f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase)
                        || f.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase))
                    .Order(StringComparer.Ordinal)
                    .ToList();
            }
            catch (Exception ex) when (IsSkippable(ex))
            {
                continue;
            }

            foreach (var file in files)
            {
                if (++seen > MaxDirectoryFonts)
                {
                    return result;
                }

                AddFaces(file, result);
            }
        }

        return result;
    }

    /// <summary>
    ///     Adds every readable face of a font file.
    /// </summary>
    /// <param name="file">The path of the file.</param>
    /// <param name="result">The list that receives the faces.</param>
    private static void AddFaces(string file, List<ResolvedFont> result)
    {
        int faceCount;
        try
        {
            faceCount = TrueTypeFont.GetFaceCount(file);
        }
        catch (Exception ex) when (IsSkippable(ex))
        {
            return;
        }

        for (var face = 0; face < faceCount; face++)
        {
            var font = TryLoad(file, face);
            if (font is null)
            {
                continue;
            }

            var name = font.GetNameInfo();
            var family = string.IsNullOrWhiteSpace(name.FamilyName) ? Path.GetFileNameWithoutExtension(file) : name.FamilyName;
            result.Add(new ResolvedFont(font, family, FontSource.AdditionalDirectory, font.IsBold, font.IsItalic));
        }
    }

    /// <summary>
    ///     Loads one face of a font file, treating an unusable file as absent.
    /// </summary>
    /// <param name="file">The path of the file.</param>
    /// <param name="faceIndex">The face index.</param>
    /// <returns>The font, or null when the file cannot be read as a font.</returns>
    private static TrueTypeFont? TryLoad(string file, int faceIndex)
    {
        try
        {
            return TrueTypeFont.Load(file, faceIndex);
        }
        catch (Exception ex) when (IsSkippable(ex))
        {
            return null;
        }
    }

    /// <summary>
    ///     Determines whether an exception only means that a font file is unusable.
    /// </summary>
    /// <param name="ex">The exception.</param>
    /// <returns><see langword="true"/> for the failures of reading a missing, locked or malformed file.</returns>
    private static bool IsSkippable(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException
            or NotSupportedException or OverflowException or FormatException or InvalidOperationException;
}
