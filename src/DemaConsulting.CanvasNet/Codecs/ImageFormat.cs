namespace DemaConsulting.CanvasNet.Codecs;

/// <summary>
///     Identifies a raster image file format that the CanvasNet <c>Codecs</c> decoders can load.
/// </summary>
/// <remarks>
///     Use <see cref="ImageFormats.Detect(ReadOnlySpan{byte})"/> or
///     <see cref="ImageFormats.FromMediaType(string?)"/> to obtain a value and
///     <see cref="ImageDecoder"/> to decode it.
/// </remarks>
public enum ImageFormat
{
    /// <summary>Portable Network Graphics, decoded by <see cref="PngCodec"/>.</summary>
    Png,

    /// <summary>JPEG (JFIF/Exif), decoded by <see cref="JpegCodec"/>.</summary>
    Jpeg,

    /// <summary>Graphics Interchange Format (first frame), decoded by <see cref="GifCodec"/>.</summary>
    Gif,

    /// <summary>Windows bitmap, decoded by <see cref="BmpCodec"/>.</summary>
    Bmp,

    /// <summary>Tagged Image File Format, decoded by <see cref="TiffCodec"/>.</summary>
    Tiff,

    /// <summary>JPEG 2000 (JP2 or raw codestream), decoded by <see cref="Jpeg2000Codec"/>.</summary>
    Jpeg2000
}
