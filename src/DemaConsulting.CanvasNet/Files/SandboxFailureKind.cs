namespace DemaConsulting.CanvasNet.Files;

/// <summary>
///     Identifies why <see cref="SandboxedFileResolver"/> refused a path or could not deliver a file.
/// </summary>
/// <remarks>
///     A caller branches on this value (never on the message text) to decide how to report the failure; the
///     <see cref="SandboxException.Path"/> and <see cref="SandboxException.Detail"/> of the exception carry the
///     kind-specific facts. The first four values are problems with the path text itself and are detected before
///     any file or callback is touched.
///     <para>
///         <b>Obsolete.</b> The folder sandbox is kept, unchanged and race-free, for one release; use
///         <see cref="FileReferenceResolver"/> with <c>FileAccessPolicy.Combine(FileAccessPolicy.UnderRoots(folder), FileAccessPolicy.NoSymlinks)</c>
///         for the same confinement. No part of the library uses it any longer.
///     </para>
/// </remarks>
#pragma warning disable S1133 // Deprecated on purpose for one release; removed in the next major release
[Obsolete("Use FileReferenceResolver with FileAccessPolicy.Combine(FileAccessPolicy.UnderRoots(...), FileAccessPolicy.NoSymlinks). This type will be removed in the next major release.")]
#pragma warning restore S1133
public enum SandboxFailureKind
{
    /// <summary>The path is empty or consists only of white space.</summary>
    EmptyPath,

    /// <summary>The path contains a control character (including NUL and the C1 range).</summary>
    ControlCharacter,

    /// <summary>The path contains a colon (a drive letter, or an alternate data stream on Windows).</summary>
    ColonInPath,

    /// <summary>The path is absolute: it starts with a slash or a backslash (including UNC paths).</summary>
    AbsolutePath,

    /// <summary>A <c>..</c> segment would leave the root that the path is confined to.</summary>
    OutsideRoot,

    /// <summary>
    ///     The file, or a directory above it below its root, is a symbolic link or junction - found by the link check
    ///     before the open, or by the real path of the open file not being the root's real location followed by the
    ///     path below the root (a link swapped in after the check).
    /// </summary>
    ThroughLink,

    /// <summary>
    ///     The link check itself failed, or the real path of the open file or the real location of its root cannot be
    ///     determined, so the file is refused rather than trusted.
    /// </summary>
    LinkCheckFailed,

    /// <summary>
    ///     The file exists but cannot be read: it is a directory or another file that is not a regular file (a device,
    ///     a named pipe), it cannot be opened, the read failed, or the caller's resolver callback threw or returned a
    ///     stream that failed.
    /// </summary>
    CannotRead,

    /// <summary>The file is longer than <see cref="SandboxOptions.MaxFileBytes"/>.</summary>
    TooLarge,

    /// <summary>A configured base directory or search path is not a valid path.</summary>
    InvalidRoot,

    /// <summary>The resolver already read <see cref="SandboxOptions.MaxFiles"/> distinct files; this one was not read.</summary>
    TooManyFiles,
}
