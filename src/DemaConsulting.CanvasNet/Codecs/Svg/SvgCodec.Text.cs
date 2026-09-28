// cspell:ignore linecap linejoin dasharray dashoffset miterlimit anchor xlink href
// cspell:ignore Glyf Loca
// cspell:ignore evenodd nonzero viewbox gradientunits gradienttransform spreadmethod
// cspell:ignore userspaceonuse objectboundingbox skewx skewy tspan
// cspell:ignore rasterizing unparseable rrggbb sizeless bbox moveto multiplicatively pillarbox SMIL uncatchable formedness
// cspell:ignore unblurred premult
// cspell:ignore aliceblue antiquewhite blanchedalmond blueviolet burlywood cadetblue cornflowerblue
// cspell:ignore cornsilk darkcyan darkgoldenrod darkgray darkgreen darkgrey darkkhaki darkmagenta
// cspell:ignore darkolivegreen darkorange darkorchid darkred darksalmon darkseagreen darkslateblue
// cspell:ignore darkslategray darkslategrey darkturquoise darkviolet deeppink deepskyblue dimgray
// cspell:ignore dimgrey dodgerblue floralwhite forestgreen gainsboro ghostwhite greenyellow hotpink
// cspell:ignore indianred lavenderblush lawngreen lemonchiffon lightcoral lightcyan
// cspell:ignore lightgoldenrodyellow lightgray lightgreen lightpink lightsalmon lightseagreen
// cspell:ignore lightskyblue lightslategray lightslategrey lightsteelblue lightyellow limegreen
// cspell:ignore mediumaquamarine mediumblue mediumorchid mediumpurple mediumseagreen mediumslateblue
// cspell:ignore mediumspringgreen mediumturquoise mediumvioletred midnightblue mintcream mistyrose
// cspell:ignore navajowhite oldlace olivedrab orangered palegoldenrod palegreen paleturquoise
// cspell:ignore palevioletred papayawhip peachpuff powderblue rebeccapurple rosybrown royalblue
// cspell:ignore saddlebrown sandybrown seagreen skyblue slateblue slategray slategrey springgreen
// cspell:ignore steelblue whitesmoke yellowgreen
using System.Globalization;
using System.Numerics;
using System.Xml;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Codecs;

public static partial class SvgCodec
{
    // ================================================================================================
    // Text rendering
    // ================================================================================================

    /// <summary>
    ///     Renders a <c>text</c> element by laying out its glyphs as one combined local-space
    ///     outline, then rendering it through the same fill/stroke pipeline as any other shape.
    /// </summary>
    /// <param name="element">The <c>text</c> element.</param>
    /// <param name="state">The cascaded render state, supplying <c>font-family</c>/<c>font-size</c>/<c>text-anchor</c>.</param>
    /// <param name="transform">The accumulated transform from local space into pixel space.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="workBudget">
    ///     The shared geometry-parsing work budget (see <see cref="GeometryWorkBudget"/>), charged
    ///     with the text's character count before glyph layout begins so a pathologically long
    ///     run's per-rune outline/kerning work never starts once the budget is exceeded. Also
    ///     forwarded to <see cref="RenderShapeWithEffects"/> for its own <c>clip-path</c> child
    ///     geometry building.
    /// </param>
    /// <param name="filterWorkBudget">
    ///     The shared cumulative filter-evaluation work budget, forwarded to
    ///     <see cref="RenderShapeWithEffects"/>.
    /// </param>
    /// <param name="useDepth">The current <c>use</c>-reference nesting depth, forwarded to <see cref="RenderShapeWithEffects"/>.</param>
    /// <param name="elementDepth">The current recursion depth, forwarded to <see cref="RenderShapeWithEffects"/>.</param>
    /// <param name="markerDepth">The current <c>marker</c>-reference nesting depth, forwarded to <see cref="RenderShapeWithEffects"/>.</param>
    /// <param name="totalElements">The running total-rendered-elements count, forwarded to <see cref="RenderShapeWithEffects"/>.</param>
    /// <param name="boundsPrePassBudget">The shared cumulative bounds-pre-pass work budget, forwarded to <see cref="RenderShapeWithEffects"/>.</param>
    /// <param name="suppressEffects">
    ///     Forwarded to <see cref="RenderShapeWithEffects"/> - <see langword="true"/> when
    ///     <paramref name="element"/> is part of a <c>marker</c> element's own content, so its own
    ///     <c>filter</c>/<c>clip-path</c>/<c>mask</c> attributes (if any) are never
    ///     resolved/evaluated, per the documented "filters on marker content have no effect" scope
    ///     decision, extended uniformly to all three effects.
    /// </param>
    /// <remarks>
    ///     Silently renders nothing - never throws - when <see cref="RenderContext.Fonts"/> is
    ///     <see langword="null"/>, no entry matches <paramref name="state"/>'s <c>font-family</c>,
    ///     or the element has no text content, per this class's documented tolerant font-lookup
    ///     policy. Nested markup (for example <c>tspan</c>) is not given its own positioning: every
    ///     descendant text node's content is concatenated and laid out as one flat run, a
    ///     documented simplification.
    /// </remarks>
    private static void RenderText(
        XElement element, RenderState state, Matrix3x2 transform, RenderContext context,
        int useDepth, int elementDepth, int markerDepth, ref int totalElements,
        GeometryWorkBudget workBudget, FilterWorkBudget filterWorkBudget, BoundsPrePassWorkBudget boundsPrePassBudget,
        bool suppressEffects = false)
    {
        if (context.Fonts == null)
        {
            return;
        }

        var font = MatchFont(state.FontFamily, state.FontWeight, state.FontStyle, context.Fonts);
        if (font == null)
        {
            return;
        }

        var text = element.Value;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        // Charge the text's character count before the expensive per-rune glyph-outline/kerning
        // loop begins, so a pathologically long run throws before that work is spent
        workBudget.Charge(text.Length);

        var origin = new Vector2(
            GetFloatAttribute(element, "x", state, PercentageAxis.Horizontal),
            GetFloatAttribute(element, "y", state, PercentageAxis.Vertical));
        var glyphRunPath = BuildGlyphRunPath(text, font, state, origin);
        RenderShapeWithEffects(element, glyphRunPath, state, transform, context, useDepth, elementDepth, markerDepth, ref totalElements, workBudget, filterWorkBudget, boundsPrePassBudget, suppressEffects);
    }

    /// <summary>
    ///     Finds the first font-family name in <paramref name="fontFamily"/>'s comma-separated
    ///     list with a case-insensitive match in <paramref name="fonts"/>, then selects the
    ///     closest-matching registered face for that family via <see cref="SelectClosestFace"/>.
    /// </summary>
    /// <param name="fontFamily">The raw, possibly comma-separated, possibly quoted <c>font-family</c> value.</param>
    /// <param name="requestedWeight">The cascaded <c>font-weight</c> to match against.</param>
    /// <param name="requestedStyle">The cascaded <c>font-style</c> to match against.</param>
    /// <param name="fonts">The caller-supplied font-family-to-face-list dictionary.</param>
    /// <returns>
    ///     The selected font, or <see langword="null"/> if no family name matches (or
    ///     <paramref name="fontFamily"/> is absent/blank), or the matching family's face list is
    ///     empty.
    /// </returns>
    private static TrueTypeFont? MatchFont(
        string? fontFamily,
        int requestedWeight,
        SvgFontStyle requestedStyle,
        IReadOnlyDictionary<string, IReadOnlyList<SvgFontFace>> fonts)
    {
        if (string.IsNullOrWhiteSpace(fontFamily))
        {
            return null;
        }

        foreach (var candidate in fontFamily.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var name = candidate.Trim('\'', '"');
            foreach (var (familyName, faces) in fonts)
            {
                if (string.Equals(familyName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return SelectClosestFace(faces, requestedWeight, requestedStyle)?.Font;
                }
            }
        }

        return null;
    }

    /// <summary>
    ///     Selects the face in <paramref name="faces"/> that best matches
    ///     <paramref name="requestedWeight"/>/<paramref name="requestedStyle"/>.
    /// </summary>
    /// <param name="faces">One font-family's registered faces (never empty when called from <see cref="MatchFont"/> with a non-empty list).</param>
    /// <param name="requestedWeight">The requested numeric <c>font-weight</c>.</param>
    /// <param name="requestedStyle">The requested <c>font-style</c>.</param>
    /// <returns>The best-matching face, or <see langword="null"/> if <paramref name="faces"/> is empty.</returns>
    /// <remarks>
    ///     A deliberately simple approximation of the CSS Fonts Module Level 4 font-weight
    ///     fallback cascade - not a byte-for-byte clone of it - matching the task's explicit
    ///     "do not over-engineer" guidance. Each candidate face is scored, in priority order, by:
    ///     <list type="number">
    ///         <item>
    ///             <description>
    ///                 <b>Style match.</b> A face whose <see cref="SvgFontFace.Style"/> exactly
    ///                 equals <paramref name="requestedStyle"/> always beats one that does not,
    ///                 regardless of how close its weight is.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 <b>Weight distance.</b> Among faces tied on style match, the face whose
    ///                 <see cref="SvgFontFace.Weight"/> has the smallest absolute difference from
    ///                 <paramref name="requestedWeight"/> wins.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 <b>Boldness-side tie-break.</b> Among faces tied on both style match and
    ///                 weight distance, the face on the same "boldness side" as the request (both
    ///                 its own weight and <paramref name="requestedWeight"/> are either <c>&gt;=
    ///                 400</c> or <c>&lt; 400</c>) wins over one on the opposite side.
    ///             </description>
    ///         </item>
    ///     </list>
    ///     The first-registered face wins any remaining tie, since the running best is only
    ///     replaced by a strictly better-scoring candidate.
    /// </remarks>
    private static SvgFontFace? SelectClosestFace(IReadOnlyList<SvgFontFace> faces, int requestedWeight, SvgFontStyle requestedStyle)
    {
        SvgFontFace? best = null;
        var bestStyleMismatch = true;
        var bestWeightDistance = long.MaxValue;
        var bestBoldnessMismatch = true;

        foreach (var face in faces)
        {
            var styleMismatch = face.Style != requestedStyle;

            // Widen to long before subtracting: face.Weight and requestedWeight are caller-
            // supplied int values that can each independently be as extreme as int.MinValue/
            // int.MaxValue (an out-of-range font-weight="-2147483648" is still parsed, not
            // rejected), and int subtraction/Math.Abs can overflow (throwing OverflowException in
            // a checked context, or silently wrapping to a wrong distance in an unchecked one) for
            // such extreme pairs - long arithmetic makes the difference (and its absolute value)
            // always representable.
            var weightDistance = Math.Abs((long)face.Weight - requestedWeight);
            var boldnessMismatch = (face.Weight >= 400) != (requestedWeight >= 400);

            if (best == null
                || IsBetterFace(styleMismatch, weightDistance, boldnessMismatch, bestStyleMismatch, bestWeightDistance, bestBoldnessMismatch))
            {
                best = face;
                bestStyleMismatch = styleMismatch;
                bestWeightDistance = weightDistance;
                bestBoldnessMismatch = boldnessMismatch;
            }
        }

        return best;
    }

    /// <summary>
    ///     Compares a candidate face's match quality against the running-best face's, per the
    ///     three-part priority order documented on <see cref="SelectClosestFace"/>.
    /// </summary>
    /// <returns><see langword="true"/> if the candidate strictly beats the running best.</returns>
    private static bool IsBetterFace(
        bool candidateStyleMismatch,
        long candidateWeightDistance,
        bool candidateBoldnessMismatch,
        bool bestStyleMismatch,
        long bestWeightDistance,
        bool bestBoldnessMismatch)
    {
        if (candidateStyleMismatch != bestStyleMismatch)
        {
            return bestStyleMismatch && !candidateStyleMismatch;
        }

        if (candidateWeightDistance != bestWeightDistance)
        {
            return candidateWeightDistance < bestWeightDistance;
        }

        return bestBoldnessMismatch && !candidateBoldnessMismatch;
    }

    /// <summary>
    ///     Lays out <paramref name="text"/> as one combined local-space glyph-outline path,
    ///     starting at <paramref name="origin"/>, applying <paramref name="state"/>'s
    ///     <c>font-size</c>/<c>text-anchor"</c> and the font's own kerning.
    /// </summary>
    /// <param name="text">The text to lay out.</param>
    /// <param name="font">The matched font.</param>
    /// <param name="state">The cascaded render state.</param>
    /// <param name="origin">The text element's <c>x</c>/<c>y</c> anchor position, in local space.</param>
    /// <returns>
    ///     The combined local-space path (empty if every glyph in <paramref name="text"/> has no
    ///     contour data, for example an all-whitespace run).
    /// </returns>
    private static Path BuildGlyphRunPath(string text, TrueTypeFont font, RenderState state, Vector2 origin)
    {
        var scale = state.FontSize / font.UnitsPerEm;
        var totalAdvance = MeasureTextAdvance(text, font, scale);
        var anchorOffset = state.TextAnchor switch
        {
            TextAnchor.Middle => totalAdvance / 2f,
            TextAnchor.End => totalAdvance,
            _ => 0f
        };

        var builder = new PathBuilder();
        var pen = origin.X - anchorOffset;
        int? previousGlyph = null;

        foreach (var rune in text.EnumerateRunes())
        {
            var glyphIndex = font.GetGlyphIndex(rune.Value);
            if (previousGlyph.HasValue)
            {
                pen += font.GetKerning(previousGlyph.Value, glyphIndex) * scale;
            }

            var outline = font.GetGlyphOutline(glyphIndex);
            if (outline.Subpaths.Count > 0)
            {
                // The font's own outline is Y-up, in design units - flipping the y-axis scale
                // maps it into this codec's Y-down pixel-space convention in the same step as
                // applying the font-size scale and positioning the glyph's origin at the pen
                var glyphMatrix = Matrix3x2.CreateScale(scale, -scale) * Matrix3x2.CreateTranslation(pen, origin.Y);
                AppendTransformedPathInto(builder, outline, glyphMatrix);
            }

            pen += font.GetAdvanceWidth(glyphIndex) * scale;
            previousGlyph = glyphIndex;
        }

        return builder.Build();
    }

    /// <summary>Measures a text run's total advance width, including inter-glyph kerning.</summary>
    /// <param name="text">The text to measure.</param>
    /// <param name="font">The font to measure with.</param>
    /// <param name="scale">The font-design-units-to-local-space scale factor.</param>
    /// <returns>The total advance width, in local-space units.</returns>
    private static float MeasureTextAdvance(string text, TrueTypeFont font, float scale)
    {
        var total = 0f;
        int? previousGlyph = null;

        foreach (var rune in text.EnumerateRunes())
        {
            var glyphIndex = font.GetGlyphIndex(rune.Value);
            if (previousGlyph.HasValue)
            {
                total += font.GetKerning(previousGlyph.Value, glyphIndex) * scale;
            }

            total += font.GetAdvanceWidth(glyphIndex) * scale;
            previousGlyph = glyphIndex;
        }

        return total;
    }
}
