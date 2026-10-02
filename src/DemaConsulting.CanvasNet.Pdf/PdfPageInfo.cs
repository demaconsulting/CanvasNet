namespace DemaConsulting.CanvasNet.Pdf;

/// <summary>
///     Reports a single PDF page's displayed (rotation-adjusted) size and effective rotation.
/// </summary>
/// <param name="Width">
///     The page's displayed width, in points, after the effective <see cref="Rotation"/> has
///     already been applied. This is <em>not</em> necessarily the raw <c>/MediaBox</c> width:
///     when <see cref="Rotation"/> is <c>90</c> or <c>270</c>, the raw <c>/MediaBox</c> width and
///     height are swapped to compute this value, since the page is displayed sideways.
/// </param>
/// <param name="Height">
///     The page's displayed height, in points, after the effective <see cref="Rotation"/> has
///     already been applied. This is <em>not</em> necessarily the raw <c>/MediaBox</c> height:
///     when <see cref="Rotation"/> is <c>90</c> or <c>270</c>, the raw <c>/MediaBox</c> width and
///     height are swapped to compute this value, since the page is displayed sideways.
/// </param>
/// <param name="Rotation">
///     The page's normalized, effective, clockwise viewing rotation in degrees: always one of
///     <c>0</c>, <c>90</c>, <c>180</c>, or <c>270</c>. This is the page's own <c>/Rotate</c> value
///     if declared, otherwise the nearest declaring ancestor's inherited value, otherwise
///     <c>0</c> - already normalized modulo 360 - and is exactly the rotation already applied to
///     produce <see cref="Width"/>/<see cref="Height"/> from the raw <c>/MediaBox</c>. It is
///     exposed separately (rather than only folded into <see cref="Width"/>/<see cref="Height"/>)
///     for documentation/debugging transparency, and because constructing a page's rendering
///     transform in a later phase requires knowing the rotation angle itself, not just the
///     resulting swapped dimensions.
/// </param>
public readonly record struct PdfPageInfo(int Width, int Height, int Rotation);
