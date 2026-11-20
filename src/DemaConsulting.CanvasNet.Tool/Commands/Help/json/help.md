---
title: Help documents
summary: 'The documents of canvasnet help --json: index, command, topics, card, examples.'
tags:
  - hosting
see-also:
  - json
examples:
  - name: topic
    kind: command
    language: shell
    content: 'canvasnet help scad --json'
    description: Read a topic as a document.
---

Every document of `canvasnet help ... --json` has the envelope and a `kind`:

- `kind: index`: for `canvasnet help`, `canvasnet --help`, and `canvasnet help
  help`: usage, commands[] and topics[] ({name, aliases, summary}; the topics
  are the root help cards), and globalOptions[].
- `kind: command`: for `canvasnet help <command>` and `canvasnet <command>
  --help`: topic is the command name; aliases, summary, usage, options[], and
  globalOptions[]. An option is {name, aliases, takesValue, valueName,
  description}.
- `kind: topics`: for `canvasnet help topics`: cards[] ({id, aliases, tags,
  summary, command}), every help card, depth first.
- `kind: card`: for `canvasnet help <topic> [<card> ...]`: id, aliases, tags,
  title, summary, body (the Markdown text of the card), children[] and seeAlso[]
  ({id, summary, command}), and examples[] ({name, kind, language, fileName,
  description, content}; fileName only for a file).
- `kind: examples`: for `canvasnet help <topic> [<card> ...] --examples --json`:
  id, examples[] (the card's own examples, each as in the card document, with
  the content), and cards[] ({id, summary, command, examples}: the cards below
  it that have examples, with the example names).
- `kind: example`: for `canvasnet help <topic> [<card> ...] --example <name>
  --json`: id and one example object ({card, name, kind, language, fileName,
  description, content}).

## Schema version {{schemaVersion}}

Version 2 replaced the help topic document (topic, sections[]) with the card and
topics documents and dropped the constant topic key of the index. The examples
and example documents name the card by id, and the examples document carries
the card's own examples in full and the cards below it that have examples. The
documents of the other commands are unchanged.

## Tags

- `tags`: The tags of a card, from a closed list: the formats it covers (svg,
  mermaid, plantuml, scad, vega-lite, pdf, pptx, vsdx, or raster; none for a
  general card) and at least one scope: authoring (what to write in a file),
  design (how to make it look good), composition (files in files, fragments,
  bindings, positions), diagnostics, limits, hosting (what the program that
  renders controls), and file-access. They were added to the card and topics
  documents without a new schema version; a reader that does not know the key
  ignores it.
