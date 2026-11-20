using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;
using DemaConsulting.CanvasNet.PlantUml.Svek;

namespace DemaConsulting.CanvasNet.PlantUml.Class;

/// <summary>
///     A parsed class or object diagram: its entities, packages, notes, links, <c>hide</c> and <c>show</c> rules, page
///     texts, layout settings, and look.
/// </summary>
/// <remarks>
///     <para>
///         The model is what <see cref="ClassParser"/> produces. At every measure or render call it is lowered to a
///         <see cref="SvekDiagram"/> (<see cref="Lower"/>): every entity gets a <see cref="ClassRenderer"/> image
///         measured with the text layout of the call, every package a cluster image, and the notes and links their
///         Svek counterparts; the Svek engine lays the diagram out and draws it, and the page texts are placed around
///         it.
///     </para>
///     <para>
///         For the document's facts, the nodes are the entities (classes and their kin, objects, and maps; notes are
///         not counted), the edges are the links that do not touch a note (hidden links counted), and the subgraphs are
///         the packages and namespaces that hold something. The model is not changed after parsing and is safe for
///         concurrent use.
///     </para>
/// </remarks>
internal sealed class ClassModel : IPlantUmlDiagram
{
    /// <summary>
    ///     Initializes a model.
    /// </summary>
    /// <param name="entities">The entities in order of declaration.</param>
    /// <param name="packages">The packages in order of declaration (a parent before its children).</param>
    /// <param name="notes">The notes in order.</param>
    /// <param name="links">The links in order.</param>
    /// <param name="rules">The hide and show rules in order.</param>
    /// <param name="page">The page texts.</param>
    /// <param name="theme">The look, with the diagram's skin parameters applied.</param>
    /// <param name="reports">The reports the parser found, in text order.</param>
    /// <param name="keywordLocation">The location the diagram type was detected at.</param>
    public ClassModel(
        IReadOnlyList<ClassEntity> entities,
        IReadOnlyList<ClassPackage> packages,
        IReadOnlyList<ClassNote> notes,
        IReadOnlyList<ClassLink> links,
        IReadOnlyList<ClassHideRule> rules,
        PageTexts page,
        SkinTheme theme,
        IReadOnlyList<PlantUmlPendingReport> reports,
        PlantUmlLocation keywordLocation)
    {
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(notes);
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(reports);
        (Entities, Packages, Notes, Links, Rules) = (entities, packages, notes, links, rules);
        (Page, Theme, Reports, KeywordLocation) = (page, theme, reports, keywordLocation);
        NodeCount = entities.Count;
        EdgeCount = links.Count(l => !l.TouchesNote);
        SubgraphCount = packages.Count(p =>
            entities.Any(e => e.Package == p) || packages.Any(c => c.Package == p) || notes.Any(n => n.IsFloating && n.Package == p));
    }

    /// <summary>Gets the entities in order of declaration.</summary>
    public IReadOnlyList<ClassEntity> Entities { get; }

    /// <summary>Gets the packages in order of declaration, a parent before its children.</summary>
    public IReadOnlyList<ClassPackage> Packages { get; }

    /// <summary>Gets the notes in order.</summary>
    public IReadOnlyList<ClassNote> Notes { get; }

    /// <summary>Gets the links in order.</summary>
    public IReadOnlyList<ClassLink> Links { get; }

    /// <summary>Gets the hide and show rules in order.</summary>
    public IReadOnlyList<ClassHideRule> Rules { get; }

    /// <summary>Gets the page texts.</summary>
    public PageTexts Page { get; }

    /// <summary>Gets the look, with the diagram's skin parameters applied.</summary>
    public SkinTheme Theme { get; }

    /// <summary>Gets the location the diagram type was detected at (where layout failures are located).</summary>
    public PlantUmlLocation KeywordLocation { get; }

    /// <summary>Gets the rank direction (<c>left to right direction</c>); top to bottom by default.</summary>
    public SvekRankDir RankDir { get; init; }

    /// <summary>Gets the space between neighbors (<c>skinparam nodesep</c>), or null.</summary>
    public double? NodeSep { get; init; }

    /// <summary>Gets the space between ranks (<c>skinparam ranksep</c>), or null.</summary>
    public double? RankSep { get; init; }

    /// <summary>Gets the diagram's own scale (its <c>scale</c> statement); 1 by default.</summary>
    public double Scale { get; init; } = 1;

    /// <inheritdoc/>
    public string? Title => Page.Title?.Text;

    /// <inheritdoc/>
    public int NodeCount { get; }

    /// <inheritdoc/>
    public int EdgeCount { get; }

    /// <inheritdoc/>
    public int SubgraphCount { get; }

    /// <inheritdoc/>
    public IReadOnlyList<PlantUmlPendingReport> Reports { get; }

    /// <inheritdoc/>
    public Scene Layout(DiagramRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var diagram = Lower(context);
        var placement = SvekLayout.Run(diagram, context);
        var body = SvekRenderer.Render(diagram, placement);
        var scene = ScenePage.Decorate(body, Page, Theme, context.Text, context.Sink, PageMetrics.Svek);
        return scene with { Background = Theme.Background, Scale = Scale };
    }

    /// <summary>
    ///     Gets what is shown of an entity once the hide and show rules are applied in order.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <returns>The portions.</returns>
    public ClassPortions PortionsOf(ClassEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var (fields, methods) = entity.IsObjectLike
            ? (entity.Body, (IReadOnlyList<string>)[])
            : ClassBody.Split(entity.Body);
        var enhanced = entity.Body.Any(ClassBody.IsSeparator);
        var result = ClassPortions.All;
        foreach (var rule in Rules.Where(r => Matches(r.Gender, entity)))
        {
            switch (rule.Portion)
            {
                case ClassPortion.Circle:
                    result = result with { Circle = rule.Show };
                    break;
                case ClassPortion.Stereotype:
                    result = result with { Stereotype = rule.Show };
                    break;
                default:
                    var applyFields = rule.Portion is ClassPortion.Members or ClassPortion.Fields && (!rule.EmptyOnly || (fields.Count == 0 && !enhanced));
                    var applyMethods = rule.Portion is ClassPortion.Members or ClassPortion.Methods && (!rule.EmptyOnly || (methods.Count == 0 && !enhanced));
                    result = result with
                    {
                        Fields = applyFields ? rule.Show : result.Fields,
                        Methods = applyMethods ? rule.Show : result.Methods,
                    };
                    break;
            }
        }

        return result;
    }

    /// <summary>
    ///     Lowers the diagram for the Svek engine, measuring its entities with the text layout of a call.
    /// </summary>
    /// <param name="context">The context of the call.</param>
    /// <returns>The lowered diagram.</returns>
    public SvekDiagram Lower(DiagramRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var nodes = new Dictionary<ClassNode, SvekNode>(ReferenceEqualityComparer.Instance);
        var clusters = new List<SvekCluster>();
        foreach (var package in Packages)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var cluster = new SvekCluster(package.Id)
            {
                Parent = package.Package is null ? null : (SvekCluster)nodes[package.Package],
                Location = package.Location,
                Image = SvekRenderer.ClusterImage(package.Symbol, package.Id, package.Title, package.Stereotypes, Theme, context.Text, context.Sink, package.Location, package.Fill),
            };
            nodes[package] = cluster;
            clusters.Add(cluster);
        }

        var entities = new List<SvekEntity>();
        foreach (var entity in Entities)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var svek = new SvekEntity(entity.Id, ClassRenderer.Image(entity, PortionsOf(entity), Theme, context.Text, context.Sink))
            {
                Parent = ClusterOf(entity, nodes),
                Location = entity.Location,
            };
            nodes[entity] = svek;
            entities.Add(svek);
        }

        // Floating notes first (links may end at them), then the links, then the notes attached to entities or links
        var notes = new List<SvekNote>();
        foreach (var note in Notes.Where(n => n.IsFloating))
        {
            var svek = new SvekNote(note.Id, note.Text) { Fill = note.Fill, Parent = ClusterOf(note, nodes), Location = note.Location };
            nodes[note] = svek;
            notes.Add(svek);
        }

        var links = new Dictionary<ClassLink, SvekLink>(ReferenceEqualityComparer.Instance);
        foreach (var link in Links)
        {
            links[link] = new SvekLink(link.Id, nodes[link.From], nodes[link.To])
            {
                TailDecor = link.TailDecor,
                HeadDecor = link.HeadDecor,
                Style = link.Style,
                Thickness = link.Thickness,
                Color = link.Color,
                Length = link.Length,
                Direction = link.Direction,
                Label = link.Label,
                LabelArrow = link.LabelArrow,
                TailLabel = link.TailLabel,
                HeadLabel = link.HeadLabel,
                Weight = link.Weight,
                Location = link.Location,
            };
        }

        foreach (var note in Notes.Where(n => !n.IsFloating))
        {
            notes.Add(new SvekNote(note.Id, note.Text)
            {
                Fill = note.Fill,
                Target = note.Target is null ? null : (SvekEntity)nodes[note.Target],
                Link = note.OnLink is null ? null : links[note.OnLink],
                Position = note.Position,
                Location = note.Location,
            });
        }

        // Keep the notes in text order, as the layout expects
        var order = Notes.Select((n, i) => (n.Id, i)).ToDictionary(p => p.Id, p => p.i, StringComparer.Ordinal);
        return new SvekDiagram
        {
            Entities = entities,
            Clusters = clusters,
            Notes = notes.OrderBy(n => order[n.Id]).ToList(),
            Links = Links.Select(l => links[l]).ToList(),
            RankDir = RankDir,
            NodeSep = NodeSep,
            RankSep = RankSep,
            Theme = Theme,
            Location = KeywordLocation,
        };
    }

    /// <summary>
    ///     Decides whether a rule applies to an entity.
    /// </summary>
    /// <param name="gender">The gender of the rule.</param>
    /// <param name="entity">The entity.</param>
    /// <returns>True when it does.</returns>
    private static bool Matches(string? gender, ClassEntity entity)
    {
        if (gender is null)
        {
            return true;
        }

        if (gender.StartsWith("<<", StringComparison.Ordinal) && gender.EndsWith(">>", StringComparison.Ordinal))
        {
            var label = gender[2..^2].Trim();
            return entity.Stereotypes.Any(s => s.Equals(label, StringComparison.OrdinalIgnoreCase));
        }

        return gender.ToLowerInvariant() switch
        {
            "class" => entity.Kind == ClassKind.Class,
            "abstract" => entity.Kind == ClassKind.AbstractClass,
            "interface" => entity.Kind == ClassKind.Interface,
            "enum" => entity.Kind == ClassKind.Enum,
            "annotation" => entity.Kind == ClassKind.Annotation,
            "entity" => entity.Kind == ClassKind.Entity,
            "exception" => entity.Kind == ClassKind.Exception,
            "struct" => entity.Kind == ClassKind.Struct,
            "protocol" => entity.Kind == ClassKind.Protocol,
            "metaclass" => entity.Kind == ClassKind.Metaclass,
            "stereotype" => entity.Kind == ClassKind.Stereotype,
            "record" => entity.Kind == ClassKind.Record,
            "dataclass" => entity.Kind == ClassKind.DataClass,
            "object" => entity.Kind == ClassKind.Object,
            _ => gender == entity.Id || gender == entity.Display,
        };
    }

    /// <summary>
    ///     Gets the cluster of the package of a node.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="nodes">The lowered nodes.</param>
    /// <returns>The cluster, or null at the top.</returns>
    private static SvekCluster? ClusterOf(ClassNode node, Dictionary<ClassNode, SvekNode> nodes) =>
        node.Package is null ? null : (SvekCluster)nodes[node.Package];
}
