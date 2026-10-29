namespace DemaConsulting.CanvasNet.VegaLite;

/// <summary>
///     The exception thrown when a Vega-Lite specification is not valid JSON or is not a valid specification.
/// </summary>
/// <remarks>
///     Reading is fail-fast: the first problem aborts reading and is reported, so <see cref="Location"/> always
///     identifies the first error in the text. The type derives from <see cref="FormatException"/> (the input is
///     not in the expected format) rather than <see cref="InvalidDataException"/>, which is sealed; source that
///     merely exceeds the size limit is reported with a plain <see cref="InvalidDataException"/> because no location
///     is meaningful. Nesting deeper than <see cref="VegaLiteLimits.MaxJsonDepth"/> is a specification error with the
///     location where the limit was crossed.
/// </remarks>
public sealed class VegaLiteSpecException : FormatException
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="VegaLiteSpecException"/> class with an unknown location.
    /// </summary>
    public VegaLiteSpecException()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="VegaLiteSpecException"/> class with a message and an unknown
    ///     location.
    /// </summary>
    /// <param name="message">The error message.</param>
    public VegaLiteSpecException(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="VegaLiteSpecException"/> class with a message, an inner
    ///     exception and an unknown location.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public VegaLiteSpecException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="VegaLiteSpecException"/> class describing an error at a known
    ///     location.
    /// </summary>
    /// <param name="description">The description of the error, without the location prefix.</param>
    /// <param name="location">The location of the error.</param>
    public VegaLiteSpecException(string description, VegaLiteLocation location)
        : base(location.IsKnown ? $"{location}: {description}" : description)
    {
        Location = location;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="VegaLiteSpecException"/> class describing an error at a known
    ///     location that was caused by another exception.
    /// </summary>
    /// <param name="description">The description of the error, without the location prefix.</param>
    /// <param name="location">The location of the error.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public VegaLiteSpecException(string description, VegaLiteLocation location, Exception innerException)
        : base(location.IsKnown ? $"{location}: {description}" : description, innerException)
    {
        Location = location;
    }

    /// <summary>
    ///     Gets the location of the error; <c>default</c> (line zero, empty pointer) when unknown.
    /// </summary>
    public VegaLiteLocation Location { get; }
}
