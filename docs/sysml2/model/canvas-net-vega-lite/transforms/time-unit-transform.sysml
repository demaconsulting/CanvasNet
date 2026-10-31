package CanvasNet {
    part def TimeUnitTransform {
        doc /* The internal TimeUnitTransform unit (TimeUnitTransform.cs and TemporalFields.cs) of the Transforms subsystem: the row work of the time units. TimeUnitTransform.Apply adds to a table, for each TimeUnitRequest, the start field of the unit of the date of a source field (the Vega timeUnit floor, in the time zone of the chart for a local unit and in UTC for a utc unit) and its end field (one step of the smallest part later), as date values, through the TableLayout of the subsystem, with null in both for a value that is not a valid date; it resolves the maxbins form from the extent of the data in one pass and hands the resolved unit on, checks the added fields and cells against the limits before allocating, charges the work meter per row (more for a zone than for UTC), checks derived names against the string limit, and polls cancellation every 1,024 rows. It serves both the timeUnit transform of a specification (through RowTransforms) and the implicit time unit step of an encoding (through EncodingTransforms). TemporalFields.Collect gives the canonical names of the fields that a specification uses as dates (a temporal type, a timeUnit, the time format type, the timeUnit transform, and the filter and condition predicates with a time unit or a datetime object) so that the loader parses them as dates. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.VegaLite/Transforms/TimeUnitTransform.cs and TemporalFields.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.VegaLite.Tests/Transforms/ (TimeUnitTransformTests and TemporalFieldsTests, with the support class TimeTestPipeline) */
        comment designRef /* Design: docs/design/canvas-net-vega-lite/transforms/time-unit-transform.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-vega-lite/transforms/time-unit-transform.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-vega-lite/transforms/time-unit-transform.yaml */
    }
}
