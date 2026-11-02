namespace DemaConsulting.CanvasNet.Packaging;

/// <summary>
///     The exception <see cref="OpcPackage"/> throws when an Open Packaging Conventions package is
///     malformed, is missing a required part, exceeds a size bound, or is asked for something it does
///     not contain.
/// </summary>
/// <remarks>
///     A caller that exposes its own exception type for its own file format (for example a
///     <c>.pptx</c> reader) catches this type and rethrows its own exception with its own wording,
///     using this exception as the inner exception. A ZIP entry whose compressed data is corrupt is
///     not reported with this type: it surfaces as the <see cref="InvalidDataException"/> the .NET ZIP
///     classes raise when the part is read.
/// </remarks>
public sealed class OpcFormatException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="OpcFormatException"/> class.</summary>
    public OpcFormatException()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="OpcFormatException"/> class with a message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public OpcFormatException(string? message)
        : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="OpcFormatException"/> class with a message and
    ///     the exception that caused it.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    public OpcFormatException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
