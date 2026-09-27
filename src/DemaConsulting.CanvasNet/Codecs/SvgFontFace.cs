using DemaConsulting.CanvasNet.Fonts;

namespace DemaConsulting.CanvasNet.Codecs;

/// <summary>
///     Represents one registered font face - a loaded <see cref="Fonts.TrueTypeFont"/> paired
///     with the <c>font-weight</c>/<c>font-style</c> it was authored to represent - within a
///     single font-family's list of faces supplied to <see cref="SvgCodec.LoadWithFontFaces(Stream, int, int, IReadOnlyDictionary{string, IReadOnlyList{SvgFontFace}}?)"/>.
/// </summary>
/// <remarks>
///     A small, immutable data carrier with no behavior beyond its record-struct value equality,
///     modeled directly on <see cref="ImageInfo"/>'s style: it exists purely so a caller can
///     register more than one <see cref="Fonts.TrueTypeFont"/> per family name - for example a
///     regular, a bold, and an italic variant of the same family - and have <see cref="SvgCodec"/>
///     pick the closest-matching face for each <c>text</c> element's own cascaded
///     <c>font-weight</c>/<c>font-style</c>, instead of every element in a family always
///     resolving to the same single font regardless of its requested weight/style. The legacy
///     <see cref="SvgCodec.Load(Stream, int, int, IReadOnlyDictionary{string, TrueTypeFont}?)"/>
///     overload, which accepts a single <see cref="Fonts.TrueTypeFont"/> per family, is
///     internally translated into exactly one <see cref="SvgFontFace"/> per family - with the
///     default <see cref="Weight"/> (<c>400</c>, "normal") and <see cref="Style"/>
///     (<see cref="SvgFontStyle.Normal"/>) - so that overload's behavior (always selecting that
///     single registered font, regardless of a requested <c>font-weight</c>/<c>font-style</c>)
///     is unchanged.
/// </remarks>
/// <param name="Font">The loaded font this face renders glyphs from.</param>
/// <param name="Weight">
///     The CSS-numeric <c>font-weight</c> this face represents, conventionally in the well-known
///     <c>100</c>-<c>900</c> range (<c>400</c> is "normal", <c>700</c> is "bold"), though any
///     positive integer is accepted, matching CSS's own numeric-range flexibility. Defaults to
///     <c>400</c> ("normal").
/// </param>
/// <param name="Style">
///     The slant style this face represents. Defaults to <see cref="SvgFontStyle.Normal"/>.
/// </param>
public readonly record struct SvgFontFace(TrueTypeFont Font, int Weight = 400, SvgFontStyle Style = SvgFontStyle.Normal);
