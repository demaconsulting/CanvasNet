using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace DemaConsulting.CanvasNet.MathMl.Syntax;

/// <summary>
///     Rewrites the named character references of MathML text into numeric references before the text is given to the
///     XML reader, and maps the positions of the rewritten text back to the original text.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why.</b> MathML is usually written with the named references of the W3C HTML+MathML set (<c>&amp;alpha;</c>,
///         <c>&amp;InvisibleTimes;</c>, <c>&amp;int;</c>), which an XML reader only knows from a document type
///         declaration. Untrusted XML is read with document types prohibited (the vehicle of entity-expansion and
///         external-entity attacks), so the references are rewritten here, with the full built-in W3C set and no document
///         type, into numeric references (<c>&amp;#x3B1;</c>) that every XML reader decodes. A numeric reference, never the
///         literal character, is written, because the values of <c>&amp;LT;</c>, <c>&amp;AMP;</c> and <c>&amp;nvlt;</c>
///         contain markup characters.
///     </para>
///     <para>
///         <b>Where.</b> One linear scan follows the markup: references are rewritten in text and in attribute values, and
///         left alone inside comments, CDATA sections and processing instructions. Numeric references and the five
///         predefined XML names (<c>lt</c>, <c>gt</c>, <c>amp</c>, <c>quot</c>, <c>apos</c>) are kept as written. A name
///         that the set does not define is a located <see cref="MathMlSyntaxException"/> at its ampersand. A document type
///         declaration stops the rewriting, so that the XML reader refuses it. An XML declaration that names an encoding
///         other than UTF-8 (US-ASCII included) is refused, because the text has already been decoded as UTF-8 and is read
///         as UTF-8: a declared ASCII would make the XML reader decode every other character as <c>?</c>.
///     </para>
///     <para>
///         <b>Locations.</b> A reference never spans a line break, so the lines of the rewritten text are the lines of the
///         original; each rewritten line keeps the column shifts of its replacements in a <see cref="LocationMap"/>,
///         which maps every position the XML reader reports back to the original text.
///     </para>
///     <para>
///         The class is stateless and safe for concurrent use; the table is built once.
///     </para>
/// </remarks>
internal static partial class CharacterReferences
{
    /// <summary>The longest name that is read as a reference (the longest W3C name has 31 characters).</summary>
    private const int MaxNameLength = 32;

    /// <summary>The five predefined XML names; they are left for the XML reader.</summary>
    private static readonly FrozenSet<string> XmlNames = new[] { "lt", "gt", "amp", "quot", "apos" }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>The named references by name (ordinal, case-sensitive), built from the generated table.</summary>
    private static readonly FrozenDictionary<string, string> Named = BuildTable();

    /// <summary>
    ///     Gets the number of named references of the built-in table (the full W3C HTML+MathML set).
    /// </summary>
    public static int Count => Named.Count;

    /// <summary>
    ///     Looks a named reference up.
    /// </summary>
    /// <param name="name">The name, without the ampersand and the semicolon (case-sensitive).</param>
    /// <param name="value">The value (one or two code points) when the name is known.</param>
    /// <returns>True when the W3C HTML+MathML set defines the name.</returns>
    public static bool TryGetValue(string name, [NotNullWhen(true)] out string? value) => Named.TryGetValue(name, out value);

    /// <summary>
    ///     Rewrites the named references of a text into numeric references.
    /// </summary>
    /// <param name="text">The original text.</param>
    /// <param name="sourceName">The source name, for the locations of errors.</param>
    /// <returns>The rewritten text, the map of its positions back to the original, and the location of its document type declaration.</returns>
    /// <exception cref="MathMlSyntaxException">Thrown, located at the ampersand, for a name that the set does not define, or at the declaration for a foreign encoding.</exception>
    public static PrePassResult Rewrite(string text, string? sourceName)
    {
        ArgumentNullException.ThrowIfNull(text);
        var scanner = new Scanner(text, sourceName);
        scanner.Run();
        return new PrePassResult(scanner.Output.ToString(), scanner.Map, scanner.DoctypeLocation);
    }

    /// <summary>
    ///     Computes the one-based line and column of a position of a text, counting lines and columns as the XML reader
    ///     does (a line break is <c>\r\n</c>, a lone <c>\r</c> or <c>\n</c>; a column is a UTF-16 code unit).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The position, from 0 to the length of the text.</param>
    /// <returns>The line and the column.</returns>
    public static (int Line, int Column) PositionOf(string text, int index)
    {
        var line = 1;
        var column = 1;
        for (var i = 0; i < index && i < text.Length; i++)
        {
            // A CR followed by an LF is one line break, counted at the LF
            if (text[i] == '\n' || (text[i] == '\r' && (i + 1 >= text.Length || text[i + 1] != '\n')))
            {
                line++;
                column = 1;
            }
            else if (text[i] != '\r')
            {
                column++;
            }
        }

        return (line, column);
    }

    /// <summary>
    ///     Builds the frozen table from the generated name and value pairs.
    /// </summary>
    /// <returns>The table.</returns>
    private static FrozenDictionary<string, string> BuildTable()
    {
        var pairs = NamedPairs;
        var table = new Dictionary<string, string>(pairs.Length / 2, StringComparer.Ordinal);
        for (var i = 0; i < pairs.Length; i += 2)
        {
            table.Add(pairs[i], pairs[i + 1]);
        }

        return table.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>
    ///     The state of the markup scan.
    /// </summary>
    private enum ScanState
    {
        /// <summary>Character data between tags; references are rewritten.</summary>
        Text,

        /// <summary>Inside a start or end tag; references in attribute values are rewritten.</summary>
        Tag,

        /// <summary>Inside a comment; copied as written.</summary>
        Comment,

        /// <summary>Inside a CDATA section; copied as written.</summary>
        CData,

        /// <summary>Inside a processing instruction or the XML declaration; copied as written.</summary>
        ProcessingInstruction,
    }

    /// <summary>
    ///     The single-use scanner of one rewrite: the position in the original text, its line and column, and the output.
    /// </summary>
    private sealed class Scanner
    {
        /// <summary>The original text.</summary>
        private readonly string _text;

        /// <summary>The source name, for the locations of errors.</summary>
        private readonly string? _sourceName;

        /// <summary>The position in the original text.</summary>
        private int _position;

        /// <summary>The one-based line of <see cref="_position"/>.</summary>
        private int _line = 1;

        /// <summary>The one-based column of <see cref="_position"/> in the original text.</summary>
        private int _column = 1;

        /// <summary>The column shift of the output against the original on the current line.</summary>
        private int _lineDelta;

        /// <summary>The scan state.</summary>
        private ScanState _state = ScanState.Text;

        /// <summary>The quote that closes the current attribute value, or zero outside a value.</summary>
        private char _quote;

        /// <summary>
        ///     Initializes a scanner.
        /// </summary>
        /// <param name="text">The original text.</param>
        /// <param name="sourceName">The source name.</param>
        public Scanner(string text, string? sourceName)
        {
            _text = text;
            _sourceName = sourceName;
            Output = new StringBuilder(text.Length);
            Map = new LocationMap(sourceName);
        }

        /// <summary>Gets the rewritten text.</summary>
        public StringBuilder Output { get; }

        /// <summary>Gets the map of the rewritten positions.</summary>
        public LocationMap Map { get; }

        /// <summary>Gets the location of the document type declaration of the text, or null when it has none.</summary>
        public MathMlLocation? DoctypeLocation { get; private set; }

        /// <summary>
        ///     Scans the whole text.
        /// </summary>
        public void Run()
        {
            while (_position < _text.Length)
            {
                switch (_state)
                {
                    case ScanState.Text:
                        ScanText();
                        break;
                    case ScanState.Tag:
                        ScanTag();
                        break;
                    case ScanState.Comment:
                        CopyUntil("-->");
                        break;
                    case ScanState.CData:
                        CopyUntil("]]>");
                        break;
                    default:
                        CopyUntil("?>");
                        break;
                }
            }
        }

        /// <summary>
        ///     Scans one step of character data: a markup opening, a reference, or a character.
        /// </summary>
        private void ScanText()
        {
            var c = _text[_position];
            if (c == '&')
            {
                Reference();
                return;
            }

            if (c != '<')
            {
                Copy(1);
                return;
            }

            // The markup that starts here decides the next state
            if (StartsWith("<!--"))
            {
                Copy(4);
                _state = ScanState.Comment;
            }
            else if (StartsWith("<![CDATA["))
            {
                Copy(9);
                _state = ScanState.CData;
            }
            else if (StartsWith("<!"))
            {
                // A document type (or another declaration): stop rewriting, so that the XML reader refuses it as written
                DoctypeLocation = new MathMlLocation(_sourceName, _line, _column);
                Copy(_text.Length - _position);
            }
            else if (StartsWith("<?"))
            {
                CheckXmlDeclaration();
                Copy(2);
                _state = ScanState.ProcessingInstruction;
            }
            else
            {
                Copy(1);
                _state = ScanState.Tag;
            }
        }

        /// <summary>
        ///     Scans one step inside a tag: a quote, the end of the tag, a reference in an attribute value, or a character.
        /// </summary>
        private void ScanTag()
        {
            var c = _text[_position];
            if (_quote == 0)
            {
                if (c is '"' or '\'')
                {
                    _quote = c;
                }
                else if (c == '>')
                {
                    _state = ScanState.Text;
                }

                Copy(1);
                return;
            }

            if (c == _quote)
            {
                _quote = '\0';
                Copy(1);
            }
            else if (c == '&')
            {
                Reference();
            }
            else
            {
                Copy(1);
            }
        }

        /// <summary>
        ///     Rewrites, keeps or refuses the reference that starts at the current ampersand.
        /// </summary>
        /// <exception cref="MathMlSyntaxException">Thrown for a name that the set does not define.</exception>
        private void Reference()
        {
            // Measure the name: ASCII letters and digits up to the longest name, then a semicolon
            var end = _position + 1;
            while (end < _text.Length && end - _position - 1 <= MaxNameLength && char.IsAsciiLetterOrDigit(_text[end]))
            {
                end++;
            }

            var nameLength = end - _position - 1;
            if (nameLength < 1 || nameLength > MaxNameLength || end >= _text.Length || _text[end] != ';')
            {
                // A numeric reference, or text that is no reference: the XML reader decides
                Copy(1);
                return;
            }

            var name = _text.Substring(_position + 1, nameLength);
            if (XmlNames.Contains(name))
            {
                Copy(nameLength + 2);
                return;
            }

            if (!Named.TryGetValue(name, out var value))
            {
                throw new MathMlSyntaxException(
                    MathMlDiagnosticCodes.SyntaxError,
                    $"'&{name};' is not a named character reference of the W3C HTML+MathML set",
                    new MathMlLocation(_sourceName, _line, _column),
                    "check the spelling (names are case-sensitive) or use a numeric character reference such as &#x2062;");
            }

            // Write one numeric reference per code point, and record the column shift it causes on this line
            var replacement = new StringBuilder();
            foreach (var rune in value.EnumerateRunes())
            {
                replacement.Append(CultureInfo.InvariantCulture, $"&#x{rune.Value:X};");
            }

            var outputColumn = _column + _lineDelta;
            var originalLength = nameLength + 2;
            Map.Add(_line, new LocationMap.Breakpoint(outputColumn, 0, _column));
            _lineDelta += replacement.Length - originalLength;
            Map.Add(_line, new LocationMap.Breakpoint(outputColumn + replacement.Length, _lineDelta, 0));
            Output.Append(replacement);
            _position += originalLength;
            _column += originalLength;
        }

        /// <summary>
        ///     Refuses an XML declaration that names an encoding other than UTF-8 (in any letter case).
        /// </summary>
        /// <exception cref="MathMlSyntaxException">Thrown, located at the encoding, for a foreign encoding.</exception>
        private void CheckXmlDeclaration()
        {
            // Only the declaration itself, <?xml followed by whitespace, names an encoding
            if (!StartsWith("<?xml") || _position + 5 >= _text.Length || !char.IsWhiteSpace(_text[_position + 5]))
            {
                return;
            }

            var close = _text.IndexOf("?>", _position, StringComparison.Ordinal);
            var declaration = close < 0 ? _text[_position..] : _text[_position..close];
            var match = EncodingPattern().Match(declaration);
            if (!match.Success)
            {
                return;
            }

            var encoding = match.Groups[2].Value;
            if (encoding.Equals("UTF-8", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Locate the encoding name from the start of the declaration, whose position is known
            var (lineOffset, column) = PositionOf(declaration, match.Index);
            var location = new MathMlLocation(_sourceName, _line + lineOffset - 1, lineOffset == 1 ? _column + column - 1 : column);
            throw new MathMlSyntaxException(
                MathMlDiagnosticCodes.SyntaxError,
                $"the XML declaration names the encoding '{encoding}'; MathML text is read as UTF-8",
                location,
                "save the file as UTF-8 and declare encoding=\"UTF-8\" (or omit the declaration)");
        }

        /// <summary>
        ///     Copies characters up to and including a terminator, and returns to the text state after it; copies the rest
        ///     of the text when the terminator is missing (the XML reader reports the unclosed construct).
        /// </summary>
        /// <param name="terminator">The terminator of the current construct.</param>
        private void CopyUntil(string terminator)
        {
            var index = _text.IndexOf(terminator, _position, StringComparison.Ordinal);
            if (index < 0)
            {
                Copy(_text.Length - _position);
                return;
            }

            Copy(index + terminator.Length - _position);
            _state = ScanState.Text;
        }

        /// <summary>
        ///     Decides whether the text continues with a string at the current position.
        /// </summary>
        /// <param name="value">The string.</param>
        /// <returns>True when it does.</returns>
        private bool StartsWith(string value) => string.CompareOrdinal(_text, _position, value, 0, value.Length) == 0;

        /// <summary>
        ///     Copies characters unchanged, keeping the line and the column of the original text.
        /// </summary>
        /// <param name="count">The number of UTF-16 code units to copy.</param>
        private void Copy(int count)
        {
            var end = _position + count;
            Output.Append(_text, _position, count);
            for (; _position < end; _position++)
            {
                var c = _text[_position];
                if (c == '\n' || (c == '\r' && (_position + 1 >= _text.Length || _text[_position + 1] != '\n')))
                {
                    // A new line starts with no shift
                    _line++;
                    _column = 1;
                    _lineDelta = 0;
                }
                else if (c != '\r')
                {
                    _column++;
                }
            }
        }
    }

    /// <summary>
    ///     The pattern of the encoding pseudo-attribute of an XML declaration.
    /// </summary>
    /// <returns>The pattern; group 2 is the encoding name.</returns>
    [System.Text.RegularExpressions.GeneratedRegex("""encoding\s*=\s*(["'])([^"']*)\1""", System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex EncodingPattern();
}

/// <summary>
///     The result of the reference pre-pass: the rewritten text and the map of its positions back to the original.
/// </summary>
/// <param name="Text">The rewritten text, with numeric references only.</param>
/// <param name="Map">The map of the rewritten positions back to the original text.</param>
/// <param name="DoctypeLocation">The location of the document type declaration (which the XML reader refuses), or null when the text has none.</param>
internal sealed record PrePassResult(string Text, LocationMap Map, MathMlLocation? DoctypeLocation);

/// <summary>
///     Maps a line and column of the rewritten text back to the original text.
/// </summary>
/// <remarks>
///     The lines of the two texts are the same; on a line with replacements, a sorted list of breakpoints records where
///     each replacement starts (columns inside it map to its ampersand) and the shift of the columns after it. Lines
///     without a replacement map to themselves. An instance is filled by one pre-pass and only read afterwards, so a
///     finished map is safe for concurrent reads.
/// </remarks>
internal sealed class LocationMap
{
    /// <summary>The breakpoints of the lines that have replacements, in ascending order of output column.</summary>
    private readonly Dictionary<int, List<Breakpoint>> _lines = [];

    /// <summary>
    ///     Initializes an empty map.
    /// </summary>
    /// <param name="sourceName">The source name placed in the mapped locations.</param>
    public LocationMap(string? sourceName)
    {
        SourceName = sourceName;
    }

    /// <summary>
    ///     Gets the source name placed in the mapped locations.
    /// </summary>
    public string? SourceName { get; }

    /// <summary>
    ///     Maps a position of the rewritten text back to the original text.
    /// </summary>
    /// <param name="line">The one-based line, or zero when unknown.</param>
    /// <param name="column">The one-based column in UTF-16 code units of the rewritten text.</param>
    /// <returns>The location in the original text; unknown when the line is not positive.</returns>
    public MathMlLocation MapBack(int line, int column)
    {
        if (line <= 0)
        {
            return new MathMlLocation(SourceName, 0, 0);
        }

        if (!_lines.TryGetValue(line, out var breakpoints))
        {
            return new MathMlLocation(SourceName, line, column);
        }

        // The last breakpoint at or before the column decides: inside a replacement, or shifted after one (a binary
        // search, because one long line can hold many replacements and many positions)
        var low = 0;
        var high = breakpoints.Count - 1;
        var found = -1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            if (breakpoints[middle].OutputColumn <= column)
            {
                found = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        if (found < 0)
        {
            return new MathMlLocation(SourceName, line, column);
        }

        var breakpoint = breakpoints[found];
        return new MathMlLocation(SourceName, line, breakpoint.PinnedColumn > 0 ? breakpoint.PinnedColumn : column - breakpoint.Delta);
    }

    /// <summary>
    ///     Adds a breakpoint to a line; breakpoints are added in ascending order of output column.
    /// </summary>
    /// <param name="line">The one-based line.</param>
    /// <param name="breakpoint">The breakpoint.</param>
    internal void Add(int line, Breakpoint breakpoint)
    {
        if (!_lines.TryGetValue(line, out var breakpoints))
        {
            breakpoints = [];
            _lines.Add(line, breakpoints);
        }

        breakpoints.Add(breakpoint);
    }

    /// <summary>
    ///     A change of the column mapping at a column of the rewritten text.
    /// </summary>
    /// <param name="OutputColumn">The first column of the rewritten text that the breakpoint applies to.</param>
    /// <param name="Delta">The shift (rewritten minus original) of the columns from here on, when not pinned.</param>
    /// <param name="PinnedColumn">The original column that every column from here on maps to (inside a replacement), or zero.</param>
    internal readonly record struct Breakpoint(int OutputColumn, int Delta, int PinnedColumn);
}
