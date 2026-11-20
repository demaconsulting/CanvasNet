---
extends: scad/views
examples:
  - name: render-top
    kind: command
    language: shell
    content: 'canvasnet render plate.scad --view top --size 400x400 -o plate-top.png --json'
    description: Draw plate.scad from above at 400x400 and print the JSON result.
  - name: free-view
    kind: command
    language: shell
    content: 'canvasnet render plate.scad --azimuth 30 --elevation 20 --projection perspective -o plate.png'
    description: Draw plate.scad from a free viewpoint with a perspective camera.
---

## Command line

- `--view <name>`: The viewpoint (View); --view, --azimuth, and --elevation win
  over the model and over def.$vpr.
- `--azimuth <degrees>`: Replace the azimuth of the view.
- `--elevation <degrees>`: Replace the elevation of the view, -90 to 90.
- `--projection <type>`: ortho (default) or perspective; projection= in an input
  fragment replaces the --projection of the command.
- `--size <WxH>`: The picture is 800x800 pixels unless a size option is given;
  --width, --height, and --scale also size it.
- A fixed camera: canvasnet montage "part.scad#def.explode=0"
  "part.scad#def.explode=1" -D '$vpd=300' -D '$vpt=[0,0,50]' --labels -o m.png
  shows the explode levels at one scale.
- Quoting: 'part.scad#azimuth=45&elevation=20' needs quotes;
  'part.scad#def.$vpr=[70,0,45]' is --azimuth 45 --elevation 20, and render
  --json reports the azimuth and elevation used. Use single quotes in a shell,
  where $ expands inside double quotes, or write the $ as %24.
