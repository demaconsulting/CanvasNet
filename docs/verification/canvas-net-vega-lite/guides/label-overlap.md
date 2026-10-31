### LabelOverlap Unit Verification Design

This document describes the unit-level verification strategy for the `LabelOverlap` unit in the
`DemaConsulting.CanvasNet.VegaLite.Guides` namespace: the internal static `LabelOverlap` class (`Hidden` and
`FromSetting`). The unit was extracted unchanged from `AxisBuilder`; the existing `AxisBuilderTests` scenario for label
overlap passes unchanged and is the regression check of the extraction.

#### Verification Approach

The unit is verified with labels built by hand: the text `1000` at 10 pixels is four digits of 0.556 em, that is 22.25
pixels wide with the bundled font, so two labels `d` pixels apart overlap when `d` is less than 22.25 and the gap between
labels 30 pixels apart is 7.75 pixels. Five labels 10 pixels apart: parity keeps the labels at 0, 20, and 40 (which still
overlap at 20 pixels), then at 0 and 40, so the labels at 10, 20, and 30 are hidden. The greedy method on labels at 0, 10,
30, 40, and 70 keeps 0, hides 10 (it overlaps 0, which ends at 22.25), keeps 30 (clear of 0), hides 40 (it overlaps 30,
which ends at 52.25), and keeps 70. Four overlapping labels at 0, 5, 10, and 15: parity keeps the first and the third and
would hide the fourth, so the last label replaces the third and the labels at 5 and 10 are hidden. Three labels 30 pixels
apart are clear with a separation of 7 and overlap with a separation of 8.

The unit has no dependency that needs mocking: the text is measured by a real `TextMeasurer` with the bundled fonts, and
the internal type is reached through the test assembly's `InternalsVisibleTo` access. Unit tests reside in
`LabelOverlapTests.cs` within the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the `Guides` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Pure functions of labels and numbers; the bundled Liberation fonts only; no files, clock, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any label that is hidden or kept differently from the expectation constitutes a failure.

#### Unit-Level Test Scenarios

##### Parity Rounds

**Tests**: `LabelOverlapTests.LabelOverlap_Hidden_ParityKeepsEveryOtherLabelUntilNothingOverlaps`

Asserts that the labels at 10, 20, and 30 pixels are hidden and those at 0 and 40 stay.

##### Greedy Pass

**Tests**: `LabelOverlapTests.LabelOverlap_Hidden_GreedyHidesLabelsThatOverlapTheLastOneKept`

Asserts that exactly the labels at 10 and 40 pixels are hidden.

##### The Last Label Stays

**Tests**: `LabelOverlapTests.LabelOverlap_Hidden_TheLastLabelIsKeptVisible`

Asserts that the last of four overlapping labels is visible and the third is hidden in its place.

##### Nothing To Hide

**Tests**: `LabelOverlapTests.LabelOverlap_Hidden_NoMethodFewLabelsOrNoOverlapHidesNothing`,
`LabelOverlapTests.LabelOverlap_Hidden_LabelsWithoutContentAreSkipped`,
`LabelOverlapTests.LabelOverlap_FromSetting_ReadsBooleansAndMethodNames`

Asserts that no method, fewer than three labels, labels that do not overlap (with a separation of 7 and of 0), and
labels without content hide nothing, that a separation of 8 hides a label, and the reading of the setting (true,
`parity`, `greedy`, false, other text, and absent).

##### Cancellation And Determinism

**Tests**: `LabelOverlapTests.LabelOverlap_Hidden_PollsCancellationAndIsDeterministic`

Asserts that a canceled token stops the removal when a round is needed and that two runs hide the same labels.
