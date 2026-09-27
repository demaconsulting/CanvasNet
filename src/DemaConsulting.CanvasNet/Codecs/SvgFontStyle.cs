namespace DemaConsulting.CanvasNet.Codecs;

/// <summary>
///     Identifies the slant style of a registered <see cref="SvgFontFace"/>, used by
///     <see cref="SvgCodec"/> when matching a <c>text</c> element's <c>font-style</c>
///     presentation attribute against the caller-supplied faces registered for its
///     <c>font-family</c>.
/// </summary>
/// <remarks>
///     Deliberately narrowed from CSS/SVG's three-way <c>font-style</c> keyword set
///     (<c>normal</c>/<c>italic</c>/<c>oblique</c>) to just two values. The reported real-world
///     use case this feature exists for - "bold titles and italic keywords" in a diagram label -
///     never needs to distinguish a true italic face (a font designer's own hand-drawn slanted
///     glyphs) from an <c>oblique</c> one (a mechanically-slanted upright face); both are simply
///     "the slanted variant" from a caller's point of view. <see cref="SvgCodec"/>'s
///     <c>font-style</c> parser (see its remarks) therefore maps the <c>oblique</c> keyword onto
///     <see cref="Italic"/> rather than rejecting it or adding a third enum member, exactly
///     mirroring how <see cref="Drawing.FillRule"/> and <see cref="Drawing.LineCap"/>/
///     <see cref="Drawing.LineJoin"/> are each declared as their own small, top-level public
///     enum alongside <see cref="Drawing"/>'s other shared value types.
/// </remarks>
public enum SvgFontStyle
{
    /// <summary>The upright, non-slanted variant (the SVG/CSS <c>font-style</c> default).</summary>
    Normal,

    /// <summary>
    ///     The slanted variant - matches an SVG/CSS <c>font-style</c> value of either
    ///     <c>italic</c> or <c>oblique</c>; see this enum's remarks for why the two keywords are
    ///     not distinguished.
    /// </summary>
    Italic
}
