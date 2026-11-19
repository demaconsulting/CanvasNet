using System.Globalization;
using DemaConsulting.CanvasNet.Layout.Dagre;

namespace DemaConsulting.CanvasNet.PlantUml.Svek;

/// <summary>
///     The work meter of the layout of one PlantUML diagram: the shared <see cref="LayoutWorkMeter"/> of the layout
///     package, whose failures are PlantUML's located <see cref="PlantUmlRenderException"/>.
/// </summary>
/// <remarks>
///     <para>
///         The layout package knows nothing of PlantUML, so the failures its meter reports are created here: a layout
///         past <see cref="PlantUmlLimits.MaxLayoutWork"/> (or one that nests deeper than the stack allows) fails with
///         <see cref="PlantUmlDiagnosticCodes.LimitExceeded"/>, naming the limit and suggesting a fix in the words the
///         sequence and activity layouts use, and a layout that dagre itself would fail fails with
///         <see cref="PlantUmlDiagnosticCodes.RenderFailed"/>; both are located at the diagram's first statement. One
///         meter serves one <see cref="SvekLayout"/> call: the dagre runs of every cluster laid out on its own and the
///         loops of the engine's own post-processing charge it, so the whole layout of a diagram is bounded by one
///         budget.
///     </para>
///     <para>
///         <see cref="IsFailure"/> recognizes every <see cref="PlantUmlRenderException"/>, so a PlantUML failure raised
///         inside a dagre layout passes through it unchanged. One meter serves one diagram on one thread.
///     </para>
/// </remarks>
internal sealed class PlantUmlLayoutWorkMeter : LayoutWorkMeter
{
    /// <summary>The location the failures are reported at.</summary>
    private readonly PlantUmlLocation _location;

    /// <summary>
    ///     Initializes a meter.
    /// </summary>
    /// <param name="maxWork">The most work allowed; at least one (normally <see cref="PlantUmlLimits.MaxLayoutWork"/>).</param>
    /// <param name="location">The location a failure is reported at (the diagram's first statement).</param>
    /// <param name="cancellationToken">The cancellation token of the call.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxWork"/> is less than one.</exception>
    public PlantUmlLayoutWorkMeter(long maxWork, PlantUmlLocation location, CancellationToken cancellationToken)
        : base(maxWork, cancellationToken)
    {
        _location = location;
    }

    /// <summary>
    ///     Creates the failure of a layout that dagre itself would fail (for example an edge whose route ends at the
    ///     center of its node).
    /// </summary>
    /// <param name="description">The description of the failure.</param>
    /// <returns>The exception, with the code <see cref="PlantUmlDiagnosticCodes.RenderFailed"/>, located at the meter's location.</returns>
    public override PlantUmlRenderException Failure(string description) =>
        new(PlantUmlDiagnosticCodes.RenderFailed, "the layout failed: " + description, _location);

    /// <summary>
    ///     Creates the failure of a layout that needs more work than allowed, or nests deeper than the stack allows.
    /// </summary>
    /// <returns>
    ///     The exception, with the code <see cref="PlantUmlDiagnosticCodes.LimitExceeded"/>, a message naming
    ///     <see cref="PlantUmlLimits.MaxLayoutWork"/> and a hint, located at the meter's location.
    /// </returns>
    public override PlantUmlRenderException LimitExceeded() =>
        new(
            PlantUmlDiagnosticCodes.LimitExceeded,
            string.Create(CultureInfo.InvariantCulture, $"the layout needs more than {MaxWork} units of work (PlantUmlLimits.MaxLayoutWork)"),
            _location,
            "simplify the diagram, or raise PlantUmlLimits.MaxLayoutWork when the source is trusted");

    /// <summary>
    ///     Tells whether an exception is a PlantUML failure, which the dagre layout lets pass unchanged.
    /// </summary>
    /// <param name="exception">The exception; not null.</param>
    /// <returns>True when the exception is a <see cref="PlantUmlRenderException"/>.</returns>
    public override bool IsFailure(Exception exception) => exception is PlantUmlRenderException;
}
