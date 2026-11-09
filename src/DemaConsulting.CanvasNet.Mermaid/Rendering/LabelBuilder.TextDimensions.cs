// Portions derived from mermaid (MIT); see third-party/README.md
using System.Globalization;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Dagre;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Rendering;

/// <summary>
///     The size Mermaid's <c>utils.calculateTextDimensions</c> gives a text: whole pixels, as the browser's
///     <c>getBBox</c> rounded.
/// </summary>
/// <param name="Width">The width of the widest line.</param>
/// <param name="Height">The sum of the line heights.</param>
/// <param name="LineHeight">The height of the tallest line (zero for an empty text, where Mermaid has none).</param>
internal readonly record struct TextDimensions(double Width, double Height, double LineHeight);

/// <content>
///     <para>
///         Mermaid's text utilities for the diagrams that lay text out themselves (the sequence diagram):
///         <c>utils.calculateTextDimensions</c>, <c>wrapLabel</c>, <c>breakString</c> and <c>parseFontSize</c>, and
///         <c>common.splitBreaks</c>.
///     </para>
///     <para>
///         <b>Measurement.</b> A text is split at <c>&lt;br&gt;</c> tags; each line is measured as the SVG text Mermaid
///         appends (an empty line as a zero-width space): its width and height are those of the browser's <c>getBBox</c>
///         (the rule of <see cref="BuildSvg"/>), each rounded with <c>Math.round</c>. The text is measured with the
///         family <c>sans-serif</c> and with its own family, and the second is used unless the first is larger in all
///         three dimensions, as Mermaid does.
///     </para>
///     <para>
///         <b>Wrapping.</b> <see cref="WrapLabel"/> breaks a label at spaces to fit a width and hyphenates words that
///         are wider than it (<see cref="BreakString"/>), joining the lines with <c>&lt;br/&gt;</c>. Both are memoized
///         with Mermaid's own keys, per builder (one render), so a label is wrapped once. Mermaid tests whether a label
///         already has a <c>&lt;br&gt;</c> with its global <c>lineBreakRegex</c>, whose <c>lastIndex</c> carries over from
///         one test to the next; the builder keeps that index too, so a label after one with a break is tested from the
///         same position, as in Mermaid.
///     </para>
///     <para>
///         <b>Cost.</b> Every measurement charges the length of its text to <see cref="Meter"/>, so hyphenating a long
///         word (which measures every prefix of it) is bounded by <see cref="MermaidLimits.MaxLayoutWork"/>.
///     </para>
/// </content>
internal sealed partial class LabelBuilder
{
    /// <summary>The text Mermaid measures for an empty line (its <c>ZERO_WIDTH_SPACE</c>).</summary>
    private const string ZeroWidthSpace = "\u200b";

    /// <summary>The font size in pixels that a text gets when its size is not a valid CSS font size (the inherited one).</summary>
    private const double InheritedFontSize = 16;

    /// <summary>The memoized results of <see cref="WrapLabel"/>, by Mermaid's key.</summary>
    private readonly Dictionary<string, string> _wrapCache = new(StringComparer.Ordinal);

    /// <summary>The memoized results of <see cref="BreakString"/>, by Mermaid's key.</summary>
    private readonly Dictionary<string, (IReadOnlyList<string> Lines, string Remaining)> _breakCache = new(StringComparer.Ordinal);

    /// <summary>The <c>lastIndex</c> of Mermaid's global <c>lineBreakRegex</c>.</summary>
    private int _lineBreakLastIndex;

    /// <summary>
    ///     Gets or sets the work meter that text measurement charges, or null for no metering.
    /// </summary>
    public LayoutWorkMeter? Meter { get; set; }

    /// <summary>
    ///     Parses a font size as Mermaid's <c>parseFontSize</c> does: a number is pixels; a string gives its
    ///     <c>parseInt</c> value, and is the CSS size itself, with <c>px</c> added when it is that integer alone.
    /// </summary>
    /// <param name="fontSize">The configured font size.</param>
    /// <returns>The number and the CSS size, both null when a string does not start with an integer.</returns>
    public static (double? Number, string? Css) ParseFontSize(ConfigScalar fontSize)
    {
        if (fontSize.IsNumber)
        {
            return (double.Parse(fontSize.Text, NumberStyles.Float, CultureInfo.InvariantCulture), fontSize.Text + "px");
        }

        if (ParseInt(fontSize.Text) is not { } number)
        {
            return (null, null);
        }

        return fontSize.Text == ColorMath.FormatNumber(number) ? (number, fontSize.Text + "px") : (number, fontSize.Text);
    }

    /// <summary>
    ///     Gets the font size in pixels that the browser draws a CSS font size at: a length with a unit (<c>em</c> and
    ///     <c>rem</c> of 16 pixels), zero, or the inherited 16 pixels for anything else (a missing size, a number without a
    ///     unit), which the browser does not accept.
    /// </summary>
    /// <param name="css">The CSS font size, or null.</param>
    /// <returns>The size in pixels.</returns>
    public static double FontPixels(string? css)
    {
        var text = css?.Trim();
        if (string.IsNullOrEmpty(text) || !StyleDeclarations.TryParseLength(text, InheritedFontSize, out var size))
        {
            return InheritedFontSize;
        }

        var unitless = char.IsDigit(text[^1]) || text[^1] == '.';
        return unitless && size != 0 ? InheritedFontSize : Math.Min(size, StyleDeclarations.MaxFontSize);
    }

    /// <summary>
    ///     Splits a text at its <c>&lt;br&gt;</c> tags (<c>&lt;br</c>, white space, an optional slash and <c>&gt;</c>,
    ///     in any letter case), as Mermaid's <c>common.splitBreaks</c> does.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The lines; an empty text is one empty line.</returns>
    public static IReadOnlyList<string> SplitBreaks(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = new List<string>();
        var start = 0;
        var i = 0;
        while (i < text.Length)
        {
            var length = text[i] == '<' ? MatchBreakTag(text, i) : 0;
            if (length > 0)
            {
                lines.Add(text[start..i]);
                i += length;
                start = i;
            }
            else
            {
                i++;
            }
        }

        lines.Add(text[start..]);
        return lines;
    }

    /// <summary>
    ///     Measures a text as Mermaid's <c>utils.calculateTextDimensions</c> does.
    /// </summary>
    /// <param name="text">The text, with <c>&lt;br&gt;</c> tags between its lines.</param>
    /// <param name="font">The font.</param>
    /// <returns>The dimensions; zero for an empty text.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="MermaidRenderException">
    ///     Thrown with <see cref="MermaidDiagnosticCodes.RenderFailed"/> when a line measures zero by zero (a font size of
    ///     zero), which fails Mermaid's render; or by <see cref="Meter"/> when the work limit is passed.
    /// </exception>
    public TextDimensions CalculateTextDimensions(string text, MermaidFontConfig font)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(font);
        Meter?.Charge(text.Length + 1L);
        if (text.Length == 0)
        {
            return default;
        }

        // The size, weight and families the browser measures with
        var size = FontPixels(ParseFontSize(font.Size).Css);
        var bold = StyleDeclarations.TryParseBold(font.Weight.Text, out var isBold) && isBold;
        var lines = SplitBreaks(text);
        var fallback = MeasureLines(lines, new SceneFont("sans-serif", size, bold, false));
        var own = MeasureLines(lines, new SceneFont(font.Family, size, bold, false));

        // The family's dimensions, unless sans-serif is larger in all three
        return fallback.Height > own.Height && fallback.Width > own.Width && fallback.LineHeight > own.LineHeight ? fallback : own;
    }

    /// <summary>
    ///     Measures the width of a text (Mermaid's <c>calculateTextWidth</c>).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="font">The font.</param>
    /// <returns>The width in whole pixels.</returns>
    public double CalculateTextWidth(string text, MermaidFontConfig font) => CalculateTextDimensions(text, font).Width;

    /// <summary>
    ///     Wraps a label to a width as Mermaid's <c>utils.wrapLabel</c> does: words (separated by single spaces) are added
    ///     to a line while it stays narrower than the width, a word wider than the width is hyphenated, and the lines are
    ///     joined with <c>&lt;br/&gt;</c>. A label that already has a <c>&lt;br&gt;</c> (as Mermaid's stateful test finds
    ///     it) is returned as it is.
    /// </summary>
    /// <param name="label">The label.</param>
    /// <param name="maxWidth">The width the lines must stay under.</param>
    /// <param name="font">The font.</param>
    /// <param name="joinWith">The text between lines, or null for Mermaid's default <c>&lt;br/&gt;</c>.</param>
    /// <returns>The wrapped label; an empty label as it is.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="label"/> or <paramref name="font"/> is null.</exception>
    public string WrapLabel(string label, double maxWidth, MermaidFontConfig font, string? joinWith = null)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(font);
        if (label.Length == 0)
        {
            return label;
        }

        // Memoized with Mermaid's key, which is built from the arguments as given (an absent joinWith is "undefined")
        var key = string.Concat(label, ColorMath.FormatNumber(maxWidth), font.Size.Text, font.Weight.Text, font.Family, joinWith ?? "undefined");
        if (_wrapCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var result = TestLineBreak(label) ? label : WrapWords(label, maxWidth, font, joinWith ?? "<br/>");
        _wrapCache[key] = result;
        return result;
    }

    /// <summary>
    ///     Hyphenates a word to a width as Mermaid's <c>breakString</c> does: characters are added to a line until it is
    ///     at least as wide as the width, which ends the line with a hyphen (except at the word's last character).
    /// </summary>
    /// <param name="word">The word.</param>
    /// <param name="maxWidth">The width.</param>
    /// <param name="font">The font.</param>
    /// <param name="hyphen">The hyphen added to a broken line.</param>
    /// <returns>The completed lines and the rest of the word, which starts the next line.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public (IReadOnlyList<string> HyphenatedStrings, string RemainingWord) BreakString(string word, double maxWidth, MermaidFontConfig font, string hyphen = "-")
    {
        ArgumentNullException.ThrowIfNull(word);
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(hyphen);
        var key = string.Concat(word, ColorMath.FormatNumber(maxWidth), hyphen, font.Size.Text, font.Weight.Text, font.Family);
        if (_breakCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        // The characters are code points, as JavaScript's spread of a string gives them (a lone surrogate alone)
        var characters = new List<string>();
        var position = 0;
        while (position < word.Length)
        {
            var pair = char.IsHighSurrogate(word[position]) && position + 1 < word.Length && char.IsLowSurrogate(word[position + 1]);
            var length = pair ? 2 : 1;
            characters.Add(word.Substring(position, length));
            position += length;
        }

        var lines = new List<string>();
        var currentLine = string.Empty;
        for (var index = 0; index < characters.Count; index++)
        {
            var nextLine = currentLine + characters[index];
            if (CalculateTextWidth(nextLine, font) >= maxWidth)
            {
                lines.Add(index + 1 == characters.Count ? nextLine : nextLine + hyphen);
                currentLine = string.Empty;
            }
            else
            {
                currentLine = nextLine;
            }
        }

        var result = ((IReadOnlyList<string>)lines, currentLine);
        _breakCache[key] = result;
        return result;
    }

    /// <summary>
    ///     Tests whether a text has a <c>&lt;br&gt;</c> tag as Mermaid's global <c>lineBreakRegex.test</c> does: the search
    ///     starts at the <c>lastIndex</c> the previous test left, which a match moves past the tag and a miss resets to 0.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>Whether a tag was found from the index.</returns>
    internal bool TestLineBreak(string text)
    {
        for (var i = _lineBreakLastIndex; i < text.Length; i++)
        {
            var length = text[i] == '<' ? MatchBreakTag(text, i) : 0;
            if (length > 0)
            {
                _lineBreakLastIndex = i + length;
                return true;
            }
        }

        _lineBreakLastIndex = 0;
        return false;
    }

    /// <summary>
    ///     JavaScript's <c>parseInt(text, 10)</c>: leading white space, an optional sign and the decimal digits that follow.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The number, or null for <c>NaN</c>.</returns>
    private static double? ParseInt(string text)
    {
        var i = 0;
        while (i < text.Length && ScanPatterns.IsJsWhitespace(text[i]))
        {
            i++;
        }

        var negative = i < text.Length && text[i] == '-';
        if (i < text.Length && text[i] is '-' or '+')
        {
            i++;
        }

        var start = i;
        while (i < text.Length && text[i] is >= '0' and <= '9')
        {
            i++;
        }

        if (i == start)
        {
            return null;
        }

        var value = double.Parse(text.AsSpan(start, i - start), NumberStyles.None, CultureInfo.InvariantCulture);
        return negative ? -value : value;
    }

    /// <summary>
    ///     Wraps the words of a label (the body of Mermaid's <c>wrapLabel</c> after its tests).
    /// </summary>
    /// <param name="label">The label.</param>
    /// <param name="maxWidth">The width.</param>
    /// <param name="font">The font.</param>
    /// <param name="joinWith">The text between lines.</param>
    /// <returns>The wrapped label.</returns>
    private string WrapWords(string label, double maxWidth, MermaidFontConfig font, string joinWith)
    {
        var words = label.Split(' ').Where(word => word.Length > 0).ToList();
        var completedLines = new List<string>();
        var nextLine = string.Empty;
        for (var index = 0; index < words.Count; index++)
        {
            var word = words[index];
            var wordLength = CalculateTextWidth(word + " ", font);
            var nextLineLength = CalculateTextWidth(nextLine, font);
            if (wordLength > maxWidth)
            {
                // A word wider than the line is hyphenated; its rest starts the next line
                var (hyphenatedStrings, remainingWord) = BreakString(word, maxWidth, font);
                completedLines.Add(nextLine);
                completedLines.AddRange(hyphenatedStrings);
                nextLine = remainingWord;
            }
            else if (nextLineLength + wordLength >= maxWidth)
            {
                completedLines.Add(nextLine);
                nextLine = word;
            }
            else
            {
                nextLine = nextLine.Length == 0 ? word : nextLine + " " + word;
            }

            if (index + 1 == words.Count)
            {
                completedLines.Add(nextLine);
            }
        }

        return string.Join(joinWith, completedLines.Where(line => line.Length > 0));
    }

    /// <summary>
    ///     Measures the lines of a text in one font as Mermaid's loop over its families does.
    /// </summary>
    /// <param name="lines">The lines.</param>
    /// <param name="font">The font.</param>
    /// <returns>The dimensions.</returns>
    /// <exception cref="MermaidRenderException">Thrown when a line measures zero by zero.</exception>
    private TextDimensions MeasureLines(IReadOnlyList<string> lines, SceneFont font)
    {
        double width = 0, height = 0, lineHeight = 0;
        foreach (var line in lines)
        {
            var (boxWidth, boxHeight) = MeasureLine(line.Length == 0 ? ZeroWidthSpace : line, font);
            if (boxWidth == 0 && boxHeight == 0)
            {
                throw new MermaidRenderException(
                    MermaidDiagnosticCodes.RenderFailed,
                    "a text measured zero by zero pixels (Mermaid fails with 'svg element not in render tree')",
                    default,
                    "give the text a font size greater than zero");
            }

            width = ColorMath.JsRound(Math.Max(width, boxWidth));
            var rounded = ColorMath.JsRound(boxHeight);
            height += rounded;
            lineHeight = ColorMath.JsRound(Math.Max(lineHeight, rounded));
        }

        return new TextDimensions(width, height, lineHeight);
    }

    /// <summary>
    ///     Measures one line as the browser's <c>getBBox</c> of an SVG text at x 0: the advance grown to the glyph outlines
    ///     rounded out to whole pixels, and the rounded ascent plus the rounded descent.
    /// </summary>
    /// <param name="line">The line (a zero-width space for an empty one).</param>
    /// <param name="font">The font.</param>
    /// <returns>The width and height of the box.</returns>
    private (double Width, double Height) MeasureLine(string line, SceneFont font)
    {
        var (ascent, descent) = (float)font.Size > 0 && float.IsFinite((float)font.Size) ? VerticalMetrics(font) : (0, 0);
        if (line == ZeroWidthSpace)
        {
            // The browser gives the zero-width space no advance and no ink
            return (0, ascent + descent);
        }

        var advance = Advance(line, font);
        var (left, right) = InkExtent([new LabelRun(line, font, default, advance)], advance);
        return (right - left, ascent + descent);
    }
}
