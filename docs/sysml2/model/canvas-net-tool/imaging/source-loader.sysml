package CanvasNet {
    part def SourceLoader {
        doc /* Turns a path into facts or into pixels for the files the tool reads. Detect recognizes the format from the signature bytes, then SVG text, then the extension; Probe reads the header facts (format, size, channels, transparency, decodability, frame count, EXIF orientation) without decoding; Load decodes a raster at its own size or renders an SVG directly at the target size, applying the size policy (scale, width, height, rounding, the 8192-pixel limit before decoding), the EXIF orientation of a JPEG, and flattening over a background color, and reports the operations applied. It maps an explicit list of library exceptions (missing, unreadable, unsupported feature, corrupt data) to expected errors and lets every other exception propagate. The unit includes ExifOrientation (reading the orientation from a JPEG and applying it to pixels), SurfaceOperations (flatten and transparency test), and the supporting model types SourceKind, OutputFormat, SourceInfo, SourceOptions, LoadedSource, AppliedOperation, ImageDiagnostic, and SourcePage. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Tool/Imaging/SourceLoader.cs, src/DemaConsulting.CanvasNet.Tool/Imaging/SourceLoader.Detect.cs, src/DemaConsulting.CanvasNet.Tool/Imaging/SourceLoader.Raster.cs, src/DemaConsulting.CanvasNet.Tool/Imaging/SourceLoader.Svg.cs, src/DemaConsulting.CanvasNet.Tool/Imaging/ExifOrientation.cs, src/DemaConsulting.CanvasNet.Tool/Imaging/SurfaceOperations.cs, src/DemaConsulting.CanvasNet.Tool/Imaging/SourceModels.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tool.Tests/Imaging/SourceLoaderTests.cs, test/DemaConsulting.CanvasNet.Tool.Tests/Imaging/ExifOrientationTests.cs */
        comment designRef /* Design: docs/design/canvas-net-tool/imaging/source-loader.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-tool/imaging/source-loader.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-tool/imaging/source-loader.yaml */
    }
}
