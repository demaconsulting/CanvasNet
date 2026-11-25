using System.Collections.Concurrent;
using DemaConsulting.CanvasNet.Fonts;

namespace DemaConsulting.CanvasNet.MathMl.Layout;

/// <summary>
///     The constants of the OpenType MATH table that the layout reads (a subset of the 56 of the table; the layouts of
///     fractions, radicals and scripts add theirs).
/// </summary>
internal enum MathConstant
{
    /// <summary>The percentage by which the font size is scaled down for the first script level.</summary>
    ScriptPercentScaleDown,

    /// <summary>The percentage by which the font size is scaled down for the second script level.</summary>
    ScriptScriptPercentScaleDown,

    /// <summary>The height of the math axis (the middle of a fraction bar, the center of a symmetric operator) above the baseline.</summary>
    AxisHeight,

    /// <summary>The thickness of a fraction bar.</summary>
    FractionRuleThickness,

    /// <summary>The minimum height of a large operator in display style.</summary>
    DisplayOperatorMinHeight,

    /// <summary>The minimum height of a delimited sub-formula, to which stretchy delimiters grow.</summary>
    DelimitedSubFormulaMinHeight,

    /// <summary>The thickness of an overbar, the thickness of the rule that draws a stretched line character.</summary>
    OverbarRuleThickness,
}

/// <summary>
///     A size variant of a stretchy glyph of the math font (OpenType MathGlyphVariantRecord), scaled to a font size.
/// </summary>
/// <param name="Glyph">The glyph index of the variant in the primary font.</param>
/// <param name="Advance">Its size along the stretch axis in CSS pixels.</param>
internal readonly record struct StretchVariant(int Glyph, double Advance);

/// <summary>
///     A part of a glyph assembly (OpenType GlyphPartRecord, or a part of a Unicode-based assembly of MathML Core B.4),
///     scaled to a font size.
/// </summary>
/// <param name="Font">The font of the part glyph.</param>
/// <param name="Glyph">The glyph index.</param>
/// <param name="StartConnector">The length of the connector at its start (bottom or left) in CSS pixels.</param>
/// <param name="EndConnector">The length of the connector at its end (top or right) in CSS pixels.</param>
/// <param name="FullAdvance">Its full size along the stretch axis in CSS pixels.</param>
/// <param name="IsExtender">Whether the part may be repeated (or left out) to reach a size.</param>
internal readonly record struct StretchPart(TrueTypeFont Font, int Glyph, double StartConnector, double EndConnector, double FullAdvance, bool IsExtender);

/// <summary>
///     How a glyph of the math font stretches along one axis (OpenType MathGlyphConstruction), scaled to a font size.
/// </summary>
/// <param name="Variants">The size variants in increasing size; may be empty.</param>
/// <param name="Parts">The parts of the glyph assembly from bottom to top or left to right, or null when there is none.</param>
/// <param name="AssemblyItalicCorrection">The italic correction of the assembled glyph in CSS pixels.</param>
internal sealed record StretchConstruction(IReadOnlyList<StretchVariant> Variants, IReadOnlyList<StretchPart>? Parts, double AssemblyItalicCorrection);

/// <summary>
///     The math font of the layout: the bundled Noto Sans Math with its OpenType MATH table, scaled to a font size, with
///     the deterministic per-character fallback of the core for the characters it lacks.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why one font.</b> A browser draws MathML with a math font (<c>font-family: math</c>) whose MATH table
///         drives the layout; the bundled Noto Sans Math gives the same result on every machine. A character it has no
///         glyph for (an accented letter in an <c>mtext</c>) falls back through <see cref="GlyphFallback"/> (Noto Sans,
///         then Noto Sans Symbols 2), never through an installed font.
///     </para>
///     <para>
///         <b>Units.</b> The constants and the glyph metrics are read in font design units and scaled to CSS pixels at the
///         font size of the caller; the ink bounds of a glyph are the bounds of its outline (cached per glyph), with y
///         growing downward relative to the glyph origin on the baseline.
///     </para>
///     <para>
///         The instance is immutable apart from its thread-safe caches and is safe for concurrent use;
///         <see cref="Default"/> is created once per process.
///     </para>
/// </remarks>
internal sealed class MathFont
{
    /// <summary>The bundled file name of the math font.</summary>
    public const string BundledFileName = "NotoSansMath-Regular.ttf";

    /// <summary>The flattening tolerance of the ink bounds in font design units: tight within a fraction of a unit.</summary>
    private const float InkTolerance = 0.5f;

    /// <summary>The shared default font, created on first use.</summary>
    private static readonly Lazy<MathFont> LazyDefault =
        new(() => new MathFont(SystemFontCatalog.LoadBundledFallbackCore(BundledFileName)), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The MATH table of the primary font.</summary>
    private readonly MathTable _table;

    /// <summary>The cached ink bounds in font units (left, bottom, right, top; y up) per font and glyph.</summary>
    private readonly ConcurrentDictionary<(TrueTypeFont Font, int Glyph), (float Left, float Bottom, float Right, float Top, bool Empty)> _ink = new();

    /// <summary>
    ///     Initializes a math font over a font with a MATH table.
    /// </summary>
    /// <param name="font">The font.</param>
    /// <exception cref="InvalidDataException">Thrown when the font has no MATH table or a malformed one.</exception>
    internal MathFont(TrueTypeFont font)
    {
        ArgumentNullException.ThrowIfNull(font);
        if (!font.TryGetMathTable(out var table))
        {
            throw new InvalidDataException("The math font has no OpenType MATH table.");
        }

        Primary = font;
        _table = table;
    }

    /// <summary>
    ///     Gets the default math font: the bundled Noto Sans Math.
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown on first use when its MATH table cannot be read (a packaging defect).</exception>
    public static MathFont Default => LazyDefault.Value;

    /// <summary>
    ///     Gets the primary font, whose MATH table drives the layout.
    /// </summary>
    public TrueTypeFont Primary { get; }

    /// <summary>
    ///     Gets the design units per em of the primary font.
    /// </summary>
    public int UnitsPerEm => Primary.UnitsPerEm;

    /// <summary>
    ///     Gets the value of a constant in font design units (a percentage for the scale-down constants).
    /// </summary>
    /// <param name="constant">The constant.</param>
    /// <returns>The value as stored in the MATH table.</returns>
    public int RawConstant(MathConstant constant)
    {
        var constants = _table.Constants;
        return constant switch
        {
            MathConstant.ScriptPercentScaleDown => constants.ScriptPercentScaleDown,
            MathConstant.ScriptScriptPercentScaleDown => constants.ScriptScriptPercentScaleDown,
            MathConstant.AxisHeight => constants.AxisHeight,
            MathConstant.FractionRuleThickness => constants.FractionRuleThickness,
            MathConstant.DisplayOperatorMinHeight => constants.DisplayOperatorMinHeight,
            MathConstant.DelimitedSubFormulaMinHeight => constants.DelimitedSubFormulaMinHeight,
            MathConstant.OverbarRuleThickness => constants.OverbarRuleThickness,
            _ => throw new ArgumentOutOfRangeException(nameof(constant), constant, "Unknown math constant."),
        };
    }

    /// <summary>
    ///     Gets the value of a length constant in CSS pixels at a font size.
    /// </summary>
    /// <param name="constant">The constant (a length, not a percentage).</param>
    /// <param name="fontSize">The font size in CSS pixels.</param>
    /// <returns>The scaled value.</returns>
    public double Constant(MathConstant constant, double fontSize) => RawConstant(constant) * fontSize / UnitsPerEm;

    /// <summary>
    ///     Resolves the font and glyph of a character: the primary font when it has a glyph, else the first bundled
    ///     fallback font that has one, else glyph 0 (the missing-glyph box) of the primary font.
    /// </summary>
    /// <param name="codePoint">The code point.</param>
    /// <returns>The font and the glyph index.</returns>
    public (TrueTypeFont Font, int Glyph) Resolve(int codePoint) => GlyphFallback.Resolve(Primary, codePoint);

    /// <summary>
    ///     Gets the advance width of a glyph in CSS pixels.
    /// </summary>
    /// <param name="font">The font of the glyph.</param>
    /// <param name="glyph">The glyph index.</param>
    /// <param name="fontSize">The font size in CSS pixels.</param>
    /// <returns>The advance width.</returns>
    public static double Advance(TrueTypeFont font, int glyph, double fontSize) => font.GetAdvanceWidth(glyph) * fontSize / font.UnitsPerEm;

    /// <summary>
    ///     Gets the ink bounds of a glyph in CSS pixels, relative to the glyph origin on the baseline (y down).
    /// </summary>
    /// <param name="font">The font of the glyph.</param>
    /// <param name="glyph">The glyph index.</param>
    /// <param name="fontSize">The font size in CSS pixels.</param>
    /// <returns>The bounds; a rectangle of zero size at the origin for a glyph without contours (a space).</returns>
    /// <exception cref="InvalidDataException">Thrown when the outline of the glyph is malformed.</exception>
    public MathRect InkBounds(TrueTypeFont font, int glyph, double fontSize)
    {
        var (left, bottom, right, top, empty) = _ink.GetOrAdd((font, glyph), static key =>
        {
            var bounds = key.Font.GetGlyphOutline(key.Glyph).GetBounds(InkTolerance);
            return bounds.IsEmpty ? (0, 0, 0, 0, true) : (bounds.Left, bounds.Top, bounds.Right, bounds.Bottom, false);
        });
        if (empty)
        {
            return new MathRect(0, 0, 0, 0);
        }

        // Font units grow upward; the layout grows downward
        var scale = fontSize / font.UnitsPerEm;
        return new MathRect(left * scale, -top * scale, (right - left) * scale, (top - bottom) * scale);
    }

    /// <summary>
    ///     Gets the italic correction of a glyph of the primary font in CSS pixels.
    /// </summary>
    /// <param name="font">The font of the glyph.</param>
    /// <param name="glyph">The glyph index.</param>
    /// <param name="fontSize">The font size in CSS pixels.</param>
    /// <returns>The italic correction, or zero for a glyph without one or of a fallback font.</returns>
    public double ItalicCorrection(TrueTypeFont font, int glyph, double fontSize) =>
        ReferenceEquals(font, Primary) && _table.TryGetItalicsCorrection(glyph, out var value) ? value * fontSize / UnitsPerEm : 0;

    /// <summary>
    ///     Gets the top accent attachment of a glyph of the primary font in CSS pixels from the glyph origin.
    /// </summary>
    /// <param name="font">The font of the glyph.</param>
    /// <param name="glyph">The glyph index.</param>
    /// <param name="fontSize">The font size in CSS pixels.</param>
    /// <returns>The attachment, or null for a glyph without one or of a fallback font.</returns>
    public double? TopAccentAttachment(TrueTypeFont font, int glyph, double fontSize) =>
        ReferenceEquals(font, Primary) && _table.TryGetTopAccentAttachment(glyph, out var value) ? value * fontSize / UnitsPerEm : null;

    /// <summary>
    ///     Gets the minimum overlap of two adjacent parts of a glyph assembly (MATH MinConnectorOverlap) in CSS pixels.
    /// </summary>
    /// <param name="fontSize">The font size in CSS pixels.</param>
    /// <returns>The overlap.</returns>
    public double MinConnectorOverlap(double fontSize) => _table.MinConnectorOverlap * fontSize / UnitsPerEm;

    /// <summary>
    ///     Gets how a glyph of the primary font stretches along an axis (the MathVariants table), scaled to a font size.
    /// </summary>
    /// <param name="font">The font of the glyph.</param>
    /// <param name="glyph">The glyph index.</param>
    /// <param name="axis">The stretch axis: block reads the vertical constructions, inline the horizontal ones.</param>
    /// <param name="fontSize">The font size in CSS pixels.</param>
    /// <param name="construction">The construction.</param>
    /// <returns>True when the glyph belongs to the primary font and has a construction for the axis.</returns>
    public bool TryGetConstruction(TrueTypeFont font, int glyph, StretchAxis axis, double fontSize, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out StretchConstruction? construction)
    {
        construction = null;
        if (!ReferenceEquals(font, Primary))
        {
            return false;
        }

        var source = axis == StretchAxis.Block ? _table.GetVerticalConstruction(glyph) : _table.GetHorizontalConstruction(glyph);
        if (source is null)
        {
            return false;
        }

        var scale = fontSize / UnitsPerEm;
        var variants = source.Variants.Select(variant => new StretchVariant(variant.Glyph, variant.AdvanceMeasurement * scale)).ToList();
        var parts = source.Assembly?.Parts
            .Select(part => new StretchPart(Primary, part.Glyph, part.StartConnectorLength * scale, part.EndConnectorLength * scale, part.FullAdvance * scale, part.IsExtender))
            .ToList();
        construction = new StretchConstruction(variants, parts, (source.Assembly?.ItalicsCorrection ?? 0) * scale);
        return true;
    }

    /// <summary>
    ///     Gets the x-height in CSS pixels (the base of the <c>ex</c> unit): the ink top of the letter x.
    /// </summary>
    /// <param name="fontSize">The font size in CSS pixels.</param>
    /// <returns>The x-height.</returns>
    public double XHeight(double fontSize)
    {
        var (font, glyph) = Resolve('x');
        return -InkBounds(font, glyph, fontSize).Y;
    }
}
