---
title: Pictures in diagrams
summary: Nodes that show a model, a chart, another diagram, or a document page.
see-also:
  - svg/embedding
  - svg/fragments
examples:
  - name: pictures
    kind: snippet
    language: mermaid
    file: pictures.mmd
    description: Nodes that show a model (part.scad) and a chart (chart.vl.json) lying beside the diagram.
---

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
