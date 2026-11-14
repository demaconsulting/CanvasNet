---
title: Git graphs
summary: Commits, branches, checkouts, merges, cherry-picks, titles, and colors of a git graph.
aliases:
  - git
children:
  - settings
see-also:
  - mermaid/style
  - mermaid/options
  - mermaid/diagnostics
examples:
  - name: gitgraph
    kind: source
    language: mermaid
    file: gitgraph.mmd
    description: A small git graph with three branches, a branch order, a highlighted and a reversed commit, a merge, a cherry-pick with its default tag, a tag, and a title.
---

Git graphs: a git graph starts with gitGraph. It is drawn by a port of Mermaid's
git graph renderer, so the commits, the branch lines and labels, the arrows, the
commit ids, the tags, and the title land where Mermaid 11 puts them; its syntax
and settings are listed below and on the settings card (`canvasnet help mermaid
gitgraph settings`).

## Syntax

- Header: `gitGraph`, `gitGraph:`, or `gitGraph` with a direction and a colon
  (`gitGraph LR:`, `gitGraph TB:`, or `gitGraph BT:`), left to right when no
  direction is given; the first statement can follow on the same line, and a
  direction without its colon is a `mermaid-syntax-error`.
- Commits: `commit`, with the options `id: "..."`, `msg: "..."` (or the message
  alone, in quotes), `tag: "..."`, and `type: NORMAL`, `REVERSE`, or
  `HIGHLIGHT`, in any order; a repeated option keeps its last value and the tags
  add up; a commit without an `id` is given one such as `0-eb28011`.
- Branches: `branch name`, the name written with letters, digits, `_`, `-`, `.`,
  and `/`, or in quotes, with an optional `order: 2`; the branch starts at the
  current commit and is checked out; the branches are drawn sorted by their
  order (0 when it is not given), in the order they were made where it is the
  same.
- Checkouts: `checkout name` or `switch name`.
- Merges: `merge name`, with the options `id: "..."`, `tag: "..."`, and `type:
  ...`; a merge commit shows its id only when it is given.
- Cherry-picks: `cherry-pick id: "..."`, with the options `tag: "..."` (by
  default the tag is `cherry-pick:` and the id; an empty tag draws none) and
  `parent: "..."` (the parent to take, which a merge commit needs).
- Errors: an operation that Mermaid's git database rejects (a merge of a branch
  into itself, of a branch that does not exist or has no commits, a `checkout`
  of a branch that does not exist, a second `branch` of the same name, a
  `cherry-pick` of a commit that does not exist or is on the current branch, and
  so on) is a `mermaid-syntax-error` with Mermaid's message, located at the
  statement.
- Titles: `title`, else the frontmatter `title`, is drawn centered above the
  graph; `accTitle` and `accDescr` (on one line, or in braces over several
  lines) are read as metadata.
- Texts: the commit ids, the tags, and the branch names are SVG text, cleaned as
  Mermaid cleans them, so entity codes (`#35;`) are drawn as their characters
  and `<`, `>`, and `=` as written; a branch name is split into rows at `<br>`
  and `\n` and keeps its spaces; the message of a commit is not drawn.
- Comments: `%%` comments, on their own line or after a statement.
- Colors: the theme variables `git0` to `git7` (the commits, branch lines, and
  arrows), `gitInv0` to `gitInv7` (highlighted commits), `gitBranchLabel0` to
  `gitBranchLabel7`, `commitLabelColor`, `commitLabelBackground`,
  `commitLabelFontSize`, `tagLabelColor`, `tagLabelBackground`,
  `tagLabelBorder`, and `tagLabelFontSize`; the ninth branch takes the colors of
  the first again.
- Configuration: YAML frontmatter, `%%{init: ...}%%` directives, the five
  themes, `themeVariables`, `themeCSS` (the git graph's own classes, such as
  `.commit0`, `.branch-label0`, `.arrow0`, `.commit-label`, and `.tag-label`),
  `look: neo` (a shadow under the branch labels), and the git graph settings
  below, with Mermaid's precedence.
