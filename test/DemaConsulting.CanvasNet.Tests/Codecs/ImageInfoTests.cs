using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Tests.Codecs;

/// <summary>
///     Unit tests for the <see cref="ImageInfo"/> record struct, focused on its
///     <see cref="ImageInfo.CanDecode"/> member - every other member is exercised indirectly
///     through each codec's own <c>GetInfo</c> tests.
/// </summary>
public class ImageInfoTests
{
    /// <summary>
    ///     Proves that constructing an <see cref="ImageInfo"/> via its primary constructor,
    ///     without setting <see cref="ImageInfo.CanDecode"/>, defaults it to true.
    /// </summary>
    [Fact]
    public void ImageInfo_ConstructedWithoutCanDecode_DefaultsToTrue()
    {
        var info = new ImageInfo(4, 3, 4, true);

        Assert.True(info.CanDecode);
    }

    /// <summary>
    ///     Proves that <see cref="ImageInfo.CanDecode"/> can be set to false via
    ///     object-initializer syntax alongside the primary constructor.
    /// </summary>
    [Fact]
    public void ImageInfo_ConstructedWithCanDecodeFalse_ReportsCanDecodeFalse()
    {
        var info = new ImageInfo(4, 3, 4, true) { CanDecode = false };

        Assert.False(info.CanDecode);
    }

    /// <summary>
    ///     Proves that two <see cref="ImageInfo"/> values with identical Width/Height/Channels/
    ///     HasAlpha but different <see cref="ImageInfo.CanDecode"/> values are not equal - since
    ///     <see cref="ImageInfo"/> is a record struct, its generated value equality includes every
    ///     member, including this one.
    /// </summary>
    [Fact]
    public void ImageInfo_EqualsOperator_DiffersWhenOnlyCanDecodeDiffers()
    {
        var canDecodeTrue = new ImageInfo(4, 3, 4, true);
        var canDecodeFalse = new ImageInfo(4, 3, 4, true) { CanDecode = false };

        Assert.NotEqual(canDecodeTrue, canDecodeFalse);
    }

    /// <summary>
    ///     Proves that two separately constructed <see cref="ImageInfo"/> values with identical
    ///     Width/Height/Channels/HasAlpha/CanDecode compare equal, confirming the record struct's
    ///     generated value equality still holds once <see cref="ImageInfo.CanDecode"/> is
    ///     explicitly set to the same value on both.
    /// </summary>
    [Fact]
    public void ImageInfo_EqualsOperator_EqualWhenEveryMemberMatches()
    {
        var first = new ImageInfo(4, 3, 4, true) { CanDecode = false };
        var second = new ImageInfo(4, 3, 4, true) { CanDecode = false };

        Assert.Equal(first, second);
    }
}
