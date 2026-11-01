namespace DemaConsulting.CanvasNet.Files;

/// <summary>
///     Describes where a <see cref="SandboxedFileResolver"/> may look for files and how large a file may be.
/// </summary>
/// <remarks>
///     <para>
///         There are two mutually exclusive ways to find files. Without <see cref="FileResolver"/> the file system is
///         searched: first <see cref="BaseDirectory"/>, then each entry of <see cref="SearchPaths"/>. With a
///         <see cref="FileResolver"/> the file system is never touched: the callback is asked for normalized logical
///         paths instead, and <see cref="BaseDirectory"/> plays no role.
///     </para>
///     <para>The record is immutable once built and may be shared by any number of resolvers and threads.</para>
/// </remarks>
public sealed record SandboxOptions
{
    /// <summary>
    ///     The default value of <see cref="MaxFileBytes"/>: 16 MiB.
    /// </summary>
    public const long DefaultMaxFileBytes = 16L * 1024 * 1024;

    /// <summary>
    ///     Gets the directory a relative path is resolved against first (file-system mode), or
    ///     <see langword="null"/> for none. It is trusted: a link at or above it is followed. Ignored in
    ///     resolver-callback mode.
    /// </summary>
    public string? BaseDirectory { get; init; }

    /// <summary>
    ///     Gets the additional directories searched, in order, after the base directory (file-system mode). Each is
    ///     trusted as a root and confines the files found under it. In resolver-callback mode each entry that is itself
    ///     a valid relative path is used as a logical directory prefix and the others are ignored. Must not be null.
    /// </summary>
    public IReadOnlyList<string> SearchPaths { get; init; } = [];

    /// <summary>
    ///     Gets the callback that supplies files instead of the file system, or <see langword="null"/> to use the
    ///     file system. It receives a normalized logical path (forward slashes, no leading slash, <c>.</c> and
    ///     <c>..</c> collapsed) and returns a readable stream, or <see langword="null"/> when it has no such file. The
    ///     stream is disposed by the resolver. The callback is invoked on the calling thread, at most once per
    ///     distinct logical path per resolver.
    /// </summary>
    public Func<string, Stream?>? FileResolver { get; init; }

    /// <summary>
    ///     Gets the largest file, in bytes, that may be read. A longer file is refused with
    ///     <see cref="SandboxFailureKind.TooLarge"/>; reading stops one chunk past the limit, so a stream that never
    ///     ends cannot exhaust memory. Must be positive. The default is <see cref="DefaultMaxFileBytes"/>.
    /// </summary>
    public long MaxFileBytes { get; init; } = DefaultMaxFileBytes;
}
