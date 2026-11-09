// Portions derived from mermaid (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Rendering;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Sequence;

/// <summary>
///     What Mermaid's <c>drawText</c> draws: a text object of <c>getTextObj</c> with the fields the sequence renderer
///     sets.
/// </summary>
internal sealed record SequenceTextData
{
    /// <summary>Gets the text, with <c>&lt;br&gt;</c> tags between its lines.</summary>
    public required string Text { get; init; }

    /// <summary>Gets the x.</summary>
    public double X { get; init; }

    /// <summary>Gets the y.</summary>
    public double Y { get; init; }

    /// <summary>Gets the width of the area the text is aligned in.</summary>
    public double Width { get; init; }

    /// <summary>Gets the alignment (<c>left</c>, <c>center</c>, <c>right</c>, <c>start</c>, <c>middle</c> or <c>end</c>), or null.</summary>
    public string? Anchor { get; init; }

    /// <summary>Gets the vertical alignment (<c>center</c> or <c>middle</c>, <c>top</c> or <c>start</c>, <c>bottom</c> or <c>end</c>), or null.</summary>
    public string? Valign { get; init; }

    /// <summary>Gets the margin around the text.</summary>
    public double TextMargin { get; init; }

    /// <summary>Gets a value indicating whether each line is moved down one em (<c>dy="1em"</c>).</summary>
    public bool DyEm { get; init; }

    /// <summary>Gets the font of the inline style.</summary>
    public required MermaidFontConfig Font { get; init; }

    /// <summary>Gets the font weight a style rule draws the text with instead of the inline one, or null.</summary>
    public string? RuleWeight { get; init; }

    /// <summary>Gets the color.</summary>
    public Rgba32? Color { get; init; }
}

/// <content>
///     Text: Mermaid's <c>drawText</c> (lines of SVG text aligned in an area, centered vertically on the measured line
///     heights) and the <c>byTspan</c> placement of participant labels, with the boxes the browser's <c>getBBox</c>
///     gives the lines.
/// </content>
internal sealed partial class SequenceRenderer
{
    /// <summary>
    ///     Gets the text the browser shows for a text of the model: Mermaid's entity placeholders become character
    ///     references when the SVG is written, the references are shown as their characters, and the white space is
    ///     collapsed as in all SVG text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text as shown.</returns>
    internal static string DisplayText(string text) =>
        LabelBuilder.CollapseSvgWhiteSpace(HtmlSanitizer.DecodeReferences(LabelBuilder.DecodeEntities(text)));

    /// <summary>
    ///     Draws a text as Mermaid's <c>drawText</c> does: one SVG text per line, aligned by the anchor in the area with
    ///     the margin, and, with a vertical alignment, each line placed by the heights of the lines before it.
    /// </summary>
    /// <param name="data">The text.</param>
    /// <param name="role">The role of the lines.</param>
    /// <param name="id">The id of the lines, or null.</param>
    /// <param name="location">The location of the construct.</param>
    /// <returns>The lines, and the sum of their heights (zero without a vertical alignment, as in Mermaid).</returns>
    private (List<SceneItem> Lines, double Height) DrawText(SequenceTextData data, string role, string? id, MermaidLocation location)
    {
        var lines = LabelBuilder.SplitBreaks(data.Text);
        var (fontSize, fontSizeCss) = LabelBuilder.ParseFontSize(data.Font.Size);
        var size = LabelBuilder.FontPixels(fontSizeCss);
        var weight = data.RuleWeight ?? data.Font.Weight.Text;
        var font = new SceneFont(data.Font.Family, size, StyleDeclarations.TryParseBold(weight, out var bold) && bold, false);
        var margin = data.TextMargin;
        var measured = data.Valign is not null && margin > 0;

        // The anchor moves the x into the area and centers the lines vertically on their middle baseline
        var x = data.X;
        var anchor = SceneTextAnchor.Start;
        var middle = false;
        switch (data.Anchor)
        {
            case "left" or "start":
                x = ColorMath.JsRound(x + margin);
                middle = true;
                break;
            case "middle" or "center":
                x = ColorMath.JsRound(x + (data.Width / 2));
                anchor = SceneTextAnchor.Middle;
                middle = true;
                break;
            case "right" or "end":
                x = ColorMath.JsRound(x + data.Width - margin);
                anchor = SceneTextAnchor.End;
                middle = true;
                break;
        }

        var items = new List<SceneItem>(lines.Count);
        var textHeight = 0.0;
        for (var i = 0; i < lines.Count; i++)
        {
            // The y of the line: centered on the lines before it and the margin
            var y = data.Valign switch
            {
                _ when !measured => data.Y,
                "top" or "start" => ColorMath.JsRound(data.Y + margin),
                "middle" or "center" => ColorMath.JsRound(data.Y + ((textHeight + textHeight + margin) / 2)),
                "bottom" or "end" => ColorMath.JsRound(data.Y + (textHeight + textHeight + (2 * margin)) - margin),
                _ => data.Y,
            };

            var dy = 0.0;
            if (data.DyEm)
            {
                dy = size;
            }
            else if (margin == 0 && fontSize is { } number)
            {
                dy = i * number;
            }
            var baseline = y + dy + (middle ? MiddleBaselineOffset(font) : 0);
            var line = TextLine(DisplayText(lines[i]), x, baseline, anchor, font, data.Color ?? RootFill, role, id, location);
            if (measured)
            {
                textHeight += line.Box.Height;
            }

            if (line.Runs.Count > 0)
            {
                items.Add(line);
            }
        }

        return (items, textHeight);
    }

    /// <summary>
    ///     Draws a participant label as Mermaid's <c>byTspan</c> does: one SVG text per line, centered in the box on the
    ///     central baseline, the lines one font size apart around the middle of the box.
    /// </summary>
    /// <param name="content">The label, with <c>&lt;br&gt;</c> tags between its lines.</param>
    /// <param name="x">The left of the box.</param>
    /// <param name="y">The top of the box.</param>
    /// <param name="width">The width of the box.</param>
    /// <param name="height">The height of the box.</param>
    /// <param name="location">The location of the participant.</param>
    /// <returns>The lines.</returns>
    private List<SceneItem> ByTspan(string content, double x, double y, double width, double height, MermaidLocation location)
    {
        var actorFont = _conf.ActorFont;
        var (fontSize, fontSizeCss) = LabelBuilder.ParseFontSize(actorFont.Size);
        var size = fontSize ?? double.NaN;
        var font = new SceneFont(
            actorFont.Family,
            LabelBuilder.FontPixels(fontSizeCss),
            StyleDeclarations.TryParseBold(actorFont.Weight.Text, out var bold) && bold,
            false);
        var color = _styles.TextColor(null, "text.actor > tspan") ?? Black;
        var (ascent, descent) = _labels.VerticalMetrics(font);
        var lines = LabelBuilder.SplitBreaks(content);
        var items = new List<SceneItem>(lines.Count);
        for (var i = 0; i < lines.Count; i++)
        {
            // A dy that is not a number is ignored by the browser
            var dy = (i * size) - (size * (lines.Count - 1) / 2);
            var baseline = y + (height / 2) + (double.IsNaN(dy) ? 0 : dy) + ((ascent - descent) / 2);
            var line = TextLine(DisplayText(lines[i]), x + (width / 2), baseline, SceneTextAnchor.Middle, font, color, "actor-text", null, location);
            if (line.Runs.Count > 0)
            {
                items.Add(line);
            }
        }

        return items;
    }

    /// <summary>
    ///     Gets how far below the y of a text its alphabetic baseline is with the <c>middle</c> dominant baseline: half
    ///     the font's x-height, in the browser's layout units of 1/64 pixel.
    /// </summary>
    /// <param name="font">The font.</param>
    /// <returns>The offset.</returns>
    private double MiddleBaselineOffset(SceneFont font) =>
        Math.Round(_labels.XHeight(font) / 2 * 64, MidpointRounding.ToEven) / 64;

    /// <summary>
    ///     Makes one line of text with the box the browser's <c>getBBox</c> gives it: the advance grown to the glyph
    ///     outlines, from the rounded ascent above the baseline to the rounded descent below it.
    /// </summary>
    /// <param name="text">The text as shown.</param>
    /// <param name="x">The x of the anchor.</param>
    /// <param name="baseline">The y of the baseline.</param>
    /// <param name="anchor">The anchor.</param>
    /// <param name="font">The font.</param>
    /// <param name="color">The color.</param>
    /// <param name="role">The role.</param>
    /// <param name="id">The id, or null.</param>
    /// <param name="location">The location of the construct.</param>
    /// <returns>The line; it has no runs when the text is empty.</returns>
    private SceneText TextLine(string text, double x, double baseline, SceneTextAnchor anchor, SceneFont font, Rgba32 color, string role, string? id, MermaidLocation location)
    {
        var (ascent, descent) = _labels.VerticalMetrics(font);
        var (advance, left, right) = text.Length > 0 ? _labels.SvgLineExtent(text, font) : (0, 0, 0);
        var start = anchor switch
        {
            SceneTextAnchor.Middle => x - (advance / 2),
            SceneTextAnchor.End => x - advance,
            _ => x,
        };

        IReadOnlyList<SceneTextRun> runs = text.Length > 0 ? [new SceneTextRun(text, font, color)] : [];
        return new SceneText(x, baseline, anchor, runs, new SceneRect(start + left, baseline - ascent, right - left, ascent + descent))
        {
            Role = role,
            Id = id,
            Location = location,
        };
    }
}
