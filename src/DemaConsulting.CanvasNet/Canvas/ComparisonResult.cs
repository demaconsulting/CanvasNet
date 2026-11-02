// cspell:ignore psnr rmse ssim
namespace DemaConsulting.CanvasNet.Canvas;

/// <summary>
///     The measures of how much two images of the same size differ, computed over every pixel.
/// </summary>
/// <param name="PixelCount">The number of pixels compared (width times height).</param>
/// <param name="Identical"><see langword="true"/> when every channel of every pixel is equal (<paramref name="MaxChannelDifference"/> is 0).</param>
/// <param name="Mae">The mean absolute error over all channels, 0 to 255.</param>
/// <param name="Rmse">The root mean square error over all channels, 0 to 255.</param>
/// <param name="PsnrDb">The peak signal-to-noise ratio in decibels, or <see langword="null"/> when the images are identical (infinite).</param>
/// <param name="Ssim">The mean structural similarity of the luma, -1 to 1; 1 for identical images.</param>
/// <param name="DifferingPixels">The number of pixels with a channel difference above the tolerance.</param>
/// <param name="DifferingPixelRatio">The fraction of pixels that differ above the tolerance, 0 to 1.</param>
/// <param name="MaxChannelDifference">The largest difference of any channel of any pixel, 0 to 255.</param>
public readonly record struct ComparisonMetrics(
    long PixelCount,
    bool Identical,
    double Mae,
    double Rmse,
    double? PsnrDb,
    double Ssim,
    long DifferingPixels,
    double DifferingPixelRatio,
    int MaxChannelDifference);

/// <summary>
///     The measures of one rectangular cell of the comparison grid.
/// </summary>
/// <param name="Column">The 0-based column of the cell.</param>
/// <param name="Row">The 0-based row of the cell.</param>
/// <param name="X">The x position of the cell's left edge in pixels.</param>
/// <param name="Y">The y position of the cell's top edge in pixels.</param>
/// <param name="Width">The width of the cell in pixels.</param>
/// <param name="Height">The height of the cell in pixels.</param>
/// <param name="Mae">The mean absolute error over the cell's channels.</param>
/// <param name="Rmse">The root mean square error over the cell's channels.</param>
/// <param name="DifferingPixelRatio">The fraction of the cell's pixels that differ above the tolerance.</param>
public sealed record ComparisonCell(
    int Column,
    int Row,
    int X,
    int Y,
    int Width,
    int Height,
    double Mae,
    double Rmse,
    double DifferingPixelRatio);

/// <summary>
///     The result of comparing two images: overall metrics and the per-cell grid.
/// </summary>
/// <param name="Width">The width of both images in pixels.</param>
/// <param name="Height">The height of both images in pixels.</param>
/// <param name="Tolerance">The tolerance that was used for the differing-pixel counts, 0 to 255.</param>
/// <param name="Metrics">The overall metrics.</param>
/// <param name="Columns">The number of grid columns.</param>
/// <param name="Rows">The number of grid rows.</param>
/// <param name="Cells">The grid cells in row-major order (<paramref name="Columns"/> times <paramref name="Rows"/>).</param>
public sealed record ComparisonResult(
    int Width,
    int Height,
    int Tolerance,
    ComparisonMetrics Metrics,
    int Columns,
    int Rows,
    IReadOnlyList<ComparisonCell> Cells)
{
    /// <summary>The largest number of grid columns or rows that <see cref="Surface.Compare"/> accepts.</summary>
    public const int MaxGridSide = 64;

    /// <summary>
    ///     Gets the largest root mean square error of any grid cell, 0 when every cell is identical.
    /// </summary>
    public double MaxCellRmse => Cells.Count == 0 ? 0 : Cells.Max(cell => cell.Rmse);

    /// <summary>
    ///     Gets the cells that differ most.
    /// </summary>
    /// <param name="count">The largest number of cells to return.</param>
    /// <returns>
    ///     Up to <paramref name="count"/> cells with a root mean square error above zero, the largest first; cells
    ///     with an equal error are in row-major order.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is negative.</exception>
    public IReadOnlyList<ComparisonCell> WorstCells(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        return Cells
            .Where(cell => cell.Rmse > 0)
            .OrderByDescending(cell => cell.Rmse)
            .ThenBy(cell => cell.Row)
            .ThenBy(cell => cell.Column)
            .Take(count)
            .ToList();
    }
}
