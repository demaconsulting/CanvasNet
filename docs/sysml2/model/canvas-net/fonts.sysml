package CanvasNet_FontsSubsystem {
    private import SoftwareItemMetadata::*;
    public import CanvasNet_TrueTypeFont::*;
    public import CanvasNet_SystemFontCatalog::*;
    public import CanvasNet_FontDirectoryScanner::*;
    public import CanvasNet_GenericFontFamilies::*;
    public import CanvasNet_FontResolver::*;
    public import CanvasNet_GlyphFallback::*;

    part def Fonts {
        doc /* TrueType and CFF/OpenType font-loading subsystem exposing the TrueTypeFont unit (loads glyph-based (glyf) SFNT fonts and CFF/OpenType (OTTO-flavored) fonts, including standalone classic PostScript Type 1/Type 1C font-program loading, selecting an individual face out of a TrueType Collection (ttcf) container, maps Unicode codepoints to glyph indices, extracts glyph outlines as Geometry.Path geometry, and reports horizontal metrics and basic format-0 kerning)         and the SystemFontCatalog unit (directory-scan-only discovery of fonts installed on the host operating system, best-effort family-name/style matching, a process-wide cached font load, and a bundled Liberation Sans/Serif/Mono last-resort fallback font) built on the FontDirectoryScanner unit (deterministic, bounded, link-safe header-only directory scan), and the FontResolver unit (resolves a CSS font-family list to a loaded font - generic keywords and optional aliases to the bundled fonts, then additional directories, installed fonts and bundled family names - with the GenericFontFamilies classification and list-splitting policy), and the GlyphFallback unit (per-character fallback to the bundled Noto Sans, Noto Sans Math and Noto Sans Symbols 2 fonts for a character that a chosen font has no glyph for, and the list of characters that stay missing). Fonts depends only on Geometry; callers combine its output with Drawing and Canvas if they want rendered pixels. */

        metadata info : SoftwareInfo {
            testFiles = (
                "test/DemaConsulting.CanvasNet.Tests/CanvasNetTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/TestSupport/SyntheticFontBuilder.cs",
                "test/DemaConsulting.CanvasNet.Tests/Fonts/TrueTypeFontRealFontIntegrationTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Fonts/SystemFontCatalogRealDiscoveryIntegrationTests.cs"
            );
            designFiles = ("docs/design/canvas-net/fonts.md");
            verificationFiles = ("docs/verification/canvas-net/fonts.md");
            requirementFiles = ("docs/reqstream/canvas-net/fonts.yaml");
        }

        part trueTypeFont : TrueTypeFont;
        part systemFontCatalog : SystemFontCatalog;
        part fontDirectoryScanner : FontDirectoryScanner;
        part genericFontFamilies : GenericFontFamilies;
        part fontResolver : FontResolver;
        part glyphFallback : GlyphFallback;
    }
}
