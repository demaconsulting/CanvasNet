// Portions derived from mermaid (MIT); see third-party/README.md
using System.Globalization;
using DemaConsulting.CanvasNet.Mermaid.Syntax;
using static DemaConsulting.CanvasNet.Mermaid.Mindmap.MindmapTokens;

namespace DemaConsulting.CanvasNet.Mermaid.Mindmap;

/// <summary>
///     The mindmap parser: a hand-written port of Mermaid's <c>mindmap.jison</c> (its lexer and its grammar) and of the
///     calls its actions make on Mermaid's mindmap database (<c>MindmapDB</c> of <c>mindmapDb.ts</c>), producing a
///     <see cref="MindmapDiagram"/>.
/// </summary>
/// <remarks>
///     <para>
///         <b>Lexer.</b> The 37 lexer rules and the five start conditions of the generated jison lexer are ported one to one
///         (first matching rule wins, case-insensitive), with hand-written matchers in place of the regular expressions, so
///         lines split exactly as in Mermaid, quirks included: a node id runs to the first bracket, brace or line feed (a
///         <c>%%</c> or a <c>:::</c> after an id is part of it), a run of white space that holds a line feed after its first
///         character is a <c>SPACELINE</c> (so a blank line right after a statement must not be followed by a node at the
///         start of a line), a description runs over line feeds up to its closing bracket, an icon runs to its <c>)</c> over
///         line feeds, and a lone <c>{</c> or <c>}</c> outside a description is not lexed.
///     </para>
///     <para>
///         <b>Grammar.</b> A recursive-descent parser with one token of lookahead accepts the language of the LALR(1)
///         grammar: blank lines, <c>mindmap</c>, then statements (an optionally indented node, icon or class, or a blank
///         line), each ended by line feeds or the end. After each token the lookahead is checked against the actions of the
///         LALR state jison is in, and each database call is made where jison reduces (after the lookahead is lexed and
///         checked), so errors come where and in the order Mermaid reports them, with its expected tokens.
///     </para>
///     <para>
///         <b>Database.</b> As Mermaid's <c>addNode</c>: the first node is the root and its indentation the base level; any
///         other node's level is its indentation less the base level, and its parent is the last node with a smaller level,
///         else the text fails with Mermaid's "There can be only one root" message. The id and the description are
///         sanitized as <c>sanitizeText</c> does. An icon or class decorates the last node added; before any node Mermaid
///         fails with a JavaScript error, and so does the parser. <see cref="MindmapDiagram"/> then holds what
///         <c>getData</c> derives from the database: the sections, the classes, the shapes and the edges.
///     </para>
///     <para>
///         <b>Errors and limits.</b> A token the grammar does not accept is a located <see cref="MermaidSyntaxException"/>
///         with code <see cref="MermaidDiagnosticCodes.SyntaxError"/>, a hint, and as
///         <see cref="MermaidSyntaxException.Expected"/> the jison names of the tokens of the LALR state where jison reports
///         the error, in jison's order. Every node counts against <see cref="MermaidLimits.MaxNodes"/>; every node's depth
///         (its number of ancestors) is bounded by <see cref="MermaidLimits.MaxNestingDepth"/>; and every kept token text,
///         sanitized text and class list is checked against <see cref="MermaidLimits.MaxLabelLength"/>
///         (<see cref="MermaidDiagnosticCodes.LimitExceeded"/>).
///     </para>
///     <para>
///         Parsing is linear in the text. An instance parses one text on one thread.
///     </para>
/// </remarks>
internal sealed partial class MindmapParser
{
    /// <summary>The tokens that end a statement (the grammar's <c>stop</c>).</summary>
    private static readonly string[] Stops = [SpaceLine, Newline, Eof];

    /// <summary>The tokens that start a statement.</summary>
    private static readonly string[] StatementStarts = [SpaceLine, SpaceList, Icon, CssClass, NodeStart, NodeId];

    /// <summary>The tokens the state after a <c>stop</c> accepts.</summary>
    private static readonly string[] AfterStop = [.. Stops, SpaceList, Icon, CssClass, NodeStart, NodeId];

    /// <summary>The limits of the open call.</summary>
    private readonly MermaidLimits _limits;

    /// <summary>The nodes in the order they were added.</summary>
    private readonly List<MindmapNode> _nodes = [];

    /// <summary>The nodes from the root to the last node added (where the parent of the next node is).</summary>
    private readonly List<MindmapNode> _path = [];

    /// <summary>The indentation of the root (Mermaid's <c>baseLevel</c>).</summary>
    private int _baseLevel;

    /// <summary>The lexer.</summary>
    private SourceScanner _scanner = null!;

    /// <summary>The token cursor.</summary>
    private TokenCursor _cursor = null!;

    /// <summary>
    ///     Initializes a parser.
    /// </summary>
    /// <param name="limits">The limits of the open call.</param>
    private MindmapParser(MermaidLimits limits) => _limits = limits;

    /// <summary>
    ///     Gets the lookahead token.
    /// </summary>
    private ScanToken Current => _cursor.Current;

    /// <summary>
    ///     Parses a mindmap.
    /// </summary>
    /// <param name="source">The preprocessed text.</param>
    /// <param name="detected">The detected diagram (its keyword location).</param>
    /// <param name="limits">The limits of the open call.</param>
    /// <returns>The diagram.</returns>
    /// <exception cref="MermaidSyntaxException">
    ///     Thrown, located, when the text does not follow the grammar, where Mermaid's database fails, or when a limit is
    ///     exceeded.
    /// </exception>
    public static MindmapDiagram Parse(PreprocessedSource source, DetectedDiagram detected, MermaidLimits limits)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(detected);
        ArgumentNullException.ThrowIfNull(limits);
        var parser = new MindmapParser(limits);
        parser.Run(source.ParserText);
        return new MindmapDiagram(detected.Location, parser._nodes, limits);
    }

    /// <summary>
    ///     Gets the shape type of a node from its brackets, as Mermaid's <c>getType</c> does.
    /// </summary>
    /// <param name="start">The opening bracket.</param>
    /// <param name="end">The closing bracket.</param>
    /// <returns>The type.</returns>
    internal static MindmapNodeType TypeOf(string start, string end) => start switch
    {
        "[" => MindmapNodeType.Rect,
        "(" => end == ")" ? MindmapNodeType.RoundedRect : MindmapNodeType.Cloud,
        "((" => MindmapNodeType.Circle,
        ")" => MindmapNodeType.Cloud,
        "))" => MindmapNodeType.Bang,
        "{{" => MindmapNodeType.Hexagon,
        _ => MindmapNodeType.Default,
    };

    /// <summary>
    ///     Parses the whole text (<c>start</c>): the blank lines, the keyword, then the statements up to the end of the
    ///     tokens.
    /// </summary>
    /// <param name="text">The text.</param>
    private void Run(MappedText text)
    {
        _scanner = new SourceScanner(text, Rules, Conditions);
        _cursor = Lex(() => new TokenCursor(_scanner));

        // spaceLines: SPACELINE (SPACELINE | NL)*
        if (Is(SpaceLine))
        {
            Advance();
            while (Is(SpaceLine, Newline))
            {
                Advance();
            }

            CheckLookahead([SpaceLine, Newline, MindmapTokens.Mindmap], "start the diagram with the mindmap keyword");
        }
        else
        {
            CheckLookahead([SpaceLine, MindmapTokens.Mindmap], "start the diagram with the mindmap keyword");
        }

        // mindMap: MINDMAP NL? document
        Advance();
        if (Is(Newline))
        {
            Advance();
            CheckLookahead(StatementStarts);
        }
        else
        {
            CheckLookahead([SpaceLine, Newline, SpaceList, Icon, CssClass, NodeStart, NodeId]);
        }

        // document: (statement stop)+, where stop is one SPACELINE, NL or EOF followed by any NL and EOF
        while (true)
        {
            ParseStatement();
            Advance();
            while (Is(Newline, Eof))
            {
                Advance();
            }

            if (IsEnd())
            {
                return;
            }

            CheckLookahead(AfterStop);
        }
    }

    /// <summary>
    ///     Parses one statement and makes its database call; the lookahead after it is a <c>stop</c>.
    /// </summary>
    private void ParseStatement()
    {
        if (Is(SpaceLine))
        {
            Advance();
            CheckLookahead(Stops);
            return;
        }

        if (Is(Icon, CssClass))
        {
            ParseDecoration();
            return;
        }

        if (Is(NodeStart, NodeId))
        {
            ParseNode(0);
            return;
        }

        // SPACELIST, alone or before a node or a decoration
        var indent = Advance();
        if (Is(Icon, CssClass))
        {
            ParseDecoration();
        }
        else if (Is(NodeStart, NodeId))
        {
            ParseNode(indent.Text.Length);
        }
        else
        {
            CheckLookahead([.. Stops, Icon, CssClass, NodeStart, NodeId]);
        }
    }

    /// <summary>
    ///     Parses an icon or a class decoration and makes its <c>decorateNode</c> call.
    /// </summary>
    private void ParseDecoration()
    {
        var token = Advance();
        var location = Locate(token);
        var text = TextOf(token);
        CheckLookahead(Stops);
        var isIcon = token.Kind == Icon;
        if (_nodes.Count == 0)
        {
            // decorateNode reads the last node, which does not exist, and fails with a TypeError
            throw new MermaidSyntaxException(
                MermaidDiagnosticCodes.SyntaxError,
                $"an {(isIcon ? "icon" : "class")} must follow a node (Mermaid fails here with \"Cannot set properties of undefined (setting '{(isIcon ? "icon" : "class")}')\")",
                location,
                "write the root node before the first ::icon(...) or ::: line; a decoration applies to the node above it");
        }

        var sanitized = Checked(MindmapDiagram.Sanitize(text), location);
        var node = _nodes[^1];
        if (isIcon)
        {
            node.Icon = sanitized;
            node.IconLocation = location;
        }
        else
        {
            node.Class = sanitized;
        }
    }

    /// <summary>
    ///     Parses a node (<c>nodeWithId</c> or <c>nodeWithoutId</c>) and makes its <c>addNode</c> call.
    /// </summary>
    /// <param name="indent">The indentation of the node (the length of its <c>SPACELIST</c>, zero without one).</param>
    private void ParseNode(int indent)
    {
        var first = Advance();
        var location = Locate(first);
        string id;
        string description;
        var type = MindmapNodeType.Default;
        if (first.Kind == NodeId && !Is(NodeStart))
        {
            // NODE_ID alone: the id is the description
            CheckLookahead([.. Stops, NodeStart]);
            id = TextOf(first);
            description = id;
        }
        else
        {
            var start = first.Kind == NodeStart ? first : Advance();
            var descr = Expect(NodeDescription);
            var end = Expect(NodeEnd);
            CheckLookahead(Stops);
            description = TextOf(descr);
            id = first.Kind == NodeId ? TextOf(first) : description;
            type = TypeOf(start.Text, end.Text);
        }

        AddNode(indent, id, description, type, location);
    }

    /// <summary>
    ///     Adds a node as Mermaid's <c>addNode</c> does: its level, its sanitized id and description, and its parent.
    /// </summary>
    /// <param name="level">The indentation.</param>
    /// <param name="id">The id as written.</param>
    /// <param name="description">The description as written.</param>
    /// <param name="type">The shape type.</param>
    /// <param name="location">The location of the node.</param>
    private void AddNode(int level, string id, string description, MindmapNodeType type, MermaidLocation location)
    {
        if (_nodes.Count >= _limits.MaxNodes)
        {
            throw new MermaidSyntaxException(
                MermaidDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"the diagram has more than {_limits.MaxNodes} nodes (MermaidLimits.MaxNodes)"),
                location,
                "split the mindmap, or raise MermaidLimits.MaxNodes when the source is trusted");
        }

        // The first node is the root at level 0; the levels of the others are relative to its indentation
        var isRoot = _nodes.Count == 0;
        if (isRoot)
        {
            _baseLevel = level;
        }

        var node = new MindmapNode(
            _nodes.Count,
            Checked(MindmapDiagram.Sanitize(id), location),
            isRoot ? 0 : level - _baseLevel,
            Checked(MindmapDiagram.Sanitize(description), location),
            type,
            location);

        // getParent: the last node with a smaller level, which is on the path from the root to the last node
        while (_path.Count > 0 && _path[^1].Level >= node.Level)
        {
            _path.RemoveAt(_path.Count - 1);
        }

        if (_path.Count > 0)
        {
            var parent = _path[^1];
            node.Parent = parent;
            node.Depth = parent.Depth + 1;
            if (node.Depth > _limits.MaxNestingDepth)
            {
                throw new MermaidSyntaxException(
                    MermaidDiagnosticCodes.LimitExceeded,
                    string.Create(CultureInfo.InvariantCulture, $"the node is nested more than {_limits.MaxNestingDepth} levels deep (MermaidLimits.MaxNestingDepth)"),
                    location,
                    "flatten the mindmap, or raise MermaidLimits.MaxNestingDepth when the source is trusted");
            }

            parent.Children.Add(node);
        }
        else if (!isRoot)
        {
            throw new MermaidSyntaxException(
                MermaidDiagnosticCodes.SyntaxError,
                $"There can be only one root. No parent could be found for (\"{node.Description}\")",
                location,
                "indent every node after the first more than the root: the root is the only node at its indentation or less");
        }

        _nodes.Add(node);
        _path.Add(node);
    }

    /// <summary>
    ///     Checks a text against <see cref="MermaidLimits.MaxLabelLength"/>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="location">The location of the text.</param>
    /// <returns>The text.</returns>
    private string Checked(string text, MermaidLocation location)
    {
        MindmapDiagram.CheckLength(text.Length, _limits, location);
        return text;
    }

    /// <summary>
    ///     Checks the lookahead against the actions of the LALR state the parser is in, and fails with them otherwise, as
    ///     jison lists them.
    /// </summary>
    /// <param name="accepted">The tokens the state accepts.</param>
    /// <param name="hint">The suggested fix, or null for a hint from the context.</param>
    private void CheckLookahead(string[] accepted, string? hint = null)
    {
        if (!Is(accepted))
        {
            throw Fail(accepted, hint);
        }
    }

    /// <summary>
    ///     Gets the text of a token that is kept, and checks <see cref="MermaidLimits.MaxLabelLength"/>.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The text.</returns>
    private string TextOf(ScanToken token) => Checked(token.Text, Locate(token));

    /// <summary>
    ///     Gets the location of a token in the original text.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The location.</returns>
    private MermaidLocation Locate(ScanToken token) => _scanner.Locate(token.Start);

    /// <summary>
    ///     Gets a value indicating whether the token stream has ended (jison's <c>$end</c>).
    /// </summary>
    /// <returns>Whether it has.</returns>
    private bool IsEnd() => Current.Kind == SourceScanner.EndOfInput;

    /// <summary>
    ///     Gets a value indicating whether the lookahead is of one of the kinds.
    /// </summary>
    /// <param name="kinds">The kinds.</param>
    /// <returns>Whether it is.</returns>
    private bool Is(params string[] kinds) => !IsEnd() && Array.IndexOf(kinds, Current.Kind) >= 0;

    /// <summary>
    ///     Consumes the lookahead token.
    /// </summary>
    /// <returns>The token.</returns>
    private ScanToken Advance() => Lex(_cursor.Advance);

    /// <summary>
    ///     Consumes the lookahead token when it is of the kind, and fails otherwise.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The token.</returns>
    private ScanToken Expect(string kind) => Is(kind) ? Advance() : throw Fail([kind]);

    /// <summary>
    ///     Runs a step that lexes a token, and gives a lexical error a hint for mindmaps.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="step">The step.</param>
    /// <returns>The result of the step.</returns>
    private T Lex<T>(Func<T> step)
    {
        try
        {
            return step();
        }
        catch (MermaidSyntaxException ex) when (ex.Code == MermaidDiagnosticCodes.SyntaxError && ex.Expected.Count == 0)
        {
            // The scanner stops at text no rule of the active condition matches
            throw new MermaidSyntaxException(
                ex.Code,
                $"unrecognized text {SourceScanner.Describe(_scanner.Input.Text, _scanner.Position)}",
                ex.Location,
                "a single { or } is only allowed inside a node's brackets, and a markdown string \"`...`\" cannot hold a quote or a backtick");
        }
    }

    /// <summary>
    ///     Creates the exception for a lookahead token the grammar does not accept.
    /// </summary>
    /// <param name="expectedKinds">The tokens jison lists in the state of the error.</param>
    /// <param name="hint">The suggested fix, or null for a hint from the context.</param>
    /// <returns>The located exception.</returns>
    private MermaidSyntaxException Fail(IEnumerable<string> expectedKinds, string? hint = null)
    {
        var expected = expectedKinds
            .Distinct(StringComparer.Ordinal)
            .OrderBy(MindmapTokens.Number)
            .Select(JisonName)
            .ToList();
        var token = Current;
        var found = IsEnd() ? "the end of the text" : $"'{JisonName(token.Kind)}' ({Describe(token.Text)})";
        return new MermaidSyntaxException(
            MermaidDiagnosticCodes.SyntaxError,
            $"expected {TokenCursor.JoinAlternatives([.. expected.Select(e => $"'{e}'")])}, found {found}",
            Locate(token),
            hint ?? HintFor(expected),
            expected);
    }

    /// <summary>
    ///     Describes the text of a token for a message.
    /// </summary>
    /// <param name="text">The token text.</param>
    /// <returns>The quoted text, shortened and with line breaks shown.</returns>
    private static string Describe(string text)
    {
        var shown = text.Length > 24 ? text[..24] + "..." : text;
        return shown.Length == 0 ? "no text" : "'" + shown.Replace("\n", "\\n", StringComparison.Ordinal) + "'";
    }

    /// <summary>
    ///     Suggests a fix for an unexpected token from what was expected.
    /// </summary>
    /// <param name="expected">The jison names of the expected tokens.</param>
    /// <returns>The hint.</returns>
    private static string HintFor(List<string> expected)
    {
        if (expected.Count == 1 && expected[0] == NodeEnd)
        {
            return "close the node's description with the bracket that matches its opening, such as id[text] or id((text))";
        }

        if (expected.Count == 1 && expected[0] == NodeDescription)
        {
            return "give the node a description between its brackets, such as id[text]";
        }

        return "write one node per line, indented under its parent, with an ::icon(...) or ::: class line after the node it decorates";
    }
}

/// <summary>
///     The shape type of a mindmap node (the <c>nodeType</c> values of Mermaid's mindmap database).
/// </summary>
internal enum MindmapNodeType
{
    /// <summary>No brackets: the default node (<c>defaultMindmapNode</c>).</summary>
    Default = 0,

    /// <summary><c>(text)</c>: the rounded rectangle.</summary>
    RoundedRect = 1,

    /// <summary><c>[text]</c>: the rectangle.</summary>
    Rect = 2,

    /// <summary><c>((text))</c>: the circle.</summary>
    Circle = 3,

    /// <summary><c>)text(</c>: the cloud.</summary>
    Cloud = 4,

    /// <summary><c>))text((</c>: the bang.</summary>
    Bang = 5,

    /// <summary><c>{{text}}</c>: the hexagon.</summary>
    Hexagon = 6,
}

/// <summary>
///     A node of a mindmap: what Mermaid's mindmap database holds for it, and where it is.
/// </summary>
/// <remarks>
///     The parser fills the decorations, the parent and the children while it reads the text; the node is never changed
///     after parsing.
/// </remarks>
/// <param name="id">The number of the node, in the order the nodes were added (Mermaid's <c>id</c>).</param>
/// <param name="nodeId">The id as written, sanitized (Mermaid's <c>nodeId</c>).</param>
/// <param name="level">The indentation less the root's (zero for the root).</param>
/// <param name="description">The description, sanitized (Mermaid's <c>descr</c>, the label).</param>
/// <param name="type">The shape type.</param>
/// <param name="location">The location of the node.</param>
internal sealed class MindmapNode(int id, string nodeId, int level, string description, MindmapNodeType type, MermaidLocation location)
{
    /// <summary>Gets the number of the node, in the order the nodes were added (Mermaid's <c>id</c>).</summary>
    public int Id { get; } = id;

    /// <summary>Gets the id as written, sanitized (Mermaid's <c>nodeId</c>).</summary>
    public string NodeId { get; } = nodeId;

    /// <summary>Gets the indentation less the root's: zero for the root, positive for every other node.</summary>
    public int Level { get; } = level;

    /// <summary>Gets the description, sanitized (Mermaid's <c>descr</c>, the label).</summary>
    public string Description { get; } = description;

    /// <summary>Gets the shape type.</summary>
    public MindmapNodeType Type { get; } = type;

    /// <summary>Gets the location of the node.</summary>
    public MermaidLocation Location { get; } = location;

    /// <summary>Gets the sanitized icon of the last <c>::icon(...)</c> after the node, or null.</summary>
    public string? Icon { get; internal set; }

    /// <summary>Gets the location of the icon, or the unknown location when there is none.</summary>
    public MermaidLocation IconLocation { get; internal set; }

    /// <summary>Gets the sanitized class text of the last <c>:::</c> after the node, or null.</summary>
    public string? Class { get; internal set; }

    /// <summary>Gets the parent, or null for the root.</summary>
    public MindmapNode? Parent { get; internal set; }

    /// <summary>Gets the number of ancestors.</summary>
    public int Depth { get; internal set; }

    /// <summary>Gets the children in the order they were added.</summary>
    public List<MindmapNode> Children { get; } = [];

    /// <summary>Gets a value indicating whether the node is the root.</summary>
    public bool IsRoot => Parent is null;

    /// <summary>
    ///     Gets the section of the node, as Mermaid's <c>assignSections</c> gives it: none for the root, the position of the
    ///     root's child modulo 11 for that child, and the section of the parent for every deeper node.
    /// </summary>
    public int? Section { get; internal set; }

    /// <summary>Gets the shape Mermaid's <c>getData</c> gives the node (<c>getShapeFromType</c>).</summary>
    public string Shape => Type switch
    {
        MindmapNodeType.Circle => "mindmapCircle",
        MindmapNodeType.Rect => "rect",
        MindmapNodeType.RoundedRect => "rounded",
        MindmapNodeType.Cloud => "cloud",
        MindmapNodeType.Bang => "bang",
        MindmapNodeType.Hexagon => "hexagon",
        _ => "defaultMindmapNode",
    };

    /// <summary>Gets the classes Mermaid's <c>getData</c> gives the node (<c>flattenNodes</c>), joined with spaces.</summary>
    public string CssClasses { get; internal set; } = string.Empty;
}

/// <summary>
///     An edge of a mindmap, from a parent to a child, as Mermaid's <c>generateEdges</c> makes it.
/// </summary>
/// <param name="Id">The id (<c>edge_parent_child</c>).</param>
/// <param name="Start">The parent.</param>
/// <param name="End">The child.</param>
/// <param name="Classes">The classes: <c>edge</c>, the child's <c>section-edge-k</c> and <c>edge-depth-n</c>, where n is the parent's level plus one.</param>
internal sealed record MindmapEdge(string Id, MindmapNode Start, MindmapNode End, string Classes);

/// <summary>
///     A parsed mindmap: the nodes of Mermaid's mindmap database with what its <c>getData</c> derives from them (sections,
///     classes, shapes and edges) and the metadata of the document.
/// </summary>
/// <remarks>
///     The nodes are in the order they were added, which is the pre-order of the tree that Mermaid's <c>flattenNodes</c>
///     walks (a new node's parent is always on the path from the root to the node before it), and the edges are in the
///     order of <c>generateEdges</c>: the edge to each node but the root, in the same order. The diagram is never changed
///     after parsing and is safe for concurrent use.
/// </remarks>
internal sealed partial class MindmapDiagram
{
    /// <summary>The number of sections the children of the root cycle through (Mermaid's <c>MAX_SECTIONS - 1</c>).</summary>
    internal const int SectionCount = 11;

    /// <summary>
    ///     Initializes a diagram and derives the sections, the classes and the edges.
    /// </summary>
    /// <param name="keywordLocation">The location of the diagram keyword.</param>
    /// <param name="nodes">The nodes in the order they were added.</param>
    /// <param name="limits">The limits of the open call (the class lists are checked against <see cref="MermaidLimits.MaxLabelLength"/>).</param>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when a class list is longer than the limit.</exception>
    public MindmapDiagram(MermaidLocation keywordLocation, IReadOnlyList<MindmapNode> nodes, MermaidLimits limits)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(limits);
        KeywordLocation = keywordLocation;
        Nodes = nodes;
        var edges = new List<MindmapEdge>(Math.Max(0, nodes.Count - 1));
        foreach (var node in nodes)
        {
            // assignSections: the root has none, its children cycle through the sections, deeper nodes inherit
            if (node.Parent is { } parent)
            {
                node.Section = parent.IsRoot ? parent.Children.IndexOf(node) % SectionCount : parent.Section;
            }

            // flattenNodes: the classes
            var classes = "mindmap-node" + (node.IsRoot ? " section-root section--1" : string.Create(CultureInfo.InvariantCulture, $" section-{node.Section}"));
            if (!string.IsNullOrEmpty(node.Class))
            {
                classes += " " + node.Class;
            }

            CheckLength(classes.Length, limits, node.Location);
            node.CssClasses = classes;

            // generateEdges: the edge from the parent, with the depth of the parent's level plus one
            if (node.Parent is { } from)
            {
                edges.Add(new MindmapEdge(
                    string.Create(CultureInfo.InvariantCulture, $"edge_{from.Id}_{node.Id}"),
                    from,
                    node,
                    string.Create(CultureInfo.InvariantCulture, $"edge section-edge-{node.Section} edge-depth-{from.Level + 1}")));
            }
        }

        Edges = edges;
    }

    /// <summary>Gets the location of the diagram keyword.</summary>
    public MermaidLocation KeywordLocation { get; }

    /// <summary>Gets the nodes in the order they were added (the pre-order of the tree); the first is the root.</summary>
    public IReadOnlyList<MindmapNode> Nodes { get; }

    /// <summary>Gets the edges in the order of Mermaid's <c>generateEdges</c>.</summary>
    public IReadOnlyList<MindmapEdge> Edges { get; }

    /// <summary>Gets the root, or null for an empty mindmap.</summary>
    public MindmapNode? Root => Nodes.Count > 0 ? Nodes[0] : null;


    /// <summary>Gets the number of nodes.</summary>
    public int NodeCount => Nodes.Count;

    /// <summary>Gets the number of edges (one to each node but the root).</summary>
    public int EdgeCount => Edges.Count;


    /// <summary>
    ///     Sanitizes a text as Mermaid's <c>sanitizeText</c> does (an empty text is kept).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The sanitized text.</returns>
    internal static string Sanitize(string text) => text.Length == 0 ? text : HtmlSanitizer.Sanitize(text);

    /// <summary>
    ///     Checks the length of a text against <see cref="MermaidLimits.MaxLabelLength"/>.
    /// </summary>
    /// <param name="length">The length.</param>
    /// <param name="limits">The limits.</param>
    /// <param name="location">The location of the text.</param>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when the text is too long.</exception>
    internal static void CheckLength(int length, MermaidLimits limits, MermaidLocation location)
    {
        if (length > limits.MaxLabelLength)
        {
            throw new MermaidSyntaxException(
                MermaidDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"a text is {length} characters long, more than the limit of {limits.MaxLabelLength} (MermaidLimits.MaxLabelLength)"),
                location,
                "shorten the text, or raise MermaidLimits.MaxLabelLength when the source is trusted");
        }
    }
}
