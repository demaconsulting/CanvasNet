using System.Text;
using System.Text.Json;

namespace DemaConsulting.CanvasNet.VegaLite;

/// <summary>
///     The metadata of a Vega-Lite specification that can be read from its text alone, produced by
///     <see cref="VegaLiteSpecScanner"/>.
/// </summary>
/// <remarks>
///     Foundation shell: the full specification model (<c>SpecModel</c> subsystem) replaces the scanner and this
///     record when the specification parser is delivered; the public properties of <see cref="VegaLiteDocument"/> that
///     are filled from it stay as they are.
/// </remarks>
/// <param name="SchemaUrl">The <c>$schema</c> value as written, or null.</param>
/// <param name="SchemaMajorVersion">The Vega-Lite major version named by the schema URL, or null.</param>
/// <param name="SchemaLocation">The location of the <c>$schema</c> value (default when absent).</param>
/// <param name="Title">The plain text of the title, or null.</param>
/// <param name="Marks">The mark types in document order with their locations.</param>
/// <param name="DataReferences">The data sources in document order.</param>
/// <param name="ParameterNames">The distinct parameter names in document order.</param>
internal sealed record VegaLiteSpecInfo(
    string? SchemaUrl,
    int? SchemaMajorVersion,
    VegaLiteLocation SchemaLocation,
    string? Title,
    IReadOnlyList<VegaLiteMarkEntry> Marks,
    IReadOnlyList<VegaLiteDataReference> DataReferences,
    IReadOnlyList<string> ParameterNames);

/// <summary>
///     A mark type found in a specification together with where it was written.
/// </summary>
/// <param name="Type">The mark type as written (for example <c>bar</c>).</param>
/// <param name="Location">The location of the <c>mark</c> value.</param>
internal sealed record VegaLiteMarkEntry(string Type, VegaLiteLocation Location);

/// <summary>
///     Reads the descriptive metadata of a Vega-Lite specification (schema, title, marks, data sources, parameter
///     names) in one forward pass over its UTF-8 text, with precise locations and a bounded nesting depth.
/// </summary>
/// <remarks>
///     <para>
///         The scanner exists so that a document can be opened, described and rendered as an empty view before the
///         full specification model exists. It validates the whole text as JSON (an unread value is skipped with
///         <see cref="Utf8JsonReader.Skip"/>, which still checks it) but only materializes the few properties that
///         describe a view, which keeps memory proportional to the number of views rather than to the size of
///         inline data.
///     </para>
///     <para>
///         The recursion over nested views (<c>layer</c>, <c>concat</c>, <c>hconcat</c>, <c>vconcat</c>, <c>spec</c>)
///         is bounded by the reader's maximum depth, which <see cref="VegaLiteLimits.MaxJsonDepth"/> caps at 256, so
///         hostile input cannot overflow the stack. Locations are computed incrementally while reading, so the cost is
///         linear in the size of the text. An instance is single-use and not thread-safe; the resulting
///         <see cref="VegaLiteSpecInfo"/> is immutable.
///     </para>
/// </remarks>
internal sealed class VegaLiteSpecScanner
{
    /// <summary>The marker that precedes the major version in a Vega-Lite schema URL.</summary>
    private const string SchemaMarker = "/vega-lite/v";

    /// <summary>The URL fragment that identifies the schema of plain Vega, which is not supported.</summary>
    private const string VegaSchemaFragment = "vega.github.io/schema/vega/";

    /// <summary>The longest run of digits accepted as a major version, which avoids overflow.</summary>
    private const int MaxMajorVersionDigits = 4;

    /// <summary>The UTF-8 text.</summary>
    private readonly byte[] _bytes;

    /// <summary>The source name for locations, or null.</summary>
    private readonly string? _file;

    /// <summary>The maximum JSON nesting depth.</summary>
    private readonly int _maxDepth;

    /// <summary>The marks found so far.</summary>
    private readonly List<VegaLiteMarkEntry> _marks = [];

    /// <summary>The data references found so far.</summary>
    private readonly List<VegaLiteDataReference> _dataReferences = [];

    /// <summary>The parameter names found so far, in order.</summary>
    private readonly List<string> _parameterNames = [];

    /// <summary>The parameter names found so far, for duplicate detection.</summary>
    private readonly HashSet<string> _parameterNameSet = new(StringComparer.Ordinal);

    /// <summary>The byte offset up to which the incremental locator has scanned.</summary>
    private int _scanOffset;

    /// <summary>The one-based line at <see cref="_scanOffset"/>.</summary>
    private int _line = 1;

    /// <summary>The one-based UTF-16 column at <see cref="_scanOffset"/>.</summary>
    private int _column = 1;

    /// <summary>The <c>$schema</c> value, or null.</summary>
    private string? _schemaUrl;

    /// <summary>The major version, or null.</summary>
    private int? _schemaMajorVersion;

    /// <summary>The location of the <c>$schema</c> value.</summary>
    private VegaLiteLocation _schemaLocation;

    /// <summary>The title text, or null.</summary>
    private string? _title;

    /// <summary>
    ///     Initializes a scanner over UTF-8 text.
    /// </summary>
    /// <param name="utf8">The UTF-8 text of the specification (without a byte-order mark).</param>
    /// <param name="sourceName">The source name for locations, or null.</param>
    /// <param name="maxDepth">The maximum JSON nesting depth.</param>
    public VegaLiteSpecScanner(byte[] utf8, string? sourceName, int maxDepth)
    {
        _bytes = utf8;
        _file = sourceName;
        _maxDepth = maxDepth;
    }

    /// <summary>
    ///     Scans the text.
    /// </summary>
    /// <returns>The metadata of the specification.</returns>
    /// <exception cref="VegaLiteSpecException">Thrown when the text is not valid JSON, is nested too deeply, or is not a specification object.</exception>
    public VegaLiteSpecInfo Scan()
    {
        try
        {
            var reader = new Utf8JsonReader(_bytes, new JsonReaderOptions { MaxDepth = _maxDepth });

            // The first token decides whether this is a specification at all: it must be an object
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                throw Fail("A Vega-Lite specification must be a JSON object.", string.Empty, reader.TokenStartIndex);
            }

            ReadSpec(ref reader, string.Empty, isRoot: true);

            // Reading past the closing brace makes the reader reject any trailing content
            reader.Read();
        }
        catch (JsonException ex)
        {
            throw FromJsonException(ex);
        }

        return new VegaLiteSpecInfo(
            _schemaUrl,
            _schemaMajorVersion,
            _schemaLocation,
            _title,
            _marks.AsReadOnly(),
            _dataReferences.AsReadOnly(),
            _parameterNames.AsReadOnly());
    }

    /// <summary>
    ///     Escapes a property name or array index as one RFC 6901 pointer segment.
    /// </summary>
    /// <param name="segment">The raw segment.</param>
    /// <returns>The escaped segment (<c>~</c> becomes <c>~0</c> and <c>/</c> becomes <c>~1</c>).</returns>
    internal static string EscapePointerSegment(string segment) =>
        segment.Contains('~') || segment.Contains('/')
            ? segment.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal)
            : segment;

    /// <summary>
    ///     Determines whether a data URL names something other than a relative local path: it has a scheme
    ///     (<c>http</c>, <c>file</c>, <c>data</c>, a drive letter, and so on) or is a network or absolute path.
    /// </summary>
    /// <remarks>
    ///     Such a URL is never handed to a file resolver: a chart renderer that fetches resources named by an
    ///     untrusted specification would be a server-side request forgery primitive, so remote data is reported and
    ///     the dataset is left empty instead.
    /// </remarks>
    /// <param name="url">The URL as written.</param>
    /// <returns><see langword="true"/> when the URL is not a plain relative path.</returns>
    internal static bool IsSchemeBearingUrl(string url)
    {
        var trimmed = url.AsSpan().TrimStart();
        if (trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith("\\", StringComparison.Ordinal))
        {
            return true;
        }

        // A scheme is a letter followed by letters, digits, '+', '-' or '.', then a colon
        if (trimmed.IsEmpty || !char.IsAsciiLetter(trimmed[0]))
        {
            return false;
        }

        for (var i = 1; i < trimmed.Length; i++)
        {
            var c = trimmed[i];
            if (c == ':')
            {
                return true;
            }

            if (!char.IsAsciiLetterOrDigit(c) && c != '+' && c != '-' && c != '.')
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    ///     Reads a view object; the reader is positioned on its opening brace and ends on its closing brace.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="pointer">The pointer of the view.</param>
    /// <param name="isRoot">Whether this is the top-level specification (which alone carries <c>$schema</c> and the title).</param>
    private void ReadSpec(ref Utf8JsonReader reader, string pointer, bool isRoot)
    {
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var name = reader.GetString()!;
            reader.Read();
            var child = pointer + "/" + EscapePointerSegment(name);
            switch (name)
            {
                case "$schema" when isRoot:
                    ReadSchema(ref reader, child);
                    break;
                case "title" when isRoot:
                    ReadTitle(ref reader, child);
                    break;
                case "mark":
                    ReadMark(ref reader, child);
                    break;
                case "data":
                    ReadData(ref reader, child);
                    break;
                case "params":
                    ReadParameters(ref reader);
                    break;
                case "layer" or "concat" or "hconcat" or "vconcat":
                    ReadViewArray(ref reader, child);
                    break;
                case "spec":
                    ReadViewObject(ref reader, child);
                    break;
                default:
                    // Everything else is validated as JSON and skipped: the view model arrives with later steps
                    reader.Skip();
                    break;
            }
        }
    }

    /// <summary>
    ///     Reads the <c>$schema</c> value.
    /// </summary>
    /// <param name="reader">The reader, positioned on the value.</param>
    /// <param name="pointer">The pointer of the value.</param>
    private void ReadSchema(ref Utf8JsonReader reader, string pointer)
    {
        var offset = reader.TokenStartIndex;
        if (reader.TokenType != JsonTokenType.String)
        {
            throw Fail("The $schema must be a string.", pointer, offset);
        }

        var url = reader.GetString()!;
        if (string.IsNullOrWhiteSpace(url))
        {
            throw Fail("The $schema must not be empty.", pointer, offset);
        }

        if (url.Contains(VegaSchemaFragment, StringComparison.OrdinalIgnoreCase))
        {
            throw Fail("The $schema names plain Vega; only Vega-Lite specifications are supported.", pointer, offset);
        }

        _schemaLocation = Locate(pointer, offset);
        _schemaUrl = url;
        _schemaMajorVersion = ParseMajorVersion(url);
    }

    /// <summary>
    ///     Extracts the Vega-Lite major version from a schema URL such as <c>https://vega.github.io/schema/vega-lite/v5.json</c>.
    /// </summary>
    /// <param name="url">The schema URL.</param>
    /// <returns>The major version, or null when the URL does not follow the Vega-Lite schema naming.</returns>
    internal static int? ParseMajorVersion(string url)
    {
        var index = url.IndexOf(SchemaMarker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        // Count the digits that follow "/vega-lite/v"; at least one and at most four are a version
        var start = index + SchemaMarker.Length;
        var end = start;
        while (end < url.Length && char.IsAsciiDigit(url[end]))
        {
            end++;
        }

        var digits = end - start;
        if (digits is 0 or > MaxMajorVersionDigits)
        {
            return null;
        }

        return int.Parse(url.AsSpan(start, digits), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     Reads the top-level <c>title</c>: a string, an array of strings, or an object with a <c>text</c>.
    /// </summary>
    /// <param name="reader">The reader, positioned on the value.</param>
    /// <param name="pointer">The pointer of the value.</param>
    private void ReadTitle(ref Utf8JsonReader reader, string pointer)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return;
            case JsonTokenType.StartObject:
                while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                {
                    var isText = reader.ValueTextEquals("text"u8);
                    reader.Read();
                    if (isText)
                    {
                        _title = ReadText(ref reader, pointer + "/text");
                    }
                    else
                    {
                        reader.Skip();
                    }
                }

                return;
            default:
                _title = ReadText(ref reader, pointer);
                return;
        }
    }

    /// <summary>
    ///     Reads a text value: a string, or an array of strings that is joined with line feeds.
    /// </summary>
    /// <param name="reader">The reader, positioned on the value.</param>
    /// <param name="pointer">The pointer of the value.</param>
    /// <returns>The text, or null for an expression reference object (which cannot be evaluated here).</returns>
    private string? ReadText(ref Utf8JsonReader reader, string pointer)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString();
            case JsonTokenType.StartObject:
                reader.Skip();
                return null;
            case JsonTokenType.StartArray:
                var lines = new List<string>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (reader.TokenType != JsonTokenType.String)
                    {
                        throw Fail("The lines of a text must be strings.", pointer + "/" + lines.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), reader.TokenStartIndex);
                    }

                    lines.Add(reader.GetString()!);
                }

                return string.Join('\n', lines);
            default:
                throw Fail("The text must be a string or an array of strings.", pointer, reader.TokenStartIndex);
        }
    }

    /// <summary>
    ///     Reads a <c>mark</c>: a type string or an object with a <c>type</c>.
    /// </summary>
    /// <param name="reader">The reader, positioned on the value.</param>
    /// <param name="pointer">The pointer of the value.</param>
    private void ReadMark(ref Utf8JsonReader reader, string pointer)
    {
        var location = Locate(pointer, reader.TokenStartIndex);
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                _marks.Add(new VegaLiteMarkEntry(reader.GetString()!, location));
                return;
            case JsonTokenType.StartObject:
                string? type = null;
                while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                {
                    var isType = reader.ValueTextEquals("type"u8);
                    reader.Read();
                    if (isType && reader.TokenType == JsonTokenType.String)
                    {
                        type = reader.GetString();
                    }
                    else if (isType)
                    {
                        throw Fail("The mark type must be a string.", pointer + "/type", reader.TokenStartIndex);
                    }
                    else
                    {
                        reader.Skip();
                    }
                }

                if (type is null)
                {
                    throw new VegaLiteSpecException("A mark definition needs a string 'type'.", location);
                }

                _marks.Add(new VegaLiteMarkEntry(type, location));
                return;
            default:
                throw Fail("The mark must be a string or an object.", pointer, reader.TokenStartIndex);
        }
    }

    /// <summary>
    ///     Reads a <c>data</c> object and records the source it names.
    /// </summary>
    /// <param name="reader">The reader, positioned on the value.</param>
    /// <param name="pointer">The pointer of the value.</param>
    private void ReadData(ref Utf8JsonReader reader, string pointer)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return;
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw Fail("The data must be an object.", pointer, reader.TokenStartIndex);
        }

        var location = Locate(pointer, reader.TokenStartIndex);
        VegaLiteDataReference? reference = null;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var name = reader.GetString()!;
            reader.Read();

            // The first source key in document order names the data; Vega-Lite allows only one per object
            switch (name)
            {
                case "values" when reference is null:
                    reference = new VegaLiteDataReference(VegaLiteDataKind.Inline, null, CountValues(ref reader), location);
                    break;
                case "url" when reference is null:
                    if (reader.TokenType != JsonTokenType.String)
                    {
                        throw Fail("The data url must be a string.", pointer + "/url", reader.TokenStartIndex);
                    }

                    reference = new VegaLiteDataReference(VegaLiteDataKind.Url, reader.GetString(), null, location);
                    break;
                case "name" when reference is null:
                    if (reader.TokenType != JsonTokenType.String)
                    {
                        throw Fail("The data name must be a string.", pointer + "/name", reader.TokenStartIndex);
                    }

                    reference = new VegaLiteDataReference(VegaLiteDataKind.Name, null, null, location);
                    break;
                case "sequence" when reference is null:
                    reference = new VegaLiteDataReference(VegaLiteDataKind.Sequence, null, ReadSequenceLength(ref reader), location);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        if (reference is not null)
        {
            _dataReferences.Add(reference);
        }
    }

    /// <summary>
    ///     Counts the rows of an inline <c>values</c> value without materializing them.
    /// </summary>
    /// <param name="reader">The reader, positioned on the value.</param>
    /// <returns>The number of array elements, or null when the value is not an array (text or an object).</returns>
    private static long? CountValues(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return null;
        }

        long count = 0;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            count++;
            reader.Skip();
        }

        return count;
    }

    /// <summary>
    ///     Reads a <c>sequence</c> object and computes how many values it generates.
    /// </summary>
    /// <param name="reader">The reader, positioned on the value.</param>
    /// <returns>The length, or null when it cannot be told from numeric <c>start</c>, <c>stop</c> and <c>step</c>.</returns>
    private static long? ReadSequenceLength(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            reader.Skip();
            return null;
        }

        double? start = null;
        double? stop = null;
        double step = 1;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var isStart = reader.ValueTextEquals("start"u8);
            var isStop = reader.ValueTextEquals("stop"u8);
            var isStep = reader.ValueTextEquals("step"u8);
            reader.Read();
            if ((isStart || isStop || isStep) && reader.TokenType == JsonTokenType.Number && reader.TryGetDouble(out var number))
            {
                if (isStart)
                {
                    start = number;
                }
                else if (isStop)
                {
                    stop = number;
                }
                else
                {
                    step = number;
                }
            }
            else
            {
                reader.Skip();
            }
        }

        if (start is null || stop is null || step == 0 || !double.IsFinite(step))
        {
            return null;
        }

        // Vega's sequence runs from start (inclusive) to stop (exclusive) in steps
        var length = Math.Ceiling((stop.Value - start.Value) / step);
        if (length < 0)
        {
            return 0;
        }

        return length < 9e18 ? (long)length : null;
    }

    /// <summary>
    ///     Reads a <c>params</c> array and records the parameter names.
    /// </summary>
    /// <param name="reader">The reader, positioned on the value.</param>
    private void ReadParameters(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                reader.Skip();
                continue;
            }

            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                var isName = reader.ValueTextEquals("name"u8);
                reader.Read();
                if (isName && reader.TokenType == JsonTokenType.String)
                {
                    var parameter = reader.GetString()!;
                    if (_parameterNameSet.Add(parameter))
                    {
                        _parameterNames.Add(parameter);
                    }
                }
                else
                {
                    reader.Skip();
                }
            }
        }
    }

    /// <summary>
    ///     Reads an array of nested views (<c>layer</c>, <c>concat</c>, <c>hconcat</c> or <c>vconcat</c>).
    /// </summary>
    /// <param name="reader">The reader, positioned on the value.</param>
    /// <param name="pointer">The pointer of the array.</param>
    private void ReadViewArray(ref Utf8JsonReader reader, string pointer)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw Fail("A view composition must be an array of views.", pointer, reader.TokenStartIndex);
        }

        var index = 0;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            var child = pointer + "/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw Fail("A view must be an object.", child, reader.TokenStartIndex);
            }

            ReadSpec(ref reader, child, isRoot: false);
            index++;
        }
    }

    /// <summary>
    ///     Reads a nested view object (the <c>spec</c> of a facet or repeat).
    /// </summary>
    /// <param name="reader">The reader, positioned on the value.</param>
    /// <param name="pointer">The pointer of the value.</param>
    private void ReadViewObject(ref Utf8JsonReader reader, string pointer)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw Fail("A view must be an object.", pointer, reader.TokenStartIndex);
        }

        ReadSpec(ref reader, pointer, isRoot: false);
    }

    /// <summary>
    ///     Creates the exception for a specification error at a byte offset.
    /// </summary>
    /// <param name="description">The description of the error.</param>
    /// <param name="pointer">The pointer of the construct.</param>
    /// <param name="offset">The byte offset of the construct's first token.</param>
    /// <returns>The exception, ready to throw.</returns>
    private VegaLiteSpecException Fail(string description, string pointer, long offset) =>
        new(description, Locate(pointer, offset));

    /// <summary>
    ///     Converts a JSON reader failure into a specification exception with a UTF-16 line and column.
    /// </summary>
    /// <param name="ex">The reader exception.</param>
    /// <returns>The specification exception.</returns>
    private VegaLiteSpecException FromJsonException(JsonException ex)
    {
        // The reader reports a zero-based line and byte position; unknown positions leave the location unknown
        VegaLiteLocation location;
        if (ex.LineNumber is { } line0 && ex.BytePositionInLine is { } bytePosition)
        {
            var offset = 0L;
            for (long l = 0; l < line0 && offset < _bytes.Length; offset++)
            {
                if (_bytes[offset] == (byte)'\n')
                {
                    l++;
                }
            }

            location = Locate(string.Empty, Math.Min(offset + bytePosition, _bytes.Length));
        }
        else
        {
            location = new VegaLiteLocation(_file, string.Empty, 1, 1);
        }

        // The reader's own message ends with its zero-based coordinates, which would contradict ours
        var message = ex.Message;
        var cut = message.IndexOf(" LineNumber:", StringComparison.Ordinal);
        if (cut >= 0)
        {
            message = message[..cut];
        }

        var description = message.Contains("depth", StringComparison.OrdinalIgnoreCase)
            ? $"The specification is nested deeper than the limit of {_maxDepth} levels."
            : "Malformed JSON: " + message;
        return new VegaLiteSpecException(description, location, ex);
    }

    /// <summary>
    ///     Converts a byte offset into a location with a one-based line and a UTF-16 column.
    /// </summary>
    /// <remarks>
    ///     Offsets are normally requested in increasing order, which keeps the total cost linear; a request that
    ///     goes backwards (only on the error path) restarts the scan from the beginning.
    /// </remarks>
    /// <param name="pointer">The pointer to put in the location.</param>
    /// <param name="offset">The byte offset.</param>
    /// <returns>The location.</returns>
    private VegaLiteLocation Locate(string pointer, long offset)
    {
        var target = (int)Math.Clamp(offset, 0, _bytes.Length);
        if (target < _scanOffset)
        {
            _scanOffset = 0;
            _line = 1;
            _column = 1;
        }

        while (_scanOffset < target)
        {
            var b = _bytes[_scanOffset++];
            if (b == (byte)'\n')
            {
                _line++;
                _column = 1;
            }
            else if ((b & 0xC0) != 0x80)
            {
                // A lead byte starts one UTF-16 unit, or two (a surrogate pair) for a four-byte sequence
                _column += b >= 0xF0 ? 2 : 1;
            }
        }

        return new VegaLiteLocation(_file, pointer, _line, _column);
    }

    /// <summary>
    ///     Encodes specification text as UTF-8 without a byte-order mark.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The bytes; unpaired surrogates become U+FFFD, deterministically.</returns>
    internal static byte[] Encode(string text) => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text);
}
