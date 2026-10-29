### ExpressionParser

![Expressions Structure](VegaLiteExpressionsView.svg)

The `ExpressionParser` unit is the first software unit in the `Expressions` subsystem. It
comprises six source files in the
`DemaConsulting.CanvasNet.VegaLite.Expressions` namespace, all `internal`:

- `ExpressionParser.cs` - `ExpressionParser` (sealed partial class): the static `Parse` entry point, the length check, the
  token cursor, the nesting counter, the precedence climbing of the operators, and the emission of instructions
- `ExpressionParser.Primary.cs` - the second part of the parser: literals, `datum`, identifiers, member and index access,
  calls, array and object literals, grouping, and the collection of unsupported constructs and parameter references
- `ExpressionParser.Tokenizer.cs` - `ExpressionToken`, `ExpressionTokenKind`, and the tokenizer part of the parser
- `CompiledExpression.cs` - `CompiledExpression`, `ExpressionOpcode`, `ExpressionInstruction`, `ExpressionTables`, and
  `ExpressionFindings`: the immutable result
- `ExpressionConstruct.cs` - `ExpressionConstruct`, `ExpressionConstructKind`, `ExpressionReference`, and
  `ExpressionRuntimeException`
- `NamespaceDoc.cs` - the namespace documentation of the subsystem

#### Purpose

`ExpressionParser` turns the text of a Vega expression into an immutable, shareable `CompiledExpression`, under the
expression limits of `VegaLiteLimits`, or throws a located `VegaLiteSpecException` at the first problem. It exists so that
the text of an expression is checked and understood exactly once, when the document is opened, and so that everything the
renderer must know about an expression before it runs (the constructs it cannot execute, the
parameters it reads, whether it
reads the datum) is available without evaluating anything. It is original code written from
the public documentation of the
Vega expression language.

The unit's behavior is specified by the `CanvasNetVegaLite-Expressions-ExpressionParser-*` requirements in
`docs/reqstream/canvas-net-vega-lite/expressions/expression-parser.yaml`.

#### Data Model

**Grammar.** The accepted language is a closed subset of JavaScript:

- **Literals**: `null`, `true`, `false`, decimal numbers with a fraction and an exponent, hexadecimal numbers, strings in
  single or double quotes with the JavaScript escapes, regular expression literals (`/pattern/flags` with the flags `g`,
  `i`, `m`, and `s`; a `/` begins one exactly when the previous token cannot end an
  operand, and an invalid literal, or one with a flag the engine does not honor such as `u`, is
  a located error), array literals, and object literals. The constants `PI`, `E`, `NaN`, and `Infinity` are literals too.
- **Access**: `datum` (the whole row), `datum.name` and `datum['a b']` (a field), and member and index access on any value
  (`a.b`, `a[0]`, `a['k']`). `datum` access compiles to a direct field read.
- **Operators**, from the lowest precedence: the conditional `?:`, `||`, `&&` (both return an operand and short-circuit),
  `===`, `!==`, `==`, `!=`, `<`, `<=`, `>`, `>=`, `+`, `-`, `*`, `/`, `%`, and the unary `+`, `-`, `!`, and `~`, with
  parentheses for grouping. All binary operators are left-associative.
- **Calls** of a function by name (`lower(datum.name)`); an identifier that is none of the above is a **parameter
  reference**.
- **Rejected as syntax errors**: assignment and compound assignment, `++` and `--`, `**`, `??`, `?.`, `=>`, the comma
  operator, the semicolon, template strings, `new`, `function`, the other reserved words,
  spread, and a method call such as
  `a.b()` (a call must name a function directly).
- **Parsed but not executed** (collected, evaluate to null): a call of a function that is not in the function table, the
  identifiers `signal`, `event`, and `item` (with or without a call), and the operators `|`, `^`, `&`, `<<`, `>>`, `>>>`,
  and `in`.

**`CompiledExpression`** (sealed class, immutable, thread-safe) holds the `Source` text, the `Location` of the string value
that holds it (JSON pointer, line, and column), the postfix `Program` (an array of `ExpressionInstruction`: an
`ExpressionOpcode`, an operand, an argument count, and the expression offset of the construct), the `ExpressionTables`
(constants, member and field names, and the functions the program calls), the `Depth` (the deepest nesting), the
`InstructionCount`, `ReferencesDatum`, the largest stack height, `UnsupportedConstructs`, and `ParameterReferences`.
`ExpressionConstruct` is `(Kind, Name, Offset)` with the stable `Identifier` (`vl-expr-function-<name>`,
`vl-expr-view-state`, or `vl-expr-construct-<name>`, with the deferred names `bitwise-or`, `bitwise-xor`, `bitwise-and`,
`shift-left`, `shift-right`, `shift-right-unsigned`, and `in`); `ExpressionReference` is
`(Name, Offset)` with one entry per
distinct parameter name at its first use. Offsets are zero-based UTF-16 offsets in the expression text.

**The instruction set** pushes constants, the datum, a datum field, and a parameter; reads a member or an index; applies
the unary and binary operators; makes arrays and objects; calls a function with a number of arguments; and jumps (the jumps
that keep or pop the tested value for `&&` and `||`, the conditional jump, and the plain
jump). An unsupported construct is
an instruction that pops its operands and pushes null, so the stack stays consistent.

**Limits and invariants.** The text is at most `MaxExpressionLength` characters (4,096 by default); the nesting is at most
`MaxExpressionDepth` (64 by default, at most 256). Nesting is counted for groups, brackets, call argument lists, array and
object literals, conditional branches, and unary operands; the nesting of level `MaxExpressionDepth` is accepted and
the next level is rejected before it is entered. A parser instance parses one text; `Parse` itself is stateless and
thread-safe.

#### Key Methods

- `ExpressionParser.Parse(string source, VegaLiteLocation location, VegaLiteLimits? limits)` - requires non-null text,
  uses the default limits when none are given, compares the length with `MaxExpressionLength` **before tokenizing** (a
  hostile text costs one comparison), tokenizes, compiles, and returns the `CompiledExpression`. `location` is the location
  of the string value in the specification; every error carries it unchanged.
- The tokenizer reads the text in one forward pass with an index (no recursion), skipping white space, and classifies each
  token as a number, string, identifier, punctuator, or regular expression; it keeps the
  operators the language rejects as
  single tokens so that the parser can name them in its message.
- The precedence climber parses a binary operator chain in a loop: the operand is parsed, and while the next operator binds
  at least as tightly as the current level, the right operand is parsed one level higher, which makes a chain of any length
  iterative. `&&`, `||`, and `?:` emit a forward jump and patch its target when the right operand is complete, so a skipped
  operand is skipped at run time. A read `datum` or identifier waits for a following `.`, `[`, or `(` before it is emitted,
  which is how `datum.x` becomes one field read and `f(x)` one call.
- The nesting counter is incremented when the parser enters a nesting construct and decremented when it leaves, and it
  throws as soon as the next level would exceed the limit.
- A call of a name that is in `ExpressionFunctions` compiles to a call instruction; a call of
  any other name is recorded as
  an unsupported function construct and compiled as an instruction that, after the arguments were compiled (so their own
  constructs are still collected), pops them and pushes null.

**Preconditions and postconditions**: the text is non-null; on return the `CompiledExpression` is complete and within the
limits, or a `VegaLiteSpecException` was thrown and nothing is returned; no function was executed.

#### Error Handling

Every problem with the text is a `VegaLiteSpecException` whose location is the location of the expression string and whose
message has the fixed form `{location}: Invalid expression: {description} at expression offset {N}.` with the zero-based
offset of the first problem: a text longer than `MaxExpressionLength` (the offset is the limit), nesting beyond
`MaxExpressionDepth` (the offset of the offending bracket or operator), an unexpected or unterminated token, an unknown
character, an invalid number, string escape, or regular expression literal, and the rejected syntax listed above. A null
text is an `ArgumentNullException`, which is not converted. Constructs that are well formed but not executable are not errors:
they are collected for the `FeatureCatalog`. The unit performs no I/O and never evaluates.

#### Dependencies

- **`DataValue`** - the constant values of a program, in the `DataTable` unit of the `Data` subsystem
- **`ExpressionFunctions`** - the function table that decides whether a call is supported, in the `ExpressionFunctions`
  unit
- **`VegaLiteLimits`**, **`VegaLiteLocation`**, and **`VegaLiteSpecException`** - the public types of the `VegaLiteDocument`
  unit
- **.NET base class library** - `System.Globalization` (invariant-culture number parsing) and `System.Text`

#### Callers

`SpecParser` (with `SpecParser.Transforms.cs`) calls `Parse` for every expression of a supported site when a document is
opened and keeps the result in the `SpecTree`; the `Transforms` and `Marks` subsystems read the compiled expressions from
there. The tests in `test/DemaConsulting.CanvasNet.VegaLite.Tests/Expressions/ExpressionParserTests.cs` call `Parse`
directly through `InternalsVisibleTo`.
