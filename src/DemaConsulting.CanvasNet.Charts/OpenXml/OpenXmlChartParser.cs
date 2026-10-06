using System.Globalization;
using System.Xml.Linq;

namespace DemaConsulting.CanvasNet.Charts.OpenXml;

/// <summary>
///     Parses a raw ECMA-376 DrawingML-Charts <c>c:chartSpace</c> (or bare <c>c:chart</c>)
///     element into a validated <see cref="Chart"/>, reading only cached values.
/// </summary>
/// <remarks>
///     <para>
///     <see cref="OpenXmlChartParser"/> is this namespace's one public entry point. It supports
///     the five chart kinds the <see cref="Charts"/> namespace's <see cref="ChartType"/>
///     enumeration models - <c>c:barChart</c> (classified as <see cref="ChartType.Bar"/> or
///     <see cref="ChartType.Column"/> by its <c>c:barDir</c> value), <c>c:lineChart</c>
///     (<see cref="ChartType.Line"/>), <c>c:pieChart</c> (<see cref="ChartType.Pie"/>),
///     <c>c:doughnutChart</c> (<see cref="ChartType.Doughnut"/>), and <c>c:areaChart</c>
///     (<see cref="ChartType.Area"/>) - and throws <see cref="ChartUnsupportedFeatureException"/>
///     for every other schema-legal plot-area shape: a recognized-but-unimplemented chart kind
///     (radar/bubble/scatter/stock/surface/3-D/"of pie"), a combo chart (more than one chart-type
///     element sharing a single plot area), a plot area with no recognized chart-type element at
///     all, and a series whose value cache (<c>c:numCache</c>) is absent.
///     </para>
///     <para>
///     This parser performs no data-shape validation of its own beyond the cached-values-only and
///     chart-kind-classification checks described above: every other invariant (non-empty series,
///     finite values, matching category/value counts, and so on) is enforced exactly once, by
///     <see cref="Chart"/>'s and its constituent types' own validating constructors, which this
///     parser ultimately calls - mirroring <see cref="ChartBuilder"/>'s own "delegate every
///     validation rule to the model constructors" convention. An OOXML document whose cached
///     series/category counts disagree therefore surfaces the identical
///     <see cref="ArgumentException"/> the corresponding model constructor documents, not a
///     parser-specific exception.
///     </para>
/// </remarks>
public static class OpenXmlChartParser
{
    /// <summary>The DrawingML-Charts XML namespace (prefix <c>c:</c> in a real <c>chart#.xml</c> part).</summary>
    private static readonly XNamespace ChartNs = "http://schemas.openxmlformats.org/drawingml/2006/chart";

    /// <summary>The DrawingML-Main XML namespace (prefix <c>a:</c>), used only for a rich-text title's <c>a:t</c> runs.</summary>
    private static readonly XNamespace DrawingNs = "http://schemas.openxmlformats.org/drawingml/2006/main";

    /// <summary>
    ///     The maximum <c>c:ptCount</c> value <see cref="ParseNumCache"/>/<see cref="ParseStrCache"/>
    ///     will allocate a dense array for, chosen to match Excel's own maximum worksheet row
    ///     count (2^20 = 1,048,576) - the practical upper bound on how many cached points a
    ///     genuine chart backed by real worksheet data could ever declare.
    /// </summary>
    /// <remarks>
    ///     <c>c:ptCount</c> is read directly from the untrusted chart part with no inherent upper
    ///     bound of its own; without this cap, a crafted/corrupted value (for example
    ///     <c>2000000000</c>) would drive an attacker-controlled <c>new double[ptCount]</c>/
    ///     <c>new string[ptCount]</c> allocation - up to ~16 GB per series - causing an
    ///     <see cref="OutOfMemoryException"/> rather than a clean, fail-closed rejection.
    /// </remarks>
    private const int MaxCachedPointCount = 1_048_576;

    /// <summary>
    ///     The local names of every ECMA-376-defined plot-area chart-type element this parser
    ///     supports, mapped to a function that classifies the element's own <see cref="ChartType"/>.
    /// </summary>
    /// <remarks>
    ///     A single lookup table (rather than scattered <see langword="if"/> checks) is the one
    ///     place a future chart-type addition needs to edit - see
    ///     <see cref="UnsupportedChartTypeFeatureTokens"/> for its unsupported-type counterpart.
    /// </remarks>
    private static readonly IReadOnlyDictionary<string, Func<XElement, ChartType>> SupportedChartTypeClassifiers =
        new Dictionary<string, Func<XElement, ChartType>>
        {
            ["barChart"] = ClassifyBarChart,
            ["lineChart"] = static _ => ChartType.Line,
            ["pieChart"] = static _ => ChartType.Pie,
            ["doughnutChart"] = static _ => ChartType.Doughnut,
            ["areaChart"] = static _ => ChartType.Area
        };

    /// <summary>
    ///     The local names of every ECMA-376-defined plot-area chart-type element this parser
    ///     recognizes but does not yet implement, mapped to the short, stable
    ///     <see cref="ChartUnsupportedFeatureException.Feature"/> token thrown for it.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> UnsupportedChartTypeFeatureTokens =
        new Dictionary<string, string>
        {
            ["radarChart"] = "charts-openxml-radar-chart",
            ["scatterChart"] = "charts-openxml-scatter-chart",
            ["bubbleChart"] = "charts-openxml-bubble-chart",
            ["stockChart"] = "charts-openxml-stock-chart",
            ["surfaceChart"] = "charts-openxml-surface-chart",
            ["surface3DChart"] = "charts-openxml-surface-chart",
            ["bar3DChart"] = "charts-openxml-3d-chart",
            ["line3DChart"] = "charts-openxml-3d-chart",
            ["pie3DChart"] = "charts-openxml-3d-chart",
            ["area3DChart"] = "charts-openxml-3d-chart",
            ["ofPieChart"] = "charts-openxml-of-pie-chart"
        };

    /// <summary>
    ///     Parses <paramref name="chartDocument"/>'s root element into a validated
    ///     <see cref="Chart"/>. See <see cref="Parse(XElement)"/> for the full parsing contract.
    /// </summary>
    /// <param name="chartDocument">
    ///     The chart XML document to parse, typically loaded directly from a <c>chart#.xml</c>
    ///     part's content. Must not be <see langword="null"/>, and must have a root element.
    /// </param>
    /// <returns>The parsed, validated <see cref="Chart"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="chartDocument"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="chartDocument"/> has no root element, or propagated
    ///     directly from a model constructor - see <see cref="Parse(XElement)"/>.
    /// </exception>
    /// <exception cref="ChartUnsupportedFeatureException">See <see cref="Parse(XElement)"/>.</exception>
    public static Chart Parse(XDocument chartDocument)
    {
        ArgumentNullException.ThrowIfNull(chartDocument);

        if (chartDocument.Root is null)
        {
            throw new ArgumentException("The chart XML document must have a root element.", nameof(chartDocument));
        }

        return Parse(chartDocument.Root);
    }

    /// <summary>
    ///     Parses <paramref name="chartSpaceOrChartElement"/> into a validated <see cref="Chart"/>.
    /// </summary>
    /// <param name="chartSpaceOrChartElement">
    ///     Either a full <c>c:chartSpace</c> root element (its own <c>c:chart</c> child is
    ///     located and parsed) or a bare <c>c:chart</c> element directly (supports hand-authored
    ///     fragments that omit the <c>c:chartSpace</c> wrapper, such as this type's own unit
    ///     tests). Must not be <see langword="null"/>.
    /// </param>
    /// <returns>
    ///     The parsed, validated <see cref="Chart"/>: its <see cref="Chart.Type"/> is classified
    ///     from the plot area's single recognized chart-type element; its <see cref="Chart.Series"/>
    ///     are built from that element's <c>c:ser</c> children (name from <c>c:tx</c>, defaulting
    ///     to <c>"Series N</c> (1-based) when absent; values from <c>c:val</c>'s cached
    ///     <c>c:numCache</c>, honoring <c>c:ptCount</c>/gaps as <c>0.0</c>); its
    ///     <see cref="Chart.CategoryAxis"/> is populated, for a category-based
    ///     <see cref="ChartType"/>, from the first series' <c>c:cat</c> cache (a value-based
    ///     type's categories are instead mapped to each series' <see cref="ChartSeries.PointLabels"/>,
    ///     since <see cref="Chart"/> has no per-point-category concept for
    ///     <see cref="ChartType.Pie"/>/<see cref="ChartType.Doughnut"/>); its
    ///     <see cref="Chart.Title"/> is populated from <c>c:title</c>'s cached text, or
    ///     <see langword="null"/> when <c>c:title</c> is absent or present without cached text (a
    ///     styled-but-textless PowerPoint auto-title placeholder); its <see cref="Chart.Legend"/>
    ///     is populated from <c>c:legend</c>/<c>c:legendPos</c> (mapped to the nearest of
    ///     <see cref="ChartLegend"/>'s five defined positions - see this type's remarks), or
    ///     <see langword="null"/> when <c>c:legend</c> is absent; and its
    ///     <see cref="Chart.CategoryAxis"/>/<see cref="Chart.ValueAxis"/> titles/range are
    ///     populated from <c>c:catAx</c>/<c>c:valAx</c>'s <c>c:title</c>/<c>c:scaling</c>/
    ///     <c>c:majorUnit</c> when present.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="chartSpaceOrChartElement"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="chartSpaceOrChartElement"/> has no <c>c:chart</c> child
    ///     (and is not itself a <c>c:chart</c> element), or when it has no <c>c:plotArea</c>
    ///     child; or propagated directly from a model constructor (for example a series/
    ///     category-axis length mismatch, or an inverted value-axis range).
    /// </exception>
    /// <exception cref="ChartUnsupportedFeatureException">
    ///     Thrown when the plot area contains no recognized chart-type element
    ///     (<c>"charts-openxml-no-chart-type</c>), more than one recognized chart-type element
    ///     (<c>"charts-openxml-combo-chart</c>), a single recognized-but-unimplemented chart-type
    ///     element (a dedicated token per kind - see <see cref="UnsupportedChartTypeFeatureTokens"/>),
    ///     or a series whose <c>c:val</c> has no cached <c>c:numCache</c>
    ///     (<c>"charts-openxml-uncached-values</c>).
    /// </exception>
    public static Chart Parse(XElement chartSpaceOrChartElement)
    {
        ArgumentNullException.ThrowIfNull(chartSpaceOrChartElement);

        // Accept either a full c:chartSpace root (locate its c:chart child) or a bare c:chart
        // element directly, so a hand-authored test fragment need not reproduce the wrapper.
        var chartElement = chartSpaceOrChartElement.Name == ChartNs + "chart"
            ? chartSpaceOrChartElement
            : chartSpaceOrChartElement.Element(ChartNs + "chart");
        if (chartElement is null)
        {
            throw new ArgumentException(
                "The supplied element must be a c:chart element, or a c:chartSpace element containing one.",
                nameof(chartSpaceOrChartElement));
        }

        var plotArea = chartElement.Element(ChartNs + "plotArea")
            ?? throw new ArgumentException("The c:chart element must contain a c:plotArea child.", nameof(chartSpaceOrChartElement));

        // Classify the plot area's single recognized chart-type element up front - this also
        // detects and rejects a combo chart or an unrecognized/absent chart type before any
        // series parsing is attempted.
        var (type, chartTypeElement) = ClassifyPlotArea(plotArea);
        var isCategoryBased = IsCategoryBased(type);

        var seriesElements = chartTypeElement.Elements(ChartNs + "ser").ToList();
        var series = new List<ChartSeries>(seriesElements.Count);
        for (var i = 0; i < seriesElements.Count; i++)
        {
            series.Add(ParseSeries(seriesElements[i], i, isCategoryBased));
        }

        var categoryAxis = isCategoryBased
            ? BuildCategoryAxis(seriesElements, plotArea)
            : null;
        var valueAxis = ParseValueAxis(plotArea);
        var title = ParseTitle(chartElement.Element(ChartNs + "title"));
        var legend = ParseLegend(chartElement.Element(ChartNs + "legend"));

        return new Chart(type, series, categoryAxis, valueAxis, legend, title);
    }

    /// <summary>
    ///     Determines whether <paramref name="type"/> associates each series' values one-for-one
    ///     with a category axis label (as opposed to a proportional wedge share). Mirrors
    ///     <c>Chart</c>'s own private identically-named check, since this parser needs the same
    ///     classification to decide whether parsed categories become a shared
    ///     <see cref="Chart.CategoryAxis"/> or each series' own <see cref="ChartSeries.PointLabels"/>.
    /// </summary>
    /// <param name="type">The chart type to classify.</param>
    /// <returns><see langword="true"/> for a category-based type; otherwise, <see langword="false"/>.</returns>
    private static bool IsCategoryBased(ChartType type) =>
        type is ChartType.Bar or ChartType.Column or ChartType.Line or ChartType.Area;

    /// <summary>
    ///     Locates and classifies the plot area's single recognized chart-type element.
    /// </summary>
    /// <param name="plotArea">The <c>c:plotArea</c> element to classify.</param>
    /// <returns>The classified <see cref="ChartType"/> and the chart-type element it was classified from.</returns>
    /// <exception cref="ChartUnsupportedFeatureException">
    ///     Thrown when zero, more than one, or a recognized-but-unimplemented chart-type element
    ///     is found - see <see cref="Parse(XElement)"/>'s own exception documentation.
    /// </exception>
    private static (ChartType Type, XElement Element) ClassifyPlotArea(XElement plotArea)
    {
        var candidates = plotArea.Elements()
            .Where(e => SupportedChartTypeClassifiers.ContainsKey(e.Name.LocalName) ||
                        UnsupportedChartTypeFeatureTokens.ContainsKey(e.Name.LocalName))
            .ToList();

        switch (candidates.Count)
        {
            case 0:
                throw new ChartUnsupportedFeatureException(
                    "charts-openxml-no-chart-type",
                    "The plot area contains no recognized chart-type element.");
            case > 1:
                throw new ChartUnsupportedFeatureException(
                    "charts-openxml-combo-chart",
                    "A combo chart (multiple chart-type elements sharing one plot area) is not supported.");
        }

        var element = candidates[0];
        var localName = element.Name.LocalName;
        if (UnsupportedChartTypeFeatureTokens.TryGetValue(localName, out var feature))
        {
            throw new ChartUnsupportedFeatureException(feature, $"The '{localName}' chart type is not supported.");
        }

        return (SupportedChartTypeClassifiers[localName](element), element);
    }

    /// <summary>
    ///     Classifies a <c>c:barChart</c> element as <see cref="ChartType.Bar"/> when its
    ///     <c>c:barDir</c> value is <c>"bar</c>, or <see cref="ChartType.Column"/> for
    ///     <c>"col</c> or any other/absent value - <c>"col</c> is the ECMA-376-documented
    ///     default when <c>c:barDir</c> is omitted, even though the schema in fact requires it.
    /// </summary>
    /// <param name="barChart">The <c>c:barChart</c> element.</param>
    /// <returns><see cref="ChartType.Bar"/> or <see cref="ChartType.Column"/>.</returns>
    private static ChartType ClassifyBarChart(XElement barChart)
    {
        var barDir = (string?)barChart.Element(ChartNs + "barDir")?.Attribute("val");
        return barDir == "bar" ? ChartType.Bar : ChartType.Column;
    }

    /// <summary>
    ///     Parses a single <c>c:ser</c> element into a validated <see cref="ChartSeries"/>.
    /// </summary>
    /// <param name="ser">The <c>c:ser</c> element to parse.</param>
    /// <param name="index">The series' zero-based position within the chart-type element, used for its default name.</param>
    /// <param name="isCategoryBased">
    ///     <see langword="true"/> when the owning chart is a category-based type (categories are
    ///     reported via the chart's shared category axis instead of per-series point labels);
    ///     <see langword="false"/> for a value-based type (Pie/Doughnut), whose categories become
    ///     this series' own <see cref="ChartSeries.PointLabels"/>.
    /// </param>
    /// <returns>The parsed, validated <see cref="ChartSeries"/>.</returns>
    /// <exception cref="ChartUnsupportedFeatureException">
    ///     Thrown when the series' <c>c:val</c> has no cached <c>c:numCache</c> values (feature
    ///     token <c>"charts-openxml-uncached-values</c>).
    /// </exception>
    private static ChartSeries ParseSeries(XElement ser, int index, bool isCategoryBased)
    {
        var name = GetTxText(ser.Element(ChartNs + "tx")) ?? $"Series {index + 1}";

        var numCache = ser.Element(ChartNs + "val")?.Element(ChartNs + "numRef")?.Element(ChartNs + "numCache");
        if (numCache is null)
        {
            throw new ChartUnsupportedFeatureException(
                "charts-openxml-uncached-values",
                $"Series '{name}' has no cached c:numCache values; only cached values are supported.");
        }

        var values = ParseNumCache(numCache);

        string[]? pointLabels = null;
        if (!isCategoryBased)
        {
            pointLabels = ParseCategoryLabels(ser.Element(ChartNs + "cat"));
        }

        return new ChartSeries(name, values, pointLabels: pointLabels);
    }

    /// <summary>
    ///     Builds the chart's shared category axis from the first series' <c>c:cat</c> cache and
    ///     the plot area's <c>c:catAx</c> title, for a category-based <see cref="ChartType"/>.
    /// </summary>
    /// <param name="seriesElements">The chart-type element's <c>c:ser</c> children, in order.</param>
    /// <param name="plotArea">The <c>c:plotArea</c> element (consulted for <c>c:catAx</c>'s title).</param>
    /// <returns>
    ///     A <see cref="ChartAxis"/> populated from whichever of the first series' categories and
    ///     the category axis title are present, or <see langword="null"/> when neither is present.
    /// </returns>
    /// <remarks>
    ///     <see cref="Chart"/> models a single, shared category axis for every series, while
    ///     OOXML attaches a <c>c:cat</c> cache to each individual <c>c:ser</c>; in valid OOXML
    ///     every series of one chart-type element shares identical categories, so only the first
    ///     series' <c>c:cat</c> is consulted. A series whose own category count disagrees with
    ///     another surfaces <see cref="Chart"/>'s own documented
    ///     <see cref="ArgumentException"/> when its mismatched <see cref="ChartSeries.Values"/>
    ///     count is checked against this axis's label count.
    /// </remarks>
    private static ChartAxis? BuildCategoryAxis(IReadOnlyList<XElement> seriesElements, XElement plotArea)
    {
        var labels = seriesElements.Count > 0
            ? ParseCategoryLabels(seriesElements[0].Element(ChartNs + "cat"))
            : null;
        var title = GetAxisTitleText(plotArea.Element(ChartNs + "catAx"));

        return labels is null && title is null ? null : new ChartAxis(labels: labels, title: title);
    }

    /// <summary>
    ///     Parses the plot area's <c>c:valAx</c> element, when present, into a <see cref="ChartAxis"/>
    ///     carrying its title and/or <c>c:scaling</c>/<c>c:majorUnit</c> range.
    /// </summary>
    /// <param name="plotArea">The <c>c:plotArea</c> element.</param>
    /// <returns>
    ///     The parsed value axis, or <see langword="null"/> when <c>c:valAx</c> is absent or
    ///     carries none of a title/minimum/maximum/tick interval.
    /// </returns>
    private static ChartAxis? ParseValueAxis(XElement plotArea)
    {
        var valAx = plotArea.Element(ChartNs + "valAx");
        if (valAx is null)
        {
            return null;
        }

        var scaling = valAx.Element(ChartNs + "scaling");
        var minimum = ParseNullableFloat(scaling?.Element(ChartNs + "min")?.Attribute("val"));
        var maximum = ParseNullableFloat(scaling?.Element(ChartNs + "max")?.Attribute("val"));
        var tickInterval = ParseNullableFloat(valAx.Element(ChartNs + "majorUnit")?.Attribute("val"));
        var title = GetAxisTitleText(valAx);

        return minimum is null && maximum is null && tickInterval is null && title is null
            ? null
            : new ChartAxis(minimum: minimum, maximum: maximum, tickInterval: tickInterval, title: title);
    }

    /// <summary>
    ///     Parses a <c>c:title</c> (or <c>c:catAx</c>/<c>c:valAx</c>'s own identically-shaped
    ///     <c>c:title</c>) element into its cached display text.
    /// </summary>
    /// <param name="titleElement">The <c>c:title</c> element, or <see langword="null"/>.</param>
    /// <returns>
    ///     A new <see cref="ChartTitle"/> wrapping the cached text, or <see langword="null"/>
    ///     when <paramref name="titleElement"/> is <see langword="null"/>, has no <c>c:tx</c>
    ///     child, or its cached text is empty/whitespace-only (a styled-but-textless PowerPoint
    ///     auto-title placeholder - see <see cref="Parse(XElement)"/>'s own remarks).
    /// </returns>
    private static ChartTitle? ParseTitle(XElement? titleElement)
    {
        var text = GetTxText(titleElement?.Element(ChartNs + "tx"));
        return text is null ? null : new ChartTitle(text);
    }

    /// <summary>
    ///     Parses a <c>c:catAx</c>/<c>c:valAx</c> element's own <c>c:title</c> child into its
    ///     cached display text, for direct use as a <see cref="ChartAxis.Title"/> (which, unlike
    ///     <see cref="Chart.Title"/>, is a plain <see cref="string"/> rather than a
    ///     <see cref="ChartTitle"/>).
    /// </summary>
    /// <param name="axisElement">The <c>c:catAx</c>/<c>c:valAx</c> element, or <see langword="null"/>.</param>
    /// <returns>The axis's cached title text, or <see langword="null"/> per <see cref="ParseTitle"/>'s own rules.</returns>
    private static string? GetAxisTitleText(XElement? axisElement) =>
        GetTxText(axisElement?.Element(ChartNs + "title")?.Element(ChartNs + "tx"));

    /// <summary>
    ///     Parses a <c>c:tx</c> element (used by both a series name and a title) into its cached
    ///     display text, honoring both of CT_Tx's schema-legal shapes: a <c>c:strRef</c>
    ///     reference with a cached <c>c:strCache</c>, a rich-text <c>c:rich</c> body (every
    ///     descendant <c>a:t</c> run concatenated), or a bare literal <c>c:v</c>.
    /// </summary>
    /// <param name="tx">The <c>c:tx</c> element, or <see langword="null"/>.</param>
    /// <returns>
    ///     The cached/literal text, or <see langword="null"/> when <paramref name="tx"/> is
    ///     <see langword="null"/>, has none of the three recognized shapes, or its text is
    ///     empty/whitespace-only.
    /// </returns>
    /// <remarks>
    ///     A missing cache here (a <c>c:strRef</c> with a <c>c:f</c> formula but no
    ///     <c>c:strCache</c>) is treated the same as "no text at all" rather than rejected with
    ///     <see cref="ChartUnsupportedFeatureException"/>: unlike a series' numeric values (whose
    ///     absence leaves nothing meaningful to plot), a missing name/title cache has a safe,
    ///     well-defined fallback (the caller-visible default series name, or no title at all).
    /// </remarks>
    private static string? GetTxText(XElement? tx)
    {
        if (tx is null)
        {
            return null;
        }

        var strCache = tx.Element(ChartNs + "strRef")?.Element(ChartNs + "strCache");
        if (strCache is not null)
        {
            return NormalizeText(GetFirstStrCacheValue(strCache));
        }

        var rich = tx.Element(ChartNs + "rich");
        if (rich is not null)
        {
            return NormalizeText(string.Concat(rich.Descendants(DrawingNs + "t").Select(e => e.Value)));
        }

        return NormalizeText((string?)tx.Element(ChartNs + "v"));
    }

    /// <summary>
    ///     Normalizes a candidate text value, converting an empty or whitespace-only string to
    ///     <see langword="null"/> so callers can use a single null check for "no meaningful text".
    /// </summary>
    /// <param name="text">The candidate text, or <see langword="null"/>.</param>
    /// <returns><paramref name="text"/> unchanged, or <see langword="null"/> when it is null/empty/whitespace-only.</returns>
    private static string? NormalizeText(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;

    /// <summary>
    ///     Parses a <c>c:legend</c> element into a <see cref="ChartLegend"/>.
    /// </summary>
    /// <param name="legendElement">The <c>c:legend</c> element, or <see langword="null"/>.</param>
    /// <returns>
    ///     The parsed legend (always visible - <c>c:legend</c>'s presence itself is the only
    ///     visibility signal OOXML's schema carries), or <see langword="null"/> when
    ///     <paramref name="legendElement"/> is <see langword="null"/>.
    /// </returns>
    /// <remarks>
    ///     <see cref="ChartLegendPosition"/> defines only <see cref="ChartLegendPosition.Top"/>/
    ///     <see cref="ChartLegendPosition.Bottom"/>/<see cref="ChartLegendPosition.Left"/>/
    ///     <see cref="ChartLegendPosition.Right"/>/<see cref="ChartLegendPosition.None"/>, while
    ///     OOXML's <c>c:legendPos</c> additionally defines <c>"tr</c> (top-right); <c>"tr</c>
    ///     is mapped to the nearest defined member, <see cref="ChartLegendPosition.Right"/> - a
    ///     documented, inherent limitation of the Phase 1 model's five-value enum, not a defect
    ///     this parser can losslessly round-trip. A <c>c:legendPos</c> value this parser does not
    ///     recognize, or an absent <c>c:legendPos</c> element (ECMA-376's own documented default),
    ///     is likewise mapped to <see cref="ChartLegendPosition.Right"/>.
    /// </remarks>
    private static ChartLegend? ParseLegend(XElement? legendElement)
    {
        if (legendElement is null)
        {
            return null;
        }

        var positionValue = (string?)legendElement.Element(ChartNs + "legendPos")?.Attribute("val");
        var position = positionValue switch
        {
            "t" => ChartLegendPosition.Top,
            "b" => ChartLegendPosition.Bottom,
            "l" => ChartLegendPosition.Left,
            "r" => ChartLegendPosition.Right,
            "tr" => ChartLegendPosition.Right,
            _ => ChartLegendPosition.Right
        };

        return new ChartLegend(position);
    }

    /// <summary>
    ///     Parses a <c>c:cat</c> element into its cached category labels, honoring both of
    ///     CT_AxDataSource's commonly used shapes: a string reference (<c>c:strRef</c>/
    ///     <c>c:strCache</c>) or a numeric reference (<c>c:numRef</c>/<c>c:numCache</c>,
    ///     formatted via <see cref="CultureInfo.InvariantCulture"/>).
    /// </summary>
    /// <param name="cat">The <c>c:cat</c> element, or <see langword="null"/>.</param>
    /// <returns>
    ///     The cached category labels, or <see langword="null"/> when <paramref name="cat"/> is
    ///     <see langword="null"/>, or uses a schema-legal shape this parser does not recognize
    ///     (for example <c>c:multiLvlStrRef</c>'s multi-level category labels) - this is a
    ///     deliberate, documented defensive fallback (no categories, rather than a thrown
    ///     exception) for a chart kind/data shape otherwise fully supported.
    /// </returns>
    private static string[]? ParseCategoryLabels(XElement? cat)
    {
        if (cat is null)
        {
            return null;
        }

        var strCache = cat.Element(ChartNs + "strRef")?.Element(ChartNs + "strCache");
        if (strCache is not null)
        {
            return ParseStrCache(strCache);
        }

        var numCache = cat.Element(ChartNs + "numRef")?.Element(ChartNs + "numCache");
        return numCache is not null ? ParseNumCacheAsStrings(numCache) : null;
    }

    /// <summary>
    ///     Parses a <c>c:numCache</c> element into a dense array of cached numeric values, sized
    ///     by its own <c>c:ptCount</c> and honoring sparse <c>c:pt</c> gaps as <c>0.0</c>.
    /// </summary>
    /// <param name="numCache">The <c>c:numCache</c> element.</param>
    /// <returns>The dense, <c>c:ptCount</c>-sized values array.</returns>
    /// <exception cref="ChartUnsupportedFeatureException">
    ///     Thrown when <c>c:ptCount</c> exceeds <see cref="MaxCachedPointCount"/>.
    /// </exception>
    private static double[] ParseNumCache(XElement numCache)
    {
        var ptCount = Math.Max(0, ParseNullableInt(numCache.Element(ChartNs + "ptCount")?.Attribute("val")) ?? 0);
        if (ptCount > MaxCachedPointCount)
        {
            throw new ChartUnsupportedFeatureException(
                "charts-openxml-point-count-too-large",
                $"c:numCache declares c:ptCount={ptCount}, which exceeds the maximum supported cached point count of {MaxCachedPointCount}.");
        }

        var values = new double[ptCount];
        foreach (var pt in numCache.Elements(ChartNs + "pt"))
        {
            var idx = ParseNullableInt(pt.Attribute("idx")) ?? -1;
            if (idx < 0 || idx >= ptCount)
            {
                continue;
            }

            values[idx] = ParseNullableDouble(pt.Element(ChartNs + "v")) ?? 0d;
        }

        return values;
    }

    /// <summary>
    ///     Parses a <c>c:strCache</c> element into a dense array of cached string values, sized
    ///     by its own <c>c:ptCount</c> and honoring sparse <c>c:pt</c> gaps as
    ///     <see cref="string.Empty"/> (a legitimate blank category label - see
    ///     <see cref="ChartAxis"/>'s own documented "empty string is a legitimate blank label"
    ///     convention).
    /// </summary>
    /// <param name="strCache">The <c>c:strCache</c> element.</param>
    /// <returns>The dense, <c>c:ptCount</c>-sized labels array.</returns>
    /// <exception cref="ChartUnsupportedFeatureException">
    ///     Thrown when <c>c:ptCount</c> exceeds <see cref="MaxCachedPointCount"/>.
    /// </exception>
    private static string[] ParseStrCache(XElement strCache)
    {
        var ptCount = Math.Max(0, ParseNullableInt(strCache.Element(ChartNs + "ptCount")?.Attribute("val")) ?? 0);
        if (ptCount > MaxCachedPointCount)
        {
            throw new ChartUnsupportedFeatureException(
                "charts-openxml-point-count-too-large",
                $"c:strCache declares c:ptCount={ptCount}, which exceeds the maximum supported cached point count of {MaxCachedPointCount}.");
        }

        var values = new string[ptCount];
        Array.Fill(values, string.Empty);
        foreach (var pt in strCache.Elements(ChartNs + "pt"))
        {
            var idx = ParseNullableInt(pt.Attribute("idx")) ?? -1;
            if (idx < 0 || idx >= ptCount)
            {
                continue;
            }

            values[idx] = (string?)pt.Element(ChartNs + "v") ?? string.Empty;
        }

        return values;
    }

    /// <summary>
    ///     Parses a <c>c:numCache</c> element (used for a numeric category axis) into a dense
    ///     array of cached values formatted as invariant-culture strings, for use as category
    ///     labels.
    /// </summary>
    /// <param name="numCache">The <c>c:numCache</c> element.</param>
    /// <returns>The dense, <c>c:ptCount</c>-sized labels array.</returns>
    private static string[] ParseNumCacheAsStrings(XElement numCache) =>
        [.. ParseNumCache(numCache).Select(static v => v.ToString(CultureInfo.InvariantCulture))];

    /// <summary>
    ///     Gets a <c>c:strCache</c> element's cached text for index <c>0</c> (the conventional
    ///     single-entry position for a series name/title cache), falling back to the first
    ///     <c>c:pt</c> present when no <c>idx="0</c> entry exists.
    /// </summary>
    /// <param name="strCache">The <c>c:strCache</c> element.</param>
    /// <returns>The cached text, or <see langword="null"/> when no <c>c:pt</c> entry exists.</returns>
    private static string? GetFirstStrCacheValue(XElement strCache)
    {
        var pt = strCache.Elements(ChartNs + "pt").FirstOrDefault(p => ParseNullableInt(p.Attribute("idx")) == 0)
                 ?? strCache.Elements(ChartNs + "pt").FirstOrDefault();
        return (string?)pt?.Element(ChartNs + "v");
    }

    /// <summary>
    ///     Parses <paramref name="attribute"/>'s value as an <see cref="int"/>, the same way the
    ///     explicit <c>(int?)</c> cast operator does, but rejects a present-but-malformed
    ///     (non-numeric or out-of-<see cref="int"/>-range) value with
    ///     <see cref="ChartUnsupportedFeatureException"/> instead of letting the cast operator's
    ///     raw <see cref="FormatException"/>/<see cref="OverflowException"/> propagate uncaught -
    ///     mirroring <c>PptxDocument.Geometry.cs</c>'s <c>ParseRequiredFloatAttribute</c>
    ///     guarded-parse convention, adapted to this parser's own
    ///     <see cref="ChartUnsupportedFeatureException"/> fail-closed idiom (see this type's own
    ///     remarks for why: <c>PptxDocument.Charts.cs</c>'s <c>ParseChart</c> only catches this
    ///     specific exception type around the parser call).
    /// </summary>
    /// <param name="attribute">The attribute to parse, or <see langword="null"/>.</param>
    /// <returns>
    ///     The parsed value, or <see langword="null"/> when <paramref name="attribute"/> is
    ///     <see langword="null"/>.
    /// </returns>
    /// <exception cref="ChartUnsupportedFeatureException">
    ///     Thrown when <paramref name="attribute"/> is present but its value is not a
    ///     well-formed, in-range <see cref="int"/>.
    /// </exception>
    private static int? ParseNullableInt(XAttribute? attribute)
    {
        if (attribute is null)
        {
            return null;
        }

        try
        {
            return (int)attribute;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            throw new ChartUnsupportedFeatureException(
                "charts-openxml-malformed-numeric-value",
                $"Attribute '{attribute.Name}' has a malformed integer value '{attribute.Value}'.");
        }
    }

    /// <summary>
    ///     Parses <paramref name="attribute"/>'s value as a <see cref="float"/>, the same way the
    ///     explicit <c>(float?)</c> cast operator does, but rejects a present-but-malformed
    ///     (non-numeric or out-of-<see cref="float"/>-range) value with
    ///     <see cref="ChartUnsupportedFeatureException"/> instead of letting the cast operator's
    ///     raw <see cref="FormatException"/>/<see cref="OverflowException"/> propagate uncaught -
    ///     see <see cref="ParseNullableInt"/>'s own remarks for the full rationale.
    /// </summary>
    /// <param name="attribute">The attribute to parse, or <see langword="null"/>.</param>
    /// <returns>
    ///     The parsed value, or <see langword="null"/> when <paramref name="attribute"/> is
    ///     <see langword="null"/>.
    /// </returns>
    /// <exception cref="ChartUnsupportedFeatureException">
    ///     Thrown when <paramref name="attribute"/> is present but its value is not a
    ///     well-formed, in-range <see cref="float"/>.
    /// </exception>
    private static float? ParseNullableFloat(XAttribute? attribute)
    {
        if (attribute is null)
        {
            return null;
        }

        try
        {
            return (float)attribute;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            throw new ChartUnsupportedFeatureException(
                "charts-openxml-malformed-numeric-value",
                $"Attribute '{attribute.Name}' has a malformed numeric value '{attribute.Value}'.");
        }
    }

    /// <summary>
    ///     Parses <paramref name="element"/>'s cached text as a <see cref="double"/>, the same
    ///     way the explicit <c>(double?)</c> cast operator does, but rejects a present-but-
    ///     malformed (non-numeric or out-of-<see cref="double"/>-range) value with
    ///     <see cref="ChartUnsupportedFeatureException"/> instead of letting the cast operator's
    ///     raw <see cref="FormatException"/>/<see cref="OverflowException"/> propagate uncaught -
    ///     see <see cref="ParseNullableInt"/>'s own remarks for the full rationale.
    /// </summary>
    /// <param name="element">The element to parse (for example a <c>c:v</c> cache entry), or <see langword="null"/>.</param>
    /// <returns>
    ///     The parsed value, or <see langword="null"/> when <paramref name="element"/> is
    ///     <see langword="null"/>.
    /// </returns>
    /// <exception cref="ChartUnsupportedFeatureException">
    ///     Thrown when <paramref name="element"/> is present but its cached text is not a
    ///     well-formed, in-range <see cref="double"/>.
    /// </exception>
    private static double? ParseNullableDouble(XElement? element)
    {
        if (element is null)
        {
            return null;
        }

        try
        {
            return (double)element;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            throw new ChartUnsupportedFeatureException(
                "charts-openxml-malformed-numeric-value",
                $"Element '{element.Name}' has a malformed numeric value '{element.Value}'.");
        }
    }
}
