// Portions derived from mermaid (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Sequence;

/// <content>
///     The diagram of the document: the metadata of <see cref="IMermaidDiagram"/> and the layout of a render, which
///     reports what is not drawn and has <see cref="SequenceLayout"/> draw the scene.
/// </content>
internal sealed partial class SequenceModel : IMermaidDiagram
{
    /// <summary>Gets the direction: a sequence diagram has none.</summary>
    public string? Direction => null;

    /// <inheritdoc />
    public Scene Layout(DiagramRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ReportUnsupported(context.Config, context.Sink);
        return new SequenceLayout(this, context).Draw();
    }

    /// <summary>
    ///     Reports what the diagram uses that this version does not draw: a look other than <c>classic</c>, which is
    ///     drawn classic.
    /// </summary>
    /// <param name="config">The effective configuration of the render.</param>
    /// <param name="sink">The report sink of the render.</param>
    /// <exception cref="MermaidUnsupportedFeatureException">Thrown by the sink in strict mode at the first report.</exception>
    private static void ReportUnsupported(MermaidConfig config, MermaidReportSink sink)
    {
        if (config.Look != "classic")
        {
            sink.Unsupported(
                MermaidDiagnosticCodes.LookUnsupported,
                $"the '{config.Look}' look is not supported; the diagram is drawn in the classic look",
                config.LocationOf("look"),
                "remove the look setting, or set look: classic");
        }
    }
}
