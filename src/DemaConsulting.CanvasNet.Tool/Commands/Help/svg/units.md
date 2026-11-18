---
title: Lengths, units, and font sizes
summary: CSS length units, font-size, and how invalid lengths are handled.
see-also:
  - svg/supported
---

## Supported

- CSS length units wherever SVG takes a length (shape and text positions and
  sizes, stroke-width, stroke-dasharray, stroke-dashoffset, font-size, gradient,
  pattern, and mask coordinates, and the root width and height): px, pt, pc, in,
  cm, mm, Q (1in is 96 user units), em, ex, ch, rem, lh, rlh, vw, vh, vmin, and
  vmax, in any letter case; a number without a unit is in user units. Also
  calc(), min(), max(), and clamp() with + - * / and nested parentheses; a math
  result keeps its type, so one that resolves to a plain number (such as
  calc(1 + 1)) is user units like a literal number wherever SVG 2 allows a
  number (stroke-width, stroke-dasharray entries, stroke-dashoffset, and every
  length attribute, including the font-size attribute), a mixed result such as
  calc(1 + 1px) or calc(10% + 1) is always invalid, a plain-number result in a
  font-size from a style declaration, stylesheet, or font shorthand is ignored,
  and line-height treats it as a multiplier.
- font-size from an attribute, a style attribute, a style element, or the font
  shorthand (style, weight, size, / line-height, family): lengths, percent, the
  keywords xx-small to xxx-large (9, 10, 13, 16, 18, 24, 32, and 48 pixels),
  larger and smaller (1.2 times), and inherit, initial, and unset. An invalid
  font-size, font-family, font-weight, font-style, or line-height declaration is
  dropped, so the next lower source (style attribute, style element,
  presentation attribute, font attribute) applies, and the value is inherited
  only when none is valid. In the font shorthand the style, variant, weight, and
  stretch components may each appear at most once (a shorthand repeating a
  component, such as bold bold, is invalid; normal may fill any free slot), and
  the size needs a unit (font: 12 serif and font: 0.5 serif are invalid; only 0
  may be bare). The keywords inherit, initial, and unset (alone as the font
  value, or on font-family, font-weight, and font-style) reset every font
  property including font-family. font-weight and font-style keywords are
  accepted in any letter case, and font-style takes oblique with an angle in
  deg, grad, rad, or turn (within 90deg either way). A font-family list splits
  only at commas outside quotes, so a quoted name can contain a comma, and a
  backslash before a newline joins the lines inside a quoted name but
  invalidates an unquoted list. Every entry of the x, y, dx, and dy lists of a
  text or tspan is a length in any unit (dy="0.71em" moves a line down).

## Known differences

- Lengths: ex is the height of the font's x glyph and ch the advance of 0 (half
  an em when the font has no such glyph); a normal line height is 1.2 em; vw,
  vh, vmin, and vmax are percentages of the root viewport in user units (the
  viewBox, else the root width and height), which differs from a browser only
  when the two disagree; and percent in a gradient, pattern, or mask coordinate
  is a fraction of 1 under objectBoundingBox (the default) but is relative to
  the viewport (width for x, height for y, the diagonal for r) under
  userSpaceOnUse, the same for a plain percent and one inside calc(). An omitted
  gradient attribute with a percent default uses the same basis: under
  userSpaceOnUse an omitted x2 is 100% of the viewport width and an omitted cx,
  cy, and r are 50% of the viewport width, height, and diagonal (fx and fy
  follow cx and cy), and a pattern without width or height paints nothing. An
  omitted mask x, y, width, or height is -10%, -10%, 120%, or 120% in both
  modes: of the shape's box under objectBoundingBox, and of the viewport width
  or height under userSpaceOnUse. lh and rlh follow the line-height of a style
  attribute, style element, or font shorthand; inside line-height itself lh is
  the parent's line height and rlh on the root is the initial 1.2 times 16, so a
  line height never refers to itself. The root width and height can use
  font-relative units (against the root's own font-size and line-height from its
  attributes or style, so lh and rlh there use the root's own line height), but
  a percent or viewport unit there is ignored. The keyword sizes follow the
  browsers' table, not the CSS Fonts factors (9.6, 12, 14.2, 16, 19.2, 24, 32,
  and 48).
- A length that is not valid (an unknown unit, a space between number and unit,
  a bad calc(), a calc() that resolves to a plain number) in a shape, a text
  position, the stroke width, a dash pattern (stroke-dasharray), or a dash
  offset (stroke-dashoffset) is corrupt-input. In a gradient, pattern, or mask
  coordinate, or in the root width and height, it is ignored and the default is
  used; an invalid font-size, line-height, or font shorthand is dropped, so the
  next lower source applies, else the size is inherited (a browser does the
  same).
- Units are not converted for: letter-spacing, word-spacing, and textLength
  (which are not supported and are reported as svg-text-unsupported), and
  nested svg elements (which are not drawn, so their width, height, x, and y
  are never read).
