namespace DemaConsulting.CanvasNet.Files;

/// <summary>
///     The exception thrown by <see cref="SandboxedFileResolver"/> when it refuses a path or cannot deliver a file.
/// </summary>
/// <remarks>
///     <para>
///         The exception exists so a caller can turn a sandbox failure into its own error type, with its own wording
///         and source location, without parsing text: <see cref="Kind"/> says what went wrong, <see cref="Path"/> and
///         <see cref="Detail"/> carry the facts. A path that is merely not found is not a failure; the resolver
///         reports it by returning <see langword="null"/>. A cancellation is never wrapped: it surfaces as
///         <see cref="OperationCanceledException"/>.
///     </para>
///     <para>
///         <see cref="Exception.Message"/> is a complete English sentence that never contains a host directory
///         (only the path as the caller wrote it, with control characters replaced, or a path relative to the
///         confining root), so it is safe to show to the author of an untrusted path. <see cref="Path"/> for a
///         file failure is the full path and is not; show it only to trusted callers.
///     </para>
///     <para>Instances are immutable and safe to use from any thread.</para>
/// </remarks>
public sealed class SandboxException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SandboxException"/> class.
    /// </summary>
    /// <param name="kind">What went wrong.</param>
    /// <param name="path">
    ///     For <see cref="SandboxFailureKind.EmptyPath"/>, <see cref="SandboxFailureKind.ControlCharacter"/>,
    ///     <see cref="SandboxFailureKind.ColonInPath"/>, <see cref="SandboxFailureKind.AbsolutePath"/> and
    ///     <see cref="SandboxFailureKind.OutsideRoot"/>: the path the caller asked for (control characters replaced
    ///     by <c>?</c>, backslashes turned into forward slashes for <see cref="SandboxFailureKind.OutsideRoot"/>).
    ///     For <see cref="SandboxFailureKind.InvalidRoot"/>: the configured directory. For every other kind: the key
    ///     of the file the failure is about (see <see cref="SandboxedFile.Key"/>). Must not be null.
    /// </param>
    /// <param name="detail">
    ///     For the first four kinds, the reason phrase (for example <c>the path is empty</c>); for
    ///     <see cref="SandboxFailureKind.ThroughLink"/>, the path of the link relative to its root, with forward
    ///     slashes; otherwise <see langword="null"/>.
    /// </param>
    /// <param name="innerException">The underlying failure, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    public SandboxException(SandboxFailureKind kind, string path, string? detail = null, Exception? innerException = null)
        : base(BuildMessage(kind, path, detail), innerException)
    {
        Kind = kind;
        Path = path;
        Detail = detail;
    }

    /// <summary>
    ///     Gets what went wrong.
    /// </summary>
    public SandboxFailureKind Kind { get; }

    /// <summary>
    ///     Gets the path the failure is about: the path text as asked for when <see cref="Kind"/> is a path-text
    ///     failure or <see cref="SandboxFailureKind.OutsideRoot"/>, the configured directory for
    ///     <see cref="SandboxFailureKind.InvalidRoot"/>, otherwise the full path (or, in resolver-callback mode, the
    ///     logical path) of the file. A full path reveals the host's directories.
    /// </summary>
    public string Path { get; }

    /// <summary>
    ///     Gets the kind-specific detail: the reason phrase of a path-text failure, the root-relative link path of a
    ///     <see cref="SandboxFailureKind.ThroughLink"/> failure, or <see langword="null"/>.
    /// </summary>
    public string? Detail { get; }

    /// <summary>
    ///     Builds the message of the exception from its facts, without disclosing a host directory.
    /// </summary>
    /// <param name="kind">The failure kind.</param>
    /// <param name="path">The path the failure is about.</param>
    /// <param name="detail">The kind-specific detail.</param>
    /// <returns>The message.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    private static string BuildMessage(SandboxFailureKind kind, string path, string? detail)
    {
        ArgumentNullException.ThrowIfNull(path);
        return kind switch
        {
            SandboxFailureKind.EmptyPath or SandboxFailureKind.ControlCharacter or SandboxFailureKind.ColonInPath
                or SandboxFailureKind.AbsolutePath => $"The path '{path}' is not allowed: {detail}.",
            SandboxFailureKind.OutsideRoot => $"The path '{path}' is outside the allowed directories.",
            SandboxFailureKind.ThroughLink =>
                $"The file passes through a link ('{detail}'); links are not followed inside the allowed directories.",
            SandboxFailureKind.LinkCheckFailed => "The file cannot be checked for links and is not read.",
            SandboxFailureKind.CannotRead => "The file cannot be read.",
            SandboxFailureKind.TooLarge => "The file is larger than the allowed size.",
            SandboxFailureKind.InvalidRoot => "A configured directory (BaseDirectory or SearchPaths) is not a valid path.",
            SandboxFailureKind.TooManyFiles => "The limit on the number of files was reached; the file was not read.",
            _ => "The file was refused by the sandbox.",
        };
    }
}
