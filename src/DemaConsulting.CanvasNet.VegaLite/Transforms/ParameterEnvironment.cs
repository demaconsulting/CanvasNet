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
///         evaluated. A parameter without a value or an expression is null. A selection parameter has no value in a
///         static chart; it is reported by the feature catalog and an expression that reads it sees null.
///     </para>
///     <para>
///         The environment is built once per measure or render call, on the calling thread. An expression that fails (the
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
    /// <exception cref="ArgumentNullException">Thrown when an argument other than <paramref name="overrides"/> is null.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown when the evaluation of an expression exceeds a limit.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the evaluator's token is canceled.</exception>
    public static void Populate(
        SpecTree tree,
        IReadOnlyDictionary<string, object?>? overrides,
        Dictionary<string, DataValue> values,
        ExpressionEvaluator evaluator)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(evaluator);
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
            return DataLoader.FromObject(supplied, tree.Locate(parameter));
        }

        if (tree.Expressions.Get(parameter["expr"]) is { } expression)
        {
            return evaluator.Evaluate(expression);
        }

        return parameter["value"] is { } value ? SpecValues.ToDataValue(value) : DataValue.Null;
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
