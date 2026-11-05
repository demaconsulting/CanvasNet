using DemaConsulting.CanvasNet.Render3D.SceneModel;

namespace DemaConsulting.CanvasNet.Scad.Tests;

/// <summary>
///     Unit tests of <see cref="ScadAnchor"/>: the value semantics of the record that
///     <see cref="ScadRenderOptions.AnchorHandler"/> delivers.
/// </summary>
public class ScadAnchorTests
{
    /// <summary>
    ///     Validates that the record keeps what it is given and compares by value.
    /// </summary>
    [Fact]
    public void ScadAnchor_Constructor_KeepsTheValuesAndComparesByValue()
    {
        // Arrange
        var location = new ScadSourceLocation("m.scad", 3, 5);

        // Act
        var anchor = new ScadAnchor("tip", new Vector3D(1, 2, 3), new Vector2D(10.5, 20.25), true, location);
        var same = new ScadAnchor("tip", new Vector3D(1, 2, 3), new Vector2D(10.5, 20.25), true, location);
        var other = anchor with { IsInsideImage = false };

        // Assert
        Assert.Equal("tip", anchor.Name);
        Assert.Equal(new Vector3D(1, 2, 3), anchor.Point);
        Assert.Equal(new Vector2D(10.5, 20.25), anchor.Pixel);
        Assert.True(anchor.IsInsideImage);
        Assert.Equal(location, anchor.Location);
        Assert.Equal(same, anchor);
        Assert.NotEqual(other, anchor);
    }

    /// <summary>
    ///     Validates that a point that cannot be projected is a null pixel.
    /// </summary>
    [Fact]
    public void ScadAnchor_Pixel_CanBeAbsent()
    {
        // Arrange / Act
        var anchor = new ScadAnchor("behind", Vector3D.Zero, null, false, default);

        // Assert
        Assert.Null(anchor.Pixel);
        Assert.False(anchor.IsInsideImage);
    }

    /// <summary>
    ///     Validates that the anchor handler of the render options is absent by default.
    /// </summary>
    [Fact]
    public void ScadAnchor_RenderOptionsHandler_IsNullByDefault()
    {
        // Arrange / Act
        var options = new ScadRenderOptions();

        // Assert
        Assert.Null(options.AnchorHandler);
    }
}
