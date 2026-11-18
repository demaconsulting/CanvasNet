using DemaConsulting.CanvasNet.PlantUml.Syntax;

namespace DemaConsulting.CanvasNet.PlantUml;

/// <summary>
///     A parsed diagram of a supported type: its metadata, the reports its parser found, and the layout that turns it
///     into a scene.
/// </summary>
/// <remarks>
///     <para>
///         This is the seam between <see cref="PlantUmlDocument"/> and the diagram subsystems: a diagram parser
///         (registered in <see cref="DiagramRegistry"/>) produces it once when the document is opened.
///     </para>
///     <para>
///         Implementations are immutable and safe for concurrent use.
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
}

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
        throw new ArgumentException($"The diagram type '{detected.DiagramType}' is not supported.", nameof(detected));
    }
}
