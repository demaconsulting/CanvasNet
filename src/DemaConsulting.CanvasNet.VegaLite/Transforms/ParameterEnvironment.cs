using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <summary>
///     Computes the values of the variable parameters of a specification: the static environment that expressions read.
/// </summary>
/// <remarks>
///     <para>
///         A variable parameter of the root view has a <c>name</c> and a <c>value</c> or an <c>expr</c> (both may be given;
///         the expression wins, as the value is only the initial state of an interactive parameter). An expression is
///         evaluated once, with the parameters it refers to, in the order that
///         <see cref="ParameterGraph"/> gives (dependencies first, otherwise the order of declaration), so the result does
///         not depend on the order of the text. <see cref="VegaLiteRenderOptions.Parameters"/> replaces the default of a
///         parameter, whatever the parameter defines: the value of the caller is used as it is and the expression is not
///         evaluated. A parameter without a value or an expression is null. A selection parameter is the signal of its resolved
///         selection (<see cref="SelectionModel"/>): an object with the selected values of each field, empty for an empty selection.
///     </para>
///     <para>
///         The environment is built once per measure or render call, on the calling thread, before anything else reads it: before the expression references of the static sites are
///         folded (<see cref="ExpressionReferenceFolder"/>) and before the data is read. The value of the caller can be a structured value (an array or an object); it replaces
///         the definition of the parameter as it is. Only the parameters of the top-level view exist: Vega-Lite compiles no signal for a variable parameter of a nested view, so
///         an expression that reads one sees null (the feature catalog reports it as <c>vl-param-variable</c>). A text of a value of the caller or
///         of the specification that is longer than <see cref="VegaLiteLimits.MaxStringLength"/> is a located
///         <see cref="VegaLiteRenderException"/>, because no function has measured it. An expression that fails (the
///         step budget, the string limit) is a located <see cref="VegaLiteRenderException"/>.
///     </para>
/// </remarks>
internal static class ParameterEnvironment
{
    /// <summary>
    ///     Computes the parameter values of the root view into a dictionary that the evaluator already reads.
    /// </summary>
    /// <param name="tree">The specification.</param>
    /// <param name="overrides">The values the caller supplied, or null.</param>
    /// <param name="values">Receives the values by parameter name; the evaluator reads this dictionary.</param>
    /// <param name="evaluator">The evaluator that was created with <paramref name="values"/>.</param>
    /// <param name="selectionValues">The value of each selection parameter by variable name (the resolved selections of <see cref="SelectionModel"/>), which the variable parameters can read; null when there are none.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument other than <paramref name="overrides"/> or <paramref name="selectionValues"/> is null.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown when the evaluation of an expression exceeds a limit.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the evaluator's token is canceled.</exception>
    public static void Populate(
        SpecTree tree,
        IReadOnlyDictionary<string, object?>? overrides,
        Dictionary<string, DataValue> values,
        ExpressionEvaluator evaluator,
        IReadOnlyDictionary<string, DataValue>? selectionValues = null)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(evaluator);

        // A selection parameter is the signal of its resolved selection, wherever it is declared; the variable parameters below may read it
        if (selectionValues is not null)
        {
            foreach (var (selectionName, selectionValue) in selectionValues)
            {
                values[selectionName] = selectionValue;
            }
        }

        if (tree.Root["params"] is not { IsArray: true } parameters)
        {
            return;
        }

        // The variable parameters by name, and what each expression needs first
        var items = parameters.Items;
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i]["select"] is null && items[i]["name"]?.StringValue is { } name)
            {
                index.TryAdd(name, i);
            }
        }

        var dependencies = new int[items.Count][];
        for (var i = 0; i < items.Count; i++)
        {
            var compiled = items[i]["select"] is null ? tree.Expressions.Get(items[i]["expr"]) : null;
            dependencies[i] = compiled is null
                ? []
                : compiled.ParameterReferences.Where(r => index.ContainsKey(r.Name)).Select(r => index[r.Name]).ToArray();
        }

        // Parameters that cannot be ordered (a cycle) were rejected when the specification was parsed
        var (order, _) = ParameterGraph.Sort(dependencies);
        foreach (var position in order)
        {
            var parameter = items[position];
            if (parameter["select"] is not null || parameter["name"]?.StringValue is not { } parameterName)
            {
                continue;
            }

            values[parameterName] = ValueOf(tree, parameter, parameterName, overrides, evaluator);
        }
    }

    /// <summary>
    ///     Converts a value of the caller: a scalar, or a sequence or a dictionary of values (the copies that <see cref="VegaLiteRenderOptions.Parameters"/> made).
    /// </summary>
    /// <remarks>
    ///     The nesting was bounded when the options were created (<see cref="VegaLiteRenderOptions.Parameters"/>), so the recursion is bounded too.
    /// </remarks>
    /// <param name="supplied">The value.</param>
    /// <param name="location">The location of the parameter, for errors.</param>
    /// <returns>The value.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when a date cannot be represented.</exception>
    internal static DataValue FromCaller(object? supplied, VegaLiteLocation location)
    {
        switch (supplied)
        {
            case IReadOnlyDictionary<string, object?> members:
                return DataValue.FromObject(DataObject.Create(
                    members.Select(m => new KeyValuePair<string, DataValue>(m.Key, FromCaller(m.Value, location)))));
            case string:
                return DataLoader.FromObject(supplied, location);
            case System.Collections.IEnumerable items:
                return DataValue.FromOwnedArray(items.Cast<object?>().Select(i => FromCaller(i, location)).ToArray());
            default:
                return DataLoader.FromObject(supplied, location);
        }
    }

    /// <summary>
    ///     Computes the value of one parameter.
    /// </summary>
    /// <param name="tree">The specification.</param>
    /// <param name="parameter">The parameter object.</param>
    /// <param name="name">The parameter name.</param>
    /// <param name="overrides">The values the caller supplied, or null.</param>
    /// <param name="evaluator">The evaluator.</param>
    /// <returns>The value.</returns>
    private static DataValue ValueOf(
        SpecTree tree,
        SpecNode parameter,
        string name,
        IReadOnlyDictionary<string, object?>? overrides,
        ExpressionEvaluator evaluator)
    {
        if (overrides is not null && overrides.TryGetValue(name, out var supplied))
        {
            return Checked(FromCaller(supplied, tree.Locate(parameter)), name, tree.Locate(parameter), evaluator.MaxStringLength);
        }

        if (tree.Expressions.Get(parameter["expr"]) is { } expression)
        {
            return evaluator.Evaluate(expression);
        }

        return parameter["value"] is { } value
            ? Checked(SpecValues.ToDataValue(value), name, tree.Locate(parameter), evaluator.MaxStringLength)
            : DataValue.Null;
    }

    /// <summary>
    ///     Checks that no text of a parameter value is longer than the limit the evaluation enforces for its own texts.
    /// </summary>
    /// <remarks>
    ///     The value of the caller and the value of the specification come from outside the evaluation, so no function has
    ///     measured them; without this check a long text could be stored by <c>calculate</c> or returned by a function. The
    ///     nesting of a composite value is bounded by the specification parser (at most 256 levels).
    /// </remarks>
    /// <param name="value">The value.</param>
    /// <param name="name">The parameter name.</param>
    /// <param name="location">The location of the parameter.</param>
    /// <param name="maxLength">The longest text allowed.</param>
    /// <returns>The value.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when a text is longer than <paramref name="maxLength"/>.</exception>
    private static DataValue Checked(DataValue value, string name, VegaLiteLocation location, int maxLength)
    {
        if (value.HasTextLongerThan(maxLength))
        {
            throw new VegaLiteRenderException(
                $"The value of the parameter '{name}' has a text longer than the limit of {maxLength} characters (VegaLiteLimits.MaxStringLength).",
                location);
        }

        return value;
    }
}

/// <summary>
///     Converts values of the specification to data values.
/// </summary>
internal static class SpecValues
{
    /// <summary>
    ///     Converts a JSON value of the specification to a data value.
    /// </summary>
    /// <remarks>
    ///     An array or an object becomes an immutable composite value. The recursion is bounded by the nesting depth that
    ///     the specification parser accepted (at most 256 levels).
    /// </remarks>
    /// <param name="node">The value.</param>
    /// <returns>The data value.</returns>
    public static DataValue ToDataValue(SpecNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        switch (node.Kind)
        {
            case SpecNodeKind.String:
                return DataValue.FromString(node.StringValue);
            case SpecNodeKind.Number:
                return DataValue.FromNumber(node.NumberValue);
            case SpecNodeKind.Boolean:
                return DataValue.FromBoolean(node.BoolValue);
            case SpecNodeKind.Array:
                return DataValue.FromOwnedArray(node.Items.Select(ToDataValue).ToArray());
            case SpecNodeKind.Object:
                return DataValue.FromObject(DataObject.Create(
                    node.Properties.Select(p => new KeyValuePair<string, DataValue>(p.Name!, ToDataValue(p)))));
            default:
                return DataValue.Null;
        }
    }
}
