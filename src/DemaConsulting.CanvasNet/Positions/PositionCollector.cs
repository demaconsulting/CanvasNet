using System.Globalization;

namespace DemaConsulting.CanvasNet.Positions;

/// <summary>
///     Keeps the positions of one render that a selection wants, up to a cap, and tells which selectors matched
///     nothing.
/// </summary>
/// <remarks>
///     <para>
///         A renderer offers every candidate position once, in its deterministic order (structural features such as
///         titles and nodes first). <see cref="Offer(ImagePosition)"/> evaluates every selector against the candidate,
///         so the union of the selectors is taken and a position matched by several selectors is kept once. The first
///         <see cref="MaxCount"/> matches are kept in delivery order; a later match is counted but not kept, and sets
///         <see cref="Truncated"/>.
///     </para>
///     <para>
///         A selector is <i>unmatched</i> only when no candidate matched it; a selector whose matches were all cut by
///         the cap still matched.
///     </para>
///     <para>
///         The collector is mutable and belongs to one render; it is not thread-safe.
///     </para>
/// </remarks>
public sealed class PositionCollector
{
    /// <summary>
    ///     The default cap on the kept positions: 1000.
    /// </summary>
    public const int MaxPositions = 1000;

    /// <summary>
    ///     The kept positions.
    /// </summary>
    private readonly List<ImagePosition> _positions = [];

    /// <summary>
    ///     Whether each selector matched a candidate, by its index in the selection.
    /// </summary>
    private readonly bool[] _matched;

    /// <summary>
    ///     Initializes a collector.
    /// </summary>
    /// <param name="selection">The selection.</param>
    /// <param name="maxCount">The cap on the kept positions; at least 1. Defaults to <see cref="MaxPositions"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="selection"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxCount"/> is below 1.</exception>
    public PositionCollector(PositionSelection selection, int maxCount = MaxPositions)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (maxCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCount), maxCount, "The cap must be at least 1.");
        }

        Selection = selection;
        MaxCount = maxCount;
        _matched = new bool[selection.Selectors.Count];
    }

    /// <summary>
    ///     Gets the selection.
    /// </summary>
    public PositionSelection Selection { get; }

    /// <summary>
    ///     Gets the cap on the kept positions.
    /// </summary>
    public int MaxCount { get; }

    /// <summary>
    ///     Gets the kept positions, in delivery order.
    /// </summary>
    public IReadOnlyList<ImagePosition> Positions => _positions;

    /// <summary>
    ///     Gets a value indicating whether a matching position was not kept because of the cap.
    /// </summary>
    public bool Truncated { get; private set; }

    /// <summary>
    ///     Gets the selectors that matched no candidate so far, in selection order.
    /// </summary>
    /// <returns>A new list of the unmatched selectors; empty when every selector matched.</returns>
    public IReadOnlyList<PositionSelector> GetUnmatchedSelectors()
    {
        return Selection.Selectors.Where((_, index) => !_matched[index]).ToArray();
    }

    /// <summary>
    ///     Tells whether a kind can be wanted, so that a renderer can skip the kinds nobody asked for.
    /// </summary>
    /// <param name="kind">The lowercase kind.</param>
    /// <returns><see langword="true"/> when a selector is <c>all</c> or names the kind.</returns>
    public bool IncludesKind(string kind)
    {
        return Selection.IncludesKind(kind);
    }

    /// <summary>
    ///     Offers a candidate position.
    /// </summary>
    /// <param name="position">The candidate; offered once.</param>
    /// <returns><see langword="true"/> when the position was kept.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="position"/> is null.</exception>
    public bool Offer(ImagePosition position)
    {
        ArgumentNullException.ThrowIfNull(position);

        // Evaluate every selector, so that each one knows it matched even after the cap
        var wanted = false;
        for (var index = 0; index < _matched.Length; index++)
        {
            if (Selection.Selectors[index].Matches(position))
            {
                _matched[index] = true;
                wanted = true;
            }
        }

        if (!wanted)
        {
            return false;
        }

        // Keep the match while there is room; otherwise record that a match was cut
        if (_positions.Count >= MaxCount)
        {
            Truncated = true;
            return false;
        }

        _positions.Add(position);
        return true;
    }

    /// <summary>
    ///     Describes the collector for a debugger.
    /// </summary>
    /// <returns>The count of kept positions and whether the list was truncated.</returns>
    public override string ToString()
    {
        return $"{_positions.Count.ToString(CultureInfo.InvariantCulture)} position(s){(Truncated ? ", truncated" : string.Empty)}";
    }
}
