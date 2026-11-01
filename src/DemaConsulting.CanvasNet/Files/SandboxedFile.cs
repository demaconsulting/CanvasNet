namespace DemaConsulting.CanvasNet.Files;

/// <summary>
///     A file that <see cref="SandboxedFileResolver"/> found and read: its identity, where it lives and its bytes.
/// </summary>
/// <remarks>
///     The same instance is returned every time the same candidate is resolved again by one resolver, so reference
///     equality (or <see cref="Key"/> equality under <see cref="SandboxedFileResolver.KeyComparer"/>) identifies a
///     file that was reached through different spellings. The type is immutable; <see cref="Content"/> is shared and
///     must not be modified by unsafe means. It is safe to use from any thread.
/// </remarks>
public sealed class SandboxedFile
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SandboxedFile"/> class.
    /// </summary>
    /// <param name="key">The identity of the file.</param>
    /// <param name="directory">The directory of the file.</param>
    /// <param name="root">The root the file is confined to.</param>
    /// <param name="content">The bytes of the file.</param>
    internal SandboxedFile(string key, string directory, string root, ReadOnlyMemory<byte> content)
    {
        Key = key;
        Directory = directory;
        Root = root;
        Content = content;
    }

    /// <summary>
    ///     Gets the identity of the file: its normalized full path (compared under
    ///     <see cref="SandboxedFileResolver.KeyComparer"/>) in file-system mode, or its normalized logical path in
    ///     resolver-callback mode. It may reveal the host's directories.
    /// </summary>
    public string Key { get; }

    /// <summary>
    ///     Gets the directory of the file (a full path, or a logical path in resolver-callback mode; the empty string
    ///     for a logical file at the root). Pass the file itself to
    ///     <see cref="SandboxedFileResolver.Resolve(string, SandboxedFile?)"/> to resolve a path relative to it.
    /// </summary>
    public string Directory { get; }

    /// <summary>
    ///     Gets the root the file was found under and is confined to (the base directory or one search path; a
    ///     logical prefix in resolver-callback mode).
    /// </summary>
    public string Root { get; }

    /// <summary>
    ///     Gets the bytes of the file, at most <see cref="SandboxOptions.MaxFileBytes"/> of them. Decoding them
    ///     to text is left to the caller, which knows the expected encoding.
    /// </summary>
    public ReadOnlyMemory<byte> Content { get; }
}
