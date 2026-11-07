### ExpressionReferenceFolder

![SpecModel Structure](VegaLiteSpecModelView.svg)

The `ExpressionReferenceFolder` unit is the fifth software unit in the `SpecModel` subsystem. It is two source files in
the `DemaConsulting.CanvasNet.VegaLite.SpecModel` namespace: `ExpressionReferenceFolder.cs`, with the `internal` types
`ExpressionReferenceFolder` (static class) and `FoldResult` (sealed record), and `ExpressionReferenceSites.cs`, with the
`internal` static class `ExpressionReferenceSites` (and its `Owner` enumeration and `Site` record). The records
`StaticReference` and `StaticSiteKind`, which hand the references from the parser to the folder, are in
`SpecExpressions.cs` and are documented with the `SpecParser` unit.

#### Purpose

Vega-Lite compiles an `ExprRef` (an `{"expr": ...}` object where the schema allows a value) to a Vega signal, so in a
chart that never changes the property has the value the expression gives with the parameters at their initial state.
`ExpressionReferenceFolder` evaluates each such reference once per call and rewrites the specification tree so that every
later stage (the configuration resolver, the normalizer, the scales, the guides, the marks, the title, and the layout)
reads the value where the author wrote the reference, and none of them needs to know that expressions exist. It exists so
that the support for parameter-driven properties is one unit and not a change in every stage.

`ExpressionReferenceSites` is the schema-derived table of the properties that take a reference, owner by owner (title,
mark, axis, legend, header, scale, scale configuration, view background, channel definition, the root view, and the
configuration), and the walk that finds the references in a tree.

The unit's behavior is specified by the `CanvasNetVegaLite-SpecModel-ExpressionReferenceFolder-*` requirements in
`docs/reqstream/canvas-net-vega-lite/spec-model/expression-reference-folder.yaml`.

#### Data Model

**`ExpressionReferenceSites.Site`** (readonly record struct) is one reference found by the walk: `Reference` (the
`{"expr": ...}` object), `Text` (the string node that holds the expression, which keys the compiled expression), and
`Kind` (a `StaticSiteKind`). **`ExpressionReferenceSites.Owner`** names the kinds of object that carry references: `View`,
`RootView`, `Title`, `Mark`, `Encoding`, `ChannelDef`, `Condition`, `Axis`, `Legend`, `Header`, `Scale`, `ScaleConfig`,
`ViewBackground`, and `Config`. The property names of each owner are sets taken from the published Vega-Lite 6 schema
(names only); `PropertiesOf(owner)` returns them.

**`StaticSiteKind`** (in `SpecExpressions.cs`) tells how a site reads the value: `Value` (a number, string, boolean,
null, or an array of them), `Text` (the `text` of a title or a mark: a number or a boolean is its JavaScript text), and
`Domain` (a property of a scale domain: a date is its milliseconds). **`StaticReference`** (sealed record) is
`Reference`, `Text`, the compiled `Expression`, and `Kind`.

**`FoldResult`** (sealed record) is the `Tree` to render from (the tree that was given when nothing is folded) and the
`Warnings` (a list of `FeatureReport`) of the references that were left in place.

The class is stateless and thread-safe; one call belongs to one evaluator, which belongs to one thread.

#### Key Methods

- `ExpressionReferenceSites.Find(root)` - walks the specification from the root with an explicit stack, along the
  properties that lead to an owner (`title`, `mark` and its overlays and composite parts, `encoding` and its channels, the
  `axis`, `legend`, `scale`, `header`, and `condition` of a channel, `view`, `facet`, `spec`, `layer`, `concat`,
  `hconcat`, `vconcat`, and, for the root only, `config`), and records each reference found at a property of the
  owner's table; `domain` and `range` of a scale also take a reference for each entry. It never enters `data`,
  `datasets`, `transform`, `params`, or `projection`. A reference is a leaf: what it holds is not searched. The result
  is in
  document order. The paint properties of a mark (`color`, `fill`, `stroke`, `opacity`, `strokeWidth`, `size`) and the
  `value` of a paint channel are found too; the parser has compiled those as row-bound expressions and tells them
  apart by
  the compiled expression it already holds.
- `ExpressionReferenceFolder.Fold(tree, evaluator)` - for each `StaticReference` of `tree.Expressions.StaticReferences`:
  a reference whose expression reads `datum` is skipped (it stays in place, and the feature catalog reports it);
  otherwise the expression is evaluated with the evaluator (which charges every step to the budget of the call),
  a result text longer than the evaluator's string limit is a located `VegaLiteRenderException`, and the value is
  converted to a node. A null, boolean, number, string, array, or object converts to the node of its kind (a number that
  is not finite converts to null; a number or a boolean at a `Text` site converts to a string node holding the JavaScript
  text; the members of an object are plain values). A date converts to a number of milliseconds only at a `Domain`
  site; a
  date elsewhere, a regular expression, or a composite holding one converts to nothing, and the reference stays with a
  `vl-expr-ref` warning (kind `Warning`) located at it. When nothing is folded the tree that was given is returned.
  Otherwise every node from a folded reference up to the root (found through the parent links of the nodes) is copied
  (`SpecNode.Overlay` for an object, `SpecNode.Synthetic` for an array and for the values) and every other subtree is
  shared, so the compiled expressions and the parameter indexes, which are keyed by node, remain valid. The values are
  located at the reference they replace. The new `SpecTree` keeps the source name, the parse warnings, and the
  expressions of the old one.

**Preconditions and postconditions**: the tree is the one the parser produced (the ancestors of a reference are found
through the parent links), and the evaluator holds the parameter values of the call, so the caller evaluates the
parameters first. The result tree is read-only like every tree and has the same shape as the input except at the folded
references.

#### Error Handling

An expression that cannot be compiled, or that reads an undeclared parameter, is a `VegaLiteSpecException` raised when
the document is opened (by the parser, which compiles the references), so no malformed reference reaches the fold. The fold
raises a `VegaLiteRenderException` located at the reference for an expression that exceeds the step budget or another limit
of the evaluation, and for a value that holds a text longer than `VegaLiteLimits.MaxStringLength`; it throws
`OperationCanceledException` when the token of the evaluator is canceled and `ArgumentNullException` for a null argument.
A value that no property can hold is not an error: the reference stays and a warning is returned, because the chart can
still be drawn. The unit does no file or network access.

#### Dependencies

- **`SpecNode`**, **`SpecTree`**, **`SpecExpressions`**, and **`FeatureReport`** - the tree and report types of the
  `SpecModel` subsystem
- **`ExpressionEvaluator`** (`Expressions` subsystem) - the evaluation of a compiled expression with the budget and the
  parameter values of the call
- **`DataValue`** (`Data` subsystem) - the value an expression gives
- **`VegaLiteLimits`**, **`VegaLiteRenderException`**, and **`VegaLiteLocation`** - the string limit and the located
  error
- **.NET base class library** - `System.Collections.Generic` (the explicit stack and the reference-equality
  dictionaries)

#### Callers

`VegaLiteDocument` calls `ExpressionReferenceFolder.Fold` once in every `Measure` and `Render`, after the parameters are
evaluated and before the configuration is resolved. `SpecParser` calls `ExpressionReferenceSites.Find` to compile the
static references when the document is opened. `FeatureCatalog` reads the static references to report one that reads
`datum`. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/SpecModel/ExpressionReferenceFolderTests.cs` call the folder and the table
directly through `InternalsVisibleTo`.
