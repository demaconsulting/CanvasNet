---
title: Accents, bars, vectors, and braces
summary: Hats, bars, vector arrows, dots for time derivatives, tildes, overlines, and labeled braces.
tags:
  - mathml
  - authoring
see-also:
  - mathml/scripts
  - mathml/symbols
  - mathml/pitfalls
examples:
  - name: accents
    kind: source
    language: xml
    file: accents.mml
    description: A hat, a bar, a vector arrow, one and two dots, a tilde, and an overline over two letters.
  - name: braces
    kind: source
    language: xml
    file: braces.mml
    description: An underbrace and an overbrace, each with a label.
---

## Accents

- An accent is an `mo` over its base in `<mover accent="true">`, the base first:
  `<mover accent="true"><mi>x</mi><mo>&#x2C6;</mo></mover>`. The `accent`
  attribute keeps the mark close to the base and at the size of the base.
- Use the spacing accent characters: `&#x2C6;` for a hat (not `^`, which is
  drawn large and offset), `&#xAF;` for a bar (drawn as a rule as wide as the
  base), `&rarr;` for a vector (it stretches over a longer base), `&#x2D9;` and
  `&#xA8;` for the first and second time derivatives, and `&#x2DC;` for a tilde.
- `&#x203E;` over an `mrow` is an overline that stretches over the whole group:
  `<mover accent="true"><mrow><mi>A</mi><mi>B</mi></mrow>
  <mo>&#x203E;</mo></mover>`. `<munder accentunder="true">` with `<mo>_</mo>`
  underlines a group the same way.
- An accent is centered over its base; on a slanted letter it may sit a little
  left of the top of the letter, as Chrome draws it.

## Braces with labels

- `&#x23DF;` under and `&#x23DE;` over a group stretch to its width. Put the
  label on the brace with a second `munder` or `mover` around the first:
  `<munder><munder><mrow>...</mrow><mo>&#x23DF;</mo></munder>` and then the
  label, `<mtext>terms</mtext></munder>` (see [[#braces]]).
