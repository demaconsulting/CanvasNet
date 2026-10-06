// cspell:ignore cspace
using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Drawing;

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
    ///     Builds a <see cref="ResolvedPattern"/> of <see cref="ResolvedPattern.PatternKind.Shading"/>
    ///     from an already-resolved <c>/PatternType 2</c> pattern dictionary.
    /// </summary>
    /// <param name="patternDict">The already-resolved <c>/PatternType 2</c> pattern dictionary.</param>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>/Shading</c>, <c>/ColorSpace</c>, <c>/Function</c>, <c>/Domain</c>, or
    ///     <c>/Coords</c> is missing or malformed, or propagated from <see cref="ReadOptionalMatrix"/>/
    ///     <see cref="ResolveFunctionOrFunctionArray"/> for their own documented malformed-input
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
    private ResolvedPattern BuildShadingPattern(PdfObject patternDict)
    {
        var matrix = ReadOptionalMatrix(patternDict);
        var shadingEntry = patternDict.Get("Shading")
            ?? throw new InvalidDataException("/PatternType 2 pattern is missing required /Shading.");
        var shading = Resolve(shadingEntry);

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

        return new ResolvedPattern
        {
            Kind = ResolvedPattern.PatternKind.Shading,
            Matrix = matrix,
            ShadingType = shadingType,
            ShadingColorSpace = colorSpace,
            Coords = coords,
            Domain = domain,
            Evaluate = evaluate,
        };
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
