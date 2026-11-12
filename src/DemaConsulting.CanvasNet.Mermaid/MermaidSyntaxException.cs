namespace DemaConsulting.CanvasNet.Mermaid;

/// <summary>
///     The exception thrown when Mermaid text cannot be read: it does not start with a known diagram keyword, its YAML
///     frontmatter is invalid, or it does not follow the grammar of its diagram type.
/// </summary>
/// <remarks>
///     <para>
///         Reading is fail-fast: the first problem aborts reading and is reported, so <see cref="Location"/> always
///         identifies the first error in the original text (frontmatter lines included). <see cref="Code"/> is the stable
///         <c>mermaid-*</c> identifier of the problem (see <see cref="MermaidDiagnosticCodes"/>), <see cref="Hint"/>
///         suggests the fix, and <see cref="Expected"/> lists what the grammar would have accepted at that point, like
///         the "Expecting ..." part of Mermaid's own parse errors. Mermaid draws an error picture for such text; this
///         library throws instead, so a build can fail with a precise message.
///     </para>
///     <para>
///         The type derives from <see cref="FormatException"/> (the input is not in the expected format). Text that is
///         merely longer than <see cref="MermaidLimits.MaxTextSize"/> is reported with a plain
///         <see cref="InvalidDataException"/>, because no location is meaningful.
///     </para>
/// </remarks>
public sealed class MermaidSyntaxException : FormatException
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="MermaidSyntaxException"/> class with the code
    ///     <see cref="MermaidDiagnosticCodes.SyntaxError"/> and an unknown location.
    /// </summary>
    public MermaidSyntaxException()
    {
        Code = MermaidDiagnosticCodes.SyntaxError;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MermaidSyntaxException"/> class with a message, the code
    ///     <see cref="MermaidDiagnosticCodes.SyntaxError"/> and an unknown location.
    /// </summary>
    /// <param name="message">The error message.</param>
    public MermaidSyntaxException(string message)
        : base(message)
    {
        Code = MermaidDiagnosticCodes.SyntaxError;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MermaidSyntaxException"/> class with a message, an inner
    ///     exception, the code <see cref="MermaidDiagnosticCodes.SyntaxError"/> and an unknown location.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public MermaidSyntaxException(string message, Exception innerException)
        : base(message, innerException)
    {
        Code = MermaidDiagnosticCodes.SyntaxError;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MermaidSyntaxException"/> class describing an error at a
    ///     location.
    /// </summary>
    /// <param name="code">The stable <c>mermaid-*</c> code of the error. Must not be null or empty.</param>
    /// <param name="description">The description of the error, without the location and the code.</param>
    /// <param name="location">The location of the error in the original text.</param>
    /// <param name="hint">The suggested fix, or <see langword="null"/>.</param>
    /// <param name="expected">What the grammar would have accepted at the location, or <see langword="null"/> for nothing.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="code"/> is null or empty.</exception>
    public MermaidSyntaxException(string code, string description, MermaidLocation location, string? hint = null, IReadOnlyList<string>? expected = null)
        : base(FormatMessage(code, description, location))
    {
        ArgumentException.ThrowIfNullOrEmpty(code);
        Code = code;
        Location = location;
        Hint = hint;
        Expected = expected is null ? [] : [.. expected];
    }

    /// <summary>
    ///     Gets the stable <c>mermaid-*</c> code of the error.
    /// </summary>
    public string Code { get; }

    /// <summary>
    ///     Gets the location of the error in the original text; <c>default</c> (line zero) when unknown.
    /// </summary>
    public MermaidLocation Location { get; }

    /// <summary>
    ///     Gets the suggested fix, or <see langword="null"/> when there is none.
    /// </summary>
    public string? Hint { get; }

    /// <summary>
    ///     Gets what the grammar would have accepted at <see cref="Location"/> (token descriptions such as
    ///     <c>'--&gt;'</c> or <c>a new line</c>); empty when the error is not about an unexpected token.
    /// </summary>
    public IReadOnlyList<string> Expected { get; } = [];

    /// <summary>
    ///     Formats the message of a located error.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <param name="description">The description.</param>
    /// <param name="location">The location.</param>
    /// <returns><c>{location}: {code}: {description}</c>, without the location when it is unknown.</returns>
    internal static string FormatMessage(string code, string description, MermaidLocation location) =>
        location.IsKnown ? $"{location}: {code}: {description}" : $"{code}: {description}";
}
