package CanvasNet {
    part def TimeFormat {
        doc /* The internal TimeFormat unit (TimeFormat.cs, TimeFormat.Format.cs, TimeFormat.Parse.cs, TimeFormat.Multi.cs, JsDateParser.cs, and JsDateParser.Legacy.cs) of the Data subsystem: the text of dates, over the TimeCalendar unit and independent of the host culture. TimeFormat.Compile builds a TimeFormatter for a d3-time-format 4.1.0 specifier (all directives with the English locale, the padding flags, the week-number and ISO week directives, the zone offset, and the composite directives; an unknown directive copies its letter, an invalid instant formats as Invalid Date) and Format stops with TimeFormatTooLongException when the output would exceed the length the caller passes; TimeFormat.Multi is the Vega multi-format that labels a time axis without a format (with the override of each level); TimeFormat.TryParse is d3 timeParse and utcParse (windowed number reads, case-insensitive names, no range validation, the whole text consumed, text beyond 256 characters refused, never throwing); JsDateParser is the JavaScript Date.parse (the ECMAScript date-time string format, in which a date alone is UTC and a date-time without an offset is a time of the calendar zone, plus a documented subset of the legacy forms; text beyond 128 characters refused before any scan; no DateTime.Parse and no regular expression). The unit ports d3-time-format and Vega algorithms with attribution headers. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.VegaLite/Data/TimeFormat.cs, TimeFormat.Format.cs, TimeFormat.Parse.cs, TimeFormat.Multi.cs, JsDateParser.cs, and JsDateParser.Legacy.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.VegaLite.Tests/Data/ (TimeFormatTests, TimeFormatParseTests, TimeFormatMultiTests, and JsDateParserTests) */
        comment designRef /* Design: docs/design/canvas-net-vega-lite/data/time-format.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-vega-lite/data/time-format.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-vega-lite/data/time-format.yaml */
    }
}
