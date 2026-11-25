using DemaConsulting.CanvasNet.MathMl.Syntax;

namespace DemaConsulting.CanvasNet.MathMl.Tests.Syntax;

/// <summary>
///     Unit tests for the equation tree (<see cref="MathElement"/>, <see cref="MathText"/>,
///     <see cref="MathAttribute"/>): its accessors.
/// </summary>
public class MathTreeTests
{
    /// <summary>
    ///     Validates the element children, the text content and the attribute lookup of an element.
    /// </summary>
    [Fact]
    public void MathTree_Element_ExposesChildrenTextAndAttributes()
    {
        // Arrange
        var location = new MathMlLocation(null, 1, 2);
        var mi = new MathElement("mi", new Dictionary<string, MathAttribute>(), [new MathText("x", location)], location);
        var mrow = new MathElement(
            "mrow",
            new Dictionary<string, MathAttribute> { ["mathcolor"] = new MathAttribute("red", location) },
            [new MathText("loose", location), mi],
            location);

        // Act / Assert
        Assert.Equal([mi], mrow.ElementChildren);
        Assert.Equal("loose", mrow.TextContent);
        Assert.Equal("x", mi.TextContent);
        Assert.Equal("red", mrow.GetAttribute("mathcolor"));
        Assert.Null(mrow.GetAttribute("mathbackground"));
    }

    /// <summary>
    ///     Validates which elements are text tokens.
    /// </summary>
    [Fact]
    public void MathTree_IsTextTokenName_KnowsTheTokenElements()
    {
        // Act / Assert
        Assert.All(["mi", "mn", "mo", "mtext", "ms"], name => Assert.True(MathElement.IsTextTokenName(name)));
        Assert.All(["mspace", "mrow", "math", "MI"], name => Assert.False(MathElement.IsTextTokenName(name)));
    }

    /// <summary>
    ///     Validates that text runs and attribute values are values: equal content and location compare equal.
    /// </summary>
    [Fact]
    public void MathTree_TextAndAttribute_HaveValueEquality()
    {
        // Arrange
        var location = new MathMlLocation("a.mml", 3, 4);

        // Act / Assert
        Assert.Equal(new MathText("x", location), new MathText("x", location));
        Assert.NotEqual(new MathText("x", location), new MathText("x", default));
        Assert.Equal(new MathAttribute("1em", location), new MathAttribute("1em", location));
    }
}
