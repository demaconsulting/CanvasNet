namespace DemaConsulting.CanvasNet.Codecs;

/// <summary>
///     The color space of the color channels of a decoded <see cref="Jpeg2000Image"/>.
/// </summary>
public enum Jpeg2000ColorSpace
{
    /// <summary>The color space is not one of the well-known ones; the channel count describes the layout.</summary>
    Unknown = 0,

    /// <summary>A single gray channel.</summary>
    Gray = 1,

    /// <summary>Three channels in red, green, blue order (sYCC data is converted to this).</summary>
    Srgb = 2,

    /// <summary>Four channels in cyan, magenta, yellow, black order.</summary>
    Cmyk = 3,
}
