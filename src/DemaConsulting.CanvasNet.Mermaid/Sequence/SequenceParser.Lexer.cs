// Portions derived from mermaid (MIT); see third-party/README.md
using System.Collections.Frozen;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Sequence;

/// <summary>
///     The token kinds of the sequence grammar: the names of the terminals of Mermaid's <c>sequenceDiagram.jison</c>.
/// </summary>
/// <remarks>
///     The kinds are the jison terminal names, so messages and <see cref="MermaidSyntaxException.Expected"/> lists read
///     like Mermaid's parse errors. The grammar has no <c>EOF</c> terminal of its own: the end of the token stream is
///     <see cref="SourceScanner.EndOfInput"/> (jison's <c>$end</c>, which Mermaid prints as <c>1</c>). The class only
///     holds constants and is safe for concurrent use.
/// </remarks>
internal static class SequenceTokens
{
    /// <summary>A space before a statement; the grammar has it, but no lexer rule produces it.</summary>
    public const string Space = "SPACE";

    /// <summary>One or more line breaks, a semicolon, or the end of the text (<c>NEWLINE</c>).</summary>
    public const string Newline = "NEWLINE";

    /// <summary>The <c>sequenceDiagram</c> keyword (<c>SD</c>).</summary>
    public const string Sd = "SD";

    /// <summary>Text no other rule takes (<c>INVALID</c>); a line of it is ignored.</summary>
    public const string Invalid = "INVALID";

    /// <summary>The <c>create</c> keyword.</summary>
    public const string Create = "create";

    /// <summary>The <c>box</c> keyword.</summary>
    public const string Box = "box";

    /// <summary>The rest of a line after a block keyword or <c>as</c> (<c>restOfLine</c>).</summary>
    public const string RestOfLine = "restOfLine";

    /// <summary>The <c>end</c> keyword.</summary>
    public const string End = "end";

    /// <summary>The <c>autonumber</c> keyword.</summary>
    public const string Autonumber = "autonumber";

    /// <summary>A number of an <c>autonumber</c> statement (<c>NUM</c>).</summary>
    public const string Num = "NUM";

    /// <summary>The <c>off</c> keyword.</summary>
    public const string Off = "off";

    /// <summary>The <c>activate</c> keyword.</summary>
    public const string Activate = "activate";

    /// <summary>The <c>deactivate</c> keyword.</summary>
    public const string Deactivate = "deactivate";

    /// <summary>A <c>title</c> statement with its text.</summary>
    public const string Title = "title";

    /// <summary>A <c>title:</c> statement with its text.</summary>
    public const string LegacyTitle = "legacy_title";

    /// <summary>The <c>accTitle:</c> keyword.</summary>
    public const string AccTitle = "acc_title";

    /// <summary>The text of an accessible title.</summary>
    public const string AccTitleValue = "acc_title_value";

    /// <summary>The <c>accDescr:</c> keyword.</summary>
    public const string AccDescr = "acc_descr";

    /// <summary>The text of a one-line accessible description.</summary>
    public const string AccDescrValue = "acc_descr_value";

    /// <summary>The text of a multi-line accessible description (<c>accDescr { ... }</c>).</summary>
    public const string AccDescrMultilineValue = "acc_descr_multiline_value";

    /// <summary>The <c>loop</c> keyword.</summary>
    public const string Loop = "loop";

    /// <summary>The <c>rect</c> keyword.</summary>
    public const string Rect = "rect";

    /// <summary>The <c>opt</c> keyword.</summary>
    public const string Opt = "opt";

    /// <summary>The <c>alt</c> keyword.</summary>
    public const string Alt = "alt";

    /// <summary>The <c>par</c> keyword.</summary>
    public const string Par = "par";

    /// <summary>The <c>par_over</c> keyword.</summary>
    public const string ParOver = "par_over";

    /// <summary>The <c>critical</c> keyword.</summary>
    public const string Critical = "critical";

    /// <summary>The <c>break</c> keyword.</summary>
    public const string Break = "break";

    /// <summary>The <c>option</c> keyword.</summary>
    public const string Option = "option";

    /// <summary>The <c>and</c> keyword.</summary>
    public const string And = "and";

    /// <summary>The <c>else</c> keyword.</summary>
    public const string Else = "else";

    /// <summary>The <c>participant</c> keyword.</summary>
    public const string Participant = "participant";

    /// <summary>The <c>as</c> of an alias (<c>AS</c>).</summary>
    public const string As = "AS";

    /// <summary>The <c>actor</c> keyword (<c>participant_actor</c>).</summary>
    public const string ParticipantActor = "participant_actor";

    /// <summary>The <c>destroy</c> keyword.</summary>
    public const string Destroy = "destroy";

    /// <summary>The <c>note</c> keyword.</summary>
    public const string Note = "note";

    /// <summary>The <c>over</c> keyword.</summary>
    public const string Over = "over";

    /// <summary>The <c>links</c> keyword.</summary>
    public const string Links = "links";

    /// <summary>The <c>link</c> keyword.</summary>
    public const string Link = "link";

    /// <summary>The <c>properties</c> keyword.</summary>
    public const string Properties = "properties";

    /// <summary>The <c>details</c> keyword.</summary>
    public const string Details = "details";

    /// <summary>A comma.</summary>
    public const string Comma = ",";

    /// <summary>The <c>left of</c> keyword.</summary>
    public const string LeftOf = "left_of";

    /// <summary>The <c>right of</c> keyword.</summary>
    public const string RightOf = "right_of";

    /// <summary>A plus (an activation).</summary>
    public const string Plus = "+";

    /// <summary>A minus (a deactivation).</summary>
    public const string Minus = "-";

    /// <summary>A central connection (<c>()</c>).</summary>
    public const string Central = "()";

    /// <summary>A participant name (<c>ACTOR</c>).</summary>
    public const string Actor = "ACTOR";

    /// <summary>The <c>@{</c> of participant metadata.</summary>
    public const string ConfigStart = "CONFIG_START";

    /// <summary>The text of participant metadata.</summary>
    public const string ConfigContent = "CONFIG_CONTENT";

    /// <summary>The <c>}</c> of participant metadata.</summary>
    public const string ConfigEnd = "CONFIG_END";

    /// <summary>The text of a message or note, with its colon (<c>TXT</c>).</summary>
    public const string Txt = "TXT";

    /// <summary>The arrows: jison terminal name, text, and the line type the grammar maps it to, in lexer order.</summary>
    public static readonly IReadOnlyList<(string Kind, string Text, int LineType)> ArrowRules =
    [
        ("SOLID_ARROW", "->>", SequenceLineType.Solid),
        ("BIDIRECTIONAL_SOLID_ARROW", "<<->>", SequenceLineType.BidirectionalSolid),
        ("DOTTED_ARROW", "-->>", SequenceLineType.Dotted),
        ("BIDIRECTIONAL_DOTTED_ARROW", "<<-->>", SequenceLineType.BidirectionalDotted),
        ("SOLID_OPEN_ARROW", "->", SequenceLineType.SolidOpen),
        ("DOTTED_OPEN_ARROW", "-->", SequenceLineType.DottedOpen),
        ("SOLID_CROSS", "-x", SequenceLineType.SolidCross),
        ("DOTTED_CROSS", "--x", SequenceLineType.DottedCross),
        ("SOLID_POINT", "-)", SequenceLineType.SolidPoint),
        ("DOTTED_POINT", "--)", SequenceLineType.DottedPoint),
        ("SOLID_ARROW_TOP_DOTTED", "--|\\", SequenceLineType.SolidTopDotted),
        ("SOLID_ARROW_BOTTOM_DOTTED", "--|/", SequenceLineType.SolidBottomDotted),
        ("STICK_ARROW_TOP_DOTTED", "--\\\\", SequenceLineType.StickTopDotted),
        ("STICK_ARROW_BOTTOM_DOTTED", "--//", SequenceLineType.StickBottomDotted),
        ("SOLID_ARROW_TOP_REVERSE_DOTTED", "/|--", SequenceLineType.SolidArrowTopReverseDotted),
        ("SOLID_ARROW_BOTTOM_REVERSE_DOTTED", "\\|--", SequenceLineType.SolidArrowBottomReverseDotted),
        ("STICK_ARROW_TOP_REVERSE_DOTTED", "//--", SequenceLineType.StickArrowTopReverseDotted),
        ("STICK_ARROW_BOTTOM_REVERSE_DOTTED", "\\\\--", SequenceLineType.StickArrowBottomReverseDotted),
        ("SOLID_ARROW_TOP", "-|\\", SequenceLineType.SolidTop),
        ("SOLID_ARROW_BOTTOM", "-|/", SequenceLineType.SolidBottom),
        ("STICK_ARROW_TOP", "-\\\\", SequenceLineType.StickTop),
        ("STICK_ARROW_BOTTOM", "-//", SequenceLineType.StickBottom),
        ("SOLID_ARROW_TOP_REVERSE", "/|-", SequenceLineType.SolidArrowTopReverse),
        ("SOLID_ARROW_BOTTOM_REVERSE", "\\|-", SequenceLineType.SolidArrowBottomReverse),
        ("STICK_ARROW_TOP_REVERSE", "//-", SequenceLineType.StickArrowTopReverse),
        ("STICK_ARROW_BOTTOM_REVERSE", "\\\\-", SequenceLineType.StickArrowBottomReverse),
    ];

    /// <summary>The line type of each arrow kind.</summary>
    public static readonly FrozenDictionary<string, int> ArrowLineTypes =
        ArrowRules.ToFrozenDictionary(rule => rule.Kind, rule => rule.LineType, StringComparer.Ordinal);

    /// <summary>The terminal numbers of the jison grammar, which order the expected tokens of an error as jison lists them.</summary>
    private static readonly FrozenDictionary<string, int> Numbers = new Dictionary<string, int>
    {
        [Space] = 4,
        [Newline] = 5,
        [Sd] = 6,
        [Invalid] = 10,
        [Create] = 14,
        [Box] = 15,
        [RestOfLine] = 16,
        [End] = 17,
        [Autonumber] = 19,
        [Num] = 20,
        [Off] = 21,
        [Activate] = 22,
        [Deactivate] = 24,
        [Title] = 30,
        [LegacyTitle] = 31,
        [AccTitle] = 32,
        [AccTitleValue] = 33,
        [AccDescr] = 34,
        [AccDescrValue] = 35,
        [AccDescrMultilineValue] = 36,
        [Loop] = 37,
        [Rect] = 38,
        [Opt] = 39,
        [Alt] = 40,
        [Par] = 42,
        [ParOver] = 44,
        [Critical] = 45,
        [Break] = 47,
        [Option] = 48,
        [And] = 49,
        [Else] = 50,
        [Participant] = 51,
        [As] = 52,
        [ParticipantActor] = 53,
        [Destroy] = 54,
        [Note] = 56,
        [Over] = 59,
        [Links] = 61,
        [Link] = 62,
        [Properties] = 63,
        [Details] = 64,
        [Comma] = 66,
        [LeftOf] = 67,
        [RightOf] = 68,
        [Plus] = 70,
        [Minus] = 71,
        [Central] = 72,
        [Actor] = 73,
        [ConfigStart] = 75,
        [ConfigContent] = 76,
        [ConfigEnd] = 77,
        ["SOLID_OPEN_ARROW"] = 78,
        ["DOTTED_OPEN_ARROW"] = 79,
        ["SOLID_ARROW"] = 80,
        ["SOLID_ARROW_TOP"] = 81,
        ["SOLID_ARROW_BOTTOM"] = 82,
        ["STICK_ARROW_TOP"] = 83,
        ["STICK_ARROW_BOTTOM"] = 84,
        ["SOLID_ARROW_TOP_DOTTED"] = 85,
        ["SOLID_ARROW_BOTTOM_DOTTED"] = 86,
        ["STICK_ARROW_TOP_DOTTED"] = 87,
        ["STICK_ARROW_BOTTOM_DOTTED"] = 88,
        ["SOLID_ARROW_TOP_REVERSE"] = 89,
        ["SOLID_ARROW_BOTTOM_REVERSE"] = 90,
        ["STICK_ARROW_TOP_REVERSE"] = 91,
        ["STICK_ARROW_BOTTOM_REVERSE"] = 92,
        ["SOLID_ARROW_TOP_REVERSE_DOTTED"] = 93,
        ["SOLID_ARROW_BOTTOM_REVERSE_DOTTED"] = 94,
        ["STICK_ARROW_TOP_REVERSE_DOTTED"] = 95,
        ["STICK_ARROW_BOTTOM_REVERSE_DOTTED"] = 96,
        ["BIDIRECTIONAL_SOLID_ARROW"] = 97,
        ["DOTTED_ARROW"] = 98,
        ["BIDIRECTIONAL_DOTTED_ARROW"] = 99,
        ["SOLID_CROSS"] = 100,
        ["DOTTED_CROSS"] = 101,
        ["SOLID_POINT"] = 102,
        ["DOTTED_POINT"] = 103,
        [Txt] = 104,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    ///     Gets the jison terminal number of a token kind, which orders expected-token lists as jison prints them.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The number; kinds the grammar does not know sort last.</returns>
    public static int Number(string kind) => Numbers.TryGetValue(kind, out var number) ? number : int.MaxValue;
}

/// <content>
///     The lexer: a port of the 94 rules and the start conditions of the lexer that jison generates from Mermaid's
///     <c>sequenceDiagram.jison</c>, with hand-written matchers in place of its regular expressions.
/// </content>
internal sealed partial class SequenceParser
{
    /// <summary>
    ///     The start conditions of the lexer and the indexes of their rules, in the order jison tries them (the
    ///     <c>conditions</c> table of the generated lexer).
    /// </summary>
    private static readonly FrozenDictionary<string, IReadOnlyList<int>> Conditions = new Dictionary<string, IReadOnlyList<int>>
    {
        ["acc_descr_multiline"] = [53, 54],
        ["acc_descr"] = [51],
        ["acc_title"] = [49],
        ["ID"] = [2, 3, 7, 11, 12, 13, 14, 15],
        ["ALIAS"] = [2, 3, 21, 22],
        ["LINE"] = [2, 3, 34],
        ["CONFIG"] = [8, 9, 10],
        [SourceScanner.InitialState] =
        [
            0, 1, 3, 4, 5, 6, 16, 17, 18, 19, 20, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 35, 36, 37, 38, 39, 40, 41,
            42, 43, 44, 45, 46, 47, 48, 50, 52, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73,
            74, 75, 76, 77, 78, 79, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 92, 93,
        ],
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    ///     The texts after which a dash run may not continue a participant name: the negative lookahead of the
    ///     <c>ACTOR</c> rule (an arrow, a double dash or a central connection).
    /// </summary>
    private static readonly string[] ArrowStarts =
    [
        "-x", "--x", "-)", "--)", "-|\\", "-\\", "-/", "-//", "-|/", "/|-", "\\|-", "//-", "\\\\-", "--|\\", "--", "()",
    ];

    /// <summary>The rules, built once; they hold no state.</summary>
    private static readonly ScanRule[] Rules = CreateRules();

    /// <summary>
    ///     Creates the lexer of a text.
    /// </summary>
    /// <param name="text">The text, mapped to the original.</param>
    /// <returns>The scanner.</returns>
    private static SourceScanner CreateScanner(MappedText text) => new(text, Rules, Conditions);

    /// <summary>
    ///     Creates the 94 rules of the lexer, in jison order.
    /// </summary>
    /// <returns>The rules.</returns>
    private static ScanRule[] CreateRules()
    {
        // Actions
        static Func<SourceScanner, string?> Push(string state, string? kind) => s =>
        {
            s.Begin(state);
            return kind;
        };
        static Func<SourceScanner, string?> Pop(string? kind) => s =>
        {
            s.PopState();
            return kind;
        };
        static Func<SourceScanner, string?> Trim(string kind, int pops) => s =>
        {
            s.Text = Preprocessor.TrimJs(s.Text);
            for (var i = 0; i < pops; i++)
            {
                s.PopState();
            }

            return kind;
        };
        static string? Skip(SourceScanner s) => null;

        var rules = new List<ScanRule>
        {
            /* 0 */ new("newline", ScanPatterns.Run(c => c == '\n'), _ => SequenceTokens.Newline),
            /* 1 */ new("whitespace", ScanPatterns.Run(ScanPatterns.IsJsWhitespace), Skip),
            /* 2 */ new("lineWhitespace", ScanPatterns.Run(c => c != '\n' && ScanPatterns.IsJsWhitespace(c)), Skip),
            /* 3 */ new("hashComment", (s, p) => p < s.Length && s[p] == '#' ? RestOfLineFrom(s, p + 1) - p : -1, Skip),
            /* 4 */ new("percentComment", (s, p) => p < s.Length && s[p] == '%' && (p + 1 >= s.Length || s[p + 1] != '{') ? RestOfLineFrom(s, p + 1) - p : -1, Skip),
            /* 5 */ new("doublePercentComment", (s, p) => p + 2 < s.Length && s[p] != '}' && s[p + 1] == '%' && s[p + 2] == '%' ? RestOfLineFrom(s, p + 3) - p : -1, Skip),
            /* 6 */ new("number", MatchNumber, _ => SequenceTokens.Num),
            /* 7 */ new("configStart", Literal("@{"), Push("CONFIG", SequenceTokens.ConfigStart)),
            /* 8 */ new("configContent", ScanPatterns.Run(c => c != '}'), _ => SequenceTokens.ConfigContent),
            /* 9 */ new("configEndBeforeAs", (s, p) => p < s.Length && s[p] == '}' && IsAsAhead(s, p + 1) ? 1 : -1, s =>
            {
                s.PopState();
                s.Begin("ALIAS");
                return SequenceTokens.ConfigEnd;
            }),
            /* 10 */ new("configEnd", Literal("}"), s =>
            {
                s.PopState();
                s.PopState();
                return SequenceTokens.ConfigEnd;
            }),
            /* 11 */ new("actorBeforeConfig", MatchActorBeforeConfig, Trim(SequenceTokens.Actor, 0)),
            /* 12 */ new("actorBeforeAlias", MatchActorBeforeAlias, s =>
            {
                s.Text = Preprocessor.TrimJs(s.Text);
                s.Begin("ALIAS");
                return SequenceTokens.Actor;
            }),
            /* 13 */ new("actorToLineEnd", MatchActorToLineEnd, Trim(SequenceTokens.Actor, 1)),
            /* 14 */ new("invalidAngle", MatchInvalidAngle, Pop(SequenceTokens.Invalid)),
            /* 15 */ new("invalidRest", ScanPatterns.Run(c => c != '\n'), Trim(SequenceTokens.Invalid, 1)),
            /* 16 */ new("box", Keyword("box"), Push("LINE", SequenceTokens.Box)),
            /* 17 */ new("participant", Keyword("participant"), Push("ID", SequenceTokens.Participant)),
            /* 18 */ new("actor", Keyword("actor"), Push("ID", SequenceTokens.ParticipantActor)),
            /* 19 */ new("create", Keyword("create"), _ => SequenceTokens.Create),
            /* 20 */ new("destroy", Keyword("destroy"), Push("ID", SequenceTokens.Destroy)),
            /* 21 */ new("as", Keyword("as"), s =>
            {
                s.PopState();
                s.PopState();
                s.Begin("LINE");
                return SequenceTokens.As;
            }),
            /* 22 */ new("aliasEnd", (_, _) => 0, s =>
            {
                s.PopState();
                s.PopState();
                return SequenceTokens.Newline;
            }),
            /* 23 */ new("loop", Keyword("loop"), Push("LINE", SequenceTokens.Loop)),
            /* 24 */ new("rect", Keyword("rect"), Push("LINE", SequenceTokens.Rect)),
            /* 25 */ new("opt", Keyword("opt"), Push("LINE", SequenceTokens.Opt)),
            /* 26 */ new("alt", Keyword("alt"), Push("LINE", SequenceTokens.Alt)),
            /* 27 */ new("else", Keyword("else"), Push("LINE", SequenceTokens.Else)),
            /* 28 */ new("par", Keyword("par"), Push("LINE", SequenceTokens.Par)),
            /* 29 */ new("par_over", Keyword("par_over"), Push("LINE", SequenceTokens.ParOver)),
            /* 30 */ new("and", Keyword("and"), Push("LINE", SequenceTokens.And)),
            /* 31 */ new("critical", Keyword("critical"), Push("LINE", SequenceTokens.Critical)),
            /* 32 */ new("option", Keyword("option"), Push("LINE", SequenceTokens.Option)),
            /* 33 */ new("break", Keyword("break"), Push("LINE", SequenceTokens.Break)),
            /* 34 */ new("restOfLine", ScanPatterns.Run(c => c is not ('#' or '\n' or ';'), 0), Pop(SequenceTokens.RestOfLine)),
            /* 35 */ new("end", Keyword("end"), _ => SequenceTokens.End),
            /* 36 */ new("left of", Keyword("left of"), _ => SequenceTokens.LeftOf),
            /* 37 */ new("right of", Keyword("right of"), _ => SequenceTokens.RightOf),
            /* 38 */ new("links", Keyword("links"), _ => SequenceTokens.Links),
            /* 39 */ new("link", Keyword("link"), _ => SequenceTokens.Link),
            /* 40 */ new("properties", Keyword("properties"), _ => SequenceTokens.Properties),
            /* 41 */ new("details", Keyword("details"), _ => SequenceTokens.Details),
            /* 42 */ new("over", Keyword("over"), _ => SequenceTokens.Over),
            /* 43 */ new("note", Keyword("note"), _ => SequenceTokens.Note),
            /* 44 */ new("activate", Keyword("activate"), Push("ID", SequenceTokens.Activate)),
            /* 45 */ new("deactivate", Keyword("deactivate"), Push("ID", SequenceTokens.Deactivate)),
            /* 46 */ new("title", (s, p) => MatchTitle(s, p, legacy: false), _ => SequenceTokens.Title),
            /* 47 */ new("legacyTitle", (s, p) => MatchTitle(s, p, legacy: true), _ => SequenceTokens.LegacyTitle),
            /* 48 */ new("accTitle", (s, p) => MatchAccessibilityKeyword(s, p, "accTitle", ':'), Push("acc_title", SequenceTokens.AccTitle)),
            /* 49 */ new("accTitleValue", ScanPatterns.Run(c => c != '\n', 0), Pop(SequenceTokens.AccTitleValue)),
            /* 50 */ new("accDescr", (s, p) => MatchAccessibilityKeyword(s, p, "accDescr", ':'), Push("acc_descr", SequenceTokens.AccDescr)),
            /* 51 */ new("accDescrValue", ScanPatterns.Run(c => c != '\n', 0), Pop(SequenceTokens.AccDescrValue)),
            /* 52 */ new("accDescrMultiline", (s, p) => MatchAccessibilityKeyword(s, p, "accDescr", '{'), Push("acc_descr_multiline", null)),
            /* 53 */ new("accDescrMultilineEnd", Literal("}"), Pop(null)),
            /* 54 */ new("accDescrMultilineValue", ScanPatterns.Run(c => c != '}', 0), _ => SequenceTokens.AccDescrMultilineValue),
            /* 55 */ new("sequenceDiagram", Keyword("sequenceDiagram"), _ => SequenceTokens.Sd),
            /* 56 */ new("autonumber", Keyword("autonumber"), _ => SequenceTokens.Autonumber),
            /* 57 */ new("off", Keyword("off"), _ => SequenceTokens.Off),
            /* 58 */ new("comma", Literal(","), _ => SequenceTokens.Comma),
            /* 59 */ new("semicolon", Literal(";"), _ => SequenceTokens.Newline),
            /* 60 */ new("actor", MatchActor, Trim(SequenceTokens.Actor, 0)),
        };

        /* 61 to 86 */
        foreach (var (kind, text, _) in SequenceTokens.ArrowRules)
        {
            rules.Add(new ScanRule(kind, Literal(text)));
        }

        rules.AddRange(
        [
            /* 87 */ new("text", (s, p) => p < s.Length && s[p] == ':' ? TextEnd(s, p + 1) - p : -1, _ => SequenceTokens.Txt),
            /* 88 */ new("colon", Literal(":"), _ => SequenceTokens.Txt),
            /* 89 */ new("plus", Literal("+"), _ => SequenceTokens.Plus),
            /* 90 */ new("minus", Literal("-"), _ => SequenceTokens.Minus),
            /* 91 */ new("central", Literal("()"), _ => SequenceTokens.Central),
            /* 92 */ new("endOfInput", ScanPatterns.EndOfInput, _ => SequenceTokens.Newline),
            /* 93 */ new("invalid", (s, p) => p < s.Length && !ScanPatterns.IsJsLineTerminator(s[p]) ? 1 : -1, _ => SequenceTokens.Invalid),
        ]);
        return [.. rules];
    }

    /// <summary>
    ///     Creates a matcher of a fixed text, in which ASCII letters match in any case (the grammar's
    ///     <c>case-insensitive</c> option, which folds only ASCII letters onto each other).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The matcher.</returns>
    private static ScanMatcher Literal(string text) => (s, p) => StartsWithAt(s, p, text) ? text.Length : -1;

    /// <summary>
    ///     Creates a matcher of a keyword: the text followed by a word boundary (jison adds <c>\b</c> to a rule that ends
    ///     with a word character).
    /// </summary>
    /// <param name="text">The keyword.</param>
    /// <returns>The matcher.</returns>
    private static ScanMatcher Keyword(string text) => (s, p) =>
        StartsWithAt(s, p, text) && (p + text.Length >= s.Length || !ScanPatterns.IsJsWordChar(s[p + text.Length])) ? text.Length : -1;

    /// <summary>
    ///     Gets a value indicating whether a text occurs at a position, ASCII letters in any case.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <param name="text">The text.</param>
    /// <returns>Whether it does.</returns>
    private static bool StartsWithAt(string s, int p, string text)
    {
        if (p + text.Length > s.Length)
        {
            return false;
        }

        for (var i = 0; i < text.Length; i++)
        {
            var a = s[p + i];
            var b = text[i];
            if (a != b && !(char.IsAsciiLetter(a) && char.IsAsciiLetter(b) && (a | 0x20) == (b | 0x20)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Gets the end of the run of characters of a class that starts at a position.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <param name="predicate">The class.</param>
    /// <returns>The position after the run.</returns>
    private static int RunEnd(string s, int p, Func<char, bool> predicate)
    {
        while (p < s.Length && predicate(s[p]))
        {
            p++;
        }

        return p;
    }

    /// <summary>
    ///     Gets the end of the line from a position (<c>[^\n]*</c>).
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The position of the line feed, or the end of the input.</returns>
    private static int RestOfLineFrom(string s, int p) => RunEnd(s, p, c => c != '\n');

    /// <summary>
    ///     Gets the end of a message text (<c>[^#\n;]*</c>): the next comment, line feed or semicolon.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The position after the text.</returns>
    private static int TextEnd(string s, int p) => RunEnd(s, p, c => c is not ('#' or '\n' or ';'));

    /// <summary>
    ///     Matches a number of an <c>autonumber</c> statement:
    ///     <c>([0-9]+(\.[0-9]{1,2})?|\.[0-9]{1,2})(?=[ \n]+)</c>, with the regular expression's backtracking (a fraction
    ///     of two digits is shortened to one when the space would not follow).
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchNumber(string s, int p)
    {
        bool SpaceAt(int q) => q < s.Length && s[q] is ' ' or '\n';

        // The fraction: a dot and two digits, else one, each only when a space or line feed follows
        int Fraction(int dot)
        {
            if (dot >= s.Length || s[dot] != '.')
            {
                return -1;
            }

            var digits = Math.Min(RunEnd(s, dot + 1, char.IsAsciiDigit) - (dot + 1), 2);
            for (var n = digits; n >= 1; n--)
            {
                if (SpaceAt(dot + 1 + n))
                {
                    return dot + 1 + n;
                }
            }

            return -1;
        }

        var integerEnd = RunEnd(s, p, char.IsAsciiDigit);
        if (integerEnd > p)
        {
            var end = Fraction(integerEnd);
            if (end >= 0)
            {
                return end - p;
            }

            return SpaceAt(integerEnd) ? integerEnd - p : -1;
        }

        var fractionEnd = Fraction(p);
        return fractionEnd >= 0 ? fractionEnd - p : -1;
    }

    /// <summary>
    ///     Gets a value indicating whether <c>\s+as\s</c> follows a position (any case).
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>Whether it does.</returns>
    private static bool IsAsAhead(string s, int p)
    {
        var word = RunEnd(s, p, ScanPatterns.IsJsWhitespace);
        return word > p && StartsWithAt(s, word, "as") && word + 2 < s.Length && ScanPatterns.IsJsWhitespace(s[word + 2]);
    }

    /// <summary>
    ///     Matches a participant name before its metadata (<c>[^\&lt;-&gt;\-&gt;:\n,;@\s]+(?=@\{)</c>): no white space and none
    ///     of <c>&lt; = &gt; - : , ; @</c>.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchActorBeforeConfig(string s, int p)
    {
        var end = RunEnd(s, p, c => c is not ('<' or '=' or '>' or '-' or ':' or '\n' or ',' or ';' or '@') && !ScanPatterns.IsJsWhitespace(c));
        return end > p && end + 1 < s.Length && s[end] == '@' && s[end + 1] == '{' ? end - p : -1;
    }

    /// <summary>
    ///     Matches a participant name before an alias (<c>[^&lt;&gt;:\n,;@\s]+(?=\s+as\s)</c>).
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchActorBeforeAlias(string s, int p)
    {
        var end = RunEnd(s, p, c => c is not ('<' or '>' or ':' or '\n' or ',' or ';' or '@') && !ScanPatterns.IsJsWhitespace(c));
        return end > p && IsAsAhead(s, end) ? end - p : -1;
    }

    /// <summary>
    ///     Matches a participant name up to the end of its statement (<c>[^&lt;&gt;:\n,;@]+(?=\s*[\n;#]|$)</c>): the run
    ///     up to a line feed, a semicolon or the end of the input, or, when the run stops at another character, up to its
    ///     last <c>#</c> (where the regular expression's backtracking ends).
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchActorToLineEnd(string s, int p)
    {
        var end = RunEnd(s, p, c => c is not ('<' or '>' or ':' or '\n' or ',' or ';' or '@'));
        if (end == p)
        {
            return -1;
        }

        if (end >= s.Length || s[end] is '\n' or ';')
        {
            return end - p;
        }

        var hash = s.LastIndexOf('#', end - 1, end - p);
        return hash > p ? hash - p : -1;
    }

    /// <summary>
    ///     Matches a participant name with a <c>&lt;</c> in it, which is invalid (<c>[^&lt;&gt;:\n,;@]*&lt;[^\n]*</c>).
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchInvalidAngle(string s, int p)
    {
        var end = RunEnd(s, p, c => c is not ('<' or '>' or ':' or '\n' or ',' or ';' or '@'));
        return end < s.Length && s[end] == '<' ? RestOfLineFrom(s, end + 1) - p : -1;
    }

    /// <summary>
    ///     Matches a participant name in a statement (the <c>ACTOR</c> rule of the initial condition): a run without
    ///     <c>/ \ + ( ) &lt; - &gt; : , ;</c>, then runs that may also hold <c>/ \ ( )</c> and may start with dashes, as
    ///     long as no arrow, double dash or <c>()</c> starts there.
    /// </summary>
    /// <remarks>
    ///     The rule's regular expression nests a negative lookahead in a repeated group; written as this loop, the match
    ///     is the one the regular expression finds (greedy, first alternative first) in linear time.
    /// </remarks>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchActor(string s, int p)
    {
        var end = RunEnd(s, p, c => c is not ('/' or '\\' or '+' or '(' or ')' or '<' or '-' or '>' or ':' or '\n' or ',' or ';'));
        if (end == p)
        {
            return -1;
        }

        while (!ArrowStarts.Any(arrow => StartsWithAt(s, end, arrow)))
        {
            var dashes = RunEnd(s, end, c => c == '-');
            var next = RunEnd(s, dashes, c => c is not ('+' or '<' or '-' or '>' or ':' or '\n' or ',' or ';'));
            if (next == dashes)
            {
                break;
            }

            end = next;
        }

        return end - p;
    }

    /// <summary>
    ///     Matches a title statement: <c>title\s[^#\n;]+</c>, or <c>title:\s[^#\n;]+</c> for the legacy form.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <param name="legacy">Whether to match the legacy form with a colon.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchTitle(string s, int p, bool legacy)
    {
        var keyword = legacy ? "title:" : "title";
        var space = p + keyword.Length;
        if (!StartsWithAt(s, p, keyword) || space >= s.Length || !ScanPatterns.IsJsWhitespace(s[space]))
        {
            return -1;
        }

        var end = TextEnd(s, space + 1);
        return end > space + 1 ? end - p : -1;
    }

    /// <summary>
    ///     Matches an accessibility keyword: <c>accTitle\s*:\s*</c>, <c>accDescr\s*:\s*</c> or <c>accDescr\s*\{\s*</c>.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <param name="keyword">The keyword.</param>
    /// <param name="separator">The colon or brace.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchAccessibilityKeyword(string s, int p, string keyword, char separator)
    {
        if (!StartsWithAt(s, p, keyword))
        {
            return -1;
        }

        var mark = RunEnd(s, p + keyword.Length, ScanPatterns.IsJsWhitespace);
        return mark < s.Length && s[mark] == separator ? RunEnd(s, mark + 1, ScanPatterns.IsJsWhitespace) - p : -1;
    }
}
