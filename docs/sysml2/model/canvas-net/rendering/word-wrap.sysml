package CanvasNet_WordWrap {
    private import SoftwareItemMetadata::*;

    part def WordWrap {
        doc /* Static, font- and unit-independent greedy word-wrap packer (single and double precision Pack overloads, with the WordWrapTokenKind enum): packs a paragraph's tokens into lines, keeping consecutive word tokens together as a group that is never split, wrapping only at whitespace, honoring explicit line breaks, placing an oversized group whole on its own line, and letting the caller measure each token given the width already used on the line. Shared by the Pptx and Vsdx text layout engines. Thread-safe: stateless. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet/Rendering/WordWrap.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.Tests/Rendering/WordWrapTests.cs");
            designFiles = ("docs/design/canvas-net/rendering/word-wrap.md");
            verificationFiles = ("docs/verification/canvas-net/rendering/word-wrap.md");
            requirementFiles = ("docs/reqstream/canvas-net/rendering/word-wrap.yaml");
        }
    }
}
