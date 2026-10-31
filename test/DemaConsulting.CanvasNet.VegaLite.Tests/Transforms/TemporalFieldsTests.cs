using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Tests of <see cref="TemporalFields"/>, the collector of the fields that are date-parsed implicitly: the encoding
///     part (types, time units, format types, secondary channels, conditions and arrays of definitions).
/// </summary>
public class TemporalFieldsTests
{
    /// <summary>
    ///     Collects the temporal fields of a view given its encoding.
    /// </summary>
    /// <param name="encodingJson">The JSON of the encoding object.</param>
    /// <returns>The canonical field names, sorted.</returns>
    private static string[] Collect(string encodingJson) =>
        [.. TemporalFields.Collect(SpecParser.Parse("{\"data\":{\"values\":[]},\"mark\":\"point\",\"encoding\":" + encodingJson + "}").Root).OrderBy(f => f, StringComparer.Ordinal)];

    /// <summary>
    ///     Validates that a view without an encoding or without a temporal field has none.
    /// </summary>
    [Fact]
    public void TemporalFields_Collect_NothingWithoutTemporalFields()
    {
        // Arrange / Act / Assert
        Assert.Empty(TemporalFields.Collect(SpecParser.Parse("{\"data\":{\"values\":[]},\"mark\":\"point\"}").Root));
        Assert.Empty(Collect("{\"x\":{\"field\":\"a\",\"type\":\"quantitative\"},\"y\":{\"field\":\"b\",\"type\":\"nominal\"},\"size\":{\"aggregate\":\"count\"}}"));
    }

    /// <summary>
    ///     Validates the three ways a field is read as dates: the type, a time unit and the format type <c>time</c>
    ///     (on the definition, its axis or its legend).
    /// </summary>
    [Fact]
    public void TemporalFields_Collect_TypeTimeUnitAndFormatType()
    {
        // Arrange / Act
        var fields = Collect(
            """
            { "x": { "field": "t", "type": "temporal" },
              "y": { "field": "u", "type": "ordinal", "timeUnit": "month" },
              "color": { "field": "v", "type": "quantitative", "legend": { "formatType": "time" } },
              "size": { "field": "w", "type": "quantitative", "axis": { "formatType": "time" } },
              "opacity": { "field": "z", "type": "quantitative", "formatType": "time" },
              "shape": { "field": "n", "type": "nominal", "timeUnit": null },
              "stroke": { "field": "q", "type": "quantitative", "formatType": "number" } }
            """);

        // Assert
        Assert.Equal(["t", "u", "v", "w", "z"], fields);
    }

    /// <summary>
    ///     Validates that a secondary channel takes the type of its main channel, that the field of a condition is judged by
    ///     the condition itself, that arrays of definitions are read and that names are canonical.
    /// </summary>
    [Fact]
    public void TemporalFields_Collect_SecondaryChannelsConditionsArraysAndNames()
    {
        // Arrange / Act
        var fields = Collect(
            """
            { "x": { "field": "start", "type": "temporal" }, "x2": { "field": "end" },
              "y": { "field": "lo", "type": "quantitative" }, "y2": { "field": "hi" },
              "color": { "condition": { "test": "true", "field": "c1", "type": "temporal" }, "value": "red" },
              "detail": [ { "field": "a.b", "type": "temporal" }, { "field": "plain", "type": "nominal" } ],
              "tooltip": { "field": "no\\.dot", "type": "temporal" } }
            """);

        // Assert: end follows the temporal x, hi follows the quantitative y; the dotted names are canonical
        Assert.Equal(["a.b", "c1", "end", "no\\.dot", "start"], fields);
    }

    /// <summary>
    ///     Validates that definitions without a field name (a value, a datum, a count) add nothing.
    /// </summary>
    [Fact]
    public void TemporalFields_Collect_IgnoresDefinitionsWithoutAFieldName()
    {
        // Arrange / Act
        var fields = Collect("{\"x\":{\"datum\":\"2024-01-01\",\"type\":\"temporal\"},\"y\":{\"value\":3},\"size\":{\"aggregate\":\"count\",\"type\":\"temporal\"}}");

        // Assert
        Assert.Empty(fields);
    }

    /// <summary>
    ///     Validates the argument check.
    /// </summary>
    [Fact]
    public void TemporalFields_Collect_RejectsNull() => Assert.Throws<ArgumentNullException>(() => TemporalFields.Collect(null!));
}
