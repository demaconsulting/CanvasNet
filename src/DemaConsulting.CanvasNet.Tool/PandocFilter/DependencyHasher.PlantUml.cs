using System.Text.RegularExpressions;

namespace DemaConsulting.CanvasNet.Tool.PandocFilter;

/// <summary>
///     The PlantUML walk of <see cref="DependencyHasher"/>: a PlantUML diagram and the local files its <c>!include</c>
///     directives read.
/// </summary>
internal sealed partial class DependencyHasher
{
    /// <summary>
    ///     Adds a PlantUML diagram, and every local file it includes that exists, to the parts.
    /// </summary>
    /// <param name="path">The diagram, or a file it includes.</param>
    /// <param name="root">The folder labels are relative to.</param>
    /// <param name="depth">The include depth (and the nesting depth of a diagram drawn inside a drawing or diagram).</param>
    /// <remarks>
    ///     Every <c>!include</c>, <c>!include_once</c>, and <c>!include_many</c> line is followed, relative to the folder
    ///     of the file that names it, as the PlantUML library resolves it; a quoted path loses its quotes. An include the
    ///     library never follows is skipped: the standard library (<c>&lt;name&gt;</c>), a URL, a <c>[name]</c>, and a
    ///     part of a file (<c>file!part</c>). A path built from a variable is taken as written, so it names no file and adds
    ///     nothing. Text that only looks like a directive (in a block comment, say) can only add a file to the key, and an
    ///     unreadable file is skipped (the tool reports it).
    /// </remarks>
    private void AddPlantUml(string path, string root, int depth)
    {
        if (!_seen.TryAdd(path, Seen.Taken))
        {
            return;
        }

        if (depth > MaxDepth)
        {
            throw Fail($"'{path}' is included more than {MaxDepth} levels deep");
        }

        var content = ReadFile(path);
        if (content == null)
        {
            return;
        }

        if (_parts.Count >= MaxDependencies)
        {
            throw Fail($"the diagram and the files it includes are more than {MaxDependencies} files; the limit is {MaxDependencies}, counting the diagram (every file must be hashed so that a changed one is drawn again)");
        }

        AddPart(FilterPaths.Label(root, path), content);
        var folder = FilterPaths.Directory(path);
        foreach (var include in CollectPlantUmlIncludes(AsBytes(content)).Select(FromBytes))
        {
            var next = FilterPaths.Collapse(FilterPaths.Join([folder, include]));
            if (DependencyExists(next))
            {
                AddPlantUml(next, root, depth + 1);
            }
        }
    }

    /// <summary>
    ///     Collects the local files the include directives of a PlantUML text name, in the order they are written.
    /// </summary>
    /// <param name="text">The text as a text of bytes.</param>
    /// <returns>The paths as written (quotes removed), as texts of bytes, without the includes that are never followed.</returns>
    internal static List<string> CollectPlantUmlIncludes(string text)
    {
        var paths = new List<string>();
        foreach (Match match in PlantUmlIncludePattern().Matches(text))
        {
            var path = FilterPaths.TrimLuaSpaceEnd(match.Groups[1].Value);
            if (path.Length >= 2 && path[0] == '"' && path[^1] == '"')
            {
                path = path[1..^1].Trim();
            }

            // The library never follows the standard library, a URL, a named block, or a part of a file
            if (path.Length == 0
                || path.StartsWith('<')
                || path.StartsWith('[')
                || path.Contains("://", StringComparison.Ordinal)
                || path.LastIndexOf('!') > 0
                || PictureAttributes.HasUrlScheme(path))
            {
                continue;
            }

            paths.Add(path);
        }

        return paths;
    }

    /// <summary>Matches an include directive at the start of a line and the rest of the line.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex("(?m)^[ \t]*!include(?:_once|_many)?[ \t]+([^\r\n]+)", RegexOptions.CultureInvariant)]
    private static partial Regex PlantUmlIncludePattern();
}
