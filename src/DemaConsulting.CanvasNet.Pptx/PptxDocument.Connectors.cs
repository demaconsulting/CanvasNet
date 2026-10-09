using System.Numerics;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore cxnsp pptx lnref prstdash headend tailend

/// <summary>
///     Implements the <see cref="PptxDocument"/> connector-shape (<c>&lt;p:cxnSp&gt;</c>) resolvers
///     (Phase 2 Follow-Up: Connector Shape Rendering): merging a connector's own <c>&lt;a:ln&gt;</c>
///     with its <c>&lt;p:style&gt;/&lt;a:lnRef&gt;</c> fallback into a single effective line style,
///     resolving its <c>&lt;a:headEnd&gt;</c>/<c>&lt;a:tailEnd&gt;</c> arrowheads, and computing its
///     geometry's own start/end points and tangent directions (needed to orient an arrowhead) - see
///     <see cref="PptxDocument.RenderConnector"/> for how these are composed into a painted
///     connector.
/// </summary>
public sealed partial class PptxDocument
{
    /// <summary>
    ///     Resolves a connector shape's effective line style, merging its own <c>&lt;a:ln&gt;</c>
    ///     with its <c>&lt;p:style&gt;/&lt;a:lnRef&gt;</c> fallback on a <em>per-attribute</em>
    ///     basis (width/paint/dash independently), unlike <see cref="RenderShape"/>'s own
    ///     "a present <c>&lt;a:ln&gt;</c> always wins outright over style, in full" policy.
    /// </summary>
    /// <remarks>
    ///     A real-world connector (for example PowerPoint's own "Straight Arrow Connector" preset)
    ///     commonly declares an <c>&lt;a:ln&gt;</c> that carries only an arrowhead
    ///     (<c>&lt;a:tailEnd&gt;</c>), no <c>w</c> attribute and no fill child at all, relying
    ///     entirely on its <c>&lt;p:style&gt;/&lt;a:lnRef&gt;</c> for its own visible width and
    ///     color - under <see cref="RenderShape"/>'s own "whole-element" fallback policy, such a
    ///     connector would resolve to "no stroke" (a present but attribute-sparse <c>&lt;a:ln&gt;</c>
    ///     still wins outright over the style, in full, under that policy), which is the opposite
    ///     of its real, intended, visible appearance. This merge instead independently falls each
    ///     of width/paint/dash back to the <c>&lt;p:style&gt;/&lt;a:lnRef&gt;</c>-resolved style
    ///     only when the connector's own <c>&lt;a:ln&gt;</c> does not itself declare that specific
    ///     attribute.
    /// </remarks>
    /// <param name="spPrElement">The connector's own <c>&lt;p:spPr&gt;</c> element.</param>
    /// <param name="styleElement">The connector's own sibling <c>&lt;p:style&gt;</c> element, or <see langword="null"/>.</param>
    /// <param name="theme">The resolved theme, used to resolve <see cref="PptxTheme.LnStyleList"/> and any <c>&lt;a:schemeClr&gt;</c>.</param>
    /// <param name="colorMap">The slide's own effective color map - see <see cref="ResolveFill"/>'s matching parameter.</param>
    /// <param name="resolveBlipImage">
    ///     Threaded unchanged into this method's own <see cref="ResolveFill"/>/
    ///     <see cref="ResolveShapeStyleLineStyle"/> calls - see <see cref="ResolveFill"/>'s
    ///     matching parameter.
    /// </param>
    /// <returns>
    ///     The resolved, merged <see cref="PptxLineStyle"/>, or <see langword="null"/> meaning "no
    ///     stroke": when the connector's own <c>&lt;a:ln&gt;</c> declares an explicit
    ///     <c>&lt;a:noFill/&gt;</c> (which always wins outright, regardless of style - a
    ///     connector explicitly opting out of a stroke must stay invisible), when the merged width
    ///     is non-positive, or when the merged paint resolves to <see cref="PptxNoFill"/>.
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the connector's own <c>&lt;a:ln&gt;</c>'s <c>w</c> attribute is present but
    ///     not a finite floating-point number - see <see cref="ParseOptionalLineWidthAttribute"/>.
    /// </exception>
    internal static PptxLineStyle? ResolveConnectorLineStyle(
        XElement spPrElement, XElement? styleElement, PptxTheme theme, PptxColorMap colorMap,
        Func<XElement, Surface>? resolveBlipImage = null)
    {
        var lnElement = spPrElement.Element(DrawingNamespace + "ln");

        // An explicit <a:noFill/> directly on the connector's own <a:ln> always wins outright,
        // even when a <p:style>/<a:lnRef> would otherwise resolve to a visible stroke - matching
        // RenderShape's own "explicit fill-definition child always wins" precedent.
        if (lnElement?.Element(DrawingNamespace + "noFill") is not null)
        {
            return null;
        }

        var styleLineStyle = ResolveShapeStyleLineStyle(styleElement, theme, colorMap, resolveBlipImage);

        var ownWidthEmu = ParseOptionalLineWidthAttribute(lnElement);
        var widthEmu = ownWidthEmu is > 0f ? ownWidthEmu.Value : styleLineStyle?.WidthEmu ?? 0f;
        if (widthEmu <= 0f)
        {
            return null;
        }

        var paint = lnElement is not null && HasExplicitFillChild(lnElement)
            ? ResolveFill(lnElement, theme, 1f, 1f, colorMap: colorMap, resolveBlipImage: resolveBlipImage)
            : styleLineStyle?.Paint ?? PptxNoFill.Instance;
        if (paint is PptxNoFill)
        {
            return null;
        }

        var dashArray = lnElement is not null && lnElement.Element(DrawingNamespace + "prstDash") is not null
            ? ResolveDashArray(lnElement, widthEmu)
            : styleLineStyle?.DashArray;

        return new PptxLineStyle(widthEmu, paint, dashArray);
    }

    /// <summary>
    ///     Resolves one of a connector's <c>&lt;a:headEnd&gt;</c>/<c>&lt;a:tailEnd&gt;</c>
    ///     arrowhead declarations.
    /// </summary>
    /// <param name="lnElement">The connector's own <c>&lt;a:ln&gt;</c> element, or <see langword="null"/>.</param>
    /// <param name="endElementName">Either <c>"headEnd"</c> or <c>"tailEnd"</c>.</param>
    /// <returns>
    ///     The resolved <see cref="PptxArrowheadStyle"/>, or <see langword="null"/> when
    ///     <paramref name="lnElement"/> is <see langword="null"/>, declares no matching end
    ///     element, or that end element's own <c>type</c> attribute is absent or
    ///     <c>"none"</c>/unrecognized (the OOXML schema default, and this phase's own
    ///     unsupported-type fallback - a cosmetic degrade to "no arrowhead" rather than a thrown
    ///     exception, consistent with <see cref="PptxDocument.ResolveDashArray"/>'s own
    ///     unsupported-preset-name philosophy).
    /// </returns>
    internal static PptxArrowheadStyle? ResolveArrowhead(XElement? lnElement, string endElementName)
    {
        var endElement = lnElement?.Element(DrawingNamespace + endElementName);
        if (endElement is null)
        {
            return null;
        }

        var kind = (string?)endElement.Attribute("type") switch
        {
            "triangle" => PptxArrowheadKind.Triangle,
            "stealth" => PptxArrowheadKind.Stealth,
            "diamond" => PptxArrowheadKind.Diamond,
            "oval" => PptxArrowheadKind.Oval,
            "arrow" => PptxArrowheadKind.Arrow,
            _ => PptxArrowheadKind.None,
        };
        if (kind == PptxArrowheadKind.None)
        {
            return null;
        }

        var widthKey = (string?)endElement.Attribute("w") ?? "med";
        var lengthKey = (string?)endElement.Attribute("len") ?? "med";
        return new PptxArrowheadStyle(kind, widthKey, lengthKey);
    }

    /// <summary>
    ///     Computes a connector geometry path's own start/end points and tangent directions, in
    ///     the same local coordinate space <paramref name="geometryPath"/> itself is expressed in
    ///     - the inputs <see cref="RenderConnector"/> needs to orient/position a
    ///     <c>&lt;a:headEnd&gt;</c>/<c>&lt;a:tailEnd&gt;</c> arrowhead (see
    ///     <see cref="PptxArrowheadGeometry"/>'s own local-space convention).
    /// </summary>
    /// <param name="geometryPath">
    ///     The connector's own resolved, local-space geometry (see
    ///     <see cref="ResolveShapeGeometry"/>) - expected to have exactly one subpath with at
    ///     least one non-<see cref="PathCommandType.Close"/> command, as every connector preset
    ///     this phase builds does (see <see cref="PptxPresetGeometry"/>'s connector builders).
    /// </param>
    /// <returns>
    ///     The connector's start point/outgoing tangent and end point/incoming tangent. A tangent
    ///     falls back to <see cref="Vector2.UnitX"/> (pointing along local <c>+x</c>) for a
    ///     degenerate (zero-length) first/last segment, so an arrowhead always has <em>some</em>
    ///     orientation rather than none.
    /// </returns>
    internal static (Vector2 StartPoint, Vector2 StartTangent, Vector2 EndPoint, Vector2 EndTangent) ComputeEndpointsAndTangents(
        Path geometryPath)
    {
        var subpath = geometryPath.Subpaths[0];
        var startPoint = subpath.Start;

        var firstCommand = subpath.Commands[0];
        var startTangent = firstCommand.ComputeTangents(subpath.Start).Outgoing ?? Vector2.UnitX;

        // Walk every command up to (but not including) a trailing Close, tracking the previous
        // vertex so the final segment's own incoming tangent can be computed - a Close command
        // carries no EndPoint of its own (see PathCommand.EndPoint's remarks) and none of this
        // phase's connector presets ever emit one (they are all open polylines/arcs), but this
        // loop tolerates one defensively rather than assuming it.
        var previousVertex = subpath.Start;
        var lastNonCloseCommand = firstCommand;
        var lastNonCloseCommandStart = subpath.Start;
        foreach (var command in subpath.Commands)
        {
            if (command.Type == PathCommandType.Close)
            {
                break;
            }

            lastNonCloseCommand = command;
            lastNonCloseCommandStart = previousVertex;
            previousVertex = command.EndPoint;
        }

        var endPoint = previousVertex;
        var endTangent = lastNonCloseCommand.ComputeTangents(lastNonCloseCommandStart).Incoming ?? Vector2.UnitX;

        return (startPoint, startTangent, endPoint, endTangent);
    }
}
