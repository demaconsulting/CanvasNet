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
    ///     Gets the full path of the host-chosen directory this lookup chain started from: the configured base
    ///     directory, or the search path under which the first file of the chain was found. It is trusted: it never
    ///     comes from a document, not even when the reference was resolved relative to a referring document (that
    ///     document is <see cref="Referrer"/>; the directory the reference was combined with is the referrer's
    ///     directory).
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

    /// <summary>
    ///     Gets the facts about the trusted folders and the written path that the resolver captured <b>before</b> the
    ///     open, or <see langword="null"/> when the request was built by other code (a direct call of
    ///     <see cref="FileAccessPolicy.Check"/>). A built-in policy uses only these facts after the open, never resolving
    ///     a folder by name again, so a folder renamed and replaced by a link after the capture cannot change its answer.
    /// </summary>
    internal PreOpenSnapshot? Snapshot { get; init; }
}

/// <summary>
///     The facts a resolver captures before it opens a file, for the check after the open.
/// </summary>
/// <param name="LongRequestedPath">
///     The requested path with short (8.3) names expanded (Windows), or the requested path itself.
/// </param>
/// <param name="AnchorLocations">
///     The real location of each trusted folder a policy asked for (<see cref="FileAccessPolicy.AnchorsOf"/>), keyed
///     by the folder as written; <see langword="null"/> where it could not be determined.
/// </param>
internal sealed record PreOpenSnapshot(string LongRequestedPath, IReadOnlyDictionary<string, string?> AnchorLocations);
