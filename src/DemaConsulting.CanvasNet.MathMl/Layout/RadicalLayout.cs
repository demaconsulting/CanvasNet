namespace DemaConsulting.CanvasNet.MathMl.Layout;

/// <summary>
///     Lays out <c>msqrt</c> and <c>mroot</c> (MathML Core 3.3.3): the radical symbol stretched to cover its base, the
///     overbar above the base, and the index of a root.
/// </summary>
/// <remarks>
///     <para>
///         <b>Radical symbol (3.3.3.1).</b> The gap between the base and the overbar is RadicalVerticalGap
///         (RadicalDisplayStyleVerticalGap in display style). The radical symbol U+221A is stretched by
///         <see cref="StretchyGlyphs"/> along the block axis to the overbar thickness RadicalRuleThickness plus the gap plus
///         the height of the base: its base glyph, a size variant, or its glyph assembly.
///     </para>
///     <para>
///         <b>Square root (3.3.3.2).</b> The ascent is the larger of the base's and the base's ascent plus the gap, the
///         overbar thickness and RadicalExtraAscender; the descent is the larger of the base's and the height of the
///         radical symbol plus RadicalExtraAscender minus the ascent. The radical symbol stands at the left with its top
///         RadicalExtraAscender below the top of the box, the base follows it, and the overbar covers the base with its
///         top at the top of the symbol. As in Chrome, the box is as wide as the base plus the preferred inline size of the
///         radical symbol (the widest of its constructions), while the base follows the symbol drawn.
///     </para>
///     <para>
///         <b>Root (3.3.3.3).</b> The index comes first, after RadicalKernBeforeDegree (not negative) and before
///         RadicalKernAfterDegree (not less than minus the width of the index; negative in Noto Sans Math, so the
///         radical symbol slides under the index); its bottom is raised by RadicalDegreeBottomRaisePercent of the height of
///         the square root from the bottom of the square root, which may raise the ascent.
///     </para>
///     <para>
///         The symbol and the overbar are painted in the color of the radical. An assembly with more glyphs than
///         <see cref="MathMlLimits.MaxAssemblyParts"/> fails with a located <see cref="MathMlRenderException"/>. An
///         instance serves one call on one thread.
///     </para>
/// </remarks>
internal sealed class RadicalLayout
{
    /// <summary>The radical symbol (square root).</summary>
    public const int RadicalSymbol = 0x221A;

    /// <summary>The tolerance that keeps an edge of 5.0000000001 from being rounded out to 6.</summary>
    private const double RoundingTolerance = 1e-6;

    /// <summary>The layout unit of Chrome (1/64 CSS pixel), to which it truncates the raise percentage of an index.</summary>
    private const double LayoutUnitsPerPixel = 64;

    /// <summary>The math font.</summary>
    private readonly MathFont _font;

    /// <summary>The limits of the call (the glyphs of a stretched radical symbol).</summary>
    private readonly MathMlLimits _limits;

    /// <summary>
    ///     Initializes the radical layout of one call.
    /// </summary>
    /// <param name="font">The math font.</param>
    /// <param name="limits">The limits of the call.</param>
    public RadicalLayout(MathFont font, MathMlLimits limits)
    {
        _font = font;
        _limits = limits;
    }

    /// <summary>
    ///     Lays out a square root around its base (MathML Core 3.3.3.2).
    /// </summary>
    /// <param name="radicand">The box of the base (the row of the children of <c>msqrt</c>).</param>
    /// <param name="style">The computed style of the radical.</param>
    /// <param name="location">The location of the radical, for the limit error.</param>
    /// <returns>The box of the square root.</returns>
    /// <exception cref="MathMlRenderException">Thrown when the radical symbol needs more glyphs than <see cref="MathMlLimits.MaxAssemblyParts"/>.</exception>
    public MathBox LayoutSquareRoot(MathBox radicand, MathStyle style, MathMlLocation location) => SquareRoot(radicand, style, location).Box;

    /// <summary>
    ///     Lays out a root: the square root of its base with its index (MathML Core 3.3.3.3).
    /// </summary>
    /// <param name="radicand">The box of the base.</param>
    /// <param name="index">The box of the index.</param>
    /// <param name="style">The computed style of the radical.</param>
    /// <param name="location">The location of the radical, for the limit error.</param>
    /// <returns>The box of the root.</returns>
    /// <exception cref="MathMlRenderException">Thrown when the radical symbol needs more glyphs than <see cref="MathMlLimits.MaxAssemblyParts"/>.</exception>
    public MathBox LayoutRoot(MathBox radicand, MathBox index, MathStyle style, MathMlLocation location)
    {
        ArgumentNullException.ThrowIfNull(radicand);
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(style);
        var size = style.FontSize;
        var (root, symbolHeight) = SquareRoot(radicand, style, location);

        // The kerns around the index; the kern after it never moves the symbol before the start of the index
        var kernBefore = Math.Max(0, _font.Constant(MathConstant.RadicalKernBeforeDegree, size));
        var kernAfter = Math.Max(-index.Width, _font.Constant(MathConstant.RadicalKernAfterDegree, size));

        // The bottom of the index is raised from the bottom of the square root by a percentage of the height of the
        // radical symbol; the metrics as Chrome computes them, with the percentage at the precision of its layout unit
        var percent = Math.Floor(_font.RawConstant(MathConstant.RadicalDegreeBottomRaisePercent) / 100.0 * LayoutUnitsPerPixel) / LayoutUnitsPerPixel;
        var raise = percent * symbolHeight;
        var ascent = Math.Max(root.Ascent, -root.Descent + raise + index.Descent + index.Ascent);
        var descent = Math.Max(root.Descent, root.Descent - raise + index.Descent + index.Ascent);
        var indexShift = root.Descent - raise - index.Descent;
        var offset = kernBefore + index.Width + kernAfter;
        return new MathBox(offset + root.Width, ascent, descent, [new PlacedBox(index, kernBefore, indexShift), new PlacedBox(root, offset, 0)]);
    }

    /// <summary>
    ///     Lays out a square root (MathML Core 3.3.3.2) and gives the height of its radical symbol.
    /// </summary>
    /// <param name="radicand">The box of the base.</param>
    /// <param name="style">The computed style of the radical.</param>
    /// <param name="location">The location of the radical, for the limit error.</param>
    /// <returns>The box of the square root and the height of the radical symbol.</returns>
    private (MathBox Box, double SymbolHeight) SquareRoot(MathBox radicand, MathStyle style, MathMlLocation location)
    {
        ArgumentNullException.ThrowIfNull(radicand);
        ArgumentNullException.ThrowIfNull(style);
        var size = style.FontSize;
        var gap = _font.Constant(style.DisplayStyle ? MathConstant.RadicalDisplayStyleVerticalGap : MathConstant.RadicalVerticalGap, size);
        var rule = _font.Constant(MathConstant.RadicalRuleThickness, size);
        var extra = _font.Constant(MathConstant.RadicalExtraAscender, size);

        // The radical symbol covers the base, the gap and the overbar (3.3.3.1)
        var target = rule + gap + radicand.Ascent + radicand.Descent;
        var symbol = StretchyGlyphs.Stretch(_font, RadicalSymbol, StretchAxis.Block, target, size, _limits.MaxAssemblyParts, location)
            ?? StretchyGlyphs.Glyph(_font, RadicalSymbol, size);

        // The metrics (3.3.3.2)
        var ascent = Math.Max(radicand.Ascent, radicand.Ascent + gap + rule + extra);
        var descent = Math.Max(radicand.Descent, symbol.Height + extra - ascent);

        // The symbol with its top below the extra ascender, the overbar from its end over the base
        var top = -ascent + extra;
        var items = new List<IPaintItem>(symbol.Glyphs.Count + 2);
        foreach (var glyph in symbol.Glyphs)
        {
            var baseline = glyph.Baseline + top + symbol.Ascent;
            var ink = _font.InkBounds(glyph.Font, glyph.Glyph, size).Offset(glyph.X, baseline);
            items.Add(new GlyphItem(glyph.Font, glyph.Glyph, size, glyph.X, baseline, style.Color, ink));
        }

        if (symbol.Rule is { } symbolRule)
        {
            items.Add(new RuleItem(symbolRule.Offset(0, top + symbol.Ascent), style.Color));
        }

        // The base follows the symbol drawn; the box holds the widest construction of the symbol (as Chrome)
        var advance = Advance(symbol, size);
        items.Add(new RuleItem(new MathRect(advance, top, radicand.Width, rule), style.Color));
        var width = Math.Max(advance, StretchyGlyphs.PreferredInlineSize(_font, RadicalSymbol, size)) + radicand.Width;
        return (new MathBox(width, ascent, descent, [new PlacedBox(radicand, advance, 0)], items), symbol.Height);
    }

    /// <summary>
    ///     Gets the inline size of a stretched radical symbol as Chrome measures it: the advance of a glyph or a size
    ///     variant, and the ink width of an assembly (whose glyphs have no shared advance), rounded out to whole CSS pixels
    ///     as the browser rounds the bounds of its glyphs.
    /// </summary>
    /// <param name="symbol">The stretched symbol.</param>
    /// <param name="size">The font size in CSS pixels.</param>
    /// <returns>The inline size in CSS pixels.</returns>
    private double Advance(StretchResult symbol, double size)
    {
        if (symbol.Kind is not (StretchKind.Assembly or StretchKind.UnicodeAssembly))
        {
            return symbol.Width;
        }

        var left = double.PositiveInfinity;
        var right = double.NegativeInfinity;
        foreach (var glyph in symbol.Glyphs)
        {
            var ink = _font.InkBounds(glyph.Font, glyph.Glyph, size);
            if (ink.Width > 0)
            {
                left = Math.Min(left, Math.Floor(glyph.X + ink.X + RoundingTolerance));
                right = Math.Max(right, Math.Ceiling(glyph.X + ink.Right - RoundingTolerance));
            }
        }

        return right > left ? right - left : symbol.Width;
    }
}
