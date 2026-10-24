<!-- cspell:ignore openscad scad -->

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

OpenSCAD itself is licensed under the GPL and its manual under CC BY-SA. Nothing from either is included here, and
nothing may be added to this folder that is. If a new fixture is needed, author it fresh.

`.scad` files are not spell-checked by the repository lint, and the repository normalizes text files to LF line
endings on checkout. Tests that depend on other line-ending conventions build their input in code instead.

## Fixtures

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
