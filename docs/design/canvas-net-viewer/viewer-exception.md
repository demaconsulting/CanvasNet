## ViewerException Unit Design

<!-- cspell:ignore canvasnet -->

![CanvasNetViewer Structure](CanvasNetViewerView.svg)

### Purpose

`ViewerException` is the exception with which every expected failure of the Viewer library is reported, and `ErrorCodes`
holds the stable strings that name those failures. A failure is expected when the user can correct it: a file that does
not exist or is not a supported image, a page that is not there, an input that is too large, an output that cannot be
written, two inputs that cannot be compared. The unit is the class `ViewerException` and the static class `ErrorCodes`
(`ViewerException.cs`). The `canvasnet` tool derives its own `ToolException` from it to add the name of the command and
the fields of a result, and reports the code, the message, and the hint of every `ViewerException`.

### Data Model

**ViewerException** (public, not sealed, derives from `InvalidOperationException`): `Code` (one of the `ErrorCodes`),
`Message`, `Hint` (a sentence that says what to do, or `null`), and the inner exception (or `null`). The constructor is
`ViewerException(code, message, hint = null, inner = null)`.

**ErrorCodes** (static): the constants `UsageError`, `OperationFailed`, `InputNotFound`, `InputUnreadable`,
`UnsupportedFormat`, `CorruptInput`, `UnsupportedFeature`, `PageOutOfRange`, `ImageFragmentInvalid`, `SizeLimit`,
`SameInputOutput`,
`OutputExists`, `OutputWriteFailed`, `ScadError`, `VegaLiteError`, `MermaidError`, `BudgetExceeded`, `SizeMismatch`, and
`ThresholdFailed`, with the strings `usage-error`, `operation-failed`, `input-not-found`, `input-unreadable`,
`unsupported-format`, `corrupt-input`, `unsupported-feature`, `page-out-of-range`, `image-fragment-invalid`,
`size-limit`, `same-input-output`,
`output-exists`, `output-write-failed`, `scad-error`, `vega-lite-error`, `mermaid-error`, `budget-exceeded`,
`size-mismatch`, and `threshold-failed`. The strings are the contract: they are not changed between releases, and the
`errors` help topic of the tool lists them.

### Key Methods

**ViewerException(code, message, hint, inner)**: stores the values; it does no validation, because an exception must
never fail while it is being created.

### Error Handling

N/A - the unit is the error contract; it throws nothing.

### Dependencies

N/A - the unit depends only on the .NET base class library.

### Callers

- **SourceLoader**, **ImageWriter**, **ImagePipeline**, **ContactSheet**, **ImageRenderer**, **ImageInspector**,
  **ImageComparer**, and **MontageBuilder** - throw it for every expected failure.
- **Program**, **ResultWriter**, and **CommandRegistry** (of the tool) - catch it and report the code, the message, and
  the
  hint; the registry wraps one thrown by a library call in a `ToolException` that names the command.
- **Library users** - catch it and branch on `Code`.
