package CanvasNet {
    doc
    /*
     * CanvasNet is a .NET library providing a canvas-based drawing and rendering API,
     * following DEMA Consulting best practices for structuring, documenting, and testing.
     */

    part def CanvasNetSystem {
        doc /* The CanvasNet system as a whole. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/CanvasNetTests.cs */
        comment designRef /* Design: docs/design/canvas-net.md */
        comment verificationRef /* Verification: docs/verification/canvas-net.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net.yaml */
        comment /* The Drawing subsystem renders Geometry.Path geometry onto a Canvas.Surface through PathFiller (direct fill, solid color or GradientPaint), PathStroker (stroke-to-fill conversion), and GradientPaint (linear/radial gradient evaluation), while the Fonts subsystem loads glyph-based TrueType fonts and produces Geometry.Path glyph outlines plus metrics and kerning for callers that want text rendering. */

        part canvas : Canvas;
        part codecs : Codecs;
        part geometry : Geometry;
        part drawing : Drawing;
        part fonts : Fonts;
        part renderingSubsystem : Rendering;
    }
}
