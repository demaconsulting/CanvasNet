using System.Xml.Linq;

namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio

/// <summary>
///     An immutable, ordinal-name-keyed lookup of <see cref="VsdxCell"/> entries parsed from the
///     direct <c>&lt;Cell&gt;</c> children of a single VisioML element (a <c>&lt;Shape&gt;</c>,
///     <c>&lt;StyleSheet&gt;</c>, <c>&lt;Section&gt;</c>, or <c>&lt;Row&gt;</c>). Deliberately a
///     thin, generic cell-bag - no cell name is special-cased here; every typed/semantic
///     interpretation (unit conversion, the Master/StyleSheet "absent-or-Inh falls through"
///     rule, color/pattern decoding) lives in the dedicated resolver that owns that meaning (see
///     <c>VsdxDocument.CellMerge.cs</c>, <c>VsdxDocument.Styles.cs</c>, <c>VsdxColorPalette.cs</c>).
/// </summary>
internal sealed class VsdxCellBag
{
    /// <summary>The shared, immutable empty instance, returned for any element with no <c>&lt;Cell&gt;</c> children.</summary>
    public static readonly VsdxCellBag Empty = new(new Dictionary<string, VsdxCell>(StringComparer.Ordinal));

    /// <summary>The parsed cells, keyed by their <c>N=</c> (name) attribute.</summary>
    private readonly IReadOnlyDictionary<string, VsdxCell> _cells;

    /// <summary>Initializes a new instance of the <see cref="VsdxCellBag"/> class wrapping an already-built cell lookup.</summary>
    /// <param name="cells">The parsed cells, keyed by name.</param>
    private VsdxCellBag(IReadOnlyDictionary<string, VsdxCell> cells) => _cells = cells;

    /// <summary>The names of every cell in this bag, for a merge helper that needs the union of two bags' cell names.</summary>
    public IEnumerable<string> Names => _cells.Keys;

    /// <summary>
    ///     Wraps an already-built name-to-cell lookup (for example the result of a Master/instance
    ///     cell merge) as a new <see cref="VsdxCellBag"/>, with no further parsing.
    /// </summary>
    /// <param name="cells">The cells to wrap, keyed by name.</param>
    /// <returns>A new <see cref="VsdxCellBag"/>, or <see cref="Empty"/> when <paramref name="cells"/> is empty.</returns>
    public static VsdxCellBag FromDictionary(IReadOnlyDictionary<string, VsdxCell> cells) =>
        cells.Count == 0 ? Empty : new VsdxCellBag(cells);

    /// <summary>
    ///     Parses every direct <c>&lt;Cell&gt;</c> child element of <paramref name="element"/>
    ///     into a new <see cref="VsdxCellBag"/>. A cell with no <c>V</c> attribute is parsed with
    ///     an empty <see cref="VsdxCell.Value"/> rather than being skipped, since some cells
    ///     (for example a deleted geometry row's sibling cells) are never expected to carry one;
    ///     callers that require a value treat an empty string as "absent" via their own typed
    ///     accessor.
    /// </summary>
    /// <param name="element">The VisioML element whose direct <c>&lt;Cell&gt;</c> children should be parsed.</param>
    /// <returns>A new <see cref="VsdxCellBag"/>, or <see cref="Empty"/> when <paramref name="element"/> has no <c>&lt;Cell&gt;</c> children.</returns>
    public static VsdxCellBag Parse(XElement element)
    {
        var ns = element.Name.Namespace;
        var cells = new Dictionary<string, VsdxCell>(StringComparer.Ordinal);
        foreach (var cellElement in element.Elements(ns + "Cell"))
        {
            var name = (string?)cellElement.Attribute("N");
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            var value = (string?)cellElement.Attribute("V") ?? string.Empty;
            var formula = (string?)cellElement.Attribute("F");
            cells[name] = new VsdxCell(name, value, formula);
        }

        return cells.Count == 0 ? Empty : new VsdxCellBag(cells);
    }

    /// <summary>Attempts to find a cell named <paramref name="name"/>.</summary>
    /// <param name="name">The cell's <c>N=</c> attribute value to search for.</param>
    /// <param name="cell">The found cell, when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when a cell named <paramref name="name"/> exists in this bag.</returns>
    public bool TryGet(string name, out VsdxCell cell) => _cells.TryGetValue(name, out cell);

    /// <summary>
    ///     Returns whether this bag has a cell named <paramref name="name"/> whose value should be
    ///     treated as authoritative at this level - present and not marked <c>F="Inh"</c> (see
    ///     <see cref="VsdxCell.IsInherited"/>). Used by every "absent-or-inherited falls through to
    ///     the next ancestor" merge (Master/MasterShape cell merge, StyleSheet chain walk).
    /// </summary>
    /// <param name="name">The cell's <c>N=</c> attribute value to search for.</param>
    /// <param name="cell">The found, non-inherited cell, when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when a literal (present, non-inherited) cell named <paramref name="name"/> exists.</returns>
    public bool TryGetLiteral(string name, out VsdxCell cell)
    {
        if (_cells.TryGetValue(name, out cell) && !cell.IsInherited)
        {
            return true;
        }

        cell = default;
        return false;
    }

    /// <summary>Returns the raw string <see cref="VsdxCell.Value"/> of the cell named <paramref name="name"/>, or <see langword="null"/> when absent.</summary>
    /// <param name="name">The cell's <c>N=</c> attribute value to search for.</param>
    /// <returns>The cell's raw value, or <see langword="null"/>.</returns>
    public string? GetString(string name) => _cells.TryGetValue(name, out var cell) ? cell.Value : null;

    /// <summary>
    ///     Parses the cell named <paramref name="name"/>'s value as a culture-invariant
    ///     floating-point number, returning <paramref name="defaultValue"/> when the cell is
    ///     absent, empty, non-numeric, or non-finite.
    /// </summary>
    /// <param name="name">The cell's <c>N=</c> attribute value to search for.</param>
    /// <param name="defaultValue">The value to return when the cell cannot be parsed as a finite number.</param>
    /// <returns>The parsed value, or <paramref name="defaultValue"/>.</returns>
    public double GetDouble(string name, double defaultValue = 0d)
    {
        if (!_cells.TryGetValue(name, out var cell) ||
            string.IsNullOrEmpty(cell.Value) ||
            !double.TryParse(
                cell.Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var result) ||
            !double.IsFinite(result))
        {
            return defaultValue;
        }

        return result;
    }

    /// <summary>
    ///     Parses the cell named <paramref name="name"/>'s value as a Visio boolean (the literal
    ///     string <c>"1"</c> is <see langword="true"/>; anything else, including absence, is
    ///     <paramref name="defaultValue"/>).
    /// </summary>
    /// <param name="name">The cell's <c>N=</c> attribute value to search for.</param>
    /// <param name="defaultValue">The value to return when the cell is absent.</param>
    /// <returns>The parsed boolean value.</returns>
    public bool GetBool(string name, bool defaultValue = false)
    {
        if (!_cells.TryGetValue(name, out var cell))
        {
            return defaultValue;
        }

        return cell.Value == "1";
    }
}
