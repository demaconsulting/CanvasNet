// cspell:ignore hival
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Pdf;

public sealed partial class PdfDocument
{
    /// <summary>
    ///     The color spaces this phase recognizes for the current fill/stroke color
    ///     (<see cref="GraphicsState.FillColorSpace"/>/<see cref="GraphicsState.StrokeColorSpace"/>)
    ///     and for an image XObject's <c>/ColorSpace</c> entry (<c>PdfDocument.Images.cs</c>): the
    ///     three device color spaces, the CIE-based <c>CalGray</c>/<c>CalRGB</c>/<c>Lab</c> spaces
    ///     (converted to sRGB by <c>PdfDocument.ColorKernel.cs</c>), <c>/ICCBased</c> (resolved to
    ///     a device or CIE-based color space via <see cref="ResolveIccBasedColorSpace"/>, never by
    ///     applying the ICC profile itself) and <c>/Indexed</c> (a palette lookup over any of the
    ///     above, resolved via <see cref="ResolveIndexedColorSpace"/>), and <c>/Separation</c>/
    ///     <c>/DeviceN</c> (a tint-transform function into a device or CIE-based alternate,
    ///     resolved via <see cref="ResolveTintColorSpace"/>).
    /// </summary>
    /// <remarks>
    ///     Any other PDF color space is rejected with <see cref="UnsupportedImageFeatureException"/>
    ///     - a well-formed but unsupported color space, not a malformed one. Within the supported
    ///     color spaces,
    ///     two further edge cases are likewise rejected with
    ///     <see cref="UnsupportedImageFeatureException"/>: an <c>/ICCBased</c> stream whose <c>/N</c>
    ///     is not <c>1</c>/<c>3</c>/<c>4</c> and has no usable <c>/Alternate</c>, and an
    ///     <c>/Indexed</c> color space whose base color space is itself unsupported. Two component
    ///     conventions exist: operator operands and function/shading outputs are in the color
    ///     space's natural units (<c>Lab</c> <c>L*</c> <c>0..100</c>, <c>a*</c>/<c>b*</c> per
    ///     <c>/Range</c>), whereas image samples and <c>/Indexed</c> palette bytes are normalized
    ///     <c>[0, 1]</c> fractions that the kernel rescales for <c>Lab</c>.
    /// </remarks>
    internal sealed class PdfColorSpace
    {
        /// <summary>The distinct families of color space this phase recognizes.</summary>
        internal enum Family
        {
            /// <summary>A single gray component in <c>[0, 1]</c> (<c>0</c> = black, <c>1</c> = white).</summary>
            DeviceGray,

            /// <summary>Three additive red/green/blue components, each in <c>[0, 1]</c>.</summary>
            DeviceRGB,

            /// <summary>Four subtractive cyan/magenta/yellow/black components, each in <c>[0, 1]</c>.</summary>
            DeviceCMYK,

            /// <summary>
            ///     A single palette-index component (not normalized to <c>[0, 1]</c> - a raw
            ///     index, clamped into <c>[0, <see cref="IndexedHival"/>]</c>), looked up against
            ///     <see cref="IndexedPalette"/> and converted via <see cref="IndexedBase"/>.
            /// </summary>
            Indexed,

            /// <summary>
            ///     The <c>/Pattern</c> color space (PDF 32000-1 §8.7.3.3): selects a named
            ///     <c>/Pattern</c> resource via <c>scn</c>/<c>SCN</c>'s trailing name operand,
            ///     optionally with an underlying tint color space (<see cref="PatternBase"/>) for
            ///     an uncolored (<c>/PaintType 2</c>) tiling pattern. <see cref="ComponentCount"/>
            ///     is deliberately undefined for this family - see that method's remarks.
            /// </summary>
            Pattern,

            /// <summary>
            ///     A single CIE-based gray component in <c>[0, 1]</c> (<c>[/CalGray dict]</c>,
            ///     PDF 32000-1 §8.6.5.2), converted via <see cref="CalWhitePoint"/>/<see cref="CalGamma"/>.
            /// </summary>
            CalGray,

            /// <summary>
            ///     Three CIE-based <c>A B C</c> components in <c>[0, 1]</c> (<c>[/CalRGB dict]</c>,
            ///     PDF 32000-1 §8.6.5.3), converted via <see cref="CalWhitePoint"/>/<see cref="CalGamma"/>/
            ///     <see cref="CalMatrix"/>.
            /// </summary>
            CalRGB,

            /// <summary>
            ///     Three CIE 1976 <c>L*a*b*</c> components (<c>[/Lab dict]</c>, PDF 32000-1 §8.6.5.4):
            ///     <c>L*</c> in <c>[0, 100]</c>, <c>a*</c>/<c>b*</c> within <see cref="LabRange"/>.
            /// </summary>
            Lab,

            /// <summary>
            ///     A single-colorant <c>[/Separation name alternate tintTransform]</c> space (PDF 32000-1
            ///     §8.6.6.4): one tint in <c>[0, 1]</c> mapped through <see cref="TintFunction"/> into
            ///     <see cref="TintAlternate"/>. The reserved colorant <c>/None</c> paints nothing.
            /// </summary>
            Separation,

            /// <summary>
            ///     A multi-colorant <c>[/DeviceN names alternate tintTransform]</c> space (PDF 32000-1
            ///     §8.6.6.5): one tint per name in <see cref="ColorantNames"/> mapped through
            ///     <see cref="TintFunction"/> into <see cref="TintAlternate"/>.
            /// </summary>
            DeviceN,
        }

        /// <summary>
        ///     Gets the colorant names (one for <see cref="Family.Separation"/>, one per component for
        ///     <see cref="Family.DeviceN"/>).
        /// </summary>
        internal string[] ColorantNames { get; private init; } = [];

        /// <summary>Gets the alternate color space the tint transform outputs into, for <see cref="Family.Separation"/>/<see cref="Family.DeviceN"/>.</summary>
        internal PdfColorSpace? TintAlternate { get; private init; }

        /// <summary>Gets the resolved tint-transform function, for <see cref="Family.Separation"/>/<see cref="Family.DeviceN"/>.</summary>
        internal IPdfMultiFunction? TintFunction { get; private init; }

        /// <summary>
        ///     Gets a value indicating whether every colorant is the reserved name <c>/None</c>, so
        ///     painting in this space marks nothing (it yields a fully transparent color).
        /// </summary>
        internal bool PaintsNothing { get; private init; }

        /// <summary>Creates a <c>/Separation</c> or <c>/DeviceN</c> color-space instance.</summary>
        internal static PdfColorSpace Tint(
            Family kind, string[] names, PdfColorSpace alternate, IPdfMultiFunction function) => new()
            {
                Kind = kind,
                ColorantNames = names,
                TintAlternate = alternate,
                TintFunction = function,
                PaintsNothing = names.All(static n => n == "None"),
            };

        /// <summary>Gets the family of color space this instance represents.</summary>
        internal Family Kind { get; private init; }

        /// <summary>
        ///     Gets the base color space, when <see cref="Kind"/> is <see cref="Family.Indexed"/>.
        /// </summary>
        internal PdfColorSpace? IndexedBase { get; private init; }

        /// <summary>
        ///     Gets the highest valid palette index, when <see cref="Kind"/> is
        ///     <see cref="Family.Indexed"/>.
        /// </summary>
        internal int IndexedHival { get; private init; }

        /// <summary>
        ///     Gets the raw (un-normalized) palette bytes, when <see cref="Kind"/> is
        ///     <see cref="Family.Indexed"/>: <c>(IndexedHival + 1) * ComponentCount(IndexedBase)</c>
        ///     bytes, one <see cref="IndexedBase"/>-component-count-sized entry per palette index.
        /// </summary>
        internal byte[] IndexedPalette { get; private init; } = [];

        /// <summary>
        ///     Gets the underlying tint color space, when <see cref="Kind"/> is
        ///     <see cref="Family.Pattern"/> and the pattern color space was declared with a base
        ///     (<c>[/Pattern baseSpace]</c>, for an uncolored tiling pattern); <see langword="null"/>
        ///     for a colored-only pattern color space (<c>/Pattern</c> alone).
        /// </summary>
        internal PdfColorSpace? PatternBase { get; private init; }

        /// <summary>
        ///     Gets the CIE <c>/WhitePoint</c> (<c>Xw Yw Zw</c>, 3 numbers), when <see cref="Kind"/>
        ///     is <see cref="Family.CalGray"/>, <see cref="Family.CalRGB"/> or <see cref="Family.Lab"/>.
        /// </summary>
        internal double[] CalWhitePoint { get; private init; } = [];

        /// <summary>
        ///     Gets the <c>/Gamma</c> exponents (1 for <see cref="Family.CalGray"/>, 3 for
        ///     <see cref="Family.CalRGB"/>), when <see cref="Kind"/> is one of those families.
        /// </summary>
        internal double[] CalGamma { get; private init; } = [];

        /// <summary>
        ///     Gets the <c>/Matrix</c> (9 numbers, <c>XA YA ZA XB YB ZB XC YC ZC</c>), when
        ///     <see cref="Kind"/> is <see cref="Family.CalRGB"/>.
        /// </summary>
        internal double[] CalMatrix { get; private init; } = [];

        /// <summary>
        ///     Gets the <c>/Range</c> (<c>amin amax bmin bmax</c>), when <see cref="Kind"/> is
        ///     <see cref="Family.Lab"/>.
        /// </summary>
        internal double[] LabRange { get; private init; } = [];

        /// <summary>
        ///     Gets the precomputed 3x3 row-major matrix (Bradford adaptation from
        ///     <see cref="CalWhitePoint"/> to D65, then CIE XYZ to linear sRGB) used by the CIE-based
        ///     families' final XYZ-to-sRGB step.
        /// </summary>
        internal double[] XyzToLinearRgb { get; private init; } = [];

        /// <summary>Creates a <c>/CalGray</c> color-space instance.</summary>
        internal static PdfColorSpace CalGray(double[] whitePoint, double gamma) => new()
        {
            Kind = Family.CalGray,
            CalWhitePoint = whitePoint,
            CalGamma = [gamma],
            XyzToLinearRgb = BuildXyzToLinearRgbMatrix(whitePoint),
        };

        /// <summary>Creates a <c>/CalRGB</c> color-space instance.</summary>
        internal static PdfColorSpace CalRgb(double[] whitePoint, double[] gamma, double[] matrix) => new()
        {
            Kind = Family.CalRGB,
            CalWhitePoint = whitePoint,
            CalGamma = gamma,
            CalMatrix = matrix,
            XyzToLinearRgb = BuildXyzToLinearRgbMatrix(whitePoint),
        };

        /// <summary>Creates a <c>/Lab</c> color-space instance.</summary>
        internal static PdfColorSpace Lab(double[] whitePoint, double[] range) => new()
        {
            Kind = Family.Lab,
            CalWhitePoint = whitePoint,
            LabRange = range,
            XyzToLinearRgb = BuildXyzToLinearRgbMatrix(whitePoint),
        };

        /// <summary>The shared <c>DeviceGray</c> color-space instance.</summary>
        internal static readonly PdfColorSpace DeviceGray = new() { Kind = Family.DeviceGray };

        /// <summary>The shared <c>DeviceRGB</c> color-space instance.</summary>
        internal static readonly PdfColorSpace DeviceRGB = new() { Kind = Family.DeviceRGB };

        /// <summary>The shared <c>DeviceCMYK</c> color-space instance.</summary>
        internal static readonly PdfColorSpace DeviceCMYK = new() { Kind = Family.DeviceCMYK };

        /// <summary>Creates an <c>/Indexed</c> color-space instance.</summary>
        internal static PdfColorSpace Indexed(PdfColorSpace baseSpace, int hival, byte[] palette) => new()
        {
            Kind = Family.Indexed,
            IndexedBase = baseSpace,
            IndexedHival = hival,
            IndexedPalette = palette,
        };

        /// <summary>
        ///     Creates a <c>/Pattern</c> color-space instance, optionally with an underlying tint
        ///     color space (<paramref name="baseSpace"/>, <see langword="null"/> for a
        ///     colored-only pattern color space).
        /// </summary>
        internal static PdfColorSpace Pattern(PdfColorSpace? baseSpace) => new()
        {
            Kind = Family.Pattern,
            PatternBase = baseSpace,
        };
    }

    /// <summary>Handles the <c>g gray</c> operator: sets the fill color/space to <c>DeviceGray</c>.</summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> does not contain exactly 1 number.
    /// </exception>
    private void OpSetGrayFill(IReadOnlyList<PdfObject> operands) =>
        SetFillColor(PdfColorSpace.DeviceGray, RequireNumbers(operands, "g", 1));

    /// <summary>Handles the <c>G gray</c> operator: sets the stroke color/space to <c>DeviceGray</c>.</summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> does not contain exactly 1 number.
    /// </exception>
    private void OpSetGrayStroke(IReadOnlyList<PdfObject> operands) =>
        SetStrokeColor(PdfColorSpace.DeviceGray, RequireNumbers(operands, "G", 1));

    /// <summary>Handles the <c>r g b rg</c> operator: sets the fill color/space to <c>DeviceRGB</c>.</summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> does not contain exactly 3 numbers.
    /// </exception>
    private void OpSetRgbFill(IReadOnlyList<PdfObject> operands) =>
        SetFillColor(PdfColorSpace.DeviceRGB, RequireNumbers(operands, "rg", 3));

    /// <summary>Handles the <c>r g b RG</c> operator: sets the stroke color/space to <c>DeviceRGB</c>.</summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> does not contain exactly 3 numbers.
    /// </exception>
    private void OpSetRgbStroke(IReadOnlyList<PdfObject> operands) =>
        SetStrokeColor(PdfColorSpace.DeviceRGB, RequireNumbers(operands, "RG", 3));

    /// <summary>Handles the <c>c m y k k</c> operator: sets the fill color/space to <c>DeviceCMYK</c>.</summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> does not contain exactly 4 numbers.
    /// </exception>
    private void OpSetCmykFill(IReadOnlyList<PdfObject> operands) =>
        SetFillColor(PdfColorSpace.DeviceCMYK, RequireNumbers(operands, "k", 4));

    /// <summary>Handles the <c>c m y k K</c> operator: sets the stroke color/space to <c>DeviceCMYK</c>.</summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> does not contain exactly 4 numbers.
    /// </exception>
    private void OpSetCmykStroke(IReadOnlyList<PdfObject> operands) =>
        SetStrokeColor(PdfColorSpace.DeviceCMYK, RequireNumbers(operands, "K", 4));

    /// <summary>
    ///     Handles the <c>/name cs</c> operator: sets the current fill color space, resetting the
    ///     fill color to the PDF specification's own documented "reset to black" rule.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> is not exactly 1 <c>Name</c> operand.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when the named color space is not supported (see <see cref="PdfColorSpace"/>'s
    ///     own remarks), whether resolved directly or via <c>/Resources/ColorSpace</c>.
    /// </exception>
    private void OpSetColorSpaceFill(IReadOnlyList<PdfObject> operands)
    {
        _gs.FillColorSpace = ResolveColorSpaceOperand(operands, "cs");
        _gs.FillColor = InitialColor(_gs.FillColorSpace);
        _gs.FillPattern = null;
    }

    /// <summary>
    ///     Handles the <c>/name CS</c> operator: sets the current stroke color space, resetting
    ///     the stroke color to the PDF specification's own documented "reset to black" rule.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> is not exactly 1 <c>Name</c> operand.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when the named color space is not supported (see <see cref="PdfColorSpace"/>'s
    ///     own remarks), whether resolved directly or via <c>/Resources/ColorSpace</c>.
    /// </exception>
    private void OpSetColorSpaceStroke(IReadOnlyList<PdfObject> operands)
    {
        _gs.StrokeColorSpace = ResolveColorSpaceOperand(operands, "CS");
        _gs.StrokeColor = InitialColor(_gs.StrokeColorSpace);
        _gs.StrokePattern = null;
    }

    /// <summary>
    ///     Gets the initial color after a <c>cs</c>/<c>CS</c> operator: opaque black, except for
    ///     <c>Separation</c>/<c>DeviceN</c> whose initial tints are all <c>1.0</c> (PDF 32000-1 §8.6.6.4).
    /// </summary>
    private static Rgba32 InitialColor(PdfColorSpace colorSpace)
    {
        if (colorSpace.Kind is PdfColorSpace.Family.Separation or PdfColorSpace.Family.DeviceN)
        {
            var ones = new double[ComponentCount(colorSpace)];
            Array.Fill(ones, 1.0);
            return ColorFromComponentSpan(colorSpace, ones);
        }

        return new Rgba32(0, 0, 0, 255);
    }

    /// <summary>
    ///     Handles the <c>sc</c>/<c>scn</c> operators: sets the fill color from its current color
    ///     space's required component count, or - when the current fill color space is
    ///     <see cref="PdfColorSpace.Family.Pattern"/> - resolves and sets the fill pattern via
    ///     <see cref="SetFillPattern"/> instead.
    /// </summary>
    /// <param name="operands">The operator's accumulated operand stack.</param>
    /// <param name="operatorName">The operator name (<c>sc</c> or <c>scn</c>), for exception messages.</param>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> does not contain exactly
    ///     <c>ComponentCount(_gs.FillColorSpace)</c> numbers, or propagated from
    ///     <see cref="ResolvePatternOperand"/> for a malformed pattern operand shape.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Propagated from <see cref="ResolvePattern"/> for an undeclared or unsupported pattern.
    /// </exception>
    private void OpSetColorFill(IReadOnlyList<PdfObject> operands, string operatorName)
    {
        if (_gs.FillColorSpace.Kind == PdfColorSpace.Family.Pattern)
        {
            SetFillPattern(_gs.FillColorSpace, ResolvePatternOperand(operands, operatorName, _gs.FillColorSpace));
            return;
        }

        SetFillColor(_gs.FillColorSpace, RequireColorComponents(operands, operatorName, _gs.FillColorSpace));
    }

    /// <summary>
    ///     Handles the <c>SC</c>/<c>SCN</c> operators: sets the stroke color from its current
    ///     color space's required component count, or - when the current stroke color space is
    ///     <see cref="PdfColorSpace.Family.Pattern"/> - resolves and sets the stroke pattern via
    ///     <see cref="SetStrokePattern"/> instead.
    /// </summary>
    /// <param name="operands">The operator's accumulated operand stack.</param>
    /// <param name="operatorName">The operator name (<c>SC</c> or <c>SCN</c>), for exception messages.</param>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> does not contain exactly
    ///     <c>ComponentCount(_gs.StrokeColorSpace)</c> numbers, or propagated from
    ///     <see cref="ResolvePatternOperand"/> for a malformed pattern operand shape.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Propagated from <see cref="ResolvePattern"/> for an undeclared or unsupported pattern.
    /// </exception>
    private void OpSetColorStroke(IReadOnlyList<PdfObject> operands, string operatorName)
    {
        if (_gs.StrokeColorSpace.Kind == PdfColorSpace.Family.Pattern)
        {
            SetStrokePattern(_gs.StrokeColorSpace, ResolvePatternOperand(operands, operatorName, _gs.StrokeColorSpace));
            return;
        }

        SetStrokeColor(_gs.StrokeColorSpace, RequireColorComponents(operands, operatorName, _gs.StrokeColorSpace));
    }

    /// <summary>
    ///     Validates a <c>sc</c>/<c>SC</c>/<c>scn</c>/<c>SCN</c> operand list against a
    ///     non-<c>Pattern</c> color space, requiring exactly that color space's own component
    ///     count of numeric operands.
    /// </summary>
    private static double[] RequireColorComponents(IReadOnlyList<PdfObject> operands, string operatorName, PdfColorSpace colorSpace) =>
        RequireNumbers(operands, operatorName, ComponentCount(colorSpace));

    /// <summary>
    ///     A resolved <c>scn</c>/<c>SCN</c> pattern operand: the resolved pattern itself, plus -
    ///     for an uncolored (<c>/PaintType 2</c>) tiling pattern whose color space declared an
    ///     underlying tint base - the tint color converted from the operand's leading numeric
    ///     components.
    /// </summary>
    private readonly record struct PatternOperand(ResolvedPattern Pattern, Rgba32? UnderlyingTint);

    /// <summary>
    ///     Validates and resolves a <c>scn</c>/<c>SCN</c> pattern-color-space operand list: the
    ///     trailing operand must be a pattern <c>Name</c>; every operand before it must be
    ///     numeric, with count exactly <c>ComponentCount(colorSpace.PatternBase)</c> when the
    ///     <em>resolved pattern itself</em> is an uncolored (<c>/PaintType 2</c>) tiling pattern
    ///     and <paramref name="colorSpace"/> declared an underlying tint base, or exactly <c>0</c>
    ///     otherwise.
    /// </summary>
    /// <remarks>
    ///     Deliberately does not key the expected leading-operand count off
    ///     <paramref name="colorSpace"/>'s own declared base alone: many real-world PDF
    ///     producers declare a <c>[/Pattern baseSpace]</c> color space (with an underlying tint
    ///     base) and then use it with <em>colored</em> (<c>/PaintType 1</c>) tiling patterns or
    ///     shading patterns too, supplying only the pattern name with no leading tint
    ///     components - which Acrobat (and every other mainstream viewer) tolerates, since a
    ///     colored pattern/shading pattern never actually needs a tint. Matching that tolerance
    ///     requires resolving the named pattern <em>first</em>, then deciding the expected
    ///     component count from the resolved pattern's own <see cref="ResolvedPattern.PaintType"/>
    ///     (only meaningful for a tiling pattern), not merely from the color space's declared
    ///     shape.
    /// </remarks>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the last operand is not a <c>Name</c>, or when the leading numeric operand
    ///     count does not match the expected shape above.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Propagated from <see cref="ResolvePattern"/> for an undeclared or unsupported pattern.
    /// </exception>
    private PatternOperand ResolvePatternOperand(IReadOnlyList<PdfObject> operands, string operatorName, PdfColorSpace colorSpace)
    {
        if (operands.Count == 0 || operands[^1].Kind != PdfKind.Name)
        {
            throw new InvalidDataException(
                $"Operator '{operatorName}' against a /Pattern color space requires a trailing pattern name operand.");
        }

        var pattern = ResolvePattern(operands[^1].Text);
        var isUncoloredTiling = pattern.Kind == ResolvedPattern.PatternKind.Tiling && pattern.PaintType == 2;
        var expectedComponentCount = isUncoloredTiling && colorSpace.PatternBase is not null
            ? ComponentCount(colorSpace.PatternBase)
            : 0;

        var componentOperands = operands.Take(operands.Count - 1).ToArray();
        if (componentOperands.Length != expectedComponentCount)
        {
            throw new InvalidDataException(
                $"Operator '{operatorName}' against this /Pattern color space requires exactly " +
                $"{expectedComponentCount} numeric operand(s) before the pattern name; got {componentOperands.Length}.");
        }

        var components = RequireNumbers(componentOperands, operatorName, expectedComponentCount);
        var tint = expectedComponentCount == 0 ? (Rgba32?)null : ColorFromComponents(colorSpace.PatternBase!, components);

        return new PatternOperand(pattern, tint);
    }

    /// <summary>
    ///     Handles setting the fill pattern (the Pattern-color-space branch of
    ///     <see cref="OpSetColorFill"/>): sets <see cref="GraphicsState.FillColorSpace"/>/
    ///     <see cref="GraphicsState.FillPattern"/>, and <see cref="GraphicsState.FillColor"/> to
    ///     <paramref name="operand"/>'s own resolved tint when present (consulted later by
    ///     <c>PaintPatternFill</c> as the tint to recolor an uncolored <c>/PaintType 2</c> tiling
    ///     pattern's cell with) or a documented opaque-black placeholder otherwise - never
    ///     actually painted with directly in that case, since <see cref="PaintCurrentPath"/>
    ///     branches on <see cref="GraphicsState.FillColorSpace"/>'s <see cref="PdfColorSpace.Kind"/>
    ///     being <see cref="PdfColorSpace.Family.Pattern"/> before ever reading
    ///     <see cref="GraphicsState.FillColor"/> for anything other than this tint purpose.
    /// </summary>
    private void SetFillPattern(PdfColorSpace colorSpace, PatternOperand operand)
    {
        _gs.FillColorSpace = colorSpace;
        _gs.FillPattern = operand.Pattern;
        _gs.FillColor = operand.UnderlyingTint ?? new Rgba32(0, 0, 0, 255);
    }

    /// <summary>
    ///     Handles setting the stroke pattern (the Pattern-color-space branch of
    ///     <see cref="OpSetColorStroke"/>) - see <see cref="SetFillPattern"/>'s remarks, which
    ///     apply identically here against <see cref="GraphicsState.StrokeColorSpace"/>/
    ///     <see cref="GraphicsState.StrokePattern"/>/<see cref="GraphicsState.StrokeColor"/>.
    /// </summary>
    private void SetStrokePattern(PdfColorSpace colorSpace, PatternOperand operand)
    {
        _gs.StrokeColorSpace = colorSpace;
        _gs.StrokePattern = operand.Pattern;
        _gs.StrokeColor = operand.UnderlyingTint ?? new Rgba32(0, 0, 0, 255);
    }

    /// <summary>Sets the current fill color/space from raw color-component values.</summary>
    private void SetFillColor(PdfColorSpace colorSpace, IReadOnlyList<double> components)
    {
        _gs.FillColorSpace = colorSpace;
        _gs.FillColor = ColorFromComponents(colorSpace, components);
        _gs.FillPattern = null;
    }

    /// <summary>Sets the current stroke color/space from raw color-component values.</summary>
    private void SetStrokeColor(PdfColorSpace colorSpace, IReadOnlyList<double> components)
    {
        _gs.StrokeColorSpace = colorSpace;
        _gs.StrokeColor = ColorFromComponents(colorSpace, components);
        _gs.StrokePattern = null;
    }

    /// <summary>
    ///     Validates a <c>cs</c>/<c>CS</c> operand list (exactly 1 <c>Name</c> operand) and
    ///     resolves it to a <see cref="PdfColorSpace"/>.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> is not exactly 1 <c>Name</c> operand.
    /// </exception>
    private PdfColorSpace ResolveColorSpaceOperand(IReadOnlyList<PdfObject> operands, string operatorName)
    {
        if (operands.Count != 1 || operands[0].Kind != PdfKind.Name)
        {
            throw new InvalidDataException(
                $"Operator '{operatorName}' requires exactly 1 name operand; got {operands.Count}.");
        }

        return ResolveColorSpaceByName(operands[0].Text);
    }

    /// <summary>
    ///     The maximum number of nested <c>/Resources/ColorSpace</c> name lookups
    ///     <see cref="ResolveColorSpaceByName"/>/<see cref="ResolveColorSpaceValue"/> allow before
    ///     failing closed. A malformed or adversarial PDF can declare a color-space name that
    ///     resolves (directly, or via a cycle of several names) back to itself; without this bound
    ///     such a document would recurse until the process' call stack is exhausted, raising an
    ///     unrecoverable <see cref="StackOverflowException"/> that crashes the whole process rather
    ///     than failing this document open/render call alone.
    /// </summary>
    private const int MaxColorSpaceRecursionDepth = 32;

    /// <summary>
    ///     The current color-space name resolution recursion depth, incremented/decremented around
    ///     every <see cref="ResolveColorSpaceByName"/> call.
    /// </summary>
    private int _colorSpaceRecursionDepth;

    /// <summary>
    ///     Resolves a color-space name to a <see cref="PdfColorSpace"/>: the three device
    ///     names resolve directly; any other name is looked up in the current page's
    ///     <c>/Resources/ColorSpace</c> dictionary and the resolved value is classified via
    ///     <see cref="ResolveColorSpaceValue"/>.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the name resolves back to itself (directly or via a cycle of several
    ///     names), or otherwise nests deeper than <see cref="MaxColorSpaceRecursionDepth"/>.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when the name is not one of the three device names and either cannot be
    ///     resolved via <c>/Resources/ColorSpace</c> at all, or resolves to an unsupported color
    ///     space (any family other than the supported ones, or an
    ///     <c>/ICCBased</c>/<c>/Indexed</c> edge case documented on <see cref="PdfColorSpace"/>) -
    ///     <c>CalGray</c>/<c>CalRGB</c>/<c>Lab</c> are supported, see <see cref="ResolveColorSpaceValue"/>.
    /// </exception>
    private PdfColorSpace ResolveColorSpaceByName(string name)
    {
        switch (name)
        {
            case "DeviceGray":
                return PdfColorSpace.DeviceGray;
            case "DeviceRGB":
                return PdfColorSpace.DeviceRGB;
            case "DeviceCMYK":
                return PdfColorSpace.DeviceCMYK;
            case "Pattern":
                return PdfColorSpace.Pattern(null);
        }

        if (_colorSpaceRecursionDepth >= MaxColorSpaceRecursionDepth)
        {
            throw new InvalidDataException(
                $"Color space '{name}' resolution exceeds the maximum supported nesting depth of {MaxColorSpaceRecursionDepth}.");
        }

        var colorSpaceDictionary = _resources?.Get("ColorSpace");
        var resolvedDictionary = colorSpaceDictionary is null ? null : Resolve(colorSpaceDictionary);
        var entry = resolvedDictionary?.Get(name);
        if (entry is null)
        {
            throw new UnsupportedImageFeatureException(
                $"pdf-colorspace-{name}",
                $"Color space '{name}' is not declared in /Resources/ColorSpace and is not a device color space.");
        }

        _colorSpaceRecursionDepth++;
        try
        {
            return ResolveColorSpaceValue(Resolve(entry));
        }
        finally
        {
            _colorSpaceRecursionDepth--;
        }
    }

    /// <summary>
    ///     Classifies an already-resolved color-space value (a <c>Name</c> or an <c>Array</c>,
    ///     for example an image XObject's own <c>/ColorSpace</c> entry) into a
    ///     <see cref="PdfColorSpace"/>.
    /// </summary>
    /// <remarks>
    ///     <c>[/CalGray dict]</c>, <c>[/CalRGB dict]</c> and <c>[/Lab dict]</c> (PDF 32000-1
    ///     §8.6.5.2-§8.6.5.4) are resolved through <see cref="ResolveCalibratedColorSpace"/> into
    ///     the CIE-based families, converted to sRGB by the kernel in <c>PdfDocument.ColorKernel.cs</c>.
    ///     <c>Separation</c> and <c>DeviceN</c> are resolved through <see cref="ResolveTintColorSpace"/>.
    /// </remarks>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="value"/> is neither a <c>Name</c> nor an <c>Array</c>,
    ///     propagated from <see cref="ResolveIndexedColorSpace"/> for a malformed <c>/Indexed</c>
    ///     array, or from <see cref="ResolveCalibratedColorSpace"/> for a malformed CIE-based
    ///     dictionary.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when the value names/represents a color space other than the supported ones, or
    ///     propagated from <see cref="ResolveIccBasedColorSpace"/>/<see cref="ResolveIndexedColorSpace"/>
    ///     for the documented edge cases of those two color spaces.
    /// </exception>
    private PdfColorSpace ResolveColorSpaceValue(PdfObject value)
    {
        if (value.Kind == PdfKind.Name)
        {
            return ResolveColorSpaceByName(value.Text);
        }

        if (value.Kind == PdfKind.Array)
        {
            var name = value.Items.Count > 0 && Resolve(value.Items[0]).Kind == PdfKind.Name
                ? Resolve(value.Items[0]).Text
                : "Unknown";

            switch (name)
            {
                case "ICCBased":
                    return ResolveIccBasedColorSpace(value);
                case "Indexed":
                    return ResolveIndexedColorSpace(value);
                case "Pattern":
                    return ResolvePatternColorSpace(value);
                case "CalGray":
                case "CalRGB":
                case "Lab":
                    return ResolveCalibratedColorSpace(value, name);
                case "Separation":
                case "DeviceN":
                    return ResolveTintColorSpace(value, name);
            }

            throw new UnsupportedImageFeatureException(
                $"pdf-colorspace-{name}",
                $"Color space '{name}' is not supported.");
        }

        throw new InvalidDataException("Color space value must be a name or an array.");
    }

    /// <summary>
    ///     Resolves a <c>[/CalGray dict]</c>, <c>[/CalRGB dict]</c> or <c>[/Lab dict]</c> array.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the array does not have exactly 2 elements, the 2nd is not a dictionary,
    ///     <c>/WhitePoint</c> is missing or not 3 positive numbers, <c>/Gamma</c> is not
    ///     positive numbers of the right count, <c>/Matrix</c> is not 9 numbers, or <c>/Range</c>
    ///     is not 4 numbers with <c>min &lt;= max</c>.
    /// </exception>
    private PdfColorSpace ResolveCalibratedColorSpace(PdfObject array, string name)
    {
        if (array.Items.Count != 2)
        {
            throw new InvalidDataException($"Color space '/{name}' requires exactly 2 array elements.");
        }

        var dictionary = Resolve(array.Items[1]);
        if (dictionary.Kind != PdfKind.Dictionary)
        {
            throw new InvalidDataException($"Color space '/{name}' requires its 2nd element to be a dictionary.");
        }

        var white = ResolveOptionalNumberArray(dictionary, "WhitePoint")
            ?? throw new InvalidDataException($"Color space '/{name}' is missing required /WhitePoint.");
        if (white.Length != 3 || white[0] <= 0 || white[1] <= 0 || white[2] <= 0
            || !double.IsFinite(white[0]) || !double.IsFinite(white[1]) || !double.IsFinite(white[2]))
        {
            throw new InvalidDataException($"Color space '/{name}' /WhitePoint must be 3 positive numbers.");
        }

        switch (name)
        {
            case "CalGray":
                {
                    var gamma = dictionary.Get("Gamma") is { } gammaEntry
                        ? Resolve(gammaEntry)
                        : null;
                    var gammaValue = 1.0;
                    if (gamma is not null)
                    {
                        if (gamma.Kind != PdfKind.Number || !(gamma.Number > 0) || !double.IsFinite(gamma.Number))
                        {
                            throw new InvalidDataException("Color space '/CalGray' /Gamma must be a positive number.");
                        }

                        gammaValue = gamma.Number;
                    }

                    return PdfColorSpace.CalGray(white, gammaValue);
                }

            case "CalRGB":
                {
                    var gamma = ResolveOptionalNumberArray(dictionary, "Gamma") ?? [1.0, 1.0, 1.0];
                    if (gamma.Length != 3 || gamma.Any(static g => !(g > 0) || !double.IsFinite(g)))
                    {
                        throw new InvalidDataException("Color space '/CalRGB' /Gamma must be 3 positive numbers.");
                    }

                    var matrix = ResolveOptionalNumberArray(dictionary, "Matrix") ?? [1, 0, 0, 0, 1, 0, 0, 0, 1];
                    if (matrix.Length != 9 || matrix.Any(static m => !double.IsFinite(m)))
                    {
                        throw new InvalidDataException("Color space '/CalRGB' /Matrix must be 9 numbers.");
                    }

                    return PdfColorSpace.CalRgb(white, gamma, matrix);
                }

            default:
                {
                    var range = ResolveOptionalNumberArray(dictionary, "Range") ?? [-100, 100, -100, 100];
                    if (range.Length != 4 || range.Any(static r => !double.IsFinite(r)) || range[0] > range[1] || range[2] > range[3])
                    {
                        throw new InvalidDataException("Color space '/Lab' /Range must be 4 numbers with min <= max.");
                    }

                    return PdfColorSpace.Lab(white, range);
                }
        }
    }

    /// <summary>
    ///     The maximum number of colorants a <c>/DeviceN</c> color space may declare (a resource
    ///     bound on per-color-evaluation work and stack use).
    /// </summary>
    private const int MaxDeviceNComponents = 32;

    /// <summary>
    ///     Resolves a <c>[/Separation name alternate tintTransform]</c> or
    ///     <c>[/DeviceN names alternate tintTransform attributes?]</c> array. The tint transform is
    ///     authoritative; the <c>/DeviceN</c> attributes dictionary (<c>/Process</c>,
    ///     <c>/Colorants</c>) is ignored. A colorant named <c>/All</c> is treated like any other
    ///     (the tint transform approximates it, as the specification permits).
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown for a wrong element count, a non-name colorant, an empty or oversized
    ///     <c>/DeviceN</c> name list, or a tint transform whose input/output counts do not match
    ///     the colorant count and alternate component count.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown (feature <c>pdf-colorspace-{name}-alternate</c>) when the alternate is
    ///     <c>Pattern</c>, <c>Indexed</c>, <c>Separation</c> or <c>DeviceN</c>, or propagated from
    ///     the alternate's or the tint transform's own resolution.
    /// </exception>
    private PdfColorSpace ResolveTintColorSpace(PdfObject array, string name)
    {
        var isSeparation = name == "Separation";
        if (array.Items.Count != 4 && (isSeparation || array.Items.Count != 5))
        {
            throw new InvalidDataException($"Color space '/{name}' has an invalid number of array elements.");
        }

        string[] names;
        var namesValue = Resolve(array.Items[1]);
        if (isSeparation)
        {
            if (namesValue.Kind != PdfKind.Name)
            {
                throw new InvalidDataException("Color space '/Separation' requires a colorant name.");
            }

            names = [namesValue.Text];
        }
        else
        {
            if (namesValue.Kind != PdfKind.Array || namesValue.Items.Count is 0 or > MaxDeviceNComponents)
            {
                throw new InvalidDataException(
                    $"Color space '/DeviceN' requires 1 to {MaxDeviceNComponents} colorant names.");
            }

            names = new string[namesValue.Items.Count];
            for (var i = 0; i < names.Length; i++)
            {
                var item = Resolve(namesValue.Items[i]);
                if (item.Kind != PdfKind.Name)
                {
                    throw new InvalidDataException("Color space '/DeviceN' colorant names must all be names.");
                }

                names[i] = item.Text;
            }
        }

        if (_colorSpaceRecursionDepth >= MaxColorSpaceRecursionDepth)
        {
            throw new InvalidDataException(
                $"Color space '{name}' alternate resolution exceeds the maximum supported nesting depth of {MaxColorSpaceRecursionDepth}.");
        }

        PdfColorSpace alternate;
        _colorSpaceRecursionDepth++;
        try
        {
            alternate = ResolveColorSpaceValue(Resolve(array.Items[2]));
        }
        finally
        {
            _colorSpaceRecursionDepth--;
        }

        if (alternate.Kind is PdfColorSpace.Family.Pattern or PdfColorSpace.Family.Indexed
            or PdfColorSpace.Family.Separation or PdfColorSpace.Family.DeviceN)
        {
            throw new UnsupportedImageFeatureException(
                $"pdf-colorspace-{name}-alternate",
                $"Color space '/{name}' alternate '{alternate.Kind}' is not supported.");
        }

        var function = ResolveMultiInputFunction(array.Items[3]);
        if (function.InputCount != names.Length)
        {
            throw new InvalidDataException(
                $"Color space '/{name}' tint transform has {function.InputCount} input(s); expected {names.Length}.");
        }

        if (function.OutputCount != ComponentCount(alternate))
        {
            throw new InvalidDataException(
                $"Color space '/{name}' tint transform has {function.OutputCount} output(s); the alternate needs {ComponentCount(alternate)}.");
        }

        return PdfColorSpace.Tint(
            isSeparation ? PdfColorSpace.Family.Separation : PdfColorSpace.Family.DeviceN, names, alternate, function);
    }

    /// <summary>
    ///     Resolves a <c>[/Pattern]</c> or <c>[/Pattern baseSpace]</c> color-space array
    ///     (PDF 32000-1 §8.7.3.3): a 1-element array is a colored-only pattern color space
    ///     (<see cref="PdfColorSpace.PatternBase"/> is <see langword="null"/>); a 2-element array
    ///     resolves its 2nd element (recursively, via <see cref="ResolveColorSpaceValue"/>, under
    ///     the same <see cref="_colorSpaceRecursionDepth"/> guard <see cref="ResolveColorSpaceByName"/>
    ///     already uses) as the underlying tint color space for an uncolored tiling pattern.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the recursion depth already equals <see cref="MaxColorSpaceRecursionDepth"/>.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when <paramref name="array"/> does not have exactly 1 or 2 elements (feature
    ///     <c>pdf-colorspace-Pattern</c>); when the resolved base color space is itself a
    ///     <c>/Pattern</c> color space - nested <c>[/Pattern /Pattern]</c> is not a meaningful PDF
    ///     construct (feature <c>pdf-colorspace-Pattern-nested</c>); or propagated from
    ///     <see cref="ResolveColorSpaceValue"/> when the base color space is itself unsupported.
    /// </exception>
    private PdfColorSpace ResolvePatternColorSpace(PdfObject array)
    {
        if (array.Items.Count is not (1 or 2))
        {
            throw new UnsupportedImageFeatureException(
                "pdf-colorspace-Pattern",
                "Color space '/Pattern' array must have exactly 1 or 2 elements.");
        }

        if (array.Items.Count == 1)
        {
            return PdfColorSpace.Pattern(null);
        }

        if (_colorSpaceRecursionDepth >= MaxColorSpaceRecursionDepth)
        {
            throw new InvalidDataException(
                $"Color space 'Pattern' base resolution exceeds the maximum supported nesting depth of {MaxColorSpaceRecursionDepth}.");
        }

        _colorSpaceRecursionDepth++;
        try
        {
            var baseSpace = ResolveColorSpaceValue(Resolve(array.Items[1]));
            if (baseSpace.Kind == PdfColorSpace.Family.Pattern)
            {
                throw new UnsupportedImageFeatureException(
                    "pdf-colorspace-Pattern-nested",
                    "Color space '/Pattern' base must not itself be a '/Pattern' color space.");
            }

            return PdfColorSpace.Pattern(baseSpace);
        }
        finally
        {
            _colorSpaceRecursionDepth--;
        }
    }

    /// <summary>
    ///     Resolves an <c>/ICCBased</c> color-space array (<c>[/ICCBased streamRef]</c>): the
    ///     stream's <c>/N</c> component count is authoritative, and the ICC profile bytes are never
    ///     parsed or applied. A present <c>/Alternate</c> that resolves to a supported,
    ///     non-<c>Indexed</c>, non-<c>Pattern</c> color space with exactly <c>/N</c> components is
    ///     used (including <c>CalGray</c>/<c>CalRGB</c>/<c>Lab</c>); otherwise a valid <c>/N</c>
    ///     (<c>1</c>/<c>3</c>/<c>4</c>) maps to <c>DeviceGray</c>/<c>DeviceRGB</c>/<c>DeviceCMYK</c>.
    ///     When <c>/N</c> is absent or not <c>1</c>/<c>3</c>/<c>4</c>, a usable <c>/Alternate</c> is
    ///     used as-is.
    /// </summary>
    /// <remarks>
    ///     An <c>/Alternate</c> whose resolution throws <see cref="UnsupportedImageFeatureException"/>
    ///     (absent, or itself naming an unsupported color space) is deliberately treated as "no
    ///     usable /Alternate", falling back to the <c>/N</c>-implied device space instead of
    ///     propagating - this is the PDF specification's own documented fallback semantics for
    ///     <c>/ICCBased</c>, not an oversight. An <c>/Alternate</c> whose component count disagrees
    ///     with a valid <c>/N</c> is likewise ignored (the spec makes <c>/N</c> authoritative). A
    ///     structurally malformed <c>/Alternate</c> (which throws <see cref="InvalidDataException"/>)
    ///     is not caught, and propagates unmodified.
    /// </remarks>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when <paramref name="array"/> does not have exactly 2 elements, when its 2nd
    ///     element does not resolve to a stream, or when neither <c>/Alternate</c> nor a 1/3/4
    ///     <c>/N</c> is usable.
    /// </exception>
    private PdfColorSpace ResolveIccBasedColorSpace(PdfObject array)
    {
        if (array.Items.Count != 2)
        {
            throw new UnsupportedImageFeatureException(
                "pdf-colorspace-ICCBased",
                "Color space '/ICCBased' requires exactly 2 array elements.");
        }

        var stream = Resolve(array.Items[1]);
        if (stream.Kind != PdfKind.Stream)
        {
            throw new UnsupportedImageFeatureException(
                "pdf-colorspace-ICCBased",
                "Color space '/ICCBased' requires its 2nd element to resolve to a stream.");
        }

        var nEntry = stream.Get("N");
        var resolvedN = nEntry is null ? null : Resolve(nEntry);
        // A present /N must be a finite exact integer: a fractional value such as 1.5 is malformed and
        // is treated like any other unusable /N (never silently truncated to 1).
        var n = resolvedN is { Kind: PdfKind.Number } && IsIntegralInRange(resolvedN.Number, int.MinValue, int.MaxValue)
            ? (int)resolvedN.Number
            : (int?)null;
        var nIsValid = n is 1 or 3 or 4;

        var alternate = stream.Get("Alternate");
        if (alternate is not null)
        {
            try
            {
                var alternateSpace = ResolveColorSpaceValue(Resolve(alternate));
                if (alternateSpace.Kind is not (PdfColorSpace.Family.Indexed or PdfColorSpace.Family.Pattern
                        or PdfColorSpace.Family.Separation or PdfColorSpace.Family.DeviceN)
                    && (!nIsValid || ComponentCount(alternateSpace) == n))
                {
                    return alternateSpace;
                }
            }
            catch (UnsupportedImageFeatureException)
            {
                // No usable /Alternate - fall through to /N-based resolution.
            }
        }

        return n switch
        {
            1 => PdfColorSpace.DeviceGray,
            3 => PdfColorSpace.DeviceRGB,
            4 => PdfColorSpace.DeviceCMYK,
            _ => throw new UnsupportedImageFeatureException(
                "pdf-colorspace-ICCBased",
                $"Color space '/ICCBased' has an unsupported /N ({(n is null ? "missing" : n.ToString())}) and no usable /Alternate."),
        };
    }

    /// <summary>
    ///     Resolves an <c>/Indexed</c> color-space array
    ///     (<c>[/Indexed baseSpace hival lookup]</c>): the base color space (resolved recursively
    ///     via <see cref="ResolveColorSpaceValue"/>), the highest valid palette index
    ///     (<c>/Hival</c>), and the raw palette bytes (<c>lookup</c>, either a PDF string's raw
    ///     bytes or a stream decoded via <see cref="GetStreamDecodedBytes"/>).
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="array"/> does not have exactly 4 elements, when
    ///     <c>/Hival</c> is not a non-negative integer or exceeds <c>255</c>, when the lookup
    ///     table is neither a string nor a stream, or when the resolved palette is shorter than
    ///     <c>(Hival + 1) * ComponentCount(baseSpace)</c> bytes.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Propagated from <see cref="ResolveColorSpaceValue"/> when the base color space is
    ///     itself unsupported.
    /// </exception>
    private PdfColorSpace ResolveIndexedColorSpace(PdfObject array)
    {
        if (array.Items.Count != 4)
        {
            throw new InvalidDataException("Color space '/Indexed' requires exactly 4 array elements.");
        }

        var baseSpace = ResolveColorSpaceValue(Resolve(array.Items[1]));

        var hivalValue = Resolve(array.Items[2]);
        if (hivalValue.Kind != PdfKind.Number || !double.IsFinite(hivalValue.Number) || hivalValue.Number < 0
            || !double.IsInteger(hivalValue.Number))
        {
            throw new InvalidDataException("Color space '/Indexed' /Hival must be a non-negative integer.");
        }

        if (hivalValue.Number > 255)
        {
            throw new InvalidDataException("Color space '/Indexed' /Hival must not exceed 255.");
        }

        var hival = (int)hivalValue.Number;

        var lookup = Resolve(array.Items[3]);
        var palette = lookup.Kind switch
        {
            PdfKind.String => lookup.Bytes,
            PdfKind.Stream => GetStreamDecodedBytes(lookup),
            _ => throw new InvalidDataException("Color space '/Indexed' lookup table must be a string or a stream."),
        };

        var requiredLength = (hival + 1) * ComponentCount(baseSpace);
        if (palette.Length < requiredLength)
        {
            throw new InvalidDataException(
                $"Color space '/Indexed' lookup table has {palette.Length} byte(s); expected at least {requiredLength}.");
        }

        return PdfColorSpace.Indexed(baseSpace, hival, palette);
    }

    /// <summary>Gets the number of numeric color components a color space requires.</summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when <paramref name="colorSpace"/>'s family is <see cref="PdfColorSpace.Family.Pattern"/>
    ///     - <c>/Pattern</c> has no fixed numeric-component count of its own; the dedicated
    ///     <c>scn</c>/<c>SCN</c> pattern-operand path (<see cref="ResolvePatternOperand"/>) must
    ///     be used instead, which only ever calls this method against
    ///     <c>colorSpace.PatternBase</c> (never against a <see cref="PdfColorSpace.Family.Pattern"/>
    ///     space itself) - this throw is therefore a fail-closed backstop for an unreachable
    ///     call path, not a documented, reachable behavior.
    /// </exception>
    private static int ComponentCount(PdfColorSpace colorSpace) => colorSpace.Kind switch
    {
        PdfColorSpace.Family.DeviceGray => 1,
        PdfColorSpace.Family.DeviceRGB => 3,
        PdfColorSpace.Family.DeviceCMYK => 4,
        PdfColorSpace.Family.Indexed => 1,
        PdfColorSpace.Family.CalGray => 1,
        PdfColorSpace.Family.CalRGB => 3,
        PdfColorSpace.Family.Lab => 3,
        PdfColorSpace.Family.Separation => 1,
        PdfColorSpace.Family.DeviceN => colorSpace.ColorantNames.Length,
        PdfColorSpace.Family.Pattern => throw new InvalidOperationException(
            "ComponentCount is not defined for the Pattern color space; use the dedicated scn/SCN pattern-operand path instead."),
        _ => throw new InvalidOperationException($"Unreachable: unrecognized color space {colorSpace.Kind}."),
    };

    /// <summary>
    ///     Converts a color space's raw component values (each expected in <c>[0, 1]</c>, but
    ///     clamped rather than rejected when slightly out of range - a documented leniency for
    ///     real-world producers that occasionally emit a cosmetic overshoot) into an opaque
    ///     <see cref="Rgba32"/> color.
    /// </summary>
    /// <remarks>
    ///     <c>DeviceCMYK</c> uses the standard, uncalibrated naive conversion
    ///     <c>R = 255 * (1 - C) * (1 - K)</c> (and the equivalent formula for <c>G</c>/<c>B</c>
    ///     from <c>M</c>/<c>Y</c>) - not a color-managed conversion, matching this phase's
    ///     documented "device color, no color management" scope. <c>Indexed</c>'s single
    ///     component is a raw, un-normalized palette index (not a <c>[0, 1]</c> fraction) - see
    ///     <see cref="IndexedToColor"/>.
    /// </remarks>
    private static Rgba32 ColorFromComponents(PdfColorSpace colorSpace, IReadOnlyList<double> components) =>
        colorSpace.Kind switch
        {
            PdfColorSpace.Family.DeviceGray => GrayToColor(components[0]),
            PdfColorSpace.Family.DeviceRGB => RgbToColor(components[0], components[1], components[2]),
            PdfColorSpace.Family.DeviceCMYK => CmykToColor(components[0], components[1], components[2], components[3]),
            PdfColorSpace.Family.Indexed => IndexedToColor(colorSpace, components[0]),
            PdfColorSpace.Family.CalGray => CalGrayToColor(colorSpace, components[0]),
            PdfColorSpace.Family.CalRGB => CalRgbToColor(colorSpace, components[0], components[1], components[2]),
            PdfColorSpace.Family.Lab => LabToColor(colorSpace, components[0], components[1], components[2]),
            PdfColorSpace.Family.Separation or PdfColorSpace.Family.DeviceN => TintToColor(colorSpace, components.ToArray()),
            _ => throw new InvalidOperationException($"Unreachable: unrecognized color space {colorSpace.Kind}."),
        };

    /// <summary>
    ///     Allocation-free twin of <see cref="ColorFromComponents(PdfColorSpace, IReadOnlyList{double})"/>
    ///     for per-pixel image conversion: identical conversion formulas over a span of components
    ///     in the color space's natural units.
    /// </summary>
    private static Rgba32 ColorFromComponentSpan(PdfColorSpace colorSpace, ReadOnlySpan<double> components) =>
        colorSpace.Kind switch
        {
            PdfColorSpace.Family.DeviceGray => GrayToColor(components[0]),
            PdfColorSpace.Family.DeviceRGB => RgbToColor(components[0], components[1], components[2]),
            PdfColorSpace.Family.DeviceCMYK => CmykToColor(components[0], components[1], components[2], components[3]),
            PdfColorSpace.Family.Indexed => IndexedToColor(colorSpace, components[0]),
            PdfColorSpace.Family.CalGray => CalGrayToColor(colorSpace, components[0]),
            PdfColorSpace.Family.CalRGB => CalRgbToColor(colorSpace, components[0], components[1], components[2]),
            PdfColorSpace.Family.Lab => LabToColor(colorSpace, components[0], components[1], components[2]),
            PdfColorSpace.Family.Separation or PdfColorSpace.Family.DeviceN => TintToColor(colorSpace, components),
            _ => throw new InvalidOperationException($"Unreachable: unrecognized color space {colorSpace.Kind}."),
        };

    /// <summary>
    ///     Normalized-component entry point (image samples and <c>/Indexed</c> palette bytes divided
    ///     by <c>255</c>, each in <c>[0, 1]</c>): identical to
    ///     <see cref="ColorFromComponentSpan"/> for every family except <c>Lab</c>, whose
    ///     normalized components are first rescaled to <c>L* = 100 v</c> and <c>a*</c>/<c>b*</c>
    ///     across <see cref="PdfColorSpace.LabRange"/>.
    /// </summary>
    private static Rgba32 ColorFromNormalizedSpan(PdfColorSpace colorSpace, ReadOnlySpan<double> components)
    {
        if (colorSpace.Kind != PdfColorSpace.Family.Lab)
        {
            return ColorFromComponentSpan(colorSpace, components);
        }

        var range = colorSpace.LabRange;
        return LabToColor(
            colorSpace,
            Clamp01(components[0]) * 100.0,
            range[0] + (Clamp01(components[1]) * (range[1] - range[0])),
            range[2] + (Clamp01(components[2]) * (range[3] - range[2])));
    }

    private static Rgba32 GrayToColor(double gray)
    {
        var value = ComponentToByte(gray);
        return new Rgba32(value, value, value, 255);
    }

    private static Rgba32 RgbToColor(double red, double green, double blue) =>
        new(ComponentToByte(red), ComponentToByte(green), ComponentToByte(blue), 255);

    private static Rgba32 CmykToColor(double cyan, double magenta, double yellow, double black)
    {
        var c = Clamp01(cyan);
        var m = Clamp01(magenta);
        var y = Clamp01(yellow);
        var k = Clamp01(black);

        var red = 255.0 * (1 - c) * (1 - k);
        var green = 255.0 * (1 - m) * (1 - k);
        var blue = 255.0 * (1 - y) * (1 - k);

        return new Rgba32(ToByte(red), ToByte(green), ToByte(blue), 255);
    }

    /// <summary>
    ///     Converts an <c>/Indexed</c> color space's single raw palette-index component (not
    ///     normalized to <c>[0, 1]</c> - a raw index) into an opaque <see cref="Rgba32"/> color:
    ///     rounds and clamps the index into <c>[0, IndexedHival]</c> (rather than rejecting an
    ///     out-of-range index), looks up that entry's raw bytes in <c>IndexedPalette</c>,
    ///     normalizes each byte to <c>[0, 1]</c>, and recurses into <c>IndexedBase</c>'s own
    ///     <see cref="ColorFromComponents"/>.
    /// </summary>
    private static Rgba32 IndexedToColor(PdfColorSpace colorSpace, double rawIndex)
    {
        var index = Math.Clamp((int)Math.Round(rawIndex), 0, colorSpace.IndexedHival);
        var baseSpace = colorSpace.IndexedBase!;
        var componentCount = ComponentCount(baseSpace);
        var offset = index * componentCount;

        Span<double> components = componentCount <= MaxStackColorComponents ? stackalloc double[componentCount] : new double[componentCount];
        for (var i = 0; i < componentCount; i++)
        {
            components[i] = colorSpace.IndexedPalette[offset + i] / 255.0;
        }

        return ColorFromNormalizedSpan(baseSpace, components);
    }

    /// <summary>Converts a <c>[0, 1]</c> color component (clamped) into a <c>[0, 255]</c> byte value.</summary>
    private static byte ComponentToByte(double component) => ToByte(Clamp01(component) * 255.0);

    /// <summary>Rounds and clamps an already-<c>[0, 255]</c>-scaled value into a byte.</summary>
    private static byte ToByte(double value0To255) => (byte)Math.Clamp(Math.Round(value0To255), 0, 255);

    /// <summary>
    ///     Clamps a color component into <c>[0, 1]</c> rather than rejecting it - values outside
    ///     this range are common in slightly-out-of-spec real-world producers; clamping avoids
    ///     rejecting an otherwise well-formed page over a cosmetic overshoot.
    /// </summary>
    private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);
}
