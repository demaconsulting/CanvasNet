namespace DemaConsulting.CanvasNet.Positions;

/// <summary>
///     One feature of a rendered picture (a node, an edge, a bar, an axis) with its geometry box and its attachment
///     pixel, so that a caller can place a callout on it.
/// </summary>
/// <remarks>
///     <para>
///         <b>Identity.</b> <see cref="Kind"/> and <see cref="Id"/> name the feature. The other facts tell it apart from
///         its neighbors and are what a <see cref="PositionSelector"/> predicate compares: <see cref="View"/>,
///         <see cref="From"/>, <see cref="To"/>, <see cref="Index"/> and <see cref="Values"/>. A renderer documents
///         which kinds it reports and how it names them.
///     </para>
///     <para>
///         <b>Geometry.</b> <see cref="Box"/> is the geometry bounds of the feature (stroke not included) and
///         <see cref="Pixel"/> is its attachment point: a deterministic point a leader line can end on, chosen by the
///         aim rule of the kind (the center of a box, the arc-length midpoint of an edge path, the vertex of a line). The
///         pixel is not a promise that the feature is painted there, nor that it is visible. Both are in the continuous
///         pixel coordinates of the picture (see <see cref="PositionPoint"/>); both are <see langword="null"/> only when a
///         later image step could not follow them.
///     </para>
///     <para>
///         The record is immutable and thread-safe: <see cref="Values"/> is a copy, sorted ordinally by channel name.
///         Record equality compares <see cref="Values"/> by reference.
///     </para>
/// </remarks>
public sealed record ImagePosition
{
    /// <summary>
    ///     The empty value list.
    /// </summary>
    private static readonly KeyValuePair<string, PositionValue>[] NoValues = [];

    /// <summary>
    ///     The kind of the feature.
    /// </summary>
    private readonly string _kind = string.Empty;

    /// <summary>
    ///     The id of the feature.
    /// </summary>
    private readonly string _id = string.Empty;

    /// <summary>
    ///     The source line of the feature.
    /// </summary>
    private readonly int? _line;

    /// <summary>
    ///     The index of the feature among its parallel siblings.
    /// </summary>
    private readonly int? _index;

    /// <summary>
    ///     The values of the feature.
    /// </summary>
    private readonly IReadOnlyList<KeyValuePair<string, PositionValue>> _values = NoValues;

    /// <summary>
    ///     The geometry box of the feature.
    /// </summary>
    private readonly PositionBox? _box;

    /// <summary>
    ///     The attachment pixel of the feature.
    /// </summary>
    private readonly PositionPoint? _pixel;

    /// <summary>
    ///     Initializes a position with its kind and id.
    /// </summary>
    /// <param name="kind">The kind; see <see cref="Kind"/>.</param>
    /// <param name="id">The id; see <see cref="Id"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="kind"/> or <paramref name="id"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="kind"/> is not a lowercase kind word.</exception>
    public ImagePosition(string kind, string id)
    {
        Kind = kind;
        Id = id;
    }

    /// <summary>
    ///     Gets the kind of the feature: a lowercase word such as <c>node</c>, <c>edge</c>, <c>mark</c> or <c>axis</c>.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
    /// <exception cref="ArgumentException">Thrown when set to text that is not <c>[a-z][a-z0-9-]*</c>.</exception>
    public string Kind
    {
        get => _kind;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!PositionSelector.IsKindWord(value) || value.Any(char.IsAsciiLetterUpper))
            {
                throw new ArgumentException($"The position kind '{value}' must be a lowercase word ([a-z][a-z0-9-]*).", nameof(value));
            }

            _kind = value;
        }
    }

    /// <summary>
    ///     Gets the id of the feature: the source id when the input names the feature, otherwise a synthetic id that is
    ///     deterministic for the same input. May be empty, never null.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
    public string Id
    {
        get => _id;
        init => _id = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    ///     Gets the text of the feature (a label, a title), or <see langword="null"/> when it has none.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    ///     Gets the 1-based source line that declares the feature, or <see langword="null"/> when it is not known.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a value below 1.</exception>
    public int? Line
    {
        get => _line;
        init => _line = value is < 1
            ? throw new ArgumentOutOfRangeException(nameof(value), value, "A position line is 1-based.")
            : value;
    }

    /// <summary>
    ///     Gets the canonical path of the chart view that holds the feature (<c>""</c> for a single view,
    ///     <c>hconcat[1]</c>, <c>vconcat[0].layer[1]</c>), or <see langword="null"/> when the picture has no views.
    /// </summary>
    public string? View { get; init; }

    /// <summary>
    ///     Gets the <c>name</c> the chart specification gives the view, or <see langword="null"/> when it has none.
    /// </summary>
    public string? ViewName { get; init; }

    /// <summary>
    ///     Gets the mark type of a chart mark (<c>bar</c>, <c>line</c>, <c>point</c>), or <see langword="null"/>.
    /// </summary>
    public string? Mark { get; init; }

    /// <summary>
    ///     Gets the id of the feature an edge or message starts at, or <see langword="null"/>.
    /// </summary>
    public string? From { get; init; }

    /// <summary>
    ///     Gets the id of the feature an edge or message ends at, or <see langword="null"/>.
    /// </summary>
    public string? To { get; init; }

    /// <summary>
    ///     Gets the 0-based index of an edge or message among the features of the same kind with the same
    ///     <see cref="From"/> and <see cref="To"/>, in drawing order, or <see langword="null"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a negative value.</exception>
    public int? Index
    {
        get => _index;
        init => _index = value is < 0
            ? throw new ArgumentOutOfRangeException(nameof(value), value, "A position index is not negative.")
            : value;
    }

    /// <summary>
    ///     Gets the values of the feature, sorted ordinally by name: for a chart mark, the normalized value of every
    ///     encoding channel bound to a scalar; for a pie slice, its <c>value</c>. Empty when the feature has none.
    /// </summary>
    /// <remarks>
    ///     The setter copies and sorts the pairs, so the caller's list may change afterwards without affecting the
    ///     position.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
    /// <exception cref="ArgumentException">Thrown when a name is null or empty, or occurs twice.</exception>
    public IReadOnlyList<KeyValuePair<string, PositionValue>> Values
    {
        get => _values;
        init => _values = CopyValues(value);
    }

    /// <summary>
    ///     Gets the geometry box of the feature (stroke not included), or <see langword="null"/> when a later image
    ///     step lost it.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when set to a box that is not finite or has a negative size.</exception>
    public PositionBox? Box
    {
        get => _box;
        init => _box = value is { } box && !box.IsValid()
            ? throw new ArgumentException("A position box must be finite with a size that is not negative.", nameof(value))
            : value;
    }

    /// <summary>
    ///     Gets the attachment point of the feature, or <see langword="null"/> when a later image step lost it.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when set to a point that is not finite.</exception>
    public PositionPoint? Pixel
    {
        get => _pixel;
        init => _pixel = value is { } point && !point.IsValid()
            ? throw new ArgumentException("A position pixel must be finite.", nameof(value))
            : value;
    }

    /// <summary>
    ///     Looks up a value by its channel name.
    /// </summary>
    /// <param name="name">The name; compared ordinally (case-sensitively).</param>
    /// <param name="value">The value when the channel is present.</param>
    /// <returns><see langword="true"/> when the channel is present (even when it holds null).</returns>
    public bool TryGetValue(string name, out PositionValue value)
    {
        for (var index = 0; index < _values.Count; index++)
        {
            if (string.Equals(_values[index].Key, name, StringComparison.Ordinal))
            {
                value = _values[index].Value;
                return true;
            }
        }

        value = PositionValue.Null;
        return false;
    }

    /// <summary>
    ///     Copies and sorts a value list, checking the names.
    /// </summary>
    /// <param name="values">The values.</param>
    /// <returns>A sorted copy.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="values"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a name is empty or occurs twice.</exception>
    private static KeyValuePair<string, PositionValue>[] CopyValues(IReadOnlyList<KeyValuePair<string, PositionValue>> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            return NoValues;
        }

        // Copy first, so that a list changed by the caller later does not change the position
        var copy = values.ToArray();
        Array.Sort(copy, (left, right) => string.CompareOrdinal(left.Key, right.Key));
        for (var index = 0; index < copy.Length; index++)
        {
            if (string.IsNullOrEmpty(copy[index].Key))
            {
                throw new ArgumentException("A position value name must not be empty.", nameof(values));
            }

            if (index > 0 && string.Equals(copy[index - 1].Key, copy[index].Key, StringComparison.Ordinal))
            {
                throw new ArgumentException($"The position value name '{copy[index].Key}' occurs twice.", nameof(values));
            }
        }

        return copy;
    }
}
