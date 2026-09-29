namespace DemaConsulting.CanvasNet.Svg;

// cspell:ignore rasterizing rasterize

/// <summary>
///     The <see cref="DemaConsulting.CanvasNet.Svg"/> namespace provides a restricted,
///     dependency-free, decode/rasterize-only codec for a common real-world subset of SVG
///     (Scalable Vector Graphics) documents: <see cref="SvgCodec"/>. Unlike the four raster
///     codecs in the core <c>DemaConsulting.CanvasNet</c> package's
///     <see cref="DemaConsulting.CanvasNet.Codecs"/> namespace (which each convert to and
///     from a <see cref="DemaConsulting.CanvasNet.Canvas.Surface"/> pixel buffer using only
///     its existing public API), <see cref="SvgCodec"/> only decodes/rasterizes SVG vector
///     artwork into a <see cref="DemaConsulting.CanvasNet.Canvas.Surface"/> - it has no
///     <c>Save</c> method, since rasterizing a textual vector document is a fundamentally
///     different (and non-invertible) operation from encoding a fixed-size raster format, so
///     it has no encode/save direction either. This namespace is distributed as the separate
///     <c>DemaConsulting.CanvasNet.Svg</c> NuGet package, which references the core
///     <c>DemaConsulting.CanvasNet</c> package.
/// </summary>
internal static class NamespaceDoc
{
}
