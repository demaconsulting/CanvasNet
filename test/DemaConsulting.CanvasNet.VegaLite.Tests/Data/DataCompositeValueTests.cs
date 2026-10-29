using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Data;

/// <summary>
///     Tests of the immutable composite values (arrays, objects and regular expressions) that the expression engine
///     adds to the scalar value model, and of the guarantee that the scalar behavior is unchanged.
/// </summary>
public class DataCompositeValueTests
{
    /// <summary>
    ///     Validates that an array value copies its input, keeps its order, has the JavaScript text form and can hold
    ///     composites.
    /// </summary>
    [Fact]
    public void DataValue_FromArray_CopiesElementsAndFormatsLikeJavaScript()
    {
        // Arrange
        var source = new List<DataValue> { DataValue.FromNumber(1), DataValue.Null, DataValue.FromString("a") };

        // Act
        var array = DataValue.FromArray(source);
        source[0] = DataValue.FromNumber(99);
        var nested = DataValue.FromArray([array, DataValue.FromBoolean(true)]);

        // Assert: copy, kind, elements and text
        Assert.Equal(DataValueKind.Array, array.Kind);
        Assert.True(array.IsArray);
        Assert.Equal(3, array.Elements.Length);
        Assert.Equal(1, array.Elements[0].Number);
        Assert.Equal("1,,a", array.ToText());
        Assert.Equal("1,,a,true", nested.ToText());
        Assert.Equal(0, DataValue.FromArray([]).Elements.Length);
        Assert.Equal(string.Empty, DataValue.FromArray([]).ToText());
        Assert.Throws<ArgumentNullException>(() => DataValue.FromArray(null!));
        Assert.Equal(0, DataValue.FromNumber(1).Elements.Length);
        Assert.Null(DataValue.FromNumber(1).ObjectValue);
        Assert.Null(DataValue.FromNumber(1).RegExpPayload);
    }

    /// <summary>
    ///     Validates that composite values are equal only to themselves, even when they are empty.
    /// </summary>
    [Fact]
    public void DataValue_Composites_AreEqualOnlyToThemselves()
    {
        // Arrange
        var first = DataValue.FromArray([DataValue.FromNumber(1)]);
        var second = DataValue.FromArray([DataValue.FromNumber(1)]);
        var emptyA = DataValue.FromArray([]);
        var emptyB = DataValue.FromArray([]);
        var objectA = DataValue.FromObject(DataObject.Create([]));
        var objectB = DataValue.FromObject(DataObject.Create([]));
        var payload = new object();

        // Act / Assert: identity, not structure
        Assert.True(first.Equals(first));
        Assert.Equal(first.GetHashCode(), first.GetHashCode());
        Assert.False(first.Equals(second));
        Assert.False(emptyA.Equals(emptyB));
        Assert.True(first != second);
        Assert.False(objectA.Equals(objectB));
        var alias = objectA;
        Assert.True(objectA == alias);
        Assert.True(DataValue.FromRegExp(payload) == DataValue.FromRegExp(payload));
        Assert.False(DataValue.FromRegExp(payload).Equals(DataValue.FromRegExp(new object())));
        Assert.False(first.Equals(objectA));
        Assert.False(first.Equals((object?)DataValue.FromString("1")));
    }

    /// <summary>
    ///     Validates the object value: insertion order, a repeated key, lookup with few and many members, and the text.
    /// </summary>
    [Fact]
    public void DataObject_Create_KeepsInsertionOrderAndLastValueOfARepeatedKey()
    {
        // Arrange
        var members = new List<KeyValuePair<string, DataValue>>
        {
            new("b", DataValue.FromNumber(1)),
            new("a", DataValue.FromNumber(2)),
            new("b", DataValue.FromNumber(3)),
        };

        // Act
        var small = DataObject.Create(members);
        members.Clear();
        var many = DataObject.Create(Enumerable.Range(0, 20).Select(i => new KeyValuePair<string, DataValue>("k" + i, DataValue.FromNumber(i))));

        // Assert: order and values of a small object (linear lookup)
        Assert.Equal(2, small.Count);
        Assert.Equal(["b", "a"], small.Keys);
        Assert.Equal(3, small.ValueAt(0).Number);
        Assert.True(small.TryGetValue("a", out var a));
        Assert.Equal(2, a.Number);
        Assert.False(small.TryGetValue("missing", out var missing));
        Assert.True(missing.IsNull);
        Assert.False(small.TryGetValue("A", out _));

        // Assert: a large object uses its index with the same results
        Assert.Equal(20, many.Count);
        Assert.True(many.TryGetValue("k17", out var k17));
        Assert.Equal(17, k17.Number);
        Assert.False(many.TryGetValue("k20", out _));

        // Assert: argument checks and the value wrapper
        Assert.Throws<ArgumentOutOfRangeException>(() => small.ValueAt(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => small.ValueAt(-1));
        Assert.Throws<ArgumentNullException>(() => DataObject.Create(null!));
        Assert.Throws<ArgumentNullException>(() => small.TryGetValue(null!, out _));
        var value = DataValue.FromObject(small);
        Assert.True(value.IsObject);
        Assert.Same(small, value.ObjectValue);
        Assert.Equal("[object Object]", value.ToText());
        Assert.Throws<ArgumentNullException>(() => DataValue.FromObject(null!));
    }

    /// <summary>
    ///     Validates the regular expression value: it carries an opaque payload and its text is the payload's text.
    /// </summary>
    [Fact]
    public void DataValue_FromRegExp_CarriesOpaquePayload()
    {
        // Arrange
        var payload = new PayloadWithText("/a+/g");

        // Act
        var value = DataValue.FromRegExp(payload);

        // Assert
        Assert.True(value.IsRegExp);
        Assert.Same(payload, value.RegExpPayload);
        Assert.Equal("/a+/g", value.ToText());
        Assert.Throws<ArgumentNullException>(() => DataValue.FromRegExp(null!));
    }

    /// <summary>
    ///     Validates that the existing scalar kinds keep their order and that the composites sort after them.
    /// </summary>
    [Fact]
    public void DataValue_Compare_ScalarOrderIsUnchangedAndCompositesSortLast()
    {
        // Arrange: one value of every scalar kind in the documented order
        DataValue[] scalars =
        [
            DataValue.Null,
            DataValue.FromBoolean(false),
            DataValue.FromNumber(5),
            DataValue.FromDate(0),
            DataValue.FromString("a"),
        ];
        var array = DataValue.FromArray([]);

        // Act / Assert: the scalar kinds keep their numeric order and a composite follows every scalar
        for (var i = 0; i < scalars.Length - 1; i++)
        {
            Assert.True(DataValue.Compare(scalars[i], scalars[i + 1]) < 0);
        }

        Assert.True(DataValue.Compare(scalars[^1], array) < 0);
        Assert.True(DataValue.Compare(array, DataValue.FromObject(DataObject.Create([]))) < 0);
        Assert.Equal(0, DataValue.Compare(array, DataValue.FromArray([DataValue.FromNumber(1)])));
        Assert.Equal(
            [DataValueKind.Null, DataValueKind.Boolean, DataValueKind.Number, DataValueKind.Date, DataValueKind.String],
            scalars.Select(v => v.Kind).ToArray());
        Assert.Equal(5, (int)DataValueKind.Array);
    }

    /// <summary>
    ///     A payload with a fixed text form.
    /// </summary>
    /// <param name="text">The text.</param>
    private sealed class PayloadWithText(string text)
    {
        /// <inheritdoc/>
        public override string ToString() => text;
    }
}
