package CanvasNet {
    part def Context {
        doc /* Command-line parser and I/O owner for one tool invocation. It parses the global options (version, help, silent, validate, results with its legacy alias, depth, and log) in any position, captures the first token that is neither an option nor an option value as the command name and every other token after it as the command arguments (unknown options before the command are rejected, after it they are passed through to the command), owns the standard-output and standard-error channels and the optional log file, and derives the exit code (0, or 1 once an error has been written). */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Tool/Context.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tool.Tests/ContextTests.cs */
        comment designRef /* Design: docs/design/canvas-net-tool/context.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-tool/context.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-tool/context.yaml */
    }
}
