### ScadParser Unit Verification Design

<!-- cspell:ignore Stmts -->

This document describes the unit-level verification strategy for the `ScadParser` unit: the internal
`ScadParser` class (the static `Parse` entry points, with recursive descent for statements and precedence
climbing for expressions), the immutable syntax-tree records (`ScadSyntax`, `ScadStatements`, and
`ScadExpressions`), and the `ScadTreeDepth` tree-height verifier, in the `DemaConsulting.CanvasNet.Scad.Language`
namespace.

#### Verification Approach

The unit is verified by parsing small source strings and asserting on the resulting tree, on the exact error
raised, or on both. Trees are asserted as strings through the `ScadAstDumper` test helper rather than by record
equality (see below), and errors are asserted as a `ScadSyntaxException` with an exact one-based line and column
and a message fragment. The parser is name-agnostic (every `name(arguments) children` statement is a module
instantiation), so the tests use arbitrary names without needing any module library. Parsing is fail-fast and
tokenizes the whole source first; the interplay with the lexer is verified at the subsystem tier (see
_ScadLanguage Subsystem Verification Design_). The unit has no dependencies other than the lexer, which is used
for real, so no mocking or stubbing is required; the internal types are reached through the test assembly's
`InternalsVisibleTo` access.

Each documented strictness rule is pinned by a test: for example, a C-style `for` header is accepted only inside
a list comprehension, a modifier may precede only an instantiation-like statement or a block, `include` and
`use` are accepted only at file scope, an assignment or definition is not an unbraced child, and a range cannot
contain a comma or start a comprehension. Hostile-input behavior is verified at the limits: nesting of exactly
256 levels is accepted and one more is rejected for each construct that recurses, left-associative and postfix
chains are bounded by the verified height of the finished tree, deep input does not overflow the call stack on
a thread with a 1 MiB, 256 KiB, or 64 KiB caller stack (the parser parses inline and falls back to its own
sized thread when the stack is too small; only the outcome is asserted there, because the stack size a platform
really grants differs), and source of
exactly 4,194,304 characters parses while one
more character throws a plain `InvalidDataException` (not a `ScadSyntaxException`, because no location is
meaningful for it). Syntax errors are `ScadSyntaxException`, which derives from `FormatException` and not
`InvalidDataException`.

##### Test Helpers

`ScadParserTestSupport` (in `ScadParserTestSupport.cs`) provides `Parse` (parse with an optional file name),
`Stmts` (parse and dump the statements separated by spaces), `ExprNode` (parse an expression as the value of an
assignment `x = ...;` and return its node), `Expr` (the same, dumped), `AssertParseError` (assert a syntax error
at an exact location with a message fragment), and `Repeat` (build hostile inputs by repetition).

`ScadAstDumper` (in `ScadAstDumper.cs`) is a test helper that renders a syntax tree as a compact, location-free
S-expression string, so that every tree assertion is a string comparison. It dumps each node kind as a
parenthesized list headed by a short tag, for example `1 + 2 * 3` as `(+ 1 (* 2 3))` and
`for (i=[0:3]) cube(i);` as `(for ((i (range 0 3))) (inst cube (i)))`. Positional arguments dump as their
expression and named arguments as `(arg name value)`; a parameter with a default dumps as `(= name default)`;
modifier flags dump as characters in the fixed order `* ! # %`; numbers dump with the invariant culture and
infinity as `inf`; and unary operators dump as `pos`, `neg`, or `!`. An if statement dumps its branches as
`(then ...)` and `(else ...)`, so an absent else is distinguishable from an empty one. Because the dumper
recurses, it relies on the parser's depth bound for safety. It dumps a whole program with `Dump` (used by the
subsystem tests) or only the statements with `DumpStatements`.

Unit tests reside in `ScadParserStatementTests.cs` (assignments, definitions, instantiations, modifiers, and
directives), `ScadParserControlFlowTests.cs` (conditionals, loops, and the let, echo, and assert forms),
`ScadParserExpressionTests.cs` (literals, vectors, ranges, postfix forms, and function literals),
`ScadParserPrecedenceTests.cs`, `ScadParserComprehensionTests.cs`, `ScadParserErrorTests.cs`,
`ScadParserLimitTests.cs`, and `ScadParserNodeTests.cs` (node locations and immutability) within the
`DemaConsulting.CanvasNet.Scad.Tests` project, in the `Language` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Threads**: The stack-safety scenarios run the parse from a caller thread with a 1 MiB, 256 KiB, or 64 KiB
  stack and join it before asserting; a normal parse runs on the caller's thread and only a parse that the
  caller's stack cannot hold falls back to the parser's own bounded 16 MiB thread (observed through the internal
  `LastParseThreadId`, `FallbackCount`, and `PeakConcurrentFallbackThreads` hooks); the fallback scenarios do not
  rely on a small stack, which a platform may round up, but force the fallback through the per-thread internal
  `ScadParser.ForceFallbackForCurrentThread` seam
- **Isolation**: Each test parses its own source strings; no files are read

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those
explicitly asserted. Any unexpected exception, wrong exception type, wrong tree shape, or wrong location,
message, or limit behavior constitutes a failure. The unit tests are written to cover every line and branch of
the parser, the syntax-tree types, and `ScadTreeDepth`; the Phase 4 coverage run of the `Scad` assembly reported
100% sequence-point and 100% branch-point coverage.

#### Unit-Level Test Scenarios

##### CanvasNetScad-ScadLanguage-ScadParser-Assignments: Variable Assignments

**Tests**: `ScadParserStatementTests.ScadParser_Parse_Assignment_FileScope_ProducesAssignmentStatement`,
`ScadParserStatementTests.ScadParser_Parse_SpecialVariableAssignment_ProducesAssignmentStatement`,
`ScadParserStatementTests.ScadParser_Parse_AssignmentInBlock_ProducesBlockOfAssignments`

Asserts that `x = 5;` and `$fn = 64;` dump as assignment statements, that assignments inside a block dump as a
block of assignments, and that empty statements (`;`) produce no nodes.

##### CanvasNetScad-ScadLanguage-ScadParser-Definitions: Module And Function Definitions

**Tests**: `ScadParserStatementTests.ScadParser_Parse_ModuleDefinition_WithDefaultParameters_ProducesModuleDefinition`,
`ScadParserStatementTests.ScadParser_Parse_FunctionDefinition_ProducesFunctionDefinition`,
`ScadParserStatementTests.ScadParser_Parse_NestedDefinitionInBlock_ProducesNestedDefinitions`,
`ScadParserStatementTests.ScadParser_Parse_ModuleWithSingleStatementBody_ProducesSingleChildBody`,
`ScadParserStatementTests.ScadParser_Parse_ParameterTrailingComma_IsAccepted`

Asserts that a module with positional, defaulted, and special-variable parameters and a function with a default
parameter produce the expected definition nodes; that a module and a function nested in a braced body parse; that
a module with a single unbraced statement body has that statement as its only child and that `module m();` has an
empty body; and that a trailing comma in a parameter list is accepted for both modules and functions.

##### CanvasNetScad-ScadLanguage-ScadParser-Instantiations: Module Instantiations

**Tests**: `ScadParserStatementTests.ScadParser_Parse_Instantiation_PositionalAndNamedArguments_ProducesArguments`,
`ScadParserStatementTests.ScadParser_Parse_Instantiation_SemicolonOnly_HasNoChildren`,
`ScadParserStatementTests.ScadParser_Parse_Instantiation_SingleChild_ProducesNestedInstantiation`,
`ScadParserStatementTests.ScadParser_Parse_Instantiation_BlockChildren_AreFlattened`,
`ScadParserStatementTests.ScadParser_Parse_Instantiation_ChainOfSingleChildren_NestsRightward`,
`ScadParserStatementTests.ScadParser_Parse_Instantiation_UnknownName_Parses`,
`ScadParserStatementTests.ScadParser_Parse_Instantiation_TrailingCommaAndEquality_AreDistinguished`

Asserts that positional and named arguments are captured in order; that a trailing semicolon yields an
instantiation with no children; that a single unbraced child nests; that braced children are flattened into the
instantiation without an extra block node (while a nested bare block is kept as a block child); that a chain of
single children nests rightward; that an unknown module name parses; and that a trailing comma in an argument
list is accepted and that `f(a == 1)` (equality, a positional argument) is distinguished from `f(a = 1)` (a named
argument).

##### CanvasNetScad-ScadLanguage-ScadParser-Modifiers: Modifier Characters

**Tests**: `ScadParserStatementTests.ScadParser_Parse_Modifier_Each_ProducesModifiedStatement`,
`ScadParserStatementTests.ScadParser_Parse_Modifiers_Combined_AccumulateFlags`,
`ScadParserStatementTests.ScadParser_Parse_Modifier_OnForIfLetBlock_ProducesModifiedStatements`,
`ScadParserStatementTests.ScadParser_Parse_Modifier_BeforeAssignment_Throws`

Asserts that each of the four modifier characters wraps an instantiation in a modified statement; that combined
modifiers (`#!`) accumulate both flags on a single node in any order; that modifiers apply to for, if, and let
statements, to blocks, and in a child position; and that a modifier before an assignment, an empty statement, a
module or function definition, the end of input, or an assignment in child position throws
`A modifier must be followed by a module instantiation or block` at the token after the modifiers.

##### CanvasNetScad-ScadLanguage-ScadParser-Conditionals: If And Else

**Tests**: `ScadParserControlFlowTests.ScadParser_Parse_If_WithoutElse_ProducesIfStatement`,
`ScadParserControlFlowTests.ScadParser_Parse_If_Else_ProducesBothBranches`,
`ScadParserControlFlowTests.ScadParser_Parse_If_ElseIfChain_NestsInElseBranch`,
`ScadParserControlFlowTests.ScadParser_Parse_If_DanglingElse_BindsToNearestIf`,
`ScadParserControlFlowTests.ScadParser_Parse_If_EmptyBody_ProducesEmptyBranches`

Asserts that an if without else has a null else branch; that both branches are captured when else is present;
that an else-if chain nests in the else branch; that a dangling else binds to the nearest if; and that empty
bodies produce empty branches while an empty else (`else ;`) is a non-null empty list, distinct from a missing
else, with braced bodies producing multi-statement branches.

##### CanvasNetScad-ScadLanguage-ScadParser-Loops: For And Intersection-For

**Tests**: `ScadParserControlFlowTests.ScadParser_Parse_For_SingleIteratorRange_ProducesForStatement`,
`ScadParserControlFlowTests.ScadParser_Parse_For_MultipleIterators_ProducesAllIterators`,
`ScadParserControlFlowTests.ScadParser_Parse_For_VectorIterator_ProducesForStatement`,
`ScadParserControlFlowTests.ScadParser_Parse_IntersectionFor_ProducesIntersectionForStatement`,
`ScadParserControlFlowTests.ScadParser_Parse_For_CStyleInStatementPosition_Throws`,
`ScadParserControlFlowTests.ScadParser_Parse_For_NoIterators_Throws`

Asserts that a for statement with a range iterator, with several iterators (and a trailing comma), and with a
vector iterator and a braced body parses; that `intersection_for` produces its own statement kind; that the
C-style three-part header in statement position throws `A C-style 'for' loop is only allowed inside a list
comprehension` at the first semicolon; and that a for or intersection-for with no iterators throws
`Expected an identifier` at the offending token.

##### CanvasNetScad-ScadLanguage-ScadParser-LetEchoAssert: Let, Echo, And Assert

**Tests**: `ScadParserControlFlowTests.ScadParser_Parse_LetStatement_ProducesLetStatement`,
`ScadParserControlFlowTests.ScadParser_Parse_EchoStatement_ProducesEchoStatement`,
`ScadParserControlFlowTests.ScadParser_Parse_AssertStatement_ProducesAssertStatement`,
`ScadParserControlFlowTests.ScadParser_Parse_EchoExpression_ProducesEchoExpression`,
`ScadParserControlFlowTests.ScadParser_Parse_AssertExpression_ProducesAssertExpression`,
`ScadParserControlFlowTests.ScadParser_Parse_LetExpression_ProducesLetExpression`,
`ScadParserControlFlowTests.ScadParser_Parse_LetExpression_BodyExtendsRight_ConsumesRemainingExpression`,
`ScadParserControlFlowTests.ScadParser_Parse_EachStatement_ProducesModuleInstantiation`,
`ScadParserControlFlowTests.ScadParser_Parse_EachStatement_ModifiedAndNested_ProducesModuleInstantiation`,
`ScadParserControlFlowTests.ScadParser_Parse_KeywordStatements_WithChildren_ProduceExpectedNodes`,
`ScadParserControlFlowTests.ScadParser_Parse_EachStatement_WithoutParenthesis_Throws`

Asserts that let, echo, and assert parse as statements with and without children (including an empty let and a
named assert argument) and as expressions with a body, and that a let expression body extends as far to the right
as possible: in `1+let(x=2) x*3` the body is `x*3`, and in `let(x=2) x ? 1 : 0` the body includes the
conditional. Also asserts that `each` in statement position is a module instantiation named `each` (with
arguments, a single child, a braced body, after a modifier, and as the child of `translate` or `let`), that let,
echo, and assert statements accept a module child, and that `each` without an argument list throws
`Expected '(' but found 'a'` at the offending token.

##### CanvasNetScad-ScadLanguage-ScadParser-Literals: Literals And Vectors

**Tests**: `ScadParserExpressionTests.ScadParser_Parse_Literals_ProduceLiteralNodes`,
`ScadParserExpressionTests.ScadParser_Parse_Literals_CarryTypedValues`,
`ScadParserExpressionTests.ScadParser_Parse_Vector_TrailingComma_IsAccepted`,
`ScadParserExpressionTests.ScadParser_Parse_Vector_Nested_ProducesNestedVectors`,
`ScadParserExpressionTests.ScadParser_Parse_Vector_EmptyElement_Throws`

Asserts that number, string, `true`, `false`, `undef`, identifier, special-variable, empty-vector, and vector
expressions produce the expected nodes, that the literal nodes carry typed values (a number, a decoded string, a
boolean), that a single trailing comma in a vector is accepted, that vectors nest, and that an empty element
(`[1,,2]` or `[,]`) throws `Expected an expression but found ','` at the offending comma.

##### CanvasNetScad-ScadLanguage-ScadParser-Ranges: Ranges

**Tests**: `ScadParserExpressionTests.ScadParser_Parse_Range_TwoParts_ProducesRangeWithoutStep`,
`ScadParserExpressionTests.ScadParser_Parse_Range_ThreeParts_ProducesRangeWithStepInWrittenOrder`,
`ScadParserExpressionTests.ScadParser_Parse_Range_ExpressionBounds_ProducesBoundExpressions`,
`ScadParserExpressionTests.ScadParser_Parse_Range_TernaryStart_ConsumesTernaryColonFirst`,
`ScadParserExpressionTests.ScadParser_Parse_Range_WithComma_Throws`,
`ScadParserExpressionTests.ScadParser_Parse_Range_FourParts_Throws`,
`ScadParserExpressionTests.ScadParser_Parse_Range_ComprehensionStart_Throws`,
`ScadParserExpressionTests.ScadParser_Parse_Range_ComprehensionFormStart_Throws`

Asserts that a two-part range has no step, that a three-part range keeps the step in written order (start, step,
end, including a negative step), that bounds may be arbitrary expressions, that a conditional start consumes its
own colon before the range colon (`[a?1:2 : 9]`), that a comma in a range throws `Expected ']' but found ','`,
that a four-part range throws `Expected ']' but found ':'`, and that a range whose start begins a comprehension
(`for`, `each`, `if`, `let`, or a C-style `for`) throws `Expected ',' or ']' but found ':'` at the colon.

##### CanvasNetScad-ScadLanguage-ScadParser-Precedence: Operator Precedence And Associativity

**Tests**: `ScadParserPrecedenceTests.ScadParser_Parse_Precedence_BinaryLevels_GroupAsTable`,
`ScadParserPrecedenceTests.ScadParser_Parse_Associativity_LeftAssociativeOperators_GroupLeft`,
`ScadParserPrecedenceTests.ScadParser_Parse_Power_IsRightAssociative`,
`ScadParserPrecedenceTests.ScadParser_Parse_Power_BindsTighterThanUnaryMinus`,
`ScadParserPrecedenceTests.ScadParser_Parse_Power_AllowsUnaryExponent`,
`ScadParserPrecedenceTests.ScadParser_Parse_Ternary_IsRightAssociative`,
`ScadParserPrecedenceTests.ScadParser_Parse_Ternary_ConditionIsLogicalOr`,
`ScadParserPrecedenceTests.ScadParser_Parse_Unary_Chains_NestRightward`,
`ScadParserPrecedenceTests.ScadParser_Parse_Parentheses_OverridePrecedence`,
`ScadParserPrecedenceTests.ScadParser_Parse_Unary_MinusLiteral_IsNotConstantFolded`,
`ScadParserPrecedenceTests.ScadParser_Parse_Binary_EveryOperator_MapsToOperatorKind`

Asserts, through the dump of each expression, that the binary precedence levels group as documented
(multiplicative over additive, relational over equality, equality over logical and, logical and over logical
or, and the conditional lowest); that all left-associative operators group from the left; that the exponent is
right-associative, binds tighter than unary minus and not (`-2^2` is the negation of `2^2`), and accepts a
prefix operator in its exponent; that the conditional is right-associative with a logical-or condition; that
prefix operator chains nest rightward; that parentheses override precedence and leave no node of their own; that
`-5` is a negation of the literal 5 and is never folded into a negative literal; and that each of the fourteen
binary operator tokens maps to its operator kind.

##### CanvasNetScad-ScadLanguage-ScadParser-Postfix: Index, Member, And Call

**Tests**: `ScadParserExpressionTests.ScadParser_Parse_Index_ProducesIndexExpression`,
`ScadParserExpressionTests.ScadParser_Parse_Member_XYZAndGenericIdentifier_ProducesMemberExpression`,
`ScadParserExpressionTests.ScadParser_Parse_Call_OnIdentifier_ProducesCallExpression`,
`ScadParserExpressionTests.ScadParser_Parse_Call_OnParenthesizedExpression_ProducesCallExpression`,
`ScadParserExpressionTests.ScadParser_Parse_Call_OnFunctionLiteral_ProducesCallExpression`,
`ScadParserExpressionTests.ScadParser_Parse_PostfixChain_LeftToRight_NestsLeftward`,
`ScadParserExpressionTests.ScadParser_Parse_Call_ResultIndexedAndMember_ProducesPostfixChain`

Asserts index expressions on identifiers, expressions, and vector literals; member access with `x`, `y`, `z`,
and any other identifier; calls on an identifier with positional and named arguments, no arguments, and a
trailing comma; calls on a parenthesized expression (including a conditional) and on a parenthesized function
literal; that postfix operations chain left to right, nesting leftward (`a[0][1].x` and `f(1)(2)[3].x`); and that
a call result can be indexed and member-accessed.

##### CanvasNetScad-ScadLanguage-ScadParser-FunctionLiterals: Function Literals

**Tests**: `ScadParserExpressionTests.ScadParser_Parse_FunctionLiteral_ProducesFunctionLiteral`,
`ScadParserExpressionTests.ScadParser_Parse_FunctionLiteral_BodyExtendsRight_ConsumesOperators`,
`ScadParserExpressionTests.ScadParser_Parse_FunctionLiteral_AsArgument_StopsAtComma`,
`ScadParserExpressionTests.ScadParser_Parse_FunctionLiteral_Nested_ProducesCurriedLiteral`

Asserts function literals with parameters, defaulted parameters, and no parameters; that the body extends as far
to the right as possible, consuming binary operators and a conditional; that a literal used as an argument ends
at the next comma; and that a literal returning another literal nests (`function(a) function(b) a+b`).

##### CanvasNetScad-ScadLanguage-ScadParser-Comprehensions: List Comprehensions

**Tests**: `ScadParserComprehensionTests.ScadParser_Parse_Comprehension_For_ProducesListFor`,
`ScadParserComprehensionTests.ScadParser_Parse_Comprehension_MultipleIterators_ProducesAllIterators`,
`ScadParserComprehensionTests.ScadParser_Parse_Comprehension_CStyleFor_ProducesListCFor`,
`ScadParserComprehensionTests.ScadParser_Parse_Comprehension_IfElse_ProducesListIf`,
`ScadParserComprehensionTests.ScadParser_Parse_Comprehension_Let_ProducesListLet`,
`ScadParserComprehensionTests.ScadParser_Parse_Comprehension_Each_ProducesListEach`,
`ScadParserComprehensionTests.ScadParser_Parse_Comprehension_Nested_ProducesNestedElements`,
`ScadParserComprehensionTests.ScadParser_Parse_Comprehension_MixedWithPlainElements_KeepsSourceOrder`,
`ScadParserComprehensionTests.ScadParser_Parse_Comprehension_Parenthesized_MatchesUnparenthesized`,
`ScadParserComprehensionTests.ScadParser_Parse_Comprehension_ParenthesizedInBodies_Parses`,
`ScadParserComprehensionTests.ScadParser_Parse_Comprehension_ParenthesizedExpression_StillParsesAsExpression`,
`ScadParserComprehensionTests.ScadParser_Parse_Comprehension_ParenthesizedMisuse_Throws`,
`ScadParserComprehensionTests.ScadParser_Parse_Comprehension_ParenthesizedUnclosed_Throws`,
`ScadParserComprehensionTests.ScadParser_Parse_Comprehension_CStyleWithoutCondition_Throws`,
`ScadParserComprehensionTests.ScadParser_Parse_Comprehension_OutsideVector_Throws`,
`ScadParserComprehensionTests.ScadParser_Parse_Comprehension_EmptyHeader_Throws`

Asserts that a for comprehension with one or several iterators, the C-style form (including empty initializer
and increment lists and several of each), if with and without else (with a dangling else binding to the nearest
if), let, and each all produce the expected list-element nodes; that comprehensions nest directly and through a
nested vector; that they mix with plain elements in source order and may have a conditional as their body; that
a C-style header without a condition throws `Expected an expression but found ';'`; that a comprehension keyword
outside a vector throws `Expected an expression but found 'for'` (or `'each'`); and that an empty header throws
`Expected an identifier but found ')'`.

Parenthesized elements are verified by asserting that `(for …)`, `(if … else …)`, `(each …)`, and `(let …)`
produce exactly the dump of the unparenthesized element in a vector and in the bodies of `for`, C-style `for`,
`if`, `else`, and `let` (including nested parentheses); that plain parenthesized expressions (`[(1+2)]`,
`[(1+2)*3]`, `[for (i=[0:1]) (i+1)]`) and a parenthesized `let` used as an operand (`[(let(a=1) [a])[0]]`) still
parse as expressions; that a parenthesized comprehension cannot start a range or appear outside a vector; and
that an unclosed parenthesis throws `Expected ')' but found ']'`.

##### CanvasNetScad-ScadLanguage-ScadParser-NodeLocations: Node Locations

**Tests**: `ScadParserNodeTests.ScadParser_Parse_Locations_Statements_PointAtIdentifyingToken`,
`ScadParserNodeTests.ScadParser_Parse_Locations_Expressions_PointAtIdentifyingToken`,
`ScadParserNodeTests.ScadParser_Parse_Locations_Expressions_BracketAndKeywordForms`,
`ScadParserNodeTests.ScadParser_Parse_Locations_Parts_PointAtFirstToken`,
`ScadParserNodeTests.ScadParser_Parse_Locations_FileName_Propagated`

Asserts the location of each node kind against the identifying-token rule: a statement at its first modifier,
keyword, or name (and the wrapped statement of a modified statement at its own token); a binary expression at its
operator, a unary expression at its operator, a member access at its dot, an index at its opening bracket, and
a call at its opening parenthesis; vectors, ranges, function literals, and let, echo, and assert expressions at
their opening token or keyword, a conditional at its question mark, and a power expression at its caret; parts
(parameters, arguments, and iterator assignments) at their first token; and that the supplied file name appears
in the location of the program and of every statement and expression.

##### CanvasNetScad-ScadLanguage-ScadParser-Immutability: Immutable Syntax Tree

**Tests**: `ScadParserNodeTests.ScadParser_Parse_Result_Collections_AreReadOnly`,
`ScadParserNodeTests.ScadParser_Parse_Result_Nodes_AreImmutableRecords`

Asserts that the statement, parameter, body, argument, child, and vector-element collections are read-only
through their mutable interface and that mutation throws `NotSupportedException`; and, by reflection over every
node type of the assembly (more than forty), that each is a record and that no property has a public setter
other than an init accessor.

##### CanvasNetScad-ScadLanguage-ScadParser-Directives: Include And Use At File Scope

**Tests**: `ScadParserStatementTests.ScadParser_Parse_Directive_FileScope_RecordedInOrder`,
`ScadParserStatementTests.ScadParser_Parse_Directive_InsideBlockOrModule_Throws`,
`ScadParserStatementTests.ScadParser_Parse_Directive_AsSingleChild_Throws`

Asserts that file-scope directive tokens become directive statements in source order with their kind and path
(a stray semicolon after a directive is an empty statement); that a directive inside a bare block, a module body,
or an if body throws `include/use is only allowed at file scope` at the directive; and that a directive in
an unbraced child position throws `Expected ';', '{' or a module instantiation` at the directive.

##### CanvasNetScad-ScadLanguage-ScadParser-SyntaxErrors: Grammatical Errors

**Tests**: `ScadParserErrorTests.ScadParser_Parse_MissingSemicolon_ReportsNextToken`,
`ScadParserErrorTests.ScadParser_Parse_UnclosedBrace_ReportsEndOfInput`,
`ScadParserErrorTests.ScadParser_Parse_UnclosedParenthesis_ReportsExpectedParenthesis`,
`ScadParserErrorTests.ScadParser_Parse_UnclosedBracket_ReportsExpectedBracket`,
`ScadParserErrorTests.ScadParser_Parse_UnexpectedClosingBrace_ReportsToken`,
`ScadParserErrorTests.ScadParser_Parse_ExpressionExpected_ReportsToken`,
`ScadParserErrorTests.ScadParser_Parse_StatementStart_InvalidToken_ReportsUnexpectedToken`,
`ScadParserErrorTests.ScadParser_Parse_IdentifierWithoutAssignOrCall_ReportsFollowingToken`,
`ScadParserErrorTests.ScadParser_Parse_AssignmentAsSingleChild_Throws`,
`ScadParserErrorTests.ScadParser_Parse_DefinitionAsSingleChild_Throws`,
`ScadParserErrorTests.ScadParser_Parse_ChildMissing_ReportsExpectedChild`,
`ScadParserErrorTests.ScadParser_Parse_FunctionKeywordWithoutName_AtStatementStart_Throws`,
`ScadParserErrorTests.ScadParser_Parse_MultipleErrors_ReportsFirstOnly`,
`ScadParserErrorTests.ScadParser_Parse_ErrorMessage_ContainsFileLineColumn`,
`ScadParserErrorTests.ScadParser_Parse_MalformedConstructs_ReportExpectedLocations`,
`ScadParserErrorTests.ScadParser_Parse_NullSource_ThrowsArgumentNullException`,
`ScadParserErrorTests.ScadParser_Parse_EmptySource_ProducesEmptyProgram`

Asserts the exact location and message of each grammatical error: a missing semicolon is reported at the next
token (`Expected ';' but found 'b'`); an unclosed brace, parenthesis, or bracket at end of input is reported as
`Unexpected end of input; expected '}'` (and the `')'` and `']'` equivalents) and a mismatched one as
`Expected ')' but found '2'`; a stray closing brace as `Unexpected token '}'`; a missing expression as
`Expected an expression but found ';'` or, at end of input, `Unexpected end of input; expected an expression`;
tokens that cannot start a statement (a number, `else`, a string, closing delimiters, `=`, `true`) as
`Unexpected token ...`; an identifier followed by neither `=` nor `(` as `Expected '=' or '(' after 'foo'`; an
assignment or definition as an unbraced child as `Use braces around a declaration`; a missing child in its
several forms; and a `function` keyword without a name. Also asserts that only the first of several errors is
reported (the message names no later line), that the message begins with `file:line:column:` and the exception
exposes the same location, a set of assorted malformed constructs (a nameless module, `if` and `echo` without a
parenthesis, a member access without a name, a number followed by letters, a non-name before `=` in an argument
list, a function literal without a parameter list, a for iterator without `=`, a function definition without
`=`, and a conditional without a colon), that a null source or null token list throws `ArgumentNullException`,
and that a whitespace-and-comment-only source is an empty program located at the start of the file.

##### CanvasNetScad-ScadLanguage-ScadParser-NestingLimit: Nesting Depth And Tree Height

**Tests**: `ScadParserLimitTests.ScadParser_Parse_Nesting_Parentheses_AtLimit_Succeeds`,
`ScadParserLimitTests.ScadParser_Parse_Nesting_Parentheses_OverLimit_Throws`,
`ScadParserLimitTests.ScadParser_Parse_Nesting_Vectors_OverLimit_Throws`,
`ScadParserLimitTests.ScadParser_Parse_Nesting_UnaryChain_OverLimit_Throws`,
`ScadParserLimitTests.ScadParser_Parse_Nesting_TernaryChain_OverLimit_Throws`,
`ScadParserLimitTests.ScadParser_Parse_Nesting_PowerChain_OverLimit_Throws`,
`ScadParserLimitTests.ScadParser_Parse_Nesting_BlockStatements_OverLimit_Throws`,
`ScadParserLimitTests.ScadParser_Parse_Nesting_SingleChildChain_OverLimit_Throws`,
`ScadParserLimitTests.ScadParser_Parse_Nesting_ElseIfChain_OverLimit_Throws`,
`ScadParserLimitTests.ScadParser_Parse_Nesting_ComprehensionChain_OverLimit_Throws`,
`ScadParserLimitTests.ScadParser_Parse_Nesting_LeftAssociativeChain_OverLimit_Throws`,
`ScadParserLimitTests.ScadParser_Parse_Nesting_LeftAssociativeChain_UnderLimit_Succeeds`,
`ScadParserLimitTests.ScadParser_Parse_Nesting_PostfixChain_OverLimit_Throws`,
`ScadParserLimitTests.ScadParser_Parse_Nesting_InteriorLeftChainsAcrossLevels_OverLimit_Throws`,
`ScadParserLimitTests.ScadParser_Parse_Nesting_MultiLineInput_ReportsLineOfOffendingConstruct`,
`ScadParserLimitTests.ScadParser_Parse_Nesting_OnSmallStackThread_DoesNotOverflow`,
`ScadParserLimitTests.ScadParser_Parse_Nesting_AtLimit_OnSmallCallerStack_Succeeds`,
`ScadParserLimitTests.ScadParser_Parse_Nesting_OverLimit_OnSmallCallerStack_ThrowsNestingError`,
`ScadParserLimitTests.ScadParser_Parse_Nesting_AtLimit_OnTinyCallerStack_Succeeds`,
`ScadParserLimitTests.ScadParser_Parse_NormalInput_RunsOnCallerThread`,
`ScadParserLimitTests.ScadParser_Parse_AtLimit_OnTinyCallerStack_FallsBackToDedicatedThread`,
`ScadParserLimitTests.ScadParser_Parse_OnTinyCallerStack_PreservesExceptionsAndCulture`,
`ScadParserLimitTests.ScadParser_Parse_ConcurrentTinyCallerStacks_BoundFallbackThreads`,
`ScadParserLimitTests.ScadParser_Parse_ThreadStartOutOfMemory_ThrowsSyntaxExceptionAndReleasesSlot`,
`ScadParserLimitTests.ScadParser_Parse_Exception_RethrownFromParserThread_PreservesTypeAndStackTrace`,
`ScadParserLimitTests.ScadParser_Parse_NumberLiteral_UnderGermanCulture_ParsesDotDecimal`,
`ScadParserLimitTests.ScadParser_Parse_Concurrent_ProducesIndependentResults`

Asserts the limit of 256 levels with the documented accounting (a file-scope assignment already uses two
levels): parentheses nested 254 deep are accepted and 255 deep are rejected with `Nesting too deep (limit 256)`
at a location on the first line; vector, prefix-operator (including 100,000 operators), conditional (both
nested forms), power, block, single-child, else-if, and comprehension chains are accepted at a moderate depth
and rejected when far beyond the limit; a left-associative chain of 255 operands (exactly the limit) is accepted
and chains of 256 and of 100,000 operands are rejected, while a 200-operand chain is accepted; long index,
member, and call postfix chains are rejected; left-leaning chains stacked through parentheses are rejected by the
verified tree height although the parser's own recursion stays shallow; and an input whose deep chain starts on
line 3 reports line 3. A thread-start failure injected through the per-thread
`ScadParser.ThreadStarterForCurrentThread` seam (an
`OutOfMemoryException`, as in a 32-bit process) is shown to surface as a `ScadSyntaxException` located on line 1,
repeatedly (more times than there are fallback slots, so a leaked slot would block), after which the same input
parses normally. Hostile inputs of seven shapes, 100,000 levels deep, are parsed on a thread with a 1 MiB
stack and each produces the typed syntax exception rather than a stack overflow. Determinism independent of the
caller's stack is proved by two theories over eleven pathological shapes (parentheses, vectors, prefix
operators, ternaries, exponent chains, comprehension chains, function literals, call arguments, blocks,
child chains, and else-if chains), each run from a caller thread with only a 256 KiB stack: the input nested
exactly at the limit parses, and the input one level beyond it throws `Nesting too deep (limit 256)` rather
than overflowing. A further test parses parentheses at the limit from a caller with a 64 KiB stack. A normal,
moderately nested parse is shown to complete on the calling thread for both entry points (no fallback thread),
while the same parse with the fallback forced through the `ScadParser.ForceFallbackForCurrentThread` seam completes
on a different (fallback) thread with the correct tree and counts the fallback. The
fallback is shown to marshal exceptions faithfully (a `ScadSyntaxException` keeps its type, location, and
parser stack frames, and the over-limit input still reports `Nesting too deep (limit 256)`), to apply the
caller's culture (a `de-DE` caller still reads `1.5` as 1.5), and to be bounded and safe for concurrent
callers: four times as many simultaneous forced callers as the bound all succeed while the observed peak number
of concurrent fallback threads stays within `MaxConcurrentFallbackThreads`; 16 parallel ordinary parses yield
independent results.

##### CanvasNetScad-ScadLanguage-ScadParser-SourceLength: Source Length Limit

**Tests**: `ScadParserLimitTests.ScadParser_Parse_SourceLength_AtLimit_Parses`,
`ScadParserLimitTests.ScadParser_Parse_SourceLength_OverLimit_ThrowsInvalidDataExceptionNotSyntaxException`

Asserts that a valid statement padded with blanks to exactly 4,194,304 characters parses to a single statement,
and that the same source one character longer throws exactly `InvalidDataException`, not a
`ScadSyntaxException`, with a message that names the limit.
