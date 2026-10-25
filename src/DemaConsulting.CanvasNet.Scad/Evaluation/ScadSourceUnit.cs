using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     A source file after <c>include</c> expansion, together with the libraries it imports with <c>use</c>.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="Statements"/> are the file-scope statements of the file with every <c>include</c> directive
///         replaced, in place, by the included file's own (recursively expanded) statements; they keep their own source
///         locations, so a diagnostic points into the included file. <c>include</c> and <c>use</c> directives
///         themselves are removed. <see cref="Uses"/> lists the libraries imported with <c>use</c> by the file or by any
///         file it includes, in order of last use.
///     </para>
///     <para>
///         A unit is filled in by <see cref="ScadFileResolver"/> and must not be modified afterwards; once
///         <see cref="ScadFileResolver.Load"/> has returned, units and the lists they expose are immutable in
///         practice and safe to share between threads. Library units may reference each other cyclically
///         (libraries may <c>use</c> one another), so <see cref="Uses"/> can form a cycle.
///     </para>
/// </remarks>
internal sealed class ScadSourceUnit
{
    /// <summary>The expanded statements.</summary>
    private readonly List<ScadStatement> _statements = [];

    /// <summary>The imported libraries.</summary>
    private readonly List<ScadSourceUnit> _uses = [];

    /// <summary>
    ///     Initializes an empty unit.
    /// </summary>
    /// <param name="name">The display name of the file, or <see langword="null"/> when it is unnamed.</param>
    internal ScadSourceUnit(string? name)
    {
        Name = name;
    }

    /// <summary>
    ///     Gets the display name of the file (the name used in its source locations), or <see langword="null"/>.
    /// </summary>
    public string? Name { get; }

    /// <summary>
    ///     Gets the file-scope statements with <c>include</c> expanded and directives removed.
    /// </summary>
    public IReadOnlyList<ScadStatement> Statements => _statements;

    /// <summary>
    ///     Gets the libraries imported by <c>use</c>, in order of last use.
    /// </summary>
    public IReadOnlyList<ScadSourceUnit> Uses => _uses;

    /// <summary>
    ///     Creates a unit directly from a program that has no directives to resolve.
    /// </summary>
    /// <remarks>
    ///     Any directive statements are dropped; use <see cref="ScadFileResolver"/> to resolve them. This is a
    ///     convenience for evaluating a program in isolation.
    /// </remarks>
    /// <param name="program">The program.</param>
    /// <param name="name">The display name, or <see langword="null"/>.</param>
    /// <returns>The unit.</returns>
    internal static ScadSourceUnit FromProgram(ScadProgram program, string? name)
    {
        var unit = new ScadSourceUnit(name);
        unit._statements.AddRange(program.Statements.Where(s => s is not ScadDirectiveStatement));
        return unit;
    }

    /// <summary>
    ///     Appends an expanded statement.
    /// </summary>
    /// <param name="statement">The statement.</param>
    internal void AddStatement(ScadStatement statement) => _statements.Add(statement);

    /// <summary>
    ///     Records that a library is imported, moving it to the end when it was already imported (the later
    ///     <c>use</c> wins).
    /// </summary>
    /// <param name="library">The library unit.</param>
    internal void AddUse(ScadSourceUnit library)
    {
        _uses.Remove(library);
        _uses.Add(library);
    }
}
