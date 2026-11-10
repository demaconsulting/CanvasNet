package CanvasNet {
    part def DiagramDetector {
        doc /* The internal static DiagramDetector of the Mermaid Syntax subsystem with DetectorSettings, DetectedDiagram and DiagramSupport. Detect ports Mermaid's detectType: it removes a further frontmatter block and the directives, replaces every comment with a line feed, finds the keyword and its location in the original text, and tries all 40 detectors of the pinned Mermaid release in Mermaid's registration order (error, the --- pseudo-diagram, the large features flowchart-elk, mindmap and architecture, then the rest up to cynefin), the first that matches winning. Each detector is a port of its expression with its quirks kept (C4 keywords anywhere, keyword prefixes, word boundaries, case-insensitive keywords), and the flowchart, class and state variants read the defaultRenderer settings. The result carries Mermaid's detector id, the public type name (class, state and flowchart for their variants), the keyword, the location, and the support: flowchart, sequence, class (with the legacy class id), state (with the legacy state id), and er are supported, the types of the following phases are planned, and the rest is not planned; UnsupportedHint gives the hint of the report of a type that is not drawn. A text that starts with --- is a mermaid-syntax-error with Mermaid's message; a text no detector matches is a located mermaid-unknown-diagram whose hint names the nearest known keyword by edit distance. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Mermaid/Syntax/DiagramDetector.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Mermaid.Tests/Syntax/DiagramDetectorTests.cs */
        comment designRef /* Design: docs/design/canvas-net-mermaid/syntax/diagram-detector.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-mermaid/syntax/diagram-detector.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-mermaid/syntax/diagram-detector.yaml */
    }
}
