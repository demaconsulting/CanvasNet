---
extends: svg/embedding
examples:
  - name: render-embed
    kind: command
    language: shell
    content: 'canvasnet render diagram.svg -o diagram.png'
    description: Draw that diagram.
---

## Command line

- `--file-access <mode>`: Chooses which local files the links may read;
  --allow-root names the folders of roots (see [[files]]).
- `--strict`: Strict mode: a link that is not drawn is an error.
- A linked model follows the same include rule as canvasnet render of the model.
