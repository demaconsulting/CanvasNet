// cspell:ignore href
namespace DemaConsulting.CanvasNet.ImageSources;

/// <summary>
///     One link a document asked a <see cref="FolderImageResolver"/> for, at any level of a render, whether or not it was
///     drawn.
/// </summary>
/// <remarks>
///     A caller that knows which links a file contains can compare them with these entries to find the links the file
///     never showed (hidden, an unused definition, outside the canvas): those are never asked for.
/// </remarks>
/// <param name="Href">The link as written in the document.</param>
/// <param name="Origin">
///     The files that contain the link, outermost first, as in <see cref="ImageSourceDiagnostic.Origin"/>: empty for a
///     link of the top-level document, <c>["a.svg"]</c> for a link of <c>a.svg</c> that the top-level document embeds.
///     The list is compared by its entries.
/// </param>
public sealed record RequestedImage(string Href, IReadOnlyList<string> Origin)
{
    /// <summary>
    ///     Gets the name of the picture when it was drawn: the folder-relative file name (forward slashes) for a file link
    ///     or the link as written for a scheme link, the entry the picture adds to the origin of its own links and notes;
    ///     <see langword="null"/> when the link was not drawn.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    ///     Determines whether another entry has the same values, comparing <see cref="Origin"/> entry by entry.
    /// </summary>
    /// <param name="other">The entry to compare with.</param>
    /// <returns><see langword="true"/> when every member, including each origin entry in order, is equal.</returns>
    public bool Equals(RequestedImage? other) =>
        other is not null
        && Href == other.Href
        && Name == other.Name
        && Origin.SequenceEqual(other.Origin);

    /// <summary>
    ///     Gets a hash code that is consistent with <see cref="Equals(RequestedImage?)"/>.
    /// </summary>
    /// <returns>The hash code, combining the members and every origin entry.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Href);
        hash.Add(Name);
        foreach (var entry in Origin)
        {
            hash.Add(entry);
        }

        return hash.ToHashCode();
    }
}
