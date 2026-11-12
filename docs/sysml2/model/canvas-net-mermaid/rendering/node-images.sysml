package CanvasNet {
    part def NodeImages {
        doc /* The internal sealed NodeImages, the ISceneImageLoader of a rendering: the pictures of the img nodes and of the img elements of labels, handed as text only to the caller's MermaidRenderOptions.ImageResolver. NaturalSize asks the resolver once per link for the picture at 256 by 256 device pixels and takes the size of what it returns as the natural size (a raster at its own size, a vector picture at the probe size), keeping the answer for the rest of the rendering; Load asks for the picture at the device size the painter draws it at. Without a resolver, for an empty link, or when the resolver gives nothing, the link is reported once as mermaid-image-unresolved at the first element that named it and nothing is drawn. Every resolver call, by the layout and the painter alike, counts against MermaidLimits.MaxImages; the call that would pass it is not made and fails with a located mermaid-limit-exceeded. An exception of the resolver propagates unchanged. An instance belongs to one call on one thread. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Mermaid/Rendering/NodeImages.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Mermaid.Tests/Rendering/NodeImagesTests.cs */
        comment designRef /* Design: docs/design/canvas-net-mermaid/rendering/node-images.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-mermaid/rendering/node-images.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-mermaid/rendering/node-images.yaml */
    }
}
