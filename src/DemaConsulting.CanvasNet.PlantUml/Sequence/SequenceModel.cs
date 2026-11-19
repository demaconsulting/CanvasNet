using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;

namespace DemaConsulting.CanvasNet.PlantUml.Sequence;

/// <summary>
///     A parsed sequence diagram: its participants in drawing order, its boxes, its events from top to bottom, its page
///     texts, and its look.
/// </summary>
/// <remarks>
///     <para>
///         The model is what <see cref="SequenceParser"/> produces and what <see cref="SequenceLayout"/> and
///         <see cref="SequenceRenderer"/> draw. Participants are sorted by their <c>order</c> values (declaration order
///         otherwise) and every event refers to them by index. The life changes that happen on a message's arrow
///         (<c>++</c>, <c>--</c>, <c>!!</c>, or an <c>activate</c> right after it) belong to that message.
///     </para>
///     <para>
///         For the document's facts, the nodes are the participants, the edges are the messages, and the subgraphs are
///         the boxes. The model is immutable and safe for concurrent use; every <see cref="Layout"/> call lays the
///         diagram out afresh under the options of the call.
///     </para>
/// </remarks>
internal sealed class SequenceModel : IPlantUmlDiagram
{
    /// <summary>
    ///     Initializes a model.
    /// </summary>
    /// <param name="participants">The participants in drawing order.</param>
    /// <param name="boxes">The boxes.</param>
    /// <param name="events">The events from top to bottom.</param>
    /// <param name="page">The page texts.</param>
    /// <param name="theme">The look, with the diagram's skin parameters applied.</param>
    /// <param name="reports">The reports the parser found.</param>
    /// <param name="keywordLocation">The location the diagram type was detected at.</param>
    public SequenceModel(
        IReadOnlyList<SequenceParticipant> participants,
        IReadOnlyList<SequenceBox> boxes,
        IReadOnlyList<SequenceEvent> events,
        PageTexts page,
        SkinTheme theme,
        IReadOnlyList<PlantUmlPendingReport> reports,
        PlantUmlLocation keywordLocation)
    {
        ArgumentNullException.ThrowIfNull(participants);
        ArgumentNullException.ThrowIfNull(boxes);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(reports);
        Participants = participants;
        Boxes = boxes;
        Events = events;
        Page = page;
        Theme = theme;
        Reports = reports;
        KeywordLocation = keywordLocation;
        EdgeCount = events.OfType<SequenceMessage>().Count();
    }

    /// <summary>
    ///     Gets the participants in drawing order.
    /// </summary>
    public IReadOnlyList<SequenceParticipant> Participants { get; }

    /// <summary>
    ///     Gets the boxes.
    /// </summary>
    public IReadOnlyList<SequenceBox> Boxes { get; }

    /// <summary>
    ///     Gets the events from top to bottom.
    /// </summary>
    public IReadOnlyList<SequenceEvent> Events { get; }

    /// <summary>
    ///     Gets the page texts.
    /// </summary>
    public PageTexts Page { get; }

    /// <summary>
    ///     Gets the look, with the diagram's skin parameters applied.
    /// </summary>
    public SkinTheme Theme { get; }

    /// <summary>
    ///     Gets the location the diagram type was detected at (where layout failures are located).
    /// </summary>
    public PlantUmlLocation KeywordLocation { get; }

    /// <summary>
    ///     Gets the diagram's own scale (its <c>scale</c> statement); 1 by default.
    /// </summary>
    public double Scale { get; init; } = 1;

    /// <inheritdoc/>
    public string? Title => Page.Title?.Text;

    /// <inheritdoc/>
    public int NodeCount => Participants.Count;

    /// <inheritdoc/>
    public int EdgeCount { get; }

    /// <inheritdoc/>
    public int SubgraphCount => Boxes.Count;

    /// <inheritdoc/>
    public IReadOnlyList<PlantUmlPendingReport> Reports { get; }

    /// <inheritdoc/>
    public Scene Layout(DiagramRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var geometry = SequenceLayout.Run(this, context);
        var body = SequenceRenderer.Render(this, geometry);
        var scene = ScenePage.Decorate(body, Page, Theme, context.Text, context.Sink);
        return scene with { Background = Theme.Background, Scale = Scale };
    }
}
