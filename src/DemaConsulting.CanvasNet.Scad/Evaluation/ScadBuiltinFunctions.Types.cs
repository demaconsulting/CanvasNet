namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     The type-test built-in functions and the language version functions.
/// </summary>
internal static partial class ScadBuiltinFunctions
{
    /// <summary>The emulated language version reported by <c>version()</c>: year.</summary>
    private const int VersionYear = 2021;

    /// <summary>The emulated language version reported by <c>version()</c>: month.</summary>
    private const int VersionMonth = 1;

    /// <summary>The emulated language version reported by <c>version()</c>: patch level.</summary>
    private const int VersionPatch = 0;

    /// <summary>
    ///     Gets the descriptors of the type-test and version functions.
    /// </summary>
    /// <returns>The descriptors.</returns>
    private static IEnumerable<Descriptor> TypeFunctions()
    {
        yield return TypeTest("is_undef", ScadValueKind.Undef);
        yield return TypeTest("is_num", ScadValueKind.Number);
        yield return TypeTest("is_bool", ScadValueKind.Boolean);
        yield return TypeTest("is_string", ScadValueKind.String);
        yield return TypeTest("is_list", ScadValueKind.Vector);
        yield return TypeTest("is_function", ScadValueKind.Function);
        yield return Define(
            "version",
            0,
            0,
            (_, _) => ScadValue.FromVector([ScadValue.FromNumber(VersionYear), ScadValue.FromNumber(VersionMonth), ScadValue.FromNumber(VersionPatch)]));
        yield return Define(
            "version_num",
            0,
            0,
            (_, _) => ScadValue.FromNumber((VersionYear * 10_000) + (VersionMonth * 100) + VersionPatch));
    }

    /// <summary>
    ///     Creates a descriptor for a function that tests the kind of its argument.
    /// </summary>
    /// <param name="name">The function name.</param>
    /// <param name="kind">The kind that makes the test true; <c>NaN</c> counts as a number.</param>
    /// <returns>The descriptor.</returns>
    private static Descriptor TypeTest(string name, ScadValueKind kind) =>
        Define(name, 1, 1, (args, _) => ScadValue.FromBoolean(args[0].Kind == kind));
}
