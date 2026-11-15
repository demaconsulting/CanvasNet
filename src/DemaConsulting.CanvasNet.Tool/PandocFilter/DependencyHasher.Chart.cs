using System.Text.Json;

namespace DemaConsulting.CanvasNet.Tool.PandocFilter;

/// <summary>
///     The chart walk of <see cref="DependencyHasher"/>: a Vega-Lite specification, its local data files, and the
///     pictures its image marks name.
/// </summary>
internal sealed partial class DependencyHasher
{
    /// <summary>
    ///     The JSON options of a specification: a reader depth far above <see cref="MaxDepth"/>, so the walk stops a
    ///     deep specification with the filter's own message, while a hostile one deeper than the reader allows is
    ///     treated like any specification that cannot be read (the tool reports it) instead of exhausting memory.
    /// </summary>
    private static readonly JsonDocumentOptions ChartJsonOptions = new() { MaxDepth = 4096 };

    /// <summary>
    ///     Adds a chart, its local data files, and the pictures of its image marks to the parts (<c>add_chart_sources</c>).
    /// </summary>
    /// <param name="path">The specification.</param>
    /// <param name="root">The folder of the specification: its data files and pictures are found and labeled from there.</param>
    /// <param name="depth">The nesting depth of charts, diagrams, and drawings.</param>
    /// <remarks>
    ///     The specification is taken before its links are followed, so a picture that links back to it does not read it
    ///     again. Its part is labeled with its file name. A specification that is not JSON adds only itself (the tool
    ///     reports the error).
    /// </remarks>
    private void AddChart(string path, string root, int depth)
    {
        _seen[path] = Seen.Taken;
        var content = ReadFile(path);
        if (content == null)
        {
            return;
        }

        AddPart(FilterPaths.FileName(path), content);

        // The tool ignores a leading UTF-8 byte order mark, so the data URLs of such a file are hashed too
        var json = content.AsMemory();
        if (json.Span.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
        {
            json = json[3..];
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, ChartJsonOptions);
        }
        catch (JsonException)
        {
            return;
        }

        using (document)
        {
            var spec = document.RootElement;
            if (!IsTable(spec))
            {
                return;
            }

            AddChartData(path, root, spec);
            AddChartPictures(path, root, spec, depth);
        }
    }

    /// <summary>
    ///     Adds the local data files every <c>data</c> object of a specification names.
    /// </summary>
    /// <param name="path">The specification, for messages.</param>
    /// <param name="root">The folder of the specification.</param>
    /// <param name="spec">The decoded specification.</param>
    private void AddChartData(string path, string root, JsonElement spec)
    {
        var urls = new HashSet<string>(StringComparer.Ordinal);
        CollectDataUrls(spec, urls, 0);
        var files = urls.Select(url => FilterPaths.LocalDataFile(root, url))
            .OfType<string>()
            .Where(DependencyExists)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Sorted, so the order a JSON object is walked in cannot change the picture's name
        FilterPaths.SortBytes(files);
        if (files.Count > MaxDependencies)
        {
            throw Fail($"'{FilterPaths.FileName(path)}' names {files.Count} data files; the limit is {MaxDependencies} (every data file must be hashed so that a changed one is drawn again)");
        }

        foreach (var file in files)
        {
            if (ReadFile(file) is { } data)
            {
                AddPart(FilterPaths.Label(root, file), data);
            }
        }
    }

    /// <summary>
    ///     Adds the pictures of the image marks of a specification: the literal links and the links of the inline rows.
    /// </summary>
    /// <param name="path">The specification, for messages.</param>
    /// <param name="root">The folder of the specification.</param>
    /// <param name="spec">The decoded specification.</param>
    /// <param name="depth">The nesting depth of charts, diagrams, and drawings.</param>
    private void AddChartPictures(string path, string root, JsonElement spec, int depth)
    {
        var links = new HashSet<string>(StringComparer.Ordinal);
        var fields = new HashSet<string>(StringComparer.Ordinal);
        CollectImageLinks(spec, links, fields, 0);
        if (fields.Count > 0)
        {
            CollectRowLinks(spec, links, fields, 0);
        }

        // Every link that names an existing file, sorted (two links may name one file: it is walked once)
        var pictures = links.Select(link => FilterPaths.LocalImageFile(root, link))
            .OfType<string>()
            .Where(DependencyExists)
            .ToList();
        FilterPaths.SortBytes(pictures);
        if (pictures.Count > MaxDependencies)
        {
            throw Fail($"'{FilterPaths.FileName(path)}' names {pictures.Count} pictures; the limit is {MaxDependencies} (every picture must be hashed so that a changed one is drawn again)");
        }

        AddImageFiles(pictures, root, depth);
    }

    /// <summary>
    ///     Collects the string <c>url</c> of every <c>data</c> object anywhere in a specification.
    /// </summary>
    /// <param name="node">An object or array of the specification.</param>
    /// <param name="urls">The URLs found.</param>
    /// <param name="depth">The depth of <paramref name="node"/>.</param>
    private static void CollectDataUrls(JsonElement node, HashSet<string> urls, int depth)
    {
        if (depth > MaxDepth)
        {
            throw Fail($"the chart specification is nested more than {MaxDepth} levels deep; every data url must be hashed so that a changed data file is drawn again");
        }

        foreach (var (key, value) in Members(node).Where(m => IsTable(m.Value)))
        {
            if (key == "data" && StringMember(value, "url") is { } url)
            {
                urls.Add(url);
            }

            CollectDataUrls(value, urls, depth + 1);
        }
    }

    /// <summary>
    ///     Collects the links of the pictures a chart draws wherever they are written: the <c>url</c> of a <c>mark</c>
    ///     object, and the literal links and the field names of a <c>url</c> channel.
    /// </summary>
    /// <param name="node">An object or array of the specification.</param>
    /// <param name="links">The links found.</param>
    /// <param name="fields">The names of the fields whose values in the rows are links.</param>
    /// <param name="depth">The depth of <paramref name="node"/>.</param>
    private static void CollectImageLinks(JsonElement node, HashSet<string> links, HashSet<string> fields, int depth)
    {
        if (depth > MaxDepth)
        {
            throw ImageDepthError();
        }

        foreach (var (key, value) in Members(node).Where(m => IsTable(m.Value)))
        {
            if (key == "mark" && StringMember(value, "url") is { } url)
            {
                links.Add(url);
            }
            else if (key == "url")
            {
                CollectUrlChannel(value, links, fields, 0);
            }

            CollectImageLinks(value, links, fields, depth + 1);
        }
    }

    /// <summary>
    ///     Collects what a <c>url</c> channel names in its subtree: the literal links of a <c>value</c> or a <c>datum</c>,
    ///     and the <c>field</c> names whose values in the rows are links.
    /// </summary>
    /// <param name="node">The channel, or an object or array below it.</param>
    /// <param name="links">The links found.</param>
    /// <param name="fields">The field names found.</param>
    /// <param name="depth">The depth below the channel.</param>
    private static void CollectUrlChannel(JsonElement node, HashSet<string> links, HashSet<string> fields, int depth)
    {
        if (depth > MaxDepth)
        {
            throw ImageDepthError();
        }

        foreach (var (key, value) in Members(node))
        {
            if (value.ValueKind == JsonValueKind.String)
            {
                if (key is "value" or "datum")
                {
                    links.Add(value.GetString()!);
                }
                else if (key == "field")
                {
                    fields.Add(value.GetString()!);
                }
            }
            else if (IsTable(value))
            {
                CollectUrlChannel(value, links, fields, depth + 1);
            }
        }
    }

    /// <summary>
    ///     Collects the links the named fields hold in the inline rows: the <c>values</c> of a data object and the arrays
    ///     of <c>datasets</c>.
    /// </summary>
    /// <param name="node">An object or array of the specification.</param>
    /// <param name="links">The links found.</param>
    /// <param name="fields">The field names whose values are links.</param>
    /// <param name="depth">The depth of <paramref name="node"/>.</param>
    private static void CollectRowLinks(JsonElement node, HashSet<string> links, HashSet<string> fields, int depth)
    {
        if (depth > MaxDepth)
        {
            throw ImageDepthError();
        }

        foreach (var (key, value) in Members(node).Where(m => IsTable(m.Value)))
        {
            if (key == "values")
            {
                AddRowLinks(value, links, fields);
            }
            else if (key == "datasets")
            {
                foreach (var (_, rows) in Members(value))
                {
                    AddRowLinks(rows, links, fields);
                }
            }

            CollectRowLinks(value, links, fields, depth + 1);
        }
    }

    /// <summary>
    ///     Adds the links the named fields hold in the rows of a table.
    /// </summary>
    /// <param name="rows">The rows: an array or object of objects; anything else holds none.</param>
    /// <param name="links">The links found.</param>
    /// <param name="fields">The field names whose values are links.</param>
    private static void AddRowLinks(JsonElement rows, HashSet<string> links, HashSet<string> fields)
    {
        if (!IsTable(rows))
        {
            return;
        }

        foreach (var (_, row) in Members(rows))
        {
            foreach (var field in fields)
            {
                if (StringMember(row, field) is { } link)
                {
                    links.Add(link);
                }
            }
        }
    }

    /// <summary>
    ///     Creates the error of a specification nested too deep for its image links.
    /// </summary>
    /// <returns>The exception to throw.</returns>
    private static ToolException ImageDepthError()
    {
        return Fail($"the chart specification is nested more than {MaxDepth} levels deep; every image url must be hashed so that a changed picture is drawn again");
    }

    /// <summary>
    ///     Determines whether a JSON value is an object or an array (a Lua table).
    /// </summary>
    /// <param name="element">The value.</param>
    /// <returns><see langword="true"/> for an object or an array.</returns>
    private static bool IsTable(JsonElement element)
    {
        return element.ValueKind is JsonValueKind.Object or JsonValueKind.Array;
    }

    /// <summary>
    ///     Gets the members of an object or array as the Lua table the JSON decoder made: the properties of an object (a
    ///     repeated name once, with its last value) by name, and the items of an array without a name.
    /// </summary>
    /// <param name="node">The object or array.</param>
    /// <returns>The members; empty for any other value.</returns>
    private static IEnumerable<(string? Key, JsonElement Value)> Members(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Array)
        {
            return node.EnumerateArray().Select(item => ((string?)null, item));
        }

        if (node.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var last = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in node.EnumerateObject())
        {
            last[property.Name] = property.Value;
        }

        return last.Select(p => ((string?)p.Key, p.Value));
    }

    /// <summary>
    ///     Gets a string member of an object (Lua <c>type(value.name) == "string"</c>).
    /// </summary>
    /// <param name="node">The value.</param>
    /// <param name="name">The member's name.</param>
    /// <returns>The string, or <see langword="null"/> when the value is not an object or its last such member is not a string.</returns>
    private static string? StringMember(JsonElement node, string name)
    {
        if (node.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        JsonElement? found = null;
        foreach (var property in node.EnumerateObject().Where(p => p.NameEquals(name)))
        {
            found = property.Value;
        }

        return found is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;
    }
}
