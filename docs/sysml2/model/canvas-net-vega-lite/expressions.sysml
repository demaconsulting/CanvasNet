package CanvasNetVegaLite_VegaLiteExpressionsSubsystem {
    private import SoftwareItemMetadata::*;
    public import CanvasNetVegaLite_ExpressionParser::*;
    public import CanvasNetVegaLite_ExpressionEvaluator::*;
    public import CanvasNetVegaLite_ExpressionFunctions::*;

    part def VegaLiteExpressions {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.VegaLite.Expressions namespace that is the expression engine of the renderer: a closed world with no reflection, eval, assignment, new, function expression, file or network access, clock, or random source. The ExpressionParser unit compiles the text of a Vega expression in one bounded pass into an immutable CompiledExpression (a postfix program with forward jumps, so that neither compiling nor evaluating recurses in proportion to the text), under the MaxExpressionLength and MaxExpressionDepth limits, with located VegaLiteSpecExceptions that name the offset inside the text, and collects the constructs it parses but does not execute (functions outside the whitelist, view state, and the deferred bitwise, shift, and in operators) and the references to parameters; the ExpressionEvaluator unit interprets a compiled expression against a datum (a table row, a calculated object, or a caller-defined datum) and a dictionary of parameter values with JavaScript coercion, culture-independently, under one shared MaxEvaluationSteps budget with cancellation checks, failing with located VegaLiteRenderExceptions; the ExpressionFunctions unit is the closed table of the pure functions (math, text and array, type and conversion, and regular expression functions, with a bounded non-backtracking regex translation and a charging context, and, from Phase 8, the date and time functions in the time zone of the chart: the constructors and parts, the week and day-of-year numbers, the calendar offset and sequence, the d3 time format and parse functions, and the time unit specifier; the ExpressionEvaluator carries the TimeCalendar to them) and the d3 number format function format (ExpressionFunctions.Number.cs, 100 functions in all, on the NumberFormat unit of the Scales subsystem: a namespace dependency of Expressions on Scales next to the existing one of Scales on Expressions; the selection functions and, in Phase 14, the nine probability distribution functions of ExpressionFunctions.Distributions.cs bring the table to 113 functions). Color, geographic, scale, and data-flow functions, the clock function now, the sampling functions (random, sampleNormal, sampleLogNormal, and sampleUniform: an expression is a pure deterministic function), signal, event, and item() view state are deferred and are reported by the FeatureCatalog. The conditions of the encoding channels are evaluated by the MarkBuilder with this engine. The subsystem is internal; compiled expressions are immutable and shared by every call, and an evaluator belongs to one call on one thread. As of this release, its constituent units are ExpressionParser, ExpressionEvaluator, and ExpressionFunctions. */

        metadata info : SoftwareInfo {
            testFiles = (
                "test/DemaConsulting.CanvasNet.VegaLite.Tests/Expressions/",
                "test/DemaConsulting.CanvasNet.VegaLite.Tests/Expressions/ExpressionParserTests.cs",
                "test/DemaConsulting.CanvasNet.VegaLite.Tests/Expressions/ExpressionEvaluatorTests.cs",
                "test/DemaConsulting.CanvasNet.VegaLite.Tests/Expressions/ExpressionFunctionsTests.cs",
                "test/DemaConsulting.CanvasNet.VegaLite.Tests/Expressions/ExpressionFunctionsTimeTests.cs",
                "test/DemaConsulting.CanvasNet.VegaLite.Tests/Expressions/ExpressionTestSupport.cs"
            );
            designFiles = ("docs/design/canvas-net-vega-lite/expressions.md");
            verificationFiles = ("docs/verification/canvas-net-vega-lite/expressions.md");
            requirementFiles = ("docs/reqstream/canvas-net-vega-lite/expressions.yaml");
        }

        part expressionParser : ExpressionParser;
        part expressionEvaluator : ExpressionEvaluator;
        part expressionFunctions : ExpressionFunctions;
    }
}
