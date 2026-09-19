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
        comment /* A future Drawing subsystem (primitives/brushes/pens/transforms) is reserved but not yet implemented. */

        part canvas : Canvas;
        part codecs : Codecs;
    }
}

