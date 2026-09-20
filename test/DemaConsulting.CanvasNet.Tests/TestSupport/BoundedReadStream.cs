namespace DemaConsulting.CanvasNet.Tests.TestSupport;

/// <summary>
///     A test-only <see cref="Stream"/> wrapper that throws if more than a configured number of
///     bytes are read from it, used to prove that a codec's <c>GetInfo</c> method reads only a
///     file's header and never decodes pixel data.
/// </summary>
/// <remarks>
///     Wraps an inner, fully in-memory stream (typically a <see cref="MemoryStream"/> containing
///     a real fixture's bytes) and forwards every read to it, counting the total bytes returned.
///     Once the configured limit is exceeded, the next <see cref="Read(byte[], int, int)"/> call
///     throws <see cref="InvalidOperationException"/> instead of returning further data - this
///     makes an accidental full-file read (rather than a genuine header-only probe) fail loudly
///     and immediately in a test, rather than silently succeeding because the fixture happened to
///     be small enough to read in full anyway.
/// </remarks>
public sealed class BoundedReadStream : Stream
{
    private readonly Stream _inner;
    private readonly int _maxBytes;
    private int _totalRead;

    /// <summary>
    ///     Initializes a new instance of the <see cref="BoundedReadStream"/> class wrapping an
    ///     inner stream and enforcing a maximum total read count.
    /// </summary>
    /// <param name="inner">The inner stream to read from and forward all reads to.</param>
    /// <param name="maxBytes">
    ///     The maximum total number of bytes that may be read from this stream before further
    ///     reads throw <see cref="InvalidOperationException"/>.
    /// </param>
    public BoundedReadStream(Stream inner, int maxBytes)
    {
        _inner = inner;
        _maxBytes = maxBytes;
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
    public override int Read(byte[] buffer, int offset, int count)
    {
        // Reject a read that would push the cumulative total past the configured bound before
        // forwarding to the inner stream, so a caller reading "just a little too much" is caught
        // deterministically rather than depending on how many bytes the inner stream happens to
        // return per call
        if (_totalRead + count > _maxBytes)
        {
            throw new InvalidOperationException(
                $"Attempted to read past the {_maxBytes}-byte bound enforced by {nameof(BoundedReadStream)}; " +
                "this indicates the caller read more than a header-only probe should.");
        }

        var read = _inner.Read(buffer, offset, count);
        _totalRead += read;
        return read;
    }

    /// <inheritdoc/>
    public override void Flush() => _inner.Flush();

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

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
