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
    // "use" element handling
    // ================================================================================================

    /// <summary>
    ///     Renders a <c>use</c> element by re-rendering its referenced element in place, offset by
    ///     the <c>use</c> element's own <c>x</c>/<c>y</c> translation and cascaded state/transform -
    ///     or, when the <c>use</c> element itself carries its own <c>filter</c> attribute, renders
    ///     the resolved target as one filtered unit (see <see cref="RenderFilteredGroup"/>).
    /// </summary>
    /// <param name="element">The <c>use</c> element.</param>
    /// <param name="state">The cascaded render state at the <c>use</c> element itself.</param>
    /// <param name="transform">The accumulated transform at the <c>use</c> element itself.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="useDepth">The current <c>use</c>-reference nesting depth.</param>
    /// <param name="elementDepth">
    ///     The current recursion depth of the enclosing <see cref="RenderElement"/> call,
    ///     propagated to the re-rendered target so it also contributes toward
    ///     <see cref="MaxElementDepth"/>.
    /// </param>
    /// <param name="markerDepth">
    ///     The current <c>marker</c>-reference nesting depth, propagated unchanged to the
    ///     re-rendered target - a <c>use</c> reference is not itself a marker reference, but a
    ///     <c>marker</c> reference reached inside the re-rendered target must still contribute
    ///     toward <see cref="MaxMarkerDepth"/>. Also determines whether this <c>use</c> element's
    ///     own <c>filter</c> attribute is resolved at all (see this method's remarks).
    /// </param>
    /// <param name="totalElements">
    ///     The running total-rendered-elements count, propagated to the re-rendered target so it
    ///     also contributes toward <see cref="MaxTotalRenderedElements"/>.
    /// </param>
    /// <param name="workBudget">The shared geometry-parsing work budget, propagated to the re-rendered target.</param>
    /// <param name="filterWorkBudget">The shared cumulative filter-evaluation work budget, propagated to the re-rendered target.</param>
    /// <param name="boundsPrePassBudget">The shared cumulative bounds-pre-pass work budget, propagated to the re-rendered target.</param>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="useDepth"/> has already reached <see cref="MaxUseDepth"/>,
    ///     guarding against a reference cycle that would otherwise recurse indefinitely.
    /// </exception>
    /// <remarks>
    ///     A dangling, absent, or malformed <c>href</c>/<c>xlink:href"</c> reference is a tolerant
    ///     no-op (nothing is rendered), consistent with this class's general dangling-reference
    ///     handling elsewhere.
    ///     <para>
    ///     This <c>use</c> element's own <c>filter</c> presentation attribute is resolved (via
    ///     <see cref="ResolveFilterElement"/>) unless <paramref name="markerDepth"/> is greater
    ///     than zero - i.e. unless this very <c>use</c> element is itself part of a <c>marker</c>
    ///     element's own content - per the documented "filters on marker content have no effect"
    ///     scope decision shared with shape/text filtering. When resolved, the referenced target
    ///     (an internal-only variable named for the element resolved from <c>href</c>) is
    ///     rendered as one filtered unit via <see cref="RenderFilteredGroup"/>
    ///     instead of the plain unfiltered re-entry into <see cref="RenderElement"/> - the
    ///     algorithm is otherwise identical for both dispatch points (see
    ///     <see cref="RenderFilteredGroup"/>'s own remarks).
    ///     </para>
    /// </remarks>
    private static void RenderUse(XElement element, RenderState state, Matrix3x2 transform, RenderContext context, int useDepth, int elementDepth, int markerDepth, ref int totalElements, GeometryWorkBudget workBudget, FilterWorkBudget filterWorkBudget, BoundsPrePassWorkBudget boundsPrePassBudget)
    {
        if (useDepth >= MaxUseDepth)
        {
            throw new InvalidDataException("Exceeded the maximum <use> reference nesting depth.");
        }

        var hrefId = GetHrefAttribute(element) is { } href ? ExtractFragmentId(href) : null;
        if (hrefId == null || !context.IdIndex.TryGetValue(hrefId, out var target))
        {
            return;
        }

        var offset = new Vector2(
            GetFloatAttribute(element, "x", state, PercentageAxis.Horizontal),
            GetFloatAttribute(element, "y", state, PercentageAxis.Vertical));

        // A "symbol" target establishes a new nested viewport (per its own resolved
        // width/height, falling back to this "use" element's own width/height, falling back to
        // the current viewport) fitted via preserveAspectRatio against the symbol's own
        // viewBox - new capability, see TryResolveUseTarget's remarks. A non-"symbol" target
        // (or a degenerate/non-finite fit) returns Matrix3x2.Identity/the unchanged state,
        // reproducing this method's original translate-only behavior exactly.
        if (!TryResolveUseTarget(element, target, state, out var viewportFit, out var targetState))
        {
            return;
        }

        var useTransform = viewportFit * Matrix3x2.CreateTranslation(offset) * transform;

        // A "use" element's own "filter" attribute (suppressed identically to shape/text
        // filtering whenever this call is itself part of a marker's own content) renders its
        // resolved target as one filtered unit via RenderFilteredGroup, instead of the plain
        // unfiltered re-entry into RenderElement - see RenderFilteredGroup's remarks for the full
        // group-filter algorithm. A "use" element is not itself rendered as part of any marker's
        // content (only RenderOneMarker increments markerDepth), so markerDepth > 0 here means the
        // *referenced* target is being rendered as part of a marker's own content instead.
        var suppressFilter = markerDepth > 0;
        var useFilterElement = suppressFilter ? null : ResolveFilterElement(element, context);
        if (useFilterElement == null)
        {
            RenderElement(target, targetState, useTransform, context, useDepth + 1, elementDepth + 1, markerDepth, ref totalElements, workBudget, filterWorkBudget, boundsPrePassBudget);
        }
        else
        {
            RenderFilteredGroup(useFilterElement, [target], targetState, useTransform, context, useDepth + 1, elementDepth + 1, markerDepth, ref totalElements, workBudget, filterWorkBudget, boundsPrePassBudget);
        }
    }

    /// <summary>
    ///     Resolves a <c>use</c> element's own nested-viewport establishment when its referenced
    ///     <paramref name="target"/> is a <c>symbol</c> - new capability, since neither
    ///     <see cref="RenderElement"/>'s <c>"g"</c>/<c>"symbol"</c> case nor this method itself
    ///     previously read a <c>symbol</c>'s own <c>width</c>/<c>height</c>/<c>viewBox</c> at all
    ///     (a <c>symbol</c> was, and when encountered directly still is, treated identically to a
    ///     plain <c>g</c> - see <see cref="RenderElement"/>'s remarks). A non-<c>symbol</c> target
    ///     is untouched: <paramref name="viewportFit"/> is <see cref="Matrix3x2.Identity"/> and
    ///     <paramref name="targetState"/> is <paramref name="state"/> unchanged, reproducing
    ///     <see cref="RenderUse"/>'s original translate-only behavior exactly for every other
    ///     reference target.
    /// </summary>
    /// <param name="useElement">The referencing <c>use</c> element.</param>
    /// <param name="target">The resolved reference target (any element; only a literal <c>symbol</c> is special-cased).</param>
    /// <param name="state">The <c>use</c> element's own cascaded render state, supplying the current viewport.</param>
    /// <param name="viewportFit">
    ///     The resulting transform fitting the symbol's own <c>viewBox</c> content into its
    ///     resolved <c>width</c>/<c>height</c> box, via the shared
    ///     <see cref="ComputePreserveAspectRatioFit"/> helper (honoring the symbol's own
    ///     <c>preserveAspectRatio</c> attribute) - or <see cref="Matrix3x2.Identity"/> for a
    ///     non-<c>symbol</c> target, a <c>symbol</c> with no <c>viewBox</c> (its own children
    ///     render directly in the new viewport's coordinate space, matching the existing
    ///     "no-viewBox g" simplification), or when this method returns <see langword="false"/>.
    /// </param>
    /// <param name="targetState">
    ///     <paramref name="state"/> with <see cref="RenderState.ViewportWidth"/>/
    ///     <see cref="RenderState.ViewportHeight"/> replaced by the symbol's own resolved
    ///     width/height, for a <c>symbol</c> target - the new percentage-resolution basis every
    ///     descendant of the symbol's content inherits; otherwise <paramref name="state"/> unchanged.
    /// </param>
    /// <returns>
    ///     <see langword="false"/> - a tolerant per-reference skip, mirroring
    ///     <see cref="TryComputeMarkerContentTransform"/>'s identical degenerate-sizing contract -
    ///     when <paramref name="target"/> is a <c>symbol</c> whose resolved width/height is
    ///     non-finite or not positive, or whose resolved <paramref name="viewportFit"/> is
    ///     non-finite; otherwise <see langword="true"/>.
    /// </returns>
    private static bool TryResolveUseTarget(XElement useElement, XElement target, RenderState state, out Matrix3x2 viewportFit, out RenderState targetState)
    {
        viewportFit = Matrix3x2.Identity;
        targetState = state;

        if (target.Name.LocalName != "symbol")
        {
            return true;
        }

        // The "use" element's own width/height take priority over the symbol's own, per the SVG
        // specification; either falls back to the current viewport if neither is present
        var width = GetOptionalFloat(useElement, "width", state, PercentageAxis.Horizontal)
            ?? GetOptionalFloat(target, "width", state, PercentageAxis.Horizontal)
            ?? state.ViewportWidth;
        var height = GetOptionalFloat(useElement, "height", state, PercentageAxis.Vertical)
            ?? GetOptionalFloat(target, "height", state, PercentageAxis.Vertical)
            ?? state.ViewportHeight;

        if (!float.IsFinite(width) || !float.IsFinite(height) || width <= 0f || height <= 0f)
        {
            return false;
        }

        var viewBox = ParseViewBox((string?)target.Attribute("viewBox"));
        if (viewBox.HasValue)
        {
            viewportFit = ComputePreserveAspectRatioFit(viewBox.Value.Origin, viewBox.Value.Size, width, height, GetPreserveAspectRatio(target));
            if (!IsFiniteTransform(viewportFit))
            {
                return false;
            }
        }

        targetState = state with { ViewportWidth = width, ViewportHeight = height };
        return true;
    }

    // ================================================================================================
    // "marker" element handling
    // ================================================================================================

    /// <summary>
    ///     One vertex of a <c>line</c>/<c>polyline</c>/<c>polygon</c>/<c>path</c>'s local-space
    ///     outline eligible to receive a <c>marker-start</c>/<c>marker-mid</c>/<c>marker-end</c>
    ///     marker, together with the unit tangent direction(s) of the segment(s) meeting at it -
    ///     used to compute an <c>orient="auto"</c> marker's rotation angle (see
    ///     <see cref="ComputeVertexAngleDegrees"/>).
    /// </summary>
    /// <remarks>
    ///     Either tangent is <see langword="null"/> at an open subpath's first (no incoming
    ///     segment) or last (no outgoing segment) vertex, or when the adjacent segment itself
    ///     degenerates to a zero-length direction (for example a repeated coordinate) - see
    ///     <see cref="ComputeCommandTangents"/>.
    /// </remarks>
    private readonly struct MarkerVertex(Vector2 position, Vector2? incomingTangent, Vector2? outgoingTangent)
    {
        /// <summary>The vertex's position, in the shape's own local (untransformed) space.</summary>
        public Vector2 Position { get; } = position;

        /// <summary>The unit direction of the segment arriving at this vertex, or <see langword="null"/> if none.</summary>
        public Vector2? IncomingTangent { get; } = incomingTangent;

        /// <summary>The unit direction of the segment leaving this vertex, or <see langword="null"/> if none.</summary>
        public Vector2? OutgoingTangent { get; } = outgoingTangent;

        /// <summary>Returns a copy of this vertex with <see cref="OutgoingTangent"/> replaced.</summary>
        /// <param name="outgoingTangent">The new outgoing tangent.</param>
        public MarkerVertex WithOutgoingTangent(Vector2? outgoingTangent) => new(Position, IncomingTangent, outgoingTangent);

        /// <summary>Returns a copy of this vertex with <see cref="IncomingTangent"/> replaced.</summary>
        /// <param name="incomingTangent">The new incoming tangent.</param>
        public MarkerVertex WithIncomingTangent(Vector2? incomingTangent) => new(Position, incomingTangent, OutgoingTangent);
    }

    /// <summary>
    ///     Classifies a marker-eligible vertex's position within its shape's whole, flattened
    ///     (whole-document-order, not per-subpath) vertex sequence, selecting which of
    ///     <c>marker-start</c>/<c>marker-mid</c>/<c>marker-end</c> applies to it.
    /// </summary>
    private enum MarkerVertexRole
    {
        /// <summary>The very first vertex of the whole shape - uses <c>marker-start</c>.</summary>
        Start,

        /// <summary>Every vertex strictly between the first and last - uses <c>marker-mid</c>.</summary>
        Mid,

        /// <summary>The very last vertex of the whole shape - uses <c>marker-end</c>.</summary>
        End
    }

    /// <summary>
    ///     Builds the ordered, whole-shape list of marker-eligible vertices (and their tangents)
    ///     from a <c>line</c>/<c>polyline</c>/<c>polygon</c>/<c>path</c>'s already-built
    ///     local-space outline, for <see cref="RenderMarkers"/> to place markers along.
    /// </summary>
    /// <param name="localPath">
    ///     The shape's local-space outline, exactly as returned by <see cref="BuildLinePath"/>/
    ///     <see cref="BuildPolyPath"/>/<see cref="BuildPathDataPath"/> - reused directly, never
    ///     re-parsed from the shape's own raw attribute text.
    /// </param>
    /// <returns>
    ///     The vertices in whole-path document order: every subpath's start point, followed by
    ///     every one of its non-<c>Close</c> commands' end points, subpaths concatenated in the
    ///     order they appear in <paramref name="localPath"/>. A multi-subpath <c>path</c>'s
    ///     <c>marker-start</c>/<c>marker-end</c> therefore apply only to the very first/last
    ///     vertex of the whole path, not per-subpath - a deliberate, documented simplification
    ///     (matches at least one common browser's behavior; the SVG specification's own wording
    ///     on this point is not unambiguous across implementations) rather than a stricter
    ///     per-subpath interpretation. A <c>Close</c> command contributes no new vertex (it always
    ///     returns to the subpath's own <see cref="Subpath.Start"/>, already recorded), but its
    ///     implicit closing segment's tangent - computed identically to an equivalent
    ///     <see cref="PathCommandType.LineTo"/> back to <see cref="Subpath.Start"/> - is still
    ///     folded into both the subpath's last vertex's outgoing tangent and its first vertex's
    ///     incoming tangent, so a closed subpath's <c>orient="auto"</c> orientation at either end
    ///     reflects the closing edge too, not only the open-path edge that happens to meet it.
    /// </returns>
    private static List<MarkerVertex> BuildMarkerVertices(Path localPath)
    {
        var vertices = new List<MarkerVertex>();
        foreach (var subpath in localPath.Subpaths)
        {
            var subpathStartIndex = vertices.Count;
            vertices.Add(new MarkerVertex(subpath.Start, incomingTangent: null, outgoingTangent: null));
            var current = subpath.Start;
            var closed = false;

            foreach (var command in subpath.Commands)
            {
                if (command.Type == PathCommandType.Close)
                {
                    // Returns to Start (already recorded above) without introducing a new vertex
                    // of its own - the closing segment's own tangent is folded in below, once the
                    // subpath's last real vertex is known
                    closed = true;
                    current = subpath.Start;
                    continue;
                }

                var (outgoing, incoming) = ComputeCommandTangents(command, current);

                // Fold this command's outgoing tangent into the vertex it starts from (the
                // previously-added vertex, whether that was the subpath's own Start or a prior
                // command's end point)
                var previousIndex = vertices.Count - 1;
                vertices[previousIndex] = vertices[previousIndex].WithOutgoingTangent(outgoing);

                vertices.Add(new MarkerVertex(command.EndPoint, incoming, outgoingTangent: null));
                current = command.EndPoint;
            }

            // A closed subpath with at least one real segment beyond its own Start has an
            // implicit closing edge back to Start - fold its tangent into the last vertex's
            // outgoing tangent and the first vertex's incoming tangent, exactly as any other
            // segment's tangent is folded into its two endpoints above
            var lastIndex = vertices.Count - 1;
            if (closed && lastIndex != subpathStartIndex)
            {
                var (closingOutgoing, closingIncoming) = ComputeCommandTangents(
                    PathCommand.LineTo(subpath.Start), vertices[lastIndex].Position);
                vertices[lastIndex] = vertices[lastIndex].WithOutgoingTangent(closingOutgoing);
                vertices[subpathStartIndex] = vertices[subpathStartIndex].WithIncomingTangent(closingIncoming);
            }
        }

        return vertices;
    }

    /// <summary>
    ///     Computes one path command's outgoing (leaving its start point) and incoming (arriving
    ///     at its end point) unit tangent directions, per this class's documented per-command-type
    ///     rules.
    /// </summary>
    /// <param name="command">The command to inspect - a <see cref="PathCommandType.LineTo"/>, <see cref="PathCommandType.QuadraticBezierTo"/>, or <see cref="PathCommandType.CubicBezierTo"/>.</param>
    /// <param name="start">The command's start point (the previous vertex's position).</param>
    /// <returns>
    ///     The outgoing/incoming unit tangents, or <see langword="null"/> for either when the
    ///     relevant control points/endpoints are coincident (a zero-length direction has no
    ///     meaningful tangent).
    /// </returns>
    /// <remarks>
    ///     <see cref="PathCommandType.ArcTo"/> is deliberately not one of this method's cases:
    ///     every one of this class's own shape builders (<see cref="BuildRectPath"/>,
    ///     <see cref="BuildEllipsePath"/>, and <see cref="PathDataParser"/>'s own arc handling)
    ///     converts an SVG arc to cubic Bezier segments immediately, via <see cref="AppendArcTo"/>/
    ///     <see cref="PathDataParser.AppendArc"/>, before ever building a <see cref="Path"/> -
    ///     confirmed directly from this codec's own source, not merely assumed - so an
    ///     <see cref="PathCommandType.ArcTo"/> command never actually appears in a local-space
    ///     <see cref="Path"/> this method is called against. The <c>default</c> case below still
    ///     handles it (and <see cref="PathCommandType.Close"/>, though that is filtered out by
    ///     <see cref="BuildMarkerVertices"/> before reaching here) defensively, returning "no
    ///     tangent" rather than throwing, so a future change elsewhere in this class that ever did
    ///     produce one would degrade to an un-oriented marker rather than an uncaught exception.
    /// </remarks>
    private static (Vector2? Outgoing, Vector2? Incoming) ComputeCommandTangents(PathCommand command, Vector2 start)
    {
        switch (command.Type)
        {
            case PathCommandType.LineTo:
                var lineDirection = NormalizeOrNull(command.EndPoint - start);
                return (lineDirection, lineDirection);

            case PathCommandType.QuadraticBezierTo:
                var outgoingQuad = NormalizeOrNull(command.Control1 - start)
                    ?? NormalizeOrNull(command.EndPoint - start);
                var incomingQuad = NormalizeOrNull(command.EndPoint - command.Control1)
                    ?? NormalizeOrNull(command.EndPoint - start);
                return (outgoingQuad, incomingQuad);

            case PathCommandType.CubicBezierTo:
                var outgoingCubic = NormalizeOrNull(command.Control1 - start)
                    ?? NormalizeOrNull(command.Control2 - start)
                    ?? NormalizeOrNull(command.EndPoint - start);
                var incomingCubic = NormalizeOrNull(command.EndPoint - command.Control2)
                    ?? NormalizeOrNull(command.EndPoint - command.Control1)
                    ?? NormalizeOrNull(command.EndPoint - start);
                return (outgoingCubic, incomingCubic);

            default:
                return (null, null);
        }
    }

    /// <summary>Normalizes <paramref name="vector"/>, tolerating a zero-length or non-finite result.</summary>
    /// <param name="vector">The vector to normalize.</param>
    /// <returns>
    ///     The unit-length direction, or <see langword="null"/> if <paramref name="vector"/>'s
    ///     length is zero, subnormal-to-zero, or non-finite (a degenerate direction has no
    ///     meaningful orientation to contribute).
    /// </returns>
    private static Vector2? NormalizeOrNull(Vector2 vector)
    {
        var lengthSquared = vector.LengthSquared();
        return float.IsFinite(lengthSquared) && lengthSquared > float.Epsilon
            ? Vector2.Normalize(vector)
            : null;
    }

    /// <summary>
    ///     Computes an <c>orient="auto"</c> marker's rotation angle at <paramref name="vertex"/>,
    ///     per the SVG averaging rule: the average of the incoming and outgoing tangents when both
    ///     are present, falling back to whichever single tangent is present, or <c>0</c> degrees
    ///     for a fully degenerate (zero-length) vertex.
    /// </summary>
    /// <param name="vertex">The vertex to orient a marker at.</param>
    /// <returns>The computed angle, in degrees, suitable for <see cref="Matrix3x2.CreateRotation(float)"/> via <see cref="DegreesToRadians"/>.</returns>
    private static float ComputeVertexAngleDegrees(MarkerVertex vertex)
    {
        Vector2? direction;
        if (vertex.IncomingTangent.HasValue && vertex.OutgoingTangent.HasValue)
        {
            // Average the two tangents; if they nearly cancel (a near-180-degree reversal), fall
            // back to just the outgoing tangent (or the incoming one, if outgoing is somehow also
            // absent) rather than an undefined zero-length average
            var sum = vertex.IncomingTangent.Value + vertex.OutgoingTangent.Value;
            direction = NormalizeOrNull(sum) ?? vertex.OutgoingTangent ?? vertex.IncomingTangent;
        }
        else
        {
            direction = vertex.OutgoingTangent ?? vertex.IncomingTangent;
        }

        return direction.HasValue
            ? MathF.Atan2(direction.Value.Y, direction.Value.X) * (180f / MathF.PI)
            : 0f;
    }

    /// <summary>
    ///     Renders every marker attached to a marker-eligible shape (<c>line</c>/<c>polyline</c>/
    ///     <c>polygon</c>/<c>path</c>), one per vertex whose corresponding
    ///     <c>marker-start</c>/<c>marker-mid</c>/<c>marker-end</c> spec is not <c>none</c> and
    ///     resolves to an actual <c>marker</c> element.
    /// </summary>
    /// <param name="localPath">The shape's local-space outline (see <see cref="BuildMarkerVertices"/>).</param>
    /// <param name="state">The shape's own cascaded render state, supplying the three marker specs and local <c>stroke-width</c>.</param>
    /// <param name="transform">The shape's own accumulated transform - every marker instance is transformed through this same transform.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="useDepth">The current <c>use</c>-reference nesting depth, propagated unchanged to each marker's content.</param>
    /// <param name="elementDepth">The current element-tree recursion depth, propagated to each marker's content.</param>
    /// <param name="markerDepth">The current <c>marker</c>-reference nesting depth, propagated to each marker's content.</param>
    /// <param name="totalElements">The running total-rendered-elements count.</param>
    /// <param name="workBudget">The shared geometry-parsing work budget.</param>
    /// <param name="filterWorkBudget">The shared cumulative filter-evaluation work budget.</param>
    /// <param name="boundsPrePassBudget">The shared cumulative bounds-pre-pass work budget.</param>
    /// <remarks>
    ///     Never called for <c>rect</c>/<c>circle</c>/<c>ellipse</c> - those shapes have no
    ///     natural vertices to orient a marker along, per the SVG specification, and this class's
    ///     <see cref="RenderElement"/> dispatch simply never routes them through this method.
    ///     Cheaply no-ops (before building any vertex list) when every one of
    ///     <paramref name="state"/>'s three marker specs is <c>"none"</c> - the overwhelming
    ///     majority of real-world shapes - so ordinary marker-free rendering pays only one string
    ///     comparison per marker-eligible element.
    /// </remarks>
    private static void RenderMarkers(
        Path localPath,
        RenderState state,
        Matrix3x2 transform,
        RenderContext context,
        int useDepth,
        int elementDepth,
        int markerDepth,
        ref int totalElements,
        GeometryWorkBudget workBudget,
        FilterWorkBudget filterWorkBudget,
        BoundsPrePassWorkBudget boundsPrePassBudget)
    {
        if (state.MarkerStart == "none" && state.MarkerMid == "none" && state.MarkerEnd == "none")
        {
            return;
        }

        var vertices = BuildMarkerVertices(localPath);
        if (vertices.Count < 2)
        {
            // A single-vertex (or empty) shape has no segment to orient a marker along - no
            // start, mid, or end marker applies, per the SVG specification
            return;
        }

        for (var i = 0; i < vertices.Count; i++)
        {
            MarkerVertexRole role;
            if (i == 0)
            {
                role = MarkerVertexRole.Start;
            }
            else if (i == vertices.Count - 1)
            {
                role = MarkerVertexRole.End;
            }
            else
            {
                role = MarkerVertexRole.Mid;
            }

            var spec = role switch
            {
                MarkerVertexRole.Start => state.MarkerStart,
                MarkerVertexRole.End => state.MarkerEnd,
                _ => state.MarkerMid
            };

            if (string.Equals(spec.Trim(), "none", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var markerElement = ResolveMarkerElement(spec, context);
            if (markerElement == null)
            {
                continue;
            }

            var vertex = vertices[i];
            var angleDegrees = ComputeVertexAngleDegrees(vertex);
            RenderOneMarker(
                markerElement,
                vertex.Position,
                angleDegrees,
                isStartVertex: role == MarkerVertexRole.Start,
                state.StrokeWidth,
                state,
                transform,
                context,
                useDepth,
                elementDepth,
                markerDepth,
                ref totalElements,
                workBudget,
                filterWorkBudget,
                boundsPrePassBudget);
        }
    }

    /// <summary>
    ///     Resolves a raw <c>marker-start</c>/<c>marker-mid</c>/<c>marker-end</c> specification
    ///     (<c>url(#id)</c>) to its referenced <c>marker</c> element, reusing the exact same
    ///     <c>url(#id)</c>-parsing and dangling-reference tolerance as <see cref="ResolvePaint"/>.
    /// </summary>
    /// <param name="spec">The raw, already-known-non-<c>"none"</c> marker specification.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <returns>
    ///     The referenced <c>marker</c> element, or <see langword="null"/> if <paramref name="spec"/>
    ///     is not <c>url(#id)</c> syntax, the id is dangling (no matching element), or the
    ///     resolved element is not literally a <c>marker</c> (a <c>url(#id)</c> mistakenly
    ///     pointing at an unrelated element, such as a <c>rect</c>, is tolerated the same way).
    /// </returns>
    private static XElement? ResolveMarkerElement(string spec, RenderContext context)
    {
        var trimmed = spec.Trim();
        if (!trimmed.StartsWith("url(", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var id = ExtractUrlId(trimmed);
        if (id == null || !context.IdIndex.TryGetValue(id, out var element))
        {
            return null;
        }

        return element.Name.LocalName == "marker" ? element : null;
    }

    /// <summary>
    ///     Renders one resolved <c>marker</c> element's content at one shape vertex: computes the
    ///     marker's own <c>refX</c>/<c>refY</c>/<c>markerWidth</c>/<c>markerHeight</c>/
    ///     <c>markerUnits</c>/<c>orient</c>/<c>viewBox</c> transform, composes it with
    ///     <paramref name="shapeTransform"/>, then re-enters <see cref="RenderElement"/> for each
    ///     of the marker's own children with a fresh cascade seeded from the marker element itself
    ///     (never inheriting <paramref name="shapeTransform"/>'s own fill/stroke state).
    /// </summary>
    /// <param name="markerElement">The resolved <c>marker</c> element.</param>
    /// <param name="vertexPosition">The vertex's position, in the referencing shape's own local space.</param>
    /// <param name="vertexAngleDegrees">
    ///     The vertex's own computed tangent angle (see <see cref="ComputeVertexAngleDegrees"/>),
    ///     used when <c>orient</c> is <c>auto</c>/<c>auto-start-reverse</c>/absent.
    /// </param>
    /// <param name="isStartVertex">
    ///     Whether this vertex is the referencing shape's very first vertex - needed only to
    ///     resolve <c>orient="auto-start-reverse"</c>, which reverses by 180 degrees at the start
    ///     vertex only.
    /// </param>
    /// <param name="localStrokeWidth">
    ///     The referencing shape's own <c>stroke-width</c>, in local (pre-<paramref name="shapeTransform"/>)
    ///     user-space units - not the pixel-scaled effective width - used to scale this marker
    ///     instance when <c>markerUnits</c> is <c>strokeWidth</c> (the SVG default). Deliberately
    ///     left unscaled: <paramref name="shapeTransform"/>, composed last below, is the only place
    ///     the pixel scale is applied, so composing an already-pixel-scaled width here would apply
    ///     that scale twice.
    /// </param>
    /// <param name="viewportState">
    ///     The referencing shape's own cascaded render state, supplying the current viewport/
    ///     font-size basis for the marker's own percentage-eligible attributes, and inherited
    ///     unchanged into the marker's own fresh content cascade (a <c>marker</c> element
    ///     establishes no new viewport of its own).
    /// </param>
    /// <param name="shapeTransform">The referencing shape's own accumulated transform.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="useDepth">The current <c>use</c>-reference nesting depth, propagated unchanged to the marker's content.</param>
    /// <param name="elementDepth">The current element-tree recursion depth, propagated (incremented by one) to the marker's content.</param>
    /// <param name="markerDepth">
    ///     The current <c>marker</c>-reference nesting depth, checked against
    ///     <see cref="MaxMarkerDepth"/> before any other work, then propagated (incremented by
    ///     one) to the marker's own content.
    /// </param>
    /// <param name="totalElements">The running total-rendered-elements count.</param>
    /// <param name="workBudget">The shared geometry-parsing work budget.</param>
    /// <param name="filterWorkBudget">The shared cumulative filter-evaluation work budget.</param>
    /// <param name="boundsPrePassBudget">The shared cumulative bounds-pre-pass work budget.</param>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="markerDepth"/> has already reached
    ///     <see cref="MaxMarkerDepth"/>, guarding against a marker-referencing-marker reference
    ///     cycle (directly, or via a chain) that would otherwise recurse indefinitely - mirrors
    ///     <see cref="RenderUse"/>'s identical <see cref="MaxUseDepth"/> guard, checked before any
    ///     other work for the same reason.
    /// </exception>
    /// <remarks>
    ///     A degenerate <c>markerWidth</c>/<c>markerHeight</c> (non-finite or non-positive), a
    ///     degenerate <c>markerUnits="strokeWidth"</c> scale (non-finite, non-positive, or beyond
    ///     <see cref="MaxCoordinateMagnitude"/> - which also tolerantly covers a zero/negative
    ///     <c>stroke-width</c>, since <c>stroke="none"</c> shapes still have a defined, positive
    ///     <c>stroke-width</c> value even when nothing is actually stroked), or a non-finite
    ///     composed transform are all tolerant per-marker-instance skips (this one vertex renders
    ///     no marker), mirroring <see cref="RenderStroke"/>'s/<see cref="RenderElement"/>'s own
    ///     established tolerant-skip conventions - none of them abort the whole document.
    /// </remarks>
    private static void RenderOneMarker(
        XElement markerElement,
        Vector2 vertexPosition,
        float vertexAngleDegrees,
        bool isStartVertex,
        float localStrokeWidth,
        RenderState viewportState,
        Matrix3x2 shapeTransform,
        RenderContext context,
        int useDepth,
        int elementDepth,
        int markerDepth,
        ref int totalElements,
        GeometryWorkBudget workBudget,
        FilterWorkBudget filterWorkBudget,
        BoundsPrePassWorkBudget boundsPrePassBudget)
    {
        if (markerDepth >= MaxMarkerDepth)
        {
            throw new InvalidDataException("Exceeded the maximum <marker> reference nesting depth.");
        }

        if (!TryComputeMarkerContentTransform(markerElement, vertexPosition, vertexAngleDegrees, isStartVertex, localStrokeWidth, viewportState, shapeTransform, out var contentTransform))
        {
            return;
        }

        // A marker's content cascade starts fresh from RenderState.Initial (seeded by the marker
        // element's own presentation attributes, if any) - never inherited from the referencing
        // shape's own state, per the SVG specification's marker-content-is-independent model and
        // this class's explicit task contract. The current viewport is the one exception:
        // percentage geometry within the marker's own content still resolves against the
        // referencing shape's own cascaded viewport, not a marker-local one (a "marker" element
        // establishes no new viewport of its own)
        var markerState = ApplyPresentationAttributes(RenderState.Initial, markerElement) with
        {
            ViewportWidth = viewportState.ViewportWidth,
            ViewportHeight = viewportState.ViewportHeight
        };
        foreach (var child in markerElement.Elements())
        {
            RenderElement(child, markerState, contentTransform, context, useDepth, elementDepth + 1, markerDepth + 1, ref totalElements, workBudget, filterWorkBudget, boundsPrePassBudget);
        }
    }

    /// <summary>
    ///     Computes one <c>marker</c> element instance's own content transform - the marker's
    ///     <c>refX</c>/<c>refY</c>/<c>markerWidth</c>/<c>markerHeight</c>/<c>markerUnits</c>/
    ///     <c>orient</c>/<c>viewBox</c> composed with the placement vertex and the referencing
    ///     shape's own accumulated transform - shared identically by <see cref="RenderOneMarker"/>
    ///     (which then re-enters <see cref="RenderElement"/> to actually paint the marker's
    ///     content) and <see cref="ComputeOneMarkerLocalBounds"/> (which instead unions the
    ///     marker's content bounds into a filtered group's own pre-render bounds pass), so both
    ///     call sites can never silently diverge on how a marker instance is placed/scaled/oriented.
    /// </summary>
    /// <param name="markerElement">The resolved <c>marker</c> element.</param>
    /// <param name="vertexPosition">The vertex's position, in the referencing shape's own local space.</param>
    /// <param name="vertexAngleDegrees">
    ///     The vertex's own computed tangent angle (see <see cref="ComputeVertexAngleDegrees"/>),
    ///     used when <c>orient</c> is <c>auto</c>/<c>auto-start-reverse</c>/absent.
    /// </param>
    /// <param name="isStartVertex">
    ///     Whether this vertex is the referencing shape's very first vertex - needed only to
    ///     resolve <c>orient="auto-start-reverse"</c>, which reverses by 180 degrees at the start
    ///     vertex only.
    /// </param>
    /// <param name="localStrokeWidth">
    ///     The referencing shape's own <c>stroke-width</c>, in local (pre-<paramref name="shapeTransform"/>)
    ///     user-space units - see <see cref="RenderOneMarker"/>'s identical parameter for the full
    ///     rationale.
    /// </param>
    /// <param name="viewportState">
    ///     The referencing shape's own cascaded render state, supplying the current viewport/
    ///     font-size basis for the marker's own percentage-eligible <c>markerWidth</c>/
    ///     <c>markerHeight</c>/<c>refX</c>/<c>refY</c> - a <c>marker</c> element establishes no new
    ///     viewport of its own.
    /// </param>
    /// <param name="shapeTransform">The referencing shape's own accumulated transform.</param>
    /// <param name="contentTransform">
    ///     The resulting composed transform from the marker's own local content space into
    ///     <paramref name="shapeTransform"/>'s reference frame, or <see cref="Matrix3x2.Identity"/>
    ///     if this method returns <see langword="false"/>.
    /// </param>
    /// <returns>
    ///     <see langword="false"/> if <paramref name="markerElement"/>'s own <c>markerWidth</c>/
    ///     <c>markerHeight</c> is non-finite/non-positive, its resolved <c>markerUnits</c> scale is
    ///     non-finite/non-positive/beyond <see cref="MaxCoordinateMagnitude"/>, or the final
    ///     composed <paramref name="contentTransform"/> is non-finite - all tolerant
    ///     per-marker-instance skip conditions (see <see cref="RenderOneMarker"/>'s remarks);
    ///     otherwise <see langword="true"/>.
    /// </returns>
    /// <remarks>
    ///     When <paramref name="markerElement"/> carries no explicit <c>preserveAspectRatio</c>
    ///     attribute, this method reproduces the exact pre-existing fit math - a plain uniform
    ///     <c>Min(markerWidth/viewBoxWidth, markerHeight/viewBoxHeight)</c> scale about the origin,
    ///     silently dropping the viewBox's own <c>Origin</c> and applying no centering offset - so
    ///     that behavior is byte-for-byte unchanged (per this phase's marker judgment call: changing
    ///     it would double-count <c>refX</c>/<c>refY</c> anchoring against a centering offset that
    ///     never existed before). Only an <b>explicit</b> <c>preserveAspectRatio</c> attribute routes
    ///     through the shared <see cref="ComputePreserveAspectRatioFit"/> helper, which does honor the
    ///     viewBox's own origin and the requested align/meetOrSlice, mapping <c>refX</c>/<c>refY</c>
    ///     through that same fit before anchoring - new capability, scoped to this explicit-attribute
    ///     path only.
    /// </remarks>
    private static bool TryComputeMarkerContentTransform(
        XElement markerElement,
        Vector2 vertexPosition,
        float vertexAngleDegrees,
        bool isStartVertex,
        float localStrokeWidth,
        RenderState viewportState,
        Matrix3x2 shapeTransform,
        out Matrix3x2 contentTransform)
    {
        contentTransform = Matrix3x2.Identity;

        var markerWidth = GetFloatAttribute(markerElement, "markerWidth", viewportState, PercentageAxis.Horizontal, 3f);
        var markerHeight = GetFloatAttribute(markerElement, "markerHeight", viewportState, PercentageAxis.Vertical, 3f);
        if (!float.IsFinite(markerWidth) || !float.IsFinite(markerHeight) || markerWidth <= 0f || markerHeight <= 0f)
        {
            return false;
        }

        var refX = GetFloatAttribute(markerElement, "refX", viewportState, PercentageAxis.Horizontal);
        var refY = GetFloatAttribute(markerElement, "refY", viewportState, PercentageAxis.Vertical);

        var isUserSpaceOnUse = string.Equals(
            (string?)markerElement.Attribute("markerUnits"), "userSpaceOnUse", StringComparison.OrdinalIgnoreCase);
        var unitsScale = isUserSpaceOnUse ? 1f : localStrokeWidth;
        if (!float.IsFinite(unitsScale) || unitsScale <= 0f || unitsScale > MaxCoordinateMagnitude)
        {
            return false;
        }

        var angleDegrees = ParseMarkerOrient((string?)markerElement.Attribute("orient"), vertexAngleDegrees, isStartVertex);

        var viewBox = ParseViewBox((string?)markerElement.Attribute("viewBox"));
        var explicitParRaw = (string?)markerElement.Attribute("preserveAspectRatio");

        Matrix3x2 fitTransform;
        Vector2 refInFitSpace;
        if (explicitParRaw != null && viewBox.HasValue)
        {
            // Explicit-preserveAspectRatio path - new capability, see this method's remarks
            var par = ParsePreserveAspectRatio(explicitParRaw);
            fitTransform = ComputePreserveAspectRatioFit(viewBox.Value.Origin, viewBox.Value.Size, markerWidth, markerHeight, par);
            refInFitSpace = Vector2.Transform(new Vector2(refX, refY), fitTransform);
        }
        else
        {
            // Default path - deliberately NOT routed through ComputePreserveAspectRatioFit, to
            // guarantee bit-for-bit unchanged output versus this method's pre-existing behavior (see
            // this method's remarks)
            var contentScale = viewBox.HasValue
                ? MathF.Min(markerWidth / viewBox.Value.Size.X, markerHeight / viewBox.Value.Size.Y)
                : 1f;
            fitTransform = Matrix3x2.CreateScale(contentScale);
            refInFitSpace = new Vector2(refX * contentScale, refY * contentScale);
        }

        // Composition order (see RenderOneMarker's remarks): fit the marker's own viewBox content
        // into its markerWidth/markerHeight box, recenter on its refX/refY anchor (mapped through
        // that same fit), scale by the markerUnits-derived units scale, rotate by the resolved
        // orientation angle, translate to the shape-local vertex position, then finally compose with
        // the shape's own accumulated transform - row-vector convention, matching every other
        // transform composition in this class (Vector2.Transform(p, A * B) applies A first, then B)
        contentTransform =
            fitTransform
            * Matrix3x2.CreateTranslation(-refInFitSpace)
            * Matrix3x2.CreateScale(unitsScale)
            * Matrix3x2.CreateRotation(DegreesToRadians(angleDegrees))
            * Matrix3x2.CreateTranslation(vertexPosition)
            * shapeTransform;

        return IsFiniteTransform(contentTransform);
    }

    /// <summary>
    ///     Computes one <c>marker</c> element instance's own content bounds at one shape vertex -
    ///     the bounds-only counterpart of <see cref="RenderOneMarker"/>, sharing its exact
    ///     placement/scale/orientation transform (via <see cref="TryComputeMarkerContentTransform"/>)
    ///     and <see cref="MaxMarkerDepth"/> cycle guard, but unioning
    ///     <see cref="ComputeSubtreeLocalBounds"/>'s bounds over the marker's own children instead
    ///     of re-entering <see cref="RenderElement"/> to actually paint them. Used exclusively by
    ///     <see cref="ComputeMarkerContentLocalBounds"/>, itself used exclusively by
    ///     <see cref="ComputeSubtreeLocalBounds"/>'s <c>line</c>/<c>polyline</c>/<c>polygon</c>/
    ///     <c>path</c> cases, so a filtered group's own offscreen buffer is always sized large
    ///     enough to contain any marker pixels the real render pass paints for it (see this
    ///     method's remarks and <see cref="ComputeSubtreeLocalBounds"/>'s remarks for the full
    ///     rationale).
    /// </summary>
    /// <param name="markerElement">The resolved <c>marker</c> element.</param>
    /// <param name="vertexPosition">The vertex's position, in the referencing shape's own local space.</param>
    /// <param name="vertexAngleDegrees">The vertex's own computed tangent angle.</param>
    /// <param name="isStartVertex">Whether this vertex is the referencing shape's very first vertex.</param>
    /// <param name="localStrokeWidth">The referencing shape's own <c>stroke-width</c>, in local units.</param>
    /// <param name="viewportState">The referencing shape's own cascaded render state, supplying the current viewport/font-size basis for the marker's own percentage-eligible attributes.</param>
    /// <param name="shapeTransform">The referencing shape's own accumulated transform.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="useDepth">The current <c>use</c>-reference nesting depth.</param>
    /// <param name="elementDepth">The current recursion depth, propagated (incremented by one) to the marker's content.</param>
    /// <param name="markerDepth">
    ///     The current <c>marker</c>-reference nesting depth, checked against
    ///     <see cref="MaxMarkerDepth"/> before any other work - identical to
    ///     <see cref="RenderOneMarker"/>'s own guard, so a marker-referencing-marker reference
    ///     cycle throws during this bounds pre-pass exactly as it would during the real render
    ///     pass that follows it, rather than only being caught later.
    /// </param>
    /// <param name="totalElements">
    ///     The running total-rendered-elements count - the caller-supplied instance, which for
    ///     <see cref="RenderFilteredGroup"/>'s own bounds pre-pass is a local, independently
    ///     bounded scratch counter, never the real per-<c>Load</c>-call counter (see
    ///     <see cref="ComputeSubtreeLocalBounds"/>'s remarks).
    /// </param>
    /// <param name="workBudget">The caller-supplied geometry-parsing work budget, subject to the same scoping as <paramref name="totalElements"/>.</param>
    /// <param name="boundsPrePassBudget">
    ///     The shared, per-<c>Load</c>-call cumulative bounds-pre-pass work budget (see
    ///     <see cref="BoundsPrePassWorkBudget"/>) - deliberately not local-scratch-scoped like
    ///     <paramref name="totalElements"/>/<paramref name="workBudget"/> above, so this method's
    ///     own <see cref="ComputeSubtreeLocalBounds"/> recursion still contributes toward the one
    ///     cumulative ceiling shared by every nested filtered group in the whole document.
    /// </param>
    /// <returns>
    ///     The union of every descendant shape/text element's stroke-expanded, transformed local
    ///     bounds within the marker's own content, mapped through the composed marker-instance
    ///     transform - or <see langword="null"/> if the marker instance itself is degenerate/
    ///     skipped (see <see cref="TryComputeMarkerContentTransform"/>) or its own content paints
    ///     nothing.
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="markerDepth"/> has already reached <see cref="MaxMarkerDepth"/>.
    /// </exception>
    private static Rect? ComputeOneMarkerLocalBounds(
        XElement markerElement,
        Vector2 vertexPosition,
        float vertexAngleDegrees,
        bool isStartVertex,
        float localStrokeWidth,
        RenderState viewportState,
        Matrix3x2 shapeTransform,
        RenderContext context,
        int useDepth,
        int elementDepth,
        int markerDepth,
        ref int totalElements,
        GeometryWorkBudget workBudget,
        BoundsPrePassWorkBudget boundsPrePassBudget)
    {
        if (markerDepth >= MaxMarkerDepth)
        {
            throw new InvalidDataException("Exceeded the maximum <marker> reference nesting depth.");
        }

        if (!TryComputeMarkerContentTransform(markerElement, vertexPosition, vertexAngleDegrees, isStartVertex, localStrokeWidth, viewportState, shapeTransform, out var contentTransform))
        {
            return null;
        }

        var markerState = ApplyPresentationAttributes(RenderState.Initial, markerElement) with
        {
            ViewportWidth = viewportState.ViewportWidth,
            ViewportHeight = viewportState.ViewportHeight
        };
        var bounds = Rect.Empty;
        foreach (var child in markerElement.Elements())
        {
            var childBounds = ComputeSubtreeLocalBounds(child, markerState, contentTransform, context, useDepth, elementDepth + 1, markerDepth + 1, ref totalElements, workBudget, boundsPrePassBudget);
            if (childBounds != null)
            {
                bounds = bounds.Union(childBounds.Value);
            }
        }

        return bounds.IsEmpty ? null : bounds;
    }

    /// <summary>
    ///     Computes the union of every marker instance's own content bounds a
    ///     <c>line</c>/<c>polyline</c>/<c>polygon</c>/<c>path</c>'s <c>marker-start</c>/
    ///     <c>marker-mid</c>/<c>marker-end</c> presentation attributes would place along
    ///     <paramref name="localPath"/> - the bounds-only counterpart of <see cref="RenderMarkers"/>,
    ///     sharing its exact vertex-eligibility/role-selection logic, used exclusively by
    ///     <see cref="ComputeSubtreeLocalBounds"/> so a filtered group's own offscreen buffer is
    ///     sized large enough to contain marker pixels that extend beyond the host shape's own
    ///     stroke-expanded outline (a real-world-common case for arrowhead markers) - without this,
    ///     those marker pixels would be silently clipped by a too-small offscreen buffer during the
    ///     real render pass (see <see cref="ComputeSubtreeLocalBounds"/>'s remarks).
    /// </summary>
    /// <param name="localPath">The shape's already-built local-space outline.</param>
    /// <param name="state">The cascaded render state.</param>
    /// <param name="transform">The accumulated transform from local space into the caller's reference frame.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="useDepth">The current <c>use</c>-reference nesting depth.</param>
    /// <param name="elementDepth">The current recursion depth at the host shape element itself.</param>
    /// <param name="markerDepth">The current <c>marker</c>-reference nesting depth.</param>
    /// <param name="totalElements">The running total-rendered-elements count (see <see cref="ComputeOneMarkerLocalBounds"/>'s remarks on scoping).</param>
    /// <param name="workBudget">The shared geometry-parsing work budget (see <see cref="ComputeOneMarkerLocalBounds"/>'s remarks on scoping).</param>
    /// <param name="boundsPrePassBudget">The shared, per-<c>Load</c>-call cumulative bounds-pre-pass work budget (see <see cref="ComputeOneMarkerLocalBounds"/>'s remarks on scoping).</param>
    /// <returns>
    ///     The union of every placed marker instance's own content bounds, or <see langword="null"/>
    ///     if <paramref name="state"/> specifies no markers at all, <paramref name="localPath"/> has
    ///     fewer than two marker-eligible vertices, every referenced marker id is dangling/invalid,
    ///     or every placed marker instance's own content paints nothing.
    /// </returns>
    private static Rect? ComputeMarkerContentLocalBounds(
        Path localPath,
        RenderState state,
        Matrix3x2 transform,
        RenderContext context,
        int useDepth,
        int elementDepth,
        int markerDepth,
        ref int totalElements,
        GeometryWorkBudget workBudget,
        BoundsPrePassWorkBudget boundsPrePassBudget)
    {
        if (state.MarkerStart == "none" && state.MarkerMid == "none" && state.MarkerEnd == "none")
        {
            return null;
        }

        var vertices = BuildMarkerVertices(localPath);
        if (vertices.Count < 2)
        {
            return null;
        }

        var bounds = Rect.Empty;
        for (var i = 0; i < vertices.Count; i++)
        {
            MarkerVertexRole role;
            if (i == 0)
            {
                role = MarkerVertexRole.Start;
            }
            else if (i == vertices.Count - 1)
            {
                role = MarkerVertexRole.End;
            }
            else
            {
                role = MarkerVertexRole.Mid;
            }

            var spec = role switch
            {
                MarkerVertexRole.Start => state.MarkerStart,
                MarkerVertexRole.End => state.MarkerEnd,
                _ => state.MarkerMid
            };

            if (string.Equals(spec.Trim(), "none", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var markerElement = ResolveMarkerElement(spec, context);
            if (markerElement == null)
            {
                continue;
            }

            var vertex = vertices[i];
            var angleDegrees = ComputeVertexAngleDegrees(vertex);
            var markerBounds = ComputeOneMarkerLocalBounds(
                markerElement,
                vertex.Position,
                angleDegrees,
                isStartVertex: role == MarkerVertexRole.Start,
                state.StrokeWidth,
                state,
                transform,
                context,
                useDepth,
                elementDepth,
                markerDepth,
                ref totalElements,
                workBudget,
                boundsPrePassBudget);

            if (markerBounds != null)
            {
                bounds = bounds.Union(markerBounds.Value);
            }
        }

        return bounds.IsEmpty ? null : bounds;
    }

    /// <summary>
    ///     Resolves a <c>marker</c> element's <c>orient</c> attribute to a concrete rotation angle.
    /// </summary>
    /// <param name="raw">The raw <c>orient</c> attribute value, or <see langword="null"/> if absent.</param>
    /// <param name="autoAngleDegrees">The vertex's own computed tangent angle (see <see cref="ComputeVertexAngleDegrees"/>).</param>
    /// <param name="isStartVertex">Whether this vertex is the referencing shape's very first vertex.</param>
    /// <returns>
    ///     <paramref name="autoAngleDegrees"/> when <paramref name="raw"/> is exactly the keyword
    ///     <c>"auto"</c>; <paramref name="autoAngleDegrees"/> plus 180 degrees when
    ///     <paramref name="raw"/> is <c>"auto-start-reverse"</c> and <paramref name="isStartVertex"/>
    ///     is <see langword="true"/> (unchanged at any other vertex); otherwise an attempted fixed
    ///     degrees value, tolerantly falling back to <c>0</c> if <paramref name="raw"/> is absent,
    ///     blank, or not a valid/finite number. Per the SVG specification, an absent/blank
    ///     <c>orient</c> uses this fixed 0-degree default rather than following the vertex tangent -
    ///     only the explicit <c>auto</c>/<c>auto-start-reverse</c> keywords opt into tangent-following
    ///     behavior. The fallback-to-<c>0</c> parse failure path is a cosmetic-only concern parallel
    ///     to this class's existing tolerant handling of an invalid
    ///     <c>stroke-miterlimit</c>/<c>stroke-dasharray</c>, not a document-abort concern.
    /// </returns>
    private static float ParseMarkerOrient(string? raw, float autoAngleDegrees, bool isStartVertex)
    {
        // An absent/blank orient is NOT the same as an explicit "auto": the SVG spec's fixed
        // 0-degree default only falls through to the parse-failure branch below, which returns 0 -
        // it must never be routed into the tangent-following "auto" branch
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0f;
        }

        var trimmed = raw.Trim();
        if (string.Equals(trimmed, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return autoAngleDegrees;
        }

        if (string.Equals(trimmed, "auto-start-reverse", StringComparison.OrdinalIgnoreCase))
        {
            return isStartVertex ? autoAngleDegrees + 180f : autoAngleDegrees;
        }

        return float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var fixedAngle) && float.IsFinite(fixedAngle)
            ? fixedAngle
            : 0f;
    }
}
