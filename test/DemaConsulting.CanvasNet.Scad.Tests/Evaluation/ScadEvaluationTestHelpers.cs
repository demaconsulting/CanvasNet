using System.Globalization;
using System.Runtime.ExceptionServices;
using DemaConsulting.CanvasNet.Scad.Evaluation;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Shared helpers for the evaluation tests: value construction, small-stack threads and culture switching.
/// </summary>
internal static class ScadEvaluationTestHelpers
{
    /// <summary>The stack size, in bytes, of the deliberately small caller threads.</summary>
    public const int SmallCallerStackSize = 256 * 1024;

    /// <summary>
    ///     Creates a number value.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The value.</returns>
    public static ScadValue N(double value) => ScadValue.FromNumber(value);

    /// <summary>
    ///     Creates a string value.
    /// </summary>
    /// <param name="value">The string.</param>
    /// <returns>The value.</returns>
    public static ScadValue S(string value) => ScadValue.FromString(value);

    /// <summary>
    ///     Creates a boolean value.
    /// </summary>
    /// <param name="value">The boolean.</param>
    /// <returns>The value.</returns>
    public static ScadValue B(bool value) => ScadValue.FromBoolean(value);

    /// <summary>
    ///     Creates a vector value from values.
    /// </summary>
    /// <param name="items">The elements.</param>
    /// <returns>The value.</returns>
    public static ScadValue Vec(params ScadValue[] items) => ScadValue.FromVector((ScadValue[])items.Clone());

    /// <summary>
    ///     Creates a vector of numbers.
    /// </summary>
    /// <param name="numbers">The numbers.</param>
    /// <returns>The value.</returns>
    public static ScadValue Nums(params double[] numbers) => ScadValue.FromVector(numbers.Select(N).ToArray());

    /// <summary>
    ///     Creates a vector nested to an exact depth (one for <c>[0]</c>, two for <c>[[0]]</c>, ...).
    /// </summary>
    /// <param name="depth">The nesting depth, at least one.</param>
    /// <returns>The value.</returns>
    public static ScadValue Nested(int depth)
    {
        var value = Nums(0);
        for (var i = 1; i < depth; i++)
        {
            value = Vec(value);
        }

        return value;
    }

    /// <summary>
    ///     Runs a function on a new thread with a small stack and returns its result, propagating any exception.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="function">The function to run.</param>
    /// <param name="stackSize">The stack size of the thread in bytes.</param>
    /// <returns>The result.</returns>
    public static T OnSmallStack<T>(Func<T> function, int stackSize = SmallCallerStackSize)
    {
        T? result = default;
        Exception? error = null;
        var thread = new Thread(
            () =>
            {
                try
                {
                    result = function();
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            },
            stackSize);
        thread.Start();
        thread.Join();
        if (error is not null)
        {
            ExceptionDispatchInfo.Capture(error).Throw();
        }

        return result!;
    }

    /// <summary>
    ///     Runs a function with a specific current culture on a dedicated thread, so no other test is affected.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="cultureName">The culture name, for example <c>de-DE</c>.</param>
    /// <param name="function">The function to run.</param>
    /// <returns>The result.</returns>
    public static T WithCulture<T>(string cultureName, Func<T> function) =>
        WithCulture(CultureInfo.GetCultureInfo(cultureName), function);

    /// <summary>
    ///     Runs a function with a specific current culture object on a dedicated thread, so no other test is affected.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="culture">The culture, which may be a synthetic one.</param>
    /// <param name="function">The function to run.</param>
    /// <returns>The result.</returns>
    public static T WithCulture<T>(CultureInfo culture, Func<T> function)
    {
        T? result = default;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
                result = function();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.Start();
        thread.Join();
        if (error is not null)
        {
            ExceptionDispatchInfo.Capture(error).Throw();
        }

        return result!;
    }
}

/// <summary>
///     A function value payload for tests; equality is by reference.
/// </summary>
/// <param name="ParameterNames">The parameter names reported.</param>
internal sealed class TestFunctionValue(params string[] ParameterNames) : IScadFunctionValue
{
    /// <inheritdoc />
    IReadOnlyList<string> IScadFunctionValue.ParameterNames => ParameterNames;
}
