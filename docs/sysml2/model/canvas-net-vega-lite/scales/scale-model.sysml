package CanvasNet {
    part def ScaleModel {
        doc /* The internal ScaleModel (a sealed immutable class) and the ScaleKind enumeration (Linear, Band, Point, Ordinal): the pure arithmetic of scales with no knowledge of the specification, a port of the d3-scale behavior with attribution. CreateLinear, CreateBand, CreatePoint, and CreateOrdinal build the models; Map maps a number or data value (a linear value through normalize and interpolate with clamp, round, reversed domain or range, and d3 piecewise (polymap and bimap) mapping for a domain and range of more than two entries, using min(domain length, range length) entries; a band or point value to the start of its band or its position; null for a value outside the domain), MapCenter gives the center of a band, MapOrdinal cycles through the range, Invert and IndexOf give the inverse mappings, and BandSpace is Vega's bandspace rule. A linear domain of zero width maps to the middle of the range, NaN maps to NaN, and calling an operation of another kind of scale is an InvalidOperationException. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.VegaLite/Scales/ScaleModel.cs, src/DemaConsulting.CanvasNet.VegaLite/Scales/NamespaceDoc.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.VegaLite.Tests/Scales/ScaleModelTests.cs */
        comment designRef /* Design: docs/design/canvas-net-vega-lite/scales/scale-model.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-vega-lite/scales/scale-model.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-vega-lite/scales/scale-model.yaml */
    }
}
