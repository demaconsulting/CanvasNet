// Portions derived from mermaid (MIT); see third-party/README.md
using System.Globalization;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Rendering;

namespace DemaConsulting.CanvasNet.Mermaid.Er;

/// <content>
///     The mapping of the database to the renderer's <see cref="LayoutData"/> (Mermaid's <c>getData</c>).
/// </content>
internal sealed partial class ErModel
{
    /// <summary>The markers an ER diagram defines (the unified ER renderer's <c>markers</c> in the classic look).</summary>
    private static readonly string[] ErMarkers = ["only_one", "zero_or_one", "one_or_more", "zero_or_more"];

    /// <summary>
    ///     Maps the model to the renderer's input under the configuration of a render (Mermaid's <c>getData</c>, with what
    ///     the unified ER renderer adds to it).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The subgraphs come first, the last one first, as clusters of shape <c>rect</c> with padding 8, their title as
    ///         the label, their direction and their classes and styles; then the entities that are not named after a
    ///         subgraph, as nodes of shape <c>erBox</c> with their name as the label and their alias and attributes as the
    ///         <see cref="LayoutNode.ErBox"/>. The parent of a node is the subgraph that holds it; when two hold it, the first
    ///         one, as Mermaid's lookup is filled from the last subgraph to the first.
    ///     </para>
    ///     <para>
    ///         Each relationship is an edge <c>id_&lt;A&gt;_&lt;B&gt;_&lt;n&gt;</c> with the role as its label, the curve
    ///         <c>basis</c>, the class <c>relationshipLine</c>, the pattern <c>solid</c> for an identifying relationship and
    ///         <c>dashed</c> otherwise, and the ER markers of its cardinalities: <c>cardB</c> (the left one) at the start and
    ///         <c>cardA</c> (the right one) at the end, in lower case (<c>only_one</c>, <c>zero_or_one</c>,
    ///         <c>one_or_more</c>, <c>zero_or_more</c> or <c>md_parent</c>, which has no marker). Mermaid's ER edges have no
    ///         <c>minlen</c>, so dagre's default of 1 applies.
    ///     </para>
    ///     <para>
    ///         The spacing is <c>er.nodeSpacing</c> and <c>er.rankSpacing</c> (140 and 80 by default), and the direction is
    ///         the diagram's direction.
    ///     </para>
    /// </remarks>
    /// <param name="config">The effective configuration of the render.</param>
    /// <returns>The layout data.</returns>
    public LayoutData GetData(MermaidConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var look = config.Look;

        // The parent of each node: the lookup is filled from the last subgraph to the first, so the first one wins
        var parents = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = _subgraphs.Count - 1; i >= 0; i--)
        {
            foreach (var id in _subgraphs[i].Nodes)
            {
                parents[id] = _subgraphs[i].Id;
            }
        }

        var nodes = new List<LayoutNode>();
        for (var i = _subgraphs.Count - 1; i >= 0; i--)
        {
            var subgraph = _subgraphs[i];
            nodes.Add(new LayoutNode
            {
                Id = subgraph.Id,
                Label = subgraph.Title,
                LabelType = "markdown",
                ParentId = parents.GetValueOrDefault(subgraph.Id),
                Padding = 8,
                CssCompiledStyles = subgraph.CssCompiledStyles,
                CssStyles = subgraph.CssStyles,
                CssClasses = string.Join(" ", subgraph.Classes),
                Shape = "rect",
                Dir = subgraph.Dir,
                IsGroup = true,
                Look = look,
                Location = subgraph.Location,
            });
        }

        foreach (var entity in _entities.Where(entity => entity.ColorIndex is not null))
        {
            nodes.Add(new LayoutNode
            {
                Id = entity.Id,
                Label = entity.Name,
                LabelType = "markdown",
                ParentId = parents.GetValueOrDefault(entity.Name),
                CssStyles = entity.CssStyles,
                CssCompiledStyles = entity.CssCompiledStyles,
                CssClasses = entity.CssClasses,
                Shape = "erBox",
                IsGroup = false,
                Look = look,
                ErBox = new LayoutErBox(
                    entity.Alias,
                    [.. entity.Attributes.Select(a => new LayoutErAttribute(a.Type, a.Name, a.Keys, a.Comment))]),
                Location = entity.Location,
            });
        }

        var edges = _relationships.Select((relationship, count) => new LayoutEdge
        {
            Id = string.Create(CultureInfo.InvariantCulture, $"id_{relationship.EntityA}_{relationship.EntityB}_{count}"),
            Start = relationship.EntityA,
            End = relationship.EntityB,
            Type = "normal",
            Curve = "basis",
            Label = relationship.RoleA,
            LabelPos = "c",
            LabelType = "markdown",
            Thickness = "normal",
            MinLen = 1,
            Classes = "relationshipLine",
            ArrowTypeStart = relationship.RelSpec.CardB.ToLowerInvariant(),
            ArrowTypeEnd = relationship.RelSpec.CardA.ToLowerInvariant(),
            Pattern = relationship.RelSpec.RelType == Identifying ? "solid" : "dashed",
            Look = look,
            Location = relationship.Location,
        }).ToList();

        return new LayoutData(nodes, edges, config)
        {
            DiagramType = MermaidId,
            Direction = Direction,
            NodeSpacing = config.Er.NodeSpacing,
            RankSpacing = config.Er.RankSpacing,
            Markers = ErMarkers,
        };
    }
}
