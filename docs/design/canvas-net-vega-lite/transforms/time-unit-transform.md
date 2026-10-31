### TimeUnitTransform

![Transforms Structure](VegaLiteTransformsView.svg)

The `TimeUnitTransform` unit is the fourth software unit in the `Transforms` subsystem. It comprises two source files in
the `DemaConsulting.CanvasNet.VegaLite.Transforms` namespace, both `internal`:

- `TimeUnitTransform.cs` - `TimeUnitRequest` (sealed record: the field, the unit, and the two output names) and
  `TimeUnitTransform` (static class: the row transform and the `maxbins` pre-pass); the file carries the attribution
  header comment for the Vega `timeunit` transform it re-implements
- `TemporalFields.cs` - `TemporalFields` (static class): the collector of the fields that are read as dates; the file
  carries the attribution header comment for the Vega-Lite implicit `parse` rules it follows

The unit also hands the work of the encoding to the `EncodingPlan` unit (its `TimeUnitPlan` and
`EncodingData.TimeUnits`, described below); those two types are specified by _EncodingPlan_.

#### Purpose

`TimeUnitTransform` computes, for every row, the **start** and the **end** of the time unit that contains a date: the
`timeUnit` transform of the specification (`{"timeUnit": "yearmonth", "field": "date", "as": "ym"}`) and the same
computation that the encoding asks for when a channel has a `timeUnit`. `TemporalFields` finds the fields of a view that
Vega-Lite parses as dates even when `format.parse` does not name them, so that a text such as `2024-03-05` becomes a
time. It exists so that a time unit has one definition (the floor and the end come from `TimeUnitSpec` in the calendar
of the chart, see _TimeCalendar_), one accounting of its work, and one set of limits, whether the unit was written as a
transform or implied by an encoding.

The unit's behavior is specified by the `CanvasNetVegaLite-Transforms-TimeUnitTransform-*` requirements in
`docs/reqstream/canvas-net-vega-lite/transforms/time-unit-transform.yaml`. The behavior rules are those of the Vega
`timeunit` transform and the Vega-Lite time unit, re-implemented from their public description (see _CanvasNetVegaLite
System Design_, Third-Party Provenance and Attribution).

#### Data Model

The class is static; one call serves one thread. A call works on an immutable `DataTable` and returns a new one built
with `TableLayout` (see _RowTransforms_ for the layout rules: ordinal field order, an added field replaces a field of
the same name and the fields nested below it, `MaxFieldCount` and the 2^25 cells checked before allocation).

**`TimeUnitRequest`** is one unit to compute: `Field` (the field path of the dates as written), `StartName` and
`EndName` (the literal names of the two fields that receive the start and the end, the end exclusive), `Spec` (the
`TimeUnitSpec`, whose `maxbins` form is resolved against the data), and `Node` (the node of the specification that asked
for the unit, the location of a failure when the call has none of its own). The `timeUnit` transform of the
specification gives the request `as` and `as_end` (the end name is the start name with `_end` appended); the encoding
gives the names of its plan (`yearmonth_date` and `yearmonth_date_end`).

**What a row gets.** The dates are those of the calendar of the chart (`ExpressionFunctionContext.Calendar`, the zone of
`VegaLiteRenderOptions.TimeZone`, UTC by default), except for a `utc` unit, which uses UTC. The value of a row is read as
a date when it is a date or a finite number (milliseconds since the epoch, truncated); null, text, a boolean, and a date
that the calendar cannot place (outside the years 1 to 9999) give **null in both fields**. The start is `TimeUnitSpec.Floor`
(Vega's `timeFloor` or `utcFloor`) and the end is `TimeUnitSpec.End` (one step of the smallest part later; a day unit is
23, 24, or 25 hours long across a daylight saving change); both are written as dates, and an end that falls outside the
range is null with a valid start. A **`binned` unit does not floor**: the value is the start and the end follows it one
step later (Vega-Lite's behavior; the encoding skips a binned unit and reports `vl-timeunit-binned`).

**The `maxbins` pre-pass.** A unit written as `{"maxbins": 10}` names no unit, so it is resolved first: one pass over the
rows finds the extent of the valid dates, and `TimeUnitSpec.ResolveMaxBins` (Vega's `timeBin`) chooses the unit and the
step for it. A table without a valid date (or without rows) has nothing to bin and gets null fields. `maxbins` above
`MaxScaleDomainSize` is a located error before the pass. The resolved unit of each request can be returned to the caller
(the `resolved` parameter), which is how the encoding keeps it.

**Accounting.** Every row is charged to the step budget of the evaluator through a `WorkMeter` (one step per 64 elements;
see _AggregateTransforms_), **2 elements per request and row for a UTC calendar and 8 for a zone** (whose offset lookups
cost more), and 1 or 2 per row for the `maxbins` extent pass. The token is polled through `TransformContext.Tick` every
1,024 rows, in the extent pass as well. The output names go through `TransformContext.CheckText`
(`MaxStringLength`).

**`TemporalFields.Collect(view)`** returns the canonical names of the fields that are read as dates, in one pass over
the specification (nothing is proportional to the data): a field of the encoding whose definition has the type
`temporal`, has a `timeUnit`, or asks for the format type `time` (on the definition, its axis, or its legend); the
secondary channels `x2`, `y2`, `theta2`, and `radius2`, which take the type of their main channel; the field of a
condition, which is judged by the definition of the condition itself; arrays of definitions; the `field` of a `timeUnit`
transform; and the field of a predicate (of a `filter` or of the `test` of a condition, also inside `and`, `or`, and
`not`, walked with an explicit stack so the nesting cannot overflow the call stack) that has a `timeUnit` or is compared
with a date-time object. A field that a transform derives (an `as`) is not read from the data, so it is not collected,
and a `sort` field has no known type and is not collected. The loader parses the collected fields of the **main** data
of the view only (never a lookup source), after the explicit `format.parse` entries, which win, and `parse: null`, which
suppresses them (see _DelimitedTextParser_ and _DataLoader_).

**The encoding hand-off.** `EncodingPlan` plans a `TimeUnitPlan` per field and unit (shared by every channel that uses
the same pair): `StartName`, `EndName` (the start name with `_end`), `SourceField`, `Spec`, `Node`, and `HasBandEnd`
(whether a channel spans its unit, that is, a bar-like mark on a position channel without a secondary channel). The
names follow `TimeUnitSpec.FieldName` (`yearmonth_date`, `month_step_2_date`, `utcmonth_date`,
`timeunit_maxbins_10_date`). `EncodingTransforms.Execute` runs bin, then **time unit**, then aggregate, then stack, by
calling `TimeUnitTransform.Apply` once with all the requests, and stores the resolved units in
`EncodingData.TimeUnits` (start name to `TimeUnitSpec`, with `maxbins` resolved), which `ScaleResolver` (the domain
and the tick step), `AxisBuilder` (the label specifier), and `MarkBuilder` (the band geometry) read. The aggregate
groups by the end field as well as the start for a channel that spans its unit, so that the end survives the
aggregate; the end follows from the start, so it adds no groups.

**Limits** (every loop or allocation of the unit):

- _Rows read_: at most `MaxDataRows` (already bounded by the loader); each row is charged
  `WorkMeter.Add(2` for UTC or `8` for a zone, per request`)` and the token is polled through `Tick` every 1,024 rows.
- _Added fields_: two per request, through `TableLayout` (`MaxFieldCount` and 2^25 cells), checked before allocation.
- _`maxbins` extent pass_: one pass over the rows with `maxbins` at most `MaxScaleDomainSize`; each row is charged
  `WorkMeter.Add(1` for UTC or `2` for a zone`)` and the token is polled every 1,024 rows.
- _Output names_: at most `MaxStringLength` characters, through `TransformContext.CheckText` before any output.
- _`TemporalFields.Collect`_: one pass over the specification, with an explicit stack for predicates.

**Determinism.** The unit uses only the calendar of the call and the data; no clock, zone, culture, or random source.
The same specification and data give the same rows on every call, thread, machine, and platform, and under any
culture.

#### Key Methods

- `TimeUnitTransform.Apply(DataTable, IReadOnlyList<TimeUnitRequest>, TransformContext, VegaLiteLocation,
  TimeUnitSpec[]?)` - adds the start and end fields of every request to every row (the rows keep their order), resolves
  the `maxbins` forms first, and optionally reports the resolved unit of each request.
- `TemporalFields.Collect(SpecNode)` - the canonical names of the date fields of a view (its encoding, its `timeUnit`
  transforms, and its predicates).

**Preconditions and postconditions**: the input table, the requests, and the context are non-null; on return the table is
complete and within the limits, or an exception was thrown.

#### Error Handling

Everything that depends on the data and exceeds a limit is a `VegaLiteRenderException` located at the call
(`location`): `MaxFieldCount` and the 2^25 cell bound (checked before the output is allocated), the step budget (the
`ExpressionRuntimeException` of the meter is converted with the same message), a `maxbins` above
`MaxScaleDomainSize`, and an output name longer than `MaxStringLength`. A value that cannot be placed is null, not an
error, and nothing here produces a warning. A canceled token is an `OperationCanceledException`, never wrapped. A
malformed time unit is not an error of this unit: `SpecParser` (`SpecParser.Time.cs`) rejected it when the document was
opened, and a member that a unit does not define is skipped here. A null argument is an `ArgumentNullException`. The
unit performs no I/O.

**Documented deviations from Vega and Vega-Lite** (each pinned by a test):

1. **Range**: a date outside the years 1 to 9999 is null (the NaN policy of _TimeCalendar_).
2. **Aggregate group-by includes the end** of a channel that spans its unit, where Vega-Lite groups by the start
   only, because the end field would otherwise vanish after the aggregate.
3. **A binned unit** in the transform gives the start and an end one step later; in an encoding it is skipped and
   reported.
4. **`maxbins` of an empty or dateless table** gives null fields (Vega would give NaN steps).

#### Dependencies

- **`TimeUnitSpec`**, **`TimeCalendar`**, and **`DataValue`** - the unit model, the calendar, and the date values
  (the `TimeCalendar` and `DataTable` units)
- **`TableLayout`** and **`FieldReader`** - the layout, bounds, and field reading of derived tables, and
  **`TransformContext`** (`Tick`, `CheckText`, `Locate`) - the `RowTransforms` unit
- **`WorkMeter`** - the proportional step charge (the `AggregateTransforms` unit)
- **`ExpressionFunctionContext`** and **`ExpressionRuntimeException`** - the calendar, the step budget, and the failure
  that is converted (the `Expressions` subsystem)
- **`SpecNode`**, **`FieldPath`**, and **`ChannelDefinitions`** - the specification tree, the canonical field names, and
  the conditions of a definition (the `SpecModel` and `DataTable` units)
- **`VegaLiteLimits`**, **`VegaLiteLocation`**, and **`VegaLiteRenderException`** - the public types of the
  `VegaLiteDocument` unit

#### Callers

`RowTransforms.Apply` dispatches the `timeUnit` transform to `TimeUnitTransform.Apply` with one request, and
`EncodingTransforms` calls it with the requests of the plan of the view (see _EncodingPlan_); `ScaleResolver`,
`AxisBuilder`, and `MarkBuilder` read the resolved units it leaves in `EncodingData.TimeUnits`. `VegaLiteDocument` calls
`TemporalFields.Collect` for the drawn view and gives the result to the `DataLoader` as the implicit date fields. The
tests in `test/DemaConsulting.CanvasNet.VegaLite.Tests/Transforms/` (`TimeUnitTransformTests.cs` and
`TemporalFieldsTests.cs`, with `EncodingPlanTimeTests.cs`, `EncodingTransformsTimeTests.cs`, and
`RowTransformsTimeTests.cs` for the hand-off) call the types directly through `InternalsVisibleTo`.
