// Portions derived from PlantUML (MIT); see third-party/README.md
using System.Globalization;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;
using DemaConsulting.CanvasNet.PlantUml.Svek;
using DemaConsulting.CanvasNet.PlantUml.Syntax;

namespace DemaConsulting.CanvasNet.PlantUml.State;

/// <summary>
///     Reads the statements of a state diagram into a <see cref="StateModel"/>.
/// </summary>
/// <remarks>
///     <para>
///         The parser reads PlantUML's state diagram statements one by one, by their leading keywords and with the
///         shared Svek readers, without regular expressions, as PlantUML's <c>StateDiagramFactory</c> does: states
///         (<c>state X</c>, <c>state "Long name" as X</c>, with stereotypes, colors, and a description after a colon),
///         descriptions (<c>X : text</c>), transitions (<c>A --&gt; B : label</c>, with lengths, direction hints, and
///         styles in brackets), the start and end pseudo-states <c>[*]</c> of the current composite state, the history
///         pseudo-states <c>[H]</c>, <c>[H*]</c>, <c>X[H]</c>, and <c>X[H*]</c>, the stereotypes that make a state a
///         choice, a fork, a join, a junction, an end, or a history, composite states (<c>state X {</c> ...
///         <c>}</c>) with concurrent regions (<c>--</c> and <c>||</c>), notes, <c>hide empty description</c>, page
///         texts, the direction, <c>scale</c>, and <c>skinparam</c>.
///     </para>
///     <para>
///         Names are resolved as PlantUML resolves them: in the current composite state, then in the composite states
///         around it, then by a short name that only one state has; a new name creates a state in the current composite
///         state, whose id is the composite's id and the name joined with a dot (<c>Active.Online</c>). A transition
///         between two concurrent regions is a located <see cref="PlantUmlDiagnosticCodes.SyntaxError"/>, as PlantUML
///         refuses it. A statement PlantUML accepts but this version does not draw (entry and exit points, pins, a
///         cross or a circle at an end of a transition, a stereotype of a transition, other <c>hide</c> forms, ...)
///         is reported as <see cref="PlantUmlDiagnosticCodes.StatementUnsupported"/> and drawn without the
///         unsupported part. The elements and notes, the transitions, the label lengths, and the nesting of composite
///         states are bounded by <see cref="PlantUmlLimits"/> (<see cref="PlantUmlDiagnosticCodes.LimitExceeded"/>).
///     </para>
///     <para>
///         An instance reads one diagram on one thread; <see cref="Parse"/> is thread-safe.
///     </para>
/// </remarks>
internal sealed partial class StateParser
{
    /// <summary>The statements of the diagram.</summary>
    private readonly IReadOnlyList<SourceStatement> _statements;

    /// <summary>The limits of the open call.</summary>
    private readonly PlantUmlLimits _limits;

    /// <summary>The reports found while reading.</summary>
    private readonly List<PlantUmlPendingReport> _reports = [];

    /// <summary>The skin parameters in order.</summary>
    private readonly List<SkinParamEntry> _skin = [];

    /// <summary>The states and pseudo-states in order of creation.</summary>
    private readonly List<StateElement> _elements = [];

    /// <summary>The states and pseudo-states by id.</summary>
    private readonly Dictionary<string, StateElement> _byId = new(StringComparer.Ordinal);

    /// <summary>The states by the name they were created with.</summary>
    private readonly Dictionary<string, List<StateElement>> _byName = new(StringComparer.Ordinal);

    /// <summary>The notes in order.</summary>
    private readonly List<StateNote> _notes = [];

    /// <summary>The floating notes by alias.</summary>
    private readonly Dictionary<string, StateNote> _noteByAlias = new(StringComparer.Ordinal);

    /// <summary>The transitions in order.</summary>
    private readonly List<StateTransition> _transitions = [];

    /// <summary>The composite states whose blocks are open, innermost last.</summary>
    private readonly List<(StateElement Composite, PlantUmlLocation Location)> _open = [];

    /// <summary>The page texts read so far.</summary>
    private PageTexts _page = PageTexts.None;

    /// <summary>The rank direction.</summary>
    private SvekRankDir _rankDir;

    /// <summary>The diagram's own scale.</summary>
    private double _scale = 1;

    /// <summary>Whether states without descriptions are drawn without their line.</summary>
    private bool _hideEmptyDescription;

    /// <summary>The element declared or used last (the target of a note without <c>of</c>), or null.</summary>
    private StateElement? _lastElement;

    /// <summary>The transition read last (the target of <c>note on link</c>), or null.</summary>
    private StateTransition? _lastTransition;

    /// <summary>The number of concurrent regions created (PlantUML numbers them across the diagram).</summary>
    private int _concurrentCount;

    /// <summary>The number of notes without an alias created.</summary>
    private int _noteCount;

    /// <summary>
    ///     Initializes a parser.
    /// </summary>
    /// <param name="statements">The statements.</param>
    /// <param name="limits">The limits.</param>
    private StateParser(IReadOnlyList<SourceStatement> statements, PlantUmlLimits limits)
    {
        _statements = statements;
        _limits = limits;
    }

    /// <summary>
    ///     Gets the region that receives the elements being read (the last region of the innermost open composite
    ///     state), or null at the top of the diagram.
    /// </summary>
    private StateRegion? CurrentRegion => _open.Count == 0 ? null : _open[^1].Composite.Regions[^1];

    /// <summary>
    ///     Reads a state diagram.
    /// </summary>
    /// <param name="source">The preprocessed source.</param>
    /// <param name="detected">The detected type.</param>
    /// <param name="limits">The limits of the open call.</param>
    /// <returns>The model.</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown, located, when a statement is not valid or a limit is exceeded.</exception>
    public static StateModel Parse(PreprocessedSource source, DetectedDiagram detected, PlantUmlLimits limits)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(detected);
        ArgumentNullException.ThrowIfNull(limits);
        var parser = new StateParser(source.Block.Statements, limits);
        parser.ReadAll();
        return parser.Build(detected.Location);
    }

    /// <summary>
    ///     Reads every statement and checks that every composite state is closed.
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
            var (composite, location) = _open[^1];
            throw Error($"the composite state '{composite.Display}' is not closed", location, "close it with '}'");
        }
    }

    /// <summary>
    ///     Builds the model: checks the transitions between concurrent regions, moves the notes of composite states,
    ///     applies the skin parameters, and sorts the reports into text order.
    /// </summary>
    /// <param name="keywordLocation">The location the diagram type was detected at.</param>
    /// <returns>The model.</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown when a transition leaves a concurrent region.</exception>
    private StateModel Build(PlantUmlLocation keywordLocation)
    {
        CheckConcurrentTransitions();

        // A note on a composite state floats in the composite's region (the engine attaches notes to simple elements)
        foreach (var note in _notes.Where(n => n.Target is { IsComposite: true }))
        {
            Unsupported($"a note on the composite state '{note.Target!.Display}' is drawn as a floating note", note.Location);
            note.Region = note.Target.Region;
            note.Target = null;
        }

        var (nodeSep, rankSep) = SvekSyntax.TakeLayoutSettings(_skin, _reports);
        var theme = SkinParams.Apply(SkinTheme.Default, _skin, _reports, graph: true);
        var reports = _reports.OrderBy(r => r.Location.Line).ThenBy(r => r.Location.Column).ToList();
        return new StateModel(_elements, _notes, _transitions, _page, theme, reports, keywordLocation)
        {
            RankDir = _rankDir,
            NodeSep = nodeSep,
            RankSep = rankSep,
            Scale = _scale,
            HideEmptyDescription = _hideEmptyDescription,
        };
    }

    /// <summary>
    ///     Refuses a transition between two concurrent regions (PlantUML's <c>checkFinalError</c>), and reports a
    ///     transition that leaves the first region of a composite state with concurrent regions, which is drawn from
    ///     the composite state's border.
    /// </summary>
    /// <exception cref="PlantUmlSyntaxException">Thrown when a transition leaves a concurrent region.</exception>
    private void CheckConcurrentTransitions()
    {
        static StateRegion? ConcurrentRegionOf(StateNode node)
        {
            for (var region = node.Region; region is not null; region = region.Owner.Region)
            {
                if (region.IsConcurrent)
                {
                    return region;
                }
            }

            return null;
        }

        static IEnumerable<StateElement> ConcurrentOwners(StateNode node)
        {
            for (var region = node.Region; region is not null; region = region.Owner.Region)
            {
                if (region.Owner.Regions.Count > 1)
                {
                    yield return region.Owner;
                }
            }
        }

        static bool IsInside(StateNode node, StateElement composite)
        {
            for (var region = node.Region; region is not null; region = region.Owner.Region)
            {
                if (ReferenceEquals(region.Owner, composite))
                {
                    return true;
                }
            }

            return false;
        }

        foreach (var transition in _transitions.Where(t => !t.TouchesNote))
        {
            var (from, to) = (transition.From, transition.To);
            if (!ReferenceEquals(ConcurrentRegionOf(from), ConcurrentRegionOf(to)))
            {
                throw Error(
                    $"'{NameOf(from)}' and '{NameOf(to)}' are in different concurrent regions; a transition cannot leave a concurrent region",
                    transition.Location,
                    "link the states of one region only, or link the composite state itself");
            }

            var left = ConcurrentOwners(from).Concat(ConcurrentOwners(to))
                .FirstOrDefault(c => !ReferenceEquals(c, from) && !ReferenceEquals(c, to) && IsInside(from, c) != IsInside(to, c));
            if (left is not null)
            {
                Unsupported($"the transition from '{NameOf(from)}' to '{NameOf(to)}' leaves the concurrent state '{left.Display}'; it is drawn from its border", transition.Location);
            }
        }
    }

    /// <summary>
    ///     Gets the name of a node for a message.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The display of an element, or the id.</returns>
    private static string NameOf(StateNode node) => node is StateElement { Display.Length: > 0 } element ? element.Display : node.Id;

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
    ///     Counts a node (an element or a note) against <see cref="PlantUmlLimits.MaxNodes"/>.
    /// </summary>
    /// <param name="location">The location of the statement.</param>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the diagram has too many.</exception>
    private void CountNode(PlantUmlLocation location)
    {
        if (_elements.Count + _notes.Count >= _limits.MaxNodes)
        {
            throw new PlantUmlSyntaxException(
                PlantUmlDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"the diagram has more than {_limits.MaxNodes} states and notes (PlantUmlLimits.MaxNodes)"),
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
    ///     Joins the prefix of a region and a name into an id.
    /// </summary>
    /// <param name="region">The region, or null for the top.</param>
    /// <param name="name">The name.</param>
    /// <returns>The id.</returns>
    private static string Join(StateRegion? region, string name) => region is null ? name : region.Prefix + "." + name;

    /// <summary>
    ///     Finds the state a name identifies: in the current region, in the regions around it, or by a name that only
    ///     one state was created with.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The state, or null.</returns>
    private StateElement? Find(string name)
    {
        var region = CurrentRegion;
        while (true)
        {
            if (_byId.TryGetValue(Join(region, name), out var found))
            {
                return found;
            }

            if (region is null)
            {
                break;
            }

            region = region.Owner.Region;
        }

        return _byName.TryGetValue(name, out var candidates) && candidates.Count == 1 ? candidates[0] : null;
    }

    /// <summary>
    ///     Finds the state a name identifies, or creates it in the current region.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="location">The location of the statement.</param>
    /// <returns>The state.</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the name is a note, or a limit is exceeded.</exception>
    private StateElement Resolve(string name, PlantUmlLocation location)
    {
        if (Find(name) is { } existing)
        {
            return existing;
        }

        if (_noteByAlias.ContainsKey(name))
        {
            throw Error($"'{name}' is a note, not a state", location, "give the state another name");
        }

        return Create(Join(CurrentRegion, name), name, name, StateKind.State, location);
    }

    /// <summary>
    ///     Creates an element in the current region.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="name">The name it is found by.</param>
    /// <param name="display">The name that is drawn.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="location">The location of the statement.</param>
    /// <returns>The element.</returns>
    private StateElement Create(string id, string name, string display, StateKind kind, PlantUmlLocation location)
    {
        CheckLabel(name, location);
        CountNode(location);
        var element = new StateElement(id, display, kind, location) { Region = CurrentRegion };
        _elements.Add(element);
        _byId[id] = element;
        if (kind == StateKind.State)
        {
            if (!_byName.TryGetValue(name, out var list))
            {
                list = [];
                _byName[name] = list;
            }

            list.Add(element);
        }

        return element;
    }

    /// <summary>
    ///     Gets a pseudo-state of a region (the start, the end, a history), creating it on first use, with the id
    ///     PlantUML gives it (<c>.start.</c> at the top, <c>Active..start.Active</c> in the composite state
    ///     <c>Active</c>).
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="region">The region, or null for the top.</param>
    /// <param name="location">The location of the statement.</param>
    /// <returns>The pseudo-state.</returns>
    private StateElement Pseudo(StateKind kind, StateRegion? region, PlantUmlLocation location)
    {
        var mark = kind switch
        {
            StateKind.Start => ".start.",
            StateKind.End => ".end.",
            StateKind.History => ".historical.",
            _ => ".deephistory.",
        };
        var id = region is null ? mark : region.Prefix + "." + mark + region.ShortName;
        if (_byId.TryGetValue(id, out var existing))
        {
            return existing;
        }

        CountNode(location);
        var element = new StateElement(id, string.Empty, kind, location) { Region = region };
        _elements.Add(element);
        _byId[id] = element;
        return element;
    }

    /// <summary>
    ///     Makes a state a composite state (gives it its first region), bounded by
    ///     <see cref="PlantUmlLimits.MaxNestingDepth"/>.
    /// </summary>
    /// <param name="element">The state.</param>
    /// <param name="location">The location of the statement.</param>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the element is a pseudo-state or the composite states nest too deeply.</exception>
    private void EnsureComposite(StateElement element, PlantUmlLocation location)
    {
        if (element.IsComposite)
        {
            return;
        }

        if (element.Kind != StateKind.State)
        {
            throw Error($"'{NameOf(element)}' is a pseudo-state; it cannot hold states", location, "give the composite state another name");
        }

        if (element.Depth + 1 > _limits.MaxNestingDepth)
        {
            throw new PlantUmlSyntaxException(
                PlantUmlDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"the composite states nest more than {_limits.MaxNestingDepth} deep (PlantUmlLimits.MaxNestingDepth)"),
                location,
                "nest fewer composite states, or raise PlantUmlLimits.MaxNestingDepth when the source is trusted");
        }

        element.Regions.Add(new StateRegion(element.Id, element, 0));
    }

    /// <summary>
    ///     Gets the id of the next note without an alias.
    /// </summary>
    /// <returns>The id.</returns>
    private string NextNoteId() => "note#" + (++_noteCount).ToString(CultureInfo.InvariantCulture);
}
