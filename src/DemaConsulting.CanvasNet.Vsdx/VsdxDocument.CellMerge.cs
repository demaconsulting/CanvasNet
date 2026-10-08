namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio davehoward

/// <summary>
///     Implements the <see cref="VsdxDocument"/> Master/MasterShape cell and geometry-row merge
///     algorithm: for each cell name present on either the instance shape or its Master shape, the
///     instance's own literal (present, non-<c>"Inh"</c>) value wins; otherwise the value falls
///     through to the Master's own cell - and, for geometry, a per-row <c>Del="1"</c> marker
///     removes the Master's row at that <c>IX</c> entirely rather than overriding it (confirmed
///     directly against <c>davehoward-test9-rect-and-line.vsdx</c>'s Shape ID='3', a Dynamic
///     Connector instance).
/// </summary>
public sealed partial class VsdxDocument
{
    /// <summary>
    ///     The <see cref="VsdxShapeTransform"/>'s own input cell names - <c>PinX</c>/<c>PinY</c>/
    ///     <c>Width</c>/<c>Height</c>/<c>LocPinX</c>/<c>LocPinY</c>/<c>Angle</c>/<c>FlipX</c>/
    ///     <c>FlipY</c> - for which an instance's own cached cell (even when marked <c>F="Inh"</c>)
    ///     must always win over the Master's own same-named cell, rather than the generic
    ///     "literal-instance-wins, else Master's cell" rule <see cref="VsdxCellBagMerge.Merge"/>
    ///     otherwise applies. Unlike a shared style cell (for example <c>LineColor</c>), where the
    ///     Master's own cell genuinely <em>is</em> the shared value an inherited instance cell is
    ///     merely caching, these nine cells are never actually shared across instances of the same
    ///     Master: a 1-D (connector) shape's <c>PinX</c>/<c>PinY</c>/<c>Width</c>/<c>Height</c>/
    ///     <c>LocPinX</c>/<c>LocPinY</c> cells are themselves baked, per-instance, from that
    ///     instance's own <c>BeginX</c>/<c>BeginY</c>/<c>EndX</c>/<c>EndY</c> endpoints (see
    ///     <see cref="VsdxShapeTransform"/>'s own remarks) - the Master's own same-named cell is
    ///     only that Master's own unrelated default template position/size, never a value any
    ///     instance should ever adopt. Confirmed against <c>60973.vsdx</c>'s own connector-group
    ///     shape <c>ID='802'</c> (<c>Master='28'</c>): its own <c>PinX</c>/<c>PinY</c>/
    ///     <c>LocPinX</c>/<c>LocPinY</c> cells are marked <c>F="Inh"</c>, so the generic merge rule
    ///     discarded their already-correct, baked instance values (<c>PinX≈3.84</c>,
    ///     <c>PinY≈6.83</c>) in favor of the Master's own small, unrelated template-local position
    ///     (<c>PinX≈0.11</c>, <c>PinY≈0.09</c>) - collapsing the connector's entire resolved
    ///     geometry to the wrong corner of the page instead of its correct position between the
    ///     shapes it visually connects. This is this milestone's own plan report's Bug #2 root
    ///     cause, refined beyond the plan's own initial stroke-width-only diagnosis after the
    ///     stroke-width floor alone did not resolve every connector's visual absence against the
    ///     Visio-reference PNG - see this milestone's own completion report for the fuller
    ///     root-cause trace.
    /// </summary>
    private static readonly string[] TransformCellNames =
    [
        "PinX", "PinY", "Width", "Height", "LocPinX", "LocPinY", "Angle", "FlipX", "FlipY",
    ];

    /// <summary>
    ///     Merges an instance shape's own cells with its Master shape's cells (if any) into a
    ///     single, flat effective <see cref="VsdxCellBag"/>, then re-applies the instance's own
    ///     cached <see cref="TransformCellNames"/> cells (if present) over whatever the generic
    ///     merge produced - see <see cref="TransformCellNames"/>'s own remarks for why these nine
    ///     cells cannot use the generic "Master's cell wins over an inherited instance cell" rule.
    /// </summary>
    /// <param name="instanceCells">The instance shape's own direct <c>&lt;Cell&gt;</c> children.</param>
    /// <param name="masterCells">The Master shape's own direct <c>&lt;Cell&gt;</c> children, or <see langword="null"/> when the instance has no Master.</param>
    /// <returns>
    ///     <paramref name="instanceCells"/> unchanged when <paramref name="masterCells"/> is
    ///     <see langword="null"/>; otherwise a new, merged <see cref="VsdxCellBag"/> built per
    ///     this class's own remarks.
    /// </returns>
    private static VsdxCellBag MergeCells(VsdxCellBag instanceCells, VsdxCellBag? masterCells)
    {
        if (masterCells is null)
        {
            return instanceCells;
        }

        var merged = VsdxCellBagMerge.Merge(instanceCells, masterCells);
        return PreferInstanceTransformCells(merged, instanceCells);
    }

    /// <summary>
    ///     Overlays <paramref name="instanceCells"/>'s own cached <see cref="TransformCellNames"/>
    ///     cells (present or not, literal or <c>"Inh"</c>) onto <paramref name="merged"/>, for
    ///     every one of those nine names the instance itself actually carries a cell for -
    ///     leaving every other cell (and any <see cref="TransformCellNames"/> entry the instance
    ///     does not itself carry at all, which correctly keeps falling through to the Master's own
    ///     cell) untouched.
    /// </summary>
    /// <param name="merged">The already Master/instance-merged cell bag to overlay onto.</param>
    /// <param name="instanceCells">The instance shape's own direct <c>&lt;Cell&gt;</c> children.</param>
    /// <returns><paramref name="merged"/> unchanged when the instance carries none of <see cref="TransformCellNames"/>; otherwise a new, overlaid <see cref="VsdxCellBag"/>.</returns>
    private static VsdxCellBag PreferInstanceTransformCells(VsdxCellBag merged, VsdxCellBag instanceCells)
    {
        Dictionary<string, VsdxCell>? overlaid = null;
        foreach (var name in TransformCellNames)
        {
            if (!instanceCells.TryGet(name, out var instanceCell))
            {
                continue;
            }

            overlaid ??= merged.Names.ToDictionary(n => n, n => merged.TryGet(n, out var c) ? c : default, StringComparer.Ordinal);
            overlaid[name] = instanceCell;
        }

        return overlaid is null ? merged : VsdxCellBag.FromDictionary(overlaid);
    }

    /// <summary>
    ///     Merges an instance shape's own <c>&lt;Section N="Geometry"&gt;</c> sections with its
    ///     Master shape's own same-indexed sections (if any), per <see cref="MergeGeometryRows"/>
    ///     for the row-level merge within a matching section index.
    /// </summary>
    /// <param name="instanceSections">The instance shape's own geometry sections, unmerged.</param>
    /// <param name="masterSections">The Master shape's own geometry sections, or <see langword="null"/> when the instance has no Master.</param>
    /// <returns>The merged geometry sections, ordered by section <c>IX</c>.</returns>
    private static IReadOnlyList<VsdxGeometrySectionRaw> MergeGeometrySections(
        IReadOnlyList<VsdxGeometrySectionRaw> instanceSections,
        IReadOnlyList<VsdxGeometrySectionRaw>? masterSections)
    {
        if (masterSections is null || masterSections.Count == 0)
        {
            return instanceSections;
        }

        var byIndex = new SortedDictionary<int, (VsdxGeometrySectionRaw? Instance, VsdxGeometrySectionRaw? Master)>();
        foreach (var section in masterSections)
        {
            byIndex[section.Index] = (null, section);
        }

        foreach (var section in instanceSections)
        {
            var existing = byIndex.TryGetValue(section.Index, out var found) ? found : (null, null);
            byIndex[section.Index] = (section, existing.Master);
        }

        var merged = new List<VsdxGeometrySectionRaw>(byIndex.Count);
        foreach (var (index, (instanceSection, masterSection)) in byIndex)
        {
            if (instanceSection is null)
            {
                // The instance does not mention this section index at all: inherit the Master's
                // section verbatim.
                merged.Add(masterSection!);
                continue;
            }

            var mergedCells = masterSection is null
                ? instanceSection.Cells
                : VsdxCellBagMerge.Merge(instanceSection.Cells, masterSection.Cells);
            var mergedRows = MergeGeometryRows(instanceSection.Rows, masterSection?.Rows);
            merged.Add(new VsdxGeometrySectionRaw(index, mergedCells, mergedRows));
        }

        return merged;
    }

    /// <summary>
    ///     Merges an instance geometry section's own rows with its Master's own same-indexed
    ///     section's rows: a Master row not mentioned by the instance is inherited verbatim; an
    ///     instance row at the same <c>IX</c> replaces it entirely (the instance always carries
    ///     its own full row, including <c>T=</c>, even when only overriding one axis - see the
    ///     format reference's own observation that <c>T=</c> is always present); and an instance
    ///     row marked <c>Del="1"</c> removes the Master's row at that <c>IX</c> from the result
    ///     entirely.
    /// </summary>
    /// <param name="instanceRows">The instance section's own rows.</param>
    /// <param name="masterRows">The Master section's own rows, or <see langword="null"/> when there is no Master section at this index.</param>
    /// <returns>The merged rows, ordered by row <c>IX</c>.</returns>
    private static IReadOnlyList<VsdxGeometryRowRaw> MergeGeometryRows(
        IReadOnlyList<VsdxGeometryRowRaw> instanceRows,
        IReadOnlyList<VsdxGeometryRowRaw>? masterRows)
    {
        if (masterRows is null || masterRows.Count == 0)
        {
            // No Master row to merge against: any Del="1" marker is meaningless (nothing to
            // delete), so simply drop it along with its row, keeping every genuine row.
            return instanceRows.Where(row => !row.IsDelete).ToList();
        }

        var byIndex = new SortedDictionary<int, VsdxGeometryRowRaw>();
        foreach (var row in masterRows)
        {
            byIndex[row.Index] = row;
        }

        foreach (var row in instanceRows)
        {
            if (row.IsDelete)
            {
                byIndex.Remove(row.Index);
            }
            else
            {
                byIndex[row.Index] = row;
            }
        }

        return [.. byIndex.Values];
    }

    /// <summary>
    ///     Merges an instance shape's own <c>Section N="Character"</c>/<c>"Paragraph"</c> rows
    ///     (already row-indexed by <see cref="VsdxDocument.ParseTextSectionRows"/>) with its
    ///     Master shape's own same-named rows (if any): an instance row at index <c>k</c> is
    ///     itself merged cell-by-cell over the Master's same-index row via the existing
    ///     <see cref="VsdxCellBagMerge.Merge"/> (literal-instance-cell-wins, else Master's cell,
    ///     else instance's own non-literal cell); a Master row with no matching instance index is
    ///     inherited verbatim. No <c>Del="1"</c> concept exists for Character/Paragraph rows in any
    ///     inspected fixture, so none is implemented here (documented absence, not an oversight -
    ///     unlike <see cref="MergeGeometryRows"/>'s own row-delete handling).
    /// </summary>
    /// <param name="instanceRows">The instance shape's own row-indexed Character/Paragraph cell bags.</param>
    /// <param name="masterRows">The Master shape's own row-indexed Character/Paragraph cell bags, or <see langword="null"/> when the instance has no Master.</param>
    /// <returns>The merged rows, keyed by row index.</returns>
    private static IReadOnlyDictionary<int, VsdxCellBag> MergeTextSectionRows(
        IReadOnlyDictionary<int, VsdxCellBag> instanceRows,
        IReadOnlyDictionary<int, VsdxCellBag>? masterRows)
    {
        if (masterRows is null || masterRows.Count == 0)
        {
            return instanceRows;
        }

        var merged = new Dictionary<int, VsdxCellBag>();
        foreach (var (index, masterRow) in masterRows)
        {
            merged[index] = masterRow;
        }

        foreach (var (index, instanceRow) in instanceRows)
        {
            merged[index] = merged.TryGetValue(index, out var masterRow)
                ? VsdxCellBagMerge.Merge(instanceRow, masterRow)
                : instanceRow;
        }

        return merged;
    }
}

/// <summary>
///     Shared cell-bag-level merge helper used both for a shape's flat cell set and for a
///     geometry section's own flag cells (<c>NoFill</c>/<c>NoLine</c>/<c>NoShow</c>/...).
/// </summary>
internal static class VsdxCellBagMerge
{
    /// <summary>
    ///     Merges <paramref name="instanceCells"/> over <paramref name="masterCells"/>: for every
    ///     cell name present in either bag, the instance's own literal (present, non-inherited)
    ///     cell wins; otherwise the Master's own cell (if any) is used; otherwise the instance's
    ///     own cell is used as-is (even if itself marked <c>"Inh"</c> - there is nothing further
    ///     to inherit from, so its cached value is the best available answer); a cell name present
    ///     in neither bag remains absent from the result.
    /// </summary>
    /// <param name="instanceCells">The instance's own cells.</param>
    /// <param name="masterCells">The Master's own cells.</param>
    /// <returns>The merged <see cref="VsdxCellBag"/>.</returns>
    public static VsdxCellBag Merge(VsdxCellBag instanceCells, VsdxCellBag masterCells)
    {
        var names = CollectCellNames(instanceCells, masterCells);
        var merged = new Dictionary<string, VsdxCell>(StringComparer.Ordinal);

        foreach (var name in names)
        {
            if (instanceCells.TryGetLiteral(name, out var literalInstanceCell))
            {
                merged[name] = literalInstanceCell;
            }
            else if (masterCells.TryGet(name, out var masterCell))
            {
                merged[name] = masterCell;
            }
            else if (instanceCells.TryGet(name, out var instanceCell))
            {
                merged[name] = instanceCell;
            }
        }

        return VsdxCellBag.FromDictionary(merged);
    }

    /// <summary>Collects the union of cell names present in either bag, via each bag's own reflection-free name enumerator.</summary>
    /// <param name="first">The first bag to collect names from.</param>
    /// <param name="second">The second bag to collect names from.</param>
    /// <returns>The union of cell names.</returns>
    private static IReadOnlyCollection<string> CollectCellNames(VsdxCellBag first, VsdxCellBag second)
    {
        var names = new HashSet<string>(first.Names, StringComparer.Ordinal);
        names.UnionWith(second.Names);
        return names;
    }
}
