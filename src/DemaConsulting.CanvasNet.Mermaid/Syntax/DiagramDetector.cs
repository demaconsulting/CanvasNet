// Portions derived from mermaid (MIT); see third-party/README.md
namespace DemaConsulting.CanvasNet.Mermaid.Syntax;

/// <summary>
///     How far this version of the package supports a Mermaid diagram type.
/// </summary>
internal enum DiagramSupport
{
    /// <summary>The type is drawn.</summary>
    Supported,

    /// <summary>The type is not drawn yet but is planned for a later version.</summary>
    Planned,

    /// <summary>The type is not drawn and not planned.</summary>
    NotPlanned,
}

/// <summary>
///     The configuration values Mermaid's detectors read: the <c>defaultRenderer</c> of flowcharts, class diagrams and
///     state diagrams.
/// </summary>
/// <param name="FlowchartRenderer">The <c>flowchart.defaultRenderer</c>.</param>
/// <param name="ClassRenderer">The <c>class.defaultRenderer</c>.</param>
/// <param name="StateRenderer">The <c>state.defaultRenderer</c>.</param>
internal sealed record DetectorSettings(string? FlowchartRenderer, string? ClassRenderer, string? StateRenderer)
{
    /// <summary>
    ///     Gets the settings of Mermaid's default configuration (<c>dagre-wrapper</c> for all three).
    /// </summary>
    public static DetectorSettings Default { get; } = new("dagre-wrapper", "dagre-wrapper", "dagre-wrapper");
}

/// <summary>
///     The diagram type detected at the start of a diagram.
/// </summary>
/// <param name="MermaidId">Mermaid's own detector id (for example <c>flowchart-v2</c> or <c>sequence</c>).</param>
/// <param name="DiagramType">The public type name (<see cref="MermaidDocument.DiagramType"/>): <c>flowchart</c>, <c>sequence</c>, <c>class</c>, <c>state</c>, ...</param>
/// <param name="Keyword">The first word of the diagram as written (for example <c>graph</c> or <c>sequenceDiagram</c>).</param>
/// <param name="Support">How far this version supports the type.</param>
/// <param name="Location">The location of the keyword in the original text.</param>
internal sealed record DetectedDiagram(string MermaidId, string DiagramType, string Keyword, DiagramSupport Support, MermaidLocation Location)
{
    /// <summary>
    ///     Gets a value indicating whether this version draws the type.
    /// </summary>
    public bool IsSupported => Support == DiagramSupport.Supported;
}

/// <summary>
///     Decides which Mermaid diagram type a text is, with Mermaid's <c>detectType</c>: its clean-up of the text and its
///     detectors, tried in Mermaid's registration order, the first that matches winning.
/// </summary>
/// <remarks>
///     <para>
///         <b>Fidelity.</b> Every diagram type that Mermaid registers is known here, so a text Mermaid draws is never an
///         unknown diagram: the order is that of <c>addDiagrams</c> (<c>error</c>, <c>---</c>, then the large features
///         <c>flowchart-elk</c>, <c>mindmap</c> and <c>architecture</c>, then the rest), and each detector is a port of its
///         regular expression, including their quirks (the <c>c4</c> detector matches <c>C4Container</c> anywhere in the
///         text; <c>graph</c> matches <c>graphs</c>). The <c>defaultRenderer</c> settings choose between the variants of the
///         flowchart, class and state detectors as in Mermaid.
///     </para>
///     <para>
///         <b>Support.</b> Each type has a <see cref="DiagramSupport"/>: <c>flowchart</c> (written <c>flowchart</c> or
///         <c>graph</c>), <c>sequenceDiagram</c>, <c>classDiagram</c> (written <c>classDiagram</c> or
///         <c>classDiagram-v2</c>, under Mermaid's <c>classDiagram</c> id or its legacy <c>class</c> id) and
///         <c>stateDiagram</c> (written <c>stateDiagram</c> or <c>stateDiagram-v2</c>, under Mermaid's
///         <c>stateDiagram</c> id or its legacy <c>state</c> id) and <c>erDiagram</c> are drawn; the types of the
///         following phases are planned; the rest is not planned. A type that is not drawn is reported by the document as <see cref="MermaidDiagnosticCodes.DiagramUnsupported"/> with
///         <see cref="UnsupportedHint(DetectedDiagram)"/>.
///     </para>
///     <para>
///         <b>Errors.</b> Text that no detector matches is a <see cref="MermaidSyntaxException"/> with
///         <see cref="MermaidDiagnosticCodes.UnknownDiagram"/> whose hint names the nearest known keyword; text that starts
///         with <c>---</c> (a frontmatter block that was not closed) is Mermaid's own syntax error.
///     </para>
///     <para>
///         The class is stateless and safe for concurrent use.
///     </para>
/// </remarks>
internal static class DiagramDetector
{
    /// <summary>The keywords of the supported diagram types, for hints.</summary>
    private static readonly string[] SupportedKeywords = ["flowchart", "graph", "sequenceDiagram", "classDiagram", "stateDiagram", "erDiagram"];

    /// <summary>
    ///     The known diagram types in Mermaid's detection order: the detector id, the public type name, the support and
    ///     the detector.
    /// </summary>
    private static readonly KnownDiagram[] Known =
    [
        new("error", "error", DiagramSupport.NotPlanned, (text, _) => Preprocessor.TrimJs(text).ToLowerInvariant() == "error"),
        new("---", "---", DiagramSupport.NotPlanned, (text, _) => TrimStartJs(text).StartsWith("---", StringComparison.Ordinal)),
        new("flowchart-elk", "flowchart", DiagramSupport.Supported, (text, settings) =>
            StartsWithKeyword(text, "flowchart-elk") ||
            ((StartsWithKeyword(text, "flowchart") || StartsWithKeyword(text, "graph")) && settings.FlowchartRenderer == "elk")),
        new("mindmap", "mindmap", DiagramSupport.Planned, (text, _) => StartsWithKeyword(text, "mindmap")),
        new("architecture", "architecture", DiagramSupport.NotPlanned, (text, _) => StartsWithKeyword(text, "architecture")),
        new("c4", "c4", DiagramSupport.NotPlanned, (text, _) =>
            StartsWithKeyword(text, "C4Context") ||
            text.Contains("C4Container", StringComparison.Ordinal) ||
            text.Contains("C4Component", StringComparison.Ordinal) ||
            text.Contains("C4Dynamic", StringComparison.Ordinal) ||
            text.Contains("C4Deployment", StringComparison.Ordinal)),
        new("kanban", "kanban", DiagramSupport.NotPlanned, (text, _) => StartsWithKeyword(text, "kanban")),
        new("classDiagram", "class", DiagramSupport.Supported, (text, settings) =>
            (StartsWithKeyword(text, "classDiagram") && settings.ClassRenderer == "dagre-wrapper") || StartsWithKeyword(text, "classDiagram-v2")),
        new("class", "class", DiagramSupport.Supported, (text, settings) =>
            settings.ClassRenderer != "dagre-wrapper" && StartsWithKeyword(text, "classDiagram")),
        new("er", "er", DiagramSupport.Supported, (text, _) => StartsWithKeyword(text, "erDiagram")),
        new("gantt", "gantt", DiagramSupport.Planned, (text, _) => StartsWithKeyword(text, "gantt")),
        new("info", "info", DiagramSupport.NotPlanned, (text, _) => StartsWithKeyword(text, "info")),
        new("pie", "pie", DiagramSupport.Planned, (text, _) => StartsWithKeyword(text, "pie")),
        new("requirement", "requirement", DiagramSupport.NotPlanned, (text, _) => StartsWithKeyword(text, "requirement")),
        new("sequence", "sequence", DiagramSupport.Supported, (text, _) => StartsWithKeyword(text, "sequenceDiagram")),
        new("swimlane", "swimlane", DiagramSupport.NotPlanned, (text, _) => StartsWithWord(text, "swimlane-beta", false)),
        new("flowchart-v2", "flowchart", DiagramSupport.Supported, (text, settings) =>
            settings.FlowchartRenderer != "dagre-d3" &&
            ((StartsWithKeyword(text, "graph") && settings.FlowchartRenderer == "dagre-wrapper") || StartsWithKeyword(text, "flowchart"))),
        new("flowchart", "flowchart", DiagramSupport.Supported, (text, settings) =>
            settings.FlowchartRenderer is not "dagre-wrapper" and not "elk" && StartsWithKeyword(text, "graph")),
        new("timeline", "timeline", DiagramSupport.NotPlanned, (text, _) => StartsWithKeyword(text, "timeline")),
        new("gitGraph", "gitGraph", DiagramSupport.NotPlanned, (text, _) => StartsWithKeyword(text, "gitGraph")),
        new("stateDiagram", "state", DiagramSupport.Supported, (text, settings) =>
            StartsWithKeyword(text, "stateDiagram-v2") || (StartsWithKeyword(text, "stateDiagram") && settings.StateRenderer == "dagre-wrapper")),
        new("state", "state", DiagramSupport.Supported, (text, settings) =>
            settings.StateRenderer != "dagre-wrapper" && StartsWithKeyword(text, "stateDiagram")),
        new("journey", "journey", DiagramSupport.NotPlanned, (text, _) => StartsWithKeyword(text, "journey")),
        new("quadrantChart", "quadrantChart", DiagramSupport.NotPlanned, (text, _) => StartsWithKeyword(text, "quadrantChart")),
        new("sankey", "sankey", DiagramSupport.NotPlanned, (text, _) => StartsWithKeyword(text, "sankey")),
        new("packet", "packet", DiagramSupport.NotPlanned, (text, _) => StartsWithKeyword(text, "packet")),
        new("xychart", "xychart", DiagramSupport.NotPlanned, (text, _) => StartsWithKeyword(text, "xychart")),
        new("block", "block", DiagramSupport.NotPlanned, (text, _) => StartsWithKeyword(text, "block")),
        new("eventmodeling", "eventmodeling", DiagramSupport.NotPlanned, (text, _) => StartsWithKeyword(text, "eventmodeling")),
        new("treeView", "treeView", DiagramSupport.NotPlanned, (text, _) => StartsWithKeyword(text, "treeView-beta")),
        new("radar", "radar", DiagramSupport.NotPlanned, (text, _) => StartsWithKeyword(text, "radar-beta")),
        new("ishikawa", "ishikawa", DiagramSupport.NotPlanned, (text, _) => StartsWithWord(text, "ishikawa", true)),
        new("treemap", "treemap", DiagramSupport.NotPlanned, (text, _) => StartsWithKeyword(text, "treemap")),
        new("railroad", "railroad", DiagramSupport.NotPlanned, (text, _) => StartsWithKeyword(text, "railroad-beta", true)),
        new("railroadEbnf", "railroadEbnf", DiagramSupport.NotPlanned, (text, _) => StartsWithKeyword(text, "railroad-ebnf-beta", true)),
        new("railroadAbnf", "railroadAbnf", DiagramSupport.NotPlanned, (text, _) => StartsWithKeyword(text, "railroad-abnf-beta", true)),
        new("railroadPeg", "railroadPeg", DiagramSupport.NotPlanned, (text, _) => StartsWithKeyword(text, "railroad-peg-beta", true)),
        new("venn", "venn", DiagramSupport.NotPlanned, (text, _) => StartsWithKeyword(text, "venn-beta")),
        new("wardley", "wardley", DiagramSupport.NotPlanned, (text, _) => StartsWithKeyword(text, "wardley-beta", true)),
        new("cynefin", "cynefin", DiagramSupport.NotPlanned, (text, _) => StartsWithCynefin(text)),
    ];

    /// <summary>
    ///     The keywords a diagram can start with, for the nearest-match hint of an unknown diagram.
    /// </summary>
    private static readonly string[] HintKeywords =
    [
        "flowchart", "graph", "sequenceDiagram", "classDiagram", "stateDiagram-v2", "stateDiagram", "erDiagram", "gantt", "pie",
        "mindmap", "journey", "gitGraph", "timeline", "quadrantChart", "requirementDiagram", "C4Context", "sankey-beta", "xychart-beta",
        "block-beta", "packet-beta", "kanban", "architecture-beta", "radar-beta", "treemap", "info",
    ];

    /// <summary>
    ///     Gets the keywords of the supported diagram types (<c>flowchart</c>, <c>graph</c>, <c>sequenceDiagram</c>, <c>classDiagram</c>, <c>stateDiagram</c> and <c>erDiagram</c>).
    /// </summary>
    public static IReadOnlyList<string> SupportedDiagramKeywords => SupportedKeywords;

    /// <summary>
    ///     Gets Mermaid's detector ids in detection order (for tests and documentation).
    /// </summary>
    public static IReadOnlyList<string> DetectorIds => [.. Known.Select(k => k.Id)];

    /// <summary>
    ///     Detects the diagram type of preprocessed diagram code.
    /// </summary>
    /// <param name="code">The diagram code (<see cref="PreprocessedSource.Code"/>).</param>
    /// <param name="settings">The detector settings of the effective configuration.</param>
    /// <returns>The detected type.</returns>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when no detector matches or the text starts with <c>---</c>.</exception>
    public static DetectedDiagram Detect(MappedText code, DetectorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(settings);

        // detectType's clean-up: a second frontmatter, the directives and every %% comment (replaced by a line feed)
        var text = code;
        if (Preprocessor.MatchFrontmatter(text.Text) is { } frontmatter)
        {
            text = text.Slice(frontmatter.End, text.Length - frontmatter.End);
        }

        text = ReplaceComments(Preprocessor.RemoveDirectives(text));

        // The keyword is the first word after the leading white space
        var start = 0;
        while (start < text.Length && ScanPatterns.IsJsWhitespace(text.Text[start]))
        {
            start++;
        }

        var end = start;
        while (end < text.Length && !ScanPatterns.IsJsWhitespace(text.Text[end]) && text.Text[end] != ';')
        {
            end++;
        }

        var keyword = text.Text[start..end];
        var location = text.Locate(start);

        // The first detector that matches wins
        foreach (var known in Known)
        {
            if (!known.Detector(text.Text, settings))
            {
                continue;
            }

            if (known.Id == "---")
            {
                throw new MermaidSyntaxException(
                    MermaidDiagnosticCodes.SyntaxError,
                    "Diagrams beginning with --- are not valid. If you were trying to use a YAML front-matter, please ensure that you've correctly opened and closed the YAML front-matter with un-indented `---` blocks",
                    location,
                    "close the frontmatter with a line of three dashes at the same indentation as the opening line");
            }

            return new DetectedDiagram(known.Id, known.Type, keyword, known.Support, location);
        }

        throw new MermaidSyntaxException(
            MermaidDiagnosticCodes.UnknownDiagram,
            keyword.Length == 0
                ? "the text contains no diagram: no diagram type keyword was found"
                : $"no diagram type was detected: '{Shorten(keyword)}' is not a Mermaid diagram keyword",
            location,
            UnknownHint(keyword));
    }

    /// <summary>
    ///     Gets the hint of the report for a type that is not drawn.
    /// </summary>
    /// <param name="diagram">The detected type.</param>
    /// <returns>The hint: whether the type is planned, and which keywords are supported.</returns>
    public static string UnsupportedHint(DetectedDiagram diagram)
    {
        ArgumentNullException.ThrowIfNull(diagram);
        var supported = $"supported in this version: {string.Join(", ", SupportedKeywords)}";
        return diagram.Support == DiagramSupport.Planned
            ? $"{diagram.DiagramType} diagrams are planned; {supported}"
            : $"{diagram.DiagramType} diagrams are not supported; {supported}";
    }

    /// <summary>
    ///     Replaces every <c>%%</c> comment with a line feed, as <c>detectType</c>'s <c>anyCommentRegex</c>
    ///     (<c>/\s*%%.*\n/gm</c>) does: the white space before the comment, the comment and its line feed become one line feed.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text without comments.</returns>
    internal static MappedText ReplaceComments(MappedText text)
    {
        var s = text.Text;
        var builder = new MappedText.Builder(text);
        var position = 0;
        var failsBefore = -1;
        while (position < s.Length)
        {
            // The match can start at the white space run before %% (or at %% itself)
            var j = position;
            while (j < s.Length && ScanPatterns.IsJsWhitespace(s[j]))
            {
                j++;
            }

            if (j >= failsBefore && string.CompareOrdinal(s, j, "%%", 0, 2) == 0)
            {
                // .* stops at the first line terminator, which must be a line feed
                var lineEnd = j + 2;
                while (lineEnd < s.Length && !ScanPatterns.IsJsLineTerminator(s[lineEnd]))
                {
                    lineEnd++;
                }

                if (lineEnd < s.Length && s[lineEnd] == '\n')
                {
                    builder.Append(lineEnd, 1);
                    position = lineEnd + 1;
                    continue;
                }

                // Every later %% on this line reaches the same terminator and fails the same way
                failsBefore = lineEnd;
            }

            // Every position of the run fails the same way, and so does j itself
            var keep = Math.Min(Math.Max(j, position + 1), s.Length);
            builder.Append(position, keep - position);
            position = keep;
        }

        return builder.Build(text.OriginOf(s.Length));
    }

    /// <summary>
    ///     Tests <c>/^\s*keyword/</c>: the keyword after the leading white space (it may be followed by anything).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="keyword">The keyword.</param>
    /// <param name="ignoreCase">Whether ASCII letters match regardless of case (the <c>i</c> flag).</param>
    /// <returns>Whether the text starts with the keyword.</returns>
    private static bool StartsWithKeyword(string text, string keyword, bool ignoreCase = false)
    {
        var start = SkipWhitespace(text);
        return string.Compare(text, start, keyword, 0, keyword.Length, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) == 0 &&
               start + keyword.Length <= text.Length;
    }

    /// <summary>
    ///     Tests <c>/^\s*keyword\b/</c>: the keyword after the leading white space, followed by a character that is not a
    ///     word character, or by the end. (<c>ishikawa(-beta)?\b</c> accepts the same texts, because <c>-</c> is not a
    ///     word character.)
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="keyword">The keyword; it ends with a word character.</param>
    /// <param name="ignoreCase">Whether ASCII letters match regardless of case.</param>
    /// <returns>Whether the text starts with the keyword as a whole word.</returns>
    private static bool StartsWithWord(string text, string keyword, bool ignoreCase)
    {
        if (!StartsWithKeyword(text, keyword, ignoreCase))
        {
            return false;
        }

        var after = SkipWhitespace(text) + keyword.Length;
        return after >= text.Length || !ScanPatterns.IsJsWordChar(text[after]);
    }

    /// <summary>
    ///     Tests <c>/^\s*cynefin-beta(?:[\s:]|$)/</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>Whether the text starts with the cynefin keyword.</returns>
    private static bool StartsWithCynefin(string text)
    {
        const string Keyword = "cynefin-beta";
        if (!StartsWithKeyword(text, Keyword))
        {
            return false;
        }

        var after = SkipWhitespace(text) + Keyword.Length;
        return after >= text.Length || text[after] == ':' || ScanPatterns.IsJsWhitespace(text[after]);
    }

    /// <summary>
    ///     Removes the leading white space of a text (JavaScript's <c>trimStart</c>).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The trimmed text.</returns>
    private static string TrimStartJs(string text) => text[SkipWhitespace(text)..];

    /// <summary>
    ///     Gets the position after the leading JavaScript white space.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The position.</returns>
    private static int SkipWhitespace(string text)
    {
        var i = 0;
        while (i < text.Length && ScanPatterns.IsJsWhitespace(text[i]))
        {
            i++;
        }

        return i;
    }

    /// <summary>
    ///     Builds the hint for an unknown keyword: the nearest known keyword by edit distance.
    /// </summary>
    /// <param name="keyword">The keyword as written.</param>
    /// <returns>The hint.</returns>
    private static string UnknownHint(string keyword)
    {
        var start = "start the diagram with a type keyword, for example 'flowchart TD' or 'graph LR'";
        if (keyword.Length == 0)
        {
            return start;
        }

        // The nearest keyword ignoring case; a tie keeps the earlier (more common) keyword
        var probe = keyword.Length > 64 ? keyword[..64] : keyword;
        var nearest = HintKeywords
            .Select(k => (Keyword: k, Distance: EditDistance(probe.ToLowerInvariant(), k.ToLowerInvariant())))
            .OrderBy(k => k.Distance)
            .First();
        return nearest.Distance <= Math.Max(2, nearest.Keyword.Length / 3)
            ? $"did you mean '{nearest.Keyword}'? {start}"
            : start;
    }

    /// <summary>
    ///     Computes the Levenshtein distance of two short texts.
    /// </summary>
    /// <param name="a">The first text.</param>
    /// <param name="b">The second text.</param>
    /// <returns>The number of insertions, deletions and substitutions that turn one into the other.</returns>
    internal static int EditDistance(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }

    /// <summary>
    ///     Shortens a keyword for a message.
    /// </summary>
    /// <param name="keyword">The keyword.</param>
    /// <returns>The keyword, cut to 40 characters with an ellipsis.</returns>
    private static string Shorten(string keyword) => keyword.Length <= 40 ? keyword : keyword[..40] + "\u2026";

    /// <summary>
    ///     One known diagram type.
    /// </summary>
    /// <param name="Id">Mermaid's detector id.</param>
    /// <param name="Type">The public type name.</param>
    /// <param name="Support">How far this version supports it.</param>
    /// <param name="Detector">The detector: the cleaned text and the settings to whether the text is of this type.</param>
    private sealed record KnownDiagram(string Id, string Type, DiagramSupport Support, Func<string, DetectorSettings, bool> Detector);
}
