using DemaConsulting.CanvasNet.Positions;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Viewer.Tests.Imaging;

/// <summary>
///     Unit tests for the feature positions of the SourceLoader class: the selectors filter before the cap, a selector
///     that matches nothing is a warning or a strict error, and the kinds without positions ignore the selectors.
/// </summary>
public class SourceLoaderPositionTests
{
    /// <summary>A flowchart with two nodes.</summary>
    private const string Flowchart = "flowchart LR\n    A[Start] --> B[End]\n";

    /// <summary>
    ///     Writes a text file into a directory.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <param name="name">The file name.</param>
    /// <param name="text">The content.</param>
    /// <returns>The path.</returns>
    private static string Write(TempDirectory directory, string name, string text)
    {
        var path = directory.Combine(name);
        File.WriteAllText(path, text);
        return path;
    }

    /// <summary>
    ///     Builds a bar chart with a title and many rows.
    /// </summary>
    /// <param name="rows">The number of rows.</param>
    /// <returns>The specification.</returns>
    private static string ManyBars(int rows)
    {
        var values = string.Join(',', Enumerable.Range(0, rows).Select(i => $"{{\"a\":{i},\"b\":{i % 7}}}"));
        return "{\"title\":\"Many\",\"width\":600,\"data\":{\"values\":[" + values + "]},\"mark\":\"tick\"," +
               "\"encoding\":{\"x\":{\"field\":\"a\",\"type\":\"quantitative\"},\"y\":{\"field\":\"b\",\"type\":\"quantitative\"}}}";
    }

    /// <summary>
    ///     Test that a diagram reports the positions its selectors match, in the pixels of the loaded surface.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_MermaidWithPositions_ReportsMatchingPositions()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = Write(directory, "flow.mmd", Flowchart);

        // Act
        using var loaded = SourceLoader.Load(path, new SourceOptions { Positions = PositionSelection.Parse(["node:B", "edge"]) });

        // Assert
        Assert.Equal(["B", "L_A_B_0"], loaded.Positions.Select(p => p.Id));
        Assert.False(loaded.PositionsTruncated);
        Assert.DoesNotContain(loaded.Diagnostics, d => d.Code == ErrorCodes.PositionsUnmatched);
        var pixel = loaded.Positions[0].Pixel!.Value;
        Assert.InRange(pixel.X, 0, loaded.Surface.Width);
        Assert.InRange(pixel.Y, 0, loaded.Surface.Height);
    }

    /// <summary>
    ///     Test that the selectors filter before the cap: 1,500 ticks give the title first, then 999 ticks, and the list
    ///     is truncated; a selector whose matches were all cut is not unmatched.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_ChartOverTheCap_FiltersFirstThenCaps()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = Write(directory, "many.vl.json", ManyBars(1500));

        // Act
        using var loaded = SourceLoader.Load(path, new SourceOptions { Positions = PositionSelection.Parse(["mark", "title"]) });

        // Assert
        Assert.Equal(PositionCollector.MaxPositions, loaded.Positions.Count);
        Assert.True(loaded.PositionsTruncated);
        Assert.Equal("title", loaded.Positions[0].Kind);
        Assert.All(loaded.Positions.Skip(1), p => Assert.Equal("mark", p.Kind));
        Assert.DoesNotContain(loaded.Diagnostics, d => d.Code == ErrorCodes.PositionsUnmatched);
    }

    /// <summary>
    ///     Test that a selector that matches nothing is a warning that names it, and with the strict option the error
    ///     <c>positions-unmatched</c> with nothing returned.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_UnmatchedSelector_WarnsOrFailsWhenStrict()
    {
        // Arrange
        using var directory = new TempDirectory();
        var diagram = Write(directory, "flow.mmd", Flowchart);
        var chart = Write(directory, "many.vl.json", ManyBars(3));
        var selection = PositionSelection.Parse(["node:A", "node:Zed"]);

        // Act
        using var loaded = SourceLoader.Load(diagram, new SourceOptions { Positions = selection });
        var mermaidError = Assert.Throws<ViewerException>(() => SourceLoader.Load(diagram, new SourceOptions { Positions = selection, Strict = true }));
        var chartError = Assert.Throws<ViewerException>(() => SourceLoader.Load(chart, new SourceOptions { Positions = PositionSelection.Parse(["axis:z"]), Strict = true }));

        // Assert
        var warning = Assert.Single(loaded.Diagnostics, d => d.Code == ErrorCodes.PositionsUnmatched);
        Assert.Equal(("mermaid", "warning"), (warning.Source, warning.Severity));
        Assert.Contains("'node:Zed'", warning.Message, StringComparison.Ordinal);
        Assert.Equal(["A"], loaded.Positions.Select(p => p.Id));
        Assert.Equal(ErrorCodes.PositionsUnmatched, mermaidError.Code);
        Assert.Equal(ErrorCodes.PositionsUnmatched, chartError.Code);
        Assert.Contains("'axis:z'", chartError.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a kind without positions ignores the selectors: no positions and no warning.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_OtherKinds_IgnoreThePositions()
    {
        // Arrange: an SVG file that embeds the flowchart (an embedded picture never reports positions)
        using var directory = new TempDirectory();
        Write(directory, "flow.mmd", Flowchart);
        var svg = Write(directory, "page.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"200\" height=\"100\"><image href=\"flow.mmd\" width=\"200\" height=\"100\"/></svg>");

        // Act
        using var loaded = SourceLoader.Load(svg, new SourceOptions { Positions = PositionSelection.All, Strict = true });

        // Assert
        Assert.Empty(loaded.Positions);
        Assert.DoesNotContain(loaded.Diagnostics, d => d.Code == ErrorCodes.PositionsUnmatched);
    }
}
