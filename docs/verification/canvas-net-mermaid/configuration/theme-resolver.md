### ThemeResolver Unit Verification Design

This document describes the unit-level verification strategy for the `ThemeResolver` unit in the
`DemaConsulting.CanvasNet.Mermaid.Configuration` namespace: the internal static `ThemeResolver` (`GetThemeVariables`
and `IsTheme`), its JavaScript object model `ThemeObject`, and the style sheets `FlowchartStyles`, `ClassStyles`,
`StateStyles`, `ErStyles`, and `SequenceStyles` with their shared bases `DiagramStyles` and (for the flowchart, class,
state, and entity relationship sheets) `UnifiedStyles`.

#### Verification Approach

The themes are verified against the theme variables recorded from the pinned Mermaid in
`Fixtures/upstream-vectors/themes.json`: for each of the five themes, without overrides, with overrides of base and
derived variables, and in dark mode, every variable must have the same text as Mermaid's (colors are compared as
strings, because the text reaches the style sheet). Hand-written tests verify the derivation of an overridden base
color, the override that wins, the bounds, the theme names, and the JavaScript semantics of `ThemeObject`. The style
sheet is verified by building `FlowchartStyles` from configurations resolved as Mermaid resolves them and asserting the
paints of the default theme (as Mermaid's flowchart CSS gives them), the effect of `themeCSS`, and the dropping of an
invalid declaration. The sequence style sheet is verified the same way with `SequenceStyles`: the shared global rules,
the cascade over the attributes Mermaid writes, every theme's variables, `themeCSS`, and the drop-shadow filters. The
class style sheet is verified with `ClassStyles`: the shared global rules, the paints of the default theme as Mermaid's
class CSS gives them (through the members of `UnifiedStyles` that the shared rendering units read), the note labels and
label backgrounds that differ from a flowchart's, and `themeCSS` against the important marker fills. The state and
entity relationship style sheets are verified the same way with `StateStyles` and `ErStyles`: the shared global
rules, the paints of the default theme as Mermaid's state and ER CSS give them (the label and title colors, the CSS
corner radii and cluster fills that the state shapes read by selector, the `barb` marker paint, the important marker
paint of the ER sheet, and the subgraph titles), and `themeCSS`; and the facts every unified sheet gives the rendering
(whether it centers the SVG text of a node, and its root text) are compared across the four sheets. The
unit is reached through the test assembly's `InternalsVisibleTo` access; `ColorMath`,
`StyleDeclarations`, and `MermaidConfigResolver` are real, so no mocking or stubbing is required.

Unit tests reside in `ThemeResolverTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the
`Configuration` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fixtures**: `Fixtures/upstream-vectors/themes.json` (the site theme cases), generated from the pinned Mermaid by
  `tools/visual-diff/mermaid/gen-theme-vectors.mjs`
- **Isolation**: Pure computations; the fixture is read only

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any theme variable whose text differs from Mermaid's, and any paint other than the asserted one, constitutes
a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Configuration-ThemeResolver-FiveThemes: Every Variable Equals Mermaid's

**Tests**: `ThemeResolverTests.ThemeResolver_GetThemeVariables_MatchesMermaid`,
`ThemeResolverTests.ThemeResolver_GetThemeVariables_DefaultTheme_FlowchartColors`

Asserts, for the 15 recorded site cases (five themes, each plain, with theme variables, and in dark mode), that every
variable equals Mermaid's, and the well-known flowchart colors of the default theme (`#ECECFF`, `#9370DB`, `#ffffde`,
and the derived `hsl(240, 60%, 86.2745098039%)`).

##### CanvasNetMermaid-Configuration-ThemeResolver-Overrides: Overrides Are Derived And Kept

**Tests**: `ThemeResolverTests.ThemeResolver_GetThemeVariables_BaseOverride_DerivesAndKeepsOverrides`,
`ThemeResolverTests.ThemeResolver_GetThemeVariables_NonObjectOverrides_Ignored`

Asserts that a `primaryColor` override of the base theme becomes `mainBkg` and is derived into
`primaryBorderColor` (`hsl(0, 60%, 40%)`) while an overridden `nodeBorder` is kept, and that a string given as the
overrides gives the same variables as no overrides.

##### CanvasNetMermaid-Configuration-ThemeResolver-JavaScriptSemantics: The Theme Object Behaves As JavaScript

**Tests**: `ThemeResolverTests.ThemeObject_JavaScriptSemantics`

Asserts that `||` replaces a zero, `??` replaces undefined but keeps an empty string, a missing key and a member of a
string are undefined, the keys keep their insertion order, and the JSON form holds the values.

##### CanvasNetMermaid-Configuration-ThemeResolver-ThemeNames: Exactly Five Themes

**Tests**: `ThemeResolverTests.ThemeResolver_IsTheme_KnowsFiveThemes`

Asserts that every public theme name is a theme, that the newer Mermaid themes, `Dark`, and a number are not, and that
computing `neo` throws `ArgumentException`.

##### CanvasNetMermaid-Configuration-ThemeResolver-Bounded: Bounded Loops And Identified Failures

**Tests**: `ThemeResolverTests.ThemeResolver_GetThemeVariables_HugeColorLimit_IsBounded`,
`ThemeResolverTests.ThemeResolver_GetThemeVariables_NotAColor_ThrowsWithValue`

Asserts that a `THEME_COLOR_LIMIT` of 1e12 returns promptly with a `ColorFormatException` on the undefined scale color
(as khroma fails in Mermaid), and that a `primaryColor` of `nonsense` fails with that value.

##### CanvasNetMermaid-Configuration-ThemeResolver-FlowchartStyles: Mermaid's Flowchart Paint

**Tests**: `ThemeResolverTests.FlowchartStyles_Create_DefaultTheme_ResolvesPaint`

Asserts the default theme's node paint (`#ECECFF` fill, `#9370DB` stroke, width 1), cluster paint, edge and marker
color (`#333333`), edge widths (1 and 3.5), node label and title fonts (16 and 18 pixels), the edge label background,
and the faded `.labelBkg` background.

##### CanvasNetMermaid-Configuration-ThemeResolver-ThemeCss: ThemeCSS Overrides Known Selectors

**Tests**: `ThemeResolverTests.FlowchartStyles_Create_ThemeCss_OverridesKnownSelectors`

Asserts that a `.node rect` rule of `themeCSS` replaces the fill and stroke width and keeps the stroke, that an
important theme declaration of `.root .anchor path` stays in effect, and that the unknown selector `.mine` is reported
once as `mermaid-theme-css-unsupported` at the directive.

##### CanvasNetMermaid-Configuration-ThemeResolver-InvalidDeclarations: Invalid Declarations Are Dropped

**Tests**: `ThemeResolverTests.FlowchartStyles_Create_InvalidVariable_DropsDeclaration`

Asserts that a site theme variable `nodeBorder` of `nonsense` is kept as a variable, but leaves the node stroke
undeclared while the node fill is unchanged.

##### CanvasNetMermaid-Configuration-ThemeResolver-SequenceStyleSheet: Mermaid's Sequence Paint

**Tests**: `ThemeResolverTests.DiagramStyles_Create_GlobalRules_SharedByFlowchartAndSequence`,
`ThemeResolverTests.SequenceStyles_Create_DefaultTheme_ResolvesCascade`,
`ThemeResolverTests.SequenceStyles_Create_EachTheme_UsesThemeVariables`,
`ThemeResolverTests.SequenceStyles_Create_ThemeCss_OverridesKnownSelectors`,
`ThemeResolverTests.SequenceStyles_TryParseFilter_ParsesDropShadow`

Asserts that the flowchart and sequence style sheets start with the same global rules, selector for selector and
declaration for declaration, from the root rule to `p`; that in the default theme a participant takes the theme's
`#ECECFF` fill and `#9370DB` stroke over its attributes, a note `#fff5ad` and `#aaaa33`, and an element of a class
without a rule keeps its attributes; that the `line` rule widens the lifeline and the block lines to 2 pixels in
`#9370DB` (the block lines dashed 2, 2), while the message rules give 1.5 pixels in `#333` (dashed 2, 2 when dotted);
that a box gets the drop shadow `1px 2px 2px` in `rgb(185, 185, 185)` and the node border, the menu panel its own
shadow, the label box none, the arrowhead, crosshead, and sequence-number markers `#333`, the sequence numbers white,
and a message text `#333` over its attribute while an unknown class keeps the attribute; that for each of the five
themes (a theory) the participant, lifeline, note, and activation paint and the box shadow come from the theme's
variables; that a `themeCSS` rule for `.actor` and `.actor-line` overrides the participant fill and the lifeline width
while the flowchart selector `.node rect` is reported once as `mermaid-theme-css-unsupported`; and that `none` and one
`drop-shadow()` (with a color first or last, two or three lengths, a negative offset) are read, while four lengths,
one length, non-zero numbers without units, a negative blur, a color between the lengths, a second filter function, a `url()`,
and `blur()` are not (a theory of 14 cases).

##### CanvasNetMermaid-Configuration-ThemeResolver-ClassStyleSheet: Mermaid's Class Diagram Paint

**Tests**: `ThemeResolverTests.ClassStyles_Create_DefaultTheme_ResolvesClassRules`,
`ThemeResolverTests.ClassStyles_Create_DefaultTheme_ResolvesNoteLabelsAndLabelBackgrounds`,
`ThemeResolverTests.ClassStyles_Create_ThemeCss_OverridesKnownSelectors`

Asserts that the class style sheet starts with the same global rules as the flowchart's, selector for selector; that
in the default theme the class box takes the `#ECECFF` fill and `#9370DB` stroke, a namespace `#ffffde` and `#aaaa33`,
the node and edge labels the root font at 16 pixels in `classText` (`#131300`), the title the root font in `#333` (its
class has no rule), the dividers a `#9370DB` stroke without fill, and a terminal label 11 pixels in `#131300` for an
HTML label and `#333` for an SVG label, while the composition fill and the dependency stroke are `#333333`, the
extension fill `transparent`, and the lollipop fill `#ECECFF`; that a note label takes the theme's `noteTextColor`,
that the HTML label backgrounds are `.labelBkg` and `.edgeLabel .label span` and the SVG one `.edgeLabel .label rect`
(filled `#ECECFF`), while a flowchart keeps its note label equal to its node label and its own backgrounds
`.labelBkg`, `.edgeLabel p`, and `.edgeLabel rect`; and that a directive's `themeCSS` rules for `.divider`,
`.composition`, and `.edgeTerminals` are applied without a report, the divider stroke becoming `#ff0000` and the
terminal label 14 pixels, while the composition fill stays `#333333` against the theme's `!important`.

##### CanvasNetMermaid-Configuration-ThemeResolver-StateStyleSheet: Mermaid's State Diagram Paint

**Tests**: `ThemeResolverTests.StateStyles_Create_DefaultTheme_ResolvesStateRules`,
`ThemeResolverTests.StateStyles_Create_ThemeCss_OverridesKnownSelectors`

Asserts that the state style sheet starts with the same global rules as the flowchart's, selector for selector; that
in the default theme a state takes the `#ECECFF` fill and `#9370DB` stroke at width 1, the node labels the root font at
16 pixels in `stateLabelColor`, the transition labels 16 pixels in `transitionLabelColor` (else
`tertiaryTextColor`), the note labels `noteTextColor`, and the title 18 pixels in `#333`; that a composite state's
title is `stateLabelColor` as an HTML label and `#333` (the root fill) as SVG text; that the CSS radius of
`.statediagram-state rect.basic` is `5px` and that of `.statediagram-cluster .inner` `0`; that the inner rectangle of a
composite state is filled `#ffffff` and that of a concurrency region `#f0f0f0`; that the fill of
`defs [id$="-barbEnd"]` is the theme's `transitionColor`, and that `.note-edge` is dashed `5`; and that a directive's
`themeCSS` rules `.statediagram-cluster rect { fill: #00ff00 }` and `.transition { stroke: #ff0000 }` are applied
without a report, the composite state fill becoming `#00ff00` and the transition stroke `#ff0000`.

##### CanvasNetMermaid-Configuration-ThemeResolver-ErStyleSheet: Mermaid's Entity Relationship Diagram Paint

**Tests**: `ThemeResolverTests.ErStyles_Create_DefaultTheme_ResolvesErRules`,
`ThemeResolverTests.ErStyles_Create_ThemeCss_KeepsTheImportantMarkerFill`

Asserts that the entity relationship style sheet starts with the same global rules as the flowchart's, selector for
selector; that in the default theme an entity takes the `#ECECFF` fill and `#9370DB` stroke at width 1, the node labels
16 pixels, the edge labels the root font at 14 pixels in the node label's color with the single HTML background
`.labelBkg`, and the title the root font at 16 pixels in `#333` (its class has no rule); that an HTML subgraph
title is black (`rgb(0, 0, 0)`) and an SVG one the theme's `titleColor`; that `.marker` has the important fill `none`
and the important stroke `#333333`, and that `.edge-pattern-dashed` is dashed `8,8`; and that a directive's `themeCSS`
rules `.marker { fill: #ff0000 }` and `.relationshipLine { stroke: #0000ff }` give no report, the marker fill staying
`none` against the theme's `!important` while the relationship line stroke becomes `#0000ff`.

##### CanvasNetMermaid-Configuration-ThemeResolver-SheetFacts: The Text Facts Of Each Unified Sheet

**Tests**: `ThemeResolverTests.UnifiedStyles_Create_EachSheet_CentersSvgNodeTextAndGivesTheRootText`

Asserts, for the default theme's flowchart, class, state, and entity relationship sheets in that order, that only the
first two center the SVG text of a node's label on its anchor (true, true, false, false); that the entity relationship
sheet's root text is the root font family at 16 pixels in the theme's `textColor`; and that the root text of every
sheet is 16 pixels.
