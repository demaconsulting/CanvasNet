using DemaConsulting.CanvasNet.Files;

namespace DemaConsulting.CanvasNet.Tests.Files;

/// <summary>
///     Unit tests for <see cref="SandboxException"/>: its facts and its messages, which must never disclose a host
///     directory.
/// </summary>
public class SandboxExceptionTests
{
    /// <summary>
    ///     Validates that every failure kind carries its facts and has a non-empty message that does not contain the
    ///     full path of a file failure.
    /// </summary>
    /// <param name="kind">The failure kind.</param>
    [Theory]
    [InlineData(SandboxFailureKind.EmptyPath)]
    [InlineData(SandboxFailureKind.ControlCharacter)]
    [InlineData(SandboxFailureKind.ColonInPath)]
    [InlineData(SandboxFailureKind.AbsolutePath)]
    [InlineData(SandboxFailureKind.OutsideRoot)]
    [InlineData(SandboxFailureKind.ThroughLink)]
    [InlineData(SandboxFailureKind.LinkCheckFailed)]
    [InlineData(SandboxFailureKind.CannotRead)]
    [InlineData(SandboxFailureKind.TooLarge)]
    [InlineData(SandboxFailureKind.InvalidRoot)]
    [InlineData(SandboxFailureKind.TooManyFiles)]
    public void SandboxException_Constructor_EveryKind_KeepsFactsAndHidesHostPath(SandboxFailureKind kind)
    {
        // Arrange
        var inner = new IOException("inner");
        var isPathText = kind <= SandboxFailureKind.OutsideRoot;
        var path = isPathText ? "a/b.txt" : "C:\\secret\\host\\a.txt";

        // Act
        var ex = new SandboxException(kind, path, "detail", inner);

        // Assert
        Assert.Equal(kind, ex.Kind);
        Assert.Equal(path, ex.Path);
        Assert.Equal("detail", ex.Detail);
        Assert.Same(inner, ex.InnerException);
        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
        Assert.Equal(isPathText, ex.Message.Contains("a/b.txt", StringComparison.Ordinal));
        Assert.DoesNotContain("secret", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a null path is an argument error.
    /// </summary>
    [Fact]
    public void SandboxException_Constructor_NullPath_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => new SandboxException(SandboxFailureKind.CannotRead, null!));
    }
}
