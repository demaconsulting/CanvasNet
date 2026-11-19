using DemaConsulting.CanvasNet.Layout.Dagre;
using DemaConsulting.CanvasNet.Layout.Graphlib;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;

namespace DemaConsulting.CanvasNet.PlantUml.Svek;

/// <summary>
///     The layout scopes of <see cref="SvekLayout"/>: the top of the diagram and every cluster laid out on its own, the
///     compound clusters inside a scope with their tight boxes and header bands, and the regions of concurrent clusters.
/// </summary>
internal sealed partial class SvekLayout
{
    /// <summary>The scope of every link: the scope whose graph routes it (null for the top of the diagram).</summary>
    private readonly List<(SvekLink Link, SvekCluster? Scope)> _linkScopes = [];

    /// <summary>
    ///     Finds the clusters laid out on their own: those marked so, those with concurrent regions, the regions, and
    ///     those that no link crosses; then the scope of every link.
    /// </summary>
    private void FindAutonomousClusters()
    {
        foreach (var cluster in _diagram.Clusters)
        {
            var forced = cluster.Autonomous || cluster.Concurrency != ClusterConcurrency.None
                || cluster.Parent is { Concurrency: not ClusterConcurrency.None };
            if (forced || !_diagram.Links.Any(l => IsInside(l.From, cluster) ^ IsInside(l.To, cluster)))
            {
                _autonomous.Add(cluster);
            }
        }

        _meter.Charge((long)_diagram.Clusters.Count * (_diagram.Links.Count + 1));
        foreach (var link in _diagram.Links)
        {
            var toChain = ScopeChain(link.To);
            var scope = ScopeChain(link.From).First(s => toChain.Any(t => ReferenceEquals(t, s)));
            _linkScopes.Add((link, scope));
        }
    }

    /// <summary>
    ///     Decides whether a node is inside a cluster (the cluster itself is not inside).
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="cluster">The cluster.</param>
    /// <returns>True when the cluster is an ancestor of the node.</returns>
    private static bool IsInside(SvekNode node, SvekCluster cluster) => Ancestors(node).Any(a => ReferenceEquals(a, cluster));

    /// <summary>
    ///     Gets the scope a node is placed in: its nearest ancestor laid out on its own, or null for the top.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The scope.</returns>
    private SvekCluster? PlacementScope(SvekNode node) => Ancestors(node).FirstOrDefault(_autonomous.Contains);

    /// <summary>
    ///     Gets the scopes around a node, innermost first, ending with null (the top).
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The scopes.</returns>
    private List<SvekCluster?> ScopeChain(SvekNode node)
    {
        var chain = new List<SvekCluster?>();
        var scope = PlacementScope(node);
        while (scope is not null)
        {
            chain.Add(scope);
            scope = PlacementScope(scope);
        }

        chain.Add(null);
        return chain;
    }

    /// <summary>
    ///     Gets the node that stands for a node in the graph of a scope: the node itself, or the cluster laid out on its
    ///     own that contains it.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="scope">The scope.</param>
    /// <returns>The representative.</returns>
    private SvekNode Representative(SvekNode node, SvekCluster? scope)
    {
        var current = node;
        while (!ReferenceEquals(PlacementScope(current), scope))
        {
            current = PlacementScope(current)!;
        }

        return current;
    }

    /// <summary>
    ///     Lays out one scope: its members as a dagre graph (or its regions, for a cluster with concurrent regions),
    ///     then the post-processing of the boxes and routes.
    /// </summary>
    /// <param name="scope">The cluster laid out on its own, or null for the top of the diagram.</param>
    /// <returns>The placement of the scope, normalized to the origin.</returns>
    private Frame LayoutScope(SvekCluster? scope)
    {
        _meter.Charge(1);
        if (scope is { Concurrency: not ClusterConcurrency.None })
        {
            return ArrangeRegions(scope);
        }

        // The graph of the scope: units, compound clusters, anchors, and the edges routed here
        var graph = new Graph<DagreGraph, DagreNode, DagreEdge>(multigraph: true, compound: true);
        graph.SetGraph(new DagreGraph
        {
            RankDir = _diagram.RankDir == SvekRankDir.LeftToRight ? "LR" : "TB",
            NodeSep = NodeSep,
            RankSep = RankSep,
            EdgeSep = EdgeSep,
            MarginX = 0,
            MarginY = 0,
        });
        var work = new ScopeWork(graph);
        AddMembers(work, scope, null, 0);
        AddEdges(work, scope);
        FormRows(work);
        AddGraphNodes(work);
        AddGraphEdges(work);

        var frame = new Frame();
        if (graph.NodeCount > 0)
        {
            DagreLayout.Run(graph, _meter);
            ReadBack(work);
            TightenCompounds(work);
            InsertBands(work);
            RouteEdges(work);
            BuildFrame(work, frame);
        }

        frame.Links.AddRange(work.Unrouted);
        frame.Normalize();
        return frame;
    }

    /// <summary>
    ///     Adds the members of a cluster (or of the top) to the graph of a scope.
    /// </summary>
    /// <param name="work">The scope's work.</param>
    /// <param name="cluster">The cluster whose members are added, or null for the top.</param>
    /// <param name="parent">The graph id of the compound node they go into, or null.</param>
    /// <param name="depth">The nesting depth of that compound node.</param>
    private void AddMembers(ScopeWork work, SvekCluster? cluster, string? parent, int depth)
    {
        foreach (var member in _diagram.MembersOf(cluster))
        {
            _meter.Charge(1);
            if (member is SvekCluster child && !_autonomous.Contains(child))
            {
                // A cluster that links cross: a compound node with its members inside
                var id = _ids[child];
                work.Graph.SetNode(id, new DagreNode());
                work.Graph.SetParent(id, parent);
                work.Compounds.Add(new Compound(child, id, parent, depth));
                AddMembers(work, child, id, depth + 1);
                continue;
            }

            var unit = member switch
            {
                SvekEntity entity => EntityUnit(entity),
                SvekNote note => NoteUnit(note),
                _ => ClusterUnit((SvekCluster)member),
            };
            unit.Parent = parent;
            work.Units.Add(unit);
            work.UnitsByNode[member] = unit;

            // The notes attached to an entity are nodes of their own beside it, joined to it by a hidden edge
            if (member is SvekEntity owner && _attached.TryGetValue(owner, out var notes))
            {
                foreach (var note in notes)
                {
                    _meter.Charge(1);
                    var noteUnit = NoteUnit(note);
                    noteUnit.Parent = parent;
                    work.Units.Add(noteUnit);
                    work.UnitsByNode[note] = noteUnit;
                    work.AttachedNotes.Add((note, owner));
                }
            }
        }
    }

    /// <summary>
    ///     Builds the unit of an entity.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <returns>The unit, as large as the entity's image.</returns>
    private Unit EntityUnit(SvekEntity entity)
    {
        var (width, height) = (entity.Image.Width, entity.Image.Height);
        var unit = new Unit(entity, _ids[entity]);
        (unit.Width, unit.Height) = (width, height);
        return unit;
    }

    /// <summary>
    ///     Builds the unit of a note (floating, or attached to an entity).
    /// </summary>
    /// <param name="note">The note.</param>
    /// <returns>The unit.</returns>
    private Unit NoteUnit(SvekNote note)
    {
        var text = Measure(note.Text, SkinElement.Note, note.Location);
        var unit = new Unit(note, _ids[note]) { NoteText = text };
        (unit.Width, unit.Height) = (text.Width + NoteMarginLeft + NoteMarginRight, text.Height + (2 * NoteMarginY));
        return unit;
    }

    /// <summary>
    ///     Builds the unit of a cluster laid out on its own: its members laid out first, then framed by its padding and
    ///     header and widened to its title.
    /// </summary>
    /// <param name="cluster">The cluster.</param>
    /// <returns>The unit.</returns>
    private Unit ClusterUnit(SvekCluster cluster)
    {
        var inner = LayoutScope(cluster);
        var region = cluster.Parent is { Concurrency: not ClusterConcurrency.None };
        var (padding, header, minWidth) = (cluster.Image?.Padding ?? (region ? RegionPadding : 0), cluster.Image?.HeaderHeight ?? 0, cluster.Image?.MinWidth ?? 0);
        var unit = new Unit(cluster, _ids[cluster]) { Inner = inner };
        unit.Width = Math.Max(inner.Width + (2 * padding), minWidth);
        unit.Height = inner.Height + (2 * padding) + header;
        (unit.InnerX, unit.InnerY) = ((unit.Width - inner.Width) / 2, header + padding);
        return unit;
    }

    /// <summary>
    ///     Lays out the regions of a cluster with concurrent regions, each on its own, and stacks them or puts them side
    ///     by side; the links between two regions cannot be routed and are kept unrouted.
    /// </summary>
    /// <param name="cluster">The cluster.</param>
    /// <returns>The placement of its content, with the boundaries between the regions.</returns>
    private Frame ArrangeRegions(SvekCluster cluster)
    {
        var frame = new Frame();
        var stacked = cluster.Concurrency == ClusterConcurrency.Stacked;
        var regions = _diagram.MembersOf(cluster).Cast<SvekCluster>().Select(r => (Region: r, Unit: ClusterUnit(r))).ToList();
        var breadth = regions.Count == 0 ? 0 : regions.Max(r => stacked ? r.Unit.Width : r.Unit.Height);
        double offset = 0;
        foreach (var (region, unit) in regions)
        {
            // Every region spans the breadth of the cluster; its content is centered in it
            var box = stacked ? new SceneRect(0, offset, breadth, unit.Height) : new SceneRect(offset, 0, unit.Width, breadth);
            if (offset > 0)
            {
                frame.Boundaries.Add(offset);
            }

            frame.Clusters.Add(new WCluster(region, box));
            var inner = unit.Inner!;
            var (innerX, innerY) = stacked
                ? (box.X + ((box.Width - inner.Width) / 2), box.Y + unit.InnerY)
                : (box.X + unit.InnerX, box.Y + unit.InnerY + ((box.Height - unit.Height) / 2));
            inner.Translate(innerX, innerY);
            frame.Merge(inner);
            offset += stacked ? unit.Height : unit.Width;
        }

        frame.BoundariesHorizontal = stacked;
        frame.Links.AddRange(_linkScopes.Where(s => ReferenceEquals(s.Scope, cluster)).Select(s => new WLink(s.Link, []) { Drawn = false }));
        frame.Normalize();
        return frame;
    }

    /// <summary>
    ///     Reads the results of dagre: the boxes of the units, the boxes of the compound clusters, the anchors, and the
    ///     routes and label centers of the edges.
    /// </summary>
    /// <param name="work">The scope's work.</param>
    private static void ReadBack(ScopeWork work)
    {
        foreach (var row in work.Rows)
        {
            var node = work.Graph.Node(row.Id)!;
            row.Box = new SceneRect(node.X - (row.Width / 2), node.Y - (row.Height / 2), row.Width, row.Height);
        }

        foreach (var unit in work.Units)
        {
            if (unit.Row is { } row)
            {
                unit.Box = new SceneRect(row.Box.X + unit.RowX, row.Box.Y + unit.RowY, unit.Width, unit.Height);
                continue;
            }

            var node = work.Graph.Node(unit.Id)!;
            unit.Box = new SceneRect(node.X - (unit.Width / 2), node.Y - (unit.Height / 2), unit.Width, unit.Height);
        }

        foreach (var compound in work.Compounds)
        {
            var node = work.Graph.Node(compound.Id)!;
            var (w, h) = (node.Width ?? 0, node.Height ?? 0);
            compound.Box = new SceneRect(node.X - (w / 2), node.Y - (h / 2), w, h);
            if (compound.Anchor is { } anchor)
            {
                var a = work.Graph.Node(anchor)!;
                compound.AnchorPoint = new SvekPoint(a.X, a.Y);
            }
        }

        foreach (var edge in work.Edges)
        {
            var label = edge.Label;
            edge.Points = label.Points?.Select(p => new SvekPoint(p.X, p.Y)).ToList() ?? [];
            edge.LabelCenter = label.X is { } x && label.Y is { } y && edge.Slot is not null ? new SvekPoint(x, y) : null;
        }
    }

    /// <summary>
    ///     Makes the box of every compound cluster as tight as its members with its padding, innermost first.
    /// </summary>
    /// <param name="work">The scope's work.</param>
    private void TightenCompounds(ScopeWork work)
    {
        foreach (var compound in work.Compounds.OrderByDescending(c => c.Depth))
        {
            _meter.Charge(work.Units.Count + work.Compounds.Count);
            SceneRect? bounds = null;
            void Include(SceneRect r) => bounds = bounds is { } b ? b.Union(r) : r;
            foreach (var unit in work.Units.Where(u => u.Parent == compound.Id))
            {
                Include(unit.Box);
            }

            foreach (var child in work.Compounds.Where(c => c.Parent == compound.Id))
            {
                Include(child.Box);
            }

            if (compound.AnchorPoint is { } anchor)
            {
                Include(new SceneRect(anchor.X, anchor.Y, 0, 0));
            }

            if (bounds is { } tight)
            {
                var padding = compound.Cluster.Image?.Padding ?? 0;
                compound.Box = new SceneRect(tight.X - padding, tight.Y - padding, tight.Width + (2 * padding), tight.Height + (2 * padding));
            }
        }
    }

    /// <summary>
    ///     Makes room for the header of every compound cluster (a band below its top that everything below moves out
    ///     of) and for its title (a band at its right side), so nothing overlaps.
    /// </summary>
    /// <param name="work">The scope's work.</param>
    private void InsertBands(ScopeWork work)
    {
        foreach (var compound in work.Compounds.OrderBy(c => c.Box.Y).ThenBy(c => c.Depth))
        {
            var header = compound.Cluster.Image?.HeaderHeight ?? 0;
            if (header > 0)
            {
                Band(work, vertical: true, compound.Box.Y, header);
            }
        }

        foreach (var compound in work.Compounds.OrderBy(c => c.Box.X).ThenBy(c => c.Depth))
        {
            var extra = (compound.Cluster.Image?.MinWidth ?? 0) - compound.Box.Width;
            if (extra > 0)
            {
                Band(work, vertical: false, compound.Box.Right, extra);
            }
        }
    }

    /// <summary>
    ///     Inserts a band into the geometry of a scope: everything past a line moves by the size of the band, and the
    ///     clusters that the line crosses (or starts at) grow by it.
    /// </summary>
    /// <param name="work">The scope's work.</param>
    /// <param name="vertical">True for a band across the drawing at a y (moving things down), false for one at an x.</param>
    /// <param name="at">The line.</param>
    /// <param name="size">The size of the band.</param>
    private void Band(ScopeWork work, bool vertical, double at, double size)
    {
        _meter.Charge(work.Units.Count + work.Compounds.Count + work.Edges.Sum(e => e.Points.Count) + 1);
        double Move(double v) => v > at ? v + size : v;
        SvekPoint MovePoint(SvekPoint p) => vertical ? p with { Y = Move(p.Y) } : p with { X = Move(p.X) };

        // A unit moves whole when its center is past the line; it never grows
        foreach (var unit in work.Units)
        {
            var center = vertical ? unit.Box.Y + (unit.Box.Height / 2) : unit.Box.X + (unit.Box.Width / 2);
            if (center > at)
            {
                unit.Box = vertical ? unit.Box with { Y = unit.Box.Y + size } : unit.Box with { X = unit.Box.X + size };
            }
        }

        // A cluster moves when it starts past the line and grows when the line crosses it
        foreach (var compound in work.Compounds)
        {
            var (start, end) = vertical ? (compound.Box.Y, compound.Box.Bottom) : (compound.Box.X, compound.Box.Right);
            var grown = end >= at ? end + size : end;
            var (newStart, newEnd) = start > at ? (start + size, end + size) : (start, grown);
            compound.Box = vertical
                ? compound.Box with { Y = newStart, Height = newEnd - newStart }
                : compound.Box with { X = newStart, Width = newEnd - newStart };
            compound.AnchorPoint = compound.AnchorPoint is { } a ? MovePoint(a) : null;
        }

        foreach (var edge in work.Edges)
        {
            edge.Points = edge.Points.Select(MovePoint).ToList();
            edge.LabelCenter = edge.LabelCenter is { } c ? MovePoint(c) : null;
        }
    }

    /// <summary>
    ///     Builds the placement of a scope from its units, compound clusters, and edges.
    /// </summary>
    /// <param name="work">The scope's work.</param>
    /// <param name="frame">The placement to fill.</param>
    private void BuildFrame(ScopeWork work, Frame frame)
    {
        foreach (var unit in work.Units)
        {
            var main = unit.MainBox;
            switch (unit.Node)
            {
                case SvekEntity entity:
                    frame.Entities.Add(new WEntity(entity, main));
                    break;
                case SvekNote { Target: { } target } note:
                    frame.Notes.Add(AttachedNote(note, unit.NoteText!, main, work.UnitsByNode[target].MainBox));
                    break;
                case SvekNote note:
                    frame.Notes.Add(new WNote(note, unit.NoteText!, main));
                    break;
                case SvekCluster cluster:
                    var inner = unit.Inner!;
                    inner.Translate(main.X + unit.InnerX, main.Y + unit.InnerY);
                    frame.Clusters.Add(new WCluster(cluster, main) { Separators = SeparatorsOf(inner, main) });
                    frame.Merge(inner);
                    break;
            }
        }

        foreach (var compound in work.Compounds)
        {
            frame.Clusters.Add(new WCluster(compound.Cluster, compound.Box));
        }

        foreach (var edge in work.Edges.Where(e => !e.Synthetic))
        {
            frame.Links.Add(edge.Placed!);
        }

        foreach (var note in work.LinkNotes)
        {
            frame.Notes.Add(note);
        }

        PointNotes(frame);
    }

    /// <summary>
    ///     Turns the boundaries between the regions of a content into the separators of its cluster, from border to
    ///     border.
    /// </summary>
    /// <param name="inner">The content, already moved into the cluster.</param>
    /// <param name="box">The box of the cluster.</param>
    /// <returns>The separators.</returns>
    private static List<(SvekPoint From, SvekPoint To)> SeparatorsOf(Frame inner, SceneRect box) =>
        inner.Boundaries
            .Select(b => inner.BoundariesHorizontal
                ? (new SvekPoint(box.X, inner.OriginY + b), new SvekPoint(box.Right, inner.OriginY + b))
                : (new SvekPoint(inner.OriginX + b, box.Y), new SvekPoint(inner.OriginX + b, box.Bottom)))
            .ToList();

    /// <summary>
    ///     Places a note attached to an entity, with its pointer from the side of the note that faces the entity (where
    ///     the layout put it: the side with the widest gap between the two boxes, the note's own position on a tie) to the
    ///     entity's border.
    /// </summary>
    /// <param name="note">The note.</param>
    /// <param name="text">The laid-out text.</param>
    /// <param name="box">The box of the note.</param>
    /// <param name="target">The box of the entity.</param>
    /// <returns>The placed note.</returns>
    private static WNote AttachedNote(SvekNote note, TextBlock text, SceneRect box, SceneRect target)
    {
        // The entity's side of the note: where the gap between them is the widest
        var gaps = new[]
        {
            (Gap: target.Y - box.Bottom, Own: NotePosition.Top),
            (Gap: box.Y - target.Bottom, Own: NotePosition.Bottom),
            (Gap: target.X - box.Right, Own: NotePosition.Left),
            (Gap: box.X - target.Right, Own: NotePosition.Right),
        };
        var position = gaps.OrderByDescending(g => g.Gap).ThenByDescending(g => g.Own == note.Position).First().Own;
        var (cx, cy) = (box.X + (box.Width / 2), box.Y + (box.Height / 2));
        var (side, tip, pointerBase) = position switch
        {
            NotePosition.Left => (NotePosition.Right, new SvekPoint(target.X, Math.Clamp(cy, target.Y, target.Bottom)), new SvekPoint(box.Right, cy)),
            NotePosition.Right => (NotePosition.Left, new SvekPoint(target.Right, Math.Clamp(cy, target.Y, target.Bottom)), new SvekPoint(box.X, cy)),
            NotePosition.Top => (NotePosition.Bottom, new SvekPoint(Math.Clamp(cx, target.X, target.Right), target.Y), new SvekPoint(cx, box.Bottom)),
            _ => (NotePosition.Top, new SvekPoint(Math.Clamp(cx, target.X, target.Right), target.Bottom), new SvekPoint(cx, box.Y)),
        };
        return new WNote(note, text, box) { Side = side, Tip = tip, Base = pointerBase };
    }

    /// <summary>
    ///     The work of one scope: its graph, units, compound clusters, and edges.
    /// </summary>
    /// <param name="graph">The graph.</param>
    private sealed class ScopeWork(Graph<DagreGraph, DagreNode, DagreEdge> graph)
    {
        /// <summary>Gets the graph.</summary>
        public Graph<DagreGraph, DagreNode, DagreEdge> Graph { get; } = graph;

        /// <summary>Gets the units (the leaf nodes), in the order they were added.</summary>
        public List<Unit> Units { get; } = [];

        /// <summary>Gets the unit of every node placed as a unit.</summary>
        public Dictionary<SvekNode, Unit> UnitsByNode { get; } = new(ReferenceEqualityComparer.Instance);

        /// <summary>Gets the compound clusters, parents first.</summary>
        public List<Compound> Compounds { get; } = [];

        /// <summary>Gets the edges.</summary>
        public List<EdgeWork> Edges { get; } = [];

        /// <summary>Gets the rows of units kept in one rank by links of length 1.</summary>
        public List<Row> Rows { get; } = [];

        /// <summary>Gets the number of loops routed so far at each unit.</summary>
        public Dictionary<Unit, int> Loops { get; } = new(ReferenceEqualityComparer.Instance);

        /// <summary>Gets the links of the scope that could not be routed.</summary>
        public List<WLink> Unrouted { get; } = [];

        /// <summary>Gets the notes on the links of the scope.</summary>
        public List<WNote> LinkNotes { get; } = [];

        /// <summary>Gets the notes attached to the entities of the scope, with their entities, in the order they were added.</summary>
        public List<(SvekNote Note, SvekEntity Owner)> AttachedNotes { get; } = [];
    }

    /// <summary>
    ///     A leaf node of a scope's graph: an entity, a note, or a cluster laid out on its own.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="id">The graph id.</param>
    private sealed class Unit(SvekNode node, string id)
    {
        /// <summary>Gets the node.</summary>
        public SvekNode Node { get; } = node;

        /// <summary>Gets the graph id.</summary>
        public string Id { get; } = id;

        /// <summary>Gets or sets the graph id of the compound node the unit is in, or null.</summary>
        public string? Parent { get; set; }

        /// <summary>Gets or sets the width of the unit.</summary>
        public double Width { get; set; }

        /// <summary>Gets or sets the height of the unit.</summary>
        public double Height { get; set; }

        /// <summary>Gets or sets the text of a note.</summary>
        public TextBlock? NoteText { get; set; }

        /// <summary>Gets or sets the content of a cluster laid out on its own.</summary>
        public Frame? Inner { get; set; }

        /// <summary>Gets or sets the left of the content in the cluster.</summary>
        public double InnerX { get; set; }

        /// <summary>Gets or sets the top of the content in the cluster.</summary>
        public double InnerY { get; set; }

        /// <summary>Gets or sets the row the unit is in, or null.</summary>
        public Row? Row { get; set; }

        /// <summary>Gets or sets the left of the unit in its row.</summary>
        public double RowX { get; set; }

        /// <summary>Gets or sets the top of the unit in its row.</summary>
        public double RowY { get; set; }

        /// <summary>Gets or sets the box of the unit after the layout.</summary>
        public SceneRect Box { get; set; }

        /// <summary>Gets the box of the node after the layout (the box of the unit).</summary>
        public SceneRect MainBox => Box;
    }

    /// <summary>
    ///     A row of units that links of length 1 keep in one rank: one leaf node of the graph.
    /// </summary>
    /// <param name="id">The graph id.</param>
    /// <param name="parent">The graph id of the compound node it is in, or null.</param>
    private sealed class Row(string id, string? parent)
    {
        /// <summary>Gets the graph id.</summary>
        public string Id { get; } = id;

        /// <summary>Gets the graph id of the compound node it is in, or null.</summary>
        public string? Parent { get; } = parent;

        /// <summary>Gets the units in their order.</summary>
        public List<Unit> Members { get; } = [];

        /// <summary>Gets or sets the width.</summary>
        public double Width { get; set; }

        /// <summary>Gets or sets the height.</summary>
        public double Height { get; set; }

        /// <summary>Gets or sets the box after the layout.</summary>
        public SceneRect Box { get; set; }
    }

    /// <summary>
    ///     A compound cluster of a scope's graph.
    /// </summary>
    /// <param name="cluster">The cluster.</param>
    /// <param name="id">The graph id.</param>
    /// <param name="parent">The graph id of the compound node it is in, or null.</param>
    /// <param name="depth">The nesting depth.</param>
    private sealed class Compound(SvekCluster cluster, string id, string? parent, int depth)
    {
        /// <summary>Gets the cluster.</summary>
        public SvekCluster Cluster { get; } = cluster;

        /// <summary>Gets the graph id.</summary>
        public string Id { get; } = id;

        /// <summary>Gets the graph id of the compound node it is in, or null.</summary>
        public string? Parent { get; } = parent;

        /// <summary>Gets the nesting depth.</summary>
        public int Depth { get; } = depth;

        /// <summary>Gets or sets the graph id of the hidden anchor that links to the cluster end at, or null.</summary>
        public string? Anchor { get; set; }

        /// <summary>Gets or sets the place of the anchor after the layout.</summary>
        public SvekPoint? AnchorPoint { get; set; }

        /// <summary>Gets or sets the box.</summary>
        public SceneRect Box { get; set; }
    }

    /// <summary>
    ///     A placed entity during the layout.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <param name="box">The box.</param>
    private sealed class WEntity(SvekEntity entity, SceneRect box)
    {
        /// <summary>Gets the entity.</summary>
        public SvekEntity Entity { get; } = entity;

        /// <summary>Gets or sets the box.</summary>
        public SceneRect Box { get; set; } = box;
    }

    /// <summary>
    ///     A placed cluster during the layout.
    /// </summary>
    /// <param name="cluster">The cluster.</param>
    /// <param name="box">The box.</param>
    private sealed class WCluster(SvekCluster cluster, SceneRect box)
    {
        /// <summary>Gets the cluster.</summary>
        public SvekCluster Cluster { get; } = cluster;

        /// <summary>Gets or sets the box.</summary>
        public SceneRect Box { get; set; } = box;

        /// <summary>Gets or sets the separators between its regions.</summary>
        public List<(SvekPoint From, SvekPoint To)> Separators { get; set; } = [];
    }

    /// <summary>
    ///     A placed note during the layout.
    /// </summary>
    /// <param name="note">The note.</param>
    /// <param name="text">The laid-out text.</param>
    /// <param name="box">The box.</param>
    private sealed class WNote(SvekNote note, TextBlock text, SceneRect box)
    {
        /// <summary>Gets the note.</summary>
        public SvekNote Note { get; } = note;

        /// <summary>Gets the text.</summary>
        public TextBlock Text { get; } = text;

        /// <summary>Gets or sets the box.</summary>
        public SceneRect Box { get; set; } = box;

        /// <summary>Gets or sets the side the pointer leaves from, or null.</summary>
        public NotePosition? Side { get; set; }

        /// <summary>Gets or sets the base of the pointer.</summary>
        public SvekPoint Base { get; set; }

        /// <summary>Gets or sets the tip of the pointer.</summary>
        public SvekPoint Tip { get; set; }
    }

    /// <summary>
    ///     A placed link during the layout.
    /// </summary>
    /// <param name="link">The link.</param>
    /// <param name="route">The route.</param>
    private sealed class WLink(SvekLink link, List<SvekPoint> route)
    {
        /// <summary>Gets the link.</summary>
        public SvekLink Link { get; } = link;

        /// <summary>Gets or sets the route from the first end to the second.</summary>
        public List<SvekPoint> Route { get; set; } = route;

        /// <summary>Gets or sets a value indicating whether the link is drawn.</summary>
        public bool Drawn { get; set; } = true;

        /// <summary>Gets or sets the middle label.</summary>
        public WLabel? Label { get; set; }

        /// <summary>Gets or sets the label near the first end.</summary>
        public WLabel? Tail { get; set; }

        /// <summary>Gets or sets the label near the second end.</summary>
        public WLabel? Head { get; set; }
    }

    /// <summary>
    ///     A placed label during the layout.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="box">The box.</param>
    private sealed class WLabel(TextBlock text, SceneRect box)
    {
        /// <summary>Gets the text.</summary>
        public TextBlock Text { get; } = text;

        /// <summary>Gets or sets the box.</summary>
        public SceneRect Box { get; set; } = box;

        /// <summary>Gets or sets the direction of the triangle, or null.</summary>
        public double? ArrowAngle { get; set; }
    }

    /// <summary>
    ///     The placement of one scope during the layout, in its own coordinates until it is moved into its parent.
    /// </summary>
    private sealed class Frame
    {
        /// <summary>Gets the entities.</summary>
        public List<WEntity> Entities { get; } = [];

        /// <summary>Gets the clusters.</summary>
        public List<WCluster> Clusters { get; } = [];

        /// <summary>Gets the notes.</summary>
        public List<WNote> Notes { get; } = [];

        /// <summary>Gets the links.</summary>
        public List<WLink> Links { get; } = [];

        /// <summary>Gets the boundaries between the regions of a concurrent cluster's content, from its origin.</summary>
        public List<double> Boundaries { get; } = [];

        /// <summary>Gets or sets a value indicating whether the regions are stacked (horizontal boundaries).</summary>
        public bool BoundariesHorizontal { get; set; }

        /// <summary>Gets the width after <see cref="Normalize"/>.</summary>
        public double Width { get; private set; }

        /// <summary>Gets the height after <see cref="Normalize"/>.</summary>
        public double Height { get; private set; }

        /// <summary>Gets the x of the origin of the content after the moves.</summary>
        public double OriginX { get; private set; }

        /// <summary>Gets the y of the origin of the content after the moves.</summary>
        public double OriginY { get; private set; }

        /// <summary>
        ///     Moves everything.
        /// </summary>
        /// <param name="dx">The horizontal offset.</param>
        /// <param name="dy">The vertical offset.</param>
        public void Translate(double dx, double dy)
        {
            static SceneRect Move(SceneRect r, double dx, double dy) => r with { X = r.X + dx, Y = r.Y + dy };
            SvekPoint MovePoint(SvekPoint p) => new(p.X + dx, p.Y + dy);
            foreach (var entity in Entities)
            {
                entity.Box = Move(entity.Box, dx, dy);
            }

            foreach (var cluster in Clusters)
            {
                cluster.Box = Move(cluster.Box, dx, dy);
                cluster.Separators = cluster.Separators.Select(s => (MovePoint(s.From), MovePoint(s.To))).ToList();
            }

            foreach (var note in Notes)
            {
                note.Box = Move(note.Box, dx, dy);
                (note.Base, note.Tip) = (MovePoint(note.Base), MovePoint(note.Tip));
            }

            foreach (var link in Links)
            {
                link.Route = link.Route.Select(MovePoint).ToList();
                foreach (var label in new[] { link.Label, link.Tail, link.Head }.OfType<WLabel>())
                {
                    label.Box = Move(label.Box, dx, dy);
                }
            }

            (OriginX, OriginY) = (OriginX + dx, OriginY + dy);
        }

        /// <summary>
        ///     Adds the content of another placement.
        /// </summary>
        /// <param name="other">The placement, already moved into place.</param>
        public void Merge(Frame other)
        {
            Entities.AddRange(other.Entities);
            Clusters.AddRange(other.Clusters);
            Notes.AddRange(other.Notes);
            Links.AddRange(other.Links);
        }

        /// <summary>
        ///     Moves the content so that its bounds start at the origin, and sets the size to the bounds.
        /// </summary>
        public void Normalize()
        {
            SceneRect? bounds = null;
            void Include(SceneRect r) => bounds = bounds is { } b ? b.Union(r) : r;
            foreach (var r in Entities.Select(e => e.Box).Concat(Clusters.Select(c => c.Box)).Concat(Notes.Select(n => n.Box)))
            {
                Include(r);
            }

            foreach (var link in Links.Where(l => l.Drawn))
            {
                foreach (var p in link.Route)
                {
                    Include(new SceneRect(p.X, p.Y, 0, 0));
                }

                foreach (var label in new[] { link.Label, link.Tail, link.Head }.OfType<WLabel>())
                {
                    Include(label.Box);
                }
            }

            if (bounds is not { } box)
            {
                (Width, Height) = (0, 0);
                return;
            }

            Translate(-box.X, -box.Y);
            (Width, Height) = (box.Width, box.Height);
        }

        /// <summary>
        ///     Converts the placement to its result, in the order of the diagram.
        /// </summary>
        /// <param name="diagram">The diagram.</param>
        /// <returns>The placement.</returns>
        public SvekPlacement ToPlacement(SvekDiagram diagram)
        {
            static PlacedLabel? Label(WLabel? label) => label is null ? null : new PlacedLabel(label.Text, label.Box) { ArrowAngle = label.ArrowAngle };
            static Dictionary<T, int> IndexOf<T>(IReadOnlyList<T> list)
                where T : class
            {
                var index = new Dictionary<T, int>(ReferenceEqualityComparer.Instance);
                for (var k = 0; k < list.Count; k++)
                {
                    index[list[k]] = k;
                }

                return index;
            }

            var (entities, clusters, notes, links) = (IndexOf(diagram.Entities), IndexOf(diagram.Clusters), IndexOf(diagram.Notes), IndexOf(diagram.Links));
            return new SvekPlacement(
                Width,
                Height,
                Entities.OrderBy(e => entities[e.Entity]).Select(e => new PlacedEntity(e.Entity, e.Box)).ToList(),
                Clusters.OrderBy(c => clusters[c.Cluster]).Select(c => new PlacedCluster(c.Cluster, c.Box) { Separators = c.Separators }).ToList(),
                Notes.OrderBy(n => notes[n.Note])
                    .Select(n => new PlacedNote(n.Note, n.Text, n.Box) { PointerSide = n.Side, PointerBase = n.Base, PointerTip = n.Tip })
                    .ToList(),
                Links.OrderBy(l => links[l.Link])
                    .Select(l => new PlacedLink(l.Link, l.Route) { Drawn = l.Drawn, Label = Label(l.Label), TailLabel = Label(l.Tail), HeadLabel = Label(l.Head) })
                    .ToList());
        }
    }
}
