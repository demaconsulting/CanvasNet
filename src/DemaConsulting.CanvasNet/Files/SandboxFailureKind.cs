namespace DemaConsulting.CanvasNet.Files;

/// <summary>
///     Identifies why <see cref="SandboxedFileResolver"/> refused a path or could not deliver a file.
/// </summary>
/// <remarks>
///     A caller branches on this value (never on the message text) to decide how to report the failure; the
///     <see cref="SandboxException.Path"/> and <see cref="SandboxException.Detail"/> of the exception carry the
///     kind-specific facts. The first four values are problems with the path text itself and are detected before
///     any file or callback is touched.
/// </remarks>
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

    /// <summary>The file, or a directory above it below its root, is a symbolic link or junction.</summary>
    ThroughLink,

    /// <summary>The link check itself failed, so the file is refused rather than trusted.</summary>
    LinkCheckFailed,

    /// <summary>
    ///     The file exists but cannot be read: it is a directory, it cannot be opened, the read failed, or the
    ///     caller's resolver callback threw or returned a stream that failed.
    /// </summary>
    CannotRead,

    /// <summary>The file is longer than <see cref="SandboxOptions.MaxFileBytes"/>.</summary>
    TooLarge,

    /// <summary>A configured base directory or search path is not a valid path.</summary>
    InvalidRoot,
}
