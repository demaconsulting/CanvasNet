// Portions derived from mermaid (MIT), @mermaid-js/parser (MIT) and Langium (MIT); see third-party/README.md
using System.Globalization;
using System.Text;
using DemaConsulting.CanvasNet.Mermaid.Dagre;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Packet;

/// <summary>
///     The packet parser: a hand-written port of the Langium grammar of <c>@mermaid-js/parser</c> (<c>packet.langium</c>
///     with <c>common.langium</c>, its token builder and value converters) and of Mermaid's packet database
///     (<c>db.ts</c> and the <c>populate</c> of <c>parser.ts</c>, with <c>populateCommonDb</c> and <c>commonDb.ts</c>),
///     producing a <see cref="PacketDiagram"/>.
/// </summary>
/// <remarks>
///     <para>
///         <b>Lexer.</b> The token types are tried at each position in the order Langium gives chevrotain (the keywords
///         <c>packet-beta</c>, <c>packet</c>, <c>-</c>, <c>+</c> and <c>:</c>, then the terminals <c>ACC_DESCR</c>,
///         <c>ACC_TITLE</c>, <c>TITLE</c>, <c>INT</c>, <c>STRING</c> and <c>NEWLINE</c>, then the hidden white space, YAML,
///         directives and comments), and the first that matches wins, as in chevrotain. Only <c>packet</c> must be followed
///         by <c>%%</c> or by white space. The texts are matched by hand-written scanners with the matches of Langium's
///         regular expressions, so lexing is linear in the text.
///     </para>
///     <para>
///         <b>Grammar.</b> <c>NEWLINE* ("packet" | "packet-beta") (TitleAndAccessibilities | PacketBlock | NEWLINE)* EOF</c>,
///         where a block is <c>(INT ("-" INT)? | "+" INT) ":" STRING EOL</c>, is parsed with one token of lookahead (the
///         grammar is LL(1)). Errors come where Mermaid reports its first one: a lexical error at the first character no
///         token matches, before any grammar error (Mermaid lists the lexer's errors first), else the first token the
///         grammar does not accept, with the token names chevrotain lists as <see cref="MermaidSyntaxException.Expected"/>.
///     </para>
///     <para>
///         <b>Values.</b> A title, <c>accTitle</c> or <c>accDescr</c> is converted with the regular expressions of the
///         mermaid value converter (trimmed, runs of spaces collapsed; the lines of a multi-line description trimmed), a
///         string with Langium's <c>convertString</c> (the quotes removed; <c>\b \f \n \r \t \v \0</c> escapes, and any
///         other escaped character taken as itself), and an integer with JavaScript's <c>parseInt</c> (a double, so very
///         long numbers are rounded and a number of more than 308 digits is infinite). The last of each statement wins; an
///         empty one is not set (<c>populateCommonDb</c> tests it for truth). A label is kept as it is (Mermaid sets it as
///         text, without sanitizing it).
///     </para>
///     <para>
///         <b>Database.</b> As Mermaid's <c>populate</c>, each block in order fails (a located
///         <see cref="MermaidSyntaxException"/> with Mermaid's message) when its end is before its start, when it does not
///         start at the bit after the last block's end, or when its bit count is zero; a missing start is the next bit, a
///         missing end the start plus the bit count (1 by default) less one. The title, <c>accTitle</c> and <c>accDescr</c>
///         are sanitized as <c>commonDb</c> sanitizes them. The rows depend on the configuration of a render, so
///         <see cref="PacketDiagram.Rows"/> splits the blocks when the diagram is drawn.
///     </para>
///     <para>
///         <b>Limits.</b> Every block statement counts against <see cref="MermaidLimits.MaxNodes"/>; every kept token
///         text, every converted value, every sanitized text and the sanitized frontmatter title are checked against
///         <see cref="MermaidLimits.MaxLabelLength"/> (<see cref="MermaidDiagnosticCodes.LimitExceeded"/>).
///     </para>
///     <para>
///         Parsing is linear in the text. An instance parses one text on one thread.
///     </para>
/// </remarks>
internal sealed partial class PacketParser
{
    /// <summary>The tokens that start an iteration of the statement loop.</summary>
    private static readonly string[] StatementStarts = [AccDescr, AccTitle, Title, Int, Plus, Newline];

    /// <summary>The parser text.</summary>
    private readonly MappedText _text;

    /// <summary>The limits of the open call.</summary>
    private readonly MermaidLimits _limits;

    /// <summary>The tokens.</summary>
    private List<PacketToken> _tokens = [];

    /// <summary>The index of the lookahead token.</summary>
    private int _index;

    /// <summary>
    ///     Initializes a parser.
    /// </summary>
    /// <param name="text">The parser text.</param>
    /// <param name="limits">The limits of the open call.</param>
    private PacketParser(MappedText text, MermaidLimits limits)
    {
        _text = text;
        _limits = limits;
    }

    /// <summary>
    ///     Gets the lookahead token.
    /// </summary>
    private PacketToken Current => _tokens[_index];

    /// <summary>
    ///     Parses a packet diagram.
    /// </summary>
    /// <param name="source">The preprocessed text (its frontmatter title is the fallback of the drawn title).</param>
    /// <param name="detected">The detected diagram (its keyword location).</param>
    /// <param name="limits">The limits of the open call.</param>
    /// <returns>The diagram.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="MermaidSyntaxException">
    ///     Thrown, located, when the text does not follow the grammar, when a block is not valid (Mermaid's message) or
    ///     when a limit is exceeded.
    /// </exception>
    public static PacketDiagram Parse(PreprocessedSource source, DetectedDiagram detected, MermaidLimits limits)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(detected);
        ArgumentNullException.ThrowIfNull(limits);
        return new PacketParser(source.ParserText, limits).Run(detected.Location, source.Frontmatter);
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
    ///     Parses the whole text (<c>Packet</c>) and fills the database.
    /// </summary>
    /// <param name="keyword">The location of the diagram keyword.</param>
    /// <param name="frontmatter">The frontmatter, or null.</param>
    /// <returns>The diagram.</returns>
    private PacketDiagram Run(MermaidLocation keyword, FrontmatterData? frontmatter)
    {
        _tokens = Tokenize(_text.Text);

        // NEWLINE* ("packet" | "packet-beta")
        while (Is(Newline))
        {
            _index++;
        }

        if (!Is(PacketKeyword, PacketBetaKeyword))
        {
            throw Fail([PacketKeyword, PacketBetaKeyword], "start the diagram with the packet or packet-beta keyword");
        }

        _index++;

        // (TitleAndAccessibilities | PacketBlock | NEWLINE)*, the AST fields keeping the last value
        string? title = null;
        string? accTitle = null;
        string? accDescr = null;
        var blocks = new List<ParsedBlock>();
        while (Is(StatementStarts))
        {
            var token = Current;
            switch (token.Kind)
            {
                case Newline:
                    _index++;
                    break;

                case Int:
                case Plus:
                    blocks.Add(Block(blocks.Count));
                    break;

                default:
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
            }
        }

        // The entry rule must end at the end of the tokens
        if (!Is(Eof))
        {
            throw Fail([Eof], "write each block, title, accTitle and accDescr on a line of its own, after the packet line");
        }

        return Populate(keyword, frontmatter, title, accTitle, accDescr, blocks);
    }

    /// <summary>
    ///     Parses a block statement (<c>PacketBlock</c>): <c>(INT ("-" INT)? | "+" INT) ":" STRING EOL</c>.
    /// </summary>
    /// <param name="count">The number of block statements before this one.</param>
    /// <returns>The block as written.</returns>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when the statement is not complete or a limit is exceeded.</exception>
    private ParsedBlock Block(int count)
    {
        var first = Current;
        if (count >= _limits.MaxNodes)
        {
            throw new MermaidSyntaxException(
                MermaidDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"the diagram has more than {_limits.MaxNodes} blocks (MermaidLimits.MaxNodes)"),
                Locate(first),
                "split the packet diagram, or raise MermaidLimits.MaxNodes when the source is trusted");
        }

        // A bit count, or a start bit with an optional end bit
        double? start = null;
        double? end = null;
        double? bits = null;
        if (Is(Plus))
        {
            _index++;
            bits = IntValue(Expect(Int));
        }
        else
        {
            start = IntValue(Expect(Int));
            if (Is(Dash))
            {
                _index++;
                end = IntValue(Expect(Int));
            }
        }

        // The colon, the label and the line end
        Expect(Colon);
        var label = Expect(StringToken);
        var value = Checked(ConvertString(TextOf(label)), label);
        EndOfLine();
        return new ParsedBlock(start, end, bits, value, Locate(first));
    }

    /// <summary>
    ///     Converts the text of an <c>INT</c> token as Langium's <c>convertInt</c> does (JavaScript's <c>parseInt</c>): the
    ///     nearest double of the digits.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The value.</returns>
    private double IntValue(PacketToken token) =>
        double.Parse(TextOf(token), NumberStyles.None, CultureInfo.InvariantCulture);

    /// <summary>
    ///     Fills the database from the parsed values, as Mermaid's <c>populateCommonDb</c> and the validation of
    ///     <c>populate</c> do.
    /// </summary>
    /// <param name="keyword">The location of the diagram keyword.</param>
    /// <param name="frontmatter">The frontmatter, or null.</param>
    /// <param name="title">The title, or null.</param>
    /// <param name="accTitle">The accessible title, or null.</param>
    /// <param name="accDescr">The accessible description, or null.</param>
    /// <param name="parsed">The block statements in order.</param>
    /// <returns>The diagram.</returns>
    /// <exception cref="MermaidSyntaxException">Thrown, located, at the first block that is not valid.</exception>
    private PacketDiagram Populate(
        MermaidLocation keyword,
        FrontmatterData? frontmatter,
        string? title,
        string? accTitle,
        string? accDescr,
        List<ParsedBlock> parsed)
    {
        // populateCommonDb sets the texts that are not empty; the frontmatter title is set before the text is parsed
        var description = string.IsNullOrEmpty(accDescr) ? string.Empty : CheckDerived(PacketDiagram.SanitizeDescription(accDescr), keyword);
        var accessibleTitle = string.IsNullOrEmpty(accTitle) ? string.Empty : CheckDerived(PacketDiagram.SanitizeAccTitle(accTitle), keyword);
        var diagramTitle = string.IsNullOrEmpty(title) ? string.Empty : CheckDerived(PacketDiagram.Sanitize(title), keyword);
        var frontmatterTitle = frontmatter?.Title is { Length: > 0 } text ? CheckDerived(PacketDiagram.Sanitize(text), frontmatter.Location) : string.Empty;

        // populate: each block in order is validated, and its missing start, end and bit count are filled in
        var blocks = new List<PacketBlock>(parsed.Count);
        var lastBit = -1.0;
        foreach (var (start, end, bits, label, location) in parsed)
        {
            if (start is { } given && end is { } last && last < given)
            {
                throw Invalid($"Packet block {Js(given)} - {Js(last)} is invalid. End must be greater than start.", location, "write the end bit after the start bit, such as 4-7");
            }

            var first = start ?? lastBit + 1;
            if (!PacketDiagram.JsEquals(first, lastBit + 1))
            {
                throw Invalid(
                    $"Packet block {Js(first)} - {Js(end ?? first)} is not contiguous. It should start from {Js(lastBit + 1)}.",
                    location,
                    $"start the block at bit {Js(lastBit + 1)}: each block starts at the bit after the end of the block before it");
            }

            if (bits == 0)
            {
                throw Invalid($"Packet block {Js(first)} is invalid. Cannot have a zero bit field.", location, "give the block at least one bit, such as +1");
            }

            var final = end ?? first + (bits ?? 1) - 1;
            blocks.Add(new PacketBlock(first, final, bits ?? final - first + 1, label, location));
            lastBit = final;
        }

        return new PacketDiagram(keyword, diagramTitle, accessibleTitle, description, frontmatterTitle, blocks);
    }

    /// <summary>
    ///     Writes a number as JavaScript writes it in a template literal.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The text.</returns>
    private static string Js(double value) => YamlSubsetParser.FormatJsNumber(value);

    /// <summary>
    ///     Creates the exception for a block that Mermaid's database rejects.
    /// </summary>
    /// <param name="message">Mermaid's message.</param>
    /// <param name="location">The location of the block statement.</param>
    /// <param name="hint">The suggested fix.</param>
    /// <returns>The located exception.</returns>
    private static MermaidSyntaxException Invalid(string message, MermaidLocation location, string hint) =>
        new(MermaidDiagnosticCodes.SyntaxError, message, location, hint);

    /// <summary>
    ///     Converts the text of an <c>ACC_DESCR</c> token: the single-line form after its colon, or the multi-line form in its
    ///     braces.
    /// </summary>
    /// <param name="raw">The token text.</param>
    /// <param name="token">The token.</param>
    /// <returns>The value.</returns>
    private string ConvertAccDescr(string raw, PacketToken token)
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
            throw Fail([Newline, Eof], "end the statement with a line break; write one statement per line");
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
    /// <returns>The token.</returns>
    private PacketToken Expect(string kind)
    {
        if (!Is(kind))
        {
            throw Fail([kind], kind switch
            {
                Colon => "separate the bits and the label with a colon: 0-15: \"Source Port\"",
                StringToken => "give the block a quoted label: 0-15: \"Source Port\"",
                _ => "write a bit number as digits without a leading zero, such as 0, 8 or 15",
            });
        }

        return _tokens[_index++];
    }

    /// <summary>
    ///     Gets the text of a token that is kept, and checks <see cref="MermaidLimits.MaxLabelLength"/>.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The text.</returns>
    private string TextOf(PacketToken token) => Checked(_text.Text[token.Start..token.End], token);

    /// <summary>
    ///     Checks a text derived from a token against <see cref="MermaidLimits.MaxLabelLength"/>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="token">The token.</param>
    /// <returns>The text.</returns>
    private string Checked(string text, PacketToken token) => CheckDerived(text, Locate(token));

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
                string.Create(CultureInfo.InvariantCulture, $"a text is {text.Length} characters long, more than the limit of {_limits.MaxLabelLength} (MermaidLimits.MaxLabelLength)"),
                location,
                "shorten the text, or raise MermaidLimits.MaxLabelLength when the source is trusted");
        }

        return text;
    }

    /// <summary>
    ///     Gets the location of a token in the original text.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The location.</returns>
    private MermaidLocation Locate(PacketToken token) => _text.Locate(token.Start);

    /// <summary>
    ///     Creates the exception for a lookahead token the grammar does not accept.
    /// </summary>
    /// <param name="expected">The token names chevrotain lists.</param>
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
///     A block statement as written: its start bit, end bit and bit count where given, its label and where it is.
/// </summary>
/// <param name="Start">The start bit, or null for a bit count.</param>
/// <param name="End">The end bit of a range, or null.</param>
/// <param name="Bits">The bit count of <c>+N</c>, or null.</param>
/// <param name="Label">The label, as the string converter gives it.</param>
/// <param name="Location">The location of the statement.</param>
internal sealed record ParsedBlock(double? Start, double? End, double? Bits, string Label, MermaidLocation Location);

/// <summary>
///     A block of a packet diagram (an entry of Mermaid's packet database) or a part of one in a row: its first and last
///     bit, its bit count as Mermaid keeps it, its label and the statement it comes from.
/// </summary>
/// <remarks>
///     A part that the row split makes has Mermaid's bit count of <c>getNextFittingBlock</c>, which is one less than its
///     bits (the renderer reads only the start and the end).
/// </remarks>
/// <param name="Start">The first bit.</param>
/// <param name="End">The last bit.</param>
/// <param name="Bits">The bit count.</param>
/// <param name="Label">The label.</param>
/// <param name="Location">The location of the block statement.</param>
internal sealed record PacketBlock(double Start, double End, double Bits, string Label, MermaidLocation Location);

/// <summary>
///     A parsed packet diagram: the state of Mermaid's packet database (the blocks, with the title and accessibility texts
///     of <c>commonDb.ts</c>) and the metadata of the document; a render splits it into rows
///     (<see cref="Rows"/>) and lays it out with <see cref="PacketRenderer"/>.
/// </summary>
/// <remarks>
///     The diagram is never changed after parsing and is safe for concurrent use.
/// </remarks>
internal sealed class PacketDiagram : IMermaidDiagram
{
    /// <summary>The most rows Mermaid makes (<c>maxPacketSize</c>).</summary>
    internal const int MaxPacketSize = 10_000;

    /// <summary>
    ///     Initializes a diagram.
    /// </summary>
    /// <param name="keywordLocation">The location of the diagram keyword.</param>
    /// <param name="diagramTitle">The sanitized title, or empty.</param>
    /// <param name="accessibleTitle">The sanitized accessible title, or empty.</param>
    /// <param name="accessibleDescription">The sanitized accessible description, or empty.</param>
    /// <param name="frontmatterTitle">The sanitized frontmatter title, or empty.</param>
    /// <param name="blocks">The blocks in order, each with its start, end and bit count.</param>
    public PacketDiagram(
        MermaidLocation keywordLocation,
        string diagramTitle,
        string accessibleTitle,
        string accessibleDescription,
        string frontmatterTitle,
        IReadOnlyList<PacketBlock> blocks)
    {
        KeywordLocation = keywordLocation;
        DiagramTitle = diagramTitle;
        AccessibleTitle = accessibleTitle;
        AccessibleDescription = accessibleDescription;
        FrontmatterTitle = frontmatterTitle;
        Blocks = blocks;
    }

    /// <summary>Gets the location of the diagram keyword.</summary>
    public MermaidLocation KeywordLocation { get; }

    /// <summary>Gets the title of the <c>title</c> statement, sanitized (empty when there is none).</summary>
    public string DiagramTitle { get; }

    /// <summary>Gets the accessible title (empty when there is none).</summary>
    public string AccessibleTitle { get; }

    /// <summary>Gets the accessible description (empty when there is none).</summary>
    public string AccessibleDescription { get; }

    /// <summary>Gets the frontmatter title, sanitized (empty when there is none).</summary>
    public string FrontmatterTitle { get; }

    /// <summary>
    ///     Gets the title Mermaid draws: the database's title, which is the frontmatter title until a non-empty
    ///     <c>title</c> statement replaces it.
    /// </summary>
    public string DrawnTitle => DiagramTitle.Length > 0 ? DiagramTitle : FrontmatterTitle;

    /// <summary>Gets the blocks in order.</summary>
    public IReadOnlyList<PacketBlock> Blocks { get; }

    /// <summary>Gets the title the diagram declares, or null (the frontmatter title is the document's fallback).</summary>
    public string? Title => DiagramTitle.Length > 0 ? DiagramTitle : null;

    /// <summary>Gets the accessible title, or null.</summary>
    public string? AccTitle => AccessibleTitle.Length > 0 ? AccessibleTitle : null;

    /// <summary>Gets the direction: a packet diagram has none.</summary>
    public string? Direction => null;

    /// <summary>Gets the number of block statements.</summary>
    public int NodeCount => Blocks.Count;

    /// <summary>Gets the number of edges: a packet diagram has none.</summary>
    public int EdgeCount => 0;

    /// <summary>Gets the number of subgraphs: a packet diagram has none.</summary>
    public int SubgraphCount => 0;

    /// <inheritdoc />
    public Scene Layout(DiagramRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new PacketRenderer(this, context).Draw();
    }

    /// <summary>
    ///     Splits the blocks into rows of <paramref name="bitsPerRow"/> bits, exactly as Mermaid's <c>populate</c> does
    ///     with <c>getNextFittingBlock</c>: a block that passes the end of its row is cut there and continues in the next
    ///     row, the parts keeping Mermaid's bit counts; a row is complete when a part ends at its last bit; a block is left
    ///     out once the row being filled has more than <c>bitsPerRow + 1</c> parts, and the split stops at 10,000 rows.
    /// </summary>
    /// <remarks>
    ///     All arithmetic is in doubles, as in JavaScript, so a fractional, zero or negative <paramref name="bitsPerRow"/>
    ///     splits as Mermaid splits it. The meter is charged 1 for each block and 1 for each part. Pure; safe for
    ///     concurrent use.
    /// </remarks>
    /// <param name="bitsPerRow">The bits of a row (<c>packet.bitsPerRow</c>).</param>
    /// <param name="limits">The limits of the call (<see cref="MermaidLimits.MaxNodes"/> bounds the parts).</param>
    /// <param name="meter">The work meter of the render.</param>
    /// <returns>The rows in order, none empty.</returns>
    /// <exception cref="MermaidRenderException">
    ///     Thrown, located at the block statement, with <see cref="MermaidDiagnosticCodes.RenderFailed"/> and Mermaid's
    ///     message when a part would start after its end (as <c>getNextFittingBlock</c> throws for some fractional
    ///     <paramref name="bitsPerRow"/>), and with <see cref="MermaidDiagnosticCodes.LimitExceeded"/> when there would be
    ///     more parts than <see cref="MermaidLimits.MaxNodes"/> or the work limit is reached.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when the render is canceled.</exception>
    public IReadOnlyList<IReadOnlyList<PacketBlock>> Rows(double bitsPerRow, MermaidLimits limits, LayoutWorkMeter meter)
    {
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(meter);
        var rows = new List<IReadOnlyList<PacketBlock>>();
        var word = new List<PacketBlock>();
        var row = 1.0;
        var parts = 0;
        foreach (var statement in Blocks)
        {
            meter.Charge(1);
            var block = statement;
            while (word.Count <= bitsPerRow + 1 && rows.Count < MaxPacketSize)
            {
                // getNextFittingBlock: the part that fits the row, and the rest of the block
                meter.Charge(1);
                var (part, next) = NextFittingBlock(block, row, bitsPerRow);
                if (++parts > limits.MaxNodes)
                {
                    throw new MermaidRenderException(
                        MermaidDiagnosticCodes.LimitExceeded,
                        string.Create(CultureInfo.InvariantCulture, $"the rows of the diagram have more than {limits.MaxNodes} blocks (MermaidLimits.MaxNodes)"),
                        statement.Location,
                        "shorten the bit ranges, raise packet.bitsPerRow, or raise MermaidLimits.MaxNodes when the source is trusted");
                }

                // A part that ends the row completes it
                word.Add(part);
                if (JsEquals(part.End + 1, row * bitsPerRow))
                {
                    rows.Add(word);
                    word = [];
                    row++;
                }

                if (next is null)
                {
                    break;
                }

                block = next;
            }
        }

        // pushWord ignores an empty row
        if (word.Count > 0)
        {
            rows.Add(word);
        }

        return rows;
    }

    /// <summary>
    ///     Compares two numbers as JavaScript's <c>===</c> does: exactly, zero equal to negative zero, NaN equal to nothing.
    /// </summary>
    /// <param name="a">The first number.</param>
    /// <param name="b">The second number.</param>
    /// <returns>Whether they are equal.</returns>
    internal static bool JsEquals(double a, double b) => !double.IsNaN(a) && !double.IsNaN(b) && a.CompareTo(b) == 0;

    /// <summary>
    ///     Sanitizes a text as Mermaid's <c>sanitizeText</c> does (an empty text is kept): <c>setDiagramTitle</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The sanitized text.</returns>
    internal static string Sanitize(string text) => text.Length == 0 ? text : HtmlSanitizer.Sanitize(text);

    /// <summary>
    ///     Sanitizes an accessible title as Mermaid's <c>setAccTitle</c> does: sanitized, the leading white space removed.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The accessible title.</returns>
    internal static string SanitizeAccTitle(string text)
    {
        var sanitized = Sanitize(text);
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
        var sanitized = Sanitize(text);
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

    /// <summary>
    ///     Mermaid's <c>getNextFittingBlock</c>: the block when it ends in the row, else its part up to the row's last bit
    ///     and the rest from the next row's first bit (the bit counts of both one less than their bits, as in Mermaid).
    /// </summary>
    /// <param name="block">The block or the rest of one.</param>
    /// <param name="row">The 1-based row being filled.</param>
    /// <param name="bitsPerRow">The bits of a row.</param>
    /// <returns>The part that fits, and the rest or null.</returns>
    /// <exception cref="MermaidRenderException">Thrown, located, when the block starts after its end.</exception>
    private static (PacketBlock Part, PacketBlock? Next) NextFittingBlock(PacketBlock block, double row, double bitsPerRow)
    {
        if (block.Start > block.End)
        {
            throw new MermaidRenderException(
                MermaidDiagnosticCodes.RenderFailed,
                $"Block start {YamlSubsetParser.FormatJsNumber(block.Start)} is greater than block end {YamlSubsetParser.FormatJsNumber(block.End)}.",
                block.Location,
                "set packet.bitsPerRow to a positive whole number, such as 32");
        }

        if (block.End + 1 <= row * bitsPerRow)
        {
            return (block, null);
        }

        var rowEnd = (row * bitsPerRow) - 1;
        var rowStart = row * bitsPerRow;
        return (
            block with { End = rowEnd, Bits = rowEnd - block.Start },
            block with { Start = rowStart, Bits = block.End - rowStart });
    }
}
