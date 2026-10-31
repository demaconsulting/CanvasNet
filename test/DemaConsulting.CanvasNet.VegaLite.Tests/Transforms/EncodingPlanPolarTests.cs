using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Unit tests for the polar half of <see cref="EncodingPlan"/>: the stack of <c>theta</c> and <c>radius</c> (the stacked channel, its dimension, the order of the
///     layers and the field names) and the link of the secondary channels <c>theta2</c> and <c>radius2</c>. The expectations are derived by hand from the rules of
///     Vega-Lite's <c>stack</c> function and from the names of the stacked fields.
/// </summary>
public class EncodingPlanPolarTests
{
    /// <summary>
    ///     Creates the plan of a view.
    /// </summary>
    /// <param name="mark">The mark.</param>
    /// <param name="encoding">The JSON text of the encoding object.</param>
    /// <returns>The plan.</returns>
    private static EncodingPlan Create(string mark, string encoding)
    {
        var tree = SpecParser.Parse("{\"data\":{\"values\":[{\"a\":1}]},\"mark\":\"" + mark + "\",\"encoding\":" + encoding + "}");
        return EncodingPlan.Create(tree.Root, ConfigResolver.Resolve(tree, null));
    }

    /// <summary>
    ///     Validates the plan of a pie chart: the stack is on <c>theta</c> of the summed field, the layers are ordered ascending by the color field (the stacked channel
    ///     is not <c>y</c>), the stack-by field splits the layers, and the two channels read the end and the start of the stack.
    /// </summary>
    [Fact]
    public void EncodingPlan_Create_PlansAPolarStackWithAscendingLayers()
    {
        // Act
        var plan = Create(
            "arc",
            """{"theta":{"field":"v","type":"quantitative","aggregate":"sum"},"color":{"field":"g","type":"nominal"}}""");

        // Assert: the sum is stacked into sum_v_start and sum_v_end, the layers ascend by g, and there is no dimension to group by
        var stack = plan.Stack!;
        Assert.Equal("theta", stack.Channel);
        Assert.Equal("sum_v", stack.Field);
        Assert.Equal("sum_v_start", stack.StartName);
        Assert.Equal("sum_v_end", stack.EndName);
        Assert.Equal([new SortField("g", false)], stack.Sort);
        Assert.Empty(stack.GroupBy);
        var theta = plan.Channel("theta")!;
        Assert.True(theta.IsStacked);
        Assert.Equal("sum_v", theta.ValueField);
        Assert.Equal("sum_v_end", theta.FieldPath);
        Assert.Equal("sum_v_start", theta.Field2Path);
        Assert.False(plan.Channel("color")!.IsStacked);

        // Assert: the aggregate groups by the layer field, and the plan is not the identity
        Assert.Equal(["g"], plan.Aggregate!.GroupBy);
        Assert.False(plan.IsIdentity);
    }

    /// <summary>
    ///     Validates that <c>radius</c> is the dimension of a <c>theta</c> stack (its fields group the stacks), that
    ///     <c>theta</c> is the dimension of a <c>radius</c> stack, and that the order channel decides the order of the layers (descending when it says so).
    /// </summary>
    [Fact]
    public void EncodingPlan_Create_PolarDimensionAndOrderDecideTheStackGroups()
    {
        // Act
        var thetaStack = Create(
            "arc",
            """{"theta":{"field":"v","type":"quantitative"},"radius":{"field":"r","type":"nominal"},"color":{"field":"g","type":"nominal"},"order":[{"field":"o"},{"field":"p","sort":"descending"}]}""");
        var radiusStack = Create(
            "arc",
            """{"radius":{"field":"v","type":"quantitative"},"theta":{"field":"t","type":"nominal"},"detail":{"field":"g"}}""");

        // Assert: the group is the field of the dimension, the sort is the order channel
        Assert.Equal("theta", thetaStack.Stack!.Channel);
        Assert.Equal(["r"], thetaStack.Stack.GroupBy);
        Assert.Equal([new SortField("o", false), new SortField("p", true)], thetaStack.Stack.Sort);
        Assert.Equal("radius", radiusStack.Stack!.Channel);
        Assert.Equal(["t"], radiusStack.Stack.GroupBy);
        Assert.Equal([new SortField("g", false)], radiusStack.Stack.Sort);
    }

    /// <summary>
    ///     Validates that a secondary polar channel is linked to the channel it extends like <c>x2</c> is to <c>x</c>: the first channel reads the field of the second
    ///     as its second end and the two titles are merged; a stacked channel keeps its own ends.
    /// </summary>
    [Fact]
    public void EncodingPlan_Create_LinksTheSecondaryPolarChannels()
    {
        // Act
        var angles = Create(
            "arc",
            """{"theta":{"field":"a","type":"quantitative","stack":false},"theta2":{"field":"b"},"radius":{"field":"c","type":"quantitative"},"radius2":{"field":"d"}}""");
        var stacked = Create(
            "arc",
            """{"theta":{"field":"a","type":"quantitative"},"theta2":{"value":1},"color":{"field":"g"}}""");

        // Assert
        Assert.Null(angles.Stack);
        Assert.Equal("b", angles.Channel("theta")!.Field2Path);
        Assert.Equal("a, b", angles.Channel("theta")!.Title);
        Assert.Equal("d", angles.Channel("radius")!.Field2Path);
        Assert.Equal("c, d", angles.Channel("radius")!.Title);
        Assert.Equal("a_start", stacked.Channel("theta")!.Field2Path);
    }

    /// <summary>
    ///     Validates that the Cartesian stack still wins, and keeps the old shape of the plan (the stack of the measure of <c>x</c>), when a mark also has a polar channel.
    /// </summary>
    [Fact]
    public void EncodingPlan_Create_CartesianStackKeepsPrecedenceOverPolar()
    {
        // Act
        var plan = Create(
            "bar",
            """{"x":{"field":"k","type":"nominal"},"y":{"field":"v","type":"quantitative"},"theta":{"field":"w","type":"quantitative"},"color":{"field":"g"}}""");

        // Assert
        Assert.Equal("y", plan.Stack!.Channel);
        Assert.Equal(["k"], plan.Stack.GroupBy);
        Assert.Equal([new SortField("g", true)], plan.Stack.Sort);
        Assert.False(plan.Channel("theta")!.IsStacked);
    }

    /// <summary>
    ///     Validates that a dimension that reads the stacked field itself does not split the stack (Vega-Lite avoids grouping by the stacked field): a radial chart reads its
    ///     <c>theta</c> and <c>radius</c> from one field, and its slices stack in one group; a dimension of another field still groups.
    /// </summary>
    [Fact]
    public void EncodingPlan_Create_StackDoesNotGroupByTheStackedField()
    {
        // Act
        var radial = Create(
            "arc",
            """{"theta":{"field":"v","type":"quantitative","stack":true},"radius":{"field":"v","type":"quantitative","scale":{"type":"sqrt"}}}""");
        var other = Create("arc", """{"theta":{"field":"v","type":"quantitative","stack":true},"radius":{"field":"r","type":"quantitative"}}""");

        // Assert
        Assert.Equal("theta", radial.Stack!.Channel);
        Assert.Empty(radial.Stack.GroupBy);
        Assert.Equal(["r"], other.Stack!.GroupBy);
    }
}
