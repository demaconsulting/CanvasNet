// Portions derived from mermaid (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Rendering;

/// <content>
///     The shared end of the unified diagrams' <c>draw</c> functions (the flowchart's <c>flowRenderer-v3-unified.ts</c>,
///     the class diagram's <c>classRenderer-v3-unified.ts</c>): the layout data through the cluster layout as the root
///     group, the diagram title (<c>insertTitle</c>) and the view box grown by the padding (<c>setupViewPortForSVG</c>).
/// </content>
internal sealed partial class ClusterLayout
{
    /// <summary>
    ///     Lays out and draws layout data as a whole diagram: the drawing as the root group, the title above it, and the
    ///     view box around both.
    /// </summary>
    /// <param name="data">The layout data.</param>
    /// <param name="title">The diagram title as written (the frontmatter <c>title</c>), or null or empty for none; it is sanitized here.</param>
    /// <param name="titleTopMargin">The distance of the title's baseline above the origin (<c>titleTopMargin</c>).</param>
    /// <param name="padding">The padding of the view box (the flowchart's <c>diagramPadding</c>, 8 for a class diagram).</param>
    /// <param name="labels">The label builder of the render (for the title's text metrics).</param>
    /// <returns>The scene, with the pictures of the render.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> or <paramref name="labels"/> is null.</exception>
    /// <exception cref="MermaidRenderException">Thrown, located, when the work limit is reached or the layout fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the render is canceled.</exception>
    public Scene RenderDiagram(LayoutData data, string? title, double titleTopMargin, double padding, LabelBuilder labels)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(labels);
        var drawing = Render(data);

        // The root group, then the title above it
        var items = new List<SceneItem>(drawing.Items.Count + 1);
        items.Add(new SceneGroup(0, 0, drawing.Items) { Role = "root" });
        var bounds = drawing.Bounds;
        if (!string.IsNullOrEmpty(title))
        {
            var text = DrawTitle(HtmlSanitizer.Sanitize(title), bounds, titleTopMargin, _context.Styles, labels);
            items.Add(text);
            bounds = bounds is { } b ? b.Union(text.Box) : text.Box;
        }

        // The view box: the bounds grown by the padding
        var box = bounds ?? new SceneRect(0, 0, 0, 0);
        var viewBox = new SceneRect(box.X - padding, box.Y - padding, box.Width + (2 * padding), box.Height + (2 * padding));
        return new Scene(viewBox, items) { Images = _context.Images };
    }

    /// <summary>
    ///     Draws the diagram title (Mermaid's <c>insertTitle</c>): centered over the bounds of the drawing, its baseline
    ///     the title margin above the origin.
    /// </summary>
    /// <param name="title">The sanitized title.</param>
    /// <param name="bounds">The bounds of the drawing, or null when nothing is drawn (Mermaid then takes an empty box at the origin).</param>
    /// <param name="topMargin">The title margin (<c>titleTopMargin</c>).</param>
    /// <param name="styles">The style sheet (its title rule).</param>
    /// <param name="labels">The label builder (for the text metrics).</param>
    /// <returns>The title line.</returns>
    private static SceneText DrawTitle(string title, SceneRect? bounds, double topMargin, UnifiedStyles styles, LabelBuilder labels)
    {
        var paint = styles.Title;
        var font = new SceneFont(paint.FontFamily, paint.FontSize, false, false);
        var box = bounds ?? new SceneRect(0, 0, 0, 0);
        var x = box.X + (box.Width / 2);
        var baseline = -topMargin;
        // The box is SVG's getBBox of the centered text: the advance grown to the glyph boxes at its ends
        var (advance, left, right) = labels.SvgLineExtent(title, font);
        var (ascent, descent) = labels.VerticalMetrics(font);
        return new SceneText(
            x,
            baseline,
            SceneTextAnchor.Middle,
            [new SceneTextRun(title, font, paint.Color)],
            new SceneRect(x - (advance / 2) + left, baseline - ascent, right - left, ascent + descent))
        {
            Role = "title",
        };
    }
}
