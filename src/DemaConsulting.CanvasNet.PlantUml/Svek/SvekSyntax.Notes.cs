// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Syntax;

namespace DemaConsulting.CanvasNet.PlantUml.Svek;

/// <summary>
///     A note statement as written.
/// </summary>
/// <param name="Text">The text (lines joined with line feeds).</param>
/// <param name="Location">The location of the statement.</param>
internal sealed record SvekNoteStatement(string Text, PlantUmlLocation Location)
{
    /// <summary>
    ///     Gets the side of the target or of the link label, or null for a floating note (<c>note "text" as N</c>) and
    ///     for a note on a link without a side.
    /// </summary>
    public NotePosition? Position { get; init; }

    /// <summary>
    ///     Gets the entity named after <c>of</c>, or null (for a floating note, a note on a link, and a note that
    ///     follows the last declared entity, <c>note left : text</c>).
    /// </summary>
    public SvekName? Target { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the note is on the last link (<c>note on link</c>).
    /// </summary>
    public bool OnLink { get; init; }

    /// <summary>
    ///     Gets the alias of a floating note (<c>as N</c>), or null.
    /// </summary>
    public string? Alias { get; init; }

    /// <summary>
    ///     Gets the fill (<c>#color</c>), or null.
    /// </summary>
    public Rgba32? Fill { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the note is floating (neither attached to an entity nor on a link).
    /// </summary>
    public bool IsFloating => !OnLink && Position is null;
}

/// <summary>
///     The note grammar of <see cref="SvekSyntax"/>.
/// </summary>
internal static partial class SvekSyntax
{
    /// <summary>
    ///     Reads a note statement (PlantUML's note commands of the class, state, and description diagrams), on one line
    ///     or as a block up to <c>end note</c>:
    ///     <c>note left|right|top|bottom [of Name] [#color] [: text]</c>,
    ///     <c>note [left|right|top|bottom] on link [#color] [: text]</c>,
    ///     <c>note "text" as N [#color]</c>, and <c>note as N [#color]</c>.
    /// </summary>
    /// <param name="statements">The statements of the diagram.</param>
    /// <param name="index">The index of the statement; advanced to the last statement read when it is a note.</param>
    /// <param name="note">Receives the note.</param>
    /// <returns>True when the statement is a note.</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown when a note is not closed, its color cannot be read, or its form is not one PlantUML knows.</exception>
    public static bool TryReadNote(IReadOnlyList<SourceStatement> statements, ref int index, out SvekNoteStatement note)
    {
        ArgumentNullException.ThrowIfNull(statements);
        var statement = statements[index];
        var text = statement.Text;
        var i = 0;
        note = null!;
        if (!TryKeyword(text, ref i, "note"))
        {
            return false;
        }

        SkipSpaces(text, ref i);

        // A floating note: note "text" as N, or note as N ... end note
        if (i < text.Length && IsQuote(text[i]))
        {
            return ReadFloatingNote(statement, i, out note);
        }

        if (TryKeyword(text, ref i, "as"))
        {
            SkipSpaces(text, ref i);
            if (!TryReadName(text, ref i, out var alias))
            {
                throw Error("a note needs a name after 'as'", statement.Location, "write for example 'note as N1'");
            }

            SkipSpaces(text, ref i);
            var fill = ReadColor(text, ref i, statement.Locate(i));
            var (block, last) = ReadBlock(statements, index, "end note", "the note");
            note = new SvekNoteStatement(block, statement.Location) { Alias = alias.Text, Fill = fill };
            index = last;
            return true;
        }

        // An attached note: a side, then 'of Name', 'on link', or nothing (the last entity)
        var position = ReadPosition(text, ref i);
        SkipSpaces(text, ref i);
        SvekName? target = null;
        var onLink = false;
        if (position is not null && TryKeyword(text, ref i, "of"))
        {
            SkipSpaces(text, ref i);
            if (!TryReadName(text, ref i, out var name))
            {
                throw Error("a note needs the name of an element after 'of'", statement.Location, "write for example 'note left of A : text'");
            }

            target = name;
            SkipSpaces(text, ref i);
        }
        else if (TryKeyword(text, ref i, "on"))
        {
            SkipSpaces(text, ref i);
            if (!TryKeyword(text, ref i, "link"))
            {
                throw Error("a note must be 'on link'", statement.Location, "write for example 'note on link : text'");
            }

            onLink = true;
            SkipSpaces(text, ref i);
        }
        else if (position is null)
        {
            throw Error(
                $"'{Shorten(text)}' is not a note",
                statement.Location,
                "write 'note left of A : text', 'note on link : text', or 'note \"text\" as N'");
        }

        // An optional color, then the text after a colon or a block
        var color = ReadColor(text, ref i, statement.Locate(i));
        SkipSpaces(text, ref i);
        string value;
        var end = index;
        if (i < text.Length && text[i] == ':')
        {
            value = text[(i + 1)..].Trim();
        }
        else if (i == text.Length)
        {
            (value, end) = ReadBlock(statements, index, "end note", "the note");
        }
        else
        {
            throw Error($"'{Shorten(text)}' is not a note", statement.Locate(i), "put the text of the note after a colon, or on the lines up to 'end note'");
        }

        note = new SvekNoteStatement(value, statement.Location)
        {
            Position = onLink ? position ?? NotePosition.Bottom : position,
            Target = target,
            OnLink = onLink,
            Fill = color,
        };
        index = end;
        return true;
    }

    /// <summary>
    ///     Reads a floating note on one line: <c>note "text" as N [#color]</c>.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="index">The index of the opening quote.</param>
    /// <param name="note">Receives the note.</param>
    /// <returns>True.</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the quote is not closed, the alias is missing, or the color cannot be read.</exception>
    private static bool ReadFloatingNote(SourceStatement statement, int index, out SvekNoteStatement note)
    {
        var text = statement.Text;
        var close = IndexOfQuote(text, index + 1);
        if (close < 0)
        {
            throw Error("the text of the note is not closed", statement.Locate(index), "close it with a double quote");
        }

        var value = text[(index + 1)..close];
        var i = close + 1;
        if (!TryReadAlias(text, ref i, out var alias))
        {
            throw Error("a floating note needs a name", statement.Locate(close + 1), "write for example 'note \"text\" as N1'");
        }

        var fill = ReadColor(text, ref i, statement.Locate(i));
        if (i < text.Length)
        {
            throw Error($"'{Shorten(text)}' is not a note", statement.Locate(i), "write for example 'note \"text\" as N1'");
        }

        note = new SvekNoteStatement(value, statement.Location) { Alias = alias, Fill = fill };
        return true;
    }

    /// <summary>
    ///     Reads the side of a note.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The index; advanced past the side when one is read.</param>
    /// <returns>The side, or null.</returns>
    private static NotePosition? ReadPosition(string text, ref int index)
    {
        if (TryKeyword(text, ref index, "left"))
        {
            return NotePosition.Left;
        }

        if (TryKeyword(text, ref index, "right"))
        {
            return NotePosition.Right;
        }

        if (TryKeyword(text, ref index, "top"))
        {
            return NotePosition.Top;
        }

        return TryKeyword(text, ref index, "bottom") ? NotePosition.Bottom : null;
    }

    /// <summary>
    ///     Shortens a statement for a message.
    /// </summary>
    /// <param name="text">The statement.</param>
    /// <returns>At most 60 characters.</returns>
    private static string Shorten(string text) => text.Length <= 60 ? text : string.Concat(text.AsSpan(0, 57), "...");
}
