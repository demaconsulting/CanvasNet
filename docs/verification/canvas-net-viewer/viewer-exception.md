## ViewerException Unit Verification Design

<!-- cspell:ignore canvasnet -->

### Verification Approach

`ViewerException` and `ErrorCodes` are verified with unit tests in `ViewerExceptionTests.cs` in the Viewer test project.
The tests create the exception with all and with only the required constructor arguments and read the properties back,
and pin every error code to its documented string, so a rename is a test failure. The behavior of the rest of the
library under failure (which code is thrown where) is verified with the tests of each unit; the way the `canvasnet` tool
reports the codes is verified with the tests of `Program` and `ResultWriter`, and a test of the tool cross-checks the
codes against its `errors` help topic. The code `positions-unmatched` is verified where it is used, by the position
tests of the `SourceLoader` unit (`Imaging/SourceLoaderPositionTests.cs`) and of the `CanvasNetViewer` unit
(`CanvasNetViewerPositionTests.cs`).

### Test Environment

N/A - standard test environment; the tests use no files.

### Acceptance Criteria

- All unit tests pass with zero failures.
- The code, message, hint, and inner exception are kept as given, and the hint and the inner exception are optional.
- The 17 constants have the documented strings.

### Test Scenarios

**ViewerException_Constructor_CarriesCodeMessageHintAndCause**: the code, message, hint, and inner exception given to
the constructor are the ones the properties return, and the type is an `InvalidOperationException`.

**ViewerException_Constructor_HintAndCauseAreOptional**: with only a code and a message, the hint and the inner
exception are `null`.

**ErrorCodes_Constants_AreTheDocumentedStableStrings**: each of the 20 constants equals its string
(`usage-error`, `operation-failed`, `input-not-found`, `input-unreadable`, `unsupported-format`, `corrupt-input`,
`unsupported-feature`, `page-out-of-range`, `image-fragment-invalid`, `size-limit`, `same-input-output`,
`output-exists`, `output-write-failed`,
`scad-error`, `vega-lite-error`, `mermaid-error`, `budget-exceeded`, `file-access-denied`, `size-mismatch`,
`threshold-failed`).

**CanvasNetViewer-ViewerException-PositionsUnmatched**: verified by
`SourceLoaderPositionTests.SourceLoader_Load_UnmatchedSelector_WarnsOrFailsWhenStrict` and
`CanvasNetViewerPositionTests.CanvasNetViewer_Render_UnmatchedSelector_NoteOrStrictFailure`, where the code is used:
`positions-unmatched` is the code of the warning that names a selector that matched nothing (`node:Zed` of a
flowchart) and of the `ViewerException` thrown under the strict option, from `SourceLoader.Load` for a diagram and for a
chart (`axis:z`) and from `CanvasNetViewer.Render` with `ThrowOnUnsupportedFeature`.

**CanvasNetViewer-ViewerException-SvgBindingFailed**: verified by
`SourceLoaderSvgBindingTests.SourceLoader_Load_SvgBindingUnmatched_WarnsOrFailsStrictly` and
`SvgFlattenerBindingTests.SvgFlattener_Execute_StrictUnresolvedBinding_WritesNothing`, where the code is used:
`svg-binding-failed` is the code of the `ViewerException` thrown under the strict option for an SVG position binding
that was not resolved, by a load and by a flatten that then writes nothing;
`ErrorCodes_Constants_AreTheDocumentedStableStrings` asserts the string.

**CanvasNetViewer-ViewerException-PandocFilterError**: verified by
`ErrorCodes_Constants_AreTheDocumentedStableStrings`, which asserts that `ErrorCodes.PandocFilterError` is
`pandoc-filter-error`; the tool's tests of the `pandoc-filter` command show where the code is used.
