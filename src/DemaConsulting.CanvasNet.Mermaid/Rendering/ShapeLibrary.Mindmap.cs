// Portions derived from mermaid (MIT); see third-party/README.md
namespace DemaConsulting.CanvasNet.Mermaid.Rendering;

/// <content>
///     The shapes of a mindmap's nodes that no other diagram uses: <c>defaultMindmapNode</c> (a node without brackets)
///     and <c>mindmapCircle</c> (<c>((text))</c>). The mindmap's other shapes (<c>rect</c>, <c>rounded</c>,
///     <c>cloud</c>, <c>bang</c> and <c>hexagon</c>) are the shared ones.
/// </content>
internal static partial class ShapeLibrary
{
    /// <summary>The corner radius of <c>defaultMindmapNode</c>.</summary>
    private const double MindmapCornerRadius = 5;

    /// <summary>
    ///     <c>defaultMindmapNode</c> in the classic look: a rectangle with corners rounded by 5, four times the padding
    ///     wide on each side of the label and half the padding high above and below it, drawn as Mermaid's path with
    ///     quadratic corners (class <c>node-bkg</c>), with a line along its bottom edge (class <c>node-line-</c>, which a
    ///     mindmap's sheet strokes in the section's inverse color) and the label centered.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult DefaultMindmapNode(ShapeInput input)
    {
        const double Rd = MindmapCornerRadius;
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var halfPadding = input.Padding / 2;
        var w = label.Width + (8 * halfPadding);
        var h = label.Height + (2 * halfPadding);

        // The background path, as Mermaid writes it with relative commands
        var path = new PathData().M(-w / 2, (h / 2) - Rd)
            .RelativeL(0, -h + (2 * Rd))
            .RelativeQ(0, -Rd, Rd, -Rd)
            .RelativeL(w - (2 * Rd), 0)
            .RelativeQ(Rd, 0, Rd, Rd)
            .RelativeL(0, h - (2 * Rd))
            .RelativeQ(0, Rd, -Rd, Rd)
            .RelativeL(-(w - (2 * Rd)), 0)
            .RelativeQ(-Rd, 0, -Rd, -Rd)
            .Z();
        draft.Part(path, Body(input.Paint));

        // The line has a class but no style of its own: only the sheet paints it
        var line = input.PaintFor("line").Css;
        draft.Part(new PathData().M(-w / 2, h / 2).L(w / 2, h / 2), new PartPaint(null, line.Stroke, line.StrokeWidth, null), role: "node-line");
        draft.PlaceLabel(label, new ShapePoint(-label.Width / 2, -label.Height / 2));
        return draft.Finish(IntersectRect);
    }

    /// <summary>
    ///     <c>mindmapCircle</c>: <c>circle</c> with the whole node padding as its padding (a circle around the label whose
    ///     radius is half the label's width plus the padding), where a flowchart's circle takes half of it.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult MindmapCircle(ShapeInput input) => Circle(input, input.Padding);
}
