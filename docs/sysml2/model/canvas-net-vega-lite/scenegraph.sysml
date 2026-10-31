package CanvasNet {
    part def VegaLiteScenegraph {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.VegaLite.Scenegraph namespace where a chart becomes pixels in two steps. The VegaLiteSceneModel unit is the immutable scene that is the assertion surface of the tests: Scene (canvas size, background, root group), the items SceneGroup (a coordinate system with its own background, border, and optional clip), SceneRect, SceneRule, ScenePath (points, or absolute segments of moves, lines, cubic Beziers, and closes), SceneSymbol, SceneText, SceneArc, and SceneTrail, each with a stable role and paints as CSS color strings, SceneBounds, and the SceneBudget that bounds the item count (charged before each item is created); enumeration is iterative in drawing order. The ScenePainter unit draws a scene onto a core Surface with the public CanvasNet drawing API only (fills, strokes, dashes, miter limits, opacity, rotation, a uniform scale and fit-and-center placement, fractional-coverage clipping through a transparent layer, symbols, paths with curved segments, arcs, trails, and filled text), deterministically and in the item's own units. The ArcGeometry unit builds the d3-shape outline of an arc (sector, ring, full circle, pad angle, and corner radius) as a core Path centered at the origin; the painter translates it to the center of each SceneArc. The CssColor unit parses hex, the 148 named colors, rgb, rgba, hsl, and hsla CSS colors without throwing for bad text. The subsystem is internal; its entry points are called by the builders and by VegaLiteDocument. As of this release, its constituent units are VegaLiteSceneModel, ScenePainter, ArcGeometry, and CssColor. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.VegaLite.Tests/Scenegraph/ (the unit test classes SceneModelTests, ScenePainterTests, ScenePainterPathTests, ArcGeometryTests, and CssColorTests) */
        comment designRef /* Design: docs/design/canvas-net-vega-lite/scenegraph.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-vega-lite/scenegraph.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-vega-lite/scenegraph.yaml */
        comment /* Within the CanvasNetVegaLite system the ScenePainter and ArcGeometry units of this subsystem are the only units that draw with or build the geometry of the core CanvasNet system (Surface and Rgba32 of Canvas, PathFiller and PathStroker of Drawing, Path and PathBuilder of Geometry, TextRenderer of Rendering); the VegaLiteSceneModel unit depends on nothing but the base class library and the public location and exception types, and CssColor needs only Rgba32. See the CanvasNetVegaLiteSystem dependencies. */

        part vegaLiteSceneModel : VegaLiteSceneModel;
        part scenePainter : ScenePainter;
        part arcGeometry : ArcGeometry;
        part cssColor : CssColor;
    }
}
