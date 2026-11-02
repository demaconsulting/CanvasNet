package CanvasNet {
    part def WordWrap {
        doc /* Static, font- and unit-independent greedy word-wrap packer (single and double precision Pack overloads, with the WordWrapTokenKind enum): packs a paragraph's tokens into lines, keeping consecutive word tokens together as a group that is never split, wrapping only at whitespace, honoring explicit line breaks, placing an oversized group whole on its own line, and letting the caller measure each token given the width already used on the line. Shared by the Pptx and Vsdx text layout engines. Thread-safe: stateless. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet/Rendering/WordWrap.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/Rendering/WordWrapTests.cs */
        comment designRef /* Design: docs/design/canvas-net/rendering/word-wrap.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/rendering/word-wrap.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/rendering/word-wrap.yaml */
    }
}
