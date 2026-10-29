namespace DemaConsulting.CanvasNet.VegaLite.Scenegraph;

/// <summary>
///     Counts the scene items that the builders of one chart create and fails the render when the count passes the
///     limit (<see cref="VegaLiteLimits.MaxSceneItems"/>).
/// </summary>
/// <remarks>
///     The budget is what keeps a hostile or simply huge data set from allocating an unbounded scene: every item is
///     counted before it is created (a row that draws nothing costs nothing), so the failure happens before the memory is
///     spent and a limit of N allows exactly N items and fails on the (N+1)th. Every <see cref="SceneItem"/> of the scene
///     counts, groups included (the root and view groups, the title and its text, each axis group with its grid group,
///     ticks, labels, domain and title, the marks group and each mark); a clip is a property of its group, not an item.
///     The count saturates at <see cref="long.MaxValue"/>. An instance belongs to one render and is not
///     thread-safe.
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
    ///     Creates a copy of the budget at its current count, for work that may be repeated (the axes are built again
    ///     until the data rectangle is stable); only the copy of the pass that is kept is adopted.
    /// </summary>
    /// <returns>A new budget with the same limit and count.</returns>
    public SceneBudget Fork() => new(_max) { Count = Count };

    /// <summary>
    ///     Takes over the count of a fork: the items of the pass that was kept.
    /// </summary>
    /// <param name="fork">A budget made by <see cref="Fork"/> of this budget.</param>
    public void Adopt(SceneBudget fork)
    {
        ArgumentNullException.ThrowIfNull(fork);
        Count = fork.Count;
    }

    /// <summary>
    ///     Counts items that are about to be created; the count saturates instead of overflowing.
    /// </summary>
    /// <param name="items">The number of items; 0 or more.</param>
    /// <param name="location">The location of the construct that creates them, for the failure.</param>
    /// <exception cref="VegaLiteRenderException">Thrown, with <paramref name="location"/>, when the total passes the limit.</exception>
    public void Add(long items, VegaLiteLocation location)
    {
        var add = Math.Max(0, items);
        Count = add > long.MaxValue - Count ? long.MaxValue : Count + add;
        if (Count > _max)
        {
            throw new VegaLiteRenderException(
                $"The chart needs more than {_max} scene items (VegaLiteLimits.MaxSceneItems); reduce the data or raise the limit.",
                location);
        }
    }
}
