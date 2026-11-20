// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Styling;
using DemaConsulting.CanvasNet.PlantUml.Svek;
using DemaConsulting.CanvasNet.PlantUml.Syntax;

namespace DemaConsulting.CanvasNet.PlantUml.Description;

/// <summary>
///     The statements of <see cref="DescriptionParser"/>: declarations, containers, ports, links, notes, and the common
///     statements.
/// </summary>
internal sealed partial class DescriptionParser
{
    /// <summary>The keywords that declare an element, with their symbols (PlantUML's <c>ALL_TYPES</c>).</summary>
    private static readonly Dictionary<string, DescriptionSymbol> SymbolKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["component"] = DescriptionSymbol.Component,
        ["interface"] = DescriptionSymbol.Interface,
        ["actor"] = DescriptionSymbol.Actor,
        ["actor/"] = DescriptionSymbol.BusinessActor,
        ["usecase"] = DescriptionSymbol.UseCase,
        ["usecase/"] = DescriptionSymbol.BusinessUseCase,
        ["node"] = DescriptionSymbol.Node,
        ["rectangle"] = DescriptionSymbol.Rectangle,
        ["folder"] = DescriptionSymbol.Folder,
        ["package"] = DescriptionSymbol.Package,
        ["frame"] = DescriptionSymbol.Frame,
        ["cloud"] = DescriptionSymbol.Cloud,
        ["database"] = DescriptionSymbol.Database,
        ["card"] = DescriptionSymbol.Card,
        ["artifact"] = DescriptionSymbol.Artifact,
        ["storage"] = DescriptionSymbol.Storage,
        ["file"] = DescriptionSymbol.File,
        ["queue"] = DescriptionSymbol.Queue,
        ["stack"] = DescriptionSymbol.Stack,
        ["agent"] = DescriptionSymbol.Agent,
        ["hexagon"] = DescriptionSymbol.Hexagon,
        ["person"] = DescriptionSymbol.Person,
        ["boundary"] = DescriptionSymbol.Boundary,
        ["control"] = DescriptionSymbol.Control,
        ["entity"] = DescriptionSymbol.Entity,
        ["collections"] = DescriptionSymbol.Collections,
        ["label"] = DescriptionSymbol.Label,
        ["action"] = DescriptionSymbol.Action,
        ["process"] = DescriptionSymbol.Process,
        ["circle"] = DescriptionSymbol.Circle,
    };

    /// <summary>The symbols that can be containers (PlantUML's <c>CommandPackageWithUSymbol</c>).</summary>
    private static readonly HashSet<DescriptionSymbol> ContainerSymbols =
    [
        DescriptionSymbol.Package, DescriptionSymbol.Rectangle, DescriptionSymbol.Hexagon, DescriptionSymbol.Node,
        DescriptionSymbol.Artifact, DescriptionSymbol.Folder, DescriptionSymbol.File, DescriptionSymbol.Frame,
        DescriptionSymbol.Cloud, DescriptionSymbol.Action, DescriptionSymbol.Process, DescriptionSymbol.Database,
        DescriptionSymbol.Storage, DescriptionSymbol.Component, DescriptionSymbol.Card, DescriptionSymbol.Queue,
        DescriptionSymbol.Stack,
    ];

    /// <summary>The keywords of the statements of other diagram types, which a description diagram refuses.</summary>
    private static readonly HashSet<string> MixingKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "class", "abstract", "enum", "annotation", "exception", "struct", "protocol", "metaclass", "stereotype", "record",
        "dataclass", "object", "namespace", "state", "participant",
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

        if (text == "}")
        {
            Close(statement);
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

        // A link first, as PlantUML tries its link command before its declarations and notes (a note alias may start
        // with a keyword: Note .. A)
        if (SvekSyntax.TryReadLink(statement, out var link))
        {
            ReadLink(link);
            return index;
        }

        var word = SvekSyntax.FirstWord(text);

        if (ReadKeywordStatement(index, word) is { } last)
        {
            return last;
        }

        // A declaration without a keyword: [C], () I, (U), :A:, or a name with 'as'
        if (IsShorthand(text))
        {
            return ReadDeclaration(index, 0, null);
        }

        if (MixingKeywords.Contains(word))
        {
            throw Error($"'{word}' is not an element of a {_type} diagram", statement.Location, MixingHint);
        }

        throw Error(
            $"'{Shorten(text)}' is not a {_type} diagram statement",
            statement.Location,
            "declare an element with 'component Name' or '[Name]', and a link with 'A --> B : label'");
    }

    /// <summary>
    ///     Reads a statement that starts with a keyword of the description diagram.
    /// </summary>
    /// <param name="index">The index of the statement.</param>
    /// <param name="word">The first word, in lower case.</param>
    /// <returns>The index of the last statement read, or null when the statement does not start with such a keyword.</returns>
    private int? ReadKeywordStatement(int index, string word)
    {
        var statement = _statements[index];
        var text = statement.Text;
        if (SymbolKeywords.TryGetValue(word, out var symbol) && IsDeclaration(text, word.Length))
        {
            return ReadDeclaration(index, word.Length, symbol);
        }

        switch (word)
        {
            case "port" or "portin" or "portout" when IsDeclaration(text, word.Length):
                ReadPort(statement, word);
                return index;
            case "archimate" when IsDeclaration(text, word.Length):
                return ReadArchimate(index);
            case "note":
                return ReadNote(index);
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
            case "together" when text.TrimEnd().EndsWith('{'):
                Unsupported("'together' groups are not drawn; their members are drawn", statement.Location);
                Open(new OpenBlock(null, statement.Location, "together group"));
                return index;
            case "json" or "map" when text.TrimEnd().EndsWith('{'):
                return SkipBlock(index, word);
            case "hide" or "show":
                Unsupported($"'{Shorten(text)}' is not applied; every element is drawn", statement.Location);
                return index;
            case "remove" or "restore":
                Unsupported($"'{word}' is not applied; every element is drawn", statement.Location);
                return index;
            case "allowmixing" or "allow_mixing":
                Unsupported($"'allowmixing' is not applied; a {_type} diagram draws only its own elements", statement.Location);
                return index;
            case "set" or "skin" or "url" or "newpage" or "mainframe":
                Unsupported($"'{word}' is not drawn", statement.Location);
                return index;
        }

        return null;
    }

    /// <summary>
    ///     Decides whether a statement that starts with a keyword is a declaration: the keyword is followed by white
    ///     space and the start of a name, or by a block opener (an anonymous container).
    /// </summary>
    /// <param name="text">The statement text.</param>
    /// <param name="length">The length of the keyword.</param>
    /// <returns>True when it is.</returns>
    private static bool IsDeclaration(string text, int length)
    {
        var i = length;
        if (i >= text.Length || !char.IsWhiteSpace(text[i]))
        {
            return false;
        }

        SvekSyntax.SkipSpaces(text, ref i);
        return i < text.Length && (SvekSyntax.IsIdentifierChar(text[i]) || SvekSyntax.IsQuote(text[i]) || text[i] is '[' or '(' or ':' or '{' or '#' or '<');
    }

    /// <summary>
    ///     Decides whether a statement is a declaration without a keyword: it starts with a component <c>[C]</c>, an
    ///     interface <c>() I</c>, a use case <c>(U)</c>, or an actor <c>:A:</c>, or it is a name with <c>as</c>.
    /// </summary>
    /// <param name="text">The statement text.</param>
    /// <returns>True when it is.</returns>
    private static bool IsShorthand(string text)
    {
        if (text[0] is '[' or '(' or ':')
        {
            return true;
        }

        var i = 0;
        if (!SvekSyntax.TryReadName(text, ref i, out _))
        {
            return false;
        }

        SvekSyntax.SkipSpaces(text, ref i);
        SvekSyntax.ReadStereotypes(text, ref i);
        return SvekSyntax.TryKeyword(text, ref i, "as");
    }

    /// <summary>
    ///     Reads a declaration: <c>[keyword] name [&lt;&lt;S&gt;&gt;] [as name] [&lt;&lt;S&gt;&gt;] [[[url]]] [#color]</c>,
    ///     followed by nothing, a block opener <c>{</c> (a container), or the opener of a multi-line description
    ///     (<c>[</c>, or <c>as "</c>).
    /// </summary>
    /// <param name="index">The index of the statement.</param>
    /// <param name="start">The index after the keyword, or 0 without one.</param>
    /// <param name="keyword">The symbol of the keyword, or null without one.</param>
    /// <returns>The index of the last statement read.</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the declaration cannot be read.</exception>
    private int ReadDeclaration(int index, int start, DescriptionSymbol? keyword)
    {
        var statement = _statements[index];
        var text = statement.Text;
        var i = start;
        SvekSyntax.SkipSpaces(text, ref i);

        // An anonymous container: the keyword, optional stereotypes and colors, and the opener
        if (keyword is { } anonymous && (text[i] == '{' || (text[i] is '#' or '<' && text.TrimEnd().EndsWith('{'))))
        {
            if (!ContainerSymbols.Contains(anonymous))
            {
                throw Error($"the {SymbolKeyword(anonymous)} symbol cannot hold elements", statement.Locate(i), "hold elements in a container symbol such as node, package, or rectangle");
            }

            var group = Declare(NextAnonymousName(), anonymous, string.Empty, statement, container: true);
            ReadAttributes(statement, ref i, group);
            ReadOpener(statement, i, group, anonymous);
            return index;
        }

        // The names: a code, a display with 'as', and the symbol their shape gives
        var (code, display, symbol, multiline, early) = ReadNames(statement, ref i, keyword);
        var stereotypes = early.Concat(SvekSyntax.ReadStereotypes(text, ref i)).ToList();
        var rest = text[i..].TrimEnd();
        var container = rest.EndsWith('{') && !rest.EndsWith("[{", StringComparison.Ordinal);
        if (multiline)
        {
            // component C as " ... " over several lines
            var (lines, last) = ReadMultiline(index, display ?? string.Empty, '"');
            var element = Declare(code, symbol, lines, statement, container: false);
            AddStereotypes(element, stereotypes, statement);
            return last;
        }

        if (container && !ContainerSymbols.Contains(symbol))
        {
            throw Error($"the {SymbolKeyword(symbol)} '{code}' cannot hold elements", statement.Locate(i), "hold elements in a container symbol such as node, package, or rectangle");
        }

        var declared = Declare(code, symbol, display, statement, container);
        AddStereotypes(declared, stereotypes, statement);
        ReadAttributes(statement, ref i, declared);
        rest = text[i..].Trim();
        if (rest.StartsWith('[') && keyword is not null && !rest.EndsWith(']'))
        {
            // component C [ ... ] over several lines: the description replaces the name
            var (lines, last) = ReadMultiline(index, rest[1..].Trim(), ']');
            CheckLabel(lines, statement.Location);
            declared.Display = lines;
            return last;
        }

        ReadOpener(statement, i, declared, symbol);
        return index;
    }

    /// <summary>
    ///     Reads the names of a declaration and the symbol their shape gives: a component <c>[x]</c>, an interface
    ///     <c>() x</c>, a use case <c>(x)</c>, or an actor <c>:x:</c> overrides the keyword, as in PlantUML.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="i">The index of the first name; advanced past the names and the white space after them.</param>
    /// <param name="keyword">The symbol of the keyword, or null without one.</param>
    /// <returns>
    ///     The code, the display (or null for the code), the symbol, whether a quoted display continues on the next lines,
    ///     and the stereotypes written before <c>as</c>.
    /// </returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the names cannot be read.</exception>
    private (string Code, string? Display, DescriptionSymbol Symbol, bool Multiline, IReadOnlyList<string> Stereotypes) ReadNames(SourceStatement statement, ref int i, DescriptionSymbol? keyword)
    {
        var text = statement.Text;
        if (!TryReadElementName(text, ref i, out var first))
        {
            throw Error("the element needs a name", statement.Locate(i), "write for example 'component Web', '[Web]', or 'node \"Long name\" as N'");
        }

        SvekSyntax.SkipSpaces(text, ref i);
        var at = i;
        var early = SvekSyntax.ReadStereotypes(text, ref at);
        var (code, display) = (first, (SvekName?)null);
        var multiline = false;
        if (!SvekSyntax.TryKeyword(text, ref at, "as"))
        {
            early = [];
        }
        else
        {
            SvekSyntax.SkipSpaces(text, ref at);
            if (TryReadElementName(text, ref at, out var second) && second.Kind != SvekNameKind.Interface)
            {
                // "Display" as Code, Display as Code, and Code as "Display" (PlantUML's three forms)
                (code, display) = second.Kind == SvekNameKind.Plain ? (second, first) : (first, second);
                i = at;
            }
            else if (at < text.Length && SvekSyntax.IsQuote(text[at]) && keyword is not null && first.Kind == SvekNameKind.Plain)
            {
                // Code as "first line ... continued on the next lines
                display = new SvekName(text[(at + 1)..], SvekNameKind.Quoted);
                multiline = true;
                i = text.Length;
            }
            else
            {
                throw Error("'as' needs a name", statement.Locate(at), "write for example 'component \"Long name\" as L'");
            }

            SvekSyntax.SkipSpaces(text, ref i);
        }

        var symbol = ShapeSymbol(code) ?? (display is { } d ? ShapeSymbol(d) : null) ?? keyword ?? DescriptionSymbol.Actor;
        if (symbol is DescriptionSymbol.UseCase && keyword is DescriptionSymbol.BusinessUseCase)
        {
            symbol = DescriptionSymbol.BusinessUseCase;
        }
        else if (symbol is DescriptionSymbol.Actor && keyword is DescriptionSymbol.BusinessActor)
        {
            symbol = DescriptionSymbol.BusinessActor;
        }

        CheckLabel(code.Text, statement.Location);
        CheckLabel(display?.Text, statement.Location);
        return (code.Text, display?.Text, symbol, multiline, early);
    }

    /// <summary>
    ///     Reads the name of an element at an index: a name of the shared reader, with the history marks read as
    ///     components (<see cref="Normalize"/>).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="i">The index; advanced past the name when one is read.</param>
    /// <param name="name">Receives the name.</param>
    /// <returns>True when a name of an element is read (not <c>[*]</c> or <c>X[H]</c>).</returns>
    private static bool TryReadElementName(string text, ref int i, out SvekName name)
    {
        var at = i;
        if (!SvekSyntax.TryReadName(text, ref at, out var read))
        {
            name = default;
            return false;
        }

        name = Normalize(read);
        if (name.Kind is SvekNameKind.Star or SvekNameKind.History or SvekNameKind.DeepHistory)
        {
            return false;
        }

        i = at;
        return true;
    }

    /// <summary>
    ///     Reads the history marks of a state diagram as the components they are in a description diagram:
    ///     <c>[H]</c> and <c>[H*]</c> are components named <c>H</c> and <c>H*</c> (PlantUML's description names exclude
    ///     only <c>[*]</c>).
    /// </summary>
    /// <param name="name">The name as the shared reader read it.</param>
    /// <returns>The name of a description diagram.</returns>
    private static SvekName Normalize(SvekName name) => name switch
    {
        { Kind: SvekNameKind.History, Text.Length: 0 } => new SvekName("H", SvekNameKind.Bracketed),
        { Kind: SvekNameKind.DeepHistory, Text.Length: 0 } => new SvekName("H*", SvekNameKind.Bracketed),
        _ => name,
    };

    /// <summary>
    ///     Gets the symbol the shape of a name gives (PlantUML's <c>getCharEncoding</c>).
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The symbol, or null for a plain or quoted name.</returns>
    private static DescriptionSymbol? ShapeSymbol(SvekName name) => name.Kind switch
    {
        SvekNameKind.Bracketed => DescriptionSymbol.Component,
        SvekNameKind.Interface => DescriptionSymbol.Interface,
        SvekNameKind.Parenthesized => name.Business ? DescriptionSymbol.BusinessUseCase : DescriptionSymbol.UseCase,
        SvekNameKind.Colon => name.Business ? DescriptionSymbol.BusinessActor : DescriptionSymbol.Actor,
        _ => null,
    };

    /// <summary>
    ///     Declares an element in the current container, or finds the one the name already declares there (PlantUML
    ///     refuses a declaration that changes its symbol: "This element is already defined").
    /// </summary>
    /// <param name="code">The name.</param>
    /// <param name="symbol">The symbol.</param>
    /// <param name="display">The display, or null for the name (empty for an anonymous container).</param>
    /// <param name="statement">The statement.</param>
    /// <param name="container">Whether the declaration opens a container.</param>
    /// <returns>The element.</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the name is already used otherwise, or a limit is exceeded.</exception>
    private DescriptionElement Declare(string code, DescriptionSymbol symbol, string? display, SourceStatement statement, bool container)
    {
        var id = Join(CurrentContainer, code);
        if (_byId.TryGetValue(id, out var existing))
        {
            if (existing.Symbol != symbol || existing.IsContainer != container)
            {
                throw Error(
                    $"the element '{id}' is already defined as {Article(existing)}",
                    statement.Location,
                    "declare each element once, before the links that use it, or give this one another name");
            }
        }
        else
        {
            existing = Create(code, symbol, statement.Location);
            existing.IsContainer = container;
        }

        if (display is not null)
        {
            existing.Display = display;
        }

        _lastElement = existing;
        return existing;
    }

    /// <summary>
    ///     Describes an element for a message: its symbol with an article, and whether it is a container.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <returns>For example <c>a component container</c>, or <c>a name only written in links</c>.</returns>
    private static string Article(DescriptionElement element)
    {
        if (element.Symbol == DescriptionSymbol.Unknown)
        {
            return "a name of a link";
        }

        var keyword = SymbolKeyword(element.Symbol);
        var article = keyword[0] is 'a' or 'e' or 'i' or 'o' or 'u' ? "an " : "a ";
        return article + keyword + (element.IsContainer ? " container" : string.Empty);
    }

    /// <summary>
    ///     Adds the stereotypes of a declaration to an element.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="stereotypes">The stereotypes.</param>
    /// <param name="statement">The statement.</param>
    private void AddStereotypes(DescriptionElement element, IReadOnlyList<string> stereotypes, SourceStatement statement)
    {
        foreach (var stereotype in stereotypes)
        {
            CheckLabel(stereotype, statement.Location);
            element.Stereotypes.Add(stereotype);
        }
    }

    /// <summary>
    ///     Reads what may follow the names of a declaration: stereotypes, a URL (reported, not drawn), and colors.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="i">The index; advanced past what was read.</param>
    /// <param name="element">The element that receives it.</param>
    private void ReadAttributes(SourceStatement statement, ref int i, DescriptionElement element)
    {
        var text = statement.Text;
        AddStereotypes(element, SvekSyntax.ReadStereotypes(text, ref i), statement);
        if (string.CompareOrdinal(text, i, "[[", 0, 2) == 0 && text.IndexOf("]]", i + 2, StringComparison.Ordinal) is var close and > 0)
        {
            Unsupported("URLs ('[[...]]') are not drawn", statement.Locate(i));
            i = close + 2;
            SvekSyntax.SkipSpaces(text, ref i);
        }

        ReadColors(statement, ref i, element);
        AddStereotypes(element, SvekSyntax.ReadStereotypes(text, ref i), statement);
    }

    /// <summary>
    ///     Reads the end of a declaration: nothing, or the block opener of a container.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="i">The index of the rest.</param>
    /// <param name="element">The element.</param>
    /// <param name="symbol">The symbol of the declaration.</param>
    /// <exception cref="PlantUmlSyntaxException">Thrown when something else follows.</exception>
    private void ReadOpener(SourceStatement statement, int i, DescriptionElement element, DescriptionSymbol symbol)
    {
        var rest = statement.Text[i..].Trim();
        if (rest.Length == 0)
        {
            return;
        }

        if (rest == "{" && element.IsContainer)
        {
            Open(new OpenBlock(element, statement.Location, SymbolKeyword(symbol)));
            return;
        }

        if (rest.StartsWith(':'))
        {
            throw Error("a description after a colon is not a description diagram statement", statement.Locate(i), "write a multi-line description in brackets: 'component C [' ... ']'");
        }

        throw Error($"'{Shorten(rest)}' cannot follow the {SymbolKeyword(symbol)} '{element.Id}'", statement.Locate(i), "open a container with '{', or write the declaration alone");
    }

    /// <summary>
    ///     Reads the lines of a multi-line description up to the line that ends with its closer (PlantUML's
    ///     <c>CommandCreateElementMultilines</c>).
    /// </summary>
    /// <param name="index">The index of the opening statement.</param>
    /// <param name="first">The text after the opener on the first line.</param>
    /// <param name="closer">The closer: <c>]</c> or <c>"</c>.</param>
    /// <returns>The lines joined with line feeds, and the index of the closing statement.</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the description is not closed.</exception>
    private (string Text, int Index) ReadMultiline(int index, string first, char closer)
    {
        var lines = new List<string>();
        if (first.Length > 0)
        {
            lines.Add(first);
        }

        var i = index + 1;
        while (i < _statements.Count)
        {
            var line = _statements[i].Text.Trim();
            if (line.EndsWith(closer) && (closer != ']' || line.IndexOfAny(['[', ']']) == line.Length - 1))
            {
                var last = line[..^1].Trim();
                if (last.Length > 0)
                {
                    lines.Add(last);
                }

                var text = string.Join('\n', lines);
                CheckLabel(text, _statements[index].Location);
                return (text, i);
            }

            lines.Add(line);
            i++;
        }

        throw Error("the multi-line description is not closed", _statements[index].Location, $"close it with a line that ends with '{closer}'");
    }

    /// <summary>
    ///     Reads the colors of a declaration: <c>#back</c> and <c>#back;line:color;text:color</c>; <c>##line</c> and
    ///     <c>##[dashed]</c> are refused, as PlantUML refuses them in a description diagram.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="i">The index; advanced past the colors and the white space after them.</param>
    /// <param name="element">The element that receives them.</param>
    /// <exception cref="PlantUmlSyntaxException">Thrown when a color cannot be read.</exception>
    private void ReadColors(SourceStatement statement, ref int i, DescriptionElement element)
    {
        var text = statement.Text;
        while (i < text.Length && text[i] == '#')
        {
            var start = i;
            var end = i;
            while (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] is not ('{' or '['))
            {
                end++;
            }

            var token = text[start..end];
            if (token.StartsWith("##", StringComparison.Ordinal))
            {
                // PlantUML's description declarations (CommandCreateElementFull) take no '##line' color or style
                var wordEnd = start;
                while (wordEnd < text.Length && !char.IsWhiteSpace(text[wordEnd]) && text[wordEnd] != '{')
                {
                    wordEnd++;
                }

                throw Error($"'{text[start..wordEnd]}' is not a color of a description diagram element", statement.Locate(start), "write a line color as '#line:color' or '#back;line:color'");
            }

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
                    case "text":
                        element.TextColor = Color(value, statement, start);
                        break;
                    default:
                        Unsupported($"'{part}' is not drawn", statement.Locate(start));
                        break;
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
    ///     Reads an archimate element (<c>archimate #color "Display" as code &lt;&lt;stereotype&gt;&gt;</c>), which is
    ///     drawn as a rectangle and reported.
    /// </summary>
    /// <param name="index">The index of the statement.</param>
    /// <returns>The index of the last statement read.</returns>
    private int ReadArchimate(int index)
    {
        var text = _statements[index].Text;
        var i = "archimate".Length;
        SvekSyntax.SkipSpaces(text, ref i);
        if (i < text.Length && text[i] == '#')
        {
            while (i < text.Length && !char.IsWhiteSpace(text[i]))
            {
                i++;
            }
        }

        return ReadDeclaration(index, i, DescriptionSymbol.Archimate);
    }

    /// <summary>
    ///     Reads a port (<c>port</c>, <c>portin</c>, <c>portout</c>): ports are read but not drawn, nor the links that
    ///     end at them; PlantUML refuses a port outside an element.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="word">The keyword.</param>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the port is not inside a container or has no name.</exception>
    private void ReadPort(SourceStatement statement, string word)
    {
        var text = statement.Text;
        var i = word.Length;
        SvekSyntax.SkipSpaces(text, ref i);
        if (CurrentContainer is not { } container)
        {
            throw Error("a port can only be declared inside an element", statement.Location, "declare the port inside a container, such as 'component C {' ... '}'");
        }

        if (!SvekSyntax.TryReadName(text, ref i, out var name) || name.Kind is not (SvekNameKind.Plain or SvekNameKind.Quoted))
        {
            throw Error("the port needs a name", statement.Locate(i), "write for example 'port p1'");
        }

        var code = SvekSyntax.TryReadAlias(text, ref i, out var alias) ? alias : name.Text;
        CheckLabel(code, statement.Location);
        CountNode(statement.Location);
        _portIds.Add(Join(container, code));
        _portNames.Add(code);
        Unsupported($"ports are not drawn; the {word} '{code}' and its links are left out", statement.Location);
    }

    /// <summary>
    ///     Skips a <c>json</c> or <c>map</c> block, counting its braces, and reports it.
    /// </summary>
    /// <param name="index">The index of the statement.</param>
    /// <param name="word">The keyword.</param>
    /// <returns>The index of the last statement of the block.</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the block is not closed.</exception>
    private int SkipBlock(int index, string word)
    {
        var statement = _statements[index];
        Unsupported($"'{word}' blocks are not drawn in a {_type} diagram", statement.Location);
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
    ///     Resolves an end of a link or the target of a note: a floating note, an element (found, or created with the
    ///     symbol its shape gives), or a port (null: not drawn).
    /// </summary>
    /// <param name="name">The name as written.</param>
    /// <param name="location">The location of the statement.</param>
    /// <returns>The node, or null for a port.</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the name is a pseudo-state of a state diagram, or a limit is exceeded.</exception>
    private DescriptionNode? ResolveEnd(SvekName name, PlantUmlLocation location)
    {
        name = Normalize(name);
        if (name.Kind is SvekNameKind.Star or SvekNameKind.History or SvekNameKind.DeepHistory)
        {
            throw Error($"'{(name.Kind == SvekNameKind.Star ? "[*]" : name.Text + "[H]")}' is a pseudo-state of a state diagram", location, MixingHint);
        }

        if (_noteByAlias.TryGetValue(name.Text, out var note))
        {
            return note;
        }

        if (Find(name.Text, location) is { } found)
        {
            return found;
        }

        if (IsPort(name.Text))
        {
            return null;
        }

        return Create(name.Text, ShapeSymbol(name) ?? DescriptionSymbol.Unknown, location);
    }

    /// <summary>
    ///     Reads a link and adds it, reporting the parts that are not drawn; a link that ends at a port is reported and
    ///     left out.
    /// </summary>
    /// <param name="read">The link statement.</param>
    /// <exception cref="PlantUmlSyntaxException">Thrown when an end cannot be resolved or a limit is exceeded.</exception>
    private void ReadLink(SvekLinkStatement read)
    {
        var location = read.Location;
        var from = ResolveEnd(read.From, location);
        var to = ResolveEnd(read.To, location);
        if (from is null || to is null)
        {
            Unsupported("a link to a port is not drawn", location);
            return;
        }

        var arrow = read.Arrow;
        LinkDecor Drawn(LinkDecor decor)
        {
            if (SvekGeometry.IsDrawn(decor))
            {
                return decor;
            }

            Unsupported($"the '{decor}' end of a link is not drawn; the line ends plain", location);
            return LinkDecor.None;
        }

        if (read.TailRole is not null || read.HeadRole is not null)
        {
            Unsupported("roles of a link ('/role') are not drawn", location);
        }

        if (read.TailQualifier is not null || read.HeadQualifier is not null)
        {
            Unsupported("qualifiers of a link ('[key]') are not drawn", location);
        }

        if (arrow.Middle is not null)
        {
            Unsupported("the middle decoration of a link is not drawn", location);
        }

        if (arrow.NoRank)
        {
            Unsupported("'norank' is not applied; the link is laid out like the others", location);
        }

        if (read.Stereotypes.Count > 0)
        {
            Unsupported("stereotypes of a link are not drawn", location);
        }

        foreach (var label in new[] { read.Label, read.TailLabel, read.HeadLabel })
        {
            CheckLabel(label, location);
        }

        if (_links.Count >= _limits.MaxEdges)
        {
            throw new PlantUmlSyntaxException(
                PlantUmlDiagnosticCodes.LimitExceeded,
                string.Create(System.Globalization.CultureInfo.InvariantCulture, $"the diagram has more than {_limits.MaxEdges} links (PlantUmlLimits.MaxEdges)"),
                location,
                "split the diagram, or raise PlantUmlLimits.MaxEdges when the source is trusted");
        }

        var link = new DescriptionLink(NextLinkId(), from, to, location)
        {
            TailDecor = Drawn(arrow.TailDecor),
            HeadDecor = Drawn(arrow.HeadDecor),
            Style = arrow.ResolveStyle(doubledIsBold: true),
            Thickness = arrow.Thickness,
            Color = read.Color ?? arrow.Color,
            Length = arrow.Length,
            Direction = arrow.Direction,
            Label = read.Label,
            LabelArrow = read.LabelArrow,
            TailLabel = read.TailLabel,
            HeadLabel = read.HeadLabel,
            Weight = read.Weight ?? 1,
        };
        _links.Add(link);
        _lastLink = link;
        if (to is DescriptionElement target)
        {
            _lastElement = target;
        }
    }

    /// <summary>
    ///     Reads a note statement and attaches the note to its element, to the last link, or keeps it floating; a note
    ///     on a container floats in it, as the engine attaches notes to leaf elements only.
    /// </summary>
    /// <param name="index">The index of the statement.</param>
    /// <returns>The index of the last statement read.</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the note cannot be read or attached, or a limit is exceeded.</exception>
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
            if (_noteByAlias.ContainsKey(alias) || _byId.ContainsKey(alias) || _byName.ContainsKey(alias))
            {
                throw Error($"the name '{alias}' is already used", statement.Location, "give the note another name");
            }

            var floating = new DescriptionNote(alias, read.Text, read.Location) { Fill = read.Fill, Container = CurrentContainer };
            _noteByAlias[alias] = floating;
            _notes.Add(floating);
            return index;
        }

        var note = new DescriptionNote(NextNoteId(), read.Text, read.Location) { Fill = read.Fill, Position = read.Position ?? NotePosition.Right };
        if (read.OnLink)
        {
            note.OnLink = _lastLink ?? throw Error("'note on link' needs a link before it", statement.Location, "write the note after the link it describes");
        }
        else if (read.Target is { } target)
        {
            switch (ResolveEnd(target, statement.Location))
            {
                case DescriptionElement { IsContainer: true } container:
                    Unsupported($"a note on the container '{container.Id}' is drawn as a floating note", statement.Location);
                    note.Container = container;
                    break;
                case DescriptionElement element:
                    note.Target = element;
                    break;
                case DescriptionNote:
                    throw Error($"'{target.Text}' is a note; a note cannot be attached to a note", statement.Location, "attach the note to an element");
                default:
                    Unsupported($"a note on the port '{target.Text}' is not drawn", statement.Location);
                    return index;
            }
        }
        else if (_lastElement is { IsContainer: true } last)
        {
            Unsupported($"a note on the container '{last.Id}' is drawn as a floating note", statement.Location);
            note.Container = last;
        }
        else
        {
            note.Target = _lastElement ?? throw Error("the note needs an element before it", statement.Location, "write 'note left of A : text'");
        }

        _notes.Add(note);
        return index;
    }
}
