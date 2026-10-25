using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Scad.Evaluation;
using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <content>
///     The iterative post-order engine and the implicit union.
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
        /// <param name="children">The children to visit.</param>
        public Frame(IReadOnlyList<ScadInstance> children)
        {
            Children = children;
        }

        /// <summary>Gets the children to visit.</summary>
        public IReadOnlyList<ScadInstance> Children { get; }

        /// <summary>Gets or sets the index of the next child to visit.</summary>
        public int Next { get; set; }

        /// <summary>Gets the results of the children visited so far, in order.</summary>
        public List<ScadGeo> Results { get; } = [];
    }

    /// <summary>
    ///     Builds the implicit union of a list of sibling instances.
    /// </summary>
    /// <remarks>
    ///     The walk is a loop over an explicit stack of frames: a leaf is built in place, a group or union pushes a
    ///     frame for its children, and a finished frame is combined and its result handed to the parent frame. The
    ///     location of a frame's own instance is not needed once it is combined, so frames hold none.
    /// </remarks>
    /// <param name="roots">The sibling instances, in order.</param>
    /// <returns>The union of their results.</returns>
    private ScadGeo BuildForest(IReadOnlyList<ScadInstance> roots)
    {
        var stack = new List<Frame> { new(roots) };
        while (true)
        {
            var top = stack[^1];
            if (top.Next < top.Children.Count)
            {
                var child = top.Children[top.Next++];
                _context.Tick();
                var immediate = Enter(child);
                if (immediate is null)
                {
                    // A union-like node: visit its children first
                    stack.Add(new Frame(child.Children));
                }
                else
                {
                    top.Results.Add(immediate);
                }

                continue;
            }

            // All children are visited: combine them and hand the result to the parent
            stack.RemoveAt(stack.Count - 1);
            var geo = CombineUnion(top.Results);
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
    /// <returns>
    ///     The result when the instance needed no children (a leaf, an unsupported module or an omitted
    ///     background subtree); <see langword="null"/> when its children are to be combined as an implicit union.
    /// </returns>
    private ScadGeo? Enter(ScadInstance node)
    {
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
            return ScadGeo.Empty;
        }

        if (node.Kind == ScadInstanceKind.Group || UnionModules.Contains(node.Name))
        {
            return null;
        }

        return LeafModules.TryGetValue(node.Name, out var builder)
            ? builder(node, _context)
            : ReportUnsupportedModule(node, _context);
    }

    /// <summary>
    ///     Combines the results of sibling instances into their implicit union.
    /// </summary>
    /// <remarks>
    ///     Empty results vanish. Solids become one <see cref="GroupNode"/> (or the single solid itself); 2D shapes
    ///     concatenate. When both dimensions are present the 2D shapes are dropped with a warning at the location
    ///     of the first dropped shape, so a mixed union is never silently mis-rendered.
    /// </remarks>
    /// <param name="results">The results to combine, in order.</param>
    /// <returns>The union.</returns>
    private ScadGeo CombineUnion(List<ScadGeo> results)
    {
        List<SceneNode>? solids = null;
        List<ScadFlatLeaf>? leaves = null;
        foreach (var result in results)
        {
            if (result.Kind == ScadGeoKind.Solid)
            {
                (solids ??= []).Add(result.Node!);
            }
            else if (result.Kind == ScadGeoKind.Flat)
            {
                (leaves ??= []).AddRange(result.Leaves!);
            }
        }

        if (solids is not null)
        {
            // Mixed dimensions: keep the 3D members and say what was dropped
            if (leaves is not null)
            {
                _context.Reporter.Warn("A union mixes 2D and 3D objects; the 2D objects are ignored", leaves[0].Location);
            }

            return ScadGeo.FromSolid(solids.Count == 1 ? solids[0] : new GroupNode(solids));
        }

        return leaves is null ? ScadGeo.Empty : ScadGeo.FromFlat(leaves);
    }
}
