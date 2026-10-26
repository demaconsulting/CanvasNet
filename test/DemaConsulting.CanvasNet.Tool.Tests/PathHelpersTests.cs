namespace DemaConsulting.CanvasNet.Tool.Tests;

/// <summary>
///     Tests for the PathHelpers class.
/// </summary>
[Collection("Sequential")]
public class PathHelpersTests
{
    /// <summary>
    ///     Test that SafePathCombine correctly combines valid paths.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_ValidPaths_CombinesCorrectly()
    {
        // Arrange: setup valid base path and relative path for combining
        var basePath = "/home/user/project";
        var relativePath = "subfolder/file.txt";

        // Act: execute the operation being tested
        var result = PathHelpers.SafePathCombine(basePath, relativePath);

        // Assert: verify expected behavior
        Assert.Equal(Path.Combine(basePath, relativePath), result);
    }

    /// <summary>
    ///     Test that SafePathCombine throws ArgumentException for path traversal with double dots.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_PathTraversalWithDoubleDots_ThrowsArgumentException()
    {
        // Arrange: setup base path and dangerous path traversal using double dots
        var basePath = "/home/user/project";
        var relativePath = "../etc/passwd";

        // Act & Assert: attempt path traversal and verify ArgumentException is thrown with expected message
        var exception = Assert.Throws<ArgumentException>(() =>
            PathHelpers.SafePathCombine(basePath, relativePath));
        Assert.Contains("Invalid path component", exception.Message);
    }

    /// <summary>
    ///     Test that SafePathCombine throws ArgumentException for path with double dots in middle.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_DoubleDotsInMiddle_ThrowsArgumentException()
    {
        // Arrange: setup base path and path with double dots in middle for traversal attempt
        var basePath = "/home/user/project";
        var relativePath = "subfolder/../../../etc/passwd";

        // Act & Assert: attempt path traversal in middle and verify ArgumentException is thrown
        var exception = Assert.Throws<ArgumentException>(() =>
            PathHelpers.SafePathCombine(basePath, relativePath));
        Assert.Contains("Invalid path component", exception.Message);
    }

    /// <summary>
    ///     Test that SafePathCombine throws ArgumentException for absolute paths.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_AbsolutePath_ThrowsArgumentException()
    {
        // Arrange & Act & Assert: test Unix absolute path rejection
        var unixBasePath = "/home/user/project";
        var unixRelativePath = "/etc/passwd";
        var unixException = Assert.Throws<ArgumentException>(() =>
            PathHelpers.SafePathCombine(unixBasePath, unixRelativePath));
        Assert.Contains("Invalid path component", unixException.Message);

        // Arrange & Act & Assert: test Windows absolute path rejection (only on Windows)
        if (OperatingSystem.IsWindows())
        {
            var windowsBasePath = "C:\\Users\\project";
            var windowsRelativePath = "C:\\Windows\\System32\\file.txt";
            var windowsException = Assert.Throws<ArgumentException>(() =>
                PathHelpers.SafePathCombine(windowsBasePath, windowsRelativePath));
            Assert.Contains("Invalid path component", windowsException.Message);
        }
    }

    /// <summary>
    ///     Test that SafePathCombine correctly handles current directory reference.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_CurrentDirectoryReference_CombinesCorrectly()
    {
        // Arrange: setup base path and current directory reference for testing
        var basePath = "/home/user/project";
        var relativePath = "./subfolder/file.txt";

        // Act: execute the operation being tested
        var result = PathHelpers.SafePathCombine(basePath, relativePath);

        // Assert: verify expected behavior
        Assert.Equal(Path.Combine(basePath, relativePath), result);
    }

    /// <summary>
    ///     Test that SafePathCombine correctly handles nested paths.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_NestedPaths_CombinesCorrectly()
    {
        // Arrange: setup base path and deeply nested relative path
        var basePath = "/home/user/project";
        var relativePath = "level1/level2/level3/file.txt";

        // Act: execute the operation being tested
        var result = PathHelpers.SafePathCombine(basePath, relativePath);

        // Assert: verify expected behavior
        Assert.Equal(Path.Combine(basePath, relativePath), result);
    }

    /// <summary>
    ///     Test that SafePathCombine correctly handles empty relative path.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_EmptyRelativePath_ReturnsBasePath()
    {
        // Arrange: setup base path and empty relative path for testing edge case
        var basePath = "/home/user/project";
        var relativePath = "";

        // Act: execute the operation being tested
        var result = PathHelpers.SafePathCombine(basePath, relativePath);

        // Assert: verify expected behavior
        Assert.Equal(Path.Combine(basePath, relativePath), result);
    }

    /// <summary>
    ///     Test that SafePathCombine correctly handles paths with segments beginning with double dots (not traversal).
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_DotDotPrefixedName_CombinesCorrectly()
    {
        // Arrange: setup base path and directory name starting with double dots (not traversal)
        var basePath = "/home/user/project";
        var relativePath = "..data/file.txt";

        // Act: execute the operation being tested
        var result = PathHelpers.SafePathCombine(basePath, relativePath);

        // Assert: verify expected behavior
        Assert.Equal(Path.Combine(basePath, relativePath), result);
    }

    /// <summary>
    ///     Test that SafePathCombine throws ArgumentNullException when basePath is null.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_NullBasePath_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert: null basePath throws ArgumentNullException
        Assert.Throws<ArgumentNullException>(() =>
            PathHelpers.SafePathCombine(null!, "file.txt"));
    }

    /// <summary>
    ///     Test that SafePathCombine throws ArgumentNullException when relativePath is null.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_NullRelativePath_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert: null relativePath throws ArgumentNullException
        Assert.Throws<ArgumentNullException>(() =>
            PathHelpers.SafePathCombine("/home/user", null!));
    }
}
