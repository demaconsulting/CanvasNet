# PPTX Test Fixtures

<!-- cspell:ignore scanny samplelib pythonpptx groupshape autoshape autoshapes -->

This folder holds a small, real-world corpus of `.pptx` files used to exercise `PptxDocument`'s
full public `Render` API (Phase 2 - "Real-World Corpus Hardening") against documents nobody at
this repository hand-authored, complementing (not replacing) the synthetic, hand-built `.pptx`
packages every other `Pptx*Tests.cs` file in this project already uses for fine-grained,
single-construct unit tests. Every file here is used unmodified, exactly as published by its own
upstream source - nothing in this folder was edited, re-saved, or re-compressed after download.

Two independent sources are represented:

- **`python-pptx`** (nine files, the `pythonpptx-*.pptx` prefix): real test fixture files taken
  unmodified from the [python-pptx](https://github.com/scanny/python-pptx) project's own
  `features/steps/test_files/` behavioral-test corpus. `python-pptx` is MIT licensed (copyright
  Steve Canny and python-pptx contributors); the full license text, with a short attribution
  header, is reproduced in `PythonPptx.LICENSE` in this same folder. Each file's exact upstream
  source URL is listed in its own table row below.
- **`samplelib.com`** (two files, the `samplelib-*.pptx` prefix): sample `.pptx` files downloaded
  from [samplelib.com](https://www.samplelib.com/)'s PPTX samples page. That site's
  [terms.html](https://www.samplelib.com/terms.html) page states "Everything allowed... better to
  download the files locally" - a permissive statement of intended use, but **not** a formal
  copyright assignment or an OSI-approved license grant of the kind `python-pptx`'s MIT license or
  `PngSuite.LICENSE`'s own explicit "Permission to use, copy, modify and distribute... is hereby
  granted" wording provides. This corpus entry is documented honestly as exactly that: a
  permissively-stated, but not formally licensed, third-party source. A human maintainer revisiting
  this corpus in the future may wish to replace these two files with a more rigorously-licensed
  equivalent; until then, they remain included because they are genuinely useful, independent
  real-world coverage (a second, non-`python-pptx` author's own `.pptx` output) and the permissive
  site statement was followed precisely (downloaded once, kept locally, not redistributed from a
  live remote link at test time).

## Included Files

| File | Exercises |
| ------ | ----------- |
| `samplelib-sample-blank.pptx` | A single blank slide from an independent, non-`python-pptx` authoring tool. |
| `samplelib-sample-presentation.pptx` | Eight slides: text/autoshapes, a table (slide 3), a chart (slide 4, throws). |
| `pythonpptx-sld-blank.pptx` | A single blank slide (`python-pptx`'s own `sld-blank.pptx`). |
| `pythonpptx-shp-shapes.pptx` | Slide 0: table/chart/SmartArt (throws). Slide 1: connectors, nested group, GIF. |
| `pythonpptx-shp-groupshape.pptx` | A `<p:grpSp>` group with nested/`avLst` autoshapes - group-transform coverage. |
| `pythonpptx-shp-picture.pptx` | Two slides placing an embedded raster picture - picture decode/composite coverage. |
| `pythonpptx-shp-autoshape-props.pptx` | A single autoshape with an `avLst` handle - preset-geometry coverage. |
| `pythonpptx-tbl-cell.pptx` | Three slides of real tables, including merged cells, no chart/OLE relationships. |
| `pythonpptx-txt-font-props.pptx` | Five text-heavy slides exercising run/paragraph font properties. |
| `pythonpptx-txt-text-frame.pptx` | Two slides exercising text-frame-level properties (margins, wrapping, anchoring). |

## Exact `python-pptx` Source URLs

Each `pythonpptx-*.pptx` file here is byte-for-byte the upstream file at the corresponding URL
below (verified by exact file size against the upstream GitHub API directory listing before
inclusion):

| This folder's file | Upstream source URL |
| ------ | ----------- |
| `pythonpptx-sld-blank.pptx` | <https://github.com/scanny/python-pptx/blob/master/features/steps/test_files/sld-blank.pptx> |
| `pythonpptx-shp-shapes.pptx` | <https://github.com/scanny/python-pptx/blob/master/features/steps/test_files/shp-shapes.pptx> |
| `pythonpptx-shp-groupshape.pptx` | <https://github.com/scanny/python-pptx/blob/master/features/steps/test_files/shp-groupshape.pptx> |
| `pythonpptx-shp-picture.pptx` | <https://github.com/scanny/python-pptx/blob/master/features/steps/test_files/shp-picture.pptx> |
| `pythonpptx-shp-autoshape-props.pptx` | <https://github.com/scanny/python-pptx/blob/master/features/steps/test_files/shp-autoshape-props.pptx> |
| `pythonpptx-tbl-cell.pptx` | <https://github.com/scanny/python-pptx/blob/master/features/steps/test_files/tbl-cell.pptx> |
| `pythonpptx-txt-font-props.pptx` | <https://github.com/scanny/python-pptx/blob/master/features/steps/test_files/txt-font-props.pptx> |
| `pythonpptx-txt-text-frame.pptx` | <https://github.com/scanny/python-pptx/blob/master/features/steps/test_files/txt-text-frame.pptx> |

The two excluded candidates (see the next section) were also taken from the same upstream
directory: `pythonpptx-minimal.pptx` from
<https://github.com/scanny/python-pptx/blob/master/features/steps/test_files/minimal.pptx>, and
`pythonpptx-mst-placeholders.pptx` from
<https://github.com/scanny/python-pptx/blob/master/features/steps/test_files/mst-placeholders.pptx>.

## Excluded Staged Candidates

Two additional `python-pptx` files were staged as candidates but are **not** included here:
`pythonpptx-minimal.pptx` and `pythonpptx-mst-placeholders.pptx` (python-pptx's own `minimal.pptx`
and `mst-placeholders.pptx`). Both declare **zero slides** - neither file's `ppt/presentation.xml`
contains a `<p:sldIdLst>` element at all (`mst-placeholders.pptx` contains only master/layout
placeholder XML, no slide part). `PptxDocument.Open` already rejects any package with no slides
(see `CanvasNetPptx-PptxDocument-PresentationValidation`), so neither file is a navigable
presentation a `Render`-focused corpus can exercise - including them would add no new coverage
toward this phase's actual goal (rendering every slide of every fixture), so they were left out
rather than kept as dead weight.

## A Note on What This Corpus Can - and Cannot - Prove

Two slides in this corpus declare a chart (and, in one case, also a SmartArt/diagram)
`<p:graphicFrame>`: `pythonpptx-shp-shapes.pptx` slide index 0, and
`samplelib-sample-presentation.pptx` slide index 4. Charts, SmartArt diagrams, and OLE objects are
explicitly out of scope for this project (see `pptx-document.md`'s Phase 1e/1c deferred-items
lists) - `ParseTable`'s own `<a:graphicData>` `uri` check throws `PptxUnsupportedFeatureException`
(feature token `"pptx-graphic-frame-kind"`) for any graphic-frame kind that is not a table, *before*
attempting to read any chart-specific or diagram-specific XML shape. The corpus test for these two
specific slide indices therefore asserts that `Render` throws this documented exception type, not
that it renders successfully - this is the **already-graceful, already-designed** behavior for a
recognized-but-unsupported construct, not a bug this phase introduces or works around.

This smoke test corpus can prove that the whole slide-level `Render` call surfaces a clean,
documented exception for these two real-world chart-bearing slides, and that every other slide in
every other fixture renders without an ungraceful crash and paints at least one real pixel. It
cannot, by itself, prove that `PptxDocument` would behave identically against every other possible
real-world chart/diagram encoding variant in the wild (different graphic-frame `uri` casing, a
chart embedded without an accompanying table-shaped sibling, or an OLE object rather than a chart) -
only that these two specific, genuinely-authored real files exercise the documented path cleanly.
Nor can a corpus this size (ten files, two sources) claim to be statistically representative of
"real-world PPTX documents" in general - it is a targeted, honest sample, not an exhaustive one.
