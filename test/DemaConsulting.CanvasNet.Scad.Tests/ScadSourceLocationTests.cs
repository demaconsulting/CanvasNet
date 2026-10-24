namespace DemaConsulting.CanvasNet.Scad.Tests;

/// <summary>
///     Unit tests for <see cref="ScadSourceLocation"/>.
/// </summary>
public class ScadSourceLocationTests
{
    /// <summary>
    ///     Validates that two locations with identical components are equal and hash identically.
    /// </summary>
    [Fact]
    public void ScadSourceLocation_Equality_SameComponents_AreEqual()
    {
        // Arrange: two locations built from the same components and one that differs
        var a = new ScadSourceLocation("a.scad", 3, 7);
        var b = new ScadSourceLocation("a.scad", 3, 7);
        var other = new ScadSourceLocation("a.scad", 3, 8);

        // Act / Assert: value equality, hash equality and inequality
        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, other);
        Assert.True(a != other);
    }

    /// <summary>
    ///     Validates the <see cref="ScadSourceLocation.ToString"/> forms with and without a file.
    /// </summary>
    [Fact]
    public void ScadSourceLocation_ToString_WithAndWithoutFile_FormatsCoordinates()
    {
        // Arrange: a named and an unnamed location
        var named = new ScadSourceLocation("dir/part.scad", 12, 5);
        var unnamed = new ScadSourceLocation(null, 12, 5);

        // Act / Assert: the file prefix appears only when a file is known
        Assert.Equal("dir/part.scad:12:5", named.ToString());
        Assert.Equal("12:5", unnamed.ToString());
    }

    /// <summary>
    ///     Validates that the default value is the "unknown" location.
    /// </summary>
    [Fact]
    public void ScadSourceLocation_Default_IsUnknown()
    {
        // Arrange: the default location
        var location = default(ScadSourceLocation);

        // Act / Assert: line zero, no file, and a placeholder rendering
        Assert.Equal(0, location.Line);
        Assert.Equal(0, location.Column);
        Assert.Null(location.File);
        Assert.Equal("?", location.ToString());
    }
}
