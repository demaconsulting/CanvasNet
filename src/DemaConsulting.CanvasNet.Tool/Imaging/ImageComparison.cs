// cspell:ignore psnr rmse ssim luma
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Tool.Imaging;

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
internal readonly record struct ComparisonMetrics(
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
internal sealed record GridCell(
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
internal sealed record ComparisonResult(
    int Width,
    int Height,
    int Tolerance,
    ComparisonMetrics Metrics,
    int Columns,
    int Rows,
    IReadOnlyList<GridCell> Cells)
{
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
    public IReadOnlyList<GridCell> WorstCells(int count)
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

/// <summary>
///     How a difference image maps the difference of a pixel to a color.
/// </summary>
internal enum DiffStyle
{
    /// <summary>The largest channel difference, amplified, drawn through a hot ramp: black, red, yellow, white.</summary>
    Heat,

    /// <summary>Each channel's own absolute difference, amplified.</summary>
    Abs
}

/// <summary>
///     A measure that a comparison can be required to meet.
/// </summary>
internal enum ThresholdMetric
{
    /// <summary>The mean absolute error; it must be at most the limit.</summary>
    Mae,

    /// <summary>The root mean square error; it must be at most the limit.</summary>
    Rmse,

    /// <summary>The peak signal-to-noise ratio; it must be at least the limit.</summary>
    Psnr,

    /// <summary>The structural similarity; it must be at least the limit.</summary>
    Ssim,

    /// <summary>The fraction of differing pixels; it must be at most the limit.</summary>
    DiffRatio,

    /// <summary>The largest channel difference; it must be at most the limit.</summary>
    MaxDiff,

    /// <summary>The largest root mean square error of any grid cell; it must be at most the limit.</summary>
    CellRmse
}

/// <summary>
///     A requirement that one measure of a comparison is within a limit.
/// </summary>
/// <param name="Metric">The measure.</param>
/// <param name="Limit">The limit: a maximum for an error measure, a minimum for a similarity measure.</param>
internal readonly record struct ComparisonThreshold(ThresholdMetric Metric, double Limit);

/// <summary>
///     The outcome of checking one threshold.
/// </summary>
/// <param name="Metric">The measure.</param>
/// <param name="IsMinimum"><see langword="true"/> when the limit is a minimum (the measured value must be at least the limit).</param>
/// <param name="Limit">The limit.</param>
/// <param name="Value">The measured value, unrounded; positive infinity for the PSNR of identical images.</param>
/// <param name="Passed"><see langword="true"/> when the value is within the limit.</param>
internal readonly record struct ThresholdCheck(ThresholdMetric Metric, bool IsMinimum, double Limit, double Value, bool Passed);

/// <summary>
///     Compares two images of the same size and creates pictures of how they differ.
/// </summary>
/// <remarks>
///     <para>
///         The comparison is defined on opaque RGB: the alpha channel is ignored. For each pixel and each channel
///         the difference is the integer <c>d = |a - b|</c>; every sum is accumulated exactly in integers, and the
///         only floating-point steps are single divisions, <c>sqrt</c>, and <c>log10</c> at the end, so the same
///         inputs give the same numbers on every run.
///     </para>
///     <para>
///         The unit has no knowledge of files or commands: it works on <see cref="Surface"/> values only. The picture
///         creators are in <c>ImageComparison.Images.cs</c>.
///     </para>
/// </remarks>
internal static partial class ImageComparison
{
    /// <summary>The default tolerance of the differing-pixel count: a channel difference above 8 counts.</summary>
    public const int DefaultTolerance = 8;

    /// <summary>The default number of grid columns.</summary>
    public const int DefaultGridColumns = 4;

    /// <summary>The default number of grid rows.</summary>
    public const int DefaultGridRows = 4;

    /// <summary>The largest number of grid columns or rows.</summary>
    public const int MaxGridSide = 64;

    /// <summary>The default amplification of a difference image.</summary>
    public const double DefaultGain = 4;

    /// <summary>The default weight of the second image in an overlay.</summary>
    public const double DefaultOverlayWeight = 0.5;

    /// <summary>The width in pixels of the gaps between the panels of a side-by-side image.</summary>
    public const int SideBySideGap = 8;

    /// <summary>The largest side of an SSIM window in pixels.</summary>
    private const int SsimWindow = 8;

    /// <summary>The distance in pixels between the origins of SSIM windows.</summary>
    private const int SsimStride = 4;

    /// <summary>The SSIM stabilizing constant of the luminance term, <c>(0.01 * 255)^2</c>.</summary>
    private const double SsimC1 = 6.5025;

    /// <summary>The SSIM stabilizing constant of the contrast term, <c>(0.03 * 255)^2</c>.</summary>
    private const double SsimC2 = 58.5225;

    /// <summary>
    ///     Compares two images of the same size.
    /// </summary>
    /// <param name="a">The first image; it is not modified or disposed.</param>
    /// <param name="b">The second image; it is not modified or disposed.</param>
    /// <param name="tolerance">The largest channel difference that does not make a pixel count as differing, 0 to 255.</param>
    /// <param name="gridColumns">The number of grid columns, 1 to <see cref="MaxGridSide"/> and at most the width.</param>
    /// <param name="gridRows">The number of grid rows, 1 to <see cref="MaxGridSide"/> and at most the height.</param>
    /// <returns>The overall metrics and the grid.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an image is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the images have different sizes.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when the tolerance is not 0 to 255, or the grid is not 1 to 64 per side and no larger than the image.
    /// </exception>
    /// <remarks>
    ///     With <c>N</c> pixels, <c>MAE = sum(d) / 3N</c>, <c>RMSE = sqrt(sum(d^2) / 3N)</c>, and
    ///     <c>PSNR = 20 * log10(255 / RMSE)</c> (none when the RMSE is 0). A pixel differs when its largest channel
    ///     difference is above <paramref name="tolerance"/>. Cell <c>i</c> of <c>C</c> columns spans the x positions
    ///     <c>floor(i * W / C)</c> to <c>floor((i + 1) * W / C)</c>, and rows are divided the same way, so the cells
    ///     partition the image exactly; each cell has the same measures over its own pixels.
    /// </remarks>
    public static ComparisonResult Compare(Surface a, Surface b, int tolerance, int gridColumns, int gridRows)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (a.Width != b.Width || a.Height != b.Height)
        {
            throw new ArgumentException(
                $"The images have different sizes: {a.Width}x{a.Height} and {b.Width}x{b.Height}.", nameof(b));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(tolerance, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(tolerance, 255);
        ArgumentOutOfRangeException.ThrowIfLessThan(gridColumns, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(gridColumns, Math.Min(MaxGridSide, a.Width));
        ArgumentOutOfRangeException.ThrowIfLessThan(gridRows, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(gridRows, Math.Min(MaxGridSide, a.Height));

        var width = a.Width;
        var height = a.Height;
        var columnOf = BuildIndexTable(width, gridColumns);
        var rowOf = BuildIndexTable(height, gridRows);
        var cellCount = gridColumns * gridRows;
        var cellSum = new ulong[cellCount];
        var cellSumSquares = new ulong[cellCount];
        var cellDiffering = new long[cellCount];
        var maxDifference = 0;

        for (var y = 0; y < height; y++)
        {
            var rowA = a.GetRowSpan(y);
            var rowB = b.GetRowSpan(y);
            var cellRowStart = rowOf[y] * gridColumns;
            for (var x = 0; x < width; x++)
            {
                var pa = rowA[x];
                var pb = rowB[x];
                var dr = Math.Abs(pa.R - pb.R);
                var dg = Math.Abs(pa.G - pb.G);
                var db = Math.Abs(pa.B - pb.B);
                var largest = Math.Max(dr, Math.Max(dg, db));
                var cell = cellRowStart + columnOf[x];
                cellSum[cell] += (ulong)(dr + dg + db);
                cellSumSquares[cell] += (ulong)((dr * dr) + (dg * dg) + (db * db));
                if (largest > tolerance)
                {
                    cellDiffering[cell]++;
                }

                if (largest > maxDifference)
                {
                    maxDifference = largest;
                }
            }
        }

        var cells = new List<GridCell>(cellCount);
        ulong totalSum = 0;
        ulong totalSumSquares = 0;
        long totalDiffering = 0;
        for (var row = 0; row < gridRows; row++)
        {
            var top = (int)((long)row * height / gridRows);
            var bottom = (int)((long)(row + 1) * height / gridRows);
            for (var column = 0; column < gridColumns; column++)
            {
                var left = (int)((long)column * width / gridColumns);
                var right = (int)((long)(column + 1) * width / gridColumns);
                var index = (row * gridColumns) + column;
                var pixels = (long)(right - left) * (bottom - top);
                totalSum += cellSum[index];
                totalSumSquares += cellSumSquares[index];
                totalDiffering += cellDiffering[index];
                cells.Add(new GridCell(
                    column,
                    row,
                    left,
                    top,
                    right - left,
                    bottom - top,
                    cellSum[index] / (3.0 * pixels),
                    Math.Sqrt(cellSumSquares[index] / (3.0 * pixels)),
                    (double)cellDiffering[index] / pixels));
            }
        }

        var count = (long)width * height;
        var rmse = Math.Sqrt(totalSumSquares / (3.0 * count));
        var metrics = new ComparisonMetrics(
            count,
            maxDifference == 0,
            totalSum / (3.0 * count),
            rmse,
            rmse == 0 ? null : 20 * Math.Log10(255 / rmse),
            Ssim(a, b),
            totalDiffering,
            (double)totalDiffering / count,
            maxDifference);
        return new ComparisonResult(width, height, tolerance, metrics, gridColumns, gridRows, cells);
    }

    /// <summary>
    ///     Computes the structural similarity of two images of the same size: the mean SSIM of their luma over windows.
    /// </summary>
    /// <param name="a">The first image.</param>
    /// <param name="b">The second image.</param>
    /// <returns>The mean SSIM, -1 to 1; 1 for identical images.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an image is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the images have different sizes.</exception>
    /// <remarks>
    ///     <para>
    ///         The luma of a pixel is the integer <c>Y = (77 R + 150 G + 29 B + 128) >> 8</c>. The windows are
    ///         <c>min(8, W)</c> by <c>min(8, H)</c> pixels with uniform weights, with origins every 4 pixels in x and y
    ///         (while the window fits) plus a final window flush with the right and bottom edges when the stride does not
    ///         land on it, so that every pixel is covered. For a window of <c>n</c> pixels with the exact integer
    ///         sums <c>sA = sum(Ya)</c>, <c>sB</c>, <c>sAA</c>, <c>sBB</c>, and <c>sAB</c>:
    ///     </para>
    ///     <code>
    ///     SSIM = ((2 sA sB + C1 n^2) (2 (n sAB - sA sB) + C2 n^2))
    ///          / ((sA^2 + sB^2 + C1 n^2) (n (sAA + sBB) - sA^2 - sB^2 + C2 n^2))
    ///     </code>
    ///     <para>
    ///         with <c>C1 = (0.01 * 255)^2</c> and <c>C2 = (0.03 * 255)^2</c>: the standard
    ///         <c>(2 ma mb + C1)(2 cov + C2) / ((ma^2 + mb^2 + C1)(va + vb + C2))</c> with population variance, multiplied
    ///         through by <c>n^2</c>. The result is the arithmetic mean of the window values in row-major window order.
    ///     </para>
    /// </remarks>
    internal static double Ssim(Surface a, Surface b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (a.Width != b.Width || a.Height != b.Height)
        {
            throw new ArgumentException(
                $"The images have different sizes: {a.Width}x{a.Height} and {b.Width}x{b.Height}.", nameof(b));
        }

        var windowWidth = Math.Min(SsimWindow, a.Width);
        var windowHeight = Math.Min(SsimWindow, a.Height);
        var xs = WindowOrigins(a.Width, windowWidth);
        var ys = WindowOrigins(a.Height, windowHeight);
        var n = (double)windowWidth * windowHeight;
        var c1 = SsimC1 * n * n;
        var c2 = SsimC2 * n * n;

        double total = 0;
        foreach (var y0 in ys)
        {
            foreach (var x0 in xs)
            {
                long sumA = 0;
                long sumB = 0;
                long sumAA = 0;
                long sumBB = 0;
                long sumAB = 0;
                for (var y = y0; y < y0 + windowHeight; y++)
                {
                    var rowA = a.GetRowSpan(y);
                    var rowB = b.GetRowSpan(y);
                    for (var x = x0; x < x0 + windowWidth; x++)
                    {
                        var ya = Luma(rowA[x]);
                        var yb = Luma(rowB[x]);
                        sumA += ya;
                        sumB += yb;
                        sumAA += ya * ya;
                        sumBB += yb * yb;
                        sumAB += ya * yb;
                    }
                }

                var count = (long)windowWidth * windowHeight;
                var luminance = (2.0 * sumA * sumB) + c1;
                var contrast = (2.0 * ((count * sumAB) - (sumA * sumB))) + c2;
                var luminanceBase = ((double)sumA * sumA) + ((double)sumB * sumB) + c1;
                var contrastBase = ((double)((count * (sumAA + sumBB)) - (sumA * sumA) - (sumB * sumB))) + c2;
                total += (luminance * contrast) / (luminanceBase * contrastBase);
            }
        }

        return total / ((double)xs.Count * ys.Count);
    }

    /// <summary>
    ///     Checks a comparison against thresholds.
    /// </summary>
    /// <param name="result">The comparison result.</param>
    /// <param name="thresholds">The thresholds, checked in order.</param>
    /// <returns>One check per threshold, in the same order, using the unrounded measured values.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <remarks>
    ///     An error measure (MAE, RMSE, differing-pixel ratio, maximum channel difference, worst cell RMSE) passes when it is
    ///     at most the limit; a similarity measure (PSNR, SSIM) passes when it is at least the limit. The PSNR of identical
    ///     images is infinite, so it passes every minimum.
    /// </remarks>
    public static IReadOnlyList<ThresholdCheck> Evaluate(ComparisonResult result, IReadOnlyList<ComparisonThreshold> thresholds)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(thresholds);

        var checks = new List<ThresholdCheck>(thresholds.Count);
        foreach (var threshold in thresholds)
        {
            var value = threshold.Metric switch
            {
                ThresholdMetric.Mae => result.Metrics.Mae,
                ThresholdMetric.Rmse => result.Metrics.Rmse,
                ThresholdMetric.Psnr => result.Metrics.PsnrDb ?? double.PositiveInfinity,
                ThresholdMetric.Ssim => result.Metrics.Ssim,
                ThresholdMetric.DiffRatio => result.Metrics.DifferingPixelRatio,
                ThresholdMetric.MaxDiff => result.Metrics.MaxChannelDifference,
                ThresholdMetric.CellRmse => result.MaxCellRmse,
                _ => throw new ArgumentOutOfRangeException(nameof(thresholds), threshold.Metric, "The metric is not defined.")
            };
            var minimum = IsMinimum(threshold.Metric);
            checks.Add(new ThresholdCheck(
                threshold.Metric,
                minimum,
                threshold.Limit,
                value,
                minimum ? value >= threshold.Limit : value <= threshold.Limit));
        }

        return checks;
    }

    /// <summary>
    ///     Gets the name of a threshold metric as written on the command line and in results.
    /// </summary>
    /// <param name="metric">The metric.</param>
    /// <returns>The lower-case name: <c>mae</c>, <c>rmse</c>, <c>psnr</c>, <c>ssim</c>, <c>diff-ratio</c>, <c>max-diff</c>, or <c>cell-rmse</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the metric is not defined.</exception>
    public static string MetricName(ThresholdMetric metric)
    {
        return metric switch
        {
            ThresholdMetric.Mae => "mae",
            ThresholdMetric.Rmse => "rmse",
            ThresholdMetric.Psnr => "psnr",
            ThresholdMetric.Ssim => "ssim",
            ThresholdMetric.DiffRatio => "diff-ratio",
            ThresholdMetric.MaxDiff => "max-diff",
            ThresholdMetric.CellRmse => "cell-rmse",
            _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "The metric is not defined.")
        };
    }

    /// <summary>
    ///     Finds a threshold metric by name.
    /// </summary>
    /// <param name="text">The name, compared without regard to case.</param>
    /// <param name="metric">The metric when found.</param>
    /// <returns><see langword="true"/> when the name is one of the names of <see cref="MetricName"/>.</returns>
    public static bool TryParseMetric(string? text, out ThresholdMetric metric)
    {
        var matches = Enum.GetValues<ThresholdMetric>()
            .Where(candidate => string.Equals(MetricName(candidate), text, StringComparison.OrdinalIgnoreCase))
            .ToList();
        metric = matches.Count == 0 ? default : matches[0];
        return matches.Count != 0;
    }

    /// <summary>
    ///     Determines whether a metric's limit is a minimum.
    /// </summary>
    /// <param name="metric">The metric.</param>
    /// <returns><see langword="true"/> for PSNR and SSIM (the value must be at least the limit); <see langword="false"/> for the error measures.</returns>
    public static bool IsMinimum(ThresholdMetric metric)
    {
        return metric is ThresholdMetric.Psnr or ThresholdMetric.Ssim;
    }

    /// <summary>
    ///     Gets the range of limits that make sense for a metric.
    /// </summary>
    /// <param name="metric">The metric.</param>
    /// <returns>The smallest and largest accepted limit: 0 to 255 for MAE, RMSE, maximum difference, and worst cell RMSE; 0 to 1 for the differing-pixel ratio; 0 to 1000 for PSNR; -1 to 1 for SSIM.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the metric is not defined.</exception>
    public static (double Min, double Max) Range(ThresholdMetric metric)
    {
        return metric switch
        {
            ThresholdMetric.Mae or ThresholdMetric.Rmse or ThresholdMetric.MaxDiff or ThresholdMetric.CellRmse => (0, 255),
            ThresholdMetric.DiffRatio => (0, 1),
            ThresholdMetric.Psnr => (0, 1000),
            ThresholdMetric.Ssim => (-1, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "The metric is not defined.")
        };
    }

    /// <summary>
    ///     Builds the table that maps each position along a side to the index of the grid cell it belongs to.
    /// </summary>
    /// <param name="length">The length of the side in pixels.</param>
    /// <param name="cells">The number of cells along the side, at most <paramref name="length"/>.</param>
    /// <returns>For each position, its cell index, the cells being <c>floor(i * length / cells)</c> to <c>floor((i + 1) * length / cells)</c>.</returns>
    private static int[] BuildIndexTable(int length, int cells)
    {
        var table = new int[length];
        for (var cell = 0; cell < cells; cell++)
        {
            var start = (int)((long)cell * length / cells);
            var end = (int)((long)(cell + 1) * length / cells);
            for (var position = start; position < end; position++)
            {
                table[position] = cell;
            }
        }

        return table;
    }

    /// <summary>
    ///     Lists the origins of the SSIM windows along one side.
    /// </summary>
    /// <param name="length">The length of the side.</param>
    /// <param name="window">The window length, at most <paramref name="length"/>.</param>
    /// <returns>The origins 0, 4, 8, ... while the window fits, plus <c>length - window</c> when it is not already there.</returns>
    private static List<int> WindowOrigins(int length, int window)
    {
        var origins = new List<int>();
        for (var origin = 0; origin + window <= length; origin += SsimStride)
        {
            origins.Add(origin);
        }

        if (origins[^1] != length - window)
        {
            origins.Add(length - window);
        }

        return origins;
    }

    /// <summary>
    ///     Computes the integer luma of a pixel.
    /// </summary>
    /// <param name="pixel">The pixel.</param>
    /// <returns><c>(77 R + 150 G + 29 B + 128) >> 8</c>, 0 to 255.</returns>
    private static long Luma(Rgba32 pixel)
    {
        return ((77 * pixel.R) + (150 * pixel.G) + (29 * pixel.B) + 128) >> 8;
    }
}
