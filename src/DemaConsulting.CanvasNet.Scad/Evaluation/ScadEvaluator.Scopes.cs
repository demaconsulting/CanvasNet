using System.Globalization;
using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Evaluation;

// cspell:ignore PI

/// <summary>
///     Scope construction, lazy cells and name lookup.
/// </summary>
internal sealed partial class ScadEvaluator
{
    /// <summary>The value of <c>$fn</c> when nothing sets it.</summary>
    private const double DefaultFn = 0;

    /// <summary>The value of <c>$fa</c> when nothing sets it.</summary>
    private const double DefaultFa = 12;

    /// <summary>The value of <c>$fs</c> when nothing sets it.</summary>
    private const double DefaultFs = 2;

    /// <summary>The outermost scope: <c>PI</c> and the special-variable defaults.</summary>
    private readonly ScadScope _defaults;

    /// <summary>The file scopes of the main unit and of every reachable <c>use</c> library.</summary>
    private readonly Dictionary<ScadSourceUnit, ScadScope> _unitScopes = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    ///     Builds the outermost scope holding <c>PI</c> and the default special variables.
    /// </summary>
    /// <returns>The scope.</returns>
    private ScadScope BuildDefaultsScope()
    {
        var scope = new ScadScope(null, null);
        scope.DefineVariable(new ScadCell("PI", ScadValue.FromNumber(Math.PI), default));
        scope.DefineVariable(new ScadCell("$fn", ScadValue.FromNumber(DefaultFn), default));
        scope.DefineVariable(new ScadCell("$fa", ScadValue.FromNumber(DefaultFa), default));
        scope.DefineVariable(new ScadCell("$fs", ScadValue.FromNumber(DefaultFs), default));
        scope.DefineVariable(new ScadCell("$t", ScadValue.FromNumber(_settings.Time), default));
        scope.DefineVariable(new ScadCell("$preview", ScadValue.True, default));
        return scope;
    }

    /// <summary>
    ///     Builds the file scope of the main unit, wiring up the libraries it imports with <c>use</c>, and forces its
    ///     assignments in source order.
    /// </summary>
    /// <returns>The main file scope.</returns>
    private ScadScope BuildMainScope()
    {
        var importScopes = new Dictionary<ScadSourceUnit, ScadScope>(ReferenceEqualityComparer.Instance);
        var order = new List<ScadSourceUnit>();
        var queue = new Queue<ScadSourceUnit>();
        queue.Enqueue(_unit);

        // Every reachable unit gets its scopes first (iteratively, so library chains cannot deepen the stack);
        // imports are wired afterwards, which makes mutually-using libraries work
        while (queue.Count > 0)
        {
            var unit = queue.Dequeue();
            if (_unitScopes.ContainsKey(unit))
            {
                continue;
            }

            var imports = new ScadScope(_defaults, _defaults.Specials);
            importScopes[unit] = imports;
            _unitScopes[unit] = BuildScope(unit.Statements, imports, imports.Specials, ReferenceEquals(unit, _unit) ? _settings.Overrides : null);
            order.Add(unit);
            foreach (var library in unit.Uses)
            {
                queue.Enqueue(library);
            }
        }

        foreach (var unit in order)
        {
            foreach (var library in unit.Uses)
            {
                importScopes[unit].ImportDefinitionsFrom(_unitScopes[library]);
            }
        }

        var main = _unitScopes[_unit];
        ForceAll(main);
        return main;
    }

    /// <summary>
    ///     Builds a scope for a statement list: assignments become lazy cells and definitions are hoisted, so every
    ///     name is visible throughout the list regardless of position.
    /// </summary>
    /// <param name="statements">The statements.</param>
    /// <param name="parent">The lexical parent.</param>
    /// <param name="specials">The dynamic <c>$</c> chain the scope starts with.</param>
    /// <param name="overrides">The variable overrides to apply (main file scope only), or <see langword="null"/>.</param>
    /// <returns>The scope. Its cells are not forced yet.</returns>
    private ScadScope BuildScope(
        IReadOnlyList<ScadStatement> statements,
        ScadScope parent,
        ScadSpecialFrame? specials,
        IReadOnlyDictionary<string, ScadValue>? overrides = null)
    {
        var scope = new ScadScope(parent, specials);
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case ScadAssignmentStatement assignment when overrides is null || !overrides.ContainsKey(assignment.Name):
                    Define(scope, new ScadCell(assignment.Name, assignment.Value, scope, assignment.Location));
                    break;
                case ScadFunctionDefinition function:
                    DefineFunction(scope, function);
                    break;
                case ScadModuleDefinition module:
                    DefineModule(scope, module);
                    break;
                default:
                    break;
            }
        }

        if (overrides is not null)
        {
            foreach (var name in overrides.Keys.Order(StringComparer.Ordinal))
            {
                scope.DefineVariable(new ScadCell(name, overrides[name], default));
                CheckSpecialDepth(scope, default);
            }
        }

        return scope;
    }

    /// <summary>
    ///     Defines a variable cell in a scope, warning when it replaces an earlier assignment of the same scope.
    /// </summary>
    /// <param name="scope">The scope.</param>
    /// <param name="cell">The cell.</param>
    private void Define(ScadScope scope, ScadCell cell)
    {
        var previous = scope.DefineVariable(cell);
        if (previous is not null)
        {
            Warn($"'{cell.Name}' is assigned more than once in this scope; the last assignment is used", previous.Location);
        }

        CheckSpecialDepth(scope, cell.Location);
    }

    /// <summary>
    ///     Defines a function of a statement list.
    /// </summary>
    /// <param name="scope">The scope.</param>
    /// <param name="function">The definition.</param>
    private void DefineFunction(ScadScope scope, ScadFunctionDefinition function)
    {
        var previous = scope.DefineFunction(new ScadClosure(function.Name, function.Parameters, function.Body, scope, function.Location));
        if (previous is not null)
        {
            Warn($"Function '{function.Name}' is defined more than once in this scope; the last definition is used", previous.Location);
        }
    }

    /// <summary>
    ///     Defines a module of a statement list.
    /// </summary>
    /// <param name="scope">The scope.</param>
    /// <param name="module">The definition.</param>
    private void DefineModule(ScadScope scope, ScadModuleDefinition module)
    {
        var previous = scope.DefineModule(module.Name, new ScadModuleBinding(module, scope));
        if (previous is not null)
        {
            Warn($"Module '{module.Name}' is defined more than once in this scope; the last definition is used", previous.Definition.Location);
        }
    }

    /// <summary>
    ///     Rejects a dynamic <c>$</c> chain deeper than the recursion limit, so recursion that binds special variables
    ///     cannot grow it (and its memory) without bound.
    /// </summary>
    /// <param name="scope">The scope that just bound a name.</param>
    /// <param name="location">The location for the error.</param>
    private void CheckSpecialDepth(ScadScope scope, ScadSourceLocation location)
    {
        if (scope.Specials is { } frame && frame.Depth > _limits.MaxRecursionDepth)
        {
            throw Fail(string.Create(CultureInfo.InvariantCulture, $"Recursion too deep (limit {_limits.MaxRecursionDepth})"), location);
        }
    }

    /// <summary>
    ///     Binds an already evaluated value as a variable of a scope; a repeated name shadows the earlier binding silently.
    /// </summary>
    /// <param name="scope">The scope.</param>
    /// <param name="name">The name.</param>
    /// <param name="value">The value.</param>
    /// <param name="location">The location of the binding.</param>
    private void BindValue(ScadScope scope, string name, ScadValue value, ScadSourceLocation location)
    {
        scope.DefineVariable(new ScadCell(name, value, location));
        CheckSpecialDepth(scope, location);
    }

    /// <summary>
    ///     Forces the lazy cells of a scope in source order, so unreferenced assignments still run their
    ///     <c>echo</c>/<c>assert</c> effects in order.
    /// </summary>
    /// <param name="scope">The scope.</param>
    private void ForceAll(ScadScope scope)
    {
        var cells = scope.LazyCells;
        for (var i = 0; i < cells.Count; i++)
        {
            if (!cells[i].Superseded)
            {
                ForceCell(cells[i]);
            }
        }
    }

    /// <summary>
    ///     Gets the value of a cell, evaluating its expression in its defining scope on first use.
    /// </summary>
    /// <param name="cell">The cell.</param>
    /// <param name="location">The location of the reference, for the circular-reference warning.</param>
    /// <returns>The value; <c>undef</c> for a circular reference.</returns>
    private ScadValue ForceCell(ScadCell cell, ScadSourceLocation? location = null)
    {
        switch (cell.State)
        {
            case ScadCellState.Done:
                return cell.Value;
            case ScadCellState.Evaluating:
                Warn($"Circular reference while evaluating '{cell.Name}'", location ?? cell.Location);
                return ScadValue.Undef;
            default:
                cell.State = ScadCellState.Evaluating;
                cell.Value = Evaluate(cell.Expression!, cell.Scope!);
                cell.State = ScadCellState.Done;
                return cell.Value;
        }
    }

    /// <summary>
    ///     Reads a variable or special variable.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="scope">The scope of the reference.</param>
    /// <param name="location">The location of the reference.</param>
    /// <returns>The value; <c>undef</c> with a warning when the name is not bound.</returns>
    private ScadValue LookupVariable(string name, ScadScope scope, ScadSourceLocation location)
    {
        int hops;
        var cell = ScadScope.IsSpecial(name) ? scope.FindSpecial(name, out hops) : scope.FindVariable(name, out hops);
        ChargeHops(hops);
        if (cell is null)
        {
            Warn($"Ignoring unknown variable '{name}'", location);
            return ScadValue.Undef;
        }

        return ForceCell(cell, location);
    }

    /// <summary>
    ///     Reads a special variable without warning when it is not bound.
    /// </summary>
    /// <param name="name">The special variable name.</param>
    /// <param name="scope">The scope.</param>
    /// <returns>The value, or <c>undef</c>.</returns>
    private ScadValue LookupSpecialQuietly(string name, ScadScope scope)
    {
        var cell = scope.FindSpecial(name, out var hops);
        ChargeHops(hops);
        return cell is null ? ScadValue.Undef : ForceCell(cell);
    }

    /// <summary>
    ///     Builds the scope of a <c>let</c>: bindings are sequential, each seeing the earlier ones.
    /// </summary>
    /// <param name="assignments">The bindings.</param>
    /// <param name="parent">The enclosing scope.</param>
    /// <returns>The new scope.</returns>
    private ScadScope BuildLetScope(IReadOnlyList<ScadAssignment> assignments, ScadScope parent)
    {
        // One scope holds all bindings (a long binding list must not create a long scope chain); each value is
        // evaluated before its own name is bound, so it sees only the earlier bindings
        var scope = new ScadScope(parent, parent.Specials);
        foreach (var assignment in assignments)
        {
            BindValue(scope, assignment.Name, Evaluate(assignment.Value, scope), assignment.Location);
        }

        return scope;
    }

    /// <summary>
    ///     Determines whether a statement list defines names, which requires a scope of its own.
    /// </summary>
    /// <param name="statements">The statements.</param>
    /// <returns><see langword="true"/> when the list contains an assignment or a definition.</returns>
    private static bool DefinesNames(IReadOnlyList<ScadStatement> statements) => statements.Any(IsDeclaration);

    /// <summary>
    ///     Determines whether a statement only declares a name (an assignment or a function or module definition).
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <returns><see langword="true"/> for declarations.</returns>
    private static bool IsDeclaration(ScadStatement statement) =>
        statement is ScadAssignmentStatement or ScadFunctionDefinition or ScadModuleDefinition;

    /// <summary>
    ///     Reads the fragment settings in effect in a scope.
    /// </summary>
    /// <param name="scope">The scope.</param>
    /// <returns>The effective <c>$fn</c>, <c>$fa</c> and <c>$fs</c>.</returns>
    private ScadFragmentSettings CurrentFragments(ScadScope scope) => new(
        NumberOr(LookupSpecialQuietly("$fn", scope), DefaultFn),
        NumberOr(LookupSpecialQuietly("$fa", scope), DefaultFa),
        NumberOr(LookupSpecialQuietly("$fs", scope), DefaultFs));

    /// <summary>
    ///     Returns a value as a number, or a fallback when it is not one.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="fallback">The fallback.</param>
    /// <returns>The number.</returns>
    private static double NumberOr(ScadValue value, double fallback) =>
        value.Kind == ScadValueKind.Number ? value.AsNumber : fallback;
}
