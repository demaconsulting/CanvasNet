// cspell:ignore canvasnet textmetrics mercator geoshape geojson longitude hconcat
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Pdf;
using DemaConsulting.CanvasNet.Pptx;
using DemaConsulting.CanvasNet.Scad;
using DemaConsulting.CanvasNet.Svg;
using DemaConsulting.CanvasNet.Tool.Commands;
using DemaConsulting.CanvasNet.Tool.Imaging;
using DemaConsulting.CanvasNet.VegaLite;
using DemaConsulting.CanvasNet.Vsdx;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Unit tests for the HelpTopics class: the embedded catalog, its consistency with the code and the libraries,
///     the examples, and the text and JSON renderings.
/// </summary>
[Collection("Sequential")]
public partial class HelpTopicsTests
{
    /// <summary>
    ///     The placeholder name of the identifier naming rule, which is a description and not an identifier.
    /// </summary>
    private const string NamingRule = "vl-<area>-<detail>";

    /// <summary>
    ///     Matches the diagnostic identifiers a topic may cite.
    /// </summary>
    private static readonly Regex IdentifierPattern =
        new(@"\b(?:scad|vl|pptx|vsdx|pdf)-[A-Za-z0-9<>-]+", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));

    /// <summary>
    ///     The names of the global options, which every command accepts.
    /// </summary>
    private static readonly string[] GlobalOptionNames =
        ["--json", "--silent", "--log", "--validate", "--results", "--result", "--depth", "--version", "--help", "-v", "-?", "-h"];

    /// <summary>
    ///     Gets the source examples as theory data: the topic name and the example name.
    /// </summary>
    public static IEnumerable<object[]> SourceExampleData =>
        HelpTopics.SourceExamples().Select(e => new object[] { e.Topic.Name, e.Example.Name });

    /// <summary>
    ///     Gets the names of every topic as theory data.
    /// </summary>
    public static IEnumerable<object[]> TopicNameData => HelpTopics.TopicNames.Select(n => new object[] { n });

    /// <summary>
    ///     Test that every topic loads, with a title, a summary, sections, and examples, in catalog order, and that
    ///     the embedded topic files are exactly the catalog (no orphan file and no missing file).
    /// </summary>
    [Fact]
    public void HelpTopics_All_EveryTopicLoads_WithTitleSummaryAndSections()
    {
        // Arrange / Act: load the catalog and list the embedded topic files
        var topics = HelpTopics.All;
        var embedded = typeof(HelpTopics).Assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(HelpTopics.ResourcePrefix, StringComparison.Ordinal)
                        && !n.StartsWith(HelpTopics.ExampleResourcePrefix, StringComparison.Ordinal)
                        && n.EndsWith(".json", StringComparison.Ordinal))
            .Select(n => n[HelpTopics.ResourcePrefix.Length..^".json".Length])
            .Order(StringComparer.Ordinal)
            .ToList();

        // Assert: the topics are the catalog, each is complete, and the files are exactly the catalog
        Assert.Equal(HelpTopics.TopicNames, topics.Select(t => t.Name));
        Assert.Equal(HelpTopics.TopicNames.Order(StringComparer.Ordinal), embedded);
        Assert.All(topics, topic =>
        {
            Assert.False(string.IsNullOrWhiteSpace(topic.Title));
            Assert.False(string.IsNullOrWhiteSpace(topic.Summary));
            Assert.NotEmpty(topic.Sections);
            Assert.NotEmpty(topic.Examples);
            Assert.All(topic.Sections, section => Assert.NotEmpty(section.Items));
        });
    }

    /// <summary>
    ///     Test that every alias resolves to its topic, ignoring case, and that an unknown name resolves to nothing.
    /// </summary>
    [Fact]
    public void HelpTopics_Find_EveryAlias_ResolvesIgnoringCase()
    {
        // Arrange: the documented aliases
        var aliases = new Dictionary<string, string>
        {
            ["openscad"] = "scad",
            ["vegalite"] = "vega-lite",
            ["vl"] = "vega-lite",
            ["powerpoint"] = "pptx",
            ["visio"] = "vsdx",
            ["png"] = "raster",
            ["jpeg"] = "raster",
            ["jpg"] = "raster",
            ["bmp"] = "raster",
            ["tiff"] = "raster",
            ["tif"] = "raster",
            ["gif"] = "raster",
            ["jpeg2000"] = "raster",
            ["jp2"] = "raster"
        };

        // Act / Assert: each alias, in lower and upper case, and each canonical name resolves to its topic
        foreach (var (alias, name) in aliases)
        {
            Assert.Equal(name, HelpTopics.Find(alias)?.Name);
            Assert.Equal(name, HelpTopics.Find(alias.ToUpperInvariant())?.Name);
        }

        Assert.All(HelpTopics.TopicNames, name => Assert.Equal(name, HelpTopics.Find(name)?.Name));
        Assert.Equal(aliases.Count, HelpTopics.All.Sum(t => t.Aliases.Count));
        Assert.Null(HelpTopics.Find("nothing"));
    }

    /// <summary>
    ///     Test that no topic name or alias collides with a command, its alias, the reserved help name, the topics
    ///     subject, or another topic.
    /// </summary>
    [Fact]
    public void HelpTopics_Names_DoNotCollideWithCommandsHelpOrEachOther()
    {
        // Arrange: every name the command line already answers to
        var registry = CommandRegistry.CreateDefault();
        var taken = registry.Commands.SelectMany(c => c.Aliases.Prepend(c.Name)).Append("help").Append("topics").ToList();
        var topicNames = HelpTopics.All.SelectMany(t => t.Aliases.Prepend(t.Name)).ToList();

        // Act / Assert: the topic names are unique and none is taken
        Assert.Equal(topicNames.Count, topicNames.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Empty(topicNames.Intersect(taken, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Test that no text contains an unresolved token and that the numbers in the topics are the live constants.
    /// </summary>
    [Fact]
    public void HelpTopics_Text_HasNoUnresolvedTokens()
    {
        // Arrange / Act: every string of every topic
        var texts = HelpTopics.All.SelectMany(AllStrings).ToList();
        var limits = string.Join("\n", HelpTopics.All.First(t => t.Name == "limits").Sections
            .SelectMany(s => s.Items).Select(i => i.Text));

        // Assert: no token is left, the table holds the live values, and the limits topic quotes them
        Assert.DoesNotContain(texts, t => t.Contains("{{", StringComparison.Ordinal));
        Assert.Equal(Surface.MaxDimension.ToString(System.Globalization.CultureInfo.InvariantCulture), HelpTopics.Tokens["maxDimension"]);
        Assert.Equal(ScadLimits.Default.MaxTriangles.ToString(System.Globalization.CultureInfo.InvariantCulture), HelpTopics.Tokens["scadMaxTriangles"]);
        Assert.Equal(ArgumentReader.MaxDpi.ToString(System.Globalization.CultureInfo.InvariantCulture), HelpTopics.Tokens["maxDpi"]);
        Assert.Equal(ResultWriter.SchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture), HelpTopics.Tokens["schemaVersion"]);
        Assert.Contains($"{Surface.MaxDimension} pixels", limits);
        Assert.Contains($"Default {ScadLimits.Default.MaxTriangles} triangles", limits);
        Assert.Contains($"At most {VegaLiteLimits.Default.MaxDataFiles} files", limits);
        Assert.Contains($"{ImageWriter.MinQuality} to {ImageWriter.MaxQuality}, default {ImageWriter.DefaultQuality}", limits);
    }

    /// <summary>
    ///     Test that every format topic has the sections the agent needs and at least two examples.
    /// </summary>
    [Fact]
    public void HelpTopics_FormatTopics_HaveSupportedUnsupportedOptionsDiagnosticsDifferencesAndExamples()
    {
        // Arrange / Act / Assert: each format topic has the five sections, filled, and two examples
        foreach (var name in HelpTopics.FormatTopicNames)
        {
            var topic = HelpTopics.All.First(t => t.Name == name);
            foreach (var id in new[] { "supported", "unsupported", "options", "diagnostics", "differences" })
            {
                Assert.True(
                    topic.Sections.Any(s => s.Id == id && s.Items.Count > 0),
                    $"Topic '{name}' lacks a filled section '{id}'.");
            }

            Assert.True(topic.Examples.Count >= 2, $"Topic '{name}' has fewer than two examples.");
        }
    }

    /// <summary>
    ///     Test that the examples have valid kinds, and that source examples have non-empty content and the file
    ///     extension their format needs.
    /// </summary>
    [Fact]
    public void HelpTopics_Examples_NamesUniqueKindsValidAndSourcesNonEmpty()
    {
        // Arrange: the extension each topic's source examples must have
        var extensions = new Dictionary<string, string>
        {
            ["scad"] = ".scad",
            ["vega-lite"] = ".vl.json",
            ["svg"] = ".svg"
        };

        // Act / Assert: the rules hold for every example of every topic
        foreach (var topic in HelpTopics.All)
        {
            Assert.Equal(topic.Examples.Count, topic.Examples.Select(e => e.Name).Distinct().Count());
            foreach (var example in topic.Examples)
            {
                Assert.False(string.IsNullOrWhiteSpace(example.Content));
                Assert.DoesNotContain('\r', example.Content);
                Assert.False(example.Content.EndsWith('\n'));
                if (example.Kind == HelpTopics.CommandKind)
                {
                    Assert.StartsWith("canvasnet ", example.Content);
                    Assert.Null(example.FileName);
                    continue;
                }

                Assert.Equal(HelpTopics.SourceKind, example.Kind);
                Assert.NotNull(example.FileName);
                if (extensions.TryGetValue(topic.Name, out var extension))
                {
                    Assert.EndsWith(extension, example.FileName);
                }
            }
        }

        Assert.Contains(HelpTopics.SourceExamples(), e => e.Topic.Name == "scad");
        Assert.Contains(HelpTopics.SourceExamples(), e => e.Topic.Name == "vega-lite");
        Assert.Contains(HelpTopics.SourceExamples(), e => e.Topic.Name == "svg");
    }

    /// <summary>
    ///     Test that every text example (SCAD, Vega-Lite, SVG) renders through the real pipeline with <c>--strict</c>
    ///     and reports no diagnostic.
    /// </summary>
    /// <param name="topicName">The topic of the example.</param>
    /// <param name="exampleName">The name of the example.</param>
    [Theory]
    [MemberData(nameof(SourceExampleData))]
    public void HelpTopics_SourceExamples_RenderStrictWithoutDiagnostics(string topicName, string exampleName)
    {
        // Arrange: the example saved under its file name; a model is drawn small to stay fast
        var (topic, example) = HelpTopics.SourceExamples().First(e => e.Topic.Name == topicName && e.Example.Name == exampleName);
        using var directory = new TempDirectory();
        var input = directory.Combine($"{topic.Name}-{example.FileName}");
        var output = directory.Combine("out.png");
        File.WriteAllText(input, example.Content);
        string[] size = topic.Name == "scad" ? ["--size", "64x64"] : [];

        // Act: render with --strict
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke(["render", input, "--strict", "--json", "-o", output, .. size]);

        // Assert: it succeeded without a diagnostic and wrote the picture
        Assert.True(exitCode == 0, stdErr + stdOut);
        using var document = JsonDocument.Parse(stdOut);
        Assert.True(document.RootElement.GetProperty("ok").GetBoolean());
        Assert.Empty(document.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.True(File.Exists(output));
    }

    /// <summary>
    ///     Test that every command example names a known command and uses only options that command (or the tool)
    ///     declares, so a documented command line cannot be a mistake.
    /// </summary>
    [Fact]
    public void HelpTopics_CommandExamples_UseKnownCommandsAndOptions()
    {
        // Arrange: the default registry
        var registry = CommandRegistry.CreateDefault();

        // Act / Assert: check the words of every command example
        foreach (var (topic, example) in HelpTopics.All.SelectMany(t => t.Examples, (t, e) => (t, e)).Where(x => x.e.Kind == HelpTopics.CommandKind))
        {
            var words = example.Content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal("canvasnet", words[0]);

            var known = new HashSet<string>(GlobalOptionNames, StringComparer.Ordinal);
            if (words[1] == "help")
            {
                known.UnionWith(["--example", "--examples"]);
            }
            else
            {
                var command = registry.Find(words[1]);
                Assert.True(command != null, $"Example '{example.Name}' of '{topic.Name}' uses the unknown command '{words[1]}'.");
                known.UnionWith(command.Options.SelectMany(o => o.Aliases.Prepend(o.Name)));
            }

            foreach (var option in words.Skip(2).Where(w => w.StartsWith('-') && w.Length > 1 && !char.IsAsciiDigit(w[1])))
            {
                var name = option.Split('=')[0];
                Assert.True(known.Contains(name), $"Example '{example.Name}' of '{topic.Name}' uses the unknown option '{name}'.");
            }
        }
    }

    /// <summary>
    ///     Test that every option a topic lists is an option of <c>render</c> or a global option.
    /// </summary>
    [Fact]
    public void HelpTopics_OptionTerms_ExistInTheRenderOptionTable()
    {
        // Arrange: every name the render command and the tool accept
        var known = RenderCommand.OptionTable.SelectMany(o => o.Aliases.Prepend(o.Name)).Concat(GlobalOptionNames).ToHashSet(StringComparer.Ordinal);

        // Act / Assert: the first word of each term of each options section is one of them
        foreach (var topic in HelpTopics.All)
        {
            foreach (var item in topic.Sections.Where(s => s.Id == "options").SelectMany(s => s.Items).Where(i => i.Term != null))
            {
                var name = item.Term!.Split(' ')[0];
                Assert.True(known.Contains(name), $"Topic '{topic.Name}' lists the unknown option '{name}'.");
            }
        }
    }

    /// <summary>
    ///     Test that the errors topic lists every error code of the tool, and nothing else as a code.
    /// </summary>
    [Fact]
    public void HelpTopics_ErrorsTopic_ListsEveryErrorCode()
    {
        // Arrange: the codes of the code, and the terms of the codes section
        var codes = ErrorCodeValues().ToList();
        var terms = HelpTopics.All.First(t => t.Name == "errors").Sections.First(s => s.Id == "codes").Items
            .Select(i => i.Term!)
            .ToList();

        // Act / Assert: the same set
        Assert.NotEmpty(codes);
        Assert.Equal(codes.Order(StringComparer.Ordinal), terms.Order(StringComparer.Ordinal));
    }

    /// <summary>
    ///     Test that every diagnostic identifier a topic cites exists: it is covered by the trigger table (which
    ///     the trigger test proves), is an error code, or is a literal in one of the libraries.
    /// </summary>
    [Fact]
    public void HelpTopics_CitedDiagnosticIds_ExistInTheLibraries()
    {
        // Arrange: what a cited identifier may be, and the text of the libraries to search for literals
        var allowed = Triggers.SelectMany(t => new[] { t.Cited, t.Code }).Concat(["scad-warning", "vega-lite-warning", NamingRule]).Concat(ErrorCodeValues()).ToHashSet(StringComparer.Ordinal);
        var literals = LibraryAssemblies.Select(a => File.ReadAllBytes(a.Location)).ToList();
        var cited = HelpTopics.All.SelectMany(AllStrings).SelectMany(s => IdentifierPattern.Matches(s).Select(m => m.Value)).Distinct().Order(StringComparer.Ordinal).ToList();

        // Act / Assert: each cited identifier is allowed, or its literal (the part before a '<' placeholder) is in a library
        Assert.NotEmpty(cited);
        foreach (var token in cited)
        {
            var literal = token.Contains('<') ? token[..token.IndexOf('<')] : token;
            var needle = Encoding.Unicode.GetBytes(literal);
            var found = allowed.Contains(token) || literals.Any(bytes => bytes.AsSpan().IndexOf(needle) >= 0);
            Assert.True(found, $"The identifier '{token}' is cited by a help topic but is neither triggered by a test nor a literal of a library.");
        }
    }

    /// <summary>
    ///     Test that the text of a topic is deterministic, plain, wrapped, and complete.
    /// </summary>
    /// <param name="name">The topic name.</param>
    [Theory]
    [MemberData(nameof(TopicNameData))]
    public void HelpTopics_RenderText_IsDeterministicPlainAndWrapped(string name)
    {
        // Arrange / Act: render the topic twice
        var topic = HelpTopics.Find(name)!;
        var first = HelpTopics.RenderText(topic);
        var second = HelpTopics.RenderText(topic);

        // Assert: identical, without escape sequences or a trailing newline, wrapped, and complete
        Assert.Equal(first, second);
        Assert.DoesNotContain('\u001b', first);
        Assert.DoesNotContain('\r', first);
        Assert.False(first.EndsWith('\n'));
        Assert.StartsWith($"Topic: {topic.Title}", first);
        Assert.All(first.Split('\n'), line => Assert.True(line.Length <= 80, $"Line too long: {line}"));
        Assert.All(topic.Sections, section => Assert.Contains($"{section.Title}:", first));
        Assert.All(topic.Examples, example => Assert.Contains(example.Name, first));
        Assert.All(topic.Sections.SelectMany(s => s.Items).Where(i => i.Term != null), item => Assert.Contains($"  {item.Term}\n", first));
        Assert.Contains($"canvasnet help {topic.Name} --example <name>", first);
    }

    /// <summary>
    ///     Test that a word longer than the width is kept whole on a line of its own.
    /// </summary>
    [Fact]
    public void HelpTopics_AppendWrapped_LongWord_IsNotSplit()
    {
        // Arrange: a word longer than the text width between short words
        var longWord = new string('x', HelpTopics.TextWidth + 10);
        var builder = new StringBuilder();

        // Act: wrap with a hanging indent
        HelpTopics.AppendWrapped(builder, $"a {longWord} b", "- ", "  ");

        // Assert: the long word has its own line, indented, and nothing is lost
        var lines = builder.ToString().TrimEnd().Split(Environment.NewLine);
        Assert.Equal(["- a", $"  {longWord}", "  b"], lines);
    }

    /// <summary>
    ///     Test that the JSON of a topic has the documented shape, on one line and in ASCII.
    /// </summary>
    /// <param name="name">The topic name.</param>
    [Theory]
    [MemberData(nameof(TopicNameData))]
    public void HelpTopics_RenderJson_HasTheDocumentedShape(string name)
    {
        // Arrange / Act: render and parse the document
        var topic = HelpTopics.Find(name)!;
        var json = HelpTopics.RenderJson(topic);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        // Assert: one ASCII line with the envelope and the fields of a topic
        Assert.DoesNotContain('\n', json);
        Assert.All(json, c => Assert.True(c < 0x80));
        Assert.Equal(ResultWriter.SchemaVersion, root.GetProperty("schemaVersion").GetInt32());
        Assert.True(root.GetProperty("ok").GetBoolean());
        Assert.Equal("help", root.GetProperty("command").GetString());
        Assert.Equal("topic", root.GetProperty("kind").GetString());
        Assert.Equal(topic.Name, root.GetProperty("topic").GetString());
        Assert.Equal(topic.Aliases, root.GetProperty("aliases").EnumerateArray().Select(a => a.GetString()));
        Assert.Equal(topic.Title, root.GetProperty("title").GetString());
        Assert.Equal(topic.Summary, root.GetProperty("summary").GetString());
        var sections = root.GetProperty("sections").EnumerateArray().ToList();
        Assert.Equal(topic.Sections.Select(s => s.Id), sections.Select(s => s.GetProperty("id").GetString()));
        Assert.All(sections, s =>
        {
            Assert.False(string.IsNullOrEmpty(s.GetProperty("title").GetString()));
            Assert.All(s.GetProperty("items").EnumerateArray(), i => Assert.False(string.IsNullOrEmpty(i.GetProperty("text").GetString())));
        });
        var examples = root.GetProperty("examples").EnumerateArray().ToList();
        Assert.Equal(topic.Examples.Select(e => e.Content), examples.Select(e => e.GetProperty("content").GetString()));
        Assert.All(examples, e =>
        {
            Assert.False(string.IsNullOrEmpty(e.GetProperty("name").GetString()));
            Assert.Contains(e.GetProperty("kind").GetString(), new[] { "source", "command" });
            Assert.False(string.IsNullOrEmpty(e.GetProperty("language").GetString()));
            Assert.False(string.IsNullOrEmpty(e.GetProperty("description").GetString()));
        });
    }

    /// <summary>
    ///     Test that an unknown example name is a usage error that lists the names of the topic.
    /// </summary>
    [Fact]
    public void HelpTopics_RenderExample_UnknownName_ThrowsUsageErrorListingNames()
    {
        // Arrange: the scad topic
        var topic = HelpTopics.Find("scad")!;

        // Act / Assert: the error names the topic and the example and lists the valid names; JSON fails the same way
        var exception = Assert.Throws<ToolException>(() => HelpTopics.RenderExample(topic, "nothing"));
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
        Assert.Equal("Topic 'scad' has no example 'nothing'.", exception.Message);
        Assert.Contains("plate", exception.Hint);
        Assert.Equal("help", exception.Command);
        Assert.Throws<ToolException>(() => HelpTopics.RenderExampleJson(topic, "nothing"));
    }

    /// <summary>
    ///     Test that a source example is returned exactly as embedded (LF line endings, no trailing newline), and
    ///     that the list and JSON renderings of the examples describe it.
    /// </summary>
    [Fact]
    public void HelpTopics_RenderExample_Source_ReturnsExactEmbeddedContent()
    {
        // Arrange: the plate example and the embedded resource it comes from
        var topic = HelpTopics.Find("scad")!;
        using var stream = typeof(HelpTopics).Assembly.GetManifestResourceStream($"{HelpTopics.ExampleResourcePrefix}scad-plate.scad")!;
        using var reader = new StreamReader(stream);
        var expected = reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');

        // Act
        var content = HelpTopics.RenderExample(topic, "PLATE");
        var list = HelpTopics.RenderExampleList(topic);
        using var listJson = JsonDocument.Parse(HelpTopics.RenderExampleListJson(topic));
        using var oneJson = JsonDocument.Parse(HelpTopics.RenderExampleJson(topic, "plate"));

        // Assert: the content is exact; the list has one line per example; the JSON list has no content, the one example has it
        Assert.Equal(expected, content);
        Assert.StartsWith("// A plate with a round hole", content);
        Assert.Equal(topic.Examples.Count, list.Split('\n').Count(l => l.Contains("[source]") || l.Contains("[command]")));
        Assert.Equal("examples", listJson.RootElement.GetProperty("kind").GetString());
        Assert.All(listJson.RootElement.GetProperty("examples").EnumerateArray(), e => Assert.False(e.TryGetProperty("content", out _)));
        Assert.Equal("example", oneJson.RootElement.GetProperty("kind").GetString());
        Assert.Equal(expected, oneJson.RootElement.GetProperty("example").GetProperty("content").GetString());
        Assert.Equal("plate.scad", oneJson.RootElement.GetProperty("example").GetProperty("fileName").GetString());
    }

    /// <summary>
    ///     Gets the values of every error code constant of the tool.
    /// </summary>
    /// <returns>The code strings.</returns>
    private static IEnumerable<string> ErrorCodeValues()
    {
        return typeof(ErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!);
    }

    /// <summary>
    ///     Gets every string of a topic that is shown to a reader.
    /// </summary>
    /// <param name="topic">The topic.</param>
    /// <returns>The title, summary, aliases, section titles, items, and examples (without the source of a source example).</returns>
    private static IEnumerable<string> AllStrings(HelpTopic topic)
    {
        yield return topic.Title;
        yield return topic.Summary;
        foreach (var section in topic.Sections)
        {
            yield return section.Title;
            foreach (var item in section.Items)
            {
                if (item.Term != null)
                {
                    yield return item.Term;
                }

                yield return item.Text;
            }
        }

        foreach (var example in topic.Examples)
        {
            yield return example.Description;
            if (example.Kind == HelpTopics.CommandKind)
            {
                yield return example.Content;
            }
        }
    }

    /// <summary>
    ///     Gets the library assemblies whose string literals may define a diagnostic identifier.
    /// </summary>
    private static IEnumerable<Assembly> LibraryAssemblies =>
    [
        typeof(ScadDocument).Assembly,
        typeof(VegaLiteDocument).Assembly,
        typeof(PdfDocument).Assembly,
        typeof(PptxDocument).Assembly,
        typeof(VsdxDocument).Assembly,
        typeof(SvgCodec).Assembly,
        typeof(Surface).Assembly
    ];
}
