using System.Diagnostics.CodeAnalysis;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Codecs;

public static partial class JpegCodec
{
    // ================================================================================================
    // Decoder
    // ================================================================================================

    private static partial class Decoder
    {
        private sealed class Component
        {
            public required int Id;
            public required int H;
            public required int V;
            public required int QuantSelector;
            public int DcSelector;
            public int AcSelector;
            public int DcPredictor;

            /// <summary>
            ///     One entry per block, in row-major order over the component's full MCU-grid
            ///     block dimensions (<see cref="BlocksPerLineMcu"/> x <see cref="BlocksPerColumnMcu"/>),
            ///     each holding 64 coefficients in zigzag scan order.
            /// </summary>
            public int[][]? Blocks;

            public int BlocksPerLineMcu;
            public int BlocksPerColumnMcu;
        }

        public static Surface Decode(byte[] file)
        {
            ValidateSoi(file);

            var state = new DecodeState();

            var pos = 2;
            var segmentCount = 0;
            while (true)
            {
                pos = SkipToMarker(file, pos);
                var marker = file[pos + 1];
                pos += 2;

                if (marker == MarkerEoi)
                {
                    break;
                }

                // Captured before ProcessSegment runs, since ProcessSegment sets state.SofSeen to
                // true while processing the SOF0/SOF2 segment itself - using the post-segment
                // value here would wrongly exempt the SOF segment's own bytes/segment-count from
                // both ceilings below, unlike GetInfo's ProbeDimensions (whose byte-based hard
                // limit is enforced unconditionally by the underlying buffer, including while
                // reading the SOF segment's own length field and payload; only its segment-count
                // ceiling excludes the terminating SOF0/SOF2 segment).
                var sofSeenBeforeSegment = state.SofSeen;

                // Shares GetInfo's MaxProbeSegmentCount ceiling on the number of non-terminating
                // marker segments preceding the SOF0/SOF2 marker, so Load and GetInfo genuinely
                // agree on this input shape too, not just the byte-based ceiling below: a stream
                // of many minimal-size segments before SOF is rejected consistently by both.
                // Counts exactly the marker kinds ProbeDimensions' CheckSegmentCount counts (stray
                // restart markers and the generic APPn/COM/etc. fallback), excluding not just the
                // terminating SOF0/SOF2 marker but also SOS-before-SOF and unsupported-SOF/frame
                // markers - both of which unconditionally throw their own, more specific
                // InvalidDataException in ProcessSegment/ProbeDimensions regardless of how many
                // segments preceded them - so Load and GetInfo never diverge on which exception
                // message a given byte sequence produces. Also mirrors ProbeDimensions'
                // CheckSegmentCount in being a no-op until the soft cap has already been crossed
                // (via pos, Load's equivalent of GetInfo's probe.Length), since this ceiling exists
                // purely to bound the post-soft-cap fallback scan's iteration count - a legitimate
                // file entirely under the soft cap with many small metadata segments must not be
                // rejected merely for that.
                if (!sofSeenBeforeSegment && pos > JpegCodec.MaxProbeHeaderBytes &&
                    CountsTowardSegmentLimit(marker) &&
                    ++segmentCount > JpegCodec.MaxProbeSegmentCount)
                {
                    throw BuildSegmentCountLimitException(JpegCodec.MaxProbeSegmentCount);
                }

                pos = ProcessSegment(file, pos, marker, state);

                // Shares GetInfo's MaxProbeHeaderBytesHardLimit ceiling on leading marker-segment
                // data preceding the SOF0/SOF2 marker, so Load and GetInfo genuinely agree on this
                // input shape rather than diverging: once this many bytes have been walked past
                // the SOI marker without finding SOF0/SOF2, Load throws the same InvalidDataException
                // GetInfo already throws for the same condition, instead of continuing to accept a
                // file GetInfo would reject. Gated on the pre-segment SofSeen value, so - exactly
                // as ProbeDimensions' byte-based ceiling does - this still applies while consuming
                // the SOF segment's own bytes (a well-formed file whose SOF segment itself
                // straddles this threshold is rejected by both GetInfo and Load, not silently
                // exempted), but never applies to the SOS/entropy-coded data that follows once the
                // SOF segment has been fully processed.
                if (!sofSeenBeforeSegment && pos > JpegCodec.MaxProbeHeaderBytesHardLimit)
                {
                    throw BuildHardLimitException(JpegCodec.MaxProbeHeaderBytesHardLimit);
                }
            }

            if (!state.SofSeen || !state.SosSeen || state.Components == null)
            {
                throw new InvalidDataException("JPEG stream is missing a mandatory SOF or SOS segment.");
            }

            return AssembleCanvas(state.Width, state.Height, state.Components, state.QuantTables);
        }

        /// <summary>
        ///     Determines whether <paramref name="marker"/> counts toward <see cref="Decode"/>'s
        ///     <see cref="JpegCodec.MaxProbeSegmentCount"/> ceiling, mirroring exactly which marker
        ///     kinds <see cref="ProbeDimensions"/>'s <c>CheckSegmentCount</c> counts: stray restart
        ///     markers and the generic APPn/COM/etc. fallback segment, but not SOS-before-SOF or an
        ///     unsupported SOF/frame marker - both of which throw their own, more specific
        ///     <see cref="InvalidDataException"/> unconditionally in <see cref="ProcessSegment"/>/
        ///     <see cref="ProbeDimensions"/> regardless of how many segments preceded them. Without
        ///     this exact alignment, a stream crafted so one of those specific markers lands exactly
        ///     on what would otherwise be the segment-count-exceeding segment could make
        ///     <see cref="Decode"/> throw the segment-count message while <see cref="ProbeDimensions"/>
        ///     throws its own more specific message for the same bytes - both still
        ///     <see cref="InvalidDataException"/>, but with diverging text, unlike every other
        ///     shared-ceiling case this file's exception-building helpers are designed to keep
        ///     identical between <see cref="JpegCodec.Load(Stream)"/> and
        ///     <see cref="JpegCodec.GetInfo(Stream)"/>.
        /// </summary>
        private static bool CountsTowardSegmentLimit(int marker) =>
            marker != MarkerSof0 && marker != MarkerSof2 && marker != MarkerSos &&
            marker is not (0xC1 or 0xC3 or 0xC5 or 0xC6 or 0xC7 or 0xC9 or 0xCA or 0xCB or
                0xCD or 0xCE or 0xCF or 0xC8 or 0xCC);

        /// <summary>
        ///     Validates that <paramref name="file"/> begins with the 2-byte SOI marker
        ///     (<c>0xFF 0xD8</c>), shared by <see cref="Decode"/> and <see cref="ProbeDimensions"/>.
        /// </summary>
        private static void ValidateSoi(byte[] file)
        {
            if (file.Length < 4 || file[0] != MarkerPrefix || file[1] != MarkerSoi)
            {
                throw new InvalidDataException("Not a JPEG file (missing SOI marker).");
            }
        }

        /// <summary>
        ///     A growable byte buffer backed by a <see cref="Stream"/>, used by
        ///     <see cref="ProbeDimensions(Stream)"/> to read only as many bytes as the marker scan
        ///     actually needs at any point, rather than eagerly reading a full fixed-size prefix
        ///     up front. Grows in small chunks, on demand, up to a <paramref name="softCap"/>;
        ///     once that soft cap is reached without satisfying a request, it keeps scanning past
        ///     it - still bounded to exactly what each request asks for, in larger chunks for
        ///     efficiency - rather than giving up outright. Because the caller (the
        ///     segment-parsing loop in <see cref="ProbeDimensions(Stream)"/>) only ever requests
        ///     as far as the next marker/segment boundary, this never reads ahead into
        ///     entropy-coded scan data, and never reads further than genuinely necessary to find
        ///     (or rule out) a SOF0/SOF2 marker - it does not unconditionally drain the stream to
        ///     end-of-stream. However, this soft-cap fallback scanning is itself bounded by a
        ///     separate, non-negotiable <paramref name="hardLimit"/>: a request for more than
        ///     <paramref name="hardLimit"/> total bytes is refused outright (see
        ///     <see cref="HardLimitExceeded"/>) rather than attempted, so a malformed,
        ///     adversarial, or effectively-infinite stream that never presents a SOF0/SOF2 marker
        ///     cannot make this buffer grow, or read from the stream, without bound.
        /// </summary>
        private sealed class IncrementalProbeBuffer(Stream stream, int softCap, int hardLimit)
        {
            /// <summary>The chunk size used for each incremental stream read up to the soft cap.</summary>
            private const int ChunkSize = 4096;

            /// <summary>
            ///     The chunk size used for reads past the soft cap, where fewer, larger reads are
            ///     more efficient since the soft cap has already proven insufficient.
            /// </summary>
            private const int BulkChunkSize = 81920;

            private byte[] _data = new byte[Math.Min(ChunkSize, softCap)];

            /// <summary>The number of bytes currently buffered.</summary>
            public int Length { get; private set; }

            /// <summary>
            ///     <see langword="true"/> once a request has been refused because it would have
            ///     required buffering more than <c>hardLimit</c> total bytes; used by callers to
            ///     select the appropriate <see cref="InvalidDataException"/> message.
            /// </summary>
            public bool HardLimitExceeded { get; private set; }

            public byte this[int index] => _data[index];

            /// <summary>
            ///     Ensures at least <paramref name="requiredLength"/> bytes are buffered, reading
            ///     further chunks from the stream only as needed. While <paramref name="requiredLength"/>
            ///     stays within the outer <c>softCap</c>, only that many bytes are ever read; once
            ///     the soft cap would otherwise be exceeded, reading continues past it - but only
            ///     as far as <paramref name="requiredLength"/>, never further - instead of giving
            ///     up (see <see cref="IncrementalProbeBuffer"/> remarks), so this method only
            ///     returns <see langword="false"/> once the stream has genuinely ended without
            ///     enough data to satisfy the request, or <paramref name="requiredLength"/>
            ///     exceeds the <c>hardLimit</c> ceiling (see <see cref="HardLimitExceeded"/>).
            /// </summary>
            /// <returns>
            ///     <see langword="true"/> if at least <paramref name="requiredLength"/> bytes are
            ///     now buffered; <see langword="false"/> if the stream ended before that many
            ///     bytes could be read (even after reading past the soft cap), or if
            ///     <paramref name="requiredLength"/> exceeds the hard limit and was refused
            ///     without attempting to read it.
            /// </returns>
            public bool TryEnsureLength(int requiredLength)
            {
                if (requiredLength <= Length)
                {
                    return true;
                }

                // The hard limit is a genuine ceiling: refuse outright, without reading anything
                // further, rather than letting the soft-cap fallback below grow unboundedly for a
                // malformed/adversarial/effectively-infinite stream that never yields a SOF0/SOF2
                // marker.
                if (requiredLength > hardLimit)
                {
                    HardLimitExceeded = true;
                    return false;
                }

                var target = Math.Min(requiredLength, softCap);
                while (Length < target)
                {
                    // Bounded by the smaller of the chunk size and how many bytes are actually
                    // still needed to satisfy target (which is itself at most requiredLength).
                    // Bounding only by softCap - Length here would read a full ChunkSize chunk
                    // even when only a few bytes are needed to reach the next marker or the SOF
                    // segment boundary, over-reading past requiredLength into whatever data
                    // follows - including entropy-coded scan data - since stream.Read always
                    // consumes what it's asked for from the underlying stream regardless of how
                    // much of it the caller actually needed.
                    var chunk = Math.Min(ChunkSize, target - Length);
                    EnsureCapacity(Length + chunk);

                    var read = stream.Read(_data, Length, chunk);
                    if (read == 0)
                    {
                        return false;
                    }

                    Length += read;
                }

                // The soft cap was reached before requiredLength was satisfied - keep reading,
                // in larger chunks for efficiency, but strictly bounded to requiredLength (which
                // is itself now known to be within the hard limit) rather than draining the
                // stream to end-of-stream. Because ProbeDimensions only ever asks for as far as
                // the next segment boundary, this stops the instant a SOF0/SOF2 marker is found
                // and never reads into entropy-coded scan data.
                while (Length < requiredLength)
                {
                    var chunk = Math.Min(BulkChunkSize, requiredLength - Length);
                    EnsureCapacity(Length + chunk);

                    var read = stream.Read(_data, Length, chunk);
                    if (read == 0)
                    {
                        return false;
                    }

                    Length += read;
                }

                return true;
            }

            /// <summary>
            ///     Grows the backing array to at least <paramref name="requiredCapacity"/> bytes,
            ///     using geometric (doubling) growth rather than resizing to exactly
            ///     <paramref name="requiredCapacity"/> each call. The post-soft-cap fallback scan
            ///     grows the buffer in many small, fixed-size (<see cref="BulkChunkSize"/>) steps
            ///     while working toward <c>hardLimit</c> (up to 16 MiB); resizing to the exact
            ///     capacity needed on every such step would copy the entire buffer roughly
            ///     <c>hardLimit / BulkChunkSize</c> times (around 200 full-array copies), which is
            ///     quadratic in the amount of data read. Doubling capacity instead makes the total
            ///     copying work amortized linear in the final buffer size, capped at
            ///     <c>hardLimit</c> since the buffer is never grown beyond what
            ///     <see cref="TryEnsureLength"/> has already confirmed is within the hard limit.
            /// </summary>
            private void EnsureCapacity(int requiredCapacity)
            {
                if (_data.Length < requiredCapacity)
                {
                    var newCapacity = Math.Min(Math.Max(requiredCapacity, _data.Length * 2), hardLimit);
                    Array.Resize(ref _data, newCapacity);
                }
            }

            /// <summary>
            ///     Ensures at least <paramref name="length"/> bytes are buffered and returns a new
            ///     array containing exactly those bytes, for handing off to a method (such as
            ///     <see cref="ReadSof"/>) that expects a plain, exactly-sized <c>byte[]</c>.
            /// </summary>
            public byte[] ToExactArray(int length)
            {
                if (!TryEnsureLength(length))
                {
                    throw HardLimitExceeded
                        ? BuildHardLimitException(hardLimit)
                        : new InvalidDataException(
                            "Unexpected end of stream while reading a JPEG segment during header probing.");
                }

                return _data.AsSpan(0, length).ToArray();
            }
        }

        /// <summary>
        ///     Builds the <see cref="InvalidDataException"/> thrown when
        ///     <see cref="ProbeDimensions(Stream)"/>'s post-soft-cap fallback scan reaches the
        ///     <see cref="MaxProbeHeaderBytesHardLimit"/> hard ceiling without ever finding a
        ///     SOF0/SOF2 marker, shared by both <see cref="IncrementalProbeBuffer.ToExactArray"/>
        ///     and <see cref="ProbeExhaustedException"/> so the two report the same message shape
        ///     for the same underlying condition.
        /// </summary>
        private static InvalidDataException BuildHardLimitException(int hardLimit) =>
            new($"JPEG SOF0/SOF2 marker not found within the {hardLimit}-byte header probe hard limit.");

        /// <summary>
        ///     Builds the <see cref="InvalidDataException"/> thrown when
        ///     <see cref="ProbeDimensions(Stream)"/>'s post-soft-cap fallback scan reaches the
        ///     <see cref="JpegCodec.MaxProbeSegmentCount"/> segment-count ceiling without ever
        ///     finding a SOF0/SOF2 marker - independent of, and typically reached far sooner than,
        ///     the byte-based <see cref="JpegCodec.MaxProbeHeaderBytesHardLimit"/> for a malformed
        ///     stream composed of many minimal-size (4-byte) segments.
        /// </summary>
        private static InvalidDataException BuildSegmentCountLimitException(int maxSegmentCount) =>
            new($"JPEG SOF0/SOF2 marker not found within the {maxSegmentCount}-segment header probe limit.");

        /// <summary>
        ///     Scans a JPEG stream's leading marker segments, reading incrementally and only as
        ///     far as necessary, looking for the first SOF0/SOF2 marker, and returns its declared
        ///     dimensions and component count without ever reaching <c>SOS</c>/entropy-coded scan
        ///     data. In the common case where the SOF segment appears near the start of the
        ///     stream (as it does for essentially all real-world JPEG files), reads only as far
        ///     as the end of that segment - never a full fixed-size prefix regardless of where
        ///     the SOF marker actually is. If more than <see cref="JpegCodec.MaxProbeHeaderBytes"/>
        ///     bytes of leading marker-segment data are present, continues scanning segment
        ///     headers past that soft cap - one marker segment at a time, exactly as below the
        ///     cap - until a SOF0/SOF2 marker is found or the stream genuinely ends, so this never
        ///     gives up merely because the soft cap was reached while more data remained, and
        ///     never reads into entropy-coded scan data even for files with unusually large
        ///     leading metadata. That post-soft-cap scanning is bounded by two independent
        ///     ceilings, either of which triggers a throw once reached without a SOF0/SOF2 marker
        ///     ever being found: the <see cref="JpegCodec.MaxProbeHeaderBytesHardLimit"/> hard
        ///     ceiling on total bytes read, and the <see cref="JpegCodec.MaxProbeSegmentCount"/>
        ///     hard ceiling on the number of marker segments scanned. The byte ceiling alone does
        ///     not cheaply bound the number of loop iterations, since a marker segment can be as
        ///     small as 4 bytes; the segment-count ceiling exists specifically to bound iterations
        ///     directly for a malformed or adversarial stream composed of many minimal-size
        ///     segments, which would otherwise take millions of iterations to reach the byte
        ///     ceiling. Either way, a malformed, adversarial, or effectively-infinite stream that
        ///     never presents a SOF0/SOF2 marker causes this method to throw once whichever
        ///     ceiling is reached first, rather than reading or buffering data, or looping,
        ///     without bound.
        /// </summary>
        /// <param name="stream">
        ///     The stream to read JPEG marker segments from, starting at its current position.
        /// </param>
        /// <exception cref="System.IO.InvalidDataException">
        ///     Thrown when the SOI marker is missing, an SOS marker or end-of-image is reached
        ///     before any SOF0/SOF2 marker is found, an unsupported SOF/frame marker (for example
        ///     SOF1/SOF3) is encountered, the stream genuinely ends (even after continuing to scan
        ///     past the <see cref="JpegCodec.MaxProbeHeaderBytes"/> soft cap) without a SOF0/SOF2
        ///     marker ever being found, the <see cref="JpegCodec.MaxProbeHeaderBytesHardLimit"/>
        ///     hard ceiling is reached without a SOF0/SOF2 marker ever being found, or the
        ///     <see cref="JpegCodec.MaxProbeSegmentCount"/> segment-count ceiling is reached
        ///     without a SOF0/SOF2 marker ever being found.
        /// </exception>
        public static ImageInfo ProbeDimensions(Stream stream)
        {
            var probe = new IncrementalProbeBuffer(
                stream,
                JpegCodec.MaxProbeHeaderBytes,
                JpegCodec.MaxProbeHeaderBytesHardLimit);

            if (!probe.TryEnsureLength(4) || probe[0] != MarkerPrefix || probe[1] != MarkerSoi)
            {
                throw new InvalidDataException("Not a JPEG file (missing SOI marker).");
            }

            var pos = 2;
            var segmentCount = 0;

            // Defense-in-depth, independent of the byte-based hard limit above: bounds the
            // number of non-terminating marker segments scanned directly, since a segment can be
            // as small as 4 bytes and a malformed/adversarial stream of many such minimal
            // segments could otherwise take millions of iterations to reach
            // MaxProbeHeaderBytesHardLimit. Called only for segments where scanning continues
            // (stray restart markers and the general/APPn/COM/etc. fallback path below), never
            // for the terminating SOF0/SOF2 marker itself, so a legitimate file whose SOF0/SOF2
            // marker happens to be the segment that would otherwise exceed the ceiling still
            // probes successfully, exactly as Load would decode it. A no-op until the soft cap
            // has already been crossed, since this ceiling exists purely to bound the post-soft-cap
            // fallback scan's iteration count - a legitimate file entirely under the soft cap that
            // happens to have many small metadata segments must not be rejected merely for that.
            void CheckSegmentCount()
            {
                if (probe.Length <= JpegCodec.MaxProbeHeaderBytes)
                {
                    return;
                }

                if (++segmentCount > JpegCodec.MaxProbeSegmentCount)
                {
                    throw BuildSegmentCountLimitException(JpegCodec.MaxProbeSegmentCount);
                }
            }

            while (true)
            {
                if (!probe.TryEnsureLength(pos + 2))
                {
                    throw ProbeExhaustedException(probe);
                }

                // Skip any fill bytes (0xFF) before the marker code.
                while (probe[pos] != MarkerPrefix)
                {
                    pos++;
                    if (!probe.TryEnsureLength(pos + 2))
                    {
                        throw ProbeExhaustedException(probe);
                    }
                }

                while (probe.TryEnsureLength(pos + 2) && probe[pos + 1] == MarkerPrefix)
                {
                    pos++;
                }

                if (!probe.TryEnsureLength(pos + 2))
                {
                    throw ProbeExhaustedException(probe);
                }

                var marker = probe[pos + 1];
                pos += 2;

                switch (marker)
                {
                    case MarkerSof0:
                    case MarkerSof2:
                        {
                            if (!probe.TryEnsureLength(pos + 2))
                            {
                                throw ProbeExhaustedException(probe);
                            }

                            var segmentLength = (ushort)((probe[pos] << 8) | probe[pos + 1]);
                            var sofBuffer = probe.ToExactArray(pos + segmentLength);
                            ReadSof(sofBuffer, pos, enforceMaxDimension: false, out var width, out var height, out var components);
                            return new ImageInfo(width, height, components.Length, HasAlpha: false);
                        }

                    case MarkerSos:
                        throw new InvalidDataException(
                            "JPEG SOS marker encountered before a SOF0/SOF2 marker was found.");

                    case MarkerEoi:
                        throw new InvalidDataException(
                            "JPEG end-of-image marker reached before a SOF0/SOF2 marker was found.");

                    case >= MarkerRst0 and <= MarkerRst7:
                        // Stray restart marker outside entropy-coded data; ignore. Counts toward
                        // the segment-count ceiling below, since scanning continues past it - a
                        // stream of many stray restart markers is just as cheap an iteration-count
                        // attack shape as many minimal APPn segments.
                        CheckSegmentCount();
                        break;

                    case 0xC1 or 0xC3 or 0xC5 or 0xC6 or 0xC7 or 0xC9 or 0xCA or 0xCB or
                         0xCD or 0xCE or 0xCF or 0xC8 or 0xCC:
                        // Same unsupported-SOF/frame-marker rejection Decode/ProcessSegment
                        // applies, so GetInfo can never "probe successfully" a file Load would
                        // unconditionally reject at this exact marker.
                        ThrowUnsupportedSofOrFrameMarker(marker);
                        break;

                    default:
                        CheckSegmentCount();

                        if (!probe.TryEnsureLength(pos + 2))
                        {
                            throw ProbeExhaustedException(probe);
                        }

                        pos += (ushort)((probe[pos] << 8) | probe[pos + 1]);
                        break;
                }
            }
        }

        /// <summary>
        ///     Builds the <see cref="InvalidDataException"/> for <see cref="ProbeDimensions(Stream)"/>
        ///     running out of readable data before a SOF0/SOF2 marker was found. Now that
        ///     <see cref="IncrementalProbeBuffer"/> keeps scanning segment headers past its soft
        ///     cap rather than giving up at it, this is thrown either when the stream has
        ///     genuinely ended (truncated or non-JPEG data) - never merely because
        ///     <see cref="JpegCodec.MaxProbeHeaderBytes"/> was reached - or when the
        ///     post-soft-cap scan reaches the <see cref="JpegCodec.MaxProbeHeaderBytesHardLimit"/>
        ///     hard ceiling without a SOF0/SOF2 marker ever being found, distinguished via
        ///     <see cref="IncrementalProbeBuffer.HardLimitExceeded"/>.
        /// </summary>
        private static InvalidDataException ProbeExhaustedException(IncrementalProbeBuffer probe) =>
            probe.HardLimitExceeded
                ? BuildHardLimitException(JpegCodec.MaxProbeHeaderBytesHardLimit)
                : new InvalidDataException(
                    "Stream ended before a JPEG SOF0/SOF2 marker was found (truncated or non-JPEG data).");

        /// <summary>
        ///     Mutable state threaded through <see cref="ProcessSegment"/> while <see cref="Decode"/>
        ///     walks the marker segments of a JPEG stream, accumulating the quantization/Huffman
        ///     tables and frame parameters needed once the SOS-terminated scan is fully decoded.
        /// </summary>
        private sealed class DecodeState
        {
            /// <summary>Quantization tables keyed by table selector, populated by DQT segments.</summary>
            public Dictionary<int, int[]> QuantTables { get; } = [];

            /// <summary>DC Huffman tables keyed by table selector, populated by DHT segments.</summary>
            public Dictionary<int, HuffmanTable> DcTables { get; } = [];

            /// <summary>AC Huffman tables keyed by table selector, populated by DHT segments.</summary>
            public Dictionary<int, HuffmanTable> AcTables { get; } = [];

            /// <summary>The frame's component descriptors, populated by the SOF segment.</summary>
            public Component[]? Components { get; set; }

            /// <summary>The frame width in pixels, populated by the SOF segment.</summary>
            public int Width { get; set; }

            /// <summary>The frame height in pixels, populated by the SOF segment.</summary>
            public int Height { get; set; }

            /// <summary>Whether the frame uses progressive (SOF2) rather than baseline (SOF0) encoding.</summary>
            public bool Progressive { get; set; }

            /// <summary>The restart interval in MCUs, populated by a DRI segment (0 if none seen).</summary>
            public int RestartInterval { get; set; }

            /// <summary>Whether a SOF segment has been seen yet.</summary>
            public bool SofSeen { get; set; }

            /// <summary>Whether a SOS segment has been seen yet.</summary>
            public bool SosSeen { get; set; }
        }

        /// <summary>
        ///     Processes a single marker segment encountered by <see cref="Decode"/> (DQT, DHT, DRI,
        ///     SOF0/SOF2, SOS, a stray restart marker, an unsupported SOF variant, or a generically
        ///     skipped length-prefixed segment such as APPn/COM), updating <paramref name="state"/>
        ///     in place and returning the stream position immediately following the segment.
        /// </summary>
        private static int ProcessSegment(byte[] file, int pos, int marker, DecodeState state)
        {
            switch (marker)
            {
                case MarkerDqt:
                    return ReadDqt(file, pos, state.QuantTables);

                case MarkerDht:
                    return ReadDht(file, pos, state.DcTables, state.AcTables);

                case MarkerDri:
                    var driLength = ReadUInt16Be(file, pos);
                    state.RestartInterval = ReadUInt16Be(file, pos + 2);
                    return pos + driLength;

                case MarkerSof0:
                case MarkerSof2:
                    if (state.SofSeen)
                    {
                        throw new InvalidDataException("Multiple SOF markers are not supported.");
                    }

                    state.Progressive = marker == MarkerSof2;
                    var sofPos = ReadSof(file, pos, enforceMaxDimension: true, out var width, out var height, out var components);
                    state.Width = width;
                    state.Height = height;
                    state.Components = components;
                    state.SofSeen = true;
                    return sofPos;

                case MarkerSos:
                    if (!state.SofSeen)
                    {
                        throw new InvalidDataException("SOS marker encountered before any SOF marker.");
                    }

                    var scanContext = new ScanDecodeContext(
                        state.Components!, state.DcTables, state.AcTables, state.Progressive, state.RestartInterval);
                    var sosPos = DecodeScan(file, pos, state.Width, state.Height, scanContext);
                    state.SosSeen = true;
                    return sosPos;

                case >= 0xD0 and <= 0xD7:
                    // Stray restart marker outside entropy-coded data; ignore.
                    return pos;

                case 0xC1 or 0xC3 or 0xC5 or 0xC6 or 0xC7 or 0xC9 or 0xCA or 0xCB or
                     0xCD or 0xCE or 0xCF or 0xC8 or 0xCC:
                    ThrowUnsupportedSofOrFrameMarker(marker);
                    return pos;

                default:
                    // APPn, COM, and any other length-prefixed segment we do not act on: skip.
                    return SkipLengthPrefixedSegment(file, pos);
            }
        }

        /// <summary>
        ///     Rejects a JPEG SOF or frame marker that this decoder does not support (an SOF
        ///     variant other than baseline (SOF0) or progressive (SOF2), or an arithmetic-coded/
        ///     JPG-extension marker), throwing the same <see cref="InvalidDataException"/> message
        ///     for a given marker value regardless of caller. Shared by <see cref="ProcessSegment"/>
        ///     (used by <see cref="Decode"/>/<see cref="JpegCodec.Load(Stream)"/>) and
        ///     <see cref="ProbeDimensions(Stream)"/> (used by <see cref="JpegCodec.GetInfo(Stream)"/>)
        ///     so the two can never diverge on which marker values are rejected, unlike before this
        ///     helper existed, when <see cref="ProbeDimensions(Stream)"/> silently skipped every one
        ///     of these markers as if it were a generic, harmless segment.
        /// </summary>
        /// <param name="marker">The unsupported marker value; must be one of the values this method rejects.</param>
        [DoesNotReturn]
        private static void ThrowUnsupportedSofOrFrameMarker(int marker)
        {
            if (marker is 0xC8 or 0xCC)
            {
                throw new InvalidDataException("Arithmetic-coded and JPG-extension JPEG variants are not supported.");
            }

            throw new InvalidDataException(
                $"Unsupported JPEG SOF marker 0x{marker:X2}; only baseline (SOF0) and progressive (SOF2) are supported.");
        }

        /// <summary>
        ///     Skips a generic length-prefixed marker segment (its 2-byte length field, read
        ///     big-endian, includes itself), returning the position immediately following the
        ///     segment. Correct for every JPEG marker segment other than SOI/EOI/RSTn/TEM, since
        ///     all of those are universally length-prefixed immediately after the marker code -
        ///     this is used by <see cref="ProcessSegment"/>'s generic default case (for APPn/COM
        ///     and any other segment this decoder does not specially parse).
        /// </summary>
        private static int SkipLengthPrefixedSegment(byte[] file, int pos) => pos + ReadUInt16Be(file, pos);

        private static int SkipToMarker(byte[] file, int pos)
        {
            while (pos < file.Length && file[pos] != MarkerPrefix)
            {
                pos++;
            }

            // Skip any run of fill bytes (multiple consecutive 0xFF) before the actual marker code.
            while (pos + 1 < file.Length && file[pos + 1] == MarkerPrefix)
            {
                pos++;
            }

            if (pos + 1 >= file.Length)
            {
                throw new InvalidDataException("Unexpected end of stream while looking for a JPEG marker.");
            }

            return pos;
        }

        private static ushort ReadUInt16Be(byte[] file, int offset)
        {
            if (offset + 1 >= file.Length)
            {
                throw new InvalidDataException("Unexpected end of stream while reading a JPEG segment length.");
            }

            return (ushort)((file[offset] << 8) | file[offset + 1]);
        }

        /// <summary>
        /// Reads a single byte from the file at the given position, throwing
        /// <see cref="InvalidDataException"/> (rather than an unhandled
        /// <see cref="IndexOutOfRangeException"/>) if the position is outside the bounds
        /// of the file. This is used to bounds-check segment payload parsing where a
        /// corrupt or truncated segment length would otherwise cause an unguarded array read.
        /// </summary>
        /// <param name="file">File bytes.</param>
        /// <param name="pos">Position to read from.</param>
        /// <returns>Byte at the given position.</returns>
        /// <exception cref="InvalidDataException">Position is outside the bounds of the file.</exception>
        private static byte ReadByte(byte[] file, int pos)
        {
            if (pos < 0 || pos >= file.Length)
            {
                throw new InvalidDataException("Unexpected end of stream while reading a JPEG segment payload.");
            }

            return file[pos];
        }

        private static int ReadDqt(byte[] file, int pos, Dictionary<int, int[]> quantTables)
        {
            var length = ReadUInt16Be(file, pos);
            var end = pos + length;
            var p = pos + 2;
            while (p < end)
            {
                var precisionAndId = ReadByte(file, p++);
                var precision = precisionAndId >> 4;
                var id = precisionAndId & 0xF;
                var table = new int[64];
                for (var i = 0; i < 64; i++)
                {
                    if (precision == 0)
                    {
                        table[i] = ReadByte(file, p++);
                    }
                    else
                    {
                        table[i] = (ReadByte(file, p) << 8) | ReadByte(file, p + 1);
                        p += 2;
                    }
                }

                quantTables[id] = table;
            }

            return end;
        }

        private static int ReadDht(
            byte[] file, int pos, Dictionary<int, HuffmanTable> dcTables, Dictionary<int, HuffmanTable> acTables)
        {
            var length = ReadUInt16Be(file, pos);
            var end = pos + length;
            var p = pos + 2;
            while (p < end)
            {
                var classAndId = ReadByte(file, p++);
                var tableClass = classAndId >> 4;
                var id = classAndId & 0xF;
                var bits = new byte[16];
                var totalSymbols = 0;
                for (var i = 0; i < 16; i++)
                {
                    bits[i] = ReadByte(file, p++);
                    totalSymbols += bits[i];
                }

                if (p + totalSymbols > file.Length)
                {
                    throw new InvalidDataException("Unexpected end of stream while reading a JPEG DHT segment payload.");
                }

                var values = new byte[totalSymbols];
                Array.Copy(file, p, values, 0, totalSymbols);
                p += totalSymbols;

                var table = HuffmanTable.Build(bits, values);
                if (tableClass == 0)
                {
                    dcTables[id] = table;
                }
                else
                {
                    acTables[id] = table;
                }
            }

            return end;
        }

        private static int ReadSof(
            byte[] file,
            int pos,
            bool enforceMaxDimension,
            out int width,
            out int height,
            out Component[] components)
        {
            var length = ReadUInt16Be(file, pos);
            var end = pos + length;
            var p = pos + 2;

            var precision = ReadByte(file, p++);
            if (precision != 8)
            {
                throw new InvalidDataException($"Unsupported JPEG sample precision {precision}; only 8-bit is supported.");
            }

            height = (ReadByte(file, p) << 8) | ReadByte(file, p + 1);
            p += 2;
            width = (ReadByte(file, p) << 8) | ReadByte(file, p + 1);
            p += 2;

            if (width <= 0 || height <= 0)
            {
                throw new InvalidDataException($"Invalid JPEG dimensions {width}x{height}.");
            }

            // Reject dimensions above Surface.MaxDimension here, before ReadSof returns and
            // before any width/height arithmetic (e.g. MCU-grid block sizing performed while
            // decoding the SOS-terminated scan, well before AssembleCanvas constructs the
            // Surface) is performed, so an oversized value surfaces as the documented
            // InvalidDataException rather than an ArgumentOutOfRangeException escaping from
            // deep inside Surface's constructor. Skipped entirely when enforceMaxDimension is
            // false, so GetInfo can report the raw header dimensions even when they exceed the
            // bound.
            if (enforceMaxDimension && (width > Surface.MaxDimension || height > Surface.MaxDimension))
            {
                throw new InvalidDataException(
                    $"JPEG dimensions {width}x{height} exceed the maximum supported size of " +
                    $"{Surface.MaxDimension}x{Surface.MaxDimension}.");
            }

            var numComponents = ReadByte(file, p++);
            if (numComponents != 1 && numComponents != 3)
            {
                throw new InvalidDataException(
                    $"Unsupported JPEG component count {numComponents}; only 1 (grayscale) and 3 (YCbCr) are supported. " +
                    "4-component (CMYK/YCCK) JPEG images are not supported.");
            }

            components = new Component[numComponents];
            for (var i = 0; i < numComponents; i++)
            {
                var id = ReadByte(file, p++);
                var samplingFactors = ReadByte(file, p++);
                var h = samplingFactors >> 4;
                var v = samplingFactors & 0xF;
                var quantSelector = ReadByte(file, p++);
                if (h is not (1 or 2) || v is not (1 or 2))
                {
                    throw new InvalidDataException(
                        $"Unsupported JPEG sampling factors {h}x{v} for component {id}; only 1 and 2 are supported.");
                }

                components[i] = new Component { Id = id, H = h, V = v, QuantSelector = quantSelector };
            }

            if (p != end)
            {
                throw new InvalidDataException("Malformed JPEG SOF segment length.");
            }

            return end;
        }
    }
}
