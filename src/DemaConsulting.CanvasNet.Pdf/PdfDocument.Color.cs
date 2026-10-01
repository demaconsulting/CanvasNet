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
    ///     three device color spaces, plus <c>/ICCBased</c> (resolved to a device color space via
    ///     <see cref="ResolveIccBasedColorSpace"/>) and <c>/Indexed</c> (a palette lookup over any
    ///     of the above, resolved via <see cref="ResolveIndexedColorSpace"/>).
    /// </summary>
    /// <remarks>
    ///     Every other PDF color space (<c>Separation</c>, <c>DeviceN</c>, <c>CalRGB</c>,
    ///     <c>CalGray</c>, <c>Lab</c>, and patterns) is out of this phase's scope and is rejected
    ///     with <see cref="UnsupportedImageFeatureException"/> - a well-formed but unsupported
    ///     color space, not a malformed one. Within the two newly supported color spaces, two
    ///     further edge cases are likewise rejected with <see cref="UnsupportedImageFeatureException"/>:
    ///     an <c>/ICCBased</c> stream whose <c>/N</c> is not <c>1</c>/<c>3</c>/<c>4</c> and has no
    ///     usable <c>/Alternate</c>, and an <c>/Indexed</c> color space whose base color space is
    ///     itself unsupported.
    /// </remarks>
    private sealed class PdfColorSpace
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
        }

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
    ///     Thrown when the named color space is not supported (see <see cref="PdfColorSpace"/>'s
    ///     own remarks), whether resolved directly or via <c>/Resources/ColorSpace</c>.
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
    private static double[] RequireColorComponents(IReadOnlyList<PdfObject> operands, string operatorName, PdfColorSpace colorSpace)
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
    private void SetFillColor(PdfColorSpace colorSpace, IReadOnlyList<double> components)
    {
        _gs.FillColorSpace = colorSpace;
        _gs.FillColor = ColorFromComponents(colorSpace, components);
    }

    /// <summary>Sets the current stroke color/space from raw color-component values.</summary>
    private void SetStrokeColor(PdfColorSpace colorSpace, IReadOnlyList<double> components)
    {
        _gs.StrokeColorSpace = colorSpace;
        _gs.StrokeColor = ColorFromComponents(colorSpace, components);
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
    ///     space (<c>Separation</c>/<c>DeviceN</c>/<c>CalRGB</c>/<c>CalGray</c>/<c>Lab</c>/
    ///     anything else, or an <c>/ICCBased</c>/<c>/Indexed</c> edge case documented on
    ///     <see cref="PdfColorSpace"/>).
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
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="value"/> is neither a <c>Name</c> nor an <c>Array</c>, or
    ///     propagated from <see cref="ResolveIndexedColorSpace"/> for a malformed <c>/Indexed</c>
    ///     array.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when the value names/represents a color space other than <c>DeviceGray</c>/
    ///     <c>DeviceRGB</c>/<c>DeviceCMYK</c>/<c>ICCBased</c>/<c>Indexed</c>, or propagated from
    ///     <see cref="ResolveIccBasedColorSpace"/>/<see cref="ResolveIndexedColorSpace"/> for the
    ///     documented edge cases of those two color spaces.
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
            }

            throw new UnsupportedImageFeatureException(
                $"pdf-colorspace-{name}",
                $"Color space '{name}' is not supported.");
        }

        throw new InvalidDataException("Color space value must be a name or an array.");
    }

    /// <summary>
    ///     Resolves an <c>/ICCBased</c> color-space array (<c>[/ICCBased streamRef]</c>): prefers
    ///     the referenced stream's <c>/Alternate</c> entry when present and itself resolves to a
    ///     supported color space, else maps the stream's <c>/N</c> component count
    ///     (<c>1</c>/<c>3</c>/<c>4</c>) to <c>DeviceGray</c>/<c>DeviceRGB</c>/<c>DeviceCMYK</c>.
    /// </summary>
    /// <remarks>
    ///     An <c>/Alternate</c> whose resolution throws <see cref="UnsupportedImageFeatureException"/>
    ///     (absent, or itself naming an unsupported color space) is deliberately treated as "no
    ///     usable /Alternate", falling back to the <c>/N</c>-implied device space instead of
    ///     propagating - this is the PDF specification's own documented fallback semantics for
    ///     <c>/ICCBased</c>, not an oversight. A structurally malformed <c>/Alternate</c> (which
    ///     throws <see cref="InvalidDataException"/>) is not caught, and propagates unmodified.
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

        var alternate = stream.Get("Alternate");
        if (alternate is not null)
        {
            try
            {
                return ResolveColorSpaceValue(Resolve(alternate));
            }
            catch (UnsupportedImageFeatureException)
            {
                // No usable /Alternate - fall through to /N-based resolution.
            }
        }

        var nEntry = stream.Get("N");
        var resolvedN = nEntry is null ? null : Resolve(nEntry);
        var n = resolvedN is { Kind: PdfKind.Number } ? (int)resolvedN.Number : (int?)null;
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
    ///     <c>/Hival</c> is not a non-negative number, when the lookup table is neither a string
    ///     nor a stream, or when the resolved palette is shorter than
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
        if (hivalValue.Kind != PdfKind.Number || hivalValue.Number < 0)
        {
            throw new InvalidDataException("Color space '/Indexed' /Hival must be a non-negative number.");
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
    private static int ComponentCount(PdfColorSpace colorSpace) => colorSpace.Kind switch
    {
        PdfColorSpace.Family.DeviceGray => 1,
        PdfColorSpace.Family.DeviceRGB => 3,
        PdfColorSpace.Family.DeviceCMYK => 4,
        PdfColorSpace.Family.Indexed => 1,
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
            _ => throw new InvalidOperationException($"Unreachable: unrecognized color space {colorSpace.Kind}."),
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

        var components = new double[componentCount];
        for (var i = 0; i < componentCount; i++)
        {
            components[i] = colorSpace.IndexedPalette[offset + i] / 255.0;
        }

        return ColorFromComponents(baseSpace, components);
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
