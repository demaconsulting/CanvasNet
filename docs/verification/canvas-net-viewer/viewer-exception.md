## ViewerException Unit Verification Design

<!-- cspell:ignore canvasnet -->

### Verification Approach

`ViewerException` and `ErrorCodes` are verified with unit tests in `ViewerExceptionTests.cs` in the Viewer test project.
The tests create the exception with all and with only the required constructor arguments and read the properties back,
and pin every error code to its documented string, so a rename is a test failure. The behavior of the rest of the library
under failure (which code is thrown where) is verified with the tests of each unit; the way the `canvasnet` tool reports
the codes is verified with the tests of `Program` and `ResultWriter`, and a test of the tool cross-checks the codes against
its `errors` help topic.

### Test Environment

N/A - standard test environment; the tests use no files.

### Acceptance Criteria

- All unit tests pass with zero failures.
- The code, message, hint, and inner exception are kept as given, and the hint and the inner exception are optional.
- The 17 constants have the documented strings.

### Test Scenarios

**ViewerException_Constructor_CarriesCodeMessageHintAndCause**: the code, message, hint, and inner exception given to the
constructor are the ones the properties return, and the type is an `InvalidOperationException`.

**ViewerException_Constructor_HintAndCauseAreOptional**: with only a code and a message, the hint and the inner exception
are `null`.

**ErrorCodes_Constants_AreTheDocumentedStableStrings**: each of the 17 constants equals its string
(`usage-error`, `operation-failed`, `input-not-found`, `input-unreadable`, `unsupported-format`, `corrupt-input`,
`unsupported-feature`, `page-out-of-range`, `size-limit`, `same-input-output`, `output-exists`, `output-write-failed`,
`scad-error`, `vega-lite-error`, `budget-exceeded`, `size-mismatch`, `threshold-failed`).
