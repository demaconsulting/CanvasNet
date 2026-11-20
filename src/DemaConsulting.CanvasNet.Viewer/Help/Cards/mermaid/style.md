---
title: Styling Mermaid diagrams
summary: Themes, theme variables, classDef, style, linkStyle, spacing, and size for good-looking diagrams.
see-also:
  - style
  - mermaid/composition
  - mermaid/flowchart
  - mermaid/options
examples:
  - name: styled
    kind: source
    language: mermaid
    file: styled-flow.mmd
    description: A finished flowchart - the base theme with theme variables, three node classes, a link style, and a title.
  - name: theme
    kind: command
    language: shell
    content: 'canvasnet render flowchart.mmd --theme dark --scale 2 --background transparent -o flowchart-dark.png'
    description: Draw at twice the size in the dark theme over a transparent background.
---

Start from the `base` theme, set a few theme variables, and give each role of
a node a `classDef`. Print a finished example with
`canvasnet help mermaid style --example styled`, render it with `--scale 2`,
and look at the PNG.

## Themes

- The themes are `default`, `base`, `dark`, `forest`, and `neutral`. Any other
  theme is not available: it is ignored with `mermaid-config-invalid`.
- Choose one with `--theme dark`, with `#theme=dark` after the file name in a
  link or an input path, or with `config.theme` in the YAML frontmatter. A
  theme the diagram sets itself wins over `--theme`.
- For your own colors use `theme: base` and set `themeVariables`:
  `primaryColor` (node fill), `primaryBorderColor`, `primaryTextColor`,
  `lineColor`, `edgeLabelBackground` (the box behind a link label; set it to
  the page color), `secondaryColor`, `tertiaryColor`, and `fontSize`
  (`"15px"`). The other themes compute their own colors from fewer inputs.

## Classes and styles

- Define a role once and use it on many nodes:
  `classDef stop fill:#fdecea,stroke:#c62828,stroke-width:2px,color:#8e1c1c`,
  then `class hold stop` or `hold[Hold the order]:::stop`.
  `classDef default ...` styles every node of a flowchart.
- `style id fill:#eef4fb` styles one node; `linkStyle 0,2 stroke:#c62828` styles
  links by their number, and `linkStyle default stroke:#5b6b7a` every link.
- The declarations that are drawn are `fill`, `stroke`, `stroke-width`,
  `stroke-dasharray`, `color`, `font-weight`, `font-style`, `font-size`,
  `font-family`, and `opacity`. Anything else, `rx` and `ry` included, is left
  out with `mermaid-style-ignored`.
- Pair each light fill with a darker stroke and text of the same hue, and keep
  one accent color for what matters (see `canvasnet help style`).

## Layout and text

- Use `flowchart LR` for a pipeline and `TB` for a hierarchy. Keep a label to a
  few words, and break a longer one with `<br>`.
- Space the nodes with `config.flowchart.nodeSpacing` and `rankSpacing` (50 by
  default); more space reads better in a small figure.
- Text is always drawn with the bundled Liberation Sans, so the diagram looks
  the same on every machine.

## Size and background

- The natural size is the size Mermaid lays out; draw with `--scale 2` (or
  `--width`) for a sharp figure, never by resizing a small PNG.
- A diagram is drawn over white unless `--background` says otherwise;
  `--background transparent` keeps it clear for a slide or a colored page.
  Inside an SVG drawing a diagram stays transparent.
