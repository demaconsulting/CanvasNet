package CanvasNet {
    part def Positions {
        doc /* Feature-position subsystem (namespace DemaConsulting.CanvasNet.Positions): describes where the features of a rendered picture are (a node of a diagram, a bar of a chart), so that a caller can place a callout or a leader line on the picture without probing its pixels. It exposes the ImagePosition unit (the immutable position of one feature: a lowercase kind, an id, optional facts that tell it apart, typed scalar values, a geometry box without stroke, and an attachment pixel, all in the continuous pixel coordinates of the picture, with the PositionValue, PositionValueKind, PositionBox, and PositionPoint value types), the PositionSelector unit (the bounded selector grammar - all, a kind, a kind with an id glob, a quoted exact id, or typed predicates on channel values and on the @id, @view, @from, @to, and @index facts - and PositionSelection, the union of several selectors), and the PositionCollector unit (keeps the positions of one render that a selection matches, each once, in delivery order, up to a cap of 1,000 applied after the selection, and names the selectors that matched nothing). The subsystem has no global state; the model types are immutable and thread-safe, and a collector belongs to one render. Positions depends on the ImageSources subsystem only (ImageParameterValues reads a bare predicate value like a --param value); the CanvasNetMermaid and CanvasNetVegaLite systems produce positions through the PositionHandler of their render options, and the CanvasNetViewer and CanvasNetTool systems collect and report them. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/Positions/ImagePositionTests.cs, test/DemaConsulting.CanvasNet.Tests/Positions/PositionSelectorTests.cs, test/DemaConsulting.CanvasNet.Tests/Positions/PositionCollectorTests.cs */
        comment designRef /* Design: docs/design/canvas-net/positions.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/positions.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/positions.yaml */

        part imagePosition : ImagePosition;
        part positionSelector : PositionSelector;
        part positionCollector : PositionCollector;

        dependency usesImageSources from Positions to ImageSources;
        dependency selectorUsesPosition from positionSelector to imagePosition;
        dependency collectorUsesSelector from positionCollector to positionSelector;
        dependency collectorUsesPosition from positionCollector to imagePosition;
    }
}
