using DemaConsulting.CanvasNet.PlantUml.Activity;
using DemaConsulting.CanvasNet.PlantUml.Class;
using DemaConsulting.CanvasNet.PlantUml.Description;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Sequence;
using DemaConsulting.CanvasNet.PlantUml.State;
using DemaConsulting.CanvasNet.PlantUml.Syntax;

namespace DemaConsulting.CanvasNet.PlantUml;

/// <summary>
///     A parsed diagram of a supported type: its metadata, the reports its parser found, and the layout that turns it
///     into a scene.
/// </summary>
/// <remarks>
///     <para>
///         This is the seam between <see cref="PlantUmlDocument"/> and the diagram subsystems: a diagram parser
///         (registered in <see cref="DiagramRegistry"/>) produces it once when the document is opened, and every measure
///         and render asks it for a scene under the limits of the call.
///     </para>
///     <para>
///         Implementations are immutable and safe for concurrent use; <see cref="Layout"/> keeps no state between calls.
///     </para>
/// </remarks>
internal interface IPlantUmlDiagram
{
    /// <summary>
    ///     Gets the title of the diagram, or null.
    /// </summary>
    string? Title { get; }

    /// <summary>
    ///     Gets the number of nodes.
    /// </summary>
    int NodeCount { get; }

    /// <summary>
    ///     Gets the number of edges.
    /// </summary>
    int EdgeCount { get; }

    /// <summary>
    ///     Gets the number of subgraphs.
    /// </summary>
    int SubgraphCount { get; }

    /// <summary>
    ///     Gets the reports the parser found (unsupported statements, ignored skin parameters), in text order.
    /// </summary>
    IReadOnlyList<PlantUmlPendingReport> Reports { get; }

    /// <summary>
    ///     Lays the diagram out under the options of a call and describes it as a scene, whose view box is the natural
    ///     size.
    /// </summary>
    /// <param name="context">The context of the call: options, report sink, text layout and cancellation.</param>
    /// <returns>The scene.</returns>
    /// <exception cref="PlantUmlRenderException">Thrown, located, when a limit is exceeded or the layout fails.</exception>
    /// <exception cref="PlantUmlUnsupportedFeatureException">Thrown in strict mode at the first unsupported feature.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the call is canceled.</exception>
    Scene Layout(DiagramRenderContext context);
}

/// <summary>
///     The context of one measure or render call, handed to <see cref="IPlantUmlDiagram.Layout"/>.
/// </summary>
/// <remarks>
///     The context belongs to one call on one thread: the sink buffers the reports of the call and the text layout is
///     shared by the measurement and the painter, so both see the same faces.
/// </remarks>
/// <param name="Options">The effective options of the call.</param>
/// <param name="Sink">The report sink of the call.</param>
/// <param name="Text">The text layout of the call.</param>
/// <param name="CancellationToken">The token that stops the work.</param>
internal sealed record DiagramRenderContext(
    PlantUmlRenderOptions Options,
    PlantUmlReportSink Sink,
    TextLayout Text,
    CancellationToken CancellationToken);

/// <summary>
///     The registry of the diagram parsers: which subsystem parses which supported diagram type.
/// </summary>
/// <remarks>
///     <para>
///         The registry is the one place a new diagram type is wired in. The class is stateless and safe for concurrent
///         use.
///     </para>
/// </remarks>
internal static class DiagramRegistry
{
    /// <summary>
    ///     Parses the diagram of a supported type.
    /// </summary>
    /// <param name="detected">The detected type; it must be supported.</param>
    /// <param name="source">The preprocessed text.</param>
    /// <param name="limits">The limits of the open call.</param>
    /// <returns>The parsed diagram.</returns>
    /// <exception cref="ArgumentException">Thrown when the type is not supported.</exception>
    /// <exception cref="PlantUmlSyntaxException">Thrown, located, when the diagram does not follow its syntax.</exception>
    public static IPlantUmlDiagram Parse(DetectedDiagram detected, PreprocessedSource source, PlantUmlLimits limits)
    {
        ArgumentNullException.ThrowIfNull(detected);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(limits);
        return detected.DiagramType switch
        {
            "sequence" => SequenceParser.Parse(source, detected, limits),
            "activity" => ActivityParser.Parse(source, detected, limits),
            "class" or "object" => ClassParser.Parse(source, detected, limits),
            "state" => StateParser.Parse(source, detected, limits),
            "usecase" or "component" or "deployment" => DescriptionParser.Parse(source, detected, limits),
            _ => throw new ArgumentException($"The diagram type '{detected.DiagramType}' is not supported.", nameof(detected)),
        };
    }
}
