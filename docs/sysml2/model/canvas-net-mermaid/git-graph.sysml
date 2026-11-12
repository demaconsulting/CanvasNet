package CanvasNet {
    part def MermaidGitGraph {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.Mermaid.GitGraph namespace that reads and draws git graphs (the gitGraph keyword): a port of the git graph of the pinned Mermaid release. The GitGraphParser unit reads the preprocessed text with a port of the Langium gitGraph grammar of the Mermaid parser package (its tokens in chevrotain's order, its value converters, and Langium's string conversion) and runs the statements as Mermaid's git graph database runs them (commits, branches with their order, checkouts and switches, merges, and cherry-picks, refused with Mermaid's messages, the commit ids Mermaid generates drawn from a fixed pseudo-random sequence, and the sanitized title and accessibility texts), with the MaxNodes and MaxLabelLength limits, into the immutable GitGraphDiagram that gives the document its metadata (the commits as nodes, the links to their parents as edges, no subgraphs, the direction LR, TB, or BT, and the title); a render whose configuration names another main branch or main-branch order runs the statements again. The GitGraphRenderer unit draws the diagram as Mermaid's git graph renderer does: the branch lines and labels, the commit bullets by commit type, the arrows with their rerouting lanes, the commit labels (rotated by 45 degrees when rotateCommitLabel is set), the tags, the title, and the view box of the browser's bounding box grown by diagramPadding, painted from the git graph style sheet. A parsed diagram is immutable and safe for concurrent renders. As of this release, its constituent units are GitGraphParser and GitGraphRenderer. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Mermaid.Tests/GitGraph/GitGraphTests.cs (with the original fixtures test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/gitgraph/) */
        comment designRef /* Design: docs/design/canvas-net-mermaid/git-graph.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-mermaid/git-graph.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-mermaid/git-graph.yaml */
        comment /* Within the CanvasNetMermaid system the GitGraph subsystem depends on the Syntax subsystem (preprocessed and mapped text, JavaScript trim and white space, HTML sanitizing), the Configuration subsystem (effective configuration with its gitGraph section and the git graph style sheet), the Rendering subsystem (text metrics of the label builder), the Dagre subsystem (layout work meter), and the Scenegraph subsystem (scene types, including the rotation of a text line); it is called by the document pipeline of the system. */

        part gitGraphParser : GitGraphParser;
        part gitGraphRenderer : GitGraphRenderer;
    }
}
