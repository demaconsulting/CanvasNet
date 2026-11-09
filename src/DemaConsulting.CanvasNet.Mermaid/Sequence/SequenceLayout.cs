// Portions derived from mermaid (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Dagre;
using DemaConsulting.CanvasNet.Mermaid.Rendering;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Sequence;

/// <summary>
///     The sequence layout: a port of Mermaid's sequence renderer (<c>sequenceRenderer.ts</c>'s <c>draw</c>), which
///     sizes the participants, places every message, note, activation and block from top to bottom, and has the
///     <see cref="SequenceRenderer"/> draw them into a scene.
/// </summary>
/// <remarks>
///     <para>
///         <b>Algorithm.</b> As in Mermaid: the widest message between neighboring participants sets their margins
///         (<c>getMaxMessageWidthPerActor</c>, <c>calculateActorMargins</c>); the participants are placed from left to
///         right (<c>addActorRenderingData</c>); a dry run sizes the blocks (<c>calculateLoopBounds</c>); then every
///         message of the model is laid out in order with Mermaid's <c>bounds</c> object (<see cref="SequenceBounds"/>),
///         which grows the extent of the drawing and of the open blocks. The participants are drawn at the top, the
///         messages over them, the mirrored participants at the bottom, and the view box is the extent with the diagram
///         margins.
///     </para>
///     <para>
///         <b>Text.</b> Every text is measured with <see cref="LabelBuilder.CalculateTextDimensions"/> and wrapped with
///         <see cref="LabelBuilder.WrapLabel"/>, in Mermaid's order, by one label builder for the whole render (so the
///         memo and the state of Mermaid's line-break test are those of one render).
///     </para>
///     <para>
///         <b>Cost.</b> The text measurement, every insert into the bounds (which grows every open block and
///         activation) and every scan of the activations charge the work meter of the render, which also checks for
///         cancellation; a diagram that needs more than <see cref="MermaidLimits.MaxLayoutWork"/> fails with
///         <see cref="MermaidDiagnosticCodes.LimitExceeded"/>.
///     </para>
///     <para>
///         An instance lays out one render on one thread; the model is only read.
///     </para>
/// </remarks>
internal sealed partial class SequenceLayout
{
    /// <summary>The width of the drawing of an <c>actor</c>, <c>control</c>, <c>entity</c> or <c>database</c> (<c>ACTOR_TYPE_WIDTH</c>).</summary>
    internal const double ActorTypeWidth = 36;

    /// <summary>JavaScript's <c>Number.MAX_SAFE_INTEGER</c>.</summary>
    private const double MaxSafeInteger = 9007199254740991;

    /// <summary>The model.</summary>
    private readonly SequenceModel _model;

    /// <summary>The context of the render.</summary>
    private readonly DiagramRenderContext _context;

    /// <summary>The sequence configuration of the render (Mermaid's <c>conf</c>).</summary>
    private readonly SequenceConfig _conf;

    /// <summary>The label builder of the render: text measurement and wrapping.</summary>
    private readonly LabelBuilder _labels;

    /// <summary>The work meter of the render.</summary>
    private readonly LayoutWorkMeter _meter;

    /// <summary>The bounds of the render.</summary>
    private readonly SequenceBounds _bounds;

    /// <summary>The renderer that draws the scene.</summary>
    private readonly SequenceRenderer _renderer;

    /// <summary>The layouts of the participants, by id.</summary>
    private readonly Dictionary<string, SequenceActorLayout> _actors = new(StringComparer.Ordinal);

    /// <summary>The layouts of the boxes, by box.</summary>
    private readonly Dictionary<SequenceBox, SequenceBoxLayout> _boxes = new(ReferenceEqualityComparer.Instance);

    /// <summary>The per-render states of the messages, in order.</summary>
    private readonly List<SequenceMessageState> _messages;

    /// <summary>The <c>rect</c> blocks, in the order they end (Mermaid's <c>backgrounds</c>), drawn behind everything.</summary>
    private readonly List<SequenceLoopModel> _backgrounds = [];

    /// <summary>The height of every participant (Mermaid's <c>conf.height</c> after <c>calculateActorMargins</c>).</summary>
    private double _actorHeight;

    /// <summary>Whether <c>autonumber</c> turned the numbers on (the database's <c>sequenceNumbersEnabled</c>).</summary>
    private bool _sequenceNumbersEnabled;

    /// <summary>
    ///     Initializes the layout of one render.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="context">The context of the render.</param>
    public SequenceLayout(SequenceModel model, DiagramRenderContext context)
    {
        _model = model;
        _context = context;
        _conf = context.Config.Sequence;
        _meter = new LayoutWorkMeter(context.Options.Limits.MaxLayoutWork, model.KeywordLocation, context.CancellationToken);
        _labels = new LabelBuilder(context.Fonts, context.Sink, null) { Meter = _meter };
        _bounds = new SequenceBounds(_conf.BoxMargin, _meter);
        _renderer = new SequenceRenderer(_conf, SequenceStyles.Create(context.Config, context.Sink), _labels, context.Config);
        foreach (var actor in model.Actors)
        {
            _actors[actor.Id] = new SequenceActorLayout(actor, SequenceModel.EffectiveWrap(actor.Wrap, _conf));
        }

        foreach (var box in model.Boxes)
        {
            _boxes[box] = new SequenceBoxLayout(box);
        }

        foreach (var actor in _actors.Values)
        {
            actor.Box = actor.Actor.Box is { } box ? _boxes[box] : null;
        }

        _messages = [.. model.Messages.Select(message => new SequenceMessageState(message, SequenceModel.EffectiveWrap(message.Wrap, _conf)))];
    }

    /// <summary>
    ///     Gets the largest of numbers as Mermaid's <c>common.getMax</c> does: values that are not set or not a number are
    ///     left out.
    /// </summary>
    /// <param name="values">The values.</param>
    /// <returns>The largest, or negative infinity when there is none.</returns>
    internal static double JsMax(params double?[] values)
    {
        var result = double.NegativeInfinity;
        foreach (var value in values)
        {
            if (value is { } v && !double.IsNaN(v))
            {
                result = Math.Max(result, v);
            }
        }

        return result;
    }

    /// <summary>
    ///     Gets the smallest of numbers as Mermaid's <c>common.getMin</c> does: values that are not set or not a number are
    ///     left out.
    /// </summary>
    /// <param name="values">The values.</param>
    /// <returns>The smallest, or positive infinity when there is none.</returns>
    internal static double JsMin(params double?[] values)
    {
        var result = double.PositiveInfinity;
        foreach (var value in values)
        {
            if (value is { } v && !double.IsNaN(v))
            {
                result = Math.Min(result, v);
            }
        }

        return result;
    }

    /// <summary>
    ///     Lays the diagram out and draws it (Mermaid's <c>draw</c>).
    /// </summary>
    /// <returns>The scene, whose view box is the natural size.</returns>
    /// <exception cref="MermaidRenderException">Thrown when the work limit is passed or the layout fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the render is canceled.</exception>
    public Scene Draw()
    {
        // The margins of the participants and boxes from the widest texts between them
        var maxMessageWidthPerActor = GetMaxMessageWidthPerActor();
        _actorHeight = CalculateActorMargins(maxMessageWidthPerActor);

        // The room for the boxes and their titles above the participants
        if (_model.HasAtLeastOneBox)
        {
            _bounds.BumpVerticalPos(_conf.BoxMargin);
            if (_model.HasAtLeastOneBoxWithTitle)
            {
                _bounds.BumpVerticalPos(_boxes[_model.Boxes[0]].TextMaxHeight);
            }
        }

        // The participants drawn: all of them, or those that a message names
        var actorKeys = _model.Actors.Select(actor => actor.Id).ToList();
        if (_conf.HideUnusedParticipants)
        {
            var used = new HashSet<string?>(StringComparer.Ordinal);
            foreach (var message in _model.Messages)
            {
                used.Add(message.From);
                used.Add(message.To);
            }

            actorKeys = actorKeys.FindAll(used.Contains);
        }

        AddActorRenderingData(actorKeys);
        var loopWidths = CalculateLoopBounds();

        // Every message in order; the arrows are drawn after the participants
        var messagesToDraw = new List<(SequenceMessageState State, double LineStartY)>();
        DrawMessages(loopWidths, messagesToDraw);
        DrawActors(actorKeys, isFooter: false);
        foreach (var (state, lineStartY) in messagesToDraw)
        {
            _renderer.DrawMessage(state, lineStartY, _actors, _conf.ShowSequenceNumbers);
        }

        if (_conf.MirrorActors)
        {
            DrawActors(actorKeys, isFooter: true);
        }

        // The rect backgrounds go behind everything drawn so far, then the lifelines end and the boxes go behind them
        foreach (var background in _backgrounds)
        {
            _renderer.DrawBackgroundRect(background);
        }

        _renderer.FixLifeLineHeights(actorKeys.Select(key => _actors[key]));
        LayoutBoxes();
        var requiredBoxSize = DrawActorsPopup(actorKeys);
        return ViewBox(requiredBoxSize);
    }

    /// <summary>
    ///     Gets the width of the widest message or note next to each participant (Mermaid's
    ///     <c>getMaxMessageWidthPerActor</c>): only messages between neighbors, self messages and notes count.
    /// </summary>
    /// <returns>The widths by participant id.</returns>
    private Dictionary<string, double> GetMaxMessageWidthPerActor()
    {
        var widths = new Dictionary<string, double>(StringComparer.Ordinal);
        void Grow(string? key, double width)
        {
            var actorKey = key ?? "undefined";
            widths[actorKey] = JsMax(widths.GetValueOrDefault(actorKey), width);
        }

        foreach (var state in _messages)
        {
            var msg = state.Source;
            if (msg.To is null || msg.From is null || !_actors.TryGetValue(msg.To, out var layout) || !_actors.ContainsKey(msg.From))
            {
                continue;
            }

            // A note left of the first or right of the last participant needs no margin
            var actor = layout.Actor;
            if ((msg.Placement == SequencePlacement.LeftOf && actor.PrevActor is null) ||
                (msg.Placement == SequencePlacement.RightOf && actor.NextActor is null))
            {
                continue;
            }

            var isNote = msg.Placement is not null;
            var font = isNote ? _conf.NoteFont : _conf.MessageFont;
            var wrapped = state.Wrap ? _labels.WrapLabel(state.Message, _conf.Width - (2 * _conf.WrapPadding), font) : state.Message;
            var messageWidth = _labels.CalculateTextDimensions(wrapped, font).Width + (2 * _conf.WrapPadding);
            if (!isNote && msg.From == actor.NextActor)
            {
                Grow(msg.To, messageWidth);
            }
            else if (!isNote && msg.From == actor.PrevActor)
            {
                Grow(msg.From, messageWidth);
            }
            else if (!isNote && msg.From == msg.To)
            {
                Grow(msg.From, messageWidth / 2);
                Grow(msg.To, messageWidth / 2);
            }
            else if (msg.Placement == SequencePlacement.RightOf)
            {
                Grow(msg.From, messageWidth);
            }
            else if (msg.Placement == SequencePlacement.LeftOf)
            {
                Grow(actor.PrevActor, messageWidth);
            }
            else if (msg.Placement == SequencePlacement.Over)
            {
                if (actor.PrevActor is not null)
                {
                    Grow(actor.PrevActor, messageWidth / 2);
                }

                if (actor.NextActor is not null)
                {
                    Grow(msg.From, messageWidth / 2);
                }
            }
        }

        return widths;
    }

    /// <summary>
    ///     Sizes the participants and sets their margins and those of the boxes (Mermaid's <c>calculateActorMargins</c>).
    /// </summary>
    /// <param name="actorToMessageWidth">The widest message next to each participant.</param>
    /// <returns>The height of the tallest participant, at least the configured height.</returns>
    private double CalculateActorMargins(Dictionary<string, double> actorToMessageWidth)
    {
        // The size of each participant from its description
        var maxHeight = 0.0;
        foreach (var actor in _model.Actors.Select(actor => _actors[actor.Id]))
        {
            if (actor.Wrap)
            {
                actor.Description = _labels.WrapLabel(actor.Description, _conf.Width - (2 * _conf.WrapPadding), _conf.ActorFont);
            }

            var dimensions = _labels.CalculateTextDimensions(actor.Description, _conf.ActorFont);
            actor.Width = actor.Wrap ? _conf.Width : JsMax(_conf.Width, dimensions.Width + (2 * _conf.WrapPadding));
            actor.Height = actor.Wrap ? JsMax(dimensions.Height, _conf.Height) : _conf.Height;
            maxHeight = JsMax(maxHeight, actor.Height);
        }

        // The margin of each participant: room for its widest message to the next one
        foreach (var (key, messageWidth) in actorToMessageWidth)
        {
            if (!_actors.TryGetValue(key, out var actor))
            {
                continue;
            }

            var next = actor.Actor.NextActor is { } nextId ? _actors.GetValueOrDefault(nextId) : null;
            var actorWidth = next is null
                ? messageWidth + _conf.ActorMargin - (actor.Width / 2)
                : messageWidth + _conf.ActorMargin - (actor.Width / 2) - (next.Width / 2);
            actor.Margin = JsMax(actorWidth, _conf.ActorMargin);
        }

        // The margin of each box: room for its title
        var maxBoxHeight = 0.0;
        foreach (var box in _model.Boxes.Select(box => _boxes[box]))
        {
            var font = _conf.MessageFont;
            var totalWidth = box.Box.ActorKeys.Sum(key => _actors[key].Width + (_actors[key].Margin is { } margin && margin != 0 ? margin : 0));
            totalWidth += _conf.BoxMargin * 8;
            totalWidth -= 2 * _conf.BoxTextMargin;
            if (SequenceModel.EffectiveWrap(box.Box.Wrap, _conf) && box.Name is { Length: > 0 } name)
            {
                box.Name = _labels.WrapLabel(name, totalWidth - (2 * _conf.WrapPadding), font);
            }

            var dimensions = _labels.CalculateTextDimensions(box.Name ?? string.Empty, font);
            maxBoxHeight = JsMax(dimensions.Height, maxBoxHeight);
            var minWidth = JsMax(totalWidth, dimensions.Width + (2 * _conf.WrapPadding));
            box.Margin = _conf.BoxTextMargin;
            if (totalWidth < minWidth)
            {
                box.Margin += (minWidth - totalWidth) / 2;
            }
        }

        foreach (var box in _boxes.Values)
        {
            box.TextMaxHeight = maxBoxHeight;
        }

        return JsMax(maxHeight, _conf.Height);
    }

    /// <summary>
    ///     Places the participants from left to right, with their boxes (Mermaid's <c>addActorRenderingData</c> for the
    ///     top row).
    /// </summary>
    /// <param name="actorKeys">The ids of the participants drawn, in order.</param>
    private void AddActorRenderingData(List<string> actorKeys)
    {
        const double VerticalPos = 0;
        var prevWidth = 0.0;
        var prevMargin = 0.0;
        SequenceBoxLayout? prevBox = null;
        var maxHeight = 0.0;
        foreach (var actor in actorKeys.Select(key => _actors[key]))
        {
            var box = actor.Box;

            // The end of a box, then the start of a new one
            if (prevBox is not null && prevBox != box)
            {
                _bounds.BoxModels.Add(prevBox);
                prevMargin += _conf.BoxMargin + prevBox.Margin;
            }

            if (box is not null && box != prevBox)
            {
                box.X = prevWidth + prevMargin;
                box.Y = VerticalPos;
                prevMargin += box.Margin;
            }

            // The size and margin of the participant; a created participant gets half its width more room
            actor.Width = JsMax(actor.Width != 0 ? actor.Width : _conf.Width, _conf.Width);
            actor.Height = JsMax(actor.Height != 0 ? actor.Height : _actorHeight, _actorHeight);
            actor.Margin = actor.Margin is { } margin && margin != 0 ? margin : _conf.ActorMargin;
            maxHeight = JsMax(maxHeight, actor.Height);
            if (_model.CreatedActors.TryGetValue(actor.Actor.Name, out var created) && created != 0)
            {
                prevMargin += actor.Width / 2;
            }

            actor.X = prevWidth + prevMargin;
            actor.StartY = _bounds.VerticalPos;
            _bounds.Insert(actor.X, VerticalPos, actor.X + actor.Width, actor.Height);
            prevWidth += actor.Width + prevMargin;
            if (box is not null)
            {
                box.Width = prevWidth + box.Margin - box.X;
            }

            prevMargin = actor.Margin.Value;
            prevBox = box;
            _bounds.ActorModels.Add(actor);
        }

        if (prevBox is not null)
        {
            _bounds.BoxModels.Add(prevBox);
        }

        // A margin between the participants and the first message
        _bounds.BumpVerticalPos(maxHeight);
    }

    /// <summary>
    ///     Draws the participants at the top or the mirrored ones at the bottom (Mermaid's <c>drawActors</c>).
    /// </summary>
    /// <param name="actorKeys">The ids of the participants drawn.</param>
    /// <param name="isFooter">Whether the mirrored participants are drawn.</param>
    private void DrawActors(List<string> actorKeys, bool isFooter)
    {
        if (!isFooter)
        {
            foreach (var key in actorKeys)
            {
                _renderer.DrawActor(_actors[key], false);
            }

            return;
        }

        var maxHeight = 0.0;
        _bounds.BumpVerticalPos(_conf.BoxMargin * 2);
        foreach (var actor in actorKeys.Select(key => _actors[key]))
        {
            if (actor.StopY is not { } stopY || stopY == 0 || double.IsNaN(stopY))
            {
                actor.StopY = _bounds.VerticalPos;
            }

            maxHeight = JsMax(maxHeight, _renderer.DrawActor(actor, true));
        }

        _bounds.BumpVerticalPos(maxHeight + _conf.BoxMargin);
    }

    /// <summary>
    ///     Gives the boxes their height, adds them to the extent and draws them behind everything (the part of Mermaid's
    ///     <c>draw</c> after the lifelines), then leaves the box margin below them.
    /// </summary>
    private void LayoutBoxes()
    {
        foreach (var box in _bounds.BoxModels)
        {
            box.Height = _bounds.VerticalPos - box.Y;
            _bounds.Insert(box.X, box.Y, box.X + box.Width, box.Height);
            _renderer.DrawBox(box);
        }

        if (_model.HasAtLeastOneBox)
        {
            _bounds.BumpVerticalPos(_conf.BoxMargin);
        }
    }

    /// <summary>
    ///     Draws the title and makes the scene with Mermaid's view box: the extent (at least the size the menus need)
    ///     with the diagram margins, the room of the title above it, and the mirrored participants' bottom margin.
    /// </summary>
    /// <param name="requiredBoxSize">The height and right edge the menus need.</param>
    /// <returns>The scene.</returns>
    private Scene ViewBox((double MaxHeight, double MaxWidth) requiredBoxSize)
    {
        var startx = _bounds.StartX ?? 0;
        var starty = _bounds.StartY ?? 0;
        var stopx = _bounds.StopX ?? 0;
        var stopy = _bounds.StopY ?? 0;
        var boxHeight = Math.Max(stopy - starty, requiredBoxSize.MaxHeight);
        var height = boxHeight + (2 * _conf.DiagramMarginY);
        if (_conf.MirrorActors)
        {
            height = height - _conf.BoxMargin + _conf.BottomMarginAdj;
        }

        var width = Math.Max(stopx - startx, requiredBoxSize.MaxWidth) + (2 * _conf.DiagramMarginX);

        // The title: the diagram's own, else the frontmatter's
        var title = _model.Title ?? (_context.Source.Frontmatter?.Title is { Length: > 0 } frontmatter ? HtmlSanitizer.Sanitize(frontmatter) : null);
        if (!string.IsNullOrEmpty(title))
        {
            _renderer.DrawTitle(title, ((stopx - startx) / 2) - (2 * _conf.DiagramMarginX), -25);
        }

        var extraVertForTitle = string.IsNullOrEmpty(title) ? 0 : 40;
        var viewBox = new SceneRect(
            startx - _conf.DiagramMarginX,
            -(_conf.DiagramMarginY + extraVertForTitle),
            width,
            height + extraVertForTitle);
        return new Scene(viewBox, _renderer.Items());
    }
}
