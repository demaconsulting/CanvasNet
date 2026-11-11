// cspell:ignore Emmd emmd
using System.Text;
using System.Text.RegularExpressions;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Viewer.Tests.Imaging;

/// <summary>
///     Unit tests for the SvgFlattener unit: writing a copy of an SVG drawing whose pictures that only CanvasNet draws
///     are PNG pictures, with every other byte kept.
/// </summary>
public class SvgFlattenerTests
{
    /// <summary>The pattern of a PNG data URI in the copy.</summary>
    private static readonly Regex DataUri = new("data:image/png;base64,[A-Za-z0-9+/=]+", RegexOptions.None, TimeSpan.FromSeconds(5));

    /// <summary>
    ///     Writes an SVG drawing of 100 by 100 units with the given markup.
    /// </summary>
    /// <param name="directory">The folder.</param>
    /// <param name="name">The file name.</param>
    /// <param name="body">The markup inside the root element.</param>
    /// <returns>The path of the file.</returns>
    private static string WriteSvg(TempDirectory directory, string name, string body)
    {
        return FixtureFactory.WriteText(
            directory.Path,
            name,
            "<svg xmlns='http://www.w3.org/2000/svg' xmlns:xlink='http://www.w3.org/1999/xlink' viewBox='0 0 100 100' width='100' height='100'>"
            + $"<rect width='100' height='100' fill='#ff0000'/>{body}</svg>");
    }

    /// <summary>
    ///     Copies the Mermaid diagram of the fixtures into a folder.
    /// </summary>
    /// <param name="directory">The folder.</param>
    /// <param name="name">The file name of the copy.</param>
    private static void CopyDiagram(TempDirectory directory, string name = "flow.mmd")
    {
        File.Copy(FixtureFactory.FixturePath("mermaid/flow.mmd"), directory.Combine(name));
    }

    /// <summary>
    ///     Writes a small PNG file that is one solid color.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    private static void WritePng(string path)
    {
        using var surface = new Surface(4, 4);
        surface.Clear(new Rgba32(0, 128, 0, 255));
        PngCodec.Save(surface, path);
    }

    /// <summary>
    ///     Flattens a drawing.
    /// </summary>
    /// <param name="input">The path of the drawing.</param>
    /// <param name="output">The path of the copy.</param>
    /// <param name="scale">The scale.</param>
    /// <param name="strict"><see langword="true"/> for the strict policy.</param>
    /// <returns>The result.</returns>
    private static FlattenResult Flatten(string input, string output, double scale = 1, bool strict = false)
    {
        return SvgFlattener.Execute(SourceLoader.Probe(input), new FlattenRequest(output) { Scale = scale, Strict = strict });
    }

    /// <summary>
    ///     Gets the size of each PNG data URI of a copy, in order.
    /// </summary>
    /// <param name="text">The text of the copy.</param>
    /// <returns>The width and height of each picture.</returns>
    private static List<(int Width, int Height)> PictureSizes(string text)
    {
        return [.. DataUri.Matches(text).Select(match =>
        {
            var bytes = Convert.FromBase64String(match.Value[SvgFlattener.DataUriPrefix.Length..]);
            using var stream = new MemoryStream(bytes);
            var info = PngCodec.GetInfo(stream);
            return (info.Width, info.Height);
        })];
    }

    /// <summary>
    ///     Test that a linked Mermaid diagram is replaced by a PNG picture of its placement size.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_MermaidImage_ReplacedWithPngDataUri()
    {
        // Arrange: a 60 by 40 placement of the diagram
        using var directory = new TempDirectory();
        CopyDiagram(directory);
        var input = WriteSvg(directory, "dashboard.svg", "<image href='flow.mmd' x='10' y='10' width='60' height='40'/>");
        var output = directory.Combine("flat.svg");

        // Act
        var result = Flatten(input, output);

        // Assert: one picture of the placement size replaces the link, and the copy was written
        Assert.True(result.Flattened);
        Assert.Equal(output, result.Output);
        var replaced = Assert.Single(result.Replaced);
        Assert.Equal(("flow.mmd", "mermaid", 1), (replaced.Href, replaced.Format, replaced.Elements));
        Assert.Equal((60, 40), (replaced.Width, replaced.Height));
        var text = File.ReadAllText(output);
        Assert.Equal([(60, 40)], PictureSizes(text));
        Assert.DoesNotContain("flow.mmd", text, StringComparison.Ordinal);
        Assert.Equal(new FileInfo(output).Length, result.Bytes);
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>
    ///     Test that a link is classified as the resolver reads it, percent-decoded: <c>flow%2Emmd</c> is the Mermaid
    ///     diagram flow.mmd, so it is replaced; hidden, it is reported as not drawn.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_PercentEncodedExtension_ClassifiedAsResolverReadsIt()
    {
        // Arrange: one shown and one hidden link whose dot is percent-encoded
        using var directory = new TempDirectory();
        CopyDiagram(directory);
        var input = WriteSvg(
            directory,
            "a.svg",
            "<image href='flow%2Emmd' width='60' height='40'/><defs><image id='h' href='flow%2emmd#theme=dark' width='9' height='9'/></defs>");
        var output = directory.Combine("b.svg");

        // Act
        var result = Flatten(input, output);

        // Assert
        var replaced = Assert.Single(result.Replaced);
        Assert.Equal(("flow%2Emmd", "mermaid", 60, 40), (replaced.Href, replaced.Format, replaced.Width, replaced.Height));
        Assert.DoesNotContain("href='flow%2Emmd'", File.ReadAllText(output), StringComparison.Ordinal);
        var note = Assert.Single(result.Diagnostics);
        Assert.Equal(SvgFlattener.ImageNotDrawnCode, note.Code);
        Assert.Contains("flow%2emmd#theme=dark", note.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that only the value of the link changes: comments, character data, unknown elements, the spacing around
    ///     the equals sign, and the quotes stay as they were.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_ReplacedLink_OnlyAttributeValueChanges()
    {
        // Arrange: markup that a rewrite of the document tree would change
        using var directory = new TempDirectory();
        CopyDiagram(directory);
        const string before = "<svg xmlns=\"http://www.w3.org/2000/svg\"   viewBox=\"0 0 100 100\" width=\"100\" height=\"100\">\n"
                              + "  <!-- <image href=\"flow.mmd\"/> stays a comment -->\n"
                              + "  <foo:bar xmlns:foo=\"urn:x\"  a = \"1\" />\n"
                              + "  <style><![CDATA[ rect { fill: red } ]]></style>\n"
                              + "  <image   width=\"50\" height=\"50\"  href\t=\n '";
        const string after = "' />\n</svg>\n";
        var input = FixtureFactory.WriteText(directory.Path, "a.svg", before + "flow.mmd" + after);
        var output = directory.Combine("b.svg");

        // Act
        Flatten(input, output);

        // Assert: the copy is the input with only the value of the link changed
        var text = File.ReadAllText(output);
        var uri = DataUri.Match(text).Value;
        Assert.Equal(before + uri + after, text);
    }

    /// <summary>
    ///     Test that a byte order mark and every kind of line break are kept byte for byte.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_BomAndLineEndings_Preserved()
    {
        // Arrange: a byte order mark, CRLF, and lone CR line breaks
        using var directory = new TempDirectory();
        CopyDiagram(directory);
        var text = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100' width='100' height='100'>\r\n"
                   + "<rect width='10' height='10'/>\r<image href='flow.mmd' width='40' height='40'/>\r\n</svg>";
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(text)];
        var input = directory.Combine("a.svg");
        File.WriteAllBytes(input, bytes);
        var output = directory.Combine("b.svg");

        // Act
        Flatten(input, output);

        // Assert: putting the link back gives the input, byte for byte
        var copy = File.ReadAllBytes(output);
        Assert.Equal(bytes[..3], copy[..3]);
        var restored = DataUri.Replace(Encoding.UTF8.GetString(copy, 3, copy.Length - 3), "flow.mmd");
        Assert.Equal(bytes, [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(restored)]);
    }

    /// <summary>
    ///     Test that the picture is drawn at the size of its placement through the viewBox and the transforms, times the
    ///     scale.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_ViewBoxTransformAndScale_RenderAtPlacementSize()
    {
        // Arrange: a viewBox of 100 by 50 (the natural size, as render reads it) shown at 200 by 100, and a 20 by 10
        // placement in a group scaled by 2
        using var directory = new TempDirectory();
        CopyDiagram(directory);
        var input = FixtureFactory.WriteText(
            directory.Path,
            "a.svg",
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 50' width='200' height='100'>"
            + "<g transform='scale(2)'><image href='flow.mmd' width='20' height='10'/></g></svg>");

        // Act: 20 x 2 x 1.5 = 60 by 30 on the canvas of 150 by 75 the viewBox is mapped to
        var result = Flatten(input, directory.Combine("b.svg"), scale: 1.5);

        // Assert
        var replaced = Assert.Single(result.Replaced);
        Assert.Equal((60, 30), (replaced.Width, replaced.Height));
    }

    /// <summary>
    ///     Test that a drawing larger than the detection canvas is drawn smaller to find the placements, and the pictures
    ///     still get the full placement size.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_LargeDrawing_PictureKeepsFullPlacementSize()
    {
        // Arrange: a drawing of 4000 by 4000 pixels, twice the side of the detection canvas
        using var directory = new TempDirectory();
        CopyDiagram(directory);
        var input = FixtureFactory.WriteText(
            directory.Path,
            "a.svg",
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 4000 4000'><image href='flow.mmd' width='1000' height='500'/></svg>");

        // Act
        var result = Flatten(input, directory.Combine("b.svg"));

        // Assert
        var replaced = Assert.Single(result.Replaced);
        Assert.Equal((1000, 500), (replaced.Width, replaced.Height));
        Assert.Equal((2048, 2048, 4000 / 2048d), SvgFlattener.DetectionCanvas(4000, 4000, 1));
    }

    /// <summary>
    ///     Test that a rotated placement is drawn at the length of its edges, not at its bounding box.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_RotatedPlacement_UsesEdgeLengths()
    {
        // Arrange: a 40 by 20 placement turned by 90 degrees
        using var directory = new TempDirectory();
        CopyDiagram(directory);
        var input = WriteSvg(directory, "a.svg", "<image href='flow.mmd' width='40' height='20' transform='translate(50,0) rotate(90)'/>");

        // Act
        var result = Flatten(input, directory.Combine("b.svg"));

        // Assert
        var replaced = Assert.Single(result.Replaced);
        Assert.Equal((40, 20), (replaced.Width, replaced.Height));
    }

    /// <summary>
    ///     Test that a drawing that links only a raster and a plain SVG file has nothing to flatten and nothing is written.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_PlainRasterAndPlainSvg_NothingWritten()
    {
        // Arrange: a raster and an SVG file that itself links only the raster
        using var directory = new TempDirectory();
        WritePng(directory.Combine("logo.png"));
        WriteSvg(directory, "plain.svg", "<image href='logo.png' width='10' height='10'/>");
        var input = WriteSvg(directory, "a.svg", "<image href='logo.png' width='20' height='20'/><image href='plain.svg' x='50' width='40' height='40'/>");
        var output = directory.Combine("b.svg");

        // Act
        var result = Flatten(input, output);

        // Assert
        Assert.False(result.Flattened);
        Assert.Null(result.Output);
        Assert.Equal(0, result.Bytes);
        Assert.Empty(result.Replaced);
        Assert.Empty(result.Diagnostics);
        Assert.False(File.Exists(output));
    }

    /// <summary>
    ///     Test that data URIs and same-document links of the input are never followed or changed.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_DataUriAndFragmentLinks_LeftUntouched()
    {
        // Arrange: a PNG data URI, a fragment link, and a diagram
        using var directory = new TempDirectory();
        CopyDiagram(directory);
        using var stream = new MemoryStream();
        using (var surface = new Surface(2, 2))
        {
            PngCodec.Save(surface, stream);
        }

        var data = SvgFlattener.DataUriPrefix + Convert.ToBase64String(stream.ToArray());
        var input = WriteSvg(
            directory,
            "a.svg",
            $"<image href='{data}' width='10' height='10'/><image href='#mark' width='10' height='10'/><image href='flow.mmd' x='50' width='40' height='40'/>");
        var output = directory.Combine("b.svg");

        // Act
        var result = Flatten(input, output);

        // Assert: both links are still in the copy, and only the diagram was replaced
        var text = File.ReadAllText(output);
        Assert.Contains($"href='{data}'", text, StringComparison.Ordinal);
        Assert.Contains("href='#mark'", text, StringComparison.Ordinal);
        Assert.Equal("flow.mmd", Assert.Single(result.Replaced).Href);
    }

    /// <summary>
    ///     Test that a nested SVG file that links a diagram is replaced as a whole.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_NestedSvgLinkingDiagram_ReplacedAsWhole()
    {
        // Arrange
        using var directory = new TempDirectory();
        CopyDiagram(directory);
        WriteSvg(directory, "inner.svg", "<image href='flow.mmd' width='50' height='50'/>");
        var input = WriteSvg(directory, "a.svg", "<image href='inner.svg' width='80' height='80'/>");

        // Act
        var result = Flatten(input, directory.Combine("b.svg"));

        // Assert
        var replaced = Assert.Single(result.Replaced);
        Assert.Equal(("inner.svg", "svg", 80, 80), (replaced.Href, replaced.Format, replaced.Width, replaced.Height));
    }

    /// <summary>
    ///     Test that a diagram linked two SVG files deep makes the outermost nested file be replaced.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_DeeplyNestedComposite_ReplacedAtTopLevel()
    {
        // Arrange: a.svg links mid.svg, which links inner.svg, which links the diagram
        using var directory = new TempDirectory();
        CopyDiagram(directory);
        WriteSvg(directory, "inner.svg", "<image href='flow.mmd' width='50' height='50'/>");
        WriteSvg(directory, "mid.svg", "<image href='inner.svg' width='50' height='50'/>");
        var input = WriteSvg(directory, "a.svg", "<image href='mid.svg' width='60' height='60'/>");

        // Act
        var result = Flatten(input, directory.Combine("b.svg"));

        // Assert
        Assert.Equal("mid.svg", Assert.Single(result.Replaced).Href);
    }

    /// <summary>
    ///     Test that an <c>xlink:href</c> with a fragment is drawn with the fragment and reported as written.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_XlinkHrefWithFragment_FragmentApplied()
    {
        // Arrange: page 1 of a document, linked with the legacy attribute
        using var directory = new TempDirectory();
        File.WriteAllBytes(directory.Combine("doc.pdf"), FixtureFactory.CreatePdf());
        var input = WriteSvg(directory, "a.svg", "<image xlink:href='doc.pdf#page=1' width='50' height='70'/>");
        var output = directory.Combine("b.svg");

        // Act
        var result = Flatten(input, output);

        // Assert
        var replaced = Assert.Single(result.Replaced);
        Assert.Equal(("doc.pdf#page=1", "pdf"), (replaced.Href, replaced.Format));
        Assert.Contains("xlink:href='data:image/png;base64,", File.ReadAllText(output), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that only the attribute the SVG library reads is rewritten when an element has both forms.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_HrefAndXlinkHref_OnlyEffectiveAttributeRewritten()
    {
        // Arrange: href wins over xlink:href
        using var directory = new TempDirectory();
        CopyDiagram(directory);
        var input = WriteSvg(directory, "a.svg", "<image xlink:href='other.mmd' href='flow.mmd' width='40' height='40'/>");
        var output = directory.Combine("b.svg");

        // Act
        Flatten(input, output);

        // Assert
        var text = File.ReadAllText(output);
        Assert.Contains("xlink:href='other.mmd'", text, StringComparison.Ordinal);
        Assert.Contains(" href='data:image/png;base64,", text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a link used twice is drawn once at the larger placement and both elements are rewritten.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_SameHrefTwice_BothReplacedAtLargestSize()
    {
        // Arrange
        using var directory = new TempDirectory();
        CopyDiagram(directory);
        var input = WriteSvg(directory, "a.svg", "<image href='flow.mmd' width='20' height='10'/><image href='flow.mmd' y='50' width='80' height='40'/>");
        var output = directory.Combine("b.svg");

        // Act
        var result = Flatten(input, output);

        // Assert
        var replaced = Assert.Single(result.Replaced);
        Assert.Equal((2, 80, 40), (replaced.Elements, replaced.Width, replaced.Height));
        Assert.Equal([(80, 40), (80, 40)], PictureSizes(File.ReadAllText(output)));
    }

    /// <summary>
    ///     Test that a link shown small first and large later counts once against the limit of 64 pictures: with 63
    ///     other diagrams between its two placements, every link is replaced and it is drawn at the larger size.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_SameHrefSmallThenLarge_ChargesImageCountOnce()
    {
        // Arrange: the repeated diagram small, 63 other diagrams, then the repeated diagram large: 64 different links
        using var directory = new TempDirectory();
        CopyDiagram(directory);
        var body = new StringBuilder("<image href='flow.mmd' width='10' height='10'/>");
        for (var i = 0; i < 63; i++)
        {
            FixtureFactory.WriteText(directory.Path, $"d{i}.mmd", "flowchart LR\n  a --> b\n");
            body.Append($"<image href='d{i}.mmd' width='8' height='8'/>");
        }

        body.Append("<image href='flow.mmd' width='60' height='60'/>");
        var input = WriteSvg(directory, "a.svg", body.ToString());
        var output = directory.Combine("b.svg");

        // Act
        var result = Flatten(input, output);

        // Assert: nothing is over the limit, and the repeated link is drawn once at its larger placement
        Assert.Equal(64, result.Replaced.Count);
        Assert.Empty(result.Diagnostics);
        var repeated = Assert.Single(result.Replaced, r => r.Href == "flow.mmd");
        Assert.Equal((2, 60, 60), (repeated.Elements, repeated.Width, repeated.Height));
        Assert.DoesNotContain(".mmd'", File.ReadAllText(output), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a link shown small first and large later is drawn only at its larger size, so a pixel budget that
    ///     holds the large picture but not the small and the large one together still draws it at the larger size.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_SameHrefSmallThenLarge_ChargesPixelBudgetOnce()
    {
        // Arrange: a 2048-unit drawing at scale 4, so the placements are 5700 and 5800 pixels square; 5800 squared fits
        // the budget of 64,000,000 pixels, but 5700 squared plus 5800 squared does not
        using var directory = new TempDirectory();
        CopyDiagram(directory);
        var input = FixtureFactory.WriteText(
            directory.Path,
            "a.svg",
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 2048 2048' width='2048' height='2048'>"
            + "<image href='flow.mmd' width='1425' height='1425'/>"
            + "<image href='flow.mmd' y='100' width='1450' height='1450'/></svg>");
        var output = directory.Combine("b.svg");

        // Act
        var result = Flatten(input, output, scale: 4);

        // Assert: one picture at the larger placement, and nothing refused for the budget
        Assert.DoesNotContain(result.Diagnostics, d => d.Code == FolderImageResolver.TooLargeCode);
        var replaced = Assert.Single(result.Replaced);
        Assert.Equal((2, 5800, 5800), (replaced.Elements, replaced.Width, replaced.Height));
    }

    /// <summary>
    ///     Test that a diagram the drawing never shows is reported with its position and left as a link.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_HiddenDiagram_ReportsNotDrawnAndWritesNothing()
    {
        // Arrange: the only diagram is an unused definition
        using var directory = new TempDirectory();
        CopyDiagram(directory);
        var input = WriteSvg(directory, "a.svg", "<defs><image id='x' href='flow.mmd' width='40' height='40'/></defs>");
        var output = directory.Combine("b.svg");

        // Act
        var result = Flatten(input, output);

        // Assert
        Assert.False(result.Flattened);
        var note = Assert.Single(result.Diagnostics);
        Assert.Equal((SvgFlattener.ImageNotDrawnCode, "warning", "svg"), (note.Code, note.Severity, note.Source));
        Assert.Equal("a.svg", note.Location?.File);
        Assert.Equal(1, note.Location?.Line);
        Assert.False(File.Exists(output));
    }

    /// <summary>
    ///     Test that a diagram that a shown nested SVG file defines but never shows is reported with the nested file as
    ///     its location and origin.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_HiddenDiagramInNestedSvg_ReportsNotDrawnWithOrigin()
    {
        // Arrange: the drawing shows parts/inner.svg, whose only diagram is an unused definition
        using var directory = new TempDirectory();
        Directory.CreateDirectory(directory.Combine("parts"));
        File.Copy(FixtureFactory.FixturePath("mermaid/flow.mmd"), directory.Combine("parts", "flow.mmd"));
        FixtureFactory.WriteText(
            directory.Path,
            "parts/inner.svg",
            "<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'>\n"
            + "<defs><image id='x' href='flow.mmd' width='40' height='40'/></defs><rect width='10' height='10'/></svg>");
        var input = WriteSvg(directory, "a.svg", "<image href='parts/inner.svg' width='100' height='100'/>");
        var output = directory.Combine("b.svg");

        // Act
        var result = Flatten(input, output);

        // Assert: the nested file is plain, so nothing is written, and its hidden diagram is reported
        Assert.False(result.Flattened);
        var note = Assert.Single(result.Diagnostics);
        Assert.Equal((SvgFlattener.ImageNotDrawnCode, "warning", "svg"), (note.Code, note.Severity, note.Source));
        Assert.Equal(["parts/inner.svg"], note.Origin);
        Assert.Equal(("parts/inner.svg", 2), (note.Location?.File, note.Location?.Line));
        Assert.Contains("'flow.mmd'", note.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a diagram a nested SVG file shows is not reported as not drawn.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_ShownDiagramInNestedSvg_NotReported()
    {
        // Arrange
        using var directory = new TempDirectory();
        CopyDiagram(directory);
        WriteSvg(directory, "inner.svg", "<image href='flow.mmd' width='40' height='40'/>");
        var input = WriteSvg(directory, "a.svg", "<image href='inner.svg' width='100' height='100'/>");

        // Act
        var result = Flatten(input, directory.Combine("b.svg"));

        // Assert
        Assert.True(result.Flattened);
        Assert.DoesNotContain(result.Diagnostics, d => d.Code == SvgFlattener.ImageNotDrawnCode);
    }

    /// <summary>
    ///     Test that a link to a missing file is reported as render reports it and nothing is written.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_MissingLinkedFile_ReportsUnresolved()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = WriteSvg(directory, "a.svg", "<image href='missing.mmd' width='40' height='40'/>");

        // Act
        var result = Flatten(input, directory.Combine("b.svg"));

        // Assert
        Assert.False(result.Flattened);
        Assert.Equal(FolderImageResolver.UnresolvedCode, Assert.Single(result.Diagnostics).Code);
    }

    /// <summary>
    ///     Test that a link to a missing file is an error with the strict policy.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_MissingLinkedFileStrict_ThrowsUnsupportedFeature()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = WriteSvg(directory, "a.svg", "<image href='missing.mmd' width='40' height='40'/>");

        // Act
        var ex = Assert.Throws<ViewerException>(() => Flatten(input, directory.Combine("b.svg"), strict: true));

        // Assert
        Assert.Equal(ErrorCodes.UnsupportedFeature, ex.Code);
        Assert.Contains("missing.mmd", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a link that leaves the folder of the drawing is refused by the sandbox.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_LinkOutsideFolder_RefusedBySandbox()
    {
        // Arrange: the diagram exists, but beside the folder of the drawing
        using var directory = new TempDirectory();
        CopyDiagram(directory);
        var input = FixtureFactory.WriteText(
            directory.Path,
            "sub/a.svg",
            "<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'><image href='../flow.mmd' width='40' height='40'/></svg>");

        // Act
        var result = Flatten(input, directory.Combine("sub", "b.svg"));

        // Assert
        Assert.False(result.Flattened);
        var note = Assert.Single(result.Diagnostics);
        Assert.Equal(FolderImageResolver.UnresolvedCode, note.Code);
        Assert.Contains("outside the folder", note.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a nested SVG file that links itself is stopped, and nothing is replaced for it.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_SvgCycle_StopsWithoutReplacingTheCycle()
    {
        // Arrange
        using var directory = new TempDirectory();
        WriteSvg(directory, "loop.svg", "<image href='loop.svg' width='50' height='50'/>");
        var input = WriteSvg(directory, "a.svg", "<image href='loop.svg' width='50' height='50'/>");

        // Act
        var result = Flatten(input, directory.Combine("b.svg"));

        // Assert
        Assert.False(result.Flattened);
        Assert.Contains(result.Diagnostics, d => d.Code == FolderImageResolver.UnresolvedCode && d.Message.Contains("cycle", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test that no more than 64 pictures are drawn; the links over the limit stay and are reported.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_MoreThan64Images_ExtraLinksStayAndAreReported()
    {
        // Arrange: 65 different diagrams
        using var directory = new TempDirectory();
        var body = new StringBuilder();
        for (var i = 0; i < 65; i++)
        {
            FixtureFactory.WriteText(directory.Path, $"d{i}.mmd", "flowchart LR\n  a --> b\n");
            body.Append($"<image href='d{i}.mmd' width='8' height='8'/>");
        }

        var input = WriteSvg(directory, "a.svg", body.ToString());
        var output = directory.Combine("b.svg");

        // Act
        var result = Flatten(input, output);

        // Assert
        Assert.Equal(64, result.Replaced.Count);
        Assert.Contains(result.Diagnostics, d => d.Code == FolderImageResolver.UnresolvedCode && d.Message.Contains("more than 64", StringComparison.Ordinal));
        Assert.Contains("href='d64.mmd'", File.ReadAllText(output), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a copy over the output limit is refused and nothing is written.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_OutputOverLimit_ThrowsSizeLimitAndWritesNothing()
    {
        // Arrange
        using var directory = new TempDirectory();
        CopyDiagram(directory);
        var input = WriteSvg(directory, "a.svg", "<image href='flow.mmd' width='100' height='100'/>");
        var output = directory.Combine("b.svg");

        // Act
        var ex = Assert.Throws<ViewerException>(
            () => SvgFlattener.Execute(SourceLoader.Probe(input), new FlattenRequest(output) { MaxOutputBytes = 1000 }));

        // Assert
        Assert.Equal(ErrorCodes.SizeLimit, ex.Code);
        Assert.False(File.Exists(output));
    }

    /// <summary>
    ///     Test that the relative links that are left are rebased when the copy is written to another folder.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_OutputInOtherFolder_RebasesRemainingRelativeLinks()
    {
        // Arrange
        using var directory = new TempDirectory();
        CopyDiagram(directory);
        WritePng(directory.Combine("my logo.png"));
        var input = WriteSvg(directory, "a.svg", "<image href='flow.mmd' width='40' height='40'/><image href='my logo.png' x='50' width='40' height='40'/>");
        var output = directory.Combine("out", "b.svg");

        // Act
        var result = Flatten(input, output);

        // Assert
        var rebased = Assert.Single(result.Rebased);
        Assert.Equal(("my logo.png", "../my%20logo.png"), (rebased.Href, rebased.NewHref));
        Assert.Contains("href='../my%20logo.png'", File.ReadAllText(output), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a relative link whose path contains a null character is left as written when the copy is written to
    ///     another folder, and does not fail the flatten.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_OutputInOtherFolder_InvalidLinkPathLeftAsWritten()
    {
        // Arrange: a diagram to replace, and a raster link that decodes to a null character
        using var directory = new TempDirectory();
        CopyDiagram(directory);
        var input = WriteSvg(directory, "a.svg", "<image href='flow.mmd' width='40' height='40'/><image href='x%00.png' x='50' width='40' height='40'/>");
        var output = directory.Combine("out", "b.svg");

        // Act
        var result = Flatten(input, output);

        // Assert: the diagram is replaced, and the link is kept as written and not reported as rebased
        Assert.True(result.Flattened);
        Assert.Empty(result.Rebased);
        Assert.Contains("href='x%00.png'", File.ReadAllText(output), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the size of the copy is counted in UTF-8 bytes while it is built: a replaced link of characters that
    ///     take three bytes each fits a limit of exactly the size of the copy, and one byte less is refused.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_NonAsciiHrefAtOutputLimit_CountsUtf8Bytes()
    {
        // Arrange: a diagram whose name is twelve CJK characters (36 bytes for 12 UTF-16 characters)
        using var directory = new TempDirectory();
        const string name = "流程图流程图流程图流程图.mmd";
        CopyDiagram(directory, name);
        var input = WriteSvg(directory, "a.svg", $"<image href='{name}' width='40' height='40'/>");
        var size = Flatten(input, directory.Combine("size.svg")).Bytes;
        var info = SourceLoader.Probe(input);

        // Act
        var atLimit = SvgFlattener.Execute(info, new FlattenRequest(directory.Combine("b.svg")) { MaxOutputBytes = size });
        var ex = Assert.Throws<ViewerException>(
            () => SvgFlattener.Execute(info, new FlattenRequest(directory.Combine("c.svg")) { MaxOutputBytes = size - 1 }));

        // Assert
        Assert.True(atLimit.Flattened);
        Assert.Equal(size, atLimit.Bytes);
        Assert.Equal(ErrorCodes.SizeLimit, ex.Code);
        Assert.False(File.Exists(directory.Combine("c.svg")));
    }

    /// <summary>
    ///     Test that a file that is not an SVG drawing is refused.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_NotSvg_ThrowsUnsupportedFormat()
    {
        // Arrange
        using var directory = new TempDirectory();
        WritePng(directory.Combine("a.png"));

        // Act
        var ex = Assert.Throws<ViewerException>(() => Flatten(directory.Combine("a.png"), directory.Combine("b.svg")));

        // Assert
        Assert.Equal(ErrorCodes.UnsupportedFormat, ex.Code);
    }

    /// <summary>
    ///     Test that a drawing with a document type declaration is refused as corrupt, as render refuses it.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_Doctype_ThrowsCorruptInput()
    {
        // Arrange: probed while it was valid, then given a DTD
        using var directory = new TempDirectory();
        var input = WriteSvg(directory, "a.svg", string.Empty);
        var info = SourceLoader.Probe(input);
        File.WriteAllText(input, "<!DOCTYPE svg [<!ENTITY x 'y'>]><svg xmlns='http://www.w3.org/2000/svg'/>");

        // Act
        var ex = Assert.Throws<ViewerException>(() => SvgFlattener.Execute(info, new FlattenRequest(directory.Combine("b.svg"))));

        // Assert
        Assert.Equal(ErrorCodes.CorruptInput, ex.Code);
    }

    /// <summary>
    ///     Test that a drawing that is not UTF-8 is refused as corrupt.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_InvalidUtf8_ThrowsCorruptInput()
    {
        // Arrange: a Latin-1 byte in a comment
        using var directory = new TempDirectory();
        var input = WriteSvg(directory, "a.svg", string.Empty);
        var info = SourceLoader.Probe(input);
        File.WriteAllBytes(input, [.. Encoding.ASCII.GetBytes("<svg xmlns='http://www.w3.org/2000/svg'><!-- "), 0xE9, .. Encoding.ASCII.GetBytes(" --></svg>")]);

        // Act
        var ex = Assert.Throws<ViewerException>(() => SvgFlattener.Execute(info, new FlattenRequest(directory.Combine("b.svg"))));

        // Assert
        Assert.Equal(ErrorCodes.CorruptInput, ex.Code);
    }

    /// <summary>
    ///     Test that a drawing that declares another encoding and is not plain ASCII is refused.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_NonUtf8Declaration_ThrowsUnsupportedFeature()
    {
        // Arrange: UTF-8 bytes that a reader would take as Latin-1
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(
            directory.Path,
            "a.svg",
            "<?xml version='1.0' encoding='ISO-8859-1'?><svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'><title>\u00E9</title></svg>");

        // Act
        var ex = Assert.Throws<ViewerException>(() => Flatten(input, directory.Combine("b.svg")));

        // Assert
        Assert.Equal(ErrorCodes.UnsupportedFeature, ex.Code);
    }

    /// <summary>
    ///     Test that the output may not be the input.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_OutputIsInput_ThrowsSameInputOutput()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = WriteSvg(directory, "a.svg", string.Empty);

        // Act
        var ex = Assert.Throws<ViewerException>(() => Flatten(input, input));

        // Assert
        Assert.Equal(ErrorCodes.SameInputOutput, ex.Code);
    }

    /// <summary>
    ///     Test that an existing output is refused with no-clobber.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_NoClobberExisting_ThrowsOutputExists()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = WriteSvg(directory, "a.svg", string.Empty);
        var output = FixtureFactory.WriteText(directory.Path, "b.svg", "keep");

        // Act
        var ex = Assert.Throws<ViewerException>(
            () => SvgFlattener.Execute(SourceLoader.Probe(input), new FlattenRequest(output) { NoClobber = true }));

        // Assert
        Assert.Equal(ErrorCodes.OutputExists, ex.Code);
        Assert.Equal("keep", File.ReadAllText(output));
    }

    /// <summary>
    ///     Test that a linked model with a syntax error is an error that names both files.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_BrokenLinkedModel_ThrowsScadErrorNamingBothFiles()
    {
        // Arrange
        using var directory = new TempDirectory();
        FixtureFactory.WriteText(directory.Path, "part.scad", "cube([1, 2, 3]");
        var input = WriteSvg(directory, "a.svg", "<image href='part.scad' width='40' height='40'/>");

        // Act
        var ex = Assert.Throws<ViewerException>(() => Flatten(input, directory.Combine("b.svg")));

        // Assert
        Assert.Equal(ErrorCodes.ScadError, ex.Code);
        Assert.Contains("'a.svg' embeds 'part.scad'", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a scale out of range is a usage error.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_ScaleOutOfRange_ThrowsUsageError()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = WriteSvg(directory, "a.svg", string.Empty);

        // Act
        var ex = Assert.Throws<ViewerException>(() => Flatten(input, directory.Combine("b.svg"), scale: 0));

        // Assert
        Assert.Equal(ErrorCodes.UsageError, ex.Code);
    }

    /// <summary>
    ///     Test that flattening the same drawing twice writes the same bytes.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_SameInputTwice_WritesIdenticalBytes()
    {
        // Arrange
        using var directory = new TempDirectory();
        File.Copy(FixtureFactory.FixturePath("cube.scad"), directory.Combine("part.scad"));
        var input = WriteSvg(directory, "a.svg", "<image href='part.scad' width='40' height='40'/>");

        // Act
        Flatten(input, directory.Combine("b.svg"));
        Flatten(input, directory.Combine("c.svg"));

        // Assert
        Assert.Equal(File.ReadAllBytes(directory.Combine("b.svg")), File.ReadAllBytes(directory.Combine("c.svg")));
    }
}
