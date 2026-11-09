namespace DemaConsulting.CanvasNet.Mermaid.Syntax;

/// <summary>
///     The <c>Syntax</c> namespace is the first stage of the pipeline: it turns diagram text into what the diagram parsers
///     read, exactly as Mermaid does before it parses a diagram, with every position mapped back to the original text.
/// </summary>
/// <remarks>
///     <para>
///         <c>Preprocessor</c> ports Mermaid's preprocessing (line endings, HTML attribute quotes, the YAML frontmatter,
///         <c>%%{init: ...}%%</c> directives, comment lines, entity encoding and <c>maxTextSize</c>) and keeps a
///         <c>MappedText</c> location map; <c>DiagramDetector</c> ports <c>detectType</c> and knows every Mermaid diagram
///         type; <c>YamlSubsetParser</c> ports the js-yaml loader for the <c>JSON_SCHEMA</c> (refusing anchors, aliases and
///         tags); <c>SourceScanner</c> is the jison-style lexer infrastructure (rules in order, start conditions, located
///         errors with the expected tokens) that the diagram parsers build on; <c>HtmlSanitizer</c> ports the DOMPurify
///         clean-up of Mermaid's <c>sanitizeText</c> for label HTML, decoding the named character references of the
///         <c>HtmlEntities</c> table.
///     </para>
///     <para>
///         No regular expression runs on diagram text: every Mermaid expression is a hand-written scanner with the same
///         matches, so the preprocessing, the YAML reader and the lexing are linear in the text; the clean-up of a label
///         is bounded by <c>MermaidLimits.MaxLabelLength</c>. The namespace is internal to the package; every type is
///         immutable or used by one call on one thread.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
