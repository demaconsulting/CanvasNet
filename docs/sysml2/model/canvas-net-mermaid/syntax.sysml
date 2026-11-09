package CanvasNet {
    part def MermaidSyntax {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.Mermaid.Syntax namespace that is the first stage of the Mermaid pipeline: it turns a diagram text into what the diagram parsers read, exactly as the pinned Mermaid release does before it parses a diagram, with every position mapped back to the line and column of the original text. The Preprocessor unit ports Mermaid's preparation (line endings, HTML attribute quotes, the YAML frontmatter with its title, display mode and configuration, the init and wrap directives, the comment lines and the entity encoding), keeps the MappedText location map, and enforces the text size limits (the code at most MaxTextSize, the original text at most four times it). The DiagramDetector unit ports detectType with all 40 of Mermaid's detectors in Mermaid's registration order, maps detector ids to public type names, tells whether a type is supported, planned or not planned, and refuses an unknown text with a located error naming the nearest keyword. The YamlSubsetParser unit ports the js-yaml loader that Mermaid bundles with the JSON schema, refuses anchors, aliases, tags, explicit keys and directives, and bounds nesting by MaxYamlDepth. The SourceScanner unit (with ScanPatterns and TokenCursor) is the jison-style lexer infrastructure that the diagram parsers build on: first matching rule, start-condition stack, the end-of-input rule run once, and located errors listing the expected tokens. The HtmlSanitizer unit (with the HtmlEntities table) reproduces the results of DOMPurify, which Mermaid's sanitizeText runs, for the inline HTML of labels. No regular expression runs on diagram text; every type is immutable or used by one call on one thread. The subsystem depends only on the public location, limit, exception and diagnostic-code types of the root namespace; it is called by MermaidDocument and by the Configuration, Flowchart and Rendering subsystems. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Mermaid.Tests/Syntax/SyntaxTests.cs */
        comment designRef /* Design: docs/design/canvas-net-mermaid/syntax.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-mermaid/syntax.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-mermaid/syntax.yaml */

        part preprocessor : Preprocessor;
        part diagramDetector : DiagramDetector;
        part yamlSubsetParser : YamlSubsetParser;
        part sourceScanner : SourceScanner;
        part htmlSanitizer : HtmlSanitizer;
    }
}
