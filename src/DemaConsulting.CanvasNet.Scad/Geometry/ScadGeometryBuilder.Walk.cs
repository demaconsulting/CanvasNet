using DemaConsulting.CanvasNet.Scad.Evaluation;
using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <content>
///     The iterative post-order engine.
/// </content>
internal sealed partial class ScadGeometryBuilder
{
    /// <summary>
    ///     One level of the explicit stack: a node whose children are being visited and the results gathered so far.
    /// </summary>
    private sealed class Frame
    {
        /// <summary>
        ///     Initializes a frame.
        /// </summary>
        /// <param name="node">The instance the frame belongs to; <see langword="null"/> for the top-level forest.</param>
        /// <param name="children">The children to visit.</param>
        /// <param name="combine">Combines the results of the children into the result of the node.</param>
        public Frame(ScadInstance? node, IReadOnlyList<ScadInstance> children, ContainerBuilder combine)
        {
            Node = node;
            Children = children;
            Combine = combine;
        }

        /// <summary>Gets the instance of the frame, or <see langword="null"/> for the top-level forest.</summary>
        public ScadInstance? Node { get; }

        /// <summary>Gets the children to visit.</summary>
        public IReadOnlyList<ScadInstance> Children { get; }

        /// <summary>Gets the function that combines the children's results.</summary>
        public ContainerBuilder Combine { get; }

        /// <summary>Gets or sets the index of the next child to visit.</summary>
        public int Next { get; set; }

        /// <summary>Gets the results of the children visited so far, in order (one per child, empty ones included).</summary>
        public List<ScadGeo> Results { get; } = [];
    }

    /// <summary>
    ///     Builds the implicit union of a list of sibling instances.
    /// </summary>
    /// <remarks>
    ///     The walk is a loop over an explicit stack of frames: a leaf is built in place, a container (a group, a
    ///     boolean, a transform or an extrusion) pushes a frame for its children, and a finished frame is combined by
    ///     the container's builder and its result handed to the parent frame. Nothing recurses, so the depth of the
    ///     tree is limited by memory only.
    /// </remarks>
    /// <param name="roots">The sibling instances, in order.</param>
    /// <returns>The union of their results.</returns>
    private ScadGeo BuildForest(IReadOnlyList<ScadInstance> roots)
    {
        var stack = new List<Frame> { new(null, roots, ScadBooleans.Union) };
        while (true)
        {
            var top = stack[^1];
            if (top.Next < top.Children.Count)
            {
                var child = top.Children[top.Next++];
                _context.Tick();
                var combiner = Enter(child, out var immediate);
                if (combiner is null)
                {
                    top.Results.Add(ApplyModifiers(child, Guard(child, immediate!)));
                }
                else
                {
                    // Everything below a hull or a Minkowski sum is built without overlays (they are never drawn there)
                    if (IsConvexCombination(combiner))
                    {
                        _convexDepth++;
                    }

                    stack.Add(new Frame(child, child.Children, combiner));
                }

                continue;
            }

            // All children are visited: combine them and hand the result to the parent
            stack.RemoveAt(stack.Count - 1);
            if (IsConvexCombination(top.Combine))
            {
                _convexDepth--;
            }

            var geo = top.Combine(top.Node, top.Results, _context);
            if (top.Node is not null)
            {
                geo = ApplyModifiers(top.Node, Guard(top.Node, geo));
            }

            if (stack.Count == 0)
            {
                return geo;
            }

            stack[^1].Results.Add(geo);
        }
    }

    /// <summary>
    ///     Tells whether a container builder is <c>hull</c> or <c>minkowski</c>, which discard the overlays of their children.
    /// </summary>
    /// <param name="combiner">The builder.</param>
    /// <returns><see langword="true"/> for the hull and the Minkowski sum.</returns>
    private static bool IsConvexCombination(ContainerBuilder combiner) =>
        combiner == ScadHulls.Hull || combiner == ScadHulls.Minkowski;

    /// <summary>
    ///     Rejects a result whose coordinates are not finite or exceed <see cref="ScadGeo.MaxCoordinate"/>.
    /// </summary>
    /// <remarks>
    ///     Every instance is checked as soon as it is built, so no mesh of the scene can be farther from the origin
    ///     than the limit (a transform that moves or scales geometry out of range is itself the instance that is
    ///     rejected), and the camera fitting and the renderer cannot overflow. The check covers the hidden operands of
    ///     a difference or an intersection too (see <see cref="ScadGeo.Reach"/>).
    /// </remarks>
    /// <param name="node">The instance that produced the result.</param>
    /// <param name="geo">The result.</param>
    /// <returns>The result, or <see cref="ScadGeo.Empty"/> after reporting <c>scad-geometry-rejected</c> at the instance.</returns>
    private ScadGeo Guard(ScadInstance node, ScadGeo geo)
    {
        if (geo.Kind == ScadGeoKind.Empty || geo.IsWithinSafeRange())
        {
            return geo;
        }

        _context.Reporter.Unsupported(
            ScadFeatureIds.GeometryRejected,
            $"{node.Name}: the geometry was rejected: its coordinates are not finite or exceed 1e100 in magnitude; the instance is omitted",
            node.Location);
        return ScadGeo.Empty;
    }

    /// <summary>
    ///     Handles an instance on entry: builds it in place or announces that its children must be visited.
    /// </summary>
    /// <remarks>
    ///     The <c>#</c> and <c>%</c> modifiers do not change how an instance is entered: its children are visited
    ///     and its result is built as usual, and <see cref="ApplyModifiers"/> turns the result into the highlight
    ///     or the ghost afterwards, so everything inside (including unsupported modules) is still reported.
    /// </remarks>
    /// <param name="node">The instance.</param>
    /// <param name="immediate">The result when the instance needed no children; otherwise <see langword="null"/>.</param>
    /// <returns>
    ///     <see langword="null"/> when the instance was handled in place (a leaf or an unsupported module) and
    ///     <paramref name="immediate"/> is its result; otherwise the function that combines the results of the
    ///     instance's children.
    /// </returns>
    private ContainerBuilder? Enter(ScadInstance node, out ScadGeo? immediate)
    {
        immediate = null;

        // A group (user module, for, modifier wrapper) is the implicit union of its children, whatever its name
        if (node.Kind == ScadInstanceKind.Group)
        {
            return ScadBooleans.Union;
        }

        if (ContainerModules.TryGetValue(node.Name, out var container))
        {
            return container;
        }

        immediate = LeafModules.TryGetValue(node.Name, out var builder)
            ? builder(node, _context)
            : ReportUnsupportedModule(node, _context);
        return null;
    }
}
