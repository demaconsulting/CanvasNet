---
title: Params and selections
summary: Selection and variable params in a still picture, and setting a param from outside.
tags:
  - vega-lite
  - authoring
  - hosting
aliases:
  - selections
see-also:
  - vega-lite/options
  - svg/fragments
---

## Supported

- Selection params (select: point or interval, and the legacy selection of a
  view) in the state their value gives (none means empty): conditions with a
  param, a test that names one, and filters on one (the empty rule applies), and
  expressions that read the selection; the caller can replace the value of a
  param (--param, or the param. fragment of an embedding link). A point
  selection projects over fields or encodings, an interval over encodings, and
  the tuples are matched as Vega does (an interval includes both ends). An
  interval selection with a value draws its brush as Vega-Lite does (a shade
  behind the marks and an outline in front, from select.mark and
  config.selection.interval.mark; one brush for a selection resolved globally,
  none in a facet); bind: scales sets the domain of the scales of a single view
  or of the layers of a top-level layered view (and is ignored in a concat,
  facet, or repeat); a selection can be the domain of a scale or the extent of a
  bin, in an encoding or in a bin transform.

## Known differences

- The picture is static: no interaction, and tooltips are omitted. A selection
  or a param is drawn in its initial state, as vega-embed paints it before any
  event; a date in a selection value is read in UTC. The brush of an initial
  interval selection is drawn (nothing is made for a brush with no extent, where
  Vega-Lite makes a zero-size rectangle), and bind: scales applies where
  Vega-Lite applies it to a still chart.
