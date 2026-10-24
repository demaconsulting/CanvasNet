package CanvasNet {
    part def ScadEvaluation {
        doc /* The internal evaluation subsystem in the DemaConsulting.CanvasNet.Scad.Evaluation namespace that runs a parsed OpenSCAD-language program: the ScadFileResolver first resolves include and use directives on the calling thread (inside a sandbox confined to the base directory and library paths, or to the logical paths handed to the caller's resolver), then the ScadEvaluator evaluates purely in memory over the resolved, immutable units - lazy last-assignment-wins scoping, dynamically scoped special variables, functions, closures, tail calls, modules with children, control flow, comprehensions - using the ScadValue value model and the ScadBuiltinFunctions library, and produces a geometry-agnostic instantiation tree plus buffered echo lines, warnings, and unsupported-feature reports. The recursion limit is counted deterministically and the pass restarts on a bounded, dedicated large-stack thread only when the caller's stack is too small; every resource (recursion, steps, loop iterations, list and string length, instances, output, include depth and count, diagnostics) is bounded by ScadLimits and cancellation is honored. The subsystem is internal; the public entry point is ScadDocument.Evaluate. As of this release, its constituent units are ScadValue, ScadEvaluator, ScadBuiltinFunctions, and ScadFileResolver. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Scad.Tests/Evaluation/ScadEvaluatorEntryPointTests.cs, test/DemaConsulting.CanvasNet.Scad.Tests/Evaluation/ScadEvaluatorFixtureTests.cs, test/DemaConsulting.CanvasNet.Scad.Tests/Evaluation/ScadEvaluatorIncludeUseTests.cs, test/DemaConsulting.CanvasNet.Scad.Tests/Evaluation/ScadEvaluatorDiagnosticsTests.cs, test/DemaConsulting.CanvasNet.Scad.Tests/Evaluation/ScadEvaluatorLimitTests.cs, test/DemaConsulting.CanvasNet.Scad.Tests/Evaluation/ScadEvaluatorStackTests.cs, test/DemaConsulting.CanvasNet.Scad.Tests/Evaluation/ScadEvaluatorCancellationTests.cs, test/DemaConsulting.CanvasNet.Scad.Tests/Evaluation/ScadEvaluatorDeterminismTests.cs */
        comment designRef /* Design: docs/design/canvas-net-scad/scad-evaluation.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-scad/scad-evaluation.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-scad/scad-evaluation.yaml */

        part scadValue : ScadValue;
        part scadEvaluator : ScadEvaluator;
        part scadBuiltinFunctions : ScadBuiltinFunctions;
        part scadFileResolver : ScadFileResolver;
    }
}
