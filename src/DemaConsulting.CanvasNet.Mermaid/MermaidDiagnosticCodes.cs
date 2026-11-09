namespace DemaConsulting.CanvasNet.Mermaid;

/// <summary>
///     The published, stable <c>mermaid-*</c> codes of the warnings, unsupported-feature reports and exceptions of this
///     package.
/// </summary>
/// <remarks>
///     <para>
///         Every report carries one of these codes (<see cref="MermaidDiagnostic.Code"/>,
///         <see cref="MermaidUnsupportedFeature.Feature"/>, <see cref="MermaidSyntaxException.Code"/>,
///         <see cref="MermaidRenderException.Code"/>), so a tool or an agent can react to a problem without parsing the
///         message, which is human-readable and may change. A code is never renamed or reused for another meaning.
///     </para>
///     <para>
///         The class only holds constants and is safe for concurrent use.
///     </para>
/// </remarks>
public static class MermaidDiagnosticCodes
{
    /// <summary>The text does not follow the grammar of its diagram type (<see cref="MermaidSyntaxException"/>).</summary>
    public const string SyntaxError = "mermaid-syntax-error";

    /// <summary>
    ///     The text does not start with the keyword of any Mermaid diagram type (<see cref="MermaidSyntaxException"/>);
    ///     the hint names the nearest known keyword.
    /// </summary>
    public const string UnknownDiagram = "mermaid-unknown-diagram";

    /// <summary>
    ///     The diagram type is a known Mermaid type that this version does not draw (an unsupported feature: the
    ///     picture is empty, and strict mode throws <see cref="MermaidUnsupportedFeatureException"/>).
    /// </summary>
    public const string DiagramUnsupported = "mermaid-diagram-unsupported";

    /// <summary>The YAML frontmatter or shape data is not valid YAML (<see cref="MermaidSyntaxException"/>).</summary>
    public const string YamlInvalid = "mermaid-yaml-invalid";

    /// <summary>
    ///     The YAML uses a construct that is refused for safety although YAML defines it: an anchor, an alias, a tag, an
    ///     explicit key or a directive (<see cref="MermaidSyntaxException"/>).
    /// </summary>
    public const string YamlUnsupported = "mermaid-yaml-unsupported";

    /// <summary>A resource limit of <see cref="MermaidLimits"/> was exceeded; the message names the limit.</summary>
    public const string LimitExceeded = "mermaid-limit-exceeded";

    /// <summary>The diagram could not be drawn for a reason other than a limit (<see cref="MermaidRenderException"/>).</summary>
    public const string RenderFailed = "mermaid-render-failed";

    /// <summary>Further reports were dropped after <see cref="MermaidLimits.MaxDiagnostics"/> (a warning).</summary>
    public const string DiagnosticsSuppressed = "mermaid-diagnostics-suppressed";

    /// <summary>A directive or the frontmatter tried to change a <c>secure</c> configuration key; the change is ignored (a warning).</summary>
    public const string ConfigSecureKey = "mermaid-config-secure-key";

    /// <summary>A configuration key that Mermaid does not define is ignored (a warning).</summary>
    public const string ConfigUnknownKey = "mermaid-config-unknown-key";

    /// <summary>A configuration value has the wrong type or range; the default is kept (a warning).</summary>
    public const string ConfigInvalid = "mermaid-config-invalid";

    /// <summary>A node uses a shape name that Mermaid does not define; the hint names the nearest shape.</summary>
    public const string ShapeUnknown = "mermaid-shape-unknown";

    /// <summary>
    ///     A sequence participant has a <c>type</c> that Mermaid does not define; it is not drawn, as in Mermaid (a
    ///     warning); the hint names the nearest of the eight types.
    /// </summary>
    public const string ParticipantTypeUnknown = "mermaid-participant-type-unknown";

    /// <summary>A <c>$$...$$</c> math text (KaTeX) in a sequence diagram is drawn as plain text (an unsupported feature).</summary>
    public const string MathUnsupported = "mermaid-math-unsupported";

    /// <summary>An icon (<c>fa:</c>, <c>@{ icon }</c>) is not drawn; the text without the icon is (an unsupported feature).</summary>
    public const string IconUnsupported = "mermaid-icon-unsupported";

    /// <summary>A picture could not be loaded through the image resolver and is not drawn (a warning).</summary>
    public const string ImageUnresolved = "mermaid-image-unresolved";

    /// <summary>A character has no glyph in the bundled fonts and is drawn as a missing-glyph box (a warning).</summary>
    public const string TextMissingGlyph = "mermaid-text-missing-glyph";

    /// <summary>A <c>look</c> other than <c>classic</c> is drawn classic (an unsupported feature).</summary>
    public const string LookUnsupported = "mermaid-look-unsupported";

    /// <summary>A <c>layout</c> other than <c>dagre</c> falls back to dagre (an unsupported feature).</summary>
    public const string LayoutUnsupported = "mermaid-layout-unsupported";

    /// <summary>A style declaration outside the supported subset is ignored (a warning).</summary>
    public const string StyleIgnored = "mermaid-style-ignored";

    /// <summary>A <c>themeCSS</c> rule with an arbitrary selector is not applied (an unsupported feature).</summary>
    public const string ThemeCssUnsupported = "mermaid-theme-css-unsupported";

    /// <summary>An HTML tag outside the supported label subset is dropped and its text kept (a warning).</summary>
    public const string HtmlTagIgnored = "mermaid-html-tag-ignored";
}
