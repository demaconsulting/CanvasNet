using DemaConsulting.CanvasNet.Scad.Evaluation;
using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <content>
///     The <c>!</c> (show only) pre-pass.
/// </content>
internal sealed partial class ScadGeometryBuilder
{
    /// <summary>
    ///     Finds the outermost instances marked with <c>!</c>, in document order.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The search is an explicit-stack depth-first walk that never descends into a marked subtree (a
    ///         <c>!</c> nested inside a marked subtree has no further effect) nor into a <c>%</c> subtree (that
    ///         subtree is omitted, so a mark inside it does not take part in the model). It visits every other
    ///         instance regardless of its module, including modules that are not built, so a mark below an
    ///         unsupported module is still honored.
    ///     </para>
    ///     <para>
    ///         The marked subtrees become the whole model without the transforms and colors of their ancestors:
    ///         the engine simply builds them instead of the roots.
    ///     </para>
    /// </remarks>
    /// <param name="roots">The top-level instances.</param>
    /// <returns>The marked instances in document order; empty when the model has no <c>!</c>.</returns>
    private static List<ScadInstance> FindShowOnlyRoots(IReadOnlyList<ScadInstance> roots)
    {
        var found = new List<ScadInstance>();
        var stack = new Stack<ScadInstance>();
        for (var i = roots.Count - 1; i >= 0; i--)
        {
            stack.Push(roots[i]);
        }

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if ((node.Modifiers & ScadModifiers.ShowOnly) != 0)
            {
                found.Add(node);
                continue;
            }

            if ((node.Modifiers & ScadModifiers.Background) != 0)
            {
                continue;
            }

            // Push in reverse so the first child is visited first
            for (var i = node.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(node.Children[i]);
            }
        }

        return found;
    }
}
