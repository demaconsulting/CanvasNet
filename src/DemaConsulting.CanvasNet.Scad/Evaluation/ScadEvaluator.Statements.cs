using System.Globalization;
using System.Text;
using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     Statement execution, loops, <c>echo</c> and <c>assert</c>.
/// </summary>
internal sealed partial class ScadEvaluator
{
    /// <summary>The text every echo line starts with; it does not count against the string length limit.</summary>
    private const string EchoPrefix = "ECHO: ";

    /// <summary>The first echo argument that turns a statement-form <c>echo</c> into an anchor declaration.</summary>
    private const string AnchorEchoTag = "anchor";

    /// <summary>
    ///     Executes the statements of a list that produce instances; assignments and definitions were already
    ///     bound when the list's scope was built.
    /// </summary>
    /// <param name="statements">The statements.</param>
    /// <param name="scope">The scope of the list.</param>
    /// <param name="output">The list that receives the produced instances.</param>
    private void ExecuteStatements(IReadOnlyList<ScadStatement> statements, ScadScope scope, List<ScadInstance> output)
    {
        foreach (var statement in statements.Where(s => !IsDeclaration(s)))
        {
            Execute(statement, scope, output);
        }
    }

    /// <summary>
    ///     Executes a statement list as a block: when it defines names it gets a scope of its own whose assignments are
    ///     forced in source order first.
    /// </summary>
    /// <param name="statements">The statements.</param>
    /// <param name="parent">The enclosing scope.</param>
    /// <param name="output">The list that receives the produced instances.</param>
    private void ExecuteBlock(IReadOnlyList<ScadStatement> statements, ScadScope parent, List<ScadInstance> output)
    {
        if (statements.Count == 0)
        {
            return;
        }

        var scope = parent;
        if (DefinesNames(statements))
        {
            scope = BuildScope(statements, parent, parent.Specials);
            ForceAll(scope);
        }

        ExecuteStatements(statements, scope, output);
    }

    /// <summary>
    ///     Executes one statement. Every statement is one frame and one step.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="scope">The scope.</param>
    /// <param name="output">The list that receives the produced instances.</param>
    private void Execute(ScadStatement statement, ScadScope scope, List<ScadInstance> output)
    {
        EnterFrame(statement.Location);
        Charge(1);
        switch (statement)
        {
            case ScadModuleInstantiation instantiation:
                ExecuteModuleInstantiation(instantiation, scope, output);
                break;
            case ScadBlockStatement block:
                ExecuteBlock(block.Statements, scope, output);
                break;
            case ScadModifiedStatement modified:
                ExecuteModified(modified, scope, output);
                break;
            case ScadIfStatement conditional:
                ExecuteIf(conditional, scope, output);
                break;
            case ScadForStatement loop:
                ExecuteFor(loop, scope, output);
                break;
            case ScadIntersectionForStatement intersection:
                ExecuteIntersectionFor(intersection, scope, output);
                break;
            case ScadLetStatement let:
                ExecuteBlock(let.Body, BuildLetScope(let.Assignments, scope), output);
                break;
            case ScadEchoStatement echo:
                DoEcho(echo.Arguments, scope, echo.Location, output);
                ExecuteBlock(echo.Children, scope, output);
                break;
            case ScadAssertStatement assert:
                DoAssert(assert.Arguments, scope, assert.Location);
                ExecuteBlock(assert.Children, scope, output);
                break;
            default:
                break;
        }

        LeaveFrame();
    }

    /// <summary>
    ///     Executes an <c>if</c> statement.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="scope">The scope.</param>
    /// <param name="output">The instance list.</param>
    private void ExecuteIf(ScadIfStatement statement, ScadScope scope, List<ScadInstance> output)
    {
        var branch = Evaluate(statement.Condition, scope).IsTrue ? statement.Then : statement.Else;
        if (branch is not null)
        {
            ExecuteBlock(branch, scope, output);
        }
    }

    /// <summary>
    ///     Executes a <c>for</c> statement into one group holding the instances of all iterations.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="scope">The scope.</param>
    /// <param name="output">The instance list.</param>
    private void ExecuteFor(ScadForStatement statement, ScadScope scope, List<ScadInstance> output)
    {
        var children = new List<ScadInstance>();
        ForEachBinding(statement.Iterators, 0, scope, iteration => ExecuteBlock(statement.Body, iteration, children));
        output.Add(NewInstance(ScadInstanceKind.Group, "for", [], children, ScadModifiers.None, scope, statement.Location));
    }

    /// <summary>
    ///     Executes an <c>intersection_for</c> statement into an <c>intersection</c> of one group per iteration.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="scope">The scope.</param>
    /// <param name="output">The instance list.</param>
    private void ExecuteIntersectionFor(ScadIntersectionForStatement statement, ScadScope scope, List<ScadInstance> output)
    {
        var iterations = new List<ScadInstance>();
        ForEachBinding(
            statement.Iterators,
            0,
            scope,
            iteration =>
            {
                var children = new List<ScadInstance>();
                ExecuteBlock(statement.Body, iteration, children);
                iterations.Add(NewInstance(ScadInstanceKind.Group, "intersection_for", [], children, ScadModifiers.None, iteration, statement.Location));
            });
        output.Add(NewInstance(ScadInstanceKind.BuiltinModule, "intersection", [], iterations, ScadModifiers.None, scope, statement.Location));
    }

    /// <summary>
    ///     Executes a statement carrying modifiers: <c>*</c> prunes it, the others are merged onto its instance.
    /// </summary>
    /// <param name="statement">The modified statement.</param>
    /// <param name="scope">The scope.</param>
    /// <param name="output">The instance list.</param>
    private void ExecuteModified(ScadModifiedStatement statement, ScadScope scope, List<ScadInstance> output)
    {
        if ((statement.Modifiers & ScadModifiers.Disable) != 0)
        {
            return;
        }

        var inner = new List<ScadInstance>();
        Execute(statement.Statement, scope, inner);
        var flags = statement.Modifiers & (ScadModifiers.ShowOnly | ScadModifiers.Highlight | ScadModifiers.Background);
        if (inner.Count == 1)
        {
            output.Add(inner[0] with { Modifiers = inner[0].Modifiers | flags });
        }
        else if (inner.Count > 1)
        {
            output.Add(NewInstance(ScadInstanceKind.Group, "group", [], inner, flags, scope, statement.Location));
        }
    }

    /// <summary>
    ///     Creates an instance, counting it against the instance limit and capturing the fragment settings in effect.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="name">The name.</param>
    /// <param name="arguments">The evaluated arguments.</param>
    /// <param name="children">The child instances.</param>
    /// <param name="modifiers">The modifiers.</param>
    /// <param name="scope">The scope the instance was created in.</param>
    /// <param name="location">The location.</param>
    /// <returns>The instance.</returns>
    private ScadInstance NewInstance(
        ScadInstanceKind kind,
        string name,
        IReadOnlyList<ScadInstanceArgument> arguments,
        IReadOnlyList<ScadInstance> children,
        ScadModifiers modifiers,
        ScadScope scope,
        ScadSourceLocation location)
    {
        if (++_instances > _limits.MaxInstances)
        {
            throw Fail(string.Create(CultureInfo.InvariantCulture, $"Instance limit exceeded (limit {_limits.MaxInstances})"), location);
        }

        var fragments = CurrentFragments(scope);
        foreach (var argument in arguments)
        {
            fragments = OverrideFragment(fragments, argument);
        }

        return new ScadInstance(kind, name, arguments, children, modifiers, fragments, location);
    }

    /// <summary>
    ///     Applies a named <c>$fn</c>, <c>$fa</c> or <c>$fs</c> argument to fragment settings.
    /// </summary>
    /// <param name="fragments">The settings.</param>
    /// <param name="argument">The argument.</param>
    /// <returns>The settings with the argument applied when it is a numeric fragment argument.</returns>
    private static ScadFragmentSettings OverrideFragment(ScadFragmentSettings fragments, ScadInstanceArgument argument)
    {
        if (argument.Name is null || argument.Value.Kind != ScadValueKind.Number)
        {
            return fragments;
        }

        var number = argument.Value.AsNumber;
        return argument.Name switch
        {
            "$fn" => fragments with { Fn = number },
            "$fa" => fragments with { Fa = number },
            "$fs" => fragments with { Fs = number },
            _ => fragments,
        };
    }

    /// <summary>
    ///     Performs the side effect of <c>echo</c>: records one <c>ECHO: </c> line and, for a statement-form
    ///     <c>echo("anchor", ...)</c>, declares an anchor.
    /// </summary>
    /// <remarks>
    ///     The arguments are evaluated once; the same values feed the echo text and the anchor check. The echo line is
    ///     recorded exactly as for any other echo, whether or not the anchor is valid, so the declaration stays
    ///     visible in the echo output.
    /// </remarks>
    /// <param name="arguments">The arguments.</param>
    /// <param name="scope">The scope.</param>
    /// <param name="location">The location of the echo.</param>
    /// <param name="output">
    ///     The instance list that receives an <see cref="ScadInstanceKind.Anchor"/> instance, or <see langword="null"/>
    ///     for the expression form, which has no instance list and therefore never declares an anchor.
    /// </param>
    private void DoEcho(IReadOnlyList<ScadArgument> arguments, ScadScope scope, ScadSourceLocation location, List<ScadInstance>? output)
    {
        var line = new StringBuilder(EchoPrefix);
        var values = new List<(string? Name, ScadValue Value)>(arguments.Count);
        for (var i = 0; i < arguments.Count; i++)
        {
            if (i > 0)
            {
                line.Append(", ");
            }

            var value = Evaluate(arguments[i].Value, scope);
            ChargeDeep(value);
            values.Add((arguments[i].Name, value));
            if (arguments[i].Name is { } name)
            {
                line.Append(name).Append(" = ");
            }

            if (!value.TryToEchoText(_limits.MaxStringLength, out var text, this) || (line.Length - EchoPrefix.Length) + text.Length > _limits.MaxStringLength)
            {
                throw Fail(string.Create(CultureInfo.InvariantCulture, $"String too long (limit {_limits.MaxStringLength})"), location);
            }

            line.Append(text);
        }

        _sink.Echo(line.ToString(), location);

        // A statement-form echo whose first argument is the unnamed string "anchor" declares an anchor
        if (output is not null && values.Count > 0 && values[0].Name is null
            && values[0].Value.Kind == ScadValueKind.String && values[0].Value.AsString == AnchorEchoTag)
        {
            EmitAnchor(values, scope, location, output);
        }
    }

    /// <summary>
    ///     Turns the arguments of an <c>echo("anchor", name, [x, y, z])</c> statement into an anchor instance, or
    ///     reports why they are malformed.
    /// </summary>
    /// <remarks>
    ///     A valid anchor has exactly three unnamed arguments: the tag, a non-empty string name and a vector of exactly
    ///     three finite numbers. Anything else produces one <see cref="ScadDiagnosticCodes.AnchorInvalid"/> warning and no instance;
    ///     the evaluation continues.
    /// </remarks>
    /// <param name="values">The evaluated arguments (name and value), the first being the tag.</param>
    /// <param name="scope">The scope the echo executes in.</param>
    /// <param name="location">The location of the echo.</param>
    /// <param name="output">The instance list that receives the anchor.</param>
    private void EmitAnchor(IReadOnlyList<(string? Name, ScadValue Value)> values, ScadScope scope, ScadSourceLocation location, List<ScadInstance> output)
    {
        // Check the shape of the call first: tag, name and point, all positional
        if (values.Count != 3 || values[1].Name is not null || values[2].Name is not null)
        {
            Warn(ScadDiagnosticCodes.AnchorInvalid, $"{ScadDiagnosticCodes.AnchorInvalid}: expected echo(\"anchor\", \"name\", [x, y, z]) with exactly three unnamed arguments; the anchor is skipped", location);
            return;
        }

        // The name must be a non-empty string
        var nameValue = values[1].Value;
        if (nameValue.Kind != ScadValueKind.String || nameValue.AsString.Length == 0)
        {
            Warn(ScadDiagnosticCodes.AnchorInvalid, $"{ScadDiagnosticCodes.AnchorInvalid}: the anchor name must be a non-empty string; the anchor is skipped", location);
            return;
        }

        // The point must be a vector of exactly three finite numbers
        var point = values[2].Value;
        if (point.Kind != ScadValueKind.Vector || point.AsVector.Count != 3 || !IsFiniteNumber(point.AsVector[0])
            || !IsFiniteNumber(point.AsVector[1]) || !IsFiniteNumber(point.AsVector[2]))
        {
            Warn(ScadDiagnosticCodes.AnchorInvalid, $"{ScadDiagnosticCodes.AnchorInvalid}: the anchor '{nameValue.AsString}' needs a point [x, y, z] of three finite numbers; the anchor is skipped", location);
            return;
        }

        _sink.AnchorsDeclared = true;
        output.Add(NewInstance(ScadInstanceKind.Anchor, nameValue.AsString, [new ScadInstanceArgument(null, point)], [], ScadModifiers.None, scope, location));
    }

    /// <summary>
    ///     Determines whether a value is a finite number.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when the value is a number that is neither NaN nor infinite.</returns>
    private static bool IsFiniteNumber(ScadValue value) => value.Kind == ScadValueKind.Number && double.IsFinite(value.AsNumber);

    /// <summary>
    ///     Performs the check of <c>assert</c>.
    /// </summary>
    /// <remarks>
    ///     The first positional argument (or the one named <c>condition</c>) is the condition and the second (or the
    ///     one named <c>message</c>) the message. A call without a condition cannot hold and fails like a false
    ///     condition. Surplus positional arguments and unknown named arguments are ignored with a warning, like the
    ///     arguments of the built-in functions; they are still evaluated.
    /// </remarks>
    /// <param name="arguments">The arguments: the condition and an optional message (positional or named).</param>
    /// <param name="scope">The scope.</param>
    /// <param name="location">The location of the assert.</param>
    /// <exception cref="ScadEvaluationException">Thrown when the condition is false or missing.</exception>
    private void DoAssert(IReadOnlyList<ScadArgument> arguments, ScadScope scope, ScadSourceLocation location)
    {
        ScadValue? condition = null;
        ScadValue? message = null;
        var position = 0;
        var extra = false;
        foreach (var argument in arguments)
        {
            var value = Evaluate(argument.Value, scope);
            var slot = argument.Name;
            if (slot is null && position < 2)
            {
                slot = position++ == 0 ? "condition" : "message";
            }
            if (string.Equals(slot, "condition", StringComparison.Ordinal))
            {
                condition = value;
            }
            else if (string.Equals(slot, "message", StringComparison.Ordinal))
            {
                message = value;
            }
            else if (slot is null)
            {
                extra = true;
            }
            else
            {
                Warn(ScadDiagnosticCodes.UnknownArgument, $"Ignoring unknown argument '{slot}' of assert", location);
            }
        }

        if (extra)
        {
            Warn(ScadDiagnosticCodes.ArgumentCount, "Ignoring extra arguments to assert", location);
        }

        if (condition is null)
        {
            throw Fail(message is null ? "Assertion failed: the condition argument is missing" : AssertionMessage(message), location);
        }

        if (!condition.Value.IsTrue)
        {
            throw Fail(AssertionMessage(message), location);
        }
    }
    /// <summary>
    ///     Builds the description of a failed assertion.
    /// </summary>
    /// <param name="message">The message value, or <see langword="null"/> when none was supplied.</param>
    /// <returns><c>Assertion failed</c>, followed by <c>: </c> and the message when one was supplied.</returns>
    private string AssertionMessage(ScadValue? message)
    {
        if (message is null)
        {
            return "Assertion failed";
        }

        if (!message.Value.TryToStr(_limits.MaxStringLength, out var text, this))
        {
            throw Fail(string.Create(CultureInfo.InvariantCulture, $"String too long (limit {_limits.MaxStringLength})"), _lastFrameLocation);
        }

        return $"Assertion failed: {text}";
    }
}
