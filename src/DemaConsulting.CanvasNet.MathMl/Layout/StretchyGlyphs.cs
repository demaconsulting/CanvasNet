using System.Collections.Frozen;
using System.Globalization;
using DemaConsulting.CanvasNet.Fonts;

namespace DemaConsulting.CanvasNet.MathMl.Layout;

/// <summary>
///     How a stretched glyph was built.
/// </summary>
internal enum StretchKind
{
    /// <summary>The base glyph of the character, which already covers the target.</summary>
    Base,

    /// <summary>A size variant of the MATH table.</summary>
    Variant,

    /// <summary>A glyph assembly of the MATH table.</summary>
    Assembly,

    /// <summary>A Unicode-based glyph assembly of MathML Core (Appendix B.4), for a font without a construction.</summary>
    UnicodeAssembly,

    /// <summary>A filled rule, for a line character that no construction stretches.</summary>
    Rule,
}

/// <summary>
///     A glyph of a stretched glyph, positioned relative to the origin of the result.
/// </summary>
/// <param name="Font">The font of the glyph.</param>
/// <param name="Glyph">The glyph index.</param>
/// <param name="X">The pen position of the glyph origin.</param>
/// <param name="Baseline">The baseline of the glyph (y, down).</param>
internal readonly record struct StretchGlyph(TrueTypeFont Font, int Glyph, double X, double Baseline);

/// <summary>
///     A glyph stretched to a target size: its metrics and what it paints, relative to its origin (its left edge on its
///     baseline, y growing downward), in CSS pixels.
/// </summary>
/// <remarks>
///     A glyph or a size variant has the ink ascent and descent of its glyph, rounded out to whole CSS pixels as the
///     token layout rounds them; a vertical assembly stands on the baseline (its ascent is its size, its descent zero),
///     and its caller moves it to its target; a horizontal assembly starts at the origin.
/// </remarks>
/// <param name="Kind">How the glyph was built.</param>
/// <param name="Width">The inline size.</param>
/// <param name="Ascent">The extent above the baseline.</param>
/// <param name="Descent">The extent below the baseline.</param>
/// <param name="ItalicCorrection">The italic correction.</param>
/// <param name="Glyphs">The glyphs, in painting order.</param>
/// <param name="Rule">The filled rectangle of a <see cref="StretchKind.Rule"/>, or null.</param>
internal sealed record StretchResult(StretchKind Kind, double Width, double Ascent, double Descent, double ItalicCorrection, IReadOnlyList<StretchGlyph> Glyphs, MathRect? Rule)
{
    /// <summary>Gets the size along the block axis (the ascent plus the descent).</summary>
    public double Height => Ascent + Descent;
}

/// <summary>
///     Stretches the glyph of a character to a target size along an axis (MathML Core 5.3): the base glyph, a size variant
///     or a glyph assembly of the MATH table of the math font, else a Unicode-based glyph assembly of MathML Core (B.4),
///     else a rule for a line character; and finds the large variant of an operator in display style (3.2.4.3).
/// </summary>
/// <remarks>
///     <para>
///         <b>Order.</b> As the algorithm to shape a stretchy glyph of MathML Core 5.3.2: with no construction for the axis
///         the glyph cannot stretch (the caller reports it and lays the text out as it is); when the base glyph covers the
///         target, it is used; else the first size variant whose advance measurement covers it; else the glyph assembly;
///         else the last of these that was tried. A character that the primary font has no construction for uses its
///         entry of the Unicode-based glyph assemblies, whose connectors are zero; a line character
///         (<see cref="RuleCharacters"/>) uses a rule of its own thickness instead, because the repeated glyph of its
///         Unicode-based assembly leaves gaps between its side bearings.
///     </para>
///     <para>
///         <b>Assembly.</b> As 5.3.1: every extender is repeated r times and the same overlap o joins every pair of
///         adjacent glyphs. r is the smallest number of repetitions whose size at the minimum overlap reaches the target;
///         o is the overlap that spreads the extra size evenly, between MinConnectorOverlap and the shortest connector that
///         joins two glyphs. A connector shorter than MinConnectorOverlap (the arrows of Noto Sans Math join their
///         extenders without overlap) lowers the minimum where MathML Core would reject the assembly, so that such an
///         assembly still grows. More than <see cref="MathMlLimits.MaxAssemblyParts"/> glyphs fail with a located
///         <see cref="MathMlRenderException"/> before any glyph is placed. A vertical part is placed with the bottom of its
///         ink on the end of the previous part, a horizontal part with its origin there.
///     </para>
///     <para>
///         The horizontal stretching serves the operators of the under- and overscript elements
///         (<see cref="TokenLayout"/>, through its inline stretching). The class is stateless and safe for concurrent use.
///     </para>
/// </remarks>
internal static partial class StretchyGlyphs
{
    /// <summary>The tolerance of the comparisons with a target, in CSS pixels.</summary>
    private const double Tolerance = 1e-9;

    /// <summary>The tolerance that keeps an ascent of 20.0000000001 from becoming 21 when it is rounded out.</summary>
    private const double RoundingTolerance = 1e-6;

    /// <summary>
    ///     The line characters that stretch as a rule when the math font has no construction for them: low line, macron,
    ///     combining low line, horizontal bar and overline.
    /// </summary>
    private static readonly FrozenSet<int> RuleCharacters = new[] { 0x005F, 0x00AF, 0x0332, 0x2015, 0x203E }.ToFrozenSet();

    /// <summary>The Unicode-based glyph assemblies by base character and axis, built once from the generated table.</summary>
    private static readonly Lazy<FrozenDictionary<(int CodePoint, StretchAxis Axis), int[]>> LazyUnicodeAssemblies =
        new(BuildUnicodeAssemblies, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    ///     Gets whether a character is a line character that stretches as a rule when no construction stretches it.
    /// </summary>
    /// <param name="codePoint">The code point.</param>
    /// <returns>True for a line character.</returns>
    public static bool IsRuleCharacter(int codePoint) => RuleCharacters.Contains(codePoint);

    /// <summary>
    ///     Stretches the glyph of a character to a target size (MathML Core 5.3.2, the algorithm to shape a stretchy glyph).
    /// </summary>
    /// <param name="font">The math font.</param>
    /// <param name="codePoint">The character.</param>
    /// <param name="axis">The stretch axis.</param>
    /// <param name="target">The target size along the axis in CSS pixels.</param>
    /// <param name="fontSize">The font size in CSS pixels.</param>
    /// <param name="maxParts">The largest number of glyphs of an assembly (<see cref="MathMlLimits.MaxAssemblyParts"/>).</param>
    /// <param name="location">The location of the operator, for the limit error.</param>
    /// <returns>The stretched glyph, or null when the character cannot stretch along the axis (no construction at all).</returns>
    /// <exception cref="MathMlRenderException">Thrown when an assembly needs more glyphs than <paramref name="maxParts"/>.</exception>
    public static StretchResult? Stretch(MathFont font, int codePoint, StretchAxis axis, double target, double fontSize, int maxParts, MathMlLocation location)
    {
        ArgumentNullException.ThrowIfNull(font);
        if (double.IsNaN(target))
        {
            target = 0;
        }

        // What can stretch the character: the math font, a rule for a line character, a Unicode-based assembly
        var (glyphFont, glyph) = font.Resolve(codePoint);
        var hasConstruction = font.TryGetConstruction(glyphFont, glyph, axis, fontSize, out var construction);
        var isRule = !hasConstruction && RuleCharacters.Contains(codePoint);
        var unicodeParts = hasConstruction || isRule ? null : UnicodeAssembly(font, codePoint, axis, fontSize);
        if (!hasConstruction && !isRule && unicodeParts is null)
        {
            return null;
        }

        // The base glyph when it is large enough
        var last = GlyphResult(font, glyphFont, glyph, fontSize, StretchKind.Base);
        if (SizeAlong(last, axis) >= target - Tolerance)
        {
            return last;
        }

        if (construction is not null)
        {
            // The first size variant that covers the target, else the assembly, else the last one tried
            foreach (var variant in construction.Variants)
            {
                last = GlyphResult(font, font.Primary, variant.Glyph, fontSize, StretchKind.Variant);
                if (variant.Advance >= target - Tolerance)
                {
                    return last;
                }
            }

            return construction.Parts is { } parts
                ? Assemble(parts, font.MinConnectorOverlap(fontSize), target, axis, fontSize, font, maxParts, location, StretchKind.Assembly, construction.AssemblyItalicCorrection) ?? last
                : last;
        }

        return isRule
            ? RuleResult(font, glyphFont, glyph, axis, target, fontSize)
            : Assemble(unicodeParts!, 0, target, axis, fontSize, font, maxParts, location, StretchKind.UnicodeAssembly, 0) ?? last;
    }

    /// <summary>
    ///     Gets the base glyph of a character as a result that does not stretch (the glyph that an operator keeps when
    ///     nothing stretches it).
    /// </summary>
    /// <param name="font">The math font.</param>
    /// <param name="codePoint">The character.</param>
    /// <param name="fontSize">The font size in CSS pixels.</param>
    /// <returns>The glyph, of kind <see cref="StretchKind.Base"/>.</returns>
    public static StretchResult Glyph(MathFont font, int codePoint, double fontSize)
    {
        ArgumentNullException.ThrowIfNull(font);
        var (glyphFont, glyph) = font.Resolve(codePoint);
        return GlyphResult(font, glyphFont, glyph, fontSize, StretchKind.Base);
    }

    /// <summary>
    ///     Finds the glyph of a large operator in display style (MathML Core 3.2.4.3): the first size variant of the MATH
    ///     table at least as tall as a minimum height, else the largest one, never the base glyph.
    /// </summary>
    /// <param name="font">The math font.</param>
    /// <param name="codePoint">The character.</param>
    /// <param name="minHeight">The minimum height (DisplayOperatorMinHeight) in CSS pixels.</param>
    /// <param name="fontSize">The font size in CSS pixels.</param>
    /// <returns>The variant, or null when the character has no size variant other than its base glyph.</returns>
    public static StretchResult? LargeVariant(MathFont font, int codePoint, double minHeight, double fontSize)
    {
        ArgumentNullException.ThrowIfNull(font);
        var (glyphFont, glyph) = font.Resolve(codePoint);
        if (!font.TryGetConstruction(glyphFont, glyph, StretchAxis.Block, fontSize, out var construction))
        {
            return null;
        }

        StretchVariant? chosen = null;
        foreach (var variant in construction.Variants.Where(variant => variant.Glyph != glyph))
        {
            chosen = variant;
            if (variant.Advance >= minHeight - Tolerance)
            {
                break;
            }
        }

        return chosen is { } found ? GlyphResult(font, font.Primary, found.Glyph, fontSize, StretchKind.Variant) : null;
    }

    /// <summary>
    ///     Computes the preferred inline size of a glyph stretched along the block axis (MathML Core 5.3.2): the largest
    ///     advance of the base glyph, its size variants and the parts of its assembly (or of its Unicode-based assembly).
    /// </summary>
    /// <param name="font">The math font.</param>
    /// <param name="codePoint">The character.</param>
    /// <param name="fontSize">The font size in CSS pixels.</param>
    /// <returns>The inline size in CSS pixels.</returns>
    public static double PreferredInlineSize(MathFont font, int codePoint, double fontSize)
    {
        ArgumentNullException.ThrowIfNull(font);
        var (glyphFont, glyph) = font.Resolve(codePoint);
        var size = MathFont.Advance(glyphFont, glyph, fontSize);
        IEnumerable<StretchPart>? parts;
        if (font.TryGetConstruction(glyphFont, glyph, StretchAxis.Block, fontSize, out var construction))
        {
            foreach (var variant in construction.Variants)
            {
                size = Math.Max(size, MathFont.Advance(font.Primary, variant.Glyph, fontSize));
            }

            parts = construction.Parts;
        }
        else
        {
            parts = RuleCharacters.Contains(codePoint) ? null : UnicodeAssembly(font, codePoint, StretchAxis.Block, fontSize);
        }

        foreach (var part in parts ?? [])
        {
            size = Math.Max(size, MathFont.Advance(part.Font, part.Glyph, fontSize));
        }

        return size;
    }

    /// <summary>
    ///     Solves an assembly for a target size (MathML Core 5.3.1): the number of repetitions of every extender and the
    ///     overlap of adjacent glyphs.
    /// </summary>
    /// <param name="parts">The parts.</param>
    /// <param name="minOverlap">The minimum connector overlap in CSS pixels.</param>
    /// <param name="target">The target size in CSS pixels.</param>
    /// <param name="maxParts">The largest number of glyphs.</param>
    /// <param name="location">The location of the operator, for the limit error.</param>
    /// <returns>The repetitions, the overlap and the size; null when the assembly is invalid (it has no extender, or its extenders do not grow it).</returns>
    /// <exception cref="MathMlRenderException">Thrown when the assembly needs more glyphs than <paramref name="maxParts"/>.</exception>
    internal static (int Repetitions, double Overlap, double Size)? SolveAssembly(IReadOnlyList<StretchPart> parts, double minOverlap, double target, int maxParts, MathMlLocation location)
    {
        ArgumentNullException.ThrowIfNull(parts);

        // The longest overlap the connectors allow; a connector shorter than the minimum overlap lowers it (Noto Sans Math
        // joins the extenders of its arrows without overlap), so that such an assembly still grows. The connectors at the
        // outer ends of the assembly (the start of the first part and the end of the last, when they are not extenders)
        // join nothing and are left out; the literal text of MathML Core 5.3.1 names the other two ends (a documented
        // difference, most likely an erratum of the specification)
        var connectors = double.PositiveInfinity;
        for (var i = 0; i < parts.Count; i++)
        {
            var part = parts[i];
            if (part.IsExtender || i > 0)
            {
                connectors = Math.Min(connectors, part.StartConnector);
            }

            if (part.IsExtender || i < parts.Count - 1)
            {
                connectors = Math.Min(connectors, part.EndConnector);
            }
        }

        connectors = Math.Max(0, connectors);
        minOverlap = Math.Min(minOverlap, connectors);

        // The sums over the extenders and the other parts
        var extenders = parts.Count(part => part.IsExtender);
        var others = parts.Count - extenders;
        var extenderSum = parts.Where(part => part.IsExtender).Sum(part => part.FullAdvance);
        var otherSum = parts.Where(part => !part.IsExtender).Sum(part => part.FullAdvance);
        var growth = extenderSum - (minOverlap * extenders);
        if (extenders == 0 || growth <= 0)
        {
            return null;
        }

        // The smallest number of repetitions that reaches the target at the minimum overlap, bounded before it is used
        var repetitions = Math.Max(0, Math.Ceiling(((target - otherSum + (minOverlap * (others - 1))) / growth) - Tolerance));
        var count = others + (repetitions * extenders);
        if (!(count <= maxParts))
        {
            throw new MathMlRenderException(
                MathMlDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"a stretchy operator needs more than {maxParts} glyphs to reach its size (MathMlLimits.MaxAssemblyParts)"),
                location,
                "reduce the size of the content around the operator, or raise the limit in MathMlRenderOptions.Limits");
        }

        // The overlap that spreads the extra size evenly, between the minimum overlap and the connectors
        var natural = otherSum + (repetitions * extenderSum);
        if (count <= 1)
        {
            return ((int)repetitions, 0, natural);
        }

        var overlap = Math.Clamp((natural - target) / (count - 1), minOverlap, connectors);
        return ((int)repetitions, overlap, natural - (overlap * (count - 1)));
    }

    /// <summary>
    ///     Builds a glyph assembly for a target size.
    /// </summary>
    /// <param name="parts">The parts.</param>
    /// <param name="minOverlap">The minimum connector overlap in CSS pixels.</param>
    /// <param name="target">The target size.</param>
    /// <param name="axis">The stretch axis.</param>
    /// <param name="fontSize">The font size.</param>
    /// <param name="font">The math font, for the ink of the parts.</param>
    /// <param name="maxParts">The largest number of glyphs.</param>
    /// <param name="location">The location of the operator.</param>
    /// <param name="kind">The kind of the result.</param>
    /// <param name="italicCorrection">The italic correction of the assembly.</param>
    /// <returns>The assembly, or null when it is invalid.</returns>
    private static StretchResult? Assemble(IReadOnlyList<StretchPart> parts, double minOverlap, double target, StretchAxis axis, double fontSize, MathFont font, int maxParts, MathMlLocation location, StretchKind kind, double italicCorrection)
    {
        if (SolveAssembly(parts, minOverlap, target, maxParts, location) is not { } solved)
        {
            return null;
        }

        var (repetitions, overlap, size) = solved;

        // Place the glyphs from the bottom (or the left), each the overlap before the end of the previous one
        var glyphs = new List<StretchGlyph>();
        var position = 0.0;
        var width = 0.0;
        var ascent = double.NegativeInfinity;
        var descent = double.NegativeInfinity;
        foreach (var part in parts)
        {
            var ink = font.InkBounds(part.Font, part.Glyph, fontSize);
            for (var i = 0; i < (part.IsExtender ? repetitions : 1); i++)
            {
                if (axis == StretchAxis.Block)
                {
                    // The bottom of the ink stands on the position (y grows downward)
                    glyphs.Add(new StretchGlyph(part.Font, part.Glyph, 0, -position - ink.Bottom));
                    width = Math.Max(width, MathFont.Advance(part.Font, part.Glyph, fontSize));
                }
                else
                {
                    glyphs.Add(new StretchGlyph(part.Font, part.Glyph, position, 0));
                    ascent = Math.Max(ascent, -ink.Y);
                    descent = Math.Max(descent, ink.Bottom);
                }

                position += part.FullAdvance - overlap;
            }
        }

        // Horizontally, the ascent and the descent are the ink of the parts, rounded out as the ink of a glyph
        return axis == StretchAxis.Block
            ? new StretchResult(kind, width, size, 0, italicCorrection, glyphs, null)
            : new StretchResult(kind, size, RoundOut(ascent), RoundOut(descent), italicCorrection, glyphs, null);
    }

    /// <summary>
    ///     Rounds an ink extent out to whole CSS pixels (zero when there is no ink).
    /// </summary>
    /// <param name="extent">The extent.</param>
    /// <returns>The rounded extent.</returns>
    private static double RoundOut(double extent) => double.IsNegativeInfinity(extent) ? 0 : Math.Ceiling(extent - RoundingTolerance);

    /// <summary>
    ///     Builds the rule of a line character.
    /// </summary>
    /// <param name="font">The math font.</param>
    /// <param name="glyphFont">The font of the glyph of the character.</param>
    /// <param name="glyph">The glyph, zero when no bundled font has one.</param>
    /// <param name="axis">The stretch axis.</param>
    /// <param name="target">The target size.</param>
    /// <param name="fontSize">The font size.</param>
    /// <returns>The rule: as thick as the ink of the glyph and where it is, or OverbarRuleThickness on the math axis when there is no ink.</returns>
    private static StretchResult RuleResult(MathFont font, TrueTypeFont glyphFont, int glyph, StretchAxis axis, double target, double fontSize)
    {
        var ink = glyph == 0 ? default : font.InkBounds(glyphFont, glyph, fontSize);
        var hasInk = ink.Width > 0 && ink.Height > 0;
        var thickness = font.Constant(MathConstant.OverbarRuleThickness, fontSize);
        if (axis == StretchAxis.Inline)
        {
            var top = hasInk ? ink.Y : -(font.Constant(MathConstant.AxisHeight, fontSize) + (thickness / 2));
            var height = hasInk ? ink.Height : thickness;
            return new StretchResult(StretchKind.Rule, target, -top, top + height, 0, [], new MathRect(0, top, target, height));
        }

        var left = hasInk ? Math.Max(0, ink.X) : 0;
        var ruleWidth = hasInk ? ink.Width : thickness;
        var width = Math.Max(MathFont.Advance(glyphFont, glyph, fontSize), left + ruleWidth);
        return new StretchResult(StretchKind.Rule, width, target, 0, 0, [], new MathRect(left, -target, ruleWidth, target));
    }

    /// <summary>
    ///     Gets the parts of the Unicode-based glyph assembly of a character (MathML Core B.4): the bottom (or left)
    ///     character, the extender, optionally the middle character and the extender again, and the top (or right)
    ///     character, each with zero connectors and the size of its glyph along the axis (its ink height vertically, its
    ///     advance horizontally).
    /// </summary>
    /// <param name="font">The math font.</param>
    /// <param name="codePoint">The base character.</param>
    /// <param name="axis">The stretch axis.</param>
    /// <param name="fontSize">The font size.</param>
    /// <returns>The parts, or null when the character has no entry for the axis or a part has no glyph in the bundled fonts.</returns>
    internal static IReadOnlyList<StretchPart>? UnicodeAssembly(MathFont font, int codePoint, StretchAxis axis, double fontSize)
    {
        if (!LazyUnicodeAssemblies.Value.TryGetValue((codePoint, axis), out var entry))
        {
            return null;
        }

        // The entry: extender, bottom, middle, top (-1 for an absent part)
        var (extender, bottom, middle, top) = (entry[0], entry[1], entry[2], entry[3]);
        var sequence = new List<(int CodePoint, bool IsExtender)>();
        if (bottom >= 0)
        {
            sequence.Add((bottom, false));
        }

        sequence.Add((extender, true));
        if (middle >= 0)
        {
            sequence.Add((middle, false));
            sequence.Add((extender, true));
        }

        if (top >= 0)
        {
            sequence.Add((top, false));
        }

        var parts = new List<StretchPart>(sequence.Count);
        foreach (var (character, isExtender) in sequence)
        {
            var (partFont, partGlyph) = font.Resolve(character);
            if (partGlyph == 0)
            {
                return null;
            }

            var full = axis == StretchAxis.Block ? font.InkBounds(partFont, partGlyph, fontSize).Height : MathFont.Advance(partFont, partGlyph, fontSize);
            parts.Add(new StretchPart(partFont, partGlyph, 0, 0, full, isExtender));
        }

        return parts;
    }

    /// <summary>
    ///     Gets the size of a result along an axis.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <param name="axis">The axis.</param>
    /// <returns>Its height for the block axis, its width for the inline axis.</returns>
    private static double SizeAlong(StretchResult result, StretchAxis axis) => axis == StretchAxis.Block ? result.Height : result.Width;

    /// <summary>
    ///     Builds the result of a single glyph: its advance and its ink ascent and descent rounded out to whole CSS pixels.
    /// </summary>
    /// <param name="font">The math font.</param>
    /// <param name="glyphFont">The font of the glyph.</param>
    /// <param name="glyph">The glyph index.</param>
    /// <param name="fontSize">The font size.</param>
    /// <param name="kind">The kind of the result.</param>
    /// <returns>The result.</returns>
    private static StretchResult GlyphResult(MathFont font, TrueTypeFont glyphFont, int glyph, double fontSize, StretchKind kind)
    {
        var ink = font.InkBounds(glyphFont, glyph, fontSize);
        var hasInk = ink.Width > 0 || ink.Height > 0;
        var ascent = hasInk ? Math.Ceiling(-ink.Y - RoundingTolerance) : 0;
        var descent = hasInk ? Math.Ceiling(ink.Bottom - RoundingTolerance) : 0;
        return new StretchResult(kind, MathFont.Advance(glyphFont, glyph, fontSize), ascent, descent, font.ItalicCorrection(glyphFont, glyph, fontSize), [new StretchGlyph(glyphFont, glyph, 0, 0)], null);
    }

    /// <summary>
    ///     Builds the lookup of the Unicode-based glyph assemblies from the generated table.
    /// </summary>
    /// <returns>The extender, bottom, middle and top characters by base character and axis.</returns>
    private static FrozenDictionary<(int CodePoint, StretchAxis Axis), int[]> BuildUnicodeAssemblies()
    {
        var table = UnicodeAssemblyTable;
        var result = new Dictionary<(int CodePoint, StretchAxis Axis), int[]>();
        for (var i = 0; i + 5 < table.Length; i += 6)
        {
            result[(table[i], (StretchAxis)table[i + 1])] = [table[i + 2], table[i + 3], table[i + 4], table[i + 5]];
        }

        return result.ToFrozenDictionary();
    }

    /// <summary>
    ///     Gets the number of entries of the Unicode-based glyph assemblies (for the tests of the generated table).
    /// </summary>
    internal static int UnicodeAssemblyCount => LazyUnicodeAssemblies.Value.Count;
}
