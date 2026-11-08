using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Flowchart;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid;

/// <summary>
///     A parsed diagram of a supported type: its metadata and the layout that turns it into a scene.
/// </summary>
/// <remarks>
///     <para>
///         This is the seam between <see cref="MermaidDocument"/> and the diagram subsystems: a diagram parser (registered in
///         <see cref="DiagramRegistry"/>) produces it once when the document is opened, and every measure and render asks it
///         for a scene under the options of the call, because the configuration and the theme of a call can change sizes.
///     </para>
///     <para>
///         Implementations are immutable and safe for concurrent use; <see cref="Layout"/> keeps no state between calls.
///     </para>
/// </remarks>
internal interface IMermaidDiagram
{
    /// <summary>
    ///     Gets the title the diagram itself declares (a title statement), or null; the frontmatter title is the document's
    ///     fallback.
    /// </summary>
    string? Title { get; }

    /// <summary>
    ///     Gets the accessible title (<c>accTitle</c>), or null.
    /// </summary>
    string? AccTitle { get; }

    /// <summary>
    ///     Gets the direction (<c>TB</c>, <c>TD</c>, <c>BT</c>, <c>LR</c>, <c>RL</c>), or null for a type without one.
    /// </summary>
    string? Direction { get; }

    /// <summary>
    ///     Gets the number of nodes, subgraphs not included.
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
    ///     Lays the diagram out under the options of a call and describes it as a scene, whose view box is the natural size.
    /// </summary>
    /// <param name="context">The context of the call: options, report sink, fonts and cancellation.</param>
    /// <returns>The scene.</returns>
    /// <exception cref="MermaidRenderException">Thrown, located, when a limit is exceeded or the layout fails.</exception>
    /// <exception cref="MermaidUnsupportedFeatureException">Thrown in strict mode at the first unsupported feature.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the call is canceled.</exception>
    Scene Layout(DiagramRenderContext context);
}

/// <summary>
///     The context of one measure or render call, handed to <see cref="IMermaidDiagram.Layout"/>.
/// </summary>
/// <remarks>
///     The context belongs to one call on one thread: the sink buffers the reports of the call and the font resolver is
///     shared by the label measurement and the painter, so both see the same faces.
/// </remarks>
/// <param name="Source">The preprocessed text (frontmatter, directives and code).</param>
/// <param name="Diagram">The detected type.</param>
/// <param name="Options">The effective options of the call.</param>
/// <param name="Config">
///     The effective configuration of the call (defaults, site configuration, frontmatter and directives) with its theme
///     variables; build the diagram's style sheet from it with <see cref="FlowchartStyles.Create"/>.
/// </param>
/// <param name="Sink">The report sink of the call.</param>
/// <param name="Fonts">The font resolver of the call.</param>
/// <param name="CancellationToken">The token that stops the work.</param>
internal sealed record DiagramRenderContext(
    PreprocessedSource Source,
    DetectedDiagram Diagram,
    MermaidRenderOptions Options,
    MermaidConfig Config,
    MermaidReportSink Sink,
    FontResolver Fonts,
    CancellationToken CancellationToken);

/// <summary>
///     The registry of the diagram parsers: which subsystem parses which supported diagram type.
/// </summary>
/// <remarks>
///     <para>
///         The registry is the one place a new diagram type is wired in: the flowchart (<c>flowchart</c> and
///         <c>graph</c>) is parsed by <see cref="FlowchartParser"/> into a <see cref="FlowchartModel"/>.
///     </para>
///     <para>
///         The class is stateless and safe for concurrent use.
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
    /// <exception cref="MermaidSyntaxException">Thrown, located, when the diagram does not follow its grammar.</exception>
    public static IMermaidDiagram Parse(DetectedDiagram detected, PreprocessedSource source, MermaidLimits limits)
    {
        ArgumentNullException.ThrowIfNull(detected);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(limits);
        return detected.IsSupported && detected.DiagramType == "flowchart"
            ? FlowchartParser.Parse(source, detected, limits)
            : throw new ArgumentException($"The diagram type '{detected.DiagramType}' is not supported.", nameof(detected));
    }
}
