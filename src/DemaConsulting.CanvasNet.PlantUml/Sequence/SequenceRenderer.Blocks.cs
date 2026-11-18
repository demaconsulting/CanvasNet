// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;

namespace DemaConsulting.CanvasNet.PlantUml.Sequence;

/// <content>
///     The group frames with their header tabs and sections, and the dividers.
/// </content>
internal static partial class SequenceRenderer
{
    /// <summary>
    ///     Draws a group frame: its header tab with the keyword, its condition, its outline, and its sections.
    /// </summary>
    /// <param name="group">The placed group.</param>
    /// <param name="theme">The theme.</param>
    /// <param name="items">The scene items.</param>
    private static void DrawGroup(PlacedGroup group, SkinTheme theme, List<SceneItem> items)
    {
        var header = theme[SkinElement.GroupHeader];
        var frame = theme[SkinElement.Group];
        var location = group.Start.Location;
        var (x, y, w, h) = (group.X, group.Y, group.Width, group.Height);
        var (tabW, tabH) = (group.TabWidth, group.TabHeight);
        items.Add(new ScenePath(SceneShapes.Polygon((x, y), (x + tabW, y), (x + tabW, y + tabH - 10), (x + tabW - 10, y + tabH), (x, y + tabH)))
        {
            Role = "group-header",
            Fill = header.Fill,
            Stroke = StrokeOf(header),
            Location = location,
        });
        items.Add(new ScenePath(SceneShapes.Rect(x, y, w, h)) { Role = "group", Stroke = StrokeOf(frame), Location = location });
        items.AddRange(group.Keyword.Draw(x + 15, y + 1, TextAlign.Left, "group-text", location));
        if (group.Label is { } label)
        {
            items.AddRange(label.Draw(x + tabW + 15, y + 2, TextAlign.Left, "group-text", location));
        }

        var dashed = new SceneStroke(frame.Line ?? SkinTheme.Black, 1) { Dash = [2, 2] };
        foreach (var section in group.Sections)
        {
            items.Add(new ScenePath(SceneShapes.Line(x, section.Y, x + w, section.Y)) { Role = "group-else", Stroke = dashed, Location = location });
            if (section.Label is { } condition)
            {
                items.AddRange(condition.Draw(x + 5, section.Y + 2, TextAlign.Left, "group-text", location));
            }
        }
    }

    /// <summary>
    ///     Draws a divider: a double line across the diagram with the text in a box over its middle.
    /// </summary>
    /// <param name="divider">The placed divider.</param>
    /// <param name="theme">The theme.</param>
    /// <param name="items">The scene items.</param>
    private static void DrawDivider(PlacedDivider divider, SkinTheme theme, List<SceneItem> items)
    {
        var style = theme[SkinElement.Separator];
        var location = divider.Location;
        var middle = divider.Top + (divider.Height / 2);
        var (x1, x2) = (divider.X1, divider.X2);
        var back = style.Fill ?? SkinTheme.White;
        var line = style.Line ?? SkinTheme.Black;
        items.Add(new ScenePath(SceneShapes.Rect(x1, middle - 1, x2 - x1, 3)) { Role = "divider", Fill = back, Stroke = new SceneStroke(back, 1), Location = location });
        var thin = new SceneStroke(line, style.LineWidth / 2);
        items.Add(new ScenePath([.. SceneShapes.Line(x1, middle - 1, x2, middle - 1), .. SceneShapes.Line(x1, middle + 2, x2, middle + 2)])
        {
            Role = "divider",
            Stroke = thin,
            Location = location,
        });
        if (divider.Text is not { } text)
        {
            return;
        }

        var rectX = x1 + ((x2 - x1 - divider.RectWidth) / 2);
        var rectY = divider.Top + ((divider.Height - divider.RectHeight) / 2);
        items.Add(new ScenePath(SceneShapes.Rect(rectX, rectY, divider.RectWidth, divider.RectHeight))
        {
            Role = "divider",
            Fill = back,
            Stroke = new SceneStroke(line, style.LineWidth),
            Location = location,
        });
        items.AddRange(text.Draw(rectX + 6, rectY + style.Padding, TextAlign.Left, "divider-text", location));
    }
}
