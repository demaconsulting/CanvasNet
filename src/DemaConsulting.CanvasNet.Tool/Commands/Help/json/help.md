---
title: Help documents
summary: 'The documents of canvasnet help --json: index, command, topics, card, examples.'
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
- `kind: topics`: for `canvasnet help topics`: cards[] ({id, aliases, summary,
  command}), every help card, depth first.
- `kind: card`: for `canvasnet help <topic> [<card> ...]`: id, aliases, title,
  summary, body (the Markdown text of the card), children[] and seeAlso[] ({id,
  summary, command}), and examples[] ({name, kind, language, fileName,
  description, content}; fileName only for a file).
- `kind: examples`: for `canvasnet help <topic> [<card> ...] --examples --json`:
  id and examples[] of the card and of the cards below it, each with the card it
  belongs to and without the content.
- `kind: example`: for `canvasnet help <topic> [<card> ...] --example <name>
  --json`: id and one example object ({card, name, kind, language, fileName,
  description, content}).

## Schema version {{schemaVersion}}

Version 2 replaced the help topic document (topic, sections[]) with the card and
topics documents, and dropped the constant topic key of the index. Every other
document is unchanged.
