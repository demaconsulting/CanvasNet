namespace DemaConsulting.CanvasNet.Codecs;

// cspell:ignore rasterizing rasterize

/// <summary>
///     The <see cref="DemaConsulting.CanvasNet.Codecs"/> namespace provides codecs for loading and saving pixel
///     data in common image file formats: uncompressed Windows BMP (<see cref="BmpCodec"/>), a
///     restricted, dependency-free subset of PNG (<see cref="PngCodec"/>), a restricted,
///     dependency-free subset of TIFF (<see cref="TiffCodec"/>), a restricted,
///     dependency-free subset of baseline JPEG (<see cref="JpegCodec"/>), a restricted,
///     dependency-free, decode-only, first-frame-only subset of GIF (<see cref="GifCodec"/>), and a
///     dependency-free, decode-only JPEG 2000 Part 1 decoder (<see cref="Jpeg2000Codec"/>).
///     Four raster codecs (<see cref="BmpCodec"/>, <see cref="PngCodec"/>,
///     <see cref="TiffCodec"/>, and <see cref="JpegCodec"/>) each convert to and from a
///     <see cref="DemaConsulting.CanvasNet.Canvas.Surface"/> pixel buffer using only its
///     existing public API - no format-specific members are added to
///     <see cref="DemaConsulting.CanvasNet.Canvas.Surface"/> itself, so new codecs can be added
///     independently in the future. Unlike those four, <see cref="GifCodec"/> and
///     <see cref="Jpeg2000Codec"/> are decode-only: <see cref="GifCodec"/> loads a
///     <see cref="DemaConsulting.CanvasNet.Canvas.Surface"/> from the first frame of a
///     GIF file and <see cref="Jpeg2000Codec"/> from a JPEG 2000 image, and neither has a <c>Save</c>
///     method. SVG rasterization is no longer part of this
///     namespace - it now lives in the separate <c>DemaConsulting.CanvasNet.Svg</c> package's
///     <c>DemaConsulting.CanvasNet.Svg</c> namespace.
/// </summary>
internal static class NamespaceDoc
{
}
