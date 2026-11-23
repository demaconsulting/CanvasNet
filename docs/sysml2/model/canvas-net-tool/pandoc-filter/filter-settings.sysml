package CanvasNetTool_FilterSettings {
    private import SoftwareItemMetadata::*;

    part def FilterSettings {
        doc /* Holds the inputs of one run of the Pandoc filter: the output format, the output file, embedding, the resource path, and the location of the Lua shim given by the command, the working directory, and a reader of the environment variables. From them it derives, on first use, the cache folder (generated/canvasnet in the folder above the shim's folder); the tool identity of every cache key (the version, the label default or CANVASNET_TOOL=command with the command split at white space and grouped by double quotes, and the SHA-1 of the DLLs beside a local build); and the file-access arguments of each picture in the supporting record FileAccessArguments, read from CANVASNET_FILE_ACCESS (roots, no-symlinks, roots,no-symlinks, or any) and CANVASNET_ROOTS (folders that imply roots), adding the working directory and an outside source folder for roots without folders, and the policy the dependency walk reads a picture's files through: none in the default mode, the tool's own policy built from the same arguments, or one that denies every file when the tool would refuse them. It gives a picture its src: relative to the output file for an HTML-like writer without embedding, else the absolute path with forward slashes. It resolves the pictures drawn at once from --jobs, else CANVASNET_JOBS (1 to 64), else the processors up to 4. A bad variable is the error pandoc-filter-error with the message of the Lua filter. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet.Tool/PandocFilter/FilterSettings.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.Tool.Tests/PandocFilter/FilterSettingsTests.cs");
            designFiles = ("docs/design/canvas-net-tool/pandoc-filter/filter-settings.md");
            verificationFiles = ("docs/verification/canvas-net-tool/pandoc-filter/filter-settings.md");
            requirementFiles = ("docs/reqstream/canvas-net-tool/pandoc-filter/filter-settings.yaml");
        }
    }
}
