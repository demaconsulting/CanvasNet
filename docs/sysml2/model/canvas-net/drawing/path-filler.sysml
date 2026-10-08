package CanvasNet {
    part def PathFiller {
        doc /* Public static Fill entry point rasterizing a closed Path onto a Surface with a solid color, using the AGG/FreeType-style analytic signed-area/coverage-accumulation scanline algorithm. Also covers the supporting FillRule enum and the internal EdgeFlattener/ScanlineRasterizer/ClipMask helpers inline; none has independent behavior of its own. ClipMask represents a PDF content stream's active clipping path as a per-pixel antialiased coverage mask, and backs internal clip-aware Fill overloads consumed solely by DemaConsulting.CanvasNet.Pdf. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet/Drawing/PathFiller.cs, src/DemaConsulting.CanvasNet/Drawing/FillRule.cs, src/DemaConsulting.CanvasNet/Drawing/EdgeFlattener.cs, src/DemaConsulting.CanvasNet/Drawing/ScanlineRasterizer.cs, src/DemaConsulting.CanvasNet/Drawing/ClipMask.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/Drawing/PathFillerTests.cs, test/DemaConsulting.CanvasNet.Tests/Drawing/EdgeFlattenerTests.cs, test/DemaConsulting.CanvasNet.Tests/Drawing/ScanlineRasterizerTests.cs, test/DemaConsulting.CanvasNet.Tests/Drawing/ClipMaskTests.cs */
        comment designRef /* Design: docs/design/canvas-net/drawing/path-filler.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/drawing/path-filler.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/drawing/path-filler.yaml */
    }
}
