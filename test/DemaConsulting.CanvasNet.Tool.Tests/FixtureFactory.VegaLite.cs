namespace DemaConsulting.CanvasNet.Tool.Tests;

/// <summary>
///     Original Vega-Lite charts for the tests, written as text into a directory the caller supplies.
/// </summary>
/// <remarks>
///     Every chart is a few lines written for these tests (inline data, or the one small CSV text beside a specification
///     that the test writes to a temporary folder; no remote URL is ever fetched, no custom font), so
///     there are no licensing constraints beyond the repository's own license. <c>baskets.vl.json</c> in the
///     <c>Fixtures</c> folder is the one chart that is a file.
/// </remarks>
internal static partial class FixtureFactory
{
    /// <summary>
    ///     The mark color a Vega-Lite bar has when the chart does not choose one.
    /// </summary>
    public const uint BarColor = 0x4C78A8;

    /// <summary>
    ///     A bar chart whose data rectangle is exactly 200 by 100 pixels: two bars of 3 and 5, so the default y domain
    ///     is 0 to 5, and the bars fill 60 and 100 percent of the height. There is no title and no legend, so the
    ///     chart has the default padding of 5 pixels on its top and right sides, whatever the width of its axes.
    /// </summary>
    public const string TwoBarChart =
        """
        {
          "$schema": "https://vega.github.io/schema/vega-lite/v5.json",
          "width": 200,
          "height": 100,
          "data": { "values": [ { "k": "a", "v": 3 }, { "k": "b", "v": 5 } ] },
          "mark": "bar",
          "encoding": {
            "x": { "field": "k", "type": "nominal" },
            "y": { "field": "v", "type": "quantitative" }
          }
        }
        """;

    /// <summary>
    ///     The rows of <see cref="TwoBarChart"/> as the CSV file that <see cref="TwoBarFileChart"/> names.
    /// </summary>
    public const string TwoBarCsv = "k,v\na,3\nb,5\n";

    /// <summary>
    ///     A bar chart with a <c>shape</c> channel that a bar mark does not draw: it renders with exactly one
    ///     diagnostic, the unsupported <c>vl-channel-shape</c>.
    /// </summary>
    public const string ShapeOnBarChart =
        """
        {
          "width": 120,
          "height": 80,
          "data": { "values": [ { "x": 1, "y": 2, "c": "a" }, { "x": 2, "y": 3, "c": "b" } ] },
          "mark": "bar",
          "encoding": {
            "x": { "field": "x", "type": "quantitative" },
            "y": { "field": "y", "type": "quantitative" },
            "shape": { "field": "c", "type": "nominal" }
          }
        }
        """;

    /// <summary>
    ///     The same chart as <see cref="TwoBarChart"/> with its rows in the file <c>bars.csv</c> beside the specification,
    ///     so it renders identically when the file is found.
    /// </summary>
    public const string TwoBarFileChart =
        """
        {
          "$schema": "https://vega.github.io/schema/vega-lite/v5.json",
          "width": 200,
          "height": 100,
          "data": { "url": "bars.csv" },
          "mark": "bar",
          "encoding": {
            "x": { "field": "k", "type": "nominal" },
            "y": { "field": "v", "type": "quantitative" }
          }
        }
        """;

    /// <summary>
    ///     Two charts of <see cref="TwoBarChart"/>'s kind side by side (an <c>hconcat</c> that shares the data): each has
    ///     a data rectangle of 200 by 100 pixels and its own axes, so the picture is more than 400 pixels wide, and both
    ///     draw bars in <see cref="BarColor"/>.
    /// </summary>
    public const string TwoChartHConcat =
        """
        {
          "$schema": "https://vega.github.io/schema/vega-lite/v5.json",
          "data": { "values": [ { "k": "a", "v": 3 }, { "k": "b", "v": 5 } ] },
          "hconcat": [
            {
              "width": 200,
              "height": 100,
              "mark": "bar",
              "encoding": {
                "x": { "field": "k", "type": "nominal" },
                "y": { "field": "v", "type": "quantitative" }
              }
            },
            {
              "width": 200,
              "height": 100,
              "mark": "bar",
              "encoding": {
                "x": { "field": "k", "type": "nominal" },
                "y": { "field": "v", "type": "quantitative" }
              }
            }
          ]
        }
        """;

    /// <summary>
    ///     A bar chart faceted into one column for each value of <c>g</c> (<c>u</c> and <c>v</c>): two cells with a data
    ///     rectangle of 100 by 100 pixels, a header label above each, and the shared y axis once at the left; both cells
    ///     draw bars in <see cref="BarColor"/>.
    /// </summary>
    public const string TwoColumnFacetChart =
        """
        {
          "$schema": "https://vega.github.io/schema/vega-lite/v5.json",
          "data": { "values": [
            { "g": "u", "k": "a", "v": 3 }, { "g": "u", "k": "b", "v": 5 },
            { "g": "v", "k": "a", "v": 5 }, { "g": "v", "k": "b", "v": 3 }
          ] },
          "facet": { "column": { "field": "g", "type": "nominal" } },
          "spec": {
            "width": 100,
            "height": 100,
            "mark": "bar",
            "encoding": {
              "x": { "field": "k", "type": "nominal" },
              "y": { "field": "v", "type": "quantitative" }
            }
          }
        }
        """;
}
