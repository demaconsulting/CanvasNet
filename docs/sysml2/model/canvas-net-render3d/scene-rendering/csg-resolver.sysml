package CanvasNet {
    part def CsgResolver {
        doc /* The internal fragment-list constructive-solid-geometry evaluator: the iterative, stack-safe CsgCompiler that collapses union-only subtrees into signed-counter leaves and compiles difference and intersection nodes into an immutable postfix CsgProgram (Or, And, AndNot over a leaf-state bitset of any size; a union-only scene compiles to no program, selecting the unchanged z-buffer fast path), and the per-worker CsgResolver that sorts each sample's fragments by a total order, tracks per-leaf crossing counters, applies anchored (and slope-aware, per-instance) depth-tie groups atomically, re-evaluates the program only when a leaf changes, and selects the first visible root transition - its fragment, enter or exit shading, and priority/triangle-index tie breaking - writing depth, color, and meta into the tile workspace. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Render3D/SceneRendering/CsgProgram.cs, src/DemaConsulting.CanvasNet.Render3D/SceneRendering/CsgCompiler.cs, src/DemaConsulting.CanvasNet.Render3D/SceneRendering/CsgResolver.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Render3D.Tests/SceneRendering/CsgProgramTests.cs, test/DemaConsulting.CanvasNet.Render3D.Tests/SceneRendering/CsgResolverTests.cs */
        comment designRef /* Design: docs/design/canvas-net-render3d/scene-rendering/csg-resolver.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-render3d/scene-rendering/csg-resolver.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-render3d/scene-rendering/csg-resolver.yaml */
    }
}
