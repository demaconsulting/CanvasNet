// Portions derived from Vega (BSD-3-Clause) and Vega-Lite (BSD-3-Clause); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Guides;

/// <summary>
///     The text that a label expression gives a guide label.
/// </summary>
/// <param name="Text">The text; the lines joined by a line feed when the expression gave an array.</param>
/// <param name="Lines">The lines of a multi-line label (the expression gave an array), or null for a single line.</param>
internal readonly record struct LabelText(string Text, IReadOnlyList<string>? Lines);

/// <summary>
///     Evaluates the <c>labelExpr</c> of axes, legends and facet headers with the expression engine of the call.
/// </summary>
/// <remarks>
///     <para>
///         <b>Where the rules come from.</b> Vega-Lite copies the expression to the <c>text</c> encoding of the labels of the Vega guide
///         (<c>axis/assemble.ts</c>, <c>legend/assemble.ts</c>; a header replaces <c>datum.label</c> and <c>datum.value</c> in the expression, so its datum has these two members only).
///         Vega's guide encoders call the expression with a datum that has <c>value</c> and <c>label</c> and, by guide, <c>index</c>, <c>tickIndex</c> (axis), <c>perc</c> and <c>perc2</c>
///         (gradient legends) - see <see cref="Datum"/>. The result goes through the text mark: <c>null</c> is the empty text, an array is a multi-line text whose elements are
///         trimmed (a null element is empty) and anything else is converted with JavaScript's string conversion and trimmed (<c>textLines</c>/<c>textValue</c> of <c>vega-scenegraph</c>).
///     </para>
///     <para>
///         <b>Limits.</b> The expression runs on the evaluator of the call, so it shares the step budget (<see cref="VegaLiteLimits.MaxEvaluationSteps"/>), the string limit
///         (<see cref="VegaLiteLimits.MaxStringLength"/>), the cancellation token and the parameter values with every other expression; a failure is a located
///         <see cref="VegaLiteRenderException"/>. Without the expressions of a call (a guide built alone) the builder uses an evaluator of its own with the same limits.
///         The text of an expression that the specification parser did not compile (the configuration the caller supplies) is compiled once per instance and cached.
///     </para>
///     <para>
///         The class belongs to one call and one thread and holds no state beyond the cache of compiled texts.
///     </para>
/// </remarks>
internal sealed class LabelExpressions
{
    /// <summary>The expressions of the call, or null.</summary>
    private readonly RowExpressions? _expressions;

    /// <summary>The evaluator the expressions run on.</summary>
    private readonly ExpressionEvaluator _evaluator;

    /// <summary>The limits.</summary>
    private readonly VegaLiteLimits _limits;

    /// <summary>The compiled texts of configured expressions by text.</summary>
    private readonly Dictionary<string, CompiledExpression> _cache = new(StringComparer.Ordinal);

    /// <summary>
    ///     Initializes the evaluation of label expressions.
    /// </summary>
    /// <param name="expressions">The compiled expressions and the evaluator of the call; null to use an evaluator of this instance.</param>
    /// <param name="limits">The limits to enforce; the defaults when null.</param>
    /// <param name="calendar">The calendar of the date functions for an evaluator of this instance; UTC when null.</param>
    /// <param name="cancellationToken">The cancellation token of an evaluator of this instance.</param>
    public LabelExpressions(RowExpressions? expressions, VegaLiteLimits? limits, TimeCalendar? calendar = null, CancellationToken cancellationToken = default)
    {
        _expressions = expressions;
        _limits = limits ?? VegaLiteLimits.Default;
        _evaluator = expressions?.Evaluator ?? new ExpressionEvaluator(_limits, cancellationToken, null, calendar);
    }

    /// <summary>
    ///     Builds the datum an expression reads: an object with the members that the guide gives.
    /// </summary>
    /// <param name="value">The value the label names (<c>datum.value</c>).</param>
    /// <param name="label">The text the label has without the expression (<c>datum.label</c>).</param>
    /// <param name="index">The <c>datum.index</c> of the entry, or null for a guide that has none (a header).</param>
    /// <param name="tickIndex">The <c>datum.tickIndex</c> (an axis), or null.</param>
    /// <param name="perc">The <c>datum.perc</c> (a gradient legend), or null.</param>
    /// <param name="perc2">The <c>datum.perc2</c> (a discrete gradient legend), or null.</param>
    /// <returns>The datum as an object value; the members that are null do not exist.</returns>
    public static DataValue Datum(DataValue value, string label, double? index = null, double? tickIndex = null, double? perc = null, double? perc2 = null)
    {
        var members = new List<KeyValuePair<string, DataValue>>(6)
        {
            new("value", value),
            new("label", DataValue.FromString(label)),
        };
        AddMember(members, "index", index);
        AddMember(members, "tickIndex", tickIndex);
        AddMember(members, "perc", perc);
        AddMember(members, "perc2", perc2);
        return DataValue.FromObject(DataObject.Create(members));
    }

    /// <summary>
    ///     Gets the compiled expression of a guide.
    /// </summary>
    /// <remarks>
    ///     The expression the parser compiled for the node of the guide is used when it exists; otherwise the text (of the node, or the configured one) is compiled and cached.
    /// </remarks>
    /// <param name="node">The <c>labelExpr</c> node of the guide, or null when the guide has none.</param>
    /// <param name="configuredText">The text from the configuration, used when <paramref name="node"/> is null.</param>
    /// <param name="location">The location that a failure to compile is reported at.</param>
    /// <returns>The expression, or null when the guide has no label expression (or the text is empty).</returns>
    /// <exception cref="VegaLiteSpecException">Thrown, at <paramref name="location"/>, when the text of a configured expression is not valid.</exception>
    public CompiledExpression? Resolve(SpecNode? node, string? configuredText, VegaLiteLocation location)
    {
        if (node is not null)
        {
            if (!node.IsString)
            {
                return null;
            }

            if (_expressions?.Compiled.Get(node) is { } compiled)
            {
                return compiled;
            }

            return Compile(node.StringValue, node.GetLocation(location.File));
        }

        return Compile(configuredText, location);
    }

    /// <summary>
    ///     Evaluates an expression for one label.
    /// </summary>
    /// <param name="expression">The compiled expression.</param>
    /// <param name="datum">The datum (see <see cref="Datum"/>).</param>
    /// <returns>The text of the label; the text of an array result is its lines joined by line feeds, and is bounded by <see cref="VegaLiteLimits.MaxStringLength"/> as a whole.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown, at the expression, when a limit is exceeded.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the call is canceled.</exception>
    public LabelText Evaluate(CompiledExpression expression, DataValue datum)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var result = _evaluator.Evaluate(expression, datum);
        var maxLength = _limits.MaxStringLength;
        var context = _evaluator.Context;
        try
        {
            if (result.IsNull)
            {
                return new LabelText(string.Empty, null);
            }

            if (result.IsArray)
            {
                var elements = result.Elements;
                var lines = new string[elements.Length];

                // The joined text (the lines and the line feeds between them) is bounded like a single text
                long total = Math.Max(0, lines.Length - 1);
                for (var i = 0; i < lines.Length; i++)
                {
                    lines[i] = elements[i].IsNull ? string.Empty : TrimJs(ExpressionEvaluator.ToJsString(elements[i], maxLength, context));
                    total += lines[i].Length;
                    context.CheckStringLength(total);
                }

                context.ChargeSize(total);
                return new LabelText(string.Join('\n', lines), lines);
            }

            return new LabelText(TrimJs(ExpressionEvaluator.ToJsString(result, maxLength, context)), null);
        }
        catch (ExpressionRuntimeException ex)
        {
            throw new VegaLiteRenderException(ex.Message, expression.Location);
        }
    }

    /// <summary>
    ///     Removes the white space of JavaScript's <c>trim</c> from both ends of a text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The trimmed text.</returns>
    private static string TrimJs(string text)
    {
        var start = 0;
        var end = text.Length;
        while (start < end && ExpressionEvaluator.IsJsWhitespace(text[start]))
        {
            start++;
        }

        while (end > start && ExpressionEvaluator.IsJsWhitespace(text[end - 1]))
        {
            end--;
        }

        return start == 0 && end == text.Length ? text : text[start..end];
    }

    /// <summary>
    ///     Compiles a text, keeping the result for the next use.
    /// </summary>
    /// <param name="text">The text, or null.</param>
    /// <param name="location">The location for a failure.</param>
    /// <returns>The expression, or null for a missing or empty text.</returns>
    private CompiledExpression? Compile(string? text, VegaLiteLocation location)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (!_cache.TryGetValue(text, out var compiled))
        {
            compiled = ExpressionParser.Parse(text, location, _limits);
            _cache[text] = compiled;
        }

        return compiled;
    }

    /// <summary>
    ///     Adds a member when it has a number.
    /// </summary>
    /// <param name="members">The members.</param>
    /// <param name="name">The name.</param>
    /// <param name="number">The number, or null for no member.</param>
    private static void AddMember(List<KeyValuePair<string, DataValue>> members, string name, double? number)
    {
        if (number is { } n)
        {
            members.Add(new(name, DataValue.FromNumber(n)));
        }
    }
}
