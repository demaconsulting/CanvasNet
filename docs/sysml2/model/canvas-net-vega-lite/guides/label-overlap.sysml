package CanvasNetVegaLite_LabelOverlap {
    private import SoftwareItemMetadata::*;

    part def LabelOverlap {
        doc /* The internal static LabelOverlap, an own C# implementation of the Overlap transform of Vega, extracted unchanged from the AxisBuilder unit so that the axes and the gradient legends share it: Hidden finds the labels that are hidden because they overlap their neighbors, taking only labels whose box is wider and taller than a pixel, doing nothing for fewer than three labels or when no two neighbors are closer than the separation, and otherwise repeating the parity method (keep the labels at even positions) or the greedy method (hide each label that overlaps the last one kept) until nothing overlaps or fewer than three labels are left, then keeping the last label of the guide visible by swapping it for the last label that stayed; the cancellation token is polled before each round; FromSetting reads a labelOverlap setting (true is parity, the names parity and greedy, anything else none). The unit is stateless and bounded by the number of labels its callers have already bounded. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet.VegaLite/Guides/LabelOverlap.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.VegaLite.Tests/Guides/LabelOverlapTests.cs");
            designFiles = ("docs/design/canvas-net-vega-lite/guides/label-overlap.md");
            verificationFiles = ("docs/verification/canvas-net-vega-lite/guides/label-overlap.md");
            requirementFiles = ("docs/reqstream/canvas-net-vega-lite/guides/label-overlap.yaml");
        }
    }
}
