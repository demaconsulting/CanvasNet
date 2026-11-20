---
extends: plantuml/preprocessor
examples:
  - name: include
    kind: command
    language: shell
    content: 'canvasnet render main.puml -o main.png --file-access roots --allow-root . --json'
    description: Draw a diagram whose !include files must lie under the current folder.
---

## Command line

- `--file-access`, `--allow-root`: Choose the file-access mode the included
  files are read under.
