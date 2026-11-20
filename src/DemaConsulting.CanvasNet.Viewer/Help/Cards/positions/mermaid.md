---
title: Positions of Mermaid diagrams
summary: The kinds, ids, and attach points of the features of each diagram type.
tags:
  - mermaid
  - composition
see-also:
  - positions/selectors
  - positions/geometry
  - mermaid/composition
---

## Kinds per diagram type

- Flowchart, class, state, and entity relationship diagrams: title, group (a
  subgraph, namespace, or composite state), node (a vertex, class, state, or
  entity), note, and edge.
- Sequence diagram: title, participant (the top box), message, and note.
- Gantt chart: title, task, milestone, and section (all its rows).
- Pie chart: title and slice, with `values` {value}.
- Mindmap: node. Packet diagram: title and block. Git graph: title, commit,
  branch, and tag.

## Ids

- The id the text gives: node `send` (not its SVG id), the subgraph id, the
  class, state, entity, participant, branch, and tag name, the task and
  commit id, and the mindmap node id (its text when it has none).
- Synthetic ids, the same for the same text: an edge's own id (`e1@-->`), else
  the id Mermaid gives it (`L_pack_send_0`, `id_Item_Book_1`, `edge3`); a
  message `1`, `2`, ... in drawing order; a sequence note `note1`, ...; a
  task without an id `task1`, ...; a commit without an id the id it is drawn
  with; a packet block its bits, `0-7` (one item per row when it wraps); a pie
  slice its label; a title `title`. A state diagram's start and end are
  `root_start` and `root_end`.
- An edge and a message have from and to (the ids of their ends) and index:
  0 for the first between the same two, in source order. text is the label,
  line the source line; items of a kind come in source order.

## Attach points

- node, group, participant, task, milestone, section, block, branch, tag,
  note, and title: the center of the box.
- edge and message: the middle of the path by length (a self-loop counts all
  its pieces). The arrowhead and the label are not part of the path; the point
  may sit under the label.
- slice: the middle angle at the middle radius.
- commit: the center of its bullet; the box is the bullet, not the label.
