// Portions derived from mermaid (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Dagre;

namespace DemaConsulting.CanvasNet.Mermaid.Sequence;

/// <summary>
///     A participant as one render lays it out: Mermaid's actor object with the fields its renderer adds (the
///     description after wrapping, the size, the margin, the position and the vertical extent).
/// </summary>
/// <remarks>The object belongs to one render on one thread; the model's participant is not changed.</remarks>
internal sealed class SequenceActorLayout
{
    /// <summary>
    ///     Initializes the layout of a participant.
    /// </summary>
    /// <param name="actor">The participant.</param>
    /// <param name="wrap">The effective wrap setting of its description.</param>
    public SequenceActorLayout(SequenceActor actor, bool wrap)
    {
        Actor = actor;
        Description = actor.Description;
        Wrap = wrap;
    }

    /// <summary>Gets the participant.</summary>
    public SequenceActor Actor { get; }

    /// <summary>Gets the description to draw (wrapped when the participant wraps).</summary>
    public string Description { get; set; }

    /// <summary>Gets a value indicating whether the description wraps.</summary>
    public bool Wrap { get; }

    /// <summary>Gets or sets the width.</summary>
    public double Width { get; set; }

    /// <summary>Gets or sets the height.</summary>
    public double Height { get; set; }

    /// <summary>Gets or sets the margin to the next participant, or null when none was computed.</summary>
    public double? Margin { get; set; }

    /// <summary>Gets or sets the left edge.</summary>
    public double X { get; set; }

    /// <summary>Gets or sets the top of the participant's drawing at the top of the diagram.</summary>
    public double StartY { get; set; }

    /// <summary>Gets or sets the top of the mirrored drawing (or where a destroyed participant ends), or null.</summary>
    public double? StopY { get; set; }

    /// <summary>Gets or sets the layout of the participant's box, or null.</summary>
    public SequenceBoxLayout? Box { get; set; }

    /// <summary>
    ///     Gets or sets the number of the participant among those drawn at the top (Mermaid's <c>actorCnt</c>, from 0),
    ///     or -1 before it is drawn; it names the participant's menu (<c>actor{n}_popup</c>).
    /// </summary>
    public int ActorCnt { get; set; } = -1;

    /// <summary>
    ///     Gets or sets the box of the participant's last drawing (Mermaid's <c>rectData</c>), which places its menu, or
    ///     null for the types that have none (<c>actor</c>, <c>boundary</c>, <c>control</c>, <c>entity</c>).
    /// </summary>
    public SequenceRectData? RectData { get; set; }
}

/// <summary>
///     The box Mermaid keeps of a participant's drawing (its <c>rectData</c>): what places and paints the participant's
///     menu.
/// </summary>
/// <param name="X">The left edge.</param>
/// <param name="Height">The height (the menu panel's top, as Mermaid uses it).</param>
/// <param name="Width">The width.</param>
/// <param name="Radius">The corner radius.</param>
/// <param name="Classes">The CSS classes of the drawing's box (<c>actor actor-top</c>, or the <c>properties</c> class).</param>
/// <param name="Fill">The fill attribute of the box.</param>
internal sealed record SequenceRectData(double X, double Height, double Width, double Radius, string Classes, Rgba32 Fill);

/// <summary>
///     A box as one render lays it out (Mermaid's box object with the renderer's fields).
/// </summary>
/// <param name="box">The box of the model.</param>
internal sealed class SequenceBoxLayout(SequenceBox box)
{
    /// <summary>Gets the box of the model.</summary>
    public SequenceBox Box { get; } = box;

    /// <summary>Gets or sets the title to draw (wrapped when the box wraps), or null.</summary>
    public string? Name { get; set; } = box.Name;

    /// <summary>Gets or sets the left edge of the participants' area.</summary>
    public double X { get; set; }

    /// <summary>Gets or sets the top.</summary>
    public double Y { get; set; }

    /// <summary>Gets or sets the width.</summary>
    public double Width { get; set; }

    /// <summary>Gets or sets the height.</summary>
    public double Height { get; set; }

    /// <summary>Gets or sets the margin inside the box.</summary>
    public double Margin { get; set; }

    /// <summary>Gets or sets the height of the tallest box title.</summary>
    public double TextMaxHeight { get; set; }
}

/// <summary>
///     The drawing model of a message (Mermaid's <c>buildMessageModel</c> result, completed by the draw loop).
/// </summary>
internal sealed class SequenceMessageModel
{
    /// <summary>Gets or sets the width the message needs.</summary>
    public double Width { get; set; }

    /// <summary>Gets or sets the height.</summary>
    public double Height { get; set; }

    /// <summary>Gets or sets the x where the line starts.</summary>
    public double StartX { get; set; }

    /// <summary>Gets or sets the x where the line stops.</summary>
    public double StopX { get; set; }

    /// <summary>Gets or sets the top.</summary>
    public double StartY { get; set; }

    /// <summary>Gets or sets the bottom.</summary>
    public double StopY { get; set; }

    /// <summary>Gets or sets the text (wrapped when the message wraps).</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Gets or sets the line type.</summary>
    public int Type { get; set; }

    /// <summary>Gets or sets the leftmost of the activation bounds of both participants.</summary>
    public double FromBounds { get; set; }

    /// <summary>Gets or sets the rightmost of the activation bounds of both participants.</summary>
    public double ToBounds { get; set; }

    /// <summary>Gets or sets the sequence number.</summary>
    public double SequenceIndex { get; set; }

    /// <summary>Gets or sets a value indicating whether numbers were turned on by <c>autonumber</c>.</summary>
    public bool SequenceVisible { get; set; }
}

/// <summary>
///     The drawing model of a note (Mermaid's <c>buildNoteModel</c> result, completed by <c>drawNote</c>).
/// </summary>
internal sealed class SequenceNoteModel
{
    /// <summary>Gets or sets the width.</summary>
    public double Width { get; set; }

    /// <summary>Gets or sets the height.</summary>
    public double Height { get; set; }

    /// <summary>Gets or sets the left edge.</summary>
    public double StartX { get; set; }

    /// <summary>Gets or sets the right edge.</summary>
    public double StopX { get; set; }

    /// <summary>Gets or sets the top.</summary>
    public double StartY { get; set; }

    /// <summary>Gets or sets the bottom.</summary>
    public double StopY { get; set; }

    /// <summary>Gets or sets the text (wrapped when the note wraps).</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
///     A section of a block (the part after an <c>else</c>, <c>and</c> or <c>option</c>).
/// </summary>
internal sealed class SequenceLoopSection
{
    /// <summary>Gets or sets the y of the separator.</summary>
    public double Y { get; set; }

    /// <summary>Gets or sets the height.</summary>
    public double Height { get; set; }
}

/// <summary>
///     A block being laid out (Mermaid's loop model of <c>bounds.createLoop</c>): its extent grows with every item drawn
///     inside it.
/// </summary>
internal sealed class SequenceLoopModel
{
    /// <summary>Gets or sets the left edge, or null until something is inside.</summary>
    public double? StartX { get; set; }

    /// <summary>Gets or sets the top.</summary>
    public double? StartY { get; set; }

    /// <summary>Gets or sets the right edge, or null until something is inside.</summary>
    public double? StopX { get; set; }

    /// <summary>Gets or sets the bottom, or null until something is inside.</summary>
    public double? StopY { get; set; }

    /// <summary>Gets or sets the title, or null.</summary>
    public string? Title { get; set; }

    /// <summary>Gets or sets a value indicating whether the title wraps.</summary>
    public bool Wrap { get; set; }

    /// <summary>Gets or sets the width the title was wrapped to, or null.</summary>
    public double? Width { get; set; }

    /// <summary>Gets or sets the height (set when the block is drawn).</summary>
    public double Height { get; set; }

    /// <summary>Gets or sets the fill of a <c>rect</c> block, or null.</summary>
    public string? Fill { get; set; }

    /// <summary>Gets the sections, or null when the block has none.</summary>
    public List<SequenceLoopSection>? Sections { get; set; }

    /// <summary>Gets the messages that start the sections (their titles), or null.</summary>
    public List<SequenceMessageState>? SectionTitles { get; set; }

    /// <summary>Gets or sets the location of the statement that starts the block.</summary>
    public MermaidLocation Location { get; set; }
}

/// <summary>
///     An activation being laid out (Mermaid's activation object of <c>bounds.newActivation</c>).
/// </summary>
internal sealed class SequenceActivation
{
    /// <summary>Gets or sets the left edge.</summary>
    public double StartX { get; set; }

    /// <summary>Gets or sets the top.</summary>
    public double StartY { get; set; }

    /// <summary>Gets or sets the right edge.</summary>
    public double StopX { get; set; }

    /// <summary>Gets or sets the bottom, or null.</summary>
    public double? StopY { get; set; }

    /// <summary>Gets or sets the participant id.</summary>
    public string? Actor { get; set; }

    /// <summary>Gets or sets the place in the paint order the activation is drawn at (its anchor group), or null in a dry run.</summary>
    public SceneSlot? Anchor { get; set; }
}

/// <summary>
///     The per-render state of a message of the model: its text and wrap after the renderer changed them, and its
///     drawing models.
/// </summary>
/// <param name="message">The message of the model.</param>
/// <param name="wrap">The effective wrap setting.</param>
internal sealed class SequenceMessageState(SequenceMessage message, bool wrap)
{
    /// <summary>Gets the message of the model.</summary>
    public SequenceMessage Source { get; } = message;

    /// <summary>Gets or sets the text (Mermaid's <c>msg.message</c>, which the renderer wraps in place).</summary>
    public string Message { get; set; } = message.Text;

    /// <summary>Gets or sets the effective wrap setting.</summary>
    public bool Wrap { get; set; } = wrap;

    /// <summary>Gets or sets the width a block title was wrapped to, or null.</summary>
    public double? Width { get; set; }

    /// <summary>Gets or sets the drawing model of a message, or null for a message that is not an arrow.</summary>
    public SequenceMessageModel? MessageModel { get; set; }

    /// <summary>Gets or sets the drawing model of a note, or null.</summary>
    public SequenceNoteModel? NoteModel { get; set; }
}

/// <summary>
///     Mermaid's <c>bounds</c> object of the sequence renderer: the extent of the drawing, the vertical position of the
///     next item, the stack of open blocks, the open activations, and the models drawn so far.
/// </summary>
/// <remarks>
///     <para>
///         Every insert grows the extent and every open block and activation (<c>updateBounds</c>), each by a margin
///         that depends on its depth in the stack. The ported quirk is kept: one counter runs over the blocks and then
///         the activations, so activations grow by a margin of zero or less.
///     </para>
///     <para>
///         Every insert charges the number of open blocks and activations to the work meter, so a deep stack of blocks
///         or activations cannot make the layout quadratic without bound. The object belongs to one render.
///     </para>
/// </remarks>
/// <param name="boxMargin">The configured <c>boxMargin</c>.</param>
/// <param name="meter">The work meter of the render.</param>
internal sealed class SequenceBounds(double boxMargin, LayoutWorkMeter meter)
{
    /// <summary>Gets the left edge of the drawing, or null while it is empty.</summary>
    public double? StartX { get; private set; }

    /// <summary>Gets the top of the drawing, or null while it is empty.</summary>
    public double? StartY { get; private set; }

    /// <summary>Gets the right edge of the drawing, or null while it is empty.</summary>
    public double? StopX { get; private set; }

    /// <summary>Gets the bottom of the drawing, or null while it is empty.</summary>
    public double? StopY { get; private set; }

    /// <summary>Gets the vertical position of the next item.</summary>
    public double VerticalPos { get; private set; }

    /// <summary>Gets the stack of open blocks.</summary>
    public List<SequenceLoopModel> SequenceItems { get; } = [];

    /// <summary>Gets the open activations, in the order they were opened.</summary>
    public List<SequenceActivation> Activations { get; set; } = [];

    /// <summary>Gets the participants laid out (the models).</summary>
    public List<SequenceActorLayout> ActorModels { get; } = [];

    /// <summary>Gets the boxes laid out.</summary>
    public List<SequenceBoxLayout> BoxModels { get; } = [];

    /// <summary>Gets the blocks laid out.</summary>
    public List<SequenceLoopModel> LoopModels { get; } = [];

    /// <summary>Gets the messages laid out.</summary>
    public List<SequenceMessageModel> MessageModels { get; } = [];

    /// <summary>Gets the notes laid out.</summary>
    public List<SequenceNoteModel> NoteModels { get; } = [];

    /// <summary>
    ///     Grows the drawing and every open block and activation by an extent (Mermaid's <c>insert</c>).
    /// </summary>
    /// <param name="startx">One x.</param>
    /// <param name="starty">One y.</param>
    /// <param name="stopx">The other x.</param>
    /// <param name="stopy">The other y.</param>
    public void Insert(double startx, double starty, double stopx, double stopy)
    {
        var minX = SequenceLayout.JsMin(startx, stopx);
        var maxX = SequenceLayout.JsMax(startx, stopx);
        var minY = SequenceLayout.JsMin(starty, stopy);
        var maxY = SequenceLayout.JsMax(starty, stopy);
        StartX = UpdateMin(StartX, minX);
        StartY = UpdateMin(StartY, minY);
        StopX = UpdateMax(StopX, maxX);
        StopY = UpdateMax(StopY, maxY);
        UpdateBounds(minX, minY, maxX, maxY);
    }

    /// <summary>
    ///     Moves the vertical position down and grows the bottom of the drawing to it (Mermaid's <c>bumpVerticalPos</c>).
    /// </summary>
    /// <param name="bump">The distance.</param>
    public void BumpVerticalPos(double bump)
    {
        VerticalPos += bump;
        StopY = SequenceLayout.JsMax(StopY, VerticalPos);
    }

    /// <summary>
    ///     Opens a block at the vertical position (Mermaid's <c>newLoop</c>).
    /// </summary>
    /// <param name="title">The message that starts the block (its text is the title), or null for a <c>rect</c>.</param>
    /// <param name="fill">The fill of a <c>rect</c> block, or null.</param>
    /// <param name="location">The location of the statement that starts the block.</param>
    public void NewLoop(SequenceMessageState? title, string? fill, MermaidLocation location) => SequenceItems.Add(new SequenceLoopModel
    {
        StartY = VerticalPos,
        Title = title?.Message,
        Wrap = title?.Wrap ?? false,
        Width = title?.Width,
        Fill = fill,
        Location = location,
    });

    /// <summary>
    ///     Closes the innermost block (Mermaid's <c>endLoop</c>).
    /// </summary>
    /// <returns>The block, or null when none is open.</returns>
    public SequenceLoopModel? EndLoop()
    {
        if (SequenceItems.Count == 0)
        {
            return null;
        }

        var loop = SequenceItems[^1];
        SequenceItems.RemoveAt(SequenceItems.Count - 1);
        return loop;
    }

    /// <summary>
    ///     Starts a section of the innermost block at the vertical position (Mermaid's <c>addSectionToLoop</c>).
    /// </summary>
    /// <param name="message">The message that starts the section.</param>
    public void AddSectionToLoop(SequenceMessageState message)
    {
        if (SequenceItems.Count == 0)
        {
            return;
        }

        var loop = SequenceItems[^1];
        loop.Sections ??= [];
        loop.SectionTitles ??= [];
        loop.Sections.Add(new SequenceLoopSection { Y = VerticalPos });
        loop.SectionTitles.Add(message);
    }

    /// <summary>
    ///     Gets the open activations of a participant (Mermaid's <c>actorActivations</c>).
    /// </summary>
    /// <param name="actor">The participant id.</param>
    /// <returns>The activations.</returns>
    public List<SequenceActivation> ActorActivations(string? actor)
    {
        meter.Charge(Activations.Count + 1L);
        return Activations.FindAll(activation => activation.Actor == actor);
    }

    /// <summary>
    ///     Closes the last activation of a participant (Mermaid's <c>endActivation</c>, whose <c>splice</c> takes the
    ///     last activation when the participant has none).
    /// </summary>
    /// <param name="actor">The participant id.</param>
    /// <returns>The activation, or null when there is none.</returns>
    public SequenceActivation? EndActivation(string? actor)
    {
        meter.Charge(Activations.Count + 1L);
        var index = Activations.FindLastIndex(activation => activation.Actor == actor);
        index = index < 0 ? Activations.Count - 1 : index;
        if (index < 0)
        {
            return null;
        }

        var activation = Activations[index];
        Activations.RemoveAt(index);
        return activation;
    }

    /// <summary>
    ///     Gets the smaller value, or the value when the other is not set (Mermaid's <c>updateVal</c> with <c>Math.min</c>).
    /// </summary>
    /// <param name="current">The current value, or null.</param>
    /// <param name="value">The new value.</param>
    /// <returns>The updated value.</returns>
    private static double UpdateMin(double? current, double value) => current is { } c ? Math.Min(value, c) : value;

    /// <summary>
    ///     Gets the larger value, or the value when the other is not set (Mermaid's <c>updateVal</c> with <c>Math.max</c>).
    /// </summary>
    /// <param name="current">The current value, or null.</param>
    /// <param name="value">The new value.</param>
    /// <returns>The updated value.</returns>
    private static double UpdateMax(double? current, double value) => current is { } c ? Math.Max(value, c) : value;

    /// <summary>
    ///     Grows every open block and activation (Mermaid's <c>updateBounds</c>): the blocks from the outermost, each by
    ///     its depth times the box margin, then the activations with the same running counter.
    /// </summary>
    /// <param name="startx">The left edge.</param>
    /// <param name="starty">The top.</param>
    /// <param name="stopx">The right edge.</param>
    /// <param name="stopy">The bottom.</param>
    private void UpdateBounds(double startx, double starty, double stopx, double stopy)
    {
        meter.Charge(SequenceItems.Count + Activations.Count + 1L);
        var count = 0;
        foreach (var item in SequenceItems)
        {
            count++;
            var n = SequenceItems.Count - count + 1;
            item.StartY = UpdateMin(item.StartY, starty - (n * boxMargin));
            item.StopY = UpdateMax(item.StopY, stopy + (n * boxMargin));
            StartX = UpdateMin(StartX, startx - (n * boxMargin));
            StopX = UpdateMax(StopX, stopx + (n * boxMargin));
            item.StartX = UpdateMin(item.StartX, startx - (n * boxMargin));
            item.StopX = UpdateMax(item.StopX, stopx + (n * boxMargin));
            StartY = UpdateMin(StartY, starty - (n * boxMargin));
            StopY = UpdateMax(StopY, stopy + (n * boxMargin));
        }

        foreach (var activation in Activations)
        {
            count++;
            var n = SequenceItems.Count - count + 1;
            activation.StartY = Math.Min(starty - (n * boxMargin), activation.StartY);
            activation.StopY = UpdateMax(activation.StopY, stopy + (n * boxMargin));
            StartX = UpdateMin(StartX, startx - (n * boxMargin));
            StopX = UpdateMax(StopX, stopx + (n * boxMargin));
        }
    }
}
