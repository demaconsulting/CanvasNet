// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Layout.Dagre;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;

namespace DemaConsulting.CanvasNet.PlantUml.Svek;

/// <summary>
///     The edges of <see cref="SvekLayout"/>: the dagre edges of the links with their lengths, hints, and label slots,
///     and the routes after the layout (the straight lines and arcs of links in one rank, the cuts at cluster borders,
///     the clipping to the outlines of the entities), the label places, and the pointers of the notes.
/// </summary>
internal sealed partial class SvekLayout
{
    /// <summary>
    ///     Prepares the edges of the links routed in a scope: their ends, lengths, hints, and label slots.
    /// </summary>
    /// <param name="work">The scope's work.</param>
    /// <param name="scope">The scope.</param>
    private void AddEdges(ScopeWork work, SvekCluster? scope)
    {
        foreach (var (link, linkScope) in _linkScopes)
        {
            if (!ReferenceEquals(linkScope, scope))
            {
                continue;
            }

            _meter.Charge(1);
            var repFrom = Representative(link.From, scope);
            var repTo = Representative(link.To, scope);
            if (ReferenceEquals(repFrom, repTo) && !ReferenceEquals(link.From, link.To))
            {
                // Both ends are inside one cluster laid out on its own, in different regions: no route
                work.Unrouted.Add(new WLink(link, []) { Drawn = false });
                continue;
            }

            // The length and the hint: a one-long link keeps its ends in one rank, up and left turn the edge around
            var length = Math.Max(1, link.Length);
            var minLen = link.Direction switch
            {
                LinkDirection.Left or LinkDirection.Right => 0,
                LinkDirection.Up or LinkDirection.Down => Math.Max(1, length - 1),
                _ => length - 1,
            };
            var reversed = link.Direction is LinkDirection.Up or LinkDirection.Left;
            var slot = MeasureSlot(link);
            var label = new DagreEdge
            {
                MinLen = minLen,
                Weight = link.Weight,
                Width = slot?.Width ?? 0,
                Height = slot?.Height ?? 0,
                LabelPos = slot is null ? "c" : "r",
                LabelOffset = slot is null ? 0 : LabelOffset,
            };
            var name = "l" + _linkIndex[link].ToString(System.Globalization.CultureInfo.InvariantCulture);
            work.Edges.Add(new EdgeWork(link, label, reversed, repFrom, repTo) { Slot = slot, MinLen = minLen, Name = name });
        }

        // The hidden edges that keep every attached note on its side of its entity
        foreach (var (note, owner) in work.AttachedNotes)
        {
            work.Edges.Add(NoteEdge(note, owner));
        }
    }

    /// <summary>
    ///     Builds the hidden edge between an attached note and its entity, as PlantUML links them for Graphviz: a note
    ///     beside the entity across the ranks (left or right, top or bottom in a left to right diagram) shares its rank,
    ///     the others are one rank before (left, top) or after (right, bottom) it.
    /// </summary>
    /// <param name="note">The note.</param>
    /// <param name="owner">The entity it is attached to.</param>
    /// <returns>The edge; it is laid out but never drawn.</returns>
    private EdgeWork NoteEdge(SvekNote note, SvekEntity owner)
    {
        var across = _diagram.RankDir == SvekRankDir.LeftToRight
            ? note.Position is NotePosition.Top or NotePosition.Bottom
            : note.Position is NotePosition.Left or NotePosition.Right;
        var before = note.Position is NotePosition.Left or NotePosition.Top;
        var (from, to) = before ? ((SvekNode)note, (SvekNode)owner) : (owner, note);
        var minLen = across ? 0 : 1;
        var link = new SvekLink("~" + note.Id, from, to) { Style = LinkStyle.Hidden, Length = minLen + 1 };
        var label = new DagreEdge { MinLen = minLen, Weight = 1, LabelPos = "c" };
        return new EdgeWork(link, label, false, from, to) { MinLen = minLen, Synthetic = true, Name = "s" + _ids[note][1..] };
    }

    /// <summary>
    ///     Groups the units that links of length 1 keep in one rank into rows, ordered as Graphviz orders the ends of such
    ///     a link (the first end of the edge left of the second, above it from left to right), because dagre's ranking
    ///     raises every edge to a length of at least one rank.
    /// </summary>
    /// <remarks>
    ///     A row is one node of the graph, as wide as its units side by side with the space between neighbors (or the
    ///     label of the links between two of them) between them, and as high as the highest; only units with the same
    ///     compound parent form a row.
    /// </remarks>
    /// <param name="work">The scope's work.</param>
    private void FormRows(ScopeWork work)
    {
        // Union-find over the units joined by a one-rank link
        var index = new Dictionary<Unit, int>(ReferenceEqualityComparer.Instance);
        for (var k = 0; k < work.Units.Count; k++)
        {
            index[work.Units[k]] = k;
        }

        var root = Enumerable.Range(0, work.Units.Count).ToArray();
        int Find(int k)
        {
            while (root[k] != k)
            {
                k = root[k];
            }

            return k;
        }

        var flat = new List<(Unit Left, Unit Right, EdgeWork Edge)>();
        foreach (var edge in work.Edges.Where(e => e.MinLen == 0 && !ReferenceEquals(e.RepFrom, e.RepTo)))
        {
            if (work.UnitsByNode.TryGetValue(edge.RepFrom, out var from) && work.UnitsByNode.TryGetValue(edge.RepTo, out var to) && from.Parent == to.Parent)
            {
                var (left, right) = edge.Reversed ? (to, from) : (from, to);
                flat.Add((left, right, edge));
                root[Find(index[left])] = Find(index[right]);
            }
        }

        _meter.Charge((2 * (work.Units.Count + flat.Count)) + 1);
        var leftToRight = _diagram.RankDir == SvekRankDir.LeftToRight;

        // The widest label slot between every two units joined by one-rank links, in either order
        var gaps = new Dictionary<(int, int), double>();
        foreach (var (left, right, edge) in flat)
        {
            var key = (Math.Min(index[left], index[right]), Math.Max(index[left], index[right]));
            gaps[key] = Math.Max(gaps.GetValueOrDefault(key), SlotExtent(edge.Slot, leftToRight));
        }

        foreach (var component in work.Units.GroupBy(u => Find(index[u])).Where(g => g.Count() > 1))
        {
            var ordered = OrderRow(component.ToList(), flat, index);

            // The units side by side (stacked in a left to right diagram), centered across the row
            var row = new Row("r" + work.Rows.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), ordered[0].Parent);
            var offset = 0.0;
            for (var k = 0; k < ordered.Count; k++)
            {
                var unit = ordered[k];
                if (k > 0)
                {
                    var (a, b) = (index[ordered[k - 1]], index[unit]);
                    offset += Math.Max(SpaceBetween(ordered[k - 1], unit), gaps.GetValueOrDefault((Math.Min(a, b), Math.Max(a, b))));
                }

                (unit.RowX, unit.RowY) = leftToRight ? (0.0, offset) : (offset, 0.0);
                offset += leftToRight ? unit.Height : unit.Width;
                unit.Row = row;
                row.Members.Add(unit);
            }

            (row.Width, row.Height) = leftToRight ? (ordered.Max(u => u.Width), offset) : (offset, ordered.Max(u => u.Height));
            foreach (var unit in ordered)
            {
                (unit.RowX, unit.RowY) = leftToRight ? ((row.Width - unit.Width) / 2, unit.RowY) : (unit.RowX, (row.Height - unit.Height) / 2);
            }

            work.Rows.Add(row);
        }

        // A link between two units of one row is drawn in the row, not by dagre
        foreach (var edge in work.Edges)
        {
            edge.InRow = work.UnitsByNode.TryGetValue(edge.RepFrom, out var from) && work.UnitsByNode.TryGetValue(edge.RepTo, out var to)
                && from.Row is not null && ReferenceEquals(from.Row, to.Row) && !ReferenceEquals(from, to);
        }
    }

    /// <summary>
    ///     Gets the space between two neighbors of a row: <see cref="NoteGap"/> between a note and its entity,
    ///     <see cref="NoteStackGap"/> between two notes of one entity, and the space between neighbors otherwise.
    /// </summary>
    /// <param name="a">The first unit.</param>
    /// <param name="b">The unit after it.</param>
    /// <returns>The space.</returns>
    private double SpaceBetween(Unit a, Unit b)
    {
        var (ta, tb) = ((a.Node as SvekNote)?.Target, (b.Node as SvekNote)?.Target);
        if (ReferenceEquals(ta, b.Node) || ReferenceEquals(tb, a.Node))
        {
            return NoteGap;
        }

        return ta is not null && ReferenceEquals(ta, tb) ? NoteStackGap : NodeSep;
    }

    /// <summary>
    ///     Gets the space a label slot needs between two units of a row.
    /// </summary>
    /// <param name="slot">The slot, or null.</param>
    /// <param name="leftToRight">Whether the row is stacked (a left to right diagram).</param>
    /// <returns>The extent of the slot along the row plus <see cref="RowLabelSpace"/>, or zero.</returns>
    private static double SlotExtent(LabelSlot? slot, bool leftToRight)
    {
        if (slot is null)
        {
            return 0;
        }

        return (leftToRight ? slot.Height : slot.Width) + RowLabelSpace;
    }

    /// <summary>
    ///     Orders the units of a row: every one-rank link's first end before its second, the earliest unit of the
    ///     diagram first among those that are free, and the earliest remaining one when the links form a cycle (Kahn's
    ///     topological order, linear in the units and links).
    /// </summary>
    /// <param name="members">The units of the row, in the order of the diagram.</param>
    /// <param name="flat">The one-rank links of the scope.</param>
    /// <param name="index">The index of every unit in the scope.</param>
    /// <returns>The units in row order.</returns>
    private static List<Unit> OrderRow(List<Unit> members, List<(Unit Left, Unit Right, EdgeWork Edge)> flat, Dictionary<Unit, int> index)
    {
        var position = new Dictionary<Unit, int>(ReferenceEqualityComparer.Instance);
        for (var k = 0; k < members.Count; k++)
        {
            position[members[k]] = k;
        }

        var incoming = new int[members.Count];
        var successors = members.Select(_ => new List<int>()).ToArray();
        foreach (var (left, right, _) in flat)
        {
            if (position.TryGetValue(left, out var l) && position.TryGetValue(right, out var r) && l != r)
            {
                successors[l].Add(r);
                incoming[r]++;
            }
        }

        var free = new PriorityQueue<int, int>();
        for (var k = 0; k < members.Count; k++)
        {
            if (incoming[k] == 0)
            {
                free.Enqueue(k, index[members[k]]);
            }
        }

        var placed = new bool[members.Count];
        var ordered = new List<Unit>(members.Count);
        var cursor = 0;
        while (ordered.Count < members.Count)
        {
            int next;
            if (free.Count > 0)
            {
                next = free.Dequeue();
                if (placed[next])
                {
                    continue;
                }
            }
            else
            {
                // A cycle: the earliest unit not yet placed breaks it
                while (placed[cursor])
                {
                    cursor++;
                }

                next = cursor;
            }

            placed[next] = true;
            ordered.Add(members[next]);
            foreach (var successor in successors[next])
            {
                incoming[successor]--;
                if (incoming[successor] == 0 && !placed[successor])
                {
                    free.Enqueue(successor, index[members[successor]]);
                }
            }
        }

        return ordered;
    }

    /// <summary>
    ///     Adds the leaf nodes of a scope's graph: every row once, at the place of its first unit, and every unit that is
    ///     not in a row.
    /// </summary>
    /// <param name="work">The scope's work.</param>
    private static void AddGraphNodes(ScopeWork work)
    {
        var added = new HashSet<Row>(ReferenceEqualityComparer.Instance);
        foreach (var unit in work.Units)
        {
            var (id, width, height, parent) = unit.Row is { } row ? (row.Id, row.Width, row.Height, row.Parent) : (unit.Id, unit.Width, unit.Height, unit.Parent);
            if (unit.Row is null || added.Add(unit.Row))
            {
                work.Graph.SetNode(id, new DagreNode { Width = width, Height = height });
                work.Graph.SetParent(id, parent);
            }
        }
    }

    /// <summary>
    ///     Adds the dagre edges of the links that dagre routes (all but the links inside a row).
    /// </summary>
    /// <param name="work">The scope's work.</param>
    private void AddGraphEdges(ScopeWork work)
    {
        foreach (var edge in work.Edges.Where(e => !e.InRow))
        {
            var (v, fromCompound) = GraphNode(work, edge.RepFrom);
            var (w, toCompound) = GraphNode(work, edge.RepTo);
            (edge.FromCompound, edge.ToCompound) = (fromCompound, toCompound);
            work.Graph.SetEdge(edge.Reversed ? w : v, edge.Reversed ? v : w, edge.Label, edge.Name);
        }
    }

    /// <summary>
    ///     Gets the graph node that a link to a node of a scope connects: its unit or the row of its unit, or the anchor
    ///     of a compound cluster (added on first use).
    /// </summary>
    /// <param name="work">The scope's work.</param>
    /// <param name="node">The representative of the link's end.</param>
    /// <returns>The graph id, and the compound cluster when the link ends at one.</returns>
    private (string Id, Compound? Compound) GraphNode(ScopeWork work, SvekNode node)
    {
        if (work.UnitsByNode.TryGetValue(node, out var unit))
        {
            return (unit.Row?.Id ?? unit.Id, null);
        }

        var compound = work.Compounds.First(c => ReferenceEquals(c.Cluster, node));
        if (compound.Anchor is null)
        {
            compound.Anchor = "a" + _ids[node][1..];
            // A box of 1: dagre cannot clip a route to a box without size
            work.Graph.SetNode(compound.Anchor, new DagreNode { Width = 1, Height = 1 });
            work.Graph.SetParent(compound.Anchor, compound.Id);
        }

        return (compound.Anchor, compound);
    }

    /// <summary>
    ///     Measures the label slot of a link: its label with a margin and its direction triangle, and the note on the
    ///     link beside it.
    /// </summary>
    /// <param name="link">The link.</param>
    /// <returns>The slot, or null for a link without label or note.</returns>
    private LabelSlot? MeasureSlot(SvekLink link)
    {
        var style = _diagram.Theme[SkinElement.SvekArrow];
        TextBlock? text = link.Label is null ? null : Measure(link.Label, SkinElement.SvekArrow, link.Location);
        var arrow = text is not null && link.LabelArrow != LinkLabelArrow.None;
        var arrowSize = arrow ? style.FontSize : 0;
        var (textW, textH) = text is null
            ? (0.0, 0.0)
            : (text.Width + (2 * LabelMargin) + arrowSize, Math.Max(text.Height + (2 * LabelMargin), arrowSize));

        _linkNotes.TryGetValue(link, out var note);
        var noteText = note is null ? null : Measure(note.Text, SkinElement.Note, note.Location);
        var (noteW, noteH) = noteText is null ? (0.0, 0.0) : (noteText.Width + NoteMarginLeft + NoteMarginRight, noteText.Height + (2 * NoteMarginY));
        if (text is null && note is null)
        {
            return null;
        }

        // The note goes on its side of the label, centered on it
        var position = note?.Position ?? NotePosition.Bottom;
        var horizontal = position is NotePosition.Left or NotePosition.Right;
        var width = horizontal ? textW + noteW : Math.Max(textW, noteW);
        var height = horizontal ? Math.Max(textH, noteH) : textH + noteH;
        var textBox = position switch
        {
            NotePosition.Left => new SceneRect(noteW, (height - textH) / 2, textW, textH),
            NotePosition.Right => new SceneRect(0, (height - textH) / 2, textW, textH),
            NotePosition.Top => new SceneRect((width - textW) / 2, noteH, textW, textH),
            _ => new SceneRect((width - textW) / 2, 0, textW, textH),
        };
        var noteBox = position switch
        {
            NotePosition.Left => new SceneRect(0, (height - noteH) / 2, noteW, noteH),
            NotePosition.Right => new SceneRect(textW, (height - noteH) / 2, noteW, noteH),
            NotePosition.Top => new SceneRect((width - noteW) / 2, 0, noteW, noteH),
            _ => new SceneRect((width - noteW) / 2, textH, noteW, noteH),
        };
        return new LabelSlot(width, height, text, textBox, arrowSize, note, noteText, noteBox);
    }

    /// <summary>
    ///     Turns the dagre routes of a scope into the routes of its links.
    /// </summary>
    /// <param name="work">The scope's work.</param>
    private void RouteEdges(ScopeWork work)
    {
        foreach (var edge in work.Edges.Where(e => !e.Synthetic))
        {
            _meter.Charge(edge.Points.Count + work.Units.Count + 1);
            var points = RowRoute(work, edge) ?? edge.Points.ToList();
            if (edge.Reversed && !edge.InRow)
            {
                points.Reverse();
            }

            var link = edge.Link;
            var self = ReferenceEquals(edge.RepFrom, edge.RepTo);
            if (points.Count == 2 && !self)
            {
                points = AvoidUnits(work, edge, points);
            }

            if (edge.InRow && !self)
            {
                points = SpreadParallel(work, edge, points);
            }

            if (!edge.InRow && !self)
            {
                SnapRowEnds(work, edge, points);
            }

            // A link to a compound cluster ends at its border; a label that dagre placed inside the cluster moves to
            // the part of the route outside it
            IReadOnlyList<SvekPoint> route = points;
            if (edge.FromCompound is { } fromCompound)
            {
                route = SvekGeometry.CutAtBox(route, fromCompound.Box, atEnd: false);
                edge.LabelCenter = edge.LabelCenter is { } c && SvekGeometry.Contains(ClipShape.Rectangle, fromCompound.Box, c) ? null : edge.LabelCenter;
            }

            if (edge.ToCompound is { } toCompound)
            {
                route = SvekGeometry.CutAtBox(route, toCompound.Box, atEnd: true);
                edge.LabelCenter = edge.LabelCenter is { } c && SvekGeometry.Contains(ClipShape.Rectangle, toCompound.Box, c) ? null : edge.LabelCenter;
            }

            points = route.ToList();
            ClipEnds(work, edge, points, self);
            var placed = new WLink(link, points) { Drawn = link.Style != LinkStyle.Hidden };
            PlaceLabel(work, edge, placed);
            placed.Tail = EndLabel(work, link.TailLabel, link.Location, points, atEnd: false);
            placed.Head = EndLabel(work, link.HeadLabel, link.Location, points, atEnd: true);
            edge.Placed = placed;
        }
    }

    /// <summary>
    ///     Gets the route of a link drawn in a row (straight between the centers of its ends), or of a link from a node
    ///     to itself (a loop at the right of the node: dagre loops it around the whole unit, attached notes included).
    /// </summary>
    /// <param name="work">The scope's work.</param>
    /// <param name="edge">The edge.</param>
    /// <returns>The route of a link in a row, or null to keep dagre's route.</returns>
    private static List<SvekPoint>? RowRoute(ScopeWork work, EdgeWork edge)
    {
        static SvekPoint Center(SceneRect r) => new(r.X + (r.Width / 2), r.Y + (r.Height / 2));
        if (edge.InRow)
        {
            return [Center(work.UnitsByNode[edge.RepFrom].MainBox), Center(work.UnitsByNode[edge.RepTo].MainBox)];
        }

        if (ReferenceEquals(edge.RepFrom, edge.RepTo) && work.UnitsByNode.TryGetValue(edge.RepFrom, out var unit))
        {
            // A loop at the right of the node, each further loop of the node a little wider (as Graphviz draws them)
            var loops = work.Loops.TryGetValue(unit, out var count) ? count : 0;
            work.Loops[unit] = loops + 1;
            var box = unit.MainBox;
            var (right, middle) = (box.Right, box.Y + (box.Height / 2));
            var half = Math.Min(box.Height / 4, 12);
            var reach = 18 + (12 * loops);
            edge.LabelCenter = edge.Slot is { } slot ? new SvekPoint(right + reach + 8 + LabelOffset + (slot.Width / 2), middle) : null;
            return
            [
                new(right, middle - half), new(right + reach, middle - half - 6), new(right + reach + 8, middle),
                new(right + reach, middle + half + 6), new(right, middle + half),
            ];
        }

        return null;
    }

    /// <summary>
    ///     Replaces a straight route that crosses another node of the scope by an arc over the nodes it crosses (dagre
    ///     routes a link in one rank straight from end to end).
    /// </summary>
    /// <param name="work">The scope's work.</param>
    /// <param name="edge">The edge.</param>
    /// <param name="points">The two points of the route.</param>
    /// <returns>The route: the two points, or the ends with the control point of the arc between them.</returns>
    private List<SvekPoint> AvoidUnits(ScopeWork work, EdgeWork edge, List<SvekPoint> points)
    {
        var (a, b) = (points[0], points[1]);
        var crossed = Crossed(work, edge, a, b);
        if (crossed.Count == 0)
        {
            return points;
        }

        // The arc bulges up (left in a left to right diagram) far enough to clear the boxes it crossed
        var normal = UpNormal(a, b);
        var mid = new SvekPoint((a.X + b.X) / 2, (a.Y + b.Y) / 2);
        var offset = Clearance(crossed, mid, normal);
        return [a, new SvekPoint(mid.X + (offset * normal.X), mid.Y + (offset * normal.Y)), b];
    }

    /// <summary>
    ///     Gets the boxes of the units of a scope (but the ends of an edge) that a straight segment crosses.
    /// </summary>
    /// <param name="work">The scope's work.</param>
    /// <param name="edge">The edge.</param>
    /// <param name="a">The start of the segment.</param>
    /// <param name="b">The end of the segment.</param>
    /// <returns>The boxes.</returns>
    private static List<SceneRect> Crossed(ScopeWork work, EdgeWork edge, SvekPoint a, SvekPoint b) =>
        work.Units
            .Where(u => !ReferenceEquals(u.Node, edge.RepFrom) && !ReferenceEquals(u.Node, edge.RepTo))
            .Select(u => u.Box)
            .Where(r => SegmentHitsRect(a, b, r))
            .ToList();

    /// <summary>
    ///     Gets how far from the middle of a segment the control point of an arc must be, along a normal, for the arc to
    ///     clear boxes (twice the distance of their farthest corner plus a quarter of the rank space, as the curve reaches
    ///     half way to its control point).
    /// </summary>
    /// <param name="crossed">The boxes; none gives zero.</param>
    /// <param name="mid">The middle of the segment.</param>
    /// <param name="normal">The side the arc bulges to.</param>
    /// <returns>The distance of the control point.</returns>
    private double Clearance(List<SceneRect> crossed, SvekPoint mid, SvekPoint normal)
    {
        if (crossed.Count == 0)
        {
            return 0;
        }

        var reach = crossed
            .SelectMany(r => new[] { (r.X, r.Y), (r.Right, r.Y), (r.X, r.Bottom), (r.Right, r.Bottom) })
            .Max(c => ((c.Item1 - mid.X) * normal.X) + ((c.Item2 - mid.Y) * normal.Y));
        return 2 * (Math.Max(0, reach) + (RankSep / 4));
    }

    /// <summary>
    ///     Gets the unit normal of a segment that points up (left in a left to right diagram), the side the arcs of a row
    ///     bulge to.
    /// </summary>
    /// <param name="a">The start.</param>
    /// <param name="b">The end.</param>
    /// <returns>The normal.</returns>
    private SvekPoint UpNormal(SvekPoint a, SvekPoint b)
    {
        var (dx, dy) = (b.X - a.X, b.Y - a.Y);
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        var normal = length == 0 ? new SvekPoint(0, -1) : new SvekPoint(-dy / length, dx / length);
        var down = _diagram.RankDir == SvekRankDir.LeftToRight ? normal.X > 0 : normal.Y > 0;
        return down ? new SvekPoint(-normal.X, -normal.Y) : normal;
    }

    /// <summary>
    ///     Keeps the links drawn in a row between the same two units apart: the first keeps its route, the further ones
    ///     are arcs below and above it in turn, each pair <see cref="ParallelArcStep"/> further out (dagre does not route
    ///     links in a row).
    /// </summary>
    /// <param name="work">The scope's work.</param>
    /// <param name="edge">The edge, drawn in a row.</param>
    /// <param name="points">Its route: two points, or the ends with the control point of an arc.</param>
    /// <returns>The route.</returns>
    private List<SvekPoint> SpreadParallel(ScopeWork work, EdgeWork edge, List<SvekPoint> points)
    {
        var (from, to) = (work.UnitsByNode[edge.RepFrom], work.UnitsByNode[edge.RepTo]);
        var key = string.CompareOrdinal(from.Id, to.Id) <= 0 ? (from.Id, to.Id) : (to.Id, from.Id);
        var count = work.RowPairs.GetValueOrDefault(key);
        work.RowPairs[key] = count + 1;
        if (count == 0)
        {
            return points;
        }

        // Every second one bulges down (right in a left to right diagram), each pair a step further out, clear of the
        // units the straight line would cross on its side
        var (a, b) = (points[0], points[^1]);
        var side = count % 2 == 1 ? -1 : 1;
        var up = UpNormal(a, b);
        var normal = new SvekPoint(side * up.X, side * up.Y);
        var mid = new SvekPoint((a.X + b.X) / 2, (a.Y + b.Y) / 2);
        var offset = Clearance(Crossed(work, edge, a, b), mid, normal) + (((count + 1) / 2) * ParallelArcStep);
        return [a, new SvekPoint(mid.X + (offset * normal.X), mid.Y + (offset * normal.Y)), b];
    }

    /// <summary>
    ///     Moves the ends of a route at a unit of a row onto the unit's own part of the row (dagre ends the route on the
    ///     border of the row, aimed at the row's center, which may be over a neighbor such as an attached note): the end
    ///     moves along the row's border in front of the unit, and the route continues to the unit's center, where
    ///     <see cref="ClipEnds"/> clips it.
    /// </summary>
    /// <param name="work">The scope's work.</param>
    /// <param name="edge">The edge.</param>
    /// <param name="points">The route; its ends may be replaced and extended.</param>
    private void SnapRowEnds(ScopeWork work, EdgeWork edge, List<SvekPoint> points)
    {
        if (points.Count < 2)
        {
            return;
        }

        var leftToRight = _diagram.RankDir == SvekRankDir.LeftToRight;
        if (edge.FromCompound is null && work.UnitsByNode.TryGetValue(edge.RepFrom, out var from) && from.Row is { } fromRow)
        {
            SnapEnd(points, from.MainBox, RowBounds(fromRow), leftToRight, atEnd: false);
        }

        if (edge.ToCompound is null && work.UnitsByNode.TryGetValue(edge.RepTo, out var to) && to.Row is { } toRow)
        {
            SnapEnd(points, to.MainBox, RowBounds(toRow), leftToRight, atEnd: true);
        }
    }

    /// <summary>
    ///     Gets the bounds of the units of a row.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns>The union of their boxes.</returns>
    private static SceneRect RowBounds(Row row) => row.Members.Select(m => m.Box).Aggregate((a, b) => a.Union(b));

    /// <summary>
    ///     Moves one end of a route in front of a unit of a row.
    /// </summary>
    /// <param name="points">The route.</param>
    /// <param name="main">The box of the unit's node.</param>
    /// <param name="row">The bounds of the row.</param>
    /// <param name="leftToRight">Whether the row is stacked (a left to right diagram).</param>
    /// <param name="atEnd">True for the last point, false for the first.</param>
    private static void SnapEnd(List<SvekPoint> points, SceneRect main, SceneRect row, bool leftToRight, bool atEnd)
    {
        var end = atEnd ? points[^1] : points[0];
        var center = new SvekPoint(main.X + (main.Width / 2), main.Y + (main.Height / 2));
        var inFront = leftToRight ? end.Y >= main.Y && end.Y <= main.Bottom : end.X >= main.X && end.X <= main.Right;
        if (inFront)
        {
            return;
        }

        // The end goes to the row's border in front of the unit, on the side it came from
        SvekPoint snapped;
        if (leftToRight)
        {
            snapped = new SvekPoint(end.X < center.X ? row.X : row.Right, center.Y);
        }
        else
        {
            snapped = new SvekPoint(center.X, end.Y < center.Y ? row.Y : row.Bottom);
        }
        var onBorder = SvekGeometry.Contains(ClipShape.Rectangle, main, snapped);
        if (atEnd)
        {
            points[^1] = snapped;
            if (!onBorder)
            {
                points.Add(center);
            }
        }
        else
        {
            points[0] = snapped;
            if (!onBorder)
            {
                points.Insert(0, center);
            }
        }
    }

    /// <summary>
    ///     Moves the ends of a route onto the outlines of the entities, notes, and clusters it connects (dagre ends it on
    ///     the box of the unit, which may hold attached notes, or a shape that is not a rectangle).
    /// </summary>
    /// <param name="work">The scope's work.</param>
    /// <param name="edge">The edge.</param>
    /// <param name="points">The route; its ends are replaced.</param>
    /// <param name="self">Whether the link connects a node to itself.</param>
    private void ClipEnds(ScopeWork work, EdgeWork edge, List<SvekPoint> points, bool self)
    {
        if (points.Count < 2)
        {
            return;
        }

        var fromShape = OutlineOf(work, edge.RepFrom);
        var toShape = OutlineOf(work, edge.RepTo);

        // A straight route aims at the centers, a longer one at the next point of the route; a longer one that
        // arrives across the ranks ends on the side of a rectangle that faces them, not at a corner
        static SvekPoint Center((ClipShape Shape, SceneRect Box)? s, SvekPoint fallback) =>
            s is { } o ? new SvekPoint(o.Box.X + (o.Box.Width / 2), o.Box.Y + (o.Box.Height / 2)) : fallback;
        var straight = points.Count == 2 && !self;
        var leftToRight = _diagram.RankDir == SvekRankDir.LeftToRight;

        // A route across one label rank with no label is drawn straight between the facing sides of its ends (as
        // Graphviz draws a short edge), when that line crosses no other unit: dagre puts the point in the label rank in
        // line with one end, which makes the other end arrive almost level
        if (points.Count == 3 && !self && edge.Slot is null && edge.FromCompound is null && edge.ToCompound is null
            && fromShape is { } fromOutline && toShape is { } toOutline)
        {
            var (fromAim, toAim) = StraightAims(fromOutline.Box, toOutline.Box, leftToRight);
            if (SvekGeometry.ClipToFacingSide(fromOutline.Shape, fromOutline.Box, fromAim, leftToRight) is { } a
                && SvekGeometry.ClipToFacingSide(toOutline.Shape, toOutline.Box, toAim, leftToRight) is { } b
                && Crossed(work, edge, a, b).Count == 0)
            {
                points[0] = a;
                points[1] = new SvekPoint((a.X + b.X) / 2, (a.Y + b.Y) / 2);
                points[2] = b;
                return;
            }
        }

        SvekPoint End((ClipShape Shape, SceneRect Box) outline, SvekPoint toward) =>
            (straight || self ? null : SvekGeometry.ClipToFacingSide(outline.Shape, outline.Box, toward, leftToRight))
            ?? SvekGeometry.Clip(outline.Shape, outline.Box, toward);
        var towardFrom = straight ? Center(toShape, points[^1]) : points[1];
        var towardTo = straight ? Center(fromShape, points[0]) : points[^2];
        if (fromShape is { } from && edge.FromCompound is null && !SvekGeometry.Contains(from.Shape, from.Box, towardFrom))
        {
            points[0] = End(from, towardFrom);
        }

        if (toShape is { } to && edge.ToCompound is null && !SvekGeometry.Contains(to.Shape, to.Box, towardTo))
        {
            points[^1] = End(to, towardTo);
        }
    }

    /// <summary>
    ///     Gets the points the two ends of a link drawn straight between neighboring ranks aim at: where the boxes face
    ///     each other across the ranks (off their corners), a line straight across the ranks at the middle of the
    ///     narrower box, kept within the part they share; else each end in line with the center of the other box.
    /// </summary>
    /// <param name="from">The box of the first end.</param>
    /// <param name="to">The box of the second end.</param>
    /// <param name="leftToRight">Whether the ranks run from left to right (else from top to bottom).</param>
    /// <returns>The point the first end aims at and the point the second end aims at.</returns>
    private static (SvekPoint FromAim, SvekPoint ToAim) StraightAims(SceneRect from, SceneRect to, bool leftToRight)
    {
        static SvekPoint Center(SceneRect r) => new(r.X + (r.Width / 2), r.Y + (r.Height / 2));
        double Start(SceneRect r) => leftToRight ? r.Y : r.X;
        double Extent(SceneRect r) => leftToRight ? r.Height : r.Width;
        double Inset(SceneRect r) => Math.Min(SvekGeometry.EndInset, Extent(r) / 4);
        var (fromCenter, toCenter) = (Center(from), Center(to));
        var low = Math.Max(Start(from) + Inset(from), Start(to) + Inset(to));
        var high = Math.Min(Start(from) + Extent(from) - Inset(from), Start(to) + Extent(to) - Inset(to));
        if (low > high)
        {
            return (toCenter, fromCenter);
        }

        var narrower = Extent(from) <= Extent(to) ? from : to;
        var line = Math.Clamp(Start(narrower) + (Extent(narrower) / 2), low, high);
        return leftToRight
            ? (new SvekPoint(toCenter.X, line), new SvekPoint(fromCenter.X, line))
            : (new SvekPoint(line, toCenter.Y), new SvekPoint(line, fromCenter.Y));
    }

    /// <summary>
    ///     Gets the outline that a link to a node of a scope ends on.
    /// </summary>
    /// <param name="work">The scope's work.</param>
    /// <param name="node">The representative.</param>
    /// <returns>The outline, or null for a compound cluster (the route is cut at its border instead).</returns>
    private static (ClipShape Shape, SceneRect Box)? OutlineOf(ScopeWork work, SvekNode node)
    {
        if (!work.UnitsByNode.TryGetValue(node, out var unit))
        {
            return null;
        }

        var main = unit.MainBox;
        if (node is SvekEntity entity)
        {
            var clip = entity.Image.Clip;
            return (clip.Shape, new SceneRect(main.X + clip.Box.X, main.Y + clip.Box.Y, clip.Box.Width, clip.Box.Height));
        }

        return (ClipShape.Rectangle, main);
    }

    /// <summary>
    ///     Places the label slot of a link: at the center dagre gave it, or above the middle of the route of a link in one
    ///     rank (which dagre gives no label place), then moved away from the drawn curve of the route until the label
    ///     keeps <see cref="LabelClearance"/> from it and overlaps no unit; the note on the link is placed in the slot.
    /// </summary>
    /// <param name="work">The scope's work.</param>
    /// <param name="edge">The edge.</param>
    /// <param name="placed">The placed link.</param>
    private void PlaceLabel(ScopeWork work, EdgeWork edge, WLink placed)
    {
        if (edge.Slot is not { } slot || placed.Route.Count < 2)
        {
            return;
        }

        var center = edge.LabelCenter ?? BesideMiddle(placed.Route, slot.Width, slot.Height);
        var (x, y) = (center.X - (slot.Width / 2), center.Y - (slot.Height / 2));
        if (slot.Text is { } text)
        {
            // (dagre placed the label beside its own route, which the drawn curve no longer follows exactly)
            var shift = ClearOfLink(work, DrawnLines(edge.Link, placed.Route), new SceneRect(x + slot.TextBox.X, y + slot.TextBox.Y, slot.TextBox.Width, slot.TextBox.Height));
            (x, y) = (x + shift.X, y + shift.Y);
            var box = new SceneRect(x + slot.TextBox.X, y + slot.TextBox.Y, slot.TextBox.Width, slot.TextBox.Height);
            double? angle = null;
            if (slot.ArrowSize > 0)
            {
                var direction = NearestLegAngle(placed.Route, new SvekPoint(box.X + (box.Width / 2), box.Y + (box.Height / 2)));
                angle = edge.Link.LabelArrow == LinkLabelArrow.Backward ? direction + Math.PI : direction;
            }

            placed.Label = new WLabel(text, box) { ArrowAngle = angle };
        }

        if (slot.Note is { } note)
        {
            work.LinkNotes.Add(new WNote(note, slot.NoteText!, new SceneRect(x + slot.NoteBox.X, y + slot.NoteBox.Y, slot.NoteBox.Width, slot.NoteBox.Height)));
        }
    }

    /// <summary>
    ///     Gets the lines a link draws: the curve of its route and the outlines of the decorations at its ends.
    /// </summary>
    /// <param name="link">The link.</param>
    /// <param name="route">Its route.</param>
    /// <returns>The lines, as polylines (closed outlines end at their first point).</returns>
    private static List<List<SvekPoint>> DrawnLines(SvekLink link, IReadOnlyList<SvekPoint> route)
    {
        var lines = new List<List<SvekPoint>> { SvekGeometry.Flatten(route, CurveSteps) };
        foreach (var (decor, atEnd) in new[] { (link.TailDecor, false), (link.HeadDecor, true) })
        {
            var extremity = SvekGeometry.Extremity(decor, atEnd ? route[^1] : route[0], SvekGeometry.ArrivalAngle(route, atEnd));
            foreach (var segments in extremity?.Parts.Select(p => p.Segments) ?? [])
            {
                var outline = segments.Where(s => s.Kind != SceneSegmentKind.Close).Select(s => new SvekPoint(s.X, s.Y)).ToList();
                if (outline.Count > 0 && segments[^1].Kind == SceneSegmentKind.Close)
                {
                    outline.Add(outline[0]);
                }

                lines.Add(outline);
            }
        }

        return lines;
    }

    /// <summary>
    ///     Gets how far a label must move, away from the lines its link draws, to keep <see cref="LabelClearance"/> from
    ///     them and overlap no unit: one pixel at a time, at most <see cref="LabelClearSteps"/> pixels.
    /// </summary>
    /// <param name="work">The scope's work.</param>
    /// <param name="lines">The lines of the link (<see cref="DrawnLines"/>); the first is its curve.</param>
    /// <param name="box">The box of the label.</param>
    /// <returns>The shift.</returns>
    private SvekPoint ClearOfLink(ScopeWork work, List<List<SvekPoint>> lines, SceneRect box)
    {
        var center = new SvekPoint(box.X + (box.Width / 2), box.Y + (box.Height / 2));

        // Away from the nearest point of the lines, or, for a label centered on the curve, above a curve that runs
        // across and right of one that runs down
        var nearest = lines[0][0];
        foreach (var line in lines)
        {
            for (var k = 1; k < line.Count; k++)
            {
                var candidate = NearestOnSegment(center, line[k - 1], line[k]);
                if (Distance(center, candidate) < Distance(center, nearest))
                {
                    nearest = candidate;
                }
            }
        }

        var (ax, ay) = (center.X - nearest.X, center.Y - nearest.Y);
        var length = Math.Sqrt((ax * ax) + (ay * ay));
        if (length < 1e-9)
        {
            var angle = NearestLegAngle(lines[0], center);
            (ax, ay, length) = Math.Abs(Math.Sin(angle)) > Math.Abs(Math.Cos(angle)) ? (1.0, 0.0, 1.0) : (0.0, -1.0, 1.0);
        }

        (ax, ay) = (ax / length, ay / length);
        var points = lines.Sum(l => l.Count);
        var shift = 0;
        for (; shift < LabelClearSteps; shift++)
        {
            _meter.Charge(points + work.Units.Count + 1);
            var moved = new SceneRect(box.X + (ax * shift), box.Y + (ay * shift), box.Width, box.Height);
            if (lines.All(l => DistanceToCurve(l, moved) >= LabelClearance - 1e-9) && !work.Units.Any(u => Overlaps(u.Box, moved)))
            {
                break;
            }
        }

        return new SvekPoint(ax * shift, ay * shift);
    }
    /// <summary>
    ///     Gets the distance from a rectangle to a polyline (zero when they touch or cross).
    /// </summary>
    /// <param name="curve">The points of the polyline.</param>
    /// <param name="box">The rectangle.</param>
    /// <returns>The distance.</returns>
    private static double DistanceToCurve(List<SvekPoint> curve, SceneRect box)
    {
        var best = double.PositiveInfinity;
        SvekPoint[] corners = [new(box.X, box.Y), new(box.Right, box.Y), new(box.X, box.Bottom), new(box.Right, box.Bottom)];
        for (var k = 1; k < curve.Count; k++)
        {
            var (a, b) = (curve[k - 1], curve[k]);
            if (SegmentHitsRect(a, b, box))
            {
                return 0;
            }

            best = Math.Min(best, Math.Min(DistanceToRect(a, box), DistanceToRect(b, box)));
            foreach (var corner in corners)
            {
                best = Math.Min(best, Distance(corner, NearestOnSegment(corner, a, b)));
            }
        }

        return best;
    }

    /// <summary>
    ///     Gets the distance from a point to a rectangle (zero inside it).
    /// </summary>
    /// <param name="p">The point.</param>
    /// <param name="box">The rectangle.</param>
    /// <returns>The distance.</returns>
    private static double DistanceToRect(SvekPoint p, SceneRect box)
    {
        var dx = Math.Max(0, Math.Max(box.X - p.X, p.X - box.Right));
        var dy = Math.Max(0, Math.Max(box.Y - p.Y, p.Y - box.Bottom));
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    /// <summary>
    ///     Gets the point of a segment nearest to a point.
    /// </summary>
    /// <param name="p">The point.</param>
    /// <param name="a">The start of the segment.</param>
    /// <param name="b">The end of the segment.</param>
    /// <returns>The nearest point (the start for a segment without length).</returns>
    private static SvekPoint NearestOnSegment(SvekPoint p, SvekPoint a, SvekPoint b)
    {
        var (dx, dy) = (b.X - a.X, b.Y - a.Y);
        var squared = (dx * dx) + (dy * dy);
        if (squared == 0)
        {
            return a;
        }

        var t = Math.Clamp((((p.X - a.X) * dx) + ((p.Y - a.Y) * dy)) / squared, 0, 1);
        return new SvekPoint(a.X + (t * dx), a.Y + (t * dy));
    }

    /// <summary>
    ///     Gets the center of a label beside the middle of a route: above a route that runs across, right of one that
    ///     runs down (the middle of a three-point route is the middle of its curve, not its control point).
    /// </summary>
    /// <param name="route">The route.</param>
    /// <param name="width">The width of the label.</param>
    /// <param name="height">The height of the label.</param>
    /// <returns>The center.</returns>
    private static SvekPoint BesideMiddle(List<SvekPoint> route, double width, double height)
    {
        SvekPoint mid;
        double dx;
        double dy;
        if (route.Count == 3)
        {
            // The quadratic curve at its middle: (a + 2c + b) / 4, running along b - a
            mid = new SvekPoint((route[0].X + (2 * route[1].X) + route[2].X) / 4, (route[0].Y + (2 * route[1].Y) + route[2].Y) / 4);
            (dx, dy) = (route[2].X - route[0].X, route[2].Y - route[0].Y);
        }
        else
        {
            // The point halfway along the polyline, and the direction of its leg
            var total = 0.0;
            for (var k = 1; k < route.Count; k++)
            {
                total += Distance(route[k - 1], route[k]);
            }

            var half = total / 2;
            (mid, dx, dy) = (route[0], route[^1].X - route[0].X, route[^1].Y - route[0].Y);
            for (var k = 1; k < route.Count; k++)
            {
                var leg = Distance(route[k - 1], route[k]);
                if (half <= leg && leg > 0)
                {
                    var t = half / leg;
                    mid = new SvekPoint(route[k - 1].X + (t * (route[k].X - route[k - 1].X)), route[k - 1].Y + (t * (route[k].Y - route[k - 1].Y)));
                    (dx, dy) = (route[k].X - route[k - 1].X, route[k].Y - route[k - 1].Y);
                    break;
                }

                half -= leg;
                mid = route[k];
            }
        }

        return Math.Abs(dy) > Math.Abs(dx)
            ? new SvekPoint(mid.X + (width / 2) + LabelClearance, mid.Y)
            : new SvekPoint(mid.X, mid.Y - (height / 2) - LabelClearance);
    }

    /// <summary>
    ///     Places a label near one end of a route: beside the line, just off the end, and moved along the line until it
    ///     no longer overlaps an entity of the scope (PlantUML moves end labels away from the nodes).
    /// </summary>
    /// <param name="work">The scope's work.</param>
    /// <param name="text">The label text, or null.</param>
    /// <param name="location">The location for reports.</param>
    /// <param name="route">The route.</param>
    /// <param name="atEnd">True for the second end, false for the first.</param>
    /// <returns>The label, or null.</returns>
    private WLabel? EndLabel(ScopeWork work, string? text, PlantUmlLocation location, List<SvekPoint> route, bool atEnd)
    {
        if (text is null || route.Count < 2)
        {
            return null;
        }

        var block = Measure(text, SkinElement.SvekArrow, location);
        var (w, h) = (block.Width + (2 * LabelMargin), block.Height + (2 * LabelMargin));

        // The direction into the line from the end, and the side of the label
        var end = atEnd ? route[^1] : route[0];
        var angle = SvekGeometry.ArrivalAngle(route, atEnd) + Math.PI;
        var along = new SvekPoint(Math.Cos(angle), Math.Sin(angle));
        var (ux, uy) = (along.X, along.Y);
        var across = new SvekPoint(uy, -ux);
        var halfAlong = ((Math.Abs(along.X) * w) + (Math.Abs(along.Y) * h)) / 2;
        var halfAcross = ((Math.Abs(across.X) * w) + (Math.Abs(across.Y) * h)) / 2;
        SceneRect BoxAt(double distance)
        {
            var (cx, cy) = (end.X + (along.X * distance) + (across.X * (halfAcross + LabelOffset)), end.Y + (along.Y * distance) + (across.Y * (halfAcross + LabelOffset)));
            return new SceneRect(cx - (w / 2), cy - (h / 2), w, h);
        }

        var distance = halfAlong + LabelMargin;
        var box = BoxAt(distance);
        for (var step = 0; step < 32 && work.Units.Any(u => Overlaps(u.MainBox, box)); step++)
        {
            _meter.Charge(work.Units.Count + 1);
            distance += 2;
            box = BoxAt(distance);
        }

        return new WLabel(block, box);
    }

    /// <summary>
    ///     Draws the floating notes with a single link as pointers to the other end of the link, when the link is a
    ///     short direct route in this placement, and hides those links.
    /// </summary>
    /// <param name="frame">The placement of the scope.</param>
    private void PointNotes(Frame frame)
    {
        foreach (var (note, link) in _pointerNotes)
        {
            var placedLink = frame.Links.FirstOrDefault(l => ReferenceEquals(l.Link, link));
            var placedNote = frame.Notes.FirstOrDefault(n => ReferenceEquals(n.Note, note));
            if (placedLink is null || placedNote is null || placedLink.Route.Count is < 2 or > 3)
            {
                continue;
            }

            var noteAtStart = ReferenceEquals(link.From, note);
            var pointerBase = noteAtStart ? placedLink.Route[0] : placedLink.Route[^1];
            var tip = noteAtStart ? placedLink.Route[^1] : placedLink.Route[0];
            placedNote.Side = NearestSide(placedNote.Box, pointerBase);
            (placedNote.Base, placedNote.Tip) = (pointerBase, tip);
            placedLink.Drawn = false;
        }
    }

    /// <summary>
    ///     Gets the side of a box nearest to a point (PlantUML's <c>getOpaleStrategy</c>, left, right, top, bottom on
    ///     ties).
    /// </summary>
    /// <param name="box">The box.</param>
    /// <param name="point">The point.</param>
    /// <returns>The side.</returns>
    private static NotePosition NearestSide(SceneRect box, SvekPoint point)
    {
        var right = Math.Abs(box.Right - point.X);
        var bottom = Math.Abs(box.Bottom - point.Y);
        var left = Math.Abs(box.X - point.X);
        var top = Math.Abs(box.Y - point.Y);
        if (left <= right && left <= bottom && left <= top)
        {
            return NotePosition.Left;
        }

        if (right <= bottom && right <= left && right <= top)
        {
            return NotePosition.Right;
        }

        return top <= right && top <= bottom && top <= left ? NotePosition.Top : NotePosition.Bottom;
    }

    /// <summary>
    ///     Gets the direction of the leg of a route nearest to a point.
    /// </summary>
    /// <param name="route">The route.</param>
    /// <param name="point">The point.</param>
    /// <returns>The angle of the leg in radians (y down).</returns>
    private static double NearestLegAngle(List<SvekPoint> route, SvekPoint point)
    {
        var best = double.PositiveInfinity;
        var angle = 0.0;
        for (var k = 1; k < route.Count; k++)
        {
            var (a, b) = (route[k - 1], route[k]);
            if (a == b)
            {
                continue;
            }

            var d = DistanceToSegment(point, a, b);
            if (d < best)
            {
                (best, angle) = (d, Math.Atan2(b.Y - a.Y, b.X - a.X));
            }
        }

        return angle;
    }

    /// <summary>
    ///     Decides whether a segment crosses the inside of a rectangle (Liang-Barsky clipping).
    /// </summary>
    /// <param name="a">The start.</param>
    /// <param name="b">The end.</param>
    /// <param name="box">The rectangle.</param>
    /// <returns>True when a part of the segment of positive length is inside.</returns>
    private static bool SegmentHitsRect(SvekPoint a, SvekPoint b, SceneRect box)
    {
        var (t0, t1) = (0.0, 1.0);
        var (dx, dy) = (b.X - a.X, b.Y - a.Y);
        foreach (var (p, q) in new[] { (-dx, a.X - box.X), (dx, box.Right - a.X), (-dy, a.Y - box.Y), (dy, box.Bottom - a.Y) })
        {
            if (p == 0)
            {
                if (q <= 0)
                {
                    return false;
                }

                continue;
            }

            var r = q / p;
            if (p < 0)
            {
                t0 = Math.Max(t0, r);
            }
            else
            {
                t1 = Math.Min(t1, r);
            }
        }

        return t1 - t0 > 1e-9;
    }

    /// <summary>
    ///     Decides whether two rectangles overlap with an area.
    /// </summary>
    /// <param name="a">The first.</param>
    /// <param name="b">The second.</param>
    /// <returns>True when they do.</returns>
    private static bool Overlaps(SceneRect a, SceneRect b) =>
        a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom;

    /// <summary>
    ///     Gets the distance between two points.
    /// </summary>
    /// <param name="a">The first.</param>
    /// <param name="b">The second.</param>
    /// <returns>The distance.</returns>
    private static double Distance(SvekPoint a, SvekPoint b) => Math.Sqrt(((b.X - a.X) * (b.X - a.X)) + ((b.Y - a.Y) * (b.Y - a.Y)));

    /// <summary>
    ///     Gets the distance from a point to a segment.
    /// </summary>
    /// <param name="p">The point.</param>
    /// <param name="a">The start of the segment.</param>
    /// <param name="b">The end of the segment.</param>
    /// <returns>The distance.</returns>
    private static double DistanceToSegment(SvekPoint p, SvekPoint a, SvekPoint b)
    {
        var (dx, dy) = (b.X - a.X, b.Y - a.Y);
        var t = Math.Clamp((((p.X - a.X) * dx) + ((p.Y - a.Y) * dy)) / ((dx * dx) + (dy * dy)), 0, 1);
        return Distance(p, new SvekPoint(a.X + (t * dx), a.Y + (t * dy)));
    }

    /// <summary>
    ///     The label slot of a link: what dagre reserves room for beside the link.
    /// </summary>
    /// <param name="Width">The width.</param>
    /// <param name="Height">The height.</param>
    /// <param name="Text">The label text, or null.</param>
    /// <param name="TextBox">The box of the label (with its margin and triangle) in the slot.</param>
    /// <param name="ArrowSize">The size of the triangle at the left of the label, or zero.</param>
    /// <param name="Note">The note on the link, or null.</param>
    /// <param name="NoteText">The text of the note.</param>
    /// <param name="NoteBox">The box of the note in the slot.</param>
    private sealed record LabelSlot(double Width, double Height, TextBlock? Text, SceneRect TextBox, double ArrowSize, SvekNote? Note, TextBlock? NoteText, SceneRect NoteBox);

    /// <summary>
    ///     The edge of a link in a scope's graph.
    /// </summary>
    /// <param name="link">The link.</param>
    /// <param name="label">The dagre edge label.</param>
    /// <param name="reversed">Whether the edge runs from the second end to the first.</param>
    /// <param name="repFrom">The node that stands for the first end.</param>
    /// <param name="repTo">The node that stands for the second end.</param>
    private sealed class EdgeWork(SvekLink link, DagreEdge label, bool reversed, SvekNode repFrom, SvekNode repTo)
    {
        /// <summary>Gets the link.</summary>
        public SvekLink Link { get; } = link;

        /// <summary>Gets the dagre edge label.</summary>
        public DagreEdge Label { get; } = label;

        /// <summary>Gets a value indicating whether the edge runs from the second end to the first.</summary>
        public bool Reversed { get; } = reversed;

        /// <summary>Gets the node that stands for the first end.</summary>
        public SvekNode RepFrom { get; } = repFrom;

        /// <summary>Gets the node that stands for the second end.</summary>
        public SvekNode RepTo { get; } = repTo;

        /// <summary>Gets the label slot, or null.</summary>
        public LabelSlot? Slot { get; init; }

        /// <summary>Gets the name of the dagre edge.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Gets a value indicating whether the edge only keeps an attached note beside its entity (never drawn).</summary>
        public bool Synthetic { get; init; }

        /// <summary>Gets or sets the compound cluster the first end is, or null.</summary>
        public Compound? FromCompound { get; set; }

        /// <summary>Gets or sets the compound cluster the second end is, or null.</summary>
        public Compound? ToCompound { get; set; }

        /// <summary>Gets the minimum number of ranks the edge spans.</summary>
        public int MinLen { get; init; }

        /// <summary>Gets or sets a value indicating whether both ends are in one row (the link is drawn in the row).</summary>
        public bool InRow { get; set; }

        /// <summary>Gets or sets the route dagre gave, in the direction of the dagre edge.</summary>
        public List<SvekPoint> Points { get; set; } = [];

        /// <summary>Gets or sets the label center dagre gave, or null.</summary>
        public SvekPoint? LabelCenter { get; set; }

        /// <summary>Gets or sets the placed link.</summary>
        public WLink? Placed { get; set; }
    }
}
