// Portions derived from Vega (BSD-3-Clause): the Overlap transform of vega-view-transforms; see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Guides;

/// <summary>
///     Removes the labels of a guide that overlap their neighbors, with Vega's parity and greedy methods.
/// </summary>
/// <remarks>
///     <para>
///         The rules are those of Vega's <c>Overlap</c> transform, which an axis (<c>labelOverlap</c>) and a gradient
///         legend (<c>labelOverlap</c>) share. Only labels whose box is wider and taller than a pixel take part (a label
///         without content is skipped). With at least three such labels, when any two neighbors are closer than the
///         separation, the method hides labels and repeats until nothing overlaps or fewer than three labels are left: the
///         <b>parity</b> method keeps the labels at even positions and hides the odd ones, the <b>greedy</b> method hides every
///         label that overlaps the last label that stays. The last label of the guide is kept visible when the rounds would
///         have hidden it (it is swapped for the last label that stayed).
///     </para>
///     <para>
///         The work is bounded by the number of labels (the callers bound it by <see cref="VegaLiteLimits.MaxTicks"/> or the
///         legend entry limits before they create a label) and by the number of rounds, at most the base-2 logarithm of
///         that number for parity and one pass per round for greedy; the cancellation token is polled between the rounds. The
///         class is stateless and safe for concurrent use.
///     </para>
/// </remarks>
internal static class LabelOverlap
{
    /// <summary>The method that keeps every other label.</summary>
    public const string Parity = "parity";

    /// <summary>The method that hides each label that overlaps the last one kept.</summary>
    public const string Greedy = "greedy";

    /// <summary>
    ///     Reads the <c>labelOverlap</c> setting: <see langword="true"/> is the parity method, <c>"parity"</c> and <c>"greedy"</c> name
    ///     their method, and <see langword="false"/> or anything else switches the removal off.
    /// </summary>
    /// <param name="node">The setting, or null when it is not defined.</param>
    /// <returns>The method (<see cref="Parity"/> or <see cref="Greedy"/>), or null for no overlap removal.</returns>
    public static string? FromSetting(ConfigNode? node) => node?.Kind switch
    {
        ConfigNodeKind.Boolean => node.BoolValue ? Parity : null,
        ConfigNodeKind.String when node.StringValue == Greedy => Greedy,
        ConfigNodeKind.String when node.StringValue == Parity => Parity,
        _ => null,
    };

    /// <summary>
    ///     Finds the labels that Vega's overlap removal hides.
    /// </summary>
    /// <param name="labels">The labels with text, in guide order.</param>
    /// <param name="measurer">The text measurer that gives the box of a label.</param>
    /// <param name="method">The method: <see cref="Parity"/> or <see cref="Greedy"/>; null hides nothing.</param>
    /// <param name="separation">The smallest gap between two labels in pixels (Vega's <c>labelSeparation</c>).</param>
    /// <param name="cancellationToken">A token that stops the work between two rounds.</param>
    /// <returns>The labels to hide (reference equality).</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="labels"/> or <paramref name="measurer"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public static HashSet<SceneText> Hidden(IReadOnlyList<SceneText> labels, TextMeasurer measurer, string? method, double separation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(measurer);
        var hidden = new HashSet<SceneText>();
        if (method is null)
        {
            return hidden;
        }

        var source = labels.Where(l => HasBounds(measurer.GetBounds(l))).ToList();
        var items = source;
        if (items.Count < 3 || !HasOverlap(items, measurer, separation))
        {
            return hidden;
        }

        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            items = method == Greedy ? RunGreedy(items, measurer, separation, hidden) : RunParity(items, hidden);
        }
        while (items.Count >= 3 && HasOverlap(items, measurer, separation));

        if (items.Count < 3 && hidden.Contains(source[^1]))
        {
            if (items.Count > 1)
            {
                hidden.Add(items[^1]);
            }

            hidden.Remove(source[^1]);
        }

        return hidden;
    }

    /// <summary>
    ///     Tells whether a label box is larger than a pixel in both directions (Vega skips labels without content).
    /// </summary>
    /// <param name="bounds">The bounds.</param>
    /// <returns><see langword="true"/> when the label takes part in the overlap removal.</returns>
    private static bool HasBounds(SceneBounds bounds) => bounds.Width > 1 && bounds.Height > 1;

    /// <summary>
    ///     Tells whether any two neighbors overlap, with a separation in pixels.
    /// </summary>
    /// <param name="items">The labels in guide order.</param>
    /// <param name="measurer">The text measurer.</param>
    /// <param name="separation">The separation.</param>
    /// <returns><see langword="true"/> when at least one pair overlaps.</returns>
    private static bool HasOverlap(List<SceneText> items, TextMeasurer measurer, double separation)
    {
        for (var i = 1; i < items.Count; i++)
        {
            if (Intersect(measurer.GetBounds(items[i - 1]), measurer.GetBounds(items[i]), separation))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Tells whether two boxes are closer than a separation (Vega's <c>intersect</c>).
    /// </summary>
    /// <param name="a">The first box.</param>
    /// <param name="b">The second box.</param>
    /// <param name="separation">The separation.</param>
    /// <returns><see langword="true"/> when the gap between the boxes is smaller than the separation.</returns>
    private static bool Intersect(SceneBounds a, SceneBounds b, double separation) =>
        separation > Math.Max(Math.Max(b.X1 - a.X2, a.X1 - b.X2), Math.Max(b.Y1 - a.Y2, a.Y1 - b.Y2));

    /// <summary>
    ///     The parity method: keeps the labels at even positions and hides the others.
    /// </summary>
    /// <param name="items">The labels.</param>
    /// <param name="hidden">Receives the hidden labels.</param>
    /// <returns>The labels that stay.</returns>
    private static List<SceneText> RunParity(List<SceneText> items, HashSet<SceneText> hidden)
    {
        var kept = new List<SceneText>();
        for (var i = 0; i < items.Count; i++)
        {
            if (i % 2 == 0)
            {
                kept.Add(items[i]);
            }
            else
            {
                hidden.Add(items[i]);
            }
        }

        return kept;
    }

    /// <summary>
    ///     The greedy method: hides every label that overlaps the last label that stays.
    /// </summary>
    /// <param name="items">The labels.</param>
    /// <param name="measurer">The text measurer.</param>
    /// <param name="separation">The separation in pixels.</param>
    /// <param name="hidden">Receives the hidden labels.</param>
    /// <returns>The labels that stay.</returns>
    private static List<SceneText> RunGreedy(List<SceneText> items, TextMeasurer measurer, double separation, HashSet<SceneText> hidden)
    {
        var kept = new List<SceneText>();
        foreach (var item in items)
        {
            if (kept.Count == 0 || !Intersect(measurer.GetBounds(kept[^1]), measurer.GetBounds(item), separation))
            {
                kept.Add(item);
            }
            else
            {
                hidden.Add(item);
            }
        }

        return kept;
    }
}
