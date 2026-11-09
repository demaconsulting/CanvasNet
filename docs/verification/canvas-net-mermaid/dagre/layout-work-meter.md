### LayoutWorkMeter Unit Verification Design

This document describes the unit-level verification strategy for the `LayoutWorkMeter` unit in the
`DemaConsulting.CanvasNet.Mermaid.Dagre` namespace: the internal sealed `LayoutWorkMeter` class (the constructor,
`Charge`, `UniqueId`, `Failure`, `LimitExceeded`, `MaxWork`, and `Used`).

#### Verification Approach

The unit is verified directly: a meter with a limit of 10 is charged up to and past the limit, a meter with a canceled
token is charged, names are drawn with several prefixes, a limit of 0 is given to the constructor, and a failure is
created with a description. The tests assert the exception types and codes, the location, the message and hint, the
work used, and the exact names. The work that the layout stages charge is verified by the pinned work counts of the
subsystem tests. No mocking or stubbing is used; the internal type is reached through the test assembly's
`InternalsVisibleTo` access.

Unit tests reside in `LayoutWorkMeterTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the
`Dagre` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test creates its own meter and, where needed, its own cancellation source

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. A charge accepted past the limit, an exception without its code or location, or a name out of sequence
constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Dagre-LayoutWorkMeter-Budget: Work Is Bounded By The Limit

**Tests**: `LayoutWorkMeterTests.LayoutWorkMeter_Charge_PastLimit_ThrowsLocatedLimitExceeded`,
`LayoutWorkMeterTests.LayoutWorkMeter_Constructor_ZeroLimit_Throws`

Asserts that charges up to a limit of 10 are accepted and that the next charge throws a `MermaidRenderException` with
the code `mermaid-limit-exceeded`, the meter's location, a message that names `MermaidLimits.MaxLayoutWork`, and a hint,
with 10 steps used; and that a limit of 0 throws `ArgumentOutOfRangeException`.

##### CanvasNetMermaid-Dagre-LayoutWorkMeter-Cancellation: A Canceled Call Stops At The Next Charge

**Tests**: `LayoutWorkMeterTests.LayoutWorkMeter_Charge_Canceled_ThrowsOperationCanceled`

Asserts that a charge to a meter whose token is canceled throws `OperationCanceledException`.

##### CanvasNetMermaid-Dagre-LayoutWorkMeter-UniqueIds: Names Share One Counter

**Tests**: `LayoutWorkMeterTests.LayoutWorkMeter_UniqueId_Prefixes_ShareOneCounter`

Asserts that names drawn with the prefixes `_d`, `rev`, and `_d` from a new meter are `_d1`, `rev2`, and `_d3`.

##### CanvasNetMermaid-Dagre-LayoutWorkMeter-Failure: A Layout Failure Is Located

**Tests**: `LayoutWorkMeterTests.LayoutWorkMeter_Failure_Description_RenderFailedAtLocation`

Asserts that the failure created with a description has the code `mermaid-render-failed`, the meter's location, and a
message that contains the description.
