package CanvasNet {
    part def VegaLiteExpressions {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.VegaLite.Expressions namespace that is the expression engine of the renderer: a closed world with no reflection, eval, assignment, new, function expression, file or network access, clock, or random source. The ExpressionParser unit compiles the text of a Vega expression in one bounded pass into an immutable CompiledExpression (a postfix program with forward jumps, so that neither compiling nor evaluating recurses in proportion to the text), under the MaxExpressionLength and MaxExpressionDepth limits, with located VegaLiteSpecExceptions that name the offset inside the text, and collects the constructs it parses but does not execute (functions outside the whitelist, view state, and the deferred bitwise, shift, and in operators) and the references to parameters; the ExpressionEvaluator unit interprets a compiled expression against a datum (a table row, a calculated object, or a caller-defined datum) and a dictionary of parameter values with JavaScript coercion, culture-independently, under one shared MaxEvaluationSteps budget with cancellation checks, failing with located VegaLiteRenderExceptions; the ExpressionFunctions unit is the closed table of 57 pure functions (math, text and array, type and conversion, and regular expression functions, with a bounded non-backtracking regex translation and a charging context). Date, time, format, color, geographic, scale, and statistical functions, signal, event, and item() view state are deferred and are reported by the FeatureCatalog. The conditions of the encoding channels are evaluated by the MarkBuilder with this engine. The subsystem is internal; compiled expressions are immutable and shared by every call, and an evaluator belongs to one call on one thread. As of this release, its constituent units are ExpressionParser, ExpressionEvaluator, and ExpressionFunctions. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.VegaLite.Tests/Expressions/ (the unit test classes ExpressionParserTests, ExpressionEvaluatorTests, and ExpressionFunctionsTests, and the shared helper ExpressionTestSupport) */
        comment designRef /* Design: docs/design/canvas-net-vega-lite/expressions.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-vega-lite/expressions.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-vega-lite/expressions.yaml */

        part expressionParser : ExpressionParser;
        part expressionEvaluator : ExpressionEvaluator;
        part expressionFunctions : ExpressionFunctions;
    }
}
