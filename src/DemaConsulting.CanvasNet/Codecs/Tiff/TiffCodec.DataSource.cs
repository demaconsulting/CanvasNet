namespace DemaConsulting.CanvasNet.Codecs;

public static partial class TiffCodec
{
    /// <summary>
    ///     Reads exactly <paramref name="buffer"/>'s length worth of bytes from <paramref name="stream"/>,
    ///     tolerating short reads by looping, and throwing <see cref="InvalidDataException"/> if
    ///     the stream ends before the buffer is filled.
    /// </summary>
    private static void ReadStreamExactly(Stream stream, byte[] buffer, string what)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = stream.Read(buffer, totalRead, buffer.Length - totalRead);
            if (read == 0)
            {
                throw new InvalidDataException($"Unexpected end of stream while reading {what}.");
            }

            totalRead += read;
        }
    }

    /// <summary>
    ///     Reads exactly the 8-byte TIFF file header from <paramref name="stream"/>, without
    ///     interpreting it (see <see cref="ParseTiffHeader"/> for that).
    /// </summary>
    private static byte[] ReadStreamHeaderBytes(Stream stream)
    {
        var headerBytes = new byte[8];
        ReadStreamExactly(stream, headerBytes, "TIFF header");
        return headerBytes;
    }

    /// <summary>
    ///     Abstracts "read N bytes at an absolute file position" over either a fully-buffered
    ///     <c>byte[]</c> or a seekable <see cref="Stream"/>, so that <see cref="ParseIfd"/>,
    ///     <see cref="ReadTagValues"/>, <see cref="RequireTagValues"/>,
    ///     <see cref="TryGetTagValues"/>, and <see cref="ReadTiffImageInfo"/> can be written once
    ///     against this interface and reused, byte-for-byte identically, by
    ///     <see cref="Load(Stream)"/> (always byte-array-backed) and by
    ///     <see cref="GetInfo(Stream)"/>, which is stream-backed
    ///     (<see cref="StreamTiffDataSource"/>) on its seekable fast path and
    ///     byte-array-backed (<see cref="ByteArrayTiffDataSource"/>) on its non-seekable
    ///     buffering fallback.
    /// </summary>
    /// <remarks>
    ///     This abstraction exists to eliminate a correctness divergence that previously existed
    ///     between <see cref="GetInfo(Stream)"/>'s seekable and non-seekable code paths: before
    ///     its introduction, the seekable path was a separate, hand-written subset scan
    ///     (<c>ProbeIfdEntriesSeekable</c>) that defaulted <c>SamplesPerPixel</c> to 1 and skipped
    ///     most of <see cref="ReadTiffImageInfo"/>'s format-support validation, while the
    ///     non-seekable path reused <see cref="ReadTiffImageInfo"/> directly - so the same file
    ///     bytes could report a different channel count, or throw only on one path, depending
    ///     solely on whether the caller's stream happened to be seekable. Routing both paths
    ///     through the same validating parser, differing only in which <see cref="ITiffDataSource"/>
    ///     backs the reads, makes that divergence structurally impossible rather than merely
    ///     patched for the one symptom that was reported.
    /// </remarks>
    private interface ITiffDataSource
    {
        /// <summary>
        ///     Reads exactly <paramref name="length"/> bytes starting at absolute position
        ///     <paramref name="position"/>, throwing <see cref="InvalidDataException"/> if the
        ///     requested range extends past the available data.
        /// </summary>
        /// <param name="position">The absolute byte position to read from.</param>
        /// <param name="length">The number of bytes to read.</param>
        /// <param name="what">
        ///     A short description of what is being read, used only to produce a descriptive
        ///     <see cref="InvalidDataException"/> message.
        /// </param>
        byte[] ReadBytes(int position, int length, string what);
    }

    /// <summary>
    ///     An <see cref="ITiffDataSource"/> backed by a fully-buffered <c>byte[]</c>, used
    ///     unconditionally by <see cref="Load(Stream)"/>/<see cref="Load(string)"/> (the whole
    ///     stream is buffered since TIFF's directory and value offsets require random access),
    ///     and conditionally by <see cref="GetInfo(Stream)"/>'s non-seekable buffering fallback
    ///     (matching <see cref="Load(Stream)"/>'s own buffering for that same input).
    ///     Preserves the exact bounds checking (<see cref="CheckBounds"/>) this codec has always
    ///     performed for byte-array access.
    /// </summary>
    private sealed class ByteArrayTiffDataSource(byte[] file) : ITiffDataSource
    {
        public byte[] ReadBytes(int position, int length, string what)
        {
            CheckBounds(file, position, length, what);
            return file.AsSpan(position, length).ToArray();
        }
    }

    /// <summary>
    ///     An <see cref="ITiffDataSource"/> backed directly by a seekable <see cref="Stream"/>,
    ///     used only by <see cref="GetInfo(Stream)"/>'s seekable fast path. Seeks to each
    ///     requested absolute position and reads exactly the requested number of bytes, never
    ///     buffering more of the stream than the parser actually asks for - in practice, only the
    ///     IFD entry count, the IFD entries themselves, and any out-of-line tag value that
    ///     <see cref="ReadTiffImageInfo"/> needs (for example a multi-value <c>BitsPerSample</c>
    ///     tag); strip/pixel data is never requested, since <see cref="ReadTiffImageInfo"/> never
    ///     resolves <c>StripOffsets</c>/<c>RowsPerStrip</c>/<c>StripByteCounts</c>. Validates every
    ///     requested range against <see cref="Stream.Length"/> before allocating a buffer, so a
    ///     malformed file cannot force a large allocation via a bogus out-of-line tag length.
    /// </summary>
    /// <param name="stream">The seekable stream to read from.</param>
    /// <param name="basePosition">
    ///     The absolute position <paramref name="stream"/> was at when <see cref="GetInfo(Stream)"/>
    ///     was invoked. All TIFF-file-relative positions the parser requests (the IFD offset, IFD
    ///     entry positions, and out-of-line tag value offsets - all of which are relative to the
    ///     start of the TIFF file, not necessarily the start of the underlying stream) are added
    ///     to this base before seeking or bounds-checking, so a stream that is not already
    ///     positioned at byte 0 (for example a substream within a larger container, or a stream
    ///     the caller has already partially consumed) is read correctly rather than from the
    ///     wrong absolute location.
    /// </param>
    private sealed class StreamTiffDataSource(Stream stream, long basePosition) : ITiffDataSource
    {
        public byte[] ReadBytes(int position, int length, string what)
        {
            // Validate the requested range against the stream's actual length before
            // allocating anything. Without this check, a tiny malformed TIFF could declare
            // an out-of-line tag value array with an attacker-controlled Count in the
            // billions, forcing a huge up-front allocation on GetInfo's "cheap probing" fast
            // path - precisely the resource-exhaustion attack GetInfo exists to guard against.
            var absolutePosition = basePosition + position;
            if (position < 0 || length < 0 || absolutePosition + length > stream.Length)
            {
                throw new InvalidDataException($"Unexpected end of stream while reading {what}.");
            }

            stream.Seek(absolutePosition, SeekOrigin.Begin);
            var buffer = new byte[length];
            ReadStreamExactly(stream, buffer, what);
            return buffer;
        }
    }
}
