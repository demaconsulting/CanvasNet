namespace DemaConsulting.CanvasNet.VegaLite;

/// <summary>
///     The exception thrown when a chart cannot be rendered although its specification was read successfully, for
///     example because data is missing or invalid, an expression fails, or a resource limit is exceeded.
/// </summary>
/// <remarks>
///     Failures are fail-closed and carry the <see cref="Location"/> of the construct that caused them, so a caller
///     can tell the author where to look. Cancellation is not reported with this type: a canceled call throws
///     <see cref="OperationCanceledException"/>. The type derives from <see cref="InvalidOperationException"/>
///     because the document is valid but cannot be rendered in its current context.
/// </remarks>
public sealed class VegaLiteRenderException : InvalidOperationException
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="VegaLiteRenderException"/> class with an unknown location.
    /// </summary>
    public VegaLiteRenderException()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="VegaLiteRenderException"/> class with a message and an
    ///     unknown location.
    /// </summary>
    /// <param name="message">The error message.</param>
    public VegaLiteRenderException(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="VegaLiteRenderException"/> class with a message, an inner
    ///     exception and an unknown location.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public VegaLiteRenderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="VegaLiteRenderException"/> class describing a failure at a
    ///     known location.
    /// </summary>
    /// <param name="description">The description of the failure, without the location prefix.</param>
    /// <param name="location">The location of the construct that caused the failure.</param>
    public VegaLiteRenderException(string description, VegaLiteLocation location)
        : base(location.IsKnown ? $"{location}: {description}" : description)
    {
        Location = location;
    }

    /// <summary>
    ///     Gets the location of the construct that caused the failure; <c>default</c> (line zero, empty pointer) when unknown.
    /// </summary>
    public VegaLiteLocation Location { get; }
}
