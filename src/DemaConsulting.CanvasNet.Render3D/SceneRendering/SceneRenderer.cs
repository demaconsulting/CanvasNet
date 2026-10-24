// cspell:ignore Binner

using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneModel;

namespace DemaConsulting.CanvasNet.Render3D.SceneRendering;

/// <summary>
///     Renders a <see cref="Scene"/> into a CanvasNet <see cref="Surface"/> with a pure-managed
///     software rasterizer.
/// </summary>
/// <remarks>
///     <para>
///         <b>Pipeline.</b> (1) Validate and flatten the scene graph; (2) transform every mesh
///         vertex into camera-relative view space in <b>double</b> precision (<c>world *
///         translate(-eye) * rotation</c>, so the world offset cancels before anything is narrowed to
///         single precision); (3) derive the near/far range from the scene's view-depth range unless
///         the camera supplies it; (4) per triangle: shade flat (Lambert, optional specular), clip
///         against the near plane and guard band, project to the sample grid and convert to 24.8
///         fixed point; (5) bin triangles into 32x32-pixel tiles; (6) per tile rasterize with exact
///         integer edge functions and the top-left rule into a depth-tested sample buffer and
///         resolve the k x k sub-pixel samples into the output pixels.
///     </para>
///     <para>
///         <b>Constructive solid geometry.</b> The scene tree is first compiled into leaves (maximal
///         union-only subtrees) and a boolean program. A <b>union-only</b> scene (no difference or
///         intersection of two or more operands) takes the z-buffer path above, unchanged and with no
///         fragment storage. Otherwise each tile collects <em>every</em> fragment of every sample
///         (an exact-sized count pass and fill pass), and a per-sample resolver evaluates the program
///         along the ray in depth order to find the visible surface of the result (see the CSG
///         resolver design); the chosen depth and shade go into the same sample buffers, so
///         supersampling, tiling and parallelism are shared with the z-buffer path. Cut surfaces
///         exposed by a subtrahend use that operand's own color and flipped normal. Before any
///         rendering work, every mesh of a CSG-path scene is checked for being a closed, outward-wound
///         solid; problems are delivered to <see cref="SceneRenderOptions.UnsupportedFeatureHandler"/>
///         (or thrown as <see cref="SceneRenderUnsupportedFeatureException"/> in strict mode) while
///         rendering otherwise proceeds. A tile needing more than
///         <see cref="SceneRenderOptions.MaxFragmentsPerTile"/> fragments throws
///         <see cref="RenderBudgetExceededException"/>.
///     </para>
///     <para>
///         <b>Determinism.</b> Coverage is integer-exact, fragment resolution uses a total order, and
///         tiles are independent, so the output (and the budget exception) is
///         byte-identical across runs, platforms and <see cref="SceneRenderOptions.MaxDegreeOfParallelism"/>
///         settings.
///     </para>
///     <para>
///         <b>Cancellation.</b> The token is observed at entry, after flattening, periodically
///         during triangle setup, at the start of every tile and periodically while collecting and
///         resolving fragments; cancellation throws
///         <see cref="OperationCanceledException"/> (never an <see cref="AggregateException"/>) and
///         the partially rendered surface is disposed.
///     </para>
///     <para>
///         Static, stateless and thread-safe: scenes, cameras and options are immutable, so many renders may run concurrently.
///     </para>
/// </remarks>
public static class SceneRenderer
{
    /// <summary>The tile edge length in output pixels.</summary>
    private const int TilePixels = 32;

    /// <summary>How many triangles are set up between cooperative cancellation checks.</summary>
    private const int CancellationCheckInterval = 4096;

    /// <summary>Relative depth tolerance (times the larger of scene diameter and far depth) within which two fragments are treated as coincident.</summary>
    private const double DepthToleranceFactor = 1e-6;

    /// <summary>Fraction of the far depth used as the automatic perspective near-plane floor.</summary>
    private const double NearFloorFactor = 1e-4;

    /// <summary>Safety margin applied below the nearest vertex depth when choosing the automatic near plane.</summary>
    private const double NearMargin = 0.99;

    /// <summary>Safety margin applied above the farthest vertex depth when choosing the automatic far plane.</summary>
    private const double FarMargin = 1.01;

    /// <summary>A tiny positive scale that keeps derived distances positive for degenerate (point-like) scenes.</summary>
    private const double Tiny = 1e-12;

    /// <summary>
    ///     Renders <paramref name="scene"/> as seen by <paramref name="camera"/> into a new surface.
    /// </summary>
    /// <remarks>
    ///     An empty scene yields a surface filled with the background color. The caller owns (and
    ///     must dispose) the returned surface. Rendering cost grows with the pixel count times the
    ///     square of <see cref="SceneRenderOptions.Supersampling"/>.
    /// </remarks>
    /// <param name="scene">The scene to render.</param>
    /// <param name="camera">The camera; an orthographic camera must define its height.</param>
    /// <param name="width">The output width in pixels, from 1 to <see cref="Surface.MaxDimension"/>.</param>
    /// <param name="height">The output height in pixels, from 1 to <see cref="Surface.MaxDimension"/>.</param>
    /// <param name="options">The render options, or <see langword="null"/> for <see cref="SceneRenderOptions.Default"/>.</param>
    /// <param name="cancellationToken">A token to cancel rendering cooperatively.</param>
    /// <returns>A new <paramref name="width"/> x <paramref name="height"/> surface.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="scene"/> or <paramref name="camera"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the size is outside 1 to <see cref="Surface.MaxDimension"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the camera is invalid (eye equals target, orthographic without height, near not below far).</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    /// <exception cref="RenderBudgetExceededException">Thrown when a CSG scene needs more fragments in a tile than <see cref="SceneRenderOptions.MaxFragmentsPerTile"/>.</exception>
    /// <exception cref="SceneRenderUnsupportedFeatureException">Thrown when <see cref="SceneRenderOptions.ThrowOnUnsupportedFeature"/> is set and a CSG scene contains an unsuitable operand.</exception>
    public static Surface Render(
        Scene scene,
        Camera camera,
        int width,
        int height,
        SceneRenderOptions? options = null,
        CancellationToken cancellationToken = default) =>
        RenderCore(scene, camera, width, height, options, forceFragmentPath: false, cancellationToken);

    /// <summary>
    ///     Test seam: renders like <see cref="Render"/> but sends even a union-only scene through the
    ///     fragment-list CSG path (a single leaf holding all meshes), so tests can prove that the CSG
    ///     path and the z-buffer fast path agree. No operand validation is performed.
    /// </summary>
    /// <param name="scene">The scene to render.</param>
    /// <param name="camera">The camera.</param>
    /// <param name="width">The output width in pixels.</param>
    /// <param name="height">The output height in pixels.</param>
    /// <param name="options">The render options, or <see langword="null"/> for the defaults.</param>
    /// <param name="cancellationToken">A token to cancel rendering cooperatively.</param>
    /// <returns>A new surface.</returns>
    internal static Surface RenderWithFragmentPath(
        Scene scene,
        Camera camera,
        int width,
        int height,
        SceneRenderOptions? options = null,
        CancellationToken cancellationToken = default) =>
        RenderCore(scene, camera, width, height, options, forceFragmentPath: true, cancellationToken);

    /// <summary>
    ///     The shared implementation of <see cref="Render"/> and the fragment-path test seam.
    /// </summary>
    /// <param name="scene">The scene to render.</param>
    /// <param name="camera">The camera.</param>
    /// <param name="width">The output width in pixels.</param>
    /// <param name="height">The output height in pixels.</param>
    /// <param name="options">The render options, or <see langword="null"/> for the defaults.</param>
    /// <param name="forceFragmentPath">Whether a union-only scene must also use the fragment-list path.</param>
    /// <param name="cancellationToken">A token to cancel rendering cooperatively.</param>
    /// <returns>A new surface.</returns>
    private static Surface RenderCore(
        Scene scene,
        Camera camera,
        int width,
        int height,
        SceneRenderOptions? options,
        bool forceFragmentPath,
        CancellationToken cancellationToken)
    {
        // Validate everything up front so no work is done for bad input
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(camera);
        ValidateSize(width, height);
        options ??= SceneRenderOptions.Default;
        cancellationToken.ThrowIfCancellationRequested();

        var view = new ViewTransform(camera, width / (double)height);

        // Decide the path: a union-only scene has no program and is rendered by the z-buffer
        var compilation = CsgCompiler.Compile(scene.Root);
        var program = compilation.Program;
        var csgScene = program is not null;
        if (program is null && forceFragmentPath && compilation.LeafCount > 0)
        {
            program = new CsgProgram([new CsgInstruction(CsgOpCode.PushLeaf, 0)], compilation.LeafCount);
        }

        // Operands of a real CSG scene must be solids; problems are reported before any rendering work
        if (csgScene)
        {
            CsgOperandValidator.Validate(scene, options, cancellationToken);
        }

        // Flatten and transform in double precision; the depth range drives near/far/tolerance
        var instances = TransformInstances(scene, view, cancellationToken, out var stats);
        cancellationToken.ThrowIfCancellationRequested();
        if (csgScene)
        {
            CsgOperandValidator.ValidateNearPlane(options, camera.NearPlane, stats.HasVertices, stats.MinDepth);
        }

        // Prepare the screen-space triangles (shading, clipping, projection, fixed point)
        var k = options.Supersampling;
        var (near, far) = ResolveClipRange(camera, stats);
        var projection = SampleProjection.Create(view, width * k, height * k, near);
        var leafOfInstance = program is null ? null : compilation.LeafOfInstance;
        var triangles = BuildScreenTriangles(instances, view, projection, options, leafOfInstance, cancellationToken);
        var epsilon = (float)(DepthToleranceFactor * Math.Max(stats.Diameter, Math.Abs(stats.MaxDepth)));

        // Bin and render the tiles
        var tilesX = (width + TilePixels - 1) / TilePixels;
        var tilesY = (height + TilePixels - 1) / TilePixels;
        var bins = TileBinner.Bin(CollectionsMarshal.AsSpan(triangles), tilesX, tilesY, TilePixels * k);
        var surface = new Surface(width, height);
        try
        {
            var job = new TileJob(triangles, bins, surface, options, epsilon, (float)far, program);
            RenderTiles(job, cancellationToken);
        }
        catch
        {
            // Never leak a partially rendered surface (cancellation or unexpected failure)
            surface.Dispose();
            throw;
        }

        return surface;
    }

    /// <summary>
    ///     Renders <paramref name="scene"/> with an automatically fitted camera: an isometric view
    ///     (azimuth 45, elevation about 35.26 degrees) that frames the whole scene with a 5 % margin.
    /// </summary>
    /// <remarks>
    ///     A convenient way to preview a scene. Use <see cref="Render"/> with an explicit
    ///     <see cref="Camera"/> (for example from <see cref="Camera.FitToBounds"/> with other angles)
    ///     for any other view. An empty scene renders the background only.
    /// </remarks>
    /// <param name="scene">The scene to render.</param>
    /// <param name="width">The output width in pixels, from 1 to <see cref="Surface.MaxDimension"/>.</param>
    /// <param name="height">The output height in pixels, from 1 to <see cref="Surface.MaxDimension"/>.</param>
    /// <param name="options">The render options, or <see langword="null"/> for <see cref="SceneRenderOptions.Default"/>.</param>
    /// <param name="cancellationToken">A token to cancel rendering cooperatively.</param>
    /// <returns>A new <paramref name="width"/> x <paramref name="height"/> surface.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="scene"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the size is outside 1 to <see cref="Surface.MaxDimension"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    public static Surface RenderFitted(
        Scene scene,
        int width,
        int height,
        SceneRenderOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ValidateSize(width, height);

        // Honor cancellation before the potentially expensive bounds computation
        cancellationToken.ThrowIfCancellationRequested();

        var camera = Camera.FitToBounds(
            scene.ComputeBounds(),
            Camera.DefaultAzimuthDegrees,
            Camera.DefaultElevationDegrees,
            width / (double)height);
        return Render(scene, camera, width, height, options, cancellationToken);
    }

    /// <summary>Validates the output size.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    private static void ValidateSize(int width, int height)
    {
        if (width < 1 || width > Surface.MaxDimension)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, $"The width must be between 1 and {Surface.MaxDimension}.");
        }

        if (height < 1 || height > Surface.MaxDimension)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, $"The height must be between 1 and {Surface.MaxDimension}.");
        }
    }

    /// <summary>
    ///     Flattens the scene and transforms every instance's vertices into view space in double precision,
    ///     gathering the depth statistics used for the clip range and depth tolerance.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="view">The view transform.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="stats">Receives the depth statistics.</param>
    /// <returns>The transformed instances in draw order.</returns>
    private static List<ViewInstance> TransformInstances(
        Scene scene,
        in ViewTransform view,
        CancellationToken cancellationToken,
        out DepthStatistics stats)
    {
        var result = new List<ViewInstance>();
        var min = new Vector3D(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
        var max = new Vector3D(double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity);

        foreach (var instance in scene.EnumerateInstances())
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Camera-relative composition: the world translation cancels the eye position in double precision
            var modelToView = view.ComposeModelToView(instance.World);
            var source = instance.Mesh.VertexSpan;
            var vertices = new Vector3D[source.Length];
            for (var i = 0; i < vertices.Length; i++)
            {
                var v = modelToView.TransformPoint(source[i]);
                vertices[i] = v;
                min = Vector3D.Min(min, v);
                max = Vector3D.Max(max, v);
            }

            result.Add(new ViewInstance(instance, vertices));
        }

        stats = DepthStatistics.From(min, max);
        return result;
    }

    /// <summary>
    ///     Chooses the near and far clip distances: the camera's values when set, otherwise values
    ///     derived from the scene's view-depth range.
    /// </summary>
    /// <param name="camera">The camera.</param>
    /// <param name="stats">The depth statistics.</param>
    /// <returns>The near and far distances.</returns>
    private static (double Near, double Far) ResolveClipRange(Camera camera, in DepthStatistics stats)
    {
        // A scene with no vertices has nothing to clip; any positive range will do
        if (!stats.HasVertices)
        {
            return (camera.NearPlane ?? 1.0, camera.FarPlane ?? 2.0);
        }

        double near;
        if (camera.Projection == CameraProjection.Perspective)
        {
            // Keep the near plane in front of the nearest vertex but never at or behind the eye
            var floor = NearFloorFactor * Math.Max(Math.Max(stats.MaxDepth, stats.Diameter), Tiny);
            near = stats.MinDepth > floor ? stats.MinDepth * NearMargin : floor;
        }
        else
        {
            // Orthographic depth may be negative; step slightly in front of the nearest vertex
            near = stats.MinDepth - (0.01 * Math.Max(stats.MaxDepth - stats.MinDepth, Math.Max(stats.Diameter, Tiny)));
        }

        var far = stats.MaxDepth > 0.0 ? stats.MaxDepth * FarMargin : stats.MaxDepth + (0.01 * Math.Max(stats.Diameter, Tiny));
        return (camera.NearPlane ?? near, camera.FarPlane ?? Math.Max(far, near + Tiny));
    }

    /// <summary>
    ///     Shades, clips and projects every triangle of every instance into screen-space triangles in draw order.
    /// </summary>
    /// <param name="instances">The transformed instances.</param>
    /// <param name="view">The view transform (for orienting world-space lights).</param>
    /// <param name="projection">The sample projection.</param>
    /// <param name="options">The render options.</param>
    /// <param name="leafOfInstance">The CSG leaf of every instance (indexed by <see cref="SceneInstance.Order"/>), or <see langword="null"/> on the z-buffer path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The prepared triangles.</returns>
    private static List<ScreenTriangle> BuildScreenTriangles(
        List<ViewInstance> instances,
        in ViewTransform view,
        in SampleProjection projection,
        SceneRenderOptions options,
        int[]? leafOfInstance,
        CancellationToken cancellationToken)
    {
        var total = 0;
        foreach (var instance in instances)
        {
            total += instance.Source.Mesh.TriangleCount;
        }

        var output = new List<ScreenTriangle>(total);
        var worldToView = view.WorldToView;
        var processed = 0;
        foreach (var instance in instances)
        {
            var baseColor = instance.Source.Appearance?.Color ?? options.DefaultColor;
            var mirrored = instance.Source.World.Determinant < 0.0;
            var leaf = leafOfInstance is null ? -1 : leafOfInstance[instance.Source.Order];
            var indices = instance.Source.Mesh.IndexSpan;
            for (var t = 0; t < indices.Length; t += 3)
            {
                if (++processed % CancellationCheckInterval == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                // A mirroring transform reverses winding, so swap two indices to keep outward = counter-clockwise
                var i1 = mirrored ? indices[t + 2] : indices[t + 1];
                var i2 = mirrored ? indices[t + 1] : indices[t + 2];
                var triangle = PrepareTriangle(instance, indices[t], i1, i2, baseColor, leaf, worldToView, options);
                if (triangle is { } prepared)
                {
                    TriangleRasterizer.Setup(in prepared, in projection, output);
                }
            }
        }

        return output;
    }

    /// <summary>
    ///     Builds the shaded view-space triangle for three vertex indices, or <see langword="null"/> for degenerate geometry.
    /// </summary>
    /// <param name="instance">The owning instance.</param>
    /// <param name="i0">The first vertex index.</param>
    /// <param name="i1">The second vertex index (after any winding correction).</param>
    /// <param name="i2">The third vertex index (after any winding correction).</param>
    /// <param name="baseColor">The surface color.</param>
    /// <param name="leaf">The CSG leaf of the instance, or a negative value on the z-buffer path (no CSG shades are computed).</param>
    /// <param name="worldToView">The world-to-view matrix for orienting world-space lights.</param>
    /// <param name="options">The render options.</param>
    /// <returns>The triangle, or <see langword="null"/> when it has no usable normal.</returns>
    private static ViewTriangle? PrepareTriangle(
        in ViewInstance instance,
        int i0,
        int i1,
        int i2,
        Rgba32 baseColor,
        int leaf,
        Matrix4x4D worldToView,
        SceneRenderOptions options)
    {
        var a = instance.Vertices[i0];
        var b = instance.Vertices[i1];
        var c = instance.Vertices[i2];

        // Face normal from the double-precision view-space vertices; zero-area and overflowed triangles are skipped
        var normal = Vector3D.Cross(b - a, c - a).Normalize();
        if (!(normal.LengthSquared > 0.0) || !normal.IsFinite)
        {
            return null;
        }

        var lights = options.EffectiveLights;
        var front = LightingModel.Shade(normal, baseColor, lights, options.SpecularStrength, options.SpecularPower, worldToView);
        var back = LightingModel.ShadeBackFace(normal, options.BackFaceColor, lights, options.SpecularStrength, options.SpecularPower, worldToView);
        if (leaf < 0)
        {
            return new ViewTriangle
            {
                A = Narrow(a),
                B = Narrow(b),
                C = Narrow(c),
                Priority = instance.Source.Order,
                FrontColor = front,
                BackColor = back,
            };
        }

        // CSG shading: an entering event shows the result surface (the leaf's own color, lit from the
        // viewer-facing normal, so a back-facing subtrahend triangle is a cut surface with a flipped
        // normal); a leaving event means the viewer is inside material and shows the back-face color.
        return new ViewTriangle
        {
            A = Narrow(a),
            B = Narrow(b),
            C = Narrow(c),
            Priority = instance.Source.Order,
            FrontColor = front,
            BackColor = back,
            Leaf = leaf,
            FrontEnterColor = front,
            FrontExitColor = LightingModel.Shade(normal, options.BackFaceColor, lights, options.SpecularStrength, options.SpecularPower, worldToView),
            BackEnterColor = LightingModel.Shade(-normal, baseColor, lights, options.SpecularStrength, options.SpecularPower, worldToView),
            BackExitColor = back,
        };
    }

    /// <summary>
    ///     Narrows a double-precision view-space vertex to single precision (the documented precision
    ///     step) and promotes it back exactly for clipping and projection.
    /// </summary>
    /// <param name="vertex">The view-space vertex.</param>
    /// <returns>The clip vertex holding the single-precision position.</returns>
    private static ClipVertex Narrow(Vector3D vertex)
    {
        var f = vertex.ToVector3();
        return new ClipVertex(f.X, f.Y, f.Z);
    }

    /// <summary>
    ///     Renders all tiles, sequentially or in parallel, honouring cancellation.
    /// </summary>
    /// <param name="job">The shared, read-only job data.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private static void RenderTiles(TileJob job, CancellationToken cancellationToken)
    {
        var tileCount = job.Bins.TilesX * job.Bins.TilesY;
        var capacity = TilePixels * job.Options.Supersampling * TilePixels * job.Options.Supersampling;

        if (job.Options.MaxDegreeOfParallelism == 1)
        {
            // Sequential: one worker on the calling thread; a budget exception propagates unchanged
            var worker = new TileWorker(capacity, job.Program, job.Options.MaxFragmentsPerTile);
            for (var tile = 0; tile < tileCount; tile++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RenderTile(job, tile, worker, cancellationToken);
            }

            return;
        }

        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = job.Options.MaxDegreeOfParallelism,
        };

        try
        {
            // Tiles are independent and write disjoint pixels; each worker reuses one workspace and fragment buffer
            Parallel.For(
                0,
                tileCount,
                parallelOptions,
                () => new TileWorker(capacity, job.Program, job.Options.MaxFragmentsPerTile),
                (tile, _, worker) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    RenderTile(job, tile, worker, cancellationToken);
                    return worker;
                },
                _ => { });
        }
        catch (AggregateException ex) when (ex.InnerExceptions.Count > 0 && ex.InnerExceptions.All(e => e is OperationCanceledException))
        {
            // Surface cooperative cancellation as a plain OperationCanceledException, never an AggregateException
            throw new OperationCanceledException(cancellationToken);
        }
        catch (AggregateException ex) when (ex.Flatten().InnerExceptions.OfType<RenderBudgetExceededException>().Any())
        {
            // Which tile exceeds the budget first is a scheduling accident, but every budget exception carries
            // the same tile-independent content, so rethrow one unwrapped (cancellation still takes precedence)
            cancellationToken.ThrowIfCancellationRequested();
            var budget = ex.Flatten().InnerExceptions.OfType<RenderBudgetExceededException>().First();
            ExceptionDispatchInfo.Capture(budget).Throw();
        }
    }

    /// <summary>
    ///     Rasterizes and resolves one tile.
    /// </summary>
    /// <param name="job">The shared job data.</param>
    /// <param name="tile">The row-major tile index.</param>
    /// <param name="worker">The worker's sample buffers (and fragment storage on the CSG path).</param>
    /// <param name="cancellationToken">The cancellation token (observed periodically on the CSG path).</param>
    /// <exception cref="RenderBudgetExceededException">Thrown on the CSG path when the tile needs more fragments than the budget.</exception>
    private static void RenderTile(TileJob job, int tile, TileWorker worker, CancellationToken cancellationToken)
    {
        var k = job.Options.Supersampling;
        var surface = job.Surface;
        var workspace = worker.Workspace;
        var tx = tile % job.Bins.TilesX;
        var ty = tile / job.Bins.TilesX;

        // Output pixel rectangle (partial tiles at the right/bottom edge) and its sample rectangle
        var pixels = new TileRect(
            tx * TilePixels,
            ty * TilePixels,
            Math.Min((tx + 1) * TilePixels, surface.Width),
            Math.Min((ty + 1) * TilePixels, surface.Height));
        var samples = new TileRect(pixels.X0 * k, pixels.Y0 * k, pixels.X1 * k, pixels.Y1 * k);
        workspace.Begin(samples.X0, samples.Y0, samples.Width, samples.Height);

        if (worker.Fragments is { } fragments && worker.Resolver is { } resolver)
        {
            ResolveTileWithFragments(job, tile, samples, workspace, fragments, resolver, cancellationToken);
            SampleResolver.Resolve(workspace, k, job.Options.BackgroundColor, surface, pixels);
            return;
        }

        // Triangles arrive in ascending draw order, which the depth tie rule relies on
        foreach (var index in job.Bins.GetTriangles(tile))
        {
            var triangle = job.Triangles[index];
            var visitor = new DepthTestVisitor(workspace, in triangle, job.Epsilon, job.FarDepth);
            TriangleRasterizer.Rasterize(in triangle, samples, ref visitor);
        }

        SampleResolver.Resolve(workspace, k, job.Options.BackgroundColor, surface, pixels);
    }

    /// <summary>
    ///     Fills the tile workspace for the CSG path: counts and collects every fragment of the tile
    ///     (budget-checked), then lets the resolver pick the visible surface of each sample.
    /// </summary>
    /// <param name="job">The shared job data.</param>
    /// <param name="tile">The row-major tile index.</param>
    /// <param name="samples">The tile's sample rectangle.</param>
    /// <param name="workspace">The begun tile workspace receiving the visible surfaces.</param>
    /// <param name="fragments">The worker's fragment buffer.</param>
    /// <param name="resolver">The worker's CSG resolver.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private static void ResolveTileWithFragments(
        TileJob job,
        int tile,
        TileRect samples,
        TileWorkspace workspace,
        FragmentBuffer fragments,
        CsgResolver resolver,
        CancellationToken cancellationToken)
    {
        var binned = job.Bins.GetTriangles(tile);
        fragments.Begin(samples.X0, samples.Y0, samples.Width, samples.Height);

        // Count pass (throws as soon as the tile's total exceeds the budget), then the fill pass over the same triangles
        for (var i = 0; i < binned.Length; i++)
        {
            if ((i & (CancellationCheckInterval - 1)) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            fragments.CountTriangle(job.Triangles[binned[i]]);
        }

        fragments.EndCounting();
        for (var i = 0; i < binned.Length; i++)
        {
            if ((i & (CancellationCheckInterval - 1)) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var index = binned[i];
            fragments.AddTriangle(index, job.Triangles[index]);
        }

        resolver.ResolveTile(fragments, CollectionsMarshal.AsSpan(job.Triangles), workspace, job.Epsilon, job.FarDepth, cancellationToken);
    }

    /// <summary>The per-thread state of a tile worker: sample buffers and, on the CSG path, fragment storage and resolver scratch.</summary>
    private sealed class TileWorker
    {
        /// <summary>
        ///     Creates a worker.
        /// </summary>
        /// <param name="capacity">The maximum number of samples in a tile.</param>
        /// <param name="program">The CSG program, or <see langword="null"/> on the z-buffer path.</param>
        /// <param name="maxFragments">The fragment budget per tile.</param>
        public TileWorker(int capacity, CsgProgram? program, long maxFragments)
        {
            Workspace = new TileWorkspace(capacity);
            if (program is not null)
            {
                Fragments = new FragmentBuffer(maxFragments);
                Resolver = new CsgResolver(program);
            }
        }

        /// <summary>Gets the sample buffers.</summary>
        public TileWorkspace Workspace { get; }

        /// <summary>Gets the fragment storage, or <see langword="null"/> on the z-buffer path.</summary>
        public FragmentBuffer? Fragments { get; }

        /// <summary>Gets the CSG resolver, or <see langword="null"/> on the z-buffer path.</summary>
        public CsgResolver? Resolver { get; }
    }

    /// <summary>One flattened mesh instance with its vertices already in double-precision view space.</summary>
    /// <param name="Source">The scene instance (mesh, world transform, appearance, draw order).</param>
    /// <param name="Vertices">The mesh vertices in camera-relative view space.</param>
    private readonly record struct ViewInstance(SceneInstance Source, Vector3D[] Vertices);

    /// <summary>The view-depth statistics of a scene.</summary>
    /// <param name="HasVertices">Whether any vertex exists.</param>
    /// <param name="MinDepth">The smallest view depth (distance along the view direction).</param>
    /// <param name="MaxDepth">The largest view depth.</param>
    /// <param name="Diameter">The length of the diagonal of the view-space bounding box.</param>
    private readonly record struct DepthStatistics(bool HasVertices, double MinDepth, double MaxDepth, double Diameter)
    {
        /// <summary>Builds the statistics from the view-space bounding box of all vertices.</summary>
        /// <param name="min">The component-wise minimum (positive infinity when no vertices).</param>
        /// <param name="max">The component-wise maximum (negative infinity when no vertices).</param>
        /// <returns>The statistics.</returns>
        public static DepthStatistics From(Vector3D min, Vector3D max)
        {
            // View depth is -Z, so the nearest vertex has the largest Z
            if (min.X > max.X)
            {
                return new DepthStatistics(false, 0.0, 0.0, 0.0);
            }

            return new DepthStatistics(true, -max.Z, -min.Z, (max - min).Length);
        }
    }

    /// <summary>Read-only data shared by all tile workers.</summary>
    /// <param name="Triangles">The prepared triangles.</param>
    /// <param name="Bins">The tile bins.</param>
    /// <param name="Surface">The output surface.</param>
    /// <param name="Options">The render options.</param>
    /// <param name="Epsilon">The depth tie tolerance.</param>
    /// <param name="FarDepth">The far distance.</param>
    /// <param name="Program">The compiled CSG program, or <see langword="null"/> for the z-buffer fast path.</param>
    private sealed record TileJob(
        List<ScreenTriangle> Triangles,
        TileBins Bins,
        Surface Surface,
        SceneRenderOptions Options,
        float Epsilon,
        float FarDepth,
        CsgProgram? Program);
}
