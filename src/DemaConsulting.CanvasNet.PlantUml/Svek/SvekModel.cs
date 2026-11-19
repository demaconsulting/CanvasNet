using DemaConsulting.CanvasNet.PlantUml.Styling;

namespace DemaConsulting.CanvasNet.PlantUml.Svek;

/// <summary>
///     The facts of a lowered diagram: what <see cref="IPlantUmlDiagram"/> reports as its node, edge, and subgraph
///     counts.
/// </summary>
/// <param name="Nodes">The entities.</param>
/// <param name="Edges">The links that do not touch a note.</param>
/// <param name="Subgraphs">The clusters that have at least one member, regions not counted.</param>
internal readonly record struct SvekFacts(int Nodes, int Edges, int Subgraphs);

/// <summary>
///     A diagram lowered for the shared engine: its entities, clusters, notes, and links with the settings of its layout
///     and its look.
/// </summary>
/// <remarks>
///     <para>
///         A diagram subsystem builds one for every measure or render call, after measuring its entities with the text
///         layout of the call, and hands it to <see cref="SvekLayout.Run(SvekDiagram, DiagramRenderContext)"/> and
///         <see cref="SvekRenderer.Render"/>. The lists keep the order of the text, which the layout keeps (dagre's
///         results depend on the order of the nodes and edges).
///     </para>
///     <para>
///         The diagram is immutable once built; <see cref="Validate"/> checks that it is consistent before a layout.
///     </para>
/// </remarks>
internal sealed class SvekDiagram
{
    /// <summary>
    ///     Gets the entities, in text order.
    /// </summary>
    public IReadOnlyList<SvekEntity> Entities { get; init; } = [];

    /// <summary>
    ///     Gets the clusters, in text order (a parent before its children).
    /// </summary>
    public IReadOnlyList<SvekCluster> Clusters { get; init; } = [];

    /// <summary>
    ///     Gets the notes, in text order.
    /// </summary>
    public IReadOnlyList<SvekNote> Notes { get; init; } = [];

    /// <summary>
    ///     Gets the links, in text order.
    /// </summary>
    public IReadOnlyList<SvekLink> Links { get; init; } = [];

    /// <summary>
    ///     Gets the rank direction; top to bottom by default.
    /// </summary>
    public SvekRankDir RankDir { get; init; }

    /// <summary>
    ///     Gets the space between neighbors in a rank (<c>skinparam nodesep</c>), or null for PlantUML's default.
    /// </summary>
    public double? NodeSep { get; init; }

    /// <summary>
    ///     Gets the space between ranks (<c>skinparam ranksep</c>), or null for PlantUML's default.
    /// </summary>
    public double? RankSep { get; init; }

    /// <summary>
    ///     Gets the look of the links, notes, and labels; PlantUML's default look by default.
    /// </summary>
    public SkinTheme Theme { get; init; } = SkinTheme.Default;

    /// <summary>
    ///     Gets the location that a layout failure is reported at (the diagram's first statement); unknown by default.
    /// </summary>
    public PlantUmlLocation Location { get; init; }

    /// <summary>
    ///     Gets the members of a cluster, or of the top of the diagram, in text order: entities, then floating notes,
    ///     then clusters.
    /// </summary>
    /// <param name="cluster">The cluster, or null for the top of the diagram.</param>
    /// <returns>The direct members.</returns>
    public IEnumerable<SvekNode> MembersOf(SvekCluster? cluster)
    {
        foreach (var entity in Entities.Where(e => e.Parent == cluster))
        {
            yield return entity;
        }

        foreach (var note in Notes.Where(n => n.Target is null && n.Link is null && n.Parent == cluster))
        {
            yield return note;
        }

        foreach (var child in Clusters.Where(c => c.Parent == cluster))
        {
            yield return child;
        }
    }

    /// <summary>
    ///     Counts the facts of the diagram.
    /// </summary>
    /// <returns>The entities, the links that do not touch a note, and the clusters with members (regions not counted).</returns>
    public SvekFacts CountFacts()
    {
        var edges = Links.Count(l => l.From is not SvekNote && l.To is not SvekNote);
        var subgraphs = Clusters.Count(c => c.Parent?.Concurrency is null or ClusterConcurrency.None && MembersOf(c).Any());
        return new SvekFacts(Entities.Count, edges, subgraphs);
    }

    /// <summary>
    ///     Checks that the diagram is consistent: ids are unique, every parent, target, and link end belongs to the
    ///     diagram, the clusters nest without cycles, a cluster with concurrent regions has only clusters as members,
    ///     and the links have valid lengths and weights.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the diagram is not consistent.</exception>
    public void Validate()
    {
        // Every node id is unique, and every link id is unique
        var nodes = new HashSet<SvekNode>(ReferenceEqualityComparer.Instance);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var duplicate = Entities.Cast<SvekNode>().Concat(Notes).Concat(Clusters).FirstOrDefault(n => !ids.Add(n.Id) || !nodes.Add(n));
        if (duplicate is not null)
        {
            throw new ArgumentException($"The node id '{duplicate.Id}' is used twice.");
        }

        var linkIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var link in Links)
        {
            if (!linkIds.Add(link.Id))
            {
                throw new ArgumentException($"The link id '{link.Id}' is used twice.");
            }

            if (!nodes.Contains(link.From) || !nodes.Contains(link.To))
            {
                throw new ArgumentException($"The link '{link.Id}' has an end that is not in the diagram.");
            }

            if (link.From is SvekNote { Target: not null } or SvekNote { Link: not null } || link.To is SvekNote { Target: not null } or SvekNote { Link: not null })
            {
                throw new ArgumentException($"The link '{link.Id}' ends at a note that is attached to an element; only floating notes are linked.");
            }

            if (link.Length < 1 || !(link.Weight > 0) || !double.IsFinite(link.Weight))
            {
                throw new ArgumentException($"The link '{link.Id}' has a length below 1 or a weight that is not positive.");
            }
        }

        // Parents belong to the diagram and nest without cycles
        foreach (var node in nodes)
        {
            var depth = 0;
            for (var parent = node.Parent; parent is not null; parent = parent.Parent)
            {
                if (!nodes.Contains(parent) || ++depth > Clusters.Count)
                {
                    throw new ArgumentException($"The node '{node.Id}' has a parent that is not in the diagram, or the clusters nest in a cycle.");
                }
            }
        }

        // Notes attach to an entity or a link of the diagram
        foreach (var note in Notes)
        {
            if ((note.Target is { } target && !nodes.Contains(target)) || (note.Link is { } link && !Links.Contains(link)))
            {
                throw new ArgumentException($"The note '{note.Id}' is attached to an element that is not in the diagram.");
            }
        }

        // A cluster with concurrent regions holds only its regions
        var mixed = Clusters.FirstOrDefault(c => c.Concurrency != ClusterConcurrency.None && MembersOf(c).Any(m => m is not SvekCluster));
        if (mixed is not null)
        {
            throw new ArgumentException($"The cluster '{mixed.Id}' has concurrent regions and members that are not regions.");
        }
    }
}
