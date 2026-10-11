namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio

/// <summary>
///     Implements the <see cref="VsdxDocument"/> unsupported-feature diagnostics channel. Shape-tree
///     resolution is synchronous, cached per page, and partly static, so resolvers record each
///     degraded construct through <see cref="ReportUnsupported"/> into a per-resolution collector
///     (a thread-static, scoped to a single <c>GetPageShapes</c> resolution); the collected
///     distinct feature tokens are stored with the page's cached tree and replayed on every
///     <c>Render</c> call - to <see cref="VsdxRenderOptions.UnsupportedFeatureHandler"/>, or as a
///     <see cref="VsdxUnsupportedFeatureException"/> in strict mode.
/// </summary>
public sealed partial class VsdxDocument
{
    /// <summary>The collector active for the current thread's in-progress page resolution, or <see langword="null"/> outside one.</summary>
    [ThreadStatic]
    private static SortedDictionary<string, string>? t_activeFeatures;

    /// <summary>Per-page-index distinct unsupported features found while resolving that page (token to first message).</summary>
    private readonly Dictionary<int, IReadOnlyDictionary<string, string>> _pageUnsupportedFeatures = new();

    /// <summary>Swaps the current thread's active collector, returning the previous one (used to scope a page resolution).</summary>
    /// <param name="next">The collector to activate, or the previous collector to restore.</param>
    /// <returns>The collector that was active before the swap.</returns>
    private static SortedDictionary<string, string>? SwapActiveFeatures(SortedDictionary<string, string>? next)
    {
        var previous = t_activeFeatures;
        t_activeFeatures = next;
        return previous;
    }

    /// <summary>
    ///     Records a degraded construct for the page currently being resolved. A no-op outside a
    ///     page resolution. Only the first message per distinct <paramref name="feature"/> token is kept.
    /// </summary>
    /// <param name="feature">The stable feature token.</param>
    /// <param name="message">The human-readable description.</param>
    internal static void ReportUnsupported(string feature, string message)
    {
        t_activeFeatures?.TryAdd(feature, message);
    }

    /// <summary>
    ///     Delivers the page's collected unsupported features per <paramref name="options"/>:
    ///     throws for the first (ordinal order) feature when
    ///     <see cref="VsdxRenderOptions.ThrowOnUnsupportedFeature"/> is set, otherwise invokes
    ///     <see cref="VsdxRenderOptions.UnsupportedFeatureHandler"/> once per distinct feature.
    /// </summary>
    /// <param name="pageIndex">The already-resolved page index.</param>
    /// <param name="options">The effective render options.</param>
    private void DeliverUnsupportedFeatures(int pageIndex, VsdxRenderOptions options)
    {
        if (options.UnsupportedFeatureHandler is null && !options.ThrowOnUnsupportedFeature)
        {
            return;
        }

        if (!_pageUnsupportedFeatures.TryGetValue(pageIndex, out var features))
        {
            return;
        }

        foreach (var (feature, message) in features)
        {
            if (options.ThrowOnUnsupportedFeature)
            {
                throw new VsdxUnsupportedFeatureException(feature, message);
            }

            options.UnsupportedFeatureHandler!(new VsdxUnsupportedFeature(feature, message));
        }
    }
}
