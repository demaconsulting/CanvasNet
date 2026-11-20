---
extends: svg/fragments
examples:
  - name: render-embed-pages
    kind: command
    language: shell
    content: 'canvasnet render pages.svg --strict -o pages.png'
    description: Draw that diagram and fail if a page does not exist.
---

## Command line

- **options**: azimuth= and elevation= turn a model like --azimuth and
  --elevation, projection= like --projection, and theme= like --theme;
  def.<name>=<value> is -D name=value of the command line (a def. name the model
  does not assign creates the variable, as -D does), and param.<name>=<value> is
  --param.
- **command-line input**: An input path of the command line takes the same
  fragment: canvasnet render "report.pdf#page=3" -o p3.png, canvasnet render
  "chart.vl.json#param.t=5" -o c.png, canvasnet render "flow.mmd#theme=dark" -o
  f.png, canvasnet montage "part.scad#def.explode=0" "part.scad#def.explode=1"
  --labels -o m.png, canvasnet compare "a.scad#view=top" "b.scad#view=top". It
  works for render and its verbs, inspect, info, compare, and montage, and it
  overrides --page, --view, --azimuth, --elevation, --projection, --theme, -D,
  and --param for that input. Quote the input in a shell: an & ends a command
  (bash, zsh) or is reserved (PowerShell), so "part.scad#view=top&def.width=20"
  needs quotes; a # at the start of a word begins a comment in bash. An unknown
  or repeated parameter name is a warning, also with --strict. Labels of montage
  show the fragment.
