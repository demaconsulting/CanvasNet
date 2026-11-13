namespace DemaConsulting.CanvasNet.Files;

/// <summary>
///     What a <see cref="FileAccessPolicy"/> is asked about: one referenced file that was already opened, described by
///     the path as written, the path that was opened, and the real path of the opened file.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="FileReferenceResolver"/> builds a request <b>after</b> it opened the file and asks the policy
///         before it reads a single byte. <see cref="RealPath"/> comes from the open handle itself (not from the name),
///         so a symbolic link or junction that is swapped in or out after the open cannot change what the policy sees:
///         the decision is about the file that would actually be read.
///     </para>
///     <para>
///         Every path except <see cref="Reference"/> may reveal the host's directories; a policy may use them freely,
///         but should not copy them into text shown to the untrusted author of a document. Instances are immutable and
///         safe to use from any thread.
///     </para>
/// </remarks>
public sealed record FileAccessRequest
{
    /// <summary>
    ///     Gets the path as written in the document (control characters replaced by <c>?</c>).
    /// </summary>
    public required string Reference { get; init; }

    /// <summary>
    ///     Gets the full path of the referring document (the file the reference was found in), or
    ///     <see langword="null"/> when the reference comes from a document that has no file (a stream).
    /// </summary>
    public string? Referrer { get; init; }

    /// <summary>
    ///     Gets the full path of the directory the reference was resolved against: the referrer's directory, the base
    ///     directory, or the search path the file was found under.
    /// </summary>
    public required string BaseDirectory { get; init; }

    /// <summary>
    ///     Gets the full path that was opened, as written: <c>.</c> and <c>..</c> collapsed, but symbolic links and
    ///     junctions not resolved.
    /// </summary>
    public required string RequestedPath { get; init; }

    /// <summary>
    ///     Gets the final path of the opened file as reported by the operating system for the open handle: symbolic
    ///     links, junctions, mapped and substituted drives, and short (8.3) names resolved.
    /// </summary>
    public required string RealPath { get; init; }
}
