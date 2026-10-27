// cspell:ignore newsize
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Scad.Evaluation;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <content>
///     The <c>resize</c> module.
/// </content>
internal static partial class ScadTransforms
{
    /// <summary>The parameter names of <c>resize</c> in positional order.</summary>
    private static readonly string[] ResizeParameters = ["newsize", "auto", "convexity"];

    /// <summary>
    ///     Builds <c>resize(newsize, auto)</c>: scales the children about the origin so that their bounding box has
    ///     the requested size.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Per axis, a positive <c>newsize</c> component scales by <c>newsize / size</c>, where <c>size</c> is the
    ///         extent of the children along the axis; a zero component leaves the axis alone unless <c>auto</c>
    ///         applies to it, in which case it is scaled by the ratio of the first axis with a positive
    ///         <c>newsize</c> (the proportions are kept). <c>auto</c> is a flag for all axes or a vector of three
    ///         flags. An axis along which the children have no extent cannot be resized and is warned about. For 2D
    ///         children only X and Y count. <c>convexity</c> is accepted and ignored.
    ///     </para>
    ///     <para>
    ///         The extent comes from the bounds of the children (see <see cref="ScadGeo"/>). For a union of
    ///         primitives and transforms it is exact (recomputed from the vertices when a rotation made the
    ///         propagated box loose). When the children contain a <c>difference</c> or an <c>intersection</c> the
    ///         bounds are an upper bound (the first operand's, respectively the overlap), which may exceed the true
    ///         extent, and the result is reported as the unsupported feature <c>scad-resize-approximate</c>; the
    ///         resize is still applied with that extent.
    ///     </para>
    /// </remarks>
    /// <param name="node">The instance.</param>
    /// <param name="results">The results of the children.</param>
    /// <param name="context">The build context.</param>
    /// <returns>The resized union of the children.</returns>
    public static ScadGeo Resize(ScadInstance? node, List<ScadGeo> results, ScadGeometryContext context)
    {
        var instance = node!;
        var args = ScadArguments.Bind(instance, ResizeParameters, context);
        var sizeState = ReadVector(args, "newsize", 0.0, out var newSize);
        if (sizeState == ReadState.Invalid || newSize.Any(s => s < 0.0))
        {
            return Invalid(instance, context, "newsize must be a vector of one to three non-negative numbers");
        }

        if (!TryReadAuto(args, out var auto))
        {
            return Invalid(instance, context, "auto must be a boolean or a vector of one to three booleans");
        }

        if (sizeState == ReadState.Absent)
        {
            return Apply(instance, results, context, Matrix4x4D.Identity);
        }

        var union = ScadBooleans.Union(instance, results, context);
        if (union.Kind == ScadGeoKind.Empty)
        {
            return union;
        }

        // Bounds: an approximate result reports it; an exact one is recomputed when the propagated box is loose
        var bounds = union.Bounds;
        if (union.IsApproximate)
        {
            context.Reporter.Unsupported(
                ScadFeatureIds.ResizeApproximate,
                "resize: the children contain a difference or an intersection, so their size is estimated from the bounding boxes of the operands and may be larger than the result",
                instance.Location);
        }
        else if (!union.IsTight)
        {
            // Ghosts (the '%' modifier) are not part of the geometry whose size is requested
            bounds = union.Kind == ScadGeoKind.Solid ? new Scene(union.Node!).ComputeBounds(includeOverlays: false) : union.Shape!.ComputeVertexBounds();
        }

        var factors = ComputeFactors(instance, context, union.Kind == ScadGeoKind.Solid ? 3 : 2, bounds, newSize, auto);
        return Apply(instance, [union], context, Matrix4x4D.CreateScale(new Vector3D(factors[0], factors[1], factors[2])));
    }

    /// <summary>
    ///     Computes the per-axis scale factors of a resize.
    /// </summary>
    /// <param name="node">The instance (for warnings).</param>
    /// <param name="context">The build context.</param>
    /// <param name="axes">The number of axes that take part (2 for a 2D shape, 3 for a solid).</param>
    /// <param name="bounds">The extent of the children.</param>
    /// <param name="newSize">The requested size per axis; zero means not requested.</param>
    /// <param name="auto">Whether each axis follows the proportions of the first requested axis.</param>
    /// <returns>The three factors.</returns>
    private static double[] ComputeFactors(ScadInstance node, ScadGeometryContext context, int axes, BoundingBox3D bounds, double[] newSize, bool[] auto)
    {
        double[] size = [bounds.Max.X - bounds.Min.X, bounds.Max.Y - bounds.Min.Y, bounds.Max.Z - bounds.Min.Z];
        double[] factors = [1.0, 1.0, 1.0];
        string[] axisNames = ["X", "Y", "Z"];
        var reference = 0.0;
        for (var axis = 0; axis < axes; axis++)
        {
            if (!(newSize[axis] > 0.0))
            {
                continue;
            }

            if (!(size[axis] > 0.0))
            {
                context.Reporter.Warn($"resize: the children have no extent along {axisNames[axis]}; they are not resized along it", node.Location);
                continue;
            }

            factors[axis] = newSize[axis] / size[axis];
            if (reference == 0.0)
            {
                reference = factors[axis];
            }
        }

        // An automatic axis follows the first requested axis; without one there is nothing to follow
        for (var axis = 0; axis < axes; axis++)
        {
            if (!(newSize[axis] > 0.0) && auto[axis] && reference > 0.0)
            {
                factors[axis] = reference;
            }
        }

        return factors;
    }

    /// <summary>
    ///     Reads the <c>auto</c> argument.
    /// </summary>
    /// <param name="args">The bound arguments.</param>
    /// <param name="auto">One flag per axis.</param>
    /// <returns><see langword="false"/> when the argument is neither a boolean-like value nor a vector of one to three of them.</returns>
    private static bool TryReadAuto(ScadArguments args, out bool[] auto)
    {
        auto = [false, false, false];
        if (!args.TryGet("auto", out var value))
        {
            return true;
        }

        if (value.Kind != ScadValueKind.Vector)
        {
            auto = [value.IsTrue, value.IsTrue, value.IsTrue];
            return true;
        }

        if (value.AsVector.Count is < 1 or > 3)
        {
            return false;
        }

        for (var i = 0; i < value.AsVector.Count; i++)
        {
            auto[i] = value.AsVector[i].IsTrue;
        }

        return true;
    }
}
