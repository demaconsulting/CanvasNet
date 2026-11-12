package CanvasNet {
    part def MermaidGantt {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.Mermaid.Gantt namespace that reads and draws gantt charts (the gantt keyword): a port of the gantt chart of the pinned Mermaid release with the dayjs and d3 time functions it uses. The GanttParser unit reads the preprocessed text with a port of the lexer and the LALR(1) grammar of Mermaid's gantt.jison (rule order, start conditions, and quirks kept; jison's expected tokens in errors) and calls the gantt database where jison runs its actions. The GanttModel unit is Mermaid's gantt database: the statements, the sections, the tasks with their tags, ids, and fields, the links checked by the shared link check and the clickable class, JavaScript object-key semantics, the MaxNodes and MaxLabelLength limits, and the metadata (the tasks as nodes and the drawn sections as subgraphs); for each render it compiles the task dates as Mermaid's getTasks does, with the dayjs subset Mermaid bundles, in UTC, with today taken only from MermaidRenderOptions.Now (else 1970-01-01 and a mermaid-gantt-today-unavailable warning), failing located where Mermaid's compilation fails, and charging the layout work meter. The GanttRenderer unit draws the chart as Mermaid's gantt renderer does: the page width or useWidth, the rows or compact rows, the time scale, the section rows, bars, milestones, vertical markers, task texts, section titles, excluded days, the d3 axes and their grid with the tick interval, the today line, and the title, painted through the cascade of the gantt style sheet, reporting skipped ticks and excluded days. The TimeAxis unit is the JavaScript Date arithmetic in UTC and the d3 time intervals, ticks, time scale, and time format the chart is built on. A parsed model is immutable and safe for concurrent renders. As of this release, its constituent units are GanttParser, GanttModel, GanttRenderer, and TimeAxis. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Mermaid.Tests/Gantt/GanttTests.cs (with the original fixtures test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/gantt/) */
        comment designRef /* Design: docs/design/canvas-net-mermaid/gantt.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-mermaid/gantt.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-mermaid/gantt.yaml */
        comment /* Within the CanvasNetMermaid system the Gantt subsystem depends on the Syntax subsystem (scanner, mapped text, JavaScript trim, HTML sanitizing, and the link check), the Configuration subsystem (effective configuration with its gantt section and the gantt style sheet), the Rendering subsystem (text metrics of the label builder), the Dagre subsystem (layout work meter), the Graphlib subsystem (JavaScript object-key order), and the Scenegraph subsystem (scene types); it is called by the document pipeline of the system and takes the clock of a render from the render options. */

        part ganttParser : GanttParser;
        part ganttModel : GanttModel;
        part ganttRenderer : GanttRenderer;
        part timeAxis : TimeAxis;
    }
}
