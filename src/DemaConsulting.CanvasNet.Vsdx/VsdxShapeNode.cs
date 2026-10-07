namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio davehoward

/// <summary>
///     A single parsed VisioML <c>&lt;Shape&gt;</c> element (from a page's content part or a
///     Master's content part), both in its as-parsed (raw) form and, once
///     <see cref="VsdxDocument"/>'s shape resolver has processed it, its resolved (Master-merged,
///     style-chain-walked) form.
/// </summary>
/// <remarks>
///     A shape's nested <c>&lt;Shapes&gt;</c> children are always parsed and exposed via
///     <see cref="Children"/> (so the model is forward-compatible with a later milestone's full
///     recursive group/nested-shape composition - see <c>davehoward-test10-nested-shapes.vsdx</c>
///     and <c>davehoward-test3-house.vsdx</c>'s own nested <c>MasterShape</c>-referencing group
///     children), but this milestone's resolver only populates <see cref="EffectiveCells"/>/
///     <see cref="Geometries"/>/<see cref="Transform"/>/<see cref="Paint"/> for the top-level
///     shapes directly under a page's <c>&lt;Shapes&gt;</c> element - a child's own corresponding
///     properties remain <see langword="null"/> until a later milestone (4/5/6) resolves them.
/// </remarks>
internal sealed class VsdxShapeNode
{
    /// <summary>Initializes a new instance of the <see cref="VsdxShapeNode"/> class from its raw, as-parsed fields.</summary>
    /// <param name="id">The shape's <c>ID=</c> attribute.</param>
    /// <param name="type">The shape's <c>Type=</c> attribute (for example <c>"Shape"</c>, <c>"Group"</c>), or <see cref="string.Empty"/> when absent.</param>
    /// <param name="masterId">The shape's own <c>Master=</c> attribute, or <see langword="null"/> when absent.</param>
    /// <param name="masterShapeId">The shape's own <c>MasterShape=</c> attribute, or <see langword="null"/> when absent.</param>
    /// <param name="lineStyleId">The shape's own <c>LineStyle=</c> attribute, or <see langword="null"/> when absent.</param>
    /// <param name="fillStyleId">The shape's own <c>FillStyle=</c> attribute, or <see langword="null"/> when absent.</param>
    /// <param name="textStyleId">The shape's own <c>TextStyle=</c> attribute, or <see langword="null"/> when absent.</param>
    /// <param name="rawCells">The shape's own direct <c>&lt;Cell&gt;</c> children.</param>
    /// <param name="rawGeometrySections">The shape's own direct <c>&lt;Section N="Geometry"&gt;</c> children, unmerged.</param>
    /// <param name="children">The shape's own direct <c>&lt;Shapes&gt;</c>/<c>&lt;Shape&gt;</c> descendants, recursively parsed but not resolved.</param>
    public VsdxShapeNode(
        string id,
        string type,
        string? masterId,
        string? masterShapeId,
        string? lineStyleId,
        string? fillStyleId,
        string? textStyleId,
        VsdxCellBag rawCells,
        IReadOnlyList<VsdxGeometrySectionRaw> rawGeometrySections,
        IReadOnlyList<VsdxShapeNode> children)
    {
        Id = id;
        Type = type;
        MasterId = masterId;
        MasterShapeId = masterShapeId;
        LineStyleId = lineStyleId;
        FillStyleId = fillStyleId;
        TextStyleId = textStyleId;
        RawCells = rawCells;
        RawGeometrySections = rawGeometrySections;
        Children = children;
    }

    /// <summary>The shape's <c>ID=</c> attribute.</summary>
    public string Id { get; }

    /// <summary>The shape's <c>Type=</c> attribute (for example <c>"Shape"</c>, <c>"Group"</c>), or <see cref="string.Empty"/> when absent.</summary>
    public string Type { get; }

    /// <summary>The shape's own <c>Master=</c> attribute - a page-level shape referencing a Master by ID - or <see langword="null"/> when absent.</summary>
    public string? MasterId { get; }

    /// <summary>The shape's own <c>MasterShape=</c> attribute - a group child referencing a sibling master shape by ID within its ancestor's Master - or <see langword="null"/> when absent. Not resolved this milestone; see this type's own remarks.</summary>
    public string? MasterShapeId { get; }

    /// <summary>The shape's own <c>LineStyle=</c> attribute (a StyleSheet ID), or <see langword="null"/> when absent.</summary>
    public string? LineStyleId { get; }

    /// <summary>The shape's own <c>FillStyle=</c> attribute (a StyleSheet ID), or <see langword="null"/> when absent.</summary>
    public string? FillStyleId { get; }

    /// <summary>The shape's own <c>TextStyle=</c> attribute (a StyleSheet ID), or <see langword="null"/> when absent.</summary>
    public string? TextStyleId { get; }

    /// <summary>The shape's own direct <c>&lt;Cell&gt;</c> children, unmerged with any Master shape.</summary>
    public VsdxCellBag RawCells { get; }

    /// <summary>The shape's own direct <c>&lt;Section N="Geometry"&gt;</c> children, unmerged with any Master shape.</summary>
    public IReadOnlyList<VsdxGeometrySectionRaw> RawGeometrySections { get; }

    /// <summary>The shape's own direct <c>&lt;Shapes&gt;</c>/<c>&lt;Shape&gt;</c> descendants, recursively parsed but not resolved - see this type's own remarks.</summary>
    public IReadOnlyList<VsdxShapeNode> Children { get; }

    /// <summary>
    ///     The shape's fully resolved (Master/MasterShape-merged) cell bag, or <see langword="null"/>
    ///     until resolved. Populated only for a page's top-level shapes this milestone.
    /// </summary>
    public VsdxCellBag? EffectiveCells { get; internal set; }

    /// <summary>
    ///     The shape's fully resolved (Master-merged, including the geometry-row-delete pattern)
    ///     geometry sections, or <see langword="null"/> until resolved.
    /// </summary>
    public IReadOnlyList<VsdxGeometrySection>? Geometries { get; internal set; }

    /// <summary>The shape's resolved shape-local-to-page-space transform, or <see langword="null"/> until resolved.</summary>
    public VsdxShapeTransform? Transform { get; internal set; }

    /// <summary>The shape's resolved stroke/fill paint, or <see langword="null"/> until resolved.</summary>
    public VsdxResolvedPaint? Paint { get; internal set; }
}
