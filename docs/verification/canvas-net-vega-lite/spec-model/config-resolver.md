### ConfigResolver Unit Verification Design

This document describes the unit-level verification strategy for the `ConfigResolver` unit in the
`DemaConsulting.CanvasNet.VegaLite.SpecModel` namespace: the internal static `ConfigResolver` class (`Resolve`, the
shared `Defaults`, and the precedence-aware lookups for mark, view, style, and title properties), the `ResolvedConfig`
class (typed accessors by dotted path), and the `ConfigNode` and `ConfigNodeKind` types (the deep merge). The resolver
combines the Vega-Lite 6 default configuration (with the Vega guide, title, and style defaults it builds on), the
specification's `config`, and the caller's configuration text, in that order of increasing precedence.

#### Verification Approach

The unit is verified by resolving configurations for small, original specifications and asserting on the values that
later stages rely on. The default values are pinned one by one in a single test, so that a change of the default table
is a deliberate decision and fails a test first: the expected values are the Vega-Lite 6 and Vega defaults, written
into the test from the public configuration documentation and from the upstream configuration sources that the
third-party notice names (a default view of 300 by 300 pixels and not the 200 of version 5; padding 5; background
white; autosize `pad`; a title offset of 4, a subtitle padding of 3, and the `group-title` and `group-subtitle` styles
of 13 pixel bold and 12 pixel; an axis with domain, ticks, labels, grid off, and the `#888` and `#ddd` colors; the
`point`, `circle`, and `square` styles with size 30 and stroke width 2; the scale, bar, rect, and tick paddings and
sizes; and the `tableau10` category range). The precedence rules are asserted with specifications that define the
same property at every level, so that the winner and the fall-through to the next level are both visible: a style named
like the mark beats the mark-specific configuration, which beats the general mark configuration, which beats the Vega
default styles; `style.cell` beats `view`, which beats the default cell style; and the title configuration beats the
`group-title` style, with its `color` spelling mapped to `fill`.

The merge rule — objects merge at any depth, arrays and scalars are replaced as a whole, and the inputs, including the
shared defaults, are never modified — is asserted directly on `ConfigNode.Merge` and through `Resolve`. The unit has no
dependencies other than the parser's tree types and `VegaLiteLimits` (for the depth limit of the caller's
configuration), so no mocking or stubbing is required; the internal types are reached through the test assembly's
`InternalsVisibleTo` access.

Unit tests reside in `ConfigResolverTests.cs` within the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the
`SpecModel` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test resolves its own configuration text; no files are read, and the shared defaults are
  asserted to be unchanged after resolving

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any wrong default value, wrong precedence, merge that modifies an input, wrong exception type or parameter
name constitutes a failure.

#### Unit-Level Test Scenarios

##### Vega-Lite 6 Defaults And Specification Overrides

**Tests**: `ConfigResolverTests.ConfigResolver_Resolve_VegaLiteV6DefaultsAndOverrides`,
`ConfigResolverTests.ConfigResolver_Resolve_SchemaV5UsesV6Defaults`

Asserts the pinned defaults listed above (top-level, view, title, axis, marks, scales, the `tableau10` category range,
the `blues` ordinal scheme, and the eight default symbol shapes), the constants that live in code rather than in the
configuration (the reduced default opacity of 0.7, the default stroke width of 1, and the version `6.5.0` of the
Vega-Lite defaults that were studied), and that the specification's configuration overrides exactly what it names, at
any depth (a view width of 400 while the height stays 300), and keeps every other default, including a user style that
merges into the default style of the same name property by property; and that a schema of version 5, version 6, or
none gives identical trees, with the Vega-Lite 6 view size.

##### Caller Configuration Precedence And Merge

**Tests**: `ConfigResolverTests.ConfigResolver_Resolve_CallerConfigOverridesSpecConfig`,
`ConfigResolverTests.ConfigNode_Merge_MergesObjectsAndReplacesEverythingElse`

Asserts that the caller wins where both define a value, the specification fills in the rest, and the defaults the
remainder; that arrays are replaced as a whole and not merged element by element; that nested objects merge from all
three sources; that resolving never modifies the shared defaults; and, directly on the merge, that objects merge,
everything else is replaced, the inputs are unchanged, and merging two empty objects gives an empty object.

##### Invalid Caller Configuration Is An Argument Error

**Tests**: `ConfigResolverTests.ConfigResolver_Resolve_InvalidCallerConfigThrowsArgumentException`,
`ConfigResolverTests.ConfigResolver_Resolve_TooDeepCallerConfigThrowsArgumentException`

Asserts, as a theory, that caller configuration text that is an array, a number, `null`, malformed JSON, or empty
throws `ArgumentException`; that text nested seventy levels deep, beyond the limit of 64, throws `ArgumentException`
rather than overflowing or being accepted; and that a null tree throws `ArgumentNullException`.

##### Mark, View, And Title Property Precedence

**Tests**: `ConfigResolverTests.ConfigResolver_Resolve_MarkPropertiesFollowTheStylePrecedenceChain`,
`ConfigResolverTests.ConfigResolver_Resolve_VegaMarkDefaultsStayOutOfTheMergedTree`,
`ConfigResolverTests.ConfigResolver_Resolve_ViewPropertiesFollowTheCellStyleChain`,
`ConfigResolverTests.ConfigResolver_IsFilledByDefault_PointLineAndRuleAreOutlines`

Asserts, with a bar that defines its size at every level and a point that has only the general level, that the style
named like the mark beats the mark-specific configuration, which beats the general mark configuration, that a
property a level does not define falls through to the next level, and that the Vega default styles are the last
resort, below every Vega-Lite level; that the Vega mark defaults do not leak into the merged tree, so `config.rect`
is Vega-Lite's rectangle configuration (with no fill) and not Vega's `rect` mark default; that view properties follow
`style.cell`, then `view`, then the default cell style; and, as a theory, that `point`, `line`, and `rule` are
outlines by default while the other mark types are filled.

##### Font Shorthand And Typed Accessors

**Tests**: `ConfigResolverTests.ConfigResolver_Resolve_FontShorthandAppliesToTextAndGuideStyles`,
`ConfigResolverTests.ResolvedConfig_Accessors_ReadTypedValuesByDottedPath`

Asserts that the `font` shorthand fills the font of the text and guide styles below the explicit settings (a font for
everything but an explicit font for guide titles keeps the explicit one), and that the typed getters
(`GetNumber`, `GetString`, `GetBool`) read values by dotted path and return null on a kind mismatch or a missing path,
and the object and array getters return the nodes or nothing.
