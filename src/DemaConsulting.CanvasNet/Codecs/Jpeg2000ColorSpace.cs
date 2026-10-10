namespace DemaConsulting.CanvasNet.Codecs;

/// <summary>
///     The colour space of the colour channels of a decoded <see cref="Jpeg2000Image"/>.
/// </summary>
public enum Jpeg2000ColorSpace
{
    /// <summary>The colour space is not one of the well-known ones; the channel count describes the layout.</summary>
    Unknown = 0,

    /// <summary>A single grey channel.</summary>
    Gray = 1,

    /// <summary>Three channels in red, green, blue order (sYCC data is converted to this).</summary>
    Srgb = 2,

    /// <summary>Four channels in cyan, magenta, yellow, black order.</summary>
    Cmyk = 3,
}
