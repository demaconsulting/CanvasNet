// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;

namespace DemaConsulting.CanvasNet.PlantUml.Sequence;

/// <content>
///     The rows: messages, self messages, notes, and life changes, and the assembly of the geometry.
/// </content>
internal sealed partial class SequenceLayout
{
    /// <summary>
    ///     Places the heads, the rows of the events, the life lines, the tails, and the boxes.
    /// </summary>
    /// <param name="positions">The x of every life line.</param>
    /// <returns>The geometry.</returns>
    private SequenceGeometry PlaceRows(double[] positions)
    {
        var n = _columns.Length;
        var titles = _model.Boxes.Select(b => b.Title is null ? null : Text(b.Title, Theme[SkinElement.Box], b.Location)).ToArray();
        var titleHeight = titles.Length == 0 ? 0 : titles.Max(t => t?.Height ?? 0);
        var headTop = _model.Boxes.Count == 0 ? Margin : Margin + 1 + titleHeight + 4;
        var headHeight = n == 0 ? 0 : _columns.Max(c => c.Height);
        var headBottom = headTop + headHeight;

        var state = new RowState(n) { Y = headBottom + FirstRowGap };
        var (before, after) = BoxEdges();
        for (var p = 0; p < n; p++)
        {
            var half = _columns[p].Width / 2;
            state.Extent(positions[p] - half - before[p], positions[p] + half + after[p]);
        }

        PlaceEvents(positions, state);

        var lifelineEnd = state.Y + LastRowGap;
        CloseActivations(state, positions, lifelineEnd);
        FinishDividers(state);

        var footbox = Theme.Footbox;
        var participants = new List<PlacedParticipant>(n);
        for (var p = 0; p < n; p++)
        {
            var column = _columns[p];
            participants.Add(new PlacedParticipant(
                _model.Participants[p],
                positions[p] - (column.Width / 2),
                positions[p],
                column.Width,
                column.Height,
                column.Text,
                column.Style,
                headBottom,
                footbox ? lifelineEnd : double.NaN));
        }

        var bottom = footbox ? lifelineEnd + headHeight : lifelineEnd;
        var boxes = PlaceBoxes(participants, titles, bottom);
        if (boxes.Count > 0)
        {
            bottom += 10;
            foreach (var box in boxes)
            {
                state.Extent(box.X, box.X + box.Width);
            }
        }

        var lifelines = BuildLifelines(participants, state, headBottom, lifelineEnd);
        var minX = double.IsFinite(state.MinX) ? state.MinX : Margin;
        var maxX = double.IsFinite(state.MaxX) ? state.MaxX : minX;
        return new SequenceGeometry(
            participants,
            lifelines,
            state.Activations,
            state.Items,
            boxes,
            Margin - minX,
            maxX - minX + (2 * Margin),
            bottom + Margin);
    }

    /// <summary>
    ///     Places every event, row after row.
    /// </summary>
    /// <param name="positions">The x of every life line.</param>
    /// <param name="state">The row state.</param>
    private void PlaceEvents(double[] positions, RowState state)
    {
        var events = _model.Events;
        var i = 0;
        while (i < events.Count)
        {
            Tick();
            var info = _info[i];
            switch (events[i])
            {
                case SequenceMessage { IsSelf: true } self:
                    i = PlaceSelf(self, i, positions, state);
                    break;
                case SequenceMessage message:
                    i = PlaceMessage(message, i, positions, state);
                    break;
                case SequenceLifeEvent life:
                    ApplyChange(life.Change, state.Y, positions, state);
                    break;
                case SequenceNote note:
                    var height = PlaceNote(note, info, state.Y, positions, state);
                    state.Y += height;
                    break;
                case SequenceGroupStart start:
                    OpenGroup(start, info, state);
                    break;
                case SequenceGroupElse:
                    PlaceElse(info, state);
                    break;
                case SequenceGroupEnd end:
                    CloseGroup(end, state);
                    break;
                case SequenceDivider divider:
                    PlaceDivider(divider, info, state);
                    break;
                case SequenceDelay delay:
                    PlaceDelay(delay, info, state, positions);
                    break;
                default:
                    state.Y += info.Height;
                    break;
            }

            i++;
        }
    }

    /// <summary>
    ///     Places a message between two participants, with the notes on its row.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="index">Its event index.</param>
    /// <param name="positions">The x of every life line.</param>
    /// <param name="state">The row state.</param>
    /// <returns>The index of the last event placed on the row.</returns>
    private int PlaceMessage(SequenceMessage message, int index, double[] positions, RowState state)
    {
        var info = _info[index];
        var (left, right) = (Math.Min(message.From, message.To), Math.Max(message.From, message.To));
        var toLeft = message.To == left;
        var (levelLeft, levelRight) = toLeft ? (info.LevelTo, info.LevelFrom) : (info.LevelFrom, info.LevelTo);
        var xLeft = positions[left] + (LiveDelta * levelLeft);

        // The arrow meets the innermost activation box: its right edge on the left, its left edge on the right
        var xRight = levelRight > 0 ? positions[right] + (LiveDelta * (levelRight - 2)) : positions[right];
        var top = state.Y;
        var below = Theme.ResponseBelowArrow && message.Arrow.Dotted;
        var arrowY = below ? top + 6 : top + info.TextHeight + 4;
        var textTop = below ? arrowY + 2 : top + 4;
        var leftHead = (toLeft ? message.Arrow.ToHead : message.Arrow.FromHead) != ArrowHead.None;
        var rightHead = (toLeft ? message.Arrow.FromHead : message.Arrow.ToHead) != ArrowHead.None;
        var textX = AlignText(xLeft, xRight, info.TextWidth, (leftHead, rightHead), toLeft);
        state.Items.Add(new PlacedMessage(message, xLeft, xRight, arrowY, toLeft, info.Label!, info.Number, textX, textTop) { Location = message.Location });
        state.Extent(positions[left], positions[right]);
        foreach (var change in message.Changes)
        {
            ApplyChange(change, arrowY, positions, state);
        }

        var height = info.Height;
        var levels = (Left: levelLeft, Right: levelRight);
        while (index + 1 < _model.Events.Count && _model.Events[index + 1] is SequenceNote { OnMessage: true } note)
        {
            index++;
            var side = note.Position == NotePosition.Left ? left : right;
            var noteInfo = _info[index];
            noteInfo.LevelFrom = note.Position == NotePosition.Left ? levels.Left : levels.Right;
            height = Math.Max(height, PlaceNote(note with { First = side, Last = side }, noteInfo, top, positions, state));
        }

        state.Y += height;
        return index;
    }

    /// <summary>
    ///     Gets the x of a message text by the message alignment.
    /// </summary>
    /// <param name="xLeft">The left end of the arrow.</param>
    /// <param name="xRight">The right end of the arrow.</param>
    /// <param name="width">The width of the text.</param>
    /// <param name="heads">Whether the arrow has a head at its left end and at its right end.</param>
    /// <param name="toLeft">Whether the arrow points to the left.</param>
    /// <returns>The x.</returns>
    private double AlignText(double xLeft, double xRight, double width, (bool Left, bool Right) heads, bool toLeft)
    {
        var setting = Theme.MessageAlign;
        var align = TextAlign.Left;
        if (setting.Equals("center", StringComparison.OrdinalIgnoreCase))
        {
            align = TextAlign.Center;
        }
        else if (setting.Equals("right", StringComparison.OrdinalIgnoreCase))
        {
            align = TextAlign.Right;
        }
        else if (setting.Equals("direction", StringComparison.OrdinalIgnoreCase))
        {
            align = toLeft ? TextAlign.Right : TextAlign.Left;
        }
        else if (setting.Equals("reverseDirection", StringComparison.OrdinalIgnoreCase))
        {
            align = toLeft ? TextAlign.Left : TextAlign.Right;
        }

        return align switch
        {
            TextAlign.Center => ((xLeft + xRight) / 2) - (width / 2),
            TextAlign.Right => xRight - (heads.Right ? 17 : 7) - width,
            _ => xLeft + (heads.Left ? 17 : 7),
        };
    }

    /// <summary>
    ///     Places a message from a participant to itself, with the notes on its row.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="index">Its event index.</param>
    /// <param name="positions">The x of every life line.</param>
    /// <param name="state">The row state.</param>
    /// <returns>The index of the last event placed on the row.</returns>
    private int PlaceSelf(SequenceMessage message, int index, double[] positions, RowState state)
    {
        var info = _info[index];
        var p = message.From;
        var startX = positions[p] + (LiveDelta * info.LevelFrom);
        var loopX = positions[p] + (LiveDelta * info.LevelTo);
        var top = state.Y;
        var topY = top + info.TextHeight + 4;
        var bottomY = topY + 13;
        var textX = loopX + 7;
        state.Items.Add(new PlacedSelf(message, startX, loopX, topY, bottomY, info.Label!, info.Number, textX, top + 4) { Location = message.Location });
        state.Extent(positions[p], positions[p] + LoopReach(p, info, _columns.Select(c => c.MaxLevel).ToArray()), Math.Max(loopX + SelfLoop + 3, textX + info.TextWidth));
        foreach (var change in message.Changes)
        {
            ApplyChange(change, bottomY, positions, state);
        }

        var height = info.Height;
        while (index + 1 < _model.Events.Count && _model.Events[index + 1] is SequenceNote { OnMessage: true } note)
        {
            index++;
            var noteInfo = _info[index];
            noteInfo.LevelFrom = info.LevelTo;
            var placedNote = note with { First = p, Last = p };
            if (note.Position == NotePosition.Right)
            {
                // A note right of a self message clears its loop
                noteInfo.LevelFrom = 0;
                var shifted = PlaceNoteAt(placedNote, noteInfo, top, Math.Max(loopX + SelfLoop + 3, textX + info.TextWidth), state);
                height = Math.Max(height, shifted);
                continue;
            }

            height = Math.Max(height, PlaceNote(placedNote, noteInfo, top, positions, state));
        }

        state.Y += height;
        return index;
    }

    /// <summary>
    ///     Places a note on a row.
    /// </summary>
    /// <param name="note">The note (with its participants resolved).</param>
    /// <param name="info">Its measures.</param>
    /// <param name="top">The top of the row.</param>
    /// <param name="positions">The x of every life line.</param>
    /// <param name="state">The row state.</param>
    /// <returns>The height the note needs on the row.</returns>
    private double PlaceNote(SequenceNote note, EventInfo info, double top, double[] positions, RowState state)
    {
        var width = info.TextWidth;
        var (first, last) = (Math.Min(note.First, note.Last), Math.Max(note.First, note.Last));
        double x;
        var used = width;
        switch (note.Position)
        {
            case NotePosition.Left:
                x = positions[first] - width;
                break;
            case NotePosition.Right:
                x = positions[first] + (LiveDelta * info.LevelFrom);
                break;
            default:
                if (first != last)
                {
                    var span = positions[last] + (_columns[last].Width / 2) - (positions[first] - (_columns[first].Width / 2));
                    used = Math.Max(width, span);
                }

                x = ((positions[first] + positions[last]) / 2) - (used / 2);
                break;
        }

        return PlaceNoteBox(note, info, top, x, used, state);
    }

    /// <summary>
    ///     Places a note whose layout box starts at a given x.
    /// </summary>
    /// <param name="note">The note.</param>
    /// <param name="info">Its measures.</param>
    /// <param name="top">The top of the row.</param>
    /// <param name="x">The left of its layout box.</param>
    /// <param name="state">The row state.</param>
    /// <returns>The height the note needs on the row.</returns>
    private static double PlaceNoteAt(SequenceNote note, EventInfo info, double top, double x, RowState state) =>
        PlaceNoteBox(note, info, top, x, info.TextWidth, state);

    /// <summary>
    ///     Places a note in its layout box: the outline is 5 pixels inside it, as wide and high as the text with its
    ///     paddings (in whole pixels, as PlantUML draws it).
    /// </summary>
    /// <param name="note">The note.</param>
    /// <param name="info">Its measures.</param>
    /// <param name="top">The top of the row.</param>
    /// <param name="x">The left of the layout box.</param>
    /// <param name="used">The width of the layout box (wider than the note's own width for a note across participants).</param>
    /// <param name="state">The row state.</param>
    /// <returns>The height the note needs on the row.</returns>
    private static double PlaceNoteBox(SequenceNote note, EventInfo info, double top, double x, double used, RowState state)
    {
        var text = info.Text!;
        var outlineX = x + 5;
        var outlineY = top + 5;
        var outlineWidth = Math.Floor(used - 10);
        var outlineHeight = Math.Floor(info.TextHeight);
        var shift = (used - info.TextWidth) / 2;
        var (textX, textY) = note.Shape switch
        {
            NoteShape.Hexagon => (12.0, 4.0),
            NoteShape.Box => (4.0, 4.0),
            _ => (6.0, 5.0),
        };
        state.Items.Add(new PlacedNote(note, outlineX, outlineY, outlineWidth, outlineHeight, text, outlineX + textX + shift, outlineY + textY) { Location = note.Location });
        state.Extent(x, x + used);
        return info.Height;
    }

    /// <summary>
    ///     Applies a life change at a y: opens or closes an activation box, or destroys a life line.
    /// </summary>
    /// <param name="change">The change.</param>
    /// <param name="y">The y.</param>
    /// <param name="positions">The x of every life line.</param>
    /// <param name="state">The row state.</param>
    private void ApplyChange(LifeChange change, double y, double[] positions, RowState state)
    {
        var p = change.Participant;
        var open = state.Open[p];
        switch (change.Action)
        {
            case LifeAction.Activate:
                open.Add((y, change.Fill));
                break;
            case LifeAction.Deactivate when open.Count > 0:
                CloseActivation(p, y, positions, state);
                break;
            case LifeAction.Destroy:
                while (open.Count > 0)
                {
                    CloseActivation(p, y, positions, state);
                }

                // The life line ends with the row of the destroying message
                state.DestroyY[p] ??= y + 8;
                state.Items.Add(new PlacedDestroy(positions[p], y));
                break;
        }
    }

    /// <summary>
    ///     Closes the innermost open activation of a participant.
    /// </summary>
    /// <param name="p">The participant.</param>
    /// <param name="y">The y where it ends.</param>
    /// <param name="positions">The x of every life line.</param>
    /// <param name="state">The row state.</param>
    private void CloseActivation(int p, double y, double[] positions, RowState state)
    {
        var open = state.Open[p];
        var level = open.Count;
        var (start, fill) = open[^1];
        open.RemoveAt(open.Count - 1);
        var x = positions[p] - LiveDelta + (LiveDelta * (level - 1));
        state.Activations.Add(new PlacedActivation(_model.Participants[p].Code, x, start, y, fill));
    }

    /// <summary>
    ///     Closes every open activation at the end of its life line.
    /// </summary>
    /// <param name="state">The row state.</param>
    /// <param name="positions">The x of every life line.</param>
    /// <param name="lifelineEnd">The y where the life lines end.</param>
    private void CloseActivations(RowState state, double[] positions, double lifelineEnd)
    {
        for (var p = 0; p < state.Open.Length; p++)
        {
            while (state.Open[p].Count > 0)
            {
                CloseActivation(p, state.DestroyY[p] ?? lifelineEnd, positions, state);
            }
        }

        // Outer boxes first, so nested ones are drawn over them
        state.Activations.Sort((a, b) => a.X.CompareTo(b.X));
    }

    /// <summary>
    ///     The state of the row placement.
    /// </summary>
    /// <param name="participants">The number of participants.</param>
    private sealed class RowState(int participants)
    {
        /// <summary>Gets or sets the top of the next row.</summary>
        public double Y { get; set; }

        /// <summary>Gets the placed events.</summary>
        public List<PlacedItem> Items { get; } = [];

        /// <summary>Gets the closed activation boxes.</summary>
        public List<PlacedActivation> Activations { get; } = [];

        /// <summary>Gets the open activations of every participant (start and fill), innermost last.</summary>
        public List<(double Y, Rgba32? Fill)>[] Open { get; } = Enumerable.Range(0, participants).Select(_ => new List<(double, Rgba32?)>()).ToArray();

        /// <summary>Gets the y where every participant's life line is destroyed, or null.</summary>
        public double?[] DestroyY { get; } = new double?[participants];

        /// <summary>Gets the delays (top and bottom).</summary>
        public List<(double Top, double Bottom)> Delays { get; } = [];

        /// <summary>Gets the open group frames, innermost last.</summary>
        public List<OpenFrame> Groups { get; } = [];

        /// <summary>Gets the left of everything placed.</summary>
        public double MinX { get; private set; } = double.PositiveInfinity;

        /// <summary>Gets the right of everything placed.</summary>
        public double MaxX { get; private set; } = double.NegativeInfinity;

        /// <summary>
        ///     Adds a horizontal extent to the diagram and to the innermost open frame.
        /// </summary>
        /// <param name="min">The left.</param>
        /// <param name="max">The right.</param>
        /// <param name="frameMax">The right as a group frame sees it (the drawn extent), or null for the same.</param>
        public void Extent(double min, double max, double? frameMax = null)
        {
            MinX = Math.Min(MinX, min);
            MaxX = Math.Max(MaxX, max);
            if (Groups.Count > 0)
            {
                var frame = Groups[^1];
                frame.MinX = Math.Min(frame.MinX, min);
                frame.MaxX = Math.Max(frame.MaxX, frameMax ?? max);
            }
        }
    }
}
