---
title: Equations in figures and documents
summary: An equation placed in an SVG figure, a Mermaid node, or a Vega-Lite chart, and how to size it.
tags:
  - mathml
  - composition
see-also:
  - composition
  - mathml/style
  - svg/embedding
  - svg/fragments
  - mermaid/composition
  - vega-lite/composition
---

An equation is drawn wherever a CanvasNet picture can be placed: sharply at the
size of its placement, scaled uniformly and centered, over a transparent
background. Each block below links `quadratic.mml` (save the fractions example
quadratic beside it).

## In an SVG figure

```xml
<svg xmlns="http://www.w3.org/2000/svg" width="480" height="160" viewBox="0 0 480 160">
  <rect width="480" height="160" fill="#f8fafc"/>
  <text x="24" y="40" font-family="sans-serif" font-size="18">The roots of a quadratic</text>
  <image href="quadratic.mml" x="102" y="56" width="276" height="88"/>
</svg>
```

## In a Mermaid node

```mermaid
flowchart LR
    eq@{ img: "quadratic.mml", label: "Roots", w: 207, h: 66 }
    eq --> check[Check the discriminant]
```

## In a Vega-Lite chart

```json
{
  "width": 300, "height": 120, "padding": 0,
  "data": { "values": [ { "x": 5 } ] },
  "mark": { "type": "image", "width": 207, "height": 66 },
  "encoding": {
    "x": { "field": "x", "type": "quantitative", "scale": { "domain": [0, 10] }, "axis": null },
    "y": { "value": 60 },
    "url": { "value": "quadratic.mml" }
  }
}
```

## Size and display

- Size the placement at the natural size of the equation times one factor for
  the whole document (the quadratic formula is 138 by 44 at its natural 16
  pixels per em: 276 by 88 doubles it, 207 by 66 is one and a half times), so
  every equation has the same text size. A probe (`SourceLoader.Probe`) reports
  the natural size and the baseline.
- A box of another aspect ratio leaves a transparent margin; the equation is
  never stretched.
- `#display=block` or `#display=inline` on the link chooses the display mode
  (`href="sum.mml#display=inline"`); any other value is refused with
  image-fragment-invalid, and another parameter is reported as
  image-fragment-unknown-parameter.
- Flattening (see [[svg/flatten]]) replaces a linked .mml with a
  PNG, for SVG readers that cannot draw MathML.
- An equation reports no feature positions, so position selectors and SVG
  position bindings cannot point at its parts.
