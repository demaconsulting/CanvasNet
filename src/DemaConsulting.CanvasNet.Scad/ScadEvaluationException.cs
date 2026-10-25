namespace DemaConsulting.CanvasNet.Scad;

/// <summary>
///     The exception thrown when evaluating an OpenSCAD-language program fails: a failed <c>assert</c>, an
///     exceeded resource limit, an include cycle or sandbox violation, or similar.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="Location"/> identifies the construct that failed. The exception derives from
///         <see cref="InvalidOperationException"/> (the program cannot be evaluated in its current state) because
///         <see cref="InvalidDataException"/> is sealed. Problems that merely deserve a warning never throw; see
///         <see cref="ScadDiagnostic"/>. Syntax errors keep their own type, <see cref="ScadSyntaxException"/>, and
///         cancellation is reported with <see cref="OperationCanceledException"/>.
///     </para>
/// </remarks>
public sealed class ScadEvaluationException : InvalidOperationException
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ScadEvaluationException"/> class with an unknown location.
    /// </summary>
    public ScadEvaluationException()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScadEvaluationException"/> class with a message and an
    ///     unknown location.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ScadEvaluationException(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScadEvaluationException"/> class with a message, an inner
    ///     exception and an unknown location.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public ScadEvaluationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScadEvaluationException"/> class describing an error at a
    ///     known source location.
    /// </summary>
    /// <param name="description">The description of the error, without the location prefix.</param>
    /// <param name="location">The location of the error.</param>
    public ScadEvaluationException(string description, ScadSourceLocation location)
        : base(FormatMessage(description, location))
    {
        Location = location;
    }

    /// <summary>
    ///     Gets the location of the failure; <c>default</c> (line zero) when the location is unknown.
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
