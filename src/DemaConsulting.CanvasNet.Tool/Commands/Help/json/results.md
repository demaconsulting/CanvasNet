---
title: Fields of the result documents
summary: The fields of render, convert, the verbs, info, formats, flatten, and the other commands.
see-also:
  - json
  - json/diagnostics
---

## Fields of render, convert, and the verbs

- `tool`: {name, version} of the tool.
- `input`: {path, format, natural: {width, height}}, and for a document
  pageCount and page; and fragment, the text after the # of an input path such
  as part.scad#def.width=20 (path is then the file; the member is absent for an
  input without a fragment). The same member is in the a and b of compare and in
  each cell of montage.
- `operations`: Every step that was applied, in order; each has an op key
  (render, embed, auto-orient, resize, fit, flatten, crop, rotate, flip, pad,
  and the photo operations) and its own fields. With --all-pages every operation
  also carries its page. The verbs are crop, resize, rotate, flip, and pad.
- `outputs`: One entry per file written: {path, format, width, height, bytes}
  and page for a document.
- `diagnostics`: The features that were left out of the picture; see the
  diagnostics section below.
- `echo`: The text an OpenSCAD model printed with echo.
- `anchors`: Only for an OpenSCAD model: the points it declared with
  echo("anchor", "name", [x, y, z]), in echo order, each {name, point: [x, y,
  z], pixel: [x, y] or null, inside}. pixel is the position in the written
  picture, from its top-left corner, after the pixel steps; see 'canvasnet help
  scad'.
- `positions`: Only with --positions (a diagram or a chart): the features the
  selectors matched, in the order of the picture, each {kind, id, then text,
  line, view, name, mark, from, to, index, values when it has them, box: {x, y,
  width, height}, pixel: [x, y], inside}; box and pixel are null when a pixel
  step lost them. See 'canvasnet help positions'.
- `positionsTruncated`: Only with --positions: true when more features matched
  than positions holds.

## Fields of the other commands

- `info`: input {path, format, bytes} and one of image, document {pageCount,
  pages[]}, model {modules, functions, directives}, chart, or diagram (chart and
  diagram follow an image object with the laid-out size).
- `diagram`: For a Mermaid diagram (info): {type, supported, direction, title,
  accTitle, nodes, edges, subgraphs}. type is the diagram type (flowchart for
  flowchart and graph, sequence for sequenceDiagram); supported is false for a
  type this version does not draw (journey, timeline, and so on), which renders
  as an empty picture with the diagnostic mermaid-diagram-unsupported; nodes,
  edges, and subgraphs count the participants, the messages, and the boxes of a
  sequence diagram, the classes, relations, and namespaces of a class diagram,
  the states, transitions, and composite states of a state diagram, and the
  entities, relationships, and subgraphs of an entity relationship diagram;
  nodes counts the slices of a pie chart (its legend entries) and the tasks of a
  gantt chart (its vertical markers included), subgraphs the sections of a gantt
  chart, and edges is 0 for both; nodes and edges count the nodes of a mindmap
  and the edges between them (one fewer than the nodes), and subgraphs is 0;
  nodes counts the blocks of a packet diagram as written, and edges and
  subgraphs are 0; nodes and edges count the commits of a git graph and the
  links from each commit to its parents, subgraphs is 0, and direction is LR,
  TB, or BT; direction, title, and accTitle are present when the diagram has
  them.
- `formats`: tool, library, inputs[], outputs[], quality {min, max, default},
  and maxDimension. Each inputs[] entry has format, kind, extensions, multiPage,
  vector, and helpTopic: the topic of 'canvasnet help <topic>' that describes
  the format (raster for png, jpeg, bmp, tiff, gif, and jpeg2000).
- `compare, montage, inspect`: Described in their own help: run 'canvasnet help
  compare', 'canvasnet help montage', or 'canvasnet help inspect'.
- `flatten`: tool, input {path, format, natural}, scale, flattened (false when
  nothing was replaced and nothing was written), replaced[] ({href, format,
  width, height, elements, bytes}: each link that became a PNG picture, with the
  number of image elements that use it and the size of the PNG), rebased[]
  ({href, newHref}: the relative links rewritten for the folder of the copy),
  outputs[] ({path, format, bytes}; empty when nothing was written),
  diagnostics, and echo.
- `help`: kind is index, command, topics, card, examples, or example; see
  `canvasnet help json help`.
