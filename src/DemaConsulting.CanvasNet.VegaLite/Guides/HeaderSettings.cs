// Portions derived from Vega-Lite (BSD-3-Clause); see third-party/README.md
using System.Globalization;
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Guides;

/// <summary>
///     What decides how the labels of a facet header are formatted: the type and time unit of the field that is faceted and the limits of the text.
/// </summary>
/// <param name="FieldType">The <c>type</c> of the facet field definition (<c>nominal</c>, <c>ordinal</c>, <c>quantitative</c> or <c>temporal</c>), or null.</param>
/// <param name="TimeUnit">The time unit of the facet field definition, or null.</param>
/// <param name="Calendar">The calendar of the dates (the zone of the chart); UTC when null.</param>
/// <param name="MaxLength">The longest label allowed in characters (<see cref="VegaLiteLimits.MaxStringLength"/>).</param>
/// <param name="Location">The location of the facet field definition, for a label that is too long.</param>
internal sealed record HeaderLabelFormat(string? FieldType, TimeUnitSpec? TimeUnit, TimeCalendar? Calendar, int MaxLength, VegaLiteLocation Location);

/// <summary>
///     The properties of the header of one facet channel: Vega-Lite's <c>header</c> object of the channel definition resolved over <c>config.headerRow</c>,
///     <c>config.headerColumn</c> or <c>config.headerFacet</c> and then <c>config.header</c>.
/// </summary>
/// <remarks>
///     <para>
///         <b>Precedence.</b> A property is the first of these that defines it (Vega-Lite's <c>getHeaderProperty</c>): the <c>header</c> of the field definition, the configuration of the
///         channel (<c>headerRow</c> for <c>row</c>, <c>headerColumn</c> for <c>column</c>, <c>headerFacet</c> for <c>facet</c>), then <c>config.header</c>. A value of null counts as
///         defined, so <c>"title": null</c> switches a title off for every level below it. The defaults are Vega-Lite's: <c>config.header</c> has <c>labelPadding</c> and <c>titlePadding</c>
///         of 10, and the text sizes come from the Vega styles <c>guide-label</c> (10 pixel) and <c>guide-title</c> (11 pixel, bold); the <see cref="TitleBuilder"/> applies them.
///     </para>
///     <para>
///         <b>What the properties decide here.</b> <see cref="Hidden"/> (<c>header: null</c>) draws neither labels nor title, <see cref="ShowLabels"/> is the <c>labels</c> flag
///         (the header of the field definition, else <c>config.header.labels</c>, else on), <see cref="LabelHeaderChannel"/> and <see cref="TitleHeaderChannel"/> are the kind of header
///         (<c>row</c> or <c>column</c>) that a <c>labelOrient</c> or <c>titleOrient</c> puts the text into (Vega-Lite's <c>getHeaderChannel</c>), <see cref="LabelsInFooter"/> and
///         <see cref="TitleAtEnd"/> the side of the cells, and <see cref="TitleBand"/> where along the cells the title is centered (<c>titleAnchor</c> <c>start</c> and <c>end</c>).
///         <see cref="CreateLabelFormatter"/> turns the value of a cell into its label (<c>format</c>, <c>formatType</c>).
///     </para>
///     <para>
///         <b>Label expressions.</b> The <c>labelExpr</c> is read like any property (<see cref="Get"/>) and evaluated by the composition over the formatted label (<see cref="LabelExpressions"/>; the datum has
///         <c>value</c> and <c>label</c>).
///     </para>
///     <para>
///         <b>Not interpreted.</b> The <c>orient</c> shortcut (Vega-Lite's compiler does not read it either; only <c>labelOrient</c> and <c>titleOrient</c> count), a custom
///         <c>formatType</c>, and properties that are expressions or signals; the feature catalog reports the ones that change the drawing. The object is immutable and safe for concurrent use.
///     </para>
/// </remarks>
internal sealed class HeaderSettings
{
    /// <summary>The <c>header</c> object of the field definition; null when there is none.</summary>
    private readonly SpecNode? _header;

    /// <summary>The configuration of the channel (<c>headerRow</c>, <c>headerColumn</c> or <c>headerFacet</c>), or null.</summary>
    private readonly ConfigNode? _channelConfig;

    /// <summary>The general header configuration (<c>config.header</c>), or null.</summary>
    private readonly ConfigNode? _generalConfig;

    /// <summary>Whether a <c>labelOrient</c> or <c>titleOrient</c> that <see cref="IsHonoredOrient"/> does not honor reads as not given.</summary>
    private readonly bool _honoredOrientsOnly;

    /// <summary>
    ///     Initializes the settings.
    /// </summary>
    /// <param name="config">The effective configuration.</param>
    /// <param name="header">The <c>header</c> of the field definition.</param>
    /// <param name="channel">The facet channel.</param>
    /// <param name="honoredOrientsOnly">Whether the orients that the renderer does not honor read as not given.</param>
    private HeaderSettings(ResolvedConfig config, SpecNode? header, string channel, bool honoredOrientsOnly)
    {
        Config = config;
        Channel = channel;
        _honoredOrientsOnly = honoredOrientsOnly;
        _header = header is { IsObject: true } ? header : null;
        Hidden = header is { IsNull: true };
        _channelConfig = config.Root[channel switch { "row" => "headerRow", "column" => "headerColumn", _ => "headerFacet" }];
        _generalConfig = config.Root["header"];
    }

    /// <summary>Gets the effective configuration.</summary>
    public ResolvedConfig Config { get; }

    /// <summary>Gets the facet channel the header belongs to: <c>row</c>, <c>column</c> or <c>facet</c>.</summary>
    public string Channel { get; }

    /// <summary>Gets a value indicating whether the header is switched off (<c>"header": null</c>): there are no labels and no title.</summary>
    public bool Hidden { get; }

    /// <summary>
    ///     Gets a value indicating whether the labels are drawn: the <c>labels</c> of the header, else of <c>config.header</c> (Vega-Lite does not look at the configuration of the channel), else on.
    /// </summary>
    public bool ShowLabels => !Hidden && (_header?["labels"]?.AsBool() ?? _generalConfig?["labels"]?.AsBool() ?? true);

    /// <summary>Gets the <c>title</c> that the header gives itself (text, lines or null), or null when it gives none.</summary>
    public SpecNode? Title => _header?["title"];

    /// <summary>
    ///     Gets a value indicating whether the title that the field would give (its field name or its own <c>title</c>) is used: false when the configuration of the channel says
    ///     <c>"title": null</c> or <c>false</c> (Vega-Lite's <c>includeDefault</c>), and when the header is <see cref="Hidden"/>.
    /// </summary>
    public bool IncludeDefaultTitle
    {
        get
        {
            if (Hidden)
            {
                return false;
            }

            var configured = _channelConfig?["title"] ?? _generalConfig?["title"];
            return configured is null || !(configured.Kind == ConfigNodeKind.Null || configured is { Kind: ConfigNodeKind.Boolean, BoolValue: false } || configured is { Kind: ConfigNodeKind.String, StringValue.Length: 0 });
        }
    }

    /// <summary>Gets the <c>labelOrient</c> (<c>top</c>, <c>bottom</c>, <c>left</c> or <c>right</c>), or null for the default side.</summary>
    public string? LabelOrient => Orient("labelOrient");

    /// <summary>Gets the <c>titleOrient</c> (<c>top</c>, <c>bottom</c>, <c>left</c> or <c>right</c>), or null for the default side.</summary>
    public string? TitleOrient => Orient("titleOrient");

    /// <summary>Gets the kind of header the labels are drawn in: <c>row</c> or <c>column</c> (Vega-Lite's <c>getHeaderChannel</c>).</summary>
    public string LabelHeaderChannel => HeaderChannelOf(Channel, LabelOrient);

    /// <summary>Gets the kind of header the title is drawn in: <c>row</c> or <c>column</c>.</summary>
    public string TitleHeaderChannel => HeaderChannelOf(Channel, TitleOrient);

    /// <summary>Gets a value indicating whether the labels go to the footers (below or right of the cells) instead of the headers.</summary>
    public bool LabelsInFooter => LabelOrient is "bottom" or "right";

    /// <summary>Gets a value indicating whether the title goes below or right of the cells (Vega-Lite puts <c>titleAnchor: end</c> into the layout then).</summary>
    public bool TitleAtEnd => TitleOrient is "bottom" or "right";

    /// <summary>
    ///     Gets where along the cells the title is centered, as the fraction that Vega's layout takes as <c>titleBand</c>: <c>titleAnchor</c> <c>start</c> is 0 for a column title and 1 for
    ///     a row title (a row runs from the bottom), <c>end</c> the other way round; null for <c>middle</c> and an unset anchor (the layout then centers, 0.5).
    /// </summary>
    public double? TitleBand
    {
        get
        {
            var column = TitleHeaderChannel == "column";
            return Text("titleAnchor") switch
            {
                "start" => column ? 0 : 1,
                "end" => column ? 1 : 0,
                _ => null,
            };
        }
    }

    /// <summary>
    ///     Reads the header properties of a facet channel.
    /// </summary>
    /// <param name="config">The effective configuration.</param>
    /// <param name="header">The <c>header</c> property of the field definition of the channel (an object, or null for no header), or null when it has none.</param>
    /// <param name="channel">The facet channel: <c>row</c>, <c>column</c> or <c>facet</c>.</param>
    /// <param name="honoredOrientsOnly">
    ///     Whether a <c>labelOrient</c> or <c>titleOrient</c> that the renderer does not honor (<see cref="IsHonoredOrient"/>) is read as not given, so that the default side is used (the feature
    ///     catalog reports it as <c>vl-header-orient</c>); false reads every side.
    /// </param>
    /// <returns>The settings.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> or <paramref name="channel"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="channel"/> is not <c>row</c>, <c>column</c> or <c>facet</c>.</exception>
    public static HeaderSettings Read(ResolvedConfig config, SpecNode? header, string channel, bool honoredOrientsOnly = false)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(channel);
        if (channel is not ("row" or "column" or "facet"))
        {
            throw new ArgumentException("The facet channel must be row, column or facet.", nameof(channel));
        }

        return new HeaderSettings(config, header, channel, honoredOrientsOnly);
    }

    /// <summary>
    ///     Tells whether the renderer draws a header text on a side.
    /// </summary>
    /// <remarks>
    ///     The labels of a <c>row</c> are on the <c>left</c> (the headers) or the <c>right</c> (the footers), those of a <c>column</c> on the <c>top</c> or the <c>bottom</c>, and the label of a
    ///     wrapped <c>facet</c> is the title of its cell on any side; the title of a channel is beside the headers or the footers of its channel (a title on a side across the
    ///     cells, such as the <c>top</c> of a <c>row</c>, is not drawn there) and the title of a wrapped <c>facet</c> is above or below the cells.
    /// </remarks>
    /// <param name="channel">The facet channel: <c>row</c>, <c>column</c> or <c>facet</c>.</param>
    /// <param name="property">The property: <c>labelOrient</c> or <c>titleOrient</c>.</param>
    /// <param name="orient">The side: <c>top</c>, <c>bottom</c>, <c>left</c> or <c>right</c>.</param>
    /// <returns><see langword="true"/> when the side is honored.</returns>
    public static bool IsHonoredOrient(string channel, string property, string orient) => (channel, property, orient) switch
    {
        ("row", _, "left" or "right") => true,
        ("column", _, "top" or "bottom") => true,
        ("facet", "labelOrient", "top" or "bottom" or "left" or "right") => true,
        ("facet", "titleOrient", "top" or "bottom") => true,
        _ => false,
    };

    /// <summary>
    ///     Gets the kind of header (<c>row</c> or <c>column</c>) that a facet channel and an orient put some text into: Vega-Lite's <c>getHeaderChannel</c>.
    /// </summary>
    /// <param name="channel">The facet channel.</param>
    /// <param name="orient">The orient, or null.</param>
    /// <returns><c>column</c> for <c>top</c> and <c>bottom</c>, <c>row</c> for <c>left</c> and <c>right</c>; otherwise <c>row</c> for the <c>row</c> channel and <c>column</c> for the others.</returns>
    public static string HeaderChannelOf(string channel, string? orient) => orient switch
    {
        "top" or "bottom" => "column",
        "left" or "right" => "row",
        _ => channel == "row" ? "row" : "column",
    };

    /// <summary>
    ///     Gets a header property: the <c>header</c> of the field definition, else the configuration of the channel, else <c>config.header</c>.
    /// </summary>
    /// <param name="property">The property name, such as <c>labelFontSize</c>.</param>
    /// <returns>The value, or null when no level defines it.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="property"/> is null.</exception>
    public ConfigNode? Get(string property)
    {
        ArgumentNullException.ThrowIfNull(property);
        return _header?[property] is { } own ? ConfigNode.FromSpec(own) : _channelConfig?[property] ?? _generalConfig?[property];
    }

    /// <summary>
    ///     Gets the node of a property that the <c>header</c> of the field definition itself has (not the configuration), such as the compiled <c>labelExpr</c>.
    /// </summary>
    /// <param name="property">The property name.</param>
    /// <returns>The node (a null node for JSON null), or null when the header does not have the property.</returns>
    public SpecNode? ExplicitNode(string property) => _header?[property];

    /// <summary>
    ///     Reads a text property (a number is read as its text).
    /// </summary>
    /// <param name="property">The property name.</param>
    /// <returns>The text, or null when the property is not defined or neither text nor a number.</returns>
    public string? Text(string property)
    {
        var node = Get(property);
        return node?.Kind switch
        {
            ConfigNodeKind.String => node.StringValue,
            ConfigNodeKind.Number => node.NumberValue.ToString("R", CultureInfo.InvariantCulture),
            _ => null,
        };
    }

    /// <summary>
    ///     Reads a number property.
    /// </summary>
    /// <param name="property">The property name.</param>
    /// <returns>The number, or null when the property is not defined or not a number.</returns>
    public double? Number(string property) => Get(property)?.AsNumber();

    /// <summary>
    ///     Gets the location of the <c>header</c> object (or of the field definition's <c>format</c>), for errors.
    /// </summary>
    /// <param name="sourceName">The source name, or null.</param>
    /// <returns>The location; the default location when there is no header object.</returns>
    public VegaLiteLocation HeaderLocation(string? sourceName) => (_header?["format"] ?? _header)?.GetLocation(sourceName) ?? default;

    /// <summary>
    ///     Creates the function that turns the value of a cell into the text of its label.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The rules are Vega-Lite's <c>formatSignalRef</c> for a facet field that is not binned: with a <c>formatType</c> of <c>number</c> the value is formatted by the d3 number
    ///         <c>format</c> (the JavaScript text of the number without one); with <c>time</c> or <c>utc</c>, or without a <c>formatType</c> for a temporal field (a <c>type</c> of
    ///         <c>temporal</c> or a time unit), it is a date formatted by the d3 time <c>format</c>, else by the specifier of the time unit, else by <c>config.timeFormat</c>; a
    ///         quantitative field with a text <c>format</c> and no <c>formatType</c> is formatted as a number; every other value is its text (a null is empty). A custom
    ///         <c>formatType</c> is not evaluated and is treated as absent.
    ///     </para>
    ///     <para>A label longer than <see cref="HeaderLabelFormat.MaxLength"/> characters is a located <see cref="VegaLiteRenderException"/>; the function is safe to call from one thread.</para>
    /// </remarks>
    /// <param name="field">What decides the format.</param>
    /// <param name="sourceName">The source name for the location of a format that is not valid, or null.</param>
    /// <returns>The function.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="field"/> is null.</exception>
    /// <exception cref="VegaLiteSpecException">Thrown, at the <c>format</c>, when a number format is not a d3 format specifier or a time format is not valid.</exception>
    public Func<DataValue, string> CreateLabelFormatter(HeaderLabelFormat field, string? sourceName = null)
    {
        ArgumentNullException.ThrowIfNull(field);
        var format = Get("format");
        var formatType = Text("formatType");
        var known = TimeLabels.IsKnownFormatType(formatType) ? formatType : null;
        var formatLocation = HeaderLocation(sourceName);
        var temporal = known is "time" or "utc" || (known is null && (field.FieldType == "temporal" || field.TimeUnit is not null));
        var quantitativeNumber = known is null && !temporal && field.FieldType == "quantitative" && format is { Kind: ConfigNodeKind.String };
        if (known == "number" || quantitativeNumber)
        {
            NumberFormat? number = null;
            if (format?.AsString() is { } specifier)
            {
                try
                {
                    number = NumberFormat.Create(specifier);
                }
                catch (FormatException ex)
                {
                    throw new VegaLiteSpecException($"The header format is not a valid d3 format specifier: {ex.Message}", formatLocation, ex);
                }
            }

            return value => Checked(value.IsNumber ? FormatNumber(number, value.Number) : PlainText(value), field);
        }

        if (temporal)
        {
            var calendar = field.Calendar ?? TimeCalendar.Utc;
            var time = TimeLabels.CreateBounded(new TimeLabels.Options(
                format,
                known,
                field.TimeUnit,
                Config.GetString("timeFormat"),
                calendar,
                field.MaxLength,
                "header",
                field.Location,
                formatLocation));
            return value =>
            {
                var ms = InstantOf(value, calendar);
                if (double.IsNaN(ms))
                {
                    return Checked(PlainText(value), field);
                }

                return time(ms, field.MaxLength) ?? throw TooLong(field);
            };
        }

        return value => Checked(PlainText(value), field);
    }

    /// <summary>
    ///     Formats a number with the d3 format of the header, or as JavaScript writes it when there is none.
    /// </summary>
    /// <param name="format">The format, or null.</param>
    /// <param name="value">The number.</param>
    /// <returns>The text.</returns>
    private static string FormatNumber(NumberFormat? format, double value) => format is null ? JsNumber.ToString(value) : format.Format(value);

    /// <summary>
    ///     Reads an orient property.
    /// </summary>
    /// <param name="property">The property name.</param>
    /// <returns>The orient, or null when it is not defined or not one of the four sides.</returns>
    private string? Orient(string property)
    {
        var text = Text(property);
        return text is "top" or "bottom" or "left" or "right" && (!_honoredOrientsOnly || IsHonoredOrient(Channel, property, text)) ? text : null;
    }

    /// <summary>
    ///     Gets the instant that a cell value stands for as a date.
    /// </summary>
    /// <param name="value">The value of the cell.</param>
    /// <param name="calendar">The calendar of the chart.</param>
    /// <returns>The milliseconds since the epoch; NaN when the value is not a date.</returns>
    private static double InstantOf(DataValue value, TimeCalendar calendar)
    {
        if (value.IsDate)
        {
            return value.Milliseconds;
        }

        if (value.IsNumber)
        {
            return Math.Truncate(value.Number);
        }

        if (value.IsString && JsDateParser.TryParse(value.Text!, calendar, out var parsed))
        {
            return parsed;
        }

        return double.NaN;
    }

    /// <summary>
    ///     Gets the text of a value as Vega draws it: empty for null.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The text.</returns>
    private static string PlainText(DataValue value) => value.IsNull ? string.Empty : value.ToText();

    /// <summary>
    ///     Checks the length of a label.
    /// </summary>
    /// <param name="text">The label.</param>
    /// <param name="field">The limits.</param>
    /// <returns>The label.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the label is longer than the limit.</exception>
    private static string Checked(string text, HeaderLabelFormat field) => text.Length > field.MaxLength ? throw TooLong(field) : text;

    /// <summary>
    ///     Creates the exception for a label that is longer than the limit.
    /// </summary>
    /// <param name="field">The limits.</param>
    /// <returns>The exception, located at the facet field.</returns>
    private static VegaLiteRenderException TooLong(HeaderLabelFormat field) =>
        new($"A label of the facet header is longer than the limit of {field.MaxLength} characters (VegaLiteLimits.MaxStringLength).", field.Location);
}
