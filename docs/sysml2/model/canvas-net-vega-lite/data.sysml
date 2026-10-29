package CanvasNet {
    part def VegaLiteData {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.VegaLite.Data namespace that puts the rows of a chart into memory, safely, without a side effect: the DataTable unit is the immutable typed value and table model (DataValue with SameValueZero equality, a total ordering of null, boolean, number, and string, a quantitative reading, and a JavaScript-style text form; JsNumber and FieldPath with the Vega field syntax; DataTable with ordinal field order, unique fields, and typed lookups), and the DataLoader unit reads inline values (an array of objects as one row each, an array of primitives as rows of the field data, nested values flattened to leaf fields named by their canonical path) under the MaxDataRows and MaxFieldCount limits (VegaLiteRenderException with the location of the data), polling cancellation every 1,024 rows; every other data source is an empty table that the FeatureCatalog reports, and nothing is opened or fetched. The subsystem is internal; its entry point is called by VegaLiteDocument and its types are read by the scale, guide, and mark builders. As of this release, its constituent units are DataTable and DataLoader. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.VegaLite.Tests/Data/ (the unit test classes DataTableTests and DataLoaderTests) */
        comment designRef /* Design: docs/design/canvas-net-vega-lite/data.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-vega-lite/data.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-vega-lite/data.yaml */

        part dataTable : DataTable;
        part dataLoader : DataLoader;
    }
}
