// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.PlantUml.Styling;

namespace DemaConsulting.CanvasNet.PlantUml.Scenegraph;

/// <summary>
///     One page text of a diagram (a title, header, footer, caption, or legend): its display text, its alignment, and
///     its location.
/// </summary>
/// <param name="Text">The display text (creole markup and <c>\n</c> allowed).</param>
/// <param name="Align">The horizontal alignment on the page.</param>
/// <param name="Location">The location of the statement.</param>
internal sealed record PageText(string Text, TextAlign Align, PlantUmlLocation Location);

/// <summary>
///     The page texts of a diagram; each one is null when the diagram has none.
/// </summary>
internal sealed record PageTexts
{
    /// <summary>
    ///     Gets an instance without any page text.
    /// </summary>
    public static PageTexts None { get; } = new();

    /// <summary>Gets the title, drawn bold above the diagram.</summary>
    public PageText? Title { get; init; }

    /// <summary>Gets the header, drawn small at the top (right aligned by default).</summary>
    public PageText? Header { get; init; }

    /// <summary>Gets the footer, drawn small at the bottom (centered by default).</summary>
    public PageText? Footer { get; init; }

    /// <summary>Gets the caption, drawn below the diagram and its legend.</summary>
    public PageText? Caption { get; init; }

    /// <summary>Gets the legend, drawn in a rounded box below the diagram (centered by default).</summary>
    public PageText? Legend { get; init; }
}

/// <summary>
///     The spacing of the page texts that differs between diagram types: PlantUML frames the body of an activity
///     diagram 5 pixels further from its header, title, footer and side margins, and counts its body 5 pixels shorter,
///     because the body of an activity diagram already ends with 5 pixels of its own margin.
/// </summary>
/// <param name="Inset">The space added above the header and the title, beside the header and footer, and below the page.</param>
/// <param name="BodyTrim">The space at the bottom of the body that the texts below it may overlap.</param>
/// <param name="LegendWidth">The space the page keeps beside the text of the legend.</param>
internal sealed record PageMetrics(double Inset, double BodyTrim, double LegendWidth)
{
    /// <summary>
    ///     Gets the spacing of a sequence diagram.
    /// </summary>
    public static PageMetrics Sequence { get; } = new(0, 0, 46);

    /// <summary>
    ///     Gets the spacing of an activity diagram.
    /// </summary>
    public static PageMetrics Activity { get; } = new(5, 5, 55);

    /// <summary>
    ///     Gets the spacing of a diagram that PlantUML lays out with Graphviz (class, object, and state diagrams): the header is
    ///     at the very top, the title 10 below the header, the centered texts are centered 6 left of the middle of the
    ///     page (the body ends with a wider margin on the right), and the legend follows the body 6 below it (measured
    ///     from the pictures the jar draws).
    /// </summary>
    public static PageMetrics Svek { get; } = new(0, 1, 40)
    {
        HeaderTop = 0,
        TitleTop = 10,
        CenterTrim = 6,
        TitleExtra = 26,
        SideExtra = 6,
        CaptionExtra = 8,
        LegendLeft = 12,
    };

    /// <summary>
    ///     Gets the space above the header (before <see cref="Inset"/>); 5 by default.
    /// </summary>
    public double HeaderTop { get; init; } = 5;

    /// <summary>
    ///     Gets the space between the header (or the top) and the title (before <see cref="Inset"/>); 15 by default.
    /// </summary>
    public double TitleTop { get; init; } = 15;

    /// <summary>
    ///     Gets the width that the centered texts are centered without: they are centered in the page width minus it;
    ///     1 by default.
    /// </summary>
    public double CenterTrim { get; init; } = 1;

    /// <summary>
    ///     Gets the space the page keeps beside the title; 31 by default.
    /// </summary>
    public double TitleExtra { get; init; } = 31;

    /// <summary>
    ///     Gets the space the page keeps beside the header and the footer; 12 by default.
    /// </summary>
    public double SideExtra { get; init; } = 12;

    /// <summary>
    ///     Gets the space the page keeps beside the caption; 12 by default.
    /// </summary>
    public double CaptionExtra { get; init; } = 12;

    /// <summary>
    ///     Gets the left edge of a legend aligned left; 17 by default.
    /// </summary>
    public double LegendLeft { get; init; } = 17;
}

/// <summary>
///     Places the page texts of a diagram around its body: the header and the title above it, the legend, the caption
///     and the footer below it, as PlantUML's annotated diagram does.
/// </summary>
/// <remarks>
///     <para>
///         The body is the diagram scene without page texts, whose view box starts at the origin. The page is as wide as
///         the widest of the body and the page texts; the body and the centered texts are centered in it. The spacing
///         follows PlantUML: the header is 10 pixels of the document font in gray, right aligned 6 pixels from the
///         edge; the title is bold with a margin of 10 and a padding of 5; the legend is a box with rounded corners 7
///         pixels below the body; the caption follows the legend; the footer is last.
///     </para>
///     <para>
///         The class is stateless and safe for concurrent use.
///     </para>
/// </remarks>
internal static class ScenePage
{
    /// <summary>
    ///     Places the page texts around a body.
    /// </summary>
    /// <param name="body">The body scene (view box at the origin).</param>
    /// <param name="page">The page texts.</param>
    /// <param name="theme">The theme that styles the texts.</param>
    /// <param name="text">The text layout of the call.</param>
    /// <param name="sink">The report sink of the call.</param>
    /// <param name="metrics">The spacing of the diagram type; <see cref="PageMetrics.Sequence"/> when null.</param>
    /// <returns>The page scene; the body itself when there is no page text.</returns>
    public static Scene Decorate(Scene body, PageTexts page, SkinTheme theme, TextLayout text, PlantUmlReportSink? sink, PageMetrics? metrics = null)
    {
        var spacing = metrics ?? PageMetrics.Sequence;
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(text);
        if (page.Title is null && page.Header is null && page.Footer is null && page.Caption is null && page.Legend is null)
        {
            return body;
        }

        var header = Layout(page.Header, theme[SkinElement.Header], text, sink);
        var title = Layout(page.Title, theme[SkinElement.Title], text, sink);
        var legend = Layout(page.Legend, theme[SkinElement.Legend], text, sink);
        var caption = Layout(page.Caption, theme[SkinElement.Caption], text, sink);
        var footer = Layout(page.Footer, theme[SkinElement.Footer], text, sink);

        // The page is as wide as its widest part
        var width = body.ViewBox.Width;
        width = Math.Max(width, (title?.Width ?? 0) + spacing.TitleExtra);
        width = Math.Max(width, (header?.Width ?? 0) + spacing.SideExtra);
        width = Math.Max(width, (footer?.Width ?? 0) + spacing.SideExtra);
        width = Math.Max(width, (caption?.Width ?? 0) + spacing.CaptionExtra);
        width = Math.Max(width, (legend?.Width ?? 0) + spacing.LegendWidth);
        var trim = spacing.CenterTrim;

        var items = new List<SceneItem>();
        double y = 0;
        if (header is not null)
        {
            items.AddRange(header.Draw(AlignedX(page.Header!.Align, width, header.Width, spacing.Inset, trim), spacing.HeaderTop + spacing.Inset, TextAlign.Left, "header", page.Header.Location));
            y += header.Height + 1;
        }

        if (title is not null)
        {
            items.AddRange(title.Draw((width - trim - title.Width) / 2, y + spacing.TitleTop + spacing.Inset, TextAlign.Center, "title", page.Title!.Location));
            y += title.Height + 21;
        }

        // The body, centered
        var dx = (width - body.ViewBox.Width) / 2;
        items.AddRange(body.Items.Select(item => Translate(item, dx, y)));
        y += body.ViewBox.Height - spacing.BodyTrim;

        if (legend is not null)
        {
            var style = theme[SkinElement.Legend];
            var (boxWidth, boxHeight) = (legend.Width + (2 * style.Padding), legend.Height + (2 * style.Padding));
            var boxX = page.Legend!.Align switch
            {
                TextAlign.Left => spacing.LegendLeft,
                TextAlign.Right => width - 18 - boxWidth,
                _ => (width - trim - boxWidth) / 2,
            };
            items.Add(new ScenePath(SceneShapes.Rect(boxX, y + 7, boxWidth, boxHeight, style.RoundCorner / 2))
            {
                Role = "legend",
                Fill = style.Fill,
                Stroke = style.Line is { } line ? new SceneStroke(line, style.LineWidth) : null,
                Location = page.Legend.Location,
            });
            items.AddRange(legend.Draw(boxX + style.Padding, y + 7 + style.Padding, TextAlign.Left, "legend", page.Legend.Location));
            y += boxHeight + 25;
        }

        if (caption is not null)
        {
            items.AddRange(caption.Draw((width - trim - caption.Width) / 2, y - 4, TextAlign.Center, "caption", page.Caption!.Location));
            y += caption.Height + 3;
        }

        if (footer is not null)
        {
            items.AddRange(footer.Draw(AlignedX(page.Footer!.Align, width, footer.Width, spacing.Inset, trim), y - 5, TextAlign.Left, "footer", page.Footer.Location));
            y += footer.Height + 1;
        }

        return body with { ViewBox = new SceneRect(0, 0, width, y + spacing.Inset), Items = items };
    }

    /// <summary>
    ///     Translates a scene item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="dx">The horizontal offset.</param>
    /// <param name="dy">The vertical offset.</param>
    /// <returns>The moved item.</returns>
    public static SceneItem Translate(SceneItem item, double dx, double dy)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (dx == 0 && dy == 0)
        {
            return item;
        }

        return item switch
        {
            ScenePath path => path with
            {
                Segments = path.Segments.Select(s => s with { X = s.X + dx, Y = s.Y + dy, X1 = s.X1 + dx, Y1 = s.Y1 + dy, X2 = s.X2 + dx, Y2 = s.Y2 + dy }).ToList(),
            },
            SceneText line => line with
            {
                X = line.X + dx,
                Baseline = line.Baseline + dy,
                Box = line.Box with { X = line.Box.X + dx, Y = line.Box.Y + dy },
            },
            _ => item,
        };
    }

    /// <summary>
    ///     Lays out one page text in its style, or returns null when there is none.
    /// </summary>
    /// <param name="page">The page text, or null.</param>
    /// <param name="style">The style.</param>
    /// <param name="text">The text layout.</param>
    /// <param name="sink">The report sink.</param>
    /// <returns>The block, or null.</returns>
    private static TextBlock? Layout(PageText? page, ElementStyle style, TextLayout text, PlantUmlReportSink? sink)
    {
        if (page is null)
        {
            return null;
        }

        var font = new SceneFont(style.FontFamily, style.FontSize, style.Bold, style.Italic);
        return text.Layout(page.Text, font, style.FontColor, 0, sink, page.Location);
    }

    /// <summary>
    ///     Gets the x of a small page text (header or footer) by its alignment.
    /// </summary>
    /// <param name="align">The alignment.</param>
    /// <param name="width">The page width.</param>
    /// <param name="textWidth">The text width.</param>
    /// <param name="inset">The extra space from the side.</param>
    /// <param name="trim">The width the centered texts are centered without.</param>
    /// <returns>The x.</returns>
    private static double AlignedX(TextAlign align, double width, double textWidth, double inset, double trim)
    {
        return align switch
        {
            TextAlign.Left => 6 + inset,
            TextAlign.Right => width - 6 - inset - textWidth,
            _ => (width - trim - textWidth) / 2,
        };
    }
}
