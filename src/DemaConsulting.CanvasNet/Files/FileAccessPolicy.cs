namespace DemaConsulting.CanvasNet.Files;

/// <summary>
///     Decides whether a file that a document references may be read: the single hook through which a host restricts
///     the files <see cref="FileReferenceResolver"/> reads.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why it exists.</b> A document (an SVG image link, an OpenSCAD include, a Vega-Lite data URL) names other
///         files, and the right answer to "may this file be read" depends on the host: a desktop converter reads
///         anything its user can, a build server confines documents to the repository, a web service may refuse links
///         entirely. A policy expresses that answer once. It is asked <b>after</b> the file was opened and before any
///         byte is read, with the real path of the open handle (see <see cref="FileAccessRequest.RealPath"/>), so a
///         link swapped in between a check and the open cannot slip through.
///     </para>
///     <para>
///         <b>Built-in policies.</b> <see cref="AllowAll"/> (the default, named <c>any</c>) reads every regular local
///         file. <see cref="UnderRoots(string[])"/> (named <c>roots</c>) requires both the path as written and the
///         real path to lie inside one of the given folders, so a link is followed only when it stays inside them.
///         <see cref="NoSymlinks"/> (named <c>no-symlinks</c>) refuses a file whose real path differs from the path as
///         written below the folder the reference starts from. <see cref="Combine(FileAccessPolicy[])"/> requires every
///         part to allow the file, and <see cref="FromDelegate"/> wraps a function.
///     </para>
///     <para>
///         <b>Always refused.</b> Whatever the policy, the resolver refuses network paths (UNC and <c>//host</c>),
///         device paths, alternate data streams, control characters and anything that is not a regular file, and keeps
///         every size and count limit. A restrictive policy (any policy other than <see cref="AllowAll"/>) is never
///         consulted without a real path: where the platform cannot tell the real path of an open file, the file is
///         refused with <see cref="FileReferenceFailureKind.RealPathUnavailable"/> (fail closed).
///     </para>
///     <para>
///         <b>Implementing a policy.</b> Derive from this class (or use <see cref="FromDelegate"/>), return a
///         <see cref="FileAccessDecision"/> and do not throw: an exception other than
///         <see cref="OperationCanceledException"/>, or a <see langword="null"/> decision, is treated as a denial.
///         Instances must be immutable and thread-safe, because one policy is typically shared by every render of a
///         host. Every built-in policy is.
///     </para>
/// </remarks>
public abstract partial class FileAccessPolicy
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="FileAccessPolicy"/> class.
    /// </summary>
    protected FileAccessPolicy()
    {
    }

    /// <summary>
    ///     Gets the policy that allows every regular local file (named <c>any</c>), like Pandoc: <c>..</c>, absolute
    ///     paths and symbolic links are followed. It is the default and never needs the real path of a file.
    /// </summary>
    public static FileAccessPolicy AllowAll { get; } = new AllowAllPolicy();

    /// <summary>
    ///     Gets the policy that refuses a file reached through a symbolic link or junction (named <c>no-symlinks</c>).
    /// </summary>
    /// <remarks>
    ///     The folder the reference starts from is the deepest folder shared by the path as written and the base
    ///     directory of the request; it and every folder above it are trusted (the host chose them). Below it, the real
    ///     path of the opened file must equal the path as written, so a link anywhere below that folder - a directory
    ///     or the file itself - is denied, whatever its target. A hard link cannot be told apart from the file itself
    ///     and is not denied.
    /// </remarks>
    public static FileAccessPolicy NoSymlinks { get; } = new NoSymlinksPolicy();

    /// <summary>
    ///     Gets the name of the policy, used in diagnostics: <c>any</c>, <c>roots</c>, <c>no-symlinks</c>, the names of
    ///     combined policies joined with <c>+</c>, or the name given to <see cref="FromDelegate"/>.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    ///     Gets a value indicating whether the policy needs the real path of a file. Only <see cref="AllowAll"/> (alone
    ///     or combined only with itself) does not; every other policy fails closed where the real path is unavailable.
    /// </summary>
    internal virtual bool RequiresRealPath => true;

    /// <summary>
    ///     Creates the policy that allows a file only when both the path as written and its real path lie inside one of
    ///     the given folders (named <c>roots</c>).
    /// </summary>
    /// <remarks>
    ///     A root is a folder chosen by the host; it and everything above it are trusted, so a root that is itself a
    ///     link is fine. A file reached through a link is allowed when the link stays inside a root. The path as written
    ///     is checked before the file is opened, so a file outside every root is never opened (no existence oracle and
    ///     no device outside the roots is touched); the real path is checked after the open. Relative roots are made
    ///     full against the current directory now; the real location of each root is determined on first use.
    /// </remarks>
    /// <param name="roots">The folders; at least one, none null, empty or invalid.</param>
    /// <returns>The policy.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="roots"/> or one of its entries is null.</exception>
    /// <exception cref="ArgumentException">Thrown when there is no root, or a root is empty, white space or not a valid path.</exception>
    public static FileAccessPolicy UnderRoots(params string[] roots) => UnderRoots((IEnumerable<string>)roots);

    /// <summary>
    ///     Creates the policy that allows a file only when both the path as written and its real path lie inside one of
    ///     the given folders (named <c>roots</c>). See <see cref="UnderRoots(string[])"/>.
    /// </summary>
    /// <param name="roots">The folders; at least one, none null, empty or invalid. The sequence is copied.</param>
    /// <returns>The policy.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="roots"/> or one of its entries is null.</exception>
    /// <exception cref="ArgumentException">Thrown when there is no root, or a root is empty, white space or not a valid path.</exception>
    public static FileAccessPolicy UnderRoots(IEnumerable<string> roots) => new UnderRootsPolicy(roots);

    /// <summary>
    ///     Creates the policy that allows a file only when every given policy allows it.
    /// </summary>
    /// <remarks>
    ///     The policies are asked in order and the first denial wins, so put the cheapest or most specific first.
    ///     Nested combinations are flattened. A single policy is returned as it is. The name is the names of the parts
    ///     joined with <c>+</c>, for example <c>roots+no-symlinks</c>.
    /// </remarks>
    /// <param name="policies">The policies; at least one, none null.</param>
    /// <returns>The combined policy.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="policies"/> or one of its entries is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="policies"/> is empty.</exception>
    public static FileAccessPolicy Combine(params FileAccessPolicy[] policies)
    {
        ArgumentNullException.ThrowIfNull(policies);
        if (policies.Length == 0)
        {
            throw new ArgumentException("At least one policy is required.", nameof(policies));
        }

        // Nested combinations are flattened, so the name and the order of evaluation read as one list
        var parts = new List<FileAccessPolicy>();
        foreach (var policy in policies)
        {
            if (policy is null)
            {
                throw new ArgumentNullException(nameof(policies), "A policy must not be null.");
            }

            if (policy is CombinedPolicy combined)
            {
                parts.AddRange(combined.Parts);
            }
            else
            {
                parts.Add(policy);
            }
        }

        return parts.Count == 1 ? parts[0] : new CombinedPolicy(parts);
    }

    /// <summary>
    ///     Creates a policy from a function, for a host rule that needs no class of its own.
    /// </summary>
    /// <param name="name">The name used in diagnostics; must not be empty or white space.</param>
    /// <param name="check">
    ///     The function that decides; it must be thread-safe and should not throw (an exception, or a
    ///     <see langword="null"/> result, is treated as a denial).
    /// </param>
    /// <returns>The policy. It always needs the real path of a file.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="check"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or white space.</exception>
    public static FileAccessPolicy FromDelegate(string name, Func<FileAccessRequest, FileAccessDecision> check)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(check);
        return new DelegatePolicy(name, check);
    }

    /// <summary>
    ///     Decides whether an opened file may be read.
    /// </summary>
    /// <param name="request">The facts of the opened file. Never null when called by <see cref="FileReferenceResolver"/>.</param>
    /// <returns>The decision; must not be null.</returns>
    public abstract FileAccessDecision Check(FileAccessRequest request);

    /// <summary>
    ///     Decides, before a file is opened, whether the path as written can be denied already: a lexical check that
    ///     keeps a file outside the allowed area from being opened at all.
    /// </summary>
    /// <param name="requestedPath">The full path as written.</param>
    /// <returns>A denial, or <see langword="null"/> when the open may go ahead (the real check follows the open).</returns>
    internal virtual FileAccessDecision? PreCheck(string requestedPath) => null;

    /// <summary>
    ///     Returns the name of the policy.
    /// </summary>
    /// <returns>The value of <see cref="Name"/>.</returns>
    public override string ToString() => Name;
}
