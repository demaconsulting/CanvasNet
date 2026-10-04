namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore pptx srcrect

/// <summary>
///     A picture shape's resolved <c>&lt;a:srcRect&gt;</c> crop: the fractional border cropped
///     away from each edge of the source image before it is sampled into the shape's placement
///     rectangle, produced by <see cref="PptxDocument.ResolveSrcRect"/> and consumed by
///     <see cref="PptxDocument.PaintPicture"/>.
/// </summary>
/// <param name="Left">
///     The fraction of the source image's width cropped away from its left edge (<c>&lt;a:srcRect
///     l="..."/&gt;</c>, OOXML's own 1/100000-of-a-percent unit divided by <c>100000</c>), where
///     <c>0</c> means "no crop on this edge".
/// </param>
/// <param name="Top">The fraction cropped away from the top edge (<c>t</c>), same unit as <see cref="Left"/>.</param>
/// <param name="Right">The fraction cropped away from the right edge (<c>r</c>), same unit as <see cref="Left"/>.</param>
/// <param name="Bottom">The fraction cropped away from the bottom edge (<c>b</c>), same unit as <see cref="Left"/>.</param>
internal sealed record PptxSrcRect(float Left, float Top, float Right, float Bottom);
