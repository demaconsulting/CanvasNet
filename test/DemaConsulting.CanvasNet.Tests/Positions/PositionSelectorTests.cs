using System.Collections.Frozen;
using DemaConsulting.CanvasNet.Positions;

namespace DemaConsulting.CanvasNet.Tests.Positions;

/// <summary>
///     Unit tests for <see cref="PositionSelector"/> and <see cref="PositionSelection"/>: the selector grammar, the
///     case rules, globs, quoted ids and values, typed predicates, and the limits.
/// </summary>
public class PositionSelectorTests
{
    /// <summary>
    ///     A node with a label.
    /// </summary>
    private static readonly ImagePosition Node = new("node", "send") { Text = "Send" };

    /// <summary>
    ///     A node with an id that holds glob characters and a space.
    /// </summary>
    private static readonly ImagePosition OddNode = new("node", "my*odd id");

    /// <summary>
    ///     An edge between two nodes.
    /// </summary>
    private static readonly ImagePosition Edge = new("edge", "L_A_B_0") { From = "A", To = "B", Index = 0 };

    /// <summary>
    ///     A chart mark with values.
    /// </summary>
    private static readonly ImagePosition Mark = new("mark", "bar-2")
    {
        View = "hconcat[1]",
        Mark = "bar",
        Values =
        [
            new("x", PositionValue.FromString("Nov")),
            new("color", PositionValue.FromString("Americas")),
            new("y", PositionValue.FromNumber(3)),
            new("size", PositionValue.Null)
        ]
    };

    /// <summary>
    ///     Test the matching of every documented example selector.
    /// </summary>
    /// <param name="text">The selector.</param>
    /// <param name="node">Whether it matches <see cref="Node"/>.</param>
    /// <param name="oddNode">Whether it matches <see cref="OddNode"/>.</param>
    /// <param name="edge">Whether it matches <see cref="Edge"/>.</param>
    /// <param name="mark">Whether it matches <see cref="Mark"/>.</param>
    [Theory]
    [InlineData("all", true, true, true, true)]
    [InlineData("ALL", true, true, true, true)]
    [InlineData("node", true, true, false, false)]
    [InlineData("Node", true, true, false, false)]
    [InlineData("node:send", true, false, false, false)]
    [InlineData("node:Send", false, false, false, false)]
    [InlineData("node:s*", true, false, false, false)]
    [InlineData("node:se?d", true, false, false, false)]
    [InlineData("node:*", true, true, false, false)]
    [InlineData("node:my*", false, true, false, false)]
    [InlineData("node:\"my*odd id\"", false, true, false, false)]
    [InlineData("node:\"my*\"", false, false, false, false)]
    [InlineData("node:\"s\\u0065nd\"", true, false, false, false)]
    [InlineData("edge:@from=A+@to=B", false, false, true, false)]
    [InlineData("edge:@FROM=A+@To=B+@index=0", false, false, true, false)]
    [InlineData("edge:@from=A+@to=B+@index=1", false, false, false, false)]
    [InlineData("edge:@from=a", false, false, false, false)]
    [InlineData("edge:@id=L_A_B_0", false, false, true, false)]
    [InlineData("edge:@id=L_*", false, false, false, false)]
    [InlineData("mark:x=Nov+color=Americas", false, false, false, true)]
    [InlineData("mark:x=Nov+color=Europe", false, false, false, false)]
    [InlineData("mark:y=3", false, false, false, true)]
    [InlineData("mark:y=3.0", false, false, false, true)]
    [InlineData("mark:y=\"3\"", false, false, false, false)]
    [InlineData("mark:x=\"Nov\"", false, false, false, true)]
    [InlineData("mark:x='Nov'", false, false, false, true)]
    [InlineData("mark:size=null", false, false, false, true)]
    [InlineData("mark:shape=null", false, false, false, false)]
    [InlineData("mark:X=Nov", false, false, false, false)]
    [InlineData("mark:@view=\"hconcat[1]\"", false, false, false, true)]
    [InlineData("mark:@view=hconcat[1]", false, false, false, true)]
    [InlineData("node:@view=\"\"", false, false, false, false)]
    [InlineData("axis", false, false, false, false)]
    public void PositionSelector_Matches_Examples_MatchAsDocumented(string text, bool node, bool oddNode, bool edge, bool mark)
    {
        // Arrange
        var selector = PositionSelector.Parse(text);

        // Act / Assert
        Assert.Equal(node, selector.Matches(Node));
        Assert.Equal(oddNode, selector.Matches(OddNode));
        Assert.Equal(edge, selector.Matches(Edge));
        Assert.Equal(mark, selector.Matches(Mark));
    }

    /// <summary>
    ///     Test that a '+' or '=' inside a quoted value is part of the value.
    /// </summary>
    [Fact]
    public void PositionSelector_Matches_PlusAndEqualsInsideQuotes_ArePartOfTheValue()
    {
        // Arrange
        var position = new ImagePosition("mark", "text-0") { Values = [new("text", PositionValue.FromString("a+b=c"))] };

        // Act
        var selector = PositionSelector.Parse("mark:text=\"a+b=c\"");

        // Assert
        Assert.True(selector.Matches(position));
    }

    /// <summary>
    ///     Test that a predicate name that is a channel (in the channel names or in the values) compares with the
    ///     values, and any other name with the data fields.
    /// </summary>
    /// <param name="text">The selector.</param>
    /// <param name="chart">Whether it matches the chart mark (with channel names).</param>
    /// <param name="slice">Whether it matches the slice (without channel names).</param>
    [Theory]
    [InlineData("mark:month=2025-11-01+region=Americas", true, false)]
    [InlineData("mark:month=2025-11-01+color=Americas", true, false)]
    [InlineData("mark:x=2025-11-01+color=Americas", false, false)]
    [InlineData("mark:x=\"2012-11-01T00:00:00.000Z\"", true, false)]
    [InlineData("mark:revenue=6.8", true, false)]
    [InlineData("mark:revenue=\"6.8\"", false, false)]
    [InlineData("mark:time=t1", false, false)]
    [InlineData("mark:shape=circle", false, false)]
    [InlineData("mark:value=3", false, true)]
    [InlineData("mark:label=A", false, true)]
    [InlineData("mark:value=3+label=A", false, true)]
    [InlineData("mark:missing=null", false, false)]
    public void PositionSelector_Matches_ChannelOrDataField_FollowsTheChannelNames(string text, bool chart, bool slice)
    {
        // Arrange: a time-unit chart point whose data has a field named like a channel, and a slice without channel names
        var point = new ImagePosition("mark", "line-1")
        {
            Values =
            [
                new("color", PositionValue.FromString("Americas")),
                new("x", PositionValue.FromString("2012-11-01T00:00:00.000Z")),
                new("y", PositionValue.FromNumber(6.8))
            ],
            Data =
            [
                new("month", PositionValue.FromString("2025-11-01")),
                new("region", PositionValue.FromString("Americas")),
                new("revenue", PositionValue.FromNumber(6.8)),
                new("time", PositionValue.FromString("t1")),
                new("shape", PositionValue.FromString("circle"))
            ],
            ChannelNames = new[] { "x", "y", "color", "time", "shape" }.ToFrozenSet(StringComparer.Ordinal)
        };
        var piece = new ImagePosition("mark", "slice-0")
        {
            Values = [new("value", PositionValue.FromNumber(3))],
            Data = [new("label", PositionValue.FromString("A")), new("value", PositionValue.FromNumber(4))]
        };

        // Act
        var selector = PositionSelector.Parse(text);

        // Assert
        Assert.Equal(chart, selector.Matches(point));
        Assert.Equal(slice, selector.Matches(piece));
    }

    /// <summary>
    ///     Test that every malformed selector is refused with a message that names it.
    /// </summary>
    /// <param name="text">The selector.</param>
    [Theory]
    [InlineData("")]
    [InlineData("all:x")]
    [InlineData("1node")]
    [InlineData("no de")]
    [InlineData("node:")]
    [InlineData("node:\"open")]
    [InlineData("node:\"a\"b")]
    [InlineData("node:a\"b")]
    [InlineData("node:\"\\q\"")]
    [InlineData("mark:x=1+")]
    [InlineData("mark:x=1++y=2")]
    [InlineData("mark:x=1+y")]
    [InlineData("mark:=1")]
    [InlineData("mark:1x=1")]
    [InlineData("mark:x-y=1")]
    [InlineData("mark:@name=1")]
    [InlineData("mark:x=1+x=2")]
    [InlineData("edge:@from=A+@FROM=B")]
    [InlineData("mark:x=[1,2]")]
    [InlineData("mark:x={\"a\":1}")]
    [InlineData("mark:x=")]
    [InlineData("mark:x=a\"b")]
    [InlineData("mark:x=\"a\"b")]
    [InlineData("edge:@index=-1")]
    [InlineData("edge:@index=x")]
    [InlineData("edge:@index=\"0\"")]
    public void PositionSelector_Parse_Malformed_ThrowsFormatException(string text)
    {
        // Act
        var exception = Assert.Throws<FormatException>(() => PositionSelector.Parse(text));

        // Assert
        Assert.Contains($"'{text}'", exception.Message, StringComparison.Ordinal);
        Assert.False(PositionSelector.TryParse(text, out _, out var problem));
        Assert.False(string.IsNullOrEmpty(problem));
    }

    /// <summary>
    ///     Test the length and predicate limits.
    /// </summary>
    [Fact]
    public void PositionSelector_Parse_OverLimits_ThrowsFormatException()
    {
        // Arrange
        var atLength = "node:" + new string('a', PositionSelector.MaxSelectorLength - 5);
        var overLength = atLength + "a";
        var atPredicates = "mark:" + string.Join('+', Enumerable.Range(0, PositionSelector.MaxPredicates).Select(i => $"c{i}=1"));
        var overPredicates = atPredicates + "+z=1";

        // Act / Assert
        Assert.Equal(atLength, PositionSelector.Parse(atLength).Text);
        Assert.Throws<FormatException>(() => PositionSelector.Parse(overLength));
        Assert.Equal(atPredicates, PositionSelector.Parse(atPredicates).Text);
        Assert.Throws<FormatException>(() => PositionSelector.Parse(overPredicates));
    }

    /// <summary>
    ///     Test that a glob is bounded and correct on a pattern that backtracks a lot.
    /// </summary>
    [Fact]
    public void PositionSelector_Matches_BacktrackingGlob_MatchesCorrectly()
    {
        // Arrange
        var id = new string('a', 200) + "b";
        var position = new ImagePosition("node", id);

        // Act / Assert
        Assert.True(PositionSelector.Parse("node:*a*a*a*a*a*b").Matches(position));
        Assert.False(PositionSelector.Parse("node:*a*a*a*a*a*c").Matches(position));
        Assert.True(PositionSelector.Parse("node:a*?b").Matches(position));
        Assert.False(PositionSelector.Parse("node:?").Matches(new ImagePosition("node", string.Empty)));
        Assert.True(PositionSelector.Parse("node:*").Matches(new ImagePosition("node", string.Empty)));
    }

    /// <summary>
    ///     Test that a selection removes duplicate texts, takes the union, and gates by kind.
    /// </summary>
    [Fact]
    public void PositionSelection_Parse_DuplicatesAndKinds_UnionAndKindGate()
    {
        // Act
        var selection = PositionSelection.Parse(["node:send", "edge", "node:send"]);

        // Assert
        Assert.Equal(["node:send", "edge"], selection.Selectors.Select(selector => selector.Text));
        Assert.True(selection.Matches(Node));
        Assert.True(selection.Matches(Edge));
        Assert.False(selection.Matches(OddNode));
        Assert.True(selection.IncludesKind("node"));
        Assert.False(selection.IncludesKind("mark"));
        Assert.True(PositionSelection.All.IncludesKind("mark"));
    }

    /// <summary>
    ///     Test that a selection refuses no selectors, too many, and a malformed one.
    /// </summary>
    [Fact]
    public void PositionSelection_Parse_BadSelections_ThrowFormatException()
    {
        // Arrange
        var many = Enumerable.Range(0, PositionSelection.MaxSelectors + 1).Select(i => $"node:n{i}").ToArray();

        // Act / Assert
        Assert.Throws<FormatException>(() => PositionSelection.Parse([]));
        Assert.Throws<FormatException>(() => PositionSelection.Parse(many));
        Assert.Equal(PositionSelection.MaxSelectors, PositionSelection.Parse(many[..^1]).Selectors.Count);
        var exception = Assert.Throws<FormatException>(() => PositionSelection.Parse(["node", "all:x"]));
        Assert.Contains("'all:x'", exception.Message, StringComparison.Ordinal);
    }
}
