using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     Function calls: callee resolution, argument binding, closures and tail calls.
/// </summary>
internal sealed partial class ScadEvaluator
{
    /// <summary>
    ///     The call a function body ended with, or its value.
    /// </summary>
    /// <param name="Value">The value when the body produced one.</param>
    /// <param name="Closure">The function to call next when the body ended in a tail call; otherwise <see langword="null"/>.</param>
    /// <param name="Args">The evaluated arguments of the tail call.</param>
    /// <param name="Scope">The scope the tail call was made from (its dynamic parent).</param>
    /// <param name="Location">The location of the tail call.</param>
    private readonly record struct TailOutcome(
        ScadValue Value,
        ScadClosure? Closure,
        (string? Name, ScadValue Value)[]? Args,
        ScadScope? Scope,
        ScadSourceLocation Location);

    /// <summary>
    ///     What a call expression resolved to.
    /// </summary>
    /// <param name="Closure">The user function or closure to call.</param>
    /// <param name="Builtin">The built-in function name to call.</param>
    private readonly record struct ResolvedCall(ScadClosure? Closure, string? Builtin);

    /// <summary>
    ///     Describes the callee of a call for warnings.
    /// </summary>
    /// <param name="Label">For example <c>function 'f'</c> or <c>module 'm'</c>.</param>
    /// <param name="Location">The call site.</param>
    private readonly record struct CallSite(string Label, ScadSourceLocation Location);

    /// <summary>
    ///     Evaluates a call expression.
    /// </summary>
    /// <param name="call">The call.</param>
    /// <param name="scope">The scope of the call site.</param>
    /// <returns>The result; <c>undef</c> with a warning when the callee is unknown or not callable.</returns>
    private ScadValue EvaluateCall(ScadCallExpression call, ScadScope scope) =>
        CompleteCall(call, ResolveCall(call, scope), scope);

    /// <summary>
    ///     Resolves the callee of a call: a user function beats a variable holding a function, which beats a built-in.
    /// </summary>
    /// <param name="call">The call.</param>
    /// <param name="scope">The scope of the call site.</param>
    /// <returns>The resolved callee; empty (with a warning) when there is none.</returns>
    private ResolvedCall ResolveCall(ScadCallExpression call, ScadScope scope)
    {
        if (call.Callee is ScadIdentifierExpression identifier)
        {
            var function = FindFunction(identifier.Name, scope);
            if (function is not null)
            {
                return new ResolvedCall(function, null);
            }

            if (ScadBuiltinFunctions.IsKnown(identifier.Name))
            {
                return new ResolvedCall(null, identifier.Name);
            }

            Warn($"Ignoring unknown function '{identifier.Name}'", identifier.Location);
            return default;
        }

        var callee = Evaluate(call.Callee, scope);
        if (callee.Kind == ScadValueKind.Function && callee.AsFunction is ScadClosure closure)
        {
            return new ResolvedCall(closure, null);
        }

        Warn("Cannot call a value that is not a function", call.Location);
        return default;
    }

    /// <summary>
    ///     Finds the user function a name refers to: per scope, innermost outward, a function definition beats a
    ///     variable that holds a function value.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="scope">The scope of the call site.</param>
    /// <returns>The function, or <see langword="null"/>.</returns>
    private ScadClosure? FindFunction(string name, ScadScope scope)
    {
        var hops = 0;
        ScadClosure? found = null;
        for (var current = scope; current is not null && found is null; current = current.Parent)
        {
            hops++;
            found = current.GetFunction(name) ?? FunctionValueOf(current.GetVariable(name));
        }

        ChargeHops(hops);
        return found;
    }

    /// <summary>
    ///     Reads a variable cell as a function value without disturbing a cell that is being evaluated.
    /// </summary>
    /// <param name="cell">The cell, or <see langword="null"/>.</param>
    /// <returns>The closure the cell holds, or <see langword="null"/>.</returns>
    private ScadClosure? FunctionValueOf(ScadCell? cell)
    {
        if (cell is null || cell.State == ScadCellState.Evaluating)
        {
            return null;
        }

        var value = ForceCell(cell);
        return value.Kind == ScadValueKind.Function ? value.AsFunction as ScadClosure : null;
    }

    /// <summary>
    ///     Evaluates the arguments of a resolved call and performs it.
    /// </summary>
    /// <param name="call">The call.</param>
    /// <param name="target">What the callee resolved to.</param>
    /// <param name="scope">The scope of the call site.</param>
    /// <returns>The result.</returns>
    private ScadValue CompleteCall(ScadCallExpression call, ResolvedCall target, ScadScope scope)
    {
        if (target.Closure is null && target.Builtin is null)
        {
            return ScadValue.Undef;
        }

        var args = EvaluateArguments(call.Arguments, scope);
        if (target.Closure is not null)
        {
            return InvokeClosure(target.Closure, scope, args, call.Location);
        }

        _hostLocation = call.Location;
        ScadBuiltinFunctions.TryInvoke(target.Builtin!, args, this, out var result);
        return result;
    }

    /// <summary>
    ///     Evaluates call arguments eagerly, left to right.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="scope">The scope to evaluate them in.</param>
    /// <returns>The evaluated arguments in call order.</returns>
    private (string? Name, ScadValue Value)[] EvaluateArguments(IReadOnlyList<ScadArgument> arguments, ScadScope scope)
    {
        if (arguments.Count == 0)
        {
            return [];
        }

        var values = new (string? Name, ScadValue Value)[arguments.Count];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = (arguments[i].Name, Evaluate(arguments[i].Value, scope));
        }

        return values;
    }

    /// <summary>
    ///     Calls a user function or closure, running tail calls in a loop so they add no frame.
    /// </summary>
    /// <param name="closure">The function.</param>
    /// <param name="callerScope">The scope of the call (its dynamic chain becomes the callee's dynamic parent).</param>
    /// <param name="args">The evaluated arguments.</param>
    /// <param name="location">The call site.</param>
    /// <returns>The result.</returns>
    private ScadValue InvokeClosure(ScadClosure closure, ScadScope callerScope, (string? Name, ScadValue Value)[] args, ScadSourceLocation location)
    {
        while (true)
        {
            var label = closure.Name is null ? "function literal" : $"function '{closure.Name}'";
            var callScope = BindArguments(closure.Parameters, closure.Scope, callerScope.Specials, args, new CallSite(label, location), null);
            var outcome = EvaluateTail(closure.Body, callScope);
            if (outcome.Closure is null)
            {
                return outcome.Value;
            }

            closure = outcome.Closure;
            args = outcome.Args!;
            callerScope = outcome.Scope!;
            location = outcome.Location;
        }
    }

    /// <summary>
    ///     Evaluates a function body, unwrapping the positions where a call is a tail call (conditional branches and the
    ///     bodies of <c>let</c>, <c>echo</c> and <c>assert</c>) without adding frames.
    /// </summary>
    /// <param name="expression">The body.</param>
    /// <param name="scope">The call scope.</param>
    /// <returns>The value, or the user-function call the body ends with.</returns>
    private TailOutcome EvaluateTail(ScadExpression expression, ScadScope scope)
    {
        while (true)
        {
            switch (expression)
            {
                case ScadConditionalExpression conditional:
                    Charge(1);
                    expression = Evaluate(conditional.Condition, scope).IsTrue ? conditional.WhenTrue : conditional.WhenFalse;
                    break;
                case ScadLetExpression let:
                    Charge(1);
                    scope = BuildLetScope(let.Assignments, scope);
                    expression = let.Body;
                    break;
                case ScadEchoExpression echo:
                    Charge(1);
                    DoEcho(echo.Arguments, scope, echo.Location);
                    expression = echo.Body;
                    break;
                case ScadAssertExpression assert:
                    Charge(1);
                    DoAssert(assert.Arguments, scope, assert.Location);
                    expression = assert.Body;
                    break;
                case ScadCallExpression call:
                    return TailCall(call, scope);
                default:
                    return new TailOutcome(Evaluate(expression, scope), null, null, null, default);
            }
        }
    }

    /// <summary>
    ///     Handles a call in tail position: a user function is returned for the caller's loop to run; anything else is
    ///     performed directly.
    /// </summary>
    /// <param name="call">The call.</param>
    /// <param name="scope">The scope of the call site.</param>
    /// <returns>The outcome.</returns>
    private TailOutcome TailCall(ScadCallExpression call, ScadScope scope)
    {
        Charge(1);
        var target = ResolveCall(call, scope);
        if (target.Closure is null)
        {
            return new TailOutcome(CompleteCall(call, target, scope), null, null, null, default);
        }

        return new TailOutcome(default, target.Closure, EvaluateArguments(call.Arguments, scope), scope, call.Location);
    }

    /// <summary>
    ///     Creates the scope of a call and binds the arguments to the parameters.
    /// </summary>
    /// <remarks>
    ///     Positional arguments fill the parameters in order and named arguments bind by name, overriding a positional
    ///     binding of the same parameter. A named argument starting with <c>$</c> that is not a parameter binds a special
    ///     variable. Parameters are then added to the scope one at a time in declaration order: the supplied argument,
    ///     else the default evaluated in the scope as built so far, else <c>undef</c>.
    /// </remarks>
    /// <param name="parameters">The formal parameters.</param>
    /// <param name="definingScope">The lexical parent of the call scope.</param>
    /// <param name="dynamicSpecials">The dynamic <c>$</c> chain of the caller.</param>
    /// <param name="args">The evaluated arguments.</param>
    /// <param name="site">The call site description.</param>
    /// <param name="children">The children of a module call, or <see langword="null"/>.</param>
    /// <returns>The call scope.</returns>
    private ScadScope BindArguments(
        IReadOnlyList<ScadParameter> parameters,
        ScadScope definingScope,
        ScadSpecialFrame? dynamicSpecials,
        (string? Name, ScadValue Value)[] args,
        CallSite site,
        ScadChildren? children)
    {
        Charge(1);
        var scope = new ScadScope(definingScope, dynamicSpecials);
        if (children is not null)
        {
            scope.Children = children;
            BindValue(scope, "$children", ScadValue.FromNumber(children.Slots.Count), site.Location);
        }

        var values = new ScadValue[parameters.Count];
        var supplied = new bool[parameters.Count];
        var position = 0;
        foreach (var (name, value) in args)
        {
            var slot = name is null ? position++ : IndexOfParameter(parameters, name);
            if (slot >= 0 && slot < values.Length)
            {
                values[slot] = value;
                supplied[slot] = true;
            }
            else if (name is not null && ScadScope.IsSpecial(name))
            {
                BindValue(scope, name, value, site.Location);
            }
            else
            {
                Warn(name is null ? $"Ignoring extra argument for {site.Label}" : $"Ignoring unknown argument '{name}' for {site.Label}", site.Location);
            }
        }

        for (var i = 0; i < values.Length; i++)
        {
            var parameter = parameters[i];
            var value = supplied[i] ? values[i] : EvaluateDefault(parameter, scope);
            BindValue(scope, parameter.Name, value, parameter.Location);
        }

        return scope;
    }

    /// <summary>
    ///     Evaluates the default of a parameter that received no argument.
    /// </summary>
    /// <param name="parameter">The parameter.</param>
    /// <param name="scope">The call scope as built so far.</param>
    /// <returns>The default value, or <c>undef</c> when there is none.</returns>
    private ScadValue EvaluateDefault(ScadParameter parameter, ScadScope scope) =>
        parameter.Default is null ? ScadValue.Undef : Evaluate(parameter.Default, scope);

    /// <summary>
    ///     Finds a parameter by name.
    /// </summary>
    /// <param name="parameters">The parameters.</param>
    /// <param name="name">The name.</param>
    /// <returns>The index, or -1.</returns>
    private static int IndexOfParameter(IReadOnlyList<ScadParameter> parameters, string name)
    {
        for (var i = 0; i < parameters.Count; i++)
        {
            if (string.Equals(parameters[i].Name, name, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
