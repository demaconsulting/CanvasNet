// Portions derived from mermaid (MIT); see third-party/README.md
using System.Collections.Frozen;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.State;

/// <summary>
///     The token kinds of the state grammar: the names of the terminals of Mermaid's <c>stateDiagram.jison</c>.
/// </summary>
/// <remarks>
///     The kinds are the jison terminal names, so messages and <see cref="MermaidSyntaxException.Expected"/> lists read
///     like Mermaid's parse errors. The lexer also produces two kinds the grammar does not know (<see cref="Invalid"/> and
///     <see cref="DefaultClassDefId"/>); either is a parse error, as in Mermaid. The grammar knows two kinds no lexer rule
///     produces where the grammar wants them (<see cref="Space"/>, and <see cref="Default"/> after <c>classDef</c>),
///     which only appear in the expected lists. The class only holds constants and is safe for concurrent use.
/// </remarks>
internal static class StateTokens
{
    /// <summary>A white-space token; the grammar has it, but no lexer rule produces it.</summary>
    public const string Space = "SPACE";

    /// <summary>One or more line feeds, or the end of the text in the initial condition.</summary>
    public const string Nl = "NL";

    /// <summary>The <c>stateDiagram</c> or <c>stateDiagram-v2</c> keyword with the white space after it.</summary>
    public const string Sd = "SD";

    /// <summary>A colon and the text after it (a state description or a transition label).</summary>
    public const string Descr = "DESCR";

    /// <summary>The <c>--&gt;</c> of a transition.</summary>
    public const string Arrow = "-->";

    /// <summary>The <c>hide empty description</c> statement.</summary>
    public const string HideEmpty = "HIDE_EMPTY";

    /// <summary>The <c>scale</c> keyword.</summary>
    public const string Scale = "scale";

    /// <summary>The number of a <c>scale</c> statement.</summary>
    public const string Width = "WIDTH";

    /// <summary>A word after the <c>state</c> keyword: the name of a composite state, or a word that is ignored.</summary>
    public const string CompositState = "COMPOSIT_STATE";

    /// <summary>The <c>{</c> of a composite state.</summary>
    public const string StructStart = "STRUCT_START";

    /// <summary>The <c>}</c> of a composite state.</summary>
    public const string StructStop = "STRUCT_STOP";

    /// <summary>The quoted description of <c>state "description" as id</c>, without the quotes.</summary>
    public const string StateDescr = "STATE_DESCR";

    /// <summary>The <c>as</c> keyword.</summary>
    public const string As = "AS";

    /// <summary>A state id.</summary>
    public const string Id = "ID";

    /// <summary>A fork state (<c>&lt;&lt;fork&gt;&gt;</c> or <c>[[fork]]</c>), with its id as the text.</summary>
    public const string Fork = "FORK";

    /// <summary>A join state (<c>&lt;&lt;join&gt;&gt;</c> or <c>[[join]]</c>), with its id as the text.</summary>
    public const string Join = "JOIN";

    /// <summary>A choice state (<c>&lt;&lt;choice&gt;&gt;</c> or <c>[[choice]]</c>), with its id as the text.</summary>
    public const string Choice = "CHOICE";

    /// <summary>The <c>--</c> between concurrent regions of a composite state.</summary>
    public const string Concurrent = "CONCURRENT";

    /// <summary>The <c>note</c> keyword.</summary>
    public const string Note = "note";

    /// <summary>The text of a note.</summary>
    public const string NoteText = "NOTE_TEXT";

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

    /// <summary>The <c>click</c> keyword.</summary>
    public const string Click = "CLICK";

    /// <summary>A double-quoted string, with its quotes.</summary>
    public const string Str = "STRING";

    /// <summary>The <c>href</c> keyword.</summary>
    public const string Href = "HREF";

    /// <summary>The <c>classDef</c> keyword.</summary>
    public const string ClassDef = "classDef";

    /// <summary>The class name of a <c>classDef</c> statement, with the white space after it.</summary>
    public const string ClassDefId = "CLASSDEF_ID";

    /// <summary>The styles of a <c>classDef</c> statement.</summary>
    public const string ClassDefStyleOpts = "CLASSDEF_STYLEOPTS";

    /// <summary>The <c>default</c> keyword.</summary>
    public const string Default = "DEFAULT";

    /// <summary>The <c>style</c> keyword.</summary>
    public const string StyleKeyword = "style";

    /// <summary>The state ids of a <c>style</c> statement, with the white space after them.</summary>
    public const string StyleIds = "STYLE_IDS";

    /// <summary>The styles of a <c>style</c> statement.</summary>
    public const string StyleDefStyleOpts = "STYLEDEF_STYLEOPTS";

    /// <summary>The <c>class</c> keyword.</summary>
    public const string ClassKeyword = "class";

    /// <summary>The state ids of a <c>class</c> statement.</summary>
    public const string ClassEntityIds = "CLASSENTITY_IDS";

    /// <summary>The class name of a <c>class</c> statement.</summary>
    public const string StyleClass = "STYLECLASS";

    /// <summary>A line with <c>direction TB</c> in it.</summary>
    public const string DirectionTb = "direction_tb";

    /// <summary>A line with <c>direction BT</c> in it.</summary>
    public const string DirectionBt = "direction_bt";

    /// <summary>A line with <c>direction RL</c> in it.</summary>
    public const string DirectionRl = "direction_rl";

    /// <summary>A line with <c>direction LR</c> in it.</summary>
    public const string DirectionLr = "direction_lr";

    /// <summary>The <c>[*]</c> of a start or end state.</summary>
    public const string EdgeState = "EDGE_STATE";

    /// <summary>The <c>:::</c> before a class name.</summary>
    public const string StyleSeparator = "STYLE_SEPARATOR";

    /// <summary>The <c>left of</c> of a note.</summary>
    public const string LeftOf = "left_of";

    /// <summary>The <c>right of</c> of a note.</summary>
    public const string RightOf = "right_of";

    /// <summary>Any other character, which the grammar does not accept.</summary>
    public const string Invalid = "INVALID";

    /// <summary>The <c>default</c> class name of a <c>classDef</c> statement, which the grammar does not accept.</summary>
    public const string DefaultClassDefId = "DEFAULT_CLASSDEF_ID";

    /// <summary>The jison terminal numbers of the kinds, which order the expected lists as jison prints them.</summary>
    private static readonly FrozenDictionary<string, int> Numbers = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        [Space] = 4,
        [Nl] = 5,
        [Sd] = 6,
        [Descr] = 14,
        [Arrow] = 15,
        [HideEmpty] = 16,
        [Scale] = 17,
        [Width] = 18,
        [CompositState] = 19,
        [StructStart] = 20,
        [StructStop] = 21,
        [StateDescr] = 22,
        [As] = 23,
        [Id] = 24,
        [Fork] = 25,
        [Join] = 26,
        [Choice] = 27,
        [Concurrent] = 28,
        [Note] = 29,
        [NoteText] = 31,
        [AccTitle] = 33,
        [AccTitleValue] = 34,
        [AccDescr] = 35,
        [AccDescrValue] = 36,
        [AccDescrMultilineValue] = 37,
        [Click] = 38,
        [Str] = 39,
        [Href] = 40,
        [ClassDef] = 41,
        [ClassDefId] = 42,
        [ClassDefStyleOpts] = 43,
        [Default] = 44,
        [StyleKeyword] = 45,
        [StyleIds] = 46,
        [StyleDefStyleOpts] = 47,
        [ClassKeyword] = 48,
        [ClassEntityIds] = 49,
        [StyleClass] = 50,
        [DirectionTb] = 51,
        [DirectionBt] = 52,
        [DirectionRl] = 53,
        [DirectionLr] = 54,
        [EdgeState] = 57,
        [StyleSeparator] = 58,
        [LeftOf] = 59,
        [RightOf] = 60,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    ///     Gets the jison terminal number of a token kind, which orders expected-token lists as jison prints them.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The number; kinds the grammar does not know sort last.</returns>
    public static int Number(string kind) => Numbers.TryGetValue(kind, out var number) ? number : int.MaxValue;
}

/// <content>
///     The lexer: a port of the 80 rules and the start conditions of the lexer that jison generates from Mermaid's
///     <c>stateDiagram.jison</c> (case-insensitive), with hand-written matchers in place of its regular expressions.
/// </content>
internal sealed partial class StateParser
{
    /// <summary>The word of a direction statement.</summary>
    private const string DirectionWord = "direction";

    /// <summary>
    ///     The start conditions of the lexer and the indexes of their rules, in the order jison tries them (the
    ///     <c>conditions</c> table of the generated lexer).
    /// </summary>
    private static readonly FrozenDictionary<string, IReadOnlyList<int>> Conditions = new Dictionary<string, IReadOnlyList<int>>
    {
        ["struct"] = [10, 11, 12, 23, 27, 30, 36, 43, 44, 45, 46, 56, 57, 58, 72, 73, 74, 75, 76, 77],
        ["FLOATING_NOTE_ID"] = [65],
        ["FLOATING_NOTE"] = [62, 63, 64],
        ["NOTE_TEXT"] = [67, 68],
        ["NOTE_ID"] = [66],
        ["NOTE"] = [59, 60, 61],
        ["STYLEDEF_STYLES"] = [32],
        ["STYLE"] = [31],
        ["CLASS_STYLE"] = [29],
        ["CLASS"] = [28],
        ["CLASSDEFID"] = [26],
        ["CLASSDEF"] = [24, 25],
        ["acc_descr_multiline"] = [21, 22],
        ["acc_descr"] = [19],
        ["acc_title"] = [17],
        ["SCALE"] = [14, 15, 34, 35],
        ["STATE_ID"] = [49],
        ["STATE_STRING"] = [50, 51],
        ["STATE"] = [10, 11, 12, 37, 38, 39, 40, 41, 42, 47, 48, 52, 53, 54, 55],
        [SourceScanner.InitialState] =
        [
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 11, 12, 13, 16, 18, 20, 23, 27, 30, 33, 36, 55, 58, 69, 70, 71, 72, 73, 74, 75,
            77, 78, 79,
        ],
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The cached last match of each line search (a direction or a pseudo-state marker), by search.</summary>
    private readonly Dictionary<string, int> _lineCache = new(StringComparer.Ordinal);

    /// <summary>The position the cached line of the line searches was computed from.</summary>
    private int _lineStart;

    /// <summary>The end of the cached line of the line searches (its first line terminator).</summary>
    private int _lineEnd;

    /// <summary>The position the cached brace search started from, or -1 before the first search.</summary>
    private int _braceFrom = -1;

    /// <summary>The end of the line of the cached brace search.</summary>
    private int _braceLineEnd;

    /// <summary>The first brace of the cached brace search, or -1 when its line has none.</summary>
    private int _braceAt = -1;

    /// <summary>
    ///     Creates the 80 rules of the lexer, in jison order.
    /// </summary>
    /// <remarks>
    ///     The rules are created per parser, because the line rules cache their searches of the current line.
    /// </remarks>
    /// <returns>The rules.</returns>
    private ScanRule[] CreateRules()
    {
        // Actions
        static Func<SourceScanner, string?> Token(string kind) => _ => kind;
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
        static Func<SourceScanner, string?> Switch(string state, string? kind) => s =>
        {
            s.PopState();
            s.Begin(state);
            return kind;
        };
        static Func<SourceScanner, string?> PseudoState(string kind, int markerLength) => s =>
        {
            s.PopState();
            s.Text = Preprocessor.TrimJs(s.Text[..^markerLength]);
            return kind;
        };
        static Func<SourceScanner, string?> IdThen(Func<SourceScanner, string?> action) => s => ProcessId(s) ? action(s) : null;
        static string? Skip(SourceScanner s) => null;

        return
        [
            /* 0 */ new("click", Keyword("click"), Token(StateTokens.Click)),
            /* 1 */ new("href", Keyword("href"), Token(StateTokens.Href)),
            /* 2 */ new("string", MatchString, Token(StateTokens.Str)),
            /* 3 */ new("default", Keyword("default"), Token(StateTokens.Default)),
            /* 4 */ new("directionTb", DirectionLine("TB"), Token(StateTokens.DirectionTb)),
            /* 5 */ new("directionBt", DirectionLine("BT"), Token(StateTokens.DirectionBt)),
            /* 6 */ new("directionRl", DirectionLine("RL"), Token(StateTokens.DirectionRl)),
            /* 7 */ new("directionLr", DirectionLine("LR"), Token(StateTokens.DirectionLr)),
            /* 8 */ new("newlines", Run(c => c == '\n', 1), Token(StateTokens.Nl)),
            /* 9 */ new("whitespace", Run(ScanPatterns.IsJsWhitespace, 1), Skip),
            /* 10 */ new("lineWhitespace", Run(c => c != '\n' && ScanPatterns.IsJsWhitespace(c), 1), Skip),
            /* 11 */ new("hashComment", (s, p) => p < s.Length && s[p] == '#' ? RestOfLine(s, p) - p : -1, Skip),
            /* 12 */ new("comment", MatchComment, Skip),
            /* 13 */ new("scale", KeywordThenSpaces("scale"), Push("SCALE", StateTokens.Scale)),
            /* 14 */ new("width", Run(char.IsAsciiDigit, 1), Token(StateTokens.Width)),
            /* 15 */ new("widthKeyword", MatchWidthKeyword, Pop(null)),
            /* 16 */ new("accTitle", (s, p) => MatchAccessibilityKeyword(s, p, "accTitle", ':'), Push("acc_title", StateTokens.AccTitle)),
            /* 17 */ new("accTitleValue", Run(c => c != '\n', 0), Pop(StateTokens.AccTitleValue)),
            /* 18 */ new("accDescr", (s, p) => MatchAccessibilityKeyword(s, p, "accDescr", ':'), Push("acc_descr", StateTokens.AccDescr)),
            /* 19 */ new("accDescrValue", Run(c => c != '\n', 0), Pop(StateTokens.AccDescrValue)),
            /* 20 */ new("accDescrMultiline", (s, p) => MatchAccessibilityKeyword(s, p, "accDescr", '{'), Push("acc_descr_multiline", null)),
            /* 21 */ new("accDescrMultilineEnd", Lit("}"), Pop(null)),
            /* 22 */ new("accDescrMultilineValue", Run(c => c != '}', 0), Token(StateTokens.AccDescrMultilineValue)),
            /* 23 */ new("classDef", KeywordThenSpaces("classDef"), Push("CLASSDEF", StateTokens.ClassDef)),
            /* 24 */ new("classDefDefault", KeywordThenSpaces("DEFAULT"), Switch("CLASSDEFID", StateTokens.DefaultClassDefId)),
            /* 25 */ new("classDefId", MatchWordThenSpaces, Switch("CLASSDEFID", StateTokens.ClassDefId)),
            /* 26 */ new("classDefStyles", Run(c => c != '\n', 0), Pop(StateTokens.ClassDefStyleOpts)),
            /* 27 */ new("class", KeywordThenSpaces("class"), Push("CLASS", StateTokens.ClassKeyword)),
            /* 28 */ new("classIds", MatchClassIds, Switch("CLASS_STYLE", StateTokens.ClassEntityIds)),
            /* 29 */ new("classStyle", Run(c => c != '\n', 0), Pop(StateTokens.StyleClass)),
            /* 30 */ new("style", KeywordThenSpaces("style"), Push("STYLE", StateTokens.StyleKeyword)),
            /* 31 */ new("styleIds", MatchStyleIds, Switch("STYLEDEF_STYLES", StateTokens.StyleIds)),
            /* 32 */ new("styleStyles", Run(c => c != '\n', 0), Pop(StateTokens.StyleDefStyleOpts)),
            /* 33 */ new("scaleAgain", KeywordThenSpaces("scale"), Push("SCALE", StateTokens.Scale)),
            /* 34 */ new("widthAgain", Run(char.IsAsciiDigit, 1), Token(StateTokens.Width)),
            /* 35 */ new("widthKeywordAgain", MatchWidthKeyword, Pop(null)),
            /* 36 */ new("state", KeywordThenSpaces("state"), Push("STATE", null)),
            /* 37 */ new("fork", PseudoStateLine("<<fork>>"), PseudoState(StateTokens.Fork, 8)),
            /* 38 */ new("join", PseudoStateLine("<<join>>"), PseudoState(StateTokens.Join, 8)),
            /* 39 */ new("choice", PseudoStateLine("<<choice>>"), PseudoState(StateTokens.Choice, 10)),
            /* 40 */ new("forkBrackets", PseudoStateLine("[[fork]]"), PseudoState(StateTokens.Fork, 8)),
            /* 41 */ new("joinBrackets", PseudoStateLine("[[join]]"), PseudoState(StateTokens.Join, 8)),
            /* 42 */ new("choiceBrackets", PseudoStateLine("[[choice]]"), PseudoState(StateTokens.Choice, 10)),
            /* 43 */ new("structDirectionTb", DirectionLine("TB"), Token(StateTokens.DirectionTb)),
            /* 44 */ new("structDirectionBt", DirectionLine("BT"), Token(StateTokens.DirectionBt)),
            /* 45 */ new("structDirectionRl", DirectionLine("RL"), Token(StateTokens.DirectionRl)),
            /* 46 */ new("structDirectionLr", DirectionLine("LR"), Token(StateTokens.DirectionLr)),
            /* 47 */ new("stateStringStart", Lit("\""), Push("STATE_STRING", null)),
            /* 48 */ new("stateAs", (s, p) => MatchAs(s, p, 1), Push("STATE_ID", StateTokens.As)),
            /* 49 */ new("stateId", Run(c => c is not ('\n' or '{'), 0), IdThen(Pop(StateTokens.Id))),
            /* 50 */ new("stateStringEnd", Lit("\""), Pop(null)),
            /* 51 */ new("stateString", Run(c => c != '"', 0), Token(StateTokens.StateDescr)),
            /* 52 */ new("stateWords", MatchStateWords, ThrowStateWords),
            /* 53 */ new("compositState", Run(c => c != '{' && !ScanPatterns.IsJsWhitespace(c), 1), Token(StateTokens.CompositState)),
            /* 54 */ new("stateNewline", Lit("\n"), Pop(null)),
            /* 55 */ new("structStart", Lit("{"), Switch("struct", StateTokens.StructStart)),
            /* 56 */ new("structStop", Lit("}"), Pop(StateTokens.StructStop)),
            /* 57 */ new("structNewline", Lit("\n"), Skip),
            /* 58 */ new("note", KeywordThenSpaces("note"), Push("NOTE", StateTokens.Note)),
            /* 59 */ new("leftOf", Keyword("left of"), Switch("NOTE_ID", StateTokens.LeftOf)),
            /* 60 */ new("rightOf", Keyword("right of"), Switch("NOTE_ID", StateTokens.RightOf)),
            /* 61 */ new("floatingNoteStart", Lit("\""), Switch("FLOATING_NOTE", null)),
            /* 62 */ new("floatingNoteAs", (s, p) => MatchAs(s, p, 0), Switch("FLOATING_NOTE_ID", StateTokens.As)),
            /* 63 */ new("floatingNoteQuote", Lit("\""), Skip),
            /* 64 */ new("floatingNoteText", Run(c => c != '"', 0), Token(StateTokens.NoteText)),
            /* 65 */ new("floatingNoteId", Run(c => c != '\n', 0), IdThen(Pop(StateTokens.Id))),
            /* 66 */ new("noteId", MatchNoteId, IdThen(Switch("NOTE_TEXT", StateTokens.Id))),
            /* 67 */ new("noteText", MatchNoteTextLine, s =>
            {
                s.PopState();
                s.Text = Preprocessor.TrimJs(s.Text[2..]);
                return StateTokens.NoteText;
            }),
            /* 68 */ new("noteTextBlock", MatchNoteTextBlock, s =>
            {
                s.PopState();
                s.Text = Preprocessor.TrimJs(s.Text[..^8]);
                return StateTokens.NoteText;
            }),
            /* 69 */ new("stateDiagram", KeywordThenSpaces("stateDiagram"), Token(StateTokens.Sd)),
            /* 70 */ new("stateDiagramV2", KeywordThenSpaces("stateDiagram-v2"), Token(StateTokens.Sd)),
            /* 71 */ new("hideEmpty", Keyword("hide empty description"), Token(StateTokens.HideEmpty)),
            /* 72 */ new("edgeState", Lit("[*]"), Token(StateTokens.EdgeState)),
            /* 73 */ new("id", Run(c => c is not (':' or '-' or '{') && !ScanPatterns.IsJsWhitespace(c), 1), IdThen(Token(StateTokens.Id))),
            /* 74 */ new("descr", MatchDescription, s =>
            {
                s.Text = Preprocessor.TrimJs(s.Text);
                return StateTokens.Descr;
            }),
            /* 75 */ new("arrow", Lit("-->"), Token(StateTokens.Arrow)),
            /* 76 */ new("concurrent", Lit("--"), Token(StateTokens.Concurrent)),
            /* 77 */ new("styleSeparator", Lit(":::"), Token(StateTokens.StyleSeparator)),
            /* 78 */ new("end", ScanPatterns.EndOfInput, Token(StateTokens.Nl)),
            /* 79 */ new("invalid", (s, p) => p < s.Length && !ScanPatterns.IsJsLineTerminator(s[p]) ? 1 : -1, Token(StateTokens.Invalid)),
        ];
    }

    /// <summary>
    ///     Splits an id from an inline comment (the lexer's <c>processId</c>): a match that starts with <c>%%</c> is
    ///     skipped, and a match with <c>%%</c> later in it keeps only the text before it, returning the rest to the input.
    /// </summary>
    /// <param name="scanner">The scanner, positioned after the match.</param>
    /// <returns>Whether the match is a token (false when it is skipped).</returns>
    private static bool ProcessId(SourceScanner scanner)
    {
        var index = scanner.Text.IndexOf("%%", StringComparison.Ordinal);
        if (index == 0)
        {
            return false;
        }

        if (index > 0)
        {
            scanner.Less(index);
        }

        return true;
    }

    /// <summary>
    ///     Throws the error of a state name of more than one word before a brace (the rule <c>\w+\s+\w+.*?\{</c>), with
    ///     Mermaid's message.
    /// </summary>
    /// <param name="scanner">The scanner, positioned after the match.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="MermaidSyntaxException">Always thrown, located at the name.</exception>
    private static string? ThrowStateWords(SourceScanner scanner) =>
        throw new MermaidSyntaxException(
            MermaidDiagnosticCodes.SyntaxError,
            $"Error: State name must be a single word. Found: \"{Preprocessor.TrimJs(scanner.Text)}\"",
            scanner.Locate(scanner.MatchStart),
            StateWordsHint);

    /// <summary>The hint of the error of a state name of more than one word, which tells it from a lexical error.</summary>
    private const string StateWordsHint =
        "write the name of a composite state as one word, or give it a description: state \"Two words\" as TwoWords { ... }";

    /// <summary>
    ///     Creates a matcher of a fixed text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The matcher.</returns>
    private static ScanMatcher Lit(string text) => (s, p) => Starts(s, p, text) ? text.Length : -1;

    /// <summary>
    ///     Creates a matcher of a keyword: the text in any case, followed by a word boundary (jison adds <c>\b</c> to a
    ///     rule that ends with a word character).
    /// </summary>
    /// <param name="text">The keyword.</param>
    /// <returns>The matcher.</returns>
    private static ScanMatcher Keyword(string text) => (s, p) =>
        Starts(s, p, text) && (p + text.Length >= s.Length || !ScanPatterns.IsJsWordChar(s[p + text.Length])) ? text.Length : -1;

    /// <summary>
    ///     Creates a matcher of a keyword in any case followed by at least one white-space character (<c>keyword\s+</c>).
    /// </summary>
    /// <param name="text">The keyword.</param>
    /// <returns>The matcher.</returns>
    private static ScanMatcher KeywordThenSpaces(string text) => (s, p) =>
    {
        if (!Starts(s, p, text))
        {
            return -1;
        }

        var end = SkipSpaces(s, p + text.Length);
        return end > p + text.Length ? end - p : -1;
    };

    /// <summary>
    ///     Creates a matcher of a run of characters of a class.
    /// </summary>
    /// <param name="predicate">The class.</param>
    /// <param name="minimum">The fewest characters the run must have.</param>
    /// <returns>The matcher.</returns>
    private static ScanMatcher Run(Func<char, bool> predicate, int minimum) => ScanPatterns.Run(predicate, minimum);

    /// <summary>
    ///     Matches a double-quoted string (<c>"[^"]*"</c>).
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchString(string s, int p)
    {
        if (p >= s.Length || s[p] != '"')
        {
            return -1;
        }

        var end = s.IndexOf('"', p + 1);
        return end < 0 ? -1 : end + 1 - p;
    }

    /// <summary>
    ///     Matches a comment (<c>%%(?!\{)[^\n]*</c>): <c>%%</c> not followed by a brace, and the rest of the line.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchComment(string s, int p) =>
        Starts(s, p, "%%") && !Starts(s, p + 2, "{") ? RestOfLine(s, p) - p : -1;

    /// <summary>
    ///     Matches the end of a <c>scale</c> statement (<c>\s+width\b</c>).
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchWidthKeyword(string s, int p)
    {
        var start = SkipSpaces(s, p);
        if (start == p)
        {
            return -1;
        }

        var length = Keyword("width")(s, start);
        return length < 0 ? -1 : start + length - p;
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
        if (!Starts(s, p, keyword))
        {
            return -1;
        }

        var mark = SkipSpaces(s, p + keyword.Length);
        return mark < s.Length && s[mark] == separator ? SkipSpaces(s, mark + 1) - p : -1;
    }

    /// <summary>
    ///     Matches a word and the white space after it (<c>\w+\s+</c>).
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchWordThenSpaces(string s, int p)
    {
        var word = SkipWord(s, p);
        if (word == p)
        {
            return -1;
        }

        var end = SkipSpaces(s, word);
        return end > word ? end - p : -1;
    }

    /// <summary>
    ///     Matches the state ids of a <c>class</c> statement (<c>(\w+)+((,\s*\w+)*)</c>): words separated by commas, with
    ///     optional white space after each comma.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchClassIds(string s, int p)
    {
        var end = SkipWord(s, p);
        if (end == p)
        {
            return -1;
        }

        while (end < s.Length && s[end] == ',')
        {
            var start = SkipSpaces(s, end + 1);
            var word = SkipWord(s, start);
            if (word == start)
            {
                break;
            }

            end = word;
        }

        return end - p;
    }

    /// <summary>
    ///     Matches the state ids of a <c>style</c> statement and the white space after them (<c>[\w,]+\s+</c>).
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchStyleIds(string s, int p)
    {
        var ids = p;
        while (ids < s.Length && (s[ids] == ',' || ScanPatterns.IsJsWordChar(s[ids])))
        {
            ids++;
        }

        var end = SkipSpaces(s, ids);
        return ids > p && end > ids ? end - p : -1;
    }

    /// <summary>
    ///     Matches the <c>as</c> of a state or floating note, with white space around it: <c>\s*as\s+</c> (at least one
    ///     white-space character after it) or <c>\s*as\s*</c>.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <param name="spacesAfter">The fewest white-space characters after <c>as</c>: 1 or 0.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchAs(string s, int p, int spacesAfter)
    {
        var start = SkipSpaces(s, p);
        if (!Starts(s, start, "as"))
        {
            return -1;
        }

        var end = SkipSpaces(s, start + 2);
        return end - (start + 2) >= spacesAfter ? end - p : -1;
    }

    /// <summary>
    ///     Matches the id of a note (<c>\s*[^:\n\s\-]+</c>), with the white space before it.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchNoteId(string s, int p)
    {
        var start = SkipSpaces(s, p);
        var end = start;
        while (end < s.Length && s[end] is not (':' or '-') && !ScanPatterns.IsJsWhitespace(s[end]))
        {
            end++;
        }

        return end > start ? end - p : -1;
    }

    /// <summary>
    ///     Matches the text of a one-line note (<c>\s*:[^:\n;]+</c>).
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchNoteTextLine(string s, int p)
    {
        var colon = SkipSpaces(s, p);
        if (colon >= s.Length || s[colon] != ':')
        {
            return -1;
        }

        var end = colon + 1;
        while (end < s.Length && s[end] is not (':' or '\n' or ';'))
        {
            end++;
        }

        return end > colon + 1 ? end - p : -1;
    }

    /// <summary>
    ///     Matches the text of a multi-line note (<c>[\s\S]*?\n\s*end note\b</c>): everything up to the first line that
    ///     starts, after white space, with <c>end note</c>.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchNoteTextBlock(string s, int p)
    {
        var endNote = Keyword("end note");
        for (var lineFeed = s.IndexOf('\n', p); lineFeed >= 0; lineFeed = s.IndexOf('\n', lineFeed + 1))
        {
            var start = SkipSpaces(s, lineFeed + 1);
            var length = endNote(s, start);
            if (length >= 0)
            {
                return start + length - p;
            }
        }

        return -1;
    }

    /// <summary>
    ///     Matches a description or transition label (<c>\s*:(?:[^:\n;]|:[^:\n;])+</c>): a colon and at least one character
    ///     that is not a colon, a line feed or a semicolon, where a colon may stand before such a character.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchDescription(string s, int p)
    {
        var colon = SkipSpaces(s, p);
        if (colon >= s.Length || s[colon] != ':')
        {
            return -1;
        }

        var end = colon + 1;
        while (end < s.Length)
        {
            if (s[end] is not (':' or '\n' or ';'))
            {
                end++;
            }
            else if (s[end] == ':' && end + 1 < s.Length && s[end + 1] is not (':' or '\n' or ';'))
            {
                end += 2;
            }
            else
            {
                break;
            }
        }

        return end > colon + 1 ? end - p : -1;
    }

    /// <summary>
    ///     Gets a value indicating whether a text starts at a position, comparing ASCII letters in any case (the grammar is
    ///     case-insensitive, and JavaScript's case-insensitive matching never folds a character outside ASCII to one in
    ///     it).
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <param name="text">The text.</param>
    /// <returns>Whether it does.</returns>
    private static bool Starts(string s, int p, string text)
    {
        if (p < 0 || p + text.Length > s.Length)
        {
            return false;
        }

        for (var i = 0; i < text.Length; i++)
        {
            var c = s[p + i];
            var t = text[i];
            if (c != t && !(char.IsAsciiLetter(c) && char.IsAsciiLetter(t) && (c | 0x20) == (t | 0x20)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Skips JavaScript white space (<c>\s*</c>).
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The position after the white space.</returns>
    private static int SkipSpaces(string s, int p)
    {
        while (p < s.Length && ScanPatterns.IsJsWhitespace(s[p]))
        {
            p++;
        }

        return p;
    }

    /// <summary>
    ///     Skips word characters (<c>\w*</c>).
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The position after the word.</returns>
    private static int SkipWord(string s, int p)
    {
        while (p < s.Length && ScanPatterns.IsJsWordChar(s[p]))
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
    private static int RestOfLine(string s, int p)
    {
        var end = s.IndexOf('\n', Math.Min(p, s.Length));
        return end < 0 ? s.Length : end;
    }

    /// <summary>
    ///     Gets the end of the line a position is on for <c>.</c>: the first line terminator at or after it.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The position of the line terminator, or the end of the input.</returns>
    private static int LineEnd(string s, int p)
    {
        while (p < s.Length && !ScanPatterns.IsJsLineTerminator(s[p]))
        {
            p++;
        }

        return p;
    }

    /// <summary>
    ///     Matches the name of a composite state of two or more words before a brace (<c>\w+\s+\w+.*?\{</c>): a word,
    ///     white space (which may span lines), a word, and the first brace after it on the same line.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private int MatchStateWords(string s, int p)
    {
        var first = SkipWord(s, p);
        var spaces = SkipSpaces(s, first);
        var second = SkipWord(s, spaces);
        if (first == p || spaces == first || second == spaces)
        {
            return -1;
        }

        var brace = FirstBraceOnLine(s, second);
        return brace < 0 ? -1 : brace + 1 - p;
    }

    /// <summary>
    ///     Finds the first brace from a position up to the end of its line, cached so that lexing a long line token by
    ///     token stays linear.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The position of the brace, or -1.</returns>
    private int FirstBraceOnLine(string s, int p)
    {
        // A later position on the cached line has the same answer while it is not past the brace found
        if (_braceFrom < 0 || p < _braceFrom || p > _braceLineEnd || (_braceAt >= 0 && p > _braceAt))
        {
            _braceFrom = p;
            _braceLineEnd = LineEnd(s, p);
            _braceAt = s.IndexOf('{', p, _braceLineEnd - p);
        }

        return _braceAt;
    }

    /// <summary>
    ///     Creates the matcher of a direction rule (<c>.*direction\s+XX[^\n]*</c>): a line that has the direction anywhere
    ///     in it from the position on is one token, up to the line feed after the direction.
    /// </summary>
    /// <param name="direction">The direction (<c>TB</c>, <c>BT</c>, <c>RL</c> or <c>LR</c>).</param>
    /// <returns>The matcher.</returns>
    private ScanMatcher DirectionLine(string direction) => (s, p) =>
    {
        var occurrence = LastOnLine(s, p, DirectionWord + " " + direction, k =>
        {
            if (!Starts(s, k, DirectionWord))
            {
                return false;
            }

            var spaces = SkipSpaces(s, k + DirectionWord.Length);
            return spaces > k + DirectionWord.Length && Starts(s, spaces, direction);
        });
        if (occurrence < p)
        {
            return -1;
        }

        // The match runs to the line feed after the direction (the white space before it may span lines)
        var after = SkipSpaces(s, occurrence + DirectionWord.Length) + direction.Length;
        return RestOfLine(s, after) - p;
    };

    /// <summary>
    ///     Creates the matcher of a pseudo-state rule (<c>.*&lt;&lt;fork&gt;&gt;</c> and the like): the text up to the last
    ///     marker on the line.
    /// </summary>
    /// <param name="marker">The marker, such as <c>&lt;&lt;fork&gt;&gt;</c>.</param>
    /// <returns>The matcher.</returns>
    private ScanMatcher PseudoStateLine(string marker) => (s, p) =>
    {
        var occurrence = LastOnLine(s, p, marker, k => k + marker.Length <= _lineEnd && Starts(s, k, marker));
        return occurrence < p ? -1 : occurrence + marker.Length - p;
    };

    /// <summary>
    ///     Finds the last position on the line of a position where a search matches (the greedy <c>.*</c> before it takes
    ///     the last), cached per line and search so that lexing a long line token by token stays linear.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <param name="key">The key of the search in the cache.</param>
    /// <param name="matchesAt">Whether the search matches at a position.</param>
    /// <returns>The position, or -1.</returns>
    private int LastOnLine(string s, int p, string key, Func<int, bool> matchesAt)
    {
        // The line of the position ends at the first line terminator ('.' does not match one)
        if (p < _lineStart || p >= _lineEnd)
        {
            _lineStart = p;
            _lineEnd = LineEnd(s, p);
            _lineCache.Clear();
        }

        if (!_lineCache.TryGetValue(key, out var found))
        {
            found = -1;
            for (var k = _lineEnd - 1; k >= _lineStart; k--)
            {
                if (matchesAt(k))
                {
                    found = k;
                    break;
                }
            }

            _lineCache[key] = found;
        }

        return found;
    }
}
