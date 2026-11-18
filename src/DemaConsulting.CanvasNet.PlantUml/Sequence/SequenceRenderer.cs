// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;

namespace DemaConsulting.CanvasNet.PlantUml.Sequence;

/// <summary>
///     Draws the geometry of a sequence diagram as a scene, with PlantUML's shapes and the theme's colors.
/// </summary>
/// <remarks>
///     <para>
///         The paint order follows PlantUML: boxes, group fills, life lines, activation boxes, heads, tails, and then
///         the events in order (messages with their heads and texts, notes, group frames with their header tabs and
///         sections, dividers, delay texts, and destroy crosses). Every item carries a role (<c>participant-head</c>,
///         <c>participant-tail</c>, <c>lifeline</c>, <c>activation</c>, <c>message</c>, <c>message-head</c>,
///         <c>message-text</c>, <c>note</c>, <c>note-text</c>, <c>group</c>, <c>group-header</c>, <c>group-text</c>,
///         <c>divider</c>, <c>divider-text</c>, <c>delay-text</c>, <c>destroy</c>, <c>box</c>, <c>box-text</c>) and,
///         for participants, the participant's code as its id.
///     </para>
///     <para>
///         The class is stateless and safe for concurrent use.
///     </para>
/// </remarks>
internal static partial class SequenceRenderer
{
    /// <summary>The color of the cross of a destroyed life line in PlantUML's default skin.</summary>
    private static readonly Rgba32 DestroyColor = new(0xA8, 0x00, 0x36, 255);

    /// <summary>
    ///     Draws a sequence diagram.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="geometry">The geometry.</param>
    /// <returns>The scene of the body (without page texts), with its view box at the origin.</returns>
    public static Scene Render(SequenceModel model, SequenceGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(geometry);
        var theme = model.Theme;
        var items = new List<SceneItem>();
        DrawBoxes(geometry, theme, items);
        DrawGroupFills(geometry, items);
        DrawLifelines(geometry, theme, items);
        DrawActivations(geometry, theme, items);
        foreach (var participant in geometry.Participants)
        {
            DrawParticipant(participant, participant.HeadBottom - participant.PrefHeight, true, items);
        }

        foreach (var participant in geometry.Participants.Where(p => !double.IsNaN(p.TailTop)))
        {
            DrawParticipant(participant, participant.TailTop, false, items);
        }

        foreach (var item in geometry.Items)
        {
            DrawItem(item, theme, items);
        }

        var moved = items.Select(item => ScenePage.Translate(item, geometry.OffsetX, 0)).ToList();
        return new Scene(new SceneRect(0, 0, geometry.Width, geometry.Height), moved) { Background = theme.Background };
    }

    /// <summary>
    ///     Draws one placed event.
    /// </summary>
    /// <param name="item">The event.</param>
    /// <param name="theme">The theme.</param>
    /// <param name="items">The scene items.</param>
    private static void DrawItem(PlacedItem item, SkinTheme theme, List<SceneItem> items)
    {
        switch (item)
        {
            case PlacedMessage message:
                DrawMessage(message, theme, items);
                break;
            case PlacedSelf self:
                DrawSelf(self, theme, items);
                break;
            case PlacedNote note:
                DrawNote(note, theme, items);
                break;
            case PlacedGroup group:
                DrawGroup(group, theme, items);
                break;
            case PlacedDivider divider:
                DrawDivider(divider, theme, items);
                break;
            case PlacedDelay delay:
                items.AddRange(delay.Text.Draw(delay.X, delay.Top, TextAlign.Center, "delay-text", delay.Location));
                break;
            case PlacedDestroy destroy:
                DrawDestroy(destroy, items);
                break;
        }
    }

    /// <summary>
    ///     Creates a stroke from a style, or null when the style has no line.
    /// </summary>
    /// <param name="style">The style.</param>
    /// <returns>The stroke.</returns>
    private static SceneStroke? StrokeOf(ElementStyle style) =>
        style.Line is { } line && style.LineWidth > 0 ? new SceneStroke(line, style.LineWidth) { Dash = style.Dash } : null;

    /// <summary>
    ///     Draws the boxes around participants.
    /// </summary>
    /// <param name="geometry">The geometry.</param>
    /// <param name="theme">The theme.</param>
    /// <param name="items">The scene items.</param>
    private static void DrawBoxes(SequenceGeometry geometry, SkinTheme theme, List<SceneItem> items)
    {
        var style = theme[SkinElement.Box];
        foreach (var box in geometry.Boxes)
        {
            items.Add(new ScenePath(SceneShapes.Rect(box.X, box.Y, box.Width, box.Height))
            {
                Role = "box",
                Fill = box.Box.Fill ?? style.Fill,
                Stroke = StrokeOf(style),
                Location = box.Box.Location,
            });
            if (box.Title is { } title)
            {
                items.AddRange(title.Draw(box.X + ((box.Width - title.Width) / 2), box.Y, TextAlign.Center, "box-text", box.Box.Location));
            }
        }
    }

    /// <summary>
    ///     Draws the fills of the group frames that have one, under everything else.
    /// </summary>
    /// <param name="geometry">The geometry.</param>
    /// <param name="items">The scene items.</param>
    private static void DrawGroupFills(SequenceGeometry geometry, List<SceneItem> items)
    {
        foreach (var group in geometry.Items.OfType<PlacedGroup>().Where(g => g.Start.Fill is not null))
        {
            items.Add(new ScenePath(SceneShapes.Rect(group.X, group.Y, group.Width, group.Height))
            {
                Role = "group-fill",
                Fill = group.Start.Fill,
                Location = group.Start.Location,
            });
        }
    }

    /// <summary>
    ///     Draws the life lines: dashed, and dotted in delays.
    /// </summary>
    /// <param name="geometry">The geometry.</param>
    /// <param name="theme">The theme.</param>
    /// <param name="items">The scene items.</param>
    private static void DrawLifelines(SequenceGeometry geometry, SkinTheme theme, List<SceneItem> items)
    {
        var line = theme[SkinElement.LifeLine];
        var delay = theme[SkinElement.Delay];
        foreach (var part in geometry.Lifelines.Where(l => l.Y2 > l.Y1))
        {
            var stroke = part.Delay
                ? new SceneStroke(line.Line ?? SkinTheme.LineColor, delay.LineWidth) { Dash = delay.Dash }
                : StrokeOf(line);
            items.Add(new ScenePath(SceneShapes.Line(part.X, part.Y1, part.X, part.Y2)) { Role = "lifeline", Id = part.Participant, Stroke = stroke });
        }
    }

    /// <summary>
    ///     Draws the activation boxes.
    /// </summary>
    /// <param name="geometry">The geometry.</param>
    /// <param name="theme">The theme.</param>
    /// <param name="items">The scene items.</param>
    private static void DrawActivations(SequenceGeometry geometry, SkinTheme theme, List<SceneItem> items)
    {
        var style = theme[SkinElement.ActivationBox];
        foreach (var box in geometry.Activations)
        {
            items.Add(new ScenePath(SceneShapes.Rect(box.X, box.Y1, 10, box.Y2 - box.Y1))
            {
                Role = "activation",
                Id = box.Participant,
                Fill = box.Fill ?? style.Fill,
                Stroke = StrokeOf(style),
            });
        }
    }

    /// <summary>
    ///     Draws the cross of a destroyed life line.
    /// </summary>
    /// <param name="destroy">The cross.</param>
    /// <param name="items">The scene items.</param>
    private static void DrawDestroy(PlacedDestroy destroy, List<SceneItem> items)
    {
        var stroke = new SceneStroke(DestroyColor, 2);
        var (x, y) = (destroy.X, destroy.Y);
        items.Add(new ScenePath([.. SceneShapes.Line(x - 9, y - 9, x + 9, y + 9), .. SceneShapes.Line(x - 9, y + 9, x + 9, y - 9)])
        {
            Role = "destroy",
            Stroke = stroke,
            Location = destroy.Location,
        });
    }
}
