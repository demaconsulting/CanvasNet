// Portions derived from PlantUML (MIT); see third-party/README.md
using System.Globalization;
using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Styling;
using DemaConsulting.CanvasNet.PlantUml.Syntax;

namespace DemaConsulting.CanvasNet.PlantUml.Svek;

/// <summary>
///     A link arrow as written (<c>&lt;|--</c>, <c>*-[#red,dashed]-&gt;</c>, <c>-up-&gt;</c>, ...): its decorations, its
///     length, its direction hint, and its style.
/// </summary>
/// <param name="TailDecor">The decoration written before the body (at the first end).</param>
/// <param name="HeadDecor">The decoration written after the body (at the second end).</param>
/// <param name="Length">The number of body characters (<c>-</c>, <c>.</c>, <c>=</c>); at least 1.</param>
/// <param name="Direction">The direction hint.</param>
/// <param name="Dotted">Whether the body has a <c>.</c> (a dashed line).</param>
/// <param name="Doubled">Whether the body has a <c>=</c> (a bold line in description diagrams).</param>
/// <param name="Text">The arrow as written.</param>
internal sealed record SvekArrow(
    LinkDecor TailDecor,
    LinkDecor HeadDecor,
    int Length,
    LinkDirection Direction,
    bool Dotted,
    bool Doubled,
    string Text)
{
    /// <summary>
    ///     Gets the line style written in brackets (<c>-[dashed]-&gt;</c>), or null.
    /// </summary>
    public LinkStyle? Style { get; init; }

    /// <summary>
    ///     Gets the line width written in brackets (<c>-[thickness=3]-&gt;</c>), or null.
    /// </summary>
    public double? Thickness { get; init; }

    /// <summary>
    ///     Gets the line color written in brackets (<c>-[#red]-&gt;</c>), or null.
    /// </summary>
    public Rgba32? Color { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the brackets ask for the link not to change the ranks (<c>norank</c>).
    /// </summary>
    public bool NoRank { get; init; }

    /// <summary>
    ///     Gets the middle decoration (<c>0</c>, <c>(0</c>, <c>0)</c>, <c>(0)</c>), or null; read, not drawn.
    /// </summary>
    public string? Middle { get; init; }

    /// <summary>
    ///     Gets the text in brackets as written, before <see cref="SvekSyntax.TryReadLink"/> checks it and applies it to
    ///     <see cref="Style"/>, <see cref="Thickness"/>, <see cref="Color"/>, and <see cref="NoRank"/>; null when there is
    ///     none or once it is applied.
    /// </summary>
    public string? StyleText { get; init; }

    /// <summary>
    ///     Gets the line style of the link: the style in brackets, else dashed for a dotted body, else bold for a doubled
    ///     body when the diagram draws <c>=</c> bold, else solid.
    /// </summary>
    /// <param name="doubledIsBold">Whether the diagram draws a body with <c>=</c> bold (description diagrams do).</param>
    /// <returns>The style.</returns>
    public LinkStyle ResolveStyle(bool doubledIsBold)
    {
        if (Style is { } style)
        {
            return style;
        }

        if (Dotted)
        {
            return LinkStyle.Dashed;
        }

        return doubledIsBold && Doubled ? LinkStyle.Bold : LinkStyle.Solid;
    }
}

/// <summary>
///     A link statement as written: its ends, its arrow, its labels, and what follows it.
/// </summary>
/// <param name="From">The first end.</param>
/// <param name="To">The second end.</param>
/// <param name="Arrow">The arrow.</param>
/// <param name="Location">The location of the statement.</param>
internal sealed record SvekLinkStatement(SvekName From, SvekName To, SvekArrow Arrow, PlantUmlLocation Location)
{
    /// <summary>Gets the quoted label written before the arrow (near the first end), or null.</summary>
    public string? TailLabel { get; init; }

    /// <summary>Gets the quoted label written after the arrow (near the second end), or null.</summary>
    public string? HeadLabel { get; init; }

    /// <summary>Gets the role written before the arrow (<c>/role</c>), or null; read, not drawn.</summary>
    public string? TailRole { get; init; }

    /// <summary>Gets the role written after the arrow, or null; read, not drawn.</summary>
    public string? HeadRole { get; init; }

    /// <summary>Gets the qualifier written before the arrow (<c>[key]</c>), or null; read, not drawn.</summary>
    public string? TailQualifier { get; init; }

    /// <summary>Gets the qualifier written after the arrow, or null; read, not drawn.</summary>
    public string? HeadQualifier { get; init; }

    /// <summary>Gets the label after the colon, without its direction mark, or null.</summary>
    public string? Label { get; init; }

    /// <summary>Gets the direction mark of the label.</summary>
    public LinkLabelArrow LabelArrow { get; init; }

    /// <summary>Gets the line color written after the second end (<c>#red</c>), or null.</summary>
    public Rgba32? Color { get; init; }

    /// <summary>Gets the stereotypes written after the second end.</summary>
    public IReadOnlyList<string> Stereotypes { get; init; } = [];

    /// <summary>Gets the weight written before the first end (<c>@2</c>), or null.</summary>
    public double? Weight { get; init; }

    /// <summary>Gets a value indicating whether a URL (<c>[[...]]</c>) is written after the second end; read, not drawn.</summary>
    public bool HasUrl { get; init; }
}

/// <summary>
///     The link grammar of <see cref="SvekSyntax"/>.
/// </summary>
internal static partial class SvekSyntax
{
    /// <summary>
    ///     The most parts (identifier runs joined by separators) that a link end may have. The ends are tried from the
    ///     longest, so a name of many parts would make the reader try every split of it against every split of the
    ///     other end; no real name comes near this bound, and a statement whose end would need more is not a link.
    /// </summary>
    public const int MaxLinkEndParts = 100;

    /// <summary>
    ///     The decorations written before the body (PlantUML's <c>decors1</c>), longest first, as the alternation of
    ///     PlantUML's regular expression tries them.
    /// </summary>
    private static readonly (string Text, LinkDecor Decor)[] TailDecors =
    [
        ("<||", LinkDecor.Redefines), ("<|:", LinkDecor.DefinedBy), ("<|", LinkDecor.Extends), ("<<", LinkDecor.ArrowTriangle),
        ("<_", LinkDecor.Arrow), ("}o", LinkDecor.CircleCrowfoot), ("|o", LinkDecor.CircleLine), ("||", LinkDecor.DoubleLine),
        ("}|", LinkDecor.LineCrowfoot), ("0)", LinkDecor.CircleConnect), ("<", LinkDecor.Arrow), ("^", LinkDecor.Extends),
        ("*", LinkDecor.Composition), ("o", LinkDecor.Aggregation), ("x", LinkDecor.NotNavigable), ("}", LinkDecor.Crowfoot),
        ("0", LinkDecor.Circle), ("@", LinkDecor.CircleFill), (")", LinkDecor.Parenthesis), ("#", LinkDecor.Square),
        ("+", LinkDecor.Plus),
    ];

    /// <summary>
    ///     The decorations written after the body (PlantUML's <c>decors2</c>), longest first.
    /// </summary>
    private static readonly (string Text, LinkDecor Decor)[] HeadDecors =
    [
        ("||>", LinkDecor.Redefines), (":|>", LinkDecor.DefinedBy), ("|>", LinkDecor.Extends), (">>", LinkDecor.ArrowTriangle),
        ("_>", LinkDecor.Arrow), ("o{", LinkDecor.CircleCrowfoot), ("o|", LinkDecor.CircleLine), ("||", LinkDecor.DoubleLine),
        ("|{", LinkDecor.LineCrowfoot), ("(0", LinkDecor.CircleConnect), ("\\\\", LinkDecor.HalfArrowUp), ("//", LinkDecor.HalfArrowDown),
        (">", LinkDecor.Arrow), ("^", LinkDecor.Extends), ("*", LinkDecor.Composition), ("o", LinkDecor.Aggregation),
        ("x", LinkDecor.NotNavigable), ("{", LinkDecor.Crowfoot), ("0", LinkDecor.Circle), ("@", LinkDecor.CircleFill),
        ("(", LinkDecor.Parenthesis), ("#", LinkDecor.Square), ("+", LinkDecor.Plus),
    ];

    /// <summary>
    ///     The direction words of an arrow, in the order of PlantUML's alternation
    ///     (<c>left|right|up|down|le?|ri?|up?|do?</c>).
    /// </summary>
    private static readonly (string Text, LinkDirection Direction)[] DirectionWords =
    [
        ("left", LinkDirection.Left), ("right", LinkDirection.Right), ("up", LinkDirection.Up), ("down", LinkDirection.Down),
        ("le", LinkDirection.Left), ("l", LinkDirection.Left), ("ri", LinkDirection.Right), ("r", LinkDirection.Right),
        ("u", LinkDirection.Up), ("do", LinkDirection.Down), ("d", LinkDirection.Down),
    ];

    /// <summary>The middle decorations, longest first.</summary>
    private static readonly string[] Middles = ["(0)", "(0", "0)", "0"];

    /// <summary>
    ///     Reads an arrow at an index (the arrow grammar of PlantUML's class and description links): an optional
    ///     decoration, a body of <c>-</c>, <c>.</c>, and <c>=</c> with an optional style in brackets, direction word, and
    ///     middle decoration, and an optional decoration.
    /// </summary>
    /// <remarks>
    ///     The first arrow found is returned: the longest decorations first, as PlantUML's regular expression tries
    ///     them. The style in brackets is not checked here; <see cref="TryReadLink"/> checks it once the whole statement
    ///     matches. A direction (<c>left</c>, <c>right</c>) keeps both ends in one rank whatever the length, as
    ///     PlantUML does; <c>up</c> and <c>down</c> are at least two long.
    /// </remarks>
    /// <param name="text">The text.</param>
    /// <param name="start">The index of the arrow.</param>
    /// <param name="arrow">Receives the arrow.</param>
    /// <param name="end">Receives the index after the arrow.</param>
    /// <returns>True when an arrow starts at the index.</returns>
    public static bool TryReadArrow(string text, int start, out SvekArrow arrow, out int end)
    {
        ArgumentNullException.ThrowIfNull(text);
        var candidate = ArrowCandidates(text, start).FirstOrDefault();
        if (candidate.Arrow is null)
        {
            (arrow, end) = (null!, start);
            return false;
        }

        (arrow, end) = (candidate.Arrow, candidate.End);
        return true;
    }

    /// <summary>
    ///     Reads a link statement: <c>[@weight] end1 ["label"] [/role] arrow ["label"] [/role] end2 [#color] [[url]]
    ///     [&lt;&lt;stereotype&gt;&gt;] [: label]</c>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The ends are names (<see cref="NameEnds"/>); the arrow is <see cref="TryReadArrow"/>'s. Ends and
    ///         decorations are tried from the longest, so <c>A-B</c> is a link from <c>A</c> to <c>B</c> although
    ///         <c>A-B</c> is also an identifier, as PlantUML's regular expression backtracks.
    ///     </para>
    ///     <para>
    ///         The label after the colon is split as PlantUML splits it: quoted end labels around it
    ///         (<c>: "1" uses "*"</c>) when the arrow has none, and a direction mark (<c>&gt;</c> at its end or start,
    ///         <c>&lt;</c> likewise).
    ///     </para>
    /// </remarks>
    /// <param name="statement">The statement.</param>
    /// <param name="link">Receives the link.</param>
    /// <returns>True when the statement is a link statement.</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the statement is a link whose style in brackets or color cannot be read.</exception>
    public static bool TryReadLink(SourceStatement statement, out SvekLinkStatement link)
    {
        ArgumentNullException.ThrowIfNull(statement);
        var text = statement.Text;

        // An optional weight: @2.5 followed by white space
        var i = 0;
        double? weight = null;
        if (text.StartsWith('@'))
        {
            var j = 1;
            while (j < text.Length && (char.IsAsciiDigit(text[j]) || text[j] == '.'))
            {
                j++;
            }

            if (j > 1 && j < text.Length && char.IsWhiteSpace(text[j])
                && double.TryParse(text.AsSpan(1, j - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var w) && w > 0)
            {
                weight = w;
                i = j;
                SkipSpaces(text, ref i);
            }
        }

        foreach (var from in NameEndsCore(text, i, MaxLinkEndParts))
        {
            if (TryReadLinkAfterFirstEnd(statement, from, out var found))
            {
                link = found with { Weight = weight };
                return true;
            }
        }

        link = null!;
        return false;
    }

    /// <summary>
    ///     Splits the label of a link as PlantUML does: quoted end labels around the text and a direction mark.
    /// </summary>
    /// <param name="raw">The text after the colon.</param>
    /// <param name="splitEndLabels">Whether quoted end labels are taken out (when the arrow has none of its own).</param>
    /// <returns>The label (null when it is only a mark), its mark, and the end labels taken out.</returns>
    public static (string? Label, LinkLabelArrow Arrow, string? TailLabel, string? HeadLabel) SplitLabel(string raw, bool splitEndLabels)
    {
        ArgumentNullException.ThrowIfNull(raw);
        var label = raw.Trim();
        string? tail = null;
        string? head = null;

        // "1" text "2", "1" text, and text "2"
        if (splitEndLabels && label.Length > 0)
        {
            var quotes = new List<int>();
            for (var k = 0; k < label.Length; k++)
            {
                if (label[k] == '"')
                {
                    quotes.Add(k);
                }
            }

            if (quotes.Count == 4 && quotes[0] == 0 && quotes[3] == label.Length - 1 && quotes[1] > 1 && quotes[2] > quotes[1] + 1 && quotes[3] > quotes[2] + 1)
            {
                tail = label[1..quotes[1]];
                head = label[(quotes[2] + 1)..quotes[3]];
                label = Unquote(label[(quotes[1] + 1)..quotes[2]].Trim()).Trim();
            }
            else if (quotes.Count == 2 && quotes[0] == 0 && quotes[1] > 1 && quotes[1] < label.Length - 1)
            {
                tail = label[1..quotes[1]];
                label = Unquote(label[(quotes[1] + 1)..].Trim()).Trim();
            }
            else if (quotes.Count == 2 && quotes[1] == label.Length - 1 && quotes[0] > 0 && quotes[1] > quotes[0] + 1)
            {
                head = label[(quotes[0] + 1)..quotes[1]];
                label = Unquote(label[..quotes[0]].Trim()).Trim();
            }
        }

        // The direction mark (PlantUML's StringWithArrow)
        label = Unquote(label);
        var arrow = LinkLabelArrow.None;
        if (label == "<" || label == ">")
        {
            return (null, label == ">" ? LinkLabelArrow.Forward : LinkLabelArrow.Backward, tail, head);
        }

        if (label.StartsWith("< ", StringComparison.Ordinal) || label.StartsWith("> ", StringComparison.Ordinal))
        {
            arrow = label[0] == '>' ? LinkLabelArrow.Forward : LinkLabelArrow.Backward;
            label = label[2..].Trim();
        }
        else if (label.EndsWith(" >", StringComparison.Ordinal) || label.EndsWith(" <", StringComparison.Ordinal))
        {
            arrow = label[^1] == '>' ? LinkLabelArrow.Forward : LinkLabelArrow.Backward;
            label = label[..^2].Trim();
        }

        return (label.Length == 0 ? null : Guillemets(label), arrow, tail, head);
    }

    /// <summary>
    ///     Writes the stereotype-like parts of a link label with guillemets, as PlantUML's <c>LinkArg</c> does
    ///     (<c>Guillemet.manageGuillemet</c>): <c>&lt;&lt;include&gt;&gt;</c> becomes <c>«include»</c>. A part is
    ///     <c>&lt;&lt;</c>, at most one space, a text without angle brackets (a sprite <c>&lt;&amp;name&gt;</c> may
    ///     appear), at most one space, and <c>&gt;&gt;</c>, on one line of the label.
    /// </summary>
    /// <param name="text">The label.</param>
    /// <returns>The label with guillemets.</returns>
    public static string Guillemets(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!text.Contains("<<", StringComparison.Ordinal))
        {
            return text;
        }

        var result = new StringBuilder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            if (string.CompareOrdinal(text, i, "<<", 0, 2) == 0 && TryReadGuillemet(text, i + 2, out var inner, out var end))
            {
                result.Append('\u00AB').Append(inner).Append('\u00BB');
                i = end;
                continue;
            }

            result.Append(text[i]);
            i++;
        }

        return result.ToString();
    }

    /// <summary>
    ///     Reads the inside of a guillemet part after its <c>&lt;&lt;</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The index after <c>&lt;&lt;</c>.</param>
    /// <param name="inner">Receives the inside, without its surrounding space.</param>
    /// <param name="end">Receives the index after the closing <c>&gt;&gt;</c>.</param>
    /// <returns>True when a part ends there.</returns>
    private static bool TryReadGuillemet(string text, int start, out string inner, out int end)
    {
        (inner, end) = (string.Empty, start);
        var j = start;
        while (j < text.Length && text[j] != '>' && !(text[j] == '\\' && j + 1 < text.Length && text[j + 1] == 'n'))
        {
            if (text[j] == '<')
            {
                // A sprite <&name> is the only bracket allowed inside
                var k = j + 2;
                while (k < text.Length && (char.IsLetterOrDigit(text[k]) || text[k] == '_'))
                {
                    k++;
                }

                if (j + 1 >= text.Length || text[j + 1] != '&' || k == j + 2 || k >= text.Length || text[k] != '>')
                {
                    return false;
                }

                j = k + 1;
                continue;
            }

            j++;
        }

        if (string.CompareOrdinal(text, j, ">>", 0, 2) != 0)
        {
            return false;
        }

        var (from, to) = (start, j);
        if (to - from > 1 && char.IsWhiteSpace(text[from]))
        {
            from++;
        }

        if (to - from > 1 && char.IsWhiteSpace(text[to - 1]))
        {
            to--;
        }

        if (to == from)
        {
            return false;
        }

        (inner, end) = (text[from..to], j + 2);
        return true;
    }

    /// <summary>
    ///     Reads the rest of a link statement after a candidate first end.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="from">The candidate first end.</param>
    /// <param name="link">Receives the link.</param>
    /// <returns>True when the rest is a link.</returns>
    private static bool TryReadLinkAfterFirstEnd(SourceStatement statement, SvekNameCandidate from, out SvekLinkStatement link)
    {
        var text = statement.Text;
        link = null!;

        // Before the arrow: a qualifier, a quoted label, and a role
        var index = from.End;
        var i = index;
        SkipSpaces(text, ref i);
        var tailQualifier = ReadQualifier(text, index, ref i);
        var tailLabel = ReadQuotedLabel(text, ref i);
        var tailRole = ReadRole(text, ref i);
        SkipSpaces(text, ref i);

        foreach (var (arrow, arrowEnd) in ArrowCandidates(text, i))
        {
            // After the arrow: a quoted label, a role, a qualifier, and the second end
            var j = arrowEnd;
            SkipSpaces(text, ref j);
            var headLabel = ReadQuotedLabel(text, ref j);
            var headRole = ReadRole(text, ref j);
            var beforeQualifier = j;
            var qualifier = ReadQualifier(text, arrowEnd, ref j);
            SkipSpaces(text, ref j);

            // A name in brackets after the arrow is a qualifier only when a second end follows it ([Web] --> [Db])
            foreach (var qualified in qualifier is null ? [false] : new[] { true, false })
            {
                var headQualifier = qualified ? qualifier : null;
                foreach (var to in NameEndsCore(text, qualified ? j : beforeQualifier, MaxLinkEndParts))
                {
                    if (!TryReadLinkTail(text, to.End, out var tail))
                    {
                        continue;
                    }

                    // The whole statement matches: check the style and the color, then split the label
                    var checkedArrow = CheckStyle(arrow, statement, i);
                    var color = tail.ColorStart < 0 ? null : ReadColorAt(text, tail.ColorStart, statement);
                    var (label, labelArrow, splitTail, splitHead) = tail.Label is null
                        ? (null, LinkLabelArrow.None, null, null)
                        : SplitLabel(tail.Label, tailLabel is null && headLabel is null);
                    link = new SvekLinkStatement(from.Name, to.Name, checkedArrow, statement.Location)
                    {
                        TailLabel = tailLabel ?? splitTail,
                        HeadLabel = headLabel ?? splitHead,
                        TailRole = tailRole,
                        HeadRole = headRole,
                        TailQualifier = tailQualifier,
                        HeadQualifier = headQualifier,
                        Label = label,
                        LabelArrow = labelArrow,
                        Color = color,
                        Stereotypes = tail.Stereotypes,
                        HasUrl = tail.HasUrl,
                    };
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     The part of a link statement after its second end.
    /// </summary>
    /// <param name="ColorStart">The index of the color's hash, or -1.</param>
    /// <param name="HasUrl">Whether a URL is written.</param>
    /// <param name="Stereotypes">The stereotypes.</param>
    /// <param name="Label">The text after the colon, or null.</param>
    private readonly record struct LinkTail(int ColorStart, bool HasUrl, IReadOnlyList<string> Stereotypes, string? Label);

    /// <summary>
    ///     Reads what may follow the second end: a color, a URL, stereotypes, and a label after a colon, up to the end of
    ///     the statement.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The index after the second end.</param>
    /// <param name="tail">Receives what was read.</param>
    /// <returns>True when nothing else follows.</returns>
    private static bool TryReadLinkTail(string text, int index, out LinkTail tail)
    {
        var i = index;
        SkipSpaces(text, ref i);

        // A color: #name up to white space or a colon
        var colorStart = -1;
        if (i < text.Length && text[i] == '#')
        {
            colorStart = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] != ':')
            {
                i++;
            }

            SkipSpaces(text, ref i);
        }

        // A URL in double brackets
        var hasUrl = false;
        if (string.CompareOrdinal(text, i, "[[", 0, 2) == 0)
        {
            var close = text.IndexOf("]]", i + 2, StringComparison.Ordinal);
            if (close < 0)
            {
                tail = default;
                return false;
            }

            hasUrl = true;
            i = close + 2;
            SkipSpaces(text, ref i);
        }

        var stereotypes = ReadStereotypes(text, ref i);
        string? label = null;
        if (i < text.Length && text[i] == ':')
        {
            label = text[(i + 1)..];
            i = text.Length;
        }

        tail = new LinkTail(colorStart, hasUrl, stereotypes, label);
        return i == text.Length;
    }

    /// <summary>
    ///     Gets the arrows that start at an index, in the order PlantUML's regular expression finds them.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The index.</param>
    /// <returns>The arrows with the index after each.</returns>
    private static IEnumerable<(SvekArrow Arrow, int End)> ArrowCandidates(string text, int start)
    {
        foreach (var (tailText, tailDecor) in DecorCandidates(text, start, TailDecors))
        {
            // The first body: at least one character
            var bodyStart = start + tailText.Length;
            var i = bodyStart;
            while (i < text.Length && IsBodyChar(text[i]))
            {
                i++;
            }

            if (i == bodyStart)
            {
                continue;
            }

            var body1 = text[bodyStart..i];

            // An optional style, a direction word, a middle decoration, and a second style
            var style1 = ReadBracket(text, ref i);
            foreach (var (directionText, direction) in DirectionCandidates(text, i))
            {
                var j = i + directionText.Length;
                var middle = ReadMiddle(text, ref j);
                var style2 = ReadBracket(text, ref j);
                var body2Start = j;
                while (j < text.Length && IsBodyChar(text[j]))
                {
                    j++;
                }

                var body2 = text[body2Start..j];
                foreach (var (headText, headDecor) in DecorCandidates(text, j, HeadDecors))
                {
                    var end = j + headText.Length;
                    var body = body1 + body2;
                    yield return (CreateArrow(tailDecor, headDecor, body, direction, text[start..end], style1 ?? style2, middle), end);
                }
            }
        }
    }

    /// <summary>
    ///     Creates an arrow from its parts.
    /// </summary>
    /// <param name="tail">The first decoration.</param>
    /// <param name="head">The second decoration.</param>
    /// <param name="body">The body characters.</param>
    /// <param name="direction">The direction hint.</param>
    /// <param name="text">The arrow as written.</param>
    /// <param name="style">The text in brackets, or null.</param>
    /// <param name="middle">The middle decoration, or null.</param>
    /// <returns>The arrow, with its style text kept unchecked in <see cref="SvekArrow.Text"/>.</returns>
    private static SvekArrow CreateArrow(LinkDecor tail, LinkDecor head, string body, LinkDirection direction, string text, string? style, string? middle)
    {
        // PlantUML's queue: a left or right hint is one long; up and down are at least two long
        var length = direction is LinkDirection.Left or LinkDirection.Right ? 1 : body.Length;
        if (direction is LinkDirection.Up or LinkDirection.Down && length == 1)
        {
            length = 2;
        }

        return new SvekArrow(tail, head, length, direction, body.Contains('.'), body.Contains('='), text)
        {
            Middle = middle,
            StyleText = style,
        };
    }

    /// <summary>
    ///     Checks the style in brackets of a matched arrow and applies it.
    /// </summary>
    /// <param name="arrow">The arrow.</param>
    /// <param name="statement">The statement.</param>
    /// <param name="arrowStart">The index of the arrow in the statement.</param>
    /// <returns>The arrow with its style.</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown when an item in the brackets is not a style.</exception>
    private static SvekArrow CheckStyle(SvekArrow arrow, SourceStatement statement, int arrowStart)
    {
        if (arrow.StyleText is not { } styleText)
        {
            return arrow;
        }

        var location = statement.Locate(arrowStart);
        var result = arrow;
        foreach (var raw in styleText.Split(','))
        {
            var item = raw.Trim();
            var lower = item.ToLowerInvariant();
            if (item.StartsWith('#'))
            {
                var k = 0;
                result = result with { Color = ReadColor(item, ref k, location) };
            }
            else if (lower.StartsWith("thickness=", StringComparison.Ordinal)
                && double.TryParse(lower.AsSpan(10), NumberStyles.Float, CultureInfo.InvariantCulture, out var thickness)
                && thickness >= 0 && thickness <= 100)
            {
                result = result with { Thickness = thickness };
            }
            else
            {
                result = lower switch
                {
                    "dashed" => result with { Style = LinkStyle.Dashed },
                    "dotted" => result with { Style = LinkStyle.Dotted },
                    "bold" => result with { Style = LinkStyle.Bold },
                    "hidden" => result with { Style = LinkStyle.Hidden },
                    "plain" => result with { Style = LinkStyle.Solid },
                    "norank" => result with { NoRank = true },
                    "single" or "node" or "nonode" => result,
                    _ => throw Error(
                        $"'{item}' is not a link style",
                        location,
                        "write a color (#red), dashed, dotted, bold, hidden, plain, norank, or thickness=N, separated by commas"),
                };
            }
        }

        return result with { StyleText = null };
    }

    /// <summary>
    ///     Reads the color after the second end of a matched link.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The index of the hash.</param>
    /// <param name="statement">The statement.</param>
    /// <returns>The color.</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the text after the hash is not a color.</exception>
    private static Rgba32? ReadColorAt(string text, int start, SourceStatement statement)
    {
        var end = start;
        while (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] != ':')
        {
            end++;
        }

        var token = text[start..end];
        return SkinParams.TryParseColor(token, out var color)
            ? color
            : throw Error($"'{token}' is not a color", statement.Locate(start), "write a color such as #LightBlue or #AABBCC");
    }

    /// <summary>
    ///     Gets the decorations that match at an index, longest first, then no decoration.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The index.</param>
    /// <param name="table">The decorations.</param>
    /// <returns>The matching decorations with their text.</returns>
    private static IEnumerable<(string Text, LinkDecor Decor)> DecorCandidates(string text, int index, (string Text, LinkDecor Decor)[] table)
    {
        foreach (var entry in table.Where(e => string.CompareOrdinal(text, index, e.Text, 0, e.Text.Length) == 0 && HasWordBoundaries(text, index, e.Text)))
        {
            yield return entry;
        }

        yield return (string.Empty, LinkDecor.None);
    }

    /// <summary>
    ///     Checks PlantUML's word boundaries around a decoration that starts or ends with <c>o</c> (so the <c>o</c> of a
    ///     name is not taken for an aggregation).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The index of the decoration.</param>
    /// <param name="decor">The decoration text.</param>
    /// <returns>True when the boundaries hold.</returns>
    private static bool HasWordBoundaries(string text, int index, string decor)
    {
        static bool IsWord(char c) => char.IsLetterOrDigit(c) || c == '_';
        if (decor.StartsWith('o') && index > 0 && IsWord(text[index - 1]))
        {
            return false;
        }

        var after = index + decor.Length;
        return !decor.EndsWith('o') || after >= text.Length || !IsWord(text[after]);
    }

    /// <summary>
    ///     Gets the direction words that match at an index, in PlantUML's order, then no word.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The index.</param>
    /// <returns>The matching words.</returns>
    private static IEnumerable<(string Text, LinkDirection Direction)> DirectionCandidates(string text, int index)
    {
        foreach (var entry in DirectionWords.Where(e => string.Compare(text, index, e.Text, 0, e.Text.Length, StringComparison.OrdinalIgnoreCase) == 0))
        {
            yield return (text.Substring(index, entry.Text.Length), entry.Direction);
        }

        yield return (string.Empty, LinkDirection.None);
    }

    /// <summary>
    ///     Reads a middle decoration that is followed by a body character.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The index; advanced past the decoration when one is read.</param>
    /// <returns>The decoration, or null.</returns>
    private static string? ReadMiddle(string text, ref int index)
    {
        foreach (var middle in Middles)
        {
            var end = index + middle.Length;
            if (string.CompareOrdinal(text, index, middle, 0, middle.Length) == 0 && end < text.Length && (IsBodyChar(text[end]) || text[end] == '~'))
            {
                index = end;
                return middle;
            }
        }

        return null;
    }

    /// <summary>
    ///     Reads a style in brackets (<c>[#red,dashed]</c>), unchecked.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The index; advanced past the brackets when they are read.</param>
    /// <returns>The text in the brackets, or null.</returns>
    private static string? ReadBracket(string text, ref int index)
    {
        if (index >= text.Length || text[index] != '[' || (index + 1 < text.Length && text[index + 1] == '['))
        {
            return null;
        }

        var close = text.IndexOf(']', index + 1);
        if (close <= index + 1)
        {
            return null;
        }

        var inner = text[(index + 1)..close];
        if (inner.Contains('[', StringComparison.Ordinal) || inner.Any(char.IsWhiteSpace))
        {
            return null;
        }

        index = close + 1;
        return inner;
    }

    /// <summary>
    ///     Reads a qualifier in brackets, which must have white space before and after it.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="before">The index where the white space before it starts.</param>
    /// <param name="index">The index after that white space; advanced past the qualifier and its white space.</param>
    /// <returns>The qualifier, or null.</returns>
    private static string? ReadQualifier(string text, int before, ref int index)
    {
        if (index == before || index >= text.Length || text[index] != '[')
        {
            return null;
        }

        var close = text.IndexOf(']', index + 1);
        if (close <= index + 1 || close + 1 >= text.Length || !char.IsWhiteSpace(text[close + 1]) || text[(index + 1)..close].Contains('['))
        {
            return null;
        }

        // [*], [H], and [H*] are the pseudo-states of a state diagram, not qualifiers
        var qualifier = text[(index + 1)..close];
        if (qualifier.ToUpperInvariant() is "*" or "H" or "H*")
        {
            return null;
        }

        index = close + 1;
        SkipSpaces(text, ref index);
        return qualifier;
    }

    /// <summary>
    ///     Reads a quoted end label.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The index; advanced past the label and the white space after it when one is read.</param>
    /// <returns>The label, or null.</returns>
    private static string? ReadQuotedLabel(string text, ref int index)
    {
        if (index >= text.Length || !IsQuote(text[index]))
        {
            return null;
        }

        var close = IndexOfQuote(text, index + 1);
        if (close <= index + 1)
        {
            return null;
        }

        var label = text[(index + 1)..close];
        index = close + 1;
        SkipSpaces(text, ref index);
        return label;
    }

    /// <summary>
    ///     Reads a role (<c>/role</c> or <c>/"role"</c>).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The index; advanced past the role and the white space after it when one is read.</param>
    /// <returns>The role, or null.</returns>
    private static string? ReadRole(string text, ref int index)
    {
        if (index + 1 >= text.Length || text[index] != '/' || text[index + 1] == '/')
        {
            return null;
        }

        var i = index + 1;
        string role;
        if (IsQuote(text[i]))
        {
            var close = IndexOfQuote(text, i + 1);
            if (close <= i + 1)
            {
                return null;
            }

            role = text[(i + 1)..close];
            i = close + 1;
        }
        else
        {
            var start = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i]))
            {
                i++;
            }

            role = text[start..i];
        }

        index = i;
        SkipSpaces(text, ref index);
        return role;
    }

    /// <summary>
    ///     Decides whether a character is part of an arrow body.
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns>True for <c>-</c>, <c>.</c>, and <c>=</c>.</returns>
    private static bool IsBodyChar(char c) => c is '-' or '.' or '=';

    /// <summary>
    ///     Removes the double quotes around a text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text without them.</returns>
    private static string Unquote(string text) =>
        text.Length >= 2 && text[0] == '"' && text[^1] == '"' ? text[1..^1] : text;
}
