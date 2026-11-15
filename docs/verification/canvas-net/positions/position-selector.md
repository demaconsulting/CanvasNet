### PositionSelector Unit Verification Design

This document describes the unit-level verification strategy for the `PositionSelector` unit, in the
`DemaConsulting.CanvasNet.Positions` namespace: the parser and matcher of one selector (`PositionSelector`) and the
union of several (`PositionSelection`).

#### Verification Approach

The unit is verified by parsing selector texts and matching them against fixed positions built by hand: a node `send`
with the text `Send`, a node whose id `my*odd id` holds glob characters and a space, an edge `L_A_B_0` from `A` to `B`
with index 0, and a bar mark of the view `hconcat[1]` with the values `x` = `Nov`, `color` = `Americas`, `y` = 3, and
`size` = null. Refusals are asserted on the exception type and on the selector text in the message. The unit depends
only on the `ImagePosition` unit and on the bare-value reader `ImageParameterValues` of the `ImageSources` subsystem,
which are real, so no mocking or stubbing is required. Tests reside in `PositionSelectorTests.cs` in the `Positions`
folder of the `DemaConsulting.CanvasNet.Tests` project.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Mocking**: None required
- **Isolation**: Each test parses its own selectors; the fixed positions are immutable

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. A selector that matches a position it should not or misses one it should, a malformed selector that is
accepted, a refusal that does not name the selector, or a limit that is off by one constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNet-Positions-PositionSelector-Grammar: The Documented Grammar Is Parsed

**Tests**: `PositionSelector_Matches_Examples_MatchAsDocumented`,
`PositionSelector_Matches_PlusAndEqualsInsideQuotes_ArePartOfTheValue`,
`PositionSelector_Parse_Malformed_ThrowsFormatException`

Asserts, as a theory over 33 selectors, that `all`, a kind, a kind with an id pattern, a quoted exact id (with a JSON
escape), predicates on channels and on `@id`, `@view`, `@from`, `@to`, and `@index`, and quoted, single-quoted,
`null`, and bare values each match exactly the expected fixed positions; that the value `"a+b=c"` matches a mark whose
text value is `a+b=c`; and, as a theory over 26 texts (an empty selector, `all:x`, a bad kind, an empty target, an
open or trailing quote, a bad escape, an empty or missing predicate, a bad or unknown name, a repeated name, a list or
object value, an empty value, and a negative, text, or quoted index), that each throws `FormatException` whose message
names the selector and that `TryParse` returns false with a problem.

##### CanvasNet-Positions-PositionSelector-Matching: Keywords Forgive Case And Data Matches Exactly

**Tests**: `PositionSelector_Matches_Examples_MatchAsDocumented`,
`PositionSelector_Matches_BacktrackingGlob_MatchesCorrectly`

Asserts, within the example theory, that `ALL`, `Node`, `@FROM`, and `@To` match while `node:Send`, `edge:@from=a`,
and the channel `X` do not; that `y=3` and `y=3.0` match the number 3 and `y="3"` does not; that `size=null` matches
the null value and `shape=null` (a missing channel) does not; and that `@id=L_*` does not glob. Asserts that the
patterns `*a*a*a*a*a*b` and `a*?b` match an id of 200 `a` characters followed by `b`, that `*a*a*a*a*a*c` does not,
and that `?` does not match an empty id while `*` does.

##### CanvasNet-Positions-PositionSelector-DataFields: A Channel Name Reads The Values, Any Other Name The Data

**Tests**: `PositionSelector_Matches_ChannelOrDataField_FollowsTheChannelNames`

Asserts, as a theory over 12 selectors against a time-unit chart point (values `color` = `Americas`, `x` =
`2012-11-01T00:00:00.000Z`, `y` = 6.8; data `month` = `2025-11-01`, `region`, `revenue` = 6.8, `time` = `t1`, and
`shape` = `circle`; channel names `x`, `y`, `color`, `time`, and `shape`) and a pie-like slice without channel names
(values `value` = 3; data `label` = `A` and `value` = 4), that `month=2025-11-01+region=Americas` and
`month=2025-11-01+color=Americas` select the point while `x=2025-11-01+color=Americas` does not; that `x` matches its
2012 value; that `revenue=6.8` matches and `revenue="6.8"` does not; that `time=t1` and `shape=circle` match nothing
(they are channels the point does not have, so its data fields of those names are not read); that `value=3` and
`label=A` select the slice (its value, then its data), and so does both together; and that a missing name never
matches.

##### CanvasNet-Positions-PositionSelector-Limits: Selectors And Selections Are Bounded

**Tests**: `PositionSelector_Parse_OverLimits_ThrowsFormatException`,
`PositionSelection_Parse_BadSelections_ThrowFormatException`

Asserts that a selector of exactly `MaxSelectorLength` (256) characters and one of exactly `MaxPredicates` (16)
predicates are parsed and that one character or one predicate more throws `FormatException`; and that an empty
selection and a selection of `MaxSelectors` + 1 (33) selectors throw `FormatException`, a selection of 32 selectors is
parsed, and a selection with a malformed selector throws `FormatException` naming it.

##### CanvasNet-Positions-PositionSelector-Union: A Selection Is The Union Of Its Selectors

**Tests**: `PositionSelection_Parse_DuplicatesAndKinds_UnionAndKindGate`

Asserts that the selection `node:send`, `edge`, `node:send` keeps the selectors `node:send` and `edge`, matches the
node and the edge but not the other node, includes the kind `node` and not `mark`, and that `PositionSelection.All`
includes `mark`.
