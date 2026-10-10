// cspell:ignore Gouraud Coons bicubic Bezier Bernstein bbox lerp
using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Geometry;

namespace DemaConsulting.CanvasNet.Pdf;

// Mesh shadings (PDF 32000-1 8.7.4.5.5-8.7.4.5.8): /ShadingType 4 (free-form Gouraud triangle
// mesh), 5 (lattice-form Gouraud triangle mesh), 6 (Coons patch mesh) and 7 (tensor-product
// patch mesh). Decoding, validation and limits live here; painting goes through an offscreen,
// surface-sized bitmap painted with the public TilePaint, so no core-library change is needed.
public sealed partial class PdfDocument
{
    /// <summary>The maximum number of vertices (type 4/5) a single mesh shading may declare.</summary>
    internal const int MaxMeshVertices = 1_048_576;

    /// <summary>The maximum number of patches (type 6/7) a single mesh shading may declare.</summary>
    internal const int MaxMeshPatches = 65_536;

    /// <summary>
    ///     The fixed number of quads per patch edge a patch is subdivided into when painted
    ///     (<c>N x N</c> quads, <c>2 * N * N</c> triangles per patch); uniform and deterministic.
    /// </summary>
    internal const int MeshPatchSubdivision = 16;

    /// <summary>
    ///     The maximum total rasterization work (clamped triangle bounding-box pixels plus a
    ///     per-triangle constant) a single mesh paint may perform.
    /// </summary>
    internal const long MaxMeshRasterPixels = 1L << 28;

    /// <summary>The number of entries in the color lookup table used for meshes with a <c>/Function</c>.</summary>
    private const int MeshFunctionLutSize = 256;

    /// <summary>
    ///     Maps a patch's stream point order (boundary: <c>p00 p01 p02 p03 p13 p23 p33 p32 p31 p30
    ///     p20 p10</c>, then interior: <c>p11 p12 p22 p21</c>) to the row-major index
    ///     (<c>i * 4 + j</c>) in a patch's 4x4 control net.
    /// </summary>
    private static readonly int[] PatchStreamOrder = [0, 1, 2, 3, 7, 11, 15, 14, 13, 12, 8, 4, 5, 6, 10, 9];

    /// <summary>
    ///     For patch edge flags 1, 2 and 3: the previous patch's control-net indices that supply
    ///     the new patch's first four stream points (ISO 32000-1 Tables 85/86).
    /// </summary>
    private static readonly int[][] PatchContinuationPoints =
    [
        [3, 7, 11, 15],
        [15, 14, 13, 12],
        [12, 8, 4, 0],
    ];

    /// <summary>
    ///     For patch edge flags 1, 2 and 3: the previous patch's corner-color indices
    ///     (in stream order <c>c00 c03 c33 c30</c>) that supply the new patch's first two colors.
    /// </summary>
    private static readonly int[][] PatchContinuationColors =
    [
        [1, 2],
        [2, 3],
        [3, 0],
    ];

    /// <summary>A mesh color: either (R, G, B) in 0-255 or, for function-based meshes, the parametric value <c>t</c> in <see cref="A"/>.</summary>
    /// <param name="A">First color component (red, or the parametric value t).</param>
    /// <param name="B">Second color component (green; unused with a function).</param>
    /// <param name="C">Third color component (blue; unused with a function).</param>
    internal readonly record struct MeshColor(double A, double B, double C);

    /// <summary>A mesh vertex: a position and a color.</summary>
    /// <param name="X">The x coordinate.</param>
    /// <param name="Y">The y coordinate.</param>
    /// <param name="Color">The vertex color.</param>
    internal readonly record struct MeshVertex(double X, double Y, MeshColor Color);

    /// <summary>A decoded Coons/tensor patch: a 4x4 control net and 4 corner colors.</summary>
    /// <param name="X">The 16 control point x coordinates (row-major, <c>i * 4 + j</c>).</param>
    /// <param name="Y">The 16 control point y coordinates (row-major, <c>i * 4 + j</c>).</param>
    /// <param name="Colors">The 4 corner colors in stream order (<c>c00 c03 c33 c30</c>).</param>
    internal sealed record MeshPatch(double[] X, double[] Y, MeshColor[] Colors);

    /// <summary>
    ///     The fully decoded contents of a <c>/ShadingType 4</c>-<c>7</c> shading, decoded once
    ///     at resolve time so malformed data fails identically for <c>sh</c> and for patterns.
    /// </summary>
    internal sealed class MeshShading
    {
        /// <summary>Gets the shading's <c>/ShadingType</c> (4-7).</summary>
        internal required int ShadingType { get; init; }

        /// <summary>Gets the vertices (type 4/5); empty for patch types.</summary>
        internal MeshVertex[] Vertices { get; init; } = [];

        /// <summary>Gets triangle vertex indices, 3 per triangle (type 4/5); empty for patch types.</summary>
        internal int[] Triangles { get; init; } = [];

        /// <summary>Gets the patches (type 6/7); empty for triangle types.</summary>
        internal MeshPatch[] Patches { get; init; } = [];

        /// <summary>Gets the color lookup table over <see cref="TMin"/>..<see cref="TMax"/> when the shading has a <c>/Function</c>, else <see langword="null"/>.</summary>
        internal Rgba32[]? Lut { get; init; }

        /// <summary>Gets the lower bound of the parametric value t (function-based meshes).</summary>
        internal double TMin { get; init; }

        /// <summary>Gets the upper bound of the parametric value t (function-based meshes).</summary>
        internal double TMax { get; init; }

        /// <summary>Gets the optional <c>/Background</c> color (honored for pattern use only), or <see langword="null"/>.</summary>
        internal Rgba32? Background { get; init; }
    }

    /// <summary>A big-endian, MSB-first bit reader over a byte array supporting 1-32 bit reads.</summary>
    internal sealed class MeshBitReader
    {
        private readonly byte[] _data;
        private long _bitPosition;

        /// <summary>Initializes a new instance of the <see cref="MeshBitReader"/> class.</summary>
        /// <param name="data">The data to read.</param>
        internal MeshBitReader(byte[] data) => _data = data;

        /// <summary>Gets the number of unread bits.</summary>
        internal long BitsRemaining => ((long)_data.Length * 8) - _bitPosition;

        /// <summary>Reads <paramref name="bits"/> bits (1-32) as an unsigned value.</summary>
        /// <param name="bits">The number of bits to read.</param>
        /// <returns>The value read.</returns>
        /// <exception cref="InvalidDataException">Thrown when fewer than <paramref name="bits"/> bits remain.</exception>
        internal ulong Read(int bits)
        {
            if (bits is < 1 or > 32 || BitsRemaining < bits)
            {
                throw new InvalidDataException("Mesh shading data is truncated.");
            }

            ulong value = 0;
            for (var i = 0; i < bits; i++)
            {
                var byteValue = _data[(int)(_bitPosition >> 3)];
                var bit = (byteValue >> (7 - (int)(_bitPosition & 7))) & 1;
                value = (value << 1) | (uint)bit;
                _bitPosition++;
            }

            return value;
        }

        /// <summary>Skips to the next byte boundary (no-op when already aligned).</summary>
        internal void AlignToByte() => _bitPosition = (_bitPosition + 7) & ~7L;
    }

    /// <summary>
    ///     Resolves a <c>/ShadingType 4</c>-<c>7</c> shading stream into a <see cref="MeshShading"/>.
    /// </summary>
    /// <param name="shading">The already-resolved shading (must be a stream).</param>
    /// <param name="shadingType">The shading type (4-7).</param>
    /// <param name="colorSpace">The already-validated Device color space.</param>
    /// <returns>The decoded mesh.</returns>
    /// <exception cref="InvalidDataException">Thrown for missing/illegal parameters, bad flags or truncated data.</exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when a mesh limit is exceeded (features <c>pdf-shading-mesh-too-many-vertices</c>,
    ///     <c>pdf-shading-mesh-too-many-patches</c>).
    /// </exception>
    private MeshShading ResolveMeshShading(PdfObject shading, int shadingType, PdfColorSpace colorSpace)
    {
        if (shading.Kind != PdfKind.Stream)
        {
            throw new InvalidDataException($"/ShadingType {shadingType} shading must be a stream.");
        }

        var bitsPerCoordinate = RequireMeshBits(shading, "BitsPerCoordinate", [1, 2, 4, 8, 12, 16, 24, 32]);
        var bitsPerComponent = RequireMeshBits(shading, "BitsPerComponent", [1, 2, 4, 8, 12, 16]);
        var bitsPerFlag = shadingType == 5 ? 0 : RequireMeshBits(shading, "BitsPerFlag", [2, 4, 8]);

        var functionEntry = shading.Get("Function");
        Func<double, double[]>? evaluate = null;
        if (functionEntry is not null)
        {
            (_, evaluate) = ResolveFunctionOrFunctionArray(functionEntry);
        }

        var componentCount = ComponentCount(colorSpace);
        var colorValueCount = evaluate is null ? componentCount : 1;
        var decode = RequireNumberArray(shading, "Decode");
        if (decode.Length != 4 + (2 * colorValueCount) || decode.Any(d => !double.IsFinite(d)))
        {
            throw new InvalidDataException(
                $"/Decode must have exactly {4 + (2 * colorValueCount)} finite elements for this mesh shading.");
        }

        Rgba32[]? lut = null;
        var tMin = 0.0;
        var tMax = 0.0;
        if (evaluate is not null)
        {
            tMin = Math.Min(decode[4], decode[5]);
            tMax = Math.Max(decode[4], decode[5]);
            lut = new Rgba32[MeshFunctionLutSize];
            for (var i = 0; i < lut.Length; i++)
            {
                var t = tMin + ((tMax - tMin) * i / (lut.Length - 1));
                var output = evaluate(t);
                if (output.Length != componentCount)
                {
                    throw new InvalidDataException(
                        $"/Function must produce {componentCount} output component(s) for the shading /ColorSpace.");
                }

                lut[i] = ColorFromComponents(colorSpace, output);
            }
        }

        Rgba32? background = null;
        var backgroundValues = ResolveOptionalNumberArray(shading, "Background");
        if (backgroundValues is not null)
        {
            if (backgroundValues.Length != componentCount || backgroundValues.Any(d => !double.IsFinite(d)))
            {
                throw new InvalidDataException(
                    $"/Background must have exactly {componentCount} finite elements for the shading /ColorSpace.");
            }

            background = ColorFromComponents(colorSpace, backgroundValues);
        }

        var parameters = new MeshParameters(
            colorSpace, bitsPerCoordinate, bitsPerComponent, bitsPerFlag, decode, evaluate is not null, colorValueCount);

        int verticesPerRow = 0;
        if (shadingType == 5)
        {
            verticesPerRow = RequireIntEntry(shading, "VerticesPerRow");
            if (verticesPerRow < 2)
            {
                throw new InvalidDataException("/VerticesPerRow must be at least 2.");
            }
        }

        var reader = new MeshBitReader(GetStreamDecodedBytes(shading));
        if (reader.BitsRemaining == 0)
        {
            throw new InvalidDataException("Mesh shading stream contains no data.");
        }

        return shadingType switch
        {
            4 => BuildTriangleMesh(ReadFreeFormTriangles(reader, parameters), shadingType, lut, tMin, tMax, background),
            5 => BuildTriangleMesh(ReadLatticeTriangles(reader, parameters, verticesPerRow), shadingType, lut, tMin, tMax, background),
            _ => new MeshShading
            {
                ShadingType = shadingType,
                Patches = ReadPatches(reader, parameters, shadingType),
                Lut = lut,
                TMin = tMin,
                TMax = tMax,
                Background = background,
            },
        };
    }

    /// <summary>The validated, shared decode parameters of a mesh stream.</summary>
    private readonly record struct MeshParameters(
        PdfColorSpace ColorSpace,
        int BitsPerCoordinate,
        int BitsPerComponent,
        int BitsPerFlag,
        double[] Decode,
        bool UsesFunction,
        int ColorValueCount);

    /// <summary>Assembles a triangle-type <see cref="MeshShading"/>.</summary>
    private static MeshShading BuildTriangleMesh(
        (MeshVertex[] Vertices, int[] Triangles) data,
        int shadingType,
        Rgba32[]? lut,
        double tMin,
        double tMax,
        Rgba32? background) =>
        new()
        {
            ShadingType = shadingType,
            Vertices = data.Vertices,
            Triangles = data.Triangles,
            Lut = lut,
            TMin = tMin,
            TMax = tMax,
            Background = background,
        };

    /// <summary>Reads a required bit-size entry and validates it against the spec-legal set.</summary>
    /// <exception cref="InvalidDataException">Thrown when missing or not one of <paramref name="legal"/>.</exception>
    private int RequireMeshBits(PdfObject shading, string key, int[] legal)
    {
        var value = RequireIntEntry(shading, key);
        if (Array.IndexOf(legal, value) < 0)
        {
            throw new InvalidDataException($"/{key} {value} is not a legal value for a mesh shading.");
        }

        return value;
    }

    /// <summary>Maps a raw <paramref name="bits"/>-bit sample into <c>[dMin, dMax]</c>.</summary>
    private static double DecodeSample(ulong raw, int bits, double dMin, double dMax) =>
        dMin + (raw * (dMax - dMin) / (Math.Pow(2, bits) - 1));

    /// <summary>Reads one coordinate pair.</summary>
    private static (double X, double Y) ReadMeshPoint(MeshBitReader reader, MeshParameters p)
    {
        var x = DecodeSample(reader.Read(p.BitsPerCoordinate), p.BitsPerCoordinate, p.Decode[0], p.Decode[1]);
        var y = DecodeSample(reader.Read(p.BitsPerCoordinate), p.BitsPerCoordinate, p.Decode[2], p.Decode[3]);
        return (x, y);
    }

    /// <summary>Reads one color (RGB bytes, or the parametric value t when a function is present).</summary>
    private static MeshColor ReadMeshColor(MeshBitReader reader, MeshParameters p)
    {
        var values = new double[p.ColorValueCount];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = DecodeSample(
                reader.Read(p.BitsPerComponent), p.BitsPerComponent, p.Decode[4 + (2 * i)], p.Decode[5 + (2 * i)]);
        }

        if (p.UsesFunction)
        {
            return new MeshColor(values[0], 0, 0);
        }

        var color = ColorFromComponents(p.ColorSpace, values);
        return new MeshColor(color.R, color.G, color.B);
    }

    /// <summary>Reads one vertex (position and color) and aligns to the next byte boundary.</summary>
    private static MeshVertex ReadMeshVertex(MeshBitReader reader, MeshParameters p)
    {
        var (x, y) = ReadMeshPoint(reader, p);
        var color = ReadMeshColor(reader, p);
        reader.AlignToByte();
        return new MeshVertex(x, y, color);
    }

    /// <summary>Reads a type 4 (free-form) mesh into a vertex list and triangle index list.</summary>
    private static (MeshVertex[] Vertices, int[] Triangles) ReadFreeFormTriangles(MeshBitReader reader, MeshParameters p)
    {
        var vertices = new List<MeshVertex>();
        var triangles = new List<int>();
        int a = -1;
        int b = -1;
        int c = -1;
        while (reader.BitsRemaining > 0)
        {
            var flag = reader.Read(p.BitsPerFlag);
            if (flag > 2 || (flag != 0 && a < 0))
            {
                throw new InvalidDataException($"Invalid edge flag {flag} in type 4 mesh shading.");
            }

            var vertex = ReadMeshVertex(reader, p);
            if (flag == 0)
            {
                AddMeshVertex(vertices, vertex);
                for (var extra = 0; extra < 2; extra++)
                {
                    _ = reader.Read(p.BitsPerFlag);
                    AddMeshVertex(vertices, ReadMeshVertex(reader, p));
                }

                a = vertices.Count - 3;
                b = vertices.Count - 2;
                c = vertices.Count - 1;
            }
            else
            {
                AddMeshVertex(vertices, vertex);
                var added = vertices.Count - 1;
                var oldB = b;
                var oldC = c;
                if (flag == 1)
                {
                    a = oldB;
                }

                b = oldC;
                c = added;
            }

            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        return (vertices.ToArray(), triangles.ToArray());
    }

    /// <summary>Reads a type 5 (lattice) mesh into a vertex list and triangle index list.</summary>
    private static (MeshVertex[] Vertices, int[] Triangles) ReadLatticeTriangles(
        MeshBitReader reader, MeshParameters p, int verticesPerRow)
    {
        var vertices = new List<MeshVertex>();
        while (reader.BitsRemaining > 0)
        {
            AddMeshVertex(vertices, ReadMeshVertex(reader, p));
        }

        if (vertices.Count % verticesPerRow != 0 || vertices.Count / verticesPerRow < 2)
        {
            throw new InvalidDataException(
                "Type 5 mesh shading vertex count must be a multiple of /VerticesPerRow and span at least 2 rows.");
        }

        var rows = vertices.Count / verticesPerRow;
        var triangles = new List<int>(2 * 3 * (rows - 1) * (verticesPerRow - 1));
        for (var r = 0; r < rows - 1; r++)
        {
            for (var col = 0; col < verticesPerRow - 1; col++)
            {
                var i00 = (r * verticesPerRow) + col;
                var i01 = i00 + 1;
                var i10 = i00 + verticesPerRow;
                var i11 = i10 + 1;
                triangles.AddRange([i00, i01, i10, i01, i11, i10]);
            }
        }

        return (vertices.ToArray(), triangles.ToArray());
    }

    /// <summary>Adds a vertex, enforcing <see cref="MaxMeshVertices"/>.</summary>
    /// <exception cref="UnsupportedImageFeatureException">Thrown when the limit would be exceeded.</exception>
    private static void AddMeshVertex(List<MeshVertex> vertices, MeshVertex vertex)
    {
        if (vertices.Count >= MaxMeshVertices)
        {
            throw new UnsupportedImageFeatureException(
                "pdf-shading-mesh-too-many-vertices",
                $"Mesh shading declares more than {MaxMeshVertices} vertices.");
        }

        vertices.Add(vertex);
    }

    /// <summary>Reads a type 6 (Coons) or type 7 (tensor) patch mesh.</summary>
    private static MeshPatch[] ReadPatches(MeshBitReader reader, MeshParameters p, int shadingType)
    {
        var patches = new List<MeshPatch>();
        MeshPatch? previous = null;
        var streamPoints = shadingType == 6 ? 12 : 16;
        while (reader.BitsRemaining > 0)
        {
            var flag = (int)reader.Read(p.BitsPerFlag);
            if (flag > 3 || (flag > 0 && previous is null))
            {
                throw new InvalidDataException($"Invalid edge flag {flag} in type {shadingType} mesh shading.");
            }

            if (patches.Count >= MaxMeshPatches)
            {
                throw new UnsupportedImageFeatureException(
                    "pdf-shading-mesh-too-many-patches",
                    $"Mesh shading declares more than {MaxMeshPatches} patches.");
            }

            var x = new double[16];
            var y = new double[16];
            var colors = new MeshColor[4];
            var firstPoint = 0;
            var firstColor = 0;
            if (flag > 0)
            {
                for (var k = 0; k < 4; k++)
                {
                    var source = PatchContinuationPoints[flag - 1][k];
                    x[PatchStreamOrder[k]] = previous!.X[source];
                    y[PatchStreamOrder[k]] = previous.Y[source];
                }

                for (var k = 0; k < 2; k++)
                {
                    colors[k] = previous!.Colors[PatchContinuationColors[flag - 1][k]];
                }

                firstPoint = 4;
                firstColor = 2;
            }

            for (var k = firstPoint; k < streamPoints; k++)
            {
                (x[PatchStreamOrder[k]], y[PatchStreamOrder[k]]) = ReadMeshPoint(reader, p);
            }

            for (var k = firstColor; k < 4; k++)
            {
                colors[k] = ReadMeshColor(reader, p);
            }

            reader.AlignToByte();
            if (shadingType == 6)
            {
                ApplyCoonsInteriorPoints(x);
                ApplyCoonsInteriorPoints(y);
            }

            previous = new MeshPatch(x, y, colors);
            patches.Add(previous);
        }

        return patches.ToArray();
    }

    /// <summary>
    ///     Fills the 4 interior control points of a Coons patch's 4x4 net (ISO 32000-1
    ///     &#xA7;8.7.4.5.7) from its 12 boundary points, one coordinate axis at a time.
    /// </summary>
    /// <param name="p">The 16 row-major (<c>i * 4 + j</c>) values of one coordinate; interior entries are overwritten.</param>
    private static void ApplyCoonsInteriorPoints(double[] p)
    {
        double P(int i, int j) => p[(i * 4) + j];

        var p11 = ((-4 * P(0, 0)) + (6 * (P(0, 1) + P(1, 0))) - (2 * (P(0, 3) + P(3, 0))) + (3 * (P(3, 1) + P(1, 3))) - P(3, 3)) / 9;
        var p12 = ((-4 * P(0, 3)) + (6 * (P(0, 2) + P(1, 3))) - (2 * (P(0, 0) + P(3, 3))) + (3 * (P(3, 2) + P(1, 0))) - P(3, 0)) / 9;
        var p21 = ((-4 * P(3, 0)) + (6 * (P(3, 1) + P(2, 0))) - (2 * (P(3, 3) + P(0, 0))) + (3 * (P(0, 1) + P(2, 3))) - P(0, 3)) / 9;
        var p22 = ((-4 * P(3, 3)) + (6 * (P(3, 2) + P(2, 3))) - (2 * (P(3, 0) + P(0, 3))) + (3 * (P(0, 2) + P(2, 0))) - P(0, 0)) / 9;
        p[5] = p11;
        p[6] = p12;
        p[9] = p21;
        p[10] = p22;
    }

    /// <summary>
    ///     Paints <paramref name="mesh"/> through <paramref name="path"/> (honoring the active clip):
    ///     the mesh is rasterized into an offscreen bitmap bounded to the path's device-space extent
    ///     (transparent outside the mesh) which is then painted over <paramref name="path"/> with a
    ///     <see cref="TilePaint"/>.
    /// </summary>
    /// <param name="mesh">The decoded mesh.</param>
    /// <param name="toDevice">The transform from the mesh's coordinate space to device space.</param>
    /// <param name="path">The device-space region/path to paint through.</param>
    /// <param name="fillRule">The fill rule for <paramref name="path"/>.</param>
    /// <param name="applyBackground">Whether to pre-fill the mesh's <c>/Background</c> (pattern use only).</param>
    /// <exception cref="UnsupportedImageFeatureException">Thrown (feature <c>pdf-shading-mesh-too-complex</c>) when the raster work budget is exceeded.</exception>
    private void PaintMesh(MeshShading mesh, Matrix3x2 toDevice, Geometry.Path path, FillRule fillRule, bool applyBackground)
    {
        // Rasterize only the part of the destination the path can touch, so a small painted
        // region never allocates a page-sized temporary bitmap.
        var bounds = path.GetBounds();
        if (bounds.IsEmpty)
        {
            return;
        }

        var left = (int)Math.Max(0, Math.Floor(Math.Min(bounds.Left, _surface.Width)) - 1);
        var top = (int)Math.Max(0, Math.Floor(Math.Min(bounds.Top, _surface.Height)) - 1);
        var right = (int)Math.Min(_surface.Width, Math.Ceiling(Math.Max(bounds.Right, 0)) + 1);
        var bottom = (int)Math.Min(_surface.Height, Math.Ceiling(Math.Max(bounds.Bottom, 0)) + 1);
        if (right <= left || bottom <= top)
        {
            return;
        }

        toDevice *= Matrix3x2.CreateTranslation(-left, -top);
        using var meshSurface = new Surface(right - left, bottom - top);
        if (applyBackground && mesh.Background is { } background)
        {
            for (var y = 0; y < meshSurface.Height; y++)
            {
                meshSurface.GetRowSpan(y).Fill(background);
            }
        }

        var rasterizer = new MeshRasterizer(meshSurface, mesh);
        if (mesh.ShadingType is 4 or 5)
        {
            for (var i = 0; i + 2 < mesh.Triangles.Length; i += 3)
            {
                rasterizer.Triangle(
                    ToDevice(mesh.Vertices[mesh.Triangles[i]], toDevice),
                    ToDevice(mesh.Vertices[mesh.Triangles[i + 1]], toDevice),
                    ToDevice(mesh.Vertices[mesh.Triangles[i + 2]], toDevice));
            }
        }
        else
        {
            var grid = new MeshVertex[(MeshPatchSubdivision + 1) * (MeshPatchSubdivision + 1)];
            foreach (var patch in mesh.Patches)
            {
                EvaluatePatchGrid(patch, toDevice, grid);
                for (var i = 0; i < MeshPatchSubdivision; i++)
                {
                    for (var j = 0; j < MeshPatchSubdivision; j++)
                    {
                        var g00 = grid[(i * (MeshPatchSubdivision + 1)) + j];
                        var g01 = grid[(i * (MeshPatchSubdivision + 1)) + j + 1];
                        var g10 = grid[((i + 1) * (MeshPatchSubdivision + 1)) + j];
                        var g11 = grid[((i + 1) * (MeshPatchSubdivision + 1)) + j + 1];
                        rasterizer.Triangle(g00, g10, g01);
                        rasterizer.Triangle(g10, g11, g01);
                    }
                }
            }
        }

        var tilePaint = new TilePaint(meshSurface, Matrix3x2.CreateTranslation(left, top), meshSurface.Width, meshSurface.Height);
        PathFiller.Fill(_surface, path, tilePaint, _gs.Clip, fillRule);
    }

    /// <summary>Transforms a mesh vertex's position by <paramref name="m"/> (in double precision).</summary>
    private static MeshVertex ToDevice(MeshVertex v, Matrix3x2 m) =>
        new(
            (v.X * m.M11) + (v.Y * m.M21) + m.M31,
            (v.X * m.M12) + (v.Y * m.M22) + m.M32,
            v.Color);

    /// <summary>
    ///     Evaluates a patch's bicubic Bezier surface and bilinear corner-color blend on an
    ///     <c>(N+1) x (N+1)</c> grid, in device space.
    /// </summary>
    private static void EvaluatePatchGrid(MeshPatch patch, Matrix3x2 toDevice, MeshVertex[] grid)
    {
        var n = MeshPatchSubdivision;
        Span<double> bu = stackalloc double[4];
        Span<double> bv = stackalloc double[4];
        for (var i = 0; i <= n; i++)
        {
            var u = (double)i / n;
            Bernstein(u, bu);
            for (var j = 0; j <= n; j++)
            {
                var v = (double)j / n;
                Bernstein(v, bv);
                double x = 0;
                double y = 0;
                for (var a = 0; a < 4; a++)
                {
                    for (var b = 0; b < 4; b++)
                    {
                        var w = bu[a] * bv[b];
                        x += patch.X[(a * 4) + b] * w;
                        y += patch.Y[(a * 4) + b] * w;
                    }
                }

                var c00 = patch.Colors[0];
                var c03 = patch.Colors[1];
                var c33 = patch.Colors[2];
                var c30 = patch.Colors[3];
                var w00 = (1 - u) * (1 - v);
                var w03 = (1 - u) * v;
                var w33 = u * v;
                var w30 = u * (1 - v);
                var color = new MeshColor(
                    (c00.A * w00) + (c03.A * w03) + (c33.A * w33) + (c30.A * w30),
                    (c00.B * w00) + (c03.B * w03) + (c33.B * w33) + (c30.B * w30),
                    (c00.C * w00) + (c03.C * w03) + (c33.C * w33) + (c30.C * w30));
                grid[(i * (n + 1)) + j] = ToDevice(new MeshVertex(x, y, color), toDevice);
            }
        }
    }

    /// <summary>Computes the 4 cubic Bernstein basis values at <paramref name="t"/>.</summary>
    private static void Bernstein(double t, Span<double> result)
    {
        var s = 1 - t;
        result[0] = s * s * s;
        result[1] = 3 * s * s * t;
        result[2] = 3 * s * t * t;
        result[3] = t * t * t;
    }

    /// <summary>
    ///     A from-scratch Gouraud triangle rasterizer writing opaque pixels (no anti-aliasing,
    ///     pixel-center sampling, edge-inclusive so shared edges leave no cracks) into a
    ///     <see cref="Surface"/> with a total work budget.
    /// </summary>
    private sealed class MeshRasterizer
    {
        private const double EdgeEpsilon = 1e-9;
        private const long PerTriangleCost = 16;

        private readonly Surface _surface;
        private readonly Rgba32[]? _lut;
        private readonly double _tMin;
        private readonly double _tRange;
        private long _remainingBudget = MaxMeshRasterPixels;

        /// <summary>Initializes a new instance of the <see cref="MeshRasterizer"/> class.</summary>
        /// <param name="surface">The destination surface.</param>
        /// <param name="mesh">The mesh being painted (supplies the color lookup table).</param>
        internal MeshRasterizer(Surface surface, MeshShading mesh)
        {
            _surface = surface;
            _lut = mesh.Lut;
            _tMin = mesh.TMin;
            _tRange = mesh.TMax - mesh.TMin;
        }

        /// <summary>Rasterizes one device-space Gouraud triangle.</summary>
        /// <exception cref="UnsupportedImageFeatureException">Thrown when the work budget is exhausted.</exception>
        internal void Triangle(MeshVertex a, MeshVertex b, MeshVertex c)
        {
            if (!double.IsFinite(a.X + a.Y + b.X + b.Y + c.X + c.Y))
            {
                return;
            }

            var area = Edge(a, b, c.X, c.Y);
            if (!double.IsFinite(area) || Math.Abs(area) < 1e-12)
            {
                return;
            }

            var x0 = (int)Math.Clamp(Math.Ceiling(Math.Min(a.X, Math.Min(b.X, c.X)) - 0.5 - 1e-6), 0, _surface.Width);
            var x1 = (int)Math.Clamp(Math.Floor(Math.Max(a.X, Math.Max(b.X, c.X)) - 0.5 + 1e-6), -1, _surface.Width - 1);
            var y0 = (int)Math.Clamp(Math.Ceiling(Math.Min(a.Y, Math.Min(b.Y, c.Y)) - 0.5 - 1e-6), 0, _surface.Height);
            var y1 = (int)Math.Clamp(Math.Floor(Math.Max(a.Y, Math.Max(b.Y, c.Y)) - 0.5 + 1e-6), -1, _surface.Height - 1);

            _remainingBudget -= PerTriangleCost + (x1 >= x0 && y1 >= y0 ? (long)(x1 - x0 + 1) * (y1 - y0 + 1) : 0);
            if (_remainingBudget < 0)
            {
                throw new UnsupportedImageFeatureException(
                    "pdf-shading-mesh-too-complex",
                    "Mesh shading requires more rasterization work than the supported limit.");
            }

            for (var y = y0; y <= y1; y++)
            {
                var row = _surface.GetRowSpan(y);
                var py = y + 0.5;
                for (var x = x0; x <= x1; x++)
                {
                    var px = x + 0.5;
                    var l0 = Edge(b, c, px, py) / area;
                    var l1 = Edge(c, a, px, py) / area;
                    var l2 = Edge(a, b, px, py) / area;
                    if (l0 < -EdgeEpsilon || l1 < -EdgeEpsilon || l2 < -EdgeEpsilon)
                    {
                        continue;
                    }

                    row[x] = ColorAt(
                        (l0 * a.Color.A) + (l1 * b.Color.A) + (l2 * c.Color.A),
                        (l0 * a.Color.B) + (l1 * b.Color.B) + (l2 * c.Color.B),
                        (l0 * a.Color.C) + (l1 * b.Color.C) + (l2 * c.Color.C));
                }
            }
        }

        private static double Edge(MeshVertex p, MeshVertex q, double rx, double ry) =>
            ((q.X - p.X) * (ry - p.Y)) - ((q.Y - p.Y) * (rx - p.X));

        private Rgba32 ColorAt(double a, double b, double c)
        {
            if (_lut is not null)
            {
                var fraction = _tRange == 0 ? 0 : (a - _tMin) / _tRange;
                var index = (int)Math.Round(Math.Clamp(fraction, 0, 1) * (_lut.Length - 1));
                return _lut[index];
            }

            return new Rgba32(ClampByte(a), ClampByte(b), ClampByte(c), 255);
        }

        private static byte ClampByte(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);
    }
}
