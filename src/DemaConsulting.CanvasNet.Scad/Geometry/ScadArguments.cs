using System.Globalization;
using DemaConsulting.CanvasNet.Scad.Evaluation;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <summary>
///     Binds the evaluated arguments of a module instance to the module's documented parameter names, and reads
///     typed values from them.
/// </summary>
/// <remarks>
///     <para>
///         Binding follows the language's calling convention: an unnamed argument fills the next parameter in the
///         documented order, a named argument fills the parameter of that name. An argument that names no parameter
///         (an unknown name or too many positional values) is reported as a warning and ignored, and a parameter
///         supplied twice keeps its last value with a warning, so a typo never goes unnoticed. Special variables
///         (<c>$fn</c>, <c>$fa</c>, <c>$fs</c>, <c>$t</c>, ...) are ignored silently: the evaluator has already
///         folded them into <see cref="ScadInstance.Fragments"/>. A parameter given as <c>undef</c> counts as not
///         supplied, so a module's default applies.
///     </para>
///     <para>
///         The typed readers never throw; they return <see langword="false"/> and the caller reports the
///         appropriate warning for its module. An instance is a short-lived helper of one build on one thread.
///     </para>
/// </remarks>
internal sealed class ScadArguments
{
    /// <summary>The documented parameter names in positional order.</summary>
    private readonly string[] _names;

    /// <summary>The bound values by parameter index.</summary>
    private readonly ScadValue[] _values;

    /// <summary>
    ///     Initializes an argument set.
    /// </summary>
    /// <param name="names">The parameter names.</param>
    /// <param name="values">The bound values, parallel to <paramref name="names"/>.</param>
    private ScadArguments(string[] names, ScadValue[] values)
    {
        _names = names;
        _values = values;
    }

    /// <summary>
    ///     Binds the arguments of an instance.
    /// </summary>
    /// <param name="node">The instance whose arguments are bound.</param>
    /// <param name="names">The documented parameter names in positional order.</param>
    /// <param name="context">The context that receives the warnings.</param>
    /// <returns>The bound arguments.</returns>
    public static ScadArguments Bind(ScadInstance node, string[] names, ScadGeometryContext context)
    {
        var values = new ScadValue[names.Length];
        var assigned = new bool[names.Length];
        var positional = 0;
        foreach (var argument in node.Arguments)
        {
            var index = -1;
            if (argument.Name is null)
            {
                // The next unnamed argument fills the next parameter; surplus values are reported
                if (positional >= names.Length)
                {
                    context.Reporter.Warn(
                        string.Create(CultureInfo.InvariantCulture, $"Ignoring extra argument {positional + 1} of {node.Name}"),
                        node.Location);
                    positional++;
                    continue;
                }

                index = positional++;
            }
            else if (argument.Name.StartsWith('$'))
            {
                // Special variables were already resolved by the evaluator
                continue;
            }
            else
            {
                index = Array.IndexOf(names, argument.Name);
                if (index < 0)
                {
                    context.Reporter.Warn($"Ignoring unknown argument '{argument.Name}' of {node.Name}", node.Location);
                    continue;
                }
            }

            if (assigned[index])
            {
                context.Reporter.Warn($"Argument '{names[index]}' of {node.Name} is given more than once; using the last value", node.Location);
            }

            assigned[index] = true;
            values[index] = argument.Value;
        }

        return new ScadArguments(names, values);
    }

    /// <summary>
    ///     Gets a parameter that was supplied with a value other than <c>undef</c>.
    /// </summary>
    /// <param name="name">The parameter name; must be one of the documented names.</param>
    /// <param name="value">The value when supplied.</param>
    /// <returns><see langword="true"/> when the parameter was supplied and is not <c>undef</c>.</returns>
    public bool TryGet(string name, out ScadValue value)
    {
        var index = Array.IndexOf(_names, name);
        if (index < 0)
        {
            throw new ArgumentException($"'{name}' is not a parameter of this module.", nameof(name));
        }

        value = _values[index];
        return value.Kind != ScadValueKind.Undef;
    }

    /// <summary>
    ///     Reads a finite number.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="number">The number when the value is a finite number.</param>
    /// <returns><see langword="true"/> when the value is a finite number.</returns>
    public static bool TryNumber(ScadValue value, out double number)
    {
        number = value.Kind == ScadValueKind.Number ? value.AsNumber : 0.0;
        return value.Kind == ScadValueKind.Number && double.IsFinite(number);
    }

    /// <summary>
    ///     Reads a vector of exactly <paramref name="count"/> finite numbers.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="count">The required length.</param>
    /// <param name="numbers">The numbers when the value qualifies; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the value is a vector of <paramref name="count"/> finite numbers.</returns>
    public static bool TryNumbers(ScadValue value, int count, out double[]? numbers)
    {
        numbers = null;
        if (value.Kind != ScadValueKind.Vector || value.AsVector.Count != count)
        {
            return false;
        }

        var result = new double[count];
        for (var i = 0; i < count; i++)
        {
            if (!TryNumber(value.AsVector[i], out result[i]))
            {
                return false;
            }
        }

        numbers = result;
        return true;
    }

    /// <summary>
    ///     Reads a non-negative whole number that fits in an <see cref="int"/> (a list index).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="index">The index when the value is a non-negative whole number in range.</param>
    /// <returns><see langword="true"/> when the value is a non-negative whole number that fits.</returns>
    public static bool TryIndex(ScadValue value, out int index)
    {
        index = 0;
        if (!TryNumber(value, out var number) || !double.IsInteger(number) || number < 0.0 || number > int.MaxValue)
        {
            return false;
        }

        index = (int)number;
        return true;
    }
}
