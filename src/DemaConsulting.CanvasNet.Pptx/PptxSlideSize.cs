namespace DemaConsulting.CanvasNet.Pptx;

/// <summary>
///     Reports a presentation's slide size in its native OOXML unit, EMU (English Metric Units;
///     914400 EMU per inch).
/// </summary>
/// <param name="WidthEmu">The slide width, in EMU, from <c>&lt;p:sldSz cx="..."&gt;</c>.</param>
/// <param name="HeightEmu">The slide height, in EMU, from <c>&lt;p:sldSz cy="..."&gt;</c>.</param>
public readonly record struct PptxSlideSize(long WidthEmu, long HeightEmu);
