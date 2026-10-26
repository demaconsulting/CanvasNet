## ResultWriter

<!-- cspell:ignore canvasnet Postconditions -->

![CanvasNetTool Structure](CanvasNetToolView.svg)

### Purpose

`ResultWriter` produces the tool's machine-readable results. Its single responsibility is to turn a command's
outcome into one deterministic JSON document in a versioned envelope, so that scripts and AI agents have a stable
contract to parse instead of text meant for people.

### Data Model

`ResultWriter` is `internal static` and holds no state apart from its constants and writer options.

**SchemaVersion**: `const int` - 1. The documented schema is versioned by this number: adding a key is not a breaking
change; removing or renaming a key, or changing its meaning, increments it.

**Document layout**. Every document is a single JSON object on a single line:

<!-- markdownlint-disable MD013 -->
| Document | Keys, in order |
| -------- | -------------- |
| success | `schemaVersion` (number), `ok` (`true`), `command` (string), then the command's own fields |
| failure | `schemaVersion`, `ok` (`false`), `command` (string, or `null` when no command was selected), `error` (object: `code`, `message`, and `hint` only when there is one) |
<!-- markdownlint-enable MD013 -->

A failure document has no exit-code field: every failure exits with code 1.

### Key Methods

**Success(command, writeFields)**: writes the envelope and then calls `writeFields` with the underlying
`Utf8JsonWriter` positioned inside the top-level object, so a command writes its own properties directly.

- *Parameters*: `string command`; `Action<Utf8JsonWriter> writeFields` (must write complete properties only).
- *Returns*: `string` - the document, without a trailing newline.
- *Preconditions*: neither argument is null.

**Failure(command, code, message, hint)**: writes the failure document. An overload with a fifth parameter,
`Action<Utf8JsonWriter>? resultFields`, calls it after the `error` object so that the document also carries the
fields of a result (it must write complete properties only); the four-parameter form passes `null`.

**Failure(command, exception)**: derives the code from the exception: a `ToolException` contributes its own code and
hint and its `ResultFields`; an `ArgumentException` is `usage-error`; anything else is `operation-failed`. A failure
document with result fields is `{"schemaVersion":1,"ok":false,"command":...,"error":{...},<result fields>}`: the
fields are additive and follow the error object, so a consumer of the plain failure document is unaffected.

**Write** (private): writes the top-level object with `Utf8JsonWriter` using `Indented = false` and the relaxed
escaping encoder (so ASCII text such as an apostrophe stays readable), then escapes every non-ASCII UTF-16 code unit
as `\uXXXX`. The result is ASCII-only, so it is identical on every console code page and in every pipe; a surrogate
pair becomes two escapes, which JSON parsers combine again.

Determinism: key order is the order the code writes; `Utf8JsonWriter` formats numbers with the invariant culture
whatever the current culture is; there is no time stamp or other varying data. The same input is the same text.

### Error Handling

Null arguments throw `ArgumentNullException`. The methods do not catch exceptions from `writeFields`. Text that
cannot be encoded as UTF-16 (an unpaired surrogate) makes the underlying writer throw; messages the tool produces do
not contain such text.

### Dependencies

- **ToolException** and **ErrorCodes** (Program unit) - the code and hint of an expected failure.
- **.NET BCL** - `System.Text.Json.Utf8JsonWriter`.

### Callers

- **Program** - writes the failure document for an expected failure when `--json` is on the command line.
- **Commands** - each command that supports `--json` writes its success document through `Success`.
- **Validation** - checks the structure of the formats document.
