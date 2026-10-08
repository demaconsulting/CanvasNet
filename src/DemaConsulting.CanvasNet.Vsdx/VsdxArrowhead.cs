namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio

/// <summary>
///     The conservative, documented subset of <c>BeginArrow</c>/<c>EndArrow</c> index values this
///     unit recognizes and renders (see <c>VsdxDocument.Arrowheads.cs</c>'s
///     <c>ParseArrowheadStyle</c>). Every other index value degrades to <see cref="None"/> rather
///     than throwing, per <c>canvas-net-vsdx.md</c>'s Design Constraints ("a documented subset of
///     common arrowhead styles, with an unrecognized value degrading to ... a plain, unadorned
///     line end ..., never throwing"). Milestone 11 confirmed two further indices actually in use
///     via a full-document scan of <c>44501e.vsdx</c> (its "Binary Association"/"Directions" UML
///     connector groups): index <c>4</c> (a solid, filled triangle - visually indistinguishable
///     at this unit's render fidelity from <see cref="Arrow"/>'s own index <c>2</c> triangle, so
///     mapped to the same style rather than introducing a redundant near-duplicate) and index
///     <c>254</c>, resolved through a <c>USE("Navigable")</c> named-cell formula on one connector
///     group's own <c>EndArrow</c> cell, which the Visio-reference render shows as a distinct,
///     open/unfilled triangle outline - mapped to the new <see cref="HollowTriangle"/> style.
/// </summary>
internal enum VsdxArrowheadStyle
{
    /// <summary>No arrowhead (a plain, unadorned line end) - index <c>0</c>, and the degrade target for every unrecognized index.</summary>
    None,

    /// <summary>A filled, closed triangular arrowhead - index <c>2</c> ("Triangle arrowhead" per the authoritative [MS-VSDX] <c>BeginArrow</c>/<c>EndArrow</c> cell specification), and index <c>4</c> (confirmed in use by <c>44501e.vsdx</c>'s "Binary Association"/"Directions" connectors - see this type's own remarks).</summary>
    Arrow,

    /// <summary>An open, unfilled line-style arrowhead - index <c>1</c> ("Line arrowhead" per [MS-VSDX]).</summary>
    OpenArrow,

    /// <summary>A triangular arrowhead with a concave (inward-curved) base - index <c>5</c> ("Triangle arrow head with inward curve at base" per [MS-VSDX], the style commonly called "stealth" in other vector-drawing tools).</summary>
    Stealth,

    /// <summary>An unfilled diamond arrowhead - index <c>22</c> ("Diamond with no fill" per [MS-VSDX]).</summary>
    Diamond,

    /// <summary>A filled, round arrowhead - index <c>10</c> ("Round" per [MS-VSDX]).</summary>
    Circle,

    /// <summary>
    ///     An open, unfilled triangular arrowhead outline (the same silhouette as
    ///     <see cref="Arrow"/>, stroked rather than filled) - index <c>254</c>, confirmed resolved
    ///     via a <c>USE("Navigable")</c> named-cell formula on <c>44501e.vsdx</c>'s own "Binary
    ///     Association" UML connector group (the "-includes" association's own <c>EndArrow</c>
    ///     cell), where the Visio-reference render shows a distinct, hollow (not solid) triangle
    ///     terminator - the conventional UML notation for a navigable/directed association end.
    /// </summary>
    HollowTriangle
}

/// <summary>
///     A connector's resolved arrowhead at one end: its recognized <see cref="Style"/> and its
///     resolved <c>BeginArrowSize</c>/<c>EndArrowSize</c> index (<c>0</c>, very small, to <c>6</c>,
///     colossal, per the <c>EndArrowSize</c> cell's own documented range).
/// </summary>
/// <param name="Style">The resolved, recognized arrowhead style, or <see cref="VsdxArrowheadStyle.None"/> for no arrowhead or an unrecognized index.</param>
/// <param name="SizeIndex">The resolved arrowhead size index. Meaningless when <paramref name="Style"/> is <see cref="VsdxArrowheadStyle.None"/>.</param>
internal sealed record VsdxArrowhead(VsdxArrowheadStyle Style, int SizeIndex)
{
    /// <summary>The shared "no arrowhead" instance, used whenever no arrowhead cell resolves at all or resolves to an unrecognized index.</summary>
    public static readonly VsdxArrowhead NoArrowhead = new(VsdxArrowheadStyle.None, 0);
}
