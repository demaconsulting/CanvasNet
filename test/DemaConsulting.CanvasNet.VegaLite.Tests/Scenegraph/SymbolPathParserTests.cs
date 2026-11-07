using System.Numerics;
using DemaConsulting.CanvasNet.Geometry;
using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Scenegraph;

/// <summary>
///     Unit tests of <see cref="SymbolPathParser"/>: the parsing of SVG path data the way vega-scenegraph's <c>path/parse.js</c> does, the drawing scaled to a symbol size
///     (<c>customSymbol</c>) and the bounds of <c>boundContext.js</c>. Every expectation is derived by hand from the path text, the radius <c>sqrt(size) / 2</c> and the curve formulas.
/// </summary>
public class SymbolPathParserTests
{
    /// <summary>The tolerance of numbers that pass through single precision and a curve extremum.</summary>
    private const double Tolerance = 1e-4;

    /// <summary>
    ///     Draws a path at a size.
    /// </summary>
    /// <param name="text">The path data.</param>
    /// <param name="size">The symbol area; 4 is a radius of 1, so the coordinates are not scaled.</param>
    /// <returns>The path and its bounds.</returns>
    private static (Path Path, SceneBounds Bounds) Draw(string text, double size = 4)
    {
        var parser = SymbolPathParser.TryParse(text, out var error);
        Assert.True(parser is not null, error);
        var path = parser.Render(size, out var bounds);
        return (path, bounds);
    }

    /// <summary>
    ///     Asserts the bounds of a drawing.
    /// </summary>
    /// <param name="bounds">The bounds.</param>
    /// <param name="x1">The smallest x.</param>
    /// <param name="y1">The smallest y.</param>
    /// <param name="x2">The largest x.</param>
    /// <param name="y2">The largest y.</param>
    private static void AssertBounds(SceneBounds bounds, double x1, double y1, double x2, double y2)
    {
        Assert.Equal(x1, bounds.X1, Tolerance);
        Assert.Equal(y1, bounds.Y1, Tolerance);
        Assert.Equal(x2, bounds.X2, Tolerance);
        Assert.Equal(y2, bounds.Y2, Tolerance);
    }

    /// <summary>
    ///     Validates that the coordinates are in units of the radius: the square of the path at the sizes 4, 16 and 100 has the sides 1, 2 and 5 times the side of the path.
    /// </summary>
    [Fact]
    public void SymbolPathParser_Render_ScalesTheCoordinatesByTheRadius()
    {
        // Arrange: size 4 has the radius 1, size 16 the radius 2, size 100 the radius 5
        const string square = "M0,0 L1,0 L1,1 L0,1 Z";

        // Act / Assert
        AssertBounds(Draw(square, 4).Bounds, 0, 0, 1, 1);
        AssertBounds(Draw(square, 16).Bounds, 0, 0, 2, 2);
        var (path, bounds) = Draw("M-1,-1 L1,-1 L1,1 L-1,1 Z", 100);
        AssertBounds(bounds, -5, -5, 5, 5);
        AssertPathBounds(path, -5, -5, 5, 5);
        Assert.Equal(new Vector2(-5, -5), Assert.Single(path.Subpaths).Start);
    }

    /// <summary>
    ///     Asserts the bounds of a core path.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="left">The smallest x.</param>
    /// <param name="top">The smallest y.</param>
    /// <param name="right">The largest x.</param>
    /// <param name="bottom">The largest y.</param>
    private static void AssertPathBounds(Path path, double left, double top, double right, double bottom)
    {
        var drawn = path.GetBounds();
        Assert.Equal(left, drawn.X, Tolerance);
        Assert.Equal(top, drawn.Y, Tolerance);
        Assert.Equal(right, drawn.X + drawn.Width, Tolerance);
        Assert.Equal(bottom, drawn.Y + drawn.Height, Tolerance);
    }

    /// <summary>
    ///     Validates the implicit repetition: more numbers than a command takes repeat it, and the repeat of a move-to is a line-to (absolute for <c>M</c>, relative for <c>m</c>).
    /// </summary>
    [Fact]
    public void SymbolPathParser_TryParse_ExtendedParameterListsRepeatTheCommand()
    {
        // Arrange / Act
        var absolute = SymbolPathParser.TryParse("M0,0 4,0 4,4", out _);
        var relative = SymbolPathParser.TryParse("m1,1 2,0 0,2", out _);
        var horizontal = SymbolPathParser.TryParse("M0,0 H1 2 3", out _);

        // Assert: M then two L; m then two l; M then three H
        Assert.Equal(3, absolute!.CommandCount);
        Assert.Equal(3, relative!.CommandCount);
        Assert.Equal(4, horizontal!.CommandCount);
        absolute.Render(4, out var absoluteBounds);
        AssertBounds(absoluteBounds, 0, 0, 4, 4);

        // The relative path starts at (1,1), moves right 2 and then down 2
        var (path, bounds) = Draw("m1,1 2,0 0,2");
        AssertBounds(bounds, 1, 1, 3, 3);
        Assert.Equal(new Vector2(1, 1), path.Subpaths[0].Start);
        Assert.Equal(new Vector2(3, 3), path.Subpaths[0].Commands[^1].EndPoint);
    }

    /// <summary>
    ///     Validates the number grammar of vega's <c>numberPattern</c> and <c>spacePattern</c>: numbers that touch (a sign, a second decimal point), a number that ends in a decimal point, an
    ///     exponent, commas with and without spaces, and white space around the commands.
    /// </summary>
    [Fact]
    public void SymbolPathParser_TryParse_ReadsTheNumbersOfVega()
    {
        // Act / Assert: -1.5 -2.5 and .5 .5 stick together
        AssertBounds(Draw("M-1.5-2.5L.5.5").Bounds, -1.5, -2.5, 0.5, 0.5);

        // Assert: an exponent, a trailing decimal point, a plus sign and mixed separators
        AssertBounds(Draw("M1e1,2E-1 L 3. , +4").Bounds, 3, 0.2, 10, 4);
        AssertBounds(Draw("  M 0 , 0   L\t1\n2  ").Bounds, 0, 0, 1, 2);

        // Assert: arc flags are single digits, so they may touch the numbers around them
        var parser = SymbolPathParser.TryParse("M0,0a1 1 0 0125 5", out var error);
        Assert.True(parser is not null, error);
        Assert.Equal(2, parser.CommandCount);
    }

    /// <summary>
    ///     Validates the errors of vega's parser: a parameter that is not a number (also a missing one inside a command, which vega reports as a wrong type because it reads past the text), a command
    ///     without any parameter, an arc flag that is not 0 or 1, and a smooth quadratic curve that
    ///     is the first command (vega throws a TypeError there); each is a null result with a reason.
    /// </summary>
    /// <param name="text">The path data.</param>
    /// <param name="reason">A part of the reason.</param>
    [Theory]
    [InlineData("M0,x", "parameter type")]
    [InlineData("M0", "parameter type")]
    [InlineData("M0,0 1", "parameter type")]
    [InlineData("M0,0 L1,1,2", "parameter type")]
    [InlineData("M0,0 A1 1 0 2 0 5 5", "parameter type")]
    [InlineData("M0,0 A1 1 0 0 0 5", "parameter type")]
    [InlineData("t1,1", "first command")]
    [InlineData("M0,0 h", "parameter count")]
    [InlineData("M0,0 l", "parameter count")]
    [InlineData("M0,0 c", "parameter count")]
    public void SymbolPathParser_TryParse_RejectsWhatVegaThrowsFor(string text, string reason)
    {
        // Act
        var parser = SymbolPathParser.TryParse(text, out var error);

        // Assert
        Assert.Null(parser);
        Assert.Contains(reason, error, StringComparison.Ordinal);
        Assert.False(SymbolPathParser.IsValid(text));
    }

    /// <summary>
    ///     Validates that text before the first command letter is skipped (vega's expression has no anchor), that a text without a command has no command and draws nothing, and that
    ///     a command without parameters (the close) needs none.
    /// </summary>
    [Fact]
    public void SymbolPathParser_TryParse_SkipsTextBeforeTheFirstCommandAndAcceptsNoCommand()
    {
        // Act
        var skipped = SymbolPathParser.TryParse("fgi0.5 M1,1 L2,2", out _);
        var none = SymbolPathParser.TryParse("fgi", out _);
        var (path, bounds) = Draw("fgi");

        // Assert: "fgi0.5 " is not a command; the two commands after it are
        Assert.Equal(2, skipped!.CommandCount);
        Assert.Equal(0, none!.CommandCount);
        Assert.Empty(path.Subpaths);
        Assert.True(bounds.IsEmpty);
        Assert.Equal(1, SymbolPathParser.TryParse("z", out _)!.CommandCount);
    }

    /// <summary>
    ///     Validates that a path of more than the limit of commands is invalid, also when it comes from one command with many parameters.
    /// </summary>
    [Fact]
    public void SymbolPathParser_TryParse_RejectsMoreCommandsThanTheLimit()
    {
        // Arrange
        var many = "M0,0" + string.Concat(Enumerable.Repeat(" L1,1", SymbolPathParser.MaxCommands));
        var single = "M0,0 L" + string.Concat(Enumerable.Repeat("1,1 ", SymbolPathParser.MaxCommands));
        var exact = "M0,0" + string.Concat(Enumerable.Repeat(" L1,1", SymbolPathParser.MaxCommands - 1));

        // Act / Assert
        Assert.Null(SymbolPathParser.TryParse(many, out var error));
        Assert.Contains("commands", error, StringComparison.Ordinal);
        Assert.Null(SymbolPathParser.TryParse(single, out _));
        Assert.Equal(SymbolPathParser.MaxCommands, SymbolPathParser.TryParse(exact, out _)!.CommandCount);
    }

    /// <summary>
    ///     Validates the drawing of a canvas path: a line before any move starts the path at its own point, a command after a close starts at the start of the closed sub-path, and a
    ///     command with a number that is not finite is skipped.
    /// </summary>
    [Fact]
    public void SymbolPathParser_Render_FollowsTheCanvasRules()
    {
        // Act
        var (withoutMove, _) = Draw("L5,5 L10,0");
        var (afterClose, _) = Draw("M0,0 L2,0 L2,2 Z L5,5");
        var (infinite, _) = Draw("M0,0 L1e999,1 L3,3");

        // Assert: one sub-path from (5,5) to (10,0)
        var first = Assert.Single(withoutMove.Subpaths);
        Assert.Equal(new Vector2(5, 5), first.Start);
        Assert.Equal(new Vector2(10, 0), Assert.Single(first.Commands).EndPoint);

        // Assert: the close ends the first sub-path; the next line draws from (0,0) to (5,5)
        Assert.Equal(2, afterClose.Subpaths.Count);
        Assert.Equal(new Vector2(0, 0), afterClose.Subpaths[1].Start);
        Assert.Equal(new Vector2(5, 5), Assert.Single(afterClose.Subpaths[1].Commands).EndPoint);

        // Assert: the line to infinity is skipped, the next line is drawn
        Assert.Equal(new Vector2(3, 3), Assert.Single(infinite.Subpaths).Commands.Single().EndPoint);
    }

    /// <summary>
    ///     Validates the bounds of the curves with their extrema, by hand: the cubic with the controls (20,20) and (20,20) over the height 10 reaches 17.5 in the middle (the weights 1/8, 3/8, 3/8, 1/8),
    ///     and <c>S</c> reflects the last control point through the current point even after a command that is not a curve (vega's behavior; the SVG rule would use the current point).
    /// </summary>
    [Fact]
    public void SymbolPathParser_Render_CurvesHaveTheirExtremaAndSmoothCurvesReflectTheLastControl()
    {
        // Act: S after M reflects the control point (0,0) through (10,10): (20,20) for the first control
        var (path, bounds) = Draw("M10,10 S20,20 30,10");

        // Assert: x from 10 to 30; y from 10 up to 17.5 (the extremum of 10, 20, 20, 10 at t = 0.5)
        AssertBounds(bounds, 10, 10, 30, 17.5);
        var curve = Assert.Single(path.Subpaths[0].Commands);
        Assert.Equal(new Vector2(30, 10), curve.EndPoint);

        // Act: the quadratic with the control (5,5) over (0,0) to (10,0) reaches 2.5; its reflection (t after q) reaches -2.5
        var (_, quadratics) = Draw("M0,0 q5,5 10,0 t10,0");

        // Assert: the reflection of (5,5) through (10,0) is (15,-5)
        AssertBounds(quadratics, 0, -2.5, 20, 2.5);
    }

    /// <summary>
    ///     Validates vega's reuse of the scaled copy of a command: at a size other than 4 a <c>t</c> sees <c>t</c> as the previous command, so its control point is the reflection of a control that
    ///     was never set (not a number), the curve is skipped by the canvas, and the bounds still include its end point (the bound context does not skip it).
    /// </summary>
    [Fact]
    public void SymbolPathParser_Render_ScaledSmoothQuadraticSeesItselfAsThePreviousCommand()
    {
        // Act: size 16 is the radius 2, so q5,5 10,0 is (10,10) and (20,0); the t moves 20 more
        var (path, bounds) = Draw("M0,0 q5,5 10,0 t10,0", 16);

        // Assert: the t draws nothing, but its end point (40,0) is in the bounds
        Assert.Single(path.Subpaths[0].Commands);
        AssertBounds(bounds, 0, 0, 40, 5);
    }

    /// <summary>
    ///     Validates the arc: the half circle of radius 1 from (0,0) to (2,0) with the sweep flag 1 goes through (1,-1) (up on the screen), and is drawn with two Bezier curves (at most a quarter turn each).
    /// </summary>
    [Fact]
    public void SymbolPathParser_Render_ArcsAreBezierCurvesOfAtMostAQuarterTurn()
    {
        // Act
        var (path, bounds) = Draw("M0,0 A1,1 0 0 1 2,0");

        // Assert: two quarter turns, the highest point is (1,-1)
        Assert.Equal(2, path.Subpaths[0].Commands.Count);
        AssertBounds(bounds, 0, -1, 2, 0);

        // Act: the other side of the circle with the sweep flag 0
        var (_, below) = Draw("M0,0 A1,1 0 0 0 2,0");

        // Assert
        AssertBounds(below, 0, 0, 2, 1);

        // Act: a radius that is too small for the chord is enlarged to the half circle
        var (_, enlarged) = Draw("M0,0 A0.1,0.1 0 0 1 2,0");

        // Assert
        AssertBounds(enlarged, 0, -1, 2, 0);
    }

    /// <summary>
    ///     Validates the parsed paths are shared: the same text gives the same parsed path, an invalid text is remembered, and the drawing of one size is reused.
    /// </summary>
    [Fact]
    public void SymbolPathParser_TryGet_SharesParsedPathsAndDrawings()
    {
        // Act
        var first = SymbolPathParser.TryGet("M0,0 L1,1 L2,0 Z");
        var second = SymbolPathParser.TryGet("M0,0 L1,1 L2,0 Z");

        // Assert
        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.Null(SymbolPathParser.TryGet("M0,"));
        Assert.Null(SymbolPathParser.TryGet("M0,"));
        Assert.Same(first.Render(100, out _), first.Render(100, out _));
        Assert.NotSame(first.Render(100, out _), first.Render(64, out _));
    }

    /// <summary>
    ///     Validates the symbol the painter draws for a path and the bounds the legend and the layout use: a custom path is drawn at the radius, an invalid path is a circle, and the bounds of a
    ///     path that is not centered are the bounds of its drawing grown by the stroke, not a square around the center.
    /// </summary>
    [Fact]
    public void ScenePainter_SymbolPath_DrawsCustomPathsAndFallsBackToACircle()
    {
        // Arrange: size 100 is the radius 5; the path is a rectangle that reaches 3 below the center
        const string custom = "M-1,-1 L1,-1 L1,3 L-1,3 Z";

        // Act
        var path = ScenePainter.SymbolPath(custom, 100);
        var fallback = ScenePainter.SymbolPath("M0,0L", 100);
        var bounds = LegendBuilder.SymbolBounds(new SceneSymbol("mark-point", 10, 20) { Shape = custom, Size = 100 });
        var stroked = LegendBuilder.SymbolBounds(new SceneSymbol("mark-point", 10, 20) { Shape = custom, Size = 100, Stroke = "#000", StrokeWidth = 2 });
        var nothing = LegendBuilder.SymbolBounds(new SceneSymbol("mark-point", 10, 20) { Shape = "fgi", Size = 100 });

        // Assert: the rectangle from (-5,-5) to (5,15) around the center
        AssertPathBounds(path, -5, -5, 5, 15);
        Assert.Equal(Path.Circle(0, 0, 5).Subpaths[0].Commands.Count, fallback.Subpaths[0].Commands.Count);
        Assert.Equal((5.0, 15.0, 15.0, 35.0), (bounds.X1, bounds.Y1, bounds.X2, bounds.Y2));

        // Assert: a stroke of 2 reaches up to two widths (the miter allowance of vega's boundStroke), 4 pixels on every side
        Assert.Equal((1.0, 11.0, 19.0, 39.0), (stroked.X1, stroked.Y1, stroked.X2, stroked.Y2));

        // Assert: a path that draws nothing has no bounds
        Assert.True(nothing.IsEmpty);
        Assert.True(ScenePainter.IsDrawableShape("M0,0L1,1"));
        Assert.True(ScenePainter.IsDrawableShape("circle"));
        Assert.False(ScenePainter.IsDrawableShape("M0,0L"));
    }

    /// <summary>
    ///     Builds a valid path text of a given length (a unique polyline).
    /// </summary>
    /// <param name="seed">The number that makes the text unique.</param>
    /// <param name="length">The approximate length in characters.</param>
    /// <returns>The path text.</returns>
    private static string LongPath(int seed, int length)
    {
        var text = new System.Text.StringBuilder($"M{seed},0");
        while (text.Length < length)
        {
            text.Append(" L1,1");
        }

        return text.ToString();
    }

    /// <summary>
    ///     Validates that a path that does not parse is cached as a failure (so a repeated malformed path is parsed once), while a path longer
    ///     than the cache keeps is parsed on each call and never retained.
    /// </summary>
    [Fact]
    public void SymbolPathParser_TryGet_CachesInvalidPathsOnceAndDoesNotCacheVeryLongPaths()
    {
        // Arrange: an invalid path within the bound (a command without parameters), and a valid path longer than the cache keeps
        var invalid = "M0,0 L1,1 L " + new string(' ', 100) + "unique-invalid";
        var veryLong = LongPath(7, 9000);
        var before = SymbolPathParser.ParsesOnThisThread;

        // Act: the same malformed path ten times, as a shape field that repeats it for each row
        var results = Enumerable.Range(0, 10).Select(_ => SymbolPathParser.TryGet(invalid)).ToList();
        var parsedForInvalid = SymbolPathParser.ParsesOnThisThread - before;
        var longResult = SymbolPathParser.TryGet(veryLong);
        var longAgain = SymbolPathParser.TryGet(veryLong);

        // Assert: the invalid path is not drawable, is kept as a failure and was parsed once
        Assert.All(results, Assert.Null);
        Assert.True(SymbolPathParser.IsCached(invalid));
        Assert.Equal(1, parsedForInvalid);

        // Assert: the overlong one is drawn, was not kept and was parsed on each call
        Assert.NotNull(longResult);
        Assert.False(SymbolPathParser.IsCached(veryLong));
        Assert.Equal(longResult.CommandCount, longAgain!.CommandCount);
        Assert.Equal(1 + 2, SymbolPathParser.ParsesOnThisThread - before);
    }

    /// <summary>
    ///     Validates that unique long paths, added from several threads, never make the cache retain more path text than its budget.
    /// </summary>
    [Fact]
    public void SymbolPathParser_TryGet_BoundsTheRetainedTextAndIsThreadSafe()
    {
        // Arrange: far more unique 4000-character paths than the budget keeps (many times 262144 characters)
        const int perThread = 200;
        var violations = 0;

        // Act: four threads add unique paths and check the retained characters after each
        var threads = Enumerable.Range(0, 4).Select(t => new Thread(() =>
        {
            for (var i = 0; i < perThread; i++)
            {
                Assert.NotNull(SymbolPathParser.TryGet(LongPath((t * perThread) + i + 1000, 4000)));
                var (count, chars) = SymbolPathParser.CacheStatistics();
                if (chars > 262144 || count > 256)
                {
                    Interlocked.Increment(ref violations);
                }
            }
        })).ToList();
        threads.ForEach(t => t.Start());
        threads.ForEach(t => t.Join());

        // Assert: the cache never exceeded its entry or character budget
        Assert.Equal(0, violations);
        var (finalCount, finalChars) = SymbolPathParser.CacheStatistics();
        Assert.InRange(finalChars, 0, 262144);
        Assert.InRange(finalCount, 0, 256);
    }
}
