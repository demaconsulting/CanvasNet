---
extends: files
summary: Which referenced files a document may read (--file-access).
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

## Command line

- `--file-access <mode>`: Chooses the mode for render and its verbs, inspect,
  compare, montage, and flatten: any, roots, no-symlinks, or roots,no-symlinks,
  in any letter case. Anything else is a usage-error that lists the modes. info
  always measures under any. The files named on the command line are never
  judged by the mode.
- `--allow-root <folder>`: A folder that referenced files may be in; repeatable.
  It implies --file-access roots and replaces the default folders (without it,
  roots allows the current folder and the folder of an input). A relative folder
  is taken from the current folder, and a folder that does not exist is a
  usage-error; with --file-access any or no-symlinks it is a usage-error that
  names the conflict.
- `earlier versions`: Exactly the confinement that earlier versions applied by
  default is --file-access roots,no-symlinks --allow-root <folder of the input>.
  For a document you do not trust, name its folder with --allow-root.
- `--strict`: A denied picture is the error file-access-denied; the detail of an
  image-access-denied note is in the JSON result.

## The Pandoc filter

- `default`: The pandoc-filter command passes no mode, so a document reads its
  pictures, includes, and data as Pandoc reads its images.
- `CANVASNET_FILE_ACCESS`: Set it to roots, no-symlinks, or roots,no-symlinks to
  pass --file-access to every render and flatten of the filter; a picture the
  mode denies then stops the build.
- `CANVASNET_ROOTS`: Folders separated like PATH (; on Windows, : elsewhere),
  passed as one --allow-root each as an absolute path (a relative folder is
  taken from the working directory); it implies roots. Without it, roots gets
  the working directory and the folder of the picture as --allow-root. These
  arguments are part of the cache name of every picture, so a picture drawn
  without them, or from another working directory, is not reused.
- `hashing`: The filter reads the files a picture references to name it in the
  cache, and in a restrictive mode it judges them as the tool does, by the real
  path of the opened file: a file outside the folders, a link that leads outside
  them, and under no-symlinks any link are never read.
