using DemaConsulting.CanvasNet.Mermaid.Rendering;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Rendering;

/// <summary>
///     Unit tests of <see cref="ShapeNames"/>, the registry of Mermaid's node shapes.
/// </summary>
public class ShapeNamesTests
{
    /// <summary>
    ///     Validates that the registry has Mermaid's 53 documented shapes, each known by its short name.
    /// </summary>
    [Fact]
    public void ShapeNames_ShortNames_AreTheFiftyThreeDocumentedShapes()
    {
        // Act
        var names = ShapeNames.ShortNames;

        // Assert
        Assert.Equal(53, ShapeNames.DocumentedCount);
        Assert.Equal(53, names.Distinct(StringComparer.Ordinal).Count());
        Assert.All(names, name => Assert.True(ShapeNames.IsKnown(name), name));
        Assert.Equal("rect", names[0]);
        Assert.Equal("lin-doc", names[^1]);
    }

    /// <summary>
    ///     Validates that aliases and internal aliases name the drawing function of their shape.
    /// </summary>
    /// <param name="name">The shape name.</param>
    /// <param name="handler">The drawing function.</param>
    [Theory]
    [InlineData("rect", "squareRect")]
    [InlineData("proc", "squareRect")]
    [InlineData("squareRect", "squareRect")]
    [InlineData("diamond", "question")]
    [InlineData("lean_right", "lean_right")]
    [InlineData("in-out", "lean_right")]
    [InlineData("doublecircle", "doublecircle")]
    [InlineData("odd", "rect_left_inv_arrow")]
    [InlineData("collapsedGroup", "collapsedGroup")]
    [InlineData("imageSquare", "imageSquare")]
    public void ShapeNames_HandlerOf_Alias_GivesItsShape(string name, string handler)
    {
        // Act
        var result = ShapeNames.HandlerOf(name);

        // Assert
        Assert.Equal(handler, result);
    }

    /// <summary>
    ///     Validates that unknown names are not shapes and get no drawing function; the check is case-sensitive.
    /// </summary>
    [Fact]
    public void ShapeNames_IsKnown_UnknownOrWrongCase_IsFalse()
    {
        // Act and Assert
        Assert.False(ShapeNames.IsKnown("Circle"));
        Assert.False(ShapeNames.IsKnown("ellipse"));
        Assert.Null(ShapeNames.HandlerOf("nope"));
    }

    /// <summary>
    ///     Validates the suggestion for a misspelled name: the nearest name the flowchart accepts.
    /// </summary>
    /// <param name="name">The name as written.</param>
    /// <param name="nearest">The suggestion.</param>
    [Theory]
    [InlineData("rectangles", "rectangle")]
    [InlineData("Circle", "circle")]
    [InlineData("lean_right", "lean-right")]
    [InlineData("hexagons", "hexagon")]
    public void ShapeNames_Nearest_Typo_SuggestsAcceptedName(string name, string nearest)
    {
        // Act
        var result = ShapeNames.Nearest(name);

        // Assert
        Assert.Equal(nearest, result);
    }
}
