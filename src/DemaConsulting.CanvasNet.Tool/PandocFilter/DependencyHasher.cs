using System.Text;
using System.Text.RegularExpressions;
using DemaConsulting.CanvasNet.Files;
using DemaConsulting.CanvasNet.Viewer;

namespace DemaConsulting.CanvasNet.Tool.PandocFilter;

/// <summary>
///     Collects every file a picture is drawn from, as the parts of its cache key, so that a change to any of them draws
///     the picture again: a literal port of the dependency walks of the Lua filter (version 16).
/// </summary>
/// <remarks>
///     <para>
///         One instance walks one picture: <see cref="ForPicture"/> or <see cref="ForSvg"/> create it, walk, and return the
///         parts. A part is the file's label (its path from the picture's folder, see <see cref="FilterPaths.Label"/>), a
///         line feed, and the file's bytes; a missing file an SVG drawing links is the part <c>missing &lt;label&gt;</c>.
///         Each path is taken once (the exact collapsed text), so an include cycle or a picture that links back ends.
///         More than <see cref="MaxDepth"/> levels or <see cref="MaxDependencies"/> files stops the build with
///         <see cref="ErrorCodes.PandocFilterError"/>, never a silent cut-off: a file left out of the key could change
///         without the picture being drawn again.
///     </para>
///     <para>
///         The text of a model, a diagram, and a drawing is scanned with the Lua patterns on its bytes (read as Latin-1, so
///         that a character is a byte and only ASCII matches the classes); a chart is decoded as JSON. In a restrictive
///         file-access mode every file the picture references is read through the policy the tool will draw it under, so
///         a file the mode denies (outside the folders, or reached through a link the mode refuses) is never read
///         (<see cref="ReadFile"/>). The walks live in <c>DependencyHasher.Chart.cs</c>, <c>DependencyHasher.Mermaid.cs</c>,
///         <c>DependencyHasher.Svg.cs</c>, and this file (models and the shared picture walk). An instance is not
///         thread-safe; the static entry points are, as each call has its own instance.
///     </para>
/// </remarks>
internal sealed partial class DependencyHasher
{
    /// <summary>
    ///     The most levels of includes, nested charts and diagrams, linked drawings, or JSON nesting that are followed.
    /// </summary>
    internal const int MaxDepth = 64;

    /// <summary>
    ///     The most files one picture may be drawn from.
    /// </summary>
    internal const int MaxDependencies = 512;

    /// <summary>
    ///     The parts of the key, in the order they were met.
    /// </summary>
    private readonly List<byte[]> _parts = [];

    /// <summary>
    ///     The paths already taken, and for an SVG drawing whether it links a file only the tool draws.
    /// </summary>
    private readonly Dictionary<string, Seen> _seen = new(StringComparer.Ordinal);

    /// <summary>
    ///     The picture's source file (absolute and collapsed): the file the tool is given, which no mode judges.
    /// </summary>
    private readonly string _source;

    /// <summary>
    ///     The policy the tool draws the picture under, or <see langword="null"/> for the default mode (every file).
    /// </summary>
    private readonly FileAccessPolicy? _policy;

    /// <summary>
    ///     The resolver that reads the referenced files through <see cref="_policy"/>, created on first use.
    /// </summary>
    private FileReferenceResolver? _resolver;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DependencyHasher"/> class.
    /// </summary>
    /// <param name="source">The picture's source file (absolute and collapsed).</param>
    /// <param name="policy">The policy the tool draws the picture under, or <see langword="null"/> for the default mode.</param>
    private DependencyHasher(string source, FileAccessPolicy? policy)
    {
        _source = source;
        _policy = policy;
    }

    /// <summary>
    ///     Gets the policy that denies every file, for the arguments the tool refuses: the walk then reads no referenced
    ///     file at all, and the tool stops the build with its usage error.
    /// </summary>
    internal static FileAccessPolicy DenyAll { get; } =
        FileAccessPolicy.FromDelegate("deny-all", _ => FileAccessDecision.Deny("the file-access arguments are not valid"));

    /// <summary>
    ///     Whether a referenced file is taken by the walk, missing, or denied by the policy.
    /// </summary>
    private enum Availability
    {
        /// <summary>The file opens and the policy allows it.</summary>
        Taken,

        /// <summary>The file does not exist or cannot be read (the tool reports it).</summary>
        Missing,

        /// <summary>A restrictive mode refuses the file; it is never read.</summary>
        Denied
    }

    /// <summary>
    ///     What the walk knows about a path it has taken.
    /// </summary>
    private enum Seen
    {
        /// <summary>The path is being, or has been, walked.</summary>
        Taken,

        /// <summary>An SVG drawing that links a file only the tool draws, directly or through a nested drawing.</summary>
        Composite,

        /// <summary>An SVG drawing that links no such file.</summary>
        Plain
    }

    /// <summary>
    ///     Collects the files of a picture of a kind the tool draws.
    /// </summary>
    /// <param name="kind">The picture's kind (one of the <see cref="PictureAttributes"/> kind constants).</param>
    /// <param name="source">The picture's source file (absolute and collapsed).</param>
    /// <param name="name">The picture's name, for the message about an unreadable document.</param>
    /// <param name="policy">
    ///     The policy the tool draws the picture under (<see cref="FilterSettings.PolicyOf"/>), or <see langword="null"/>
    ///     for the default mode.
    /// </param>
    /// <returns>The parts of the key, in order.</returns>
    /// <exception cref="ToolException">
    ///     Thrown with <see cref="ErrorCodes.PandocFilterError"/> when a limit is exceeded, or a document (vsdx, pptx, pdf)
    ///     cannot be read.
    /// </exception>
    internal static IReadOnlyList<byte[]> ForPicture(string kind, string source, string name, FileAccessPolicy? policy)
    {
        var hasher = new DependencyHasher(source, policy);
        var root = FilterPaths.Directory(source);
        switch (kind)
        {
            case PictureAttributes.Scad:
                hasher.AddScad(root, source, 0);
                break;
            case PictureAttributes.VegaLite:
                hasher.AddChart(source, root, 0);
                break;
            case PictureAttributes.Mermaid:
                hasher.AddMermaid(source, root, 0);
                break;
            default:
                var content = hasher.ReadFile(source) ?? throw Fail($"cannot read '{name}'");
                hasher.AddPart(FilterPaths.FileName(source), content);
                break;
        }

        return hasher._parts;
    }

    /// <summary>
    ///     Collects the files of an SVG drawing, and tells whether the tool must flatten it.
    /// </summary>
    /// <param name="source">The drawing (absolute and collapsed).</param>
    /// <param name="policy">
    ///     The policy the tool flattens the drawing under (<see cref="FilterSettings.PolicyOf"/>), or
    ///     <see langword="null"/> for the default mode.
    /// </param>
    /// <returns>
    ///     The parts of the key, and whether the drawing links a file only the tool draws (a model, chart, diagram, or
    ///     document page, present or missing), directly or through a nested drawing.
    /// </returns>
    /// <exception cref="ToolException">Thrown with <see cref="ErrorCodes.PandocFilterError"/> when a limit is exceeded.</exception>
    internal static (IReadOnlyList<byte[]> Parts, bool Composite) ForSvg(string source, FileAccessPolicy? policy)
    {
        var hasher = new DependencyHasher(source, policy);
        var composite = hasher.AddSvg(source, FilterPaths.Directory(source), 0);
        return (hasher._parts, composite);
    }

    /// <summary>
    ///     Reads bytes as Latin-1, so that each character of the text is one byte and a Lua pattern can be matched on it.
    /// </summary>
    /// <param name="content">The bytes.</param>
    /// <returns>The text.</returns>
    internal static string AsBytes(byte[] content)
    {
        return Encoding.Latin1.GetString(content);
    }

    /// <summary>
    ///     Turns a text of bytes (see <see cref="AsBytes"/>) back into the UTF-8 text it holds.
    /// </summary>
    /// <param name="bytes">The text of bytes.</param>
    /// <returns>The text.</returns>
    internal static string FromBytes(string bytes)
    {
        return Encoding.UTF8.GetString(Encoding.Latin1.GetBytes(bytes));
    }

    /// <summary>
    ///     Adds a model file, and every file it includes that exists, to the parts (<c>add_scad_sources</c>).
    /// </summary>
    /// <param name="root">The folder labels are relative to.</param>
    /// <param name="path">The model file.</param>
    /// <param name="depth">The include depth.</param>
    /// <remarks>
    ///     Every <c>include &lt;...&gt;</c> and <c>use &lt;...&gt;</c> is followed, comments included, relative to the
    ///     including file's folder; an unreadable file is skipped (the tool reports it).
    /// </remarks>
    private void AddScad(string root, string path, int depth)
    {
        if (!_seen.TryAdd(path, Seen.Taken))
        {
            return;
        }

        if (depth > MaxDepth)
        {
            throw Fail($"'{path}' is included more than {MaxDepth} levels deep");
        }

        var content = ReadFile(path);
        if (content == null)
        {
            return;
        }

        if (_parts.Count >= MaxDependencies)
        {
            throw Fail($"the model and the files it includes are more than {MaxDependencies} files; the limit is {MaxDependencies}, counting the model (every file must be hashed so that a changed one is drawn again)");
        }

        AddPart(FilterPaths.Label(root, path), content);
        var folder = FilterPaths.Directory(path);
        var text = AsBytes(content);
        foreach (var pattern in new[] { ScadIncludePattern(), ScadUsePattern() })
        {
            foreach (Match match in pattern.Matches(text))
            {
                var next = FilterPaths.Collapse(FilterPaths.Join([folder, FromBytes(match.Groups[1].Value)]));
                if (DependencyExists(next))
                {
                    AddScad(root, next, depth + 1);
                }
            }
        }
    }

    /// <summary>
    ///     Adds the picture files of a chart, a diagram, or a drawing to the parts (<c>add_image_files</c>): a model with its
    ///     includes, a chart, diagram, or drawing with its own files, and any other file as it is.
    /// </summary>
    /// <param name="files">The files, sorted.</param>
    /// <param name="root">The folder labels are relative to.</param>
    /// <param name="depth">The nesting depth of charts, diagrams, and drawings.</param>
    private void AddImageFiles(IEnumerable<string> files, string root, int depth)
    {
        if (depth > MaxDepth)
        {
            throw Fail($"charts and diagrams are drawn inside each other more than {MaxDepth} levels deep");
        }

        foreach (var file in files)
        {
            if (_seen.ContainsKey(file))
            {
                continue;
            }

            var lower = FilterPaths.AsciiLower(file);
            if (lower.EndsWith(".scad", StringComparison.Ordinal))
            {
                AddScad(root, file, 0);
            }
            else if (lower.EndsWith(".vl.json", StringComparison.Ordinal) || lower.EndsWith(".vl", StringComparison.Ordinal))
            {
                _seen[file] = Seen.Taken;
                AddChart(file, FilterPaths.Directory(file), depth + 1);
            }
            else if (lower.EndsWith(".mmd", StringComparison.Ordinal) || lower.EndsWith(".mermaid", StringComparison.Ordinal))
            {
                AddMermaid(file, root, depth + 1);
            }
            else if (lower.EndsWith(".svg", StringComparison.Ordinal))
            {
                AddSvg(file, root, depth + 1);
            }
            else
            {
                _seen[file] = Seen.Taken;
                var data = ReadFile(file);
                if (data != null)
                {
                    if (_parts.Count >= MaxDependencies)
                    {
                        throw Fail($"the chart or diagram and the files it draws are more than {MaxDependencies} files; the limit is {MaxDependencies} (every file must be hashed so that a changed one is drawn again)");
                    }

                    AddPart(FilterPaths.Label(root, file), data);
                }
            }
        }
    }

    /// <summary>
    ///     Determines whether the walk takes a file a picture references (<c>dependency_exists</c>): it opens and, in a
    ///     restrictive mode, the policy allows it (see <see cref="ReadFile"/>).
    /// </summary>
    /// <param name="path">The file (absolute and collapsed).</param>
    /// <returns><see langword="true"/> when the file is taken.</returns>
    private bool DependencyExists(string path)
    {
        return Probe(path) == Availability.Taken;
    }

    /// <summary>
    ///     Tells whether the walk takes a file a picture references, and, when it does not, whether the file is missing or
    ///     the policy denies it.
    /// </summary>
    /// <param name="path">The file (absolute and collapsed).</param>
    /// <returns>
    ///     <see cref="Availability.Taken"/> when the file opens and the policy allows it; <see cref="Availability.Denied"/>
    ///     when a restrictive mode refuses it (its path is outside the roots, the real path of the opened file is denied, or
    ///     cannot be judged), decided without reading a byte of it; otherwise <see cref="Availability.Missing"/>.
    /// </returns>
    private Availability Probe(string path)
    {
        if (ReadsAsIs(path))
        {
            return FilterPaths.FileExists(path) ? Availability.Taken : Availability.Missing;
        }

        if (Resolve(path, out var denied) != null)
        {
            return Availability.Taken;
        }

        return denied ? Availability.Denied : Availability.Missing;
    }

    /// <summary>
    ///     Reads a file of the picture: the source, or a file it references.
    /// </summary>
    /// <param name="path">The file (absolute and collapsed).</param>
    /// <returns>The bytes, or <see langword="null"/> when the file cannot be read or the policy denies it.</returns>
    /// <remarks>
    ///     In the default mode, and for the source itself (the tool never judges the file it is given), the file is read
    ///     as it is. In a restrictive mode a referenced file is read as the tool reads it: through a
    ///     <see cref="FileReferenceResolver"/> with the tool's policy and the source's folder as the trusted origin, which
    ///     refuses a path outside the roots before the open and asks the policy about the real path of the open handle
    ///     before any byte is read. A file the policy denies, or that cannot be judged (fail closed), is therefore never
    ///     read: the walk treats it as a file that is not there, and the tool denies it when it draws.
    /// </remarks>
    private byte[]? ReadFile(string path)
    {
        return ReadsAsIs(path) ? FilterPaths.ReadFile(path) : Resolve(path, out _)?.Content.ToArray();
    }

    /// <summary>
    ///     Determines whether a file is read without the policy: in the default mode, or the source itself.
    /// </summary>
    /// <param name="path">The file (absolute and collapsed).</param>
    /// <returns><see langword="true"/> when the file is read as it is.</returns>
    private bool ReadsAsIs(string path)
    {
        return _policy == null || string.Equals(path, _source, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Opens and reads a referenced file through the policy; each file is opened at most once per picture.
    /// </summary>
    /// <param name="path">The file (absolute and collapsed).</param>
    /// <param name="denied">
    ///     Set to <see langword="true"/> when the policy refused the file (or cannot judge it, or the arguments are refused):
    ///     the resolver decides that before any byte is read.
    /// </param>
    /// <returns>The file, or <see langword="null"/> when it does not exist, cannot be read, or is denied.</returns>
    private FileReference? Resolve(string path, out bool denied)
    {
        denied = false;
        if (ReferenceEquals(_policy, DenyAll))
        {
            denied = true;
            return null;
        }

        // The size limit of the walk is the one it always had (none but the largest array), so the key of an allowed
        // file is unchanged; the tool applies its own limits when it draws
        _resolver ??= new FileReferenceResolver(new FileReferenceOptions
        {
            BaseDirectory = FilterPaths.Directory(_source),
            FileAccess = _policy!,
            MaxFileBytes = Array.MaxLength
        });

        try
        {
            return _resolver.Resolve(path);
        }
        catch (FileReferenceException ex)
        {
            denied = ex.Kind is FileReferenceFailureKind.AccessDenied or FileReferenceFailureKind.RealPathUnavailable;
            return null;
        }
    }

    /// <summary>
    ///     Adds a part: a label, a line feed, and the content.
    /// </summary>
    /// <param name="label">The label.</param>
    /// <param name="content">The file's bytes.</param>
    private void AddPart(string label, byte[] content)
    {
        _parts.Add([.. Encoding.UTF8.GetBytes(label + "\n"), .. content]);
    }

    /// <summary>
    ///     Creates the error of a dependency walk.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>The exception to throw.</returns>
    private static ToolException Fail(string message)
    {
        return new ToolException(ErrorCodes.PandocFilterError, message);
    }

    /// <summary>
    ///     Matches an <c>include &lt;file&gt;</c> of a model (<c>include%s*&lt;%s*([^&gt;\r\n]-)%s*&gt;</c>).
    /// </summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex("include[ \t\n\v\f\r]*<[ \t\n\v\f\r]*([^>\r\n]*?)[ \t\n\v\f\r]*>", RegexOptions.CultureInvariant)]
    private static partial Regex ScadIncludePattern();

    /// <summary>
    ///     Matches a <c>use &lt;file&gt;</c> of a model (<c>use%s*&lt;%s*([^&gt;\r\n]-)%s*&gt;</c>).
    /// </summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex("use[ \t\n\v\f\r]*<[ \t\n\v\f\r]*([^>\r\n]*?)[ \t\n\v\f\r]*>", RegexOptions.CultureInvariant)]
    private static partial Regex ScadUsePattern();
}
