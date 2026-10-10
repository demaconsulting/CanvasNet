namespace DemaConsulting.CanvasNet.Codecs;

/// <summary>
///     A decoded JPEG 2000 image: 8-bit interleaved colour samples plus an optional separate alpha plane.
/// </summary>
/// <param name="Width">The image width in pixels.</param>
/// <param name="Height">The image height in pixels.</param>
/// <param name="ColorSpace">The colour space of the colour channels.</param>
/// <param name="ColorChannelCount">The number of colour channels (1, 3 or 4).</param>
/// <param name="ColorSamples">
///     The colour samples scaled to 8 bits, interleaved per pixel, row by row, with
///     <paramref name="ColorChannelCount"/> bytes per pixel.
/// </param>
/// <param name="AlphaSamples">
///     The opacity samples scaled to 8 bits, one byte per pixel, or <see langword="null"/> when the image has no alpha channel.
/// </param>
/// <param name="AlphaPremultiplied">Whether the colour samples are premultiplied by the alpha samples.</param>
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
}
