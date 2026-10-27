package CanvasNet {
    part def Validation {
        doc /* The self-validation framework behind --validate. It runs the contractual platform-evidence self-tests (CanvasNetTool_VersionDisplay and CanvasNetTool_HelpDisplay, each running the tool in-process into a log file), the formats self-test (CanvasNetTool_Formats, which runs the formats command with JSON output in-process and checks the document), the imaging self-tests (render, convert, info, the transformation commands) and the comparison self-tests (CanvasNetTool_Compare and CanvasNetTool_Montage) and the photo self-test (CanvasNetTool_Adjustments, which runs the tone operations, deskew, oblique rotation, trim, grid, and the inspect commands on a generated sketch; fourteen self-tests in all), prints a header table and a summary, and optionally writes the results as a TRX file (.trx) or a JUnit XML file (.xml) using DemaConsulting.TestResults; an unsupported results extension is reported as an error. Further self-tests are added with the commands they exercise. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Tool/Validation.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tool.Tests/ValidationTests.cs */
        comment designRef /* Design: docs/design/canvas-net-tool/validation.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-tool/validation.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-tool/validation.yaml */
    }
}
