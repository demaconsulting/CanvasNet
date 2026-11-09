// Portions derived from mermaid (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Rendering;

namespace DemaConsulting.CanvasNet.Mermaid.Sequence;

/// <content>
///     The messages: the dry run that sizes the blocks (<c>calculateLoopBounds</c>), the message and note models
///     (<c>buildMessageModel</c>, <c>buildNoteModel</c>), and the draw loop that places every message, note,
///     activation and block (<c>boundMessage</c>, <c>drawNote</c>, <c>activeEnd</c>, <c>adjustLoopHeightForWrap</c>,
///     <c>adjustCreatedDestroyedData</c>).
/// </content>
internal sealed partial class SequenceLayout
{
    /// <summary>The offset of a central connection at the start of a message (<c>CENTRAL_CONNECTION_BASE_OFFSET</c>).</summary>
    private const double CentralConnectionBaseOffset = 4;

    /// <summary>The extra offset of a bidirectional central connection (<c>CENTRAL_CONNECTION_BIDIRECTIONAL_OFFSET</c>).</summary>
    private const double CentralConnectionBidirectionalOffset = 6;

    /// <summary>The line types whose end is not shortened for an arrowhead.</summary>
    private static readonly HashSet<int> FullLengthEnds =
    [
        SequenceLineType.SolidOpen, SequenceLineType.DottedOpen, SequenceLineType.StickTop, SequenceLineType.StickBottom,
        SequenceLineType.StickTopDotted, SequenceLineType.StickBottomDotted, SequenceLineType.SolidArrowTopReverseDotted,
        SequenceLineType.SolidArrowBottomReverseDotted, SequenceLineType.StickArrowTopReverse, SequenceLineType.StickArrowBottomReverse,
        SequenceLineType.StickArrowTopReverseDotted, SequenceLineType.StickArrowBottomReverseDotted, SequenceLineType.SolidArrowTopReverse,
        SequenceLineType.SolidArrowBottomReverse,
    ];

    /// <summary>The line types whose start is shortened for a second arrowhead.</summary>
    private static readonly HashSet<int> ShortenedStarts =
    [
        SequenceLineType.BidirectionalSolid, SequenceLineType.BidirectionalDotted, SequenceLineType.SolidArrowTopReverseDotted,
        SequenceLineType.SolidArrowBottomReverseDotted, SequenceLineType.SolidArrowTopReverse, SequenceLineType.SolidArrowBottomReverse,
    ];

    /// <summary>
    ///     Gets whether a line type starts a block.
    /// </summary>
    /// <param name="type">The line type.</param>
    /// <returns>Whether it does.</returns>
    private static bool IsBlockStart(int type) => type is SequenceLineType.LoopStart or SequenceLineType.AltStart or SequenceLineType.OptStart
        or SequenceLineType.ParStart or SequenceLineType.ParOverStart or SequenceLineType.CriticalStart or SequenceLineType.BreakStart;

    /// <summary>
    ///     Gets whether a line type starts a section of a block.
    /// </summary>
    /// <param name="type">The line type.</param>
    /// <returns>Whether it does.</returns>
    private static bool IsBlockSection(int type) =>
        type is SequenceLineType.AltElse or SequenceLineType.ParAnd or SequenceLineType.CriticalOption;

    /// <summary>
    ///     Gets whether a line type ends a block (other than <c>rect</c>).
    /// </summary>
    /// <param name="type">The line type.</param>
    /// <returns>Whether it does.</returns>
    private static bool IsBlockEnd(int type) => type is SequenceLineType.LoopEnd or SequenceLineType.AltEnd or SequenceLineType.OptEnd
        or SequenceLineType.ParEnd or SequenceLineType.CriticalEnd or SequenceLineType.BreakEnd;

    /// <summary>
    ///     Lays out every message of the model in order (the loop of Mermaid's <c>draw</c>): notes and activations are
    ///     drawn as they come, blocks are opened and closed, and the arrows are placed to be drawn after the participants.
    /// </summary>
    /// <param name="loopWidths">The widths of the blocks from the dry run, by message id.</param>
    /// <param name="messagesToDraw">The arrows to draw, filled in order.</param>
    private void DrawMessages(Dictionary<string, LoopWidth> loopWidths, List<(SequenceMessageState State, double LineStartY)> messagesToDraw)
    {
        var sequenceIndex = 1.0;
        var sequenceIndexStep = 1.0;
        for (var index = 0; index < _messages.Count; index++)
        {
            _meter.Charge(1);
            var state = _messages[index];
            var msg = state.Source;
            switch (msg.Type)
            {
                case SequenceLineType.Note:
                    DrawNote(state.NoteModel!, msg);
                    break;
                case SequenceLineType.ActiveStart or SequenceLineType.CentralConnection or SequenceLineType.CentralConnectionReverse:
                    NewActivation(msg.From);
                    break;
                case SequenceLineType.ActiveEnd:
                    ActiveEnd(msg, _bounds.VerticalPos);
                    break;
                case var type when IsBlockStart(type):
                    AdjustLoopHeightForWrap(loopWidths, state, _conf.BoxMargin, _conf.BoxMargin + _conf.BoxTextMargin, title => _bounds.NewLoop(title, null));
                    break;
                case var type when IsBlockSection(type):
                    AdjustLoopHeightForWrap(loopWidths, state, _conf.BoxMargin + _conf.BoxTextMargin, _conf.BoxMargin, _bounds.AddSectionToLoop);
                    break;
                case var type when IsBlockEnd(type):
                    EndLoop();
                    break;
                case SequenceLineType.RectStart:
                    AdjustLoopHeightForWrap(loopWidths, state, _conf.BoxMargin, _conf.BoxMargin, title => _bounds.NewLoop(null, RectFill(title.Message)));
                    break;
                case SequenceLineType.RectEnd:
                    EndLoop();
                    break;
                case SequenceLineType.Autonumber:
                    {
                        var numbering = msg.Autonumber!;
                        sequenceIndex = numbering.Start is { } start && start != 0 ? start : sequenceIndex;
                        sequenceIndexStep = numbering.Step is { } step && step != 0 ? step : sequenceIndexStep;
                        _sequenceNumbersEnabled = numbering.Visible;
                    }

                    break;
                default:
                    // An arrow: placed now, drawn after the participants so that its head is over the activations
                    if (state.MessageModel is { } model)
                    {
                        model.StartY = _bounds.VerticalPos;
                        model.SequenceIndex = sequenceIndex;
                        model.SequenceVisible = _sequenceNumbersEnabled;
                        var lineStartY = BoundMessage(model);
                        AdjustCreatedDestroyedData(msg, model, lineStartY, index);
                        messagesToDraw.Add((state, lineStartY));
                        _bounds.MessageModels.Add(model);
                    }

                    break;
            }

            // Every arrow advances the number, rounded to hundredths as Mermaid does against floating-point drift
            if (SequenceLineType.IsArrow(msg.Type))
            {
                sequenceIndex = ColorMath.JsRound((sequenceIndex + sequenceIndexStep) * 100) / 100;
            }
        }
    }

    /// <summary>
    ///     Gets the fill of a <c>rect</c> block: its color, else the theme's <c>rectBkgColor</c>, else its
    ///     <c>actorBkg</c>, else a translucent gray.
    /// </summary>
    /// <param name="color">The color of the block as written.</param>
    /// <returns>The fill.</returns>
    private string RectFill(string color)
    {
        if (color.Length > 0)
        {
            return color;
        }

        var config = _context.Config;
        if (config.ThemeVariable("rectBkgColor") is { Length: > 0 } rect)
        {
            return rect;
        }

        return config.ThemeVariable("actorBkg") is { Length: > 0 } actor ? actor : "rgba(128, 128, 128, 0.5)";
    }

    /// <summary>
    ///     Closes the innermost block and moves below it.
    /// </summary>
    private void EndLoop()
    {
        var loop = _bounds.EndLoop() ?? throw _meter.Failure("a block ends that was not started");
        _bounds.BumpVerticalPos((loop.StopY ?? double.NaN) - _bounds.VerticalPos);
        _bounds.LoopModels.Add(loop);
    }

    /// <summary>
    ///     Places a message line and grows the extent around it and its text (Mermaid's <c>boundMessage</c>).
    /// </summary>
    /// <param name="model">The message model.</param>
    /// <returns>The y of the line.</returns>
    private double BoundMessage(SequenceMessageModel model)
    {
        _bounds.BumpVerticalPos(10);
        var lines = LabelBuilder.SplitBreaks(model.Message).Count;
        var dimensions = _labels.CalculateTextDimensions(model.Message, _conf.MessageFont);
        var lineHeight = dimensions.Height / lines;
        model.Height += lineHeight;
        _bounds.BumpVerticalPos(lineHeight);

        double lineStartY;
        var totalOffset = dimensions.Height - 10;
        if (ColorMath.Same(model.StartX, model.StopX))
        {
            // A self message: a loop to the right of the lifeline
            lineStartY = _bounds.VerticalPos + totalOffset;
            if (!_conf.RightAngles)
            {
                totalOffset += _conf.BoxMargin;
                lineStartY = _bounds.VerticalPos + totalOffset;
            }

            totalOffset += 30;
            var dx = JsMax(dimensions.Width / 2, _conf.Width / 2);
            _bounds.Insert(
                model.StartX - dx,
                _bounds.VerticalPos - 10 + totalOffset,
                model.StopX + dx,
                _bounds.VerticalPos + 30 + totalOffset);
        }
        else
        {
            totalOffset += _conf.BoxMargin;
            lineStartY = _bounds.VerticalPos + totalOffset;
            _bounds.Insert(model.StartX, lineStartY - 10, model.StopX, lineStartY);
        }

        _bounds.BumpVerticalPos(totalOffset);
        model.Height += totalOffset;
        model.StopY = model.StartY + model.Height;
        _bounds.Insert(model.FromBounds, model.StartY, model.ToBounds, model.StopY);
        return lineStartY;
    }

    /// <summary>
    ///     Draws a note and moves below it (Mermaid's <c>drawNote</c>).
    /// </summary>
    /// <param name="model">The note model.</param>
    /// <param name="msg">The note's message.</param>
    private void DrawNote(SequenceNoteModel model, SequenceMessage msg)
    {
        _bounds.BumpVerticalPos(_conf.BoxMargin);
        model.Height = _conf.BoxMargin;
        model.StartY = _bounds.VerticalPos;
        var width = model.Width != 0 && !double.IsNaN(model.Width) ? model.Width : _conf.Width;
        var textHeight = _renderer.DrawNote(model.StartX, model.StartY, width, model.Message, msg);
        model.Height += textHeight + (2 * _conf.NoteMargin);
        _bounds.BumpVerticalPos(textHeight + (2 * _conf.NoteMargin));
        model.StopY = model.StartY + textHeight + (2 * _conf.NoteMargin);
        model.StopX = model.StartX + width;
        _bounds.Insert(model.StartX, model.StartY, model.StopX, model.StopY);
        _bounds.NoteModels.Add(model);
    }

    /// <summary>
    ///     Opens an activation of a participant at the vertical position (Mermaid's <c>newActivation</c>): stacked
    ///     activations move right by half the activation width each.
    /// </summary>
    /// <param name="actorId">The participant id.</param>
    private void NewActivation(string? actorId)
    {
        var actor = Actor(actorId);
        var stackedSize = _bounds.ActorActivations(actorId).Count;
        var x = actor.X + (actor.Width / 2) + ((stackedSize - 1) * _conf.ActivationWidth / 2);
        _bounds.Activations.Add(new SequenceActivation
        {
            StartX = x,
            StartY = _bounds.VerticalPos + 2,
            StopX = x + _conf.ActivationWidth,
            Actor = actorId,
            Anchor = _renderer.AnchorElement(),
        });
    }

    /// <summary>
    ///     Closes and draws the last activation of a participant (the <c>activeEnd</c> of Mermaid's <c>draw</c>): an
    ///     activation shorter than 18 pixels is grown to 12 pixels around the position.
    /// </summary>
    /// <param name="msg">The deactivation.</param>
    /// <param name="verticalPos">The vertical position.</param>
    private void ActiveEnd(SequenceMessage msg, double verticalPos)
    {
        var activation = _bounds.EndActivation(msg.From) ?? throw _meter.Failure($"'{msg.From}' is deactivated but has no activation");
        if (activation.StartY + 18 > verticalPos)
        {
            activation.StartY = verticalPos - 6;
            verticalPos += 12;
        }

        _renderer.DrawActivation(activation, verticalPos, _bounds.ActorActivations(msg.From).Count);
        _bounds.Insert(activation.StartX, verticalPos - 10, activation.StopX, verticalPos);
    }

    /// <summary>
    ///     Opens a block or a section, with the room for its title (Mermaid's <c>adjustLoopHeightForWrap</c>): a title is
    ///     put in brackets and wrapped to the width of the block.
    /// </summary>
    /// <param name="loopWidths">The widths of the blocks, by message id.</param>
    /// <param name="state">The message that opens the block or section.</param>
    /// <param name="preMargin">The space above.</param>
    /// <param name="postMargin">The space below without a title.</param>
    /// <param name="addLoop">Opens the block or section.</param>
    private void AdjustLoopHeightForWrap(
        Dictionary<string, LoopWidth> loopWidths,
        SequenceMessageState state,
        double preMargin,
        double postMargin,
        Action<SequenceMessageState> addLoop)
    {
        _bounds.BumpVerticalPos(preMargin);
        var heightAdjust = postMargin;
        if (state.Source.Id.Length > 0 && state.Message.Length > 0 && loopWidths.TryGetValue(state.Source.Id, out var loopWidth))
        {
            state.Message = _labels.WrapLabel($"[{state.Message}]", loopWidth.Width - (2 * _conf.WrapPadding), _conf.MessageFont);
            state.Width = loopWidth.Width;
            state.Wrap = true;
            var dimensions = _labels.CalculateTextDimensions(state.Message, _conf.MessageFont);
            heightAdjust = postMargin + JsMax(dimensions.Height, _conf.LabelBoxHeight);
        }

        addLoop(state);
        _bounds.BumpVerticalPos(heightAdjust);
    }

    /// <summary>
    ///     Moves the ends of a message that creates or destroys a participant to the participant's drawing, and the
    ///     participant to the message (Mermaid's <c>adjustCreatedDestroyedData</c>).
    /// </summary>
    /// <param name="msg">The message.</param>
    /// <param name="model">The message model.</param>
    /// <param name="lineStartY">The y of the line.</param>
    /// <param name="index">The index of the message.</param>
    private void AdjustCreatedDestroyedData(SequenceMessage msg, SequenceMessageModel model, double lineStartY, int index)
    {
        static bool IsFigure(SequenceActorLayout actor) => actor.Actor.Type is SequenceParticipantTypes.Actor or SequenceParticipantTypes.Control
            or SequenceParticipantTypes.Entity or SequenceParticipantTypes.Database;

        void ReceiverAdjustment(SequenceActorLayout actor, double adjustment)
        {
            if (actor.X < Actor(msg.From).X)
            {
                _bounds.Insert(model.StopX - adjustment, model.StartY, model.StartX, model.StopY + (actor.Height / 2) + _conf.NoteMargin);
                model.StopX += adjustment;
            }
            else
            {
                _bounds.Insert(model.StartX, model.StartY, model.StopX + adjustment, model.StopY + (actor.Height / 2) + _conf.NoteMargin);
                model.StopX -= adjustment;
            }
        }

        void SenderAdjustment(SequenceActorLayout actor, double adjustment)
        {
            if (actor.X < Actor(msg.To).X)
            {
                _bounds.Insert(model.StartX - adjustment, model.StartY, model.StopX, model.StopY + (actor.Height / 2) + _conf.NoteMargin);
                model.StartX += adjustment;
            }
            else
            {
                _bounds.Insert(model.StopX, model.StartY, model.StartX + adjustment, model.StopY + (actor.Height / 2) + _conf.NoteMargin);
                model.StartX -= adjustment;
            }
        }

        if (msg.To is { } to && _model.CreatedActors.TryGetValue(to, out var created) && created == index)
        {
            // A creating message ends at the side of the new participant, which is drawn at the message
            var actor = Actor(to);
            ReceiverAdjustment(actor, IsFigure(actor) ? (ActorTypeWidth / 2) + 3 : (actor.Width / 2) + 3);
            actor.StartY = lineStartY - (actor.Height / 2);
            _bounds.BumpVerticalPos(actor.Height / 2);
        }
        else if (msg.From is { } from && _model.DestroyedActors.TryGetValue(from, out var destroyedFrom) && destroyedFrom == index)
        {
            var actor = Actor(from);
            if (_conf.MirrorActors)
            {
                SenderAdjustment(actor, IsFigure(actor) ? ActorTypeWidth / 2 : actor.Width / 2);
            }

            actor.StopY = lineStartY - (actor.Height / 2);
            _bounds.BumpVerticalPos(actor.Height / 2);
        }
        else if (msg.To is { } target && _model.DestroyedActors.TryGetValue(target, out var destroyedTo) && destroyedTo == index)
        {
            var actor = Actor(target);
            if (_conf.MirrorActors)
            {
                ReceiverAdjustment(actor, IsFigure(actor) ? (ActorTypeWidth / 2) + 3 : (actor.Width / 2) + 3);
            }

            actor.StopY = lineStartY - (actor.Height / 2);
            _bounds.BumpVerticalPos(actor.Height / 2);
        }
    }

    /// <summary>
    ///     Sizes the blocks in a dry run over the messages (Mermaid's <c>calculateLoopBounds</c>), building the message and
    ///     note models on the way with the activations of the dry run.
    /// </summary>
    /// <returns>The width of each block, by the id of its start and section messages.</returns>
    private Dictionary<string, LoopWidth> CalculateLoopBounds()
    {
        var loops = new Dictionary<string, LoopWidth>(StringComparer.Ordinal);
        var stack = new List<LoopWidth>();
        foreach (var state in _messages)
        {
            _meter.Charge(stack.Count + 1L);
            var msg = state.Source;
            switch (msg.Type)
            {
                case var type when IsBlockStart(type):
                    stack.Add(new LoopWidth(msg.Id) { From = MaxSafeInteger, To = -MaxSafeInteger });
                    break;
                case var type when IsBlockSection(type):
                    if (state.Message.Length > 0 && stack.Count > 0)
                    {
                        var current = stack[^1];
                        loops[current.Id] = current;
                        loops[msg.Id] = current;
                    }

                    break;
                case var type when IsBlockEnd(type):
                    if (stack.Count > 0)
                    {
                        var current = stack[^1];
                        stack.RemoveAt(stack.Count - 1);
                        loops[current.Id] = current;
                    }

                    break;
                case SequenceLineType.ActiveStart:
                    {
                        var actor = Actor(msg.From);
                        var stackedSize = _bounds.ActorActivations(msg.From).Count;
                        var x = actor.X + (actor.Width / 2) + ((stackedSize - 1) * _conf.ActivationWidth / 2);
                        _bounds.Activations.Add(new SequenceActivation { StartX = x, StopX = x + _conf.ActivationWidth, Actor = msg.From });
                    }

                    break;
                case SequenceLineType.ActiveEnd:
                    _bounds.EndActivation(msg.From);
                    break;
            }

            if (msg.Placement is not null)
            {
                var note = BuildNoteModel(state);
                state.NoteModel = note;
                foreach (var current in stack)
                {
                    current.From = JsMin(current.From, note.StartX);
                    current.To = JsMax(current.To, note.StartX + note.Width);
                    current.Width = JsMax(current.Width, Math.Abs(current.From - current.To)) - _conf.LabelBoxWidth;
                }

                continue;
            }

            var model = BuildMessageModel(state);
            state.MessageModel = model;
            if (model is null || IsFalsy(model.StartX) || IsFalsy(model.StopX))
            {
                continue;
            }

            foreach (var current in stack)
            {
                if (ColorMath.Same(model.StartX, model.StopX))
                {
                    var from = Actor(msg.From);
                    var to = Actor(msg.To);
                    current.From = JsMin(from.X - (model.Width / 2), from.X - (from.Width / 2), current.From);
                    current.To = JsMax(to.X + (model.Width / 2), to.X + (from.Width / 2), current.To);
                    current.Width = JsMax(current.Width, Math.Abs(current.To - current.From)) - _conf.LabelBoxWidth;
                }
                else
                {
                    current.From = JsMin(model.StartX, current.From);
                    current.To = JsMax(model.StopX, current.To);
                    current.Width = JsMax(current.Width, model.Width) - _conf.LabelBoxWidth;
                }
            }
        }

        _bounds.Activations = [];
        return loops;
    }

    /// <summary>
    ///     Builds the model of a note (Mermaid's <c>buildNoteModel</c>): its width and left edge by placement, and its
    ///     text wrapped when the note wraps.
    /// </summary>
    /// <param name="state">The note's message.</param>
    /// <returns>The note model.</returns>
    private SequenceNoteModel BuildNoteModel(SequenceMessageState state)
    {
        var msg = state.Source;
        var font = _conf.NoteFont;
        var fromActor = Actor(msg.From);
        var toActor = Actor(msg.To);
        var startx = fromActor.X;
        var stopx = toActor.X;
        var shouldWrap = state.Wrap && state.Message.Length > 0;
        var dimensions = _labels.CalculateTextDimensions(shouldWrap ? _labels.WrapLabel(state.Message, _conf.Width, font) : state.Message, font);
        var note = new SequenceNoteModel
        {
            Width = shouldWrap ? _conf.Width : JsMax(_conf.Width, dimensions.Width + (2 * _conf.NoteMargin)),
            StartX = fromActor.X,
            Message = state.Message,
        };

        if (msg.Placement == SequencePlacement.RightOf)
        {
            note.Width = shouldWrap
                ? JsMax(_conf.Width, dimensions.Width)
                : JsMax((fromActor.Width / 2) + (toActor.Width / 2), dimensions.Width + (2 * _conf.NoteMargin));
            note.StartX = startx + ((fromActor.Width + _conf.ActorMargin) / 2);
        }
        else if (msg.Placement == SequencePlacement.LeftOf)
        {
            note.Width = shouldWrap
                ? JsMax(_conf.Width, dimensions.Width + (2 * _conf.NoteMargin))
                : JsMax((fromActor.Width / 2) + (toActor.Width / 2), dimensions.Width + (2 * _conf.NoteMargin));
            note.StartX = startx - note.Width + ((fromActor.Width - _conf.ActorMargin) / 2);
        }
        else if (msg.To == msg.From)
        {
            dimensions = _labels.CalculateTextDimensions(
                shouldWrap ? _labels.WrapLabel(state.Message, JsMax(_conf.Width, fromActor.Width), font) : state.Message,
                font);
            note.Width = shouldWrap
                ? JsMax(_conf.Width, fromActor.Width)
                : JsMax(fromActor.Width, _conf.Width, dimensions.Width + (2 * _conf.NoteMargin));
            note.StartX = startx + ((fromActor.Width - note.Width) / 2);
        }
        else
        {
            note.Width = Math.Abs(startx + (fromActor.Width / 2) - (stopx + (toActor.Width / 2))) + _conf.ActorMargin;
            note.StartX = startx < stopx
                ? startx + (fromActor.Width / 2) - (_conf.ActorMargin / 2)
                : stopx + (toActor.Width / 2) - (_conf.ActorMargin / 2);
        }

        if (shouldWrap)
        {
            note.Message = _labels.WrapLabel(state.Message, note.Width - (2 * _conf.WrapPadding), font);
        }

        return note;
    }

    /// <summary>
    ///     Builds the model of an arrow (Mermaid's <c>buildMessageModel</c>): its ends at the activations of both
    ///     participants, shortened for the arrowheads and moved for central connections, and its text wrapped when the
    ///     message wraps.
    /// </summary>
    /// <param name="state">The message.</param>
    /// <returns>The model, or null when the message is not an arrow.</returns>
    private SequenceMessageModel? BuildMessageModel(SequenceMessageState state)
    {
        var msg = state.Source;
        if (!SequenceLineType.IsArrow(msg.Type))
        {
            return null;
        }

        var (fromLeft, fromRight) = ActivationBounds(msg.From);
        var (toLeft, toRight) = ActivationBounds(msg.To);
        var isArrowToRight = fromLeft <= toLeft;
        var startx = isArrowToRight ? fromRight : fromLeft;
        var stopx = isArrowToRight ? toLeft : toRight;
        startx += CentralConnectionOffset(msg, isArrowToRight);

        // The line width makes the left and right of a lifeline 2 apart; more is an activation
        var isArrowToActivation = Math.Abs(toLeft - toRight) > 2;
        double Adjust(double value) => isArrowToRight ? -value : value;
        if (msg.From == msg.To)
        {
            stopx = startx;
        }
        else
        {
            if (msg.Activate == true && !isArrowToActivation)
            {
                stopx += Adjust((_conf.ActivationWidth / 2) - 1);
            }

            if (!FullLengthEnds.Contains(msg.Type))
            {
                stopx += Adjust(3);
            }

            if (ShortenedStarts.Contains(msg.Type))
            {
                startx -= Adjust(3);
            }
        }

        var boundedWidth = Math.Abs(startx - stopx);
        if (state.Wrap && state.Message.Length > 0)
        {
            state.Message = _labels.WrapLabel(state.Message, JsMax(boundedWidth + (2 * _conf.WrapPadding), _conf.Width), _conf.MessageFont);
        }

        var dimensions = _labels.CalculateTextDimensions(state.Message, _conf.MessageFont);
        return new SequenceMessageModel
        {
            Width = JsMax(state.Wrap ? 0 : dimensions.Width + (2 * _conf.WrapPadding), boundedWidth + (2 * _conf.WrapPadding), _conf.Width),
            StartX = startx,
            StopX = stopx,
            Message = state.Message,
            Type = msg.Type,
            FromBounds = Math.Min(Math.Min(fromLeft, fromRight), Math.Min(toLeft, toRight)),
            ToBounds = Math.Max(Math.Max(fromLeft, fromRight), Math.Max(toLeft, toRight)),
        };
    }

    /// <summary>
    ///     Gets the offset of the start of a message with a central connection at its start
    ///     (<c>calculateCentralConnectionOffset</c>).
    /// </summary>
    /// <param name="msg">The message.</param>
    /// <param name="isArrowToRight">Whether the arrow points right.</param>
    /// <returns>The offset.</returns>
    private static double CentralConnectionOffset(SequenceMessage msg, bool isArrowToRight)
    {
        var atStart = msg.CentralConnection is SequenceLineType.CentralConnectionReverse or SequenceLineType.CentralConnectionDual;
        var offset = atStart ? CentralConnectionBaseOffset : 0;
        if (atStart && msg.Type is SequenceLineType.BidirectionalSolid or SequenceLineType.BidirectionalDotted)
        {
            offset += isArrowToRight ? 0 : -CentralConnectionBidirectionalOffset;
        }

        return offset;
    }

    /// <summary>
    ///     Gets the left and right of a participant's lifeline with its open activations (Mermaid's
    ///     <c>activationBounds</c>).
    /// </summary>
    /// <param name="actorId">The participant id.</param>
    /// <returns>The left and right.</returns>
    private (double Left, double Right) ActivationBounds(string? actorId)
    {
        var actor = Actor(actorId);
        var activations = _bounds.ActorActivations(actorId);
        var left = (actor.X + (actor.Width / 2)) - 1;
        var right = actor.X + (actor.Width / 2) + 1;
        foreach (var activation in activations)
        {
            left = JsMin(left, activation.StartX);
            right = JsMax(right, activation.StopX);
        }

        return (left, right);
    }

    /// <summary>
    ///     Gets the layout of a participant a message names.
    /// </summary>
    /// <param name="actorId">The participant id.</param>
    /// <returns>The layout.</returns>
    /// <exception cref="MermaidRenderException">Thrown when the participant does not exist (Mermaid fails too).</exception>
    private SequenceActorLayout Actor(string? actorId) =>
        actorId is not null && _actors.TryGetValue(actorId, out var actor)
            ? actor
            : throw _meter.Failure($"a message names the participant '{actorId}', which does not exist");

    /// <summary>
    ///     Gets whether a number is falsy in JavaScript (zero or not a number).
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>Whether it is falsy.</returns>
    private static bool IsFalsy(double value) => value == 0 || double.IsNaN(value);

    /// <summary>
    ///     The extent of a block in the dry run (an entry of Mermaid's <c>loops</c>).
    /// </summary>
    /// <param name="id">The id of the message that starts the block.</param>
    private sealed class LoopWidth(string id)
    {
        /// <summary>Gets the id of the message that starts the block.</summary>
        public string Id { get; } = id;

        /// <summary>Gets or sets the leftmost x of the items inside.</summary>
        public double From { get; set; }

        /// <summary>Gets or sets the rightmost x of the items inside.</summary>
        public double To { get; set; }

        /// <summary>Gets or sets the width.</summary>
        public double Width { get; set; }
    }
}
