namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     The immutable payload of a vector <see cref="ScadValue"/>.
/// </summary>
/// <remarks>
///     The element array is private and never modified after construction, so a vector can be shared between
///     values and threads. The nesting <see cref="Depth"/> is computed once from the (already known) depths of
///     the elements, so checking the nesting limit never needs recursion.
/// </remarks>
internal sealed class ScadVector
{
    /// <summary>The elements; never modified after construction.</summary>
    private readonly ScadValue[] _items;

    /// <summary>
    ///     Initializes a vector that takes ownership of an element array.
    /// </summary>
    /// <param name="items">The elements; the caller must not modify the array afterwards.</param>
    internal ScadVector(ScadValue[] items)
    {
        _items = items;

        // A vector is one level deeper than its deepest element (an empty vector has depth one)
        var deepest = 0;
        foreach (var item in items)
        {
            deepest = Math.Max(deepest, item.Depth);
        }

        Depth = deepest + 1;
    }

    /// <summary>
    ///     Gets the number of elements.
    /// </summary>
    public int Count => _items.Length;

    /// <summary>
    ///     Gets the nesting depth: one for a vector with no vector elements.
    /// </summary>
    public int Depth { get; }

    /// <summary>
    ///     Gets an element.
    /// </summary>
    /// <param name="index">The zero-based index; must be within range.</param>
    /// <returns>The element.</returns>
    public ScadValue this[int index] => _items[index];

    /// <summary>
    ///     Gets a read-only view of the elements.
    /// </summary>
    /// <returns>A span over the elements; it must not be used to modify them.</returns>
    public ReadOnlySpan<ScadValue> AsSpan() => _items;
}
