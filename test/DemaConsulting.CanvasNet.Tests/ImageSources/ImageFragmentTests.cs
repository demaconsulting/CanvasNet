// cspell:ignore hrefs bzzz
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Tests.ImageSources;

/// <summary>
///     Unit tests for <see cref="ImageFragment"/>: the grammar of the fragment of a link, its decoding, its limits and
///     its typed accessors.
/// </summary>
public class ImageFragmentTests
{
    /// <summary>
    ///     Test that a missing or empty fragment is the empty fragment.
    /// </summary>
    /// <param name="raw">The raw text.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ImageFragment_Parse_NullOrEmpty_IsEmpty(string? raw)
    {
        // Act
        var fragment = ImageFragment.Parse(raw);

        // Assert
        Assert.Same(ImageFragment.Empty, fragment);
        Assert.Equal(string.Empty, fragment.Raw);
        Assert.Empty(fragment.Parameters);
        Assert.Null(fragment.Problem);
    }

    /// <summary>
    ///     Test that parameters are split on the ampersand, keep their order, and that the raw text is kept as written.
    /// </summary>
    [Fact]
    public void ImageFragment_Parse_TwoParameters_KeepsOrderAndRaw()
    {
        // Act
        var fragment = ImageFragment.Parse("page=3&view=front");

        // Assert
        Assert.Equal("page=3&view=front", fragment.Raw);
        Assert.Equal(
            [new ImageFragmentParameter("page", "3"), new ImageFragmentParameter("view", "front")],
            fragment.Parameters);
        Assert.Null(fragment.Problem);
    }

    /// <summary>
    ///     Test the shapes of a single parameter: split at the first equals sign, no equals sign, and an empty value.
    /// </summary>
    /// <param name="raw">The raw text.</param>
    /// <param name="name">The expected name.</param>
    /// <param name="value">The expected value.</param>
    [Theory]
    [InlineData("a=b=c", "a", "b=c")]
    [InlineData("a", "a", "")]
    [InlineData("a=", "a", "")]
    [InlineData("=3", "", "3")]
    [InlineData("a=b+c", "a", "b+c")]
    [InlineData("a=%zz", "a", "%zz")]
    [InlineData("a=%26b", "a", "&b")]
    [InlineData("a%3Db=1", "a=b", "1")]
    [InlineData("pa%67e=%33", "page", "3")]
    public void ImageFragment_Parse_SingleParameter_SplitsBeforeDecoding(string raw, string name, string value)
    {
        // Act
        var fragment = ImageFragment.Parse(raw);

        // Assert
        var parameter = Assert.Single(fragment.Parameters);
        Assert.Equal(name, parameter.Name);
        Assert.Equal(value, parameter.Value);
    }

    /// <summary>
    ///     Test that empty segments are skipped.
    /// </summary>
    /// <param name="raw">The raw text.</param>
    [Theory]
    [InlineData("&page=2")]
    [InlineData("page=2&")]
    [InlineData("&&page=2&&")]
    public void ImageFragment_Parse_EmptySegments_AreSkipped(string raw)
    {
        // Act
        var fragment = ImageFragment.Parse(raw);

        // Assert
        Assert.Equal([new ImageFragmentParameter("page", "2")], fragment.Parameters);
    }

    /// <summary>
    ///     Test that a second hash and a question mark in the fragment belong to it.
    /// </summary>
    [Fact]
    public void ImageFragment_Parse_HashAndQuestionMark_StayInTheText()
    {
        // Act
        var fragment = ImageFragment.Parse("a=b#c?d");

        // Assert
        Assert.Equal([new ImageFragmentParameter("a", "b#c?d")], fragment.Parameters);
    }

    /// <summary>
    ///     Test that names are matched case-insensitively and the last occurrence wins, also across aliases.
    /// </summary>
    [Fact]
    public void ImageFragment_GetValue_CaseInsensitiveLastWins_AcrossAliases()
    {
        // Arrange
        var fragment = ImageFragment.Parse("page=1&Slide=4&PAGE=2&x=9");

        // Act / Assert
        Assert.Equal("2", fragment.GetValue("page"));
        Assert.Equal("2", fragment.GetValue("PAGE", "slide"));
        Assert.Equal("4", fragment.GetValue("slide"));
        Assert.Equal("9", fragment.GetValue("X"));
        Assert.Null(fragment.GetValue("view"));
        Assert.Null(ImageFragment.Empty.GetValue("page"));
        Assert.Equal(4, ImageFragment.Parse("page=1&slide=4").GetPositiveInt32("page", "slide"));
        Assert.Equal(2, ImageFragment.Parse("slide=4&page=2").GetPositiveInt32("page", "slide"));
    }

    /// <summary>
    ///     Test that <see cref="ImageFragment.GetValue(string[])"/> rejects a null list of names.
    /// </summary>
    [Fact]
    public void ImageFragment_GetValue_NullNames_ThrowsArgumentNullException()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => ImageFragment.Empty.GetValue(null!));
    }

    /// <summary>
    ///     Test that valid page numbers are read, including leading zeros, and that an absent parameter is null.
    /// </summary>
    [Fact]
    public void ImageFragment_GetPositiveInt32_ValidValues_ReturnsNumber()
    {
        // Act / Assert
        Assert.Equal(3, ImageFragment.Parse("page=003").GetPositiveInt32("page"));
        Assert.Equal(1, ImageFragment.Parse("page=1").GetPositiveInt32("page"));
        Assert.Equal(int.MaxValue, ImageFragment.Parse("page=2147483647").GetPositiveInt32("page"));
        Assert.Null(ImageFragment.Parse("view=front").GetPositiveInt32("page"));
        Assert.Null(ImageFragment.Empty.GetPositiveInt32("page"));
    }

    /// <summary>
    ///     Test that every unusable value is refused with the invalid-fragment code.
    /// </summary>
    /// <param name="raw">The raw fragment.</param>
    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-1")]
    [InlineData("page=+2")]
    [InlineData("page=%202")]
    [InlineData("page=2.5")]
    [InlineData("page=abc")]
    [InlineData("page=")]
    [InlineData("page")]
    [InlineData("page=2147483648")]
    [InlineData("page=99999999999999999999")]
    [InlineData("page=%D9%A2")]
    public void ImageFragment_GetPositiveInt32_UnusableValue_ThrowsRefusedWithInvalidCode(string raw)
    {
        // Arrange
        var fragment = ImageFragment.Parse(raw);

        // Act
        var exception = Assert.Throws<ImageSourceRefusedException>(() => fragment.GetPositiveInt32("page"));

        // Assert
        Assert.Equal(FolderImageResolver.FragmentInvalidCode, exception.Code);
        Assert.Contains("'page'", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a fragment over the length limit is not parsed and says why.
    /// </summary>
    [Fact]
    public void ImageFragment_Parse_TooLong_HasProblemAndNoParameters()
    {
        // Arrange
        var raw = "a=" + new string('x', ImageFragment.MaxLength);

        // Act
        var fragment = ImageFragment.Parse(raw);

        // Assert
        Assert.NotNull(fragment.Problem);
        Assert.Contains("2048", fragment.Problem, StringComparison.Ordinal);
        Assert.Empty(fragment.Parameters);
        Assert.Equal(raw, fragment.Raw);
    }

    /// <summary>
    ///     Test that the length limit is inclusive and the parameter limit counts only real parameters.
    /// </summary>
    [Fact]
    public void ImageFragment_Parse_AtTheLimits_IsParsed()
    {
        // Arrange: exactly 2048 characters, and exactly 32 parameters padded with empty segments
        var longest = "a=" + new string('x', ImageFragment.MaxLength - 2);
        var many = string.Join("&&", Enumerable.Range(0, ImageFragment.MaxParameters).Select(i => "p" + i));

        // Act
        var long1 = ImageFragment.Parse(longest);
        var long2 = ImageFragment.Parse(many);

        // Assert
        Assert.Null(long1.Problem);
        Assert.Single(long1.Parameters);
        Assert.Null(long2.Problem);
        Assert.Equal(ImageFragment.MaxParameters, long2.Parameters.Count);
    }

    /// <summary>
    ///     Test that more than the maximum number of parameters is a problem.
    /// </summary>
    [Fact]
    public void ImageFragment_Parse_TooManyParameters_HasProblemAndNoParameters()
    {
        // Arrange
        var raw = string.Join('&', Enumerable.Range(0, ImageFragment.MaxParameters + 1).Select(i => "p" + i));

        // Act
        var fragment = ImageFragment.Parse(raw);

        // Assert
        Assert.NotNull(fragment.Problem);
        Assert.Contains("32", fragment.Problem, StringComparison.Ordinal);
        Assert.Empty(fragment.Parameters);
    }

    /// <summary>
    ///     Test that the quoting of untrusted text replaces control characters and truncates.
    /// </summary>
    [Fact]
    public void ImageFragment_GetPositiveInt32_HostileValue_IsQuotedSafelyInMessage()
    {
        // Arrange: a control character (decoded from %0A) and a very long value
        var fragment = ImageFragment.Parse("page=a%0Ab" + new string('z', 200));

        // Act
        var exception = Assert.Throws<ImageSourceRefusedException>(() => fragment.GetPositiveInt32("page"));

        // Assert: one line, truncated
        Assert.DoesNotContain('\n', exception.Message);
        Assert.Contains("a?bzzz", exception.Message, StringComparison.Ordinal);
        Assert.Contains("...'", exception.Message, StringComparison.Ordinal);
        Assert.True(exception.Message.Length < 150);
    }

    /// <summary>
    ///     Test that the parameters of a fragment cannot be changed through the exposed list.
    /// </summary>
    [Fact]
    public void ImageFragment_Parameters_AreReadOnly()
    {
        // Arrange
        var fragment = ImageFragment.Parse("a=1");

        // Act / Assert
        ReadOnlyListAssert.IsReadOnly(fragment.Parameters, new ImageFragmentParameter("b", "2"));
    }
}
