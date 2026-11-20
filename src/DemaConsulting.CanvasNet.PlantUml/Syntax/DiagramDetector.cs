using System.Globalization;

namespace DemaConsulting.CanvasNet.PlantUml.Syntax;

/// <summary>
///     The detected type of a diagram.
/// </summary>
/// <param name="DiagramType">
///     The public type name: <c>sequence</c>, <c>activity</c>, <c>class</c>, <c>object</c>, <c>state</c>,
///     <c>usecase</c>, <c>component</c>, <c>deployment</c>, or a recognized type that is not drawn (<c>timing</c>,
///     <c>activity-legacy</c>, <c>mindmap</c>, <c>gantt</c>, <c>wbs</c>, <c>json</c>, <c>yaml</c>, <c>salt</c>, ...).
/// </param>
/// <param name="IsSupported">Whether this version draws the type.</param>
/// <param name="Keyword">The construct the type was recognized by (an <c>@start</c> tag or a statement keyword).</param>
/// <param name="Location">The location of that construct.</param>
internal sealed record DetectedDiagram(string DiagramType, bool IsSupported, string Keyword, PlantUmlLocation Location);

/// <summary>
///     Detects the type of a diagram from its <c>@start</c> tag and, for <c>@startuml</c>, from its statements, in the
///     order PlantUML's diagram factories try them.
/// </summary>
/// <remarks>
///     <para>
///         A tag other than <c>@startuml</c> names its type directly (<c>@startmindmap</c> is a mindmap, ...); an unknown
///         tag is a <see cref="PlantUmlSyntaxException"/> with <see cref="PlantUmlDiagnosticCodes.UnknownDiagram"/> and a
///         hint that names the nearest known tag. Inside <c>@startuml</c> (or a text without a tag), PlantUML tries the
///         sequence diagram first: when every statement is a sequence statement, the diagram is a sequence diagram;
///         else, when every statement is an activity statement of the new syntax, it is an activity diagram; else the
///         first statement with a keyword that only another type uses (<c>class</c>, <c>usecase</c>, <c>[*]</c>,
///         <c>(*)</c>, <c>component</c>, <c>node</c>, ...) names that type, which is drawn or only recognized; else the
///         text is a syntax error at the first statement that the more likely of the two drawn types does not accept.
///     </para>
///     <para>
///         The statements are recognized by their leading keywords and arrows only; the diagram parser then reads them
///         strictly and reports a located syntax error for a statement it cannot read. Detection runs no regular
///         expression. The class is stateless and safe for concurrent use.
///     </para>
/// </remarks>
internal static partial class DiagramDetector
{
    /// <summary>The types this version draws.</summary>
    internal static readonly IReadOnlySet<string> SupportedTypes = new HashSet<string>(StringComparer.Ordinal) { "sequence", "activity", "class", "object", "state", "usecase", "component", "deployment" };

    /// <summary>The closer of a multi-line action (<c>:text</c> up to a line that ends with <c>;</c>).</summary>
    internal const string ActionCloser = ";";

    /// <summary>The <c>@start</c> tags PlantUML defines, other than <c>uml</c>, with the type each one starts.</summary>
    internal static readonly IReadOnlyDictionary<string, string> TagTypes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["mindmap"] = "mindmap",
        ["gantt"] = "gantt",
        ["wbs"] = "wbs",
        ["json"] = "json",
        ["yaml"] = "yaml",
        ["salt"] = "salt",
        ["ditaa"] = "ditaa",
        ["dot"] = "dot",
        ["math"] = "math",
        ["latex"] = "latex",
        ["regex"] = "regex",
        ["ebnf"] = "ebnf",
        ["chen"] = "chen",
        ["files"] = "files",
        ["chronology"] = "chronology",
        ["board"] = "board",
        ["creole"] = "creole",
        ["wire"] = "wire",
        ["nwdiag"] = "nwdiag",
        ["chart"] = "chart",
        ["def"] = "def",
        ["git"] = "git",
        ["project"] = "project",
        ["flow"] = "flow",
        ["packetdiag"] = "packetdiag",
        ["jcckit"] = "jcckit",
    };

    /// <summary>The keywords that declare a participant of a sequence diagram.</summary>
    private static readonly HashSet<string> ParticipantKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "participant", "actor", "boundary", "control", "entity", "database", "collections", "queue",
    };

    /// <summary>The keywords that start a statement of a sequence diagram only.</summary>
    private static readonly HashSet<string> SequenceKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "activate", "deactivate", "destroy", "create", "return", "autonumber", "alt", "opt", "loop", "par", "par2",
        "break", "critical", "group", "box", "ref", "newpage", "mainframe", "autoactivate", "hnote", "rnote",
        "also", "ignore", "endbox",
    };

    /// <summary>The keywords that start a statement of an activity diagram of the new syntax only.</summary>
    private static readonly HashSet<string> ActivityKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "start", "stop", "kill", "detach", "endif", "elseif", "while", "endwhile", "repeat", "fork", "split",
        "partition", "switch", "case", "endswitch", "goto", "label", "backward", "floating", "if", "swimlane",
    };

    /// <summary>The keywords that both drawn types accept (titles, notes, styling, blocks).</summary>
    private static readonly HashSet<string> CommonKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "title", "header", "footer", "caption", "legend", "endlegend", "skinparam", "scale", "hide", "show", "note",
        "end", "else", "endheader", "endfooter", "endtitle", "endcaption", "skin", "endnote",
    };

    /// <summary>The keywords that only another (not drawn) type uses, with that type, in the order they are tried.</summary>
    private static readonly (string Keyword, string Type)[] OtherTypeKeywords =
    [
        ("class", "class"),
        ("abstract", "class"),
        ("interface", "class"),
        ("enum", "class"),
        ("annotation", "class"),
        ("struct", "class"),
        ("exception", "class"),
        ("metaclass", "class"),
        ("stereotype", "class"),
        ("protocol", "class"),
        ("object", "object"),
        ("map", "object"),
        ("json", "json"),
        ("usecase", "usecase"),
        ("state", "state"),
        ("component", "component"),
        ("interface", "component"),
        ("port", "component"),
        ("portin", "component"),
        ("portout", "component"),
        ("node", "deployment"),
        ("artifact", "deployment"),
        ("cloud", "deployment"),
        ("folder", "deployment"),
        ("frame", "deployment"),
        ("storage", "deployment"),
        ("rectangle", "deployment"),
        ("card", "deployment"),
        ("file", "deployment"),
        ("stack", "deployment"),
        ("agent", "deployment"),
        ("hexagon", "deployment"),
        ("person", "deployment"),
        ("package", "class"),
        ("namespace", "class"),
        ("robust", "timing"),
        ("concise", "timing"),
        ("clock", "timing"),
        ("binary", "timing"),
        ("analog", "timing"),
        ("nwdiag", "nwdiag"),
        ("salt", "salt"),
    ];

    /// <summary>
    ///     Detects the type of a preprocessed diagram.
    /// </summary>
    /// <param name="source">The preprocessed source.</param>
    /// <returns>The detected type.</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown, located, for an unknown tag, an empty diagram, or statements that no type accepts.</exception>
    public static DetectedDiagram Detect(PreprocessedSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var block = source.Block;

        // A tag other than uml names its type
        if (block.Tag is { } tag && tag != "uml")
        {
            if (TagTypes.TryGetValue(tag, out var tagType))
            {
                return new DetectedDiagram(tagType, SupportedTypes.Contains(tagType), "@start" + tag, block.Location);
            }

            var nearest = TagTypes.Keys.Append("uml").MinBy(known => EditDistance(known, tag))!;
            throw new PlantUmlSyntaxException(
                PlantUmlDiagnosticCodes.UnknownDiagram,
                $"'@start{tag}' is not a PlantUML diagram tag",
                block.Location,
                $"did you mean '@start{nearest}'?",
                ["@startuml"]);
        }

        var statements = block.Statements.Where(s => s.Text.Length > 0).ToList();
        return DetectUml(statements, block.Location);
    }

    /// <summary>
    ///     Detects the type of an <c>@startuml</c> diagram from its statements.
    /// </summary>
    /// <param name="statements">The non-empty statements.</param>
    /// <param name="location">The location of the diagram start.</param>
    /// <returns>The detected type.</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown, located, when the diagram is empty or no type accepts it.</exception>
    private static DetectedDiagram DetectUml(List<SourceStatement> statements, PlantUmlLocation location)
    {
        // The statements inside multi-line blocks (notes, legends, titles) are text, not statements
        var outer = OuterStatements(statements);
        if (outer.All(s => Classify(s.Text) == StatementFamily.Common))
        {
            throw new PlantUmlSyntaxException(
                PlantUmlDiagnosticCodes.SyntaxError,
                "the diagram has no statement that draws something",
                outer.Count > 0 ? outer[0].Location : location,
                "add a participant and a message, for example 'Alice -> Bob : hello'",
                ["a participant", "a message"]);
        }

        // PlantUML tries the sequence diagram first, then the activity diagram of the new syntax
        var sequenceReject = outer.FirstOrDefault(s => !AcceptsSequence(s.Text) || IsClassOnlyLink(s));
        if (sequenceReject is null)
        {
            var first = outer.First(s => Classify(s.Text) != StatementFamily.Common);
            return new DetectedDiagram("sequence", SupportedTypes.Contains("sequence"), FirstWord(first.Text), first.Location);
        }

        // (an action that is never closed refuses the activity diagram when a graph family accepts the text, as
        // PlantUML's multi-line action needs its end: ':Guest:' declares an actor; the graph families are tried once)
        DetectedDiagram? graph = null;
        var graphTried = false;
        DetectedDiagram? Graph()
        {
            if (!graphTried)
            {
                graph = DetectGraph(statements);
                graphTried = true;
            }

            return graph;
        }

        var activityReject = outer.FirstOrDefault(s => !AcceptsActivity(s.Text));
        if (activityReject is null && outer.Any(s => Classify(s.Text) == StatementFamily.Activity)
            && !(EndsInOpenAction(statements) && Graph() is not null))
        {
            var first = outer.First(s => Classify(s.Text) != StatementFamily.Common);
            return new DetectedDiagram("activity", SupportedTypes.Contains("activity"), FirstWord(first.Text), first.Location);
        }

        // Then the graph families in PlantUML's order: class, legacy activity, description, and state
        if (Graph() is { } detected)
        {
            return detected;
        }

        // The first statement that only another type uses names that type
        foreach (var statement in outer)
        {
            if (OtherType(statement.Text) is { } other)
            {
                return new DetectedDiagram(other, SupportedTypes.Contains(other), FirstWord(statement.Text), statement.Location);
            }
        }

        // Otherwise the error is at the first statement the more likely drawn type refuses
        var activityScore = outer.Count(s => Classify(s.Text) == StatementFamily.Activity);
        var sequenceScore = outer.Count(s => Classify(s.Text) == StatementFamily.Sequence);
        var (reject, family) = activityScore > sequenceScore && activityReject is not null
            ? (activityReject, "an activity")
            : (sequenceReject, "a sequence");
        throw new PlantUmlSyntaxException(
            PlantUmlDiagnosticCodes.SyntaxError,
            $"'{Shorten(reject.Text)}' is not a statement of {family} diagram",
            reject.Location,
            family == "a sequence"
                ? "write a message as 'A -> B : text' and declare participants with 'participant', 'actor', ..."
                : "write an action as ':text;' and use start, stop, if, while, repeat, fork and partition",
            null);
    }

    /// <summary>
    ///     Gets the statements outside multi-line text blocks (the lines of a multi-line note, legend, title, header,
    ///     footer or caption are text).
    /// </summary>
    /// <param name="statements">The statements.</param>
    /// <returns>The outer statements, block openers and closers included.</returns>
    internal static List<SourceStatement> OuterStatements(IReadOnlyList<SourceStatement> statements) =>
        OuterStatements(statements, openActionIsText: true);

    /// <summary>
    ///     Decides whether the statements end inside a multi-line action (<c>:text</c> whose ending <c>;</c> never
    ///     comes).
    /// </summary>
    /// <param name="statements">The statements.</param>
    /// <returns>True when they do.</returns>
    internal static bool EndsInOpenAction(IReadOnlyList<SourceStatement> statements)
    {
        string? closer = null;
        foreach (var text in statements.Select(s => s.Text))
        {
            if (closer is null)
            {
                closer = BlockCloser(text);
            }
            else if (IsCloser(text, closer))
            {
                closer = null;
            }
        }

        return closer == ActionCloser;
    }

    /// <summary>
    ///     Gets the statements outside multi-line text blocks.
    /// </summary>
    /// <param name="statements">The statements.</param>
    /// <param name="openActionIsText">
    ///     Whether the lines after an action that is never closed are its text (false: the unclosed action is one
    ///     statement and the lines after it are statements).
    /// </param>
    /// <returns>The outer statements, block openers and closers included.</returns>
    private static List<SourceStatement> OuterStatements(IReadOnlyList<SourceStatement> statements, bool openActionIsText) =>
        OuterStatements(statements, openActionIsText, out _);

    /// <summary>
    ///     Gets the statements outside multi-line text blocks, counting the statements visited: each statement is
    ///     visited at most twice, since after an action that is never closed no later action can be closed either.
    /// </summary>
    /// <param name="statements">The statements.</param>
    /// <param name="openActionIsText">
    ///     Whether the lines after an action that is never closed are its text (false: the unclosed action is one
    ///     statement and the lines after it are statements).
    /// </param>
    /// <param name="visits">Receives the number of statements visited.</param>
    /// <returns>The outer statements, block openers and closers included.</returns>
    internal static List<SourceStatement> OuterStatements(IReadOnlyList<SourceStatement> statements, bool openActionIsText, out int visits)
    {
        var outer = new List<SourceStatement>();
        string? closer = null;
        var opener = -1;
        var actionsClose = true;
        var i = -1;
        visits = 0;
        while (++i < statements.Count)
        {
            visits++;
            var statement = statements[i];
            var text = statement.Text;
            if (closer is not null)
            {
                if (IsCloser(text, closer))
                {
                    // The last line of a multi-line action is text too; the closer of other blocks is a statement
                    if (closer != ActionCloser)
                    {
                        outer.Add(statement);
                    }

                    closer = null;
                }
                else if (i == statements.Count - 1 && closer == ActionCloser && !openActionIsText)
                {
                    // The action is never closed: it is one statement and the lines after it are statements (no line
                    // after it ends an action, so every later action is one statement too)
                    closer = null;
                    actionsClose = false;
                    i = opener;
                }

                continue;
            }

            outer.Add(statement);
            closer = BlockCloser(text);
            if (closer == ActionCloser && !actionsClose)
            {
                closer = null;
            }

            opener = i;
        }

        return outer;
    }

    /// <summary>
    ///     Gets the keyword that closes a multi-line text block opened by a statement.
    /// </summary>
    /// <param name="text">The statement text.</param>
    /// <returns>The closing keyword, or null when the statement opens no text block.</returns>
    internal static string? BlockCloser(string text)
    {
        if (OpensAction(text))
        {
            return ActionCloser;
        }

        var word = FirstWord(text).ToLowerInvariant();
        switch (word)
        {
            case "note" or "hnote" or "rnote" or "floating":
                // A note with its text after a colon is on one line
                return text.Contains(':', StringComparison.Ordinal) ? null : "end note";
            case "legend":
                return "endlegend";
            case "title" or "header" or "footer" or "caption":
                // A keyword alone on its line opens a block; with text it is one line
                return text.Trim().Length == word.Length ? "end" + word : null;
            case "left" or "right" or "center" when IsAlignedPageText(text, word):
                // An aligned header or footer alone on its line opens a block too
                var rest = text[word.Length..].Trim();
                return rest.Length == FirstWord(rest).Length ? "end" + rest.ToLowerInvariant() : null;
            case "ref":
                return text.Contains(':', StringComparison.Ordinal) ? null : "end ref";
            case "skinparam":
                return text.TrimEnd().EndsWith('{') ? "}" : null;
            default:
                return text.StartsWith("<style", StringComparison.OrdinalIgnoreCase) ? "</style>" : null;
        }
    }

    /// <summary>
    ///     Checks whether a statement closes a multi-line text block.
    /// </summary>
    /// <param name="text">The statement text.</param>
    /// <param name="closer">The closing keyword.</param>
    /// <returns>True when it does.</returns>
    internal static bool IsCloser(string text, string closer)
    {
        if (closer == ActionCloser)
        {
            return EndsAction(text);
        }

        var compact = string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
        return closer switch
        {
            "end note" => compact is "end note" or "endnote" or "end hnote" or "endhnote" or "end rnote" or "endrnote",
            "end ref" => compact is "end ref" or "endref",
            _ => compact == closer || (closer.StartsWith("end", StringComparison.Ordinal) && compact == "end " + closer[3..]),
        };
    }

    /// <summary>
    ///     The family of a statement by its leading keyword.
    /// </summary>
    internal enum StatementFamily
    {
        /// <summary>A statement both drawn types accept (title, note, skinparam, ...).</summary>
        Common,

        /// <summary>A statement of the sequence diagram only.</summary>
        Sequence,

        /// <summary>A statement of the activity diagram only.</summary>
        Activity,

        /// <summary>A statement of neither drawn type.</summary>
        Other,
    }

    /// <summary>
    ///     Classifies a statement by its leading keyword or arrow.
    /// </summary>
    /// <param name="text">The statement text.</param>
    /// <returns>The family.</returns>
    internal static StatementFamily Classify(string text)
    {
        var word = FirstWord(text);
        if (IsSequenceSeparator(text))
        {
            return StatementFamily.Sequence;
        }

        if (text.StartsWith(':') || text.StartsWith('|') || IsActivityArrow(text) || ActivityKeywords.Contains(word) ||
            (word.Equals("end", StringComparison.OrdinalIgnoreCase) && IsActivityEnd(text)))
        {
            return StatementFamily.Activity;
        }

        if (CommonKeywords.Contains(word) || IsAlignedPageText(text, word) || text.StartsWith("<style", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("</style", StringComparison.OrdinalIgnoreCase) || text.StartsWith('!'))
        {
            return StatementFamily.Common;
        }

        if (ParticipantKeywords.Contains(word) || SequenceKeywords.Contains(word) || IsSequenceArrow(text) ||
            IsSequenceSeparator(text))
        {
            return StatementFamily.Sequence;
        }

        return StatementFamily.Other;
    }

    /// <summary>
    ///     Decides whether a statement is an aligned header or footer (<c>left header</c>, <c>right footer</c>, ...).
    /// </summary>
    /// <param name="text">The statement text.</param>
    /// <param name="word">Its first word.</param>
    /// <returns>True for an aligned page text.</returns>
    private static bool IsAlignedPageText(string text, string word)
    {
        if (!word.Equals("left", StringComparison.OrdinalIgnoreCase) && !word.Equals("right", StringComparison.OrdinalIgnoreCase) &&
            !word.Equals("center", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var next = FirstWord(text[word.Length..].TrimStart());
        return next.Equals("header", StringComparison.OrdinalIgnoreCase) || next.Equals("footer", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Decides whether a sequence diagram accepts a statement.
    /// </summary>
    /// <param name="text">The statement text.</param>
    /// <returns>True when the statement is a sequence or common statement.</returns>
    internal static bool AcceptsSequence(string text)
    {
        var word = FirstWord(text);
        if (word.Equals("end", StringComparison.OrdinalIgnoreCase) && !IsActivityEnd(text))
        {
            return true;
        }

        return Classify(text) is StatementFamily.Common or StatementFamily.Sequence && !IsActivityOnly(text);
    }

    /// <summary>
    ///     Decides whether an activity diagram of the new syntax accepts a statement.
    /// </summary>
    /// <param name="text">The statement text.</param>
    /// <returns>True when the statement is an activity or common statement.</returns>
    internal static bool AcceptsActivity(string text)
    {
        if (Classify(text) is StatementFamily.Common or StatementFamily.Activity || IsConnector(text))
        {
            return true;
        }

        // Group closers, colored actions and tests, the group keywords, and labeled elseif or backward statements
        var word = FirstWord(text).ToLowerInvariant();
        return text == "}" || (text.StartsWith('#') && (text.Contains(':', StringComparison.Ordinal) || ColoredKeyword(text))) ||
               (word is "group" or "package" or "rectangle" or "card" && text.TrimEnd().EndsWith('{')) || word == "break" ||
               (text.StartsWith('(') && (text.Contains("elseif", StringComparison.OrdinalIgnoreCase) ||
                                         text.Contains("else if", StringComparison.OrdinalIgnoreCase) ||
                                         text.Contains("backward", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    ///     Decides whether a statement that starts with a color continues with an activity keyword
    ///     (<c>#red if (...)</c>, <c>#red while (...)</c>, <c>#red repeat</c>, <c>#red (A)</c>).
    /// </summary>
    /// <param name="text">The statement text.</param>
    /// <returns>True for a colored activity statement.</returns>
    private static bool ColoredKeyword(string text)
    {
        var space = text.IndexOfAny([' ', '\t', '(']);
        if (space < 0)
        {
            return false;
        }

        var rest = text[space..].TrimStart();
        var word = FirstWord(rest).ToLowerInvariant();
        return word is "if" or "while" or "repeat" || rest.StartsWith('(');
    }

    /// <summary>
    ///     Decides whether a statement opens an action that continues on the next lines (<c>:text</c> without its
    ///     ending <c>;</c>).
    /// </summary>
    /// <param name="text">The statement text.</param>
    /// <returns>True when the action continues.</returns>
    internal static bool OpensAction(string text)
    {
        var colon = -1;
        if (text.StartsWith(':'))
        {
            colon = 0;
        }
        else if (text.StartsWith('#'))
        {
            colon = text.IndexOf(':', StringComparison.Ordinal);
        }

        if (colon < 0 || (colon > 0 && text[..colon].Contains(' ', StringComparison.Ordinal)))
        {
            return false;
        }

        return !EndsAction(text[(colon + 1)..]);
    }

    /// <summary>
    ///     Decides whether a line ends an action: it ends with a semicolon (possibly followed by a stereotype or a link)
    ///     or with one of the box style characters.
    /// </summary>
    /// <param name="text">The line.</param>
    /// <returns>True when the line ends the action.</returns>
    internal static bool EndsAction(string text)
    {
        var trimmed = text.TrimEnd();
        if (trimmed.EndsWith(">>", StringComparison.Ordinal) && trimmed.LastIndexOf("<<", StringComparison.Ordinal) is var open and >= 0)
        {
            trimmed = trimmed[..open].TrimEnd();
        }
        else if (trimmed.EndsWith("]]", StringComparison.Ordinal) && trimmed.LastIndexOf("[[", StringComparison.Ordinal) is var link and >= 0)
        {
            trimmed = trimmed[..link].TrimEnd();
        }

        return trimmed.Length > 0 && trimmed[^1] is ';' or '|' or '<' or '>' or '/' or '\\' or ']' or '}';
    }

    /// <summary>
    ///     Decides whether a statement is an activity connector <c>(A)</c>: a short name in parentheses alone.
    /// </summary>
    /// <param name="text">The statement text.</param>
    /// <returns>True for a connector.</returns>
    private static bool IsConnector(string text) =>
        text.Length >= 3 && text[0] == '(' && text[^1] == ')' && text.IndexOf(')', StringComparison.Ordinal) == text.Length - 1 &&
        text.Length <= 34;

    /// <summary>
    ///     Gets the type that only another (not drawn) diagram type uses a statement for, or null.
    /// </summary>
    /// <param name="text">The statement text.</param>
    /// <returns>The type, or null.</returns>
    internal static string? OtherType(string text)
    {
        // Markers written with brackets come first: [*] is a state, (*) a legacy activity, [x] a component,
        // (x) a use case, and [x] followed by a gantt verb a gantt chart
        if (text.Contains("[*]", StringComparison.Ordinal))
        {
            return "state";
        }

        if (text.Contains("(*)", StringComparison.Ordinal))
        {
            return "activity-legacy";
        }

        if (text.StartsWith('[') && IsGantt(text))
        {
            return "gantt";
        }

        if (text.StartsWith('['))
        {
            return "component";
        }

        if (text.StartsWith('(') && text.Contains(')', StringComparison.Ordinal))
        {
            return "usecase";
        }

        var word = FirstWord(text);
        foreach (var (keyword, type) in OtherTypeKeywords)
        {
            if (word.Equals(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return type;
            }
        }

        // A timing diagram's time statements start with @
        return text.StartsWith('@') && text.Length > 1 && (char.IsAsciiDigit(text[1]) || text[1] == '+') ? "timing" : null;
    }

    /// <summary>
    ///     Gives the hint of a type that is not drawn.
    /// </summary>
    /// <param name="detected">The detected type.</param>
    /// <returns>The hint.</returns>
    public static string UnsupportedHint(DetectedDiagram detected)
    {
        ArgumentNullException.ThrowIfNull(detected);
        var drawn = SupportedTypes.Count == 0
            ? "no PlantUML diagram type yet"
            : "only " + JoinTypes(SupportedTypes.Order(StringComparer.Ordinal).ToList()) + " diagrams";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"this version draws {drawn}; render this '{detected.DiagramType}' diagram with PlantUML itself");
    }

    /// <summary>
    ///     Gets the first word of a statement: the letters (and digits) before the first other character.
    /// </summary>
    /// <param name="text">The statement text.</param>
    /// <returns>The word, possibly empty.</returns>
    internal static string FirstWord(string text)
    {
        var i = 0;
        while (i < text.Length && (char.IsAsciiLetterOrDigit(text[i]) || text[i] == '_'))
        {
            i++;
        }

        return text[..i];
    }

    /// <summary>
    ///     Decides whether an <c>end</c> statement belongs to an activity diagram (<c>end fork</c>, <c>end split</c>,
    ///     <c>end merge</c>, <c>end while</c>, or <c>end</c> as the end node is ambiguous and taken as a sequence
    ///     block end).
    /// </summary>
    /// <param name="text">The statement text.</param>
    /// <returns>True for an activity-only end.</returns>
    private static bool IsActivityEnd(string text)
    {
        var rest = text[3..].Trim().ToLowerInvariant();
        return rest is "fork" or "split" or "merge" or "while" or "switch" or "repeat";
    }

    /// <summary>
    ///     Decides whether a statement accepted by keyword is still only an activity statement (an <c>else</c> with a
    ///     parenthesized label).
    /// </summary>
    /// <param name="text">The statement text.</param>
    /// <returns>True for an activity-only statement.</returns>
    private static bool IsActivityOnly(string text)
    {
        var word = FirstWord(text).ToLowerInvariant();
        var rest = text[word.Length..].TrimStart();
        return word == "else" && rest.StartsWith('(');
    }

    /// <summary>
    ///     Decides whether a statement is an arrow of an activity diagram (<c>-&gt; label;</c> or <c>-[#red]-&gt;</c>).
    /// </summary>
    /// <param name="text">The statement text.</param>
    /// <returns>True for an activity arrow.</returns>
    private static bool IsActivityArrow(string text) =>
        text.StartsWith("->", StringComparison.Ordinal) || text.StartsWith("-[", StringComparison.Ordinal) ||
        text.StartsWith("-->", StringComparison.Ordinal);

    /// <summary>
    ///     Decides whether a statement is a sequence separator: a divider <c>== x ==</c>, a delay <c>...</c>, or a space
    ///     <c>|||</c> / <c>||45||</c>.
    /// </summary>
    /// <param name="text">The statement text.</param>
    /// <returns>True for a separator.</returns>
    private static bool IsSequenceSeparator(string text) =>
        text.StartsWith("==", StringComparison.Ordinal) || text.StartsWith("...", StringComparison.Ordinal) ||
        text.StartsWith("||", StringComparison.Ordinal);

    /// <summary>
    ///     Decides whether a statement is a sequence message: a participant (or an end on the diagram border), an arrow
    ///     and a participant (or an end on the diagram border).
    /// </summary>
    /// <param name="text">The statement text.</param>
    /// <returns>True for a message.</returns>
    internal static bool IsSequenceArrow(string text) =>
        TryFindMessageArrow(text, out var left, out var run, out var right)
        && (IsParticipantEnd(left, isLeft: true) || IsBorderStart(left))
        && (IsParticipantEnd(right, isLeft: false) || IsBorderEnd(run, right));

    /// <summary>
    ///     Decides whether a statement is a message to or from the diagram border (a found or a lost message): the
    ///     left forms <c>[-></c>, <c>[o-></c>, <c>[x-></c>, <c>]-></c>, <c>?-></c>, <c>?o-></c> and the right forms
    ///     <c>-&gt;]</c>, <c>-&gt;o]</c>, <c>-&gt;x]</c>, <c>-&gt;?</c>, <c>-&gt;o?</c>, with any body, head, or color
    ///     (PlantUML's <c>CommandExoArrowLeft</c> and <c>CommandExoArrowRight</c>); only the part before the label is
    ///     read, so a label may hold brackets and arrows.
    /// </summary>
    /// <param name="text">The statement text.</param>
    /// <returns>True for a message with an end on the border.</returns>
    internal static bool IsBorderMessage(string text) =>
        TryFindMessageArrow(text, out var left, out var run, out var right)
        && ((IsBorderStart(left) && IsParticipantEnd(right, isLeft: false))
            || (IsParticipantEnd(left, isLeft: true) && IsBorderEnd(run, right)));

    /// <summary>
    ///     Finds the arrow of a message before its label: the first run of arrow characters that holds a dash or a
    ///     dot and a head or a dash pair.
    /// </summary>
    /// <param name="text">The statement text.</param>
    /// <param name="left">Receives the trimmed text before the arrow.</param>
    /// <param name="run">Receives the arrow.</param>
    /// <param name="right">Receives the trimmed text after the arrow, up to the label.</param>
    /// <returns>True when the statement has an arrow before its label.</returns>
    private static bool TryFindMessageArrow(string text, out string left, out string run, out string right)
    {
        var colon = text.IndexOf(':', StringComparison.Ordinal);
        var head = colon >= 0 ? text[..colon] : text;
        for (var i = 0; i < head.Length; i++)
        {
            if (head[i] is '-' or '.' && IsArrowRun(head, i, out var runStart, out var runEnd))
            {
                left = head[..runStart].Trim();
                run = head[runStart..runEnd];
                right = head[runEnd..].Trim();
                return true;
            }
        }

        left = run = right = string.Empty;
        return false;
    }

    /// <summary>
    ///     Decides whether the end before an arrow is on the diagram border: <c>[</c>, <c>]</c>, or <c>?</c>, optionally
    ///     followed by a circle <c>o</c> or a cross <c>x</c>.
    /// </summary>
    /// <param name="end">The trimmed text before the arrow.</param>
    /// <returns>True for a border end.</returns>
    private static bool IsBorderStart(string end) =>
        end.Length is 1 or 2 && end[0] is '[' or ']' or '?' && (end.Length == 1 || end[1] is 'o' or 'x');

    /// <summary>
    ///     Decides whether the end after an arrow is on the diagram border: a <c>?</c> (or a lone bracket) after the
    ///     arrow, or an arrow that ends with <c>]</c> after its head or body (a circle or a cross between them),
    ///     followed only by the activation shorthands.
    /// </summary>
    /// <param name="run">The arrow.</param>
    /// <param name="right">The trimmed text after the arrow, up to the label.</param>
    /// <returns>True for a border end.</returns>
    private static bool IsBorderEnd(string run, string right)
    {
        var rest = right.TrimEnd('+', '-', '*', '!', ' ');
        if (rest is "?" or "]" or "[")
        {
            return true;
        }

        if (rest.Length > 0 || !run.EndsWith(']'))
        {
            return false;
        }

        // The ']' must follow the head or the body (not close a color, as in -[#red])
        var body = run[..^1];
        if (body.Length > 0 && body[^1] is 'o' or 'x')
        {
            body = body[..^1];
        }

        return body.Length > 0 && body[^1] is '>' or '-' or '<' or '/' or '\\';
    }

    /// <summary>
    ///     Finds the run of arrow characters around a dash or a dot and checks that it is an arrow.
    /// </summary>
    /// <param name="head">The part of the statement before the label.</param>
    /// <param name="index">The index of a dash or a dot.</param>
    /// <param name="runStart">Receives the start of the run.</param>
    /// <param name="runEnd">Receives the end of the run (exclusive).</param>
    /// <returns>True when the run is an arrow (a head, or at least two dashes).</returns>
    private static bool IsArrowRun(string head, int index, out int runStart, out int runEnd)
    {
        static bool IsArrowChar(char c) => c is '-' or '.' or '<' or '>' or '\\' or '/' or 'x' or 'o' or '*' or '[' or ']' or '#';
        runStart = index;
        while (runStart > 0 && head[runStart - 1] is '<' or '\\' or '/' or '-' or '.')
        {
            runStart--;
        }

        // A color in brackets (-[#red]>) is part of the arrow
        runEnd = index;
        while (runEnd < head.Length && (IsArrowChar(head[runEnd]) || (runEnd > index && head[runEnd - 1] == '[' && head[runEnd] != ']')))
        {
            if (head[runEnd] == '[')
            {
                var close = head.IndexOf(']', runEnd);
                runEnd = close < 0 ? head.Length : close + 1;
                continue;
            }

            if (head[runEnd] is 'x' or 'o' && runEnd + 1 < head.Length && char.IsAsciiLetterOrDigit(head[runEnd + 1]))
            {
                break;
            }

            runEnd++;
        }

        var run = head[runStart..runEnd];
        return run.Contains('>', StringComparison.Ordinal) || run.Contains('<', StringComparison.Ordinal) ||
               run.Contains("--", StringComparison.Ordinal) || run.Contains("-\\", StringComparison.Ordinal) ||
               run.Contains("-/", StringComparison.Ordinal) || run.Contains("\\-", StringComparison.Ordinal) ||
               run.Contains("/-", StringComparison.Ordinal) || run.EndsWith("-x", StringComparison.Ordinal) ||
               run.EndsWith("-o", StringComparison.Ordinal);
    }

    /// <summary>
    ///     Decides whether one end of a message can be a sequence participant: a name not in brackets or parentheses
    ///     (which belong to other types).
    /// </summary>
    /// <param name="end">The trimmed text of the end.</param>
    /// <param name="isLeft">Whether it is the end before the arrow.</param>
    /// <returns>True when the end can be a participant.</returns>
    private static bool IsParticipantEnd(string end, bool isLeft)
    {
        if (end.Length == 0)
        {
            return false;
        }

        // The end after the arrow may carry the activation shorthands (++, --, **, !!) and a color
        if (!isLeft)
        {
            end = end.TrimEnd('+', '-', '*', '!', ' ');
        }

        return end.Length > 0 && end[0] is not '(' and not '[' && end[^1] is not ')' and not ']';
    }
    /// <summary>
    ///     Decides whether a bracketed statement is a gantt task statement.
    /// </summary>
    /// <param name="text">The statement text.</param>
    /// <returns>True for a gantt statement.</returns>
    private static bool IsGantt(string text)
    {
        var close = text.IndexOf(']', StringComparison.Ordinal);
        var rest = close < 0 ? string.Empty : text[(close + 1)..].TrimStart().ToLowerInvariant();
        return rest.StartsWith("lasts", StringComparison.Ordinal) || rest.StartsWith("requires", StringComparison.Ordinal) ||
               rest.StartsWith("starts", StringComparison.Ordinal) || rest.StartsWith("ends", StringComparison.Ordinal) ||
               rest.StartsWith("happens", StringComparison.Ordinal) || rest.StartsWith("is ", StringComparison.Ordinal);
    }

    /// <summary>
    ///     Shortens a statement for a message.
    /// </summary>
    /// <param name="text">The statement.</param>
    /// <returns>At most 60 characters.</returns>
    private static string Shorten(string text) => text.Length <= 60 ? text : string.Concat(text.AsSpan(0, 57), "...");

    /// <summary>
    ///     Computes the Levenshtein distance of two words, for the nearest-tag hint.
    /// </summary>
    /// <param name="a">The first word.</param>
    /// <param name="b">The second word.</param>
    /// <returns>The number of edits.</returns>
    internal static int EditDistance(string a, string b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
