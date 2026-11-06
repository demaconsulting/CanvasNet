package CanvasNet {
    part def ViewerException {
        doc /* The exception with which every expected failure of the Viewer library is reported: a file that is missing, unsupported, corrupt, or too large, a page that does not exist, an output that cannot be written, and inputs that cannot be compared. It is an InvalidOperationException that carries a stable error code, a message, an optional hint, and an optional inner exception, and it is not sealed so that the canvasnet tool derives its ToolException from it. The ErrorCodes class holds the stable strings (usage-error, operation-failed, input-not-found, input-unreadable, unsupported-format, corrupt-input, unsupported-feature, page-out-of-range, size-limit, same-input-output, output-exists, output-write-failed, scad-error, vega-lite-error, budget-exceeded, size-mismatch, and threshold-failed), which are fixed between releases. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Viewer/ViewerException.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Viewer.Tests/ViewerExceptionTests.cs */
        comment designRef /* Design: docs/design/canvas-net-viewer/viewer-exception.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-viewer/viewer-exception.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-viewer/viewer-exception.yaml */
    }
}
