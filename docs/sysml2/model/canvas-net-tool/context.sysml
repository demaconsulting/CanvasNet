package CanvasNetTool_Context {
    private import SoftwareItemMetadata::*;

    part def Context {
        doc /* Command-line parser and I/O owner for one tool invocation. It parses the global options (version, help, silent, validate, json, results with its legacy alias, depth, and log) in any position, captures the first token that is neither an option nor an option value as the command name and every other token after it as the command arguments (unknown options before the command are rejected, after it they are passed through to the command), owns the standard-output and standard-error channels and the optional log file, and derives the exit code (0, or 1 once an error has been written). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet.Tool/Context.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.Tool.Tests/ContextTests.cs");
            designFiles = ("docs/design/canvas-net-tool/context.md");
            verificationFiles = ("docs/verification/canvas-net-tool/context.md");
            requirementFiles = ("docs/reqstream/canvas-net-tool/context.yaml");
        }
    }
}
