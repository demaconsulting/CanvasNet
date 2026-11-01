namespace DemaConsulting.CanvasNet.Codecs;

/// <summary>
///     Decodes a raster image of any supported <see cref="ImageFormat"/> into a
///     <see cref="Canvas.Surface"/>, choosing the matching codec from the image's own signature or
///     from a caller-supplied format.
/// </summary>
/// <remarks>
///     <para>
///         This is a thin dispatcher over <see cref="PngCodec"/>, <see cref="JpegCodec"/>,
///         <see cref="GifCodec"/>, <see cref="BmpCodec"/>, <see cref="TiffCodec"/>, and
///         <see cref="Jpeg2000Codec"/>: it adds no decoding logic, so every exception, limit, and
///         tolerance of the chosen codec's <c>Load(Stream)</c> applies unchanged. It holds no state
///         and is thread-safe.
///     </para>
/// </remarks>
public static class ImageDecoder
{
    /// <summary>
    ///     Decodes an image from a stream, detecting its format from its first bytes.
    /// </summary>
    /// <param name="stream">
    ///     A readable stream positioned at the start of the image. The stream is read, not closed. A
    ///     seekable stream is rewound to its starting position after the signature is read, including
    ///     when the signature is not recognized. A non-seekable stream is read through; the signature is
    ///     replayed rather than buffered with the image, though a codec that needs the whole file
    ///     (JPEG, TIFF, JPEG 2000) still reads it into memory.
    /// </param>
    /// <returns>The decoded image.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">
    ///     The stream does not start with a supported image signature, or the chosen codec rejects the data.
    /// </exception>
    public static Canvas.Surface Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var header = new byte[ImageFormats.HeaderLength];
        var headerLength = 0;
        var startPosition = stream.CanSeek ? stream.Position : 0;
        while (headerLength < header.Length)
        {
            var read = stream.Read(header, headerLength, header.Length - headerLength);
            if (read == 0)
            {
                break;
            }

            headerLength += read;
        }

        var format = ImageFormats.Detect(header.AsSpan(0, headerLength));
        if (stream.CanSeek)
        {
            stream.Position = startPosition;
        }

        if (format is null)
        {
            throw new InvalidDataException("The data is not a recognized image format.");
        }

        if (stream.CanSeek)
        {
            return Load(stream, format.Value);
        }

        using var replay = new PrefixedStream(header.AsSpan(0, headerLength).ToArray(), stream);
        return Load(replay, format.Value);
    }

    /// <summary>
    ///     Decodes an image from a byte span, detecting its format from its first bytes.
    /// </summary>
    /// <param name="data">The complete image file.</param>
    /// <returns>The decoded image.</returns>
    /// <exception cref="InvalidDataException">
    ///     The data does not start with a supported image signature, or the chosen codec rejects it.
    /// </exception>
    public static Canvas.Surface Load(ReadOnlySpan<byte> data)
    {
        var format = ImageFormats.Detect(data)
            ?? throw new InvalidDataException("The data is not a recognized image format.");

        using var stream = new MemoryStream(data.ToArray(), false);
        return Load(stream, format);
    }

    /// <summary>
    ///     Decodes an image from a stream as the given format, without sniffing its signature.
    /// </summary>
    /// <param name="stream">A readable stream positioned at the start of the image. The stream is read, not closed.</param>
    /// <param name="format">The format to decode the stream as.</param>
    /// <returns>The decoded image.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="format"/> is not a defined <see cref="ImageFormat"/>.</exception>
    /// <exception cref="InvalidDataException">The codec for <paramref name="format"/> rejects the data.</exception>
    public static Canvas.Surface Load(Stream stream, ImageFormat format)
    {
        ArgumentNullException.ThrowIfNull(stream);

        return format switch
        {
            ImageFormat.Png => PngCodec.Load(stream),
            ImageFormat.Jpeg => JpegCodec.Load(stream),
            ImageFormat.Gif => GifCodec.Load(stream),
            ImageFormat.Bmp => BmpCodec.Load(stream),
            ImageFormat.Tiff => TiffCodec.Load(stream),
            ImageFormat.Jpeg2000 => Jpeg2000Codec.Load(stream),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "The image format is not supported.")
        };
    }

    /// <summary>
    ///     A forward-only read stream that replays an already-consumed prefix and then continues with
    ///     the rest of an underlying (non-seekable) stream, so a signature can be sniffed without
    ///     buffering the whole image.
    /// </summary>
    private sealed class PrefixedStream(byte[] prefix, Stream rest) : Stream
    {
        private int _prefixPosition;

        /// <inheritdoc />
        public override bool CanRead => true;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <inheritdoc />
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc />
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        /// <inheritdoc />
        public override int Read(Span<byte> buffer)
        {
            if (_prefixPosition < prefix.Length)
            {
                var copied = Math.Min(buffer.Length, prefix.Length - _prefixPosition);
                prefix.AsSpan(_prefixPosition, copied).CopyTo(buffer);
                _prefixPosition += copied;
                return copied;
            }

            return rest.Read(buffer);
        }

        /// <inheritdoc />
        public override void Flush()
        {
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
