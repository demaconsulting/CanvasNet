namespace DemaConsulting.CanvasNet.VegaLite;


/// <summary>
///     The <see cref="DemaConsulting.CanvasNet.VegaLite"/> namespace renders charts written in the Vega-Lite
///     specification language onto a CanvasNet surface. It reads a specification from a string, a stream or a file and
///     produces a validated document (<see cref="VegaLiteDocument"/>) that describes the chart
///     (<see cref="VegaLiteDocument.SchemaUrl"/>, <see cref="VegaLiteDocument.Title"/>,
///     <see cref="VegaLiteDocument.MarkTypes"/>, <see cref="VegaLiteDocument.DataReferences"/>,
///     <see cref="VegaLiteDocument.ParameterNames"/>) or fails with a <see cref="VegaLiteSpecException"/> carrying a
///     precise <see cref="VegaLiteLocation"/> (file, JSON pointer, line and column). The document can be measured
///     (<see cref="VegaLiteDocument.Measure(VegaLiteRenderOptions?, System.Threading.CancellationToken)"/>) and drawn onto a
///     surface (<see cref="VegaLiteDocument.Render(VegaLiteRenderOptions?, System.Threading.CancellationToken)"/>) under
///     the options of <see cref="VegaLiteRenderOptions"/>, with warnings (<see cref="VegaLiteDiagnostic"/>) and
///     unsupported features (<see cref="VegaLiteUnsupportedFeature"/>) reported through handlers or, in strict mode, a
///     <see cref="VegaLiteUnsupportedFeatureException"/>.
/// </summary>
/// <remarks>
///     <para>
///         Open behavior and the data sandbox are configured through <see cref="VegaLiteOpenOptions"/>, rendering through
///         <see cref="VegaLiteRenderOptions"/> and resource budgets through <see cref="VegaLiteLimits"/>. The
///         specification model, data, expression, scale, guide, layout, mark and scene-graph stages are internal; only the
///         document surface is public so that the internals can evolve.
///     </para>
///     <para>
///         Rendering is static, offline and deterministic: nothing is fetched from the network, no script runs, fonts
///         come from the bundled Liberation families unless the caller opts in to more, and two renders of the same
///         document and options are byte-identical.
///     </para>
///     <para>
///         Third-party provenance and attribution: this package implements the public Vega-Lite specification
///         language and is not affiliated with or endorsed by the Vega project or the University of Washington
///         Interactive Data Lab. The specification model, normalization, layout structure and marks are original
///         code written from the public documentation; a small number of default values, layout constants and numeric
///         algorithms are ported from Vega and Vega-Lite (BSD-3-Clause) and d3 (ISC) with attribution in the
///         <c>third-party</c> folder, whose license texts ship in the package under <c>licenses\</c>.
///     </para>
///     <para>
///         See <see cref="VegaLiteDocument"/> for the entry points. A chart is drawn onto a
///         <see cref="DemaConsulting.CanvasNet.Canvas.Surface"/> (<see cref="DemaConsulting.CanvasNet.Codecs"/> saves it),
///         and the files a chart names (<c>data.url</c>) are read through the sandbox of
///         <see cref="DemaConsulting.CanvasNet.Files"/>. <see cref="VegaLiteImageSource"/> lets an SVG document draw a
///         <c>.vl.json</c> chart through <see cref="DemaConsulting.CanvasNet.ImageSources"/>, and the
///         <c>DemaConsulting.CanvasNet.Viewer</c> package renders and compares charts with the rest of its formats. For
///         charts built in code rather than described in JSON, see the <c>DemaConsulting.CanvasNet.Charts</c> namespace of
///         <c>DemaConsulting.CanvasNet.Charts</c>.
///     </para>
/// </remarks>
/// <example>
///     Parse a specification, read what it declares, and draw it at its natural size:
///     <code language="csharp">
///     using DemaConsulting.CanvasNet.Codecs;
///     using DemaConsulting.CanvasNet.VegaLite;
///
///     var chart = VegaLiteDocument.Parse("""
///         {
///           "$schema": "https://vega.github.io/schema/vega-lite/v5.json",
///           "title": "Crates sold per stall",
///           "data": { "values": [ { "stall": "North", "crates": 28 }, { "stall": "South", "crates": 55 } ] },
///           "mark": "bar",
///           "encoding": {
///             "x": { "field": "stall", "type": "nominal" },
///             "y": { "field": "crates", "type": "quantitative" }
///           }
///         }
///         """);
///     Console.WriteLine($"{chart.Title}: {string.Join(", ", chart.MarkTypes)}");
///     using var image = chart.Render();
///     PngCodec.Save(image, "crates.png");
///     </code>
/// </example>
/// <example>
///     Give a parameter a value from the host: an expression reference (<c>{"expr": ...}</c>) is evaluated once with the parameters, and a parameter can be the domain of a scale
///     (the host gives an array, here <c>[20, 60]</c>, instead of the <c>value</c> of the specification):
///     <code language="csharp">
///     using DemaConsulting.CanvasNet.Codecs;
///     using DemaConsulting.CanvasNet.VegaLite;
///
///     var chart = VegaLiteDocument.Parse("""
///         {
///           "params": [ { "name": "window", "value": [0, 100] }, { "name": "size", "value": 14 } ],
///           "title": { "text": "Readings", "fontSize": { "expr": "size" } },
///           "data": { "values": [ { "t": 10, "v": 1 }, { "t": 40, "v": 2 }, { "t": 90, "v": 3 } ] },
///           "mark": "circle",
///           "encoding": {
///             "x": { "field": "t", "type": "quantitative", "scale": { "domain": { "param": "window" } } },
///             "y": { "field": "v", "type": "quantitative" }
///           }
///         }
///         """);
///     var options = new VegaLiteRenderOptions
///     {
///         Parameters = new Dictionary&lt;string, object?&gt; { ["window"] = new object[] { 20, 60 }, ["size"] = 18 },
///     };
///     using var image = chart.Render(options);
///     PngCodec.Save(image, "readings.png");
///     </code>
/// </example>
/// <example>
///     Draw an interval selection in its initial state: its <c>value</c> (or the one the host gives) is the brush rectangle, the marks inside it are red, and a second view below
///     bins by the selected range (the span of the range chooses the width of the bins):
///     <code language="csharp">
///     using DemaConsulting.CanvasNet.Codecs;
///     using DemaConsulting.CanvasNet.VegaLite;
///
///     var chart = VegaLiteDocument.Parse("""
///         {
///           "data": { "values": [ { "a": 1 }, { "a": 2.5 }, { "a": 4 }, { "a": 8 } ] },
///           "vconcat": [
///             {
///               "params": [ { "name": "brush", "select": { "type": "interval", "encodings": ["x"] }, "value": { "x": [2, 5] } } ],
///               "mark": "circle",
///               "encoding": {
///                 "x": { "field": "a", "type": "quantitative" },
///                 "color": { "condition": { "param": "brush", "value": "firebrick" }, "value": "steelblue" }
///               }
///             },
///             {
///               "mark": "bar",
///               "encoding": {
///                 "x": { "field": "a", "type": "quantitative", "bin": { "extent": { "param": "brush" } } },
///                 "y": { "aggregate": "count" }
///               }
///             }
///           ]
///         }
///         """);
///     var options = new VegaLiteRenderOptions
///     {
///         Parameters = new Dictionary&lt;string, object?&gt;
///         {
///             ["brush"] = new Dictionary&lt;string, object?&gt; { ["x"] = new object[] { 3, 6 } },
///         },
///     };
///     using var image = chart.Render(options);
///     PngCodec.Save(image, "selection.png");
///     </code>
/// </example>
internal static class NamespaceDoc
{
}
