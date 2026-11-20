### LayoutWorkMeter Unit Verification Design

This document describes the unit-level verification strategy for the `LayoutWorkMeter` unit in the
`DemaConsulting.CanvasNet.Layout.Dagre` namespace: the internal abstract `LayoutWorkMeter` class (the protected
constructor, `Charge`, `UniqueId`, `MaxWork`, `Used`, and the abstract `Failure`, `LimitExceeded`, and `IsFailure`).

#### Verification Approach

The unit is abstract, so it is verified through the test consumer `TestLayoutWorkMeter` (in `Dagre/`), whose failures
are `LayoutTestException` values that record whether they are a limit failure and the budget of the meter; it stands in
for the meters of the diagram packages, whose own failures are verified with those packages (Mermaid's
`MermaidLayoutWorkMeter` in *MermaidDocument Unit Verification Design*). A meter with a limit of 10 is charged up to and
past the limit, a meter with a canceled token is charged, names are drawn with several prefixes, a limit of 0 is given
to the constructor, a failure is created with a description, the meter classifies the consumer's failures and other
exceptions, and a layout past its budget shows that the consumer's limit failure passes through `DagreLayout.Run`
unchanged. The tests assert the exception types, the kind of failure, the budget, the message, the work used, and the
exact names. The work that the layout stages charge is verified by the pinned work counts of the subsystem tests. No
mocking framework is used; the internal type is reached through the test assembly's `InternalsVisibleTo` access.

Unit tests reside in `LayoutWorkMeterTests.cs` within the `DemaConsulting.CanvasNet.Layout.Tests` project, in the
`Dagre` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; the test consumer `TestLayoutWorkMeter` derives the abstract meter
- **Isolation**: Each test creates its own meter and, where needed, its own cancellation source and graph

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. A charge accepted past the limit, a failure that is not the one the consumer created, a consumer failure
wrapped by the layout, or a name out of sequence constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetLayout-Dagre-LayoutWorkMeter-Budget: Work Is Bounded By The Limit

**Tests**: `LayoutWorkMeterTests.LayoutWorkMeter_Charge_PastLimit_ThrowsConsumerLimitFailure`,
`LayoutWorkMeterTests.LayoutWorkMeter_Constructor_ZeroLimit_Throws`

Asserts that charges up to a limit of 10 are accepted and that the next charge throws the consumer's limit failure
(a `LayoutTestException` marked as a limit failure with the budget 10), with 10 steps used; and that a limit of 0 throws
`ArgumentOutOfRangeException`.

##### CanvasNetLayout-Dagre-LayoutWorkMeter-Cancellation: A Canceled Call Stops At The Next Charge

**Tests**: `LayoutWorkMeterTests.LayoutWorkMeter_Charge_Canceled_ThrowsOperationCanceled`

Asserts that a charge to a meter whose token is canceled throws `OperationCanceledException`.

##### CanvasNetLayout-Dagre-LayoutWorkMeter-UniqueIds: Names Share One Counter

**Tests**: `LayoutWorkMeterTests.LayoutWorkMeter_UniqueId_Prefixes_ShareOneCounter`

Asserts that names drawn with the prefixes `_d`, `rev`, and `_d` from a new meter are `_d1`, `rev2`, and `_d3`.

##### CanvasNetLayout-Dagre-LayoutWorkMeter-Failure: The Consumer Creates And Recognizes The Failures

**Tests**: `LayoutWorkMeterTests.LayoutWorkMeter_Failure_Description_CreatedByConsumer`,
`LayoutWorkMeterTests.LayoutWorkMeter_IsFailure_ConsumerAndOtherExceptions_Classified`

Asserts that the failure created with a description, called through the abstract base, is the consumer's render
failure (a `LayoutTestException` that is not a limit failure) and that its message contains the description; and that
the meter recognizes the consumer's failure but not another `InvalidOperationException`, which the layout therefore
wraps.

The pass-through of the consumer's failures through the layout is verified with the DagreLayout unit (scenario
*CanvasNetLayout-Dagre-DagreLayout-Failures*, test `LayoutWorkMeter_IsFailure_ConsumerFailureInLayout_PassesUnchanged`):
a two-node graph laid out with a budget of one step throws the consumer's limit failure itself, not a render failure
that wraps its message.
