// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Styling;
using DemaConsulting.CanvasNet.PlantUml.Svek;
using DemaConsulting.CanvasNet.PlantUml.Syntax;

namespace DemaConsulting.CanvasNet.PlantUml.Class;

/// <content>
///     The readers of the declarations (classes, objects, maps, packages), their bodies and members, the notes, the
///     relationships, and <c>hide</c> and <c>show</c>, and the resolution of names into entities and packages.
/// </content>
internal sealed partial class ClassParser
{
    /// <summary>The keywords that declare a class or one of its kin, with their kinds.</summary>
    private static readonly Dictionary<string, ClassKind> ClassKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["class"] = ClassKind.Class,
        ["abstract"] = ClassKind.AbstractClass,
        ["interface"] = ClassKind.Interface,
        ["enum"] = ClassKind.Enum,
        ["annotation"] = ClassKind.Annotation,
        ["entity"] = ClassKind.Entity,
        ["exception"] = ClassKind.Exception,
        ["struct"] = ClassKind.Struct,
        ["protocol"] = ClassKind.Protocol,
        ["metaclass"] = ClassKind.Metaclass,
        ["stereotype"] = ClassKind.Stereotype,
        ["record"] = ClassKind.Record,
        ["dataclass"] = ClassKind.DataClass,
        ["static"] = ClassKind.Class,
        ["circle"] = ClassKind.Class,
        ["diamond"] = ClassKind.Class,
    };

    /// <summary>The stereotypes that choose the outline of a package instead of being drawn.</summary>
    private static readonly Dictionary<string, ClusterSymbol> PackageSymbols = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Folder"] = ClusterSymbol.Folder,
        ["Rectangle"] = ClusterSymbol.Rectangle,
        ["Frame"] = ClusterSymbol.Frame,
        ["Node"] = ClusterSymbol.Node,
        ["Cloud"] = ClusterSymbol.Cloud,
        ["Database"] = ClusterSymbol.Database,
        ["Card"] = ClusterSymbol.Card,
    };

    /// <summary>
    ///     Decides whether a word is a keyword that declares a class or one of its kin.
    /// </summary>
    /// <param name="word">The word.</param>
    /// <returns>True when it is.</returns>
    private static bool IsClassKeyword(string word) => ClassKeywords.ContainsKey(word);

    /// <summary>
    ///     Decides whether a statement that starts with a keyword is a declaration: the keyword is followed by white
    ///     space and the start of a name (not an arrow, so that an element named like a keyword can be linked).
    /// </summary>
    /// <param name="text">The statement text.</param>
    /// <param name="word">The keyword.</param>
    /// <returns>True when it is.</returns>
    private static bool IsDeclaration(string text, string word)
    {
        var i = word.Length;
        if (i >= text.Length || !char.IsWhiteSpace(text[i]))
        {
            return false;
        }

        SvekSyntax.SkipSpaces(text, ref i);
        return i < text.Length && (SvekSyntax.IsIdentifierChar(text[i]) || SvekSyntax.IsQuote(text[i]));
    }

    /// <summary>
    ///     Reads a class declaration: <c>[abstract] class|interface|... Name [&lt;T&gt;] [&lt;&lt;S&gt;&gt;] [#color]
    ///     [##line] [extends A] [implements B] [{ ... }]</c>.
    /// </summary>
    /// <param name="index">The index of the statement.</param>
    /// <param name="start">The index of the keyword in the statement.</param>
    /// <returns>The index of the last statement read (the closing brace of a body).</returns>
    private int ReadDeclaration(int index, int start)
    {
        var statement = _statements[index];
        var text = statement.Text;
        var i = start;
        var word = SvekSyntax.FirstWord(text[i..]);
        var kind = ClassKeywords[word];
        i += word.Length;
        SvekSyntax.SkipSpaces(text, ref i);
        if (word is "abstract" or "static")
        {
            var afterKeyword = i;
            if (SvekSyntax.TryKeyword(text, ref afterKeyword, "class"))
            {
                i = afterKeyword;
                SvekSyntax.SkipSpaces(text, ref i);
            }
        }

        if (word is "circle" or "diamond")
        {
            Unsupported($"'{word}' elements are not drawn; it is drawn as a class", statement.Location);
        }

        var (name, display, generic) = ReadEntityName(statement, ref i, allowGeneric: true);
        var entity = Declare(name, kind, display, statement);
        entity.Generic = generic ?? entity.Generic;
        CheckLabel(entity.Generic, statement.Location);
        ReadStereotypes(statement, ref i, entity);
        ReadColors(statement, ref i, entity);

        // extends and implements: links from the parents, drawn as extensions (dashed for implements)
        foreach (var (keyword, style) in new[] { ("extends", LinkStyle.Solid), ("implements", LinkStyle.Dashed) })
        {
            var at = i;
            if (!SvekSyntax.TryKeyword(text, ref at, keyword))
            {
                continue;
            }

            i = at;
            while (true)
            {
                SvekSyntax.SkipSpaces(text, ref i);
                if (!SvekSyntax.TryReadName(text, ref i, out var parentName) || parentName.Kind is not (SvekNameKind.Plain or SvekNameKind.Quoted))
                {
                    throw Error($"'{keyword}' needs the name of a class", statement.Locate(i), $"write for example 'class B {keyword} A'");
                }

                var parent = ResolveEnd(parentName, statement);
                AddLink(new ClassLink(NextLinkId(), parent, entity, statement.Location) { TailDecor = LinkDecor.Extends, Style = style });
                SvekSyntax.SkipSpaces(text, ref i);
                if (i >= text.Length || text[i] != ',')
                {
                    break;
                }

                i++;
            }
        }

        return ReadBodyOpener(index, i, entity);
    }

    /// <summary>
    ///     Reads an object declaration: <c>object Name [&lt;&lt;S&gt;&gt;] [#color] [{ ... }]</c>.
    /// </summary>
    /// <param name="index">The index of the statement.</param>
    /// <returns>The index of the last statement read.</returns>
    private int ReadObject(int index)
    {
        var statement = _statements[index];
        var i = "object".Length;
        SvekSyntax.SkipSpaces(statement.Text, ref i);
        var (name, display, _) = ReadEntityName(statement, ref i, allowGeneric: false);
        var entity = Declare(name, ClassKind.Object, display, statement);
        ReadStereotypes(statement, ref i, entity);
        ReadColors(statement, ref i, entity);
        return ReadBodyOpener(index, i, entity);
    }

    /// <summary>
    ///     Reads a map declaration: <c>map Name [&lt;&lt;S&gt;&gt;] [#color] { key =&gt; value ... }</c>.
    /// </summary>
    /// <param name="index">The index of the statement.</param>
    /// <returns>The index of the last statement read.</returns>
    private int ReadMap(int index)
    {
        var statement = _statements[index];
        var i = "map".Length;
        SvekSyntax.SkipSpaces(statement.Text, ref i);
        var (name, display, _) = ReadEntityName(statement, ref i, allowGeneric: false);
        var entity = Declare(name, ClassKind.Map, display, statement);
        ReadStereotypes(statement, ref i, entity);
        ReadColors(statement, ref i, entity);
        return ReadBodyOpener(index, i, entity);
    }

    /// <summary>
    ///     Reads what ends a declaration: nothing, an empty body <c>{}</c>, or <c>{</c> and the body lines up to the
    ///     closing <c>}</c>.
    /// </summary>
    /// <param name="index">The index of the declaration.</param>
    /// <param name="at">The index in the statement after the declaration.</param>
    /// <param name="entity">The entity declared.</param>
    /// <returns>The index of the last statement read.</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown when something else follows, or the body is not closed.</exception>
    private int ReadBodyOpener(int index, int at, ClassEntity entity)
    {
        var statement = _statements[index];
        var rest = statement.Text[at..].Trim();
        if (rest.Length == 0)
        {
            return index;
        }

        if (rest.Replace(" ", string.Empty, StringComparison.Ordinal) == "{}")
        {
            return index;
        }

        if (rest != "{")
        {
            throw Error(
                $"'{Shorten(rest)}' cannot follow the declaration of '{entity.Display}'",
                statement.Locate(at),
                "write the members on the lines after '{' up to '}', or with 'Name : member'");
        }

        var i = index + 1;
        for (; i < _statements.Count && _statements[i].Text != "}"; i++)
        {
            var line = _statements[i];
            CheckLabel(line.Text, line.Location);
            if (entity.Kind == ClassKind.Map)
            {
                ReadMapLine(entity, line);
            }
            else
            {
                entity.Body.Add(line.Text);
            }
        }

        if (i >= _statements.Count)
        {
            throw Error($"the body of '{entity.Display}' is not closed", statement.Location, "close it with '}'");
        }

        return i;
    }

    /// <summary>
    ///     Reads a line of a map body: <c>key =&gt; value</c>, or <c>key arrow Name</c>, a link from the key, drawn from
    ///     the map and reported.
    /// </summary>
    /// <param name="map">The map.</param>
    /// <param name="line">The line.</param>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the line is neither.</exception>
    private void ReadMapLine(ClassEntity map, SourceStatement line)
    {
        var text = line.Text;
        var arrow = text.IndexOf("=>", StringComparison.Ordinal);
        if (arrow > 0)
        {
            map.Entries.Add((text[..arrow].Trim(), text[(arrow + 2)..].Trim()));
            return;
        }

        if (text.Length == 0)
        {
            return;
        }

        if (SvekSyntax.TryReadLink(line, out var link))
        {
            Unsupported("a link from a map entry is drawn from the map", line.Location);
            var target = ResolveEnd(link.To, line);
            AddLink(LinkFrom(link, map, target));
            return;
        }

        throw Error($"'{Shorten(text)}' is not a map entry", line.Location, "write an entry as 'key => value'");
    }

    /// <summary>
    ///     Reads a member added to an entity with <c>Name : text</c> (an object's field, a map's entry with
    ///     <c>=&gt;</c>); the entity is created as a class when it does not exist.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <returns>True when the statement is such a member.</returns>
    private bool TryReadMemberStatement(SourceStatement statement)
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

        var member = text[(i + 1)..].TrimStart();
        CheckLabel(member, statement.Location);
        var entity = ResolveEnd(name, statement) as ClassEntity
            ?? throw Error($"'{name.Text}' is not a class, so it has no members", statement.Location, "add members to classes, objects, and maps only");
        if (entity.Kind == ClassKind.Map)
        {
            ReadMapLine(entity, statement.Slice(i + 1, text.Length - i - 1));
        }
        else
        {
            entity.Body.Add(member);
        }

        return true;
    }

    /// <summary>
    ///     Reads the name of a declared entity: <c>Name</c>, <c>"Display"</c>, <c>"Display" as Name</c>, or
    ///     <c>Name as "Display"</c>, with optional generic parameters after the name.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="i">The index of the name; advanced past it and the white space after it.</param>
    /// <param name="allowGeneric">Whether generic parameters may follow the name.</param>
    /// <returns>The name that identifies the entity, its display (or null for the name's), and the generic parameters (or null).</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown when there is no name.</exception>
    private static (SvekName Name, string? Display, string? Generic) ReadEntityName(SourceStatement statement, ref int i, bool allowGeneric)
    {
        var text = statement.Text;
        if (!SvekSyntax.TryReadName(text, ref i, out var first) || first.Kind is not (SvekNameKind.Plain or SvekNameKind.Quoted))
        {
            throw Error("the declaration needs a name", statement.Locate(i), "write for example 'class Car' or 'class \"Long name\" as L'");
        }

        string? generic = allowGeneric ? ReadGeneric(text, ref i) : null;
        string? display = null;
        var name = first;
        var at = i;
        SvekSyntax.SkipSpaces(text, ref at);
        if (SvekSyntax.TryKeyword(text, ref at, "as"))
        {
            SvekSyntax.SkipSpaces(text, ref at);
            if (!SvekSyntax.TryReadName(text, ref at, out var second) || second.Kind is not (SvekNameKind.Plain or SvekNameKind.Quoted))
            {
                throw Error("'as' needs a name", statement.Locate(at), "write for example 'class \"Long name\" as L'");
            }

            // "Display" as Name, or Name as "Display"
            (name, display) = first.Kind == SvekNameKind.Quoted && second.Kind == SvekNameKind.Plain
                ? (second, first.Text)
                : (first, second.Text);
            i = at;
            generic ??= allowGeneric ? ReadGeneric(text, ref i) : null;
        }

        SvekSyntax.SkipSpaces(text, ref i);
        if (generic is null && allowGeneric)
        {
            generic = ReadGeneric(text, ref i);
            SvekSyntax.SkipSpaces(text, ref i);
        }

        return (name, display, generic);
    }

    /// <summary>
    ///     Reads generic parameters (<c>&lt;T&gt;</c>, nested brackets balanced), but not a stereotype.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="i">The index; advanced past the parameters when they are read.</param>
    /// <returns>The parameters, or null.</returns>
    private static string? ReadGeneric(string text, ref int i)
    {
        var at = i;
        SvekSyntax.SkipSpaces(text, ref at);
        if (at >= text.Length || text[at] != '<' || (at + 1 < text.Length && text[at + 1] == '<'))
        {
            return null;
        }

        var depth = 0;
        for (var k = at; k < text.Length; k++)
        {
            depth += text[k] switch
            {
                '<' => 1,
                '>' => -1,
                _ => 0,
            };
            if (depth == 0)
            {
                var generic = text[(at + 1)..k].Trim();
                if (generic.Length == 0)
                {
                    return null;
                }

                i = k + 1;
                return generic;
            }
        }

        return null;
    }

    /// <summary>
    ///     Reads the stereotypes of a declaration; a stereotype that starts with <c>(X,color)</c> gives the circled
    ///     character and its color.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="i">The index; advanced past the stereotypes.</param>
    /// <param name="entity">The entity that receives them.</param>
    private void ReadStereotypes(SourceStatement statement, ref int i, ClassEntity entity)
    {
        foreach (var raw in SvekSyntax.ReadStereotypes(statement.Text, ref i))
        {
            CheckLabel(raw, statement.Location);
            var label = raw;
            if (label.StartsWith('(') && label.IndexOf(')', StringComparison.Ordinal) is var close and > 0)
            {
                var spot = label[1..close].Split(',');
                if (spot.Length == 2 && spot[0].Trim().Length == 1)
                {
                    entity.SpotCharacter = spot[0].Trim()[0];
                    var color = spot[1].Trim();
                    if (SkinParams.TryParseColor(color.StartsWith('#') ? color : "#" + color, out var parsed))
                    {
                        entity.SpotColor = parsed;
                    }
                    else
                    {
                        throw Error($"'{color}' is not a color", statement.Location, "write a color such as #FF7700 or Orange");
                    }

                    label = label[(close + 1)..].Trim();
                }
            }

            entity.Stereotypes.Add(label);
        }
    }

    /// <summary>
    ///     Reads the colors of a declaration: <c>#back</c>, <c>#back;line:color;text:color</c>, and <c>##line</c> (a line
    ///     style in brackets, <c>##[dashed]</c>, or <c>line.dashed</c>, is reported, not drawn).
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="i">The index; advanced past the colors and the white space after them.</param>
    /// <param name="entity">The entity that receives them.</param>
    /// <exception cref="PlantUmlSyntaxException">Thrown when a color cannot be read.</exception>
    private void ReadColors(SourceStatement statement, ref int i, ClassEntity entity)
    {
        var text = statement.Text;
        while (i < text.Length && text[i] == '#')
        {
            var start = i;
            var end = i;
            while (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] != '{')
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
                    Unsupported("the line style of a class is not drawn", statement.Locate(start));
                    line = close < 0 ? string.Empty : line[(close + 1)..];
                }

                if (line.Length > 0)
                {
                    entity.Line = Color(line, statement, start);
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
                            entity.Fill = Color(value, statement, start);
                            break;
                        case "line":
                            entity.Line = Color(value, statement, start);
                            break;
                        case "text":
                            entity.TextColor = Color(value, statement, start);
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
    ///     Reads a package or namespace declaration: <c>package Name [as Alias] [&lt;&lt;S&gt;&gt;] [#color] [{]</c>; a
    ///     stereotype that names a symbol (<c>&lt;&lt;Rectangle&gt;&gt;</c>, <c>&lt;&lt;Node&gt;&gt;</c>, ...) chooses the
    ///     outline.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="keyword">The keyword (<c>package</c> or <c>namespace</c>).</param>
    private void ReadPackage(SourceStatement statement, string keyword)
    {
        var text = statement.Text;
        var i = keyword.Length;
        SvekSyntax.SkipSpaces(text, ref i);
        var (name, display, _) = ReadEntityName(statement, ref i, allowGeneric: false);
        var package = DeclarePackage(name, statement.Location);
        if (display is not null)
        {
            CheckLabel(display, statement.Location);
            package.Title = display;
        }

        foreach (var stereotype in SvekSyntax.ReadStereotypes(text, ref i))
        {
            CheckLabel(stereotype, statement.Location);
            if (PackageSymbols.TryGetValue(stereotype, out var symbol))
            {
                package.Symbol = symbol;
            }
            else
            {
                package.Stereotypes.Add(stereotype);
            }
        }

        if (i < text.Length && text[i] == '#' && !text[i..].StartsWith("##", StringComparison.Ordinal))
        {
            var end = i;
            while (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] != '{')
            {
                end++;
            }

            package.Fill = Color(text[(i + 1)..end].Split(';')[0], statement, i);
            i = end;
            SvekSyntax.SkipSpaces(text, ref i);
        }

        var rest = text[i..].Trim();
        if (rest == "{")
        {
            Open(new OpenBlock(package, statement.Location, keyword));
        }
        else if (rest.Length > 0 && rest.Replace(" ", string.Empty, StringComparison.Ordinal) != "{}")
        {
            throw Error($"'{Shorten(rest)}' cannot follow the {keyword} '{package.Title}'", statement.Locate(i), $"open the {keyword} with '{{' and close it with '}}'");
        }
    }

    /// <summary>
    ///     Reads a note statement and attaches the note to its entity, to the last link, or keeps it floating.
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
            if (_noteByAlias.ContainsKey(alias) || _entityById.ContainsKey(alias))
            {
                throw Error($"the name '{alias}' is already used", statement.Location, "give the note another name");
            }

            var floating = new ClassNote(alias, read.Text, read.Location) { Fill = read.Fill, Package = CurrentPackage };
            _noteByAlias[alias] = floating;
            _notes.Add(floating);
            return index;
        }

        var note = new ClassNote(NextNoteId(), read.Text, read.Location) { Fill = read.Fill, Position = read.Position ?? NotePosition.Right };
        if (read.OnLink)
        {
            note.OnLink = _lastLink ?? throw Error("'note on link' needs a link before it", statement.Location, "write the note after the link it describes");
        }
        else if (read.Target is { } target)
        {
            switch (ResolveEnd(target, statement))
            {
                case ClassEntity entity:
                    note.Target = entity;
                    break;
                case ClassPackage package:
                    Unsupported("a note on a package is drawn as a floating note", statement.Location);
                    note.Package = package;
                    break;
                default:
                    throw Error($"'{target.Text}' is a note; a note cannot be attached to a note", statement.Location, "attach the note to a class");
            }
        }
        else
        {
            note.Target = _lastEntity ?? throw Error("the note needs an element before it", statement.Location, "write 'note left of A : text'");
        }

        _notes.Add(note);
        return index;
    }

    /// <summary>
    ///     Reads a relationship and adds it, reporting the parts that are not drawn.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="read">The link statement.</param>
    private void ReadLink(SourceStatement statement, SvekLinkStatement read)
    {
        if (read.From.Kind == SvekNameKind.Parenthesized && read.From.Text.Contains(',', StringComparison.Ordinal))
        {
            Unsupported("association classes ('(A, B) .. C') are not drawn", read.Location);
            return;
        }

        var from = ResolveEnd(read.From, statement);
        var to = ResolveEnd(read.To, statement);
        AddLink(LinkFrom(read, from, to));
    }

    /// <summary>
    ///     Builds a link from a link statement, reporting the decorations, roles, qualifiers, middle decorations,
    ///     <c>norank</c>, and stereotypes that are not drawn.
    /// </summary>
    /// <param name="read">The link statement.</param>
    /// <param name="from">The first end.</param>
    /// <param name="to">The second end.</param>
    /// <returns>The link.</returns>
    private ClassLink LinkFrom(SvekLinkStatement read, ClassNode from, ClassNode to)
    {
        var arrow = read.Arrow;
        var location = read.Location;
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

        return new ClassLink(NextLinkId(), from, to, location)
        {
            TailDecor = Drawn(arrow.TailDecor),
            HeadDecor = Drawn(arrow.HeadDecor),
            Style = arrow.ResolveStyle(doubledIsBold: false),
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
    }

    /// <summary>
    ///     Adds a link, bounded by <see cref="PlantUmlLimits.MaxEdges"/>.
    /// </summary>
    /// <param name="link">The link.</param>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the diagram has too many links.</exception>
    private void AddLink(ClassLink link)
    {
        if (_links.Count >= _limits.MaxEdges)
        {
            throw new PlantUmlSyntaxException(
                PlantUmlDiagnosticCodes.LimitExceeded,
                string.Create(System.Globalization.CultureInfo.InvariantCulture, $"the diagram has more than {_limits.MaxEdges} links (PlantUmlLimits.MaxEdges)"),
                link.Location,
                "split the diagram, or raise PlantUmlLimits.MaxEdges when the source is trusted");
        }

        _links.Add(link);
        _lastLink = link;
    }

    /// <summary>
    ///     Reads a <c>hide</c> or <c>show</c> statement: <c>[hide|show] [kind|Name|&lt;&lt;S&gt;&gt;] [empty]
    ///     members|fields|attributes|methods|circle|stereotype</c>; anything else (hiding an element, by visibility,
    ///     <c>@unlinked</c>) is reported.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="command">The statement as the shared reader read it.</param>
    private void ReadHideShow(SourceStatement statement, SvekHideShow command)
    {
        var words = statement.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Skip(1).ToList();
        var portion = words[^1].ToLowerInvariant() switch
        {
            "member" or "members" => ClassPortion.Members,
            "field" or "fields" or "attribute" or "attributes" => ClassPortion.Fields,
            "method" or "methods" => ClassPortion.Methods,
            "circle" or "circles" or "circled" => ClassPortion.Circle,
            "stereotype" or "stereotypes" => ClassPortion.Stereotype,
            _ => (ClassPortion?)null,
        };
        var empty = words.Count >= 2 && words[^2].Equals("empty", StringComparison.OrdinalIgnoreCase);
        var genders = words.Take(words.Count - (empty ? 2 : 1)).ToList();
        if (portion is null || genders.Count > 1 || (genders.Count == 1 && genders[0].ToLowerInvariant() is "private" or "protected" or "package" or "public"))
        {
            Unsupported($"'{Shorten(statement.Text)}' is not applied; only the members, fields, methods, circle, and stereotype of kinds, names, and stereotypes are hidden or shown", statement.Location);
            return;
        }

        _rules.Add(new ClassHideRule(command.Show, genders.Count == 0 ? null : genders[0].Trim('"'), empty, portion.Value));
    }
}
