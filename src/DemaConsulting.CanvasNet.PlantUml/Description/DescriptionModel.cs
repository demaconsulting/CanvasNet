using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;
using DemaConsulting.CanvasNet.PlantUml.Svek;

namespace DemaConsulting.CanvasNet.PlantUml.Description;

/// <summary>
///     A parsed description diagram (a use case, component, or deployment diagram): its elements (leaves and
///     containers), notes, links, page texts, layout settings, and look.
/// </summary>
/// <remarks>
///     <para>
///         The model is what <see cref="DescriptionParser"/> produces. At every measure or render call it is lowered to a
///         <see cref="SvekDiagram"/> (<see cref="Lower"/>): every leaf element gets a <see cref="DescriptionRenderer"/>
///         image measured with the text layout of the call, every container a cluster with the image of its symbol, and
///         the notes and links their Svek counterparts; the Svek engine lays the diagram out and draws it, and the page
///         texts are placed around it.
///     </para>
///     <para>
///         For the document's facts, the nodes are the leaf elements (actors, use cases, components, interfaces, and the
///         leaf symbols; ports are not drawn and not counted), the edges are the links (the hidden ones included, the
///         links of notes not), and the subgraphs are the containers. The model is not changed after parsing and is safe
///         for concurrent use.
///     </para>
/// </remarks>
internal sealed class DescriptionModel : IPlantUmlDiagram
{
    /// <summary>
    ///     Initializes a model.
    /// </summary>
    /// <param name="elements">The elements (leaves and containers) in order of creation.</param>
    /// <param name="notes">The notes in order.</param>
    /// <param name="links">The links in order.</param>
    /// <param name="page">The page texts.</param>
    /// <param name="theme">The look, with the diagram's skin parameters applied.</param>
    /// <param name="reports">The reports the parser found, in text order.</param>
    /// <param name="keywordLocation">The location the diagram type was detected at.</param>
    public DescriptionModel(
        IReadOnlyList<DescriptionElement> elements,
        IReadOnlyList<DescriptionNote> notes,
        IReadOnlyList<DescriptionLink> links,
        PageTexts page,
        SkinTheme theme,
        IReadOnlyList<PlantUmlPendingReport> reports,
        PlantUmlLocation keywordLocation)
    {
        ArgumentNullException.ThrowIfNull(elements);
        ArgumentNullException.ThrowIfNull(notes);
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(reports);
        (Elements, Notes, Links, Page, Theme, Reports, KeywordLocation) = (elements, notes, links, page, theme, reports, keywordLocation);
        NodeCount = elements.Count(e => !e.IsContainer);
        EdgeCount = links.Count(l => !l.TouchesNote);
        SubgraphCount = elements.Count(e => e.IsContainer);
    }

    /// <summary>Gets the elements (leaves and containers) in order of creation.</summary>
    public IReadOnlyList<DescriptionElement> Elements { get; }

    /// <summary>Gets the notes in order.</summary>
    public IReadOnlyList<DescriptionNote> Notes { get; }

    /// <summary>Gets the links in order.</summary>
    public IReadOnlyList<DescriptionLink> Links { get; }

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
    ///     Lowers the diagram for the Svek engine, measuring its elements with the text layout of a call.
    /// </summary>
    /// <param name="context">The context of the call.</param>
    /// <returns>The lowered diagram.</returns>
    public SvekDiagram Lower(DiagramRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var nodes = new Dictionary<DescriptionNode, SvekNode>(ReferenceEqualityComparer.Instance);
        SvekCluster? ClusterOf(DescriptionNode node) => node.Container is { } container ? (SvekCluster)nodes[container] : null;

        // The containers, parents first
        var clusters = new List<SvekCluster>();
        foreach (var container in Elements.Where(e => e.IsContainer).OrderBy(e => e.Depth))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var cluster = new SvekCluster(container.Id)
            {
                Parent = ClusterOf(container),
                Location = container.Location,
                Image = DescriptionRenderer.ContainerImage(container, Theme, context.Text, context.Sink),
            };
            nodes[container] = cluster;
            clusters.Add(cluster);
        }

        var entities = new List<SvekEntity>();
        foreach (var element in Elements.Where(e => !e.IsContainer))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var entity = new SvekEntity(element.Id, DescriptionRenderer.Image(element, Theme, context.Text, context.Sink))
            {
                Parent = ClusterOf(element),
                Location = element.Location,
            };
            nodes[element] = entity;
            entities.Add(entity);
        }

        // Floating notes first (links may end at them), then the links, then the attached notes
        var notes = new List<SvekNote>();
        foreach (var note in Notes.Where(n => n.IsFloating))
        {
            var svek = new SvekNote(note.Id, note.Text) { Fill = note.Fill, Parent = ClusterOf(note), Location = note.Location };
            nodes[note] = svek;
            notes.Add(svek);
        }

        var links = new Dictionary<DescriptionLink, SvekLink>(ReferenceEqualityComparer.Instance);
        foreach (var link in Links)
        {
            links[link] = new SvekLink(link.Id, nodes[link.From], nodes[link.To])
            {
                TailDecor = link.TailDecor,
                HeadDecor = link.HeadDecor,
                Style = link.Style,
                Thickness = link.Thickness,
                Color = link.Color,
                Length = link.Length,
                Direction = link.Direction,
                Label = link.Label,
                LabelArrow = link.LabelArrow,
                TailLabel = link.TailLabel,
                HeadLabel = link.HeadLabel,
                Weight = link.Weight,
                Location = link.Location,
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
            Links = Links.Select(l => links[l]).ToList(),
            RankDir = RankDir,
            NodeSep = NodeSep,
            RankSep = RankSep,
            Theme = Theme,
            Location = KeywordLocation,
        };
    }
}
