package CanvasNetMermaid_NodeImages {
    private import SoftwareItemMetadata::*;

    part def NodeImages {
        doc /* The internal sealed NodeImages, the ISceneImageLoader of a rendering: the pictures of the img nodes and of the img elements of labels, handed as text only to the caller's MermaidRenderOptions.ImageResolver. NaturalSize asks the resolver once per link for the picture at 256 by 256 device pixels and takes the size of what it returns as the natural size (a raster at its own size, a vector picture at the probe size), keeping the answer for the rest of the rendering; it asks through the probe of a FolderImageResolver (the internal FolderImageResolver.IImageProbe, of the resolver or a scope of one), which lists nothing for it, so the picture is listed once in Embedded, by the draw; Load asks for the picture at the device size the painter draws it at. Without a resolver, for an empty link, or when the resolver gives nothing, the link is reported once as mermaid-image-unresolved at the first element that named it and nothing is drawn. Every resolver call, by the layout and the painter alike, counts against MermaidLimits.MaxImages; the call that would pass it is not made and fails with a located mermaid-limit-exceeded. An exception of the resolver propagates unchanged. An instance belongs to one call on one thread. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet.Mermaid/Rendering/NodeImages.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.Mermaid.Tests/Rendering/NodeImagesTests.cs");
            designFiles = ("docs/design/canvas-net-mermaid/rendering/node-images.md");
            verificationFiles = ("docs/verification/canvas-net-mermaid/rendering/node-images.md");
            requirementFiles = ("docs/reqstream/canvas-net-mermaid/rendering/node-images.yaml");
        }
    }
}
