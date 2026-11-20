// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.PlantUml.Svek;

namespace DemaConsulting.CanvasNet.PlantUml.Syntax;

/// <content>
///     The detection of the diagram families that PlantUML lays out with Graphviz, tried in the order of PlantUML's
///     factories (<c>PSystemBuilder</c>): the class family (class and object diagrams), the legacy activity diagram,
///     the description family (use case, component, and deployment diagrams), and the state diagram.
/// </content>
internal static partial class DiagramDetector
{
    /// <summary>The keywords that start a statement of the class family.</summary>
    private static readonly HashSet<string> ClassFamilyKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "class", "abstract", "interface", "enum", "annotation", "entity", "exception", "struct", "protocol", "metaclass",
        "stereotype", "record", "dataclass", "static", "circle", "diamond", "object", "map", "json", "package",
        "namespace", "together", "set", "remove", "restore", "allowmixing", "allow_mixing", "url",
    };

    /// <summary>The keywords that declare an element of the description family.</summary>
    private static readonly HashSet<string> DescriptionKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "actor", "usecase", "component", "interface", "node", "artifact", "cloud", "folder", "frame", "storage",
        "rectangle", "card", "file", "stack", "agent", "hexagon", "person", "boundary", "control", "entity",
        "collections", "queue", "database", "label", "port", "portin", "portout", "package", "together", "action",
        "process", "allowmixing", "allow_mixing", "set", "remove", "restore",
    };

    /// <summary>The keywords that name a use case diagram among the description family.</summary>
    private static readonly HashSet<string> UseCaseKeywords = new(StringComparer.OrdinalIgnoreCase) { "actor", "usecase" };

    /// <summary>The keywords that name a component diagram among the description family.</summary>
    private static readonly HashSet<string> ComponentKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "component", "interface", "port", "portin", "portout",
    };

    /// <summary>
    ///     Detects the graph diagram families in PlantUML's order: the class family, the legacy activity diagram, the
    ///     description family, and the state diagram.
    /// </summary>
    /// <param name="statements">The non-empty statements.</param>
    /// <returns>The detected type, or null when no graph family accepts every statement.</returns>
    internal static DetectedDiagram? DetectGraph(IReadOnlyList<SourceStatement> statements)
    {
        // The class family: every statement outside the bodies is a class statement
        var classOuter = ClassOuterStatements(statements);
        if (classOuter.All(s => AcceptsClass(s)))
        {
            var first = classOuter.FirstOrDefault(s => Classify(s.Text) != StatementFamily.Common) ?? classOuter[0];
            var declaration = classOuter.FirstOrDefault(s => ClassFamilyKeywords.Contains(FirstWord(s.Text)) && FirstWord(s.Text) is not ("package" or "namespace" or "together" or "set"));
            var type = declaration is not null && FirstWord(declaration.Text).ToLowerInvariant() is "object" or "map" ? "object" : "class";
            return new DetectedDiagram(type, SupportedTypes.Contains(type), FirstWord(first.Text), first.Location);
        }

        // The legacy activity diagram: a statement with (*) (the graph families have no multi-line action: a line
        // ':x' that is never closed is a statement)
        var outer = OuterStatements(statements, openActionIsText: false);
        if (outer.FirstOrDefault(s => s.Text.Contains("(*)", StringComparison.Ordinal)) is { } legacy)
        {
            return new DetectedDiagram("activity-legacy", SupportedTypes.Contains("activity-legacy"), FirstWord(legacy.Text), legacy.Location);
        }

        // The description family: use case, then component, then deployment by the constructs it uses
        if (outer.All(AcceptsDescription))
        {
            var first = outer.FirstOrDefault(s => Classify(s.Text) != StatementFamily.Common) ?? outer[0];
            var type = "deployment";
            if (outer.Any(IsUseCaseConstruct))
            {
                type = "usecase";
            }
            else if (outer.Any(IsComponentConstruct))
            {
                type = "component";
            }

            return new DetectedDiagram(type, SupportedTypes.Contains(type), FirstWord(first.Text), first.Location);
        }

        // The state diagram
        if (outer.All(AcceptsState) && outer.Any(s => Classify(s.Text) != StatementFamily.Common))
        {
            var first = outer.First(s => Classify(s.Text) != StatementFamily.Common);
            return new DetectedDiagram("state", SupportedTypes.Contains("state"), FirstWord(first.Text), first.Location);
        }

        return null;
    }

    /// <summary>
    ///     Decides whether a statement is a relationship that only the class family draws (so a sequence diagram
    ///     refuses it, as PlantUML's message grammar does): a link whose arrow has a dotted or doubled body, a direction
    ///     word, or a decoration that no message has (an extension, a composition, a circle, ...), or whose ends are not
    ///     participant names; a message to or from the diagram border never is.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <returns>True when it is.</returns>
    internal static bool IsClassOnlyLink(SourceStatement statement)
    {
        ArgumentNullException.ThrowIfNull(statement);

        // A message to or from the border is a sequence statement ('?o-> A' reads as a link with the end '?o')
        if (IsBorderMessage(statement.Text) || !SvekSyntax.TryReadLink(statement, out var link))
        {
            return false;
        }

        static bool MessageDecor(LinkDecor decor) => decor is LinkDecor.None or LinkDecor.Arrow or LinkDecor.ArrowTriangle
            or LinkDecor.Aggregation or LinkDecor.NotNavigable or LinkDecor.HalfArrowUp or LinkDecor.HalfArrowDown;
        static bool MessageName(SvekName name) => name.Kind == SvekNameKind.Quoted
            || (name.Kind == SvekNameKind.Plain && name.Text.All(c => char.IsLetterOrDigit(c) || c is '_' or '.' or '@'));
        var arrow = link.Arrow;
        return arrow.Dotted || arrow.Doubled || arrow.Direction != LinkDirection.None || !MessageDecor(arrow.TailDecor)
            || !MessageDecor(arrow.HeadDecor) || !MessageName(link.From) || !MessageName(link.To);
    }

    /// <summary>
    ///     Gets the statements of a class diagram outside the bodies of its classes, objects, maps, and json blocks
    ///     (whose lines are members, not statements) and outside multi-line text blocks.
    /// </summary>
    /// <param name="statements">The statements.</param>
    /// <returns>The outer statements, the openers and closers of the bodies included.</returns>
    private static List<SourceStatement> ClassOuterStatements(IReadOnlyList<SourceStatement> statements)
    {
        var outer = new List<SourceStatement>();
        var inBody = false;
        foreach (var statement in OuterStatements(statements, openActionIsText: false))
        {
            var text = statement.Text;
            if (inBody)
            {
                if (text == "}")
                {
                    outer.Add(statement);
                    inBody = false;
                }

                continue;
            }

            outer.Add(statement);
            var word = FirstWord(text).ToLowerInvariant();
            inBody = text.EndsWith('{') && ClassFamilyKeywords.Contains(word) && word is not ("package" or "namespace" or "together");
        }

        return outer;
    }

    /// <summary>
    ///     Decides whether a class diagram accepts a statement: a common statement, a declaration of the class family, a
    ///     closing brace, a direction, a member (<c>Name : text</c>), or a relationship between named elements.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <returns>True when it does.</returns>
    private static bool AcceptsClass(SourceStatement statement)
    {
        var text = statement.Text;
        var word = FirstWord(text);
        if (Classify(text) == StatementFamily.Common || text == "}" || ClassFamilyKeywords.Contains(word) || SvekSyntax.TryReadDirection(text, out _))
        {
            return true;
        }

        if (text.Length > 1 && text[0] is '+' or '-' or '#' or '~' && ClassFamilyKeywords.Contains(FirstWord(text[1..])))
        {
            return true;
        }

        if (SvekSyntax.TryReadLink(statement, out var link))
        {
            return IsClassName(link.From) && IsClassName(link.To)
                || (link.From.Kind == SvekNameKind.Parenthesized && link.From.Text.Contains(',', StringComparison.Ordinal));
        }

        // A member: a name, white space, and a colon
        var i = 0;
        if (SvekSyntax.TryReadName(text, ref i, out var name) && IsClassName(name))
        {
            SvekSyntax.SkipSpaces(text, ref i);
            return i < text.Length && text[i] == ':';
        }

        return false;
    }

    /// <summary>
    ///     Decides whether a description diagram accepts a statement: a common statement, a declaration of an element or
    ///     a container, a closing brace, a direction, a use case <c>(x)</c>, a component <c>[x]</c>, an interface
    ///     <c>() x</c>, an actor <c>:x:</c> (each with what follows it: an alias, stereotypes, a color), or a link
    ///     between such elements.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <returns>True when it does.</returns>
    private static bool AcceptsDescription(SourceStatement statement)
    {
        var text = statement.Text;
        if (Classify(text) == StatementFamily.Common || text == "}" || DescriptionKeywords.Contains(FirstWord(text)) || SvekSyntax.TryReadDirection(text, out _))
        {
            return true;
        }

        if (SvekSyntax.TryReadLink(statement, out var link))
        {
            return link.From.Kind is not (SvekNameKind.Star or SvekNameKind.History or SvekNameKind.DeepHistory)
                && link.To.Kind is not (SvekNameKind.Star or SvekNameKind.History or SvekNameKind.DeepHistory);
        }

        return text.StartsWith("()", StringComparison.Ordinal) || (text.StartsWith('(') && text.IndexOf(')', 1) > 1)
            || (text.StartsWith('[') && text.Contains(']', StringComparison.Ordinal) && !IsGantt(text)) || (text.StartsWith(':') && text.IndexOf(':', 1) > 1);
    }

    /// <summary>
    ///     Decides whether a state diagram accepts a statement: a common statement, a state declaration, a closing
    ///     brace, a concurrent region separator, a direction, a description (<c>Name : text</c>), or a transition.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <returns>True when it does.</returns>
    private static bool AcceptsState(SourceStatement statement)
    {
        var text = statement.Text;
        if (Classify(text) == StatementFamily.Common || text == "}" || text is "--" or "||" || SvekSyntax.TryReadDirection(text, out _)
            || FirstWord(text).Equals("state", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (SvekSyntax.TryReadLink(statement, out var link))
        {
            return link.From.Kind is not (SvekNameKind.Parenthesized or SvekNameKind.Bracketed or SvekNameKind.Colon or SvekNameKind.Interface)
                && link.To.Kind is not (SvekNameKind.Parenthesized or SvekNameKind.Bracketed or SvekNameKind.Colon or SvekNameKind.Interface);
        }

        var i = 0;
        if (SvekSyntax.TryReadName(text, ref i, out var name) && name.Kind is SvekNameKind.Plain or SvekNameKind.Quoted)
        {
            SvekSyntax.SkipSpaces(text, ref i);
            return i < text.Length && text[i] == ':';
        }

        return false;
    }

    /// <summary>
    ///     Joins type names as a list in prose: <c>a</c>, <c>a and b</c>, or <c>a, b, and c</c>.
    /// </summary>
    /// <param name="types">The type names.</param>
    /// <returns>The list.</returns>
    internal static string JoinTypes(IReadOnlyList<string> types)
    {
        ArgumentNullException.ThrowIfNull(types);
        return types.Count switch
        {
            0 => string.Empty,
            1 => types[0],
            2 => types[0] + " and " + types[1],
            _ => string.Join(", ", types.Take(types.Count - 1)) + ", and " + types[^1],
        };
    }

    /// <summary>
    ///     Decides whether a name can be an element of a class diagram (a plain or quoted name).
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>True when it can.</returns>
    private static bool IsClassName(SvekName name) => name.Kind is SvekNameKind.Plain or SvekNameKind.Quoted;

    /// <summary>
    ///     Decides whether a statement uses a construct of a use case diagram (<c>actor</c>, <c>usecase</c>,
    ///     <c>(name)</c>, <c>:name:</c>).
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <returns>True when it does.</returns>
    private static bool IsUseCaseConstruct(SourceStatement statement)
    {
        var text = statement.Text;
        if (UseCaseKeywords.Contains(FirstWord(text)) || (text.StartsWith('(') && !text.StartsWith("()", StringComparison.Ordinal)) || text.StartsWith(':'))
        {
            return true;
        }

        return SvekSyntax.TryReadLink(statement, out var link)
            && (link.From.Kind is SvekNameKind.Parenthesized or SvekNameKind.Colon || link.To.Kind is SvekNameKind.Parenthesized or SvekNameKind.Colon);
    }

    /// <summary>
    ///     Decides whether a statement uses a construct of a component diagram (<c>component</c>, <c>interface</c>,
    ///     <c>[name]</c>, <c>() name</c>, ports).
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <returns>True when it does.</returns>
    private static bool IsComponentConstruct(SourceStatement statement)
    {
        var text = statement.Text;
        if (ComponentKeywords.Contains(FirstWord(text)) || text.StartsWith('[') || text.StartsWith("()", StringComparison.Ordinal))
        {
            return true;
        }

        return SvekSyntax.TryReadLink(statement, out var link)
            && (link.From.Kind is SvekNameKind.Bracketed or SvekNameKind.Interface || link.To.Kind is SvekNameKind.Bracketed or SvekNameKind.Interface);
    }
}
