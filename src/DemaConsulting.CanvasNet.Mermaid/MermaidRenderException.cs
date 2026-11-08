namespace DemaConsulting.CanvasNet.Mermaid;

/// <summary>
///     The exception thrown when a diagram cannot be measured or rendered although its text was read successfully,
///     for example because a resource limit is exceeded or the layout fails.
/// </summary>
/// <remarks>
///     Failures are fail-closed and carry the <see cref="Location"/> of the construct that caused them and a stable
///     <see cref="Code"/> (<see cref="MermaidDiagnosticCodes.LimitExceeded"/> for a limit, whose message names the
///     <see cref="MermaidLimits"/> property). Cancellation is not reported with this type: a canceled call throws
///     <see cref="OperationCanceledException"/>. The type derives from <see cref="InvalidOperationException"/>
///     because the document is valid but cannot be rendered in its current context.
/// </remarks>
public sealed class MermaidRenderException : InvalidOperationException
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="MermaidRenderException"/> class with the code
    ///     <see cref="MermaidDiagnosticCodes.RenderFailed"/> and an unknown location.
    /// </summary>
    public MermaidRenderException()
    {
        Code = MermaidDiagnosticCodes.RenderFailed;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MermaidRenderException"/> class with a message, the code
    ///     <see cref="MermaidDiagnosticCodes.RenderFailed"/> and an unknown location.
    /// </summary>
    /// <param name="message">The error message.</param>
    public MermaidRenderException(string message)
        : base(message)
    {
        Code = MermaidDiagnosticCodes.RenderFailed;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MermaidRenderException"/> class with a message, an inner
    ///     exception, the code <see cref="MermaidDiagnosticCodes.RenderFailed"/> and an unknown location.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public MermaidRenderException(string message, Exception innerException)
        : base(message, innerException)
    {
        Code = MermaidDiagnosticCodes.RenderFailed;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MermaidRenderException"/> class describing a failure at a
    ///     location.
    /// </summary>
    /// <param name="code">The stable <c>mermaid-*</c> code of the failure. Must not be null or empty.</param>
    /// <param name="description">The description of the failure, without the location and the code.</param>
    /// <param name="location">The location of the construct that caused the failure.</param>
    /// <param name="hint">The suggested fix, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="code"/> is null or empty.</exception>
    public MermaidRenderException(string code, string description, MermaidLocation location, string? hint = null)
        : base(MermaidSyntaxException.FormatMessage(code, description, location))
    {
        ArgumentException.ThrowIfNullOrEmpty(code);
        Code = code;
        Location = location;
        Hint = hint;
    }

    /// <summary>
    ///     Gets the stable <c>mermaid-*</c> code of the failure.
    /// </summary>
    public string Code { get; }

    /// <summary>
    ///     Gets the location of the construct that caused the failure; <c>default</c> (line zero) when unknown.
    /// </summary>
    public MermaidLocation Location { get; }

    /// <summary>
    ///     Gets the suggested fix, or <see langword="null"/> when there is none.
    /// </summary>
    public string? Hint { get; }
}
