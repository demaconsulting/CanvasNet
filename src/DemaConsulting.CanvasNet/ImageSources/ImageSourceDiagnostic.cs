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
///         are embedded, and lists the chain of embedding files in <see cref="Origin"/>; <see cref="Source"/>, <see cref="Severity"/> and <see cref="Code"/> are kept as reported.
///     </para>
///     <para>
///         A source that must stop rather than draw without a feature honors
///         <see cref="ImageSourceRequest.ThrowOnUnsupportedFeature"/> by throwing instead of reporting.
///     </para>
/// </remarks>
public sealed record ImageSourceDiagnostic(string Source, ImageDiagnosticSeverity Severity, string Code, string Message)
{
    /// <summary>
    ///     Gets the embedded files, outermost first, that the note is about: for a note a source reported, the file that
    ///     reported it preceded by the files that embedded it; for a link that was not drawn, the file that contains the
    ///     link. Empty for a note about the top-level document itself.
    /// </summary>
    /// <remarks>
    ///     Each entry is the folder-relative file name, with forward slashes, for a file link (the
    ///     <see cref="ImageSourceRequest.FileName"/> its source received) and the link as written for a scheme link.
    ///     For example, a top-level document that embeds <c>a.svg</c>, which embeds <c>sub/b.scad</c>: a note that
    ///     <c>b.scad</c>'s source reported has the origin <c>["a.svg", "sub/b.scad"]</c>, and a note that a picture
    ///     linked by <c>a.svg</c> is too large has <c>["a.svg"]</c>. <see cref="Message"/> keeps its
    ///     <c>"{href}: "</c> prefix unchanged. The list is compared by its entries, so two notes with equal origins are
    ///     equal.
    /// </remarks>
    public IReadOnlyList<string> Origin { get; init; } = [];

    /// <summary>
    ///     Determines whether another note has the same values, comparing <see cref="Origin"/> entry by entry.
    /// </summary>
    /// <param name="other">The note to compare with.</param>
    /// <returns><see langword="true"/> when every member, including each origin entry in order, is equal.</returns>
    public bool Equals(ImageSourceDiagnostic? other) =>
        other is not null
        && Source == other.Source
        && Severity == other.Severity
        && Code == other.Code
        && Message == other.Message
        && Origin.SequenceEqual(other.Origin);

    /// <summary>
    ///     Gets a hash code that is consistent with <see cref="Equals(ImageSourceDiagnostic?)"/>.
    /// </summary>
    /// <returns>The hash code, combining the members and every origin entry.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Source);
        hash.Add(Severity);
        hash.Add(Code);
        hash.Add(Message);
        foreach (var entry in Origin)
        {
            hash.Add(entry);
        }

        return hash.ToHashCode();
    }
}
