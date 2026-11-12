// Portions derived from mermaid (MIT); see third-party/README.md
using System.Globalization;
using System.Text;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Dagre;
using DemaConsulting.CanvasNet.Mermaid.Rendering;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Packet;

/// <summary>
///     The packet renderer: a port of Mermaid's packet <c>renderer.ts</c> (<c>draw</c> and <c>drawWord</c>) that turns a
///     <see cref="PacketDiagram"/> into a scene, with the paint of Mermaid's packet style sheet (<see cref="PacketStyles"/>).
/// </summary>
/// <remarks>
///     <para>
///         <b>Layout.</b> As in Mermaid, the blocks are split into rows of <c>bitsPerRow</c> bits
///         (<see cref="PacketDiagram.Rows"/>). Row <c>r</c> (from 0) is at <c>r * (rowHeight + paddingY) + paddingY</c>,
///         where <c>paddingY</c> is grown by 10 when the bit numbers are shown. A block is a rectangle at
///         <c>(start % bitsPerRow) * bitWidth + 1</c>, <c>(end - start + 1) * bitWidth - paddingX</c> wide and
///         <c>rowHeight</c> high, with its label centered in it; with <c>showBits</c>, its first bit number is drawn 2
///         pixels above its left edge (centered for a single bit) and its last bit number above its right edge. The title
///         is centered in an extra row at the bottom. The view box is <c>bitWidth * bitsPerRow + 2</c> wide and as high as
///         the rows and the title row (less <c>rowHeight</c> without a title).
///     </para>
///     <para>
///         <b>Texts.</b> A text is drawn as it is shown (Mermaid's entity placeholders decoded, white space collapsed), with
///         the box of the browser's <c>getBBox</c> (<see cref="LabelBuilder"/>): a <c>middle</c> dominant baseline is half
///         the font's x-height below the text's <c>y</c> (rounded to 1/64 pixel, as the browser lays it out), an
///         <c>auto</c> baseline is at the <c>y</c>. An empty text draws nothing and is left out. Bit numbers are written as
///         JavaScript writes numbers.
///     </para>
///     <para>
///         <b>Roles.</b> The items carry roles and ids for tests and the reference comparison: the <c>packet</c> group, a
///         <c>packet-row</c> group per row (its index as id), and per block part a <c>packet-block</c> rectangle (left out
///         when its width or height is not more than zero, as SVG draws no such rectangle) and the <c>packet-label</c>,
///         <c>packet-bit-start</c> and <c>packet-bit-end</c> texts (the label as id, the block statement as location), then
///         the <c>title</c>.
///     </para>
///     <para>
///         <b>Cost.</b> The work meter (<see cref="MermaidLimits.MaxLayoutWork"/>) is charged for each block and part of the
///         row split and for the length of each text, and checks cancellation.
///     </para>
///     <para>
///         An instance draws one render on one thread.
///     </para>
/// </remarks>
internal sealed class PacketRenderer
{
    /// <summary>The space between a block's top and the baseline of its bit numbers (<c>bitNumberY = wordY - 2</c>).</summary>
    private const double BitNumberGap = 2;

    /// <summary>The diagram.</summary>
    private readonly PacketDiagram _diagram;

    /// <summary>The context of the render.</summary>
    private readonly DiagramRenderContext _context;

    /// <summary>The packet configuration of the render.</summary>
    private readonly PacketConfig _conf;

    /// <summary>The style sheet of the render.</summary>
    private readonly PacketStyles _styles;

    /// <summary>The label builder of the render (text metrics).</summary>
    private readonly LabelBuilder _labels;

    /// <summary>The work meter of the render.</summary>
    private readonly LayoutWorkMeter _meter;

    /// <summary>
    ///     Initializes a renderer for one render.
    /// </summary>
    /// <param name="diagram">The diagram.</param>
    /// <param name="context">The context of the render.</param>
    public PacketRenderer(PacketDiagram diagram, DiagramRenderContext context)
    {
        _diagram = diagram;
        _context = context;
        _conf = context.Config.Packet;
        _styles = PacketStyles.Create(context.Config, context.Sink);
        _meter = new LayoutWorkMeter(context.Options.Limits.MaxLayoutWork, diagram.KeywordLocation, context.CancellationToken);
        _labels = new LabelBuilder(context.Fonts, context.Sink, null) { Meter = _meter };
    }

    /// <summary>
    ///     Gets the text the browser shows for a text that d3 sets as the content of an SVG text: Mermaid's entity
    ///     placeholders become character references when the SVG is written and are shown as their characters (anything
    ///     else is shown as written), and the white space is collapsed as in all SVG text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text as shown.</returns>
    internal static string DisplayText(string text)
    {
        const string Prefix = "\ufb02\u00b0";
        const string Suffix = "\u00b6\u00df";
        var builder = new StringBuilder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            var start = text.IndexOf(Prefix, i, StringComparison.Ordinal);
            var end = start < 0 ? -1 : text.IndexOf(Suffix, start + Prefix.Length, StringComparison.Ordinal);
            if (end < 0)
            {
                builder.Append(text, i, text.Length - i);
                break;
            }

            // The placeholder is decoded on its own (decodeEntities), then read as markup
            builder.Append(text, i, start - i);
            var reference = LabelBuilder.DecodeEntities(text[start..(end + Suffix.Length)]);
            builder.Append(HtmlSanitizer.DecodeReferences(reference));
            i = end + Suffix.Length;
        }

        return LabelBuilder.CollapseSvgWhiteSpace(builder.ToString());
    }

    /// <summary>
    ///     Draws the diagram.
    /// </summary>
    /// <returns>The scene, whose view box is the natural size.</returns>
    /// <exception cref="MermaidRenderException">
    ///     Thrown, located, when the row split fails as Mermaid's does, or when a limit is exceeded.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when the render is canceled.</exception>
    public Scene Draw()
    {
        // The rows (the database's populate, which reads the configuration of the render)
        var rows = _diagram.Rows(_conf.BitsPerRow, _context.Options.Limits, _meter);
        var rowHeight = _conf.RowHeight;
        var paddingY = _conf.EffectivePaddingY;
        var bitWidth = _conf.BitWidth;
        var bitsPerRow = _conf.BitsPerRow;
        var title = _diagram.DrawnTitle;

        // draw: the size of the picture
        var totalRowHeight = rowHeight + paddingY;
        var svgHeight = (totalRowHeight * (rows.Count + 1)) - (title.Length > 0 ? 0 : rowHeight);
        var svgWidth = (bitWidth * bitsPerRow) + 2;

        // drawWord for each row, then the title
        var items = new List<SceneItem>(rows.Count + 1);
        for (var r = 0; r < rows.Count; r++)
        {
            items.Add(new SceneGroup(0, 0, DrawWord(rows[r], r)) { Role = "packet-row", Id = r.ToString(CultureInfo.InvariantCulture), Location = rows[r][0].Location });
        }

        var titlePaint = _styles.Title;
        if (TextLine(title, svgWidth / 2, svgHeight - (totalRowHeight / 2), SceneTextAnchor.Middle, true, titlePaint, "title", null, _diagram.KeywordLocation) is { } titleText)
        {
            items.Add(titleText);
        }

        var root = new SceneGroup(0, 0, items) { Role = "packet", Location = _diagram.KeywordLocation };
        return new Scene(new SceneRect(0, 0, svgWidth, svgHeight), [root]);
    }

    /// <summary>
    ///     Draws a row (Mermaid's <c>drawWord</c>): each part's rectangle and label, and with <c>showBits</c> its bit
    ///     numbers.
    /// </summary>
    /// <param name="word">The parts of the row.</param>
    /// <param name="rowNumber">The index of the row.</param>
    /// <returns>The items of the row.</returns>
    private List<SceneItem> DrawWord(IReadOnlyList<PacketBlock> word, int rowNumber)
    {
        var rowHeight = _conf.RowHeight;
        var paddingY = _conf.EffectivePaddingY;
        var bitWidth = _conf.BitWidth;
        var wordY = (rowNumber * (rowHeight + paddingY)) + paddingY;
        var block = _styles.Block;
        var label = _styles.Label;
        var byteStart = _styles.ByteStart;
        var byteEnd = _styles.ByteEnd;
        var items = new List<SceneItem>();
        foreach (var part in word)
        {
            // The rectangle (JavaScript's % keeps the sign of the dividend, as C#'s does)
            var blockX = (part.Start % _conf.BitsPerRow * bitWidth) + 1;
            var width = ((part.End - part.Start + 1) * bitWidth) - _conf.PaddingX;
            if (width > 0 && rowHeight > 0 && double.IsFinite(blockX) && double.IsFinite(width) && double.IsFinite(wordY) && double.IsFinite(rowHeight))
            {
                items.Add(new ScenePath(Rectangle(blockX, wordY, width, rowHeight))
                {
                    Role = "packet-block",
                    Id = part.Label,
                    Location = part.Location,
                    Fill = block.Fill,
                    Stroke = block.Stroke is { } stroke && block.StrokeWidth > 0 ? new SceneStroke(stroke, block.StrokeWidth) : null,
                });
            }

            // The label, centered in the block
            AddText(items, TextLine(part.Label, blockX + (width / 2), wordY + (rowHeight / 2), SceneTextAnchor.Middle, true, label, "packet-label", part.Label, part.Location));
            if (!_conf.ShowBits)
            {
                continue;
            }

            // The bit numbers above the block: one centered for a single bit, else the first and the last
            var single = PacketDiagram.JsEquals(part.End, part.Start);
            var bitNumberY = wordY - BitNumberGap;
            AddText(items, TextLine(
                YamlSubsetParser.FormatJsNumber(part.Start),
                blockX + (single ? width / 2 : 0),
                bitNumberY,
                single ? SceneTextAnchor.Middle : SceneTextAnchor.Start,
                false,
                byteStart,
                "packet-bit-start",
                part.Label,
                part.Location));
            if (!single)
            {
                AddText(items, TextLine(YamlSubsetParser.FormatJsNumber(part.End), blockX + width, bitNumberY, SceneTextAnchor.End, false, byteEnd, "packet-bit-end", part.Label, part.Location));
            }
        }

        return items;
    }

    /// <summary>
    ///     Adds a text to a list when it draws something.
    /// </summary>
    /// <param name="items">The list.</param>
    /// <param name="text">The text, or null.</param>
    private static void AddText(List<SceneItem> items, SceneText? text)
    {
        if (text is not null)
        {
            items.Add(text);
        }
    }

    /// <summary>
    ///     Gets the outline of a rectangle.
    /// </summary>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The segments.</returns>
    private static SceneSegment[] Rectangle(double x, double y, double width, double height) =>
    [
        SceneSegment.Move(x, y),
        SceneSegment.Line(x + width, y),
        SceneSegment.Line(x + width, y + height),
        SceneSegment.Line(x, y + height),
        SceneSegment.Close(),
    ];

    /// <summary>
    ///     Makes one SVG text with the box the browser's <c>getBBox</c> gives it, or nothing for a text that shows nothing
    ///     or whose position is not a number.
    /// </summary>
    /// <param name="content">The text content (with Mermaid's entity placeholders).</param>
    /// <param name="x">The x of the anchor.</param>
    /// <param name="y">The y attribute.</param>
    /// <param name="anchor">The text anchor.</param>
    /// <param name="middle">Whether the dominant baseline is <c>middle</c> (else <c>auto</c>, the alphabetic baseline).</param>
    /// <param name="paint">The font and color.</param>
    /// <param name="role">The role.</param>
    /// <param name="id">The id, or null.</param>
    /// <param name="location">The location of the construct.</param>
    /// <returns>The text, or null.</returns>
    private SceneText? TextLine(string content, double x, double y, SceneTextAnchor anchor, bool middle, TextPaint paint, string role, string? id, MermaidLocation location)
    {
        var text = DisplayText(content);
        _meter.Charge(1 + text.Length);
        if (text.Length == 0 || !double.IsFinite(x) || !double.IsFinite(y))
        {
            return null;
        }

        // The baseline: half the x-height below the y for a middle baseline, in the browser's layout units of 1/64 pixel
        var font = new SceneFont(paint.FontFamily, paint.FontSize, false, false);
        var baseline = middle ? y + (Math.Round(_labels.XHeight(font) / 2 * 64, MidpointRounding.ToEven) / 64) : y;

        // The box from the rounded ascent to the rounded descent, the advance grown to the glyph outlines
        var (ascent, descent) = _labels.VerticalMetrics(font);
        var (advance, left, right) = _labels.SvgLineExtent(text, font);
        var start = anchor switch
        {
            SceneTextAnchor.Middle => x - (advance / 2),
            SceneTextAnchor.End => x - advance,
            _ => x,
        };
        return new SceneText(x, baseline, anchor, [new SceneTextRun(text, font, paint.Color)], new SceneRect(start + left, baseline - ascent, right - left, ascent + descent))
        {
            Role = role,
            Id = id,
            Location = location,
        };
    }
}
