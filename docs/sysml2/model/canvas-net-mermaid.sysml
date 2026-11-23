package CanvasNetMermaid {
    private import SoftwareItemMetadata::*;
    public import CanvasNetMermaid_MermaidDocument::*;
    public import CanvasNetMermaid_MermaidSyntaxSubsystem::*;
    public import CanvasNetMermaid_MermaidConfigurationSubsystem::*;
    public import CanvasNetMermaid_MermaidRenderingSubsystem::*;
    public import CanvasNetMermaid_MermaidScenegraphSubsystem::*;
    public import CanvasNetMermaid_MermaidFlowchartSubsystem::*;
    public import CanvasNetMermaid_MermaidSequenceSubsystem::*;
    public import CanvasNetMermaid_MermaidClassSubsystem::*;
    public import CanvasNetMermaid_MermaidStateSubsystem::*;
    public import CanvasNetMermaid_MermaidErSubsystem::*;
    public import CanvasNetMermaid_MermaidPieSubsystem::*;
    public import CanvasNetMermaid_MermaidGanttSubsystem::*;
    public import CanvasNetMermaid_MermaidMindmapSubsystem::*;
    public import CanvasNetMermaid_MermaidCoseSubsystem::*;
    public import CanvasNetMermaid_MermaidPacketSubsystem::*;
    public import CanvasNetMermaid_MermaidGitGraphSubsystem::*;

    part def CanvasNetMermaidSystem {
        doc /* CanvasNetMermaid is a static, offline, deterministic renderer for the Mermaid diagram language, delivered as the separate DemaConsulting.CanvasNet.Mermaid NuGet package. The public MermaidDocument unit parses Mermaid text from a string, a stream, or a file path (a located, coded, hinted MermaidSyntaxException for invalid text; an InvalidDataException for oversized text), describes the diagram without drawing it (type, supported flag, title, accessible title, direction, node, edge, and subgraph counts), measures it, and renders it onto a CanvasNet Surface at its natural size times a scale or fitted inside an exact size, over a transparent background by default. This version draws flowcharts (flowchart and graph) with Mermaid's own layout, shapes, labels, edges, themes, and styles, sequence diagrams (sequenceDiagram) with a port of Mermaid's sequence renderer, class diagrams (classDiagram) with the same layout as flowcharts and Mermaid's class boxes, markers, and cardinalities, state diagrams (stateDiagram) with the same layout and Mermaid's state shapes, composite states, concurrent regions, and notes, entity relationship diagrams (erDiagram) with the same layout and Mermaid's entity tables and cardinality markers, pie charts (pie) with a port of Mermaid's pie renderer, gantt charts (gantt) with a port of Mermaid's gantt renderer and the d3 time and dayjs date functions it uses, in UTC, with today taken only from MermaidRenderOptions.Now (the library never reads the system clock), mindmaps (mindmap) with a port of Mermaid's mindmap renderer laid out by a port of Mermaid's cose-bilkent layout, deterministic and the same on every platform, packet diagrams (packet and packet-beta) with a port of Mermaid's packet renderer and of the row split of its packet parser, and git graphs (gitGraph) with a port of Mermaid's git graph database and renderer; every other Mermaid diagram type is recognized and reported as mermaid-diagram-unsupported with an empty transparent picture. Every recognized but unsupported construct is reported with a stable mermaid-* code and location, and strict mode throws. Untrusted input is bounded by MermaidLimits. The internal subsystems are Syntax (preprocessing, detection, YAML, lexing, HTML sanitizing), Configuration (precedence, themes, colors, CSS declarations), Rendering (clusters, shapes, edges, labels, pictures), Scenegraph (scene model and painter), Flowchart (parser and model), Sequence (parser, model, layout, and renderer), Class (parser and model), State (parser and model), Er (parser and model), Pie (parser and renderer), Gantt (parser, model, renderer, and time axis), Mindmap (parser and renderer), Cose (the cose-bilkent layout of mindmaps), Packet (parser with the row split, and renderer), and GitGraph (parser with the database, and renderer); the graph library and the dagre layered layout that Mermaid bundles are the separate CanvasNetLayout system, which the Rendering subsystem calls and whose work meter the MermaidDocument unit's MermaidLayoutWorkMeter derives. The MermaidImageSource plugs the package into the CanvasNet image-source contract. With MermaidRenderOptions.PositionHandler a render reports the feature positions of the diagram (titles, groups, nodes, participants, tasks, milestones, sections, slices, blocks, commits, branches, tags, edges, messages, and notes, with their source ids, ends, and source lines) as core ImagePosition records in the pixels of the returned surface, through the ScenePositions unit of the Scenegraph subsystem, without changing the pixels; PositionKindFilter limits the kinds that are built. */

        metadata info : SoftwareInfo {
            testFiles = (
                "test/DemaConsulting.CanvasNet.Mermaid.Tests/MermaidSystemIntegrationTests.cs",
                "test/DemaConsulting.CanvasNet.Mermaid.Tests/MermaidFlowchartIntegrationTests.cs",
                "test/DemaConsulting.CanvasNet.Mermaid.Tests/MermaidReferenceLayoutTests.cs",
                "test/DemaConsulting.CanvasNet.Mermaid.Tests/MermaidSequenceIntegrationTests.cs",
                "test/DemaConsulting.CanvasNet.Mermaid.Tests/MermaidSequenceReferenceLayoutTests.cs",
                "test/DemaConsulting.CanvasNet.Mermaid.Tests/MermaidClassIntegrationTests.cs",
                "test/DemaConsulting.CanvasNet.Mermaid.Tests/MermaidStateIntegrationTests.cs",
                "test/DemaConsulting.CanvasNet.Mermaid.Tests/MermaidErIntegrationTests.cs",
                "test/DemaConsulting.CanvasNet.Mermaid.Tests/MermaidPieIntegrationTests.cs",
                "test/DemaConsulting.CanvasNet.Mermaid.Tests/MermaidGanttIntegrationTests.cs",
                "test/DemaConsulting.CanvasNet.Mermaid.Tests/MermaidChartReferenceLayoutTests.cs",
                "test/DemaConsulting.CanvasNet.Mermaid.Tests/MermaidMindmapIntegrationTests.cs",
                "test/DemaConsulting.CanvasNet.Mermaid.Tests/MermaidMindmapReferenceLayoutTests.cs",
                "test/DemaConsulting.CanvasNet.Mermaid.Tests/MermaidPacketIntegrationTests.cs",
                "test/DemaConsulting.CanvasNet.Mermaid.Tests/MermaidGitGraphIntegrationTests.cs",
                "test/DemaConsulting.CanvasNet.Mermaid.Tests/MermaidGitGraphReferenceLayoutTests.cs",
                "test/DemaConsulting.CanvasNet.Mermaid.Tests/MermaidHardeningTests.cs",
                "test/DemaConsulting.CanvasNet.Mermaid.Tests/MermaidUnsupportedDiagramTests.cs"
            );
            designFiles = ("docs/design/canvas-net-mermaid.md");
            verificationFiles = ("docs/verification/canvas-net-mermaid.md");
            requirementFiles = ("docs/reqstream/canvas-net-mermaid.yaml");
        }

        comment /* Third-Party Provenance and Attribution: CanvasNetMermaid is derived with attribution, not clean-room. Its preprocessing, detection, configuration, themes, flowchart grammar and database, shapes, labels, edges, cluster layout, sequence grammar, database, renderer, and drawing functions, class grammar and database, class box, class markers, terminal labels, state grammar and database, state shapes and clusters, the barb marker, ER grammar and database, entity box, ER markers, pie database and renderer, gantt grammar, database, and renderer, mindmap grammar, database, renderer, and shapes, packet database and renderer, git graph database, renderer, and style sheet, and text utilities are ported from mermaid (MIT); the pie, packet, and gitGraph grammars and their value converters from the Mermaid parser package @mermaid-js/parser (MIT) with the string and integer conversions of Langium (MIT); the gantt date functions from dayjs (MIT); the link check of the diagrams that keep links from sanitize-url (MIT); the intersect functions of the node shapes from dagre-d3-es (MIT; the layered layout and graph it ports from dagre and graphlib are in the CanvasNetLayout system, with their own provenance); the force-directed layout of mindmaps from layout-base and cose-base (MIT, iVis@Bilkent) and cytoscape-cose-bilkent and cytoscape (MIT, The Cytoscape Consortium); the curves, the pie and arc, the time scale, intervals, ticks, format, and axis from the d3 modules (ISC); the color functions from khroma (MIT); the YAML subset from js-yaml (MIT); the configuration merge from lodash (MIT); the markdown subset from marked (MIT). The license texts and the provenance table ship in src/DemaConsulting.CanvasNet.Mermaid/third-party/, and every file with ported logic carries a Portions derived from header. The package is not affiliated with those projects. */
        comment /* CanvasNetMermaid references the sibling CanvasNetLayout system (the graph library and the dagre layered layout, through internal friend access) and the sibling CanvasNet system: its Canvas subsystem (Surface, Rgba32, CssColor), Geometry (paths), Drawing (path filling and stroking), Fonts (the bundled fonts through a font resolver with no system fonts), Rendering (text measuring and drawing, word wrapping), ImageSources (the image-source contract and the caller's image resolver), and Positions (the ImagePosition records that the PositionHandler receives). It has no OTS runtime dependency. */

        part mermaidDocument : MermaidDocument;
        part mermaidSyntax : MermaidSyntax;
        part mermaidConfiguration : MermaidConfiguration;
        part mermaidRendering : MermaidRendering;
        part mermaidScenegraph : MermaidScenegraph;
        part mermaidFlowchart : MermaidFlowchart;
        part mermaidSequence : MermaidSequence;
        part mermaidClass : MermaidClass;
        part mermaidState : MermaidState;
        part mermaidEr : MermaidEr;
        part mermaidPie : MermaidPie;
        part mermaidGantt : MermaidGantt;
        part mermaidMindmap : MermaidMindmap;
        part mermaidCose : MermaidCose;
        part mermaidPacket : MermaidPacket;
        part mermaidGitGraph : MermaidGitGraph;

        dependency usesCanvas from CanvasNetMermaidSystem to CanvasNet_CanvasSubsystem::Canvas;
        dependency usesDrawing from CanvasNetMermaidSystem to CanvasNet_DrawingSubsystem::Drawing;
        dependency usesFonts from CanvasNetMermaidSystem to CanvasNet_FontsSubsystem::Fonts;
        dependency usesGeometry from CanvasNetMermaidSystem to CanvasNet_GeometrySubsystem::Geometry;
        dependency usesRendering from CanvasNetMermaidSystem to CanvasNet_RenderingSubsystem::Rendering;
        dependency usesImageSources from CanvasNetMermaidSystem to CanvasNet_ImageSourcesSubsystem::ImageSources;
        dependency usesPositions from CanvasNetMermaidSystem to CanvasNet_PositionsSubsystem::Positions;
        dependency usesLayout from CanvasNetMermaidSystem to CanvasNetLayout::CanvasNetLayoutSystem;
    }
}
