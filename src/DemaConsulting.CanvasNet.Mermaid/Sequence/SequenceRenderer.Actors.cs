// Portions derived from mermaid (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Sequence;

/// <content>
///     Participants: Mermaid's <c>drawActor</c> for the <c>participant</c> box and the <c>actor</c> stick figure, at
///     the top (with the lifeline) and mirrored at the bottom.
/// </content>
internal sealed partial class SequenceRenderer
{
    /// <summary>
    ///     Draws a participant (Mermaid's <c>drawActor</c>).
    /// </summary>
    /// <param name="actor">The participant.</param>
    /// <param name="isFooter">Whether the mirrored participant at the bottom is drawn.</param>
    /// <returns>The height of the drawing, or NaN for a type that draws nothing (Mermaid's <c>undefined</c>).</returns>
    public double DrawActor(SequenceActorLayout actor, bool isFooter) => actor.Actor.Type switch
    {
        SequenceParticipantTypes.Actor => DrawActorTypeActor(actor, isFooter),
        SequenceParticipantTypes.Participant => DrawActorTypeParticipant(actor, isFooter),
        _ => double.NaN,
    };

    /// <summary>
    ///     Draws a <c>participant</c> (Mermaid's <c>drawActorTypeParticipant</c>): a rounded box with the label centered,
    ///     lowered with its lifeline from the bottom of the box.
    /// </summary>
    /// <param name="actor">The participant.</param>
    /// <param name="isFooter">Whether the mirrored participant at the bottom is drawn.</param>
    /// <returns>The height of the box.</returns>
    private double DrawActorTypeParticipant(SequenceActorLayout actor, bool isFooter)
    {
        var actorY = isFooter ? actor.StopY ?? double.NaN : actor.StartY;
        var center = actor.X + (actor.Width / 2);
        var slot = Lower();
        if (!isFooter)
        {
            AddLifeline(actor, slot, center, actorY + actor.Height);
        }

        // The box: Mermaid's attributes under the .actor rule
        var location = actor.Actor.Location;
        var paint = _styles.Shape(new ShapePaint(ActorRectFill, NoteRectStroke, 1), ".actor");
        var children = new List<SceneItem> { Rect(actor.X, actorY, actor.Width, actor.Height, 3, paint, "actor-rect", null, location) };
        children.AddRange(ByTspan(actor.Description, actor.X, actorY, actor.Width, actor.Height, location));
        slot.Items.Add(new SceneGroup(0, 0, children) { Role = isFooter ? "actor-bottom" : "actor-top", Id = actor.Actor.Name, Location = location });
        return actor.Height;
    }

    /// <summary>
    ///     Draws an <c>actor</c> (Mermaid's <c>drawActorTypeActor</c>): a stick figure with the label below it, its lifeline
    ///     lowered from 80 below its top; the figure is 65 high.
    /// </summary>
    /// <param name="actor">The participant.</param>
    /// <param name="isFooter">Whether the mirrored participant at the bottom is drawn.</param>
    /// <returns>The height of the figure.</returns>
    private double DrawActorTypeActor(SequenceActorLayout actor, bool isFooter)
    {
        const double FigureHeight = 65;
        var actorY = isFooter ? actor.StopY ?? double.NaN : actor.StartY;
        var center = actor.X + (actor.Width / 2);
        var lowered = Lower();
        if (!isFooter)
        {
            AddLifeline(actor, lowered, center, actorY + 80);
        }

        // The figure's lines and head: the stroke of the group's style, the width of the "line" and ".actor-man circle" rules
        var location = actor.Actor.Location;
        var border = _config.ThemeVariable("actorBorder") is { } text && CssColor.TryParse(text, out var parsed) ? parsed : (Rgba32?)null;
        var linePaint = _styles.Line(new StrokePaint(border, 1, []), "line");
        var circlePaint = _styles.Shape(new ShapePaint(Black, border, 1), ".actor-man circle");
        const double HalfWidth = SequenceLayout.ActorTypeWidth / 2;
        ScenePath Limb(double x1, double y1, double x2, double y2) =>
            new(LineSegments(x1, y1, x2, y2)) { Role = "actor-line", Location = location, Stroke = Stroke(linePaint) };
        var children = new List<SceneItem>
        {
            Limb(center, actorY + 25, center, actorY + 45),
            Limb(center - HalfWidth, actorY + 33, center + HalfWidth, actorY + 33),
            Limb(center - HalfWidth, actorY + 60, center, actorY + 45),
            Limb(center, actorY + 45, center + HalfWidth - 2, actorY + 60),
            new ScenePath(CircleSegments(actor.X + (actor.Width / 2), actorY + 10, 15))
            {
                Role = "actor-head",
                Location = location,
                Fill = circlePaint.Fill,
                Stroke = Stroke(circlePaint),
            },
        };

        // The figure's height is its box: the head's top 5 above the top to the feet 60 below it
        actor.Height = FigureHeight;
        children.AddRange(ByTspan(actor.Description, actor.X, actorY + 35, actor.Width, FigureHeight, location));
        Append().Items.Add(new SceneGroup(0, 0, children) { Role = isFooter ? "actor-bottom" : "actor-top", Id = actor.Actor.Name, Location = location });
        return actor.Height;
    }

    /// <summary>
    ///     Adds the lifeline of a participant, drawn from a point to y 2000 until its end is fixed.
    /// </summary>
    /// <param name="actor">The participant.</param>
    /// <param name="slot">The lowered place of the lifeline.</param>
    /// <param name="x">The x.</param>
    /// <param name="y1">The top.</param>
    private void AddLifeline(SequenceActorLayout actor, SceneSlot slot, double x, double y1) =>
        _lifelines[actor.Actor.Id] = new Lifeline(slot, actor.Actor.Name, x, y1, actor.Actor.Location);
}
