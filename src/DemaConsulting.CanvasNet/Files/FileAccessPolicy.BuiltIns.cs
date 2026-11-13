namespace DemaConsulting.CanvasNet.Files;

/// <summary>
///     The built-in policies of <see cref="FileAccessPolicy"/>: allow all, under roots, no symbolic links, combined,
///     delegate and anchored, plus the link match that no-symlinks shares with <see cref="SandboxedFileResolver"/>. The policies
///     are private so that callers depend only on the factory members and the abstract contract.
/// </summary>
public abstract partial class FileAccessPolicy
{
    /// <summary>The separators a path is split at when its components are counted.</summary>
    private static readonly char[] ComponentSeparators = ['/', '\\'];

    /// <summary>
    ///     Determines whether the real path of a file is the real location of a trusted anchor followed by the path as
    ///     written below it, so no link lies below the anchor. Shared by <see cref="NoSymlinks"/> and
    ///     <see cref="SandboxedFileResolver"/>, which must tell an unknown anchor from a link.
    /// </summary>
    /// <remarks>
    ///     Nothing is resolved by name here: the real location of the anchor and the long spelling of the written path
    ///     are captured by the caller <b>before</b> the file is opened. An anchor renamed and replaced by a link after
    ///     that capture therefore leads the open elsewhere, and the real path no longer matches.
    /// </remarks>
    /// <param name="anchor">The trusted folder as written, an ancestor-or-self of <paramref name="requestedPath"/>.</param>
    /// <param name="canonicalAnchor">The real location of the anchor captured before the open, or <see langword="null"/> when unknown.</param>
    /// <param name="requestedPath">The full path as written.</param>
    /// <param name="longRequestedPath">The written path with short (8.3) names expanded, captured before the open.</param>
    /// <param name="realPath">The real path of the opened file.</param>
    /// <returns>
    ///     <see langword="true"/> when no link lies below the anchor, <see langword="false"/> when one does, and
    ///     <see langword="null"/> when the real location of the anchor is unknown.
    /// </returns>
    internal static bool? MatchesWithoutLinks(
        string anchor, string? canonicalAnchor, string requestedPath, string longRequestedPath, string realPath)
    {
        if (canonicalAnchor is null)
        {
            return null;
        }

        // Below the anchor the real path must equal the written path: any link there changes the real path
        var relative = Path.GetRelativePath(anchor, requestedPath);
        if (RealPath.PathsEqual(Expected(canonicalAnchor, relative), realPath))
        {
            return true;
        }

        // A written path with short (8.3) names is compared in its long spelling too. The expansion keeps one component
        // per component, so the part below the anchor is the same number of trailing components of the long spelling;
        // this only rewrites the expected text, so a link still shows up as a different real path
        if (relative == "." || string.Equals(longRequestedPath, requestedPath, StringComparison.Ordinal))
        {
            return false;
        }

        var count = relative.Split(ComponentSeparators, StringSplitOptions.RemoveEmptyEntries).Length;
        var longParts = longRequestedPath.Split(ComponentSeparators, StringSplitOptions.RemoveEmptyEntries);
        return longParts.Length >= count
            && RealPath.PathsEqual(
                Expected(canonicalAnchor, string.Join(Path.DirectorySeparatorChar, longParts[^count..])),
                realPath);
    }

    /// <summary>
    ///     Gets the spelling of a full path with short (8.3) names expanded (Windows); elsewhere, or for a path without a
    ///     short name, the path itself. Resolves by name, so it is called before a file is opened.
    /// </summary>
    /// <param name="path">The full path.</param>
    /// <returns>The long spelling.</returns>
    internal static string LongSpelling(string path) =>
        OperatingSystem.IsWindows() && path.Contains('~', StringComparison.Ordinal) ? RealPath.ExpandShortNames(path) : path;

    /// <summary>
    ///     Computes the real path a file must have when no link lies below the anchor.
    /// </summary>
    /// <param name="canonicalAnchor">The real location of the anchor.</param>
    /// <param name="relative">The path below the anchor (<c>.</c> for the anchor itself).</param>
    /// <returns>The expected real path.</returns>
    private static string Expected(string canonicalAnchor, string relative) =>
        relative == "." ? canonicalAnchor : Path.Join(canonicalAnchor, relative);

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

                // The lexical full path is fixed now, so a later change of the current directory does not move a root.
                // A root in a device namespace other than an ordinary extended-length drive or volume path
                // (\\?\GLOBALROOT\..., \\.\...) is no folder a file may be confined to; it is not even resolved
                string full;
                try
                {
                    full = Path.GetFullPath(root);
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    throw new ArgumentException("A root is not a valid path.", nameof(roots), ex);
                }

                if (OperatingSystem.IsWindows() && RealPath.IsWindowsDeviceNamespace(full) && !RealPath.IsOrdinaryExtendedPath(full))
                {
                    throw new ArgumentException("A root must not be in a device namespace.", nameof(roots));
                }

                list.Add(new Root(full));
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
            // pre-check refused it before the open) and the real path of the handle (so a link that leads out is refused).
            // The long spelling of the written path is the one captured before the open, when there is one
            if (!IsWrittenPathInside(request.RequestedPath, request.Snapshot?.LongRequestedPath))
            {
                return FileAccessDecision.Deny("the path as written is outside the allowed roots", RootsHint);
            }

            if (Array.Exists(_roots, root => root.ContainsRealPath(request.RealPath)))
            {
                return FileAccessDecision.Allow;
            }

            // A root that did not exist when the policy was created has no captured real location and allows nothing
            return Array.Exists(_roots, root => root.Canonical is null && root.ContainsWrittenPath(request.RequestedPath))
                ? FileAccessDecision.Deny(
                    "the allowed root it is in did not exist when the policy was created, so it allows nothing",
                    "create the folder before creating the policy")
                : FileAccessDecision.Deny("its real path is outside the allowed roots", RootsHint);
        }

        /// <summary>
        ///     Determines whether a path as written lies inside a root, in the root's written or real spelling.
        /// </summary>
        /// <param name="path">The full path as written.</param>
        /// <param name="longPath">
        ///     The path with short (8.3) names expanded, captured before the open; <see langword="null"/> to expand it now
        ///     (before the open, or for a request built by other code).
        /// </param>
        /// <returns><see langword="true"/> when the path is inside a root.</returns>
        private bool IsWrittenPathInside(string path, string? longPath = null)
        {
            if (Array.Exists(_roots, root => root.ContainsWrittenPath(path)))
            {
                return true;
            }

            // A path written with short (8.3) names is compared in its long spelling too (Windows only; identity elsewhere)
            var expanded = longPath ?? LongSpelling(path);
            return !string.Equals(expanded, path, StringComparison.Ordinal)
                && Array.Exists(_roots, root => root.ContainsWrittenPath(expanded));
        }

        /// <summary>
        ///     One root: its full path as given, and its real location captured once, when the policy is created.
        /// </summary>
        /// <remarks>
        ///     The real location is never looked up again, so a root renamed and replaced by a link later (even between
        ///     the open of a file and its check) cannot widen the policy: the real path of a file is compared only with
        ///     the location captured here. A root that does not exist (or whose real location cannot be determined) when
        ///     the policy is created has no captured location and allows no file (fail closed), even once it is created.
        /// </remarks>
        private sealed class Root
        {
            /// <summary>
            ///     Initializes a new instance of the <see cref="Root"/> class, capturing the real location of the root.
            /// </summary>
            /// <param name="lexical">The full path of the root as given.</param>
            public Root(string lexical)
            {
                Lexical = lexical;
                Canonical = RealPath.TryGetCanonicalDirectory(lexical);
            }

            /// <summary>Gets the full path of the root as given.</summary>
            public string Lexical { get; }

            /// <summary>Gets the real location captured when the policy was created, or <see langword="null"/>.</summary>
            public string? Canonical { get; }

            /// <summary>
            ///     Determines whether a path as written lies inside the root, in its given or its captured real spelling.
            /// </summary>
            /// <param name="path">The full path.</param>
            /// <returns><see langword="true"/> when it is inside.</returns>
            public bool ContainsWrittenPath(string path) =>
                RealPath.IsUnder(path, Lexical) || (Canonical is { } canonical && RealPath.IsUnder(path, canonical));

            /// <summary>
            ///     Determines whether a real path lies inside the real location captured when the policy was created.
            /// </summary>
            /// <param name="path">The real path.</param>
            /// <returns><see langword="true"/> when it is inside.</returns>
            public bool ContainsRealPath(string path) => Canonical is { } canonical && RealPath.IsUnder(path, canonical);
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
        internal override IEnumerable<string> AnchorsOf(string requestedPath, string baseDirectory) =>
            [AnchorOf(requestedPath, baseDirectory)];

        /// <inheritdoc/>
        public override FileAccessDecision Check(FileAccessRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            // After an open by the resolver only what it captured before the open is used: an anchor it did not capture
            // is unknown (fail closed), and nothing is resolved by name. A request built by other code has no open in
            // progress, so the anchor is resolved now
            var anchor = AnchorOf(request.RequestedPath, request.BaseDirectory);
            string? canonicalAnchor;
            string longRequestedPath;
            if (request.Snapshot is { } snapshot)
            {
                snapshot.AnchorLocations.TryGetValue(anchor, out canonicalAnchor);
                longRequestedPath = snapshot.LongRequestedPath;
            }
            else
            {
                canonicalAnchor = RealPath.TryGetCanonicalDirectory(anchor);
                longRequestedPath = LongSpelling(request.RequestedPath);
            }

            return MatchesWithoutLinks(anchor, canonicalAnchor, request.RequestedPath, longRequestedPath, request.RealPath) switch
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

        /// <summary>
        ///     Gets the anchor of a reference: the deepest folder shared by the written path and the trusted base
        ///     directory (the origin of the chain, never a referring document's folder). It and everything above it are
        ///     trusted, so links there (macOS <c>/var</c>, an 8.3 or linked home folder) do not count.
        /// </summary>
        /// <param name="requestedPath">The full path as written.</param>
        /// <param name="baseDirectory">The trusted base directory.</param>
        /// <returns>The anchor.</returns>
        private static string AnchorOf(string requestedPath, string baseDirectory) =>
            RealPath.CommonAncestor(requestedPath, baseDirectory)
            ?? Path.GetPathRoot(requestedPath)
            ?? requestedPath;
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
        internal override IEnumerable<string> AnchorsOf(string requestedPath, string baseDirectory) =>
            Parts.SelectMany(part => part.AnchorsOf(requestedPath, baseDirectory));

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
