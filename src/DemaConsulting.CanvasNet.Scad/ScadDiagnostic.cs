namespace DemaConsulting.CanvasNet.Scad;

/// <summary>
///     A warning produced while evaluating an OpenSCAD-language program: something questionable that does not
///     stop evaluation, such as the use of an unknown name.
/// </summary>
/// <remarks>
///     <para>
///         The message carries no severity prefix; presentation (for example a <c>WARNING:</c> prefix) is left to the
///         consumer. A diagnostic is a warning by definition: an error stops evaluation and is reported with an
///         exception (see <see cref="ScadEvaluationException"/>), and a feature that is recognized but not supported
///         is reported with <see cref="ScadUnsupportedFeature"/>, so there is no severity to read.
///     </para>
///     <para>
///         Test <see cref="Code"/>, never the message, to recognize a kind of warning: the message is free text meant
///         for people. The record is immutable and safe for concurrent use.
///     </para>
/// </remarks>
/// <param name="Message">The description of the problem, without the location.</param>
/// <param name="Location">The location in the source that the warning is about; <c>default</c> when unknown.</param>
public sealed record ScadDiagnostic(string Message, ScadSourceLocation Location)
{
    /// <summary>
    ///     Gets the stable machine-readable code of the kind of warning, one of the <see cref="ScadDiagnosticCodes"/>
    ///     constants; <see cref="ScadDiagnosticCodes.Warning"/> (<c>scad-warning</c>) for a warning that has no more
    ///     specific code.
    /// </summary>
    /// <remarks>
    ///     Test the code, never the message. For compatibility the message of a
    ///     <see cref="ScadDiagnosticCodes.AnchorInvalid"/> warning also starts with <c>scad-anchor-invalid:</c>; no
    ///     other message starts with its code. Codes are lower-case kebab-case, start with <c>scad-</c>, and are never
    ///     renamed once published; new codes may be added.
    /// </remarks>
    public string Code { get; init; } = ScadDiagnosticCodes.Warning;

    /// <summary>
    ///     Formats the diagnostic for display.
    /// </summary>
    /// <returns><c>{location}: {message}</c>, with the location rendered using the invariant culture.</returns>
    public override string ToString() => $"{Location}: {Message}";
}

/// <summary>
///     The stable machine-readable codes of the warnings (<see cref="ScadDiagnostic.Code"/>) and failures
///     (<see cref="ScadSyntaxException.Code"/>, <see cref="ScadEvaluationException.Code"/>) of the OpenSCAD-language
///     engine.
/// </summary>
/// <remarks>
///     A caller that reacts to one kind of warning compares <see cref="ScadDiagnostic.Code"/> with one of these
///     constants instead of matching message text. A warning that has no more specific category carries
///     <see cref="Warning"/>, so the property is always set. The values are part of the public contract and are never
///     renamed; later versions may add codes.
/// </remarks>
public static class ScadDiagnosticCodes
{
    /// <summary>The code of a warning that has no more specific code (<c>scad-warning</c>).</summary>
    public const string Warning = "scad-warning";

    /// <summary>
    ///     An anchor declaration (<c>echo("anchor", "name", [x, y, z])</c>) that cannot be read or located, so the anchor
    ///     is skipped (<c>scad-anchor-invalid</c>).
    /// </summary>
    public const string AnchorInvalid = "scad-anchor-invalid";

    /// <summary>An <c>include</c> or <c>use</c> file that could not be opened (<c>scad-include-not-found</c>).</summary>
    public const string IncludeNotFound = "scad-include-not-found";

    /// <summary>A variable that is not defined and is treated as <c>undef</c> (<c>scad-unknown-variable</c>).</summary>
    public const string UnknownVariable = "scad-unknown-variable";

    /// <summary>A function that is not defined; the call yields <c>undef</c> (<c>scad-unknown-function</c>).</summary>
    public const string UnknownFunction = "scad-unknown-function";

    /// <summary>A module that is not defined; the instantiation is ignored (<c>scad-unknown-module</c>).</summary>
    public const string UnknownModule = "scad-unknown-module";

    /// <summary>A named or extra argument the callee does not have, ignored (<c>scad-unknown-argument</c>).</summary>
    public const string UnknownArgument = "scad-unknown-argument";

    /// <summary>An argument given more than once; the last value is used (<c>scad-duplicate-argument</c>).</summary>
    public const string DuplicateArgument = "scad-duplicate-argument";

    /// <summary>A call with a wrong number of arguments (<c>scad-argument-count</c>).</summary>
    public const string ArgumentCount = "scad-argument-count";

    /// <summary>
    ///     An argument value that is not valid for a built-in; the instance or the value is ignored
    ///     (<c>scad-invalid-argument</c>).
    /// </summary>
    public const string InvalidArgument = "scad-invalid-argument";

    /// <summary>
    ///     A variable, function, or module defined more than once in a scope; the last definition is used
    ///     (<c>scad-duplicate-definition</c>).
    /// </summary>
    public const string DuplicateDefinition = "scad-duplicate-definition";

    /// <summary>An operator or construct applied to values of the wrong type (<c>scad-type-mismatch</c>).</summary>
    public const string TypeMismatch = "scad-type-mismatch";

    /// <summary>A variable that refers to itself while it is evaluated (<c>scad-circular-reference</c>).</summary>
    public const string CircularReference = "scad-circular-reference";

    /// <summary>A viewport variable (such as <c>$vpr</c>) whose value cannot be used (<c>scad-viewport-variable</c>).</summary>
    public const string ViewportVariable = "scad-viewport-variable";

    /// <summary>
    ///     The notice that further warnings were dropped because the diagnostics cap was reached
    ///     (<c>scad-diagnostics-suppressed</c>).
    /// </summary>
    public const string DiagnosticsSuppressed = "scad-diagnostics-suppressed";

    /// <summary>The code of <see cref="ScadSyntaxException"/> (<c>scad-syntax-error</c>).</summary>
    public const string SyntaxError = "scad-syntax-error";

    /// <summary>The code of <see cref="ScadEvaluationException"/> (<c>scad-evaluation-error</c>).</summary>
    public const string EvaluationError = "scad-evaluation-error";
}
