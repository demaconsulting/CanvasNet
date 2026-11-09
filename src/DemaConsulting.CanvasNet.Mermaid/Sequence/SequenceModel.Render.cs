// Portions derived from mermaid (MIT); see third-party/README.md
using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

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
    ///     Reports what the diagram uses that this version does not draw as Mermaid does: a look other than
    ///     <c>classic</c> (drawn classic), the <c>fo</c> text placement (drawn as SVG text), participants of an unknown
    ///     type (not drawn, as in Mermaid), <c>properties</c> icons (not drawn), and <c>$$</c> math (drawn as text).
    /// </summary>
    /// <param name="config">The effective configuration of the render.</param>
    /// <param name="sink">The report sink of the render.</param>
    /// <exception cref="MermaidUnsupportedFeatureException">Thrown by the sink in strict mode at the first unsupported feature.</exception>
    private void ReportUnsupported(MermaidConfig config, MermaidReportSink sink)
    {
        if (config.Look != "classic")
        {
            sink.Unsupported(
                MermaidDiagnosticCodes.LookUnsupported,
                $"the '{config.Look}' look is not supported; the diagram is drawn in the classic look",
                config.LocationOf("look"),
                "remove the look setting, or set look: classic");
        }

        if (config.Sequence.TextPlacement == "fo")
        {
            sink.Warn(
                MermaidDiagnosticCodes.ConfigInvalid,
                "sequence.textPlacement 'fo' (HTML labels in foreignObject) is not supported; the participant labels are drawn as SVG text",
                config.LocationOf("sequence"),
                "remove sequence.textPlacement, or set it to 'old'");
        }

        // The participants: their types, icons and math
        foreach (var actor in _actors)
        {
            if (!SequenceParticipantTypes.IsKnown(actor.Type))
            {
                var nearest = SequenceParticipantTypes.All.MinBy(type => DiagramDetector.EditDistance(actor.Type.ToLowerInvariant(), type));
                sink.Warn(
                    MermaidDiagnosticCodes.ParticipantTypeUnknown,
                    $"participant '{actor.Name}' has the type '{actor.Type}', which Mermaid does not define; it is not drawn",
                    actor.Location,
                    $"did you mean '{nearest}'? The types are participant, actor, boundary, control, entity, database, collections and queue");
            }

            if (actor.Type is SequenceParticipantTypes.Participant or SequenceParticipantTypes.Collections or SequenceParticipantTypes.Queue &&
                actor.Properties.TryGetValue("icon", out var icon) && SequenceRenderer.IsTruthy(icon) && icon is JsonValue value &&
                value.TryGetValue<string>(out var source) && !source.Trim().StartsWith('@'))
            {
                sink.Unsupported(
                    MermaidDiagnosticCodes.IconUnsupported,
                    $"the icon of participant '{actor.Name}' (an image from '{source.Trim()}') is not drawn",
                    actor.Location,
                    "remove the icon from the participant's properties");
            }

            ReportMath(actor.Description, actor.Location, sink);
        }

        // The texts of messages, notes and block titles
        foreach (var message in _messages.Where(message => DrawsText(message.Type)))
        {
            ReportMath(message.Text, message.Location, sink);
        }
    }

    /// <summary>
    ///     Gets whether a message of a type is drawn with its text: an arrow, a note, or the start of a block or section
    ///     (not a <c>rect</c>, whose text is its color).
    /// </summary>
    /// <param name="type">The line type.</param>
    /// <returns>Whether it is.</returns>
    private static bool DrawsText(int type) => SequenceLineType.IsArrow(type) || type is SequenceLineType.Note
        or SequenceLineType.LoopStart or SequenceLineType.AltStart or SequenceLineType.AltElse or SequenceLineType.OptStart
        or SequenceLineType.ParStart or SequenceLineType.ParAnd or SequenceLineType.ParOverStart or SequenceLineType.CriticalStart
        or SequenceLineType.CriticalOption or SequenceLineType.BreakStart;

    /// <summary>
    ///     Reports a text with <c>$$...$$</c> math (Mermaid's <c>hasKatex</c>), which Mermaid draws with KaTeX and this
    ///     version draws as written.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="location">The location of the construct.</param>
    /// <param name="sink">The report sink.</param>
    /// <exception cref="MermaidUnsupportedFeatureException">Thrown by the sink in strict mode.</exception>
    private static void ReportMath(string text, MermaidLocation location, MermaidReportSink sink)
    {
        if (HasKatex(text))
        {
            sink.Unsupported(
                MermaidDiagnosticCodes.MathUnsupported,
                "the $$...$$ math (KaTeX) in the text is not supported; it is drawn as written",
                location,
                "remove the $$ delimiters, or write the formula as plain text");
        }
    }

    /// <summary>
    ///     Gets whether a text has math as Mermaid's <c>hasKatex</c> tests it (its <c>katexRegex</c>,
    ///     <c>/\$\$(.*?)\$\$/</c>): two dollar signs, then two more on the same line.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>Whether it has.</returns>
    internal static bool HasKatex(string text)
    {
        for (var start = text.IndexOf("$$", StringComparison.Ordinal); start >= 0; start = text.IndexOf("$$", start + 1, StringComparison.Ordinal))
        {
            // The closing pair, before the end of the line (JavaScript's dot does not match a line terminator)
            for (var i = start + 2; i + 1 < text.Length && text[i] is not ('\n' or '\r' or '\u2028' or '\u2029'); i++)
            {
                if (text[i] == '$' && text[i + 1] == '$')
                {
                    return true;
                }
            }
        }

        return false;
    }
}
