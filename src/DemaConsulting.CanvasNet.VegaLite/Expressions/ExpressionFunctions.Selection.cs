using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.Expressions;

/// <summary>
///     The selection functions of the closed function table (a partial file of the functions unit): <c>data</c> for the stores of selections, and Vega's
///     <c>vlSelectionTest</c>, <c>vlSelectionIdTest</c> and <c>vlSelectionResolve</c>.
/// </summary>
/// <remarks>
///     <para>
///         A selection parameter of Vega-Lite is compiled to a data set <c>name_store</c> that holds the tuples of the selection and to the predicate
///         <c>!length(data("name_store")) || vlSelectionTest("name_store", datum)</c>. The renderer evaluates exactly that text, so the table has these
///         functions. <c>data</c> gives the tuples of a selection store of the call (an array of objects) and an empty array for every other name, because no other data set
///         can be read from an expression. The three selection functions read the store by name from the evaluation (<see cref="ExpressionFunctionContext.SelectionStores"/>) and
///         follow <see cref="SelectionTestSemantics"/>; a name that is not a store behaves as an empty store.
///     </para>
/// </remarks>
internal static partial class ExpressionFunctions
{
    /// <summary>
    ///     Adds the selection functions.
    /// </summary>
    /// <param name="table">The table being built.</param>
    private static void AddSelection(Dictionary<string, ExpressionFunction> table)
    {
        table["data"] = (a, c) => StoreOf(a, c) is { } store ? store.ToValue() : DataValue.FromOwnedArray([]);
        table["vlSelectionTest"] = (a, c) => SelectionTestSemantics.Test(StoreOf(a, c), Arg(a, 1), OperationOf(a, 2), c);
        table["vlSelectionIdTest"] = (a, c) => SelectionTestSemantics.IdTest(StoreOf(a, c), Arg(a, 1), OperationOf(a, 2), c);
        table["vlSelectionResolve"] = (a, c) => SelectionTestSemantics.Resolve(
            StoreOf(a, c),
            OperationOf(a, 1),
            ExpressionEvaluator.IsTruthy(Arg(a, 2)),
            ExpressionEvaluator.IsTruthy(Arg(a, 3)),
            c);
    }

    /// <summary>
    ///     Finds the selection store that the first argument names.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="context">The context that holds the stores.</param>
    /// <returns>The store; null when the name is not a store of the call.</returns>
    private static SelectionStore? StoreOf(ReadOnlySpan<DataValue> arguments, ExpressionFunctionContext context) =>
        arguments.Length > 0 && arguments[0].IsString && context.SelectionStores is { } stores && stores.TryGetValue(arguments[0].Text!, out var store)
            ? store
            : null;

    /// <summary>
    ///     Reads the operation argument (<c>union</c> or <c>intersect</c>).
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="index">The position of the argument.</param>
    /// <returns>The text; null when the argument is missing or not a text.</returns>
    private static string? OperationOf(ReadOnlySpan<DataValue> arguments, int index) =>
        index < arguments.Length && arguments[index].IsString ? arguments[index].Text : null;
}
