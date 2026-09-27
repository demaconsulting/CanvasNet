using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Codecs;

public static partial class PngCodec
{
    /// <summary>
    ///     Validates that <paramref name="stream"/> begins with the fixed 8-byte PNG signature,
    ///     consuming exactly those 8 bytes.
    /// </summary>
    private static void ValidateSignature(Stream stream)
    {
        var signature = ReadExactly(stream, Signature.Length, "PNG signature");
        for (var i = 0; i < Signature.Length; i++)
        {
            if (signature[i] != Signature[i])
            {
                throw new InvalidDataException("Not a PNG file (missing PNG signature).");
            }
        }
    }

    /// <summary>
    ///     The width, height, color type, and bit depth parsed from a PNG's <c>IHDR</c> chunk,
    ///     produced by <see cref="ReadChunks"/> once the chunk stream has been fully consumed
    ///     through <c>IEND</c> (or by <see cref="ReadIhdrOnly"/>, which never reads past IHDR).
    /// </summary>
    private readonly record struct PngHeader(int Width, int Height, int ColorType, int BitDepth, int InterlaceMethod);

    /// <summary>
    ///     Tracks whether the mandatory <c>IHDR</c> chunk has been seen and accumulates the
    ///     header's field values, plus the optional <c>PLTE</c> and <c>tRNS</c> chunk payloads,
    ///     as chunks are read, threaded through <see cref="ProcessChunk"/> while
    ///     <see cref="ReadChunks"/> walks the chunk stream.
    /// </summary>
    private sealed class ChunkReadState
    {
        /// <summary>Whether the mandatory IHDR chunk has been seen yet.</summary>
        public bool IhdrSeen { get; set; }

        /// <summary>Whether the mandatory IEND chunk has been seen yet.</summary>
        public bool IendSeen { get; set; }

        /// <summary>The image width in pixels, populated by the IHDR chunk.</summary>
        public int Width { get; set; }

        /// <summary>The image height in pixels, populated by the IHDR chunk.</summary>
        public int Height { get; set; }

        /// <summary>The PNG color type, populated by the IHDR chunk.</summary>
        public int ColorType { get; set; }

        /// <summary>The PNG bit depth, populated by the IHDR chunk.</summary>
        public int BitDepth { get; set; }

        /// <summary>The raw PLTE chunk data (RGB triples), or null if no PLTE chunk was present.</summary>
        public byte[]? PlteData { get; set; }

        /// <summary>The raw tRNS chunk data, or null if no tRNS chunk was present.</summary>
        public byte[]? TrnsData { get; set; }

        /// <summary>Whether a PLTE chunk has already been seen.</summary>
        public bool PlteSeen { get; set; }

        /// <summary>Whether a tRNS chunk has already been seen.</summary>
        public bool TrnsSeen { get; set; }

        /// <summary>Whether an IDAT chunk has already been seen.</summary>
        public bool IdatSeen { get; set; }

        /// <summary>
        ///     Whether the run of consecutive IDAT chunks has already ended - set the moment a
        ///     non-IDAT chunk is processed after at least one IDAT chunk has been seen. The PNG
        ///     specification requires every IDAT chunk to be consecutive, so a further IDAT chunk
        ///     encountered once this flag is set indicates a non-conforming file.
        /// </summary>
        public bool IdatRunEnded { get; set; }
    }

    /// <summary>
    ///     Reads and validates every chunk from <paramref name="stream"/> until (and including)
    ///     <c>IEND</c>, accumulating <c>IDAT</c> payload bytes into <paramref name="idatData"/>
    ///     and returning the parsed <c>IHDR</c> fields plus any <c>PLTE</c>/<c>tRNS</c> payloads.
    ///     The PNG specification requires <c>IEND</c> to be the final chunk in the datastream, so
    ///     once the <c>IEND</c> chunk itself has been consumed, the stream must immediately reach
    ///     end-of-file; any further byte found after <c>IEND</c> is rejected.
    /// </summary>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown, in addition to the per-chunk conditions <see cref="ProcessChunk"/> documents,
    ///     when the stream contains any further byte after the <c>IEND</c> chunk has been read.
    /// </exception>
    private static PngHeader ReadChunks(Stream stream, out byte[] idatData, out byte[]? plteData, out byte[]? trnsData)
    {
        var state = new ChunkReadState();
        using var idatStream = new MemoryStream();

        // Read chunks until IEND is encountered; ReadExactly throws InvalidDataException if the
        // stream ends before IEND is found, which correctly rejects a truncated stream
        while (!state.IendSeen)
        {
            ProcessChunk(stream, idatStream, state);
        }

        // The PNG specification requires IEND to be the final chunk in the datastream: reading
        // a single byte here (rather than relying on Stream.Length, which is unavailable for a
        // non-seekable stream) works uniformly for both seekable and non-seekable streams and
        // returns -1 only once the stream is genuinely exhausted.
        if (stream.ReadByte() != -1)
        {
            throw new InvalidDataException("Data found after IEND chunk.");
        }

        idatData = idatStream.ToArray();
        plteData = state.PlteData;
        trnsData = state.TrnsData;
        return new PngHeader(state.Width, state.Height, state.ColorType, state.BitDepth, InterlaceNone);
    }

    /// <summary>
    ///     Reads, CRC-validates, and dispatches a single chunk. <see cref="ValidateChunkLengthBeforeAllocation"/>
    ///     is this codec's single authoritative gate for every chunk-ordering/identity rule that
    ///     does not depend on a chunk's payload content - IHDR-must-be-first, IHDR's exact
    ///     13-byte length, duplicate IHDR, non-consecutive IDAT, and unrecognized critical chunks
    ///     - so it always runs, and always rejects those cases, before this method is ever
    ///     reached; this method therefore does not re-check any of them; only <em>content</em>-
    ///     dependent rules (which require the chunk's actual payload, only available once
    ///     buffered) are checked here. <c>IHDR</c> populates <paramref name="state"/>'s header
    ///     fields; <c>PLTE</c>/<c>tRNS</c> data is stored for later use by the decode step;
    ///     <c>IDAT</c> data has already been streamed directly into <paramref name="idatStream"/>
    ///     by <see cref="ReadChunkFrame"/> itself (via its <c>idatDestination</c> parameter)
    ///     rather than buffered and copied here, since <c>IDAT</c> may legitimately be very large;
    ///     <c>IEND</c> marks the chunk stream complete; and every other chunk type reaching this
    ///     method is, by elimination, a recognized-and-ignored ancillary chunk whose payload was
    ///     never buffered at all - only streamed through the CRC-32 calculation in bounded pieces
    ///     by <see cref="ReadChunkFrame"/> (see <see cref="StreamDiscardChunkPayload"/>) - so it
    ///     requires no further action here.
    /// </summary>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown for the content-dependent conditions documented inline below (duplicate/
    ///     out-of-order <c>PLTE</c>/<c>tRNS</c>, invalid <c>PLTE</c> entry count, <c>tRNS</c>
    ///     forbidden for the image's color type, non-empty <c>IEND</c> payload); see
    ///     <see cref="ValidateChunkLengthBeforeAllocation"/> for every ordering/identity rule
    ///     rejected before this method runs.
    /// </exception>
    private static void ProcessChunk(Stream stream, MemoryStream idatStream, ChunkReadState state)
    {
        var (typeBytes, data) = ReadChunkFrame(
            stream,
            (chunkTypeBytes, declaredLength) => ValidateChunkLengthBeforeAllocation(chunkTypeBytes, declaredLength, state),
            idatStream);

        // The PNG specification requires every IDAT chunk to be consecutive: the moment a
        // non-IDAT chunk is processed after at least one IDAT chunk has been seen, the IDAT run
        // has ended, so any further IDAT chunk encountered later is non-conforming (rejected by
        // ValidateChunkLengthBeforeAllocation before this method is reached)
        if (!ChunkTypeIs(typeBytes, "IDAT") && state.IdatSeen)
        {
            state.IdatRunEnded = true;
        }

        if (ChunkTypeIs(typeBytes, "IHDR"))
        {
            (state.Width, state.Height, state.ColorType, state.BitDepth, _) =
                ParseIhdr(data, enforceMaxDimension: true, validateDecodability: true);
            state.IhdrSeen = true;
        }
        else if (ChunkTypeIs(typeBytes, "PLTE"))
        {
            ProcessPlteChunk(data, state);
        }
        else if (ChunkTypeIs(typeBytes, "tRNS"))
        {
            ProcessTrnsChunk(data, state);
        }
        else if (ChunkTypeIs(typeBytes, "IDAT"))
        {
            // The payload has already been streamed directly into idatStream by ReadChunkFrame
            // (via its idatDestination parameter) as it was read, in bounded pieces, rather than
            // buffered into a single length-sized array first; data is always empty here, exactly
            // like the recognized-ancillary-chunk stream-discard case, so there is nothing left
            // to copy
            state.IdatSeen = true;
        }
        else if (ChunkTypeIs(typeBytes, "IEND"))
        {
            // The PNG specification defines IEND as always carrying zero bytes of data;
            // ValidateChunkLengthBeforeAllocation already rejects a non-zero declared length
            // before this method is reached, so data is always empty here
            state.IendSeen = true;
        }

        // Every other chunk type reaching this point (for example "tEXt", "pHYs", "gAMA") is, by
        // elimination, a recognized-and-ignored ancillary chunk: ValidateChunkLengthBeforeAllocation
        // already rejects an unrecognized critical chunk (uppercase first type byte) and any chunk
        // preceding the mandatory first IHDR, so nothing reaching here can be either of those. Its
        // CRC-32 has already been validated by ReadChunkFrame and its data was never buffered at
        // all, so no further action is required.
    }

    /// <summary>
    ///     Validates and records a <c>PLTE</c> chunk's payload into <paramref name="state"/>,
    ///     enforcing every content-dependent PLTE rule that requires the chunk's actual payload
    ///     (ordering relative to <c>tRNS</c>/<c>IDAT</c>, entry-count shape, and color-type/bit-depth
    ///     compatibility).
    /// </summary>
    /// <remarks>
    ///     Isolated from <see cref="ProcessChunk"/> as its own self-contained validation step,
    ///     independently nameable and testable from the surrounding chunk-type dispatch and from
    ///     <see cref="ProcessTrnsChunk"/>'s equivalent tRNS validation.
    /// </remarks>
    /// <param name="data">The chunk's payload bytes.</param>
    /// <param name="state">The in-progress chunk-read state, updated with the accepted palette data.</param>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown for a duplicate PLTE chunk, a PLTE chunk following tRNS or the first IDAT, an
    ///     invalid entry-count shape, a PLTE chunk on a grayscale color type, or a PLTE chunk
    ///     declaring more entries than its bit depth permits.
    /// </exception>
    private static void ProcessPlteChunk(byte[] data, ChunkReadState state)
    {
        if (state.PlteSeen)
        {
            throw new InvalidDataException("Duplicate PLTE chunk.");
        }

        if (state.IdatSeen)
        {
            throw new InvalidDataException("PLTE chunk encountered after the first IDAT chunk.");
        }

        // The PNG specification requires PLTE to precede tRNS whenever both are present,
        // regardless of color type: a tRNS chunk that has already been accepted means a PLTE
        // chunk arriving afterward is out of order, even for color types (2 and 6) where
        // PLTE is merely an optional suggested palette rather than mandatory
        if (state.TrnsSeen)
        {
            throw new InvalidDataException("PLTE chunk must precede tRNS chunk.");
        }

        // ValidateChunkLengthBeforeAllocation already bounds the declared length to at most
        // 256 entries (768 bytes), so only the multiple-of-3/non-empty shape remains to check
        if (data.Length % 3 != 0 || data.Length == 0)
        {
            throw new InvalidDataException(
                $"Invalid PNG PLTE chunk length {data.Length}; expected a positive multiple of 3.");
        }

        // The PNG specification forbids a PLTE chunk for the two grayscale color types (0 and
        // 4): grayscale samples are never resolved through a palette, so a PLTE chunk on such
        // a file cannot represent anything a conforming decoder is permitted to use
        if (state.ColorType is ColorTypeGrayscale or ColorTypeGrayscaleAlpha)
        {
            throw new InvalidDataException(
                $"PLTE chunk is not permitted for grayscale PNG color type {state.ColorType}.");
        }

        if (state.ColorType == ColorTypePalette)
        {
            var maxEntries = 1 << state.BitDepth;
            var entryCount = data.Length / 3;
            if (entryCount > maxEntries)
            {
                throw new InvalidDataException(
                    $"PNG PLTE chunk declares {entryCount} palette entries, which exceeds the maximum of " +
                    $"{maxEntries} entries permitted for a palette (color type 3) image at bit depth " +
                    $"{state.BitDepth}.");
            }
        }

        state.PlteData = data;
        state.PlteSeen = true;
    }

    /// <summary>
    ///     Validates and records a <c>tRNS</c> chunk's payload into <paramref name="state"/>,
    ///     enforcing every content-dependent tRNS rule that requires the chunk's actual payload
    ///     (ordering relative to <c>PLTE</c>/<c>IDAT</c> and color-type compatibility).
    /// </summary>
    /// <remarks>
    ///     Isolated from <see cref="ProcessChunk"/> as its own self-contained validation step,
    ///     independently nameable and testable from the surrounding chunk-type dispatch and from
    ///     <see cref="ProcessPlteChunk"/>'s equivalent PLTE validation.
    /// </remarks>
    /// <param name="data">The chunk's payload bytes.</param>
    /// <param name="state">The in-progress chunk-read state, updated with the accepted transparency data.</param>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown for a duplicate tRNS chunk, a tRNS chunk following the first IDAT, a tRNS chunk
    ///     on a color type that already carries full per-pixel alpha, or an indexed-color tRNS
    ///     chunk preceding PLTE.
    /// </exception>
    private static void ProcessTrnsChunk(byte[] data, ChunkReadState state)
    {
        if (state.TrnsSeen)
        {
            throw new InvalidDataException("Duplicate tRNS chunk.");
        }

        if (state.IdatSeen)
        {
            throw new InvalidDataException("tRNS chunk encountered after the first IDAT chunk.");
        }

        // The PNG specification forbids a tRNS chunk for the two color types that already
        // carry a full per-pixel alpha channel (grayscale-with-alpha and Truecolor-with-alpha):
        // there is nothing for a single-key-color transparency chunk to add for those formats,
        // so its presence indicates a malformed, non-conforming file rather than a feature to
        // silently ignore
        if (state.ColorType is ColorTypeGrayscaleAlpha or ColorTypeTruecolorAlpha)
        {
            throw new InvalidDataException(
                $"tRNS chunk is not permitted for PNG color type {state.ColorType}, which already " +
                "carries a full per-pixel alpha channel.");
        }

        // For indexed-color (palette) images, tRNS's per-palette-entry alpha values are
        // meaningless without the PLTE chunk they index into, so the specification requires
        // tRNS to appear after PLTE, not merely after IHDR and before the first IDAT
        if (state.ColorType == ColorTypePalette && !state.PlteSeen)
        {
            throw new InvalidDataException("tRNS chunk for indexed-color PNG must follow PLTE.");
        }

        state.TrnsData = data;
        state.TrnsSeen = true;
    }

    /// <summary>
    ///     This codec's single authoritative gate for every chunk-ordering/identity rule that
    ///     does not depend on a chunk's payload content, invoked by <see cref="ReadChunkFrame"/>
    ///     before it allocates or reads any payload buffer. It rejects: a first chunk that is not
    ///     <c>IHDR</c>, or an <c>IHDR</c> first chunk whose declared length is not exactly the 13
    ///     bytes the PNG specification mandates (mirroring <see cref="ReadIhdrChunkFrame"/>'s
    ///     identical guard used by <c>GetInfo</c>); a second <c>IHDR</c> chunk; a declared
    ///     <c>PLTE</c> or <c>tRNS</c> chunk length beyond the largest that type can legitimately
    ///     have; a non-zero declared <c>IEND</c> chunk length; a further <c>IDAT</c> chunk once
    ///     the run of consecutive <c>IDAT</c> chunks has already ended; and an unrecognized
    ///     critical chunk type (uppercase first type byte, per the PNG naming convention, and not
    ///     one of the five chunks this codec recognizes) once <c>IHDR</c> has been parsed. Because
    ///     every one of these rules is rejected here, unconditionally, before <see cref="ProcessChunk"/>
    ///     ever runs, <see cref="ProcessChunk"/> does not duplicate them.
    ///     <para>
    ///         Its <c>PLTE</c>/<c>tRNS</c> length bounds are the sole enforcement of those upper
    ///         limits (not merely a loose safety net), since <see cref="ProcessChunk"/> no longer
    ///         re-checks them; the remaining, content-dependent rules it cannot check here (exact
    ///         <c>PLTE</c> entry count against bit depth, precise <c>tRNS</c> per-color-type
    ///         validation, etc.) still run in <see cref="ProcessChunk"/> once the payload is
    ///         available. A recognized-and-ignored ancillary chunk type has no small type-specific
    ///         maximum this method could bound a declared length against - unlike <c>PLTE</c> or
    ///         <c>tRNS</c>, it may legitimately be large (for example an <c>iCCP</c> embedded color
    ///         profile or a long <c>tEXt</c> comment) - so this method does not attempt to bound
    ///         it; <see cref="ReadChunkFrame"/> instead closes that same memory-exhaustion vector
    ///         for both ancillary and <c>IDAT</c> chunks by never buffering their payload in a
    ///         single length-sized array, streaming it through the CRC-32 calculation (or into the
    ///         <c>IDAT</c> accumulator) in bounded pieces instead - see
    ///         <see cref="StreamChunkPayload"/>.
    ///     </para>
    /// </summary>
    /// <param name="typeBytes">The chunk's 4-byte type field.</param>
    /// <param name="length">The chunk's declared data length, read from the chunk header.</param>
    /// <param name="state">
    ///     The in-progress chunk-read state: <see cref="ChunkReadState.IhdrSeen"/> being
    ///     <see langword="false"/> identifies the current chunk as the very first chunk in the
    ///     file, since any earlier chunk that violated the IHDR-must-be-first rule would already
    ///     have thrown before this chunk was ever reached, while being <see langword="true"/>
    ///     identifies any further <c>IHDR</c> chunk as a duplicate; it is also used to narrow the
    ///     <c>tRNS</c> bound by color type once <c>IHDR</c> has been parsed - before that, only
    ///     the loosest (palette-sized) bound is available, which is still small enough to rule out
    ///     a memory-exhaustion attempt; <see cref="ChunkReadState.IdatRunEnded"/> identifies
    ///     whether a further <c>IDAT</c> chunk would be non-conforming.
    /// </param>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when the first chunk is not <c>IHDR</c>, an <c>IHDR</c> first chunk's declared
    ///     length is not exactly 13, a <c>PLTE</c>/<c>tRNS</c> chunk declares a length beyond the
    ///     largest value that type can legitimately have, an <c>IEND</c> chunk declares a
    ///     non-zero length, an <c>IDAT</c> chunk is declared after the <c>IDAT</c> run has already
    ///     ended, the chunk type is an unrecognized critical chunk encountered after <c>IHDR</c>,
    ///     or a second <c>IHDR</c> chunk is encountered. Also covers a chunk of any type
    ///     encountered before the mandatory first <c>IHDR</c> chunk.
    /// </exception>
    private static void ValidateChunkLengthBeforeAllocation(byte[] typeBytes, uint length, ChunkReadState state)
    {
        if (!state.IhdrSeen)
        {
            if (!ChunkTypeIs(typeBytes, "IHDR"))
            {
                throw new InvalidDataException("Chunk encountered before IHDR.");
            }

            if (length != IhdrDataLength)
            {
                throw new InvalidDataException("IHDR chunk does not have the required length of 13 bytes.");
            }
        }
        else if (ChunkTypeIs(typeBytes, "IHDR"))
        {
            // IHDR is only ever legitimate as the first chunk (handled above); a second IHDR
            // encountered later is a duplicate regardless of its declared length, so reject it
            // before ReadChunkFrame allocates a payload buffer for it, rather than only after the
            // payload has already been allocated and read
            throw new InvalidDataException("Duplicate IHDR chunk.");
        }

        if (ChunkTypeIs(typeBytes, "PLTE"))
        {
            if (length > MaxPlteDataLength)
            {
                throw new InvalidDataException(
                    $"PNG PLTE chunk declares a {length}-byte payload, which exceeds the maximum of " +
                    $"{MaxPlteDataLength} bytes ({MaxPaletteEntries} entries) permitted by the PNG " +
                    "specification, regardless of color type or bit depth.");
            }
        }
        else if (ChunkTypeIs(typeBytes, "tRNS"))
        {
            // Once IHDR has been parsed, the color type pins the exact tRNS length for grayscale
            // (2 bytes) and Truecolor (6 bytes); every other case - palette, an as-yet-unparsed
            // IHDR, or a color type for which tRNS is not even permitted - falls back to the
            // one-byte-per-palette-entry ceiling, which is still small enough to rule out a
            // large allocation while leaving the precise rejection to the checks noted above
            var maxLength = state switch
            {
                { IhdrSeen: true, ColorType: ColorTypeGrayscale } => 2,
                { IhdrSeen: true, ColorType: ColorTypeTruecolor } => 6,
                _ => MaxPaletteEntries
            };

            if (length > maxLength)
            {
                throw new InvalidDataException(
                    $"PNG tRNS chunk declares a {length}-byte payload, which exceeds the maximum of " +
                    $"{maxLength} bytes permitted for this file.");
            }
        }
        else if (ChunkTypeIs(typeBytes, "IEND") && length != 0)
        {
            // IEND always carries zero bytes of data per the PNG specification; reject a
            // non-zero declared length before ReadChunkFrame allocates a payload buffer for it,
            // rather than only after the payload has already been allocated and read
            throw new InvalidDataException(
                $"IEND chunk must have an empty payload, but declared {length} bytes.");
        }

        if (ChunkTypeIs(typeBytes, "IDAT"))
        {
            // The PNG specification requires every IDAT chunk to be consecutive; once the run
            // has ended, a further IDAT chunk is non-conforming regardless of its declared
            // length, so reject it before ReadChunkFrame allocates a payload buffer for it,
            // rather than only after the payload has already been allocated and read
            if (state.IdatRunEnded)
            {
                throw new InvalidDataException("IDAT chunks must be consecutive.");
            }
        }
        else if (state.IhdrSeen &&
                 typeBytes[0] is >= (byte)'A' and <= (byte)'Z' &&
                 !ChunkTypeIs(typeBytes, "IHDR") &&
                 !ChunkTypeIs(typeBytes, "PLTE") &&
                 !ChunkTypeIs(typeBytes, "tRNS") &&
                 !ChunkTypeIs(typeBytes, "IEND"))
        {
            // This chunk type is not one of the five chunks this codec explicitly recognizes,
            // yet its first byte is uppercase, marking it critical per the PNG specification's
            // chunk-naming convention: it is always refused outright regardless of its declared
            // length, so reject it before ReadChunkFrame allocates a payload buffer for it,
            // rather than only after the payload has already been allocated and read
            var typeName = System.Text.Encoding.ASCII.GetString(typeBytes);
            throw new InvalidDataException($"Unrecognized critical PNG chunk '{typeName}'.");
        }
    }

    /// <summary>
    ///     Reads and CRC-validates a single chunk frame (length, type, data, and CRC-32) from
    ///     <paramref name="stream"/>. The chunk type's four bytes are validated against the PNG
    ///     specification's letter/reserved-bit rules (see <see cref="ValidateChunkTypeCode"/>)
    ///     before this method otherwise interprets the type in any way, and
    ///     <paramref name="validateLengthBeforeAllocation"/>, when supplied, is invoked with the
    ///     (now type-validated) type bytes and the declared length immediately afterward - both
    ///     checks run <em>before</em> the length-dependent payload buffer below is allocated and
    ///     read, so a crafted chunk with a malformed type code, or a declared length that already
    ///     violates a type-specific maximum, is rejected without first forcing a large allocation.
    ///     A chunk type that is not one of the five this codec recognizes and buffers
    ///     (<c>IHDR</c>/<c>PLTE</c>/<c>tRNS</c>/<c>IDAT</c>/<c>IEND</c>) can only reach the payload
    ///     step below as a recognized-and-ignored ancillary chunk (lowercase first type byte): an
    ///     unrecognized <em>critical</em> chunk, or any chunk preceding the mandatory first
    ///     <c>IHDR</c>, is always rejected by <paramref name="validateLengthBeforeAllocation"/>
    ///     before this point. Such an ancillary chunk's payload is never consumed by
    ///     <see cref="ProcessChunk"/>, so - unlike the other four chunk types, whose data is
    ///     actually used - its bytes are streamed through the CRC-32 calculation in small, bounded
    ///     pieces via <see cref="StreamDiscardChunkPayload"/> and discarded immediately, rather
    ///     than buffered in a single length-sized array; this bounds peak allocation to
    ///     <see cref="AncillaryChunkStreamBufferSize"/> regardless of how large a declared
    ///     ancillary chunk length is, closing the same memory-exhaustion vector that
    ///     <paramref name="validateLengthBeforeAllocation"/> closes for the other five chunk
    ///     types (which do have a small type-specific maximum to check up front). An <c>IDAT</c>
    ///     chunk has no such small type-specific maximum either - a conforming encoder may
    ///     legitimately emit an entire large image's compressed data as one very large <c>IDAT</c>
    ///     chunk - so, when <paramref name="idatDestination"/> is supplied, an <c>IDAT</c> chunk's
    ///     payload is likewise never buffered in a single length-sized array: it is instead read
    ///     directly into <paramref name="idatDestination"/> in the same bounded pieces, via the
    ///     shared <see cref="StreamChunkPayload"/> helper that also backs
    ///     <see cref="StreamDiscardChunkPayload"/>, closing the identical memory-exhaustion vector
    ///     for <c>IDAT</c> that the ancillary-chunk path already closes.
    /// </summary>
    /// <param name="stream">The stream to read the chunk frame from.</param>
    /// <param name="validateLengthBeforeAllocation">
    ///     Optional callback invoked with the chunk's type bytes and declared length before the
    ///     data payload is allocated or read, allowing a caller to reject a declared length that
    ///     already exceeds a type-specific maximum (for example <c>PLTE</c> or <c>tRNS</c>)
    ///     without first allocating a buffer of that size. Should throw
    ///     <see cref="System.IO.InvalidDataException"/> to reject; a non-throwing return accepts
    ///     the declared length.
    /// </param>
    /// <param name="idatDestination">
    ///     Optional destination stream that, when supplied and the chunk type is <c>IDAT</c>,
    ///     receives the chunk's payload directly as it is read in bounded pieces, instead of the
    ///     payload being allocated as a single length-sized array first. <see cref="ProcessChunk"/>
    ///     always passes its <c>idatStream</c> accumulator here; every other caller of this method
    ///     reads only a first chunk (always expected to be <c>IHDR</c>, never <c>IDAT</c>) and
    ///     leaves this at its default <see langword="null"/>, which falls back to the ordinary
    ///     buffered-array path for <c>IDAT</c> as a safe default.
    /// </param>
    /// <returns>
    ///     The chunk's 4-byte type field and its data payload - an empty array for a
    ///     recognized-and-ignored ancillary chunk (whose payload is stream-discarded rather than
    ///     buffered, since <see cref="ProcessChunk"/> never reads the data for that case) or for
    ///     an <c>IDAT</c> chunk streamed into <paramref name="idatDestination"/> (whose payload
    ///     has already been written there directly, so there is nothing left to return).
    /// </returns>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when the declared chunk length exceeds the supported range, the chunk type is
    ///     not four ASCII letters or violates the reserved-bit rule (see
    ///     <see cref="ValidateChunkTypeCode"/>), <paramref name="validateLengthBeforeAllocation"/>
    ///     rejects the declared length, the stream ends before the full chunk frame has been
    ///     read, or the chunk's CRC-32 does not match its type and data.
    /// </exception>
    private static (byte[] TypeBytes, byte[] Data) ReadChunkFrame(
        Stream stream,
        Action<byte[], uint>? validateLengthBeforeAllocation = null,
        MemoryStream? idatDestination = null)
    {
        var lengthBytes = ReadExactly(stream, 4, "chunk length");
        var length = ReadUInt32Be(lengthBytes, 0);
        if (length > int.MaxValue)
        {
            throw new InvalidDataException("Chunk length exceeds the supported range.");
        }

        var typeBytes = ReadExactly(stream, 4, "chunk type");
        ValidateChunkTypeCode(typeBytes);
        validateLengthBeforeAllocation?.Invoke(typeBytes, length);

        // A chunk type that is not one of the five this codec recognizes and buffers can only be
        // a recognized-and-ignored ancillary chunk at this point (see the summary above); its
        // payload is never consumed by ProcessChunk, so it is streamed through the CRC-32
        // calculation and discarded in bounded pieces instead of being buffered in a single
        // length-sized array
        if (!IsBufferedChunkType(typeBytes))
        {
            StreamDiscardChunkPayload(stream, typeBytes, length);
            return (typeBytes, []);
        }

        // An IDAT chunk may legitimately carry the entire compressed image as a single, very
        // large payload - the PNG specification does not require encoders to split IDAT into
        // small pieces - so, when the caller has supplied a destination to stream the payload
        // into (ProcessChunk always does, passing its idatStream accumulator), the payload is
        // read directly into that destination in the same bounded pieces used for ancillary
        // chunks, rather than allocated as one array of the full declared length first
        if (ChunkTypeIs(typeBytes, "IDAT") && idatDestination != null)
        {
            StreamChunkPayload(stream, typeBytes, length, idatDestination.Write);
            return (typeBytes, []);
        }

        var data = length == 0 ? [] : ReadExactly(stream, (int)length, "chunk data");
        var crcBytes = ReadExactly(stream, 4, "chunk CRC");
        var expectedCrc = ReadUInt32Be(crcBytes, 0);

        // The CRC-32 covers the chunk type and data, but not the length field itself
        var crcInput = new byte[4 + data.Length];
        typeBytes.CopyTo(crcInput, 0);
        data.CopyTo(crcInput, 4);
        var actualCrc = ComputeCrc32(crcInput);
        if (actualCrc != expectedCrc)
        {
            throw new InvalidDataException("Corrupt PNG chunk (CRC-32 mismatch).");
        }

        return (typeBytes, data);
    }

    /// <summary>
    ///     Determines whether a chunk type is one of the five types this codec recognizes,
    ///     namely <c>IHDR</c>, <c>PLTE</c>, <c>tRNS</c>, <c>IDAT</c>, and <c>IEND</c>, as opposed
    ///     to an unrecognized (ancillary or unrecognized-critical) chunk type. Used by
    ///     <see cref="ReadChunkFrame"/> to decide between reading the payload for later use
    ///     (required for these five types - though <c>IDAT</c> is, when a destination is
    ///     supplied, streamed directly into it rather than buffered in a single array; see
    ///     <see cref="ReadChunkFrame"/>'s <c>idatDestination</c> parameter) and
    ///     stream-discarding it in bounded pieces (safe for every other, ancillary, chunk type,
    ///     whose data is never used at all).
    /// </summary>
    /// <param name="typeBytes">The chunk's 4-byte type field.</param>
    /// <returns>
    ///     <see langword="true"/> if the chunk type is one of the five recognized types;
    ///     otherwise, <see langword="false"/>.
    /// </returns>
    private static bool IsBufferedChunkType(byte[] typeBytes) =>
        ChunkTypeIs(typeBytes, "IHDR") ||
        ChunkTypeIs(typeBytes, "PLTE") ||
        ChunkTypeIs(typeBytes, "tRNS") ||
        ChunkTypeIs(typeBytes, "IDAT") ||
        ChunkTypeIs(typeBytes, "IEND");

    /// <summary>
    ///     Invoked once per bounded piece read by <see cref="StreamChunkPayload"/>, receiving that
    ///     piece's bytes as a span rather than a copied array, so a caller that only needs to
    ///     inspect or copy the bytes (for example writing them onward into another stream) never
    ///     forces an extra allocation per piece. Declared as its own delegate type - rather than
    ///     using <see cref="Action{T}"/> - because a <c>ReadOnlySpan&lt;byte&gt;</c> is a ref
    ///     struct and this codec multi-targets a framework whose C# language version does not
    ///     permit a ref struct as a generic type argument (the "allows ref struct" constraint
    ///     needed for <c>Action&lt;ReadOnlySpan&lt;byte&gt;&gt;</c> requires a newer target); an
    ///     ordinary (non-generic) delegate parameter has no such restriction.
    /// </summary>
    /// <param name="piece">The bytes of the chunk-payload piece just read.</param>
    private delegate void ChunkPayloadPieceHandler(ReadOnlySpan<byte> piece);

    /// <summary>
    ///     Reads a chunk's declared-length payload from <paramref name="stream"/> and CRC-validates
    ///     it, without ever buffering the full payload in a single array: bytes are read in pieces
    ///     of at most <see cref="AncillaryChunkStreamBufferSize"/> via the same
    ///     <see cref="ReadExactly"/> helper every other chunk read uses (so truncated-stream
    ///     behavior is identical), each piece is folded into a running CRC-32 as soon as it is
    ///     read, and every piece is then eligible for garbage collection before the next is read.
    ///     This exists purely to bound peak allocation for a chunk type whose data this codec
    ///     never needs (see <see cref="ReadChunkFrame"/>'s summary for why only such chunk types
    ///     ever reach this method) - a declared ancillary chunk length of, say, 500 MB never
    ///     forces anywhere near a 500 MB allocation, only <see cref="AncillaryChunkStreamBufferSize"/>
    ///     at a time. Implemented as a thin wrapper around the shared <see cref="StreamChunkPayload"/>
    ///     helper, passing a <see langword="null"/> per-piece action so each piece really is simply
    ///     discarded once folded into the running CRC-32; <see cref="ReadChunkFrame"/>'s
    ///     <c>IDAT</c>-streaming case reuses the same helper with a non-null action instead, to
    ///     write each piece into the accumulating <c>idatStream</c> rather than discard it.
    /// </summary>
    /// <param name="stream">The stream to read the chunk's payload and trailing CRC-32 from.</param>
    /// <param name="typeBytes">
    ///     The chunk's already-read 4-byte type field, folded into the running CRC-32 before the
    ///     payload, matching the PNG specification's CRC-32 coverage (type and data, not length).
    /// </param>
    /// <param name="length">The chunk's declared data length, already validated to be within range.</param>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when the stream ends before <paramref name="length"/> payload bytes and the
    ///     trailing 4-byte CRC-32 have been read, or the computed CRC-32 does not match the
    ///     trailing CRC-32 value read from the stream.
    /// </exception>
    private static void StreamDiscardChunkPayload(Stream stream, byte[] typeBytes, uint length) =>
        StreamChunkPayload(stream, typeBytes, length, onPieceRead: null);

    /// <summary>
    ///     Reads a chunk's declared-length payload from <paramref name="stream"/> and CRC-validates
    ///     it, without ever buffering the full payload in a single array: bytes are read in pieces
    ///     of at most <see cref="AncillaryChunkStreamBufferSize"/> via the same
    ///     <see cref="ReadExactly"/> helper every other chunk read uses (so truncated-stream
    ///     behavior is identical), each piece is folded into a running CRC-32 as soon as it is
    ///     read, and then handed to <paramref name="onPieceRead"/>, if supplied, before the next
    ///     piece is read - allowing each piece to become eligible for garbage collection
    ///     immediately afterward regardless of what <paramref name="onPieceRead"/> does with it.
    ///     This is the single shared implementation behind both bounded-piece streaming use cases
    ///     in this codec: <see cref="StreamDiscardChunkPayload"/> calls this with a
    ///     <see langword="null"/> action to simply discard each piece for a recognized ancillary
    ///     chunk whose data this codec never needs, and <see cref="ReadChunkFrame"/>'s
    ///     <c>IDAT</c>-streaming case calls this with an action that writes each piece into the
    ///     accumulating <c>idatStream</c>, so that neither case ever allocates a single array
    ///     sized to the full declared chunk length - a declared length of, say, 500 MB never
    ///     forces anywhere near a 500 MB allocation for either case, only
    ///     <see cref="AncillaryChunkStreamBufferSize"/> at a time.
    /// </summary>
    /// <param name="stream">The stream to read the chunk's payload and trailing CRC-32 from.</param>
    /// <param name="typeBytes">
    ///     The chunk's already-read 4-byte type field, folded into the running CRC-32 before the
    ///     payload, matching the PNG specification's CRC-32 coverage (type and data, not length).
    /// </param>
    /// <param name="length">The chunk's declared data length, already validated to be within range.</param>
    /// <param name="onPieceRead">
    ///     Optional action invoked once per piece, immediately after that piece has been read and
    ///     folded into the running CRC-32, receiving the piece's bytes. Pass <see langword="null"/>
    ///     to simply discard each piece once its bytes have been folded into the CRC-32.
    /// </param>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when the stream ends before <paramref name="length"/> payload bytes and the
    ///     trailing 4-byte CRC-32 have been read, or the computed CRC-32 does not match the
    ///     trailing CRC-32 value read from the stream.
    /// </exception>
    private static void StreamChunkPayload(
        Stream stream,
        byte[] typeBytes,
        uint length,
        ChunkPayloadPieceHandler? onPieceRead)
    {
        var crc = UpdateCrc32(Crc32InitialState, typeBytes);

        // Read and fold in the declared payload in bounded pieces, handing each piece to
        // onPieceRead (or simply discarding it, when null) immediately, so peak allocation
        // never grows with the declared length
        var remaining = length;
        while (remaining > 0)
        {
            var sliceLength = (int)Math.Min(remaining, AncillaryChunkStreamBufferSize);
            var slice = ReadExactly(stream, sliceLength, "chunk data");
            crc = UpdateCrc32(crc, slice);
            onPieceRead?.Invoke(slice);
            remaining -= (uint)sliceLength;
        }

        var crcBytes = ReadExactly(stream, 4, "chunk CRC");
        var expectedCrc = ReadUInt32Be(crcBytes, 0);
        if (FinalizeCrc32(crc) != expectedCrc)
        {
            throw new InvalidDataException("Corrupt PNG chunk (CRC-32 mismatch).");
        }
    }

    /// <summary>
    ///     Validates the 8-byte PNG signature and reads exactly the first chunk frame, requiring
    ///     it to be <c>IHDR</c>, without reading any further chunks.
    /// </summary>
    /// <param name="stream">The stream to read the signature and IHDR chunk from.</param>
    /// <param name="enforceMaxDimension">
    ///     Forwarded to <see cref="ParseIhdr"/>; see its documentation.
    /// </param>
    /// <param name="validateDecodability">
    ///     Forwarded to <see cref="ParseIhdr"/>; see its documentation.
    /// </param>
    /// <returns>The parsed <c>IHDR</c> fields.</returns>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when the signature does not match, the first chunk is not <c>IHDR</c>, or the
    ///     <c>IHDR</c> chunk itself is malformed (see <see cref="ParseIhdr"/>).
    /// </exception>
    private static PngHeader ReadIhdrOnly(Stream stream, bool enforceMaxDimension, bool validateDecodability)
    {
        ValidateSignature(stream);

        var (_, data) = ReadIhdrChunkFrame(stream);
        var (width, height, colorType, bitDepth, interlaceMethod) = ParseIhdr(data, enforceMaxDimension, validateDecodability);
        return new PngHeader(width, height, colorType, bitDepth, interlaceMethod);
    }


    /// <summary>
    ///     Reads and CRC-validates the first chunk frame from <paramref name="stream"/>,
    ///     requiring it to be an <c>IHDR</c> chunk with the exact 13-byte length mandated by the
    ///     PNG specification. Unlike <see cref="ReadChunkFrame(Stream, Action{byte[], uint}, MemoryStream)"/>, the chunk type and
    ///     declared length are both validated <em>before</em> the (fixed-size) data payload is
    ///     allocated or read, so a crafted non-<c>IHDR</c> or wrong-length first chunk with a huge
    ///     declared length can never force a large allocation.
    /// </summary>
    /// <param name="stream">The stream to read the first chunk frame from.</param>
    /// <returns>The chunk's 4-byte type field (always <c>IHDR</c>) and its 13-byte data payload.</returns>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when the stream ends before the chunk length and type fields have been read,
    ///     the first chunk is not <c>IHDR</c>, the <c>IHDR</c> chunk's declared length is not
    ///     exactly 13, the stream ends before the full chunk frame has been read, or the chunk's
    ///     CRC-32 does not match its type and data.
    /// </exception>
    private static (byte[] TypeBytes, byte[] Data) ReadIhdrChunkFrame(Stream stream)
    {
        var lengthBytes = ReadExactly(stream, 4, "chunk length");
        var length = ReadUInt32Be(lengthBytes, 0);

        var typeBytes = ReadExactly(stream, 4, "chunk type");
        if (!ChunkTypeIs(typeBytes, "IHDR"))
        {
            throw new InvalidDataException("First PNG chunk is not IHDR.");
        }

        if (length != IhdrDataLength)
        {
            throw new InvalidDataException("IHDR chunk does not have the required length of 13 bytes.");
        }

        var data = ReadExactly(stream, IhdrDataLength, "chunk data");
        var crcBytes = ReadExactly(stream, 4, "chunk CRC");
        var expectedCrc = ReadUInt32Be(crcBytes, 0);

        // The CRC-32 covers the chunk type and data, but not the length field itself
        var crcInput = new byte[4 + data.Length];
        typeBytes.CopyTo(crcInput, 0);
        data.CopyTo(crcInput, 4);
        var actualCrc = ComputeCrc32(crcInput);
        if (actualCrc != expectedCrc)
        {
            throw new InvalidDataException("Corrupt PNG chunk (CRC-32 mismatch).");
        }

        return (typeBytes, data);
    }

    /// <summary>
    ///     Parses and validates a 13-byte <c>IHDR</c> chunk payload.
    /// </summary>
    /// <param name="data">The raw <c>IHDR</c> chunk data (must be exactly 13 bytes).</param>
    /// <param name="enforceMaxDimension">
    ///     When <see langword="true"/>, rejects a width or height above
    ///     <see cref="Surface.MaxDimension"/> with an <see cref="InvalidDataException"/>, as
    ///     <see cref="Load(Stream)"/> requires. When <see langword="false"/>, the raw
    ///     header-declared width and height are returned without comparison, as
    ///     <see cref="GetInfo(Stream)"/> requires. This flag gates only this one check.
    /// </param>
    /// <param name="validateDecodability">
    ///     When <see langword="true"/>, additionally rejects Adam7 interlacing (interlace method
    ///     1) with an <see cref="UnsupportedImageFeatureException"/>, as
    ///     <see cref="Load(Stream)"/> requires, since this codec does not implement Adam7
    ///     decoding. When <see langword="false"/>, an
    ///     Adam7-interlaced <c>IHDR</c> is accepted, as <see cref="GetInfo(Stream)"/> requires,
    ///     since Adam7 interlacing does not affect the declared width/height it reports. This
    ///     flag gates only this one check - every other check below (bit depth in range, color
    ///     type in range, their combination being legal per the PNG specification, compression/
    ///     filter method, interlace method in range) is a well-formedness check always enforced
    ///     regardless of this flag's value, since this codec's decodable color-type/bit-depth
    ///     space is now the PNG specification's entire legal space (see the type-level remarks).
    /// </param>
    /// <returns>The parsed image width, height, PNG color type byte, bit depth, and interlace method.</returns>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when <paramref name="data"/> is not 13 bytes, describes non-positive
    ///     dimensions (or, when <paramref name="enforceMaxDimension"/> is <see langword="true"/>,
    ///     oversized dimensions exceeding <see cref="Surface.MaxDimension"/>), describes a bit
    ///     depth or color type not defined by the PNG specification, describes a bit-depth/color-
    ///     type combination that is itself invalid per the specification (for example palette at
    ///     16-bit depth), describes an unsupported compression or filter method, or describes an
    ///     interlace method not defined by the specification.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when <paramref name="validateDecodability"/> is <see langword="true"/> and
    ///     <paramref name="data"/> declares Adam7 interlacing - a well-formed PNG feature that
    ///     <see cref="Load(Stream)"/> does not implement.
    /// </exception>
    private static (int Width, int Height, int ColorType, int BitDepth, int InterlaceMethod) ParseIhdr(
        byte[] data,
        bool enforceMaxDimension,
        bool validateDecodability)
    {
        if (data.Length != IhdrDataLength)
        {
            throw new InvalidDataException($"Invalid IHDR chunk length {data.Length}; expected 13 bytes.");
        }

        var width = (int)ReadUInt32Be(data, 0);
        var height = (int)ReadUInt32Be(data, 4);
        int bitDepth = data[8];
        int colorType = data[9];
        var compressionMethod = data[10];
        var filterMethod = data[11];
        var interlaceMethod = data[12];

        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException($"Invalid PNG dimensions {width}x{height}.");
        }

        // Reject dimensions above Surface.MaxDimension here, before ParseIhdr returns and before
        // any width/height arithmetic performed later in Load, so an oversized value surfaces as
        // the documented InvalidDataException rather than an ArgumentOutOfRangeException escaping
        // from deep inside Surface's constructor. Skipped entirely when enforceMaxDimension is
        // false, so GetInfo can report the raw header dimensions even when they exceed the bound.
        if (enforceMaxDimension && (width > Surface.MaxDimension || height > Surface.MaxDimension))
        {
            throw new InvalidDataException(
                $"PNG dimensions {width}x{height} exceed the maximum supported size of " +
                $"{Surface.MaxDimension}x{Surface.MaxDimension}.");
        }

        if (bitDepth is not (1 or 2 or 4 or 8 or 16))
        {
            throw new InvalidDataException(
                $"Invalid PNG bit depth {bitDepth}; only 1, 2, 4, 8, or 16 bits per sample are " +
                "defined by the PNG specification.");
        }

        if (colorType is not (ColorTypeGrayscale or ColorTypeTruecolor or ColorTypePalette
            or ColorTypeGrayscaleAlpha or ColorTypeTruecolorAlpha))
        {
            throw new InvalidDataException(
                $"Invalid PNG color type {colorType}; only grayscale (0), Truecolor (2), " +
                "palette (3), grayscale-with-alpha (4), and Truecolor-with-alpha (6) are " +
                "defined by the PNG specification.");
        }

        if (!IsValidBitDepthForColorType(colorType, bitDepth))
        {
            throw new InvalidDataException(
                $"Bit depth {bitDepth} is not valid for PNG color type {colorType} per the PNG " +
                "specification's color-type/bit-depth combination table.");
        }

        if (compressionMethod != CompressionMethodZlib)
        {
            throw new InvalidDataException(
                $"Unsupported PNG compression method {compressionMethod}; only zlib/DEFLATE (0) is supported.");
        }

        if (filterMethod != FilterMethodStandard)
        {
            throw new InvalidDataException(
                $"Unsupported PNG filter method {filterMethod}; only the standard adaptive filter method (0) is supported.");
        }

        if (interlaceMethod is not (InterlaceNone or InterlaceAdam7))
        {
            throw new InvalidDataException(
                $"Invalid PNG interlace method {interlaceMethod}; only 0 (none) and 1 (Adam7) " +
                "are defined by the PNG specification.");
        }

        if (validateDecodability && interlaceMethod == InterlaceAdam7)
        {
            throw new UnsupportedImageFeatureException(
                "png-adam7-interlace",
                "Adam7 interlacing is not supported by Load; use GetInfo to obtain this file's " +
                "declared dimensions without decoding its pixel data.");
        }

        return (width, height, colorType, bitDepth, interlaceMethod);
    }


    /// <summary>
    ///     Validates a 4-byte PNG chunk type field against the PNG specification's chunk-naming
    ///     rules, before <see cref="ProcessChunk"/> uses the first byte's case to classify an
    ///     otherwise-unrecognized chunk as critical or ancillary. Each of the four bytes must be
    ///     an ASCII letter (<c>A</c>-<c>Z</c> or <c>a</c>-<c>z</c>), and the third byte (the
    ///     specification's "reserved" bit position) must always be uppercase, since the
    ///     specification currently reserves a lowercase third byte for future definition. Without
    ///     this check, a malformed type such as <c>"a!cd"</c> (a non-letter byte) or <c>"aBcd"</c>
    ///     (lowercase reserved byte) would reach the ancillary/critical classification below and
    ///     be silently accepted as a spec-valid ancillary chunk, contrary to this codec's
    ///     spec-valid/malformed-input contract.
    /// </summary>
    /// <param name="typeBytes">The 4-byte chunk type field read from the stream.</param>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when any byte is not an ASCII letter, or the third byte is a lowercase letter.
    /// </exception>
    private static void ValidateChunkTypeCode(byte[] typeBytes)
    {
        for (var i = 0; i < 4; i++)
        {
            var b = typeBytes[i];
            if (b is not (>= (byte)'A' and <= (byte)'Z') and not (>= (byte)'a' and <= (byte)'z'))
            {
                throw new InvalidDataException(
                    $"Invalid PNG chunk type byte {i + 1} (0x{b:X2}); every chunk type byte must be an ASCII letter.");
            }
        }

        // The PNG specification's reserved-bit rule requires the third chunk-type byte to
        // always be uppercase; a lowercase third byte is reserved for future definition and is
        // never spec-valid for a chunk produced today, so this codec rejects it outright rather
        // than silently classifying it as ancillary (or critical) based on the first byte alone
        if (typeBytes[2] is >= (byte)'a' and <= (byte)'z')
        {
            var typeName = System.Text.Encoding.ASCII.GetString(typeBytes);
            throw new InvalidDataException(
                $"Invalid PNG chunk type '{typeName}'; the third byte must be uppercase (reserved-bit rule).");
        }
    }

    /// <summary>
    ///     Determines whether a 4-byte chunk type field matches the given ASCII chunk type name.
    /// </summary>
    /// <param name="typeBytes">The 4-byte chunk type field read from the stream.</param>
    /// <param name="type">The 4-character ASCII chunk type name to compare against.</param>
    /// <returns><see langword="true"/> if every byte matches; otherwise, <see langword="false"/>.</returns>
    private static bool ChunkTypeIs(byte[] typeBytes, string type)
    {
        for (var i = 0; i < 4; i++)
        {
            if (typeBytes[i] != (byte)type[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Writes a complete PNG chunk (length, type, data, and CRC-32) to a stream.
    /// </summary>
    /// <param name="stream">The stream to write the chunk to.</param>
    /// <param name="type">The 4-character ASCII chunk type name (for example <c>"IHDR"</c>).</param>
    /// <param name="data">The chunk's data payload; may be empty.</param>
    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var lengthBytes = new byte[4];
        WriteUInt32Be(lengthBytes, 0, (uint)data.Length);
        stream.Write(lengthBytes, 0, lengthBytes.Length);

        var typeBytes = new byte[4];
        for (var i = 0; i < 4; i++)
        {
            typeBytes[i] = (byte)type[i];
        }

        stream.Write(typeBytes, 0, typeBytes.Length);
        stream.Write(data, 0, data.Length);

        var crcInput = new byte[4 + data.Length];
        typeBytes.CopyTo(crcInput, 0);
        data.CopyTo(crcInput, 4);
        var crcBytes = new byte[4];
        WriteUInt32Be(crcBytes, 0, ComputeCrc32(crcInput));
        stream.Write(crcBytes, 0, crcBytes.Length);
    }
}
