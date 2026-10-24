namespace DemaConsulting.CanvasNet.Scad.Language;

/// <summary>
///     Measures the height of a finished syntax tree without recursion and rejects trees that are too deep.
/// </summary>
/// <remarks>
///     The parser builds left-associative operator chains and postfix chains in loops, so a hostile
///     <c>1+1+1+…</c> produces an arbitrarily deep left-leaning tree without any parser recursion. Every later
///     consumer of the tree (an evaluator, a dumper, record equality or printing) may recurse, so the tree height
///     itself is bounded here. The measurement uses an explicit stack and is therefore safe for any tree the
///     parser can produce within the source size limit.
/// </remarks>
internal static class ScadTreeDepth
{
    /// <summary>
    ///     Verifies that no node is deeper than the limit; the program node has depth zero.
    /// </summary>
    /// <param name="program">The tree root.</param>
    /// <param name="limit">The maximum permitted node depth.</param>
    /// <exception cref="ScadSyntaxException">Thrown, located at the first node found beyond the limit.</exception>
    public static void Verify(ScadProgram program, int limit)
    {
        // Depth-first traversal with an explicit stack of (node, depth) pairs
        var pending = new Stack<(ScadNode Node, int Depth)>();
        pending.Push((program, 0));
        while (pending.Count > 0)
        {
            var (node, depth) = pending.Pop();
            if (depth > limit)
            {
                throw new ScadSyntaxException($"Nesting too deep (limit {limit})", node.Location);
            }

            foreach (var child in Children(node).OfType<ScadNode>())
            {
                pending.Push((child, depth + 1));
            }
        }
    }

    /// <summary>
    ///     Enumerates the direct children of a node.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The non-null and null (absent optional) children; callers skip nulls.</returns>
    private static IEnumerable<ScadNode?> Children(ScadNode node) => node switch
    {
        ScadProgram p => p.Statements,
        ScadParameter p => [p.Default],
        ScadArgument a => [a.Value],
        ScadAssignment a => [a.Value],
        ScadStatement s => StatementChildren(s),
        ScadExpression e => ExpressionChildren(e),
        _ => [],
    };

    /// <summary>
    ///     Enumerates the direct children of a statement.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <returns>The children (nulls are absent optional children).</returns>
    private static IEnumerable<ScadNode?> StatementChildren(ScadStatement statement) => statement switch
    {
        ScadAssignmentStatement s => [s.Value],
        ScadModuleDefinition s => [.. s.Parameters, .. s.Body],
        ScadFunctionDefinition s => [.. s.Parameters, s.Body],
        ScadModuleInstantiation s => [.. s.Arguments, .. s.Children],
        ScadBlockStatement s => s.Statements,
        ScadModifiedStatement s => [s.Statement],
        ScadIfStatement s => [s.Condition, .. s.Then, .. s.Else ?? []],
        ScadForStatement s => [.. s.Iterators, .. s.Body],
        ScadIntersectionForStatement s => [.. s.Iterators, .. s.Body],
        ScadLetStatement s => [.. s.Assignments, .. s.Body],
        ScadEchoStatement s => [.. s.Arguments, .. s.Children],
        ScadAssertStatement s => [.. s.Arguments, .. s.Children],
        _ => [],
    };

    /// <summary>
    ///     Enumerates the direct children of an expression.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The children (nulls are absent optional children).</returns>
    private static IEnumerable<ScadNode?> ExpressionChildren(ScadExpression expression) => expression switch
    {
        ScadVectorExpression e => e.Elements,
        ScadRangeExpression e => [e.Start, e.Step, e.End],
        ScadUnaryExpression e => [e.Operand],
        ScadBinaryExpression e => [e.Left, e.Right],
        ScadConditionalExpression e => [e.Condition, e.WhenTrue, e.WhenFalse],
        ScadIndexExpression e => [e.Target, e.Index],
        ScadMemberExpression e => [e.Target],
        ScadCallExpression e => [e.Callee, .. e.Arguments],
        ScadFunctionLiteral e => [.. e.Parameters, e.Body],
        ScadLetExpression e => [.. e.Assignments, e.Body],
        ScadEchoExpression e => [.. e.Arguments, e.Body],
        ScadAssertExpression e => [.. e.Arguments, e.Body],
        ScadListForExpression e => [.. e.Iterators, e.Body],
        ScadListCForExpression e => [.. e.Initializers, e.Condition, .. e.Increments, e.Body],
        ScadListIfExpression e => [e.Condition, e.WhenTrue, e.WhenFalse],
        ScadListLetExpression e => [.. e.Assignments, e.Body],
        ScadListEachExpression e => [e.Value],
        _ => [],
    };
}
