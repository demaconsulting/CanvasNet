// cspell:ignore psnr rmse ssim
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Tool.Imaging;

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
///     The command-line side of comparing two images: the defaults of the options and the pass/fail thresholds.
/// </summary>
/// <remarks>
///     <para>
///         The measures, the grid, and the pictures are computed by the core library
///         (<see cref="Surface.Compare"/>, <see cref="Surface.CreateDiff"/>, <see cref="Surface.CreateOverlay"/>,
///         <see cref="Surface.CreateSideBySide"/>). This unit holds what only a command line needs: the default value
///         of each option, and the thresholds (<c>--threshold name=limit</c>) that turn a result into a pass or a fail,
///         with the names under which the metrics are written on the command line and in results.
///     </para>
/// </remarks>
internal static class ImageComparison
{
    /// <summary>The default tolerance of the differing-pixel count: a channel difference above 8 counts.</summary>
    public const int DefaultTolerance = 8;

    /// <summary>The default number of grid columns.</summary>
    public const int DefaultGridColumns = 4;

    /// <summary>The default number of grid rows.</summary>
    public const int DefaultGridRows = 4;

    /// <summary>The default amplification of a difference image.</summary>
    public const double DefaultGain = 4;

    /// <summary>The default weight of the second image in an overlay.</summary>
    public const double DefaultOverlayWeight = 0.5;

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
}
