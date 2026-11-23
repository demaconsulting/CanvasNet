package CanvasNet_SystemFontCatalog {
    private import SoftwareItemMetadata::*;

    part def SystemFontCatalog {
        doc /* Directory-scan-only discovery of fonts installed on the host operating system (Fonts), best-effort family-name/style matching against that catalog (FindBestMatch), and a bundled, always-available Liberation Sans/Serif/Mono last-resort fallback font shipped as an embedded resource of this assembly (LoadBundledFallback). Format-agnostic: no knowledge of any particular document format's font-naming conventions. Reads only font headers through the internal SfntHeaderReader (Fonts/SfntHeaderReader.cs) rather than loading whole files. Depends on TrueTypeFont within the same Fonts subsystem. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet/Fonts/SystemFontCatalog.cs",
                "src/DemaConsulting.CanvasNet/Fonts/SfntHeaderReader.cs",
                "src/DemaConsulting.CanvasNet/Fonts/BundledFonts/README.md"
            );
            testFiles = (
                "test/DemaConsulting.CanvasNet.Tests/Fonts/SystemFontCatalogTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Fonts/SystemFontCatalogRealDiscoveryIntegrationTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Fonts/BundledLiberationFontsTests.cs"
            );
            designFiles = ("docs/design/canvas-net/fonts/system-font-catalog.md");
            verificationFiles = ("docs/verification/canvas-net/fonts/system-font-catalog.md");
            requirementFiles = ("docs/reqstream/canvas-net/fonts/system-font-catalog.yaml");
        }
    }
}
