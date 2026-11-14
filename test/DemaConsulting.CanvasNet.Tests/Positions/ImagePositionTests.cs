using DemaConsulting.CanvasNet.Positions;

namespace DemaConsulting.CanvasNet.Tests.Positions;

/// <summary>
///     Unit tests for <see cref="ImagePosition"/>, <see cref="PositionBox"/>, <see cref="PositionPoint"/> and
///     <see cref="PositionValue"/>: validation, immutable value snapshots and typed equality.
/// </summary>
public class ImagePositionTests
{
    /// <summary>
    ///     Test that a position keeps its kind, id and facts.
    /// </summary>
    [Fact]
    public void ImagePosition_Constructor_ValidKindAndId_KeepsEveryFact()
    {
        // Act
        var position = new ImagePosition("node", "send")
        {
            Text = "Send",
            Line = 10,
            From = "a",
            To = "b",
            Index = 0,
            Box = new PositionBox(1, 2, 3, 4),
            Pixel = new PositionPoint(2.5, 4)
        };

        // Assert
        Assert.Equal("node", position.Kind);
        Assert.Equal("send", position.Id);
        Assert.Equal("Send", position.Text);
        Assert.Equal(10, position.Line);
        Assert.Equal(0, position.Index);
        Assert.Equal(new PositionBox(1, 2, 3, 4), position.Box);
        Assert.Equal(new PositionPoint(2.5, 4), position.Pixel);
        Assert.Empty(position.Values);
    }

    /// <summary>
    ///     Test that a kind that is not a lowercase word is refused.
    /// </summary>
    /// <param name="kind">The kind.</param>
    [Theory]
    [InlineData("")]
    [InlineData("Node")]
    [InlineData("1node")]
    [InlineData("node id")]
    [InlineData("node\n")]
    public void ImagePosition_Constructor_BadKind_Throws(string kind)
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() => new ImagePosition(kind, "a"));
    }

    /// <summary>
    ///     Test that a null id, a line below 1, a negative index and non-finite geometry are refused.
    /// </summary>
    [Fact]
    public void ImagePosition_Properties_InvalidValues_Throw()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => new ImagePosition("node", null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ImagePosition("node", "a") { Line = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new ImagePosition("edge", "a") { Index = -1 });
        Assert.Throws<ArgumentException>(() => new ImagePosition("node", "a") { Box = new PositionBox(0, 0, -1, 1) });
        Assert.Throws<ArgumentException>(() => new ImagePosition("node", "a") { Box = new PositionBox(double.NaN, 0, 1, 1) });
        Assert.Throws<ArgumentException>(() => new ImagePosition("node", "a") { Pixel = new PositionPoint(double.PositiveInfinity, 0) });
    }

    /// <summary>
    ///     Test that the values are a sorted copy, so that changing the caller's list does not change the position.
    /// </summary>
    [Fact]
    public void ImagePosition_Values_CallerListChanges_PositionKeepsSortedCopy()
    {
        // Arrange
        var values = new List<KeyValuePair<string, PositionValue>>
        {
            new("y", PositionValue.FromNumber(43)),
            new("x", PositionValue.FromString("C"))
        };

        // Act
        var position = new ImagePosition("mark", "bar-2") { Values = values };
        values.Clear();

        // Assert
        Assert.Equal(["x", "y"], position.Values.Select(pair => pair.Key));
        Assert.True(position.TryGetValue("y", out var y));
        Assert.Equal(PositionValue.FromNumber(43), y);
        Assert.False(position.TryGetValue("Y", out _));
    }

    /// <summary>
    ///     Test that a duplicate or empty value name is refused.
    /// </summary>
    [Fact]
    public void ImagePosition_Values_DuplicateOrEmptyName_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() => new ImagePosition("mark", "a")
        {
            Values = [new("x", PositionValue.Null), new("x", PositionValue.FromNumber(1))]
        });
        Assert.Throws<ArgumentException>(() => new ImagePosition("mark", "a") { Values = [new("", PositionValue.Null)] });
    }

    /// <summary>
    ///     Test the typed equality of values: 3 equals 3.0, not "3"; null equals null only; text is case-sensitive.
    /// </summary>
    [Fact]
    public void PositionValue_Equals_TypedEquality_ComparesKindAndValue()
    {
        // Assert
        Assert.Equal(PositionValue.FromNumber(3), PositionValue.FromNumber(3.0));
        Assert.NotEqual(PositionValue.FromNumber(3), PositionValue.FromString("3"));
        Assert.Equal(PositionValue.Null, default);
        Assert.NotEqual(PositionValue.Null, PositionValue.FromString("null"));
        Assert.NotEqual(PositionValue.FromString("Nov"), PositionValue.FromString("nov"));
        Assert.NotEqual(PositionValue.FromBoolean(true), PositionValue.FromNumber(1));
        Assert.Equal(PositionValue.FromNumber(0), PositionValue.FromNumber(-0.0));
        Assert.Equal(PositionValue.FromNumber(0).GetHashCode(), PositionValue.FromNumber(-0.0).GetHashCode());
        Assert.True(PositionValue.FromNumber(1.5) == PositionValue.FromNumber(1.5));
        Assert.True(PositionValue.FromNumber(1.5) != PositionValue.FromNumber(2));
    }

    /// <summary>
    ///     Test that non-finite numbers are refused and that the text form is invariant.
    /// </summary>
    [Fact]
    public void PositionValue_FromNumber_NonFinite_ThrowsAndTextIsInvariant()
    {
        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => PositionValue.FromNumber(double.NaN));
        Assert.Equal("1.5", PositionValue.FromNumber(1.5).ToString());
        Assert.Equal("true", PositionValue.FromBoolean(true).ToString());
        Assert.Equal("null", PositionValue.Null.ToString());
        Assert.Equal("a b", PositionValue.FromString("a b").ToString());
    }

    /// <summary>
    ///     Test the conversion of plain scalars and the refusal of lists.
    /// </summary>
    [Fact]
    public void PositionValue_TryFromObject_Scalars_ConvertAndListsAreRefused()
    {
        // Act / Assert
        Assert.True(PositionValue.TryFromObject(3.0, out var number));
        Assert.Equal(PositionValueKind.Number, number.Kind);
        Assert.True(PositionValue.TryFromObject(7, out var integer));
        Assert.Equal(7, integer.NumberValue);
        Assert.True(PositionValue.TryFromObject(null, out var none));
        Assert.Equal(PositionValueKind.Null, none.Kind);
        Assert.True(PositionValue.TryFromObject("x", out var text));
        Assert.Equal("x", text.StringValue);
        Assert.True(PositionValue.TryFromObject(false, out var flag));
        Assert.False(flag.BooleanValue);
        Assert.False(PositionValue.TryFromObject(new List<object?>(), out _));
        Assert.False(PositionValue.TryFromObject(double.NaN, out _));
    }

    /// <summary>
    ///     Test that a box gives its center and corners and encloses points.
    /// </summary>
    [Fact]
    public void PositionBox_EncloseAndCenter_ComputesBounds()
    {
        // Arrange
        var box = new PositionBox(10, 20, 30, 40);

        // Act
        var enclosed = PositionBox.Enclose([new PositionPoint(5, 9), new PositionPoint(-1, 12), new PositionPoint(3, 2)]);

        // Assert
        Assert.Equal(new PositionPoint(25, 40), box.Center);
        Assert.Equal(new PositionPoint(40, 60), box.Corners()[2]);
        Assert.Equal(new PositionBox(-1, 2, 6, 10), enclosed);
        Assert.Throws<ArgumentException>(() => PositionBox.Enclose([]));
    }
}
