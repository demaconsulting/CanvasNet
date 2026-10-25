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
                    top.Results.Add(immediate!);
                }
                else
                {
                    stack.Add(new Frame(child, child.Children, combiner));
                }

                continue;
            }

            // All children are visited: combine them and hand the result to the parent
            stack.RemoveAt(stack.Count - 1);
            var geo = top.Combine(top.Node, top.Results, _context);
            if (stack.Count == 0)
            {
                return geo;
            }

            stack[^1].Results.Add(geo);
        }
    }

    /// <summary>
    ///     Handles an instance on entry: reports its modifiers and either builds it in place or announces that
    ///     its children must be visited.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="immediate">The result when the instance needed no children; otherwise <see langword="null"/>.</param>
    /// <returns>
    ///     <see langword="null"/> when the instance was handled in place (a leaf, an unsupported module or an
    ///     omitted background subtree) and <paramref name="immediate"/> is its result; otherwise the function that
    ///     combines the results of the instance's children.
    /// </returns>
    private ContainerBuilder? Enter(ScadInstance node, out ScadGeo? immediate)
    {
        immediate = null;

        // A highlighted subtree renders normally; the degradation is reported once per location
        if ((node.Modifiers & ScadModifiers.Highlight) != 0)
        {
            _context.Reporter.Unsupported(
                ScadFeatureIds.ModifierHighlight,
                "The '#' modifier is not highlighted; the subtree is rendered normally",
                node.Location);
        }

        // A background subtree is omitted (and said so)
        if ((node.Modifiers & ScadModifiers.Background) != 0)
        {
            _context.Reporter.Unsupported(
                ScadFeatureIds.ModifierBackground,
                "The '%' modifier is not supported; the subtree is omitted",
                node.Location);
            immediate = ScadGeo.Empty;
            return null;
        }

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
