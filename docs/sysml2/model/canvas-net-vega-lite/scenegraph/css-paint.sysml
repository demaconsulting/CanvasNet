package CanvasNet {
    part def CssPaint {
        doc /* The internal static CssPaint: the paint rules of a chart on top of the shared CSS color grammar of the core CssColor unit. A paint is a color or no color: null, empty, white space, and none mean do not draw (IsNone); TryResolvePaint applies that rule, parses the color with CssColor.TryParse, and applies an opacity in one step (false for no paint, an unparseable paint, and a fully transparent result, never throwing); WithOpacity multiplies the alpha of a color by an opacity clamped to 0 to 1 (not a number counts as 1), rounded half away from zero. The unit keeps no color table or parser of its own. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.VegaLite/Scenegraph/CssPaint.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.VegaLite.Tests/Scenegraph/CssPaintTests.cs */
        comment designRef /* Design: docs/design/canvas-net-vega-lite/scenegraph/css-paint.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-vega-lite/scenegraph/css-paint.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-vega-lite/scenegraph/css-paint.yaml */
    }
}
