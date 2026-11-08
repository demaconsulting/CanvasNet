// Portions derived from lodash (MIT); see third-party/README.md
using System.Globalization;

namespace DemaConsulting.CanvasNet.Mermaid.Dagre;

/// <summary>
///     The work budget of the layout of one diagram: every iterative layout stage charges its steps here, and the meter
///     also checks for cancellation.
/// </summary>
/// <remarks>
///     <para>
///         The stages charge work in proportion to what they do: a network-simplex pivot costs the nodes and edges it
///         recomputes, an ordering sweep the nodes and edges of its layer graphs, a crossing count its edges, and a
///         Brandes-Koepf pass its nodes and edges. When the total would pass <see cref="MermaidLimits.MaxLayoutWork"/>,
///         the call fails with a <see cref="MermaidRenderException"/> whose code is
///         <see cref="MermaidDiagnosticCodes.LimitExceeded"/>, located at the construct the meter was made for, so a
///         pathological graph stops early instead of running for minutes. The charges are deterministic, so the same
///         graph always uses the same work.
///     </para>
///     <para>
///         The meter also holds the counter of lodash's <c>_.uniqueId</c>, which dagre uses to name its dummy nodes.
///         lodash shares one counter among all the layouts of a page; the port shares one counter among all the layouts
///         of one diagram, so a diagram always gets the same names whatever was drawn before. One meter serves one
///         diagram on one thread.
///     </para>
/// </remarks>
internal sealed class LayoutWorkMeter
{
    /// <summary>The location the limit failure is reported at.</summary>
    private readonly MermaidLocation _location;

    /// <summary>The cancellation token of the call.</summary>
    private readonly CancellationToken _cancellationToken;

    /// <summary>The last number given out by <see cref="UniqueId"/>.</summary>
    private long _idCounter;

    /// <summary>
    ///     Initializes a meter.
    /// </summary>
    /// <param name="maxWork">The most work allowed; at least one.</param>
    /// <param name="location">The location a limit failure is reported at (the diagram, or its first statement).</param>
    /// <param name="cancellationToken">The cancellation token of the call.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxWork"/> is less than one.</exception>
    public LayoutWorkMeter(long maxWork, MermaidLocation location, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxWork, 1);
        MaxWork = maxWork;
        _location = location;
        _cancellationToken = cancellationToken;
    }

    /// <summary>
    ///     Gets the most work allowed.
    /// </summary>
    public long MaxWork { get; }

    /// <summary>
    ///     Gets the work charged so far.
    /// </summary>
    public long Used { get; private set; }

    /// <summary>
    ///     Charges work, after checking for cancellation.
    /// </summary>
    /// <param name="units">The steps to charge; not negative.</param>
    /// <exception cref="OperationCanceledException">Thrown when the call is canceled.</exception>
    /// <exception cref="MermaidRenderException">
    ///     Thrown with <see cref="MermaidDiagnosticCodes.LimitExceeded"/> when the total would pass
    ///     <see cref="MaxWork"/>.
    /// </exception>
    public void Charge(long units)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        if (units > MaxWork - Used)
        {
            Used = MaxWork;
            throw LimitExceeded();
        }

        Used += units;
    }

    /// <summary>
    ///     Gives a new name with a prefix, as lodash's <c>_.uniqueId</c> does: the prefix followed by the next number of
    ///     one counter shared by every prefix.
    /// </summary>
    /// <param name="prefix">The prefix.</param>
    /// <returns>The name.</returns>
    public string UniqueId(string prefix) =>
        prefix + (++_idCounter).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    ///     Creates the failure of a layout that dagre itself would fail (for example an edge whose route ends at the
    ///     center of its node).
    /// </summary>
    /// <param name="description">The description of the failure.</param>
    /// <returns>The exception, with the code <see cref="MermaidDiagnosticCodes.RenderFailed"/>, located at the meter's location.</returns>
    public MermaidRenderException Failure(string description) =>
        new(MermaidDiagnosticCodes.RenderFailed, "the layout failed: " + description, _location);

    /// <summary>
    ///     Creates the failure of a layout that needs more work than allowed, or nests deeper than the stack allows.
    /// </summary>
    /// <returns>The exception, located at the meter's location.</returns>
    public MermaidRenderException LimitExceeded() =>
        new(
            MermaidDiagnosticCodes.LimitExceeded,
            string.Create(
                CultureInfo.InvariantCulture,
                $"laying the diagram out needs more than the limit of {MaxWork} steps (MermaidLimits.MaxLayoutWork)"),
            _location,
            "simplify the diagram (fewer edges or subgraphs), or raise MermaidLimits.MaxLayoutWork");
}
