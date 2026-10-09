using System.Globalization;

namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio

/// <summary>
///     Implements the <see cref="VsdxDocument"/> arrowhead resolver: resolves a connector's
///     <c>BeginArrow</c>/<c>EndArrow</c> style index and <c>BeginArrowSize</c>/<c>EndArrowSize</c>
///     size index through the same StyleSheet-chain-then-direct-override precedence pattern
///     already established for every other <c>Line*</c>-category cell (see
///     <c>VsdxDocument.Paint.cs</c>'s <c>ResolveLineCellValue</c>), then degrades an unrecognized
///     style index to <see cref="VsdxArrowheadStyle.None"/> rather than throwing - see
///     <see cref="VsdxArrowheadStyle"/>'s own remarks for the documented, conservative subset of
///     recognized indices, including the two indices (<c>4</c>, <c>254</c>) this milestone added
///     after a full-corpus scan confirmed them genuinely in use.
/// </summary>
public sealed partial class VsdxDocument
{
    /// <summary>
    ///     Resolves a connector's arrowhead at one end: its <c>BeginArrow</c>/<c>EndArrow</c> style
    ///     cell and its paired <c>BeginArrowSize</c>/<c>EndArrowSize</c> size cell, both resolved
    ///     through the shape's own literal cell first, then the StyleSheet chain walked via the
    ///     shape's effective <c>LineStyle</c> (the same precedence <see cref="ResolveLineCellValue"/>
    ///     already applies to every other <c>Line*</c>-category cell).
    /// </summary>
    /// <param name="effectiveCells">The shape's merged flat cell bag.</param>
    /// <param name="lineStyleId">The shape's effective <c>LineStyle</c> StyleSheet ID, or <see langword="null"/>.</param>
    /// <param name="isBegin"><see langword="true"/> to resolve <c>BeginArrow</c>/<c>BeginArrowSize</c>; <see langword="false"/> to resolve <c>EndArrow</c>/<c>EndArrowSize</c>.</param>
    /// <returns>The resolved <see cref="VsdxArrowhead"/>.</returns>
    private VsdxArrowhead ResolveArrowhead(VsdxCellBag effectiveCells, string? lineStyleId, bool isBegin)
    {
        var styleCellName = isBegin ? "BeginArrow" : "EndArrow";
        var sizeCellName = isBegin ? "BeginArrowSize" : "EndArrowSize";

        var styleRaw = ResolveLineCellValue(effectiveCells, styleCellName, lineStyleId)?.Value;
        var sizeRaw = ResolveLineCellValue(effectiveCells, sizeCellName, lineStyleId)?.Value;

        var style = ParseArrowheadStyle(styleRaw);
        return style == VsdxArrowheadStyle.None
            ? VsdxArrowhead.NoArrowhead
            : new VsdxArrowhead(style, ParseArrowheadSizeIndex(sizeRaw));
    }

    /// <summary>
    ///     Parses a <c>BeginArrow</c>/<c>EndArrow</c> cell's raw value into a recognized
    ///     <see cref="VsdxArrowheadStyle"/>, degrading an absent, non-numeric, or unrecognized
    ///     index to <see cref="VsdxArrowheadStyle.None"/> rather than throwing - see
    ///     <see cref="VsdxArrowheadStyle"/>'s own remarks for the exact recognized-index mapping.
    /// </summary>
    /// <param name="rawValue">The resolved cell's raw string value, or <see langword="null"/> when unresolved anywhere in the chain.</param>
    /// <returns>The recognized <see cref="VsdxArrowheadStyle"/>, or <see cref="VsdxArrowheadStyle.None"/> when <paramref name="rawValue"/> is absent, non-numeric, or an unrecognized index.</returns>
    private static VsdxArrowheadStyle ParseArrowheadStyle(string? rawValue)
    {
        if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
        {
            return VsdxArrowheadStyle.None;
        }

        return index switch
        {
            0 => VsdxArrowheadStyle.None,
            1 => VsdxArrowheadStyle.OpenArrow,
            2 => VsdxArrowheadStyle.Arrow,
            4 => VsdxArrowheadStyle.Arrow,
            5 => VsdxArrowheadStyle.Stealth,
            10 => VsdxArrowheadStyle.Circle,
            22 => VsdxArrowheadStyle.Diamond,
            254 => VsdxArrowheadStyle.HollowTriangle,
            _ => VsdxArrowheadStyle.None
        };
    }

    /// <summary>
    ///     Parses a <c>BeginArrowSize</c>/<c>EndArrowSize</c> cell's raw value into its size index,
    ///     defaulting to <c>2</c> (the built-in "No Style" StyleSheet's own documented default
    ///     resolved line/fill style) for an absent,
    ///     non-numeric, or negative value.
    /// </summary>
    /// <param name="rawValue">The resolved cell's raw string value, or <see langword="null"/> when unresolved anywhere in the chain.</param>
    /// <returns>The parsed size index, or <c>2</c> when <paramref name="rawValue"/> cannot be parsed as a non-negative integer.</returns>
    private static int ParseArrowheadSizeIndex(string? rawValue) =>
        int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var size) && size >= 0
            ? size
            : 2;
}
