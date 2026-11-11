package CanvasNet {
    part def FlattenCommand {
        doc /* The flatten command: writes a copy of an SVG drawing in which every image link that only CanvasNet can draw (a Mermaid diagram, an OpenSCAD model, a Vega-Lite chart, a page of a PDF, PPTX, or VSDX document, or a nested SVG file that links one) is a PNG picture of its placement size, so any SVG reader shows it. It declares its options (output, scale, and the strict and no-clobber options of render), probes the one input, calls the SvgFlattener of the Viewer library, and reports the result as one text line (the output, its size, and each replaced link) with the notes on standard error, or as one JSON document with the input, the scale, flattened, the replaced and rebased links, the output, the diagnostics, and the echo. When there is nothing to flatten it writes nothing and exits with code 0. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Tool/Commands/FlattenCommand.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tool.Tests/Commands/FlattenCommandTests.cs */
        comment designRef /* Design: docs/design/canvas-net-tool/commands/flatten-command.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-tool/commands/flatten-command.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-tool/commands/flatten-command.yaml */
    }
}
