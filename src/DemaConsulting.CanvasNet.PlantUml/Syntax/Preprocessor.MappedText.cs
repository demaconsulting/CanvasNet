using System.Text;

namespace DemaConsulting.CanvasNet.PlantUml.Syntax;

/// <summary>
///     The line table of an original text: where each line starts, so that an offset becomes a one-based line and
///     column.
/// </summary>
/// <remarks>
///     A line ends at <c>\n</c>, at <c>\r\n</c> or at a lone <c>\r</c>; a column counts UTF-16 code units and a tab
///     counts as one column. The table is immutable and safe for concurrent use.
/// </remarks>
internal sealed class SourceLines
{
    /// <summary>The offsets at which the lines start, in increasing order; the first is zero.</summary>
    private readonly int[] _starts;

    /// <summary>
    ///     Initializes the line table of a text.
    /// </summary>
    /// <param name="text">The original text.</param>
    /// <param name="sourceName">The source name placed in every location, or null.</param>
    public SourceLines(string text, string? sourceName)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
        SourceName = sourceName;
        var starts = new List<int> { 0 };
        var i = 0;
        while (i < text.Length)
        {
            // A carriage return followed by a line feed is one line ending
            var crlf = text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n';
            var length = crlf ? 2 : 1;
            if (crlf || text[i] is '\n' or '\r')
            {
                starts.Add(i + length);
            }

            i += length;
        }

        _starts = [.. starts];
    }

    /// <summary>
    ///     Gets the original text.
    /// </summary>
    public string Text { get; }

    /// <summary>
    ///     Gets the source name, or null.
    /// </summary>
    public string? SourceName { get; }

    /// <summary>
    ///     Gets the number of lines (an empty text has one empty line).
    /// </summary>
    public int Count => _starts.Length;

    /// <summary>
    ///     Gets the offset at which a line starts.
    /// </summary>
    /// <param name="index">The zero-based line index.</param>
    /// <returns>The offset.</returns>
    public int StartOf(int index) => _starts[index];

    /// <summary>
    ///     Gets the text of a line, without its line ending.
    /// </summary>
    /// <param name="index">The zero-based line index.</param>
    /// <returns>The line text.</returns>
    public string LineText(int index)
    {
        var start = _starts[index];
        var end = index + 1 < _starts.Length ? _starts[index + 1] : Text.Length;
        while (end > start && Text[end - 1] is '\n' or '\r')
        {
            end--;
        }

        return Text[start..end];
    }

    /// <summary>
    ///     Locates an offset of the original text.
    /// </summary>
    /// <param name="offset">The offset, clamped to the text.</param>
    /// <returns>The one-based location.</returns>
    public PlantUmlLocation Locate(int offset)
    {
        offset = Math.Clamp(offset, 0, Text.Length);
        var index = Array.BinarySearch(_starts, offset);
        if (index < 0)
        {
            index = ~index - 1;
        }

        return new PlantUmlLocation(SourceName, index + 1, offset - _starts[index] + 1);
    }
}

/// <summary>
///     One logical statement of a diagram after preprocessing: its text and, for every character, the offset of the
///     original character it came from, so that every position of the statement has a location in the original text.
/// </summary>
/// <remarks>
///     A statement is built from one physical line, or from several lines joined by a trailing backslash; the
///     preprocessor trims it and substitutes variables and macros, and the characters that a substitution inserts are
///     mapped to the offset of the name they replace. The record is immutable and safe for concurrent use.
/// </remarks>
internal sealed class SourceStatement
{
    /// <summary>The offset in the original text of every character, plus one for the end of the statement.</summary>
    private readonly int[] _origins;

    /// <summary>
    ///     Initializes a statement.
    /// </summary>
    /// <param name="text">The statement text.</param>
    /// <param name="origins">The original offsets: one per character plus one for the end.</param>
    /// <param name="lines">The line table of the original text.</param>
    /// <exception cref="ArgumentException">Thrown when the offsets do not match the text.</exception>
    public SourceStatement(string text, int[] origins, SourceLines lines)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(origins);
        ArgumentNullException.ThrowIfNull(lines);
        if (origins.Length != text.Length + 1)
        {
            throw new ArgumentException("There must be one offset per character plus one.", nameof(origins));
        }

        Text = text;
        _origins = origins;
        Lines = lines;
    }

    /// <summary>
    ///     Gets the statement text (trimmed, comments removed, variables and macros substituted).
    /// </summary>
    public string Text { get; }

    /// <summary>
    ///     Gets the line table of the original text.
    /// </summary>
    public SourceLines Lines { get; }

    /// <summary>
    ///     Gets the location of the first character of the statement.
    /// </summary>
    public PlantUmlLocation Location => Locate(0);

    /// <summary>
    ///     Locates a character of the statement in the original text.
    /// </summary>
    /// <param name="index">The index in <see cref="Text"/>, clamped to the statement.</param>
    /// <returns>The one-based location.</returns>
    public PlantUmlLocation Locate(int index) => Lines.Locate(OriginOf(index));

    /// <summary>
    ///     Gets the offset in the original text of a character of the statement.
    /// </summary>
    /// <param name="index">The index in <see cref="Text"/>, clamped to the statement.</param>
    /// <returns>The offset.</returns>
    public int OriginOf(int index) => _origins[Math.Clamp(index, 0, Text.Length)];

    /// <summary>
    ///     Creates the statement of a substring, keeping the original offsets.
    /// </summary>
    /// <param name="start">The start index.</param>
    /// <param name="length">The length.</param>
    /// <returns>The statement of the substring.</returns>
    public SourceStatement Slice(int start, int length)
    {
        var origins = new int[length + 1];
        Array.Copy(_origins, start, origins, 0, length + 1);
        return new SourceStatement(Text.Substring(start, length), origins, Lines);
    }

    /// <summary>
    ///     Gets the statement text.
    /// </summary>
    /// <returns>The text.</returns>
    public override string ToString() => Text;

    /// <summary>
    ///     Builds a statement from pieces of the original text and inserted text.
    /// </summary>
    /// <remarks>
    ///     The builder is used by one call on one thread.
    /// </remarks>
    internal sealed class Builder
    {
        /// <summary>The text built so far.</summary>
        private readonly StringBuilder _text = new();

        /// <summary>The original offset of every character built so far.</summary>
        private readonly List<int> _origins = [];

        /// <summary>
        ///     Gets the length of the text built so far.
        /// </summary>
        public int Length => _text.Length;

        /// <summary>
        ///     Gets a value indicating whether the text built so far is empty or white space only.
        /// </summary>
        public bool IsBlank
        {
            get
            {
                for (var i = 0; i < _text.Length; i++)
                {
                    if (!char.IsWhiteSpace(_text[i]))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>
        ///     Removes the white space at the end of the text built so far.
        /// </summary>
        public void TrimEnd()
        {
            while (_text.Length > 0 && char.IsWhiteSpace(_text[^1]))
            {
                RemoveLast();
            }
        }

        /// <summary>
        ///     Checks whether the text ends with a single backslash, PlantUML's line continuation (a doubled backslash
        ///     is an escaped backslash, not a continuation).
        /// </summary>
        /// <returns>True when the text ends with a continuation.</returns>
        public bool EndsWithContinuation() =>
            _text.Length > 0 && _text[^1] == '\\' && !(_text.Length > 1 && _text[^2] == '\\');

        /// <summary>
        ///     Removes the last character.
        /// </summary>
        public void RemoveLast()
        {
            _text.Length--;
            _origins.RemoveAt(_origins.Count - 1);
        }

        /// <summary>
        ///     Appends the text of another builder with its offsets.
        /// </summary>
        /// <param name="other">The other builder.</param>
        public void AppendFrom(Builder other)
        {
            ArgumentNullException.ThrowIfNull(other);
            for (var i = 0; i < other._text.Length; i++)
            {
                Append(other._text[i], other._origins[i]);
            }
        }

        /// <summary>
        ///     Appends one character with its original offset.
        /// </summary>
        /// <param name="c">The character.</param>
        /// <param name="origin">Its offset in the original text.</param>
        public void Append(char c, int origin)
        {
            _text.Append(c);
            _origins.Add(origin);
        }

        /// <summary>
        ///     Appends inserted text, every character mapped to one original offset.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="origin">The offset of what the text replaces.</param>
        public void AppendInserted(string text, int origin)
        {
            foreach (var c in text)
            {
                Append(c, origin);
            }
        }

        /// <summary>
        ///     Builds the statement, trimmed of white space at both ends.
        /// </summary>
        /// <param name="lines">The line table.</param>
        /// <param name="endOrigin">The original offset of the end of the statement.</param>
        /// <returns>The statement.</returns>
        public SourceStatement Build(SourceLines lines, int endOrigin)
        {
            // Trim while keeping the offsets of the characters that remain
            var start = 0;
            var end = _text.Length;
            while (start < end && char.IsWhiteSpace(_text[start]))
            {
                start++;
            }

            while (end > start && char.IsWhiteSpace(_text[end - 1]))
            {
                end--;
            }

            var origins = new int[end - start + 1];
            for (var i = start; i < end; i++)
            {
                origins[i - start] = _origins[i];
            }

            origins[end - start] = end < _origins.Count ? _origins[end] : endOrigin;
            return new SourceStatement(_text.ToString(start, end - start), origins, lines);
        }
    }
}
