package CanvasNet {
    part def HtmlSanitizer {
        doc /* The internal static HtmlSanitizer of the Mermaid Syntax subsystem (HtmlSanitizer.cs) and the grouped HtmlEntities table of named character references (HtmlEntities.cs). Sanitize reproduces the results of Mermaid's sanitizeText, which runs DOMPurify, for the inline HTML of labels: a text without a less-than sign is returned unchanged; otherwise the text is read in one pass as an HTML fragment, elements and attributes outside DOMPurify's default allow lists are removed (style elements forbidden as Mermaid configures it, data- and aria- attributes allowed), the content of script, style, template, SVG, MathML and the other forbidden-content elements is removed with them, attribute values with an unsafe URI scheme are dropped, Mermaid's hook keeps the target of a link last and adds rel noopener for _blank, and the result is serialized as a browser serializes HTML (lower-case names, double-quoted attributes, escapes, void elements, every open element closed, formatting elements closed out of order reopened). DecodeReferences decodes numeric references and the 252 HTML 4 named references plus apos, equals, num, semi and colon; other names stay as written. Known simplifications: a plain open-element stack (no paragraph or table fixing) and SVG and MathML removed. The unit is original work, stateless, and uses no recursion. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Mermaid/Syntax/HtmlSanitizer.cs, src/DemaConsulting.CanvasNet.Mermaid/Syntax/HtmlEntities.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Mermaid.Tests/Syntax/HtmlSanitizerTests.cs */
        comment designRef /* Design: docs/design/canvas-net-mermaid/syntax/html-sanitizer.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-mermaid/syntax/html-sanitizer.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-mermaid/syntax/html-sanitizer.yaml */
    }
}
