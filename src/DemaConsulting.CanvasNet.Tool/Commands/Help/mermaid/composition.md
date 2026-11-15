---
title: Pictures in diagrams and diagrams in figures
summary: Nodes that show a model, a chart, a diagram, or a page, and a diagram placed in an SVG figure.
see-also:
  - composition
  - mermaid/style
  - positions/mermaid
  - svg/composition
  - svg/embedding
  - svg/fragments
examples:
  - name: pictures
    kind: snippet
    language: mermaid
    file: pictures.mmd
    description: Nodes that show a model (part.scad) and a chart (chart.vl.json) lying beside the diagram.
---

Show the thing itself in a node: a node can hold a model, a chart, another
diagram, or a document page that lies beside the diagram. Print the pattern
with `canvasnet help mermaid composition --example pictures`.

## Pictures in nodes

- Write `id@{ img: "part.scad", label: "Part", w: 120, h: 120 }` in a
  flowchart. Give every picture node its `w` and `h`: they keep the layout
  stable, and `info` lays the diagram out without the pictures.
- Pick the view or the variant per node with a fragment:
  `img: "part.scad#view=front"`, `img: "chart.vl.json#param.year=2024"`,
  `img: "report.pdf#page=2"` (see `canvasnet help svg fragments`).
- A picture that is not drawn keeps its frame and is reported
  (`mermaid-image-unresolved`); add `--strict` to fail instead.

## A diagram in a figure

- Place the diagram in an SVG layout with
  `<image href="flow.mmd" x="40" y="120" width="720" height="210"/>`; it is
  drawn sharply at that size and stays transparent, so the page color shows.
  Choose a theme for this placement only with `href="flow.mmd#theme=neutral"`.
  See `canvasnet help svg composition` for a complete figure.
- Show variants side by side with montage, one cell per theme:

```shell
canvasnet montage "flow.mmd#theme=default" "flow.mmd#theme=neutral" --labels --cell 480x240 -o themes.png
```

## Point at a feature

- Render the diagram alone at the size of its placement with `--positions`
  and `--json`: `node:send` gives the box and center of that node,
  `edge:@from=pack` the middle of each edge that leaves `pack`. Add the image
  `x` and `y` for an arrow in a figure (see `canvasnet help positions
  mermaid`).

## Supported

- Pictures: an @{ img: "part.png", label, pos, w, h, constraint } node, or an
  `<img src="part.png">` in a label of a flowchart, class, state, or entity
  relationship diagram, shows a raster, an SVG file, an OpenSCAD model, a
  Vega-Lite chart, a page of a PDF, PPTX, or VSDX document, or another diagram
  that lies beside the diagram; a link takes the fragments of an SVG link
  (part.scad#def.width=20, flow.mmd#theme=dark; see 'canvasnet help svg' and the
  pictures example). Give a picture node its w and h, because info lays the
  diagram out without its pictures. At most {{mmdMaxImages}} picture loads: a
  picture counts when it is measured and again when it is drawn.
