// cspell:ignore FEFF
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace DemaConsulting.CanvasNet.VegaLite.SpecModel;

/// <summary>
///     Reads the UTF-8 JSON text of a Vega-Lite specification into an immutable <see cref="SpecNode"/> tree with
///     precise locations, under the resource limits of <see cref="VegaLiteLimits"/>, and checks the shape of the
///     properties that the renderer depends on.
/// </summary>
/// <remarks>
///     <para>
///         The parser exists so that every later stage can read one trustworthy model of the specification and point
///         its diagnostics at the exact construct. It is fail-closed: text longer than
///         <see cref="VegaLiteLimits.MaxSourceLength"/> characters is an <see cref="InvalidDataException"/> (no
///         location is meaningful), and everything else that is wrong is a <see cref="VegaLiteSpecException"/> with
///         the file, the RFC 6901 pointer and the one-based line and column: malformed JSON (the first problem),
///         nesting deeper than <see cref="VegaLiteLimits.MaxJsonDepth"/>, a string longer than
///         <see cref="VegaLiteLimits.MaxStringLength"/>, a number outside the double range, a root that is not an
///         object, and a known Vega-Lite property whose value has the wrong type or value (see
///         <c>SpecParser.Validation</c>).
///     </para>
///     <para>
///         Reading is a single forward pass over the bytes with an explicit stack (frames are indexed by depth and
///         reused), so hostile nesting cannot overflow the call stack and the cost is linear in the size of the text.
///         Columns count UTF-16 code units and a tab is one column, matching <see cref="VegaLiteLocation"/>. A leading
///         byte-order mark is ignored. A key that is written twice keeps the last value and produces one
///         <c>vl-duplicate-key</c> warning at the later key; the parser opens no file and uses no network. An
///         instance is single-use and not thread-safe; the resulting <see cref="SpecTree"/> is immutable.
///     </para>
/// </remarks>
internal sealed partial class SpecParser
{
    /// <summary>The marker that precedes the major version in a Vega-Lite schema URL.</summary>
    private const string SchemaMarker = "/vega-lite/v";

    /// <summary>The URL fragment that identifies the schema of plain Vega, which is not supported.</summary>
    private const string VegaSchemaFragment = "vega.github.io/schema/vega/";

    /// <summary>The longest run of digits accepted as a major version, which avoids overflow.</summary>
    private const int MaxMajorVersionDigits = 4;

    /// <summary>The UTF-8 text without a byte-order mark.</summary>
    private readonly byte[] _bytes;

    /// <summary>The source name for locations, or null.</summary>
    private readonly string? _file;

    /// <summary>The limits that bound the parse.</summary>
    private readonly VegaLiteLimits _limits;

    /// <summary>The warnings found so far, in order.</summary>
    private readonly List<FeatureReport> _warnings = [];

    /// <summary>The frames of the open containers while reading, kept for the pointers of errors and warnings.</summary>
    private Frame?[] _frames = [];

    /// <summary>The byte offset up to which the incremental locator has scanned.</summary>
    private int _scanOffset;

    /// <summary>The one-based line at <see cref="_scanOffset"/>.</summary>
    private int _line = 1;

    /// <summary>The one-based UTF-16 column at <see cref="_scanOffset"/>.</summary>
    private int _column = 1;

    /// <summary>
    ///     Initializes a parser over UTF-8 text.
    /// </summary>
    /// <param name="utf8">The UTF-8 text of the specification (without a byte-order mark).</param>
    /// <param name="sourceName">The source name for locations, or null.</param>
    /// <param name="limits">The limits that bound the parse.</param>
    private SpecParser(byte[] utf8, string? sourceName, VegaLiteLimits limits)
    {
        _bytes = utf8;
        _file = sourceName;
        _limits = limits;
    }

    /// <summary>
    ///     Parses specification text.
    /// </summary>
    /// <remarks>
    ///     A leading U+FEFF is ignored. Unpaired surrogates in the text become U+FFFD, deterministically. The call is
    ///     deterministic and independent of the culture and the current directory.
    /// </remarks>
    /// <param name="text">The specification text. Must not be null.</param>
    /// <param name="sourceName">The source name used in locations, or null for an unnamed source.</param>
    /// <param name="limits">The limits; <see langword="null"/> uses <see cref="VegaLiteLimits.Default"/>.</param>
    /// <returns>The parsed specification.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    /// <exception cref="InvalidDataException">Thrown when <paramref name="text"/> is longer than <see cref="VegaLiteLimits.MaxSourceLength"/> characters.</exception>
    /// <exception cref="VegaLiteSpecException">Thrown when the text is not valid JSON, is nested too deeply, or is not a valid specification.</exception>
    public static SpecTree Parse(string text, string? sourceName = null, VegaLiteLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        var effective = limits ?? VegaLiteLimits.Default;

        // The size check comes first and needs no location: an oversized source is rejected before it is encoded
        var start = text.Length > 0 && text[0] == '\uFEFF' ? 1 : 0;
        if (text.Length - start > effective.MaxSourceLength)
        {
            throw CreateSourceTooLongException(effective.MaxSourceLength);
        }

        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(start == 0 ? text : text[start..]);
        return new SpecParser(bytes, sourceName, effective).Run();
    }

    /// <summary>
    ///     Parses specification text given as UTF-8 bytes.
    /// </summary>
    /// <remarks>
    ///     A leading UTF-8 byte-order mark is ignored. Invalid UTF-8 is a malformed-JSON error at its position. Apart
    ///     from the input type this is the same as <see cref="Parse(string, string?, VegaLiteLimits?)"/>.
    /// </remarks>
    /// <param name="utf8">The specification text as UTF-8. Must not be null.</param>
    /// <param name="sourceName">The source name used in locations, or null for an unnamed source.</param>
    /// <param name="limits">The limits; <see langword="null"/> uses <see cref="VegaLiteLimits.Default"/>.</param>
    /// <returns>The parsed specification.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="utf8"/> is null.</exception>
    /// <exception cref="InvalidDataException">Thrown when the text is longer than <see cref="VegaLiteLimits.MaxSourceLength"/> characters.</exception>
    /// <exception cref="VegaLiteSpecException">Thrown when the text is not valid JSON, is nested too deeply, or is not a valid specification.</exception>
    public static SpecTree Parse(byte[] utf8, string? sourceName = null, VegaLiteLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(utf8);
        var effective = limits ?? VegaLiteLimits.Default;
        var bytes = utf8.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()) ? utf8[3..] : utf8;

        // Every character takes at least one byte, so a byte count at or below the limit is always within it
        if (bytes.Length > effective.MaxSourceLength && Encoding.UTF8.GetCharCount(bytes) > effective.MaxSourceLength)
        {
            throw CreateSourceTooLongException(effective.MaxSourceLength);
        }

        return new SpecParser(bytes, sourceName, effective).Run();
    }

    /// <summary>
    ///     Escapes a property name or array index as one RFC 6901 pointer segment.
    /// </summary>
    /// <param name="segment">The raw segment.</param>
    /// <returns>The escaped segment (<c>~</c> becomes <c>~0</c> and <c>/</c> becomes <c>~1</c>).</returns>
    public static string EscapePointerSegment(string segment) => SpecNode.EscapePointerSegment(segment);

    /// <summary>
    ///     Extracts the Vega-Lite major version from a schema URL such as <c>https://vega.github.io/schema/vega-lite/v5.json</c>.
    /// </summary>
    /// <param name="url">The schema URL.</param>
    /// <returns>The major version, or null when the URL does not follow the Vega-Lite schema naming.</returns>
    public static int? ParseSchemaMajorVersion(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
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

        return int.Parse(url.AsSpan(start, digits), NumberStyles.None, CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     Creates the exception for source that exceeds the size limit.
    /// </summary>
    /// <param name="maxLength">The limit that was exceeded.</param>
    /// <returns>The exception, ready to throw.</returns>
    private static InvalidDataException CreateSourceTooLongException(int maxLength) =>
        new($"The Vega-Lite specification is longer than the limit of {maxLength} characters (VegaLiteLimits.MaxSourceLength).");

    /// <summary>
    ///     Reads and validates the text.
    /// </summary>
    /// <returns>The parsed specification.</returns>
    /// <exception cref="VegaLiteSpecException">Thrown when the text is not a valid specification.</exception>
    private SpecTree Run()
    {
        SpecNode root;
        try
        {
            root = Read();
        }
        catch (JsonException ex)
        {
            throw FromJsonException(ex);
        }

        // The shape checks run on the finished tree so that every error carries the pointer of the offending value
        Validate(root);
        return new SpecTree(root, _file, _warnings.AsReadOnly());
    }

    /// <summary>
    ///     Reads the JSON into a tree with one forward pass and an explicit stack.
    /// </summary>
    /// <returns>The root node.</returns>
    /// <exception cref="JsonException">Thrown by the reader for malformed JSON and excessive depth.</exception>
    /// <exception cref="VegaLiteSpecException">Thrown for a root that is not an object, an overlong string or an out-of-range number.</exception>
    private SpecNode Read()
    {
        var reader = new Utf8JsonReader(_bytes, new JsonReaderOptions { MaxDepth = _limits.MaxJsonDepth });

        // The first token decides whether this is a specification at all: it must be an object
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            throw Fail("A Vega-Lite specification must be a JSON object.", string.Empty, reader.TokenStartIndex);
        }

        // Frames are indexed by depth and reused, so the allocation per container is amortized away
        var frames = new Frame?[_limits.MaxJsonDepth + 1];
        _frames = frames;
        var depth = -1;
        SpecNode? root = null;
        do
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.PropertyName:
                    {
                        var frame = frames[depth]!;
                        frame.PendingName = ReadString(ref reader, depth, null, -1);
                        var key = Locate(reader.TokenStartIndex);
                        frame.PendingKeyLine = key.Line;
                        frame.PendingKeyColumn = key.Column;
                        break;
                    }

                case JsonTokenType.StartObject or JsonTokenType.StartArray:
                    {
                        var position = Locate(reader.TokenStartIndex);
                        var (name, index, keyLine, keyColumn) = NextChildIdentity(frames, depth);
                        depth++;
                        var frame = frames[depth] ??= new Frame();
                        frame.Reset(
                            reader.TokenType == JsonTokenType.StartObject ? SpecNodeKind.Object : SpecNodeKind.Array,
                            name,
                            index,
                            new NodePosition(position.Line, position.Column, keyLine, keyColumn));
                        break;
                    }

                case JsonTokenType.EndObject or JsonTokenType.EndArray:
                    {
                        var frame = frames[depth]!;
                        depth--;
                        var node = new SpecNode(frame.Kind, frame.Name, frame.Index, frame.Position, null, 0, false, frame.Children.ToArray());
                        frame.Children.Clear();
                        frame.Names?.Clear();
                        Attach(frames, depth, node, ref root);
                        break;
                    }

                default:
                    {
                        // A scalar value: string, number, true, false or null
                        var position = Locate(reader.TokenStartIndex);
                        var (name, index, keyLine, keyColumn) = NextChildIdentity(frames, depth);
                        var nodePosition = new NodePosition(position.Line, position.Column, keyLine, keyColumn);
                        var node = ReadScalar(ref reader, depth, name, index, nodePosition);
                        Attach(frames, depth, node, ref root);
                        break;
                    }
            }
        }
        while (root is null && reader.Read());

        // Reading past the closing brace makes the reader reject any trailing content
        reader.Read();
        return root!;
    }

    /// <summary>
    ///     Reads a scalar token into a node.
    /// </summary>
    /// <param name="reader">The reader, positioned on the token.</param>
    /// <param name="depth">The depth of the enclosing frame.</param>
    /// <param name="name">The property name, or null.</param>
    /// <param name="index">The array index, or -1.</param>
    /// <param name="position">The position of the node.</param>
    /// <returns>The node.</returns>
    /// <exception cref="VegaLiteSpecException">Thrown for an overlong string or an out-of-range number.</exception>
    private SpecNode ReadScalar(ref Utf8JsonReader reader, int depth, string? name, int index, NodePosition position)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return new SpecNode(SpecNodeKind.String, name, index, position, ReadString(ref reader, depth, name, index), 0, false, null);
            case JsonTokenType.Number:
                if (!reader.TryGetDouble(out var number) || !double.IsFinite(number))
                {
                    throw Fail("The number is outside the range that can be represented.", PointerOfChild(depth, name, index), reader.TokenStartIndex);
                }

                return new SpecNode(SpecNodeKind.Number, name, index, position, null, number, false, null);
            case JsonTokenType.True:
                return new SpecNode(SpecNodeKind.Boolean, name, index, position, null, 0, true, null);
            case JsonTokenType.False:
                return new SpecNode(SpecNodeKind.Boolean, name, index, position, null, 0, false, null);
            default:
                return new SpecNode(SpecNodeKind.Null, name, index, position, null, 0, false, null);
        }
    }

    /// <summary>
    ///     Reads the current string or property-name token and enforces the string length limit.
    /// </summary>
    /// <param name="reader">The reader, positioned on the token.</param>
    /// <param name="depth">The depth of the enclosing frame, for the pointer of an error.</param>
    /// <param name="name">The property name of the value for the pointer of an error, or null.</param>
    /// <param name="index">The array index of the value for the pointer of an error, or -1.</param>
    /// <returns>The unescaped text.</returns>
    /// <exception cref="VegaLiteSpecException">Thrown when the text is longer than <see cref="VegaLiteLimits.MaxStringLength"/>, or is not valid UTF-8.</exception>
    private string ReadString(ref Utf8JsonReader reader, int depth, string? name, int index)
    {
        string text;
        try
        {
            text = reader.GetString()!;
        }
        catch (InvalidOperationException ex)
        {
            // Only text that was supplied as bytes can contain invalid UTF-8 (strings are encoded deterministically)
            var position = Locate(reader.TokenStartIndex);
            throw new VegaLiteSpecException(
                "Malformed JSON: the text is not valid UTF-8.",
                new VegaLiteLocation(_file, string.Empty, position.Line, position.Column),
                ex);
        }

        if (text.Length > _limits.MaxStringLength)
        {
            throw Fail(
                $"The string is longer than the limit of {_limits.MaxStringLength} characters (VegaLiteLimits.MaxStringLength).",
                reader.TokenType == JsonTokenType.PropertyName ? PointerOfChild(depth, text, -1) : PointerOfChild(depth, name, index),
                reader.TokenStartIndex);
        }

        return text;
    }

    /// <summary>
    ///     Determines how the next value is identified in its parent: by property name or by array index.
    /// </summary>
    /// <param name="frames">The frames.</param>
    /// <param name="depth">The depth of the parent frame, or -1 at the root.</param>
    /// <returns>The property name (or null), the array index (or -1), and the position of the property key (or zero).</returns>
    private static (string? Name, int Index, int KeyLine, int KeyColumn) NextChildIdentity(Frame?[] frames, int depth)
    {
        if (depth < 0)
        {
            return (null, -1, 0, 0);
        }

        var parent = frames[depth]!;
        return parent.Kind == SpecNodeKind.Object
            ? (parent.PendingName, -1, parent.PendingKeyLine, parent.PendingKeyColumn)
            : (null, parent.Children.Count, 0, 0);
    }

    /// <summary>
    ///     Adds a finished node to its parent frame (applying the duplicate-key policy) or makes it the root.
    /// </summary>
    /// <param name="frames">The frames.</param>
    /// <param name="depth">The depth of the parent frame, or -1 for the root.</param>
    /// <param name="node">The finished node.</param>
    /// <param name="root">Receives the root when the node is the root.</param>
    private void Attach(Frame?[] frames, int depth, SpecNode node, ref SpecNode? root)
    {
        if (depth < 0)
        {
            root = node;
            return;
        }

        var parent = frames[depth]!;
        if (parent.Kind == SpecNodeKind.Array)
        {
            parent.Children.Add(node);
            return;
        }

        // Last wins: the later value replaces the earlier one in place, so the property keeps its first position
        var existing = parent.IndexOfProperty(node.Name!);
        if (existing < 0)
        {
            parent.AddProperty(node);
            return;
        }

        parent.Children[existing] = node;
        _warnings.Add(new FeatureReport(
            "vl-duplicate-key",
            $"The key '{node.Name}' is written more than once; the last value is used.",
            new VegaLiteLocation(_file, PointerOfChild(depth, node.Name, -1), node.KeyLine, node.KeyColumn),
            FeatureReportKind.Warning));
    }

    /// <summary>
    ///     Computes the pointer of a child of the frame at a depth while the tree is still being built.
    /// </summary>
    /// <param name="depth">The depth of the parent frame, or -1 for the root.</param>
    /// <param name="name">The property name of the child, or null.</param>
    /// <param name="index">The array index of the child, or -1.</param>
    /// <returns>The pointer.</returns>
    private string PointerOfChild(int depth, string? name, int index)
    {
        // This is only needed on error and warning paths, where the frames hold the identity of every ancestor
        var segments = new List<string>();
        for (var d = 1; d <= depth; d++)
        {
            segments.Add(Identify(_frames[d]?.Name, _frames[d]?.Index ?? -1));
        }

        if (name is not null)
        {
            segments.Add(SpecNode.EscapePointerSegment(name));
        }
        else if (index >= 0)
        {
            segments.Add(index.ToString(CultureInfo.InvariantCulture));
        }

        return segments.Count == 0 ? string.Empty : "/" + string.Join('/', segments);
    }

    /// <summary>
    ///     Formats one pointer segment.
    /// </summary>
    /// <param name="frameName">The property name, or null.</param>
    /// <param name="frameIndex">The array index, or -1.</param>
    /// <returns>The escaped segment.</returns>
    private static string Identify(string? frameName, int frameIndex) =>
        frameName is not null ? SpecNode.EscapePointerSegment(frameName) : frameIndex.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    ///     Creates the exception for a specification error at a byte offset.
    /// </summary>
    /// <param name="description">The description of the error.</param>
    /// <param name="pointer">The pointer of the construct.</param>
    /// <param name="offset">The byte offset of the construct's first token.</param>
    /// <returns>The exception, ready to throw.</returns>
    private VegaLiteSpecException Fail(string description, string pointer, long offset)
    {
        var position = Locate(offset);
        return new VegaLiteSpecException(description, new VegaLiteLocation(_file, pointer, position.Line, position.Column));
    }

    /// <summary>
    ///     Converts a JSON reader failure into a specification exception with a UTF-16 line and column.
    /// </summary>
    /// <param name="ex">The reader exception.</param>
    /// <returns>The specification exception.</returns>
    private VegaLiteSpecException FromJsonException(JsonException ex)
    {
        // The reader reports a zero-based line and byte position; unknown positions leave the location at the start
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

            var position = Locate(Math.Min(offset + bytePosition, _bytes.Length));
            location = new VegaLiteLocation(_file, string.Empty, position.Line, position.Column);
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
            ? $"The specification is nested deeper than the limit of {_limits.MaxJsonDepth} levels."
            : "Malformed JSON: " + message;
        return new VegaLiteSpecException(description, location, ex);
    }

    /// <summary>
    ///     Converts a byte offset into a one-based line and a UTF-16 column.
    /// </summary>
    /// <remarks>
    ///     Offsets are normally requested in increasing order, which keeps the total cost linear; a request that
    ///     goes backwards (only on the error path) restarts the scan from the beginning.
    /// </remarks>
    /// <param name="offset">The byte offset.</param>
    /// <returns>The line and the column.</returns>
    private (int Line, int Column) Locate(long offset)
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

        return (_line, _column);
    }

    /// <summary>
    ///     The state of one open JSON container while the tree is being built.
    /// </summary>
    private sealed class Frame
    {
        /// <summary>The number of properties above which an object tracks its names in a dictionary.</summary>
        private const int NameIndexThreshold = 8;

        /// <summary>Gets the kind of container.</summary>
        public SpecNodeKind Kind { get; private set; }

        /// <summary>Gets the property name that introduced the container, or null.</summary>
        public string? Name { get; private set; }

        /// <summary>Gets the array index of the container, or -1.</summary>
        public int Index { get; private set; }

        /// <summary>Gets the position of the container and of its key.</summary>
        public NodePosition Position { get; private set; }

        /// <summary>Gets the children collected so far.</summary>
        public List<SpecNode> Children { get; } = [];

        /// <summary>Gets the name-to-position index of a large object, or null.</summary>
        public Dictionary<string, int>? Names { get; private set; }

        /// <summary>Gets or sets the name of the property whose value is being read.</summary>
        public string? PendingName { get; set; }

        /// <summary>Gets or sets the line of the pending property key.</summary>
        public int PendingKeyLine { get; set; }

        /// <summary>Gets or sets the column of the pending property key.</summary>
        public int PendingKeyColumn { get; set; }

        /// <summary>
        ///     Prepares the frame for a new container.
        /// </summary>
        /// <param name="kind">The kind of container.</param>
        /// <param name="name">The property name that introduced the container, or null.</param>
        /// <param name="index">The array index of the container, or -1.</param>
        /// <param name="position">The position of the container.</param>
        public void Reset(SpecNodeKind kind, string? name, int index, NodePosition position)
        {
            Kind = kind;
            Name = name;
            Index = index;
            Position = position;
            PendingName = null;
            Children.Clear();
            Names?.Clear();
        }

        /// <summary>
        ///     Finds a property by name among the children collected so far.
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <returns>The position in <see cref="Children"/>, or -1.</returns>
        public int IndexOfProperty(string name)
        {
            if (Names is not null)
            {
                return Names.GetValueOrDefault(name, -1);
            }

            for (var i = 0; i < Children.Count; i++)
            {
                if (string.Equals(Children[i].Name, name, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        ///     Appends a property that is not yet present, building the name index when the object grows large.
        /// </summary>
        /// <param name="node">The property value, which carries its name.</param>
        public void AddProperty(SpecNode node)
        {
            Children.Add(node);
            if (Names is not null)
            {
                Names[node.Name!] = Children.Count - 1;
            }
            else if (Children.Count > NameIndexThreshold)
            {
                Names = new Dictionary<string, int>(StringComparer.Ordinal);
                for (var i = 0; i < Children.Count; i++)
                {
                    Names[Children[i].Name!] = i;
                }
            }
        }
    }
}
