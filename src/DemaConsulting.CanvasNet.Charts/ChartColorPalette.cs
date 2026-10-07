using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Charts;

/// <summary>
///     Supplies <see cref="ChartRenderer"/>'s built-in default categorical color sequence, used
///     whenever a chart/series/point does not supply its own color.
/// </summary>
/// <remarks>
///     <para>
///     This mirrors the categorical-palette convention common to mainstream charting libraries:
///     ten colors at approximately evenly spaced hues, a consistent saturation/lightness, and a
///     fixed, documented alpha of 255 (fully opaque) - chosen so that adjacent series/wedges
///     remain visually distinguishable from one another without relying on color alone for every
///     comparison. The ten entries, in order, are: blue, orange, green, red, purple, brown, pink,
///     gray, olive, and cyan - the same ordering convention (and approximate hues) widely
///     recognized from common charting-library "tab10"-style categorical palettes.
///     </para>
///     <para>
///     <see cref="ChartRenderer"/> resolves a series/point's effective color by first checking
///     that series/point's own explicit color (<c>ChartSeries.Color</c>/
///     <c>ChartSeries.PointColors</c>), then <c>Chart.ColorPalette</c>, then
///     <c>ChartRenderOptions.ColorPalette</c>, and only falls back to <see cref="Default"/> when
///     all of those are absent. Whichever palette is actually in effect, a series/point index
///     beyond the palette's own length wraps around via <c>index % palette.Count</c> rather than
///     throwing or running out of colors - so a chart with more series/points than palette
///     entries simply repeats colors from the start, documented explicitly here since it is the
///     one piece of resolution behavior every palette (not just this default one) shares.
///     </para>
/// </remarks>
public static class ChartColorPalette
{
    /// <summary>
    ///     The built-in, fixed 10-entry categorical color palette <see cref="ChartRenderer"/>
    ///     falls back to when no caller-supplied palette (<c>Chart.ColorPalette</c> or
    ///     <c>ChartRenderOptions.ColorPalette</c>) and no series/point-level explicit color is
    ///     available for a given series/point index. See this type's own remarks for the
    ///     resolution order and index-wrapping behavior.
    /// </summary>
    public static readonly IReadOnlyList<Rgba32> Default = Array.AsReadOnly(
    [
        new Rgba32(0x1F, 0x77, 0xB4, 255), // blue
        new Rgba32(0xFF, 0x7F, 0x0E, 255), // orange
        new Rgba32(0x2C, 0xA0, 0x2C, 255), // green
        new Rgba32(0xD6, 0x27, 0x28, 255), // red
        new Rgba32(0x94, 0x67, 0xBD, 255), // purple
        new Rgba32(0x8C, 0x56, 0x4B, 255), // brown
        new Rgba32(0xE3, 0x77, 0xC2, 255), // pink
        new Rgba32(0x7F, 0x7F, 0x7F, 255), // gray
        new Rgba32(0xBC, 0xBD, 0x22, 255), // olive
        new Rgba32(0x17, 0xBE, 0xCF, 255), // cyan
    ]);
}
