namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio

/// <summary>
///     A single parsed VisioML <c>&lt;Cell&gt;</c> element: a cell-bag entry keyed by its
///     <c>N=</c> (name) attribute, carrying its raw <c>V=</c> (value) attribute and optional
///     <c>F=</c> (formula) attribute verbatim, with no unit conversion or type interpretation
///     performed here - every typed accessor (<c>AsDouble</c>-style helper) lives on the owning
///     <see cref="VsdxCellBag"/> instead, keeping this type a dumb, immutable value carrier.
/// </summary>
/// <param name="Name">The cell's <c>N=</c> attribute (its name, for example <c>"PinX"</c>).</param>
/// <param name="Value">The cell's raw <c>V=</c> attribute, exactly as it appears in the XML.</param>
/// <param name="Formula">
///     The cell's raw <c>F=</c> attribute, or <see langword="null"/> when the cell carries no
///     <c>F=</c> attribute at all. A literal <c>"Inh"</c> value marks <see cref="Value"/> as a
///     cached, inherited copy of a Master/MasterShape or StyleSheet ancestor's own resolved
///     value - see <see cref="IsInherited"/>.
/// </param>
internal readonly record struct VsdxCell(string Name, string Value, string? Formula)
{
    /// <summary>
    ///     <see langword="true"/> when <see cref="Formula"/> is exactly the literal string
    ///     <c>"Inh"</c> (Inherit) - meaning <see cref="Value"/> is only a cached, round-tripped
    ///     copy of whatever a Master/MasterShape or StyleSheet ancestor currently resolves to, not
    ///     a genuine instance-specific override. The Master/MasterShape and StyleSheet chain
    ///     merge algorithms (see <c>VsdxDocument.CellMerge.cs</c>/<c>VsdxDocument.Styles.cs</c>)
    ///     treat an inherited cell identically to an absent cell: both fall through to the
    ///     ancestor's own resolved value rather than using <see cref="Value"/> directly.
    /// </summary>
    public bool IsInherited => string.Equals(Formula, "Inh", StringComparison.Ordinal);
}
