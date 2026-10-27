package CanvasNet {
    part def CsgResolver {
        doc /* The internal fragment-list constructive-solid-geometry evaluator: the iterative, stack-safe CsgCompiler that collapses union-only subtrees into signed-counter leaves compiles difference and intersection nodes into an immutable postfix CsgProgram, and gives every OverlayNode body a program of its own (an overlay is false in the main program) (Or, And, AndNot over a leaf-state bitset of any size; an opaque union-only scene without overlay compiles to no program, selecting the unchanged z-buffer fast path), the integer front-to-back LayerCompositor, and the per-worker CsgResolver that sorts each sample's fragments by a total order, tracks per-leaf crossing counters, applies anchored (and slope-aware, per-instance) depth-tie groups atomically, re-evaluates the program only when a leaf changes, and turns every result transition of the main and overlay programs into a surface layer - its fragment, enter or exit shading, and priority/triangle-index tie breaking, overlay layers first within a tie group - accumulating the layers until an opaque one and compositing the remainder over the background, writing the composite color with the first layer depth and meta into the tile workspace. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Render3D/SceneRendering/CsgProgram.cs, src/DemaConsulting.CanvasNet.Render3D/SceneRendering/CsgCompiler.cs, src/DemaConsulting.CanvasNet.Render3D/SceneRendering/CsgResolver.cs, src/DemaConsulting.CanvasNet.Render3D/SceneRendering/LayerCompositor.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Render3D.Tests/SceneRendering/CsgProgramTests.cs, test/DemaConsulting.CanvasNet.Render3D.Tests/SceneRendering/CsgResolverTests.cs */
        comment designRef /* Design: docs/design/canvas-net-render3d/scene-rendering/csg-resolver.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-render3d/scene-rendering/csg-resolver.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-render3d/scene-rendering/csg-resolver.yaml */
    }
}
