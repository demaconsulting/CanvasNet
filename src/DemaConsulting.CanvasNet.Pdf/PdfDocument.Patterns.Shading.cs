// cspell:ignore cspace bbox
using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Geometry;

namespace DemaConsulting.CanvasNet.Pdf;

public sealed partial class PdfDocument
{
    /// <summary>
    ///     The minimum number of evenly spaced points across a shading's own function-input domain
    ///     that <see cref="BuildShadingGradient"/> samples to build a <see cref="Gradient"/>'s own
    ///     <see cref="GradientStop"/> list (used for short on-device gradients).
    /// </summary>
    private const int ShadingGradientMinSampleCount = 32;

    /// <summary>
    ///     The maximum number of sample points (see <see cref="ShadingGradientMinSampleCount"/>);
    ///     the actual count is one sample per device pixel of gradient length, clamped to
    ///     <c>[ShadingGradientMinSampleCount, ShadingGradientMaxSampleCount]</c>.
    /// </summary>
    private const int ShadingGradientMaxSampleCount = 256;

    /// <summary>
    ///     The largest extension (in multiples of the shading's own <c>[0, 1]</c> parameter range)
    ///     applied beyond either end for <c>/Extend</c>; bounds float precision of the widened ramp.
    /// </summary>
    private const double ShadingMaxExtension = 1e4;

    /// <summary>
    ///     The small parameter margin (relative to the <c>[0, 1]</c> range) placed beyond an
    ///     un-extended end so a hard transparent stop can sit outside the painted range.
    /// </summary>
    private const double ShadingEdgeMargin = 1e-3;

    /// <summary>
    ///     The fully resolved, immutable contents of a <c>/ShadingType 2</c>-<c>7</c> shading
    ///     dictionary (or mesh stream), shared by both consumers of a <c>/Shading</c> dictionary: the
    ///     <c>scn</c>/<c>SCN</c> + <c>/Pattern</c> + <c>/PatternType 2</c> path
    ///     (<see cref="BuildShadingPattern"/>, which wraps a shading dictionary in a pattern's own
    ///     <c>/Matrix</c>) and the <c>sh</c> operator (<see cref="OpPaintShading"/>, which paints
    ///     the shading directly against the current CTM, with no pattern wrapper at all).
    /// </summary>
    /// <param name="ShadingType">The shading's <c>/ShadingType</c> (<c>2</c> axial or <c>3</c> radial).</param>
    /// <param name="ColorSpace">The shading's resolved <c>/ColorSpace</c> (restricted to <c>DeviceGray</c>/<c>DeviceRGB</c>/<c>DeviceCMYK</c>).</param>
    /// <param name="Coords">The shading's <c>/Coords</c> (4 elements for axial, 6 for radial).</param>
    /// <param name="Domain">The function input domain to sample across (2 elements).</param>
    /// <param name="Evaluate">The resolved function (or function-array) evaluation delegate.</param>
    /// <param name="BBox">
    ///     The shading's own optional <c>/BBox</c> (PDF 32000-1 &#xA7;8.7.4.3, <c>[llx lly urx ury]</c>
    ///     in the shading's target coordinate space), or <see langword="null"/> when absent. Only
    ///     consulted by <see cref="OpPaintShading"/> (via <see cref="BuildShadingPaintRegion"/>) -
    ///     a shading *pattern*'s own fill region is instead whatever path it is painted onto, per
    ///     <see cref="PaintPatternFill"/>.
    /// </param>
    /// <param name="Mesh">
    ///     The decoded mesh for <c>/ShadingType 4</c>-<c>7</c> (see <see cref="MeshShading"/>), or
    ///     <see langword="null"/> for axial/radial shadings (for mesh shadings
    ///     <paramref name="Coords"/>/<paramref name="Domain"/>/<paramref name="Evaluate"/> are unused).
    /// </param>
    /// <param name="Extend">The shading's <c>/Extend</c> flags (start, end); <see langword="null"/> means <c>[false false]</c>.</param>
    private readonly record struct ShadingDescriptor(
        int ShadingType,
        PdfColorSpace ColorSpace,
        double[] Coords,
        double[] Domain,
        Func<double, double[]> Evaluate,
        double[]? BBox,
        MeshShading? Mesh = null,
        bool[]? Extend = null);

    /// <summary>
    ///     Resolves an already-resolved <c>/Shading</c> dictionary (or stream - some
    ///     <c>/ShadingType</c>s are defined as streams, which <see cref="Resolve"/> handles
    ///     uniformly alongside plain dictionaries) into a <see cref="ShadingDescriptor"/>, shared
    ///     identically by <see cref="BuildShadingPattern"/> and <see cref="OpPaintShading"/>.
    /// </summary>
    /// <param name="shading">The already-resolved <c>/Shading</c> dictionary (or stream).</param>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>/ColorSpace</c>, <c>/Function</c>, <c>/Domain</c>, <c>/Coords</c>, or
    ///     <c>/BBox</c> is missing (where required) or malformed, or propagated from
    ///     <see cref="ResolveFunctionOrFunctionArray"/> for its own documented malformed-input
    ///     cases.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when <c>/ShadingType</c> is not <c>2</c>-<c>7</c> (feature
    ///     <c>pdf-shading-type-{n}</c>), when the resolved <c>/ColorSpace</c>'s family is not
    ///     <c>DeviceGray</c>/<c>DeviceRGB</c>/<c>DeviceCMYK</c> (feature
    ///     <c>pdf-shading-colorspace-{family}</c>) - the device spaces and the CIE-based
    ///     <c>CalGray</c>/<c>CalRGB</c>/<c>Lab</c> spaces are accepted - or propagated from
    ///     <see cref="ResolveColorSpaceValue"/>/<see cref="ResolveFunctionOrFunctionArray"/> for
    ///     their own documented unsupported-feature cases.
    /// </exception>
    private ShadingDescriptor ResolveShadingDescriptor(PdfObject shading)
    {
        var shadingType = RequireIntEntry(shading, "ShadingType");
        if (shadingType is < 2 or > 7)
        {
            throw new UnsupportedImageFeatureException(
                $"pdf-shading-type-{shadingType}",
                $"/ShadingType {shadingType} is not supported; only 2 (axial), 3 (radial), 4-7 (mesh) are supported.");
        }

        var colorSpaceEntry = shading.Get("ColorSpace")
            ?? throw new InvalidDataException("/Shading is missing required /ColorSpace.");
        var colorSpace = ResolveColorSpaceValue(Resolve(colorSpaceEntry));
        if (colorSpace.Kind is not (
            PdfColorSpace.Family.DeviceGray or PdfColorSpace.Family.DeviceRGB or PdfColorSpace.Family.DeviceCMYK
                or PdfColorSpace.Family.CalGray or PdfColorSpace.Family.CalRGB or PdfColorSpace.Family.Lab))
        {
            throw new UnsupportedImageFeatureException(
                $"pdf-shading-colorspace-{colorSpace.Kind}",
                $"Shading /ColorSpace family '{colorSpace.Kind}' is not supported; only the device and " +
                "CIE-based (CalGray, CalRGB, Lab) color spaces are supported.");
        }

        // /BBox (PDF 32000-1 §8.7.4.3) is only meaningful to sh (see ShadingDescriptor's own
        // remarks); it is still validated here, unconditionally, so a malformed /BBox is rejected
        // identically regardless of which of the two consumers resolved this shading dictionary.
        var bbox = ResolveOptionalNumberArray(shading, "BBox");
        if (bbox is not null && bbox.Length != 4)
        {
            throw new InvalidDataException("/Shading /BBox must have exactly 4 elements.");
        }

        if (shadingType >= 4)
        {
            var mesh = ResolveMeshShading(shading, shadingType, colorSpace);
            return new ShadingDescriptor(shadingType, colorSpace, [], [0.0, 1.0], static _ => [], bbox, mesh);
        }

        var functionEntry = shading.Get("Function")
            ?? throw new InvalidDataException("/Shading is missing required /Function.");
        var (_, evaluate) = ResolveFunctionOrFunctionArray(functionEntry);

        // The shading's own /Domain (PDF 32000-1 §8.7.4.5.3, default [0, 1]) parametrizes
        // position along the /Coords geometry; it is distinct from - and must not be confused
        // with - the /Function's own /Domain (which only clips/validates the function's input).
        var domain = ResolveOptionalNumberArray(shading, "Domain") ?? [0.0, 1.0];
        if (domain.Length != 2)
        {
            throw new InvalidDataException("/Shading /Domain must have exactly 2 elements.");
        }

        var coords = RequireNumberArray(shading, "Coords");
        var expectedCoordCount = shadingType == 2 ? 4 : 6;
        if (coords.Length != expectedCoordCount)
        {
            throw new InvalidDataException(
                $"/Coords must have exactly {expectedCoordCount} elements for /ShadingType {shadingType}.");
        }

        var extend = ResolveExtend(shading);
        return new ShadingDescriptor(shadingType, colorSpace, coords, domain, evaluate, bbox, null, extend);
    }

    /// <summary>
    ///     Reads a shading's optional <c>/Extend</c> (PDF 32000-1 &#xA7;8.7.4.5.2), defaulting to
    ///     <c>[false false]</c>.
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown when <c>/Extend</c> is not an array of exactly 2 booleans.</exception>
    private bool[] ResolveExtend(PdfObject shading)
    {
        var entry = shading.Get("Extend");
        if (entry is null)
        {
            return [false, false];
        }

        var resolved = Resolve(entry);
        if (resolved.Kind != PdfKind.Array || resolved.Items.Count != 2)
        {
            throw new InvalidDataException("/Shading /Extend must be an array of exactly 2 booleans.");
        }

        var result = new bool[2];
        for (var i = 0; i < 2; i++)
        {
            var item = Resolve(resolved.Items[i]);
            if (item.Kind != PdfKind.Boolean)
            {
                throw new InvalidDataException("/Shading /Extend must be an array of exactly 2 booleans.");
            }

            result[i] = item.Boolean;
        }

        return result;
    }

    /// <summary>
    ///     Builds a <see cref="ResolvedPattern"/> of <see cref="ResolvedPattern.PatternKind.Shading"/>
    ///     from an already-resolved <c>/PatternType 2</c> pattern dictionary.
    /// </summary>
    /// <param name="patternDict">The already-resolved <c>/PatternType 2</c> pattern dictionary.</param>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>/Shading</c> is missing, or propagated from <see cref="ReadOptionalMatrix"/>/
    ///     <see cref="ResolveShadingDescriptor"/> for their own documented malformed-input cases.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Propagated from <see cref="ResolveShadingDescriptor"/> for its own documented
    ///     unsupported-feature cases.
    /// </exception>
    private ResolvedPattern BuildShadingPattern(PdfObject patternDict)
    {
        var matrix = ReadOptionalMatrix(patternDict);
        var shadingEntry = patternDict.Get("Shading")
            ?? throw new InvalidDataException("/PatternType 2 pattern is missing required /Shading.");
        var shading = Resolve(shadingEntry);
        var descriptor = ResolveShadingDescriptor(shading);

        return new ResolvedPattern
        {
            Kind = ResolvedPattern.PatternKind.Shading,
            Matrix = matrix,
            ShadingType = descriptor.ShadingType,
            ShadingColorSpace = descriptor.ColorSpace,
            Coords = descriptor.Coords,
            Domain = descriptor.Domain,
            Evaluate = descriptor.Evaluate,
            Extend = descriptor.Extend,
            Mesh = descriptor.Mesh,
        };
    }

    /// <summary>
    ///     Handles the <c>/name sh</c> operator (PDF 32000-1 &#xA7;8.7.4.2): paints a named
    ///     <c>/Resources/Shading</c> dictionary's gradient directly onto the destination surface,
    ///     restricted to the current clipping path (<see cref="GraphicsState.Clip"/>) - or, when
    ///     no clip is active, to the shading's own <c>/BBox</c> (see
    ///     <see cref="BuildShadingPaintRegion"/>) - without constructing/consuming "the current
    ///     path" (<see cref="_pathBuilder"/> is untouched) and without any <c>/Pattern</c>
    ///     color-space selection at all, unlike a shading *pattern*'s <c>scn</c>/<c>SCN</c> path.
    /// </summary>
    /// <param name="operands">The <c>sh</c> operator's accumulated operand stack.</param>
    /// <remarks>
    ///     Per PDF 32000-1 &#xA7;8.7.4.2, <c>sh</c>'s target coordinate space is the CTM in effect
    ///     when <c>sh</c> executes (<see cref="GraphicsState.CurrentTransform"/>) - unlike a
    ///     shading *pattern*'s own <c>/Matrix</c>, which is anchored against the page's default
    ///     (initial) coordinate system via <see cref="PatternToDeviceTransform"/>. A shading
    ///     dictionary has no <c>/Matrix</c> entry of its own in the specification's object model,
    ///     so the transient <see cref="ResolvedPattern"/> built below always uses
    ///     <see cref="Matrix3x2.Identity"/> - the current CTM supplies position directly.
    /// </remarks>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> is not exactly 1 name operand, or propagated
    ///     from <see cref="ResolveShadingDescriptor"/> for its own documented malformed-input
    ///     cases.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when <paramref name="operands"/>' name is not declared in the current page's
    ///     <c>/Resources/Shading</c> dictionary (feature <c>pdf-shading-not-declared</c>), or
    ///     propagated from <see cref="ResolveShadingDescriptor"/> for its own documented
    ///     unsupported-feature cases - the same exception the <c>scn</c>/<c>SCN</c> + <c>/Pattern</c>
    ///     path already throws for the identical underlying condition.
    /// </exception>
    private void OpPaintShading(IReadOnlyList<PdfObject> operands)
    {
        RequireOperandCount(operands, "sh", 1);
        if (operands[0].Kind != PdfKind.Name)
        {
            throw new InvalidDataException("Operator 'sh' requires a name operand.");
        }

        var name = operands[0].Text;
        var shadingDictionaryEntry = _resources?.Get("Shading");
        var resolvedDictionary = shadingDictionaryEntry is null ? null : Resolve(shadingDictionaryEntry);
        var entry = resolvedDictionary?.Get(name);
        if (entry is null)
        {
            throw new UnsupportedImageFeatureException(
                "pdf-shading-not-declared",
                $"Undefined shading '/{name}' (not declared in the current page's /Resources/Shading).");
        }

        var shading = Resolve(entry);
        var descriptor = ResolveShadingDescriptor(shading);

        var pattern = new ResolvedPattern
        {
            Kind = ResolvedPattern.PatternKind.Shading,
            Matrix = Matrix3x2.Identity,
            ShadingType = descriptor.ShadingType,
            ShadingColorSpace = descriptor.ColorSpace,
            Coords = descriptor.Coords,
            Domain = descriptor.Domain,
            Evaluate = descriptor.Evaluate,
            Extend = descriptor.Extend,
        };
        var region = BuildShadingPaintRegion(descriptor.BBox);
        if (descriptor.Mesh is { } mesh)
        {
            // /Background applies to shading patterns only, never to sh (PDF 32000-1 §8.7.4.3).
            PaintMesh(mesh, _gs.CurrentTransform, region, FillRule.NonZero, applyBackground: false);
            return;
        }

        var gradient = BuildShadingGradient(pattern, _gs.CurrentTransform, _surface.Width, _surface.Height);
        PathFiller.Fill(_surface, region, gradient, _gs.Clip, FillRule.NonZero);
    }

    /// <summary>
    ///     Builds the device-space region <see cref="OpPaintShading"/> paints a shading's own
    ///     gradient onto: <paramref name="bbox"/>'s 4 corners (transformed by the current CTM via
    ///     <see cref="Transform(double, double)"/>) when present, or a rectangle covering the
    ///     full destination surface otherwise.
    /// </summary>
    /// <param name="bbox">
    ///     The shading's own optional <c>/BBox</c> (<see cref="ShadingDescriptor.BBox"/>), in the
    ///     shading's target coordinate space (the same space <c>sh</c> paints against), or
    ///     <see langword="null"/> when the shading declares none.
    /// </param>
    /// <returns>A closed device-space path covering the region to paint.</returns>
    /// <remarks>
    ///     Either way, the returned path is always bounded by the destination surface regardless:
    ///     <see cref="PathFiller.Fill(Surface, Geometry.Path, Gradient, ClipMask?, FillRule, float)"/>'s
    ///     own existing <c>TryFlattenForFill</c> step intersects every fill's path bounds against
    ///     the surface's own bounds before painting a single pixel, so a full-surface fallback
    ///     region (used here precisely because PDF 32000-1 &#xA7;8.7.4.2 does not require a
    ///     <c>/BBox</c> to be present at all) can never paint beyond the surface, and a narrower
    ///     declared <c>/BBox</c> restricts the paint further still - this method never needs (and
    ///     does not implement) a second, independent bounding concept of its own.
    /// </remarks>
    private Geometry.Path BuildShadingPaintRegion(double[]? bbox)
    {
        var builder = new PathBuilder();
        if (bbox is null)
        {
            builder.MoveTo(new Vector2(0f, 0f));
            builder.LineTo(new Vector2(_surface.Width, 0f));
            builder.LineTo(new Vector2(_surface.Width, _surface.Height));
            builder.LineTo(new Vector2(0f, _surface.Height));
            builder.Close();
            return builder.Build();
        }

        var llx = bbox[0];
        var lly = bbox[1];
        var urx = bbox[2];
        var ury = bbox[3];

        builder.MoveTo(Transform(llx, lly));
        builder.LineTo(Transform(urx, lly));
        builder.LineTo(Transform(urx, ury));
        builder.LineTo(Transform(llx, ury));
        builder.Close();
        return builder.Build();
    }

    /// <summary>
    ///     Builds a <see cref="Gradient"/> (<see cref="LinearGradient"/> for
    ///     <c>/ShadingType 2</c>, <see cref="RadialGradient"/> for <c>/ShadingType 3</c>) from
    ///     <paramref name="pattern"/>'s resolved shading descriptor, sampling its resolved
    ///     function(s) at evenly spaced points across <see cref="ResolvedPattern.Domain"/> and
    ///     converting each sampled output through <see cref="ResolvedPattern.ShadingColorSpace"/>
    ///     via <see cref="ColorFromComponents"/>.
    /// </summary>
    /// <param name="pattern">The resolved shading pattern. Must have <see cref="ResolvedPattern.Kind"/> of <see cref="ResolvedPattern.PatternKind.Shading"/>.</param>
    /// <param name="patternToDevice">The pattern-space-to-device-space transform (see <see cref="PatternToDeviceTransform"/>), used as the gradient's own <see cref="Gradient.Transform"/>.</param>
    /// <param name="surfaceWidth">The destination surface width in device pixels (bounds how far an extended end must reach).</param>
    /// <param name="surfaceHeight">The destination surface height in device pixels.</param>
    /// <returns>The constructed gradient, ready to pass to <c>PathFiller.Fill(Surface, Path, Gradient, FillRule, float)</c>.</returns>
    /// <remarks>
    ///     <para>
    ///     <b>Sample count.</b> One sample per device pixel of gradient length (axial: the
    ///     transformed <c>/Coords</c> vector; radial: the larger of the sweep length and the
    ///     diameter sum, scaled by the larger matrix axis scale), rounded up and clamped to
    ///     <see cref="ShadingGradientMinSampleCount"/>..<see cref="ShadingGradientMaxSampleCount"/>.
    ///     </para>
    ///     <para>
    ///     <b>/Extend</b> (PDF 32000-1 &#xA7;8.7.4.5.2, default <c>[false false]</c>) is honored per
    ///     end without any new <see cref="GradientSpread"/>: the gradient's geometry is widened
    ///     beyond the shading's own <c>[0, 1]</c> parameter range (by enough to cover the surface,
    ///     capped by <see cref="ShadingMaxExtension"/>; for radial shadings also limited so the
    ///     interpolated radius never goes negative), and the widened part is filled with the end
    ///     color when that end is extended, or with a hard transparent stop when it is not. The
    ///     gradient always uses <see cref="GradientSpread.Pad"/> over the widened range.
    ///     </para>
    /// </remarks>
    private static Gradient BuildShadingGradient(
        ResolvedPattern pattern,
        Matrix3x2 patternToDevice,
        float surfaceWidth,
        float surfaceHeight)
    {
        var domain = pattern.Domain!;
        var evaluate = pattern.Evaluate!;
        var colorSpace = pattern.ShadingColorSpace!;
        var coords = pattern.Coords!;
        var extend0 = pattern.Extend is { Length: 2 } && pattern.Extend[0];
        var extend1 = pattern.Extend is { Length: 2 } && pattern.Extend[1];
        var isAxial = pattern.ShadingType == 2;

        // Geometry in pattern space.
        var p0 = new Vector2((float)coords[0], (float)coords[1]);
        var p1 = isAxial ? new Vector2((float)coords[2], (float)coords[3]) : new Vector2((float)coords[3], (float)coords[4]);
        var r0 = isAxial ? 0.0 : coords[2];
        var r1 = isAxial ? 0.0 : coords[5];
        var dr = r1 - r0;
        var sweep = (p1 - p0).Length();

        // Device-space length of one unit of t, and of the whole ramp (for the sample count).
        var scale = Math.Max(
            Vector2.TransformNormal(new Vector2(1f, 0f), patternToDevice).Length(),
            Vector2.TransformNormal(new Vector2(0f, 1f), patternToDevice).Length());
        double unitLength;
        double rampLength;
        if (isAxial)
        {
            unitLength = Vector2.TransformNormal(p1 - p0, patternToDevice).Length();
            rampLength = unitLength;
        }
        else
        {
            unitLength = (sweep + Math.Abs(dr)) * scale;
            rampLength = Math.Max(unitLength, (Math.Abs(r0) + Math.Abs(r1)) * scale);
        }

        var count = double.IsFinite(rampLength)
            ? (int)Math.Clamp(Math.Ceiling(rampLength), ShadingGradientMinSampleCount, ShadingGradientMaxSampleCount)
            : ShadingGradientMinSampleCount;

        // How far (in t units) an extended end must reach to cover the surface.
        var reach = MaxDeviceDistance(p0, p1, patternToDevice, surfaceWidth, surfaceHeight);
        var extension = unitLength > 1e-9 && double.IsFinite(reach)
            ? Math.Min((reach / unitLength) + 1.0, ShadingMaxExtension)
            : ShadingMaxExtension;

        // Radial: the radius must stay non-negative over the widened range.
        var startLimit = double.PositiveInfinity;
        var endLimit = double.PositiveInfinity;
        if (!isAxial)
        {
            if (dr > 0.0)
            {
                startLimit = Math.Max(r0, 0.0) / dr;
            }
            else if (dr < 0.0)
            {
                endLimit = Math.Max(r1, 0.0) / -dr;
            }
        }

        var before = extend0 ? Math.Min(extension, startLimit) : Math.Min(ShadingEdgeMargin, startLimit);
        var after = extend1 ? Math.Min(extension, endLimit) : Math.Min(ShadingEdgeMargin, endLimit);
        if (before < 1e-6)
        {
            before = 0.0;
        }

        if (after < 1e-6)
        {
            after = 0.0;
        }

        var tMin = -before;
        var tMax = 1.0 + after;
        var span = tMax - tMin;
        float Offset(double t) => (float)Math.Clamp((t - tMin) / span, 0.0, 1.0);

        var transparent = new Rgba32(0, 0, 0, 0);
        var stops = new List<GradientStop>(count + 4);
        for (var i = 0; i < count; i++)
        {
            var t = (double)i / (count - 1);
            var domainValue = domain[0] + (t * (domain[1] - domain[0]));
            var color = ColorFromComponents(colorSpace, evaluate(domainValue));
            var offset = Offset(t);

            if (i == 0 && before > 0.0)
            {
                var fill = extend0 ? color : transparent;
                stops.Add(new GradientStop(0f, fill));
                stops.Add(new GradientStop(offset, fill));
            }

            stops.Add(new GradientStop(offset, color));

            if (i == count - 1 && after > 0.0)
            {
                var fill = extend1 ? color : transparent;
                stops.Add(new GradientStop(offset, fill));
                stops.Add(new GradientStop(1f, fill));
            }
        }

        if (isAxial)
        {
            var dir = p1 - p0;
            return new LinearGradient(
                p0 + ((float)tMin * dir),
                p0 + ((float)tMax * dir),
                stops,
                GradientSpread.Pad,
                patternToDevice);
        }

        var dc = p1 - p0;
        return new RadialGradient(
            p0 + ((float)tMin * dc),
            (float)Math.Max(r0 + (tMin * dr), 0.0),
            p0 + ((float)tMax * dc),
            (float)Math.Max(r0 + (tMax * dr), 0.0),
            stops,
            GradientSpread.Pad,
            patternToDevice);
    }

    /// <summary>
    ///     Returns the largest device-space distance from either of the two pattern-space anchor
    ///     points to any corner of the surface.
    /// </summary>
    private static double MaxDeviceDistance(
        Vector2 p0,
        Vector2 p1,
        Matrix3x2 patternToDevice,
        float surfaceWidth,
        float surfaceHeight)
    {
        var a = Vector2.Transform(p0, patternToDevice);
        var b = Vector2.Transform(p1, patternToDevice);
        Span<Vector2> corners =
        [
            new(0f, 0f),
            new(surfaceWidth, 0f),
            new(0f, surfaceHeight),
            new(surfaceWidth, surfaceHeight),
        ];
        double max = 0.0;
        foreach (var corner in corners)
        {
            max = Math.Max(max, Math.Max(Vector2.Distance(a, corner), Vector2.Distance(b, corner)));
        }

        return max;
    }

    /// <summary>
    ///     Resolves a <c>/Function</c> entry in either of its two shading-pattern-legal shapes: a
    ///     single multi-output function, or an array of 1-output functions (one function per
    ///     output component) - into a common <c>(Domain, Evaluate)</c> shape
    ///     <see cref="BuildShadingGradient"/> samples uniformly, regardless of which shape was
    ///     declared.
    /// </summary>
    /// <param name="functionEntry">The (possibly indirect-reference) <c>/Function</c> entry to resolve.</param>
    /// <returns>
    ///     A nominal <c>[0, 1]</c> domain for the array form (or the single function's own
    ///     <c>/Domain</c>) - callers sample across the shading's own <c>/Domain</c> instead and
    ///     ignore it - and a delegate evaluating the
    ///     function(s) at a single input value, returning one output value per color component.
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the array form is empty, when any array element is not a 1-output
    ///     function, or when a single function's own <c>/Domain</c> is present but does not have
    ///     exactly 2 elements.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Propagated from <see cref="ResolveFunctionGeneric"/> for an unsupported function type.
    /// </exception>
    private (double[] Domain, Func<double, double[]> Evaluate) ResolveFunctionOrFunctionArray(PdfObject functionEntry)
    {
        var resolved = Resolve(functionEntry);
        if (resolved.Kind == PdfKind.Array)
        {
            if (resolved.Items.Count == 0)
            {
                throw new InvalidDataException("/Function array must not be empty.");
            }

            var subFunctions = new IPdfFunction[resolved.Items.Count];
            for (var i = 0; i < subFunctions.Length; i++)
            {
                var sub = ResolveFunctionGeneric(resolved.Items[i]);
                if (sub.OutputCount != 1)
                {
                    throw new InvalidDataException(
                        "Each /Function array element must be a 1-output function.");
                }

                // Each sub-function clamps its own input to its own /Domain when evaluated.
                subFunctions[i] = sub;
            }

            double[] EvaluateArray(double input)
            {
                var outputs = new double[subFunctions.Length];
                for (var i = 0; i < subFunctions.Length; i++)
                {
                    outputs[i] = subFunctions[i].Evaluate(input)[0];
                }

                return outputs;
            }

            return ([0.0, 1.0], EvaluateArray);
        }

        var function = ResolveFunctionGeneric(functionEntry);
        var domain = ResolveOptionalNumberArray(resolved, "Domain") ?? [0.0, 1.0];
        if (domain.Length != 2)
        {
            throw new InvalidDataException("/Domain must have exactly 2 elements for a 1-input function.");
        }

        return (domain, function.Evaluate);
    }
}
