## SpecModel

![SpecModel Structure](VegaLiteSpecModelView.svg)

### Overview

The `SpecModel` subsystem (namespace `DemaConsulting.CanvasNet.VegaLite.SpecModel`, folder
`src/DemaConsulting.CanvasNet.VegaLite/SpecModel/`) is everything the system knows about the specification text:
it turns Vega-Lite JSON into an immutable tree with exact locations, lists the constructs that are recognized but not
supported, and resolves the effective configuration. It contains no data, no pixels, and no file or network access. It
is **internal**: the tree, the reports, and the configuration types are visible only to the `VegaLiteDocument` unit,
the other subsystems, and, through `InternalsVisibleTo`, the test assembly. It contains three units:

- **SpecParser** - the internal partial `SpecParser` (`SpecParser.cs`, `SpecParser.Validation.cs`,
  `SpecParser.Transforms.cs`, `SpecParser.Aggregates.cs`, and `SpecParser.Time.cs`, with `ParameterGraph`), the tree
  types `SpecNode`,
  `SpecNodeKind`, `NodePosition`, and
  `SpecTree` (`SpecNode.cs`), `SpecExpressions` (`SpecExpressions.cs`), and `SpecMetadata` and `SpecMarkEntry`
  (`SpecMetadata.cs`); `PredicateSource` (`PredicateSource.cs`) writes the field predicates, including the time
  predicates, as expression source; see _SpecParser Unit Design_ (`spec-model/spec-parser.md`)
- **FeatureCatalog** - the internal static `FeatureCatalog`, `FeatureReport`, and `FeatureReportKind`; see
  _FeatureCatalog Unit Design_ (`spec-model/feature-catalog.md`)
- **ConfigResolver** - the internal static `ConfigResolver`, `ResolvedConfig`, `ConfigNode`, and `ConfigNodeKind`; see
  _ConfigResolver Unit Design_ (`spec-model/config-resolver.md`)

The subsystem's namespace documentation is in `SpecModel/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetVegaLite-SpecModel-*` requirements in `docs/reqstream/canvas-net-vega-lite/spec-model.yaml`.

### Interfaces

**Exposed** (internal only): `SpecParser.Parse(string, string?, VegaLiteLimits?)` and
`SpecParser.Parse(byte[], string?, VegaLiteLimits?)`, which return a `SpecTree`; the helpers
`SpecParser.ParseSchemaMajorVersion`, `SpecParser.IsChannelName`, `SpecParser.FindTransformOperation`,
`SpecParser.EscapePointerSegment`, and the helpers `SpecParser.IsSupportedTransform`, `IsDrawnView`, `IsViewSignal`,
`IsParameterDeclared`, `FindParameter`, `IsSupportedSort`, `TryParseSortShorthand`, and `IsSortChannel`;
`SpecTree.Expressions` (the compiled expressions) and `ParameterGraph.Sort`;
`SpecMetadata.Read(SpecTree)`; `FeatureCatalog.Visit(SpecTree)` and
`FeatureCatalog.IsSchemeBearingUrl(string)`; and `ConfigResolver.Resolve(SpecTree, string?)`,
`ConfigResolver.Resolve(SpecNode?, string?)`, and `ConfigResolver.Defaults`, which return a `ResolvedConfig`.

**Consumed**: the public `VegaLiteLimits`, `VegaLiteLocation`, and `VegaLiteSpecException` types of the
`VegaLiteDocument` unit (limits to enforce, the location of every node and report, and the error type of a malformed
specification), `ExpressionParser` and `CompiledExpression` of the `Expressions` subsystem (the compilation of the
expressions and the constructs they collect), `FieldPath`, `ParseDirectives`, `TimeUnitSpec`, `DateTimeObject`, and
`JsDateParser` of the `Data` subsystem (field names, the `parse` entry rule, and the time unit, date-time object, and
date text rules of the time validation), `AggregateMeasures` and `StackRules`
of
the `Transforms` subsystem (the operation names and the stack decision), and `ScaleResolver.InferOrient` of the `Scales`
subsystem (so that the catalog reports `order` exactly when the plan will stack). The .NET base class
library supplies `System.Text.Json`, `System.Text`, and `System.Globalization`.

**Consumers**: `VegaLiteDocument` (parse, metadata, reports, and configuration), the `Data` subsystem (`DataLoader`
reads `SpecNode`), the `Transforms` subsystem (`RowTransforms` and `ParameterEnvironment` read the tree, its compiled
expressions, and the transform and parameter helpers), the `Scales` subsystem (`ScaleResolver` reads `SpecNode` and
`ResolvedConfig`), the `Guides`
subsystem (`TitleBuilder` and `AxisBuilder`), the `Layout` subsystem (`ViewLayout`), and the `Marks` subsystem
(`MarkBuilder`), all of which read the tree and the resolved configuration.

### Design

**Read once, then everything points into the tree.** `SpecParser` reads the text in a single forward pass into the
immutable `SpecNode` tree. Every later stage reads that tree and reports problems at the node's pointer, line, and
column, so no stage re-reads or re-locates text. Nodes carry the position of their value and, for properties, the
position of the key; the RFC 6901 pointer is computed lazily, so reading a large specification allocates no pointer
strings. The tree is immutable and safe to share between threads.

**Structural errors versus warnings.** A property that Vega-Lite defines but whose value has the wrong type or value is
a `VegaLiteSpecException` with the pointer of the value, because the specification cannot be what its author meant. The
same holds for an expression that cannot be compiled (the message adds the offset inside the text), a transform or predicate
of the wrong shape, and a parameter cycle or an undeclared parameter reference: the
expressions of every supported site are
**compiled once when the document is opened** and kept in the tree, so a broken expression
never reaches the data stage. A
property that Vega-Lite does not define is only a `vl-unknown-property` warning at the key, because the text may come
from a newer version or a tool. A key written twice keeps the last value and yields one `vl-duplicate-key` warning.
Values the validator does not check are left to the stage that uses them.

**The catalog is the boundary of support.** `FeatureCatalog` is the single place that defines which recognized
constructs the renderer does not draw. It is a pure function of the tree and returns one report per construct, sorted
by position. Supported constructs are simply absent from its output, so moving a construct from deferred to supported
is an edit of the catalog's supported sets together with the unit that draws it. The document merges the catalog
reports with the parse warnings once.

**Configuration is a merged tree, not a bag of constants.** `ConfigResolver` builds one `ConfigNode` tree from the Vega
guide defaults, the Vega-Lite defaults, the specification's `config`, and the caller's `Config`, in that order of
increasing precedence, and exposes typed accessors and the precedence chains of marks, views, titles, and styles on the
`ResolvedConfig`. Defaults that Vega-Lite computes by rule rather than by configuration (a scale's `zero` and `nice`,
for example) are not in the tree; the stage that applies the rule owns it.

**Limits fail closed.** The parser enforces `MaxSourceLength`, `MaxJsonDepth`, and `MaxStringLength`; nothing in the
subsystem recurses in proportion to the input (the reader, the validator, the catalog walker, and the configuration
merge use explicit stacks or bounded depth), and no code opens a file or uses the network.

**Provenance.** The default values of `ConfigResolver` are ported with attribution (see _Third-Party Provenance And
Attribution_ in _CanvasNetVegaLite System Design_); the parser and the catalog are original code, and the sets of known
property names were taken from the published Vega-Lite 6 JSON schema (names and enumerations only).
