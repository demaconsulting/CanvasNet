package CanvasNet {
    part def MermaidPacket {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.Mermaid.Packet namespace that reads and draws packet diagrams (the packet and packet-beta keywords): a port of the packet diagram of the pinned Mermaid release. The PacketParser unit reads the preprocessed text with a port of the Langium packet grammar of the Mermaid parser package (its tokens in chevrotain's order, its value converters, and Langium's string and integer conversions) and of Mermaid's packet database (the blocks in order, each starting at the bit after the block before it, with a non-zero bit count and an end not before its start, refused with Mermaid's messages, and the sanitized title and accessibility texts), with the MaxNodes and MaxLabelLength limits, into the immutable PacketDiagram that gives the document its metadata (the blocks as nodes, no edges or subgraphs, no direction) and splits its blocks into rows of the configured bitsPerRow as Mermaid's populate does, in JavaScript's double arithmetic, with Mermaid's 10,000-row cap and MaxNodes on the parts. The PacketRenderer unit draws the diagram as Mermaid's packet renderer does: each row's block rectangles with their labels and bit numbers, the title in a row of its own below them, and the view box of the bits of a row by the rows, painted from the packet style sheet. A parsed diagram is immutable and safe for concurrent renders. As of this release, its constituent units are PacketParser and PacketRenderer. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Mermaid.Tests/Packet/PacketTests.cs (with the original fixtures test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/packet/) */
        comment designRef /* Design: docs/design/canvas-net-mermaid/packet.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-mermaid/packet.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-mermaid/packet.yaml */
        comment /* Within the CanvasNetMermaid system the Packet subsystem depends on the Syntax subsystem (preprocessed and mapped text, JavaScript trim, HTML sanitizing), the Configuration subsystem (effective configuration with its packet section and the packet style sheet), the Rendering subsystem (text metrics of the label builder), the Dagre subsystem (layout work meter), and the Scenegraph subsystem (scene types); it is called by the document pipeline of the system. */

        part packetParser : PacketParser;
        part packetRenderer : PacketRenderer;
    }
}
