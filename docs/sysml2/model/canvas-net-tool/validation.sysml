package CanvasNet {
    part def Validation {
        doc /* The self-validation framework behind --validate. It runs the contractual platform-evidence self-tests (CanvasNetTool_VersionDisplay and CanvasNetTool_HelpDisplay, each running the tool in-process into a log file), prints a header table and a summary, and optionally writes the results as a TRX file (.trx) or a JUnit XML file (.xml) using DemaConsulting.TestResults; an unsupported results extension is reported as an error. Further self-tests are added with the commands they exercise. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Tool/Validation.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tool.Tests/ValidationTests.cs */
        comment designRef /* Design: docs/design/canvas-net-tool/validation.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-tool/validation.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-tool/validation.yaml */
    }
}
