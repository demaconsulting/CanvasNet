using System.IO.Compression;
using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Pdf.Tests;

/// <summary>
///     Tests of the PostScript calculator (<c>/FunctionType 4</c>), multi-input sampled functions and
///     the <c>/Separation</c> / <c>/DeviceN</c> color spaces (operators, images, <c>/Indexed</c>
///     palettes and shadings). Expected colors are derived from the specification formulas, or by
///     painting the same color through the alternate space directly; no other viewer's output is used.
/// </summary>
public class PdfDocumentTintTests
{
    private static readonly Rgba32 Background = new(1, 2, 3, 255);

    private const string SpotCmyk =
        "[/Separation /Spot /DeviceCMYK << /FunctionType 2 /Domain [0 1] /C0 [0 0 0 0] /C1 [0 1 1 0] /N 1 >>]";

    // ---- helpers ----

    private static byte[] Stream(string dictionary, byte[] data)
    {
        var buffer = new List<byte>();
        buffer.AddRange(Encoding.ASCII.GetBytes(dictionary + "\nstream\n"));
        buffer.AddRange(data);
        buffer.AddRange("\nendstream"u8.ToArray());
        return [.. buffer];
    }

    private static byte[] PsFunction(string domain, string range, string program) =>
        Stream($"<< /FunctionType 4 /Domain {domain} /Range {range} /Length {program.Length} >>", Encoding.ASCII.GetBytes(program));

    private static byte[] BuildPdf(string content, string resources, params byte[][] extraBodies)
    {
        var contentBytes = Encoding.ASCII.GetBytes(content);
        var bodies = new List<byte[]>
        {
            "<< /Type /Catalog /Pages 2 0 R >>"u8.ToArray(),
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>"u8.ToArray(),
            Encoding.ASCII.GetBytes(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Contents 4 0 R /Resources << {resources} >> >>"),
            Stream($"<< /Length {contentBytes.Length} >>", contentBytes),
        };
        bodies.AddRange(extraBodies);

        var buffer = new List<byte>();
        buffer.AddRange("%PDF-1.7\n"u8.ToArray());
        var offsets = new List<int>();
        for (var i = 0; i < bodies.Count; i++)
        {
            offsets.Add(buffer.Count);
            buffer.AddRange(Encoding.ASCII.GetBytes($"{i + 1} 0 obj\n"));
            buffer.AddRange(bodies[i]);
            buffer.AddRange("\nendobj\n"u8.ToArray());
        }

        var xrefOffset = buffer.Count;
        buffer.AddRange(Encoding.ASCII.GetBytes($"xref\n0 {bodies.Count + 1}\n0000000000 65535 f \n"));
        foreach (var offset in offsets)
        {
            buffer.AddRange(Encoding.ASCII.GetBytes($"{offset:D10} 00000 n \n"));
        }

        buffer.AddRange(Encoding.ASCII.GetBytes(
            $"trailer\n<< /Size {bodies.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n"));
        return [.. buffer];
    }

    private static byte[] ImageObject(int width, int height, string colorSpace, byte[] samples, string extra = "")
    {
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(samples);
        }

        var data = compressed.ToArray();
        return Stream(
            $"<< /Type /XObject /Subtype /Image /Width {width} /Height {height} /ColorSpace {colorSpace} " +
            $"/BitsPerComponent 8 /Filter /FlateDecode {extra} /Length {data.Length} >>",
            data);
    }

    private static Surface Render(byte[] pdf)
    {
        using var document = PdfDocument.Open(new MemoryStream(pdf));
        return document.Render(0, 100, 100, new PdfRenderOptions { BackgroundColor = Background });
    }

    /// <summary>Paints an 80 x 80 square and returns the center pixel.</summary>
    private static Rgba32 PaintCenter(string colorSpaceEntry, string paint, params byte[][] extra)
    {
        var pdf = BuildPdf(paint + " 10 10 80 80 re f", $"/ColorSpace << /CS0 {colorSpaceEntry} >>", extra);
        using var surface = Render(pdf);
        return surface[50, 50];
    }

    private static void AssertNear(Rgba32 expected, Rgba32 actual, int tolerance)
    {
        Assert.InRange(actual.R, Math.Max(0, expected.R - tolerance), Math.Min(255, expected.R + tolerance));
        Assert.InRange(actual.G, Math.Max(0, expected.G - tolerance), Math.Min(255, expected.G + tolerance));
        Assert.InRange(actual.B, Math.Max(0, expected.B - tolerance), Math.Min(255, expected.B + tolerance));
        Assert.Equal(255, actual.A);
    }

    private static double[] EvalPs(string program, double input, int outputs)
    {
        var range = string.Join(' ', Enumerable.Repeat("-1000 1000", outputs));
        var bytes = BuildPdf("BT ET", string.Empty, PsFunction("[-1000 1000]", $"[{range}]", program));
        using var document = PdfDocument.Open(new MemoryStream(bytes));
        return document.ResolveFunctionGeneric(PdfDocument.PdfObject.FromReference(5, 0)).Evaluate(input);
    }

    private static Exception? ResolvePsError(string program)
    {
        var bytes = BuildPdf("BT ET", string.Empty, PsFunction("[-1000 1000]", "[-1000 1000]", program));
        using var document = PdfDocument.Open(new MemoryStream(bytes));
        return Record.Exception(
            () => document.ResolveFunctionGeneric(PdfDocument.PdfObject.FromReference(5, 0)).Evaluate(1));
    }

    // ---- PostScript operators ----

    /// <summary>Proves the PostScript arithmetic, rounding and conversion operators follow the specification.</summary>
    [Theory]
    [InlineData("pop 7 3 idiv", 2)]
    [InlineData("pop -7 3 idiv", -2)]
    [InlineData("pop 7 3 mod", 1)]
    [InlineData("pop -7 3 mod", -1)]
    [InlineData("pop 1 1 atan", 45)]
    [InlineData("pop -1 -1 atan", 225)]
    [InlineData("pop 1 0 atan", 90)]
    [InlineData("pop 2.5 round", 3)]
    [InlineData("pop -2.5 round", -2)]
    [InlineData("pop 3.9 cvi", 3)]
    [InlineData("pop -3.9 cvi", -3)]
    [InlineData("pop -1.7 truncate", -1)]
    [InlineData("pop 1.2 ceiling", 2)]
    [InlineData("pop 1.8 floor", 1)]
    [InlineData("pop 5 dup mul", 25)]
    [InlineData("pop 2 3 exp", 8)]
    [InlineData("pop 100 log", 2)]
    [InlineData("pop 9 sqrt", 3)]
    [InlineData("pop 0 cos", 1)]
    [InlineData("pop 90 sin", 1)]
    [InlineData("pop -5 abs", 5)]
    [InlineData("pop 6 3 and", 2)]
    [InlineData("pop 1 3 bitshift", 8)]
    [InlineData("pop 16 -2 bitshift", 4)]
    [InlineData("pop 3 4 gt { 10 } { 20 } ifelse", 20)]
    [InlineData("pop 3 4 lt { 10 } if 30", 30)]
    [InlineData("pop true false or { 1 } { 2 } ifelse", 1)]
    public void PdfDocument_Functions_Type4_Operator_ProducesSpecResult(string program, double expected)
    {
        // Act
        var result = EvalPs("{ " + program + " }", 0, 1);

        // Assert
        Assert.Equal(expected, result[0], 6);
    }

    /// <summary>Proves the stack-manipulation operators (exch, roll, copy, index) behave per the specification.</summary>
    [Theory]
    [InlineData("pop 1 2 exch", new double[] { 2, 1 })]
    [InlineData("pop 1 2 3 3 1 roll", new double[] { 3, 1, 2 })]
    [InlineData("pop 1 2 3 3 -1 roll", new double[] { 2, 3, 1 })]
    [InlineData("pop 1 2 2 copy", new double[] { 1, 2, 1, 2 })]
    [InlineData("pop 1 2 3 2 index", new double[] { 1, 2, 3, 1 })]
    public void PdfDocument_Functions_Type4_StackOperators_ProduceSpecStack(string program, double[] expected)
    {
        // Act
        var result = EvalPs("{ " + program + " }", 0, expected.Length);

        // Assert
        Assert.Equal(expected.Length, result.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], result[i], 6);
        }
    }

    /// <summary>Proves that the PostScript function output is clamped to its <c>/Range</c>.</summary>
    [Fact]
    public void PdfDocument_Functions_Type4_OutputBeyondRange_IsClamped()
    {
        // Act
        var result = EvalPs("{ pop 5000 }", 0, 1);

        // Assert
        Assert.Equal(1000, result[0], 6);
    }

    /// <summary>Proves that malformed or abusive PostScript programs fail closed with <see cref="InvalidDataException"/>.</summary>
    [Theory]
    [InlineData("{ pop add }")]
    [InlineData("{ pop 1 0 idiv }")]
    [InlineData("{ pop -1 sqrt }")]
    [InlineData("{ pop bogus }")]
    [InlineData("{ pop 1 2 add")]
    [InlineData("{ pop 1 { 2 } }")]
    [InlineData("{ pop true { 1 } if 2 }}")]
    public void PdfDocument_Functions_Type4_MalformedProgram_ThrowsInvalidDataException(string program)
    {
        // Act
        var error = ResolvePsError(program);

        // Assert
        Assert.IsType<InvalidDataException>(error);
    }

    /// <summary>Proves that exceeding the stack, nesting, instruction or execution caps throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PdfDocument_Functions_Type4_ResourceCaps_ThrowInvalidDataException()
    {
        // Arrange
        var stackOverflow = "{ pop " + string.Concat(Enumerable.Repeat("1 ", 150)) + "}";
        var nested = "{ pop " + string.Concat(Enumerable.Repeat("true { ", 20)) + "1" + string.Concat(Enumerable.Repeat(" } if", 20)) + " 1 }";
        var tooLong = "{ pop " + string.Concat(Enumerable.Repeat("1 pop ", 5000)) + "1 }";

        // Act & Assert
        Assert.IsType<InvalidDataException>(ResolvePsError(stackOverflow));
        Assert.IsType<InvalidDataException>(ResolvePsError(nested));
        Assert.IsType<InvalidDataException>(ResolvePsError(tooLong));
    }

    /// <summary>Proves that a Type 4 function that is not a stream is rejected.</summary>
    [Fact]
    public void PdfDocument_Functions_Type4_NotAStream_ThrowsInvalidDataException()
    {
        // Arrange
        var bytes = BuildPdf(
            "BT ET", string.Empty, "<< /FunctionType 4 /Domain [0 1] /Range [0 1] >>"u8.ToArray());
        using var document = PdfDocument.Open(new MemoryStream(bytes));

        // Act & Assert
        Assert.Throws<InvalidDataException>(
            () => document.ResolveFunctionGeneric(PdfDocument.PdfObject.FromReference(5, 0)));
    }

    // ---- Separation ----

    /// <summary>Proves that a Separation with a Type 2 tint to CMYK equals painting the tinted CMYK directly.</summary>
    [Theory]
    [InlineData("1", "0 1 1 0 k")]
    [InlineData("0.5", "0 0.5 0.5 0 k")]
    [InlineData("0", "0 0 0 0 k")]
    public void PdfDocument_Tint_SeparationType2ToCmyk_MatchesDirectCmyk(string tint, string direct)
    {
        // Act
        var viaSeparation = PaintCenter(SpotCmyk, $"/CS0 cs {tint} sc");
        var viaCmyk = PaintCenter(SpotCmyk, direct);

        // Assert
        Assert.Equal(viaCmyk, viaSeparation);
    }

    /// <summary>Proves that selecting a Separation space initializes the tint to 1.0 (not black).</summary>
    [Fact]
    public void PdfDocument_Tint_SetColorSpace_InitialTintIsOne()
    {
        // Act
        var initial = PaintCenter(SpotCmyk, "/CS0 cs");
        var explicitOne = PaintCenter(SpotCmyk, "/CS0 cs 1 sc");

        // Assert
        Assert.Equal(explicitOne, initial);
    }

    /// <summary>Proves that stroke color operators accept Separation tints.</summary>
    [Fact]
    public void PdfDocument_Tint_SeparationStroke_MatchesDirectCmyk()
    {
        // Arrange
        var resources = $"/ColorSpace << /CS0 {SpotCmyk} >>";
        var viaSeparation = BuildPdf("/CS0 CS 1 SC 20 w 0 50 m 100 50 l S", resources);
        var viaCmyk = BuildPdf("0 1 1 0 K 20 w 0 50 m 100 50 l S", resources);

        // Act
        using var a = Render(viaSeparation);
        using var b = Render(viaCmyk);

        // Assert
        Assert.Equal(b[50, 50], a[50, 50]);
        Assert.NotEqual(Background, a[50, 50]);
    }

    /// <summary>Proves that a Separation with a Type 4 tint transform evaluates the program.</summary>
    [Fact]
    public void PdfDocument_Tint_SeparationType4ToRgb_EvaluatesProgram()
    {
        // Arrange: x -> (0.5x, 1-x, 0).
        var fn = PsFunction("[0 1]", "[0 1 0 1 0 1]", "{ dup 0.5 mul exch 1 exch sub 0 }");

        // Act
        var pixel = PaintCenter("[/Separation /Spot /DeviceRGB 5 0 R]", "/CS0 cs 0.5 sc", fn);

        // Assert: (0.25, 0.5, 0)
        AssertNear(new Rgba32(64, 128, 0, 255), pixel, 1);
    }

    /// <summary>Proves that Separation /None paints nothing.</summary>
    [Fact]
    public void PdfDocument_Tint_SeparationNone_LeavesBackgroundUntouched()
    {
        // Arrange
        var space = "[/Separation /None /DeviceCMYK << /FunctionType 2 /Domain [0 1] /C0 [0 0 0 0] /C1 [0 1 1 0] /N 1 >>]";

        // Act
        var pixel = PaintCenter(space, "/CS0 cs 1 sc");

        // Assert
        Assert.Equal(Background, pixel);
    }

    /// <summary>Proves that Separation /All is evaluated through its tint transform.</summary>
    [Fact]
    public void PdfDocument_Tint_SeparationAll_UsesTintTransform()
    {
        // Arrange
        var space = "[/Separation /All /DeviceGray << /FunctionType 2 /Domain [0 1] /C0 [1] /C1 [0] /N 1 >>]";

        // Act
        var pixel = PaintCenter(space, "/CS0 cs 1 sc");

        // Assert
        AssertNear(new Rgba32(0, 0, 0, 255), pixel, 0);
    }

    /// <summary>Proves that a tint transform with the wrong output arity is rejected.</summary>
    [Fact]
    public void PdfDocument_Tint_TransformOutputMismatch_ThrowsInvalidDataException()
    {
        // Arrange: 1 output for a 3-component alternate.
        var space = "[/Separation /Spot /DeviceRGB << /FunctionType 2 /Domain [0 1] /C0 [0] /C1 [1] /N 1 >>]";
        var bytes = BuildPdf("/CS0 cs 10 10 80 80 re f", $"/ColorSpace << /CS0 {space} >>");

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => Render(bytes));
    }

    /// <summary>Proves that unsupported alternate spaces (Indexed, Separation, DeviceN, Pattern) are reported as unsupported.</summary>
    [Theory]
    [InlineData("[/Indexed /DeviceRGB 1 <000000FFFFFF>]")]
    [InlineData("/Pattern")]
    public void PdfDocument_Tint_UnsupportedAlternate_ThrowsUnsupportedImageFeatureException(string alternate)
    {
        // Arrange
        var space = $"[/Separation /Spot {alternate} << /FunctionType 2 /Domain [0 1] /C0 [0] /C1 [1] /N 1 >>]";
        var bytes = BuildPdf("/CS0 cs 10 10 80 80 re f", $"/ColorSpace << /CS0 {space} >>");

        // Act & Assert
        Assert.Throws<UnsupportedImageFeatureException>(() => Render(bytes));
    }

    // ---- DeviceN ----

    /// <summary>Proves that a DeviceN with two colorants and a Type 4 tint evaluates both tints.</summary>
    [Fact]
    public void PdfDocument_Tint_DeviceNType4_EvaluatesBothTints()
    {
        // Arrange: (a, b) -> (a, b, 0).
        var fn = PsFunction("[0 1 0 1]", "[0 1 0 1 0 1]", "{ 0 }");

        // Act
        var pixel = PaintCenter("[/DeviceN [/A /B] /DeviceRGB 5 0 R]", "/CS0 cs 0.2 0.8 sc", fn);

        // Assert
        AssertNear(new Rgba32(51, 204, 0, 255), pixel, 1);
    }

    /// <summary>Proves that a DeviceN with a hand-built two-input Type 0 sampled function interpolates multilinearly.</summary>
    [Theory]
    [InlineData("0.5 0.5", 128, 128)]
    [InlineData("0.25 0.75", 64, 191)]
    [InlineData("1 0", 255, 0)]
    public void PdfDocument_Tint_DeviceNMultiInputSampled_InterpolatesMultilinearly(string tints, int r, int g)
    {
        // Arrange: 2x2 grid, first input varies fastest; R = a, G = b, B = 0.
        byte[] samples = [0, 0, 0, 255, 0, 0, 0, 255, 0, 255, 255, 0];
        var fn = Stream(
            $"<< /FunctionType 0 /Domain [0 1 0 1] /Range [0 1 0 1 0 1] /Size [2 2] /BitsPerSample 8 /Length {samples.Length} >>",
            samples);

        // Act
        var pixel = PaintCenter("[/DeviceN [/A /B] /DeviceRGB 5 0 R]", $"/CS0 cs {tints} sc", fn);

        // Assert
        AssertNear(new Rgba32((byte)r, (byte)g, 0, 255), pixel, 1);
    }

    /// <summary>Proves that a multi-input Type 0 sampled function with a fractional /Size entry (1.5 2) is rejected as malformed instead of being truncated to (1 2).</summary>
    [Fact]
    public void PdfDocument_Tint_DeviceNMultiInputSampledFractionalSize_ThrowsInvalidDataException()
    {
        // Arrange
        byte[] samples = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        var fn = Stream(
            $"<< /FunctionType 0 /Domain [0 1 0 1] /Range [0 1 0 1 0 1] /Size [1.5 2] /BitsPerSample 8 /Length {samples.Length} >>",
            samples);
        var bytes = BuildPdf(
            "/CS0 cs 0.5 0.5 sc 10 10 80 80 re f", "/ColorSpace << /CS0 [/DeviceN [/A /B] /DeviceRGB 5 0 R] >>", fn);

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() => Render(bytes));
        Assert.Contains("/Size", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves a DeviceN with more than eight colorants (beyond the old stack-scratch size) converts correctly through its tint transform.</summary>
    [Fact]
    public void PdfDocument_Tint_DeviceNTwelveColorants_ConvertsThroughTintTransform()
    {
        // Arrange: 12 colorants; the program drops the last nine so the first three become R G B.
        var fn = PsFunction(
            "[" + string.Concat(Enumerable.Repeat("0 1 ", 12)).Trim() + "]",
            "[0 1 0 1 0 1]",
            "{ pop pop pop pop pop pop pop pop pop }");
        var names = string.Join(' ', Enumerable.Range(1, 12).Select(i => $"/C{i}"));

        // Act
        var pixel = PaintCenter($"[/DeviceN [{names}] /DeviceRGB 5 0 R]", "/CS0 cs 0.2 0.8 0.5 0 0 0 0 0 0 0 0 0 sc", fn);

        // Assert
        AssertNear(new Rgba32(51, 204, 128, 255), pixel, 1);
    }

    /// <summary>Proves that DeviceN with all colorants /None paints nothing.</summary>
    [Fact]
    public void PdfDocument_Tint_DeviceNAllNone_LeavesBackgroundUntouched()
    {
        // Arrange
        var fn = PsFunction("[0 1 0 1]", "[0 1 0 1 0 1]", "{ 0 }");

        // Act
        var pixel = PaintCenter("[/DeviceN [/None /None] /DeviceRGB 5 0 R]", "/CS0 cs 1 1 sc", fn);

        // Assert
        Assert.Equal(Background, pixel);
    }

    /// <summary>Proves that a DeviceN with a wrong number of operands is rejected.</summary>
    [Fact]
    public void PdfDocument_Tint_DeviceNOperandCountMismatch_ThrowsInvalidDataException()
    {
        // Arrange
        var fn = PsFunction("[0 1 0 1]", "[0 1 0 1 0 1]", "{ 0 }");
        var bytes = BuildPdf(
            "/CS0 cs 1 sc 10 10 80 80 re f", "/ColorSpace << /CS0 [/DeviceN [/A /B] /DeviceRGB 5 0 R] >>", fn);

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => Render(bytes));
    }

    /// <summary>Proves that a DeviceN with more than the supported number of colorants is rejected.</summary>
    [Fact]
    public void PdfDocument_Tint_DeviceNTooManyColorants_ThrowsInvalidDataException()
    {
        // Arrange
        var names = string.Join(' ', Enumerable.Range(0, 40).Select(i => $"/C{i}"));
        var fn = PsFunction("[" + string.Concat(Enumerable.Repeat("0 1 ", 40)) + "]", "[0 1 0 1 0 1]", "{ pop 0 0 0 }");
        var bytes = BuildPdf(
            "/CS0 cs 10 10 80 80 re f", $"/ColorSpace << /CS0 [/DeviceN [{names}] /DeviceRGB 5 0 R] >>", fn);

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => Render(bytes));
    }

    // ---- Indexed over tint spaces / images ----

    /// <summary>Proves that an Indexed palette over a Separation base maps each index through the tint transform.</summary>
    [Fact]
    public void PdfDocument_Tint_IndexedOverSeparation_MatchesDirectCmyk()
    {
        // Arrange
        var indexed = $"[/Indexed {SpotCmyk} 1 <00FF>]";

        // Act
        var viaIndexed = PaintCenter(indexed, "/CS0 cs 1 sc");
        var direct = PaintCenter(SpotCmyk, "0 1 1 0 k");

        // Assert
        Assert.Equal(direct, viaIndexed);
    }

    /// <summary>Proves that an Indexed palette over a DeviceN base maps each entry through the tint transform.</summary>
    [Fact]
    public void PdfDocument_Tint_IndexedOverDeviceN_EvaluatesTransform()
    {
        // Arrange
        var fn = PsFunction("[0 1 0 1]", "[0 1 0 1 0 1]", "{ 0 }");

        // Act: palette entry 1 = (255, 0) -> RGB (1, 0, 0)
        var pixel = PaintCenter("[/Indexed [/DeviceN [/A /B] /DeviceRGB 5 0 R] 1 <0000FF00>]", "/CS0 cs 1 sc", fn);

        // Assert
        AssertNear(new Rgba32(255, 0, 0, 255), pixel, 1);
    }

    /// <summary>Proves that a one-component Separation image decodes through the tint transform.</summary>
    [Fact]
    public void PdfDocument_Tint_SeparationImage_MatchesDirectCmyk()
    {
        // Arrange
        var image = ImageObject(1, 1, SpotCmyk, [255]);
        var pdf = BuildPdf("q 80 0 0 80 10 10 cm /Im0 Do Q", "/XObject << /Im0 5 0 R >>", image);
        using var surface = Render(pdf);
        var direct = PaintCenter(SpotCmyk, "0 1 1 0 k");

        // Assert
        Assert.Equal(direct, surface[50, 50]);
    }

    /// <summary>Proves that a two-component DeviceN image decodes through the tint transform.</summary>
    [Fact]
    public void PdfDocument_Tint_DeviceNImage_EvaluatesTransform()
    {
        // Arrange
        var fn = PsFunction("[0 1 0 1]", "[0 1 0 1 0 1]", "{ 0 }");
        var image = ImageObject(1, 1, "[/DeviceN [/A /B] /DeviceRGB 5 0 R]", [51, 204]);
        var pdf = BuildPdf("q 80 0 0 80 10 10 cm /Im0 Do Q", "/XObject << /Im0 6 0 R >>", fn, image);

        // Act
        using var surface = Render(pdf);

        // Assert
        AssertNear(new Rgba32(51, 204, 0, 255), surface[50, 50], 1);
    }

    /// <summary>Proves that a Separation image cannot serve as a soft mask (only DeviceGray qualifies).</summary>
    [Fact]
    public void PdfDocument_Tint_SeparationImageAsSoftMask_ThrowsInvalidDataException()
    {
        // Arrange
        var mask = ImageObject(1, 1, SpotCmyk, [255]);
        var image = ImageObject(1, 1, "/DeviceRGB", [255, 0, 0], "/SMask 5 0 R");
        var pdf = BuildPdf("q 80 0 0 80 10 10 cm /Im0 Do Q", "/XObject << /Im0 6 0 R >>", mask, image);

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => Render(pdf));
    }

    // ---- Shadings ----

    /// <summary>Proves that an axial shading in a Separation color space is evaluated through the tint transform.</summary>
    [Fact]
    public void PdfDocument_Tint_AxialShadingInSeparation_MatchesDirectCmyk()
    {
        // Arrange: 5 = shading, 6 = function t -> tint (identity).
        var shading =
            Encoding.ASCII.GetBytes($"<< /ShadingType 2 /ColorSpace {SpotCmyk} /Coords [0 0 100 0] /Function 6 0 R /Extend [true true] >>");
        var fn = "<< /FunctionType 2 /Domain [0 1] /C0 [0] /C1 [1] /N 1 >>"u8.ToArray();
        var pdf = BuildPdf("/Sh1 sh", "/Shading << /Sh1 5 0 R >>", shading, fn);

        // Act
        using var surface = Render(pdf);
        var direct = PaintCenter(SpotCmyk, "0 0.5 0.5 0 k");

        // Assert
        AssertNear(direct, surface[50, 50], 2);
    }

    /// <summary>Proves that an axial shading in an Indexed color space is rejected.</summary>
    [Fact]
    public void PdfDocument_Tint_AxialShadingInIndexed_ThrowsUnsupportedImageFeatureException()
    {
        // Arrange
        var shading =
            "<< /ShadingType 2 /ColorSpace [/Indexed /DeviceRGB 1 <000000FFFFFF>] /Coords [0 0 100 0] /Function 6 0 R >>"u8.ToArray();
        var fn = "<< /FunctionType 2 /Domain [0 1] /C0 [0] /C1 [1] /N 1 >>"u8.ToArray();
        var pdf = BuildPdf("/Sh1 sh", "/Shading << /Sh1 5 0 R >>", shading, fn);

        // Act & Assert
        var ex = Assert.Throws<UnsupportedImageFeatureException>(() => Render(pdf));
        Assert.Equal("pdf-shading-colorspace-Indexed", ex.Feature);
    }

    /// <summary>Proves that a Type 4 function drives an axial shading.</summary>
    [Fact]
    public void PdfDocument_Tint_AxialShadingWithType4Function_EvaluatesProgram()
    {
        // Arrange: t -> (t, 0, 0)
        var shading =
            "<< /ShadingType 2 /ColorSpace /DeviceRGB /Coords [0 0 100 0] /Function 6 0 R /Extend [true true] >>"u8.ToArray();
        var fn = PsFunction("[0 1]", "[0 1 0 1 0 1]", "{ 0 0 }");
        var pdf = BuildPdf("/Sh1 sh", "/Shading << /Sh1 5 0 R >>", shading, fn);

        // Act
        using var surface = Render(pdf);

        // Assert
        AssertNear(new Rgba32(128, 0, 0, 255), surface[50, 50], 3);
    }

    // ---- Bounded per-image tint evaluation ----

    private static PdfDocument.PdfColorSpace TwoColorantSpace(int inputs = 2)
    {
        var program = Encoding.ASCII.GetBytes(inputs == 2 ? "{ 0 }" : "{ }");
        var fn = new PdfDocument.PostScriptFunction(
            program,
            [.. Enumerable.Repeat(new[] { 0.0, 1.0 }, inputs).SelectMany(static a => a)],
            [0, 1, 0, 1, 0, 1]);
        return PdfDocument.PdfColorSpace.Tint(
            PdfDocument.PdfColorSpace.Family.DeviceN,
            [.. Enumerable.Range(0, inputs).Select(static i => "C" + i)],
            PdfDocument.PdfColorSpace.DeviceRGB,
            fn);
    }

    /// <summary>Proves that a noisy DeviceN image exhausts the evaluation budget deterministically (counted evaluations, no timing).</summary>
    [Fact]
    public void PdfDocument_Tint_NoisyDeviceNImage_ExceedsBudget_ThrowsUnsupportedImageFeatureException()
    {
        // Arrange: budget for exactly 10 uncached evaluations; every pixel distinct.
        var space = TwoColorantSpace();
        var cost = space.TintFunction!.EstimatedCost;
        var converter = new PdfDocument.TintImageConverter(space, cost * 10);
        var samples = new byte[2 * 100];
        for (var i = 0; i < 100; i++)
        {
            samples[2 * i] = (byte)i;
            samples[(2 * i) + 1] = (byte)(255 - i);
        }

        // Act
        var ex = Assert.Throws<UnsupportedImageFeatureException>(() =>
        {
            for (var i = 0; i < 100; i++)
            {
                converter.Convert(samples, 2 * i);
            }
        });

        // Assert
        Assert.Equal("pdf-image-tint-budget", ex.Feature);
        Assert.Equal(10, converter.EvaluationCount);
    }

    /// <summary>Proves that the memo cache yields identical pixels to uncached evaluation and bounds the evaluation count.</summary>
    [Fact]
    public void PdfDocument_Tint_DeviceNImageCache_MatchesUncachedConversion()
    {
        // Arrange: 5000 pixels drawn from a small noisy palette (many repeats).
        var space = TwoColorantSpace();
        var cached = new PdfDocument.TintImageConverter(space, long.MaxValue);
        var uncached = new PdfDocument.TintImageConverter(space, long.MaxValue);
        var random = new Random(1234);
        var samples = new byte[2 * 5000];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (byte)(random.Next(16) * 17);
        }

        // Act & Assert
        for (var i = 0; i < 5000; i++)
        {
            Assert.Equal(uncached.Convert(samples, 2 * i, useCache: false), cached.Convert(samples, 2 * i));
        }

        Assert.Equal(5000, uncached.EvaluationCount);
        Assert.Equal(cached.CacheCount, cached.EvaluationCount);
        Assert.True(cached.EvaluationCount <= 256);
    }

    /// <summary>Proves that the memo cache never grows past its cap while conversion stays correct.</summary>
    [Fact]
    public void PdfDocument_Tint_DeviceNImageCache_IsBounded()
    {
        // Arrange: 3 colorants, more distinct pixels than the cap.
        var space = TwoColorantSpace(3);
        var converter = new PdfDocument.TintImageConverter(space, long.MaxValue);
        var count = PdfDocument.MaxTintImageCacheEntries + 100;
        var samples = new byte[3 * count];
        for (var i = 0; i < count; i++)
        {
            samples[3 * i] = (byte)i;
            samples[(3 * i) + 1] = (byte)(i >> 8);
            samples[(3 * i) + 2] = (byte)(i >> 16);
        }

        // Act
        for (var i = 0; i < count; i++)
        {
            converter.Convert(samples, 3 * i);
        }

        // Assert
        Assert.Equal(PdfDocument.MaxTintImageCacheEntries, converter.CacheCount);
        Assert.Equal(count, converter.EvaluationCount);
    }

    /// <summary>Proves that the byte-aligned fast path of the sampled-function sample reader equals the bitwise path.</summary>
    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(32)]
    [InlineData(12)]
    public void PdfDocument_Tint_SampledFunctionReadSample_FastPathMatchesBitwise(int bitsPerSample)
    {
        // Arrange
        var bytes = new byte[bitsPerSample * 6];
        new Random(bitsPerSample).NextBytes(bytes);
        var function = new PdfDocument.MultiSampledFunction
        {
            Domain = [0, 1, 0, 1],
            Range = [0, 1],
            Encode = [0, 1, 0, 1],
            Decode = [0, 1],
            Size = [2, 3],
            BitsPerSample = bitsPerSample,
            Samples = bytes,
        };

        // Act & Assert
        for (var i = 0; i < 6 * 8 / bitsPerSample; i++)
        {
            Assert.Equal(function.ReadSampleBitwise((long)i * bitsPerSample), function.ReadSample(i));
        }
    }
}
