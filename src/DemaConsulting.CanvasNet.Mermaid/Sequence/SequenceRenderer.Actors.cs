// Portions derived from mermaid (MIT); see third-party/README.md
using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Sequence;

/// <content>
///     Participants: Mermaid's <c>drawActor</c> for the eight participant types, at the top (with the lifeline) and
///     mirrored at the bottom. The box types (<c>participant</c>, <c>collections</c>, <c>queue</c>, <c>database</c>) are
///     lowered with their lifelines; the figure types (<c>actor</c>, <c>boundary</c>, <c>control</c>, <c>entity</c>)
///     lower only their lifelines and are appended.
/// </content>
internal sealed partial class SequenceRenderer
{
    /// <summary>The radius of the circle of a <c>boundary</c>, <c>control</c> or <c>entity</c>.</summary>
    private const double FigureRadius = 22;

    /// <summary>The height of the figure of a <c>boundary</c>, <c>control</c> or <c>entity</c> (its circle, as <c>getBBox</c> measures it).</summary>
    private const double FigureCircleHeight = 2 * FigureRadius;

    /// <summary>The offset of the stacked rectangle of a <c>collections</c> participant.</summary>
    private const double CollectionsOffset = 6;

    /// <summary>The shape of the <c>filled-head-control</c> marker of a <c>control</c>.</summary>
    private static readonly (double X, double Y)[] ControlHead = [(14.4, 5.6), (7.2, 10.4), (8.8, 5.6), (7.2, 0.8)];

    /// <summary>The number of the last participant drawn at the top (Mermaid's <c>actorCnt</c>, which starts at -1).</summary>
    private int _actorCnt = -1;

    /// <summary>
    ///     Draws a participant (Mermaid's <c>drawActor</c>).
    /// </summary>
    /// <param name="actor">The participant.</param>
    /// <param name="isFooter">Whether the mirrored participant at the bottom is drawn.</param>
    /// <returns>The height of the drawing, or NaN for a type that draws nothing (Mermaid's <c>undefined</c>).</returns>
    /// <exception cref="MermaidRenderException">Thrown for a <c>properties</c> icon that is not text, on which Mermaid fails.</exception>
    public double DrawActor(SequenceActorLayout actor, bool isFooter) => actor.Actor.Type switch
    {
        SequenceParticipantTypes.Actor => DrawActorTypeActor(actor, isFooter),
        SequenceParticipantTypes.Participant => DrawActorTypeParticipant(actor, isFooter),
        SequenceParticipantTypes.Boundary => DrawActorTypeBoundary(actor, isFooter),
        SequenceParticipantTypes.Control => DrawActorTypeControl(actor, isFooter),
        SequenceParticipantTypes.Entity => DrawActorTypeEntity(actor, isFooter),
        SequenceParticipantTypes.Database => DrawActorTypeDatabase(actor, isFooter),
        SequenceParticipantTypes.Collections => DrawActorTypeCollections(actor, isFooter),
        SequenceParticipantTypes.Queue => DrawActorTypeQueue(actor, isFooter),
        _ => double.NaN,
    };

    /// <summary>
    ///     Gets whether a JSON value is truthy as JavaScript tests it.
    /// </summary>
    /// <param name="value">The value (null for JSON <c>null</c>).</param>
    /// <returns>Whether it is truthy.</returns>
    internal static bool IsTruthy(JsonNode? value) => value switch
    {
        null => false,
        JsonValue v when v.TryGetValue<string>(out var text) => text.Length > 0,
        JsonValue v when v.TryGetValue<bool>(out var flag) => flag,
        JsonValue v when v.TryGetValue<double>(out var number) => number != 0 && !double.IsNaN(number),
        _ => true,
    };

    /// <summary>
    ///     Gets the text JavaScript's <c>String()</c> gives a JSON value.
    /// </summary>
    /// <param name="value">The value (null for JSON <c>null</c>).</param>
    /// <returns>The text.</returns>
    internal static string JsString(JsonNode? value) => value switch
    {
        null => "null",
        JsonValue v when v.TryGetValue<string>(out var text) => text,
        JsonValue v when v.TryGetValue<bool>(out var flag) => flag ? "true" : "false",
        JsonValue v when v.TryGetValue<double>(out var number) => ColorMath.FormatNumber(number),
        JsonArray array => string.Join(',', array.Select(item => item is null ? string.Empty : JsString(item))),
        _ => "[object Object]",
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
        var slot = Lower();
        if (!isFooter)
        {
            Count(actor);
            AddLifeline(actor, slot, actor.X + (actor.Width / 2), actorY + actor.Height);
        }

        // The box: its attributes under the rules of its classes (.actor, or the properties class)
        var location = actor.Actor.Location;
        var (classes, fill) = BoxClasses(actor, isFooter);
        var paint = _styles.Shape(new ShapePaint(fill, NoteRectStroke, 1), [.. ClassSelectors(classes)]);
        var children = new List<SceneItem> { Rect(actor.X, actorY, actor.Width, actor.Height, 3, paint, "actor-rect", null, location) };
        actor.RectData = new SequenceRectData(actor.X, actor.Height, actor.Width, 3, classes, fill);
        CheckIcon(actor);
        children.AddRange(ActorText(actor.Description, actor.X, actorY, actor.Width, actor.Height, LabelStyle, location));
        slot.Items.Add(new SceneGroup(0, 0, children) { Role = Side(isFooter), Id = actor.Actor.Name, Location = location });
        return actor.Height;
    }

    /// <summary>
    ///     Draws a <c>collections</c> participant (Mermaid's <c>drawActorTypeCollections</c>): a box with a second box
    ///     drawn over it 6 pixels down and left, the label centered on the second, lowered with its lifeline.
    /// </summary>
    /// <param name="actor">The participant.</param>
    /// <param name="isFooter">Whether the mirrored participant at the bottom is drawn.</param>
    /// <returns>The height of the box.</returns>
    private double DrawActorTypeCollections(SequenceActorLayout actor, bool isFooter)
    {
        var actorY = isFooter ? actor.StopY ?? double.NaN : actor.StartY;
        var slot = Lower();
        if (!isFooter)
        {
            Count(actor);
            AddLifeline(actor, slot, actor.X + (actor.Width / 2), actorY + actor.Height);
        }

        // The box with its classes, then the stacked box (always of the class actor) over it
        var location = actor.Actor.Location;
        var (classes, fill) = BoxClasses(actor, isFooter);
        var paint = _styles.Shape(new ShapePaint(fill, NoteRectStroke, 1), [.. ClassSelectors(classes)]);
        var stacked = _styles.Shape(new ShapePaint(fill, NoteRectStroke, 1), ".actor");
        var children = new List<SceneItem>
        {
            Rect(actor.X, actorY, actor.Width, actor.Height, 0, paint, "actor-rect", null, location),
            Rect(actor.X - CollectionsOffset, actorY + CollectionsOffset, actor.Width, actor.Height, 0, stacked, "actor-rect", null, location),
        };
        actor.RectData = new SequenceRectData(actor.X, actor.Height, actor.Width, 0, classes, fill);
        CheckIcon(actor);
        children.AddRange(ActorText(actor.Description, actor.X - CollectionsOffset, actorY + CollectionsOffset, actor.Width, actor.Height, LabelStyle, location));
        slot.Items.Add(new SceneGroup(0, 0, children) { Role = Side(isFooter), Id = actor.Actor.Name, Location = location });
        return actor.Height;
    }

    /// <summary>
    ///     Draws a <c>queue</c> participant (Mermaid's <c>drawActorTypeQueue</c>): a horizontal cylinder of the box's size
    ///     with its right end drawn again, the label centered, lowered with its lifeline.
    /// </summary>
    /// <param name="actor">The participant.</param>
    /// <param name="isFooter">Whether the mirrored participant at the bottom is drawn.</param>
    /// <returns>The height of the cylinder.</returns>
    private double DrawActorTypeQueue(SequenceActorLayout actor, bool isFooter)
    {
        var actorY = isFooter ? actor.StopY ?? double.NaN : actor.StartY;
        var slot = Lower();
        if (!isFooter)
        {
            Count(actor);
            AddLifeline(actor, slot, actor.X + (actor.Width / 2), actorY + actor.Height);
        }

        // The cylinder: the ends are half ellipses as high as the box; the paths inherit the group's classes
        var location = actor.Actor.Location;
        var (classes, fill) = BoxClasses(actor, isFooter);
        var paint = _styles.Shape(new ShapePaint(RootFill, null, 1), [.. ClassSelectors(classes)]);
        var (x, width, height) = (actor.X, actor.Width, actor.Height);
        var ry = height / 2;
        var rx = ry / (2.5 + (height / 50));
        ScenePath Part(IReadOnlyList<SceneSegment> segments) =>
            new(segments) { Role = "actor-cylinder", Location = location, Fill = paint.Fill, Stroke = Stroke(paint) };
        var children = new List<SceneItem>
        {
            Part(
            [
                SceneSegment.Move(x + rx, actorY),
                SceneSegment.Arc(rx, ry, 0, false, false, x + rx, actorY + height),
                SceneSegment.Line(x + width - rx, actorY + height),
                SceneSegment.Arc(rx, ry, 0, false, false, x + width - rx, actorY),
                SceneSegment.Close(),
            ]),
            Part(
            [
                SceneSegment.Move(x + width - rx, actorY),
                SceneSegment.Arc(rx, ry, 0, false, false, x + width - rx, actorY + height),
            ]),
        };

        // Mermaid's box of a queue has no class, so its menu panel gets none
        actor.RectData = new SequenceRectData(x, height, width, 0, string.Empty, fill);
        CheckIcon(actor);
        children.AddRange(ActorText(actor.Description, x, actorY, width, height, LabelStyle, location));
        slot.Items.Add(new SceneGroup(0, 0, children) { Role = Side(isFooter), Id = actor.Actor.Name, Location = location });
        return height;
    }

    /// <summary>
    ///     Draws a <c>database</c> participant (Mermaid's <c>drawActorTypeDatabase</c>): an upright cylinder a third of
    ///     the box's width, with the label below it, lowered with its lifeline from below the label's box; the height is
    ///     the cylinder's plus <c>labelBoxHeight</c>.
    /// </summary>
    /// <param name="actor">The participant.</param>
    /// <param name="isFooter">Whether the mirrored participant at the bottom is drawn.</param>
    /// <returns>The new height of the participant.</returns>
    private double DrawActorTypeDatabase(SequenceActorLayout actor, bool isFooter)
    {
        var actorY = isFooter ? actor.StopY ?? double.NaN : actor.StartY;
        var slot = Lower();
        if (!isFooter)
        {
            Count(actor);
            AddLifeline(actor, slot, actor.X + (actor.Width / 2), actorY + actor.Height + (2 * _conf.BoxTextMargin));
        }

        // The cylinder: its classes paint it, and its group's inline style strokes it with the participant border
        var location = actor.Actor.Location;
        var (classes, fill) = BoxClasses(actor, isFooter);
        var rule = _styles.Shape(new ShapePaint(RootFill, null, 1), [.. ClassSelectors(classes)]);
        var paint = rule with { Stroke = ThemeColor("actorBorder") ?? rule.Stroke };
        var w = actor.Width / 3;
        var h = actor.Width / 3;
        var rx = w / 2;
        var ry = rx / (2.5 + (w / 50));
        var (left, top) = (actor.X + w, actorY + (2 * ry));
        var children = new List<SceneItem>
        {
            new ScenePath(
            [
                SceneSegment.Move(left, top),
                SceneSegment.Arc(rx, ry, 0, false, false, left + w, top),
                SceneSegment.Arc(rx, ry, 0, false, false, left, top),
                SceneSegment.Line(left, top + h - (2 * ry)),
                SceneSegment.Arc(rx, ry, 0, false, false, left + w, top + h - (2 * ry)),
                SceneSegment.Line(left + w, top),
            ])
            {
                Role = "actor-cylinder",
                Location = location,
                Fill = paint.Fill,
                Stroke = Stroke(paint),
            },
        };

        // The label below the cylinder in the box's former height, which then becomes the cylinder's and a label box
        actor.RectData = new SequenceRectData(actor.X, actor.Height, actor.Width, 0, classes, fill);
        children.AddRange(ActorText(actor.Description, actor.X, actorY + 35, actor.Width, actor.Height, LabelStyle, location));
        actor.Height = h + _conf.LabelBoxHeight;
        slot.Items.Add(new SceneGroup(0, 0, children) { Role = Side(isFooter), Id = actor.Actor.Name, Location = location });
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
            Count(actor);
            AddLifeline(actor, lowered, center, actorY + 80);
        }

        // The figure's lines and head: the stroke of the group's style, the width of the "line" and ".actor-man circle" rules
        var location = actor.Actor.Location;
        var border = ThemeColor("actorBorder");
        var linePaint = _styles.Line(new StrokePaint(border, 1, []), "line");
        var circlePaint = _styles.Shape(new ShapePaint(RootFill, border, 1), ".actor-man circle");
        const double HalfWidth = SequenceLayout.ActorTypeWidth / 2;
        var children = new List<SceneItem>
        {
            Limb(center, actorY + 25, center, actorY + 45, linePaint, location),
            Limb(center - HalfWidth, actorY + 33, center + HalfWidth, actorY + 33, linePaint, location),
            Limb(center - HalfWidth, actorY + 60, center, actorY + 45, linePaint, location),
            Limb(center, actorY + 45, center + HalfWidth - 2, actorY + 60, linePaint, location),
            Circle(center, actorY + 10, 15, circlePaint, location),
        };

        // The figure's height is its box: the head's top 5 above the top to the feet 60 below it
        actor.Height = FigureHeight;
        actor.RectData = null;
        children.AddRange(ActorText(actor.Description, actor.X, actorY + 35, actor.Width, FigureHeight, LabelStyle, location));
        Append().Items.Add(new SceneGroup(0, 0, children) { Role = Side(isFooter), Id = actor.Actor.Name, Location = location });
        return actor.Height;
    }

    /// <summary>
    ///     Draws a <c>boundary</c> (Mermaid's <c>drawActorTypeBoundary</c>): a circle with a T bar on its left, moved 21
    ///     pixels down with the label below it; its lifeline is lowered from 80 below its top, and its height is the
    ///     figure's plus <c>labelBoxHeight</c>.
    /// </summary>
    /// <param name="actor">The participant.</param>
    /// <param name="isFooter">Whether the mirrored participant at the bottom is drawn.</param>
    /// <returns>The new height of the participant.</returns>
    private double DrawActorTypeBoundary(SequenceActorLayout actor, bool isFooter)
    {
        var actorY = isFooter ? actor.StopY ?? double.NaN : actor.StartY;
        var center = actor.X + (actor.Width / 2);
        var lowered = Lower();
        if (!isFooter)
        {
            Count(actor);
            AddLifeline(actor, lowered, center, actorY + 80);
        }

        // The figure, moved down by half the radius and 10 (the group's transform)
        var location = actor.Actor.Location;
        var y = actorY + (FigureRadius / 2) + 10;
        var border = ThemeColor("actorBorder");
        var linePaint = _styles.Line(new StrokePaint(border, 1, []), "line");
        var circlePaint = _styles.Shape(new ShapePaint(RootFill, border, 1), ".actor-man circle");
        var bar = center - (FigureRadius * 2.5);
        var children = new List<SceneItem>
        {
            Limb(bar, y + 12, center - 15, y + 12, linePaint, location),
            Limb(bar, y + 2, bar, y + 22, linePaint, location),
            Circle(center, y + 12, FigureRadius, circlePaint, location),
        };

        // The label in the former height, which then becomes the figure's and a label box
        var labelHeight = actor.Height;
        actor.Height = FigureCircleHeight + _conf.LabelBoxHeight;
        actor.RectData = null;
        children.AddRange(ActorText(actor.Description, actor.X, y + 15, actor.Width, labelHeight, LabelStyle, location));
        Append().Items.Add(new SceneGroup(0, 0, children) { Role = Side(isFooter), Id = actor.Actor.Name, Location = location });
        return actor.Height;
    }

    /// <summary>
    ///     Draws a <c>control</c> (Mermaid's <c>drawActorTypeControl</c>): a circle with the <c>filled-head-control</c>
    ///     arrowhead on its top, with the label below it; its lifeline is lowered from 75 below its top, and its height is
    ///     the circle's plus twice <c>labelBoxHeight</c>.
    /// </summary>
    /// <param name="actor">The participant.</param>
    /// <param name="isFooter">Whether the mirrored participant at the bottom is drawn.</param>
    /// <returns>The new height of the participant.</returns>
    private double DrawActorTypeControl(SequenceActorLayout actor, bool isFooter)
    {
        var actorY = isFooter ? actor.StopY ?? double.NaN : actor.StartY;
        var center = actor.X + (actor.Width / 2);
        var lowered = Lower();
        if (!isFooter)
        {
            Count(actor);
            AddLifeline(actor, lowered, center, actorY + 75);
        }

        // The circle, and the zero-length line at its top that carries the arrowhead; the group's style strokes and
        // fills them with the participant colors
        var location = actor.Actor.Location;
        var cy = actorY + 32;
        var border = ThemeColor("actorBorder");
        var background = ThemeColor("actorBkg");
        var circlePaint = _styles.Shape(new ShapePaint(background, border, 1), ".actor-man circle");
        var linePaint = _styles.Line(new StrokePaint(border, 1, []), "line");
        var children = new List<SceneItem>
        {
            Circle(center, cy, FigureRadius, circlePaint, location),
            Limb(center, cy - FigureRadius, center, cy - FigureRadius, linePaint, location),
            ControlMarker(center, cy - FigureRadius, linePaint.Width, background, border, location),
        };

        // The label in the former height, which then becomes the circle's and two label boxes
        var labelHeight = actor.Height;
        actor.Height = FigureCircleHeight + (2 * _conf.LabelBoxHeight);
        actor.RectData = null;
        children.AddRange(ActorText(actor.Description, actor.X, actorY + FigureRadius + (isFooter ? 5 : 12), actor.Width, labelHeight, LabelStyle, location));
        Append().Items.Add(new SceneGroup(0, 0, children) { Role = Side(isFooter), Id = actor.Actor.Name, Location = location });
        return actor.Height;
    }

    /// <summary>
    ///     Draws an <c>entity</c> (Mermaid's <c>drawActorTypeEntity</c>): a circle on a line, painted by the
    ///     <c>.actor</c> rule, with the label below it, moved down by 6 pixels at the top and 22 at the bottom; its
    ///     lifeline is lowered from 75 below its top, and its height is the circle's plus <c>labelBoxHeight</c>.
    /// </summary>
    /// <param name="actor">The participant.</param>
    /// <param name="isFooter">Whether the mirrored participant at the bottom is drawn.</param>
    /// <returns>The new height of the participant.</returns>
    private double DrawActorTypeEntity(SequenceActorLayout actor, bool isFooter)
    {
        var actorY = isFooter ? actor.StopY ?? double.NaN : actor.StartY;
        var center = actor.X + (actor.Width / 2);
        var lowered = Lower();
        var location = actor.Actor.Location;

        // The figure inherits the paint of its group's .actor class; the line's width is its attribute and the line rule
        var shift = isFooter ? FigureRadius : (FigureRadius / 2) - 5;
        var cy = actorY + (isFooter ? 10 : 25) + shift;
        var paint = _styles.Shape(new ShapePaint(RootFill, null, 1), [.. ClassSelectors("actor " + (isFooter ? "actor-bottom" : "actor-top"))]);
        var linePaint = _styles.Line(new StrokePaint(paint.Stroke, 2, []), "line");
        var children = new List<SceneItem>
        {
            Circle(center, cy, FigureRadius, paint, location),
            Limb(center - FigureRadius, cy + FigureRadius, center + FigureRadius, cy + FigureRadius, linePaint, location),
        };

        // The height, then the lifeline (Mermaid counts the participant here), then the label in the former height
        var labelHeight = actor.Height;
        actor.Height = FigureCircleHeight + _conf.LabelBoxHeight;
        if (!isFooter)
        {
            Count(actor);
            AddLifeline(actor, lowered, center, actorY + 75);
        }

        actor.RectData = null;
        children.AddRange(ActorText(actor.Description, actor.X, actorY + (isFooter ? 15 : 30) + shift, actor.Width, labelHeight, LabelStyle, location));
        Append().Items.Add(new SceneGroup(0, 0, children) { Role = Side(isFooter), Id = actor.Actor.Name, Location = location });
        return actor.Height;
    }

    /// <summary>Gets the paint and role of a participant label in a box (its text of the classes <c>actor actor-box</c> or <c>actor actor-man</c>).</summary>
    private ActorTextStyle LabelStyle => new(_styles.TextColor(null, "text.actor > tspan"), ".actor", "actor-text");

    /// <summary>
    ///     Gets the role of a participant drawing.
    /// </summary>
    /// <param name="isFooter">Whether it is the mirrored drawing.</param>
    /// <returns>The role.</returns>
    private static string Side(bool isFooter) => isFooter ? "actor-bottom" : "actor-top";

    /// <summary>
    ///     Gets a color of the theme, or null when it is not set or not a color.
    /// </summary>
    /// <param name="name">The theme variable.</param>
    /// <returns>The color, or null.</returns>
    private Rgba32? ThemeColor(string name) =>
        _config.ThemeVariable(name) is { } text && CssColor.TryParse(text, out var color) ? color : null;

    /// <summary>
    ///     Gets the classes and fill attribute of a participant's box: <c>actor</c> and <c>#eaeaea</c>, or the
    ///     <c>properties</c> class (its JavaScript text) and the note fill <c>#EDF2AE</c>, then the side's class.
    /// </summary>
    /// <param name="actor">The participant.</param>
    /// <param name="isFooter">Whether the mirrored drawing is drawn.</param>
    /// <returns>The classes and the fill.</returns>
    private static (string Classes, Rgba32 Fill) BoxClasses(SequenceActorLayout actor, bool isFooter)
    {
        var side = isFooter ? " actor-bottom" : " actor-top";
        return actor.Actor.Properties.TryGetValue("class", out var value) && IsTruthy(value)
            ? (JsString(value) + side, NoteRectFill)
            : ("actor" + side, ActorRectFill);
    }

    /// <summary>
    ///     Checks the <c>properties</c> icon of a box participant: Mermaid trims it as text, so an icon that is not text
    ///     makes it fail. A text icon is reported by the model and not drawn.
    /// </summary>
    /// <param name="actor">The participant.</param>
    /// <exception cref="MermaidRenderException">Thrown for an icon that is not text.</exception>
    private static void CheckIcon(SequenceActorLayout actor)
    {
        if (actor.Actor.Properties.TryGetValue("icon", out var icon) && IsTruthy(icon) && !(icon is JsonValue value && value.TryGetValue<string>(out _)))
        {
            throw new MermaidRenderException(
                MermaidDiagnosticCodes.RenderFailed,
                $"the icon of participant '{actor.Actor.Name}' is not text, and Mermaid fails to draw it",
                actor.Actor.Location,
                "write the icon as a string, or remove it");
        }
    }

    /// <summary>
    ///     Numbers a participant drawn at the top (Mermaid's <c>actorCnt</c>).
    /// </summary>
    /// <param name="actor">The participant.</param>
    private void Count(SequenceActorLayout actor) => actor.ActorCnt = ++_actorCnt;

    /// <summary>
    ///     Makes a line of a figure.
    /// </summary>
    /// <param name="x1">The start's x.</param>
    /// <param name="y1">The start's y.</param>
    /// <param name="x2">The end's x.</param>
    /// <param name="y2">The end's y.</param>
    /// <param name="paint">The stroke.</param>
    /// <param name="location">The location of the participant.</param>
    /// <returns>The path.</returns>
    private static ScenePath Limb(double x1, double y1, double x2, double y2, StrokePaint paint, MermaidLocation location) =>
        new(LineSegments(x1, y1, x2, y2)) { Role = "actor-line", Location = location, Stroke = Stroke(paint) };

    /// <summary>
    ///     Makes a circle of a figure.
    /// </summary>
    /// <param name="cx">The center's x.</param>
    /// <param name="cy">The center's y.</param>
    /// <param name="r">The radius.</param>
    /// <param name="paint">The paint.</param>
    /// <param name="location">The location of the participant.</param>
    /// <returns>The path.</returns>
    private static ScenePath Circle(double cx, double cy, double r, ShapePaint paint, MermaidLocation location) =>
        new(CircleSegments(cx, cy, r)) { Role = "actor-head", Location = location, Fill = paint.Fill, Stroke = Stroke(paint) };

    /// <summary>
    ///     Draws the <c>filled-head-control</c> marker of a <c>control</c> at the end of its zero-length line: the arrowhead
    ///     turned by 172.5 degrees and scaled by the line's width (its viewport does not clip it), filled and stroked
    ///     (1.2 wide in marker units) with the figure group's colors, which the marker's content inherits.
    /// </summary>
    /// <param name="x">The x of the line's end.</param>
    /// <param name="y">The y of the line's end.</param>
    /// <param name="scale">The width of the line (the marker units).</param>
    /// <param name="fill">The fill.</param>
    /// <param name="stroke">The stroke color.</param>
    /// <param name="location">The location of the participant.</param>
    /// <returns>The marker's path.</returns>
    private static ScenePath ControlMarker(double x, double y, double scale, Rgba32? fill, Rgba32? stroke, MermaidLocation location)
    {
        const double RefX = 11;
        const double RefY = 5.8;
        const double Orient = 172.5;
        var (sin, cos) = Math.SinCos(Orient * Math.PI / 180);
        var segments = new List<SceneSegment>();
        foreach (var (px, py) in ControlHead)
        {
            var (dx, dy) = ((px - RefX) * scale, (py - RefY) * scale);
            var point = (X: x + (dx * cos) - (dy * sin), Y: y + (dx * sin) + (dy * cos));
            segments.Add(segments.Count == 0 ? SceneSegment.Move(point.X, point.Y) : SceneSegment.Line(point.X, point.Y));
        }

        segments.Add(SceneSegment.Close());
        return new ScenePath(segments)
        {
            Role = "actor-marker",
            Id = "filled-head-control",
            Location = location,
            Fill = fill,
            Stroke = stroke is { } color ? new SceneStroke(color, 1.2 * scale) : null,
        };
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
