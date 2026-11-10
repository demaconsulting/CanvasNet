// Portions derived from mermaid (MIT); see third-party/README.md
using System.Globalization;
using System.Text;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.State;

/// <summary>
///     A parsed state diagram: a port of Mermaid's state database (<c>stateDb.ts</c>, with the constants of
///     <c>stateCommon.ts</c>) and of its data fetcher (<c>dataFetcher.ts</c>), which the parser fills with the statement
///     tree as Mermaid's grammar builds it.
/// </summary>
/// <remarks>
///     <para>
///         <b>Fidelity.</b> The parser builds Mermaid's statement tree (states, transitions, notes, composite documents,
///         concurrency dividers, style statements, clicks and directions); <see cref="SetRootDocument"/> then runs what
///         Mermaid's <c>setRootDoc</c> runs. The tree is translated (<c>docTranslator</c>): <c>[*]</c> becomes
///         <c>&lt;parent&gt;_start</c> or <c>&lt;parent&gt;_end</c> (<c>root_start</c> and <c>root_end</c> at the top), and
///         the statements of a composite state with <c>--</c> dividers are split into divider regions, the last one named
///         with Mermaid's generated id (<c>id-random-N</c> here: Mermaid's <c>generateId</c> puts a random number where this
///         port writes <c>random</c>, so the ids stay deterministic). The top-level statements are then applied to the
///         database (<c>extract</c>): states with their descriptions, notes, CSS classes and styles, transitions, style
///         classes (<c>classDef</c>) and links (kept, never drawn). The legacy diagram (Mermaid id <c>state</c>, version 1)
///         applies them before the translation, as Mermaid does, so its start and end states are <c>start1</c>,
///         <c>start2</c>, ... in the database. Last, the data fetcher maps the translated tree to the nodes and edges of the
///         layout, with Mermaid's DOM ids (<c>state-&lt;id&gt;-&lt;n&gt;</c>, where <c>n</c> counts the transitions and
///         notes before), shapes, descriptions, cluster classes, note groups and note edges.
///     </para>
///     <para>
///         <b>Errors and limits.</b> A composite state with a description beyond its title is a located
///         <see cref="MermaidSyntaxException"/> with code <see cref="MermaidDiagnosticCodes.SyntaxError"/> and Mermaid's
///         message. The states of the database, and the nodes of the layout (states, composite states, concurrency regions,
///         notes and note groups) count against <see cref="MermaidLimits.MaxNodes"/>, as the flowchart model counts
///         vertices and subgraphs; transitions count against <see cref="MermaidLimits.MaxEdges"/>, as class relations do
///         (the edge of a note comes with its note and is bounded by the nodes). Composite states and their concurrency
///         regions count as nesting levels against <see cref="MermaidLimits.MaxNestingDepth"/>, because each nests one
///         more cluster. The texts the model joins or derives are checked against <see cref="MermaidLimits.MaxLabelLength"/>:
///         the descriptions of a state drawn under its title (joined with line breaks), the ids of start and end states
///         (their parent's id with <c>_start</c> or <c>_end</c>) and the text styles of a style class
///         (<see cref="MermaidDiagnosticCodes.LimitExceeded"/>).
///     </para>
///     <para>
///         The model is filled by one parser on one thread; once parsing is over it is never changed, so it is safe for
///         concurrent use.
///     </para>
/// </remarks>
internal sealed partial class StateModel : IMermaidDiagram
{
    /// <summary>The type of a state without a special type.</summary>
    public const string DefaultType = "default";

    /// <summary>The type of a concurrency divider (a region of a composite state).</summary>
    public const string DividerType = "divider";

    /// <summary>The id of the root document.</summary>
    private const string RootId = "root";

    /// <summary>The CSS class of every state node (Mermaid's <c>CSS_DIAGRAM_STATE</c>).</summary>
    private const string StateClass = "statediagram-state";

    /// <summary>The limits of the open call.</summary>
    private readonly MermaidLimits _limits;

    /// <summary>The states of the top-level document in insertion order (Mermaid's <c>getStates</c>).</summary>
    private readonly List<StateRecord> _states = [];

    /// <summary>The states by id.</summary>
    private readonly Dictionary<string, StateRecord> _stateIndex = new(StringComparer.Ordinal);

    /// <summary>The transitions of the top-level document (Mermaid's <c>getRelations</c>).</summary>
    private readonly List<StateRelation> _relations = [];

    /// <summary>The style classes in insertion order (Mermaid's <c>getClasses</c>).</summary>
    private readonly List<StateStyleClass> _styleClasses = [];

    /// <summary>The style classes by id.</summary>
    private readonly Dictionary<string, StateStyleClass> _styleClassIndex = new(StringComparer.Ordinal);

    /// <summary>The links of the click statements (Mermaid's <c>getLinks</c>).</summary>
    private readonly List<StateLink> _links = [];

    /// <summary>The nodes of the layout, in insertion order.</summary>
    private readonly List<StateNode> _nodes = [];

    /// <summary>The nodes of the layout by id.</summary>
    private readonly Dictionary<string, StateNode> _nodeIndex = new(StringComparer.Ordinal);

    /// <summary>The data fetcher's record of each state id (Mermaid's <c>nodeDb</c>).</summary>
    private readonly Dictionary<string, FetchedState> _fetched = new(StringComparer.Ordinal);

    /// <summary>The edges of the layout, in insertion order.</summary>
    private readonly List<StateEdge> _edges = [];

    /// <summary>The top-level statements (Mermaid's <c>rootDoc</c>).</summary>
    private List<StateStatement> _rootDocument = [];

    /// <summary>The number of divider ids given out (Mermaid's <c>dividerCnt</c>).</summary>
    private int _dividerCount;

    /// <summary>The number of region ids generated by the translation (the counter of Mermaid's <c>generateId</c>).</summary>
    private int _generatedCount;

    /// <summary>The number of start and end states of the legacy database (Mermaid's <c>startEndCount</c>).</summary>
    private int _startEndCount;

    /// <summary>The data fetcher's counter of transitions and notes (Mermaid's <c>graphItemCount</c>).</summary>
    private int _graphItemCount;

    /// <summary>The number of transitions of the layout.</summary>
    private int _transitionCount;

    /// <summary>
    ///     Initializes an empty model.
    /// </summary>
    /// <param name="limits">The limits of the open call.</param>
    /// <param name="keywordLocation">The location of the diagram keyword.</param>
    /// <param name="mermaidId">Mermaid's id of the detected diagram: <c>stateDiagram</c>, or <c>state</c> for the legacy id.</param>
    public StateModel(MermaidLimits limits, MermaidLocation keywordLocation, string mermaidId)
    {
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(mermaidId);
        _limits = limits;
        KeywordLocation = keywordLocation;
        MermaidId = mermaidId;
    }

    /// <summary>Gets the location of the diagram keyword.</summary>
    public MermaidLocation KeywordLocation { get; }

    /// <summary>
    ///     Gets Mermaid's id of the diagram (<c>stateDiagram</c>, or the legacy <c>state</c> that a non-default
    ///     <c>state.defaultRenderer</c> selects for the <c>stateDiagram</c> keyword), its <c>diag.type</c>.
    /// </summary>
    public string MermaidId { get; }

    /// <summary>Gets a value indicating whether the diagram is the legacy one (Mermaid's <c>StateDB(1)</c>).</summary>
    public bool IsLegacy => MermaidId == "state";

    /// <summary>Gets the direction Mermaid's <c>getDirection</c> reports: the first top-level direction statement, else <c>TB</c>.</summary>
    public string Direction => _rootDocument.FirstOrDefault(statement => statement.Stmt == "dir")?.Value ?? "TB";

    /// <summary>Gets the direction of the layout: the last top-level direction statement, else <c>TB</c>.</summary>
    public string LayoutDirection => DirectionOf(_rootDocument);

    /// <summary>Gets the title the diagram declares: none, because the state grammar has no title statement.</summary>
    public string? Title => null;

    /// <summary>Gets the accessible title, or null when there is none.</summary>
    public string? AccTitle => AccessibleTitle.Length > 0 ? AccessibleTitle : null;

    /// <summary>
    ///     Gets the direction of the metadata: the direction the diagram is laid out in (<see cref="LayoutDirection"/>),
    ///     which is the last top-level direction statement, else <c>TB</c>.
    /// </summary>
    string? IMermaidDiagram.Direction => LayoutDirection;

    /// <summary>
    ///     Gets the number of states drawn as nodes: plain states, states with descriptions, start and end states, forks,
    ///     joins and choices (composite states, concurrency regions, notes and note groups are not counted).
    /// </summary>
    public int NodeCount => _nodes.Count(node => node.Kind == StateNodeKind.State && !node.IsGroup);

    /// <summary>Gets the number of transitions (the edges of notes are not counted).</summary>
    public int EdgeCount => _edges.Count(edge => !edge.IsNote);

    /// <summary>Gets the number of composite states (concurrency regions and note groups are not counted).</summary>
    public int SubgraphCount => _nodes.Count(node => node.Kind == StateNodeKind.State && node.IsGroup && node.Shape != DividerType);

    /// <summary>Gets the accessible title (empty when there is none).</summary>
    public string AccessibleTitle { get; private set; } = string.Empty;

    /// <summary>Gets the accessible description (empty when there is none).</summary>
    public string AccessibleDescription { get; private set; } = string.Empty;

    /// <summary>Gets the top-level statements after the translation (Mermaid's <c>rootDoc</c>).</summary>
    public IReadOnlyList<StateStatement> RootDocument => _rootDocument;

    /// <summary>Gets the states of the top-level document in insertion order (Mermaid's <c>getStates</c>).</summary>
    public IReadOnlyList<StateRecord> States => _states;

    /// <summary>Gets the transitions of the top-level document (Mermaid's <c>getRelations</c>).</summary>
    public IReadOnlyList<StateRelation> Relations => _relations;

    /// <summary>Gets the style classes in insertion order (Mermaid's <c>getClasses</c>).</summary>
    public IReadOnlyList<StateStyleClass> StyleClasses => _styleClasses;

    /// <summary>Gets the links of the click statements (Mermaid's <c>getLinks</c>), which are never drawn.</summary>
    public IReadOnlyList<StateLink> Links => _links;

    /// <summary>Gets the nodes of the layout in Mermaid's order (the data fetcher's <c>nodes</c>).</summary>
    public IReadOnlyList<StateNode> Nodes => _nodes;

    /// <summary>Gets the edges of the layout in Mermaid's order (the data fetcher's <c>edges</c>).</summary>
    public IReadOnlyList<StateEdge> Edges => _edges;

    /// <summary>
    ///     Gives out the id of the next concurrency divider (Mermaid's <c>getDividerId</c>: <c>divider-id-N</c>, numbered in
    ///     parse order).
    /// </summary>
    /// <returns>The id.</returns>
    public string NextDividerId()
    {
        _dividerCount++;
        return string.Create(CultureInfo.InvariantCulture, $"divider-id-{_dividerCount}");
    }

    /// <summary>
    ///     Sets the accessible title (Mermaid's <c>setAccTitle</c>: sanitized, leading white space removed).
    /// </summary>
    /// <param name="text">The title.</param>
    public void SetAccessibleTitle(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var sanitized = Sanitize(text);
        var start = 0;
        while (start < sanitized.Length && ScanPatterns.IsJsWhitespace(sanitized[start]))
        {
            start++;
        }

        AccessibleTitle = sanitized[start..];
    }

    /// <summary>
    ///     Sets the accessible description (Mermaid's <c>setAccDescription</c>: sanitized, the white space after each line
    ///     break removed).
    /// </summary>
    /// <param name="text">The description.</param>
    public void SetAccessibleDescription(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var sanitized = Sanitize(text);
        var builder = new StringBuilder(sanitized.Length);
        var i = 0;
        while (i < sanitized.Length)
        {
            builder.Append(sanitized[i]);
            i++;

            // \n\s+ becomes \n: skip the white space after a line feed (more line feeds included)
            if (sanitized[i - 1] == '\n')
            {
                while (i < sanitized.Length && ScanPatterns.IsJsWhitespace(sanitized[i]))
                {
                    i++;
                }
            }
        }

        AccessibleDescription = builder.ToString();
    }

    /// <summary>
    ///     Sets the top-level statements and applies them (Mermaid's <c>setRootDoc</c>): the translation, the database and
    ///     the data fetcher, as described on the class.
    /// </summary>
    /// <param name="statements">The top-level statements, as the parser built them.</param>
    /// <exception cref="MermaidSyntaxException">
    ///     Thrown, located, when a composite state has a description beyond its title, or when a limit is exceeded.
    /// </exception>
    public void SetRootDocument(List<StateStatement> statements)
    {
        ArgumentNullException.ThrowIfNull(statements);
        _rootDocument = statements;
        var root = new StateStatement { Stmt = RootId, Id = RootId, Doc = statements };

        // The current diagram translates before it applies the statements; the legacy one after
        if (!IsLegacy)
        {
            Translate(root, root, true);
        }

        foreach (var item in statements)
        {
            Apply(item);
        }

        if (IsLegacy)
        {
            Translate(root, root, true);
        }

        // The data fetcher reads the top-level statements, as getRootDocV2 returns them
        FetchDocument(root, _rootDocument, altFlag: false, level: 1);
        ProcessLabels();
    }

    /// <summary>
    ///     Sanitizes a text as Mermaid's <c>sanitizeText</c> does (an empty text is kept).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The sanitized text.</returns>
    internal static string Sanitize(string text) => text.Length == 0 ? text : HtmlSanitizer.Sanitize(text);

    /// <summary>
    ///     Removes a leading colon and the white space around a text (Mermaid's <c>trimColon</c>).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The trimmed text.</returns>
    internal static string TrimColon(string text) =>
        text.StartsWith(':') ? Preprocessor.TrimJs(text[1..]) : Preprocessor.TrimJs(text);

    /// <summary>
    ///     Gets the direction of a document (Mermaid's <c>getDir</c>): its last direction statement, else <c>TB</c>.
    /// </summary>
    /// <param name="document">The statements, or null.</param>
    /// <returns>The direction.</returns>
    private static string DirectionOf(List<StateStatement>? document) =>
        document?.LastOrDefault(statement => statement.Stmt == "dir")?.Value ?? "TB";

    /// <summary>
    ///     Translates a statement and its documents (Mermaid's <c>docTranslator</c>): start and end ids, trimmed ids, and
    ///     the concurrency regions of a document with dividers.
    /// </summary>
    /// <param name="parent">The statement whose document holds the statement.</param>
    /// <param name="node">The statement.</param>
    /// <param name="first">Whether the statement is the first state of a transition (or not part of one).</param>
    private void Translate(StateStatement parent, StateStatement node, bool first)
    {
        if (node.Stmt == "relation")
        {
            Translate(parent, node.State1!, true);
            Translate(parent, node.State2!, false);
            return;
        }

        if (node.Stmt == "state")
        {
            if (node.Id == "[*]")
            {
                node.Id = parent.Id + (first ? "_start" : "_end");
                node.Start = first;
                CheckLabel(node.Id, node.Location);
            }
            else
            {
                node.Id = Preprocessor.TrimJs(node.Id);
            }
        }

        if (node.Stmt is not (RootId or "state") || node.Doc is null)
        {
            return;
        }

        // A document with dividers becomes its regions: the statements before each divider, then the rest in a region
        // with a generated id
        var regions = new List<(StateStatement Divider, List<StateStatement> Statements)>();
        var current = new List<StateStatement>();
        foreach (var statement in node.Doc)
        {
            if (statement.Type == DividerType)
            {
                regions.Add((statement, current));
                current = [];
            }
            else
            {
                current.Add(statement);
            }
        }

        if (regions.Count > 0 && current.Count > 0)
        {
            foreach (var (divider, statements) in regions)
            {
                divider.Doc = statements;
            }

            _generatedCount++;
            var last = new StateStatement
            {
                Stmt = "state",
                Id = string.Create(CultureInfo.InvariantCulture, $"id-random-{_generatedCount}"),
                Type = DividerType,
                Doc = current,
                Location = node.Location,
            };
            node.Doc = [.. regions.Select(region => region.Divider), last];
        }

        foreach (var child in node.Doc)
        {
            Translate(node, child, true);
        }
    }

    /// <summary>
    ///     Applies one top-level statement to the database (one step of Mermaid's <c>extract</c>).
    /// </summary>
    /// <param name="item">The statement.</param>
    private void Apply(StateStatement item)
    {
        switch (item.Stmt)
        {
            case "state":
                // The classes of a lone id:::class are not applied, as in Mermaid
                AddState(Preprocessor.TrimJs(item.Id), item.Type ?? DefaultType, item.Doc, DescriptionsOf(item), item.Note, null, item.Location);
                break;
            case "relation":
                AddRelation(item.State1!, item.State2!, item.Description ?? string.Empty);
                break;
            case "classDef":
                AddStyleClass(Preprocessor.TrimJs(item.Id), item.Styles ?? string.Empty, item.Location);
                break;
            case "style":
                SetStyles(item);
                break;
            case "applyClass":
                SetCssClass(Preprocessor.TrimJs(item.Id), item.StyleClass ?? string.Empty, item.Location);
                break;
            case "click":
                _links.Add(new StateLink(item.Target!, item.Url ?? string.Empty, item.Tooltip ?? string.Empty));
                break;
        }
    }

    /// <summary>
    ///     Adds a state unless it exists, and applies descriptions, a note and CSS classes to it (Mermaid's
    ///     <c>addState</c>).
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="type">The type of a new state.</param>
    /// <param name="document">The document of a new state (or of a state that has none yet), or null.</param>
    /// <param name="descriptions">The descriptions to add.</param>
    /// <param name="note">The note, or null.</param>
    /// <param name="classes">The CSS classes to add, or null.</param>
    /// <param name="location">The location of the statement.</param>
    private void AddState(
        string id,
        string type,
        List<StateStatement>? document,
        IReadOnlyList<string> descriptions,
        StateNote? note,
        IReadOnlyList<string>? classes,
        MermaidLocation location)
    {
        if (!_stateIndex.TryGetValue(id, out var state))
        {
            CheckNodeCount(_states.Count, location);
            state = new StateRecord { Id = id, Type = type, Doc = document };
            _states.Add(state);
            _stateIndex[id] = state;
        }
        else
        {
            state.Doc ??= document;
        }

        foreach (var description in descriptions)
        {
            AddDescription(state, Preprocessor.TrimJs(description));
        }

        if (note is not null)
        {
            // The note is shared with the statement, so its text is sanitized there too
            state.Note = note;
            note.Text = Sanitize(note.Text);
        }

        foreach (var name in classes ?? [])
        {
            SetCssClass(id, Preprocessor.TrimJs(name), location);
        }
    }

    /// <summary>
    ///     Gets the descriptions a statement gives its state: none for an empty description, one, or the two of
    ///     <c>state "description" as id:text</c>.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <returns>The descriptions.</returns>
    private static IReadOnlyList<string> DescriptionsOf(StateStatement statement) =>
        statement.DescriptionParts ?? (string.IsNullOrEmpty(statement.Description) ? [] : [statement.Description]);

    /// <summary>
    ///     Adds a description to a state (Mermaid's <c>addDescription</c>): a leading colon is removed, and the text is
    ///     sanitized.
    /// </summary>
    /// <param name="state">The state.</param>
    /// <param name="description">The description, trimmed.</param>
    private static void AddDescription(StateRecord state, string description)
    {
        var text = description.StartsWith(':') ? Preprocessor.TrimJs(description[1..]) : description;
        state.Descriptions.Add(Sanitize(text));
    }

    /// <summary>
    ///     Adds a transition between the states of two statements (Mermaid's <c>addRelationObjs</c>); the legacy database
    ///     names each <c>[*]</c> <c>start</c> and a running number.
    /// </summary>
    /// <param name="item1">The first state.</param>
    /// <param name="item2">The second state.</param>
    /// <param name="title">The label.</param>
    private void AddRelation(StateStatement item1, StateStatement item2, string title)
    {
        var (id1, type1) = StartIfNeeded(Preprocessor.TrimJs(item1.Id), item1.Type);
        var (id2, type2) = StartIfNeeded(Preprocessor.TrimJs(item2.Id), item2.Type);
        AddState(id1, type1, item1.Doc, DescriptionsOf(item1), item1.Note, item1.Classes, item1.Location);
        AddState(id2, type2, item2.Doc, DescriptionsOf(item2), item2.Note, item2.Classes, item2.Location);
        _relations.Add(new StateRelation(id1, id2, Sanitize(title)));
    }

    /// <summary>
    ///     Gets the id and type of a state of a transition (Mermaid's <c>startIdIfNeeded</c> and <c>startTypeIfNeeded</c>):
    ///     <c>[*]</c> becomes <c>start</c> and the next number, of type <c>start</c>.
    /// </summary>
    /// <param name="id">The trimmed id.</param>
    /// <param name="type">The type of the statement, or null.</param>
    /// <returns>The id and type.</returns>
    private (string Id, string Type) StartIfNeeded(string id, string? type)
    {
        if (id != "[*]")
        {
            return (id, type ?? DefaultType);
        }

        _startEndCount++;
        return (string.Create(CultureInfo.InvariantCulture, $"start{_startEndCount}"), "start");
    }

    /// <summary>
    ///     Adds or extends a style class (Mermaid's <c>addStyleClass</c>): each comma-separated attribute loses its first
    ///     semicolon and is trimmed; an attribute with <c>color</c> also gives a text style, with <c>fill</c> as
    ///     <c>bgFill</c> and <c>color</c> as <c>fill</c>.
    /// </summary>
    /// <param name="id">The class name.</param>
    /// <param name="attributes">The attributes.</param>
    /// <param name="location">The location of the statement.</param>
    private void AddStyleClass(string id, string attributes, MermaidLocation location)
    {
        if (!_styleClassIndex.TryGetValue(id, out var styleClass))
        {
            styleClass = new StateStyleClass(id);
            _styleClasses.Add(styleClass);
            _styleClassIndex[id] = styleClass;
        }

        if (attributes.Length == 0)
        {
            return;
        }

        foreach (var attribute in attributes.Split(','))
        {
            var fixedAttribute = Preprocessor.TrimJs(ReplaceFirst(attribute, ";", string.Empty));
            if (attribute.Contains("color", StringComparison.Ordinal))
            {
                var textStyle = ReplaceFirst(ReplaceFirst(fixedAttribute, "fill", "bgFill"), "color", "fill");
                CheckLabel(textStyle, location);
                styleClass.TextStyles.Add(textStyle);
            }

            styleClass.Styles.Add(fixedAttribute);
        }
    }

    /// <summary>
    ///     Applies a <c>style</c> statement (Mermaid's <c>handleStyleDef</c>): each listed state, added when it does not
    ///     exist, gets the comma-separated styles without semicolons.
    /// </summary>
    /// <param name="item">The statement.</param>
    private void SetStyles(StateStatement item)
    {
        var styles = (item.StyleClass ?? string.Empty).Split(',');
        foreach (var id in Preprocessor.TrimJs(item.Id).Split(','))
        {
            if (!_stateIndex.TryGetValue(id, out var state))
            {
                var trimmed = Preprocessor.TrimJs(id);
                AddState(trimmed, DefaultType, null, [], null, null, item.Location);
                state = _stateIndex[trimmed];
            }

            state.Styles.Clear();
            state.Styles.AddRange(styles.Select(style => Preprocessor.TrimJs(style.Replace(";", string.Empty, StringComparison.Ordinal))));
        }
    }

    /// <summary>
    ///     Adds a CSS class to comma-separated states, adding the states that do not exist (Mermaid's <c>setCssClass</c>).
    /// </summary>
    /// <param name="ids">The ids.</param>
    /// <param name="cssClass">The class.</param>
    /// <param name="location">The location of the statement.</param>
    private void SetCssClass(string ids, string cssClass, MermaidLocation location)
    {
        foreach (var id in ids.Split(','))
        {
            if (!_stateIndex.TryGetValue(id, out var state))
            {
                var trimmed = Preprocessor.TrimJs(id);
                AddState(trimmed, DefaultType, null, [], null, null, location);
                state = _stateIndex[trimmed];
            }

            state.Classes.Add(cssClass);
        }
    }

    /// <summary>
    ///     Replaces the first occurrence of a text, as JavaScript's <c>replace</c> with a string pattern does.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="search">The text to find.</param>
    /// <param name="replacement">The replacement.</param>
    /// <returns>The text with the first occurrence replaced.</returns>
    private static string ReplaceFirst(string text, string search, string replacement)
    {
        var index = text.IndexOf(search, StringComparison.Ordinal);
        return index < 0 ? text : string.Concat(text.AsSpan(0, index), replacement, text.AsSpan(index + search.Length));
    }

    /// <summary>
    ///     Checks that one more state or node fits <see cref="MermaidLimits.MaxNodes"/>, which counts states, composite
    ///     states, concurrency regions, notes and note groups together, as the flowchart model counts vertices and
    ///     subgraphs.
    /// </summary>
    /// <param name="count">The number there already is.</param>
    /// <param name="location">The location of the statement that adds it.</param>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when it does not.</exception>
    private void CheckNodeCount(int count, MermaidLocation location)
    {
        if (count >= _limits.MaxNodes)
        {
            throw new MermaidSyntaxException(
                MermaidDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"the diagram has more than {_limits.MaxNodes} nodes and subgraphs (MermaidLimits.MaxNodes)"),
                location,
                "split the diagram, or raise MermaidLimits.MaxNodes when the source is trusted");
        }
    }

    /// <summary>
    ///     Checks a text the model joins or derives from texts the parser checked apart against
    ///     <see cref="MermaidLimits.MaxLabelLength"/>, as the flowchart model checks its labels.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="location">The location of the statement.</param>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when the text is too long.</exception>
    private void CheckLabel(string text, MermaidLocation location) => CheckLabelLength(text.Length, location);

    /// <summary>
    ///     Checks the length of a text the model joins against <see cref="MermaidLimits.MaxLabelLength"/>.
    /// </summary>
    /// <param name="length">The length of the text.</param>
    /// <param name="location">The location of the statement.</param>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when the text is too long.</exception>
    private void CheckLabelLength(long length, MermaidLocation location)
    {
        if (length > _limits.MaxLabelLength)
        {
            throw new MermaidSyntaxException(
                MermaidDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"a label is {length} characters long, more than the limit of {_limits.MaxLabelLength} (MermaidLimits.MaxLabelLength)"),
                location,
                "shorten the label, or raise MermaidLimits.MaxLabelLength when the source is trusted");
        }
    }
}

/// <summary>
///     A statement of the state grammar (Mermaid's <c>Stmt</c>): a state, a transition, a style statement, a click, a
///     direction, the root, or a statement the grammar keeps as text.
/// </summary>
/// <remarks>
///     The parser builds the statements; the model's translation changes their ids, documents and start flags, and the
///     database sanitizes the text of their notes. Once parsing is over they are never changed.
/// </remarks>
internal sealed class StateStatement
{
    /// <summary>
    ///     Gets the kind (Mermaid's <c>stmt</c>): <c>state</c>, <c>relation</c>, <c>classDef</c>, <c>style</c>,
    ///     <c>applyClass</c>, <c>click</c>, <c>dir</c> or <c>root</c>; null for a statement the grammar keeps as text
    ///     (<see cref="Text"/>), which the database ignores.
    /// </summary>
    public string? Stmt { get; init; }

    /// <summary>Gets the text of a statement the grammar keeps as text, such as <c>hide empty description</c>.</summary>
    public string? Text { get; init; }

    /// <summary>Gets or sets the id: a state id, or the ids of a style statement.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets the type of a state (<c>default</c>, <c>fork</c>, <c>join</c>, <c>choice</c> or <c>divider</c>), or null.</summary>
    public string? Type { get; init; }

    /// <summary>Gets or sets the description of a state or the label of a transition, or null.</summary>
    public string? Description { get; set; }

    /// <summary>Gets the two descriptions of <c>state "description" as id:text</c>, or null.</summary>
    public IReadOnlyList<string>? DescriptionParts { get; init; }

    /// <summary>Gets or sets the document of a composite state or region, or null.</summary>
    public List<StateStatement>? Doc { get; set; }

    /// <summary>Gets the note of a note statement, or null.</summary>
    public StateNote? Note { get; init; }

    /// <summary>Gets or sets whether a translated <c>[*]</c> is a start (true) or an end (false); null for other states.</summary>
    public bool? Start { get; set; }

    /// <summary>Gets the CSS classes of <c>id:::class</c>, or null.</summary>
    public IReadOnlyList<string>? Classes { get; init; }

    /// <summary>Gets the direction of a direction statement, or null.</summary>
    public string? Value { get; init; }

    /// <summary>Gets the styles of a <c>classDef</c> statement (Mermaid's <c>classes</c>), or null.</summary>
    public string? Styles { get; init; }

    /// <summary>Gets the styles of a <c>style</c> statement or the class of a <c>class</c> statement, or null.</summary>
    public string? StyleClass { get; init; }

    /// <summary>Gets the URL of a click statement, with its quotes, or null.</summary>
    public string? Url { get; init; }

    /// <summary>Gets the tooltip of a click statement, with its quotes, or null.</summary>
    public string? Tooltip { get; init; }

    /// <summary>Gets the first state of a transition, or null.</summary>
    public StateStatement? State1 { get; init; }

    /// <summary>Gets the second state of a transition, or null.</summary>
    public StateStatement? State2 { get; init; }

    /// <summary>Gets the state of a click statement (Mermaid's <c>id</c>, a state statement), or null.</summary>
    public StateStatement? Target { get; init; }

    /// <summary>Gets the location of the statement in the source.</summary>
    public MermaidLocation Location { get; init; }
}

/// <summary>
///     The note of a note statement: its position and text.
/// </summary>
/// <remarks>The database sanitizes the text of a top-level note; once parsing is over the note is never changed.</remarks>
internal sealed class StateNote
{
    /// <summary>Gets the position as written (<c>left of</c> or <c>right of</c>).</summary>
    public required string Position { get; init; }

    /// <summary>Gets or sets the text.</summary>
    public required string Text { get; set; }
}

/// <summary>
///     A state of the database (Mermaid's <c>StateStmt</c> in <c>getStates</c>).
/// </summary>
/// <remarks>The database changes a state while it applies the statements; once parsing is over it is never changed.</remarks>
internal sealed class StateRecord
{
    /// <summary>Gets the id.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the type.</summary>
    public required string Type { get; init; }

    /// <summary>Gets the descriptions, sanitized.</summary>
    public List<string> Descriptions { get; } = [];

    /// <summary>Gets or sets the document of a composite state, or null.</summary>
    public List<StateStatement>? Doc { get; set; }

    /// <summary>Gets or sets the note, or null.</summary>
    public StateNote? Note { get; set; }

    /// <summary>Gets the CSS classes.</summary>
    public List<string> Classes { get; } = [];

    /// <summary>Gets the styles of the <c>style</c> statements.</summary>
    public List<string> Styles { get; } = [];

    /// <summary>Gets the text styles (never set by the grammar, kept for the facts).</summary>
    public List<string> TextStyles { get; } = [];
}

/// <summary>
///     A transition of the database: its states and sanitized label (Mermaid's <c>relationTitle</c>).
/// </summary>
/// <param name="Id1">The id of the first state.</param>
/// <param name="Id2">The id of the second state.</param>
/// <param name="RelationTitle">The label.</param>
internal sealed record StateRelation(string Id1, string Id2, string RelationTitle);

/// <summary>
///     A style class of a <c>classDef</c> statement: its styles and text styles.
/// </summary>
/// <remarks>The database adds styles while it applies the statements; once parsing is over it is never changed.</remarks>
/// <param name="Id">The class name.</param>
internal sealed record StateStyleClass(string Id)
{
    /// <summary>Gets the styles.</summary>
    public List<string> Styles { get; } = [];

    /// <summary>Gets the text styles.</summary>
    public List<string> TextStyles { get; } = [];
}

/// <summary>
///     The link of a click statement, as Mermaid keeps it (keyed by the statement of the state, with the quotes of the
///     URL and tooltip); links are never drawn.
/// </summary>
/// <param name="Target">The state statement of the click.</param>
/// <param name="Url">The URL, with its quotes.</param>
/// <param name="Tooltip">The tooltip, with its quotes, or empty.</param>
internal sealed record StateLink(StateStatement Target, string Url, string Tooltip);
