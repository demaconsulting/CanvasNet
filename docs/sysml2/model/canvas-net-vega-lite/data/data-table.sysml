package CanvasNet {
    part def DataTable {
        doc /* The internal DataTable (a sealed immutable class) with the DataValueKind enumeration, the DataValue readonly struct, and the JsNumber and FieldPath static classes: DataValue holds a null, boolean, number, date, or string with SameValueZero equality (so it can be a dictionary key), a total ordering (null, boolean, number, date, string), TryGetQuantitative (numbers, dates as milliseconds, booleans as 1 and 0, and numeric strings), and ToText (JavaScript String semantics; a date as ISO 8601 in UTC with milliseconds); a date is an instant in UTC held as milliseconds since the epoch (years 1 to 9999, created from milliseconds or a DateTime, never dependent on the host time zone); JsNumber reproduces JavaScript number-to-text conversion with the shortest round-trip digits, parsing, rounding, and radix conversion; FieldPath parses, joins, escapes, and canonicalizes Vega field paths (dotted, bracketed, quoted, and escaped); DataTable keeps its canonical field names in ordinal order and its cells row by row, is built by Create (a field list and rows) or FromRecords (dictionaries, with the union of the keys and null for a missing value) under MaxDataRows and MaxFieldCount (VegaLiteRenderException with the supplied location), and offers IndexOfField, FindField, typed GetValue, GetNumber, GetString, and GetBoolean accessors, GetRow, and Column. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.VegaLite/Data/DataTable.cs, src/DemaConsulting.CanvasNet.VegaLite/Data/NamespaceDoc.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.VegaLite.Tests/Data/DataTableTests.cs */
        comment designRef /* Design: docs/design/canvas-net-vega-lite/data/data-table.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-vega-lite/data/data-table.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-vega-lite/data/data-table.yaml */
    }
}
