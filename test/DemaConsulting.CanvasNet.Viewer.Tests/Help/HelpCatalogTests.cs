using System.Text.Json;
using DemaConsulting.CanvasNet.Viewer.Help;

namespace DemaConsulting.CanvasNet.Viewer.Tests.Help;

/// <summary>
///     Unit tests for the HelpCatalog unit, on small in-memory catalogs: loading and validation, tags, links, filtered
///     views, extensions, and rendering.
/// </summary>
public partial class HelpCatalogTests
{
    /// <summary>
    ///     The root order of the test catalog.
    /// </summary>
    private static readonly string[] TestRoots = ["a", "d"];

    /// <summary>
    ///     The example files of the test catalog, by <c>&lt;root&gt;-&lt;file&gt;</c>.
    /// </summary>
    private static readonly Dictionary<string, string> TestExamples = new(StringComparer.Ordinal)
    {
        ["a-one.svg"] = "<svg/>\r\n",
        ["a-two.svg"] = "<svg>two</svg>"
    };

    /// <summary>
    ///     Test that the tag lists are the closed vocabulary: the format tags, then the scope tags, each once.
    /// </summary>
    [Fact]
    public void HelpTags_All_IsTheFormatTagsThenTheScopeTags()
    {
        // Act
        var all = HelpTags.All;

        // Assert
        Assert.Equal(HelpTags.FormatTags.Concat(HelpTags.ScopeTags), all);
        Assert.Equal(all.Count, all.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(HelpTags.VegaLite, HelpTags.FormatTags);
        Assert.Contains(HelpTags.FileAccess, HelpTags.ScopeTags);
    }

    /// <summary>
    ///     Test that a catalog lists its cards depth first, its roots in order, and the data of each card.
    /// </summary>
    [Fact]
    public void HelpCatalog_Create_ValidCards_BuildsTheTree()
    {
        // Act
        var catalog = CreateTestCatalog();

        // Assert: the tree
        Assert.Equal(["a", "a/b", "a/b/deep", "a/c", "d"], catalog.Cards.Select(c => c.Id));
        Assert.Equal(["a", "d"], catalog.Roots.Select(c => c.Id));
        var a = catalog.Cards[0];
        Assert.Equal(["a/b", "a/c"], a.ChildIds);
        Assert.Equal(["a/b", "a/c"], catalog.GetChildren(a).Select(c => c.Id));
        Assert.Equal(["d"], a.SeeAlsoIds);
        Assert.Null(a.ParentId);
        Assert.Equal("a/b", catalog.Cards[2].ParentId);
        Assert.Equal("deep", catalog.Cards[2].Name);

        // Assert: the card data, with the example file read and normalized
        Assert.Equal("A", a.Title);
        Assert.Equal("The a card.", a.Summary);
        Assert.Equal([HelpTags.Svg, HelpTags.Authoring], a.Tags);
        var example = Assert.Single(a.Examples);
        Assert.Equal(("one", HelpExample.SourceKind, "xml", "one.svg", "<svg/>"), (example.Name, example.Kind, example.Language, example.FileName, example.Content));
        Assert.Equal(["dee"], catalog.Cards[4].Aliases);
    }

    /// <summary>
    ///     Test that links are written in the library form, and that link text in code is literal.
    /// </summary>
    [Fact]
    public void HelpCatalog_Markdown_Links_RenderInLibraryForm()
    {
        // Act
        var catalog = CreateTestCatalog();

        // Assert: card, own example, and example of a card below; code spans and blocks untouched
        Assert.Equal(
            "See the card `a/b`, the card `d`, the example `one`,\nand the example `two` of the card `a/b/deep`.\n\n"
            + "Code: `[[a/b]]` and ``x [[nope]]``.\n\n```\n[[nope]]\n```\n\nMax {8192}.",
            catalog.Find("a")!.Markdown);
        Assert.Equal("Deep the example `two` of the card `a/b/deep`.", catalog.Find("a/b/deep")!.Markdown);
        Assert.Equal("D links the example `two` of the card `a/b/deep`.", catalog.Find("d")!.Markdown);
    }

    /// <summary>
    ///     Test that a card is found by its path or aliases, ignoring case, with <c>/</c> or white space, and that an
    ///     unknown path gives no card.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="expected">The identifier expected, or <see langword="null"/>.</param>
    [Theory]
    [InlineData("a/b", "a/b")]
    [InlineData("A B deep", "a/b/deep")]
    [InlineData(" dee ", "d")]
    [InlineData("a/x", null)]
    [InlineData("a/b/deep/more", null)]
    [InlineData("", null)]
    public void HelpCatalog_Find_PathsAndAliases_ResolveOrGiveNull(string path, string? expected)
    {
        // Arrange
        var catalog = CreateTestCatalog();

        // Act
        var card = catalog.Find(path);

        // Assert
        Assert.Equal(expected, card?.Id);
    }

    /// <summary>
    ///     Test that a malformed card or an inconsistent tree fails to load with a message that says where and what.
    /// </summary>
    /// <param name="fileName">The file to replace or add.</param>
    /// <param name="text">The text of the file.</param>
    /// <param name="message">A part of the message.</param>
    [Theory]
    [InlineData("a/c.md", "---\ntitle: C\nsummary: S.\ntags:\n  - svg\n  - authoring\n---\n\nOK.\nSee [[a/x]].", "Help card 'a/c.md', line 10: the link '[[a/x]]' names no card 'a/x'")]
    [InlineData("a/c.md", "---\ntitle: C\nsummary: S.\ntags:\n  - svg\n  - authoring\n---\n\nSee [[a#nope]].", "names no example 'nope' of the card 'a'")]
    [InlineData("a/c.md", "---\ntitle: C\nsummary: S.\ntags:\n  - svg\n  - authoring\n---\n\nSee [[#one]].", "names no example 'one' of the card 'a/c'")]
    [InlineData("a/c.md", "---\ntitle: C\nsummary: S.\ntags:\n  - svg\n  - authoring\n---\n\nSee [[A/b]] or [[a b]].", "line 9: text that starts like a link")]
    [InlineData("a/c.md", "---\ntitle: C\nsummary: S.\n---\n\nC.", "lacks the key 'tags'")]
    [InlineData("a/c.md", "---\ntitle: C\nsummary: S.\ntags:\n  - svg\n  - pretty\n---\n\nC.", "line 6: the tag 'pretty' is unknown")]
    [InlineData("a/c.md", "---\ntitle: C\nsummary: S.\ntags:\n  - svg\n---\n\nC.", "at least one scope tag")]
    [InlineData("a/c.md", "---\ntitle: C\nsummary: S.\ntags:\n  - design\n  - design\n---\n\nC.", "a tag is repeated")]
    [InlineData("a/c.md", "---\nextends: a/c\ntitle: C\nsummary: S.\ntags:\n  - design\n---\n\nC.", "not allowed in an overlay")]
    [InlineData("a/c.md", "---\ntitle: C\nsummary: S.\nextends: a/c\n---\n\nC.", "line 4: the key 'extends' is only allowed in an extension")]
    [InlineData("a/c.md", "---\ntitle: C\nsummary: S.\ntags:\n  - design\nsee-also:\n  - zzz\n---\n\nC.", "see-also entry 'zzz'")]
    [InlineData("a/c.md", "---\ntitle: C {{nope}}\nsummary: S.\ntags:\n  - design\n---\n\nC.", "unknown token '{{nope}}'")]
    [InlineData("a/c.md", "---\ntitle: C\nsummary: S.\ntags:\n  - design\nexamples:\n  - name: one\n    kind: source\n    language: xml\n    file: one.svg\n    description: Again.\n---\n\nC.", "example name below 'a' 'one' is repeated")]
    [InlineData("a/c.md", "---\ntitle: C\nsummary: S.\ntags:\n  - design\nexamples:\n  - name: x\n    kind: source\n    language: xml\n    file: gone.svg\n    description: X.\n---\n\nC.", "example file 'a-gone.svg'")]
    [InlineData("a/e.md", "---\ntitle: E\nsummary: S.\ntags:\n  - design\n---\n\nE.", "lists the children a/b, a/c, but its folder holds a/b, a/c, a/e")]
    [InlineData("z.md", "---\ntitle: Z\nsummary: S.\ntags:\n  - design\n---\n\nZ.", "The root help cards are a, d, z")]
    [InlineData("A.md", "---\ntitle: Z\nsummary: S.\ntags:\n  - design\n---\n\nZ.", "must be named")]
    public void HelpCatalog_Create_MalformedCard_ThrowsWithTheFileAndTheReason(string fileName, string text, string message)
    {
        // Arrange: the test cards with one file replaced or added
        var cards = TestCards().Where(c => c.FileName != fileName).Append((fileName, text)).ToList();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => HelpCatalog.Create(cards, TestExamples, TestRoots));

        // Assert
        Assert.Contains(message, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a filter keeps the cards with an included tag and none of the excluded tags.
    /// </summary>
    [Fact]
    public void HelpCatalog_Filter_IncludeExclude_SelectsCardsByTags()
    {
        // Arrange
        var catalog = CreateTestCatalog();

        // Act
        var svg = catalog.Filter([HelpTags.Svg]);
        var notHosting = catalog.Filter(null, [HelpTags.Hosting]);
        var svgNotHosting = catalog.Filter([HelpTags.Svg], [HelpTags.Hosting]);
        var all = catalog.Filter([], []);

        // Assert
        Assert.Equal(["a", "a/b", "a/b/deep", "a/c"], svg.Cards.Select(c => c.Id));
        Assert.Equal(["a", "a/b/deep", "a/c", "d"], notHosting.Cards.Select(c => c.Id));
        Assert.Equal(["a", "a/b/deep", "a/c"], svgNotHosting.Cards.Select(c => c.Id));
        Assert.Equal(catalog.Cards.Select(c => c.Id), all.Cards.Select(c => c.Id));
    }

    /// <summary>
    ///     Test that the visible children of a hidden card move up to its nearest visible ancestor, or to the roots.
    /// </summary>
    [Fact]
    public void HelpCatalog_Filter_HiddenParent_LiftsVisibleChildrenToRootsAndChildren()
    {
        // Arrange
        var catalog = CreateTestCatalog();

        // Act: a/b (hosting) is hidden below a visible a; a is hidden above a visible a/b/deep
        var notHosting = catalog.Filter(null, [HelpTags.Hosting]);
        var design = catalog.Filter([HelpTags.Design]);

        // Assert
        Assert.Equal(["a/b/deep", "a/c"], notHosting.Find("a")!.ChildIds);
        Assert.Equal(["a/b/deep", "a/c"], notHosting.GetChildren(notHosting.Find("a")!).Select(c => c.Id));
        Assert.Equal(["a", "d"], notHosting.Roots.Select(c => c.Id));
        Assert.Equal(["a/b/deep", "d"], design.Roots.Select(c => c.Id));
        Assert.Equal("a/b", design.Find("a/b/deep")!.ParentId);
    }

    /// <summary>
    ///     Test that a filtered view finds a visible card below hidden ones by its full path, and finds no hidden card.
    /// </summary>
    [Fact]
    public void HelpCatalog_Filter_Find_HiddenIsNull_VisibleChildOfHiddenParentResolves()
    {
        // Arrange
        var design = CreateTestCatalog().Filter([HelpTags.Design]);

        // Act / Assert
        Assert.Null(design.Find("a"));
        Assert.Null(design.Find("a/b"));
        Assert.Equal("a/b/deep", design.Find("a b deep")?.Id);
        Assert.Equal("d", design.Find("dee")?.Id);
    }

    /// <summary>
    ///     Test that links and related cards never point at a hidden card or example: a link becomes the target's title
    ///     (or "an example"), and a related hidden card is left out.
    /// </summary>
    [Fact]
    public void HelpCatalog_Filter_HiddenLinkTargets_BecomePlainText()
    {
        // Arrange
        var catalog = CreateTestCatalog();

        // Act: svg without hosting hides a/b and d; design hides a/b/deep's owner of nothing but keeps d
        var svgNotHosting = catalog.Filter([HelpTags.Svg], [HelpTags.Hosting]);
        var hostingOnly = catalog.Filter([HelpTags.Hosting, HelpTags.Design]);
        var a = svgNotHosting.Find("a")!;

        // Assert: hidden cards are their titles, visible examples stay links, and see-also drops hidden cards
        Assert.StartsWith("See Bee, Dee, the example `one`,\nand the example `two` of the card `a/b/deep`.", a.Markdown);
        Assert.Empty(a.SeeAlsoIds);
        Assert.Empty(svgNotHosting.GetSeeAlso(a));
        Assert.DoesNotContain("## See also", svgNotHosting.RenderText(a));
        Assert.Equal(["a/b"], hostingOnly.Find("d")!.SeeAlsoIds);
        Assert.Equal("Back to A.", hostingOnly.Find("a/b")!.Markdown);

        // Assert: an example whose card is hidden is "an example"
        var notDesign = catalog.Filter(null, [HelpTags.Design]);
        Assert.Contains("and an example.", notDesign.Find("a")!.Markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that an unknown tag is rejected.
    /// </summary>
    [Fact]
    public void HelpCatalog_Filter_UnknownTag_ThrowsArgumentException()
    {
        // Arrange
        var catalog = CreateTestCatalog();

        // Act / Assert
        var include = Assert.Throws<ArgumentException>(() => catalog.Filter(["pretty"]));
        var exclude = Assert.Throws<ArgumentException>(() => catalog.Filter(null, ["SVG"]));
        Assert.Equal("include", include.ParamName);
        Assert.Equal("exclude", exclude.ParamName);
    }

    /// <summary>
    ///     Test that a filter of a view keeps only the cards visible in both.
    /// </summary>
    [Fact]
    public void HelpCatalog_Filter_OfAView_Composes()
    {
        // Arrange
        var svg = CreateTestCatalog().Filter([HelpTags.Svg]);

        // Act
        var svgDesign = svg.Filter([HelpTags.Design]);

        // Assert: d has design but not svg
        Assert.Equal(["a/b/deep"], svgDesign.Cards.Select(c => c.Id));
    }

    /// <summary>
    ///     Test that a host formats only the visible links, in order, with the link's card, example, and owner.
    /// </summary>
    [Fact]
    public void HelpCatalog_FormatMarkdown_CallsTheHostOnlyForVisibleLinks()
    {
        // Arrange
        var view = CreateTestCatalog().Filter([HelpTags.Svg], [HelpTags.Hosting]);
        var links = new List<HelpLink>();

        // Act
        var text = view.FormatMarkdown(view.Find("a")!, link =>
        {
            links.Add(link);
            return $"<{link.CardId}|{link.ExampleName}|{link.ExampleCardId}>";
        });

        // Assert
        Assert.StartsWith("See Bee, Dee, <|one|a>,\nand <|two|a/b/deep>.", text);
        Assert.Equal(2, links.Count);
    }

    /// <summary>
    ///     Test that a card the view hides is rejected by the members that take a card.
    /// </summary>
    [Fact]
    public void HelpCatalog_CardMembers_HiddenCard_ThrowArgumentException()
    {
        // Arrange
        var catalog = CreateTestCatalog();
        var hidden = catalog.Find("d")!;
        var view = catalog.Filter([HelpTags.Svg]);

        // Act / Assert
        Assert.Throws<ArgumentException>(() => view.GetChildren(hidden));
        Assert.Throws<ArgumentException>(() => view.GetSeeAlso(hidden));
        Assert.Throws<ArgumentException>(() => view.RenderText(hidden));
        Assert.Throws<ArgumentException>(() => view.RenderJson(hidden));
        Assert.Throws<ArgumentException>(() => view.FormatMarkdown(hidden, _ => string.Empty));
    }

    /// <summary>
    ///     Test that an overlay appends its body, related cards, and examples to its card and replaces the title, that a
    ///     new card starts a new root, and that the extended catalog is unchanged.
    /// </summary>
    [Fact]
    public void HelpCatalog_Extend_OverlayAppendsBodySeeAlsoExamplesAndOverridesTitleSummary()
    {
        // Arrange
        var catalog = CreateTestCatalog();
        (string, string)[] extension =
        [
            ("a/b.md", "---\nextends: a/b\ntitle: Bee {{tool}}\nsee-also:\n  - a/c\n  - d\nexamples:\n  - name: run\n    kind: command\n    language: shell\n    content: tool run\n    description: Run it.\n---\n\nRun [[#run]], [[a/b/deep#two]], or [[json]]."),
            ("json.md", "---\ntitle: JSON\nsummary: J.\ntags:\n  - hosting\n---\n\nSee [[a/b]]."),
            ("d.md", "---\nextends: d\n---\n")
        ];

        // Act
        var extended = catalog.ExtendWith(extension, new Dictionary<string, string>(), new Dictionary<string, string> { ["tool"] = "CLI" });

        // Assert: the merged card
        var b = extended.Find("a/b")!;
        Assert.Equal("Bee CLI", b.Title);
        Assert.Equal("The b card.", b.Summary);
        Assert.Equal(["a/c", "d"], b.SeeAlsoIds);
        Assert.Equal(["run"], b.Examples.Select(e => e.Name));
        Assert.Equal("Back to the card `a`.\n\nRun the example `run`, the example `two` of the card `a/b/deep`, or the card `json`.", b.Markdown);
        Assert.Equal("D links the example `two` of the card `a/b/deep`.", extended.Find("d")!.Markdown);

        // Assert: the new root follows the existing ones, and the extended catalog did not change
        Assert.Equal(["a", "d", "json"], extended.Roots.Select(c => c.Id));
        Assert.Equal("See the card `a/b`.", extended.Find("json")!.Markdown);
        Assert.Equal("Bee", catalog.Find("a/b")!.Title);
        Assert.Empty(catalog.Find("a/b")!.Examples);
        Assert.Null(catalog.Find("json"));
    }

    /// <summary>
    ///     Test that an extension that does not fit the catalog fails to load with a message that says why.
    /// </summary>
    /// <param name="fileName">The extension file.</param>
    /// <param name="text">The text of the file.</param>
    /// <param name="message">A part of the message.</param>
    [Theory]
    [InlineData("x.md", "---\nextends: x\n---\n\nX.", "must name the existing card at the overlay's own path, 'x'")]
    [InlineData("a/c.md", "---\nextends: a/b\n---\n\nX.", "'extends: a/b' must name the existing card at the overlay's own path, 'a/c'")]
    [InlineData("a/e.md", "---\ntitle: E\nsummary: S.\ntags:\n  - design\n---\n\nE.", "is in the tree of the card 'a' of the catalog")]
    [InlineData("a.md", "---\ntitle: E\nsummary: S.\ntags:\n  - design\n---\n\nE.", "is in the tree of the card 'a' of the catalog")]
    [InlineData("a/c.md", "---\nextends: a/c\ntags:\n  - design\n---\n\nX.", "the key 'tags' is not allowed in an overlay")]
    [InlineData("a/c.md", "---\nextends: a/c\nchildren:\n  - z\n---\n\nX.", "the key 'children' is not allowed in an overlay")]
    [InlineData("a/c.md", "---\nextends: a/c\n---\n\nSee [[json]].", "line 5: the link '[[json]]' names no card 'json'")]
    [InlineData("a/c.md", "---\nextends: a/c\nexamples:\n  - name: one\n    kind: command\n    language: shell\n    content: x\n    description: X.\n---\n\nX.", "example name below 'a' 'one' is repeated")]
    [InlineData("a/c.md", "---\nextends: a/c\nsee-also:\n  - a/c\n---\n\nX.", "see-also entry 'a/c'")]
    [InlineData("tool.md", "---\ntitle: T\nsummary: S.\naliases:\n  - dee\ntags:\n  - hosting\n---\n\nT.", "help root card name 'dee' is repeated")]
    public void HelpCatalog_Extend_MisfitExtension_ThrowsWithTheReason(string fileName, string text, string message)
    {
        // Arrange
        var catalog = CreateTestCatalog();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => catalog.ExtendWith([(fileName, text)], new Dictionary<string, string>(), null));

        // Assert
        Assert.Contains(message, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that an extension cannot redefine a library token and that a filtered view cannot be extended.
    /// </summary>
    [Fact]
    public void HelpCatalog_Extend_TokenCollisionOrFilteredView_Throws()
    {
        // Arrange
        var catalog = CreateTestCatalog();
        var empty = new Dictionary<string, string>();

        // Act
        var collision = Assert.Throws<InvalidOperationException>(
            () => catalog.ExtendWith([], empty, new Dictionary<string, string> { ["maxDimension"] = "1" }));
        var filtered = Assert.Throws<InvalidOperationException>(
            () => catalog.Filter([HelpTags.Svg]).ExtendWith([], empty, null));

        // Assert
        Assert.Contains("'maxDimension'", collision.Message, StringComparison.Ordinal);
        Assert.Contains("filtered", filtered.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the public extension reads its cards from the embedded resources of an assembly (here, an assembly
    ///     without any, so the catalog is unchanged).
    /// </summary>
    [Fact]
    public void HelpCatalog_Extend_AssemblyWithoutCards_KeepsTheCards()
    {
        // Arrange
        var catalog = CreateTestCatalog();

        // Act
        var extended = catalog.Extend(typeof(HelpCatalogTests).Assembly, "No.Such.Prefix");

        // Assert
        Assert.Equal(catalog.Cards.Select(c => c.Id), extended.Cards.Select(c => c.Id));
    }

    /// <summary>
    ///     Test that the text of a card has the heading, the body, and the footers of what the view shows, with line
    ///     feeds only, no trailing newline, and lines of at most 80 columns.
    /// </summary>
    [Fact]
    public void HelpCatalog_RenderText_Card_IsPlainWrappedTextWithFooters()
    {
        // Arrange
        var catalog = CreateTestCatalog();

        // Act
        var text = catalog.RenderText(catalog.Find("a")!);

        // Assert
        Assert.StartsWith("# A\n\nThe a card.\nTags: svg, authoring\n\nSee the card `a/b`", text);
        Assert.Contains("\n## Cards\n\n- a/b\n  The b card.\n- a/c\n  The c card.\n", text, StringComparison.Ordinal);
        Assert.Contains("\n## Examples\n\n- one [source] one.svg: A drawing.\n", text, StringComparison.Ordinal);
        Assert.EndsWith("\n## See also\n\n- d\n  The d card.", text);
        Assert.DoesNotContain('\r', text);
        Assert.All(text.Split('\n'), line => Assert.True(line.Length <= 80, line));
        Assert.Equal(text, catalog.RenderText(catalog.Find("a")!));
        Assert.Contains("Also known as: dee", catalog.RenderText(catalog.Find("d")!), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the JSON of a card has every field, and only the cards the view shows.
    /// </summary>
    [Fact]
    public void HelpCatalog_RenderJson_Card_HasEveryField()
    {
        // Arrange
        var view = CreateTestCatalog().Filter(null, [HelpTags.Hosting]);

        // Act
        using var document = JsonDocument.Parse(view.RenderJson(view.Find("a")!));

        // Assert
        var root = document.RootElement;
        Assert.Equal("a", root.GetProperty("id").GetString());
        Assert.Equal("A", root.GetProperty("title").GetString());
        Assert.Equal("The a card.", root.GetProperty("summary").GetString());
        Assert.Equal(["svg", "authoring"], root.GetProperty("tags").EnumerateArray().Select(t => t.GetString()));
        Assert.Equal(0, root.GetProperty("aliases").GetArrayLength());
        Assert.StartsWith("See Bee, the card `d`", root.GetProperty("body").GetString());
        Assert.Equal(["a/b/deep", "a/c"], root.GetProperty("children").EnumerateArray().Select(c => c.GetProperty("id").GetString()));
        Assert.Equal("Deep", root.GetProperty("children")[0].GetProperty("title").GetString());
        Assert.Equal(["d"], root.GetProperty("seeAlso").EnumerateArray().Select(c => c.GetProperty("id").GetString()));
        var example = root.GetProperty("examples")[0];
        Assert.Equal("one", example.GetProperty("name").GetString());
        Assert.Equal("one.svg", example.GetProperty("fileName").GetString());
        Assert.Equal("<svg/>", example.GetProperty("content").GetString());
    }

    /// <summary>
    ///     Test that the index lists the visible cards, indented by their visible depth, as text and as JSON.
    /// </summary>
    [Fact]
    public void HelpCatalog_RenderIndex_ListsTheVisibleTree()
    {
        // Arrange
        var view = CreateTestCatalog().Filter(null, [HelpTags.Hosting]);

        // Act
        var text = view.RenderIndexText();
        using var json = JsonDocument.Parse(view.RenderIndexJson());

        // Assert: a/b is hidden, so a/b/deep is one level below a
        Assert.Equal(
            ["a           The a card.", "  a/b/deep  The deep card.", "  a/c       The c card.", "d           The d card."],
            text.Split('\n'));
        var cards = json.RootElement.GetProperty("cards");
        Assert.Equal(["a", "a/b/deep", "a/c", "d"], cards.EnumerateArray().Select(c => c.GetProperty("id").GetString()));
        Assert.Equal(["dee"], cards[3].GetProperty("aliases").EnumerateArray().Select(a => a.GetString()));
        Assert.Equal(["design"], cards[3].GetProperty("tags").EnumerateArray().Select(a => a.GetString()));
    }

    /// <summary>
    ///     Creates the test catalog.
    /// </summary>
    /// <returns>The catalog.</returns>
    private static HelpCatalog CreateTestCatalog()
    {
        return HelpCatalog.Create(TestCards(), TestExamples, TestRoots);
    }

    /// <summary>
    ///     Gets the card files of the test catalog: <c>a</c> (svg) with <c>a/b</c> (hosting), <c>a/b/deep</c> (design),
    ///     and <c>a/c</c>, and the general root <c>d</c> (design).
    /// </summary>
    /// <returns>The files.</returns>
    private static List<(string FileName, string Text)> TestCards()
    {
        return
        [
            ("a.md", string.Join(
                "\n",
                "---",
                "title: A",
                "summary: The a card.",
                "tags:",
                "  - svg",
                "  - authoring",
                "children:",
                "  - b",
                "  - c",
                "see-also:",
                "  - d",
                "examples:",
                "  - name: one",
                "    kind: source",
                "    language: xml",
                "    file: one.svg",
                "    description: A drawing.",
                "---",
                "",
                "See [[a/b]], [[d]], [[#one]],",
                "and [[#two]].",
                "",
                "Code: `[[a/b]]` and ``x [[nope]]``.",
                "",
                "```",
                "[[nope]]",
                "```",
                "",
                "Max {{{maxDimension}}}.")),
            ("a/b.md", "---\ntitle: Bee\nsummary: The b card.\ntags:\n  - svg\n  - hosting\nchildren:\n  - deep\n---\n\nBack to [[a]]."),
            ("a/b/deep.md", "---\ntitle: Deep\nsummary: The deep card.\ntags:\n  - svg\n  - design\nexamples:\n  - name: two\n    kind: snippet\n    language: xml\n    file: two.svg\n    description: Two.\n---\n\nDeep [[a/b#two]]."),
            ("a/c.md", "---\ntitle: C\nsummary: The c card.\ntags:\n  - svg\n  - authoring\n---\n\nC."),
            ("d.md", "---\ntitle: Dee\nsummary: The d card.\ntags:\n  - design\naliases:\n  - dee\nsee-also:\n  - a/b\n---\n\nD links [[a/b/deep#two]].")
        ];
    }
}
