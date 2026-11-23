package CanvasNetVegaLite_TimeCalendar {
    private import SoftwareItemMetadata::*;

    part def TimeCalendar {
        doc /* The internal TimeCalendar unit (TimeCalendar.cs, TimeCalendar.Zone.cs, TimeCalendar.Intervals.cs, TimeUnits.cs, and TimeDateObject.cs) of the Data subsystem: the calendar of the time phase. TimeCalendar converts an instant (a double of milliseconds since the Unix epoch, exactly the years 1 to 9999 of the DataTable date values; NaN outside, never an exception) to and from the civil date parts of one time zone (UTC when the caller sets none) with integer arithmetic and the ECMAScript MakeDay and MakeTime roll-over, takes the offset of the zone at an instant from its own rules (a fixed-offset zone on a fast path), and converts a wall time to an instant by the ECMAScript rule (a wall time that occurs twice is the earlier instant; one that never occurs is the wall time minus the offset before the gap); it never reads the host time zone, the clock, or the host culture. TimeInterval and TimeIntervalKind are the d3-time intervals millisecond to year (Sunday-based weeks), local and UTC, with floor, ceil, round, offset, count, every, and a bounded range that fails with TimeRangeLimitException and polls cancellation; TimeUnitSpec reads the Vega-Lite time units (the single and multi-part names, the utc and binned prefixes, and the object form), names the derived fields, floors an instant with the Vega timeFloor and utcFloor algorithms (the year 2012 for a unit without a year), gives the end of a unit, its default label specifier, and the duration of its smallest part (DurationMilliseconds, from the year 2001 in UTC or in the time zone of a given calendar) and its one-step reference span (ReferenceSpan, for the offset of a time unit), and resolves the maxbins form with the Vega timeBin; DateTimeObject reads and evaluates the Vega-Lite datetime objects. The unit ports d3-time and Vega algorithms with attribution headers and is read by the TimeFormat unit, the DelimitedTextParser, the DataLoader, the expression functions, the TimeUnitTransform, the scales, the guides, and the marks. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.VegaLite/Data/TimeCalendar.cs",
                "src/DemaConsulting.CanvasNet.VegaLite/Data/TimeCalendar.Zone.cs",
                "src/DemaConsulting.CanvasNet.VegaLite/Data/TimeCalendar.Intervals.cs",
                "src/DemaConsulting.CanvasNet.VegaLite/Data/TimeUnits.cs",
                "src/DemaConsulting.CanvasNet.VegaLite/Data/TimeDateObject.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasNet.VegaLite.Tests/Data/TimeCalendarTests.cs",
                "test/DemaConsulting.CanvasNet.VegaLite.Tests/Data/TimeCalendarZoneTests.cs",
                "test/DemaConsulting.CanvasNet.VegaLite.Tests/Data/TimeCalendarIntervalTests.cs",
                "test/DemaConsulting.CanvasNet.VegaLite.Tests/Data/TimeCalendarEpochDayTests.cs",
                "test/DemaConsulting.CanvasNet.VegaLite.Tests/Data/TimeUnitSpecTests.cs",
                "test/DemaConsulting.CanvasNet.VegaLite.Tests/Data/TimeUnitSpecifierTests.cs",
                "test/DemaConsulting.CanvasNet.VegaLite.Tests/Data/DateTimeObjectTests.cs",
                "test/DemaConsulting.CanvasNet.VegaLite.Tests/Data/TimeTestSupport.cs"
            );
            designFiles = ("docs/design/canvas-net-vega-lite/data/time-calendar.md");
            verificationFiles = ("docs/verification/canvas-net-vega-lite/data/time-calendar.md");
            requirementFiles = ("docs/reqstream/canvas-net-vega-lite/data/time-calendar.yaml");
        }
    }
}
