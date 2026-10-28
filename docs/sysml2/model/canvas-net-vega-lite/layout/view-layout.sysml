package CanvasNet {
    part def ViewLayout {
        doc /* The internal static ViewLayout with the ViewPadding, ViewExtents, ViewSizeHints, AutosizeType, ViewStyle, ViewLayoutRequest, ViewMeasure, and ViewLayoutResult types: Resolve reads the size, padding, autosize, backgrounds, and view style of a single view from the specification and the configuration (the data rectangle is the number in width and height, the container size for container, the size computed from the step of a discrete scale, or the configured continuous size of 300 by 300; padding is a number or an object with missing sides 0, then the configuration, then 5; the background is the caller's override, the specification's, the configuration's, or white; the plot style comes from the cell style and view configuration), Measure computes the final data rectangle, places the TitleBlock, unites the bounds of the guides and the title, and computes the canvas size (pad grows the canvas by the whole pixels the bounds reach; none uses the given size plus the padding, or exactly the given size when contains is padding) and the origin of the data rectangle, Build assembles the Scene (a root group holding the view group with the plot background and border, the content in the given order, then the title), and Compute does both. A negative or missing size or padding is clamped or defaulted, an autosize of fit is laid out as pad (the FeatureCatalog has reported it), and the canvas size is clamped to the range of an integer; the check against Surface.MaxDimension is made by VegaLiteDocument. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.VegaLite/Layout/ViewLayout.cs, src/DemaConsulting.CanvasNet.VegaLite/Layout/NamespaceDoc.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.VegaLite.Tests/Layout/ViewLayoutTests.cs */
        comment designRef /* Design: docs/design/canvas-net-vega-lite/layout/view-layout.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-vega-lite/layout/view-layout.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-vega-lite/layout/view-layout.yaml */
    }
}
