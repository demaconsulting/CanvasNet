# CanvasNet Vega-Lite Benchmarks

A small Stopwatch harness for the Vega-Lite renderer (`DemaConsulting.CanvasNet.VegaLite`). It is **developer
tooling**: the solution build compiles it, so it cannot rot, but neither the build nor CI ever runs it, it is not
packed, and it has no dependency beyond the library itself (no BenchmarkDotNet).

## What it measures

Two tables:

1. **Workloads.** Heavy charts rendered with the default limits, each parsed once and rendered after one warm-up
   render: a large scatter plot (100,000 points), stacked bars (200,000 rows summed into 400 categories by 6
   series), a 6x6 facet grid (60,000 rows), a loess smoother, a kernel density estimate (200,000 rows), a cubic
   regression (200,000 rows), a running mean, 2,000 image marks (a resolver that hands out solid 24x24 pictures),
   and 200 rects that each cover the whole 1000x800 view (painted pixels). For each it prints the median and
   minimum time of the runs, the median time per row (or image, or rect), and the memory allocated by one render.
2. **Calibration probes.** The work meter (`MaxEvaluationSteps`, 50,000,000 by default) and the paint budget
   (`MaxPaintedPixels`, 250,000,000 by default) promise to stop a runaway specification within about ten seconds.
   A probe is a specification that costs far more than a *tightened* limit (a fiftieth of the step budget, a
   twenty-fifth of the pixel budget); it is rendered until the limit stops it, and the time is multiplied by the
   factor to estimate how long the default limit takes. A probe that is not stopped by the expected limit (or is
   stopped by another one) is reported and makes the exit code 1. The families are a loess smoother with the
   bandwidth 1, a density with 1,000 steps, a polynomial regression of order 8, a running median over a wide
   frame, a bootstrap confidence interval, and full-surface rects.

Every specification is made of a `sequence` and `calculate` transforms, so there are no data files and the rows are
a function of their index: the same on every machine.

## Running it

```powershell
# Full sizes, 5 timed runs each (a few minutes)
dotnet run --project tools/DemaConsulting.CanvasNet.VegaLite.Benchmarks -c Release

# A quick check that every workload and probe still renders (seconds; the probes are too small to reach a limit)
dotnet run --project tools/DemaConsulting.CanvasNet.VegaLite.Benchmarks -c Release -- --smoke

# Options: --runs N, --filter TEXT (a name contains TEXT), --no-calibration, --help
dotnet run --project tools/DemaConsulting.CanvasNet.VegaLite.Benchmarks -c Release -- --runs 3 --filter loess
```

Always use the Release configuration, and compare runs only on one machine and one .NET version.

## A reference run

Phase 14, .NET 10.0.14, an x64 desktop with 28 logical processors, Windows, 3 timed runs. The numbers **depend on the
machine** (and on the garbage collector: the first run of the scatter plot took half as long as the median);
they are a guide to the order of magnitude, not a promise.

| Workload | Units | Median ms | Min ms | ns per unit | Allocated MB |
| -------- | ----: | --------: | -----: | ----------: | -----------: |
| scatter | 100,000 rows | 2,317 | 1,125 | 23,166 | 1,014 |
| stacked-bars | 200,000 rows | 683 | 656 | 3,416 | 237 |
| facet-grid | 60,000 rows | 2,853 | 2,844 | 47,551 | 1,922 |
| loess | 5,000 rows | 224 | 200 | 44,818 | 16 |
| density | 200,000 rows | 1,059 | 1,036 | 5,293 | 51 |
| regression | 200,000 rows | 332 | 330 | 1,660 | 78 |
| window | 50,000 rows | 340 | 327 | 6,807 | 134 |
| images | 2,000 images | 144 | 133 | 71,802 | 12 |
| full-rects | 200 rects (160 M pixels) | 7,885 | 7,559 | 39,423,463 | 10 |

| Probe | Stopped after ms | Factor | Estimated seconds at the default limit |
| ----- | ---------------: | -----: | -------------------------------------: |
| loess (bandwidth 1, 20,000 rows) | 33 | 50 | 1.7 |
| density (1,000 steps) | 58 | 50 | 2.9 |
| polynomial regression (order 8) | 63 | 50 | 3.1 |
| running median (frame of 10,001) | 193 | 50 | 9.7 |
| bootstrap confidence interval | 76 | 50 | 3.8 |
| full-surface rects | 480 | 25 | 12.0 |

Reading the calibration: the default limits stop every family within about ten seconds on this machine. The
running median (9.7 s) and the painter (12 s, about 48 ns for each pixel of a half-transparent full-surface rect) are
the slowest to be stopped; loess is the most conservative (1.7 s). A change to a weight of the work meter
(`Transforms/WorkMeter.cs`) or to the paint budget (`Scenegraph/ScenePainter.Budget.cs`) should be followed by a run of
the probes.
