// cspell:ignore uncatchable
using System.IO.Compression;
using DemaConsulting.CanvasNet.Codecs;

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
    /// <param name="password">
    ///     The password to authenticate the document's <c>/Encrypt</c> dictionary with, if the
    ///     recovered trailer declares one - forwarded to <see cref="InitializeEncryption"/>. See
    ///     <see cref="InitializeEncryption"/>'s own remarks for the full authentication semantics.
    /// </param>
    /// <returns>A trailer dictionary with at least a working <c>/Root</c> entry.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when no document catalog can be located even via this fallback scan.
    /// </exception>
    private PdfObject BuildLinearScanFallback(string? password)
    {
        _xref = ScanObjectOffsets(out var streamPayloadRanges);

        var explicitTrailer = ScanForTrailerDictionary(streamPayloadRanges);

        // Establish the file decryption key (if any) now, before decoding any compressed object
        // streams below: a compressed object stream in an encrypted document is itself encrypted
        // ciphertext, and attempting to inflate that ciphertext as if it were plain FlateDecode
        // data before the correct key is known would fail - silently skipping every object nested
        // in that stream as "unrecoverable" even though the key was available all along. A
        // malformed /Encrypt dictionary is tolerated here (ignored) exactly as elsewhere in this
        // fallback, since it must not prevent recovering whatever objects still can be recovered;
        // an incorrect/missing password is not tolerated - it is allowed to propagate, matching
        // the primary (non-fallback) parsing path's own fail-closed behavior.
        if (explicitTrailer is not null)
        {
            try
            {
                InitializeEncryption(explicitTrailer, password);
            }
            catch (InvalidDataException)
            {
                // Ignore a malformed /Encrypt dictionary at this stage; keep attempting recovery.
            }
        }

        RegisterCompressedObjectsFromObjectStreams();

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
    ///     Scans the entire buffer for literal <c>N G obj</c> byte markers, recording each object
    ///     number's most recently seen offset (a later occurrence - for example from an
    ///     incremental update - overrides an earlier one, matching classic cross-reference table
    ///     override semantics). This operates directly on raw bytes rather than through
    ///     <see cref="PdfTokenizer"/>: tokenizing the entire buffer would also tokenize
    ///     compressed/binary stream payloads that were never meant to be parsed as PDF syntax, and
    ///     real-world binary noise routinely derails a general-purpose tokenizer (for example by
    ///     being misread as an implausibly long number or string token that swallows a genuine
    ///     "obj" marker a few bytes later). Matching the exact literal pattern
    ///     "&lt;digits&gt; &lt;digits&gt; obj" byte-by-byte is far more resilient to such noise.
    ///     For the same reason, each matched object's <c>stream</c>/<c>endstream</c> payload (if
    ///     any) is skipped over rather than scanned byte-by-byte: binary/compressed payload bytes
    ///     can coincidentally spell out a byte-perfect "N G obj" marker of their own, which would
    ///     otherwise register a bogus offset - possibly even overwriting a legitimate object
    ///     number's real offset, since a later match wins.
    ///     <para>
    ///         Before checking each position for an "obj" match, <see cref="AdvancePastNonSyntax"/>
    ///         is consulted to skip over any PDF comment or literal/hex string starting there.
    ///         This is a soundness property, not a formatting-convention heuristic: a decoy byte
    ///         sequence that is only reachable by first entering a comment or string can never be
    ///         matched, regardless of how it is laid out (for example across several lines, or not
    ///         at the start of any of them). This replaces an earlier line-start-based heuristic
    ///         that was both unsound (a decoy could still be crafted to start at the beginning of
    ///         a line while remaining lexically inside a multi-line string) and overly strict (a
    ///         genuine header is not actually required by the PDF grammar to begin its own line,
    ///         so that heuristic could also reject legitimate documents).
    ///     </para>
    /// </summary>
    /// <param name="streamPayloadRanges">
    ///     Populated with the <c>[Start, End)</c> byte range of every stream payload skipped over
    ///     while scanning, so that <see cref="ScanForTrailerDictionary"/> can likewise exclude
    ///     that same payload content from its own independent raw-byte search for the
    ///     <c>trailer</c> keyword.
    /// </param>
    private Dictionary<int, XrefEntry> ScanObjectOffsets(out List<(int Start, int End)> streamPayloadRanges)
    {
        var xref = new Dictionary<int, XrefEntry>();
        var ranges = new List<(int Start, int End)>();
        var buffer = _buffer;
        var i = 0;

        while (i < buffer.Length)
        {
            var afterNonSyntax = AdvancePastNonSyntax(buffer, i);
            if (afterNonSyntax != i)
            {
                i = afterNonSyntax;
                continue;
            }

            if (i + 3 > buffer.Length ||
                buffer[i] != (byte)'o' || buffer[i + 1] != (byte)'b' || buffer[i + 2] != (byte)'j')
            {
                i++;
                continue;
            }

            // Reject matches inside a larger token (for example the tail of "endobj", or a
            // binary-noise byte run that merely contains "obj" as a substring): a genuine keyword
            // is not immediately followed by another regular (non-whitespace, non-delimiter) byte
            // - the same rule PdfTokenizer.ReadKeyword uses to decide where a keyword token ends.
            if (i + 3 < buffer.Length && IsMarkerRegularByte(buffer[i + 3]))
            {
                i++;
                continue;
            }

            if (TryParseObjectHeaderBackward(buffer, i, out var objectNumber, out var headerStart))
            {
                xref[objectNumber] = XrefEntry.CreateDirect(headerStart);
                var searchStart = i + 3;
                var next = SkipPastStreamPayload(buffer, searchStart);
                if (next != searchStart)
                {
                    ranges.Add((searchStart, next));
                }

                i = next;
            }
            else
            {
                // Not a genuine "N G obj" header - for example the tail of the "endobj" keyword
                // itself. There is no associated stream payload to skip, so only advance past this
                // "obj" occurrence and keep scanning normally.
                i += 3;
            }
        }

        streamPayloadRanges = ranges;
        return xref;
    }

    /// <summary>
    ///     If the object just matched by <see cref="ScanObjectOffsets"/> is a dictionary
    ///     immediately followed by a literal <c>stream</c> keyword (per the PDF specification's
    ///     own stream-object grammar), returns the index just past the matching <c>endstream</c>
    ///     keyword so the caller's scan resumes after the payload instead of inside it. Returns
    ///     <paramref name="searchStart"/> unchanged when the object is not a dictionary, or its
    ///     dictionary is not immediately followed by <c>stream</c> - including when the
    ///     dictionary's own entries merely contain the word "stream" as a name, string, or other
    ///     value, which must never be mistaken for the real keyword.
    /// </summary>
    private static int SkipPastStreamPayload(byte[] buffer, int searchStart)
    {
        // Parsing the dictionary with the same tokenizer-based logic the primary (non-fallback)
        // parsing path uses (see ParseDictionaryOrStream) - rather than raw-searching for the
        // next standalone "stream" keyword within a bounded window - guarantees the keyword
        // found truly terminates this object's own dictionary, instead of being a coincidental
        // "/stream" name, a "(...stream...)" string, or some other unrelated dictionary value, or
        // even the stream keyword belonging to a different, later object entirely.
        var tokenizer = new PdfTokenizer(buffer) { Position = searchStart };
        PdfToken openToken;
        try
        {
            openToken = tokenizer.NextToken();
        }
        catch (InvalidDataException)
        {
            return searchStart;
        }

        if (openToken.Kind != PdfTokenKind.DictStart)
        {
            // This object's value is not a dictionary at all (for example a bare number, string,
            // or array), so it cannot possibly have a stream payload to skip.
            return searchStart;
        }

        Dictionary<string, PdfObject> entries;
        try
        {
            entries = ParseDictionaryEntries(tokenizer, 0);
        }
        catch (InvalidDataException)
        {
            return searchStart;
        }

        var savedPosition = tokenizer.Position;
        PdfToken next;
        try
        {
            next = tokenizer.NextToken();
        }
        catch (InvalidDataException)
        {
            return searchStart;
        }

        if (next.Kind != PdfTokenKind.Keyword || next.Text != "stream")
        {
            // A plain dictionary object (not a stream) - nothing follows to skip past.
            tokenizer.Position = savedPosition;
            return searchStart;
        }

        SkipStreamLineEnding(tokenizer);
        var dataStart = tokenizer.Position;

        // Prefer the stream dictionary's own declared /Length to bound the payload - the
        // spec-mandated way to determine where stream data ends - using the raw "endstream"
        // keyword search below only as a lightweight consistency check against it, and as the
        // sole fallback when /Length is absent, an indirect reference (not yet resolvable while
        // the cross-reference table this scan is building is itself still incomplete), or
        // inconsistent with the buffer. Relying on a raw, unbounded "endstream" search alone would
        // end the protected range too early whenever the payload's own bytes happen to contain a
        // coincidental "endstream" byte sequence of their own, followed by a non-regular byte.
        if (entries.TryGetValue("Length", out var lengthValue) &&
            lengthValue.Kind == PdfKind.Number &&
            TryGetDeclaredStreamEnd(buffer, dataStart, lengthValue.Number, out var declaredEnd))
        {
            return declaredEnd;
        }

        // Unlike the dictionary above, the payload itself can legitimately be large and binary,
        // so this search for the matching "endstream" keyword operates on raw bytes rather than
        // tokens (an arbitrary payload byte is not valid PDF syntax for a tokenizer to walk
        // through). This is a deliberate, acknowledged limitation rather than something this
        // check fully closes: binary payload data can legitimately, coincidentally contain a
        // byte-exact "endstream" (and even "endstream"+"endobj") sequence of its own partway
        // through, before the real end of the payload, and no finite pattern-matching rule can
        // distinguish that from the genuine terminator without a trustworthy declared length -
        // this is an inherent consequence of the PDF format itself (a stream's data is defined to
        // be arbitrary bytes, with no in-band escaping of its own terminator), not a gap specific
        // to this implementation. It is also the reason every other mainstream recovery
        // implementation we are aware of (for example the documented "attempting to recover
        // stream length" fallback in qpdf, and the "clean" repair pass in mutool) takes the same
        // approach of searching raw bytes for the next "endstream" when /Length cannot be
        // trusted, without claiming to solve this case fully either. Requiring the match to
        // additionally be followed by "endobj" (the one genuine structural neighbor every real
        // stream object has) is retained here purely as a cheap, strictly-safer-than-nothing
        // consistency check that rejects some, but provably not all, coincidental matches; a
        // worst-case false match here can at most cause this one already-malformed document's
        // recovery to produce partial or no content (the documented contract of this fallback
        // path), never a crash, since every caller of the resulting offsets already handles a
        // subsequently-unparseable object by catching <see cref="InvalidDataException"/> and
        // treating that one object as unrecoverable.
        return FindStructurallyValidEndstream(buffer, dataStart);
    }

    /// <summary>
    ///     Searches forward from <paramref name="dataStart"/> for the first <c>endstream</c>
    ///     keyword match that is itself immediately followed (after tolerating an optional
    ///     end-of-line sequence) by the literal <c>endobj</c> keyword - the structure every
    ///     genuine stream object has - skipping over any earlier match that fails this check as
    ///     coincidental payload noise. Returns <paramref name="dataStart"/> if no match at all,
    ///     structurally valid or not, exists before the end of the buffer.
    /// </summary>
    /// <remarks>
    ///     This is only ever reached when the stream's own declared <c>/Length</c> is absent, an
    ///     unresolvable indirect reference, or inconsistent with the buffer - that is, the
    ///     document is already corrupt in a way the PDF specification provides no defined recovery
    ///     for. See the caller's remarks for why no amount of additional pattern-matching here can
    ///     fully close the resulting ambiguity, and why that residual risk is accepted rather than
    ///     chased with further heuristics.
    /// </remarks>
    private static int FindStructurallyValidEndstream(byte[] buffer, int dataStart)
    {
        var position = dataStart;
        while (true)
        {
            var endStreamIndex = IndexOfKeyword(buffer, position, buffer.Length, "endstream"u8, requirePrecedingBoundary: false);
            if (endStreamIndex < 0)
            {
                return dataStart;
            }

            var afterKeyword = endStreamIndex + "endstream"u8.Length;
            if (IsFollowedByEndObjKeyword(buffer, afterKeyword))
            {
                return afterKeyword;
            }

            position = afterKeyword;
        }
    }

    /// <summary>
    ///     Returns whether <paramref name="index"/>, after tolerating an optional end-of-line
    ///     sequence, is immediately followed by the literal <c>endobj</c> keyword - the structure
    ///     every genuine <c>endstream</c> keyword is followed by.
    /// </summary>
    private static bool IsFollowedByEndObjKeyword(byte[] buffer, int index)
    {
        var afterEol = index;
        if (afterEol < buffer.Length && buffer[afterEol] == (byte)'\r')
        {
            afterEol++;
        }

        if (afterEol < buffer.Length && buffer[afterEol] == (byte)'\n')
        {
            afterEol++;
        }

        var endObjIndex = IndexOfKeyword(
            buffer,
            afterEol,
            Math.Min(afterEol + "endobj"u8.Length, buffer.Length),
            "endobj"u8,
            requirePrecedingBoundary: false);
        return endObjIndex == afterEol;
    }

    /// <summary>
    ///     Validates a stream dictionary's declared <c>/Length</c> against the buffer - it must be
    ///     a non-negative integer landing fully within the buffer, and the bytes immediately
    ///     following it (after tolerating an optional end-of-line sequence, since several
    ///     real-world producers include one despite it not being strictly required) must be the
    ///     literal <c>endstream</c> keyword - and if so, returns the index just past that keyword.
    /// </summary>
    private static bool TryGetDeclaredStreamEnd(byte[] buffer, int dataStart, double declaredLength, out int payloadEnd)
    {
        payloadEnd = 0;
        if (declaredLength < 0 || !double.IsInteger(declaredLength) || declaredLength > buffer.Length)
        {
            return false;
        }

        var declaredDataEnd = dataStart + (long)declaredLength;
        if (declaredDataEnd > buffer.Length)
        {
            return false;
        }

        var afterData = (int)declaredDataEnd;
        if (afterData < buffer.Length && buffer[afterData] == (byte)'\r')
        {
            afterData++;
        }

        if (afterData < buffer.Length && buffer[afterData] == (byte)'\n')
        {
            afterData++;
        }

        var endStreamIndex = IndexOfKeyword(
            buffer,
            afterData,
            Math.Min(afterData + "endstream"u8.Length, buffer.Length),
            "endstream"u8,
            requirePrecedingBoundary: false);
        if (endStreamIndex != afterData)
        {
            // The declared /Length does not land on a literal "endstream" keyword, so it cannot
            // be trusted (for example it is stale, wrong, or an indirect reference the caller
            // already excluded) - let the caller fall back to its own raw "endstream" search.
            return false;
        }

        payloadEnd = endStreamIndex + "endstream"u8.Length;
        return true;
    }

    /// <summary>
    ///     Finds the first standalone occurrence of <paramref name="keyword"/> - one not
    ///     immediately followed by another regular byte, and (unless
    ///     <paramref name="requirePrecedingBoundary"/> is <see langword="false"/>) not immediately
    ///     preceded by one either - so a match is never found inside a larger token or incidental
    ///     binary-noise byte run - at or after <paramref name="startIndex"/> and before
    ///     <paramref name="searchLimit"/>. Returns -1 if no such occurrence exists in that range.
    ///     <para>
    ///         "Regular byte" here means exactly what <see cref="PdfTokenizer"/> itself treats as
    ///         part of a keyword token: any byte that is not PDF whitespace and not one of the PDF
    ///         delimiter characters (<c>( ) &lt; &gt; [ ] { } / %</c>). An earlier version of this
    ///         check instead only recognized ASCII letters and digits as boundary bytes, which
    ///         under-approximated the tokenizer's own notion of a token boundary: a byte such as
    ///         <c>#</c> is neither a letter/digit nor PDF whitespace/a delimiter, so a sequence
    ///         like <c>/foo#trailer</c> was wrongly treated as ending the identifier at <c>#</c>,
    ///         exposing <c>trailer</c> as a standalone match even though <see cref="PdfTokenizer"/>
    ///         would read <c>foo#trailer</c> as a single keyword token. Matching the tokenizer's
    ///         own boundary rule exactly closes that gap.
    ///     </para>
    /// </summary>
    /// <param name="buffer">The raw document buffer to search.</param>
    /// <param name="startIndex">The byte index at which to start searching (inclusive).</param>
    /// <param name="searchLimit">The byte index at which to stop searching (exclusive).</param>
    /// <param name="keyword">The literal keyword bytes to search for.</param>
    /// <param name="requirePrecedingBoundary">
    ///     Whether the byte immediately before a candidate match must be a non-regular byte.
    ///     This must be <see langword="false"/> for a keyword such as <c>endstream</c>, whose
    ///     preceding byte is the final byte of an arbitrary, possibly binary, payload rather than
    ///     a continuation of a textual PDF token - that byte can legitimately be alphanumeric (or
    ///     any other regular byte) by coincidence, and requiring otherwise would wrongly reject a
    ///     genuine match.
    /// </param>
    private static int IndexOfKeyword(
        byte[] buffer,
        int startIndex,
        int searchLimit,
        ReadOnlySpan<byte> keyword,
        bool requirePrecedingBoundary = true)
    {
        var maxStart = Math.Min(searchLimit, buffer.Length) - keyword.Length;
        for (var i = Math.Max(startIndex, 0); i <= maxStart; i++)
        {
            var isMatch = true;
            for (var k = 0; k < keyword.Length; k++)
            {
                if (buffer[i + k] != keyword[k])
                {
                    isMatch = false;
                    break;
                }
            }

            if (!isMatch)
            {
                continue;
            }

            if (requirePrecedingBoundary && i > 0 && IsMarkerRegularByte(buffer[i - 1]))
            {
                continue;
            }

            var after = i + keyword.Length;
            if (after < buffer.Length && IsMarkerRegularByte(buffer[after]))
            {
                continue;
            }

            return i;
        }

        return -1;
    }

    /// <summary>
    ///     Attempts to parse a <c>N G</c> object number/generation header immediately preceding
    ///     the byte index of a literal <c>obj</c> keyword match, walking backward over the
    ///     generation digits, the separating whitespace, and the object number digits. The object
    ///     number's own leading boundary is also verified: it must not be immediately preceded by
    ///     another regular byte (which would make the digits only the suffix of a larger token)
    ///     or by <c>/</c> (which would make them a PDF name token's text rather than a standalone
    ///     number).
    /// </summary>
    private static bool TryParseObjectHeaderBackward(byte[] buffer, int objIndex, out int objectNumber, out int headerStart)
    {
        objectNumber = 0;
        headerStart = 0;

        var cursor = objIndex;
        var sectionEnd = cursor;
        cursor = SkipMarkerBytesBackward(buffer, cursor, IsMarkerWhitespace);
        if (cursor == sectionEnd)
        {
            return false;
        }

        sectionEnd = cursor;
        cursor = SkipMarkerBytesBackward(buffer, cursor, IsMarkerDigit);
        if (cursor == sectionEnd)
        {
            return false;
        }

        sectionEnd = cursor;
        cursor = SkipMarkerBytesBackward(buffer, cursor, IsMarkerWhitespace);
        if (cursor == sectionEnd)
        {
            return false;
        }

        var numberEnd = cursor;
        cursor = SkipMarkerBytesBackward(buffer, cursor, IsMarkerDigit);
        if (cursor == numberEnd)
        {
            return false;
        }

        var numberStart = cursor;

        // Require a genuine token boundary immediately before the object number: without this, a
        // decoy such as "foo3 0 obj" would be accepted as object 3, even though "foo3" is
        // lexically a single regular-byte token under the PDF grammar and "3" is only its suffix,
        // not a standalone number token of its own. A preceding '/' must also be rejected even
        // though '/' is itself a delimiter (not a regular byte): per the PDF specification a name
        // token begins with '/' directly followed by its regular characters with no intervening
        // whitespace, so (for example) "/3 0 obj" has "/3" as a name token whose text happens to
        // read "3" - a legitimate dictionary value any real document can contain - never a
        // standalone object-number token, and must not be parsed as one.
        if (numberStart > 0 && (IsMarkerRegularByte(buffer[numberStart - 1]) || buffer[numberStart - 1] == (byte)'/'))
        {
            return false;
        }

        var parsedNumber = 0L;
        for (var i = numberStart; i < numberEnd; i++)
        {
            parsedNumber = parsedNumber * 10 + (buffer[i] - (byte)'0');
            if (parsedNumber > int.MaxValue)
            {
                // An implausibly large object number is almost certainly a false-positive match
                // against binary noise rather than a genuine object header.
                return false;
            }
        }

        objectNumber = (int)parsedNumber;
        headerStart = numberStart;
        return true;
    }

    /// <summary>Walks <paramref name="cursor"/> backward while the preceding byte satisfies <paramref name="predicate"/>.</summary>
    private static int SkipMarkerBytesBackward(byte[] buffer, int cursor, Func<byte, bool> predicate)
    {
        while (cursor > 0 && predicate(buffer[cursor - 1]))
        {
            cursor--;
        }

        return cursor;
    }

    private static bool IsMarkerWhitespace(byte b) => b is 0x00 or 0x09 or 0x0A or 0x0C or 0x0D or 0x20;

    private static bool IsMarkerDigit(byte b) => b is >= (byte)'0' and <= (byte)'9';

    private static bool IsMarkerDelimiter(byte b) =>
        b is (byte)'(' or (byte)')' or (byte)'<' or (byte)'>' or (byte)'[' or (byte)']' or (byte)'{' or (byte)'}' or (byte)'/' or (byte)'%';

    /// <summary>
    ///     Whether <paramref name="b"/> is a "regular" byte under the PDF grammar - that is,
    ///     neither PDF whitespace nor a PDF delimiter character - matching exactly the rule
    ///     <see cref="PdfTokenizer"/> itself uses (via its own private <c>IsRegular</c>) to decide
    ///     which bytes belong to the same keyword token. This is intentionally broader than
    ///     "ASCII letter or digit": PDF permits other bytes (for example <c>#</c>, <c>!</c>,
    ///     <c>$</c>) inside a regular run, and a boundary check narrower than the tokenizer's own
    ///     would wrongly treat such a byte as ending a token when the tokenizer would not.
    /// </summary>
    private static bool IsMarkerRegularByte(byte b) => !IsMarkerWhitespace(b) && !IsMarkerDelimiter(b);

    /// <summary>
    ///     If <paramref name="position"/> is the first byte of a PDF comment (<c>%</c> through
    ///     the end of its line) or a literal string - either round-bracket <c>(...)</c>, with
    ///     backslash-escaped bytes and balanced nesting, or angle-bracket <c>&lt;...&gt;</c>
    ///     hex, carefully distinguished from a dictionary's <c>&lt;&lt;</c> delimiter - returns
    ///     the index of the first byte past it. Otherwise returns <paramref name="position"/>
    ///     unchanged.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This is what lets <see cref="ScanObjectOffsets"/> and
    ///         <see cref="ScanForTrailerDictionary"/> reject an <c>N G obj</c>/<c>trailer</c>
    ///         byte sequence embedded inside a PDF comment or string by construction, rather than
    ///         by a formatting-convention heuristic. An earlier approach instead required such a
    ///         candidate to begin at the start of its own line - a real-world convention, but not
    ///         one the PDF grammar actually requires, and not a sound check either way: a decoy
    ///         could still be crafted to sit at the start of a line while remaining lexically
    ///         inside an enclosing multi-line string, and a genuine header not conventionally
    ///         placed at a line's start (the grammar permits, for example, two object headers on
    ///         the same physical line) would be wrongly rejected. Tracking the actual PDF lexical
    ///         grammar for comments and strings instead closes the false-positive case completely
    ///         (a comment or string's contents are structurally unreachable as a keyword match)
    ///         while also removing the false-negative one (nothing about a header's position on
    ///         its line matters any more).
    ///     </para>
    ///     <para>
    ///         If a <c>(</c> or <c>&lt;</c> byte is encountered that turns out not to be the start
    ///         of a real string - for example binary noise containing a stray delimiter byte with
    ///         no matching close before the end of the buffer - this degrades safely by leaving
    ///         the position unchanged, so the caller simply treats that one byte as ordinary
    ///         content instead of skipping an unbounded remainder of the file.
    ///     </para>
    /// </remarks>
    private static int AdvancePastNonSyntax(byte[] buffer, int position)
    {
        var b = buffer[position];
        if (b == (byte)'%')
        {
            var end = position + 1;
            while (end < buffer.Length && buffer[end] is not ((byte)'\n' or (byte)'\r'))
            {
                end++;
            }

            return end;
        }

        if (b == (byte)'(')
        {
            return SkipLiteralString(buffer, position);
        }

        if (b == (byte)'<' && (position + 1 >= buffer.Length || buffer[position + 1] != (byte)'<'))
        {
            return SkipHexString(buffer, position);
        }

        return position;
    }

    /// <summary>
    ///     Skips a literal string starting at the <c>(</c> byte index <paramref name="start"/>,
    ///     tracking balanced nested parentheses and backslash-escaped bytes per the PDF
    ///     specification's string-literal grammar, and returns the index just past the matching
    ///     closing <c>)</c>. Returns <paramref name="start"/> unchanged if no matching close is
    ///     found before the end of the buffer.
    /// </summary>
    private static int SkipLiteralString(byte[] buffer, int start)
    {
        var depth = 1;
        var cursor = start + 1;
        while (cursor < buffer.Length)
        {
            var b = buffer[cursor];
            if (b == (byte)'\\')
            {
                // An escaped byte (including an escaped parenthesis) never affects nesting depth.
                cursor += 2;
                continue;
            }

            if (b == (byte)'(')
            {
                depth++;
            }
            else if (b == (byte)')')
            {
                depth--;
                if (depth == 0)
                {
                    return cursor + 1;
                }
            }

            cursor++;
        }

        return start;
    }

    /// <summary>
    ///     Skips a hex string starting at the <c>&lt;</c> byte index <paramref name="start"/> and
    ///     returns the index just past the matching closing <c>&gt;</c>. Returns
    ///     <paramref name="start"/> unchanged if no closing <c>&gt;</c> is found before the end of
    ///     the buffer.
    /// </summary>
    private static int SkipHexString(byte[] buffer, int start)
    {
        var cursor = start + 1;
        while (cursor < buffer.Length)
        {
            if (buffer[cursor] == (byte)'>')
            {
                return cursor + 1;
            }

            cursor++;
        }

        return start;
    }

    /// <summary>
    ///     After a linear scan has located every directly-offset object (<c>N G obj</c> markers),
    ///     also registers the objects nested inside any discovered object streams
    ///     (<c>/Type /ObjStm</c>). A compressed object has no literal <c>N G obj</c> marker of its
    ///     own in the raw buffer - it exists only inside its container stream's decompressed body
    ///     - so without this pass, any page or resource reachable only through an object stream
    ///     would be wrongly reported as missing whenever normal cross-reference parsing has failed
    ///     and this linear scan is the document's only remaining route to a working object table.
    ///     This scan examines every directly-located <c>/Type /ObjStm</c> candidate eagerly and
    ///     independently of whether it is actually needed to resolve the document's catalog, so a
    ///     single unrelated object stream must never be allowed to abort recovery of every other
    ///     object: both <see cref="InvalidDataException"/> (malformed stream data) and
    ///     <see cref="UnsupportedImageFeatureException"/> (well-formed data using a filter this
    ///     library does not implement, for example a stream filter decoding path it does not
    ///     support) are caught here for that reason - the two types are deliberately not a common
    ///     base type (see <see cref="UnsupportedImageFeatureException"/>'s own remarks), so both
    ///     must be listed explicitly to treat an unusable object stream the same way regardless of
    ///     which of the two reasons made it unusable: skip only that one stream and keep
    ///     recovering every other object normally.
    /// </summary>
    private void RegisterCompressedObjectsFromObjectStreams()
    {
        // Snapshot the object numbers before mutating `_xref` within the loop below.
        foreach (var streamNumber in _xref.Keys.ToArray())
        {
            PdfObject container;
            byte[] decoded;
            try
            {
                container = GetObject(streamNumber);
                if (container.Kind != PdfKind.Stream || GetNameValue(container, "Type") != "ObjStm")
                {
                    continue;
                }

                decoded = GetStreamDecodedBytes(container);
            }
            catch (InvalidDataException)
            {
                continue;
            }
            catch (UnsupportedImageFeatureException)
            {
                continue;
            }

            if (container.Get("N") is not { Kind: PdfKind.Number } countObject)
            {
                continue;
            }

            var count = (int)countObject.Number;
            if (count < 0 || count > decoded.Length)
            {
                continue;
            }

            var headerTokenizer = new PdfTokenizer(decoded);
            for (var i = 0; i < count; i++)
            {
                if (headerTokenizer.Position >= decoded.Length)
                {
                    break;
                }

                var numberToken = TryNextToken(headerTokenizer);
                var offsetToken = TryNextToken(headerTokenizer);
                if (numberToken is not { Kind: PdfTokenKind.Number } || offsetToken is not { Kind: PdfTokenKind.Number })
                {
                    break;
                }

                // A direct "N G obj" marker (if one somehow also exists for this number) is more
                // trustworthy than an assumed containment index, so never overwrite an existing
                // entry here.
                var containedNumber = (int)numberToken.Value.Number;
                if (!_xref.ContainsKey(containedNumber))
                {
                    _xref[containedNumber] = XrefEntry.CreateCompressed(streamNumber, i);
                }
            }
        }
    }

    /// <summary>
    ///     Reads the next token, treating a malformed token - for example a hex string containing
    ///     a byte that is neither a hex digit nor whitespace - as unparsable noise rather than
    ///     letting the exception abort the whole header scan. This matters because
    ///     <see cref="RegisterCompressedObjectsFromObjectStreams"/> tokenizes an object stream's
    ///     own decompressed header bytes (the <c>N1 O1 N2 O2 ...</c> pairs following its
    ///     <c>/Type /ObjStm</c> dictionary), which - unlike a well-formed document's normal
    ///     content - cannot be trusted to be valid PDF syntax when this linear-scan fallback is
    ///     running at all. On failure the tokenizer is resynchronized by advancing at least one
    ///     byte past the token's start position (guaranteeing forward progress even when the
    ///     failing token consumed zero bytes before throwing), and <see langword="null"/> is
    ///     returned so the caller can simply stop registering further entries from that one
    ///     object stream (its other already-registered entries, and every other object stream,
    ///     are unaffected).
    /// </summary>
    private static PdfToken? TryNextToken(PdfTokenizer tokenizer)
    {
        var start = tokenizer.Position;
        try
        {
            return tokenizer.NextToken();
        }
        catch (InvalidDataException)
        {
            if (tokenizer.Position <= start)
            {
                tokenizer.Position = start + 1;
            }

            return null;
        }
    }

    /// <summary>
    ///     Scans the buffer for every literal <c>trailer</c> keyword byte marker and parses the
    ///     dictionary that follows it, keeping the last successfully parsed one (a later
    ///     occurrence - for example from an incremental update - overrides an earlier one). This
    ///     locates the keyword itself via a raw byte search rather than <see cref="PdfTokenizer"/>
    ///     tokenizing the whole buffer, for the same reason <see cref="ScanObjectOffsets"/> does:
    ///     real-world binary stream noise routinely derails a general-purpose tokenizer long
    ///     before it would ever reach a genuine "trailer" keyword. As in
    ///     <see cref="ScanObjectOffsets"/>, <see cref="AdvancePastNonSyntax"/> is consulted at
    ///     every position so a "trailer" byte sequence embedded inside a comment or string can
    ///     never be mistaken for the real keyword, soundly rather than via a formatting
    ///     convention.
    /// </summary>
    /// <param name="streamPayloadRanges">
    ///     The stream payload ranges already identified by <see cref="ScanObjectOffsets"/>; a
    ///     "trailer" byte-sequence match found inside one of these ranges is ignored rather than
    ///     parsed, since it is necessarily just coincidental content inside some object's
    ///     compressed/binary stream data (for example embedded verbatim inside a PDF being
    ///     recovered, or compressed content that happens to decompress-match nothing in
    ///     particular) rather than the document's real trailer keyword. <paramref
    ///     name="streamPayloadRanges"/> is produced by <see cref="ScanObjectOffsets"/>'s single
    ///     left-to-right pass in increasing, non-overlapping <c>Start</c> order, so membership is
    ///     tested here with a single monotonically-advancing index alongside the scan position
    ///     rather than a per-position linear search of the whole list - keeping this scan overall
    ///     linear in the buffer size instead of quadratic in stream-object count.
    /// </param>
    private PdfObject? ScanForTrailerDictionary(List<(int Start, int End)> streamPayloadRanges)
    {
        var buffer = _buffer;
        PdfObject? last = null;
        var i = 0;
        var rangeIndex = 0;

        while (i < buffer.Length)
        {
            var afterNonSyntax = AdvancePastNonSyntax(buffer, i);
            if (afterNonSyntax != i)
            {
                i = afterNonSyntax;
                continue;
            }

            while (rangeIndex < streamPayloadRanges.Count && i >= streamPayloadRanges[rangeIndex].End)
            {
                rangeIndex++;
            }

            if (rangeIndex < streamPayloadRanges.Count &&
                i >= streamPayloadRanges[rangeIndex].Start && i < streamPayloadRanges[rangeIndex].End)
            {
                // Jump straight past the whole payload range - it is not real PDF syntax, so
                // neither comment/string tracking nor keyword matching apply inside it.
                i = streamPayloadRanges[rangeIndex].End;
                continue;
            }

            if (!IsKeywordAt(buffer, i, "trailer"u8))
            {
                i++;
                continue;
            }

            var after = i + "trailer"u8.Length;
            var tokenizer = new PdfTokenizer(buffer) { Position = after };
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

            i = after;
        }

        return last;
    }

    /// <summary>
    ///     Returns whether <paramref name="keyword"/> matches the buffer exactly at
    ///     <paramref name="position"/>, applying the same regular-byte boundary rules as
    ///     <see cref="IndexOfKeyword"/> (so a match inside a larger token, such as the tail
    ///     of a longer word, is rejected) rather than merely comparing bytes. A match is also
    ///     rejected when immediately preceded by <c>/</c>: per the PDF specification a name token
    ///     begins with <c>/</c> directly followed by its regular characters with no intervening
    ///     whitespace, so (for example) <c>/trailer</c> is a <em>name</em> whose text happens to
    ///     read "trailer" - a legitimate dictionary key or value any real document can contain -
    ///     never the <c>trailer</c> keyword itself, and must not be parsed as one.
    /// </summary>
    private static bool IsKeywordAt(byte[] buffer, int position, ReadOnlySpan<byte> keyword) =>
        position + keyword.Length <= buffer.Length &&
        (position == 0 || buffer[position - 1] != (byte)'/') &&
        IndexOfKeyword(buffer, position, position + keyword.Length, keyword) == position;

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
