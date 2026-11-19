using System.Globalization;
using DemaConsulting.CanvasNet.Layout.Dagre;

namespace DemaConsulting.CanvasNet.Layout.Tests.Dagre;

/// <summary>
///     The work meter the layout tests run the layout with: a consumer of the abstract <see cref="LayoutWorkMeter"/>
///     whose failures are <see cref="LayoutTestException"/>, so the tests need no diagram package.
/// </summary>
/// <remarks>
///     It stands in for the meters of the diagram packages (such as Mermaid's), which create their own located
///     exceptions: the tests check that the layout throws exactly what the consumer's meter creates, and lets the
///     consumer's own failures pass unchanged.
/// </remarks>
internal sealed class TestLayoutWorkMeter : LayoutWorkMeter
{
    /// <summary>
    ///     Initializes a meter.
    /// </summary>
    /// <param name="maxWork">The most work allowed; at least one.</param>
    /// <param name="cancellationToken">The cancellation token of the call.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxWork"/> is less than one.</exception>
    public TestLayoutWorkMeter(long maxWork, CancellationToken cancellationToken)
        : base(maxWork, cancellationToken)
    {
    }

    /// <summary>
    ///     Creates the test failure of a layout that dagre itself would fail.
    /// </summary>
    /// <param name="description">The description of the failure.</param>
    /// <returns>A render failure (not a limit failure) whose message contains the description.</returns>
    public override LayoutTestException Failure(string description) =>
        new(isLimit: false, "the layout failed: " + description, MaxWork);

    /// <summary>
    ///     Creates the test failure of a layout past the work budget.
    /// </summary>
    /// <returns>A limit failure that records the budget.</returns>
    public override LayoutTestException LimitExceeded() =>
        new(isLimit: true, string.Create(CultureInfo.InvariantCulture, $"the layout needs more than {MaxWork} steps"), MaxWork);

    /// <summary>
    ///     Tells whether an exception is a test failure.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns>True when the exception is a <see cref="LayoutTestException"/>.</returns>
    public override bool IsFailure(Exception exception) => exception is LayoutTestException;
}
