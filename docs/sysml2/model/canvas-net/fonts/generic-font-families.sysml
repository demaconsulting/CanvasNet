package CanvasNet_GenericFontFamilies {
    private import SoftwareItemMetadata::*;

    part def GenericFontFamilies {
        doc /* Classifies a font family name as sans-serif, serif or monospace when it is a CSS generic family keyword or, on request, a well-known alias (Arial, Times New Roman, Courier New, the Liberation family names, and similar), and splits a CSS font-family list at the commas outside quotes. The single table of generic-family policy behind FontResolver. Stateless and thread-safe. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet/Fonts/GenericFontFamilies.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.Tests/Fonts/GenericFontFamiliesTests.cs");
            designFiles = ("docs/design/canvas-net/fonts/generic-font-families.md");
            verificationFiles = ("docs/verification/canvas-net/fonts/generic-font-families.md");
            requirementFiles = ("docs/reqstream/canvas-net/fonts/generic-font-families.yaml");
        }
    }
}
