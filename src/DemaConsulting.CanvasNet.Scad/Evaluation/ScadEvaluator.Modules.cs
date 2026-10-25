using System.Runtime.CompilerServices;
using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     Module instantiation: user modules, built-in modules and <c>children()</c>.
/// </summary>
internal sealed partial class ScadEvaluator
{
    /// <summary>
    ///     Executes a module instantiation. Lookup order: the <c>children</c> pseudo-module, a user module (innermost
    ///     scope outward), a built-in module, otherwise a warning and nothing is emitted or evaluated.
    /// </summary>
    /// <param name="instantiation">The instantiation.</param>
    /// <param name="scope">The scope.</param>
    /// <param name="output">The instance list.</param>
    private void ExecuteModuleInstantiation(ScadModuleInstantiation instantiation, ScadScope scope, List<ScadInstance> output)
    {
        if (string.Equals(instantiation.Name, "children", StringComparison.Ordinal))
        {
            ExpandChildren(instantiation, scope, output);
            return;
        }

        var module = scope.FindModule(instantiation.Name, out var hops);
        ChargeHops(hops);
        if (module is not null)
        {
            ExecuteUserModule(module, instantiation, scope, output);
        }
        else if (ScadBuiltinModules.IsBuiltin(instantiation.Name))
        {
            ExecuteBuiltinModule(instantiation, scope, output);
        }
        else
        {
            Warn($"Ignoring unknown module '{instantiation.Name}'", instantiation.Location);
        }
    }

    /// <summary>
    ///     Instantiates a built-in module: its arguments are evaluated first, then its children.
    /// </summary>
    /// <param name="instantiation">The instantiation.</param>
    /// <param name="scope">The scope.</param>
    /// <param name="output">The instance list.</param>
    private void ExecuteBuiltinModule(ScadModuleInstantiation instantiation, ScadScope scope, List<ScadInstance> output)
    {
        var values = EvaluateArguments(instantiation.Arguments, scope);
        var arguments = new ScadInstanceArgument[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            arguments[i] = new ScadInstanceArgument(values[i].Name, values[i].Value);
        }

        var children = new List<ScadInstance>();
        ExecuteBlock(instantiation.Children, scope, children);
        output.Add(NewInstance(ScadInstanceKind.BuiltinModule, instantiation.Name, arguments, children, ScadModifiers.None, scope, instantiation.Location));
    }

    /// <summary>
    ///     Instantiates a user module: the arguments are bound, the children are captured for <c>children()</c> and the
    ///     body's instances are wrapped in a group named after the module. A module body is one frame.
    /// </summary>
    /// <param name="module">The module.</param>
    /// <param name="instantiation">The instantiation.</param>
    /// <param name="scope">The scope of the call site.</param>
    /// <param name="output">The instance list.</param>
    private void ExecuteUserModule(ScadModuleBinding module, ScadModuleInstantiation instantiation, ScadScope scope, List<ScadInstance> output)
    {
        var args = EvaluateArguments(instantiation.Arguments, scope);
        EnterFrame(instantiation.Location);
        var children = CaptureChildren(instantiation, scope);
        var site = new CallSite($"module '{module.Definition.Name}'", instantiation.Location);
        var callScope = BindArguments(module.Definition.Parameters, module.Scope, scope.Specials, args, site, children);
        var body = new List<ScadInstance>();
        ExecuteBlock(module.Definition.Body, callScope, body);
        output.Add(NewInstance(ScadInstanceKind.Group, module.Definition.Name, [], body, ScadModifiers.None, callScope, instantiation.Location));
        LeaveFrame();
    }

    /// <summary>
    ///     Captures the child statements of a module call as <c>children()</c> slots without executing them.
    /// </summary>
    /// <remarks>
    ///     Nested braces are flattened; every other statement that is not an assignment or a definition is one slot,
    ///     however many instances it later produces. Assignments and definitions of the child list are bound in a scope
    ///     that extends the call site, which is the scope the slots run in. The <c>$</c> assignments of the list are
    ///     forced once, here, under the call site's dynamic chain, and their frame is remembered separately so that
    ///     <see cref="RunSlot"/> can put it in front of the chain in effect where <c>children()</c> executes.
    /// </remarks>
    /// <param name="instantiation">The instantiation.</param>
    /// <param name="scope">The scope of the call site.</param>
    /// <returns>The captured children.</returns>
    private ScadChildren CaptureChildren(ScadModuleInstantiation instantiation, ScadScope scope)
    {
        var slots = new List<ScadStatement>();
        var declarations = new List<ScadStatement>();
        Flatten(instantiation.Children, slots, declarations);
        var listScope = scope;
        ScadSpecialFrame? listSpecials = null;
        if (declarations.Count > 0)
        {
            listScope = BuildScope(declarations, scope, scope.Specials);
            ForceAll(listScope);

            // Only a frame the list created itself counts; the call site's own frame is never taken from the call site
            listSpecials = listScope.OwnedFrame;
        }

        return new ScadChildren(slots, listScope, instantiation.Location, listSpecials);
    }

    /// <summary>
    ///     Splits a child list into slots and declarations, flattening nested braces.
    /// </summary>
    /// <param name="statements">The statements.</param>
    /// <param name="slots">Receives the slots.</param>
    /// <param name="declarations">Receives assignments and definitions.</param>
    private static void Flatten(IReadOnlyList<ScadStatement> statements, List<ScadStatement> slots, List<ScadStatement> declarations)
    {
        RuntimeHelpers.EnsureSufficientExecutionStack();
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case ScadBlockStatement block:
                    Flatten(block.Statements, slots, declarations);
                    break;
                case ScadAssignmentStatement or ScadFunctionDefinition or ScadModuleDefinition:
                    declarations.Add(statement);
                    break;
                default:
                    slots.Add(statement);
                    break;
            }
        }
    }

    /// <summary>
    ///     Expands <c>children()</c>: all slots, one slot by index, or several by vector or range of indexes.
    /// </summary>
    /// <param name="instantiation">The <c>children</c> instantiation.</param>
    /// <param name="scope">The scope that executes it.</param>
    /// <param name="output">The instance list.</param>
    private void ExpandChildren(ScadModuleInstantiation instantiation, ScadScope scope, List<ScadInstance> output)
    {
        var children = scope.FindChildren();
        if (children is null)
        {
            Warn("children() used outside a module", instantiation.Location);
            return;
        }

        var selector = FindChildSelector(EvaluateArguments(instantiation.Arguments, scope));
        EnterFrame(instantiation.Location);
        if (selector is null)
        {
            for (var i = 0; i < children.Slots.Count; i++)
            {
                RunSlot(children, i, scope, output);
            }
        }
        else
        {
            RunSelectedSlots(selector.Value, children, instantiation.Location, scope, output);
        }

        LeaveFrame();
    }

    /// <summary>
    ///     Picks the index argument of <c>children()</c>: the first positional argument, or the one named <c>index</c>.
    /// </summary>
    /// <param name="args">The evaluated arguments.</param>
    /// <returns>The selector value, or <see langword="null"/> when there is none.</returns>
    private static ScadValue? FindChildSelector((string? Name, ScadValue Value)[] args)
    {
        foreach (var (name, value) in args)
        {
            if (name is null || string.Equals(name, "index", StringComparison.Ordinal))
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>
    ///     Runs the slots chosen by a selector value.
    /// </summary>
    /// <param name="selector">A number, a vector of numbers or a range.</param>
    /// <param name="children">The children.</param>
    /// <param name="location">The location of <c>children()</c>.</param>
    /// <param name="scope">The executing scope.</param>
    /// <param name="output">The instance list.</param>
    private void RunSelectedSlots(ScadValue selector, ScadChildren children, ScadSourceLocation location, ScadScope scope, List<ScadInstance> output)
    {
        switch (selector.Kind)
        {
            case ScadValueKind.Vector:
                foreach (var item in selector.AsVector.AsSpan())
                {
                    Charge(1);
                    RunSlotAt(item, children, location, scope, output);
                }

                break;
            case ScadValueKind.Range:
                var range = selector.AsRange;
                CheckLoopBudget(range.Count, location);
                for (long i = 0; i < range.Count; i++)
                {
                    ChargeLoopTurn(location);
                    RunSlotAt(ScadValue.FromNumber(range.ElementAt(i)), children, location, scope, output);
                }

                break;
            default:
                RunSlotAt(selector, children, location, scope, output);
                break;
        }
    }

    /// <summary>
    ///     Runs the slot an index value refers to, warning when the index is not a valid slot number.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <param name="children">The children.</param>
    /// <param name="location">The location of <c>children()</c>.</param>
    /// <param name="scope">The executing scope.</param>
    /// <param name="output">The instance list.</param>
    private void RunSlotAt(ScadValue index, ScadChildren children, ScadSourceLocation location, ScadScope scope, List<ScadInstance> output)
    {
        if (index.Kind == ScadValueKind.Number
            && ScadNumbers.AreEqual(Math.Floor(index.AsNumber), index.AsNumber)
            && index.AsNumber >= 0
            && index.AsNumber < children.Slots.Count)
        {
            RunSlot(children, (int)index.AsNumber, scope, output);
        }
        else
        {
            Warn("Ignoring invalid child index", location);
        }
    }

    /// <summary>
    ///     Executes one child slot in a fresh scope: lexically the call site (extended by the child list's own
    ///     assignments), dynamically the <c>$</c> chain in effect where <c>children()</c> executes with the <c>$</c>
    ///     assignments of the child list itself in front, so they shadow a same-named binding of the module.
    /// </summary>
    /// <param name="children">The children.</param>
    /// <param name="index">The slot index.</param>
    /// <param name="scope">The executing scope.</param>
    /// <param name="output">The instance list.</param>
    private void RunSlot(ScadChildren children, int index, ScadScope scope, List<ScadInstance> output)
    {
        var specials = children.ListSpecials is null ? scope.Specials : children.ListSpecials.CopyOnto(scope.Specials);
        var slotScope = new ScadScope(children.ListScope, specials);

        // The extra frame counts towards the recursion limit like any other binding of a special variable
        CheckSpecialDepth(slotScope, children.Location);
        Execute(children.Slots[index], slotScope, output);
    }
}
