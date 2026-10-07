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
    ///     Merges an instance shape's own cells with its Master shape's cells (if any) into a
    ///     single, flat effective <see cref="VsdxCellBag"/>.
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

        return VsdxCellBagMerge.Merge(instanceCells, masterCells);
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
