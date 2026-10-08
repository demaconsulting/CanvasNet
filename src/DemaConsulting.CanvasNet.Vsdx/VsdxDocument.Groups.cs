namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio davehoward

/// <summary>
///     Implements the <see cref="VsdxDocument"/> recursive group/nested-shape resolver: walks a
///     shape's <see cref="VsdxShapeNode.Children"/> tree to arbitrary nesting depth, resolving
///     each child exactly like a top-level shape (see <c>VsdxDocument.Shapes.cs</c>'s former
///     <c>ResolveShape</c>, whose body now lives in <see cref="ResolveShapeRecursive"/>) but
///     additionally correlating a group child's own Master counterpart by
///     <see cref="VsdxShapeNode.MasterShapeId"/>/<see cref="VsdxShapeNode.Id"/> (not position -
///     see <c>davehoward-test3-house.vsdx</c>'s own evidence) and recording each child's resolved
///     <see cref="VsdxShapeNode.Parent"/> so <see cref="ToPageSpace"/> can later compose its
///     absolute page-space position.
/// </summary>
public sealed partial class VsdxDocument
{
    /// <summary>
    ///     The maximum group nesting depth a page's shape tree may reach before
    ///     <see cref="ResolveShapeRecursive"/> throws <see cref="InvalidDataException"/>, guarding
    ///     against a pathological or cyclic structure causing unbounded recursion (a stack
    ///     overflow cannot itself be caught, so this budget must trip well before any realistic
    ///     call-stack limit). No in-scope fixture nests more than 3 levels deep (see
    ///     <c>davehoward-test10-nested-shapes.vsdx</c>), so this generous budget never trips for
    ///     any real document.
    /// </summary>
    private const int MaxGroupNestingDepth = 64;

    /// <summary>
    ///     The maximum number of shapes (across an entire page, top-level and nested combined)
    ///     <see cref="ResolveShapeRecursive"/> will resolve before throwing
    ///     <see cref="InvalidDataException"/>, guarding against a pathologically wide shape tree
    ///     (many shapes, each with many children) consuming unbounded time/memory even while
    ///     staying within <see cref="MaxGroupNestingDepth"/>.
    /// </summary>
    private const int MaxResolvedShapeCount = 50_000;

    /// <summary>
    ///     Resolves <paramref name="shape"/> in place - exactly as the single top-level-only
    ///     resolution this method superseded used to (Master/MasterShape cell and geometry-row
    ///     merge, StyleSheet chain walk, transform, paint, text, 1-D connector endpoints) - then
    ///     recurses into each of <paramref name="shape"/>'s own <see cref="VsdxShapeNode.Children"/>,
    ///     correlating each child's Master counterpart from <paramref name="masterShape"/>'s own
    ///     children by ID (see <c>davehoward-test3-house.vsdx</c>'s own stub-child/Master-group
    ///     evidence: a page shape's minimal <c>&lt;Shape MasterShape="6"/&gt;</c> stub must be
    ///     matched against the Master group's own child whose <c>ID="6"</c>, not against whichever
    ///     child happens to occupy the same position).
    /// </summary>
    /// <param name="shape">The shape to resolve.</param>
    /// <param name="masterShape">
    ///     <paramref name="shape"/>'s own Master counterpart (a top-level page shape's own
    ///     <see cref="VsdxShapeNode.MasterId"/>-referenced Master shape, or - for a nested group
    ///     child - the Master group's own child located by matching <see cref="VsdxShapeNode.MasterShapeId"/>
    ///     against <see cref="VsdxShapeNode.Id"/>), or <see langword="null"/> when none applies.
    /// </param>
    /// <param name="parent">
    ///     <paramref name="shape"/>'s own already-resolved parent shape (the immediately-enclosing
    ///     group), or <see langword="null"/> for one of a page's own top-level shapes.
    /// </param>
    /// <param name="depth">
    ///     <paramref name="shape"/>'s own nesting depth: <c>0</c> for a page's top-level shapes,
    ///     incrementing by <c>1</c> for each level of <see cref="VsdxShapeNode.Children"/>.
    /// </param>
    /// <param name="resolvedShapeCount">
    ///     A running count of every shape resolved so far across the whole page (top-level and
    ///     nested combined), incremented before each shape's own resolution and checked against
    ///     <see cref="MaxResolvedShapeCount"/>.
    /// </param>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="depth"/> exceeds <see cref="MaxGroupNestingDepth"/>, or
    ///     <paramref name="resolvedShapeCount"/> exceeds <see cref="MaxResolvedShapeCount"/> -
    ///     either budget's violation is treated as a pathological or cyclic shape tree, never
    ///     silently truncated.
    /// </exception>
    private void ResolveShapeRecursive(
        VsdxShapeNode shape,
        VsdxShapeNode? masterShape,
        VsdxShapeNode? parent,
        int depth,
        ref int resolvedShapeCount)
    {
        if (depth > MaxGroupNestingDepth)
        {
            throw new InvalidDataException(
                $"Shape '{shape.Id}' exceeds the maximum supported group nesting depth of {MaxGroupNestingDepth}; " +
                "its page's shape tree is either pathological or cyclic.");
        }

        resolvedShapeCount++;
        if (resolvedShapeCount > MaxResolvedShapeCount)
        {
            throw new InvalidDataException(
                $"The page's shape tree exceeds the maximum supported resolved shape count of {MaxResolvedShapeCount}; " +
                "it is either pathological or cyclic.");
        }

        shape.Parent = parent;

        var effectiveCells = MergeCells(shape.RawCells, masterShape?.RawCells, isGroupChild: parent is not null);
        var effectiveGeometrySections = MergeGeometrySections(shape.RawGeometrySections, masterShape?.RawGeometrySections);

        var effectiveLineStyleId = shape.LineStyleId ?? masterShape?.LineStyleId;
        var effectiveFillStyleId = shape.FillStyleId ?? masterShape?.FillStyleId;
        var effectiveTextStyleId = shape.TextStyleId ?? masterShape?.TextStyleId;

        shape.EffectiveCells = effectiveCells;
        shape.Geometries = BuildGeometrySections(effectiveGeometrySections, effectiveCells);
        shape.Transform = BuildTransform(effectiveCells);
        shape.Paint = ResolvePaint(effectiveCells, shape.Geometries, effectiveLineStyleId, effectiveFillStyleId);

        // A 1-D (connector) shape always carries both a BeginX and an EndX cell (see the format
        // reference's §4.4); a 2-D shape carries neither. Trust the already-resolved V values
        // directly rather than recomputing them - see VsdxConnectorEndpoints's own remarks.
        shape.ConnectorEndpoints = effectiveCells.TryGet("BeginX", out _) && effectiveCells.TryGet("EndX", out _)
            ? new VsdxConnectorEndpoints(
                effectiveCells.GetDouble("BeginX"),
                effectiveCells.GetDouble("BeginY"),
                effectiveCells.GetDouble("EndX"),
                effectiveCells.GetDouble("EndY"))
            : null;

        // Assumption #4 (documented, low-risk design-consistency extension, not fixture-evidenced
        // - see the Milestone 4 plan report): an instance shape with no own <Text> element falls
        // back to its Master shape's own <Text> verbatim, mirroring every other "absent instance
        // cell/section ⇒ inherit Master" rule this unit already establishes.
        var effectiveRawText = shape.RawText.Runs.Count > 0 ? shape.RawText : masterShape?.RawText ?? VsdxRawText.Empty;
        var effectiveCharacterRows = MergeTextSectionRows(shape.RawCharacterRows, masterShape?.RawCharacterRows);
        var effectiveParagraphRows = MergeTextSectionRows(shape.RawParagraphRows, masterShape?.RawParagraphRows);

        var textRuns = effectiveRawText.Runs
            .Select(run => ResolveEffectiveRun(run, effectiveCharacterRows, effectiveParagraphRows, effectiveTextStyleId))
            .ToList();
        shape.TextRuns = textRuns;
        shape.TextBox = BuildTextBox(effectiveCells, shape.Transform);
        var textBoxStyle = ResolveTextBoxStyle(effectiveCells, effectiveTextStyleId);
        shape.TextLayout = ResolveTextLayout(textRuns, shape.TextBox, textBoxStyle, ResolveTextFont);

        if (shape.Children.Count == 0)
        {
            return;
        }

        Dictionary<string, VsdxShapeNode>? masterChildrenById = null;
        if (masterShape is not null && masterShape.Children.Count > 0)
        {
            masterChildrenById = new Dictionary<string, VsdxShapeNode>(StringComparer.Ordinal);
            foreach (var masterChild in masterShape.Children)
            {
                masterChildrenById[masterChild.Id] = masterChild;
            }
        }

        foreach (var child in shape.Children)
        {
            VsdxShapeNode? childMasterShape = null;
            if (child.MasterShapeId is not null)
            {
                masterChildrenById?.TryGetValue(child.MasterShapeId, out childMasterShape);
            }

            ResolveShapeRecursive(child, childMasterShape, shape, depth + 1, ref resolvedShapeCount);
        }
    }

    /// <summary>
    ///     Composes <paramref name="shape"/>'s own resolved transform with every ancestor's own
    ///     resolved transform (walking <see cref="VsdxShapeNode.Parent"/> up to a page's own
    ///     top-level shape, whose transform already maps directly into page space), mapping a
    ///     point expressed in <paramref name="shape"/>'s own local box into absolute page-space
    ///     inches.
    /// </summary>
    /// <remarks>
    ///     Confirmed directly against <c>davehoward-test10-nested-shapes.vsdx</c>'s
    ///     <c>visio/pages/page1.xml</c>: a nested group child's own <c>PinX</c>/<c>PinY</c> cell
    ///     formulas reference <c>Sheet.N!Width</c>/<c>Sheet.N!Height</c>, where <c>N</c> is the
    ///     immediately-enclosing parent group's own sheet number (for example
    ///     <c>PinX F='Sheet.7!Width*0.2125'</c>) - meaning a child's already-resolved <c>V</c>
    ///     values are expressed directly in its parent's own local box, with no DrawingML-style
    ///     <c>chOff</c>/<c>chExt</c> child-coordinate-space remap. So each level's own
    ///     <see cref="VsdxShapeTransform.ToPage(double, double)"/> maps that level's own local
    ///     point directly into its immediate parent's local box - which is why a single recursive
    ///     walk up <see cref="VsdxShapeNode.Parent"/>, applying one <c>ToPage</c> call per level,
    ///     is sufficient to reach true page space.
    /// </remarks>
    /// <param name="shape">The shape whose own local box <paramref name="localX"/>/<paramref name="localY"/> are expressed in.</param>
    /// <param name="localX">The local-box X coordinate, in inches.</param>
    /// <param name="localY">The local-box Y coordinate, in inches.</param>
    /// <returns>The corresponding absolute page-space point, in inches.</returns>
    internal static (double X, double Y) ToPageSpace(VsdxShapeNode shape, double localX, double localY)
    {
        var (x, y) = shape.Transform!.ToPage(localX, localY);
        return shape.Parent is null ? (x, y) : ToPageSpace(shape.Parent, x, y);
    }
}
