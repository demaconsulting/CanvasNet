---
title: Preprocessor and includes
summary: Comments, line continuation, !define, variables, and local !include files of a diagram.
tags:
  - plantuml
  - authoring
  - file-access
see-also:
  - plantuml/unsupported
  - files
  - limits
---

## Supported

- Framing: the first @startuml ... @enduml block is the diagram, and text
  before it is ignored; a text with no @start tag (a Pandoc block) is one
  diagram. Only the first of several blocks is drawn
  (plantuml-multiple-diagrams).
- Comments: lines that start with ' and /' ... '/ blocks; a line that ends
  with \ continues on the next one.
- Definitions: !define NAME value (without parameters) and !undef NAME; the
  name is replaced on word boundaries.
- Variables: !$name = "text" or a number, and !$name ?= value; $name is
  replaced by its value.
- Includes: !include, !include_once, and !include_many of a local file,
  relative to the file that names it; an .iuml fragment is read whole, and a
  file with @startuml blocks gives its first block.
- File access: an included file is read under the file-access mode of
  --file-access and --allow-root, as every other referenced file; a file the
  mode denies is file-access-denied (see [[files]]).
- Network: a URL, the standard library (!include <name>), !includeurl, and
  !includesub are never fetched (plantuml-include-unsupported).
- Limits: at most {{pumlMaxIncludes}} include directives and
  {{pumlMaxIncludeDepth}} levels of nesting (see [[limits]]).

## Unsupported

- !if, !ifdef, !while, !foreach, !function, !procedure, !import, built-in
  %functions, and !define with parameters are reported as
  plantuml-preprocessor-unsupported and dropped with the block they open.
- A missing include file is a plantuml-error (plantuml-include-failed) at the
  directive, where PlantUML ignores it.
