namespace DemaConsulting.CanvasNet.Codecs;

// cspell:ignore rasterizing rasterize

/// <summary>
///     The <see cref="DemaConsulting.CanvasNet.Codecs"/> namespace provides codecs for loading and saving pixel
///     data in common image file formats: uncompressed Windows BMP (<see cref="BmpCodec"/>), a
///     restricted, dependency-free subset of PNG (<see cref="PngCodec"/>), a restricted,
///     dependency-free subset of TIFF (<see cref="TiffCodec"/>), a restricted,
///     dependency-free subset of baseline JPEG (<see cref="JpegCodec"/>), a restricted,
///     dependency-free, decode-only, first-frame-only subset of GIF (<see cref="GifCodec"/>), and
///     a restricted, dependency-free, decode/rasterize-only subset of SVG
///     (<see cref="SvgCodec"/>). The five raster codecs (<see cref="BmpCodec"/>,
///     <see cref="PngCodec"/>, <see cref="TiffCodec"/>, <see cref="JpegCodec"/>, and
///     <see cref="GifCodec"/>) each convert to and from a
///     <see cref="DemaConsulting.CanvasNet.Canvas.Surface"/> pixel buffer using only its existing
///     public API - no format-specific members are added to
///     <see cref="DemaConsulting.CanvasNet.Canvas.Surface"/> itself, so new codecs can be added
///     independently in the future. Unlike those five, <see cref="SvgCodec"/> only
///     decodes/rasterizes SVG vector artwork into a
///     <see cref="DemaConsulting.CanvasNet.Canvas.Surface"/> - it has no <c>Save</c> method, since
///     rasterizing a textual vector document is a fundamentally different (and non-invertible)
///     operation from encoding a fixed-size raster format, so it has no encode/save direction.
/// </summary>
internal static class NamespaceDoc
{
}
