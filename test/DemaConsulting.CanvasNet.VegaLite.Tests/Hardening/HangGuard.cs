using System.Diagnostics;
using System.Globalization;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Hardening;

/// <summary>
///     The wall-clock hang guard of the hardening tests. The hardening tests assert outcomes (a located limit error, a cancellation, the same result twice), never how long
///     something took, because the machines that run them can be very slow or heavily loaded. The only wall-clock check that remains is a generous guard that turns a
///     stage that never stops into a failure with a clear message instead of a timed-out test run.
/// </summary>
/// <remarks>
///     The default is 120 seconds, which is orders of magnitude above what any case takes under the tight limits of the tests (well under a second on an ordinary machine) and so can only
///     be reached by work that nothing bounds. The environment variable <c>CANVASNET_VL_HANG_GUARD_SECONDS</c> sets another number of seconds for an even slower machine, or a smaller one to hunt for slow stages.
/// </remarks>
internal static class HangGuard
{
    /// <summary>The environment variable that sets the guard in seconds.</summary>
    public const string Variable = "CANVASNET_VL_HANG_GUARD_SECONDS";

    /// <summary>The default guard.</summary>
    private static readonly TimeSpan DefaultLimit = TimeSpan.FromSeconds(120);

    /// <summary>
    ///     Gets the guard: the number of seconds in the environment variable when it is a positive number, else 120 seconds.
    /// </summary>
    public static TimeSpan Limit =>
        double.TryParse(Environment.GetEnvironmentVariable(Variable), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0 && seconds < 86_400
            ? TimeSpan.FromSeconds(seconds)
            : DefaultLimit;

    /// <summary>
    ///     Gets the text of the failure of a stage that exceeded the guard.
    /// </summary>
    /// <param name="what">What was running.</param>
    /// <param name="elapsed">How long it ran.</param>
    /// <returns>The message.</returns>
    public static string Message(string what, TimeSpan elapsed) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Hang guard: {what} ran for {elapsed.TotalSeconds:F1} s, more than the hang guard of {Limit.TotalSeconds:F0} s. This is not a performance assertion: a stage that is bounded and polls the cancellation token stops far sooner, whatever the speed of the machine. Set {Variable} to raise the guard on a very slow machine.");

    /// <summary>
    ///     Asserts that a stage did not exceed the guard.
    /// </summary>
    /// <param name="stopwatch">The stopwatch that was started with the stage.</param>
    /// <param name="what">What was running.</param>
    public static void AssertNotHung(Stopwatch stopwatch, string what) =>
        Assert.True(stopwatch.Elapsed < Limit, Message(what, stopwatch.Elapsed));
}
