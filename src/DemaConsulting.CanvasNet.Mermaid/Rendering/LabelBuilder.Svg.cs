// Portions derived from mermaid (MIT); see third-party/README.md
using System.Globalization;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;
using DemaConsulting.CanvasNet.Rendering;

namespace DemaConsulting.CanvasNet.Mermaid.Rendering;

/// <content>
///     SVG labels (<c>htmlLabels: false</c>): Mermaid's <c>createFormattedText</c>, <c>splitLineToFitWidth</c> and
///     <c>splitWordToFitWidth</c>, and the box the browser's <c>getBBox</c> gives the text.
/// </content>
internal sealed partial class LabelBuilder
{
    /// <summary>
    ///     Lays out an SVG label.
    /// </summary>
    /// <param name="text">The prepared text.</param>
    /// <param name="request">The request.</param>
    /// <returns>The measured label.</returns>
    private LabelLayout BuildSvg(string text, LabelRequest request)
    {
        // createText: <br> tags normalized and entity placeholders decoded, then markdown or plain lines
        var normalized = DecodeEntities(NormalizeBreakTags(text));
        var structured = request.Markdown
            ? MarkdownToLines(ReplaceFirst(normalized, "<br>", "<br/>"))
            : NonMarkdownToLines(normalized);

        // The wrapping is measured with the inherited font: the label's own style is applied after the lines are made
        var wrapFont = new SceneFont(request.Font.Family, request.RootFontSize, Bold: false, Italic: false);
        var lines = new List<List<(string Content, WordType Type)>>();
        foreach (var line in structured)
        {
            var words = line.Where(word => word.Content.Length > 0).ToList();
            bool Fits(List<(string Content, WordType Type)> candidate) => LineAdvance(candidate, wrapFont) <= request.Width;
            if (Fits(words))
            {
                lines.Add(words);
            }
            else
            {
                lines.AddRange(SplitLineToFitWidth(words, Fits, wrapFont, request.Width));
            }
        }

        // The tspans: 1.1 em apart, the first baseline one em below the origin, words joined by a space
        var (placed, box) = PlaceSvgLines(lines, request, request.Font.Size);
        ReportMissingGlyphs(placed, request.Location);
        var bounds = box ?? new SceneRect(0, 0, 0, 0);
        if (request.AddBackground && text.Length > 0)
        {
            // createFormattedText's background rectangle: the text box grown by its padding. Mermaid measures the text
            // before the label is moved under the rules that set its own font size, so the rectangle is the box of the
            // text in the inherited size, and the label's box holds the rectangle and the text drawn in its own size
            var inherited = Math.Abs(request.RootFontSize - request.Font.Size) < 1e-9;
            var measured = inherited ? bounds : PlaceSvgLines(lines, request, request.RootFontSize).Box ?? new SceneRect(0, 0, 0, 0);
            var background = new SceneRect(
                measured.X - SvgBackgroundPadding,
                measured.Y - SvgBackgroundPadding,
                measured.Width + (2 * SvgBackgroundPadding),
                measured.Height + (2 * SvgBackgroundPadding));
            bounds = inherited || box is null ? background : background.Union(bounds);
        }
        return new LabelLayout(false, bounds, placed, []);
    }

    /// <summary>
    ///     Places the lines of an SVG label as tspans in a font size: 1.1 em apart, the first baseline one em below the
    ///     origin, words joined by a space.
    /// </summary>
    /// <param name="lines">The words of each line.</param>
    /// <param name="request">The request.</param>
    /// <param name="size">The font size.</param>
    /// <returns>The placed lines and the box of the drawn ones, or null when none is drawn.</returns>
    private (List<LabelLine> Placed, SceneRect? Box) PlaceSvgLines(List<List<(string Content, WordType Type)>> lines, LabelRequest request, double size)
    {
        var (ascent, descent) = VerticalMetrics(new SceneFont(request.Font.Family, size, false, false));
        var placed = new List<LabelLine>();
        SceneRect? box = null;
        for (var index = 0; index < lines.Count; index++)
        {
            var runs = new List<LabelRun>();
            var words = lines[index];
            for (var w = 0; w < words.Count; w++)
            {
                var bold = words[w].Type == WordType.Strong || (request.SvgInheritsWeight && request.Font.Bold);
                var font = new SceneFont(request.Font.Family, size, bold, words[w].Type == WordType.Em);
                var content = (w == 0 ? string.Empty : " ") + DecodeHtmlEntities(words[w].Content);
                runs.Add(new LabelRun(content, font, request.Font.Color, SvgRunAdvance(Advance(content, font))));
            }

            var width = runs.Sum(run => run.Width);
            var baseline = (1 + (SvgLineHeight * index)) * size;
            var x = request.CenterSvgText ? -width / 2 : 0;
            if (runs.Count == 0 || width <= 0)
            {
                placed.Add(new LabelLine(x, baseline, 0, new SceneRect(x, baseline - ascent, 0, ascent + descent), runs));
                continue;
            }

            var (left, right) = InkExtent(runs, width);
            var lineBox = new SceneRect(x + left, baseline - ascent, right - left, ascent + descent);
            placed.Add(new LabelLine(x, baseline, width, lineBox, runs));
            box = box is { } current ? current.Union(lineBox) : lineBox;
        }

        return (placed, box);
    }

    /// <summary>
    ///     Gets the horizontal extent of a line's box as the browser's <c>getBBox</c> gives it: the advance, grown to
    ///     the boxes of the glyphs at the ends of each run (<see cref="SvgLineExtent"/>, the rule of the sequence
    ///     texts), each at its own origin.
    /// </summary>
    /// <param name="runs">The runs.</param>
    /// <param name="width">The advance of the line.</param>
    /// <returns>The left and right ends relative to the start of the line.</returns>
    private (double Left, double Right) InkExtent(List<LabelRun> runs, double width)
    {
        var left = 0.0;
        var right = width;
        var pen = 0.0;
        foreach (var run in runs)
        {
            var (_, runLeft, runRight) = SvgLineExtent(run.Text, run.Font);
            left = Math.Min(left, pen + runLeft);
            right = Math.Max(right, pen + runRight);
            pen += run.Width;
        }

        return (left, right);
    }

    /// <summary>
    ///     Gets the advance of a line of words joined by spaces (<c>computeWidthOfText</c>).
    /// </summary>
    /// <param name="words">The words.</param>
    /// <param name="font">The font of normal words.</param>
    /// <returns>The advance.</returns>
    private double LineAdvance(List<(string Content, WordType Type)> words, SceneFont font)
    {
        var width = 0.0;
        for (var i = 0; i < words.Count; i++)
        {
            var wordFont = font with { Bold = words[i].Type == WordType.Strong, Italic = words[i].Type == WordType.Em };
            width += Advance((i == 0 ? string.Empty : " ") + DecodeHtmlEntities(words[i].Content), wordFont);
        }

        return width;
    }

    /// <summary>
    ///     Splits a line of words into lines that fit (Mermaid's <c>splitLineToFitWidth</c>): words are added while the
    ///     line fits; a word that does not fit on an empty line is split at characters.
    /// </summary>
    /// <param name="words">The words of the line.</param>
    /// <param name="fits">Whether a line fits.</param>
    /// <param name="font">The font of normal words.</param>
    /// <param name="width">The width a line may have.</param>
    /// <returns>The lines.</returns>
    private List<List<(string Content, WordType Type)>> SplitLineToFitWidth(
        List<(string Content, WordType Type)> words,
        Func<List<(string Content, WordType Type)>, bool> fits,
        SceneFont font,
        double width)
    {
        var lines = new List<List<(string, WordType)>>();
        var queue = new LinkedList<(string Content, WordType Type)>(words);
        var newLine = new List<(string Content, WordType Type)>();
        while (queue.Count > 0)
        {
            var next = queue.First!.Value;
            queue.RemoveFirst();
            var candidate = new List<(string Content, WordType Type)>(newLine) { next };
            if (fits(candidate))
            {
                newLine = candidate;
                continue;
            }

            if (newLine.Count > 0)
            {
                // The line is full: the word starts the next one
                lines.Add(newLine);
                queue.AddFirst(next);
            }
            else if (next.Content.Length > 0)
            {
                // The word alone does not fit: it is split at characters
                var (head, rest) = SplitWordToFitWidth(next, font, width);
                lines.Add([head]);
                if (rest.Content.Length > 0)
                {
                    queue.AddFirst(rest);
                }
            }

            newLine = [];
        }

        if (newLine.Count > 0)
        {
            lines.Add(newLine);
        }

        return lines;
    }

    /// <summary>
    ///     Splits a word into the longest leading part that fits and the rest (Mermaid's <c>splitWordToFitWidth</c>); the
    ///     first character is taken even when it does not fit.
    /// </summary>
    /// <param name="word">The word.</param>
    /// <param name="font">The font of normal words.</param>
    /// <param name="width">The width a line may have.</param>
    /// <returns>The part that fits and the rest.</returns>
    private ((string Content, WordType Type) Head, (string Content, WordType Type) Remainder) SplitWordToFitWidth(
        (string Content, WordType Type) word,
        SceneFont font,
        double width)
    {
        // The graphemes of the word; the fitting prefix is found by bisection (the advance grows with the prefix)
        var boundaries = new List<int>();
        var enumerator = StringInfo.GetTextElementEnumerator(word.Content);
        while (enumerator.MoveNext())
        {
            boundaries.Add(enumerator.ElementIndex + ((string)enumerator.Current).Length);
        }

        var wordFont = font with { Bold = word.Type == WordType.Strong, Italic = word.Type == WordType.Em };
        var low = 0;
        var high = boundaries.Count;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (Advance(DecodeHtmlEntities(word.Content[..boundaries[middle - 1]]), wordFont) <= width)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        var count = Math.Max(low, 1);
        var cut = boundaries[count - 1];
        return ((word.Content[..cut], word.Type), (word.Content[cut..], word.Type));
    }

    /// <summary>
    ///     Normalizes <c>&lt;br&gt;</c> tags written with spaces or a slash to <c>&lt;br/&gt;</c>
    ///     (<c>/&lt;br\s*\/?&gt;/g</c>, case-sensitive).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text.</returns>
    private static string NormalizeBreakTags(string text)
    {
        var builder = new System.Text.StringBuilder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            if (string.CompareOrdinal(text, i, "<br", 0, 3) == 0 && MatchBreakTag(text, i) is > 0 and var length)
            {
                builder.Append("<br/>");
                i += length;
                continue;
            }

            builder.Append(text[i]);
            i++;
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Replaces the first occurrence of a text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="old">The text to replace.</param>
    /// <param name="replacement">The replacement.</param>
    /// <returns>The text.</returns>
    private static string ReplaceFirst(string text, string old, string replacement)
    {
        var index = text.IndexOf(old, StringComparison.Ordinal);
        return index < 0 ? text : string.Concat(text.AsSpan(0, index), replacement, text.AsSpan(index + old.Length));
    }

    /// <summary>
    ///     Decodes the three references that Mermaid's sanitizer makes in SVG text (<c>decodeHTMLEntities</c>):
    ///     <c>&amp;amp;</c>, <c>&amp;lt;</c> and <c>&amp;gt;</c>; any other reference stays as written.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text.</returns>
    internal static string DecodeHtmlEntities(string text) =>
        text.Contains('&', StringComparison.Ordinal)
            ? text.Replace("&amp;", "\u0001", StringComparison.Ordinal)
                .Replace("&lt;", "<", StringComparison.Ordinal)
                .Replace("&gt;", ">", StringComparison.Ordinal)
                .Replace('\u0001', '&')
            : text;
}
