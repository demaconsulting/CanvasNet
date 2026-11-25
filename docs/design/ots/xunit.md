## xUnit

### Purpose

xUnit v3 (`xunit.v3` and `xunit.runner.visualstudio`) is used as the unit-testing framework for
CanvasNet. It was chosen as the standard, widely supported .NET test framework
that discovers and executes `[Fact]`/`[Theory]` test methods and produces TRX results consumable
by downstream tooling.

### Features Used

- Test discovery and execution of `[Fact]`-annotated test methods in
  `DemaConsulting.CanvasNet.Tests`
- TRX result-file generation, consumed by ReqStream for requirements-to-test traceability
- The `Category=Platform` trait (`[Trait("Category", "Platform")]`) and the trait filter of the xUnit v3
  Microsoft Testing Platform runner (`--filter-trait "Category=Platform"`), which select the Platform subset that
  the CI build runs on the frameworks other than net10.0

### Integration Pattern

xUnit is invoked indirectly through `dotnet test` in `build.ps1` and in the test jobs of
`.github/workflows/build.yaml`; there is no direct CLI invocation of an `xunit` tool by name.
`build.ps1` runs every test on every target framework. The CI build runs every test on net10.0 and the Platform
subset on net8.0, net9.0, and net11.0; its `full-test-matrix` input, which the release workflow and the weekly
scheduled build set, runs every test on every framework.
It is a stateless test-run invocation — each run discovers tests in the compiled test assembly,
executes them, writes TRX output, and exits; there is no initialization, configuration object, or
disposal step beyond the standard .NET test-project configuration (`.csproj` test SDK settings).
