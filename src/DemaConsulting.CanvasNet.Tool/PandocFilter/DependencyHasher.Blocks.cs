using System.Text;
using System.Text.Json;
using DemaConsulting.CanvasNet.Scad;
using DemaConsulting.CanvasNet.Viewer;

namespace DemaConsulting.CanvasNet.Tool.PandocFilter;

/// <summary>
///     A local file the text of a code block names, which the block cannot reach: the text is written to the cache folder,
///     so a relative name would be looked up there instead of beside the document.
/// </summary>
/// <param name="Action">What the block does with the file, as a message says it: <c>reads the data file</c>, <c>draws the picture</c>, <c>includes</c>, or <c>uses</c>.</param>
/// <param name="Path">The name of the file as written in the block.</param>
/// <param name="Line">The line of the block (1-based) the name is on.</param>
internal sealed record BlockReference(string Action, string Path, int Line);

/// <summary>
///     The block check of <see cref="DependencyHasher"/>: the first local file the text of a Vega-Lite or OpenSCAD code block
///     names.
/// </summary>
internal sealed partial class DependencyHasher
{
    /// <summary>
    ///     The folder the names of a block are resolved from to tell a local name from a remote one; any absolute folder
    ///     gives the same answer, as only whether a name is local matters.
    /// </summary>
    private const string AnyFolder = "/";

    /// <summary>
    ///     Finds the first local file the text of a code block names: a data file or a picture of a Vega-Lite chart, or a
    ///     file an OpenSCAD model includes or uses.
    /// </summary>
    /// <param name="kind">The kind the block is drawn as (one of the <see cref="PictureAttributes"/> kind constants).</param>
    /// <param name="text">The text of the block.</param>
    /// <returns>
    ///     The first such file in the order of the text, or <see langword="null"/> when the block names none, its kind
    ///     reads no other file (Mermaid, PlantUML, and MathML blocks keep their own behavior), or its text cannot be read
    ///     (the tool reports the error when it draws the block).
    /// </returns>
    /// <exception cref="ToolException">
    ///     Thrown with <see cref="ErrorCodes.PandocFilterError"/> when a chart is nested deeper than the walk follows, as the
    ///     walk of the picture would.
    /// </exception>
    /// <remarks>
    ///     A code block has no folder: its text is written to the cache folder and drawn from there, so a relative name
    ///     would be looked up in the cache folder. The engine therefore stops the build with a message that says to link
    ///     the chart or model as a picture. A chart is checked with the collectors of the chart walk (data URLs, the links
    ///     of image marks, and the links in the inline rows, as written and percent-decoded), so the check refuses exactly
    ///     what the walk would follow; a remote link is left to the tool, which never reads it. A model is parsed by the
    ///     OpenSCAD library, so a comment or a comparison such as <c>a &lt; b &amp;&amp; c &gt; d</c> is never taken for a
    ///     directive; <c>import()</c> and <c>surface()</c> read no file, so they are not checked. Stateless and thread-safe.
    /// </remarks>
    internal static BlockReference? FindBlockReference(string kind, string text)
    {
        return kind switch
        {
            PictureAttributes.VegaLite => FindChartReference(text),
            PictureAttributes.Scad => FindModelReference(text),
            _ => null
        };
    }

    /// <summary>
    ///     Finds the first local data file or picture the text of a Vega-Lite block names.
    /// </summary>
    /// <param name="text">The text of the block.</param>
    /// <returns>The file, or <see langword="null"/> when the chart names none or is not a JSON object or array.</returns>
    private static BlockReference? FindChartReference(string text)
    {
        // The tool ignores a leading byte order mark, as the walk of a chart file does
        var json = Encoding.UTF8.GetBytes(text).AsMemory();
        if (json.Span.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
        {
            json = json[3..];
        }

        // The names the walk would follow, kept only when they are local
        HashSet<string> data;
        HashSet<string> pictures;
        try
        {
            using var document = JsonDocument.Parse(json, ChartJsonOptions);
            var spec = document.RootElement;
            if (!IsTable(spec))
            {
                return null;
            }

            var urls = new HashSet<string>(StringComparer.Ordinal);
            CollectDataUrls(spec, urls, 0);
            data = [.. urls.Where(url => FilterPaths.LocalDataFile(AnyFolder, url) != null)];

            var links = new HashSet<string>(StringComparer.Ordinal);
            var fields = new HashSet<string>(StringComparer.Ordinal);
            CollectImageLinks(spec, links, fields, 0);
            if (fields.Count > 0)
            {
                CollectRowLinks(spec, links, fields, 0);
            }

            pictures = [.. links.Where(link => FilterPaths.LocalImageFile(AnyFolder, link) != null || FilterPaths.LocalImageFile(AnyFolder, FilterPaths.DecodePercent(link)) != null)];
        }
        catch (JsonException)
        {
            return null;
        }

        if (data.Count == 0 && pictures.Count == 0)
        {
            return null;
        }

        // The first string value in the text that is one of them, with its line
        return FirstStringValue(json.Span, data, pictures);
    }

    /// <summary>
    ///     Finds the first string value of a chart's text that is one of the local names, in the order of the text.
    /// </summary>
    /// <param name="json">The text of the chart, without a byte order mark; it is valid JSON.</param>
    /// <param name="data">The local data URLs.</param>
    /// <param name="pictures">The local picture links.</param>
    /// <returns>The first name, as a data file before a picture when a value is both; <see langword="null"/> when none is found.</returns>
    private static BlockReference? FirstStringValue(ReadOnlySpan<byte> json, HashSet<string> data, HashSet<string> pictures)
    {
        var reader = new Utf8JsonReader(json, new JsonReaderOptions { MaxDepth = ChartJsonOptions.MaxDepth });
        while (reader.Read())
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                continue;
            }

            var value = reader.GetString()!;
            string action;
            if (data.Contains(value))
            {
                action = "reads the data file";
            }
            else if (pictures.Contains(value))
            {
                action = "draws the picture";
            }
            else
            {
                continue;
            }

            var line = 1 + json[..(int)reader.TokenStartIndex].Count((byte)'\n');
            return new BlockReference(action, value, line);
        }

        return null;
    }

    /// <summary>
    ///     Finds the first file the text of an OpenSCAD block includes or uses.
    /// </summary>
    /// <param name="text">The text of the block.</param>
    /// <returns>The file, or <see langword="null"/> when the model names none or cannot be parsed (the tool reports the syntax error).</returns>
    private static BlockReference? FindModelReference(string text)
    {
        try
        {
            var directive = ScadDocument.Parse(text).Directives.FirstOrDefault();
            if (directive == null)
            {
                return null;
            }

            var action = directive.Kind == ScadDirectiveKind.Use ? "uses" : "includes";
            return new BlockReference(action, directive.Path, directive.Location.Line);
        }
        catch (Exception ex) when (ex is ScadSyntaxException or InvalidDataException)
        {
            return null;
        }
    }
}
