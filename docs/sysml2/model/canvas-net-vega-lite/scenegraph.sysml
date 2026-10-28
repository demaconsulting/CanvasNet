package CanvasNet {
    part def VegaLiteScenegraph {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.VegaLite.Scenegraph namespace where a chart becomes pixels in two steps. The VegaLiteSceneModel unit is the immutable scene that is the assertion surface of the tests: Scene (canvas size, background, root group), the items SceneGroup (a coordinate system with its own background, border, and optional clip), SceneRect, SceneRule, ScenePath, SceneSymbol, and SceneText, each with a stable role and paints as CSS color strings, SceneBounds, and the SceneBudget that bounds the item count; enumeration is iterative in drawing order. The ScenePainter unit draws a scene onto a core Surface with the public CanvasNet drawing API only (fills, strokes, dashes, opacity, rotation, a uniform scale and fit-and-center placement, fractional-coverage clipping through a transparent layer, symbols, and filled text), deterministically and in the item's own units. The CssColor unit parses hex, the 148 named colors, rgb, rgba, hsl, and hsla CSS colors without throwing for bad text. The subsystem is internal; its entry points are called by the builders and by VegaLiteDocument. As of this release, its constituent units are VegaLiteSceneModel, ScenePainter, and CssColor. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.VegaLite.Tests/Scenegraph/ (the unit test classes SceneModelTests, ScenePainterTests, and CssColorTests) */
        comment designRef /* Design: docs/design/canvas-net-vega-lite/scenegraph.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-vega-lite/scenegraph.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-vega-lite/scenegraph.yaml */
        comment /* Within the CanvasNetVegaLite system the ScenePainter unit of this subsystem is the only unit that draws with the core CanvasNet system (Surface and Rgba32 of Canvas, PathFiller and PathStroker of Drawing, Path and PathBuilder of Geometry, TextRenderer of Rendering); the VegaLiteSceneModel unit depends on nothing but the base class library and the public location and exception types, and CssColor needs only Rgba32. See the CanvasNetVegaLiteSystem dependencies. */

        part vegaLiteSceneModel : VegaLiteSceneModel;
        part scenePainter : ScenePainter;
        part cssColor : CssColor;
    }
}
