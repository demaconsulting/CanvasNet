using System.Xml.Linq;

namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio NURBSTo RelLineTo RelMoveTo davehoward

/// <summary>
///     A single, as-parsed (not yet merged with any Master/MasterShape row) VisioML
///     <c>&lt;Row&gt;</c> element from a <c>&lt;Section N="Geometry"&gt;</c> section.
/// </summary>
/// <param name="Index">The row's <c>IX=</c> attribute - its address for the Master/instance row-level merge (see <c>VsdxDocument.CellMerge.cs</c>).</param>
/// <param name="Type">The row's <c>T=</c> attribute (for example <c>"MoveTo"</c>, <c>"LineTo"</c>); every row observed across the in-scope fixtures always carries its own <c>T=</c>, even a <see cref="IsDelete"/> marker row, so this is never inherited from a Master row.</param>
/// <param name="IsDelete">
///     <see langword="true"/> when the row carries <c>Del="1"</c>: an instance-side marker that
///     removes the Master row at the same <see cref="Index"/> from the merged geometry entirely,
///     rather than overriding it (see the format reference's Master geometry-row-delete pattern,
///     confirmed directly against <c>davehoward-test9-rect-and-line.vsdx</c>'s Shape ID='3').
/// </param>
/// <param name="Cells">The row's own direct <c>&lt;Cell&gt;</c> children (for example <c>X</c>/<c>Y</c>).</param>
internal sealed record VsdxGeometryRowRaw(int Index, string Type, bool IsDelete, VsdxCellBag Cells)
{
    /// <summary>
    ///     Parses a single <c>&lt;Row&gt;</c> element of a <c>&lt;Section N="Geometry"&gt;</c>
    ///     section.
    /// </summary>
    /// <param name="rowElement">The <c>&lt;Row&gt;</c> element to parse.</param>
    /// <returns>The parsed <see cref="VsdxGeometryRowRaw"/>, or <see langword="null"/> when the row has no (or a non-numeric) <c>IX=</c> attribute.</returns>
    public static VsdxGeometryRowRaw? Parse(XElement rowElement)
    {
        var indexText = (string?)rowElement.Attribute("IX");
        if (!int.TryParse(indexText, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var index))
        {
            return null;
        }

        var type = (string?)rowElement.Attribute("T") ?? string.Empty;
        var isDelete = (string?)rowElement.Attribute("Del") == "1";
        return new VsdxGeometryRowRaw(index, type, isDelete, VsdxCellBag.Parse(rowElement));
    }
}

/// <summary>
///     A single, as-parsed (not yet merged with any Master's own same-index section) VisioML
///     <c>&lt;Section N="Geometry" IX="..."&gt;</c> section: an independent sub-path/hole of a
///     shape's combined outline (a shape may declare several <c>Geometry</c> sections, each
///     rendered as its own subpath within the same shape's fill/stroke pass).
/// </summary>
/// <param name="Index">The section's own <c>IX=</c> attribute - its address for the Master/instance section-level merge.</param>
/// <param name="Cells">The section's own direct <c>&lt;Cell&gt;</c> children (<c>NoFill</c>/<c>NoLine</c>/<c>NoShow</c>/<c>NoSnap</c>/<c>NoQuickDrag</c>).</param>
/// <param name="Rows">The section's parsed <c>&lt;Row&gt;</c> children, in document order.</param>
internal sealed record VsdxGeometrySectionRaw(int Index, VsdxCellBag Cells, IReadOnlyList<VsdxGeometryRowRaw> Rows)
{
    /// <summary>Parses a single <c>&lt;Section N="Geometry"&gt;</c> element.</summary>
    /// <param name="sectionElement">The <c>&lt;Section&gt;</c> element to parse.</param>
    /// <returns>The parsed <see cref="VsdxGeometrySectionRaw"/>, or <see langword="null"/> when the section has no (or a non-numeric) <c>IX=</c> attribute.</returns>
    public static VsdxGeometrySectionRaw? Parse(XElement sectionElement)
    {
        var indexText = (string?)sectionElement.Attribute("IX");
        if (!int.TryParse(indexText, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var index))
        {
            return null;
        }

        var ns = sectionElement.Name.Namespace;
        var rows = new List<VsdxGeometryRowRaw>();
        foreach (var rowElement in sectionElement.Elements(ns + "Row"))
        {
            var row = VsdxGeometryRowRaw.Parse(rowElement);
            if (row is not null)
            {
                rows.Add(row);
            }
        }

        return new VsdxGeometrySectionRaw(index, VsdxCellBag.Parse(sectionElement), rows);
    }
}

/// <summary>
///     A fully resolved (Master-merged) geometry section: its visibility flags and its shape-
///     local-space <see cref="DemaConsulting.CanvasNet.Geometry.Path"/>, built by walking the
///     merged, <c>IX</c>-ordered row list and emitting only the recognized row types
///     (<c>MoveTo</c>/<c>LineTo</c>/<c>RelMoveTo</c>/<c>RelLineTo</c>; see
///     <c>VsdxDocument.Geometry.cs</c>). Coordinates are left in the shape's own local box
///     (inches, origin bottom-left, spanning <c>[0, Width] x [0, Height]</c>) - never converted to
///     page coordinates here; that is <see cref="VsdxShapeTransform"/>'s job, applied by a later
///     milestone's paint pipeline (Milestone 7).
/// </summary>
/// <param name="NoFill">The section's <c>NoFill</c> flag - when set, this sub-path must not contribute to the shape's fill pass.</param>
/// <param name="NoLine">The section's <c>NoLine</c> flag - when set, this sub-path must not contribute to the shape's stroke pass.</param>
/// <param name="NoShow">The section's <c>NoShow</c> flag - when set, this sub-path is not rendered at all.</param>
/// <param name="Path">The sub-path's shape-local-space geometry.</param>
internal sealed record VsdxGeometrySection(bool NoFill, bool NoLine, bool NoShow, DemaConsulting.CanvasNet.Geometry.Path Path);
