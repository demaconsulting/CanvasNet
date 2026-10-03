using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Pdf;

/// <summary>
///     Groups page-rendering configuration accepted by
///     <see cref="PdfDocument.Render(int, int, int, PdfRenderOptions?)"/> and
///     <see cref="PdfDocument.Render(int, float, PdfRenderOptions?)"/>.
/// </summary>
/// <remarks>
///     This type exists as a single, extensible options parameter rather than a growing list of
///     individual <c>Render</c> arguments, so future rendering options can be added as new
///     <see langword="init"/>-only properties without another breaking change to
///     <see cref="PdfDocument"/>'s <c>Render</c> signatures. It is a <see langword="sealed"/>
///     class with <see langword="init"/>-only properties (rather than a positional record) so
///     that object-initializer call sites such as
///     <c>new PdfRenderOptions { BackgroundColor = ... }</c> keep compiling unchanged whenever a
///     new option is added later.
/// </remarks>
public sealed class PdfRenderOptions
{
    /// <summary>
    ///     The default <see cref="PdfRenderOptions"/> instance, used by <c>Render</c> whenever a
    ///     caller passes <see langword="null"/> (or omits the <c>options</c> parameter entirely) -
    ///     opaque white (see <see cref="BackgroundColor"/>'s own default).
    /// </summary>
    public static readonly PdfRenderOptions Default = new();

    /// <summary>
    ///     The color the rendered <see cref="Surface"/> is cleared to before the page's
    ///     <c>/Contents</c> content stream (if any) is executed. Defaults to opaque white
    ///     (<c>R=255, G=255, B=255, A=255</c>).
    /// </summary>
    /// <remarks>
    ///     Set this to a fully transparent color (<c>new Rgba32(0, 0, 0, 0)</c>) to reproduce the
    ///     fully transparent background that <c>Render</c> always produced before this option
    ///     existed.
    /// </remarks>
    public Rgba32 BackgroundColor { get; init; } = new(255, 255, 255, 255);
}
