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
///     <see cref="Children"/>. Since Milestone 6, <see cref="VsdxDocument"/>'s recursive shape
///     resolver (<c>VsdxDocument.Groups.cs</c>'s <c>ResolveShapeRecursive</c>) walks this tree to
///     arbitrary nesting depth, composing each child's own local transform with its parent's
///     already-resolved transform (see <see cref="Parent"/>) to produce correct absolute
///     page-space coordinates - see <c>davehoward-test10-nested-shapes.vsdx</c> (plain 3-level
///     nesting) and <c>davehoward-test3-house.vsdx</c> (Master-driven group children, correlated
///     by <c>MasterShapeId</c>/<see cref="Id"/>, not position).
/// </remarks>
internal sealed class VsdxShapeNode
{
    /// <summary>Initializes a new instance of the <see cref="VsdxShapeNode"/> class from its raw, as-parsed fields.</summary>
    /// <param name="identity">The shape's identifying attributes (<c>ID=</c>, <c>Type=</c>, <c>Master=</c>, <c>MasterShape=</c>).</param>
    /// <param name="styles">The shape's own <c>LineStyle=</c>, <c>FillStyle=</c> and <c>TextStyle=</c> attributes.</param>
    /// <param name="rawCells">The shape's own direct <c>&lt;Cell&gt;</c> children.</param>
    /// <param name="rawGeometrySections">The shape's own direct <c>&lt;Section N="Geometry"&gt;</c> children, unmerged.</param>
    /// <param name="text">The shape's own direct <c>&lt;Text&gt;</c> child and its <c>Character</c>/<c>Paragraph</c> section rows, all unmerged with any Master shape.</param>
    /// <param name="children">The shape's own direct <c>&lt;Shapes&gt;</c>/<c>&lt;Shape&gt;</c> descendants, recursively parsed but not resolved.</param>
    public VsdxShapeNode(
        VsdxShapeIdentity identity,
        VsdxShapeStyleIds styles,
        VsdxCellBag rawCells,
        IReadOnlyList<VsdxGeometrySectionRaw> rawGeometrySections,
        VsdxShapeTextData text,
        IReadOnlyList<VsdxShapeNode> children)
    {
        Id = identity.Id;
        Type = identity.Type;
        MasterId = identity.MasterId;
        MasterShapeId = identity.MasterShapeId;
        LineStyleId = styles.LineStyleId;
        FillStyleId = styles.FillStyleId;
        TextStyleId = styles.TextStyleId;
        RawCells = rawCells;
        RawGeometrySections = rawGeometrySections;
        RawText = text.RawText;
        RawCharacterRows = text.RawCharacterRows;
        RawParagraphRows = text.RawParagraphRows;
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

    /// <summary>
    ///     The shape's own direct <c>&lt;Text&gt;</c> child, parsed into marker-delimited runs, or
    ///     <see cref="VsdxRawText.Empty"/> when the shape has no <c>&lt;Text&gt;</c> element at
    ///     all. See <see cref="VsdxRawText.HasElement"/> to distinguish that genuinely-absent case
    ///     from an explicit, present-but-empty <c>&lt;Text/&gt;</c> element.
    /// </summary>
    public VsdxRawText RawText { get; }

    /// <summary>The shape's own direct <c>&lt;Section N="Character"&gt;</c> child's <c>&lt;Row IX="k"&gt;</c> children, unmerged with any Master shape. Empty when the shape declares no such section.</summary>
    public IReadOnlyDictionary<int, VsdxCellBag> RawCharacterRows { get; }

    /// <summary>The shape's own direct <c>&lt;Section N="Paragraph"&gt;</c> child's <c>&lt;Row IX="k"&gt;</c> children, unmerged with any Master shape. Empty when the shape declares no such section.</summary>
    public IReadOnlyDictionary<int, VsdxCellBag> RawParagraphRows { get; }

    /// <summary>The shape's own direct <c>&lt;Shapes&gt;</c>/<c>&lt;Shape&gt;</c> descendants, recursively parsed but not resolved - see this type's own remarks.</summary>
    public IReadOnlyList<VsdxShapeNode> Children { get; }

    /// <summary>
    ///     The shape's fully resolved (Master/MasterShape-merged) cell bag, or <see langword="null"/>
    ///     until resolved.
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

    /// <summary>
    ///     The shape's fully resolved (Master-merged, StyleSheet-chain-walked) text runs, or
    ///     <see langword="null"/> until resolved. Populated only for a page's top-level shapes
    ///     this milestone. Empty (not <see langword="null"/>) when the resolved shape has no text
    ///     content at all.
    /// </summary>
    public IReadOnlyList<VsdxEffectiveTextRun>? TextRuns { get; internal set; }

    /// <summary>The shape's resolved text-box transform (implicit default or explicit <c>Txt*</c> override), or <see langword="null"/> until resolved.</summary>
    public VsdxTextBoxTransform? TextBox { get; internal set; }

    /// <summary>The shape's resolved, word-wrapped glyph layout, or <see langword="null"/> until resolved.</summary>
    public VsdxTextLayout? TextLayout { get; internal set; }

    /// <summary>
    ///     This shape's own <c>&lt;Connect&gt;</c> entries from the page's <c>&lt;Connects&gt;</c>
    ///     section (see <c>VsdxDocument.Connects.cs</c>), attached by matching
    ///     <see cref="VsdxConnect.ConnectorShapeId"/> against <see cref="Id"/>. Empty (not
    ///     <see langword="null"/>) for a shape that is not a connector, or whose page declares no
    ///     <c>&lt;Connects&gt;</c> section at all.
    /// </summary>
    public IReadOnlyList<VsdxConnect> Connects { get; internal set; } = [];

    /// <summary>
    ///     This shape's resolved 1-D (connector) begin/end endpoint coordinates, or
    ///     <see langword="null"/> for a 2-D shape (one with no <c>BeginX</c>/<c>EndX</c> cell
    ///     pair in its merged, effective cell bag) - see <see cref="VsdxConnectorEndpoints"/>'s own
    ///     remarks for why these are trusted directly rather than recomputed from
    ///     <see cref="Connects"/>.
    /// </summary>
    public VsdxConnectorEndpoints? ConnectorEndpoints { get; internal set; }

    /// <summary>
    ///     This shape's resolved parent shape - the immediately-enclosing <c>&lt;Shape
    ///     Type="Group"&gt;</c> (or any other container shape) one level up the
    ///     <see cref="Children"/> tree - or <see langword="null"/> for a page's own top-level
    ///     shapes. Set by <c>VsdxDocument.Groups.cs</c>'s <c>ResolveShapeRecursive</c> as it walks
    ///     the tree, so a descendant's absolute page-space position can be composed by walking
    ///     this chain upward (see <c>VsdxDocument.Groups.cs</c>'s <c>ToPageSpace</c>) without
    ///     re-deriving it from scratch at every level.
    /// </summary>
    public VsdxShapeNode? Parent { get; internal set; }

    /// <summary>
    ///     Whether the shape's <c>LayerMember</c> places it only on layers that are hidden or
    ///     non-printing on its page; such a shape and its subtree are not painted.
    /// </summary>
    public bool HiddenByLayer { get; internal set; }
}

/// <summary>A shape's identifying attributes.</summary>
/// <param name="Id">The shape's <c>ID=</c> attribute.</param>
/// <param name="Type">The shape's <c>Type=</c> attribute (for example <c>"Shape"</c>, <c>"Group"</c>), or <see cref="string.Empty"/> when absent.</param>
/// <param name="MasterId">The shape's own <c>Master=</c> attribute, or <see langword="null"/> when absent.</param>
/// <param name="MasterShapeId">The shape's own <c>MasterShape=</c> attribute, or <see langword="null"/> when absent.</param>
internal readonly record struct VsdxShapeIdentity(string Id, string Type, string? MasterId, string? MasterShapeId);

/// <summary>A shape's own style-sheet references.</summary>
/// <param name="LineStyleId">The shape's own <c>LineStyle=</c> attribute, or <see langword="null"/> when absent.</param>
/// <param name="FillStyleId">The shape's own <c>FillStyle=</c> attribute, or <see langword="null"/> when absent.</param>
/// <param name="TextStyleId">The shape's own <c>TextStyle=</c> attribute, or <see langword="null"/> when absent.</param>
internal readonly record struct VsdxShapeStyleIds(string? LineStyleId, string? FillStyleId, string? TextStyleId);

/// <summary>A shape's own raw text content and its text-formatting section rows, unmerged with any Master shape.</summary>
/// <param name="RawText">The shape's own direct <c>&lt;Text&gt;</c> child, parsed into marker-delimited runs - see <see cref="VsdxDocument.ParseTextElement"/>.</param>
/// <param name="RawCharacterRows">The shape's own <c>&lt;Section N="Character"&gt;</c> rows.</param>
/// <param name="RawParagraphRows">The shape's own <c>&lt;Section N="Paragraph"&gt;</c> rows.</param>
internal readonly record struct VsdxShapeTextData(
    VsdxRawText RawText,
    IReadOnlyDictionary<int, VsdxCellBag> RawCharacterRows,
    IReadOnlyDictionary<int, VsdxCellBag> RawParagraphRows);
