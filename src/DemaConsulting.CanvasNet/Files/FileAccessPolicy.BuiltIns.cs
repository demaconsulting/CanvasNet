namespace DemaConsulting.CanvasNet.Files;

/// <summary>
///     The built-in policies of <see cref="FileAccessPolicy"/>: allow all, under roots, no symbolic links, combined,
///     delegate and anchored, plus the link match that no-symlinks shares with <see cref="SandboxedFileResolver"/>. The policies
///     are private so that callers depend only on the factory members and the abstract contract.
/// </summary>
public abstract partial class FileAccessPolicy
{
    /// <summary>
    ///     Determines whether the real path of a file is the real location of a trusted anchor followed by the path as
    ///     written below it, so no link lies below the anchor. Shared by <see cref="NoSymlinks"/> and
    ///     <see cref="SandboxedFileResolver"/>, which must tell an unknown anchor from a link.
    /// </summary>
    /// <param name="anchor">The trusted folder, an ancestor-or-self of <paramref name="requestedPath"/>.</param>
    /// <param name="requestedPath">The full path as written.</param>
    /// <param name="realPath">The real path of the opened file.</param>
    /// <returns>
    ///     <see langword="true"/> when no link lies below the anchor, <see langword="false"/> when one does, and
    ///     <see langword="null"/> when the real location of the anchor cannot be determined.
    /// </returns>
    internal static bool? MatchesWithoutLinks(string anchor, string requestedPath, string realPath)
    {
        var canonicalAnchor = RealPath.TryGetCanonicalDirectory(anchor);
        if (canonicalAnchor is null)
        {
            return null;
        }

        // Below the anchor the real path must equal the written path: any link there changes the real path
        if (RealPath.PathsEqual(Expected(canonicalAnchor, anchor, requestedPath), realPath))
        {
            return true;
        }

        // A written path with short (8.3) names is compared in its long spelling too; this only rewrites the expected
        // text, so a link still shows up as a different real path (Windows only; identity elsewhere)
        if (!requestedPath.Contains('~', StringComparison.Ordinal))
        {
            return false;
        }

        var longAnchor = RealPath.ExpandShortNames(anchor);
        var longRequested = RealPath.ExpandShortNames(requestedPath);
        return RealPath.PathsEqual(Expected(canonicalAnchor, longAnchor, longRequested), realPath);
    }

    /// <summary>
    ///     Computes the real path a file must have when no link lies below the anchor.
    /// </summary>
    /// <param name="canonicalAnchor">The real location of the anchor.</param>
    /// <param name="anchor">The anchor as written.</param>
    /// <param name="requestedPath">The full path as written.</param>
    /// <returns>The expected real path.</returns>
    private static string Expected(string canonicalAnchor, string anchor, string requestedPath)
    {
        var relative = Path.GetRelativePath(anchor, requestedPath);
        return relative == "." ? canonicalAnchor : Path.Join(canonicalAnchor, relative);
    }

    /// <summary>
    ///     The policy that allows every file (<see cref="AllowAll"/>).
    /// </summary>
    private sealed class AllowAllPolicy : FileAccessPolicy
    {
        /// <inheritdoc/>
        public override string Name => "any";

        /// <inheritdoc/>
        internal override bool RequiresRealPath => false;

        /// <inheritdoc/>
        public override FileAccessDecision Check(FileAccessRequest request) => FileAccessDecision.Allow;
    }

    /// <summary>
    ///     The policy that confines files to folders (<see cref="UnderRoots(IEnumerable{string})"/>).
    /// </summary>
    private sealed class UnderRootsPolicy : FileAccessPolicy
    {
        /// <summary>The hint of every denial.</summary>
        private const string RootsHint = "add the folder the file is in to the allowed roots";

        /// <summary>The roots.</summary>
        private readonly Root[] _roots;

        /// <summary>
        ///     Initializes the policy, validating and copying the roots.
        /// </summary>
        /// <param name="roots">The folders.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="roots"/> or an entry is null.</exception>
        /// <exception cref="ArgumentException">Thrown when there is no root, or a root is empty or not a valid path.</exception>
        public UnderRootsPolicy(IEnumerable<string> roots)
        {
            ArgumentNullException.ThrowIfNull(roots);
            var list = new List<Root>();
            foreach (var root in roots)
            {
                if (root is null)
                {
                    throw new ArgumentNullException(nameof(roots), "A root must not be null.");
                }

                if (string.IsNullOrWhiteSpace(root))
                {
                    throw new ArgumentException("A root must not be empty or white space.", nameof(roots));
                }

                // The lexical full path is fixed now, so a later change of the current directory does not move a root
                try
                {
                    list.Add(new Root(Path.GetFullPath(root)));
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    throw new ArgumentException("A root is not a valid path.", nameof(roots), ex);
                }
            }

            if (list.Count == 0)
            {
                throw new ArgumentException("At least one root is required.", nameof(roots));
            }

            _roots = [.. list];
        }

        /// <inheritdoc/>
        public override string Name => "roots";

        /// <inheritdoc/>
        internal override FileAccessDecision? PreCheck(string requestedPath) =>
            IsWrittenPathInside(requestedPath)
                ? null
                : FileAccessDecision.Deny("the path as written is outside the allowed roots", RootsHint);

        /// <inheritdoc/>
        public override FileAccessDecision Check(FileAccessRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            // Both spellings must be inside: the written path (so an outside link that points in is still refused, as the
            // pre-check refused it before the open) and the real path of the handle (so a link that leads out is refused)
            if (!IsWrittenPathInside(request.RequestedPath))
            {
                return FileAccessDecision.Deny("the path as written is outside the allowed roots", RootsHint);
            }

            return Array.Exists(_roots, root => root.ContainsRealPath(request.RealPath))
                ? FileAccessDecision.Allow
                : FileAccessDecision.Deny("its real path is outside the allowed roots", RootsHint);
        }

        /// <summary>
        ///     Determines whether a path as written lies inside a root, in the root's written or real spelling.
        /// </summary>
        /// <param name="path">The full path as written.</param>
        /// <returns><see langword="true"/> when the path is inside a root.</returns>
        private bool IsWrittenPathInside(string path)
        {
            if (Array.Exists(_roots, root => root.ContainsWrittenPath(path)))
            {
                return true;
            }

            // A path written with short (8.3) names is compared in its long spelling too (Windows only; identity elsewhere)
            if (!path.Contains('~', StringComparison.Ordinal))
            {
                return false;
            }

            var expanded = RealPath.ExpandShortNames(path);
            return !string.Equals(expanded, path, StringComparison.Ordinal)
                && Array.Exists(_roots, root => root.ContainsWrittenPath(expanded));
        }

        /// <summary>
        ///     One root: its full path as given, and its real location once it could be determined.
        /// </summary>
        /// <param name="lexical">The full path of the root as given.</param>
        private sealed class Root(string lexical)
        {
            /// <summary>The real location of the root, or <see langword="null"/> while it could not be determined.</summary>
            private string? _canonical;

            /// <summary>
            ///     Gets the real location of the root, determined on first use and again on later uses while it cannot be
            ///     determined (a root that does not exist yet may be created later).
            /// </summary>
            private string? Canonical => _canonical ??= RealPath.TryGetCanonicalDirectory(lexical);

            /// <summary>
            ///     Determines whether a path as written lies inside the root.
            /// </summary>
            /// <param name="path">The full path.</param>
            /// <returns><see langword="true"/> when it is inside.</returns>
            public bool ContainsWrittenPath(string path) =>
                RealPath.IsUnder(path, lexical) || (Canonical is { } canonical && RealPath.IsUnder(path, canonical));

            /// <summary>
            ///     Determines whether a real path lies inside the root. A real path contains no link, so it can lie under
            ///     the root's written spelling only when that spelling contains no link either; both spellings are safe.
            /// </summary>
            /// <param name="path">The real path.</param>
            /// <returns><see langword="true"/> when it is inside.</returns>
            public bool ContainsRealPath(string path) => ContainsWrittenPath(path);
        }
    }

    /// <summary>
    ///     The policy that refuses links below the folder a reference starts from (<see cref="NoSymlinks"/>).
    /// </summary>
    private sealed class NoSymlinksPolicy : FileAccessPolicy
    {
        /// <inheritdoc/>
        public override string Name => "no-symlinks";

        /// <inheritdoc/>
        public override FileAccessDecision Check(FileAccessRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            // The anchor is the deepest folder shared by the written path and the trusted base directory (the origin of
            // the chain, never a referring document's folder); it and everything above it are trusted, so links there
            // (macOS /var, an 8.3 or linked home folder) do not count
            var anchor = RealPath.CommonAncestor(request.RequestedPath, request.BaseDirectory)
                ?? Path.GetPathRoot(request.RequestedPath)
                ?? request.RequestedPath;
            return MatchesWithoutLinks(anchor, request.RequestedPath, request.RealPath) switch
            {
                null => FileAccessDecision.Deny(
                    "the real path of the folder the reference starts from cannot be determined",
                    "make sure the folder exists and can be opened"),
                true => FileAccessDecision.Allow,
                false => FileAccessDecision.Deny(
                    "its real path differs from the path as written, so the path passes through a symbolic link or junction",
                    "reference the file at its real location, or use a policy that follows links"),
            };
        }
    }

    /// <summary>
    ///     The policy that requires every part to allow a file (<see cref="Combine"/>).
    /// </summary>
    /// <param name="parts">The parts, in order of evaluation; at least two, none combined.</param>
    private sealed class CombinedPolicy(IReadOnlyList<FileAccessPolicy> parts) : FileAccessPolicy
    {
        /// <summary>
        ///     Gets the parts, in order of evaluation.
        /// </summary>
        public IReadOnlyList<FileAccessPolicy> Parts { get; } = parts;

        /// <inheritdoc/>
        public override string Name { get; } = string.Join('+', parts.Select(part => part.Name));

        /// <inheritdoc/>
        internal override bool RequiresRealPath => Parts.Any(part => part.RequiresRealPath);

        /// <inheritdoc/>
        internal override FileAccessDecision? PreCheck(string requestedPath) =>
            Parts.Select(part => part.PreCheck(requestedPath)).FirstOrDefault(decision => decision is not null);

        /// <inheritdoc/>
        public override FileAccessDecision Check(FileAccessRequest request)
        {
            // The first denial wins; a part that answers nothing is a failure of the policy, which the resolver denies
            foreach (var part in Parts)
            {
                var decision = part.Check(request)
                    ?? throw new InvalidOperationException($"The file-access policy '{part.Name}' returned no decision.");
                if (!decision.IsAllowed)
                {
                    return decision;
                }
            }

            return FileAccessDecision.Allow;
        }
    }

    /// <summary>
    ///     The policy backed by a function (<see cref="FromDelegate"/>).
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="check">The function.</param>
    private sealed class DelegatePolicy(string name, Func<FileAccessRequest, FileAccessDecision> check) : FileAccessPolicy
    {
        /// <inheritdoc/>
        public override string Name => name;

        /// <inheritdoc/>
        public override FileAccessDecision Check(FileAccessRequest request) => check(request);
    }

    /// <summary>
    ///     A policy judged from a fixed chain origin (<see cref="AnchoredAt(string)"/>).
    /// </summary>
    /// <param name="inner">The policy that decides.</param>
    /// <param name="origin">The origin given to it as the base directory of every request.</param>
    private sealed class AnchoredPolicy(FileAccessPolicy inner, string origin) : FileAccessPolicy
    {
        /// <inheritdoc/>
        public override string Name => inner.Name;

        /// <inheritdoc/>
        internal override bool RequiresRealPath => inner.RequiresRealPath;

        /// <inheritdoc/>
        internal override FileAccessDecision? PreCheck(string requestedPath) => inner.PreCheck(requestedPath);

        /// <inheritdoc/>
        internal override FileAccessPolicy AnchoredAt(string origin)
        {
            // The outermost origin wins: a nested document never moves the anchor of its outer chain
            ArgumentNullException.ThrowIfNull(origin);
            return this;
        }

        /// <inheritdoc/>
        public override FileAccessDecision Check(FileAccessRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            return inner.Check(request with { BaseDirectory = origin });
        }
    }
}
