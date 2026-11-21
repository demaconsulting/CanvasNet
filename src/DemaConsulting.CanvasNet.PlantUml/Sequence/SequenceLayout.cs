// Portions derived from PlantUML (MIT); see third-party/README.md
using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;

namespace DemaConsulting.CanvasNet.PlantUml.Sequence;

/// <summary>
///     A placed participant: its column, its text, and where its head and tail are drawn.
/// </summary>
/// <param name="Participant">The participant.</param>
/// <param name="PosB">The left edge of its column.</param>
/// <param name="PosC">The x of its life line.</param>
/// <param name="Width">The width of its column.</param>
/// <param name="PrefHeight">The height of its head (and tail), with the pixel below it.</param>
/// <param name="Text">Its text.</param>
/// <param name="Style">Its style.</param>
/// <param name="HeadBottom">The y where its head ends (the heads are aligned at the bottom).</param>
/// <param name="TailTop">The y where its tail starts, or NaN when the foot box is hidden.</param>
internal sealed record PlacedParticipant(
    SequenceParticipant Participant,
    double PosB,
    double PosC,
    double Width,
    double PrefHeight,
    TextBlock Text,
    ElementStyle Style,
    double HeadBottom,
    double TailTop);

/// <summary>
///     A part of a life line.
/// </summary>
/// <param name="Participant">The participant code.</param>
/// <param name="X">The x.</param>
/// <param name="Y1">The top.</param>
/// <param name="Y2">The bottom.</param>
/// <param name="Delay">Whether the part is in a delay (drawn dotted).</param>
internal sealed record PlacedLifeline(string Participant, double X, double Y1, double Y2, bool Delay);

/// <summary>
///     An activation box.
/// </summary>
/// <param name="Participant">The participant code.</param>
/// <param name="X">The left edge.</param>
/// <param name="Y1">The top.</param>
/// <param name="Y2">The bottom.</param>
/// <param name="Fill">The fill, or null for the theme's.</param>
internal sealed record PlacedActivation(string Participant, double X, double Y1, double Y2, Rgba32? Fill);

/// <summary>
///     A placed event, drawn in event order over the life lines.
/// </summary>
internal abstract record PlacedItem
{
    /// <summary>
    ///     Gets the location of the statement.
    /// </summary>
    public PlantUmlLocation Location { get; init; }
}

/// <summary>
///     A placed message between two participants.
/// </summary>
/// <param name="Message">The message.</param>
/// <param name="XLeft">The x where the arrow meets the left participant (its activation box included).</param>
/// <param name="XRight">The x where the arrow meets the right participant (its activation box included).</param>
/// <param name="ArrowY">The y of the arrow.</param>
/// <param name="ToLeft">Whether the receiver is the left participant.</param>
/// <param name="Label">The label.</param>
/// <param name="Number">The number, or null.</param>
/// <param name="TextX">The x of the text (the number, then the label).</param>
/// <param name="TextTop">The top of the text.</param>
internal sealed record PlacedMessage(
    SequenceMessage Message,
    double XLeft,
    double XRight,
    double ArrowY,
    bool ToLeft,
    TextBlock Label,
    TextBlock? Number,
    double TextX,
    double TextTop) : PlacedItem;

/// <summary>
///     A placed message from a participant to itself.
/// </summary>
/// <param name="Message">The message.</param>
/// <param name="StartX">The x where the loop leaves the participant.</param>
/// <param name="LoopX">The x the loop is measured from (where it returns, one pixel left of the head's tip).</param>
/// <param name="TopY">The y of the outgoing line.</param>
/// <param name="BottomY">The y of the returning line.</param>
/// <param name="Label">The label.</param>
/// <param name="Number">The number, or null.</param>
/// <param name="TextX">The x of the text.</param>
/// <param name="TextTop">The top of the text.</param>
internal sealed record PlacedSelf(
    SequenceMessage Message,
    double StartX,
    double LoopX,
    double TopY,
    double BottomY,
    TextBlock Label,
    TextBlock? Number,
    double TextX,
    double TextTop) : PlacedItem;

/// <summary>
///     A placed note.
/// </summary>
/// <param name="Note">The note.</param>
/// <param name="X">The left edge of the outline.</param>
/// <param name="Y">The top of the outline.</param>
/// <param name="Width">The width of the outline.</param>
/// <param name="Height">The height of the outline.</param>
/// <param name="Text">The text.</param>
/// <param name="TextX">The x of the text.</param>
/// <param name="TextTop">The top of the text.</param>
internal sealed record PlacedNote(
    SequenceNote Note,
    double X,
    double Y,
    double Width,
    double Height,
    TextBlock Text,
    double TextX,
    double TextTop) : PlacedItem;

/// <summary>
///     A section separator (<c>else</c>) of a placed group.
/// </summary>
/// <param name="Y">The y of the dashed line.</param>
/// <param name="Label">The condition text (with brackets), or null.</param>
internal sealed record PlacedElse(double Y, TextBlock? Label);

/// <summary>
///     A placed group frame.
/// </summary>
/// <param name="Start">The group start.</param>
/// <param name="X">The left edge of the frame.</param>
/// <param name="Y">The top of the frame.</param>
/// <param name="Width">The width of the frame.</param>
/// <param name="Height">The height of the frame.</param>
/// <param name="Keyword">The text of the header tab.</param>
/// <param name="Label">The condition text (with brackets), or null.</param>
/// <param name="TabWidth">The width of the header tab.</param>
/// <param name="TabHeight">The height of the header tab.</param>
/// <param name="Sections">The section separators.</param>
internal sealed record PlacedGroup(
    SequenceGroupStart Start,
    double X,
    double Y,
    double Width,
    double Height,
    TextBlock Keyword,
    TextBlock? Label,
    double TabWidth,
    double TabHeight,
    IReadOnlyList<PlacedElse> Sections) : PlacedItem;

/// <summary>
///     A placed divider.
/// </summary>
/// <param name="Text">The text, or null when it is empty.</param>
/// <param name="X1">The left end.</param>
/// <param name="X2">The right end.</param>
/// <param name="Top">The top of the row.</param>
/// <param name="Height">The height of the row.</param>
/// <param name="RectWidth">The width of the text box.</param>
/// <param name="RectHeight">The height of the text box.</param>
internal sealed record PlacedDivider(TextBlock? Text, double X1, double X2, double Top, double Height, double RectWidth, double RectHeight) : PlacedItem;

/// <summary>
///     The text of a placed delay.
/// </summary>
/// <param name="Text">The text.</param>
/// <param name="X">The x of the text.</param>
/// <param name="Top">The top of the text.</param>
internal sealed record PlacedDelay(TextBlock Text, double X, double Top) : PlacedItem;

/// <summary>
///     The cross that ends a destroyed life line.
/// </summary>
/// <param name="X">The x of the center.</param>
/// <param name="Y">The y of the center.</param>
internal sealed record PlacedDestroy(double X, double Y) : PlacedItem;

/// <summary>
///     A placed box around participants.
/// </summary>
/// <param name="Box">The box.</param>
/// <param name="X">The left edge.</param>
/// <param name="Y">The top.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
/// <param name="Title">The title, or null.</param>
internal sealed record PlacedBox(SequenceBox Box, double X, double Y, double Width, double Height, TextBlock? Title);

/// <summary>
///     The laid-out geometry of a sequence diagram, before the page texts.
/// </summary>
/// <param name="Participants">The participants.</param>
/// <param name="Lifelines">The parts of the life lines.</param>
/// <param name="Activations">The activation boxes.</param>
/// <param name="Items">The events in drawing order.</param>
/// <param name="Boxes">The boxes.</param>
/// <param name="OffsetX">The horizontal offset that moves the leftmost extent to the margin.</param>
/// <param name="Width">The width of the body (with its margins).</param>
/// <param name="Height">The height of the body (with its margins).</param>
internal sealed record SequenceGeometry(
    IReadOnlyList<PlacedParticipant> Participants,
    IReadOnlyList<PlacedLifeline> Lifelines,
    IReadOnlyList<PlacedActivation> Activations,
    IReadOnlyList<PlacedItem> Items,
    IReadOnlyList<PlacedBox> Boxes,
    double OffsetX,
    double Width,
    double Height);

/// <summary>
///     Lays a sequence diagram out as PlantUML's sequence engine does: participant columns spaced by the width their
///     messages need, and rows of messages, notes, groups, dividers, delays and spaces from top to bottom.
/// </summary>
/// <remarks>
///     <para>
///         <b>Columns.</b> Every participant has a column as wide as its head; neighbor columns are at least 10 pixels
///         apart (20 at the edge of a box). A message pushes its receiver right until the arrow is as long as its text
///         plus 24 pixels (plus 5 pixels for each activation box it meets); a message to itself pushes the next
///         participant past its loop; a group frame pushes the participants outside it past its margins. The
///         constraints all point from left to right, so one pass in column order solves them. The diagram is then moved
///         so that its leftmost extent (a note left of the first participant, a group frame) is at the 10-pixel margin.
///     </para>
///     <para>
///         <b>Rows.</b> The heads are aligned at the bottom, the life lines start below them, and the first row starts
///         8 pixels lower. A message row is as tall as its text plus 14 pixels, a self message adds 25, a note adds 20,
///         a group adds its header, 18 pixels of margins, and its sections, a divider is its text box plus 20, a delay
///         28 (plus its text), and a space its height. The life lines end 10 pixels below the last row, where the tails
///         start unless the foot box is hidden.
///     </para>
///     <para>
///         <b>Limits.</b> The work of a layout is counted against <see cref="PlantUmlLimits.MaxLayoutWork"/>
///         (<see cref="PlantUmlDiagnosticCodes.LimitExceeded"/>) and the cancellation token is checked as it goes. An
///         instance lays out one diagram on one thread; <see cref="Run"/> is thread-safe.
///     </para>
/// </remarks>
internal sealed partial class SequenceLayout
{
    /// <summary>The margin around the diagram.</summary>
    private const double Margin = 10;

    /// <summary>The smallest space between neighbor columns.</summary>
    private const double ColumnGap = 10;

    /// <summary>The extra space at the edges of a box.</summary>
    private const double BoxMargin = 10;

    /// <summary>The length a message arrow needs beyond its text.</summary>
    private const double MessageMargin = 24;

    /// <summary>The horizontal step of nested activation boxes (half their width).</summary>
    private const double LiveDelta = 5;

    /// <summary>The space between the life line start and the first row.</summary>
    private const double FirstRowGap = 8;

    /// <summary>The space between the last row and the life line end.</summary>
    private const double LastRowGap = 10;

    /// <summary>The smallest width of a self message.</summary>
    private const double SelfMinWidth = 50;

    /// <summary>The width of a self message loop.</summary>
    private const double SelfLoop = 42;

    /// <summary>The margin of a group frame around its content.</summary>
    private const double FrameMargin = 16;

    /// <summary>The space a group frame keeps on its left.</summary>
    private const double FrameOuterLeft = 3;

    /// <summary>The space a group frame keeps on its right.</summary>
    private const double FrameOuterRight = 9;

    /// <summary>The space above a group frame.</summary>
    private const double FrameTopMargin = 4;

    /// <summary>The space below a group frame.</summary>
    private const double FrameBottomMargin = 14;

    /// <summary>How many units of work pass between two cancellation checks.</summary>
    private const long CancellationStride = 256;

    /// <summary>The model.</summary>
    private readonly SequenceModel _model;

    /// <summary>The context of the call.</summary>
    private readonly DiagramRenderContext _context;

    /// <summary>The measured events, by event index.</summary>
    private readonly EventInfo[] _info;

    /// <summary>The measured participants.</summary>
    private readonly ColumnInfo[] _columns;

    /// <summary>The margin inside each box on both sides of its participants (for a title wider than them).</summary>
    private readonly double[] _boxMargins;

    /// <summary>The work done so far.</summary>
    private long _work;

    /// <summary>
    ///     Initializes a layout.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="context">The context of the call.</param>
    private SequenceLayout(SequenceModel model, DiagramRenderContext context)
    {
        _model = model;
        _context = context;
        _info = new EventInfo[model.Events.Count];
        _columns = new ColumnInfo[model.Participants.Count];
        _boxMargins = new double[model.Boxes.Count];
    }

    /// <summary>
    ///     Gets the theme.
    /// </summary>
    private SkinTheme Theme => _model.Theme;

    /// <summary>
    ///     Gets the extra space between a message text and its arrow for a thick arrow: half the width the line has
    ///     beyond 1 pixel, so that a thick line does not touch its label (0 at the default thickness).
    /// </summary>
    private double ArrowClearance => Math.Max(0, (Theme.ArrowThickness - 1) / 2);

    /// <summary>
    ///     Lays a sequence diagram out.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="context">The context of the call.</param>
    /// <returns>The geometry.</returns>
    /// <exception cref="PlantUmlRenderException">Thrown when the layout needs more work than the limit allows.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the call is canceled.</exception>
    public static SequenceGeometry Run(SequenceModel model, DiagramRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(context);
        var layout = new SequenceLayout(model, context);
        layout.Measure();
        var positions = layout.SolveColumns();
        return layout.PlaceRows(positions);
    }

    /// <summary>
    ///     Gets the font of a style.
    /// </summary>
    /// <param name="style">The style.</param>
    /// <returns>The font.</returns>
    internal static SceneFont FontOf(ElementStyle style) => new(style.FontFamily, style.FontSize, style.Bold, style.Italic);

    /// <summary>
    ///     Counts work and checks the limit and the cancellation.
    /// </summary>
    /// <param name="units">The units of work.</param>
    /// <exception cref="PlantUmlRenderException">Thrown when the work exceeds the limit.</exception>
    private void Tick(long units = 1)
    {
        var before = _work;
        _work += units;
        var limit = _context.Options.Limits.MaxLayoutWork;
        if (_work > limit)
        {
            throw new PlantUmlRenderException(
                PlantUmlDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"the layout needs more than {limit} units of work (PlantUmlLimits.MaxLayoutWork)"),
                _model.KeywordLocation,
                "simplify the diagram, or raise PlantUmlLimits.MaxLayoutWork when the source is trusted");
        }

        if (before / CancellationStride != _work / CancellationStride)
        {
            _context.CancellationToken.ThrowIfCancellationRequested();
        }
    }

    /// <summary>
    ///     Lays out a text in a style.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="style">The style.</param>
    /// <param name="location">The location for reports.</param>
    /// <param name="wrap">The wrapping width, or zero.</param>
    /// <returns>The block.</returns>
    private TextBlock Text(string text, ElementStyle style, PlantUmlLocation location, double wrap = 0)
    {
        Tick(1 + (text.Length / 64));
        return _context.Text.Layout(text, FontOf(style), style.FontColor, wrap, _context.Sink, location);
    }

    /// <summary>
    ///     Measures the participants and the events, and works out the activation levels of every message.
    /// </summary>
    private void Measure()
    {
        for (var i = 0; i < _columns.Length; i++)
        {
            _columns[i] = MeasureParticipant(_model.Participants[i]);
        }

        // A box title wider than the box's participants (measured side by side) widens the box on both sides
        for (var b = 0; b < _boxMargins.Length; b++)
        {
            var box = _model.Boxes[b];
            if (box.Title is not null)
            {
                var title = Text(box.Title, Theme[SkinElement.Box], box.Location);
                var participants = Enumerable.Range(box.First, box.Last - box.First + 1).Sum(p => _columns[p].Width);
                _boxMargins[b] = Math.Max(0, (title.Width + 16 - participants) / 2);
            }
        }

        var levels = new int[_columns.Length];
        var maxLevels = new int[_columns.Length];
        for (var i = 0; i < _info.Length; i++)
        {
            Tick();
            _info[i] = MeasureEvent(_model.Events[i], levels, maxLevels);
        }

        var reach = new double[_columns.Length];
        for (var i = 0; i < _info.Length; i++)
        {
            if (_model.Events[i] is SequenceMessage { IsSelf: true } self)
            {
                reach[self.From] = Math.Max(reach[self.From], LoopReach(self.From, _info[i], maxLevels));
            }
        }

        for (var i = 0; i < _columns.Length; i++)
        {
            _columns[i] = _columns[i] with { MaxLevel = maxLevels[i], SelfReach = reach[i] };
        }
    }

    /// <summary>
    ///     Measures a participant: its text, its column width, and its head height.
    /// </summary>
    /// <param name="participant">The participant.</param>
    /// <returns>The measures.</returns>
    private ColumnInfo MeasureParticipant(SequenceParticipant participant)
    {
        var element = participant.Kind switch
        {
            ParticipantKind.Actor => SkinElement.Actor,
            ParticipantKind.Boundary => SkinElement.Boundary,
            ParticipantKind.Control => SkinElement.Control,
            ParticipantKind.Entity => SkinElement.Entity,
            ParticipantKind.Database => SkinElement.Database,
            ParticipantKind.Collections => SkinElement.Collections,
            ParticipantKind.Queue => SkinElement.Queue,
            _ => SkinElement.Participant,
        };
        var style = Theme[element];
        var text = Text(participant.Display, style, participant.Location);
        var (w, h) = (text.Width, text.Height);
        var pad = style.Padding;
        var (width, height) = participant.Kind switch
        {
            ParticipantKind.Actor => (Math.Max(27, w + 6), 60 + h),
            ParticipantKind.Boundary => (Math.Max(49, w + 6), 28 + h),
            ParticipantKind.Control or ParticipantKind.Entity => (Math.Max(32, w + 6), 28 + h),
            ParticipantKind.Database => (Math.Max(36, w + 6), 46 + h),
            ParticipantKind.Collections => (w + (2 * pad) + 4, h + (2 * pad) + 5),
            ParticipantKind.Queue => (w + 20, h + 10),
            _ => (w + (2 * pad), h + (2 * pad) + 1),
        };
        return new ColumnInfo(text, style, width, height, 0);
    }

    /// <summary>
    ///     Measures an event and advances the activation levels.
    /// </summary>
    /// <param name="item">The event.</param>
    /// <param name="levels">The current activation level of every participant.</param>
    /// <param name="maxLevels">The deepest activation level of every participant so far.</param>
    /// <returns>The measures.</returns>
    private EventInfo MeasureEvent(SequenceEvent item, int[] levels, int[] maxLevels)
    {
        var info = new EventInfo();
        switch (item)
        {
            case SequenceMessage message:
                MeasureMessage(message, info, levels);
                foreach (var change in message.Changes)
                {
                    ApplyLevel(change, levels, maxLevels);
                }

                break;
            case SequenceLifeEvent life:
                ApplyLevel(life.Change, levels, maxLevels);
                break;
            case SequenceNote note:
                MeasureNote(note, info, levels);
                break;
            case SequenceGroupStart start:
                MeasureGroup(start, info);
                break;
            case SequenceGroupElse otherwise:
                info.Label = otherwise.Label is null ? null : Text("[" + otherwise.Label + "]", Theme[SkinElement.Group], item.Location);
                info.Height = (info.Label?.Height ?? 0) + 6;
                break;
            case SequenceDivider divider:
                MeasureDivider(divider, info);
                break;
            case SequenceDelay delay:
                info.Text = delay.Text.Length == 0 ? null : Text(delay.Text, Theme[SkinElement.Delay], item.Location);
                info.Height = info.Text is null ? 28 : info.Text.Height + 28;
                break;
            case SequenceSpace space:
                info.Height = space.Height;
                break;
        }

        return info;
    }

    /// <summary>
    ///     Gets how far right of its life line a self message reserves room: the participant's deepest activation and
    ///     the wider of the text (with its margins) and the loop.
    /// </summary>
    /// <param name="p">The participant.</param>
    /// <param name="info">The measures of the message.</param>
    /// <param name="maxLevels">The deepest levels.</param>
    /// <returns>The reach from the life line.</returns>
    private static double LoopReach(int p, EventInfo info, int[] maxLevels) =>
        (LiveDelta * maxLevels[p]) + Math.Max(info.TextWidth + 14, SelfMinWidth);

    /// <summary>
    ///     Changes the activation level of a participant.
    /// </summary>
    /// <param name="change">The change.</param>
    /// <param name="levels">The current levels.</param>
    /// <param name="maxLevels">The deepest levels.</param>
    private static void ApplyLevel(LifeChange change, int[] levels, int[] maxLevels)
    {
        var p = change.Participant;
        levels[p] = change.Action switch
        {
            LifeAction.Activate => levels[p] + 1,
            LifeAction.Deactivate => Math.Max(0, levels[p] - 1),
            _ => 0,
        };
        maxLevels[p] = Math.Max(maxLevels[p], levels[p]);
    }

    /// <summary>
    ///     Measures a message: its text and the activation levels at both ends.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="info">The measures to fill.</param>
    /// <param name="levels">The current levels.</param>
    private void MeasureMessage(SequenceMessage message, EventInfo info, int[] levels)
    {
        var style = Theme[SkinElement.Arrow];
        info.Label = Text(message.Label, style, message.Location, Theme.MaxMessageSize);
        info.Number = message.Number is null ? null : Text(message.Number, style, message.Location);
        var numberWidth = info.Number is null ? 0 : info.Number.Width + 4;
        info.TextWidth = numberWidth + info.Label.Width;
        // (a thick arrow keeps its label clear: the row grows by the clearance between the text and the line)
        info.TextHeight = Math.Max(info.Label.Height, info.Number?.Height ?? 0) + 2 + ArrowClearance;
        // An activation on the arrow already counts at both ends; a deactivation only after the arrow
        var activatesTo = message.Changes.Any(c => c.Participant == message.To && c.Action == LifeAction.Activate);
        var activatesFrom = !message.IsSelf && message.Changes.Any(c => c.Participant == message.From && c.Action == LifeAction.Activate);
        info.LevelFrom = levels[message.From] + (activatesFrom ? 1 : 0);
        info.LevelTo = levels[message.To] + (activatesTo ? 1 : 0);
        info.Height = message.IsSelf ? info.TextHeight + 25 : info.TextHeight + 12;
    }

    /// <summary>
    ///     Measures a note.
    /// </summary>
    /// <param name="note">The note.</param>
    /// <param name="info">The measures to fill.</param>
    /// <param name="levels">The current levels.</param>
    private void MeasureNote(SequenceNote note, EventInfo info, int[] levels)
    {
        // A note has paddings of 6 and 15 around its text and 5 above and below; a hexagon 12 and a rectangle 4 on
        // every side; every outline is 5 pixels inside its layout box
        info.Text = Text(note.Text, Theme[SkinElement.Note], note.Location);
        var (padX, padY) = note.Shape switch
        {
            NoteShape.Hexagon => (24.0, 8.0),
            NoteShape.Box => (8.0, 8.0),
            _ => (21.0, 10.0),
        };
        info.TextWidth = info.Text.Width + padX + 10;
        info.TextHeight = info.Text.Height + padY;
        info.Height = info.TextHeight + 10;
        info.LevelFrom = levels[note.First];
        info.LevelTo = levels[note.Last];
    }

    /// <summary>
    ///     Measures a group header: its tab text and its condition.
    /// </summary>
    /// <param name="start">The group start.</param>
    /// <param name="info">The measures to fill.</param>
    private void MeasureGroup(SequenceGroupStart start, EventInfo info)
    {
        info.Text = Text(start.Keyword, Theme[SkinElement.GroupHeader], start.Location);
        info.Label = start.Label is null ? null : Text("[" + start.Label + "]", Theme[SkinElement.Group], start.Location);
        info.TabWidth = info.Text.Width + 45;
        info.TabHeight = info.Text.Height + 2;
        info.TextWidth = info.TabWidth + (info.Label is null ? 0 : info.Label.Width + 15);
        info.Height = info.TabHeight + 10 + FrameTopMargin;
    }

    /// <summary>
    ///     Measures a divider: its text box and its row height.
    /// </summary>
    /// <param name="divider">The divider.</param>
    /// <param name="info">The measures to fill.</param>
    private void MeasureDivider(SequenceDivider divider, EventInfo info)
    {
        var style = Theme[SkinElement.Separator];
        if (divider.Text.Length == 0)
        {
            info.Height = 28;
            return;
        }

        info.Text = Text(divider.Text, style, divider.Location);

        // PlantUML measures the divider text with a trailing space
        var space = _context.Text.Width(" ", FontOf(style));
        info.TextWidth = info.Text.Width + space + (2 * style.Padding) + 6;
        info.TextHeight = info.Text.Height + (2 * style.Padding);
        info.Height = info.TextHeight + 20;
    }

    /// <summary>
    ///     The measures of a participant.
    /// </summary>
    /// <param name="Text">The text.</param>
    /// <param name="Style">The style.</param>
    /// <param name="Width">The width of the column.</param>
    /// <param name="Height">The height of the head and tail.</param>
    /// <param name="MaxLevel">The deepest activation level.</param>
    private sealed record ColumnInfo(TextBlock Text, ElementStyle Style, double Width, double Height, int MaxLevel)
    {
        /// <summary>
        ///     Gets how far right of the life line the participant's self messages reach; zero without any.
        /// </summary>
        public double SelfReach { get; init; }
    }

    /// <summary>
    ///     The measures of an event.
    /// </summary>
    private sealed class EventInfo
    {
        /// <summary>Gets or sets the main text (a note, a group tab, a divider, a delay).</summary>
        public TextBlock? Text { get; set; }

        /// <summary>Gets or sets the label (a message label, a condition).</summary>
        public TextBlock? Label { get; set; }

        /// <summary>Gets or sets the number of a message.</summary>
        public TextBlock? Number { get; set; }

        /// <summary>Gets or sets the width the text needs.</summary>
        public double TextWidth { get; set; }

        /// <summary>Gets or sets the height of the text.</summary>
        public double TextHeight { get; set; }

        /// <summary>Gets or sets the height of the row.</summary>
        public double Height { get; set; }

        /// <summary>Gets or sets the activation level of the sender (or the first participant of a note).</summary>
        public int LevelFrom { get; set; }

        /// <summary>Gets or sets the activation level of the receiver (with an activation on the arrow).</summary>
        public int LevelTo { get; set; }

        /// <summary>Gets or sets the width of a group's header tab.</summary>
        public double TabWidth { get; set; }

        /// <summary>Gets or sets the height of a group's header tab.</summary>
        public double TabHeight { get; set; }
    }
}
