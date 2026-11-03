using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Guides;

/// <summary>
///     Unit tests of <see cref="HeaderSettings"/>: the precedence of the header properties (field definition, channel configuration, general configuration), the defaults of
///     Vega-Lite, the side and band rules of the layout, and the formatting of the labels of the cells.
/// </summary>
public class HeaderSettingsTests
{
    /// <summary>
    ///     Reads the header settings of a facet channel from a specification text and an optional configuration.
    /// </summary>
    /// <param name="channel">The facet channel.</param>
    /// <param name="header">The JSON text of the <c>header</c> of the field definition (<c>null</c> is the JSON null), or null for no header.</param>
    /// <param name="callerConfig">The caller's configuration text, or null.</param>
    /// <returns>The settings.</returns>
    private static HeaderSettings Settings(string channel, string? header = null, string? callerConfig = null)
    {
        var field = header is null
            ? "{ \"field\": \"a\", \"type\": \"nominal\" }"
            : "{ \"field\": \"a\", \"type\": \"nominal\", \"header\": " + header + " }";
        var tree = SpecParser.Parse("{ \"data\": { \"values\": [ { \"a\": 1 } ] }, \"facet\": " + field + ", \"spec\": { \"mark\": \"point\" } }");
        return HeaderSettings.Read(ConfigResolver.Resolve(tree, callerConfig), tree.Root["facet"]!["header"], channel);
    }

    /// <summary>
    ///     Validates the order of lookup: the header of the field definition, then the configuration of the channel, then <c>config.header</c>.
    /// </summary>
    [Fact]
    public void HeaderSettings_Read_HeaderOverRowColumnConfigOverHeaderConfig()
    {
        // Arrange: three levels give the label font size, two give the padding, one gives the title font size
        const string config = """
            { "header": { "labelFontSize": 12, "labelPadding": 7, "titleFontSize": 15 },
              "headerRow": { "labelFontSize": 14 },
              "headerColumn": { "labelFontSize": 13, "labelPadding": 3 },
              "headerFacet": { "labelFontSize": 9 } }
            """;

        // Act
        var rowOwn = Settings("row", "{ \"labelFontSize\": 16 }", config);
        var row = Settings("row", null, config);
        var column = Settings("column", null, config);
        var facet = Settings("facet", null, config);

        // Assert: the header of the field definition wins, then the channel's configuration, then config.header
        Assert.Equal(16, rowOwn.Number("labelFontSize"));
        Assert.Equal(14, row.Number("labelFontSize"));
        Assert.Equal(13, column.Number("labelFontSize"));
        Assert.Equal(9, facet.Number("labelFontSize"));

        // Assert: a property that the channel's configuration does not give comes from config.header
        Assert.Equal(7, row.Number("labelPadding"));
        Assert.Equal(3, column.Number("labelPadding"));
        Assert.Equal(7, facet.Number("labelPadding"));
        Assert.Equal(15, row.Number("titleFontSize"));
        Assert.Null(row.Get("labelColor"));
    }

    /// <summary>
    ///     Validates the Vega-Lite defaults: label and title padding of 10, labels on, the default title included, no explicit side, anchor or band.
    /// </summary>
    [Fact]
    public void HeaderSettings_Read_DefaultsAreTitleAndLabelPaddingTen()
    {
        // Arrange / Act
        var row = Settings("row");
        var column = Settings("column");
        var facet = Settings("facet");

        // Assert: config.header of Vega-Lite is { titlePadding: 10, labelPadding: 10 }
        Assert.All(new[] { row, column, facet }, s =>
        {
            Assert.Equal(10, s.Number("labelPadding"));
            Assert.Equal(10, s.Number("titlePadding"));
            Assert.True(s.ShowLabels);
            Assert.True(s.IncludeDefaultTitle);
            Assert.False(s.Hidden);
            Assert.Null(s.Title);
            Assert.Null(s.LabelOrient);
            Assert.Null(s.TitleOrient);
            Assert.False(s.LabelsInFooter);
            Assert.False(s.TitleAtEnd);
            Assert.Null(s.TitleBand);
        });

        // Assert: without an orient a row's text is in a row header and the text of the other channels in a column header
        Assert.Equal(("row", "row"), (row.LabelHeaderChannel, row.TitleHeaderChannel));
        Assert.Equal(("column", "column"), (column.LabelHeaderChannel, column.TitleHeaderChannel));
        Assert.Equal(("column", "column"), (facet.LabelHeaderChannel, facet.TitleHeaderChannel));
        Assert.Equal("row", row.Channel);
    }

    /// <summary>
    ///     Validates that <c>"header": null</c> switches off the labels and the title, and that the field definition's own title is read.
    /// </summary>
    [Fact]
    public void HeaderSettings_Read_HeaderNullHidesLabelsAndTitle()
    {
        // Arrange / Act
        var hidden = Settings("column", "null");
        var titled = Settings("column", "{ \"title\": \"Region\" }");
        var untitled = Settings("column", "{ \"title\": null }");

        // Assert
        Assert.True(hidden.Hidden);
        Assert.False(hidden.ShowLabels);
        Assert.False(hidden.IncludeDefaultTitle);
        Assert.False(titled.Hidden);
        Assert.Equal("Region", titled.Title!.StringValue);
        Assert.True(untitled.Title!.IsNull);
    }

    /// <summary>
    ///     Validates that the <c>labels</c> flag is read from the header and then <c>config.header</c> only (Vega-Lite does not look at the configuration of the channel), and
    ///     that <c>"title": null</c> or <c>false</c> in the configuration of a channel stops the default title.
    /// </summary>
    [Fact]
    public void HeaderSettings_Read_LabelsFlagAndDefaultTitleFollowVegaLiteLookup()
    {
        // Arrange / Act
        var channelConfig = Settings("row", null, "{ \"headerRow\": { \"labels\": false } }");
        var generalConfig = Settings("row", null, "{ \"header\": { \"labels\": false } }");
        var headerWins = Settings("row", "{ \"labels\": true }", "{ \"header\": { \"labels\": false } }");
        var ownOff = Settings("row", "{ \"labels\": false }");
        var titleOff = Settings("column", null, "{ \"headerColumn\": { \"title\": null } }");
        var titleFalse = Settings("row", null, "{ \"header\": { \"title\": false } }");
        var titleOther = Settings("facet", null, "{ \"headerRow\": { \"title\": null } }");

        // Assert
        Assert.True(channelConfig.ShowLabels);
        Assert.False(generalConfig.ShowLabels);
        Assert.True(headerWins.ShowLabels);
        Assert.False(ownOff.ShowLabels);
        Assert.False(titleOff.IncludeDefaultTitle);
        Assert.False(titleFalse.IncludeDefaultTitle);
        Assert.True(titleOther.IncludeDefaultTitle);
    }

    /// <summary>
    ///     Validates Vega-Lite's <c>getHeaderChannel</c>: an orient on the top or bottom puts the text in a column header, on the left or right in a row header, and without
    ///     an orient the channel decides.
    /// </summary>
    /// <param name="channel">The facet channel.</param>
    /// <param name="orient">The orient, or null.</param>
    /// <param name="expected">The kind of header.</param>
    [Theory]
    [InlineData("row", null, "row")]
    [InlineData("row", "top", "column")]
    [InlineData("row", "bottom", "column")]
    [InlineData("row", "left", "row")]
    [InlineData("column", null, "column")]
    [InlineData("column", "left", "row")]
    [InlineData("column", "right", "row")]
    [InlineData("column", "bottom", "column")]
    [InlineData("facet", null, "column")]
    [InlineData("facet", "right", "row")]
    public void HeaderSettings_HeaderChannelOf_OrientDecidesTheKindOfHeader(string channel, string? orient, string expected)
    {
        // Act / Assert
        Assert.Equal(expected, HeaderSettings.HeaderChannelOf(channel, orient));
    }

    /// <summary>
    ///     Validates the orient and anchor rules of the layout: footers for <c>bottom</c> and <c>right</c> labels, the title at the end for those orients, and the title band
    ///     (0 or 1 along the cells) of <c>titleAnchor</c> <c>start</c> and <c>end</c>.
    /// </summary>
    [Fact]
    public void HeaderSettings_Read_OrientAndAnchorGiveFootersEndTitleAndBand()
    {
        // Arrange / Act
        var footer = Settings("column", "{ \"labelOrient\": \"bottom\", \"titleOrient\": \"bottom\" }");
        var right = Settings("row", "{ \"labelOrient\": \"right\", \"titleOrient\": \"right\" }");
        var invalid = Settings("row", null, "{ \"header\": { \"labelOrient\": \"middle\" } }");

        // Assert: bottom and right put the labels into the footers and the title after the cells
        Assert.True(footer.LabelsInFooter);
        Assert.True(footer.TitleAtEnd);
        Assert.Equal("bottom", footer.LabelOrient);
        Assert.True(right.LabelsInFooter);
        Assert.True(right.TitleAtEnd);
        Assert.Null(invalid.LabelOrient);

        // Assert: Vega-Lite's LAYOUT_TITLE_BAND is { column: { start 0, end 1 }, row: { start 1, end 0 } }; middle and unset keep the default
        Assert.Equal(0, Settings("column", "{ \"titleAnchor\": \"start\" }").TitleBand);
        Assert.Equal(1, Settings("column", "{ \"titleAnchor\": \"end\" }").TitleBand);
        Assert.Equal(1, Settings("row", "{ \"titleAnchor\": \"start\" }").TitleBand);
        Assert.Equal(0, Settings("row", "{ \"titleAnchor\": \"end\" }").TitleBand);
        Assert.Null(Settings("row", "{ \"titleAnchor\": \"middle\" }").TitleBand);

        // Assert: a row title that is oriented to the top is a column title, so the band follows the column rule
        Assert.Equal(0, Settings("row", "{ \"titleAnchor\": \"start\", \"titleOrient\": \"top\" }").TitleBand);
    }

    /// <summary>
    ///     Validates which sides the renderer honors: along the channel's headers (left and right for a row, top and bottom for a column), any side for the label of a wrapped facet and
    ///     the top and bottom for its title; and that, read with honored sides only, the other sides read as not given while all the sides are read by default.
    /// </summary>
    [Fact]
    public void HeaderSettings_IsHonoredOrient_AlongTheChannelOnlyAndReadCanIgnoreTheOthers()
    {
        // Arrange
        const string orthogonal = "{ \"labelOrient\": \"top\", \"titleOrient\": \"left\" }";
        var tree = SpecParser.Parse("{ \"data\": { \"values\": [ { \"a\": 1 } ] }, \"facet\": { \"field\": \"a\", \"type\": \"nominal\", \"header\": " + orthogonal + " }, \"spec\": { \"mark\": \"point\" } }");
        var config = ConfigResolver.Resolve(tree, null);
        var header = tree.Root["facet"]!["header"];

        // Act
        var all = HeaderSettings.Read(config, header, "row");
        var honored = HeaderSettings.Read(config, header, "row", honoredOrientsOnly: true);
        var facetHonored = HeaderSettings.Read(config, header, "facet", honoredOrientsOnly: true);

        // Assert: a row honors its title on the left; its label on top is not honored (read as not given when asked for honored sides only)
        Assert.Equal(("top", "left"), (all.LabelOrient, all.TitleOrient));
        Assert.Equal((null, "left"), (honored.LabelOrient, honored.TitleOrient));

        // Assert: a wrapped facet honors its label on any side and its title on the top and bottom only
        Assert.Equal(("top", null), (facetHonored.LabelOrient, facetHonored.TitleOrient));
        Assert.True(HeaderSettings.IsHonoredOrient("row", "labelOrient", "right"));
        Assert.False(HeaderSettings.IsHonoredOrient("row", "labelOrient", "top"));
        Assert.True(HeaderSettings.IsHonoredOrient("column", "titleOrient", "bottom"));
        Assert.False(HeaderSettings.IsHonoredOrient("column", "titleOrient", "right"));
        Assert.True(HeaderSettings.IsHonoredOrient("facet", "labelOrient", "left"));
        Assert.False(HeaderSettings.IsHonoredOrient("facet", "titleOrient", "left"));
    }

    /// <summary>
    ///     Validates that the facet channel is checked.
    /// </summary>
    [Fact]
    public void HeaderSettings_Read_ChecksItsArguments()
    {
        // Arrange
        var tree = SpecParser.Parse("{ \"mark\": \"point\" }");
        var config = ConfigResolver.Resolve(tree, null);

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => HeaderSettings.Read(null!, null, "row"));
        Assert.Throws<ArgumentNullException>(() => HeaderSettings.Read(config, null, null!));
        Assert.Throws<ArgumentException>(() => HeaderSettings.Read(config, null, "x"));
        Assert.Throws<ArgumentNullException>(() => HeaderSettings.Read(config, null, "row").Get(null!));
    }

    /// <summary>
    ///     Validates the label of a cell: a number with a d3 format (<c>formatType</c> <c>number</c>, or a quantitative field with a format), the JavaScript text of a number without
    ///     one, the text of a nominal value and the empty label of a null.
    /// </summary>
    [Fact]
    public void HeaderSettings_CreateLabelFormatter_FormatsNumbersAndText()
    {
        // Arrange: d3 ".1f" keeps one decimal, ".2f" two and ",d" groups thousands
        var field = new HeaderLabelFormat("quantitative", null, null, 100, default);
        var nominal = new HeaderLabelFormat("nominal", null, null, 100, default);

        // Act
        var oneDecimal = Settings("column", "{ \"format\": \".1f\", \"formatType\": \"number\" }").CreateLabelFormatter(nominal);
        var twoDecimals = Settings("column", "{ \"format\": \".2f\" }").CreateLabelFormatter(field);
        var grouped = Settings("column", "{ \"format\": \",d\" }").CreateLabelFormatter(field);
        var plainNumber = Settings("column").CreateLabelFormatter(field);
        var plainText = Settings("column", "{ \"format\": \".2f\" }").CreateLabelFormatter(nominal);

        // Assert
        Assert.Equal("3.1", oneDecimal(DataValue.FromNumber(3.14159)));
        Assert.Equal("2.50", twoDecimals(DataValue.FromNumber(2.5)));
        Assert.Equal("12,345", grouped(DataValue.FromNumber(12345)));
        Assert.Equal("0.5", plainNumber(DataValue.FromNumber(0.5)));
        Assert.Equal("Alpha", plainNumber(DataValue.FromString("Alpha")));
        Assert.Equal(string.Empty, plainNumber(DataValue.Null));

        // Assert: a nominal field is not formatted as a number without a formatType, a text under a number format stays text
        Assert.Equal("2.5", plainText(DataValue.FromNumber(2.5)));
        Assert.Equal("beta", oneDecimal(DataValue.FromString("beta")));
    }

    /// <summary>
    ///     Validates the label of a temporal cell: the d3 time <c>format</c>, else the specifier of the time unit, else <c>config.timeFormat</c> (<c>%b %d, %Y</c>), for a date, a
    ///     number of milliseconds and a date text.
    /// </summary>
    [Fact]
    public void HeaderSettings_CreateLabelFormatter_FormatsDatesWithFormatUnitAndDefault()
    {
        // Arrange: 2020-03-05T00:00:00Z computed from the calendar, not from the code under test
        var ms = new DateTimeOffset(2020, 3, 5, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        Assert.True(TimeUnitSpec.TryParse("yearmonth", out var yearMonth, out _));
        var temporal = new HeaderLabelFormat("temporal", null, null, 100, default);
        var withUnit = new HeaderLabelFormat("ordinal", yearMonth, null, 100, default);

        // Act
        var formatted = Settings("column", "{ \"format\": \"%Y-%m-%d\" }").CreateLabelFormatter(temporal);
        var byDefault = Settings("column").CreateLabelFormatter(temporal);
        var byUnit = Settings("column").CreateLabelFormatter(withUnit);
        var configured = Settings("column", null, "{ \"timeFormat\": \"%d.%m.%Y\" }").CreateLabelFormatter(temporal);
        var forcedTime = Settings("column", "{ \"formatType\": \"time\", \"format\": \"%B\" }").CreateLabelFormatter(new HeaderLabelFormat("nominal", null, null, 100, default));

        // Assert
        Assert.Equal("2020-03-05", formatted(DataValue.FromDate(ms)));
        Assert.Equal("2020-03-05", formatted(DataValue.FromNumber(ms)));
        Assert.Equal("2020-03-05", formatted(DataValue.FromString("2020-03-05T00:00:00Z")));
        Assert.Equal("Mar 05, 2020", byDefault(DataValue.FromDate(ms)));
        Assert.Equal("Mar 2020", byUnit(DataValue.FromDate(ms)));
        Assert.Equal("05.03.2020", configured(DataValue.FromDate(ms)));
        Assert.Equal("March", forcedTime(DataValue.FromDate(ms)));

        // Assert: a value that is no date stays its text
        Assert.Equal("n/a", formatted(DataValue.FromString("n/a")));
    }

    /// <summary>
    ///     Validates the errors of the label formatter: a format that is not a d3 specifier is a located spec error, and a label longer than the limit is a render error.
    /// </summary>
    [Fact]
    public void HeaderSettings_CreateLabelFormatter_RejectsBadFormatsAndLongLabels()
    {
        // Arrange
        var field = new HeaderLabelFormat("quantitative", null, null, 5, default);

        // Act / Assert: the number format is parsed when the formatter is created
        Assert.Throws<VegaLiteSpecException>(() => Settings("column", "{ \"format\": \"(((\" }", null).CreateLabelFormatter(field));
        var format = Settings("column").CreateLabelFormatter(field);
        Assert.Equal("abcde", format(DataValue.FromString("abcde")));
        Assert.Throws<VegaLiteRenderException>(() => format(DataValue.FromString("abcdef")));
        Assert.Throws<ArgumentNullException>(() => Settings("column").CreateLabelFormatter(null!));
    }
}
