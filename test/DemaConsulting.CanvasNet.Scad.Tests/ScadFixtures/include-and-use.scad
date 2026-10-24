// Original CanvasNet test fixture: include and use directives.
// The paths below are only recorded by the parser and intentionally do not exist.
include <parts/frames.scad>
use <lib/gears.scad>
include <./relative/../nested/deep/path/box.scad>
use <fasteners.scad>;

frame_size = 5;
include_flag = true;
user = 1;

cube(frame_size);
