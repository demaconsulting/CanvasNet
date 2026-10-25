using DemaConsulting.CanvasNet.Scad.Evaluation;
using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

// cspell:ignore scadtest mklink

/// <summary>
///     An in-memory file source for resolver tests: a dictionary from logical path to text that records every
///     path it is asked for and the threads that asked.
/// </summary>
internal sealed class ScadVirtualFiles
{
    /// <summary>The files by logical path.</summary>
    private readonly Dictionary<string, string> _files;

    /// <summary>The recorded requests.</summary>
    private readonly List<string> _requests = [];

    /// <summary>The recorded requesting threads.</summary>
    private readonly List<int> _threads = [];

    /// <summary>
    ///     Initializes the file set.
    /// </summary>
    /// <param name="files">The files as (path, text) pairs.</param>
    public ScadVirtualFiles(params (string Path, string Text)[] files)
    {
        _files = files.ToDictionary(f => f.Path, f => f.Text, StringComparer.Ordinal);
    }

    /// <summary>Gets or sets an action invoked on every request (for example to cancel a token).</summary>
    public Action<string>? OnRequest { get; set; }

    /// <summary>Gets the paths requested, in order, including requests that found nothing.</summary>
    public IReadOnlyList<string> Requests => _requests;

    /// <summary>Gets the managed thread ids of the requests.</summary>
    public IReadOnlyList<int> Threads => _threads;

    /// <summary>
    ///     Adds or replaces a file.
    /// </summary>
    /// <param name="path">The logical path.</param>
    /// <param name="text">The text.</param>
    public void Set(string path, string text) => _files[path] = text;

    /// <summary>
    ///     The resolver callback: returns a UTF-8 stream of the file or null.
    /// </summary>
    /// <param name="path">The requested logical path.</param>
    /// <returns>The stream, or <see langword="null"/>.</returns>
    public Stream? Resolve(string path)
    {
        _requests.Add(path);
        _threads.Add(Environment.CurrentManagedThreadId);
        OnRequest?.Invoke(path);
        return _files.TryGetValue(path, out var text) ? new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text)) : null;
    }
}

/// <summary>
///     Helpers for the <see cref="ScadFileResolver"/> tests.
/// </summary>
internal static class ScadResolverTestSupport
{
    /// <summary>
    ///     Loads a main document through a resolver with an in-memory file set.
    /// </summary>
    /// <param name="source">The main source text.</param>
    /// <param name="files">The virtual files.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <param name="libraryPaths">The library paths, or null.</param>
    /// <param name="sink">The sink that receives warnings, or null to create one.</param>
    /// <param name="cancellation">The cancellation token.</param>
    /// <returns>The expanded main unit.</returns>
    public static ScadSourceUnit LoadVirtual(
        string source,
        ScadVirtualFiles files,
        ScadLimits? limits = null,
        IReadOnlyList<string>? libraryPaths = null,
        ScadEvaluationSink? sink = null,
        CancellationToken? cancellation = null)
    {
        var options = new ScadOpenOptions
        {
            FileResolver = files.Resolve,
            SourceName = "main.scad",
            LibraryPaths = libraryPaths ?? [],
        };
        return Load(source, options, limits, sink, cancellation);
    }

    /// <summary>
    ///     Loads a main document with explicit options.
    /// </summary>
    /// <param name="source">The main source text.</param>
    /// <param name="options">The open options.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <param name="sink">The sink that receives warnings, or null to create one.</param>
    /// <param name="cancellation">The cancellation token.</param>
    /// <param name="isLink">The link probe handed to the resolver, or null for the file system.</param>
    /// <returns>The expanded main unit.</returns>
    public static ScadSourceUnit Load(
        string source,
        ScadOpenOptions options,
        ScadLimits? limits = null,
        ScadEvaluationSink? sink = null,
        CancellationToken? cancellation = null,
        Func<string, bool>? isLink = null)
    {
        var effectiveLimits = limits ?? ScadLimits.Default;
        var document = ScadDocument.Parse(source, options);
        var resolver = new ScadFileResolver(document.Options, effectiveLimits, sink ?? new ScadEvaluationSink(effectiveLimits), cancellation ?? TestContext.Current.CancellationToken, isLink);
        return resolver.Load(document.Program, document.SourceName);
    }

    /// <summary>
    ///     Describes the assignment statements of a unit as <c>name@file:line</c> in order, to assert order and provenance.
    /// </summary>
    /// <param name="unit">The unit.</param>
    /// <returns>The descriptions.</returns>
    public static string[] Assignments(ScadSourceUnit unit) =>
        unit.Statements
            .OfType<ScadAssignmentStatement>()
            .Select(a => $"{a.Name}@{a.Location.File}:{a.Location.Line}")
            .ToArray();

    /// <summary>
    ///     Gets the names of the assignment statements of a unit in order.
    /// </summary>
    /// <param name="unit">The unit.</param>
    /// <returns>The names.</returns>
    public static string[] AssignmentNames(ScadSourceUnit unit) =>
        unit.Statements.OfType<ScadAssignmentStatement>().Select(a => a.Name).ToArray();

    /// <summary>
    ///     Loads a program made of hand-built directives, bypassing the lexer so hostile path texts can be used.
    /// </summary>
    /// <param name="options">The open options.</param>
    /// <param name="paths">The include paths, one directive each on lines 1, 2, ...</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <param name="isLink">The link probe handed to the resolver, or null for the file system.</param>
    /// <returns>The expanded main unit.</returns>
    public static ScadSourceUnit LoadDirectives(ScadOpenOptions options, IReadOnlyList<string> paths, ScadLimits? limits = null, Func<string, bool>? isLink = null)
    {
        var effectiveLimits = limits ?? ScadLimits.Default;
        var statements = new List<ScadStatement>();
        for (var i = 0; i < paths.Count; i++)
        {
            statements.Add(new ScadDirectiveStatement(ScadDirectiveKind.Include, paths[i]) { Location = new ScadSourceLocation("main.scad", i + 1, 1) });
        }

        var resolver = new ScadFileResolver(options, effectiveLimits, new ScadEvaluationSink(effectiveLimits), CancellationToken.None, isLink);
        return resolver.Load(new ScadProgram(statements), "main.scad");
    }

    /// <summary>
    ///     Tries to create a directory link (a symbolic link, or on Windows a junction when links need a privilege).
    /// </summary>
    /// <param name="link">The full path of the link to create.</param>
    /// <param name="target">The full path of the existing target directory.</param>
    /// <param name="reason">The reason the link could not be created, or an empty string.</param>
    /// <returns><see langword="true"/> when the link exists afterwards.</returns>
    public static bool TryCreateDirectoryLink(string link, string target, out string reason)
    {
        try
        {
            Directory.CreateSymbolicLink(link, target);
            reason = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            reason = ex.Message;
        }

        // Without the privilege for symbolic links a junction can still be made on Windows
        return OperatingSystem.IsWindows() && TryCreateJunction(link, target, out reason);
    }

    /// <summary>
    ///     Tries to create a file symbolic link.
    /// </summary>
    /// <param name="link">The full path of the link to create.</param>
    /// <param name="target">The full path of the existing target file.</param>
    /// <param name="reason">The reason the link could not be created, or an empty string.</param>
    /// <returns><see langword="true"/> when the link was created.</returns>
    public static bool TryCreateFileLink(string link, string target, out string reason)
    {
        try
        {
            File.CreateSymbolicLink(link, target);
            reason = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            reason = ex.Message;
            return false;
        }
    }

    /// <summary>
    ///     Creates a Windows junction through the command interpreter (no privilege needed).
    /// </summary>
    /// <param name="link">The full path of the junction.</param>
    /// <param name="target">The full path of the target directory.</param>
    /// <param name="reason">The reason the junction could not be created, or an empty string.</param>
    /// <returns><see langword="true"/> when the junction exists afterwards.</returns>
    private static bool TryCreateJunction(string link, string target, out string reason)
    {
        var info = new System.Diagnostics.ProcessStartInfo("cmd.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        info.ArgumentList.Add("/c");
        info.ArgumentList.Add("mklink");
        info.ArgumentList.Add("/J");
        info.ArgumentList.Add(Path.GetFullPath(link));
        info.ArgumentList.Add(Path.GetFullPath(target));
        try
        {
            using var process = System.Diagnostics.Process.Start(info);
            if (process is null)
            {
                reason = "cmd.exe could not be started";
                return false;
            }

            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            reason = process.ExitCode == 0 ? string.Empty : "mklink /J failed";
            return process.ExitCode == 0 && Directory.Exists(link);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            reason = ex.Message;
            return false;
        }
    }
}

/// <summary>
///     A temporary directory that is deleted when disposed.
/// </summary>
internal sealed class ScadTempDirectory : IDisposable
{
    /// <summary>
    ///     Initializes a new, empty temporary directory.
    /// </summary>
    public ScadTempDirectory()
    {
        Path = Directory.CreateTempSubdirectory("scadtest").FullName;
    }

    /// <summary>
    ///     Gets the full path of the directory.
    /// </summary>
    public string Path { get; }

    /// <summary>
    ///     Writes a file, creating its folders.
    /// </summary>
    /// <param name="relativePath">The path relative to the directory (forward slashes).</param>
    /// <param name="text">The file text.</param>
    /// <returns>The full path of the file.</returns>
    public string Write(string relativePath, string text)
    {
        var full = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
        return full;
    }

    /// <summary>
    ///     Gets the full path of a location inside the directory.
    /// </summary>
    /// <param name="relativePath">The relative path.</param>
    /// <returns>The full path.</returns>
    public string Combine(string relativePath) => System.IO.Path.Combine(Path, relativePath);

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a leftover temporary folder must not fail a test
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort, as above
        }
    }
}
