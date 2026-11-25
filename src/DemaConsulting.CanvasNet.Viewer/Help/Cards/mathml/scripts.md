---
title: Subscripts, superscripts, and primes
summary: Subscripts, superscripts, double indices, primes, tensors, and nuclide prescripts.
tags:
  - mathml
  - authoring
see-also:
  - mathml/large-operators
  - mathml/pitfalls
  - mathml/accents
examples:
  - name: scripts
    kind: source
    language: xml
    file: scripts.mml
    description: A subscript and a superscript together, a double index, a prime, an exponent, and a tensor.
  - name: nuclide
    kind: source
    language: xml
    file: nuclide.mml
    description: Alpha decay of uranium written with prescripts - mass number above, atomic number below.
---

## Scripts

- `<msub>base sub</msub>`, `<msup>base sup</msup>`, and `<msubsup>base sub
  sup</msubsup>`: the base first, then the scripts, each one element; wrap a
  script of several parts in `mrow`
  (`<msup><mi>e</mi><mrow><mo>&minus;</mo><mi>x</mi></mrow></msup>`).
- A double index is one row: `<msub><mi>a</mi><mrow><mi>i</mi><mi>j</mi>
  </mrow></msub>` is a with the subscript ij.
- Scripts are one size smaller, and a script of a script smaller again (the
  script and script-script sizes of the math font).
- Put the scripts on the symbol they belong to: `<msup><mi>x</mi><mn>2</mn>
  </msup><mi>y</mi>` squares x only.

## Primes

- Write a prime after its base as an operator: `<mi>f</mi><mo>&prime;</mo>` for
  f', `&Prime;` for f'' and `&tprime;` for f'''. The prime glyph is already
  raised, so it must not be a superscript: `<msup><mi>f</mi>
  <mo>&prime;</mo></msup>` draws a tiny prime too high.

## Prescripts and tensors

- `mmultiscripts` takes a base, then pairs of subscript and superscript after
  it, then `<mprescripts/>` and the pairs before it; `<none/>` leaves a place
  empty.
- Nuclides: `<mmultiscripts><mi>He</mi><mprescripts/><mn>2</mn><mn>4</mn>
  </mmultiscripts>` (see [[#nuclide]]); a one-letter element symbol
  needs `mathvariant="normal"` to stay upright.
- Tensors: `<mmultiscripts><mi>R</mi><none/><mi>&mu;</mi><mi>&nu;</mi>
  <none/></mmultiscripts>` staggers an upper and a lower index.
