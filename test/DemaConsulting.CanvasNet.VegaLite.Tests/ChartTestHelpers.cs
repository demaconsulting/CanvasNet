using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Marks;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     What the guide and mark stages need, prepared from a specification text the way the pipeline prepares it.
/// </summary>
/// <param name="Tree">The parsed specification.</param>
/// <param name="Table">The rows the marks draw (the Main table of the implicit pipeline).</param>
/// <param name="Scales">The resolved scales.</param>
/// <param name="Config">The effective configuration.</param>
/// <param name="Plan">The plan of the encoding.</param>
/// <param name="Data">The Raw and Main tables of the implicit pipeline and the bins.</param>
internal sealed record ChartParts(SpecTree Tree, DataTable Table, ResolvedScales Scales, ResolvedConfig Config, EncodingPlan Plan, EncodingData Data);

/// <summary>
///     Helpers that run the data, scale, axis and mark stages on a specification text without painting, so the tests can
///     assert the scene items directly.
/// </summary>
internal static class ChartTestHelpers
{
    /// <summary>
    ///     Loads the data, runs the transforms and the implicit encoding stage, and resolves the scales of a single view the way the
    ///     pipeline does.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="callerConfig">The caller's configuration text, or null.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <returns>The prepared parts.</returns>
    public static ChartParts Parts(string json, string? callerConfig = null, VegaLiteLimits? limits = null)
    {
        var effective = limits ?? VegaLiteLimits.Default;
        var token = TestContext.Current.CancellationToken;
        var tree = SpecParser.Parse(json, "test.vl.json", limits);
        var config = ConfigResolver.Resolve(tree, callerConfig);
        var loader = new DataLoader(tree.SourceName, effective, null, tree.Root["datasets"], null, token);
        var loaded = loader.Load(tree.Root["data"]);
        var values = new Dictionary<string, DataValue>(StringComparer.Ordinal);
        var evaluator = new ExpressionEvaluator(effective, token, values);
        ParameterEnvironment.Populate(tree, null, values, evaluator);
        var context = new TransformContext(tree, loader, evaluator, values, effective, token, []);
        var table = RowTransforms.Apply(loaded, tree.Root["transform"], context);
        var plan = EncodingPlan.Create(tree.Root, config);
        var data = EncodingTransforms.Apply(plan, table, context);
        var scales = ScaleResolver.Resolve(tree.Root, plan, data, config, tree.SourceName, effective, token);
        return new ChartParts(tree, data.Main, scales, config, plan, data);
    }

    /// <summary>
    ///     Runs the data, transform, encoding and scale stages of a specification (the full pipeline up to the scales).
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <returns>The prepared parts.</returns>
    public static ChartParts ResolveFull(string json, VegaLiteLimits? limits = null) => Parts(json, null, limits);

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
