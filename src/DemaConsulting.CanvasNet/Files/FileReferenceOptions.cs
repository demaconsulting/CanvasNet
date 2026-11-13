namespace DemaConsulting.CanvasNet.Files;

/// <summary>
///     Describes where a <see cref="FileReferenceResolver"/> looks for referenced files, which of them it may read, and
///     how large and how many they may be.
/// </summary>
/// <remarks>
///     <para>
///         There are two mutually exclusive ways to find files. Without <see cref="FileResolver"/> the file system is
///         used: a relative reference is looked up in the referring file's directory (or <see cref="BaseDirectory"/>)
///         and then in each entry of <see cref="SearchPaths"/>; an absolute reference names exactly one file. Every
///         file that is found is checked by <see cref="FileAccess"/> after it was opened. With a
///         <see cref="FileResolver"/> the file system is never touched and the policy is not consulted: the callback
///         is asked for normalized logical paths and is the policy.
///     </para>
///     <para>The record is immutable once built and may be shared by any number of resolvers and threads.</para>
/// </remarks>
public sealed record FileReferenceOptions
{
    /// <summary>
    ///     The default value of <see cref="MaxFileBytes"/>: 16 MiB.
    /// </summary>
    public const long DefaultMaxFileBytes = 16L * 1024 * 1024;

    /// <summary>
    ///     Gets the directory a relative reference of the top-level document is resolved against, or
    ///     <see langword="null"/> for none. It is a starting point, not a boundary: <c>..</c> and absolute references
    ///     may leave it, subject to <see cref="FileAccess"/>. Ignored in resolver-callback mode.
    /// </summary>
    public string? BaseDirectory { get; init; }

    /// <summary>
    ///     Gets the full path of the top-level referring document, or <see langword="null"/> when it has no file. It is
    ///     passed to the policy as <see cref="FileAccessRequest.Referrer"/> for references of that document.
    /// </summary>
    public string? ReferrerPath { get; init; }

    /// <summary>
    ///     Gets the additional directories searched, in order, for a relative reference that is not found next to the
    ///     referring document. They are search locations only, not roots: to confine reads to them, add them to
    ///     <see cref="FileAccessPolicy.UnderRoots(IEnumerable{string})"/>. In resolver-callback mode each entry that is
    ///     itself a valid relative logical path is used as a logical directory prefix and the others are ignored. Must
    ///     not be null.
    /// </summary>
    public IReadOnlyList<string> SearchPaths { get; init; } = [];

    /// <summary>
    ///     Gets the callback that supplies files instead of the file system, or <see langword="null"/> to use the file
    ///     system. It receives a normalized logical path (forward slashes, no leading slash, <c>.</c> and <c>..</c>
    ///     collapsed, never above the logical root) and returns a readable stream, or <see langword="null"/> when it has
    ///     no such file. The stream is disposed by the resolver. The callback is invoked on the calling thread, at most
    ///     once per distinct logical path per resolver. <see cref="FileAccess"/> is not consulted in this mode.
    /// </summary>
    public Func<string, Stream?>? FileResolver { get; init; }

    /// <summary>
    ///     Gets the policy that decides whether an opened file may be read. The default,
    ///     <see cref="FileAccessPolicy.AllowAll"/>, reads every regular local file. Must not be null.
    /// </summary>
    public FileAccessPolicy FileAccess { get; init; } = FileAccessPolicy.AllowAll;

    /// <summary>
    ///     Gets the largest file, in bytes, that may be read, whatever the policy. A longer file is refused with
    ///     <see cref="FileReferenceFailureKind.TooLarge"/>; reading stops one chunk past the limit, so a stream that
    ///     never ends cannot exhaust memory. Must be positive. The default is <see cref="DefaultMaxFileBytes"/>.
    /// </summary>
    public long MaxFileBytes { get; init; } = DefaultMaxFileBytes;

    /// <summary>
    ///     Gets the largest number of distinct files one resolver may open or request, whatever the policy, or
    ///     <see langword="null"/> (the default) for no limit. Once reached, a file that is not yet known is refused with
    ///     <see cref="FileReferenceFailureKind.TooManyFiles"/> before it is opened or requested. A file counts once it
    ///     was opened or requested, whatever the outcome (denied, too large, not a regular file, unreadable); a file
    ///     that does not exist does not count. Must not be negative.
    /// </summary>
    public int? MaxFiles { get; init; }
}
