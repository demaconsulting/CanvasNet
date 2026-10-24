using System.Collections.Frozen;

namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     The built-in function library of the language: mathematics, lists, strings, type tests, random numbers and
///     the language version.
/// </summary>
/// <remarks>
///     <para>
///         The library is a frozen, ordinal, case-sensitive registry from function name to a descriptor (parameter
///         names, argument counts and the implementation). <see cref="TryInvoke"/> binds the already evaluated
///         arguments (positional and named) to the descriptor and runs it; the evaluator decides <em>whether</em> a
///         name refers to a built-in (a user function of the same name shadows it) and this class decides <em>how</em>
///         it behaves.
///     </para>
///     <para>
///         Built-ins never throw for bad arguments: a wrong type, a wrong argument count or an unknown named
///         argument produces a warning and <c>undef</c>. The only exceptions are the resource-limit errors raised
///         through <see cref="IScadBuiltinHost"/>. Functions that are recognized but not implemented
///         (<c>parent_module</c>, <c>textmetrics</c>, <c>fontmetrics</c>) and unimplemented argument forms are
///         reported as unsupported features and yield <c>undef</c>.
///     </para>
///     <para>
///         All members are thread-safe: the registry is immutable and no state is shared between calls.
///         Transcendental functions use the platform math library, whose last-bit results may differ between
///         platforms; exact special cases (for example <c>sin(90)</c>) are handled explicitly.
///     </para>
/// </remarks>
internal static partial class ScadBuiltinFunctions
{
    /// <summary>The names that are recognized but not implemented.</summary>
    private static readonly FrozenSet<string> UnsupportedNames =
        new[] { "parent_module", "textmetrics", "fontmetrics" }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>The registry of implemented functions, keyed by name.</summary>
    private static readonly FrozenDictionary<string, Descriptor> Registry = BuildRegistry();

    /// <summary>
    ///     The implementation of a built-in function.
    /// </summary>
    /// <param name="args">The bound arguments: one slot per parameter, in order; a gap left by named arguments is <c>undef</c>.</param>
    /// <param name="host">The evaluator services.</param>
    /// <returns>The result value.</returns>
    private delegate ScadValue Implementation(ReadOnlySpan<ScadValue> args, IScadBuiltinHost host);

    /// <summary>
    ///     Determines whether a name is a built-in function, implemented or known-unsupported.
    /// </summary>
    /// <param name="name">The function name.</param>
    /// <returns><see langword="true"/> when the name is recognized.</returns>
    public static bool IsKnown(string name) => Registry.ContainsKey(name) || UnsupportedNames.Contains(name);

    /// <summary>
    ///     Calls a built-in function.
    /// </summary>
    /// <remarks>
    ///     Named arguments are matched to the descriptor's parameter names and override a positional value of the same
    ///     parameter; a name that matches no parameter (or any named argument to a function without parameter names)
    ///     is ignored with a warning. Too few arguments warn and yield <c>undef</c>; extra positional arguments warn
    ///     and are dropped.
    /// </remarks>
    /// <param name="name">The function name.</param>
    /// <param name="args">The evaluated arguments in call order; a null name marks a positional argument.</param>
    /// <param name="host">The evaluator services.</param>
    /// <param name="result">The result; <c>undef</c> when the call failed with a warning or the function is unsupported.</param>
    /// <returns><see langword="true"/> when <paramref name="name"/> is a built-in (the call was handled, even if it warned); <see langword="false"/> when it is not.</returns>
    /// <exception cref="ScadEvaluationException">Thrown when a resource limit is exceeded while the function runs.</exception>
    public static bool TryInvoke(string name, ReadOnlySpan<(string? Name, ScadValue Value)> args, IScadBuiltinHost host, out ScadValue result)
    {
        result = ScadValue.Undef;
        if (UnsupportedNames.Contains(name))
        {
            host.Unsupported($"scad-function-{name}", $"Function '{name}' is not supported");
            return true;
        }

        if (!Registry.TryGetValue(name, out var descriptor))
        {
            return false;
        }

        var bound = Bind(descriptor, args, host);
        if (bound is not null)
        {
            result = descriptor.Invoke(bound, host);
        }

        return true;
    }

    /// <summary>
    ///     Reports that an argument has the wrong kind.
    /// </summary>
    /// <param name="host">The evaluator services.</param>
    /// <param name="name">The function name.</param>
    /// <param name="expected">What was expected, for example "a number".</param>
    /// <param name="actual">The offending value.</param>
    /// <returns><c>undef</c>, so callers can <c>return</c> the call.</returns>
    private static ScadValue BadArgument(IScadBuiltinHost host, string name, string expected, ScadValue actual)
    {
        host.Warn($"Function '{name}' expects {expected}, got {actual.KindDescription}");
        return ScadValue.Undef;
    }

    /// <summary>
    ///     Binds evaluated call arguments to the parameter slots of a descriptor.
    /// </summary>
    /// <param name="descriptor">The function descriptor.</param>
    /// <param name="args">The evaluated arguments.</param>
    /// <param name="host">The evaluator services, for warnings.</param>
    /// <returns>The bound slots, or <see langword="null"/> when too few arguments were supplied (a warning was reported).</returns>
    private static ScadValue[]? Bind(Descriptor descriptor, ReadOnlySpan<(string? Name, ScadValue Value)> args, IScadBuiltinHost host)
    {
        var slots = new List<ScadValue>();
        var named = new List<(int Slot, ScadValue Value)>();

        // Positional arguments fill the slots in order; named ones are placed after the positional pass
        foreach (var (argName, value) in args)
        {
            if (argName is null)
            {
                slots.Add(value);
            }
            else
            {
                BindNamed(descriptor, argName, value, named, host);
            }
        }

        foreach (var (slot, value) in named)
        {
            while (slots.Count <= slot)
            {
                slots.Add(ScadValue.Undef);
            }

            slots[slot] = value;
        }

        // Drop surplus positional arguments, then enforce the minimum
        if (descriptor.MaxArgs >= 0 && slots.Count > descriptor.MaxArgs)
        {
            host.Warn($"Ignoring extra arguments to function '{descriptor.Name}'");
            slots.RemoveRange(descriptor.MaxArgs, slots.Count - descriptor.MaxArgs);
        }

        if (slots.Count < descriptor.MinArgs)
        {
            host.Warn($"Function '{descriptor.Name}' expects {DescribeArity(descriptor)} arguments, got {slots.Count}");
            return null;
        }

        return [.. slots];
    }

    /// <summary>
    ///     Resolves one named argument to a slot, or warns when the function has no such parameter.
    /// </summary>
    /// <param name="descriptor">The function descriptor.</param>
    /// <param name="argName">The argument name.</param>
    /// <param name="value">The argument value.</param>
    /// <param name="named">The list collecting resolved named arguments.</param>
    /// <param name="host">The evaluator services, for warnings.</param>
    private static void BindNamed(Descriptor descriptor, string argName, ScadValue value, List<(int Slot, ScadValue Value)> named, IScadBuiltinHost host)
    {
        var slot = Array.IndexOf(descriptor.Parameters, argName);
        if (slot < 0)
        {
            host.Warn($"Ignoring unknown argument '{argName}' of function '{descriptor.Name}'");
            return;
        }

        named.Add((slot, value));
    }

    /// <summary>
    ///     Describes the accepted argument count of a function for a warning.
    /// </summary>
    /// <param name="descriptor">The function descriptor.</param>
    /// <returns>For example "2", "1 to 3" or "at least 1".</returns>
    private static string DescribeArity(Descriptor descriptor)
    {
        if (descriptor.MaxArgs < 0)
        {
            return $"at least {descriptor.MinArgs}";
        }

        return descriptor.MinArgs == descriptor.MaxArgs
            ? $"{descriptor.MinArgs}"
            : $"{descriptor.MinArgs} to {descriptor.MaxArgs}";
    }

    /// <summary>
    ///     Builds the frozen registry from the function groups.
    /// </summary>
    /// <returns>The registry.</returns>
    private static FrozenDictionary<string, Descriptor> BuildRegistry() =>
        MathFunctions()
            .Concat(ListFunctions())
            .Concat(StringFunctions())
            .Concat(TypeFunctions())
            .Concat(RandomFunctions())
            .ToFrozenDictionary(d => d.Name, StringComparer.Ordinal);

    /// <summary>
    ///     Creates a descriptor.
    /// </summary>
    /// <param name="name">The function name.</param>
    /// <param name="minArgs">The minimum argument count.</param>
    /// <param name="maxArgs">The maximum argument count, or -1 for any number.</param>
    /// <param name="implementation">The implementation.</param>
    /// <param name="parameters">The parameter names accepted for named arguments, in order; empty when none are accepted.</param>
    /// <returns>The descriptor.</returns>
    private static Descriptor Define(string name, int minArgs, int maxArgs, Implementation implementation, params string[] parameters) =>
        new(name, parameters, minArgs, maxArgs, implementation);

    /// <summary>
    ///     Describes one built-in function.
    /// </summary>
    /// <param name="Name">The function name.</param>
    /// <param name="Parameters">The parameter names accepted for named arguments, in order.</param>
    /// <param name="MinArgs">The minimum argument count.</param>
    /// <param name="MaxArgs">The maximum argument count, or -1 for any number.</param>
    /// <param name="Invoke">The implementation.</param>
    private sealed record Descriptor(string Name, string[] Parameters, int MinArgs, int MaxArgs, Implementation Invoke);
}
