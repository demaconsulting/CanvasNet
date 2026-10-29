### ExpressionParser Unit Verification Design

This document describes the unit-level verification strategy for the `ExpressionParser` unit in the
`DemaConsulting.CanvasNet.VegaLite.Expressions` namespace: the internal static `ExpressionParser` class (`Parse`, with
its tokenizer and primary-expression files) and the types that carry its result — `CompiledExpression` (the immutable,
shareable postfix program with its `Source`, `Location`, `Depth`, `InstructionCount`, `ReferencesDatum`,
`UnsupportedConstructs`, and `ParameterReferences`), `ExpressionConstruct` and `ExpressionConstructKind` (a function,
view state, or deferred operator that is parsed but not executed, with its stable `vl-expr-*` identifier), and
`ExpressionReference` (a parameter name with the offset of its first use). The parser compiles the text of one
expression of the supported Vega expression subset in a single pass into a postfix program (a stack machine with
forward jumps for `&&`, `||`, and `?:`); it builds no syntax tree.

#### Verification Approach

The unit is verified by compiling small, original expression texts and asserting on the compiled program, on the
values the program produces, and on the exact message and location of every failure. The unit depends only on
`VegaLiteLimits`, `VegaLiteLocation`, and `VegaLiteSpecException`, so no mocking or stubbing is required. The
`ExpressionTestSupport` helpers compile a text for a fixed location (file `spec.vl.json`, pointer
`/transform/0/calculate`, line 12, column 17) and evaluate it with the real evaluator where the observable result of a
parse is a value; the internal types are reached through the test assembly's `InternalsVisibleTo` access. The evaluator
itself is verified under its own design (see _ExpressionEvaluator Unit Verification Design_) and the interaction of the
units is verified at the subsystem tier (see _Expressions Subsystem Verification Design_).

The expected values are written out by hand from the JavaScript grammar and the public Vega expression documentation,
never produced by running a JavaScript engine: `1 + 2 * 3` is `7`, `2 - 3 - 4` is `-5` (left associative),
`0 || 1 && 0` is `0` and `1 || 0 && 0` is `1` (`&&` binds tighter), `1 ? 2 : 3 ? 4 : 5` is `2` (the conditional is
right associative), `!1 + 1` is `1` (the unary operator binds tighter than `+`), and `'a' + 'b' + 1 + 2` is `ab12`
while `1 + 2 + 'a'` is `3a`. The limit scenarios count by eye: a text of 4,096 characters is accepted and one of 4,097
fails with the offset 4,096; 64 nested parentheses are accepted and 65 fail at the offset of the 65th, which is 64; and
a chain of 2,048 ones joined by 2,047 plus signs has the instruction count 2,048 + 2,047 and the depth zero.

The parse-error scenario is a theory of forty-three malformed texts, each with its exact description and offset (for
example `1 + * 2` is "unexpected '*'" at offset 4, `(1` is "expected ')' but found end of expression" at offset 2, and
`x.y()` is the unsupported method call at offset 3). The message form is fixed, and the tests compare whole messages:
`{location}: Invalid expression: {description} at expression offset {N}.`, where the location is the one the caller
passed (the owning JSON pointer and the line and column of the string value).

Unit tests reside in `ExpressionParserTests.cs` within the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the
`Expressions` folder, with the shared helpers in `ExpressionTestSupport.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test compiles its own texts; no files are read and nothing is shared between tests. The
  concurrency scenario compiles one text in sixty-four parallel iterations and compares each result with a baseline

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any wrong literal value, wrong precedence or associativity, wrong instruction count or depth, wrong or missing
unsupported construct or parameter reference, an exception of the wrong type (an `ArgumentNullException` where a located
`VegaLiteSpecException` is documented, or the reverse), a wrong message, offset, or location, a stack overflow, or any
difference between repeated or concurrent parses constitutes a failure.

#### Unit-Level Test Scenarios

##### Literals Have JavaScript Values

**Tests**: `ExpressionParserTests.ExpressionParser_Parse_NumberLiterals_HaveJavaScriptValues`,
`ExpressionParserTests.ExpressionParser_Parse_StringLiterals_DecodeEscapes`,
`ExpressionParserTests.ExpressionParser_Parse_KeywordsAndConstants_HaveTheirValues`,
`ExpressionParserTests.ExpressionParser_Parse_ArrayAndObjectLiterals_BuildCompositeValues`

Asserts that number literals of every supported syntax have their exact values (`0`, `42`, `1.5`, `.5`, `5.`, `1e3`,
`1E3`, `1.5e-2`, `2e+2`, `0x1F`, `0Xff`, and `007` as `7`); that string literals in single or double quotes decode the
escapes `\n`, `\t`, `\\`, `\'`, `\x41`, `\u0042`, `\u{43}`, `\u{1F600}`, `\0`, an unknown escape such as `\q` (the
letter itself), and a backslash before a line break (the line break is dropped); that `null`, `true`, `false`, `PI`, `E`,
`NaN`, `Infinity`, and `-Infinity` have their values; and that an array literal with a trailing comma keeps its four
elements in order (the last one an object) and an object literal keeps the insertion order of its keys, converts the
number key `3` to the text `3`, and has a missing member that reads as null.

##### Precedence And Associativity Match JavaScript

**Tests**: `ExpressionParserTests.ExpressionParser_Parse_PrecedenceAndAssociativity_MatchJavaScript`

Asserts, as a theory of twenty-two hand-computed texts, the results listed above and the others of the same kind:
parentheses, left associativity of `-` and `/`, `*` and `%` at one level, a double unary minus, `+` before `<` before
`===` before `&&` before `||`, and a right-associative conditional with a true, a false, and a nested branch.

##### Datum Access Is Recorded And Reads Fields

**Tests**: `ExpressionParserTests.ExpressionParser_Parse_DatumAccess_IsRecordedAndReadsFields`

Asserts that `datum.x`, `datum['a b']`, and `datum.n.m + datum['x']` record that the expression reads the datum while
`1 + 2` does not, that a field read is one instruction (the row is not materialized for it), and that plain, spaced,
and nested members read their values (5, 6, and 12), a missing member is null, and a member of a missing member is null
rather than an error.

##### Parameter References Are Collected Once Per Name

**Tests**: `ExpressionParserTests.ExpressionParser_Parse_ParameterReferences_AreCollectedOncePerName`

Asserts that `alpha + beta * alpha + abs(gamma) + datum.x` lists exactly the three names `alpha`, `beta`, and `gamma`
in order of first use with their zero-based offsets (0, 8, and 27), and that nothing is reported as unsupported: the
call name, the constants, and `datum` are not parameter references.

##### Unsupported Constructs Are Collected, Not Syntax Errors

**Tests**: `ExpressionParserTests.ExpressionParser_Parse_UnsupportedConstructs_AreCollectedInOrder`,
`ExpressionParserTests.ExpressionParser_Parse_MixedCaseFunction_HasLowerCaseIdentifierAndOriginalName`,
`ExpressionParserTests.ExpressionParser_Parse_DeferredOperators_AreNamed`

Asserts that an unknown function (`foo`, `datetime`), the view state names `signal`, `event`, and `item`, and the
operators of a later phase (`&`, `|`, `^`, `<<`, `>>`, `>>>`, and `in`) parse and are collected in order of appearance
with their kind, name, and offset, and with the stable identifiers `vl-expr-function-<name>` (the name lower-cased, while
the construct keeps the name as written: `timeFormat` and `TIMEFORMAT` both give `vl-expr-function-timeformat`),
`vl-expr-view-state`, and `vl-expr-construct-<name>` (`bitwise-and`, `bitwise-or`, `bitwise-xor`, `shift-left`,
`shift-right`, `shift-right-unsigned`, and `in`); that the parameter names of the operands are still collected; and that
each deferred operator evaluates to null, so the feature catalog and strict mode, not the parser, decide what happens to
them.

##### Regular Expression Literals And Division

**Tests**: `ExpressionParserTests.ExpressionParser_Parse_RegExpLiteralsAndDivision_AreDistinguished`,
`ExpressionParserTests.ExpressionParser_Parse_InvalidRegExpLiteral_IsLocatedSpecError`

Asserts that a slash after an operator or an opening parenthesis starts a regular expression literal (`/^a[/]b$/`
with a slash inside a class, and `/x/ig`, which prints as `/x/gi`) while a slash after an operand is a division
(`(4) / 2 / 2` is `1` and `[8][0] / 4` is `2`); and that an unbalanced group and an unknown flag in a literal are
located `VegaLiteSpecException` errors whose message ends with the offset of the literal (4 and 0) and names the
invalid pattern or the flags.

##### Syntax Errors Carry The Location And The Offset

**Tests**: `ExpressionParserTests.ExpressionParser_Parse_SyntaxError_CarriesLocationAndOffset`,
`ExpressionParserTests.ExpressionParser_Parse_MalformedExpression_ReportsFirstProblemAndOffset`,
`ExpressionParserTests.ExpressionParser_Parse_NullText_ThrowsArgumentNullException`

Asserts that `1 +` throws a `VegaLiteSpecException` whose location is the pointer `/transform/0/calculate` with line
12, column 17, and file `spec.vl.json`, and whose message is exactly
`spec.vl.json:12:17 /transform/0/calculate: Invalid expression: unexpected end of expression at expression offset 3.`;
that each of the forty-three malformed texts reports the first problem with its description and offset, including the
texts that are syntax errors by decision — method calls (`x.y()`, `(1)()`, `datum()`), assignments and increments
(`a = 1`, `a += 1`, `a++`), `**`, `??`, arrow functions, the comma and the semicolon, and the words `new`, `function`,
`typeof`, and `this` — and the lexical problems (an unterminated string, a bad escape, an octal escape, a number
followed by a letter, `0x` without digits, `1e999`, an invalid character, and an empty or unterminated regular
expression literal); and that a null text is an `ArgumentNullException`, not a specification error.

##### Length Limit

**Tests**: `ExpressionParserTests.ExpressionParser_Parse_LengthLimit_AcceptsUpToLimit`,
`ExpressionParserTests.ExpressionParser_Parse_OverlongTextWithInvalidCharacter_ReportsLengthFirst`

Asserts that the number `1` padded to 4,095 and to 4,096 characters is accepted and padded to 4,097 characters fails
with the message "the expression has 4097 characters, more than the limit of 4096 (MaxExpressionLength) at expression
offset 4096." at the given location, and that a text that is both too long (limit 5) and invalid is reported as too
long, so an oversize text is never tokenized.

##### Depth Limit Is Exact And Stack Safe

**Tests**: `ExpressionParserTests.ExpressionParser_Parse_DepthLimit_AcceptsUpToLimit`,
`ExpressionParserTests.ExpressionParser_Parse_EveryNestingConstruct_IsBoundedByDepthLimit`,
`ExpressionParserTests.ExpressionParser_Parse_MaximumConfiguredDepth_DoesNotOverflowTheStack`

Asserts that 63 and 64 nested parentheses are accepted (the reported depth equals the nesting and the value is `1`) and
65 fail with "the expression is nested more than 64 levels deep (MaxExpressionDepth) at expression offset 64."; that
every nesting construct counts — brackets, call arguments, array literals, object literals, unary `-` and `!`
operands, conditional branches, and an index in brackets — by failing at 65 levels and passing at 5; and that, with
the ceiling limit of 256, a nest of 256 levels (128 unary minus signs in parentheses, which use several recursive
routines per level) parses without exhausting the stack and one more level is rejected.

##### Long Chains Do Not Recurse

**Tests**: `ExpressionParserTests.ExpressionParser_Parse_LongOperatorChain_IsNotRecursive`,
`ExpressionParserTests.ExpressionParser_Parse_LongLogicalChain_EvaluatesWithoutRecursion`

Asserts that a chain of 2,048 ones joined by 2,047 plus signs compiles to depth zero and exactly 4,095 instructions and
evaluates to 2,048, and that chains of 1,000 `||` and 500 `&&` operands evaluate to their last operand, so the length
of a left-associative chain has no influence on the stack.

##### Compiled Expression Metadata And Determinism

**Tests**: `ExpressionParserTests.ExpressionParser_Parse_CompiledExpression_ExposesSourceAndLocation`,
`ExpressionParserTests.ExpressionParser_Parse_RepeatedAndConcurrent_GivesIdenticalResults`

Asserts that `1 + 2` has the source `1 + 2`, the given location, three instructions, and depth zero, and that
sixty-four parallel compilations of an expression with an unknown function, a parameter reference, a conditional, and
view state all have the same instruction count, depth, unsupported constructs, and parameter references as the
baseline, so the compiled program is immutable and the parser keeps no shared state.
