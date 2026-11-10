// Portions derived from mermaid (MIT); see third-party/README.md
using System.Collections.Frozen;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Er;

/// <summary>
///     The token kinds of the ER grammar: the names of the terminals of Mermaid's <c>erDiagram.jison</c>.
/// </summary>
/// <remarks>
///     The kinds are the jison terminal names, so messages and <see cref="MermaidSyntaxException.Expected"/> lists read
///     like Mermaid's parse errors. Two lexer rules return the character they match as the kind (jison's
///     <c>yytext[0]</c>), so a kind may also be a single character: <c>?</c> and <c>,</c> are terminals of the attribute
///     grammar, and any other character is a parse error, as in Mermaid. The grammar's <c>EOF</c> token (the lexer's
///     <c>&lt;&lt;EOF&gt;&gt;</c> rule) has the kind <see cref="Eof"/>, which is named <c>EOF</c> in messages, so that it
///     differs from the scanner's end of the tokens (jison's <c>$end</c>). The grammar knows two kinds no lexer rule
///     produces (<see cref="Title"/> and <see cref="TitleValue"/>), which only appear in the expected lists. The class only
///     holds constants and is safe for concurrent use.
/// </remarks>
internal static class ErTokens
{
    /// <summary>The <c>erDiagram</c> keyword.</summary>
    public const string ErDiagram = "ER_DIAGRAM";

    /// <summary>The end of the text in the initial condition (the grammar's <c>EOF</c>).</summary>
    public const string Eof = "<<EOF>>";

    /// <summary>One or more line feeds, or the line feed that ends a <c>style</c> or <c>classDef</c> statement.</summary>
    public const string Newline = "NEWLINE";

    /// <summary>A colon.</summary>
    public const string Colon = "COLON";

    /// <summary>The <c>:::</c> before class names.</summary>
    public const string StyleSeparator = "STYLE_SEPARATOR";

    /// <summary>The <c>{</c> of an attribute block.</summary>
    public const string BlockStart = "BLOCK_START";

    /// <summary>The <c>}</c> of an attribute block.</summary>
    public const string BlockStop = "BLOCK_STOP";

    /// <summary>The <c>[</c> of an alias or a subgraph title.</summary>
    public const string Sqs = "SQS";

    /// <summary>The <c>]</c> of an alias or a subgraph title.</summary>
    public const string Sqe = "SQE";

    /// <summary>A title keyword; the grammar has it, but no lexer rule produces it.</summary>
    public const string Title = "title";

    /// <summary>A title text; the grammar has it, but no lexer rule produces it.</summary>
    public const string TitleValue = "title_value";

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

    /// <summary>The <c>end</c> of a subgraph, with the white space after it.</summary>
    public const string End = "END";

    /// <summary>The <c>subgraph</c> keyword.</summary>
    public const string Subgraph = "SUBGRAPH";

    /// <summary>A line with <c>direction TB</c> in it.</summary>
    public const string DirectionTb = "direction_tb";

    /// <summary>A line with <c>direction BT</c> in it.</summary>
    public const string DirectionBt = "direction_bt";

    /// <summary>A line with <c>direction RL</c> in it.</summary>
    public const string DirectionRl = "direction_rl";

    /// <summary>A line with <c>direction LR</c> in it.</summary>
    public const string DirectionLr = "direction_lr";

    /// <summary>The <c>classDef</c> keyword.</summary>
    public const string ClassDef = "CLASSDEF";

    /// <summary>A word of the initial condition: an entity name, a class name or a role.</summary>
    public const string UnicodeText = "UNICODE_TEXT";

    /// <summary>A word of a <c>style</c> or <c>classDef</c> statement.</summary>
    public const string StyleText = "STYLE_TEXT";

    /// <summary>A comma outside an attribute block.</summary>
    public const string Comma = "COMMA";

    /// <summary>The <c>class</c> keyword.</summary>
    public const string ClassKeyword = "CLASS";

    /// <summary>The <c>style</c> keyword.</summary>
    public const string StyleKeyword = "STYLE";

    /// <summary>The semicolon that may end a <c>style</c> or <c>classDef</c> statement.</summary>
    public const string Semi = "SEMI";

    /// <summary>A number (an entity name).</summary>
    public const string Num = "NUM";

    /// <summary>A <c>#</c>.</summary>
    public const string Brkt = "BRKT";

    /// <summary>A double-quoted entity name (no <c>%</c>, backslash or line break in it), with its quotes.</summary>
    public const string EntityName = "ENTITY_NAME";

    /// <summary>A decimal number (an entity name).</summary>
    public const string DecimalNum = "DECIMAL_NUM";

    /// <summary>The number <c>1</c> as an entity name.</summary>
    public const string EntityOne = "ENTITY_ONE";

    /// <summary>A word of an attribute block: an attribute type or name.</summary>
    public const string AttributeWord = "ATTRIBUTE_WORD";

    /// <summary>The <c>?</c> after an attribute type.</summary>
    public const string Question = "?";

    /// <summary>The comma between the keys of an attribute.</summary>
    public const string KeyComma = ",";

    /// <summary>A key of an attribute: <c>PK</c>, <c>FK</c> or <c>UK</c>.</summary>
    public const string AttributeKey = "ATTRIBUTE_KEY";

    /// <summary>The double-quoted comment of an attribute, with its quotes.</summary>
    public const string Comment = "COMMENT";

    /// <summary>The cardinality zero or one (<c>|o</c>, <c>o|</c>, <c>one or zero</c>, <c>zero or one</c>).</summary>
    public const string ZeroOrOne = "ZERO_OR_ONE";

    /// <summary>The cardinality zero or more (<c>}o</c>, <c>o{</c>, <c>zero or more</c>, <c>0+</c>, <c>many</c>, ...).</summary>
    public const string ZeroOrMore = "ZERO_OR_MORE";

    /// <summary>The cardinality one or more (<c>}|</c>, <c>|{</c>, <c>one or more</c>, <c>1+</c>, <c>many(1)</c>, ...).</summary>
    public const string OneOrMore = "ONE_OR_MORE";

    /// <summary>The cardinality exactly one (<c>||</c>, <c>one</c>, <c>only one</c>, a <c>1</c> before a name or a line).</summary>
    public const string OnlyOne = "ONLY_ONE";

    /// <summary>The parent cardinality (<c>u</c> before a relationship line).</summary>
    public const string MdParent = "MD_PARENT";

    /// <summary>A non-identifying (dashed) relationship: <c>..</c>, <c>.-</c>, <c>-.</c> or <c>optionally to</c>.</summary>
    public const string NonIdentifying = "NON_IDENTIFYING";

    /// <summary>An identifying (solid) relationship: <c>--</c> or <c>to</c>.</summary>
    public const string Identifying = "IDENTIFYING";

    /// <summary>A double-quoted text (a role), with its quotes.</summary>
    public const string Word = "WORD";

    /// <summary>The jison terminal numbers of the kinds, which order the expected lists as jison prints them.</summary>
    private static readonly FrozenDictionary<string, int> Numbers = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        [ErDiagram] = 4,
        [Eof] = 6,
        [Newline] = 9,
        [Colon] = 12,
        [StyleSeparator] = 14,
        [BlockStart] = 16,
        [BlockStop] = 18,
        [Sqs] = 19,
        [Sqe] = 20,
        [Title] = 21,
        [TitleValue] = 22,
        [AccTitle] = 23,
        [AccTitleValue] = 24,
        [AccDescr] = 25,
        [AccDescrValue] = 26,
        [AccDescrMultilineValue] = 27,
        [End] = 33,
        [Subgraph] = 34,
        [DirectionTb] = 37,
        [DirectionBt] = 38,
        [DirectionRl] = 39,
        [DirectionLr] = 40,
        [ClassDef] = 41,
        [UnicodeText] = 43,
        [StyleText] = 44,
        [Comma] = 45,
        [ClassKeyword] = 46,
        [StyleKeyword] = 47,
        [Semi] = 50,
        [Num] = 51,
        [Brkt] = 52,
        [EntityName] = 53,
        [DecimalNum] = 54,
        [EntityOne] = 55,
        [AttributeWord] = 61,
        [Question] = 62,
        [KeyComma] = 64,
        [AttributeKey] = 65,
        [Comment] = 66,
        [ZeroOrOne] = 69,
        [ZeroOrMore] = 70,
        [OneOrMore] = 71,
        [OnlyOne] = 72,
        [MdParent] = 73,
        [NonIdentifying] = 74,
        [Identifying] = 75,
        [Word] = 76,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    ///     Gets the jison terminal number of a token kind, which orders expected-token lists as jison prints them.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The number; kinds the grammar does not know sort last.</returns>
    public static int Number(string kind) => Numbers.TryGetValue(kind, out var number) ? number : int.MaxValue;

    /// <summary>
    ///     Gets the jison name of a token kind, as messages and expected lists show it.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The name: <c>EOF</c> for <see cref="Eof"/>, else the kind.</returns>
    public static string JisonName(string kind) => kind == Eof ? "EOF" : kind;
}

/// <content>
///     The lexer: a port of the 83 rules and the start conditions of the lexer that jison generates from Mermaid's
///     <c>erDiagram.jison</c> (case-insensitive), with hand-written matchers in place of its regular expressions.
/// </content>
internal sealed partial class ErParser
{
    /// <summary>The word of a direction statement.</summary>
    private const string DirectionWord = "direction";

    /// <summary>The key of the cached search for the last tilde of a line.</summary>
    private const string TildeKey = "~";

    /// <summary>The keys of an attribute.</summary>
    private static readonly string[] Keys = ["PK", "FK", "UK"];

    /// <summary>
    ///     The start conditions of the lexer and the indexes of their rules, in the order jison tries them (the
    ///     <c>conditions</c> table of the generated lexer).
    /// </summary>
    private static readonly FrozenDictionary<string, IReadOnlyList<int>> Conditions = new Dictionary<string, IReadOnlyList<int>>
    {
        ["style"] = [36, 37, 38, 39, 40, 78, 79],
        ["acc_descr_multiline"] = [5, 6],
        ["acc_descr"] = [3],
        ["acc_title"] = [1],
        ["block_bq"] = [27, 28],
        ["block"] = [22, 23, 24, 25, 26, 29, 30, 31, 32],
        [SourceScanner.InitialState] =
        [
            0, 2, 4, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 33, 34, 35, 41, 42, 43, 44, 45, 46, 47, 48,
            49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75,
            76, 77, 80, 81, 82,
        ],
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The cached last match of each line search (a direction or a tilde), by search.</summary>
    private readonly Dictionary<string, int> _lineCache = new(StringComparer.Ordinal);

    /// <summary>The position the cached line of the line searches was computed from.</summary>
    private int _lineStart;

    /// <summary>The end of the cached line of the line searches (its first line terminator).</summary>
    private int _lineEnd;

    /// <summary>The start of the cached run of characters that are not white space, or -1 before the first.</summary>
    private int _runStart = -1;

    /// <summary>The end of the cached run (its first white-space character, or the end of the input).</summary>
    private int _runEnd;

    /// <summary>The last tilde of the cached run before the last tilde of its line, or -1 when there is none.</summary>
    private int _runTilde = -1;

    /// <summary>
    ///     Creates the 83 rules of the lexer, in jison order.
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
        static string? Character(SourceScanner s) => s.Text[..1];
        static string? Skip(SourceScanner s) => null;

        return
        [
            /* 0 */ new("accTitle", (s, p) => MatchAccessibilityKeyword(s, p, "accTitle", ':'), Push("acc_title", ErTokens.AccTitle)),
            /* 1 */ new("accTitleValue", Run(c => c != '\n', 0), Pop(ErTokens.AccTitleValue)),
            /* 2 */ new("accDescr", (s, p) => MatchAccessibilityKeyword(s, p, "accDescr", ':'), Push("acc_descr", ErTokens.AccDescr)),
            /* 3 */ new("accDescrValue", Run(c => c != '\n', 0), Pop(ErTokens.AccDescrValue)),
            /* 4 */ new("accDescrMultiline", (s, p) => MatchAccessibilityKeyword(s, p, "accDescr", '{'), Push("acc_descr_multiline", null)),
            /* 5 */ new("accDescrMultilineEnd", Lit("}"), Pop(null)),
            /* 6 */ new("accDescrMultilineValue", Run(c => c != '}', 0), Token(ErTokens.AccDescrMultilineValue)),
            /* 7 */ new("directionTb", DirectionLine("TB"), Token(ErTokens.DirectionTb)),
            /* 8 */ new("directionBt", DirectionLine("BT"), Token(ErTokens.DirectionBt)),
            /* 9 */ new("directionRl", DirectionLine("RL"), Token(ErTokens.DirectionRl)),
            /* 10 */ new("directionLr", DirectionLine("LR"), Token(ErTokens.DirectionLr)),
            /* 11 */ new("whitespace", Run(c => c is ' ' or '\t' or '\r', 1), Skip),
            /* 12 */ new("newlines", Run(c => c == '\n', 1), Token(ErTokens.Newline)),
            /* 13 */ new("entityName", MatchEntityName, Token(ErTokens.EntityName)),
            /* 14 */ new("word", MatchQuoted, Token(ErTokens.Word)),
            /* 15 */ new("erDiagram", Keyword("erDiagram"), Token(ErTokens.ErDiagram)),
            /* 16 */ new("blockStart", Lit("{"), Push("block", ErTokens.BlockStart)),
            /* 17 */ new("hash", Lit("#"), Token(ErTokens.Brkt)),
            /* 18 */ new("hashAgain", Lit("#"), Token(ErTokens.Brkt)),
            /* 19 */ new("comma", Lit(","), Token(ErTokens.Comma)),
            /* 20 */ new("styleSeparator", Lit(":::"), Token(ErTokens.StyleSeparator)),
            /* 21 */ new("colon", Lit(":"), Token(ErTokens.Colon)),
            /* 22 */ new("blockWhitespace", Run(ScanPatterns.IsJsWhitespace, 1), Skip),
            /* 23 */ new("attributeKey", MatchKey, Token(ErTokens.AttributeKey)),
            /* 24 */ new("genericWord", MatchGenericWord, Token(ErTokens.AttributeWord)),
            /* 25 */ new("attributeWord", MatchAttributeWord, Token(ErTokens.AttributeWord)),
            /* 26 */ new("backtickStart", Lit("`"), Push("block_bq", null)),
            /* 27 */ new("backtickWord", Run(c => c != '`', 1), Token(ErTokens.AttributeWord)),
            /* 28 */ new("backtickEnd", Lit("`"), Pop(null)),
            /* 29 */ new("comment", MatchQuoted, Token(ErTokens.Comment)),
            /* 30 */ new("blockNewlines", Run(c => c == '\n', 1), Skip),
            /* 31 */ new("blockStop", Lit("}"), Pop(ErTokens.BlockStop)),
            /* 32 */ new("blockCharacter", AnyCharacter, Character),
            /* 33 */ new("squareStart", Lit("["), Token(ErTokens.Sqs)),
            /* 34 */ new("squareEnd", Lit("]"), Token(ErTokens.Sqe)),
            /* 35 */ new("style", Keyword("style"), Push("style", ErTokens.StyleKeyword)),
            /* 36 */ new("styleNewlines", Run(c => c == '\n', 1), Pop(ErTokens.Newline)),
            /* 37 */ new("styleWhitespace", Run(ScanPatterns.IsJsWhitespace, 1), Skip),
            /* 38 */ new("styleColon", Lit(":"), Token(ErTokens.Colon)),
            /* 39 */ new("styleComma", Lit(","), Token(ErTokens.Comma)),
            /* 40 */ new("styleHash", Lit("#"), Token(ErTokens.Brkt)),
            /* 41 */ new("classDef", Keyword("classDef"), Push("style", ErTokens.ClassDef)),
            /* 42 */ new("class", Keyword("class"), Token(ErTokens.ClassKeyword)),
            /* 43 */ new("subgraph", Keyword("subgraph"), Token(ErTokens.Subgraph)),
            /* 44 */ new("end", MatchEnd, Token(ErTokens.End)),
            /* 45 */ new("oneOrZero", Keyword("one or zero"), Token(ErTokens.ZeroOrOne)),
            /* 46 */ new("oneOrMore", Keyword("one or more"), Token(ErTokens.OneOrMore)),
            /* 47 */ new("oneOrMany", Keyword("one or many"), Token(ErTokens.OneOrMore)),
            /* 48 */ new("onePlus", Lit("1+"), Token(ErTokens.OneOrMore)),
            /* 49 */ new("barO", Keyword("|o"), Token(ErTokens.ZeroOrOne)),
            /* 50 */ new("zeroOrOne", Keyword("zero or one"), Token(ErTokens.ZeroOrOne)),
            /* 51 */ new("zeroOrMore", Keyword("zero or more"), Token(ErTokens.ZeroOrMore)),
            /* 52 */ new("zeroOrMany", Keyword("zero or many"), Token(ErTokens.ZeroOrMore)),
            /* 53 */ new("zeroPlus", Lit("0+"), Token(ErTokens.ZeroOrMore)),
            /* 54 */ new("crowO", Keyword("}o"), Token(ErTokens.ZeroOrMore)),
            /* 55 */ new("many0", Lit("many(0)"), Token(ErTokens.ZeroOrMore)),
            /* 56 */ new("many1", Lit("many(1)"), Token(ErTokens.OneOrMore)),
            /* 57 */ new("many", Keyword("many"), Token(ErTokens.ZeroOrMore)),
            /* 58 */ new("crowBar", Lit("}|"), Token(ErTokens.OneOrMore)),
            /* 59 */ new("one", Keyword("one"), Token(ErTokens.OnlyOne)),
            /* 60 */ new("onlyOne", Keyword("only one"), Token(ErTokens.OnlyOne)),
            /* 61 */ new("decimal", MatchDecimal, Token(ErTokens.DecimalNum)),
            /* 62 */ new("oneBeforeName", (s, p) => MatchOneBefore(s, p, c => char.IsAsciiLetter(c) || c is '_' or '"' or '\''), Token(ErTokens.OnlyOne)),
            /* 63 */ new("oneBeforeNumber", (s, p) => MatchOneBefore(s, p, char.IsAsciiDigit), Token(ErTokens.OnlyOne)),
            /* 64 */ new("oneBeforeLine", MatchOneBeforeLine, Token(ErTokens.OnlyOne)),
            /* 65 */ new("entityOne", Keyword("1"), Token(ErTokens.EntityOne)),
            /* 66 */ new("number", Run(char.IsAsciiDigit, 1), Token(ErTokens.Num)),
            /* 67 */ new("bars", Lit("||"), Token(ErTokens.OnlyOne)),
            /* 68 */ new("oBar", Lit("o|"), Token(ErTokens.ZeroOrOne)),
            /* 69 */ new("oCrow", Lit("o{"), Token(ErTokens.ZeroOrMore)),
            /* 70 */ new("barCrow", Lit("|{"), Token(ErTokens.OneOrMore)),
            /* 71 */ new("parent", MatchParent, Token(ErTokens.MdParent)),
            /* 72 */ new("dots", Lit(".."), Token(ErTokens.NonIdentifying)),
            /* 73 */ new("dashes", Lit("--"), Token(ErTokens.Identifying)),
            /* 74 */ new("to", Keyword("to"), Token(ErTokens.Identifying)),
            /* 75 */ new("optionallyTo", Keyword("optionally to"), Token(ErTokens.NonIdentifying)),
            /* 76 */ new("dotDash", Lit(".-"), Token(ErTokens.NonIdentifying)),
            /* 77 */ new("dashDot", Lit("-."), Token(ErTokens.NonIdentifying)),
            /* 78 */ new("styleText", Run(c => c > '\x7F' || ScanPatterns.IsJsWordChar(c) || c is '-' or '*', 1), Token(ErTokens.StyleText)),
            /* 79 */ new("semicolon", Lit(";"), Token(ErTokens.Semi)),
            /* 80 */ new("unicodeText", Run(c => c > '\x7F' || ScanPatterns.IsJsWordChar(c) || c is '-' or '*' or '.', 1), Token(ErTokens.UnicodeText)),
            /* 81 */ new("character", AnyCharacter, Character),
            /* 82 */ new("eof", ScanPatterns.EndOfInput, Token(ErTokens.Eof)),
        ];
    }

    /// <summary>
    ///     Creates a matcher of a fixed text in any case.
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
    ///     Creates a matcher of a run of characters of a class.
    /// </summary>
    /// <param name="predicate">The class.</param>
    /// <param name="minimum">The fewest characters the run must have.</param>
    /// <returns>The matcher.</returns>
    private static ScanMatcher Run(Func<char, bool> predicate, int minimum) => ScanPatterns.Run(predicate, minimum);

    /// <summary>
    ///     Matches any one character but a line terminator (<c>.</c>).
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int AnyCharacter(string s, int p) => p < s.Length && !ScanPatterns.IsJsLineTerminator(s[p]) ? 1 : -1;

    /// <summary>
    ///     Matches a double-quoted text (<c>"[^"]*"</c>), which may span lines.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchQuoted(string s, int p)
    {
        if (p >= s.Length || s[p] != '"')
        {
            return -1;
        }

        var end = s.IndexOf('"', p + 1);
        return end < 0 ? -1 : end + 1 - p;
    }

    /// <summary>
    ///     Matches a quoted entity name (<c>"[^"%\r\n\v\b\\]+"</c>, where <c>\b</c> is the backspace character): at least
    ///     one character, with no percent sign, backslash or line break.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchEntityName(string s, int p)
    {
        if (p >= s.Length || s[p] != '"')
        {
            return -1;
        }

        var end = p + 1;
        while (end < s.Length && s[end] is not ('"' or '%' or '\r' or '\n' or '\v' or '\b' or '\\'))
        {
            end++;
        }

        return end > p + 1 && end < s.Length && s[end] == '"' ? end + 1 - p : -1;
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
    ///     Matches the <c>end</c> of a subgraph and the white space after it (<c>end\b\s*</c>), line breaks included.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchEnd(string s, int p) => Keyword("end")(s, p) < 0 ? -1 : SkipSpaces(s, p + 3) - p;

    /// <summary>
    ///     Matches a key of an attribute (<c>\b(PK|FK|UK)\b</c>, in any case): the key between word boundaries.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchKey(string s, int p)
    {
        if (p > 0 && ScanPatterns.IsJsWordChar(s[p - 1]))
        {
            return -1;
        }

        return Keys.Any(key => Keyword(key)(s, p) >= 0) ? 2 : -1;
    }

    /// <summary>
    ///     Matches an attribute word (<c>[\*A-Za-z_\u00C0-\uFFFF][A-Za-z0-9\-\_\[\]\(\)\.,\u00C0-\uFFFF\*]*</c>): a type or a
    ///     name, which may hold brackets, parentheses, dots and commas after its first character.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchAttributeWord(string s, int p)
    {
        if (p >= s.Length || !(s[p] is '*' or '_' || char.IsAsciiLetter(s[p]) || s[p] >= '\u00C0'))
        {
            return -1;
        }

        var end = p + 1;
        while (end < s.Length && (char.IsAsciiLetterOrDigit(s[end]) || s[end] is '-' or '_' or '[' or ']' or '(' or ')' or '.' or ',' or '*' || s[end] >= '\u00C0'))
        {
            end++;
        }

        return end - p;
    }

    /// <summary>
    ///     Matches a decimal number (<c>[0-9]+\.[0-9]+</c>).
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchDecimal(string s, int p)
    {
        var dot = SkipDigits(s, p);
        if (dot == p || dot >= s.Length || s[dot] != '.')
        {
            return -1;
        }

        var end = SkipDigits(s, dot + 1);
        return end > dot + 1 ? end - p : -1;
    }

    /// <summary>
    ///     Matches a <c>1</c> followed by white space and a character of a class (<c>1(?=\s+[A-Za-z_"'])</c> and
    ///     <c>1(?=\s+[0-9])</c>); only the <c>1</c> is consumed.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <param name="next">The class of the character after the white space.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchOneBefore(string s, int p, Func<char, bool> next)
    {
        if (p >= s.Length || s[p] != '1')
        {
            return -1;
        }

        var after = SkipSpaces(s, p + 1);
        return after > p + 1 && after < s.Length && next(s[after]) ? 1 : -1;
    }

    /// <summary>
    ///     Matches a <c>1</c> directly before a relationship line (<c>1(?=(--|\.\.|\.-|-\.))</c>); only the <c>1</c> is
    ///     consumed.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchOneBeforeLine(string s, int p) =>
        p < s.Length && s[p] == '1' && (Starts(s, p + 1, "--") || Starts(s, p + 1, "..") || Starts(s, p + 1, ".-") || Starts(s, p + 1, "-."))
            ? 1
            : -1;

    /// <summary>
    ///     Matches the parent cardinality (<c>u(?=[\.\-\|])</c>, in any case); only the <c>u</c> is consumed.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchParent(string s, int p) =>
        Starts(s, p, "u") && p + 1 < s.Length && s[p + 1] is '.' or '-' or '|' ? 1 : -1;

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
    ///     Skips ASCII digits (<c>[0-9]*</c>).
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The position after the digits.</returns>
    private static int SkipDigits(string s, int p)
    {
        while (p < s.Length && char.IsAsciiDigit(s[p]))
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
    ///     Matches a generic attribute word (<c>([^\s]*)[~].*[~]([^\s]*)</c>): the run of non-white-space characters up to
    ///     its last tilde that has another tilde after it on the line, then everything up to the last tilde of the line and
    ///     the non-white-space characters after it.
    /// </summary>
    /// <remarks>
    ///     The run and the last tilde of the line are cached, so that lexing a long line token by token stays linear.
    /// </remarks>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private int MatchGenericWord(string s, int p)
    {
        var last = LastOnLine(s, p, TildeKey, k => s[k] == '~');
        if (last < p)
        {
            return -1;
        }

        // The run of non-white-space characters the position is in (its end is the same for every position in it)
        if (_runStart < 0 || p < _runStart || p >= _runEnd)
        {
            _runStart = p;
            _runEnd = p;
            while (_runEnd < s.Length && !ScanPatterns.IsJsWhitespace(s[_runEnd]))
            {
                _runEnd++;
            }

            var limit = Math.Min(_runEnd, last);
            _runTilde = limit > p ? s.LastIndexOf('~', limit - 1, limit - p) : -1;
        }

        // The first tilde: the last one of the run before the last tilde of the line
        if (_runTilde < p)
        {
            return -1;
        }

        var end = last + 1;
        while (end < s.Length && !ScanPatterns.IsJsWhitespace(s[end]))
        {
            end++;
        }

        return end - p;
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
