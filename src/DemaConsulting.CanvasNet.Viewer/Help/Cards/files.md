---
title: Referenced files and the file-access modes
summary: 'Which files a document may read: the file-access policy.'
tags:
  - file-access
  - hosting
aliases:
  - file-access
  - links
see-also:
  - svg/embedding
  - errors
---

Which files a document may reference, the `FileAccessPolicy` of a render, and
the diagnostics of a denial.

## Modes

- `any`: The default (`FileAccessPolicy.AllowAll`). A document may reference
  every regular local file, as Pandoc reads them: a path relative to the file
  that holds the link, a path with .., an absolute or drive path, a local file:
  URI, and a symbolic link or junction.
- `roots`: Only a file inside one of the folders of the policy
  (`FileAccessPolicy.UnderRoots(folders)`). Both the path as written and the
  real path of the opened file must be inside, so a link that leads out is
  denied, and so is a path outside that links back in.
- `no-symlinks`: A reference may not pass through a symbolic link or junction
  below the folder of the input it starts from (`FileAccessPolicy.NoSymlinks`);
  that folder and the folders above it are trusted. A hard link cannot be
  detected.
- `roots,no-symlinks`: Both rules
  (`FileAccessPolicy.Combine` of `UnderRoots(folder)` and `NoSymlinks`,
  named roots+no-symlinks). With the folder of the input as the root, it is
  exactly the confinement that earlier versions applied by default. For a
  document you do not trust, confine it to its folder.

## Options

- `FileAccess`: The policy of a render: `ViewerRenderOptions.FileAccess` for
  `CanvasNetViewer.Render`, or `SourceOptions.FileAccess` for the operations of
  the Imaging namespace (probe with the policy you will render with: an
  operation refuses the facts of a probe made under another one). The default is
  `AllowAll`.
- `FromDelegate`: A host can add its own rule with
  `FileAccessPolicy.FromDelegate(name, check)`, alone or combined with the
  built-in policies.

## In every mode

- `inputs`: The file that is rendered is never judged by the policy, only the
  files it references: images, pictures, data files, includes, and linked pages,
  at every level.
- `equations`: A MathML equation reads no other file: its links (href) are
  reported and never followed, in every mode.
- `never read`: A network URL (http:, https:, and other schemes), a UNC or
  network path, a file: URI with a host, a device path, an alternate data
  stream, and anything that is not a regular file are never read.
- `letter case`: The real path of an opened file is compared letter for letter,
  in the case its folders and its name have on disk, so a folder whose name
  differs from an allowed folder only in letter case is outside it. On a
  case-insensitive volume a reference or a root folder in any letter case still
  works. A file whose spelling on disk cannot be confirmed to name the opened
  file is denied.
- `limits`: Every size and count limit holds in every mode; a mode decides which
  files may be read, never how much. See [[limits]].

## Diagnostics

- `image-access-denied`: Warning: a picture (an SVG image, a Mermaid picture, a
  Vega-Lite image mark, or a linked page) that the mode denies is not drawn, and
  the rest is; the message names the link, the mode, and why. In strict mode it
  is the error file-access-denied.
- `file-access-denied`: Error: an OpenSCAD include or use file, a PlantUML
  !include file, or a Vega-Lite data file that the mode denies, or a denied
  picture in strict mode; the message names the file as written and where it is
  referenced. No output is written.
- `real path unavailable`: A mode other than any needs the real path of every
  opened file. Where the system cannot tell it (Linux without /proc, for
  example), the file is denied, never read unchecked.
- `detail`: The detail of an image-access-denied note (`Detail` of the
  diagnostic) holds the reference, the requested and real paths, and the mode.
  These are host paths: a service that shows notes to untrusted authors should
  remove them.
