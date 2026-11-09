// Portions derived from mermaid (MIT); see third-party/README.md
using System.Collections.Frozen;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Class;

/// <summary>
///     The token kinds of the class grammar: the names of the terminals of Mermaid's <c>classDiagram.jison</c>.
/// </summary>
/// <remarks>
///     The kinds are the jison terminal names, so messages and <see cref="MermaidSyntaxException.Expected"/> lists read
///     like Mermaid's parse errors. The one exception is <see cref="Eof"/>, the <c>EOF</c> terminal that the lexer produces
///     once at the end of the text, which must be told apart from <see cref="SourceScanner.EndOfInput"/> (the end of the
///     token stream, jison's <c>$end</c>). The lexer also produces kinds the grammar does not use (<see cref="EdgeState"/>,
///     <see cref="EofInStruct"/>, <see cref="OpenInStruct"/>, <see cref="Plus"/>, <see cref="EqualsSign"/> and
///     <see cref="Punctuation"/>); any of them is a parse error, as in Mermaid. The class only holds constants and is safe
///     for concurrent use.
/// </remarks>
internal static class ClassTokens
{
    /// <summary>The <c>classDiagram</c> or <c>classDiagram-v2</c> keyword.</summary>
    public const string ClassDiagram = "CLASS_DIAGRAM";

    /// <summary>One or more line breaks, with the white space around them.</summary>
    public const string Newline = "NEWLINE";

    /// <summary>The <c>EOF</c> terminal, produced once when the lexer reaches the end of the text.</summary>
    public const string Eof = "EOF_TERMINAL";

    /// <summary>The <c>[</c> of a class or namespace label.</summary>
    public const string Sqs = "SQS";

    /// <summary>The text of a double-quoted string, without the quotes.</summary>
    public const string Str = "STR";

    /// <summary>The <c>]</c> of a class or namespace label.</summary>
    public const string Sqe = "SQE";

    /// <summary>A dot in a class or namespace name.</summary>
    public const string Dot = "DOT";

    /// <summary>The text between two tildes (a generic type).</summary>
    public const string GenericType = "GENERICTYPE";

    /// <summary>A colon and the text after it, up to a colon, a line feed or a semicolon.</summary>
    public const string Label = "LABEL";

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

    /// <summary>The <c>{</c> of a class body or a namespace.</summary>
    public const string StructStart = "STRUCT_START";

    /// <summary>The <c>}</c> of a class body or a namespace.</summary>
    public const string StructStop = "STRUCT_STOP";

    /// <summary>The <c>namespace</c> keyword.</summary>
    public const string Namespace = "NAMESPACE";

    /// <summary>The <c>:::</c> before a CSS class.</summary>
    public const string StyleSeparator = "STYLE_SEPARATOR";

    /// <summary>The <c>&lt;&lt;</c> of an annotation.</summary>
    public const string AnnotationStart = "ANNOTATION_START";

    /// <summary>The <c>&gt;&gt;</c> of an annotation.</summary>
    public const string AnnotationEnd = "ANNOTATION_END";

    /// <summary>The <c>class</c> keyword.</summary>
    public const string Class = "CLASS";

    /// <summary>A white-space character; the grammar has it, but the lexer skips white space before it is reached.</summary>
    public const string Space = "SPACE";

    /// <summary>A line of a class body.</summary>
    public const string Member = "MEMBER";

    /// <summary>A separator statement; the grammar has it, but no lexer rule produces it.</summary>
    public const string Separator = "SEPARATOR";

    /// <summary>The <c>note for</c> keyword.</summary>
    public const string NoteFor = "NOTE_FOR";

    /// <summary>The <c>note</c> keyword.</summary>
    public const string Note = "NOTE";

    /// <summary>The <c>classDef</c> keyword.</summary>
    public const string ClassDef = "CLASSDEF";

    /// <summary>A run of word characters (<c>\w+</c>).</summary>
    public const string Alpha = "ALPHA";

    /// <summary>A comma.</summary>
    public const string Comma = "COMMA";

    /// <summary>A line with <c>direction TB</c> in it.</summary>
    public const string DirectionTb = "direction_tb";

    /// <summary>A line with <c>direction BT</c> in it.</summary>
    public const string DirectionBt = "direction_bt";

    /// <summary>A line with <c>direction RL</c> in it.</summary>
    public const string DirectionRl = "direction_rl";

    /// <summary>A line with <c>direction LR</c> in it.</summary>
    public const string DirectionLr = "direction_lr";

    /// <summary>The <c>o</c> end of an aggregation.</summary>
    public const string Aggregation = "AGGREGATION";

    /// <summary>The <c>&lt;|</c> or <c>|&gt;</c> end of an extension.</summary>
    public const string Extension = "EXTENSION";

    /// <summary>The <c>*</c> end of a composition.</summary>
    public const string Composition = "COMPOSITION";

    /// <summary>The <c>&lt;</c> or <c>&gt;</c> end of a dependency.</summary>
    public const string Dependency = "DEPENDENCY";

    /// <summary>The <c>()</c> end of a lollipop.</summary>
    public const string Lollipop = "LOLLIPOP";

    /// <summary>The <c>--</c> of a solid relation.</summary>
    public const string Line = "LINE";

    /// <summary>The <c>..</c> of a dashed relation.</summary>
    public const string DottedLine = "DOTTED_LINE";

    /// <summary>The <c>callback</c> keyword.</summary>
    public const string Callback = "CALLBACK";

    /// <summary>The <c>link</c> keyword.</summary>
    public const string Link = "LINK";

    /// <summary>A link target: <c>_self</c>, <c>_blank</c>, <c>_parent</c> or <c>_top</c>.</summary>
    public const string LinkTarget = "LINK_TARGET";

    /// <summary>The <c>click</c> keyword.</summary>
    public const string Click = "CLICK";

    /// <summary>The function name after <c>call</c>.</summary>
    public const string CallbackName = "CALLBACK_NAME";

    /// <summary>The arguments of a <c>call</c> function, without the parentheses.</summary>
    public const string CallbackArgs = "CALLBACK_ARGS";

    /// <summary>The <c>href</c> keyword.</summary>
    public const string Href = "HREF";

    /// <summary>The <c>style</c> keyword.</summary>
    public const string Style = "STYLE";

    /// <summary>The <c>cssClass</c> keyword.</summary>
    public const string CssClass = "CSSCLASS";

    /// <summary>A number; the grammar has it, but the <c>ALPHA</c> rule takes digits first.</summary>
    public const string Num = "NUM";

    /// <summary>A colon that does not start a label.</summary>
    public const string Colon = "COLON";

    /// <summary>A unit; the grammar has it, but no lexer rule produces it.</summary>
    public const string Unit = "UNIT";

    /// <summary>A <c>#</c>.</summary>
    public const string Brkt = "BRKT";

    /// <summary>A <c>%</c>.</summary>
    public const string Pct = "PCT";

    /// <summary>A minus, which is part of a name.</summary>
    public const string Minus = "MINUS";

    /// <summary>One letter outside ASCII.</summary>
    public const string UnicodeText = "UNICODE_TEXT";

    /// <summary>The text of a backticked name, without the backticks.</summary>
    public const string BquoteStr = "BQUOTE_STR";

    /// <summary>The <c>[*]</c> of a state diagram, which the class grammar does not accept.</summary>
    public const string EdgeState = "EDGE_STATE";

    /// <summary>The end of the text inside a class body or a namespace.</summary>
    public const string EofInStruct = "EOF_IN_STRUCT";

    /// <summary>A <c>{</c> inside a class body.</summary>
    public const string OpenInStruct = "OPEN_IN_STRUCT";

    /// <summary>A plus.</summary>
    public const string Plus = "PLUS";

    /// <summary>An equals sign (<c>EQUALS</c>).</summary>
    public const string EqualsSign = "EQUALS";

    /// <summary>Another punctuation character.</summary>
    public const string Punctuation = "PUNCTUATION";

    /// <summary>The terminal numbers of the jison grammar, which order the expected tokens of an error as jison lists them.</summary>
    private static readonly FrozenDictionary<string, int> Numbers = new Dictionary<string, int>
    {
        [ClassDiagram] = 7,
        [Newline] = 8,
        [Eof] = 9,
        [Sqs] = 12,
        [Str] = 13,
        [Sqe] = 14,
        [Dot] = 18,
        [GenericType] = 20,
        [Label] = 22,
        [AccTitle] = 33,
        [AccTitleValue] = 34,
        [AccDescr] = 35,
        [AccDescrValue] = 36,
        [AccDescrMultilineValue] = 37,
        [StructStart] = 39,
        [StructStop] = 41,
        [Namespace] = 42,
        [StyleSeparator] = 44,
        [AnnotationStart] = 46,
        [AnnotationEnd] = 47,
        [Class] = 48,
        [Space] = 50,
        [Member] = 51,
        [Separator] = 52,
        [NoteFor] = 54,
        [Note] = 56,
        [ClassDef] = 57,
        [Alpha] = 60,
        [Comma] = 61,
        [DirectionTb] = 62,
        [DirectionBt] = 63,
        [DirectionRl] = 64,
        [DirectionLr] = 65,
        [Aggregation] = 68,
        [Extension] = 69,
        [Composition] = 70,
        [Dependency] = 71,
        [Lollipop] = 72,
        [Line] = 73,
        [DottedLine] = 74,
        [Callback] = 75,
        [Link] = 76,
        [LinkTarget] = 77,
        [Click] = 78,
        [CallbackName] = 79,
        [CallbackArgs] = 80,
        [Href] = 81,
        [Style] = 82,
        [CssClass] = 83,
        [Num] = 86,
        [Colon] = 87,
        [Unit] = 88,
        [Brkt] = 89,
        [Pct] = 90,
        [Minus] = 100,
        [UnicodeText] = 102,
        [BquoteStr] = 103,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    ///     Gets the jison name of a token kind, as Mermaid's parse errors print it.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The name: the kind itself, or <c>EOF</c> for <see cref="Eof"/>.</returns>
    public static string JisonName(string kind) => kind == Eof ? "EOF" : kind;

    /// <summary>
    ///     Gets the jison terminal number of a token kind, which orders expected-token lists as jison prints them.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The number; kinds the grammar does not know sort last.</returns>
    public static int Number(string kind) => Numbers.TryGetValue(kind, out var number) ? number : int.MaxValue;
}

/// <content>
///     The lexer: a port of the 99 rules and the start conditions of the lexer that jison generates from Mermaid's
///     <c>classDiagram.jison</c>, with hand-written matchers in place of its regular expressions.
/// </content>
internal sealed partial class ClassParser
{
    /// <summary>The word of a direction statement.</summary>
    private const string DirectionWord = "direction";

    /// <summary>
    ///     The rules every start condition other than the initial one ends with (jison's <c>&lt;*&gt;</c> rules: the
    ///     keywords, generics, backticks, link targets, relation ends, labels, names and the end of the text).
    /// </summary>
    private static readonly int[] SharedRules =
    [
        50, 51, 52, 53, 54, 55, 56, 57, 58, 61, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82,
        87, 88, 89, 90, 91, 92, 93, 94, 95, 96, 98,
    ];

    /// <summary>
    ///     The start conditions of the lexer and the indexes of their rules, in the order jison tries them (the
    ///     <c>conditions</c> table of the generated lexer).
    /// </summary>
    private static readonly FrozenDictionary<string, IReadOnlyList<int>> Conditions = new Dictionary<string, IReadOnlyList<int>>
    {
        ["namespace-body"] = [26, 29, 34, 35, 36, 37, 38, 39, .. SharedRules],
        ["namespace"] = [26, 29, 30, 31, 32, 33, .. SharedRules],
        ["class-body"] = [26, 44, 45, 46, 47, 48, 49, .. SharedRules],
        ["class"] = [26, 40, 41, 42, 43, .. SharedRules],
        ["acc_descr_multiline"] = [11, 12, 26, .. SharedRules],
        ["acc_descr"] = [9, 26, .. SharedRules],
        ["acc_title"] = [7, 26, .. SharedRules],
        ["callback_args"] = [22, 23, 26, .. SharedRules],
        ["callback_name"] = [19, 20, 21, 26, .. SharedRules],
        ["href"] = [26, .. SharedRules],
        ["struct"] = [26, .. SharedRules],
        ["generic"] = [26, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, .. SharedRules[9..]],
        ["bqstring"] = [26, 50, 51, 52, 53, 54, 55, 56, 57, 58, 61, 62, 63, .. SharedRules[10..]],
        ["string"] = [24, 25, 26, .. SharedRules],
        [SourceScanner.InitialState] =
        [
            0, 1, 2, 3, 4, 5, 6, 8, 10, 13, 14, 15, 16, 17, 18, 26, 27, 28, 29, 39, 50, 51, 52, 53, 54, 55, 56, 57, 58, 61,
            64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91,
            92, 93, 94, 95, 96, 97, 98,
        ],
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The cached last match of <c>direction\s+XX</c> on the current line, by direction.</summary>
    private readonly Dictionary<string, int> _directionCache = new(StringComparer.Ordinal);

    /// <summary>The position the cached line of the direction rules was computed from.</summary>
    private int _directionLineStart;

    /// <summary>The end of the cached line of the direction rules (its first line terminator).</summary>
    private int _directionLineEnd;

    /// <summary>
    ///     Creates the 99 rules of the lexer, in jison order.
    /// </summary>
    /// <remarks>
    ///     The rules are created per parser, because the direction rules cache their search of the current line.
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
        static string? Skip(SourceScanner s) => null;

        return
        [
            /* 0 */ new("directionTb", DirectionLine("TB"), Token(ClassTokens.DirectionTb)),
            /* 1 */ new("directionBt", DirectionLine("BT"), Token(ClassTokens.DirectionBt)),
            /* 2 */ new("directionRl", DirectionLine("RL"), Token(ClassTokens.DirectionRl)),
            /* 3 */ new("directionLr", DirectionLine("LR"), Token(ClassTokens.DirectionLr)),
            /* 4 */ new("comment", MatchComment, Skip),
            /* 5 */ new("commentLines", MatchCommentLines, Skip),
            /* 6 */ new("accTitle", (s, p) => MatchAccessibilityKeyword(s, p, "accTitle", ':'), Push("acc_title", ClassTokens.AccTitle)),
            /* 7 */ new("accTitleValue", Run(c => c != '\n', 0), Pop(ClassTokens.AccTitleValue)),
            /* 8 */ new("accDescr", (s, p) => MatchAccessibilityKeyword(s, p, "accDescr", ':'), Push("acc_descr", ClassTokens.AccDescr)),
            /* 9 */ new("accDescrValue", Run(c => c != '\n', 0), Pop(ClassTokens.AccDescrValue)),
            /* 10 */ new("accDescrMultiline", (s, p) => MatchAccessibilityKeyword(s, p, "accDescr", '{'), Push("acc_descr_multiline", null)),
            /* 11 */ new("accDescrMultilineEnd", Lit("}"), Pop(null)),
            /* 12 */ new("accDescrMultilineValue", Run(c => c != '}', 0), Token(ClassTokens.AccDescrMultilineValue)),
            /* 13 */ new("newline", MatchNewlines, Token(ClassTokens.Newline)),
            /* 14 */ new("whitespace", Run(ScanPatterns.IsJsWhitespace, 1), Skip),
            /* 15 */ new("classDiagramV2", Keyword("classDiagram-v2"), Token(ClassTokens.ClassDiagram)),
            /* 16 */ new("classDiagram", Keyword("classDiagram"), Token(ClassTokens.ClassDiagram)),
            /* 17 */ new("edgeState", Lit("[*]"), Token(ClassTokens.EdgeState)),
            /* 18 */ new("call", MatchCall, Push("callback_name", null)),
            /* 19 */ new("callbackEmptyArgs", MatchEmptyArguments, Pop(null)),
            /* 20 */ new("callbackArgsStart", Lit("("), s =>
            {
                s.PopState();
                s.Begin("callback_args");
                return null;
            }),
            /* 21 */ new("callbackName", Run(c => c != '(', 0), Token(ClassTokens.CallbackName)),
            /* 22 */ new("callbackArgsEnd", Lit(")"), Pop(null)),
            /* 23 */ new("callbackArgs", Run(c => c != ')', 0), Token(ClassTokens.CallbackArgs)),
            /* 24 */ new("stringEnd", Lit("\""), Pop(null)),
            /* 25 */ new("string", Run(c => c != '"', 0), Token(ClassTokens.Str)),
            /* 26 */ new("stringStart", Lit("\""), Push("string", null)),
            /* 27 */ new("style", Keyword("style"), Token(ClassTokens.Style)),
            /* 28 */ new("classDef", Keyword("classDef"), Token(ClassTokens.ClassDef)),
            /* 29 */ new("namespace", Keyword("namespace"), Push("namespace", ClassTokens.Namespace)),
            /* 30 */ new("namespaceNewline", MatchNewlines, Pop(ClassTokens.Newline)),
            /* 31 */ new("namespaceWhitespace", Run(ScanPatterns.IsJsWhitespace, 1), Skip),
            /* 32 */ new("namespaceBodyStart", Lit("{"), Push("namespace-body", ClassTokens.StructStart)),
            /* 33 */ new("namespaceEnd", Lit("}"), s =>
            {
                s.PopState();
                s.Less(0);
                return null;
            }),
            /* 34 */ new("namespaceBodyEnd", Lit("}"), Pop(ClassTokens.StructStop)),
            /* 35 */ new("namespaceBodyEof", ScanPatterns.EndOfInput, Token(ClassTokens.EofInStruct)),
            /* 36 */ new("namespaceBodyNewline", MatchNewlines, Token(ClassTokens.Newline)),
            /* 37 */ new("namespaceBodyWhitespace", Run(ScanPatterns.IsJsWhitespace, 1), Skip),
            /* 38 */ new("namespaceBodyEdgeState", Lit("[*]"), Token(ClassTokens.EdgeState)),
            /* 39 */ new("class", Keyword("class"), Push("class", ClassTokens.Class)),
            /* 40 */ new("classNewline", MatchNewlines, Pop(ClassTokens.Newline)),
            /* 41 */ new("classWhitespace", Run(ScanPatterns.IsJsWhitespace, 1), Skip),
            /* 42 */ new("classEnd", Lit("}"), s =>
            {
                s.PopState();
                s.PopState();
                return ClassTokens.StructStop;
            }),
            /* 43 */ new("classBodyStart", Lit("{"), Push("class-body", ClassTokens.StructStart)),
            /* 44 */ new("classBodyEnd", Lit("}"), Pop(ClassTokens.StructStop)),
            /* 45 */ new("classBodyEof", ScanPatterns.EndOfInput, Token(ClassTokens.EofInStruct)),
            /* 46 */ new("classBodyEdgeState", Lit("[*]"), Token(ClassTokens.EdgeState)),
            /* 47 */ new("classBodyOpen", Lit("{"), Token(ClassTokens.OpenInStruct)),
            /* 48 */ new("classBodyNewline", Lit("\n"), Skip),
            /* 49 */ new("member", Run(c => c is not ('{' or '}' or '\n'), 0), Token(ClassTokens.Member)),
            /* 50 */ new("cssClass", Keyword("cssClass"), Token(ClassTokens.CssClass)),
            /* 51 */ new("callback", Keyword("callback"), Token(ClassTokens.Callback)),
            /* 52 */ new("link", Keyword("link"), Token(ClassTokens.Link)),
            /* 53 */ new("click", Keyword("click"), Token(ClassTokens.Click)),
            /* 54 */ new("noteFor", Keyword("note for"), Token(ClassTokens.NoteFor)),
            /* 55 */ new("note", Keyword("note"), Token(ClassTokens.Note)),
            /* 56 */ new("annotationStart", Lit("<<"), Token(ClassTokens.AnnotationStart)),
            /* 57 */ new("annotationEnd", Lit(">>"), Token(ClassTokens.AnnotationEnd)),
            /* 58 */ new("href", Keyword("href"), Token(ClassTokens.Href)),
            /* 59 */ new("genericEnd", Lit("~"), Pop(null)),
            /* 60 */ new("genericType", Run(c => c != '~', 0), Token(ClassTokens.GenericType)),
            /* 61 */ new("genericStart", Lit("~"), Push("generic", null)),
            /* 62 */ new("backtickEnd", Lit("`"), Pop(null)),
            /* 63 */ new("backtickString", Run(c => c != '`', 1), Token(ClassTokens.BquoteStr)),
            /* 64 */ new("backtickStart", Lit("`"), Push("bqstring", null)),
            /* 65 */ new("targetSelf", Keyword("_self"), Token(ClassTokens.LinkTarget)),
            /* 66 */ new("targetBlank", Keyword("_blank"), Token(ClassTokens.LinkTarget)),
            /* 67 */ new("targetParent", Keyword("_parent"), Token(ClassTokens.LinkTarget)),
            /* 68 */ new("targetTop", Keyword("_top"), Token(ClassTokens.LinkTarget)),
            /* 69 */ new("extensionStart", SpacesThen("<|"), Token(ClassTokens.Extension)),
            /* 70 */ new("extensionEnd", SpacesThen("|>"), Token(ClassTokens.Extension)),
            /* 71 */ new("dependencyEnd", SpacesThen(">"), Token(ClassTokens.Dependency)),
            /* 72 */ new("dependencyStart", SpacesThen("<"), Token(ClassTokens.Dependency)),
            /* 73 */ new("composition", SpacesThen("*"), Token(ClassTokens.Composition)),
            /* 74 */ new("aggregation", MatchAggregation, Token(ClassTokens.Aggregation)),
            /* 75 */ new("lollipop", SpacesThen("()"), Token(ClassTokens.Lollipop)),
            /* 76 */ new("line", Lit("--"), Token(ClassTokens.Line)),
            /* 77 */ new("dottedLine", Lit(".."), Token(ClassTokens.DottedLine)),
            /* 78 */ new("label", MatchLabel, Token(ClassTokens.Label)),
            /* 79 */ new("styleSeparator", Lit(":::"), Token(ClassTokens.StyleSeparator)),
            /* 80 */ new("minus", Lit("-"), Token(ClassTokens.Minus)),
            /* 81 */ new("dot", Lit("."), Token(ClassTokens.Dot)),
            /* 82 */ new("plus", Lit("+"), Token(ClassTokens.Plus)),
            /* 83 */ new("colon", Lit(":"), Token(ClassTokens.Colon)),
            /* 84 */ new("comma", Lit(","), Token(ClassTokens.Comma)),
            /* 85 */ new("hash", Lit("#"), Token(ClassTokens.Brkt)),
            /* 86 */ new("hashAgain", Lit("#"), Token(ClassTokens.Brkt)),
            /* 87 */ new("percent", Lit("%"), Token(ClassTokens.Pct)),
            /* 88 */ new("equals", Lit("="), Token(ClassTokens.EqualsSign)),
            /* 89 */ new("equalsAgain", Lit("="), Token(ClassTokens.EqualsSign)),
            /* 90 */ new("alpha", Run(ScanPatterns.IsJsWordChar, 1), Token(ClassTokens.Alpha)),
            /* 91 */ new("squareStart", Lit("["), Token(ClassTokens.Sqs)),
            /* 92 */ new("squareEnd", Lit("]"), Token(ClassTokens.Sqe)),
            /* 93 */ new("punctuation", Char(c => "!\"#$%&'*+,-.`?\\/".Contains(c, StringComparison.Ordinal)), Token(ClassTokens.Punctuation)),
            /* 94 */ new("number", Run(char.IsAsciiDigit, 1), Token(ClassTokens.Num)),
            /* 95 */ new("unicodeText", Char(ScanPatterns.IsUnicodeLetter), Token(ClassTokens.UnicodeText)),
            /* 96 */ new("space", Char(ScanPatterns.IsJsWhitespace), Token(ClassTokens.Space)),
            /* 97 */ new("spaceAgain", Char(ScanPatterns.IsJsWhitespace), Token(ClassTokens.Space)),
            /* 98 */ new("eof", ScanPatterns.EndOfInput, Token(ClassTokens.Eof)),
        ];
    }

    /// <summary>
    ///     Creates a matcher of a fixed text (the grammar is case-sensitive).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The matcher.</returns>
    private static ScanMatcher Lit(string text) => (s, p) => Starts(s, p, text) ? text.Length : -1;

    /// <summary>
    ///     Creates a matcher of a keyword: the text followed by a word boundary (jison adds <c>\b</c> to a rule that ends
    ///     with a word character).
    /// </summary>
    /// <param name="text">The keyword.</param>
    /// <returns>The matcher.</returns>
    private static ScanMatcher Keyword(string text) => (s, p) =>
        Starts(s, p, text) && (p + text.Length >= s.Length || !ScanPatterns.IsJsWordChar(s[p + text.Length])) ? text.Length : -1;

    /// <summary>
    ///     Creates a matcher of a run of characters of a class.
    /// </summary>
    /// <param name="predicate">The class.</param>
    /// <param name="minimum">The fewest characters the run must have.</param>
    /// <returns>The matcher.</returns>
    private static ScanMatcher Run(Func<char, bool> predicate, int minimum) => ScanPatterns.Run(predicate, minimum);

    /// <summary>
    ///     Creates a matcher of one character of a class.
    /// </summary>
    /// <param name="predicate">The class.</param>
    /// <returns>The matcher.</returns>
    private static ScanMatcher Char(Func<char, bool> predicate) => (s, p) => p < s.Length && predicate(s[p]) ? 1 : -1;

    /// <summary>
    ///     Creates a matcher of optional white space and then a fixed text (the <c>\s*</c> before a relation end).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The matcher.</returns>
    private static ScanMatcher SpacesThen(string text) => (s, p) =>
    {
        var start = SkipSpaces(s, p);
        return Starts(s, start, text) ? start + text.Length - p : -1;
    };

    /// <summary>
    ///     Matches the <c>o</c> end of an aggregation (<c>\s*o\b</c>): an <c>o</c> that does not start a longer word, so
    ///     only a name that is exactly <c>o</c> is taken for one.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchAggregation(string s, int p)
    {
        var start = SkipSpaces(s, p);
        return start < s.Length && s[start] == 'o' && (start + 1 >= s.Length || !ScanPatterns.IsJsWordChar(s[start + 1])) ? start + 1 - p : -1;
    }

    /// <summary>
    ///     Matches a label (<c>:{1}[^:\n;]+</c>): a colon and at least one character that is not a colon, a line feed or a
    ///     semicolon.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchLabel(string s, int p)
    {
        if (p >= s.Length || s[p] != ':')
        {
            return -1;
        }

        var end = p + 1;
        while (end < s.Length && s[end] is not (':' or '\n' or ';'))
        {
            end++;
        }

        return end > p + 1 ? end - p : -1;
    }

    /// <summary>
    ///     Matches line breaks with the white space around them (<c>\s*(\r?\n)+</c>): the run of white space up to and
    ///     including its last line feed.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchNewlines(string s, int p)
    {
        var lastLineFeed = -1;
        for (var i = p; i < s.Length && ScanPatterns.IsJsWhitespace(s[i]); i++)
        {
            if (s[i] == '\n')
            {
                lastLineFeed = i;
            }
        }

        return lastLineFeed < 0 ? -1 : lastLineFeed + 1 - p;
    }

    /// <summary>
    ///     Matches a comment (<c>%%(?!\{)*[^\n]*(\r?\n?)+</c>, whose lookahead may repeat zero times): <c>%%</c>, the rest of
    ///     the line, and every line break after it.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchComment(string s, int p)
    {
        if (!Starts(s, p, "%%"))
        {
            return -1;
        }

        var end = RestOfLine(s, p + 2);
        while (end < s.Length && s[end] is '\r' or '\n')
        {
            end++;
        }

        return end - p;
    }

    /// <summary>
    ///     Matches a comment with the line breaks after it (<c>%%[^\n]*(\r?\n)*</c>); the rule before it always matches
    ///     first, so this one only keeps the rule numbers of the generated lexer.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchCommentLines(string s, int p)
    {
        if (!Starts(s, p, "%%"))
        {
            return -1;
        }

        var end = RestOfLine(s, p + 2);
        while (true)
        {
            var next = end < s.Length && s[end] == '\r' ? end + 1 : end;
            if (next >= s.Length || s[next] != '\n')
            {
                return end - p;
            }

            end = next + 1;
        }
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
    ///     Matches the <c>call</c> of a click statement with the white space after it (<c>call[\s]+</c>).
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchCall(string s, int p)
    {
        if (!Starts(s, p, "call"))
        {
            return -1;
        }

        var end = SkipSpaces(s, p + 4);
        return end > p + 4 ? end - p : -1;
    }

    /// <summary>
    ///     Matches empty function arguments (<c>\([\s]*\)</c>).
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchEmptyArguments(string s, int p)
    {
        if (p >= s.Length || s[p] != '(')
        {
            return -1;
        }

        var end = SkipSpaces(s, p + 1);
        return end < s.Length && s[end] == ')' ? end + 1 - p : -1;
    }

    /// <summary>
    ///     Gets a value indicating whether a text occurs at a position.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <param name="text">The text.</param>
    /// <returns>Whether it does.</returns>
    private static bool Starts(string s, int p, string text) =>
        p >= 0 && p + text.Length <= s.Length && string.CompareOrdinal(s, p, text, 0, text.Length) == 0;

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
    ///     Creates the matcher of a direction rule (<c>.*direction\s+XX[^\n]*</c>): a line that has the direction anywhere
    ///     in it from the position on is one token, up to the line feed after the direction.
    /// </summary>
    /// <param name="direction">The direction (<c>TB</c>, <c>BT</c>, <c>RL</c> or <c>LR</c>).</param>
    /// <returns>The matcher.</returns>
    private ScanMatcher DirectionLine(string direction) => (s, p) =>
    {
        var occurrence = LastDirectionOn(s, p, direction);
        if (occurrence < p)
        {
            return -1;
        }

        // The match runs to the line feed after the direction word (the white space before it may span lines)
        var after = SkipSpaces(s, occurrence + DirectionWord.Length) + direction.Length;
        return RestOfLine(s, after) - p;
    };

    /// <summary>
    ///     Finds the last <c>direction\s+XX</c> that starts on the line of a position, cached per line and direction so
    ///     that lexing a long line token by token stays linear.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <param name="direction">The direction.</param>
    /// <returns>The position of the <c>direction</c> word, or -1.</returns>
    private int LastDirectionOn(string s, int p, string direction)
    {
        // The line of the position ends at the first line terminator ('.' does not match one)
        if (p < _directionLineStart || p >= _directionLineEnd)
        {
            var lineEnd = p;
            while (lineEnd < s.Length && !ScanPatterns.IsJsLineTerminator(s[lineEnd]))
            {
                lineEnd++;
            }

            _directionLineStart = p;
            _directionLineEnd = lineEnd;
            _directionCache.Clear();
        }

        if (!_directionCache.TryGetValue(direction, out var found))
        {
            // The last occurrence wins, as the greedy '.*' takes it
            found = -1;
            for (var k = _directionLineEnd - DirectionWord.Length; k >= _directionLineStart; k--)
            {
                if (!Starts(s, k, DirectionWord))
                {
                    continue;
                }

                var spaces = SkipSpaces(s, k + DirectionWord.Length);
                if (spaces > k + DirectionWord.Length && Starts(s, spaces, direction))
                {
                    found = k;
                    break;
                }
            }

            _directionCache[direction] = found;
        }

        return found;
    }
}
