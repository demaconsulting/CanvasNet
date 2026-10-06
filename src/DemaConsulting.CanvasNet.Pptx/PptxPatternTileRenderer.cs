using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore pptx divot dmnd

/// <summary>
///     Procedurally synthesizes a small, repeating one-cell tile <see cref="Surface"/> for a
///     covered <see cref="PptxPresetPattern"/>, alternating a caller-supplied foreground/
///     background color per preset's own deterministic per-pixel rule - see
///     <see cref="PptxDocument.FillPaint(Surface, Geometry.Path, PptxPaint, System.Numerics.Matrix3x2)"/>,
///     which wraps this renderer's output in a <see cref="Drawing.TilePaint"/>.
/// </summary>
/// <remarks>
///     <para>
///     OOXML does not mandate a pixel-exact appearance for any <c>ST_PresetPatternVal</c> name -
///     only a named preset. This renderer's own per-preset rules are therefore a documented,
///     deliberately simple approximation (density/period based, not a reproduction of
///     PowerPoint's own exact rendering) - see <see cref="PptxPresetPattern"/>'s own remarks for
///     the full covered/deferred preset-name boundary this renderer implements.
///     </para>
///     <para>
///     Every tile is exactly <see cref="TileSize"/> x <see cref="TileSize"/> pixels - a single,
///     small, fixed size shared by every covered preset, chosen (not a magic number) to render a
///     visually sane repeat density at normal slide-rendering zoom levels while staying cheap to
///     synthesize - see <c>PptxDocument.Tables.cs</c>'s own <c>FillPaint</c> for how this tile's
///     pixel space is mapped into the owning shape's local EMU space (reusing the existing
///     <c>EmuPerPixelAt96Dpi</c> convention).
///     </para>
/// </remarks>
internal static class PptxPatternTileRenderer
{
    /// <summary>
    ///     The fixed pixel width/height of every synthesized tile - see this class's own remarks.
    /// </summary>
    internal const int TileSize = 8;

    /// <summary>
    ///     The classic 8x8 ordered (Bayer) dithering threshold matrix, each cell holding a
    ///     distinct value in <c>[0, 63]</c> arranged for maximally even visual dispersion - used
    ///     by <see cref="IsPercentageForeground"/> to approximate a percentage-density dot fill
    ///     without clumping.
    /// </summary>
    private static readonly int[,] BayerMatrix8X8 =
    {
        { 0, 48, 12, 60, 3, 51, 15, 63 },
        { 32, 16, 44, 28, 35, 19, 47, 31 },
        { 8, 56, 4, 52, 11, 59, 7, 55 },
        { 40, 24, 36, 20, 43, 27, 39, 23 },
        { 2, 50, 14, 62, 1, 49, 13, 61 },
        { 34, 18, 46, 30, 33, 17, 45, 29 },
        { 10, 58, 6, 54, 9, 57, 5, 53 },
        { 42, 26, 38, 22, 41, 25, 37, 21 },
    };

    /// <summary>The fixed pixel coordinates of each small diamond dot in <see cref="PptxPresetPattern.Divot"/>'s tile.</summary>
    private static readonly (int X, int Y)[] DivotForegroundPixels =
    {
        (1, 0), (0, 1), (1, 1), (2, 1), (1, 2),
        (5, 4), (4, 5), (5, 5), (6, 5), (5, 6),
    };

    /// <summary>
    ///     The per-column row offset defining <see cref="PptxPresetPattern.Wave"/>'s own wavy
    ///     line (a small, symmetric triangle wave spanning the tile's own 8-pixel height).
    /// </summary>
    private static readonly int[] WaveRowOffsets = { 0, 1, 2, 3, 3, 2, 1, 0 };

    /// <summary>
    ///     Synthesizes a <see cref="TileSize"/> x <see cref="TileSize"/> <see cref="Surface"/>
    ///     for <paramref name="preset"/>, painting each pixel <paramref name="foreground"/> or
    ///     <paramref name="background"/> per that preset's own deterministic rule (see this
    ///     class's own remarks and each private per-family method's documentation).
    /// </summary>
    /// <param name="preset">The covered preset pattern to synthesize a tile for.</param>
    /// <param name="foreground">The pattern's resolved foreground color.</param>
    /// <param name="background">The pattern's resolved background color.</param>
    /// <returns>A newly allocated <see cref="Surface"/> - ownership passes to the caller.</returns>
    internal static Surface RenderTile(PptxPresetPattern preset, Rgba32 foreground, Rgba32 background)
    {
        var surface = new Surface(TileSize, TileSize);
        for (var y = 0; y < TileSize; y++)
        {
            for (var x = 0; x < TileSize; x++)
            {
                surface[x, y] = IsForeground(preset, x, y) ? foreground : background;
            }
        }

        return surface;
    }

    /// <summary>
    ///     Determines whether pixel <paramref name="x"/>/<paramref name="y"/> (each in
    ///     <c>[0, TileSize)</c>) is painted the foreground color for <paramref name="preset"/>.
    /// </summary>
    private static bool IsForeground(PptxPresetPattern preset, int x, int y) =>
        preset switch
        {
            // Horizontal/vertical stripe family: see this method's own per-arm comments.
            PptxPresetPattern.Horz => y % 2 == 0,
            PptxPresetPattern.Vert => x % 2 == 0,
            PptxPresetPattern.LtHorz => y % 4 == 0,
            PptxPresetPattern.LtVert => x % 4 == 0,
            PptxPresetPattern.DkHorz => y % 4 != 0,
            PptxPresetPattern.DkVert => x % 4 != 0,

            // Diagonal-stripe family: "falling" uses (x+y) mod period; "rising" uses (x-y) mod
            // period (offset by TileSize to keep the operand non-negative before the CLR '%').
            PptxPresetPattern.DnDiag => (x + y) % 4 == 0,
            PptxPresetPattern.UpDiag => (x - y + TileSize) % 4 == 0,
            PptxPresetPattern.LtDnDiag => (x + y) % 8 == 0,
            PptxPresetPattern.LtUpDiag => (x - y + TileSize) % 8 == 0,
            PptxPresetPattern.DkDnDiag => (x + y) % 4 != 0,
            PptxPresetPattern.DkUpDiag => (x - y + TileSize) % 4 != 0,
            PptxPresetPattern.WdDnDiag => (x + y) % 8 < 4,
            PptxPresetPattern.WdUpDiag => (x - y + TileSize) % 8 < 4,

            // Cross-hatch family: the union of the corresponding "lt" horizontal/vertical or
            // diagonal stripe rules above.
            PptxPresetPattern.Cross => y % 4 == 0 || x % 4 == 0,
            PptxPresetPattern.DiagCross => (x + y) % 4 == 0 || (x - y + TileSize) % 4 == 0,

            // Percentage/dot-density family: see RenderPercentageTile's own remarks.
            PptxPresetPattern.Pct5 => IsPercentageForeground(x, y, 5),
            PptxPresetPattern.Pct10 => IsPercentageForeground(x, y, 10),
            PptxPresetPattern.Pct20 => IsPercentageForeground(x, y, 20),
            PptxPresetPattern.Pct25 => IsPercentageForeground(x, y, 25),
            PptxPresetPattern.Pct30 => IsPercentageForeground(x, y, 30),
            PptxPresetPattern.Pct40 => IsPercentageForeground(x, y, 40),
            PptxPresetPattern.Pct50 => IsPercentageForeground(x, y, 50),
            PptxPresetPattern.Pct60 => IsPercentageForeground(x, y, 60),
            PptxPresetPattern.Pct70 => IsPercentageForeground(x, y, 70),
            PptxPresetPattern.Pct75 => IsPercentageForeground(x, y, 75),
            PptxPresetPattern.Pct80 => IsPercentageForeground(x, y, 80),
            PptxPresetPattern.Pct90 => IsPercentageForeground(x, y, 90),

            // Fixture-mandatory named presets: see each field's own documentation.
            PptxPresetPattern.Divot => Array.IndexOf(DivotForegroundPixels, (x, y)) >= 0,
            PptxPresetPattern.Wave => WaveRowOffsets[x] == y || WaveRowOffsets[x] + 4 == y,

            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unrecognized covered preset pattern."),
        };

    /// <summary>
    ///     Determines whether pixel <paramref name="x"/>/<paramref name="y"/> is foreground for a
    ///     <c>pctNN</c> preset, approximating <paramref name="percent"/>'s density via
    ///     <see cref="BayerMatrix8X8"/>'s ordered-dithering threshold: a pixel is foreground when
    ///     its matrix cell value is less than <c>percent * 64 / 100</c> - evenly dispersing the
    ///     foreground-colored cells across the tile rather than clumping them, without needing to
    ///     reproduce PowerPoint's own exact rendering (see this class's own remarks).
    /// </summary>
    private static bool IsPercentageForeground(int x, int y, int percent) =>
        BayerMatrix8X8[y, x] < percent * TileSize * TileSize / 100;
}
