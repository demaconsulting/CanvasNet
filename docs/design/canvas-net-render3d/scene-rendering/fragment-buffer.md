### FragmentBuffer

<!-- cspell:ignore rasterizer rasterization rasterized prefix -->

The `FragmentBuffer` unit is a software unit in the `SceneRendering` subsystem. It is an
internal, per-worker, reusable store for the fragments of one tile on the fragment-list path (constructive
solid geometry, translucent surfaces and overlays):

- `FragmentBuffer` (sealed class): the two-pass collection protocol, the budget check, and the
  per-sample access
- `FragmentBuffer.Fragment` (readonly record struct): one fragment, the depth at which a triangle
  covers a sample and the triangle's index
- two private nested visitor structs (`CountVisitor`, `FillVisitor`) implementing
  `IFragmentVisitor`

All types are internal to the assembly (visible to the test assembly through
`InternalsVisibleTo`).

#### Purpose

A depth test keeps only the nearest fragment per sample, but difference and intersection (and
translucent surfaces and overlays, which composite several layers) need
**every** surface a sample's ray crosses. `FragmentBuffer` gathers all of them for one tile into
contiguous, exact-sized storage without any per-sample allocation, and refuses to grow beyond a
configured budget so memory stays bounded and a too-complex scene fails in a repeatable way.

#### Data Model

- `_counts` (`int[]`, one per sample): the number of fragments of each sample (count pass)
- `_offsets` (`int[]`, samples + 1): the start of each sample's run (prefix sums of the counts)
- `_cursor` (`int[]`, one per sample): the next write position per sample (fill pass)
- `_fragments` (`Fragment[]`): all fragments, one contiguous run per sample; the array may be
  longer than the tile needs because it is reused
- `MaxFragments` (the budget), `FragmentCount` (the count so far), `Capacity`, `SampleCount`

The arrays only ever grow, to exactly the size a tile needs, and are reused by the next tile.

#### Key Methods

- **`Begin(originX, originY, width, height)`** starts a tile: records the sample rectangle, grows
  the per-sample arrays if needed, and clears the counts.
- **`CountTriangle(triangle)`** rasterizes the triangle restricted to the tile with a
  `CountVisitor` that increments the covered samples' counts, adds the hits to `FragmentCount`, and
  then checks the budget.
- **`EndCounting()`** prefix-sums the counts into `_offsets`, initializes `_cursor`, and grows
  `_fragments` to exactly `FragmentCount` entries when the current array is smaller.
- **`AddTriangle(triangleIndex, triangle)`** rasterizes the same triangle again with a `FillVisitor`
  that writes `(depth, triangleIndex)` at the sample's cursor. Rasterization is deterministic, so
  the fill pass sees exactly the hits of the count pass when the same triangles are added.
- **`GetSample(sampleIndex)`** returns the sample's run as a `Span<Fragment>` (the resolver sorts it
  in place). It verifies that the sample's cursor reached the end of its run, which detects a fill
  pass that did not match the count pass.

**Budget.** `FragmentCount` is checked against `MaxFragments` after every counted triangle and
`RenderBudgetExceededException` is thrown (with the standard, tile-independent message and the
budget) as soon as it is exceeded. The count only grows during the pass, so a tile exceeds the
budget if and only if its complete triangle set does: the outcome depends on neither the order of
the triangles, nor which tile it is, nor which worker handled it. The budget must be between 1 and
`Array.MaxLength` because the storage is array-backed. Fragments beyond the far distance are
counted and stored (the resolver needs them to infer the ray's starting state).

#### Error Handling

- A budget outside 1 to `Array.MaxLength` throws `ArgumentOutOfRangeException` from the
  constructor.
- Exceeding the budget throws `RenderBudgetExceededException`; the buffer returns to its idle state
  and can be started again with `Begin`.
- Calling a method in the wrong phase (counting before `Begin`, filling before `EndCounting`,
  counting after `EndCounting`) or reading a sample that was not completely filled throws
  `InvalidOperationException`.

#### Dependencies

The `TriangleRasterizer` unit (`TriangleRasterizer.Rasterize`, `IFragmentVisitor`, `ScreenTriangle`,
`TileRect`) and the public `RenderBudgetExceededException` of the `SceneRenderer` unit. No other
dependencies beyond the .NET base class library.

#### Callers

`SceneRenderer` (per tile on the CSG path: `Begin`, `CountTriangle` per binned triangle,
`EndCounting`, `AddTriangle` per binned triangle) and `CsgResolver` (through `GetSample`). The
buffer is not thread-safe; each worker owns one.
