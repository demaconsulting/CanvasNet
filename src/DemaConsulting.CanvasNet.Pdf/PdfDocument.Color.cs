using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Pdf;

public sealed partial class PdfDocument
{
    /// <summary>
    ///     The device color spaces this phase recognizes for the current fill/stroke color
    ///     (<see cref="GraphicsState.FillColorSpace"/>/<see cref="GraphicsState.StrokeColorSpace"/>)
    ///     and for an image XObject's <c>/ColorSpace</c> entry (<c>PdfDocument.Images.cs</c>).
    /// </summary>
    /// <remarks>
    ///     Every other PDF color space (<c>Indexed</c>, <c>Separation</c>, <c>DeviceN</c>,
    ///     <c>ICCBased</c>, <c>CalRGB</c>, <c>CalGray</c>, <c>Lab</c>, and patterns) is out of this
    ///     phase's scope and is rejected with <see cref="UnsupportedImageFeatureException"/> - a
    ///     well-formed but unsupported color space, not a malformed one.
    /// </remarks>
    private enum PdfColorSpaceKind
    {
        /// <summary>A single gray component in <c>[0, 1]</c> (<c>0</c> = black, <c>1</c> = white).</summary>
        DeviceGray,

        /// <summary>Three additive red/green/blue components, each in <c>[0, 1]</c>.</summary>
        DeviceRGB,

        /// <summary>Four subtractive cyan/magenta/yellow/black components, each in <c>[0, 1]</c>.</summary>
        DeviceCMYK,
    }

    /// <summary>Handles the <c>g gray</c> operator: sets the fill color/space to <c>DeviceGray</c>.</summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> does not contain exactly 1 number.
    /// </exception>
    private void OpSetGrayFill(IReadOnlyList<PdfObject> operands) =>
        SetFillColor(PdfColorSpaceKind.DeviceGray, RequireNumbers(operands, "g", 1));

    /// <summary>Handles the <c>G gray</c> operator: sets the stroke color/space to <c>DeviceGray</c>.</summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> does not contain exactly 1 number.
    /// </exception>
    private void OpSetGrayStroke(IReadOnlyList<PdfObject> operands) =>
        SetStrokeColor(PdfColorSpaceKind.DeviceGray, RequireNumbers(operands, "G", 1));

    /// <summary>Handles the <c>r g b rg</c> operator: sets the fill color/space to <c>DeviceRGB</c>.</summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> does not contain exactly 3 numbers.
    /// </exception>
    private void OpSetRgbFill(IReadOnlyList<PdfObject> operands) =>
        SetFillColor(PdfColorSpaceKind.DeviceRGB, RequireNumbers(operands, "rg", 3));

    /// <summary>Handles the <c>r g b RG</c> operator: sets the stroke color/space to <c>DeviceRGB</c>.</summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> does not contain exactly 3 numbers.
    /// </exception>
    private void OpSetRgbStroke(IReadOnlyList<PdfObject> operands) =>
        SetStrokeColor(PdfColorSpaceKind.DeviceRGB, RequireNumbers(operands, "RG", 3));

    /// <summary>Handles the <c>c m y k k</c> operator: sets the fill color/space to <c>DeviceCMYK</c>.</summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> does not contain exactly 4 numbers.
    /// </exception>
    private void OpSetCmykFill(IReadOnlyList<PdfObject> operands) =>
        SetFillColor(PdfColorSpaceKind.DeviceCMYK, RequireNumbers(operands, "k", 4));

    /// <summary>Handles the <c>c m y k K</c> operator: sets the stroke color/space to <c>DeviceCMYK</c>.</summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> does not contain exactly 4 numbers.
    /// </exception>
    private void OpSetCmykStroke(IReadOnlyList<PdfObject> operands) =>
        SetStrokeColor(PdfColorSpaceKind.DeviceCMYK, RequireNumbers(operands, "K", 4));

    /// <summary>
    ///     Handles the <c>/name cs</c> operator: sets the current fill color space, resetting the
    ///     fill color to the PDF specification's own documented "reset to black" rule.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> is not exactly 1 <c>Name</c> operand.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when the named color space is not <c>DeviceGray</c>/<c>DeviceRGB</c>/
    ///     <c>DeviceCMYK</c>, whether resolved directly or via <c>/Resources/ColorSpace</c>.
    /// </exception>
    private void OpSetColorSpaceFill(IReadOnlyList<PdfObject> operands)
    {
        _gs.FillColorSpace = ResolveColorSpaceOperand(operands, "cs");
        _gs.FillColor = new Rgba32(0, 0, 0, 255);
    }

    /// <summary>
    ///     Handles the <c>/name CS</c> operator: sets the current stroke color space, resetting
    ///     the stroke color to the PDF specification's own documented "reset to black" rule.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> is not exactly 1 <c>Name</c> operand.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when the named color space is not <c>DeviceGray</c>/<c>DeviceRGB</c>/
    ///     <c>DeviceCMYK</c>, whether resolved directly or via <c>/Resources/ColorSpace</c>.
    /// </exception>
    private void OpSetColorSpaceStroke(IReadOnlyList<PdfObject> operands)
    {
        _gs.StrokeColorSpace = ResolveColorSpaceOperand(operands, "CS");
        _gs.StrokeColor = new Rgba32(0, 0, 0, 255);
    }

    /// <summary>
    ///     Handles the <c>sc</c>/<c>scn</c> operators: sets the fill color from its current color
    ///     space's required component count.
    /// </summary>
    /// <param name="operands">The operator's accumulated operand stack.</param>
    /// <param name="operatorName">The operator name (<c>sc</c> or <c>scn</c>), for exception messages.</param>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> does not contain exactly
    ///     <c>ComponentCount(_gs.FillColorSpace)</c> numbers.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when the last operand is a pattern name (<c>scn</c>'s <c>/Pattern</c> form).
    /// </exception>
    private void OpSetColorFill(IReadOnlyList<PdfObject> operands, string operatorName) =>
        SetFillColor(_gs.FillColorSpace, RequireColorComponents(operands, operatorName, _gs.FillColorSpace));

    /// <summary>
    ///     Handles the <c>SC</c>/<c>SCN</c> operators: sets the stroke color from its current
    ///     color space's required component count.
    /// </summary>
    /// <param name="operands">The operator's accumulated operand stack.</param>
    /// <param name="operatorName">The operator name (<c>SC</c> or <c>SCN</c>), for exception messages.</param>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> does not contain exactly
    ///     <c>ComponentCount(_gs.StrokeColorSpace)</c> numbers.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when the last operand is a pattern name (<c>SCN</c>'s <c>/Pattern</c> form).
    /// </exception>
    private void OpSetColorStroke(IReadOnlyList<PdfObject> operands, string operatorName) =>
        SetStrokeColor(_gs.StrokeColorSpace, RequireColorComponents(operands, operatorName, _gs.StrokeColorSpace));

    /// <summary>
    ///     Validates a <c>sc</c>/<c>SC</c>/<c>scn</c>/<c>SCN</c> operand list, rejecting a
    ///     trailing pattern-name operand and requiring exactly the current color space's own
    ///     component count of numeric operands.
    /// </summary>
    private static double[] RequireColorComponents(IReadOnlyList<PdfObject> operands, string operatorName, PdfColorSpaceKind colorSpace)
    {
        if (operands.Count > 0 && operands[^1].Kind == PdfKind.Name)
        {
            throw new UnsupportedImageFeatureException(
                "pdf-pattern-color",
                $"Operator '{operatorName}' with a /Pattern color space name is not supported.");
        }

        return RequireNumbers(operands, operatorName, ComponentCount(colorSpace));
    }

    /// <summary>Sets the current fill color/space from raw color-component values.</summary>
    private void SetFillColor(PdfColorSpaceKind colorSpace, IReadOnlyList<double> components)
    {
        _gs.FillColorSpace = colorSpace;
        _gs.FillColor = ColorFromComponents(colorSpace, components);
    }

    /// <summary>Sets the current stroke color/space from raw color-component values.</summary>
    private void SetStrokeColor(PdfColorSpaceKind colorSpace, IReadOnlyList<double> components)
    {
        _gs.StrokeColorSpace = colorSpace;
        _gs.StrokeColor = ColorFromComponents(colorSpace, components);
    }

    /// <summary>
    ///     Validates a <c>cs</c>/<c>CS</c> operand list (exactly 1 <c>Name</c> operand) and
    ///     resolves it to a <see cref="PdfColorSpaceKind"/>.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> is not exactly 1 <c>Name</c> operand.
    /// </exception>
    private PdfColorSpaceKind ResolveColorSpaceOperand(IReadOnlyList<PdfObject> operands, string operatorName)
    {
        if (operands.Count != 1 || operands[0].Kind != PdfKind.Name)
        {
            throw new InvalidDataException(
                $"Operator '{operatorName}' requires exactly 1 name operand; got {operands.Count}.");
        }

        return ResolveColorSpaceByName(operands[0].Text);
    }

    /// <summary>
    ///     Resolves a color-space name to a <see cref="PdfColorSpaceKind"/>: the three device
    ///     names resolve directly; any other name is looked up in the current page's
    ///     <c>/Resources/ColorSpace</c> dictionary and the resolved value is classified via
    ///     <see cref="ResolveColorSpaceValue"/>.
    /// </summary>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when the name is not one of the three device names and either cannot be
    ///     resolved via <c>/Resources/ColorSpace</c> at all, or resolves to an unsupported color
    ///     space (<c>Indexed</c>/<c>Separation</c>/<c>DeviceN</c>/<c>ICCBased</c>/<c>CalRGB</c>/
    ///     <c>CalGray</c>/<c>Lab</c>/anything else).
    /// </exception>
    private PdfColorSpaceKind ResolveColorSpaceByName(string name)
    {
        switch (name)
        {
            case "DeviceGray":
                return PdfColorSpaceKind.DeviceGray;
            case "DeviceRGB":
                return PdfColorSpaceKind.DeviceRGB;
            case "DeviceCMYK":
                return PdfColorSpaceKind.DeviceCMYK;
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

        return ResolveColorSpaceValue(Resolve(entry));
    }

    /// <summary>
    ///     Classifies an already-resolved color-space value (a <c>Name</c> or an <c>Array</c>,
    ///     for example an image XObject's own <c>/ColorSpace</c> entry) into a
    ///     <see cref="PdfColorSpaceKind"/>.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="value"/> is neither a <c>Name</c> nor an <c>Array</c>.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when the value names/represents a color space other than <c>DeviceGray</c>/
    ///     <c>DeviceRGB</c>/<c>DeviceCMYK</c>.
    /// </exception>
    private PdfColorSpaceKind ResolveColorSpaceValue(PdfObject value)
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
            throw new UnsupportedImageFeatureException(
                $"pdf-colorspace-{name}",
                $"Color space '{name}' is not supported.");
        }

        throw new InvalidDataException("Color space value must be a name or an array.");
    }

    /// <summary>Gets the number of numeric color components a color space requires.</summary>
    private static int ComponentCount(PdfColorSpaceKind colorSpace) => colorSpace switch
    {
        PdfColorSpaceKind.DeviceGray => 1,
        PdfColorSpaceKind.DeviceRGB => 3,
        PdfColorSpaceKind.DeviceCMYK => 4,
        _ => throw new InvalidOperationException($"Unreachable: unrecognized color space {colorSpace}."),
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
    ///     documented "device color, no color management" scope.
    /// </remarks>
    private static Rgba32 ColorFromComponents(PdfColorSpaceKind colorSpace, IReadOnlyList<double> components) =>
        colorSpace switch
        {
            PdfColorSpaceKind.DeviceGray => GrayToColor(components[0]),
            PdfColorSpaceKind.DeviceRGB => RgbToColor(components[0], components[1], components[2]),
            PdfColorSpaceKind.DeviceCMYK => CmykToColor(components[0], components[1], components[2], components[3]),
            _ => throw new InvalidOperationException($"Unreachable: unrecognized color space {colorSpace}."),
        };

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
