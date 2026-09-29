using System.Globalization;
using System.Text;

namespace DemaConsulting.CanvasNet.Pdf;

public sealed partial class PdfDocument
{
    /// <summary>
    ///     The distinct kinds of low-level lexical token a <see cref="PdfTokenizer"/> can produce.
    /// </summary>
    internal enum PdfTokenKind
    {
        /// <summary>No more tokens remain in the input.</summary>
        EndOfFile,

        /// <summary>An integer or real number, for example <c>12</c>, <c>-3.5</c>, <c>+.5</c>.</summary>
        Number,

        /// <summary>A parenthesized literal string, for example <c>(Hello)</c>.</summary>
        LiteralString,

        /// <summary>A hexadecimal string, for example <c>&lt;48656C6C6F&gt;</c>.</summary>
        HexString,

        /// <summary>A name, for example <c>/Type</c> (the leading slash is not included in <see cref="PdfToken.Text"/>).</summary>
        Name,

        /// <summary>The array-opening delimiter <c>[</c>.</summary>
        ArrayStart,

        /// <summary>The array-closing delimiter <c>]</c>.</summary>
        ArrayEnd,

        /// <summary>The dictionary-opening delimiter <c>&lt;&lt;</c>.</summary>
        DictStart,

        /// <summary>The dictionary-closing delimiter <c>&gt;&gt;</c>.</summary>
        DictEnd,

        /// <summary>
        ///     A bare keyword token: <c>obj</c>, <c>endobj</c>, <c>stream</c>, <c>endstream</c>,
        ///     <c>xref</c>, <c>trailer</c>, <c>startxref</c>, <c>R</c>, <c>true</c>, <c>false</c>,
        ///     or <c>null</c>.
        /// </summary>
        Keyword,
    }

    /// <summary>
    ///     A single lexical token produced by <see cref="PdfTokenizer"/>.
    /// </summary>
    /// <param name="kind">The token's kind.</param>
    /// <param name="number">The decoded numeric value, when <paramref name="kind"/> is <see cref="PdfTokenKind.Number"/>.</param>
    /// <param name="bytes">The decoded raw bytes, when <paramref name="kind"/> is <see cref="PdfTokenKind.LiteralString"/> or <see cref="PdfTokenKind.HexString"/>.</param>
    /// <param name="text">The decoded text, when <paramref name="kind"/> is <see cref="PdfTokenKind.Name"/> or <see cref="PdfTokenKind.Keyword"/>.</param>
    internal readonly struct PdfToken(PdfTokenKind kind, double number = 0, byte[]? bytes = null, string? text = null)
    {
        /// <summary>Gets the token's kind.</summary>
        internal PdfTokenKind Kind { get; } = kind;

        /// <summary>Gets the decoded numeric value for a <see cref="PdfTokenKind.Number"/> token.</summary>
        internal double Number { get; } = number;

        /// <summary>Gets the decoded raw bytes for a string token.</summary>
        internal byte[]? Bytes { get; } = bytes;

        /// <summary>Gets the decoded text for a name or keyword token.</summary>
        internal string? Text { get; } = text;
    }

    /// <summary>
    ///     A low-level, stateless-per-call lexer over a complete in-memory PDF document buffer.
    /// </summary>
    /// <remarks>
    ///     Character classification (whitespace, delimiter, regular) follows the PDF
    ///     specification's own character classes. Whitespace bytes are: NUL, HT, LF, FF, CR, and
    ///     space. Delimiter bytes are: <c>( ) &lt; &gt; [ ] { } / %</c>. Every other byte is
    ///     "regular" and may appear in a name, keyword, or number.
    /// </remarks>
    internal sealed class PdfTokenizer
    {
        private readonly byte[] _buffer;

        /// <summary>
        ///     Initializes a new instance of the <see cref="PdfTokenizer"/> class over the given
        ///     buffer, starting at position zero.
        /// </summary>
        /// <param name="buffer">The complete document (or object-stream) bytes to tokenize.</param>
        internal PdfTokenizer(byte[] buffer) => _buffer = buffer;

        /// <summary>
        ///     Gets or sets the current zero-based byte offset within the buffer. Callers may
        ///     save and restore this value to implement bounded lookahead (for example,
        ///     distinguishing <c>N G R</c> indirect references from a bare number).
        /// </summary>
        internal int Position { get; set; }

        private int Length => _buffer.Length;

        /// <summary>
        ///     Reads and returns the next token, first skipping any leading whitespace and
        ///     <c>%</c> comments.
        /// </summary>
        /// <returns>The next token, or a token of kind <see cref="PdfTokenKind.EndOfFile"/>.</returns>
        /// <exception cref="System.IO.InvalidDataException">
        ///     Thrown when the input contains a character that cannot begin any valid token.
        /// </exception>
        internal PdfToken NextToken()
        {
            SkipWhitespaceAndComments();

            if (Position >= Length)
            {
                return new PdfToken(PdfTokenKind.EndOfFile);
            }

            var b = _buffer[Position];
            switch (b)
            {
                case (byte)'[':
                    Position++;
                    return new PdfToken(PdfTokenKind.ArrayStart);

                case (byte)']':
                    Position++;
                    return new PdfToken(PdfTokenKind.ArrayEnd);

                case (byte)'/':
                    return ReadName();

                case (byte)'(':
                    return ReadLiteralString();

                case (byte)'<':
                    if (Position + 1 < Length && _buffer[Position + 1] == (byte)'<')
                    {
                        Position += 2;
                        return new PdfToken(PdfTokenKind.DictStart);
                    }

                    return ReadHexString();

                case (byte)'>':
                    if (Position + 1 < Length && _buffer[Position + 1] == (byte)'>')
                    {
                        Position += 2;
                        return new PdfToken(PdfTokenKind.DictEnd);
                    }

                    throw new InvalidDataException("Unexpected '>' in PDF content.");

                case (byte)'+':
                case (byte)'-':
                case (byte)'.':
                    return ReadNumber();
            }

            if (b is >= (byte)'0' and <= (byte)'9')
            {
                return ReadNumber();
            }

            if (IsRegular(b))
            {
                return ReadKeyword();
            }

            throw new InvalidDataException($"Unexpected character 0x{b:X2} in PDF content.");
        }

        /// <summary>
        ///     Gets the raw byte at the current position without consuming it, or
        ///     <see langword="null"/> at end of buffer.
        /// </summary>
        internal byte? PeekByte() => Position < Length ? _buffer[Position] : null;

        /// <summary>Advances <see cref="Position"/> by one raw byte, bypassing tokenization.</summary>
        internal void AdvanceRaw() => Position++;

        private void SkipWhitespaceAndComments()
        {
            while (Position < Length)
            {
                var b = _buffer[Position];
                if (IsWhitespace(b))
                {
                    Position++;
                    continue;
                }

                if (b == (byte)'%')
                {
                    while (Position < Length && _buffer[Position] != (byte)'\n' && _buffer[Position] != (byte)'\r')
                    {
                        Position++;
                    }

                    continue;
                }

                break;
            }
        }

        private PdfToken ReadName()
        {
            Position++; // skip the leading '/'
            var bytes = new List<byte>();
            while (Position < Length && IsRegular(_buffer[Position]))
            {
                var b = _buffer[Position];
                if (b == (byte)'#' && Position + 2 < Length && IsHexDigit(_buffer[Position + 1]) && IsHexDigit(_buffer[Position + 2]))
                {
                    bytes.Add((byte)((HexValue(_buffer[Position + 1]) << 4) | HexValue(_buffer[Position + 2])));
                    Position += 3;
                }
                else
                {
                    bytes.Add(b);
                    Position++;
                }
            }

            return new PdfToken(PdfTokenKind.Name, text: Encoding.Latin1.GetString(bytes.ToArray()));
        }

        private PdfToken ReadLiteralString()
        {
            Position++; // skip the opening '('
            var depth = 1;
            var bytes = new List<byte>();

            while (Position < Length)
            {
                var b = _buffer[Position];

                if (b == (byte)'\\')
                {
                    Position++;
                    if (Position >= Length)
                    {
                        break;
                    }

                    var escaped = _buffer[Position];
                    switch (escaped)
                    {
                        case (byte)'n':
                            bytes.Add((byte)'\n');
                            Position++;
                            break;
                        case (byte)'r':
                            bytes.Add((byte)'\r');
                            Position++;
                            break;
                        case (byte)'t':
                            bytes.Add((byte)'\t');
                            Position++;
                            break;
                        case (byte)'b':
                            bytes.Add(0x08);
                            Position++;
                            break;
                        case (byte)'f':
                            bytes.Add(0x0C);
                            Position++;
                            break;
                        case (byte)'(':
                            bytes.Add((byte)'(');
                            Position++;
                            break;
                        case (byte)')':
                            bytes.Add((byte)')');
                            Position++;
                            break;
                        case (byte)'\\':
                            bytes.Add((byte)'\\');
                            Position++;
                            break;
                        case (byte)'\r':
                            // Backslash-newline is a line continuation: no byte is emitted.
                            Position++;
                            if (Position < Length && _buffer[Position] == (byte)'\n')
                            {
                                Position++;
                            }

                            break;
                        case (byte)'\n':
                            Position++;
                            break;
                        default:
                            if (escaped is >= (byte)'0' and <= (byte)'7')
                            {
                                var value = 0;
                                var digits = 0;
                                while (digits < 3 && Position < Length && _buffer[Position] is >= (byte)'0' and <= (byte)'7')
                                {
                                    value = (value * 8) + (_buffer[Position] - (byte)'0');
                                    Position++;
                                    digits++;
                                }

                                bytes.Add((byte)(value & 0xFF));
                            }
                            else
                            {
                                // An unrecognized escape simply yields the escaped character
                                // itself, per the PDF specification.
                                bytes.Add(escaped);
                                Position++;
                            }

                            break;
                    }

                    continue;
                }

                if (b == (byte)'(')
                {
                    depth++;
                    bytes.Add(b);
                    Position++;
                    continue;
                }

                if (b == (byte)')')
                {
                    depth--;
                    Position++;
                    if (depth == 0)
                    {
                        break;
                    }

                    bytes.Add(b);
                    continue;
                }

                bytes.Add(b);
                Position++;
            }

            return new PdfToken(PdfTokenKind.LiteralString, bytes: bytes.ToArray());
        }

        private PdfToken ReadHexString()
        {
            Position++; // skip the opening '<'
            var digits = new List<byte>();

            while (Position < Length && _buffer[Position] != (byte)'>')
            {
                var b = _buffer[Position];
                if (IsHexDigit(b))
                {
                    digits.Add(b);
                }
                else if (!IsWhitespace(b))
                {
                    throw new InvalidDataException("Invalid character in hexadecimal string.");
                }

                Position++;
            }

            if (Position >= Length)
            {
                throw new InvalidDataException("Unterminated hexadecimal string.");
            }

            Position++; // skip the closing '>'

            if (digits.Count % 2 != 0)
            {
                digits.Add((byte)'0');
            }

            var bytes = new byte[digits.Count / 2];
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)((HexValue(digits[2 * i]) << 4) | HexValue(digits[(2 * i) + 1]));
            }

            return new PdfToken(PdfTokenKind.HexString, bytes: bytes);
        }

        private PdfToken ReadNumber()
        {
            var start = Position;
            if (_buffer[Position] is (byte)'+' or (byte)'-')
            {
                Position++;
            }

            while (Position < Length && (_buffer[Position] is (byte)'.' or (>= (byte)'0' and <= (byte)'9')))
            {
                Position++;
            }

            var text = Encoding.ASCII.GetString(_buffer, start, Position - start);
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                throw new InvalidDataException($"Malformed numeric token '{text}' in PDF content.");
            }

            return new PdfToken(PdfTokenKind.Number, number: value);
        }

        private PdfToken ReadKeyword()
        {
            var start = Position;
            while (Position < Length && IsRegular(_buffer[Position]))
            {
                Position++;
            }

            return new PdfToken(PdfTokenKind.Keyword, text: Encoding.ASCII.GetString(_buffer, start, Position - start));
        }

        private static bool IsWhitespace(byte b) => b is 0x00 or 0x09 or 0x0A or 0x0C or 0x0D or 0x20;

        private static bool IsDelimiter(byte b) =>
            b is (byte)'(' or (byte)')' or (byte)'<' or (byte)'>' or (byte)'[' or (byte)']' or (byte)'{' or (byte)'}' or (byte)'/' or (byte)'%';

        private static bool IsRegular(byte b) => !IsWhitespace(b) && !IsDelimiter(b);

        private static bool IsHexDigit(byte b) => b is (>= (byte)'0' and <= (byte)'9') or (>= (byte)'a' and <= (byte)'f') or (>= (byte)'A' and <= (byte)'F');

        private static int HexValue(byte b) => b switch
        {
            >= (byte)'0' and <= (byte)'9' => b - (byte)'0',
            >= (byte)'a' and <= (byte)'f' => b - (byte)'a' + 10,
            >= (byte)'A' and <= (byte)'F' => b - (byte)'A' + 10,
            _ => throw new InvalidDataException("Invalid hexadecimal digit."),
        };
    }
}
