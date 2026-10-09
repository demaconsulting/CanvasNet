using DemaConsulting.CanvasNet.Codecs;

// cspell:ignore Gouraud Coons bicubic Bezier subdivision

namespace DemaConsulting.CanvasNet.Pdf.Tests;

/// <summary>
///     Unit-level tests for PDF mesh shadings (<c>/ShadingType 4</c> free-form Gouraud, <c>5</c>
///     lattice Gouraud, <c>6</c> Coons patch, <c>7</c> tensor patch), exercised through both the
///     <c>sh</c> operator and <c>/PatternType 2</c> shading patterns, plus fail-closed behavior
///     and DoS limits. Mesh streams are packed bit-exactly by the test-local helpers below.
/// </summary>
public class PdfDocumentMeshShadingTests
{
    /// <summary>The default decode array: coordinates 0-100 (the page size) and colors 0-1.</summary>
    private const string DefaultDecode = "[0 100 0 100 0 1 0 1 0 1]";

    /// <summary>The (i, j) control-net position of each point in a patch's stream order.</summary>
    private static readonly (int I, int J)[] PatchOrder =
    [
        (0, 0), (0, 1), (0, 2), (0, 3), (1, 3), (2, 3), (3, 3), (3, 2), (3, 1), (3, 0), (2, 0), (1, 0),
        (1, 1), (1, 2), (2, 2), (2, 1),
    ];

    private static readonly PdfRenderOptions Transparent = new() { BackgroundColor = new(0, 0, 0, 0) };

    /// <summary>The bit sizes a mesh stream is packed with.</summary>
    private readonly record struct Fmt(int CoordBits, int CompBits, int FlagBits);

    private static readonly Fmt Std = new(16, 8, 8);

    /// <summary>Packs values MSB-first into bytes.</summary>
    private sealed class BitPacker
    {
        private readonly List<byte> _bytes = [];
        private int _used;

        public void Write(ulong value, int bits)
        {
            for (var i = bits - 1; i >= 0; i--)
            {
                var bit = (int)((value >> i) & 1);
                if (_used == 0)
                {
                    _bytes.Add(0);
                }

                _bytes[^1] |= (byte)(bit << (7 - _used));
                _used = (_used + 1) % 8;
            }
        }

        public void Align() => _used = 0;

        public byte[] ToArray() => [.. _bytes];
    }

    private static void WriteCoord(BitPacker p, Fmt f, double v) =>
        p.Write((ulong)Math.Round(v / 100.0 * (Math.Pow(2, f.CoordBits) - 1)), f.CoordBits);

    private static void WriteComp(BitPacker p, Fmt f, double v) =>
        p.Write((ulong)Math.Round(v * (Math.Pow(2, f.CompBits) - 1)), f.CompBits);

    private static void WriteVertex(BitPacker p, Fmt f, int? flag, double x, double y, params double[] comps)
    {
        if (flag is { } fl)
        {
            p.Write((ulong)fl, f.FlagBits);
        }

        WriteCoord(p, f, x);
        WriteCoord(p, f, y);
        foreach (var c in comps)
        {
            WriteComp(p, f, c);
        }

        p.Align();
    }

    private static byte[] Type4(Fmt f, params (int Flag, double X, double Y, double R, double G, double B)[] vs)
    {
        var p = new BitPacker();
        foreach (var v in vs)
        {
            WriteVertex(p, f, v.Flag, v.X, v.Y, v.R, v.G, v.B);
        }

        return p.ToArray();
    }

    private static byte[] Type5(Fmt f, params (double X, double Y, double R, double G, double B)[] vs)
    {
        var p = new BitPacker();
        foreach (var v in vs)
        {
            WriteVertex(p, f, null, v.X, v.Y, v.R, v.G, v.B);
        }

        return p.ToArray();
    }

    private static void WritePatch(
        BitPacker p, Fmt f, int type, int flag, Func<int, int, (double X, double Y)> point, (double R, double G, double B)[] colors)
    {
        p.Write((ulong)flag, f.FlagBits);
        var count = type == 6 ? 12 : 16;
        for (var k = flag == 0 ? 0 : 4; k < count; k++)
        {
            var (x, y) = point(PatchOrder[k].I, PatchOrder[k].J);
            WriteCoord(p, f, x);
            WriteCoord(p, f, y);
        }

        foreach (var c in colors)
        {
            WriteComp(p, f, c.R);
            WriteComp(p, f, c.G);
            WriteComp(p, f, c.B);
        }

        p.Align();
    }

    private static readonly (double R, double G, double B)[] CornerColors =
    [
        (1, 0, 0), (0, 1, 0), (0, 0, 1), (1, 1, 1),
    ];

    private static (double X, double Y) Square(int i, int j, double x0 = 10, double y0 = 10, double size = 80) =>
        (x0 + (size * i / 3.0), y0 + (size * j / 3.0));

    private static byte[] SinglePatch(int type, Func<int, int, (double X, double Y)> point)
    {
        var p = new BitPacker();
        WritePatch(p, Std, type, 0, point, CornerColors);
        return p.ToArray();
    }

    /// <summary>Builds the shading dictionary entries (a null argument omits that entry).</summary>
    private static string Entries(
        int type,
        string? coordBits = "16",
        string? compBits = "8",
        string? flagBits = "8",
        string? decode = DefaultDecode,
        string colorSpace = "/DeviceRGB",
        string extra = "")
    {
        var s = $"/ShadingType {type} /ColorSpace {colorSpace}";
        s += coordBits is null ? string.Empty : $" /BitsPerCoordinate {coordBits}";
        s += compBits is null ? string.Empty : $" /BitsPerComponent {compBits}";
        s += flagBits is null || type == 5 ? string.Empty : $" /BitsPerFlag {flagBits}";
        s += decode is null ? string.Empty : $" /Decode {decode}";
        return s + " " + extra;
    }

    /// <summary>Builds a one-page PDF holding a mesh shading used via <c>sh</c> or as a shading pattern.</summary>
    private static byte[] BuildPdf(string entries, byte[] data, bool pattern, string? content = null, string patternEntries = "", string? shadingBody = null)
    {
        var functionNumber = pattern ? "7" : "6";
        var shading = shadingBody is null
            ? BuildStream(entries.Replace("{FN}", functionNumber, StringComparison.Ordinal), data)
            : System.Text.Encoding.ASCII.GetBytes(shadingBody);
        var function = BuildStream("/FunctionType 2 /Domain [0 1] /C0 [0 0 0] /C1 [1 1 1] /N 1", []);
        string resources;
        List<byte[]> objects;
        if (pattern)
        {
            objects =
            [
                System.Text.Encoding.ASCII.GetBytes($"<< /PatternType 2 /Shading 6 0 R {patternEntries} >>"),
                shading,
                function,
            ];
            resources = "/Pattern << /P1 5 0 R >>";
            content ??= "/Pattern cs /P1 scn 0 0 100 100 re f";
        }
        else
        {
            objects = [shading, function];
            resources = "/Shading << /Sh1 5 0 R >>";
            content ??= "/Sh1 sh";
        }

        return BuildSinglePage(content, resources, objects);
    }

    private static byte[] BuildStream(string entries, byte[] data)
    {
        var header = System.Text.Encoding.ASCII.GetBytes($"<< {entries} /Length {data.Length} >>\nstream\n");
        return [.. header, .. data, .. "\nendstream"u8.ToArray()];
    }

    private static byte[] BuildSinglePage(string content, string resources, List<byte[]> extraObjects)
    {
        var contentBytes = System.Text.Encoding.ASCII.GetBytes(content);
        var bodies = new List<byte[]>
        {
            "<< /Type /Catalog /Pages 2 0 R >>"u8.ToArray(),
            "<< /Type /Pages /Kids [3 0 R] /Count 1 /MediaBox [0 0 100 100] >>"u8.ToArray(),
            System.Text.Encoding.ASCII.GetBytes($"<< /Type /Page /Parent 2 0 R /Contents 4 0 R /Resources << {resources} >> >>"),
            BuildStream(string.Empty, contentBytes),
        };
        bodies.AddRange(extraObjects);

        using var buffer = new MemoryStream();
        void Write(byte[] b) => buffer.Write(b, 0, b.Length);
        Write("%PDF-1.7\n"u8.ToArray());
        var offsets = new List<long>();
        for (var i = 0; i < bodies.Count; i++)
        {
            offsets.Add(buffer.Position);
            Write(System.Text.Encoding.ASCII.GetBytes($"{i + 1} 0 obj\n"));
            Write(bodies[i]);
            Write("\nendobj\n"u8.ToArray());
        }

        var xref = buffer.Position;
        Write(System.Text.Encoding.ASCII.GetBytes($"xref\n0 {bodies.Count + 1}\n0000000000 65535 f \n"));
        foreach (var o in offsets)
        {
            Write(System.Text.Encoding.ASCII.GetBytes($"{o:D10} 00000 n \n"));
        }

        Write(System.Text.Encoding.ASCII.GetBytes(
            $"trailer\n<< /Size {bodies.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n"));
        return buffer.ToArray();
    }

    private static Canvas.Surface Render(byte[] pdf)
    {
        using var document = PdfDocument.Open(new MemoryStream(pdf));
        return document.Render(0, 100, 100, Transparent);
    }

    private static Canvas.Surface RenderMesh(
        int type, byte[] data, bool pattern = false, string? entries = null, string? content = null, string patternEntries = "") =>
        Render(BuildPdf(entries ?? Entries(type), data, pattern, content, patternEntries));

    /// <summary>The pixel covering the PDF point (x, y) (PDF y points up, the surface's y points down).</summary>
    private static Canvas.Rgba32 At(Canvas.Surface s, double x, double y) => s[(int)x, 99 - (int)y];

    private static void AssertNear(Canvas.Rgba32 actual, int r, int g, int b, int tol)
    {
        Assert.InRange(actual.A, 255, 255);
        Assert.InRange(actual.R, r - tol, r + tol);
        Assert.InRange(actual.G, g - tol, g + tol);
        Assert.InRange(actual.B, b - tol, b + tol);
    }

    private static readonly (int, double, double, double, double, double)[] RgbTriangle =
    [
        (0, 10, 10, 1, 0, 0), (0, 90, 10, 0, 1, 0), (0, 50, 90, 0, 0, 1),
    ];

    private static byte[] RgbTriangleData(Fmt f) =>
        Type4(f, RgbTriangle[0], RgbTriangle[1], RgbTriangle[2]);

    #region Type 4

    /// <summary>Proves that <c>sh</c> paints a type 4 triangle with Gouraud-interpolated vertex colors and leaves outside pixels untouched.</summary>
    [Fact]
    public void PdfDocument_MeshShading_Type4_ShOperator_InterpolatesVertexColors()
    {
        using var surface = RenderMesh(4, RgbTriangleData(Std));

        AssertNear(At(surface, 50, 36), 85, 85, 85, 25);
        Assert.True(At(surface, 12, 11).R > 200);
        Assert.True(At(surface, 88, 11).G > 200);
        Assert.True(At(surface, 50, 88).B > 200);
        Assert.Equal(default, At(surface, 95, 95));
        Assert.Equal(default, At(surface, 5, 5));
    }

    /// <summary>Proves that a type 4 shading pattern paints only where both the filled rectangle and the mesh cover.</summary>
    [Fact]
    public void PdfDocument_MeshShading_Type4_Pattern_PaintsOnlyInsideFillAndMesh()
    {
        using var surface = RenderMesh(
            4, RgbTriangleData(Std), pattern: true, content: "/Pattern cs /P1 scn 0 0 50 100 re f");

        Assert.NotEqual(default, At(surface, 30, 20));
        Assert.Equal(default, At(surface, 70, 20));
        Assert.Equal(default, At(surface, 30, 95));
    }

    /// <summary>Proves that edge flag 1 continues a type 4 strip with (vb, vc, new).</summary>
    [Fact]
    public void PdfDocument_MeshShading_Type4_Flag1_ContinuesStrip()
    {
        using var surface = RenderMesh(4, Type4(
            Std,
            (0, 10, 10, 1, 0, 0), (0, 40, 10, 1, 0, 0), (0, 10, 40, 1, 0, 0), (1, 80, 80, 0, 0, 1)));

        Assert.NotEqual(default, At(surface, 68, 64));
        Assert.NotEqual(default, At(surface, 15, 15));
        Assert.Equal(default, At(surface, 15, 70));
    }

    /// <summary>Proves that edge flag 2 continues a type 4 fan with (va, vc, new), distinct from flag 1.</summary>
    [Fact]
    public void PdfDocument_MeshShading_Type4_Flag2_ContinuesFan()
    {
        using var surface = RenderMesh(4, Type4(
            Std,
            (0, 10, 10, 1, 0, 0), (0, 40, 10, 1, 0, 0), (0, 10, 40, 1, 0, 0), (2, 80, 80, 0, 0, 1)));

        Assert.NotEqual(default, At(surface, 50, 55));
        Assert.Equal(default, At(surface, 68, 64));
        Assert.Equal(default, At(surface, 15, 70));
    }

    /// <summary>Proves that a type 4 shading with a <c>/Function</c> maps the vertex parametric value through the function (and honors non-default coordinate/t decode ranges).</summary>
    [Fact]
    public void PdfDocument_MeshShading_Type4_Function_MapsParametricValue()
    {
        // Coordinates decode over 0-200 (so raw 0.5 -> 100 only reaches the page edge), t over 0-1.
        var p = new BitPacker();
        var f = Std;
        foreach (var (x, y, t) in new[] { (0.0, 0.0, 0.0), (100.0, 0.0, 1.0), (0.0, 100.0, 0.0) })
        {
            p.Write(0, 8);
            p.Write((ulong)Math.Round(x / 200.0 * 65535), 16);
            p.Write((ulong)Math.Round(y / 200.0 * 65535), 16);
            p.Write((ulong)Math.Round(t * 255), 8);
            p.Align();
        }

        var entries = $"/ShadingType 4 /ColorSpace /DeviceRGB /BitsPerCoordinate {f.CoordBits} /BitsPerComponent 8 " +
            "/BitsPerFlag 8 /Decode [0 200 0 200 0 1] /Function {FN} 0 R";
        using var surface = Render(BuildPdf(entries, p.ToArray(), pattern: false));

        Assert.True(At(surface, 5, 3).R < 40);
        Assert.True(At(surface, 90, 3).R > 200);
        Assert.Equal(At(surface, 90, 3).R, At(surface, 90, 3).G);
    }

    /// <summary>Proves that type 4 decodes identically across coordinate/component/flag bit sizes, including non-byte-aligned vertex records.</summary>
    [Theory]
    [MemberData(nameof(BitSizeMatrix))]
    public void PdfDocument_MeshShading_Type4_BitSizeMatrix_RendersSame(int coordBits, int compBits, int flagBits)
    {
        var f = new Fmt(coordBits, compBits, flagBits);
        var entries = Entries(4, coordBits.ToString(), compBits.ToString(), flagBits.ToString());
        using var surface = RenderMesh(4, RgbTriangleData(f), entries: entries);

        AssertNear(At(surface, 50, 36), 85, 85, 85, 30);
        Assert.NotEqual(default, At(surface, 50, 50));
        Assert.Equal(default, At(surface, 95, 95));
    }

    /// <summary>The bit-size combinations exercised by the matrix test.</summary>
    public static TheoryData<int, int, int> BitSizeMatrix()
    {
        var data = new TheoryData<int, int, int>();
        foreach (var c in new[] { 8, 12, 16, 24, 32 })
        {
            foreach (var k in new[] { 8, 12, 16 })
            {
                foreach (var fl in new[] { 2, 4, 8 })
                {
                    data.Add(c, k, fl);
                }
            }
        }

        return data;
    }

    /// <summary>Proves that DeviceGray and DeviceCMYK mesh color spaces render.</summary>
    [Fact]
    public void PdfDocument_MeshShading_GrayAndCmykColorSpaces_Render()
    {
        var gray = new BitPacker();
        foreach (var (x, y) in new[] { (10.0, 10.0), (90.0, 10.0), (50.0, 90.0) })
        {
            WriteVertex(gray, Std, 0, x, y, 0.5);
        }

        using var graySurface = RenderMesh(
            4, gray.ToArray(), entries: Entries(4, decode: "[0 100 0 100 0 1]", colorSpace: "/DeviceGray"));
        AssertNear(At(graySurface, 50, 36), 128, 128, 128, 4);

        var cmyk = new BitPacker();
        foreach (var (x, y) in new[] { (10.0, 10.0), (90.0, 10.0), (50.0, 90.0) })
        {
            WriteVertex(cmyk, Std, 0, x, y, 1, 0, 0, 0);
        }

        using var cmykSurface = RenderMesh(
            4, cmyk.ToArray(), entries: Entries(4, decode: "[0 100 0 100 0 1 0 1 0 1 0 1]", colorSpace: "/DeviceCMYK"));
        AssertNear(At(cmykSurface, 50, 36), 0, 255, 255, 4);
    }

    #endregion

    #region Type 5

    /// <summary>Proves that a type 5 lattice (3 columns x 2 rows) renders its 4 triangles with interpolated colors.</summary>
    [Fact]
    public void PdfDocument_MeshShading_Type5_Lattice_RendersAllCells()
    {
        var data = Type5(
            Std,
            (10, 10, 1, 0, 0), (50, 10, 0, 1, 0), (90, 10, 0, 0, 1),
            (10, 90, 1, 1, 0), (50, 90, 0, 1, 1), (90, 90, 1, 0, 1));
        using var surface = RenderMesh(5, data, entries: Entries(5, extra: "/VerticesPerRow 3"));

        Assert.True(At(surface, 11, 11).R > 200);
        Assert.True(At(surface, 50, 11).G > 200);
        Assert.True(At(surface, 89, 11).B > 200);
        AssertNear(At(surface, 30, 50), 128, 255, 0, 40);
        Assert.NotEqual(default, At(surface, 70, 70));
        Assert.Equal(default, At(surface, 5, 50));
    }

    /// <summary>Proves that type 5 pads every vertex to a byte boundary (spec-literal per-vertex alignment).</summary>
    [Fact]
    public void PdfDocument_MeshShading_Type5_NonByteAlignedVertices_AreByteAligned()
    {
        var f = new Fmt(12, 12, 8);
        var data = Type5(
            f,
            (10, 10, 1, 0, 0), (90, 10, 1, 0, 0), (10, 90, 1, 0, 0), (90, 90, 1, 0, 0));
        var entries = Entries(5, "12", "12", extra: "/VerticesPerRow 2");
        using var surface = RenderMesh(5, data, entries: entries);

        AssertNear(At(surface, 50, 50), 255, 0, 0, 3);
    }

    #endregion

    #region Types 6 and 7

    /// <summary>Proves that a flat-edged Coons patch blends its 4 corner colors with bilinear weights.</summary>
    [Fact]
    public void PdfDocument_MeshShading_Type6_FlatPatch_BilinearCornerColors()
    {
        using var surface = RenderMesh(6, SinglePatch(6, (i, j) => Square(i, j)));

        Assert.True(At(surface, 11, 11).R > 200 && At(surface, 11, 11).G < 60);
        Assert.True(At(surface, 11, 88).G > 200 && At(surface, 11, 88).R < 60);
        Assert.True(At(surface, 88, 88).B > 200 && At(surface, 88, 88).R < 60);
        AssertNear(At(surface, 88, 11), 255, 255, 255, 40);
        AssertNear(At(surface, 50, 50), 128, 128, 128, 12);
        Assert.Equal(default, At(surface, 5, 50));
    }

    /// <summary>Proves that Coons patch edges are Bezier curves, not the chord between the corners.</summary>
    [Fact]
    public void PdfDocument_MeshShading_Type6_CurvedEdge_CoversAreaBeyondChord()
    {
        (double X, double Y) Bulged(int i, int j)
        {
            var (x, y) = Square(i, j, 30, 30, 60);
            return i == 0 && (j is 1 or 2) ? (x - 20, y) : (x, y);
        }

        using var surface = RenderMesh(6, SinglePatch(6, Bulged));

        Assert.NotEqual(default, At(surface, 20, 60));
        Assert.Equal(default, At(surface, 10, 60));
    }

    /// <summary>Proves that a type 7 patch whose interior points are the Coons-derived ones renders like the equivalent type 6 patch.</summary>
    [Fact]
    public void PdfDocument_MeshShading_Type7_CoonsInteriorPoints_EqualsType6()
    {
        (double X, double Y) Bulged(int i, int j)
        {
            var (x, y) = Square(i, j, 30, 30, 60);
            return i == 0 && (j is 1 or 2) ? (x - 20, y + CurveOffset(j)) : (x, y);
        }

        double[] CoonsAxis(Func<int, int, double> p)
        {
            double P(int i, int j) => p(i, j);
            return
            [
                (-4 * P(0, 0) + 6 * (P(0, 1) + P(1, 0)) - 2 * (P(0, 3) + P(3, 0)) + 3 * (P(3, 1) + P(1, 3)) - P(3, 3)) / 9,
                (-4 * P(0, 3) + 6 * (P(0, 2) + P(1, 3)) - 2 * (P(0, 0) + P(3, 3)) + 3 * (P(3, 2) + P(1, 0)) - P(3, 0)) / 9,
                (-4 * P(3, 0) + 6 * (P(3, 1) + P(2, 0)) - 2 * (P(3, 3) + P(0, 0)) + 3 * (P(0, 1) + P(2, 3)) - P(0, 3)) / 9,
                (-4 * P(3, 3) + 6 * (P(3, 2) + P(2, 3)) - 2 * (P(3, 0) + P(0, 3)) + 3 * (P(0, 2) + P(2, 0)) - P(0, 0)) / 9,
            ];
        }

        var xs = CoonsAxis((i, j) => Bulged(i, j).X);
        var ys = CoonsAxis((i, j) => Bulged(i, j).Y);
        (double X, double Y) WithCoons(int i, int j) => (i, j) switch
        {
            (1, 1) => (xs[0], ys[0]),
            (1, 2) => (xs[1], ys[1]),
            (2, 1) => (xs[2], ys[2]),
            (2, 2) => (xs[3], ys[3]),
            _ => Bulged(i, j),
        };

        using var six = RenderMesh(6, SinglePatch(6, Bulged));
        using var seven = RenderMesh(7, SinglePatch(7, WithCoons));
        using var moved = RenderMesh(7, SinglePatch(7, (i, j) => (i, j) == (1, 1) ? (WithCoons(1, 1).X + 40, WithCoons(1, 1).Y) : WithCoons(i, j)));

        var maxDiff = 0;
        var movedDiff = 0;
        for (var y = 0; y < 100; y++)
        {
            for (var x = 0; x < 100; x++)
            {
                maxDiff = Math.Max(maxDiff, Math.Abs(six[x, y].R - seven[x, y].R) + Math.Abs(six[x, y].G - seven[x, y].G) + Math.Abs(six[x, y].B - seven[x, y].B));
                movedDiff = Math.Max(movedDiff, Math.Abs(six[x, y].R - moved[x, y].R) + Math.Abs(six[x, y].G - moved[x, y].G) + Math.Abs(six[x, y].B - moved[x, y].B));
            }
        }

        Assert.True(maxDiff <= 12, $"type 6 and Coons-derived type 7 differ by {maxDiff}");
        Assert.True(movedDiff > 20, "moving a tensor interior point must change the rendering");
    }

    /// <summary>Proves that patch edge flags 1-3 inherit the shared edge's points and corner colors from the previous patch (type 6 and 7).</summary>
    [Theory]
    [InlineData(6, 1)]
    [InlineData(6, 2)]
    [InlineData(6, 3)]
    [InlineData(7, 1)]
    [InlineData(7, 2)]
    [InlineData(7, 3)]
    public void PdfDocument_MeshShading_PatchContinuation_InheritsEdgeAndColors(int type, int flag)
    {
        var f = Std;
        Func<int, int, (double X, double Y)> first = (i, j) => Square(i, j, 30, 30, 30);

        // Previous patch's shared edge (new p00..p03) and the outward direction of the new patch.
        (double X, double Y)[] edge;
        (double X, double Y) d;
        (double R, double G, double B)[] inherited;
        switch (flag)
        {
            case 1:
                edge = [first(0, 3), first(1, 3), first(2, 3), first(3, 3)];
                d = (0, 30);
                inherited = [CornerColors[1], CornerColors[2]];
                break;
            case 2:
                edge = [first(3, 3), first(3, 2), first(3, 1), first(3, 0)];
                d = (30, 0);
                inherited = [CornerColors[2], CornerColors[3]];
                break;
            default:
                edge = [first(3, 0), first(2, 0), first(1, 0), first(0, 0)];
                d = (0, -30);
                inherited = [CornerColors[3], CornerColors[0]];
                break;
        }

        (double X, double Y) Next(int i, int j) => (edge[j].X + (d.X * i / 3.0), edge[j].Y + (d.Y * i / 3.0));

        var p = new BitPacker();
        WritePatch(p, f, type, 0, first, CornerColors);
        WritePatch(p, f, type, flag, Next, [(0, 0, 0), (0, 0, 0)]);
        using var surface = RenderMesh(type, p.ToArray(), entries: Entries(type));

        // The new patch is painted at its center, and its two inherited corners carry the previous
        // patch's colors (sampled 2px inward from the shared-edge corners).
        var center = (X: edge[0].X + (edge[3].X - edge[0].X) / 2 + d.X / 2, Y: edge[0].Y + (edge[3].Y - edge[0].Y) / 2 + d.Y / 2);
        Assert.NotEqual(default, At(surface, center.X, center.Y));
        for (var k = 0; k < 2; k++)
        {
            var corner = edge[k * 3];
            var sx = corner.X + (center.X > corner.X ? 2 : -2);
            var sy = corner.Y + (center.Y > corner.Y ? 2 : -2);
            AssertNear(At(surface, sx, sy), (int)(inherited[k].R * 255), (int)(inherited[k].G * 255), (int)(inherited[k].B * 255), 60);
        }
    }

    #endregion

    #region sh and pattern behaviors

    /// <summary>Proves that <c>sh</c> honors the shading's <c>/BBox</c> and the active clip for meshes.</summary>
    [Fact]
    public void PdfDocument_MeshShading_ShOperator_HonorsBBoxAndClip()
    {
        var data = SinglePatch(6, (i, j) => Square(i, j));
        using var boxed = RenderMesh(6, data, entries: Entries(6, extra: "/BBox [0 0 50 100]"));
        Assert.NotEqual(default, At(boxed, 30, 50));
        Assert.Equal(default, At(boxed, 70, 50));

        using var clipped = RenderMesh(6, data, content: "0 0 100 50 re W n /Sh1 sh");
        Assert.NotEqual(default, At(clipped, 50, 30));
        Assert.Equal(default, At(clipped, 50, 70));
    }

    /// <summary>Proves that <c>/Background</c> fills uncovered pattern area but is ignored by <c>sh</c>.</summary>
    [Fact]
    public void PdfDocument_MeshShading_Background_AppliesToPatternsOnly()
    {
        var data = SinglePatch(6, (i, j) => Square(i, j, 30, 30, 40));
        var entries = Entries(6, extra: "/Background [0 0 1]");
        using var pattern = RenderMesh(6, data, pattern: true, entries: entries);
        AssertNear(At(pattern, 10, 10), 0, 0, 255, 0);
        Assert.NotEqual(At(pattern, 10, 10), At(pattern, 50, 50));

        using var sh = RenderMesh(6, data, entries: entries);
        Assert.Equal(default, At(sh, 10, 10));
    }

    /// <summary>Proves that a mesh shading pattern's <c>/Matrix</c> positions the mesh.</summary>
    [Fact]
    public void PdfDocument_MeshShading_PatternMatrix_ShiftsMesh()
    {
        var data = RgbTriangleData(Std);
        using var surface = RenderMesh(4, data, pattern: true, patternEntries: "/Matrix [1 0 0 1 30 0]");

        Assert.NotEqual(default, At(surface, 80, 20));
        Assert.Equal(default, At(surface, 15, 12));
    }

    /// <summary>Proves that stroking with a mesh shading pattern paints the stroke through the mesh.</summary>
    [Fact]
    public void PdfDocument_MeshShading_PatternStroke_PaintsThroughMesh()
    {
        using var surface = RenderMesh(
            4,
            RgbTriangleData(Std),
            pattern: true,
            content: "/Pattern CS /P1 SCN 6 w 0 30 m 100 30 l S");

        Assert.NotEqual(default, At(surface, 50, 30));
        Assert.Equal(default, At(surface, 5, 30));
        Assert.Equal(default, At(surface, 50, 60));
    }

    #endregion

    #region Fail-closed

    private static double CurveOffset(int j) => j == 1 ? 6 : -6;

    private static byte[] ValidData(int type) => type switch
    {
        4 => RgbTriangleData(Std),
        5 => Type5(Std, (10, 10, 1, 0, 0), (90, 10, 0, 1, 0), (10, 90, 0, 0, 1), (90, 90, 1, 1, 1)),
        _ => SinglePatch(type, (i, j) => Square(i, j)),
    };

    private static string ValidEntries(int type) => type == 5 ? Entries(5, extra: "/VerticesPerRow 2") : Entries(type);

    private static string MissingEntryEntries(int type, string missing)
    {
        if (type == 5 && missing == "VerticesPerRow")
        {
            return Entries(5);
        }

        return Entries(
            type,
            coordBits: Omit(missing, "BitsPerCoordinate", "16"),
            compBits: Omit(missing, "BitsPerComponent", "8"),
            flagBits: Omit(missing, "BitsPerFlag", "8"),
            decode: Omit(missing, "Decode", DefaultDecode),
            extra: type == 5 ? "/VerticesPerRow 2" : string.Empty);
    }

    private static string? Omit(string missing, string key, string value) => missing == key ? null : value;

    /// <summary>Proves that a missing required mesh entry throws <see cref="InvalidDataException"/>.</summary>
    [Theory]
    [InlineData(4, "BitsPerCoordinate")]
    [InlineData(4, "BitsPerComponent")]
    [InlineData(4, "BitsPerFlag")]
    [InlineData(4, "Decode")]
    [InlineData(5, "BitsPerCoordinate")]
    [InlineData(5, "BitsPerComponent")]
    [InlineData(5, "Decode")]
    [InlineData(5, "VerticesPerRow")]
    [InlineData(6, "BitsPerFlag")]
    [InlineData(6, "Decode")]
    [InlineData(7, "BitsPerFlag")]
    [InlineData(7, "BitsPerCoordinate")]
    public void PdfDocument_MeshShading_MissingEntry_ThrowsInvalidData(int type, string missing)
    {
        var entries = MissingEntryEntries(type, missing);
        Assert.Throws<InvalidDataException>(() => RenderMesh(type, ValidData(type), entries: entries));
        Assert.Throws<InvalidDataException>(() => RenderMesh(type, ValidData(type), pattern: true, entries: entries));
    }

    /// <summary>Proves that spec-illegal bit sizes and a wrong-length <c>/Decode</c> throw <see cref="InvalidDataException"/>.</summary>
    [Theory]
    [InlineData("3", "8", "8", DefaultDecode)]
    [InlineData("33", "8", "8", DefaultDecode)]
    [InlineData("16", "7", "8", DefaultDecode)]
    [InlineData("16", "20", "8", DefaultDecode)]
    [InlineData("16", "8", "3", DefaultDecode)]
    [InlineData("16", "8", "16", DefaultDecode)]
    [InlineData("16", "8", "8", "[0 100 0 100 0 1 0 1]")]
    [InlineData("16", "8", "8", "[0 100 0 100 0 1 0 1 0 1 0 1]")]
    public void PdfDocument_MeshShading_IllegalBitSizeOrDecode_ThrowsInvalidData(
        string coordBits, string compBits, string flagBits, string decode)
    {
        var entries = Entries(4, coordBits, compBits, flagBits, decode);

        Assert.Throws<InvalidDataException>(() => RenderMesh(4, RgbTriangleData(Std), entries: entries));
    }

    /// <summary>Proves that truncated or empty mesh data throws <see cref="InvalidDataException"/> for every type.</summary>
    [Theory]
    [InlineData(4, "partial")]
    [InlineData(4, "empty")]
    [InlineData(4, "flag0-short")]
    [InlineData(5, "partial")]
    [InlineData(5, "empty")]
    [InlineData(6, "partial")]
    [InlineData(6, "empty")]
    [InlineData(7, "partial")]
    [InlineData(7, "empty")]
    public void PdfDocument_MeshShading_TruncatedData_ThrowsInvalidData(int type, string kind)
    {
        var data = ValidData(type);
        data = kind switch
        {
            "empty" => [],
            "partial" => [.. data, 0],
            _ => data[..(data.Length / 3 * 2)],
        };

        Assert.Throws<InvalidDataException>(() => RenderMesh(type, data, entries: ValidEntries(type)));
    }

    /// <summary>Proves that patch data cut inside a patch throws <see cref="InvalidDataException"/>.</summary>
    [Theory]
    [InlineData(6)]
    [InlineData(7)]
    public void PdfDocument_MeshShading_PatchCutShort_ThrowsInvalidData(int type)
    {
        var data = ValidData(type);

        Assert.Throws<InvalidDataException>(() => RenderMesh(type, data[..(data.Length - 3)]));
    }

    /// <summary>Proves that illegal or first-record edge flags throw <see cref="InvalidDataException"/>.</summary>
    [Theory]
    [InlineData(4, 3, false)]
    [InlineData(4, 1, true)]
    [InlineData(6, 4, false)]
    [InlineData(6, 1, true)]
    [InlineData(7, 4, false)]
    [InlineData(7, 2, true)]
    public void PdfDocument_MeshShading_BadFlags_ThrowInvalidData(int type, int flag, bool first)
    {
        byte[] data;
        if (type == 4)
        {
            data = first
                ? Type4(Std, (flag, 10, 10, 1, 0, 0), (0, 90, 10, 0, 1, 0), (0, 50, 90, 0, 0, 1))
                : Type4(Std, (0, 10, 10, 1, 0, 0), (0, 90, 10, 0, 1, 0), (0, 50, 90, 0, 0, 1), (flag, 50, 50, 1, 1, 1));
        }
        else
        {
            var p = new BitPacker();
            if (!first)
            {
                WritePatch(p, Std, type, 0, (i, j) => Square(i, j), CornerColors);
            }

            WritePatch(p, Std, type, flag, (i, j) => Square(i, j), flag == 0 ? CornerColors : [(0, 0, 0), (0, 0, 0)]);
            data = p.ToArray();
        }

        Assert.Throws<InvalidDataException>(() => RenderMesh(type, data));
    }

    /// <summary>Proves that invalid type 5 lattice shapes throw <see cref="InvalidDataException"/>.</summary>
    [Theory]
    [InlineData(1, 4)]
    [InlineData(0, 4)]
    [InlineData(3, 4)]
    [InlineData(4, 4)]
    public void PdfDocument_MeshShading_Type5_BadLattice_ThrowsInvalidData(int verticesPerRow, int vertexCount)
    {
        var vertices = Enumerable.Range(0, vertexCount).Select(i => (10.0 + i, 10.0, 1.0, 0.0, 0.0)).ToArray();
        var data = Type5(Std, vertices);

        Assert.Throws<InvalidDataException>(() => RenderMesh(
            5, data, entries: Entries(5, extra: $"/VerticesPerRow {verticesPerRow}")));
    }

    /// <summary>Proves that a mesh shading given as a plain dictionary (no stream data) throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PdfDocument_MeshShading_NotAStream_ThrowsInvalidData()
    {
        var body = "<< /ShadingType 4 /ColorSpace /DeviceRGB /BitsPerCoordinate 16 /BitsPerComponent 8 /BitsPerFlag 8 /Decode [0 100 0 100 0 1 0 1 0 1] >>";

        Assert.Throws<InvalidDataException>(() => Render(BuildPdf(string.Empty, [], false, shadingBody: body)));
    }

    /// <summary>Proves that a non-Device mesh <c>/ColorSpace</c> throws <see cref="UnsupportedImageFeatureException"/>.</summary>
    [Fact]
    public void PdfDocument_MeshShading_IndexedColorSpace_ThrowsUnsupported()
    {
        var entries = Entries(4, colorSpace: "[/Indexed /DeviceRGB 1 <000000FFFFFF>]", decode: "[0 100 0 100 0 1]");

        var ex = Assert.Throws<UnsupportedImageFeatureException>(() => RenderMesh(4, RgbTriangleData(Std), entries: entries));
        Assert.Equal("pdf-shading-colorspace-Indexed", ex.Feature);
    }

    /// <summary>Proves that a <c>/Function</c> with the wrong output arity throws <see cref="InvalidDataException"/> and a Type 4 function stays unsupported.</summary>
    [Fact]
    public void PdfDocument_MeshShading_FunctionProblems_FailClosed()
    {
        var arity = Entries(4, decode: "[0 100 0 100 0 1]", extra: "/Function {FN} 0 R");
        var p = new BitPacker();
        WriteVertex(p, Std, 0, 10, 10, 0);
        WriteVertex(p, Std, 0, 90, 10, 0.5);
        WriteVertex(p, Std, 0, 50, 90, 1);
        var data = p.ToArray();
        var bytes = BuildSinglePage(
            "/Sh1 sh",
            "/Shading << /Sh1 5 0 R >>",
            [
                BuildStream(arity.Replace("{FN}", "6", StringComparison.Ordinal), data),
                BuildStream("/FunctionType 2 /Domain [0 1] /C0 [0] /C1 [1] /N 1", []),
            ]);
        Assert.Throws<InvalidDataException>(() => Render(bytes));

        var type4 = BuildSinglePage(
            "/Sh1 sh",
            "/Shading << /Sh1 5 0 R >>",
            [
                BuildStream(arity.Replace("{FN}", "6", StringComparison.Ordinal), data),
                BuildStream("/FunctionType 4", "{ }"u8.ToArray()),
            ]);
        var ex = Assert.Throws<UnsupportedImageFeatureException>(() => Render(type4));
        Assert.Equal("pdf-functiontype-4", ex.Feature);
    }

    /// <summary>Proves that exceeding the vertex limit throws <see cref="UnsupportedImageFeatureException"/> (<c>pdf-shading-mesh-too-many-vertices</c>).</summary>
    [Fact]
    public void PdfDocument_MeshShading_TooManyVertices_ThrowsUnsupported()
    {
        // 1-bit coordinates/components and 2-bit flags: 7 bits per vertex, padded to 1 byte.
        var data = new byte[PdfDocument.MaxMeshVertices + 1];
        var entries = Entries(4, "1", "1", "2");

        var ex = Assert.Throws<UnsupportedImageFeatureException>(() => RenderMesh(4, data, entries: entries));
        Assert.Equal("pdf-shading-mesh-too-many-vertices", ex.Feature);
    }

    /// <summary>Proves that exceeding the patch limit throws <see cref="UnsupportedImageFeatureException"/> (<c>pdf-shading-mesh-too-many-patches</c>).</summary>
    [Fact]
    public void PdfDocument_MeshShading_TooManyPatches_ThrowsUnsupported()
    {
        // 2-bit flag + 12 points x 2 bits + 4 colors x 3 bits = 38 bits = 5 bytes per patch.
        var data = new byte[5 * (PdfDocument.MaxMeshPatches + 1)];
        var entries = Entries(6, "1", "1", "2");

        var ex = Assert.Throws<UnsupportedImageFeatureException>(() => RenderMesh(6, data, entries: entries));
        Assert.Equal("pdf-shading-mesh-too-many-patches", ex.Feature);
    }

    /// <summary>Proves that a mesh exceeding the rasterization work budget throws <see cref="UnsupportedImageFeatureException"/> (<c>pdf-shading-mesh-too-complex</c>).</summary>
    [Fact]
    public void PdfDocument_MeshShading_TooMuchRasterWork_ThrowsUnsupported()
    {
        // Each page-covering triangle costs ~10,000 pixels of work on the 100x100 surface.
        var triangles = (int)(PdfDocument.MaxMeshRasterPixels / 10_000) + 2_000;
        var p = new BitPacker();
        var f = new Fmt(8, 1, 2);
        for (var t = 0; t < triangles; t++)
        {
            WriteVertex(p, f, 0, 0, 0, 1, 1, 1);
            WriteVertex(p, f, 0, 100, 0, 1, 1, 1);
            WriteVertex(p, f, 0, 0, 100, 1, 1, 1);
        }

        var entries = Entries(4, "8", "1", "2");

        var ex = Assert.Throws<UnsupportedImageFeatureException>(() => RenderMesh(4, p.ToArray(), entries: entries));
        Assert.Equal("pdf-shading-mesh-too-complex", ex.Feature);
    }

    #endregion
}
