namespace DemaConsulting.CanvasNet.Mermaid.Syntax;

/// <summary>
///     Converts offsets of the original diagram text to one-based line and column locations.
/// </summary>
/// <remarks>
///     A line ends at <c>\n</c>, at <c>\r\n</c> or at a lone <c>\r</c>, the three line endings that Mermaid's
///     preprocessing turns into <c>\n</c>, so a location points at the same line an editor shows. Columns count UTF-16
///     code units. The instance is immutable and safe for concurrent use.
/// </remarks>
internal sealed class SourceLines
{
    /// <summary>The offsets at which each line starts, ascending; the first is zero.</summary>
    private readonly int[] _lineStarts;

    /// <summary>
    ///     Initializes the line table of a text.
    /// </summary>
    /// <param name="text">The original text.</param>
    /// <param name="sourceName">The source name placed in every location, or null.</param>
    public SourceLines(string text, string? sourceName)
    {
        Text = text;
        SourceName = sourceName;

        // Record the start of every line; \r\n counts as one break
        var starts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n' || (text[i] == '\r' && (i + 1 >= text.Length || text[i + 1] != '\n')))
            {
                starts.Add(i + 1);
            }
        }

        _lineStarts = [.. starts];
    }

    /// <summary>
    ///     Gets the original text.
    /// </summary>
    public string Text { get; }

    /// <summary>
    ///     Gets the source name placed in every location, or null.
    /// </summary>
    public string? SourceName { get; }

    /// <summary>
    ///     Gets the location of an offset of the original text.
    /// </summary>
    /// <param name="offset">The offset, clamped to 0 to the length of the text.</param>
    /// <returns>The one-based location.</returns>
    public MermaidLocation Locate(int offset)
    {
        offset = Math.Clamp(offset, 0, Text.Length);

        // The line is the last one that starts at or before the offset
        var index = Array.BinarySearch(_lineStarts, offset);
        if (index < 0)
        {
            index = ~index - 1;
        }

        return new MermaidLocation(SourceName, index + 1, offset - _lineStarts[index] + 1);
    }
}

/// <summary>
///     A text derived from the original diagram text that remembers, for every character, the offset in the original
///     text it came from.
/// </summary>
/// <remarks>
///     <para>
///         Mermaid's preprocessing changes the text before the parser sees it (line endings, the frontmatter, directives,
///         comment lines and entity encoding are rewritten or removed), yet every diagnostic must point into the text the
///         author wrote. Each transformation therefore builds a new mapped text from slices of the previous one (whose
///         characters keep their origins) and inserted text (whose characters take the origin of what they replace), so a
///         location is always one lookup away. One origin is kept per character plus one for the end, which bounds the
///         memory to a small multiple of the text.
///     </para>
///     <para>
///         The instance is immutable and safe for concurrent use.
///     </para>
/// </remarks>
internal sealed class MappedText
{
    /// <summary>The origin of every character, plus one entry for the end of the text.</summary>
    private readonly int[] _origins;

    /// <summary>
    ///     Initializes a mapped text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="origins">The origin of every character plus the end (length + 1 entries).</param>
    /// <param name="lines">The line table of the original text.</param>
    private MappedText(string text, int[] origins, SourceLines lines)
    {
        Text = text;
        _origins = origins;
        Lines = lines;
    }

    /// <summary>
    ///     Gets the text.
    /// </summary>
    public string Text { get; }

    /// <summary>
    ///     Gets the line table of the original text.
    /// </summary>
    public SourceLines Lines { get; }

    /// <summary>
    ///     Gets the number of characters of the text.
    /// </summary>
    public int Length => Text.Length;

    /// <summary>
    ///     Creates the mapped text of the original text itself (every character maps to its own offset).
    /// </summary>
    /// <param name="lines">The line table of the original text.</param>
    /// <returns>The mapped text.</returns>
    public static MappedText Identity(SourceLines lines)
    {
        var origins = new int[lines.Text.Length + 1];
        for (var i = 0; i < origins.Length; i++)
        {
            origins[i] = i;
        }

        return new MappedText(lines.Text, origins, lines);
    }

    /// <summary>
    ///     Gets the offset in the original text of a position of this text.
    /// </summary>
    /// <param name="index">The position, clamped to 0 to <see cref="Length"/> (the end maps to the end of what came last).</param>
    /// <returns>The original offset.</returns>
    public int OriginOf(int index) => _origins[Math.Clamp(index, 0, Text.Length)];

    /// <summary>
    ///     Gets the location in the original text of a position of this text.
    /// </summary>
    /// <param name="index">The position, clamped to 0 to <see cref="Length"/>.</param>
    /// <returns>The location.</returns>
    public MermaidLocation Locate(int index) => Lines.Locate(OriginOf(index));

    /// <summary>
    ///     Gets a part of the text that keeps its origins.
    /// </summary>
    /// <param name="start">The first position.</param>
    /// <param name="length">The number of characters.</param>
    /// <returns>The part.</returns>
    public MappedText Slice(int start, int length)
    {
        var builder = new Builder(this);
        builder.Append(start, length);
        return builder.Build(OriginOf(start + length));
    }

    /// <summary>
    ///     Builds a mapped text from slices of a source mapped text and inserted text.
    /// </summary>
    /// <remarks>
    ///     A builder is used by one transformation on one thread.
    /// </remarks>
    internal sealed class Builder
    {
        /// <summary>The text the slices come from.</summary>
        private readonly MappedText _source;

        /// <summary>The characters built so far.</summary>
        private readonly System.Text.StringBuilder _text = new();

        /// <summary>The origins of the characters built so far.</summary>
        private readonly List<int> _origins = [];

        /// <summary>
        ///     Initializes a builder over a source text.
        /// </summary>
        /// <param name="source">The text the slices come from.</param>
        public Builder(MappedText source)
        {
            _source = source;
        }

        /// <summary>
        ///     Gets the number of characters built so far.
        /// </summary>
        public int Length => _text.Length;

        /// <summary>
        ///     Appends a slice of the source text with its origins.
        /// </summary>
        /// <param name="start">The first position in the source text.</param>
        /// <param name="length">The number of characters.</param>
        public void Append(int start, int length)
        {
            _text.Append(_source.Text, start, length);
            for (var i = 0; i < length; i++)
            {
                _origins.Add(_source._origins[start + i]);
            }
        }

        /// <summary>
        ///     Appends text that does not come from the source, mapped to one original offset.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="origin">The original offset every character of the text maps to (that of what it replaces).</param>
        public void AppendInserted(string text, int origin)
        {
            _text.Append(text);
            for (var i = 0; i < text.Length; i++)
            {
                _origins.Add(origin);
            }
        }

        /// <summary>
        ///     Finishes the text.
        /// </summary>
        /// <param name="endOrigin">The original offset of the end of the text.</param>
        /// <returns>The mapped text.</returns>
        public MappedText Build(int endOrigin)
        {
            _origins.Add(endOrigin);
            return new MappedText(_text.ToString(), [.. _origins], _source.Lines);
        }
    }
}
