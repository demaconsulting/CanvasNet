// Portions derived from PlantUML (MIT); see third-party/README.md
using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;
using DemaConsulting.CanvasNet.PlantUml.Syntax;

namespace DemaConsulting.CanvasNet.PlantUml.Svek;

/// <summary>
///     The kind of a name written in a statement of a graph diagram.
/// </summary>
internal enum SvekNameKind
{
    /// <summary>An identifier (<c>Foo</c>, <c>geo.Square</c>, <c>a::b</c>).</summary>
    Plain,

    /// <summary>A quoted name (<c>"Long name"</c>).</summary>
    Quoted,

    /// <summary>A name in parentheses (<c>(Login)</c>, a use case).</summary>
    Parenthesized,

    /// <summary>A name in brackets (<c>[Web]</c>, a component).</summary>
    Bracketed,

    /// <summary>A name between colons (<c>:User:</c>, an actor).</summary>
    Colon,

    /// <summary>The start or end pseudo-state <c>[*]</c>.</summary>
    Star,

    /// <summary>The shallow history pseudo-state <c>[H]</c> or <c>X[H]</c> (the text is the owner, empty for none).</summary>
    History,

    /// <summary>The deep history pseudo-state <c>[H*]</c> or <c>X[H*]</c> (the text is the owner, empty for none).</summary>
    DeepHistory,
}

/// <summary>
///     A name written in a statement: its text without the quotes or brackets, and how it was written.
/// </summary>
/// <param name="Text">The text.</param>
/// <param name="Kind">How it was written.</param>
internal readonly record struct SvekName(string Text, SvekNameKind Kind);

/// <summary>
///     A <c>hide</c> or <c>show</c> statement: whether it shows, and what it names, in lower case with single spaces.
/// </summary>
/// <param name="Show">True for <c>show</c>, false for <c>hide</c>.</param>
/// <param name="What">What the statement names (for example <c>empty members</c> or <c>circle</c>).</param>
/// <param name="Location">The location of the statement.</param>
internal sealed record SvekHideShow(bool Show, string What, PlantUmlLocation Location);

/// <summary>
///     The statement readers that every diagram PlantUML lays out with Graphviz shares: names, aliases, stereotypes,
///     colors, page texts, the direction, the scale, <c>hide</c> and <c>show</c>, the layout skin parameters, the link
///     arrow grammar (<c>SvekSyntax.Links.cs</c>), and the notes (<c>SvekSyntax.Notes.cs</c>).
/// </summary>
/// <remarks>
///     <para>
///         The readers are hand-written scanners over the preprocessed statements (no regular expressions on the
///         input), linear in the length of a statement except for the bounded backtracking of the link grammar. They
///         follow the statements of PlantUML's class and description diagrams (<c>CommandLinkClass</c>,
///         <c>CommandLinkElement</c>, <c>CommandCreateClass</c>, the note commands, and the common commands). A reader
///         that does not recognize a statement returns false and leaves the index unchanged; a statement it recognizes
///         but cannot read (a malformed color, an unclosed block) is refused with a located
///         <see cref="PlantUmlSyntaxException"/> whose code is <see cref="PlantUmlDiagnosticCodes.SyntaxError"/>.
///     </para>
///     <para>
///         The class is stateless and safe for concurrent use.
///     </para>
/// </remarks>
internal static partial class SvekSyntax
{
    /// <summary>The largest scale factor that is drawn.</summary>
    public const double MaxScale = 100;

    /// <summary>The hint of a statement that is not drawn.</summary>
    public const string UnsupportedHint = "remove the statement, or render the diagram with PlantUML itself";

    /// <summary>
    ///     Decides whether a character is part of the letters of an identifier (a letter, a digit, <c>_</c>, or
    ///     <c>$</c>).
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns>True when it is.</returns>
    public static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '$';

    /// <summary>
    ///     Decides whether a character can join two parts of a qualified identifier (PlantUML's separator: any
    ///     character that is not a letter, a digit, white space, a quote, or one of <c>_ $ # : { } &lt; &gt;</c>).
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns>True when it is.</returns>
    public static bool IsSeparatorChar(char c) =>
        !IsIdentifierChar(c) && !char.IsWhiteSpace(c) && !IsQuote(c) && c is not ('#' or ':' or '{' or '}' or '<' or '>');

    /// <summary>
    ///     Decides whether a character is a quote (the straight double quote, the curly double quotes, and the
    ///     guillemets PlantUML accepts as quotes).
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns>True when it is.</returns>
    public static bool IsQuote(char c) => c is '"' or '\u201C' or '\u201D' or '\u00AB' or '\u00BB';

    /// <summary>
    ///     Gets the ends of the identifiers that start at an index, longest first: the identifier grammar
    ///     <c>sep? letters (sep letters)*</c>, where the separator is one character of <see cref="IsSeparatorChar"/>
    ///     or <c>::</c>, so <c>a.b-c</c> yields the ends after <c>c</c>, <c>b</c>, and <c>a</c>.
    /// </summary>
    /// <remarks>
    ///     The link reader tries the ends in this order, as the regular expression of PlantUML backtracks from its
    ///     greedy match.
    /// </remarks>
    /// <param name="text">The text.</param>
    /// <param name="start">The index.</param>
    /// <returns>The exclusive ends, longest first; empty when no identifier starts at the index.</returns>
    public static IReadOnlyList<int> IdentifierEnds(string text, int start)
    {
        ArgumentNullException.ThrowIfNull(text);
        var ends = new List<int>();
        var i = start;

        // An optional leading separator, then runs of letters joined by separators
        i += SeparatorLength(text, i);
        while (true)
        {
            var runStart = i;
            while (i < text.Length && IsIdentifierChar(text[i]))
            {
                i++;
            }

            if (i == runStart)
            {
                break;
            }

            ends.Add(i);
            var separator = SeparatorLength(text, i);
            if (separator == 0 || i + separator >= text.Length || !IsIdentifierChar(text[i + separator]))
            {
                break;
            }

            i += separator;
        }

        ends.Reverse();
        return ends;
    }

    /// <summary>
    ///     Reads a name at an index: a quoted name, a name in parentheses, brackets, or colons, a pseudo-state
    ///     (<c>[*]</c>, <c>[H]</c>, <c>[H*]</c>, <c>X[H]</c>, <c>X[H*]</c>), or the longest identifier.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The index; advanced past the name when one is read.</param>
    /// <param name="name">Receives the name.</param>
    /// <returns>True when a name is read.</returns>
    public static bool TryReadName(string text, ref int index, out SvekName name)
    {
        ArgumentNullException.ThrowIfNull(text);
        var ends = NameEnds(text, index);
        if (ends.Count == 0)
        {
            name = default;
            return false;
        }

        (name, index) = (ends[0].Name, ends[0].End);
        return true;
    }

    /// <summary>
    ///     Gets the names that start at an index with their ends, longest first (the candidates of a link end).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The index.</param>
    /// <returns>The candidates; empty when no name starts at the index.</returns>
    public static IReadOnlyList<(SvekName Name, int End)> NameEnds(string text, int start)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (start >= text.Length)
        {
            return [];
        }

        var c = text[start];
        if (IsQuote(c))
        {
            var close = IndexOfQuote(text, start + 1);
            return close > start + 1 ? [(new SvekName(text[(start + 1)..close], SvekNameKind.Quoted), close + 1)] : [];
        }

        if (c == '[')
        {
            var close = text.IndexOf(']', start + 1);
            if (close < 0 || close == start + 1)
            {
                return [];
            }

            var inner = text[(start + 1)..close];
            var kind = inner switch
            {
                "*" => SvekNameKind.Star,
                "H" or "h" => SvekNameKind.History,
                "H*" or "h*" => SvekNameKind.DeepHistory,
                _ => SvekNameKind.Bracketed,
            };
            return [(new SvekName(kind == SvekNameKind.Bracketed ? inner : string.Empty, kind), close + 1)];
        }

        if (c is '(' or ':')
        {
            var (closer, kind) = c == '(' ? (')', SvekNameKind.Parenthesized) : (':', SvekNameKind.Colon);
            var close = text.IndexOf(closer, start + 1);
            return close > start + 1 ? [(new SvekName(text[(start + 1)..close], kind), close + 1)] : [];
        }

        // Identifiers, each possibly followed by a history mark
        var result = new List<(SvekName, int)>();
        foreach (var end in IdentifierEnds(text, start))
        {
            var identifier = text[start..end];
            if (string.Compare(text, end, "[H*]", 0, 4, StringComparison.OrdinalIgnoreCase) == 0)
            {
                result.Add((new SvekName(identifier, SvekNameKind.DeepHistory), end + 4));
            }
            else if (string.Compare(text, end, "[H]", 0, 3, StringComparison.OrdinalIgnoreCase) == 0)
            {
                result.Add((new SvekName(identifier, SvekNameKind.History), end + 3));
            }

            result.Add((new SvekName(identifier, SvekNameKind.Plain), end));
        }

        return result;
    }

    /// <summary>
    ///     Reads an alias (<c>as Name</c>) at an index, after optional white space.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The index; advanced past the alias and the white space after it when one is read.</param>
    /// <param name="alias">Receives the alias.</param>
    /// <returns>True when an alias is read.</returns>
    public static bool TryReadAlias(string text, ref int index, out string alias)
    {
        ArgumentNullException.ThrowIfNull(text);
        var i = index;
        SkipSpaces(text, ref i);
        if (!TryKeyword(text, ref i, "as"))
        {
            alias = string.Empty;
            return false;
        }

        SkipSpaces(text, ref i);
        if (!TryReadName(text, ref i, out var name) || name.Kind is not (SvekNameKind.Plain or SvekNameKind.Quoted))
        {
            alias = string.Empty;
            return false;
        }

        alias = name.Text;
        SkipSpaces(text, ref i);
        index = i;
        return true;
    }

    /// <summary>
    ///     Reads the stereotypes at an index (<c>&lt;&lt;a&gt;&gt; &lt;&lt;b&gt;&gt;</c>), with the white space around them.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The index; advanced past the stereotypes read.</param>
    /// <returns>The texts of the stereotypes in order (trimmed, without the brackets); empty when there is none.</returns>
    public static IReadOnlyList<string> ReadStereotypes(string text, ref int index)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = new List<string>();
        var i = index;
        while (true)
        {
            SkipSpaces(text, ref i);
            if (string.CompareOrdinal(text, i, "<<", 0, 2) != 0)
            {
                break;
            }

            var close = text.IndexOf(">>", i + 2, StringComparison.Ordinal);
            if (close < 0)
            {
                break;
            }

            result.Add(text[(i + 2)..close].Trim());
            i = close + 2;
            index = i;
        }

        if (result.Count > 0)
        {
            SkipSpaces(text, ref index);
        }

        return result;
    }

    /// <summary>
    ///     Reads a color written as <c>#name</c>, <c>#RGB</c>, or <c>#RRGGBB</c> at an index (PlantUML's simple color, up
    ///     to white space, a colon, or a bracket).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The index of the hash; advanced past the color and the white space after it.</param>
    /// <param name="location">The location for the error.</param>
    /// <returns>The color, or null when there is no hash at the index.</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the hash is not followed by a color.</exception>
    public static Rgba32? ReadColor(string text, ref int index, PlantUmlLocation location)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (index >= text.Length || text[index] != '#')
        {
            return null;
        }

        var end = index + 1;
        while (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] is not (':' or '[' or ']' or '{' or '<' or ','))
        {
            end++;
        }

        var token = text[index..end];
        if (!SkinParams.TryParseColor(token, out var color))
        {
            throw Error($"'{token}' is not a color", location, "write a color such as #LightBlue or #AABBCC");
        }

        index = end;
        SkipSpaces(text, ref index);
        return color;
    }

    /// <summary>
    ///     Reads a direction statement (<c>left to right direction</c>, <c>top to bottom direction</c>).
    /// </summary>
    /// <param name="text">The statement text.</param>
    /// <param name="direction">Receives the rank direction.</param>
    /// <returns>True when the statement is a direction statement.</returns>
    public static bool TryReadDirection(string text, out SvekRankDir direction)
    {
        ArgumentNullException.ThrowIfNull(text);
        var compact = Compact(text);
        (var known, direction) = compact switch
        {
            "left to right direction" => (true, SvekRankDir.LeftToRight),
            "top to bottom direction" => (true, SvekRankDir.TopToBottom),
            _ => (false, SvekRankDir.TopToBottom),
        };
        return known;
    }

    /// <summary>
    ///     Reads the factor of a <c>scale N</c> or <c>scale N/M</c> statement.
    /// </summary>
    /// <param name="text">The statement text (starting with <c>scale</c>).</param>
    /// <returns>The factor, or null when the form is not drawn (another form, or a factor outside 0 to <see cref="MaxScale"/>).</returns>
    public static double? ReadScale(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var rest = text.Length > 5 ? text[5..].Trim() : string.Empty;
        var slash = rest.IndexOf('/', StringComparison.Ordinal);
        double value;
        bool ok;
        if (slash > 0)
        {
            var numeratorOk = double.TryParse(rest[..slash], NumberStyles.Float, CultureInfo.InvariantCulture, out var numerator);
            var denominatorOk = double.TryParse(rest[(slash + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out var denominator);
            value = numerator / denominator;
            ok = numeratorOk && denominatorOk && double.IsFinite(value);
        }
        else
        {
            ok = double.TryParse(rest, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        return ok && value > 0 && value <= MaxScale ? value : null;
    }

    /// <summary>
    ///     Reads a <c>hide</c> or <c>show</c> statement.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="command">Receives the statement read.</param>
    /// <returns>True when the statement is a <c>hide</c> or <c>show</c> statement with something after the keyword.</returns>
    public static bool TryReadHideShow(SourceStatement statement, out SvekHideShow command)
    {
        ArgumentNullException.ThrowIfNull(statement);
        var text = statement.Text;
        var i = 0;
        var show = TryKeyword(text, ref i, "show");
        if (!show && !TryKeyword(text, ref i, "hide"))
        {
            command = new SvekHideShow(false, string.Empty, statement.Location);
            return false;
        }

        var what = Compact(text[i..]);
        command = new SvekHideShow(show, what, statement.Location);
        return what.Length > 0;
    }

    /// <summary>
    ///     Reads a page text: <c>title</c>, <c>header</c>, <c>footer</c>, <c>caption</c> (on one line or as a block up to
    ///     <c>end title</c>, ...), <c>left|center|right header|footer</c>, or <c>legend [left|right|center]</c> ...
    ///     <c>endlegend</c>.
    /// </summary>
    /// <param name="statements">The statements of the diagram.</param>
    /// <param name="index">The index of the statement; advanced to the last statement read when it is a page text.</param>
    /// <param name="page">The page texts; the text read replaces the one of its kind.</param>
    /// <param name="limits">The limits of the open call (the label length).</param>
    /// <returns>True when the statement is a page text.</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown when a block is not closed, an alignment has no header or footer, or the text is too long.</exception>
    public static bool TryReadPageText(IReadOnlyList<SourceStatement> statements, ref int index, ref PageTexts page, PlantUmlLimits limits)
    {
        ArgumentNullException.ThrowIfNull(statements);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(limits);
        var statement = statements[index];
        var text = statement.Text;
        var word = FirstWord(text);
        if (word is not ("title" or "header" or "footer" or "caption" or "legend" or "left" or "right" or "center"))
        {
            return false;
        }

        // An alignment applies to the header or footer that follows it
        var i = word.Length;
        TextAlign? align = null;
        if (word is "left" or "right" or "center")
        {
            align = ParseAlign(word);
            SkipSpaces(text, ref i);
            if (TryKeyword(text, ref i, "header"))
            {
                word = "header";
            }
            else if (TryKeyword(text, ref i, "footer"))
            {
                word = "footer";
            }
            else
            {
                return false;
            }
        }

        // The text: one line after the keyword (with an optional colon), or a block up to its closer
        string value;
        var rest = text[i..].Trim();
        var last = index;
        if (word == "legend")
        {
            foreach (var alignWord in rest.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                align = alignWord.ToLowerInvariant() is "left" or "right" or "center" ? ParseAlign(alignWord) : align;
            }

            (value, last) = ReadBlock(statements, index, "endlegend", "the legend");
        }
        else if (rest.Length == 0)
        {
            (value, last) = ReadBlock(statements, index, "end" + word, $"the {word}");
        }
        else
        {
            value = rest.StartsWith(':') ? rest[1..].TrimStart() : rest;
        }

        CheckLabel(value, statement.Location, limits);
        page = word switch
        {
            "title" => page with { Title = new PageText(value, TextAlign.Center, statement.Location) },
            "header" => page with { Header = new PageText(value, align ?? TextAlign.Right, statement.Location) },
            "footer" => page with { Footer = new PageText(value, align ?? TextAlign.Center, statement.Location) },
            "caption" => page with { Caption = new PageText(value, TextAlign.Center, statement.Location) },
            _ => page with { Legend = new PageText(value, align ?? TextAlign.Center, statement.Location) },
        };
        index = last;
        return true;
    }

    /// <summary>
    ///     Takes the layout skin parameters (<c>nodesep</c>, <c>ranksep</c>) out of the parameters of a diagram, so that
    ///     the look does not report them as ignored, and reports the layout parameters that are not drawn
    ///     (<c>linetype</c>).
    /// </summary>
    /// <param name="entries">The skin parameters; the layout parameters are removed.</param>
    /// <param name="reports">The pending reports, which receive a warning for a value that cannot be read or is not drawn.</param>
    /// <returns>The space between neighbors and between ranks, each null when not set.</returns>
    public static (double? NodeSep, double? RankSep) TakeLayoutSettings(List<SkinParamEntry> entries, List<PlantUmlPendingReport> reports)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(reports);
        double? nodeSep = null;
        double? rankSep = null;
        static bool IsLayout(SkinParamEntry e) => e.Name.ToLowerInvariant() is "nodesep" or "ranksep" or "linetype";
        var layout = entries.Where(IsLayout).ToList();
        entries.RemoveAll(IsLayout);
        foreach (var entry in layout)
        {
            var name = entry.Name.ToLowerInvariant();
            if (name == "linetype")
            {
                reports.Add(PlantUmlPendingReport.Warning(
                    PlantUmlDiagnosticCodes.SkinParamIgnored,
                    $"skinparam '{entry.Name}' is ignored: links are drawn as splines",
                    entry.Location));
                continue;
            }

            if (!double.TryParse(entry.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !(value >= 0) || value > 10_000)
            {
                reports.Add(PlantUmlPendingReport.Warning(
                    PlantUmlDiagnosticCodes.SkinParamIgnored,
                    $"skinparam '{entry.Name}' is ignored: '{entry.Value}' is not a number of pixels from 0 to 10000",
                    entry.Location));
                continue;
            }

            if (name == "nodesep")
            {
                nodeSep = value;
            }
            else
            {
                rankSep = value;
            }
        }

        return (nodeSep, rankSep);
    }

    /// <summary>
    ///     Refuses a text longer than the label limit.
    /// </summary>
    /// <param name="text">The text, or null.</param>
    /// <param name="location">The location of the statement.</param>
    /// <param name="limits">The limits.</param>
    /// <exception cref="PlantUmlSyntaxException">Thrown, with the code <see cref="PlantUmlDiagnosticCodes.LimitExceeded"/>, when the text is too long.</exception>
    public static void CheckLabel(string? text, PlantUmlLocation location, PlantUmlLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        if (text is not null && text.Length > limits.MaxLabelLength)
        {
            throw new PlantUmlSyntaxException(
                PlantUmlDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"a text is {text.Length} characters long, more than the limit of {limits.MaxLabelLength} (PlantUmlLimits.MaxLabelLength)"),
                location,
                "shorten the text, or raise PlantUmlLimits.MaxLabelLength when the source is trusted");
        }
    }

    /// <summary>
    ///     Creates a located syntax error.
    /// </summary>
    /// <param name="description">The description.</param>
    /// <param name="location">The location.</param>
    /// <param name="hint">The hint.</param>
    /// <returns>The exception, with the code <see cref="PlantUmlDiagnosticCodes.SyntaxError"/>.</returns>
    public static PlantUmlSyntaxException Error(string description, PlantUmlLocation location, string hint) =>
        new(PlantUmlDiagnosticCodes.SyntaxError, description, location, hint);

    /// <summary>
    ///     Creates the pending report of a statement that is read but not drawn.
    /// </summary>
    /// <param name="what">What is not drawn (the message).</param>
    /// <param name="location">The location.</param>
    /// <returns>The report, with the code <see cref="PlantUmlDiagnosticCodes.StatementUnsupported"/>.</returns>
    public static PlantUmlPendingReport Unsupported(string what, PlantUmlLocation location) =>
        PlantUmlPendingReport.Unsupported(PlantUmlDiagnosticCodes.StatementUnsupported, what, location, UnsupportedHint);

    /// <summary>
    ///     Gets the first word of a statement in lower case (up to white space, a colon, a brace, a quote, or a hash).
    /// </summary>
    /// <param name="text">The statement text.</param>
    /// <returns>The word, possibly empty.</returns>
    public static string FirstWord(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var end = 0;
        while (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] is not (':' or '{' or '#') && !IsQuote(text[end]))
        {
            end++;
        }

        return text[..end].ToLowerInvariant();
    }

    /// <summary>
    ///     Skips white space.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The index; advanced past the white space.</param>
    public static void SkipSpaces(string text, ref int index)
    {
        ArgumentNullException.ThrowIfNull(text);
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }
    }

    /// <summary>
    ///     Reads a keyword at an index, without regard to case.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The index; advanced past the keyword when it matches.</param>
    /// <param name="keyword">The keyword.</param>
    /// <returns>True when the keyword is at the index and is not followed by a letter, a digit, or <c>_</c>.</returns>
    public static bool TryKeyword(string text, ref int index, string keyword)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(keyword);
        if (index + keyword.Length > text.Length || string.Compare(text, index, keyword, 0, keyword.Length, StringComparison.OrdinalIgnoreCase) != 0)
        {
            return false;
        }

        var end = index + keyword.Length;
        if (end < text.Length && IsIdentifierChar(text[end]))
        {
            return false;
        }

        index = end;
        return true;
    }

    /// <summary>
    ///     Reads the lines of a text block up to its closing keyword.
    /// </summary>
    /// <param name="statements">The statements of the diagram.</param>
    /// <param name="index">The index of the opening statement.</param>
    /// <param name="closer">The closing keyword (for example <c>end note</c>).</param>
    /// <param name="what">What the block is, for the error.</param>
    /// <returns>The text (lines joined with line feeds) and the index of the closing statement.</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the block is not closed.</exception>
    public static (string Text, int Index) ReadBlock(IReadOnlyList<SourceStatement> statements, int index, string closer, string what)
    {
        ArgumentNullException.ThrowIfNull(statements);
        var lines = new List<string>();
        var i = index + 1;
        while (i < statements.Count && !DiagramDetector.IsCloser(statements[i].Text, closer))
        {
            lines.Add(statements[i].Text);
            i++;
        }

        if (i >= statements.Count)
        {
            throw Error($"{what} is not closed", statements[index].Location, $"close it with '{closer}'");
        }

        return (string.Join('\n', lines), i);
    }

    /// <summary>
    ///     Gets the length of a separator at an index (one separator character or <c>::</c>), or zero.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The index.</param>
    /// <returns>The length.</returns>
    private static int SeparatorLength(string text, int index)
    {
        if (index + 1 < text.Length && text[index] == ':' && text[index + 1] == ':')
        {
            return 2;
        }

        return index < text.Length && IsSeparatorChar(text[index]) ? 1 : 0;
    }

    /// <summary>
    ///     Finds the next quote.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The index to search from.</param>
    /// <returns>The index of the quote, or -1.</returns>
    private static int IndexOfQuote(string text, int start)
    {
        for (var i = start; i < text.Length; i++)
        {
            if (IsQuote(text[i]))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    ///     Lower-cases a text and joins its words with single spaces.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The compact text.</returns>
    private static string Compact(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    /// <summary>
    ///     Reads an alignment word.
    /// </summary>
    /// <param name="word">The word.</param>
    /// <returns>The alignment.</returns>
    private static TextAlign ParseAlign(string word) => word.ToLowerInvariant() switch
    {
        "left" => TextAlign.Left,
        "right" => TextAlign.Right,
        _ => TextAlign.Center,
    };
}
