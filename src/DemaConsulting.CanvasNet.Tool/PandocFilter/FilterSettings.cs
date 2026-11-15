using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DemaConsulting.CanvasNet.Files;
using DemaConsulting.CanvasNet.Tool.Commands;
using DemaConsulting.CanvasNet.Viewer;

namespace DemaConsulting.CanvasNet.Tool.PandocFilter;

/// <summary>
///     The file-access arguments of the pictures drawn from one source.
/// </summary>
/// <param name="Arguments">
///     The arguments passed to every render and flatten: <c>--file-access MODE</c> and one <c>--allow-root FOLDER</c> per
///     folder; empty in the default mode.
/// </param>
/// <param name="Roots">
///     The folders of a roots mode, each passed as <c>--allow-root</c>, or <see langword="null"/> for anywhere. The
///     dependency walk judges files by the policy built from <paramref name="Arguments"/> (<see cref="FilterSettings.PolicyOf"/>).
/// </param>
/// <param name="Restrictive">Whether a denied picture stops the build: a mode other than <c>any</c>, or folders.</param>
/// <param name="Key">The arguments as part of every picture's cache key: <c>file-access</c> and each argument, one per line.</param>
internal sealed record FileAccessArguments(
    IReadOnlyList<string> Arguments,
    IReadOnlyList<string>? Roots,
    bool Restrictive,
    string Key);

/// <summary>
///     The inputs of one run of the Pandoc filter: what only the Lua shim can see (the format, the output file, the
///     resource path, the filter's location), the environment variables of the filter, and what follows from them (the
///     cache folder, the file-access arguments, the tool identity, and the <c>src</c> of a picture).
/// </summary>
/// <remarks>
///     <para>
///         The environment and the working directory are inputs (<see cref="Environment"/>,
///         <see cref="WorkingDirectory"/>), so a test needs no process state. The derived values are computed on first use
///         and kept, exactly when the Lua filter computed them: a malformed <c>CANVASNET_FILE_ACCESS</c> or
///         <c>CANVASNET_ROOTS</c> stops the build only when a picture needs the file-access arguments, as before.
///     </para>
///     <para>
///         Not thread-safe: the engine reads it only while it plans, in document order, on one thread.
///     </para>
/// </remarks>
internal sealed class FilterSettings
{
    /// <summary>The variable that replaces the command <c>dotnet canvasnet</c>.</summary>
    internal const string ToolVariable = "CANVASNET_TOOL";

    /// <summary>The variable that chooses a restrictive file-access mode.</summary>
    internal const string FileAccessVariable = "CANVASNET_FILE_ACCESS";

    /// <summary>The variable that lists the folders of a roots mode.</summary>
    internal const string RootsVariable = "CANVASNET_ROOTS";

    /// <summary>The variable that sets how many pictures are drawn at once.</summary>
    internal const string JobsVariable = "CANVASNET_JOBS";

    /// <summary>The most pictures drawn at once.</summary>
    internal const int MaxJobs = 64;

    /// <summary>The default number of pictures drawn at once, at most (fewer on a machine with fewer processors).</summary>
    internal const int DefaultJobs = 4;

    /// <summary>
    ///     The modes <c>CANVASNET_FILE_ACCESS</c> may name, as the tool's <c>--file-access</c> reads them.
    /// </summary>
    private static readonly HashSet<string> FileAccessModes = new(StringComparer.Ordinal)
    {
        "any", "roots", "no-symlinks", "roots,no-symlinks", "no-symlinks,roots"
    };

    /// <summary>
    ///     The writers whose output is read back relative to the output file (HTML and slide shows).
    /// </summary>
    private static readonly string[] HtmlWriters = ["html", "revealjs", "slidy", "slideous", "s5", "dzslides"];

    /// <summary>
    ///     The cache folder, once computed.
    /// </summary>
    private string? _cacheFolder;

    /// <summary>
    ///     The tool identity, once computed.
    /// </summary>
    private string? _toolIdentity;

    /// <summary>
    ///     The file-access arguments shared by every picture, once read.
    /// </summary>
    private SharedFileAccess? _fileAccess;

    /// <summary>
    ///     Gets the path of the Lua shim as Pandoc names it (<c>PANDOC_SCRIPT_FILE</c>): the cache folder is
    ///     <c>generated/canvasnet</c> beside the shim's folder.
    /// </summary>
    public required string FilterPath { get; init; }

    /// <summary>
    ///     Gets the working directory (absolute): relative paths are taken from it.
    /// </summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>
    ///     Gets the reader of the environment variables; <see langword="null"/> is an unset variable.
    /// </summary>
    public required Func<string, string?> Environment { get; init; }

    /// <summary>
    ///     Gets the output format (<c>FORMAT</c>), for example <c>html5</c>; empty when unknown.
    /// </summary>
    public string Format { get; init; } = string.Empty;

    /// <summary>
    ///     Gets the output file of Pandoc (<c>PANDOC_STATE.output_file</c>), or <see langword="null"/> for standard output.
    /// </summary>
    public string? OutputFile { get; init; }

    /// <summary>
    ///     Gets a value indicating whether Pandoc embeds the resources in the output (<c>--embed-resources</c>).
    /// </summary>
    public bool EmbedResources { get; init; }

    /// <summary>
    ///     Gets the folders of the document's resource path, as Pandoc gives them, in order; <c>.</c> by default.
    /// </summary>
    public IReadOnlyList<string> ResourcePaths { get; init; } = ["."];

    /// <summary>
    ///     Gets the version text of the tool, part of every picture's key; the running tool's version by default.
    /// </summary>
    public string ToolVersion { get; init; } = Program.Version;

    /// <summary>
    ///     Gets the cache folder: <c>generated/canvasnet</c> in the folder above the shim's folder (absolute and collapsed),
    ///     so <c>docs/filters/canvasnet.lua</c> draws into <c>docs/generated/canvasnet</c>.
    /// </summary>
    public string CacheFolder => _cacheFolder ??= ComputeCacheFolder();

    /// <summary>
    ///     Gets the resource folders, absolute and collapsed, in order.
    /// </summary>
    /// <returns>The folders.</returns>
    public IReadOnlyList<string> GetResourceDirectories()
    {
        return [.. ResourcePaths.Select(dir => FilterPaths.Absolute(dir, WorkingDirectory))];
    }

    /// <summary>
    ///     Gets the tool identity, part of every picture's key: the version text, the command label (<c>default</c> or
    ///     <c>CANVASNET_TOOL=...</c>), and the fingerprint of a local build, one per line.
    /// </summary>
    /// <exception cref="ToolException">Thrown with <see cref="ErrorCodes.PandocFilterError"/> when <c>CANVASNET_TOOL</c> cannot be split.</exception>
    /// <remarks>
    ///     A local build named by <c>CANVASNET_TOOL</c> (<c>dotnet path/Tool.dll</c>) keeps its version text while its code
    ///     changes, so the first leading argument that ends in <c>.dll</c> and exists adds <c>name=sha1</c> of every
    ///     <c>.dll</c> beside it, sorted and joined with <c>;</c>.
    /// </remarks>
    public string ToolIdentity => _toolIdentity ??= ComputeToolIdentity();

    /// <summary>
    ///     Resolves how many pictures are drawn at once: <c>--jobs</c>, else <c>CANVASNET_JOBS</c>, else the number of
    ///     processors up to <see cref="DefaultJobs"/>.
    /// </summary>
    /// <param name="option">The value of <c>--jobs</c>, already checked, or <see langword="null"/> when not given.</param>
    /// <param name="environment">The reader of the environment variables.</param>
    /// <returns>The number of pictures drawn at once, 1 to <see cref="MaxJobs"/>; 1 draws them one after the other.</returns>
    /// <exception cref="ToolException">Thrown with <see cref="ErrorCodes.PandocFilterError"/> when <c>CANVASNET_JOBS</c> is not a whole number from 1 to <see cref="MaxJobs"/>.</exception>
    internal static int ResolveJobs(int? option, Func<string, string?> environment)
    {
        if (option is { } jobs)
        {
            return jobs;
        }

        var text = environment(JobsVariable);
        if (text == null || !FilterPaths.HasNonSpace(text))
        {
            return Math.Min(System.Environment.ProcessorCount, DefaultJobs);
        }

        var trimmed = FilterPaths.TrimLuaSpace(text);
        if (trimmed.All(char.IsAsciiDigit) && int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value is >= 1 and <= MaxJobs)
        {
            return value;
        }

        throw new ToolException(ErrorCodes.PandocFilterError, $"{JobsVariable} is '{text}'; write a whole number from 1 up to {MaxJobs}");
    }

    /// <summary>
    ///     Splits a tool command into the program and its leading arguments (<c>split_command</c>): white space separates,
    ///     a double quote groups (including spaces), and there are no escapes, so a Windows path needs none.
    /// </summary>
    /// <param name="text">The command.</param>
    /// <returns>The program followed by its leading arguments.</returns>
    /// <exception cref="ToolException">
    ///     Thrown with <see cref="ErrorCodes.PandocFilterError"/> for an empty quoted part, a quote that is never closed, or
    ///     no program.
    /// </exception>
    internal static IReadOnlyList<string> SplitCommand(string text)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        var started = false;
        void Finish()
        {
            if (current.Length == 0)
            {
                throw new ToolException(ErrorCodes.PandocFilterError, $"{ToolVariable} has an empty quoted part: {text}");
            }

            tokens.Add(current.ToString());
            current.Clear();
            started = false;
        }

        foreach (var c in text)
        {
            if (c == '"')
            {
                quoted = !quoted;
                started = true;
            }
            else if (!quoted && FilterPaths.IsLuaSpace(c))
            {
                if (started)
                {
                    Finish();
                }
            }
            else
            {
                current.Append(c);
                started = true;
            }
        }

        if (quoted)
        {
            throw new ToolException(ErrorCodes.PandocFilterError, $"{ToolVariable} has a quote that is never closed: {text}");
        }

        if (started)
        {
            Finish();
        }

        return tokens.Count > 0 ? tokens : throw new ToolException(ErrorCodes.PandocFilterError, $"{ToolVariable} names no program");
    }

    /// <summary>
    ///     Gets the file-access arguments of the pictures drawn from a source (<c>file_access_of</c>).
    /// </summary>
    /// <param name="source">The picture's source file.</param>
    /// <returns>
    ///     The shared arguments; for the mode <c>roots</c> without <c>CANVASNET_ROOTS</c>, the folders the tool would take by
    ///     default added as <c>--allow-root</c>: the working directory, and the source's folder when it is outside it.
    /// </returns>
    /// <exception cref="ToolException">
    ///     Thrown with <see cref="ErrorCodes.PandocFilterError"/> when <c>CANVASNET_FILE_ACCESS</c> names an unknown mode, or
    ///     <c>CANVASNET_ROOTS</c> is set with the mode <c>any</c> or <c>no-symlinks</c>.
    /// </exception>
    public FileAccessArguments FileAccessOf(string source)
    {
        var shared = _fileAccess ??= ReadFileAccess();
        if (!shared.DefaultRoots)
        {
            return shared.Arguments;
        }

        // The tool's decision then depends only on its arguments, which are part of the picture's name
        var arguments = shared.Arguments.Arguments.ToList();
        var working = FilterPaths.Absolute(WorkingDirectory, WorkingDirectory);
        var folder = FilterPaths.Directory(FilterPaths.Absolute(source, WorkingDirectory));
        var roots = new List<string> { working };
        arguments.AddRange(["--allow-root", working]);
        if (!FilterPaths.IsSameOrUnder(folder, working))
        {
            arguments.AddRange(["--allow-root", folder]);
            roots.Add(folder);
        }

        return new FileAccessArguments(arguments, roots, shared.Arguments.Restrictive, KeyOf(arguments));
    }

    /// <summary>
    ///     Gets the policy the tool will judge the files of a picture by, so that the dependency walk reads only what the
    ///     tool may read when it draws the picture.
    /// </summary>
    /// <param name="access">The file-access arguments of the picture (<see cref="FileAccessOf"/>).</param>
    /// <param name="source">The picture's source file, the input the tool is given.</param>
    /// <returns>
    ///     <see langword="null"/> in the default mode (every file is read as it is); otherwise the policy the tool builds
    ///     from the same arguments (<see cref="RenderCommand.ReadFileAccess(IReadOnlyList{string}, IReadOnlyList{string}, string)"/>),
    ///     or <see cref="DependencyHasher.DenyAll"/> when the tool refuses the arguments (a folder of
    ///     <c>CANVASNET_ROOTS</c> that does not exist): no referenced file is read, and the tool stops the build with its
    ///     usage error (fail closed).
    /// </returns>
    public FileAccessPolicy? PolicyOf(FileAccessArguments access, string source)
    {
        if (!access.Restrictive)
        {
            return null;
        }

        try
        {
            return RenderCommand.ReadFileAccess(access.Arguments, [source], WorkingDirectory);
        }
        catch (ToolException)
        {
            return DependencyHasher.DenyAll;
        }
    }

    /// <summary>
    ///     Gets the <c>src</c> a picture gets (<c>picture_src</c>).
    /// </summary>
    /// <param name="file">The picture's file in the cache folder.</param>
    /// <returns>
    ///     For an HTML writer without embedding and with an output file, the path from the output file's folder (WeasyPrint
    ///     reads the picture from there); otherwise the absolute path with forward slashes, which Pandoc itself can read.
    /// </returns>
    public string PictureSrc(string file)
    {
        var html = HtmlWriters.Any(writer => Format.StartsWith(writer, StringComparison.Ordinal));
        if (html && !EmbedResources && !string.IsNullOrEmpty(OutputFile))
        {
            var relative = FilterPaths.RelativePath(FilterPaths.Directory(FilterPaths.Absolute(OutputFile, WorkingDirectory)), file);
            if (relative != null)
            {
                return relative;
            }
        }

        return file.Replace('\\', '/');
    }

    /// <summary>
    ///     Computes the lowercase hexadecimal SHA-1 of bytes, as <c>pandoc.sha1</c>.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The forty hexadecimal digits.</returns>
    /// <remarks>
    ///     SHA-1 names cached content (the key the Lua filter used); it is not a security control, so the weak-hash rules
    ///     do not apply.
    /// </remarks>
    internal static string Sha1Hex(byte[] bytes)
    {
#pragma warning disable CA5350, S4790 // SHA-1 names cached content and is not a security control
        return Convert.ToHexStringLower(SHA1.HashData(bytes));
#pragma warning restore CA5350, S4790
    }

    /// <summary>
    ///     Computes the cache folder.
    /// </summary>
    /// <returns>The folder.</returns>
    private string ComputeCacheFolder()
    {
        var filters = FilterPaths.Directory(FilterPaths.Absolute(FilterPath, WorkingDirectory));
        return FilterPaths.Join([FilterPaths.Directory(filters), "generated", "canvasnet"]);
    }

    /// <summary>
    ///     Computes the tool identity (<c>find_tool</c>).
    /// </summary>
    /// <returns>The identity.</returns>
    private string ComputeToolIdentity()
    {
        var command = Environment(ToolVariable);
        IReadOnlyList<string> parts;
        string label;
        if (command != null && FilterPaths.HasNonSpace(command))
        {
            parts = SplitCommand(command);
            label = ToolVariable + "=" + command;
        }
        else
        {
            parts = ["dotnet", "canvasnet"];
            label = "default";
        }

        // The DLLs beside the first leading .dll argument that exists, sorted
        var fingerprint = string.Empty;
        foreach (var part in parts.Skip(1))
        {
            var path = FilterPaths.Absolute(part, WorkingDirectory);
            if (!FilterPaths.AsciiLower(part).EndsWith(".dll", StringComparison.Ordinal) || !FilterPaths.FileExists(path))
            {
                continue;
            }

            var folder = FilterPaths.Directory(path);
            var entries = new List<string>();
            foreach (var name in System.IO.Directory.EnumerateFileSystemEntries(folder).Select(System.IO.Path.GetFileName).OfType<string>())
            {
                if (FilterPaths.AsciiLower(name).EndsWith(".dll", StringComparison.Ordinal)
                    && FilterPaths.ReadFile(FilterPaths.Join([folder, name])) is { } content)
                {
                    entries.Add(name + "=" + Sha1Hex(content));
                }
            }

            FilterPaths.SortBytes(entries);
            fingerprint = string.Join(';', entries);
            break;
        }

        return string.Join('\n', FilterPaths.TrimLuaSpaceEnd(ToolVersion), label, fingerprint);
    }

    /// <summary>
    ///     Reads <c>CANVASNET_FILE_ACCESS</c> and <c>CANVASNET_ROOTS</c> (<c>find_file_access</c>).
    /// </summary>
    /// <returns>The shared arguments.</returns>
    private SharedFileAccess ReadFileAccess()
    {
        // The mode, trimmed and lowercase, without white space around its commas
        var mode = Environment(FileAccessVariable) is { } text
            ? string.Join(',', FilterPaths.AsciiLower(FilterPaths.TrimLuaSpace(text)).Split(',').Select((p, i) => TrimAroundComma(p, i)))
            : null;
        if (mode?.Length == 0)
        {
            mode = null;
        }

        if (mode != null && !FileAccessModes.Contains(mode))
        {
            throw new ToolException(ErrorCodes.PandocFilterError, $"{FileAccessVariable} is '{mode}'; use roots, no-symlinks, roots,no-symlinks, or any");
        }

        // The folders, each absolute
        var roots = new List<string>();
        if (Environment(RootsVariable) is { } list && FilterPaths.HasNonSpace(list))
        {
            roots.AddRange(FilterPaths.SplitSearchPath(list).Where(FilterPaths.HasNonSpace).Select(entry => FilterPaths.Absolute(entry, WorkingDirectory)));
        }

        if (roots.Count > 0 && mode is "any" or "no-symlinks")
        {
            throw new ToolException(ErrorCodes.PandocFilterError, $"{RootsVariable} cannot be used with {FileAccessVariable}={mode}; use roots or roots,no-symlinks");
        }

        // Folders imply roots; the mode is passed explicitly rather than left to the tool's default
        if (roots.Count > 0 && mode == null)
        {
            mode = "roots";
        }

        var arguments = new List<string>();
        if (mode != null)
        {
            arguments.AddRange(["--file-access", mode]);
        }

        foreach (var root in roots)
        {
            arguments.AddRange(["--allow-root", root]);
        }

        var restrictive = roots.Count > 0 || (mode != null && mode != "any");
        var defaultRoots = roots.Count == 0 && mode != null && mode.Contains("roots", StringComparison.Ordinal);
        return new SharedFileAccess(new FileAccessArguments(arguments, roots.Count > 0 ? roots : null, restrictive, KeyOf(arguments)), defaultRoots);
    }

    /// <summary>
    ///     Removes the white space around a comma of the mode (<c>gsub("%s*,%s*", ",")</c>): the end of every part but the
    ///     last and the start of every part but the first.
    /// </summary>
    /// <param name="part">A part of the mode between commas.</param>
    /// <param name="index">The part's position.</param>
    /// <returns>The part.</returns>
    private static string TrimAroundComma(string part, int index)
    {
        return index == 0 ? FilterPaths.TrimLuaSpaceEnd(part) : FilterPaths.TrimLuaSpace(part);
    }

    /// <summary>
    ///     Gets the key text of file-access arguments.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <returns><c>file-access</c> and each argument, one per line.</returns>
    private static string KeyOf(IReadOnlyList<string> arguments)
    {
        return "file-access\n" + string.Join('\n', arguments);
    }

    /// <summary>
    ///     The file-access arguments shared by every picture, and whether each picture adds its default folders.
    /// </summary>
    /// <param name="Arguments">The shared arguments.</param>
    /// <param name="DefaultRoots">Whether the mode is <c>roots</c> without folders, so each picture adds the folders the tool would take.</param>
    private sealed record SharedFileAccess(FileAccessArguments Arguments, bool DefaultRoots);
}
