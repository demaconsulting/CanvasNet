// cspell:ignore srgb hlink

using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Pptx;

/// <summary>
///     A parsed OOXML theme (<c>ppt/theme/themeN.xml</c>): its color scheme and font scheme. This
///     is <c>internal</c> - carried alongside the slide/layout/master placeholder property chain
///     as context for a later rendering phase to resolve a scheme-color/font token embedded in an
///     effective property fragment; it is not itself part of the placeholder type/idx matching
///     chain (see <see cref="PptxDocument.ResolvePlaceholderProperties"/>'s remarks).
/// </summary>
/// <param name="ColorScheme">The theme's <c>&lt;a:clrScheme&gt;</c>, resolved to concrete colors.</param>
/// <param name="FontScheme">The theme's <c>&lt;a:fontScheme&gt;</c>, resolved to concrete typefaces.</param>
internal sealed record PptxTheme(PptxColorScheme ColorScheme, PptxFontScheme FontScheme);

/// <summary>
///     A theme's resolved 12-slot color scheme (<c>&lt;a:clrScheme&gt;</c>), each slot already
///     resolved to a concrete <see cref="Rgba32"/> value (an <c>&lt;a:srgbClr val="RRGGBB"/&gt;</c>
///     resolves directly; an <c>&lt;a:sysClr val="windowText" lastClr="RRGGBB"/&gt;</c> resolves to
///     its cached <c>lastClr</c> RGB equivalent - no actual OS system-color resolution is
///     performed or possible offline).
/// </summary>
/// <param name="Dark1">The <c>dk1</c> slot.</param>
/// <param name="Light1">The <c>lt1</c> slot.</param>
/// <param name="Dark2">The <c>dk2</c> slot.</param>
/// <param name="Light2">The <c>lt2</c> slot.</param>
/// <param name="Accent1">The <c>accent1</c> slot.</param>
/// <param name="Accent2">The <c>accent2</c> slot.</param>
/// <param name="Accent3">The <c>accent3</c> slot.</param>
/// <param name="Accent4">The <c>accent4</c> slot.</param>
/// <param name="Accent5">The <c>accent5</c> slot.</param>
/// <param name="Accent6">The <c>accent6</c> slot.</param>
/// <param name="Hyperlink">The <c>hlink</c> slot.</param>
/// <param name="FollowedHyperlink">The <c>folHlink</c> slot.</param>
internal sealed record PptxColorScheme(
    Rgba32 Dark1,
    Rgba32 Light1,
    Rgba32 Dark2,
    Rgba32 Light2,
    Rgba32 Accent1,
    Rgba32 Accent2,
    Rgba32 Accent3,
    Rgba32 Accent4,
    Rgba32 Accent5,
    Rgba32 Accent6,
    Rgba32 Hyperlink,
    Rgba32 FollowedHyperlink);

/// <summary>
///     A theme's font scheme (<c>&lt;a:fontScheme&gt;</c>): the major (heading) and minor (body)
///     typeface collections.
/// </summary>
/// <param name="MajorFont">The <c>&lt;a:majorFont&gt;</c> typeface collection.</param>
/// <param name="MinorFont">The <c>&lt;a:minorFont&gt;</c> typeface collection.</param>
internal sealed record PptxFontScheme(PptxFontCollection MajorFont, PptxFontCollection MinorFont);

/// <summary>
///     A single typeface collection (either a theme's major or minor font) naming the typeface
///     used for Latin, East Asian, and complex-script text runs.
/// </summary>
/// <param name="Latin">The <c>&lt;a:latin typeface="..."/&gt;</c> value.</param>
/// <param name="EastAsian">The <c>&lt;a:ea typeface="..."/&gt;</c> value.</param>
/// <param name="ComplexScript">The <c>&lt;a:cs typeface="..."/&gt;</c> value.</param>
internal sealed record PptxFontCollection(string Latin, string EastAsian, string ComplexScript);
