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
        comment /* The sibling CanvasNetSvgSystem (distributed as the separate DemaConsulting.CanvasNet.Svg package) depends on this system's Canvas, Geometry, Drawing, Fonts, and Codecs subsystems - see docs/sysml2/model/canvas-net-svg.sysml. */
        comment /* The Positions subsystem is the neutral model of the feature positions of a rendered picture (ImagePosition), the selector grammar that asks for some of them (PositionSelector and PositionSelection), and the collector that keeps the matches of one render up to a cap (PositionCollector); the sibling CanvasNetMermaid and CanvasNetVegaLite systems report positions with it, and the CanvasNetViewer and CanvasNetTool systems collect and report them. */

        part canvas : Canvas;
        part codecs : Codecs;
        part geometry : Geometry;
        part drawing : Drawing;
        part fonts : Fonts;
        part renderingSubsystem : Rendering;
        part files : Files;
        part packaging : Packaging;
        part imageSources : ImageSources;
        part positions : Positions;
    }
}
