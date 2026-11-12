package CanvasNet {
    part def PacketRenderer {
        doc /* The internal sealed PacketRenderer: Draw turns a PacketDiagram into a scene for one render as Mermaid's packet renderer (draw and drawWord) does. The blocks are split into rows of the configured bitsPerRow; each row is the row height plus the vertical padding (grown by 10 for the bit numbers) below the one before it; each part is a rectangle at its first bit within the row times the bit width plus 1, as wide as its bits times the bit width less the horizontal padding (no rectangle when that is not more than zero), with its label centered on it and, when the bit numbers are shown, its first and last bits 2 pixels above its corners (one centered number for a single bit), written as JavaScript writes numbers. The title is centered in a row of its own below the rows, and the view box is the bit width times bitsPerRow plus 2 wide and as high as the rows and the title row. Texts are shown as the browser shows them (Mermaid's entity placeholders decoded, white space collapsed) on the baseline the dominant-baseline gives, with the box of the browser's getBBox. The paint comes from the packet style sheet. The layout work meter is charged for the blocks, the parts, and the measured texts and checks cancellation. An instance draws one render on one thread. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Mermaid/Packet/PacketRenderer.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Mermaid.Tests/Packet/PacketRendererTests.cs */
        comment designRef /* Design: docs/design/canvas-net-mermaid/packet/packet-renderer.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-mermaid/packet/packet-renderer.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-mermaid/packet/packet-renderer.yaml */
    }
}
