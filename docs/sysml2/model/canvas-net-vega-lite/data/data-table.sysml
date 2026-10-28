package CanvasNet {
    part def DataTable {
        doc /* The internal DataTable (a sealed immutable class) with the DataValueKind enumeration, the DataValue readonly struct, and the JsNumber and FieldPath static classes: DataValue holds a null, boolean, number, or string with SameValueZero equality (so it can be a dictionary key), a total ordering (null, boolean, number, string), TryGetQuantitative (numbers, booleans as 1 and 0, and numeric strings), and ToText (JavaScript String semantics); JsNumber reproduces JavaScript number-to-text conversion with the shortest round-trip digits, parsing, rounding, and radix conversion; FieldPath parses, joins, escapes, and canonicalizes Vega field paths (dotted, bracketed, quoted, and escaped); DataTable keeps its canonical field names in ordinal order and its cells row by row, is built by Create (a field list and rows) or FromRecords (dictionaries, with the union of the keys and null for a missing value) under MaxDataRows and MaxFieldCount (VegaLiteRenderException with the supplied location), and offers IndexOfField, FindField, typed GetValue, GetNumber, GetString, and GetBoolean accessors, GetRow, and Column. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.VegaLite/Data/DataTable.cs, src/DemaConsulting.CanvasNet.VegaLite/Data/NamespaceDoc.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.VegaLite.Tests/Data/DataTableTests.cs */
        comment designRef /* Design: docs/design/canvas-net-vega-lite/data/data-table.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-vega-lite/data/data-table.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-vega-lite/data/data-table.yaml */
    }
}
