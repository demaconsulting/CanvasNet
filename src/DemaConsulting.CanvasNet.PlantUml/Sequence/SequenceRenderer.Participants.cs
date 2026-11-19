// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;

namespace DemaConsulting.CanvasNet.PlantUml.Sequence;

/// <content>
///     The participant shapes: the box, the stick figure, the boundary, control, and entity symbols, the cylinder,
///     the stacked boxes, and the queue.
/// </content>
internal static partial class SequenceRenderer
{
    /// <summary>
    ///     Draws a participant's head or tail.
    /// </summary>
    /// <remarks>
    ///     A head is aligned at the bottom of the head row (the text below a symbol); a tail hangs from the end of the
    ///     life line (the text above a symbol). The text of a symbol is centered 3 pixels left of the life line, as
    ///     PlantUML draws it.
    /// </remarks>
    /// <param name="participant">The participant.</param>
    /// <param name="top">The top of the head or tail.</param>
    /// <param name="head">Whether it is the head.</param>
    /// <param name="items">The scene items.</param>
    private static void DrawParticipant(PlacedParticipant participant, double top, bool head, List<SceneItem> items)
    {
        var role = head ? "participant-head" : "participant-tail";
        var style = participant.Style;
        var fill = participant.Participant.Fill ?? style.Fill;
        var stroke = StrokeOf(style);
        var text = participant.Text;
        var code = participant.Participant.Code;
        var location = participant.Participant.Location;
        var (x, c, w) = (participant.PosB, participant.PosC, participant.Width);
        var symbolTextX = x + ((w - text.Width) / 2) - 3;
        var parts = new List<SceneItem>();
        switch (participant.Participant.Kind)
        {
            case ParticipantKind.Actor:
                var figure = head ? top : top + text.Height;
                parts.AddRange(StickFigure(c, figure, fill, stroke));
                parts.AddRange(text.Draw(symbolTextX, head ? top + 60 : top, TextAlign.Center, role, location));
                break;
            case ParticipantKind.Boundary or ParticipantKind.Control or ParticipantKind.Entity:
                var symbol = head ? top : top + text.Height + 4;
                parts.AddRange(Symbol(participant.Participant.Kind, c, symbol, fill, stroke));
                parts.AddRange(text.Draw(symbolTextX, head ? top + 28 : top, TextAlign.Center, role, location));
                break;
            case ParticipantKind.Database:
                var cylinder = head ? top : top + text.Height;
                parts.AddRange(Cylinder(c, cylinder, fill, stroke));
                parts.AddRange(text.Draw(symbolTextX, head ? top + 46 : top, TextAlign.Center, role, location));
                break;
            case ParticipantKind.Collections:
                var boxHeight = participant.PrefHeight - 5;
                var front = top + 4;
                parts.Add(new ScenePath(SceneShapes.Rect(x + 4, front - 4, w - 4, boxHeight)) { Role = "participant", Fill = fill, Stroke = stroke });
                parts.Add(new ScenePath(SceneShapes.Rect(x, front, w - 4, boxHeight)) { Role = "participant", Fill = fill, Stroke = stroke });
                parts.AddRange(text.Draw(x + style.Padding, front + style.Padding, TextAlign.Center, role, location));
                break;
            case ParticipantKind.Queue:
                parts.AddRange(Queue(x, top, text.Width + 10, participant.PrefHeight, fill, stroke));
                parts.AddRange(text.Draw(x + 5, top + 5, TextAlign.Center, role, location));
                break;
            default:
                parts.Add(new ScenePath(SceneShapes.Rect(x, top, w, participant.PrefHeight - 1, style.RoundCorner / 2)) { Role = "participant", Fill = fill, Stroke = stroke });
                parts.AddRange(text.Draw(x + style.Padding, top + style.Padding, TextAlign.Center, role, location));
                break;
        }

        items.AddRange(parts.Select(p => p with { Role = role, Id = code, Location = location }));
    }

    /// <summary>
    ///     Builds an actor's stick figure: a head of radius 8, a body, arms 26 wide, and legs.
    /// </summary>
    /// <param name="c">The x of the life line.</param>
    /// <param name="top">The top of the figure.</param>
    /// <param name="fill">The fill of the head.</param>
    /// <param name="stroke">The stroke.</param>
    /// <returns>The paths.</returns>
    private static IEnumerable<ScenePath> StickFigure(double c, double top, Rgba32? fill, SceneStroke? stroke)
    {
        yield return new ScenePath(SceneShapes.Ellipse(c, top + 8.5, 8, 8)) { Role = "participant", Fill = fill, Stroke = stroke };
        var neck = top + 16.5;
        var hip = top + 43.5;
        yield return new ScenePath(
        [
            SceneSegment.Move(c, neck), SceneSegment.Line(c, hip),
            SceneSegment.Move(c - 13, top + 24.5), SceneSegment.Line(c + 13, top + 24.5),
            SceneSegment.Move(c, hip), SceneSegment.Line(c - 13, top + 58.5),
            SceneSegment.Move(c, hip), SceneSegment.Line(c + 13, top + 58.5),
        ])
        {
            Role = "participant",
            Stroke = stroke,
        };
    }

    /// <summary>
    ///     Builds a boundary, control, or entity symbol: a circle of radius 12 with its mark.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="c">The x of the life line.</param>
    /// <param name="top">The top of the circle.</param>
    /// <param name="fill">The fill.</param>
    /// <param name="stroke">The stroke.</param>
    /// <returns>The paths.</returns>
    private static IEnumerable<ScenePath> Symbol(ParticipantKind kind, double c, double top, Rgba32? fill, SceneStroke? stroke)
    {
        var cy = top + 12;
        if (kind == ParticipantKind.Boundary)
        {
            var x0 = c - 20.5;
            yield return new ScenePath([.. SceneShapes.Line(x0, top, x0, top + 24), .. SceneShapes.Line(x0, cy, x0 + 17, cy)])
            {
                Role = "participant",
                Stroke = stroke,
            };
            yield return new ScenePath(SceneShapes.Ellipse(x0 + 29, cy, 12, 12)) { Role = "participant", Fill = fill, Stroke = stroke };
            yield break;
        }

        yield return new ScenePath(SceneShapes.Ellipse(c, cy, 12, 12)) { Role = "participant", Fill = fill, Stroke = stroke };
        if (kind == ParticipantKind.Control)
        {
            yield return new ScenePath(SceneShapes.Polygon((c - 4, top), (c + 2, top - 5), (c, top), (c + 2, top + 5)))
            {
                Role = "participant",
                Fill = stroke?.Color,
                Stroke = stroke,
            };
        }
        else
        {
            yield return new ScenePath(SceneShapes.Line(c - 12, top + 26, c + 12, top + 26)) { Role = "participant", Stroke = stroke };
        }
    }

    /// <summary>
    ///     Builds a database cylinder 36 wide and 46 high.
    /// </summary>
    /// <param name="c">The x of the life line.</param>
    /// <param name="top">The top of the cylinder.</param>
    /// <param name="fill">The fill.</param>
    /// <param name="stroke">The stroke.</param>
    /// <returns>The paths.</returns>
    private static IEnumerable<ScenePath> Cylinder(double c, double top, Rgba32? fill, SceneStroke? stroke)
    {
        var (x0, x1) = (c - 18, c + 18);
        yield return new ScenePath(
        [
            SceneSegment.Move(x0, top + 10),
            SceneSegment.Cubic(x0, top, c, top, c, top),
            SceneSegment.Cubic(c, top, x1, top, x1, top + 10),
            SceneSegment.Line(x1, top + 36),
            SceneSegment.Cubic(x1, top + 46, c, top + 46, c, top + 46),
            SceneSegment.Cubic(c, top + 46, x0, top + 46, x0, top + 36),
            SceneSegment.Close(),
        ])
        {
            Role = "participant",
            Fill = fill,
            Stroke = stroke,
        };
        yield return new ScenePath(
        [
            SceneSegment.Move(x0, top + 10),
            SceneSegment.Cubic(x0, top + 20, c, top + 20, c, top + 20),
            SceneSegment.Cubic(c, top + 20, x1, top + 20, x1, top + 10),
        ])
        {
            Role = "participant",
            Stroke = stroke,
        };
    }

    /// <summary>
    ///     Builds a queue: a horizontal cylinder around the text.
    /// </summary>
    /// <param name="x">The left of the column.</param>
    /// <param name="top">The top of the queue.</param>
    /// <param name="width">The width of the straight part.</param>
    /// <param name="height">The height.</param>
    /// <param name="fill">The fill.</param>
    /// <param name="stroke">The stroke.</param>
    /// <returns>The paths.</returns>
    private static IEnumerable<ScenePath> Queue(double x, double top, double width, double height, Rgba32? fill, SceneStroke? stroke)
    {
        var (left, right, middle, bottom) = (x + 5, x + 5 + width, top + (height / 2), top + height);
        yield return new ScenePath(
        [
            SceneSegment.Move(left, top),
            SceneSegment.Line(right, top),
            SceneSegment.Cubic(right + 5, top, right + 5, middle, right + 5, middle),
            SceneSegment.Cubic(right + 5, middle, right + 5, bottom, right, bottom),
            SceneSegment.Line(left, bottom),
            SceneSegment.Cubic(x, bottom, x, middle, x, middle),
            SceneSegment.Cubic(x, middle, x, top, left, top),
            SceneSegment.Close(),
        ])
        {
            Role = "participant",
            Fill = fill,
            Stroke = stroke,
        };
        yield return new ScenePath(
        [
            SceneSegment.Move(right, top),
            SceneSegment.Cubic(right - 5, top, right - 5, middle, right - 5, middle),
            SceneSegment.Cubic(right - 5, bottom, right, bottom, right, bottom),
        ])
        {
            Role = "participant",
            Stroke = stroke,
        };
    }
}
