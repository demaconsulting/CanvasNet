package CanvasNet {
    part def Rgba32 {
        doc /* 32-bit RGBA pixel value type. Exposes Parse(string) and TryParse(string?, out) methods that accept #RRGGBB (alpha=255) and #AARRGGBB hex color literals, case-insensitive. Rejects short forms, missing '#', wrong length, and non-hex characters. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet/Canvas/Rgba32.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/Canvas/Rgba32Tests.cs */
        comment designRef /* Design: docs/design/canvas-net/canvas/rgba32.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/canvas/rgba32.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/canvas/rgba32.yaml */
    }
}
