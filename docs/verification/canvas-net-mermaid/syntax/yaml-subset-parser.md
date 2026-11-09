### YamlSubsetParser Unit Verification Design

This document describes the unit-level verification strategy for the `YamlSubsetParser` unit in the
`DemaConsulting.CanvasNet.Mermaid.Syntax` namespace: the internal `YamlSubsetParser` class (`Parse`, `ResolvePlain`,
and `FormatJsNumber`) and its value tree `YamlNode`, `YamlScalar`, `YamlSequence`, `YamlMapping`, and `YamlEntry`.

#### Verification Approach

The unit is verified differentially against js-yaml: the expected values of the document, type, and error scenarios
were produced by running the js-yaml that the pinned Mermaid release bundles, with the JSON schema, on the same
inputs. The tests convert the parsed tree to compact JSON with JavaScript's `JSON.stringify` rules and compare it with
js-yaml's result, compare each error's message and location with js-yaml's, and compare the number formatting with
JavaScript's `String(n)`. The refused constructs and the depth limit, which js-yaml does not have, are asserted for
their codes, hints, and locations. Locations come from a line table of the YAML text itself. No mocking or stubbing
is required; the internal types are reached through the test assembly's `InternalsVisibleTo` access.

Unit tests reside in `YamlSubsetParserTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the
`Syntax` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Pure functions of in-memory text; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any value, message, or location that differs from js-yaml's, or any refused construct that is accepted,
constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Syntax-YamlSubsetParser-Documents: Documents Read To js-yaml's Values

**Tests**: `YamlSubsetParserTests.YamlSubsetParser_Parse_Documents_MatchJsYaml`

Asserts js-yaml's result for 15 documents: nested block and flow collections, compact sequences of mappings,
literal and folded block scalars with chomping, multi-line plain and single-quoted scalars with folding and
comments, flow mappings inside sequences, JSON-style keys and trailing commas, double-quoted escapes and line
continuations, empty sequence entries, a multi-line plain document, a comment-only and an empty text, an explicit
document start and end, and plain scalars that look like mappings.

##### CanvasNetMermaid-Syntax-YamlSubsetParser-ImplicitTypes: Plain Scalars Take The JSON Schema Types

**Tests**: `YamlSubsetParserTests.YamlSubsetParser_Parse_ImplicitTypes_MatchJsonSchema`

Asserts that the four null spellings are null, `True` and `FALSE` are booleans, `0x1F`, `-0o17`, `0b101`, and `012`
are 31, -15, 5, and 12, `1.5e3`, `.5`, `1.`, `-.inf`, and `.NaN` are floats, and `1_000`, `1e400`, `-.5`, and `0x`
stay strings, as in js-yaml.

##### CanvasNetMermaid-Syntax-YamlSubsetParser-JavaScriptValues: Values Convert As JavaScript Converts Them

**Tests**: `YamlSubsetParserTests.YamlSubsetParser_FormatJsNumber_MatchesJavaScript`,
`YamlSubsetParserTests.YamlNode_TruthinessAndString_FollowJavaScript`

Asserts JavaScript's text of 14 numbers (positional and exponential forms at both thresholds, negative zero, the
smallest and largest doubles, not a number, and negative infinity), the string conversion of a sequence with a null
item and of a mapping, and the truthiness of zero, not a number, the empty string, the string `0`, and a mapping.

##### CanvasNetMermaid-Syntax-YamlSubsetParser-Errors: Invalid YAML Fails With js-yaml's Message And Location

**Tests**: `YamlSubsetParserTests.YamlSubsetParser_Parse_InvalidYaml_ThrowsLocatedJsYamlError`

Asserts `mermaid-yaml-invalid`, js-yaml's message, and js-yaml's line and column for nine invalid texts: a
duplicated key, a tab in the indentation, three kinds of bad indentation, an unterminated single-quoted scalar and
flow collection, an unknown escape, and a second document.

##### CanvasNetMermaid-Syntax-YamlSubsetParser-RefusedConstructs: Unneeded YAML Constructs Are Refused

**Tests**: `YamlSubsetParserTests.YamlSubsetParser_Parse_RefusedConstruct_ThrowsUnsupported`

Asserts `mermaid-yaml-unsupported` with a hint for an anchor on a key and on a value, an alias in a block and in a
flow collection, a tag, an explicit key, and a `%YAML` directive.

##### CanvasNetMermaid-Syntax-YamlSubsetParser-Depth: Nesting Is Bounded Without Exhausting The Stack

**Tests**: `YamlSubsetParserTests.YamlSubsetParser_Parse_TooDeep_ThrowsLimitExceeded`,
`YamlSubsetParserTests.YamlSubsetParser_Parse_HostileNesting_IsBounded`

Asserts that four nested flow sequences and four nested block mappings are `mermaid-limit-exceeded` at a limit of 3
(located at the fourth `[`, with a message that names `MermaidLimits.MaxYamlDepth`) and accepted at a limit of 4,
and that 100,000 opening brackets fail with the same code instead of overflowing the stack.

##### CanvasNetMermaid-Syntax-YamlSubsetParser-Locations: Values And Keys Are Located

**Tests**: `YamlSubsetParserTests.YamlSubsetParser_Parse_Nodes_CarryLocations`

Asserts the locations of a mapping (line 1, column 1), of its second key (line 2, column 1), and of a sequence item
(line 3, column 5), and that a missing key gives no value.
