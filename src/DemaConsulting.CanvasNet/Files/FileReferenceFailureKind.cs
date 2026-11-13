namespace DemaConsulting.CanvasNet.Files;

/// <summary>
///     Identifies why <see cref="FileReferenceResolver"/> refused a reference or could not deliver a file.
/// </summary>
/// <remarks>
///     A caller branches on this value (never on the message text) to decide how to report the failure; the
///     <see cref="FileReferenceException"/> carries the kind-specific facts. The first four values are problems with the
///     text of the reference itself and are detected before any file or callback is touched; they are refused under
///     every policy.
/// </remarks>
public enum FileReferenceFailureKind
{
    /// <summary>The reference is empty or consists only of white space.</summary>
    EmptyPath,

    /// <summary>The reference contains a control character (including NUL, DEL and the C1 range).</summary>
    ControlCharacter,

    /// <summary>
    ///     The reference is not a usable local path: a device path (<c>\\?\</c>, <c>\\.\</c>), a colon other than the
    ///     drive of an absolute Windows path (an alternate data stream, a drive-relative path, a drive path on Linux or
    ///     macOS), a path that is not valid, or, with a resolver callback, an absolute path or a <c>..</c> above the root.
    /// </summary>
    InvalidPath,

    /// <summary>The reference names a network location (a UNC path or <c>//host</c>), which is never read.</summary>
    NetworkPath,

    /// <summary>The file-access policy denied the opened file; nothing was read.</summary>
    AccessDenied,

    /// <summary>
    ///     The policy needs the real path of the opened file, but the platform cannot tell it (for example
    ///     <c>/proc</c> is not mounted, or the file was deleted after the open); the file is refused rather than trusted.
    /// </summary>
    RealPathUnavailable,

    /// <summary>The path names something other than a regular file: a directory, a device or a named pipe.</summary>
    NotARegularFile,

    /// <summary>
    ///     The file exists but cannot be read: it cannot be opened (access, sharing, I/O), the read failed, or the
    ///     caller's resolver callback threw or returned a stream that failed.
    /// </summary>
    CannotRead,

    /// <summary>The file is longer than <see cref="FileReferenceOptions.MaxFileBytes"/>.</summary>
    TooLarge,

    /// <summary>A configured base directory or search path is not a valid path.</summary>
    InvalidBaseDirectory,

    /// <summary>The resolver already read <see cref="FileReferenceOptions.MaxFiles"/> distinct files; this one was not read.</summary>
    TooManyFiles,
}
