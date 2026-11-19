// Portions derived from PlantUML (MIT); see third-party/README.md
using System.Globalization;
using System.Text;
using DemaConsulting.CanvasNet.Files;

namespace DemaConsulting.CanvasNet.PlantUml.Syntax;

/// <content>
///     The include directives of the preprocessor: <c>!include</c>, <c>!include_once</c> and <c>!include_many</c> of a
///     local file read that file in place, as PlantUML's preprocessor does.
/// </content>
/// <remarks>
///     <para>
///         <b>Semantics.</b> The path after the directive (variables substituted, one pair of quotes removed) names a
///         local file, looked up in the folder of the including file and then in the base directory. When the file has
///         an <c>@start</c> line, the content of its first block is included; otherwise the whole file (an
///         <c>.iuml</c> fragment). The included lines are read like the lines of the diagram: comments, continued lines,
///         directives (nested includes are relative to the included file), variables and macros, which the including
///         text and the included file share. A file is included by <c>!include</c> once (a second <c>!include</c> of
///         the same file is skipped, as PlantUML skips it); <c>!include_once</c> of a file that was already included is
///         an error; <c>!include_many</c> includes it again. The file of the diagram itself counts as included.
///     </para>
///     <para>
///         <b>Never followed.</b> A URL (<c>http://</c>, <c>https://</c>, any <c>scheme://</c>), the standard library
///         (<c>&lt;name&gt;</c>), a part of a file (<c>file!part</c> or <c>file!1</c>), <c>!includeurl</c>,
///         <c>!includesub</c>, <c>!includedef</c> and <c>!import</c> are reported as
///         <see cref="PlantUmlDiagnosticCodes.IncludeUnsupported"/> and dropped; the network is never used.
///     </para>
///     <para>
///         <b>Limits.</b> Includes nest at most <see cref="PlantUmlLimits.MaxIncludeDepth"/> deep and one diagram
///         follows at most <see cref="PlantUmlLimits.MaxIncludes"/> include directives
///         (<see cref="PlantUmlDiagnosticCodes.LimitExceeded"/>, located at the directive); every included statement
///         counts against <see cref="PlantUmlLimits.MaxTextSize"/>.
///     </para>
/// </remarks>
internal static partial class Preprocessor
{
    /// <summary>
    ///     The include part of the state of a preprocessing run.
    /// </summary>
    private sealed partial class State
    {
        /// <summary>The identities of the files included so far, compared as the file engine compares paths.</summary>
        private readonly HashSet<string> _included = new(includes?.KeyComparer ?? StringComparer.Ordinal);

        /// <summary>The file being read, or null for the text itself.</summary>
        private FileReference? _from;

        /// <summary>The include nesting depth of the file being read.</summary>
        private int _depth;

        /// <summary>The number of include directives followed so far.</summary>
        private int _includes;

        /// <summary>Whether the identity of the diagram's own file was recorded.</summary>
        private bool _rootRecorded;

        /// <summary>
        ///     Follows an include directive that reads a local file.
        /// </summary>
        /// <param name="statement">The directive statement.</param>
        /// <param name="name">The directive name (<c>include</c>, <c>include_once</c> or <c>include_many</c>).</param>
        /// <exception cref="PlantUmlSyntaxException">Thrown, located at the directive, when the include fails or a limit is exceeded.</exception>
        /// <exception cref="InvalidDataException">Thrown when the included file is longer than the limits allow.</exception>
        private void Include(SourceStatement statement, string name)
        {
            // The path may use variables, and may be quoted
            var substituted = Substitute(statement).Text;
            var path = substituted[(name.Length + 1)..].Trim();
            if (path.Length >= 2 && path[0] == '"' && path[^1] == '"')
            {
                path = path[1..^1].Trim();
            }

            if (path.Length == 0)
            {
                throw new PlantUmlSyntaxException(
                    PlantUmlDiagnosticCodes.IncludeFailed,
                    $"'!{name}' has no file name",
                    statement.Location,
                    $"write the path of a local file after '!{name}'");
            }

            if (UnfollowedReason(path) is { } reason)
            {
                UnfollowedInclude(statement, reason);
                return;
            }

            if (includes is null || !includes.HasSource)
            {
                throw new PlantUmlSyntaxException(
                    PlantUmlDiagnosticCodes.IncludeFailed,
                    $"the file '{path}' of '!{name}' cannot be found: the diagram has no folder",
                    statement.Location,
                    "open the diagram from its file, or set PlantUmlOpenOptions.BaseDirectory");
            }

            if (_depth >= limits.MaxIncludeDepth)
            {
                throw new PlantUmlSyntaxException(
                    PlantUmlDiagnosticCodes.LimitExceeded,
                    string.Create(CultureInfo.InvariantCulture, $"the includes nest deeper than the limit of {limits.MaxIncludeDepth} (PlantUmlLimits.MaxIncludeDepth)"),
                    statement.Location,
                    "include the files from fewer levels");
            }

            if (++_includes > limits.MaxIncludes)
            {
                throw new PlantUmlSyntaxException(
                    PlantUmlDiagnosticCodes.LimitExceeded,
                    string.Create(CultureInfo.InvariantCulture, $"the diagram includes more than {limits.MaxIncludes} files (PlantUmlLimits.MaxIncludes)"),
                    statement.Location,
                    "include fewer files, or raise PlantUmlLimits.MaxIncludes when the source is trusted");
            }

            if (!_rootRecorded)
            {
                _rootRecorded = true;
                if (includes.RootIdentity is { } root)
                {
                    _included.Add(root);
                }
            }

            var file = includes.Load(path, _from, statement, name);
            var seen = !_included.Add(file.Identity);
            if (seen && name == "include")
            {
                return;
            }

            if (seen && name == "include_once")
            {
                throw new PlantUmlSyntaxException(
                    PlantUmlDiagnosticCodes.IncludeFailed,
                    $"the file '{file.Lines.SourceName}' was already included",
                    statement.Location,
                    "remove the second '!include_once', or use '!include_many' to include the file again");
            }

            ReadIncluded(file);
        }

        /// <summary>
        ///     Reads the lines of an included file in place, with its own line table, folder and depth.
        /// </summary>
        /// <param name="file">The included file.</param>
        private void ReadIncluded(IncludedFile file)
        {
            var (first, end, startsInComment) = IncludedRange(file.Lines);
            var (lines, from, depth, inComment) = (_lines, _from, _depth, _inComment);
            (_lines, _from, _depth) = (file.Lines, file.File, _depth + 1);
            try
            {
                Read(first, end, startsInComment);
            }
            finally
            {
                (_lines, _from, _depth, _inComment) = (lines, from, depth, inComment);
            }
        }

        /// <summary>
        ///     Reports an include that is never followed.
        /// </summary>
        /// <param name="statement">The directive statement.</param>
        /// <param name="reason">Why it is not followed.</param>
        private void UnfollowedInclude(SourceStatement statement, string reason) =>
            reports.Add(PlantUmlPendingReport.Unsupported(
                PlantUmlDiagnosticCodes.IncludeUnsupported,
                reason,
                statement.Location,
                "copy the included statements into the diagram, or save them in a local file and include it by its path"));
    }

    /// <summary>
    ///     Decides whether the path of an include names something that is never followed.
    /// </summary>
    /// <param name="path">The path as written.</param>
    /// <returns>The reason it is not followed, or null for a local file.</returns>
    internal static string? UnfollowedReason(string path)
    {
        if (path.StartsWith('<'))
        {
            return $"the standard library '{path}' is not included; what it would include is not drawn";
        }

        if (path.Contains("://", StringComparison.Ordinal))
        {
            return $"the URL '{path}' is never read; what it would include is not drawn";
        }

        if (path.StartsWith('['))
        {
            return $"'{path}' is not a local file; what it would include is not drawn";
        }

        if (path.LastIndexOf('!') > 0)
        {
            return $"only whole files are included, not the part '{path[(path.LastIndexOf('!') + 1)..]}' of '{path[..path.LastIndexOf('!')]}'";
        }

        return null;
    }

    /// <summary>
    ///     Gets the lines of an included file that are included: the content of its first block when it has an
    ///     <c>@start</c> line, otherwise the whole file. Tags inside comments are not tags, as in the diagram's framing.
    /// </summary>
    /// <param name="lines">The line table of the file.</param>
    /// <returns>The first line index, the index after the last line, and whether the first line starts inside a block comment.</returns>
    private static (int First, int End, bool InComment) IncludedRange(SourceLines lines)
    {
        var inComment = false;
        var openedAt = -1;
        var start = FindTag(lines, 0, "@start", ref inComment, ref openedAt);
        if (start < 0)
        {
            return (0, lines.Count, false);
        }

        var contentInComment = inComment;
        var end = FindTag(lines, start + 1, "@end", ref inComment, ref openedAt);
        return (start + 1, end < 0 ? lines.Count : end, contentInComment);
    }
}

/// <summary>
///     A file read for an include directive: its line table (named by its display name) and the file the core engine
///     returned, whose folder the file's own includes start in.
/// </summary>
/// <param name="Lines">The line table, whose source name is the display name of the file.</param>
/// <param name="File">The file as read.</param>
internal sealed record IncludedFile(SourceLines Lines, FileReference File)
{
    /// <summary>
    ///     Gets the identity of the file: its real path, or its path when the real path is unknown.
    /// </summary>
    public string Identity => File.RealPath ?? File.Key;
}

/// <summary>
///     Finds, checks and reads the local files of the include directives of one open call, through the core
///     <see cref="FileReferenceResolver"/> under the caller's <see cref="FileAccessPolicy"/>.
/// </summary>
/// <remarks>
///     <para>
///         Everything that makes the access safe is done by the core engine: the validation of the path text (network
///         and device paths, alternate data streams, control characters), the policy judged on the real path of the
///         opened file, the regular-file check, and the bounded read. A file is read once per call and decoded as strict
///         UTF-8 (a byte-order mark is removed); it is named in locations by its path relative to the base directory
///         (forward slashes, with leading <c>..</c> segments for a file outside it), or by its file name when it has no
///         such path, so no location shows a host directory.
///     </para>
///     <para>
///         A missing file, a refused file (whose <see cref="Exception.InnerException"/> is the
///         <see cref="FileReferenceException"/>), and a file that is not valid UTF-8 are
///         <see cref="PlantUmlDiagnosticCodes.IncludeFailed"/> errors at the directive; a file longer than the text
///         limit is an <see cref="InvalidDataException"/>. An instance is single-use and not thread-safe.
///     </para>
/// </remarks>
internal sealed class IncludeResolver
{
    /// <summary>The strict UTF-8 decoder: invalid bytes are an error, never U+FFFD.</summary>
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>The core engine that finds, checks and reads the files.</summary>
    private readonly FileReferenceResolver _engine;

    /// <summary>The limits of the open call.</summary>
    private readonly PlantUmlLimits _limits;

    /// <summary>The base directory, or null.</summary>
    private readonly string? _baseDirectory;

    /// <summary>The files read so far, by key.</summary>
    private readonly Dictionary<string, IncludedFile> _files;

    /// <summary>
    ///     Initializes a resolver for one open call.
    /// </summary>
    /// <param name="baseDirectory">The directory relative paths start in, or null.</param>
    /// <param name="referrerPath">The full path of the diagram's own file, or null.</param>
    /// <param name="fileAccess">The policy that decides which files may be read.</param>
    /// <param name="limits">The limits of the open call.</param>
    public IncludeResolver(string? baseDirectory, string? referrerPath, FileAccessPolicy fileAccess, PlantUmlLimits limits)
    {
        ArgumentNullException.ThrowIfNull(fileAccess);
        ArgumentNullException.ThrowIfNull(limits);
        _limits = limits;
        _baseDirectory = baseDirectory;
        RootIdentity = referrerPath;
        _engine = new FileReferenceResolver(new FileReferenceOptions
        {
            BaseDirectory = baseDirectory,
            ReferrerPath = referrerPath,
            FileAccess = fileAccess,

            // Three bytes per character of the text limit (the widest UTF-8 of a UTF-16 unit) and a byte-order mark
            MaxFileBytes = (3L * limits.MaxTextSize * Preprocessor.SourceLengthFactor) + 3,
        });
        _files = new Dictionary<string, IncludedFile>(_engine.KeyComparer);
    }

    /// <summary>
    ///     Gets a value indicating whether the resolver has a place to look for files.
    /// </summary>
    public bool HasSource => _engine.HasSource;

    /// <summary>
    ///     Gets the comparer under which two file identities name the same file (the platform's path comparison).
    /// </summary>
    public StringComparer KeyComparer => _engine.KeyComparer;

    /// <summary>
    ///     Gets the identity of the diagram's own file (its full path), or null when it has none.
    /// </summary>
    public string? RootIdentity { get; }

    /// <summary>
    ///     Finds, reads and decodes the file of an include directive.
    /// </summary>
    /// <param name="path">The path as written.</param>
    /// <param name="from">The including file, or null for the diagram itself.</param>
    /// <param name="directive">The directive statement, where errors are located.</param>
    /// <param name="name">The directive name, for the messages.</param>
    /// <returns>The file.</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the file is missing, refused or not valid UTF-8.</exception>
    /// <exception cref="InvalidDataException">Thrown when the file is longer than the limits allow.</exception>
    public IncludedFile Load(string path, FileReference? from, SourceStatement directive, string name)
    {
        FileReference? found;
        try
        {
            found = _engine.Resolve(path, from);
        }
        catch (FileReferenceException ex)
        {
            throw Translate(ex, directive.Location);
        }

        if (found is null)
        {
            throw new PlantUmlSyntaxException(
                PlantUmlDiagnosticCodes.IncludeFailed,
                $"the file '{path}' of '!{name}' does not exist",
                directive.Location,
                "check the path, which is relative to the folder of the including file");
        }

        if (_files.TryGetValue(found.Key, out var known))
        {
            return known;
        }

        var display = DisplayName(found.Key);
        var data = found.Content.Span;
        if (data.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
        {
            data = data[3..];
        }

        string text;
        try
        {
            text = StrictUtf8.GetString(data);
        }
        catch (DecoderFallbackException)
        {
            throw new PlantUmlSyntaxException(
                PlantUmlDiagnosticCodes.IncludeFailed,
                $"the included file '{display}' is not valid UTF-8",
                directive.Location,
                "save the included file with UTF-8 encoding");
        }

        if ((long)text.Length > (long)_limits.MaxTextSize * Preprocessor.SourceLengthFactor)
        {
            throw TooLong();
        }

        var file = new IncludedFile(new SourceLines(text, display), found);
        _files[found.Key] = file;
        return file;
    }

    /// <summary>
    ///     Translates a refusal of the file engine into a located error.
    /// </summary>
    /// <param name="ex">The refusal.</param>
    /// <param name="location">The location of the directive.</param>
    /// <returns>The exception to throw: a located include error whose inner exception is the refusal, or the text-size error.</returns>
    private Exception Translate(FileReferenceException ex, PlantUmlLocation location)
    {
        var shown = ex.RequestedPath is { } requested && RelativeToBase(requested) is { } relative && !IsOutside(relative) ? relative : ex.Reference;
        var (description, hint) = ex.Kind switch
        {
            FileReferenceFailureKind.EmptyPath or FileReferenceFailureKind.ControlCharacter or FileReferenceFailureKind.InvalidPath
                or FileReferenceFailureKind.NetworkPath =>
                ($"the include path '{ex.Reference}' is not allowed: {ex.Reason}", "include a local file by a plain relative or absolute path"),
            FileReferenceFailureKind.AccessDenied =>
                ($"the included file '{shown}' was denied by the file-access policy '{ex.PolicyName}': {ex.Reason}", "include a file that the file-access policy allows"),
            FileReferenceFailureKind.NotARegularFile =>
                ($"the included file '{shown}' is not a regular file", "include a file, not a directory or a device"),
            FileReferenceFailureKind.InvalidBaseDirectory =>
                ("the base directory is not a valid path", "set PlantUmlOpenOptions.BaseDirectory to a valid directory"),
            _ => ($"the included file '{shown}' cannot be read", "check that the file can be read"),
        };

        return ex.Kind == FileReferenceFailureKind.TooLarge
            ? TooLong()
            : new PlantUmlSyntaxException(ex, PlantUmlDiagnosticCodes.IncludeFailed, description, location, hint);
    }

    /// <summary>
    ///     Creates the error of an included file longer than the text limit.
    /// </summary>
    /// <returns>The exception.</returns>
    private InvalidDataException TooLong() =>
        new(string.Create(
            CultureInfo.InvariantCulture,
            $"An included PlantUML file is longer than {Preprocessor.SourceLengthFactor} times the limit of {_limits.MaxTextSize} characters (PlantUmlLimits.MaxTextSize)."));

    /// <summary>
    ///     Gets the name of a file in locations.
    /// </summary>
    /// <param name="key">The full path of the file.</param>
    /// <returns>The path relative to the base directory with forward slashes, or the file name.</returns>
    private string DisplayName(string key) => RelativeToBase(key) ?? Path.GetFileName(key);

    /// <summary>
    ///     Computes the path of a file relative to the base directory, with forward slashes.
    /// </summary>
    /// <param name="key">The full path.</param>
    /// <returns>The relative path, or null without a base directory or on another root.</returns>
    private string? RelativeToBase(string key)
    {
        if (_baseDirectory is null)
        {
            return null;
        }

        var relative = Path.GetRelativePath(Path.GetFullPath(_baseDirectory), key);
        return Path.IsPathRooted(relative) ? null : relative.Replace('\\', '/');
    }

    /// <summary>
    ///     Determines whether a relative path leaves its directory.
    /// </summary>
    /// <param name="relative">The relative path with forward slashes.</param>
    /// <returns>True when it starts with a <c>..</c> segment.</returns>
    private static bool IsOutside(string relative) =>
        relative == ".." || relative.StartsWith("../", StringComparison.Ordinal);
}
