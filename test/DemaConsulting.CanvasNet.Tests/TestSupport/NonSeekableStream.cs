namespace DemaConsulting.CanvasNet.Tests.TestSupport;

/// <summary>
///     A test-only <see cref="Stream"/> wrapper that forces <see cref="CanSeek"/> to
///     <see langword="false"/> and rejects any attempt to seek, used to exercise a codec's
///     non-seekable-stream rejection path (for example
///     <see cref="global::CanvasNet.Codecs.TiffCodec.GetInfo(System.IO.Stream)"/>'s
///     <see cref="NotSupportedException"/> guard) with a stream that behaves like a genuinely
///     forward-only source such as a network stream.
/// </summary>
/// <remarks>
///     Wraps an inner, fully readable stream (typically a <see cref="MemoryStream"/> containing a
///     real fixture's bytes) and forwards <see cref="Read(byte[], int, int)"/> calls to it
///     unchanged, while <see cref="CanSeek"/> always reports <see langword="false"/> and
///     <see cref="Seek"/>/the <see cref="Position"/> setter both throw
///     <see cref="NotSupportedException"/>, matching the contract real non-seekable
///     <see cref="Stream"/> implementations (for example <see cref="System.Net.Sockets.NetworkStream"/>)
///     expose.
/// </remarks>
public sealed class NonSeekableStream : Stream
{
    private readonly Stream _inner;

    /// <summary>
    ///     Initializes a new instance of the <see cref="NonSeekableStream"/> class wrapping an
    ///     inner, readable stream.
    /// </summary>
    /// <param name="inner">The inner stream to read from and forward all reads to.</param>
    public NonSeekableStream(Stream inner)
    {
        _inner = inner;
    }

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

    /// <inheritdoc/>
    public override void Flush() => _inner.Flush();

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException($"{nameof(NonSeekableStream)} does not support seeking.");

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
