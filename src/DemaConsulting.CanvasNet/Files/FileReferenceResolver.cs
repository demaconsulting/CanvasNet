using Microsoft.Win32.SafeHandles;

namespace DemaConsulting.CanvasNet.Files;

/// <summary>
///     Resolves a file that a document references (an image link, an include, a data URL) to the bytes of that file,
///     after a host-chosen <see cref="FileAccessPolicy"/> has approved the file that was actually opened.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why it exists.</b> Every format that reads files named by its documents needs the same mechanics: find the
///         file next to the referring document or on a search path, refuse references that are never safe (network and
///         device paths, alternate data streams, control characters, anything that is not a regular file), let the host
///         decide which files may be read, bound what is read, and turn every failure into one exception type. This type
///         is that single, tested implementation. The default policy, <see cref="FileAccessPolicy.AllowAll"/>, reads
///         any regular local file, as Pandoc does: <c>..</c>, absolute paths and symbolic links are followed.
///     </para>
///     <para>
///         <b>Race-free check.</b> A file is opened first; the policy is then asked about the real path of the
///         <b>open handle</b> (see <see cref="FileAccessRequest.RealPath"/>), and the bytes are read from that same
///         handle. A link swapped in or out by another process after a name-based check therefore cannot redirect the
///         read: the decision is always about the file that is read. A policy that restricts paths
///         (<see cref="FileAccessPolicy.UnderRoots(string[])"/>) also refuses a path as written that lies outside its
///         roots before anything is opened.
///     </para>
///     <para>
///         <b>Search order.</b> A relative reference is looked up in the directory of the file it is relative to (when
///         one is given), otherwise in <see cref="FileReferenceOptions.BaseDirectory"/>, and then under each entry of
///         <see cref="FileReferenceOptions.SearchPaths"/> in order; the first candidate that exists wins. An absolute
///         reference (<c>/x</c>, or <c>C:/x</c> on Windows) names exactly one candidate. A file that does not exist,
///         including a dangling link, is a miss and the next candidate is tried; any other outcome (a denial, a file
///         that cannot be read) ends the search. With a <see cref="FileReferenceOptions.FileResolver"/> the file
///         system and the policy are never touched and the callback is asked for normalized logical paths instead.
///     </para>
///     <para>
///         <b>Caching and limits.</b> Each distinct full path is opened at most once per instance (a miss and a failure
///         are remembered too); the policy is asked again on every <see cref="Resolve(string, FileReference?)"/>,
///         because a custom policy may depend on the referrer. A file is never longer than
///         <see cref="FileReferenceOptions.MaxFileBytes"/>, and with <see cref="FileReferenceOptions.MaxFiles"/> a new
///         file beyond the limit is refused before it is opened, whatever the policy. A file reached again through another
///         chain is the same instance and keeps the origin of the chain that first read it (see
///         <see cref="FileAccessRequest.BaseDirectory"/>); every origin is host-chosen, so either is trusted.
///     </para>
///     <para>
///         <b>Thread safety.</b> An instance is <b>not</b> thread-safe: it keeps unsynchronized caches and invokes the
///         callback and the policy on the calling thread. Use one instance per operation (one render, one document).
///         The options, policies and returned <see cref="FileReference"/> values are immutable and may be shared.
///     </para>
///     <para>
///         <b>Top-level file.</b> Inside the library, the document a host names itself is read through the same
///         instance without being judged by the policy (the host chose it), so its references can be resolved
///         relative to it and are judged from its folder.
///     </para>
/// </remarks>
public sealed partial class FileReferenceResolver
{
    /// <summary>The number of bytes read from a stream per call.</summary>
    private const int ReadChunkSize = 8192;

    /// <summary>The effective options.</summary>
    private readonly FileReferenceOptions _options;

    /// <summary>A snapshot of <see cref="FileReferenceOptions.SearchPaths"/> taken at construction.</summary>
    private readonly string[] _searchPaths;

    /// <summary>The policy that decides whether an opened file may be read.</summary>
    private readonly FileAccessPolicy _policy;

    /// <summary>The cancellation token polled between files and chunks.</summary>
    private readonly CancellationToken _cancellation;

    /// <summary>The test seams around the open, or <see langword="null"/>.</summary>
    private readonly FileOpenHooks? _hooks;

    /// <summary>The outcome of each key that was looked up; <see langword="null"/> remembers a key that does not exist.</summary>
    private readonly Dictionary<string, Outcome?> _outcomes;

    /// <summary>The keys counted against <see cref="FileReferenceOptions.MaxFiles"/>: every file opened or requested.</summary>
    private readonly HashSet<string> _counted;

    /// <summary>
    ///     Initializes a resolver for one operation.
    /// </summary>
    /// <param name="options">Where files may be found, which may be read, and how large and how many. Must not be null.</param>
    /// <param name="cancellationToken">The token polled before each file is read and after each chunk.</param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="options"/>, its <see cref="FileReferenceOptions.SearchPaths"/> or its
    ///     <see cref="FileReferenceOptions.FileAccess"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <see cref="FileReferenceOptions.MaxFileBytes"/> is not positive or
    ///     <see cref="FileReferenceOptions.MaxFiles"/> is negative.
    /// </exception>
    public FileReferenceResolver(FileReferenceOptions options, CancellationToken cancellationToken = default)
        : this(options, cancellationToken, null)
    {
    }

    /// <summary>
    ///     Initializes a resolver with test seams around the open, so the race-free check can be proven without timing.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="hooks">The seams, or <see langword="null"/>.</param>
    internal FileReferenceResolver(FileReferenceOptions options, CancellationToken cancellationToken, FileOpenHooks? hooks)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.SearchPaths);
        ArgumentNullException.ThrowIfNull(options.FileAccess);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.MaxFileBytes, 0L);
        ArgumentOutOfRangeException.ThrowIfNegative(options.MaxFiles ?? 0);
        _options = options;

        // The caller's list may be mutable; a later change must not change where a running resolver looks
        _searchPaths = [.. options.SearchPaths];
        _policy = options.FileAccess;
        _cancellation = cancellationToken;
        _hooks = hooks;
        KeyComparer = UsesCallback ? StringComparer.Ordinal : RealPath.PathComparer;
        _outcomes = new Dictionary<string, Outcome?>(KeyComparer);
        _counted = new HashSet<string>(KeyComparer);
    }

    /// <summary>
    ///     Gets the comparer under which two <see cref="FileReference.Key"/> values name the same path: ordinal for
    ///     logical paths, and the platform's path comparison (case-insensitive on Windows and macOS) for full paths.
    /// </summary>
    public StringComparer KeyComparer { get; }

    /// <summary>
    ///     Gets a value indicating whether at least one way of finding files is configured: a resolver callback, a base
    ///     directory or a search path. <see cref="Resolve(string, FileReference?)"/> returns <see langword="null"/> for
    ///     every reference when it is <see langword="false"/>.
    /// </summary>
    public bool HasSource => UsesCallback || _options.BaseDirectory is not null || _searchPaths.Length > 0;

    /// <summary>
    ///     Gets a value indicating whether the caller supplied a resolver callback (logical-path mode).
    /// </summary>
    private bool UsesCallback => _options.FileResolver is not null;

    /// <summary>
    ///     Finds, checks and reads the file a reference names.
    /// </summary>
    /// <param name="reference">
    ///     The path as written in the document: relative (resolved against the referring file's directory, the base
    ///     directory and the search paths) or absolute. Backslashes are treated as forward slashes, so a reference means
    ///     the same on every platform.
    /// </param>
    /// <param name="relativeTo">
    ///     A file previously returned by <b>this</b> resolver instance (the referring document) whose directory is
    ///     searched first, or <see langword="null"/> to start from the base directory.
    /// </param>
    /// <returns>The file, or <see langword="null"/> when no candidate exists (or no file source is configured).</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="reference"/> is null.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="relativeTo"/> was not returned by this resolver instance (including a file from
    ///     the other mode: a logical file in file-system mode, or the reverse).
    /// </exception>
    /// <exception cref="FileReferenceException">
    ///     Thrown when the reference is never allowed, a configured directory is invalid, the policy denies the file
    ///     (or cannot be applied), the path is not a regular file, the file cannot be read or is too large, or reading it
    ///     would exceed <see cref="FileReferenceOptions.MaxFiles"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when the cancellation token is canceled.</exception>
    public FileReference? Resolve(string reference, FileReference? relativeTo = null)
    {
        ArgumentNullException.ThrowIfNull(reference);
        _cancellation.ThrowIfCancellationRequested();

        // The text is validated before anything is touched, so a hostile reference never causes an access or a request
        var shown = Shown(reference);
        var text = ValidateText(reference, shown);
        if (relativeTo is not null && relativeTo.IsLogical != UsesCallback)
        {
            throw new ArgumentException("The file was resolved in the other mode (file system or callback).", nameof(relativeTo));
        }

        // Only a file this instance handed out carries a trusted origin; the cache holds the one instance per key
        if (relativeTo is not null
            && !(_outcomes.TryGetValue(relativeTo.Key, out var own) && ReferenceEquals(own?.File, relativeTo)))
        {
            throw new ArgumentException("The file was not returned by this resolver.", nameof(relativeTo));
        }

        if (!HasSource)
        {
            return null;
        }

        // Candidates are produced lazily: the first one that exists wins, so later ones are only examined on a miss
        foreach (var candidate in Candidates(text, shown, relativeTo))
        {
            var file = UsesCallback ? LoadLogical(candidate, shown) : LoadFile(candidate, shown, relativeTo, exempt: false);
            if (file is not null)
            {
                return file;
            }
        }

        return null;
    }

    /// <summary>
    ///     Reads the top-level file a host named itself (the document being rendered), which the policy does not judge.
    /// </summary>
    /// <remarks>
    ///     The host chose this file, so the path-text rules, the pre-check, the network-target check and the policy are
    ///     skipped; the open, the regular-file check, the size limit, the <see cref="FileReferenceOptions.MaxFiles"/>
    ///     slot and the cache apply as for <see cref="Resolve(string, FileReference?)"/>. The chain origin of the file is
    ///     its own folder, so references inside it are judged from there. The returned instance belongs to this
    ///     resolver and may be passed as <c>relativeTo</c>. A later <see cref="Resolve(string, FileReference?)"/> that
    ///     reaches the same file is checked as usual.
    /// </remarks>
    /// <param name="path">The path of the file: relative to the base directory (or the current directory), or absolute.</param>
    /// <returns>The file, or <see langword="null"/> when it does not exist.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the resolver uses a resolver callback.</exception>
    /// <exception cref="FileReferenceException">
    ///     Thrown when the path is not valid, is not a regular file, cannot be read or is too large, or reading it would
    ///     exceed <see cref="FileReferenceOptions.MaxFiles"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when the cancellation token is canceled.</exception>
    internal FileReference? ResolveTopLevel(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        _cancellation.ThrowIfCancellationRequested();
        if (UsesCallback)
        {
            throw new InvalidOperationException("A top-level file is read from the file system, not from the resolver callback.");
        }

        EnsureDirectories();
        var shown = Shown(path);
        string full;
        try
        {
            full = Path.GetFullPath(path, _baseDirectory ?? Directory.GetCurrentDirectory());
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new FileReferenceException(FileReferenceFailureKind.InvalidPath, shown, "the path is not a valid path", ex);
        }

        var origin = Path.GetDirectoryName(full) ?? full;
        return LoadFile(new Candidate(full, origin), shown, null, exempt: true);
    }

    /// <summary>
    ///     Reads a whole stream as bytes under a size limit, stopping once the limit is certainly exceeded.
    /// </summary>
    /// <remarks>
    ///     A size the stream reports is checked first, and the running length is checked after every chunk, so a stream
    ///     that never ends stops one chunk past the limit. Failures of the stream itself are not caught here: the
    ///     caller decides what a failing source means. Shared with <see cref="SandboxedFileResolver"/>.
    /// </remarks>
    /// <param name="stream">The stream; it is left open.</param>
    /// <param name="maxBytes">The largest number of bytes accepted.</param>
    /// <param name="cancellationToken">The token polled after every chunk.</param>
    /// <returns>The bytes, or <see langword="null"/> when the stream is longer than the limit.</returns>
    internal static byte[]? ReadBounded(Stream stream, long maxBytes, CancellationToken cancellationToken)
    {
        if (stream.CanSeek && stream.Length > maxBytes)
        {
            return null;
        }

        using var bytes = new MemoryStream();
        var buffer = new byte[ReadChunkSize];
        int count;
        while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (bytes.Length + count > maxBytes)
            {
                return null;
            }

            bytes.Write(buffer, 0, count);
            cancellationToken.ThrowIfCancellationRequested();
        }

        return bytes.ToArray();
    }

    /// <summary>
    ///     Classifies a text as a <c>file:</c> URI and gets the local path of one without a host.
    /// </summary>
    /// <remarks>
    ///     The short form <c>file:/x</c> has no authority and is written out as <c>file:///x</c> first, so every
    ///     platform parses it alike (<see cref="Uri"/> rejects <c>file:/x</c> on Windows). A URI with any host
    ///     (<c>localhost</c>, a server, or a UNC form that <see cref="Uri"/> maps to a host) is
    ///     <see cref="FileUriKind.HasHost"/> and is never read. The text is not trimmed. Shared with the image links of
    ///     <see cref="ImageSources.FolderImageResolver"/> and the data URLs of the Vega-Lite package, so the rules for
    ///     both cannot drift apart.
    /// </remarks>
    /// <param name="text">The text as written.</param>
    /// <param name="localPath">The local path (percent-decoded) when the result is <see cref="FileUriKind.Local"/>.</param>
    /// <returns>How the text reads as a <c>file:</c> URI.</returns>
    internal static FileUriKind ParseFileUri(string text, out string? localPath)
    {
        localPath = null;
        if (!text.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            return FileUriKind.NotFileUri;
        }

        var rest = text[5..];
        var normalized = rest.StartsWith('/') && !rest.StartsWith("//", StringComparison.Ordinal) ? "file://" + rest : text;
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) || !uri.IsFile)
        {
            return FileUriKind.Malformed;
        }

        if (!string.IsNullOrEmpty(uri.Host))
        {
            return FileUriKind.HasHost;
        }

        localPath = uri.LocalPath;
        return FileUriKind.Local;
    }

    /// <summary>
    ///     How a text reads as a <c>file:</c> URI (see <see cref="ParseFileUri(string, out string?)"/>).
    /// </summary>
    internal enum FileUriKind
    {
        /// <summary>The text does not start with <c>file:</c>.</summary>
        NotFileUri,

        /// <summary>A <c>file:</c> URI without a host; it has a local path.</summary>
        Local,

        /// <summary>A <c>file:</c> URI with a host (<c>localhost</c> included); it is never read.</summary>
        HasHost,

        /// <summary>A text that starts with <c>file:</c> but does not parse as a file URI.</summary>
        Malformed
    }

    /// <summary>
    ///     Loads a file-system candidate: lexical pre-check, cache, file limit, then open, inspect, check and read.
    /// </summary>
    /// <param name="candidate">The candidate.</param>
    /// <param name="shown">The reference as shown in messages.</param>
    /// <param name="relativeTo">The referring file, or <see langword="null"/>.</param>
    /// <param name="exempt">
    ///     <see langword="true"/> for the top-level file a host named (<see cref="ResolveTopLevel(string)"/>): the
    ///     pre-check, the network-target check and the policy are skipped.
    /// </param>
    /// <returns>The file, or <see langword="null"/> when the candidate does not exist.</returns>
    private FileReference? LoadFile(Candidate candidate, string shown, FileReference? relativeTo, bool exempt)
    {
        // A restrictive policy refuses a path as written outside its area before the open, so such a file is never
        // opened: no existence oracle, and no device outside the area is touched
        if (!exempt && _policy.PreCheck(candidate.Key) is { IsAllowed: false } early)
        {
            throw Denied(shown, candidate.Key, null, early, null);
        }

        // A known key answers from the cache; the policy is asked again, because it may depend on the referrer. A file
        // first read as the exempt top-level file gets every check it skipped when a reference reaches it
        if (_outcomes.TryGetValue(candidate.Key, out var known))
        {
            if (!exempt && known?.File is { } cachedFile)
            {
                if (known.Exempt)
                {
                    CheckExemptFile(candidate, shown, relativeTo, cachedFile);
                }
                else if (_policy.RequiresRealPath)
                {
                    CheckPolicy(candidate, shown, relativeTo, cachedFile.RealPath!);
                }
            }

            return known is null ? null : Deliver(known, shown, candidate.Key);
        }

        EnforceFileLimit(candidate.Key, shown);
        _cancellation.ThrowIfCancellationRequested();
        var outcome = OpenCheckAndRead(candidate, shown, relativeTo, exempt);
        _outcomes[candidate.Key] = outcome;
        return outcome is null ? null : Deliver(outcome, shown, candidate.Key);
    }

    /// <summary>
    ///     Applies the checks the top-level file skipped when a reference reaches it later: the network target, the real
    ///     path a restrictive policy needs, and the policy.
    /// </summary>
    /// <param name="candidate">The candidate.</param>
    /// <param name="shown">The reference as shown in messages.</param>
    /// <param name="relativeTo">The referring file, or <see langword="null"/>.</param>
    /// <param name="file">The file read as the top-level file.</param>
    /// <exception cref="FileReferenceException">Thrown when a check refuses the file.</exception>
    private void CheckExemptFile(Candidate candidate, string shown, FileReference? relativeTo, FileReference file)
    {
        if (OperatingSystem.IsWindows() && file.RealPath is { } real
            && RealPath.IsNetworkLocation(real) && !RealPath.IsNetworkLocation(candidate.Key))
        {
            throw new FileReferenceException(FileReferenceFailureKind.NetworkPath, shown, "it leads to a network location")
            {
                RequestedPath = candidate.Key,
                RealPath = real,
            };
        }

        if (!_policy.RequiresRealPath)
        {
            return;
        }

        if (file.RealPath is null)
        {
            throw new FileReferenceException(
                FileReferenceFailureKind.RealPathUnavailable,
                shown,
                "the real path of the opened file cannot be determined on this system")
            {
                RequestedPath = candidate.Key,
                PolicyName = _policy.Name,
            };
        }

        CheckPolicy(candidate, shown, relativeTo, file.RealPath);
    }

    /// <summary>
    ///     Opens a candidate, inspects the open handle, asks the policy about it and reads it from the same handle.
    /// </summary>
    /// <param name="candidate">The candidate.</param>
    /// <param name="shown">The reference as shown in messages.</param>
    /// <param name="relativeTo">The referring file, or <see langword="null"/>.</param>
    /// <param name="exempt">Whether the file is the top-level file, which the network-target check and the policy skip.</param>
    /// <returns>The outcome to remember, or <see langword="null"/> when the candidate does not exist.</returns>
    /// <exception cref="FileReferenceException">Thrown when the policy denies the file (not remembered: it is asked again next time).</exception>
    private Outcome? OpenCheckAndRead(Candidate candidate, string shown, FileReference? relativeTo, bool exempt)
    {
        var key = candidate.Key;
        _hooks?.BeforeOpen?.Invoke(key);
        SafeFileHandle handle;
        try
        {
            handle = _hooks?.Open?.Invoke(key) ?? RealPath.OpenForRead(key);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            // The file is missing (a dangling link included): the next candidate is tried
            return null;
        }
        catch (UnauthorizedAccessException ex) when (Directory.Exists(key))
        {
            // Windows refuses to open a directory as a file; it exists, but it is not a file
            _counted.Add(key);
            return Outcome.Failed(FileReferenceFailureKind.NotARegularFile, null, ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // Any other failure to open an existing file is a located read failure that ends the search
            _counted.Add(key);
            return Outcome.Failed(FileReferenceFailureKind.CannotRead, null, ex);
        }

        // From here on the file was opened: it counts, whatever the outcome
        _counted.Add(key);
        using (handle)
        {
            // The facts come from the handle, never from the name: whatever happens to the name after the open does
            // not change what the policy sees or what is read
            _hooks?.AfterOpen?.Invoke(key);
            var facts = RealPath.Inspect(handle);
            var realPath = _hooks?.RealPathOf is { } realPathOf ? realPathOf(handle) : facts.FinalPath;

            // A local path that leads to a network share (a link to a UNC target) is refused under every policy; a path
            // the host placed on a network drive or share itself is not. Where the real path is unknown, a restrictive
            // policy fails closed below, and the default policy reads the file
            if (!exempt && OperatingSystem.IsWindows() && realPath is not null
                && RealPath.IsNetworkLocation(realPath) && !RealPath.IsNetworkLocation(key))
            {
                return Outcome.Failed(FileReferenceFailureKind.NetworkPath, realPath, null);
            }

            // Only a handle known to be a regular file is read: a directory, a device, a pipe, or a handle whose type the
            // platform cannot tell is refused under every policy (fail closed)
            var isRegularFile = _hooks?.IsRegularFileOf is { } isRegularFileOf ? isRegularFileOf(handle) : facts.IsRegularFile;
            if (isRegularFile != true)
            {
                return Outcome.Failed(FileReferenceFailureKind.NotARegularFile, realPath, null);
            }

            // A restrictive policy is never consulted without the real path: unknown means refused (fail closed). The
            // top-level file is not judged by the policy at all
            if (!exempt && _policy.RequiresRealPath)
            {
                if (realPath is null)
                {
                    return Outcome.Failed(FileReferenceFailureKind.RealPathUnavailable, null, null);
                }

                CheckPolicy(candidate, shown, relativeTo, realPath);
            }

            var outcome = Read(handle, candidate, realPath);
            return exempt && outcome.File is not null ? outcome with { Exempt = true } : outcome;
        }
    }

    /// <summary>
    ///     Reads an approved file from its open handle.
    /// </summary>
    /// <param name="handle">The open handle; disposed by the caller.</param>
    /// <param name="candidate">The candidate: its key and the origin of its chain.</param>
    /// <param name="realPath">The real path, or <see langword="null"/>.</param>
    /// <returns>The outcome.</returns>
    private Outcome Read(SafeFileHandle handle, Candidate candidate, string? realPath)
    {
        // Once the file is open it exists: a failure while reading it - even one spelled "not found" - is a read failure
        try
        {
            using var stream = new FileStream(handle, FileAccess.Read, bufferSize: 1);
            var bytes = ReadBounded(stream, _options.MaxFileBytes, _cancellation);
            var key = candidate.Key;
            return bytes is null
                ? Outcome.Failed(FileReferenceFailureKind.TooLarge, realPath, null)
                : new Outcome(
                    new FileReference(key, realPath, Path.GetDirectoryName(key)!, bytes, false, candidate.Origin),
                    null,
                    realPath,
                    null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return Outcome.Failed(FileReferenceFailureKind.CannotRead, realPath, ex);
        }
    }

    /// <summary>
    ///     Asks the policy about an opened file and throws its denial.
    /// </summary>
    /// <param name="candidate">The candidate.</param>
    /// <param name="shown">The reference as shown in messages.</param>
    /// <param name="relativeTo">The referring file, or <see langword="null"/>.</param>
    /// <param name="realPath">The real path of the opened file.</param>
    /// <exception cref="FileReferenceException">Thrown when the policy denies the file, fails, or answers nothing.</exception>
    private void CheckPolicy(Candidate candidate, string shown, FileReference? relativeTo, string realPath)
    {
        var request = new FileAccessRequest
        {
            Reference = shown,
            Referrer = relativeTo?.Key ?? _options.ReferrerPath,
            BaseDirectory = candidate.Origin,
            RequestedPath = candidate.Key,
            RealPath = realPath,
        };

        // A policy is host code that may fail; a failure, or no answer, is a denial (fail closed). Only a cancellation
        // passes through
        FileAccessDecision? decision;
        Exception? failure = null;
        try
        {
            decision = _policy.Check(request);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            decision = null;
            failure = ex;
        }

        if (decision is not { IsAllowed: true })
        {
            throw Denied(shown, candidate.Key, realPath, decision, failure);
        }
    }

    /// <summary>
    ///     Builds the exception of a policy denial.
    /// </summary>
    /// <param name="shown">The reference as shown in messages.</param>
    /// <param name="key">The full path as written.</param>
    /// <param name="realPath">The real path, or <see langword="null"/> before the open.</param>
    /// <param name="decision">The denial, or <see langword="null"/> when the policy failed or answered nothing.</param>
    /// <param name="failure">The exception of a failed policy, or <see langword="null"/>.</param>
    /// <returns>The exception.</returns>
    private FileReferenceException Denied(
        string shown, string key, string? realPath, FileAccessDecision? decision, Exception? failure) =>
        new(FileReferenceFailureKind.AccessDenied, shown, decision?.Reason ?? "the file-access policy failed", failure)
        {
            RequestedPath = key,
            RealPath = realPath,
            PolicyName = _policy.Name,
            Hint = decision?.Hint,
        };

    /// <summary>
    ///     Turns a remembered outcome into the file or the failure it stands for.
    /// </summary>
    /// <param name="outcome">The outcome.</param>
    /// <param name="shown">The reference as shown in messages.</param>
    /// <param name="key">The key.</param>
    /// <returns>The file.</returns>
    /// <exception cref="FileReferenceException">Thrown when the outcome is a failure.</exception>
    private FileReference Deliver(Outcome outcome, string shown, string key)
    {
        if (outcome.File is { } file)
        {
            return file;
        }

        var kind = outcome.Failure!.Value;
        var reason = kind switch
        {
            FileReferenceFailureKind.RealPathUnavailable => "the real path of the opened file cannot be determined on this system",
            FileReferenceFailureKind.NotARegularFile => "it is not a regular file",
            FileReferenceFailureKind.NetworkPath => "it leads to a network location",
            _ => null,
        };
        throw new FileReferenceException(kind, shown, reason, outcome.Cause)
        {
            RequestedPath = key,
            RealPath = outcome.RealPath,
            PolicyName = kind == FileReferenceFailureKind.RealPathUnavailable ? _policy.Name : null,
        };
    }

    /// <summary>
    ///     Refuses a new key once the file limit is reached, before it is opened or requested; a key that was already
    ///     opened or requested never counts again.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="shown">The reference as shown in messages.</param>
    /// <exception cref="FileReferenceException">Thrown when the limit is reached.</exception>
    private void EnforceFileLimit(string key, string shown)
    {
        if (_options.MaxFiles is { } maxFiles && _counted.Count >= maxFiles && !_counted.Contains(key))
        {
            throw new FileReferenceException(FileReferenceFailureKind.TooManyFiles, shown) { RequestedPath = key };
        }
    }

    /// <summary>
    ///     Loads a logical candidate from the resolver callback, through the cache and the file limit.
    /// </summary>
    /// <param name="candidate">The candidate.</param>
    /// <param name="shown">The reference as shown in messages.</param>
    /// <returns>The file, or <see langword="null"/> when the callback has no such file.</returns>
    private FileReference? LoadLogical(Candidate candidate, string shown)
    {
        if (!_outcomes.TryGetValue(candidate.Key, out var outcome))
        {
            EnforceFileLimit(candidate.Key, shown);
            _cancellation.ThrowIfCancellationRequested();
            outcome = ReadFromCallback(candidate.Key);
            _outcomes[candidate.Key] = outcome;
            if (outcome is not null)
            {
                _counted.Add(candidate.Key);
            }
        }

        return outcome is null ? null : Deliver(outcome, shown, candidate.Key);
    }

    /// <summary>
    ///     Reads a logical file from the resolver callback.
    /// </summary>
    /// <remarks>
    ///     The callback, the bounded read and the disposal of the stream form one boundary: any failure other than a
    ///     cancellation is <see cref="FileReferenceFailureKind.CannotRead"/>, because the callback is untrusted code that
    ///     must not leak a raw exception, or forge another failure kind, through the contract.
    /// </remarks>
    /// <param name="key">The logical path.</param>
    /// <returns>The outcome, or <see langword="null"/> when the callback has no such file.</returns>
    private Outcome? ReadFromCallback(string key)
    {
        byte[]? bytes;
        try
        {
            using var stream = _options.FileResolver!(key);
            if (stream is null)
            {
                return null;
            }

            bytes = ReadBounded(stream, _options.MaxFileBytes, _cancellation);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Outcome.Failed(FileReferenceFailureKind.CannotRead, null, ex);
        }

        var slash = key.LastIndexOf('/');
        var directory = slash < 0 ? string.Empty : key[..slash];
        return bytes is null
            ? Outcome.Failed(FileReferenceFailureKind.TooLarge, null, null)
            : new Outcome(new FileReference(key, null, directory, bytes, true, origin: null), null, null, null);
    }

    /// <summary>
    ///     What was learned about one key.
    /// </summary>
    /// <param name="File">The file, when it was read.</param>
    /// <param name="Failure">The failure, when it could not be delivered.</param>
    /// <param name="RealPath">The real path of the opened file, when known.</param>
    /// <param name="Cause">The underlying exception of a failure, if any.</param>
    private sealed record Outcome(FileReference? File, FileReferenceFailureKind? Failure, string? RealPath, Exception? Cause)
    {
        /// <summary>
        ///     Gets a value indicating whether the file was read as the top-level file, without the network-target check
        ///     and the policy; a later reference to it applies them.
        /// </summary>
        public bool Exempt { get; init; }

        /// <summary>
        ///     Creates the outcome of a failure.
        /// </summary>
        /// <param name="kind">The failure kind.</param>
        /// <param name="realPath">The real path, when known.</param>
        /// <param name="cause">The underlying exception, if any.</param>
        /// <returns>The outcome.</returns>
        public static Outcome Failed(FileReferenceFailureKind kind, string? realPath, Exception? cause) =>
            new(null, kind, realPath, cause);
    }
}
