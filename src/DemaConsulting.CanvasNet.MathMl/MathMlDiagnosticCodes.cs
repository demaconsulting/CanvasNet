namespace DemaConsulting.CanvasNet.MathMl;

/// <summary>
///     The published, stable <c>mathml-*</c> codes of the warnings, unsupported-feature reports and exceptions of this
///     package.
/// </summary>
/// <remarks>
///     <para>
///         Every report carries one of these codes (<see cref="MathMlDiagnostic.Code"/>,
///         <see cref="MathMlUnsupportedFeature.Feature"/>, <see cref="MathMlSyntaxException.Code"/>,
///         <see cref="MathMlRenderException.Code"/>), so a tool or an agent can react to a problem without parsing the
///         message, which is human-readable and may change. A code is never renamed or reused for another meaning.
///     </para>
///     <para>
///         The class only holds constants and is safe for concurrent use.
///     </para>
/// </remarks>
public static class MathMlDiagnosticCodes
{
    /// <summary>
    ///     The text is not valid UTF-8, not well-formed XML, declares a document type or an encoding other than UTF-8,
    ///     uses a named character reference that is not in the W3C HTML+MathML set, or its root element is not
    ///     <c>math</c> (<see cref="MathMlSyntaxException"/>).
    /// </summary>
    public const string SyntaxError = "mathml-syntax-error";

    /// <summary>
    ///     A resource limit of <see cref="MathMlLimits"/> is exceeded: while the text is read
    ///     (<see cref="MathMlSyntaxException"/>) or while the equation is laid out or painted
    ///     (<see cref="MathMlRenderException"/>); the message names the property.
    /// </summary>
    public const string LimitExceeded = "mathml-limit-exceeded";

    /// <summary>
    ///     The layout or the painting failed unexpectedly, for example because the math data of the font is malformed
    ///     (<see cref="MathMlRenderException"/>); the inner exception holds the cause.
    /// </summary>
    public const string RenderFailed = "mathml-render-failed";

    /// <summary>
    ///     An element is outside MathML Core (an element of MathML 4 Full that MathML Core does not define, such as
    ///     <c>mfenced</c>, <c>menclose</c> or <c>mlabeledtr</c>, an element of another namespace, or an element inside a
    ///     token), or text stands directly inside a layout element: the element is laid out as an <c>mrow</c> of its
    ///     children, as MathML Core lays out an unknown element, an element of another namespace or inside a token is
    ///     skipped with its subtree, and stray text is drawn like an <c>mtext</c> (<see cref="MathMlUnsupportedFeature"/>).
    ///     Every element of MathML Core is drawn.
    /// </summary>
    public const string ElementUnsupported = "mathml-element-unsupported";

    /// <summary>
    ///     An attribute has no effect in this version, such as a legacy MathML 3 attribute, a link (<c>href</c>,
    ///     <c>xlink:*</c>; never followed) or right-to-left math; it is ignored (<see cref="MathMlUnsupportedFeature"/>).
    /// </summary>
    public const string AttributeUnsupported = "mathml-attribute-unsupported";

    /// <summary>
    ///     An attribute value is not valid for its attribute (for example a length without a unit); the attribute is
    ///     ignored, as MathML Core ignores it (<see cref="MathMlDiagnostic"/>).
    /// </summary>
    public const string AttributeInvalid = "mathml-attribute-invalid";

    /// <summary>
    ///     A declaration of an inline <c>style</c> attribute is outside the supported subset (<c>color</c>,
    ///     <c>background-color</c>, <c>font-size</c>, <c>math-style</c>, <c>math-depth</c>, <c>font-style</c> and
    ///     <c>display: none</c>); it is ignored (<see cref="MathMlUnsupportedFeature"/>).
    /// </summary>
    public const string CssUnsupported = "mathml-css-unsupported";

    /// <summary>
    ///     A character of a token has no glyph in the bundled math font nor in the bundled fallback fonts; it is drawn
    ///     as the missing-glyph box (<see cref="MathMlDiagnostic"/>).
    /// </summary>
    public const string TextMissingGlyph = "mathml-text-missing-glyph";

    /// <summary>
    ///     A stretchy operator must grow to cover its neighbors, but neither the bundled math font, nor the Unicode-based
    ///     glyph assemblies of MathML Core, nor a rule can stretch its character; it is drawn at its normal size
    ///     (<see cref="MathMlDiagnostic"/>).
    /// </summary>
    public const string StretchUnavailable = "mathml-stretch-unavailable";

    /// <summary>
    ///     An element does not have the in-flow children that MathML Core requires (an <c>mfrac</c> or an <c>mroot</c>
    ///     without exactly two); it is laid out as an <c>mrow</c> of its children, as MathML Core lays out invalid markup
    ///     (<see cref="MathMlDiagnostic"/>).
    /// </summary>
    public const string ChildCount = "mathml-child-count";

    /// <summary>
    ///     More reports than <see cref="MathMlLimits.MaxDiagnostics"/> allows were produced by one call; the rest were
    ///     dropped (<see cref="MathMlDiagnostic"/>).
    /// </summary>
    public const string DiagnosticsSuppressed = "mathml-diagnostics-suppressed";
}
