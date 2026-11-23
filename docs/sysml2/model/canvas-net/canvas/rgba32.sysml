package CanvasNet_Rgba32 {
    private import SoftwareItemMetadata::*;

    part def Rgba32 {
        doc /* 32-bit RGBA pixel value type. Exposes Parse(string) and TryParse(string?, out) methods that accept #RRGGBB (alpha=255) and #AARRGGBB hex color literals, case-insensitive. Rejects short forms, missing '#', wrong length, and non-hex characters. Also exposes a public single-pixel CompositeOver(Rgba32, Rgba32) Porter-Duff "over" alpha-blending helper (same formula and rounding as Surface.CompositeOver(Rgba32)), used by DemaConsulting.CanvasNet.Pptx's PaintPicture and DemaConsulting.CanvasNet.Pdf's CompositeImageOntoSurface. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet/Canvas/Rgba32.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.Tests/Canvas/Rgba32Tests.cs");
            designFiles = ("docs/design/canvas-net/canvas/rgba32.md");
            verificationFiles = ("docs/verification/canvas-net/canvas/rgba32.md");
            requirementFiles = ("docs/reqstream/canvas-net/canvas/rgba32.yaml");
        }
    }
}
