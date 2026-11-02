// cspell:ignore psnr rmse ssim luma
namespace DemaConsulting.CanvasNet.Canvas;

/// <summary>
///     The comparison of <see cref="Surface"/> with another surface of the same size: error metrics, structural
///     similarity, and a grid of per-cell errors. (The pictures of a comparison are in <c>Surface.ComparisonImages.cs</c>.)
/// </summary>
/// <remarks>
///     <para>
///         The comparison is defined on opaque RGB: the alpha channel is ignored. For each pixel and each channel
///         the difference is the integer <c>d = |a - b|</c>; every sum is accumulated exactly in integers, and the
///         only floating-point steps are single divisions, <c>sqrt</c>, and <c>log10</c> at the end, so the same
///         inputs give the same numbers on every run.
///     </para>
///     <para>
///         Neither surface is modified or disposed, and a comparison reads both, so neither may be changed by another
///         thread meanwhile.
///     </para>
/// </remarks>
public sealed partial class Surface
{
    /// <summary>The largest side of an SSIM window in pixels.</summary>
    private const int SsimWindow = 8;

    /// <summary>The distance in pixels between the origins of SSIM windows.</summary>
    private const int SsimStride = 4;

    /// <summary>The SSIM stabilizing constant of the luminance term, <c>(0.01 * 255)^2</c>.</summary>
    private const double SsimC1 = 6.5025;

    /// <summary>The SSIM stabilizing constant of the contrast term, <c>(0.03 * 255)^2</c>.</summary>
    private const double SsimC2 = 58.5225;

    /// <summary>
    ///     Compares this surface with another of the same size.
    /// </summary>
    /// <param name="other">The surface to compare with; it is not modified or disposed.</param>
    /// <param name="tolerance">The largest channel difference that does not make a pixel count as differing, 0 to 255.</param>
    /// <param name="gridColumns">The number of grid columns, 1 to <see cref="ComparisonResult.MaxGridSide"/> and at most the width.</param>
    /// <param name="gridRows">The number of grid rows, 1 to <see cref="ComparisonResult.MaxGridSide"/> and at most the height.</param>
    /// <returns>The overall metrics and the grid.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the surfaces have different sizes.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when the tolerance is not 0 to 255, or the grid is not 1 to 64 per side and no larger than the image.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when either surface has been disposed.</exception>
    /// <remarks>
    ///     With <c>N</c> pixels, <c>MAE = sum(d) / 3N</c>, <c>RMSE = sqrt(sum(d^2) / 3N)</c>, and
    ///     <c>PSNR = 20 * log10(255 / RMSE)</c> (none when the RMSE is 0). A pixel differs when its largest channel
    ///     difference is above <paramref name="tolerance"/>. Cell <c>i</c> of <c>C</c> columns spans the x positions
    ///     <c>floor(i * W / C)</c> to <c>floor((i + 1) * W / C)</c>, and rows are divided the same way, so the cells
    ///     partition the image exactly; each cell has the same measures over its own pixels. The structural similarity
    ///     is described at <see cref="Ssim"/>.
    /// </remarks>
    public ComparisonResult Compare(Surface other, int tolerance, int gridColumns, int gridRows)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(other);
        ObjectDisposedException.ThrowIf(other._disposed, other);
        RequireSameSize(other);
        ArgumentOutOfRangeException.ThrowIfLessThan(tolerance, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(tolerance, 255);
        ArgumentOutOfRangeException.ThrowIfLessThan(gridColumns, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(gridColumns, Math.Min(ComparisonResult.MaxGridSide, Width));
        ArgumentOutOfRangeException.ThrowIfLessThan(gridRows, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(gridRows, Math.Min(ComparisonResult.MaxGridSide, Height));

        var width = Width;
        var height = Height;
        var columnOf = BuildIndexTable(width, gridColumns);
        var rowOf = BuildIndexTable(height, gridRows);
        var cellCount = gridColumns * gridRows;
        var cellSum = new ulong[cellCount];
        var cellSumSquares = new ulong[cellCount];
        var cellDiffering = new long[cellCount];
        var maxDifference = 0;

        for (var y = 0; y < height; y++)
        {
            var rowA = GetRowSpan(y);
            var rowB = other.GetRowSpan(y);
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

        var cells = new List<ComparisonCell>(cellCount);
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
                cells.Add(new ComparisonCell(
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
            Ssim(this, other),
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
    ///         The luma of a pixel is the integer <c>Y = (77 R + 150 G + 29 B + 128) >> 8</c> of its color, alpha ignored.
    ///         The windows are <c>min(8, W)</c> by <c>min(8, H)</c> pixels with uniform weights, with origins every 4
    ///         pixels in x and y (while the window fits) plus a final window flush with the right and bottom edges when
    ///         the stride does not land on it, so that every pixel is covered. For a window of <c>n</c> pixels with the
    ///         exact integer sums <c>sA = sum(Ya)</c>, <c>sB</c>, <c>sAA</c>, <c>sBB</c>, and <c>sAB</c>:
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
        a.RequireSameSize(b);

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
                        long ya = OpaqueLuma(rowA[x]);
                        long yb = OpaqueLuma(rowB[x]);
                        sumA += ya;
                        sumB += yb;
                        sumAA += ya * ya;
                        sumBB += yb * yb;
                        sumAB += ya * yb;
                    }
                }

                // A window is at most 8 by 8 pixels whatever the image size, so the sums are at most 64 * 255^2 and the
                // products below at most about 2.7e8: they cannot overflow a long, and the image size does not matter.
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
    ///     Requires that another surface has the size of this one.
    /// </summary>
    /// <param name="other">The other surface.</param>
    /// <exception cref="ArgumentException">Thrown when the sizes differ.</exception>
    private void RequireSameSize(Surface other)
    {
        if (Width != other.Width || Height != other.Height)
        {
            throw new ArgumentException(
                $"The images have different sizes: {Width}x{Height} and {other.Width}x{other.Height}.",
                nameof(other));
        }
    }
}
