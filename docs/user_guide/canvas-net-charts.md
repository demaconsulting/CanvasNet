# CanvasNet Charts Package

## API Reference

### Chart

`Chart` and its companion types (`ChartSeries`, `ChartAxis`, `ChartLegend`, `ChartTitle`,
`ChartType`, `ChartLegendPosition`) are distributed via the separate
`DemaConsulting.CanvasNet.Charts` NuGet package (namespace `DemaConsulting.CanvasNet.Charts`),
which references the core `DemaConsulting.CanvasNet` package - see the Installation section of
the project README.

`Chart` is an immutable, fully validated description of a chart: its `ChartType`, one or more
`ChartSeries`, and optional `CategoryAxis`/`ValueAxis` (both `ChartAxis`), `Legend`
(`ChartLegend`), `Title` (`ChartTitle`), and default `ColorPalette`. Every constructor argument is
validated eagerly and every caller-supplied collection is defensively copied, so a constructed
`Chart` can be trusted by a renderer without re-validation.

```csharp
public enum ChartType { Bar, Column, Line, Pie, Doughnut, Area }

public enum ChartLegendPosition { Top, Bottom, Left, Right, None }

public sealed class ChartSeries
{
    public ChartSeries(
        string name,
        IReadOnlyList<double> values,
        IReadOnlyList<Rgba32>? pointColors = null,
        IReadOnlyList<string>? pointLabels = null,
        Rgba32? color = null);

    public string Name { get; }
    public IReadOnlyList<double> Values { get; }
    public IReadOnlyList<Rgba32>? PointColors { get; }
    public IReadOnlyList<string>? PointLabels { get; }
    public Rgba32? Color { get; }
}

public sealed class ChartAxis
{
    public ChartAxis(
        IReadOnlyList<string>? labels = null,
        float? minimum = null,
        float? maximum = null,
        float? tickInterval = null,
        string? title = null);

    public IReadOnlyList<string>? Labels { get; }
    public float? Minimum { get; }
    public float? Maximum { get; }
    public float? TickInterval { get; }
    public string? Title { get; }
}

public sealed class ChartLegend
{
    public ChartLegend(ChartLegendPosition position = ChartLegendPosition.Right, bool isVisible = true);

    public ChartLegendPosition Position { get; }
    public bool IsVisible { get; }
}

public sealed class ChartTitle
{
    public ChartTitle(string text, float? fontSize = null);

    public string Text { get; }
    public float? FontSize { get; }
}

public sealed class Chart
{
    public Chart(
        ChartType type,
        IReadOnlyList<ChartSeries> series,
        ChartAxis? categoryAxis = null,
        ChartAxis? valueAxis = null,
        ChartLegend? legend = null,
        ChartTitle? title = null,
        IReadOnlyList<Rgba32>? colorPalette = null);

    public ChartType Type { get; }
    public IReadOnlyList<ChartSeries> Series { get; }
    public ChartAxis? CategoryAxis { get; }
    public ChartAxis? ValueAxis { get; }
    public ChartLegend? Legend { get; }
    public ChartTitle? Title { get; }
    public IReadOnlyList<Rgba32>? ColorPalette { get; }
}
```

For `Bar`, `Column`, `Line`, and `Area` (the category-based chart kinds), when `CategoryAxis` has
non-null `Labels`, every series' `Values.Count` must exactly equal that label count - `Pie` and
`Doughnut` are exempt, since their single series' values represent proportional wedge shares
rather than per-category values.

**Exceptions:**

- `ArgumentNullException`: Thrown when a required reference-type argument (`name`/`values` on
  `ChartSeries`; `text` on `ChartTitle`; `series` on `Chart`) is null.
- `ArgumentException`: Thrown for a blank name/title/text, an empty or null-containing values/
  series/labels collection, a non-finite values entry, a per-point color/label collection whose
  count does not match `Values.Count`, or a series/category-axis label count mismatch.
- `ArgumentOutOfRangeException`: Thrown for an undefined `ChartType`/`ChartLegendPosition` value,
  a non-finite axis minimum/maximum/font size, a tick interval that is not finite or not positive,
  or a minimum not strictly less than a supplied maximum.

### ChartBuilder

`ChartBuilder` is a fluent, mutable-until-`Build` API for assembling a `Chart` without
constructing every nested type by hand. It performs no independent data-shape validation of its
own beyond `Build()` itself: every other invalid argument surfaces the identical exception the
corresponding model type's constructor would throw.

```csharp
public sealed class ChartBuilder
{
    public ChartBuilder OfType(ChartType type);
    public ChartBuilder AddSeries(ChartSeries series);
    public ChartBuilder AddSeries(
        string name,
        IReadOnlyList<double> values,
        IReadOnlyList<Rgba32>? pointColors = null,
        IReadOnlyList<string>? pointLabels = null,
        Rgba32? color = null);
    public ChartBuilder WithCategoryAxis(ChartAxis axis);
    public ChartBuilder WithCategoryAxis(IReadOnlyList<string> labels, string? title = null);
    public ChartBuilder WithValueAxis(ChartAxis axis);
    public ChartBuilder WithValueAxis(
        float? minimum = null, float? maximum = null, float? tickInterval = null, string? title = null);
    public ChartBuilder WithLegend(ChartLegend legend);
    public ChartBuilder WithLegend(ChartLegendPosition position = ChartLegendPosition.Right, bool isVisible = true);
    public ChartBuilder WithTitle(string text, float? fontSize = null);
    public ChartBuilder WithColorPalette(IReadOnlyList<Rgba32> colorPalette);

    public Chart Build();
}
```

Every chainable method returns the same `ChartBuilder` instance (`this`), enabling fluent method
chaining; `AddSeries` preserves insertion order across multiple calls.

**Exceptions:**

- `InvalidOperationException`: Thrown by `Build()` when `OfType` was never called, or when no
  series was added via `AddSeries`.
- Every other exception documented above for the corresponding model type's own constructor.

### ChartRenderer

`ChartRenderer` is distributed in the same `DemaConsulting.CanvasNet.Charts` package as `Chart`.
It is a public static entry point that paints a `Chart` onto a new core `CanvasNet`
`Canvas.Surface`, dispatching per `ChartType` to paint bars/columns/lines/areas/pie or doughnut
wedges, plus axes (for the category-based chart kinds), a legend (at any `ChartLegendPosition`),
a title, and opt-in per-point data labels (`ChartSeries.PointLabels`).

```csharp
public static class ChartRenderer
{
    public static Surface Render(Chart chart, int width, int height, ChartRenderOptions? options = null);

    public static Surface Render(
        Chart chart,
        float widthInches,
        float heightInches,
        float dpi,
        ChartRenderOptions? options = null);
}
```

The first overload renders directly to a caller-specified pixel size. The second computes pixel
dimensions as `round(widthInches * dpi)`/`round(heightInches * dpi)` and delegates to the first -
a `Chart` carries no intrinsic physical page size of its own to scale from (unlike a PDF page or
PPTX slide), so this overload exists purely for callers who prefer to reason in physical
units/DPI the same way `PdfDocument.Render(int, float, PdfRenderOptions?)` does.

A caller with no explicit series/point colors, color palette, or render options still receives a
fully deterministic, legible rendering: `ChartRenderer` resolves each series'/point's color, when
unspecified, from `Chart.ColorPalette`, then `ChartRenderOptions.ColorPalette`, then
`ChartColorPalette.Default` (a documented, fixed 10-entry categorical palette), wrapping by index
via modulo; text is drawn with the bundled Liberation Sans Regular fallback font unless
`ChartRenderOptions.Font` overrides it. A render target too small to reserve a title/legend/axis
band simply skips that band rather than throwing; a legend with more entries than fit its band
omits the entries that do not fit; an over-long category/legend label is truncated to fit.

**Exceptions:**

- `ArgumentNullException`: Thrown when `chart` is null.
- `ArgumentOutOfRangeException`: Thrown when `width`/`height` is outside `Surface`'s own supported
  range (propagated from `Surface`'s own constructor); thrown by the DPI overload when
  `widthInches`/`heightInches`/`dpi` is not finite or not positive, or when the computed pixel
  width/height would exceed `Surface.MaxDimension`.

### ChartRenderOptions

`ChartRenderOptions` groups every optional rendering setting `ChartRenderer.Render` accepts, as a
single, extensible options parameter - mirroring `PptxRenderOptions`'s own init-property/
`Default`-singleton shape.

```csharp
public sealed class ChartRenderOptions
{
    public static readonly ChartRenderOptions Default;

    public Rgba32 BackgroundColor { get; init; } // default: opaque white
    public TrueTypeFont? Font { get; init; } // default: null (bundled fallback font)
    public float TitleFontSize { get; init; } // default: 18
    public float AxisFontSize { get; init; } // default: 11
    public float LegendFontSize { get; init; } // default: 11
    public float DataLabelFontSize { get; init; } // default: 10
    public IReadOnlyList<Rgba32>? ColorPalette { get; init; } // default: null
}
```

Passing `null` (or omitting the `options` parameter entirely) to either `ChartRenderer.Render`
overload uses `ChartRenderOptions.Default`.

### OpenXmlChartParser

`OpenXmlChartParser` is distributed in the same `DemaConsulting.CanvasNet.Charts` package as
`Chart`, in the `DemaConsulting.CanvasNet.Charts.OpenXml` namespace. It is a public static entry
point that parses a raw ECMA-376 DrawingML-Charts `c:chartSpace` (or bare `c:chart`)
`System.Xml.Linq` element into a validated `Chart`, directly consumable by `ChartRenderer.Render`
with no further adaptation. It is format-agnostic: it has no knowledge of OPC/ZIP packaging or
any host document format (for example, PresentationML/`.pptx`) - a caller is responsible for
first locating and opening the relevant `chart#.xml` part and handing this parser only the
resulting XML content.

```csharp
public static class OpenXmlChartParser
{
    public static Chart Parse(XDocument chartDocument);
    public static Chart Parse(XElement chartSpaceOrChartElement);
}
```

`OpenXmlChartParser` reads only cached values (`c:numCache`/`c:strCache`) - it never recomputes a
value from a sibling `c:f` formula - and supports `c:barChart` (bar/column, classified by
`c:barDir`), `c:lineChart`, `c:pieChart`, `c:doughnutChart`, and `c:areaChart`, extracting series
names/categories/values, the chart title, axis titles/range, and legend presence/position.

#### Exceptions

- `ArgumentNullException`: Thrown when `chartDocument`/`chartSpaceOrChartElement` is null.
- `ArgumentException`: Thrown for a document with no root element, an element that is neither a
  `c:chart` element nor one containing one, or a `c:chart` with no `c:plotArea` child.
- `ChartUnsupportedFeatureException`: Thrown - carrying a short, stable `Feature` token - for a
  recognized-but-unimplemented chart kind (radar, bubble, scatter, stock, surface, 3-D, or "of
  pie"), a combo chart (more than one recognized chart-type element in one plot area), a plot
  area with no recognized chart-type element at all, or a series whose value has no cached
  `c:numCache`.

```csharp
public class ChartUnsupportedFeatureException : IOException
{
    public ChartUnsupportedFeatureException();
    public ChartUnsupportedFeatureException(string message);
    public ChartUnsupportedFeatureException(string message, Exception innerException);
    public ChartUnsupportedFeatureException(string feature, string message);
    public ChartUnsupportedFeatureException(string feature, string message, Exception innerException);

    public string Feature { get; } // default: string.Empty
}
```

When rendering a `.pptx` presentation via the separate `DemaConsulting.CanvasNet.Pptx` package's
`PptxDocument.Render`, an embedded `<p:graphicFrame>` chart is automatically located, parsed via
`OpenXmlChartParser`, rendered via `ChartRenderer`, and composited onto the slide - no extra
caller code is required beyond calling `PptxDocument.Render` itself.

## Examples

### Example 15: Building and Rendering a Chart

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Charts;

// Build a validated Chart fluently: a two-category Column chart with one series.
var chart = new ChartBuilder()
    .OfType(ChartType.Column)
    .WithCategoryAxis(["Q1", "Q2"])
    .WithValueAxis(minimum: 0f, maximum: 10f)
    .AddSeries("Revenue", [4.0, 9.0], color: new Rgba32(31, 119, 180, 255))
    .WithTitle("Quarterly Revenue")
    .WithLegend(ChartLegendPosition.Bottom)
    .Build();

// Render it onto a new 400x300 surface using every rendering default.
using var chartSurface = ChartRenderer.Render(chart, 400, 300);
Console.WriteLine($"{chartSurface.Width}x{chartSurface.Height}"); // Output: 400x300
```

### Example 16: Parsing and Rendering an OOXML Chart

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Charts;
using DemaConsulting.CanvasNet.Charts.OpenXml;
using System.Xml.Linq;

// Parse a c:chartSpace/c:chart XML document (for example, a chart1.xml OPC part a host
// document-format library has already located and opened). This hand-authored fragment mirrors
// the shape a real PowerPoint chart part's bar chart would carry.
var chartDocument = XDocument.Parse("""
    <c:chartSpace xmlns:c="http://schemas.openxmlformats.org/drawingml/2006/chart"
                  xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
      <c:chart>
        <c:plotArea>
          <c:barChart>
            <c:barDir val="col"/>
            <c:ser>
              <c:tx><c:v>Revenue</c:v></c:tx>
              <c:cat>
                <c:strRef>
                  <c:strCache>
                    <c:ptCount val="2"/>
                    <c:pt idx="0"><c:v>Q1</c:v></c:pt>
                    <c:pt idx="1"><c:v>Q2</c:v></c:pt>
                  </c:strCache>
                </c:strRef>
              </c:cat>
              <c:val>
                <c:numRef>
                  <c:numCache>
                    <c:ptCount val="2"/>
                    <c:pt idx="0"><c:v>4.0</c:v></c:pt>
                    <c:pt idx="1"><c:v>9.0</c:v></c:pt>
                  </c:numCache>
                </c:numRef>
              </c:val>
            </c:ser>
          </c:barChart>
        </c:plotArea>
        <c:legend><c:legendPos val="b"/></c:legend>
      </c:chart>
    </c:chartSpace>
    """);
var parsedChart = OpenXmlChartParser.Parse(chartDocument);
Console.WriteLine(parsedChart.Type); // Output: Column

// The parsed Chart is directly consumable by ChartRenderer, exactly like a Chart built via
// ChartBuilder.
using var parsedChartSurface = ChartRenderer.Render(parsedChart, 400, 300);
Console.WriteLine($"{parsedChartSurface.Width}x{parsedChartSurface.Height}"); // Output: 400x300
```
