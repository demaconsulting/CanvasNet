package CanvasNet {
    part def Preprocessor {
        doc /* The internal static Preprocessor of the Mermaid Syntax subsystem and its records PreprocessedSource, FrontmatterData, DirectiveText, FrontmatterMatch and DirectiveMatch, with the location map SourceLines and MappedText (Preprocessor.MappedText.cs). Run prepares a diagram text exactly as Mermaid's preprocessDiagram and the encodeEntities of Diagram.fromText do: CRLF and CR become LF and the double quotes of HTML attributes become single quotes (cleanupText); a YAML frontmatter block is matched where Mermaid's frontmatter expression matches (indented, empty and unclosed blocks included), read with YamlSubsetParser and MaxYamlDepth, and only its title and displayMode (as JavaScript strings, dropped when falsy) and config are kept; the directives are found as detectDirective finds them (type, arguments with single quotes replaced, JSON or word, location) and removed; the comment lines are removed; and the parser text is the code with Mermaid's private entity markers and a final line feed. Every transformation builds a MappedText from slices that keep their original offsets and inserted text mapped to what it replaces, so every position has a one-based location in the original text. The original text may be at most four times MermaidLimits.MaxTextSize and the code at most MaxTextSize (an InvalidDataException otherwise); every Mermaid expression is a hand-written scanner with the same matches, linear in the text. Invalid or refused frontmatter YAML is the located MermaidSyntaxException of YamlSubsetParser. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Mermaid/Syntax/Preprocessor.cs, src/DemaConsulting.CanvasNet.Mermaid/Syntax/Preprocessor.MappedText.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Mermaid.Tests/Syntax/PreprocessorTests.cs */
        comment designRef /* Design: docs/design/canvas-net-mermaid/syntax/preprocessor.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-mermaid/syntax/preprocessor.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-mermaid/syntax/preprocessor.yaml */
    }
}
