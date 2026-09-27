// cspell:ignore linecap linejoin dasharray dashoffset miterlimit anchor xlink href
// cspell:ignore Glyf Loca
// cspell:ignore evenodd nonzero viewbox gradientunits gradienttransform spreadmethod
// cspell:ignore userspaceonuse objectboundingbox skewx skewy tspan
// cspell:ignore rasterizing unparseable rrggbb sizeless bbox moveto multiplicatively pillarbox SMIL uncatchable formedness
// cspell:ignore letterboxing pillarboxing
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
using System.Numerics;
using System.Xml.Linq;

namespace DemaConsulting.CanvasNet.Codecs;

public static partial class SvgCodec
{
    // ================================================================================================
    // "preserveAspectRatio" parsing and the shared viewBox-fit algorithm
    // ================================================================================================

    /// <summary>
    ///     Identifies the <c>&lt;align&gt;</c> component of a parsed <c>preserveAspectRatio</c>
    ///     value - the 9 <c>x{Min,Mid,Max}Y{Min,Mid,Max}</c> combinations, plus <c>none</c> (an
    ///     exact, non-uniform stretch-to-fit with no alignment concept at all).
    /// </summary>
    private enum PreserveAspectRatioAlign
    {
        /// <summary>Stretch non-uniformly to exactly fill the viewport - no letterbox/pillarbox, no centering.</summary>
        None,

        /// <summary>Align the scaled content's left/top edge to the viewport's left/top edge.</summary>
        XMinYMin,

        /// <summary>Align the scaled content horizontally centered, top edge aligned.</summary>
        XMidYMin,

        /// <summary>Align the scaled content's right edge, top edge aligned.</summary>
        XMaxYMin,

        /// <summary>Align the scaled content's left edge, vertically centered.</summary>
        XMinYMid,

        /// <summary>Center the scaled content on both axes (the SVG/CSS default).</summary>
        XMidYMid,

        /// <summary>Align the scaled content's right edge, vertically centered.</summary>
        XMaxYMid,

        /// <summary>Align the scaled content's left edge, bottom edge aligned.</summary>
        XMinYMax,

        /// <summary>Align the scaled content horizontally centered, bottom edge aligned.</summary>
        XMidYMax,

        /// <summary>Align the scaled content's right/bottom edge to the viewport's right/bottom edge.</summary>
        XMaxYMax
    }

    /// <summary>
    ///     A fully-parsed <c>preserveAspectRatio</c> value (the leading, parsed-and-ignored
    ///     <c>defer</c> token is not itself represented - see <see cref="ParsePreserveAspectRatio"/>'s
    ///     remarks).
    /// </summary>
    /// <param name="Align">The resolved alignment (or <see cref="PreserveAspectRatioAlign.None"/> for an exact stretch).</param>
    /// <param name="Slice">
    ///     <see langword="true"/> for <c>slice</c> (uniformly scale to fill the viewport, cropping
    ///     any overflow); <see langword="false"/> for <c>meet</c> (uniformly scale to fit entirely
    ///     within the viewport, letterboxing/pillarboxing any remainder) - the SVG/CSS default.
    ///     Meaningless when <see cref="Align"/> is <see cref="PreserveAspectRatioAlign.None"/>.
    /// </param>
    private readonly record struct PreserveAspectRatio(PreserveAspectRatioAlign Align, bool Slice)
    {
        /// <summary>
        ///     The value used for every fit computation this codec performs when no
        ///     <c>preserveAspectRatio</c> attribute is present - <c>xMidYMid meet</c>, the SVG/CSS
        ///     specification's own initial value.
        /// </summary>
        public static readonly PreserveAspectRatio Default = new(PreserveAspectRatioAlign.XMidYMid, Slice: false);
    }

    /// <summary>
    ///     Parses a <c>preserveAspectRatio</c> attribute's raw value: an optional leading
    ///     <c>defer</c> token (parsed and discarded - meaningful only for an <c>&lt;image&gt;</c>
    ///     element's external resource loading order, which this codec does not implement, so it
    ///     has no observable effect either way), one of the 10 <c>&lt;align&gt;</c> keywords, and
    ///     an optional trailing <c>meet</c>/<c>slice</c> keyword (defaulting to <c>meet</c>).
    /// </summary>
    /// <param name="raw">The attribute's raw value, or <see langword="null"/> if absent.</param>
    /// <returns>
    ///     The parsed value, or <see cref="PreserveAspectRatio.Default"/> if <paramref name="raw"/>
    ///     is <see langword="null"/>, blank, or does not begin with a recognized <c>&lt;align&gt;</c>
    ///     keyword (an unrecognized/malformed value tolerantly falls back to the specification's
    ///     own default rather than throwing, matching this codec's established tolerant-fallback
    ///     policy for other malformed presentation-like attributes, for example
    ///     <c>stroke-miterlimit</c>).
    /// </returns>
    private static PreserveAspectRatio ParsePreserveAspectRatio(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return PreserveAspectRatio.Default;
        }

        var tokens = raw.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var index = 0;

        // "defer" only ever affects external-resource load ordering for <image>, which this
        // codec does not implement - parsed here only so its presence does not shift the
        // following <align>/meetOrSlice tokens out of position
        if (index < tokens.Length && string.Equals(tokens[index], "defer", StringComparison.OrdinalIgnoreCase))
        {
            index++;
        }

        if (index >= tokens.Length || ParseAlignToken(tokens[index]) is not { } align)
        {
            return PreserveAspectRatio.Default;
        }

        index++;
        var slice = index < tokens.Length && string.Equals(tokens[index], "slice", StringComparison.OrdinalIgnoreCase);
        return new PreserveAspectRatio(align, slice);
    }

    /// <summary>Parses one <c>&lt;align&gt;</c> keyword token.</summary>
    /// <param name="token">The token to parse.</param>
    /// <returns>The matching <see cref="PreserveAspectRatioAlign"/>, or <see langword="null"/> if unrecognized.</returns>
    private static PreserveAspectRatioAlign? ParseAlignToken(string token) => token switch
    {
        "none" => PreserveAspectRatioAlign.None,
        "xMinYMin" => PreserveAspectRatioAlign.XMinYMin,
        "xMidYMin" => PreserveAspectRatioAlign.XMidYMin,
        "xMaxYMin" => PreserveAspectRatioAlign.XMaxYMin,
        "xMinYMid" => PreserveAspectRatioAlign.XMinYMid,
        "xMidYMid" => PreserveAspectRatioAlign.XMidYMid,
        "xMaxYMid" => PreserveAspectRatioAlign.XMaxYMid,
        "xMinYMax" => PreserveAspectRatioAlign.XMinYMax,
        "xMidYMax" => PreserveAspectRatioAlign.XMidYMax,
        "xMaxYMax" => PreserveAspectRatioAlign.XMaxYMax,
        _ => null
    };

    /// <summary>
    ///     Computes the single shared "viewBox-fit" transform - mapping a content
    ///     <paramref name="origin"/>/<paramref name="size"/> box into a
    ///     <paramref name="viewportWidth"/> x <paramref name="viewportHeight"/> viewport per
    ///     <paramref name="preserveAspectRatio"/> - reused, identically, by the root <c>svg</c>
    ///     element (see <c>ComputeFitTransform</c>), an explicit <c>marker</c>
    ///     <c>preserveAspectRatio</c> (see <c>TryComputeMarkerContentTransform</c>), and a
    ///     <c>symbol</c> referenced via <c>use</c> (see <c>RenderUse</c>) - centralizing the
    ///     align/meet-slice transform algorithm exactly once rather than duplicating it at each of
    ///     the three call sites.
    /// </summary>
    /// <param name="origin">The content box's own origin (for example a <c>viewBox</c>'s <c>min-x</c>/<c>min-y</c>).</param>
    /// <param name="size">The content box's own size (for example a <c>viewBox</c>'s <c>width</c>/<c>height</c>).</param>
    /// <param name="viewportWidth">The destination viewport's width.</param>
    /// <param name="viewportHeight">The destination viewport's height.</param>
    /// <param name="preserveAspectRatio">The resolved <c>preserveAspectRatio</c> value to apply.</param>
    /// <returns>
    ///     A transform mapping content-space coordinates into viewport-space coordinates. Can be
    ///     non-finite when <paramref name="size"/> is extremely small but still positive - dividing
    ///     the viewport dimensions by such a value overflows the resulting scale to <c>Infinity</c>.
    ///     This method does not itself validate its result - every call site is responsible for
    ///     checking the returned transform with <c>IsFiniteTransform</c> before using it.
    /// </returns>
    /// <remarks>
    ///     With <paramref name="preserveAspectRatio"/> equal to
    ///     <see cref="PreserveAspectRatio.Default"/> (<c>xMidYMid meet</c>) and
    ///     <paramref name="origin"/> equal to <see cref="Vector2.Zero"/>, this method reproduces
    ///     this codec's original, pre-<c>preserveAspectRatio</c> "meet, centered" root-fit math
    ///     bit-for-bit - see <c>ComputeFitTransform</c>'s own remarks for the regression contract
    ///     this guarantees.
    /// </remarks>
    private static Matrix3x2 ComputePreserveAspectRatioFit(
        Vector2 origin,
        Vector2 size,
        float viewportWidth,
        float viewportHeight,
        PreserveAspectRatio preserveAspectRatio)
    {
        float scaleX;
        float scaleY;
        float offsetX;
        float offsetY;

        if (preserveAspectRatio.Align == PreserveAspectRatioAlign.None)
        {
            // "none" stretches non-uniformly to exactly fill the viewport - no uniform scale, no
            // letterbox/pillarbox remainder, so there is nothing to align/center either
            scaleX = viewportWidth / size.X;
            scaleY = viewportHeight / size.Y;
            offsetX = 0f;
            offsetY = 0f;
        }
        else
        {
            // Every other <align> value scales uniformly - "meet" (contain: fit entirely within
            // the viewport, the smaller of the two axis ratios) or "slice" (cover: fill the whole
            // viewport, the larger of the two axis ratios, cropping any overflow) - then aligns
            // the resulting box per the align keyword's own Min/Mid/Max component on each axis
            var uniformScale = preserveAspectRatio.Slice
                ? MathF.Max(viewportWidth / size.X, viewportHeight / size.Y)
                : MathF.Min(viewportWidth / size.X, viewportHeight / size.Y);
            scaleX = uniformScale;
            scaleY = uniformScale;

            var scaledWidth = size.X * uniformScale;
            var scaledHeight = size.Y * uniformScale;
            offsetX = ResolveHorizontalOffset(preserveAspectRatio.Align, viewportWidth, scaledWidth);
            offsetY = ResolveVerticalOffset(preserveAspectRatio.Align, viewportHeight, scaledHeight);
        }

        return Matrix3x2.CreateTranslation(-origin.X, -origin.Y)
            * Matrix3x2.CreateScale(scaleX, scaleY)
            * Matrix3x2.CreateTranslation(offsetX, offsetY);
    }

    /// <summary>Resolves the horizontal (x-axis) letterbox/pillarbox offset for one align keyword.</summary>
    /// <param name="align">The resolved align value (never <see cref="PreserveAspectRatioAlign.None"/>).</param>
    /// <param name="viewportWidth">The destination viewport's width.</param>
    /// <param name="scaledWidth">The uniformly-scaled content width.</param>
    /// <returns>The x offset: <c>0</c> for <c>xMin</c>, centered for <c>xMid</c>, or full-remainder for <c>xMax</c>.</returns>
    private static float ResolveHorizontalOffset(PreserveAspectRatioAlign align, float viewportWidth, float scaledWidth) => align switch
    {
        PreserveAspectRatioAlign.XMinYMin or PreserveAspectRatioAlign.XMinYMid or PreserveAspectRatioAlign.XMinYMax => 0f,
        PreserveAspectRatioAlign.XMaxYMin or PreserveAspectRatioAlign.XMaxYMid or PreserveAspectRatioAlign.XMaxYMax => viewportWidth - scaledWidth,
        _ => (viewportWidth - scaledWidth) / 2f
    };

    /// <summary>Resolves the vertical (y-axis) letterbox/pillarbox offset for one align keyword.</summary>
    /// <param name="align">The resolved align value (never <see cref="PreserveAspectRatioAlign.None"/>).</param>
    /// <param name="viewportHeight">The destination viewport's height.</param>
    /// <param name="scaledHeight">The uniformly-scaled content height.</param>
    /// <returns>The y offset: <c>0</c> for <c>yMin</c>, centered for <c>yMid</c>, or full-remainder for <c>yMax</c>.</returns>
    private static float ResolveVerticalOffset(PreserveAspectRatioAlign align, float viewportHeight, float scaledHeight) => align switch
    {
        PreserveAspectRatioAlign.XMinYMin or PreserveAspectRatioAlign.XMidYMin or PreserveAspectRatioAlign.XMaxYMin => 0f,
        PreserveAspectRatioAlign.XMinYMax or PreserveAspectRatioAlign.XMidYMax or PreserveAspectRatioAlign.XMaxYMax => viewportHeight - scaledHeight,
        _ => (viewportHeight - scaledHeight) / 2f
    };

    /// <summary>Reads an element's own <c>preserveAspectRatio</c> attribute. A small named helper for call-site clarity.</summary>
    /// <param name="element">The element to inspect.</param>
    /// <returns>The parsed value - see <see cref="ParsePreserveAspectRatio"/>.</returns>
    private static PreserveAspectRatio GetPreserveAspectRatio(XElement element) =>
        ParsePreserveAspectRatio((string?)element.Attribute("preserveAspectRatio"));
}
