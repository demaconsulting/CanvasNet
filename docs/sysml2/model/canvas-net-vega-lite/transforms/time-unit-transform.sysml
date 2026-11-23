package CanvasNetVegaLite_TimeUnitTransform {
    private import SoftwareItemMetadata::*;

    part def TimeUnitTransform {
        doc /* The internal TimeUnitTransform unit (TimeUnitTransform.cs and TemporalFields.cs) of the Transforms subsystem: the row work of the time units. TimeUnitTransform.Apply adds to a table, for each TimeUnitRequest, the start field of the unit of the date of a source field (the Vega timeUnit floor, in the time zone of the chart for a local unit and in UTC for a utc unit) and its end field (one step of the smallest part later), as date values, through the TableLayout of the subsystem, with null in both for a value that is not a valid date; it resolves the maxbins form from the extent of the data in one pass and hands the resolved unit on, checks the added fields and cells against the limits before allocating, charges the work meter per row (more for a zone than for UTC), checks derived names against the string limit, and polls cancellation every 1,024 rows. It serves both the timeUnit transform of a specification (through RowTransforms) and the implicit time unit step of an encoding (through EncodingTransforms); TimeUnitRequest.EndOnly serves a binned unit of an encoding (Vega-Lite's timeOffset of the field as it is: only the end field is added, from the unfloored value), and the transform floors a binned unit like any other (Vega's timeunit ignores binned). TemporalFields.Collect gives the canonical names of the fields that a specification uses as dates (a temporal type, a timeUnit, the time format type, the timeUnit transform, and the filter and condition predicates with a time unit or a datetime object) so that the loader parses them as dates. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.VegaLite/Transforms/TimeUnitTransform.cs",
                "src/DemaConsulting.CanvasNet.VegaLite/Transforms/TemporalFields.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasNet.VegaLite.Tests/Transforms/TimeUnitTransformTests.cs",
                "test/DemaConsulting.CanvasNet.VegaLite.Tests/Transforms/TemporalFieldsTests.cs",
                "test/DemaConsulting.CanvasNet.VegaLite.Tests/Transforms/TimeTestPipeline.cs"
            );
            designFiles = ("docs/design/canvas-net-vega-lite/transforms/time-unit-transform.md");
            verificationFiles = ("docs/verification/canvas-net-vega-lite/transforms/time-unit-transform.md");
            requirementFiles = ("docs/reqstream/canvas-net-vega-lite/transforms/time-unit-transform.yaml");
        }
    }
}
