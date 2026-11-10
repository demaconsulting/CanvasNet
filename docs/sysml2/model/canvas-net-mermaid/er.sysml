package CanvasNet {
    part def MermaidEr {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.Mermaid.Er namespace that reads entity relationship diagrams (the erDiagram keyword) and lays them out through the shared unified renderer: a port of the ER diagram of the pinned Mermaid release. The ErParser unit reads the preprocessed text with a port of the lexer of the generated erDiagram.jison parser (its rules and start conditions as matchers that never backtrack, the cardinalities in Mermaid's rule order) and a recursive-descent parser that accepts the language of its LALR(1) grammar with jison's error states and expected tokens, running each database action where jison reduces its rule. The ErModel unit is Mermaid's ER database (erDb.ts): entities with aliases and attributes, relationships with their cardinalities, line types, and roles (an end named after a subgraph using its id and adding an entity that is not drawn), subgraphs with their titles, directions, and unique nodes, style classes, and styles, with the MaxNodes, MaxEdges, and MaxLabelLength limits; it gives the document its metadata (the drawn entities, relationships, and subgraphs as the node, edge, and subgraph counts, and the last direction), maps the diagram for each render to the LayoutData of the Rendering subsystem as getData does, reports a look other than classic and a layout other than dagre, and has the cluster layout draw the scene with the ER style sheet, the title, and the padding of the ER renderer. The entity boxes and the cardinality markers are drawn by the Rendering subsystem. A parsed model is immutable and safe for concurrent renders. As of this release, its constituent units are ErParser and ErModel. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Mermaid.Tests/Er/ErTests.cs (with the original fixtures test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/er/) */
        comment designRef /* Design: docs/design/canvas-net-mermaid/er.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-mermaid/er.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-mermaid/er.yaml */
        comment /* Within the CanvasNetMermaid system the Er subsystem depends on the Syntax subsystem (scanner, mapped text, HTML sanitizing), the Configuration subsystem (effective configuration with its er section and the ER style sheet), the Rendering subsystem (layout data, cluster layout, label builder, and node images), the Dagre subsystem (layout work meter), and the Scenegraph subsystem (scene types); it is called by the document pipeline of the system. */

        part erParser : ErParser;
        part erModel : ErModel;
    }
}
