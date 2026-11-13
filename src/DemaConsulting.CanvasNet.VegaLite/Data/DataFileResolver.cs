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
///     Finds and reads the local data files that a chart names with <c>data.url</c>, under the file-access policy of
///     the caller, and enforces the data-file limits.
/// </summary>
/// <remarks>
///     <para>
///         <b>Scope.</b> The resolver handles local paths: relative, absolute and drive paths, and the local path of a
///         <c>file:</c> URI without a host (converted by <see cref="DataLoader"/>). A URL that names remote data (a scheme
///         other than a local <c>file:</c> URI, or a network path) never reaches it: <see cref="DataLoader"/> classifies it
///         (see <see cref="SpecModel.FeatureCatalog.IsRemoteDataUrl"/>; reported by the feature catalog, never fetched) before it asks
///         the resolver for anything. The core engine itself also rejects a network or device path, an empty path, a
///         control character and a stray colon, so the resolver stays safe when used on its own.
///     </para>
///     <para>
///         <b>Delegation.</b> Everything that makes the access safe is done by the core
///         <see cref="FileReferenceResolver"/>: path validation, the file-access policy
///         (<see cref="VegaLiteOpenOptions.FileAccess"/>, every regular local file by default) judged on the real path of
///         the opened file, the regular-file check, the open and bounded read (with the byte limit
///         <see cref="VegaLiteLimits.MaxDataBytes"/> and the distinct-file limit <see cref="VegaLiteLimits.MaxDataFiles"/>,
///         the latter enforced before the file beyond it is opened or requested, whatever the policy), the
///         resolver-callback mode and the per-call file cache. This class
///         adds what is specific to Vega-Lite: the search order (base directory, then <see cref="VegaLiteOpenOptions.DataPaths"/>
///         in declaration order, or with a <see cref="VegaLiteOpenOptions.FileResolver"/> the normalized logical path under
///         the logical root and each data path; an absolute path names one file), strict
///         UTF-8 decoding (a byte-order mark is removed; invalid UTF-8 is rejected, never replaced by U+FFFD) and the
///         translation of every <see cref="FileReferenceException"/> into a located <see cref="VegaLiteRenderException"/>
///         with Vega-Lite wording and the refusal as its inner exception. A local data file that cannot be used is never
///         treated as an empty dataset: a file that is missing, denied by the policy, unreadable or undecodable is a
///         failure. Only a cancellation passes through unchanged.
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

    /// <summary>The core engine that finds, checks and reads the files.</summary>
    private readonly FileReferenceResolver _engine;

    /// <summary>The render limits.</summary>
    private readonly VegaLiteLimits _limits;

    /// <summary>The cancellation token polled between files.</summary>
    private readonly CancellationToken _cancellation;

    /// <summary>The decoded files by key; each distinct file is decoded once.</summary>
    private readonly Dictionary<string, DataFile> _files;

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
        _engine = new FileReferenceResolver(
            new FileReferenceOptions
            {
                BaseDirectory = options.BaseDirectory,
                SearchPaths = options.DataPaths,
                FileResolver = options.FileResolver,
                FileAccess = options.FileAccess,
                MaxFileBytes = limits.MaxDataBytes,
                MaxFiles = limits.MaxDataFiles,
            },
            cancellation);
        _files = new Dictionary<string, DataFile>(_engine.KeyComparer);
    }

    /// <summary>
    ///     Gets a value indicating whether at least one way of finding files is configured.
    /// </summary>
    public bool HasFileSource => _engine.HasSource;

    /// <summary>
    ///     Finds and reads the file a local path names.
    /// </summary>
    /// <param name="path">The local path: relative, or absolute (the local path of a <c>file:</c> URI included).</param>
    /// <param name="location">The location of the path in the specification, for errors.</param>
    /// <param name="written">
    ///     The URL as the specification wrote it, for messages (a <c>file:</c> URI whose local path is
    ///     <paramref name="path"/>), or <see langword="null"/> when it is <paramref name="path"/> itself.
    /// </param>
    /// <returns>The file; never <see langword="null"/>.</returns>
    /// <exception cref="VegaLiteRenderException">
    ///     Thrown when no file source is configured, the path is never allowed, the file-access policy denies the file,
    ///     the file does not exist, is not a regular file, cannot be read, is not valid UTF-8, or exceeds
    ///     <see cref="VegaLiteLimits.MaxDataBytes"/> or <see cref="VegaLiteLimits.MaxDataFiles"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when the cancellation token is canceled.</exception>
    public DataFile Load(string path, VegaLiteLocation location, string? written = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        _cancellation.ThrowIfCancellationRequested();

        // Without any file source the path cannot be honored at all, which is an error and never an empty dataset
        if (!HasFileSource)
        {
            throw new VegaLiteRenderException(
                $"Cannot read the data file '{Printable(written ?? path)}': the document has no base directory, data path or file resolver (open the specification from a file, or set VegaLiteOpenOptions.BaseDirectory, DataPaths or FileResolver).",
                location);
        }

        // The engine validates the path, asks the policy and reads the file; its refusals become located errors that
        // name the file as written, never a host directory the author did not write
        var display = Printable(written ?? path).Replace('\\', '/');
        FileReference? found;
        try
        {
            found = _engine.Resolve(path);
        }
        catch (FileReferenceException ex)
        {
            throw Translate(ex, display, location);
        }

        if (found is null)
        {
            throw new VegaLiteRenderException($"The data file '{display}' was not found.", location);
        }

        // A file that is reached again (by another spelling or another request) is served from the cache
        if (_files.TryGetValue(found.Key, out var cached))
        {
            return cached;
        }

        var file = new DataFile(found.Key, display, Decode(found.Content.Span, display, location));
        _files[found.Key] = file;
        return file;
    }

    /// <summary>
    ///     Translates a refusal of the file engine into the located render exception with Vega-Lite wording.
    /// </summary>
    /// <param name="ex">The refusal.</param>
    /// <param name="display">The name shown in messages: the URL as written, with forward slashes.</param>
    /// <param name="location">The location of the path, for errors.</param>
    /// <returns>The exception, ready to throw, whose inner exception is <paramref name="ex"/>.</returns>
    internal VegaLiteRenderException Translate(FileReferenceException ex, string display, VegaLiteLocation location) => ex.Kind switch
    {
        FileReferenceFailureKind.EmptyPath or FileReferenceFailureKind.ControlCharacter or FileReferenceFailureKind.InvalidPath
            or FileReferenceFailureKind.NetworkPath => new($"The data path '{display}' is not allowed: {ex.Reason}.", location, ex),
        FileReferenceFailureKind.AccessDenied => new(
            $"The data file '{display}' was denied by the file-access policy '{ex.PolicyName}': {ex.Reason}.", location, ex),
        FileReferenceFailureKind.RealPathUnavailable => new(
            $"The data file '{display}' cannot be checked: its real path cannot be determined on this system, so the file-access policy '{ex.PolicyName}' cannot be applied.",
            location,
            ex),
        FileReferenceFailureKind.NotARegularFile => new($"The data file '{display}' is not a regular file.", location, ex),
        FileReferenceFailureKind.TooLarge => new(
            $"The data file '{display}' is larger than the limit of {_limits.MaxDataBytes} bytes (MaxDataBytes).", location, ex),
        FileReferenceFailureKind.InvalidBaseDirectory => new(
            "A configured data directory (BaseDirectory or DataPaths) is not a valid path.", location, ex),
        FileReferenceFailureKind.TooManyFiles => new(
            $"The chart reads more than the limit of {_limits.MaxDataFiles} data files (MaxDataFiles); the file '{display}' was not read.",
            location,
            ex),
        _ => new($"The data file '{display}' cannot be read.", location, ex),
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
