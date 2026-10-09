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
    ///     The number of evenly spaced points across a shading's own function-input domain that
    ///     <see cref="BuildShadingGradient"/> samples to build a <see cref="Gradient"/>'s own
    ///     <see cref="GradientStop"/> list.
    /// </summary>
    /// <remarks>
    ///     32 balances visual smoothness against allocation/compute cost per fill - no caller of
    ///     this evaluator needs more precision than this, matching <see cref="SampledFunction"/>'s
    ///     own "no caller needs more precision than this" posture toward its own evaluation
    ///     granularity.
    /// </remarks>
    private const int ShadingGradientSampleCount = 32;

    /// <summary>
    ///     The fully resolved, immutable contents of a <c>/ShadingType 2</c>/<c>3</c> shading
    ///     dictionary, shared by both consumers of a <c>/Shading</c> dictionary: the
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
    private readonly record struct ShadingDescriptor(
        int ShadingType,
        PdfColorSpace ColorSpace,
        double[] Coords,
        double[] Domain,
        Func<double, double[]> Evaluate,
        double[]? BBox);

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
    ///     Thrown when <c>/ShadingType</c> is not <c>2</c> or <c>3</c> (feature
    ///     <c>pdf-shading-type-{n}</c>), when the resolved <c>/ColorSpace</c>'s family is not
    ///     <c>DeviceGray</c>/<c>DeviceRGB</c>/<c>DeviceCMYK</c> (feature
    ///     <c>pdf-shading-colorspace-{family}</c>), or propagated from
    ///     <see cref="ResolveColorSpaceValue"/>/<see cref="ResolveFunctionOrFunctionArray"/> for
    ///     their own documented unsupported-feature cases.
    /// </exception>
    private ShadingDescriptor ResolveShadingDescriptor(PdfObject shading)
    {
        var shadingType = RequireIntEntry(shading, "ShadingType");
        if (shadingType is not (2 or 3))
        {
            throw new UnsupportedImageFeatureException(
                $"pdf-shading-type-{shadingType}",
                $"/ShadingType {shadingType} is not supported; only 2 (axial) and 3 (radial) are supported.");
        }

        var colorSpaceEntry = shading.Get("ColorSpace")
            ?? throw new InvalidDataException("/Shading is missing required /ColorSpace.");
        var colorSpace = ResolveColorSpaceValue(Resolve(colorSpaceEntry));
        if (colorSpace.Kind is not (
            PdfColorSpace.Family.DeviceGray or PdfColorSpace.Family.DeviceRGB or PdfColorSpace.Family.DeviceCMYK))
        {
            throw new UnsupportedImageFeatureException(
                $"pdf-shading-colorspace-{colorSpace.Kind}",
                $"Shading /ColorSpace family '{colorSpace.Kind}' is not supported; only DeviceGray, " +
                "DeviceRGB, and DeviceCMYK are supported.");
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

        // /BBox (PDF 32000-1 §8.7.4.3) is only meaningful to sh (see ShadingDescriptor's own
        // remarks); it is still validated here, unconditionally, so a malformed /BBox is rejected
        // identically regardless of which of the two consumers resolved this shading dictionary.
        var bbox = ResolveOptionalNumberArray(shading, "BBox");
        if (bbox is not null && bbox.Length != 4)
        {
            throw new InvalidDataException("/Shading /BBox must have exactly 4 elements.");
        }

        return new ShadingDescriptor(shadingType, colorSpace, coords, domain, evaluate, bbox);
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
        };
        var gradient = BuildShadingGradient(pattern, _gs.CurrentTransform);
        var region = BuildShadingPaintRegion(descriptor.BBox);
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
    ///     function(s) at <see cref="ShadingGradientSampleCount"/> evenly spaced points across
    ///     <see cref="ResolvedPattern.Domain"/> and converting each sampled output through
    ///     <see cref="ResolvedPattern.ShadingColorSpace"/> via <see cref="ColorFromComponents"/>.
    /// </summary>
    /// <param name="pattern">The resolved shading pattern. Must have <see cref="ResolvedPattern.Kind"/> of <see cref="ResolvedPattern.PatternKind.Shading"/>.</param>
    /// <param name="patternToDevice">The pattern-space-to-device-space transform (see <see cref="PatternToDeviceTransform"/>), used as the gradient's own <see cref="Gradient.Transform"/>.</param>
    /// <returns>The constructed gradient, ready to pass to <c>PathFiller.Fill(Surface, Path, Gradient, FillRule, float)</c>.</returns>
    /// <remarks>
    ///     <c>/Extend</c> (PDF 32000-1 §8.7.4.5.3, defaulting to <c>[false false]</c> - "paint
    ///     nothing outside the shading's own defining geometry") has no existing
    ///     <see cref="GradientSpread"/> equivalent (only <see cref="GradientSpread.Pad"/>/
    ///     <see cref="GradientSpread.Reflect"/>/<see cref="GradientSpread.Repeat"/> exist, none of
    ///     which is "transparent outside"). This method always uses
    ///     <see cref="GradientSpread.Pad"/> regardless of the shading's own declared
    ///     <c>/Extend</c> value - a documented, narrower-than-spec approximation (a true
    ///     "unextended" clip would require a general clipping mechanism, which is out of this
    ///     phase's scope alongside the <c>W</c>/<c>W*</c> operators).
    /// </remarks>
    private static Gradient BuildShadingGradient(ResolvedPattern pattern, Matrix3x2 patternToDevice)
    {
        var domain = pattern.Domain!;
        var evaluate = pattern.Evaluate!;
        var colorSpace = pattern.ShadingColorSpace!;
        var coords = pattern.Coords!;

        var stops = new GradientStop[ShadingGradientSampleCount];
        for (var i = 0; i < ShadingGradientSampleCount; i++)
        {
            var t = (float)i / (ShadingGradientSampleCount - 1);
            var domainValue = domain[0] + (t * (domain[1] - domain[0]));
            var components = evaluate(domainValue);
            var color = ColorFromComponents(colorSpace, components);
            stops[i] = new GradientStop(t, color);
        }

        if (pattern.ShadingType == 2)
        {
            return new LinearGradient(
                new Vector2((float)coords[0], (float)coords[1]),
                new Vector2((float)coords[2], (float)coords[3]),
                stops,
                GradientSpread.Pad,
                patternToDevice);
        }

        return new RadialGradient(
            new Vector2((float)coords[0], (float)coords[1]),
            (float)coords[2],
            new Vector2((float)coords[3], (float)coords[4]),
            (float)coords[5],
            stops,
            GradientSpread.Pad,
            patternToDevice);
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
    ///     The input domain to sample across (2 elements), and a delegate evaluating the
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
