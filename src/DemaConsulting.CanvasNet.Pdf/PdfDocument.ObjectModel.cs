namespace DemaConsulting.CanvasNet.Pdf;

public sealed partial class PdfDocument
{
    /// <summary>
    ///     The distinct kinds of value a <see cref="PdfObject"/> can represent.
    /// </summary>
    internal enum PdfKind
    {
        /// <summary>The PDF <c>null</c> value.</summary>
        Null,

        /// <summary>A PDF boolean (<c>true</c>/<c>false</c>).</summary>
        Boolean,

        /// <summary>A PDF integer or real number.</summary>
        Number,

        /// <summary>A PDF literal or hexadecimal string, decoded to raw bytes.</summary>
        String,

        /// <summary>A PDF name (for example <c>/Type</c>), decoded to text without the leading slash.</summary>
        Name,

        /// <summary>A PDF array.</summary>
        Array,

        /// <summary>A PDF dictionary.</summary>
        Dictionary,

        /// <summary>A PDF stream (a dictionary plus a raw data byte range).</summary>
        Stream,

        /// <summary>An indirect reference (<c>N G R</c>).</summary>
        Reference,
    }

    /// <summary>
    ///     An internal tagged-union representation of a parsed PDF value. This type is not part of
    ///     this package's public API - it exists purely to support this document's own parsing.
    /// </summary>
    internal sealed class PdfObject
    {
        /// <summary>Gets the kind of value this instance represents.</summary>
        internal PdfKind Kind { get; private init; }

        /// <summary>Gets the boolean value, when <see cref="Kind"/> is <see cref="PdfKind.Boolean"/>.</summary>
        internal bool Boolean { get; private init; }

        /// <summary>Gets the numeric value, when <see cref="Kind"/> is <see cref="PdfKind.Number"/>.</summary>
        internal double Number { get; private init; }

        /// <summary>
        ///     Gets or sets the raw string bytes, when <see cref="Kind"/> is
        ///     <see cref="PdfKind.String"/>. Settable (rather than <c>private init</c>, unlike
        ///     every other field on this type) solely so <c>PdfDocument.Encryption.cs</c>'s
        ///     <c>DecryptStringsInPlace</c> can overwrite already-parsed ciphertext with plaintext
        ///     in place, without rebuilding the surrounding object tree. This setter must never be
        ///     called from anywhere else - a cached <see cref="PdfObject"/> instance is shared by
        ///     every caller that resolves the same object number, so mutating it outside the
        ///     one-time, pre-cache-return decrypt step would corrupt every other holder's view of
        ///     the same object.
        /// </summary>
        internal byte[] Bytes { get; set; } = [];

        /// <summary>Gets the name/keyword text, when <see cref="Kind"/> is <see cref="PdfKind.Name"/>.</summary>
        internal string Text { get; private init; } = string.Empty;

        /// <summary>Gets the array elements, when <see cref="Kind"/> is <see cref="PdfKind.Array"/>.</summary>
        internal List<PdfObject> Items { get; private init; } = [];

        /// <summary>
        ///     Gets the dictionary entries (keyed by name text, without the leading slash), when
        ///     <see cref="Kind"/> is <see cref="PdfKind.Dictionary"/> or <see cref="PdfKind.Stream"/>.
        /// </summary>
        internal Dictionary<string, PdfObject> Entries { get; private init; } = new();

        /// <summary>Gets the referenced object number, when <see cref="Kind"/> is <see cref="PdfKind.Reference"/>.</summary>
        internal int RefNumber { get; private init; }

        /// <summary>Gets the referenced generation number, when <see cref="Kind"/> is <see cref="PdfKind.Reference"/>.</summary>
        internal int RefGeneration { get; private init; }

        /// <summary>
        ///     Gets the buffer offset at which this stream's raw (still-encoded) data begins, when
        ///     <see cref="Kind"/> is <see cref="PdfKind.Stream"/>.
        /// </summary>
        internal int StreamDataStart { get; private init; }

        /// <summary>
        ///     Gets or sets the indirect object number this value was parsed as the top-level
        ///     value of, or <c>-1</c> when this value was never parsed as a top-level indirect
        ///     object (for example a nested dictionary entry, or the single shared
        ///     <see cref="NullValue"/> instance). Deliberately settable, not <c>private init</c>
        ///     (breaking this type's otherwise-uniform immutability idiom): it is stamped onto an
        ///     already-constructed value by <c>PdfDocument.Xref.cs</c>'s <c>ParseIndirectObjectAt</c>
        ///     after <c>ParseValue</c> returns, since the parser itself has no notion of which
        ///     indirect object it is being invoked for. Used, together with <see cref="Generation"/>,
        ///     as the per-object key input to ISO 32000-1 Algorithm 1 when decrypting an encrypted
        ///     document's streams.
        /// </summary>
        internal int ObjectNumber { get; set; } = -1;

        /// <summary>
        ///     Gets or sets the indirect object's generation number, alongside
        ///     <see cref="ObjectNumber"/> - see its own remarks for why this is settable rather
        ///     than <c>private init</c>.
        /// </summary>
        internal int Generation { get; set; }

        /// <summary>The single, shared <see cref="PdfKind.Null"/> instance.</summary>
        internal static readonly PdfObject NullValue = new() { Kind = PdfKind.Null };

        internal static PdfObject FromBoolean(bool value) => new() { Kind = PdfKind.Boolean, Boolean = value };

        internal static PdfObject FromNumber(double value) => new() { Kind = PdfKind.Number, Number = value };

        internal static PdfObject FromString(byte[] value) => new() { Kind = PdfKind.String, Bytes = value };

        internal static PdfObject FromName(string value) => new() { Kind = PdfKind.Name, Text = value };

        internal static PdfObject FromArray(List<PdfObject> items) => new() { Kind = PdfKind.Array, Items = items };

        internal static PdfObject FromDictionary(Dictionary<string, PdfObject> entries) =>
            new() { Kind = PdfKind.Dictionary, Entries = entries };

        internal static PdfObject FromStream(Dictionary<string, PdfObject> entries, int dataStart) =>
            new() { Kind = PdfKind.Stream, Entries = entries, StreamDataStart = dataStart };

        internal static PdfObject FromReference(int number, int generation) =>
            new() { Kind = PdfKind.Reference, RefNumber = number, RefGeneration = generation };

        /// <summary>
        ///     Gets the dictionary entry with the given key (without the leading slash), or
        ///     <see langword="null"/> if this is not a dictionary/stream or the key is absent.
        /// </summary>
        /// <param name="key">The entry key, without a leading slash.</param>
        /// <returns>The entry's value, or <see langword="null"/>.</returns>
        internal PdfObject? Get(string key) => Entries.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    ///     Parses a single PDF value (of any kind) starting at the tokenizer's current position.
    /// </summary>
    /// <param name="tokenizer">The tokenizer to read from.</param>
    /// <returns>The parsed value.</returns>
    /// <exception cref="InvalidDataException">Thrown when the input is not a well-formed value.</exception>
    internal static PdfObject ParseValue(PdfTokenizer tokenizer) => ParseValue(tokenizer, tokenizer.NextToken());

    private static PdfObject ParseValue(PdfTokenizer tokenizer, PdfToken token) => token.Kind switch
    {
        PdfTokenKind.Number => ParseNumberOrReference(tokenizer, token),
        PdfTokenKind.LiteralString or PdfTokenKind.HexString => PdfObject.FromString(token.Bytes ?? []),
        PdfTokenKind.Name => PdfObject.FromName(token.Text ?? string.Empty),
        PdfTokenKind.ArrayStart => ParseArray(tokenizer),
        PdfTokenKind.DictStart => ParseDictionaryOrStream(tokenizer),
        PdfTokenKind.Keyword => ParseKeywordValue(token),
        _ => throw new InvalidDataException("Unexpected end of PDF content while parsing a value."),
    };

    private static PdfObject ParseKeywordValue(PdfToken token) => token.Text switch
    {
        "true" => PdfObject.FromBoolean(true),
        "false" => PdfObject.FromBoolean(false),
        "null" => PdfObject.NullValue,
        _ => throw new InvalidDataException($"Unexpected keyword '{token.Text}' in PDF content."),
    };

    /// <summary>
    ///     Disambiguates a bare number from the start of an indirect reference (<c>N G R</c>) by
    ///     looking ahead up to two further tokens, rewinding the tokenizer if the lookahead does
    ///     not confirm a reference.
    /// </summary>
    private static PdfObject ParseNumberOrReference(PdfTokenizer tokenizer, PdfToken first)
    {
        var savedPosition = tokenizer.Position;
        var second = tokenizer.NextToken();
        if (second.Kind == PdfTokenKind.Number)
        {
            var third = tokenizer.NextToken();
            if (third.Kind == PdfTokenKind.Keyword && third.Text == "R")
            {
                return PdfObject.FromReference((int)first.Number, (int)second.Number);
            }
        }

        tokenizer.Position = savedPosition;
        return PdfObject.FromNumber(first.Number);
    }

    private static PdfObject ParseArray(PdfTokenizer tokenizer)
    {
        var items = new List<PdfObject>();
        while (true)
        {
            var token = tokenizer.NextToken();
            if (token.Kind == PdfTokenKind.ArrayEnd)
            {
                break;
            }

            if (token.Kind == PdfTokenKind.EndOfFile)
            {
                throw new InvalidDataException("Unterminated array in PDF content.");
            }

            items.Add(ParseValue(tokenizer, token));
        }

        return PdfObject.FromArray(items);
    }

    private static Dictionary<string, PdfObject> ParseDictionaryEntries(PdfTokenizer tokenizer)
    {
        var entries = new Dictionary<string, PdfObject>();
        while (true)
        {
            var keyToken = tokenizer.NextToken();
            if (keyToken.Kind == PdfTokenKind.DictEnd)
            {
                break;
            }

            if (keyToken.Kind == PdfTokenKind.EndOfFile)
            {
                throw new InvalidDataException("Unterminated dictionary in PDF content.");
            }

            if (keyToken.Kind != PdfTokenKind.Name)
            {
                throw new InvalidDataException("Dictionary keys must be names.");
            }

            entries[keyToken.Text ?? string.Empty] = ParseValue(tokenizer);
        }

        return entries;
    }

    /// <summary>
    ///     Parses a dictionary, upgrading it to a <see cref="PdfKind.Stream"/> if immediately
    ///     followed by the <c>stream</c> keyword.
    /// </summary>
    /// <remarks>
    ///     Locating a stream's data <em>end</em> requires its <c>/Length</c> entry, which may
    ///     itself be an indirect reference not yet resolvable while the cross-reference table is
    ///     still being built (see <see cref="GetStreamRawLength(PdfObject)"/>'s remarks for the one
    ///     documented exception to this) - so only the data's <em>start</em> offset is recorded
    ///     here; the length is resolved lazily, on first access to the stream's bytes.
    /// </remarks>
    private static PdfObject ParseDictionaryOrStream(PdfTokenizer tokenizer)
    {
        var entries = ParseDictionaryEntries(tokenizer);

        var savedPosition = tokenizer.Position;
        var next = tokenizer.NextToken();
        if (next.Kind == PdfTokenKind.Keyword && next.Text == "stream")
        {
            SkipStreamLineEnding(tokenizer);
            return PdfObject.FromStream(entries, tokenizer.Position);
        }

        tokenizer.Position = savedPosition;
        return PdfObject.FromDictionary(entries);
    }

    /// <summary>
    ///     Skips the end-of-line sequence required immediately after the <c>stream</c> keyword
    ///     (<c>CRLF</c> or a bare <c>LF</c>; a bare <c>CR</c> is non-conformant but tolerated for
    ///     robustness), using raw byte access rather than token-level whitespace skipping, since
    ///     the following binary stream data must not be misinterpreted as further whitespace to
    ///     skip.
    /// </summary>
    private static void SkipStreamLineEnding(PdfTokenizer tokenizer)
    {
        if (tokenizer.PeekByte() == (byte)'\r')
        {
            tokenizer.AdvanceRaw();
        }

        if (tokenizer.PeekByte() == (byte)'\n')
        {
            tokenizer.AdvanceRaw();
        }
    }
}
