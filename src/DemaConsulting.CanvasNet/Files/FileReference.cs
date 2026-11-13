namespace DemaConsulting.CanvasNet.Files;

/// <summary>
///     A referenced file that <see cref="FileReferenceResolver"/> found, checked and read: its identity, its real
///     location, its directory and its bytes.
/// </summary>
/// <remarks>
///     The same instance is returned every time the same file is resolved again by one resolver, so reference
///     equality (or <see cref="Key"/> equality under <see cref="FileReferenceResolver.KeyComparer"/>) identifies a file
///     reached through different spellings of one path. Two different paths that reach one file through a link have
///     different keys but the same <see cref="RealPath"/>, which is the identity to use for cycle detection. The type
///     is immutable; <see cref="Content"/> is shared and must not be modified by unsafe means. It is safe to use from
///     any thread.
/// </remarks>
public sealed class FileReference
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="FileReference"/> class.
    /// </summary>
    /// <param name="key">The identity of the file.</param>
    /// <param name="realPath">The real path, or <see langword="null"/>.</param>
    /// <param name="directory">The directory of the file.</param>
    /// <param name="content">The bytes of the file.</param>
    /// <param name="isLogical">Whether the file came from a resolver callback.</param>
    /// <param name="origin">The host-chosen directory the chain of references started from, or <see langword="null"/> in resolver-callback mode.</param>
    internal FileReference(
        string key, string? realPath, string directory, ReadOnlyMemory<byte> content, bool isLogical, string? origin)
    {
        Key = key;
        RealPath = realPath;
        Directory = directory;
        Content = content;
        IsLogical = isLogical;
        Origin = origin;
    }

    /// <summary>
    ///     Gets the identity of the file: its full path as written (<c>.</c> and <c>..</c> collapsed, links not
    ///     resolved) in file-system mode, or its normalized logical path in resolver-callback mode. It may reveal the
    ///     host's directories.
    /// </summary>
    public string Key { get; }

    /// <summary>
    ///     Gets the real path of the file as reported for the open handle (links, junctions and short names resolved),
    ///     or <see langword="null"/> in resolver-callback mode and where the platform cannot tell. It may reveal the
    ///     host's directories.
    /// </summary>
    public string? RealPath { get; }

    /// <summary>
    ///     Gets the directory of the file as written (a full path, or a logical path in resolver-callback mode; the
    ///     empty string for a logical file at the root). Pass the file itself to
    ///     <see cref="FileReferenceResolver.Resolve(string, FileReference?)"/> to resolve a reference relative to it.
    /// </summary>
    public string Directory { get; }

    /// <summary>
    ///     Gets the bytes of the file, at most <see cref="FileReferenceOptions.MaxFileBytes"/> of them. Decoding them to
    ///     text is left to the caller, which knows the expected encoding.
    /// </summary>
    public ReadOnlyMemory<byte> Content { get; }

    /// <summary>
    ///     Gets a value indicating whether the file came from a resolver callback (logical paths) rather than the file
    ///     system.
    /// </summary>
    internal bool IsLogical { get; }

    /// <summary>
    ///     Gets the full path of the host-chosen base directory or search path where the chain of references that
    ///     reached this file started (file-system mode), or <see langword="null"/> in resolver-callback mode. It is
    ///     trusted: it never comes from a document.
    /// </summary>
    internal string? Origin { get; }
}
