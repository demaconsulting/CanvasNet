// cspell:ignore href hrefs
using System.Globalization;
using DemaConsulting.CanvasNet.Files;

namespace DemaConsulting.CanvasNet.ImageSources;

/// <summary>
///     The link parsing of <see cref="FolderImageResolver"/>: turning the text of a link into a path for the file
///     engine, and describing a refusal of the engine in words.
/// </summary>
public sealed partial class FolderImageResolver
{
    /// <summary>
    ///     Turns the text of a link without a URI scheme (or with <c>file:</c>) into a path for the file engine, or says
    ///     why it cannot be one.
    /// </summary>
    /// <param name="href">The link as written.</param>
    /// <returns>
    ///     The path (relative to the file that contains the link, or absolute), or <see langword="null"/> and the reason.
    ///     A local <c>file:</c> URI (no host) becomes its absolute local path; a <c>file:</c> URI with a host (including
    ///     <c>localhost</c>) or a malformed one has no path, because a network location is never read.
    /// </returns>
    /// <remarks>
    ///     A relative reference is parsed as RFC 3986 says: the query (from the first <c>?</c>) and the fragment (from the
    ///     first <c>#</c>) are not part of the path, so they are cut off <b>before</b> percent-decoding (see
    ///     <see cref="SplitHref(string)"/>). That order keeps
    ///     an encoded <c>%23</c> or <c>%3F</c> a literal character of the file name, and it means a decoded
    ///     <c>#</c> or <c>?</c> can never hide or reveal a path segment. What is left is decoded and goes to the
    ///     extension choice and the file engine: <c>..</c>, absolute and drive paths are passed through as they are, and
    ///     the engine and the file-access policy decide; the query that was cut off is never used, and the fragment is
    ///     parsed separately.
    /// </remarks>
    private static (string? Path, string? Reason) ToReferencePath(string href)
    {
        var reference = SplitHref(href).Reference;
        if (reference.Length == 0)
        {
            return (null, "the reference is empty (it has only a query or a fragment)");
        }

        if (reference.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            return FileUriToLocalPath(reference);
        }

        // An absolute path, a drive letter and a UNC path are left in the text on purpose: the engine refuses what is
        // never allowed, and the policy judges the rest
        return (Unescape(reference), null);
    }

    /// <summary>
    ///     Gets the path a link names as the resolver reads it to choose a source by its extension: the reference (the
    ///     query and the fragment cut off, <see cref="SplitHref(string)"/>) percent-decoded.
    /// </summary>
    /// <param name="href">The link as written.</param>
    /// <returns>The decoded path; for a <c>file:</c> URI the decoded URI, whose extension is that of its local path.</returns>
    /// <remarks>
    ///     A host that must tell links apart by kind exactly as the resolver does (the SVG flattener of the viewer) uses
    ///     this, so <c>flow%2Emmd</c> is a Mermaid diagram for both and <c>a%23b.mmd</c> is the file <c>a#b.mmd</c>.
    /// </remarks>
    internal static string DecodeReference(string href)
    {
        return Unescape(SplitHref(href).Reference);
    }

    /// <summary>
    ///     Percent-decodes a reference whose query and fragment are already cut off.
    /// </summary>
    /// <param name="reference">The reference.</param>
    /// <returns>The decoded text; an invalid escape is kept as written.</returns>
    private static string Unescape(string reference)
    {
        return reference.Contains('%') ? Uri.UnescapeDataString(reference) : reference;
    }

    /// <summary>
    ///     Splits a link into the reference (its path, still percent-encoded) and the raw fragment.
    /// </summary>
    /// <param name="href">The link as written.</param>
    /// <returns>
    ///     The reference: the text before the first <c>#</c> with its query (from the first <c>?</c>) cut off; and the
    ///     fragment: everything after the first <c>#</c>, still percent-encoded, with later <c>#</c> and <c>?</c>
    ///     characters belonging to it (empty when there is no <c>#</c>).
    /// </returns>
    /// <remarks>
    ///     The order is RFC 3986's: the fragment starts at the first <c>#</c>, and the query is only the part before it,
    ///     so <c>a.pdf#page=2?x</c> has no query and <c>a.pdf?v=2#page=2</c> has the ignored query <c>v=2</c>.
    /// </remarks>
    internal static (string Reference, string Fragment) SplitHref(string href)
    {
        var hash = href.IndexOf('#');
        var beforeFragment = hash < 0 ? href : href[..hash];
        var fragment = hash < 0 ? string.Empty : href[(hash + 1)..];
        var query = beforeFragment.IndexOf('?');
        return (query < 0 ? beforeFragment : beforeFragment[..query], fragment);
    }

    /// <summary>
    ///     Converts a local <c>file:</c> URI (one without a host) to its absolute local path.
    /// </summary>
    /// <param name="href">The URI as written.</param>
    /// <returns>The local path, or <see langword="null"/> and the reason.</returns>
    private static (string? Path, string? Reason) FileUriToLocalPath(string href)
    {
        // A URI with a host (a network share, localhost included) and a malformed URI are never read
        return Uri.TryCreate(href, UriKind.Absolute, out var uri) && uri.IsFile && string.IsNullOrEmpty(uri.Host)
            ? (uri.LocalPath, null)
            : (null, "it names a network location (file URIs with a host are never read)");
    }

    /// <summary>
    ///     Describes a refusal of the file engine (other than a denial of the policy) in words for a note.
    /// </summary>
    /// <param name="ex">The refusal.</param>
    /// <returns>The reason.</returns>
    private string DescribeFailure(FileReferenceException ex)
    {
        return ex.Kind switch
        {
            FileReferenceFailureKind.EmptyPath => "the reference is empty",
            FileReferenceFailureKind.ControlCharacter => "the reference contains a control character",
            FileReferenceFailureKind.NetworkPath => "it names a network location",
            FileReferenceFailureKind.InvalidPath => $"it is not a valid local path ({ex.Reason})",
            FileReferenceFailureKind.NotARegularFile => "it is not a regular file",
            FileReferenceFailureKind.TooLarge => "the file is larger than the maximum of "
                + (FileReferenceOptions.DefaultMaxFileBytes / (1024 * 1024)).ToString(CultureInfo.InvariantCulture) + " MiB",
            FileReferenceFailureKind.TooManyFiles => $"more than {_options.MaxImages} different files are embedded",
            _ => "the file cannot be read"
        };
    }
}
