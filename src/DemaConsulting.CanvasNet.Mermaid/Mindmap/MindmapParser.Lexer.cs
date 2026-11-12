// Portions derived from mermaid (MIT); see third-party/README.md
using System.Collections.Frozen;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Mindmap;

/// <summary>
///     The token kinds of the mindmap grammar: the names of the terminals of Mermaid's <c>mindmap.jison</c>.
/// </summary>
/// <remarks>
///     The kinds are the jison terminal names, so messages and <see cref="MermaidSyntaxException.Expected"/> lists read
///     like Mermaid's parse errors. The class only holds constants and is safe for concurrent use.
/// </remarks>
internal static class MindmapTokens
{
    /// <summary>A comment, or a run of white space that ends with a line feed.</summary>
    public const string SpaceLine = "SPACELINE";

    /// <summary>One or more line feeds.</summary>
    public const string Newline = "NL";

    /// <summary>The <c>mindmap</c> keyword.</summary>
    public const string Mindmap = "MINDMAP";

    /// <summary>The end of the text in the initial condition (the grammar's <c>EOF</c>).</summary>
    public const string Eof = "<<EOF>>";

    /// <summary>The white space that indents a statement.</summary>
    public const string SpaceList = "SPACELIST";

    /// <summary>The text of an <c>::icon(...)</c> decoration.</summary>
    public const string Icon = "ICON";

    /// <summary>The text of a <c>:::</c> class decoration.</summary>
    public const string CssClass = "CLASS";

    /// <summary>The bracket that starts a node's description.</summary>
    public const string NodeStart = "NODE_DSTART";

    /// <summary>The text of a node's description.</summary>
    public const string NodeDescription = "NODE_DESCR";

    /// <summary>The bracket that ends a node's description.</summary>
    public const string NodeEnd = "NODE_DEND";

    /// <summary>The id of a node (everything up to a bracket, a brace or a line feed).</summary>
    public const string NodeId = "NODE_ID";

    /// <summary>The jison terminal numbers of the kinds, which order the expected lists as jison prints them.</summary>
    private static readonly FrozenDictionary<string, int> Numbers = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        [SpaceLine] = 6,
        [Newline] = 7,
        [Mindmap] = 8,
        [Eof] = 11,
        [SpaceList] = 13,
        [Icon] = 15,
        [CssClass] = 16,
        [NodeStart] = 19,
        [NodeDescription] = 20,
        [NodeEnd] = 21,
        [NodeId] = 22,
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
///     The lexer: a port of the 37 rules and the start conditions of the lexer that jison generates from Mermaid's
///     <c>mindmap.jison</c> (case-insensitive), with hand-written matchers in place of its regular expressions.
/// </content>
internal sealed partial class MindmapParser
{
    /// <summary>The start condition of a class decoration.</summary>
    private const string ClassState = "CLASS";

    /// <summary>The start condition of an icon decoration.</summary>
    private const string IconState = "ICON";

    /// <summary>The start condition of a node's description.</summary>
    private const string NodeState = "NODE";

    /// <summary>The start condition of a quoted description.</summary>
    private const string StringState = "NSTR";

    /// <summary>The start condition of a quoted markdown description (<c>"`...`"</c>).</summary>
    private const string MarkdownState = "NSTR2";

    /// <summary>
    ///     The start conditions of the lexer and the indexes of their rules, in the order jison tries them (the
    ///     <c>conditions</c> table of the generated lexer).
    /// </summary>
    private static readonly FrozenDictionary<string, IReadOnlyList<int>> Conditions = new Dictionary<string, IReadOnlyList<int>>
    {
        [ClassState] = [3, 4],
        [IconState] = [8, 9],
        [MarkdownState] = [22, 23],
        [StringState] = [25, 26],
        [NodeState] = [21, 24, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36],
        [SourceScanner.InitialState] = [0, 1, 2, 5, 6, 7, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20],
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The 37 rules of the lexer, in jison order.</summary>
    private static readonly ScanRule[] Rules = CreateRules();

    /// <summary>
    ///     Creates the 37 rules of the lexer, in jison order.
    /// </summary>
    /// <returns>The rules.</returns>
    private static ScanRule[] CreateRules()
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

        // The brackets that start a node, each entering its description, and those that end it, each leaving it
        static ScanRule Start(string name, string bracket) => new(name, Lit(bracket), Push(NodeState, MindmapTokens.NodeStart));
        static ScanRule End(string name, string bracket) => new(name, Lit(bracket), Pop(MindmapTokens.NodeEnd));

        return
        [
            /* 0 */ new("comment", MatchComment, Token(MindmapTokens.SpaceLine)),
            /* 1 */ new("mindmap", MatchKeyword, Token(MindmapTokens.Mindmap)),
            /* 2 */ new("classStart", Lit(":::"), Push(ClassState, null)),
            /* 3 */ new("class", Run(c => !ScanPatterns.IsJsLineTerminator(c)), Pop(MindmapTokens.CssClass)),
            /* 4 */ new("classEnd", Lit("\n"), Pop(null)),
            /* 5 */ new("iconStart", Lit("::icon("), Push(IconState, null)),
            /* 6 */ new("spaceLine", MatchSpaceLine, Token(MindmapTokens.SpaceLine)),
            /* 7 */ new("newlines", Run(c => c == '\n'), Token(MindmapTokens.Newline)),
            /* 8 */ new("icon", Run(c => c != ')'), Token(MindmapTokens.Icon)),
            /* 9 */ new("iconEnd", Lit(")"), Pop(null)),
            /* 10 */ Start("explodingStart", "-)"),
            /* 11 */ Start("cloudStart", "(-"),
            /* 12 */ Start("bangStart", "))"),
            /* 13 */ Start("cloudBangStart", ")"),
            /* 14 */ Start("circleStart", "(("),
            /* 15 */ Start("hexagonStart", "{{"),
            /* 16 */ Start("roundedStart", "("),
            /* 17 */ Start("squareStart", "["),
            /* 18 */ new("spaceList", Run(ScanPatterns.IsJsWhitespace), Token(MindmapTokens.SpaceList)),
            /* 19 */ new("nodeId", Run(c => c is not ('(' or '[' or '\n' or ')' or '{' or '}')), Token(MindmapTokens.NodeId)),
            /* 20 */ new("eof", ScanPatterns.EndOfInput, Token(MindmapTokens.Eof)),
            /* 21 */ new("markdownStart", Lit("\"`"), Push(MarkdownState, null)),
            /* 22 */ new("markdown", Run(c => c is not ('`' or '"')), Token(MindmapTokens.NodeDescription)),
            /* 23 */ new("markdownEnd", Lit("`\""), Pop(null)),
            /* 24 */ new("stringStart", Lit("\""), Push(StringState, null)),
            /* 25 */ new("string", Run(c => c != '"'), Token(MindmapTokens.NodeDescription)),
            /* 26 */ new("stringEnd", Lit("\""), Pop(null)),
            /* 27 */ End("bangEnd", "))"),
            /* 28 */ End("roundedEnd", ")"),
            /* 29 */ End("squareEnd", "]"),
            /* 30 */ End("hexagonEnd", "}}"),
            /* 31 */ End("cloudEnd", "(-"),
            /* 32 */ End("explodingEnd", "-)"),
            /* 33 */ End("circleEnd", "(("),
            /* 34 */ End("cloudBangEnd", "("),
            /* 35 */ new("description", Run(c => c is not (')' or ']' or '(' or '}')), Token(MindmapTokens.NodeDescription)),

            // .+(?!\(\()): the greedy run ends at a line end or at the end of the text, where the look-ahead always holds
            /* 36 */ new("descriptionRest", Run(c => !ScanPatterns.IsJsLineTerminator(c)), Token(MindmapTokens.NodeDescription)),
        ];
    }

    /// <summary>
    ///     Creates a matcher of a fixed text in any case.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The matcher.</returns>
    private static ScanMatcher Lit(string text) => (s, p) => Starts(s, p, text) ? text.Length : -1;

    /// <summary>
    ///     Creates a matcher of a run of one or more characters of a class.
    /// </summary>
    /// <param name="predicate">The class.</param>
    /// <returns>The matcher.</returns>
    private static ScanMatcher Run(Func<char, bool> predicate) => ScanPatterns.Run(predicate, 1);

    /// <summary>
    ///     Matches the keyword rule <c>mindmap\b</c> in any case.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchKeyword(string s, int p)
    {
        const string Keyword = "mindmap";
        var end = p + Keyword.Length;
        return Starts(s, p, Keyword) && !(end < s.Length && ScanPatterns.IsJsWordChar(s[end])) ? Keyword.Length : -1;
    }

    /// <summary>
    ///     Matches the comment rule <c>\s*%%.*</c>: white space (line feeds included), <c>%%</c>, and the rest of the line.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchComment(string s, int p)
    {
        var at = p;
        while (at < s.Length && ScanPatterns.IsJsWhitespace(s[at]))
        {
            at++;
        }

        if (!Starts(s, at, "%%"))
        {
            return -1;
        }

        var end = at + 2;
        while (end < s.Length && !ScanPatterns.IsJsLineTerminator(s[end]))
        {
            end++;
        }

        return end - p;
    }

    /// <summary>
    ///     Matches <c>[\s]+[\n]</c>: a run of white space up to its last line feed, with at least one white-space
    ///     character before that line feed (the greedy run gives back characters until a line feed follows).
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchSpaceLine(string s, int p)
    {
        var end = p;
        while (end < s.Length && ScanPatterns.IsJsWhitespace(s[end]))
        {
            end++;
        }

        for (var at = end - 1; at > p; at--)
        {
            if (s[at] == '\n')
            {
                return at + 1 - p;
            }
        }

        return -1;
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
}
