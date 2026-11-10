// Portions derived from mermaid (MIT); see third-party/README.md
using System.Globalization;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Rendering;

namespace DemaConsulting.CanvasNet.Mermaid.State;

/// <content>
///     The data fetcher (Mermaid's <c>dataFetcher.ts</c>), which maps the translated statements to the nodes and edges of
///     the layout when the root document is set, and the mapping of those nodes and edges to the renderer's
///     <see cref="LayoutData"/> (Mermaid's <c>getData</c>).
/// </content>
internal sealed partial class StateModel
{
    /// <summary>The markers a state diagram defines (the unified state renderer's <c>markers</c>).</summary>
    private static readonly string[] StateMarkers = ["barb"];

    /// <summary>
    ///     Maps the model to the renderer's input under the configuration of a render (Mermaid's <c>getData</c>).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The nodes and edges are those of the data fetcher, in its order, with the render's look: states with Mermaid's
    ///         radius 10, padding 8 and centered label (a state with descriptions has the first as its label and the others
    ///         as <see cref="LayoutNode.Descriptions"/>), composite states and concurrency regions as clusters with their
    ///         direction, notes with the flowchart padding inside note groups of padding 16, and edges with the barb end
    ///         (<c>arrow_barb</c>) for transitions and none for the edges of notes. A node whose shape Mermaid leaves
    ///         undefined (a note on a state that appears nowhere before) has an empty <see cref="LayoutNode.Shape"/>.
    ///     </para>
    ///     <para>
    ///         Mermaid's state edges have no <c>minlen</c>, so dagre's default of 1 applies: every edge has a
    ///         <see cref="LayoutEdge.MinLen"/> of 1. The diagram type is <c>stateDiagram</c> for the legacy id too, because
    ///         the legacy diagram is drawn by the unified renderer as well.
    ///     </para>
    /// </remarks>
    /// <param name="config">The effective configuration of the render.</param>
    /// <returns>The layout data.</returns>
    public LayoutData GetData(MermaidConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var look = config.Look;
        var nodes = _nodes.Select(node => new LayoutNode
        {
            Id = node.Id,
            Label = node.Label,
            LabelType = node.Kind == StateNodeKind.NoteGroup ? null : "markdown",
            ParentId = node.ParentId,
            Padding = node.Kind switch
            {
                StateNodeKind.Note => config.Flowchart.Padding,
                StateNodeKind.NoteGroup => 16,
                _ => 8,
            },
            CssStyles = node.CssStyles,
            CssCompiledStyles = node.CssCompiledStyles,
            CssClasses = node.CssClasses,
            Dir = node.Dir,
            DomId = node.DomId,
            Look = look,
            IsGroup = node.IsGroup,
            Shape = node.Shape ?? string.Empty,
            Rx = node.Kind == StateNodeKind.State ? 10 : null,
            Ry = node.Kind == StateNodeKind.State ? 10 : null,
            CenterLabel = node.Kind == StateNodeKind.State,
            Descriptions = node.Descriptions,
            Location = node.Location,
        }).ToList();
        var edges = _edges.Select(edge => new LayoutEdge
        {
            Id = edge.Id,
            Start = edge.Start,
            End = edge.End,
            Type = edge.IsNote ? "none" : "normal",
            Label = edge.Label ?? string.Empty,
            LabelType = "markdown",
            LabelPos = "c",
            Thickness = "normal",
            MinLen = 1,
            Classes = edge.IsNote ? "transition note-edge" : "transition",
            ArrowTypeEnd = edge.IsNote ? string.Empty : "arrow_barb",
            ArrowheadStyle = "fill: #333",
            Style = ["fill:none"],
            Look = look,
            Location = edge.Location,
        }).ToList();

        return new LayoutData(nodes, edges, config)
        {
            DiagramType = "stateDiagram",
            Direction = LayoutDirection,
            NodeSpacing = config.State.NodeSpacing,
            RankSpacing = config.State.RankSpacing,
            Markers = StateMarkers,
        };
    }

    /// <summary>
    ///     Fetches the statements of a document (Mermaid's <c>setupDoc</c>): states, and transitions with both their states
    ///     and an edge.
    /// </summary>
    /// <param name="parent">The statement that holds the document.</param>
    /// <param name="document">The statements.</param>
    /// <param name="altFlag">Whether a composite state of the document has the alternate cluster class.</param>
    /// <param name="level">The nesting level of the document's clusters: 1 at the top.</param>
    private void FetchDocument(StateStatement parent, List<StateStatement> document, bool altFlag, int level)
    {
        foreach (var item in document)
        {
            switch (item.Stmt)
            {
                case "state":
                    Fetch(parent, item, altFlag, level);
                    break;
                case "relation":
                    Fetch(parent, item.State1!, altFlag, level);
                    Fetch(parent, item.State2!, altFlag, level);
                    if (_transitionCount >= _limits.MaxEdges)
                    {
                        throw new MermaidSyntaxException(
                            MermaidDiagnosticCodes.LimitExceeded,
                            string.Create(CultureInfo.InvariantCulture, $"the diagram has more than {_limits.MaxEdges} transitions (MermaidLimits.MaxEdges)"),
                            item.Location,
                            "split the diagram, or raise MermaidLimits.MaxEdges when the source is trusted");
                    }

                    _transitionCount++;
                    _edges.Add(new StateEdge
                    {
                        Id = string.Create(CultureInfo.InvariantCulture, $"edge{_graphItemCount}"),
                        Start = item.State1!.Id,
                        End = item.State2!.Id,
                        Label = Sanitize(item.Description ?? string.Empty),
                        IsNote = false,
                        Location = item.Location,
                    });
                    _graphItemCount++;
                    break;
            }
        }
    }

    /// <summary>
    ///     Fetches one state statement (Mermaid's <c>dataFetcher</c>): its node, created or updated, its note with the note
    ///     group and edge, and its document.
    /// </summary>
    /// <param name="parent">The statement whose document holds the state.</param>
    /// <param name="item">The state statement.</param>
    /// <param name="altFlag">Whether a composite state here has the alternate cluster class.</param>
    /// <param name="level">The nesting level of a cluster here: 1 at the top.</param>
    private void Fetch(StateStatement parent, StateStatement item, bool altFlag, int level)
    {
        var itemId = item.Id;
        if (itemId != RootId)
        {
            FetchState(parent, item, altFlag, level);
        }

        if (item.Doc is not null)
        {
            FetchDocument(item, item.Doc, !altFlag, level + 1);
        }
    }

    /// <summary>
    ///     Creates or updates the node of a state statement, and adds its note.
    /// </summary>
    /// <param name="parent">The statement whose document holds the state.</param>
    /// <param name="item">The state statement.</param>
    /// <param name="altFlag">Whether a composite state here has the alternate cluster class.</param>
    /// <param name="level">The nesting level of a cluster here: 1 at the top.</param>
    private void FetchState(StateStatement parent, StateStatement item, bool altFlag, int level)
    {
        var itemId = item.Id;

        // The shape of a new state: from its start flag, then from its type (undefined for a note statement)
        var shape = item.Start switch
        {
            true => "stateStart",
            false => "stateEnd",
            _ => "rect",
        };
        if (item.Type != DefaultType)
        {
            shape = item.Type;
        }

        if (!_fetched.TryGetValue(itemId, out var state))
        {
            var record = _stateIndex.GetValueOrDefault(itemId);
            var classes = record is null ? string.Empty : string.Join(' ', record.Classes);
            state = new FetchedState
            {
                Shape = shape,
                Text = Sanitize(itemId),
                CssClasses = $"{classes} {StateClass}",
                CssStyles = record is null ? [] : [.. record.Styles],
            };
            _fetched[itemId] = state;
        }

        // The descriptions: the first replaces the id, the next ones make a state with a title
        if (DescriptionsOf(item).Count > 0)
        {
            AddFetchedDescriptions(state, itemId, item);
        }

        if (state.Length == 1 && state.Shape == "rectWithTitle")
        {
            state.Shape = state.Type == "group" ? "roundedWithTitle" : "rect";
        }

        // A state with a document is a cluster: a region or a composite state
        if (item.Doc is not null)
        {
            CheckDepth(level, item.Location);
            if (state.Type is null)
            {
                state.Type = "group";
                state.Dir = DirectionOf(item.Doc);
                state.Shape = item.Type == DividerType ? DividerType : "roundedWithTitle";
                state.CssClasses = $"{state.CssClasses} statediagram-cluster {(altFlag ? "statediagram-cluster-alt" : string.Empty)}";
            }
        }

        var node = new StateNode
        {
            Id = itemId,
            Kind = StateNodeKind.State,
            Shape = state.Shape,
            Label = state.Shape == DividerType ? string.Empty : state.Text,
            LabelParts = state.Shape == DividerType ? null : state.Parts,
            CssClasses = state.CssClasses,
            CssStyles = state.CssStyles,
            Dir = state.Dir,
            DomId = string.Create(CultureInfo.InvariantCulture, $"state-{itemId}-{_graphItemCount}"),
            IsGroup = state.Type == "group",
            ParentId = parent.Id != RootId ? parent.Id : null,
            Location = item.Location,
        };

        if (item.Note is not { } note)
        {
            Insert(node);
            return;
        }

        // A note: a note node inside a note group, and an edge between the note and the state
        var noteNode = new StateNode
        {
            Id = string.Create(CultureInfo.InvariantCulture, $"{itemId}----note-{_graphItemCount}"),
            Kind = StateNodeKind.Note,
            Shape = "note",
            Label = note.Text,
            CssClasses = "statediagram-note",
            CssStyles = [],
            DomId = string.Create(CultureInfo.InvariantCulture, $"state-{itemId}----note-{_graphItemCount}"),
            IsGroup = state.Type == "group",
            ParentId = itemId + "----parent",
            Position = note.Position,
            Location = item.Location,
        };
        var groupNode = new StateNode
        {
            Id = itemId + "----parent",
            Kind = StateNodeKind.NoteGroup,
            Shape = "noteGroup",
            Label = note.Text,
            CssClasses = state.CssClasses,
            CssStyles = [],
            DomId = string.Create(CultureInfo.InvariantCulture, $"state-{itemId}----parent-{_graphItemCount}"),
            IsGroup = true,
            Position = note.Position,
            Location = item.Location,
        };
        _graphItemCount++;
        Insert(groupNode);
        Insert(noteNode);
        Insert(node);

        var (from, to) = note.Position == "left of" ? (noteNode.Id, itemId) : (itemId, noteNode.Id);
        _edges.Add(new StateEdge { Id = $"{from}-{to}", Start = from, End = to, IsNote = true, Location = item.Location });
    }

    /// <summary>
    ///     Adds the descriptions of a statement to the fetched record of its state, as the data fetcher does: a first
    ///     description replaces the id, and each further one makes the state a state with a title (the list is sanitized
    ///     again as a whole, as <c>sanitizeTextOrArray</c> does).
    /// </summary>
    /// <param name="state">The fetched record.</param>
    /// <param name="itemId">The state id.</param>
    /// <param name="item">The statement, with at least one description.</param>
    private void AddFetchedDescriptions(FetchedState state, string itemId, StateStatement item)
    {
        var descriptions = DescriptionsOf(item);
        List<string> parts;
        if (state.Parts is not null)
        {
            state.Shape = "rectWithTitle";
            parts = [.. state.Parts, .. descriptions];
        }
        else if (state.Text?.Length > 0)
        {
            state.Shape = "rectWithTitle";
            parts = state.Text == itemId ? [.. descriptions] : [state.Text, .. descriptions];
        }
        else if (item.DescriptionParts is not null)
        {
            state.Shape = "rect";
            parts = [.. descriptions];
        }
        else
        {
            state.Shape = "rect";
            state.Text = Sanitize(descriptions[0]);
            return;
        }

        // A new list each time, as sanitizeTextOrArray makes one; the nodes fetched before keep theirs
        state.Parts = [.. parts.Select(Sanitize)];
        state.Text = null;

        // The descriptions after the title are drawn joined with line breaks
        var rest = state.Parts.Skip(1).ToList();
        CheckLabelLength(rest.Sum(text => (long)text.Length) + (Math.Max(0, rest.Count - 1) * 5L), item.Location);
    }

    /// <summary>
    ///     Inserts a node, or updates the node of the same id (Mermaid's <c>insertOrUpdateNode</c>): the styles of its
    ///     style classes are compiled, and an update keeps the parent of a node that has none.
    /// </summary>
    /// <param name="node">The node.</param>
    private void Insert(StateNode node)
    {
        if (node.Id.Length == 0 || node.Id == "</join></fork>" || node.Id == "</choice>")
        {
            return;
        }

        var compiled = new List<string>();
        foreach (var name in node.CssClasses.Split(' '))
        {
            if (_styleClassIndex.TryGetValue(name, out var styleClass))
            {
                compiled.AddRange(styleClass.Styles);
            }
        }

        node.CssCompiledStyles = compiled;
        if (_nodeIndex.TryGetValue(node.Id, out var existing))
        {
            existing.Assign(node);
            return;
        }

        CheckNodeCount(_nodes.Count, node.Location);
        _nodes.Add(node);
        _nodeIndex[node.Id] = node;
    }

    /// <summary>
    ///     Gives each node with descriptions its first as the label and the others as its descriptions (the end of
    ///     Mermaid's <c>extract</c>).
    /// </summary>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when a cluster has descriptions beyond its title.</exception>
    private void ProcessLabels()
    {
        foreach (var node in _nodes)
        {
            if (node.LabelParts is not { } parts)
            {
                continue;
            }

            node.Descriptions = parts[1..];
            if (node.IsGroup && node.Descriptions.Count > 0)
            {
                throw new MermaidSyntaxException(
                    MermaidDiagnosticCodes.SyntaxError,
                    $"Group nodes can only have label. Remove the additional description for node [{node.Id}]",
                    node.Location,
                    "a composite state takes one description, its title; remove the other descriptions of the state");
            }

            node.Label = parts.Count > 0 ? parts[0] : null;
            node.LabelParts = null;
        }
    }

    /// <summary>
    ///     Checks the nesting level of a cluster (a composite state or a concurrency region) against
    ///     <see cref="MermaidLimits.MaxNestingDepth"/>.
    /// </summary>
    /// <param name="level">The level: 1 at the top.</param>
    /// <param name="location">The location of the statement.</param>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when the cluster is too deep.</exception>
    private void CheckDepth(int level, MermaidLocation location)
    {
        if (level > _limits.MaxNestingDepth)
        {
            throw new MermaidSyntaxException(
                MermaidDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"composite states and their concurrent regions are nested deeper than {_limits.MaxNestingDepth} levels (MermaidLimits.MaxNestingDepth)"),
                location,
                "nest fewer composite states or concurrent regions, or raise MermaidLimits.MaxNestingDepth when the source is trusted");
        }
    }

    /// <summary>
    ///     The data fetcher's record of a state id (an entry of Mermaid's <c>nodeDb</c>).
    /// </summary>
    private sealed class FetchedState
    {
        /// <summary>Gets or sets the shape, or null when Mermaid leaves it undefined.</summary>
        public string? Shape { get; set; }

        /// <summary>Gets or sets the description when it is one text, or null when it is a list.</summary>
        public string? Text { get; set; }

        /// <summary>Gets or sets the descriptions when they are a list, or null.</summary>
        public List<string>? Parts { get; set; }

        /// <summary>Gets or sets the CSS classes.</summary>
        public required string CssClasses { get; set; }

        /// <summary>Gets the styles of the state's <c>style</c> statements.</summary>
        public required IReadOnlyList<string> CssStyles { get; init; }

        /// <summary>Gets or sets the type: <c>group</c> for a cluster, else null.</summary>
        public string? Type { get; set; }

        /// <summary>Gets or sets the direction of a cluster, or null.</summary>
        public string? Dir { get; set; }

        /// <summary>Gets the length of the description: of the text, or the number of descriptions.</summary>
        public int Length => Parts?.Count ?? Text?.Length ?? 0;
    }
}

/// <summary>
///     The kind of a node of the layout of a state diagram.
/// </summary>
internal enum StateNodeKind
{
    /// <summary>A state, composite state or concurrency region.</summary>
    State,

    /// <summary>A note.</summary>
    Note,

    /// <summary>The group around the notes of a state.</summary>
    NoteGroup,
}

/// <summary>
///     A node of the layout of a state diagram (Mermaid's <c>NodeData</c>, as the data fetcher makes it).
/// </summary>
/// <remarks>The data fetcher creates and updates the nodes; once parsing is over they are never changed.</remarks>
internal sealed class StateNode
{
    /// <summary>Gets the id.</summary>
    public required string Id { get; init; }

    /// <summary>Gets or sets the kind.</summary>
    public required StateNodeKind Kind { get; set; }

    /// <summary>Gets or sets the shape, or null when Mermaid leaves it undefined.</summary>
    public string? Shape { get; set; }

    /// <summary>Gets or sets the label (for a state with descriptions, the first), or null.</summary>
    public string? Label { get; set; }

    /// <summary>Gets or sets the descriptions while the label is a list, before the labels are processed.</summary>
    public List<string>? LabelParts { get; set; }

    /// <summary>Gets or sets the descriptions after the first, for a node whose label was a list; else null.</summary>
    public IReadOnlyList<string>? Descriptions { get; set; }

    /// <summary>Gets or sets the CSS classes.</summary>
    public required string CssClasses { get; set; }

    /// <summary>Gets or sets the styles of the <c>style</c> statements.</summary>
    public required IReadOnlyList<string> CssStyles { get; set; }

    /// <summary>Gets or sets the styles of the node's style classes, compiled in class order.</summary>
    public IReadOnlyList<string> CssCompiledStyles { get; set; } = [];

    /// <summary>Gets or sets the direction of a cluster, or null.</summary>
    public string? Dir { get; set; }

    /// <summary>Gets or sets the DOM id.</summary>
    public required string DomId { get; set; }

    /// <summary>Gets or sets a value indicating whether the node is a cluster.</summary>
    public bool IsGroup { get; set; }

    /// <summary>Gets or sets the id of the cluster that holds the node, or null.</summary>
    public string? ParentId { get; set; }

    /// <summary>Gets or sets the position of a note or note group (<c>left of</c> or <c>right of</c>), or null.</summary>
    public string? Position { get; set; }

    /// <summary>Gets or sets the location of the statement that last set the node.</summary>
    public MermaidLocation Location { get; set; }

    /// <summary>
    ///     Updates the node from a node of the same id (JavaScript's <c>Object.assign</c>): every field is taken, except a
    ///     parent the newer node does not have.
    /// </summary>
    /// <param name="node">The newer node.</param>
    public void Assign(StateNode node)
    {
        Kind = node.Kind;
        Shape = node.Shape;
        Label = node.Label;
        LabelParts = node.LabelParts;
        CssClasses = node.CssClasses;
        CssStyles = node.CssStyles;
        CssCompiledStyles = node.CssCompiledStyles;
        Dir = node.Dir;
        DomId = node.DomId;
        IsGroup = node.IsGroup;
        ParentId = node.ParentId ?? ParentId;
        Position = node.Position;
        Location = node.Location;
    }
}

/// <summary>
///     An edge of the layout of a state diagram: a transition, or the link between a note and its state.
/// </summary>
/// <remarks>The record is immutable.</remarks>
internal sealed record StateEdge
{
    /// <summary>Gets the id: <c>edge</c> and the data fetcher's counter, or the ids of the note's ends.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the id of the start node.</summary>
    public required string Start { get; init; }

    /// <summary>Gets the id of the end node.</summary>
    public required string End { get; init; }

    /// <summary>Gets the sanitized label of a transition, or null for the edge of a note (which has none).</summary>
    public string? Label { get; init; }

    /// <summary>Gets a value indicating whether the edge links a note to its state.</summary>
    public required bool IsNote { get; init; }

    /// <summary>Gets the location of the statement.</summary>
    public MermaidLocation Location { get; init; }
}
