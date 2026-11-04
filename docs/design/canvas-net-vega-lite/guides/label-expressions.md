### LabelExpressions

![Guides Structure](VegaLiteGuidesView.svg)

The `LabelExpressions` unit is the seventh software unit in the `Guides` subsystem. It is one source file,
`LabelExpressions.cs`, in the `DemaConsulting.CanvasNet.VegaLite.Guides` namespace, and defines one `internal sealed`
class, `LabelExpressions`, and the `internal readonly record struct LabelText`. The file carries the attribution header
comment for the Vega and Vega-Lite behavior it follows. The unit is shared by `AxisBuilder`, by `LegendBuilder` (with its
`LegendBuilder.Symbols.cs` and `LegendBuilder.Gradient.cs` partial files), and by the facet headers of
`VegaLiteDocument` (`VegaLiteDocument.Composition.cs`).

#### Purpose

`LabelExpressions` evaluates the `labelExpr` of axes, legends, and facet headers: the expression that Vega-Lite copies to
the `text` encoding of the labels of the Vega guide. It exists so that the three guides build the datum of an
expression, run it on the expression engine of the call, and coerce the result to label text the same way, and so that
the guides stay free of expression details.

The rules are those of Vega-Lite (`compile/axis/assemble.ts`, `compile/legend/assemble.ts`, and
`compile/header/assemble.ts`, which copy `labelExpr` to the label `text` encode entry; a header replaces `datum.label`
and `datum.value` in the expression by its own members) and of Vega (`vega-encode`: `AxisTicks.js` and `LegendEntries.js`
build the entries the expression reads; `vega-scenegraph`: `text.js` converts the result), re-implemented from their
public description (see _CanvasNetVegaLite System Design_, Third-Party Provenance and Attribution). The unit's behavior
is specified by the `CanvasNetVegaLite-Guides-LabelExpressions-*` requirements in
`docs/reqstream/canvas-net-vega-lite/guides/label-expressions.yaml`.

#### Data Model

**`LabelText`** (readonly record struct) is the result for one label: `Text` (the text; for an array result the lines
joined by a line feed) and `Lines` (the lines of a multi-line label, or null for a single line; the axis, the legends,
and the facet headers all use the lines).

**`LabelExpressions`** (sealed class) holds the `RowExpressions` of the call (or null), the evaluator, the limits, and a
cache of the compiled texts of configured expressions by text. Its constructor takes `(RowExpressions? expressions,
VegaLiteLimits? limits, TimeCalendar? calendar, CancellationToken)`: with the expressions of the call the unit runs on
**the shared evaluator of the call** (the same `MaxEvaluationSteps`, `MaxStringLength`, cancellation token, calendar, and
parameter values as every other expression); a guide built alone (a unit test) gets an evaluator of its own with the same
limits and the calendar given.

**The datum.** `Datum(value, label, index?, tickIndex?, perc?, perc2?)` builds an object value with `value` (the value
the label names), `label` (the text the label has without the expression), and the optional numbers; a member that is
null **does not exist**, so `datum.index` of a header is null, as in Vega. By guide (Vega's `AxisTicks.js` and
`LegendEntries.js`; Vega-Lite's header):

- _Axis_: `value` (the tick value: a number, a category, or a **date** for a time scale), `label`, `index`
  (`i / max(n - 1, 1)`: a **fraction** from 0 to 1 along the ticks), and `tickIndex` (`i`: a whole number).
- _Symbol legend_: `index` (a whole number), `label`, and `value`; the `...N entries` row of `symbolLimit` is an extra
  entry whose `index` is the number of entries shown.
- _Gradient legend_: `index`, `label`, `value` (a date for a temporal field), and `perc` (the position along the
  gradient, 0 to 1).
- _Discrete gradient legend_: as the gradient legend, plus `perc2` (the end of the class along the gradient).
- _Facet header_: `value` (the raw value of the cell) and `label` only.

`datum.size` and `datum.offset` of a symbol legend are null in this engine (a documented deviation below).

**Coercion** (`Evaluate`, after `textLines` and `textValue` of `vega-scenegraph`, since the result goes through the Vega
text mark): a null result is the empty text; an array is **lines**, each element a null (the empty text) or its
JavaScript string conversion, each line trimmed with JavaScript's `trim`; any other result is converted with the
JavaScript string conversion and trimmed. A date converts to its ISO text in this engine (not the JavaScript
`Date.toString`).

**Precedence of the expression** (`Resolve(SpecNode? node, string? configuredText, VegaLiteLocation)`): the expression
of the guide's own definition when it has one (the parser compiled it when the document was opened, so the unit takes it
from `RowExpressions.Compiled`; a text that the parser did not compile is compiled here), otherwise the configured text.

- _Axis_: the `labelExpr` of the axis object, then the existing axis configuration chain (`axisX` or `axisY`,
  `axisBottom`, `axisTop`, `axisLeft`, `axisRight`, `axisBand`, `axisPoint`, `axisQuantitative`, `axisTemporal`, then
  `axis`) that `AxisBuilder` already uses for every other property.
- _Legend_: only the explicit `legend.labelExpr`. **`config.legend.labelExpr` is not read**: Vega-Lite has no such
  configuration property (the feature catalog reports it, see _FeatureCatalog_).
- _Facet header_: `header`, then `headerRow`, `headerColumn`, or `headerFacet`, then `config.header` (the precedence of
  `HeaderSettings`; `HeaderSettings.ExplicitNode` returns the node of the header's own property for the compiled
  expression).

An axis with `labels: false` does not evaluate its expression.

#### Key Methods

- `LabelExpressions(RowExpressions?, VegaLiteLimits?, TimeCalendar?, CancellationToken)` - creates the unit for a call or
  for a guide built alone.
- `Datum(DataValue, string, double?, double?, double?, double?)` - static; builds the datum object above.
- `Resolve(SpecNode?, string?, VegaLiteLocation)` - returns the compiled expression of a guide, or null when the guide has
  none (no node, a node that is not a text, or an empty text). A node that the parser compiled is returned as is;
  otherwise the text is compiled with `ExpressionParser.Parse` **once per instance** (the cache keeps the compiled text,
  so three layout passes and many labels compile it once). A text that is not valid is a `VegaLiteSpecException`.
- `Evaluate(CompiledExpression, DataValue)` - runs the expression on the shared evaluator with the datum and coerces the
  result to a `LabelText` by the rules above. An `ExpressionRuntimeException` (budget, string limit) becomes a
  `VegaLiteRenderException` located at the expression.

**Where the callers apply it.** The unit runs **before the labels are measured, laid out, and thinned**, so everything
downstream (the extent of the axis, the `labelLimit` truncation, the overlap removal, the legend box) sees the replaced
text:

- `AxisBuilder.ApplyLabelExpression` runs after `BuildTicks` and before `Layout`, replacing `AxisTick.Label` and setting
  `AxisTick.Lines`; the label scene item uses the lines when there are some.
- `LegendBuilder` calls `Labeled` for each entry of a symbol legend (the `...N entries` row included) and for each tick
  of a gradient or discrete gradient legend; `MakeLabel` uses the lines.
- `VegaLiteDocument.Composition.WithLabelExpression` wraps the label formatter of a facet header: the wrapper formats the
  cell first (so `datum.label` is the formatted text) and returns the **lines** of the result (`Lines` of an array
  result, so `[datum.label, 'n=' + datum.value]` is a header of two lines; a string result stays one line, even when it
  holds line feeds). The header, footer, and wrapped-facet cell title pass the lines to `TitleBuilder`, which measures
  them, so a multi-line header is taller.

**Preconditions and postconditions**: `Evaluate` and `Datum` arguments are non-null; the result never exceeds
`MaxStringLength` per label (the conversion stops at the limit); the unit changes no state of the guide.

#### Error Handling

- A **syntax error**, an over-long or over-deep expression, or a reference to an undeclared parameter in the expression
  of a specification is a `VegaLiteSpecException` located **at the expression text** when the document is opened
  (`SpecParser.LabelExpressions.cs`, which compiles the `labelExpr` of axes, legends, headers, and of the
  `config.axis*` and `config.header*` entries); a configured text that reaches the unit without being compiled and is
  invalid is the same exception, located at the guide.
- A **runtime failure** is a located `VegaLiteRenderException` at the expression: exhausting the step budget
  (`MaxEvaluationSteps`), a string longer than `MaxStringLength`, an expression that builds too large a value, or an
  invalid number format specifier of `format`.
  Cancellation is an `OperationCanceledException` (the callers poll the token per label as well).
- An unsupported function (`luminance`, `now`), a view-state reference, or an unsupported construct is **not an error
  of this unit**: the expression engine evaluates it to null (so the label is empty) and the feature catalog reports it
  at the text (see _FeatureCatalog_). The number `format(value, specifier)` and `timeFormat` are supported, so
  `format(datum.value, ',.1f')` is a label; an invalid specifier of `format` is a runtime failure (above).

**Limits and budget.** The expression runs on the evaluator of the call, so its steps are charged to the same
`MaxEvaluationSteps` budget as every other expression. The number of evaluations is bounded by the number of labels
(`MaxTicks` per axis, the legend entry limits per legend, the number of cells of a header); three layout passes of an axis
re-evaluate its labels, so the cost is at most three times `MaxTicks` times the steps of one expression, and
exhausting the budget is a located `VegaLiteRenderException`. The label length is bounded by `MaxStringLength` per label.

**Determinism.** The expression engine reads no clock, host zone, or culture; the same specification, data, and
parameters give the same labels on every call and machine.

**Documented deviations from Vega-Lite** (each pinned by a test):

1. **`datum.size` and `datum.offset` of a symbol legend** are null (the renderer does not build the encoded sizes of the
   Vega legend entries).
2. **A date coerces to its ISO text**, not to the JavaScript `Date.toString` text, when an expression returns a date.
3. **A custom `formatType`** is reported and not evaluated, so the interplay of a custom format function with the text
   replacement of `datum.label` is not modeled.
4. **The extra tick of a binned axis** (Vega-Lite adds a tick for the end of the last bin) is not modeled.
5. **`config.legend.labelExpr`** is not read, because Vega-Lite 6 does not read it.

#### Dependencies

- **`RowExpressions`**, **`CompiledExpression`**, **`ExpressionEvaluator`**, **`ExpressionParser`**, and
  **`ExpressionRuntimeException`** - the expression engine of the `Expressions` subsystem (the shared evaluator, the
  compile, the JavaScript string conversion, and `IsJsWhitespace`)
- **`DataValue`**, **`DataObject`**, and **`TimeCalendar`** - the datum members and the calendar of the date functions
  (the `Data` subsystem)
- **`SpecNode`** - the specification value of the `labelExpr` property
- **`VegaLiteLimits`**, **`VegaLiteLocation`**, **`VegaLiteSpecException`**, and **`VegaLiteRenderException`** - the public
  types of the `VegaLiteDocument` unit

#### Callers

`AxisBuilder` (through `ApplyLabelExpression`, with the `RowExpressions` passed to its constructor), `LegendBuilder`
(with its symbol and gradient partial files, through `Labeled`), and the facet-header code of `VegaLiteDocument`
(`WithLabelExpression` in `VegaLiteDocument.Composition.cs`). The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Guides/` (`LabelExpressionsTests.cs`, with `AxisBuilderLabelExprTests.cs`
and `LegendBuilderLabelExprTests.cs` for the guides that use it) call the types directly through `InternalsVisibleTo`.
