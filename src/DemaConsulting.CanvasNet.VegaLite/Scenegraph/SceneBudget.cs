namespace DemaConsulting.CanvasNet.VegaLite.Scenegraph;

/// <summary>
///     Counts the scene items that the builders of one chart create and fails the render when the count passes the
///     limit (<see cref="VegaLiteLimits.MaxSceneItems"/>).
/// </summary>
/// <remarks>
///     The budget is what keeps a hostile or simply huge data set from allocating an unbounded scene: marks are counted
///     in batches while they are built, so the failure happens before the memory is spent. An instance belongs to one
///     render and is not thread-safe.
/// </remarks>
internal sealed class SceneBudget
{
    /// <summary>The largest number of items that may be created.</summary>
    private readonly long _max;

    /// <summary>
    ///     Initializes a budget.
    /// </summary>
    /// <param name="max">The largest number of scene items the chart may have; 1 or more.</param>
    public SceneBudget(long max)
    {
        _max = Math.Max(1, max);
    }

    /// <summary>Gets the number of items counted so far.</summary>
    public long Count { get; private set; }

    /// <summary>
    ///     Counts items that are about to be created.
    /// </summary>
    /// <param name="items">The number of items; 0 or more.</param>
    /// <param name="location">The location of the construct that creates them, for the failure.</param>
    /// <exception cref="VegaLiteRenderException">Thrown, with <paramref name="location"/>, when the total passes the limit.</exception>
    public void Add(long items, VegaLiteLocation location)
    {
        Count += Math.Max(0, items);
        if (Count > _max)
        {
            throw new VegaLiteRenderException(
                $"The chart needs more than {_max} scene items (VegaLiteLimits.MaxSceneItems); reduce the data or raise the limit.",
                location);
        }
    }
}
