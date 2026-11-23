package CanvasNetTool_PathHelpers {
    private import SoftwareItemMetadata::*;

    part def PathHelpers {
        doc /* Static helper that combines a base path and a relative path while verifying, by resolving both to absolute form, that the result does not escape the base directory (path-traversal and rooted-path protection). It performs no file-system I/O. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet.Tool/PathHelpers.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.Tool.Tests/PathHelpersTests.cs");
            designFiles = ("docs/design/canvas-net-tool/path-helpers.md");
            verificationFiles = ("docs/verification/canvas-net-tool/path-helpers.md");
            requirementFiles = ("docs/reqstream/canvas-net-tool/path-helpers.yaml");
        }
    }
}
