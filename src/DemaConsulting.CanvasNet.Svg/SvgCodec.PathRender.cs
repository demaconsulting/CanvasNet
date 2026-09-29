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

namespace DemaConsulting.CanvasNet.Svg;

public static partial class SvgCodec
{
    // ================================================================================================
    // Path transform mapping and shape rendering
    // ================================================================================================

    /// <summary>
    ///     Re-issues every subpath/command of <paramref name="source"/> through a fresh
    ///     <see cref="PathBuilder"/>, mapping every point through <paramref name="transform"/>.
    /// </summary>
    /// <param name="source">The local, untransformed-space path to remap.</param>
    /// <param name="transform">The transform mapping local-space points to pixel-space points.</param>
    /// <returns>A new path with every point transformed, and the same command structure.</returns>
    /// <remarks>
    ///     Mirrors <c>TrueTypeFontRealFontIntegrationTests</c>' "re-issue through a fresh
    ///     <see cref="PathBuilder"/> with a point-mapping lambda" pattern: this is the one place
    ///     every shape/glyph outline this codec builds gets baked into final pixel-space
    ///     coordinates, since <see cref="Drawing.PathFiller"/>/<see cref="Drawing.PathStroker"/>
    ///     have no transform parameter of their own.
    /// </remarks>
    private static Path TransformPath(Path source, Matrix3x2 transform)
    {
        var builder = new PathBuilder();
        AppendTransformedPathInto(builder, source, transform);
        return builder.Build();
    }

    /// <summary>
    ///     Appends every subpath of <paramref name="source"/> into <paramref name="builder"/>'s
    ///     already-in-progress build, transformed through <paramref name="transform"/> - the
    ///     shared core of <see cref="TransformPath"/> (a fresh builder) and glyph-run assembly (an
    ///     existing builder accumulating multiple glyphs' outlines into one combined path).
    /// </summary>
    /// <param name="builder">The destination builder.</param>
    /// <param name="source">The local, untransformed-space path to append.</param>
    /// <param name="transform">The transform mapping local-space points to the destination space.</param>
    private static void AppendTransformedPathInto(PathBuilder builder, Path source, Matrix3x2 transform)
    {
        foreach (var subpath in source.Subpaths)
        {
            builder.MoveTo(Vector2.Transform(subpath.Start, transform));
            AppendTransformedCommands(builder, subpath.Commands, transform);
        }
    }

    /// <summary>Appends one subpath's already-open commands to <paramref name="builder"/>, transformed.</summary>
    /// <param name="builder">The destination builder, already positioned via a preceding <see cref="PathBuilder.MoveTo"/>.</param>
    /// <param name="commands">The source subpath's commands, in local/untransformed space.</param>
    /// <param name="transform">The transform mapping local-space points to pixel-space points.</param>
    /// <exception cref="NotSupportedException">
    ///     Thrown for a <see cref="PathCommandType.ArcTo"/> command - this codec never issues one
    ///     (arcs are always pre-converted to cubic Beziers before reaching this method), so
    ///     encountering one indicates an internal defect rather than a data-driven condition.
    /// </exception>
    private static void AppendTransformedCommands(PathBuilder builder, IReadOnlyList<PathCommand> commands, Matrix3x2 transform)
    {
        foreach (var command in commands)
        {
            switch (command.Type)
            {
                case PathCommandType.LineTo:
                    builder.LineTo(Vector2.Transform(command.EndPoint, transform));
                    break;
                case PathCommandType.QuadraticBezierTo:
                    builder.QuadraticBezierTo(
                        Vector2.Transform(command.Control1, transform),
                        Vector2.Transform(command.EndPoint, transform));
                    break;
                case PathCommandType.CubicBezierTo:
                    builder.CubicBezierTo(
                        Vector2.Transform(command.Control1, transform),
                        Vector2.Transform(command.Control2, transform),
                        Vector2.Transform(command.EndPoint, transform));
                    break;
                case PathCommandType.Close:
                    builder.Close();
                    break;
                default:
                    throw new NotSupportedException(
                        $"Unsupported path command type '{command.Type}' encountered while transforming a path.");
            }
        }
    }

    /// <summary>
    ///     Renders one shape's local-space outline: transforms it into pixel space once, then
    ///     fills and/or strokes it per <paramref name="state"/>.
    /// </summary>
    /// <param name="localPath">The shape's outline, in local (untransformed) user-space coordinates.</param>
    /// <param name="state">The cascaded render state supplying fill/stroke paint and style.</param>
    /// <param name="transform">The accumulated transform mapping local space to pixel space.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="useDepth">The current <c>use</c>-reference nesting depth, forwarded to a <c>pattern</c> fill/stroke's own tile content walk (see <see cref="RenderFill"/>/<see cref="RenderStroke"/>).</param>
    /// <param name="elementDepth">The current recursion depth, forwarded (incremented by one further) to a <c>pattern</c> fill/stroke's own tile content walk - the sole guard against a pattern-reference cycle.</param>
    /// <param name="markerDepth">The current <c>marker</c>-reference nesting depth, forwarded unchanged.</param>
    /// <param name="totalElements">The running total-rendered-elements count, forwarded to a <c>pattern</c> fill/stroke's own tile content walk.</param>
    /// <param name="workBudget">The shared geometry-parsing work budget, forwarded to a <c>pattern</c> fill/stroke's own tile content walk.</param>
    /// <param name="filterWorkBudget">The shared cumulative filter-evaluation work budget, charged by a <c>pattern</c> fill/stroke's own offscreen-buffer allocations (see <see cref="RenderPatternFill"/>).</param>
    /// <param name="boundsPrePassBudget">The shared cumulative bounds-pre-pass work budget, forwarded to a <c>pattern</c> fill/stroke's own tile content walk.</param>
    private static void RenderShape(
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
        if (localPath.Subpaths.Count == 0)
        {
            return;
        }

        var pixelPath = TransformPath(localPath, transform);

        // Post-transform coordinate-magnitude re-check: a source-literal coordinate is already
        // bounded by MaxCoordinateMagnitude at parse time (see ParseCoordinate/TryReadNumber), but
        // that check runs before any transform is applied. A transform argument itself only needs
        // to stay at or under MaxCoordinateMagnitude to pass its own parse-time check (see
        // TryReadNumber's remarks on the strict '>' boundary), so an in-bound local coordinate
        // composed with an in-bound-but-large transform (e.g. scale(1000000)) can still produce a
        // final pixel-space magnitude the flattening/stroking pipeline was never meant to see.
        // This single check point, immediately after TransformPath, uniformly covers every shape
        // built by this class - plain shapes, text/glyph runs, and arc-converted rounded-rect/
        // ellipse geometry alike - because all of them are baked into pixel space through this
        // same TransformPath call. On failure, tolerantly skip rendering this shape entirely,
        // mirroring BuildRectPath/BuildEllipsePath's existing tolerant-skip convention for an
        // overflowing arc conversion, rather than the parse-time check's hard reject: unlike a
        // malformed literal (a document-authoring mistake), a huge final magnitude can arise from
        // perfectly valid, independently-in-bound inputs composing multiplicatively, so aborting
        // only this shape - not the whole document - is the more tolerant, consistent choice.
        if (!IsWithinCoordinateMagnitudeBudget(pixelPath))
        {
            return;
        }

        RenderFill(
            localPath, pixelPath, state, transform, context, useDepth, elementDepth, markerDepth, ref totalElements,
            workBudget, filterWorkBudget, boundsPrePassBudget);
        RenderStroke(
            localPath, pixelPath, state, transform, context, useDepth, elementDepth, markerDepth, ref totalElements,
            workBudget, filterWorkBudget, boundsPrePassBudget);
    }

    /// <summary>
    ///     Determines whether every point of <paramref name="path"/> - each subpath's start point,
    ///     and every command's <c>EndPoint</c>/<c>Control1</c>/<c>Control2</c> where applicable -
    ///     is finite and within <see cref="MaxCoordinateMagnitude"/>.
    /// </summary>
    /// <param name="path">The already pixel-space-transformed path to check.</param>
    /// <returns>
    ///     <see langword="true"/> if every point in <paramref name="path"/> is finite and within
    ///     <see cref="MaxCoordinateMagnitude"/>; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///     <paramref name="path"/> is expected to have already passed through
    ///     <see cref="TransformPath"/>, so every <see cref="PathCommandType.ArcTo"/> command has
    ///     already been converted to cubic Bezier segments by
    ///     <see cref="AppendTransformedCommands"/> - this method therefore never needs to handle
    ///     <see cref="PathCommandType.ArcTo"/> itself.
    /// </remarks>
    private static bool IsWithinCoordinateMagnitudeBudget(Path path)
    {
        foreach (var subpath in path.Subpaths)
        {
            if (!IsFiniteAndWithinCoordinateMagnitude(subpath.Start))
            {
                return false;
            }

            foreach (var command in subpath.Commands)
            {
                switch (command.Type)
                {
                    case PathCommandType.LineTo:
                        if (!IsFiniteAndWithinCoordinateMagnitude(command.EndPoint))
                        {
                            return false;
                        }

                        break;

                    case PathCommandType.QuadraticBezierTo:
                        if (!IsFiniteAndWithinCoordinateMagnitude(command.Control1)
                            || !IsFiniteAndWithinCoordinateMagnitude(command.EndPoint))
                        {
                            return false;
                        }

                        break;

                    case PathCommandType.CubicBezierTo:
                        if (!IsFiniteAndWithinCoordinateMagnitude(command.Control1)
                            || !IsFiniteAndWithinCoordinateMagnitude(command.Control2)
                            || !IsFiniteAndWithinCoordinateMagnitude(command.EndPoint))
                        {
                            return false;
                        }

                        break;
                }
            }
        }

        return true;
    }

    /// <summary>
    ///     Determines whether a single point is finite and within <see cref="MaxCoordinateMagnitude"/>
    ///     in both components.
    /// </summary>
    /// <param name="point">The point to check.</param>
    private static bool IsFiniteAndWithinCoordinateMagnitude(Vector2 point) =>
        float.IsFinite(point.X) && float.IsFinite(point.Y)
        && MathF.Abs(point.X) <= MaxCoordinateMagnitude && MathF.Abs(point.Y) <= MaxCoordinateMagnitude;


    /// <summary>Fills <paramref name="pixelPath"/> per <paramref name="state"/>'s <c>fill</c> paint.</summary>
    /// <param name="localPath">The shape's local-space outline, used as a gradient's or pattern's object-bounding-box basis.</param>
    /// <param name="pixelPath">The shape's already pixel-space-transformed outline.</param>
    /// <param name="state">The cascaded render state.</param>
    /// <param name="transform">The accumulated transform, used to resolve a gradient's or pattern's own transform.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="useDepth">The current <c>use</c>-reference nesting depth, forwarded to a <c>pattern</c> fill's own tile content walk (see <see cref="RenderPatternFill"/>).</param>
    /// <param name="elementDepth">The current recursion depth, forwarded (incremented by one further) to a <c>pattern</c> fill's own tile content walk.</param>
    /// <param name="markerDepth">The current <c>marker</c>-reference nesting depth, forwarded unchanged.</param>
    /// <param name="totalElements">The running total-rendered-elements count, forwarded to a <c>pattern</c> fill's own tile content walk.</param>
    /// <param name="workBudget">The shared geometry-parsing work budget, forwarded to a <c>pattern</c> fill's own tile content walk.</param>
    /// <param name="filterWorkBudget">The shared cumulative filter-evaluation work budget, charged by a <c>pattern</c> fill's own offscreen-buffer allocations.</param>
    /// <param name="boundsPrePassBudget">The shared cumulative bounds-pre-pass work budget, forwarded to a <c>pattern</c> fill's own tile content walk.</param>
    /// <remarks>
    ///     When <paramref name="state"/>'s <c>fill</c> resolves to a <c>url(#id)</c> reference to a
    ///     <c>pattern</c> element (see <see cref="ResolvePatternElement"/>), rendering is dispatched
    ///     entirely to <see cref="RenderPatternFill"/> instead - a purely additive branch: every
    ///     other <c>fill</c> value (a solid color, a gradient reference, <c>none</c>, or a
    ///     dangling/wrong-type <c>url(#id)</c> reference) falls through this check unaffected, and
    ///     reaches the exact same <see cref="ResolvePaint"/>/<see cref="FillWithPaint"/> call this
    ///     method has always made.
    /// </remarks>
    private static void RenderFill(
        Path localPath,
        Path pixelPath,
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
        var patternElement = ResolvePatternElement(state.Fill, context);
        if (patternElement != null)
        {
            RenderPatternFill(
                patternElement, localPath, pixelPath, state.FillRule, state.FillOpacity * state.Opacity, transform,
                state, context, useDepth, elementDepth, markerDepth, ref totalElements, workBudget, filterWorkBudget,
                boundsPrePassBudget);
            return;
        }

        var paint = ResolvePaint(state.Fill, state.FillOpacity * state.Opacity, localPath, transform, context);
        FillWithPaint(context.Surface, pixelPath, paint, state.FillRule);
    }

    /// <summary>
    ///     Strokes <paramref name="pixelPath"/> per <paramref name="state"/>'s <c>stroke</c> paint
    ///     and stroke-style attributes, converting the stroke to fillable outline geometry first
    ///     via <see cref="Drawing.PathStroker"/>.
    /// </summary>
    /// <param name="localPath">The shape's local-space outline, used as a gradient's or pattern's object-bounding-box basis.</param>
    /// <param name="pixelPath">The shape's already pixel-space-transformed outline.</param>
    /// <param name="state">The cascaded render state.</param>
    /// <param name="transform">The accumulated transform, used to estimate the pixel-space stroke-width scale, and to resolve a pattern's own transform.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="useDepth">The current <c>use</c>-reference nesting depth, forwarded to a <c>pattern</c> stroke's own tile content walk (see <see cref="RenderPatternFill"/>).</param>
    /// <param name="elementDepth">The current recursion depth, forwarded (incremented by one further) to a <c>pattern</c> stroke's own tile content walk.</param>
    /// <param name="markerDepth">The current <c>marker</c>-reference nesting depth, forwarded unchanged.</param>
    /// <param name="totalElements">The running total-rendered-elements count, forwarded to a <c>pattern</c> stroke's own tile content walk.</param>
    /// <param name="workBudget">The shared geometry-parsing work budget, forwarded to a <c>pattern</c> stroke's own tile content walk.</param>
    /// <param name="filterWorkBudget">The shared cumulative filter-evaluation work budget, charged by a <c>pattern</c> stroke's own offscreen-buffer allocations.</param>
    /// <param name="boundsPrePassBudget">The shared cumulative bounds-pre-pass work budget, forwarded to a <c>pattern</c> stroke's own tile content walk.</param>
    /// <remarks>
    ///     <para>
    ///     When <paramref name="state"/>'s <c>stroke</c> resolves to a <c>url(#id)</c> reference to
    ///     a <c>pattern</c> element (see <see cref="ResolvePatternElement"/>), the stroke outline
    ///     is still built and magnitude-checked exactly as usual below, but rendering is then
    ///     dispatched entirely to <see cref="RenderPatternFill"/> instead of
    ///     <see cref="ResolvePaint"/>/<see cref="FillWithPaint"/> - every other <c>stroke</c> value
    ///     (a solid color, a gradient reference, <c>none</c>, or a dangling/wrong-type
    ///     <c>url(#id)</c> reference) is completely unaffected by this additional check.
    ///     </para>
    ///     A no-op stroke (<c>stroke="none"</c>, a dangling gradient reference, or a non-positive
    ///     effective stroke width) never constructs a <see cref="Drawing.StrokeStyle"/> at all,
    ///     avoiding its constructor's own <see cref="ArgumentOutOfRangeException"/> for a
    ///     zero width. The same tolerant skip also covers a non-finite effective stroke width: the
    ///     locally-finite <c>stroke-width</c> is scaled by <see cref="EstimateUniformScale"/>,
    ///     whose composed nested <c>transform="scale(...)"</c> determinant can overflow to a
    ///     non-finite value (<c>Infinity</c>, or <c>NaN</c> if the overflow arithmetic itself
    ///     produces an indeterminate result) even though every individual transform literal was
    ///     finite - such an overflowed width would otherwise pass the <c>&lt;= 0f</c> check (since
    ///     neither <c>Infinity</c> nor <c>NaN</c> compares <c>&lt;= 0f</c>) and reach
    ///     <see cref="Drawing.StrokeStyle"/>'s constructor, which throws an uncaught
    ///     <see cref="ArgumentOutOfRangeException"/> for it.
    ///     <para>
    ///     A finite-but-extreme effective stroke width is a distinct, independent gap from the
    ///     overflow-to-infinity case above: <c>stroke-width</c> is validated finite/positive at
    ///     parse time (pre-transform, see <see cref="ParseGeometryCoordinate"/>/<see cref="ParseCoordinate"/>),
    ///     but is then scaled by <paramref name="transform"/>'s estimated scale with no bound of
    ///     its own - a compliant, in-bound <c>stroke-width</c> (up to <see cref="MaxCoordinateMagnitude"/>)
    ///     composed with a large-but-finite transform scale can still produce a finite effective
    ///     width many orders of magnitude beyond what <see cref="Drawing.PathStroker"/>'s
    ///     offset-curve generation was ever meant to see, without ever tripping the
    ///     <c>!float.IsFinite(strokeWidth)</c> check. Reusing <see cref="MaxCoordinateMagnitude"/>
    ///     to bound the post-transform effective width closes this gap the same tolerant-skip way
    ///     the other conditions in this guard already do.
    ///     </para>
    ///     <para>
    ///     A third, independent gap exists even when both <paramref name="pixelPath"/>'s
    ///     coordinates and the effective <c>strokeWidth</c> are individually in-bound:
    ///     <c>stroke-miterlimit</c> (see <see cref="ParseValidMiterLimit(string?)"/>) is only bounded below
    ///     (finite, <c>&gt;= 1</c>), never above, so an in-bound-but-large <c>strokeWidth</c>
    ///     combined with an in-bound-but-extreme <c>miterlimit</c> and a near-straight ("spike")
    ///     vertex can drive <see cref="Drawing.StrokeOutliner"/>'s miter-join synthesis
    ///     (<c>TryCreateMiter</c>) to a point many orders of magnitude beyond
    ///     <see cref="MaxCoordinateMagnitude"/> - <c>TryCreateMiter</c> only rejects a miter point
    ///     whose ratio to half the stroke width exceeds the miterlimit, a check that says nothing
    ///     about the point's own absolute magnitude. This is re-checked, and tolerantly skipped the
    ///     same way, immediately after <see cref="Drawing.PathStroker.Stroke"/> runs below.
    ///     </para>
    /// </remarks>
    private static void RenderStroke(
        Path localPath,
        Path pixelPath,
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
        var scale = EstimateUniformScale(transform);
        var strokeWidth = EstimateEffectiveStrokeWidth(state.StrokeWidth, scale);
        if (!float.IsFinite(strokeWidth) || strokeWidth <= 0f || strokeWidth > MaxCoordinateMagnitude)
        {
            return;
        }

        // A pattern-referencing stroke must be checked before ResolvePaint's own dangling-
        // reference-tolerant null-return: unlike a solid color/gradient/none stroke - none of
        // which need the stroke outline built at all when there is no paint to fill it with - a
        // pattern paint is resolved from state.Stroke directly (see ResolvePatternElement), so
        // this check must run first and, when it matches, skip ResolvePaint's own null-paint
        // early-return entirely and instead build the stroke outline unconditionally below
        var patternElement = ResolvePatternElement(state.Stroke, context);
        object? paint = null;
        if (patternElement == null)
        {
            paint = ResolvePaint(state.Stroke, state.StrokeOpacity * state.Opacity, localPath, transform, context);
            if (paint == null)
            {
                return;
            }
        }

        // The scaled dasharray/dashoffset - not just their raw, already-finite parsed values -
        // must be validated: 'scale' (like strokeWidth's own scale above) can itself be extreme
        // from a composed transform, overflowing an individually-finite dash entry/offset to a
        // non-finite value that would otherwise reach StrokeStyle's constructor and throw an
        // uncaught ArgumentOutOfRangeException/ArgumentException. Tolerantly fall back to "no
        // dashing" (a solid stroke) rather than skipping the whole stroke - the stroke geometry
        // itself is still perfectly valid, only its dash pattern overflowed - mirroring
        // ParseDashArray's own existing tolerant "malformed dash array -> no dashing" convention.
        var scaledDashArray = ScaleDashArray(state.StrokeDashArray, scale);
        var scaledDashOffset = state.StrokeDashOffset * scale;
        if (!float.IsFinite(scaledDashOffset) || (scaledDashArray?.Any(v => !float.IsFinite(v)) ?? false))
        {
            scaledDashArray = null;
            scaledDashOffset = 0f;
        }

        var style = new StrokeStyle(
            strokeWidth,
            state.StrokeLineCap,
            state.StrokeLineJoin,
            state.StrokeMiterLimit,
            scaledDashArray,
            scaledDashOffset);

        var outline = PathStroker.Stroke(pixelPath, style);

        // Post-stroke coordinate-magnitude re-check: pixelPath and strokeWidth each already
        // passed their own magnitude checks above (IsWithinCoordinateMagnitudeBudget on
        // pixelPath in RenderShape, and the strokeWidth <= MaxCoordinateMagnitude check above),
        // but Drawing.StrokeOutliner's miter-join synthesis (TryCreateMiter) only rejects a
        // miter point whose ratio to half the stroke width exceeds StrokeStyle.MiterLimit - a
        // ratio check that says nothing about the miter point's own absolute magnitude.
        // stroke-miterlimit is parsed (see ParseValidMiterLimit) with no upper bound beyond
        // "finite and >= 1", so an in-bound-but-large stroke-width composed with an in-bound-but-
        // extreme miterlimit and a near-straight (acute-spike) vertex can still synthesize a
        // miter point many orders of magnitude beyond MaxCoordinateMagnitude, even though every
        // individual literal involved - each pixelPath coordinate, strokeWidth, and miterlimit -
        // passed its own check. Reusing IsWithinCoordinateMagnitudeBudget here, on the actual
        // synthesized outline, closes this gap directly at its source instead of guessing a
        // conservative-but-arbitrary miterlimit ceiling: it composes with every path command type
        // IsWithinCoordinateMagnitudeBudget already understands, since PathStroker.Stroke only
        // ever emits LineTo commands into its returned outline. On failure, tolerantly skip
        // rendering this stroke entirely, mirroring RenderShape's own tolerant-skip convention.
        if (!IsWithinCoordinateMagnitudeBudget(outline))
        {
            return;
        }

        if (patternElement != null)
        {
            RenderPatternFill(
                patternElement, localPath, outline, FillRule.NonZero, state.StrokeOpacity * state.Opacity, transform,
                state, context, useDepth, elementDepth, markerDepth, ref totalElements, workBudget, filterWorkBudget,
                boundsPrePassBudget);
            return;
        }

        FillWithPaint(context.Surface, outline, paint, FillRule.NonZero);
    }

    /// <summary>Fills <paramref name="path"/> with a resolved paint value, tolerating a <see langword="null"/> (no-op) paint.</summary>
    /// <param name="surface">The surface to fill into.</param>
    /// <param name="path">The pixel-space path to fill.</param>
    /// <param name="paint">The resolved paint: a boxed <see cref="Rgba32"/>, a <see cref="Gradient"/>, or <see langword="null"/>.</param>
    /// <param name="fillRule">The fill rule to apply.</param>
    private static void FillWithPaint(Surface surface, Path path, object? paint, FillRule fillRule)
    {
        switch (paint)
        {
            case Rgba32 color:
                PathFiller.Fill(surface, path, color, fillRule);
                break;
            case Gradient gradient:
                PathFiller.Fill(surface, path, gradient, fillRule);
                break;
        }
    }

    /// <summary>
    ///     Estimates a single isotropic scale factor for <paramref name="transform"/>, used to map
    ///     a local-space stroke-width/dash-array into pixel space.
    /// </summary>
    /// <param name="transform">The transform to estimate.</param>
    /// <returns>The square root of the absolute value of the transform's linear determinant.</returns>
    /// <remarks>
    ///     A deliberate simplification: under a non-uniform-scale or skewed transform, a
    ///     mathematically correct stroke outline would need to be generated in local space (where
    ///     the stroke width is defined) and then transformed, rather than transformed first and
    ///     stroked with a single scalar width second. This codec always does the latter, matching
    ///     this class's "bake every transform into final pixel-space points" design - visually
    ///     reasonable for the common case of uniform-scale-only transforms, but not exact for
    ///     skewed or non-uniformly scaled ones.
    /// </remarks>
    private static float EstimateUniformScale(Matrix3x2 transform) =>
        MathF.Sqrt(MathF.Abs((transform.M11 * transform.M22) - (transform.M12 * transform.M21)));

    /// <summary>
    ///     Scales a local-space <c>stroke-width</c> into its effective pixel-space width, shared by
    ///     <see cref="RenderStroke"/> (to size the actual stroke outline) and
    ///     <see cref="RenderMarkers"/> (to size a <c>markerUnits="strokeWidth"</c> marker, the SVG
    ///     default) - extracted so both call sites compute this one expression identically rather
    ///     than duplicating it, per this codebase's no-copy-paste coding standard.
    /// </summary>
    /// <param name="strokeWidth">The local-space <c>stroke-width</c> value.</param>
    /// <param name="uniformScale">
    ///     The isotropic scale factor estimated by <see cref="EstimateUniformScale"/> for the
    ///     accumulated transform in effect.
    /// </param>
    /// <returns>
    ///     The effective pixel-space stroke width. Can be non-finite, non-positive, or extremely
    ///     large if <paramref name="uniformScale"/> is itself extreme - each caller is responsible
    ///     for its own tolerant-skip validation of the result, matching <see cref="RenderStroke"/>'s
    ///     existing guard.
    /// </returns>
    private static float EstimateEffectiveStrokeWidth(float strokeWidth, float uniformScale) =>
        strokeWidth * uniformScale;

    /// <summary>Scales every entry of a dash array by <paramref name="scale"/>.</summary>
    /// <param name="dashArray">The local-space dash array, or <see langword="null"/> for a solid stroke.</param>
    /// <param name="scale">The local-to-pixel-space scale factor.</param>
    /// <returns>The scaled dash array, or <see langword="null"/> if <paramref name="dashArray"/> is <see langword="null"/>.</returns>
    private static float[]? ScaleDashArray(IReadOnlyList<float>? dashArray, float scale) =>
        dashArray?.Select(value => value * scale).ToArray();
}
