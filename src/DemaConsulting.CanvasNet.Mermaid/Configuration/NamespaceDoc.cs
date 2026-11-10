namespace DemaConsulting.CanvasNet.Mermaid.Configuration;

/// <summary>
///     The <c>Configuration</c> namespace turns the site configuration, the frontmatter and the directives of a diagram
///     into the effective configuration of a call, and the theme into the paint and fonts every element class is drawn
///     with, exactly as Mermaid computes them.
/// </summary>
/// <remarks>
///     <para>
///         <c>MermaidConfigResolver</c> ports Mermaid's configuration code (<c>initialize</c>, <c>setSiteConfig</c>,
///         <c>detectInit</c>, <c>addDirective</c>, <c>updateCurrentConfig</c>, <c>assignWithDepth</c>, the sanitizers)
///         with the global, flowchart, sequence, class, state, entity relationship and pie defaults, and reports secure, unknown
///         and invalid keys at their locations; <c>ThemeResolver</c> ports the five built-in themes (<c>default</c>,
///         <c>base</c>, <c>dark</c>, <c>forest</c>, <c>neutral</c>) and builds the style rules of the flowchart, the
///         sequence diagram, the class diagram, the state diagram, the entity relationship diagram and the pie chart
///         (<c>FlowchartStyles</c>, <c>SequenceStyles</c>, <c>ClassStyles</c>, <c>StateStyles</c>, <c>ErStyles</c>,
///         <c>PieStyles</c>);
///         <c>ColorMath</c> ports the subset of the khroma color library the themes use, with its exact rounding and output
///         formats;
///         <c>StyleDeclarations</c> parses the CSS subset of <c>style</c>, <c>classDef</c>, <c>linkStyle</c> and
///         <c>themeCSS</c>.
///     </para>
///     <para>
///         No regular expression runs on configuration text, and every loop that a value controls is bounded. The
///         namespace is internal to the package; every type is stateless, immutable, or used by one call on one thread.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
