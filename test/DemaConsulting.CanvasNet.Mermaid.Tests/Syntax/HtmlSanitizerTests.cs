using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Syntax;

/// <summary>
///     Unit tests of <see cref="HtmlSanitizer"/>; the expected texts are what Mermaid's <c>sanitizeText</c> (DOMPurify in
///     Chrome) gives for the same labels.
/// </summary>
public class HtmlSanitizerTests
{
    /// <summary>
    ///     Validates the clean-up of labels: the fast path, entities, removed and kept elements and attributes, cut-off
    ///     tags and formatting elements closed out of order.
    /// </summary>
    /// <param name="text">The label.</param>
    /// <param name="expected">Mermaid's result.</param>
    [Theory]
    [InlineData("a & b", "a & b")]
    [InlineData("fish &amp; chips", "fish &amp; chips")]
    [InlineData("a<b", "a")]
    [InlineData("x <br/> y & z", "x <br> y &amp; z")]
    [InlineData("a < b", "a &lt; b")]
    [InlineData("<b>bold</b> and <i>italic</i>", "<b>bold</b> and <i>italic</i>")]
    [InlineData("<B>Upper</B>", "<b>Upper</b>")]
    [InlineData("<b>open bold", "<b>open bold</b>")]
    [InlineData("safe <script>alert</script> text", "safe  text")]
    [InlineData("<foo>kept text</foo> after", "kept text after")]
    [InlineData("<span style='color:red'>red</span>", "<span style=\"color:red\">red</span>")]
    [InlineData("<span title='a>b'>t</span>", "<span title=\"a&gt;b\">t</span>")]
    [InlineData("<a href='x.html' target='_blank'>l</a>", "<a href=\"x.html\" rel=\"noopener\" target=\"_blank\">l</a>")]
    [InlineData("<a href='javascript:alert'>l</a>", "<a>l</a>")]
    [InlineData("<img src='p.png' width='20' onerror='x'>", "<img src=\"p.png\" width=\"20\">")]
    [InlineData("x</br>y", "x<br>y")]
    [InlineData("<!-- c -->x<br>", "x<br>")]
    [InlineData("&lt;b&gt; <br>", "&lt;b&gt; <br>")]
    [InlineData("&copy; &unknown; <br>", "\u00a9 &amp;unknown; <br>")]
    [InlineData("<i>a<b>b</i>c", "<i>a<b>b</b></i><b>c</b>")]
    [InlineData("<i><b>b</i>", "<i><b>b</b></i>")]
    public void HtmlSanitizer_Sanitize_Label_MatchesMermaid(string text, string expected)
    {
        // Act
        var result = HtmlSanitizer.Sanitize(text);

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>
    ///     Validates that a long hostile text is cleaned in one pass without failing.
    /// </summary>
    [Fact]
    public void HtmlSanitizer_Sanitize_ManyUnclosedTags_ClosesThemAll()
    {
        // Arrange
        var text = string.Concat(Enumerable.Repeat("<b>x", 2_000));

        // Act
        var result = HtmlSanitizer.Sanitize(text);

        // Assert
        Assert.EndsWith(string.Concat(Enumerable.Repeat("</b>", 2_000)), result, StringComparison.Ordinal);
        Assert.Equal(2_000, result.Split("<b>").Length - 1);
    }
}
