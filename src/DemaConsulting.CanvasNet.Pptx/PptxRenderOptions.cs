using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Pptx;

/// <summary>
///     Groups slide-rendering configuration accepted by
///     <see cref="PptxDocument.Render(int, int, int, PptxRenderOptions?)"/> and
///     <see cref="PptxDocument.Render(int, float, PptxRenderOptions?)"/>.
/// </summary>
/// <remarks>
///     This type exists as a single, extensible options parameter rather than a growing list of
///     individual <c>Render</c> arguments, so future rendering options can be added as new
///     <see langword="init"/>-only properties without another breaking change to
///     <see cref="PptxDocument"/>'s <c>Render</c> signatures - mirroring
///     <c>DemaConsulting.CanvasNet.Pdf.PdfRenderOptions</c>'s own rationale exactly. It is a
///     <see langword="sealed"/> class with <see langword="init"/>-only properties (rather than a
///     positional record) so
///     that object-initializer call sites such as
///     <c>new PptxRenderOptions { BackgroundColor = ... }</c> keep compiling unchanged whenever a
///     new option is added later.
/// </remarks>
public sealed class PptxRenderOptions
{
    /// <summary>
    ///     The default <see cref="PptxRenderOptions"/> instance, used by <c>Render</c> whenever a
    ///     caller passes <see langword="null"/> (or omits the <c>options</c> parameter entirely) -
    ///     opaque white (see <see cref="BackgroundColor"/>'s own default).
    /// </summary>
    public static readonly PptxRenderOptions Default = new();

    /// <summary>
    ///     The color the rendered <see cref="Surface"/> is cleared to before the slide's shape
    ///     tree is painted. Defaults to opaque white (<c>R=255, G=255, B=255, A=255</c>).
    /// </summary>
    /// <remarks>
    ///     Set this to a fully transparent color (<c>new Rgba32(0, 0, 0, 0)</c>) to reproduce a
    ///     fully transparent background. A slide's own <c>&lt;p:bg&gt;</c> background fill (or,
    ///     when the slide declares none, its layout's/master's own <c>&lt;p:bg&gt;</c>) now takes
    ///     priority over this option when declared - painted across the full slide after this
    ///     clear and before the shape-tree walk (see
    ///     <see cref="PptxDocument.Render(int, int, int, PptxRenderOptions?)"/>'s remarks and
    ///     <see cref="PptxDocument.ResolveSlideBackgroundFill"/>). This option remains the
    ///     fallback - and the base clear color - only when none of slide/layout/master declare a
    ///     <c>&lt;p:bg&gt;</c> at all.
    /// </remarks>
    public Rgba32 BackgroundColor { get; init; } = new(255, 255, 255, 255);
}
