package CanvasNet {
    part def FeatureCatalog {
        doc /* The internal static FeatureCatalog with the FeatureReport record and the FeatureReportKind enumeration: a pure function of the parsed SpecTree that lists every construct of a Vega-Lite specification that the renderer recognizes but does not implement, each with a stable lower-case vl- identifier (vl-schema-version, vl-data-*, vl-mark-type, vl-channel-name, vl-type-*, vl-scale-*, vl-encoding-*, vl-guide-*, vl-format-type-custom, vl-expr-*, vl-transform-name, vl-composition-*, vl-param-*, vl-projection, vl-layout-autosize-*) and the exact location, sorted by line and column with a stable sort. It is the single definition of the supported boundary: supported marks, channels, scale types, scale properties, and data sources are absent from its result. It classifies a data url as remote (a scheme, drive letter, absolute path, or network path: vl-data-remote-url, never fetched) or local (vl-data-file), chooses a data source by precedence rather than key order, and reports an empty or sourceless data object as the warning vl-data-empty; it opens no file, reads no data rows, and uses no network. The warnings vl-duplicate-key and vl-unknown-property are produced by the SpecParser unit. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.VegaLite/SpecModel/FeatureCatalog.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.VegaLite.Tests/SpecModel/FeatureCatalogTests.cs */
        comment designRef /* Design: docs/design/canvas-net-vega-lite/spec-model/feature-catalog.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-vega-lite/spec-model/feature-catalog.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-vega-lite/spec-model/feature-catalog.yaml */
    }
}
