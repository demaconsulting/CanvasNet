using System.Globalization;

namespace DemaConsulting.CanvasNet.PlantUml.Syntax;

/// <summary>
///     The detected type of a diagram.
/// </summary>
/// <param name="DiagramType">
///     The public type name: <c>sequence</c>, <c>activity</c>, or a recognized type that is not drawn (<c>class</c>,
///     <c>usecase</c>, <c>component</c>, <c>deployment</c>, <c>state</c>, <c>object</c>, <c>timing</c>,
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
///         <c>(*)</c>, <c>component</c>, <c>node</c>, ...) names that type, which is recognized but not drawn; else the
///         text is a syntax error at the first statement that the more likely of the two drawn types does not accept.
///     </para>
///     <para>
///         The statements are recognized by their leading keywords and arrows only; the diagram parser then reads them
///         strictly and reports a located syntax error for a statement it cannot read. Detection runs no regular
///         expression. The class is stateless and safe for concurrent use.
///     </para>
/// </remarks>
internal static class DiagramDetector
{
    /// <summary>The types this version draws.</summary>
    internal static readonly IReadOnlySet<string> SupportedTypes = new HashSet<string>(StringComparer.Ordinal);

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
        "also", "ignore",
    };

    /// <summary>The keywords that start a statement of an activity diagram of the new syntax only.</summary>
    private static readonly HashSet<string> ActivityKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "start", "stop", "kill", "detach", "endif", "elseif", "while", "endwhile", "repeat", "fork", "split",
        "partition", "switch", "case", "endswitch", "goto", "label", "backward", "floating", "if",
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
        var sequenceReject = outer.FirstOrDefault(s => !AcceptsSequence(s.Text));
        if (sequenceReject is null)
        {
            var first = outer.First(s => Classify(s.Text) != StatementFamily.Common);
            return new DetectedDiagram("sequence", SupportedTypes.Contains("sequence"), FirstWord(first.Text), first.Location);
        }

        var activityReject = outer.FirstOrDefault(s => !AcceptsActivity(s.Text));
        if (activityReject is null && outer.Any(s => Classify(s.Text) == StatementFamily.Activity))
        {
            var first = outer.First(s => Classify(s.Text) != StatementFamily.Common);
            return new DetectedDiagram("activity", SupportedTypes.Contains("activity"), FirstWord(first.Text), first.Location);
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
    internal static List<SourceStatement> OuterStatements(IReadOnlyList<SourceStatement> statements)
    {
        var outer = new List<SourceStatement>();
        string? closer = null;
        foreach (var statement in statements)
        {
            var text = statement.Text;
            if (closer is not null)
            {
                if (IsCloser(text, closer))
                {
                    closer = null;
                    outer.Add(statement);
                }

                continue;
            }

            outer.Add(statement);
            closer = BlockCloser(text);
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
        var word = FirstWord(text).ToLowerInvariant();
        switch (word)
        {
            case "note" or "hnote" or "rnote":
                // A note with its text after a colon is on one line
                return text.Contains(':', StringComparison.Ordinal) ? null : "end note";
            case "legend":
                return "endlegend";
            case "title" or "header" or "footer" or "caption":
                // A keyword alone on its line opens a block; with text it is one line
                return text.Trim().Length == word.Length ? "end" + word : null;
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
        var compact = string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
        return closer switch
        {
            "end note" => compact is "end note" or "endnote" or "end hnote" or "endhnote" or "end rnote" or "endrnote",
            "end ref" => compact is "end ref" or "endref",
            _ => compact == closer || compact == closer.Replace("end", "end ", StringComparison.Ordinal),
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

        if (CommonKeywords.Contains(word) || text.StartsWith("<style", StringComparison.OrdinalIgnoreCase) ||
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
    internal static bool AcceptsActivity(string text) =>
        Classify(text) is StatementFamily.Common or StatementFamily.Activity || IsConnector(text);

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
            : "only " + string.Join(" and ", SupportedTypes.Order(StringComparer.Ordinal)) + " diagrams";
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
    ///     Decides whether a statement is a sequence message: a participant (or <c>[</c>, <c>]</c>, <c>?</c>), an arrow
    ///     and a participant.
    /// </summary>
    /// <param name="text">The statement text.</param>
    /// <returns>True for a message.</returns>
    internal static bool IsSequenceArrow(string text)
    {
        // The arrow is before the label: a run of arrow characters that holds a dash or a dot and a head or a dash pair
        var colon = text.IndexOf(':', StringComparison.Ordinal);
        var head = colon >= 0 ? text[..colon] : text;
        for (var i = 0; i < head.Length; i++)
        {
            if (head[i] is '-' or '.' && IsArrowRun(head, i, out var runStart, out var runEnd))
            {
                var left = head[..runStart].Trim();
                var right = head[runEnd..].Trim();
                return IsParticipantEnd(left, true) && IsParticipantEnd(right, false);
            }
        }

        return false;
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
    ///     Decides whether one end of a message can be a sequence participant: a name (not one in brackets or
    ///     parentheses, which belong to other types), or the found and lost ends <c>[</c>, <c>]</c> and <c>?</c>.
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

        if (end is "[" or "]" or "?")
        {
            return true;
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
