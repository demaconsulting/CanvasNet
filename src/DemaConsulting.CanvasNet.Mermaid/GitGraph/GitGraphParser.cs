// Portions derived from mermaid (MIT), @mermaid-js/parser (MIT) and Langium (MIT); see third-party/README.md
using System.Globalization;
using System.Text;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.GitGraph;

/// <summary>
///     The git graph parser: a hand-written port of the Langium grammar of <c>@mermaid-js/parser</c>
///     (<c>gitGraph.langium</c> with <c>common.langium</c>, its token builder and value converters) and of the
///     <c>populate</c> of Mermaid's <c>gitGraphParser.ts</c>, producing a <see cref="GitGraphDiagram"/> whose statements
///     <see cref="GitGraphDatabase"/> runs as Mermaid's git graph database runs them.
/// </summary>
/// <remarks>
///     <para>
///         <b>Lexer.</b> The keywords (<c>cherry-pick</c>, <c>gitGraph:</c>, <c>HIGHLIGHT</c>, <c>gitGraph</c>, ... longest
///         first) are tried at each position before the terminals <c>ACC_DESCR</c>, <c>ACC_TITLE</c>, <c>TITLE</c>,
///         <c>INT</c>, <c>STRING</c> and <c>NEWLINE</c>, the hidden white space, YAML, directives and comments, and last
///         <c>REFERENCE</c> (<c>\w([-\./\w]*[-\w])?</c>), the first that matches winning, as in chevrotain; a keyword gives
///         way to a <c>REFERENCE</c> that matches more characters (<c>commits</c>, <c>LRx</c> and <c>commit2</c> are
///         names; <c>commit</c> and <c>LR</c> are keywords), and only <c>gitGraph</c> must be followed by <c>%%</c> or by
///         white space.
///     </para>
///     <para>
///         <b>Grammar.</b> <c>NEWLINE* header (NEWLINE | TitleAndAccessibilities | Statement)* EOF</c>, where the header is
///         <c>gitGraph</c>, <c>gitGraph :</c>, <c>gitGraph:</c> or <c>gitGraph</c> with a direction (<c>LR</c>, <c>TB</c>
///         or <c>BT</c>) and a colon (a direction without its colon ends the diagram, as Mermaid's prediction takes the
///         header alone), and a statement is a <c>commit</c> (<c>id:</c>, <c>msg:</c> or a bare message, <c>tag:</c> and
///         <c>type:</c> options in any order), a <c>branch</c> (a name and an <c>order:</c>), a <c>merge</c> (a branch,
///         <c>id:</c>, <c>tag:</c> and <c>type:</c>), a <c>checkout</c> or <c>switch</c>, or a <c>cherry-pick</c>
///         (<c>id:</c>, <c>tag:</c> and <c>parent:</c>), each ended by a line end. A repeated option keeps its last value,
///         and tags add up. Errors come where Mermaid reports its first one: a lexical error at the first character no
///         token matches, before any grammar error, else the first token the grammar does not accept, with the token
///         names chevrotain lists as <see cref="MermaidSyntaxException.Expected"/>.
///     </para>
///     <para>
///         <b>Values.</b> A title, <c>accTitle</c> or <c>accDescr</c> is converted with the mermaid value converter, a
///         string with Langium's <c>convertString</c>, an order with JavaScript's <c>parseInt</c> and a name written
///         without quotes is kept as written.
///     </para>
///     <para>
///         <b>Database.</b> The statements run against <see cref="GitGraphDatabase"/> with the main branch name and order
///         of the text's own configuration; an operation Mermaid's database rejects is a located
///         <see cref="MermaidSyntaxException"/> with Mermaid's message. The title, <c>accTitle</c> and <c>accDescr</c> are
///         sanitized as <c>commonDb</c> sanitizes them.
///     </para>
///     <para>
///         <b>Limits.</b> The main branch, each branch and each commit count against <see cref="MermaidLimits.MaxNodes"/>;
///         every kept token text, every converted value, every sanitized text, the messages and tags Mermaid composes and
///         the sanitized frontmatter title are checked against <see cref="MermaidLimits.MaxLabelLength"/>
///         (<see cref="MermaidDiagnosticCodes.LimitExceeded"/>).
///     </para>
///     <para>
///         Parsing is linear in the text. An instance parses one text on one thread.
///     </para>
/// </remarks>
internal sealed partial class GitGraphParser
{
    /// <summary>The tokens that start an iteration of the statement loop.</summary>
    private static readonly string[] StatementStarts =
    [
        Newline, AccDescr, AccTitle, Title, CommitKeyword, BranchKeyword, MergeKeyword, CheckoutKeyword, SwitchKeyword, CherryPickKeyword,
    ];

    /// <summary>The commit types (<c>NORMAL</c>, <c>REVERSE</c> and <c>HIGHLIGHT</c>, Mermaid's 0, 1 and 2).</summary>
    private static readonly string[] CommitTypes = [NormalKeyword, ReverseKeyword, HighlightKeyword];

    /// <summary>The directions of the header.</summary>
    private static readonly string[] Directions = [LeftToRight, TopToBottom, BottomToTop];

    /// <summary>The tokens of a branch name.</summary>
    private static readonly string[] Names = [Reference, StringToken];

    /// <summary>The parser text.</summary>
    private readonly MappedText _text;

    /// <summary>The limits of the open call.</summary>
    private readonly MermaidLimits _limits;

    /// <summary>The tokens.</summary>
    private List<GitGraphToken> _tokens = [];

    /// <summary>The index of the lookahead token.</summary>
    private int _index;

    /// <summary>
    ///     Initializes a parser.
    /// </summary>
    /// <param name="text">The parser text.</param>
    /// <param name="limits">The limits of the open call.</param>
    private GitGraphParser(MappedText text, MermaidLimits limits)
    {
        _text = text;
        _limits = limits;
    }

    /// <summary>
    ///     Gets the lookahead token.
    /// </summary>
    private GitGraphToken Current => _tokens[_index];

    /// <summary>
    ///     Parses a git graph and runs its statements with the main branch of the text's own configuration.
    /// </summary>
    /// <param name="source">The preprocessed text (its frontmatter title is the fallback of the drawn title).</param>
    /// <param name="detected">The detected diagram (its keyword location).</param>
    /// <param name="limits">The limits of the open call.</param>
    /// <param name="textConfig">The configuration of the text alone (its <c>gitGraph.mainBranchName</c> and <c>mainBranchOrder</c>).</param>
    /// <returns>The diagram.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="MermaidSyntaxException">
    ///     Thrown, located, when the text does not follow the grammar, when Mermaid's database rejects a statement (Mermaid's
    ///     message) or when a limit is exceeded.
    /// </exception>
    public static GitGraphDiagram Parse(PreprocessedSource source, DetectedDiagram detected, MermaidLimits limits, MermaidConfig textConfig)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(detected);
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(textConfig);
        return new GitGraphParser(source.ParserText, limits).Run(detected.Location, source.Frontmatter, textConfig.GitGraph);
    }

    /// <summary>
    ///     Converts the text of a <c>STRING</c> token as Langium's <c>convertString</c> does: the quotes removed, the
    ///     escapes <c>\b \f \n \r \t \v \0</c> replaced, and any other escaped character kept without its backslash.
    /// </summary>
    /// <param name="input">The token text, quotes included.</param>
    /// <returns>The value.</returns>
    internal static string ConvertString(string input)
    {
        var builder = new StringBuilder(input.Length);
        var i = 1;
        while (i < input.Length - 1)
        {
            var c = input[i++];
            if (c != '\\')
            {
                builder.Append(c);
                continue;
            }

            // charAt past the end is the empty string
            if (i < input.Length)
            {
                builder.Append(input[i++] switch
                {
                    'b' => '\b',
                    'f' => '\f',
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    'v' => '\v',
                    '0' => '\0',
                    var other => other,
                });
            }
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Converts the text of a <c>TITLE</c>, <c>ACC_TITLE</c> or single-line <c>ACC_DESCR</c> token as the mermaid value
    ///     converter does: the text after the keyword (and its colon) trimmed, with each run of two or more tabs and
    ///     spaces made one space.
    /// </summary>
    /// <param name="value">The text after the keyword (and its colon).</param>
    /// <returns>The value.</returns>
    internal static string ConvertSingleLine(string value) => CollapseSpaces(Preprocessor.TrimJs(value));

    /// <summary>
    ///     Converts the text in the braces of a multi-line <c>ACC_DESCR</c> as the mermaid value converter does: the white
    ///     space at the start of each line removed (<c>/^\s*/gm</c>), the white space before each line end removed
    ///     (<c>/\s+$/gm</c>), runs of tabs and spaces made one space and runs of line breaks one line feed.
    /// </summary>
    /// <param name="value">The text in the braces.</param>
    /// <returns>The value.</returns>
    internal static string ConvertMultiLine(string value)
    {
        // /^\s*/gm: a white-space run that starts a line is removed, line breaks in it included
        var builder = new StringBuilder(value.Length);
        var i = 0;
        while (i < value.Length)
        {
            if (i == 0 || ScanPatterns.IsJsLineTerminator(value[i - 1]))
            {
                var start = i;
                while (i < value.Length && ScanPatterns.IsJsWhitespace(value[i]))
                {
                    i++;
                }

                if (i > start)
                {
                    continue;
                }
            }

            builder.Append(value[i]);
            i++;
        }

        // /\s+$/gm: a white-space run is removed up to its last line terminator, or wholly at the end of the text
        var text = builder.ToString();
        builder.Clear();
        i = 0;
        while (i < text.Length)
        {
            if (!ScanPatterns.IsJsWhitespace(text[i]))
            {
                builder.Append(text[i]);
                i++;
                continue;
            }

            var end = i;
            while (end < text.Length && ScanPatterns.IsJsWhitespace(text[end]))
            {
                end++;
            }

            // The run is kept from its last line terminator after its first character, or wholly without one
            var keep = i;
            if (end < text.Length)
            {
                for (var t = end - 1; t > i; t--)
                {
                    if (ScanPatterns.IsJsLineTerminator(text[t]))
                    {
                        keep = t;
                        break;
                    }
                }

                builder.Append(text, keep, end - keep);
            }

            i = end;
        }

        // [\t ]{2,} becomes one space, [\n\r]{2,} one line feed
        return CollapseRuns(CollapseSpaces(builder.ToString()), c => c is '\n' or '\r', '\n');
    }

    /// <summary>
    ///     Makes each run of two or more tabs and spaces one space (<c>/[\t ]{2,}/gm</c>).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text.</returns>
    private static string CollapseSpaces(string text) => CollapseRuns(text, c => c is '\t' or ' ', ' ');

    /// <summary>
    ///     Replaces each run of two or more characters of a class by one character.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="inRun">Whether a character is of the class.</param>
    /// <param name="replacement">The character that replaces a run.</param>
    /// <returns>The text.</returns>
    private static string CollapseRuns(string text, Func<char, bool> inRun, char replacement)
    {
        var builder = new StringBuilder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            var end = i;
            while (end < text.Length && inRun(text[end]))
            {
                end++;
            }

            if (end - i >= 2)
            {
                builder.Append(replacement);
                i = end;
                continue;
            }

            builder.Append(text[i]);
            i++;
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Parses the whole text (<c>GitGraph</c>) and runs its statements.
    /// </summary>
    /// <param name="keyword">The location of the diagram keyword.</param>
    /// <param name="frontmatter">The frontmatter, or null.</param>
    /// <param name="config">The gitGraph section of the text's own configuration.</param>
    /// <returns>The diagram.</returns>
    private GitGraphDiagram Run(MermaidLocation keyword, FrontmatterData? frontmatter, GitGraphConfig config)
    {
        _tokens = Tokenize(_text.Text);

        // NEWLINE* and the header: gitGraph | gitGraph ":" | gitGraph: | gitGraph Direction ":" (a direction without its
        // colon leaves the header alone, which ends the diagram there)
        while (Is(Newline))
        {
            _index++;
        }

        string? direction = null;
        if (Is(GitGraphColonKeyword))
        {
            _index++;
        }
        else if (Is(GitGraphKeyword))
        {
            _index++;
            if (Is(Colon))
            {
                _index++;
            }
            else if (Is(Directions) && _tokens[_index + 1].Kind == Colon)
            {
                direction = Current.Kind;
                _index += 2;
            }
        }
        else
        {
            throw Fail([GitGraphKeyword, GitGraphColonKeyword], "start the diagram with gitGraph, gitGraph: or a direction such as gitGraph TB:");
        }

        // (NEWLINE | TitleAndAccessibilities | Statement)*, the AST fields keeping the last value
        string? title = null;
        string? accTitle = null;
        string? accDescr = null;
        var statements = new List<GitStatement>();
        while (Is(StatementStarts))
        {
            switch (Current.Kind)
            {
                case Newline:
                    _index++;
                    break;

                case AccDescr:
                case AccTitle:
                case Title:
                    {
                        // TitleAndAccessibilities: one or more of the three, each ended by a line end
                        do
                        {
                            var statement = Current;
                            var raw = TextOf(statement);
                            _index++;
                            switch (statement.Kind)
                            {
                                case AccDescr:
                                    accDescr = ConvertAccDescr(raw, statement);
                                    break;
                                case AccTitle:
                                    accTitle = Checked(ConvertSingleLine(raw[(raw.IndexOf(':', StringComparison.Ordinal) + 1)..]), statement);
                                    break;
                                default:
                                    title = Checked(ConvertSingleLine(raw[(raw.IndexOf("title", StringComparison.Ordinal) + "title".Length)..]), statement);
                                    break;
                            }

                            EndOfLine();
                        }
                        while (Is(AccDescr, AccTitle, Title));
                        break;
                    }

                default:
                    statements.Add(Statement());
                    break;
            }
        }

        // The entry rule must end at the end of the tokens
        if (!Is(Eof))
        {
            throw Fail([Eof], "write each statement on a line of its own after the gitGraph line, starting with commit, branch, checkout, switch, merge or cherry-pick");
        }

        // populateCommonDb sets the texts that are not empty, testing each value before sanitizing it; the frontmatter
        // title is set before the text is parsed
        var description = string.IsNullOrEmpty(accDescr) ? string.Empty : CheckDerived(GitGraphDiagram.SanitizeDescription(accDescr), keyword);
        var accessibleTitle = string.IsNullOrEmpty(accTitle) ? string.Empty : CheckDerived(GitGraphDiagram.SanitizeAccTitle(accTitle), keyword);
        var diagramTitle = string.IsNullOrEmpty(title) ? string.Empty : CheckDerived(HtmlSanitizer.Sanitize(title), keyword);
        var frontmatterTitle = frontmatter?.Title is { Length: > 0 } text ? CheckDerived(HtmlSanitizer.Sanitize(text), frontmatter.Location) : string.Empty;

        // The database with the text's main branch: an operation Mermaid rejects is a syntax error of its statement
        var database = GitGraphDatabase.Run(statements, config.MainBranchName, config.MainBranchOrder, keyword, _limits, (code, message, location, hint) =>
            new MermaidSyntaxException(code, message, location, hint));
        return new GitGraphDiagram(
            keyword,
            direction ?? LeftToRight,
            diagramTitle,
            !string.IsNullOrEmpty(title),
            accessibleTitle,
            description,
            frontmatterTitle,
            statements,
            _limits,
            database);
    }

    /// <summary>
    ///     Parses a statement: <c>Commit</c>, <c>Branch</c>, <c>Merge</c>, <c>Checkout</c> or <c>CherryPicking</c>.
    /// </summary>
    /// <returns>The statement as written.</returns>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when the statement is not complete or a text is too long.</exception>
    private GitStatement Statement()
    {
        var first = Current;
        var location = Locate(first);
        _index++;
        string? id = null;
        string? message = null;
        string? parent = null;
        int? type = null;
        var tags = new List<string>();
        switch (first.Kind)
        {
            case CommitKeyword:
                // ("id:" STRING | "msg:"? STRING | "tag:" STRING | "type:" Type)*
                while (true)
                {
                    if (Is(IdKeyword))
                    {
                        _index++;
                        id = StringValue();
                    }
                    else if (Is(MsgKeyword) || Is(StringToken))
                    {
                        _index += Is(MsgKeyword) ? 1 : 0;
                        message = StringValue();
                    }
                    else if (Is(TagKeyword))
                    {
                        _index++;
                        tags.Add(StringValue());
                    }
                    else if (Is(TypeKeyword))
                    {
                        _index++;
                        type = TypeValue();
                    }
                    else
                    {
                        break;
                    }
                }

                EndOfLine();
                return new GitStatement(GitStatementKind.Commit, location) { Id = id, Message = message, Tags = tags, Type = type };

            case BranchKeyword:
                {
                    // (REFERENCE | STRING) ("order:" INT)?
                    var name = NameValue();
                    double? order = null;
                    if (Is(OrderKeyword))
                    {
                        _index++;
                        order = IntValue(Expect(Int, "give the order as digits without a leading zero, such as order: 2"));
                    }

                    EndOfLine();
                    return new GitStatement(GitStatementKind.Branch, location) { Name = name, Order = order };
                }

            case MergeKeyword:
                {
                    // (REFERENCE | STRING) ("id:" STRING | "tag:" STRING | "type:" Type)*
                    var name = NameValue();
                    while (true)
                    {
                        if (Is(IdKeyword))
                        {
                            _index++;
                            id = StringValue();
                        }
                        else if (Is(TagKeyword))
                        {
                            _index++;
                            tags.Add(StringValue());
                        }
                        else if (Is(TypeKeyword))
                        {
                            _index++;
                            type = TypeValue();
                        }
                        else
                        {
                            break;
                        }
                    }

                    EndOfLine();
                    return new GitStatement(GitStatementKind.Merge, location) { Name = name, Id = id, Tags = tags, Type = type };
                }

            case CheckoutKeyword:
            case SwitchKeyword:
                {
                    var name = NameValue();
                    EndOfLine();
                    return new GitStatement(GitStatementKind.Checkout, location) { Name = name };
                }

            default:
                // cherry-pick ("id:" STRING | "tag:" STRING | "parent:" STRING)*
                while (true)
                {
                    if (Is(IdKeyword))
                    {
                        _index++;
                        id = StringValue();
                    }
                    else if (Is(TagKeyword))
                    {
                        _index++;
                        tags.Add(StringValue());
                    }
                    else if (Is(ParentKeyword))
                    {
                        _index++;
                        parent = StringValue();
                    }
                    else
                    {
                        break;
                    }
                }

                EndOfLine();
                return new GitStatement(GitStatementKind.CherryPick, location) { Id = id, Tags = tags, Parent = parent };
        }
    }

    /// <summary>
    ///     Parses a quoted string and converts it.
    /// </summary>
    /// <returns>The value.</returns>
    private string StringValue()
    {
        var token = Expect(StringToken, "write the value in quotes, such as id: \"a1\" or tag: \"v1.0\"");
        return Checked(ConvertString(TextOf(token)), token);
    }

    /// <summary>
    ///     Parses a branch name: a <c>REFERENCE</c> as written or a converted <c>STRING</c>.
    /// </summary>
    /// <returns>The name.</returns>
    private string NameValue()
    {
        if (!Is(Names))
        {
            throw Fail(Names, "name the branch with letters, digits, '_', '-', '.' and '/', or in quotes; a keyword such as commit or LR needs quotes");
        }

        var token = _tokens[_index++];
        var text = TextOf(token);
        return token.Kind == StringToken ? Checked(ConvertString(text), token) : text;
    }

    /// <summary>
    ///     Parses a commit type (<c>NORMAL</c>, <c>REVERSE</c> or <c>HIGHLIGHT</c>).
    /// </summary>
    /// <returns>Mermaid's number of the type.</returns>
    private int TypeValue()
    {
        if (!Is(CommitTypes))
        {
            throw Fail(CommitTypes, "write the type in capitals: NORMAL, REVERSE or HIGHLIGHT");
        }

        return Array.IndexOf(CommitTypes, _tokens[_index++].Kind);
    }

    /// <summary>
    ///     Converts the text of an <c>INT</c> token as Langium's <c>convertInt</c> does (JavaScript's <c>parseInt</c>): the
    ///     nearest double of the digits.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The value.</returns>
    private double IntValue(GitGraphToken token) =>
        double.Parse(TextOf(token), NumberStyles.None, CultureInfo.InvariantCulture);

    /// <summary>
    ///     Converts the text of an <c>ACC_DESCR</c> token: the single-line form after its colon, or the multi-line form in its
    ///     braces.
    /// </summary>
    /// <param name="raw">The token text.</param>
    /// <param name="token">The token.</param>
    /// <returns>The value.</returns>
    private string ConvertAccDescr(string raw, GitGraphToken token)
    {
        var after = raw.IndexOf("accDescr", StringComparison.Ordinal) + "accDescr".Length;
        var colon = after;
        while (colon < raw.Length && raw[colon] is '\t' or ' ')
        {
            colon++;
        }

        var value = colon < raw.Length && raw[colon] == ':'
            ? ConvertSingleLine(raw[(colon + 1)..])
            : ConvertMultiLine(raw[(raw.IndexOf('{', after) + 1)..^1]);
        return Checked(value, token);
    }

    /// <summary>
    ///     Parses a line end (<c>EOL</c>: <c>NEWLINE+ | EOF</c>).
    /// </summary>
    private void EndOfLine()
    {
        if (Is(Eof))
        {
            return;
        }

        if (!Is(Newline))
        {
            throw Fail([Newline, Eof], "end the statement with a line break; write one statement per line with its options (id:, msg:, tag:, type:, order:, parent:) on it");
        }

        while (Is(Newline))
        {
            _index++;
        }
    }

    /// <summary>
    ///     Gets whether the lookahead is of one of the kinds.
    /// </summary>
    /// <param name="kinds">The kinds.</param>
    /// <returns>Whether it is.</returns>
    private bool Is(params string[] kinds) => Array.IndexOf(kinds, Current.Kind) >= 0;

    /// <summary>
    ///     Consumes the lookahead token when it is of the kind, and fails otherwise.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="hint">The suggested fix.</param>
    /// <returns>The token.</returns>
    private GitGraphToken Expect(string kind, string hint)
    {
        if (!Is(kind))
        {
            throw Fail([kind], hint);
        }

        return _tokens[_index++];
    }

    /// <summary>
    ///     Gets the text of a token that is kept, and checks <see cref="MermaidLimits.MaxLabelLength"/>.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The text.</returns>
    private string TextOf(GitGraphToken token) => Checked(_text.Text[token.Start..token.End], token);

    /// <summary>
    ///     Checks a text derived from a token against <see cref="MermaidLimits.MaxLabelLength"/>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="token">The token.</param>
    /// <returns>The text.</returns>
    private string Checked(string text, GitGraphToken token) => CheckDerived(text, Locate(token));

    /// <summary>
    ///     Checks a text against <see cref="MermaidLimits.MaxLabelLength"/>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="location">The location of the text.</param>
    /// <returns>The text.</returns>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when the text is too long.</exception>
    private string CheckDerived(string text, MermaidLocation location)
    {
        if (text.Length > _limits.MaxLabelLength)
        {
            throw new MermaidSyntaxException(
                MermaidDiagnosticCodes.LimitExceeded,
                GitGraphDatabase.LabelLimitMessage(text.Length, _limits),
                location,
                GitGraphDatabase.LabelLimitHint);
        }

        return text;
    }

    /// <summary>
    ///     Gets the location of a token in the original text.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The location.</returns>
    private MermaidLocation Locate(GitGraphToken token) => _text.Locate(token.Start);

    /// <summary>
    ///     Creates the exception for a lookahead token the grammar does not accept.
    /// </summary>
    /// <param name="expected">The token names chevrotain lists (the first token of each sequence).</param>
    /// <param name="hint">The suggested fix.</param>
    /// <returns>The located exception.</returns>
    private MermaidSyntaxException Fail(string[] expected, string hint)
    {
        var token = Current;
        var found = token.Kind == Eof ? "the end of the text" : $"'{token.Kind}' ({Describe(_text.Text[token.Start..token.End])})";
        return new MermaidSyntaxException(
            MermaidDiagnosticCodes.SyntaxError,
            $"expected {TokenCursor.JoinAlternatives([.. expected.Select(e => $"'{e}'")])}, found {found}",
            Locate(token),
            hint,
            expected);
    }

    /// <summary>
    ///     Describes the text of a token for a message.
    /// </summary>
    /// <param name="text">The token text.</param>
    /// <returns>The quoted text, shortened and with line breaks shown.</returns>
    private static string Describe(string text)
    {
        var shown = text.Length > 24 ? text[..24] + "..." : text;
        return "'" + shown.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal) + "'";
    }
}

/// <summary>
///     The kinds of git graph statements.
/// </summary>
internal enum GitStatementKind
{
    /// <summary>A <c>commit</c>.</summary>
    Commit,

    /// <summary>A <c>branch</c>.</summary>
    Branch,

    /// <summary>A <c>merge</c>.</summary>
    Merge,

    /// <summary>A <c>checkout</c> or <c>switch</c>.</summary>
    Checkout,

    /// <summary>A <c>cherry-pick</c>.</summary>
    CherryPick,
}

/// <summary>
///     A git graph statement as written (the values of its options, converted but not sanitized).
/// </summary>
/// <param name="Kind">The kind.</param>
/// <param name="Location">The location of its keyword.</param>
internal sealed record GitStatement(GitStatementKind Kind, MermaidLocation Location)
{
    /// <summary>Gets the <c>id:</c> of a commit, merge or cherry-pick, or null.</summary>
    public string? Id { get; init; }

    /// <summary>Gets the message of a commit, or null.</summary>
    public string? Message { get; init; }

    /// <summary>Gets the branch name of a branch, merge or checkout, or null.</summary>
    public string? Name { get; init; }

    /// <summary>Gets the <c>order:</c> of a branch, or null.</summary>
    public double? Order { get; init; }

    /// <summary>Gets the <c>type:</c> of a commit or merge (Mermaid's 0, 1 or 2), or null.</summary>
    public int? Type { get; init; }

    /// <summary>Gets the <c>parent:</c> of a cherry-pick, or null.</summary>
    public string? Parent { get; init; }

    /// <summary>Gets the tags in order.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];
}

/// <summary>
///     A parsed git graph: its statements, the database they make with the text's own main branch, the title and
///     accessibility texts of <c>commonDb.ts</c>, and the metadata of the document; a render lays it out with
///     <see cref="GitGraphRenderer"/>.
/// </summary>
/// <remarks>
///     The diagram is never changed after parsing and is safe for concurrent use.
/// </remarks>
internal sealed class GitGraphDiagram : IMermaidDiagram
{
    /// <summary>
    ///     Initializes a diagram.
    /// </summary>
    /// <param name="keywordLocation">The location of the diagram keyword.</param>
    /// <param name="direction">The direction (<c>LR</c>, <c>TB</c> or <c>BT</c>).</param>
    /// <param name="diagramTitle">The sanitized title, or empty.</param>
    /// <param name="isTitleSet">Whether a <c>title</c> statement with a value set the title.</param>
    /// <param name="accessibleTitle">The sanitized accessible title, or empty.</param>
    /// <param name="accessibleDescription">The sanitized accessible description, or empty.</param>
    /// <param name="frontmatterTitle">The sanitized frontmatter title, or empty.</param>
    /// <param name="statements">The statements in order.</param>
    /// <param name="limits">The limits of the open call (a database run at render keeps them).</param>
    /// <param name="database">The database of the text's own main branch.</param>
    public GitGraphDiagram(
        MermaidLocation keywordLocation,
        string direction,
        string diagramTitle,
        bool isTitleSet,
        string accessibleTitle,
        string accessibleDescription,
        string frontmatterTitle,
        IReadOnlyList<GitStatement> statements,
        MermaidLimits limits,
        GitGraphDatabase database)
    {
        KeywordLocation = keywordLocation;
        Direction = direction;
        DiagramTitle = diagramTitle;
        IsTitleSet = isTitleSet;
        AccessibleTitle = accessibleTitle;
        AccessibleDescription = accessibleDescription;
        FrontmatterTitle = frontmatterTitle;
        Statements = statements;
        Limits = limits;
        OpenDatabase = database;
    }

    /// <summary>Gets the location of the diagram keyword.</summary>
    public MermaidLocation KeywordLocation { get; }

    /// <summary>Gets the direction (<c>LR</c>, <c>TB</c> or <c>BT</c>).</summary>
    public string Direction { get; }

    /// <summary>Gets the title of the <c>title</c> statement, sanitized (empty when there is none).</summary>
    public string DiagramTitle { get; }

    /// <summary>
    ///     Gets a value indicating whether a <c>title</c> statement with a value set the title, even one that sanitizing
    ///     empties.
    /// </summary>
    public bool IsTitleSet { get; }

    /// <summary>Gets the accessible title (empty when there is none).</summary>
    public string AccessibleTitle { get; }

    /// <summary>Gets the accessible description (empty when there is none).</summary>
    public string AccessibleDescription { get; }

    /// <summary>Gets the frontmatter title, sanitized (empty when there is none).</summary>
    public string FrontmatterTitle { get; }

    /// <summary>
    ///     Gets the title Mermaid draws: the database's title, which is the frontmatter title until a <c>title</c>
    ///     statement with a value replaces it, even one that sanitizes to empty.
    /// </summary>
    public string DrawnTitle => IsTitleSet ? DiagramTitle : FrontmatterTitle;

    /// <summary>Gets the statements in order.</summary>
    public IReadOnlyList<GitStatement> Statements { get; }

    /// <summary>Gets the limits of the open call.</summary>
    public MermaidLimits Limits { get; }

    /// <summary>Gets the database of the text's own main branch name and order.</summary>
    public GitGraphDatabase OpenDatabase { get; }

    /// <summary>Gets the title the diagram declares, or null (the frontmatter title is the document's fallback unless IsTitleSet).</summary>
    public string? Title => DiagramTitle.Length > 0 ? DiagramTitle : null;

    /// <summary>Gets the accessible title, or null.</summary>
    public string? AccTitle => AccessibleTitle.Length > 0 ? AccessibleTitle : null;

    /// <summary>Gets the number of commits.</summary>
    public int NodeCount => OpenDatabase.Commits.Count;

    /// <summary>Gets the number of parent links (the arrows).</summary>
    public int EdgeCount => OpenDatabase.Commits.Sum(commit => commit.Parents.Count);

    /// <summary>Gets the number of subgraphs: a git graph has none.</summary>
    public int SubgraphCount => 0;

    /// <inheritdoc />
    public Scene Layout(DiagramRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new GitGraphRenderer(this, context).Draw();
    }

    /// <summary>
    ///     Gets the database of a render's configuration: the one of the text when the main branch name and order are
    ///     the same, else the statements run again (as Mermaid runs them with the configuration of the render).
    /// </summary>
    /// <param name="config">The gitGraph section of the render.</param>
    /// <returns>The database.</returns>
    /// <exception cref="MermaidRenderException">
    ///     Thrown, located at the statement, with <see cref="MermaidDiagnosticCodes.RenderFailed"/> and Mermaid's message
    ///     when the database rejects a statement under the render's main branch, or with
    ///     <see cref="MermaidDiagnosticCodes.LimitExceeded"/> when a limit is exceeded.
    /// </exception>
    public GitGraphDatabase Database(GitGraphConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.MainBranchName == OpenDatabase.MainBranchName && config.MainBranchOrder.CompareTo(OpenDatabase.MainBranchOrder) == 0)
        {
            return OpenDatabase;
        }

        return GitGraphDatabase.Run(Statements, config.MainBranchName, config.MainBranchOrder, KeywordLocation, Limits, (code, message, location, hint) =>
            new MermaidRenderException(code == MermaidDiagnosticCodes.SyntaxError ? MermaidDiagnosticCodes.RenderFailed : code, message, location, hint));
    }

    /// <summary>
    ///     Sanitizes an accessible title as Mermaid's <c>setAccTitle</c> does: sanitized, the leading white space removed.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The accessible title.</returns>
    internal static string SanitizeAccTitle(string text)
    {
        var sanitized = HtmlSanitizer.Sanitize(text);
        var start = 0;
        while (start < sanitized.Length && ScanPatterns.IsJsWhitespace(sanitized[start]))
        {
            start++;
        }

        return sanitized[start..];
    }

    /// <summary>
    ///     Sanitizes an accessible description as Mermaid's <c>setAccDescription</c> does: sanitized, the white space after
    ///     each line feed removed (<c>/\n\s+/g</c>).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The accessible description.</returns>
    internal static string SanitizeDescription(string text)
    {
        var sanitized = HtmlSanitizer.Sanitize(text);
        var builder = new StringBuilder(sanitized.Length);
        var i = 0;
        while (i < sanitized.Length)
        {
            builder.Append(sanitized[i]);
            i++;
            if (sanitized[i - 1] == '\n')
            {
                while (i < sanitized.Length && ScanPatterns.IsJsWhitespace(sanitized[i]))
                {
                    i++;
                }
            }
        }

        return builder.ToString();
    }
}
