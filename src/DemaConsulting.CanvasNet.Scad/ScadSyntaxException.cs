namespace DemaConsulting.CanvasNet.Scad;

/// <summary>
///     The exception thrown when OpenSCAD-language source cannot be tokenized or parsed.
/// </summary>
/// <remarks>
///     Parsing is fail-fast: the first lexical or grammatical error aborts parsing and is reported, so
///     <see cref="Location"/> always identifies the first error in the source (with the documented
///     exception that all lexical errors are detected before any grammatical error). It derives from
///     <see cref="FormatException"/> (the source text is not in the expected format) rather than
///     <see cref="InvalidDataException"/>, which is sealed; source that merely exceeds the size limit
///     is reported with a plain <see cref="InvalidDataException"/> because no location is meaningful.
/// </remarks>
public sealed class ScadSyntaxException : FormatException
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ScadSyntaxException"/> class with an unknown location.
    /// </summary>
    public ScadSyntaxException()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScadSyntaxException"/> class with a message and an
    ///     unknown location.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ScadSyntaxException(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScadSyntaxException"/> class with a message, an
    ///     inner exception and an unknown location.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public ScadSyntaxException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScadSyntaxException"/> class describing an error at a
    ///     known source location.
    /// </summary>
    /// <param name="description">The description of the error, without the location prefix.</param>
    /// <param name="location">The location of the error.</param>
    public ScadSyntaxException(string description, ScadSourceLocation location)
        : base(FormatMessage(description, location))
    {
        Location = location;
    }

    /// <summary>
    ///     Gets the location of the error; <c>default</c> (line zero) when the location is unknown.
    /// </summary>
    public ScadSourceLocation Location { get; }

    /// <summary>
    ///     Builds the exception message by prefixing the description with the location when it is known.
    /// </summary>
    /// <param name="description">The error description.</param>
    /// <param name="location">The error location.</param>
    /// <returns><c>{location}: {description}</c> when the location is known; otherwise the description alone.</returns>
    private static string FormatMessage(string description, ScadSourceLocation location) =>
        location.Line == 0 ? description : $"{location}: {description}";
}
