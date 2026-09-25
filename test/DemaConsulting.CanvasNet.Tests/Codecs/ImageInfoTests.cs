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

    /// <summary>
    ///     Proves that <c>default(ImageInfo)</c> has <see cref="ImageInfo.CanDecode"/> equal to
    ///     false - not true, despite the property's <c>= true</c> initializer - because a struct's
    ///     field initializers only run when one of its declared constructors runs, and
    ///     <c>default(ImageInfo)</c> zero-initializes every field directly, bypassing every
    ///     constructor and initializer. This is a documented caveat, not a bug: no codec's
    ///     <c>GetInfo</c> ever produces a bare <c>default(ImageInfo)</c>.
    /// </summary>
    [Fact]
    public void ImageInfo_DefaultValue_HasCanDecodeFalse()
    {
        var info = default(ImageInfo);

        Assert.False(info.CanDecode);
    }
}
