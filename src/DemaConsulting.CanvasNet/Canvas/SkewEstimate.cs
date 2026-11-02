namespace DemaConsulting.CanvasNet.Canvas;

/// <summary>
///     The estimated skew of a surface, from <see cref="Surface.EstimateSkew"/>.
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
/// <remarks>
///     The estimate is a measurement, not a decision: a very wide image of random content can reach a confidence
///     near <see cref="MinConfidence"/>, so a caller that corrects the skew should still be able to show the number.
///     <see cref="IsCorrectable"/> applies the thresholds that separate line structure from noise.
/// </remarks>
public readonly record struct SkewEstimate(double Degrees, double Confidence, bool Estimated)
{
    /// <summary>The smallest confidence at which an estimate is worth correcting.</summary>
    /// <remarks>
    ///     Pinned with synthetic images: ruled and text-like line structure scores 0.73 to 0.97, random strokes in every
    ///     direction up to about 0.30, and random noise up to about 0.30 (only a very wide image, 2000 by 300, gets that
    ///     high: the rectangle of the image itself favors horizontal profiles; a roughly square or 4:3 image gets 0.01 to
    ///     0.06).
    /// </remarks>
    public const double MinConfidence = 0.4;

    /// <summary>The smallest skew in degrees worth correcting; a smaller estimate leaves the image as it is.</summary>
    public const double MinCorrectedDegrees = 0.05;

    /// <summary>The largest skew in degrees that is searched, in both directions.</summary>
    public const double MaxDegrees = 15.0;

    /// <summary>
    ///     Gets a value indicating whether the estimate is reliable and large enough to be worth correcting.
    /// </summary>
    /// <value>
    ///     <see langword="true"/> when <see cref="Estimated"/> is <see langword="true"/>, <c>|Degrees|</c> is at least
    ///     <see cref="MinCorrectedDegrees"/>, and <see cref="Confidence"/> is at least <see cref="MinConfidence"/>.
    /// </value>
    public bool IsCorrectable => Estimated && Math.Abs(Degrees) >= MinCorrectedDegrees && Confidence >= MinConfidence;

    /// <summary>
    ///     Gets the clockwise rotation in degrees, 0 to below 360, that levels the image.
    /// </summary>
    /// <value>
    ///     <c>360 - Degrees</c> for a positive skew and <c>-Degrees</c> otherwise, the angle to give to
    ///     <see cref="Surface.RotateArbitrary(double, Rgba32, bool)"/>.
    /// </value>
    public double CorrectionDegrees => Degrees > 0 ? 360 - Degrees : -Degrees;
}
