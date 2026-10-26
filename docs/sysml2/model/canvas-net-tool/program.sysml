package CanvasNet {
    part def Program {
        doc /* The tool's entry point and top-level dispatcher. Main delegates to Execute, which creates the Context, runs the program logic, and applies the error policy: an ArgumentException, an InvalidOperationException (including the ToolException supporting type, which carries a stable error code, an optional hint, and the command name), is an expected failure reported as a concise message on standard error with exit code 1 (and, when the command line contains --json and not --silent, also as a JSON failure document on standard output); any other exception is reported as an unexpected error and re-thrown. Run dispatches in priority order - version query, help, self-validation, the selected command through the command registry - and prints the banner only for help, self-validation, and an empty command line. The ToolException and ErrorCodes supporting types are documented inline in the Program design. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Tool/Program.cs, src/DemaConsulting.CanvasNet.Tool/ToolException.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tool.Tests/ProgramTests.cs */
        comment designRef /* Design: docs/design/canvas-net-tool/program.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-tool/program.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-tool/program.yaml */
    }
}
