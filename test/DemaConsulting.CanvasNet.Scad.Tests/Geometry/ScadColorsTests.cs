// cspell:ignore rebeccapurple aliceblue darkslategray darkslategrey yellowgreen dodgerblue nosuchcolor
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Scad.Geometry;
using static DemaConsulting.CanvasNet.Scad.Tests.Geometry.ScadGeometryTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Geometry;

/// <summary>
///     Unit tests of <see cref="ScadColors"/> and <see cref="ScadColorTable"/>: the accepted color forms, the
///     innermost-wins rule, invalid values, and the alpha degradation report.
/// </summary>
public class ScadColorsTests
{
    /// <summary>
    ///     Builds a program with one colored cube and returns the appearance color of its only leaf.
    /// </summary>
    /// <param name="call">The color call.</param>
    /// <returns>The color, or <see langword="null"/> when the leaf has no appearance.</returns>
    private static Rgba32? ColorOf(string call)
    {
        var result = Build($"{call} cube(1);");
        return Assert.Single(result.Leaves).Appearance?.Color;
    }

    /// <summary>
    ///     Validates the size and a sample of the named-color table.
    /// </summary>
    [Fact]
    public void ScadColorTable_Colors_HoldTheCssNamesAndTransparent()
    {
        // Arrange / Act
        var colors = ScadColorTable.Colors;

        // Assert: 148 CSS names plus transparent, case-insensitive
        Assert.Equal(149, colors.Count);
        Assert.Equal(new Rgba32(255, 0, 0, 255), colors["red"]);
        Assert.Equal(new Rgba32(255, 0, 0, 255), colors["RED"]);
        Assert.Equal(new Rgba32(0x66, 0x33, 0x99, 255), colors["RebeccaPurple"]);
        Assert.Equal(colors["gray"], colors["grey"]);
        Assert.Equal(colors["darkslategray"], colors["darkslategrey"]);
        Assert.Equal(new Rgba32(0, 0, 0, 0), colors["transparent"]);
        Assert.Equal(new Rgba32(0xF0, 0xF8, 0xFF, 255), colors["aliceblue"]);
        Assert.Equal(new Rgba32(0x9A, 0xCD, 0x32, 255), colors["yellowgreen"]);
    }

    /// <summary>
    ///     Validates that every named color renders as exactly its table entry, opaque, except <c>transparent</c>.
    /// </summary>
    [Fact]
    public void ScadColors_EveryName_RendersAsItsTableEntry()
    {
        // Arrange / Act / Assert
        foreach (var (name, expected) in ScadColorTable.Colors.Where(c => c.Key != "transparent"))
        {
            Assert.Equal(expected, ColorOf($"color(\"{name}\")"));
        }
    }

    /// <summary>
    ///     Validates named colors in any case, by position and by name.
    /// </summary>
    /// <param name="call">The color call.</param>
    /// <param name="r">The expected red.</param>
    /// <param name="g">The expected green.</param>
    /// <param name="b">The expected blue.</param>
    [Theory]
    [InlineData("color(\"red\")", 255, 0, 0)]
    [InlineData("color(c = \"Blue\")", 0, 0, 255)]
    [InlineData("color(\"ORANGE\")", 255, 165, 0)]
    [InlineData("color(\"dodgerblue\")", 0x1E, 0x90, 255)]
    [InlineData("color(\"#f80\")", 255, 0x88, 0)]
    [InlineData("color(\"#ff8800\")", 255, 0x88, 0)]
    [InlineData("color(\"#FF8800\")", 255, 0x88, 0)]
    [InlineData("color(\"#0a1B2c\")", 0x0A, 0x1B, 0x2C)]
    [InlineData("color([1, 0.5, 0])", 255, 128, 0)]
    [InlineData("color(c = [0, 1, 0])", 0, 255, 0)]
    [InlineData("color([2, -1, 0.5])", 255, 0, 128)]
    [InlineData("color([0.2, 0.4, 0.6, 1])", 51, 102, 153)]
    public void ScadColors_Forms_GiveTheExpectedOpaqueColor(string call, int r, int g, int b)
    {
        // Arrange / Act
        var result = Build($"{call} cube(1);");

        // Assert
        var color = Assert.Single(result.Leaves).Appearance!.Color;
        Assert.Equal(new Rgba32((byte)r, (byte)g, (byte)b, 255), color);
        Assert.Empty(result.Warnings);
        Assert.Empty(result.Unsupported);
    }

    /// <summary>
    ///     Validates that the innermost color wins and that colors do not leak to siblings.
    /// </summary>
    [Fact]
    public void ScadColors_Nesting_InnermostWinsAndSiblingsAreUnaffected()
    {
        // Arrange / Act
        var result = Build("color(\"red\") { color(\"blue\") cube(1); cube(2); }\ncube(3);");

        // Assert
        var leaves = result.Leaves;
        Assert.Equal(3, leaves.Count);
        Assert.Equal(new Rgba32(0, 0, 255, 255), leaves[0].Appearance!.Color);
        Assert.Equal(new Rgba32(255, 0, 0, 255), leaves[1].Appearance!.Color);
        Assert.Null(leaves[2].Appearance);
    }

    /// <summary>
    ///     Validates that a color applies to every child of the module.
    /// </summary>
    [Fact]
    public void ScadColors_SeveralChildren_AreAllColored()
    {
        // Arrange / Act
        var result = Build("color(\"green\") { cube(1); sphere(1); cylinder(); }");

        // Assert
        Assert.All(result.Leaves, leaf => Assert.Equal(new Rgba32(0, 128, 0, 255), leaf.Appearance!.Color));
        Assert.Equal(3, result.Leaves.Count);
    }

    /// <summary>
    ///     Validates that the color of a transform chain is kept through transforms in either order.
    /// </summary>
    [Fact]
    public void ScadColors_ThroughTransforms_IsKept()
    {
        // Arrange / Act
        var result = Build("translate([1, 0, 0]) color(\"red\") rotate(30) cube(1);\ncolor(\"blue\") scale(2) cube(1);");

        // Assert
        Assert.Equal(new Rgba32(255, 0, 0, 255), result.Leaves[0].Appearance!.Color);
        Assert.Equal(new Rgba32(0, 0, 255, 255), result.Leaves[1].Appearance!.Color);
    }

    /// <summary>
    ///     Validates that a 2D shape keeps its color into its slab.
    /// </summary>
    [Fact]
    public void ScadColors_FlatShape_ColorsTheSlab()
    {
        // Arrange / Act
        var result = Build("color(\"yellow\") square(2);");

        // Assert
        Assert.Equal(new Rgba32(255, 255, 0, 255), Assert.Single(result.Leaves).Appearance!.Color);
    }

    /// <summary>
    ///     Validates that a color with nothing to color, or without a color, changes nothing and reports nothing.
    /// </summary>
    [Fact]
    public void ScadColors_NoColorOrNoChildren_IsSilent()
    {
        // Arrange / Act
        var plain = Build("color() cube(1);");
        var empty = Build("color(\"red\") { }");

        // Assert
        Assert.Null(Assert.Single(plain.Leaves).Appearance);
        Assert.Empty(plain.Warnings);
        Assert.Empty(plain.Unsupported);
        Assert.Empty(empty.Meshes);
        Assert.Empty(empty.Warnings);
    }

    /// <summary>
    ///     Validates that an invalid color is a warning at the instance, leaves the children uncolored and keeps them.
    /// </summary>
    /// <param name="call">The color call.</param>
    [Theory]
    [InlineData("color(\"nosuchcolor\")")]
    [InlineData("color(\"\")")]
    [InlineData("color(\"#12\")")]
    [InlineData("color(\"#12345\")")]
    [InlineData("color(\"#ggg\")")]
    [InlineData("color(\"#-12\")")]
    [InlineData("color(\"#+12\")")]
    [InlineData("color(5)")]
    [InlineData("color([1, 2])")]
    [InlineData("color([1, 2, 3, 4, 5])")]
    [InlineData("color([1, \"a\", 0])")]
    [InlineData("color([1, 0, 1 / 0])")]
    [InlineData("color(true)")]
    public void ScadColors_InvalidColor_WarnsAndLeavesChildrenUncolored(string call)
    {
        // Arrange / Act
        var result = Build($"\n  {call} cube(1);");

        // Assert
        Assert.Null(Assert.Single(result.Leaves).Appearance);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal(new ScadSourceLocation(null, 2, 3), warning.Location);
        Assert.Contains("color:", warning.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that every route to an alpha below 1 is reported once at the color's location while the
    ///     surface is drawn opaque.
    /// </summary>
    /// <param name="call">The color call.</param>
    /// <param name="r">The expected red.</param>
    /// <param name="g">The expected green.</param>
    /// <param name="b">The expected blue.</param>
    [Theory]
    [InlineData("color(\"red\", 0.5)", 255, 0, 0)]
    [InlineData("color(\"red\", alpha = 0.5)", 255, 0, 0)]
    [InlineData("color(c = \"red\", alpha = 0)", 255, 0, 0)]
    [InlineData("color([0, 1, 0, 0.25])", 0, 255, 0)]
    [InlineData("color(\"#0000ff80\")", 0, 0, 255)]
    [InlineData("color(\"#0f08\")", 0, 255, 0)]
    [InlineData("color(\"transparent\")", 0, 0, 0)]
    [InlineData("color([1, 1, 1, 1], alpha = 0.1)", 255, 255, 255)]
    public void ScadColors_AlphaBelowOne_ReportsAndDrawsOpaque(string call, int r, int g, int b)
    {
        // Arrange / Act
        var result = Build($"\n  {call} cube(1);");

        // Assert
        Assert.Equal(new Rgba32((byte)r, (byte)g, (byte)b, 255), Assert.Single(result.Leaves).Appearance!.Color);
        var report = Assert.Single(result.Unsupported);
        Assert.Equal("scad-color-alpha", report.Feature);
        Assert.Equal(new ScadSourceLocation(null, 2, 3), report.Location);
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    ///     Validates that an alpha argument of 1 or more (also from the vector or hex form) is not reported and that
    ///     the alpha argument overrides the alpha of the color value.
    /// </summary>
    [Fact]
    public void ScadColors_FullAlpha_IsNotReportedAndAlphaArgumentOverrides()
    {
        // Arrange / Act
        var full = Build("color(\"red\", 1) cube(1);\ncolor(\"#ff0000ff\") cube(1);\ncolor([1, 0, 0, 1]) cube(1);\ncolor(\"red\", 7) cube(1);");
        var overridden = Build("color([1, 0, 0, 0.2], alpha = 1) cube(1);");

        // Assert
        Assert.Empty(full.Unsupported);
        Assert.Empty(overridden.Unsupported);
        Assert.Empty(full.Warnings);
    }

    /// <summary>
    ///     Validates that an alpha without a color is reported but colors nothing, and that the report is made even
    ///     when there is nothing to color.
    /// </summary>
    [Fact]
    public void ScadColors_AlphaOnly_ReportsWithoutColoring()
    {
        // Arrange / Act
        var withChild = Build("color(alpha = 0.3) cube(1);");
        var withoutChild = Build("color(\"red\", 0.3) { }");

        // Assert
        Assert.Null(Assert.Single(withChild.Leaves).Appearance);
        Assert.Equal(["scad-color-alpha"], withChild.Features);
        Assert.Equal(["scad-color-alpha"], withoutChild.Features);
    }

    /// <summary>
    ///     Validates that an invalid alpha argument warns and is ignored while the color applies.
    /// </summary>
    [Fact]
    public void ScadColors_InvalidAlpha_WarnsAndAppliesTheColor()
    {
        // Arrange / Act
        var result = Build("color(\"red\", alpha = \"half\") cube(1);");

        // Assert
        Assert.Equal(new Rgba32(255, 0, 0, 255), Assert.Single(result.Leaves).Appearance!.Color);
        Assert.Contains(result.Warnings, w => w.Message.Contains("alpha must be a number", StringComparison.Ordinal));
        Assert.Empty(result.Unsupported);
    }

    /// <summary>
    ///     Validates that strict mode throws at the first color alpha report with its feature and location.
    /// </summary>
    [Fact]
    public void ScadColors_StrictMode_ThrowsForAlpha()
    {
        // Arrange / Act
        var ex = Assert.Throws<ScadUnsupportedFeatureException>(() => Build("cube(1);\n  color(\"red\", 0.5) cube(1);", Settings(strict: true)));

        // Assert
        Assert.Equal("scad-color-alpha", ex.Feature);
        Assert.Equal(new ScadSourceLocation(null, 2, 3), ex.Location);
    }

    /// <summary>
    ///     Validates that colors survive a 2D tree of transforms (a color around a translated 2D shape colors the
    ///     slab it becomes).
    /// </summary>
    [Fact]
    public void ScadColors_AroundTransformedFlatShape_ColorsTheSlab()
    {
        // Arrange / Act
        var result = Build("color(\"red\") translate([1, 1]) { square(1); translate([3, 0]) circle(1); }");

        // Assert
        Assert.Equal(2, result.Leaves.Count);
        Assert.All(result.Leaves, leaf => Assert.Equal(new Rgba32(255, 0, 0, 255), leaf.Appearance!.Color));
    }
}
