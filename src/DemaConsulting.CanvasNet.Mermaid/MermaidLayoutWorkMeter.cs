using System.Globalization;
using DemaConsulting.CanvasNet.Layout.Dagre;

namespace DemaConsulting.CanvasNet.Mermaid;

/// <summary>
///     The work meter of the layout of one Mermaid diagram: the shared <see cref="LayoutWorkMeter"/> of the layout package,
///     whose failures are Mermaid's located <see cref="MermaidRenderException"/>.
/// </summary>
/// <remarks>
///     <para>
///         The layout package knows nothing of Mermaid, so the failures its meter reports are created here: a layout past
///         <see cref="MermaidLimits.MaxLayoutWork"/> (or one that nests deeper than the stack allows) fails with
///         <see cref="MermaidDiagnosticCodes.LimitExceeded"/>, naming the limit and suggesting a fix, and a layout that dagre
///         itself would fail fails with <see cref="MermaidDiagnosticCodes.RenderFailed"/>; both are located at the
///         construct the meter was made for (the diagram, or its first statement). Every Mermaid layout - dagre, cose
///         (mindmap), sequence, gantt, pie, packet, git graph and the text measurement - charges one of these meters, so
///         the messages, codes and hints are the same whichever layout reaches the limit.
///     </para>
///     <para>
///         <see cref="IsFailure"/> recognizes every <see cref="MermaidRenderException"/>, so a Mermaid failure raised
///         inside a dagre layout (by this meter or by Mermaid code the layout calls) passes through it unchanged. One
///         meter serves one diagram on one thread.
///     </para>
/// </remarks>
internal sealed class MermaidLayoutWorkMeter : LayoutWorkMeter
{
    /// <summary>The location the failures are reported at.</summary>
    private readonly MermaidLocation _location;

    /// <summary>
    ///     Initializes a meter.
    /// </summary>
    /// <param name="maxWork">The most work allowed; at least one (normally <see cref="MermaidLimits.MaxLayoutWork"/>).</param>
    /// <param name="location">The location a failure is reported at (the diagram, or its first statement).</param>
    /// <param name="cancellationToken">The cancellation token of the call.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxWork"/> is less than one.</exception>
    public MermaidLayoutWorkMeter(long maxWork, MermaidLocation location, CancellationToken cancellationToken)
        : base(maxWork, cancellationToken)
    {
        _location = location;
    }

    /// <summary>
    ///     Creates the failure of a layout that dagre itself would fail (for example an edge whose route ends at the
    ///     center of its node).
    /// </summary>
    /// <param name="description">The description of the failure.</param>
    /// <returns>The exception, with the code <see cref="MermaidDiagnosticCodes.RenderFailed"/>, located at the meter's location.</returns>
    public override MermaidRenderException Failure(string description) =>
        new(MermaidDiagnosticCodes.RenderFailed, "the layout failed: " + description, _location);

    /// <summary>
    ///     Creates the failure of a layout that needs more work than allowed, or nests deeper than the stack allows.
    /// </summary>
    /// <returns>
    ///     The exception, with the code <see cref="MermaidDiagnosticCodes.LimitExceeded"/>, a message naming
    ///     <see cref="MermaidLimits.MaxLayoutWork"/> and a hint, located at the meter's location.
    /// </returns>
    public override MermaidRenderException LimitExceeded() =>
        new(
            MermaidDiagnosticCodes.LimitExceeded,
            string.Create(
                CultureInfo.InvariantCulture,
                $"laying the diagram out needs more than the limit of {MaxWork} steps (MermaidLimits.MaxLayoutWork)"),
            _location,
            "simplify the diagram, or raise MermaidLimits.MaxLayoutWork");

    /// <summary>
    ///     Tells whether an exception is a Mermaid failure, which the dagre layout lets pass unchanged.
    /// </summary>
    /// <param name="exception">The exception; not null.</param>
    /// <returns>True when the exception is a <see cref="MermaidRenderException"/>.</returns>
    public override bool IsFailure(Exception exception) => exception is MermaidRenderException;
}
