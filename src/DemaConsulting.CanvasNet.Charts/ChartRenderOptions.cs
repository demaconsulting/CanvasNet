using System.Collections.ObjectModel;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Fonts;

namespace DemaConsulting.CanvasNet.Charts;

/// <summary>
///     Groups chart-rendering configuration accepted by
///     <see cref="ChartRenderer.Render(Chart, int, int, ChartRenderOptions?)"/> and
///     <see cref="ChartRenderer.Render(Chart, float, float, float, ChartRenderOptions?)"/>.
/// </summary>
/// <remarks>
///     This type exists as a single, extensible options parameter rather than a growing list of
///     individual <c>Render</c> arguments, so future rendering options can be added as new
///     <see langword="init"/>-only properties without another breaking change to
///     <see cref="ChartRenderer"/>'s <c>Render</c> signatures - mirroring
///     <c>DemaConsulting.CanvasNet.Pptx.PptxRenderOptions</c>'s own rationale exactly. It is a
///     <see langword="sealed"/> class with <see langword="init"/>-only properties (rather than a
///     positional record) so that object-initializer call sites such as
///     <c>new ChartRenderOptions { BackgroundColor = ... }</c> keep compiling unchanged whenever a
///     new option is added later.
/// </remarks>
public sealed class ChartRenderOptions
{
    /// <summary>
    ///     Creates a new <see cref="ChartRenderOptions"/> instance with all options set to their
    ///     documented defaults (opaque white background, the bundled fallback font, and the
    ///     documented default font sizes).
    /// </summary>
    public ChartRenderOptions()
    {
    }

    /// <summary>
    ///     The default <see cref="ChartRenderOptions"/> instance, used by <c>Render</c> whenever a
    ///     caller passes <see langword="null"/> (or omits the <c>options</c> parameter entirely).
    /// </summary>
    public static readonly ChartRenderOptions Default = new();

    /// <summary>
    ///     The color the rendered <see cref="Surface"/> is cleared to before any chart content is
    ///     painted. Defaults to opaque white (<c>R=255, G=255, B=255, A=255</c>).
    /// </summary>
    /// <remarks>
    ///     Set this to a fully transparent color (<c>new Rgba32(0, 0, 0, 0)</c>) to reproduce a
    ///     fully transparent background, matching <c>PptxRenderOptions.BackgroundColor</c>'s own
    ///     documented convention.
    /// </remarks>
    public Rgba32 BackgroundColor { get; init; } = new(255, 255, 255, 255);

    /// <summary>
    ///     An explicit font used for every piece of text <see cref="ChartRenderer"/> paints
    ///     (title, axis labels, legend labels, data labels), or <see langword="null"/> (the
    ///     default) to use the bundled Liberation Sans Regular fallback font
    ///     (<see cref="SystemFontCatalog.LoadBundledFallback"/>) - the same zero-OS-dependency,
    ///     process-lifetime-cached font family core <c>CanvasNet</c> document renderers already
    ///     fall back to when no more specific font is available, chosen here as the deliberate
    ///     default (rather than requiring every caller to supply a font) because a chart has no
    ///     document-level font resource to inherit from the way a PPTX/PDF page does.
    /// </summary>
    public TrueTypeFont? Font { get; init; }

    /// <summary>
    ///     The font size, in pixels, used to paint <see cref="Chart.Title"/>'s text when
    ///     <see cref="ChartTitle.FontSize"/> is <see langword="null"/>. Defaults to <c>18</c>.
    /// </summary>
    public float TitleFontSize { get; init; } = 18f;

    /// <summary>
    ///     The font size, in pixels, used to paint category-axis and value-axis tick labels.
    ///     Defaults to <c>11</c>.
    /// </summary>
    public float AxisFontSize { get; init; } = 11f;

    /// <summary>
    ///     The font size, in pixels, used to paint legend entry labels. Defaults to <c>11</c>.
    /// </summary>
    public float LegendFontSize { get; init; } = 11f;

    /// <summary>
    ///     The font size, in pixels, used to paint per-point data labels
    ///     (<see cref="ChartSeries.PointLabels"/>). Defaults to <c>10</c>.
    /// </summary>
    public float DataLabelFontSize { get; init; } = 10f;

    /// <summary>
    ///     An explicit categorical color sequence used in place of
    ///     <see cref="ChartColorPalette.Default"/> whenever a series/point has no more specific
    ///     color of its own and <see cref="Chart.ColorPalette"/> is <see langword="null"/>, or
    ///     <see langword="null"/> (the default) to defer entirely to
    ///     <see cref="Chart.ColorPalette"/>/<see cref="ChartColorPalette.Default"/>. See
    ///     <see cref="ChartColorPalette"/>'s own remarks for the full color-resolution order and
    ///     index-wrapping behavior. Must not be empty when non-null - an empty override is
    ///     treated the same as <see langword="null"/> (falls through to
    ///     <see cref="ChartColorPalette.Default"/>), since an empty sequence can resolve no
    ///     color at all.
    /// </summary>
    public IReadOnlyList<Rgba32>? ColorPalette
    {
        get => _colorPalette;
        init => _colorPalette = value is null ? null : new ReadOnlyCollection<Rgba32>([.. value]);
    }

    /// <summary>
    ///     The backing field for <see cref="ColorPalette"/>. A defensive copy is made and wrapped
    ///     in a <see cref="ReadOnlyCollection{T}"/> in the <see langword="init"/> accessor above,
    ///     so neither mutating the caller's original list/array after construction (or reusing a
    ///     single mutable list across several <see cref="ChartRenderOptions"/> instances), nor
    ///     casting <see cref="ColorPalette"/> back to <c>Rgba32[]</c>/<c>IList&lt;Rgba32&gt;</c>
    ///     and mutating it that way, can retroactively change palette resolution for an
    ///     already-constructed, supposedly-immutable options instance - matching the same
    ///     defensive-copy-plus-read-only-wrapper pattern already used by <see cref="Chart.ColorPalette"/>
    ///     and <see cref="ChartSeries.PointColors"/>.
    /// </summary>
    private readonly IReadOnlyList<Rgba32>? _colorPalette;
}
