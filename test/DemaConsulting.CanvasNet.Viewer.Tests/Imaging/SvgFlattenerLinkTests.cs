using System.Text;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Viewer.Tests.Imaging;

/// <summary>
///     Unit tests for the link scanner and the rebasing of the SvgFlattener unit: where the value of each link is in the
///     raw text, and how a relative link is rewritten for another folder.
/// </summary>
public class SvgFlattenerLinkTests
{
    /// <summary>
    ///     Finds the links of a text written as UTF-8 without a byte order mark.
    /// </summary>
    /// <param name="text">The text of the drawing.</param>
    /// <returns>The links.</returns>
    private static List<SvgFlattener.ImageLink> Find(string text)
    {
        return SvgFlattener.FindImageLinks(Encoding.UTF8.GetBytes(text), text);
    }

    /// <summary>
    ///     Gets the raw value of a link from the text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="link">The link.</param>
    /// <returns>The characters of the value as written.</returns>
    private static string Raw(string text, SvgFlattener.ImageLink link)
    {
        return text.Substring(link.Start, link.Length);
    }

    /// <summary>
    ///     Test that tabs, line breaks, and spaces around the equals sign do not move the value.
    /// </summary>
    [Fact]
    public void SvgFlattener_FindImageLinks_TabsNewlinesAndSpacesAroundEquals_LocatesValue()
    {
        // Arrange
        const string text = "<svg xmlns='http://www.w3.org/2000/svg'>\n\t<image\twidth='1'\n  href \t=\n \"a.mmd\"/></svg>";

        // Act
        var link = Assert.Single(Find(text));

        // Assert
        Assert.Equal(("a.mmd", '"', "a.mmd", 3), (Raw(text, link), link.Quote, link.Href, link.Line));
    }

    /// <summary>
    ///     Test that characters outside the basic plane before the attribute do not move the value.
    /// </summary>
    [Fact]
    public void SvgFlattener_FindImageLinks_SurrogatePairsBeforeAttribute_LocatesValue()
    {
        // Arrange: a character that is two UTF-16 units and four UTF-8 bytes
        const string text = "<svg xmlns='http://www.w3.org/2000/svg'><image title='\U0001F600\U0001F600' href='b.scad'/></svg>";

        // Act
        var link = Assert.Single(Find(text));

        // Assert
        Assert.Equal("b.scad", Raw(text, link));
    }

    /// <summary>
    ///     Test that lone carriage returns count as line breaks.
    /// </summary>
    [Fact]
    public void SvgFlattener_FindImageLinks_CrOnlyLineBreaks_LocatesValue()
    {
        // Arrange
        const string text = "<svg xmlns='http://www.w3.org/2000/svg'>\r\r<image\rhref='c.pdf'/>\r\n<image href='d.pdf'/></svg>";

        // Act
        var links = Find(text);

        // Assert
        Assert.Equal(["c.pdf", "d.pdf"], links.Select(link => Raw(text, link)));
        Assert.Equal([4, 5], links.Select(link => link.Line));
    }

    /// <summary>
    ///     Test that a byte order mark in the bytes does not move the value in the text.
    /// </summary>
    [Fact]
    public void SvgFlattener_FindImageLinks_BomInput_LocatesValue()
    {
        // Arrange
        const string text = "<svg xmlns='http://www.w3.org/2000/svg'><image href='e.vsdx'/></svg>";
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(text)];

        // Act
        var link = Assert.Single(SvgFlattener.FindImageLinks(bytes, text));

        // Assert
        Assert.Equal("e.vsdx", Raw(text, link));
    }

    /// <summary>
    ///     Test that the span is the raw value with its entities, and the link is the decoded, trimmed value.
    /// </summary>
    [Fact]
    public void SvgFlattener_FindImageLinks_EntitiesInValue_SpanIsRawValueHrefIsDecoded()
    {
        // Arrange
        const string text = "<svg xmlns='http://www.w3.org/2000/svg'><image href=' c.vl.json#param.a=1&amp;param.b=2 '/></svg>";

        // Act
        var link = Assert.Single(Find(text));

        // Assert
        Assert.Equal(" c.vl.json#param.a=1&amp;param.b=2 ", Raw(text, link));
        Assert.Equal("c.vl.json#param.a=1&param.b=2", link.Href);
    }

    /// <summary>
    ///     Test that markup in comments and character data is not found.
    /// </summary>
    [Fact]
    public void SvgFlattener_FindImageLinks_ImageInCommentOrCdata_Ignored()
    {
        // Arrange
        const string text = "<svg xmlns='http://www.w3.org/2000/svg'><!-- <image href='a.mmd'/> --><style><![CDATA[<image href='b.mmd'/>]]></style></svg>";

        // Act / Assert
        Assert.Empty(Find(text));
    }

    /// <summary>
    ///     Test that a prefixed image element is found, as the SVG library matches it by its local name.
    /// </summary>
    [Fact]
    public void SvgFlattener_FindImageLinks_PrefixedSvgImage_Found()
    {
        // Arrange
        const string text = "<s:svg xmlns:s='http://www.w3.org/2000/svg' xmlns:x='http://www.w3.org/1999/xlink'><s:image x:href='f.pptx'/></s:svg>";

        // Act
        var link = Assert.Single(Find(text));

        // Assert
        Assert.Equal("f.pptx", link.Href);
    }

    /// <summary>
    ///     Test that a link is rebased to a sibling folder and a parent folder.
    /// </summary>
    [Fact]
    public void SvgFlattener_RebaseLink_SiblingAndParent_RelativePaths()
    {
        // Arrange
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "flatten-rebase"));
        var input = Path.Combine(root, "docs");
        var output = Path.Combine(root, "out", "deep");

        // Act / Assert
        Assert.Equal("../../docs/logo.png", SvgFlattener.RebaseLink("logo.png", '\'', input, output));
        Assert.Equal("../../shared/a.png", SvgFlattener.RebaseLink("../shared/a.png", '\'', input, output));
        Assert.Equal("docs/img/a.png", SvgFlattener.RebaseLink("img/a.png", '\'', input, root));
    }

    /// <summary>
    ///     Test that the query and the fragment are kept and escaped for the attribute, and the path is percent-encoded.
    /// </summary>
    [Fact]
    public void SvgFlattener_RebaseLink_FragmentAndSpecialCharacters_KeptAndEscaped()
    {
        // Arrange
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "flatten-rebase"));
        var output = Path.Combine(root, "out");

        // Act / Assert
        Assert.Equal("../my%20chart.vl.json#param.a=1&amp;param.b=&apos;x&apos;", SvgFlattener.RebaseLink("my chart.vl.json#param.a=1&param.b='x'", '\'', root, output));
        Assert.Equal("../a%20b.pdf?v=1#page=2", SvgFlattener.RebaseLink("a%20b.pdf?v=1#page=2", '"', root, output));
    }

    /// <summary>
    ///     Test that a link that is not a relative path is left alone.
    /// </summary>
    [Fact]
    public void SvgFlattener_RebaseLink_NotRelativePath_Untouched()
    {
        // Arrange
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "flatten-rebase"));
        var output = Path.Combine(root, "out");

        // Act / Assert
        Assert.Null(SvgFlattener.RebaseLink("data:image/png;base64,AAAA", '\'', root, output));
        Assert.Null(SvgFlattener.RebaseLink("https://example.com/a.png", '\'', root, output));
        Assert.Null(SvgFlattener.RebaseLink("#mark", '\'', root, output));
        Assert.Null(SvgFlattener.RebaseLink("/abs/a.png", '\'', root, output));
        Assert.Null(SvgFlattener.RebaseLink(string.Empty, '\'', root, output));
    }

    /// <summary>
    ///     Test that a link whose path contains a null character is left alone instead of failing the rebase.
    /// </summary>
    [Fact]
    public void SvgFlattener_RebaseLink_InvalidPath_Untouched()
    {
        // Arrange
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "flatten-rebase"));
        var output = Path.Combine(root, "out");

        // Act / Assert: an encoded null character in the file name, and in a folder name with a fragment
        Assert.Null(SvgFlattener.RebaseLink("x%00.png", '\'', root, output));
        Assert.Null(SvgFlattener.RebaseLink("a%00b/c.pdf#page=2", '\'', root, output));
    }

    /// <summary>
    ///     Test that a link too long for the file system does not fail the rebase.
    /// </summary>
    [Fact]
    public void SvgFlattener_RebaseLink_VeryLongPath_DoesNotThrow()
    {
        // Arrange: a path longer than Windows allows (other systems rebase it)
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "flatten-rebase"));
        var output = Path.Combine(root, "out");
        var link = new string('a', 40000) + ".png";

        // Act
        var exception = Record.Exception(() => SvgFlattener.RebaseLink(link, '\'', root, output));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    ///     Test the regression that a Windows drive or UNC path is classified as rooted on every platform (the check does
    ///     not depend on the platform's own path rules), and that relative paths, rooted POSIX paths, and schemes are not.
    /// </summary>
    /// <param name="path">The decoded path.</param>
    /// <param name="expected">Whether it is a Windows drive or UNC path.</param>
    [Theory]
    [InlineData(@"C:\x.png", true)]
    [InlineData("C:/x.png", true)]
    [InlineData("c:x.png", true)]
    [InlineData("Z:", true)]
    [InlineData(@"\\server\share\x.png", true)]
    [InlineData(@"\x.png", true)]
    [InlineData("x.png", false)]
    [InlineData("img/x.png", false)]
    [InlineData("../x.png", false)]
    [InlineData("/abs/x.png", false)]
    [InlineData("//server/share/x.png", false)]
    [InlineData("1:x.png", false)]
    [InlineData("C", false)]
    [InlineData("", false)]
    public void SvgFlattener_IsWindowsRootedPath_DriveAndUncPaths_ClassifiedOnEveryPlatform(string path, bool expected)
    {
        // Act
        var rooted = SvgFlattener.IsWindowsRootedPath(path);

        // Assert
        Assert.Equal(expected, rooted);
    }

    /// <summary>
    ///     Test the regression that a Windows drive or UNC link, written plainly or percent-encoded, is left as written when
    ///     the copy goes to another folder, on every platform (on Linux and macOS the platform's own rules would read it as
    ///     a relative file name and rebase it).
    /// </summary>
    /// <param name="href">The link.</param>
    [Theory]
    [InlineData(@"C:\x.png")]
    [InlineData("C:/x.png")]
    [InlineData("C:/x.pdf#page=2")]
    [InlineData(@"\\server\share\x.png")]
    [InlineData("%5C%5Cserver%5Cshare%5Cx.png")]
    [InlineData(@"\x.png")]
    public void SvgFlattener_RebaseLink_WindowsDriveOrUncLink_UntouchedOnEveryPlatform(string href)
    {
        // Arrange
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "flatten-rebase"));
        var output = Path.Combine(root, "out");

        // Act
        var moved = SvgFlattener.RebaseLink(href, '"', root, output);

        // Assert
        Assert.Null(moved);
    }
}
