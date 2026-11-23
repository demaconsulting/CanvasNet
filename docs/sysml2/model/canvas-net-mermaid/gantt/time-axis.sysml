package CanvasNetMermaid_TimeAxis {
    private import SoftwareItemMetadata::*;

    part def TimeAxis {
        doc /* The internal JsDate value, the TimeInterval and TimeScale classes, and the static TimeAxis class (three files): the time arithmetic a gantt chart is built on. JsDate is a JavaScript Date whose local time zone is UTC: a time value in milliseconds or NaN, with the ECMAScript MakeDay, MakeTime, MakeDate, and TimeClip rules, the getters and the setters with their overflow, and the range of plus or minus 8.64e15 milliseconds, wider than DateTime. TimeInterval and the TimeAxis intervals are d3-time's millisecond, second, minute, hour, day, the seven week intervals, month, and year with floor, ceil, offset, range, count, filter, and every (with the special every of milliseconds and years). TimeAxis gives d3-time's tick generator over the 18-row interval table, d3-array's tickStep, min, and max, JavaScript's Math.round, and d3-time-format's timeFormat with the en-US locale and the pad modifiers; TimeScale is d3-scale's time scale with rangeRound, without nice or clamp, as Mermaid uses it. Every function is pure and every value immutable, so the unit is safe for concurrent use. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.Mermaid/Gantt/TimeAxis.cs",
                "src/DemaConsulting.CanvasNet.Mermaid/Gantt/TimeAxis.Scale.cs",
                "src/DemaConsulting.CanvasNet.Mermaid/Gantt/TimeAxis.Format.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasNet.Mermaid.Tests/Gantt/TimeAxisTests.cs",
                "test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/upstream-vectors/time-axis.json"
            );
            designFiles = ("docs/design/canvas-net-mermaid/gantt/time-axis.md");
            verificationFiles = ("docs/verification/canvas-net-mermaid/gantt/time-axis.md");
            requirementFiles = ("docs/reqstream/canvas-net-mermaid/gantt/time-axis.yaml");
        }
    }
}
