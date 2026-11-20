---
title: 'Git graphs: settings and known differences'
summary: The git graph settings and the known differences of a git graph.
see-also:
  - mermaid/gitgraph
  - mermaid/differences
---

Set these in the YAML frontmatter (`config:`) or in an `%%{init: ...}%%`
directive, with Mermaid's precedence.

## Settings

- `gitGraph.showBranches` (default `true`): `false` draws neither the branch
  lines nor their labels.
- `gitGraph.showCommitLabel` (default `true`): `false` draws no commit ids (the
  tags are still drawn).
- `gitGraph.rotateCommitLabel` (default `true`): Turns the commit ids by 45
  degrees; `false` draws them level under the commits.
- `gitGraph.parallelCommits` (default `false`): `true` places each commit one
  step after its parent, so commits made on different branches can stand side by
  side.
- `gitGraph.mainBranchName` (default `main`): The name of the first branch.
- `gitGraph.mainBranchOrder` (default 0): The `order` of the main branch among
  the other branches.
- `gitGraph.titleTopMargin` (default 25): How far above the graph the title is
  drawn.
- `gitGraph.diagramPadding` (default 8): The margin around the graph.
- `gitGraph.useMaxWidth` (default `true`): No meaning for a picture.

## Known differences

- A commit without an id is given an id of the same form as Mermaid's (its
  number, a dash, and seven hexadecimal digits, such as 0-eb28011), but the
  digits come from a fixed pseudo-random sequence that starts again for every
  diagram, where Mermaid draws random digits in every render, so the picture is
  the same every time.
- MaxNodes counts the main branch, the branches, and the commits of a git graph;
  MaxLabelLength also bounds the messages and tags that Mermaid composes for
  merges and cherry-picks, so a long chain of cherry-picks of cherry-picks fails
  with mermaid-limit-exceeded; a git graph of more than about 650 commits in one
  direction needs --scale, --width, or --height to fit the largest picture.
- A git graph setting of the wrong type is mermaid-config-invalid and ignored. A
  site configuration that renames or reorders the main branch runs the
  statements again when the git graph is drawn, where an operation that then
  fails is a located mermaid-render-failed; where Mermaid's git graph renderer
  fails (Closest parent not found for commit ...), the render is a located
  mermaid-render-failed.
- The width of a commit id or tag of a git graph can be up to a pixel narrower
  than Chrome's for a few glyphs at a few sizes (such as a 10-pixel id that ends
  in D); a commit id used again replaces the earlier commit without a report, as
  in Mermaid, which only logs a warning.
