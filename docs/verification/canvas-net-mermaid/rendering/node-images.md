### NodeImages Unit Verification Design

This document describes the unit-level verification strategy for the `NodeImages` unit in the
`DemaConsulting.CanvasNet.Mermaid.Rendering` namespace: the internal sealed `NodeImages` class (`NaturalSize` for the
layout and `Load` for the painter), which loads the pictures of one rendering only through the caller's image
resolver.

#### Verification Approach

The unit is verified with a counting test resolver, a private `IImageResolver` of the test class that records the sizes
it is asked for and returns a surface of a fixed size (or nothing when the size is zero), and with a real
`MermaidReportSink` whose warnings are delivered to a list. The tests assert the resolver calls (how many, at which
size), the sizes returned, the warnings with their codes and locations, and the limit failure. The test resolver is
the only stand-in: it replaces the host's resolver so that the calls can be counted, which is the unit's contract; the
internal type is reached through the test assembly's `InternalsVisibleTo` access.

Unit tests reside in `NodeImagesTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the
`Rendering` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: A counting test image resolver in place of the host's resolver
- **Isolation**: Each test creates its own instance, resolver, and sink; no files are read

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. A resolver call that is not expected (or is missing), a natural size other than the returned picture's, a
missing or repeated report, or a limit that is not enforced constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Rendering-NodeImages-Resolver: Pictures Come Only From The Resolver

**Tests**: `NodeImagesTests.NodeImages_NaturalSize_NoResolver_ReportsOnceAndGivesNull`,
`NodeImagesTests.NodeImages_NaturalSize_ResolverGivesNothing_Reports`

Asserts that without a resolver two questions for one link give no size, make no call, and report the link once as
`mermaid-image-unresolved` at the location of the first question, and that a link for which the resolver gives no
picture has no size and is reported.

##### CanvasNetMermaid-Rendering-NodeImages-NaturalSize: The Natural Size Is Asked For Once

**Tests**: `NodeImagesTests.NodeImages_NaturalSize_Resolver_AsksOnce`

Asserts that the natural size is the size of the picture the resolver returns (40 by 30), that the resolver is asked
once, at 256 by 256 pixels, and that a second question for the same link gives the same size without a call.

##### CanvasNetMermaid-Rendering-NodeImages-DeviceSize: Pictures Are Loaded At The Drawn Size

**Tests**: `NodeImagesTests.NodeImages_Load_Resolver_AsksAtDeviceSize`

Asserts that a load for the painter asks the resolver once at the device size it is drawn at (80 by 60) and returns
the picture.

##### CanvasNetMermaid-Rendering-NodeImages-MaxImages: The Resolver Calls Are Bounded

**Tests**: `NodeImagesTests.NodeImages_NaturalSize_BeyondMaxImages_ThrowsLimitExceeded`

Asserts that with a limit of 2 the third link fails with `mermaid-limit-exceeded`, located at the element that named it
and naming `MaxImages`, and that the resolver was called only twice.
