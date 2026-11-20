// Portions derived from lodash (MIT); see third-party/README.md
using System.Globalization;

namespace DemaConsulting.CanvasNet.Layout.Dagre;

/// <summary>
///     The work budget of the layout of one diagram: every iterative layout stage charges its steps here, and the meter
///     also checks for cancellation.
/// </summary>
/// <remarks>
///     <para>
///         The stages charge work in proportion to what they do: a network-simplex pivot costs the nodes and edges it
///         recomputes, an ordering sweep the nodes and edges of its layer graphs, a crossing count its edges, and a
///         Brandes-Koepf pass its nodes and edges. When the total would pass <see cref="MaxWork"/>, the call fails with
///         the exception <see cref="LimitExceeded"/> creates, so a pathological graph stops early instead of running for
///         minutes. The charges are deterministic, so the same graph always uses the same work.
///     </para>
///     <para>
///         The meter is abstract because the failures belong to the consumer: each diagram package derives its own
///         meter that creates its own located exceptions (with its own diagnostic codes, the name of its own limit and
///         the location of the diagram), so the layout reports errors in the words of the package that called it and
///         needs no reference to any diagram package. <see cref="IsFailure"/> lets <see cref="DagreLayout"/> tell the
///         consumer's failures, which pass through unchanged, from the invalid operations of a layout that dagre itself
///         would fail.
///     </para>
///     <para>
///         The meter also holds the counter of lodash's <c>_.uniqueId</c>, which dagre uses to name its dummy nodes.
///         lodash shares one counter among all the layouts of a page; the port shares one counter among all the layouts
///         of one diagram, so a diagram always gets the same names whatever was drawn before. One meter serves one
///         diagram on one thread.
///     </para>
/// </remarks>
internal abstract class LayoutWorkMeter
{
    /// <summary>The cancellation token of the call.</summary>
    private readonly CancellationToken _cancellationToken;

    /// <summary>The last number given out by <see cref="UniqueId"/>.</summary>
    private long _idCounter;

    /// <summary>
    ///     Initializes a meter.
    /// </summary>
    /// <param name="maxWork">The most work allowed; at least one.</param>
    /// <param name="cancellationToken">The cancellation token of the call.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxWork"/> is less than one.</exception>
    protected LayoutWorkMeter(long maxWork, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxWork, 1);
        MaxWork = maxWork;
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
    /// <exception cref="Exception">
    ///     Thrown as the exception <see cref="LimitExceeded"/> creates when the total would pass <see cref="MaxWork"/>;
    ///     <see cref="Used"/> is then <see cref="MaxWork"/>.
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
    /// <remarks>
    ///     The consumer creates its own located render failure; <see cref="IsFailure"/> must recognize it.
    /// </remarks>
    /// <param name="description">The description of the failure.</param>
    /// <returns>The exception to throw; never null.</returns>
    public abstract Exception Failure(string description);

    /// <summary>
    ///     Creates the failure of a layout that needs more work than allowed, or nests deeper than the stack allows.
    /// </summary>
    /// <remarks>
    ///     The consumer creates its own located limit failure, naming its own limit; <see cref="IsFailure"/> must
    ///     recognize it.
    /// </remarks>
    /// <returns>The exception to throw; never null.</returns>
    public abstract Exception LimitExceeded();

    /// <summary>
    ///     Tells whether an exception is one of the failures this meter's consumer creates (by <see cref="Failure"/>,
    ///     <see cref="LimitExceeded"/>, or the consumer's own code that the layout calls back), which
    ///     <see cref="DagreLayout"/> lets pass unchanged instead of wrapping it as a layout failure.
    /// </summary>
    /// <param name="exception">The exception; not null.</param>
    /// <returns>True when the exception is the consumer's own failure.</returns>
    public abstract bool IsFailure(Exception exception);
}
