package CanvasNet {
    part def PathHelpers {
        doc /* Static helper that combines a base path and a relative path while verifying, by resolving both to absolute form, that the result does not escape the base directory (path-traversal and rooted-path protection). It performs no file-system I/O. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Tool/PathHelpers.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tool.Tests/PathHelpersTests.cs */
        comment designRef /* Design: docs/design/canvas-net-tool/path-helpers.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-tool/path-helpers.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-tool/path-helpers.yaml */
    }
}
