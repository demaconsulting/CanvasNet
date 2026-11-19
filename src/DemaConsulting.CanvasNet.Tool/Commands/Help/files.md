---
title: Referenced files and the file-access modes
summary: Which referenced files a document may read (--file-access).
aliases:
  - file-access
  - links
see-also:
  - svg/embedding
  - errors
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

Which files a document may reference, the --file-access and --allow-root
options, the diagnostics of a denial, and the Pandoc filter.

## Modes

- `any`: The default. A document may reference every regular local file, as
  Pandoc reads them: a path relative to the file that holds the link, a path
  with .., an absolute or drive path, a local file: URI, and a symbolic link or
  junction.
- `roots`: Only a file inside a folder named by --allow-root, or, without
  --allow-root, inside the current folder or the folder of an input. Both the
  path as written and the real path of the opened file must be inside, so a link
  that leads out is denied, and so is a path outside that links back in.
- `no-symlinks`: A reference may not pass through a symbolic link or junction
  below the folder of the input it starts from; that folder and the folders
  above it are trusted. A hard link cannot be detected.
- `roots,no-symlinks`: Both rules. Exactly the confinement that earlier versions
  applied by default is --file-access roots,no-symlinks --allow-root <folder of
  the input>; without --allow-root, roots also allows every file under the
  current folder. For a document you do not trust, name its folder with
  --allow-root.

## Options

- `--file-access <mode>`: Chooses the mode for render and its verbs, inspect,
  compare, montage, and flatten: any, roots, no-symlinks, or roots,no-symlinks,
  in any letter case. Anything else is a usage-error that lists the modes. info
  always measures under any.
- `--allow-root <folder>`: A folder that referenced files may be in; repeatable.
  It implies --file-access roots and replaces the default folders. A relative
  folder is taken from the current folder, and a folder that does not exist is a
  usage-error. With --file-access any or no-symlinks it is a usage-error that
  names the conflict.

## In every mode

- `inputs`: The files named on the command line are never judged by the mode,
  only the files they reference: images, pictures, data files, includes, and
  linked pages, at every level.
- `never read`: A network URL (http:, https:, and other schemes), a UNC or
  network path, a file: URI with a host, a device path, an alternate data
  stream, and anything that is not a regular file are never read.
- `letter case`: The real path of an opened file is compared letter for letter,
  in the case its folders and its name have on disk, so a folder whose name
  differs from an allowed folder only in letter case is outside it. On a
  case-insensitive volume a reference or --allow-root in any letter case still
  works. A file whose spelling on disk cannot be confirmed to name the opened
  file is denied.
- `limits`: Every size and count limit holds in every mode; a mode decides which
  files may be read, never how much. See 'canvasnet help limits'.

## Diagnostics

- `image-access-denied`: Warning: a picture (an SVG image, a Mermaid picture, a
  Vega-Lite image mark, or a linked page) that the mode denies is not drawn, and
  the rest is; the message names the link, the mode, and why. With --strict it
  is the error file-access-denied.
- `file-access-denied`: Error: an OpenSCAD include or use file, a PlantUML
  !include file, or a Vega-Lite data file that the mode denies, or a denied
  picture with --strict; the message names the file as written and where it is
  referenced. No output is written.
- `real path unavailable`: A mode other than any needs the real path of every
  opened file. Where the system cannot tell it (Linux without /proc, for
  example), the file is denied, never read unchecked.
- `detail`: In the JSON result, the detail of an image-access-denied note holds
  the reference, the requested and real paths, and the mode. These are host
  paths: a service that shows notes to untrusted authors should remove them.

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
- `hashing`: The filter reads the files a picture references to name it in
  the cache, and in a restrictive mode it judges them as the tool does, by
  the real path of the opened file: a file outside the folders, a link that
  leads outside them, and under no-symlinks any link are never read.
