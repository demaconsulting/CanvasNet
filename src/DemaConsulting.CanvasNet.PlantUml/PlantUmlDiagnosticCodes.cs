namespace DemaConsulting.CanvasNet.PlantUml;

/// <summary>
///     The published, stable <c>plantuml-*</c> codes of the warnings, unsupported-feature reports and exceptions of this
///     package.
/// </summary>
/// <remarks>
///     <para>
///         Every report carries one of these codes (<see cref="PlantUmlDiagnostic.Code"/>,
///         <see cref="PlantUmlUnsupportedFeature.Feature"/>, <see cref="PlantUmlSyntaxException.Code"/>,
///         <see cref="PlantUmlRenderException.Code"/>), so a tool or an agent can react to a problem without parsing the
///         message, which is human-readable and may change. A code is never renamed or reused for another meaning.
///     </para>
///     <para>
///         The class only holds constants and is safe for concurrent use.
///     </para>
/// </remarks>
public static class PlantUmlDiagnosticCodes
{
    /// <summary>
    ///     The text holds no diagram, is not valid UTF-8, or a statement does not follow the syntax of its diagram type
    ///     (<see cref="PlantUmlSyntaxException"/>).
    /// </summary>
    public const string SyntaxError = "plantuml-syntax-error";

    /// <summary>
    ///     The text starts a diagram with an <c>@start</c> tag that PlantUML does not define
    ///     (<see cref="PlantUmlSyntaxException"/>); the hint names the nearest known tag.
    /// </summary>
    public const string UnknownDiagram = "plantuml-unknown-diagram";

    /// <summary>
    ///     A file that an <c>!include</c> directive names cannot be read (<see cref="PlantUmlSyntaxException"/>).
    /// </summary>
    public const string IncludeFailed = "plantuml-include-failed";

    /// <summary>
    ///     A resource limit of <see cref="PlantUmlLimits"/> is exceeded: while the text is read
    ///     (<see cref="PlantUmlSyntaxException"/>) or while the diagram is laid out or painted
    ///     (<see cref="PlantUmlRenderException"/>); the message names the property.
    /// </summary>
    public const string LimitExceeded = "plantuml-limit-exceeded";

    /// <summary>
    ///     The layout failed unexpectedly on a diagram that was read successfully (<see cref="PlantUmlRenderException"/>);
    ///     the inner exception is the defect.
    /// </summary>
    public const string RenderFailed = "plantuml-render-failed";

    /// <summary>
    ///     The diagram type is a known PlantUML type that this version does not draw (an unsupported feature: the
    ///     picture is empty, and strict mode throws <see cref="PlantUmlUnsupportedFeatureException"/>).
    /// </summary>
    public const string DiagramUnsupported = "plantuml-diagram-unsupported";

    /// <summary>
    ///     The text holds more than one <c>@start</c> ... <c>@end</c> block; only the first one is drawn (an unsupported
    ///     feature).
    /// </summary>
    public const string MultipleDiagrams = "plantuml-multiple-diagrams";

    /// <summary>
    ///     A preprocessor directive outside the supported subset (<c>!if</c>, <c>!function</c>, a <c>!define</c> with
    ///     parameters, ...) is dropped (an unsupported feature).
    /// </summary>
    public const string PreprocessorUnsupported = "plantuml-preprocessor-unsupported";

    /// <summary>
    ///     An include that is never followed is dropped: a URL, the standard library, <c>!includesub</c> or
    ///     <c>!includeurl</c> (an unsupported feature; the library never uses the network).
    /// </summary>
    public const string IncludeUnsupported = "plantuml-include-unsupported";

    /// <summary>
    ///     A <c>!theme</c> directive is ignored and the default look is drawn (an unsupported feature).
    /// </summary>
    public const string ThemeUnsupported = "plantuml-theme-unsupported";

    /// <summary>
    ///     A <c>&lt;style&gt;</c> block is ignored (an unsupported feature).
    /// </summary>
    public const string StyleUnsupported = "plantuml-style-unsupported";

    /// <summary>
    ///     A statement that PlantUML defines is recognized but not drawn (an unsupported feature), for example
    ///     <c>ref over</c> or <c>newpage</c> in a sequence diagram.
    /// </summary>
    public const string StatementUnsupported = "plantuml-statement-unsupported";

    /// <summary>
    ///     Creole markup outside the supported subset (tables, lists, images, sprites, icons) is drawn as plain text (an
    ///     unsupported feature).
    /// </summary>
    public const string MarkupUnsupported = "plantuml-markup-unsupported";

    /// <summary>
    ///     A <c>skinparam</c> outside the supported subset, or with a value that cannot be read, is ignored (a warning).
    /// </summary>
    public const string SkinParamIgnored = "plantuml-skinparam-ignored";

    /// <summary>
    ///     A <c>!pragma</c> directive has no effect on the drawing (a warning).
    /// </summary>
    public const string PragmaIgnored = "plantuml-pragma-ignored";

    /// <summary>
    ///     A character has no glyph in the bundled face of its text and is drawn with a bundled fallback font, or with
    ///     the missing-glyph box (a warning).
    /// </summary>
    public const string TextMissingGlyph = "plantuml-text-missing-glyph";

    /// <summary>
    ///     More reports than <see cref="PlantUmlLimits.MaxDiagnostics"/> were produced; the rest were dropped (a warning).
    /// </summary>
    public const string DiagnosticsSuppressed = "plantuml-diagnostics-suppressed";
}
