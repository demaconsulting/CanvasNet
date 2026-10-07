namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio

/// <summary>
///     The conservative, documented subset of <c>BeginArrow</c>/<c>EndArrow</c> index values this
///     unit recognizes and renders (see <c>VsdxDocument.Arrowheads.cs</c>'s
///     <c>ParseArrowheadStyle</c>). Every other index value (including every index actually
///     exercised - all literally <c>0</c> - by this milestone's own real-world fixture corpus,
///     since no inspected sample declares a non-zero <c>BeginArrow</c>/<c>EndArrow</c> - see the
///     format reference's §9 "non-zero values not exercised in these samples - gap") degrades to
///     <see cref="None"/> rather than throwing, per <c>canvas-net-vsdx.md</c>'s Design Constraints
///     ("a documented subset of common arrowhead styles, with an unrecognized value degrading to
///     ... a plain, unadorned line end ..., never throwing").
/// </summary>
internal enum VsdxArrowheadStyle
{
    /// <summary>No arrowhead (a plain, unadorned line end) - index <c>0</c>, and the degrade target for every unrecognized index.</summary>
    None,

    /// <summary>A filled, closed triangular arrowhead - index <c>2</c> ("Triangle arrowhead" per the authoritative [MS-VSDX] <c>BeginArrow</c>/<c>EndArrow</c> cell specification).</summary>
    Arrow,

    /// <summary>An open, unfilled line-style arrowhead - index <c>1</c> ("Line arrowhead" per [MS-VSDX]).</summary>
    OpenArrow,

    /// <summary>A triangular arrowhead with a concave (inward-curved) base - index <c>5</c> ("Triangle arrow head with inward curve at base" per [MS-VSDX], the style commonly called "stealth" in other vector-drawing tools).</summary>
    Stealth,

    /// <summary>An unfilled diamond arrowhead - index <c>22</c> ("Diamond with no fill" per [MS-VSDX]).</summary>
    Diamond,

    /// <summary>A filled, round arrowhead - index <c>10</c> ("Round" per [MS-VSDX]).</summary>
    Circle
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
