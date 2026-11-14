---
title: The --json result documents
summary: The --json result documents and their fields.
children:
  - results
  - diagnostics
  - help
see-also:
  - errors
examples:
  - name: render
    kind: command
    language: shell
    content: 'canvasnet render shapes.svg -o shapes.png --json'
    description: Render and read the result document.
  - name: formats
    kind: command
    language: shell
    content: 'canvasnet formats --json'
    description: Discover the formats and limits of the installed tool.
---

With --json every command writes exactly one JSON document, on one line, to
standard output.

## Envelope

- `schemaVersion`: The version of the document layout, {{schemaVersion}}. It
  changes only when a document changes incompatibly; adding a key is not a
  change, so ignore keys and operations you do not know.
- `ok`: true for a result, false for a failure.
- `command`: The canonical name of the command that ran (null in a failure when
  no command was selected).
- The document is deterministic: fixed key order, invariant-culture numbers, one
  line, ASCII only (other characters are written as \uXXXX). Standard error is
  not part of it.

## Failure documents

- `error`: {code, message, hint}: code is one of the stable codes of the errors
  topic ('canvasnet help errors'); hint is omitted when there is none. The
  document has no exit-code field: the exit code of every failure is 1.
- A failure document is written only when --json is given and --silent is not. A
  failed compare gate also carries the fields of the comparison.
