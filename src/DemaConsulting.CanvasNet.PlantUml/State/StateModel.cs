using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;
using DemaConsulting.CanvasNet.PlantUml.Svek;

namespace DemaConsulting.CanvasNet.PlantUml.State;

/// <summary>
///     A parsed state diagram: its states and pseudo-states (composite states with their regions), notes, transitions,
///     page texts, layout settings, and look.
/// </summary>
/// <remarks>
///     <para>
///         The model is what <see cref="StateParser"/> produces. At every measure or render call it is lowered to a
///         <see cref="SvekDiagram"/> (<see cref="Lower"/>): every simple state and pseudo-state gets a
///         <see cref="StateRenderer"/> image measured with the text layout of the call, every composite state a cluster
///         with its image (and, when it has concurrent regions, one cluster without image per region, stacked or side
///         by side by the engine with dashed separators), and the notes and transitions their Svek counterparts; the
///         Svek engine lays the diagram out and draws it, and the page texts are placed around it.
///     </para>
///     <para>
///         For the document's facts, the nodes are the simple states and the pseudo-states (start, end, choice, fork,
///         join, junction, and history), the edges are the transitions (not the links of notes), and the subgraphs are
///         the composite states. The model is not changed after parsing and is safe for concurrent use.
///     </para>
/// </remarks>
internal sealed class StateModel : IPlantUmlDiagram
{
    /// <summary>
    ///     Initializes a model.
    /// </summary>
    /// <param name="elements">The states and pseudo-states in order of creation.</param>
    /// <param name="notes">The notes in order.</param>
    /// <param name="transitions">The transitions in order.</param>
    /// <param name="page">The page texts.</param>
    /// <param name="theme">The look, with the diagram's skin parameters applied.</param>
    /// <param name="reports">The reports the parser found, in text order.</param>
    /// <param name="keywordLocation">The location the diagram type was detected at.</param>
    public StateModel(
        IReadOnlyList<StateElement> elements,
        IReadOnlyList<StateNote> notes,
        IReadOnlyList<StateTransition> transitions,
        PageTexts page,
        SkinTheme theme,
        IReadOnlyList<PlantUmlPendingReport> reports,
        PlantUmlLocation keywordLocation)
    {
        ArgumentNullException.ThrowIfNull(elements);
        ArgumentNullException.ThrowIfNull(notes);
        ArgumentNullException.ThrowIfNull(transitions);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(reports);
        (Elements, Notes, Transitions, Page, Theme, Reports, KeywordLocation) = (elements, notes, transitions, page, theme, reports, keywordLocation);
        NodeCount = elements.Count(e => !e.IsComposite);
        EdgeCount = transitions.Count(t => !t.TouchesNote);
        SubgraphCount = elements.Count(e => e.IsComposite);
    }

    /// <summary>Gets the states and pseudo-states in order of creation.</summary>
    public IReadOnlyList<StateElement> Elements { get; }

    /// <summary>Gets the notes in order.</summary>
    public IReadOnlyList<StateNote> Notes { get; }

    /// <summary>Gets the transitions in order.</summary>
    public IReadOnlyList<StateTransition> Transitions { get; }

    /// <summary>Gets the page texts.</summary>
    public PageTexts Page { get; }

    /// <summary>Gets the look, with the diagram's skin parameters applied.</summary>
    public SkinTheme Theme { get; }

    /// <summary>Gets the location the diagram type was detected at (where layout failures are located).</summary>
    public PlantUmlLocation KeywordLocation { get; }

    /// <summary>Gets the rank direction (<c>left to right direction</c>); top to bottom by default.</summary>
    public SvekRankDir RankDir { get; init; }

    /// <summary>Gets the space between neighbors (<c>skinparam nodesep</c>), or null.</summary>
    public double? NodeSep { get; init; }

    /// <summary>Gets the space between ranks (<c>skinparam ranksep</c>), or null.</summary>
    public double? RankSep { get; init; }

    /// <summary>Gets the diagram's own scale (its <c>scale</c> statement); 1 by default.</summary>
    public double Scale { get; init; } = 1;

    /// <summary>Gets a value indicating whether states without descriptions are drawn without their line (<c>hide empty description</c>).</summary>
    public bool HideEmptyDescription { get; init; }

    /// <inheritdoc/>
    public string? Title => Page.Title?.Text;

    /// <inheritdoc/>
    public int NodeCount { get; }

    /// <inheritdoc/>
    public int EdgeCount { get; }

    /// <inheritdoc/>
    public int SubgraphCount { get; }

    /// <inheritdoc/>
    public IReadOnlyList<PlantUmlPendingReport> Reports { get; }

    /// <inheritdoc/>
    public Scene Layout(DiagramRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var diagram = Lower(context);
        var placement = SvekLayout.Run(diagram, context);
        var body = SvekRenderer.Render(diagram, placement);
        var scene = ScenePage.Decorate(body, Page, Theme, context.Text, context.Sink, PageMetrics.Svek);
        return scene with { Background = Theme.Background, Scale = Scale };
    }

    /// <summary>
    ///     Gets the id of the cluster that stands for a region of a composite state with concurrent regions.
    /// </summary>
    /// <param name="region">The region.</param>
    /// <returns>The id (never a qualified name of an element: it holds a <c>#</c>).</returns>
    public static string RegionClusterId(StateRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);
        return region.Owner.Id + "#region" + region.Index.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     Lowers the diagram for the Svek engine, measuring its elements with the text layout of a call.
    /// </summary>
    /// <param name="context">The context of the call.</param>
    /// <returns>The lowered diagram.</returns>
    public SvekDiagram Lower(DiagramRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var nodes = new Dictionary<StateNode, SvekNode>(ReferenceEqualityComparer.Instance);
        var regions = new Dictionary<StateRegion, SvekCluster>(ReferenceEqualityComparer.Instance);
        SvekCluster? ClusterOf(StateNode node) => node.Region is { } region ? regions[region] : null;

        // The composite states, parents first, with a cluster per region when the regions are concurrent
        var clusters = new List<SvekCluster>();
        foreach (var composite in Elements.Where(e => e.IsComposite).OrderBy(e => e.Depth))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var concurrent = composite.Regions.Count > 1;
            var cluster = new SvekCluster(composite.Id)
            {
                Parent = ClusterOf(composite),
                Location = composite.Location,
                Image = StateRenderer.CompositeImage(composite, Theme, context.Text, context.Sink),
                Concurrency = concurrent ? composite.Concurrency : ClusterConcurrency.None,
            };
            nodes[composite] = cluster;
            clusters.Add(cluster);
            foreach (var region in composite.Regions)
            {
                if (!concurrent)
                {
                    regions[region] = cluster;
                    continue;
                }

                var regionCluster = new SvekCluster(RegionClusterId(region)) { Parent = cluster, Location = composite.Location };
                regions[region] = regionCluster;
                clusters.Add(regionCluster);
            }
        }

        var entities = new List<SvekEntity>();
        foreach (var element in Elements.Where(e => !e.IsComposite))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var entity = new SvekEntity(element.Id, StateRenderer.Image(element, HideEmptyDescription, RankDir, Theme, context.Text, context.Sink))
            {
                Parent = ClusterOf(element),
                Location = element.Location,
            };
            nodes[element] = entity;
            entities.Add(entity);
        }

        // Floating notes first (transitions may end at them), then the transitions, then the attached notes
        var notes = new List<SvekNote>();
        foreach (var note in Notes.Where(n => n.IsFloating))
        {
            var svek = new SvekNote(note.Id, note.Text) { Fill = note.Fill, Parent = ClusterOf(note), Location = note.Location };
            nodes[note] = svek;
            notes.Add(svek);
        }

        var links = new Dictionary<StateTransition, SvekLink>(ReferenceEqualityComparer.Instance);
        foreach (var transition in Transitions)
        {
            links[transition] = new SvekLink(transition.Id, nodes[transition.From], nodes[transition.To])
            {
                TailDecor = transition.TailDecor,
                HeadDecor = transition.HeadDecor,
                Style = transition.Style,
                Thickness = transition.Thickness,
                Color = transition.Color,
                Length = transition.Length,
                Direction = transition.Direction,
                Label = transition.Label,
                Location = transition.Location,
            };
        }

        foreach (var note in Notes.Where(n => !n.IsFloating))
        {
            notes.Add(new SvekNote(note.Id, note.Text)
            {
                Fill = note.Fill,
                Target = note.Target is null ? null : (SvekEntity)nodes[note.Target],
                Link = note.OnLink is null ? null : links[note.OnLink],
                Position = note.Position,
                Location = note.Location,
            });
        }

        // Keep the notes in text order, as the layout expects
        var order = Notes.Select((n, i) => (n.Id, i)).ToDictionary(p => p.Id, p => p.i, StringComparer.Ordinal);
        return new SvekDiagram
        {
            Entities = entities,
            Clusters = clusters,
            Notes = notes.OrderBy(n => order[n.Id]).ToList(),
            Links = Transitions.Select(t => links[t]).ToList(),
            RankDir = RankDir,
            NodeSep = NodeSep,
            RankSep = RankSep,
            Theme = Theme,
            Location = KeywordLocation,
        };
    }
}
