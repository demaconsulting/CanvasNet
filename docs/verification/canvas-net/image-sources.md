## ImageSources Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `ImageSources` subsystem (the
`ImageSourceContract`, `FolderImageResolver`, and `RasterImageSource` units and their supporting types).

### Verification Approach

The `ImageSources` subsystem is verified through its three units' tests (see _ImageSourceContract Unit Verification
Design_,
_FolderImageResolver Unit Verification Design_, and _RasterImageSource Unit Verification Design_, in the `image-sources`
folder): every public constructor, property, and method is exercised in isolation with stub sources that stand in for
the
formats, so no format package is needed in the core test project. The subsystem-level requirements reuse those unit
tests
as verification evidence, because the units' only collaboration - `FolderImageResolver` calling `IImageSource`
implementations
through `ImageSourceSet` - is exercised by the `FolderImageResolver` tests with real `ImageSourceSet` instances and a
real
`RasterImageSource` is covered by its own tests.

The end-to-end behavior with the real format packages (an SVG document that embeds a raster, a model, a chart, another
SVG
file, and a PDF page; a custom source for a new extension; a custom source that overrides a built-in extension; a custom
scheme-claimed live source) is verified by the system tests of the `CanvasNetViewer` system, because only that system
references every format package.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None; the stub sources are real implementations of `IImageSource` that record every request
- **Files**: per-test temporary folders, deleted by the test; the GIF and JPEG 2000 fixtures of the core test project
- **Capabilities**: the file-symbolic-link scenarios skip where a file link cannot be created (Windows without the
  privilege); the directory-link scenarios use a junction on Windows and never skip; the drive-path scenario asserts the
  outcome of each operating system

### Acceptance Criteria

The `ImageSources` subsystem's verification passes when every unit test scenario described in the three unit
verification
designs passes without error or unexpected exception.

### Test Scenarios

The subsystem's test scenarios are those named in the unit verification designs: the validation of claims and the
precedence rule, the request and the sizing rule, the file-access policy (the default that follows every local link,
the explicit roots and no-symlinks policies, and the policy a request carries to its source), file URIs, dispatch,
schemes, nested scopes, limits,
cancellation, notes and failures, no caching, direct rendering, and the raster formats. No subsystem-level test exists
beyond
the units' own coverage.
