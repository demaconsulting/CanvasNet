---
extends: mermaid/differences
---

## Command line

- info is the probe: a picture node without w and h can make the drawn diagram a
  little larger or smaller than the size info reports (with --width or --height
  it is fitted into the box). A render without --width and --height reports the
  natural size of the diagram it drew (the natural size of render --json).
- --background chooses another color under the diagram.
