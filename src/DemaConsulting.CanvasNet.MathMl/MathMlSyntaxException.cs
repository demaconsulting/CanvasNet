namespace DemaConsulting.CanvasNet.MathMl;

/// <summary>
///     The exception thrown when MathML text cannot be read: it is not valid UTF-8 or not well-formed XML, it declares a
///     document type or a foreign encoding, it uses an unknown named character reference, its root element is not
///     <c>math</c>, or a structural limit is exceeded.
/// </summary>
/// <remarks>
///     <para>
///         Reading is fail-fast: the first problem aborts reading and is reported, so <see cref="Location"/> always
///         identifies the first error in the original text. <see cref="Code"/> is the stable <c>mathml-*</c> identifier
///         of the problem (see <see cref="MathMlDiagnosticCodes"/>) and <see cref="Hint"/> suggests the fix. A browser
///         shows nothing, or a parse error page, for such text; this library throws instead, so a build can fail with a
///         precise message.
///     </para>
///     <para>
///         The type derives from <see cref="FormatException"/> (the input is not in the expected format). Text that is
///         merely longer than <see cref="MathMlLimits.MaxTextSize"/> allows is reported with a plain
///         <see cref="InvalidDataException"/>, because no location is meaningful.
///     </para>
/// </remarks>
public sealed class MathMlSyntaxException : FormatException
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="MathMlSyntaxException"/> class with the code
    ///     <see cref="MathMlDiagnosticCodes.SyntaxError"/> and an unknown location.
    /// </summary>
    public MathMlSyntaxException()
    {
        Code = MathMlDiagnosticCodes.SyntaxError;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MathMlSyntaxException"/> class with a message, the code
    ///     <see cref="MathMlDiagnosticCodes.SyntaxError"/> and an unknown location.
    /// </summary>
    /// <param name="message">The error message.</param>
    public MathMlSyntaxException(string message)
        : base(message)
    {
        Code = MathMlDiagnosticCodes.SyntaxError;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MathMlSyntaxException"/> class with a message, an inner
    ///     exception, the code <see cref="MathMlDiagnosticCodes.SyntaxError"/> and an unknown location.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public MathMlSyntaxException(string message, Exception innerException)
        : base(message, innerException)
    {
        Code = MathMlDiagnosticCodes.SyntaxError;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MathMlSyntaxException"/> class describing an error at a
    ///     location.
    /// </summary>
    /// <param name="code">The stable <c>mathml-*</c> code of the error. Must not be null or empty.</param>
    /// <param name="description">The description of the error, without the location and the code.</param>
    /// <param name="location">The location of the error in the original text.</param>
    /// <param name="hint">The suggested fix, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="code"/> is null or empty.</exception>
    public MathMlSyntaxException(string code, string description, MathMlLocation location, string? hint = null)
        : base(FormatMessage(code, description, location))
    {
        ArgumentException.ThrowIfNullOrEmpty(code);
        Code = code;
        Location = location;
        Hint = hint;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MathMlSyntaxException"/> class describing an error at a
    ///     location that another exception caused (the <see cref="System.Xml.XmlException"/> of the XML reader).
    /// </summary>
    /// <param name="innerException">The exception that caused the error.</param>
    /// <param name="code">The stable <c>mathml-*</c> code of the error. Must not be null or empty.</param>
    /// <param name="description">The description of the error, without the location and the code.</param>
    /// <param name="location">The location of the error in the original text.</param>
    /// <param name="hint">The suggested fix, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="code"/> is null or empty.</exception>
    internal MathMlSyntaxException(Exception innerException, string code, string description, MathMlLocation location, string? hint)
        : base(FormatMessage(code, description, location), innerException)
    {
        ArgumentException.ThrowIfNullOrEmpty(code);
        Code = code;
        Location = location;
        Hint = hint;
    }

    /// <summary>
    ///     Gets the stable <c>mathml-*</c> code of the error.
    /// </summary>
    public string Code { get; }

    /// <summary>
    ///     Gets the location of the error in the original text; <c>default</c> (line zero) when unknown.
    /// </summary>
    public MathMlLocation Location { get; }

    /// <summary>
    ///     Gets the suggested fix, or <see langword="null"/> when there is none.
    /// </summary>
    public string? Hint { get; }

    /// <summary>
    ///     Formats the message of a located error.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <param name="description">The description.</param>
    /// <param name="location">The location.</param>
    /// <returns><c>{location}: {code}: {description}</c>, without the location when it is unknown.</returns>
    internal static string FormatMessage(string code, string description, MathMlLocation location) =>
        location.IsKnown ? $"{location}: {code}: {description}" : $"{code}: {description}";
}
