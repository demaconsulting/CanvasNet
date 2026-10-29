package CanvasNet {
    part def VegaLiteData {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.VegaLite.Data namespace that puts the rows of a chart into memory, safely, and never uses the network: the DataTable unit is the immutable typed value and table model (DataValue with SameValueZero equality, a total ordering of null, boolean, number, date, and string, a quantitative reading, and a JavaScript-style text form; dates as UTC instants; JsNumber and FieldPath with the Vega field syntax; DataTable with ordinal field order, unique fields, and typed lookups); the DataLoader unit chooses the source of a view - inline values, a relative local url, a named dataset (the caller's before the specification's), or a sequence - and builds its table under the MaxDataRows (a total per call), MaxFieldCount, and string limits; the DataFileResolver unit finds and reads local data files inside a sandbox (base directory and data paths, or the caller's callback; no link followed; strict bounded UTF-8; per-call cache; MaxDataFiles and MaxDataBytes; an existing file that cannot be opened is unreadable, never missing); the DelimitedTextParser unit reads CSV, TSV, and DSV and applies the parse conversions (type inference only for an explicit parse of auto, as in Vega; dates only within the years 1 to 9999). A local file or named dataset that cannot be used, or JSON text that is not valid Unicode, is a VegaLiteRenderException located at the data; remote URLs and the other recognized sources are an empty table that the FeatureCatalog reports. The subsystem is internal; one DataLoader is created by VegaLiteDocument per measure or render call and its types are read by the scale, guide, and mark builders. As of this release, its constituent units are DataTable, DataLoader, DataFileResolver, and DelimitedTextParser. Phase 5 adds composite cell values (the immutable array, object, and regular expression values that expression results and derived rows hold, compared by identity), the loading of the secondary source of a lookup transform through the same per-call DataLoader, and the derived-row reservation of fold and flatten against the same MaxDataRows budget (the Transforms subsystem is the client). */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.VegaLite.Tests/Data/ (the unit test classes DataTableTests, DataCompositeValueTests, DataLoaderTests, DataFileResolverTests, DataFileResolverLinkTests, and DelimitedTextParserTests) */
        comment designRef /* Design: docs/design/canvas-net-vega-lite/data.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-vega-lite/data.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-vega-lite/data.yaml */

        part dataTable : DataTable;
        part dataLoader : DataLoader;
        part dataFileResolver : DataFileResolver;
        part delimitedTextParser : DelimitedTextParser;
    }
}
