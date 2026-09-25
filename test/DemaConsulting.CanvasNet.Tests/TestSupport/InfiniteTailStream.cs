namespace DemaConsulting.CanvasNet.Tests.TestSupport;

/// <summary>
///     A test-only <see cref="Stream"/> that serves a fixed, finite prefix of bytes and then
///     never reaches end-of-stream: every subsequent read returns a full buffer of zero bytes
///     rather than <c>0</c>, used to prove that a codec's <c>GetInfo</c> method stops reading as
///     soon as it has found what it needs, rather than draining a stream to end-of-stream (which,
///     for a genuinely unbounded or slow stream, could otherwise never return).
/// </summary>
/// <remarks>
///     Pair with <see cref="BoundedReadStream"/> (wrapping this stream) in tests so that any
///     attempt to read past the known prefix - which would otherwise read the "infinite" tail
///     forever and hang the test - instead fails fast with an
///     <see cref="InvalidOperationException"/>.
/// </remarks>
public sealed class InfiniteTailStream : Stream
{
    private readonly byte[] _prefix;
    private int _prefixPosition;

    /// <summary>
    ///     Initializes a new instance of the <see cref="InfiniteTailStream"/> class that serves
    ///     the supplied prefix bytes before becoming an unbounded stream of zero bytes.
    /// </summary>
    /// <param name="prefix">The finite prefix of bytes to serve before the infinite tail.</param>
    public InfiniteTailStream(byte[] prefix)
    {
        _prefix = prefix;
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
        if (count == 0)
        {
            return 0;
        }

        if (_prefixPosition < _prefix.Length)
        {
            var fromPrefix = Math.Min(count, _prefix.Length - _prefixPosition);
            Array.Copy(_prefix, _prefixPosition, buffer, offset, fromPrefix);
            _prefixPosition += fromPrefix;
            return fromPrefix;
        }

        // The prefix has been fully served: the tail never ends, so always fill the requested
        // buffer (with arbitrary zero bytes) rather than ever returning 0.
        Array.Clear(buffer, offset, count);
        return count;
    }

    /// <inheritdoc/>
    public override void Flush()
    {
        // No-op: this stream has no buffered writes to flush.
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
