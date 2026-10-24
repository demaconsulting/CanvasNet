### ScadParser

<!-- cspell:ignore lexer lexical lexeme lexemes tokenize tokenized tokenizes tokenizer parenthesized -->
<!-- cspell:ignore prefixed unparenthesized EBNF -->

The `ScadParser` unit is the second software unit in the `ScadLanguage` subsystem. It comprises seven
source files in the `DemaConsulting.CanvasNet.Scad.Language` namespace, all `internal`:

- `ScadSyntax.cs` — the base records (`ScadNode`, `ScadStatement`, `ScadExpression`), the structural
  records (`ScadProgram`, `ScadParameter`, `ScadArgument`, `ScadAssignment`), and the enums
  `ScadModifiers`, `ScadUnaryOperator`, and `ScadBinaryOperator`
- `ScadStatements.cs` — the statement records
- `ScadExpressions.cs` — the expression records
- `ScadParser.cs` — `ScadParser` (sealed partial class): entry points, token cursor, error
  construction, and the limit mechanics
- `ScadParser.Statements.cs` — the statement grammar
- `ScadParser.Expressions.cs` — the expression grammar
- `ScadTreeDepth.cs` — `ScadTreeDepth` (static class): the post-parse tree-height verification

#### Purpose

`ScadParser` converts a token list (or, through the string overload, a source text) into an immutable
`ScadProgram` syntax tree, or throws a `ScadSyntaxException` at the first grammatical error or when
nesting is too deep. It is name-agnostic: every `name(args) children` is a module instantiation and
built-in names are never special, so unknown or unsupported modules never produce a syntax error. The
parser is an original implementation written from general language-design knowledge, with no OpenSCAD
source, grammar, manual text, or test material consulted.

The unit's behavior is specified by the `CanvasNetScad-ScadLanguage-ScadParser-*` requirements
(`Assignments`, `Definitions`, `Instantiations`, `Modifiers`, `Conditionals`, `Loops`, `LetEchoAssert`,
`Literals`, `Ranges`, `Precedence`, `Postfix`, `FunctionLiterals`, `Comprehensions`, `NodeLocations`,
`Immutability`, `Directives`, `SyntaxErrors`, `NestingLimit`, and `SourceLength`) in
`docs/reqstream/canvas-net-scad/scad-language/scad-parser.yaml`.

#### Data Model

**Parser state.** `ScadParser` is a single-use, non-thread-safe instance holding the token list
(`_tokens`, whose last element is always the end-of-file token), the index of the current token
(`_index`), and the current recursion depth (`_depth`). The cursor never advances past the end-of-file
token, so lookahead can never run off the list. The static `Parse` entry points are safe for
concurrent use because each call creates a new instance.

**Limit constants.** `MaxNestingDepth = 256` (applied to both parser recursion and finished-tree
height) and `MaxSourceLength = 4_194_304` UTF-16 characters.

**Syntax tree.** Every node is an immutable record (init-only properties, read-only collections) that
derives from `ScadNode`, which carries a single `Location` (`ScadSourceLocation`). Collections are
`IReadOnlyList<T>` wrapped with `List<T>.AsReadOnly()`. Record equality compares collections by
reference, so the tests compare trees through a canonical text dump instead.

Structural records (`ScadSyntax.cs`):

| Record | Fields |
| ------ | ------ |
| `ScadProgram` | `Statements` (file-scope statements in source order; empty statements produce no node) |
| `ScadParameter` | `Name` (may start with `$`), `Default` (`ScadExpression?`) |
| `ScadArgument` | `Name` (`string?`, null for a positional argument), `Value` |
| `ScadAssignment` | `Name`, `Value` (a `name = value` binding used by `for`, `let`, and comprehensions) |

Statement records (`ScadStatements.cs`; each derives from `ScadStatement`):

| Record | Fields | Notes |
| ------ | ------ | ----- |
| `ScadAssignmentStatement` | `Name`, `Value` | `name = value;` |
| `ScadModuleDefinition` | `Name`, `Parameters`, `Body` | a braced body is flattened; `;` is empty |
| `ScadFunctionDefinition` | `Name`, `Parameters`, `Body` (expression) | |
| `ScadModuleInstantiation` | `Name`, `Arguments`, `Children` | empty `Children` for a trailing `;` |
| `ScadBlockStatement` | `Statements` | a braced block standing on its own |
| `ScadModifiedStatement` | `Modifiers` (`ScadModifiers`), `Statement` | modifiers are combined flags |
| `ScadIfStatement` | `Condition`, `Then`, `Else` | `Else` is null without `else`, empty for `else ;` |
| `ScadForStatement` | `Iterators` (`ScadAssignment` list), `Body` | |
| `ScadIntersectionForStatement` | `Iterators`, `Body` | |
| `ScadLetStatement` | `Assignments`, `Body` | |
| `ScadEchoStatement` | `Arguments`, `Children` | |
| `ScadAssertStatement` | `Arguments`, `Children` | |
| `ScadDirectiveStatement` | `Kind` (`ScadDirectiveKind`), `Path` | recorded, never resolved |

Expression records (`ScadExpressions.cs`; each derives from `ScadExpression`):

| Record | Fields |
| ------ | ------ |
| `ScadNumberLiteral` | `Value` (`double`) |
| `ScadStringLiteral` | `Value` (decoded) |
| `ScadBooleanLiteral` | `Value` |
| `ScadUndefLiteral` | none |
| `ScadIdentifierExpression` | `Name` (a variable, function, or special variable) |
| `ScadVectorExpression` | `Elements` (may include comprehension elements) |
| `ScadRangeExpression` | `Start`, `Step` (null when omitted), `End` (written order) |
| `ScadUnaryExpression` | `Operator` (`Plus`, `Negate`, `Not`), `Operand` |
| `ScadBinaryExpression` | `Operator` (`ScadBinaryOperator`), `Left`, `Right` |
| `ScadConditionalExpression` | `Condition`, `WhenTrue`, `WhenFalse` |
| `ScadIndexExpression` | `Target`, `Index` |
| `ScadMemberExpression` | `Target`, `Member` (name) |
| `ScadCallExpression` | `Callee` (any expression), `Arguments` |
| `ScadFunctionLiteral` | `Parameters`, `Body` |
| `ScadLetExpression` | `Assignments`, `Body` |
| `ScadEchoExpression` | `Arguments`, `Body` |
| `ScadAssertExpression` | `Arguments`, `Body` |
| `ScadListForExpression` | `Iterators`, `Body` (comprehension element `for (iterators) body`) |
| `ScadListCForExpression` | `Initializers`, `Condition`, `Increments`, `Body` (C-style `for`) |
| `ScadListIfExpression` | `Condition`, `WhenTrue`, `WhenFalse` (null without `else`) |
| `ScadListLetExpression` | `Assignments`, `Body` (comprehension element `let`) |
| `ScadListEachExpression` | `Value` (splices the elements of a vector) |

`ScadBinaryOperator` has the members `Add`, `Subtract`, `Multiply`, `Divide`, `Modulo`, `Power`,
`Less`, `LessEqual`, `Greater`, `GreaterEqual`, `Equal`, `NotEqual`, `And`, and `Or`.
`ScadModifiers` is a `[Flags]` enum: `None = 0`, `Disable = 1` (`*`), `ShowOnly = 2` (`!`),
`Highlight = 4` (`#`), and `Background = 8` (`%`).

**Node location rule.** `Location` is the location of the token that best identifies the construct,
so that later diagnostics point somewhere useful:

- the program node is `(file, 1, 1)`, with the file name taken from the end-of-file token;
- a statement that starts with a keyword (`module`, `function`, `if`, `for`, `intersection_for`, `let`,
  `echo`, `assert`) or a directive is located at that keyword; an assignment statement and a module
  instantiation are located at the name; a block is located at its `{`; a modified statement is located
  at its first modifier character;
- a parameter, an assignment binding, and a named argument are located at the name; an argument is
  located at its first token (the name for a named argument, otherwise the first token of the value);
- a literal or identifier is located at its token; a unary or binary expression is located at the
  operator (the `^` for a power); a conditional is located at the `?`; a call is located at its `(`, an
  index expression at its `[`, and a member access at its `.`; a vector or range is located at its `[`;
  a function literal, a `let`/`echo`/`assert` expression, and a comprehension element are located at
  their keyword (`function`, `let`, `echo`, `assert`, `for`, `if`, `each`);
- parentheses produce no node, so a parenthesized expression is located where its inner expression is.

**Invariants**: a tree returned from `Parse` is complete (no partially built nodes), immutable, and at
most 256 levels high (program node at depth 0). Empty statements (`;`) produce no node at any level, an
unbraced `;` child list is empty, and `else ;` yields an empty (non-null) `Else` list. Unary minus is
never constant-folded: `-5` is `ScadUnaryExpression(Negate, ScadNumberLiteral(5))`.

#### Key Methods

- `ScadParser.Parse(string source, string? file)` (static) — checks that `source` is not null
  (`ArgumentNullException`), rejects source longer than `MaxSourceLength` with a plain
  `InvalidDataException` before any work, tokenizes with `ScadLexer.Tokenize`, and parses the tokens.
- `ScadParser.Parse(IReadOnlyList<ScadToken> tokens)` (static) — checks that `tokens` is not null,
  creates a parser instance, and calls `ParseProgramGuarded`. The tokens must end with an end-of-file
  token, as `ScadLexer.Tokenize` guarantees.
- `ParseProgramGuarded()` — runs `ParseProgram()` inside a `try` that converts
  `InsufficientExecutionStackException` into the `Nesting too deep` error, then calls
  `ScadTreeDepth.Verify` on the finished tree.
- `ParseProgram()` — `{ top-level-statement } EOF`; collects the non-null statements.
- Statement methods (`ScadParser.Statements.cs`): `ParseTopLevelStatement` (adds directives at file
  scope), `ParseStatement` (counts one level of depth, then `ParseStatementCore`, which dispatches on
  the first token), `ParseIdentifierStatement` (one-token lookahead to choose assignment or
  instantiation), `ParseModifiedOrInstantiationLike`, `ParseModifierTarget`, `ParseKeywordStatement`,
  `ParseBlock`, `ParseBlockStatements`, `ParseChildren`, `ParseSingleChild`, `ParseIdentifierChild`,
  `ParseModuleInstantiation`, `ParseModuleDefinition`, `ParseFunctionDefinition`, `ParseIfStatement`,
  `ParseForStatement`, `ParseIntersectionForStatement`, `ParseLoopHeader`, `ParseLetStatement`,
  `ParseEchoStatement`, `ParseAssertStatement`, `ParseAssignments`, `ParseAssignment`,
  `ParseParameters`, `ParseParameter`, `ParseArgumentsAfterParen`, and `ParseArgument`.
- Expression methods (`ScadParser.Expressions.cs`): `ParseExpression` (counts one level of depth),
  `ParseConditional`, `ParseBinary` (precedence climbing), `ParseUnary`, `ParsePower`, `ParsePostfix`,
  `ParsePrimary`, `ParseParenthesized`, `ParsePrefixForm`, `ParseBracketExpression`, `ParseRangeRest`,
  `ParseListElement`, `ParseListFor`, `ParseListCForRest`, `ParseListIf`, `ParseListLet`, and
  `ParseListEach`.
- Cursor and error helpers (`ScadParser.cs`): `Current`, `PeekAt`, `At`, `Advance`, `Accept`, `Expect`,
  `Enter`, `Leave`, `Error`, `ErrorExpected`, `ErrorUnexpected`, `Describe`, and `Freeze`.
- `ScadTreeDepth.Verify(ScadProgram program, int limit)` — the iterative height check described under
  _Depth and limit mechanics_.

##### Statement grammar

The grammar below is informal EBNF as implemented. Terminals in capitals are token kinds; quoted text
is a keyword or punctuation token. `{ x }` is repetition, `[ x ]` is optional, and `|` is alternation.

```text
program         = { top-level } EOF
top-level       = INCLUDE | USE                      (directive, file scope only)
                | statement
statement       = ";"                                (empty; produces no node)
                | block
                | module-def
                | function-def
                | assignment
                | modified-or-plain
block           = "{" { statement } "}"
module-def      = "module" IDENT params children
function-def    = "function" IDENT params "=" expression ";"
assignment      = IDENT "=" expression ";"
modified-or-plain
                = { modifier } target                (a block needs at least one modifier here)
modifier        = "*" | "!" | "#" | "%"
target          = block
                | instantiation
                | keyword-stmt
instantiation   = IDENT "(" arguments ")" children
keyword-stmt    = "for" "(" assignments1 ")" children
                | "intersection_for" "(" assignments1 ")" children
                | "if" "(" expression ")" children [ "else" children ]
                | "let" "(" assignments ")" children
                | "echo" "(" arguments ")" children
                | "assert" "(" arguments ")" children
children        = ";"                                (no children)
                | "{" { statement } "}"              (children are the block contents, flattened)
                | single-child
single-child    = instantiation | keyword-stmt       (unbraced; counts one level of depth)
                | modifier { modifier } target
params          = "(" [ param { "," param } [ "," ] ] ")"
param           = IDENT [ "=" expression ]
arguments       = [ argument { "," argument } [ "," ] ]
argument        = [ IDENT "=" ] expression
assignments     = [ assignments1 ]
assignments1    = IDENT "=" expression { "," IDENT "=" expression } [ "," ]
```

Notes on the statement grammar:

- A statement that starts with an identifier is an assignment when the next token is `=` and a module
  instantiation when it is `(`; anything else is an error. Module and function definitions, assignments,
  and directives are declarations: they are accepted at file scope and inside braces, but not as an
  unbraced single child (use braces) and not after a modifier. Directives are accepted only at file scope.
- `*`, `!`, `#`, and `%` combine into one `ScadModifiedStatement` whose `Modifiers` is the OR of the
  flags. A modifier may precede an instantiation, a keyword statement (`for`, `if`, and so on), or a
  block.
- The body of a module definition uses the same `children` rule as an instantiation, so
  `module m() cube(1);`, `module m() { ... }`, and `module m();` are all valid.
- An `else` binds to the nearest `if`, because a nested `if` has already consumed its own `else`; an
  `else if` chain nests in the `Else` list of the outer statement.
- `for` and `intersection_for` require at least one iterator; the iterators have nested-loop
  semantics. A C-style header (`for (init; condition; step)`) is an error in statement position and is
  legal only in a list comprehension.
- A trailing comma is accepted in parameter lists, argument lists, and assignment lists.

##### Expression precedence

From highest to lowest binding. Left-associative levels group `a op b op c` as `(a op b) op c`.

<!-- markdownlint-disable MD013 -->

| Level | Operators and forms | Associativity | AST node |
| ----- | ------------------- | ------------- | -------- |
| 1 | Primary: literals, identifiers, `( e )`, `[ … ]`, `function`/`let`/`echo`/`assert` forms | — | the literal, identifier, vector, range, or prefix-form node (parentheses add no node) |
| 2 | Postfix: call `e(args)`, index `e[i]`, member `e.name` | left, applied in source order | `ScadCallExpression`, `ScadIndexExpression`, `ScadMemberExpression` |
| 3 | `^` | right | `ScadBinaryExpression(Power)` |
| 4 | Prefix `+` `-` `!` | prefix (right) | `ScadUnaryExpression(Plus / Negate / Not)` |
| 5 | `*` `/` `%` | left | `ScadBinaryExpression(Multiply / Divide / Modulo)` |
| 6 | `+` `-` | left | `ScadBinaryExpression(Add / Subtract)` |
| 7 | `<` `<=` `>` `>=` | left | `ScadBinaryExpression(Less / LessEqual / Greater / GreaterEqual)` |
| 8 | `==` `!=` | left | `ScadBinaryExpression(Equal / NotEqual)` |
| 9 | `&&` | left | `ScadBinaryExpression(And)` |
| 10 | `\|\|` | left | `ScadBinaryExpression(Or)` |
| 11 | `c ? a : b` | right | `ScadConditionalExpression` |

<!-- markdownlint-enable MD013 -->

The expression grammar, in the same notation:

```text
expression   = conditional
conditional  = binary [ "?" expression ":" expression ]
binary       = unary { binary-op unary }               (precedence climbing, levels 5 to 10 above)
unary        = ( "+" | "-" | "!" ) unary | power
power        = postfix [ "^" unary ]
postfix      = primary { "(" arguments ")" | "[" expression "]" | "." IDENT }
primary      = NUMBER | STRING | "true" | "false" | "undef" | IDENT
             | "(" expression ")"
             | bracket
             | "function" params expression
             | "let" "(" assignments ")" expression
             | "echo" "(" arguments ")" expression
             | "assert" "(" arguments ")" expression
```

Consequences of the structure:

- `^` binds tighter than prefix minus, so `-2^2` is `-(2^2)`; the right operand of `^` may itself carry
  prefix operators, so `2^-1` parses, and `a^b^c` is `a^(b^c)`.
- Prefix operators bind tighter than `*`, so `-a * b` is `(-a) * b`; a chain such as `!-+x` nests
  rightward.
- The condition of `?:` is a `||`-level expression; the two branches are full expressions, so
  `a ? b : c ? d : e` is `a ? b : (c ? d : e)`.
- The keyword forms `function`, `let`, `echo`, and `assert` take a body that is a full expression and
  therefore extends as far right as possible: `let (x = 1) x + 2` has the body `x + 2`, and as an
  argument a function literal stops at the next `,` or `)`.
- Any expression can be called, indexed, or accessed: `f(x)[0].y`, `(function (a) a)(1)`, and
  `[1, 2][0]` are valid.

##### Vector, range, and comprehension forms

The bracket form `[` is parsed by `ParseBracketExpression`:

```text
bracket    = "[" "]"                                         (empty vector)
           | "[" element { "," element } [ "," ] "]"         (vector)
           | "[" expression ":" expression [ ":" expression ] "]"   (range)
element    = "for" "(" assignments1 ")" element              (ScadListForExpression)
           | "for" "(" assignments ";" expression ";" assignments ")" element
                                                              (ScadListCForExpression)
           | "if" "(" expression ")" element [ "else" element ]   (ScadListIfExpression)
           | "let" "(" assignments ")" element               (ScadListLetExpression)
           | "each" expression                               (ScadListEachExpression)
           | expression
```

- The first element is parsed, and when it is followed by `:` the form is a range. A range has two or
  three parts in written order: `[start : end]` has no step, and `[start : step : end]` stores the
  middle part as `Step`. A ternary expression as the start consumes its own colon first, so
  `[a ? b : c : d]` is a range whose start is the conditional. A range whose first part is a
  comprehension element, a range with a comma, or a range with more than three parts is an error.
- Comprehension elements are legal only as vector elements. Inside a vector, `let (…)` is always the
  comprehension form `ScadListLetExpression` whose body is itself an element, even when the body is an
  ordinary expression; outside a vector, `let (…) body` is `ScadLetExpression`.
- In the C-style header the initializer and increment lists may be empty (`for (; c; )`); the
  condition is required. An `else` in a comprehension binds to the nearest `if`.
- Elements nest: `[for (i = a) for (j = b) if (c) each f(i, j)]` is a chain of comprehension nodes.

#### Error Handling

The parser is fail-fast: the first grammatical error throws a `ScadSyntaxException` whose message is
`{file}:{line}:{column}: {description}` (the prefix is shortened when the file name is null). Lexical
errors are raised earlier, during tokenization, so they win over any grammatical error elsewhere in
the file (see _ScadLanguage_, `scad-language.md`). Error construction goes through three helpers:

- `ErrorExpected(expected)` — `Expected {expected} but found {token}` at the current token, or
  `Unexpected end of input; expected {expected}` at the end of the file. `Expect(kind)` uses it with
  the fixed names `an identifier`, `'('`, `')'`, `'}'`, `']'`, `';'`, `':'`, and `'='`.
- `ErrorUnexpected(token)` — `Unexpected token {token}` for a token that cannot start a statement.
- `Describe(token)` — the quoted lexeme (`'x'`), a string value in double quotes (`"x"`), or for a
  directive `'include <path>'` / `'use <path>'`.

Message catalogue (the location is the offending token unless stated otherwise):

<!-- markdownlint-disable MD013 -->

| Message | Raised when |
| ------- | ----------- |
| `Unexpected token 'x'` | a token that cannot start a statement, such as `}`, `)`, `else`, or a literal |
| `Expected ';' but found 'b'` | a statement or definition is not terminated (for example `a = 1 b`) |
| `Unexpected end of input; expected '}'` | the input ends inside a block (also `')'`, `']'`, `';'`) |
| `Expected an expression but found ';'` | an expression is required and the token cannot start one (`for` and `if` outside a vector are not expressions); `Unexpected end of input; expected an expression` at the end of the file |
| `Expected an identifier but found ')'` | a name is required (a parameter, an iterator, a module name, a member name) |
| `Expected '=' or '(' after 'foo' but found ';'` | an identifier at statement start is followed by neither; located at the token **after** the name; `Unexpected end of input; expected '=' or '(' after 'foo'` at the end of the file |
| `Expected ';', '{' or a module instantiation but found '}'` | an unbraced child is missing or is not an instantiation, keyword statement, or modified statement; at the end of the file the message is `Unexpected end of input; expected ';'` |
| `Expected '(' after 'b'` | an unbraced child starts with an identifier that is followed by neither `(` nor `=`; located at the identifier |
| `Use braces around a declaration` | a `module` or `function` definition, or an assignment (`x = 1;`), is used as an unbraced child; located at the keyword or the identifier |
| `include/use is only allowed at file scope` | a directive token appears inside a block or module body (as an unbraced child it gives the `Expected ';', '{' or a module instantiation` message) |
| `A modifier must be followed by a module instantiation or block` | modifiers precede anything else, such as an assignment or a definition; located at the token after the modifiers, including the end of the file |
| `A C-style 'for' loop is only allowed inside a list comprehension` | a statement-level `for` or `intersection_for` header contains `;`; located at the `;` |
| `Expected a function name after 'function' but found '('` | `function` at statement start is not followed by a name (a function literal is not a statement); `Unexpected end of input; expected a function name after 'function'` at the end of the file |
| `Expected ',' or ']' but found ':'` | a comprehension element is followed by `:` in a vector (it cannot be a range start); located at the `:` |
| `Nesting too deep (limit 256)` | the depth limit is exceeded; see _Depth and limit mechanics_ |

<!-- markdownlint-enable MD013 -->

Other malformed ranges and vectors use the generic forms: a range with a comma reports
`Expected ']' but found ','`, a range with four parts reports `Expected ']' but found ':'`, and an empty
vector element such as `[1,,2]` reports `Expected an expression but found ','`. A null `source` or null
`tokens` argument is a programming error and throws `ArgumentNullException`. Source over the length
limit throws `InvalidDataException("Source exceeds the maximum length of 4194304 characters")`, which is
deliberately not a `ScadSyntaxException`. `ErrorUnexpected` and `Describe` are never given the
end-of-file token, because every loop that could meet the end of the file checks for it first; the end
of the input is always reported through `ErrorExpected` (`Unexpected end of input; expected …`).

##### Depth and limit mechanics

Nesting is bounded in two complementary parts, both with the limit `MaxNestingDepth` (256) and the same
message, `Nesting too deep (limit 256)`.

**Part 1: recursion counter.** `Enter()` increments `_depth`, throws the nesting error at the current
token when `_depth` exceeds 256, and then calls `RuntimeHelpers.EnsureSufficientExecutionStack()`;
`Leave()` decrements it. One level is counted for each of: `ParseStatement` (a statement, including
each statement inside a block), the unbraced single child in `ParseChildren`, `ParseExpression` (every
nested expression, including each parenthesized, bracketed, argument, or index expression), each prefix
operator in `ParseUnary`, the right operand of `^` in `ParsePower`, and `ParseListElement` (each
comprehension element). A braced body adds no extra level because every statement inside it is parsed
through `ParseStatement`. Right operands of left-associative binary operators recurse at most one
step per precedence level and are not counted. Because `Leave()` is not called when an exception
propagates, the parser instance must not be reused after an error; it is single-use.

If the runtime stack guard fires first (for example on a thread with a very small stack), the resulting
`InsufficientExecutionStackException` is caught in `ParseProgramGuarded` and reported as the same
nesting error at the current token. A file-scope assignment `x = …;` already uses depth 2 (one for the
statement, one for the expression), so an assignment wrapped in N nested parentheses succeeds exactly
when N is at most 254, and N chained prefix operators succeed exactly when N is at most 254.

**Part 2: tree-height verification.** The parser builds left-associative operator chains
(`1+1+1+…`) and postfix chains (`a[0][0]…`, `f()()…`) in loops, so a hostile input produces an
arbitrarily deep left-leaning tree without any parser recursion, and the counter never fires. Counting
loop iterations is not a sound substitute: a nest such as `a+(…)+a+a…` can build a tree thousands of
levels deep while every individual loop stays under the limit. After a successful parse,
`ScadTreeDepth.Verify` therefore walks the finished tree with an explicit stack of
`(node, depth)` pairs. The program node has depth 0, each child has its parent's depth plus one, and a
node whose depth exceeds the limit throws `Nesting too deep (limit 256)` located at that node (the
first over-limit node the traversal reaches). `ScadParameter`, `ScadArgument`, and `ScadAssignment` are
nodes and count as levels; parentheses add no node and do not count. A left-associative chain of n
operands directly under a file-scope assignment therefore succeeds exactly when n is at most 255. The
traversal enumerates the children of every statement and expression record (`Children`,
`StatementChildren`, `ExpressionChildren`), skips absent optional children, and is safe for any tree
the parser can produce within the source size limit.

Together the two parts guarantee that every later recursive consumer of the tree (an evaluator, a
dumper, record equality, or printing) sees a tree of bounded depth, and that the parser itself cannot
overflow the stack, so hostile input fails closed with a typed, located exception.

#### Dependencies

- **`ScadLexer`** — `ScadLexer.Tokenize` (called by `Parse(string, string?)`), `ScadToken`, and
  `ScadTokenKind`. See _ScadLexer Unit Design_ (`scad-language/scad-lexer.md`).
- **`ScadSourceLocation`**, **`ScadSyntaxException`**, and **`ScadDirectiveKind`** — the public types
  of the `ScadDocument` unit; the enum is shared with `ScadDirectiveStatement`. See _ScadDocument Unit
  Design_ (`scad-document.md`).
- **.NET base class library** — `System.Runtime.CompilerServices.RuntimeHelpers`
  (`EnsureSufficientExecutionStack`), `InsufficientExecutionStackException`, `InvalidDataException`,
  and `System.Collections.Generic` collections.

#### Callers

`ScadDocument` is the only production caller: it calls `ScadParser.Parse(string, string?)`, reads
`ScadParser.MaxSourceLength` and `ScadParser.CreateSourceTooLongException()` while reading streams and
files, and extracts the directives and file-scope definition names from the returned `ScadProgram`.
`ScadTreeDepth` is called only by `ScadParser`. The tests in
`test/DemaConsulting.CanvasNet.Scad.Tests/Language/ScadParser*Tests.cs` call both `Parse` overloads
directly through `InternalsVisibleTo` and compare trees with the `ScadAstDumper` test helper.
