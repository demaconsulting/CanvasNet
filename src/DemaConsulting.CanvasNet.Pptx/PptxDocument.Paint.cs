using System.Globalization;
using System.Numerics;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore xfrm srgbclr schemeclr lummod lumoff pptx gsLst prstDash lgDash sysDash sysDot dashDot patt hlink scrgb prst positionable

/// <summary>
///     Implements the <see cref="PptxDocument"/> DrawingML paint resolvers (Phase 1c): fill
///     (<c>&lt;a:noFill&gt;</c>/<c>&lt;a:solidFill&gt;</c>/<c>&lt;a:gradFill&gt;</c>), color
///     (<c>&lt;a:srgbClr&gt;</c>/<c>&lt;a:sysClr&gt;</c>/<c>&lt;a:schemeClr&gt;</c> plus the
///     <c>lumMod</c>/<c>lumOff</c>/<c>shade</c>/<c>tint</c>/<c>alpha</c> color-transform chain),
///     and line/stroke resolution (<c>&lt;a:ln&gt;</c>) - see <c>pptx-document.md</c>'s "Geometry
///     and Paint (Phase 1c)" design section for the full color-transform math and the
///     supported/deferred fill-kind boundary.
/// </summary>
public sealed partial class PptxDocument
{
    /// <summary>
    ///     Resolves a shape's fill: the single recognized fill-definition child
    ///     (<c>&lt;a:noFill&gt;</c>/<c>&lt;a:solidFill&gt;</c>/<c>&lt;a:gradFill&gt;</c>/
    ///     <c>&lt;a:pattFill&gt;</c>/<c>&lt;a:blipFill&gt;</c>) of <paramref name="fillParentElement"/>,
    ///     into a concrete <see cref="PptxPaint"/>.
    /// </summary>
    /// <param name="fillParentElement">
    ///     The element that may directly contain a fill-definition child - typically a shape's
    ///     <c>&lt;p:spPr&gt;</c>, or an <c>&lt;a:ln&gt;</c> line-properties element (both declare
    ///     their fill the same way).
    /// </param>
    /// <param name="theme">The resolved theme, used to resolve any <c>&lt;a:schemeClr&gt;</c>.</param>
    /// <param name="widthEmu">The owning shape's own declared width, in EMU (needed to position a gradient fill).</param>
    /// <param name="heightEmu">The owning shape's own declared height, in EMU (needed to position a gradient fill).</param>
    /// <param name="phClrOverride">
    ///     The concrete color to substitute for an <c>&lt;a:schemeClr val="phClr"/&gt;</c>
    ///     ("placeholder color") token, or <see langword="null"/> (the default) to preserve the
    ///     existing <see cref="PptxColorScheme.Dark1"/> fallback - see
    ///     <see cref="ResolveColor"/>'s remarks. Supplied by
    ///     <see cref="ResolveSlideBackgroundFill"/> when resolving a <c>&lt;p:bgRef&gt;</c>'s own
    ///     theme format-scheme fill-style entry, which is always <c>phClr</c>-templated; every
    ///     other, pre-existing call site omits this parameter, leaving its behavior unchanged.
    /// </param>
    /// <param name="colorMap">
    ///     The effective <c>&lt;p:clrMap&gt;</c>/<c>&lt;p:clrMapOvr&gt;</c> color map consulted
    ///     when resolving an <c>&lt;a:schemeClr val="bg1"/&gt;</c>-shaped token, or
    ///     <see langword="null"/> (the default, resolving to <see cref="PptxColorMap.Default"/>)
    ///     to preserve this unit's pre-existing hardcoded bg/tx aliasing - see
    ///     <see cref="ResolveSchemeColor"/>'s remarks.
    /// </param>
    /// <param name="resolveBlipImage">
    ///     Lazily invoked, only when <paramref name="fillParentElement"/> declares an
    ///     <c>&lt;a:blipFill&gt;</c> child, to decode that element's own <c>&lt;a:blip&gt;</c>
    ///     relationship into a concrete <see cref="Surface"/> - typically a closure over
    ///     <see cref="ResolvePictureSurface"/>, bound to the owning part's own path (an
    ///     <c>&lt;a:blipFill&gt;</c>'s <c>r:embed</c> relationship is part-scoped, so this method,
    ///     which has no owning-part context of its own, cannot resolve it directly). Defaults to
    ///     <see langword="null"/> ("no resolver available"), preserving this method's pre-existing
    ///     behavior (throwing <see cref="PptxUnsupportedFeatureException"/>) for every call site
    ///     that does not supply one - see this method's own <c>&lt;exception&gt;</c> documentation.
    /// </param>
    /// <returns>
    ///     The resolved <see cref="PptxPaint"/> - <see cref="PptxNoFill.Instance"/> when
    ///     <paramref name="fillParentElement"/> is <see langword="null"/>, declares an explicit
    ///     <c>&lt;a:noFill/&gt;</c>, declares no recognized fill-definition child at all (a
    ///     deliberate simplification: this phase does not resolve fill inheritance from a
    ///     placeholder/layout/master/theme format scheme - see the design document), or declares a
    ///     non-conformant, attribute-less <c>&lt;a:pattFill/&gt;</c> with no <c>prst</c> attribute
    ///     (observed in the project's own real <c>pythonpptx-dml-fill.pptx</c> fixture - treated
    ///     as "no override", not a named preset); a resolved <see cref="PptxPatternFill"/> when it
    ///     declares an <c>&lt;a:pattFill prst="..."/&gt;</c> naming a covered
    ///     <see cref="PptxPresetPattern"/> (see that enum's own remarks for the full covered/
    ///     deferred preset-name boundary); a resolved <see cref="PptxImageFill"/> when it declares
    ///     an <c>&lt;a:blipFill&gt;</c> and <paramref name="resolveBlipImage"/> is supplied
    ///     (non-<see langword="null"/>).
    /// </returns>
    /// <exception cref="PptxUnsupportedFeatureException">
    ///     Thrown when the recognized fill-definition child is <c>&lt;a:pattFill prst="..."/&gt;</c>
    ///     naming a preset not covered by <see cref="PptxPresetPattern"/> (pattern fill - every
    ///     other preset name is deferred to a later phase, see the design document's rationale and
    ///     that enum's own remarks), or when it is <c>&lt;a:blipFill&gt;</c> (picture fill) and
    ///     <paramref name="resolveBlipImage"/> is <see langword="null"/> (no resolver supplied);
    ///     otherwise propagated unchanged from <paramref name="resolveBlipImage"/> itself - see
    ///     <see cref="ResolvePictureSurface"/>'s own <c>&lt;exception&gt;</c> documentation for
    ///     every cause (a linked, non-embedded image; an SVG-only fallback blip; or an unsupported
    ///     raster image format).
    /// </exception>
    internal static PptxPaint ResolveFill(
        XElement? fillParentElement, PptxTheme theme, float widthEmu, float heightEmu, Rgba32? phClrOverride = null,
        PptxColorMap? colorMap = null, Func<XElement, Surface>? resolveBlipImage = null)
    {
        colorMap ??= PptxColorMap.Default;

        if (fillParentElement is null)
        {
            return PptxNoFill.Instance;
        }

        if (fillParentElement.Element(DrawingNamespace + "noFill") is not null)
        {
            return PptxNoFill.Instance;
        }

        var solidFill = fillParentElement.Element(DrawingNamespace + "solidFill");
        if (solidFill is not null)
        {
            var colorElement = solidFill.Elements().FirstOrDefault() ??
                throw new InvalidDataException("An <a:solidFill> element has no color-definition child.");
            return new PptxSolidFill(ResolveColor(colorElement, theme, phClrOverride, colorMap));
        }

        var gradFill = fillParentElement.Element(DrawingNamespace + "gradFill");
        if (gradFill is not null)
        {
            return ResolveGradientFill(gradFill, theme, widthEmu, heightEmu, phClrOverride, colorMap);
        }

        var pattFill = fillParentElement.Element(DrawingNamespace + "pattFill");
        if (pattFill is not null)
        {
            return ResolvePatternFill(pattFill, theme, phClrOverride, colorMap);
        }

        var blipFill = fillParentElement.Element(DrawingNamespace + "blipFill");
        if (blipFill is not null)
        {
            if (resolveBlipImage is null)
            {
                throw new PptxUnsupportedFeatureException("pptx-picture-fill", "Picture fill (<a:blipFill>) is not supported.");
            }

            var image = resolveBlipImage(blipFill);
            var transform = ResolveImageFillTransform(blipFill, image, widthEmu, heightEmu);
            return new PptxImageFill(image, transform);
        }

        return PptxNoFill.Instance;
    }

    /// <summary>
    ///     Resolves an <c>&lt;a:pattFill&gt;</c> element into either <see cref="PptxNoFill.Instance"/>
    ///     (a non-conformant, attribute-less <c>&lt;a:pattFill/&gt;</c> with no <c>prst</c>
    ///     attribute - see <see cref="ResolveFill"/>'s own remarks) or a resolved
    ///     <see cref="PptxPatternFill"/> for a covered <see cref="PptxPresetPattern"/>.
    /// </summary>
    /// <param name="pattFillElement">The <c>&lt;a:pattFill&gt;</c> element.</param>
    /// <param name="theme">The resolved theme, used to resolve the <c>&lt;a:fgClr&gt;</c>/<c>&lt;a:bgClr&gt;</c> colors.</param>
    /// <param name="phClrOverride">
    ///     The concrete color to substitute for an <c>&lt;a:schemeClr val="phClr"/&gt;</c> token
    ///     in either color, or <see langword="null"/> (the default) - see <see cref="ResolveFill"/>'s
    ///     matching parameter.
    /// </param>
    /// <param name="colorMap">
    ///     The effective color map consulted when either color declares an <c>&lt;a:schemeClr
    ///     val="bg1"/&gt;</c>-shaped token - see <see cref="ResolveFill"/>'s matching parameter.
    /// </param>
    /// <returns>
    ///     <see cref="PptxNoFill.Instance"/> when <paramref name="pattFillElement"/> has no
    ///     <c>prst</c> attribute; otherwise a resolved <see cref="PptxPatternFill"/>, defaulting an
    ///     absent <c>&lt;a:fgClr&gt;</c> to black and an absent <c>&lt;a:bgClr&gt;</c> to white
    ///     (both a documented, OOXML-adjacent convention - every real-world preset fill this
    ///     project has encountered declares both explicitly).
    /// </returns>
    /// <exception cref="PptxUnsupportedFeatureException">
    ///     Thrown when <c>prst</c> names a preset not covered by <see cref="PptxPresetPattern"/>
    ///     (feature token <c>"pptx-pattern-fill"</c>) - see that enum's own remarks for the full
    ///     covered/deferred preset-name boundary.
    /// </exception>
    private static PptxPaint ResolvePatternFill(
        XElement pattFillElement, PptxTheme theme, Rgba32? phClrOverride, PptxColorMap? colorMap)
    {
        var prst = (string?)pattFillElement.Attribute("prst");
        if (prst is null)
        {
            // Non-conformant, attribute-less <a:pattFill/> (observed in the project's own real
            // pythonpptx-dml-fill.pptx fixture) - treated as "no override", not a named preset.
            return PptxNoFill.Instance;
        }

        if (!TryResolvePresetPattern(prst, out var preset))
        {
            throw new PptxUnsupportedFeatureException(
                "pptx-pattern-fill", $"Pattern fill preset '{prst}' is not supported.");
        }

        var fgColorElement = pattFillElement.Element(DrawingNamespace + "fgClr")?.Elements().FirstOrDefault();
        var bgColorElement = pattFillElement.Element(DrawingNamespace + "bgClr")?.Elements().FirstOrDefault();
        var foreground = fgColorElement is null
            ? new Rgba32(0, 0, 0, 255)
            : ResolveColor(fgColorElement, theme, phClrOverride, colorMap);
        var background = bgColorElement is null
            ? new Rgba32(255, 255, 255, 255)
            : ResolveColor(bgColorElement, theme, phClrOverride, colorMap);

        return new PptxPatternFill(preset, foreground, background);
    }

    /// <summary>
    ///     Maps an <c>&lt;a:pattFill prst="..."/&gt;</c> attribute value to its matching
    ///     <see cref="PptxPresetPattern"/>, when covered - see that enum's own remarks for the
    ///     full covered/deferred preset-name boundary.
    /// </summary>
    /// <param name="prst">The <c>prst</c> attribute's string value (an ECMA-376 <c>ST_PresetPatternVal</c> name).</param>
    /// <param name="preset">The resolved <see cref="PptxPresetPattern"/>, when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="prst"/> names a covered preset; otherwise <see langword="false"/>.</returns>
    private static bool TryResolvePresetPattern(string prst, out PptxPresetPattern preset)
    {
        switch (prst)
        {
            case "horz": preset = PptxPresetPattern.Horz; return true;
            case "vert": preset = PptxPresetPattern.Vert; return true;
            case "ltHorz": preset = PptxPresetPattern.LtHorz; return true;
            case "ltVert": preset = PptxPresetPattern.LtVert; return true;
            case "dkHorz": preset = PptxPresetPattern.DkHorz; return true;
            case "dkVert": preset = PptxPresetPattern.DkVert; return true;
            case "dnDiag": preset = PptxPresetPattern.DnDiag; return true;
            case "upDiag": preset = PptxPresetPattern.UpDiag; return true;
            case "ltDnDiag": preset = PptxPresetPattern.LtDnDiag; return true;
            case "ltUpDiag": preset = PptxPresetPattern.LtUpDiag; return true;
            case "dkDnDiag": preset = PptxPresetPattern.DkDnDiag; return true;
            case "dkUpDiag": preset = PptxPresetPattern.DkUpDiag; return true;
            case "wdDnDiag": preset = PptxPresetPattern.WdDnDiag; return true;
            case "wdUpDiag": preset = PptxPresetPattern.WdUpDiag; return true;
            case "cross": preset = PptxPresetPattern.Cross; return true;
            case "diagCross": preset = PptxPresetPattern.DiagCross; return true;
            case "pct5": preset = PptxPresetPattern.Pct5; return true;
            case "pct10": preset = PptxPresetPattern.Pct10; return true;
            case "pct20": preset = PptxPresetPattern.Pct20; return true;
            case "pct25": preset = PptxPresetPattern.Pct25; return true;
            case "pct30": preset = PptxPresetPattern.Pct30; return true;
            case "pct40": preset = PptxPresetPattern.Pct40; return true;
            case "pct50": preset = PptxPresetPattern.Pct50; return true;
            case "pct60": preset = PptxPresetPattern.Pct60; return true;
            case "pct70": preset = PptxPresetPattern.Pct70; return true;
            case "pct75": preset = PptxPresetPattern.Pct75; return true;
            case "pct80": preset = PptxPresetPattern.Pct80; return true;
            case "pct90": preset = PptxPresetPattern.Pct90; return true;
            case "divot": preset = PptxPresetPattern.Divot; return true;
            case "wave": preset = PptxPresetPattern.Wave; return true;
            default:
                preset = default;
                return false;
        }
    }

    /// <summary>
    ///     Resolves an <c>&lt;a:gradFill&gt;</c> element's <c>&lt;a:gsLst&gt;</c> gradient stops
    ///     and <c>&lt;a:lin&gt;</c> linear direction into a <see cref="PptxGradientFill"/>.
    /// </summary>
    /// <param name="gradFillElement">The <c>&lt;a:gradFill&gt;</c> element.</param>
    /// <param name="theme">The resolved theme, used to resolve any stop's <c>&lt;a:schemeClr&gt;</c>.</param>
    /// <param name="widthEmu">The owning shape's own declared width, in EMU.</param>
    /// <param name="heightEmu">The owning shape's own declared height, in EMU.</param>
    /// <param name="phClrOverride">
    ///     The concrete color to substitute for each stop's <c>&lt;a:schemeClr val="phClr"/&gt;</c>
    ///     token, or <see langword="null"/> (the default) - see <see cref="ResolveFill"/>'s
    ///     matching parameter.
    /// </param>
    /// <param name="colorMap">
    ///     The effective color map consulted when resolving a stop's <c>&lt;a:schemeClr
    ///     val="bg1"/&gt;</c>-shaped token, or <see langword="null"/> (the default) - see
    ///     <see cref="ResolveFill"/>'s matching parameter.
    /// </param>
    /// <returns>The resolved <see cref="PptxGradientFill"/>, positioned in the shape's own local geometry coordinate space.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="gradFillElement"/> has no <c>&lt;a:gsLst&gt;</c>, or
    ///     <c>&lt;a:gsLst&gt;</c> has no <c>&lt;a:gs&gt;</c> children, or a <c>&lt;a:gs&gt;</c> has
    ///     a missing, non-numeric, or non-finite <c>pos</c> attribute, or no color-definition
    ///     child.
    /// </exception>
    /// <exception cref="PptxUnsupportedFeatureException">
    ///     Thrown when <paramref name="gradFillElement"/> declares a path gradient
    ///     (<c>&lt;a:path&gt;</c>) rather than a linear one (<c>&lt;a:lin&gt;</c>), or declares
    ///     neither - both radial/path gradients and the (rare) gradient with no direction element
    ///     at all are deferred to a later phase (feature token <c>"pptx-gradient-path"</c>).
    /// </exception>
    internal static PptxGradientFill ResolveGradientFill(
        XElement gradFillElement, PptxTheme theme, float widthEmu, float heightEmu, Rgba32? phClrOverride = null,
        PptxColorMap? colorMap = null)
    {
        colorMap ??= PptxColorMap.Default;

        var gsLst = gradFillElement.Element(DrawingNamespace + "gsLst") ??
            throw new InvalidDataException("An <a:gradFill> element has no <a:gsLst> element.");

        var gsElements = gsLst.Elements(DrawingNamespace + "gs").ToList();
        if (gsElements.Count == 0)
        {
            throw new InvalidDataException("An <a:gradFill>'s <a:gsLst> element has no <a:gs> children.");
        }

        var stops = new List<GradientStop>(gsElements.Count);
        foreach (var gs in gsElements)
        {
            var posValue = (string?)gs.Attribute("pos") ??
                throw new InvalidDataException("An <a:gs> element has no 'pos' attribute.");
            if (!float.TryParse(posValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var pos))
            {
                throw new InvalidDataException($"An <a:gs> element has a non-numeric 'pos' attribute value '{posValue}'.");
            }

            if (!float.IsFinite(pos))
            {
                throw new InvalidDataException($"An <a:gs> element has a non-finite 'pos' attribute value '{posValue}'.");
            }

            var colorElement = gs.Elements().FirstOrDefault() ??
                throw new InvalidDataException("An <a:gs> element has no color-definition child.");
            var offset = Math.Clamp(pos / 100000f, 0f, 1f);
            stops.Add(new GradientStop(offset, ResolveColor(colorElement, theme, phClrOverride, colorMap)));
        }

        if (gradFillElement.Element(DrawingNamespace + "lin") is not { } lin)
        {
            throw new PptxUnsupportedFeatureException(
                "pptx-gradient-path",
                "Only a linear gradient (<a:lin>) is supported; radial/path gradients (<a:path>) are not.");
        }

        var ang60000ths = ParseOptionalIntAttribute(lin, "ang") ?? 0;
        var angleRadians = ang60000ths / 60000f * (MathF.PI / 180f);
        var direction = new Vector2(MathF.Cos(angleRadians), MathF.Sin(angleRadians));
        var extent = (MathF.Abs(direction.X) * widthEmu + MathF.Abs(direction.Y) * heightEmu) / 2f;
        var center = new Vector2(widthEmu / 2f, heightEmu / 2f);

        var gradient = new LinearGradient(center - direction * extent, center + direction * extent, stops);
        return new PptxGradientFill(gradient);
    }

    /// <summary>
    ///     Resolves a single color-definition element
    ///     (<c>&lt;a:srgbClr&gt;</c>/<c>&lt;a:sysClr&gt;</c>/<c>&lt;a:schemeClr&gt;</c>/
    ///     <c>&lt;a:prstClr&gt;</c>) and its child color-transform chain into a concrete
    ///     <see cref="Rgba32"/> value.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     <c>&lt;a:schemeClr val="..."/&gt;</c>'s 12 ordinary slot names (<c>dk1</c>/<c>lt1</c>/
    ///     <c>dk2</c>/<c>lt2</c>/<c>accent1-6</c>/<c>hlink</c>/<c>folHlink</c>) map directly to
    ///     <see cref="PptxColorScheme"/>'s like-named property; the four special aliases
    ///     <c>bg1</c>/<c>tx1</c>/<c>bg2</c>/<c>tx2</c> map to <c>lt1</c>/<c>dk1</c>/<c>lt2</c>/
    ///     <c>dk2</c> respectively (per ECMA-376's documented background/text-color aliasing),
    ///     and <c>phClr</c> ("placeholder color" - a shape-style-reference token meaning "whatever
    ///     color this shape's style already resolved to") resolves to <paramref name="phClrOverride"/>
    ///     when supplied (the one caller that threads shape-style-reference context through -
    ///     <see cref="ResolveSlideBackgroundFill"/>'s <c>&lt;p:bgRef&gt;</c> resolution); every
    ///     other call site - which does not have that surrounding context available - omits
    ///     <paramref name="phClrOverride"/>, so <c>phClr</c> resolves to
    ///     <see cref="PptxColorScheme.Dark1"/> as a deterministic, documented fallback.
    ///     </para>
    ///     <para>
    ///     The color-transform chain is applied in a fixed pipeline order regardless of the
    ///     transform elements' own order in the XML: <c>lumMod</c>/<c>lumOff</c> together (in HSL
    ///     luminance space: <c>L' = L * lumMod + lumOff</c>, each as a <c>val/100000</c>
    ///     fraction), then <c>shade</c> (RGB-space: <c>c' = c * val/100000</c>), then <c>tint</c>
    ///     (RGB-space: <c>c' = c * val/100000 + 255 * (1 - val/100000)</c>), then <c>alpha</c>
    ///     (<c>a' = val/100000 * 255</c>, replacing the alpha channel outright). This is a
    ///     deliberate, documented simplification of OOXML's true colorimetric transform model -
    ///     see the design document.
    ///     </para>
    /// </remarks>
    /// <param name="colorElement">The color-definition element.</param>
    /// <param name="theme">The resolved theme, used to resolve an <c>&lt;a:schemeClr&gt;</c>'s named slot.</param>
    /// <param name="phClrOverride">
    ///     The concrete color to substitute for an <c>&lt;a:schemeClr val="phClr"/&gt;</c> token,
    ///     or <see langword="null"/> (the default) to preserve the existing
    ///     <see cref="PptxColorScheme.Dark1"/> fallback documented below. Every pre-existing call
    ///     site omits this parameter, leaving its behavior unchanged; only
    ///     <see cref="ResolveSlideBackgroundFill"/>'s <c>&lt;p:bgRef&gt;</c> resolution supplies
    ///     it.
    /// </param>
    /// <param name="colorMap">
    ///     The effective <c>&lt;p:clrMap&gt;</c>/<c>&lt;p:clrMapOvr&gt;</c> color map consulted
    ///     when <paramref name="colorElement"/> is an <c>&lt;a:schemeClr val="bg1"/&gt;</c>-,
    ///     <c>"tx1"</c>-, <c>"bg2"</c>-, or <c>"tx2"</c>-shaped token, or <see langword="null"/>
    ///     (the default, resolving to <see cref="PptxColorMap.Default"/>) to preserve this unit's
    ///     pre-existing hardcoded bg/tx aliasing - see <see cref="ResolveSchemeColor"/>'s remarks.
    /// </param>
    /// <returns>The resolved, fully color-transformed <see cref="Rgba32"/> value.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="colorElement"/> is an <c>&lt;a:srgbClr&gt;</c>/
    ///     <c>&lt;a:sysClr&gt;</c>/<c>&lt;a:schemeClr&gt;</c>/<c>&lt;a:prstClr&gt;</c> missing its
    ///     required <c>val</c> attribute (or, for <c>&lt;a:sysClr&gt;</c>, missing
    ///     <c>lastClr</c>), has an invalid hex value, or names an unrecognized
    ///     <c>&lt;a:schemeClr val="..."/&gt;</c> slot.
    /// </exception>
    /// <exception cref="PptxUnsupportedFeatureException">
    ///     Thrown when <paramref name="colorElement"/> is a color-definition kind other than
    ///     <c>&lt;a:srgbClr&gt;</c>/<c>&lt;a:sysClr&gt;</c>/<c>&lt;a:schemeClr&gt;</c>/
    ///     <c>&lt;a:prstClr&gt;</c> (for example <c>&lt;a:scrgbClr&gt;</c>/<c>&lt;a:hslClr&gt;</c>)
    ///     - deferred to a later phase (feature token <c>"pptx-color-kind"</c>); also thrown when
    ///     <paramref name="colorElement"/> is an <c>&lt;a:prstClr val="..."/&gt;</c> naming an
    ///     <c>ST_PresetColorVal</c> other than the two names this project resolves (<c>white</c>/
    ///     <c>black</c> - see <see cref="ResolveBaseColor"/>'s own remarks for the narrowly-scoped
    ///     rationale), with the same feature token.
    /// </exception>
    internal static Rgba32 ResolveColor(
        XElement colorElement, PptxTheme theme, Rgba32? phClrOverride = null, PptxColorMap? colorMap = null)
    {
        var baseColor = ResolveBaseColor(colorElement, theme, phClrOverride, colorMap);
        return ApplyColorTransforms(baseColor, colorElement);
    }

    /// <summary>
    ///     Resolves a color-definition element's own base color, before any color-transform chain
    ///     is applied.
    /// </summary>
    /// <remarks>
    ///     <c>&lt;a:prstClr val="..."/&gt;</c> (an ECMA-376 <c>ST_PresetColorVal</c> named color -
    ///     over 140 X11/SVG-keyword-style names in the full schema) resolves only its two names
    ///     actually required by this project's own real-world fixture corpus today (<c>white</c>
    ///     and <c>black</c>) - a deliberately minimal, narrowly-scoped companion fix to pattern
    ///     fill (<see cref="PptxPatternFill"/>) support, whose own <c>&lt;a:bgClr&gt;</c> always
    ///     uses <c>&lt;a:prstClr val="white"/&gt;</c> in that fixture. Every other preset-color
    ///     name throws the same narrowed <see cref="PptxUnsupportedFeatureException"/> (feature
    ///     token <c>"pptx-color-kind"</c>) every other unsupported color-definition kind already
    ///     throws - not a full <c>ST_PresetColorVal</c> table, which remains explicitly out of
    ///     scope.
    /// </remarks>
    private static Rgba32 ResolveBaseColor(
        XElement colorElement, PptxTheme theme, Rgba32? phClrOverride = null, PptxColorMap? colorMap = null)
    {
        if (colorElement.Name == DrawingNamespace + "srgbClr")
        {
            var hex = (string?)colorElement.Attribute("val") ??
                throw new InvalidDataException("An <a:srgbClr> element has no 'val' attribute.");
            return ParseHexColor(hex, "srgbClr");
        }

        if (colorElement.Name == DrawingNamespace + "sysClr")
        {
            var hex = (string?)colorElement.Attribute("lastClr") ??
                throw new InvalidDataException("An <a:sysClr> element has no 'lastClr' attribute.");
            return ParseHexColor(hex, "sysClr");
        }

        if (colorElement.Name == DrawingNamespace + "schemeClr")
        {
            var val = (string?)colorElement.Attribute("val") ??
                throw new InvalidDataException("An <a:schemeClr> element has no 'val' attribute.");
            if (val == "phClr" && phClrOverride is { } overrideColor)
            {
                return overrideColor;
            }

            return ResolveSchemeColor(val, theme.ColorScheme, colorMap ?? PptxColorMap.Default);
        }

        if (colorElement.Name == DrawingNamespace + "prstClr")
        {
            var val = (string?)colorElement.Attribute("val") ??
                throw new InvalidDataException("An <a:prstClr> element has no 'val' attribute.");
            return val switch
            {
                "white" => new Rgba32(255, 255, 255, 255),
                "black" => new Rgba32(0, 0, 0, 255),
                _ => throw new PptxUnsupportedFeatureException(
                    "pptx-color-kind", $"Preset color '{val}' is not supported."),
            };
        }

        throw new PptxUnsupportedFeatureException(
            "pptx-color-kind",
            $"Color-definition element '{colorElement.Name.LocalName}' is not supported.");
    }

    /// <summary>
    ///     Parses a required <c>#RRGGBB</c>/<c>RRGGBB</c> hex color value, rejecting any value
    ///     that is not exactly six hexadecimal digits.
    /// </summary>
    /// <remarks>
    ///     OOXML's <c>srgbClr/@val</c> and <c>sysClr/@lastClr</c> are always exactly six hex
    ///     digits (<c>RRGGBB</c>); an eight-digit <c>AARRGGBB</c> value, which
    ///     <see cref="Rgba32.Parse(string)"/> would otherwise also accept, misinterpreting its
    ///     first byte as alpha, must be rejected rather than silently misread - mirroring
    ///     <c>PptxDocument.Theme.cs</c>'s own <c>ParseSchemeColor</c> six-digit validation for the
    ///     same class of value.
    /// </remarks>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="hex"/> (after stripping any leading <c>'#'</c>) is not
    ///     exactly six hexadecimal digits, or is not valid hexadecimal at all.
    /// </exception>
    private static Rgba32 ParseHexColor(string hex, string elementName)
    {
        var digits = hex.StartsWith('#') ? hex[1..] : hex;
        if (digits.Length != 6)
        {
            throw new InvalidDataException(
                $"An <a:{elementName}> element has a color value '{hex}' that is not exactly six hexadecimal digits (OOXML's RRGGBB form).");
        }

        try
        {
            return Rgba32.Parse("#" + digits);
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException($"An <a:{elementName}> element has an invalid hexadecimal color value '{hex}'.", ex);
        }
    }

    /// <summary>
    ///     Maps an <c>&lt;a:schemeClr val="..."/&gt;</c> slot name to its resolved
    ///     <see cref="PptxColorScheme"/> color, consulting <paramref name="colorMap"/>'s own
    ///     <c>bg1</c>/<c>tx1</c>/<c>bg2</c>/<c>tx2</c> indirection targets for those four special
    ///     aliases (per ECMA-376's <c>&lt;p:clrMap&gt;</c>/<c>&lt;p:clrMapOvr&gt;</c> background/
    ///     text-color mapping - see <see cref="PptxColorMap"/>'s own remarks) before resolving the
    ///     redirected slot name via <see cref="ResolveNamedSlot"/>. Every other, ordinary slot
    ///     name (<c>dk1</c>/<c>lt1</c>/<c>dk2</c>/<c>lt2</c>/<c>accent1-6</c>/<c>hlink</c>/
    ///     <c>folHlink</c>/<c>phClr</c>) resolves directly, unaffected by <paramref name="colorMap"/>.
    /// </summary>
    internal static Rgba32 ResolveSchemeColor(string val, PptxColorScheme scheme, PptxColorMap colorMap) =>
        val switch
        {
            // Background/text aliases indirect through the effective color map - see this
            // method's own remarks and PptxColorMap's remarks for the ECMA-376 background/
            // text-color mapping this implements.
            "bg1" => ResolveNamedSlot(colorMap.Bg1, scheme),
            "tx1" => ResolveNamedSlot(colorMap.Tx1, scheme),
            "bg2" => ResolveNamedSlot(colorMap.Bg2, scheme),
            "tx2" => ResolveNamedSlot(colorMap.Tx2, scheme),
            _ => ResolveNamedSlot(val, scheme),
        };

    /// <summary>
    ///     Maps a direct (non-bg/tx-aliased) color-scheme slot name to its resolved
    ///     <see cref="PptxColorScheme"/> color - the 12 canonical slot names plus <c>phClr</c>,
    ///     deliberately excluding the <c>bg1</c>/<c>tx1</c>/<c>bg2</c>/<c>tx2</c> aliasing arms
    ///     <see cref="ResolveSchemeColor"/> itself already resolves, so a malformed
    ///     <c>&lt;p:clrMap&gt;</c> redirecting, for example, <c>bg1</c> to <c>"bg1"</c> itself
    ///     cannot recurse indefinitely - it instead deterministically throws
    ///     <see cref="InvalidDataException"/> below.
    /// </summary>
    private static Rgba32 ResolveNamedSlot(string slotName, PptxColorScheme scheme) =>
        slotName switch
        {
            "dk1" => scheme.Dark1,
            "lt1" => scheme.Light1,
            "dk2" => scheme.Dark2,
            "lt2" => scheme.Light2,
            "accent1" => scheme.Accent1,
            "accent2" => scheme.Accent2,
            "accent3" => scheme.Accent3,
            "accent4" => scheme.Accent4,
            "accent5" => scheme.Accent5,
            "accent6" => scheme.Accent6,
            "hlink" => scheme.Hyperlink,
            "folHlink" => scheme.FollowedHyperlink,
            // "Placeholder color" - unresolvable without shape-style-reference context; see ResolveColor's remarks.
            "phClr" => scheme.Dark1,
            _ => throw new InvalidDataException($"An <a:schemeClr> element names an unrecognized slot '{slotName}'."),
        };

    /// <summary>Applies a color-definition element's lumMod/lumOff/shade/tint/alpha transform chain, in that fixed order.</summary>
    private static Rgba32 ApplyColorTransforms(Rgba32 color, XElement colorElement)
    {
        var lumMod = ReadPercentageChild(colorElement, "lumMod");
        var lumOff = ReadPercentageChild(colorElement, "lumOff");
        if (lumMod is not null || lumOff is not null)
        {
            color = ApplyLuminance(color, lumMod ?? 1f, lumOff ?? 0f);
        }

        if (ReadPercentageChild(colorElement, "shade") is { } shade)
        {
            color = new Rgba32(
                (byte)Math.Clamp(color.R * shade, 0f, 255f),
                (byte)Math.Clamp(color.G * shade, 0f, 255f),
                (byte)Math.Clamp(color.B * shade, 0f, 255f),
                color.A);
        }

        if (ReadPercentageChild(colorElement, "tint") is { } tint)
        {
            color = new Rgba32(
                (byte)Math.Clamp(color.R * tint + 255f * (1f - tint), 0f, 255f),
                (byte)Math.Clamp(color.G * tint + 255f * (1f - tint), 0f, 255f),
                (byte)Math.Clamp(color.B * tint + 255f * (1f - tint), 0f, 255f),
                color.A);
        }

        if (ReadPercentageChild(colorElement, "alpha") is { } alpha)
        {
            color = new Rgba32(color.R, color.G, color.B, (byte)Math.Clamp(alpha * 255f, 0f, 255f));
        }

        return color;
    }

    /// <summary>
    ///     Reads a color-transform child element's own <c>val</c> attribute (OOXML's "thousandths
    ///     of a percent", 100000 = 100%) as a <c>[0,1]</c>-scaled (or, for <c>lumOff</c>, signed)
    ///     fraction, or <see langword="null"/> when the child is absent.
    /// </summary>
    private static float? ReadPercentageChild(XElement colorElement, string childName)
    {
        var child = colorElement.Element(DrawingNamespace + childName);
        if (child is null)
        {
            return null;
        }

        var val = (string?)child.Attribute("val") ??
            throw new InvalidDataException($"An <a:{childName}> element has no 'val' attribute.");
        if (!int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out var val100000ths))
        {
            throw new InvalidDataException($"An <a:{childName}> element has a non-numeric 'val' attribute value '{val}'.");
        }

        return val100000ths / 100000f;
    }

    /// <summary>Applies <c>L' = L * lumMod + lumOff</c> in HSL space, preserving hue/saturation/alpha.</summary>
    private static Rgba32 ApplyLuminance(Rgba32 color, float lumMod, float lumOff)
    {
        var (h, s, l) = RgbToHsl(color);
        l = Math.Clamp(l * lumMod + lumOff, 0f, 1f);
        return HslToRgb(h, s, l, color.A);
    }

    /// <summary>Converts an <see cref="Rgba32"/> color's RGB channels to HSL (hue in turns [0,1), saturation/lightness in [0,1]).</summary>
    private static (float H, float S, float L) RgbToHsl(Rgba32 color)
    {
        var r = color.R / 255f;
        var g = color.G / 255f;
        var b = color.B / 255f;

        // Identify the dominant (max) channel by comparing the original byte values rather than
        // their derived float equivalents, so no floating-point equality comparison is needed.
        var maxByte = Math.Max(color.R, Math.Max(color.G, color.B));
        var minByte = Math.Min(color.R, Math.Min(color.G, color.B));
        var max = maxByte / 255f;
        var min = minByte / 255f;
        var l = (max + min) / 2f;

        if (maxByte == minByte)
        {
            return (0f, 0f, l);
        }

        var delta = max - min;
        var s = l > 0.5f ? delta / (2f - max - min) : delta / (max + min);

        float h;
        if (maxByte == color.R)
        {
            h = (g - b) / delta + (g < b ? 6f : 0f);
        }
        else if (maxByte == color.G)
        {
            h = (b - r) / delta + 2f;
        }
        else
        {
            h = (r - g) / delta + 4f;
        }

        h /= 6f;
        return (h, s, l);
    }

    /// <summary>Converts an HSL color (hue in turns [0,1), saturation/lightness in [0,1]) back to RGB, with the given alpha.</summary>
    private static Rgba32 HslToRgb(float h, float s, float l, byte alpha)
    {
        if (s <= 0f)
        {
            var gray = (byte)Math.Clamp(l * 255f, 0f, 255f);
            return new Rgba32(gray, gray, gray, alpha);
        }

        var q = l < 0.5f ? l * (1f + s) : l + s - l * s;
        var p = 2f * l - q;

        var r = HueToChannel(p, q, h + 1f / 3f);
        var g = HueToChannel(p, q, h);
        var b = HueToChannel(p, q, h - 1f / 3f);

        return new Rgba32(
            (byte)Math.Clamp(r * 255f, 0f, 255f),
            (byte)Math.Clamp(g * 255f, 0f, 255f),
            (byte)Math.Clamp(b * 255f, 0f, 255f),
            alpha);
    }

    /// <summary>Standard HSL-to-RGB single-channel helper (see the CSS Color Module's reference algorithm).</summary>
    private static float HueToChannel(float p, float q, float t)
    {
        if (t < 0f)
        {
            t += 1f;
        }

        if (t > 1f)
        {
            t -= 1f;
        }

        if (t < 1f / 6f)
        {
            return p + (q - p) * 6f * t;
        }

        if (t < 1f / 2f)
        {
            return q;
        }

        if (t < 2f / 3f)
        {
            return p + (q - p) * (2f / 3f - t) * 6f;
        }

        return p;
    }

    /// <summary>
    ///     The conventional default stroke width PowerPoint applies to an <c>&lt;a:ln&gt;</c> that
    ///     declares a fill but omits its own <c>w</c> attribute entirely: <c>0.75</c>pt
    ///     (<c>0.75 * 12700</c> EMU). This is an <em>application-level convention</em> observed in
    ///     PowerPoint's own default new-shape border weight - the ECMA-376/ISO-29500
    ///     <c>CT_LineProperties</c> schema does not declare a formal XSD <c>default="..."</c> for
    ///     <c>w</c>, so this value is cross-checked against community/library documentation (for
    ///     example python-pptx's own documented default) rather than cited as a schema default.
    /// </summary>
    private const float DefaultLineWidthEmu = 0.75f * 12700f;

    /// <summary>
    ///     Parses an <c>&lt;a:ln&gt;</c> element's optional <c>w</c> (width, EMU) attribute,
    ///     returning <see langword="null"/> when <paramref name="lnElement"/> or its <c>w</c>
    ///     attribute is absent, shared by every <c>&lt;a:ln w="..."/&gt;</c> call site in this
    ///     partial class that merges or defaults an explicit line width (<see cref="ResolveLineStyle"/>,
    ///     <see cref="ResolveShapeLineStyle"/>, and <see cref="ResolveConnectorLineStyle"/>).
    /// </summary>
    /// <param name="lnElement">The <c>&lt;a:ln&gt;</c> element to inspect, or <see langword="null"/> when absent.</param>
    /// <returns>
    ///     The parsed, finite <c>w</c> attribute value, or <see langword="null"/> when
    ///     <paramref name="lnElement"/> is <see langword="null"/> or declares no <c>w</c> attribute.
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="lnElement"/>'s <c>w</c> attribute is present but not a
    ///     finite floating-point number.
    /// </exception>
    private static float? ParseOptionalLineWidthAttribute(XElement? lnElement)
    {
        var widthAttribute = lnElement?.Attribute("w");
        if (widthAttribute is null)
        {
            return null;
        }

        try
        {
            var width = (float?)widthAttribute;
            if (width is not null && !float.IsFinite(width.Value))
            {
                throw new InvalidDataException(
                    $"An <a:ln> element has a non-finite 'w' attribute value '{(string?)widthAttribute}'.");
            }

            return width;
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException(
                $"An <a:ln> element has a non-numeric 'w' attribute value '{(string?)widthAttribute}'.",
                ex);
        }
    }

    /// <summary>
    ///     Resolves an <c>&lt;a:ln&gt;</c> line-properties element into a <see cref="PptxLineStyle"/>.
    /// </summary>
    /// <param name="lnElement">The <c>&lt;a:ln&gt;</c> element, or <see langword="null"/> for "no line properties declared".</param>
    /// <param name="theme">The resolved theme, used to resolve any <c>&lt;a:schemeClr&gt;</c> in the line's fill.</param>
    /// <param name="colorMap">
    ///     The effective color map consulted when the line's fill declares an <c>&lt;a:schemeClr
    ///     val="bg1"/&gt;</c>-shaped token, or <see langword="null"/> (the default) - see
    ///     <see cref="ResolveFill"/>'s matching parameter.
    /// </param>
    /// <param name="phClrOverride">
    ///     The concrete color to substitute for an <c>&lt;a:schemeClr val="phClr"/&gt;</c> token
    ///     in the line's fill, or <see langword="null"/> (the default) - see
    ///     <see cref="ResolveFill"/>'s matching parameter. Supplied by
    ///     <see cref="ResolveShapeStyleLineStyle"/> when resolving a <c>&lt;p:style&gt;/
    ///     &lt;a:lnRef&gt;</c>'s own theme format-scheme line-style entry, which is always
    ///     <c>phClr</c>-templated; every other, pre-existing call site omits this parameter,
    ///     leaving its behavior unchanged.
    /// </param>
    /// <returns>
    ///     The resolved <see cref="PptxLineStyle"/>, or <see langword="null"/> meaning "no
    ///     stroke": when <paramref name="lnElement"/> is <see langword="null"/>, its resolved fill
    ///     is <see cref="PptxNoFill"/> (including an explicit <c>&lt;a:noFill/&gt;</c>, or no
    ///     recognized fill-definition child at all), or its <c>w</c> attribute is <em>explicitly
    ///     present</em> with a non-positive (<c>&lt;= 0</c>) value. A <c>w</c> attribute that is
    ///     genuinely <em>absent</em> (as opposed to explicitly zero/negative) instead resolves to
    ///     <see cref="DefaultLineWidthEmu"/>, PowerPoint's own observed default stroke weight -
    ///     this mirrors real-world documents that declare only a line color/fill and rely on
    ///     PowerPoint's own default width, which must not render invisibly. See the design
    ///     document's "Stroke/Line Style Resolution" section for the full rationale; this
    ///     default-width fallback applies only to this method, not to
    ///     <see cref="ResolveShapeLineStyle"/>'s case-3 branch or
    ///     <see cref="ResolveConnectorLineStyle"/>'s own width-merge logic, both of which
    ///     deliberately retain their existing "a present, width-less <c>&lt;a:ln&gt;</c> never
    ///     gains a width from style or default" behavior.
    /// </returns>
    /// <param name="resolveBlipImage">
    ///     Threaded unchanged into this method's own <see cref="ResolveFill"/> call - see
    ///     <see cref="ResolveFill"/>'s matching parameter.
    /// </param>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="lnElement"/>'s <c>w</c> attribute is present but not a
    ///     finite floating-point number - see <see cref="ParseOptionalLineWidthAttribute"/>.
    /// </exception>
    internal static PptxLineStyle? ResolveLineStyle(
        XElement? lnElement, PptxTheme theme, PptxColorMap? colorMap = null, Rgba32? phClrOverride = null,
        Func<XElement, Surface>? resolveBlipImage = null)
    {
        if (lnElement is null)
        {
            return null;
        }

        var ownWidthEmu = ParseOptionalLineWidthAttribute(lnElement);
        float widthEmu;
        if (ownWidthEmu is null)
        {
            // Genuinely absent "w" - fall back to PowerPoint's own observed default stroke width
            // rather than treating this the same as an explicit zero/negative width.
            widthEmu = DefaultLineWidthEmu;
        }
        else
        {
            widthEmu = ownWidthEmu.Value;
            if (widthEmu <= 0f)
            {
                return null;
            }
        }

        // A line's fill carries no shape width/height of its own to position a gradient against -
        // gradient-filled lines are not meaningfully positionable this phase, so 1x1 is used as a
        // neutral placeholder extent (only reachable if a document declares <a:ln><a:gradFill>).
        var paint = ResolveFill(lnElement, theme, 1f, 1f, phClrOverride, colorMap, resolveBlipImage);
        if (paint is PptxNoFill)
        {
            return null;
        }

        var dashArray = ResolveDashArray(lnElement, widthEmu);
        return new PptxLineStyle(widthEmu, paint, dashArray);
    }

    /// <summary>
    ///     Resolves an <c>&lt;a:ln&gt;</c>'s <c>&lt;a:prstDash val="..."/&gt;</c> preset dash
    ///     pattern name into a dash-length array scaled to <paramref name="widthEmu"/> (matching
    ///     OOXML's own width-relative dash/gap proportions), or <see langword="null"/> for a
    ///     solid line.
    /// </summary>
    /// <remarks>
    ///     Supports the six most common preset names (<c>dash</c>, <c>dashDot</c>, <c>dot</c>,
    ///     <c>lgDash</c>, <c>lgDashDot</c>, <c>sysDash</c>, <c>sysDot</c>); <c>solid</c>, an
    ///     absent <c>&lt;a:prstDash&gt;</c>, and any other (rarer) preset name all resolve to
    ///     <see langword="null"/> (solid line) - a documented simplification, since an unsupported
    ///     dash name degrading to a solid line is a purely cosmetic difference that should not
    ///     block resolving an otherwise well-formed line style.
    /// </remarks>
    private static IReadOnlyList<float>? ResolveDashArray(XElement lnElement, float widthEmu)
    {
        var prstDash = lnElement.Element(DrawingNamespace + "prstDash");
        var val = (string?)prstDash?.Attribute("val");
        return val switch
        {
            "dash" => [widthEmu * 4f, widthEmu * 3f],
            "dashDot" => [widthEmu * 4f, widthEmu * 3f, widthEmu, widthEmu * 3f],
            "dot" => [widthEmu, widthEmu * 3f],
            "lgDash" => [widthEmu * 8f, widthEmu * 3f],
            "lgDashDot" => [widthEmu * 8f, widthEmu * 3f, widthEmu, widthEmu * 3f],
            "sysDash" => [widthEmu * 3f, widthEmu * 2f],
            "sysDot" => [widthEmu, widthEmu * 2f],
            _ => null,
        };
    }

    /// <summary>
    ///     Resolves a shape's own <c>&lt;p:style&gt;/&lt;a:fillRef idx="..."/&gt;</c> shape-style
    ///     reference (Phase 2 Follow-Up) into a concrete <see cref="PptxPaint"/>: maps <c>idx</c>
    ///     directly, 1-based, into the theme's <see cref="PptxTheme.FillStyleList"/> (unlike
    ///     <c>&lt;p:bgRef&gt;</c>'s own <c>&lt;a:bgFillStyleLst&gt;</c> 1001-offset convention -
    ///     an ordinary <c>fillRef</c>/<c>lnRef</c> indexes its own style list with no offset at
    ///     all), substituting <c>&lt;a:fillRef&gt;</c>'s own single color-definition child
    ///     (resolved via the existing <see cref="ResolveColor"/>) for every <c>&lt;a:schemeClr
    ///     val="phClr"/&gt;</c> token the matched style-list entry declares - reusing the exact
    ///     phClr-substitution pattern <see cref="ResolveBackgroundStyleReference"/> already
    ///     establishes for <c>&lt;p:bgRef&gt;</c>.
    /// </summary>
    /// <param name="styleElement">
    ///     The shape's own sibling <c>&lt;p:style&gt;</c> element (see <see cref="RenderShape"/>),
    ///     or <see langword="null"/> when the shape declares no <c>&lt;p:style&gt;</c> at all.
    /// </param>
    /// <param name="theme">The resolved theme, used to resolve <see cref="PptxTheme.FillStyleList"/> and any <c>&lt;a:schemeClr&gt;</c>.</param>
    /// <param name="widthEmu">The owning shape's own declared width, in EMU (needed to position a gradient fill).</param>
    /// <param name="heightEmu">The owning shape's own declared height, in EMU (needed to position a gradient fill).</param>
    /// <param name="colorMap">
    ///     The effective color map consulted when the matched style-list entry declares an
    ///     <c>&lt;a:schemeClr val="bg1"/&gt;</c>-shaped token, or <see langword="null"/> (the
    ///     default) - see <see cref="ResolveFill"/>'s matching parameter.
    /// </param>
    /// <param name="resolveBlipImage">
    ///     Threaded unchanged into this method's own <see cref="ResolveFill"/> call - see
    ///     <see cref="ResolveFill"/>'s matching parameter.
    /// </param>
    /// <returns>
    ///     The resolved <see cref="PptxPaint"/>, or <see cref="PptxNoFill.Instance"/> when
    ///     <paramref name="styleElement"/> is <see langword="null"/>, declares no
    ///     <c>&lt;a:fillRef&gt;</c> at all, or declares a <c>&lt;a:fillRef idx="0"/&gt;</c>
    ///     (the schema-defined "no fill" sentinel).
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>&lt;a:fillRef&gt;</c>'s <c>idx</c> attribute is missing, not a valid
    ///     non-negative integer, or outside <c>[0,3]</c> (a real theme's
    ///     <c>&lt;a:fillStyleLst&gt;</c> always declares exactly 3 entries, so any larger value,
    ///     or a value indexing past the end of (or into an empty) <see cref="PptxTheme.FillStyleList"/>,
    ///     is rejected rather than silently clamped).
    /// </exception>
    internal static PptxPaint ResolveShapeStyleFill(
        XElement? styleElement, PptxTheme theme, float widthEmu, float heightEmu, PptxColorMap? colorMap = null,
        Func<XElement, Surface>? resolveBlipImage = null)
    {
        colorMap ??= PptxColorMap.Default;

        var fillRef = styleElement?.Element(DrawingNamespace + "fillRef");
        if (fillRef is null)
        {
            return PptxNoFill.Instance;
        }

        var idx = ParseStyleRefIdx(fillRef, "fillRef");
        if (idx == 0)
        {
            return PptxNoFill.Instance;
        }

        if (idx is < 1 or > 3 || idx - 1 >= theme.FillStyleList.Count)
        {
            throw new InvalidDataException(
                $"A <p:style>/<a:fillRef idx=\"{idx}\"> indexes outside the theme's <a:fillStyleLst> (which has {theme.FillStyleList.Count} entries, expected exactly 3).");
        }

        var styleEntry = theme.FillStyleList[(int)(idx - 1)];

        // <a:fillRef>'s own single color-definition child (when present) supplies the concrete
        // phClr substitution value for the matched style-list entry's own <a:schemeClr
        // val="phClr"/> token(s) - same pattern as ResolveBackgroundStyleReference's own <p:bgRef>
        // resolution.
        var colorElement = fillRef.Elements().FirstOrDefault();
        var phClrOverride = colorElement is null ? (Rgba32?)null : ResolveColor(colorElement, theme, colorMap: colorMap);

        // theme.FillStyleList's entries are themselves fill-definition elements (e.g.
        // <a:solidFill>), not a parent containing one - wrap in a synthetic parent so
        // ResolveFill's existing "look up a direct named child" dispatch logic applies unchanged.
        var syntheticFillParent = new XElement("pptxSyntheticStyleFillParent", styleEntry);
        return ResolveFill(syntheticFillParent, theme, widthEmu, heightEmu, phClrOverride, colorMap, resolveBlipImage);
    }

    /// <summary>
    ///     Resolves a shape's own <c>&lt;p:style&gt;/&lt;a:lnRef idx="..."/&gt;</c> shape-style
    ///     reference (Phase 2 Follow-Up) into a concrete <see cref="PptxLineStyle"/>: maps
    ///     <c>idx</c> directly, 1-based, into the theme's <see cref="PptxTheme.LnStyleList"/> -
    ///     each entry is itself an already <c>&lt;a:ln&gt;</c>-shaped element, so it is fed
    ///     directly to the existing <see cref="ResolveLineStyle"/> after substituting
    ///     <c>&lt;a:lnRef&gt;</c>'s own single color-definition child as that entry's
    ///     <c>phClr</c> override (same pattern as <see cref="ResolveShapeStyleFill"/>).
    /// </summary>
    /// <param name="styleElement">
    ///     The shape's own sibling <c>&lt;p:style&gt;</c> element (see <see cref="RenderShape"/>),
    ///     or <see langword="null"/> when the shape declares no <c>&lt;p:style&gt;</c> at all.
    /// </param>
    /// <param name="theme">The resolved theme, used to resolve <see cref="PptxTheme.LnStyleList"/> and any <c>&lt;a:schemeClr&gt;</c>.</param>
    /// <param name="colorMap">
    ///     The effective color map consulted when the matched style-list entry's fill declares an
    ///     <c>&lt;a:schemeClr val="bg1"/&gt;</c>-shaped token, or <see langword="null"/> (the
    ///     default) - see <see cref="ResolveFill"/>'s matching parameter.
    /// </param>
    /// <param name="resolveBlipImage">
    ///     Threaded unchanged into this method's own <see cref="ResolveLineStyle"/> call - see
    ///     <see cref="ResolveFill"/>'s matching parameter.
    /// </param>
    /// <returns>
    ///     The resolved <see cref="PptxLineStyle"/>, or <see langword="null"/> meaning "no
    ///     stroke": when <paramref name="styleElement"/> is <see langword="null"/>, declares no
    ///     <c>&lt;a:lnRef&gt;</c> at all, declares a <c>&lt;a:lnRef idx="0"/&gt;</c> (the
    ///     schema-defined "no line" sentinel), or the matched style-list entry itself resolves to
    ///     "no stroke" per <see cref="ResolveLineStyle"/>'s own rules.
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>&lt;a:lnRef&gt;</c>'s <c>idx</c> attribute is missing, not a valid
    ///     non-negative integer, or outside <c>[0,3]</c> - see
    ///     <see cref="ResolveShapeStyleFill"/>'s matching exception documentation.
    /// </exception>
    internal static PptxLineStyle? ResolveShapeStyleLineStyle(
        XElement? styleElement, PptxTheme theme, PptxColorMap? colorMap = null,
        Func<XElement, Surface>? resolveBlipImage = null)
    {
        colorMap ??= PptxColorMap.Default;

        var lnRef = styleElement?.Element(DrawingNamespace + "lnRef");
        if (lnRef is null)
        {
            return null;
        }

        var idx = ParseStyleRefIdx(lnRef, "lnRef");
        if (idx == 0)
        {
            return null;
        }

        if (idx is < 1 or > 3 || idx - 1 >= theme.LnStyleList.Count)
        {
            throw new InvalidDataException(
                $"A <p:style>/<a:lnRef idx=\"{idx}\"> indexes outside the theme's <a:lnStyleLst> (which has {theme.LnStyleList.Count} entries, expected exactly 3).");
        }

        var styleEntry = theme.LnStyleList[(int)(idx - 1)];

        var colorElement = lnRef.Elements().FirstOrDefault();
        var phClrOverride = colorElement is null ? (Rgba32?)null : ResolveColor(colorElement, theme, colorMap: colorMap);

        return ResolveLineStyle(styleEntry, theme, colorMap, phClrOverride, resolveBlipImage);
    }

    /// <summary>
    ///     Resolves a shape's effective line style by combining its own <c>&lt;a:ln&gt;</c>
    ///     (<see cref="RenderShape"/>'s own <c>&lt;p:spPr&gt;/&lt;a:ln&gt;</c> element) with its
    ///     <c>&lt;p:style&gt;/&lt;a:lnRef&gt;</c> fallback (Phase 2 Follow-Up: Shape Style
    ///     References), per the corrected 4-case precedence documented on <see cref="RenderShape"/>:
    ///     an explicit fill-definition child (including <c>&lt;a:noFill/&gt;</c>) on the shape's
    ///     own <c>&lt;a:ln&gt;</c> wins outright over the style; a present <c>&lt;a:ln&gt;</c>
    ///     with no recognized fill-definition child of its own keeps its own width/dash but
    ///     defers only its <em>color</em> to the style <c>&lt;a:lnRef&gt;</c>; a fully absent
    ///     <c>&lt;a:ln&gt;</c> defers entirely to the style.
    /// </summary>
    /// <remarks>
    ///     This is a deliberately <em>narrower</em> merge than <see cref="ResolveConnectorLineStyle"/>'s
    ///     own per-attribute (width/paint/dash independently) merge for connector shapes: here,
    ///     width and dash always come from the shape's own <c>&lt;a:ln&gt;</c> when it is present
    ///     at all (never pulled from the style, even when the shape's own <c>&lt;a:ln&gt;</c>
    ///     declares no <c>w</c>/<c>&lt;a:prstDash&gt;</c>) - only the paint/color ever falls back
    ///     to the style. When the style itself supplies no usable color (no <c>&lt;p:style&gt;</c>
    ///     at all, no <c>&lt;a:lnRef&gt;</c>, an <c>&lt;a:lnRef idx="0"/&gt;</c>, or a style entry
    ///     that itself resolves to <see cref="PptxNoFill"/>), the result is "no stroke" - the same
    ///     default every other fill-less-line call site already produces (see
    ///     <see cref="ResolveLineStyle"/>'s own remarks), and the same default
    ///     <see cref="ResolveConnectorLineStyle"/> already establishes as precedent
    ///     (<c>styleLineStyle?.Paint ?? PptxNoFill.Instance</c>) - no new "default color" is
    ///     invented, since OOXML's own schema default for an absent/<c>idx="0"</c> style
    ///     reference is "no line at all", not a specific color.
    /// </remarks>
    /// <param name="lnElement">
    ///     The shape's own <c>&lt;p:spPr&gt;/&lt;a:ln&gt;</c> element, or <see langword="null"/>
    ///     when the shape declares no <c>&lt;a:ln&gt;</c> at all.
    /// </param>
    /// <param name="styleElement">
    ///     The shape's own sibling <c>&lt;p:style&gt;</c> element (see <see cref="RenderShape"/>),
    ///     or <see langword="null"/> when the shape declares no <c>&lt;p:style&gt;</c> at all.
    /// </param>
    /// <param name="theme">The resolved theme, used to resolve <see cref="PptxTheme.LnStyleList"/> and any <c>&lt;a:schemeClr&gt;</c>.</param>
    /// <param name="colorMap">
    ///     The effective color map consulted when a fill declares an <c>&lt;a:schemeClr
    ///     val="bg1"/&gt;</c>-shaped token, or <see langword="null"/> (the default) - see
    ///     <see cref="ResolveFill"/>'s matching parameter.
    /// </param>
    /// <param name="resolveBlipImage">
    ///     Threaded unchanged into this method's own <see cref="ResolveLineStyle"/>/
    ///     <see cref="ResolveShapeStyleLineStyle"/> calls - see <see cref="ResolveFill"/>'s
    ///     matching parameter.
    /// </param>
    /// <returns>
    ///     The resolved <see cref="PptxLineStyle"/>, or <see langword="null"/> meaning "no
    ///     stroke" - see this method's own remarks and <see cref="ResolveLineStyle"/>/
    ///     <see cref="ResolveShapeStyleLineStyle"/>'s matching documentation for the full set of
    ///     "no stroke" conditions each delegated-to case produces.
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="lnElement"/>'s <c>w</c> attribute is present but not a
    ///     finite floating-point number - see <see cref="ParseOptionalLineWidthAttribute"/>.
    /// </exception>
    internal static PptxLineStyle? ResolveShapeLineStyle(
        XElement? lnElement, XElement? styleElement, PptxTheme theme, PptxColorMap? colorMap = null,
        Func<XElement, Surface>? resolveBlipImage = null)
    {
        // Case 4: a fully absent <a:ln> defers entirely to the style <a:lnRef>.
        if (lnElement is null)
        {
            return ResolveShapeStyleLineStyle(styleElement, theme, colorMap, resolveBlipImage);
        }

        // Cases 1/2: an explicit fill-definition child (including <a:noFill/>) on the shape's own
        // <a:ln> always wins outright over the style - ResolveLineStyle already correctly returns
        // null for an explicit <a:noFill/>, distinguishing it from case 3 below.
        if (HasExplicitFillChild(lnElement))
        {
            return ResolveLineStyle(lnElement, theme, colorMap, resolveBlipImage: resolveBlipImage);
        }

        // Case 3: a present <a:ln> with no recognized fill-definition child of its own keeps its
        // own width/dash, but defers only its color to the style <a:lnRef> - never merging width
        // or dash from style, unlike ResolveConnectorLineStyle's broader per-attribute merge.
        var widthEmu = ParseOptionalLineWidthAttribute(lnElement) ?? 0f;
        if (widthEmu <= 0f)
        {
            return null;
        }

        var styleLineStyle = ResolveShapeStyleLineStyle(styleElement, theme, colorMap, resolveBlipImage);
        var paint = styleLineStyle?.Paint ?? PptxNoFill.Instance;
        if (paint is PptxNoFill)
        {
            return null;
        }

        var dashArray = ResolveDashArray(lnElement, widthEmu);
        return new PptxLineStyle(widthEmu, paint, dashArray);
    }

    /// <summary>
    ///     Parses a shape-style reference element's (<c>&lt;a:fillRef&gt;</c>/<c>&lt;a:lnRef&gt;</c>)
    ///     required <c>idx</c> attribute.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>idx</c> is missing or not a valid non-negative integer.
    /// </exception>
    private static uint ParseStyleRefIdx(XElement refElement, string refElementName)
    {
        var idxValue = (string?)refElement.Attribute("idx") ??
            throw new InvalidDataException($"A <a:{refElementName}> element has no 'idx' attribute.");
        if (!uint.TryParse(idxValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx))
        {
            throw new InvalidDataException($"A <a:{refElementName}> element has a non-numeric 'idx' attribute value '{idxValue}'.");
        }

        return idx;
    }

    /// <summary>
    ///     Converts a shape's local-space outline <paramref name="shapePath"/> into its stroked,
    ///     fillable outline per <paramref name="lineStyle"/>, mirroring the exact
    ///     <see cref="StrokeStyle"/>-construction/<see cref="PathStroker.Stroke"/> call pattern
    ///     the PDF renderer uses (<c>PdfDocument.PaintStroke</c>).
    /// </summary>
    /// <param name="shapePath">The shape's own local-space geometry (see <see cref="ResolveShapeGeometry"/>).</param>
    /// <param name="lineStyle">The resolved line style to stroke with.</param>
    /// <param name="localToSurface">
    ///     The transform that will later be applied to the returned outline (see
    ///     <c>PptxDocument.Render.cs</c>'s callers) - used only to derive a coordinate-scale-aware
    ///     <see cref="PathStroker.Stroke"/> <c>flattenTolerance</c> (see
    ///     <see cref="ResolveFlattenTolerance"/>'s remarks); the returned outline itself remains in
    ///     <paramref name="shapePath"/>'s own untransformed local coordinate space, exactly as
    ///     before - the caller still applies this same transform afterward.
    /// </param>
    /// <returns>
    ///     The stroked outline <see cref="Path"/>, in the same local coordinate space as
    ///     <paramref name="shapePath"/> - a later rendering phase fills it (via
    ///     <see cref="Drawing.PathFiller.Fill(Canvas.Surface, Path, Rgba32, FillRule, float)"/>,
    ///     <see cref="FillRule.NonZero"/>) with <paramref name="lineStyle"/>'s own resolved paint;
    ///     this phase has no rendering surface yet, so only the outline geometry is produced here.
    /// </returns>
    internal static Path ResolveStrokeOutline(Path shapePath, PptxLineStyle lineStyle, Matrix3x2 localToSurface)
    {
        var style = new StrokeStyle(lineStyle.WidthEmu, dashArray: lineStyle.DashArray);
        var flattenTolerance = ResolveFlattenTolerance(localToSurface);
        return PathStroker.Stroke(shapePath, style, flattenTolerance);
    }

    /// <summary>
    ///     Derives a <see cref="PathStroker.Stroke"/> <c>flattenTolerance</c>, in
    ///     <paramref name="localToSurface"/>'s own pre-transform (local, native-EMU) coordinate
    ///     space, that keeps the EFFECTIVE post-transform flattening deviation close to
    ///     <see cref="PathStroker.Stroke"/>'s own pixel-tuned default (<c>0.25f</c>, i.e. a quarter
    ///     device pixel) - regardless of how large a shape's own native coordinate magnitude is.
    /// </summary>
    /// <remarks>
    ///     <see cref="ResolveStrokeOutline"/> strokes <c>shapePath</c> in its own local, untransformed
    ///     coordinate space - a real-world PPTX shape's native EMU coordinates (hundreds of
    ///     thousands to millions of units) - and only applies <paramref name="localToSurface"/>
    ///     afterward (see <c>PptxDocument.Render.cs</c>'s callers). Using <c>PathStroker.Stroke</c>'s
    ///     literal pixel-tuned default <c>flattenTolerance</c> directly against that native EMU
    ///     magnitude tessellates every curved join/cap and every Bezier-flattened curve segment
    ///     roughly 200x finer than the shape will ever actually be rendered at (a quarter of one
    ///     EMU, rather than a quarter of one device pixel) - a severe, unnecessary tessellation
    ///     performance cost, and (see the regression this fix accompanies) the direct trigger for
    ///     <see cref="Drawing.StrokeOutliner.Outline"/>'s inner-ring collapse detector to false-
    ///     positive on a thin, closed, curved stroke (e.g. a noFill ellipse/roundRect with a thin
    ///     color-only <c>&lt;a:ln&gt;</c>) at this coordinate magnitude and tessellation density.
    ///     <para>
    ///     Dividing the library's own <c>0.25f</c> target by <paramref name="localToSurface"/>'s
    ///     own scale factor keeps the post-transform deviation at that same intended ~0.25 device
    ///     pixels, independent of the shape's own native coordinate magnitude - exactly like
    ///     <c>PdfDocument.PaintStroke</c> already achieves by construction (it strokes an
    ///     already-device-space-baked path, so the untouched <c>0.25f</c> default is already
    ///     correct there). The LARGER of the transform's own X/Y axis scales is used (rather than,
    ///     say, their average) so that even a non-uniformly scaled shape's more-magnified axis
    ///     still stays within the intended device-pixel deviation (the other axis then tessellates
    ///     somewhat finer than strictly necessary - a negligible, not a correctness, cost).
    ///     </para>
    /// </remarks>
    private static float ResolveFlattenTolerance(Matrix3x2 localToSurface)
    {
        const float defaultFlattenTolerance = 0.25f;

        var scaleX = new Vector2(localToSurface.M11, localToSurface.M12).Length();
        var scaleY = new Vector2(localToSurface.M21, localToSurface.M22).Length();
        var scale = Math.Max(scaleX, scaleY);

        // A degenerate (zero, negative-impossible-but-defensive, infinite, or NaN) transform scale
        // - e.g. a shape collapsed to zero size - falls back to the library's own pixel-tuned
        // default rather than producing a zero/negative/non-finite flattenTolerance that
        // PathStroker.Stroke itself would reject.
        if (!float.IsFinite(scale) || scale <= 0f)
        {
            return defaultFlattenTolerance;
        }

        return defaultFlattenTolerance / scale;
    }
}
