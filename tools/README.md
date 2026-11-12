# Developer Tools

`tools/` is a **separate tree of support tools that are not software items**. The `docs/reqstream/`, `docs/design/`,
`docs/verification/`, `src/`, and `test/` trees follow the software-item naming convention (system, subsystem, and
unit); `tools/` is outside that convention. Its contents have no requirements, design, or verification artifacts, are
not shipped, and are not run by CI: a developer or AI agent runs them by hand. The tests that CI runs live in `test/`.
Run every command from the repository root.

## DemaConsulting.CanvasNet.VegaLite.Benchmarks

**For** profiling and performance work. A Stopwatch console app that times representative heavy Vega-Lite workloads
and reports the memory each render allocates: use it to find hot spots, to measure an optimization before and after,
and to set the defaults of the work and paint limits (its calibration probes). It is compiled with the solution, so it
cannot rot, and is run by hand.

- **When**: a targeted profiling or optimization task (including an AI-agent task) starts here; run it before and
  after the change, and after any change to the work meter or the paint budget. New workloads belong in
  `Workloads.cs`.
- **Prerequisites**: the .NET SDK of `global.json`.
- **Run**: `dotnet run --project tools/DemaConsulting.CanvasNet.VegaLite.Benchmarks -c Release`
  (add `-- --smoke` for a quick check that everything still renders).

See [its README](DemaConsulting.CanvasNet.VegaLite.Benchmarks/README.md).

## visual-diff

**For** fidelity work. The scripts render the same input with the reference JavaScript renderer (Vega and Vega-Lite
in Node, or mermaid-cli in headless Chrome) and with CanvasNet, and write side-by-side and difference images with the
difference metrics (`compare-with-vega.ps1`, `compare-with-mermaid.ps1`). The generators regenerate the committed
Mermaid test data under `test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/`: `update-mermaid-goldens.ps1` the
reference layout goldens and `mermaid/gen-*.mjs` the upstream parser, shape, curve, theme, and dagre vectors.

- **When**: compare while implementing or fixing a rendering feature; regenerate when adding fixtures or upgrading the
  pinned mermaid, then commit the result (CI only compares).
- **Prerequisites**: `npm install` in the repository root (the pinned mermaid and mermaid-cli, which brings puppeteer
  with its headless Chrome; the repository commits no lockfile), PowerShell 7, the `canvasnet` tool (`dotnet canvasnet`
  or a local build), and Node.js 20 or later for the Mermaid scripts (18 or later is enough for the Vega scripts).

See [its README](visual-diff/README.md).

## Adding a Tool

A tool goes in `tools/<name>/` with its own section in this README. Shipped code (`src/`) must not reference it. A
.NET tool project is added to `CanvasNet.slnx` under the `/tools/` solution folder so that the solution build keeps
compiling it.
