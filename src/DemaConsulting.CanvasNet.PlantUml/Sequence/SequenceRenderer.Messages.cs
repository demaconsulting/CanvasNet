// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;

namespace DemaConsulting.CanvasNet.PlantUml.Sequence;

/// <content>
///     The messages (arrows, heads, numbers, and labels), the self messages, and the notes.
/// </content>
internal static partial class SequenceRenderer
{
    /// <summary>The length of an arrow head.</summary>
    private const double HeadLength = 10;

    /// <summary>The half height of an arrow head.</summary>
    private const double HeadHalf = 4;

    /// <summary>The size of the fold of a note.</summary>
    private const double Fold = 10;

    /// <summary>
    ///     Draws a message between two participants.
    /// </summary>
    /// <param name="message">The placed message.</param>
    /// <param name="theme">The theme.</param>
    /// <param name="items">The scene items.</param>
    private static void DrawMessage(PlacedMessage message, SkinTheme theme, List<SceneItem> items)
    {
        var style = theme[SkinElement.Arrow];
        var arrow = message.Message.Arrow;
        var color = arrow.Color ?? style.Line ?? SkinTheme.LineColor;
        var stroke = new SceneStroke(color, theme.ArrowThickness) { Dash = arrow.Dotted ? [2, 2] : null };
        var location = message.Message.Location;
        var y = message.ArrowY;

        // The receiver's end and the sender's end, each with its head and circle
        var (leftHead, leftCircle, rightHead, rightCircle) = message.ToLeft
            ? (arrow.ToHead, arrow.ToCircle, arrow.FromHead, arrow.FromCircle)
            : (arrow.FromHead, arrow.FromCircle, arrow.ToHead, arrow.ToCircle);
        var heads = new List<SceneItem>();
        var x1 = DrawEnd(message.XLeft, y, -1, leftHead, leftCircle, color, theme.ArrowThickness, heads);
        var x2 = DrawEnd(message.XRight, y, 1, rightHead, rightCircle, color, theme.ArrowThickness, heads);
        items.AddRange(heads.Select(h => h with { Location = location }));
        items.Add(new ScenePath(SceneShapes.Line(x1, y, x2, y)) { Role = "message", Stroke = stroke, Location = location });
        DrawMessageText(message.Number, message.Label, message.TextX, message.TextTop, location, items);
    }

    /// <summary>
    ///     Draws the number and the label of a message.
    /// </summary>
    /// <param name="number">The number, or null.</param>
    /// <param name="label">The label.</param>
    /// <param name="x">The x of the text.</param>
    /// <param name="top">The top of the text.</param>
    /// <param name="location">The location.</param>
    /// <param name="items">The scene items.</param>
    private static void DrawMessageText(TextBlock? number, TextBlock label, double x, double top, PlantUmlLocation location, List<SceneItem> items)
    {
        if (number is not null)
        {
            items.AddRange(number.Draw(x, top, TextAlign.Left, "message-number", location));
            x += number.Width + 4;
        }

        items.AddRange(label.Draw(x, top, TextAlign.Left, "message-text", location));
    }

    /// <summary>
    ///     Draws the decoration at one end of an arrow and gets where the line ends.
    /// </summary>
    /// <param name="x">The x where the arrow meets the participant.</param>
    /// <param name="y">The y of the arrow.</param>
    /// <param name="side">-1 for the left end, 1 for the right end.</param>
    /// <param name="head">The head.</param>
    /// <param name="circle">Whether a circle is drawn at the end.</param>
    /// <param name="color">The color.</param>
    /// <param name="thickness">The line width.</param>
    /// <param name="items">The scene items.</param>
    /// <returns>The x where the line ends.</returns>
    private static double DrawEnd(double x, double y, int side, ArrowHead head, bool circle, Rgba32 color, double thickness, List<SceneItem> items)
    {
        // Measured toward the inside of the arrow: the tip is 2 pixels inside on the right, 1 on the left
        var inward = -side;
        if (circle)
        {
            var center = x + (inward * 0.5);
            items.Add(new ScenePath(SceneShapes.Ellipse(center, y, 4, 4)) { Role = "message-head", Fill = color, Stroke = new SceneStroke(color, 1.5) });
        }

        double gap = side > 0 ? 2 : 1;
        if (circle)
        {
            gap = 7.5;
        }

        var tip = x + (inward * gap);
        var stroke = new SceneStroke(color, thickness);
        var back = tip + (inward * HeadLength);
        switch (head)
        {
            case ArrowHead.Filled:
                items.Add(new ScenePath(SceneShapes.Polygon((back, y - HeadHalf), (tip, y), (back, y + HeadHalf), (tip + (inward * 6), y)))
                {
                    Role = "message-head",
                    Fill = color,
                    Stroke = stroke,
                });
                return tip + (inward * 4);
            case ArrowHead.Open:
                items.Add(new ScenePath([.. SceneShapes.Line(tip, y, back, y - HeadHalf), .. SceneShapes.Line(tip, y, back, y + HeadHalf)])
                {
                    Role = "message-head",
                    Stroke = stroke,
                });
                return tip - inward;
            case ArrowHead.HalfTop:
                items.Add(new ScenePath(SceneShapes.Polygon((back, y - HeadHalf), (tip, y), (back, y))) { Role = "message-head", Fill = color, Stroke = stroke });
                return tip - inward;
            case ArrowHead.HalfBottom:
                items.Add(new ScenePath(SceneShapes.Polygon((back, y), (tip, y), (back, y + HeadHalf))) { Role = "message-head", Fill = color, Stroke = stroke });
                return tip - inward;
            case ArrowHead.Cross:
                var center = x + (inward * 13);
                var cross = new SceneStroke(color, 2);
                items.Add(new ScenePath([.. SceneShapes.Line(center - 5, y - 5, center + 5, y + 5), .. SceneShapes.Line(center - 5, y + 5, center + 5, y - 5)])
                {
                    Role = "message-head",
                    Stroke = cross,
                });
                return center;
            default:
                if (circle)
                {
                    return x + (inward * 4.5);
                }

                return side > 0 ? x - 1 : x;
        }
    }

    /// <summary>
    ///     Draws a message from a participant to itself: out 42 pixels, down 13, and back with the head.
    /// </summary>
    /// <param name="self">The placed self message.</param>
    /// <param name="theme">The theme.</param>
    /// <param name="items">The scene items.</param>
    private static void DrawSelf(PlacedSelf self, SkinTheme theme, List<SceneItem> items)
    {
        var style = theme[SkinElement.Arrow];
        var arrow = self.Message.Arrow;
        var color = arrow.Color ?? style.Line ?? SkinTheme.LineColor;
        var stroke = new SceneStroke(color, theme.ArrowThickness) { Dash = arrow.Dotted ? [2, 2] : null };
        var location = self.Message.Location;
        var right = self.LoopX + 42;
        items.Add(new ScenePath(SceneShapes.Line(self.StartX, self.TopY, right, self.TopY)) { Role = "message", Stroke = stroke, Location = location });
        items.Add(new ScenePath(SceneShapes.Line(right, self.TopY, right, self.BottomY)) { Role = "message", Stroke = stroke, Location = location });
        var heads = new List<SceneItem>();
        var end = DrawEnd(self.LoopX, self.BottomY, -1, arrow.ToHead, arrow.ToCircle, color, theme.ArrowThickness, heads);
        var lineEnd = arrow.ToHead == ArrowHead.Filled ? self.LoopX + 1 : end;
        items.Add(new ScenePath(SceneShapes.Line(lineEnd, self.BottomY, right, self.BottomY)) { Role = "message", Stroke = stroke, Location = location });
        items.AddRange(heads.Select(h => h with { Location = location }));
        DrawMessageText(self.Number, self.Label, self.TextX, self.TextTop, location, items);
    }

    /// <summary>
    ///     Draws a note: a sheet with a folded corner, a hexagon, or a rectangle, and its text.
    /// </summary>
    /// <param name="note">The placed note.</param>
    /// <param name="theme">The theme.</param>
    /// <param name="items">The scene items.</param>
    private static void DrawNote(PlacedNote note, SkinTheme theme, List<SceneItem> items)
    {
        var style = theme[SkinElement.Note];
        var fill = note.Note.Fill ?? style.Fill;
        var stroke = StrokeOf(style);
        var location = note.Note.Location;
        var (x, y, w, h) = (note.X, note.Y, note.Width, note.Height);
        switch (note.Note.Shape)
        {
            case NoteShape.Box:
                items.Add(new ScenePath(SceneShapes.Rect(x, y, w, h)) { Role = "note", Fill = fill, Stroke = stroke, Location = location });
                break;
            case NoteShape.Hexagon:
                items.Add(new ScenePath(SceneShapes.Polygon((x + Fold, y), (x + w - Fold, y), (x + w, y + (h / 2)), (x + w - Fold, y + h), (x + Fold, y + h), (x, y + (h / 2))))
                {
                    Role = "note",
                    Fill = fill,
                    Stroke = stroke,
                    Location = location,
                });
                break;
            default:
                items.Add(new ScenePath(SceneShapes.Polygon((x, y), (x, y + h), (x + w, y + h), (x + w, y + Fold), (x + w - Fold, y)))
                {
                    Role = "note",
                    Fill = fill,
                    Stroke = stroke,
                    Location = location,
                });
                items.Add(new ScenePath(SceneShapes.Polygon((x + w - Fold, y), (x + w - Fold, y + Fold), (x + w, y + Fold)))
                {
                    Role = "note",
                    Fill = fill,
                    Stroke = stroke,
                    Location = location,
                });
                break;
        }

        items.AddRange(note.Text.Draw(note.TextX, note.TextTop, TextAlign.Left, "note-text", location));
    }
}
