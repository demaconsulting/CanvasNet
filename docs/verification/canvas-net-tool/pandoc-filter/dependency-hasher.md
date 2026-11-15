### DependencyHasher

#### Verification Approach

`DependencyHasher` is verified with unit tests in `DependencyHasherTests.cs` (xUnit). The tests call the internal entry
points `ForPicture` and `ForSvg` directly on real files: each test writes its models, charts, diagrams, drawings, data
files, and pictures to a temporary directory (`TempDirectory`) and passes the collapsed path of the picture
(`FilterPaths.Collapse`) and the file-access policy (`null` for the default mode, or a core `FileAccessPolicy`). Nothing
is stubbed: the walks read the file system, and `FilterPaths` and `PictureAttributes` are the real units. The assertions
read the label of each part (the text before its first line feed), the full bytes of a part where the format matters,
the composite flag, and the code and exact message of each `ToolException`.

#### Test Environment

N/A - standard test environment. The tests only need a writable temporary directory.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The parts are labeled from the picture's folder, hold the file's bytes after a line feed, and come in the
  documented order, each file once; a missing linked file is a `missing` part.
- The model, chart, diagram, and drawing walks find every documented link form and ignore the documented decoys.
- A drawing is composite only when it links, directly or through a nested drawing, a file only the tool draws.
- The 64-level and 512-file limits stop the build with `pandoc-filter-error` and the exact messages of the Lua filter.
- In a restrictive mode a file outside the folders, a file behind a link that leads outside them, and under
  `no-symlinks` a file reached through any link are not hashed, an allowed file is hashed exactly as in the default
  mode, and an unreadable document is the `cannot read` error.

#### Test Scenarios

**DependencyHasher_ForPicture_ModelIncludes_AreHashedOnceInOrder**: a model `a.scad` that includes `lib/b.scad`, which
uses `../a.scad` back and `c.scad` with spaces inside the brackets, and an include of a missing file in a comment, gives
the labels `a.scad`, `lib/b.scad`, `lib/c.scad` in that order (the cycle ends, the missing file is skipped), and the
first part is exactly `a.scad`, a line feed, and the model's text.

**DependencyHasher_ForPicture_ModelLimits_StopTheBuild**: a chain of 66 models fails with `'<f65.scad path>' is
included more than 64 levels deep`, and a model that includes 512 others fails with `the model and the files it
includes are more than 512 files; the limit is 512, counting the model (every file must be hashed so that a changed one
is drawn again)` and the code `pandoc-filter-error`.

**DependencyHasher_ForPicture_Chart_HashesDataPicturesAndNestedCharts**: a chart with a byte order mark, a data URL with
a backslash (`data\x.csv`), one with a literal `%20`, a remote URL, a `mark` with a `url` naming a nested chart, and a
`url` channel `field` whose inline rows name one picture twice gives `a.vl.json`, `b%20c.csv`, `data/x.csv`,
`img/q.png`, `b.vl.json`, `d.csv`: the data files sorted, the remote URL skipped, the picture once, and the nested chart
with its own data file labeled from its own folder while its link back to the outer chart is not hashed again.

**DependencyHasher_ForPicture_ChartLimits_StopTheBuild**: a specification nested 66 arrays deep fails with `the chart
specification is nested more than 64 levels deep; every data url must be hashed so that a changed data file is drawn
again`, and one that names 513 existing data files fails with `'many.vl.json' names 513 data files; the limit is 512
(every data file must be hashed so that a changed one is drawn again)`.

**DependencyHasher_ForPicture_Diagram_HashesEveryPictureForm**: a diagram with `img: "p.png"`, `img: 'q%20r.png'`
(percent-decoded to `q r.png`), unquoted `img: s.png , h: 3`, `<img src='t.png'>`, and `<IMG SRC=u.png/>` gives
`flow.mmd` and the five pictures sorted (`u.png/` collapses to `u.png`).

**DependencyHasher_ForSvg_Scanner_FindsOnlyRealImageLinks**: in a drawing with an XML declaration, a DOCTYPE whose
internal subset holds an image, an image in a comment, in CDATA, and in a processing instruction, a `>` inside a quoted
attribute, an `l:href` bound to the XLink namespace, an `x:href` bound to another namespace, an entity-encoded href with
surrounding spaces, and a missing diagram with a fragment, only the real links count: the result is composite and the
labels are `drawing.svg`, `missing gone.mmd`, `a&b.png`, `flow.mmd`, `flow2.mmd` (the missing part first, then the
existing files sorted, with no decoy as a missing entry).

**DependencyHasher_ForSvg_NestedDrawings_CompositeOnlyThroughADiagram**: a drawing that links only a PNG picture is not
composite; a drawing that links an inner drawing that links a Mermaid diagram is composite, with the labels `outer.svg`,
`inner.svg`, `flow.mmd`.

**DependencyHasher_ForPicture_Roots_SkipFilesOutside**: a model in `docs` that includes `../outside/b.scad` gives only
`a.scad` under the policy `roots` of `docs`, and `a.scad`, `../outside/b.scad` without a policy.

**DependencyHasher_ForPicture_RootsLinkLeadingOutside_IsNotHashed**: a model in `docs` that includes `lib/b.scad`,
where `docs/lib` is a directory link (a symbolic link, or a junction on Windows without the privilege) to a folder
outside `docs`, and `c.scad`, gives `a.scad`, `c.scad` under the policy `roots` of `docs` (the file behind the link is
not read), and `a.scad`, `lib/b.scad`, `c.scad` without a policy. The test is skipped when no directory link can be
created.

**DependencyHasher_ForSvg_NoSymlinksLinkedFile_IsNotHashed**: a drawing in `docs` that links `flow.mmd` and
`alias/inner.mmd`, where `docs/alias` is a directory link to `docs/real`, is composite and gives `d.svg`,
`missing alias/inner.mmd`, `flow.mmd` under `no-symlinks` and under `roots,no-symlinks` (the linked diagram is not
read), and `d.svg`, `alias/inner.mmd`, `flow.mmd` under `roots` alone (the link stays inside the folder). The test is
skipped when no directory link can be created.

**DependencyHasher_ForPicture_RestrictiveMode_HashesAllowedFilesIdentically**: under `roots,no-symlinks` of `docs`, a
model with an include and a chart with a data file and a nested diagram with a picture give the same parts, byte for
byte, as without a policy (the chart's labels `c.vl.json`, `d.csv`, `f.mmd`, `p.png`).

**DependencyHasher_ForPicture_DenyAll_ReadsOnlyTheSource**: under `DenyAll` a model that includes an existing file gives
only the model's part.

**DependencyHasher_ForPicture_MissingDocument_ThrowsCannotRead**: a `pdf` picture whose file does not exist fails with
`cannot read 'gone.pdf'`.
