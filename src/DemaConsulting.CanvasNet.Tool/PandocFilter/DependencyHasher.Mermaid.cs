using System.Text.RegularExpressions;

namespace DemaConsulting.CanvasNet.Tool.PandocFilter;

/// <summary>
///     The diagram walk of <see cref="DependencyHasher"/>: a Mermaid diagram and the local pictures its nodes name.
/// </summary>
internal sealed partial class DependencyHasher
{
    /// <summary>
    ///     Adds a diagram and the local pictures its nodes name to the parts (<c>add_mermaid_sources</c>).
    /// </summary>
    /// <param name="path">The diagram.</param>
    /// <param name="root">The folder labels are relative to.</param>
    /// <param name="depth">The nesting depth of charts, diagrams, and drawings.</param>
    /// <remarks>
    ///     A link is taken from the diagram's folder, as written and percent-decoded, without its fragment or query; a
    ///     remote link is skipped; a linked model, chart, or diagram adds its own files. Text that only looks like a link
    ///     (in a comment, say) can only add a file to the key.
    /// </remarks>
    private void AddMermaid(string path, string root, int depth)
    {
        _seen[path] = Seen.Taken;
        var content = FilterPaths.ReadFile(path);
        if (content == null)
        {
            return;
        }

        if (_parts.Count >= MaxDependencies)
        {
            throw Fail($"the diagram and the files it draws are more than {MaxDependencies} files; the limit is {MaxDependencies} (every file must be hashed so that a changed one is drawn again)");
        }

        AddPart(FilterPaths.Label(root, path), content);
        var folder = FilterPaths.Directory(path);
        var pictures = new List<string>();
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (var link in CollectMermaidLinks(AsBytes(content)).Select(FromBytes))
        {
            foreach (var name in new[] { link, FilterPaths.DecodePercent(link) })
            {
                if (FilterPaths.LocalImageFile(folder, name) is { } resolved && !unique.Contains(resolved) && DependencyExists(resolved))
                {
                    unique.Add(resolved);
                    pictures.Add(resolved);
                }
            }
        }

        // Sorted, so the order the links are met in cannot change the picture's name
        FilterPaths.SortBytes(pictures);
        if (pictures.Count > MaxDependencies)
        {
            throw Fail($"'{FilterPaths.FileName(path)}' names {pictures.Count} pictures; the limit is {MaxDependencies} (every picture must be hashed so that a changed one is drawn again)");
        }

        AddImageFiles(pictures, root, depth);
    }

    /// <summary>
    ///     Collects the links of the pictures a diagram draws in its nodes: the <c>img</c> of a shape's data, quoted or
    ///     not, and the <c>src</c> of an <c>&lt;img&gt;</c> tag in a label.
    /// </summary>
    /// <param name="text">The diagram as a text of bytes.</param>
    /// <returns>The links, as texts of bytes.</returns>
    private static HashSet<string> CollectMermaidLinks(string text)
    {
        var links = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in ImgDoubleQuotedPattern().Matches(text).Concat(ImgSingleQuotedPattern().Matches(text)))
        {
            links.Add(match.Groups[1].Value);
        }

        foreach (Match match in ImgUnquotedPattern().Matches(text))
        {
            links.Add(FilterPaths.TrimLuaSpaceEnd(match.Groups[1].Value));
        }

        foreach (var tag in ImgTagPattern().Matches(text).Select(m => m.Value))
        {
            foreach (Match match in SrcDoubleQuotedPattern().Matches(tag).Concat(SrcSingleQuotedPattern().Matches(tag)).Concat(SrcUnquotedPattern().Matches(tag)))
            {
                links.Add(match.Groups[1].Value);
            }
        }

        return links;
    }

    /// <summary>Matches <c>img: "link"</c>.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex("img[ \t\n\v\f\r]*:[ \t\n\v\f\r]*\"([^\"\r\n]*)\"", RegexOptions.CultureInvariant)]
    private static partial Regex ImgDoubleQuotedPattern();

    /// <summary>Matches <c>img: 'link'</c>.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex("img[ \t\n\v\f\r]*:[ \t\n\v\f\r]*'([^'\r\n]*)'", RegexOptions.CultureInvariant)]
    private static partial Regex ImgSingleQuotedPattern();

    /// <summary>Matches <c>img: link</c> up to a comma, a brace, or the end of the line.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex("img[ \t\n\v\f\r]*:[ \t\n\v\f\r]*([^ \t\n\v\f\r\"',}][^,}\r\n]*)", RegexOptions.CultureInvariant)]
    private static partial Regex ImgUnquotedPattern();

    /// <summary>Matches an <c>&lt;img ...&gt;</c> tag in any letter case.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex("<[iI][mM][gG][^>]*>", RegexOptions.CultureInvariant)]
    private static partial Regex ImgTagPattern();

    /// <summary>Matches <c>src="link"</c> in any letter case.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex("[sS][rR][cC][ \t\n\v\f\r]*=[ \t\n\v\f\r]*\"([^\"]*)\"", RegexOptions.CultureInvariant)]
    private static partial Regex SrcDoubleQuotedPattern();

    /// <summary>Matches <c>src='link'</c> in any letter case.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex("[sS][rR][cC][ \t\n\v\f\r]*=[ \t\n\v\f\r]*'([^']*)'", RegexOptions.CultureInvariant)]
    private static partial Regex SrcSingleQuotedPattern();

    /// <summary>Matches an unquoted <c>src=link</c> in any letter case.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex("[sS][rR][cC][ \t\n\v\f\r]*=[ \t\n\v\f\r]*([^ \t\n\v\f\r\"'>][^ \t\n\v\f\r>]*)", RegexOptions.CultureInvariant)]
    private static partial Regex SrcUnquotedPattern();
}
