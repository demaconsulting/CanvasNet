using System.Globalization;
using DemaConsulting.CanvasNet.Files;
using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Evaluation;

// cspell:ignore OPENSCADPATH

/// <summary>
///     Resolves the <c>include</c> and <c>use</c> directives of a program before it is evaluated, inside a
///     sandbox, and produces include-expanded <see cref="ScadSourceUnit"/> values.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why a separate phase.</b> Directive paths are literal and occur only at file scope, so the complete
///         file graph can be discovered before any statement runs. Doing all file access here, on the calling thread,
///         keeps the evaluator pure CPU work on immutable data (it may be restarted on a large-stack thread without
///         repeating I/O or calling the caller's resolver again) and makes missing-file warnings precede any output.
///     </para>
///     <para>
///         <b>Semantics.</b> <c>include</c> is textual: the included file's statements replace the directive and keep
///         their own source locations. <c>use</c> imports only definitions: the used file becomes its own
///         <see cref="ScadSourceUnit"/> (loaded once per resolved file; libraries may use each other). A file that is
///         already being included (a cycle), nesting deeper than <see cref="ScadLimits.MaxIncludeDepth"/>, more than
///         <see cref="ScadLimits.MaxIncludedFiles"/> inclusions and library loads, and more than
///         <see cref="ScadLimits.MaxInstances"/> expanded statements are errors.
///     </para>
///     <para>
///         <b>Sandbox.</b> Environment variables (including <c>OPENSCADPATH</c>) are never consulted. The path rules are
///         those of the core <c>SandboxedFileResolver</c>: for a path text the candidates are, in order, the directory of
///         the including file and each library path; every candidate must stay inside the root it was derived from (the
///         base directory or a library path), absolute paths, drive letters or colons, control characters (C0 and C1) and
///         <c>..</c> above the root are errors, and a file that is, or lies beneath, a symbolic link or junction below
///         its root is rejected whatever the link's target, a dangling link included. A directory where a file is
///         expected, a file that cannot be opened and a resolver callback that throws are errors too (never a silently
///         missing file); only a file that does not exist is a warning. With a <see cref="ScadOpenOptions.FileResolver"/>
///         no file system access happens at all: candidates are normalized logical paths handed to the callback. See
///         <c>ScadFileResolver.Paths</c> for the Scad-specific part (decoding, naming and wording).
///     </para>
///     <para>
///         Each distinct file is read and parsed once per instance (a miss is remembered too, so the resolver callback
///         sees each candidate path at most once). An instance is single-use and not thread-safe; it holds no state
///         beyond one <see cref="Load"/> call.
///     </para>
/// </remarks>
internal sealed partial class ScadFileResolver
{
    /// <summary>The effective open options (base directory, library paths, resolver callback).</summary>
    private readonly ScadOpenOptions _options;

    /// <summary>The resource limits.</summary>
    private readonly ScadLimits _limits;

    /// <summary>The sink that receives missing-file warnings.</summary>
    private readonly ScadEvaluationSink _sink;

    /// <summary>The cancellation token polled between files.</summary>
    private readonly CancellationToken _cancellation;

    /// <summary>The comparer under which two file keys name the same file (platform path comparison, or ordinal for logical paths).</summary>
    private readonly StringComparer _keyComparer;

    /// <summary>The loaded files by root and key; each distinct file is parsed once per root it is reached through.</summary>
    private readonly Dictionary<string, LoadedFile> _files;

    /// <summary>The loaded libraries by root and file key; an entry is registered before the library is expanded so libraries may use each other.</summary>
    private readonly Dictionary<string, ScadSourceUnit> _libraries;

    /// <summary>The libraries registered but not yet expanded.</summary>
    private readonly Queue<(ScadSourceUnit Unit, LoadedFile File)> _pendingLibraries = new();

    /// <summary>The number of inclusions and library loads so far.</summary>
    private int _inclusions;

    /// <summary>The number of statements produced by expansion so far.</summary>
    private long _expandedStatements;

    /// <summary>
    ///     Initializes a resolver.
    /// </summary>
    /// <param name="options">The effective open options.</param>
    /// <param name="limits">The resource limits.</param>
    /// <param name="sink">The sink that receives missing-file warnings.</param>
    /// <param name="cancellation">The cancellation token, polled between files.</param>
    public ScadFileResolver(ScadOpenOptions options, ScadLimits limits, ScadEvaluationSink sink, CancellationToken cancellation)
    {
        _options = options;
        _limits = limits;
        _sink = sink;
        _cancellation = cancellation;
        _sandbox = new SandboxedFileResolver(
            new SandboxOptions
            {
                BaseDirectory = options.BaseDirectory,
                SearchPaths = options.LibraryPaths,
                FileResolver = options.FileResolver,
                MaxFileBytes = MaxSourceBytes,
            },
            cancellation);
        _keyComparer = _sandbox.KeyComparer;
        _files = new Dictionary<string, LoadedFile>(_keyComparer);
        _libraries = new Dictionary<string, ScadSourceUnit>(_keyComparer);
    }

    /// <summary>
    ///     Expands the <c>include</c> directives of the main program and loads the libraries it imports.
    /// </summary>
    /// <param name="main">The parsed main program.</param>
    /// <param name="mainName">The display name of the main file, or <see langword="null"/> when it is unnamed.</param>
    /// <returns>The include-expanded unit of the main program, whose <see cref="ScadSourceUnit.Uses"/> hold the libraries.</returns>
    /// <exception cref="ScadEvaluationException">Thrown for a sandbox violation, an include cycle, an exceeded limit, or a directive when no file source is configured.</exception>
    /// <exception cref="ScadSyntaxException">Thrown when an included or used file has a syntax error; its location names that file.</exception>
    /// <exception cref="InvalidDataException">Thrown when an included file exceeds the source size limit.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the cancellation token is canceled.</exception>
    public ScadSourceUnit Load(ScadProgram main, string? mainName)
    {
        _cancellation.ThrowIfCancellationRequested();

        // The main file is the first entry of the include stack so that a file that includes it again is a cycle
        var unit = new ScadSourceUnit(mainName);
        var stack = new List<StackEntry> { new(MainKey(mainName), mainName ?? "(document)") };
        ExpandInto(unit, main.Statements, null, 0, stack);

        // Libraries are expanded from a work queue (not recursively), so a long chain of libraries cannot deepen the stack
        while (_pendingLibraries.Count > 0)
        {
            var (library, file) = _pendingLibraries.Dequeue();
            _cancellation.ThrowIfCancellationRequested();
            ExpandInto(library, file.Program.Statements, file.Source, 0, [new StackEntry(file.Key, file.Display)]);
        }

        return unit;
    }

    /// <summary>
    ///     Copies statements into a unit, replacing directives.
    /// </summary>
    /// <param name="unit">The unit being filled.</param>
    /// <param name="statements">The statements of one file.</param>
    /// <param name="referrer">The sandbox file the statements came from, whose directory relative directives start in, or <see langword="null"/> for the main file.</param>
    /// <param name="depth">The include nesting depth of that file (zero for a main or library file).</param>
    /// <param name="stack">The chain of files being included, outermost first.</param>
    private void ExpandInto(ScadSourceUnit unit, IReadOnlyList<ScadStatement> statements, SandboxedFile? referrer, int depth, List<StackEntry> stack)
    {
        foreach (var statement in statements)
        {
            if (statement is ScadDirectiveStatement directive)
            {
                HandleDirective(unit, directive, referrer, depth, stack);
            }
            else
            {
                AddStatement(unit, statement);
            }
        }
    }

    /// <summary>
    ///     Appends one statement, enforcing the bound on expanded statements.
    /// </summary>
    /// <param name="unit">The unit.</param>
    /// <param name="statement">The statement.</param>
    /// <exception cref="ScadEvaluationException">Thrown when too many statements were produced.</exception>
    private void AddStatement(ScadSourceUnit unit, ScadStatement statement)
    {
        // Repeated inclusion copies statement references, so the total is bounded to stop an include blow-up
        if (++_expandedStatements > _limits.MaxInstances)
        {
            throw new ScadEvaluationException(string.Create(CultureInfo.InvariantCulture, $"Included statement limit exceeded (limit {_limits.MaxInstances})"), statement.Location);
        }

        unit.AddStatement(statement);
    }

    /// <summary>
    ///     Handles one <c>include</c> or <c>use</c> directive.
    /// </summary>
    /// <param name="unit">The unit being filled.</param>
    /// <param name="directive">The directive.</param>
    /// <param name="referrer">The sandbox file that contains the directive, or <see langword="null"/> for the main file.</param>
    /// <param name="depth">The include nesting depth of that file.</param>
    /// <param name="stack">The chain of files being included.</param>
    private void HandleDirective(ScadSourceUnit unit, ScadDirectiveStatement directive, SandboxedFile? referrer, int depth, List<StackEntry> stack)
    {
        // Without any file source the directive cannot be honored at all, which is an error rather than a skipped file
        if (!HasFileSource)
        {
            throw new ScadEvaluationException(
                $"Cannot resolve '{directive.Path}': the document has no base directory, library path or file resolver",
                directive.Location);
        }

        var file = Resolve(directive, referrer);
        if (file is null)
        {
            _sink.Warn($"Can't open include file '{directive.Path}'", directive.Location);
            return;
        }

        if (directive.Kind == ScadDirectiveKind.Use)
        {
            AttachLibrary(unit, file, directive.Location);
        }
        else
        {
            ExpandInclude(unit, file, directive.Location, depth, stack);
        }
    }

    /// <summary>
    ///     Expands an <c>include</c> in place.
    /// </summary>
    /// <param name="unit">The unit being filled.</param>
    /// <param name="file">The resolved file.</param>
    /// <param name="location">The location of the directive.</param>
    /// <param name="depth">The include nesting depth of the including file.</param>
    /// <param name="stack">The chain of files being included.</param>
    private void ExpandInclude(ScadSourceUnit unit, LoadedFile file, ScadSourceLocation location, int depth, List<StackEntry> stack)
    {
        // A file that is already being included would expand forever
        var cycleStart = stack.FindIndex(e => e.Key is not null && _keyComparer.Equals(e.Key, file.Key));
        if (cycleStart >= 0)
        {
            var chain = stack.Skip(cycleStart).Select(e => e.Display).Append(file.Display);
            throw new ScadEvaluationException($"Include cycle: {string.Join(" \u2192 ", chain)}", location);
        }

        if (depth + 1 > _limits.MaxIncludeDepth)
        {
            throw new ScadEvaluationException(string.Create(CultureInfo.InvariantCulture, $"Include nesting too deep (limit {_limits.MaxIncludeDepth})"), location);
        }

        CountInclusion(location);
        stack.Add(new StackEntry(file.Key, file.Display));
        ExpandInto(unit, file.Program.Statements, file.Source, depth + 1, stack);
        stack.RemoveAt(stack.Count - 1);
    }

    /// <summary>
    ///     Registers a <c>use</c>d library, loading it once however often it is used.
    /// </summary>
    /// <param name="unit">The unit that imports the library.</param>
    /// <param name="file">The resolved library file.</param>
    /// <param name="location">The location of the directive.</param>
    private void AttachLibrary(ScadSourceUnit unit, LoadedFile file, ScadSourceLocation location)
    {
        // The cache entry is registered before the library is expanded, so mutually using libraries terminate
        if (!_libraries.TryGetValue(file.Identity, out var library))
        {
            CountInclusion(location);
            library = new ScadSourceUnit(file.Display);
            _libraries.Add(file.Identity, library);
            _pendingLibraries.Enqueue((library, file));
        }

        unit.AddUse(library);
    }

    /// <summary>
    ///     Counts one inclusion or library load against the limit.
    /// </summary>
    /// <param name="location">The location of the directive.</param>
    /// <exception cref="ScadEvaluationException">Thrown when the limit is exceeded.</exception>
    private void CountInclusion(ScadSourceLocation location)
    {
        if (++_inclusions > _limits.MaxIncludedFiles)
        {
            throw new ScadEvaluationException(string.Create(CultureInfo.InvariantCulture, $"Included file limit exceeded (limit {_limits.MaxIncludedFiles})"), location);
        }

        _cancellation.ThrowIfCancellationRequested();
    }

    /// <summary>
    ///     Gets a value indicating whether at least one way of finding files is configured.
    /// </summary>
    private bool HasFileSource => _sandbox.HasSource;

    /// <summary>
    ///     One entry of the chain of files currently being included.
    /// </summary>
    /// <param name="Key">The identity of the file, or <see langword="null"/> for an unnamed main document.</param>
    /// <param name="Display">The name shown in a cycle message.</param>
    private sealed record StackEntry(string? Key, string Display);

    /// <summary>
    ///     A file that was found, read and parsed.
    /// </summary>
    /// <param name="Key">The identity of the file (its normalized path).</param>
    /// <param name="Display">The name used in source locations.</param>
    /// <param name="Program">The parsed program.</param>
    /// <param name="Source">The sandbox file, whose directory and root are the context for the file's own directives.</param>
    private sealed record LoadedFile(string Key, string Display, ScadProgram Program, SandboxedFile Source)
    {
        /// <summary>Gets the identity of the file together with the root that confines it, used by the caches.</summary>
        public string Identity => Source.Root + '\0' + Key;
    }
}
