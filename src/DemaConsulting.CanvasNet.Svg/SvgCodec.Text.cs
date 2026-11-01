// cspell:ignore linecap linejoin dasharray dashoffset miterlimit anchor xlink href
// cspell:ignore Glyf Loca
// cspell:ignore evenodd nonzero viewbox gradientunits gradienttransform spreadmethod
// cspell:ignore userspaceonuse objectboundingbox skewx skewy tspan
// cspell:ignore rasterizing unparseable rrggbb sizeless bbox moveto multiplicatively pillarbox SMIL uncatchable formedness
// cspell:ignore unblurred premult
using System.Globalization;
using System.Numerics;
using System.Xml;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Svg;

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
    /// <param name="scope">
    ///     The recursion depths and budgets, forwarded to <see cref="RenderShapeWithEffects"/>.
    ///     Its geometry work budget (see <see cref="GeometryWorkBudget"/>) is charged with the
    ///     text's character count before glyph layout begins so a pathologically long run's
    ///     per-rune outline/kerning work never starts once the budget is exceeded. A marker depth
    ///     above zero means <paramref name="element"/> is part of a <c>marker</c> element's own
    ///     content, so its own <c>filter</c>/<c>clip-path</c>/<c>mask</c> attributes (if any) are
    ///     never resolved/evaluated, per the documented "filters on marker content have no effect"
    ///     scope decision, extended uniformly to all three effects.
    /// </param>
    /// <param name="totalElements">The running total-rendered-elements count, forwarded to <see cref="RenderShapeWithEffects"/>.</param>
    /// <remarks>
    ///     Silently renders nothing - never throws - when the element has no text content. The font
    ///     is always resolved (see <see cref="ResolveFont"/>): a caller-registered font first, then
    ///     installed/generic/bundled defaults, so text is never dropped for want of a registered
    ///     font. Nested markup (for example <c>tspan</c>) is not given its own positioning: every
    ///     descendant text node's content is concatenated and laid out as one flat run, a
    ///     documented simplification.
    /// </remarks>
    private static void RenderText(
        XElement element, RenderState state, Matrix3x2 transform, RenderContext context,
        RenderScope scope, ref int totalElements)
    {
        var text = element.Value;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var font = ResolveFont(context, state.FontFamily, state.FontWeight, state.FontStyle);

        // Charge the text's character count before the expensive per-rune glyph-outline/kerning
        // loop begins, so a pathologically long run throws before that work is spent
        scope.WorkBudget.Charge(text.Length);

        var origin = new Vector2(
            GetFloatAttribute(element, "x", state, PercentageAxis.Horizontal),
            GetFloatAttribute(element, "y", state, PercentageAxis.Vertical));
        var glyphRunPath = BuildGlyphRunPath(text, font, state, origin);
        RenderShapeWithEffects(element, glyphRunPath, state, transform, context, scope, ref totalElements);
    }

    /// <summary>
    ///     Finds the first font-family name in <paramref name="fontFamily"/>'s comma-separated
    ///     list with a case-insensitive match in the caller-registered <paramref name="fonts"/>, then selects the
    ///     closest-matching registered face for that family via <see cref="SelectClosestFace"/>.
    ///     This is step 1 of <see cref="ResolveFont"/>; a <see langword="null"/> result there means
    ///     the default (installed/generic/bundled) resolution applies.
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
                if (!string.Equals(familyName, name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // A matching family with an empty face list is unavailable: keep looking, so a
                // later registered family in the list can still be used
                var selected = SelectClosestFace(faces, requestedWeight, requestedStyle);
                if (selected != null)
                {
                    return selected.Value.Font;
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
    ///     contour data, for example an all-whitespace run, or if the cascaded <c>font-size</c> is
    ///     not a positive finite number).
    /// </returns>
    /// <remarks>
    ///     A thin adapter over <c>Rendering.TextRenderer.CreateTextPath</c>, which owns the
    ///     advance, kerning, alignment, and y-flip rules. This method adds only the two SVG
    ///     specifics: the mapping of <c>text-anchor</c> to a text alignment, and the rule that a
    ///     zero or negative <c>font-size</c> renders nothing (the core method rejects a
    ///     non-positive size, and a negative size is invalid per CSS/SVG), so a document with such
    ///     a value degrades to blank text instead of failing.
    /// </remarks>
    private static Path BuildGlyphRunPath(string text, TrueTypeFont font, RenderState state, Vector2 origin)
    {
        // Non-positive (or non-finite) font sizes draw nothing, per the remarks above
        if (!(state.FontSize > 0f) || !float.IsFinite(state.FontSize))
        {
            return Path.Empty;
        }

        var align = state.TextAnchor switch
        {
            TextAnchor.Middle => Rendering.TextAlign.Center,
            TextAnchor.End => Rendering.TextAlign.Right,
            _ => Rendering.TextAlign.Left
        };

        return Rendering.TextRenderer.CreateTextPath(text, font, state.FontSize, origin.X, origin.Y, align);
    }
}
