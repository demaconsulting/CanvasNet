using DemaConsulting.CanvasNet.Mermaid.Graphlib;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Graphlib;

/// <summary>
///     Unit tests of <see cref="ObjectKeyMap{TValue}"/>: the key order of a JavaScript object.
/// </summary>
public class ObjectKeyMapTests
{
    /// <summary>
    ///     Validates that array-index keys come first in numeric order, then the other keys in insertion order.
    /// </summary>
    [Fact]
    public void ObjectKeyMap_Keys_MixedKeys_IndexesFirstThenInsertionOrder()
    {
        // Arrange
        var map = new ObjectKeyMap<int>();
        foreach (var key in new[] { "b", "10", "a", "2", "07", "-1", "1.5", "4294967295", "4294967294", "0" })
        {
            map[key] = map.Count;
        }

        // Act
        var keys = map.Keys();

        // Assert
        Assert.Equal(["0", "2", "10", "4294967294", "b", "a", "07", "-1", "1.5", "4294967295"], keys);
        Assert.Equal([9, 3, 1, 8, 0, 2, 4, 5, 6, 7], map.Values());
    }

    /// <summary>
    ///     Validates that a removed key that is added again moves to the end, and that setting an existing key keeps its
    ///     place.
    /// </summary>
    [Fact]
    public void ObjectKeyMap_Remove_KeyAddedAgain_MovesToEnd()
    {
        // Arrange
        var map = new ObjectKeyMap<string>();
        map["x"] = "1";
        map["y"] = "2";
        map["z"] = "3";

        // Act
        Assert.True(map.Remove("x"));
        Assert.False(map.Remove("missing"));
        map["x"] = "4";
        map["y"] = "5";

        // Assert
        Assert.Equal(["y", "z", "x"], map.Keys());
        Assert.Equal(["5", "3", "4"], map.Values());
        Assert.True(map.TryGetValue("x", out var value) && value == "4");
        Assert.False(map.ContainsKey("w"));
    }

    /// <summary>
    ///     Validates the array-index rule on its edge cases.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="expected">Whether it is an array index.</param>
    [Theory]
    [InlineData("0", true)]
    [InlineData("42", true)]
    [InlineData("4294967294", true)]
    [InlineData("4294967295", false)]
    [InlineData("00", false)]
    [InlineData("01", false)]
    [InlineData("", false)]
    [InlineData("1e3", false)]
    [InlineData("+1", false)]
    [InlineData("12345678901", false)]
    public void ObjectKeyMap_IsArrayIndex_Key_MatchesJavaScript(string key, bool expected)
    {
        // Act
        var actual = ObjectKeyMap<int>.IsArrayIndex(key, out _);

        // Assert
        Assert.Equal(expected, actual);
    }
}
