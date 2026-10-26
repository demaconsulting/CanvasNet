package CanvasNet {
    part def ImagePipeline {
        doc /* The ordered image pipeline: parses the transformation options of a command line (crop as x,y,w,h or WxH+X+Y in pixels or percent, resize to an exact size, fit inside a box that only shrinks unless upscaling is allowed, rotate by 90, 180, or 270 degrees, flip, and pad with a border color, with the resampling filter chosen by name) into operations, and applies them left to right to a Surface, one new surface per step with every intermediate disposed. A crop past an edge is cut off, a crop outside the image, an angle that is not a right angle, a malformed value, and a setting without its operation are usage errors, and a size or border beyond 8192 pixels is refused before allocation. The unit reports every operation applied with its parameters and the size of its result. The render command and the verb commands crop, resize, rotate, flip, and pad share it. The unit includes the operation records in ImageOperations.cs. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Tool/Imaging/ImagePipeline.cs, src/DemaConsulting.CanvasNet.Tool/Imaging/ImageOperations.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tool.Tests/Imaging/ImagePipelineTests.cs */
        comment designRef /* Design: docs/design/canvas-net-tool/imaging/image-pipeline.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-tool/imaging/image-pipeline.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-tool/imaging/image-pipeline.yaml */
    }
}
