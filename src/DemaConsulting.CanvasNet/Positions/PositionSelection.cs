using System.Globalization;

namespace DemaConsulting.CanvasNet.Positions;

/// <summary>
///     The union of several position selectors: a position is wanted when any selector matches it.
/// </summary>
/// <remarks>
///     <para>
///         Each selector text is given once (one <c>--positions</c> argument of the tool); identical texts are kept once,
///         in their first order. The order of the selectors never changes the order of the positions: a
///         <see cref="PositionCollector"/> keeps them in the order the renderer delivers them.
///     </para>
///     <para>
///         At most <see cref="MaxSelectors"/> selectors are accepted. The class is immutable and thread-safe.
///     </para>
/// </remarks>
public sealed class PositionSelection
{
    /// <summary>
    ///     The most selectors one selection may have: 32.
    /// </summary>
    public const int MaxSelectors = 32;

    /// <summary>
    ///     Initializes a selection.
    /// </summary>
    /// <param name="selectors">The parsed, distinct selectors.</param>
    private PositionSelection(PositionSelector[] selectors)
    {
        // A read-only wrapper, so a caller cannot cast the list back to the array and change the selection
        Selectors = Array.AsReadOnly(selectors);
    }

    /// <summary>
    ///     Gets a selection that wants every position.
    /// </summary>
    public static PositionSelection All { get; } = new([PositionSelector.Parse("all")]);

    /// <summary>
    ///     Gets the selectors, in their first order with duplicates removed.
    /// </summary>
    public IReadOnlyList<PositionSelector> Selectors { get; }

    /// <summary>
    ///     Parses the selector texts of a selection.
    /// </summary>
    /// <param name="texts">The selector texts; at least one.</param>
    /// <returns>The selection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="texts"/> or one of the texts is null.</exception>
    /// <exception cref="FormatException">
    ///     Thrown when there is no selector, more than <see cref="MaxSelectors"/>, or one that is not valid (see
    ///     <see cref="PositionSelector.Parse(string)"/>); the message names the selector and the problem.
    /// </exception>
    public static PositionSelection Parse(IEnumerable<string> texts)
    {
        ArgumentNullException.ThrowIfNull(texts);

        // Parse each distinct text once, bounding the count before parsing more
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var selectors = new List<PositionSelector>();
        var count = 0;
        foreach (var text in texts)
        {
            if (text is null)
            {
                throw new ArgumentNullException(nameof(texts), "A position selector text must not be null.");
            }
            if (++count > MaxSelectors)
            {
                throw new FormatException(
                    $"There are more than {MaxSelectors.ToString(CultureInfo.InvariantCulture)} position selectors.");
            }

            if (seen.Add(text))
            {
                selectors.Add(PositionSelector.Parse(text));
            }
        }

        return selectors.Count == 0
            ? throw new FormatException("At least one position selector is needed (use 'all' to select everything).")
            : new PositionSelection([.. selectors]);
    }

    /// <summary>
    ///     Tells whether any selector can match a position of a kind, so that a renderer can skip building the
    ///     positions of kinds nobody asked for.
    /// </summary>
    /// <param name="kind">The lowercase kind.</param>
    /// <returns><see langword="true"/> when a selector is <c>all</c> or names the kind.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="kind"/> is null.</exception>
    public bool IncludesKind(string kind)
    {
        ArgumentNullException.ThrowIfNull(kind);
        return Selectors.Any(selector => selector.IsAll || string.Equals(selector.Kind, kind, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Tells whether any selector matches a position.
    /// </summary>
    /// <param name="position">The position.</param>
    /// <returns><see langword="true"/> when the position is wanted.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="position"/> is null.</exception>
    public bool Matches(ImagePosition position)
    {
        ArgumentNullException.ThrowIfNull(position);
        return Selectors.Any(selector => selector.Matches(position));
    }
}
