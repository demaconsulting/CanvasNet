using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Charts.OpenXml;

namespace DemaConsulting.CanvasNet.Charts.Tests;

/// <summary>
///     Tests for <see cref="OpenXmlChartParser"/>: argument validation, per-<see cref="ChartType"/>
///     success parsing, series/category/value/title/legend/axis extraction, cached-values-only
///     enforcement, unsupported-chart-type/combo-chart/empty-plot-area rejection, a real-fixture-
///     derived end-to-end test, and parse-then-render round-trip pixel-sanity tests.
/// </summary>
/// <remarks>
///     Every fixture in this file is either a minimal hand-authored XML fragment following the
///     ECMA-376 DrawingML-Charts schema shapes this parser documents, or - for the bar/column
///     cases - a trimmed, adapted excerpt of a real <c>chart1.xml</c> part; see
///     <c>OpenXmlChartFixtures/README.md</c> for exact provenance.
/// </remarks>
public class OpenXmlChartParserTests
{
    /// <summary>The DrawingML-Charts XML namespace, used throughout to build fragment elements.</summary>
    private static readonly XNamespace C = "http://schemas.openxmlformats.org/drawingml/2006/chart";

    /// <summary>The DrawingML-Main XML namespace, used only for a rich-text title's <c>a:t</c> runs.</summary>
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";

    /// <summary>Opaque white - <see cref="ChartRenderOptions.Default"/>'s own documented default background.</summary>
    private static readonly Rgba32 White = new(255, 255, 255, 255);

    #region Fragment builders

    /// <summary>Builds a <c>c:numCache</c> element for the given dense values.</summary>
    private static XElement NumCache(params double[] values) =>
        new(C + "numCache",
            new XElement(C + "formatCode", "General"),
            new XElement(C + "ptCount", new XAttribute("val", values.Length)),
            values.Select((v, i) => new XElement(C + "pt", new XAttribute("idx", i), new XElement(C + "v", v))));

    /// <summary>Builds a <c>c:strCache</c> element for the given dense labels.</summary>
    private static XElement StrCache(params string[] values) =>
        new(C + "strCache",
            new XElement(C + "ptCount", new XAttribute("val", values.Length)),
            values.Select((v, i) => new XElement(C + "pt", new XAttribute("idx", i), new XElement(C + "v", v))));

    /// <summary>Builds a <c>c:tx</c> element with a cached string name.</summary>
    private static XElement Tx(string name) =>
        new(C + "tx", new XElement(C + "strRef", new XElement(C + "f", "Sheet1!$B$1"), StrCache(name)));

    /// <summary>Builds a <c>c:val</c> element with cached numeric values.</summary>
    private static XElement Val(params double[] values) =>
        new(C + "val", new XElement(C + "numRef", new XElement(C + "f", "Sheet1!$B$2:$B$5"), NumCache(values)));

    /// <summary>Builds a <c>c:cat</c> element with cached string labels.</summary>
    private static XElement Cat(params string[] labels) =>
        new(C + "cat", new XElement(C + "strRef", new XElement(C + "f", "Sheet1!$A$2:$A$5"), StrCache(labels)));

    /// <summary>Builds a <c>c:ser</c> element from the given name/values/categories.</summary>
    private static XElement Ser(string name, double[] values, string[]? categories = null)
    {
        var ser = new XElement(C + "ser",
            new XElement(C + "idx", new XAttribute("val", 0)),
            new XElement(C + "order", new XAttribute("val", 0)),
            Tx(name));
        if (categories is not null)
        {
            ser.Add(Cat(categories));
        }

        ser.Add(Val(values));
        return ser;
    }

    /// <summary>Builds a minimal <c>c:chart</c> element wrapping a single chart-type element.</summary>
    private static XElement Chart(XElement chartTypeElement, XElement? title = null, XElement? legend = null) =>
        new(C + "chart",
            title is null ? null : new XElement(C + "title", title),
            new XElement(C + "autoTitleDeleted", new XAttribute("val", 0)),
            new XElement(C + "plotArea", new XElement(C + "layout"), chartTypeElement),
            legend is null ? null : new XElement(C + "legend", legend));

    #endregion

    #region Argument validation

    /// <summary>Proves a null <see cref="XDocument"/> is rejected.</summary>
    [Fact]
    public void Parse_NullDocument_ThrowsArgumentNullException()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => OpenXmlChartParser.Parse((XDocument)null!));
    }

    /// <summary>Proves a null <see cref="XElement"/> is rejected.</summary>
    [Fact]
    public void Parse_NullElement_ThrowsArgumentNullException()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => OpenXmlChartParser.Parse((XElement)null!));
    }

    /// <summary>Proves a document with no root element is rejected.</summary>
    [Fact]
    public void Parse_DocumentWithNoRoot_ThrowsArgumentException()
    {
        // Arrange
        var document = new XDocument();

        // Act / Assert
        Assert.Throws<ArgumentException>(() => OpenXmlChartParser.Parse(document));
    }

    /// <summary>Proves an element that is neither a <c>c:chart</c> nor a <c>c:chartSpace</c> containing one is rejected.</summary>
    [Fact]
    public void Parse_ElementWithNoChartChild_ThrowsArgumentException()
    {
        // Arrange
        var element = new XElement(C + "notAChartSpace");

        // Act / Assert
        Assert.Throws<ArgumentException>(() => OpenXmlChartParser.Parse(element));
    }

    /// <summary>Proves a <c>c:chart</c> element with no <c>c:plotArea</c> child is rejected.</summary>
    [Fact]
    public void Parse_ChartWithNoPlotArea_ThrowsArgumentException()
    {
        // Arrange
        var element = new XElement(C + "chart");

        // Act / Assert
        Assert.Throws<ArgumentException>(() => OpenXmlChartParser.Parse(element));
    }

    /// <summary>Proves a full <c>c:chartSpace</c> wrapper around <c>c:chart</c> is accepted identically to a bare <c>c:chart</c>.</summary>
    [Fact]
    public void Parse_ChartSpaceWrapper_ParsesSameAsBareChart()
    {
        // Arrange
        var barChart = new XElement(C + "barChart",
            new XElement(C + "barDir", new XAttribute("val", "col")),
            Ser("S1", [1.0, 2.0], ["A", "B"]));
        var bareChart = Chart(barChart);
        var chartSpace = new XElement(C + "chartSpace", bareChart);

        // Act
        var fromBare = OpenXmlChartParser.Parse(bareChart);
        var fromWrapped = OpenXmlChartParser.Parse(chartSpace);

        // Assert
        Assert.Equal(fromBare.Type, fromWrapped.Type);
        Assert.Equal(fromBare.Series[0].Name, fromWrapped.Series[0].Name);
    }

    /// <summary>Proves <see cref="OpenXmlChartParser.Parse(XDocument)"/> delegates correctly to the element overload.</summary>
    [Fact]
    public void Parse_Document_ParsesSameAsElement()
    {
        // Arrange
        var barChart = new XElement(C + "barChart",
            new XElement(C + "barDir", new XAttribute("val", "col")),
            Ser("S1", [1.0, 2.0], ["A", "B"]));
        var chartElement = Chart(barChart);
        var document = new XDocument(new XElement(C + "chartSpace", chartElement));

        // Act
        var chart = OpenXmlChartParser.Parse(document);

        // Assert
        Assert.Equal(ChartType.Column, chart.Type);
    }

    #endregion

    #region Per-chart-type success parsing

    /// <summary>Proves a <c>c:barChart</c> with <c>barDir val="bar"</c> classifies as <see cref="ChartType.Bar"/>.</summary>
    [Fact]
    public void Parse_BarChartWithBarDirection_ClassifiesAsBar()
    {
        // Arrange
        var barChart = new XElement(C + "barChart",
            new XElement(C + "barDir", new XAttribute("val", "bar")),
            Ser("S1", [1.0, 2.0], ["A", "B"]));
        var chartElement = Chart(barChart);

        // Act
        var chart = OpenXmlChartParser.Parse(chartElement);

        // Assert
        Assert.Equal(ChartType.Bar, chart.Type);
    }

    /// <summary>Proves a <c>c:barChart</c> with <c>barDir val="col"</c> classifies as <see cref="ChartType.Column"/>.</summary>
    [Fact]
    public void Parse_BarChartWithColumnDirection_ClassifiesAsColumn()
    {
        // Arrange
        var barChart = new XElement(C + "barChart",
            new XElement(C + "barDir", new XAttribute("val", "col")),
            Ser("S1", [1.0, 2.0], ["A", "B"]));
        var chartElement = Chart(barChart);

        // Act
        var chart = OpenXmlChartParser.Parse(chartElement);

        // Assert
        Assert.Equal(ChartType.Column, chart.Type);
    }

    /// <summary>Proves a <c>c:barChart</c> with no <c>c:barDir</c> element defaults to <see cref="ChartType.Column"/>.</summary>
    [Fact]
    public void Parse_BarChartWithNoBarDirection_DefaultsToColumn()
    {
        // Arrange
        var barChart = new XElement(C + "barChart", Ser("S1", [1.0, 2.0], ["A", "B"]));
        var chartElement = Chart(barChart);

        // Act
        var chart = OpenXmlChartParser.Parse(chartElement);

        // Assert
        Assert.Equal(ChartType.Column, chart.Type);
    }

    /// <summary>Proves a <c>c:lineChart</c> classifies as <see cref="ChartType.Line"/>.</summary>
    [Fact]
    public void Parse_LineChart_ClassifiesAsLine()
    {
        // Arrange
        var lineChart = new XElement(C + "lineChart", Ser("S1", [1.0, 2.0], ["A", "B"]));
        var chartElement = Chart(lineChart);

        // Act
        var chart = OpenXmlChartParser.Parse(chartElement);

        // Assert
        Assert.Equal(ChartType.Line, chart.Type);
    }

    /// <summary>Proves a <c>c:areaChart</c> classifies as <see cref="ChartType.Area"/>.</summary>
    [Fact]
    public void Parse_AreaChart_ClassifiesAsArea()
    {
        // Arrange
        var areaChart = new XElement(C + "areaChart", Ser("S1", [1.0, 2.0], ["A", "B"]));
        var chartElement = Chart(areaChart);

        // Act
        var chart = OpenXmlChartParser.Parse(chartElement);

        // Assert
        Assert.Equal(ChartType.Area, chart.Type);
    }

    /// <summary>Proves a <c>c:pieChart</c> classifies as <see cref="ChartType.Pie"/> and maps categories to point labels.</summary>
    [Fact]
    public void Parse_PieChart_ClassifiesAsPieWithPointLabels()
    {
        // Arrange
        var pieChart = new XElement(C + "pieChart", Ser("S1", [30.0, 70.0], ["Red", "Blue"]));
        var chartElement = Chart(pieChart);

        // Act
        var chart = OpenXmlChartParser.Parse(chartElement);

        // Assert
        Assert.Equal(ChartType.Pie, chart.Type);
        Assert.Null(chart.CategoryAxis);
        Assert.Equal(["Red", "Blue"], chart.Series[0].PointLabels);
    }

    /// <summary>Proves a <c>c:doughnutChart</c> classifies as <see cref="ChartType.Doughnut"/>.</summary>
    [Fact]
    public void Parse_DoughnutChart_ClassifiesAsDoughnut()
    {
        // Arrange
        var doughnutChart = new XElement(C + "doughnutChart", Ser("S1", [30.0, 70.0], ["Red", "Blue"]));
        var chartElement = Chart(doughnutChart);

        // Act
        var chart = OpenXmlChartParser.Parse(chartElement);

        // Assert
        Assert.Equal(ChartType.Doughnut, chart.Type);
    }

    #endregion

    #region Series/category/value extraction

    /// <summary>Proves a series' cached name is extracted from its <c>c:tx/c:strRef/c:strCache</c>.</summary>
    [Fact]
    public void Parse_SeriesWithCachedName_ExtractsName()
    {
        // Arrange
        var barChart = new XElement(C + "barChart", Ser("Revenue", [1.0, 2.0], ["A", "B"]));
        var chartElement = Chart(barChart);

        // Act
        var chart = OpenXmlChartParser.Parse(chartElement);

        // Assert
        Assert.Equal("Revenue", chart.Series[0].Name);
    }

    /// <summary>Proves a series with no <c>c:tx</c> falls back to a default 1-based "Series N" name.</summary>
    [Fact]
    public void Parse_SeriesWithNoTx_DefaultsToSeriesIndexName()
    {
        // Arrange
        var ser = new XElement(C + "ser",
            new XElement(C + "idx", new XAttribute("val", 0)),
            new XElement(C + "order", new XAttribute("val", 0)),
            Cat("A", "B"),
            Val(1.0, 2.0));
        var barChart = new XElement(C + "barChart", ser);
        var chartElement = Chart(barChart);

        // Act
        var chart = OpenXmlChartParser.Parse(chartElement);

        // Assert
        Assert.Equal("Series 1", chart.Series[0].Name);
    }

    /// <summary>Proves a series' literal <c>c:tx/c:v</c> name (no <c>c:strRef</c>) is extracted.</summary>
    [Fact]
    public void Parse_SeriesWithLiteralTxValue_ExtractsName()
    {
        // Arrange
        var ser = new XElement(C + "ser",
            new XElement(C + "idx", new XAttribute("val", 0)),
            new XElement(C + "order", new XAttribute("val", 0)),
            new XElement(C + "tx", new XElement(C + "v", "Literal Name")),
            Cat("A", "B"),
            Val(1.0, 2.0));
        var barChart = new XElement(C + "barChart", ser);
        var chartElement = Chart(barChart);

        // Act
        var chart = OpenXmlChartParser.Parse(chartElement);

        // Assert
        Assert.Equal("Literal Name", chart.Series[0].Name);
    }

    /// <summary>Proves categories backed by <c>c:numRef/c:numCache</c> are converted to invariant-culture strings.</summary>
    [Fact]
    public void Parse_NumericCategories_ConvertsToInvariantCultureStrings()
    {
        // Arrange
        var ser = new XElement(C + "ser",
            new XElement(C + "idx", new XAttribute("val", 0)),
            new XElement(C + "order", new XAttribute("val", 0)),
            Tx("S1"),
            new XElement(C + "cat", new XElement(C + "numRef", new XElement(C + "f", "Sheet1!$A$2:$A$3"), NumCache(2000, 2001))),
            Val(1.0, 2.0));
        var barChart = new XElement(C + "barChart", ser);
        var chartElement = Chart(barChart);

        // Act
        var chart = OpenXmlChartParser.Parse(chartElement);

        // Assert
        Assert.Equal(["2000", "2001"], chart.CategoryAxis!.Labels);
    }

    /// <summary>Proves sparse <c>c:pt</c> gaps in a <c>c:numCache</c> default to <c>0.0</c>.</summary>
    [Fact]
    public void Parse_SparseNumCache_MissingIndexDefaultsToZero()
    {
        // Arrange
        var numCache = new XElement(C + "numCache",
            new XElement(C + "formatCode", "General"),
            new XElement(C + "ptCount", new XAttribute("val", 3)),
            new XElement(C + "pt", new XAttribute("idx", 0), new XElement(C + "v", 1.0)),
            new XElement(C + "pt", new XAttribute("idx", 2), new XElement(C + "v", 3.0)));
        var ser = new XElement(C + "ser",
            new XElement(C + "idx", new XAttribute("val", 0)),
            new XElement(C + "order", new XAttribute("val", 0)),
            Tx("S1"),
            Cat("A", "B", "C"),
            new XElement(C + "val", new XElement(C + "numRef", new XElement(C + "f", "Sheet1!$B$2:$B$4"), numCache)));
        var barChart = new XElement(C + "barChart", ser);
        var chartElement = Chart(barChart);

        // Act
        var chart = OpenXmlChartParser.Parse(chartElement);

        // Assert
        Assert.Equal([1.0, 0.0, 3.0], chart.Series[0].Values);
    }

    /// <summary>Proves multiple series under one chart-type element are all parsed, in order.</summary>
    [Fact]
    public void Parse_MultipleSeries_ParsesAllInOrder()
    {
        // Arrange
        var barChart = new XElement(C + "barChart",
            Ser("First", [1.0, 2.0], ["A", "B"]),
            Ser("Second", [3.0, 4.0], ["A", "B"]));
        var chartElement = Chart(barChart);

        // Act
        var chart = OpenXmlChartParser.Parse(chartElement);

        // Assert
        Assert.Equal(2, chart.Series.Count);
        Assert.Equal("First", chart.Series[0].Name);
        Assert.Equal("Second", chart.Series[1].Name);
    }

    #endregion

    #region Title extraction

    /// <summary>Proves a chart title's cached <c>c:strCache</c> text is extracted.</summary>
    [Fact]
    public void Parse_TitleWithCachedText_ExtractsTitle()
    {
        // Arrange
        var titleContent = new XElement(C + "tx", new XElement(C + "strRef", new XElement(C + "f", "Sheet1!$A$1"), StrCache("Quarterly Revenue")));
        var barChart = new XElement(C + "barChart", Ser("S1", [1.0, 2.0], ["A", "B"]));
        var chartElement = Chart(barChart, title: titleContent);

        // Act
        var chart = OpenXmlChartParser.Parse(chartElement);

        // Assert
        Assert.Equal("Quarterly Revenue", chart.Title?.Text);
    }

    /// <summary>Proves a chart title's rich-text <c>c:rich</c> body's concatenated <c>a:t</c> runs are extracted.</summary>
    [Fact]
    public void Parse_TitleWithRichText_ConcatenatesRuns()
    {
        // Arrange
        var rich = new XElement(C + "rich",
            new XElement(A + "p", new XElement(A + "r", new XElement(A + "t", "Hello ")), new XElement(A + "r", new XElement(A + "t", "World"))));
        var titleContent = new XElement(C + "tx", rich);
        var barChart = new XElement(C + "barChart", Ser("S1", [1.0, 2.0], ["A", "B"]));
        var chartElement = Chart(barChart, title: titleContent);

        // Act
        var chart = OpenXmlChartParser.Parse(chartElement);

        // Assert
        Assert.Equal("Hello World", chart.Title?.Text);
    }

    /// <summary>Proves a <c>c:title</c> present with no <c>c:tx</c> (a styled-but-textless auto-title placeholder) maps to a null title.</summary>
    [Fact]
    public void Parse_TitlePresentWithNoTx_MapsToNullTitle()
    {
        // Arrange
        var barChart = new XElement(C + "barChart", Ser("S1", [1.0, 2.0], ["A", "B"]));
        var chartElement = new XElement(C + "chart",
            new XElement(C + "title", new XElement(C + "overlay", new XAttribute("val", 0))),
            new XElement(C + "autoTitleDeleted", new XAttribute("val", 0)),
            new XElement(C + "plotArea", new XElement(C + "layout"), barChart));

        // Act
        var chart = OpenXmlChartParser.Parse(chartElement);

        // Assert
        Assert.Null(chart.Title);
    }

    /// <summary>Proves an absent <c>c:title</c> element maps to a null title.</summary>
    [Fact]
    public void Parse_NoTitleElement_MapsToNullTitle()
    {
        // Arrange
        var barChart = new XElement(C + "barChart", Ser("S1", [1.0, 2.0], ["A", "B"]));
        var chartElement = Chart(barChart);

        // Act
        var chart = OpenXmlChartParser.Parse(chartElement);

        // Assert
        Assert.Null(chart.Title);
    }

    #endregion

    #region Legend extraction

    /// <summary>Proves an absent <c>c:legend</c> element maps to a null legend.</summary>
    [Fact]
    public void Parse_NoLegendElement_MapsToNullLegend()
    {
        // Arrange
        var barChart = new XElement(C + "barChart", Ser("S1", [1.0, 2.0], ["A", "B"]));
        var chartElement = Chart(barChart);

        // Act
        var chart = OpenXmlChartParser.Parse(chartElement);

        // Assert
        Assert.Null(chart.Legend);
    }

    /// <summary>Proves each defined <c>c:legendPos</c> value maps to the expected <see cref="ChartLegendPosition"/>.</summary>
    [Theory]
    [InlineData("t", ChartLegendPosition.Top)]
    [InlineData("b", ChartLegendPosition.Bottom)]
    [InlineData("l", ChartLegendPosition.Left)]
    [InlineData("r", ChartLegendPosition.Right)]
    [InlineData("tr", ChartLegendPosition.Right)]
    public void Parse_LegendPosition_MapsToExpectedChartLegendPosition(string legendPos, ChartLegendPosition expected)
    {
        // Arrange
        var legendContent = new XElement(C + "legendPos", new XAttribute("val", legendPos));
        var barChart = new XElement(C + "barChart", Ser("S1", [1.0, 2.0], ["A", "B"]));
        var chartElement = Chart(barChart, legend: legendContent);

        // Act
        var chart = OpenXmlChartParser.Parse(chartElement);

        // Assert
        Assert.Equal(expected, chart.Legend?.Position);
    }

    /// <summary>Proves a <c>c:legend</c> with no <c>c:legendPos</c> defaults to <see cref="ChartLegendPosition.Right"/>.</summary>
    [Fact]
    public void Parse_LegendWithNoPosition_DefaultsToRight()
    {
        // Arrange
        var barChart = new XElement(C + "barChart", Ser("S1", [1.0, 2.0], ["A", "B"]));
        var chartElement = Chart(barChart, legend: new XElement(C + "overlay", new XAttribute("val", 0)));

        // Act
        var chart = OpenXmlChartParser.Parse(chartElement);

        // Assert
        Assert.Equal(ChartLegendPosition.Right, chart.Legend?.Position);
    }

    #endregion

    #region Axis extraction

    /// <summary>Proves a category axis's cached labels are extracted from the first series' <c>c:cat</c>.</summary>
    [Fact]
    public void Parse_CategoryBasedChart_ExtractsCategoryAxisLabels()
    {
        // Arrange
        var barChart = new XElement(C + "barChart", Ser("S1", [1.0, 2.0, 3.0], ["Jan", "Feb", "Mar"]));
        var chartElement = Chart(barChart);

        // Act
        var chart = OpenXmlChartParser.Parse(chartElement);

        // Assert
        Assert.Equal(["Jan", "Feb", "Mar"], chart.CategoryAxis!.Labels);
    }

    /// <summary>Proves a <c>c:catAx/c:title</c>'s cached text becomes the category axis title.</summary>
    [Fact]
    public void Parse_CatAxTitle_ExtractsCategoryAxisTitle()
    {
        // Arrange
        var barChart = new XElement(C + "barChart", Ser("S1", [1.0, 2.0], ["A", "B"]));
        var catAx = new XElement(C + "catAx", new XElement(C + "title", Tx("Month")));
        var chartElement = new XElement(C + "chart",
            new XElement(C + "autoTitleDeleted", new XAttribute("val", 1)),
            new XElement(C + "plotArea", new XElement(C + "layout"), barChart, catAx));

        // Act
        var chart = OpenXmlChartParser.Parse(chartElement);

        // Assert
        Assert.Equal("Month", chart.CategoryAxis!.Title);
    }

    /// <summary>Proves a <c>c:valAx</c>'s <c>c:scaling/c:min</c>, <c>c:max</c>, <c>c:majorUnit</c>, and title are all extracted.</summary>
    [Fact]
    public void Parse_ValAx_ExtractsRangeAndTitle()
    {
        // Arrange
        var barChart = new XElement(C + "barChart", Ser("S1", [1.0, 2.0], ["A", "B"]));
        var valAx = new XElement(C + "valAx",
            new XElement(C + "scaling",
                new XElement(C + "min", new XAttribute("val", 0.0)),
                new XElement(C + "max", new XAttribute("val", 100.0))),
            new XElement(C + "majorUnit", new XAttribute("val", 10.0)),
            new XElement(C + "title", Tx("Units Sold")));
        var chartElement = new XElement(C + "chart",
            new XElement(C + "autoTitleDeleted", new XAttribute("val", 1)),
            new XElement(C + "plotArea", new XElement(C + "layout"), barChart, valAx));

        // Act
        var chart = OpenXmlChartParser.Parse(chartElement);

        // Assert
        var valueAxis = chart.ValueAxis;
        Assert.NotNull(valueAxis);
        Assert.Equal(0.0f, valueAxis.Minimum);
        Assert.Equal(100.0f, valueAxis.Maximum);
        Assert.Equal(10.0f, valueAxis.TickInterval);
        Assert.Equal("Units Sold", valueAxis.Title);
    }

    /// <summary>Proves an absent <c>c:valAx</c> element maps to a null value axis.</summary>
    [Fact]
    public void Parse_NoValAx_MapsToNullValueAxis()
    {
        // Arrange
        var barChart = new XElement(C + "barChart", Ser("S1", [1.0, 2.0], ["A", "B"]));
        var chartElement = Chart(barChart);

        // Act
        var chart = OpenXmlChartParser.Parse(chartElement);

        // Assert
        Assert.Null(chart.ValueAxis);
    }

    #endregion

    #region Cached-values-only enforcement

    /// <summary>Proves a series whose <c>c:val</c> has a formula but no cached <c>c:numCache</c> is rejected.</summary>
    [Fact]
    public void Parse_SeriesWithUncachedValues_ThrowsWithUncachedValuesFeatureToken()
    {
        // Arrange
        var ser = new XElement(C + "ser",
            new XElement(C + "idx", new XAttribute("val", 0)),
            new XElement(C + "order", new XAttribute("val", 0)),
            Tx("S1"),
            Cat("A", "B"),
            new XElement(C + "val", new XElement(C + "numRef", new XElement(C + "f", "Sheet1!$B$2:$B$3"))));
        var barChart = new XElement(C + "barChart", ser);
        var chartElement = Chart(barChart);

        // Act
        var exception = Assert.Throws<ChartUnsupportedFeatureException>(() => OpenXmlChartParser.Parse(chartElement));

        // Assert
        Assert.Equal("charts-openxml-uncached-values", exception.Feature);
    }

    #endregion

    #region Oversized-c:ptCount rejection

    /// <summary>
    ///     Proves a <c>c:numCache</c> declaring a <c>c:ptCount</c> far larger than any genuine
    ///     worksheet-backed chart could have is rejected before a dense array is allocated,
    ///     rather than attempting a multi-gigabyte allocation / crashing with
    ///     <see cref="OutOfMemoryException"/>.
    /// </summary>
    [Fact]
    public void Parse_NumCacheWithExcessivePtCount_ThrowsWithPointCountTooLargeFeatureToken()
    {
        // Arrange
        var val = new XElement(C + "val",
            new XElement(C + "numRef",
                new XElement(C + "f", "Sheet1!$B$2:$B$5"),
                new XElement(C + "numCache",
                    new XElement(C + "formatCode", "General"),
                    new XElement(C + "ptCount", new XAttribute("val", 2_000_000_000)))));
        var ser = new XElement(C + "ser",
            new XElement(C + "idx", new XAttribute("val", 0)),
            new XElement(C + "order", new XAttribute("val", 0)),
            Tx("S1"),
            Cat("A", "B"),
            val);
        var barChart = new XElement(C + "barChart", ser);
        var chartElement = Chart(barChart);

        // Act
        var exception = Assert.Throws<ChartUnsupportedFeatureException>(() => OpenXmlChartParser.Parse(chartElement));

        // Assert
        Assert.Equal("charts-openxml-point-count-too-large", exception.Feature);
    }

    /// <summary>
    ///     Proves the same oversized-<c>c:ptCount</c> rejection applies to a <c>c:strCache</c>
    ///     (for example a string-labeled category axis), not just <c>c:numCache</c>.
    /// </summary>
    [Fact]
    public void Parse_CatStrCacheWithExcessivePtCount_ThrowsWithPointCountTooLargeFeatureToken()
    {
        // Arrange
        var cat = new XElement(C + "cat",
            new XElement(C + "strRef",
                new XElement(C + "f", "Sheet1!$A$2:$A$5"),
                new XElement(C + "strCache",
                    new XElement(C + "ptCount", new XAttribute("val", 2_000_000_000)))));
        var ser = new XElement(C + "ser",
            new XElement(C + "idx", new XAttribute("val", 0)),
            new XElement(C + "order", new XAttribute("val", 0)),
            Tx("S1"),
            cat,
            Val(1.0, 2.0));
        var barChart = new XElement(C + "barChart", ser);
        var chartElement = Chart(barChart);

        // Act
        var exception = Assert.Throws<ChartUnsupportedFeatureException>(() => OpenXmlChartParser.Parse(chartElement));

        // Assert
        Assert.Equal("charts-openxml-point-count-too-large", exception.Feature);
    }

    #endregion

    #region Unsupported-type / combo / empty-plot-area rejection

    /// <summary>Proves every recognized-but-unimplemented chart-type element is rejected with its documented feature token.</summary>
    [Theory]
    [InlineData("radarChart", "charts-openxml-radar-chart")]
    [InlineData("scatterChart", "charts-openxml-scatter-chart")]
    [InlineData("bubbleChart", "charts-openxml-bubble-chart")]
    [InlineData("stockChart", "charts-openxml-stock-chart")]
    [InlineData("surfaceChart", "charts-openxml-surface-chart")]
    [InlineData("surface3DChart", "charts-openxml-surface-chart")]
    [InlineData("bar3DChart", "charts-openxml-3d-chart")]
    [InlineData("line3DChart", "charts-openxml-3d-chart")]
    [InlineData("pie3DChart", "charts-openxml-3d-chart")]
    [InlineData("area3DChart", "charts-openxml-3d-chart")]
    [InlineData("ofPieChart", "charts-openxml-of-pie-chart")]
    public void Parse_UnsupportedChartType_ThrowsWithExpectedFeatureToken(string localName, string expectedFeature)
    {
        // Arrange
        var unsupported = new XElement(C + localName, Ser("S1", [1.0, 2.0], ["A", "B"]));
        var chartElement = Chart(unsupported);

        // Act
        var exception = Assert.Throws<ChartUnsupportedFeatureException>(() => OpenXmlChartParser.Parse(chartElement));

        // Assert
        Assert.Equal(expectedFeature, exception.Feature);
    }

    /// <summary>Proves a combo chart (two chart-type elements sharing one plot area) is rejected.</summary>
    [Fact]
    public void Parse_ComboChart_ThrowsWithComboChartFeatureToken()
    {
        // Arrange
        var barChart = new XElement(C + "barChart", Ser("S1", [1.0, 2.0], ["A", "B"]));
        var lineChart = new XElement(C + "lineChart", Ser("S2", [3.0, 4.0], ["A", "B"]));
        var chartElement = new XElement(C + "chart",
            new XElement(C + "autoTitleDeleted", new XAttribute("val", 1)),
            new XElement(C + "plotArea", new XElement(C + "layout"), barChart, lineChart));

        // Act
        var exception = Assert.Throws<ChartUnsupportedFeatureException>(() => OpenXmlChartParser.Parse(chartElement));

        // Assert
        Assert.Equal("charts-openxml-combo-chart", exception.Feature);
    }

    /// <summary>Proves a plot area with no recognized chart-type element is rejected.</summary>
    [Fact]
    public void Parse_EmptyPlotArea_ThrowsWithNoChartTypeFeatureToken()
    {
        // Arrange
        var chartElement = new XElement(C + "chart",
            new XElement(C + "autoTitleDeleted", new XAttribute("val", 1)),
            new XElement(C + "plotArea", new XElement(C + "layout")));

        // Act
        var exception = Assert.Throws<ChartUnsupportedFeatureException>(() => OpenXmlChartParser.Parse(chartElement));

        // Assert
        Assert.Equal("charts-openxml-no-chart-type", exception.Feature);
    }

    #endregion

    #region Real-fixture-derived end-to-end test

    /// <summary>
    ///     Proves a trimmed, adapted excerpt of the real <c>chart1.xml</c> part found in
    ///     <c>aiden0z-1-chart-and-complex.pptx</c> (see <c>OpenXmlChartFixtures/README.md</c> for
    ///     exact provenance) parses into the expected <see cref="Chart"/> shape: a stacked column
    ///     chart (<c>barDir val="col"</c>), three series, four categories, a styled-but-textless
    ///     title (so <see cref="Chart.Title"/> is <see langword="null"/>), no axis titles, and a
    ///     bottom legend.
    /// </summary>
    [Fact]
    public void Parse_RealFixtureBarChartExcerpt_ParsesExpectedShape()
    {
        // Arrange - a trimmed, styling-stripped excerpt of the real chart1.xml's c:chart element
        // (the real file's <c:title> likewise has no <c:tx> child - only styling - confirming the
        // "present but textless" rule; its <c:legend> has <c:legendPos val="b"/>).
        var barChart = new XElement(C + "barChart",
            new XElement(C + "barDir", new XAttribute("val", "col")),
            new XElement(C + "grouping", new XAttribute("val", "stacked")),
            Ser("Series 1", [4.3, 2.5, 3.5, 4.5], ["Category 1", "Category 2", "Category 3", "Category 4"]),
            Ser("Series 2", [2.4, 4.4, 1.8, 2.8], ["Category 1", "Category 2", "Category 3", "Category 4"]),
            Ser("Series 3", [2.0, 2.0, 3.0, 5.0], ["Category 1", "Category 2", "Category 3", "Category 4"]));
        var chartElement = new XElement(C + "chart",
            new XElement(C + "title", new XElement(C + "overlay", new XAttribute("val", 0))),
            new XElement(C + "autoTitleDeleted", new XAttribute("val", 0)),
            new XElement(C + "plotArea", new XElement(C + "layout"), barChart,
                new XElement(C + "catAx"), new XElement(C + "valAx")),
            new XElement(C + "legend", new XElement(C + "legendPos", new XAttribute("val", "b"))));

        // Act
        var chart = OpenXmlChartParser.Parse(chartElement);

        // Assert
        Assert.Equal(ChartType.Column, chart.Type);
        Assert.Equal(3, chart.Series.Count);
        Assert.Equal("Series 1", chart.Series[0].Name);
        Assert.Equal([4.3, 2.5, 3.5, 4.5], chart.Series[0].Values);
        var categoryAxis = chart.CategoryAxis;
        Assert.NotNull(categoryAxis);
        Assert.Equal(["Category 1", "Category 2", "Category 3", "Category 4"], categoryAxis.Labels);
        Assert.Null(chart.Title);
        Assert.Null(categoryAxis.Title);
        Assert.Null(chart.ValueAxis);
        Assert.Equal(ChartLegendPosition.Bottom, chart.Legend?.Position);
    }

    #endregion

    #region Parse-then-render round-trip pixel-sanity tests

    /// <summary>
    ///     Proves a parsed bar/column/line/area/pie/doughnut chart renders onto a correctly
    ///     sized surface without throwing, for every supported <see cref="ChartType"/>.
    /// </summary>
    [Theory]
    [InlineData("barChart", "bar", ChartType.Bar)]
    [InlineData("barChart", "col", ChartType.Column)]
    [InlineData("lineChart", null, ChartType.Line)]
    [InlineData("areaChart", null, ChartType.Area)]
    [InlineData("pieChart", null, ChartType.Pie)]
    [InlineData("doughnutChart", null, ChartType.Doughnut)]
    public void Parse_ThenRender_EveryChartType_ProducesCorrectlySizedSurface(string localName, string? barDir, ChartType expectedType)
    {
        // Arrange
        var chartTypeElement = new XElement(C + localName);
        if (barDir is not null)
        {
            chartTypeElement.Add(new XElement(C + "barDir", new XAttribute("val", barDir)));
        }

        chartTypeElement.Add(Ser("S1", [3.0, 7.0], ["A", "B"]));
        var chartElement = Chart(chartTypeElement);

        // Act
        var chart = OpenXmlChartParser.Parse(chartElement);
        using var surface = ChartRenderer.Render(chart, 300, 200);

        // Assert
        Assert.Equal(expectedType, chart.Type);
        Assert.Equal(300, surface.Width);
        Assert.Equal(200, surface.Height);

        // The far top-left corner is always outside every chart type's painted content for this
        // chart (no title/legend reserved) - a deliberately type-agnostic smoke assertion that the
        // cleared background survives untouched there.
        Assert.Equal(White, surface[1, 1]);
    }

    /// <summary>Proves the real-fixture-derived excerpt parses and then renders onto a correctly sized surface.</summary>
    [Fact]
    public void Parse_ThenRender_RealFixtureBarChartExcerpt_ProducesCorrectlySizedSurface()
    {
        // Arrange
        var barChart = new XElement(C + "barChart",
            new XElement(C + "barDir", new XAttribute("val", "col")),
            new XElement(C + "grouping", new XAttribute("val", "stacked")),
            Ser("Series 1", [4.3, 2.5, 3.5, 4.5], ["Category 1", "Category 2", "Category 3", "Category 4"]),
            Ser("Series 2", [2.4, 4.4, 1.8, 2.8], ["Category 1", "Category 2", "Category 3", "Category 4"]),
            Ser("Series 3", [2.0, 2.0, 3.0, 5.0], ["Category 1", "Category 2", "Category 3", "Category 4"]));
        var chartElement = Chart(barChart, legend: new XElement(C + "legendPos", new XAttribute("val", "b")));

        // Act
        var chart = OpenXmlChartParser.Parse(chartElement);
        using var surface = ChartRenderer.Render(chart, 400, 300);

        // Assert
        Assert.Equal(400, surface.Width);
        Assert.Equal(300, surface.Height);
    }

    #endregion
}
