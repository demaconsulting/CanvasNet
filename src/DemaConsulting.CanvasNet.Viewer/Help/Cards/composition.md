---
title: Composing figures
summary: Combine diagrams, charts, models, and pages in one picture.
tags:
  - composition
  - design
see-also:
  - svg/bindings
  - svg/composition
  - svg/framed-figure
  - svg/arrows
  - positions
  - mermaid/composition
  - plantuml/composition
  - mathml/composition
  - vega-lite/composition
  - scad/composition
  - style
  - files
---

Every picture CanvasNet draws can be placed in another: a chart in a diagram
node, a model in a chart, any of them in an SVG figure. Choose the outer
picture by what the figure has to say, size each placement from its natural
size, then render, look, and fix.

## Choose the outer picture

- A page with a title, captions, and several pictures: an SVG figure
  ([[svg/composition]];
  [[svg/composition#report]] is a complete one).
- One picture presented in a designed frame - a title band, accents, a
  caption, and arrows that point at its features: a framed SVG figure
  ([[svg/framed-figure]]).
- A process whose steps show the thing itself: Mermaid nodes with pictures
  ([[mermaid/composition]]); a PlantUML diagram (sequence,
  activity, class, state, use case, component, or deployment) can be such
  a picture ([[plantuml/composition]]).
- An equation in a figure, a node, or a chart: a MathML equation placed like
  any picture, sized at one multiple of its natural size so every equation
  has one text size ([[mathml/composition]]).
- Several charts of one data set: one Vega-Lite specification with `layer`,
  `concat`, or `facet` ([[vega-lite/composition]]).
- Labels and leader lines on a model: anchors and an SVG overlay
  ([[scad/composition]]).
- Variants or pages side by side: a contact sheet (`MontageBuilder` of the
  Imaging namespace), one cell per input.

## Size each placement

- An embedded diagram, chart, model, SVG file, or page is drawn at the size of
  its placement, so it stays sharp; its text scales with the placement.
- Read the natural size of a diagram, chart, or SVG file (the size it is drawn
  at when no size is asked for), keep that aspect ratio, and stay near that size
  so the text of every picture matches. A model has no natural aspect: it is
  fitted to the box it gets.

## Framed figures and callouts

- A framed figure layers a container, a title band, decoration, the picture
  slot, a callout layer, and a caption; arrows use `<marker>` (see
  [[svg/framed-figure]]).
- To point at a feature, render the picture alone at the size of its box: a
  model reports its anchors (`ViewerImage.Anchors`; see
  [[scad/anchors]]), a diagram or chart the features its position
  selectors choose (`ViewerImage.Positions`; see [[positions]]).
  Add the image `x` and `y`. For a point no selector names, read its x,y from a
  labeled grid drawn over the picture at that size, and confirm it with the
  color of that pixel.

## Options per placement

- A fragment after the file name sets the options of one placement:
  `flow.mmd#theme=dark`, `part.scad#view=front&amp;def.width=20` (`&amp;` in
  XML), `chart.vl.json#param.t=5`, `report.pdf#page=2`, `deck.pptx#slide=3`,
  `eq.mml#display=inline`. The same fragment works on an input path and in a
  contact sheet (see [[svg/fragments]]).

## Limits and file access

- At most 64 files are embedded in one render and SVG files nest 4 levels
  deep; a Mermaid diagram loads at most {{mmdMaxImages}} pictures and a chart at
  most {{vlMaxImages}} (see [[limits]]).
- A picture that is not drawn is left out with a warning
  (`svg-image-unresolved`, `mermaid-image-unresolved`); strict mode makes it an
  error. Links read any local file unless the file-access policy confines them
  (see [[files]]).

## Render, look, fix

1. Render the figure at its final width (1200 pixels) in strict mode.
2. Look at the picture: sizes, alignment, contrast, and anything cut off.
3. Fix and render again; a comparison with the previous picture (`ImageComparer`
   of the Imaging namespace) measures what changed.
4. Publish the PNG, or a flattened SVG for a browser, WeasyPrint, or Pandoc
   HTML ([[svg/flatten]]).
