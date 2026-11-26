using System.Runtime.InteropServices;
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
/// <param name="Line">The line of the block (1-based) of the JSON value or the directive that names the file.</param>
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
    ///     what the walk would follow; a remote link is left to the tool, which never reads it. The action and line are
    ///     those of the JSON value the walk took the name from, so the same text elsewhere (a title, say) is never taken
    ///     for it, and a name both read and drawn gets the action of the value written first. A model is parsed by the
    ///     OpenSCAD library, so a comment or a comparison such as <c>a &lt; b &amp;&amp; c &gt; d</c> is never taken for a
    ///     directive, and the line is that of the directive's keyword, so a comment or string naming the same file does
    ///     not move it; <c>import()</c> and <c>surface()</c> read no file, so they are not checked. Stateless and
    ///     thread-safe.
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

        try
        {
            // The document keeps these bytes, so each value the walk takes is located in them while it is open
            using var document = JsonDocument.Parse(json, ChartJsonOptions);
            var spec = document.RootElement;
            if (!IsTable(spec))
            {
                return null;
            }

            // The values the walk would follow, kept only when they are local
            var urls = new List<JsonElement>();
            CollectDataUrls(spec, urls, 0);
            var data = urls.Where(url => FilterPaths.LocalDataFile(AnyFolder, url.GetString()!) != null)
                .Select(url => ("reads the data file", url));

            var links = new List<JsonElement>();
            var fields = new HashSet<string>(StringComparer.Ordinal);
            CollectImageLinks(spec, links, fields, 0);
            if (fields.Count > 0)
            {
                CollectRowLinks(spec, links, fields, 0);
            }

            var pictures = links.Where(link => IsLocalPicture(link.GetString()!))
                .Select(link => ("draws the picture", link));

            // The one written first, with its line
            return FirstInText(json, data.Concat(pictures));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Determines whether a link of a chart names a local picture, as written or percent-decoded.
    /// </summary>
    /// <param name="link">The link.</param>
    /// <returns><see langword="true"/> when the link names a local picture.</returns>
    private static bool IsLocalPicture(string link)
    {
        return FilterPaths.LocalImageFile(AnyFolder, link) != null ||
               FilterPaths.LocalImageFile(AnyFolder, FilterPaths.DecodePercent(link)) != null;
    }

    /// <summary>
    ///     Finds the value written first in a chart's text, with the line it is on.
    /// </summary>
    /// <param name="json">The bytes the chart was parsed from, without a byte order mark.</param>
    /// <param name="found">The values with what the chart does with them, data URLs first; they belong to the open document of <paramref name="json"/>.</param>
    /// <returns>The value written first (a data URL when the same value is both), or <see langword="null"/> when there is none.</returns>
    private static BlockReference? FirstInText(ReadOnlyMemory<byte> json, IEnumerable<(string Action, JsonElement Value)> found)
    {
        (string Action, JsonElement Value)? first = null;
        var firstOffset = 0;
        foreach (var candidate in found)
        {
            var offset = OffsetOf(json.Span, candidate.Value);
            if (first == null || offset < firstOffset)
            {
                first = candidate;
                firstOffset = offset;
            }
        }

        if (first is not { } reference)
        {
            return null;
        }

        var line = 1 + json.Span[..firstOffset].Count((byte)'\n');
        return new BlockReference(reference.Action, reference.Value.GetString()!, line);
    }

    /// <summary>
    ///     Gets the position of a value in the bytes its document was parsed from.
    /// </summary>
    /// <param name="json">The bytes the document was parsed from.</param>
    /// <param name="value">A JSON string of the document.</param>
    /// <returns>The offset of its opening quote.</returns>
    /// <remarks>
    ///     <see cref="JsonDocument.Parse(ReadOnlyMemory{byte}, JsonDocumentOptions)"/> uses the caller's bytes for the
    ///     lifetime of the document without copying them, so the raw text of a value is a slice of them.
    /// </remarks>
    private static int OffsetOf(ReadOnlySpan<byte> json, JsonElement value)
    {
        _ = json.Overlaps(JsonMarshal.GetRawUtf8Value(value), out var offset);
        return offset;
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
