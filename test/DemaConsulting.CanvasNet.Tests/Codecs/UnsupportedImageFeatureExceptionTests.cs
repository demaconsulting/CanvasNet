using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Tests.Codecs;

/// <summary>
///     Unit tests for the <see cref="UnsupportedImageFeatureException"/> class.
/// </summary>
public class UnsupportedImageFeatureExceptionTests
{
    /// <summary>
    ///     Proves that the parameterless constructor produces a non-empty default message and an
    ///     empty <see cref="UnsupportedImageFeatureException.Feature"/>.
    /// </summary>
    [Fact]
    public void UnsupportedImageFeatureException_ParameterlessConstructor_HasEmptyFeature()
    {
        var exception = new UnsupportedImageFeatureException();

        Assert.Equal(string.Empty, exception.Feature);
        Assert.NotEmpty(exception.Message);
    }

    /// <summary>
    ///     Proves that the message-only constructor sets <see cref="Exception.Message"/> and
    ///     leaves <see cref="UnsupportedImageFeatureException.Feature"/> empty.
    /// </summary>
    [Fact]
    public void UnsupportedImageFeatureException_MessageOnlyConstructor_HasEmptyFeature()
    {
        var exception = new UnsupportedImageFeatureException("test message");

        Assert.Equal("test message", exception.Message);
        Assert.Equal(string.Empty, exception.Feature);
    }

    /// <summary>
    ///     Proves that the message-and-inner-exception constructor sets both
    ///     <see cref="Exception.Message"/> and <see cref="Exception.InnerException"/>, and leaves
    ///     <see cref="UnsupportedImageFeatureException.Feature"/> empty.
    /// </summary>
    [Fact]
    public void UnsupportedImageFeatureException_MessageAndInnerConstructor_HasEmptyFeature()
    {
        var inner = new InvalidOperationException("inner");

        var exception = new UnsupportedImageFeatureException("test message", inner);

        Assert.Equal("test message", exception.Message);
        Assert.Same(inner, exception.InnerException);
        Assert.Equal(string.Empty, exception.Feature);
    }

    /// <summary>
    ///     Proves that the feature-and-message constructor sets both
    ///     <see cref="UnsupportedImageFeatureException.Feature"/> and
    ///     <see cref="Exception.Message"/>.
    /// </summary>
    [Fact]
    public void UnsupportedImageFeatureException_FeatureAndMessageConstructor_SetsBoth()
    {
        var exception = new UnsupportedImageFeatureException("test-feature", "test message");

        Assert.Equal("test-feature", exception.Feature);
        Assert.Equal("test message", exception.Message);
    }

    /// <summary>
    ///     Proves that the feature, message, and inner-exception constructor sets all three
    ///     properties.
    /// </summary>
    [Fact]
    public void UnsupportedImageFeatureException_FeatureMessageAndInnerConstructor_SetsAll()
    {
        var inner = new InvalidOperationException("inner");

        var exception = new UnsupportedImageFeatureException("test-feature", "test message", inner);

        Assert.Equal("test-feature", exception.Feature);
        Assert.Equal("test message", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }

    /// <summary>
    ///     Proves that <see cref="UnsupportedImageFeatureException"/> is an
    ///     <see cref="IOException"/> - its actual base type, since
    ///     <see cref="InvalidDataException"/> is sealed in .NET and cannot be a base type.
    /// </summary>
    [Fact]
    public void UnsupportedImageFeatureException_IsIOException()
    {
        var exception = new UnsupportedImageFeatureException("test-feature", "test message");

        Assert.IsAssignableFrom<IOException>(exception);
    }

    /// <summary>
    ///     Proves that <see cref="UnsupportedImageFeatureException"/> is deliberately <em>not</em>
    ///     an <see cref="InvalidDataException"/>, documenting the compatibility boundary: an
    ///     existing <c>catch (InvalidDataException)</c> block does not catch this exception.
    /// </summary>
    [Fact]
    public void UnsupportedImageFeatureException_IsNotInvalidDataException()
    {
        var exception = new UnsupportedImageFeatureException("test-feature", "test message");

        Assert.IsNotType<InvalidDataException>(exception, exactMatch: false);
    }
}
