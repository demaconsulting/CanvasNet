// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;

namespace DemaConsulting.CanvasNet.PlantUml.Svek;

/// <summary>
///     The renderer of the shared engine: draws a placed diagram as a scene, with PlantUML's link lines, decorations,
///     and labels, its cluster outlines and titles (<c>SvekRenderer.Clusters.cs</c>), and its notes with their pointers
///     (<c>SvekRenderer.Notes.cs</c>), and calls the painters of the entity images.
/// </summary>
/// <remarks>
///     <para>
///         The drawing has PlantUML's margins around the placement (6 on the left, 12 at the top, 15 on the right, 16
///         at the bottom, measured from the pictures the jar draws). The items are painted in PlantUML's order:
///         clusters (parents first) with their region separators, entities, notes, then links with their decorations
///         and labels. Every item carries the role that names what it draws (<c>cluster</c>, <c>cluster-title</c>,
///         <c>cluster-separator</c>, <c>note</c>, <c>note-text</c>, <c>link</c>, <c>link-head</c>, <c>link-tail</c>,
///         <c>link-label</c>, <c>link-label-arrow</c>, <c>link-tail-label</c>, <c>link-head-label</c>) and the id of its
///         element; the entity images give their own roles.
///     </para>
///     <para>
///         A link is drawn as the curve of its route (<see cref="SvekGeometry.Spline"/>), shortened by the contact
///         length of its decorations, solid, dashed (7, 7), dotted (1, 3), or bold (2 wide), in its color or the
///         theme's arrow color; a hidden link, a link drawn as a note pointer, and a link without a route are not drawn.
///         The class is stateless and thread-safe.
///     </para>
/// </remarks>
internal static partial class SvekRenderer
{
    /// <summary>The margin left of the placement.</summary>
    public const double MarginLeft = 6;

    /// <summary>The margin above the placement.</summary>
    public const double MarginTop = 12;

    /// <summary>The margin right of the placement.</summary>
    public const double MarginRight = 15;

    /// <summary>The margin below the placement.</summary>
    public const double MarginBottom = 16;

    /// <summary>
    ///     Draws a placed diagram.
    /// </summary>
    /// <param name="diagram">The diagram.</param>
    /// <param name="placement">Its placement.</param>
    /// <returns>The scene, whose view box is the placement with the margins, over the theme's background.</returns>
    public static Scene Render(SvekDiagram diagram, SvekPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(diagram);
        ArgumentNullException.ThrowIfNull(placement);
        var (dx, dy) = (MarginLeft, MarginTop);
        var items = new List<SceneItem>();

        // Clusters first, parents before children (the order of the diagram)
        foreach (var cluster in placement.Clusters)
        {
            var box = Shift(cluster.Box, dx, dy);
            if (cluster.Cluster.Image is { } image)
            {
                items.AddRange(image.Draw(box));
            }

            items.AddRange(cluster.Separators.Select(s => new ScenePath(SceneShapes.Line(s.From.X + dx, s.From.Y + dy, s.To.X + dx, s.To.Y + dy))
            {
                Role = "cluster-separator",
                Id = cluster.Cluster.Id,
                Location = cluster.Cluster.Location,
                Stroke = new SceneStroke(SkinTheme.LineColor, 1.5) { Dash = [8, 10] },
            }));
        }

        foreach (var entity in placement.Entities)
        {
            items.AddRange(entity.Entity.Image.Draw(entity.Box.X + dx, entity.Box.Y + dy));
        }

        foreach (var note in placement.Notes)
        {
            items.AddRange(DrawNote(note, diagram.Theme, dx, dy));
        }

        foreach (var link in placement.Links)
        {
            items.AddRange(DrawLink(link, diagram.Theme, dx, dy));
        }

        var view = new SceneRect(0, 0, placement.Width + MarginLeft + MarginRight, placement.Height + MarginTop + MarginBottom);
        return new Scene(view, items) { Background = diagram.Theme.Background };
    }

    /// <summary>
    ///     Draws one placed link: its line, its decorations, and its labels.
    /// </summary>
    /// <param name="link">The placed link.</param>
    /// <param name="theme">The theme.</param>
    /// <param name="dx">The horizontal offset of the drawing.</param>
    /// <param name="dy">The vertical offset of the drawing.</param>
    /// <returns>The items; none for a link that is not drawn.</returns>
    public static IReadOnlyList<SceneItem> DrawLink(PlacedLink link, SkinTheme theme, double dx, double dy)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(theme);
        if (!link.Drawn || link.Route.Count < 2)
        {
            return [];
        }

        // The stroke of the line: the link's color and width, or the theme's
        var model = link.Link;
        var style = theme[SkinElement.SvekArrow];
        var color = model.Color ?? style.Line ?? SkinTheme.LineColor;
        var width = model.Thickness ?? (model.Style == LinkStyle.Bold ? 2 : style.LineWidth);
        IReadOnlyList<double>? dash = model.Style switch
        {
            LinkStyle.Dashed => [7, 7],
            LinkStyle.Dotted => [1, 3],
            _ => null,
        };

        // The decorations at both ends, and the line that stops where they begin
        var route = link.Route.Select(p => new SvekPoint(p.X + dx, p.Y + dy)).ToList();
        var tail = SvekGeometry.Extremity(model.TailDecor, route[0], SvekGeometry.ArrivalAngle(route, atEnd: false));
        var head = SvekGeometry.Extremity(model.HeadDecor, route[^1], SvekGeometry.ArrivalAngle(route, atEnd: true));
        var line = SvekGeometry.Shorten(route, tail?.ContactLength ?? 0, head?.ContactLength ?? 0);
        var items = new List<SceneItem>
        {
            new ScenePath(SvekGeometry.Spline(line))
            {
                Role = "link",
                Id = model.Id,
                Location = model.Location,
                Stroke = width > 0 ? new SceneStroke(color, width) { Dash = dash } : null,
            },
        };
        var background = theme.Background ?? SkinTheme.White;
        foreach (var (extremity, role) in new[] { (tail, "link-tail"), (head, "link-head") })
        {
            if (extremity is null)
            {
                continue;
            }

            items.AddRange(extremity.Parts.Select(part => new ScenePath(part.Segments)
            {
                Role = role,
                Id = model.Id,
                Location = model.Location,
                Fill = part.Fill switch
                {
                    ExtremityFill.Line => color,
                    ExtremityFill.Background => background,
                    _ => null,
                },
                Stroke = new SceneStroke(color, part.StrokeWidth ?? width),
            }));
        }

        // The labels: the middle one with its triangle, then the end labels
        if (link.Label is { } label)
        {
            items.AddRange(DrawLabel(label, model, style, "link-label", dx, dy));
        }

        if (link.TailLabel is { } tailLabel)
        {
            items.AddRange(DrawLabel(tailLabel, model, style, "link-tail-label", dx, dy));
        }

        if (link.HeadLabel is { } headLabel)
        {
            items.AddRange(DrawLabel(headLabel, model, style, "link-head-label", dx, dy));
        }

        return items;
    }

    /// <summary>
    ///     Draws a placed label: its text inside its margin, after the triangle of its direction mark (PlantUML's
    ///     <c>TextBlockArrow2</c>: a filled triangle of 80 % of the font size in a square of the font size, centered on
    ///     the text, pointing along the link).
    /// </summary>
    /// <param name="label">The label.</param>
    /// <param name="link">The link.</param>
    /// <param name="style">The style of the link labels.</param>
    /// <param name="role">The role of the text.</param>
    /// <param name="dx">The horizontal offset of the drawing.</param>
    /// <param name="dy">The vertical offset of the drawing.</param>
    /// <returns>The items.</returns>
    private static IEnumerable<SceneItem> DrawLabel(PlacedLabel label, SvekLink link, ElementStyle style, string role, double dx, double dy)
    {
        var box = Shift(label.Box, dx, dy);
        var arrowSize = label.ArrowAngle is null ? 0 : style.FontSize;
        var top = box.Y + ((box.Height - label.Text.Height) / 2);
        foreach (var item in label.Text.Draw(box.X + SvekLayout.LabelMargin + arrowSize, top, TextAlign.Left, role, link.Location))
        {
            yield return item with { Id = link.Id };
        }

        if (label.ArrowAngle is { } angle)
        {
            // PlantUML measures the angle from the y axis toward the x axis
            var half = (int)(style.FontSize * 0.8) / 2;
            var (cx, cy) = (box.X + SvekLayout.LabelMargin + half, box.Y + (box.Height / 2));
            var alpha = (Math.PI / 2) - angle;
            const double Beta = Math.PI * 4 / 5;
            (double X, double Y) Point(double a) => (cx + (half * Math.Sin(a)), cy + (half * Math.Cos(a)));
            yield return new ScenePath(SceneShapes.Polygon(Point(alpha), Point(alpha + Beta), Point(alpha - Beta)))
            {
                Role = "link-label-arrow",
                Id = link.Id,
                Location = link.Location,
                Fill = style.FontColor,
                Stroke = new SceneStroke(style.FontColor, 1),
            };
        }
    }

    /// <summary>
    ///     Moves a rectangle.
    /// </summary>
    /// <param name="box">The rectangle.</param>
    /// <param name="dx">The horizontal offset.</param>
    /// <param name="dy">The vertical offset.</param>
    /// <returns>The moved rectangle.</returns>
    private static SceneRect Shift(SceneRect box, double dx, double dy) => box with { X = box.X + dx, Y = box.Y + dy };

    /// <summary>
    ///     Creates a stroke, or null for no line.
    /// </summary>
    /// <param name="color">The color, or null.</param>
    /// <param name="width">The width.</param>
    /// <returns>The stroke.</returns>
    private static SceneStroke? StrokeOf(Rgba32? color, double width) =>
        color is { } c && width > 0 ? new SceneStroke(c, width) : null;
}
