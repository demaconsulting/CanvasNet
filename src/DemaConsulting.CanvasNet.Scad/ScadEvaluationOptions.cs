using System.Collections.ObjectModel;
using DemaConsulting.CanvasNet.Scad.Evaluation;

namespace DemaConsulting.CanvasNet.Scad;

/// <summary>
///     Options that control one call of <see cref="ScadDocument.Evaluate(ScadEvaluationOptions?, CancellationToken)"/>.
/// </summary>
/// <remarks>
///     <para>
///         Every property is validated when it is set (fail-closed) and the instance is immutable after
///         initialization, so an options object can be shared between threads and evaluations. The
///         <see cref="Variables"/> dictionary is converted and copied when it is set, so later changes to the
///         caller's dictionary or to a sequence it contains have no effect.
///     </para>
///     <para>
///         The handlers are optional. They are never invoked while the program runs: the events are buffered and
///         delivered, in the order they occurred, on the thread that called
///         <see cref="ScadDocument.Evaluate(ScadEvaluationOptions?, CancellationToken)"/> once evaluation has
///         finished, whether it succeeded or failed. An exception thrown by a handler propagates out of a
///         successful evaluation; it is ignored when the evaluation itself failed, so the original failure is
///         reported.
///     </para>
/// </remarks>
public sealed class ScadEvaluationOptions
{
    /// <summary>The characters allowed in a name after an optional <c>$</c>.</summary>
    private static readonly System.Buffers.SearchValues<char> IdentifierCharacters =
        System.Buffers.SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_");

    /// <summary>The validated, copied and ordinal-sorted variable definitions as host objects, or null.</summary>
    private readonly IReadOnlyDictionary<string, object?>? _variables;

    /// <summary>The converted variable definitions in ordinal key order.</summary>
    private readonly IReadOnlyDictionary<string, ScadValue> _overrides = new Dictionary<string, ScadValue>();

    /// <summary>The validated time.</summary>
    private readonly double _time;

    /// <summary>The validated limits.</summary>
    private readonly ScadLimits _limits = ScadLimits.Default;

    /// <summary>
    ///     Gets the variable definitions (the <c>-D</c> style overrides of a command line), or <see langword="null"/>
    ///     for none.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Each entry replaces every top-level assignment to that name in the evaluated document (including
    ///         assignments in files it <c>include</c>s; their right-hand sides are not evaluated) and creates the
    ///         variable when the document does not assign it. Names that start with <c>$</c> create special
    ///         variables. Files imported with <c>use</c> are not affected.
    ///     </para>
    ///     <para>
    ///         A key is a name made of ASCII letters, digits and underscores that does not start with a digit, with
    ///         an optional leading <c>$</c> (then digits may start the name). A value is <see langword="null"/>
    ///         (<c>undef</c>), a <see cref="bool"/>, a number (any built-in integer or floating-point type or
    ///         <see cref="decimal"/>; non-finite values are allowed), a <see cref="string"/>, or any other
    ///         <see cref="System.Collections.IEnumerable"/> of supported values (a vector, nested at most 256 levels
    ///         deep and holding at most 1,000,000 values in total). A sequence is enumerated once, when the property
    ///         is set, and is returned by this property as an <c>object?[]</c>. Overrides are applied in ordinal key
    ///         order, so the result never depends on the iteration order of the caller's dictionary.
    ///     </para>
    /// </remarks>
    /// <exception cref="ArgumentException">
    ///     Thrown on initialization when a key is not a valid name or a value is not supported (the message names the key).
    /// </exception>
    public IReadOnlyDictionary<string, object?>? Variables
    {
        get => _variables;
        init => (_variables, _overrides) = CopyVariables(value, nameof(Variables));
    }

    /// <summary>
    ///     Gets the value of the special variable <c>$t</c> (the animation time); 0 by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is not finite.</exception>
    public double Time
    {
        get => _time;
        init
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(Time), value, "The time must be finite.");
            }

            _time = value;
        }
    }

    /// <summary>
    ///     Gets the resource limits of the evaluation; <see cref="ScadLimits.Default"/> by default.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown on initialization when the value is null.</exception>
    public ScadLimits Limits
    {
        get => _limits;
        init => _limits = value ?? throw new ArgumentNullException(nameof(Limits));
    }

    /// <summary>
    ///     Gets the handler that receives every <c>echo</c> line (including its <c>ECHO: </c> prefix), or
    ///     <see langword="null"/>. Lines are also listed in <see cref="ScadEvaluationResult.Echo"/>.
    /// </summary>
    public Action<string>? EchoHandler { get; init; }

    /// <summary>
    ///     Gets the handler that receives every warning, or <see langword="null"/>. Warnings are also listed in
    ///     <see cref="ScadEvaluationResult.Diagnostics"/>.
    /// </summary>
    public Action<ScadDiagnostic>? WarningHandler { get; init; }

    /// <summary>
    ///     Gets the handler that receives every unsupported feature the program used, or <see langword="null"/>.
    ///     Reports are also listed in <see cref="ScadEvaluationResult.UnsupportedFeatures"/>.
    /// </summary>
    public Action<ScadUnsupportedFeature>? UnsupportedFeatureHandler { get; init; }

    /// <summary>
    ///     Gets a value indicating whether evaluation stops with a <see cref="ScadUnsupportedFeatureException"/> at
    ///     the first unsupported feature instead of reporting it and continuing; <see langword="false"/> by default.
    /// </summary>
    public bool ThrowOnUnsupportedFeature { get; init; }

    /// <summary>
    ///     Gets the converted variable definitions in ordinal key order (internal; consumed by the evaluator).
    /// </summary>
    internal IReadOnlyDictionary<string, ScadValue> Overrides => _overrides;

    /// <summary>
    ///     Validates, converts and copies a variable dictionary.
    /// </summary>
    /// <param name="source">The caller's dictionary, or <see langword="null"/>.</param>
    /// <param name="propertyName">The property name reported by exceptions.</param>
    /// <returns>The host-object copy (null for none) and the converted values, both in ordinal key order.</returns>
    /// <exception cref="ArgumentException">Thrown when a key or value is not supported.</exception>
    private static (IReadOnlyDictionary<string, object?>? Copy, IReadOnlyDictionary<string, ScadValue> Converted) CopyVariables(
            IReadOnlyDictionary<string, object?>? source,
            string propertyName)
    {
        if (source is null)
        {
            return (null, new Dictionary<string, ScadValue>());
        }

        // Ordinal sorting fixes the iteration order of both copies, whatever the caller's dictionary does
        var copy = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        var converted = new SortedDictionary<string, ScadValue>(StringComparer.Ordinal);
        foreach (var (key, value) in source)
        {
            if (key is null || !IsValidName(key))
            {
                throw new ArgumentException($"The variable name '{key}' is not valid.", propertyName);
            }

            if (!ScadValue.TryFromObject(value, out var scadValue))
            {
                throw new ArgumentException($"The value of variable '{key}' is not supported.", propertyName);
            }

            converted[key] = scadValue;
            copy[key] = Normalize(value, scadValue);
        }

        return (new ReadOnlyDictionary<string, object?>(copy), new ReadOnlyDictionary<string, ScadValue>(converted));
    }

    /// <summary>
    ///     Gets the object kept in the public copy: scalars as supplied, sequences as an array of the converted elements.
    /// </summary>
    /// <param name="original">The caller's value.</param>
    /// <param name="converted">The converted value.</param>
    /// <returns>The object to keep.</returns>
    private static object? Normalize(object? original, ScadValue converted)
    {
        if (converted.Kind != ScadValueKind.Vector)
        {
            return original;
        }

        // The depth of a converted vector is bounded by the language limit, so this recursion is shallow
        var vector = converted.AsVector;
        var items = new object?[vector.Count];
        for (var i = 0; i < items.Length; i++)
        {
            items[i] = ToHostObject(vector[i]);
        }

        return items;
    }

    /// <summary>
    ///     Converts a converted value back to a host object.
    /// </summary>
    /// <param name="value">The value (only kinds <see cref="ScadValue.TryFromObject"/> produces).</param>
    /// <returns>The host object.</returns>
    private static object? ToHostObject(ScadValue value) => value.Kind switch
    {
        ScadValueKind.Boolean => value.AsBoolean,
        ScadValueKind.Number => value.AsNumber,
        ScadValueKind.String => value.AsString,
        ScadValueKind.Vector => Normalize(null, value),
        _ => null,
    };

    /// <summary>
    ///     Determines whether text is a valid variable name: an optional <c>$</c> followed by ASCII letters, digits
    ///     and underscores, where an ordinary name must not start with a digit.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns><see langword="true"/> when the name is valid.</returns>
    private static bool IsValidName(string name)
    {
        var start = name.StartsWith('$') ? 1 : 0;
        if (name.Length == start)
        {
            return false;
        }

        var firstIsDigit = char.IsAsciiDigit(name[start]);
        return (start == 1 || !firstIsDigit) && name.AsSpan(start).IndexOfAnyExcept(IdentifierCharacters) < 0;
    }
}
