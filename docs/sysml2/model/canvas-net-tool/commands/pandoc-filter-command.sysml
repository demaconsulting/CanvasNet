package CanvasNetTool_PandocFilterCommand {
    private import SoftwareItemMetadata::*;

    part def PandocFilterCommand {
        doc /* The pandoc-filter command: the front end of the PandocFilter subsystem for the Lua shim docs/filters/canvasnet.lua, which sends it the whole Pandoc document once per Pandoc run. It declares its options (protocol, filter-path, format, pandoc-version, resource-path, pandoc-output, embed-resources, jobs, input, output, and write-shim) in one table that also drives the help. With --write-shim it writes the shim embedded in the tool (PandocFilterShim.Write) and reports it as one text line or one JSON document. Otherwise it checks the protocol of the shim before reading anything (PandocFilterShim.CheckProtocol; an unknown protocol is a usage-error whose hint names the --write-shim command for the shim's path or says to update the tool), builds the FilterSettings of the run (the filter path, the working directory, the environment, the format, the output file, the embed flag, and the resource paths, . by default), resolves the jobs (--jobs, else CANVASNET_JOBS, else the processors up to 4), reads the document as raw bytes from standard input (or --input), runs PandocFilterEngine with a ToolPictureDrawer, and writes the result as UTF-8 bytes without a byte order mark to standard output (or --output) only when the run succeeded. On a failure it writes the warnings of the earlier pictures to standard error as [WARNING] canvasnet.lua lines and lets the error escape, which the registry reports as an expected error (pandoc-filter-error, corrupt-input, or the code of the tool) with the JSON failure document under --json. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet.Tool/Commands/PandocFilterCommand.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.Tool.Tests/Commands/PandocFilterCommandTests.cs");
            designFiles = ("docs/design/canvas-net-tool/commands/pandoc-filter-command.md");
            verificationFiles = ("docs/verification/canvas-net-tool/commands/pandoc-filter-command.md");
            requirementFiles = ("docs/reqstream/canvas-net-tool/commands/pandoc-filter-command.yaml");
        }
    }
}
