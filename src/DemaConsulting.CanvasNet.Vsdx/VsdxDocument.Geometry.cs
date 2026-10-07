using System.Numerics;
using DemaConsulting.CanvasNet.Geometry;

namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio RelLineTo RelMoveTo NURBSTo davehoward

/// <summary>
///     Implements the <see cref="VsdxDocument"/> geometry-row resolver: converts a shape's merged
///     geometry sections (see <c>VsdxDocument.CellMerge.cs</c>) into shape-local-space
///     <see cref="VsdxGeometrySection"/> instances, recognizing <c>MoveTo</c>/<c>LineTo</c>/
///     <c>RelMoveTo</c>/<c>RelLineTo</c> rows and gracefully skipping every other row type.
/// </summary>
public sealed partial class VsdxDocument
{
    /// <summary>
    ///     Builds the resolved, shape-local-space <see cref="VsdxGeometrySection"/> list from the
    ///     given merged geometry sections.
    /// </summary>
    /// <param name="mergedSections">The shape's merged (Master/instance) geometry sections, in <c>IX</c> order.</param>
    /// <param name="effectiveCells">The shape's merged flat cell bag, supplying the <c>Width</c>/<c>Height</c> values <c>RelMoveTo</c>/<c>RelLineTo</c> rows are normalized against.</param>
    /// <returns>The resolved geometry sections, one per merged section, in the same order.</returns>
    private static IReadOnlyList<VsdxGeometrySection> BuildGeometrySections(
        IReadOnlyList<VsdxGeometrySectionRaw> mergedSections,
        VsdxCellBag effectiveCells)
    {
        if (mergedSections.Count == 0)
        {
            return [];
        }

        var width = effectiveCells.GetDouble("Width");
        var height = effectiveCells.GetDouble("Height");

        var result = new List<VsdxGeometrySection>(mergedSections.Count);
        foreach (var section in mergedSections)
        {
            result.Add(BuildGeometrySection(section, width, height));
        }

        return result;
    }

    /// <summary>Builds a single resolved <see cref="VsdxGeometrySection"/> from its merged raw section.</summary>
    /// <param name="section">The merged raw section.</param>
    /// <param name="width">The owning shape's resolved <c>Width</c> cell, in inches (normalizes <c>RelMoveTo</c>/<c>RelLineTo</c> rows).</param>
    /// <param name="height">The owning shape's resolved <c>Height</c> cell, in inches (normalizes <c>RelMoveTo</c>/<c>RelLineTo</c> rows).</param>
    /// <returns>The resolved <see cref="VsdxGeometrySection"/>.</returns>
    private static VsdxGeometrySection BuildGeometrySection(VsdxGeometrySectionRaw section, double width, double height)
    {
        var builder = new PathBuilder();
        var hasOpenSubpath = false;

        foreach (var row in section.Rows.OrderBy(r => r.Index))
        {
            switch (row.Type)
            {
                case "MoveTo":
                    builder.MoveTo(ToLocalPoint(row.Cells.GetDouble("X"), row.Cells.GetDouble("Y")));
                    hasOpenSubpath = true;
                    break;

                case "LineTo":
                    if (hasOpenSubpath)
                    {
                        builder.LineTo(ToLocalPoint(row.Cells.GetDouble("X"), row.Cells.GetDouble("Y")));
                    }

                    break;

                case "RelMoveTo":
                    builder.MoveTo(ToLocalPoint(row.Cells.GetDouble("X") * width, row.Cells.GetDouble("Y") * height));
                    hasOpenSubpath = true;
                    break;

                case "RelLineTo":
                    if (hasOpenSubpath)
                    {
                        builder.LineTo(ToLocalPoint(row.Cells.GetDouble("X") * width, row.Cells.GetDouble("Y") * height));
                    }

                    break;

                default:
                    // Unrecognized geometry row type (for example ArcTo, NURBSTo, EllipticalArcTo,
                    // InfiniteLine, RelCubBezTo, Ellipse): per canvas-net-vsdx.md's Risk Control
                    // Measures ("an unrecognized ... construct is tolerantly skipped rather than
                    // failing the whole parse"), this row is silently skipped, leaving the pen
                    // position exactly where the previous recognized row left it, rather than
                    // throwing. Confirmed exercised directly by
                    // davehoward-test5-master.vsdx's master1.xml, Shape ID='6', whose rounded-
                    // corner outline alternates recognized LineTo rows with unrecognized NURBSTo
                    // rows.
                    break;
            }
        }

        return new VsdxGeometrySection(
            NoFill: section.Cells.GetBool("NoFill"),
            NoLine: section.Cells.GetBool("NoLine"),
            NoShow: section.Cells.GetBool("NoShow"),
            Path: builder.Build());
    }

    /// <summary>Converts a shape-local X/Y pair (inches) into the <see cref="Vector2"/> coordinate <see cref="PathBuilder"/> expects.</summary>
    /// <param name="x">The local X coordinate, in inches.</param>
    /// <param name="y">The local Y coordinate, in inches.</param>
    /// <returns>The corresponding <see cref="Vector2"/>.</returns>
    private static Vector2 ToLocalPoint(double x, double y) => new((float)x, (float)y);
}
