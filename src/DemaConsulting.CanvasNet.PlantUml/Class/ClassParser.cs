// Portions derived from PlantUML (MIT); see third-party/README.md
using System.Globalization;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;
using DemaConsulting.CanvasNet.PlantUml.Svek;
using DemaConsulting.CanvasNet.PlantUml.Syntax;

namespace DemaConsulting.CanvasNet.PlantUml.Class;

/// <summary>
///     Reads the statements of a class or object diagram into a <see cref="ClassModel"/>.
/// </summary>
/// <remarks>
///     <para>
///         The parser reads PlantUML's class diagram statements one by one, by their leading keywords and with the
///         shared Svek readers, without regular expressions, as PlantUML's <c>ClassDiagramFactory</c> does: classes and
///         their kin (<c>class</c>, <c>abstract</c>, <c>interface</c>, <c>enum</c>, <c>annotation</c>, <c>entity</c>,
///         <c>exception</c>, <c>struct</c>, <c>protocol</c>, <c>metaclass</c>, <c>stereotype</c>, <c>record</c>,
///         <c>dataclass</c>) with display names, generic parameters, stereotypes, colors, <c>extends</c> and
///         <c>implements</c>, and bodies; objects and maps; members added with <c>Name : text</c>; relationships;
///         packages and namespaces (nested, dotted names making implicit packages, stereotypes choosing their
///         outline); notes; <c>hide</c> and <c>show</c>; page texts; the direction; <c>scale</c>; and
///         <c>skinparam</c>.
///     </para>
///     <para>
///         A statement PlantUML accepts but this version does not draw (a role or a qualifier, a crow's foot, an
///         association class, <c>together</c>, <c>remove</c>, hiding single elements, ...) is reported as
///         <see cref="PlantUmlDiagnosticCodes.StatementUnsupported"/> and drawn without the unsupported part. An element
///         of a description diagram (an actor, a use case, a component, ...) is a located
///         <see cref="PlantUmlDiagnosticCodes.SyntaxError"/>, as PlantUML refuses to mix them without <c>allowmixing</c>.
///         The entities and notes, the links, the label lengths, and the package nesting are bounded by
///         <see cref="PlantUmlLimits"/> (<see cref="PlantUmlDiagnosticCodes.LimitExceeded"/>).
///     </para>
///     <para>
///         An instance reads one diagram on one thread; <see cref="Parse"/> is thread-safe.
///     </para>
/// </remarks>
internal sealed partial class ClassParser
{
    /// <summary>The hint of a statement that mixes description elements into a class diagram.</summary>
    private const string MixingHint = "PlantUML needs 'allowmixing' to mix classes and other elements; draw them in a separate diagram";

    /// <summary>The keywords of the elements of the other graph diagrams, which a class diagram refuses.</summary>
    private static readonly HashSet<string> MixingKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "actor", "usecase", "component", "node", "artifact", "cloud", "folder", "frame", "storage", "rectangle",
        "card", "file", "stack", "agent", "hexagon", "person", "boundary", "control", "database", "collections",
        "queue", "label", "port", "portin", "portout", "action", "process", "state", "participant",
    };

    /// <summary>The statements of the diagram.</summary>
    private readonly IReadOnlyList<SourceStatement> _statements;

    /// <summary>The limits of the open call.</summary>
    private readonly PlantUmlLimits _limits;

    /// <summary>The reports found while reading.</summary>
    private readonly List<PlantUmlPendingReport> _reports = [];

    /// <summary>The skin parameters in order.</summary>
    private readonly List<SkinParamEntry> _skin = [];

    /// <summary>The entities in order of declaration.</summary>
    private readonly List<ClassEntity> _entities = [];

    /// <summary>The entities by id.</summary>
    private readonly Dictionary<string, ClassEntity> _entityById = new(StringComparer.Ordinal);

    /// <summary>The entities by short name (the last segment of their id).</summary>
    private readonly Dictionary<string, List<ClassEntity>> _entityByShortName = new(StringComparer.Ordinal);

    /// <summary>The packages in order of declaration.</summary>
    private readonly List<ClassPackage> _packages = [];

    /// <summary>The packages by id.</summary>
    private readonly Dictionary<string, ClassPackage> _packageById = new(StringComparer.Ordinal);

    /// <summary>The notes in order.</summary>
    private readonly List<ClassNote> _notes = [];

    /// <summary>The floating notes by alias.</summary>
    private readonly Dictionary<string, ClassNote> _noteByAlias = new(StringComparer.Ordinal);

    /// <summary>The links in order.</summary>
    private readonly List<ClassLink> _links = [];

    /// <summary>The hide and show rules in order.</summary>
    private readonly List<ClassHideRule> _rules = [];

    /// <summary>The open blocks (packages, namespaces, and <c>together</c> groups), innermost last.</summary>
    private readonly List<OpenBlock> _open = [];

    /// <summary>The page texts read so far.</summary>
    private PageTexts _page = PageTexts.None;

    /// <summary>The rank direction.</summary>
    private SvekRankDir _rankDir;

    /// <summary>The diagram's own scale.</summary>
    private double _scale = 1;

    /// <summary>The separator of qualified names (<c>set namespaceSeparator</c>), or null for none.</summary>
    private string? _separator = ".";

    /// <summary>The entity declared last (the target of a note without <c>of</c>), or null.</summary>
    private ClassEntity? _lastEntity;

    /// <summary>The link read last (the target of <c>note on link</c>), or null.</summary>
    private ClassLink? _lastLink;

    /// <summary>The number of links created.</summary>
    private int _linkCount;

    /// <summary>The number of notes without an alias created.</summary>
    private int _noteCount;

    /// <summary>
    ///     Initializes a parser.
    /// </summary>
    /// <param name="statements">The statements.</param>
    /// <param name="limits">The limits.</param>
    private ClassParser(IReadOnlyList<SourceStatement> statements, PlantUmlLimits limits)
    {
        _statements = statements;
        _limits = limits;
    }

    /// <summary>
    ///     Gets the package that receives the elements being read (the innermost open package), or null at the top.
    /// </summary>
    private ClassPackage? CurrentPackage => _open.LastOrDefault(b => b.Package is not null)?.Package;

    /// <summary>
    ///     Reads a class or object diagram.
    /// </summary>
    /// <param name="source">The preprocessed source.</param>
    /// <param name="detected">The detected type (class or object).</param>
    /// <param name="limits">The limits of the open call.</param>
    /// <returns>The model.</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown, located, when a statement is not valid or a limit is exceeded.</exception>
    public static ClassModel Parse(PreprocessedSource source, DetectedDiagram detected, PlantUmlLimits limits)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(detected);
        ArgumentNullException.ThrowIfNull(limits);
        var parser = new ClassParser(source.Block.Statements, limits);
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
    ///     Builds the model: applies the skin parameters and sorts the reports into text order.
    /// </summary>
    /// <param name="keywordLocation">The location the diagram type was detected at.</param>
    /// <returns>The model.</returns>
    private ClassModel Build(PlantUmlLocation keywordLocation)
    {
        var (nodeSep, rankSep) = SvekSyntax.TakeLayoutSettings(_skin, _reports);
        var theme = SkinParams.Apply(SkinTheme.Default, _skin, _reports, graph: true);
        var reports = _reports.OrderBy(r => r.Location.Line).ThenBy(r => r.Location.Column).ToList();
        return new ClassModel(_entities, _packages, _notes, _links, _rules, _page, theme, reports, keywordLocation)
        {
            RankDir = _rankDir,
            NodeSep = nodeSep,
            RankSep = rankSep,
            Scale = _scale,
        };
    }

    /// <summary>
    ///     Reads one statement (and the lines of its block).
    /// </summary>
    /// <param name="index">The index of the statement.</param>
    /// <returns>The index of the last statement read.</returns>
    private int ReadStatement(int index)
    {
        var statement = _statements[index];
        var text = statement.Text;
        if (SkinParams.TryRead(_statements, ref index, _skin) || SkinParams.TrySkipStyle(_statements, ref index, _reports))
        {
            return index;
        }

        if (text == "}")
        {
            Close(statement);
            return index;
        }

        if (SvekSyntax.TryReadPageText(_statements, ref index, ref _page, _limits))
        {
            return index;
        }

        if (SvekSyntax.TryReadDirection(text, out var direction))
        {
            _rankDir = direction;
            return index;
        }

        var word = SvekSyntax.FirstWord(text);
        if (ReadKeywordStatement(index, word) is { } last)
        {
            return last;
        }

        // A relationship, then a member added to an entity
        if (SvekSyntax.TryReadLink(statement, out var link))
        {
            ReadLink(statement, link);
            return index;
        }

        if (TryReadMemberStatement(statement))
        {
            return index;
        }

        if (text.StartsWith('(') && text.Contains(',', StringComparison.Ordinal))
        {
            Unsupported("association classes ('(A, B) .. C') are not drawn", statement.Location);
            return index;
        }

        if (MixingKeywords.Contains(word))
        {
            throw Error($"'{word}' is not an element of a class diagram", statement.Location, MixingHint);
        }

        throw Error(
            $"'{Shorten(text)}' is not a class diagram statement",
            statement.Location,
            "declare a class with 'class Name', a member with 'Name : member', and a relationship with 'A <|-- B'");
    }

    /// <summary>
    ///     Reads a statement that starts with a keyword of the class diagram.
    /// </summary>
    /// <param name="index">The index of the statement.</param>
    /// <param name="word">The first word, in lower case.</param>
    /// <returns>The index of the last statement read, or null when the statement does not start with such a keyword.</returns>
    private int? ReadKeywordStatement(int index, string word)
    {
        var statement = _statements[index];
        var text = statement.Text;
        switch (word)
        {
            case "class" or "abstract" or "interface" or "enum" or "annotation" or "entity" or "exception" or "struct" or "protocol"
                or "metaclass" or "stereotype" or "record" or "dataclass" or "static" or "circle" or "diamond"
                when IsDeclaration(text, word):
                return ReadDeclaration(index, 0);
            case "object" when IsDeclaration(text, word):
                return ReadObject(index);
            case "map" when IsDeclaration(text, word):
                return ReadMap(index);
            case "package" or "namespace" when IsDeclaration(text, word):
                ReadPackage(statement, word);
                return index;
            case "note":
                return ReadNote(index);
            case "hide" or "show" when SvekSyntax.TryReadHideShow(statement, out var hideShow):
                ReadHideShow(statement, hideShow);
                return index;
            case "scale":
                if (SvekSyntax.ReadScale(text) is { } scale)
                {
                    _scale = scale;
                }
                else
                {
                    Unsupported("this 'scale' form is not drawn; the diagram is drawn at its natural size", statement.Location);
                }

                return index;
            case "together" when text.TrimEnd().EndsWith('{'):
                Unsupported("'together' groups are not drawn; their members are drawn", statement.Location);
                Open(new OpenBlock(null, statement.Location, "together group"));
                return index;
            case "set":
                ReadSet(statement);
                return index;
            case "json":
                return SkipJson(index);
            case "remove" or "restore":
                Unsupported($"'{word}' is not drawn; every element is drawn", statement.Location);
                return index;
            case "allowmixing" or "allow_mixing":
                Unsupported("'allowmixing' is not drawn; a class diagram draws only classes, objects, and maps", statement.Location);
                return index;
            case "skin":
                Unsupported("'skin' is not drawn; the default look is drawn", statement.Location);
                return index;
            case "url" or "newpage" or "mainframe":
                Unsupported($"'{word}' is not drawn", statement.Location);
                return index;
        }

        // A declaration after a visibility character (+class A): the visibility is not drawn
        if (text.Length > 1 && text[0] is '+' or '-' or '#' or '~' && SvekSyntax.FirstWord(text[1..]) is { } after && IsClassKeyword(after) && IsDeclaration(text[1..], after))
        {
            Unsupported("the visibility of a class is not drawn", statement.Location);
            return ReadDeclaration(index, 1);
        }

        return null;
    }

    /// <summary>
    ///     Reads a <c>set</c> statement: <c>set namespaceSeparator X</c> (or <c>none</c>) changes how qualified names are
    ///     split into packages; anything else is reported.
    /// </summary>
    /// <param name="statement">The statement.</param>
    private void ReadSet(SourceStatement statement)
    {
        var parts = statement.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 3 && parts[1].ToLowerInvariant() is "namespaceseparator" or "separator")
        {
            _separator = parts[2].Equals("none", StringComparison.OrdinalIgnoreCase) ? null : parts[2];
            return;
        }

        Unsupported($"'{Shorten(statement.Text)}' is not drawn", statement.Location);
    }

    /// <summary>
    ///     Skips a <c>json</c> block, counting its braces, and reports it.
    /// </summary>
    /// <param name="index">The index of the statement.</param>
    /// <returns>The index of the last statement of the block.</returns>
    private int SkipJson(int index)
    {
        var statement = _statements[index];
        Unsupported("'json' blocks are not drawn", statement.Location);
        var depth = statement.Text.Count(c => c == '{') - statement.Text.Count(c => c == '}');
        while (depth > 0 && index + 1 < _statements.Count)
        {
            index++;
            depth += _statements[index].Text.Count(c => c == '{') - _statements[index].Text.Count(c => c == '}');
        }

        if (depth > 0)
        {
            throw Error("the json block is not closed", statement.Location, "close it with '}'");
        }

        return index;
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
            throw NestingError(block.Location);
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
            throw Error("'}' closes no package", statement.Location, "remove the '}', or open a package with 'package Name {'");
        }

        _open.RemoveAt(_open.Count - 1);
    }

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
    ///     Counts a node (an entity or a note) against <see cref="PlantUmlLimits.MaxNodes"/>.
    /// </summary>
    /// <param name="location">The location of the statement.</param>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the diagram has too many.</exception>
    private void CountNode(PlantUmlLocation location)
    {
        if (_entities.Count + _notes.Count >= _limits.MaxNodes)
        {
            throw new PlantUmlSyntaxException(
                PlantUmlDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"the diagram has more than {_limits.MaxNodes} elements and notes (PlantUmlLimits.MaxNodes)"),
                location,
                "split the diagram, or raise PlantUmlLimits.MaxNodes when the source is trusted");
        }
    }

    /// <summary>
    ///     Creates the error of packages nested too deeply.
    /// </summary>
    /// <param name="location">The location.</param>
    /// <returns>The exception.</returns>
    private PlantUmlSyntaxException NestingError(PlantUmlLocation location) =>
        new(
            PlantUmlDiagnosticCodes.LimitExceeded,
            string.Create(CultureInfo.InvariantCulture, $"the packages nest more than {_limits.MaxNestingDepth} deep (PlantUmlLimits.MaxNestingDepth)"),
            location,
            "nest fewer packages, or raise PlantUmlLimits.MaxNestingDepth when the source is trusted");

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
    ///     Declares an entity (or reuses the one the name already identifies in the current package, changing its kind
    ///     and display) and makes it the last entity.
    /// </summary>
    /// <param name="name">The name as written.</param>
    /// <param name="kind">The kind of the declaration.</param>
    /// <param name="display">The display written with <c>as</c>, or null.</param>
    /// <param name="statement">The statement.</param>
    /// <returns>The entity.</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the name is a package or a note, or a limit is exceeded.</exception>
    private ClassEntity Declare(SvekName name, ClassKind kind, string? display, SourceStatement statement)
    {
        CheckLabel(display, statement.Location);
        var entity = Lookup(name, shortNames: false) switch
        {
            ClassEntity existing => existing,
            null => Create(name, kind, statement),
            var other => throw NameUsed(name, other, statement),
        };
        entity.Kind = kind;
        entity.Display = display ?? entity.Display;
        _lastEntity = entity;
        return entity;
    }

    /// <summary>
    ///     Resolves the name of a link end, a note target, or a member owner: a floating note, an entity, or a package
    ///     it identifies (in the current package, its parents, or by its short name when that is unique), or a new
    ///     class in the current package.
    /// </summary>
    /// <param name="name">The name as written.</param>
    /// <param name="statement">The statement.</param>
    /// <returns>The node.</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the name is an element of another diagram type, or a limit is exceeded.</exception>
    private ClassNode ResolveEnd(SvekName name, SourceStatement statement)
    {
        var (what, example) = name.Kind switch
        {
            SvekNameKind.Parenthesized => ("a use case", "(" + name.Text + ")"),
            SvekNameKind.Bracketed => ("a component", "[" + name.Text + "]"),
            SvekNameKind.Colon => ("an actor", ":" + name.Text + ":"),
            SvekNameKind.Star or SvekNameKind.History or SvekNameKind.DeepHistory => ("a state", "[*]"),
            _ => (null, null),
        };
        if (what is not null)
        {
            throw Error($"'{example}' is {what}, not an element of a class diagram", statement.Location, MixingHint);
        }

        // A link to a member (A::field) is drawn from its class
        if (name.Kind == SvekNameKind.Plain && _separator != "::" && name.Text.IndexOf("::", StringComparison.Ordinal) is var member and > 0)
        {
            Unsupported($"a link to the member '{name.Text}' is drawn from its class", statement.Location);
            name = name with { Text = name.Text[..member] };
        }

        return Lookup(name, shortNames: true) ?? Create(name, ClassKind.Class, statement);
    }

    /// <summary>
    ///     Finds the node a name identifies.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="shortNames">Whether a name is also looked up in the parent packages and by its short name (for references, not declarations).</param>
    /// <returns>The node, or null.</returns>
    private ClassNode? Lookup(SvekName name, bool shortNames)
    {
        if (_noteByAlias.TryGetValue(name.Text, out var note))
        {
            return note;
        }

        var segments = Segments(name);
        var package = ContextOf(segments);
        while (true)
        {
            var id = Join(package?.Id, segments);
            if (_entityById.TryGetValue(id, out var entity))
            {
                return entity;
            }

            if (_packageById.TryGetValue(id, out var found))
            {
                return found;
            }

            if (package is null || !shortNames)
            {
                break;
            }

            package = package.Package;
        }

        // A short name that identifies exactly one entity
        if (shortNames && segments.Count == 1 && _entityByShortName.TryGetValue(segments[0], out var candidates) && candidates.Count == 1)
        {
            return candidates[0];
        }

        return null;
    }

    /// <summary>
    ///     Creates an entity in the current package, creating the implicit packages of a qualified name.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="statement">The statement.</param>
    /// <returns>The entity.</returns>
    private ClassEntity Create(SvekName name, ClassKind kind, SourceStatement statement)
    {
        var segments = Segments(name);
        var parent = ContextOf(segments);
        foreach (var segment in segments.Take(segments.Count - 1))
        {
            parent = EnsurePackage(parent, segment, statement.Location);
        }

        var shortName = segments[^1];
        var id = Join(parent?.Id, [shortName]);
        if (_packageById.TryGetValue(id, out var package))
        {
            throw NameUsed(name, package, statement);
        }

        CheckLabel(shortName, statement.Location);
        CountNode(statement.Location);
        var entity = new ClassEntity(id, shortName, kind, statement.Location) { Package = parent };
        _entities.Add(entity);
        _entityById[id] = entity;
        if (!_entityByShortName.TryGetValue(shortName, out var list))
        {
            list = [];
            _entityByShortName[shortName] = list;
        }

        list.Add(entity);
        return entity;
    }

    /// <summary>
    ///     Declares a package (and the implicit packages of a qualified name) in the current package.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="location">The location of the statement.</param>
    /// <returns>The innermost package.</returns>
    private ClassPackage DeclarePackage(SvekName name, PlantUmlLocation location)
    {
        var segments = Segments(name);
        var package = ContextOf(segments);
        foreach (var segment in segments)
        {
            package = EnsurePackage(package, segment, location);
        }

        return package!;
    }

    /// <summary>
    ///     Gets the package of a name in a parent, creating it when it does not exist.
    /// </summary>
    /// <param name="parent">The parent, or null at the top.</param>
    /// <param name="name">The short name.</param>
    /// <param name="location">The location of the statement.</param>
    /// <returns>The package.</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the name is a class, or the packages nest too deeply.</exception>
    private ClassPackage EnsurePackage(ClassPackage? parent, string name, PlantUmlLocation location)
    {
        var id = Join(parent?.Id, [name]);
        if (_packageById.TryGetValue(id, out var existing))
        {
            return existing;
        }

        if (_entityById.ContainsKey(id) || _noteByAlias.ContainsKey(id))
        {
            throw Error($"the name '{id}' is already used by an element; a package cannot have it", location, "give the package another name");
        }

        var depth = 1;
        for (var p = parent; p is not null; p = p.Package)
        {
            depth++;
        }

        if (depth > _limits.MaxNestingDepth)
        {
            throw NestingError(location);
        }

        CheckLabel(name, location);
        var package = new ClassPackage(id, name, location) { Package = parent };
        _packages.Add(package);
        _packageById[id] = package;
        return package;
    }

    /// <summary>
    ///     Splits a name into the segments of its packages and its short name (a quoted name, and every name when the
    ///     separator is <c>none</c>, is one segment).
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The segments.</returns>
    private List<string> Segments(SvekName name)
    {
        if (name.Kind == SvekNameKind.Quoted || _separator is null)
        {
            return [name.Text];
        }

        var parts = name.Text.Split(_separator);
        return parts.Any(p => p.Length == 0) ? [name.Text] : [.. parts];
    }

    /// <summary>
    ///     Gets the package a qualified name is relative to: the top of the diagram when its first segment is a package
    ///     at the top (<c>package a.b</c> inside <c>package a</c> is <c>a.b</c>), else the current package (<c>class
    ///     x.C</c> inside <c>package p</c> is <c>p.x.C</c>), as PlantUML resolves names.
    /// </summary>
    /// <param name="segments">The segments of the name.</param>
    /// <returns>The package, or null for the top.</returns>
    private ClassPackage? ContextOf(List<string> segments) =>
        segments.Count > 1 && _packageById.TryGetValue(segments[0], out var root) && root.Package is null ? null : CurrentPackage;

    /// <summary>
    ///     Joins a package id and name segments into a qualified id.
    /// </summary>
    /// <param name="prefix">The id of the package, or null at the top.</param>
    /// <param name="segments">The segments.</param>
    /// <returns>The id.</returns>
    private string Join(string? prefix, IReadOnlyList<string> segments)
    {
        if (_separator is null)
        {
            return segments[^1];
        }

        var tail = string.Join(_separator, segments);
        return prefix is null ? tail : prefix + _separator + tail;
    }

    /// <summary>
    ///     Creates the error of a name that is already used by another kind of node.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="node">The node that uses it.</param>
    /// <param name="statement">The statement.</param>
    /// <returns>The exception.</returns>
    private static PlantUmlSyntaxException NameUsed(SvekName name, ClassNode node, SourceStatement statement)
    {
        var what = node switch
        {
            ClassPackage => "a package",
            ClassNote => "a note",
            _ => "an element",
        };
        return Error($"'{name.Text}' is already {what}", statement.Location, "give the element another name");
    }

    /// <summary>
    ///     Gets the id of the next link.
    /// </summary>
    /// <returns>The id.</returns>
    private string NextLinkId() => "link#" + (++_linkCount).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    ///     Gets the id of the next note without an alias: <c>note"k</c>, with a quote that no name or alias can hold, so
    ///     that it never takes the id of an element or of a floating note.
    /// </summary>
    /// <returns>The id.</returns>
    private string NextNoteId() => "note\"" + (++_noteCount).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    ///     An open block: a package or namespace, or a <c>together</c> group (no package).
    /// </summary>
    /// <param name="Package">The package, or null for a group.</param>
    /// <param name="Location">The location of the opening statement.</param>
    /// <param name="Keyword">What the block is, for the error of an unclosed block.</param>
    private sealed record OpenBlock(ClassPackage? Package, PlantUmlLocation Location, string Keyword);
}
