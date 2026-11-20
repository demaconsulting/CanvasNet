---
extends: scad/differences
---

## Command line

- `--file-access` chooses which local files include and use may read.
- A fragment turns a model as --azimuth, --elevation, and --projection do, and
  def.<name>=<value> is -D name=value.
- An input path of the command line takes the same fragment (canvasnet render
  "part.scad#view=front&def.width=20" -o part.png).
