package CanvasNet {
    part def DataLoader {
        doc /* The internal static DataLoader: Load reads the data of a view from its data property and returns a DataTable. Only inline values given as an array are read (an array of objects gives one row per object; an array of primitives gives one row per value in the field data; JSON types are kept; a nested object or array is flattened to leaf fields named by their canonical path, and a field a row lacks is null); every other source (values given as text, any url, a name, a sequence, or no source) is the empty table and never an error, because the FeatureCatalog reports it (vl-data-empty, vl-data-remote-url, vl-data-file, vl-data-named, vl-data-sequence, vl-data-values-text). It never opens a file and never uses the network. More rows than MaxDataRows (checked before any row is converted) or more fields than MaxFieldCount in a row throws a VegaLiteRenderException with the location of the offending data, and cancellation is checked every 1,024 rows. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.VegaLite/Data/DataLoader.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.VegaLite.Tests/Data/DataLoaderTests.cs */
        comment designRef /* Design: docs/design/canvas-net-vega-lite/data/data-loader.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-vega-lite/data/data-loader.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-vega-lite/data/data-loader.yaml */
    }
}
