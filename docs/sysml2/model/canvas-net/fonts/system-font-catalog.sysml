package CanvasNet {
    part def SystemFontCatalog {
        doc /* Directory-scan-only discovery of fonts installed on the host operating system (Fonts), best-effort family-name/style matching against that catalog (FindBestMatch), and a bundled, always-available Liberation Sans/Serif/Mono last-resort fallback font shipped as an embedded resource of this assembly (LoadBundledFallback). Format-agnostic: no knowledge of any particular document format's font-naming conventions. Depends on TrueTypeFont within the same Fonts subsystem. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet/Fonts/SystemFontCatalog.cs, src/DemaConsulting.CanvasNet/Fonts/BundledFonts/README.md */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/Fonts/SystemFontCatalogTests.cs, test/DemaConsulting.CanvasNet.Tests/Fonts/SystemFontCatalogRealDiscoveryIntegrationTests.cs, test/DemaConsulting.CanvasNet.Tests/Fonts/BundledLiberationFontsTests.cs */
        comment designRef /* Design: docs/design/canvas-net/fonts/system-font-catalog.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/fonts/system-font-catalog.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/fonts/system-font-catalog.yaml */
    }
}
