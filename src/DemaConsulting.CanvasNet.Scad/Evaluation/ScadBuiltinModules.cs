using System.Collections.Frozen;

namespace DemaConsulting.CanvasNet.Scad.Evaluation;

// cspell:ignore multmatrix polyhedron minkowski

/// <summary>
///     The names the evaluator recognizes as built-in modules.
/// </summary>
/// <remarks>
///     A recognized name produces a <see cref="ScadInstanceKind.BuiltinModule"/> instance with its evaluated
///     arguments and children; the evaluator never interprets geometry or validates arguments, so a name in this
///     set that a later phase cannot build is that phase's concern. A user module of the same name shadows the
///     built-in.
/// </remarks>
internal static class ScadBuiltinModules
{
    /// <summary>The recognized built-in module names (ordinal, case-sensitive).</summary>
    public static readonly FrozenSet<string> Names = FrozenSet.ToFrozenSet(
        [
            "cube", "sphere", "cylinder", "polyhedron", "square", "circle", "polygon", "text", "translate", "rotate",
            "scale", "mirror", "multmatrix", "resize", "color", "union", "difference", "intersection", "linear_extrude",
            "rotate_extrude", "hull", "minkowski", "offset", "projection", "render", "surface", "import", "roof",
        ],
        StringComparer.Ordinal);

    /// <summary>
    ///     Determines whether a name is a built-in module.
    /// </summary>
    /// <param name="name">The module name.</param>
    /// <returns><see langword="true"/> when the name is recognized.</returns>
    public static bool IsBuiltin(string name) => Names.Contains(name);
}
