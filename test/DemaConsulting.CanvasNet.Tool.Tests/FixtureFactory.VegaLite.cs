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
}
