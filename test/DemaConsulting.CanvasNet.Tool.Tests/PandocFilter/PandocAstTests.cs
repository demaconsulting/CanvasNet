using System.Text;
using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Tool.PandocFilter;

namespace DemaConsulting.CanvasNet.Tool.Tests.PandocFilter;

/// <summary>
///     Unit tests for the PandocAst unit: reading, walking, building, and writing Pandoc's JSON.
/// </summary>
public class PandocAstTests
{
    /// <summary>
    ///     A document written by <c>pandoc -t json</c> (Pandoc 3.12.1) from a title with an image, a figure whose image has
    ///     an image in its description, a pipe table with relative column widths, non-ASCII text, and a mermaid code block.
    /// </summary>
    private const string Sample =
        """{"pandoc-api-version":[1,23,1,2],"meta":{"title":{"t":"MetaInlines","c":[{"t":"Str","c":"Grüße"},{"t":"Space"},{"t":"Image","c":[["",[],[]],[{"t":"Str","c":"m"}],["meta.mmd",""]]}]}},"blocks":[{"t":"Figure","c":[["",[],[]],[null,[{"t":"Plain","c":[{"t":"Image","c":[["",[],[]],[{"t":"Str","c":"inner"}],["in.mmd",""]]},{"t":"Space"},{"t":"Str","c":"outer"}]}]],[{"t":"Plain","c":[{"t":"Image","c":[["",[],[["b","2"]]],[{"t":"Image","c":[["",[],[]],[{"t":"Str","c":"inner"}],["in.mmd",""]]},{"t":"Space"},{"t":"Str","c":"outer"}],["out.mmd",""]]}]}]]},{"t":"Table","c":[["",[],[]],[null,[]],[[{"t":"AlignDefault"},{"t":"ColWidth","c":0.42857142857142855}],[{"t":"AlignRight"},{"t":"ColWidth","c":0.5714285714285714}]],[["",[],[]],[[["",[],[]],[[["",[],[]],{"t":"AlignDefault"},1,1,[{"t":"Plain","c":[{"t":"Str","c":"Name"}]}]],[["",[],[]],{"t":"AlignDefault"},1,1,[{"t":"Plain","c":[{"t":"Str","c":"Wide"}]}]]]]]],[[["",[],[]],0,[],[[["",[],[]],[[["",[],[]],{"t":"AlignDefault"},1,1,[{"t":"Plain","c":[{"t":"Str","c":"π"},{"t":"Space"},{"t":"Str","c":"—"},{"t":"Space"},{"t":"Str","c":"“q”"}]}]],[["",[],[]],{"t":"AlignDefault"},1,1,[{"t":"Plain","c":[{"t":"Str","c":"1.50"}]}]]]]]]],[["",[],[]],[]]]},{"t":"CodeBlock","c":[["id1",["mermaid"],[]],"graph"]}]}""";

    /// <summary>
    ///     Test that the images are found as Pandoc's Lua filter visits them: the metadata first, then the blocks, an image
    ///     in the description of another before it, and the code blocks separately.
    /// </summary>
    [Fact]
    public void PandocAst_FindAll_RealDocument_MetadataFirstAndChildrenBeforeParents()
    {
        // Arrange: the sample document
        var document = PandocAst.Parse(Encoding.UTF8.GetBytes(Sample), null);

        // Act: find the images and the code blocks
        var images = PandocAst.FindAll(document, "Image").Select(n => PandocAst.ReadImage(n).Src).ToList();
        var blocks = PandocAst.FindAll(document, "CodeBlock").Select(n => PandocAst.ReadCodeBlock(n).Attr.Id).ToList();

        // Assert: the order a probe of Pandoc 3.12.1 printed for the same document
        Assert.Equal(["meta.mmd", "in.mmd", "in.mmd", "out.mmd"], images);
        Assert.Equal(["id1"], blocks);
    }

    /// <summary>
    ///     Test that a document is written back byte for byte: unknown elements, the column widths, and non-ASCII text.
    /// </summary>
    [Fact]
    public void PandocAst_Write_UnchangedRealDocument_IsByteIdentical()
    {
        // Arrange: the sample's bytes
        var bytes = Encoding.UTF8.GetBytes(Sample);

        // Act: read and write it
        var written = PandocAst.Write(PandocAst.Parse(bytes, null));

        // Assert: the same bytes, without a byte order mark
        Assert.Equal(bytes, written);
    }

    /// <summary>
    ///     Test that a document nested far beyond the default JSON depth of 64 is read, walked, and written.
    /// </summary>
    [Fact]
    public void PandocAst_Parse_NestingDeeperThan64_RoundTrips()
    {
        // Arrange: an image inside 300 block quotes
        var nested = PandocDoc.Para(PandocDoc.Image("deep.mmd"));
        for (var i = 0; i < 300; i++)
        {
            nested = $$"""{"t":"BlockQuote","c":[{{nested}}]}""";
        }

        var bytes = PandocDoc.Document(nested);

        // Act: read, find, and write
        var document = PandocAst.Parse(bytes, null);
        var images = PandocAst.FindAll(document, "Image");

        // Assert: the image is found and the document is written unchanged
        Assert.Equal("deep.mmd", PandocAst.ReadImage(Assert.Single(images)).Src);
        Assert.Equal(bytes, PandocAst.Write(document));
    }

    /// <summary>
    ///     Test the API version check: 1.22 (Pandoc 2) and another major version are corrupt input naming Pandoc 3, and
    ///     1.23 and a later minor version are accepted.
    /// </summary>
    /// <param name="version">The API version.</param>
    /// <param name="accepted">Whether the version is accepted.</param>
    [Theory]
    [InlineData("1,22,1", false)]
    [InlineData("2,0", false)]
    [InlineData("1,23,1,2", true)]
    [InlineData("1,24", true)]
    public void PandocAst_Parse_ApiVersion_AcceptsPandoc3Only(string version, bool accepted)
    {
        // Arrange: an empty document of the version
        var bytes = Encoding.UTF8.GetBytes($$"""{"pandoc-api-version":[{{version}}],"meta":{},"blocks":[]}""");

        // Act / Assert: accepted, or corrupt input with the Pandoc version and the hint
        if (accepted)
        {
            Assert.NotNull(PandocAst.Parse(bytes, "3.12.1"));
            return;
        }

        var error = Assert.Throws<ToolException>(() => PandocAst.Parse(bytes, "2.19"));
        Assert.Equal(ErrorCodes.CorruptInput, error.Code);
        Assert.Contains("(Pandoc 2.19)", error.Message);
        Assert.Equal("This filter needs Pandoc 3 or later.", error.Hint);
    }

    /// <summary>
    ///     Test that input that is not a Pandoc document, and an image of another shape, are corrupt input.
    /// </summary>
    [Fact]
    public void PandocAst_ReadAndParse_MalformedInput_ThrowsCorruptInput()
    {
        // Arrange: text that is not JSON, and an image whose target is not a pair of strings
        var notJson = Encoding.UTF8.GetBytes("not json");
        var document = PandocAst.Parse(PandocDoc.Document("""{"t":"Para","c":[{"t":"Image","c":[["",[],[]],[],[1]]}]}"""), null);

        // Act
        var parse = Assert.Throws<ToolException>(() => PandocAst.Parse(notJson, null));
        var read = Assert.Throws<ToolException>(() => PandocAst.ReadImage(PandocAst.FindAll(document, "Image")[0]));

        // Assert: both are corrupt input; the second names the element
        Assert.Equal(ErrorCodes.CorruptInput, parse.Code);
        Assert.Equal(ErrorCodes.CorruptInput, read.Code);
        Assert.Equal("The document has a malformed Image element.", read.Message);
    }

    /// <summary>
    ///     Test the exact JSON of the built elements and of the warnings in the metadata.
    /// </summary>
    [Fact]
    public void PandocAst_Builders_FigureParaSpanAndWarnings_HaveTheExactJson()
    {
        // Arrange: a document to receive the warnings
        var document = PandocAst.Parse(PandocDoc.Document(), null);

        // Act: build a figure, a paragraph, and set two warnings
        var image = PandocAst.Image(new PandocAttr("", ["wide"], [new("width", "50%")]), [PandocAst.MarkdownPlaceholder("A *b*")], "x.png");
        var figure = PandocAst.Figure("fig", [PandocAst.Plain(PandocAst.MarkdownPlaceholder("A *b*"))], [PandocAst.Plain(image)]);
        var para = PandocAst.Para(PandocAst.Image(new PandocAttr("id", [], []), [], "y.png"));
        PandocAst.SetWarnings(document, ["a: one", "b: two"]);

        // Assert: the shapes Pandoc reads
        const string span = """{"t":"Span","c":[["",["canvasnet-markdown"],[["markdown","A *b*"]]],[]]}""";
        Assert.Equal(
            $$"""{"t":"Figure","c":[["fig",[],[]],[null,[{"t":"Plain","c":[{{span}}]}]],[{"t":"Plain","c":[{"t":"Image","c":[["",["wide"],[["width","50%"]]],[{{span}}],["x.png",""]]}]}]]}""",
            figure.ToJsonString());
        Assert.Equal("""{"t":"Para","c":[{"t":"Image","c":[["id",[],[]],[],["y.png",""]]}]}""", para.ToJsonString());
        Assert.Equal(
            """{"t":"MetaList","c":[{"t":"MetaString","c":"a: one"},{"t":"MetaString","c":"b: two"}]}""",
            document["meta"]![PandocAst.WarningsKey]!.ToJsonString());
    }

    /// <summary>
    ///     Test that rewriting an image changes only its source and its key-value attributes.
    /// </summary>
    [Fact]
    public void PandocAst_RewriteImage_ChangesOnlySourceAndAttributes()
    {
        // Arrange: an image with an id, a class, attributes, a description, and a title
        var node = (JsonObject)JsonNode.Parse("""{"t":"Image","c":[["i",["c"],[["view","top"],["width","5"]]],[{"t":"Str","c":"Grüße"}],["a.scad","t"]]}""")!;

        // Act: point it at a picture, keeping one attribute
        PandocAst.RewriteImage(node, "gen/a.png", [new("width", "5")]);

        // Assert: everything else is as it was
        Assert.Equal("""{"t":"Image","c":[["i",["c"],[["width","5"]]],[{"t":"Str","c":"Grüße"}],["gen/a.png","t"]]}""", node.ToJsonString(new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    }

    /// <summary>
    ///     Test that an image in running text is told from an image on its own, on a document written by
    ///     <c>pandoc -t json</c> (Pandoc 3.14): a sentence, a link in a sentence, a heading, two images on one line, and
    ///     quotation marks are running text; a figure, a lone image with no description, a lone emphasized image, and a
    ///     lone linked image are not.
    /// </summary>
    [Fact]
    public void PandocAst_IsInRunningText_PandocDocument_TellsInlineFromDisplay()
    {
        // Arrange: the JSON of "Area ![s](s.mml) of a circle.", "![f](f.mml)", "![](p.mml)", "*![e](e.mml)*",
        // "See [![l](l.mml)](https://x) here.", "[![k](k.mml)](https://x)", "# Head ![h](h.mml)",
        // "![t1](t1.mml) ![t2](t2.mml)", and "\"![q](q.mml)\"", each a paragraph of its own
        const string json =
            """{"pandoc-api-version":[1,23,1,2],"meta":{},"blocks":[{"t":"Para","c":[{"t":"Str","c":"Area"},{"t":"Space"},{"t":"Image","c":[["",[],[]],[{"t":"Str","c":"s"}],["s.mml",""]]},{"t":"Space"},{"t":"Str","c":"of"},{"t":"Space"},{"t":"Str","c":"a"},{"t":"Space"},{"t":"Str","c":"circle."}]},{"t":"Figure","c":[["",[],[]],[null,[{"t":"Plain","c":[{"t":"Str","c":"f"}]}]],[{"t":"Plain","c":[{"t":"Image","c":[["",[],[]],[{"t":"Str","c":"f"}],["f.mml",""]]}]}]]},{"t":"Para","c":[{"t":"Image","c":[["",[],[]],[],["p.mml",""]]}]},{"t":"Para","c":[{"t":"Emph","c":[{"t":"Image","c":[["",[],[]],[{"t":"Str","c":"e"}],["e.mml",""]]}]}]},{"t":"Para","c":[{"t":"Str","c":"See"},{"t":"Space"},{"t":"Link","c":[["",[],[]],[{"t":"Image","c":[["",[],[]],[{"t":"Str","c":"l"}],["l.mml",""]]}],["https://x",""]]},{"t":"Space"},{"t":"Str","c":"here."}]},{"t":"Para","c":[{"t":"Link","c":[["",[],[]],[{"t":"Image","c":[["",[],[]],[{"t":"Str","c":"k"}],["k.mml",""]]}],["https://x",""]]}]},{"t":"Header","c":[1,["head-h",[],[]],[{"t":"Str","c":"Head"},{"t":"Space"},{"t":"Image","c":[["",[],[]],[{"t":"Str","c":"h"}],["h.mml",""]]}]]},{"t":"Para","c":[{"t":"Image","c":[["",[],[]],[{"t":"Str","c":"t1"}],["t1.mml",""]]},{"t":"Space"},{"t":"Image","c":[["",[],[]],[{"t":"Str","c":"t2"}],["t2.mml",""]]}]},{"t":"Para","c":[{"t":"Quoted","c":[{"t":"DoubleQuote"},[{"t":"Image","c":[["",[],[]],[{"t":"Str","c":"q"}],["q.mml",""]]}]]}]}]}""";
        var document = PandocAst.Parse(Encoding.UTF8.GetBytes(json), null);

        // Act: check every image
        var inRunningText = PandocAst.FindAll(document, "Image").ToDictionary(n => PandocAst.ReadImage(n).Src, PandocAst.IsInRunningText);

        // Assert: running text, and pictures on their own
        Assert.Equal(
            new Dictionary<string, bool>
            {
                ["s.mml"] = true,
                ["f.mml"] = false,
                ["p.mml"] = false,
                ["e.mml"] = false,
                ["l.mml"] = true,
                ["k.mml"] = false,
                ["h.mml"] = true,
                ["t1.mml"] = true,
                ["t2.mml"] = true,
                ["q.mml"] = true
            },
            inRunningText);
    }

    /// <summary>
    ///     Test that an image alone in a heading, a table cell, another image's description, or a list item is on its own,
    ///     while an image beside text in a cell or in a sentence is in running text, on a document written by
    ///     <c>pandoc -t json</c> (API 1.23.1.2).
    /// </summary>
    [Fact]
    public void PandocAst_IsInRunningText_PandocDocument_LoneImageInHeadingCellDescriptionOrListItemIsOnItsOwn()
    {
        // Arrange: the JSON of "# ![h2](h2.mml)", a table "| A | B |" with the row "| ![c](c.mml) | x ![d](d.mml) |",
        // "See ![![in](in.mml)](o.mml) here.", and the list "- ![](li.mml)" and "- b"
        const string json =
            """{"pandoc-api-version":[1,23,1,2],"meta":{},"blocks":[{"t":"Header","c":[1,["h2",[],[]],[{"t":"Image","c":[["",[],[]],[{"t":"Str","c":"h2"}],["h2.mml",""]]}]]},{"t":"Table","c":[["",[],[]],[null,[]],[[{"t":"AlignDefault"},{"t":"ColWidthDefault"}],[{"t":"AlignDefault"},{"t":"ColWidthDefault"}]],[["",[],[]],[[["",[],[]],[[["",[],[]],{"t":"AlignDefault"},1,1,[{"t":"Plain","c":[{"t":"Str","c":"A"}]}]],[["",[],[]],{"t":"AlignDefault"},1,1,[{"t":"Plain","c":[{"t":"Str","c":"B"}]}]]]]]],[[["",[],[]],0,[],[[["",[],[]],[[["",[],[]],{"t":"AlignDefault"},1,1,[{"t":"Plain","c":[{"t":"Image","c":[["",[],[]],[{"t":"Str","c":"c"}],["c.mml",""]]}]}]],[["",[],[]],{"t":"AlignDefault"},1,1,[{"t":"Plain","c":[{"t":"Str","c":"x"},{"t":"Space"},{"t":"Image","c":[["",[],[]],[{"t":"Str","c":"d"}],["d.mml",""]]}]}]]]]]]],[["",[],[]],[]]]},{"t":"Para","c":[{"t":"Str","c":"See"},{"t":"Space"},{"t":"Image","c":[["",[],[]],[{"t":"Image","c":[["",[],[]],[{"t":"Str","c":"in"}],["in.mml",""]]}],["o.mml",""]]},{"t":"Space"},{"t":"Str","c":"here."}]},{"t":"BulletList","c":[[{"t":"Plain","c":[{"t":"Image","c":[["",[],[]],[],["li.mml",""]]}]}],[{"t":"Plain","c":[{"t":"Str","c":"b"}]}]]}]}""";
        var document = PandocAst.Parse(Encoding.UTF8.GetBytes(json), null);

        // Act: check every image
        var inRunningText = PandocAst.FindAll(document, "Image").ToDictionary(n => PandocAst.ReadImage(n).Src, PandocAst.IsInRunningText);

        // Assert: alone in its owner is on its own, beside text is running text
        Assert.Equal(
            new Dictionary<string, bool>
            {
                ["h2.mml"] = false,
                ["c.mml"] = false,
                ["d.mml"] = true,
                ["o.mml"] = true,
                ["in.mml"] = false,
                ["li.mml"] = false
            },
            inRunningText);
    }
}
