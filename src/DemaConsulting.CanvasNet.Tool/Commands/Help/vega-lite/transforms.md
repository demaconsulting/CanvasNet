---
title: Transforms and expressions
summary: Data transforms, the expression language, and the statistical transforms.
see-also:
  - vega-lite/encodings
  - vega-lite/diagnostics
examples:
  - name: regression
    kind: source
    language: json
    file: regression.vl.json
    description: A scatter plot with a regression line and a loess curve.
---

## Supported

- Transforms: calculate, filter, fold, flatten, sample, pivot (with any
  aggregate operation as op), lookup, timeUnit, aggregate, joinaggregate,
  window, bin, stack, impute, and the statistical transforms regression (linear,
  log, exp, pow, quad, poly), loess, density, quantile, and extent.
- A subset of the Vega expression language with 113 functions (including format,
  timeFormat and utcFormat, the selection functions, and the distribution
  functions densityNormal, cumulativeNormal, quantileNormal and their LogNormal
  and Uniform forms), static variable params (the expr of a param wins over its
  value), and expression references ({"expr": ...}) wherever Vega-Lite allows
  one and no row is involved: titles, padding, background, mark, axis, legend,
  header, scale, and view properties, and config. Each is evaluated once, with
  the params at their initial value. A scale domain can be an array of
  references or {"param": name} of a variable or selection param: the value of
  the param is then the domain as it is (no zero, nice, or padding) and the
  marks are clipped to the view; a bin extent of the form {"param": name} sets
  the span that chooses the bin width.

## Known differences

- labelExpr and the other expressions have format(value, specifier) (d3-format),
  timeFormat() and utcFormat() (d3-time-format); an invalid specifier is a
  render error. config.legend.labelExpr is not read, as in Vega-Lite 6.
- The distribution functions of expressions are pure and follow vega-statistics
  (the cumulative normal is the approximation of West, the quantile uses an
  inverse error function approximation); the sampling functions are not
  available, so an expression gives the same result on every run. A symbol
  legend with a clipHeight clips its symbols to entries of that height; a
  gradient legend ignores it, as Vega does.
- The statistical transforms follow Vega: the groups of regression, loess,
  density, and quantile are keyed by the text of the group values, a density
  counts every row (a null reads as 0, a text that is not a number makes the
  density NaN), a shared density samples a uniform grid (Vega-Lite's default),
  and a pivot gives no value (not 0) for a combination without rows. A
  regression with params: true gives one row of keys, coef, and rSquared; the
  curve of a date is sampled in milliseconds; the order of a poly regression is
  an integer from 0 to 64 and the bandwidth of a loess is from 0 to 1. The
  extent transform writes its [min, max] into the param while the data is read.
