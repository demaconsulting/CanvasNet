package CanvasNet_FontResolver {
    private import SoftwareItemMetadata::*;

    part def FontResolver {
        doc /* Resolves a CSS font-family list, a bold flag and an italic flag to a loaded font, deterministically and without ever failing (the bundled Liberation Sans is the last resort). Per family: a generic keyword (or, when requested, a common alias) maps to the bundled Liberation font of its design; otherwise the family is looked up by exact name in the additional directories (scanned once, lazily, with FontDirectoryScanner under FontScanLimits), then, when enabled, in the installed fonts, then among the three bundled family names. Reports, through a failure output, why the fallback was used. Includes the FontResolverOptions, ResolvedFont and FontSource types. Thread-safe; one resolver per render or job. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet/Fonts/FontResolver.cs",
                "src/DemaConsulting.CanvasNet/Fonts/FontResolverOptions.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasNet.Tests/Fonts/FontResolverTests.cs");
            designFiles = ("docs/design/canvas-net/fonts/font-resolver.md");
            verificationFiles = ("docs/verification/canvas-net/fonts/font-resolver.md");
            requirementFiles = ("docs/reqstream/canvas-net/fonts/font-resolver.yaml");
        }
    }
}
