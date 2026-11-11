package CanvasNet {
    part def MermaidPie {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.Mermaid.Pie namespace that reads and draws pie charts (the pie keyword): a port of the pie chart of the pinned Mermaid release. The PieParser unit reads the preprocessed text with a port of the Langium pie grammar of the Mermaid parser package (its tokens in chevrotain's order, its value converters, and Langium's string conversion) and of Mermaid's pie database (the sections in order with the first value of a repeated label, a negative value refused, showData, the sanitized title and accessibility texts), with the MaxNodes and MaxLabelLength limits, into the immutable PieDiagram that gives the document its metadata (the sections as nodes, no edges or subgraphs, no direction). The PieRenderer unit draws the chart as Mermaid's pie renderer does: d3's pie and arc with the 1% filter, the percentage labels, the theme's twelve pie colors by section, the title, the legend in its five positions from the measured legend texts, the donut hole, the highlighted slice, and the view box that takes in the legend and a wide title, painted from the pie style sheet. A parsed diagram is immutable and safe for concurrent renders. As of this release, its constituent units are PieParser and PieRenderer. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Mermaid.Tests/Pie/PieTests.cs (with the original fixtures test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/pie/) */
        comment designRef /* Design: docs/design/canvas-net-mermaid/pie.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-mermaid/pie.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-mermaid/pie.yaml */
        comment /* Within the CanvasNetMermaid system the Pie subsystem depends on the Syntax subsystem (preprocessed and mapped text, JavaScript trim, HTML sanitizing), the Configuration subsystem (effective configuration with its pie section and the pie style sheet), the Rendering subsystem (text metrics of the label builder), the Dagre subsystem (layout work meter), and the Scenegraph subsystem (scene types); it is called by the document pipeline of the system. */

        part pieParser : PieParser;
        part pieRenderer : PieRenderer;
    }
}
