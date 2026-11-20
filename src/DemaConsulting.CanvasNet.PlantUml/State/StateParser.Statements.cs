// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Styling;
using DemaConsulting.CanvasNet.PlantUml.Svek;
using DemaConsulting.CanvasNet.PlantUml.Syntax;

namespace DemaConsulting.CanvasNet.PlantUml.State;

/// <summary>
///     The statements of <see cref="StateParser"/>: declarations, descriptions, transitions, composite states and their
///     regions, notes, and the common statements.
/// </summary>
internal sealed partial class StateParser
{
    /// <summary>The stereotypes that make a state a pseudo-state (PlantUML's <c>Stereogroup.getLeafType</c>).</summary>
    private static readonly Dictionary<string, StateKind> PseudoStereotypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["choice"] = StateKind.Choice,
        ["fork"] = StateKind.ForkJoin,
        ["join"] = StateKind.ForkJoin,
        ["start"] = StateKind.Start,
        ["junction"] = StateKind.Junction,
        ["end"] = StateKind.End,
        ["history"] = StateKind.History,
        ["history*"] = StateKind.DeepHistory,
    };

    /// <summary>The stereotypes that PlantUML draws on the border of a composite state or as SDL symbols; read, not drawn.</summary>
    private static readonly HashSet<string> BorderStereotypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "entryPoint", "exitPoint", "inputPin", "outputPin", "expansionInput", "expansionOutput", "sdlreceive",
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

        if (text == "}" || IsEndState(text))
        {
            Close(statement);
            return index;
        }

        if (IsConcurrentSeparator(text))
        {
            ReadConcurrentSeparator(statement);
            return index;
        }

        if (SvekSyntax.TryReadPageText(_statements, ref index, ref _page, _limits))
        {
            return index;
        }

        if (SvekSyntax.TryReadDirection(text, out var direction))
        {
            _rankDir = direction;
            return index;
        }

        var word = SvekSyntax.FirstWord(text);
        if (ReadKeywordStatement(index, word) is { } last)
        {
            return last;
        }

        if (SvekSyntax.TryReadLink(statement, out var link))
        {
            ReadTransition(statement, link);
            return index;
        }

        if (TryReadDescription(statement))
        {
            return index;
        }

        throw Error(
            $"'{Shorten(text)}' is not a state diagram statement",
            statement.Location,
            "declare a state with 'state Name', a description with 'Name : text', and a transition with 'A --> B : event'");
    }

    /// <summary>
    ///     Reads a statement that starts with a keyword of the state diagram.
    /// </summary>
    /// <param name="index">The index of the statement.</param>
    /// <param name="word">The first word, in lower case.</param>
    /// <returns>The index of the last statement read, or null when the statement does not start with such a keyword.</returns>
    private int? ReadKeywordStatement(int index, string word)
    {
        var statement = _statements[index];
        var text = statement.Text;
        switch (word)
        {
            case "state" when text.Length > 5 && (char.IsWhiteSpace(text[5]) || SvekSyntax.IsQuote(text[5])):
                ReadState(statement);
                return index;
            case "note":
                return ReadNote(index);
            case "hide" or "show" when SvekSyntax.TryReadHideShow(statement, out var hideShow):
                if (hideShow.What == "empty description")
                {
                    _hideEmptyDescription = !hideShow.Show;
                }
                else
                {
                    Unsupported($"'{Shorten(text)}' is not applied; only 'hide empty description' is", statement.Location);
                }

                return index;
            case "scale":
                if (SvekSyntax.ReadScale(text) is { } scale)
                {
                    _scale = scale;
                }
                else
                {
                    Unsupported("this 'scale' form is not drawn; the diagram is drawn at its natural size", statement.Location);
                }

                return index;
            case "json" or "map" when text.TrimEnd().EndsWith('{'):
                return SkipBlock(index, word);
            case "remove" or "restore" or "skin" or "url" or "newpage" or "mainframe" or "allowmixing" or "allow_mixing" or "set":
                Unsupported($"'{word}' is not drawn", statement.Location);
                return index;
        }

        return null;
    }

    /// <summary>
    ///     Decides whether a statement closes a composite state with <c>end state</c> (or <c>endstate</c>).
    /// </summary>
    /// <param name="text">The statement.</param>
    /// <returns>True when it does.</returns>
    private static bool IsEndState(string text)
    {
        var compact = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
        return compact is "end state" or "endstate";
    }

    /// <summary>
    ///     Decides whether a statement is a concurrent separator: two or more <c>-</c>, or two or more <c>|</c>.
    /// </summary>
    /// <param name="text">The statement.</param>
    /// <returns>True when it is.</returns>
    internal static bool IsConcurrentSeparator(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Length >= 2 && (text.All(c => c == '-') || text.All(c => c == '|'));
    }

    /// <summary>
    ///     Skips a <c>json</c> or <c>map</c> block, counting its braces, and reports it.
    /// </summary>
    /// <param name="index">The index of the statement.</param>
    /// <param name="word">The keyword.</param>
    /// <returns>The index of the last statement of the block.</returns>
    private int SkipBlock(int index, string word)
    {
        var statement = _statements[index];
        Unsupported($"'{word}' blocks are not drawn in a state diagram", statement.Location);
        var depth = statement.Text.Count(c => c == '{') - statement.Text.Count(c => c == '}');
        while (depth > 0 && index + 1 < _statements.Count)
        {
            index++;
            depth += _statements[index].Text.Count(c => c == '{') - _statements[index].Text.Count(c => c == '}');
        }

        if (depth > 0)
        {
            throw Error($"the {word} block is not closed", statement.Location, "close it with '}'");
        }

        return index;
    }

    /// <summary>
    ///     Closes the innermost composite state.
    /// </summary>
    /// <param name="statement">The closing statement.</param>
    /// <exception cref="PlantUmlSyntaxException">Thrown when no composite state is open.</exception>
    private void Close(SourceStatement statement)
    {
        if (_open.Count == 0)
        {
            throw Error($"'{statement.Text}' closes no composite state", statement.Location, "remove it, or open a composite state with 'state Name {'");
        }

        _open.RemoveAt(_open.Count - 1);
    }

    /// <summary>
    ///     Reads a concurrent separator: a new region of the innermost composite state (stacked after <c>--</c>, side by
    ///     side after <c>||</c>, as the first separator of the composite state says).
    /// </summary>
    /// <param name="statement">The statement.</param>
    private void ReadConcurrentSeparator(SourceStatement statement)
    {
        if (_open.Count == 0)
        {
            Unsupported("a concurrent separator outside a composite state is not drawn", statement.Location);
            return;
        }

        var composite = _open[^1].Composite;
        if (composite.Concurrency == ClusterConcurrency.None)
        {
            composite.Concurrency = statement.Text[0] == '-' ? ClusterConcurrency.Stacked : ClusterConcurrency.SideBySide;
        }

        _concurrentCount++;
        var prefix = composite.Id + ".CONC" + _concurrentCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        composite.Regions.Add(new StateRegion(prefix, composite, composite.Regions.Count));
    }

    /// <summary>
    ///     Reads a state declaration: <c>state Name</c>, <c>state "Display" as Name</c>, <c>state Name as "Display"</c>,
    ///     or <c>state "Display"</c>, followed by stereotypes, colors (<c>#back</c>, <c>##line</c>), and a block opener
    ///     (<c>{</c> or <c>begin</c>) or a description after a colon.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the declaration cannot be read.</exception>
    private void ReadState(SourceStatement statement)
    {
        var text = statement.Text;
        var i = 5;
        SvekSyntax.SkipSpaces(text, ref i);
        var (name, display) = ReadStateName(statement, ref i);
        CheckLabel(display, statement.Location);
        var element = Resolve(name, statement.Location);
        if (display is not null)
        {
            element.Display = display;
        }

        _lastElement = element;

        // Stereotypes: a pseudo-state kind, a border symbol (not drawn), or a label (not drawn by PlantUML either)
        var stereotypes = SvekSyntax.ReadStereotypes(text, ref i);
        foreach (var stereotype in stereotypes)
        {
            CheckLabel(stereotype, statement.Location);
        }

        if (stereotypes.Count > 0)
        {
            ApplyStereotype(element, stereotypes[0], statement);
        }

        ReadStateColors(statement, ref i, element);

        // What follows: nothing, a block opener, or a description
        var rest = text[i..].Trim();
        if (rest.Length == 0)
        {
            return;
        }

        if (rest == "{" || rest.Equals("begin", StringComparison.OrdinalIgnoreCase))
        {
            EnsureComposite(element, statement.Location);
            _open.Add((element, statement.Location));
            return;
        }

        if (rest.StartsWith(':'))
        {
            AddDescription(element, rest[1..].Trim(), statement.Location);
            return;
        }

        throw Error($"'{Shorten(rest)}' cannot follow the state '{element.Display}'", statement.Locate(i), "write a description after a colon, or open a composite state with '{'");
    }

    /// <summary>
    ///     Reads the name of a declared state.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="i">The index of the name; advanced past it and the white space after it.</param>
    /// <returns>The name that identifies the state and its display (or null for the name's).</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown when there is no name.</exception>
    private static (string Name, string? Display) ReadStateName(SourceStatement statement, ref int i)
    {
        var text = statement.Text;
        if (!SvekSyntax.TryReadName(text, ref i, out var first) || first.Kind is not (SvekNameKind.Plain or SvekNameKind.Quoted))
        {
            throw Error("the state needs a name", statement.Locate(i), "write for example 'state Idle' or 'state \"Long name\" as L'");
        }

        var at = i;
        SvekSyntax.SkipSpaces(text, ref at);
        string? display = first.Kind == SvekNameKind.Quoted ? first.Text : null;
        var name = first.Text;
        if (SvekSyntax.TryKeyword(text, ref at, "as"))
        {
            SvekSyntax.SkipSpaces(text, ref at);
            if (!SvekSyntax.TryReadName(text, ref at, out var second) || second.Kind is not (SvekNameKind.Plain or SvekNameKind.Quoted))
            {
                throw Error("'as' needs a name", statement.Locate(at), "write for example 'state \"Long name\" as L'");
            }

            // "Display" as Name, or Name as "Display"
            (name, display) = first.Kind == SvekNameKind.Quoted && second.Kind == SvekNameKind.Plain
                ? (second.Text, first.Text)
                : (first.Text, second.Text);
            i = at;
        }

        SvekSyntax.SkipSpaces(text, ref i);
        return (name, display);
    }

    /// <summary>
    ///     Applies the first stereotype of a declaration: a pseudo-state kind, or a border symbol that is reported.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="stereotype">The stereotype.</param>
    /// <param name="statement">The statement.</param>
    /// <exception cref="PlantUmlSyntaxException">Thrown when a composite state is made a pseudo-state.</exception>
    private void ApplyStereotype(StateElement element, string stereotype, SourceStatement statement)
    {
        if (PseudoStereotypes.TryGetValue(stereotype, out var kind))
        {
            if (element.IsComposite)
            {
                throw Error($"the composite state '{element.Display}' cannot be a {stereotype}", statement.Location, "give the pseudo-state another name");
            }

            element.Kind = kind;
            return;
        }

        if (BorderStereotypes.Contains(stereotype))
        {
            Unsupported($"the <<{stereotype}>> symbol is not drawn; '{element.Display}' is drawn as a state", statement.Location);
        }
    }

    /// <summary>
    ///     Reads the colors of a declaration: <c>#back</c> (or <c>#back;line:color</c>) and <c>##line</c> (a line style in
    ///     brackets, <c>##[dashed]</c>, is reported, not drawn).
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="i">The index; advanced past the colors and the white space after them.</param>
    /// <param name="element">The element that receives them.</param>
    /// <exception cref="PlantUmlSyntaxException">Thrown when a color cannot be read.</exception>
    private void ReadStateColors(SourceStatement statement, ref int i, StateElement element)
    {
        var text = statement.Text;
        while (i < text.Length && text[i] == '#')
        {
            var start = i;
            var end = i;
            while (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] != '{'
                && (text[end] != ':' || text.IndexOf(';', start, end - start) >= 0))
            {
                end++;
            }

            var token = text[start..end];
            if (token.StartsWith("##", StringComparison.Ordinal))
            {
                var line = token[2..];
                if (line.StartsWith('['))
                {
                    var close = line.IndexOf(']', StringComparison.Ordinal);
                    Unsupported("the line style of a state is not drawn", statement.Locate(start));
                    line = close < 0 ? string.Empty : line[(close + 1)..];
                }

                if (line.Length > 0)
                {
                    element.Line = Color(line, statement, start);
                }
            }
            else
            {
                foreach (var part in token[1..].Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    var colon = part.IndexOf(':', StringComparison.Ordinal);
                    var key = colon < 0 ? "back" : part[..colon].ToLowerInvariant();
                    var value = colon < 0 ? part : part[(colon + 1)..];
                    switch (key)
                    {
                        case "back":
                            element.Fill = Color(value, statement, start);
                            break;
                        case "line":
                            element.Line = Color(value, statement, start);
                            break;
                        default:
                            Unsupported($"'{part}' is not drawn", statement.Locate(start));
                            break;
                    }
                }
            }

            i = end;
            SvekSyntax.SkipSpaces(text, ref i);
        }
    }

    /// <summary>
    ///     Reads one color of a color list.
    /// </summary>
    /// <param name="value">The color, with or without its hash.</param>
    /// <param name="statement">The statement.</param>
    /// <param name="at">The index of the colors in the statement.</param>
    /// <returns>The color.</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the value is not a color.</exception>
    private static Rgba32 Color(string value, SourceStatement statement, int at)
    {
        var token = value.StartsWith('#') ? value : "#" + value;
        if (!SkinParams.TryParseColor(token, out var color))
        {
            throw Error($"'{value}' is not a color", statement.Locate(at), "write a color such as #LightBlue or #AABBCC");
        }

        return color;
    }

    /// <summary>
    ///     Adds a description line to a state.
    /// </summary>
    /// <param name="element">The state.</param>
    /// <param name="line">The line.</param>
    /// <param name="location">The location.</param>
    private void AddDescription(StateElement element, string line, PlantUmlLocation location)
    {
        CheckLabel(line, location);
        if (element.Kind != StateKind.State)
        {
            Unsupported($"the description of the pseudo-state '{NameOf(element)}' is not drawn", location);
            return;
        }

        element.Descriptions.Add(line);
    }

    /// <summary>
    ///     Reads a description statement: <c>Name : text</c> (or <c>"Name" : text</c>), which adds a line to the state
    ///     (created when needed).
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <returns>True when the statement is a description.</returns>
    private bool TryReadDescription(SourceStatement statement)
    {
        var text = statement.Text;
        var i = 0;
        if (!SvekSyntax.TryReadName(text, ref i, out var name) || name.Kind is not (SvekNameKind.Plain or SvekNameKind.Quoted))
        {
            return false;
        }

        SvekSyntax.SkipSpaces(text, ref i);
        if (i >= text.Length || text[i] != ':')
        {
            return false;
        }

        var element = Resolve(name.Text, statement.Location);
        _lastElement = element;
        AddDescription(element, text[(i + 1)..].Trim(), statement.Location);
        return true;
    }

    /// <summary>
    ///     Reads a transition: checks its arrow (a body of dashes with an arrow head at one end), resolves its ends
    ///     (<c>[*]</c> as the start or the end of the current region, the history pseudo-states, a floating note, or a
    ///     state), and adds it, reporting the parts that are not drawn.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="read">The link statement.</param>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the arrow is not a transition arrow or a limit is exceeded.</exception>
    private void ReadTransition(SourceStatement statement, SvekLinkStatement read)
    {
        var arrow = read.Arrow;
        var location = read.Location;
        var tail = arrow.TailDecor;
        var head = arrow.HeadDecor;
        var forward = tail is LinkDecor.None or LinkDecor.NotNavigable && head == LinkDecor.Arrow;
        var backward = tail == LinkDecor.Arrow && head == LinkDecor.None;
        if ((!forward && !backward) || arrow.Dotted || arrow.Doubled || arrow.Middle is not null || read.TailRole is not null
            || read.HeadRole is not null || read.TailQualifier is not null || read.HeadQualifier is not null)
        {
            throw Error(
                $"'{arrow.Text}' is not a transition arrow",
                location,
                "write a transition as 'A --> B' (or 'A -> B', 'A -down-> B', 'A -[dashed]-> B', 'B <-- A')");
        }

        if (tail == LinkDecor.NotNavigable)
        {
            Unsupported("the cross at the start of a transition is not drawn", location);
            tail = LinkDecor.None;
        }

        if (arrow.NoRank)
        {
            Unsupported("'norank' is not applied; the transition is laid out like the others", location);
        }

        if (read.Stereotypes.Count > 0)
        {
            Unsupported("stereotypes of a transition are not drawn", location);
        }

        var label = RawLabel(statement.Text, read);
        CheckLabel(label, location);
        var from = ResolveEnd(read.From, source: true, statement);
        var to = ResolveEnd(read.To, source: false, statement);
        if (_transitions.Count >= _limits.MaxEdges)
        {
            throw new PlantUmlSyntaxException(
                PlantUmlDiagnosticCodes.LimitExceeded,
                string.Create(System.Globalization.CultureInfo.InvariantCulture, $"the diagram has more than {_limits.MaxEdges} transitions (PlantUmlLimits.MaxEdges)"),
                location,
                "split the diagram, or raise PlantUmlLimits.MaxEdges when the source is trusted");
        }

        var transition = new StateTransition("link#" + (_transitions.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), from, to, location)
        {
            TailDecor = tail,
            HeadDecor = head,
            Style = arrow.ResolveStyle(doubledIsBold: false),
            Thickness = arrow.Thickness,
            Color = read.Color ?? arrow.Color,
            Length = arrow.Length,
            Direction = arrow.Direction,
            Label = label,
        };
        _transitions.Add(transition);
        _lastTransition = transition;
        if (to is StateElement target)
        {
            _lastElement = target;
        }
    }

    /// <summary>
    ///     Gets the label of a transition as written after its colon (PlantUML's state transitions keep quotes and
    ///     direction marks as text).
    /// </summary>
    /// <param name="text">The statement.</param>
    /// <param name="read">The link statement.</param>
    /// <returns>The trimmed label, or null when there is none.</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown when a quoted label is written beside the arrow.</exception>
    private static string? RawLabel(string text, SvekLinkStatement read)
    {
        var hasLabel = read.Label is not null || read.LabelArrow != LinkLabelArrow.None;
        var arrowAt = text.IndexOf(read.Arrow.Text, StringComparison.Ordinal);
        var colon = arrowAt < 0 ? -1 : text.IndexOf(':', arrowAt + read.Arrow.Text.Length);
        if (!hasLabel && colon < 0 && (read.TailLabel is not null || read.HeadLabel is not null))
        {
            throw Error("a transition has no labels at its ends", read.Location, "write the label after a colon: 'A --> B : event'");
        }

        if (colon < 0)
        {
            return null;
        }

        var label = text[(colon + 1)..].Trim();
        return label.Length == 0 ? null : label;
    }

    /// <summary>
    ///     Resolves an end of a transition.
    /// </summary>
    /// <param name="name">The name as written.</param>
    /// <param name="source">Whether the end is the source (<c>[*]</c> is then the start, else the end).</param>
    /// <param name="statement">The statement.</param>
    /// <returns>The node.</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the name is an element of another diagram type.</exception>
    private StateNode ResolveEnd(SvekName name, bool source, SourceStatement statement)
    {
        var location = statement.Location;
        switch (name.Kind)
        {
            case SvekNameKind.Star:
                return Pseudo(source ? StateKind.Start : StateKind.End, CurrentRegion, location);
            case SvekNameKind.History or SvekNameKind.DeepHistory:
                {
                    var kind = name.Kind == SvekNameKind.History ? StateKind.History : StateKind.DeepHistory;
                    if (name.Text.Length == 0)
                    {
                        return Pseudo(kind, CurrentRegion, location);
                    }

                    // X[H]: the history of the composite state X (made composite when it is not)
                    var owner = Resolve(name.Text, location);
                    EnsureComposite(owner, location);
                    return Pseudo(kind, owner.Regions[0], location);
                }

            case SvekNameKind.Plain or SvekNameKind.Quoted:
                if (_noteByAlias.TryGetValue(name.Text, out var note))
                {
                    return note;
                }

                return Resolve(name.Text, location);
            default:
                throw Error($"'{name.Text}' is not a state", location, "write a state name, [*], [H], or [H*]");
        }
    }

    /// <summary>
    ///     Reads a note statement and attaches the note to its state, to the last transition, or keeps it floating.
    /// </summary>
    /// <param name="index">The index of the statement.</param>
    /// <returns>The index of the last statement read.</returns>
    private int ReadNote(int index)
    {
        var statement = _statements[index];
        if (!SvekSyntax.TryReadNote(_statements, ref index, out var read))
        {
            throw Error($"'{Shorten(statement.Text)}' is not a note", statement.Location, "write 'note left of A : text', 'note on link : text', or 'note \"text\" as N'");
        }

        CheckLabel(read.Text, statement.Location);
        CountNode(statement.Location);
        if (read.IsFloating)
        {
            var alias = read.Alias ?? NextNoteId();
            if (_noteByAlias.ContainsKey(alias) || Find(alias) is not null)
            {
                throw Error($"the name '{alias}' is already used", statement.Location, "give the note another name");
            }

            var floating = new StateNote(alias, read.Text, read.Location) { Fill = read.Fill, Region = CurrentRegion };
            _noteByAlias[alias] = floating;
            _notes.Add(floating);
            return index;
        }

        var note = new StateNote(NextNoteId(), read.Text, read.Location) { Fill = read.Fill, Position = read.Position ?? NotePosition.Right };
        if (read.OnLink)
        {
            note.OnLink = _lastTransition ?? throw Error("'note on link' needs a transition before it", statement.Location, "write the note after the transition it describes");
        }
        else if (read.Target is { } target)
        {
            if (target.Kind is not (SvekNameKind.Plain or SvekNameKind.Quoted) || _noteByAlias.ContainsKey(target.Text))
            {
                throw Error($"'{target.Text}' is not a state; a note is attached to a state", statement.Location, "attach the note to a state");
            }

            note.Target = Resolve(target.Text, statement.Location);
        }
        else
        {
            note.Target = _lastElement ?? throw Error("the note needs a state before it", statement.Location, "write 'note left of A : text'");
        }

        _notes.Add(note);
        return index;
    }
}
