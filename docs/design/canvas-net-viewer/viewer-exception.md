## ViewerException Unit Design

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
`OutputExists`, `OutputWriteFailed`, `ScadError`, `VegaLiteError`, `MermaidError`, `PlantUmlError`, `BudgetExceeded`,
`FileAccessDenied`,
`SizeMismatch`, `ThresholdFailed`,
`PositionsUnmatched`, `SvgBindingFailed`, and `PandocFilterError`, with the strings `usage-error`, `operation-failed`,
`input-not-found`, `input-unreadable`,
`unsupported-format`, `corrupt-input`, `unsupported-feature`, `page-out-of-range`, `image-fragment-invalid`,
`size-limit`, `same-input-output`,
`output-exists`, `output-write-failed`, `scad-error`, `vega-lite-error`, `mermaid-error`, `plantuml-error`,
`budget-exceeded`,
`file-access-denied`, `size-mismatch`, `threshold-failed`, `positions-unmatched`, `svg-binding-failed`, and
`pandoc-filter-error`. The
strings are the contract: they
are not changed
between releases, and the
`errors` help topic of the tool lists them. `file-access-denied` names a file the rendered document references (a
model's include, a chart's data, or with the strict option a picture) that the caller's file-access policy denied, or
could not judge because the real path of the file cannot be determined; for an include or data file its hint is "Allow
the file in the file-access policy, or reference a file it allows." `positions-unmatched` names a feature-position
selector that matched no feature of a diagram or chart when the strict option was given (without it, the same code is
a warning). `svg-binding-failed` names an SVG position binding that was not resolved when the strict option was
given; its message names the warning code (`svg-binding-unmatched`, ...), the element, and the value.
`pandoc-filter-error` is used only by the `pandoc-filter` command of the tool: a picture of a Pandoc document with an
attribute that is not valid, a source that is not in the resource path, too many files or levels, a variable of the
filter that is not valid, or a cache file that cannot be written; its message names the picture.

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
