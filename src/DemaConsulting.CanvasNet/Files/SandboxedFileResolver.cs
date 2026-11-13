using Microsoft.Win32.SafeHandles;

namespace DemaConsulting.CanvasNet.Files;

/// <summary>
///     Resolves an untrusted relative path to the bytes of a file that is guaranteed to lie inside caller-chosen
///     directories (or a caller-supplied virtual file source), and refuses everything else.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why it exists.</b> Programs that read files named by untrusted text (an include directive, a data URL)
///         must not let that text reach outside the directories the host chose. This type is the single, tested
///         implementation of that rule, so a host does not have to re-derive it: it validates the path text, confines
///         every candidate to its root, refuses symbolic links and junctions, opens files without racing against a
///         separate existence check, bounds the bytes read, and converts every failure into a
///         <see cref="SandboxException"/> that the caller can map to its own error type.
///     </para>
///     <para>
///         <b>Search order.</b> <see cref="Resolve(string, SandboxedFile?)"/> looks first in the directory of the file
///         the path is relative to (when one is given), otherwise in <see cref="SandboxOptions.BaseDirectory"/>, and then
///         under each entry of <see cref="SandboxOptions.SearchPaths"/> in declaration order; the first candidate that
///         exists wins and later roots are only examined on a miss. With a <see cref="SandboxOptions.FileResolver"/> the
///         file system is never touched and the callback is asked for normalized logical paths instead.
///     </para>
///     <para>
///         <b>Caching.</b> Each distinct candidate (a file together with the root it is confined to) is looked up at
///         most once per instance (a miss is remembered too, so
///         a callback sees each logical path at most once - whichever root it is reached through - and a file changed
///         after its first read is not read again).
///         The root is part of the identity because the link checks are relative to it: a file reached through one root
///         never answers for another (in callback mode there are no links, so only the callback's answer is shared
///         and each result still carries the root it was found under). An instance therefore serves one logical operation (one render, one document) and
///         is then dropped. When <see cref="SandboxOptions.MaxFiles"/> is set, the distinct files that exist are counted
///         (a file that is too large included, which is remembered so it is never opened twice) and
///         a new file beyond the limit is refused before it is opened or requested.
///     </para>
///     <para>
///         <b>Thread safety.</b> An instance is <b>not</b> thread-safe: it keeps unsynchronized per-instance caches and
///         invokes the resolver callback on the calling thread. Use one instance per operation, or serialize access.
///         <see cref="SandboxOptions"/> and the returned <see cref="SandboxedFile"/> values are immutable and may be
///         shared freely. The link walk runs before the open, and the open handle is verified before a single byte is
///         read: it must be a regular file whose real path (asked of the operating system for the handle, not the name)
///         is the canonical root followed by the path below the root. A link that another process swaps into the tree
///         between the walk and the open is therefore refused as well, and where the platform cannot tell the real path
///         of an open file the file is refused (<see cref="SandboxFailureKind.LinkCheckFailed"/>, fail closed).
///     </para>
///     <para>
///         <b>Rules.</b> An empty path, a control character, a colon and a leading slash are refused, a <c>..</c> that
///         would leave the root is refused, and every component below the root, including the file itself, must not be a
///         symbolic link or junction, whatever its target (the root and everything above it are trusted because the
///         caller chose them). A caller who wants to use linked content adds the real directory as a search path. A
///         directory where a file is expected, anything else that is not a regular file (a device, a named pipe), an
///         open that fails, and a callback that throws are
///         <see cref="SandboxFailureKind.CannotRead"/>, never "not found": only a file that does not exist yields
///         <see langword="null"/>. Files are opened read-only with shared access (and without blocking on Linux and
///         macOS), so concurrent readers, writers and rename or delete operations by other processes do not block the
///         read, and a named pipe cannot hang it.
///     </para>
/// </remarks>
public sealed partial class SandboxedFileResolver
{
    /// <summary>The effective options.</summary>
    private readonly SandboxOptions _options;

    /// <summary>A snapshot of <see cref="SandboxOptions.SearchPaths"/> taken at construction.</summary>
    private readonly string[] _searchPaths;

    /// <summary>The cancellation token polled between files and chunks.</summary>
    private readonly CancellationToken _cancellation;

    /// <summary>The opener of a file-system candidate as a stream: a test seam; <see langword="null"/> uses the verified handle path.</summary>
    private readonly Func<string, Stream>? _openFile;

    /// <summary>The test seams around the verified open, or <see langword="null"/>.</summary>
    private readonly FileOpenHooks? _hooks;

    /// <summary>The resolved files by root and candidate key; <see langword="null"/> remembers a candidate that does not exist.</summary>
    private readonly Dictionary<string, SandboxedFile?> _files;

    /// <summary>
    ///     What was read for a candidate, so nothing is opened or requested twice: by root and key in file-system mode, but
    ///     by logical key alone in callback mode (the callback must see each logical path at most once, whichever root it
    ///     was reached through). <see langword="null"/> remembers a candidate that does not exist; an entry without bytes
    ///     remembers a file that is too large.
    /// </summary>
    private readonly Dictionary<string, Content?> _contents;

    /// <summary>
    ///     The outcome of reading one candidate that was opened or requested.
    /// </summary>
    /// <param name="Bytes">The bytes, or <see langword="null"/> when the file is too large or could not be read.</param>
    /// <param name="Unreadable"><see langword="true"/> when reading failed (a <c>CannotRead</c> failure).</param>
    /// <param name="Cause">The cause of the read failure, if any.</param>
    private sealed record Content(byte[]? Bytes, bool Unreadable = false, Exception? Cause = null);

    /// <summary>The keys of the distinct files read so far, counted against <see cref="SandboxOptions.MaxFiles"/>.</summary>
    private readonly HashSet<string> _readKeys;

    /// <summary>
    ///     Initializes a resolver for one operation.
    /// </summary>
    /// <param name="options">Where files may be found and how large they may be. Must not be null.</param>
    /// <param name="cancellationToken">The token polled before each file is read and after each chunk.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or its <see cref="SandboxOptions.SearchPaths"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="SandboxOptions.MaxFileBytes"/> is not positive or <see cref="SandboxOptions.MaxFiles"/> is negative.</exception>
    public SandboxedFileResolver(SandboxOptions options, CancellationToken cancellationToken = default)
        : this(options, cancellationToken, null, null)
    {
    }

    /// <summary>
    ///     Initializes a resolver with test seams, so the link and open rules can be proven without any symbolic-link
    ///     privilege or file-system permission.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="isLink">The probe that tells whether a full path is a symbolic link or junction; <see langword="null"/> asks the file system.</param>
    /// <param name="openFile">
    ///     The opener of a file-system candidate for reading; <see langword="null"/> opens it read-only with shared
    ///     access and verifies the open handle. An injected opener returns a stream, which has no handle to verify, so
    ///     the post-open verification is skipped for it.
    /// </param>
    internal SandboxedFileResolver(
        SandboxOptions options,
        CancellationToken cancellationToken,
        Func<string, bool>? isLink,
        Func<string, Stream>? openFile)
        : this(options, cancellationToken, isLink, openFile, null)
    {
    }

    /// <summary>
    ///     Initializes a resolver with test seams, including the seams around the verified open, so the post-open
    ///     verification can be proven without timing.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="isLink">The link probe, or <see langword="null"/>.</param>
    /// <param name="openFile">The stream opener, or <see langword="null"/> for the verified handle path.</param>
    /// <param name="hooks">The seams around the verified open, or <see langword="null"/>.</param>
    internal SandboxedFileResolver(
        SandboxOptions options,
        CancellationToken cancellationToken,
        Func<string, bool>? isLink,
        Func<string, Stream>? openFile,
        FileOpenHooks? hooks)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.SearchPaths);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.MaxFileBytes, 0L);
        ArgumentOutOfRangeException.ThrowIfNegative(options.MaxFiles ?? 0);
        _options = options;

        // The caller's list may be mutable; a later change to it must not widen or reorder the roots of a running resolver
        _searchPaths = options.SearchPaths.ToArray();
        _cancellation = cancellationToken;
        _isLink = isLink ?? DefaultIsLink;
        _openFile = openFile;
        _hooks = hooks;
        KeyComparer = options.FileResolver is null ? PlatformKeyComparer : StringComparer.Ordinal;
        _files = new Dictionary<string, SandboxedFile?>(KeyComparer);
        _contents = new Dictionary<string, Content?>(KeyComparer);
        _readKeys = new HashSet<string>(KeyComparer);
        _linkStates = new Dictionary<string, bool>(KeyComparer);
        _linkFailures = new Dictionary<string, Exception>(KeyComparer);
    }

    /// <summary>
    ///     Gets the comparer under which two <see cref="SandboxedFile.Key"/> values name the same file: ordinal for
    ///     logical paths, and the platform's path comparison (case-insensitive on Windows and macOS) for full paths.
    ///     Use it to detect that two spellings reached one file, for example in an include-cycle check.
    /// </summary>
    public StringComparer KeyComparer { get; }

    /// <summary>
    ///     Gets a value indicating whether at least one way of finding files is configured: a resolver callback, a base
    ///     directory or a search path.
    /// </summary>
    /// <remarks>
    ///     <see cref="Resolve(string, SandboxedFile?)"/> returns <see langword="null"/> when this is
    ///     <see langword="false"/>; a caller that wants "no file source" to be an error of its own checks this first.
    /// </remarks>
    public bool HasSource =>
        _options.FileResolver is not null || _options.BaseDirectory is not null || _searchPaths.Length > 0;

    /// <summary>
    ///     Finds and reads the file a relative path names.
    /// </summary>
    /// <param name="path">
    ///     The untrusted path as written; must be a relative path. Backslashes are treated as forward slashes, so a
    ///     path means the same on every platform.
    /// </param>
    /// <param name="relativeTo">
    ///     A file previously returned by this resolver whose directory is searched first (and which confines that
    ///     candidate to its <see cref="SandboxedFile.Root"/>), or <see langword="null"/> to start from the base
    ///     directory. Must come from this resolver (or one with the same roots); otherwise it is rejected.
    /// </param>
    /// <returns>The file, or <see langword="null"/> when no candidate exists (or no file source is configured).</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="relativeTo"/> was not resolved through one of this resolver's own roots (for
    ///     example a file returned by another resolver with wider roots): its directory and root are trusted only when
    ///     they match the roots this resolver was configured with.
    /// </exception>
    /// <exception cref="SandboxException">
    ///     Thrown when the path text is not allowed, a <c>..</c> leaves the root, a component is a link or cannot be
    ///     checked, a configured directory is invalid, the file exists but cannot be read or is too large, or reading it
    ///     would exceed <see cref="SandboxOptions.MaxFiles"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when the cancellation token is canceled.</exception>
    public SandboxedFile? Resolve(string path, SandboxedFile? relativeTo = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        _cancellation.ThrowIfCancellationRequested();

        // The path text is validated before anything is touched, so a hostile path never causes any file access or callback
        var text = ValidatePathText(path);

        // A file the resolver did not hand out (or hands out for another set of roots) is not a trustworthy starting point
        if (relativeTo is not null)
        {
            EnsureRoots();
            ValidateRelativeTo(relativeTo);
        }

        // Candidates are produced lazily: the first one that exists wins, so later roots are only examined on a miss
        foreach (var candidate in Candidates(text, relativeTo))
        {
            var file = LoadCandidate(candidate);
            if (file is not null)
            {
                return file;
            }
        }

        return null;
    }

    /// <summary>
    ///     Loads a candidate through the cache.
    /// </summary>
    /// <param name="candidate">The candidate.</param>
    /// <returns>The file, or <see langword="null"/> when the candidate does not exist.</returns>
    private SandboxedFile? LoadCandidate(Candidate candidate)
    {
        // The cache identity includes the root: the link checks are relative to the root a candidate is confined to, so a
        // file reached through one root (for example a trusted, linked search path) must never answer for another root
        var cacheKey = candidate.Root + '\0' + candidate.Key;
        if (_files.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        // What was read is shared across roots only in callback mode, where there are no links to check and the callback
        // must see each logical path once; the SandboxedFile built from it still carries the root it was found under
        var contentKey = UsesResolver ? candidate.Key : cacheKey;
        if (!_contents.TryGetValue(contentKey, out var content))
        {
            // The file limit is enforced before anything is looked up, so a file beyond it is never opened or requested;
            // a file that was already read (through any root) does not count again
            if (_options.MaxFiles is { } maxFiles && _readKeys.Count >= maxFiles && !_readKeys.Contains(candidate.Key))
            {
                throw new SandboxException(SandboxFailureKind.TooManyFiles, candidate.Key);
            }

            _cancellation.ThrowIfCancellationRequested();
            try
            {
                content = ReadBytes(candidate);
            }
            catch (SandboxException ex) when (ex.Kind == SandboxFailureKind.CannotRead)
            {
                // The file was opened or requested and failed: remember it like a size failure, so it is not tried again
                content = new Content(null, true, ex.InnerException);
            }

            _contents[contentKey] = content;

            // A file counts once it was opened or requested, whatever the outcome (a miss is the only thing that does not)
            if (content is not null)
            {
                _readKeys.Add(candidate.Key);
            }
        }

        // A file that is too large or could not be read is remembered too, so asking again fails the same way without
        // opening or requesting it again
        if (content is { Bytes: null })
        {
            throw content.Unreadable
                ? new SandboxException(SandboxFailureKind.CannotRead, candidate.Key, innerException: content.Cause)
                : new SandboxException(SandboxFailureKind.TooLarge, candidate.Key);
        }

        var file = content is null ? null : new SandboxedFile(candidate.Key, DirectoryOf(candidate.Key), candidate.Root, content.Bytes);
        _files[cacheKey] = file;
        return file;
    }

    /// <summary>
    ///     Reads the bytes of a candidate from the resolver callback or the file system.
    /// </summary>
    /// <param name="candidate">The candidate.</param>
    /// <returns>What was read, or <see langword="null"/> when the candidate does not exist.</returns>
    private Content? ReadBytes(Candidate candidate)
    {
        if (UsesResolver)
        {
            return ReadFromCallback(candidate);
        }

        // The walk runs before the file is opened, so nothing is ever read through a link and a file behind a link is
        // never reported as existing. A missing intermediate component ends the candidate there; the final component is
        // not decided by an existence probe, because a probe reports "missing" for an existing file it cannot access
        if (!WalkRejectingLinks(candidate))
        {
            return null;
        }

        // A directory where a file is expected is not a missing file: it exists but cannot be read as data
        if (Directory.Exists(candidate.Key))
        {
            throw new SandboxException(SandboxFailureKind.CannotRead, candidate.Key);
        }

        // An injected stream opener (a test seam) has no handle to verify; the default path opens a handle and
        // verifies it before reading, which closes the race between the link walk and the open
        return _openFile is null ? ReadVerified(candidate) : ReadFromStream(candidate, _openFile);
    }

    /// <summary>
    ///     Opens a candidate, verifies the open handle and reads it: the handle must be a regular file whose real path
    ///     is the candidate's path below the canonical root, so a link swapped in after the link walk is refused.
    /// </summary>
    /// <param name="candidate">The candidate; its link walk has passed.</param>
    /// <returns>What was read, or <see langword="null"/> when the candidate does not exist.</returns>
    /// <exception cref="SandboxException">
    ///     Thrown with <see cref="SandboxFailureKind.CannotRead"/> when the open or the read fails or the file is not a
    ///     regular file, <see cref="SandboxFailureKind.LinkCheckFailed"/> when the real path cannot be determined, and
    ///     <see cref="SandboxFailureKind.ThroughLink"/> when the real path shows a link below the root.
    /// </exception>
    private Content? ReadVerified(Candidate candidate)
    {
        // The open attempt decides, and only the open: "not found" from the open means the candidate is missing
        _hooks?.BeforeOpen?.Invoke(candidate.Key);
        SafeFileHandle handle;
        try
        {
            handle = _hooks?.Open?.Invoke(candidate.Key) ?? RealPath.OpenForRead(candidate.Key);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            // The file is missing (or vanished since the walk): the next candidate is tried
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // Any other failure to open the file (sharing, access, path too long, device) is a located read failure
            throw new SandboxException(SandboxFailureKind.CannotRead, candidate.Key, innerException: ex);
        }

        using (handle)
        {
            // The facts come from the handle, never from the name, so a change of the tree after the walk cannot slip in
            _hooks?.AfterOpen?.Invoke(candidate.Key);
            var facts = RealPath.Inspect(handle);
            var realPath = _hooks?.RealPathOf is { } realPathOf ? realPathOf(handle) : facts.FinalPath;
            if (facts.IsRegularFile == false)
            {
                throw new SandboxException(SandboxFailureKind.CannotRead, candidate.Key);
            }

            // The link rule needs the real path; when the platform cannot tell it, the file is refused (fail closed)
            if (realPath is null)
            {
                throw new SandboxException(SandboxFailureKind.LinkCheckFailed, candidate.Key);
            }

            // Anchored at the root, the no-symlinks rule is exactly the walk's rule: the real path must be the canonical
            // root followed by the path below the root
            var relative = Path.GetRelativePath(candidate.Root, candidate.Key).Replace('\\', '/');
            var decision = FileAccessPolicy.NoSymlinks.Check(new FileAccessRequest
            {
                Reference = relative,
                BaseDirectory = candidate.Root,
                RequestedPath = candidate.Key,
                RealPath = realPath,
            });
            if (!decision.IsAllowed)
            {
                throw new SandboxException(SandboxFailureKind.ThroughLink, candidate.Key, relative);
            }

            return ReadFromStream(candidate, _ => new FileStream(handle, FileAccess.Read, bufferSize: 1));
        }
    }

    /// <summary>
    ///     Reads a candidate from a stream that an opener supplies.
    /// </summary>
    /// <param name="candidate">The candidate.</param>
    /// <param name="open">The opener; "not found" from it means the candidate is missing.</param>
    /// <returns>What was read, or <see langword="null"/> when the candidate does not exist.</returns>
    /// <exception cref="SandboxException">Thrown with <see cref="SandboxFailureKind.CannotRead"/> when the open or the read fails.</exception>
    private Content? ReadFromStream(Candidate candidate, Func<string, Stream> open)
    {
        // The open attempt decides, and only the open: "not found" from the open means the candidate is missing
        Stream file;
        try
        {
            file = open(candidate.Key);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            // The file is missing (or vanished since the walk): the next candidate is tried
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // Any other failure to open the file (sharing, access, path too long, device) is a located read failure
            throw new SandboxException(SandboxFailureKind.CannotRead, candidate.Key, innerException: ex);
        }

        // Once the file is open it exists: a failure while reading or closing it - even one that looks like "not found" -
        // is a read failure that stops the search, never a missing candidate
        try
        {
            using (file)
            {
                return new Content(ReadBounded(file));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new SandboxException(SandboxFailureKind.CannotRead, candidate.Key, innerException: ex);
        }
    }

    /// <summary>
    ///     Reads the bytes of a candidate from the resolver callback.
    /// </summary>
    /// <remarks>
    ///     The callback, the bounded read and the disposal of the returned stream form one boundary: a failure of any of
    ///     them - including a <see cref="SandboxException"/> that the callback itself throws - is a
    ///     <see cref="SandboxFailureKind.CannotRead"/> failure (a cancellation excepted), because the callback is untrusted
    ///     code that must not leak a raw exception, or forge another failure kind, through the sandbox contract. The
    ///     size failure is raised by this resolver outside that boundary.
    /// </remarks>
    /// <param name="candidate">The candidate.</param>
    /// <returns>What was read, or <see langword="null"/> when the callback has no such file.</returns>
    private Content? ReadFromCallback(Candidate candidate)
    {
        byte[]? bytes;
        try
        {
            // The callback owns what it returns until it is handed to us; we dispose it exactly once after reading
            using var stream = _options.FileResolver!(candidate.Key);
            if (stream is null)
            {
                return null;
            }

            bytes = ReadBounded(stream);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Everything the callback or its stream raises - even a SandboxException of its own - is a read failure
            throw new SandboxException(SandboxFailureKind.CannotRead, candidate.Key, innerException: ex);
        }

        // The size outcome is decided here, outside the callback boundary, so the callback can never forge it
        return new Content(bytes);
    }

    /// <summary>
    ///     Reads a whole stream as bytes under the size limit (see <see cref="FileReferenceResolver.ReadBounded"/>).
    /// </summary>
    /// <param name="stream">The stream; it is left open.</param>
    /// <returns>The bytes, or <see langword="null"/> when the stream is longer than the size limit.</returns>
    private byte[]? ReadBounded(Stream stream) =>
        FileReferenceResolver.ReadBounded(stream, _options.MaxFileBytes, _cancellation);
}
