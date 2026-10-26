package CanvasNet {
    part def ResultWriter {
        doc /* The machine contract of the tool: writes one deterministic JSON document per invocation. A success document is a single-line JSON object whose first keys are schemaVersion, ok (true), and command, followed by the command's own fields; a failure document has ok false, the command (or null), and an error object with the stable error code, the message, and the hint when there is one, and no exit-code field. The schema version is 1. The output is identical for identical input: keys are written in the order the code writes them, numbers are formatted with the invariant culture, and every non-ASCII character is escaped, so the text is ASCII-only. A failure document can be created from an exception: a ToolException contributes its code and hint, an ArgumentException is usage-error, and any other exception is operation-failed. A ToolException can also carry result fields, which are appended after the error object so that a failure that still produced a result reports it in the same document. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Tool/ResultWriter.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tool.Tests/ResultWriterTests.cs */
        comment designRef /* Design: docs/design/canvas-net-tool/result-writer.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-tool/result-writer.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-tool/result-writer.yaml */
    }
}
