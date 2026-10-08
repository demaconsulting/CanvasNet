namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio

/// <summary>
///     Reports a Visio page's declared name and size, in its internal unit, EMU (English Metric
///     Units; <c>914400</c> EMU per inch - the same constant and internal unit already used by
///     the sibling <c>DemaConsulting.CanvasNet.Pptx.PptxSlideSize</c>, reused here so any future
///     pixel-conversion helper is shared unchanged rather than duplicated).
/// </summary>
/// <param name="Name">
///     The page's declared name, from its <c>&lt;Page&gt;</c> element's <c>Name</c> attribute (or
///     its <c>NameU</c> attribute when <c>Name</c> is absent or empty - see
///     <c>VsdxDocument.Pages.cs</c>'s <c>ParsePage</c> for the exact fallback rule).
/// </param>
/// <param name="WidthEmu">
///     The page's declared width, in EMU, converted from its <c>&lt;PageSheet&gt;</c>'s
///     <c>PageWidth</c> cell (always expressed in inches, regardless of any <c>U=</c> attribute -
///     see the example below).
/// </param>
/// <param name="HeightEmu">
///     The page's declared height, in EMU, converted from its <c>&lt;PageSheet&gt;</c>'s
///     <c>PageHeight</c> cell, using the same inches-to-EMU conversion as <see cref="WidthEmu"/>.
/// </param>
/// <example>
///     A page whose <c>&lt;PageSheet&gt;</c> declares
///     <c>&lt;Cell N="PageWidth" V="8.26771653543307"/&gt;</c> (8.26771653543307 inches, A4 width)
///     converts to EMU as <c>8.26771653543307 * 914400 = 7,560,000</c> (rounded to the nearest
///     whole EMU) - this is <see cref="WidthEmu"/>'s exact value for that page. The raw <c>V</c>
///     value is always in inches even when a sibling, unrelated cell on the same
///     <c>&lt;PageSheet&gt;</c> carries a <c>U=</c> attribute (for example
///     <c>&lt;Cell N="PageScale" V="1" U="IN_F"/&gt;</c>) - <c>U</c> is a UI display-format hint
///     only and is never applied to <c>PageWidth</c>/<c>PageHeight</c>'s own <c>V</c> value.
/// </example>
public readonly record struct VsdxPageInfo(string Name, long WidthEmu, long HeightEmu);
