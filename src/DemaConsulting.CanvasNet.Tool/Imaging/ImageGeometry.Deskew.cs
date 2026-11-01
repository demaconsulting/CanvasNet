// cspell:ignore deskew luma
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Tool.Imaging;

/// <summary>
///     The estimated skew of a surface.
/// </summary>
/// <param name="Degrees">
///     The skew in degrees, in hundredths of a degree from -15 to 15; positive means the dominant near-horizontal
///     structure descends to the right (it is turned clockwise), so rotating the image by the negative of it, clockwise,
///     levels it. 0 when <paramref name="Estimated"/> is <see langword="false"/>.
/// </param>
/// <param name="Confidence">
///     How much better the best angle scores than a typical one, 0 (a flat profile: no line structure) to just below 1
///     (a very strong line structure), rounded to 3 decimals; 0 when <paramref name="Estimated"/> is <see langword="false"/>.
/// </param>
/// <param name="Estimated"><see langword="false"/> when the image has too few dark marks to estimate anything.</param>
internal readonly record struct SkewEstimate(double Degrees, double Confidence, bool Estimated);

/// <summary>
///     The skew estimation of <see cref="ImageGeometry"/>.
/// </summary>
internal static partial class ImageGeometry
{
    /// <summary>The smallest confidence at which the pipeline applies an estimated skew.</summary>
    /// <remarks>
    ///     Pinned with synthetic images (see the design of the unit): ruled and text-like line structure scores 0.73 to
    ///     0.97, random strokes in every direction up to about 0.30, and random noise up to about 0.30 (only a very wide
    ///     image, 2000 by 300, gets that high: the rectangle of the image itself favors horizontal profiles; a roughly
    ///     square or 4:3 image gets 0.01 to 0.06).
    /// </remarks>
    public const double MinConfidence = 0.4;

    /// <summary>The smallest skew in degrees that the pipeline corrects; a smaller estimate leaves the image unchanged.</summary>
    public const double MinCorrectedSkew = 0.05;

    /// <summary>The largest skew in degrees that is searched, in both directions.</summary>
    public const double MaxSkewDegrees = 15.0;

    /// <summary>The largest side in pixels of the plane the skew is estimated on.</summary>
    private const int AnalysisSide = 1024;

    /// <summary>The radius in analysis pixels of the window whose mean decides whether a pixel is a dark mark.</summary>
    private const int WindowRadius = 8;

    /// <summary>The number of levels a pixel must be darker than the mean of its window to be a dark mark.</summary>
    private const int ForegroundContrast = 10;

    /// <summary>The fewest dark marks that are estimated from, regardless of the size of the plane.</summary>
    private const int MinForegroundCount = 100;

    /// <summary>The fewest dark marks as a fraction of the pixels of the plane.</summary>
    private const double MinForegroundFraction = 0.0005;

    /// <summary>The largest searched angle in hundredths of a degree.</summary>
    private const int MaxHundredths = 1500;

    /// <summary>The number of fractional bits of the fixed-point cosine and sine.</summary>
    private const int TrigBits = 20;

    /// <summary>The shift that turns <c>y cos - x sin</c> in fixed point into whole-pixel bins (<c>floor(r)</c>).</summary>
    private const int BinShift = TrigBits;

    /// <summary>
    ///     Estimates the skew of the dominant near-horizontal structure of a surface (text lines, table rules, sketch
    ///     baselines, page edges).
    /// </summary>
    /// <param name="surface">The surface; it is not modified. Dark marks on a lighter ground are assumed (invert first for light marks on a dark ground).</param>
    /// <returns>
    ///     The skew from -15 to 15 degrees, positive when the lines descend to the right, with a confidence. The
    ///     estimate is made on an <b>analysis plane</b>: with <c>f = ceil(max(W, H) / 1024)</c> each analysis pixel is the
    ///     integer mean, <c>(sum + n / 2) / n</c>, of the <see cref="Surface.Luma"/> of its (possibly partial)
    ///     <c>f</c> by <c>f</c> block, so transparency is flattened over white. A pixel of the plane is a <b>dark mark</b>
    ///     when <c>sum - v * count &gt;= 10 * count</c> for the window of radius 8 around it (clipped to the plane; at least
    ///     10 levels darker than the local mean, exact integers), which is immune to uneven lighting. With fewer than
    ///     <c>max(100, 0.0005 * w * h)</c> marks the result is not estimated. Otherwise the <b>score</b> of an angle
    ///     <c>φ</c> (in hundredths of a degree, <c>k</c>) is the sum over the bins of the squared count of marks, the marks
    ///     being binned by <c>floor(y cos φ - x sin φ)</c> (constant along a line descending at angle <c>φ</c>; whole-pixel
    ///     bins, because half-pixel bins would leave every other bin empty at exactly 0 degrees and so favor that angle)
    ///     with the cosine and sine in 2<sup>20</sup> fixed point so that last-bit differences of <c>Math.Cos</c> cannot flip a
    ///     bin; the search is coarse to fine over <c>k</c> (every 1.00 degree from -15 to 15, then every 0.25 around the
    ///     best, then every 0.05), a higher score wins and equal scores prefer the smaller <c>|k|</c> and then the smaller
    ///     <c>k</c>. The confidence is <c>(best - median) / best</c> for the median of the 31 first-stage scores.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="surface"/> is <see langword="null"/>.</exception>
    public static SkewEstimate EstimateSkew(Surface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var (plane, width, height) = BuildAnalysisPlane(surface);
        var (xs, ys, count) = FindDarkMarks(plane, width, height);
        if (count < Math.Max(MinForegroundCount, MinForegroundFraction * width * height))
        {
            return new SkewEstimate(0, 0, false);
        }

        var search = new SkewSearch(xs, ys, count, width, height);

        // Stage 1: every degree from -15 to 15; the scores are kept for the confidence.
        var coarse = new long[(2 * (MaxHundredths / 100)) + 1];
        var bestK = 0;
        var bestScore = -1L;
        for (var i = 0; i < coarse.Length; i++)
        {
            var k = (i * 100) - MaxHundredths;
            coarse[i] = search.Score(k);
            if (PreferCandidate(coarse[i], k, bestScore, bestK))
            {
                bestScore = coarse[i];
                bestK = k;
            }
        }

        // Stage 2: every quarter degree within a degree of the best; stage 3: every 0.05 within a quarter degree.
        (bestK, bestScore) = search.Refine(bestK, bestScore, 25, 4);
        (bestK, bestScore) = search.Refine(bestK, bestScore, 5, 5);

        var confidence = 0.0;
        if (bestScore > 0)
        {
            Array.Sort(coarse);
            confidence = Math.Round((double)(bestScore - coarse[coarse.Length / 2]) / bestScore, 3, MidpointRounding.AwayFromZero);
        }

        return new SkewEstimate(bestK / 100.0, confidence, true);
    }

    /// <summary>
    ///     Decides whether an angle is a better candidate than the best one found so far.
    /// </summary>
    /// <param name="score">The score of the candidate.</param>
    /// <param name="hundredths">The candidate angle in hundredths of a degree.</param>
    /// <param name="bestScore">The score of the best angle so far.</param>
    /// <param name="bestHundredths">The best angle so far in hundredths of a degree.</param>
    /// <returns>
    ///     <see langword="true"/> when the score is higher; on equal scores when the angle is closer to 0; and on equal
    ///     scores and equal distance when the angle is the smaller (more negative) one.
    /// </returns>
    internal static bool PreferCandidate(long score, int hundredths, long bestScore, int bestHundredths)
    {
        if (score != bestScore)
        {
            return score > bestScore;
        }

        var distance = Math.Abs(hundredths);
        var bestDistance = Math.Abs(bestHundredths);
        return distance != bestDistance ? distance < bestDistance : hundredths < bestHundredths;
    }

    /// <summary>
    ///     Scores angles for the dark marks of an analysis plane, reusing one work array of bins.
    /// </summary>
    private sealed class SkewSearch
    {
        /// <summary>The columns of the dark marks.</summary>
        private readonly int[] _xs;

        /// <summary>The rows of the dark marks.</summary>
        private readonly int[] _ys;

        /// <summary>The number of dark marks.</summary>
        private readonly int _count;

        /// <summary>The work array of bins, cleared for each angle.</summary>
        private readonly int[] _bins;

        /// <summary>The offset added to a bin index so that it is never negative.</summary>
        private readonly int _offset;

        /// <summary>
        ///     Initializes a new instance of the <see cref="SkewSearch"/> class.
        /// </summary>
        /// <param name="xs">The columns of the dark marks.</param>
        /// <param name="ys">The rows of the dark marks.</param>
        /// <param name="count">The number of marks.</param>
        /// <param name="width">The width of the plane.</param>
        /// <param name="height">The height of the plane.</param>
        public SkewSearch(int[] xs, int[] ys, int count, int width, int height)
        {
            _xs = xs;
            _ys = ys;
            _count = count;
            _offset = (width / 2) + 2;
            _bins = new int[width + height + 8];
        }

        /// <summary>
        ///     Searches the angles around the best one at a fixed step.
        /// </summary>
        /// <param name="centerK">The best angle so far in hundredths of a degree.</param>
        /// <param name="centerScore">The score of the best angle so far.</param>
        /// <param name="step">The step in hundredths of a degree.</param>
        /// <param name="steps">The number of steps searched each side of the center.</param>
        /// <returns>The best angle and its score; angles beyond 15 degrees are not searched.</returns>
        public (int Hundredths, long Score) Refine(int centerK, long centerScore, int step, int steps)
        {
            var bestK = centerK;
            var bestScore = centerScore;
            for (var i = -steps; i <= steps; i++)
            {
                var k = centerK + (i * step);
                if (i == 0 || k < -MaxHundredths || k > MaxHundredths)
                {
                    continue;
                }

                var score = Score(k);
                if (PreferCandidate(score, k, bestScore, bestK))
                {
                    bestScore = score;
                    bestK = k;
                }
            }

            return (bestK, bestScore);
        }

        /// <summary>
        ///     Scores one angle: the sum of the squared counts of the bins of the projected marks.
        /// </summary>
        /// <param name="hundredths">The angle in hundredths of a degree.</param>
        /// <returns>The score.</returns>
        public long Score(int hundredths)
        {
            var radians = hundredths / 100.0 * Math.PI / 180.0;
            var cosQ = (long)Math.Round(Math.Cos(radians) * (1 << TrigBits), MidpointRounding.AwayFromZero);
            var sinQ = (long)Math.Round(Math.Sin(radians) * (1 << TrigBits), MidpointRounding.AwayFromZero);

            Array.Clear(_bins);
            long score = 0;
            for (var i = 0; i < _count; i++)
            {
                var bin = (int)(((_ys[i] * cosQ) - (_xs[i] * sinQ)) >> BinShift) + _offset;

                // (c + 1)^2 - c^2 = 2 c + 1, so the sum of squares grows by this much when a bin gains a mark.
                score += (2L * _bins[bin]) + 1;
                _bins[bin]++;
            }

            return score;
        }
    }

    /// <summary>
    ///     Builds the gray analysis plane of a surface.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <returns>The plane (one luma byte per pixel, row by row), its width, and its height.</returns>
    private static (byte[] Plane, int Width, int Height) BuildAnalysisPlane(Surface surface)
    {
        var factor = (Math.Max(surface.Width, surface.Height) + AnalysisSide - 1) / AnalysisSide;
        var width = (surface.Width + factor - 1) / factor;
        var height = (surface.Height + factor - 1) / factor;
        var plane = new byte[width * height];

        Parallel.For(0, height, y =>
        {
            var top = y * factor;
            var bottom = Math.Min(surface.Height, top + factor);
            for (var x = 0; x < width; x++)
            {
                var left = x * factor;
                var right = Math.Min(surface.Width, left + factor);
                long sum = 0;
                for (var sy = top; sy < bottom; sy++)
                {
                    var row = surface.GetRowSpan(sy);
                    for (var sx = left; sx < right; sx++)
                    {
                        sum += Surface.Luma(row[sx]);
                    }
                }

                var pixels = (long)(right - left) * (bottom - top);
                plane[(y * width) + x] = (byte)((sum + (pixels / 2)) / pixels);
            }
        });

        return (plane, width, height);
    }

    /// <summary>
    ///     Finds the dark marks of an analysis plane.
    /// </summary>
    /// <param name="plane">The plane.</param>
    /// <param name="width">The width of the plane.</param>
    /// <param name="height">The height of the plane.</param>
    /// <returns>The columns and rows of the marks (the first <c>Count</c> entries of the arrays) and their number.</returns>
    private static (int[] Xs, int[] Ys, int Count) FindDarkMarks(byte[] plane, int width, int height)
    {
        // The integral image: integral[(y + 1) * stride + (x + 1)] is the sum of plane[0..y, 0..x].
        var stride = width + 1;
        var integral = new long[stride * (height + 1)];
        for (var y = 0; y < height; y++)
        {
            long rowSum = 0;
            for (var x = 0; x < width; x++)
            {
                rowSum += plane[(y * width) + x];
                integral[((y + 1) * stride) + x + 1] = integral[(y * stride) + x + 1] + rowSum;
            }
        }

        var xs = new int[width * height];
        var ys = new int[width * height];
        var count = 0;
        for (var y = 0; y < height; y++)
        {
            var y0 = Math.Max(0, y - WindowRadius);
            var y1 = Math.Min(height - 1, y + WindowRadius);
            for (var x = 0; x < width; x++)
            {
                var x0 = Math.Max(0, x - WindowRadius);
                var x1 = Math.Min(width - 1, x + WindowRadius);
                var pixels = (long)(x1 - x0 + 1) * (y1 - y0 + 1);
                var sum = integral[((y1 + 1) * stride) + x1 + 1] - integral[(y0 * stride) + x1 + 1]
                    - integral[((y1 + 1) * stride) + x0] + integral[(y0 * stride) + x0];
                if (sum - (plane[(y * width) + x] * pixels) >= ForegroundContrast * pixels)
                {
                    xs[count] = x;
                    ys[count] = y;
                    count++;
                }
            }
        }

        return (xs, ys, count);
    }
}
