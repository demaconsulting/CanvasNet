using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Positions;

namespace DemaConsulting.CanvasNet.Viewer.Imaging;

/// <summary>
///     Feature positions of <see cref="SourceLoader"/>: the collection of the positions a Mermaid diagram or a Vega-Lite
///     chart reports while it is drawn, filtered by the selectors of <see cref="SourceOptions.Positions"/> before the cap.
/// </summary>
/// <remarks>
///     <para>
///         The renderer offers every candidate once, in its deterministic order, to a <see cref="PositionCollector"/>; only
///         the kinds a selector can match are built (<see cref="PositionCollector.IncludesKind(string)"/>). The first
///         <see cref="PositionCollector.MaxPositions"/> matches are kept and <see cref="LoadedSource.PositionsTruncated"/>
///         says whether more matched.
///     </para>
///     <para>
///         A selector that matched nothing is the warning <c>positions-unmatched</c> that names it, in the order the
///         selectors were given; with <see cref="SourceOptions.Strict"/> it is the error
///         <see cref="ErrorCodes.PositionsUnmatched"/>, raised before the loaded page is returned, so nothing is written.
///     </para>
/// </remarks>
public static partial class SourceLoader
{
    /// <summary>
    ///     Creates the collector of a load, when the options ask for positions.
    /// </summary>
    /// <param name="options">The loading options.</param>
    /// <returns>The collector, or <see langword="null"/> when <see cref="SourceOptions.Positions"/> is not set.</returns>
    private static PositionCollector? CreatePositionCollector(SourceOptions options) =>
        options.Positions is { } selection ? new PositionCollector(selection) : null;

    /// <summary>
    ///     Refuses a load whose selectors matched nothing, in strict mode, before the loaded page exists.
    /// </summary>
    /// <param name="collector">The collector of the load, or <see langword="null"/>.</param>
    /// <param name="strict">Whether the load is strict.</param>
    /// <param name="surface">The drawn surface, disposed when the load is refused.</param>
    /// <exception cref="ViewerException">Thrown with <see cref="ErrorCodes.PositionsUnmatched"/> when a selector matched nothing in strict mode.</exception>
    private static void ThrowForUnmatchedPositions(PositionCollector? collector, bool strict, Surface surface)
    {
        if (!strict || collector?.GetUnmatchedSelectors() is not { Count: > 0 } unmatched)
        {
            return;
        }

        surface.Dispose();
        throw new ViewerException(
            ErrorCodes.PositionsUnmatched,
            UnmatchedMessage(unmatched[0]),
            "List the ids that exist with the selector 'all', and check the case of ids and values.");
    }

    /// <summary>
    ///     Adds the collected positions and the warnings of the selectors that matched nothing to a loaded page.
    /// </summary>
    /// <param name="loaded">The loaded page.</param>
    /// <param name="collector">The collector of the load, or <see langword="null"/>.</param>
    /// <param name="source">The source of the warnings: <c>mermaid</c> or <c>vega-lite</c>.</param>
    /// <returns>The loaded page with its positions; the same page when no positions were asked for.</returns>
    private static LoadedSource WithPositions(LoadedSource loaded, PositionCollector? collector, string source)
    {
        if (collector is null)
        {
            return loaded;
        }

        var warnings = collector.GetUnmatchedSelectors()
            .Select(selector => new ImageDiagnostic(source, "warning", ErrorCodes.PositionsUnmatched, UnmatchedMessage(selector)));
        return loaded with
        {
            Diagnostics = [.. loaded.Diagnostics, .. warnings],
            Positions = [.. collector.Positions],
            PositionsTruncated = collector.Truncated,
        };
    }

    /// <summary>
    ///     Describes a selector that matched nothing.
    /// </summary>
    /// <param name="selector">The selector.</param>
    /// <returns>The message, which names the selector.</returns>
    private static string UnmatchedMessage(PositionSelector selector) =>
        $"The --positions selector '{selector.Text}' matched nothing.";
}
