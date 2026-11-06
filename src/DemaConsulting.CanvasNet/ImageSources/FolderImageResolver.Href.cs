// cspell:ignore href hrefs
using System.Globalization;
using DemaConsulting.CanvasNet.Files;

namespace DemaConsulting.CanvasNet.ImageSources;

/// <summary>
///     The link parsing of <see cref="FolderImageResolver"/>: turning the text of a link into a path the sandbox accepts,
///     and describing a refusal of the sandbox in words.
/// </summary>
public sealed partial class FolderImageResolver
{
    /// <summary>
    ///     Turns the text of a link without a URI scheme (or with <c>file:</c>) into a path that the sandbox accepts, or
    ///     says why it cannot be one.
    /// </summary>
    /// <param name="href">The link as written.</param>
    /// <returns>
    ///     The relative path, whether it is relative to the file being rendered, and the reason when there is no path. A
    ///     <c>file:</c> URI inside the folder becomes a path relative to the folder; a <c>file:</c> URI outside the folder
    ///     has no path.
    /// </returns>
    /// <remarks>
    ///     A relative reference is parsed as RFC 3986 says: the query (from the first <c>?</c>) and the fragment (from the
    ///     first <c>#</c>) are not part of the path, so they are cut off <b>before</b> percent-decoding (see
    ///     <see cref="SplitHref(string)"/>). That order keeps
    ///     an encoded <c>%23</c> or <c>%3F</c> a literal character of the file name, and it means a decoded
    ///     <c>#</c> or <c>?</c> can never hide or reveal a path segment. What is left is decoded and goes to the
    ///     extension choice and the sandbox, which refuse everything that is not a plain relative path below the folder
    ///     exactly as before; the query that was cut off is never used, and the fragment is parsed separately.
    /// </remarks>
    private (string? Path, bool RelativeToCurrent, string? Reason) ToRelativePath(string href)
    {
        var reference = SplitHref(href).Reference;
        if (reference.Length == 0)
        {
            return (null, false, "the reference is empty (it has only a query or a fragment)");
        }

        if (reference.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            var (relative, reason) = FileUriToRelativePath(reference);
            return (relative, false, reason);
        }

        // An absolute path, a drive letter and a UNC path are left in the text on purpose: the sandbox refuses all of them
        var path = reference.Contains('%') ? Uri.UnescapeDataString(reference) : reference;
        return (path, true, null);
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
    ///     Converts a <c>file:</c> URI that lies inside the folder of the resolver to a relative path.
    /// </summary>
    /// <param name="href">The URI as written.</param>
    /// <returns>The path relative to the folder, or <see langword="null"/> and the reason.</returns>
    private (string? Path, string? Reason) FileUriToRelativePath(string href)
    {
        const string outside = "it is outside the folder (only relative paths below the folder are allowed)";

        // A URI with a host (a network share), a malformed URI, and any text that is not a plain local file are outside
        if (_rootDirectory is null
            || !Uri.TryCreate(href, UriKind.Absolute, out var uri)
            || !uri.IsFile
            || !string.IsNullOrEmpty(uri.Host))
        {
            return (null, outside);
        }

        string relative;
        try
        {
            relative = Path.GetRelativePath(_rootDirectory, uri.LocalPath);
        }
        catch (ArgumentException)
        {
            return (null, outside);
        }

        var leavesRoot = Path.IsPathRooted(relative)
                         || relative == ".."
                         || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                         || relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
        return leavesRoot ? (null, outside) : (relative, null);
    }

    /// <summary>
    ///     Describes a refusal of the sandbox in words for a note.
    /// </summary>
    /// <param name="ex">The refusal.</param>
    /// <returns>The reason.</returns>
    private string DescribeSandboxFailure(SandboxException ex)
    {
        return ex.Kind switch
        {
            SandboxFailureKind.EmptyPath => "the reference is empty",
            SandboxFailureKind.ControlCharacter => "the reference contains a control character",
            SandboxFailureKind.ColonInPath or SandboxFailureKind.AbsolutePath or SandboxFailureKind.OutsideRoot
                => "it is outside the folder (only relative paths below the folder are allowed)",
            SandboxFailureKind.ThroughLink or SandboxFailureKind.LinkCheckFailed
                => "it is, or is reached through, a symbolic link",
            SandboxFailureKind.TooLarge => "the file is larger than the maximum of "
                + (SandboxOptions.DefaultMaxFileBytes / (1024 * 1024)).ToString(CultureInfo.InvariantCulture) + " MiB",
            SandboxFailureKind.TooManyFiles => $"more than {_options.MaxImages} different files are embedded",
            _ => "the file cannot be read"
        };
    }
}
