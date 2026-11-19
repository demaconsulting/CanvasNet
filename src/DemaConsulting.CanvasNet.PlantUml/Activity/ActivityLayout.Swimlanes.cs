// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;

namespace DemaConsulting.CanvasNet.PlantUml.Activity;

/// <content>
///     The swimlanes (PlantUML's <c>Swimlanes</c>): every lane as wide as what it draws, side by side with dividers,
///     titles on top, and the arrows between lanes drawn last.
/// </content>
internal sealed partial class ActivityLayout
{
    /// <summary>The half space of a divider.</summary>
    private const double DividerHalf = 5;

    /// <summary>
    ///     Draws a diagram with swimlanes.
    /// </summary>
    /// <param name="root">The tile of the diagram.</param>
    /// <returns>The shapes, arrows last.</returns>
    private List<ActivityShape> DrawLanes(Tile root)
    {
        var lanes = Lanes;
        var count = lanes.Count;

        // The extent of what each lane draws, its title, and its width (the last, unnamed lane is empty)
        var extents = new SceneRect[count + 1];
        var titles = new TextBlock[count + 1];
        var widths = new double[count + 1];
        for (var i = 0; i < count; i++)
        {
            var canvas = new TileCanvas(this).ForLane(lanes[i]);
            root.Draw(canvas, 0, 0);
            extents[i] = Limits(Flush(canvas)) ?? new SceneRect(0, 0, 0, 0);
            titles[i] = Text(lanes[i].Display, SkinElement.Swimlane, lanes[i].Location);
            widths[i] = extents[i].Width;
        }

        extents[count] = new SceneRect(0, 0, 0, 0);
        titles[count] = TextBlock.Empty;
        var titlesHeight = titles.Max(t => t.Height);
        var titleDy = titlesHeight > 0 ? titlesHeight + 5 : 0;
        var height = root.Geometry.Height + titleDy;

        // The dividers and the translation of each lane
        double HalfMissing(int i)
        {
            if (i == 0 || i > count)
            {
                return DividerHalf;
            }

            var titleWidth = titles[i - 1].Width;
            return titleWidth <= widths[i - 1] ? DividerHalf : Math.Max(DividerHalf, DividerHalf + ((titleWidth - widths[i - 1]) / 2));
        }

        var dividers = new (double X1, double X2)[count + 1];
        var translates = new double[count + 1];
        double xpos = 0;
        for (var i = 0; i <= count; i++)
        {
            dividers[i] = (HalfMissing(i), HalfMissing(i + 1));
            var dividerWidth = dividers[i].X1 + dividers[i].X2;
            translates[i] = xpos + dividerWidth - extents[i].X;
            xpos += widths[i] + dividerWidth;
        }

        // Each lane: its fill, its tiles, and the divider on its left
        var output = new TileCanvas(this);
        var style = Theme[SkinElement.Swimlane];
        var dividerStroke = StrokeOf(style) ?? new SceneStroke(SkinTheme.Black, 1.5);

        // The unpainted background of the titles, which spans every lane and sets the width of the page
        var firstDivider = translates[0] + extents[0].X - dividers[0].X2;
        var lastDivider = translates[count] + extents[count].X - dividers[count].X2;
        var titlesBack = output.Rect("lane-titles", firstDivider, 0, lastDivider - firstDivider + 1.5, titlesHeight, 0, null, null, default);
        titlesBack.IgnoreX = true;
        titlesBack.IgnoreY = true;

        for (var i = 0; i <= count; i++)
        {
            var x = translates[i] + extents[i].X;
            if (i < count && lanes[i].Fill is { } fill && fill.A > 0)
            {
                var next = dividers[i + 1];
                var back = output.Rect("lane", x - dividers[i].X2, 0, widths[i] + dividers[i].X2 + next.X1, height, 0, fill, new SceneStroke(fill, 1), lanes[i].Location);
                back.IgnoreX = true;
                back.IgnoreY = true;
            }

            if (i < count)
            {
                var column = (x - dividers[i].X2, x + widths[i] + dividers[i + 1].X1);
                root.Draw(output.ForLane(lanes[i], column), translates[i], titleDy);
            }

            var dividerX = x - (dividers[i].X1 + dividers[i].X2);
            output.Empty(dividerX, 0, dividers[i].X1 + dividers[i].X2, 1);
            output.Line("lane-divider", dividerX + dividers[i].X1, 0, dividerX + dividers[i].X1, height, dividerStroke, default);
        }

        // The arrows between two lanes
        var index = new Dictionary<ActivityLane, int>();
        for (var i = 0; i < count; i++)
        {
            index[lanes[i]] = i;
        }

        root.Draw(output.ForCrossing(lane => translates[index[lane]]), 0, titleDy);

        // The titles, centered over their lanes; PlantUML's compression keeps them centered (and only the compression on y sees them)
        for (var i = 0; i < count; i++)
        {
            var left = translates[i] + extents[i].X;
            var title = titles[i];
            var before = output.Shapes.Count;
            output.Text(title, left + ((widths[i] - title.Width) / 2), 0, TextAlign.Left, "lane-title", lanes[i].Location);
            for (var j = before; j < output.Shapes.Count; j++)
            {
                var shape = output.Shapes[j];
                shape.IgnoreX = true;
                shape.Center = (left, widths[i], title.Width, shape.X - (left + ((widths[i] - title.Width) / 2)));
            }
        }

        return Flush(output);
    }
}
