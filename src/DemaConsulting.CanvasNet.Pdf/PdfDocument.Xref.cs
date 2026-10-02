// cspell:ignore uncatchable
using System.IO.Compression;

namespace DemaConsulting.CanvasNet.Pdf;

public sealed partial class PdfDocument
{
    /// <summary>
    ///     The maximum total number of decoded output bytes <see cref="ZlibDecompress"/> allows
    ///     before failing closed, bounding a decompression-bomb-style crafted zlib stream (a tiny
    ///     compressed input that expands to an enormous output) from exhausting memory. 64 MiB is
    ///     generous for any legitimate PDF image/content stream, matching <c>LzwMaxOutputBytes</c>
    ///     in <c>PdfDocument.Filters.Lzw.cs</c>.
    /// </summary>
    private const int FlateMaxOutputBytes = 64 * 1024 * 1024;

    /// <summary>The kind of location a <see cref="XrefEntry"/> describes.</summary>
    private enum XrefEntryType
    {
        /// <summary>The object number is on the free list (not currently in use).</summary>
        Free,

        /// <summary>The object's value begins at a direct byte offset within <see cref="_buffer"/>.</summary>
        Direct,

        /// <summary>The object's value is entry number <see cref="XrefEntry.IndexInStream"/> within object-stream <see cref="XrefEntry.StreamNumber"/>.</summary>
        Compressed,
    }

    /// <summary>A single resolved cross-reference table entry.</summary>
    private readonly struct XrefEntry
    {
        private XrefEntry(XrefEntryType type, long offset, int streamNumber, int indexInStream)
        {
            Type = type;
            Offset = offset;
            StreamNumber = streamNumber;
            IndexInStream = indexInStream;
        }

        internal XrefEntryType Type { get; }

        internal long Offset { get; }

        internal int StreamNumber { get; }

        internal int IndexInStream { get; }

        internal static readonly XrefEntry Free = new(XrefEntryType.Free, 0, 0, 0);

        internal static XrefEntry CreateDirect(long offset) => new(XrefEntryType.Direct, offset, 0, 0);

        internal static XrefEntry CreateCompressed(int streamNumber, int indexInStream) =>
            new(XrefEntryType.Compressed, 0, streamNumber, indexInStream);
    }

    /// <summary>
    ///     Parses the full cross-reference chain starting from the trailing <c>startxref</c>
    ///     offset, following classic <c>/Prev</c> and hybrid <c>/XRefStm</c> links, and populates
    ///     <see cref="_xref"/> with the merged result (an entry from an earlier-processed, more
    ///     recent section always wins over one discovered later in the chain).
    /// </summary>
    /// <returns>The main (most recent) trailer dictionary.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when no <c>startxref</c> keyword, cross-reference section, or trailer can be
    ///     located, or when the chain contains a cycle.
    /// </exception>
    private PdfObject ParseCrossReferenceChain()
    {
        var xref = new Dictionary<int, XrefEntry>();
        PdfObject? trailer = null;
        var visited = new HashSet<int>();
        int? offset = FindStartXrefOffset();

        while (offset is not null)
        {
            if (!visited.Add(offset.Value))
            {
                throw new InvalidDataException("Cross-reference /Prev chain contains a cycle.");
            }

            var section = ParseCrossReferenceSection(offset.Value);

            // Merge this revision's own classic entries with its (optional) hybrid /XRefStm
            // entries first, within just this revision: a hybrid entry fills in an object that
            // this revision's classic table can only mark as free (old readers cannot see
            // compressed objects), but a classic entry for an object number the hybrid stream
            // does not describe is left untouched.
            var revision = new Dictionary<int, XrefEntry>(section.Xref);
            if (section.XRefStm is { } hybridOffset && visited.Add(hybridOffset))
            {
                var hybrid = ParseCrossReferenceSection(hybridOffset);
                foreach (var (number, entry) in hybrid.Xref)
                {
                    if (!revision.TryGetValue(number, out var existing) || existing.Type == XrefEntryType.Free)
                    {
                        revision[number] = entry;
                    }
                }
            }

            // An entry from an earlier-processed (more recent) revision always wins over one
            // discovered later via /Prev, so only fill in object numbers not already known.
            foreach (var (number, entry) in revision)
            {
                xref.TryAdd(number, entry);
            }

            trailer ??= section.Trailer;

            offset = section.Prev;
        }

        if (trailer is null)
        {
            throw new InvalidDataException("No trailer dictionary was found.");
        }

        _xref = xref;
        return trailer;
    }

    private readonly record struct XrefSection(Dictionary<int, XrefEntry> Xref, PdfObject Trailer, int? Prev, int? XRefStm);

    /// <summary>
    ///     Validates that a byte offset parsed from untrusted PDF input (a <c>startxref</c>
    ///     value, a trailer's <c>/Prev</c>/<c>/XRefStm</c> entry, a classic cross-reference
    ///     entry's direct offset, or an object stream's <c>/First</c> + per-entry relative
    ///     offset) is actually within <paramref name="bufferLength"/> before it is assigned to a
    ///     <see cref="PdfTokenizer"/>'s <see cref="PdfTokenizer.Position"/>. <c>Position</c> is a
    ///     plain, unchecked property, so a negative value would index the underlying buffer
    ///     negatively on the very next <see cref="PdfTokenizer.NextToken"/> call, leaking an
    ///     uncatchable-by-callers <see cref="IndexOutOfRangeException"/> instead of the
    ///     <see cref="InvalidDataException"/> this library otherwise consistently uses to signal
    ///     malformed input.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="offset"/> is negative or beyond the end of the buffer.
    /// </exception>
    private static void ValidateBufferOffset(long offset, int bufferLength, string description)
    {
        if (offset < 0 || offset > bufferLength)
        {
            throw new InvalidDataException($"{description} offset {offset} is outside the bounds of the document.");
        }
    }

    private XrefSection ParseCrossReferenceSection(int offset)
    {
        ValidateBufferOffset(offset, _buffer.Length, "Cross-reference section");
        var tokenizer = new PdfTokenizer(_buffer) { Position = offset };
        var savedPosition = tokenizer.Position;
        var first = tokenizer.NextToken();

        if (first.Kind == PdfTokenKind.Keyword && first.Text == "xref")
        {
            return ParseClassicXrefSection(tokenizer);
        }

        tokenizer.Position = savedPosition;
        return ParseXrefStreamSection(tokenizer);
    }

    /// <summary>Parses a classic <c>xref</c> table followed by a <c>trailer</c> dictionary.</summary>
    private static XrefSection ParseClassicXrefSection(PdfTokenizer tokenizer)
    {
        var xref = new Dictionary<int, XrefEntry>();

        while (true)
        {
            var token = tokenizer.NextToken();
            if (token.Kind == PdfTokenKind.Keyword && token.Text == "trailer")
            {
                break;
            }

            if (token.Kind != PdfTokenKind.Number)
            {
                throw new InvalidDataException("Malformed classic cross-reference table.");
            }

            var startNumber = (int)token.Number;
            var countToken = tokenizer.NextToken();
            if (countToken.Kind != PdfTokenKind.Number)
            {
                throw new InvalidDataException("Malformed classic cross-reference subsection header.");
            }

            var count = (int)countToken.Number;

            // Each classic cross-reference entry needs at least a few bytes (minimally
            // "0 0 n\n"), so a declared count that exceeds the number of bytes remaining in the
            // buffer can never be satisfied by real entries. Without this check, a crafted
            // subsection header such as "0 2147483647" would make the loop below call
            // NextToken() an unbounded number of times - bounded only by how much of the rest of
            // the file happens to parse as plausible "offset generation keyword" triplets -
            // before finally failing, a CPU-time denial of service proportional to the
            // attacker's chosen count rather than the file's real content.
            if (count < 0 || count > tokenizer.BufferLength - tokenizer.Position)
            {
                throw new InvalidDataException("Classic cross-reference subsection declares an invalid entry count.");
            }

            for (var i = 0; i < count; i++)
            {
                if (tokenizer.Position >= tokenizer.BufferLength)
                {
                    throw new InvalidDataException("Classic cross-reference table ended before all declared entries were read.");
                }

                var offsetToken = tokenizer.NextToken();
                var generationToken = tokenizer.NextToken();
                var flagToken = tokenizer.NextToken();
                if (offsetToken.Kind != PdfTokenKind.Number || generationToken.Kind != PdfTokenKind.Number ||
                    flagToken.Kind != PdfTokenKind.Keyword)
                {
                    throw new InvalidDataException("Malformed classic cross-reference entry.");
                }

                var objectNumber = startNumber + i;
                xref.TryAdd(
                    objectNumber,
                    flagToken.Text == "n" ? XrefEntry.CreateDirect((long)offsetToken.Number) : XrefEntry.Free);
            }
        }

        var trailer = ParseValue(tokenizer);
        if (trailer.Kind != PdfKind.Dictionary)
        {
            throw new InvalidDataException("Malformed trailer dictionary.");
        }

        return new XrefSection(xref, trailer, GetOptionalInt(trailer, "Prev"), GetOptionalInt(trailer, "XRefStm"));
    }

    /// <summary>
    ///     Parses a cross-reference stream (an indirect object of <c>/Type /XRef</c>) at the
    ///     tokenizer's current position, decoding its binary entry table per its <c>/W</c> field
    ///     widths and <c>/Index</c> subsection ranges.
    /// </summary>
    private XrefSection ParseXrefStreamSection(PdfTokenizer tokenizer)
    {
        var numberToken = tokenizer.NextToken();
        var generationToken = tokenizer.NextToken();
        var objToken = tokenizer.NextToken();
        if (numberToken.Kind != PdfTokenKind.Number || generationToken.Kind != PdfTokenKind.Number ||
            objToken.Kind != PdfTokenKind.Keyword || objToken.Text != "obj")
        {
            throw new InvalidDataException("Expected an indirect object header for a cross-reference stream.");
        }

        var streamObject = ParseValue(tokenizer);
        if (streamObject.Kind != PdfKind.Stream || GetNameValue(streamObject, "Type") != "XRef")
        {
            throw new InvalidDataException("Expected a /Type /XRef cross-reference stream.");
        }

        // The cross-reference stream's own /Length must be a direct integer (Phase 1
        // simplification): resolving an indirect /Length here would require the very
        // cross-reference table this stream exists to build.
        if (streamObject.Get("Length") is not { Kind: PdfKind.Number })
        {
            throw new InvalidDataException("Cross-reference stream /Length must be a direct integer.");
        }

        var decoded = GetStreamDecodedBytes(streamObject);

        var widthsObject = streamObject.Get("W") ?? throw new InvalidDataException("Cross-reference stream is missing /W.");
        if (widthsObject.Kind != PdfKind.Array || widthsObject.Items.Count != 3)
        {
            throw new InvalidDataException("Cross-reference stream /W must have exactly three entries.");
        }

        var widths = widthsObject.Items.Select(item => (int)item.Number).ToArray();
        if (widths.Any(w => w is < 0 or > 8))
        {
            throw new InvalidDataException("Cross-reference stream /W entries must be between 0 and 8.");
        }

        // A zero total entry width would never advance the read position, so a crafted large
        // /Size or /Index range could spin the decode loop below indefinitely without making
        // progress. Reject the table up front instead of looping.
        if (widths[0] + widths[1] + widths[2] == 0)
        {
            throw new InvalidDataException("Cross-reference stream /W entries must not all be zero.");
        }

        var size = streamObject.Get("Size") is { Kind: PdfKind.Number } sizeObject
            ? (int)sizeObject.Number
            : throw new InvalidDataException("Cross-reference stream is missing /Size.");

        var ranges = new List<(int Start, int Count)>();
        var indexObject = streamObject.Get("Index");
        if (indexObject is { Kind: PdfKind.Array })
        {
            for (var i = 0; i + 1 < indexObject.Items.Count; i += 2)
            {
                ranges.Add(((int)indexObject.Items[i].Number, (int)indexObject.Items[i + 1].Number));
            }
        }
        else
        {
            ranges.Add((0, size));
        }

        var xref = new Dictionary<int, XrefEntry>();
        var entryWidth = widths[0] + widths[1] + widths[2];
        var position = 0;
        foreach (var (start, count) in ranges)
        {
            for (var i = 0; i < count; i++)
            {
                if (position + entryWidth > decoded.Length)
                {
                    throw new InvalidDataException("Cross-reference stream data is truncated.");
                }

                var type = widths[0] == 0 ? 1 : (int)ReadBigEndian(decoded, position, widths[0]);
                var field2 = ReadBigEndian(decoded, position + widths[0], widths[1]);
                var field3 = widths[2] == 0 ? 0UL : ReadBigEndian(decoded, position + widths[0] + widths[1], widths[2]);
                position += entryWidth;

                var objectNumber = start + i;
                var entry = type switch
                {
                    0 => XrefEntry.Free,
                    1 => XrefEntry.CreateDirect((long)field2),
                    2 => XrefEntry.CreateCompressed((int)field2, (int)field3),
                    _ => throw new InvalidDataException($"Unsupported cross-reference stream entry type {type}."),
                };

                xref.TryAdd(objectNumber, entry);
            }
        }

        return new XrefSection(xref, streamObject, GetOptionalInt(streamObject, "Prev"), null);
    }

    private static int? GetOptionalInt(PdfObject dictionary, string key) =>
        dictionary.Get(key) is { Kind: PdfKind.Number } number ? (int)number.Number : null;

    private static ulong ReadBigEndian(byte[] buffer, int offset, int width)
    {
        ulong value = 0;
        for (var i = 0; i < width; i++)
        {
            value = (value << 8) | buffer[offset + i];
        }

        return value;
    }

    /// <summary>Finds the byte offset recorded after the last <c>startxref</c> keyword in the buffer.</summary>
    private int FindStartXrefOffset()
    {
        var marker = "startxref"u8.ToArray();
        var index = LastIndexOf(_buffer, marker);
        if (index < 0)
        {
            throw new InvalidDataException("No 'startxref' keyword found.");
        }

        var tokenizer = new PdfTokenizer(_buffer) { Position = index + marker.Length };
        var offsetToken = tokenizer.NextToken();
        if (offsetToken.Kind != PdfTokenKind.Number)
        {
            throw new InvalidDataException("Malformed 'startxref' offset.");
        }

        return (int)offsetToken.Number;
    }

    private static int LastIndexOf(byte[] haystack, byte[] needle)
    {
        for (var i = haystack.Length - needle.Length; i >= 0; i--)
        {
            var matched = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
            {
                return i;
            }
        }

        return -1;
    }

    private bool IsValidCatalogRoot(PdfObject trailer)
    {
        var rootReference = trailer.Get("Root");
        if (rootReference is null)
        {
            return false;
        }

        try
        {
            var root = Resolve(rootReference);
            return root.Kind == PdfKind.Dictionary && GetNameValue(root, "Type") == "Catalog";
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    /// <summary>
    ///     Recovers a usable trailer by scanning the whole buffer for <c>N G obj</c> markers when
    ///     normal cross-reference/trailer parsing fails or does not resolve to a valid catalog.
    /// </summary>
    /// <returns>A trailer dictionary with at least a working <c>/Root</c> entry.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when no document catalog can be located even via this fallback scan.
    /// </exception>
    private PdfObject BuildLinearScanFallback()
    {
        _xref = ScanObjectOffsets();

        var explicitTrailer = ScanForTrailerDictionary();
        if (explicitTrailer is not null && IsValidCatalogRoot(explicitTrailer))
        {
            return explicitTrailer;
        }

        foreach (var number in _xref.Keys.OrderBy(n => n))
        {
            PdfObject candidate;
            try
            {
                candidate = GetObject(number);
            }
            catch (InvalidDataException)
            {
                continue;
            }

            if (candidate.Kind == PdfKind.Dictionary && GetNameValue(candidate, "Type") == "Catalog")
            {
                return PdfObject.FromDictionary(new Dictionary<string, PdfObject>
                {
                    ["Root"] = PdfObject.FromReference(number, 0),
                });
            }
        }

        throw new InvalidDataException("Could not locate a document catalog, even via a linear scan.");
    }

    /// <summary>
    ///     Scans the entire buffer, token by token, for <c>N G obj</c> markers, recording each
    ///     object number's most recently seen offset (a later occurrence - for example from an
    ///     incremental update - overrides an earlier one, matching classic cross-reference table
    ///     override semantics).
    /// </summary>
    private Dictionary<int, XrefEntry> ScanObjectOffsets()
    {
        var xref = new Dictionary<int, XrefEntry>();
        var tokenizer = new PdfTokenizer(_buffer);

        while (true)
        {
            var startPosition = tokenizer.Position;
            var first = tokenizer.NextToken();
            if (first.Kind == PdfTokenKind.EndOfFile)
            {
                break;
            }

            if (first.Kind != PdfTokenKind.Number)
            {
                continue;
            }

            var second = tokenizer.NextToken();
            if (second.Kind != PdfTokenKind.Number)
            {
                continue;
            }

            var third = tokenizer.NextToken();
            if (third.Kind == PdfTokenKind.Keyword && third.Text == "obj")
            {
                xref[(int)first.Number] = XrefEntry.CreateDirect(startPosition);
            }
        }

        return xref;
    }

    /// <summary>Scans the buffer for the last <c>trailer</c> keyword and parses its dictionary.</summary>
    private PdfObject? ScanForTrailerDictionary()
    {
        var tokenizer = new PdfTokenizer(_buffer);
        PdfObject? last = null;

        while (true)
        {
            var token = tokenizer.NextToken();
            if (token.Kind == PdfTokenKind.EndOfFile)
            {
                break;
            }

            if (token.Kind != PdfTokenKind.Keyword || token.Text != "trailer")
            {
                continue;
            }

            try
            {
                var candidate = ParseValue(tokenizer);
                if (candidate.Kind == PdfKind.Dictionary)
                {
                    last = candidate;
                }
            }
            catch (InvalidDataException)
            {
                // Ignore a malformed trailer at this position and keep scanning.
            }
        }

        return last;
    }

    private static string? GetNameValue(PdfObject dictionary, string key) =>
        dictionary.Get(key) is { Kind: PdfKind.Name } name ? name.Text : null;

    /// <summary>Resolves an object, following one level of indirect reference if necessary.</summary>
    private PdfObject Resolve(PdfObject value) => value.Kind == PdfKind.Reference ? GetObject(value.RefNumber) : value;

    /// <summary>
    ///     Resolves an indirect object by number, using the cached value if this object number has
    ///     already been resolved.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the object number is not defined in the cross-reference table (or is on the
    ///     free list), or its value cannot be parsed.
    /// </exception>
    private PdfObject GetObject(int number)
    {
        if (_objectCache.TryGetValue(number, out var cached))
        {
            return cached;
        }

        if (!_xref.TryGetValue(number, out var entry) || entry.Type == XrefEntryType.Free)
        {
            throw new InvalidDataException($"Indirect object {number} is not defined.");
        }

        // Guard against a crafted cross-reference table/object graph that forms a resolution
        // cycle - whether via compressed-object containment or via two direct objects whose
        // values reference each other (for example a stream's indirect /Length referring back to
        // the stream itself) - before either object can be cached. Without this, GetObject would
        // recurse indefinitely and crash with an uncatchable StackOverflowException.
        if (!_objectResolutionStack.Add(number))
        {
            throw new InvalidDataException($"Indirect object {number} contains a resolution cycle.");
        }

        try
        {
            PdfObject result;
            if (entry.Type == XrefEntryType.Direct)
            {
                result = ParseIndirectObjectAt((int)entry.Offset, number);
            }
            else
            {
                result = LoadCompressedObject(entry.StreamNumber, entry.IndexInStream, number);
            }

            _objectCache[number] = result;
            return result;
        }
        finally
        {
            _objectResolutionStack.Remove(number);
        }
    }

    private PdfObject ParseIndirectObjectAt(int offset, int expectedNumber)
    {
        ValidateBufferOffset(offset, _buffer.Length, $"Indirect object {expectedNumber}");
        var tokenizer = new PdfTokenizer(_buffer) { Position = offset };
        var numberToken = tokenizer.NextToken();
        var generationToken = tokenizer.NextToken();
        var objToken = tokenizer.NextToken();

        if (numberToken.Kind != PdfTokenKind.Number || generationToken.Kind != PdfTokenKind.Number ||
            objToken.Kind != PdfTokenKind.Keyword || objToken.Text != "obj")
        {
            throw new InvalidDataException($"Malformed indirect object header for object {expectedNumber} at offset {offset}.");
        }

        var generation = (int)generationToken.Number;
        var result = ParseValue(tokenizer);
        result.ObjectNumber = expectedNumber;
        result.Generation = generation;

        // Decrypt every string found anywhere within this top-level indirect object's value now,
        // in place, before it is ever cached/returned - see DecryptStringsInPlace's own remarks
        // for why this never mistakenly decrypts the /Encrypt dictionary's own strings.
        if (_encryptionKey is not null)
        {
            DecryptStringsInPlace(result, expectedNumber, generation);
        }

        return result;
    }

    /// <summary>
    ///     Loads a single compressed object (entry type 2 in a cross-reference stream) out of its
    ///     containing object stream (<c>/Type /ObjStm</c>), decompressing the whole container the
    ///     first time any of its contained objects is requested (the decompressed result is not
    ///     itself cached across multiple compressed-object lookups within the same stream, since
    ///     each contained object's own parsed <see cref="PdfObject"/> is cached individually by
    ///     <see cref="GetObject(int)"/>).
    /// </summary>
    private PdfObject LoadCompressedObject(int streamNumber, int indexInStream, int expectedNumber)
    {
        var container = GetObject(streamNumber);
        if (container.Kind != PdfKind.Stream || GetNameValue(container, "Type") != "ObjStm")
        {
            throw new InvalidDataException($"Object {streamNumber} is not a valid /Type /ObjStm object stream.");
        }

        var decoded = GetStreamDecodedBytes(container);

        var count = container.Get("N") is { Kind: PdfKind.Number } countObject
            ? (int)countObject.Number
            : throw new InvalidDataException("Object stream is missing /N.");
        var first = container.Get("First") is { Kind: PdfKind.Number } firstObject
            ? (int)firstObject.Number
            : throw new InvalidDataException("Object stream is missing /First.");

        if (indexInStream < 0 || indexInStream >= count)
        {
            throw new InvalidDataException(
                $"Compressed object index {indexInStream} is out of range for object stream {streamNumber}.");
        }

        // Each header entry needs at least one byte (a single-digit object number, whitespace,
        // and a single-digit offset collapse to a minimum of 3 bytes, but 1 byte/entry is used
        // here as a conservative lower bound that never rejects a legitimate stream). Without
        // this check, a crafted /N far larger than the actual decoded stream would make the loop
        // below keep calling NextToken() long after the header's real content has been exhausted,
        // reading nothing but zero-cost EndOfFile tokens for up to int.MaxValue iterations.
        if (count > decoded.Length)
        {
            throw new InvalidDataException(
                $"Object stream {streamNumber} declares /N {count}, which exceeds its decoded byte length.");
        }

        var headerTokenizer = new PdfTokenizer(decoded);
        var relativeOffset = -1;
        for (var i = 0; i < count; i++)
        {
            // Stop as soon as the header tokenizer has no bytes left, rather than relying on the
            // token-kind check below to eventually notice an EndOfFile token: this fails fast on
            // a malformed header with a declared /N that overruns its own content.
            if (headerTokenizer.Position >= decoded.Length)
            {
                throw new InvalidDataException("Object stream header ended before all declared /N entries were read.");
            }

            var numberToken = headerTokenizer.NextToken();
            var offsetToken = headerTokenizer.NextToken();
            if (numberToken.Kind != PdfTokenKind.Number || offsetToken.Kind != PdfTokenKind.Number)
            {
                throw new InvalidDataException("Malformed object stream header.");
            }

            if (i == indexInStream)
            {
                relativeOffset = (int)offsetToken.Number;
                if ((int)numberToken.Number != expectedNumber)
                {
                    throw new InvalidDataException(
                        $"Object stream {streamNumber} entry {indexInStream} does not match expected object number {expectedNumber}.");
                }
            }
        }

        if (relativeOffset < 0)
        {
            throw new InvalidDataException("Malformed object stream header.");
        }

        var bodyTokenizer = new PdfTokenizer(decoded) { Position = ValidatedCompressedObjectOffset(first, relativeOffset, decoded.Length, streamNumber) };
        return ParseValue(bodyTokenizer);
    }

    /// <summary>
    ///     Combines an object stream's <c>/First</c> and a compressed object's relative offset
    ///     using <see langword="long"/> arithmetic (both are attacker-controlled, so their sum
    ///     could otherwise overflow <see langword="int"/> and wrap to a negative/incorrect value)
    ///     and validates the result against the decompressed container's actual length before it
    ///     is used as a <see cref="PdfTokenizer"/> position.
    /// </summary>
    private static int ValidatedCompressedObjectOffset(int first, int relativeOffset, int decodedLength, int streamNumber)
    {
        var combined = (long)first + relativeOffset;
        ValidateBufferOffset(combined, decodedLength, $"Object stream {streamNumber} compressed object");
        return (int)combined;
    }

    /// <summary>
    ///     Resolves a stream's declared <c>/Length</c>, which may be a direct integer or an
    ///     indirect reference.
    /// </summary>
    /// <remarks>
    ///     Resolving an indirect <c>/Length</c> requires the cross-reference table to already be
    ///     fully built (via <see cref="GetObject(int)"/>), so this only succeeds for regular
    ///     content/object streams accessed after cross-reference parsing has completed. The one
    ///     exception, documented at its own call site, is a cross-reference stream's own
    ///     <c>/Length</c>, which this Phase 1 implementation requires to be a direct integer.
    /// </remarks>
    private int GetStreamRawLength(PdfObject streamObject)
    {
        var lengthObject = streamObject.Get("Length") ?? throw new InvalidDataException("Stream object is missing required /Length entry.");
        var resolved = Resolve(lengthObject);
        if (resolved.Kind != PdfKind.Number)
        {
            throw new InvalidDataException("Stream /Length must be a number.");
        }

        return (int)resolved.Number;
    }

    private byte[] GetStreamRawBytes(PdfObject streamObject)
    {
        var length = GetStreamRawLength(streamObject);

        // Perform the bounds check in `long` arithmetic: with `int` arithmetic, a crafted huge
        // /Length value close to int.MaxValue could cause `StreamDataStart + length` to overflow
        // and silently wrap to a small (even negative) value, bypassing this check entirely and
        // letting an invalid range reach `AsSpan` below, which throws the undocumented
        // ArgumentOutOfRangeException instead of the documented InvalidDataException.
        var streamEnd = (long)streamObject.StreamDataStart + length;
        if (length < 0 || streamObject.StreamDataStart < 0 || streamEnd > _buffer.Length)
        {
            throw new InvalidDataException("Stream data range is out of bounds.");
        }

        var raw = _buffer.AsSpan(streamObject.StreamDataStart, length).ToArray();
        return _encryptionKey is null ? raw : DecryptStreamBytes(raw, streamObject.ObjectNumber, streamObject.Generation);
    }

    /// <summary>
    ///     Decompresses a complete zlib stream (2-byte header, DEFLATE-compressed data, 4-byte
    ///     big-endian Adler-32 trailer), mirroring <c>PngCodec</c>'s own established zlib-handling
    ///     pattern: <see cref="DeflateStream"/> decodes the raw DEFLATE payload once the 2-byte
    ///     header and 4-byte trailer have been stripped off.
    /// </summary>
    /// <remarks>
    ///     The trailing Adler-32 checksum is deliberately not validated against the decompressed
    ///     data: several real-world PDF producers emit a wrong (or placeholder) checksum while the
    ///     DEFLATE payload itself is perfectly well-formed, and other mainstream PDF readers
    ///     tolerate this and render the stream anyway. Rejecting such streams here would make this
    ///     library strictly less tolerant than the ecosystem it has to interoperate with, for a
    ///     mismatch that - unlike a truncated/corrupt DEFLATE payload, which <see cref="DeflateStream"/>
    ///     itself already fails on - does not affect the correctness of the bytes actually produced.
    /// </remarks>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the data is too short to be a valid zlib stream, the header's compression
    ///     method is not DEFLATE, the DEFLATE payload itself is truncated/corrupt (propagated
    ///     from <see cref="DeflateStream"/>), or the decoded output exceeds
    ///     <see cref="FlateMaxOutputBytes"/>.
    /// </exception>
    private static byte[] ZlibDecompress(byte[] zlibData)
    {
        if (zlibData.Length < 6)
        {
            throw new InvalidDataException("Zlib stream is too short to be valid.");
        }

        var compressionMethod = zlibData[0];
        if ((compressionMethod & 0x0F) != 8)
        {
            throw new InvalidDataException(
                $"Unsupported zlib compression method {compressionMethod & 0x0F}; only DEFLATE (8) is supported.");
        }

        var deflateData = zlibData.AsSpan(2, zlibData.Length - 2 - 4).ToArray();

        using var input = new MemoryStream(deflateData);
        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();

        // Copy in bounded chunks, checking the running total after every chunk, rather than
        // calling CopyTo(output) and checking output.Length afterward - a crafted tiny zlib
        // stream can expand to gigabytes, and checking only after an unbounded CopyTo would
        // already have allocated (and copied) all of that oversized output before the check
        // ever ran. Mirrors the DecodeLzw output cap in PdfDocument.Filters.Lzw.cs.
        var buffer = new byte[81920];
        long total = 0;
        int bytesRead;
        while ((bytesRead = deflate.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += bytesRead;
            if (total > FlateMaxOutputBytes)
            {
                throw new InvalidDataException(
                    $"FlateDecode output exceeds the maximum supported size of {FlateMaxOutputBytes} bytes.");
            }

            output.Write(buffer, 0, bytesRead);
        }

        return output.ToArray();
    }
}
