// Portions derived from mermaid (MIT); see third-party/README.md
using System.Collections.Frozen;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Gantt;

/// <summary>
///     The token kinds of the gantt grammar: the names of the terminals of Mermaid's <c>gantt.jison</c>.
/// </summary>
/// <remarks>
///     The kinds are the jison terminal names, so messages and <see cref="MermaidSyntaxException.Expected"/> lists read
///     like Mermaid's parse errors. Five lexer rules return names the grammar does not know (<c>open_directive</c>,
///     <c>date</c>, <c>accDescription</c>, <c>:</c> and <c>INVALID</c>), which are always parse errors, as in Mermaid. The
///     grammar knows one kind no lexer rule produces (<see cref="Space"/>), which only appears in the expected lists. The
///     class only holds constants and is safe for concurrent use.
/// </remarks>
internal static class GanttTokens
{
    /// <summary>The <c>gantt</c> keyword.</summary>
    public const string Gantt = "gantt";

    /// <summary>The end of the text in the initial condition (the grammar's <c>EOF</c>).</summary>
    public const string Eof = "<<EOF>>";

    /// <summary>A space before a statement; the grammar has it, but no lexer rule produces it.</summary>
    public const string Space = "SPACE";

    /// <summary>One or more line feeds.</summary>
    public const string Newline = "NL";

    /// <summary>The <c>weekday monday</c> statement.</summary>
    public const string WeekdayMonday = "weekday_monday";

    /// <summary>The <c>weekday tuesday</c> statement.</summary>
    public const string WeekdayTuesday = "weekday_tuesday";

    /// <summary>The <c>weekday wednesday</c> statement.</summary>
    public const string WeekdayWednesday = "weekday_wednesday";

    /// <summary>The <c>weekday thursday</c> statement.</summary>
    public const string WeekdayThursday = "weekday_thursday";

    /// <summary>The <c>weekday friday</c> statement.</summary>
    public const string WeekdayFriday = "weekday_friday";

    /// <summary>The <c>weekday saturday</c> statement.</summary>
    public const string WeekdaySaturday = "weekday_saturday";

    /// <summary>The <c>weekday sunday</c> statement.</summary>
    public const string WeekdaySunday = "weekday_sunday";

    /// <summary>The <c>weekend friday</c> statement.</summary>
    public const string WeekendFriday = "weekend_friday";

    /// <summary>The <c>weekend saturday</c> statement.</summary>
    public const string WeekendSaturday = "weekend_saturday";

    /// <summary>The <c>dateFormat</c> statement with its value.</summary>
    public const string DateFormat = "dateFormat";

    /// <summary>The <c>inclusiveEndDates</c> statement.</summary>
    public const string InclusiveEndDates = "inclusiveEndDates";

    /// <summary>The <c>topAxis</c> statement.</summary>
    public const string TopAxis = "topAxis";

    /// <summary>The <c>axisFormat</c> statement with its value.</summary>
    public const string AxisFormat = "axisFormat";

    /// <summary>The <c>tickInterval</c> statement with its value.</summary>
    public const string TickInterval = "tickInterval";

    /// <summary>The <c>excludes</c> statement with its value.</summary>
    public const string Excludes = "excludes";

    /// <summary>The <c>includes</c> statement with its value.</summary>
    public const string Includes = "includes";

    /// <summary>The <c>todayMarker</c> statement with its value.</summary>
    public const string TodayMarker = "todayMarker";

    /// <summary>The <c>title</c> statement with its value.</summary>
    public const string Title = "title";

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

    /// <summary>The <c>section</c> statement with its value.</summary>
    public const string Section = "section";

    /// <summary>The name of a task (everything up to a colon or a line feed).</summary>
    public const string TaskTxt = "taskTxt";

    /// <summary>The data of a task (a colon, then everything up to <c>#</c>, <c>;</c> or a line feed).</summary>
    public const string TaskData = "taskData";

    /// <summary>The ids of a <c>click</c> statement.</summary>
    public const string Click = "click";

    /// <summary>The function name of a <c>call</c>.</summary>
    public const string CallbackName = "callbackname";

    /// <summary>The arguments of a <c>call</c>, in its parentheses.</summary>
    public const string CallbackArgs = "callbackargs";

    /// <summary>The URL of an <c>href</c>, in its quotes.</summary>
    public const string Href = "href";

    /// <summary>A <c>%%{</c> that the preprocessing left; no state of the lexer follows it.</summary>
    public const string OpenDirective = "open_directive";

    /// <summary>A date at the start of a statement, which the grammar does not use.</summary>
    public const string Date = "date";

    /// <summary>The <c>accDescription</c> statement, which the grammar does not use.</summary>
    public const string AccDescription = "accDescription";

    /// <summary>A colon alone.</summary>
    public const string Colon = ":";

    /// <summary>Any other character.</summary>
    public const string Invalid = "INVALID";

    /// <summary>The jison terminal numbers of the kinds, which order the expected lists as jison prints them.</summary>
    private static readonly FrozenDictionary<string, int> Numbers = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        [Gantt] = 4,
        [Eof] = 6,
        [Space] = 8,
        [Newline] = 10,
        [WeekdayMonday] = 12,
        [WeekdayTuesday] = 13,
        [WeekdayWednesday] = 14,
        [WeekdayThursday] = 15,
        [WeekdayFriday] = 16,
        [WeekdaySaturday] = 17,
        [WeekdaySunday] = 18,
        [WeekendFriday] = 20,
        [WeekendSaturday] = 21,
        [DateFormat] = 22,
        [InclusiveEndDates] = 23,
        [TopAxis] = 24,
        [AxisFormat] = 25,
        [TickInterval] = 26,
        [Excludes] = 27,
        [Includes] = 28,
        [TodayMarker] = 29,
        [Title] = 30,
        [AccTitle] = 31,
        [AccTitleValue] = 32,
        [AccDescr] = 33,
        [AccDescrValue] = 34,
        [AccDescrMultilineValue] = 35,
        [Section] = 36,
        [TaskTxt] = 38,
        [TaskData] = 39,
        [Click] = 40,
        [CallbackName] = 41,
        [CallbackArgs] = 42,
        [Href] = 43,
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
///     The lexer: a port of the 53 rules and the start conditions of the lexer that jison generates from Mermaid's
///     <c>gantt.jison</c> (case-insensitive), with hand-written matchers in place of its regular expressions.
/// </content>
internal sealed partial class GanttParser
{
    /// <summary>The start condition of an <c>href</c> URL.</summary>
    private const string HrefState = "href";

    /// <summary>The start condition of a <c>call</c> function name.</summary>
    private const string CallbackNameState = "callbackname";

    /// <summary>The start condition of the arguments of a <c>call</c>.</summary>
    private const string CallbackArgsState = "callbackargs";

    /// <summary>The start condition of the ids of a <c>click</c>.</summary>
    private const string ClickState = "click";

    /// <summary>The start condition that the lexer enters after <c>%%{</c>; it has no rules.</summary>
    private const string OpenDirectiveState = "open_directive";

    /// <summary>
    ///     The start conditions of the lexer and the indexes of their rules, in the order jison tries them (the
    ///     <c>conditions</c> table of the generated lexer).
    /// </summary>
    private static readonly FrozenDictionary<string, IReadOnlyList<int>> Conditions = new Dictionary<string, IReadOnlyList<int>>
    {
        ["acc_descr_multiline"] = [6, 7],
        ["acc_descr"] = [4],
        ["acc_title"] = [2],
        [CallbackArgsState] = [21, 22],
        [CallbackNameState] = [18, 19, 20],
        [HrefState] = [15, 16],
        [ClickState] = [24, 25],
        [OpenDirectiveState] = [],
        [SourceScanner.InitialState] =
        [
            0, 1, 3, 5, 8, 9, 10, 11, 12, 13, 14, 17, 23, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41,
            42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52,
        ],
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The 53 rules of the lexer, in jison order.</summary>
    private static readonly ScanRule[] Rules = CreateRules();

    /// <summary>
    ///     Creates the 53 rules of the lexer, in jison order.
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
        static string? Skip(SourceScanner s) => null;

        return
        [
            /* 0 */ new("openDirective", Lit("%%{"), Push(OpenDirectiveState, GanttTokens.OpenDirective)),
            /* 1 */ new("accTitle", (s, p) => MatchAccessibilityKeyword(s, p, "accTitle", ':'), Push("acc_title", GanttTokens.AccTitle)),
            /* 2 */ new("accTitleValue", Run(c => c != '\n', 0), Pop(GanttTokens.AccTitleValue)),
            /* 3 */ new("accDescr", (s, p) => MatchAccessibilityKeyword(s, p, "accDescr", ':'), Push("acc_descr", GanttTokens.AccDescr)),
            /* 4 */ new("accDescrValue", Run(c => c != '\n', 0), Pop(GanttTokens.AccDescrValue)),
            /* 5 */ new("accDescrMultiline", (s, p) => MatchAccessibilityKeyword(s, p, "accDescr", '{'), Push("acc_descr_multiline", null)),
            /* 6 */ new("accDescrMultilineEnd", Lit("}"), Pop(null)),
            /* 7 */ new("accDescrMultilineValue", Run(c => c != '}', 0), Token(GanttTokens.AccDescrMultilineValue)),
            /* 8 */ new("comment", (s, p) => Starts(s, p, "%%") ? RestOfLine(s, p) - p : -1, Skip),
            /* 9 */ new("commentAfterCharacter", MatchCommentAfterCharacter, Skip),
            /* 10 */ new("percentComment", MatchPercentComment, Skip),
            /* 11 */ new("newlines", Run(c => c == '\n', 1), Token(GanttTokens.Newline)),
            /* 12 */ new("whitespace", Run(ScanPatterns.IsJsWhitespace, 1), Skip),
            /* 13 */ new("percentLine", (s, p) => Starts(s, p, "%") ? RestOfLine(s, p) - p : -1, Skip),
            /* 14 */ new("hrefStart", (s, p) => MatchWordSpacesQuote(s, p, "href"), Push(HrefState, null)),
            /* 15 */ new("hrefEnd", Lit("\""), Pop(null)),
            /* 16 */ new("hrefValue", Run(c => c != '"', 0), Token(GanttTokens.Href)),
            /* 17 */ new("callStart", (s, p) => MatchWordSpaces(s, p, "call"), Push(CallbackNameState, null)),
            /* 18 */ new("callbackEmptyArgs", MatchEmptyParentheses, Pop(null)),
            /* 19 */ new("callbackArgsStart", Lit("("), s =>
            {
                s.PopState();
                s.Begin(CallbackArgsState);
                return null;
            }),
            /* 20 */ new("callbackName", Run(c => c != '(', 0), Token(GanttTokens.CallbackName)),
            /* 21 */ new("callbackArgsEnd", Lit(")"), Pop(null)),
            /* 22 */ new("callbackArgs", Run(c => c != ')', 0), Token(GanttTokens.CallbackArgs)),
            /* 23 */ new("clickStart", (s, p) => MatchWordSpaces(s, p, "click"), Push(ClickState, null)),
            /* 24 */ new("clickEnd", (s, p) => p < s.Length && ScanPatterns.IsJsWhitespace(s[p]) ? 1 : -1, Pop(null)),
            /* 25 */ new("clickIds", Run(c => !ScanPatterns.IsJsWhitespace(c), 0), Token(GanttTokens.Click)),
            /* 26 */ new("gantt", Keyword("gantt"), Token(GanttTokens.Gantt)),
            /* 27 */ new("dateFormat", (s, p) => MatchStatement(s, p, "dateFormat", hash: true), Token(GanttTokens.DateFormat)),
            /* 28 */ new("inclusiveEndDates", Keyword("inclusiveEndDates"), Token(GanttTokens.InclusiveEndDates)),
            /* 29 */ new("topAxis", Keyword("topAxis"), Token(GanttTokens.TopAxis)),
            /* 30 */ new("axisFormat", (s, p) => MatchStatement(s, p, "axisFormat", hash: true), Token(GanttTokens.AxisFormat)),
            /* 31 */ new("tickInterval", (s, p) => MatchStatement(s, p, "tickInterval", hash: true), Token(GanttTokens.TickInterval)),
            /* 32 */ new("includes", (s, p) => MatchStatement(s, p, "includes", hash: true), Token(GanttTokens.Includes)),
            /* 33 */ new("excludes", (s, p) => MatchStatement(s, p, "excludes", hash: true), Token(GanttTokens.Excludes)),
            /* 34 */ new("todayMarker", (s, p) => MatchStatement(s, p, "todayMarker", hash: false), Token(GanttTokens.TodayMarker)),
            /* 35 */ new("weekdayMonday", Day("weekday", "monday"), Token(GanttTokens.WeekdayMonday)),
            /* 36 */ new("weekdayTuesday", Day("weekday", "tuesday"), Token(GanttTokens.WeekdayTuesday)),
            /* 37 */ new("weekdayWednesday", Day("weekday", "wednesday"), Token(GanttTokens.WeekdayWednesday)),
            /* 38 */ new("weekdayThursday", Day("weekday", "thursday"), Token(GanttTokens.WeekdayThursday)),
            /* 39 */ new("weekdayFriday", Day("weekday", "friday"), Token(GanttTokens.WeekdayFriday)),
            /* 40 */ new("weekdaySaturday", Day("weekday", "saturday"), Token(GanttTokens.WeekdaySaturday)),
            /* 41 */ new("weekdaySunday", Day("weekday", "sunday"), Token(GanttTokens.WeekdaySunday)),
            /* 42 */ new("weekendFriday", Day("weekend", "friday"), Token(GanttTokens.WeekendFriday)),
            /* 43 */ new("weekendSaturday", Day("weekend", "saturday"), Token(GanttTokens.WeekendSaturday)),
            /* 44 */ new("date", MatchDate, Token(GanttTokens.Date)),
            /* 45 */ new("title", (s, p) => MatchLine(s, p, "title"), Token(GanttTokens.Title)),
            /* 46 */ new("accDescription", (s, p) => MatchStatement(s, p, "accDescription", hash: true), Token(GanttTokens.AccDescription)),
            /* 47 */ new("section", (s, p) => MatchLine(s, p, "section"), Token(GanttTokens.Section)),
            /* 48 */ new("taskTxt", Run(c => c is not (':' or '\n'), 1), Token(GanttTokens.TaskTxt)),
            /* 49 */ new("taskData", MatchTaskData, Token(GanttTokens.TaskData)),
            /* 50 */ new("colon", Lit(":"), Token(GanttTokens.Colon)),
            /* 51 */ new("eof", ScanPatterns.EndOfInput, Token(GanttTokens.Eof)),
            /* 52 */ new("invalid", (s, p) => p < s.Length && !ScanPatterns.IsJsLineTerminator(s[p]) ? 1 : -1, Token(GanttTokens.Invalid)),
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
        Starts(s, p, text) && !IsWordAt(s, p + text.Length) ? text.Length : -1;

    /// <summary>
    ///     Creates a matcher of a run of characters of a class.
    /// </summary>
    /// <param name="predicate">The class.</param>
    /// <param name="minimum">The fewest characters the run must have.</param>
    /// <returns>The matcher.</returns>
    private static ScanMatcher Run(Func<char, bool> predicate, int minimum) => ScanPatterns.Run(predicate, minimum);

    /// <summary>
    ///     Creates a matcher of a weekday or weekend statement (<c>weekday\s+monday\b</c>).
    /// </summary>
    /// <param name="keyword">The keyword, <c>weekday</c> or <c>weekend</c>.</param>
    /// <param name="day">The day.</param>
    /// <returns>The matcher.</returns>
    private static ScanMatcher Day(string keyword, string day) => (s, p) =>
    {
        if (!Starts(s, p, keyword))
        {
            return -1;
        }

        var at = SkipSpaces(s, p + keyword.Length);
        return at > p + keyword.Length && Starts(s, at, day) && !IsWordAt(s, at + day.Length) ? at + day.Length - p : -1;
    };

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
    ///     Matches the quirky comment rule <c>[^\}]%%*[^\n]*</c>: any character but <c>}</c> (a line feed included)
    ///     followed by one or more <c>%</c>, and the rest of the line.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchCommentAfterCharacter(string s, int p) =>
        p + 1 < s.Length && s[p] != '}' && s[p + 1] == '%' ? RestOfLine(s, p + 1) - p : -1;

    /// <summary>
    ///     Matches <c>%%*[^\n]*[\n]*</c>: one or more <c>%</c>, the rest of the line and the line feeds after it.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchPercentComment(string s, int p)
    {
        if (p >= s.Length || s[p] != '%')
        {
            return -1;
        }

        var end = RestOfLine(s, p);
        while (end < s.Length && s[end] == '\n')
        {
            end++;
        }

        return end - p;
    }

    /// <summary>
    ///     Matches a word followed by white space (<c>call[\s]+</c>, <c>click[\s]+</c>).
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <param name="word">The word.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchWordSpaces(string s, int p, string word)
    {
        if (!Starts(s, p, word))
        {
            return -1;
        }

        var end = SkipSpaces(s, p + word.Length);
        return end > p + word.Length ? end - p : -1;
    }

    /// <summary>
    ///     Matches <c>href[\s]+["]</c>.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <param name="word">The word.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchWordSpacesQuote(string s, int p, string word)
    {
        var length = MatchWordSpaces(s, p, word);
        return length > 0 && p + length < s.Length && s[p + length] == '"' ? length + 1 : -1;
    }

    /// <summary>
    ///     Matches <c>\([\s]*\)</c>.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchEmptyParentheses(string s, int p)
    {
        if (p >= s.Length || s[p] != '(')
        {
            return -1;
        }

        var end = SkipSpaces(s, p + 1);
        return end < s.Length && s[end] == ')' ? end + 1 - p : -1;
    }

    /// <summary>
    ///     Matches a statement with a value up to the end of the line: the keyword in any case, one white-space character
    ///     (a line feed included), then one or more characters that are not a line feed or <c>;</c> (nor <c>#</c> when
    ///     asked): <c>dateFormat\s[^#\n;]+</c>.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <param name="keyword">The keyword.</param>
    /// <param name="hash">Whether <c>#</c> ends the value.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchStatement(string s, int p, string keyword, bool hash)
    {
        var at = p + keyword.Length;
        if (!Starts(s, p, keyword) || at >= s.Length || !ScanPatterns.IsJsWhitespace(s[at]))
        {
            return -1;
        }

        var end = at + 1;
        while (end < s.Length && s[end] is not ('\n' or ';') && !(hash && s[end] == '#'))
        {
            end++;
        }

        return end > at + 1 ? end - p : -1;
    }

    /// <summary>
    ///     Matches a statement with the rest of the line as its value (<c>title\s[^\n]+</c>, <c>section\s[^\n]+</c>).
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <param name="keyword">The keyword.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchLine(string s, int p, string keyword)
    {
        var at = p + keyword.Length;
        if (!Starts(s, p, keyword) || at >= s.Length || !ScanPatterns.IsJsWhitespace(s[at]))
        {
            return -1;
        }

        var end = RestOfLine(s, at + 1);
        return end > at + 1 ? end - p : -1;
    }

    /// <summary>
    ///     Matches a date (<c>\d\d\d\d-\d\d-\d\d\b</c>).
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchDate(string s, int p)
    {
        if (p + 10 > s.Length || s[p + 4] != '-' || s[p + 7] != '-' || IsWordAt(s, p + 10))
        {
            return -1;
        }

        for (var i = 0; i < 10; i++)
        {
            if (i is not (4 or 7) && !char.IsAsciiDigit(s[p + i]))
            {
                return -1;
            }
        }

        return 10;
    }

    /// <summary>
    ///     Matches the data of a task (<c>:[^#\n;]+</c>).
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchTaskData(string s, int p)
    {
        if (p >= s.Length || s[p] != ':')
        {
            return -1;
        }

        var end = p + 1;
        while (end < s.Length && s[end] is not ('#' or '\n' or ';'))
        {
            end++;
        }

        return end > p + 1 ? end - p : -1;
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
    ///     Gets whether a word character is at a position (no word boundary before it).
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>Whether it is.</returns>
    private static bool IsWordAt(string s, int p) => p < s.Length && ScanPatterns.IsJsWordChar(s[p]);

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
}
