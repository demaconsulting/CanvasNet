using DemaConsulting.CanvasNet.Fonts;

namespace DemaConsulting.CanvasNet.Tests.Fonts;

/// <summary>
///     Unit tests for <see cref="CffStandardEncoding"/>.
/// </summary>
public class CffStandardEncodingTests
{
    /// <summary>
    ///     Proves that CffStandardEncoding CodeToGlyphName HasExpectedLength.
    /// </summary>
    [Fact]
    public void CffStandardEncoding_CodeToGlyphName_HasExpectedLength()
    {
        Assert.Equal(256, CffStandardEncoding.CodeToGlyphName.Length);
    }

    /// <summary>
    ///     Proves that CffStandardEncoding CodeToGlyphName SpotChecksKnownCodes.
    /// </summary>
    [Theory]
    [InlineData(32, "space")]
    [InlineData(65, "A")]
    [InlineData(97, "a")]
    [InlineData(193, "grave")]
    [InlineData(194, "acute")]
    public void CffStandardEncoding_CodeToGlyphName_SpotChecksKnownCodes(int code, string expectedName)
    {
        Assert.Equal(expectedName, CffStandardEncoding.CodeToGlyphName[code]);
    }

    /// <summary>
    ///     Proves that CffStandardEncoding CodeToGlyphName UndefinedControlCodesAreNull.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    [InlineData(127)]
    [InlineData(160)]
    public void CffStandardEncoding_CodeToGlyphName_UndefinedControlCodesAreNull(int code)
    {
        Assert.Null(CffStandardEncoding.CodeToGlyphName[code]);
    }
}
