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
  - name: search
    kind: command
    language: shell
    content: 'canvasnet help --search gantt --json'
    description: Find the cards about a word as a document.
---

Every document of `canvasnet help ... --json` has the envelope and a `kind`:

- `kind: index`: for `canvasnet help`, `canvasnet --help`, and `canvasnet help
  help`: usage, commands[] and topics[] ({name, aliases, summary}; the topics
  are the root help cards), and globalOptions[]. With --all, cards[] lists
  every card instead of the roots.
- `kind: command`: for `canvasnet help <command>` and `canvasnet <command>
  --help`: topic is the command name; aliases, summary, usage, options[], and
  globalOptions[]. An option is {name, aliases, takesValue, valueName,
  description}.
- `kind: topics`: for `canvasnet help topics`: cards[] ({id, summary, aliases,
  tags, command}), every help card, depth first.
- `kind: card`: for `canvasnet help <topic> [<card> ...]`: id, title, summary,
  aliases, tags, body (the Markdown text of the card), children[] and seeAlso[]
  ({id, summary, command}), and examples[] ({name, kind, language, fileName,
  description, content}; fileName only for a file).
- `kind: examples`: for `canvasnet help <topic> [<card> ...] --examples --json`:
  id, examples[] (the card's own examples, each as in the card document, with
  the content), and cards[] ({id, summary, command, examples}: the cards below
  it that have examples, with the example names); both lists are empty for a
  card without examples.
- `kind: example`: for `canvasnet help <topic> [<card> ...] --example <name>
  --json`: id (the card that was asked for) and one example object ({card (the
  card that holds it), name, kind, language, fileName, description, content}).
- `kind: search`: for `canvasnet help --search <text>`: query and results[]
  ({id, title, summary, source, score, matchedOn, command}).
- `kind: cards`: for `canvasnet help --tag <tag>`: filter ({tags, sources})
  and cards[] ({id, title, summary, tags, source, command}).

A failure has no kind: ok is false and error is {code, message, hint}, with
code usage-error (an unknown subject, card, example, tag, or source, or a wrong
option) and exit code 1.

## Added keys

The documents are written by the HelpCards library inside the envelope, so they
carry more keys than these lists: tool, version, sources[], and cards[] in the
index and topics documents; title, source, and depth of a card in a list;
source, culture, and links[] of a card; and title of a child or See also entry.
A reader that does not know a key ignores it. The json and pandoc topics are
listed after the library topics.

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
