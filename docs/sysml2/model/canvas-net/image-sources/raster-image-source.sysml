package CanvasNet {
    part def RasterImageSource {
        doc /* The IImageSource for raster pictures: it claims .png, .jpg, .jpeg, .bmp, .tif, .tiff, .gif, .jp2, .j2k, and .j2c and no scheme, decides the format from the content (not the name), turns a JPEG upright by its Exif orientation, returns the natural size with transparency and the detected format name, checks the size its header declares against the remaining pixel budget before decoding (ImageSourceBudgetException), and reports a corrupt or unrecognized file with InvalidDataException. Stateless and thread-safe. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet/ImageSources/RasterImageSource.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/ImageSources/RasterImageSourceTests.cs */
        comment designRef /* Design: docs/design/canvas-net/image-sources/raster-image-source.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/image-sources/raster-image-source.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/image-sources/raster-image-source.yaml */
    }
}
