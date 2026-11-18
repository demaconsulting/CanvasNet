using System.Globalization;

namespace DemaConsulting.CanvasNet.PlantUml.Syntax;

/// <summary>
///     The diagram block of a text: the statements between <c>@startXXX</c> and <c>@endXXX</c> (or the whole text when
///     it has no <c>@start</c> tag), after preprocessing.
/// </summary>
/// <param name="Tag">
///     The tag of the block in lowercase (<c>uml</c> for <c>@startuml</c>, <c>mindmap</c> for <c>@startmindmap</c>,
///     ...), or null for a text without an <c>@start</c> tag, which PlantUML reads as one <c>@startuml</c> diagram.
/// </param>
/// <param name="Location">The location of the <c>@start</c> tag, or of the start of the text.</param>
/// <param name="Statements">The statements of the block in text order, empty lines included.</param>
internal sealed record DiagramBlock(string? Tag, PlantUmlLocation Location, IReadOnlyList<SourceStatement> Statements);

/// <summary>
///     The result of preprocessing a text: its line table, its first diagram block and the reports found on the way.
/// </summary>
/// <param name="Lines">The line table of the original text.</param>
/// <param name="Block">The first diagram block.</param>
/// <param name="Reports">The pending reports in text order (ignored directives, more than one diagram).</param>
internal sealed record PreprocessedSource(SourceLines Lines, DiagramBlock Block, IReadOnlyList<PlantUmlPendingReport> Reports);

/// <summary>
///     Prepares a PlantUML text for detection and parsing: finds the first diagram block, removes the comments, joins
///     continued lines, and applies the supported subset of the PlantUML preprocessor, keeping the original location of
///     every character.
/// </summary>
/// <remarks>
///     <para>
///         <b>Framing.</b> The first <c>@startXXX</c> ... <c>@endXXX</c> block is the diagram; text before it is ignored. A
///         text without any <c>@start</c> tag is one diagram, as PlantUML reads a piped text. A further block is reported
///         as <see cref="PlantUmlDiagnosticCodes.MultipleDiagrams"/> and not read.
///     </para>
///     <para>
///         <b>Lines.</b> A line whose first character (after white space) is <c>'</c> is a comment, <c>/'</c> ...
///         <c>'/</c> is a comment that may span lines, and a line that ends with <c>\</c> continues on the next line.
///         Statements are trimmed; empty lines are kept as empty statements, because a multi-line note or legend keeps
///         them.
///     </para>
///     <para>
///         <b>Directives.</b> <c>!define NAME [value]</c> (no parameters), <c>!undef NAME</c>, <c>!$name = value</c> and
///         <c>!$name ?= value</c> (a quoted text, a number or another variable) are applied: every later <c>$name</c> and
///         every later <c>NAME</c> on word boundaries is replaced, once (a substituted value is not expanded again).
///         Every other directive is reported and dropped: includes as
///         <see cref="PlantUmlDiagnosticCodes.IncludeUnsupported"/>, <c>!theme</c> as
///         <see cref="PlantUmlDiagnosticCodes.ThemeUnsupported"/>, <c>!pragma</c> as the warning
///         <see cref="PlantUmlDiagnosticCodes.PragmaIgnored"/>, and the rest (<c>!if</c>, <c>!function</c>, ...) as
///         <see cref="PlantUmlDiagnosticCodes.PreprocessorUnsupported"/>; a directive that opens a block
///         (<c>!if</c> ... <c>!endif</c>, <c>!function</c> ... <c>!endfunction</c>, ...) is dropped with its whole block.
///         A built-in function call (<c>%date()</c>, ...) is reported and left as text.
///     </para>
///     <para>
///         <b>Limits.</b> The original text may be at most <see cref="SourceLengthFactor"/> times
///         <see cref="PlantUmlLimits.MaxTextSize"/> and the statements at most <see cref="PlantUmlLimits.MaxTextSize"/>
///         (an <see cref="InvalidDataException"/> otherwise); the nesting of dropped directive blocks is bounded by
///         <see cref="PlantUmlLimits.MaxNestingDepth"/>. No regular expression runs on the text and nothing is read from
///         another file or the network. The class is stateless and safe for concurrent use.
///     </para>
/// </remarks>
internal static class Preprocessor
{
    /// <summary>How many times <see cref="PlantUmlLimits.MaxTextSize"/> the original text may be.</summary>
    public const int SourceLengthFactor = 4;

    /// <summary>The directives that open a block, with the directive that closes it.</summary>
    private static readonly Dictionary<string, string> BlockDirectives = new(StringComparer.Ordinal)
    {
        ["if"] = "endif",
        ["ifdef"] = "endif",
        ["ifndef"] = "endif",
        ["while"] = "endwhile",
        ["foreach"] = "endfor",
        ["function"] = "endfunction",
        ["procedure"] = "endprocedure",
        ["definelong"] = "enddefinelong",
    };

    /// <summary>The include directives, none of which is followed by this version.</summary>
    private static readonly HashSet<string> IncludeDirectives = new(StringComparer.Ordinal)
    {
        "include", "include_once", "include_many", "includeurl", "includesub", "includedef", "import",
    };

    /// <summary>
    ///     Preprocesses a text.
    /// </summary>
    /// <param name="text">The original text.</param>
    /// <param name="sourceName">The source name placed in every location, or null.</param>
    /// <param name="limits">The limits of the open call.</param>
    /// <returns>The preprocessed source.</returns>
    /// <exception cref="InvalidDataException">Thrown when the text or its statements are longer than the limits allow.</exception>
    /// <exception cref="PlantUmlSyntaxException">Thrown, located, when a block has no end tag or the directives nest too deeply.</exception>
    public static PreprocessedSource Run(string text, string? sourceName, PlantUmlLimits limits)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(limits);

        // Refuse a hostile text before any work on it
        if ((long)text.Length > (long)limits.MaxTextSize * SourceLengthFactor)
        {
            throw new InvalidDataException(string.Create(
                CultureInfo.InvariantCulture,
                $"The PlantUML text is longer than {SourceLengthFactor} times the limit of {limits.MaxTextSize} characters (PlantUmlLimits.MaxTextSize)."));
        }

        var lines = new SourceLines(text, sourceName);
        var reports = new List<PlantUmlPendingReport>();
        var (tag, tagLocation, first, end) = Frame(lines, reports);
        var statements = new State(lines, limits, reports).Read(first, end);
        return new PreprocessedSource(lines, new DiagramBlock(tag, tagLocation, statements), reports);
    }

    /// <summary>
    ///     Finds the first diagram block of a text.
    /// </summary>
    /// <param name="lines">The line table.</param>
    /// <param name="reports">The pending reports, which receive a report for a further block.</param>
    /// <returns>The tag (null without one), its location, and the range of lines of the block's content.</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the block has no end tag.</exception>
    private static (string? Tag, PlantUmlLocation Location, int First, int End) Frame(SourceLines lines, List<PlantUmlPendingReport> reports)
    {
        // The first line that starts with @start opens the diagram; without one, the whole text is the diagram
        var start = -1;
        for (var i = 0; i < lines.Count && start < 0; i++)
        {
            if (TagOf(lines.LineText(i), "@start") is not null)
            {
                start = i;
            }
        }

        if (start < 0)
        {
            return (null, lines.Locate(0), 0, lines.Count);
        }

        var tag = TagOf(lines.LineText(start), "@start")!;
        var tagLocation = lines.Locate(lines.StartOf(start) + Indent(lines.LineText(start)));

        // The first @end line closes it, whatever its tag
        var end = -1;
        for (var i = start + 1; i < lines.Count && end < 0; i++)
        {
            if (TagOf(lines.LineText(i), "@end") is not null)
            {
                end = i;
            }
        }

        if (end < 0)
        {
            throw new PlantUmlSyntaxException(
                PlantUmlDiagnosticCodes.SyntaxError,
                $"the diagram that starts with '@start{tag}' has no '@end{tag}'",
                tagLocation,
                $"add a line '@end{tag}' after the last statement of the diagram",
                [$"@end{tag}"]);
        }

        // Only the first block is read; a further block is reported once
        for (var i = end + 1; i < lines.Count; i++)
        {
            if (TagOf(lines.LineText(i), "@start") is not null)
            {
                reports.Add(PlantUmlPendingReport.Unsupported(
                    PlantUmlDiagnosticCodes.MultipleDiagrams,
                    "the text holds more than one diagram; only the first one is drawn",
                    lines.Locate(lines.StartOf(i) + Indent(lines.LineText(i))),
                    "put each diagram in its own file or code block"));
                break;
            }
        }

        return (tag, tagLocation, start + 1, end);
    }

    /// <summary>
    ///     Reads the tag of a line that starts (after white space) with a prefix such as <c>@start</c>.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <param name="prefix">The prefix.</param>
    /// <returns>The letters after the prefix in lowercase (possibly empty), or null when the line does not start with it.</returns>
    internal static string? TagOf(string line, string prefix)
    {
        var text = line.AsSpan().TrimStart();
        if (!text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var rest = text[prefix.Length..];
        var length = 0;
        while (length < rest.Length && char.IsAsciiLetter(rest[length]))
        {
            length++;
        }

        return rest[..length].ToString().ToLowerInvariant();
    }

    /// <summary>
    ///     Counts the white space at the start of a line.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <returns>The number of leading white-space characters.</returns>
    private static int Indent(string line)
    {
        var i = 0;
        while (i < line.Length && char.IsWhiteSpace(line[i]))
        {
            i++;
        }

        return i;
    }

    /// <summary>
    ///     The state of one preprocessing run: the variables, the macros and the statements read so far.
    /// </summary>
    /// <param name="lines">The line table.</param>
    /// <param name="limits">The limits.</param>
    /// <param name="reports">The pending reports.</param>
    private sealed class State(SourceLines lines, PlantUmlLimits limits, List<PlantUmlPendingReport> reports)
    {
        /// <summary>
        ///     Checks whether a character can be part of a PlantUML identifier.
        /// </summary>
        /// <param name="c">The character.</param>
        /// <returns>True for a letter, a digit or an underscore.</returns>
        private static bool IsIdentifierChar(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';

        /// <summary>The variables (<c>!$name = value</c>) by name, without the dollar sign.</summary>
        private readonly Dictionary<string, string> _variables = new(StringComparer.Ordinal);

        /// <summary>The macros (<c>!define NAME value</c>) by name.</summary>
        private readonly Dictionary<string, string> _macros = new(StringComparer.Ordinal);

        /// <summary>The closing directives of the dropped blocks that are open, innermost last.</summary>
        private readonly Stack<string> _skipped = new();

        /// <summary>The statements read so far.</summary>
        private readonly List<SourceStatement> _statements = [];

        /// <summary>The total length of the statements read so far.</summary>
        private long _total;

        /// <summary>Whether a <c>/'</c> comment is open.</summary>
        private bool _inComment;

        /// <summary>
        ///     Reads the lines of a block into statements.
        /// </summary>
        /// <param name="first">The first line index of the block's content.</param>
        /// <param name="end">The index after its last line.</param>
        /// <returns>The statements.</returns>
        public List<SourceStatement> Read(int first, int end)
        {
            var statement = new SourceStatement.Builder();
            var continued = false;
            for (var i = first; i < end; i++)
            {
                // Strip the comments of the line; a line that is only a comment adds nothing
                var start = lines.StartOf(i);
                var line = lines.LineText(i);
                var lineBuilder = new SourceStatement.Builder();
                var hadComment = StripComments(line, start, lineBuilder);
                if (hadComment && lineBuilder.IsBlank && !continued)
                {
                    continue;
                }

                // A trailing backslash continues the statement on the next line
                lineBuilder.TrimEnd();
                if (lineBuilder.EndsWithContinuation() && i + 1 < end)
                {
                    lineBuilder.RemoveLast();
                    statement.AppendFrom(lineBuilder);
                    continued = true;
                    continue;
                }

                statement.AppendFrom(lineBuilder);
                Handle(statement.Build(lines, start + line.Length));
                statement = new SourceStatement.Builder();
                continued = false;
            }

            return _statements;
        }

        /// <summary>
        ///     Copies a line into a statement builder without its comments.
        /// </summary>
        /// <param name="line">The line text.</param>
        /// <param name="start">The offset of the line in the original text.</param>
        /// <param name="builder">The builder.</param>
        /// <returns>True when the line was, held or ended inside a comment.</returns>
        private bool StripComments(string line, int start, SourceStatement.Builder builder)
        {
            var comment = _inComment;

            // A line whose first character is an apostrophe is a comment (unless it opens a block comment)
            var indent = Indent(line);
            if (!_inComment && indent < line.Length && line[indent] == '\'' && !(indent + 1 < line.Length && line[indent + 1] == '/'))
            {
                return true;
            }

            var i = 0;
            while (i < line.Length)
            {
                var opensOrCloses = i + 1 < line.Length &&
                    ((_inComment && line[i] == '\'' && line[i + 1] == '/') || (!_inComment && line[i] == '/' && line[i + 1] == '\''));
                if (opensOrCloses)
                {
                    // A block comment starts at /' and ends at '/
                    _inComment = !_inComment;
                    comment = true;
                    i += 2;
                    continue;
                }

                if (!_inComment)
                {
                    builder.Append(line[i], start + i);
                }

                i++;
            }

            return comment;
        }

        /// <summary>
        ///     Handles one complete statement: a directive, a statement inside a dropped block, or a diagram statement.
        /// </summary>
        /// <param name="statement">The statement.</param>
        private void Handle(SourceStatement statement)
        {
            var name = DirectiveName(statement.Text);
            if (_skipped.Count > 0)
            {
                SkipNested(statement, name);
                return;
            }

            if (name is not null)
            {
                Directive(statement, name);
                return;
            }

            Add(Substitute(statement));
        }

        /// <summary>
        ///     Gets the original offset of a character of a statement.
        /// </summary>
        /// <param name="statement">The statement.</param>
        /// <param name="index">The index.</param>
        /// <returns>The offset.</returns>
        private static int OffsetOf(SourceStatement statement, int index) => statement.OriginOf(index);

        /// <summary>
        ///     Reads the name of a directive statement.
        /// </summary>
        /// <param name="text">The statement text.</param>
        /// <returns>The directive name in lowercase (<c>$</c> for a variable assignment), or null when it is not one.</returns>
        private static string? DirectiveName(string text)
        {
            if (text.Length < 2 || text[0] != '!')
            {
                return null;
            }

            if (text[1] == '$')
            {
                return "$";
            }

            var length = 1;
            while (length < text.Length && IsIdentifierChar(text[length]))
            {
                length++;
            }

            return length == 1 ? null : text[1..length].ToLowerInvariant();
        }

        /// <summary>
        ///     Follows the nesting of the dropped blocks.
        /// </summary>
        /// <param name="statement">A statement inside a dropped block.</param>
        /// <param name="name">Its directive name, or null.</param>
        private void SkipNested(SourceStatement statement, string? name)
        {
            if (name is null)
            {
                return;
            }

            if (name == _skipped.Peek())
            {
                _skipped.Pop();
            }
            else if (OpensBlock(statement.Text, name, out var closing))
            {
                Push(closing, statement);
            }
        }

        /// <summary>
        ///     Opens a dropped block, within the nesting limit.
        /// </summary>
        /// <param name="closing">The directive that closes it.</param>
        /// <param name="statement">The opening statement.</param>
        /// <exception cref="PlantUmlSyntaxException">Thrown when the blocks nest deeper than the limit.</exception>
        private void Push(string closing, SourceStatement statement)
        {
            if (_skipped.Count >= limits.MaxNestingDepth)
            {
                throw new PlantUmlSyntaxException(
                    PlantUmlDiagnosticCodes.LimitExceeded,
                    string.Create(CultureInfo.InvariantCulture, $"the preprocessor blocks nest deeper than the limit of {limits.MaxNestingDepth} (PlantUmlLimits.MaxNestingDepth)"),
                    statement.Location,
                    "flatten the nested directives");
            }

            _skipped.Push(closing);
        }

        /// <summary>
        ///     Decides whether a directive opens a block that ends with another directive.
        /// </summary>
        /// <param name="text">The statement text.</param>
        /// <param name="name">The directive name.</param>
        /// <param name="closing">Receives the closing directive.</param>
        /// <returns>True when the directive opens a block.</returns>
        private static bool OpensBlock(string text, string name, out string closing)
        {
            // !unquoted function and !final procedure are written with a qualifier first
            if (name is "unquoted" or "final")
            {
                var rest = text[(name.Length + 1)..].TrimStart();
                if (rest.StartsWith("function", StringComparison.OrdinalIgnoreCase))
                {
                    name = "function";
                }
                else if (rest.StartsWith("procedure", StringComparison.OrdinalIgnoreCase))
                {
                    name = "procedure";
                }
            }

            // A one-line function (with its !return on the same line) opens no block
            if (BlockDirectives.TryGetValue(name, out closing!) &&
                !(name == "function" && text.Contains("!return", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            closing = string.Empty;
            return false;
        }

        /// <summary>
        ///     Applies or reports a directive.
        /// </summary>
        /// <param name="statement">The directive statement.</param>
        /// <param name="name">Its name.</param>
        private void Directive(SourceStatement statement, string name)
        {
            switch (name)
            {
                case "$":
                    Assign(statement, 1);
                    return;
                case "global" or "local" when statement.Text[(name.Length + 1)..].TrimStart().StartsWith('$'):
                    Assign(statement, statement.Text.IndexOf('$', StringComparison.Ordinal));
                    return;
                case "define":
                    Define(statement);
                    return;
                case "undef":
                    _macros.Remove(statement.Text[6..].Trim());
                    return;
                case "startsub" or "endsub":
                    // Markers of the parts that !includesub takes from a file; they draw nothing
                    return;
                case "theme":
                    reports.Add(PlantUmlPendingReport.Unsupported(
                        PlantUmlDiagnosticCodes.ThemeUnsupported,
                        "'!theme' is not supported; the default look is drawn",
                        statement.Location,
                        "remove '!theme' and set the look with 'skinparam' statements"));
                    return;
                case "pragma":
                    reports.Add(PlantUmlPendingReport.Warning(
                        PlantUmlDiagnosticCodes.PragmaIgnored,
                        $"'{statement.Text}' has no effect on the drawing",
                        statement.Location));
                    return;
            }

            if (IncludeDirectives.Contains(name))
            {
                reports.Add(PlantUmlPendingReport.Unsupported(
                    PlantUmlDiagnosticCodes.IncludeUnsupported,
                    $"'!{name}' is not followed; what it would include is not drawn",
                    statement.Location,
                    "copy the included statements into the diagram"));
                return;
            }

            // Every other directive is dropped, with its whole block when it opens one
            reports.Add(PlantUmlPendingReport.Unsupported(
                PlantUmlDiagnosticCodes.PreprocessorUnsupported,
                $"the preprocessor directive '!{name}' is not supported and is dropped",
                statement.Location,
                "use only !define (without parameters), !undef and !$variable = value"));
            if (OpensBlock(statement.Text, name, out var closing))
            {
                Push(closing, statement);
            }
        }

        /// <summary>
        ///     Applies a variable assignment <c>!$name = value</c> or <c>!$name ?= value</c>.
        /// </summary>
        /// <param name="statement">The statement.</param>
        /// <param name="dollar">The index of the dollar sign.</param>
        private void Assign(SourceStatement statement, int dollar)
        {
            var text = statement.Text;
            var i = dollar + 1;
            while (i < text.Length && IsIdentifierChar(text[i]))
            {
                i++;
            }

            var name = text[(dollar + 1)..i];
            var rest = text[i..].TrimStart();
            var conditional = rest.StartsWith("?=", StringComparison.Ordinal);
            if (name.Length == 0 || !(conditional || rest.StartsWith('=')))
            {
                Unsupported(statement, "the variable statement is not an assignment of the supported form");
                return;
            }

            var value = ReadValue(rest[(conditional ? 2 : 1)..].Trim());
            if (value is null)
            {
                Unsupported(statement, "the value of the variable is an expression, which is not supported");
                return;
            }

            if (!conditional || !_variables.ContainsKey(name))
            {
                _variables[name] = value;
            }
        }

        /// <summary>
        ///     Reads the value of an assignment: a quoted text, a number or another variable.
        /// </summary>
        /// <param name="text">The value text.</param>
        /// <returns>The value, or null for an expression this version does not evaluate.</returns>
        private string? ReadValue(string text)
        {
            if (text.Length >= 2 && (text[0] is '"' or '\'') && text[^1] == text[0] && text.IndexOf(text[0], 1) == text.Length - 1)
            {
                return text[1..^1];
            }

            if (text.Length > 0 && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                return text;
            }

            if (text.Length > 1 && text[0] == '$' && text.Skip(1).All(IsIdentifierChar))
            {
                return _variables.GetValueOrDefault(text[1..]);
            }

            return null;
        }

        /// <summary>
        ///     Applies <c>!define NAME [value]</c>.
        /// </summary>
        /// <param name="statement">The statement.</param>
        private void Define(SourceStatement statement)
        {
            var text = statement.Text;
            var i = "!define".Length;
            while (i < text.Length && char.IsWhiteSpace(text[i]))
            {
                i++;
            }

            var nameStart = i;
            while (i < text.Length && IsIdentifierChar(text[i]))
            {
                i++;
            }

            var name = text[nameStart..i];
            if (name.Length == 0 || (i < text.Length && text[i] == '('))
            {
                Unsupported(statement, name.Length == 0
                    ? "the '!define' has no name"
                    : $"the macro '{name}' has parameters, which are not supported");
                return;
            }

            _macros[name] = text[i..].Trim();
        }

        /// <summary>
        ///     Records an unsupported preprocessor construct.
        /// </summary>
        /// <param name="statement">The statement.</param>
        /// <param name="message">The description.</param>
        private void Unsupported(SourceStatement statement, string message) =>
            reports.Add(PlantUmlPendingReport.Unsupported(
                PlantUmlDiagnosticCodes.PreprocessorUnsupported,
                message,
                statement.Location,
                "use only !define (without parameters), !undef and !$variable = value"));

        /// <summary>
        ///     Replaces the variables and macros of a diagram statement and reports its built-in function calls.
        /// </summary>
        /// <param name="statement">The statement.</param>
        /// <returns>The statement with the substitutions, or the statement itself when nothing changed.</returns>
        private SourceStatement Substitute(SourceStatement statement)
        {
            var text = statement.Text;
            if (text.Length == 0 || (_variables.Count == 0 && _macros.Count == 0 && !text.Contains('%')))
            {
                return statement;
            }

            var builder = new SourceStatement.Builder();
            var changed = false;
            var reported = false;
            var i = 0;
            while (i < text.Length)
            {
                // A word: a $variable, a macro on word boundaries, or a %function( call
                var wordStart = i;
                var sigil = text[i] is '$' or '%' ? text[i] : '\0';
                var j = sigil == '\0' ? i : i + 1;
                var atBoundary = wordStart == 0 || !IsIdentifierChar(text[wordStart - 1]);
                while (j < text.Length && IsIdentifierChar(text[j]))
                {
                    j++;
                }

                var word = text[(sigil == '\0' ? i : i + 1)..j];
                if (word.Length > 0 && atBoundary)
                {
                    string? value = null;
                    if (sigil == '$')
                    {
                        value = _variables.GetValueOrDefault(word);
                    }
                    else if (sigil == '\0' && (j == text.Length || !IsIdentifierChar(text[j])))
                    {
                        value = _macros.GetValueOrDefault(word);
                    }
                    else if (sigil == '%' && j < text.Length && text[j] == '(' && !reported)
                    {
                        reported = true;
                        reports.Add(PlantUmlPendingReport.Unsupported(
                            PlantUmlDiagnosticCodes.PreprocessorUnsupported,
                            $"the built-in function '%{word}()' is not supported; it is drawn as text",
                            statement.Locate(wordStart),
                            "write the value in the diagram instead of calling the function"));
                    }

                    if (value is not null)
                    {
                        builder.AppendInserted(value, OffsetOf(statement, wordStart));
                        changed = true;
                        i = j;
                        continue;
                    }
                }

                // Not substituted: copy the word (or the single character) as written
                var copyEnd = Math.Max(j, i + 1);
                for (var k = i; k < copyEnd; k++)
                {
                    builder.Append(text[k], OffsetOf(statement, k));
                }

                i = copyEnd;
            }

            return changed ? builder.Build(lines, OffsetOf(statement, text.Length)) : statement;
        }

        /// <summary>
        ///     Adds a diagram statement within the size limit.
        /// </summary>
        /// <param name="statement">The statement.</param>
        /// <exception cref="InvalidDataException">Thrown when the statements are longer than the limit allows.</exception>
        private void Add(SourceStatement statement)
        {
            _total += statement.Text.Length;
            if (_total > limits.MaxTextSize)
            {
                throw new InvalidDataException(string.Create(
                    CultureInfo.InvariantCulture,
                    $"The PlantUML diagram is longer than the limit of {limits.MaxTextSize} characters (PlantUmlLimits.MaxTextSize) after preprocessing."));
            }

            _statements.Add(statement);
        }
    }
}
