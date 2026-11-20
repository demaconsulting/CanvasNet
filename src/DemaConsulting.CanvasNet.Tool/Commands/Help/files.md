---
extends: files
see-also:
  - pandoc
examples:
  - name: confine
    kind: command
    language: shell
    content: 'canvasnet render docs/diagram.svg --allow-root docs --allow-root assets -o diagram.png --json'
    description: Render a drawing whose links must stay in the docs and assets folders.
  - name: no-links
    kind: command
    language: shell
    content: 'canvasnet render docs/diagram.svg --file-access roots,no-symlinks --allow-root docs -o diagram.png'
    description: 'Exactly the folder confinement of earlier versions: the folder of the input and below it, and no symbolic links.'
  - name: strict
    kind: command
    language: shell
    content: 'canvasnet flatten diagram.svg -o flat.svg --file-access roots --strict --json'
    description: Fail instead of leaving out a picture the mode denies.
---
