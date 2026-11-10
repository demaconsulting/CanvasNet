### MermaidConfigResolver Unit Verification Design

This document describes the unit-level verification strategy for the `MermaidConfigResolver` unit in the
`DemaConsulting.CanvasNet.Mermaid.Configuration` namespace: the internal static `MermaidConfigResolver` (`Resolve`, the
defaults and configuration keys, and the merge and sanitizer primitives) and the typed view `MermaidConfig` of its
result.

#### Verification Approach

The unit is verified in two ways. `MermaidConfigResolverVectorTests` resolves the 53 configuration cases recorded from
the pinned Mermaid in `Fixtures/upstream-vectors/themes.json` (each recorded as `mermaid.initialize(site)`, then
`mermaid.parse(text)`, then `getConfig()`) the way Mermaid's `parse` builds them - resolve, detect the diagram with the
result, resolve again with the detected diagram - and compares the global values, the flowchart section,
`gantt.displayMode`, and every theme variable with the recorded ones; it also compares the configuration keys and the
global and flowchart defaults with Mermaid's. `MermaidConfigResolverTests` verifies the precedence, the reports, and the
merge primitives on small hand-written texts, asserting the effective values, the diagnostic codes, the locations
(file, line, and column of the frontmatter key or of the directive), and the hints; for the sequence, class, state,
and `er` sections it also asserts the typed values that the renderers read, against Mermaid's schema and the reads of
its renderers. The unit is reached through the
test assembly's `InternalsVisibleTo` access; the `Preprocessor` and the report sink are real, so no mocking or stubbing
is required.

Unit tests reside in `MermaidConfigResolverTests.cs` and `MermaidConfigResolverVectorTests.cs` within the
`DemaConsulting.CanvasNet.Mermaid.Tests` project, in the `Configuration` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fixtures**: `Fixtures/upstream-vectors/themes.json` (configuration cases, configuration keys, and defaults),
  generated from the pinned Mermaid by `tools/visual-diff/mermaid/gen-theme-vectors.mjs`
- **Isolation**: Each test builds its own text, options, and report sink; the fixture is read only

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any configuration value that differs from the recorded Mermaid value, and any report with another code,
location, or count than asserted, constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Configuration-MermaidConfigResolver-Precedence: Layers Apply In Mermaid's Order

**Tests**: `MermaidConfigResolverTests.MermaidConfigResolver_Resolve_ThemePrecedence`,
`MermaidConfigResolverVectorTests.MermaidConfigResolver_Resolve_MatchesMermaid`

Asserts the effective theme for every combination of the theme option, the site configuration, the frontmatter, and a
directive (each later layer wins), with the theme's own `mainBkg` and no warning, and the recorded precedence cases.

##### CanvasNetMermaid-Configuration-MermaidConfigResolver-MermaidFidelity: The Configuration Equals Mermaid's

**Tests**: `MermaidConfigResolverVectorTests.MermaidConfigResolver_Resolve_MatchesMermaid`,
`MermaidConfigResolverVectorTests.MermaidConfigResolver_ConfigKeys_MatchMermaid`,
`MermaidConfigResolverVectorTests.MermaidConfigResolver_CreateDefaults_MatchMermaid`,
`MermaidConfigResolverTests.MermaidConfigResolver_MergePrimitives_FollowMermaid`

Asserts, for each recorded case, that the global values, the flowchart section, the Gantt display mode, and the theme
variables equal Mermaid's; that the configuration keys are exactly Mermaid's 705 keys; that the global and flowchart
defaults equal Mermaid's (the keys whose default is undefined being absent); and that `assignWithDepth` keeps an object
from being clobbered, skips nulls, and merges arrays as sets, lodash's `merge` replaces with nulls, and `sanitizeCss`
refuses unbalanced braces.

##### CanvasNetMermaid-Configuration-MermaidConfigResolver-Directives: Init Directives Are Read As Mermaid Reads Them

**Tests**: `MermaidConfigResolverTests.MermaidConfigResolver_Resolve_DirectiveSingleQuotesAndConfigKey`,
`MermaidConfigResolverTests.MermaidConfigResolver_Resolve_InvalidJson_IgnoresAllInitDirectives`,
`MermaidConfigResolverTests.MermaidConfigResolver_Resolve_NonObjectArguments_Reported`,
`MermaidConfigResolverTests.MermaidConfigResolver_Resolve_WrapHtmlLabelsAndLocations`

Asserts that a directive written with single quotes is read and its `config` key moves to the flowchart section, that
invalid JSON in the second of two init directives is reported at that directive and makes both ignored, that an array
of arguments is reported and ignored, and that `%%{wrap}%%` sets `wrap`.

##### CanvasNetMermaid-Configuration-MermaidConfigResolver-SecureKeys: Secure Keys Cannot Be Set By The Text

**Tests**: `MermaidConfigResolverTests.MermaidConfigResolver_Resolve_SecureKeyInFrontmatter_IgnoredAndLocated`,
`MermaidConfigResolverTests.MermaidConfigResolver_Resolve_SecureKeysOfSite_IgnoredInDirective`

Asserts that `securityLevel` and `maxEdges` in the frontmatter keep their defaults and are reported at their keys, and
that a directive cannot set `maxTextSize` (a default secure key) nor `theme` when the site configuration lists it as
secure, while the site's own `maxEdges` is kept.

##### CanvasNetMermaid-Configuration-MermaidConfigResolver-UnknownKeys: Unknown Keys Are Reported With A Hint

**Tests**: `MermaidConfigResolverTests.MermaidConfigResolver_Resolve_UnknownKey_ReportedWithHint`,
`MermaidConfigResolverTests.MermaidConfigResolver_Resolve_ManyUnknownKeys_HintsBounded`

Asserts that `flowchart.nodeSpaces` is reported at its key with the hint `did you mean 'nodeSpacing'?` and has no
effect, and that 40 unknown keys are each reported while only 32 carry a hint.

##### CanvasNetMermaid-Configuration-MermaidConfigResolver-InvalidValues: Invalid Values Are Reported And Dropped

**Tests**: `MermaidConfigResolverTests.MermaidConfigResolver_Resolve_WrongType_KeepsLowerLayer`,
`MermaidConfigResolverTests.MermaidConfigResolver_Resolve_FrontmatterConfigNotMapping_Reported`,
`MermaidConfigResolverTests.MermaidConfigResolver_Resolve_RefusedStrings_Reported`,
`MermaidConfigResolverTests.MermaidConfigResolver_Resolve_UnavailableTheme_Reported`

Asserts that a text `nodeSpacing` keeps the site configuration's value and a numeric `htmlLabels` is dropped, both
reported; that a frontmatter `config: 5` is reported and ignored; that a `fontFamily` with markup and a theme variable
with refused characters are reported and the defaults kept; and that `neo` (a newer Mermaid theme, named as not
supported by this version) and `nope` are reported at their key with the five themes in the hint and leave the default
theme in effect.

##### CanvasNetMermaid-Configuration-MermaidConfigResolver-ThemeRecovery: An Unreadable Theme Variable Is Dropped

**Tests**: `MermaidConfigResolverTests.MermaidConfigResolver_Resolve_ThemeVariableNotAColor_Dropped`,
`MermaidConfigResolverTests.MermaidConfigResolver_Resolve_HostileColorLimit_FallsBackToTheme`

Asserts that a `primaryColor` of `nonsense` is reported by name and the base theme computed without it while the other
override (`lineColor`) is kept, and that a `THEME_COLOR_LIMIT` of 1e12, which has no single culprit, is reported once
at the directive and the theme computed without any override.

##### CanvasNetMermaid-Configuration-MermaidConfigResolver-TypedView: Typed Settings And Locations

**Tests**: `MermaidConfigResolverTests.MermaidConfigResolver_Resolve_WrapHtmlLabelsAndLocations`,
`MermaidConfigResolverTests.MermaidConfigResolver_Resolve_ElkRenderer_SettingsAndLayout`

Asserts the typed `Wrap`, `HtmlLabels` (from the deprecated `flowchart.htmlLabels`), `Look`, and `Layout`, the
locations of the frontmatter key, the directive, and the wrap directive (and no location for a default), and that a
flowchart with `defaultRenderer: elk` gives the detector settings of the three sections and the `elk` layout.

##### CanvasNetMermaid-Configuration-MermaidConfigResolver-SilentResolution: Reports Do Not Change The Result

**Tests**: `MermaidConfigResolverTests.MermaidConfigResolver_Resolve_WithoutSink_SameResult`

Asserts that a text with a secure key and an invalid directive gives warnings with a sink and a configuration equal
(deep JSON equality) to the one resolved without a sink.

##### CanvasNetMermaid-Configuration-MermaidConfigResolver-SequenceSection: The Sequence Section

**Tests**: `MermaidConfigResolverTests.MermaidConfigResolver_Resolve_SequenceDefaults_MatchMermaid`,
`MermaidConfigResolverTests.MermaidConfigResolver_Resolve_SequenceFonts_FollowSetConf`,
`MermaidConfigResolverTests.MermaidConfigResolver_Resolve_SequenceWrap_FollowsInit`,
`MermaidConfigResolverTests.MermaidConfigResolver_Resolve_SequenceInvalidValues_ReportedAndIgnored`,
`MermaidConfigResolverTests.MermaidConfigResolver_Resolve_SequenceAlignInvalid_ReportsAllSixValues`,
`MermaidConfigResolverTests.MermaidConfigResolver_Resolve_SequenceAlignValues_Accepted`,
`MermaidConfigResolverTests.MermaidConfigResolver_Resolve_SequenceDirectiveConfig_MovesToSection`

Asserts, for a sequence diagram without configuration, Mermaid's sequence defaults (participants 150 by 65, the
margins 50, 10, 5, 10, and 35, the activation width 10, the diagram margins 50 and 10, the alignments `center`,
mirrored participants, no forced menus, a bottom adjustment of 1, no right angles, no sequence numbers, no wrap, the
wrap padding 10, the label box 50 by 20, and no text placement) with the three fonts in the top-level family at 16
pixels and weight 400, without a warning; that the section's font sizes and families have no effect while its weights
apply, and that a top-level `fontSize`, `fontFamily`, and `fontWeight` replace all three fonts; that the texts wrap
when the top-level `wrap`, a `%%{wrap}%%` directive, or `sequence.wrap` is true, and that a false top-level `wrap`
does not turn off the section's (a theory of seven cases); that a Boolean of the wrong type, a width that is text, a
font size that is a Boolean, and the alignment `diagonal` are four `mermaid-config-invalid` warnings at the directive
and leave the defaults, while the alignment `end` and the font size `18px` are accepted; that the alignment `justify`
of `messageAlign` or `noteAlign` is one `mermaid-config-invalid` warning whose message says the value must be left,
center, right, start, middle or end and whose hint names the same six values, leaving `center`; that each of those
six values is accepted for both keys without a warning; and that a single directive's `config` key is moved to the
sequence section (`mirrorActors`, `actorMargin`, and `textPlacement`).

##### CanvasNetMermaid-Configuration-MermaidConfigResolver-ClassSection: The Class Section As The Class Renderer Reads It

**Tests**: `MermaidConfigResolverTests.MermaidConfigResolver_Resolve_ClassDefaults_MatchMermaid`,
`MermaidConfigResolverTests.MermaidConfigResolver_Resolve_ClassSpacingFromState_FollowsClassRenderer`,
`MermaidConfigResolverTests.MermaidConfigResolver_Resolve_ClassInvalidValues_ReportedAndIgnored`

Asserts, for a class diagram without configuration, that the effective class section holds exactly the three keys of
Mermaid's `defaultConfig.ts` (`defaultRenderer`, `hideEmptyMembersBox`, and `hierarchicalNamespaces`, without
`padding`) and that the typed class section is `dagre-wrapper`, false, true, and no padding, with the node and rank
spacing 50 and the title margin 25, without a warning; that a frontmatter with `class.padding: 0`,
`hideEmptyMembersBox: true`, and `hierarchicalNamespaces: false` and with `state.nodeSpacing: 0`,
`state.rankSpacing: 80`, and `state.titleTopMargin: 0` gives a padding of 0, the two switches, a node spacing of 50 (a
zero falls back, as `||` does), a rank spacing of 80, and a title margin of 0 (a zero is kept, as `??` does), without
a warning; and that a directive with a text `class.padding`, a text `class.hierarchicalNamespaces`, and a text
`state.nodeSpacing` gives three `mermaid-config-invalid` warnings at the directive (line 1, column 1), whose messages
name `class.padding` and `state.nodeSpacing`, and leaves the typed class section at its defaults.

##### CanvasNetMermaid-Configuration-MermaidConfigResolver-StateSection: The State Section As The State Renderer Reads It

**Tests**: `MermaidConfigResolverTests.MermaidConfigResolver_Resolve_StateAndErDefaults_MatchMermaid`,
`MermaidConfigResolverTests.MermaidConfigResolver_Resolve_StateAndErSpacing_FollowTheRenderers`,
`MermaidConfigResolverTests.MermaidConfigResolver_Resolve_StateAndErInvalidValues_ReportedAndIgnored`

Asserts, for a state diagram without configuration, that the effective state section holds the 18 keys of Mermaid's
schema (among them `padding` 8 and `edgeLengthFactor` the string `"20"`) and that the typed state section is
`dagre-wrapper`, a node and rank spacing of 50, and a title margin of 25, without a warning; that a frontmatter with
`state.nodeSpacing: 0`, `state.rankSpacing: 70`, and `state.titleTopMargin: 0` gives a node spacing of 50 (a zero
falls back, as `||` does), a rank spacing of 70, and a title margin of 0 (a zero is kept, as `??` does), without a
warning; and that a directive with a number `state.defaultRenderer` is reported as `mermaid-config-invalid` with a
message that names `state.defaultRenderer`, and leaves the typed state section at its defaults.

##### CanvasNetMermaid-Configuration-MermaidConfigResolver-ErSection: The ER Section As The ER Renderer Reads It

**Tests**: `MermaidConfigResolverTests.MermaidConfigResolver_Resolve_StateAndErDefaults_MatchMermaid`,
`MermaidConfigResolverTests.MermaidConfigResolver_Resolve_StateAndErSpacing_FollowTheRenderers`,
`MermaidConfigResolverTests.MermaidConfigResolver_Resolve_StateAndErInvalidValues_ReportedAndIgnored`

Asserts, for an entity relationship diagram without configuration, that the effective `er` section holds the 12 keys
of Mermaid's schema (among them `minEntityHeight` 75 and `fill` `honeydew`) and that the typed `er` section is a
diagram padding of 20, an entity padding of 15, a minimum entity width of 100, a node spacing of 140, a rank spacing of
80, and a title margin of 25, without a warning; that a frontmatter with `er.nodeSpacing: 0`, `er.rankSpacing: 60`,
`er.titleTopMargin: 0`, `er.diagramPadding: 5`, `er.entityPadding: 4`, and `er.minEntityWidth: 30` gives a node
spacing of 140 (a zero falls back), a rank spacing of 60, a title margin of 0 (a zero is kept), and the paddings 5
and 4 and the width 30 as given, without a warning; and that a directive with a text `er.entityPadding` and a text
`er.useMaxWidth`, beside the number `state.defaultRenderer`, gives three `mermaid-config-invalid` warnings, one of
whose messages names `er.entityPadding`, and leaves the typed `er` section at its defaults.
