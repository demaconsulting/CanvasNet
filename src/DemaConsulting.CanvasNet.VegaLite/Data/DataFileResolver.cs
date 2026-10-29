using System.Text;

namespace DemaConsulting.CanvasNet.VegaLite.Data;

/// <summary>
///     A data file that was found and read: its identity, the name shown in messages, and its decoded text.
/// </summary>
/// <param name="Key">The identity of the file (its normalized full path, or its normalized logical path in resolver mode).</param>
/// <param name="Display">The name shown in messages: the path as the specification wrote it, with forward slashes.</param>
/// <param name="Text">The text of the file, decoded as strict UTF-8 without the byte-order mark.</param>
internal sealed record DataFile(string Key, string Display, string Text);

/// <summary>
///     Finds and reads the local data files that a chart names with <c>data.url</c>, inside a sandbox, and enforces
///     the data-file limits.
/// </summary>
/// <remarks>
///     <para>
///         <b>Scope.</b> The resolver handles relative local paths only. A URL that has a scheme, or that is absolute,
///         rooted, a drive path or a network path, never reaches it: <see cref="DataLoader"/> classifies such a URL as
///         remote data (reported by the feature catalog, never fetched) before it asks the resolver for anything. The
///         resolver itself also rejects such a path (and an empty path, a control character, a colon and a path that
///         leaves its root) as a sandbox violation, so it stays safe when used on its own. The resolver is
///         independent of every other CanvasNet project; it re-implements the path rules of the other file resolvers
///         of the library instead of referencing them.
///     </para>
///     <para>
///         <b>Search order.</b> In file-system mode a path is looked for under the base directory of the document
///         (<see cref="VegaLiteOpenOptions.BaseDirectory"/>) and then under each entry of
///         <see cref="VegaLiteOpenOptions.DataPaths"/>, in declaration order; the first existing file wins. With a
///         <see cref="VegaLiteOpenOptions.FileResolver"/> the file system is never touched: the callback receives the
///         normalized logical path (forward slashes, no leading slash, <c>.</c> and <c>..</c> collapsed) under the
///         logical root and under each data path that is itself a valid logical path, and the stream it returns is
///         disposed here. The environment variables and the current directory are never consulted for the base
///         directory.
///     </para>
///     <para>
///         <b>Reading.</b> A file is read as bytes with a bound: at most <see cref="VegaLiteLimits.MaxDataBytes"/>
///         bytes, so an endless stream stops one chunk past the limit, and at most
///         <see cref="VegaLiteLimits.MaxDataFiles"/> distinct files per call. The bytes are decoded as strict UTF-8
///         (a byte-order mark is removed; invalid UTF-8 is rejected, never replaced by U+FFFD), the same strictness as
///         the specification text. A file that is found, read or decoded wrongly, a file that is missing and a file that
///         is blocked by the sandbox are all failures (<see cref="VegaLiteRenderException"/>): a local data file that
///         cannot be used is never treated as an empty dataset. A directory where a file is expected, an open that fails
///         with an I/O or access error, and a callback that throws or whose stream fails to read or dispose are the same
///         located read failure; only a cancellation passes through unchanged. Files are opened read-only with
///         <see cref="FileShare.ReadWrite"/> and <see cref="FileShare.Delete"/>, so concurrent readers, writers and
///         rename or delete operations by other processes do not block the bounded read.
///     </para>
///     <para>
///         <b>Cache.</b> Each distinct candidate is requested at most once per instance (a miss is remembered too, so a
///         resolver callback sees each logical path at most once). An instance serves one measure or render call, runs
///         only on the calling thread and is not thread-safe. See <c>DataFileResolver.Paths</c> for the path rules and the
///         link check; a concurrent change of the tree between the link check and the open is not defended against.
///     </para>
/// </remarks>
internal sealed partial class DataFileResolver
{
    /// <summary>The number of bytes read from a stream per call.</summary>
    private const int ReadChunkSize = 8192;

    /// <summary>The strict UTF-8 decoder: invalid bytes throw instead of becoming U+FFFD.</summary>
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>The effective open options (base directory, data paths, resolver callback).</summary>
    private readonly VegaLiteOpenOptions _options;

    /// <summary>The render limits.</summary>
    private readonly VegaLiteLimits _limits;

    /// <summary>The cancellation token polled between files and chunks.</summary>
    private readonly CancellationToken _cancellation;

    /// <summary>The number of distinct files read so far.</summary>
    private int _filesRead;

    /// <summary>
    ///     Initializes a resolver for one measure or render call.
    /// </summary>
    /// <param name="options">The effective open options.</param>
    /// <param name="limits">The render limits.</param>
    /// <param name="cancellation">The cancellation token, polled between files and chunks.</param>
    /// <param name="isLink">The probe that tells whether a full path is a symbolic link or junction; <see langword="null"/> uses the file system. A seam for tests.</param>
    public DataFileResolver(VegaLiteOpenOptions options, VegaLiteLimits limits, CancellationToken cancellation, Func<string, bool>? isLink = null)
    {
        _options = options;
        _limits = limits;
        _cancellation = cancellation;
        _isLink = isLink ?? DefaultIsLink;
        _keyComparer = options.FileResolver is null ? PlatformKeyComparer : StringComparer.Ordinal;
        _files = new Dictionary<string, DataFile?>(_keyComparer);
        _linkStates = new Dictionary<string, bool>(_keyComparer);
    }

    /// <summary>
    ///     Gets a value indicating whether at least one way of finding files is configured.
    /// </summary>
    public bool HasFileSource =>
        _options.FileResolver is not null || _options.BaseDirectory is not null || _options.DataPaths.Count > 0;

    /// <summary>
    ///     Finds and reads the file a relative path names.
    /// </summary>
    /// <param name="path">The path as written in the specification; must be a relative local path.</param>
    /// <param name="location">The location of the path in the specification, for errors.</param>
    /// <returns>The file; never <see langword="null"/>.</returns>
    /// <exception cref="VegaLiteRenderException">
    ///     Thrown when no file source is configured, the path violates the sandbox, a component is a link, the file does
    ///     not exist, cannot be read, is not valid UTF-8, or exceeds <see cref="VegaLiteLimits.MaxDataBytes"/> or
    ///     <see cref="VegaLiteLimits.MaxDataFiles"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when the cancellation token is canceled.</exception>
    public DataFile Load(string path, VegaLiteLocation location)
    {
        ArgumentNullException.ThrowIfNull(path);
        _cancellation.ThrowIfCancellationRequested();

        // Without any file source the path cannot be honored at all, which is an error and never an empty dataset
        if (!HasFileSource)
        {
            throw new VegaLiteRenderException(
                $"Cannot read the data file '{Printable(path)}': the document has no base directory, data path or file resolver (open the specification from a file, or set VegaLiteOpenOptions.BaseDirectory, DataPaths or FileResolver).",
                location);
        }

        // Candidates are produced lazily: the first one that exists wins, so later roots are only examined on a miss
        var text = ValidatePathText(path, location);
        foreach (var candidate in Candidates(text, location))
        {
            var file = LoadCandidate(candidate, text, location);
            if (file is not null)
            {
                return file;
            }
        }

        throw new VegaLiteRenderException($"The data file '{text}' was not found.", location);
    }

    /// <summary>
    ///     Loads a candidate through the cache.
    /// </summary>
    /// <param name="candidate">The candidate.</param>
    /// <param name="display">The name shown in messages.</param>
    /// <param name="location">The location of the path, for errors.</param>
    /// <returns>The file, or <see langword="null"/> when the candidate does not exist.</returns>
    private DataFile? LoadCandidate(Candidate candidate, string display, VegaLiteLocation location)
    {
        if (_files.TryGetValue(candidate.Key, out var cached))
        {
            return cached;
        }

        _cancellation.ThrowIfCancellationRequested();
        var bytes = ReadBytes(candidate, display, location);
        var file = bytes is null ? null : new DataFile(candidate.Key, display, Decode(bytes, display, location));
        _files[candidate.Key] = file;
        return file;
    }

    /// <summary>
    ///     Reads the bytes of a candidate from the resolver callback or the file system.
    /// </summary>
    /// <param name="candidate">The candidate.</param>
    /// <param name="display">The name shown in messages.</param>
    /// <param name="location">The location of the path, for errors.</param>
    /// <returns>The bytes, or <see langword="null"/> when the candidate does not exist.</returns>
    private byte[]? ReadBytes(Candidate candidate, string display, VegaLiteLocation location)
    {
        if (UsesResolver)
        {
            return ReadFromCallback(candidate, display, location);
        }

        // The walk runs before the file is opened (and before File.Exists on the final component), so nothing is
        // ever read through a link and a file behind a link is never reported as existing
        if (!WalkRejectingLinks(candidate, display, location))
        {
            return null;
        }

        if (!File.Exists(candidate.Key))
        {
            // A directory where a file is expected is not a missing file: it exists but cannot be read as data
            return Directory.Exists(candidate.Key) ? throw CannotRead(display, location) : null;
        }

        try
        {
            // Read-only access that tolerates concurrent readers, writers and rename or delete operations by other
            // processes, so a file that is being written elsewhere does not turn into a sharing violation
            using var file = new FileStream(candidate.Key, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, ReadChunkSize);
            return ReadBounded(file, display, location);
        }
        catch (FileNotFoundException)
        {
            // The file vanished between the check and the open: it is missing
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Any other failure to open the file (sharing, access, device) is a located read failure
            throw CannotRead(display, location);
        }
    }

    /// <summary>
    ///     Reads the bytes of a candidate from the resolver callback.
    /// </summary>
    /// <remarks>
    ///     The callback, the bounded read and the disposal of the returned stream form one boundary: a failure of any of
    ///     them (other than a cancellation or a limit failure that is already located) is the located read failure,
    ///     because the callback is untrusted code that must not leak a raw exception through the render contract.
    /// </remarks>
    /// <param name="candidate">The candidate.</param>
    /// <param name="display">The name shown in messages.</param>
    /// <param name="location">The location of the path, for errors.</param>
    /// <returns>The bytes, or <see langword="null"/> when the callback has no such file.</returns>
    private byte[]? ReadFromCallback(Candidate candidate, string display, VegaLiteLocation location)
    {
        try
        {
            // The callback owns what it returns until it is handed to us; we dispose it exactly once after reading
            using var stream = _options.FileResolver!(candidate.Key);
            return stream is null ? null : ReadBounded(stream, display, location);
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or VegaLiteRenderException))
        {
            throw CannotRead(display, location);
        }
    }

    /// <summary>
    ///     Creates the exception for a file that cannot be read.
    /// </summary>
    /// <param name="display">The name shown in messages.</param>
    /// <param name="location">The location of the path.</param>
    /// <returns>The exception, ready to throw.</returns>
    private static VegaLiteRenderException CannotRead(string display, VegaLiteLocation location) =>
        new($"The data file '{display}' cannot be read.", location);

    /// <summary>
    ///     Reads a whole stream as bytes under the limits, stopping once a limit is certainly exceeded.
    /// </summary>
    /// <remarks>
    ///     The file counts against <see cref="VegaLiteLimits.MaxDataFiles"/> as soon as it is known to exist, before any
    ///     byte is read. A size the stream reports is checked first, and the running length is checked after every
    ///     chunk, so a stream that never ends stops one chunk past the limit.
    /// </remarks>
    /// <param name="stream">The stream; it is left open.</param>
    /// <param name="display">The name shown in messages.</param>
    /// <param name="location">The location of the path, for errors.</param>
    /// <returns>The bytes.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when a limit is exceeded or the stream cannot be read.</exception>
    private byte[] ReadBounded(Stream stream, string display, VegaLiteLocation location)
    {
        if (_filesRead >= _limits.MaxDataFiles)
        {
            throw new VegaLiteRenderException(
                $"The chart reads more than the limit of {_limits.MaxDataFiles} data files (MaxDataFiles); the file '{display}' was not read.", location);
        }

        _filesRead++;
        var max = _limits.MaxDataBytes;
        try
        {
            if (stream.CanSeek && stream.Length > max)
            {
                throw TooLarge(display, location);
            }

            using var bytes = new MemoryStream();
            var buffer = new byte[ReadChunkSize];
            int count;
            while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (bytes.Length + count > max)
                {
                    throw TooLarge(display, location);
                }

                bytes.Write(buffer, 0, count);
                _cancellation.ThrowIfCancellationRequested();
            }

            return bytes.ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw CannotRead(display, location);
        }
    }

    /// <summary>
    ///     Creates the exception for a file that is longer than the byte limit.
    /// </summary>
    /// <param name="display">The name shown in messages.</param>
    /// <param name="location">The location of the path.</param>
    /// <returns>The exception, ready to throw.</returns>
    private VegaLiteRenderException TooLarge(string display, VegaLiteLocation location) =>
        new($"The data file '{display}' is larger than the limit of {_limits.MaxDataBytes} bytes (MaxDataBytes).", location);

    /// <summary>
    ///     Decodes the bytes of a file as strict UTF-8, removing a byte-order mark.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="display">The name shown in messages.</param>
    /// <param name="location">The location of the path, for errors.</param>
    /// <returns>The text.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the bytes are not valid UTF-8.</exception>
    private static string Decode(byte[] bytes, string display, VegaLiteLocation location)
    {
        // The byte-order mark of UTF-8 is EF BB BF; any other encoding (UTF-16, for example) fails the strict decode
        var start = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        try
        {
            return StrictUtf8.GetString(bytes, start, bytes.Length - start);
        }
        catch (DecoderFallbackException)
        {
            throw new VegaLiteRenderException($"The data file '{display}' is not valid UTF-8.", location);
        }
    }
}
