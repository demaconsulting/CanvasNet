using System.Globalization;
using System.Runtime.ExceptionServices;
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Expressions;

/// <summary>
///     Helpers shared by the expression tests: compiling and evaluating expression text, building datum values and
///     running code under an explicit culture. The class holds no tests.
/// </summary>
internal static class ExpressionTestSupport
{
    /// <summary>The location that every test expression is attributed to.</summary>
    public static readonly VegaLiteLocation Location = new("spec.vl.json", "/transform/0/calculate", 12, 17);

    /// <summary>
    ///     Compiles an expression at the standard test location.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <param name="limits">The limits; the defaults when null.</param>
    /// <returns>The compiled expression.</returns>
    public static CompiledExpression Compile(string text, VegaLiteLimits? limits = null) =>
        ExpressionParser.Parse(text, Location, limits);

    /// <summary>
    ///     Compiles and evaluates an expression with a fresh evaluator and no datum.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <param name="limits">The limits; the defaults when null.</param>
    /// <param name="parameters">The parameter values.</param>
    /// <returns>The value.</returns>
    public static DataValue Eval(string text, VegaLiteLimits? limits = null, IReadOnlyDictionary<string, DataValue>? parameters = null)
    {
        var evaluator = new ExpressionEvaluator(limits, CancellationToken.None, parameters);
        return evaluator.Evaluate(Compile(text, limits));
    }

    /// <summary>
    ///     Compiles and evaluates an expression against a datum value.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <param name="datum">The datum.</param>
    /// <returns>The value.</returns>
    public static DataValue EvalWith(string text, DataValue datum)
    {
        var evaluator = new ExpressionEvaluator(null, CancellationToken.None);
        return evaluator.Evaluate(Compile(text), datum);
    }

    /// <summary>
    ///     Evaluates an expression and returns its JavaScript text form.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <returns>The text of the value (<c>null</c> for null).</returns>
    public static string EvalText(string text) => Eval(text).ToText();

    /// <summary>
    ///     Evaluates an expression that must give a number.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <returns>The number.</returns>
    public static double EvalNumber(string text)
    {
        var value = Eval(text);
        Assert.True(value.IsNumber, $"'{text}' gave {value.Kind}, not a number");
        return value.Number;
    }

    /// <summary>
    ///     Builds an object value from members.
    /// </summary>
    /// <param name="members">The members in insertion order.</param>
    /// <returns>The object value.</returns>
    public static DataValue Obj(params (string Key, DataValue Value)[] members) =>
        DataValue.FromObject(DataObject.Create(members.Select(m => new KeyValuePair<string, DataValue>(m.Key, m.Value))));

    /// <summary>
    ///     Creates limits with a small step budget.
    /// </summary>
    /// <param name="steps">The step budget.</param>
    /// <returns>The limits.</returns>
    public static VegaLiteLimits Steps(long steps) => new() { MaxEvaluationSteps = steps };

    /// <summary>
    ///     Runs an action on a dedicated thread whose culture is a named culture. The cultures of the calling (test)
    ///     thread, and the process-wide default cultures, are never changed.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="cultureName">The culture name; the empty text selects the invariant culture.</param>
    /// <param name="action">The action.</param>
    /// <returns>The result of the action; an exception of the action is rethrown with its original stack.</returns>
    public static T UnderCulture<T>(string cultureName, Func<T> action)
    {
        // A host without culture data (invariant globalization) cannot create the named culture; the invariant one is used then
        CultureInfo culture;
        try
        {
            culture = CultureInfo.GetCultureInfo(cultureName);
        }
        catch (CultureNotFoundException)
        {
            culture = CultureInfo.InvariantCulture;
        }

        T result = default!;
        ExceptionDispatchInfo? failure = null;
        var worker = new Thread(() =>
        {
            try
            {
                // The cultures belong to this worker thread only
                Thread.CurrentThread.CurrentCulture = culture;
                Thread.CurrentThread.CurrentUICulture = culture;
                result = action();
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
        });
        worker.Start();
        worker.Join();
        failure?.Throw();
        return result;
    }

    /// <summary>
    ///     A datum whose fields come from a dictionary and that runs a callback on every field read.
    /// </summary>
    /// <param name="onRead">The callback.</param>
    internal sealed class CallbackDatum(Action onRead) : IExpressionDatum
    {
        /// <summary>Gets the number of field reads.</summary>
        public int Reads { get; private set; }

        /// <inheritdoc/>
        public DataValue GetField(string name)
        {
            Reads++;
            onRead();
            return DataValue.FromNumber(1);
        }

        /// <inheritdoc/>
        public DataValue ToValue() => default;
    }
}
