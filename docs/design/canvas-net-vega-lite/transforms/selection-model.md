### SelectionModel

![Transforms Structure](VegaLiteTransformsView.svg)

The `SelectionModel` unit is a software unit in the `Transforms` subsystem. It is four source files in the
`DemaConsulting.CanvasNet.VegaLite.Transforms` namespace, all `internal`: `SelectionModel.cs` (the `SelectionBind`
enumeration, the `SelectionComponent` class, and the `SelectionModel` sealed partial class with the stores, the
values, and the scan `Declares`), `SelectionModel.Projection.cs` (`Create`, the projection, and the reading of the
initial value), `SelectionModel.Queries.cs` (the binding to the units that are drawn and what the later stages ask of the
model: the selection a view sees, the selection bound to a scale, and the extent of a scale or a bin) and
`SelectionTimeUnits.cs` (the time units that a predicate on a selection needs computed first). The helpers that
decide which selection applies to which unit view (`SelectionDeclarations`, with
`Assign` and `UnitNameOf`) are in the `SpecModel` namespace and are documented with the `SpecParser` unit; the tuples,
the stores, and the tests and resolution of a selection (`SelectionEntry`, `SelectionStore`, `SelectionTestSemantics`) are
in the `Expressions` namespace and are documented with the `ExpressionFunctions` unit.

#### Purpose

A selection parameter is the state of a chart that a viewer would change. A static render never receives an event, so the
chart is the state that Vega-Lite's compiled specification holds before anything happens: the `value` of the selection
(or nothing, an empty selection) as a store of tuples, and the value of the signal that Vega-Lite derives from the store.
`SelectionModel` computes exactly that state, once per call, so that the predicates of the conditions and filters, the
expressions that read a selection, the scales, the bins, and the marks (the brush) all see one and the same
state.

The unit's behavior is specified by the `CanvasNetVegaLite-Transforms-SelectionModel-*` requirements in
`docs/reqstream/canvas-net-vega-lite/transforms/selection-model.yaml`.

#### Data Model

**`SelectionComponent`** (sealed class with `init` properties) is one selection as one unit view declares it: `Name` and
`VariableName` (Vega-Lite's `varName`, which names the signal and, followed by `_store`, the data set), `Type` (`point`
or
`interval`), `Resolve` (`global`, `union`, or `intersect`), `Declaration` (the parameter object or the entry of the legacy
`selection` property), `UnitIndex` and `UnitName` (the position of the unit view in the layer tree and its name),
`Projection` (a list of `SelectionTupleField`: the field, the channel it came from, and the tuple type `E`, `R`, or `R-RE`),
`TimeUnits` (the `TimeUnitRequest` of each projected field that a time unit derives), `Init` (one list of values per
initial value, one value per projected field), `Bind` (`SelectionBind`: none, widget, legend, or scales), `Mark` (the
`mark` of the brush of an interval selection: `config.selection.interval.mark` with the `mark` of the declaration over
it, property by property; an `{"expr": ...}` property is not read (Vega-Lite's schema gives `BrushConfig` plain values
only, and the feature catalog reports it as `vl-expr-ref`): a reference in the configuration gives the default of
that property (`#333`, 0.125, white), and one in the declaration leaves the configured value in place; null for a point
selection), `IsScaleBound` (an interval selection resolved globally with
`bind: "scales"`: Vega-Lite's `scales.defined`), `IsTopLevelView` (the unit is the whole view or a layer of a layered
view that is the whole view), `IsDropped`, and `UsesRowIdentity` (a projection over `_vgsid_`).

**`SelectionModel`** (sealed partial class) holds `Components` (one per unit view that a selection applies to, in
document order), `Stores` (by data set name, such as `brush_store`), `Values` (the resolved value of each selection by
variable name), and `Warnings` (`vl-param-selection-projection` and `vl-param-selection-id`, as `FeatureReport` records
of kind warning). `SelectionModel.Empty` is the model of a specification that declares no selection. The model is
immutable and safe to share; creating it reads no data. `BindUnits` returns a copy bound to the layer tree that is drawn
(see below).

#### Key Methods

- `Declares(tree)` - tells, from the view objects only, whether the specification declares a selection (a parameter with
  `select`, or an entry of the legacy `selection`); a specification without one never builds a model.
- `Create(tree, layers, config, overrides, calendar, context)` - for each assignment that `SelectionDeclarations.Assign`
  gives (a selection of a unit view for that unit; a top-level selection of a composition for every unit or for those that
  `views` names; a selection that two or more layers of one layered view receive is marked as a duplicate), it
  1. reads the initial value: the value of the caller when `overrides` names the selection (converted with
     `ParameterEnvironment.FromCaller`, the dates as milliseconds; a null clears it), else the `value` (or the `init` of
     a
     legacy selection) of the specification, in which a date-time object of a member is the number of milliseconds of the
     instant in `calendar`; a value that is not an array is a list of one value;
  2. projects the selection (`Project`, a port of Vega-Lite's `project.ts`): an explicit `fields` and `encodings`; else
     the
     keys of the initial value (a channel name is an encoding, any other key a field of a point selection; an interval
     initialized with another key reports `vl-param-selection-projection` and takes the default encodings); else the defaults
     of the selection type (`config.selection.<type>`, falling back to `x` and `y` for an interval and `_vgsid_` for a point).
     A channel without a field definition or with an aggregate is skipped with a warning; a channel with a time unit
     projects its derived field (`<unit>_<field>`); a field is projected once; the tuple type is `R` for an interval
     over a
     channel whose scale has a continuous domain (`ScaleResolver.HasContinuousDomain`), `R-RE` for a binned field, else
     `E`;
  3. maps each initial value to a tuple: the value of the channel (or of the field) of a mapping, or the scalar itself,
     for
     every projected field.
  The store of a name comes from its first declaration: its tuples are `SelectionEntry` records (unit, fields, values);
  a dropped selection and a projection over `_vgsid_` (row identity, not modeled) have none, and a value that the caller
  gave for the latter is reported as `vl-param-selection-id`. The resolved value of each selection is
  `SelectionTestSemantics.Resolve(store, resolve == "global" ? "union" : resolve, type == "point", true, context)`,
  which is
  the signal that Vega-Lite compiles (`vlSelectionResolve`). A selection bound to the scales of a unit that is not the
  whole view (`IsScaleBound` and not `IsTopLevelView`, with a projected channel over a continuous scale) has its
  top-level signal rebuilt by Vega-Lite from the signals of the scales, which hold nothing in a static chart: its value
  is the empty object, while its store keeps its tuple.
- `Find(variableName)` - the component of the first declaration of a name.
- `BindUnits(tree)` - the model bound to the layer tree that is drawn. The model is built from the specification as
  written, the tree that is drawn from the folded one; both have the same units in the same order, so the units are
  matched by position (`LayerNode.Template` lets a copy of a facet cell find its template).
- `ComponentsOf(unit)` - the components that a unit declares, in declaration order (the order Vega-Lite draws the brushes
  in).
- `FindComponent(view, variableName)` - Vega-Lite's `getSelectionComponent`: the selection of the view itself, else
  that of
  the nearest view above it, where a view that holds other views has the selections of its units and the last unit that
  declares the name decides.
- `BoundExtentOf(unit, channel)` - the selection bound to the scale of a channel (`IsScaleBound`, projected over the channel
  with the tuple type `R`; the last such selection wins) and its field.
- `ResolveExtent(view, param, field, encoding, location, warnings)` - Vega-Lite's `parseSelectionExtent`: whether the
  parameter is a selection and the value of the field for the extent of a scale or a bin: the field that the extent
  names; else the field projected over its `encoding` (exactly one); else the first projected field, reporting a guess as
  `vl-param-selection-projection` (once for a location). The value is the resolved value of the selection (`Values`).
- `SelectionTimeUnits.Apply(table, predicates, context, location)` - adds to a table the start and the end of the time
  units (`SelectionComponent.TimeUnits`) of every selection that some predicates name (`param`, a legacy `selection`,
  and the compositions `and`, `or`, `not`), as Vega-Lite's time unit node does above a filter and above the data of a
  unit whose conditions test the selection (`SelectionTimeUnits.ConditionsOf` lists the predicates of the conditions
  of a unit). The fields are computed with the `TimeUnitTransform` unit, which replaces a field of the same name, so
  the time unit of the encoding itself finds the same values.

**Preconditions and postconditions**: `layers` is the layer tree of the specification (the units with their merged
encodings), `config` the resolved configuration, and `context` the evaluation state that the resolution of the selections
is charged to. The model describes the specification as written; the folded tree that later stages read has the same
structure, so a component finds its unit by `UnitIndex`.

#### Error Handling

An invalid selection (a `select` that is not `point` or `interval`, a malformed `fields`, `encodings`, `resolve`, or `views`)
is rejected when the document is opened (the `SpecParser` unit), so `Create` sees valid declarations. A value of the caller
that holds a text longer than `VegaLiteLimits.MaxStringLength` or a date that cannot be represented is a
`VegaLiteRenderException` located at the declaration (`ParameterEnvironment.FromCaller`); the resolution is charged to the
step budget of the call (`ExpressionRuntimeException` becomes a located render error when an expression reads it).
`ArgumentNullException` is thrown for a null argument. A channel that cannot be projected, an interval initialized with
a
key that is not a channel, and a value of the caller for a row-identity selection are warnings, not errors, because the
chart can still be drawn.

#### Dependencies

- **`SelectionDeclarations`**, **`SpecNode`**, **`SpecTree`**, **`LayerTree`**, **`ChannelDefinitions`**,
  **`ResolvedConfig`**, **`FeatureReport`** (`SpecModel`) - the declarations, the unit views and their encodings, the
  configuration, and the report records
- **`SelectionEntry`**, **`SelectionStore`**, **`SelectionTestSemantics`**, **`ExpressionFunctionContext`**
  (`Expressions`) - the tuples, the stores, and the resolution
- **`ScaleResolver.HasContinuousDomain`** (`Scales`) - the scale type of a channel before any scale exists
- **`TimeUnitTransform`**, **`TimeUnitRequest`**, **`TransformContext`** (`Transforms`) - the time units that
  `SelectionTimeUnits` computes
- **`ParameterEnvironment`**, **`TimeCalendar`**, **`DateTimeObject`**, **`DataValue`** - the values of the caller and the
  reading of dates

#### Callers

`VegaLiteDocument` calls `SelectionModel.Declares` and, when it holds, `Create` once in every `Measure` and `Render`,
before the parameters are evaluated: it gives `Stores` to the expression evaluator, `Values` to
`ParameterEnvironment.Populate`, and `Warnings` to the reports of the call. It builds the model from the specification as
written (the configuration and the layer tree of an unfolded tree are cheap and only needed when a selection is declared),
because a parameter expression or an expression reference may read a selection. After the layer tree of the folded
specification exists it calls `BindUnits`, and hands the bound model to the data stage (`TransformContext.Selections`, with
`TransformContext.View` the view that runs), to `ScaleResolver.ResolveLayers` (the extent of a scale), and to every
`LayerChart` (the brushes of `MarkBuilder.BuildBrush`). `RowTransforms.Filter` and the data stage call
`SelectionTimeUnits.Apply`; `TransformContext.BinSpanOf` calls `ResolveExtent` for a bin extent. `FeatureCatalog` calls
`SelectionDeclarations.Assign` (not the model) to find the selections that two layers receive. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Transforms/SelectionModelTests.cs` and `SelectionModelQueryTests.cs`
call the
model directly through `InternalsVisibleTo`.
