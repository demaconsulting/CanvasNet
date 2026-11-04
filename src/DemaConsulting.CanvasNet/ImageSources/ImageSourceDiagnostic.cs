// cspell:ignore href scad
namespace DemaConsulting.CanvasNet.ImageSources;

/// <summary>
///     A note about a picture: something a source ignored, did not support, or a link that was not drawn.
/// </summary>
/// <param name="Source">
///     The name of the part that reported the note: the <see cref="IImageSource.Name"/> of a source (<c>scad</c>,
///     <c>vega-lite</c>, ...), or <c>svg</c> for a link that <see cref="FolderImageResolver"/> did not draw.
/// </param>
/// <param name="Severity">How serious the note is.</param>
/// <param name="Code">
///     A short, stable, machine-readable code, such as <c>scad-warning</c> or
///     <see cref="FolderImageResolver.UnresolvedCode"/>. A source may use the name of an unsupported feature as the code
///     of an <see cref="ImageDiagnosticSeverity.Unsupported"/> note.
/// </param>
/// <param name="Message">A single-line, human-readable description.</param>
/// <remarks>
///     <para>
///         <b>How a source reports.</b> A source returns its notes in <see cref="ImageSourceResult.Diagnostics"/>; it
///         never prints or throws for a note. <see cref="FolderImageResolver"/> collects them in
///         <see cref="FolderImageResolver.Diagnostics"/>, in render order, and puts the link that embedded the file
///         in front of the message (<c>"{href}: {message}"</c>) so the origin of every note is clear when many files
///         are embedded; <see cref="Source"/>, <see cref="Severity"/> and <see cref="Code"/> are kept as reported.
///     </para>
///     <para>
///         A source that must stop rather than draw without a feature honors
///         <see cref="ImageSourceRequest.ThrowOnUnsupportedFeature"/> by throwing instead of reporting.
///     </para>
/// </remarks>
public sealed record ImageSourceDiagnostic(string Source, ImageDiagnosticSeverity Severity, string Code, string Message);
