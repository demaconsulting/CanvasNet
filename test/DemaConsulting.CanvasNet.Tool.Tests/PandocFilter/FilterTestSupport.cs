using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using DemaConsulting.CanvasNet.Tool.PandocFilter;

namespace DemaConsulting.CanvasNet.Tool.Tests.PandocFilter;

/// <summary>
///     Builds the Pandoc JSON documents of the filter tests, as <c>pandoc -t json</c> writes them.
/// </summary>
internal static class PandocDoc
{
    /// <summary>
    ///     Builds an <c>Image</c> element whose description is the word <c>pic</c>.
    /// </summary>
    /// <param name="src">The source.</param>
    /// <param name="attributes">The key-value attributes.</param>
    /// <returns>The element's JSON.</returns>
    public static string Image(string src, params (string Key, string Value)[] attributes)
    {
        return $$"""{"t":"Image","c":[["",[],{{Pairs(attributes)}}],[{"t":"Str","c":"pic"}],[{{Text(src)}},""]]}""";
    }

    /// <summary>
    ///     Builds a <c>Para</c> element.
    /// </summary>
    /// <param name="inlines">The inline elements' JSON.</param>
    /// <returns>The element's JSON.</returns>
    public static string Para(params string[] inlines)
    {
        return $$"""{"t":"Para","c":[{{string.Join(',', inlines)}}]}""";
    }

    /// <summary>
    ///     Builds a <c>CodeBlock</c> element.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="classes">The classes.</param>
    /// <param name="text">The text.</param>
    /// <param name="attributes">The key-value attributes.</param>
    /// <returns>The element's JSON.</returns>
    public static string CodeBlock(string id, string[] classes, string text, params (string Key, string Value)[] attributes)
    {
        var classList = string.Join(',', classes.Select(Text));
        return $$"""{"t":"CodeBlock","c":[[{{Text(id)}},[{{classList}}],{{Pairs(attributes)}}],{{Text(text)}}]}""";
    }

    /// <summary>
    ///     Builds a document of blocks with empty metadata.
    /// </summary>
    /// <param name="blocks">The blocks' JSON.</param>
    /// <returns>The document as UTF-8 bytes.</returns>
    public static byte[] Document(params string[] blocks)
    {
        return Encoding.UTF8.GetBytes($$"""{"pandoc-api-version":[1,23,1,2],"meta":{},"blocks":[{{string.Join(',', blocks)}}]}""");
    }

    /// <summary>
    ///     Gets the warnings a run returned in the metadata.
    /// </summary>
    /// <param name="output">The output document.</param>
    /// <returns>The warnings, in order; empty when there are none.</returns>
    public static List<string> Warnings(byte[] output)
    {
        using var document = JsonDocument.Parse(output);
        return document.RootElement.GetProperty("meta").TryGetProperty(PandocAst.WarningsKey, out var list)
            ? list.GetProperty("c").EnumerateArray().Select(m => m.GetProperty("c").GetString()!).ToList()
            : [];
    }

    /// <summary>
    ///     Gets the sources of every image of an output document, in document order.
    /// </summary>
    /// <param name="output">The output document.</param>
    /// <returns>The sources.</returns>
    public static List<string> ImageSources(byte[] output)
    {
        var sources = new List<string>();
        void Walk(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty("t", out var type) && type.ValueKind == JsonValueKind.String && type.GetString() == "Image")
                {
                    sources.Add(element.GetProperty("c")[2][0].GetString()!);
                }

                foreach (var property in element.EnumerateObject())
                {
                    Walk(property.Value);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    Walk(item);
                }
            }
        }

        using var document = JsonDocument.Parse(output);
        Walk(document.RootElement.GetProperty("blocks"));
        return sources;
    }

    /// <summary>
    ///     Writes a JSON string.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The JSON string.</returns>
    private static string Text(string text)
    {
        return JsonSerializer.Serialize(text);
    }

    /// <summary>
    ///     Writes key-value attributes.
    /// </summary>
    /// <param name="attributes">The attributes.</param>
    /// <returns>The JSON array of pairs.</returns>
    private static string Pairs((string Key, string Value)[] attributes)
    {
        return "[" + string.Join(',', attributes.Select(a => $"[{Text(a.Key)},{Text(a.Value)}]")) + "]";
    }
}

/// <summary>
///     A document tree of a filter test: a temporary folder with <c>docs/filters/canvasnet.lua</c> (named, not written),
///     <c>docs/guide</c> for the sources, and the cache in <c>docs/generated/canvasnet</c>.
/// </summary>
internal sealed class FilterTree : IDisposable
{
    /// <summary>
    ///     The temporary folder.
    /// </summary>
    private readonly TempDirectory _directory = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="FilterTree"/> class and creates the guide folder.
    /// </summary>
    public FilterTree()
    {
        Directory.CreateDirectory(Guide);
    }

    /// <summary>
    ///     Gets the root of the tree, the working directory of the runs.
    /// </summary>
    public string Root => _directory.Path;

    /// <summary>
    ///     Gets the folder of the sources (the resource path).
    /// </summary>
    public string Guide => _directory.Combine("docs", "guide");

    /// <summary>
    ///     Gets the cache folder.
    /// </summary>
    public string Cache => _directory.Combine("docs", "generated", "canvasnet");

    /// <summary>
    ///     Gets or sets the environment variables of the runs.
    /// </summary>
    public Dictionary<string, string> Environment { get; } = new(StringComparer.Ordinal);

    /// <summary>
    ///     Writes a source file into the guide folder.
    /// </summary>
    /// <param name="name">The relative name.</param>
    /// <param name="content">The text.</param>
    /// <returns>The full path.</returns>
    public string Write(string name, string content)
    {
        var path = Path.Combine(Guide, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>
    ///     Creates the settings of a run: HTML output to <c>out/doc.html</c>, the guide as the resource path, version 1.0.0.
    /// </summary>
    /// <returns>The settings.</returns>
    public FilterSettings Settings()
    {
        return new FilterSettings
        {
            FilterPath = "docs/filters/canvasnet.lua",
            WorkingDirectory = Root,
            Environment = name => Environment.TryGetValue(name, out var value) ? value : null,
            Format = "html5",
            OutputFile = "out/doc.html",
            ResourcePaths = ["docs/guide"],
            ToolVersion = "1.0.0"
        };
    }

    /// <summary>
    ///     Runs the engine over a document.
    /// </summary>
    /// <param name="drawer">The drawer.</param>
    /// <param name="document">The document.</param>
    /// <param name="jobs">The pictures drawn at once.</param>
    /// <returns>The output document.</returns>
    public byte[] Run(IPictureDrawer drawer, byte[] document, int jobs = 1)
    {
        return new PandocFilterEngine(Settings(), drawer, jobs).Run(document, "3.12.1");
    }

    /// <summary>
    ///     Gets the names of the files in the cache folder, sorted.
    /// </summary>
    /// <returns>The names.</returns>
    public List<string> CacheFiles()
    {
        return Directory.Exists(Cache) ? [.. Directory.GetFiles(Cache).Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal)] : [];
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _directory.Dispose();
    }
}

/// <summary>
///     A drawer that writes a fixed picture and counts its calls, with configurable failures and notes.
/// </summary>
/// <remarks>Safe to call from several threads at once, as the engine does with more than one job.</remarks>
internal sealed class StubDrawer : IPictureDrawer
{
    /// <summary>
    ///     The bytes written for every picture.
    /// </summary>
    public static readonly byte[] Picture = Encoding.ASCII.GetBytes("stub picture");

    /// <summary>
    ///     The calls, as <c>render NAME</c> or <c>flatten NAME</c> with the source's file name.
    /// </summary>
    private readonly ConcurrentQueue<string> _calls = new();

    /// <summary>
    ///     Gets the failure of a source by its file name, or <see langword="null"/> to draw it.
    /// </summary>
    public Func<string, Exception?> Failure { get; init; } = _ => null;

    /// <summary>
    ///     Gets what the tool reports for a source by its file name.
    /// </summary>
    public Func<string, PictureDrawing> Report { get; init; } = _ => new PictureDrawing(true, [], []);

    /// <summary>
    ///     Gets the calls so far.
    /// </summary>
    public IReadOnlyList<string> Calls => [.. _calls];

    /// <inheritdoc />
    public PictureDrawing Render(IReadOnlyList<string> args)
    {
        return Draw("render", args);
    }

    /// <inheritdoc />
    public PictureDrawing Flatten(IReadOnlyList<string> args)
    {
        return Draw("flatten", args);
    }

    /// <summary>
    ///     Records a call, then fails or writes the picture and reports.
    /// </summary>
    /// <param name="verb">The command.</param>
    /// <param name="args">The arguments.</param>
    /// <returns>The report.</returns>
    private PictureDrawing Draw(string verb, IReadOnlyList<string> args)
    {
        var name = Path.GetFileName(args[0]);
        _calls.Enqueue($"{verb} {name}");
        if (Failure(name) is { } failure)
        {
            throw failure;
        }

        var report = Report(name);
        if (report.Flattened)
        {
            File.WriteAllBytes(args[args.ToList().IndexOf("--output") + 1], Picture);
        }

        return report;
    }
}
