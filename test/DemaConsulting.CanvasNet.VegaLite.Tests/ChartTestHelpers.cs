using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Marks;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     What the guide and mark stages need, prepared from a specification text the way the pipeline prepares it.
/// </summary>
/// <param name="Tree">The parsed specification.</param>
/// <param name="Table">The inline data.</param>
/// <param name="Scales">The resolved scales.</param>
/// <param name="Config">The effective configuration.</param>
internal sealed record ChartParts(SpecTree Tree, DataTable Table, ResolvedScales Scales, ResolvedConfig Config);

/// <summary>
///     Helpers that run the data, scale, axis and mark stages on a specification text without painting, so the tests can
///     assert the scene items directly.
/// </summary>
internal static class ChartTestHelpers
{
    /// <summary>
    ///     Loads the data and resolves the scales of a single view.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="callerConfig">The caller's configuration text, or null.</param>
    /// <returns>The prepared parts.</returns>
    public static ChartParts Parts(string json, string? callerConfig = null)
    {
        var tree = SpecParser.Parse(json, "test.vl.json");
        var config = ConfigResolver.Resolve(tree, callerConfig);
        var table = DataLoader.Load(tree.Root["data"], tree.SourceName);
        var scales = ScaleResolver.Resolve(tree.Root, table, config, tree.SourceName);
        return new ChartParts(tree, table, scales, config);
    }

    /// <summary>
    ///     Builds the axis of a channel for a data rectangle of a given size.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="channel">The channel, <c>x</c> or <c>y</c>.</param>
    /// <param name="width">The width of the data rectangle.</param>
    /// <param name="height">The height of the data rectangle.</param>
    /// <param name="callerConfig">The caller's configuration text, or null.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <returns>The axis, or <see langword="null"/> when the axis is switched off.</returns>
    public static AxisResult? Axis(string json, string channel, double width, double height, string? callerConfig = null, VegaLiteLimits? limits = null)
    {
        var parts = Parts(json, callerConfig);
        var scale = parts.Scales[channel] ?? throw new InvalidOperationException("The test specification has no scale for " + channel);
        var model = scale.CreateModel(channel == "x" ? width : height);
        var request = new AxisRequest(channel, parts.Tree.Root["encoding"]?[channel], scale, model, width, height, parts.Scales.MarkType);
        return new AxisBuilder(new TextMeasurer(), parts.Config, limits, parts.Tree.SourceName).Build(request);
    }

    /// <summary>
    ///     Builds the mark group for a data rectangle of a given size.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="width">The width of the data rectangle.</param>
    /// <param name="height">The height of the data rectangle.</param>
    /// <param name="callerConfig">The caller's configuration text, or null.</param>
    /// <returns>The group of mark items.</returns>
    public static SceneGroup Marks(string json, double width, double height, string? callerConfig = null)
    {
        var parts = Parts(json, callerConfig);
        return new MarkBuilder(parts.Config, sourceName: parts.Tree.SourceName).Build(parts.Tree.Root, parts.Table, parts.Scales, width, height);
    }

    /// <summary>
    ///     Gets the children of a group with a role, cast to a scene item type.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="group">The group.</param>
    /// <param name="role">The role.</param>
    /// <returns>The items in drawing order.</returns>
    public static List<T> Of<T>(SceneGroup group, string role)
        where T : SceneItem =>
        [.. group.Children.Where(c => c.Role == role).Cast<T>()];
}
