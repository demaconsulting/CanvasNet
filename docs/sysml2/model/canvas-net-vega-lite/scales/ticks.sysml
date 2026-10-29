package CanvasNet {
    part def Ticks {
        doc /* The internal static Ticks, a port with attribution of the d3-array tick algorithms and the d3-scale linear nice method plus the tick-count rules of Vega and Vega-Lite: GetTicks, TickIncrement (d3's signed encoding, in which a negative value stands for the reciprocal of the step, which keeps the arithmetic exact), and TickStep choose evenly spaced ticks that are 1, 2, or 5 times a power of ten with the thresholds sqrt(50), sqrt(10), and sqrt(2); Nice extends a domain to nice boundaries (at most ten rounds until the step settles, direction kept, unchanged when it does not settle); DefaultAxisTickCount is Vega-Lite's one tick per 40 pixels, DefaultAxisTickMinStep is 1 for the integer format d, ResolveTickCount is Vega's tickCount that lowers the count until the step reaches the minimum step, and AxisTicks combines them. A result of more than MaxTicks values is a VegaLiteRenderException thrown before the array is allocated; inputs that are not numbers give an empty array or an unchanged domain. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.VegaLite/Scales/Ticks.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.VegaLite.Tests/Scales/TicksTests.cs */
        comment designRef /* Design: docs/design/canvas-net-vega-lite/scales/ticks.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-vega-lite/scales/ticks.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-vega-lite/scales/ticks.yaml */
    }
}
