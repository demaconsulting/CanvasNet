// Portions derived from PlantUML (MIT); see third-party/README.md
using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;
using DemaConsulting.CanvasNet.PlantUml.Syntax;

namespace DemaConsulting.CanvasNet.PlantUml.Activity;

/// <summary>
///     Reads the statements of an activity diagram of the new syntax into an <see cref="ActivityModel"/>.
/// </summary>
/// <remarks>
///     <para>
///         The parser reads PlantUML's activity statements one by one, by their leading keywords and punctuation,
///         without regular expressions, and builds the instruction tree as PlantUML's <c>ActivityDiagram3</c> does:
///         actions (<c>:text;</c>, on one line or many), <c>start</c>, <c>stop</c>, <c>end</c>, <c>kill</c>,
///         <c>detach</c>, ifs (<c>if</c>, <c>elseif</c>, <c>else</c>, <c>endif</c>), while and repeat loops (with
///         <c>backward</c>), forks (<c>end fork</c> or <c>end merge</c>), splits, swimlanes, partitions and groups,
///         notes, arrows (<c>-&gt; label;</c>, <c>-[#color,dashed]-&gt;</c>), connectors (<c>(A)</c>), page texts,
///         <c>skinparam</c> and <c>scale</c>.
///     </para>
///     <para>
///         A statement PlantUML accepts but this version does not draw (<c>switch</c>, <c>goto</c>, <c>break</c>, a box
///         style such as <c>&lt;&lt;output&gt;&gt;</c>, a <c>package</c> frame, ...) is reported as
///         <see cref="PlantUmlDiagnosticCodes.StatementUnsupported"/> and skipped (or drawn without the unsupported
///         part). A statement that is not valid PlantUML is a <see cref="PlantUmlSyntaxException"/> with
///         <see cref="PlantUmlDiagnosticCodes.SyntaxError"/> located at it. The instructions, label lengths and block
///         nesting are bounded by <see cref="PlantUmlLimits"/> (<see cref="PlantUmlDiagnosticCodes.LimitExceeded"/>).
///     </para>
///     <para>
///         An instance reads one diagram on one thread; <see cref="Parse"/> is thread-safe.
///     </para>
/// </remarks>
internal sealed partial class ActivityParser
{
    /// <summary>The largest diagram scale that is accepted.</summary>
    private const double MaxScale = 100;

    /// <summary>The statements of the diagram.</summary>
    private readonly IReadOnlyList<SourceStatement> _statements;

    /// <summary>The limits of the open call.</summary>
    private readonly PlantUmlLimits _limits;

    /// <summary>The reports found while reading.</summary>
    private readonly List<PlantUmlPendingReport> _reports = [];

    /// <summary>The skin parameters in order.</summary>
    private readonly List<SkinParamEntry> _skin = [];

    /// <summary>The swimlanes in order of declaration.</summary>
    private readonly List<ActivityLane> _lanes = [];

    /// <summary>The instructions of the diagram.</summary>
    private readonly ActivityList _root = new(null);

    /// <summary>The open blocks (if, while, repeat, fork, split, group), innermost last.</summary>
    private readonly List<ActivityInstruction> _open = [];

    /// <summary>The arrow that leads into the next instruction.</summary>
    private ActivityLink _next = ActivityLink.None;

    /// <summary>The current swimlane, or null before the first one.</summary>
    private ActivityLane? _lane;

    /// <summary>Whether a swimlane may still be declared (only before the first instruction).</summary>
    private bool? _lanesAllowed;

    /// <summary>The number of nodes read so far.</summary>
    private int _nodes;

    /// <summary>The page texts read so far.</summary>
    private PageTexts _page = PageTexts.None;

    /// <summary>The diagram's own scale.</summary>
    private double _scale = 1;

    /// <summary>
    ///     Initializes a parser.
    /// </summary>
    /// <param name="statements">The statements.</param>
    /// <param name="limits">The limits.</param>
    private ActivityParser(IReadOnlyList<SourceStatement> statements, PlantUmlLimits limits)
    {
        _statements = statements;
        _limits = limits;
    }

    /// <summary>
    ///     Gets the block that receives the instructions being read (the innermost open block, or the root list).
    /// </summary>
    private ActivityInstruction Current => _open.Count == 0 ? _root : _open[^1];

    /// <summary>
    ///     Reads an activity diagram.
    /// </summary>
    /// <param name="source">The preprocessed source.</param>
    /// <param name="detected">The detected type (activity).</param>
    /// <param name="limits">The limits of the open call.</param>
    /// <returns>The model.</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown, located, when a statement is not valid or a limit is exceeded.</exception>
    public static ActivityModel Parse(PreprocessedSource source, DetectedDiagram detected, PlantUmlLimits limits)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(detected);
        ArgumentNullException.ThrowIfNull(limits);
        var parser = new ActivityParser(source.Block.Statements, limits);
        parser.ReadAll();
        return parser.Build(detected.Location);
    }

    /// <summary>
    ///     Reads every statement and checks that every block is closed.
    /// </summary>
    private void ReadAll()
    {
        var i = 0;
        while (i < _statements.Count)
        {
            if (_statements[i].Text.Length > 0)
            {
                i = ReadStatement(i);
            }

            i++;
        }

        if (_open.Count > 0)
        {
            var block = _open[^1];
            var (what, closer) = block switch
            {
                ActivityIf => ("if", "endif"),
                ActivityWhile => ("while loop", "endwhile"),
                ActivityRepeat => ("repeat loop", "repeat while (...)"),
                ActivityFork => ("fork", "end fork"),
                ActivitySplit => ("split", "end split"),
                _ => ("group", "}"),
            };
            throw Error($"the {what} is not closed", block.Location, $"close it with '{closer}'");
        }
    }

    /// <summary>
    ///     Builds the model: applies the skin parameters and sorts the reports into text order.
    /// </summary>
    /// <param name="keywordLocation">The location the diagram type was detected at.</param>
    /// <returns>The model.</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the diagram has more arrows than <see cref="PlantUmlLimits.MaxEdges"/>.</exception>
    private ActivityModel Build(PlantUmlLocation keywordLocation)
    {
        var theme = SkinParams.Apply(SkinTheme.Default, _skin, _reports);

        // The skin parameters are reported when they are applied: put every report back in text order
        var reports = _reports.OrderBy(r => r.Location.Line).ThenBy(r => r.Location.Column).ToList();
        var model = new ActivityModel(_root, _lanes, _page, theme, reports, keywordLocation) { Scale = _scale };
        if (model.EdgeCount > _limits.MaxEdges)
        {
            throw new PlantUmlSyntaxException(
                PlantUmlDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"the diagram has more than {_limits.MaxEdges} arrows (PlantUmlLimits.MaxEdges)"),
                keywordLocation,
                "split the diagram, or raise PlantUmlLimits.MaxEdges when the source is trusted");
        }

        return model;
    }

    /// <summary>
    ///     Adds an instruction to the current block.
    /// </summary>
    /// <param name="instruction">The instruction.</param>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the diagram has too many nodes.</exception>
    private void Add(ActivityInstruction instruction)
    {
        CountNode(instruction);
        ListOf(Current).Add(instruction);
    }

    /// <summary>
    ///     Counts an instruction against <see cref="PlantUmlLimits.MaxNodes"/>.
    /// </summary>
    /// <param name="instruction">The instruction.</param>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the diagram has too many instructions.</exception>
    private void CountNode(ActivityInstruction instruction)
    {
        if (++_nodes > _limits.MaxNodes)
        {
            throw new PlantUmlSyntaxException(
                PlantUmlDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"the diagram has more than {_limits.MaxNodes} instructions (PlantUmlLimits.MaxNodes)"),
                instruction.Location,
                "split the diagram, or raise PlantUmlLimits.MaxNodes when the source is trusted");
        }
    }

    /// <summary>
    ///     Gets the list that receives the instructions of a block.
    /// </summary>
    /// <param name="block">The block.</param>
    /// <returns>The list.</returns>
    private static ActivityList ListOf(ActivityInstruction block) => block switch
    {
        ActivityIf branches => branches.Current.List,
        ActivityWhile loop => loop.Body,
        ActivityRepeat repeat => repeat.Body,
        ActivityFork fork => fork.Last,
        ActivitySplit split => split.Last,
        ActivityGroup group => group.Body,
        _ => (ActivityList)block,
    };

    /// <summary>
    ///     Gets the last instruction of a block (PlantUML's <c>InstructionCollection.getLast</c>), or null.
    /// </summary>
    /// <param name="block">The block.</param>
    /// <returns>The last instruction, or null.</returns>
    private static ActivityInstruction? LastOf(ActivityInstruction block) => block switch
    {
        ActivityList or ActivityIf or ActivityWhile or ActivityGroup => ListOf(block).Last,
        _ => null,
    };

    /// <summary>
    ///     Opens a block: adds it to the current block and makes it current.
    /// </summary>
    /// <param name="block">The block.</param>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the blocks nest too deeply.</exception>
    private void Open(ActivityInstruction block)
    {
        if (_open.Count >= _limits.MaxNestingDepth)
        {
            throw new PlantUmlSyntaxException(
                PlantUmlDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"the blocks nest more than {_limits.MaxNestingDepth} deep (PlantUmlLimits.MaxNestingDepth)"),
                block.Location,
                "nest fewer blocks, or raise PlantUmlLimits.MaxNestingDepth when the source is trusted");
        }

        Add(block);
        _open.Add(block);
    }

    /// <summary>
    ///     Closes the current block.
    /// </summary>
    private void Close() => _open.RemoveAt(_open.Count - 1);

    /// <summary>
    ///     Marks that the diagram has started: swimlanes may only be declared before (PlantUML's
    ///     <c>manageSwimlaneStrategy</c>).
    /// </summary>
    private void Started() => _lanesAllowed ??= false;

    /// <summary>
    ///     Takes the pending arrow and resets it.
    /// </summary>
    /// <returns>The arrow.</returns>
    private ActivityLink TakeNext()
    {
        var next = _next;
        _next = ActivityLink.None;
        return next;
    }

    /// <summary>
    ///     Checks a text against <see cref="PlantUmlLimits.MaxLabelLength"/>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="location">The location.</param>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the text is too long.</exception>
    private void CheckLabel(string? text, PlantUmlLocation location)
    {
        if (text is not null && text.Length > _limits.MaxLabelLength)
        {
            throw new PlantUmlSyntaxException(
                PlantUmlDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"a text is {text.Length} characters long, more than the limit of {_limits.MaxLabelLength} (PlantUmlLimits.MaxLabelLength)"),
                location,
                "shorten the text, or raise PlantUmlLimits.MaxLabelLength when the source is trusted");
        }
    }

    /// <summary>
    ///     Reports a statement that is not drawn.
    /// </summary>
    /// <param name="what">What is not drawn.</param>
    /// <param name="location">The location.</param>
    private void Unsupported(string what, PlantUmlLocation location) =>
        _reports.Add(PlantUmlPendingReport.Unsupported(
            PlantUmlDiagnosticCodes.StatementUnsupported,
            what,
            location,
            "remove the statement, or render the diagram with PlantUML itself"));

    /// <summary>
    ///     Creates a located syntax error.
    /// </summary>
    /// <param name="description">The description.</param>
    /// <param name="location">The location.</param>
    /// <param name="hint">The hint.</param>
    /// <returns>The exception.</returns>
    private static PlantUmlSyntaxException Error(string description, PlantUmlLocation location, string hint) =>
        new(PlantUmlDiagnosticCodes.SyntaxError, description, location, hint);

    /// <summary>
    ///     Shortens a statement for a message.
    /// </summary>
    /// <param name="text">The statement.</param>
    /// <returns>At most 60 characters.</returns>
    private static string Shorten(string text) => text.Length <= 60 ? text : string.Concat(text.AsSpan(0, 57), "...");

    /// <summary>
    ///     Reads a color written as <c>#name</c> or <c>#RRGGBB</c> at the start of a text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The index of the hash; advanced past the color and the white space after it.</param>
    /// <param name="location">The location for the error.</param>
    /// <returns>The color, or null when the text does not start with a hash at the index.</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the hash is not followed by a color.</exception>
    private static Rgba32? ReadLeadingColor(string text, ref int index, PlantUmlLocation location)
    {
        if (index >= text.Length || text[index] != '#')
        {
            return null;
        }

        var end = index + 1;
        while (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] is not ':' and not '(' and not '|')
        {
            end++;
        }

        var token = text[index..end];
        if (!SkinParams.TryParseColor(token, out var color))
        {
            throw Error($"'{token}' is not a color", location, "write a color such as #LightBlue or #AABBCC");
        }

        index = end;
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        return color;
    }

    /// <summary>
    ///     Reads a text in parentheses at an index: the text up to the closing parenthesis whose remainder satisfies a
    ///     condition (PlantUML's lazy <c>\((.*?)\)</c> followed by the rest of the statement).
    /// </summary>
    /// <param name="text">The statement text.</param>
    /// <param name="open">The index of the opening parenthesis.</param>
    /// <param name="rest">The condition the text after the closing parenthesis must satisfy.</param>
    /// <param name="close">Receives the index of the closing parenthesis.</param>
    /// <returns>The text in the parentheses, or null when no closing parenthesis satisfies the condition.</returns>
    private static string? ReadParenthesized(string text, int open, Func<string, bool> rest, out int close)
    {
        close = -1;
        if (open >= text.Length || text[open] != '(')
        {
            return null;
        }

        for (var i = text.IndexOf(')', open + 1); i >= 0; i = text.IndexOf(')', i + 1))
        {
            if (rest(text[(i + 1)..]))
            {
                close = i;
                return text[(open + 1)..i];
            }
        }

        return null;
    }

    /// <summary>
    ///     Skips white space.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The index; advanced past the white space.</param>
    private static void SkipSpace(string text, ref int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }
    }

    /// <summary>
    ///     Reads a word at an index, without regard to case.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The index; advanced past the word when it matches.</param>
    /// <param name="word">The word.</param>
    /// <returns>True when the word is at the index and is not followed by a letter or digit.</returns>
    private static bool TryWord(string text, ref int index, string word)
    {
        if (index + word.Length > text.Length || string.Compare(text, index, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) != 0)
        {
            return false;
        }

        var end = index + word.Length;
        if (end < text.Length && (char.IsAsciiLetterOrDigit(text[end]) || text[end] == '_'))
        {
            return false;
        }

        index = end;
        return true;
    }

    /// <summary>
    ///     Decides whether the end of a statement is empty or an optional semicolon.
    /// </summary>
    /// <param name="rest">The end of the statement.</param>
    /// <returns>True for nothing, white space, or a semicolon.</returns>
    private static bool IsEnd(string rest)
    {
        var trimmed = rest.Trim();
        return trimmed.Length == 0 || trimmed == ";";
    }
}
