package CanvasNet {
    part def ImageSources {
        doc /* Linked-picture subsystem: lets a document that links to other pictures (an SVG image element, for example) draw models, charts, other vector files, document pages, and rasters through one resolver and pluggable sources, without the renderer knowing any format. It exposes the ImageSourceContract unit (the public IImageSource, ImageSourceRequest, ImageSourceResult, ImageSourceDiagnostic, IImageResolver, EmbeddedImage, and ImageSourceException types, and the ImageSourceSet that validates what sources claim and lets the later source win), the FolderImageResolver unit (confines links to a folder through the Files subsystem, picks the source by file name extension or URI scheme, bounds depth, cycles, and image count, forwards notes with the embedding link, wraps failures, and never caches a picture), and the RasterImageSource unit (the source for PNG, JPEG, BMP, TIFF, GIF, and JPEG 2000). ImageSources depends on the Files, Canvas, and Codecs subsystems; no core subsystem depends on it; the format packages each supply a source, the CanvasNetSvg system takes an IImageResolver, and the CanvasNetViewer system lists the sources. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/ImageSources/FolderImageResolverTests.cs, test/DemaConsulting.CanvasNet.Tests/ImageSources/ImageSourceSetTests.cs, test/DemaConsulting.CanvasNet.Tests/ImageSources/ImageSourceContractTests.cs, test/DemaConsulting.CanvasNet.Tests/ImageSources/RasterImageSourceTests.cs */
        comment designRef /* Design: docs/design/canvas-net/image-sources.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/image-sources.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/image-sources.yaml */

        part imageSourceContract : ImageSourceContract;
        part folderImageResolver : FolderImageResolver;
        part rasterImageSource : RasterImageSource;

        dependency usesFiles from ImageSources to Files;
        dependency usesCanvas from ImageSources to Canvas;
        dependency usesCodecs from ImageSources to Codecs;
    }
}
