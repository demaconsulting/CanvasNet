<!-- cspell:ignore openscad scad multmatrix -->

# Scad Test Fixtures

The `.scad` files in this folder are the test fixtures for `DemaConsulting.CanvasNet.Scad`.

## Provenance

All fixtures were written for CanvasNet and are original works. They are licensed together with the repository
under the MIT license.

They are **not** derived from, copied from, translated from or checked against OpenSCAD's source code, grammar
files, test corpus, manual text or manual examples, or any OpenSCAD library or third-party OpenSCAD-derived
parser or grammar. They were written from general knowledge of C-family language design only. The parts and
scenes (a shelf bracket, rings of pegs, grids and so on) were invented for these tests; any resemblance to
common idioms is generic and coincidental.

The `render-*.scad` fixtures (geometry and rendering) are original works of the same kind, authored for CanvasNet from
general geometry (the shapes, coordinates and sizes were chosen so that their silhouettes can be computed by hand;
the convex outlines of `render-hull.scad` and `render-minkowski.scad` follow from the definition of a convex hull
and of a Minkowski sum of boxes alone, and were worked out on paper).
They are **not** derived from OpenSCAD's source code, test corpus, manual text or manual examples. The tests that use
them (`ScadRenderSystemTests`, `ScadSystemIntegrationTests`) assert only analytic properties of the models - covered
pixel rectangles and polygons, sample-pixel colors, symmetry, bounds, determinism and reports - computed in the test
itself. There are no reference images: no expected image was produced by, compared with or generated from OpenSCAD or
any other implementation, and the render fixtures have no expected-output files beside them.

OpenSCAD itself is licensed under the GPL and its manual under CC BY-SA. Nothing from either is included here, and
nothing may be added to this folder that is. If a new fixture is needed, author it fresh.

`render-transparency.scad` (Phase 8) and the transparency edits of `render-modifiers.scad` and `render-unsupported.scad`
are original works of the same kind, written for CanvasNet in this repository. The translucent boxes, the cavity, the
ghost, the highlighted cutter, and the colors with alpha were invented for these tests; the expected pixels are not
read from any image or program: the tests compute them from the closed form of front-to-back alpha compositing of the
documented colors (a half-transparent surface seen from above shows its near and far wall) and from the documented
default overlay colors of `ScadRenderOptions`. Nothing in them was derived from, compared with, or checked against
OpenSCAD's source code, test corpus, manual text, manual examples, or rendering behavior; the semantics of the `%` and
`#` modifiers in CanvasNet (a ghost that is not part of the model, a highlight that still takes part in the booleans)
are written down in `docs/design/canvas-net-scad.md` as CanvasNet's own decisions.

`.scad` files are not spell-checked by the repository lint, and the repository normalizes text files to LF line
endings on checkout. Tests that depend on other line-ending conventions build their input in code instead.

The evaluation fixtures (`eval-*.scad`, the `libs/` folder) and every expected-output file next to them
(`.expected.txt`, `.diagnostics.txt`, `.unsupported.txt`, `.tree.txt`) are original works of the same kind. The
expected outputs were **not** produced by running or consulting OpenSCAD or any other implementation: they are the
output of the CanvasNet evaluator, reviewed line by line against the dialect rules documented for CanvasNet (see
`docs/design/canvas-net-scad.md`) before being committed, and they are CanvasNet's own specification of its dialect.
They pin the behavior of this implementation and make no claim of parity with any other. Where a result is a
deliberate CanvasNet choice (for example how a scalar is iterated, or how a number is printed) the fixture is
named after the topic, not after any other implementation's behavior.

## Fixtures

<!-- markdownlint-disable MD013 -->

| File | Purpose |
| ---- | ------- |
| `basic-shapes.scad` | Variables, special variables, primitive calls with positional and named arguments, transform chains and blocks |
| `modules-and-functions.scad` | Module and function definitions with default parameters, nested definitions, `children()`, recursion |
| `control-flow.scad` | `if`/`else if`/`else`, `for` with several iterators, `intersection_for`, `let`, `echo` and `assert` |
| `list-comprehensions.scad` | `for`, nested `for`, `if`/`else`, `let`, `each`, C-style `for` and mixed elements |
| `expressions-and-operators.scad` | Every operator, precedence-sensitive expressions, ternary chains, indexing, members and ranges |
| `function-literals.scad` | Function literals, immediate calls, passing and returning functions, calls on arbitrary expressions |
| `modifiers.scad` | The `*`, `!`, `#` and `%` modifiers, combined and on calls, loops, conditionals and blocks |
| `include-and-use.scad` | `include <...>` and `use <...>` directives; the paths intentionally do not exist and are never resolved |
| `lexical-forms.scad` | Comments, string escapes, number forms and special variables |
| `parametric-bracket.scad` | A realistic shelf bracket using most features; its module and function names are asserted by a system test |
| `invalid-unterminated-block-comment.scad` | A block comment that is never closed |
| `invalid-missing-semicolon.scad` | A statement without its terminating semicolon |
| `invalid-unbalanced-brace.scad` | A module body whose closing brace is missing |
| `eval-scoping.scad` | Last assignment wins, hoisted definitions, lazy and circular variables, block, module, `let` and loop scopes |
| `eval-special-variables.scad` | Defaults, dynamic scoping of `$` variables through modules, functions and `children()`, `$`-named arguments |
| `eval-modules-and-children.scad` | Module defaults, named arguments, `children()` with index and range, `$children`, recursion, user modules shadowing built-ins |
| `eval-functions-and-recursion.scad` | Recursion, tail recursion 100 000 levels deep, mutual recursion, named arguments, `echo`/`let`/`assert` expressions |
| `eval-closures.scad` | Function literals, captured variables, composition, immediate calls, type tests |
| `eval-control-flow.scad` | Ranges (including descending and fractional), several iterators, `if`/`else`, `let`, `intersection_for` |
| `eval-comprehensions.scad` | Every list-comprehension form including `each`, nested and C-style generators |
| `eval-operators-and-vectors.scad` | Operators, comparison, truthiness, vector and matrix arithmetic, indexing, members, precedence |
| `eval-builtin-math.scad` | Numeric built-in functions, rounding, trigonometry in degrees, `norm`, `cross`, wrong-type warnings |
| `eval-builtin-lists-and-strings.scad` | `len`, `concat`, `str`, `chr`, `ord`, `lookup`, `search`, seeded `rands`, `version` |
| `eval-number-formatting.scad` | How numbers, strings, lists, ranges and functions print in `echo` and `str` |
| `eval-overrides.scad` | Variable overrides: run as written and with `width`, `tag`, `$fn` and `extra` supplied |
| `eval-include-main.scad` | `include` and `use` of `libs/defs.scad`, `libs/nested/inner.scad` and `libs/shapes.scad`, from disk and through a resolver |
| `eval-instance-tree.scad` | The instantiation tree of a parametric bracket with modifiers, loops and fragment settings |
| `eval-diagnostics.scad` | Every kind of survivable problem and its warning text and location |
| `eval-unsupported.scad` | Recognized but unsupported functions, reported once per location |
| `render-primitives.scad` | A colored cube, sphere and cylinder side by side: exact colors, footprints and heights |
| `render-bracket-csg.scad` | A plate with a round through hole and a rectangular slot (`difference`) |
| `render-intersection.scad` | A block trimmed by a round cutter (`intersection`): corners lost, sides kept |
| `render-transforms.scad` | `translate`, `rotate`, `mirror`, `scale`, `resize` and `multmatrix` on separately colored parts with known extents |
| `render-extrusions.scad` | `linear_extrude` (star with a hole, `scale`, `twist` with `center`) and `rotate_extrude` (vase and ring) |
| `render-2d-csg.scad` | 2D `difference` and `intersection` of circles and squares distributed into extrusions |
| `render-polyhedron.scad` | A pyramid, an octahedron and an L-shaped prism with a concave face, faces listed clockwise from outside |
| `render-modules-scene.scad` | A recursive staircase, a ring of posts made with a loop and `children()`, and a painting module |
| `render-modifiers.scad` | The `*`, `#` and `%` modifiers: disabled (nothing drawn), highlighted (drawn, with a translucent copy in front) and background (only a translucent ghost) |
| `render-root-modifier.scad` | The `!` modifier: only the marked subtree is drawn (a highlight inside it included) |
| `render-parametric.scad` | A peg board whose size comes from variables that the tests override (the `-D` equivalent) |
| `render-unsupported.scad` | One recognized but unsupported construct per line (modules, a hull of mixed dimensions, a Minkowski sum with a boolean operand, a `%` on a 2D shape that is not extruded) next to supported geometry |
| `render-text.scad` | `text()` with the default bundled font, all extruded: centered text with holes, an extruded line with a descender, an extruded 2D difference of text, a 2D hull of text and a `rotate_extrude` of a glyph |
| `render-transparency.scad` | Color alpha (vector, hex, `alpha=`), a translucent cube alone and over an opaque plate, a translucent difference with a blind cavity, a `%` ghost that passes through a solid without cutting it, a `#` on a subtracted cylinder, and a `%` of an extruded circle |
| `render-hull.scad` | `hull` of two cubes (a hexagonal prism), of 2D squares (extruded), of two spheres (a capsule), and as a cutter in a difference |
| `render-minkowski.scad` | `minkowski` of a block and a ball (a rounded box), of a union of blocks (distributed), and of a hull and a cube |
| `libs/defs.scad`, `libs/nested/inner.scad`, `libs/shapes.scad` | Files included or used by `eval-include-main.scad`; they are not enumerated by the parse tests |

## Expected Outputs

Each `eval-<topic>.scad` is evaluated through the public `ScadDocument.Evaluate` and compared with the files beside it.
Line endings are normalized before comparison and the comparison is culture-invariant. A line that starts with `#`
(a hash and a space) in an expected-output file is a comment (it carries spelling directives) and is ignored.

| File | Content | When absent |
| ---- | ------- | ----------- |
| `eval-<topic>.expected.txt` | The `echo` lines, one per line, including the `ECHO:` prefix | Required |
| `eval-<topic>.diagnostics.txt` | The warnings as `line:column: message` | No warnings are expected |
| `eval-<topic>.unsupported.txt` | The unsupported features as `line:column: feature: message` | None are expected |
| `eval-<topic>.tree.txt` | The instantiation tree, one instance per line, children indented two spaces | The tree is not compared |
| `eval-overrides.overridden.expected.txt` | The echo lines when the caller supplies `width = 6`, `tag = "custom"`, `$fn = 48`, `extra = [1, 2]` | - |

The tree is internal, so it is read through the test helper `ScadInstanceDumper`; its first column shows a modifier
(`!`, `#`, `%`), `group<name>` for the groups the evaluator creates, and `name(arguments)` for built-in modules, followed
by any fragment setting that differs from the default.

<!-- markdownlint-enable MD013 -->

## Expected Errors

| File | Line | Column | Description |
| ---- | ---- | ------ | ----------- |
| `invalid-unterminated-block-comment.scad` | 4 | 1 | The opening `/*` of the unterminated comment |
| `invalid-missing-semicolon.scad` | 4 | 1 | The token after the unterminated assignment (`height`) |
| `invalid-unbalanced-brace.scad` | 5 | 1 | End of input, where the closing `}` was expected |

## Clean-Room Rule

The OpenSCAD language support in CanvasNet is implemented clean-room: from general knowledge of the language only,
without consulting, copying, porting or paraphrasing OpenSCAD source code, grammar files, tests, manual prose or
examples. Fixtures follow the same rule.
