package CanvasNet_RasterImageSource {
    private import SoftwareItemMetadata::*;

    part def RasterImageSource {
        doc /* The IImageSource for raster pictures: it claims .png, .jpg, .jpeg, .bmp, .tif, .tiff, .gif, .jp2, .j2k, and .j2c and no scheme, decides the format from the content (not the name), turns a JPEG upright by its Exif orientation, returns the natural size with transparency and the detected format name, checks the size its header declares against the live pixel budget (charging it once with EnsureWithinBudget) before decoding (ImageSourceBudgetException), and reports a corrupt or unrecognized file with InvalidDataException. Stateless and thread-safe. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet/ImageSources/RasterImageSource.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.Tests/ImageSources/RasterImageSourceTests.cs");
            designFiles = ("docs/design/canvas-net/image-sources/raster-image-source.md");
            verificationFiles = ("docs/verification/canvas-net/image-sources/raster-image-source.md");
            requirementFiles = ("docs/reqstream/canvas-net/image-sources/raster-image-source.yaml");
        }
    }
}
