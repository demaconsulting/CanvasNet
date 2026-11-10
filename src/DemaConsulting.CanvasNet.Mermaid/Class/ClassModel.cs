// Portions derived from mermaid (MIT); see third-party/README.md
using System.Globalization;
using System.Text;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Class;

/// <summary>
///     A parsed class diagram: a port of Mermaid's class database (<c>classDb.ts</c>, with <c>ClassMember</c> of
///     <c>classTypes.ts</c>), whose operations the parser calls as Mermaid's grammar actions do.
/// </summary>
/// <remarks>
///     <para>
///         <b>Fidelity.</b> Every database operation is ported with Mermaid's semantics: classes in insertion order with
///         their generic type, label, DOM id (<c>classId-Name-N</c>, numbered per document from 0, as in a page that draws
///         one diagram), CSS classes, styles, annotations and members; members split into attributes and methods with
///         their visibility, classifier, parameters and return type; relations with Mermaid's relation and line codes,
///         cardinalities and labels, and lollipop interfaces; notes; namespaces with implicit dotted ancestors; style
///         classes (<c>classDef</c>), which apply only to the classes that already carry them; <c>style</c> and
///         <c>cssClass</c>, which apply only to classes that already exist, with the <c>classId-</c> prefix Mermaid gives an
///         id that starts with a digit; the link, tooltip and callback data of <c>link</c>, <c>click</c> and
///         <c>callback</c> (kept, never drawn); the direction and the accessibility texts. Texts are sanitized where Mermaid
///         sanitizes them (<see cref="HtmlSanitizer"/>), and links are checked as Mermaid's <c>strict</c> security level
///         does (<see cref="UrlSanitizer"/>), so only the checked link is ever kept.
///     </para>
///     <para>
///         <b>Errors and limits.</b> Where Mermaid's database fails with a JavaScript error (an annotation statement for a
///         class that does not exist, a member with a <c>)</c> that is not a method), the model throws a located
///         <see cref="MermaidSyntaxException"/> with code <see cref="MermaidDiagnosticCodes.SyntaxError"/> and Mermaid's
///         message. Classes, notes, interfaces and namespaces (implicit ancestors included) count together against
///         <see cref="MermaidLimits.MaxNodes"/>, as the flowchart model counts vertices and subgraphs, and relations against
///         <see cref="MermaidLimits.MaxEdges"/>. The texts the model joins are checked against
///         <see cref="MermaidLimits.MaxLabelLength"/> where they are drawn: the class name text of a labeled generic class
///         (<c>label&lt;T&gt;</c>) and the qualified id of a namespace, which is its label when
///         <c>class.hierarchicalNamespaces</c> is false, and, as they grow, the CSS classes of a class (joined with
///         spaces), the styles of a class and of a style class (joined with semicolons), which a name listed again in a
///         <c>cssClass</c>, <c>classDef</c> or click statement repeats as in Mermaid
///         (<see cref="MermaidDiagnosticCodes.LimitExceeded"/>). The parts of
///         a qualified namespace id, from nested <c>namespace</c> keywords and from dots in a name alike, count against
///         <see cref="MermaidLimits.MaxNestingDepth"/>, because each part nests one more namespace cluster; so does the
///         nesting a class named like a namespace makes, which <see cref="GetData"/> checks.
///     </para>
///     <para>
///         The model is filled by one parser on one thread; once parsing is over it is never changed, so it is safe for
///         concurrent use.
///     </para>
/// </remarks>
internal sealed partial class ClassModel : IMermaidDiagram
{
    /// <summary>The relation code of an aggregation end (<c>o</c>).</summary>
    public const int Aggregation = 0;

    /// <summary>The relation code of an extension end (<c>&lt;|</c>).</summary>
    public const int Extension = 1;

    /// <summary>The relation code of a composition end (<c>*</c>).</summary>
    public const int Composition = 2;

    /// <summary>The relation code of a dependency end (<c>&lt;</c>).</summary>
    public const int Dependency = 3;

    /// <summary>The relation code of a lollipop end (<c>()</c>).</summary>
    public const int Lollipop = 4;

    /// <summary>The line code of a solid relation (<c>--</c>).</summary>
    public const int SolidLine = 0;

    /// <summary>The line code of a dashed relation (<c>..</c>).</summary>
    public const int DottedLine = 1;

    /// <summary>The prefix of the DOM ids of classes and namespaces (Mermaid's <c>MERMAID_DOM_ID_PREFIX</c>).</summary>
    private const string DomIdPrefix = "classId-";

    /// <summary>The limits of the open call.</summary>
    private readonly MermaidLimits _limits;

    /// <summary>The classes in insertion order.</summary>
    private readonly List<ClassNode> _classes = [];

    /// <summary>The classes by id.</summary>
    private readonly Dictionary<string, ClassNode> _classIndex = new(StringComparer.Ordinal);

    /// <summary>The relations in order.</summary>
    private readonly List<ClassRelation> _relations = [];

    /// <summary>The notes in order (their ids are <c>note</c> and their index).</summary>
    private readonly List<ClassNote> _notes = [];

    /// <summary>The lollipop interfaces in order.</summary>
    private readonly List<ClassInterface> _interfaces = [];

    /// <summary>The namespaces in insertion order.</summary>
    private readonly List<ClassNamespace> _namespaces = [];

    /// <summary>The namespaces by qualified id.</summary>
    private readonly Dictionary<string, ClassNamespace> _namespaceIndex = new(StringComparer.Ordinal);

    /// <summary>The qualified ids of the namespaces being declared, innermost last.</summary>
    private readonly List<string> _namespaceStack = [];

    /// <summary>The style classes (<c>classDef</c>) in insertion order.</summary>
    private readonly List<ClassStyleClass> _styleClasses = [];

    /// <summary>The style classes by id.</summary>
    private readonly Dictionary<string, ClassStyleClass> _styleClassIndex = new(StringComparer.Ordinal);

    /// <summary>The number of classes added so far, which numbers their DOM ids (Mermaid's <c>classCounter</c>).</summary>
    private int _classCounter;

    /// <summary>The number of namespaces added so far, which numbers their DOM ids.</summary>
    private int _namespaceCounter;

    /// <summary>
    ///     Initializes an empty model.
    /// </summary>
    /// <param name="limits">The limits of the open call.</param>
    /// <param name="keywordLocation">The location of the diagram keyword.</param>
    /// <param name="mermaidId">Mermaid's id of the detected diagram: <c>classDiagram</c>, or <c>class</c> for the legacy id.</param>
    public ClassModel(MermaidLimits limits, MermaidLocation keywordLocation, string mermaidId)
    {
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(mermaidId);
        _limits = limits;
        KeywordLocation = keywordLocation;
        MermaidId = mermaidId;
    }

    /// <summary>Gets the location of the diagram keyword.</summary>
    public MermaidLocation KeywordLocation { get; }

    /// <summary>Gets Mermaid's id of the diagram (<c>classDiagram</c> or the legacy <c>class</c>), its <c>diag.type</c>.</summary>
    public string MermaidId { get; }

    /// <summary>Gets the direction: the last direction statement, else <c>TB</c>.</summary>
    public string Direction { get; private set; } = "TB";

    /// <summary>Gets the accessible title (empty when there is none).</summary>
    public string AccessibleTitle { get; private set; } = string.Empty;

    /// <summary>Gets the title the diagram declares: always null, because the class grammar has no title statement.</summary>
    public string? Title => null;

    /// <summary>Gets the accessible title, or null when there is none.</summary>
    public string? AccTitle => AccessibleTitle.Length > 0 ? AccessibleTitle : null;

    /// <summary>Gets the number of classes (notes and lollipop interfaces are not counted).</summary>
    public int NodeCount => _classes.Count;

    /// <summary>Gets the number of relations (the links of notes are not counted).</summary>
    public int EdgeCount => _relations.Count;

    /// <summary>
    ///     Gets a value indicating whether the text's own configuration nests dotted namespaces
    ///     (<c>class.hierarchicalNamespaces</c>), which selects the namespaces <see cref="SubgraphCount"/> counts.
    /// </summary>
    public bool HierarchicalNamespaces { get; init; } = true;

    /// <summary>
    ///     Gets the number of namespaces drawn under the text's own configuration: every namespace, implicit ancestors
    ///     included, or with <see cref="HierarchicalNamespaces"/> false only the declared ones, as <see cref="GetData"/>
    ///     keeps them.
    /// </summary>
    public int SubgraphCount => HierarchicalNamespaces ? _namespaces.Count : _namespaces.Count(space => space.Explicit);

    /// <summary>Gets the accessible description (empty when there is none).</summary>
    public string AccessibleDescription { get; private set; } = string.Empty;

    /// <summary>Gets the classes in insertion order (Mermaid's <c>getClasses</c>).</summary>
    public IReadOnlyList<ClassNode> Classes => _classes;

    /// <summary>Gets the relations in order (Mermaid's <c>getRelations</c>).</summary>
    public IReadOnlyList<ClassRelation> Relations => _relations;

    /// <summary>Gets the notes in order (Mermaid's <c>getNotes</c>).</summary>
    public IReadOnlyList<ClassNote> Notes => _notes;

    /// <summary>Gets the lollipop interfaces in order.</summary>
    public IReadOnlyList<ClassInterface> Interfaces => _interfaces;

    /// <summary>Gets the namespaces in insertion order, implicit ancestors included (Mermaid's <c>getNamespaces</c>).</summary>
    public IReadOnlyList<ClassNamespace> Namespaces => _namespaces;

    /// <summary>Gets the style classes in insertion order.</summary>
    public IReadOnlyList<ClassStyleClass> StyleClasses => _styleClasses;

    /// <summary>
    ///     Gets a class by id.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <returns>The class, or null.</returns>
    public ClassNode? GetClass(string id) => _classIndex.GetValueOrDefault(id);

    /// <summary>
    ///     Removes a leading colon and the white space around a label and sanitizes it (Mermaid's <c>cleanupLabel</c>).
    /// </summary>
    /// <param name="label">The label, as the grammar reads it (with its colon).</param>
    /// <returns>The cleaned label.</returns>
    public static string CleanupLabel(string label)
    {
        ArgumentNullException.ThrowIfNull(label);
        if (label.StartsWith(':'))
        {
            label = label[1..];
        }

        return Sanitize(Preprocessor.TrimJs(label));
    }

    /// <summary>
    ///     Sets the direction (a <c>direction</c> statement; the last one wins).
    /// </summary>
    /// <param name="direction">The direction: <c>TB</c>, <c>BT</c>, <c>RL</c> or <c>LR</c>.</param>
    public void SetDirection(string direction) => Direction = direction;

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
    ///     Adds a class unless it exists (Mermaid's <c>addClass</c>): a name with a generic type (<c>Name~T~</c>) is split
    ///     into the class name and the type.
    /// </summary>
    /// <param name="id">The class name as written.</param>
    /// <param name="location">The location of the name, for reports.</param>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when <see cref="MermaidLimits.MaxNodes"/> is exceeded.</exception>
    public void AddClass(string id, MermaidLocation location)
    {
        ArgumentNullException.ThrowIfNull(id);
        var (className, type) = SplitClassNameAndType(Sanitize(id));
        if (_classIndex.ContainsKey(className))
        {
            return;
        }

        CheckNodeCount(location);
        var name = Sanitize(className);
        var node = new ClassNode
        {
            Id = name,
            Type = type,
            Label = name,
            Text = type.Length > 0 ? $"{name}&lt;{type}&gt;" : name,
            DomId = string.Create(CultureInfo.InvariantCulture, $"{DomIdPrefix}{name}-{_classCounter}"),
            Location = location,
        };
        _classCounter++;
        _classes.Add(node);
        _classIndex.TryAdd(name, node);
    }

    /// <summary>
    ///     Sets the label of a class (<c>class X["label"]</c>, Mermaid's <c>setClassLabel</c>).
    /// </summary>
    /// <param name="id">The class name as written.</param>
    /// <param name="label">The label.</param>
    /// <param name="location">The location of the class name, for reports.</param>
    /// <exception cref="MermaidSyntaxException">
    ///     Thrown, located, when the label with the generic type of the name is longer than
    ///     <see cref="MermaidLimits.MaxLabelLength"/>.
    /// </exception>
    public void SetClassLabel(string id, string label, MermaidLocation location)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(label);
        var sanitized = label.Length > 0 ? Sanitize(label) : label;
        var node = RequireClass(SplitClassNameAndType(Sanitize(id)).ClassName, "setting 'label'", location);

        // The drawn name joins the label and the generic type of the name, two texts checked apart
        var text = node.Type.Length > 0 ? $"{sanitized}<{node.Type}>" : sanitized;
        CheckLabel(text, location);
        node.Label = sanitized;
        node.Text = text;
    }

    /// <summary>
    ///     Adds an annotation to a class (Mermaid's <c>addAnnotation</c>); only the first one is drawn.
    /// </summary>
    /// <param name="className">The class name as written.</param>
    /// <param name="annotation">The annotation, without the angle quotes.</param>
    /// <param name="location">The location of the class name, for the error.</param>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when the class does not exist, where Mermaid fails.</exception>
    public void AddAnnotation(string className, string annotation, MermaidLocation location)
    {
        ArgumentNullException.ThrowIfNull(className);
        ArgumentNullException.ThrowIfNull(annotation);
        RequireClass(SplitClassNameAndType(className).ClassName, "reading 'annotations'", location).Annotations.Add(annotation);
    }

    /// <summary>
    ///     Adds a member to a class, adding the class first (Mermaid's <c>addMember</c>): <c>&lt;&lt;x&gt;&gt;</c> is an
    ///     annotation, a text with a <c>)</c> after its first character is a method, and any other text that is not empty
    ///     is an attribute.
    /// </summary>
    /// <param name="className">The class name as written.</param>
    /// <param name="member">The member text.</param>
    /// <param name="location">The location of the statement, for reports.</param>
    /// <exception cref="MermaidSyntaxException">
    ///     Thrown, located, when a limit is exceeded or a method cannot be read, where Mermaid fails.
    /// </exception>
    public void AddMember(string className, string member, MermaidLocation location)
    {
        ArgumentNullException.ThrowIfNull(className);
        ArgumentNullException.ThrowIfNull(member);
        AddClass(className, location);
        var node = RequireClass(SplitClassNameAndType(className).ClassName, "reading 'annotations'", location);
        var text = Preprocessor.TrimJs(member);
        if (text.StartsWith("<<", StringComparison.Ordinal) && text.EndsWith(">>", StringComparison.Ordinal))
        {
            node.Annotations.Add(Sanitize(text[2..^2]));
        }
        else if (text.IndexOf(')', StringComparison.Ordinal) > 0)
        {
            node.Methods.Add(ClassMember.Create(text, isMethod: true, location));
        }
        else if (text.Length > 0)
        {
            node.Members.Add(ClassMember.Create(text, isMethod: false, location));
        }
    }

    /// <summary>
    ///     Adds the lines of a class body in order (Mermaid's <c>addMembers</c>).
    /// </summary>
    /// <param name="className">The class name as written.</param>
    /// <param name="members">The lines.</param>
    /// <param name="location">The location of the class, for reports.</param>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when a method cannot be read, where Mermaid fails.</exception>
    public void AddMembers(string className, IEnumerable<string> members, MermaidLocation location)
    {
        ArgumentNullException.ThrowIfNull(members);
        foreach (var member in members)
        {
            AddMember(className, member, location);
        }
    }

    /// <summary>
    ///     Adds a relation (Mermaid's <c>addRelation</c>): both classes are added, except that a lollipop end with no end
    ///     at the other side adds an interface node in place of its class.
    /// </summary>
    /// <param name="input">The relation as the grammar reads it.</param>
    /// <param name="location">The location of the statement, for reports.</param>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when a limit is exceeded.</exception>
    public void AddRelation(ClassRelationInput input, MermaidLocation location)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (_relations.Count >= _limits.MaxEdges)
        {
            throw new MermaidSyntaxException(
                MermaidDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"the diagram has more than {_limits.MaxEdges} relations (MermaidLimits.MaxEdges)"),
                location,
                "split the diagram, or raise MermaidLimits.MaxEdges when the source is trusted");
        }

        var id1 = input.Id1;
        var id2 = input.Id2;
        var relation = input.Relation;
        if (relation.Type1 == Lollipop && relation.Type2 is null)
        {
            AddClass(id2, location);
            id1 = AddInterface(id1, id2, location);
        }
        else if (relation.Type2 == Lollipop && relation.Type1 is null)
        {
            AddClass(id1, location);
            id2 = AddInterface(id2, id1, location);
        }
        else
        {
            AddClass(id1, location);
            AddClass(id2, location);
        }

        // The ends are the class ids AddClass gives (sanitized, the generic type split off), as Mermaid's addRelation
        // sets them (packages/mermaid/src/diagrams/class/classDb.ts:239-240), so Box~T~ --> Other starts at Box
        _relations.Add(new ClassRelation
        {
            Id1 = SplitClassNameAndType(id1).ClassName,
            Id2 = SplitClassNameAndType(id2).ClassName,
            RelationTitle1 = Sanitize(Preprocessor.TrimJs(input.Title1)),
            RelationTitle2 = Sanitize(Preprocessor.TrimJs(input.Title2)),
            Title = input.Title,
            Relation = relation,
            Location = location,
        });
    }

    /// <summary>
    ///     Adds a note (Mermaid's <c>addNote</c>), for a class or free.
    /// </summary>
    /// <param name="text">The text, as written.</param>
    /// <param name="className">The class name as written, or null for a free note.</param>
    /// <param name="location">The location of the statement, for reports.</param>
    /// <returns>The note id (<c>note</c> and its index).</returns>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when <see cref="MermaidLimits.MaxNodes"/> is exceeded.</exception>
    public string AddNote(string text, string? className, MermaidLocation location)
    {
        ArgumentNullException.ThrowIfNull(text);
        CheckNodeCount(location);
        var index = _notes.Count;
        // The class name is kept as written, not split or sanitized: Mermaid's addNote stores it so
        // (packages/mermaid/src/diagrams/class/classDb.ts:306-316), and getData looks it up as is (see GetData)
        var note = new ClassNote
        {
            Id = string.Create(CultureInfo.InvariantCulture, $"note{index}"),
            Class = className,
            Text = text,
            Index = index,
            Location = location,
        };
        _notes.Add(note);
        return note.Id;
    }

    /// <summary>
    ///     Adds a CSS class to classes that exist (<c>:::</c> and <c>cssClass</c>, Mermaid's <c>setCssClass</c>).
    /// </summary>
    /// <param name="ids">The class names, separated by commas (not trimmed); a name that starts with a digit gets the
    ///     <c>classId-</c> prefix first, so it matches no class.</param>
    /// <param name="cssClass">The CSS class.</param>
    /// <param name="location">The location of the statement.</param>
    /// <exception cref="MermaidSyntaxException">
    ///     Thrown, located, when the CSS classes of a class get longer than <see cref="MermaidLimits.MaxLabelLength"/>.
    /// </exception>
    public void SetCssClass(string ids, string cssClass, MermaidLocation location)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(cssClass);
        foreach (var id in ids.Split(','))
        {
            if (FindClass(PrefixDigitId(id)) is { } node)
            {
                // A class named again (A,A,...) gets the CSS class again, as in Mermaid, so the joined text is checked
                CheckLabelLength(node.CssClasses.Length + 1L + cssClass.Length, location);
                node.CssClasses += " " + cssClass;
            }
        }
    }

    /// <summary>
    ///     Defines style classes (<c>classDef</c>, Mermaid's <c>defineClass</c>): each keeps its styles (and as text styles
    ///     those that mention <c>color</c>, with <c>fill</c> written <c>bgFill</c>), and the classes whose CSS classes
    ///     already contain its name get its styles, split at commas.
    /// </summary>
    /// <param name="ids">The style class names.</param>
    /// <param name="styles">The styles.</param>
    /// <param name="location">The location of the statement.</param>
    /// <exception cref="MermaidSyntaxException">
    ///     Thrown, located, when the styles of a style class or of a class, joined with semicolons, get longer than
    ///     <see cref="MermaidLimits.MaxLabelLength"/>.
    /// </exception>
    public void DefineClass(IReadOnlyList<string> ids, IReadOnlyList<string> styles, MermaidLocation location)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(styles);
        foreach (var id in ids)
        {
            if (!_styleClassIndex.TryGetValue(id, out var styleClass))
            {
                styleClass = new ClassStyleClass { Id = id };
                _styleClassIndex.Add(id, styleClass);
                _styleClasses.Add(styleClass);
            }

            // A name listed again (c,c,...) gets the styles again, as in Mermaid, so the joined styles are checked
            foreach (var style in styles)
            {
                if (style.Contains("color", StringComparison.Ordinal))
                {
                    var textStyle = ReplaceFirst(style, "fill", "bgFill");
                    styleClass.TextStylesLength += JoinedLength(styleClass.TextStyles.Count, textStyle);
                    CheckLabelLength(styleClass.TextStylesLength, location);
                    styleClass.TextStyles.Add(textStyle);
                }

                styleClass.StylesLength += JoinedLength(styleClass.Styles.Count, style);
                CheckLabelLength(styleClass.StylesLength, location);
                styleClass.Styles.Add(style);
            }

            // The name is matched as a substring of the CSS classes, as in Mermaid
            foreach (var node in _classes.Where(node => node.CssClasses.Contains(id, StringComparison.Ordinal)))
            {
                AddStyles(node, styles.SelectMany(style => style.Split(',')), location);
            }
        }
    }

    /// <summary>
    ///     Adds styles to a class that exists (<c>style</c>, Mermaid's <c>setCssStyle</c>), split at commas.
    /// </summary>
    /// <param name="id">The class name as written.</param>
    /// <param name="styles">The styles.</param>
    /// <param name="location">The location of the statement.</param>
    /// <exception cref="MermaidSyntaxException">
    ///     Thrown, located, when the styles of the class, joined with semicolons, get longer than
    ///     <see cref="MermaidLimits.MaxLabelLength"/>.
    /// </exception>
    public void SetCssStyle(string id, IReadOnlyList<string> styles, MermaidLocation location)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(styles);
        if (!_classIndex.TryGetValue(id, out var node))
        {
            return;
        }

        AddStyles(node, styles.SelectMany(style => style.Split(',')), location);
    }

    /// <summary>
    ///     Adds styles to a class, checking their running length joined with semicolons (as Mermaid's
    ///     <c>styles2String</c> joins them for the shape) against <see cref="MermaidLimits.MaxLabelLength"/>.
    /// </summary>
    /// <param name="node">The class.</param>
    /// <param name="styles">The styles in order.</param>
    /// <param name="location">The location of the statement.</param>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when the joined styles get too long.</exception>
    private void AddStyles(ClassNode node, IEnumerable<string> styles, MermaidLocation location)
    {
        foreach (var style in styles)
        {
            node.StylesLength += JoinedLength(node.Styles.Count, style);
            CheckLabelLength(node.StylesLength, location);
            node.Styles.Add(style);
        }
    }

    /// <summary>
    ///     Gets the length a text adds to a list of texts joined with one-character separators.
    /// </summary>
    /// <param name="count">The number of texts in the list before.</param>
    /// <param name="text">The text.</param>
    /// <returns>The length of the text, and of its separator when the list is not empty.</returns>
    private static long JoinedLength(int count, string text) => (count > 0 ? 1 : 0) + (long)text.Length;

    /// <summary>
    ///     Records a link on classes that exist (<c>link</c> and <c>click ... href</c>, Mermaid's <c>setLink</c>) and gives
    ///     them the <c>clickable</c> CSS class. The link is checked as Mermaid's <c>strict</c> security level does
    ///     (<see cref="UrlSanitizer.FormatUrl(string)"/>) and only the checked link is kept; it is never drawn.
    /// </summary>
    /// <param name="ids">The class names, separated by commas.</param>
    /// <param name="link">The link as written.</param>
    /// <param name="target">The link target, or null for <c>_blank</c>.</param>
    /// <param name="location">The location of the statement.</param>
    /// <exception cref="MermaidSyntaxException">
    ///     Thrown, located, when the CSS classes of a class get longer than <see cref="MermaidLimits.MaxLabelLength"/>.
    /// </exception>
    public void SetLink(string ids, string link, string? target, MermaidLocation location)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(link);
        foreach (var id in ids.Split(','))
        {
            if (FindClass(PrefixDigitId(id)) is { } node)
            {
                node.Link = UrlSanitizer.FormatUrl(link);
                node.LinkTarget = target is null ? "_blank" : Sanitize(target);
            }
        }

        SetCssClass(ids, "clickable", location);
    }

    /// <summary>
    ///     Records a callback on classes that exist (<c>callback</c> and <c>click ... call</c>, Mermaid's
    ///     <c>setClickEvent</c>) and gives them the <c>clickable</c> CSS class. No function is ever bound.
    /// </summary>
    /// <param name="ids">The class names, separated by commas.</param>
    /// <param name="function">The function name.</param>
    /// <param name="location">The location of the statement.</param>
    /// <param name="arguments">The function arguments, or null.</param>
    /// <exception cref="MermaidSyntaxException">
    ///     Thrown, located, when the CSS classes of a class get longer than <see cref="MermaidLimits.MaxLabelLength"/>.
    /// </exception>
    public void SetClickEvent(string ids, string function, MermaidLocation location, string? arguments = null)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(function);
        foreach (var id in ids.Split(','))
        {
            if (FindClass(id) is { } node)
            {
                node.HaveCallback = true;
            }
        }

        SetCssClass(ids, "clickable", location);
    }

    /// <summary>
    ///     Records a tooltip on classes that exist (Mermaid's <c>setTooltip</c>, sanitized); it is never drawn.
    /// </summary>
    /// <param name="ids">The class names, separated by commas.</param>
    /// <param name="tooltip">The tooltip, or null for none (nothing changes).</param>
    public void SetTooltip(string ids, string? tooltip)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (tooltip is null)
        {
            return;
        }

        foreach (var id in ids.Split(','))
        {
            if (FindClass(id) is { } node)
            {
                node.Tooltip = Sanitize(tooltip);
            }
        }
    }

    /// <summary>
    ///     Adds a namespace and enters it (Mermaid's <c>addNamespace</c>): its id is qualified by the enclosing namespace,
    ///     the ancestors of a dotted id are created as implicit namespaces, and declaring a namespace again makes it
    ///     explicit and may relabel it.
    /// </summary>
    /// <param name="id">The namespace name as written.</param>
    /// <param name="label">The label, or null.</param>
    /// <param name="location">The location of the name, for reports.</param>
    /// <returns>The qualified id.</returns>
    /// <exception cref="MermaidSyntaxException">
    ///     Thrown, located, when <see cref="MermaidLimits.MaxNodes"/> is exceeded, the qualified id is longer than
    ///     <see cref="MermaidLimits.MaxLabelLength"/>, or it has more parts than <see cref="MermaidLimits.MaxNestingDepth"/>.
    /// </exception>
    public string AddNamespace(string id, string? label, MermaidLocation location)
    {
        ArgumentNullException.ThrowIfNull(id);

        // The qualified id joins the names of the enclosing namespaces; it is the drawn label when
        // class.hierarchicalNamespaces is false, which the site configuration may set after parsing
        var qualifiedId = _namespaceStack.Count > 0 ? _namespaceStack[^1] + "." + id : id;
        CheckLabel(qualifiedId, location);
        CheckDepth(qualifiedId, location);
        _namespaceStack.Add(qualifiedId);
        if (_namespaceIndex.TryGetValue(qualifiedId, out var existing))
        {
            existing.Explicit = true;
            if (!string.IsNullOrEmpty(label))
            {
                existing.Label = label;
            }

            return qualifiedId;
        }

        // The namespace and its implicit ancestors, outermost first
        var parts = qualifiedId.Split('.');
        var partEnd = -1;
        string? parentId = null;
        for (var i = 0; i < parts.Length; i++)
        {
            // The ancestor id is the qualified id up to the end of this part
            partEnd += parts[i].Length + 1;
            var ancestorId = qualifiedId[..partEnd];
            if (i > 0)
            {
                parentId = qualifiedId[..(partEnd - parts[i].Length - 1)];
            }

            var isLeaf = i == parts.Length - 1;
            if (!_namespaceIndex.TryGetValue(ancestorId, out var current))
            {
                CheckNodeCount(location);
                current = new ClassNamespace
                {
                    Id = ancestorId,
                    Label = isLeaf && !string.IsNullOrEmpty(label) ? label : parts[i],
                    DomId = string.Create(CultureInfo.InvariantCulture, $"{DomIdPrefix}{ancestorId}-{_namespaceCounter++}"),
                    Parent = parentId,
                    Explicit = isLeaf,
                    Location = location,
                };
                _namespaceIndex.Add(ancestorId, current);
                _namespaces.Add(current);
            }
            else if (isLeaf)
            {
                current.Explicit = true;
            }

            if (parentId is not null)
            {
                var parent = _namespaceIndex[parentId];
                parent.AddChild(ancestorId);
                current.Parent ??= parentId;
            }
        }

        return qualifiedId;
    }

    /// <summary>
    ///     Leaves the namespace entered last (Mermaid's <c>popNamespace</c>).
    /// </summary>
    public void PopNamespace()
    {
        if (_namespaceStack.Count > 0)
        {
            _namespaceStack.RemoveAt(_namespaceStack.Count - 1);
        }
    }

    /// <summary>
    ///     Puts the classes and notes declared in a namespace into it (Mermaid's <c>addClassesToNamespace</c>).
    /// </summary>
    /// <param name="id">The qualified namespace id.</param>
    /// <param name="classNames">The class names as written in the namespace.</param>
    /// <param name="noteIds">The note ids.</param>
    public void AddClassesToNamespace(string id, IEnumerable<string> classNames, IEnumerable<string> noteIds)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(classNames);
        ArgumentNullException.ThrowIfNull(noteIds);
        if (!_namespaceIndex.TryGetValue(id, out var space))
        {
            return;
        }

        foreach (var name in classNames)
        {
            var className = SplitClassNameAndType(name).ClassName;
            var node = RequireClass(className, "setting 'parent'", default);
            node.Parent = id;
            space.AddClass(className);
        }

        foreach (var noteId in noteIds)
        {
            var note = _notes.First(n => n.Id == noteId);
            note.Parent = id;
            space.AddNote(noteId);
        }
    }

    /// <summary>
    ///     Splits a class name with a generic type (<c>Name~T~</c>) into the name and the type (Mermaid's
    ///     <c>splitClassNameAndType</c>, which sanitizes both).
    /// </summary>
    /// <param name="id">The name as written.</param>
    /// <returns>The class name and the generic type (empty when there is none).</returns>
    internal static (string ClassName, string Type) SplitClassNameAndType(string id)
    {
        var sanitized = Sanitize(id);
        if (sanitized.IndexOf('~', StringComparison.Ordinal) > 0)
        {
            var parts = sanitized.Split('~');
            return (Sanitize(parts[0]), Sanitize(parts[1]));
        }

        return (sanitized, string.Empty);
    }

    /// <summary>
    ///     Writes generics in TypeScript syntax (Mermaid's <c>parseGenericTypes</c>): matching tildes become angle brackets,
    ///     outermost first, with the parts of <c>~K, V~</c> joined back across the comma.
    /// </summary>
    /// <param name="input">The text.</param>
    /// <returns>The text with angle brackets.</returns>
    internal static string ParseGenericTypes(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        // input.split(/(,)/): the parts and the commas between them
        var sets = new List<string>();
        var start = 0;
        for (var i = 0; i < input.Length; i++)
        {
            if (input[i] == ',')
            {
                sets.Add(input[start..i]);
                sets.Add(",");
                start = i + 1;
            }
        }

        sets.Add(input[start..]);

        // A comma between two parts with one tilde each joins them (the "~K, V~" case)
        var output = new List<string>();
        var index = 0;
        while (index < sets.Count)
        {
            var set = sets[index];
            if (set == "," && index > 0 && index + 1 < sets.Count && CountTildes(sets[index - 1]) == 1 && CountTildes(sets[index + 1]) == 1)
            {
                set = sets[index - 1] + "," + sets[index + 1];
                index++;
                output.RemoveAt(output.Count - 1);
            }

            output.Add(ProcessGenericSet(set));
            index++;
        }

        return string.Concat(output);
    }

    /// <summary>
    ///     Sanitizes a text as Mermaid's <c>sanitizeText</c> does (an empty text stays as it is).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The sanitized text.</returns>
    internal static string Sanitize(string text) => text.Length == 0 ? text : HtmlSanitizer.Sanitize(text);

    /// <summary>
    ///     Replaces the first occurrence of a text (JavaScript's <c>replace</c> with a string pattern).
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
    ///     Counts the tildes of a text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The count.</returns>
    private static int CountTildes(string text) => text.Count(c => c == '~');

    /// <summary>
    ///     Converts the matching tildes of one part of a generic text to angle brackets (Mermaid's <c>processSet</c>).
    /// </summary>
    /// <param name="input">The part.</param>
    /// <returns>The converted part.</returns>
    private static string ProcessGenericSet(string input)
    {
        var tildes = CountTildes(input);
        if (tildes <= 1)
        {
            return input;
        }

        // An odd count with a leading tilde keeps that tilde as it is
        var leadingTilde = tildes % 2 != 0 && input.StartsWith('~');
        var chars = (leadingTilde ? input[1..] : input).ToCharArray();
        var first = Array.IndexOf(chars, '~');
        var last = Array.LastIndexOf(chars, '~');
        while (first != -1 && last != -1 && first != last)
        {
            chars[first] = '<';
            chars[last] = '>';
            first = Array.IndexOf(chars, '~');
            last = Array.LastIndexOf(chars, '~');
        }

        return (leadingTilde ? "~" : string.Empty) + new string(chars);
    }

    /// <summary>
    ///     Gives an id that starts with a digit the <c>classId-</c> prefix, as <c>setCssClass</c> and <c>setLink</c> do.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <returns>The id, prefixed when it starts with a digit.</returns>
    private static string PrefixDigitId(string id) => id.Length > 0 && char.IsAsciiDigit(id[0]) ? DomIdPrefix + id : id;

    /// <summary>
    ///     Finds a class by a name as written (its generic type ignored).
    /// </summary>
    /// <param name="id">The name.</param>
    /// <returns>The class, or null.</returns>
    private ClassNode? FindClass(string id) => _classIndex.GetValueOrDefault(SplitClassNameAndType(id).ClassName);

    /// <summary>
    ///     Gets a class that Mermaid's database dereferences without a check, and fails as Mermaid does when it does not
    ///     exist.
    /// </summary>
    /// <param name="className">The class name.</param>
    /// <param name="access">What Mermaid's JavaScript does with it (for the message, for example <c>reading 'annotations'</c>).</param>
    /// <param name="location">The location of the statement.</param>
    /// <returns>The class.</returns>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when the class does not exist.</exception>
    private ClassNode RequireClass(string className, string access, MermaidLocation location) =>
        _classIndex.TryGetValue(className, out var node)
            ? node
            : throw new MermaidSyntaxException(
                MermaidDiagnosticCodes.SyntaxError,
                $"the class '{className}' is not defined: Cannot read properties of undefined ({access})",
                location,
                "declare the class (class Name) before the statement that annotates it");

    /// <summary>
    ///     Checks that one more node (class, note or interface) or subgraph (namespace) fits
    ///     <see cref="MermaidLimits.MaxNodes"/>, which counts them together as the flowchart model counts vertices and
    ///     subgraphs.
    /// </summary>
    /// <param name="location">The location of the statement that adds it.</param>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when it does not.</exception>
    private void CheckNodeCount(MermaidLocation location)
    {
        if (_classes.Count + _namespaces.Count + _notes.Count + _interfaces.Count >= _limits.MaxNodes)
        {
            throw new MermaidSyntaxException(
                MermaidDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"the diagram has more than {_limits.MaxNodes} nodes and subgraphs (MermaidLimits.MaxNodes)"),
                location,
                "split the diagram, or raise MermaidLimits.MaxNodes when the source is trusted");
        }
    }

    /// <summary>
    ///     Checks a drawn text the model joins from texts the parser checked apart against
    ///     <see cref="MermaidLimits.MaxLabelLength"/>, as the flowchart model checks its labels.
    /// </summary>
    /// <param name="text">The joined text.</param>
    /// <param name="location">The location of the statement.</param>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when the text is too long.</exception>
    private void CheckLabel(string text, MermaidLocation location) => CheckLabelLength(text.Length, location);

    /// <summary>
    ///     Checks the length of a text the model joins (a label, the CSS classes or the styles of a class, the styles of a
    ///     style class) against <see cref="MermaidLimits.MaxLabelLength"/>.
    /// </summary>
    /// <param name="length">The length of the joined text.</param>
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

    /// <summary>
    ///     Checks the depth of a namespace against <see cref="MermaidLimits.MaxNestingDepth"/>: each part of its qualified
    ///     id is a level, whether it comes from a nested <c>namespace</c> keyword or a dot in a name, because each part is
    ///     a namespace (an implicit ancestor or the namespace itself) and each level a nested cluster to lay out.
    /// </summary>
    /// <param name="qualifiedId">The qualified id.</param>
    /// <param name="location">The location of the name.</param>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when the namespace is too deep.</exception>
    private void CheckDepth(string qualifiedId, MermaidLocation location)
    {
        if (qualifiedId.AsSpan().Count('.') + 1 > _limits.MaxNestingDepth)
        {
            throw new MermaidSyntaxException(
                MermaidDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"namespaces are nested deeper than {_limits.MaxNestingDepth} levels (MermaidLimits.MaxNestingDepth)"),
                location,
                "nest fewer namespaces or use fewer dots in namespace names, or raise MermaidLimits.MaxNestingDepth when the source is trusted");
        }
    }

    /// <summary>
    ///     Adds a lollipop interface node (Mermaid's <c>addInterface</c>).
    /// </summary>
    /// <param name="label">The interface name as written.</param>
    /// <param name="classId">The class name as written on the other side.</param>
    /// <param name="location">The location of the relation.</param>
    /// <returns>The interface id (<c>interface</c> and its index).</returns>
    private string AddInterface(string label, string classId, MermaidLocation location)
    {
        CheckNodeCount(location);
        var id = string.Create(CultureInfo.InvariantCulture, $"interface{_interfaces.Count}");
        _interfaces.Add(new ClassInterface(id, label, classId, location));
        return id;
    }
}

/// <summary>
///     A class of the model (Mermaid's <c>ClassNode</c>).
/// </summary>
/// <remarks>
///     The database changes a class while the text is parsed (labels, CSS classes, styles, members, the namespace and the
///     link data); once parsing is over it is never changed.
/// </remarks>
internal sealed class ClassNode
{
    /// <summary>Gets the class name (the id).</summary>
    public required string Id { get; init; }

    /// <summary>Gets the generic type of the name (<c>T</c> of <c>Name~T~</c>), or empty.</summary>
    public required string Type { get; init; }

    /// <summary>Gets or sets the label (the name, or the text of <c>class X["label"]</c>).</summary>
    public required string Label { get; set; }

    /// <summary>Gets or sets the text drawn as the name: the label with its generic type.</summary>
    public required string Text { get; set; }

    /// <summary>Gets or sets the CSS classes, separated by spaces (<c>default</c> first).</summary>
    public string CssClasses { get; set; } = "default";

    /// <summary>Gets the methods in order.</summary>
    public List<ClassMember> Methods { get; } = [];

    /// <summary>Gets the attributes in order.</summary>
    public List<ClassMember> Members { get; } = [];

    /// <summary>Gets the annotations in order, without the angle quotes.</summary>
    public List<string> Annotations { get; } = [];

    /// <summary>Gets the styles of <c>style</c> statements and of style classes, in order.</summary>
    public List<string> Styles { get; } = [];

    /// <summary>Gets or sets the length of <see cref="Styles"/> joined with semicolons.</summary>
    public long StylesLength { get; set; }

    /// <summary>Gets the DOM id (<c>classId-Name-N</c>).</summary>
    public required string DomId { get; init; }

    /// <summary>Gets or sets the qualified id of the namespace that declares the class, or null.</summary>
    public string? Parent { get; set; }

    /// <summary>Gets or sets the checked link, or null.</summary>
    public string? Link { get; set; }

    /// <summary>Gets or sets the link target, or null.</summary>
    public string? LinkTarget { get; set; }

    /// <summary>Gets or sets a value indicating whether a callback was recorded, or null when none was.</summary>
    public bool? HaveCallback { get; set; }

    /// <summary>Gets or sets the tooltip, or null.</summary>
    public string? Tooltip { get; set; }

    /// <summary>Gets the location of the class's first mention, for reports.</summary>
    public MermaidLocation Location { get; init; }
}

/// <summary>
///     An attribute or a method of a class: a port of Mermaid's <c>ClassMember</c> (<c>classTypes.ts</c>).
/// </summary>
/// <remarks>
///     <para>
///         <b>Methods</b> are read with Mermaid's <c>/([#+~-])?(.+)\((.*)\)([\s$*])?(.*)([$*])?/</c>: the visibility, the
///         name up to the last <c>(</c> that a <c>)</c> follows, the parameters up to the last <c>)</c>, a classifier or a
///         space, and the return type, whose last character is the classifier when none came before it.
///         <b>Attributes</b> take the visibility from their first character and the classifier from their last. The
///         classifier is <c>*</c> (abstract, drawn italic) or <c>$</c> (static, drawn underlined).
///     </para>
///     <para>
///         The instance is immutable.
///     </para>
/// </remarks>
internal sealed class ClassMember
{
    /// <summary>The visibility characters.</summary>
    private const string Visibilities = "#+~-";

    /// <summary>Gets the name (Mermaid's <c>id</c>).</summary>
    public required string Id { get; init; }

    /// <summary>Gets the member type: <c>method</c> or <c>attribute</c>.</summary>
    public required string MemberType { get; init; }

    /// <summary>Gets the visibility: <c>+</c>, <c>-</c>, <c>#</c>, <c>~</c> or empty.</summary>
    public required string Visibility { get; init; }

    /// <summary>Gets the classifier: <c>*</c>, <c>$</c> or empty.</summary>
    public required string Classifier { get; init; }

    /// <summary>Gets the parameters of a method, or null for an attribute.</summary>
    public string? Parameters { get; init; }

    /// <summary>Gets the return type of a method, or null for an attribute.</summary>
    public string? ReturnType { get; init; }

    /// <summary>Gets the text the class box draws (Mermaid's <c>text</c>: escaped visibility, angle brackets as entities).</summary>
    public required string Text { get; init; }

    /// <summary>
    ///     Gets the display text (the text of Mermaid's <c>getDisplayDetails</c>): visibility, name, parameters and return
    ///     type with generics in angle brackets.
    /// </summary>
    public string DisplayText
    {
        get
        {
            var text = new StringBuilder(Visibility).Append(ClassModel.ParseGenericTypes(Id));
            if (MemberType == "method")
            {
                text.Append('(').Append(ClassModel.ParseGenericTypes(Preprocessor.TrimJs(Parameters ?? string.Empty))).Append(')');
                if (!string.IsNullOrEmpty(ReturnType))
                {
                    text.Append(" : ").Append(ClassModel.ParseGenericTypes(ReturnType));
                }
            }

            return Preprocessor.TrimJs(text.ToString());
        }
    }

    /// <summary>
    ///     Gets the style of the classifier (Mermaid's <c>parseClassifier</c>): italic for <c>*</c>, underlined for
    ///     <c>$</c>, else empty.
    /// </summary>
    public string CssStyle => Classifier switch
    {
        "*" => "font-style:italic;",
        "$" => "text-decoration:underline;",
        _ => string.Empty,
    };

    /// <summary>
    ///     Reads a member (Mermaid's <c>ClassMember</c> constructor): the text is sanitized, then split.
    /// </summary>
    /// <param name="input">The member text, trimmed.</param>
    /// <param name="isMethod">Whether the database took it for a method (it has a <c>)</c> after its first character).</param>
    /// <param name="location">The location of the statement, for the error.</param>
    /// <returns>The member.</returns>
    /// <exception cref="MermaidSyntaxException">
    ///     Thrown, located, when a method does not match Mermaid's method expression, where Mermaid fails.
    /// </exception>
    public static ClassMember Create(string input, bool isMethod, MermaidLocation location)
    {
        var text = ClassModel.Sanitize(input);
        var visibility = string.Empty;
        string classifier;
        string id;
        string? parameters = null;
        string? returnType = null;
        if (isMethod)
        {
            if (!MatchMethod(text, out var match))
            {
                throw new MermaidSyntaxException(
                    MermaidDiagnosticCodes.SyntaxError,
                    $"the method '{text}' cannot be read: Cannot read properties of undefined (reading 'startsWith')",
                    location,
                    "write a method as name(parameters) with an optional return type, for example +area(double scale) double");
            }

            visibility = match.Visibility;
            id = match.Name;
            parameters = Preprocessor.TrimJs(match.Parameters);
            classifier = Preprocessor.TrimJs(match.Classifier);
            returnType = Preprocessor.TrimJs(match.Rest);
            if (classifier.Length == 0 && returnType.Length > 0 && returnType[^1] is '$' or '*')
            {
                classifier = returnType[^1..];
                returnType = returnType[..^1];
            }
        }
        else
        {
            if (text.Length > 0 && Visibilities.Contains(text[0], StringComparison.Ordinal))
            {
                visibility = text[..1];
            }

            classifier = text.Length > 0 && text[^1] is '$' or '*' ? text[^1..] : string.Empty;
            var start = visibility.Length;
            var end = text.Length - classifier.Length;
            id = start <= end ? text[start..end] : text[end..start];
        }

        // Keep one leading space only
        id = id.StartsWith(' ') ? " " + Preprocessor.TrimJs(id) : Preprocessor.TrimJs(id);

        // The drawn text: an escaped visibility, generics in angle brackets, then the angle brackets as entities
        var drawn = (visibility.Length > 0 ? "\\" + visibility : string.Empty) + ClassModel.ParseGenericTypes(id);
        if (isMethod)
        {
            drawn += "(" + ClassModel.ParseGenericTypes(parameters!) + ")" +
                (returnType!.Length > 0 ? " : " + ClassModel.ParseGenericTypes(returnType) : string.Empty);
        }

        drawn = drawn.Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);
        if (drawn.StartsWith("\\&lt;", StringComparison.Ordinal))
        {
            drawn = "~" + drawn[5..];
        }

        return new ClassMember
        {
            Id = id,
            MemberType = isMethod ? "method" : "attribute",
            Visibility = visibility,
            Classifier = classifier,
            Parameters = parameters,
            ReturnType = returnType,
            Text = drawn,
        };
    }

    /// <summary>
    ///     Matches Mermaid's method expression <c>/([#+~-])?(.+)\((.*)\)([\s$*])?(.*)([$*])?/</c> as JavaScript does: the
    ///     first start that matches, greedy groups, the visibility taken when that still matches.
    /// </summary>
    /// <param name="text">The member text.</param>
    /// <param name="match">The groups of the match.</param>
    /// <returns>Whether it matches.</returns>
    private static bool MatchMethod(string text, out MethodMatch match)
    {
        // A failed start fails for every later start of its line, so only the start of each line is tried
        var start = 0;
        while (start < text.Length)
        {
            var lineEnd = start;
            while (lineEnd < text.Length && !ScanPatterns.IsJsLineTerminator(text[lineEnd]))
            {
                lineEnd++;
            }

            var close = text.LastIndexOf(')', Math.Max(lineEnd - 1, 0), lineEnd - start);
            var hasVisibility = Visibilities.Contains(text[start], StringComparison.Ordinal);
            foreach (var body in hasVisibility ? new[] { start + 1, start } : [start])
            {
                // (.+)\( : the last '(' after at least one character and before the last ')'
                var open = close > body + 1 ? text.LastIndexOf('(', close - 1, close - 1 - body) : -1;
                if (open <= body)
                {
                    continue;
                }

                // ([\s$*])? then (.*) to the end of its line
                var rest = close + 1;
                var classifier = string.Empty;
                if (rest < text.Length && (ScanPatterns.IsJsWhitespace(text[rest]) || text[rest] is '$' or '*'))
                {
                    classifier = text[rest..(rest + 1)];
                    rest++;
                }

                var restEnd = rest;
                while (restEnd < text.Length && !ScanPatterns.IsJsLineTerminator(text[restEnd]))
                {
                    restEnd++;
                }

                match = new MethodMatch(
                    body > start ? text[start..body] : string.Empty,
                    text[body..open],
                    text[(open + 1)..close],
                    classifier,
                    text[rest..restEnd]);
                return true;
            }

            start = lineEnd + 1;
        }

        match = default;
        return false;
    }

    /// <summary>
    ///     The groups of a match of Mermaid's method expression.
    /// </summary>
    /// <param name="Visibility">The visibility, or empty.</param>
    /// <param name="Name">The name.</param>
    /// <param name="Parameters">The parameters, untrimmed.</param>
    /// <param name="Classifier">The character after the parameters when it is white space, <c>$</c> or <c>*</c>, or empty.</param>
    /// <param name="Rest">The rest of the line.</param>
    private readonly record struct MethodMatch(string Visibility, string Name, string Parameters, string Classifier, string Rest);
}

/// <summary>
///     The ends and the line of a relation, as Mermaid's relation and line type codes.
/// </summary>
/// <param name="Type1">The end at the first class (a code of <see cref="ClassModel"/>), or null for none.</param>
/// <param name="Type2">The end at the second class, or null for none.</param>
/// <param name="LineType"><see cref="ClassModel.SolidLine"/> or <see cref="ClassModel.DottedLine"/>.</param>
internal sealed record ClassRelationType(int? Type1, int? Type2, int LineType);

/// <summary>
///     A relation as the grammar reads it, before the database adds it.
/// </summary>
/// <param name="Id1">The first class name as written.</param>
/// <param name="Id2">The second class name as written.</param>
/// <param name="Title1">The first cardinality, or <c>none</c>.</param>
/// <param name="Title2">The second cardinality, or <c>none</c>.</param>
/// <param name="Relation">The ends and the line.</param>
/// <param name="Title">The cleaned label, or null.</param>
internal sealed record ClassRelationInput(string Id1, string Id2, string Title1, string Title2, ClassRelationType Relation, string? Title);

/// <summary>
///     A relation of the model (Mermaid's <c>ClassRelation</c>).
/// </summary>
internal sealed record ClassRelation
{
    /// <summary>Gets the id of the first class or interface.</summary>
    public required string Id1 { get; init; }

    /// <summary>Gets the id of the second class or interface.</summary>
    public required string Id2 { get; init; }

    /// <summary>Gets the first cardinality, sanitized (<c>none</c> for none).</summary>
    public required string RelationTitle1 { get; init; }

    /// <summary>Gets the second cardinality, sanitized (<c>none</c> for none).</summary>
    public required string RelationTitle2 { get; init; }

    /// <summary>Gets the label, or null.</summary>
    public string? Title { get; init; }

    /// <summary>Gets the ends and the line.</summary>
    public required ClassRelationType Relation { get; init; }

    /// <summary>Gets the location of the statement, for reports.</summary>
    public MermaidLocation Location { get; init; }
}

/// <summary>
///     A note of the model (Mermaid's <c>ClassNote</c>).
/// </summary>
/// <remarks>
///     The database sets <see cref="Parent"/> while the text is parsed; once parsing is over the note is never changed.
/// </remarks>
internal sealed class ClassNote
{
    /// <summary>Gets the id (<c>note</c> and its index).</summary>
    public required string Id { get; init; }

    /// <summary>Gets the class name as written, or null for a free note.</summary>
    public string? Class { get; init; }

    /// <summary>Gets the text as written.</summary>
    public required string Text { get; init; }

    /// <summary>Gets the index.</summary>
    public required int Index { get; init; }

    /// <summary>Gets or sets the qualified id of the namespace that declares the note, or null.</summary>
    public string? Parent { get; set; }

    /// <summary>Gets the location of the statement, for reports.</summary>
    public MermaidLocation Location { get; init; }
}

/// <summary>
///     A lollipop interface of the model: the node at the lollipop end of a relation.
/// </summary>
/// <param name="Id">The id (<c>interface</c> and its index).</param>
/// <param name="Label">The interface name as written.</param>
/// <param name="ClassId">The class name as written on the other side.</param>
/// <param name="Location">The location of the relation, for reports.</param>
internal sealed record ClassInterface(string Id, string Label, string ClassId, MermaidLocation Location);

/// <summary>
///     A namespace of the model (Mermaid's <c>NamespaceNode</c>).
/// </summary>
/// <remarks>
///     The database changes a namespace while the text is parsed; once parsing is over it is never changed.
/// </remarks>
internal sealed class ClassNamespace
{
    /// <summary>The class names in insertion order.</summary>
    private readonly List<string> _classes = [];

    /// <summary>The note ids in insertion order.</summary>
    private readonly List<string> _notes = [];

    /// <summary>The child namespace ids in insertion order.</summary>
    private readonly List<string> _children = [];

    /// <summary>Gets the qualified id.</summary>
    public required string Id { get; init; }

    /// <summary>Gets or sets the label: the last part of the id, or the label of the declaration.</summary>
    public required string Label { get; set; }

    /// <summary>Gets the DOM id (<c>classId-Id-N</c>).</summary>
    public required string DomId { get; init; }

    /// <summary>Gets or sets the qualified id of the parent namespace, or null.</summary>
    public string? Parent { get; set; }

    /// <summary>Gets or sets a value indicating whether the namespace was declared, not only implied by a dotted id.</summary>
    public bool Explicit { get; set; }

    /// <summary>Gets the names of the classes declared in it, in order.</summary>
    public IReadOnlyList<string> Classes => _classes;

    /// <summary>Gets the ids of the notes declared in it, in order.</summary>
    public IReadOnlyList<string> Notes => _notes;

    /// <summary>Gets the ids of its child namespaces, in order.</summary>
    public IReadOnlyList<string> Children => _children;

    /// <summary>Gets the location of the first declaration, for reports.</summary>
    public MermaidLocation Location { get; init; }

    /// <summary>
    ///     Adds a class name unless it is there (a JavaScript map keeps the first position of a key).
    /// </summary>
    /// <param name="name">The class name.</param>
    public void AddClass(string name) => AddOnce(_classes, name);

    /// <summary>
    ///     Adds a note id unless it is there.
    /// </summary>
    /// <param name="id">The note id.</param>
    public void AddNote(string id) => AddOnce(_notes, id);

    /// <summary>
    ///     Adds a child namespace id unless it is there.
    /// </summary>
    /// <param name="id">The child id.</param>
    public void AddChild(string id) => AddOnce(_children, id);

    /// <summary>
    ///     Adds an item to a list unless it is there.
    /// </summary>
    /// <param name="list">The list.</param>
    /// <param name="item">The item.</param>
    private static void AddOnce(List<string> list, string item)
    {
        if (!list.Contains(item, StringComparer.Ordinal))
        {
            list.Add(item);
        }
    }
}

/// <summary>
///     A style class of the model (<c>classDef</c>, Mermaid's <c>StyleClass</c>).
/// </summary>
internal sealed class ClassStyleClass
{
    /// <summary>Gets the name.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the styles in order.</summary>
    public List<string> Styles { get; } = [];

    /// <summary>Gets the styles that set a color, with <c>fill</c> written <c>bgFill</c>.</summary>
    public List<string> TextStyles { get; } = [];

    /// <summary>Gets or sets the length of <see cref="Styles"/> joined with semicolons.</summary>
    public long StylesLength { get; set; }

    /// <summary>Gets or sets the length of <see cref="TextStyles"/> joined with semicolons.</summary>
    public long TextStylesLength { get; set; }
}
