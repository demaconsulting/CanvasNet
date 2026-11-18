// Portions derived from PlantUML (MIT); see third-party/README.md
using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;
using DemaConsulting.CanvasNet.PlantUml.Syntax;

namespace DemaConsulting.CanvasNet.PlantUml.Activity;

/// <content>
///     The readers of the individual statements.
/// </content>
internal sealed partial class ActivityParser
{
    /// <summary>The characters that end an action other than the semicolon (box styles PlantUML draws with SDL shapes).</summary>
    private const string StyleTerminators = "|<>/\\]}";

    /// <summary>The keywords of statements PlantUML accepts that this version does not draw, with what is not drawn.</summary>
    private static readonly Dictionary<string, string> UnsupportedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["goto"] = "'goto' jumps are not drawn",
        ["label"] = "'label' targets of 'goto' are not drawn",
        ["break"] = "'break' is not drawn; the loop is drawn without it",
        ["hide"] = "'hide' is not drawn",
        ["show"] = "'show' is not drawn",
        ["skin"] = "'skin' is not drawn; the default look is drawn",
        ["mainframe"] = "the main frame (mainframe) is not drawn",
    };

    /// <summary>
    ///     Reads one statement (and the lines of its block).
    /// </summary>
    /// <param name="index">The index of the statement.</param>
    /// <returns>The index of the last statement read.</returns>
    private int ReadStatement(int index)
    {
        var statement = _statements[index];
        var text = statement.Text;
        if (SkinParams.TryRead(_statements, ref index, _skin) || SkinParams.TrySkipStyle(_statements, ref index, _reports))
        {
            return index;
        }

        switch (text[0])
        {
            case ':':
                return ReadAction(index, 0, null);
            case '#':
                return ReadColored(index);
            case '|':
                ReadSwimlane(statement);
                return index;
            case '}':
                CloseGroup(statement);
                return index;
            case '(':
                return ReadParenthesisStatement(index);
        }

        if (text.StartsWith("->", StringComparison.Ordinal) || text.StartsWith("-[", StringComparison.Ordinal))
        {
            ReadArrow(statement);
            return index;
        }

        return ReadKeywordStatement(index, null, 0);
    }

    /// <summary>
    ///     Reads a statement that starts with a color: <c>#color:action;</c>, <c>#color if</c>, <c>#color while</c>,
    ///     <c>#color repeat</c> or <c>#color (A)</c>.
    /// </summary>
    /// <param name="index">The index of the statement.</param>
    /// <returns>The index of the last statement read.</returns>
    private int ReadColored(int index)
    {
        var statement = _statements[index];
        var text = statement.Text;
        var i = 0;
        var color = ReadLeadingColor(text, ref i, statement.Location);
        if (i < text.Length && text[i] == ':')
        {
            return ReadAction(index, i, color);
        }

        if (i < text.Length && text[i] == '(')
        {
            ReadSpot(statement, i, color);
            return index;
        }

        return ReadKeywordStatement(index, color, i);
    }

    /// <summary>
    ///     Reads a statement that starts with a keyword.
    /// </summary>
    /// <param name="index">The index of the statement.</param>
    /// <param name="color">The color written before the keyword, or null.</param>
    /// <param name="start">The index of the keyword in the statement.</param>
    /// <returns>The index of the last statement read.</returns>
    private int ReadKeywordStatement(int index, Rgba32? color, int start)
    {
        var statement = _statements[index];
        var text = statement.Text;
        var word = DiagramDetector.FirstWord(text[start..]).ToLowerInvariant();
        var rest = text[(start + word.Length)..];
        if (color is not null && word is not ("if" or "while" or "repeat"))
        {
            throw Error($"a color cannot start '{Shorten(text)}'", statement.Location, "write a color only before an action, if, while, repeat or connector");
        }

        switch (word)
        {
            case "start":
                RequireEnd(statement, rest, "start");
                Started();
                Add(new ActivityStart(_lane, TakeNext(), statement.Location));
                return index;
            case "stop":
                RequireEnd(statement, rest, "stop");
                AddStopOrEnd(new ActivityStop(_lane, _next, statement.Location));
                return index;
            case "end":
                return ReadEnd(index, rest);
            case "kill" or "detach":
                RequireEnd(statement, rest, word);
                Kill(statement);
                return index;
            case "if":
                ReadIf(statement, start + word.Length, color);
                return index;
            case "elseif":
                ReadElseIf(statement, start + word.Length, ActivityLink.None);
                return index;
            case "else":
                ReadElse(statement, rest);
                return index;
            case "endif":
                RequireEnd(statement, rest, "endif");
                Endif(statement);
                return index;
            case "while":
                ReadWhile(statement, start + word.Length, color);
                return index;
            case "endwhile":
                EndWhile(statement, rest);
                return index;
            case "repeat":
                ReadRepeat(statement, start + word.Length, color);
                return index;
            case "backward":
                ReadBackward(statement, word.Length, ActivityLink.None);
                return index;
            case "fork":
                ReadFork(statement, rest);
                return index;
            case "split":
                ReadSplit(statement, rest);
                return index;
            case "partition" or "group" or "package" or "rectangle" or "card":
                ReadGroup(statement, word, rest);
                return index;
            case "note":
                return ReadNote(index, false, word.Length);
            case "floating":
                return ReadFloatingNote(index);
            case "swimlane":
                ReadSwimlaneKeyword(statement, rest);
                return index;
            case "title" or "header" or "footer" or "caption" or "legend" or "left" or "right" or "center":
                return ReadPageText(index, word);
            case "scale":
                ReadScale(statement);
                return index;
            case "switch":
                return SkipSwitch(index);
        }

        if (UnsupportedKeywords.TryGetValue(word, out var what))
        {
            Unsupported(what, statement.Location);
            return index;
        }

        throw Error(
            $"'{Shorten(text)}' is not an activity diagram statement",
            statement.Location,
            "write an action as ':text;' and use start, stop, if, while, repeat, fork, split and partition");
    }

    /// <summary>
    ///     Checks that nothing but an optional semicolon follows a keyword.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="rest">The text after the keyword.</param>
    /// <param name="keyword">The keyword.</param>
    /// <exception cref="PlantUmlSyntaxException">Thrown when something else follows.</exception>
    private static void RequireEnd(SourceStatement statement, string rest, string keyword)
    {
        var trimmed = rest.Trim();
        if (IsEnd(trimmed) || (trimmed.StartsWith("<<", StringComparison.Ordinal) && trimmed.EndsWith(">>", StringComparison.Ordinal)))
        {
            return;
        }

        throw Error($"'{Shorten(statement.Text)}' has text after '{keyword}'", statement.Location, $"write '{keyword}' alone on its line");
    }

    /// <summary>
    ///     Reads an action <c>:text;</c>, on one line or on the following lines up to a line that ends it.
    /// </summary>
    /// <param name="index">The index of the statement.</param>
    /// <param name="colon">The index of the colon in the statement.</param>
    /// <param name="color">The fill written before it, or null.</param>
    /// <returns>The index of the last statement read.</returns>
    private int ReadAction(int index, int colon, Rgba32? color)
    {
        var statement = _statements[index];
        var first = statement.Text[(colon + 1)..];
        string label;
        if (TryEndAction(first, statement.Location, out var single))
        {
            label = single;
        }
        else
        {
            var lines = new List<string> { first };
            var i = index + 1;
            while (true)
            {
                if (i >= _statements.Count)
                {
                    throw Error("the action is not closed", statement.Location, "end the action with ';'");
                }

                if (TryEndAction(_statements[i].Text, _statements[i].Location, out var last))
                {
                    lines.Add(last);
                    break;
                }

                lines.Add(_statements[i].Text);
                i++;
            }

            label = string.Join('\n', lines);
            index = i;
        }

        CheckLabel(label, statement.Location);
        Started();
        Add(new ActivityAction(label, color, _lane, TakeNext(), statement.Location));
        return index;
    }

    /// <summary>
    ///     Reads the end of an action: the text up to a semicolon followed by nothing, a stereotype or a link, or up to
    ///     a box style character at the end of the line.
    /// </summary>
    /// <param name="text">The text of the line after the colon (or a continuation line).</param>
    /// <param name="location">The location of the line.</param>
    /// <param name="label">Receives the text of the line that belongs to the action.</param>
    /// <returns>True when the line ends the action.</returns>
    private bool TryEndAction(string text, PlantUmlLocation location, out string label)
    {
        // The first semicolon after which only a stereotype or a link follows ends the action
        for (var i = text.IndexOf(';', StringComparison.Ordinal); i >= 0; i = text.IndexOf(';', i + 1))
        {
            var rest = text[(i + 1)..].Trim();
            if (rest.Length == 0 || IsDecoration(rest))
            {
                if (rest.Length > 0)
                {
                    Unsupported($"the decoration '{Shorten(rest)}' of the action is not drawn; the action is drawn as a plain box", location);
                }

                label = text[..i];
                return true;
            }
        }

        // A box style character at the end selects one of PlantUML's SDL shapes, drawn here as a plain box
        if (text.Length > 0 && StyleTerminators.Contains(text[^1], StringComparison.Ordinal))
        {
            Unsupported($"the box style '{text[^1]}' is not drawn; the action is drawn as a plain box", location);
            label = text[..^1];
            return true;
        }

        label = text;
        return false;
    }

    /// <summary>
    ///     Decides whether the text after an action's semicolon is a stereotype or a link.
    /// </summary>
    /// <param name="rest">The trimmed text.</param>
    /// <returns>True for a stereotype (<c>&lt;&lt;x&gt;&gt;</c>) or a link (<c>[[x]]</c>).</returns>
    private static bool IsDecoration(string rest) =>
        (rest.StartsWith("<<", StringComparison.Ordinal) && rest.EndsWith(">>", StringComparison.Ordinal)) ||
        (rest.StartsWith("[[", StringComparison.Ordinal) && rest.EndsWith("]]", StringComparison.Ordinal));

    /// <summary>
    ///     Adds a stop or an end node; right after a while loop at the top level it becomes the loop's exit (PlantUML's
    ///     <c>manageSpecialStopEndAfterEndWhile</c>).
    /// </summary>
    /// <param name="node">The node.</param>
    private void AddStopOrEnd(ActivityInstruction node)
    {
        Started();
        if (Current is ActivityList list && list.Last is ActivityWhile loop)
        {
            CountNode(node);
            loop.SpecialOut = node;
            return;
        }

        Add(node);
    }

    /// <summary>
    ///     Reads <c>end</c> and the statements that start with it (<c>end fork</c>, <c>end merge</c>, <c>end split</c>,
    ///     <c>end while</c>, <c>end if</c>, <c>end group</c>, <c>end switch</c>).
    /// </summary>
    /// <param name="index">The index of the statement.</param>
    /// <param name="rest">The text after <c>end</c>.</param>
    /// <returns>The index of the last statement read.</returns>
    private int ReadEnd(int index, string rest)
    {
        var statement = _statements[index];
        var trimmed = rest.TrimStart();
        var next = DiagramDetector.FirstWord(trimmed).ToLowerInvariant();
        var after = trimmed[next.Length..];
        switch (next)
        {
            case "":
                RequireEnd(statement, rest, "end");
                AddStopOrEnd(new ActivityEnd(_lane, _next, statement.Location));
                return index;
            case "fork" or "merge":
                EndFork(statement, next == "merge" ? ActivityForkStyle.Merge : ActivityForkStyle.Fork, after);
                return index;
            case "split":
                RequireEnd(statement, after, "end split");
                EndSplit(statement);
                return index;
            case "while":
                EndWhile(statement, after);
                return index;
            case "if":
                RequireEnd(statement, after, "end if");
                Endif(statement);
                return index;
            case "group" or "partition":
                CloseGroup(statement);
                return index;
        }

        throw Error($"'{Shorten(statement.Text)}' is not an activity diagram statement", statement.Location, "write 'end', 'end fork', 'end merge', 'end split', 'end while' or 'end if'");
    }

    /// <summary>
    ///     Ends the flow after the last instruction (<c>kill</c>, <c>detach</c>).
    /// </summary>
    /// <param name="statement">The statement.</param>
    private void Kill(SourceStatement statement)
    {
        if (!Current.Kill())
        {
            throw Error("'kill' or 'detach' cannot be used here", statement.Location, "write it after an action");
        }
    }

    /// <summary>
    ///     Reads <c>if (test) then (label)</c> or <c>if (test) is (value) then</c>.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="start">The index after the keyword.</param>
    /// <param name="color">The fill of the diamond, or null.</param>
    private void ReadIf(SourceStatement statement, int start, Rgba32? color)
    {
        var text = statement.Text;
        var i = start;
        SkipSpace(text, ref i);
        string? when = null;
        var test = ReadParenthesized(text, i, rest => TryIfRest(rest, out when), out _) ??
            throw Error($"'{Shorten(text)}' is not a valid if", statement.Location, "write for example 'if (valid?) then (yes)'");
        CheckLabel(test, statement.Location);
        CheckLabel(when, statement.Location);
        Started();
        var block = new ActivityIf(_lane, test, Label(when), TakeNext(), color, statement.Location);
        Open(block);
    }

    /// <summary>
    ///     Reads what follows the test of an if: <c>then</c>, <c>then (label)</c>, <c>is (value) then</c>, or nothing.
    /// </summary>
    /// <param name="rest">The text after the closing parenthesis of the test.</param>
    /// <param name="when">Receives the label of the then arrow, or null.</param>
    /// <returns>True when the text is valid.</returns>
    private static bool TryIfRest(string rest, out string? when)
    {
        when = null;
        var i = 0;
        SkipSpace(rest, ref i);
        if (TryWord(rest, ref i, "is") || TryWord(rest, ref i, "equals") || TryWord(rest, ref i, "equal"))
        {
            SkipSpace(rest, ref i);
            string? value = null;
            var inner = ReadParenthesized(rest, i, after =>
            {
                var j = 0;
                SkipSpace(after, ref j);
                return TryWord(after, ref j, "then") && IsStereotypeEnd(after[j..]);
            }, out _);
            value = inner;
            when = value;
            return inner is { Length: > 0 };
        }

        if (TryWord(rest, ref i, "then"))
        {
            SkipSpace(rest, ref i);
            if (i < rest.Length && rest[i] == '(')
            {
                var label = ReadParenthesized(rest, i, IsStereotypeEnd, out _);
                when = label;
                return label is { Length: > 0 };
            }
        }

        return IsStereotypeEnd(rest[i..]);
    }

    /// <summary>
    ///     Decides whether the end of a statement is empty, a semicolon, or a stereotype.
    /// </summary>
    /// <param name="rest">The end of the statement.</param>
    /// <returns>True when nothing else follows.</returns>
    private static bool IsStereotypeEnd(string rest)
    {
        var trimmed = rest.Trim().TrimStart(';').Trim();
        return trimmed.Length == 0 || (trimmed.StartsWith("<<", StringComparison.Ordinal) && trimmed.EndsWith(">>", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Reads <c>elseif (test) then (label)</c> (also written <c>else if</c>), after an optional incoming label.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="start">The index of the opening parenthesis of the test (or the space before it).</param>
    /// <param name="inLabel">The label of the arrow to the diamond.</param>
    private void ReadElseIf(SourceStatement statement, int start, ActivityLink inLabel)
    {
        var text = statement.Text;
        var i = start;
        SkipSpace(text, ref i);
        string? when = null;
        var test = ReadParenthesized(text, i, rest => TryElseIfRest(rest, out when), out _) ??
            throw Error($"'{Shorten(text)}' is not a valid elseif", statement.Location, "write for example 'elseif (other?) then (yes)'");
        CheckLabel(test, statement.Location);
        if (Current is not ActivityIf block)
        {
            throw Error("'elseif' must be inside an if", statement.Location, "open the if first with 'if (test) then'");
        }

        if (!block.ElseIf(inLabel, test, Label(when), TakeNext(), null))
        {
            throw Error("'elseif' cannot follow 'else'", statement.Location, "write the elseif branches before the else branch");
        }
    }

    /// <summary>
    ///     Reads what follows the test of an elseif: nothing, <c>then</c>, or <c>then (label)</c>.
    /// </summary>
    /// <param name="rest">The text after the closing parenthesis of the test.</param>
    /// <param name="when">Receives the label of the then arrow, or null.</param>
    /// <returns>True when the text is valid.</returns>
    private static bool TryElseIfRest(string rest, out string? when)
    {
        when = null;
        var i = 0;
        SkipSpace(rest, ref i);
        if (TryWord(rest, ref i, "then"))
        {
            SkipSpace(rest, ref i);
            if (i < rest.Length && rest[i] == '(')
            {
                var label = ReadParenthesized(rest, i, IsStereotypeEnd, out _);
                when = label is null ? null : StripArrowPrefix(label);
                return label is not null;
            }
        }

        return IsStereotypeEnd(rest[i..]);
    }

    /// <summary>
    ///     Removes the <c>-&gt;</c> that may start the label of a branch arrow.
    /// </summary>
    /// <param name="label">The label.</param>
    /// <returns>The label without it.</returns>
    private static string StripArrowPrefix(string label) =>
        label.StartsWith("->", StringComparison.Ordinal) ? label[2..] : label;

    /// <summary>
    ///     Reads <c>else</c>, <c>else (label)</c>, or <c>else if (...)</c>.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="rest">The text after <c>else</c>.</param>
    private void ReadElse(SourceStatement statement, string rest)
    {
        var i = 0;
        SkipSpace(rest, ref i);
        if (TryWord(rest, ref i, "if"))
        {
            ReadElseIf(statement, statement.Text.Length - rest.Length + i, ActivityLink.None);
            return;
        }

        string? when = null;
        if (i < rest.Length && rest[i] == '(')
        {
            when = ReadParenthesized(rest, i, IsEnd, out _) ??
                throw Error($"'{Shorten(statement.Text)}' is not a valid else", statement.Location, "write 'else' or 'else (label)'");
            when = StripArrowPrefix(when);
        }
        else if (!IsEnd(rest))
        {
            throw Error($"'{Shorten(statement.Text)}' is not a valid else", statement.Location, "write 'else' or 'else (label)'");
        }

        CheckLabel(when, statement.Location);
        if (Current is not ActivityIf block)
        {
            throw Error("'else' must be inside an if", statement.Location, "open the if first with 'if (test) then'");
        }

        if (!block.SwitchToElse(Label(when), TakeNext()))
        {
            throw Error("the if already has an else branch", statement.Location, "write one 'else' per if");
        }
    }

    /// <summary>
    ///     Reads <c>endif</c>.
    /// </summary>
    /// <param name="statement">The statement.</param>
    private void Endif(SourceStatement statement)
    {
        if (Current is not ActivityIf block)
        {
            throw Error("'endif' must close an if", statement.Location, "remove the 'endif' or open the if first");
        }

        block.Endif(TakeNext());
        Close();
    }

    /// <summary>
    ///     Reads <c>while (test)</c> or <c>while (test) is (label)</c>.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="start">The index after the keyword.</param>
    /// <param name="color">The fill of the diamond, or null.</param>
    private void ReadWhile(SourceStatement statement, int start, Rgba32? color)
    {
        var text = statement.Text;
        var i = start;
        SkipSpace(text, ref i);
        if (TryWord(text, ref i, "end"))
        {
            EndWhile(statement, text[i..]);
            return;
        }

        string? yes = null;
        var test = ReadParenthesized(text, i, rest => TryLabeledRest(rest, out yes), out _) ??
            throw Error($"'{Shorten(text)}' is not a valid while", statement.Location, "write for example 'while (more?) is (yes)'");
        CheckLabel(test, statement.Location);
        CheckLabel(yes, statement.Location);
        Started();

        // PlantUML keeps the pending arrow for the first instruction of the body as well
        Open(new ActivityWhile(_lane, test, yes, _next, color, statement.Location));
    }

    /// <summary>
    ///     Reads what may follow a while test: nothing, or <c>is (label)</c> / <c>equals (label)</c>.
    /// </summary>
    /// <param name="rest">The text after the closing parenthesis of the test.</param>
    /// <param name="label">Receives the label, or null.</param>
    /// <returns>True when the text is valid.</returns>
    private static bool TryLabeledRest(string rest, out string? label)
    {
        label = null;
        var i = 0;
        SkipSpace(rest, ref i);
        if (TryWord(rest, ref i, "is") || TryWord(rest, ref i, "equals") || TryWord(rest, ref i, "equal"))
        {
            SkipSpace(rest, ref i);
            var inner = ReadParenthesized(rest, i, IsStereotypeEnd, out _);
            label = inner;
            return inner is { Length: > 0 };
        }

        return IsStereotypeEnd(rest[i..]);
    }

    /// <summary>
    ///     Reads <c>endwhile</c> or <c>endwhile (label)</c>.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="rest">The text after the keyword.</param>
    private void EndWhile(SourceStatement statement, string rest)
    {
        var i = 0;
        SkipSpace(rest, ref i);
        string? exit = null;
        if (i < rest.Length && rest[i] == '(')
        {
            exit = ReadParenthesized(rest, i, IsEnd, out _);
            if (exit is not { Length: > 0 })
            {
                throw Error($"'{Shorten(statement.Text)}' is not a valid endwhile", statement.Location, "write 'endwhile' or 'endwhile (label)'");
            }
        }
        else if (!IsEnd(rest))
        {
            throw Error($"'{Shorten(statement.Text)}' is not a valid endwhile", statement.Location, "write 'endwhile' or 'endwhile (label)'");
        }

        CheckLabel(exit, statement.Location);
        if (Current is not ActivityWhile loop)
        {
            throw Error("'endwhile' must close a while loop", statement.Location, "remove the 'endwhile' or open the loop first");
        }

        loop.SetIncoming(TakeNext());
        if (exit is not null)
        {
            loop.OutLink = loop.OutLink with { Label = exit };
        }

        Close();
    }

    /// <summary>
    ///     Reads <c>repeat</c>, <c>repeat :label;</c>, or <c>repeat while ...</c>.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="start">The index after the keyword.</param>
    /// <param name="color">The fill of the start, or null.</param>
    private void ReadRepeat(SourceStatement statement, int start, Rgba32? color)
    {
        var text = statement.Text;
        var i = start;
        SkipSpace(text, ref i);
        if (TryWord(text, ref i, "while"))
        {
            RepeatWhile(statement, text[i..]);
            return;
        }

        string? label = null;
        if (i < text.Length && text[i] == ':')
        {
            var semicolon = text.LastIndexOf(';');
            if (semicolon <= i || !IsStereotypeEnd(text[(semicolon + 1)..]))
            {
                throw Error($"'{Shorten(text)}' is not a valid repeat", statement.Location, "write 'repeat' or 'repeat :label;'");
            }

            label = text[(i + 1)..semicolon];
        }
        else if (!IsStereotypeEnd(text[i..]))
        {
            throw Error($"'{Shorten(text)}' is not a valid repeat", statement.Location, "write 'repeat' or 'repeat :label;'");
        }

        CheckLabel(label, statement.Location);
        Started();
        Open(new ActivityRepeat(_lane, label, TakeNext(), color, statement.Location));
    }

    /// <summary>
    ///     Reads <c>repeat while (test) is (yes) not (no)</c> and its shorter forms, with an optional arrow back.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="rest">The text after <c>repeat while</c>.</param>
    private void RepeatWhile(SourceStatement statement, string rest)
    {
        var i = 0;
        SkipSpace(rest, ref i);
        string? test = null;
        string? yes = null;
        string? exit = null;
        var back = ActivityLink.None;
        if (i < rest.Length && rest[i] == '(')
        {
            test = ReadParenthesized(rest, i, after => TryRepeatRest(after, statement.Location, out yes, out exit, out back), out _) ??
                throw Error($"'{Shorten(statement.Text)}' is not a valid repeat while", statement.Location, "write for example 'repeat while (more?) is (yes) not (no)'");
        }
        else if (!TryArrowEnd(rest[i..], statement.Location, out back))
        {
            throw Error($"'{Shorten(statement.Text)}' is not a valid repeat while", statement.Location, "write for example 'repeat while (more?) is (yes) not (no)'");
        }

        CheckLabel(test, statement.Location);
        CheckLabel(yes, statement.Location);
        CheckLabel(exit, statement.Location);
        Started();
        if (Current is not ActivityRepeat loop)
        {
            throw Error("'repeat while' must close a repeat loop", statement.Location, "open the loop first with 'repeat'");
        }

        loop.SetTest(test, yes, exit, TakeNext(), back);
        loop.LaneOut = _lane;
        Close();
    }

    /// <summary>
    ///     Reads what follows the test of a repeat loop: <c>is (yes)</c>, <c>not (no)</c>, both, or neither, then an
    ///     optional arrow back.
    /// </summary>
    /// <param name="rest">The text after the closing parenthesis of the test.</param>
    /// <param name="location">The location for reports.</param>
    /// <param name="yes">Receives the label of the arrow back.</param>
    /// <param name="exit">Receives the label of the exit arrow.</param>
    /// <param name="back">Receives the arrow back.</param>
    /// <returns>True when the text is valid.</returns>
    private bool TryRepeatRest(string rest, PlantUmlLocation location, out string? yes, out string? exit, out ActivityLink back)
    {
        yes = null;
        exit = null;
        back = ActivityLink.None;
        var i = 0;
        SkipSpace(rest, ref i);
        if (TryWord(rest, ref i, "is") || TryWord(rest, ref i, "equals") || TryWord(rest, ref i, "equal"))
        {
            SkipSpace(rest, ref i);
            if (!TryReadGroup(rest, ref i, out yes))
            {
                return false;
            }

            SkipSpace(rest, ref i);
        }

        if (TryWord(rest, ref i, "not"))
        {
            SkipSpace(rest, ref i);
            if (!TryReadGroup(rest, ref i, out exit))
            {
                return false;
            }
        }

        return TryArrowEnd(rest[i..], location, out back);
    }

    /// <summary>
    ///     Reads a non-empty text in parentheses (without nested parentheses) at an index.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The index of the opening parenthesis; advanced past the closing one.</param>
    /// <param name="value">Receives the text inside.</param>
    /// <returns>True when a non-empty group is read.</returns>
    private static bool TryReadGroup(string text, ref int index, out string? value)
    {
        value = null;
        if (index >= text.Length || text[index] != '(')
        {
            return false;
        }

        var close = text.IndexOf(')', index + 1);
        if (close <= index + 1)
        {
            return false;
        }

        value = text[(index + 1)..close];
        index = close + 1;
        return true;
    }

    /// <summary>
    ///     Reads the optional arrow at the end of <c>repeat while</c>: nothing, <c>-&gt; label</c>, or
    ///     <c>-[#color]-&gt; label</c>.
    /// </summary>
    /// <param name="rest">The end of the statement.</param>
    /// <param name="location">The location for reports.</param>
    /// <param name="back">Receives the arrow.</param>
    /// <returns>True when the text is valid.</returns>
    private bool TryArrowEnd(string rest, PlantUmlLocation location, out ActivityLink back)
    {
        back = ActivityLink.None;
        var trimmed = rest.Trim();
        if (IsStereotypeEnd(trimmed))
        {
            return true;
        }

        if (!TryReadArrowHead(trimmed, location, out var link, out var after))
        {
            return false;
        }

        var label = after.Trim().TrimEnd(';').Trim();
        back = label.Length == 0 ? link : link with { Label = label };
        return true;
    }

    /// <summary>
    ///     Reads <c>backward :label;</c>, with optional arrow labels in parentheses before and after it.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="start">The index after the keyword.</param>
    /// <param name="incoming">The label of the arrow into the backward action.</param>
    private void ReadBackward(SourceStatement statement, int start, ActivityLink incoming)
    {
        var text = statement.Text;
        var i = start;
        SkipSpace(text, ref i);
        var semicolon = text.IndexOf(';', i);
        if (i >= text.Length || text[i] != ':' || semicolon < 0)
        {
            throw Error($"'{Shorten(text)}' is not a valid backward", statement.Location, "write for example 'backward :retry;'");
        }

        var label = text[(i + 1)..semicolon];
        var rest = text[(semicolon + 1)..].Trim();
        var outgoing = ActivityLink.None;
        if (rest.StartsWith("<<", StringComparison.Ordinal))
        {
            var close = rest.IndexOf(">>", StringComparison.Ordinal);
            rest = close < 0 ? rest : rest[(close + 2)..].Trim();
        }

        if (rest.Length > 0)
        {
            var j = 0;
            if (!TryReadGroup(rest, ref j, out var outLabel) || !IsEnd(rest[j..]))
            {
                throw Error($"'{Shorten(text)}' is not a valid backward", statement.Location, "write for example 'backward :retry;'");
            }

            outgoing = Label(StripArrowPrefix(outLabel!));
        }

        CheckLabel(label, statement.Location);
        Started();
        switch (Current)
        {
            case ActivityRepeat repeat:
                repeat.SetBackward(label, incoming, outgoing);
                repeat.BackwardLane = _lane;
                break;
            case ActivityWhile loop:
                loop.SetBackward(label, incoming, outgoing);
                break;
            default:
                throw Error("'backward' must be inside a repeat or while loop", statement.Location, "write it before 'repeat while' or 'endwhile'");
        }
    }

    /// <summary>
    ///     Reads <c>fork</c>, <c>fork again</c>, or <c>fork end</c>.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="rest">The text after <c>fork</c>.</param>
    private void ReadFork(SourceStatement statement, string rest)
    {
        var i = 0;
        SkipSpace(rest, ref i);
        if (TryWord(rest, ref i, "again"))
        {
            RequireEnd(statement, rest[i..], "fork again");
            if (Current is not ActivityFork fork)
            {
                throw Error("'fork again' must be inside a fork", statement.Location, "open the fork first with 'fork'");
            }

            fork.Last.OutLink = TakeNext();
            fork.ForkAgain(_lane);
            return;
        }

        if (TryWord(rest, ref i, "end"))
        {
            EndFork(statement, ActivityForkStyle.Fork, rest[i..]);
            return;
        }

        RequireEnd(statement, rest, "fork");
        Started();
        Open(new ActivityFork(_lane, TakeNext(), statement.Location));
    }

    /// <summary>
    ///     Reads <c>end fork</c> or <c>end merge</c> with an optional <c>{label}</c>.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="style">How the fork ends.</param>
    /// <param name="rest">The text after the keywords.</param>
    private void EndFork(SourceStatement statement, ActivityForkStyle style, string rest)
    {
        var trimmed = rest.Trim().TrimEnd(';').Trim();
        string? label = null;
        if (trimmed.StartsWith('{') && trimmed.EndsWith('}') && trimmed.Length > 2)
        {
            label = trimmed[1..^1];
        }
        else if (trimmed.Length > 0)
        {
            throw Error($"'{Shorten(statement.Text)}' is not a valid end of a fork", statement.Location, "write 'end fork', 'end fork {label}' or 'end merge'");
        }

        CheckLabel(label, statement.Location);
        if (Current is not ActivityFork fork)
        {
            throw Error("'end fork' must close a fork", statement.Location, "open the fork first with 'fork'");
        }

        fork.Style = style;
        fork.Label = label;
        fork.LaneOut = _lane;
        fork.Finished = true;
        fork.Last.OutLink = TakeNext();
        Close();
    }

    /// <summary>
    ///     Reads <c>split</c>, <c>split again</c>, or <c>split end</c>.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="rest">The text after <c>split</c>.</param>
    private void ReadSplit(SourceStatement statement, string rest)
    {
        var i = 0;
        SkipSpace(rest, ref i);
        if (TryWord(rest, ref i, "again"))
        {
            RequireEnd(statement, rest[i..], "split again");
            if (Current is not ActivitySplit split)
            {
                throw Error("'split again' must be inside a split", statement.Location, "open the split first with 'split'");
            }

            split.Last.OutLink = TakeNext();
            split.SplitAgain();
            return;
        }

        if (TryWord(rest, ref i, "end"))
        {
            RequireEnd(statement, rest[i..], "split end");
            EndSplit(statement);
            return;
        }

        RequireEnd(statement, rest, "split");
        Open(new ActivitySplit(_lane, TakeNext(), statement.Location));
    }

    /// <summary>
    ///     Reads <c>end split</c>.
    /// </summary>
    /// <param name="statement">The statement.</param>
    private void EndSplit(SourceStatement statement)
    {
        if (Current is not ActivitySplit split)
        {
            throw Error("'end split' must close a split", statement.Location, "open the split first with 'split'");
        }

        split.Last.OutLink = TakeNext();
        split.LaneOut = _lane;
        Close();
    }

    /// <summary>
    ///     Reads a group: <c>partition Name {</c>, <c>group Name {</c>, or <c>package</c>, <c>rectangle</c> and
    ///     <c>card</c>, which are drawn without their frame.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="keyword">The keyword.</param>
    /// <param name="rest">The text after the keyword.</param>
    private void ReadGroup(SourceStatement statement, string keyword, string rest)
    {
        var name = rest.Trim();
        if (name.EndsWith('{'))
        {
            name = name[..^1].TrimEnd();
        }

        // A color may be written before or after the name
        Rgba32? fill = null;
        var space = name.LastIndexOf(' ');
        if (space > 0 && name[(space + 1)..].StartsWith('#') && SkinParams.TryParseColor(name[(space + 1)..], out var after))
        {
            fill = after;
            name = name[..space].TrimEnd();
        }
        else if (name.StartsWith('#'))
        {
            var i = 0;
            fill = ReadLeadingColor(name, ref i, statement.Location);
            name = name[i..];
        }

        if (name.StartsWith("<<", StringComparison.Ordinal) || name.EndsWith(">>", StringComparison.Ordinal))
        {
            Unsupported("stereotypes of groups are not drawn", statement.Location);
            var open = name.IndexOf("<<", StringComparison.Ordinal);
            name = open >= 0 ? name[..open].TrimEnd() : name;
        }

        if (name.Length >= 2 && name[0] == '"' && name[^1] == '"')
        {
            name = name[1..^1];
        }

        if (name.Length == 0)
        {
            throw Error($"the {keyword} has no name", statement.Location, $"write for example '{keyword} Name {{'");
        }

        CheckLabel(name, statement.Location);
        var framed = keyword is "partition" or "group";
        if (!framed)
        {
            Unsupported($"'{keyword}' frames are not drawn; the instructions inside are drawn without the frame", statement.Location);
        }

        Started();

        // PlantUML keeps the pending arrow for the first instruction of the group as well
        Open(new ActivityGroup(_lane, name, fill, framed, _next, statement.Location));
    }

    /// <summary>
    ///     Closes a group (<c>}</c>, <c>end group</c>).
    /// </summary>
    /// <param name="statement">The statement.</param>
    private void CloseGroup(SourceStatement statement)
    {
        if (Current is not ActivityGroup)
        {
            throw Error($"'{Shorten(statement.Text)}' does not close a group", statement.Location, "open the group first with 'partition Name {'");
        }

        Close();
    }

    /// <summary>
    ///     Reads a statement that starts with a parenthesis: a connector <c>(A)</c>, or an elseif or backward with the
    ///     label of the arrow into it (<c>(label) elseif (...)</c>).
    /// </summary>
    /// <param name="index">The index of the statement.</param>
    /// <returns>The index of the last statement read.</returns>
    private int ReadParenthesisStatement(int index)
    {
        var statement = _statements[index];
        var text = statement.Text;
        var close = text.IndexOf(')', StringComparison.Ordinal);
        if (close > 0)
        {
            var i = close + 1;
            SkipSpace(text, ref i);
            var inLabel = Label(StripArrowPrefix(text[1..close]));
            if (TryWord(text, ref i, "elseif"))
            {
                ReadElseIf(statement, i, inLabel);
                return index;
            }

            var j = i;
            if (TryWord(text, ref j, "else"))
            {
                SkipSpace(text, ref j);
                if (TryWord(text, ref j, "if"))
                {
                    ReadElseIf(statement, j, inLabel);
                    return index;
                }
            }

            if (TryWord(text, ref i, "backward"))
            {
                ReadBackward(statement, i, inLabel);
                return index;
            }
        }

        ReadSpot(statement, 0, null);
        return index;
    }

    /// <summary>
    ///     Reads a connector <c>(A)</c>.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="start">The index of the opening parenthesis.</param>
    /// <param name="color">The fill, or null.</param>
    private void ReadSpot(SourceStatement statement, int start, Rgba32? color)
    {
        var text = statement.Text;
        var body = text[start..].TrimEnd(';').TrimEnd();
        if (body.Length < 3 || body[0] != '(' || body[^1] != ')' || body[1..^1].EnumerateRunes().Count() != 1 || char.IsWhiteSpace(body[1]))
        {
            throw Error($"'{Shorten(text)}' is not a valid connector", statement.Location, "write a connector as one character in parentheses, for example '(A)'");
        }

        Started();
        Add(new ActivitySpot(body[1..^1], color, _lane, TakeNext(), statement.Location));
    }

    /// <summary>
    ///     Reads an arrow: <c>-&gt;</c>, <c>-&gt; label;</c>, <c>-[#color,dashed]-&gt; label;</c>.
    /// </summary>
    /// <param name="statement">The statement.</param>
    private void ReadArrow(SourceStatement statement)
    {
        var text = statement.Text;
        if (!TryReadArrowHead(text, statement.Location, out var link, out var after))
        {
            throw Error($"'{Shorten(text)}' is not a valid arrow", statement.Location, "write '-> label;' or '-[#color]-> label;'");
        }

        var rest = after.Trim();
        string? label = null;
        if (rest.Length > 0)
        {
            if (!rest.EndsWith(';'))
            {
                throw Error($"'{Shorten(text)}' is not a valid arrow", statement.Location, "end the label of the arrow with ';'");
            }

            label = rest[..^1].Trim();
        }

        CheckLabel(label, statement.Location);

        // A color or style replaces the pending arrow (and colors the exit of a loop or if just before)
        if (link.HasRainbow)
        {
            if (LastOf(Current) is ActivityWhile loop)
            {
                loop.OutLink = loop.OutLink with { Color = link.Color, Style = link.Style };
            }
            else if (LastOf(Current) is ActivityIf branches)
            {
                branches.OutLink = link;
            }

            _next = link;
        }

        if (label is { Length: > 0 })
        {
            if (Current is ActivityWhile body && body.Body.Last is null)
            {
                body.Yes = label;
                return;
            }

            _next = _next with { Label = label, Location = statement.Location };
        }
    }

    /// <summary>
    ///     Reads the head of an arrow: <c>-&gt;</c> or <c>-[options]-&gt;</c>.
    /// </summary>
    /// <param name="text">The text, which starts with the arrow.</param>
    /// <param name="location">The location for reports.</param>
    /// <param name="link">Receives the arrow's color and style.</param>
    /// <param name="after">Receives the text after the arrow.</param>
    /// <returns>True when the text starts with an arrow.</returns>
    private bool TryReadArrowHead(string text, PlantUmlLocation location, out ActivityLink link, out string after)
    {
        link = ActivityLink.None with { Location = location };
        after = string.Empty;
        if (text.StartsWith("->", StringComparison.Ordinal))
        {
            after = text[2..];
            return true;
        }

        if (!text.StartsWith("-[", StringComparison.Ordinal))
        {
            return false;
        }

        var close = text.IndexOf("]->", StringComparison.Ordinal);
        if (close < 0)
        {
            return false;
        }

        link = ReadArrowOptions(text[2..close], location);
        after = text[(close + 3)..];
        return true;
    }

    /// <summary>
    ///     Reads the options of an arrow: colors and styles separated by commas or semicolons.
    /// </summary>
    /// <param name="options">The text between the brackets.</param>
    /// <param name="location">The location for reports.</param>
    /// <returns>The arrow's color and style.</returns>
    private ActivityLink ReadArrowOptions(string options, PlantUmlLocation location)
    {
        Rgba32? color = null;
        var style = ActivityLinkStyle.Normal;
        foreach (var option in options.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var lower = option.ToLowerInvariant();
            switch (lower)
            {
                case "dashed":
                    style = ActivityLinkStyle.Dashed;
                    continue;
                case "dotted":
                    style = ActivityLinkStyle.Dotted;
                    continue;
                case "bold":
                    style = ActivityLinkStyle.Bold;
                    continue;
                case "hidden":
                    style = ActivityLinkStyle.Hidden;
                    continue;
                case "plain":
                    continue;
            }

            if (SkinParams.TryParseColor(option, out var parsed))
            {
                if (color is not null)
                {
                    Unsupported("arrows with several colors are drawn with their first color", location);
                    continue;
                }

                color = parsed;
                continue;
            }

            Unsupported($"the arrow option '{option}' is not drawn", location);
        }

        return new ActivityLink(null, color, style, location);
    }

    /// <summary>
    ///     Reads a swimlane: <c>|Name|</c>, <c>|#color|Name|</c>, or <c>|Name| label</c>.
    /// </summary>
    /// <param name="statement">The statement.</param>
    private void ReadSwimlane(SourceStatement statement)
    {
        var parts = statement.Text[1..].Split('|');
        Rgba32? color = null;
        var at = 0;
        if (parts.Length >= 3 && parts[0].StartsWith('#'))
        {
            if (!SkinParams.TryParseColor(parts[0], out var parsed))
            {
                throw Error($"'{parts[0]}' is not a color", statement.Location, "write a color such as #LightBlue");
            }

            color = parsed;
            at = 1;
        }

        if (parts.Length < at + 2 || parts[at].Trim().Length == 0)
        {
            throw Error($"'{Shorten(statement.Text)}' is not a valid swimlane", statement.Location, "write a swimlane as '|Name|' or '|#color|Name|'");
        }

        var label = string.Join('|', parts[(at + 1)..]).Trim();
        SwitchLane(statement, parts[at].Trim(), color, label.Length == 0 ? null : label);
    }

    /// <summary>
    ///     Reads <c>swimlane Name</c> or <c>swimlane Name as Label</c>.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="rest">The text after the keyword.</param>
    private void ReadSwimlaneKeyword(SourceStatement statement, string rest)
    {
        var text = rest.Trim();
        var i = 0;
        var color = ReadLeadingColor(text, ref i, statement.Location);
        text = text[i..];
        string? label = null;
        var asIndex = text.IndexOf(" as ", StringComparison.OrdinalIgnoreCase);
        if (asIndex > 0)
        {
            label = text[(asIndex + 4)..].Trim();
            text = text[..asIndex].Trim();
        }

        if (text.Length == 0)
        {
            throw Error("the swimlane has no name", statement.Location, "write for example 'swimlane Kitchen'");
        }

        SwitchLane(statement, text, color, label);
    }

    /// <summary>
    ///     Makes a swimlane current, declaring it on first use (PlantUML allows swimlanes only before the first
    ///     instruction or when the diagram started with one).
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="name">The name.</param>
    /// <param name="color">The fill, or null.</param>
    /// <param name="label">The title, or null for the name.</param>
    private void SwitchLane(SourceStatement statement, string name, Rgba32? color, string? label)
    {
        _lanesAllowed ??= true;
        if (_lanesAllowed == false)
        {
            throw Error("a swimlane must be declared at the start of the diagram", statement.Location, "declare the first swimlane before the first instruction");
        }

        CheckLabel(name, statement.Location);
        CheckLabel(label, statement.Location);
        var lane = _lanes.Find(l => l.Name == name);
        if (lane is null)
        {
            if (_lanes.Count >= _limits.MaxNodes)
            {
                throw new PlantUmlSyntaxException(
                    PlantUmlDiagnosticCodes.LimitExceeded,
                    string.Create(CultureInfo.InvariantCulture, $"the diagram has more than {_limits.MaxNodes} swimlanes (PlantUmlLimits.MaxNodes)"),
                    statement.Location,
                    "split the diagram, or raise PlantUmlLimits.MaxNodes when the source is trusted");
            }

            lane = new ActivityLane(name, _lanes.Count, statement.Location);
            _lanes.Add(lane);
        }

        lane.Fill = color ?? lane.Fill;
        if (label is not null)
        {
            lane.Display = label;
        }

        _lane = lane;
    }

    /// <summary>
    ///     Reads a note: <c>note left|right [#color] : text</c>, or the same without a colon and the text on the
    ///     following lines up to <c>end note</c>.
    /// </summary>
    /// <param name="index">The index of the statement.</param>
    /// <param name="floating">Whether the note is a floating note.</param>
    /// <param name="start">The index after the <c>note</c> keyword.</param>
    /// <returns>The index of the last statement read.</returns>
    private int ReadNote(int index, bool floating, int start)
    {
        var statement = _statements[index];
        var text = statement.Text;
        var i = start;
        SkipSpace(text, ref i);
        var position = ActivityNotePosition.Left;
        if (TryWord(text, ref i, "right"))
        {
            position = ActivityNotePosition.Right;
        }
        else
        {
            TryWord(text, ref i, "left");
        }

        SkipSpace(text, ref i);
        var fill = ReadLeadingColor(text, ref i, statement.Location);
        if (text.AsSpan(i).StartsWith("<<", StringComparison.Ordinal))
        {
            var close = text.IndexOf(">>", i, StringComparison.Ordinal);
            if (close > 0)
            {
                Unsupported("stereotypes of notes are not drawn", statement.Location);
                i = close + 2;
                SkipSpace(text, ref i);
            }
        }

        string note;
        if (i < text.Length && text[i] == ':')
        {
            note = text[(i + 1)..].Trim();
        }
        else if (i == text.Length)
        {
            var lines = new List<string>();
            var j = index + 1;
            while (j < _statements.Count && !DiagramDetector.IsCloser(_statements[j].Text, "end note"))
            {
                lines.Add(_statements[j].Text);
                j++;
            }

            if (j >= _statements.Count)
            {
                throw Error("the note is not closed", statement.Location, "close it with 'end note'");
            }

            note = string.Join('\n', lines);
            index = j;
        }
        else
        {
            throw Error($"'{Shorten(text[i..])}' is not expected in a note", statement.Location, "write the text after a colon, or on the next lines up to 'end note'");
        }

        CheckLabel(note, statement.Location);
        if (!Current.AddNote(new ActivityNote(note, position, floating, fill, _lane, statement.Location)))
        {
            throw Error("a note cannot be added here", statement.Location, "write the note after an action");
        }

        return index;
    }

    /// <summary>
    ///     Reads a floating note: <c>floating note left|right ...</c>.
    /// </summary>
    /// <param name="index">The index of the statement.</param>
    /// <returns>The index of the last statement read.</returns>
    private int ReadFloatingNote(int index)
    {
        var statement = _statements[index];
        var text = statement.Text;
        var i = "floating".Length;
        SkipSpace(text, ref i);
        if (!TryWord(text, ref i, "note"))
        {
            throw Error($"'{Shorten(text)}' is not a valid note", statement.Location, "write for example 'floating note right: text'");
        }

        return ReadNote(index, true, i);
    }

    /// <summary>
    ///     Skips a <c>switch</c> block up to its <c>endswitch</c> and reports it.
    /// </summary>
    /// <param name="index">The index of the statement.</param>
    /// <returns>The index of the last statement read.</returns>
    private int SkipSwitch(int index)
    {
        var statement = _statements[index];
        Unsupported("'switch' blocks are not drawn; the flow continues after 'endswitch'", statement.Location);
        var depth = 0;
        for (var i = index; i < _statements.Count; i++)
        {
            var word = DiagramDetector.FirstWord(_statements[i].Text).ToLowerInvariant();
            var compact = string.Join(' ', _statements[i].Text.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
            if (word == "switch")
            {
                depth++;
            }
            else if (word == "endswitch" || compact.StartsWith("end switch", StringComparison.Ordinal))
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        throw Error("the switch is not closed", statement.Location, "close it with 'endswitch'");
    }

    /// <summary>
    ///     Creates the rendering of an arrow with only a label.
    /// </summary>
    /// <param name="label">The label, or null.</param>
    /// <returns>The rendering.</returns>
    private static ActivityLink Label(string? label) =>
        label is { Length: > 0 } ? ActivityLink.None with { Label = label } : ActivityLink.None;

    /// <summary>
    ///     Reads the lines of a text block up to its closing keyword.
    /// </summary>
    /// <param name="index">The index of the opening statement.</param>
    /// <param name="closer">The closing keyword.</param>
    /// <param name="what">What the block is, for the error.</param>
    /// <returns>The text (lines joined with line feeds) and the index of the closing statement.</returns>
    private (string Text, int Index) ReadBlock(int index, string closer, string what)
    {
        var lines = new List<string>();
        var i = index + 1;
        while (i < _statements.Count && !DiagramDetector.IsCloser(_statements[i].Text, closer))
        {
            lines.Add(_statements[i].Text);
            i++;
        }

        if (i >= _statements.Count)
        {
            throw Error($"{what} is not closed", _statements[index].Location, $"close it with '{closer}'");
        }

        return (string.Join('\n', lines), i);
    }

    /// <summary>
    ///     Reads a page text: <c>title</c>, <c>header</c>, <c>footer</c>, <c>caption</c> (on one line or as a block),
    ///     <c>left|center|right header|footer</c>, or <c>legend [left|right|center]</c> ... <c>endlegend</c>.
    /// </summary>
    /// <param name="index">The index of the statement.</param>
    /// <param name="word">The first word in lower case.</param>
    /// <returns>The index of the last statement read.</returns>
    private int ReadPageText(int index, string word)
    {
        var statement = _statements[index];
        var text = statement.Text;
        var i = word.Length;
        TextAlign? align = null;
        if (word is "left" or "right" or "center")
        {
            align = ParseAlign(word);
            SkipSpace(text, ref i);
            if (TryWord(text, ref i, "header"))
            {
                word = "header";
            }
            else if (TryWord(text, ref i, "footer"))
            {
                word = "footer";
            }
            else
            {
                throw Error($"'{word}' must be followed by 'header' or 'footer'", statement.Location, $"write for example '{word} header text'");
            }
        }

        string value;
        var rest = text[i..].Trim();
        if (word == "legend")
        {
            foreach (var alignWord in rest.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                align = alignWord.ToLowerInvariant() is "left" or "right" or "center" ? ParseAlign(alignWord) : align;
            }

            (value, index) = ReadBlock(index, "endlegend", "the legend");
        }
        else if (rest.Length == 0)
        {
            (value, index) = ReadBlock(index, "end" + word, $"the {word}");
        }
        else
        {
            value = rest.StartsWith(':') ? rest[1..].TrimStart() : rest;
        }

        CheckLabel(value, statement.Location);
        _page = word switch
        {
            "title" => _page with { Title = new PageText(value, TextAlign.Center, statement.Location) },
            "header" => _page with { Header = new PageText(value, align ?? TextAlign.Right, statement.Location) },
            "footer" => _page with { Footer = new PageText(value, align ?? TextAlign.Center, statement.Location) },
            "caption" => _page with { Caption = new PageText(value, TextAlign.Center, statement.Location) },
            _ => _page with { Legend = new PageText(value, align ?? TextAlign.Center, statement.Location) },
        };
        return index;
    }

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

    /// <summary>
    ///     Reads <c>scale N</c> or <c>scale N/M</c>; other forms are not drawn.
    /// </summary>
    /// <param name="statement">The statement.</param>
    private void ReadScale(SourceStatement statement)
    {
        var rest = statement.Text["scale".Length..].Trim();
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

        if (ok && value > 0 && value <= MaxScale)
        {
            _scale = value;
            return;
        }

        Unsupported($"'{Shorten(statement.Text)}' is not drawn; only 'scale N' from 0 to {MaxScale.ToString(CultureInfo.InvariantCulture)} is", statement.Location);
    }
}
