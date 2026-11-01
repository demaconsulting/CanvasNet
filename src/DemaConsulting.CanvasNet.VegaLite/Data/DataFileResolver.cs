using System.Text;
using DemaConsulting.CanvasNet.Files;

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
///         sandbox itself also rejects such a path (and an empty path, a control character, a colon and a path that
///         leaves its root), so the resolver stays safe when used on its own.
///     </para>
///     <para>
///         <b>Delegation.</b> Everything that makes the access safe is done by the core
///         <see cref="SandboxedFileResolver"/>: path validation, confinement to the base directory and each data path,
///         the refusal of symbolic links and junctions, the open and bounded read (with the byte limit
///         <see cref="VegaLiteLimits.MaxDataBytes"/>), the resolver-callback mode and the per-call file cache. This class
///         adds what is specific to Vega-Lite: the search order (base directory, then <see cref="VegaLiteOpenOptions.DataPaths"/>
///         in declaration order, or with a <see cref="VegaLiteOpenOptions.FileResolver"/> the normalized logical path under
///         the logical root and each data path), the distinct-file limit <see cref="VegaLiteLimits.MaxDataFiles"/>, strict
///         UTF-8 decoding (a byte-order mark is removed; invalid UTF-8 is rejected, never replaced by U+FFFD) and the
///         translation of every <see cref="SandboxException"/> into a located <see cref="VegaLiteRenderException"/> with
///         Vega-Lite wording. A local data file that cannot be used is never treated as an empty dataset: a file that is
///         missing, blocked by the sandbox, unreadable or undecodable is a failure. Only a cancellation passes through
///         unchanged.
///     </para>
///     <para>
///         <b>Cache.</b> Each distinct file is requested at most once per instance (a miss is remembered too, so a
///         resolver callback sees each logical path at most once) and decoded at most once; it counts once against
///         <see cref="VegaLiteLimits.MaxDataFiles"/>. An instance serves one measure or render call, runs only on the
///         calling thread and is not thread-safe.
///     </para>
/// </remarks>
internal sealed class DataFileResolver
{
    /// <summary>The strict UTF-8 decoder: invalid bytes throw instead of becoming U+FFFD.</summary>
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>The core sandbox that finds and reads the files.</summary>
    private readonly SandboxedFileResolver _sandbox;

    /// <summary>The render limits.</summary>
    private readonly VegaLiteLimits _limits;

    /// <summary>The cancellation token polled between files.</summary>
    private readonly CancellationToken _cancellation;

    /// <summary>The decoded files by key; each distinct file is decoded once.</summary>
    private readonly Dictionary<string, DataFile> _files;

    /// <summary>The number of distinct files read so far.</summary>
    private int _filesRead;

    /// <summary>
    ///     Initializes a resolver for one measure or render call.
    /// </summary>
    /// <param name="options">The effective open options.</param>
    /// <param name="limits">The render limits.</param>
    /// <param name="cancellation">The cancellation token, polled between files and chunks.</param>
    public DataFileResolver(VegaLiteOpenOptions options, VegaLiteLimits limits, CancellationToken cancellation)
    {
        _limits = limits;
        _cancellation = cancellation;
        _sandbox = new SandboxedFileResolver(
            new SandboxOptions
            {
                BaseDirectory = options.BaseDirectory,
                SearchPaths = options.DataPaths,
                FileResolver = options.FileResolver,
                MaxFileBytes = limits.MaxDataBytes,
            },
            cancellation);
        _files = new Dictionary<string, DataFile>(_sandbox.KeyComparer);
    }

    /// <summary>
    ///     Gets a value indicating whether at least one way of finding files is configured.
    /// </summary>
    public bool HasFileSource => _sandbox.HasSource;

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

        // The sandbox validates the path, confines it, refuses links and reads the file; its refusals become located errors
        var display = path.Replace('\\', '/');
        SandboxedFile? found;
        try
        {
            found = _sandbox.Resolve(path);
        }
        catch (SandboxException ex)
        {
            throw Translate(ex, display, location);
        }

        if (found is null)
        {
            throw new VegaLiteRenderException($"The data file '{display}' was not found.", location);
        }

        // A file that is reached again (by another spelling or another request) is served from the cache and counts once
        if (_files.TryGetValue(found.Key, out var cached))
        {
            return cached;
        }

        if (_filesRead >= _limits.MaxDataFiles)
        {
            throw new VegaLiteRenderException(
                $"The chart reads more than the limit of {_limits.MaxDataFiles} data files (MaxDataFiles); the file '{display}' was not read.", location);
        }

        _filesRead++;
        var file = new DataFile(found.Key, display, Decode(found.Content.Span, display, location));
        _files[found.Key] = file;
        return file;
    }

    /// <summary>
    ///     Translates a sandbox refusal into the located render exception with Vega-Lite wording.
    /// </summary>
    /// <param name="ex">The sandbox exception.</param>
    /// <param name="display">The name shown in messages: the path as written, with forward slashes.</param>
    /// <param name="location">The location of the path, for errors.</param>
    /// <returns>The exception, ready to throw.</returns>
    internal VegaLiteRenderException Translate(SandboxException ex, string display, VegaLiteLocation location) => ex.Kind switch
    {
        SandboxFailureKind.EmptyPath or SandboxFailureKind.ControlCharacter or SandboxFailureKind.ColonInPath
            or SandboxFailureKind.AbsolutePath => new($"The data path '{ex.Path}' is not allowed: {ex.Detail}.", location),
        SandboxFailureKind.OutsideRoot => new($"The data path '{display}' is outside the allowed directories.", location),
        SandboxFailureKind.ThroughLink => new(
            $"The data file '{display}' passes through a link ('{ex.Detail}'); links are not followed inside the allowed directories.",
            location),
        SandboxFailureKind.LinkCheckFailed => new($"The data file '{display}' cannot be checked for links and is not read.", location),
        SandboxFailureKind.TooLarge => new(
            $"The data file '{display}' is larger than the limit of {_limits.MaxDataBytes} bytes (MaxDataBytes).", location),
        SandboxFailureKind.InvalidRoot => new("A configured data directory (BaseDirectory or DataPaths) is not a valid path.", location),
        _ => new($"The data file '{display}' cannot be read.", location),
    };

    /// <summary>
    ///     Replaces control characters so that a path can be shown in a message.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text with each control character replaced by a question mark.</returns>
    private static string Printable(string text) => new(text.Select(c => char.IsControl(c) ? '?' : c).ToArray());

    /// <summary>
    ///     Decodes the bytes of a file as strict UTF-8, removing a byte-order mark.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="display">The name shown in messages.</param>
    /// <param name="location">The location of the path, for errors.</param>
    /// <returns>The text.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the bytes are not valid UTF-8.</exception>
    private static string Decode(ReadOnlySpan<byte> bytes, string display, VegaLiteLocation location)
    {
        // The byte-order mark of UTF-8 is EF BB BF; any other encoding (UTF-16, for example) fails the strict decode
        var start = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        try
        {
            return StrictUtf8.GetString(bytes[start..]);
        }
        catch (DecoderFallbackException)
        {
            throw new VegaLiteRenderException($"The data file '{display}' is not valid UTF-8.", location);
        }
    }
}
