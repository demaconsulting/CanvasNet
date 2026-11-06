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
///     method. <see cref="ImageFormats"/> and <see cref="ImageDecoder"/> add a format-neutral
///     facade over all six codecs: recognize an image's <see cref="ImageFormat"/> from its signature
///     or media type, and decode it without a per-format switch. SVG rasterization is no longer part of this
///     namespace - it now lives in the separate <c>DemaConsulting.CanvasNet.Svg</c> package's
///     <c>DemaConsulting.CanvasNet.Svg</c> namespace.
/// </summary>
/// <remarks>
///     See <see cref="ImageDecoder"/> to decode a file without knowing its format, <see cref="ImageFormats"/> to
///     recognize one, and the codec of each format (for example <see cref="PngCodec"/>) to load or save it. Every codec
///     works on a <see cref="DemaConsulting.CanvasNet.Canvas.Surface"/>, which has the editing members. The vector and
///     document formats are separate packages: <c>DemaConsulting.CanvasNet.Svg</c>, <c>.Pdf</c>, <c>.Pptx</c>, and
///     <c>.Vsdx</c> each rasterize into a surface, and <c>DemaConsulting.CanvasNet.Viewer</c> reads all of them with one
///     call. <see cref="DemaConsulting.CanvasNet.ImageSources"/> is the contract that lets one document embed pictures
///     of another format.
/// </remarks>
/// <example>
///     Convert a picture to another format, and load a file whose format is not known in advance:
///     <code language="csharp">
///     using DemaConsulting.CanvasNet.Canvas;
///     using DemaConsulting.CanvasNet.Codecs;
///
///     using var photo = PngCodec.Load("photo.png");
///     JpegCodec.Save(photo, "photo.jpg");
///
///     using var stream = File.OpenRead("picture.dat");
///     using var picture = ImageDecoder.Load(stream);   // recognizes PNG, JPEG, BMP, TIFF, GIF, and JPEG 2000
///     Console.WriteLine($"{picture.Width} x {picture.Height}");
///     </code>
/// </example>
internal static class NamespaceDoc
{
}
