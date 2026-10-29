namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     Unit tests for <see cref="VegaLiteLocation"/>.
/// </summary>
public class VegaLiteLocationTests
{
    /// <summary>
    ///     Validates the full format with file, coordinates and pointer.
    /// </summary>
    [Fact]
    public void VegaLiteLocation_ToString_FileCoordinatesAndPointer_FormatsAllParts()
    {
        // Arrange
        var location = new VegaLiteLocation("spec.vl.json", "/layer/1/encoding/x", 3, 7);

        // Act
        var text = location.ToString();

        // Assert
        Assert.Equal("spec.vl.json:3:7 /layer/1/encoding/x", text);
    }

    /// <summary>
    ///     Validates that an unnamed source omits the file part.
    /// </summary>
    [Fact]
    public void VegaLiteLocation_ToString_NoFile_OmitsFile()
    {
        // Arrange
        var location = new VegaLiteLocation(null, string.Empty, 12, 4);

        // Act
        var text = location.ToString();

        // Assert
        Assert.Equal("12:4", text);
    }

    /// <summary>
    ///     Validates that an unknown position prints a placeholder and still shows the pointer.
    /// </summary>
    [Fact]
    public void VegaLiteLocation_ToString_UnknownPosition_PrintsPlaceholderAndPointer()
    {
        // Arrange
        var location = new VegaLiteLocation("a.json", "/mark", 0, 0);

        // Act
        var text = location.ToString();

        // Assert
        Assert.Equal("a.json:? /mark", text);
    }

    /// <summary>
    ///     Validates that the default value is unknown and tolerates the null pointer of a default struct.
    /// </summary>
    [Fact]
    public void VegaLiteLocation_Default_IsUnknown()
    {
        // Arrange
        var location = default(VegaLiteLocation);

        // Act / Assert
        Assert.False(location.IsKnown);
        Assert.Equal("?", location.ToString());
    }

    /// <summary>
    ///     Validates that a pointer alone makes a location known.
    /// </summary>
    [Fact]
    public void VegaLiteLocation_IsKnown_PointerOnly_IsTrue()
    {
        // Arrange
        var location = new VegaLiteLocation(null, "/data", 0, 0);

        // Act / Assert
        Assert.True(location.IsKnown);
    }

    /// <summary>
    ///     Validates value equality of the record struct.
    /// </summary>
    [Fact]
    public void VegaLiteLocation_Equality_SameValues_AreEqual()
    {
        // Arrange
        var a = new VegaLiteLocation("f", "/x", 1, 2);
        var b = new VegaLiteLocation("f", "/x", 1, 2);

        // Act / Assert
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, new VegaLiteLocation("f", "/x", 1, 3));
    }
}
