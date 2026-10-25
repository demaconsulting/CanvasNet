using System.Globalization;
using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     Vector literals, list comprehensions and the iteration shared with <c>for</c> statements.
/// </summary>
internal sealed partial class ScadEvaluator
{
    /// <summary>
    ///     The values a <c>for</c> iterator walks: the elements of a vector, the numbers of a range, or one scalar.
    /// </summary>
    /// <param name="Source">The iterated value.</param>
    /// <param name="Count">The number of elements.</param>
    private readonly record struct Iteration(ScadValue Source, long Count)
    {
        /// <summary>
        ///     Gets an element.
        /// </summary>
        /// <param name="index">The zero-based index, less than <see cref="Count"/>.</param>
        /// <returns>The element.</returns>
        public ScadValue ElementAt(long index) => Source.Kind switch
        {
            ScadValueKind.Vector => Source.AsVector[(int)index],
            ScadValueKind.Range => ScadValue.FromNumber(Source.AsRange.ElementAt(index)),
            _ => Source,
        };
    }

    /// <summary>
    ///     Evaluates a vector literal, whose elements may be comprehension elements.
    /// </summary>
    /// <param name="vector">The literal.</param>
    /// <param name="scope">The scope.</param>
    /// <returns>The vector value.</returns>
    private ScadValue EvaluateVector(ScadVectorExpression vector, ScadScope scope)
    {
        var builder = new ListBuilder(this, vector.Location);
        foreach (var element in vector.Elements)
        {
            AppendElement(element, scope, builder);
        }

        return builder.ToValue();
    }

    /// <summary>
    ///     Appends the values one vector element produces: one for an ordinary expression, any number for a
    ///     comprehension element. A comprehension element is one frame and one step.
    /// </summary>
    /// <param name="element">The element expression.</param>
    /// <param name="scope">The scope.</param>
    /// <param name="builder">The list being built.</param>
    private void AppendElement(ScadExpression element, ScadScope scope, ListBuilder builder)
    {
        if (element is not (ScadListForExpression or ScadListCForExpression or ScadListIfExpression or ScadListLetExpression or ScadListEachExpression))
        {
            builder.Add(Evaluate(element, scope));
            return;
        }

        EnterFrame(element.Location);
        Charge(1);
        switch (element)
        {
            case ScadListForExpression loop:
                ForEachBinding(loop.Iterators, 0, scope, iteration => AppendElement(loop.Body, iteration, builder));
                break;
            case ScadListCForExpression cLoop:
                AppendCStyleFor(cLoop, scope, builder);
                break;
            case ScadListIfExpression conditional:
                AppendIf(conditional, scope, builder);
                break;
            case ScadListLetExpression let:
                AppendElement(let.Body, BuildLetScope(let.Assignments, scope), builder);
                break;
            default:
                AppendEach((ScadListEachExpression)element, scope, builder);
                break;
        }

        LeaveFrame();
    }

    /// <summary>
    ///     Appends the element of an <c>if</c>/<c>else</c> comprehension element.
    /// </summary>
    /// <param name="conditional">The element.</param>
    /// <param name="scope">The scope.</param>
    /// <param name="builder">The list being built.</param>
    private void AppendIf(ScadListIfExpression conditional, ScadScope scope, ListBuilder builder)
    {
        var branch = Evaluate(conditional.Condition, scope).IsTrue ? conditional.WhenTrue : conditional.WhenFalse;
        if (branch is not null)
        {
            AppendElement(branch, scope, builder);
        }
    }

    /// <summary>
    ///     Appends the elements spliced by <c>each</c>: a vector's elements or a range's numbers; any other value is
    ///     appended itself.
    /// </summary>
    /// <param name="each">The element.</param>
    /// <param name="scope">The scope.</param>
    /// <param name="builder">The list being built.</param>
    private void AppendEach(ScadListEachExpression each, ScadScope scope, ListBuilder builder)
    {
        var value = Evaluate(each.Value, scope);
        if (value.Kind is not (ScadValueKind.Vector or ScadValueKind.Range))
        {
            builder.Add(value);
            return;
        }

        var iteration = PrepareIteration(value, each.Location);
        CheckLoopBudget(iteration.Count, each.Location);
        for (long i = 0; i < iteration.Count; i++)
        {
            ChargeLoopTurn(each.Location);
            builder.Add(iteration.ElementAt(i));
        }
    }

    /// <summary>
    ///     Appends the elements of a C-style <c>for (init; condition; increment)</c> comprehension element.
    /// </summary>
    /// <remarks>
    ///     The initializers bind sequentially. Each turn evaluates the condition, then the body, then all increments
    ///     simultaneously from the old state into a fresh state, so closures created in the body keep their turn's values.
    /// </remarks>
    /// <param name="loop">The element.</param>
    /// <param name="scope">The scope.</param>
    /// <param name="builder">The list being built.</param>
    private void AppendCStyleFor(ScadListCForExpression loop, ScadScope scope, ListBuilder builder)
    {
        var state = BuildLetScope(loop.Initializers, scope);
        while (Evaluate(loop.Condition, state).IsTrue)
        {
            ChargeLoopTurn(loop.Location);
            AppendElement(loop.Body, state, builder);
            state = NextLoopState(loop, state, scope);
        }
    }

    /// <summary>
    ///     Computes the state of the next turn of a C-style loop: every variable keeps its value except those the
    ///     increments assign, all of which are evaluated from the old state first.
    /// </summary>
    /// <param name="loop">The loop.</param>
    /// <param name="state">The old state.</param>
    /// <param name="scope">The scope the loop started in.</param>
    /// <returns>The new state.</returns>
    private ScadScope NextLoopState(ScadListCForExpression loop, ScadScope state, ScadScope scope)
    {
        var updated = new ScadValue[loop.Increments.Count];
        for (var i = 0; i < updated.Length; i++)
        {
            updated[i] = Evaluate(loop.Increments[i].Value, state);
        }

        var next = new ScadScope(scope, scope.Specials);
        foreach (var initializer in loop.Initializers)
        {
            BindValue(next, initializer.Name, CurrentValue(state, initializer.Name), initializer.Location);
        }

        for (var i = 0; i < updated.Length; i++)
        {
            BindValue(next, loop.Increments[i].Name, updated[i], loop.Increments[i].Location);
        }

        return next;
    }

    /// <summary>
    ///     Reads the value a loop-state variable currently holds.
    /// </summary>
    /// <param name="state">The loop state.</param>
    /// <param name="name">The variable name.</param>
    /// <returns>The value, or <c>undef</c> when the state does not bind it.</returns>
    private ScadValue CurrentValue(ScadScope state, string name)
    {
        var cell = ScadScope.IsSpecial(name) ? state.FindSpecial(name, out _) : state.GetVariable(name);
        return cell is null ? ScadValue.Undef : ForceCell(cell);
    }

    /// <summary>
    ///     Runs a body once per combination of iterator values, the first iterator being the outermost loop.
    /// </summary>
    /// <remarks>
    ///     Every iteration gets a scope binding its iterator variable. The element count of each iterator is checked
    ///     against the remaining loop budget before the first turn, so a huge range is rejected without looping. Each
    ///     level is one frame; each turn charges one step and one loop iteration.
    /// </remarks>
    /// <param name="iterators">The iterator bindings.</param>
    /// <param name="index">The index of the iterator to run.</param>
    /// <param name="scope">The scope with the outer iterators bound.</param>
    /// <param name="body">The body, called with the scope of the innermost turn.</param>
    private void ForEachBinding(IReadOnlyList<ScadAssignment> iterators, int index, ScadScope scope, Action<ScadScope> body)
    {
        if (index == iterators.Count)
        {
            body(scope);
            return;
        }

        var iterator = iterators[index];
        EnterFrame(iterator.Location);
        var iteration = PrepareIteration(Evaluate(iterator.Value, scope), iterator.Location);
        CheckLoopBudget(iteration.Count, iterator.Location);
        for (long i = 0; i < iteration.Count; i++)
        {
            ChargeLoopTurn(iterator.Location);
            var turn = new ScadScope(scope, scope.Specials);
            BindValue(turn, iterator.Name, iteration.ElementAt(i), iterator.Location);
            ForEachBinding(iterators, index + 1, turn, body);
        }

        LeaveFrame();
    }

    /// <summary>
    ///     Determines what an iterated value yields.
    /// </summary>
    /// <param name="source">The value.</param>
    /// <param name="location">The location, for warnings.</param>
    /// <returns>The iteration: elements of a vector, numbers of a range, nothing for <c>undef</c> or an invalid range, one turn for any other value.</returns>
    private Iteration PrepareIteration(ScadValue source, ScadSourceLocation location)
    {
        switch (source.Kind)
        {
            case ScadValueKind.Vector:
                return new Iteration(source, source.AsVector.Count);
            case ScadValueKind.Range:
                if (source.AsRange.IsInvalid)
                {
                    Warn("Ignoring a range with a zero step or a non-finite bound", location);
                    return new Iteration(source, 0);
                }

                return new Iteration(source, source.AsRange.Count);
            case ScadValueKind.Undef:
                Warn("Cannot iterate over undef", location);
                return new Iteration(source, 0);
            default:
                return new Iteration(source, 1);
        }
    }

    /// <summary>
    ///     Reports that a list grew beyond <see cref="ScadValue.MaxNestingDepth"/> levels.
    /// </summary>
    /// <param name="location">The location of the list.</param>
    /// <returns>The exception to throw.</returns>
    private static ScadEvaluationException NestingTooDeep(ScadSourceLocation location) =>
        Fail(string.Create(CultureInfo.InvariantCulture, $"List nesting too deep (limit {ScadValue.MaxNestingDepth})"), location);

    /// <summary>
    ///     Accumulates the elements of a vector under the list-length, nesting and step limits.
    /// </summary>
    private sealed class ListBuilder
    {
        /// <summary>The owning evaluator.</summary>
        private readonly ScadEvaluator _owner;

        /// <summary>The location of the vector literal.</summary>
        private readonly ScadSourceLocation _location;

        /// <summary>The elements so far.</summary>
        private readonly List<ScadValue> _items = [];

        /// <summary>
        ///     Initializes a builder.
        /// </summary>
        /// <param name="owner">The evaluator whose limits apply.</param>
        /// <param name="location">The location used for limit errors.</param>
        public ListBuilder(ScadEvaluator owner, ScadSourceLocation location)
        {
            _owner = owner;
            _location = location;
        }

        /// <summary>
        ///     Appends an element.
        /// </summary>
        /// <param name="value">The element.</param>
        /// <exception cref="ScadEvaluationException">Thrown when the list would be too long or too deeply nested.</exception>
        public void Add(ScadValue value)
        {
            if (_items.Count >= _owner._limits.MaxListLength)
            {
                throw Fail(string.Create(CultureInfo.InvariantCulture, $"List too long (limit {_owner._limits.MaxListLength})"), _location);
            }

            if (value.Depth >= ScadValue.MaxNestingDepth)
            {
                throw NestingTooDeep(_location);
            }

            _owner.Charge(1);
            _items.Add(value);
        }

        /// <summary>
        ///     Creates the vector value.
        /// </summary>
        /// <returns>The vector.</returns>
        public ScadValue ToValue() => ScadValue.FromVector(_items.ToArray());
    }
}
