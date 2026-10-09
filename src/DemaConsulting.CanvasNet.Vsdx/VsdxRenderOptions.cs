using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio Rgba

/// <summary>
///     Groups page-rendering configuration accepted by
///     <see cref="VsdxDocument.Render(int, int, int, VsdxRenderOptions?)"/> and
///     <see cref="VsdxDocument.Render(int, int, VsdxRenderOptions?)"/>.
/// </summary>
/// <remarks>
///     This type exists as a single, extensible options parameter rather than a growing list of
///     individual <c>Render</c> arguments, so future rendering options can be added as new
///     <see langword="init"/>-only properties without another breaking change to
///     <see cref="VsdxDocument"/>'s <c>Render</c> signatures - mirroring
///     <c>DemaConsulting.CanvasNet.Pptx.PptxRenderOptions</c>'s own rationale exactly. It is a
///     <see langword="sealed"/> class with <see langword="init"/>-only properties (rather than a
///     positional record) so that object-initializer call sites such as
///     <c>new VsdxRenderOptions { BackgroundColor = ... }</c> keep compiling unchanged whenever a
///     new option is added later.
/// </remarks>
public sealed class VsdxRenderOptions
{
    /// <summary>
    ///     Creates a new <see cref="VsdxRenderOptions"/> instance with
    ///     <see cref="BackgroundColor"/> set to its documented default (opaque white).
    /// </summary>
    public VsdxRenderOptions()
    {
    }

    /// <summary>
    ///     The default <see cref="VsdxRenderOptions"/> instance, used by <c>Render</c> whenever a
    ///     caller passes <see langword="null"/> (or omits the <c>options</c> parameter entirely) -
    ///     opaque white (see <see cref="BackgroundColor"/>'s own default).
    /// </summary>
    public static readonly VsdxRenderOptions Default = new();

    /// <summary>
    ///     The color the rendered <see cref="Surface"/> is cleared to before the page's shape tree
    ///     is painted. Defaults to opaque white (<c>R=255, G=255, B=255, A=255</c>), matching a
    ///     printed page's own "blank paper" expectation rather than <see cref="Surface"/>'s own
    ///     transparent-black default.
    /// </summary>
    /// <remarks>
    ///     Set this to a fully transparent color (<c>new Rgba32(0, 0, 0, 0)</c>) to reproduce a
    ///     fully transparent background. Unlike <c>PptxRenderOptions.BackgroundColor</c>, no
    ///     further background resolution is performed: VisioML's resolved page/shape model
    ///     exposes no page-background-fill concept of its own, so this option alone fully
    ///     determines the rendered background.
    /// </remarks>
    public Rgba32 BackgroundColor { get; init; } = new(255, 255, 255, 255);
}
