// Portions derived from PlantUML (MIT); see third-party/README.md
using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Rendering;

namespace DemaConsulting.CanvasNet.PlantUml.Scenegraph;

/// <summary>
///     How the lines of a text block are aligned in its width.
/// </summary>
internal enum TextAlign
{
    /// <summary>Lines start at the left of the block.</summary>
    Left,

    /// <summary>Lines are centered in the block.</summary>
    Center,

    /// <summary>Lines end at the right of the block.</summary>
    Right,
}

/// <summary>
///     One laid-out line of text: its runs, its width, its height, and the descent below its baseline.
/// </summary>
/// <param name="Runs">The runs in order; empty for an empty line.</param>
/// <param name="Width">The width (the sum of the run advances).</param>
/// <param name="Height">The line height (the largest of its fonts).</param>
/// <param name="Descent">The descent below the baseline (the largest of its fonts).</param>
internal sealed record TextLine(IReadOnlyList<SceneTextRun> Runs, double Width, double Height, double Descent);

/// <summary>
///     A laid-out text: lines stacked from the top, with the size of the whole block.
/// </summary>
/// <param name="Lines">The lines from top to bottom.</param>
internal sealed record TextBlock(IReadOnlyList<TextLine> Lines)
{
    /// <summary>
    ///     Gets an empty block.
    /// </summary>
    public static TextBlock Empty { get; } = new([]);

    /// <summary>
    ///     Gets the width of the block: its widest line.
    /// </summary>
    public double Width => Lines.Count == 0 ? 0 : Lines.Max(l => l.Width);

    /// <summary>
    ///     Gets the height of the block: the sum of its line heights.
    /// </summary>
    public double Height => Lines.Sum(l => l.Height);

    /// <summary>
    ///     Gets the text of the block, its lines joined with line feeds.
    /// </summary>
    public string Text => string.Join('\n', Lines.Select(l => string.Concat(l.Runs.Select(r => r.Text))));

    /// <summary>
    ///     Draws the block with its top left corner at a point.
    /// </summary>
    /// <remarks>
    ///     A line's baseline is its top plus its height minus its descent, as PlantUML places text; the lines are aligned
    ///     in the width of the block.
    /// </remarks>
    /// <param name="x">The left of the block.</param>
    /// <param name="top">The top of the block.</param>
    /// <param name="align">The alignment of the lines.</param>
    /// <param name="role">The role of the items.</param>
    /// <param name="location">The location of the construct the text belongs to.</param>
    /// <returns>One text item per non-empty line.</returns>
    public IEnumerable<SceneText> Draw(double x, double top, TextAlign align, string role, PlantUmlLocation location)
    {
        var width = Width;
        var y = top;
        foreach (var line in Lines)
        {
            if (line.Runs.Count > 0)
            {
                var lineX = align switch
                {
                    TextAlign.Center => x + ((width - line.Width) / 2),
                    TextAlign.Right => x + width - line.Width,
                    _ => x,
                };
                var baseline = y + line.Height - line.Descent;
                yield return new SceneText(lineX, baseline, line.Runs, new SceneRect(lineX, y, line.Width, line.Height))
                {
                    Role = role,
                    Location = location,
                };
            }

            y += line.Height;
        }
    }
}

/// <summary>
///     Measures and lays out the texts of a diagram with the bundled fonts, as PlantUML measures them with Java's logical
///     fonts, and reads PlantUML's creole markup.
/// </summary>
/// <remarks>
///     <para>
///         <b>Fonts.</b> Only the fonts bundled with CanvasNet are used: <c>SansSerif</c>, <c>sans-serif</c>,
///         <c>Arial</c>, <c>Helvetica</c>, and any unknown family are Liberation Sans (which has the metrics of Arial,
///         Java's <c>SansSerif</c> on Windows), <c>Serif</c> and <c>Times</c> are Liberation Serif, and
///         <c>Monospaced</c> and <c>Courier</c> are Liberation Mono. A character the face lacks is measured and drawn
///         with the core's bundled fallback fonts and reported as the warning
///         <see cref="PlantUmlDiagnosticCodes.TextMissingGlyph"/>.
///     </para>
///     <para>
///         <b>Metrics.</b> A run is as wide as the sum of the advances of its glyphs (fractional, no kerning, as Java
///         measures with fractional metrics). A line is as high as the ascent, descent and line gap of its font, where
///         the ascent and the descent are at least those of Java's logical fonts on Windows (2059 and 450 units of 2048),
///         because PlantUML's <c>SansSerif</c> is a logical font whose line makes room for the fonts it falls back on: a
///         line of 14 pixels is 17.609 pixels high.
///     </para>
///     <para>
///         An instance caches the faces it resolves and is used by one call on one thread.
///     </para>
/// </remarks>
internal sealed partial class TextLayout
{
    /// <summary>The smallest ascent of Java's logical fonts on Windows, as a fraction of the size.</summary>
    internal const double LogicalMinAscent = 2059.0 / 2048.0;

    /// <summary>The smallest descent of Java's logical fonts on Windows, as a fraction of the size.</summary>
    internal const double LogicalMinDescent = 450.0 / 2048.0;

    /// <summary>The fonts the text is measured with.</summary>
    private readonly FontResolver _fonts;

    /// <summary>The faces resolved so far, by family, weight and style.</summary>
    private readonly Dictionary<(string Family, bool Bold, bool Italic), ResolvedFont> _faces = [];

    /// <summary>
    ///     Initializes a text layout.
    /// </summary>
    /// <param name="fonts">The font resolver of the call (normally <see cref="CreateFontResolver"/>), shared with the painter.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fonts"/> is null.</exception>
    public TextLayout(FontResolver fonts)
    {
        ArgumentNullException.ThrowIfNull(fonts);
        _fonts = fonts;
    }

    /// <summary>
    ///     Gets the font resolver the layout measures with.
    /// </summary>
    public FontResolver Fonts => _fonts;

    /// <summary>
    ///     Creates the font resolver of a rendering: bundled fonts only, so text never depends on the fonts installed on
    ///     the machine.
    /// </summary>
    /// <returns>A new resolver for one rendering.</returns>
    public static FontResolver CreateFontResolver() =>
        new(new FontResolverOptions { MapCommonAliasesToBundled = true, UseSystemFonts = false });

    /// <summary>
    ///     Maps a PlantUML font family to the CSS generic family of the bundled font that draws it.
    /// </summary>
    /// <param name="family">The family as PlantUML writes it.</param>
    /// <returns><c>serif</c>, <c>monospace</c>, or <c>sans-serif</c>.</returns>
    public static string CssFamily(string family)
    {
        var name = (family ?? string.Empty).Trim().Trim('"', '\'').ToLowerInvariant();
        if (name is "monospaced" or "monospace" or "courier" or "courier new" or "liberation mono" or "consolas")
        {
            return "monospace";
        }

        return name is "serif" or "times" or "times new roman" or "liberation serif" ? "serif" : "sans-serif";
    }

    /// <summary>
    ///     Resolves the bundled face of a font.
    /// </summary>
    /// <param name="font">The font.</param>
    /// <returns>The face.</returns>
    public ResolvedFont Resolve(SceneFont font)
    {
        ArgumentNullException.ThrowIfNull(font);
        var key = (CssFamily(font.Family), font.Bold, font.Italic);
        if (!_faces.TryGetValue(key, out var face))
        {
            face = _fonts.Resolve(key.Item1, font.Bold, font.Italic, out _);
            _faces[key] = face;
        }

        return face;
    }

    /// <summary>
    ///     Gets the height of a line of a font and its descent below the baseline.
    /// </summary>
    /// <param name="font">The font.</param>
    /// <returns>The line height and the descent, in scene units.</returns>
    public (double Height, double Descent) LineMetrics(SceneFont font)
    {
        var face = Resolve(font).Font;
        var units = (double)face.UnitsPerEm;
        var ascent = Math.Max(face.Ascender / units, LogicalMinAscent);
        var descent = Math.Max(Math.Abs(face.Descender) / units, LogicalMinDescent);
        var gap = Math.Max(0, face.LineGap) / units;
        return ((ascent + descent + gap) * font.Size, descent * font.Size);
    }

    /// <summary>
    ///     Measures the advance of a text in a font.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="font">The font.</param>
    /// <returns>The width in scene units.</returns>
    public double Width(string text, SceneFont font) => Width(text, font, out _);

    /// <summary>
    ///     Measures the advance of a text in a font and tells whether a character is missing from the face.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="font">The font.</param>
    /// <param name="missing">Receives whether a visible character has no glyph in the face.</param>
    /// <returns>The width in scene units.</returns>
    public double Width(string text, SceneFont font, out bool missing)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(font);
        missing = false;
        if (text.Length == 0 || !(font.Size > 0))
        {
            return 0;
        }

        var face = Resolve(font).Font;
        var scale = font.Size / face.UnitsPerEm;
        double width = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            var glyph = face.GetGlyphIndex(rune.Value);
            if (glyph == 0 && !Rune.IsWhiteSpace(rune) && !Rune.IsControl(rune))
            {
                missing = true;
            }

            width += face.GetAdvanceWidth(glyph) * scale;
        }

        // A run with a missing character is measured as it is drawn: with the bundled fallback fonts
        return missing ? TextRenderer.MeasureText(text, face, (float)font.Size).Width : width;
    }

    /// <summary>
    ///     Lays out a text as PlantUML displays it: <c>\n</c> breaks a line, creole markup changes the style of a part,
    ///     and lines longer than a width are wrapped at spaces.
    /// </summary>
    /// <param name="display">The text as written in the diagram (real line feeds also break lines).</param>
    /// <param name="font">The base font.</param>
    /// <param name="color">The base color.</param>
    /// <param name="wrapWidth">The width at which lines are wrapped, or zero for no wrapping.</param>
    /// <param name="sink">The report sink of the call, or null to report nothing.</param>
    /// <param name="location">The location of the construct, for the reports.</param>
    /// <returns>The block; empty for an empty text.</returns>
    public TextBlock Layout(string display, SceneFont font, Rgba32 color, double wrapWidth = 0, PlantUmlReportSink? sink = null, PlantUmlLocation location = default)
    {
        ArgumentNullException.ThrowIfNull(display);
        ArgumentNullException.ThrowIfNull(font);
        if (display.Length == 0)
        {
            return TextBlock.Empty;
        }

        // Each source line is parsed into styled pieces, wrapped, and measured
        var parser = new CreoleParser(font, color);
        var lines = new List<TextLine>();
        var missing = false;
        foreach (var source in SplitLines(display))
        {
            var pieces = parser.Parse(source);
            foreach (var wrapped in Wrap(pieces, wrapWidth))
            {
                lines.Add(Measure(wrapped, font, ref missing));
            }
        }

        if (parser.Unsupported is { } markup)
        {
            sink?.Unsupported(
                PlantUmlDiagnosticCodes.MarkupUnsupported,
                $"the creole markup '{markup}' is not supported; it is drawn as text",
                location,
                "use **bold**, //italic//, \"\"mono\"\", __underline__, --strike--, <color>, <size> and <font>");
        }

        if (missing)
        {
            sink?.Warn(
                PlantUmlDiagnosticCodes.TextMissingGlyph,
                "a character of the text has no glyph in the bundled font; it is drawn with a fallback font",
                location);
        }

        return new TextBlock(lines);
    }

    /// <summary>
    ///     Splits a display text into its lines: at PlantUML's <c>\n</c> (a backslash and an n, unless the backslash is
    ///     escaped) and at real line feeds.
    /// </summary>
    /// <param name="display">The display text.</param>
    /// <returns>The lines, with escaped backslashes undone.</returns>
    internal static List<string> SplitLines(string display)
    {
        var lines = new List<string>();
        var current = new StringBuilder();
        var i = 0;
        while (i < display.Length)
        {
            var c = display[i];
            var next = i + 1 < display.Length ? display[i + 1] : '\0';
            if (c == '\\' && next is 'n' or 'r' or 'l')
            {
                // \n breaks the line; PlantUML's \r and \l also break it (right and left aligned lines are drawn as one)
                lines.Add(current.ToString());
                current.Clear();
                i += 2;
                continue;
            }

            if (c == '\\' && next is '\\' or 't')
            {
                current.Append(next == 't' ? '\t' : '\\');
                i += 2;
                continue;
            }

            if (c == '\n')
            {
                lines.Add(current.ToString());
                current.Clear();
            }
            else if (c != '\r')
            {
                current.Append(c);
            }

            i++;
        }

        lines.Add(current.ToString());
        return lines;
    }

    /// <summary>
    ///     Measures the styled pieces of one line into a line.
    /// </summary>
    /// <param name="pieces">The pieces.</param>
    /// <param name="font">The base font, which gives an empty line its height.</param>
    /// <param name="missing">Set when a character is missing from its face.</param>
    /// <returns>The line.</returns>
    private TextLine Measure(List<CreolePiece> pieces, SceneFont font, ref bool missing)
    {
        var runs = new List<SceneTextRun>();
        var (height, descent) = LineMetrics(font);
        if (pieces.Count > 0)
        {
            height = 0;
            descent = 0;
        }

        double width = 0;
        foreach (var piece in pieces)
        {
            var (pieceHeight, pieceDescent) = LineMetrics(piece.Font);
            height = Math.Max(height, pieceHeight);
            descent = Math.Max(descent, pieceDescent);
            if (piece.Text.Length == 0)
            {
                continue;
            }

            var runWidth = Width(piece.Text, piece.Font, out var pieceMissing);
            missing |= pieceMissing;
            runs.Add(new SceneTextRun(piece.Text, piece.Font, piece.Color, runWidth) { Underline = piece.Underline, Strike = piece.Strike });
            width += runWidth;
        }

        return new TextLine(runs, width, height, descent);
    }

    /// <summary>
    ///     Wraps the pieces of a line at spaces so that no line is wider than a width (a word wider than the width stays
    ///     on its own line).
    /// </summary>
    /// <param name="pieces">The pieces of the line.</param>
    /// <param name="wrapWidth">The width, or zero for no wrapping.</param>
    /// <returns>The wrapped lines.</returns>
    private List<List<CreolePiece>> Wrap(List<CreolePiece> pieces, double wrapWidth)
    {
        if (!(wrapWidth > 0))
        {
            return [pieces];
        }

        // Split the pieces into words that keep their style, then fill lines greedily
        var words = new List<List<CreolePiece>>();
        var word = new List<CreolePiece>();
        foreach (var piece in pieces)
        {
            var parts = piece.Text.Split(' ');
            for (var i = 0; i < parts.Length; i++)
            {
                if (i > 0)
                {
                    words.Add(word);
                    word = [];
                }

                if (parts[i].Length > 0)
                {
                    word.Add(piece with { Text = parts[i] });
                }
            }
        }

        words.Add(word);
        var lines = new List<List<CreolePiece>>();
        var line = new List<CreolePiece>();
        double lineWidth = 0;
        foreach (var w in words.Where(w => w.Count > 0))
        {
            var wordWidth = w.Sum(p => Width(p.Text, p.Font));
            var space = line.Count > 0 ? Width(" ", line[^1].Font) : 0;
            if (line.Count > 0 && lineWidth + space + wordWidth > wrapWidth)
            {
                lines.Add(line);
                line = [];
                lineWidth = 0;
                space = 0;
            }

            if (line.Count > 0)
            {
                line.Add(line[^1] with { Text = " " });
            }

            line.AddRange(w);
            lineWidth += space + wordWidth;
        }

        lines.Add(line);
        return lines;
    }
}
