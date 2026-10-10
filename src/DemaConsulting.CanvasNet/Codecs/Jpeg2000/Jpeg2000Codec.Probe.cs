namespace DemaConsulting.CanvasNet.Codecs;

public static partial class Jpeg2000Codec
{
    // ================================================================================================
    // Incremental header probing for GetInfo
    // ================================================================================================

    /// <summary>The largest SOC + SIZ prefix: 2 (SOC) + 2 (marker) + the 16-bit length field value (at most 65535).</summary>
    private const int MaxSizPrefix = 4 + 65535;

    /// <summary>The chunk size used when discarding the content of a skipped box on a non-seekable stream.</summary>
    private const int DiscardChunk = 8192;

    /// <summary>
    ///     The most bytes read for one <c>jp2h</c> child box. The largest valid child is a palette
    ///     (3 + 255 + 1024 x 255 x 2 = about 510 KiB), so no valid file exceeds this; a larger palette, component
    ///     mapping or channel definition box is malformed, and a larger color specification box is only read in part.
    /// </summary>
    private const int MaxProbeChildBytes = 1024 * 1024;

    /// <summary>The leading bytes of a <c>colr</c> box that are read: the method, two reserved bytes, and the start of the ICC profile.</summary>
    private const int ProbeColrBytes = 256;

    /// <summary>
    ///     A forward-only view of the stream that counts the bytes consumed and enforces the input-size cap
    ///     (<see cref="Jpeg2000DecoderLimits.MaxInputBytes"/>) without ever buffering more than the caller asks for.
    /// </summary>
    private sealed class ProbeReader
    {
        private readonly Stream _stream;
        private readonly long _cap;

        /// <summary>Initializes a new instance of the <see cref="ProbeReader"/> class.</summary>
        /// <param name="stream">The stream; reading starts at its current position.</param>
        /// <param name="cap">The maximum number of bytes the reader may consume.</param>
        public ProbeReader(Stream stream, long cap)
        {
            _stream = stream;
            _cap = cap;

            // A seekable stream reveals its remaining length up front, so oversized input and boxes that
            // overrun the data are rejected exactly like the whole-buffer path would.
            Remaining = stream.CanSeek ? Math.Max(0, stream.Length - stream.Position) : -1;
            if (Remaining > cap)
            {
                throw Malformed("input is too large.");
            }
        }

        /// <summary>Gets the number of bytes consumed so far.</summary>
        public long Consumed { get; private set; }

        /// <summary>Gets the number of bytes left, or -1 when the stream cannot report it.</summary>
        public long Remaining { get; private set; }

        /// <summary>Gets the number of bytes that may still be consumed under the cap.</summary>
        public long CapLeft => _cap - Consumed;

        /// <summary>Reads up to <paramref name="count"/> bytes, fewer only at the end of the stream.</summary>
        /// <param name="count">The number of bytes wanted.</param>
        /// <returns>The bytes read.</returns>
        public byte[] Read(int count)
        {
            // Grow in chunks so a hostile declared length cannot force a large allocation before any byte arrives.
            using var memory = new MemoryStream();
            var chunk = new byte[Math.Min(count, DiscardChunk)];
            var got = 0;
            while (got < count)
            {
                var n = _stream.Read(chunk, 0, Math.Min(chunk.Length, count - got));
                if (n <= 0)
                {
                    break;
                }

                memory.Write(chunk, 0, n);
                got += n;
            }

            Account(got);
            return memory.ToArray();
        }

        /// <summary>Skips <paramref name="count"/> bytes.</summary>
        /// <param name="count">The number of bytes to skip.</param>
        /// <returns><see langword="true"/> when all bytes were skipped; <see langword="false"/> when the stream ended first.</returns>
        public bool Skip(long count)
        {
            if (count > CapLeft)
            {
                throw Malformed("input is too large.");
            }

            if (_stream.CanSeek)
            {
                if (count > Remaining)
                {
                    return false;
                }

                _stream.Seek(count, SeekOrigin.Current);
                Account(count);
                return true;
            }

            var chunk = new byte[DiscardChunk];
            var left = count;
            while (left > 0)
            {
                var n = _stream.Read(chunk, 0, (int)Math.Min(left, chunk.Length));
                if (n <= 0)
                {
                    Account(count - left);
                    return false;
                }

                left -= n;
            }

            Account(count);
            return true;
        }

        private void Account(long n)
        {
            Consumed += n;
            if (Remaining >= 0)
            {
                Remaining -= n;
            }

            if (Consumed > _cap)
            {
                throw Malformed("input is too large.");
            }
        }
    }

    /// <summary>
    ///     Reads only the part of a raw codestream or JP2 file that <see cref="GetInfo(Stream)"/> needs: the JP2
    ///     boxes up to the codestream box, plus the SOC and SIZ marker segments. The existing box and marker parsers
    ///     run on the bytes read.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="limits">The limits supplying the input-size cap.</param>
    /// <param name="siz">Receives the validated SIZ information.</param>
    /// <returns>The container information (an empty one for a raw codestream).</returns>
    private static Jp2Info ProbeHeaders(Stream stream, Jpeg2000DecoderLimits limits, out SizInfo siz)
    {
        var reader = new ProbeReader(stream, limits.MaxInputBytes);
        var first = reader.Read(12);
        if (first.Length >= 2 && first[0] == 0xFF && first[1] == 0x4F)
        {
            // Raw codestream: the SOC and SIZ segments are the whole prefix; a shorter stream is passed as is.
            var prefix = first;
            if (first.Length == 12)
            {
                var rest = reader.Read(MaxSizPrefix - 12);
                prefix = new byte[12 + rest.Length];
                first.CopyTo(prefix, 0);
                rest.CopyTo(prefix, 12);
            }

            siz = Codestream.ParseSizOnly(prefix, 0, prefix.Length, out _);
            return new Jp2Info { CodestreamStart = 0, CodestreamEnd = prefix.Length };
        }

        // The signature checks of ParseContainer.
        if (first.Length < 12 || ReadBe32(first, 0) != 12 || ReadBe32(first, 4) != BoxJp2Signature)
        {
            throw Malformed("data is neither a JPEG 2000 codestream nor a JP2 file.");
        }

        if (ReadBe32(first, 8) != 0x0D0A870A)
        {
            throw Malformed("invalid JP2 signature box content.");
        }

        var info = new Jp2Info();
        for (var count = 0; count < MaxBoxes; count++)
        {
            var header = ReadProbeBoxHeader(reader, out var toEnd, out var atEnd);
            if (atEnd)
            {
                break;
            }

            var contentLength = header.End - header.ContentStart;
            if (header.Type == BoxCodestream)
            {
                // Only the SOC and SIZ segments are needed, whatever the length of the codestream.
                var take = Math.Min(MaxSizPrefix, toEnd ? int.MaxValue : contentLength);
                var prefix = reader.Read(take);
                info.CodestreamStart = 0;
                info.CodestreamEnd = prefix.Length;
                siz = Codestream.ParseSizOnly(prefix, 0, prefix.Length, out _);
                return info;
            }

            if (header.Type == BoxJp2Header)
            {
                // The header box is walked child by child: only the child headers and the small boxes are read.
                ProbeHeaderBox(reader, contentLength, toEnd, info);
                if (toEnd)
                {
                    break;
                }
            }
            else if (toEnd || !reader.Skip(contentLength))
            {
                if (!toEnd)
                {
                    throw Malformed("invalid box length.");
                }

                break;
            }
        }

        throw Malformed("JP2 file has no codestream box.");
    }

    /// <summary>Reads one box header with the shared <see cref="ReadBoxHeader"/> parser.</summary>
    /// <param name="reader">The probe reader positioned at the box.</param>
    /// <param name="toEnd">Receives whether the box extends to the end of the data (a zero length field).</param>
    /// <param name="atEnd">Receives whether the data ended cleanly before the box.</param>
    /// <returns>The header, relative to the start of the box (header start at offset zero).</returns>
    private static BoxHeader ReadProbeBoxHeader(ProbeReader reader, out bool toEnd, out bool atEnd)
    {
        toEnd = false;
        atEnd = false;
        var head = reader.Read(8);
        if (head.Length == 0)
        {
            atEnd = true;
            return default;
        }

        if (head.Length == 8 && ReadBe32(head, 0) == 1)
        {
            var extra = reader.Read(8);
            var both = new byte[8 + extra.Length];
            head.CopyTo(both, 0);
            extra.CopyTo(both, 8);
            head = both;
        }

        // At the end of the data the read was short, so the length is exact; otherwise it is the known remaining
        // length, or (non-seekable) the most the cap still allows. A larger box is then reported as invalid.
        var truncated = head.Length < 8 || (head.Length < 16 && ReadBe32(head, 0) == 1);
        var known = reader.Remaining >= 0 ? reader.Remaining : reader.CapLeft;
        var limit = truncated ? head.Length : known + head.Length;
        toEnd = head.Length >= 4 && ReadBe32(head, 0) == 0;
        var box = ReadBoxHeader(head, 0, (int)Math.Min(limit, int.MaxValue));
        return box;
    }

    /// <summary>
    ///     Walks the children of the <c>jp2h</c> superbox without buffering it: each child header is read, the boxes
    ///     the shared parsers interpret are read (bounded by <see cref="MaxProbeChildBytes"/>) and parsed, and every
    ///     other box is skipped. A <c>colr</c> box is read only for its leading bytes, so a large ICC profile is
    ///     never buffered (only the profile header is needed to classify the color space).
    /// </summary>
    /// <param name="reader">The probe reader positioned at the first child box.</param>
    /// <param name="contentLength">The declared content length of <c>jp2h</c>, or the most the stream can hold when <paramref name="toEnd"/>.</param>
    /// <param name="toEnd">Whether the <c>jp2h</c> box extends to the end of the data.</param>
    /// <param name="info">Receives the parsed information.</param>
    private static void ProbeHeaderBox(ProbeReader reader, int contentLength, bool toEnd, Jp2Info info)
    {
        var left = (long)contentLength;
        if (toEnd)
        {
            left = reader.Remaining >= 0 ? reader.Remaining : reader.CapLeft;
        }

        for (var count = 0; count < MaxBoxes && left > 0; count++)
        {
            var head = reader.Read(8);
            if (head.Length == 8 && ReadBe32(head, 0) == 1)
            {
                var extra = reader.Read(8);
                var both = new byte[8 + extra.Length];
                head.CopyTo(both, 0);
                extra.CopyTo(both, 8);
                head = both;
            }

            if (head.Length == 0 && toEnd)
            {
                return;
            }

            var truncated = head.Length < 8 || (head.Length < 16 && ReadBe32(head, 0) == 1);
            var box = ReadBoxHeader(head, 0, (int)(truncated ? head.Length : Math.Min(left, int.MaxValue)));
            var content = box.End - box.ContentStart;
            left -= box.End;

            var read = box.Type switch
            {
                BoxColr => Math.Min(content, ProbeColrBytes),
                BoxPclr or BoxCmap or BoxCdef => content <= MaxProbeChildBytes ? content : throw Malformed("header box is too large."),
                _ => 0,
            };

            if (box.Type is BoxColr or BoxPclr or BoxCmap or BoxCdef)
            {
                var body = reader.Read(read);
                if (body.Length != read)
                {
                    throw Malformed("invalid box length.");
                }

                if (box.Type == BoxColr && content > read && body[0] == 1)
                {
                    throw Malformed("color specification box has an invalid length.");
                }

                ParseHeaderChild(body, new BoxHeader(box.Type, 0, body.Length), info);
            }

            if (content > read && !reader.Skip(content - read))
            {
                throw Malformed("invalid box length.");
            }
        }
    }
}
