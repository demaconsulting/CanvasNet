// Portions derived from PlantUML (MIT); see third-party/README.md
using System.Globalization;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;
using DemaConsulting.CanvasNet.PlantUml.Svek;
using DemaConsulting.CanvasNet.PlantUml.Syntax;

namespace DemaConsulting.CanvasNet.PlantUml.Description;

/// <summary>
///     Reads the statements of a description diagram (a use case, component, or deployment diagram) into a
///     <see cref="DescriptionModel"/>.
/// </summary>
/// <remarks>
///     <para>
///         The parser reads PlantUML's description diagram statements one by one, by their leading keywords and with the
///         shared Svek readers, without regular expressions, as PlantUML's <c>DescriptionDiagramFactory</c> does:
///         elements of every symbol (<c>component</c>, <c>interface</c>, <c>actor</c>, <c>usecase</c>, <c>node</c>,
///         <c>database</c>, ...) with display names (<c>"Long" as L</c>, <c>L as "Long"</c>), stereotypes, colors, and
///         multi-line descriptions (<c>component C [</c> ... <c>]</c>); the shorthands <c>[C]</c>, <c>() I</c>,
///         <c>(U)</c>, <c>(U)/</c>, <c>:A:</c>, and <c>:A:/</c>; containers (an element of a container symbol declared with
///         <c>{</c> ... <c>}</c>, nested); links (<c>A --&gt; B : label</c>, with every decoration, lengths, direction
///         hints, styles in brackets, and end labels); notes; page texts; the direction; <c>scale</c>; and
///         <c>skinparam</c>.
///     </para>
///     <para>
///         Names are resolved as PlantUML resolves them: a declaration names an element of the current container
///         (whose id is the container's id and the name joined with a dot, <c>Server.DB</c>); a link names an element of
///         the current container, of the containers around it, or by a name that only one element was created with,
///         and otherwise creates one in the current container, whose symbol the way it is written gives
///         (<c>[x]</c> a component, <c>() x</c> an interface, <c>(x)</c> a use case, <c>:x:</c> an actor). A name only
///         written in links becomes an actor when the diagram has a use case or an actor, and an interface otherwise.
///         Declaring an element again with another symbol, a port outside a container, a statement of another diagram
///         type, and a name that several elements share are located <see cref="PlantUmlDiagnosticCodes.SyntaxError"/>s. A
///         statement PlantUML accepts but this version does not draw (ports and their links, the middle decoration of a
///         link, roles, <c>together</c>, <c>hide</c>, <c>remove</c>, <c>archimate</c>, a symbol without its own
///         drawing, ...) is reported as <see cref="PlantUmlDiagnosticCodes.StatementUnsupported"/> and drawn without the
///         unsupported part. The elements and notes, the links, the label lengths, and the container nesting are bounded
///         by <see cref="PlantUmlLimits"/> (<see cref="PlantUmlDiagnosticCodes.LimitExceeded"/>).
///     </para>
///     <para>
///         An instance reads one diagram on one thread; <see cref="Parse"/> is thread-safe.
///     </para>
/// </remarks>
internal sealed partial class DescriptionParser
{
    /// <summary>The hint of a statement of another diagram type.</summary>
    private const string MixingHint = "PlantUML needs 'allowmixing' to mix the elements of other diagrams; draw them in a separate diagram";

    /// <summary>The statements of the diagram.</summary>
    private readonly IReadOnlyList<SourceStatement> _statements;

    /// <summary>The limits of the open call.</summary>
    private readonly PlantUmlLimits _limits;

    /// <summary>The detected type (<c>usecase</c>, <c>component</c>, or <c>deployment</c>), for the messages.</summary>
    private readonly string _type;

    /// <summary>The reports found while reading.</summary>
    private readonly List<PlantUmlPendingReport> _reports = [];

    /// <summary>The skin parameters in order.</summary>
    private readonly List<SkinParamEntry> _skin = [];

    /// <summary>The elements in order of creation.</summary>
    private readonly List<DescriptionElement> _elements = [];

    /// <summary>The elements by id.</summary>
    private readonly Dictionary<string, DescriptionElement> _byId = new(StringComparer.Ordinal);

    /// <summary>The elements by the name they were created with.</summary>
    private readonly Dictionary<string, List<DescriptionElement>> _byName = new(StringComparer.Ordinal);

    /// <summary>The notes in order.</summary>
    private readonly List<DescriptionNote> _notes = [];

    /// <summary>The floating notes by alias.</summary>
    private readonly Dictionary<string, DescriptionNote> _noteByAlias = new(StringComparer.Ordinal);

    /// <summary>The links in order.</summary>
    private readonly List<DescriptionLink> _links = [];

    /// <summary>The ids of the ports (read, not drawn).</summary>
    private readonly HashSet<string> _portIds = new(StringComparer.Ordinal);

    /// <summary>The names the ports were declared with.</summary>
    private readonly HashSet<string> _portNames = new(StringComparer.Ordinal);

    /// <summary>The open blocks (containers and <c>together</c> groups), innermost last.</summary>
    private readonly List<OpenBlock> _open = [];

    /// <summary>The page texts read so far.</summary>
    private PageTexts _page = PageTexts.None;

    /// <summary>The rank direction.</summary>
    private SvekRankDir _rankDir;

    /// <summary>The diagram's own scale.</summary>
    private double _scale = 1;

    /// <summary>The element declared or linked last (the target of a note without <c>of</c>), or null.</summary>
    private DescriptionElement? _lastElement;

    /// <summary>The link read last (the target of <c>note on link</c>), or null.</summary>
    private DescriptionLink? _lastLink;

    /// <summary>The number of notes without an alias created.</summary>
    private int _noteCount;

    /// <summary>The number of containers without a name created.</summary>
    private int _anonymousCount;

    /// <summary>
    ///     Initializes a parser.
    /// </summary>
    /// <param name="statements">The statements.</param>
    /// <param name="limits">The limits.</param>
    /// <param name="type">The detected type.</param>
    private DescriptionParser(IReadOnlyList<SourceStatement> statements, PlantUmlLimits limits, string type)
    {
        _statements = statements;
        _limits = limits;
        _type = type;
    }

    /// <summary>
    ///     Gets the container that receives the elements being read (the innermost open container), or null at the top.
    /// </summary>
    private DescriptionElement? CurrentContainer => _open.LastOrDefault(b => b.Container is not null)?.Container;

    /// <summary>
    ///     Reads a description diagram.
    /// </summary>
    /// <param name="source">The preprocessed source.</param>
    /// <param name="detected">The detected type (use case, component, or deployment).</param>
    /// <param name="limits">The limits of the open call.</param>
    /// <returns>The model.</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown, located, when a statement is not valid or a limit is exceeded.</exception>
    public static DescriptionModel Parse(PreprocessedSource source, DetectedDiagram detected, PlantUmlLimits limits)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(detected);
        ArgumentNullException.ThrowIfNull(limits);
        var parser = new DescriptionParser(source.Block.Statements, limits, detected.DiagramType);
        parser.ReadAll();
        return parser.Build(detected.Location);
    }

    /// <summary>
    ///     Reads every statement and checks that every block is closed.
    /// </summary>
    private void ReadAll()
    {
        var i = 0;
        while (i < _statements.Count)
        {
            if (_statements[i].Text.Length > 0)
            {
                i = ReadStatement(i);
            }

            i++;
        }

        if (_open.Count > 0)
        {
            var block = _open[^1];
            throw Error($"the {block.Keyword} is not closed", block.Location, "close it with '}'");
        }
    }

    /// <summary>
    ///     Builds the model: gives the names only written in links their symbol (PlantUML's <c>makeDiagramReady</c>),
    ///     reports the symbols that are drawn as rectangles, applies the skin parameters, and sorts the reports into text
    ///     order.
    /// </summary>
    /// <param name="keywordLocation">The location the diagram type was detected at.</param>
    /// <returns>The model.</returns>
    private DescriptionModel Build(PlantUmlLocation keywordLocation)
    {
        // A name only written in links is an actor in a diagram with a use case or an actor, else an interface
        var usecase = _elements.Any(e => e.Symbol is DescriptionSymbol.UseCase or DescriptionSymbol.Actor);
        foreach (var element in _elements.Where(e => e.Symbol == DescriptionSymbol.Unknown))
        {
            element.Symbol = usecase ? DescriptionSymbol.Actor : DescriptionSymbol.Interface;
        }

        foreach (var element in _elements.Where(e => !DescriptionRenderer.IsDrawn(e.Symbol, e.IsContainer)))
        {
            var what = element.Symbol == DescriptionSymbol.Archimate ? "archimate element" : SymbolKeyword(element.Symbol) + " symbol";
            Unsupported($"the {what} is not drawn yet; '{element.Id}' is drawn as a rectangle", element.Location);
        }

        var (nodeSep, rankSep) = SvekSyntax.TakeLayoutSettings(_skin, _reports);
        var theme = SkinParams.Apply(SkinTheme.Description, _skin, _reports, graph: true, description: true);
        var reports = _reports.OrderBy(r => r.Location.Line).ThenBy(r => r.Location.Column).ToList();
        return new DescriptionModel(_elements, _notes, _links, _page, theme, reports, keywordLocation)
        {
            RankDir = _rankDir,
            NodeSep = nodeSep,
            RankSep = rankSep,
            Scale = _scale,
        };
    }

    /// <summary>
    ///     Gets the keyword of a symbol, for the messages.
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    /// <returns>The keyword (<c>actor/</c> for a business actor, <c>usecase/</c> for a business use case).</returns>
    internal static string SymbolKeyword(DescriptionSymbol symbol) => symbol switch
    {
        DescriptionSymbol.BusinessActor => "actor/",
        DescriptionSymbol.UseCase => "usecase",
        DescriptionSymbol.BusinessUseCase => "usecase/",
        _ => symbol.ToString().ToLowerInvariant(),
    };

    /// <summary>
    ///     Reports a statement that is not drawn.
    /// </summary>
    /// <param name="what">What is not drawn.</param>
    /// <param name="location">The location.</param>
    private void Unsupported(string what, PlantUmlLocation location) => _reports.Add(SvekSyntax.Unsupported(what, location));

    /// <summary>
    ///     Checks a text against <see cref="PlantUmlLimits.MaxLabelLength"/>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="location">The location.</param>
    private void CheckLabel(string? text, PlantUmlLocation location) => SvekSyntax.CheckLabel(text, location, _limits);

    /// <summary>
    ///     Counts a node (an element, a note, or a port) against <see cref="PlantUmlLimits.MaxNodes"/>.
    /// </summary>
    /// <param name="location">The location of the statement.</param>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the diagram has too many.</exception>
    private void CountNode(PlantUmlLocation location)
    {
        if (_elements.Count + _notes.Count + _portIds.Count >= _limits.MaxNodes)
        {
            throw new PlantUmlSyntaxException(
                PlantUmlDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"the diagram has more than {_limits.MaxNodes} elements and notes (PlantUmlLimits.MaxNodes)"),
                location,
                "split the diagram, or raise PlantUmlLimits.MaxNodes when the source is trusted");
        }
    }

    /// <summary>
    ///     Creates a located syntax error.
    /// </summary>
    /// <param name="description">The description.</param>
    /// <param name="location">The location.</param>
    /// <param name="hint">The hint.</param>
    /// <returns>The exception.</returns>
    private static PlantUmlSyntaxException Error(string description, PlantUmlLocation location, string hint) =>
        SvekSyntax.Error(description, location, hint);

    /// <summary>
    ///     Shortens a statement for a message.
    /// </summary>
    /// <param name="text">The statement.</param>
    /// <returns>At most 60 characters.</returns>
    private static string Shorten(string text) => text.Length <= 60 ? text : string.Concat(text.AsSpan(0, 57), "...");

    /// <summary>
    ///     Joins the id of a container and a name into an id.
    /// </summary>
    /// <param name="container">The container, or null for the top.</param>
    /// <param name="name">The name.</param>
    /// <returns>The id.</returns>
    private static string Join(DescriptionElement? container, string name) => container is null ? name : container.Id + "." + name;

    /// <summary>
    ///     Finds the element a name of a link or a note identifies: in the current container, in the containers around
    ///     it, or by a name that only one element was created with.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="location">The location of the statement.</param>
    /// <returns>The element, or null when the name identifies none.</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown when several elements in different containers have the name.</exception>
    private DescriptionElement? Find(string name, PlantUmlLocation location)
    {
        var container = CurrentContainer;
        while (true)
        {
            if (_byId.TryGetValue(Join(container, name), out var found))
            {
                return found;
            }

            if (container is null)
            {
                break;
            }

            container = container.Container;
        }

        if (!_byName.TryGetValue(name, out var candidates))
        {
            return null;
        }

        if (candidates.Count > 1)
        {
            throw Error(
                $"'{name}' names {candidates.Count} elements in different containers",
                location,
                "name the element by its qualified name, for example '" + candidates[0].Id + "'");
        }

        return candidates[0];
    }

    /// <summary>
    ///     Decides whether a name of a link or a note identifies a port (read, not drawn), in the current container,
    ///     the containers around it, or by the name it was declared with.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>True when it does.</returns>
    private bool IsPort(string name)
    {
        var container = CurrentContainer;
        while (true)
        {
            if (_portIds.Contains(Join(container, name)))
            {
                return true;
            }

            if (container is null)
            {
                return _portNames.Contains(name);
            }

            container = container.Container;
        }
    }

    /// <summary>
    ///     Creates an element in the current container.
    /// </summary>
    /// <param name="name">The name (the last part of the id).</param>
    /// <param name="symbol">The symbol.</param>
    /// <param name="location">The location of the statement.</param>
    /// <returns>The element.</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the name is a note or a port, or a limit is exceeded.</exception>
    private DescriptionElement Create(string name, DescriptionSymbol symbol, PlantUmlLocation location)
    {
        var container = CurrentContainer;
        var id = Join(container, name);
        if (_noteByAlias.ContainsKey(id) || _portIds.Contains(id))
        {
            throw Error($"the name '{id}' is already used by a note or a port", location, "give the element another name");
        }

        CheckLabel(name, location);
        CountNode(location);
        var element = new DescriptionElement(id, name, symbol, location) { Container = container };
        _elements.Add(element);
        _byId[id] = element;
        if (!_byName.TryGetValue(name, out var list))
        {
            list = [];
            _byName[name] = list;
        }

        list.Add(element);
        return element;
    }

    /// <summary>
    ///     Opens a block, bounded by <see cref="PlantUmlLimits.MaxNestingDepth"/>.
    /// </summary>
    /// <param name="block">The block.</param>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the blocks nest too deeply.</exception>
    private void Open(OpenBlock block)
    {
        if (_open.Count >= _limits.MaxNestingDepth)
        {
            throw new PlantUmlSyntaxException(
                PlantUmlDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"the containers nest more than {_limits.MaxNestingDepth} deep (PlantUmlLimits.MaxNestingDepth)"),
                block.Location,
                "nest fewer containers, or raise PlantUmlLimits.MaxNestingDepth when the source is trusted");
        }

        _open.Add(block);
    }

    /// <summary>
    ///     Closes the innermost block.
    /// </summary>
    /// <param name="statement">The closing statement.</param>
    /// <exception cref="PlantUmlSyntaxException">Thrown when no block is open.</exception>
    private void Close(SourceStatement statement)
    {
        if (_open.Count == 0)
        {
            throw Error("'}' closes no container", statement.Location, "remove the '}', or open a container with 'node Name {'");
        }

        _open.RemoveAt(_open.Count - 1);
    }

    /// <summary>
    ///     Gets the id of the next note without an alias: <c>note</c>, a line feed, and a number. No name or alias holds a
    ///     line feed (a statement is one line; a name in brackets, parentheses, or colons may hold a quote), so the id
    ///     never takes the id of an element or of a floating note.
    /// </summary>
    /// <returns>The id.</returns>
    private string NextNoteId() => "note\n" + (++_noteCount).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    ///     Gets the name of the next container without a name: <c>group</c>, a line feed, and a number, which no name can
    ///     hold.
    /// </summary>
    /// <returns>The name.</returns>
    private string NextAnonymousName() => "group\n" + (++_anonymousCount).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    ///     Gets the id of the next link.
    /// </summary>
    /// <returns>The id.</returns>
    private string NextLinkId() => "link#" + (_links.Count + 1).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    ///     An open block: a container, or a <c>together</c> group (no container).
    /// </summary>
    /// <param name="Container">The container, or null for a group.</param>
    /// <param name="Location">The location of the opening statement.</param>
    /// <param name="Keyword">What the block is, for the error of an unclosed block.</param>
    private sealed record OpenBlock(DescriptionElement? Container, PlantUmlLocation Location, string Keyword);
}
