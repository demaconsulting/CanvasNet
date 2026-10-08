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
    ///     <c>FlipY</c> - for which, <em>on a 1-D (connector) shape only</em> (both a
    ///     <c>BeginX</c> and an <c>EndX</c> cell present on the merged result - see
    ///     <see cref="MergeCells"/>'s own remarks), an instance's own cached cell (even when
    ///     marked <c>F="Inh"</c>) must always win over the Master's own same-named cell, rather
    ///     than the generic "literal-instance-wins, else Master's cell" rule
    ///     <see cref="VsdxCellBagMerge.Merge"/> otherwise applies. Unlike a shared style cell (for
    ///     example <c>LineColor</c>), where the Master's own cell genuinely <em>is</em> the shared
    ///     value an inherited instance cell is merely caching, these nine cells are never actually
    ///     shared across instances of the same Master <em>for a 1-D shape</em>: a 1-D (connector)
    ///     shape's <c>PinX</c>/<c>PinY</c>/<c>Width</c>/<c>Height</c>/<c>LocPinX</c>/
    ///     <c>LocPinY</c> cells are themselves baked, per-instance, from that instance's own
    ///     <c>BeginX</c>/<c>BeginY</c>/<c>EndX</c>/<c>EndY</c> endpoints (see
    ///     <see cref="VsdxShapeTransform"/>'s own remarks) - the Master's own same-named cell is
    ///     only that Master's own unrelated default template position/size, never a value any
    ///     instance should ever adopt. A 2-D shape (one with no <c>BeginX</c>/<c>EndX</c> cell) is
    ///     deliberately <em>excluded</em> from this overlay (see <see cref="MergeCells"/>): for a
    ///     2-D shape, the Master's own same-named cell genuinely can be the shared,
    ///     legitimately-inherited value (a shape never locally moved/resized by the author), so
    ///     the generic "Master's cell wins over an inherited instance cell" rule remains correct
    ///     and must not be overridden merely because the instance happens to carry its own stale/
    ///     cached cell for one of these nine names. Confirmed against <c>60973.vsdx</c>'s own connector-group
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
    ///     The arrowhead-decoration cell names - <c>BeginArrow</c>/<c>EndArrow</c>/
    ///     <c>BeginArrowSize</c>/<c>EndArrowSize</c> - for which, <em>on a 1-D (connector) shape
    ///     only</em> (the same <c>BeginX</c>/<c>EndX</c> gate <see cref="TransformCellNames"/>
    ///     uses - see <see cref="MergeCells"/>'s own remarks), an instance's own cached cell (even
    ///     when marked <c>F="Inh"</c>) must always win over the Master's own same-named cell,
    ///     mirroring <see cref="TransformCellNames"/>'s own rationale: a 1-D connector's own
    ///     arrowhead selection is a per-instance authoring choice (for example toggling a UML
    ///     association's navigability, or resizing one specific connector's arrow), never a value
    ///     genuinely shared with every other instance of the same Master the way an ordinary 2-D
    ///     shape's style cells are. A 2-D shape is deliberately excluded from this overlay for the
    ///     same reason <see cref="TransformCellNames"/> excludes one: a 2-D shape's own arrowhead
    ///     cell (when present at all) genuinely can be the Master's own shared default, so the
    ///     generic "Master's cell wins over an inherited instance cell" rule remains correct there.
    /// </summary>
    private static readonly string[] ArrowCellNames =
    [
        "BeginArrow", "EndArrow", "BeginArrowSize", "EndArrowSize",
    ];

    /// <summary>
    ///     Merges an instance shape's own cells with its Master shape's cells (if any) into a
    ///     single, flat effective <see cref="VsdxCellBag"/>, then - <em>only when the merged
    ///     result identifies the shape as 1-D</em> (both a <c>BeginX</c> and an <c>EndX</c> cell
    ///     present, the same detection convention <c>VsdxDocument.Groups.cs</c>'s own
    ///     <c>ConnectorEndpoints</c> resolution uses) - re-applies the instance's own cached
    ///     <see cref="TransformCellNames"/> and <see cref="ArrowCellNames"/> cells (if present)
    ///     over whatever the generic merge produced; see those two arrays' own remarks for why a
    ///     1-D shape's transform/arrowhead cells cannot use the generic "Master's cell wins over
    ///     an inherited instance cell" rule. A 2-D shape is left unaffected by this overlay: its
    ///     generic merge result (Master's cell wins over an <c>F="Inh"</c> instance cell) is
    ///     correct for a shape that legitimately inherits its full transform/arrowheads from its
    ///     Master.
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

        // Only a 1-D (connector) shape's own transform/arrowhead cells are baked, per-instance,
        // from that instance's own BeginX/BeginY/EndX/EndY endpoints or its own authoring choices
        // (see TransformCellNames/ArrowCellNames's own remarks) - the same detection convention
        // VsdxDocument.Groups.cs's own ConnectorEndpoints resolution uses (a 1-D shape always
        // carries both a BeginX and an EndX cell; a 2-D shape carries neither). A 2-D shape that
        // legitimately inherits its full transform/arrowheads from its Master (never locally
        // moved/resized/re-arrowed, its own cell - if any - still marked F="Inh") must keep
        // falling through to the generic "Master's cell wins over an inherited instance cell"
        // rule above; only a 1-D shape's own cached cell should ever override the Master's
        // unrelated template-local position/size/arrowhead.
        if (!merged.TryGet("BeginX", out _) || !merged.TryGet("EndX", out _))
        {
            return merged;
        }

        merged = PreferInstanceCells(merged, instanceCells, TransformCellNames);
        return PreferInstanceCells(merged, instanceCells, ArrowCellNames);
    }

    /// <summary>
    ///     Overlays <paramref name="instanceCells"/>'s own cached cells named in
    ///     <paramref name="names"/> (present or not, literal or <c>"Inh"</c>) onto
    ///     <paramref name="merged"/>, for every name the instance itself actually carries a cell
    ///     for - leaving every other cell (and any <paramref name="names"/> entry the instance
    ///     does not itself carry at all, which correctly keeps falling through to the Master's own
    ///     cell) untouched. The overlaid cell is always written back <em>as a literal</em> (its own
    ///     <c>F=</c> formula, if any, is dropped - see this method's own remarks) rather than
    ///     carrying forward an <c>"Inh"</c> marking: for <see cref="ArrowCellNames"/> specifically,
    ///     <c>ResolveLineCellValue</c>/<c>ResolveFillCellValue</c> (<c>VsdxDocument.Paint.cs</c>)
    ///     treat any <c>"Inh"</c>-marked cell as "not a genuine override, defer to the StyleSheet
    ///     chain instead" - the exact same convention <see cref="VsdxCellBagMerge.Merge"/> applies
    ///     generically - so an overlay that preserved the <c>"Inh"</c> marking verbatim would
    ///     silently have no effect on arrowhead resolution at all (the StyleSheet chain would still
    ///     win), defeating the entire purpose of this override. <see cref="TransformCellNames"/>'s
    ///     own consumer (<c>VsdxShapeTransform</c>) never inspects a cell's formula/inherited
    ///     status, so dropping it there is a no-op change, not merely a special case for arrowheads.
    /// </summary>
    /// <param name="merged">The already Master/instance-merged cell bag to overlay onto.</param>
    /// <param name="instanceCells">The instance shape's own direct <c>&lt;Cell&gt;</c> children.</param>
    /// <param name="names">The cell names to prefer the instance's own cached cell for, when present (see <see cref="TransformCellNames"/>/<see cref="ArrowCellNames"/>).</param>
    /// <returns><paramref name="merged"/> unchanged when the instance carries none of <paramref name="names"/>; otherwise a new, overlaid <see cref="VsdxCellBag"/>.</returns>
    private static VsdxCellBag PreferInstanceCells(VsdxCellBag merged, VsdxCellBag instanceCells, IReadOnlyList<string> names)
    {
        Dictionary<string, VsdxCell>? overlaid = null;
        foreach (var name in names)
        {
            if (!instanceCells.TryGet(name, out var instanceCell))
            {
                continue;
            }

            overlaid ??= merged.Names.ToDictionary(n => n, n => merged.TryGet(n, out var c) ? c : default, StringComparer.Ordinal);
            overlaid[name] = new VsdxCell(instanceCell.Name, instanceCell.Value, Formula: null);
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
    ///     section's rows <em>cell-by-cell</em> within each shared <c>IX</c>: a Master row not
    ///     mentioned by the instance is inherited verbatim; an instance row at the same <c>IX</c>
    ///     as a Master row is merged over it cell-by-cell via <see cref="VsdxCellBagMerge.Merge"/>
    ///     (the instance's own literal cells win; a cell name the instance row does not itself
    ///     carry falls through to the Master row's same-named cell - see
    ///     <see cref="MergeGeometryRow"/>); an instance row at an <c>IX</c> with no Master row is
    ///     used as-is; and an instance row marked <c>Del="1"</c> removes the Master's row at that
    ///     <c>IX</c> from the result entirely.
    /// </summary>
    /// <param name="instanceRows">The instance section's own rows.</param>
    /// <param name="masterRows">The Master section's own rows, or <see langword="null"/> when there is no Master section at this index.</param>
    /// <returns>The merged rows, ordered by row <c>IX</c>.</returns>
    /// <remarks>
    ///     This method previously replaced the Master's same-indexed row wholesale whenever the
    ///     instance carried any row at that <c>IX</c>, on the (now falsified) assumption that "the
    ///     instance always carries its own full row... even when only overriding one axis." Direct
    ///     raw-XML evidence from three independent real-world fixtures (<c>60489.vsdx</c>'s
    ///     "system boundary" rounded rect, <c>60973.vsdx</c>'s rack frame, and
    ///     <c>44501e.vsdx</c>'s UML class compartment divider) shows an instance row commonly
    ///     carrying only a subset of cells (for example only <c>X</c>, relying on Visio's own
    ///     <c>F="Inh"</c> to inherit <c>Y</c> from the Master's same-indexed row) - the previous
    ///     whole-row replacement silently defaulted every cell the instance row omitted to
    ///     <c>0</c>, corrupting the resolved path into a diagonal "zigzag"/degenerate shape. See
    ///     this milestone's own completion report for the fuller root-cause trace.
    /// </remarks>
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
                byIndex[row.Index] = byIndex.TryGetValue(row.Index, out var masterRow)
                    ? MergeGeometryRow(row, masterRow)
                    : row;
            }
        }

        return [.. byIndex.Values];
    }

    /// <summary>
    ///     Merges a single instance geometry row over its Master's own same-indexed row,
    ///     cell-by-cell: the row's own cell bag is merged via <see cref="VsdxCellBagMerge.Merge"/>
    ///     (instance's own literal cell wins per name, else the Master row's same-named cell,
    ///     else the instance's own non-literal cell), then the instance row's own present cells
    ///     (present at all - literal <em>or</em> <c>F="Inh"</c>) are unconditionally overlaid back
    ///     on top via <see cref="PreferInstanceCells"/>, and the row's <c>T=</c> (type) is the
    ///     instance row's own value when present, otherwise the Master row's value - mirroring
    ///     every other cell's own "absent-on-instance falls through to Master" rule, since
    ///     <c>T=</c> is not itself a <c>&lt;Cell&gt;</c> but the row element's own attribute.
    /// </summary>
    /// <param name="instanceRow">The instance's own row at this <c>IX</c>.</param>
    /// <param name="masterRow">The Master's own row at the same <c>IX</c>.</param>
    /// <returns>The merged <see cref="VsdxGeometryRowRaw"/>, keyed at <paramref name="instanceRow"/>'s own <c>IX</c> (identical to <paramref name="masterRow"/>'s, by construction).</returns>
    /// <remarks>
    ///     A geometry row's cells (<c>X</c>/<c>Y</c>/<c>A</c>/.../<c>Del</c>) are never a value
    ///     genuinely shared across every instance of the same Master the way an ordinary style
    ///     cell (for example <c>LineColor</c>) is - a geometry row's resolved coordinates are
    ///     always derived from <em>that shape's own</em> already-resolved <c>Width</c>/
    ///     <c>Height</c> (for example <c>Width*0.5</c>), which differ per instance even when the
    ///     row's own cell is cached as <c>F="Inh"</c>. <see cref="VsdxCellBagMerge.Merge"/>'s
    ///     generic "literal-instance-wins, else Master's cell" rule therefore silently discards an
    ///     instance row's own already-correct, per-shape-derived <c>Inh</c> cell in favor of the
    ///     Master's own differently-scaled cached value whenever the instance's own cell is itself
    ///     marked <c>Inh</c> (not literal) - confirmed directly against <c>60489.vsdx</c>'s own
    ///     Shape <c>ID='114'</c> (an ellipse glued to its Master via a differently-sized
    ///     <c>Width</c>/<c>Height</c>): every one of its own geometry-row cells is cached as
    ///     <c>F="Inh"</c>, so the generic merge rule substituted the Master's own smaller cached
    ///     radius for every row, fusing two independently-sized ellipse instances of the same
    ///     Master into a single, wrongly-proportioned blob. Unlike <see cref="TransformCellNames"/>/
    ///     <see cref="ArrowCellNames"/> (which apply this same "instance's own cached cell always
    ///     wins" rule only on a 1-D shape), this overlay applies unconditionally to every geometry
    ///     row regardless of 1-D/2-D shape kind, because a geometry row's own coordinates are
    ///     <em>always</em> derived from that shape's own dimensions, never a shared page-space
    ///     position the way <see cref="TransformCellNames"/>'s own <c>PinX</c>/<c>PinY</c> can
    ///     legitimately be for a 2-D shape. A cell genuinely absent from the instance row (not
    ///     merely <c>Inh</c>-cached, but never mentioned by the instance row at all) is left alone
    ///     by <see cref="PreferInstanceCells"/> - see that method's own remarks - and keeps falling
    ///     through to the Master row's same-named cell via the preceding
    ///     <see cref="VsdxCellBagMerge.Merge"/> call, exactly as the existing, still-correct
    ///     "Master row not mentioned by the instance is inherited verbatim" rule for an entire row
    ///     already does at <see cref="MergeGeometryRows"/>'s own level.
    /// </remarks>
    private static VsdxGeometryRowRaw MergeGeometryRow(VsdxGeometryRowRaw instanceRow, VsdxGeometryRowRaw masterRow)
    {
        var mergedCells = VsdxCellBagMerge.Merge(instanceRow.Cells, masterRow.Cells);
        mergedCells = PreferInstanceCells(mergedCells, instanceRow.Cells, instanceRow.Cells.Names.ToArray());
        var type = instanceRow.Type.Length > 0 ? instanceRow.Type : masterRow.Type;
        return new VsdxGeometryRowRaw(instanceRow.Index, type, IsDelete: false, mergedCells);
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
