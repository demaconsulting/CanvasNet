namespace DemaConsulting.CanvasNet.Charts.OpenXml;

/// <summary>
///     The exception thrown when <see cref="OpenXmlChartParser"/> refuses to parse an otherwise
///     well-formed DrawingML <c>c:chartSpace</c>/<c>c:chart</c> element because it declares a
///     chart kind or data shape this parser does not implement (for example a radar, bubble,
///     scatter, stock, surface, 3-D, or "of pie" chart type; a combo chart combining more than
///     one chart-type element in a single plot area; or a series whose value cache is absent -
///     see <see cref="OpenXmlChartParser"/>'s own remarks for the exact supported/deferred
///     chart-type boundary).
/// </summary>
/// <remarks>
///     This type intentionally mirrors the shape of the equivalent "well-formed but unsupported"
///     exception already established elsewhere in the CanvasNet family of packages (a short,
///     stable, machine-matchable <see cref="Feature"/> token distinct from the free-text,
///     human-readable <see cref="Exception.Message"/>, five parallel constructors, and a base
///     type of <see cref="IOException"/> rather than the sealed <see cref="InvalidDataException"/>
///     - a sibling of <see cref="InvalidDataException"/>, not its base type, since
///     <see cref="InvalidDataException"/> derives directly from <see cref="SystemException"/>,
///     not <see cref="IOException"/>). This type is deliberately re-declared here, local to
///     <c>DemaConsulting.CanvasNet.Charts</c>, rather than reused from another CanvasNet package:
///     <c>DemaConsulting.CanvasNet.Charts</c> must never take a project or package reference on
///     any host-document-format package (see the <c>Charts</c> namespace's own documented
///     dependency-direction constraint), so it cannot reference an exception type declared in one
///     of those packages even though the two types' shape is intentionally identical. A caller
///     that wants to handle both the malformed-XML case and the well-formed-but-unsupported case
///     together cannot do so with a single <c>catch (IOException)</c> clause and must add two
///     explicit clauses. <see cref="Feature"/> identifies which unsupported feature was
///     encountered (for example <c>"charts-openxml-combo-chart"</c>,
///     <c>"charts-openxml-radar-chart"</c>, <c>"charts-openxml-uncached-values"</c>), distinct
///     from the free-text <see cref="Exception.Message"/>.
/// </remarks>
public sealed class ChartUnsupportedFeatureException : IOException
{
    /// <summary>
    ///     A short, stable, machine-matchable token identifying which unsupported feature caused
    ///     this exception to be thrown (for example <c>"charts-openxml-combo-chart"</c>), or
    ///     <see cref="string.Empty"/> when this exception was constructed via one of the standard
    ///     parameterless/message-only/inner-exception constructors rather than the two
    ///     feature-carrying constructors below.
    /// </summary>
    public string Feature { get; }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChartUnsupportedFeatureException"/> class
    ///     with a default message and an empty <see cref="Feature"/>.
    /// </summary>
    public ChartUnsupportedFeatureException()
        : this(string.Empty, "An unsupported OOXML chart feature was encountered.")
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChartUnsupportedFeatureException"/> class
    ///     with the specified message and an empty <see cref="Feature"/>.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public ChartUnsupportedFeatureException(string message)
        : this(string.Empty, message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChartUnsupportedFeatureException"/> class
    ///     with the specified message, inner exception, and an empty <see cref="Feature"/>.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    public ChartUnsupportedFeatureException(string message, Exception innerException)
        : this(string.Empty, message, innerException)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChartUnsupportedFeatureException"/> class
    ///     with the specified feature token and message.
    /// </summary>
    /// <param name="feature">
    ///     A short, stable, machine-matchable token identifying the unsupported feature (for
    ///     example <c>"charts-openxml-combo-chart"</c>).
    /// </param>
    /// <param name="message">The message that describes the error.</param>
    public ChartUnsupportedFeatureException(string feature, string message)
        : base(message)
        => Feature = feature;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChartUnsupportedFeatureException"/> class
    ///     with the specified feature token, message, and inner exception.
    /// </summary>
    /// <param name="feature">
    ///     A short, stable, machine-matchable token identifying the unsupported feature (for
    ///     example <c>"charts-openxml-combo-chart"</c>).
    /// </param>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    public ChartUnsupportedFeatureException(string feature, string message, Exception innerException)
        : base(message, innerException)
        => Feature = feature;
}
