// Portions derived from mermaid (MIT); see third-party/README.md
using System.Globalization;
using System.Text;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Er;

/// <summary>
///     A parsed ER diagram: a port of Mermaid's ER database (<c>erDb.ts</c>, with the types of <c>erTypes.ts</c>), which
///     the parser fills through the calls the grammar's actions make.
/// </summary>
/// <remarks>
///     <para>
///         <b>Fidelity.</b> Entities are kept by name in the order they are first named, with Mermaid's id
///         (<c>entity-&lt;name&gt;-&lt;n&gt;</c>, where <c>n</c> counts the entities before), the first non-empty alias,
///         their attributes (repeated blocks append), the CSS classes after <c>default</c> and the inline styles.
///         Relationships keep the entity ids (or the id of a subgraph they name), the role and the specification, whose
///         <c>cardA</c> is the right cardinality and <c>cardB</c> the left one. Style classes (<c>classDef</c>) keep their
///         styles and, for a style with <c>color</c>, a text style with <c>fill</c> as <c>bgFill</c>. A subgraph keeps its
///         trimmed id, its sanitized and trimmed title, its direction and its nodes: the values of its statements without
///         duplicates, without empty texts and without the nodes an earlier subgraph already holds. As in Mermaid, a
///         relationship to a subgraph also adds an entity named after the subgraph, which <c>getData</c> skips but which
///         uses up an entity number.
///     </para>
///     <para>
///         <b>Limits.</b> Entities (including those named after a subgraph) and subgraphs count against
///         <see cref="MermaidLimits.MaxNodes"/>, as the flowchart model counts vertices and subgraphs; relationships count
///         against <see cref="MermaidLimits.MaxEdges"/>, as class relations and state transitions do. The texts the model
///         joins or derives are checked against <see cref="MermaidLimits.MaxLabelLength"/>: the text styles of a style
///         class, the CSS classes of an entity or a subgraph (joined with spaces) and their compiled styles (joined with
///         semicolons) (<see cref="MermaidDiagnosticCodes.LimitExceeded"/>); the parser checks the texts it keeps and
///         joins. Mermaid's ER database has no errors of its own.
///     </para>
///     <para>
///         The model is filled by one parser on one thread; once parsing is over it is never changed, so it is safe for
///         concurrent use.
///     </para>
/// </remarks>
internal sealed partial class ErModel
{
    /// <summary>The relationship type of an identifying (solid) relationship.</summary>
    public const string Identifying = "IDENTIFYING";

    /// <summary>The CSS class every entity has first.</summary>
    private const string DefaultClass = "default";

    /// <summary>The limits of the open call.</summary>
    private readonly MermaidLimits _limits;

    /// <summary>The entities in the order they were first named (Mermaid's <c>entities</c>).</summary>
    private readonly List<ErEntity> _entities = [];

    /// <summary>The entities by name.</summary>
    private readonly Dictionary<string, ErEntity> _entityIndex = new(StringComparer.Ordinal);

    /// <summary>The relationships in order (Mermaid's <c>relationships</c>).</summary>
    private readonly List<ErRelationship> _relationships = [];

    /// <summary>The style classes in insertion order (Mermaid's <c>classes</c>).</summary>
    private readonly List<ErStyleClass> _styleClasses = [];

    /// <summary>The style classes by id.</summary>
    private readonly Dictionary<string, ErStyleClass> _styleClassIndex = new(StringComparer.Ordinal);

    /// <summary>The subgraphs in the order they end (Mermaid's <c>subGraphs</c>; a nested one ends first).</summary>
    private readonly List<ErSubgraph> _subgraphs = [];

    /// <summary>The subgraphs by id; a later subgraph with the same id replaces an earlier one (Mermaid's <c>subGraphLookup</c>).</summary>
    private readonly Dictionary<string, ErSubgraph> _subgraphIndex = new(StringComparer.Ordinal);

    /// <summary>The nodes every subgraph holds, which a later subgraph cannot hold again (Mermaid's <c>makeUniq</c>).</summary>
    private readonly HashSet<string> _subgraphNodes = new(StringComparer.Ordinal);

    /// <summary>
    ///     Initializes an empty model.
    /// </summary>
    /// <param name="limits">The limits of the open call.</param>
    /// <param name="keywordLocation">The location of the diagram keyword.</param>
    public ErModel(MermaidLimits limits, MermaidLocation keywordLocation)
    {
        ArgumentNullException.ThrowIfNull(limits);
        _limits = limits;
        KeywordLocation = keywordLocation;
    }

    /// <summary>Gets the location of the diagram keyword.</summary>
    public MermaidLocation KeywordLocation { get; }

    /// <summary>Mermaid's id of the diagram, its <c>diag.type</c>.</summary>
    public const string MermaidId = "er";

    /// <summary>Gets the direction (Mermaid's <c>getDirection</c>): the last top-level direction statement, else <c>TB</c>.</summary>
    public string Direction { get; private set; } = "TB";

    /// <summary>Gets the accessible title (empty when there is none).</summary>
    public string AccessibleTitle { get; private set; } = string.Empty;

    /// <summary>Gets the accessible description (empty when there is none).</summary>
    public string AccessibleDescription { get; private set; } = string.Empty;

    /// <summary>Gets the entities in the order they were first named (Mermaid's <c>getEntities</c>).</summary>
    public IReadOnlyList<ErEntity> Entities => _entities;

    /// <summary>Gets the relationships in order (Mermaid's <c>getRelationships</c>).</summary>
    public IReadOnlyList<ErRelationship> Relationships => _relationships;

    /// <summary>Gets the style classes in insertion order (Mermaid's <c>getClasses</c>).</summary>
    public IReadOnlyList<ErStyleClass> StyleClasses => _styleClasses;

    /// <summary>Gets the subgraphs in the order they end (Mermaid's <c>getSubGraphs</c>).</summary>
    public IReadOnlyList<ErSubgraph> Subgraphs => _subgraphs;

    /// <summary>
    ///     Adds an entity when its name is new (Mermaid's <c>addEntity</c>), or gives an entity without an alias the alias.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="alias">The alias, or empty for none.</param>
    /// <param name="location">The location of the statement.</param>
    /// <returns>The entity.</returns>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when <see cref="MermaidLimits.MaxNodes"/> is exceeded.</exception>
    public ErEntity AddEntity(string name, string alias, MermaidLocation location)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(alias);
        if (_entityIndex.TryGetValue(name, out var entity))
        {
            if (entity.Alias.Length == 0 && alias.Length > 0)
            {
                entity.Alias = alias;
            }

            return entity;
        }

        CheckNodeCount(location);
        entity = new ErEntity
        {
            Name = name,
            Id = string.Create(CultureInfo.InvariantCulture, $"entity-{name}-{_entities.Count}"),
            Alias = alias,
            Location = location,
        };
        _entities.Add(entity);
        _entityIndex[name] = entity;
        return entity;
    }

    /// <summary>
    ///     Adds attributes to an entity, adding the entity when it is new (Mermaid's <c>addAttributes</c>).
    /// </summary>
    /// <param name="name">The entity name.</param>
    /// <param name="attributes">The attributes in order.</param>
    /// <param name="location">The location of the statement.</param>
    public void AddAttributes(string name, IReadOnlyList<ErAttribute> attributes, MermaidLocation location)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        AddEntity(name, string.Empty, location).Attributes.AddRange(attributes);
    }

    /// <summary>
    ///     Adds a relationship (Mermaid's <c>addRelationship</c>): each side is the id of the subgraph it names, else the id
    ///     of its entity, which is added when it is new.
    /// </summary>
    /// <param name="entityA">The name of the first entity.</param>
    /// <param name="roleA">The role.</param>
    /// <param name="entityB">The name of the second entity.</param>
    /// <param name="spec">The specification.</param>
    /// <param name="location">The location of the statement.</param>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when a limit is exceeded.</exception>
    public void AddRelationship(string entityA, string roleA, string entityB, ErRelationshipSpec spec, MermaidLocation location)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var idA = _subgraphIndex.ContainsKey(entityA) ? entityA : AddEntity(entityA, string.Empty, location).Id;
        var idB = _subgraphIndex.ContainsKey(entityB) ? entityB : AddEntity(entityB, string.Empty, location).Id;
        if (_relationships.Count >= _limits.MaxEdges)
        {
            throw new MermaidSyntaxException(
                MermaidDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"the diagram has more than {_limits.MaxEdges} relationships (MermaidLimits.MaxEdges)"),
                location,
                "split the diagram, or raise MermaidLimits.MaxEdges when the source is trusted");
        }

        _relationships.Add(new ErRelationship(idA, roleA, idB, spec, location));
    }

    /// <summary>
    ///     Sets the direction (Mermaid's <c>setDirection</c>).
    /// </summary>
    /// <param name="direction">The direction.</param>
    public void SetDirection(string direction)
    {
        ArgumentNullException.ThrowIfNull(direction);
        Direction = direction;
    }

    /// <summary>
    ///     Adds inline styles to the entities and subgraphs of the ids (Mermaid's <c>addCssStyles</c>); unknown ids are
    ///     ignored.
    /// </summary>
    /// <param name="ids">The ids.</param>
    /// <param name="styles">The styles.</param>
    /// <param name="location">The location of the statement.</param>
    public void AddCssStyles(IReadOnlyList<string> ids, IReadOnlyList<string> styles, MermaidLocation location)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(styles);
        foreach (var id in ids)
        {
            if (_entityIndex.TryGetValue(id, out var entity))
            {
                entity.CssStyles.AddRange(styles);
            }

            if (_subgraphIndex.TryGetValue(id, out var subgraph))
            {
                subgraph.CssStyles.AddRange(styles);
            }
        }
    }

    /// <summary>
    ///     Adds or extends style classes (Mermaid's <c>addClass</c>): a style with <c>color</c> also gives a text style,
    ///     with its first <c>fill</c> as <c>bgFill</c>.
    /// </summary>
    /// <param name="ids">The class names.</param>
    /// <param name="styles">The styles.</param>
    /// <param name="location">The location of the statement.</param>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when a text style is too long.</exception>
    public void AddClass(IReadOnlyList<string> ids, IReadOnlyList<string> styles, MermaidLocation location)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(styles);
        foreach (var id in ids)
        {
            if (!_styleClassIndex.TryGetValue(id, out var styleClass))
            {
                styleClass = new ErStyleClass(id);
                _styleClasses.Add(styleClass);
                _styleClassIndex[id] = styleClass;
            }

            foreach (var style in styles)
            {
                if (style.Contains("color", StringComparison.Ordinal))
                {
                    var textStyle = ReplaceFirst(style, "fill", "bgFill");
                    CheckLabelLength(textStyle.Length, location);
                    styleClass.TextStyles.Add(textStyle);
                }

                styleClass.Styles.Add(style);
            }
        }
    }

    /// <summary>
    ///     Adds CSS classes to the entities and subgraphs of the ids (Mermaid's <c>setClass</c>); unknown ids are ignored.
    /// </summary>
    /// <param name="ids">The ids.</param>
    /// <param name="classNames">The class names.</param>
    /// <param name="location">The location of the statement.</param>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when the classes of an entity or a subgraph get too long.</exception>
    public void SetClass(IReadOnlyList<string> ids, IReadOnlyList<string> classNames, MermaidLocation location)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(classNames);
        foreach (var id in ids)
        {
            if (_entityIndex.TryGetValue(id, out var entity))
            {
                foreach (var className in classNames)
                {
                    entity.CssClassesLength += 1 + className.Length;
                    CheckLabelLength(entity.CssClassesLength, location);
                    entity.Classes.Add(className);
                }

                entity.ClassLocation = location;
            }

            if (_subgraphIndex.TryGetValue(id, out var subgraph))
            {
                foreach (var className in classNames)
                {
                    subgraph.CssClassesLength += (subgraph.Classes.Count > 0 ? 1 : 0) + className.Length;
                    CheckLabelLength(subgraph.CssClassesLength, location);
                    subgraph.Classes.Add(className);
                }

                subgraph.ClassLocation = location;
            }
        }
    }

    /// <summary>
    ///     Adds a subgraph (Mermaid's <c>addSubGraph</c>): its trimmed id, its sanitized and trimmed title, its direction
    ///     and the values of its statements as nodes, without duplicates, empty texts and the nodes of earlier subgraphs.
    /// </summary>
    /// <param name="id">The id as written.</param>
    /// <param name="items">The values of the subgraph's statements.</param>
    /// <param name="direction">The subgraph's last direction statement, or null.</param>
    /// <param name="title">The title as written (the id when it has none).</param>
    /// <param name="location">The location of the <c>subgraph</c> keyword.</param>
    /// <returns>The trimmed id, the subgraph's value in the enclosing document.</returns>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when <see cref="MermaidLimits.MaxNodes"/> is exceeded.</exception>
    public string AddSubgraph(string id, IReadOnlyList<string> items, string? direction, string title, MermaidLocation location)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(title);
        CheckNodeCount(location);
        var trimmedId = Preprocessor.TrimJs(id);

        // uniq: no empty texts, no duplicates (compared trimmed); then makeUniq: no nodes of earlier subgraphs
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var nodes = items
            .Where(item => Preprocessor.TrimJs(item) is { Length: > 0 } trimmed && seen.Add(trimmed))
            .Where(item => !_subgraphNodes.Contains(item))
            .ToList();
        _subgraphNodes.UnionWith(nodes);
        var subgraph = new ErSubgraph
        {
            Id = trimmedId,
            Title = Preprocessor.TrimJs(Sanitize(title)),
            Nodes = nodes,
            Dir = direction,
            Location = location,
        };
        _subgraphs.Add(subgraph);
        _subgraphIndex[trimmedId] = subgraph;
        return trimmedId;
    }

    /// <summary>
    ///     Completes the model once the text is parsed: the color index of each entity <c>getData</c> draws (the entities
    ///     not named after a subgraph, counted in order) and the compiled styles of the entities and subgraphs.
    /// </summary>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when compiled styles get too long.</exception>
    public void Complete()
    {
        var colorIndex = 0;
        foreach (var entity in _entities)
        {
            if (_subgraphIndex.ContainsKey(entity.Name))
            {
                continue;
            }

            entity.ColorIndex = colorIndex++;
            entity.CssCompiledStyles = CompiledStyles([DefaultClass, .. entity.Classes], entity.ClassLocation ?? entity.Location);
        }

        foreach (var subgraph in _subgraphs)
        {
            subgraph.CssCompiledStyles = CompiledStyles(subgraph.Classes, subgraph.ClassLocation ?? subgraph.Location);
        }
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
    ///     Sanitizes a text as Mermaid's <c>sanitizeText</c> does (an empty text is kept).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The sanitized text.</returns>
    private static string Sanitize(string text) => text.Length == 0 ? text : HtmlSanitizer.Sanitize(text);

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
    ///     Compiles the styles of classes (Mermaid's <c>getCompiledStyles</c>): for each class that exists, its styles then
    ///     its text styles, trimmed.
    /// </summary>
    /// <param name="classNames">The class names in order.</param>
    /// <param name="location">The location of the statement that gave the classes.</param>
    /// <returns>The compiled styles.</returns>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when the styles joined with semicolons are too long.</exception>
    private List<string> CompiledStyles(IEnumerable<string> classNames, MermaidLocation location)
    {
        var compiled = new List<string>();
        var length = -1;
        foreach (var className in classNames)
        {
            if (!_styleClassIndex.TryGetValue(className, out var styleClass))
            {
                continue;
            }

            foreach (var style in styleClass.Styles.Concat(styleClass.TextStyles))
            {
                var trimmed = Preprocessor.TrimJs(style);
                length += 1 + trimmed.Length;
                CheckLabelLength(length, location);
                compiled.Add(trimmed);
            }
        }

        return compiled;
    }

    /// <summary>
    ///     Checks that one more entity or subgraph fits <see cref="MermaidLimits.MaxNodes"/>, which counts them together,
    ///     as the flowchart model counts vertices and subgraphs.
    /// </summary>
    /// <param name="location">The location of the statement that adds it.</param>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when it does not.</exception>
    private void CheckNodeCount(MermaidLocation location)
    {
        if (_entities.Count + _subgraphs.Count >= _limits.MaxNodes)
        {
            throw new MermaidSyntaxException(
                MermaidDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"the diagram has more than {_limits.MaxNodes} entities and subgraphs (MermaidLimits.MaxNodes)"),
                location,
                "split the diagram, or raise MermaidLimits.MaxNodes when the source is trusted");
        }
    }

    /// <summary>
    ///     Checks the length of a text the model joins or derives against <see cref="MermaidLimits.MaxLabelLength"/>, as
    ///     the flowchart model checks its labels.
    /// </summary>
    /// <param name="length">The length of the text.</param>
    /// <param name="location">The location of the statement.</param>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when the text is too long.</exception>
    private void CheckLabelLength(int length, MermaidLocation location)
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
///     An entity of the ER database (Mermaid's <c>EntityNode</c>).
/// </summary>
/// <remarks>
///     The parser's calls fill it; once parsing is over it is never changed.
/// </remarks>
internal sealed class ErEntity
{
    /// <summary>Gets the name, which is also the label.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the id (<c>entity-&lt;name&gt;-&lt;n&gt;</c>).</summary>
    public required string Id { get; init; }

    /// <summary>Gets or sets the alias, drawn in place of the name (empty for none).</summary>
    public required string Alias { get; set; }

    /// <summary>Gets the attributes in order.</summary>
    public List<ErAttribute> Attributes { get; } = [];

    /// <summary>Gets the CSS classes after <c>default</c>, in order.</summary>
    public List<string> Classes { get; } = [];

    /// <summary>Gets the CSS classes as Mermaid's <c>cssClasses</c> holds them: <c>default</c>, then each class after a space.</summary>
    public string CssClasses => string.Concat(Classes.Select(c => " " + c).Prepend("default"));

    /// <summary>Gets or sets the length of <see cref="CssClasses"/>.</summary>
    public int CssClassesLength { get; set; } = 7;

    /// <summary>Gets the inline styles (<c>style</c> statements), in order.</summary>
    public List<string> CssStyles { get; } = [];

    /// <summary>Gets or sets the compiled styles of the classes, set when the model is complete.</summary>
    public IReadOnlyList<string> CssCompiledStyles { get; set; } = [];

    /// <summary>Gets or sets the color index of an entity that is drawn, or null for an entity named after a subgraph.</summary>
    public int? ColorIndex { get; set; }

    /// <summary>Gets the location of the statement that first named the entity.</summary>
    public MermaidLocation Location { get; init; }

    /// <summary>Gets or sets the location of the last statement that gave the entity classes, or null.</summary>
    public MermaidLocation? ClassLocation { get; set; }
}

/// <summary>
///     An attribute of an entity (Mermaid's <c>Attribute</c>).
/// </summary>
/// <param name="Type">The type, with a <c>?</c> when it has one.</param>
/// <param name="Name">The name.</param>
/// <param name="Keys">The keys in order (<c>PK</c>, <c>FK</c>, <c>UK</c>, as written); empty for none.</param>
/// <param name="Comment">The comment without its quotes, or empty for none.</param>
internal sealed record ErAttribute(string Type, string Name, IReadOnlyList<string> Keys, string Comment);

/// <summary>
///     The specification of a relationship (Mermaid's <c>RelSpec</c>).
/// </summary>
/// <param name="CardA">The cardinality at the second entity (the right one), such as <c>ZERO_OR_MORE</c>.</param>
/// <param name="RelType">The type: <c>IDENTIFYING</c> or <c>NON_IDENTIFYING</c>.</param>
/// <param name="CardB">The cardinality at the first entity (the left one).</param>
internal sealed record ErRelationshipSpec(string CardA, string RelType, string CardB);

/// <summary>
///     A relationship of the ER database (Mermaid's <c>Relationship</c>).
/// </summary>
/// <param name="EntityA">The id of the first entity, or of the subgraph it names.</param>
/// <param name="RoleA">The role.</param>
/// <param name="EntityB">The id of the second entity, or of the subgraph it names.</param>
/// <param name="RelSpec">The specification.</param>
/// <param name="Location">The location of the statement.</param>
internal sealed record ErRelationship(string EntityA, string RoleA, string EntityB, ErRelationshipSpec RelSpec, MermaidLocation Location);

/// <summary>
///     A style class of the ER database (Mermaid's <c>EntityClass</c>).
/// </summary>
/// <param name="Id">The class name.</param>
internal sealed record ErStyleClass(string Id)
{
    /// <summary>Gets the styles in order.</summary>
    public List<string> Styles { get; } = [];

    /// <summary>Gets the text styles (the styles with <c>color</c>, with <c>fill</c> as <c>bgFill</c>), in order.</summary>
    public List<string> TextStyles { get; } = [];
}

/// <summary>
///     A subgraph of the ER database (Mermaid's <c>ErSubGraph</c>).
/// </summary>
/// <remarks>
///     The parser's calls fill it; once parsing is over it is never changed.
/// </remarks>
internal sealed class ErSubgraph
{
    /// <summary>Gets the id.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the nodes: entity names and the ids of nested subgraphs (and the texts of other statements).</summary>
    public required IReadOnlyList<string> Nodes { get; init; }

    /// <summary>Gets the CSS classes in order.</summary>
    public List<string> Classes { get; } = [];

    /// <summary>Gets or sets the length of the CSS classes joined with spaces.</summary>
    public int CssClassesLength { get; set; }

    /// <summary>Gets the inline styles in order.</summary>
    public List<string> CssStyles { get; } = [];

    /// <summary>Gets or sets the compiled styles of the classes, set when the model is complete.</summary>
    public IReadOnlyList<string> CssCompiledStyles { get; set; } = [];

    /// <summary>Gets the direction, or null when the subgraph has none.</summary>
    public string? Dir { get; init; }

    /// <summary>Gets the location of the <c>subgraph</c> keyword.</summary>
    public MermaidLocation Location { get; init; }

    /// <summary>Gets or sets the location of the last statement that gave the subgraph classes, or null.</summary>
    public MermaidLocation? ClassLocation { get; set; }
}
