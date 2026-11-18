// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;

namespace DemaConsulting.CanvasNet.PlantUml.Sequence;

/// <content>
///     The columns (the horizontal constraints and their solution), the group frames, the dividers, the delays, the
///     boxes, and the life lines.
/// </content>
internal sealed partial class SequenceLayout
{
    /// <summary>
    ///     Solves the horizontal constraints: the x of every participant's life line, before the diagram is moved to its
    ///     margin.
    /// </summary>
    /// <returns>The x of every life line.</returns>
    private double[] SolveColumns()
    {
        var n = _columns.Length;
        var (before, after) = BoxEdges();
        var constraints = new List<(int To, int From, double Delta)>();
        for (var i = 1; i < n; i++)
        {
            var gap = ColumnGap + after[i - 1] + before[i];
            constraints.Add((i, i - 1, (_columns[i - 1].Width / 2) + (_columns[i].Width / 2) + gap));
        }

        AddEventConstraints(constraints);

        // Every constraint points from a column to a later one: one pass in column order solves them all
        var byTarget = constraints.ToLookup(c => c.To);
        var positions = new double[n];
        for (var j = 0; j < n; j++)
        {
            var x = double.NegativeInfinity;
            if (j == 0)
            {
                x = (_columns[0].Width / 2) + before[0];
            }

            foreach (var (_, from, delta) in byTarget[j])
            {
                Tick();
                x = Math.Max(x, positions[from] + delta);
            }

            positions[j] = x;
        }

        return positions;
    }

    /// <summary>
    ///     Gets the extra space before and after every participant that a box edge adds: 10 pixels and the box's
    ///     margin for a title wider than its participants.
    /// </summary>
    /// <returns>The spaces by participant index.</returns>
    private (double[] Before, double[] After) BoxEdges()
    {
        var before = new double[_columns.Length];
        var after = new double[_columns.Length];
        for (var b = 0; b < _model.Boxes.Count; b++)
        {
            var box = _model.Boxes[b];
            before[box.First] += BoxMargin + _boxMargins[b];
            after[box.Last] += BoxMargin + _boxMargins[b];
        }

        return (before, after);
    }

    /// <summary>
    ///     Adds the constraints of the events: message lengths, self message loops, and group frames.
    /// </summary>
    /// <param name="constraints">The constraints.</param>
    private void AddEventConstraints(List<(int To, int From, double Delta)> constraints)
    {
        var n = _columns.Length;
        var groups = new List<GroupAnchors>();
        for (var i = 0; i < _info.Length; i++)
        {
            Tick();
            var info = _info[i];
            var group = groups.Count > 0 ? groups[^1] : null;
            switch (_model.Events[i])
            {
                case SequenceMessage { IsSelf: true } self:
                    var p = self.From;
                    var reach = (LiveDelta * _columns[p].MaxLevel) + Math.Max(info.TextWidth + 14, SelfMinWidth);
                    if (p + 1 < n)
                    {
                        constraints.Add((p + 1, p, reach));
                    }

                    // A box clears the loops of its participants, and the participant after the box clears the box
                    if (_model.Boxes.FirstOrDefault(b => b.First <= p && p <= b.Last) is { } box && box.Last + 1 < n)
                    {
                        constraints.Add((box.Last + 1, p, reach + ColumnGap + BoxMargin + (_columns[box.Last + 1].Width / 2)));
                    }

                    group?.Add(p, 0, p, (LiveDelta * info.LevelTo) + SelfLoop + 3, true);
                    break;
                case SequenceMessage message:
                    var (left, right) = (Math.Min(message.From, message.To), Math.Max(message.From, message.To));
                    var (levelLeft, levelRight) = left == message.From ? (info.LevelFrom, info.LevelTo) : (info.LevelTo, info.LevelFrom);
                    var delta = info.TextWidth + MessageMargin + (LiveDelta * levelLeft) + (levelRight > 0 ? LiveDelta : 0);
                    constraints.Add((right, left, delta));
                    group?.Add(left, 0, right, 0, false);
                    break;
                case SequenceNote note:
                    AddNoteAnchors(note, info, group);
                    break;
                case SequenceGroupStart:
                    groups.Add(new GroupAnchors(info.TextWidth));
                    break;
                case SequenceGroupElse when group is not null:
                    group.HeaderWidth = Math.Max(group.HeaderWidth, 5 + (info.Label?.Width ?? 0));
                    break;
                case SequenceGroupEnd when group is not null:
                    groups.RemoveAt(groups.Count - 1);
                    CloseGroupAnchors(group, groups.Count > 0 ? groups[^1] : null, constraints);
                    break;
            }
        }
    }

    /// <summary>
    ///     Adds the extents of a note to the innermost group.
    /// </summary>
    /// <param name="note">The note.</param>
    /// <param name="info">Its measures.</param>
    /// <param name="group">The innermost group, or null.</param>
    private void AddNoteAnchors(SequenceNote note, EventInfo info, GroupAnchors? group)
    {
        if (group is null)
        {
            return;
        }

        var width = info.TextWidth;
        var (first, last) = (Math.Min(note.First, note.Last), Math.Max(note.First, note.Last));
        switch (note.Position)
        {
            case NotePosition.Left:
                var p = note.OnMessage ? first : note.First;
                group.Add(p, -width, p, 0, true);
                break;
            case NotePosition.Right:
                var q = note.OnMessage ? last : note.First;
                group.Add(q, 0, q, (LiveDelta * info.LevelFrom) + width, true);
                break;
            default:
                var half = first == last ? width / 2 : 0;
                group.Add(first, first == last ? -half : -_columns[first].Width / 2, last, first == last ? half : _columns[last].Width / 2, true);
                break;
        }
    }

    /// <summary>
    ///     Adds the constraints of a closed group frame and passes its extents to the enclosing group.
    /// </summary>
    /// <remarks>
    ///     As PlantUML's sequence engine does: the participant right after the group's last participant is at least 25
    ///     pixels right of it and clears the frame's header and the drawn reach of the group's self messages and notes
    ///     by 9 pixels; the participant right before the group's first participant is at least 25 pixels left of it
    ///     and clears the reach of the group's notes on the left. Messages do not push beyond that baseline. A nested
    ///     group's extents reach 25 pixels further on the right and 19 on the left (its frame margin and outer margin).
    /// </remarks>
    /// <param name="group">The closed group.</param>
    /// <param name="parent">The enclosing group, or null.</param>
    /// <param name="constraints">The constraints.</param>
    private void CloseGroupAnchors(GroupAnchors group, GroupAnchors? parent, List<(int To, int From, double Delta)> constraints)
    {
        if (group.Min.Count == 0)
        {
            return;
        }

        const double Clear = FrameMargin + FrameOuterRight;
        var first = Math.Min(group.Min.Min(a => a.P), group.Max.Min(a => a.P));
        var last = Math.Max(group.Min.Max(a => a.P), group.Max.Max(a => a.P));
        var frameMin = group.Min.Where(a => a.P == first).Select(a => a.O).DefaultIfEmpty(0).Min() - FrameMargin;
        var title = (P: first, O: frameMin + group.HeaderWidth + FrameMargin, Stable: true);
        if (last + 1 < _columns.Length)
        {
            constraints.Add((last + 1, last, Clear));
            constraints.Add((last + 1, title.P, title.O + FrameOuterRight));
            constraints.AddRange(group.Max.Where(a => a.Stable).Select(a => (last + 1, a.P, a.O + Clear)));
        }

        if (first > 0)
        {
            constraints.Add((first, first - 1, Clear));
            constraints.AddRange(group.Min.Where(a => a.Stable).Select(a => (a.P, first - 1, Clear - a.O)));
        }

        parent?.Min.AddRange(group.Min.Select(a => (a.P, a.O - FrameMargin - FrameOuterLeft, a.Stable)));
        parent?.Max.AddRange(group.Max.Select(a => (a.P, a.O + Clear, a.Stable)).Append((title.P, title.O + FrameOuterRight, true)));
    }
    /// <summary>
    ///     Places a group start: opens its frame.
    /// </summary>
    /// <param name="start">The group start.</param>
    /// <param name="info">Its measures.</param>
    /// <param name="state">The row state.</param>
    private static void OpenGroup(SequenceGroupStart start, EventInfo info, RowState state)
    {
        state.Groups.Add(new OpenFrame(start, info, state.Y + FrameTopMargin));
        state.Y += info.Height;
    }

    /// <summary>
    ///     Places an <c>else</c>: a section separator in the innermost frame.
    /// </summary>
    /// <param name="info">Its measures.</param>
    /// <param name="state">The row state.</param>
    private static void PlaceElse(EventInfo info, RowState state)
    {
        if (state.Groups.Count > 0)
        {
            var frame = state.Groups[^1];
            frame.Sections.Add(new PlacedElse(state.Y + 1, info.Label));
            frame.ElseWidth = Math.Max(frame.ElseWidth, 5 + (info.Label?.Width ?? 0));
        }

        state.Y += info.Height;
    }

    /// <summary>
    ///     Places a group end: closes the innermost frame around its content.
    /// </summary>
    /// <param name="end">The group end.</param>
    /// <param name="state">The row state.</param>
    private static void CloseGroup(SequenceGroupEnd end, RowState state)
    {
        if (state.Groups.Count == 0)
        {
            return;
        }

        var frame = state.Groups[^1];
        state.Groups.RemoveAt(state.Groups.Count - 1);
        var hasContent = double.IsFinite(frame.MinX);
        var x = hasContent ? frame.MinX - FrameMargin : state.MinX;
        var right = Math.Max(hasContent ? frame.MaxX + FrameMargin : x, x + frame.Info.TextWidth + FrameMargin);
        right = Math.Max(right, x + frame.ElseWidth + FrameMargin);
        var placed = new PlacedGroup(
            frame.Start,
            x,
            frame.Top,
            right - x,
            state.Y - frame.Top,
            frame.Info.Text!,
            frame.Info.Label,
            frame.Info.TabWidth,
            frame.Info.TabHeight,
            frame.Sections)
        {
            Location = end.Location,
        };
        state.Items.Add(placed);
        state.Extent(x - FrameOuterLeft, right + FrameOuterRight);
        state.Y += FrameBottomMargin;
    }

    /// <summary>
    ///     Places a divider row; its ends are set when the width of the diagram is known.
    /// </summary>
    /// <param name="divider">The divider.</param>
    /// <param name="info">Its measures.</param>
    /// <param name="state">The row state.</param>
    private static void PlaceDivider(SequenceDivider divider, EventInfo info, RowState state)
    {
        state.Items.Add(new PlacedDivider(info.Text, 0, 0, state.Y, info.Height, info.TextWidth, info.TextHeight) { Location = divider.Location });
        state.Y += info.Height;
    }

    /// <summary>
    ///     Places a delay row: dotted life lines and its text centered over the participants.
    /// </summary>
    /// <param name="delay">The delay.</param>
    /// <param name="info">Its measures.</param>
    /// <param name="state">The row state.</param>
    /// <param name="positions">The x of every life line.</param>
    private static void PlaceDelay(SequenceDelay delay, EventInfo info, RowState state, double[] positions)
    {
        state.Delays.Add((state.Y, state.Y + info.Height));
        if (info.Text is { } text && positions.Length > 0)
        {
            var middle = (positions[0] + positions[^1]) / 2;
            state.Items.Add(new PlacedDelay(text, middle - (text.Width / 2), state.Y + 14) { Location = delay.Location });
        }

        state.Y += info.Height;
    }

    /// <summary>
    ///     Sets the ends of the dividers: from the left of the diagram to its right, or wider for a long text.
    /// </summary>
    /// <param name="state">The row state.</param>
    private static void FinishDividers(RowState state)
    {
        var dividers = state.Items.OfType<PlacedDivider>().ToList();
        if (dividers.Count == 0)
        {
            return;
        }

        var x1 = double.IsFinite(state.MinX) ? state.MinX : Margin;
        var x2 = Math.Max(double.IsFinite(state.MaxX) ? state.MaxX : x1, dividers.Max(d => x1 + d.RectWidth + MessageMargin));
        for (var i = 0; i < state.Items.Count; i++)
        {
            if (state.Items[i] is PlacedDivider divider)
            {
                state.Items[i] = divider with { X1 = x1, X2 = x2 };
            }
        }

        state.Extent(x1, x2);
    }

    /// <summary>
    ///     Builds the parts of the life lines: dashed, dotted in delays, and ending at a destroy.
    /// </summary>
    /// <param name="participants">The placed participants.</param>
    /// <param name="state">The row state.</param>
    /// <param name="start">The y where the life lines start.</param>
    /// <param name="end">The y where the life lines end.</param>
    /// <returns>The parts.</returns>
    private static List<PlacedLifeline> BuildLifelines(IReadOnlyList<PlacedParticipant> participants, RowState state, double start, double end)
    {
        var lines = new List<PlacedLifeline>();
        for (var p = 0; p < participants.Count; p++)
        {
            var code = participants[p].Participant.Code;
            var x = participants[p].PosC;
            var stop = state.DestroyY[p] ?? end;
            var y = start;
            foreach (var (top, bottom) in state.Delays.Where(d => d.Top < stop))
            {
                lines.Add(new PlacedLifeline(code, x, y, top, false));
                var delayEnd = Math.Min(bottom, stop);
                lines.Add(new PlacedLifeline(code, x, top, delayEnd, true));
                y = delayEnd;
            }

            if (stop > y)
            {
                lines.Add(new PlacedLifeline(code, x, y, stop, false));
            }
        }

        return lines;
    }

    /// <summary>
    ///     Places the boxes around their participants.
    /// </summary>
    /// <param name="participants">The placed participants.</param>
    /// <param name="titles">The box titles.</param>
    /// <param name="bottom">The y of the bottom of the participants.</param>
    /// <returns>The boxes.</returns>
    private List<PlacedBox> PlaceBoxes(IReadOnlyList<PlacedParticipant> participants, TextBlock?[] titles, double bottom)
    {
        var boxes = new List<PlacedBox>();
        for (var i = 0; i < _model.Boxes.Count; i++)
        {
            var box = _model.Boxes[i];
            var left = participants[box.First].PosB - _boxMargins[i] - 4;
            var right = participants[box.Last].PosB + participants[box.Last].Width + _boxMargins[i];
            for (var p = box.First; p <= box.Last; p++)
            {
                right = Math.Max(right, participants[p].PosC + _columns[p].SelfReach);
            }

            var width = right + 4 - left;
            boxes.Add(new PlacedBox(box, left, Margin + 1, width, bottom + 6 - (Margin + 1), titles[i]));
        }

        return boxes;
    }

    /// <summary>
    ///     The horizontal extents of the content of a group, as anchors on life lines: a participant index and an
    ///     offset from its life line.
    /// </summary>
    /// <param name="headerWidth">The width of the group's header.</param>
    private sealed class GroupAnchors(double headerWidth)
    {
        /// <summary>Gets the left extents (stable ones are self messages, notes, and nested headers).</summary>
        public List<(int P, double O, bool Stable)> Min { get; } = [];

        /// <summary>Gets the right extents (stable ones are self messages, notes, and nested headers).</summary>
        public List<(int P, double O, bool Stable)> Max { get; } = [];

        /// <summary>Gets or sets the width of the header (or the widest section condition).</summary>
        public double HeaderWidth { get; set; } = headerWidth;

        /// <summary>
        ///     Adds the extents of one row.
        /// </summary>
        /// <param name="minP">The participant of the left extent.</param>
        /// <param name="minO">The offset of the left extent.</param>
        /// <param name="maxP">The participant of the right extent.</param>
        /// <param name="maxO">The offset of the right extent.</param>
        /// <param name="stable">Whether the extents push the participants outside the group.</param>
        public void Add(int minP, double minO, int maxP, double maxO, bool stable)
        {
            Min.Add((minP, minO, stable));
            Max.Add((maxP, maxO, stable));
        }
    }

    /// <summary>
    ///     A group frame being placed.
    /// </summary>
    /// <param name="start">The group start.</param>
    /// <param name="info">Its measures.</param>
    /// <param name="top">The top of the frame.</param>
    private sealed class OpenFrame(SequenceGroupStart start, EventInfo info, double top)
    {
        /// <summary>Gets the group start.</summary>
        public SequenceGroupStart Start { get; } = start;

        /// <summary>Gets the measures of the header.</summary>
        public EventInfo Info { get; } = info;

        /// <summary>Gets the top of the frame.</summary>
        public double Top { get; } = top;

        /// <summary>Gets the section separators.</summary>
        public List<PlacedElse> Sections { get; } = [];

        /// <summary>Gets or sets the width the widest section condition needs.</summary>
        public double ElseWidth { get; set; }

        /// <summary>Gets or sets the left of the content.</summary>
        public double MinX { get; set; } = double.PositiveInfinity;

        /// <summary>Gets or sets the right of the content.</summary>
        public double MaxX { get; set; } = double.NegativeInfinity;
    }
}
