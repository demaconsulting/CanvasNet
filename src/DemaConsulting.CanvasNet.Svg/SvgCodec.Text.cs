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
    ///     Renders a <c>text</c> element: lays it out as styled, positioned runs (see
    ///     <see cref="LayoutText"/>) and renders them through the same fill/stroke and effects
    ///     pipeline as any other shape.
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
    ///     Silently renders nothing when the element has no character content. The font is always
    ///     resolved (see <see cref="ResolveFont"/>): a caller-registered font first, then
    ///     installed/generic/bundled defaults, so text is never dropped for want of a registered
    ///     font. Each <c>tspan</c>/<c>a</c> run is painted with its own cascaded state, and a paint
    ///     server or effect uses the bounding box of the whole text. A text that lays out as one
    ///     run of the <c>text</c> element itself takes exactly the call it always took, so plain
    ///     text renders as before. <c>rotate</c>, <c>textLength</c>, spacing, vertical writing
    ///     and the other features the layout ignores are reported (see <see cref="SvgUnsupportedFeature"/>),
    ///     as is text whose coordinates are beyond the supported range (the shape renderer skips it).
    /// </remarks>
    private static void RenderText(
        XElement element, RenderState state, Matrix3x2 transform, RenderContext context,
        RenderScope scope, ref int totalElements)
    {
        var characterCount = CountTextCharacters(element);
        if (characterCount == 0)
        {
            return;
        }

        // Charge the text's character count before the expensive per-rune glyph-outline/kerning
        // loop begins, so a pathologically long run throws before that work is spent
        scope.WorkBudget.Charge(characterCount);

        var runs = LayoutText(element, state, context, report: true);

        // Nothing to paint (white space, or a zero font size): the empty outline still goes through the effects
        // pipeline, exactly as before the run layout, so a filter with its own region behaves the same
        if (runs.Count == 0)
        {
            RenderShapeWithEffects(element, Path.Empty, state, transform, context, scope, ref totalElements);
            return;
        }

        var combined = CombineTextRuns(runs);
        if (context.Unsupported is { } collector && !IsBoundsWithinCoordinateMagnitude(combined.GetBounds().Transform(transform)))
        {
            collector.Add(SvgUnsupportedFeature.TextNotDrawnCode, "coordinates beyond the supported range", element);
        }

        // One run of the text element itself is a plain text: the call it always took
        if (runs.Count == 1 && ReferenceEquals(runs[0].Element, element))
        {
            RenderShapeWithEffects(element, combined, state, transform, context, scope, ref totalElements);
            return;
        }

        RenderShapeWithEffects(element, combined, state, transform, context, scope, ref totalElements, runs);
    }

    /// <summary>
    ///     Determines whether the corners of a pixel-space box are finite and within <see cref="MaxCoordinateMagnitude"/>,
    ///     the same limit <see cref="RenderShape"/> applies to every point of a shape.
    /// </summary>
    /// <param name="pixelBounds">The pixel-space bounds of a shape.</param>
    /// <returns><see langword="true"/> when the whole box is within the limit.</returns>
    private static bool IsBoundsWithinCoordinateMagnitude(Rect pixelBounds) =>
        IsFiniteAndWithinCoordinateMagnitude(pixelBounds.TopLeft) && IsFiniteAndWithinCoordinateMagnitude(pixelBounds.BottomRight);

    /// <summary>
    ///     Finds the first font-family name in <paramref name="fontFamily"/>'s CSS family list
    ///     with a case-insensitive match in the caller-registered <paramref name="fonts"/>, then selects the
    ///     closest-matching registered face for that family via <see cref="SelectClosestFace"/>.
    ///     The list is split by <see cref="GenericFontFamilies.SplitFamilyList"/> - the same splitter
    ///     the default resolution uses - so a comma inside a quoted name (<c>"Acme, Sans"</c>) does
    ///     not split it and both passes agree on the candidate names.
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

        foreach (var name in GenericFontFamilies.SplitFamilyList(fontFamily))
        {
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
    ///     Outlines one run of text as a combined local-space glyph-outline path, starting at
    ///     <paramref name="origin"/>, aligned per <paramref name="anchor"/>, with the font's own kerning.
    /// </summary>
    /// <param name="text">The text to lay out.</param>
    /// <param name="font">The matched font.</param>
    /// <param name="fontSize">The cascaded <c>font-size</c>.</param>
    /// <param name="anchor">The <c>text-anchor</c> to align the run by around <paramref name="origin"/>.</param>
    /// <param name="origin">The run's anchor position, in local space.</param>
    /// <returns>
    ///     The combined local-space path (empty if every glyph in <paramref name="text"/> has no
    ///     contour data, for example an all-whitespace run, or if <paramref name="fontSize"/> is
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
    private static Path BuildGlyphRunPath(string text, TrueTypeFont font, float fontSize, TextAnchor anchor, Vector2 origin)
    {
        // Non-positive (or non-finite) font sizes draw nothing, per the remarks above
        if (!(fontSize > 0f) || !float.IsFinite(fontSize))
        {
            return Path.Empty;
        }

        var align = anchor switch
        {
            TextAnchor.Middle => Rendering.TextAlign.Center,
            TextAnchor.End => Rendering.TextAlign.Right,
            _ => Rendering.TextAlign.Left
        };

        return Rendering.TextRenderer.CreateTextPath(text, font, fontSize, origin.X, origin.Y, align);
    }
}
