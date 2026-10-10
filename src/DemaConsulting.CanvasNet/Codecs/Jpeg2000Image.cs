namespace DemaConsulting.CanvasNet.Codecs;

/// <summary>
///     A decoded JPEG 2000 image: 8-bit interleaved color samples plus an optional separate alpha plane.
/// </summary>
/// <param name="Width">The image width in pixels.</param>
/// <param name="Height">The image height in pixels.</param>
/// <param name="ColorSpace">The color space of the color channels.</param>
/// <param name="ColorChannelCount">The number of color channels (1, 3 or 4).</param>
/// <param name="ColorSamples">
///     The color samples scaled to 8 bits, interleaved per pixel, row by row, with
///     <paramref name="ColorChannelCount"/> bytes per pixel.
/// </param>
/// <param name="AlphaSamples">
///     The opacity samples scaled to 8 bits, one byte per pixel, or <see langword="null"/> when the image has no alpha channel.
/// </param>
/// <param name="AlphaPremultiplied">Whether the color samples are premultiplied by the alpha samples.</param>
/// <param name="IccProfile">The embedded ICC profile, if the file carried one. The profile is reported but never applied.</param>
public sealed record Jpeg2000Image(
    int Width,
    int Height,
    Jpeg2000ColorSpace ColorSpace,
    int ColorChannelCount,
    byte[] ColorSamples,
    byte[]? AlphaSamples,
    bool AlphaPremultiplied,
    byte[]? IccProfile)
{
    /// <summary>Gets a value indicating whether the image carries an alpha channel.</summary>
    public bool HasAlpha => AlphaSamples is not null;

    /// <summary>
    ///     Gets the bit depth of the first color channel's source component before scaling to 8 bits
    ///     (for a palette-mapped image, the depth of the palette index component).
    /// </summary>
    public int BitDepth { get; init; } = 8;

    /// <summary>
    ///     Gets a value indicating whether the JP2 file's palette (<c>pclr</c>/<c>cmap</c>) was applied, so
    ///     <see cref="ColorSamples"/> hold palette colors rather than raw component samples.
    /// </summary>
    public bool HasPalette { get; init; }
}
