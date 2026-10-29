using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <content>
///     The predicates of the <c>filter</c> transform (a partial file of the row transform unit).
/// </content>
internal sealed partial class RowTransforms
{
    /// <summary>The comparison operators of a field predicate, in the order they are looked for.</summary>
    private static readonly string[] PredicateOperators = ["equal", "lt", "lte", "gt", "gte", "range", "oneOf", "valid"];

    /// <summary>
    ///     A condition on one row of a table.
    /// </summary>
    private interface IRowPredicate
    {
        /// <summary>
        ///     Tests a row.
        /// </summary>
        /// <param name="table">The table.</param>
        /// <param name="row">The row.</param>
        /// <returns><see langword="true"/> when the row passes.</returns>
        bool Test(DataTable table, int row);
    }

    /// <summary>
    ///     A predicate that does not depend on the row.
    /// </summary>
    /// <param name="Value">The result.</param>
    private sealed class ConstantPredicate(bool Value) : IRowPredicate
    {
        /// <inheritdoc/>
        public bool Test(DataTable table, int row) => Value;
    }

    /// <summary>
    ///     A predicate given as an expression: the row passes when the result is truthy.
    /// </summary>
    /// <param name="Expression">The compiled expression.</param>
    /// <param name="Evaluator">The evaluator of the call.</param>
    private sealed class ExpressionPredicate(CompiledExpression Expression, ExpressionEvaluator Evaluator) : IRowPredicate
    {
        /// <inheritdoc/>
        public bool Test(DataTable table, int row) =>
            ExpressionEvaluator.IsTruthy(Evaluator.Evaluate(Expression, table, row));
    }

    /// <summary>
    ///     A predicate that holds when every (<c>and</c>) or any (<c>or</c>) of its parts holds.
    /// </summary>
    /// <param name="Parts">The parts.</param>
    /// <param name="All">Whether every part must hold (otherwise any).</param>
    private sealed class CompositePredicate(IRowPredicate[] Parts, bool All) : IRowPredicate
    {
        /// <inheritdoc/>
        public bool Test(DataTable table, int row) =>
            All ? Parts.All(part => part.Test(table, row)) : Parts.Any(part => part.Test(table, row));
    }

    /// <summary>
    ///     A predicate that holds when its part does not.
    /// </summary>
    /// <param name="Part">The part.</param>
    private sealed class NotPredicate(IRowPredicate Part) : IRowPredicate
    {
        /// <inheritdoc/>
        public bool Test(DataTable table, int row) => !Part.Test(table, row);
    }

    /// <summary>
    ///     A predicate on the value of a field.
    /// </summary>
    /// <param name="Reader">The reader of the field.</param>
    /// <param name="Check">The condition on the value.</param>
    private sealed class FieldPredicate(FieldReader Reader, Func<DataValue, bool> Check) : IRowPredicate
    {
        /// <inheritdoc/>
        public bool Test(DataTable table, int row) => Check(Reader.Read(row));
    }

    /// <summary>
    ///     Builds the predicate of a <c>filter</c> value.
    /// </summary>
    /// <remarks>
    ///     The recursion follows the nesting of the predicate, which the specification parser bounded by the JSON depth
    ///     limit.
    /// </remarks>
    /// <param name="node">An expression text or a predicate object.</param>
    /// <param name="table">The table the predicate tests.</param>
    /// <returns>The predicate.</returns>
    private IRowPredicate BuildPredicate(SpecNode node, DataTable table)
    {
        if (node.IsString)
        {
            return _context.Tree.Expressions.Get(node) is { } expression
                ? new ExpressionPredicate(expression, _context.Evaluator)
                : new ConstantPredicate(true);
        }

        foreach (var composition in new[] { "and", "or" })
        {
            if (node[composition] is { IsArray: true } items)
            {
                return new CompositePredicate([.. items.Items.Select(item => BuildPredicate(item, table))], composition == "and");
            }
        }

        if (node["not"] is { } negated)
        {
            return new NotPredicate(BuildPredicate(negated, table));
        }

        if (node["param"] is { } parameter)
        {
            // A variable parameter passes every row or none; a selection parameter has no value here and passes every row
            return new ConstantPredicate(!_context.Parameters.TryGetValue(parameter.StringValue!, out var value) || ExpressionEvaluator.IsTruthy(value));
        }

        if (node["selection"] is not null)
        {
            return new ConstantPredicate(true);
        }

        return BuildFieldPredicate(node, table);
    }

    /// <summary>
    ///     Builds the predicate of a field and one comparison.
    /// </summary>
    /// <param name="node">The predicate object.</param>
    /// <param name="table">The table the predicate tests.</param>
    /// <returns>The predicate; one that always holds for a comparison with a date object, which is not supported.</returns>
    private IRowPredicate BuildFieldPredicate(SpecNode node, DataTable table)
    {
        var comparison = PredicateOperators.Select(name => node[name]).FirstOrDefault(n => n is not null);
        if (comparison is null || comparison.IsObject || comparison.Items.Any(i => i.IsObject))
        {
            // A date object cannot be compared yet; the catalog reports it and the predicate holds
            return new ConstantPredicate(true);
        }

        var reader = new FieldReader(table, node["field"]!.StringValue);
        var maxLength = _context.Limits.MaxStringLength;
        switch (comparison.Name)
        {
            case "equal":
                var equal = SpecValues.ToDataValue(comparison);
                return new FieldPredicate(reader, a => ExpressionEvaluator.StrictEquals(a, equal));
            case "lt":
                var lt = SpecValues.ToDataValue(comparison);
                return new FieldPredicate(reader, a => ExpressionEvaluator.RelationalLess(a, lt, maxLength) == 1);
            case "lte":
                var lte = SpecValues.ToDataValue(comparison);
                return new FieldPredicate(reader, a => ExpressionEvaluator.RelationalLess(lte, a, maxLength) == 0);
            case "gt":
                var gt = SpecValues.ToDataValue(comparison);
                return new FieldPredicate(reader, a => ExpressionEvaluator.RelationalLess(gt, a, maxLength) == 1);
            case "gte":
                var gte = SpecValues.ToDataValue(comparison);
                return new FieldPredicate(reader, a => ExpressionEvaluator.RelationalLess(a, gte, maxLength) == 0);
            case "range":
                return BuildRange(reader, comparison, maxLength);
            case "oneOf":
                var members = comparison.Items.Select(SpecValues.ToDataValue).ToArray();
                return new FieldPredicate(reader, a => members.Any(m => ExpressionEvaluator.StrictEquals(a, m)));
            default:
                var wanted = comparison.BoolValue;
                return new FieldPredicate(reader, a => IsValid(a) == wanted);
        }
    }

    /// <summary>
    ///     Builds the predicate of an inclusive range.
    /// </summary>
    /// <param name="reader">The reader of the field.</param>
    /// <param name="range">The array of the two endpoints.</param>
    /// <param name="maxLength">The longest text that an array is converted to in a comparison.</param>
    /// <returns>The predicate.</returns>
    private static FieldPredicate BuildRange(FieldReader reader, SpecNode range, int maxLength)
    {
        var low = SpecValues.ToDataValue(range.Items[0]);
        var high = SpecValues.ToDataValue(range.Items[1]);
        if (!low.IsNull && !high.IsNull && ExpressionEvaluator.RelationalLess(high, low, maxLength) == 1)
        {
            (low, high) = (high, low);
        }

        var lower = low;
        var upper = high;
        return new FieldPredicate(
            reader,
            a => (lower.IsNull || ExpressionEvaluator.RelationalLess(a, lower, maxLength) == 0)
                && (upper.IsNull || ExpressionEvaluator.RelationalLess(upper, a, maxLength) == 0));
    }

    /// <summary>
    ///     Determines whether a value is valid: not null and not NaN (Vega's <c>isValid</c>).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> for a valid value.</returns>
    private static bool IsValid(DataValue value) => !value.IsNull && !(value.IsNumber && double.IsNaN(value.Number));
}
