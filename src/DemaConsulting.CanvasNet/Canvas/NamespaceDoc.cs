namespace DemaConsulting.CanvasNet.Canvas;

/// <summary>
///     The <see cref="DemaConsulting.CanvasNet.Canvas"/> namespace provides a mutable, span-based 32-bit RGBA
///     pixel buffer (<see cref="Surface"/>) along with its pixel value type
///     (<see cref="Rgba32"/>), together with the CSS color parser (<see cref="CssColor"/>) that turns color text
///     into <see cref="Rgba32"/> values. These are the core pixel-storage primitives that codecs and
///     future drawing functionality build upon; no format-specific or drawing-specific members
///     are added to <see cref="Surface"/> itself, so consumers can be added independently.
/// </summary>
/// <remarks>
///     See <see cref="Surface"/> for the pixel buffer (cropping, resizing, compositing, color adjustment, and image
///     comparison are its members), <see cref="Rgba32"/> for one pixel, and <see cref="CssColor"/> for color text such
///     as <c>#336699</c> or <c>rebeccapurple</c>. A surface only holds pixels: see the
///     <see cref="DemaConsulting.CanvasNet.Codecs"/> namespace to load and save image files,
///     <see cref="DemaConsulting.CanvasNet.Drawing"/> and <see cref="DemaConsulting.CanvasNet.Rendering"/> to draw
///     vector paths, shapes, and text onto it, and <see cref="DemaConsulting.CanvasNet.ImageSources"/> to draw linked
///     pictures. The format packages (<c>DemaConsulting.CanvasNet.Svg</c>, <c>.Pdf</c>, <c>.Pptx</c>, <c>.Vsdx</c>)
///     rasterize their documents into a <see cref="Surface"/>.
/// </remarks>
/// <example>
///     Create a surface, fill it with a CSS color, set one pixel, make a thumbnail, and save it as a PNG file:
///     <code language="csharp">
///     using DemaConsulting.CanvasNet.Canvas;
///     using DemaConsulting.CanvasNet.Codecs;
///
///     using var surface = new Surface(64, 64);
///     surface.Clear(CssColor.Parse("#336699"));
///     surface[8, 8] = new Rgba32(255, 255, 255, 255);
///     using var thumbnail = surface.ResizeToFit(32, 32);
///     PngCodec.Save(thumbnail, "thumbnail.png");
///     </code>
/// </example>
internal static class NamespaceDoc
{
}
